using EarthquakeWaring.App.Infrastructure.Models.BaseModels;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EarthquakeWaring.App.Infrastructure.Models.ApiModels;

namespace EarthquakeWaring.App.Services;

public class EarthQuakeInfoUpdater : INotificationHandler<HeartBeatNotification>, IEarthQuakeInfoUpdater
{
    private readonly IEarthQuakeApiWrapper _earthQuakeApi;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EarthQuakeInfoUpdater> _logger;
    private readonly ISetting<UpdaterSetting> _updaterSetting;
    private long _lastEarthQuakeId = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    private readonly Dictionary<string, TrackerEntry> _trackers = new();
    private DateTime _lastApiCallTime = DateTime.MinValue;

    public EarthQuakeInfoUpdater(IEarthQuakeApiWrapper earthQuakeApi, IServiceProvider serviceProvider,
                                 ILogger<EarthQuakeInfoUpdater> logger, ISetting<UpdaterSetting> updaterSetting)
    {
        _earthQuakeApi = earthQuakeApi;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _updaterSetting = updaterSetting;
    }

    private sealed class TrackerEntry
    {
        public IEarthQuakeTracker Tracker { get; }
        public CancellationTokenSource Cts { get; }
        public Task? RunningTask { get; set; }

        public TrackerEntry(IEarthQuakeTracker tracker, CancellationTokenSource cts)
        {
            Tracker = tracker;
            Cts = cts;
        }
    }

    public async Task Handle(HeartBeatNotification notification, CancellationToken cancellationToken)
    {
        // 每轮先清理已经完成的 tracker，避免 _trackers 无限膨胀
        CleanupCompletedTrackers();

        // 使用时间间隔控制 API 调用频率，与心跳频率解耦
        var interval = TimeSpan.FromSeconds(_updaterSetting.Setting?.UpdateTimeSpanSecond ?? 5);
        if (DateTime.Now - _lastApiCallTime < interval)
            return;
        _lastApiCallTime = DateTime.Now;

        var quakeList = await _earthQuakeApi.GetEarthQuakeList(_lastEarthQuakeId, cancellationToken)
            .ConfigureAwait(false);
        if (quakeList.Count <= 0) return;

        // 用列表中的最大发震时间推进游标，避免 API 返回顺序不稳定导致同一地震被反复返回、反复弹窗
        var maxStartAt = quakeList.Max(t => t.StartAt);

        foreach (var earthQuake in quakeList)
        {
            // 该地震已有跟踪器在运行，跳过，防止同一地震重复跟踪/重复弹窗
            if (_trackers.ContainsKey(earthQuake.Id)) continue;

            _logger.LogDebug("Tracking earthquake at {Position} with DayMagnitude {DayMagnitude}", earthQuake.PlaceName,
                earthQuake.Magnitude);
            var tracker = _serviceProvider.GetService<IEarthQuakeTracker>();
            var trackCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var entry = new TrackerEntry(tracker!, trackCancellationTokenSource);
            _trackers[earthQuake.Id] = entry;
            entry.RunningTask = tracker?.StartTrack(earthQuake, trackCancellationTokenSource);
        }

        _lastEarthQuakeId = DateTimeOffset.FromFileTime(maxStartAt.ToFileTime()).ToUnixTimeMilliseconds() + 1;
    }

    private void CleanupCompletedTrackers()
    {
        var completed = _trackers.Where(kvp =>
        {
            var entry = kvp.Value;
            return entry.RunningTask?.IsCompleted == true || entry.Cts.IsCancellationRequested;
        }).ToList();

        foreach (var kvp in completed)
        {
            kvp.Value.Cts.Dispose();
            _trackers.Remove(kvp.Key);
        }
    }
}