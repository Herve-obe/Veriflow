using System;
using System.Globalization;
using System.Windows.Data;

namespace Veriflow.Desktop.Converters
{
    /// <summary>
    /// Converts an enum value to a boolean for ToggleButton binding.
    /// Parameter should be the enum value name as a string.
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string parameterString = parameter.ToString() ?? string.Empty;
            
            if (Enum.IsDefined(value.GetType(), value) == false)
                return false;

            object parameterValue = Enum.Parse(value.GetType(), parameterString);
            
            return parameterValue.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null)
                return Binding.DoNothing;

            string parameterString = parameter.ToString() ?? string.Empty;
            
            return Enum.Parse(targetType, parameterString);
        }
    }
}
