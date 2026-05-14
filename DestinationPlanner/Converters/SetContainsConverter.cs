using System.Globalization;
using System.Windows.Data;

namespace DestinationPlanner.Converters;

// Returns true when values[0] (Guid) is contained in values[1] (IReadOnlySet<Guid>).
// Used by LogbookView to highlight newly-imported rows.
public sealed class SetContainsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [Guid id, IReadOnlySet<Guid> set])
            return set.Contains(id);
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
