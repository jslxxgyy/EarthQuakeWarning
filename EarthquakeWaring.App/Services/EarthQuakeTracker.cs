using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using EarthquakeWaring.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EarthquakeWaring.App.Infrastructure.Models.ApiModels;

namespace EarthquakeWaring.App.Services;

public class EarthQuakeTracker : IEarthQuakeTracker
{
    private readonly IEarthQuakeApiWrapper _earthQuakeApi;
    private readonly IEarthQuakeCalculator _earthQuakeCalculator;
    private readonly ISetting<CurrentPosition> _currentPosition;
    private readonly ISetting<TrackerSetting> _trackerSetting;
    private readonly ILogger<EarthQuakeTracker> _logger;
    private readonly IServiceProvider _service;

    private EarlyWarningWindow? _warningWindow;
    private EarthQuakeTrackingInformation _trackingInformation = new();

    private CancellationTokenSource? _tokenSource;
    private CancellationToken _cancellationToken;
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private bool _isFirstCheck = true;
    private bool _hasArrived;  // 标记地震已到达，停止 API 轮询
    private bool _countDownInitialized; // 是否已完成首次倒计时初始化
    private DispatcherTimer? _countDownTimer;

    public EarthQuakeTracker(IEarthQuakeApiWrapper earthQuakeApi, IEarthQuakeCalculator earthQuakeCalculator,
                             ISetting<CurrentPosition> currentPosition, ILogger<EarthQuakeTracker> logger,
                             IServiceProvider service, ISetting<TrackerSetting> trackerSetting)
    {
        _earthQuakeApi = earthQuakeApi;
        _earthQuakeCalculator = earthQuakeCalculator;
        _currentPosition = currentPosition;
        _logger = logger;
        _service = service;
        _trackerSetting = trackerSetting;
    }

    public TimeSpan SimulateTimeSpan { get; set; } = TimeSpan.Zero;
    public List<EarthQuakeInfoBase>? SimulateUpdates { get; set; } = null;

    public async Task StartTrack(EarthQuakeInfoBase earthQuakeInfo, CancellationTokenSource cancellationTokenSource)
    {
        _tokenSource = cancellationTokenSource;
        _cancellationToken = cancellationTokenSource.Token;
        try
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await CheckEarthQuake(earthQuakeInfo).ConfigureAwait(false);

                // 地震已到达后降频，减少无意义的轮询
                var delayMs = _hasArrived
                    ? 10_000
                    : (_trackerSetting?.Setting?.TrackerTimeSpanMillisecond * 100 ?? 2000);
                await Task.Delay(delayMs, _cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，不做处理
        }
        finally
        {
            // 清理资源
            StopCountdownTimer();
            _warningWindow = null;
            _tokenSource?.Dispose();
            _tokenSource = null;
        }
    }

