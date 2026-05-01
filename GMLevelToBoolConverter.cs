using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AzerothCoreLauncher
{
    public class GMLevelToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int gmLevel)
            {
                return gmLevel > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isGM && isGM)
            {
                return 1; // GM level 1 when toggled on
            }
            return 0; // GM level 0 when toggled off
        }
    }
}
