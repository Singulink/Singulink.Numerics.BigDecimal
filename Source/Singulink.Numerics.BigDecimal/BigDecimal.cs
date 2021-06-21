using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics
{
    /// <summary>
    /// Represents an arbitrary precision decimal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All operations on <see cref="BigDecimal"/> values are exact except division in the case of a repeating decimal result. If the result of the division
    /// cannot be exactly represented in decimal form then the largest of the dividend precision, divisor precision and the specified maximum extended
    /// precision is used to represent the result. You can specify the maximum extended precision to use for each division operation by calling the <see
    /// cref="Divide(BigDecimal, BigDecimal, int, RoundingMode)"/> method or use the <see cref="DivideExact(BigDecimal, BigDecimal)"/> / <see
    /// cref="TryDivideExact(BigDecimal, BigDecimal, out BigDecimal)"/> methods for division operations that are expected to return exact results. The standard
    /// division operator (<c>/</c>) first attempts to do an exact division and falls back to extended precision division using <see
    /// cref="MaxExtendedDivisionPrecision"/> as the maximum extended precision parameter.</para>
    /// <para>
    /// Addition and subtraction are fully commutitive and associative for all converted data types. This makes <see cref="BigDecimal"/> a great data type to
    /// store aggregate totals that can freely add and subtract values without accruing inaccuracies over time.</para>
    /// <para>
    /// Conversions from floating-point types (<see cref="float"/>/<see cref="double"/>) must be done explicitly using <see cref="FromSingle(float,
    /// FloatConversion)"/> or <see cref="FromDouble(double, FloatConversion)"/> as there are several conversion modes to choose from that are each suitable in
    /// different situations.</para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>, IFormattable
    {
        #region Static Contants/Fields/Properties

        private const string ToDecimalOrFloatFormat = "R";
        private const NumberStyles ToDecimalOrFloatStyle = NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;

        private const string FromFloatFormat = "G";
        private const NumberStyles FromFloatStyle = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;

        // A max size of 1024 conveniently fits all the base2 double exponent ranges needed for the base5 cache to do fast double => BigDecimal conversions
        // and also happens to be a good limit for the base10 cache.

        // Num bytes needed to store a number with d digits = d * Log(10)/Log(2)/8 = d * 0.415
        // 1024 entries * 512 avg entry digits = max 524k digits in memory * 0.415 = ~220kb max memory usage if cache grows to max size

        private static readonly BigIntegerPowCache BigIntegerPow10 = BigIntegerPowCache.GetCache(10);
        private static readonly BigIntegerPowCache BigIntegerPow5 = BigIntegerPowCache.GetCache(5);

        /// <summary>
        /// Gets the maximum extended precision used by the division operator if the result is not exact (i.e. has repeating decimals). If the dividend or
        /// divisor precision is greater then that value is used instead. The current value is 50 but is subject to change.
        /// </summary>
        /// <remarks>
        /// <para>For better control over the result of each division operation see the <see cref="Divide(BigDecimal, BigDecimal, int, RoundingMode)"/>,
        /// <see cref="DivideExact(BigDecimal, BigDecimal)"/> and <see cref="TryDivideExact(BigDecimal, BigDecimal, out BigDecimal)"/> methods.</para>
        /// </remarks>
        public static int MaxExtendedDivisionPrecision => 50;

        /// <summary>
        /// Gets a value representing zero (0).
        /// </summary>
        public static BigDecimal Zero => BigInteger.Zero;

        /// <summary>
        /// Gets a value representing one (1).
        /// </summary>
        public static BigDecimal One => BigInteger.One;

        /// <summary>
        /// Gets a value representing negative one (-1).
        /// </summary>
        public static BigDecimal MinusOne => BigInteger.MinusOne;

        #endregion

        // Do not change the order of fields

        private readonly BigInteger _mantissa;
        private readonly int _exponent;
        private readonly SharedPrecisionHolder _precision;

        /// <summary>
        /// Gets a value indicating whether the current value is 0.
        /// </summary>
        public bool IsZero => _mantissa.IsZero;

        /// <summary>
        /// Gets a value indicating whether the current value is 1.
        /// </summary>
        public bool IsOne => _mantissa.IsOne && _exponent == 0;

        /// <summary>
        /// Gets a number indicating the sign (negative, positive, or zero) of the current value.
        /// </summary>
        public int Sign => _mantissa.Sign;

        /// <summary>
        /// Gets the precision of this value, i.e. the total number of digits it contains (excluding any leading/trailing zeros). Zero values have a precision of 1.
        /// </summary>
        public int Precision {
            get {
                if (_precision.Value != 0)
                    return _precision.Value;

                return GetAndCachePrecision();
            }
        }

        /// <summary>
        /// Gets the number of digits that appear after the decimal point.
        /// </summary>
        public int DecimalPlaces => _exponent >= 0 ? 0 : -_exponent;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => ToString("G100", CultureInfo.InvariantCulture);

        /// <summary>
        /// Initializes a new instance of the <see cref="BigDecimal"/> struct.
        /// </summary>
        public BigDecimal(BigInteger mantissa, int exponent)
        {
            if (mantissa.IsZero) {
                _mantissa = mantissa;
                _exponent = 0;
                _precision = SharedPrecisionHolder.Get(1);
            }
            else {
                // ToString() method benchmarked faster than repeated DivRem(10), even with an exponentially increasing divisor to reduce iterations.
                // Bonus that we get to determine precision up front at no cost.

                string ms = BigInteger.Abs(mantissa).ToString(CultureInfo.InvariantCulture);
                int precision = ms.Length;
                int shift = 0;

                for (int i = ms.Length - 1; i > 0 && ms[i] == '0'; i--)
                    shift++;

                if (shift > 0) {
                    mantissa /= BigIntegerPow10.Get(shift);
                    exponent += shift;
                    precision -= shift;
                }

                _mantissa = mantissa;
                _exponent = exponent;
                _precision = SharedPrecisionHolder.Get(precision);
            }
        }

        // Trusted private constructor

        private BigDecimal(BigInteger mantissa, int exponent, SharedPrecisionHolder precision)
        {
            _mantissa = mantissa;
            _exponent = exponent;
            _precision = precision;
        }

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        #region Conversions to BigDecimal

        public static implicit operator BigDecimal(BigInteger value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(int value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(uint value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(long value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(ulong value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(decimal value)
        {
            ref var decimalData = ref Unsafe.As<decimal, DecimalData>(ref value);

            var mantissa = (new BigInteger(decimalData.Hi) << 8) + decimalData.Lo;

            if (!decimalData.IsPositive)
                mantissa = -mantissa;

            return new BigDecimal(mantissa, -decimalData.Scale);
        }

        #endregion

        #region Conversions from BigDecimal

        public static explicit operator float(BigDecimal value)
        {
            return float.Parse(value.ToString(ToDecimalOrFloatFormat), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
        }

        public static explicit operator double(BigDecimal value)
        {
            return double.Parse(value.ToString(ToDecimalOrFloatFormat), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
        }

        public static explicit operator decimal(BigDecimal value)
        {
            return decimal.Parse(value.ToString(ToDecimalOrFloatFormat), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
        }

        public static explicit operator BigInteger(BigDecimal value)
        {
            return value._exponent < 0 ? value._mantissa / BigIntegerPow10.Get(-value._exponent) : value._mantissa * BigIntegerPow10.Get(value._exponent);
        }

        public static explicit operator int(BigDecimal value) => (int)(BigInteger)value;

        public static explicit operator uint(BigDecimal value) => (uint)(BigInteger)value;

        public static explicit operator long(BigDecimal value) => (long)(BigInteger)value;

        public static explicit operator ulong(BigDecimal value) => (ulong)(BigInteger)value;

        #endregion

        #region Mathematical Operators

        public static BigDecimal operator +(BigDecimal value) => value;

        public static BigDecimal operator -(BigDecimal value) => new BigDecimal(BigInteger.Negate(value._mantissa), value._exponent, value._precision);

        public static BigDecimal operator ++(BigDecimal value) => value + One;

        public static BigDecimal operator --(BigDecimal value) => value - One;

        public static BigDecimal operator +(BigDecimal left, BigDecimal right)
        {
            if (left.IsZero)
                return right;

            if (right.IsZero)
                return left;

            return left._exponent > right._exponent
                ? new BigDecimal(AlignMantissa(left, right) + right._mantissa, right._exponent)
                : new BigDecimal(AlignMantissa(right, left) + left._mantissa, left._exponent);
        }

        public static BigDecimal operator -(BigDecimal left, BigDecimal right) => left + (-right);

        public static BigDecimal operator *(BigDecimal left, BigDecimal right)
        {
            if (left.IsZero || right.IsZero)
                return Zero;

            if (left.IsOne)
                return right;

            if (right.IsOne)
                return left;

            return new BigDecimal(left._mantissa * right._mantissa, left._exponent + right._exponent);
        }

        public static BigDecimal operator /(BigDecimal dividend, BigDecimal divisor)
        {
            if (TryDivideExact(dividend, divisor, out var result))
                return result;

            return Divide(dividend, divisor, MaxExtendedDivisionPrecision);
        }

        public static BigDecimal operator %(BigDecimal left, BigDecimal right) => left - (right * Floor(left / right));

        public static bool operator ==(BigDecimal left, BigDecimal right) => left._exponent == right._exponent && left._mantissa == right._mantissa;

        public static bool operator !=(BigDecimal left, BigDecimal right) => left._exponent != right._exponent || left._mantissa != right._mantissa;

        public static bool operator <(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignMantissa(left, right) < right._mantissa : left._mantissa < AlignMantissa(right, left);
        }

        public static bool operator >(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignMantissa(left, right) > right._mantissa : left._mantissa > AlignMantissa(right, left);
        }

        public static bool operator <=(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignMantissa(left, right) <= right._mantissa : left._mantissa <= AlignMantissa(right, left);
        }

        public static bool operator >=(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignMantissa(left, right) >= right._mantissa : left._mantissa >= AlignMantissa(right, left);
        }

        #endregion

        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #region Conversion Methods

        /// <summary>
        /// Gets a <see cref="BigDecimal"/> representation of a <see cref="float"/> value.
        /// </summary>
        public static BigDecimal FromSingle(float value, FloatConversion conversionMode)
        {
            return conversionMode switch {
                FloatConversion.Roundtrip => FromFloat(value, 9),
                FloatConversion.Truncate => FromFloat(value, 7),
                FloatConversion.Exact => FromFloat(value, 0),
                FloatConversion.ParseString => Parse(value.ToString(FromFloatFormat, CultureInfo.InvariantCulture).AsSpan(), FromFloatStyle, CultureInfo.InvariantCulture),
                _ => throw new ArgumentOutOfRangeException(nameof(conversionMode)),
            };
        }

        /// <summary>
        /// Gets a <see cref="BigDecimal"/> representation of a <see cref="double"/> value.
        /// </summary>
        public static BigDecimal FromDouble(double value, FloatConversion conversionMode)
        {
            return conversionMode switch {
                FloatConversion.Roundtrip => FromFloat(value, 17),
                FloatConversion.Truncate => FromFloat(value, 15),
                FloatConversion.Exact => FromFloat(value, 0),
                FloatConversion.ParseString => Parse(value.ToString(FromFloatFormat, CultureInfo.InvariantCulture).AsSpan(), FromFloatStyle, CultureInfo.InvariantCulture),
                _ => throw new ArgumentOutOfRangeException(nameof(conversionMode)),
            };
        }

        private static BigDecimal FromFloat(double value, int precision)
        {
            if (double.IsNaN(value))
                throw new ArgumentException("Floating point value cannot be NaN.", nameof(value));

            if (double.IsNegativeInfinity(value) || double.IsPositiveInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot convert floating point infinity values.");

            Debug.Assert(precision is 0 or 7 or 9 or 15 or 17, "unexpected precision value");

            unchecked {
                // Based loosely on Jon Skeet's DoubleConverter:

                long bits = BitConverter.DoubleToInt64Bits(value);
                bool negative = bits < 0;
                int exponent = (int)((bits >> 52) & 0x7ffL);
                long mantissa = bits & 0xfffffffffffffL;

                // Subnormal numbers: exponent is effectively one higher, but there's no extra normalisation bit in the mantissa Normal numbers.
                // Leave exponent as it is but add extra bit to the front of the mantissa
                if (exponent == 0)
                    exponent++;
                else
                    mantissa |= 1L << 52;

                if (mantissa == 0)
                    return Zero;

                // Bias the exponent. It's actually biased by 1023, but we're treating the mantissa as m.0 rather than 0.m, so we need to subtract another 52
                // from it.
                exponent -= 1075;

                // Normalize

                while ((mantissa & 1) == 0) { // mantissa is even
                    mantissa >>= 1;
                    exponent++;
                }

                if (negative)
                    mantissa = -mantissa;

                var resultMantissa = (BigInteger)mantissa;
                int resultExponent;
                bool trimTrailingZeros;

                if (exponent == 0) {
                    resultExponent = 0;
                    trimTrailingZeros = false;
                }
                else if (exponent < 0) {
                    resultMantissa *= BigIntegerPow5.Get(-exponent);
                    resultExponent = exponent;
                    trimTrailingZeros = false;
                }
                else { // exponent > 0
                    resultMantissa <<= exponent; // *= BigInteger.Pow(BigInt2, exponent);
                    resultExponent = 0;
                    trimTrailingZeros = true;
                }

                if (precision > 0) {
                    int digits = resultMantissa.CountDigits();
                    int extraDigits = digits - precision;

                    if (extraDigits > 0) {
                        resultMantissa = resultMantissa.Divide(BigIntegerPow10.Get(extraDigits));
                        resultExponent += extraDigits;
                        trimTrailingZeros = true;
                    }
                }

                return trimTrailingZeros ? new BigDecimal(resultMantissa, resultExponent) : new BigDecimal(resultMantissa, resultExponent, SharedPrecisionHolder.Create());
            }
        }

        #endregion

        #region Mathematical Functions

        /// <summary>
        /// Gets the absolute value of the given value.
        /// </summary>
        public static BigDecimal Abs(BigDecimal value) => value._mantissa.Sign >= 0 ? value : -value;

        /// <summary>
        /// Performs a division operation using the specified maximum extended precision.
        /// </summary>
        /// <param name="dividend">The dividend of the division operation.</param>
        /// <param name="divisor">The divisor of the division operation.</param>
        /// <param name="maxExtendedPrecision">If the result of the division does not fit into the precision of the dividend or divisor then this extended
        /// precision is used.</param>
        /// <param name="mode">The rounding mode to use.</param>
        public static BigDecimal Divide(BigDecimal dividend, BigDecimal divisor, int maxExtendedPrecision, RoundingMode mode = RoundingMode.MidpointToEven)
        {
            if (divisor.IsZero)
                throw new DivideByZeroException();

            if (maxExtendedPrecision <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExtendedPrecision));

            if (divisor.IsOne)
                return dividend;

            if (dividend.IsZero)
                return Zero;

            // Never reduce precision of the result compared to input values but cap precision extensions to maxExtendedPrecision

            int maxPrecision = Math.Max(Math.Max(maxExtendedPrecision, dividend.Precision), divisor.Precision);
            int exponentChange = Math.Max(0, maxPrecision - dividend.Precision + divisor.Precision);
            var dividendMantissa = dividend._mantissa * BigIntegerPow10.Get(exponentChange);
            int exponent = dividend._exponent - divisor._exponent - exponentChange;

            return new BigDecimal(dividendMantissa.Divide(divisor._mantissa, mode), exponent);
        }

        /// <summary>
        /// Performs a division operation that results in an exact decimal answer (i.e. no repeating decimals).
        /// </summary>
        /// <param name="dividend">The dividend of the division operation.</param>
        /// <param name="divisor">The divisor of the division operation.</param>
        /// <exception cref="ArithmeticException">The result could not be represented exactly as a decimal value.</exception>
        public static BigDecimal DivideExact(BigDecimal dividend, BigDecimal divisor)
        {
            if (!TryDivideExact(dividend, divisor, out var result))
                throw new ArithmeticException("The result of the division could not be represented exactly as a decimal value.");

            return result;
        }

        /// <summary>
        /// Performs a division operation that results in an exact decimal answer (i.e. no repeating decimals).
        /// </summary>
        /// <param name="dividend">The dividend of the division operation.</param>
        /// <param name="divisor">The divisor of the division operation.</param>
        /// <param name="result">The result of the division operation.</param>
        /// <returns><see langword="true"/> if an exact result could be produced, otherwise <see langword="false"/>.</returns>
        public static bool TryDivideExact(BigDecimal dividend, BigDecimal divisor, out BigDecimal result)
        {
            if (divisor.IsZero)
                throw new DivideByZeroException();

            if (dividend.IsZero) {
                result = Zero;
                return true;
            }

            if (BigInteger.Abs(divisor._mantissa).IsOne) {
                result = divisor switch {
                    { IsOne: true } => dividend,
                    { Sign: < 0 } => new BigDecimal(-dividend._mantissa, dividend._exponent - divisor._exponent, dividend._precision),
                    _ => new BigDecimal(dividend._mantissa, dividend._exponent - divisor._exponent, dividend._precision),
                };

                return true;
            }

            int maxPrecision = (int)Math.Min(dividend.Precision + (long)Math.Ceiling(10.0 * divisor.Precision / 3.0), int.MaxValue);
            int exponentChange = Math.Max(0, maxPrecision - dividend.Precision + divisor.Precision);
            var dividendMantissa = dividend._mantissa * BigIntegerPow10.Get(exponentChange);

            var mantissa = BigInteger.DivRem(dividendMantissa, divisor._mantissa, out var remainder);

            if (remainder.IsZero) {
                result = new BigDecimal(mantissa, dividend._exponent - divisor._exponent - exponentChange);
                return true;
            }

            result = Zero;
            return false;
        }

        /// <summary>
        /// Returns the specified basis raised to the specified exponent. Exponent must be greater than or equal to 0.
        /// </summary>
        public static BigDecimal Pow(BigDecimal basis, int exponent) => exponent switch {
            < 0 => throw new ArgumentOutOfRangeException(nameof(exponent)),
            0 => One,
            1 => basis,
            _ => new BigDecimal(BigInteger.Pow(basis._mantissa, exponent), basis._exponent * exponent),
        };

        /// <summary>
        /// Returns ten (10) raised to the specified exponent.
        /// </summary>
        public static BigDecimal Pow10(int exponent) => exponent == 0 ? One : new BigDecimal(BigInteger.One, exponent, SharedPrecisionHolder.Get(1));

        #endregion

        #region Rounding Functions

        /// <summary>
        /// Discards any fractional digits, effectively rounding towards zero.
        /// </summary>
        public static BigDecimal Truncate(BigDecimal value)
        {
            if (value._exponent >= 0)
                return value;

            return new BigDecimal(value._mantissa / BigIntegerPow10.Get(-value._exponent), 0);
        }

        /// <summary>
        /// Truncates the number to the given precision by removing any extra least significant digits.
        /// </summary>
        public static BigDecimal TruncateToPrecision(BigDecimal value, int precision)
        {
            if (precision < 1)
                throw new ArgumentOutOfRangeException(nameof(precision));

            int extraDigits = value.Precision - precision;

            if (extraDigits <= 0)
                return value;

            return new BigDecimal(value._mantissa / BigIntegerPow10.Get(extraDigits), value._exponent + extraDigits);
        }

        /// <summary>
        /// Rounds down to the nearest integral value.
        /// </summary>
        public static BigDecimal Floor(BigDecimal value)
        {
            var result = Truncate(value);

            if (value._mantissa.Sign < 0 && value != result)
                result -= 1;

            return result;
        }

        /// <summary>
        /// Rounds up to the nearest integral value.
        /// </summary>
        public static BigDecimal Ceiling(BigDecimal value)
        {
            var result = Truncate(value);

            if (value._mantissa.Sign > 0 && value != result)
                result += 1;

            return result;
        }

        /// <summary>
        /// Rounds the value to the nearest integer using the given midpoint rounding mode.
        /// </summary>
        public static BigDecimal Round(BigDecimal value, RoundingMode mode = RoundingMode.MidpointToEven) => Round(value, 0, mode);

        /// <summary>
        /// Rounds the value to the specified number of decimal places using the given midpoint rounding mode.
        /// </summary>
        /// <remarks>
        /// <para>A negative number of decimal places indicates rounding to a whole number digit, i.e. <c>-1</c> for the nearest 10, <c>-2</c> for the nearest 100, etc.</para>
        /// </remarks>
        public static BigDecimal Round(BigDecimal value, int decimals, RoundingMode mode = RoundingMode.MidpointToEven)
        {
            int extraDigits = -value._exponent - decimals;

            if (extraDigits <= 0)
                return value;

            return new BigDecimal(value._mantissa.Divide(BigIntegerPow10.Get(extraDigits), mode), value._exponent + extraDigits);
        }

        /// <summary>
        /// Rounds the value to the specified precision using the given midpoint rounding mode.
        /// </summary>
        public static BigDecimal RoundToPrecision(BigDecimal value, int precision, RoundingMode mode = RoundingMode.MidpointToEven)
        {
            if (precision < 1)
                throw new ArgumentOutOfRangeException(nameof(precision));

            int extraDigits = value.Precision - precision;

            if (extraDigits <= 0)
                return value;

            return new BigDecimal(value._mantissa.Divide(BigIntegerPow10.Get(extraDigits), mode), value._exponent + extraDigits);
        }

        #endregion

        #region String Conversion Methods

        /// <summary>
        /// Converts the string representation of a number to its decimal equivalent.
        /// </summary>
        /// <param name="s">The string representation of the number to convert.</param>
        /// <param name="style">A combination of <see cref="NumberStyles"/> values that indicate the styles that can be parsed.</param>
        /// <param name="formatProvider">A format provider that supplies culture-specific parsing information.</param>
        public static BigDecimal Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Number, IFormatProvider? formatProvider = null)
        {
            if (!TryParse(s, style, formatProvider, out var result))
                throw new FormatException("Input string was not in a correct format.");

            return result;
        }

        /// <summary>
        /// Converts the string representation of a number to its decimal equivalent.
        /// </summary>
        /// <param name="s">The string representation of the number to convert.</param>
        /// <param name="result">The parsed decimal value if parsing was successful, otherwise zero.</param>
        /// <returns><see langword="true"/> if parsing was successful, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<char> s, out BigDecimal result) => TryParse(s, NumberStyles.Number, null, out result);

        /// <summary>
        /// Converts the string representation of a number to its decimal equivalent.
        /// </summary>
        /// <param name="s">The string representation of the number to convert.</param>
        /// <param name="style">A combination of <see cref="NumberStyles"/> values that indicate the styles that can be parsed.</param>
        /// <param name="formatProvider">A format provider that supplies culture-specific parsing information.</param>
        /// <param name="result">The parsed decimal value if parsing was successful, otherwise zero.</param>
        /// <returns><see langword="true"/> if parsing was successful, otherwise <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? formatProvider, out BigDecimal result)
        {
            if (style.HasFlag(NumberStyles.AllowHexSpecifier))
                throw new ArgumentException("Hex number styles are not supported.", nameof(style));

            var formatInfo = NumberFormatInfo.GetInstance(formatProvider);
            const StringComparison cmp = StringComparison.Ordinal;

            bool allowCurrencySymbol = style.HasFlag(NumberStyles.AllowCurrencySymbol);
            bool allowLeadingWhite = style.HasFlag(NumberStyles.AllowLeadingWhite);
            bool allowLeadingSign = style.HasFlag(NumberStyles.AllowLeadingSign);
            bool allowTrailingWhite = style.HasFlag(NumberStyles.AllowTrailingWhite);
            bool allowTrailingSign = style.HasFlag(NumberStyles.AllowTrailingSign);
            bool allowParenthesis = style.HasFlag(NumberStyles.AllowParentheses);
            bool allowExponent = style.HasFlag(NumberStyles.AllowExponent);
            bool allowDecimalPoint = style.HasFlag(NumberStyles.AllowDecimalPoint);
            bool allowThousands = style.HasFlag(NumberStyles.AllowThousands);

            bool currency = false;
            int sign = 0;

            Trim(ref s);

            if (TryParseParenthesis(ref s) &&
                TryParseStart(ref s) &&
                TryParseEnd(ref s) &&
                TryParseExponent(ref s, out int exponent) &&
                TryParseFractional(ref s, out var fractional) &&
                TryParseWhole(s, out var whole) &&
                (fractional.HasValue || whole.HasValue))
            {
                result = fractional.GetValueOrDefault() + whole.GetValueOrDefault();

                if (sign < 0)
                    result = -result;

                if (exponent != 0)
                    result *= Pow10(exponent);

                return true;
            }

            result = Zero;
            return false;

            bool TryParseParenthesis(ref ReadOnlySpan<char> s)
            {
                if (allowParenthesis && s.Length >= 3 && s[0] == '(') {
                    if (s[^1] != ')')
                        return false;

                    sign = -1;
                    s = s[1..^1];
                    Trim(ref s);
                }

                return true;
            }

            bool TryParseStart(ref ReadOnlySpan<char> s)
            {
                while (s.Length > 0 && !char.IsDigit(s[0]) && !s.StartsWith(formatInfo.NumberDecimalSeparator.AsSpan(), cmp)) {
                    if (allowCurrencySymbol && s.StartsWith(formatInfo.CurrencySymbol.AsSpan(), cmp)) {
                        if (currency)
                            return false;

                        currency = true;
                        s = s[formatInfo.CurrencySymbol.Length..];
                    }
                    else if (allowLeadingSign && StartsWithSign(s, out int parsedSign, out int signLength)) {
                        if (sign != 0)
                            return false;

                        sign = parsedSign;
                        s = s[signLength..];
                    }
                    else {
                        return false;
                    }

                    TrimStart(ref s);
                }

                return true;

                bool StartsWithSign(ReadOnlySpan<char> s, out int sign, out int signLength)
                {
                    if (s.StartsWith(formatInfo.PositiveSign.AsSpan(), cmp)) {
                        sign = 1;
                        signLength = formatInfo.PositiveSign.Length;
                        return true;
                    }
                    else if (s.StartsWith(formatInfo.NegativeSign.AsSpan(), cmp)) {
                        sign = -1;
                        signLength = formatInfo.NegativeSign.Length;
                        return true;
                    }

                    sign = 0;
                    signLength = 0;
                    return false;
                }
            }

            bool TryParseEnd(ref ReadOnlySpan<char> s)
            {
                while (s.Length > 0 && !char.IsDigit(s[^1]) && !s.EndsWith(formatInfo.NumberDecimalSeparator.AsSpan(), cmp)) {
                    if (allowCurrencySymbol && s.EndsWith(formatInfo.CurrencySymbol.AsSpan(), cmp)) {
                        if (currency)
                            return false;

                        currency = true;
                        s = s[..^formatInfo.CurrencySymbol.Length];
                    }
                    else if (allowTrailingSign && EndsWithSign(s, out int parsedSign, out int signLength)) {
                        if (sign != 0)
                            return false;

                        sign = parsedSign;
                        s = s[..^signLength];
                    }
                    else {
                        return false;
                    }

                    TrimEnd(ref s);
                }

                return true;

                bool EndsWithSign(ReadOnlySpan<char> s, out int sign, out int signLength)
                {
                    if (s.EndsWith(formatInfo.PositiveSign.AsSpan(), cmp)) {
                        sign = 1;
                        signLength = formatInfo.PositiveSign.Length;
                        return true;
                    }
                    else if (s.EndsWith(formatInfo.NegativeSign.AsSpan(), cmp)) {
                        sign = -1;
                        signLength = formatInfo.NegativeSign.Length;
                        return true;
                    }

                    sign = 0;
                    signLength = 0;
                    return false;
                }
            }

            bool TryParseExponent(ref ReadOnlySpan<char> s, out int result)
            {
                if (allowExponent) {
                    int index = s.LastIndexOfAny('E', 'e');

                    if (index >= 0) {
                        var e = s[(index + 1)..];
                        s = s[..index];
#if NETSTANDARD2_0
                        return int.TryParse(e.ToString(), NumberStyles.AllowLeadingSign, formatProvider, out result);
#else
                        return int.TryParse(e, NumberStyles.AllowLeadingSign, formatProvider, out result);
#endif
                    }
                }

                result = 0;
                return true;
            }

            bool TryParseFractional(ref ReadOnlySpan<char> s, out BigDecimal? result)
            {
                if (!allowDecimalPoint || !SplitFractional(ref s, out var f)) {
                    result = null;
                    return true;
                }

                f = f.TrimEnd('0');

                if (f.Length == 0) {
                    result = Zero;
                    return true;
                }

                int exponent = -f.Length;
                f = f.TrimStart('0');

#if NETSTANDARD2_0
                if (!BigInteger.TryParse(f.ToString(), NumberStyles.None, formatProvider, out var mantissa)) {
#else
                if (!BigInteger.TryParse(f, NumberStyles.None, formatProvider, out var mantissa)) {
#endif
                    result = null;
                    return false;
                }

                result = new BigDecimal(mantissa, exponent, SharedPrecisionHolder.Get(f.Length));
                return true;

                bool SplitFractional(ref ReadOnlySpan<char> s, out ReadOnlySpan<char> f)
                {
                    string decimalSeparator = currency ? formatInfo.CurrencyDecimalSeparator : formatInfo.NumberDecimalSeparator;
                    int decimalIndex = s.IndexOf(decimalSeparator.AsSpan(), cmp);

                    if (decimalIndex >= 0) {
                        f = s[(decimalIndex + decimalSeparator.Length)..];
                        s = s[..decimalIndex];

                        return f.Length > 0;
                    }

                    f = default;
                    return false;
                }
            }

            bool TryParseWhole(ReadOnlySpan<char> s, out BigDecimal? result)
            {
                if (s.Length == 0) {
                    result = null;
                    return true;
                }

                s = s.TrimStart('0');

                if (s.Length == 0) {
                    result = Zero;
                    return true;
                }

                int numDigits = s.Length;
                s = s.TrimEnd('0');
                int exponent = numDigits - s.Length;

                var (wholeStyle, wholeFormatInfo) = GetWholeStyleAndInfo();

#if NETSTANDARD2_0
                if (!BigInteger.TryParse(s.ToString(), wholeStyle, wholeFormatInfo, out var mantissa)) {
#else
                if (!BigInteger.TryParse(s, wholeStyle, wholeFormatInfo, out var mantissa)) {
#endif
                    result = null;
                    return false;
                }

                result = new BigDecimal(mantissa, exponent, SharedPrecisionHolder.Get(s.Length));
                return true;

                (NumberStyles Style, NumberFormatInfo FormatInfo) GetWholeStyleAndInfo()
                {
                    if (allowThousands) {
                        if (currency && formatInfo.CurrencyGroupSeparator != formatInfo.NumberGroupSeparator) {
                            var copy = (NumberFormatInfo)formatInfo.Clone();
                            copy.NumberGroupSeparator = formatInfo.CurrencyGroupSeparator;

                            return (NumberStyles.AllowThousands, copy);
                        }
                        else {
                            return (NumberStyles.AllowThousands, formatInfo);
                        }
                    }

                    return (NumberStyles.None, formatInfo);
                }
            }

            void Trim(ref ReadOnlySpan<char> s)
            {
                TrimStart(ref s);
                TrimEnd(ref s);
            }

            void TrimStart(ref ReadOnlySpan<char> s)
            {
                if (allowLeadingWhite)
                    s = s.TrimStart();
            }

            void TrimEnd(ref ReadOnlySpan<char> s)
            {
                if (allowTrailingWhite)
                    s = s.TrimEnd();
            }
        }

        /// <summary>
        /// Returns an exponential notation string representation of this value.
        /// </summary>
        public override string ToString() => ToString(null);

        /// <summary>
        /// Returns a string representation of this value.
        /// </summary>
        /// <param name="format">The string format to use.</param>
        /// <param name="formatProvider">The format provider that will be used to obtain number format information.</param>
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
        ///     specifier is omitted then the value is written out in full precision in standard decimal form. If a precision specifier is provided then the
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

            if (string.IsNullOrEmpty(format)) {
                formatSpecifier = 'G';
            }
            else {
                formatSpecifier = char.ToUpperInvariant(format[0]);

                if (format.Length > 1) {
#if NETSTANDARD2_0
                    if (int.TryParse(format[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
#else
                    if (int.TryParse(format.AsSpan()[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
#endif
                        precisionSpecifier = ps;
                    else
                        throw new FormatException($"Invalid precision specifier: '{format[1..]}'");
                }
            }

            if (formatSpecifier == 'G') {
                BigDecimal value;

                if (precisionSpecifier == null || precisionSpecifier.GetValueOrDefault() == 0) {
                    value = this;
                }
                else {
                    int precision = precisionSpecifier.GetValueOrDefault();
                    value = RoundToPrecision(this, precision, RoundingMode.MidpointAwayFromZero);

                    if (GetEstimatedFullDecimalLength(value) > GetEstimatedExponentialLength(value)) {
                        int exponentDecimals = Math.Min(value.Precision, precision) - 1;
                        return GetExponentialString(value, exponentDecimals);
                    }
                }

                if (value._exponent >= 0)
                    return GetIntegerString(value, "G");

                return GetDecimalString(value, "G", null);
            }

            if (formatSpecifier == 'F' || formatSpecifier == 'N') {
                string wholePartFormat = formatSpecifier == 'F' ? "F0" : "N0";

                int decimals = precisionSpecifier.HasValue ? precisionSpecifier.GetValueOrDefault() : formatInfo.NumberDecimalDigits;
                var value = Round(this, decimals, RoundingMode.MidpointAwayFromZero);

                if (decimals == 0)
                    return GetIntegerString(value, wholePartFormat);

                return GetDecimalString(value, wholePartFormat, decimals);
            }

            if (formatSpecifier == 'E')
                return GetExponentialString(this, precisionSpecifier);

            if (formatSpecifier == 'C' || formatSpecifier == 'P') {
                BigDecimal value = this;

                if (formatSpecifier == 'P') {
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

                if (decimals == 0)
                    return GetIntegerString(value, "C0");

                return GetDecimalString(value, "C0", decimals);
            }

            if (formatSpecifier == 'R') {
                if (_exponent == 0)
                    return _mantissa.ToString(CultureInfo.InvariantCulture);

                return ((FormattableString)$"{_mantissa}E{_exponent}").ToString(CultureInfo.InvariantCulture);
            }

            throw new FormatException($"Format specifier was invalid: '{formatSpecifier}'.");

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

                if (value._exponent == 0)
                    return result;

                int eIndex = result.LastIndexOf("E", StringComparison.Ordinal);

#if NETSTANDARD2_0
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
                Debug.Assert(decimalPart._exponent == 0, "unexpected transformed decimal part exponent");

                string decimalString = GetIntegerString(decimalPart, "G");

                int insertPoint;

                for (insertPoint = wholeString.Length; insertPoint > 0; insertPoint--) {
                    if (char.IsDigit(wholeString[insertPoint - 1]))
                        break;
                }

                string decimalSeparator = wholePartFormat[0] == 'C' ? formatInfo.CurrencyDecimalSeparator : formatInfo.NumberDecimalSeparator;

                var sb = new StringBuilder(wholeString.Length + decimalSeparator.Length + decimalLeadingZeros + decimalString.Length);
#if NETSTANDARD2_0
                sb.Append(wholeString[..insertPoint]);
#else
                sb.Append(wholeString.AsSpan()[..insertPoint]);
#endif
                sb.Append(decimalSeparator);
                sb.Append('0', decimalLeadingZeros);
                sb.Append(decimalString);
                sb.Append('0', decimalTrailingZeros);
#if NETSTANDARD2_0
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
                _ => throw new NotSupportedException("Unsupported positive percentage pattern."),
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
                _ => throw new NotSupportedException("Unsupported negative percentage pattern."),
            };
        }

#endregion

#region Equality and Comparison Methods

        /// <summary>
        /// Compares this to another <see cref="BigDecimal"/>.
        /// </summary>
        public int CompareTo(BigDecimal other)
        {
            return _exponent > other._exponent ? AlignMantissa(this, other).CompareTo(other._mantissa) : _mantissa.CompareTo(AlignMantissa(other, this));
        }

        /// <summary>
        /// Indicates whether this value and the specified other value are equal.
        /// </summary>
        public bool Equals(BigDecimal other) => other._mantissa.Equals(_mantissa) && other._exponent == _exponent;

        /// <summary>
        /// Indicates whether this value and the specified object are equal.
        /// </summary>
        public override bool Equals(object? obj) => obj is BigDecimal bigDecimal && Equals(bigDecimal);

        /// <summary>
        /// Returns the hash code for this value.
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(_mantissa, _exponent);

#endregion

#region Helper Methods

        // Moved this to a non-local method for docfx. See: https://github.com/dotnet/docfx/issues/7055

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int GetAndCachePrecision()
        {
            Debug.Assert(_precision.Value == 0, "precision is already cached");

            int precision = _mantissa.CountDigits();
            _precision.Value = precision;
            return precision;
        }

        /// <summary>
        /// Returns the mantissa of value, aligned to the reference exponent. Assumes the value exponent is larger than the reference exponent.
        /// </summary>
        private static BigInteger AlignMantissa(BigDecimal value, BigDecimal reference)
        {
            Debug.Assert(value._exponent >= reference._exponent, "value exponent must be greater than or equal to reference exponent");
            return value._mantissa * BigIntegerPow10.Get(value._exponent - reference._exponent);
        }

#endregion
    }
}