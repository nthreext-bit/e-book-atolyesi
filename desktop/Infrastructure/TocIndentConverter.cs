using System.Globalization;
using System.Windows.Data;

namespace KitaplikEditoru.Infrastructure;

public sealed class TocIndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var level = value is int indent ? Math.Clamp(indent, 1, 6) : 1;
        return new string(' ', (level - 1) * 3);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
