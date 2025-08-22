using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Timer.Converters
{
    /// <summary>
    /// BoolToStringConverter 布尔值与字符串之间的转换器
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCountdown)
            {
                return isCountdown ? "[倒计时]" : "[计时器]";
            }

            return "[未知]";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}