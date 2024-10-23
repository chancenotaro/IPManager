// File: NullToBoolConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace IPManager
{
    public class NullToBoolConverter : IValueConverter
    {
        // Converts null to false and non-null to true
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        // Not implemented
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
