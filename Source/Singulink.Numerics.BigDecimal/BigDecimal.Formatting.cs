using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <content>
/// Contains string formatting functionality for <see cref="BigDecimal"/>.
/// </content>
partial struct BigDecimal : IFormattable
{
    /// <summary>
    /// Returns a full-precision decimal form string representation of this value using the current culture.
    /// </summary>
    public override string ToString() => ToString(null);

    /// <summary>
    /// Returns a string representation of this value.
    /// </summary>
    /// <param name="format">The string format to use. The "G" format is used if none is provided.</param>
    /// <param name="formatProvider">The format provider that will be used to obtain number format information. The current culture is used if none is
    /// provided.</param>
    /// <remarks>
    /// <para>String format is composed of a format specifier followed by an optional precision specifier.</para>
    /// <para>Format specifiers:</para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Specifier</term>
    ///     <term>Name</term>
    ///     <description>Description</description>
    ///   </listheader>
    ///   <item>
    ///     <term>"G"</term>
    ///     <term>General</term>
    ///     <description>Default format specifier if none is provided. Precision specifier determines the number of significant digits. If the precision
    ///     specifier is omitted then the value is written out in full precision decimal form. If a precision specifier is provided then the
    ///     more compact of either decimal form or scientific notation is used.</description>
    ///   </item>
    ///   <item>
    ///     <term>"F"</term>
    ///     <term>Fixed-point</term>
    ///     <description>Precision specifier determines the number of decimal digits. Default value is <see cref="NumberFormatInfo.NumberDecimalDigits"/>.</description>
    ///   </item>
    ///   <item>
    ///     <term>"N"</term>
    ///     <term>Number</term>
    ///     <description>Like fixed-point, but also outputs group separators. Precision specifier determines the number of decimal digits. Default value is <see cref="NumberFormatInfo.NumberDecimalDigits"/>.</description>
    ///   </item>
    ///   <item>
    ///     <term>"E"</term>
    ///     <term>Exponential</term>
    ///     <description>Exponential (scientific) notation. Precision specifier determines the number of decimal digits.</description>
    ///   </item>
    ///   <item>
    ///     <term>"C"</term>
    ///     <term>Currency</term>
    ///     <description>Precision specifier determines the number of decimal digits. Default value is <see cref="NumberFormatInfo.CurrencyDecimalDigits"/>.</description>
    ///   </item>
    ///   <item>
    ///     <term>"P"</term>
    ///     <term>Percentage</term>
    ///     <description>Precision specifier determines the number of decimal digits. Default value is <see cref="NumberFormatInfo.PercentDecimalDigits"/>.</description>
    ///   </item>
    ///   <item>
    ///     <term>"R"</term>
    ///     <term>Round-trip</term>
    ///     <description>Outputs the mantissa followed by <c>E</c> and then the exponent, always using the <see cref="CultureInfo.InvariantCulture"/>.</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        format = format?.Trim();
        var formatInfo = NumberFormatInfo.GetInstance(formatProvider);

        char formatSpecifier;
        int? precisionSpecifier = null;

