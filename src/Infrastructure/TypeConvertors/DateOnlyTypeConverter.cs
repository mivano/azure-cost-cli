namespace AzureCostCli.Infrastructure.TypeConvertors;

public class DateOnlyTypeConverter : StringTypeConverterBase<DateOnly>
{
    protected override DateOnly Parse(string s, IFormatProvider? provider) => DateOnly.Parse(s, provider);

    protected override string ToIsoString(DateOnly source, IFormatProvider? provider) => source.ToString("O", provider);
}