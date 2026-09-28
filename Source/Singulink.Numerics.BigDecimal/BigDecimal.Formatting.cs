using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <content>
/// Contains string formatting functionality for <see cref="BigDecimal"/>.
/// </content>
#if NET
partial struct BigDecimal : IFormattable, ISpanFormattable
#else
partial struct BigDecimal : IFormattable
#endif
{
    private const int StackAllocCharThreshold = 512;

    private enum FormatKind
    {
        /// <summary>
        /// Whole number output using the whole part format (i.e. "D", "F0", "N0" or "C0").
        /// </summary>
        Integer,

        /// <summary>
        /// Whole part output using the whole part format followed by a decimal separator and the fractional digits.
        /// </summary>
        Decimal,

        /// <summary>
        /// Scientific notation output (i.e. "1.23E+005").
        /// </summary>
        Exponential,

        /// <summary>
        /// Invariant culture mantissa followed by "E" and the exponent (i.e. "123E-2").
        /// </summary>
        RoundTrip,
    }

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
    ///     specifier is omitted then the value is written out in full precision decimal form. If a precision specifier is provided then the value is rounded
    ///     to that many significant digits and the more compact of either decimal form or scientific notation is used. Scientific notation exponents contain a
    ///     sign and at least two digits.</description>
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
    ///     <description>Exponential (scientific) notation. Precision specifier determines the number of decimal digits. Default value is 6. The exponent
    ///     always contains a sign and at least three digits.</description>
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
    /// <para>Format specifiers are case-insensitive, but lowercase "g" and "e" specifiers produce a lowercase exponent character ("e") when scientific
    /// notation is used, matching the behavior of the standard .NET numeric types.</para>
    /// <para>Values are rounded using <see cref="RoundingMode.MidpointAwayFromZero"/> when the format does not have enough precision to represent the value
    /// exactly.</para>
    /// </remarks>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        var plan = FormatPlan.Create(this, format.AsSpan(), formatProvider);
        int maxLength = plan.GetMaxLength();

        if (maxLength <= StackAllocCharThreshold)
        {
            Span<char> buffer = stackalloc char[StackAllocCharThreshold];

            if (plan.TryWrite(buffer, out int charsWritten))
                return buffer[..charsWritten].ToString();

            Debug.Fail("estimated max length was too small");
        }

        return ToStringRented(plan, maxLength);
    }

    /// <summary>
    /// Tries to format this value into the provided span of characters.
    /// </summary>
    /// <param name="destination">The span in which to write this value formatted as a span of characters.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination"/>.</param>
    /// <param name="format">The string format to use. The "G" format is used if none is provided.</param>
    /// <param name="provider">The format provider that will be used to obtain number format information. The current culture is used if none is
    /// provided.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/> if <paramref name="destination"/> was too
    /// small.</returns>
    /// <remarks>
    /// <para>See <see cref="ToString(string?, IFormatProvider?)"/> for details on the supported format specifiers.</para>
    /// </remarks>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        return FormatPlan.Create(this, format, provider).TryWrite(destination, out charsWritten);
    }

    private static string ToStringRented(in FormatPlan plan, int length)
    {
        while (true)
        {
            char[] buffer = ArrayPool<char>.Shared.Rent(length);

            try
            {
                if (plan.TryWrite(buffer, out int charsWritten))
                    return new string(buffer, 0, charsWritten);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }

            Debug.Fail("estimated max length was too small");
            length = checked(length * 2);
        }
    }

    /// <summary>
    /// Formats an integer using the specified standard format string and writes it to the destination.
    /// </summary>
    private static bool TryFormatInteger(BigInteger value, Span<char> destination, out int charsWritten, string format, NumberFormatInfo info)
    {
#if NET
        return value.TryFormat(destination, out charsWritten, format, info);
#else
        return TryCopy(value.ToString(format, info), destination, out charsWritten);
#endif
    }

    private static bool TryFormatInt32(int value, Span<char> destination, out int charsWritten, NumberFormatInfo info)
    {
#if NET
        return value.TryFormat(destination, out charsWritten, default, info);
#else
        return TryCopy(value.ToString(info), destination, out charsWritten);
#endif
    }

#if !NET
    private static bool TryCopy(string value, Span<char> destination, out int charsWritten)
    {
        if (destination.Length < value.Length)
        {
            charsWritten = 0;
            return false;
        }

        value.AsSpan().CopyTo(destination);
        charsWritten = value.Length;
        return true;
    }
