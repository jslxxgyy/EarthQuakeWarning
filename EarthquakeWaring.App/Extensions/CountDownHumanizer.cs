using System;
using System.Globalization;
using System.Windows.Data;

namespace EarthquakeWaring.App.Extensions;

public class CountDownHumanizer : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            int countDown => countDown switch
            {
                > 0 => countDown.ToString(),
                0 => " 地震到达！",
                _ => $"到达 {Math.Abs(countDown)} 秒"
            },
            double countDownDouble => countDownDouble switch
            {
                > 0 => countDownDouble.ToString("F1"),
                0 => " 地震到达！",
                _ => $"到达 {Math.Abs(countDownDouble):F1} 秒"
            },
            _ => "未知"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}