using EarthquakeWaring.App.Infrastructure.Models.BaseModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EarthquakeWaring.App.Services;

public class HeartBeatBackgroundService : BackgroundService
{
    private readonly INotificationPublisher _publisher;
    private readonly ITimeHandler _timeHandler;

    public HeartBeatBackgroundService(INotificationPublisher publisher, ITimeHandler timeHandler)
    {
        _publisher = publisher;
        _timeHandler = timeHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _publisher.Publish(new HeartBeatNotification(DateTime.Now + _timeHandler!.Offset), stoppingToken).ConfigureAwait(false);
            // 每 2.5 秒检查一次，降低空闲时的 CPU 占用
            await Task.Delay(2500, stoppingToken).ConfigureAwait(false);
        }
    }
}