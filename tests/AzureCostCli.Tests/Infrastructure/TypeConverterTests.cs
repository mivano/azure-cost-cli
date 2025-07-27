using AzureCostCli.Infrastructure.TypeConvertors;
using Shouldly;
using System.Globalization;
using Xunit;

namespace AzureCostCli.Tests.Infrastructure;

public class DateOnlyTypeConverterTests
{
    private readonly DateOnlyTypeConverter _converter;

    public DateOnlyTypeConverterTests()
    {
        _converter = new DateOnlyTypeConverter();
    }

    [Fact]
    public void CanConvertFrom_String_ReturnsTrue()
    {
        // Act
        var result = _converter.CanConvertFrom(typeof(string));

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanConvertFrom_Int_ReturnsFalse()
    {
        // Act
        var result = _converter.CanConvertFrom(typeof(int));

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ConvertFrom_ValidDateString_ReturnsDateOnly()
    {
        // Arrange
        var dateString = "2023-01-15";

        // Act
        var result = _converter.ConvertFrom(dateString);

        // Assert
        result.ShouldBe(new DateOnly(2023, 1, 15));
    }

    [Fact]
    public void ConvertFrom_ValidDateStringWithCulture_ReturnsDateOnly()
    {
        // Arrange
        var dateString = "15/01/2023";
        var culture = new CultureInfo("en-GB");

        // Act
        var result = _converter.ConvertFrom(null, culture, dateString);

        // Assert
        result.ShouldBe(new DateOnly(2023, 1, 15));
    }

    [Fact]
    public void ConvertFrom_InvalidType_ThrowsException()
    {
        // Arrange
        var value = 123;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _converter.ConvertFrom(value));
    }

    [Fact]
    public void CanConvertTo_String_ReturnsTrue()
    {
        // Act
        var result = _converter.CanConvertTo(typeof(string));

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanConvertTo_Int_ReturnsFalse()
    {
        // Act
        var result = _converter.CanConvertTo(typeof(int));

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ConvertTo_DateOnlyToString_ReturnsIsoString()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);

        // Act
        var result = _converter.ConvertTo(date, typeof(string));

        // Assert
        result.ShouldBe("2023-01-15");
    }

    [Fact]
    public void ConvertTo_DateOnlyToStringWithCulture_ReturnsIsoString()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);
        var culture = new CultureInfo("en-GB");

        // Act
        var result = _converter.ConvertTo(null, culture, date, typeof(string));

        // Assert
        result.ShouldBe("2023-01-15");
    }

    [Fact]
    public void ConvertTo_InvalidType_ThrowsException()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _converter.ConvertTo(date, typeof(int)));
    }

    [Fact]
    public void ConvertTo_NullValue_CallsBaseConvertTo()
    {
        // Act
        var result = _converter.ConvertTo(null, typeof(string));

        // Assert
        // The base implementation might return null or empty string
        result.ShouldBeOfType<string>();
    }
}