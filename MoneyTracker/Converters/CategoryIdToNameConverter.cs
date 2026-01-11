using System.Globalization;
using System.Windows.Data;
using MoneyTracker.Models;

namespace MoneyTracker.Converters;

public sealed class CategoryIdToNameConverter : IMultiValueConverter
{

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var categoryId = values.Length > 0 ? values[0] as string : null;
        var categories = values.Length > 1 ? values[1] as IEnumerable<Category> : null;

        if (string.IsNullOrWhiteSpace(categoryId) || categories is null) return "";
        return categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}