        if (string.IsNullOrEmpty(format))
        {
            formatSpecifier = 'G';
        }
        else
        {
            formatSpecifier = char.ToUpperInvariant(format![0]);

            if (format.Length > 1)
            {
#if NETSTANDARD
                if (int.TryParse(format[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
#else
                if (int.TryParse(format.AsSpan()[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
#endif
                    precisionSpecifier = ps;
                else
                    Throw.FormatEx($"Invalid precision specifier: '{format[1..]}'");
            }
        }

        if (formatSpecifier is 'G')
        {
            BigDecimal value;

            if (precisionSpecifier is null || precisionSpecifier.GetValueOrDefault() is 0)
            {
                value = this;
            }
            else
            {
                int precision = precisionSpecifier.GetValueOrDefault();
                value = RoundToPrecision(this, precision, RoundingMode.MidpointAwayFromZero);

                if (GetEstimatedFullDecimalLength(value) > GetEstimatedExponentialLength(value))
                {
                    int exponentDecimals = Math.Min(value.Precision, precision) - 1;
                    return GetExponentialString(value, exponentDecimals);
                }
            }

            if (value._exponent >= 0)
                return GetIntegerString(value, "G");

            return GetDecimalString(value, "G", null);
        }

        if (formatSpecifier is 'F' or 'N')
        {
            string wholePartFormat = formatSpecifier is 'F' ? "F0" : "N0";

            int decimals = precisionSpecifier.HasValue ? precisionSpecifier.GetValueOrDefault() : formatInfo.NumberDecimalDigits;
            var value = Round(this, decimals, RoundingMode.MidpointAwayFromZero);

            if (decimals is 0)
                return GetIntegerString(value, wholePartFormat);

            return GetDecimalString(value, wholePartFormat, decimals);
        }

        if (formatSpecifier is 'E')
            return GetExponentialString(this, precisionSpecifier);

        if (formatSpecifier is 'C' or 'P')
        {
            BigDecimal value = this;

            if (formatSpecifier is 'P')
            {
                // Convert percentage format info params to currency params and write it out as a currency value:

                formatInfo = new NumberFormatInfo() {
                    CurrencySymbol = formatInfo.PercentSymbol,
                    CurrencyDecimalDigits = formatInfo.PercentDecimalDigits,
                    CurrencyDecimalSeparator = formatInfo.PercentDecimalSeparator,
                    CurrencyGroupSeparator = formatInfo.PercentGroupSeparator,
                    CurrencyGroupSizes = formatInfo.PercentGroupSizes,
                    CurrencyPositivePattern = PositivePercentagePatternToCurrencyPattern(formatInfo.PercentPositivePattern),
                    CurrencyNegativePattern = NegativePercentagePatternToCurrencyPattern(formatInfo.PercentNegativePattern),
                };

                value *= 100;
            }

            int decimals = precisionSpecifier.HasValue ? precisionSpecifier.GetValueOrDefault() : formatInfo.CurrencyDecimalDigits;
            value = Round(value, decimals, RoundingMode.MidpointAwayFromZero);

            if (decimals is 0)
                return GetIntegerString(value, "C0");

            return GetDecimalString(value, "C0", decimals);
        }

        if (formatSpecifier is not 'R')
            Throw.FormatEx($"Format specifier was invalid: '{formatSpecifier}'.");

        if (_exponent is 0)
            return _mantissa.ToString(CultureInfo.InvariantCulture);

        return ((FormattableString)$"{_mantissa}E{_exponent}").ToString(CultureInfo.InvariantCulture);

        static int GetEstimatedFullDecimalLength(BigDecimal value)
        {
            if (value._exponent >= 0)
                return value.Precision + value._exponent;

            return value.Precision + Math.Max(0, -value._exponent - value.Precision) + 1; // digits + additional leading zeros + decimal separator
        }

        static int GetEstimatedExponentialLength(BigDecimal value) => value.Precision + 5; // .E+99

        string GetExponentialString(BigDecimal value, int? precisionSpecifier)
        {
            string result = value._mantissa.ToString("E" + precisionSpecifier, formatInfo);

            if (value._exponent is 0)
                return result;

            int eIndex = result.LastIndexOf("E", StringComparison.Ordinal);

#if NETSTANDARD
            string exponentString = result[(eIndex + 1)..];
#else
            var exponentString = result.AsSpan()[(eIndex + 1)..];
#endif
            int exponent = int.Parse(exponentString, NumberStyles.AllowLeadingSign, formatInfo) + value._exponent;
            var mantissa = result.AsSpan()[..(eIndex + 1)];
            string absExponentString = Math.Abs(exponent).ToString(formatInfo);

            if (exponent > 0)
                return StringHelper.Concat(mantissa, formatInfo.PositiveSign.AsSpan(), absExponentString.AsSpan());

            return StringHelper.Concat(mantissa, formatInfo.NegativeSign.AsSpan(), absExponentString.AsSpan());
        }

        string GetDecimalString(BigDecimal value, string wholePartFormat, int? fixedDecimalPlaces)
        {
            var wholePart = Truncate(value);
            string wholeString;

            if (wholePart.IsZero && value.Sign < 0)
                wholeString = (-1).ToString(wholePartFormat, formatInfo).Replace('1', '0');
            else
                wholeString = GetIntegerString(wholePart, wholePartFormat);

            var decimalPart = Abs(value - wholePart);
            int decimalPartShift = -decimalPart._exponent;
            int decimalLeadingZeros = decimalPart.IsZero ? 0 : decimalPartShift - decimalPart.Precision;
            int decimalTrailingZeros = 0;

            if (fixedDecimalPlaces.HasValue)
                decimalTrailingZeros = Math.Max(0, fixedDecimalPlaces.GetValueOrDefault() - decimalPart.Precision - decimalLeadingZeros);

            decimalPart = decimalPart._mantissa;
            Debug.Assert(decimalPart._exponent is 0, "unexpected transformed decimal part exponent");

            string decimalString = GetIntegerString(decimalPart, "G");

            int insertPoint;

            for (insertPoint = wholeString.Length; insertPoint > 0; insertPoint--)
            {
                if (char.IsDigit(wholeString[insertPoint - 1]))
                    break;
            }

            string decimalSeparator = wholePartFormat[0] == 'C' ? formatInfo.CurrencyDecimalSeparator : formatInfo.NumberDecimalSeparator;

            var sb = new StringBuilder(wholeString.Length + decimalSeparator.Length + decimalLeadingZeros + decimalString.Length + decimalTrailingZeros);
#if NETSTANDARD
            sb.Append(wholeString[..insertPoint]);
#else
            sb.Append(wholeString.AsSpan()[..insertPoint]);
#endif
            sb.Append(decimalSeparator);
            sb.Append('0', decimalLeadingZeros);
            sb.Append(decimalString);
            sb.Append('0', decimalTrailingZeros);
#if NETSTANDARD
            sb.Append(wholeString[insertPoint..]);
#else
            sb.Append(wholeString.AsSpan()[insertPoint..]);
#endif

            return sb.ToString();
        }

        string GetIntegerString(BigDecimal value, string format)
        {
            Debug.Assert(value._exponent >= 0, "value contains decimal digits");
            BigInteger intValue = value._mantissa;

            if (value._exponent > 0)
                intValue *= BigIntegerPow10.Get(value._exponent);

            return intValue.ToString(format, formatInfo);
        }

        static int PositivePercentagePatternToCurrencyPattern(int positivePercentagePattern) => positivePercentagePattern switch {
            0 => 3,
            1 => 1,
            2 => 0,
            3 => 2,
            _ => Throw.NotSupportedEx<int>("Unsupported positive percentage pattern."),
        };

        static int NegativePercentagePatternToCurrencyPattern(int negativePercentagePattern) => negativePercentagePattern switch {
            0 => 8,
            1 => 5,
            2 => 1,
            3 => 2,
            4 => 3,
            5 => 6,
            6 => 7,
            7 => 9,
            8 => 10,
            9 => 11,
            10 => 12,
            11 => 13,
            _ => Throw.NotSupportedEx<int>("Unsupported negative percentage pattern."),
        };
    }
}