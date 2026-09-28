using System.Globalization;

namespace Singulink.Numerics.Tests;

/// <summary>
/// Shared test data for the formatting tests. All values are invariant culture strings that can be parsed with <see cref="NumberStyles.Float"/>.
/// </summary>
internal static class FormattingTestData
{
    /// <summary>
    /// Values that can be exactly represented as a <see cref="decimal"/> (without trailing zeros in the scale so that "G" formatting matches).
    /// </summary>
    public static readonly string[] DecimalValues = [
        "0", "1", "-1", "5", "-5", "9", "-9", "10", "-10", "100", "-100",
        "0.5", "-0.5", "0.05", "-0.05", "0.125", "-0.125", "0.135", "-0.135", "0.00123", "-0.00123", "0.001", "-0.001", "0.0005", "-0.0005",
        "1.5", "-1.5", "9.5", "-9.5", "99.95", "-99.95", "100.5", "999.995", "-999.995", "0.999", "-0.999", "1099.95", "1234.5678", "-1234.5678",
        "12000", "-12000", "12000.123", "-12000.123", "1234567", "-1234567", "12345670", "-12345670", "123456700", "1000000", "1E+5", "1E+6", "1E+7", "123E+10",
        "0.000001", "0.0000001", "0.0000000123", "-0.00000001", "0.0000000000000000000000000001", "-0.0000000000000000000000000001",
        "2147483647", "2147483648", "-2147483648", "-2147483649", "4294967296", "-4294967296", "4294967295.5", "18446744073709551615", "18446744073709551616", "-18446744073709551616",
        "1.5E+20", "123456789012345678901234567.5", "-123456789012345678901234567.5", "79228162514264337593543950335", "-79228162514264337593543950335",
        "1234567890.0987654321", "-0.1234567890123456789012345678",
    ];

    /// <summary>
    /// Values that cannot be represented as a <see cref="decimal"/>.
    /// </summary>
    public static readonly string[] LargeValues = [
        "1E+30", "-1E+30", "1E-30", "-1E-30", "1.5E+31", "12345678901234567890123456789012345", "-12345678901234567890123456789012345.6789",
        "123456789012345678901234567890123456789012345678901234567890.123456789012345678901234567890", "1E+100", "1E-100", "-9.99999999999999999999999999999999999999E+99",
        "340282366920938463463374607431768211456", "-340282366920938463463374607431768211455",
    ];

    public static readonly string[] AllValues = [.. DecimalValues, .. LargeValues];

    /// <summary>
    /// All supported standard format strings (with a selection of precision specifiers).
    /// </summary>
    public static readonly string[] StandardFormats = [
        string.Empty, "G", "g", "G0", "G1", "G2", "G3", "G5", "G10", "G20", "G100",
        "F", "f", "F0", "F1", "F2", "F5", "N", "n", "N0", "N1", "N3",
        "E", "e", "E0", "E1", "E3", "E10", "C", "c", "C0", "C1", "C3", "P", "p", "P0", "P1", "P3", "R", "r",
    ];

    /// <summary>
    /// Formats that produce identical output to <see cref="decimal"/> formatting.
    /// </summary>
    public static readonly string[] DecimalParityFormats = [
        string.Empty, "G", "g", "F", "f", "F0", "F1", "F2", "F5", "N", "n", "N0", "N1", "N3",
        "E", "e", "E0", "E1", "E3", "E10", "C", "c", "C0", "C1", "C3", "P", "p", "P0", "P1", "P3",
    ];

    /// <summary>
    /// Typical European style formatting with a unicode minus sign.
    /// </summary>
    public static readonly NumberFormatInfo Euro = new() {
        NegativeSign = "−",
        PositiveSign = "+",
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberNegativePattern = 1,
        CurrencySymbol = "€",
        CurrencyDecimalSeparator = ",",
        CurrencyGroupSeparator = ".",
        CurrencyPositivePattern = 3,
        CurrencyNegativePattern = 8,
        PercentSymbol = "%",
        PercentDecimalSeparator = ",",
        PercentGroupSeparator = ".",
        PercentPositivePattern = 0,
        PercentNegativePattern = 0,
    };

    /// <summary>
    /// Multi-character signs/separators/symbols, parenthesis negative patterns, uneven group sizes and non-default decimal digits.
    /// </summary>
    public static readonly NumberFormatInfo Weird = new() {
        NegativeSign = "neg",
        PositiveSign = "pos",
        NumberDecimalSeparator = "<d>",
        NumberGroupSeparator = "<g>",
        NumberGroupSizes = [3, 2],
        NumberNegativePattern = 0,
        NumberDecimalDigits = 4,
        CurrencySymbol = "USD",
        CurrencyDecimalSeparator = "<cd>",
        CurrencyGroupSeparator = "<cg>",
        CurrencyGroupSizes = [2, 3],
        CurrencyPositivePattern = 2,
        CurrencyNegativePattern = 14,
        CurrencyDecimalDigits = 3,
        PercentSymbol = "pct",
        PercentDecimalSeparator = "<pd>",
        PercentGroupSeparator = "<pg>",
        PercentGroupSizes = [1],
        PercentPositivePattern = 2,
        PercentNegativePattern = 4,
        PercentDecimalDigits = 1,
    };

    /// <summary>
    /// Trailing sign/symbol patterns, terminated group sizes and zero default decimal digits.
    /// </summary>
    public static readonly NumberFormatInfo Trailing = new() {
        NegativeSign = "-",
        NumberNegativePattern = 4,
        NumberGroupSizes = [2, 0],
        NumberDecimalDigits = 0,
        CurrencySymbol = "$",
        CurrencyPositivePattern = 1,
        CurrencyNegativePattern = 15,
        CurrencyGroupSizes = [4],
        CurrencyDecimalDigits = 0,
        PercentSymbol = "%",
        PercentPositivePattern = 3,
        PercentNegativePattern = 11,
        PercentGroupSizes = [3, 0],
        PercentDecimalDigits = 0,
    };

    /// <summary>
    /// Number format infos that exercise the various patterns, symbols and separators. Custom infos are used instead of named cultures so that the tests
    /// are deterministic across globalization implementations (ICU/NLS/invariant mode). Must be declared after the infos it references so that they are
    /// initialized first.
    /// </summary>
    public static readonly (string Name, NumberFormatInfo Info)[] FormatInfos = [
        ("Invariant", NumberFormatInfo.InvariantInfo),
        ("Euro", Euro),
        ("Weird", Weird),
        ("Trailing", Trailing),
    ];

    public static BigDecimal ParseValue(string value) => BigDecimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}