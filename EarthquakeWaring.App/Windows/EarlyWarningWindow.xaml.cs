using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace EarthquakeWaring.App.Windows;

public partial class EarlyWarningWindow : Window
{
    private readonly EarthQuakeTrackingInformation _information;
    private readonly IServiceProvider _service;
    private readonly ISetting<TrackerSetting> _trackerSetting;
    private IVolumeManager? _volumeManager;
    private int _lastPlayedCountdown = -1;

    // 音频文件路径
    private static readonly string AlarmDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alarm");
    private static readonly string NumbersDir = Path.Combine(AlarmDir, "numbers");
    private static readonly string LevelFile = Path.Combine(AlarmDir, "level.mp3");
    private static readonly string ArrivingFile = Path.Combine(AlarmDir, "arriving.mp3");
    private static readonly string BeginningFile = Path.Combine(AlarmDir, "beginning.mp3");
    private System.Threading.Timer? _autoCloseTimer;
    private MediaPlayer? _arrivingLoopPlayer;

    // ── MediaPlayer 缓存 ──
    // 预打开所有音频，等待 MediaOpened 就绪后标记可用。
    private static readonly Dictionary<string, MediaPlayer> _preloadedPlayers = [];
    private static readonly HashSet<MediaPlayer> _preloadedPlayerSet = [];
    private static readonly HashSet<string> _readyFiles = [];
    private static bool _preloadStarted;
    private static readonly object _preloadLock = new();

    // 活跃播放器引用（阻止 GC，关闭时统一停止）
    private static readonly List<MediaPlayer> _activePlayers = [];
    private static readonly object _audioLock = new();

    public EarlyWarningWindow(EarthQuakeTrackingInformation information, IServiceProvider service)
    {
        _information = information;
        _service = service;
        _trackerSetting = _service.GetRequiredService<ISetting<TrackerSetting>>();
        _volumeManager = _service.GetRequiredService<IVolumeManager>();
        DataContext = information;
        InitializeComponent();

        if (_trackerSetting.Setting?.MaximumVolume is not false)
            _volumeManager?.SetVolumeToMax();

        // 窗口显示后播放开始提示音
        Loaded += (_, _) => PlayOrEnqueue(BeginningFile);

        _information.PropertyChanged += InformationOnPropertyChanged;
    }

    // ── 预加载（程序启动时调用） ──

    /// <summary>
    /// 在 App 启动时预加载所有报警音频到内存。
    /// 创建 MediaPlayer 并调用 Open（异步加载），MediaOpened 触发后标记为"就绪"。
    /// </summary>
    public static void InitializeAudio()
    {
        if (_preloadStarted) return;
        lock (_preloadLock)
        {
            if (_preloadStarted) return;
            _preloadStarted = true;

            try
            {
                // 预加载数字 0-9 和 10（"十"）的音频（用于逐位播报和"几十几"组合播报）
                for (var i = 0; i <= 10; i++)
                {
                    var file = Path.Combine(NumbersDir, $"{i}.mp3");
                    if (File.Exists(file)) OpenPlayer(file);
                }
                // 预加载等级提示音、开始提示音、地震波到达提示音
                foreach (var file in new[] { LevelFile, BeginningFile, ArrivingFile })
                {
                    if (File.Exists(file)) OpenPlayer(file);
                }
            }
            catch { }
        }
    }

    private static void OpenPlayer(string filePath)
    {
        var player = new MediaPlayer();
        player.MediaOpened += OnMediaOpened;
        player.Open(new Uri(filePath, UriKind.Absolute));
        _preloadedPlayers[filePath] = player;
        _preloadedPlayerSet.Add(player);
        lock (_audioLock) _activePlayers.Add(player);
    }

    private static void OnMediaOpened(object? sender, EventArgs e)
    {
        if (sender is not MediaPlayer player) return;
        lock (_readyFiles)
        {
            foreach (var kvp in _preloadedPlayers)
            {
                if (kvp.Value == player)
                {
                    _readyFiles.Add(kvp.Key);
                    break;
                }
            }
        }
    }

    // ── 播放 ──

