using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EarthquakeWaring.App.Infrastructure.Models.SettingModels;

public class TrackerSetting : INotificationOption
{
    private int _trackerTimeSpanMillisecond = 5;
    private bool _maximumVolume = true;
    private int _alertDurationSecond = 30;

    public bool MaximumVolume
    {
        get => _maximumVolume;
        set => SetField(ref _maximumVolume, value);
    }

    public int TrackerTimeSpanMillisecond
    {
        get => _trackerTimeSpanMillisecond;
        set => SetField(ref _trackerTimeSpanMillisecond, value);
    }

    /// <summary>
    /// 地震到达后警报持续的时间（秒）。默认 30 秒。
    /// </summary>
    public int AlertDurationSecond
    {
        get => _alertDurationSecond;
        set => SetField(ref _alertDurationSecond, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}