    private async Task CheckEarthQuake(EarthQuakeInfoBase earthQuakeInfo)
    {
        _logger.LogDebug("Checking earthquake shocking at {Time} at {Position} with Magnitude {DayMagnitude}",
                              earthQuakeInfo.StartAt,
                              earthQuakeInfo.PlaceName,
                               earthQuakeInfo.Magnitude);
        if (SimulateUpdates is null && (DateTime.Now - earthQuakeInfo.StartAt).TotalMinutes > 5)
        {
            _logger.LogWarning("Earthquake Expired 5 minutes, exit");
            _tokenSource?.Cancel();
            return;
        }

        var timeHandler = _service.GetService<ITimeHandler>();

        // 首次检查：立即使用初始数据弹窗，不等待 API 响应
        // 后续检查：获取 API 最新数据以更新倒计时等信息
        // 如果地震已到达，不再请求 API，仅使用已有数据
        EarthQuakeInfoBase latestInfo;
        if (_isFirstCheck)
        {
            _isFirstCheck = false;
            // 模拟时优先使用 SimulateUpdates 中的最新数据，避免首次使用空对象导致计算错误
            if (SimulateUpdates != null && SimulateUpdates.Count > 0)
                latestInfo = SimulateUpdates[^1];
            else
                latestInfo = earthQuakeInfo;
        }
        else if (_hasArrived)
        {
            latestInfo = earthQuakeInfo;
            // 地震已到达：正计时超过"警报持续时间 + 缓冲"后退出，避免 tracker 永久运行泄漏
            var alertDuration = _trackerSetting?.Setting?.AlertDurationSecond ?? 30;
            if ((DateTime.Now + timeHandler!.Offset - SimulateTimeSpan - latestInfo.StartAt).TotalSeconds >
                _trackingInformation.TheoryCountDown + alertDuration + 30)
            {
                _logger.LogDebug("Earthquake arrived and elapsed {Elapsed}s, quitting tracker.",
                    (DateTime.Now + timeHandler!.Offset - SimulateTimeSpan - latestInfo.StartAt).TotalSeconds);
                _tokenSource?.Cancel();
                return;
            }
        }
        else
        {
            var infos = SimulateUpdates;
            if (infos is null)
            {
                var fetched = await _earthQuakeApi.GetEarthQuakeInfo(earthQuakeInfo.Id, _cancellationToken);
                if (fetched.Count > 0)
                {
                    infos = fetched;
                    _lastUpdateTime = fetched[^1].UpdateAt;
                }
            }

            infos ??= new List<EarthQuakeInfoBase> { earthQuakeInfo };
            if (infos.Count == 0)
                infos = new List<EarthQuakeInfoBase> { earthQuakeInfo };

            if (SimulateTimeSpan == TimeSpan.Zero)
            {
                latestInfo = infos[^1];
            }
            else
            {
                _logger.LogWarning("Simulating with simulatingInfo");
                var simulatingInfo =
                    infos.FirstOrDefault(t => t.UpdateAt > DateTime.Now + timeHandler!.Offset - SimulateTimeSpan);
                if (infos.Count <= 0 && simulatingInfo == null) return;
                simulatingInfo ??= infos[^1];
                latestInfo = simulatingInfo;
            }

            if ((DateTime.Now + timeHandler!.Offset - SimulateTimeSpan - latestInfo.StartAt).TotalSeconds >
                _trackingInformation.TheoryCountDown + 30)
            {
                _logger.LogDebug("Earthquake Expired for {Time} but theory {Theory} Quitting.",
                                       (DateTime.Now + timeHandler!.Offset - SimulateTimeSpan - latestInfo.UpdateAt)
                                       .TotalSeconds,
                                       _trackingInformation.TheoryCountDown + 30);
                _tokenSource?.Cancel();
                return;
            }
        }

        // 先做所有计算，最后一次性 Invoke 到 UI 线程更新 UI 和弹窗
        var position = latestInfo.PlaceName;
        var startTime = latestInfo.StartAt;
        var updateTime = latestInfo.UpdateAt;
        var depth = latestInfo.Depth;
        var latitude = latestInfo.Latitude;
        var longitude = latestInfo.Longitude;
        var id = latestInfo.Id;
        var magnitude = latestInfo.Magnitude;

        // 标记本次事件是否已经结束（到达后超过警报时长），结束则不再弹窗并退出跟踪
        var shouldQuit = false;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _trackingInformation.Position = position;
            _trackingInformation.StartTime = startTime;
            _trackingInformation.UpdateTime = updateTime;
            _trackingInformation.Depth = depth;
            _trackingInformation.Latitude = latitude;
            _trackingInformation.Longitude = longitude;
            _trackingInformation.Id = id;
            _trackingInformation.Magnitude = magnitude;

            if (_currentPosition.Setting != null)
            {
                _trackingInformation.Distance = _earthQuakeCalculator.GetDistance(_currentPosition.Setting.Latitude,
                    _currentPosition.Setting.Longitude, latitude, longitude);
                _trackingInformation.TheoryCountDown =
                    (int)_earthQuakeCalculator.GetCountDownSeconds(depth, _trackingInformation.Distance);
                _trackingInformation.Intensity =
                    _earthQuakeCalculator.GetIntensity(magnitude, _trackingInformation.Distance);
                _trackingInformation.Stage = GetEarthQuakeAlertStage(_trackingInformation);

                // ── 倒计时计算：首个周期用公式估算，后续周期按真实挂钟时间推进 ──
                if (!_countDownInitialized)
                {
                    // 首次：用公式从地震发震时间推算初始剩余秒数
                    var elapsedSeconds = (int)((DateTime.Now + timeHandler!.Offset - SimulateTimeSpan -
                                                startTime).TotalSeconds);
                    var computedCountdown = _trackingInformation.TheoryCountDown - elapsedSeconds;
                    _trackingInformation.CountDown = Math.Max(0, computedCountdown);
                    _logger.LogWarning("[CountDownTrace] INITIALIZED: TheoryCountDown={Theory}, ElapsedSec={Elapsed}, CountDown={Val}",
                        _trackingInformation.TheoryCountDown, elapsedSeconds, _trackingInformation.CountDown);
                    _countDownInitialized = true;
                    StartCountdownTimer();

                    if (computedCountdown <= 0)
                    {
                        _hasArrived = true;
                        _trackingInformation.CountDown = 0;

                        // 地震已到达且超过"警报时长 + 缓冲"仍被拉起跟踪：
                        // 说明是 API 重复返回的旧事件，直接退出，不再弹窗，避免叉掉后又弹出。
                        var alertDuration = _trackerSetting?.Setting?.AlertDurationSecond ?? 30;
                        if (elapsedSeconds > _trackingInformation.TheoryCountDown + alertDuration + 10)
                            shouldQuit = true;
                    }
                }

                if (_warningWindow != null)
                    return;

                // 地震已经结束的事件直接退出，不弹窗
                if (shouldQuit)
                    return;

                // 烈度小于一级无需弹窗
                if (_trackingInformation.Intensity < 1)
                    return;

                _logger.LogInformation(
                    "Intensity={Intensity} reached, POPING",
                    _trackingInformation.Intensity);
                _warningWindow = new EarlyWarningWindow(_trackingInformation, _service);
                _warningWindow.Show();
            }
        }, DispatcherPriority.Send, _cancellationToken);

        // 该地震已结束（到达后超过警报时长），停止跟踪，防止后续再次弹窗
        if (shouldQuit)
        {
            _logger.LogWarning("Earthquake already finished, skip popup and quit tracker.");
            _tokenSource?.Cancel();
        }
    }

    private void StartCountdownTimer()
    {
        if (_countDownTimer != null)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        _countDownTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countDownTimer.Tick += OnCountdownTick;
        _countDownTimer.Start();
    }

    private void StopCountdownTimer()
    {
        if (_countDownTimer == null)
            return;

        _countDownTimer.Stop();
        _countDownTimer.Tick -= OnCountdownTick;
        _countDownTimer = null;
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        UpdateCountdownBySecond();
    }

    private void UpdateCountdownBySecond()
    {
        if (!_countDownInitialized)
            return;

        // 基于挂钟时间重算，而非逐 tick 累加，避免 UI 线程繁忙时计时漂移。
        var timeHandler = _service.GetService<ITimeHandler>();
        var elapsedSeconds = (int)((DateTime.Now + (timeHandler?.Offset ?? TimeSpan.Zero) - SimulateTimeSpan -
                                    _trackingInformation.StartTime).TotalSeconds);
        var value = _trackingInformation.TheoryCountDown - elapsedSeconds;

        if (value > 0)
        {
            // 尚未到达：倒计时
            _trackingInformation.CountDown = value;
        }
        else
        {
            // 已到达（或恰好到达）：正计时，CountDown 为 0 或负数
            if (!_hasArrived)
            {
                _hasArrived = true;
                _trackingInformation.CountDown = 0;
            }
            else
            {
                _trackingInformation.CountDown = value; // 负数，绝对值即"到达后第 N 秒"
            }
        }
    }

    public static EarthQuakeStage GetEarthQuakeAlertStage(EarthQuakeTrackingInformation information)
    {
        return information.Intensity switch
               {
                   >= 5         => EarthQuakeStage.Forced,
                   >= 3 and < 5 => EarthQuakeStage.Emergency,
                   >= 1         => EarthQuakeStage.Warning,
                   < 1          => EarthQuakeStage.Record,
                   _            => EarthQuakeStage.Record
               };
    }

    public static bool ShouldPopupAlert(EarthQuakeTrackingInformation information, AlertLimit alertLimit)
    {
        if (information.UpdateTime.Hour is >= 7 and <= 22)
        {
            // 日间，仅校核震级（烈度判定已移除）
            return information.Magnitude >= alertLimit.DayMagnitude;
        }

        // 夜间，仅校核震级（烈度判定已移除）
        return information.Magnitude >= alertLimit.NightMagnitude;
    }
}