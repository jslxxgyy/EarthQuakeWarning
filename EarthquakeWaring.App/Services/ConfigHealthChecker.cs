using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.DependencyInjection;

namespace EarthquakeWaring.App.Services;

public enum ConfigHealthState
{
    Healthy,
    FirstRun,
    Corrupted
}

/// <summary>
/// 检查配置的健康状态：位置是否已设置、各配置是否损坏、ApiType 是否越界。
/// 完全基于 FileJsonSetting 的内存状态判断，不直接读写配置文件，避免与设置写入产生竞态。
/// </summary>
public class ConfigHealthChecker : INotifyPropertyChanged
{
    private readonly IServiceProvider _services;
    private volatile ConfigHealthState _state = ConfigHealthState.Healthy;

    public ConfigHealthState State => _state;

    public bool IsHealthy => _state == ConfigHealthState.Healthy;

    public bool BannerVisible => _state != ConfigHealthState.Healthy;

    public string BannerText => _state switch
    {
        ConfigHealthState.FirstRun => "欢迎使用，请先进行设置以开启预警服务",
        ConfigHealthState.Corrupted => "配置文件损坏，请重新设置以开启预警服务",
        _ => string.Empty
    };

    public ConfigHealthChecker(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// 重新评估配置健康状态（仅读取内存状态，开销极小，可在心跳中调用）。
    /// </summary>
    public void Recheck()
    {
        try
        {
            var corrupted = 0;

            void CheckSetting<T>() where T : INotificationOption, new()
            {
                if (_services.GetService<ISetting<T>>() is FileJsonSetting<T> fileSetting && fileSetting.LoadFailed)
                    corrupted++;
            }

            CheckSetting<AlertLimit>();
            CheckSetting<UpdaterSetting>();
            CheckSetting<TrackerSetting>();
            CheckSetting<TimeSetting>();
            CheckSetting<LocationSetting>();

            // 位置必须已设置（经纬度有值），其余配置只要有默认值即可
            var position = _services.GetService<ISetting<CurrentPosition>>()?.Setting;
            var positionConfigured = position is { Latitude: { }, Longitude: { } };

            // UpdaterSetting 的 ApiType 越界视为损坏（EarthQuakeApiWrapper 另有 clamp 兜底）
            var updater = _services.GetService<ISetting<UpdaterSetting>>()?.Setting;
            if (updater != null)
            {
                var apiCount = _services.GetServices<IEarthQuakeApi>().Count();
                if (updater.ApiType < 0 || updater.ApiType >= Math.Max(1, apiCount))
                    corrupted++;
            }

            var newState = corrupted > 0
                ? ConfigHealthState.Corrupted
                : positionConfigured ? ConfigHealthState.Healthy : ConfigHealthState.FirstRun;

            if (_state != newState)
            {
                _state = newState;
                NotifyChanged();
            }
        }
        catch (Exception)
        {
            // 检查本身失败时保持原状态，不影响预警
        }
    }

    private void NotifyChanged()
    {
        // 确保在 UI 线程触发，以便绑定刷新
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() =>
            {
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsHealthy));
                OnPropertyChanged(nameof(BannerVisible));
                OnPropertyChanged(nameof(BannerText));
            });
        }
        else
        {
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsHealthy));
            OnPropertyChanged(nameof(BannerVisible));
            OnPropertyChanged(nameof(BannerText));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
