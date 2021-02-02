﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Singulink.Numerics
{
    /// <summary>
    /// Represents an arbitrary precision decimal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All operations between <see cref="BigDecimal"/> values are exact except division in the case of a repeating decimal result. If the result of the
    /// division cannot be exactly represented in decimal form then the largest of the dividend precision, divisor precision or <see
    /// cref="MaxExtendedDivisionPrecision"/> is used to represent the result. You can specify the maximum extended precision to use for each division
    /// operation by calling the <see cref="Divide(BigDecimal, BigDecimal, int, MidpointRounding?)"/> method or use the <see cref="DivideExact(BigDecimal,
    /// BigDecimal)"/> / <see cref="TryDivideExact(BigDecimal, BigDecimal, out BigDecimal)"/> methods for division operations that are expected to return exact
    /// results. The division operator first attempts to do an exact result division and falls back to the extended precision division method.</para>
    /// <para>
    /// Addition and subtraction are fully commutitive and associative for all data types as long as the <c>exactConversion</c> parameter value does not change
    /// when adding or subtracting converted floating point type values. This makes <see cref="BigDecimal"/> a great data type to store aggregate totals that
    /// can freely add and subtract values without accruing inaccuracies over time.</para>
    /// <para>
    /// Conversions from floating point types <see cref="float"/> and <see cref="double"/> are not "exact" when casting. This results in a <see
    /// cref="BigDecimal"/> value that matches the output of ToString() on the floating point type as this is probably what is usually expected. The <see
    /// cref="FromDouble(double, bool)"/> method is provided which accepts a parameter indicating whether an exact conversion should be used if control over
    /// this behavior is desired. Exact conversions can result in much larger precision values being produced, i.e. a <see cref="double"/> value of 0.1d
    /// converts to the <see cref="BigDecimal"/> value 0.1000000000000000055511151231257827021181583404541015625 instead of 0.1.</para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>, IFormattable
    {
        #region Static Contants/Fields/Properties

        private const string ToDecimalOrFloatFormat = "R";
        private const NumberStyles ToDecimalOrFloatStyle = NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        private const string FromFloatFormat = "R";
        private const NumberStyles FromFloatStyle = NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        // private const string FromDecimalFormat = "G0";
        // private const NumberStyles FromDecimalStyle = NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;

        // private static readonly BigInteger BigInt2 = 2;
        private static readonly BigInteger BigInt5 = 5;

        /// <summary>
        /// Gets the maximum extended precision used by the division operator if the result is not exact (i.e. has repeating decimals). If the dividend or
        /// divisor precision is greater than this value then that is used instead. The current value is 50 but is subject to change.
        /// </summary>
        /// <remarks>
        /// <para>For better control over the result of each division operation see the <see cref="Divide(BigDecimal, BigDecimal, int, MidpointRounding?)"/>,
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
        /// Gets the precision of this value, i.e. the total number of digits it contains.
        /// </summary>
        public int Precision {
            get {
                if (_precision == null)
                    return 1;

                if (_precision.Value != 0)
                    return _precision.Value;

                return GetAndCachePrecision(this);

                [MethodImpl(MethodImplOptions.NoInlining)]
                static int GetAndCachePrecision(BigDecimal @this)
                {
                    Debug.Assert(@this._precision != null && @this._precision.Value == 0, "precision is already cached");

                    int precision = CountDigits(@this._mantissa);
                    @this._precision.Value = precision;
                    return precision;
                }
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
                _precision = SharedPrecisionHolder.One;
            }
            else {
                // ToString() method bencharked faster than repeated DivRem(10), even with an exponentially increasing divisor to reduce iterations.
                // Bonus that we get to determine precision up front at no cost.

                string ms = BigInteger.Abs(mantissa).ToString(CultureInfo.InvariantCulture);
                int precision = ms.Length;
                int shift = 0;

                for (int i = ms.Length - 1; i > 0 && ms[i] == '0'; i--)
                    shift++;

                if (shift > 0) {
                    mantissa /= BigIntPow10.Get(shift);
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

        public static implicit operator BigDecimal(float value) => FromDouble(value, false);

        public static implicit operator BigDecimal(double value) => FromDouble(value, false);

        public static implicit operator BigDecimal(decimal value)
        {
            // return Parse(value.ToString(FromDecimalFormat, CultureInfo.InvariantCulture), FromDecimalStyle, CultureInfo.InvariantCulture);

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
            return float.Parse(value.ToString(ToDecimalOrFloatFormat, CultureInfo.InvariantCulture), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
        }

        public static explicit operator double(BigDecimal value)
        {
            return double.Parse(value.ToString(ToDecimalOrFloatFormat, CultureInfo.InvariantCulture), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
        }

        public static explicit operator decimal(BigDecimal value)
        {
            return decimal.Parse(value.ToString(ToDecimalOrFloatFormat, CultureInfo.InvariantCulture), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
        }

        public static explicit operator BigInteger(BigDecimal value)
        {
            return value._exponent < 0 ? value._mantissa / BigIntPow10.Get(-value._exponent) : value._mantissa * BigIntPow10.Get(value._exponent);
        }

        /// <summary>
        /// Converts a <see cref="BigDecimal"/> into an <see cref="int"/>.
        /// </summary>
        public static explicit operator int(BigDecimal value) => (int)(BigInteger)value;

        /// <summary>
        /// Converts a <see cref="BigDecimal"/> into a <see cref="uint"/>.
        /// </summary>
        public static explicit operator uint(BigDecimal value) => (uint)(BigInteger)value;

        /// <summary>
        /// Converts a <see cref="BigDecimal"/> into a <see cref="long"/>.
        /// </summary>
        public static explicit operator long(BigDecimal value) => (long)(BigInteger)value;

        /// <summary>
        /// Converts a <see cref="BigDecimal"/> into a <see cref="ulong"/>.
        /// </summary>
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
        /// Converts a <see cref="float"/> into a <see cref="BigDecimal"/> using the given conversion mode.
        /// </summary>
        public static BigDecimal FromSingle(float value, bool exactConversion) => FromDouble(value, exactConversion);

        /// <summary>
        /// Converts a <see cref="double"/> into a <see cref="BigDecimal"/> using the given conversion mode.
        /// </summary>
        public static BigDecimal FromDouble(double value, bool exactConversion)
        {
            if (double.IsNaN(value))
                throw new ArgumentException("Floating point value cannot be NaN.", nameof(value));

            if (double.IsNegativeInfinity(value) || double.IsPositiveInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot convert floating point infinity values.");

            if (!exactConversion)
                return Parse(value.ToString(FromFloatFormat, CultureInfo.InvariantCulture), FromFloatStyle, CultureInfo.InvariantCulture);

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

                if (exponent == 0) {
                    return new BigDecimal(resultMantissa, 0, new SharedPrecisionHolder());
                }
                else if (exponent < 0) {
                    resultMantissa *= BigInteger.Pow(BigInt5, -exponent);
                    return new BigDecimal(resultMantissa, exponent, new SharedPrecisionHolder());
                }
                else { // exponent > 0
                    resultMantissa <<= exponent; // *= BigInteger.Pow(BigInt2, exponent);
                    return new BigDecimal(resultMantissa, 0);
                }
            }
        }

        #endregion

        #region Mathematical Functions

        /// <summary>
        /// Gets the absolute value of the given value.
        /// </summary>
        public static BigDecimal Abs(BigDecimal value) => value._mantissa.Sign >= 0 ? value : -value;

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
        /// Performs a division operation using the specified maximum extended precision.
        /// </summary>
        /// <param name="dividend">The dividend of the division operation.</param>
        /// <param name="divisor">The divisor of the division operation.</param>
        /// <param name="maxExtendedPrecision">If the result of the division does not fit into the precision of the dividend or divisor then this extended
        /// precision is used.</param>
        /// <param name="mode">The rounding mode to use or <see langword="null"/> to truncate the result.</param>
        public static BigDecimal Divide(BigDecimal dividend, BigDecimal divisor, int maxExtendedPrecision, MidpointRounding? mode = MidpointRounding.ToEven)
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
            var dividendMantissa = dividend._mantissa * BigIntPow10.Get(exponentChange);
            int exponent = dividend._exponent - divisor._exponent - exponentChange;

            if (mode == null) // truncate
                return new BigDecimal(dividendMantissa / divisor._mantissa, exponent);

            return new BigDecimal(DivideAndRound(dividendMantissa, divisor._mantissa, mode.GetValueOrDefault()), exponent);
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
            var dividendMantissa = dividend._mantissa * BigIntPow10.Get(exponentChange);

            var mantissa = BigInteger.DivRem(dividendMantissa, divisor._mantissa, out var remainder);

            if (remainder.IsZero) {
                result = new BigDecimal(mantissa, dividend._exponent - divisor._exponent - exponentChange);
                return true;
            }

            result = Zero;
            return false;
        }

        /// <summary>
        /// Returns <c>e</c> raised to the specified exponent.
        /// </summary>
        public static BigDecimal Exp(double exponent, bool useExactFloatConversion = false) => Pow(Math.E, exponent, useExactFloatConversion);

        /// <summary>
        /// Returns the specified basis raised to the specified exponent.
        /// </summary>
        public static BigDecimal Pow(double basis, double exponent, bool useExactFloatConversion = false)
        {
            if (exponent == 0)
                return One;

            var value = One;
            double exponentStep = exponent > 0 ? Math.Min(100, exponent) : Math.Max(-100, exponent);

            // Get largest exponent step that results in a multiplier that fits into a double
            // 307 = approx +/- exponent range for double

            while (GetNumBase10Digits(basis, exponentStep) > 307)
                exponentStep /= 10;

            var multiplier = FromDouble(Math.Pow(basis, exponentStep), useExactFloatConversion);

            if (Math.Abs(exponent) > Math.Abs(exponentStep)) {
                do {
                    value *= multiplier;
                    exponent -= exponentStep;
                }
                while (Math.Abs(exponent) > Math.Abs(exponentStep));

                multiplier = FromDouble(Math.Pow(basis, exponent), useExactFloatConversion);
            }

            value *= multiplier;
            return value;

            static int GetNumBase10Digits(double basis, double exponent) => (int)Math.Ceiling(Math.Abs(exponent / Math.Log(10, basis)));
        }

        /// <summary>
        /// Returns ten (10) raised to the specified exponent.
        /// </summary>
        public static BigDecimal Pow10(int exponent) => exponent == 0 ? One : new BigDecimal(BigInteger.One, exponent, SharedPrecisionHolder.One);

        #endregion

        #region Truncate Functions

        /// <summary>
        /// Discards any fractional digits, effectively rounding towards zero.
        /// </summary>
        public static BigDecimal Truncate(BigDecimal value)
        {
            if (value._exponent >= 0)
                return value;

            return new BigDecimal(value._mantissa / BigIntPow10.Get(-value._exponent), 0);
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

            return new BigDecimal(value._mantissa / BigIntPow10.Get(extraDigits), value._exponent + extraDigits);
        }

        #endregion

        #region Round Functions

        /// <summary>
        /// Rounds the value to the nearest integer using the <see cref="MidpointRounding.ToEven"/> midpoint rounding mode.
        /// </summary>
        public static BigDecimal Round(BigDecimal value) => Round(value, 0);

        /// <summary>
        /// Rounds the value to the specified number of decimal places using the <see cref="MidpointRounding.ToEven"/> midpoint rounding mode.
        /// </summary>
        public static BigDecimal Round(BigDecimal value, int decimals) => Round(value, decimals, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the nearest integer using the given midpoint rounding mode.
        /// </summary>
        public static BigDecimal Round(BigDecimal value, MidpointRounding mode) => Round(value, 0, mode);

        /// <summary>
        /// Rounds the value to the specified number of decimal places using the given midpoint rounding mode.
        /// </summary>
        public static BigDecimal Round(BigDecimal value, int decimals, MidpointRounding mode)
        {
            if (decimals < 0)
                throw new ArgumentOutOfRangeException(nameof(decimals));

            int removeDecimals = -value._exponent - decimals;

            if (removeDecimals <= 0)
                return value;

            int precision = value.Precision - removeDecimals;

            return RoundToPrecision(value, precision, mode);
        }

        /// <summary>
        /// Rounds the value to the specified precision using the <see cref="MidpointRounding.ToEven"/> midpoint rounding mode.
        /// </summary>
        public static BigDecimal RoundToPrecision(BigDecimal value, int precision) => RoundToPrecision(value, precision, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the specified precision using the given midpoint rounding mode.
        /// </summary>
        public static BigDecimal RoundToPrecision(BigDecimal value, int precision, MidpointRounding mode)
        {
            if (precision < 1)
                throw new ArgumentOutOfRangeException(nameof(precision));

            int extraDigits = value.Precision - precision;

            if (extraDigits <= 0)
                return value;

            return new BigDecimal(DivideAndRound(value._mantissa, BigIntPow10.Get(extraDigits), mode), value._exponent + extraDigits);
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
                if (style.HasFlag(NumberStyles.AllowParentheses) && s.Length >= 3 && s[0] == '(') {
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
                while (s.Length > 0 && !char.IsDigit(s[0]) && !s.StartsWith(formatInfo.NumberDecimalSeparator, cmp)) {
                    if (allowCurrencySymbol && s.StartsWith(formatInfo.CurrencySymbol, cmp)) {
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
                    if (s.StartsWith(formatInfo.PositiveSign, cmp)) {
                        sign = 1;
                        signLength = formatInfo.PositiveSign.Length;
                        return true;
                    }
                    else if (s.StartsWith(formatInfo.NegativeSign, cmp)) {
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
                while (s.Length > 0 && !char.IsDigit(s[^1]) && !s.EndsWith(formatInfo.NumberDecimalSeparator, cmp)) {
                    if (allowCurrencySymbol && s.EndsWith(formatInfo.CurrencySymbol, cmp)) {
                        if (currency)
                            return false;

                        currency = true;
                        s = s[..^formatInfo.CurrencySymbol.Length];
                    }
                    else if (allowLeadingSign && EndsWithSign(s, out int parsedSign, out int signLength)) {
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
                    if (s.EndsWith(formatInfo.PositiveSign, cmp)) {
                        sign = 1;
                        signLength = formatInfo.PositiveSign.Length;
                        return true;
                    }
                    else if (s.EndsWith(formatInfo.NegativeSign, cmp)) {
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
                if (style.HasFlag(NumberStyles.AllowExponent)) {
                    int index = s.LastIndexOfAny('E', 'e');

                    if (index >= 0) {
                        var e = s[(index + 1)..];
                        s = s[..index];
                        return int.TryParse(e, NumberStyles.AllowLeadingSign, formatProvider, out result);
                    }
                }

                result = 0;
                return true;
            }

            bool TryParseFractional(ref ReadOnlySpan<char> s, out BigDecimal? result)
            {
                if (!style.HasFlag(NumberStyles.AllowDecimalPoint) || !SplitFractional(ref s, out var f)) {
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

                if (!BigInteger.TryParse(f, NumberStyles.None, formatProvider, out var mantissa)) {
                    result = null;
                    return false;
                }

                result = new BigDecimal(mantissa, exponent, SharedPrecisionHolder.Get(f.Length));
                return true;

                bool SplitFractional(ref ReadOnlySpan<char> s, out ReadOnlySpan<char> f)
                {
                    string decimalSeparator = currency ? formatInfo.CurrencyDecimalSeparator : formatInfo.NumberDecimalSeparator;
                    int decimalIndex = s.IndexOf(decimalSeparator, cmp);

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

                (var wholeStyle, var wholeFormatInfo) = GetInfo();

                if (!BigInteger.TryParse(s, wholeStyle, wholeFormatInfo, out var mantissa)) {
                    result = null;
                    return false;
                }

                result = new BigDecimal(mantissa, exponent, SharedPrecisionHolder.Get(s.Length));
                return true;

                (NumberStyles Style, NumberFormatInfo FormatInfo) GetInfo()
                {
                    if (style.HasFlag(NumberStyles.AllowThousands)) {
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
        /// <list type="bullet">
        ///   <item><term>G</term><description>General (default). Precision specifier determines the number of significant digits. If the precision specifier
        ///   is omitted then the value is written out in full precision in standard decimal form. If a precision specifier is provided then the more compact
        ///   of either decimal form or scientific notation is used.</description></item>
        ///   <item><term>E</term><description>Exponential (scientific) notation. Precision specifier determines the number of decimal
        ///   places.</description></item>
        ///   <item><term>C</term><description>Currency. Precision specifier determines the number of decimal places.</description></item>
        ///   <item><term>R</term><description>Round-trip. Writes out the mantissa followed by <c>E</c> and then the exponent.</description></item>
        /// </list>
        /// </remarks>
        public string ToString(string? format, IFormatProvider? formatProvider = null)
        {
            format = format?.Trim();

            char formatSpecifier;
            int? precisionSpecifier = null;

            if (string.IsNullOrEmpty(format)) {
                formatSpecifier = 'G';
            }
            else {
                formatSpecifier = char.ToUpperInvariant(format[0]);

                if (format.Length > 1) {
                    if (int.TryParse(format.AsSpan()[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
                        precisionSpecifier = ps;
                    else
                        throw new FormatException($"Invalid precision specifier: '{format[1..]}'");
                }
            }

            if (formatSpecifier == 'R') {
                if (_exponent == 0)
                    return _mantissa.ToString(formatProvider);

                return ((FormattableString)$"{_mantissa}E{_exponent}").ToString(formatProvider);
            }

            if (formatSpecifier == 'E')
                return GetExponentialString(this, precisionSpecifier, formatProvider);

            if (formatSpecifier == 'G') {
                BigDecimal value;

                if (precisionSpecifier == null || precisionSpecifier.Value == 0) {
                    value = this;
                }
                else {
                    int precision = precisionSpecifier.GetValueOrDefault();
                    value = RoundToPrecision(this, precision, MidpointRounding.AwayFromZero);

                    if (GetEstimatedFullDecimalLength(value) > GetEstimatedExponentialLength(value)) {
                        int exponentDecimals = Math.Min(value.Precision, precision) - 1;
                        return GetExponentialString(value, exponentDecimals, formatProvider);
                    }
                }

                if (value._exponent >= 0)
                    return GetIntegerString(value, "G", formatProvider);

                return GetDecimalString(value, "G", formatProvider);
            }

            if (formatSpecifier == 'C') {
                BigDecimal value;

                if (precisionSpecifier == null) {
                    value = this;
                }
                else {
                    int precision = precisionSpecifier.GetValueOrDefault();
                    value = Round(this, precision, MidpointRounding.AwayFromZero);
                }

                return GetDecimalString(value, "C", formatProvider);
            }

            throw new FormatException($"Unknown format specifier '{formatSpecifier}'");

            static int GetEstimatedFullDecimalLength(BigDecimal value)
            {
                if (value._exponent >= 0)
                    return value.Precision + value._exponent;

                return value.Precision + Math.Max(0, -value._exponent - value.Precision) + 1; // digits + additional leading zeros + decimal separator
            }

            static int GetEstimatedExponentialLength(BigDecimal value) => value.Precision + 5; // .E+99

            static string GetExponentialString(BigDecimal value, int? precisionSpecifier, IFormatProvider? formatProvider)
            {
                string result = value._mantissa.ToString("E" + precisionSpecifier, formatProvider);
                int eIndex = result.LastIndexOf("E", StringComparison.Ordinal);

                int exponent = int.Parse(result[(eIndex + 1)..], formatProvider) + value._exponent;

                return $"{result[..(eIndex + 1)]}{(exponent > 0 ? "+" : string.Empty)}{exponent}";
            }

            static string GetDecimalString(BigDecimal value, string wholePartFormat, IFormatProvider? formatProvider)
            {
                var formatInfo = NumberFormatInfo.GetInstance(formatProvider);

                var wholePart = Truncate(value);

                var decimalPart = Abs(value - wholePart);
                int decimalPartShift = -decimalPart._exponent;
                int decimalPartLeadingZeros = decimalPartShift - decimalPart.Precision;
                decimalPart = decimalPart._mantissa;

                Debug.Assert(decimalPart._exponent == 0, "unexpected transformed decimal part exponent");

                string wholePartString;

                if (wholePart.IsZero && value.Sign < 0)
                    wholePartString = (-1).ToString(wholePartFormat, formatProvider).Replace('1', '0');
                else
                    wholePartString = GetIntegerString(wholePart, wholePartFormat, formatProvider);

                string decimalPartString = GetIntegerString(decimalPart, "G", formatProvider);

                int insertPoint;

                for (insertPoint = wholePartString.Length; insertPoint > 0; insertPoint--) {
                    if (char.IsDigit(wholePartString[insertPoint - 1]))
                        break;
                }

                var sb = new StringBuilder(wholePartString.Length + formatInfo.NumberDecimalSeparator.Length + decimalPartLeadingZeros + decimalPartString.Length);
                sb.Append(wholePartString.AsSpan()[..insertPoint]);
                sb.Append(formatInfo.NumberDecimalSeparator);
                sb.Append('0', decimalPartLeadingZeros);
                sb.Append(decimalPartString);
                sb.Append(wholePartString.AsSpan()[insertPoint..]);

                return sb.ToString();
            }

            static string GetIntegerString(BigDecimal value, string format, IFormatProvider? formatProvider)
            {
                Debug.Assert(value._exponent >= 0, "value contains decimal digits");
                BigInteger intValue = value._mantissa;

                if (value._exponent > 0)
                    intValue *= BigIntPow10.Get(value._exponent);

                return intValue.ToString(format, formatProvider);
            }
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

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is BigDecimal bigDecimal && Equals(bigDecimal);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(_mantissa, _exponent);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Returns the mantissa of value, aligned to the reference exponent. Assumes the value exponent is larger than the reference exponent.
        /// </summary>
        private static BigInteger AlignMantissa(BigDecimal value, BigDecimal reference)
        {
            Debug.Assert(value._exponent >= reference._exponent, "value exponent must be greater than or equal to reference exponent");
            return value._mantissa * BigIntPow10.Get(value._exponent - reference._exponent);
        }

        private static BigInteger DivideAndRound(BigInteger dividend, BigInteger divisor, MidpointRounding mode)
        {
            var result = BigInteger.DivRem(dividend, divisor, out var remainder);

            if (!remainder.IsZero) {
                int compareResult = (BigInteger.Abs(remainder) << 1).CompareTo(BigInteger.Abs(divisor));
                int sign = dividend.Sign * divisor.Sign;

                if (compareResult > 0) {
                    return RoundAwayFromZero(result, sign);
                }
                else if (compareResult == 0) {
                    switch (mode) {
                        case MidpointRounding.AwayFromZero:
                            return RoundAwayFromZero(result, sign);
                        case MidpointRounding.ToEven:
                            if (!result.IsEven)
                                return RoundAwayFromZero(result, sign);

                            return result;
                        default:
                            throw new ArgumentException($"Unsupported rounding mode '{mode}'.", nameof(mode));
                    }
                }
            }

            return result;

            static BigInteger RoundAwayFromZero(BigInteger value, int sign) => sign > 0 ? value + BigInteger.One : value - BigInteger.One;
        }

        private static int CountDigits(BigInteger value)
        {
            value = BigInteger.Abs(value);

            if (value.IsZero || value.IsOne)
                return 1;

            int exp = (int)Math.Ceiling(BigInteger.Log10(value));
            var test = BigIntPow10.Get(exp);

            return value >= test ? exp + 1 : exp;
        }

        #endregion
    }
}