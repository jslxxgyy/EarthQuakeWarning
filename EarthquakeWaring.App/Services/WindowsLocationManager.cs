using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace EarthquakeWaring.App.Services;

public class WindowsLocationManager : ILocationHandler
{
    private readonly ILogger<WindowsLocationManager> _logger;
    private readonly ISetting<CurrentPosition> _positionSetting;
    private readonly ISetting<LocationSetting> _locationSetting;
    private readonly ITimeHandler _timeHandler;

    public WindowsLocationManager(
        ISetting<CurrentPosition> positionSetting,
        ISetting<LocationSetting> locationSetting,
        ITimeHandler timeHandler,
        ILogger<WindowsLocationManager> logger)
    {
        _positionSetting = positionSetting;
        _locationSetting = locationSetting;
        _timeHandler = timeHandler;
        _logger = logger;
        timeHandler.Timer.Elapsed += OnTimerElapsed;

        // 监听 FileJsonSetting 的 PropertyChanged，当 Setting 对象被替换时自动绑定新对象
        if (_locationSetting is INotifyPropertyChanged settingProvider)
        {
            settingProvider.PropertyChanged += OnSettingProviderPropertyChanged;
        }

        // 绑定当前 Setting 对象
        SubscribeToLocationSetting();
    }

    private void OnSettingProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ISetting<LocationSetting>.Setting))
        {
            SubscribeToLocationSetting();
            // 新 Setting 载入后如果开关已打开则立即定位
            if (_locationSetting.Setting?.UseWindowsLocation == true)
                _ = GetCurrentInfoAsync();
        }
    }

    private INotifyPropertyChanged? _currentLocationSettingSubscription;

    private void SubscribeToLocationSetting()
    {
        // 解绑旧对象
        if (_currentLocationSettingSubscription != null)
        {
            _currentLocationSettingSubscription.PropertyChanged -= OnLocationSettingPropertyChanged;
            _currentLocationSettingSubscription = null;
        }

        // 绑定新对象
        if (_locationSetting.Setting is INotifyPropertyChanged newSetting)
        {
            _currentLocationSettingSubscription = newSetting;
            newSetting.PropertyChanged += OnLocationSettingPropertyChanged;
        }
    }

    private void OnLocationSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocationSetting.UseWindowsLocation) &&
            _locationSetting.Setting?.UseWindowsLocation == true)
        {
            _ = GetCurrentInfoAsync();
        }
    }

    public async Task<bool> GetCurrentInfoAsync(CancellationToken token = default)
    {
        if (_locationSetting.Setting is not { UseWindowsLocation: true })
            return false;

        try
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                _logger.LogWarning("Windows 位置权限被拒绝，请在系统设置中允许应用访问位置。");
                return false;
            }

            var geolocator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.High
            };

            var position = await geolocator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(1),
                timeout: TimeSpan.FromSeconds(10));

            var coord = position.Coordinate.Point.Position;
            _positionSetting.Setting!.Longitude = Math.Round(coord.Longitude, 6);
            _positionSetting.Setting!.Latitude = Math.Round(coord.Latitude, 6);
            _timeHandler.LastUpdated = DateTime.Now;

            _logger.LogInformation(
                "Windows 定位成功: 经度={Longitude}, 纬度={Latitude}",
                _positionSetting.Setting.Longitude,
                _positionSetting.Setting.Latitude);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "Windows 位置权限不足（未在系统设置中启用定位），请前往 设置 > 隐私和安全性 > 位置 开启定位功能。");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Windows 位置信息失败");
            return false;
        }
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _ = GetCurrentInfoAsync();
    }
}
