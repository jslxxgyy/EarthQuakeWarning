using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace EarthquakeWaring.App.Services;

public class FileJsonSetting<TSetting> : ISetting<TSetting>, INotifyPropertyChanged where TSetting : INotificationOption, new()
{
    private readonly IJsonConvertService _jsonConvertService;
    private readonly ILogger<FileJsonSetting<TSetting>> _logger;

    private TSetting? _inMemorySetting;
    private readonly string _settingName;
    private DateTime _lastWriteTime = DateTime.MinValue;

    /// <summary>文件存在且内容不为空对象，表示用户已真正配置过</summary>
    public bool IsConfigured { get; private set; }

    /// <summary>最近一次加载/反序列化失败</summary>
    public bool LoadFailed { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TSetting? Setting => _inMemorySetting;

    public FileJsonSetting(IJsonConvertService jsonConvertService, ILogger<FileJsonSetting<TSetting>> logger)
    {
        _jsonConvertService = jsonConvertService;
        _logger = logger;
        _settingName = typeof(TSetting).Name;

        // Pre Load Settings
        LoadSettingsFromFile();

        // Add FileSystem Monitor
        var fileSystemWatcher = new FileSystemWatcher();
        fileSystemWatcher.Path = $"{Directory.GetCurrentDirectory()}/settings";
        fileSystemWatcher.Changed += FileMonitorOnChanged;
        fileSystemWatcher.Created += FileMonitorOnChanged;
        fileSystemWatcher.Deleted += FileMonitorOnChanged;
        fileSystemWatcher.Renamed += FileMonitorOnChanged;
        fileSystemWatcher.Filters.Add("*.json");
        fileSystemWatcher.EnableRaisingEvents = true;

        // Add Option Monitor
        if (_inMemorySetting != null) _inMemorySetting.PropertyChanged += InMemorySettingOnPropertyChanged;
    }

    private void InMemorySettingOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_inMemorySetting != null)
        {
            IsConfigured = true;
            _lastWriteTime = DateTime.Now;
            File.WriteAllText($"settings/{_settingName}.json", _jsonConvertService.ConvertBack(_inMemorySetting));
            return;
        }
        IsConfigured = false;
        File.Delete($"settings/{_settingName}.json");
    }

    private void FileMonitorOnChanged(object sender, FileSystemEventArgs e)
    {
        // 忽略由本程序自身写入所触发的重载，避免拖动滑块时反复重建绑定对象导致数值错乱
        if ((DateTime.Now - _lastWriteTime).TotalMilliseconds < 800)
            return;
        if (e.FullPath.EndsWith($"{_settingName}.json"))
        {
            LoadSettingsFromFile();
        }
    }

    private void LoadSettingsFromFile()
    {
        _logger.LogTrace("Loading Configuration of {SettingName}", nameof(TSetting));
        try
        {
            if (!Directory.Exists("settings")) Directory.CreateDirectory("settings");

            // 解绑旧对象的事件，防止内存泄漏
            if (_inMemorySetting != null)
                _inMemorySetting.PropertyChanged -= InMemorySettingOnPropertyChanged;

            if (File.Exists($"settings/{_settingName}.json"))
            {
                var raw = File.ReadAllText($"settings/{_settingName}.json");
                IsConfigured = !string.IsNullOrWhiteSpace(raw) && raw.Trim() != "{}";
                try
                {
                    _inMemorySetting = _jsonConvertService.ConvertTo<TSetting>(raw);
                    LoadFailed = _inMemorySetting == null;
                }
                catch
                {
                    LoadFailed = true;
                    _inMemorySetting = new TSetting();
                }
            }
            else
            {
                IsConfigured = false;
                LoadFailed = false;
                File.WriteAllText($"settings/{_settingName}.json", "{}");
                _inMemorySetting = new TSetting();
            }

            // 绑定新对象的事件
            if (_inMemorySetting != null)
                _inMemorySetting.PropertyChanged += InMemorySettingOnPropertyChanged;
        }
        catch (Exception e)
        {
            LoadFailed = true;
            _inMemorySetting = new TSetting();
        }
        OnPropertyChanged(nameof(Setting));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}