#endif

    /// <summary>
    /// Gets the index just past the last ASCII digit in the span (or 0 if there are no digits).
    /// </summary>
    private static int GetDigitsEnd(ReadOnlySpan<char> value)
    {
        int end = value.Length;

        while (end > 0 && !IsAsciiDigit(value[end - 1]))
            end--;

        return end;
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static int CountDigits(ulong value)
    {
        int count = 1;

        while (value >= 10)
        {
            value /= 10;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Writes the digits of the value right-aligned into the destination, filling any remaining leading characters with zeros.
    /// </summary>
    private static void WriteDigits(ulong value, Span<char> destination)
    {
        for (int i = destination.Length - 1; i >= 0; i--)
        {
            destination[i] = (char)('0' + (int)(value % 10));
            value /= 10;
        }

        Debug.Assert(value is 0, "destination was too small to hold all the digits");
    }

    /// <summary>
    /// Describes how a value should be formatted after the format string has been parsed and the value has been rounded to the required precision.
    /// </summary>
    private readonly struct FormatPlan
    {
        private const string IntegerFormat = "D";
        private const string FixedPointFormat = "F0";
        private const string NumberFormat = "N0";
        private const string CurrencyFormat = "C0";

        private const int DefaultExponentialDecimals = 6;
        private const int ExponentialMinExponentDigits = 3;
        private const int GeneralMinExponentDigits = 2;

        private readonly BigDecimal _value;
        private readonly NumberFormatInfo _info;
        private readonly string _wholeFormat;
        private readonly FormatKind _kind;
        private readonly int _decimals;
        private readonly int _minExponentDigits;
        private readonly char _exponentChar;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormatPlan"/> struct.
        /// </summary>
        /// <param name="kind">The kind of output to produce.</param>
        /// <param name="value">The value to format, already rounded as required by the format.</param>
        /// <param name="info">The number format info to use.</param>
        /// <param name="wholeFormat">The standard integer format string used to format the whole part of the value.</param>
        /// <param name="decimals">For <see cref="FormatKind.Decimal"/>: the fixed number of decimal places, or -1 to output only as many as needed. For <see
        /// cref="FormatKind.Exponential"/>: the number of digits after the decimal separator.</param>
        /// <param name="minExponentDigits">Minimum number of exponent digits for <see cref="FormatKind.Exponential"/>.</param>
        /// <param name="exponentChar">The exponent character for <see cref="FormatKind.Exponential"/>.</param>
        private FormatPlan(FormatKind kind, BigDecimal value, NumberFormatInfo info, string wholeFormat, int decimals, int minExponentDigits = 0, char exponentChar = 'E')
        {
            _kind = kind;
            _value = value;
            _info = info;
            _wholeFormat = wholeFormat;
            _decimals = decimals;
            _minExponentDigits = minExponentDigits;
            _exponentChar = exponentChar;
        }

        private bool IsCurrency => _wholeFormat == CurrencyFormat;

        private bool HasGroupSeparators => _wholeFormat is NumberFormat or CurrencyFormat;

        public static FormatPlan Create(BigDecimal value, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            format = format.Trim();
            var info = NumberFormatInfo.GetInstance(provider);

            char specifier = 'G';
            int precision = -1;

            if (format.Length > 0)
            {
                specifier = format[0];

                if (format.Length > 1 && !TryParsePrecision(format[1..], out precision))
                    Throw.FormatEx($"Invalid precision specifier: '{format[1..].ToString()}'");
            }

            switch (char.ToUpperInvariant(specifier))
            {
                case 'G':
                    return CreateGeneral(value, info, precision, specifier is 'g' ? 'e' : 'E');
                case 'F':
                    return CreateFixed(value, info, FixedPointFormat, precision);
                case 'N':
                    return CreateFixed(value, info, NumberFormat, precision);
                case 'E':
                    return CreateExponential(value, info, precision < 0 ? DefaultExponentialDecimals : precision, ExponentialMinExponentDigits, specifier is 'e' ? 'e' : 'E');
                case 'C':
                    return CreateCurrency(value, info, precision);
                case 'P':
                    return CreateCurrency(ShiftDecimal(value, 2), CreatePercentInfo(info), precision);
                case 'R':
                    return new FormatPlan(FormatKind.RoundTrip, value, info, IntegerFormat, 0);
                default:
                    return Throw.FormatEx<FormatPlan>($"Format specifier was invalid: '{specifier}'.");
            }
        }

        /// <summary>
        /// Gets a length that is guaranteed to be large enough to hold the formatted output.
        /// </summary>
        public int GetMaxLength()
        {
            var info = _info;
            long length;

            switch (_kind)
            {
                case FormatKind.RoundTrip:
                    length = 1 + (long)_value.Precision + 1 + 11; // sign + digits + 'E' + exponent sign and digits
                    break;

                case FormatKind.Exponential:
                    length = info.NegativeSign.Length + 1;

                    if (_decimals > 0)
                        length += info.NumberDecimalSeparator.Length + (long)_decimals;

                    length += 1 + Math.Max(info.PositiveSign.Length, info.NegativeSign.Length) + Math.Max(_minExponentDigits, 20);
                    break;

                default:
                    long wholeDigits = Math.Max(1, (long)_value.Precision + _value._exponent);
                    long fractionDigits = _kind is FormatKind.Decimal ? (_decimals >= 0 ? _decimals : -(long)_value._exponent) : 0;

                    string groupSeparator = IsCurrency ? info.CurrencyGroupSeparator : info.NumberGroupSeparator;
                    string decimalSeparator = IsCurrency ? info.CurrencyDecimalSeparator : info.NumberDecimalSeparator;

                    long groupSeparators = HasGroupSeparators ? wholeDigits * groupSeparator.Length : 0;
                    int symbols = info.NegativeSign.Length + (IsCurrency ? info.CurrencySymbol.Length : 0) + 4; // sign + currency symbol + parenthesis/spaces

                    length = wholeDigits + groupSeparators + decimalSeparator.Length + fractionDigits + symbols;
                    break;
            }

            return length >= int.MaxValue ? int.MaxValue : (int)length;
        }

        public bool TryWrite(Span<char> destination, out int charsWritten)
        {
            switch (_kind)
            {
                case FormatKind.RoundTrip:
                    return TryWriteRoundTrip(destination, out charsWritten);
                case FormatKind.Exponential:
                    return TryWriteExponential(destination, out charsWritten);
                case FormatKind.Integer:
                    return TryWriteInteger(_value, destination, out charsWritten);
                default:
                    return TryWriteDecimal(destination, out charsWritten);
            }
        }

        private static bool TryParsePrecision(ReadOnlySpan<char> value, out int precision)
        {
            long result = 0;

            foreach (char c in value)
            {
                if (!IsAsciiDigit(c))
                {
                    precision = 0;
                    return false;
                }

                result = (result * 10) + (c - '0');

                if (result > int.MaxValue)
                {
                    precision = 0;
                    return false;
                }
            }

            precision = (int)result;
            return true;
        }

        private static FormatPlan CreateGeneral(BigDecimal value, NumberFormatInfo info, int precision, char exponentChar)
        {
            if (precision > 0)
            {
                value = RoundToPrecision(value, precision, RoundingMode.MidpointAwayFromZero);

                if (GetEstimatedFullDecimalLength(value) > GetEstimatedExponentialLength(value))
                {
                    int exponentDecimals = Math.Min(value.Precision, precision) - 1;
                    return new FormatPlan(FormatKind.Exponential, value, info, IntegerFormat, exponentDecimals, GeneralMinExponentDigits, exponentChar);
                }
            }

            return CreateIntegerOrDecimal(value, info, IntegerFormat, -1);

            static long GetEstimatedFullDecimalLength(BigDecimal value)
            {
                if (value._exponent >= 0)
                    return (long)value.Precision + value._exponent;

                return value.Precision + Math.Max(0, -(long)value._exponent - value.Precision) + 1; // digits + additional leading zeros + decimal separator
            }

            static long GetEstimatedExponentialLength(BigDecimal value) => value.Precision + 5L; // .E+99
        }

        private static FormatPlan CreateFixed(BigDecimal value, NumberFormatInfo info, string wholeFormat, int precision)
        {
            int decimals = precision < 0 ? info.NumberDecimalDigits : precision;
            return CreateIntegerOrDecimal(Round(value, decimals, RoundingMode.MidpointAwayFromZero), info, wholeFormat, decimals);
        }

        private static FormatPlan CreateCurrency(BigDecimal value, NumberFormatInfo info, int precision)
        {
            int decimals = precision < 0 ? info.CurrencyDecimalDigits : precision;
            return CreateIntegerOrDecimal(Round(value, decimals, RoundingMode.MidpointAwayFromZero), info, CurrencyFormat, decimals);
        }

        private static FormatPlan CreateExponential(BigDecimal value, NumberFormatInfo info, int decimals, int minExponentDigits, char exponentChar)
        {
            if (decimals < value.Precision - 1)
                value = RoundToPrecision(value, decimals + 1, RoundingMode.MidpointAwayFromZero);

            return new FormatPlan(FormatKind.Exponential, value, info, IntegerFormat, decimals, minExponentDigits, exponentChar);
        }

        /// <summary>
        /// Creates a plan that outputs the value in decimal form, or as a whole number if the value has no fractional part and no fixed decimals are
        /// required.
        /// </summary>
        /// <param name="value">The value to format, already rounded to the required number of decimals.</param>
        /// <param name="info">The number format info to use.</param>
        /// <param name="wholeFormat">The standard integer format string used to format the whole part of the value.</param>
        /// <param name="decimals">The fixed number of decimal places, or -1 to output only as many as needed.</param>
        private static FormatPlan CreateIntegerOrDecimal(BigDecimal value, NumberFormatInfo info, string wholeFormat, int decimals)
        {
            if (value._exponent >= 0 && decimals <= 0)
                return new FormatPlan(FormatKind.Integer, value, info, wholeFormat, 0);

            return new FormatPlan(FormatKind.Decimal, value, info, wholeFormat, decimals);
        }

        /// <summary>
        /// Creates a format info that maps the percent format parameters onto the currency format parameters so that percentages can be written out as
        /// currency values.
        /// </summary>
        private static NumberFormatInfo CreatePercentInfo(NumberFormatInfo info)
        {
            var percentInfo = (NumberFormatInfo)info.Clone();

            percentInfo.CurrencySymbol = info.PercentSymbol;
            percentInfo.CurrencyDecimalDigits = info.PercentDecimalDigits;
            percentInfo.CurrencyDecimalSeparator = info.PercentDecimalSeparator;
            percentInfo.CurrencyGroupSeparator = info.PercentGroupSeparator;
            percentInfo.CurrencyGroupSizes = info.PercentGroupSizes;
            percentInfo.CurrencyPositivePattern = PositivePercentagePatternToCurrencyPattern(info.PercentPositivePattern);
            percentInfo.CurrencyNegativePattern = NegativePercentagePatternToCurrencyPattern(info.PercentNegativePattern);

            return percentInfo;

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

        private bool TryWriteRoundTrip(Span<char> destination, out int charsWritten)
        {
            var invariantInfo = NumberFormatInfo.InvariantInfo;

            if (!TryFormatInteger(_value._mantissa, destination, out charsWritten, IntegerFormat, invariantInfo))
                return false;

            if (_value._exponent is 0)
                return true;

            if (destination.Length > charsWritten && TryFormatInt32(_value._exponent, destination[(charsWritten + 1)..], out int exponentLength, invariantInfo))
            {
                destination[charsWritten] = 'E';
                charsWritten += 1 + exponentLength;
                return true;
            }

            charsWritten = 0;
            return false;
        }

        private bool TryWriteExponential(Span<char> destination, out int charsWritten)
        {
            var value = _value;
            var info = _info;

            int digitCount = value.Precision;
            Debug.Assert(digitCount <= _decimals + 1, "value has more significant digits than the format allows");

            long exponent = value.IsZero ? 0 : (long)value.Precision - 1 + value._exponent;
            string exponentSign = exponent < 0 ? info.NegativeSign : info.PositiveSign;
            ulong absExponent = (ulong)Math.Abs(exponent);
            int exponentDigits = Math.Max(_minExponentDigits, CountDigits(absExponent));

            string decimalSeparator = info.NumberDecimalSeparator;
            int signLength = value.Sign < 0 ? info.NegativeSign.Length : 0;
            int fractionLength = _decimals > 0 ? decimalSeparator.Length + _decimals : 0;
            int totalLength = signLength + 1 + fractionLength + 1 + exponentSign.Length + exponentDigits;

            if (destination.Length < totalLength)
            {
                charsWritten = 0;
                return false;
            }

            // Writes the sign (if negative) followed by all the digits:

            bool formatted = TryFormatInteger(value._mantissa, destination, out int mantissaLength, IntegerFormat, info);
            Debug.Assert(formatted && mantissaLength == signLength + digitCount, "unexpected mantissa format result");

            int position = signLength + 1;

            if (_decimals > 0)
            {
                // Shift the digits after the first digit over to make room for the decimal separator and then pad with zeros to the required decimals.

                int remainingDigits = digitCount - 1;
                destination.Slice(position, remainingDigits).CopyTo(destination[(position + decimalSeparator.Length)..]);
                decimalSeparator.AsSpan().CopyTo(destination[position..]);
                position += decimalSeparator.Length + remainingDigits;

                int zeroPadding = _decimals - remainingDigits;
                destination.Slice(position, zeroPadding).Fill('0');
                position += zeroPadding;
            }

            destination[position++] = _exponentChar;
            exponentSign.AsSpan().CopyTo(destination[position..]);
            position += exponentSign.Length;
            WriteDigits(absExponent, destination.Slice(position, exponentDigits));
            position += exponentDigits;

            charsWritten = position;
            return true;
        }

        private bool TryWriteInteger(BigDecimal value, Span<char> destination, out int charsWritten)
        {
            Debug.Assert(value._exponent >= 0, "value contains decimal digits");

            int trailingZeros = value._exponent;

            if (trailingZeros is 0 || HasGroupSeparators)
            {
                var intValue = trailingZeros is 0 ? value._mantissa : value._mantissa * BigIntegerPow10.Get(trailingZeros);
                return TryFormatInteger(intValue, destination, out charsWritten, _wholeFormat, _info);
            }

            // Formats without group separators can format the mantissa and then insert the trailing zeros after the last digit, which avoids
            // potentially huge multiplications for values with large exponents.

            if (!TryFormatInteger(value._mantissa, destination, out int mantissaLength, _wholeFormat, _info) || destination.Length - mantissaLength < trailingZeros)
            {
                charsWritten = 0;
                return false;
            }

            int insertPoint = GetDigitsEnd(destination[..mantissaLength]);
            destination[insertPoint..mantissaLength].CopyTo(destination[(insertPoint + trailingZeros)..]);
            destination.Slice(insertPoint, trailingZeros).Fill('0');

            charsWritten = mantissaLength + trailingZeros;
            return true;
        }

        private bool TryWriteDecimal(Span<char> destination, out int charsWritten)
        {
            var value = _value;

            int fractionDigits = value._exponent < 0 ? -value._exponent : 0;
            int trailingZeros = _decimals >= 0 ? _decimals - fractionDigits : 0;
            Debug.Assert(trailingZeros >= 0, "value was not rounded to the required decimals");

            string decimalSeparator = IsCurrency ? _info.CurrencyDecimalSeparator : _info.NumberDecimalSeparator;

            int wholeLength;
            BigInteger fraction = default;

            if (fractionDigits is 0)
            {
                if (!TryWriteInteger(value, destination, out wholeLength))
                {
                    charsWritten = 0;
                    return false;
                }
            }
            else
            {
                var whole = BigInteger.DivRem(value._mantissa, BigIntegerPow10.Get(fractionDigits), out fraction);

                if (fraction.Sign < 0)
                    fraction = -fraction;

                bool formatted;

                if (whole.IsZero && value.Sign < 0)
                {
                    // Negative values between -1 and 0 have a zero whole part which must still be formatted as negative (i.e. "-0", "(¤0)", etc.) so
                    // format -1 instead and replace the digit with a zero.

                    formatted = TryFormatInteger(BigInteger.MinusOne, destination, out wholeLength, _wholeFormat, _info);

                    if (formatted)
                        destination[destination[..wholeLength].IndexOf('1')] = '0';
                }
                else
                {
                    formatted = TryFormatInteger(whole, destination, out wholeLength, _wholeFormat, _info);
                }

                if (!formatted)
                {
                    charsWritten = 0;
                    return false;
                }
            }

            int fractionLength = decimalSeparator.Length + fractionDigits + trailingZeros;

            if (destination.Length - wholeLength < fractionLength)
            {
                charsWritten = 0;
                return false;
            }

            // Insert the decimal separator and fractional digits after the last whole digit, shifting anything that follows the digits (i.e. currency
            // symbols, closing parenthesis, trailing negative signs) to the end.

            int insertPoint = GetDigitsEnd(destination[..wholeLength]);
            destination[insertPoint..wholeLength].CopyTo(destination[(insertPoint + fractionLength)..]);

            decimalSeparator.AsSpan().CopyTo(destination[insertPoint..]);
            int position = insertPoint + decimalSeparator.Length;

            if (fractionDigits > 0)
            {
                var fractionDestination = destination.Slice(position, fractionDigits);

                bool formatted = TryFormatInteger(fraction, fractionDestination, out int digitCount, IntegerFormat, NumberFormatInfo.InvariantInfo);
                Debug.Assert(formatted && digitCount <= fractionDigits, "unexpected fraction format result");

                int leadingZeros = fractionDigits - digitCount;

                if (leadingZeros > 0)
                {
                    fractionDestination[..digitCount].CopyTo(fractionDestination[leadingZeros..]);
                    fractionDestination[..leadingZeros].Fill('0');
                }

                position += fractionDigits;
            }

            destination.Slice(position, trailingZeros).Fill('0');

            charsWritten = wholeLength + fractionLength;
            return true;
        }
    }
}