    /// <summary>
    /// 播放音频。若未就绪则等待 MediaOpened 后再播。
    /// </summary>
    private static void PlayOrEnqueue(string filePath)
    {
        if (!_preloadedPlayers.TryGetValue(filePath, out var player)) return;

        lock (_readyFiles)
        {
            if (_readyFiles.Contains(filePath))
            {
                PlayFromStart(player);
                return;
            }
        }

        // 未就绪 → 订阅 MediaOpened 待就绪后立即播放
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            player.MediaOpened -= handler;
            lock (_readyFiles) _readyFiles.Add(filePath);
            PlayFromStart(player);
        };
        player.MediaOpened += handler;
    }

    private static void PlayFromStart(MediaPlayer player)
    {
        try
        {
            player.Stop();
            player.Position = TimeSpan.Zero;
            player.Play();
        }
        catch { }
    }

    // ── 事件响应 ──

    private void InformationOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EarthQuakeTrackingInformation.CountDown))
            PlayAlertSequence();
    }

    private void PlayAlertSequence()
    {
        var countdown = _information.CountDown;
        var intensity = _information.Intensity;

        if (countdown <= 0)
        {
            _information.PropertyChanged -= InformationOnPropertyChanged;
            PlayArrivingLoop();
            return;
        }

        if (countdown == _lastPlayedCountdown) return;
        _lastPlayedCountdown = countdown;

        if (countdown > 10)
        {
            // ── 10 以上：偶数秒播报倒计时（几十几），奇数秒播报级别提示音 ──
            if (countdown % 2 == 0)
            {
                // 偶数秒 → "几十几"格式，如 28 → 2.mp3 + 10.mp3 + 8.mp3
                PlayCompositeCountdown(countdown);
            }
            else
            {
                // 奇数秒 → 播报 level.mp3（表示震级）
                if (intensity > 2)
                {
                    PlayOrEnqueue(LevelFile);
                    if (intensity > 4)
                    {
                        var _ = Dispatcher.InvokeAsync(async () =>
                        {
                            await Task.Delay(400);
                            PlayOrEnqueue(LevelFile);
                        });
                    }
                }
            }
        }
        else
        {
            // ── 10 及以下：保持原有行为 ──
            var fullFilePath = Path.Combine(NumbersDir, $"{countdown}.mp3");
            if (_preloadedPlayers.ContainsKey(fullFilePath))
            {
                PlayOrEnqueue(fullFilePath);
            }
            else
            {
                foreach (var digitChar in countdown.ToString())
                {
                    var digitFile = Path.Combine(NumbersDir, $"{digitChar}.mp3");
                    if (_preloadedPlayers.ContainsKey(digitFile))
                        PlayOrEnqueue(digitFile);
                }
            }

            if (intensity > 4)
            {
                PlayOrEnqueue(LevelFile);
                var _ = Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(400);
                    PlayOrEnqueue(LevelFile);
                });
            }
            else if (intensity > 2)
            {
                PlayOrEnqueue(LevelFile);
            }
        }
    }

    /// <summary>
    /// 以"几十几"方式播报倒计时，如 28 → 2 + 十 + 8 → "二十八"
    /// 所有 PlayOrEnqueue 调用均在 UI 线程上执行（通过 Dispatcher），
    /// 因为 MediaPlayer 是 WPF 组件，必须运行在 STA 线程。
    /// </summary>
    private async void PlayCompositeCountdown(int countdown)
    {
        var tens = countdown / 10;   // 十位
        var ones = countdown % 10;   // 个位

        // 十位
        await Dispatcher.InvokeAsync(() =>
            PlayOrEnqueue(Path.Combine(NumbersDir, $"{tens}.mp3")));
        await Task.Delay(250); // 等待 250ms 再播报 "十"
        // "十"
        await Dispatcher.InvokeAsync(() =>
            PlayOrEnqueue(Path.Combine(NumbersDir, "10.mp3")));
        if (ones > 0)
        {
            await Task.Delay(250); // 等待 250ms 再播报个位
            // 个位
            await Dispatcher.InvokeAsync(() =>
                PlayOrEnqueue(Path.Combine(NumbersDir, $"{ones}.mp3")));
        }
    }

    // ── arriving.mp3 循环 ──

    private void PlayArrivingLoop()
    {
        if (!_preloadedPlayers.ContainsKey(ArrivingFile) && !File.Exists(ArrivingFile)) return;

        // 地震到达后警报持续时间（用户可配置），默认 30 秒
        var durationMs = Math.Max(1000, (_trackerSetting.Setting?.AlertDurationSecond ?? 30) * 1000);
        _autoCloseTimer = new System.Threading.Timer(_ =>
        {
            Dispatcher.Invoke(Close);
        }, null, durationMs, System.Threading.Timeout.Infinite);

        Application.Current.Dispatcher.Invoke(() =>
        {
            var player = new MediaPlayer();
            EventHandler? handler = null;
            handler = (_, _) =>
            {
                player.Position = TimeSpan.Zero;
                player.Play();
            };
            player.MediaEnded += handler;
            _arrivingLoopPlayer = player;
            lock (_audioLock) _activePlayers.Add(player);
            player.Open(new Uri(ArrivingFile, UriKind.Absolute));
            player.Play();
        });
    }

    // ── 清理 ──

    private void StopAllAudio()
    {
        // 停止窗口专属的 arriving 循环播放器（非预加载，需要关闭释放）
        if (_arrivingLoopPlayer != null)
        {
            lock (_audioLock) _activePlayers.Remove(_arrivingLoopPlayer);
            _arrivingLoopPlayer.Stop();
            _arrivingLoopPlayer.Close();
            _arrivingLoopPlayer = null;
        }

        // 仅关闭非预加载的播放器（预加载的播放器需要跨窗口复用）
        lock (_audioLock)
        {
            foreach (var p in _activePlayers.ToArray())
            {
                if (_preloadedPlayerSet.Contains(p))
                    continue;
                try { p.Stop(); p.Close(); } catch { }
            }
            _activePlayers.Clear();
            // 将预加载播放器重新加入活跃列表，供下一次窗口使用
            foreach (var p in _preloadedPlayerSet)
                _activePlayers.Add(p);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void EarlyWarningWindow_OnClosed(object? sender, EventArgs e)
    {
        _autoCloseTimer?.Dispose();
        StopAllAudio();
        _information.PropertyChanged -= InformationOnPropertyChanged;
    }
}


