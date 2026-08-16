using EarthquakeWaring.App.Icons;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using EarthquakeWaring.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace EarthquakeWaring.App.Services;

public class TrayIconHolder : ITrayIconHolder, IDisposable
{
    private readonly IServiceProvider _sp;
    private readonly NotifyIcon _notifyIcon;

    public TrayIconHolder(IServiceProvider sp, IHostApplicationLifetime lifetime)
    {
        _sp = sp;
        _notifyIcon = new NotifyIcon();
        _notifyIcon.Text = "地震预警 正在运行";

        // 优先使用与 exe 相同的 original.ico（ApplicationIcon 已嵌入 exe）
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "original.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "original.ico");
            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
            if (!File.Exists(iconPath))
            {
                var imageOriginal = Convert.FromBase64String(IconResource.Original);
                File.WriteAllBytes(iconPath, imageOriginal);
            }
        }
        _notifyIcon.Icon = new System.Drawing.Icon(iconPath);

        // 右键菜单：打开主界面 / 退出
        var menu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("打开主界面");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) =>
        {
            lifetime.StopApplication();
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = menu;

        // 仅左键单击打开主界面；右键弹出菜单
        _notifyIcon.MouseClick += NotifyIconOnMouseClick;
    }

    private void ShowMainWindow()
    {
        if (App.MainWindowOpened) return;
        _sp.GetService<MainWindow>()?.Show();
    }

    private void NotifyIconOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ShowMainWindow();
    }

    public void ShowIcon()
    {
        _notifyIcon.Visible = true;
    }

    public void HideIcon()
    {
        _notifyIcon.Visible = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _notifyIcon.Dispose();
    }
}