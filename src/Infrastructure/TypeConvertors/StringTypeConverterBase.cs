using System.ComponentModel;
using System.Globalization;

namespace AzureCostCli.Infrastructure.TypeConvertors;

public abstract class StringTypeConverterBase<T> : TypeConverter
{
    protected abstract T Parse(string s, IFormatProvider? provider);

    protected abstract string ToIsoString(T source, IFormatProvider? provider);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        if (sourceType == typeof(string))
        {
            return true;
        }
        return base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            return Parse(str, GetFormat(culture));
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        if (destinationType == typeof(string))
        {
            return true;
        }
        return base.CanConvertTo(context, destinationType);
    }
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is T typedValue)
        {
            return ToIsoString(typedValue, GetFormat(culture));
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }

    private static IFormatProvider? GetFormat(CultureInfo? culture)
    {
        DateTimeFormatInfo? formatInfo = null;
        if (culture != null)
        {
            formatInfo = (DateTimeFormatInfo?)culture.GetFormat(typeof(DateTimeFormatInfo));
        }

        return (IFormatProvider?)formatInfo ?? culture;
    }
}