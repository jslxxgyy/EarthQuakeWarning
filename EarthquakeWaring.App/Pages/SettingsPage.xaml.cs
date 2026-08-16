using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.Models.ViewModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using GuerrillaNtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EarthquakeWaring.App.Pages;

public partial class SettingsPage : Page
{
    private bool dontFire = false;
    private readonly IServiceProvider _services;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly INTPHandler _ntpHandler;

    public SettingsPage(SettingsPageViewModel vm, IServiceProvider services, IHostApplicationLifetime lifetime, INTPHandler ntpHandler)
    {
        _services = services;
        _lifetime = lifetime;
        _ntpHandler = ntpHandler;
        InitializeComponent();
        DataContext = vm;
        dontFire = true;
        StartupSwitch.IsChecked =
            Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\\")?.GetValue(nameof(EarthquakeWaring)) != null;
        dontFire = false;
    }

    /// <summary>
    /// 页面加载完成后，找到 UiPageScrollable 提供的 ScrollViewer 并启用触摸平移
    /// </summary>
    private void SettingsPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(this);
        if (scrollViewer != null)
        {
            scrollViewer.PanningMode = PanningMode.Both;
            scrollViewer.PanningDeceleration = 0.01;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void OpenPositionSelector(object sender, RoutedEventArgs e)
    {
        Process.Start("explorer.exe", "https://lbs.qq.com/getPoint/");
    }

    private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
    {
        if (dontFire) return;
        Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run")
            .SetValue(nameof(EarthquakeWaring), Environment.ProcessPath + " /nogui");
    }

    private void StartupSwitch_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (dontFire) return;
        Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run")
            .DeleteValue(nameof(EarthquakeWaring));
    }

    private void ShowNotifyIcon_OnChecked(object sender, RoutedEventArgs e)
    {
        if (dontFire) return;
        _services.GetService<ITrayIconHolder>()?.ShowIcon();
    }

    private void ShowNotifyIcon_OnUnchecked(object sender, RoutedEventArgs e)
    {
        if (dontFire) return;
        _services.GetService<ITrayIconHolder>()?.HideIcon();
    }

    private void DeveloperClicked(object sender, RoutedEventArgs e)
    {
        Process.Start("explorer.exe", "https://github.com/jslxxgyy");
    }

    private void OpenSourceClick(object sender, RoutedEventArgs e)
    {
        Process.Start("explorer.exe", "https://github.com/jslxxgyy/EarthQuakeWarning");
    }

    private void ThanksClick(object sender, RoutedEventArgs e)
    {
        Process.Start("explorer.exe", "http://www.365icl.com/");
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        _lifetime.StopApplication();
        Application.Current.Shutdown();
    }
    public async void TestNTPServer(object sender, RoutedEventArgs e)
    {
        var setting = _services.GetService<ISetting<TimeSetting>>();
        var server = setting?.Setting?.NTPServer;
        var client = new NtpClient(server, TimeSpan.FromMilliseconds(500));
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var result = await client.QueryAsync(cts.Token);
            if (result != null && result.Synchronized)
            {
                MessageBox.Show($"NTP服务器状态正常, 当前时间 {result.Now.DateTime}");
            }
            else
            {
                MessageBox.Show("NTP服务器状态异常");
            }
        }
        catch
        {
            MessageBox.Show("NTP服务器状态异常");
        }
    }

    private void GetLocationInformation(object sender, RoutedEventArgs e)
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        _ = _services.GetService<ILocationHandler>()?.GetCurrentInfoAsync(cts.Token);
    }
}