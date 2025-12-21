using System;
using System.Globalization;
using System.Windows.Data;

namespace Veriflow.Desktop.Converters
{
    public class PercentToPositionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double percent && values[1] is double width)
            {
                // Return position: percent * width
                return percent * width;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is double position && targetTypes.Length >= 2)
            {
                // We can't know the width from here easily unless passed back or stored?
                // Actually, MultiBinding passes existing values into ConvertBack in 'values' in some frameworks? No.
                // Standard IMultiValueConverter.ConvertBack is hard because we don't have the 2nd bound value (Width).
                // If we can't calculate it, returning DoNothing is safer than throwing.
                // But wait, usually ConvertBack is for TwoWay binding on the SLIDER.
                // If the slider sets Position, we want to update Percent.
                // But we don't know Width.
                // If this is used for a timeline playhead overlay, it's likely OneWay.
                // If it IS TwoWay, better to handle it customized. 
                // Given the context (likely OneWay overlay), safe return:
                return new object[] { Binding.DoNothing, Binding.DoNothing };
            }
            return new object[] { Binding.DoNothing, Binding.DoNothing };
        }
    }
}
