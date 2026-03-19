using BamboozClipStudio.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BamboozClipStudio.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}

[ValueConversion(typeof(bool), typeof(string))]
public class DoneToCloseTextConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? "Close" : "Cancel";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

[ValueConversion(typeof(BitrateMode), typeof(int))]
public class EnumToIndexConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is BitrateMode mode ? (int)mode : 0;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is int idx ? (BitrateMode)idx : BitrateMode.Crf;
}
