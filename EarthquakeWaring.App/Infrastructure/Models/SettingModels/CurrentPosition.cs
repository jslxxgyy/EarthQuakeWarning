using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EarthquakeWaring.App.Infrastructure.Models.SettingModels;

public class CurrentPosition : INotificationOption
{
    // 默认留空（null）：首次使用时经纬度输入框为空，避免用户误认为已自动定位
    private double? _latitude;
    private double? _longitude;

    public double? Latitude
    {
        get => _latitude;
        set => SetField(ref _latitude, value);
    }

    public double? Longitude
    {
        get => _longitude;
        set => SetField(ref _longitude, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}