using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EarthquakeWaring.App.Infrastructure.Models.SettingModels;

public class AlertLimit : INotificationOption
{
    private double _dayIntensity = 3.0;
    private double _nightIntensity = 2.0;

    /// <summary>日间（7时-22时）报警烈度阈值：烈度大于等于此值才弹窗</summary>
    public double DayIntensity
    {
        get => _dayIntensity;
        set => SetField(ref _dayIntensity, value);
    }

    /// <summary>夜间（23时-次日6时）报警烈度阈值：烈度大于等于此值才弹窗</summary>
    public double NightIntensity
    {
        get => _nightIntensity;
        set => SetField(ref _nightIntensity, value);
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