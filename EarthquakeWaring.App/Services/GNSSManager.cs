// GNSS 串口定位功能已移除，改用 WindowsLocationManager 调用 Windows 原生定位 API。
// 此文件已弃用，保留仅用于避免编译错误，实际未引用。

namespace EarthquakeWaring.App.Services;

[System.Obsolete("已由 WindowsLocationManager 替代")]
public class GNSSManager
{
}
