using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EarthquakeWaring.App.Extensions;

/// <summary>
/// 按烈度返回预警窗口的背景色：
/// 烈度 &lt; 2 → 灰色；2 ≤ 烈度 &lt; 4 → 黄色；烈度 ≥ 4 → 红色。
/// </summary>
public class EarthQuakeIntensityToColorBrush : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double intensity)
        {
            return new SolidColorBrush(intensity switch
            {
                < 2 => Colors.Gray,
                < 4 => Colors.Orange,
                _ => Colors.Red
            });
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
