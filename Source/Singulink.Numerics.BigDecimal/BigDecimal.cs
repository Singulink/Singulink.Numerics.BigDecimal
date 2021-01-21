using System;
using System.Diagnostics;
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
    /// All operations between <see cref="BigDecimal"/> values are exact except for division. If the result of the division does not fit into the precision of
    /// the dividend or divisor then a maximum precision of <see cref="MaxExtendedDivisionPrecision"/> is used. You should typically specify the maximum
    /// extended precision to use for a each division operation that may result in an arbitrary number of digits by calling the <see cref="Divide(BigDecimal,
    /// BigDecimal, int)"/> method instead of using the division operator.</para>
    /// <para>
    /// Addition and subtraction are fully commutitive and associative for all data types as long as the <c>useExactConversion</c> parameter value does not
    /// change when adding or subtracting converted floating point type values. This makes <see cref="BigDecimal"/> a great data type to store aggregate totals
    /// that can freely add and subtract values without accruing inaccuracies over time.</para>
    /// <para>
    /// Conversions from floating point types <see cref="float"/> and <see cref="double"/> are not exact when casting. This results in a <see
    /// cref="BigDecimal"/> value that matches the output of ToString() on the floating point type as this is probably what is usually expected. The <see
    /// cref="FromDouble(double, bool)"/> method is provided which accepts a parameter indicating whether an exact conversion should be used if control over
    /// this behavior is desired. Exact conversions can result in much larger precision values being produced, i.e. a <see cref="double"/> value of 0.1d
    /// converts to the <see cref="BigDecimal"/> value 0.1000000000000000055511151231257827021181583404541015625 instead of 0.1.</para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>, IFormattable
    {
        #region Static Fields/Properties

        private static readonly BigDecimal Half = 0.5m;
        private static readonly BigDecimal MinusHalf = -0.5m;

        /// <summary>
        /// Gets the default maximum extended precision to use for division operations if the result does not fit into the precision of the dividend or
        /// divisor. The current value is 50 but is subject to change.
        /// </summary>
        /// <remarks>
        /// <para>It is recommended that you specify the maximum extended precision to use for each division operation that could require more precision than
        /// the greater of (dividend precision, divisor precision, this property value) by calling the <see cref="Divide(BigDecimal, BigDecimal, int)"/> method
        /// instead of using the division operator which uses on this shared static value. It is also more performant to use that method with a smaller
        /// precision if 50 significant digits are not required.</para>
        /// </remarks>
        public static int MaxExtendedDivisionPrecision => 50;

        /// <summary>
        /// Gets a value representing zero (0).
        /// </summary>
        public static BigDecimal Zero => default;

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

        private readonly IntHolder? _precisionCache;
        private readonly BigInteger _mantissa;
        private readonly int _exponent;

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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => ToString("G100", CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets a cached precision calculated from the mantissa digit count.
        /// </summary>
        private int Precision {
            get {
                if (_precisionCache == null)
                    return 1;

                if (_precisionCache.Value != 0)
                    return _precisionCache.Value;

                return GetAndCachePrecision(this);

                [MethodImpl(MethodImplOptions.NoInlining)]
                int GetAndCachePrecision(BigDecimal @this)
                {
                    var value = BigInteger.Abs(@this._mantissa);

                    if (value.IsZero || value.IsOne)
                        return 1;

                    // TODO: Possibly reimplement Log10 with more efficient integer based log10.

                    int precision = (int)Math.Ceiling(BigInteger.Log10(value));

                    // We can skip this because the mantissa is always normalized so it does not contain any trailing zeros, but I'm leaving this here so that
                    // there the correct generally applicable implementation is documented:
                    // if (value == BigInteger.Pow(10, digits))
                    //     precision++;

                    @this._precisionCache!.Value = precision;
                    return precision;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BigDecimal"/> struct.
        /// </summary>
        public BigDecimal(BigInteger mantissa, int exponent)
        {
            _mantissa = mantissa;
            _precisionCache = new();

            if (_mantissa.IsZero) {
                _exponent = 0;
            }
            else {
                _exponent = exponent;

                // Normalize trailing zeros in 3 steps to reduce iterations for large numbers with lots of trailing zeros

                if (mantissa >= 1_000_000_000_000_000_000 || mantissa <= -1_000_000_000_000_000_000) {
                    while (true) {
                        var shortened = BigInteger.DivRem(_mantissa, 1_000_000_000_000_000_000, out BigInteger remainder);

                        if (!remainder.IsZero)
                            break;

                        _mantissa = shortened;
                        _exponent += 18;
                    }
                }

                if (mantissa >= 1_000_000 || mantissa <= -1_000_000) {
                    while (true) {
                        var shortened = BigInteger.DivRem(_mantissa, 1_000_000, out BigInteger remainder);

                        if (!remainder.IsZero)
                            break;

                        _mantissa = shortened;
                        _exponent += 6;
                    }
                }

                if (mantissa >= 10 || mantissa <= -10) {
                    while (true) {
                        var shortened = BigInteger.DivRem(_mantissa, 10, out BigInteger remainder);

                        if (!remainder.IsZero)
                            break;

                        _mantissa = shortened;
                        _exponent++;
                    }
                }
            }
        }

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        #region Conversion Operators

        public static implicit operator BigDecimal(BigInteger value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(int value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(uint value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(long value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(ulong value) => new BigDecimal(value, 0);

        public static implicit operator BigDecimal(float value) => FromDouble(value, false);

        public static implicit operator BigDecimal(double value) => FromDouble(value, false);

        public static implicit operator BigDecimal(decimal value)
        {
            var mantissa = (BigInteger)value;
            int exponent = 0;
            decimal scaleFactor = 1;

            while ((decimal)mantissa != value * scaleFactor) {
                exponent--;
                scaleFactor *= 10;
                mantissa = (BigInteger)(value * scaleFactor);
            }

            return new BigDecimal(mantissa, exponent);
        }

        // TODO: Implement more efficient conversions to floating point types
        // Conversions to floating point types in a way that ensures all values within the range of decimal/double/float are correctly converted is rather
        // complex so taking this simple approach for now that uses the built in string parsing.

        public static explicit operator float(BigDecimal value)
        {
            return float.Parse(value.ToString(), NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        public static explicit operator double(BigDecimal value)
        {
            return double.Parse(value.ToString(), NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        public static explicit operator decimal(BigDecimal value)
        {
            return decimal.Parse(value.ToString(), NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        public static explicit operator BigInteger(BigDecimal value)
        {
            return value._exponent < 0 ? value._mantissa / BigInteger.Pow(10, -value._exponent) : value._mantissa * BigInteger.Pow(10, value._exponent);
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

        public static BigDecimal operator -(BigDecimal value) => new BigDecimal(BigInteger.Negate(value._mantissa), value._exponent);

        public static BigDecimal operator ++(BigDecimal value) => value + 1;

        public static BigDecimal operator --(BigDecimal value) => value - 1;

        public static BigDecimal operator +(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent
                ? new BigDecimal(AlignExponent(left, right) + right._mantissa, right._exponent)
                : new BigDecimal(AlignExponent(right, left) + left._mantissa, left._exponent);
        }

        public static BigDecimal operator -(BigDecimal left, BigDecimal right) => left + (-right);

        public static BigDecimal operator *(BigDecimal left, BigDecimal right) => new BigDecimal(left._mantissa * right._mantissa, left._exponent + right._exponent);

        public static BigDecimal operator /(BigDecimal dividend, BigDecimal divisor) => Divide(dividend, divisor, MaxExtendedDivisionPrecision);

        public static BigDecimal operator %(BigDecimal left, BigDecimal right) => left - (right * Floor(left / right));

        public static bool operator ==(BigDecimal left, BigDecimal right) => left._exponent == right._exponent && left._mantissa == right._mantissa;

        public static bool operator !=(BigDecimal left, BigDecimal right) => left._exponent != right._exponent || left._mantissa != right._mantissa;

        public static bool operator <(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignExponent(left, right) < right._mantissa : left._mantissa < AlignExponent(right, left);
        }

        public static bool operator >(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignExponent(left, right) > right._mantissa : left._mantissa > AlignExponent(right, left);
        }

        public static bool operator <=(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignExponent(left, right) <= right._mantissa : left._mantissa <= AlignExponent(right, left);
        }

        public static bool operator >=(BigDecimal left, BigDecimal right)
        {
            return left._exponent > right._exponent ? AlignExponent(left, right) >= right._mantissa : left._mantissa >= AlignExponent(right, left);
        }

        #endregion

        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #region Conversion Methods

        /// <summary>
        /// Converts a <see cref="float"/> into a <see cref="BigDecimal"/> using the given conversion mode.
        /// </summary>
        public static BigDecimal FromSingle(float value, bool useExactConversion) => FromDouble(value, useExactConversion);

        /// <summary>
        /// Converts a <see cref="double"/> into a <see cref="BigDecimal"/> using the given conversion mode.
        /// </summary>
        public static BigDecimal FromDouble(double value, bool useExactConversion)
        {
            return useExactConversion ? FromDoubleExact(value) : FromDoubleApproximate(value);

            static BigDecimal FromDoubleApproximate(double value)
            {
                unchecked {
                    var mantissa = (BigInteger)value;
                    int exponent = 0;
                    double scaleFactor = 1;

                    while ((double)mantissa != value * scaleFactor) {
                        exponent--;
                        scaleFactor *= 10;
                        mantissa = (BigInteger)(value * scaleFactor);
                    }

                    return new BigDecimal(mantissa, exponent);
                }
            }

            static BigDecimal FromDoubleExact(double value)
            {
                unchecked {
                    // Based on Jon Skeet's DoubleConverter:

                    if (double.IsNaN(value))
                        throw new ArgumentException("Floating point value cannot be NaN.", nameof(value));

                    if (double.IsNegativeInfinity(value) || double.IsPositiveInfinity(value))
                        throw new ArgumentOutOfRangeException(nameof(value), "Cannot convert floating point infinity values.");

                    // Translate the double into sign, exponent and mantissa.
                    long bits = BitConverter.DoubleToInt64Bits(value);
                    bool negative = bits < 0;
                    int exponent = (int)((bits >> 52) & 0x7ffL);
                    long mantissa = bits & 0xfffffffffffffL;

                    // Subnormal numbers; exponent is effectively one higher,
                    // but there's no extra normalisation bit in the mantissa
                    // Normal numbers; leave exponent as it is but add extra
                    // bit to the front of the mantissa
                    if (exponent == 0)
                        exponent++;
                    else
                        mantissa |= 1L << 52;

                    // Bias the exponent. It's actually biased by 1023, but we're
                    // treating the mantissa as m.0 rather than 0.m, so we need
                    // to subtract another 52 from it.
                    exponent -= 1075;

                    if (mantissa == 0)
                        return Zero;

                    // Normalize
                    while ((mantissa & 1) == 0) { // mantissa is even
                        mantissa >>= 1;
                        exponent++;
                    }

                    if (negative)
                        mantissa *= -1;

                    var result = new BigDecimal(mantissa, 0);

                    // If the exponent is less than 0, we need to repeatedly
                    // divide by 2, otherwise, we need to repeatedly multiply by 2
                    if (exponent < 0) {
                        for (int i = 0; i < -exponent; i++)
                            result /= 2;
                    }
                    else {
                        for (int i = 0; i < exponent; i++)
                            result *= 2;
                    }

                    return result;
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
        /// Divides two values using the specified maximum extended precision.
        /// </summary>
        /// <param name="dividend">The dividend of the division operation.</param>
        /// <param name="divisor">The divisor of the division operation.</param>
        /// <param name="maxExtendedPrecision">If the result of the division does not fit into the precision of the dividend or divisor then this maximum
        /// precision is used.</param>
        public static BigDecimal Divide(BigDecimal dividend, BigDecimal divisor, int maxExtendedPrecision)
        {
            // Never reduce precision of the result compared to input values but cap precision extensions to maxExtendedPrecision
            int maxPrecision = Math.Max(Math.Max(maxExtendedPrecision, dividend.Precision), divisor.Precision);
            int exponentChange = maxPrecision - (dividend.Precision - divisor.Precision);

            if (exponentChange < 0)
                exponentChange = 0;

            var dividendMantissa = dividend._mantissa * BigInteger.Pow(10, exponentChange);
            return new BigDecimal(dividendMantissa / divisor._mantissa, dividend._exponent - divisor._exponent - exponentChange);
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
                return 1;

            var value = (BigDecimal)1;
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

        #endregion

        #region Truncate Functions

        /// <summary>
        /// Discards any fractional digits, effectively rounding towards zero.
        /// </summary>
        public static BigDecimal Truncate(BigDecimal value)
        {
            if (value._exponent >= 0)
                return value;

            return new BigDecimal(value._mantissa / BigInteger.Pow(10, -value._exponent), 0);
        }

        /// <summary>
        /// Truncates the number to the given precision by removing any extra least significant digits.
        /// </summary>
        public static BigDecimal TruncateToPrecision(BigDecimal value, int precision)
        {
            if (precision < 1)
                throw new ArgumentOutOfRangeException(nameof(precision));

            if (value.Precision <= precision)
                return value;

            int extraDigits = value.Precision - precision;

            return new BigDecimal(value._mantissa / BigInteger.Pow(10, extraDigits), value._exponent + extraDigits);
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

            if (value.Precision <= precision)
                return value;

            int firstTruncatedPosition = value.Precision + value._exponent - precision;

            // Used for shifting values to/from the rounded position.
            // Multiply to go from rounded position => unit position, divide to go from unit position => rounded position.

            BigDecimal shift;

            if (firstTruncatedPosition == 0)
                shift = One;
            else if (firstTruncatedPosition > 0)
                shift = One / BigInteger.Pow(10, firstTruncatedPosition);
            else // firstTruncatedPosition < 0
                shift = BigInteger.Pow(10, -firstTruncatedPosition);

            var result = TruncateToPrecision(value, precision);
            var diff = (value - result) * shift;

            Debug.Assert(!diff.IsZero, "unexpected 0 diff for rounding");

            bool positive = diff.Sign > 0;
            int diffCompareResult = positive ? diff.CompareTo(Half) : diff.CompareTo(MinusHalf);

            if (diffCompareResult < 0)
                return positive ? result : AddToRoundedResultPosition(MinusOne);

            if (diffCompareResult > 0)
                return positive ? AddToRoundedResultPosition(One) : result;

            // diff is exactly +/-0.5:

            switch (mode) {
                case MidpointRounding.AwayFromZero:
                    return positive ? AddToRoundedResultPosition(One) : AddToRoundedResultPosition(MinusOne);
                case MidpointRounding.ToEven:
                    if (firstTruncatedPosition < result._exponent || result._mantissa.IsEven)
                        return result;

                    return positive ? AddToRoundedResultPosition(One) : AddToRoundedResultPosition(MinusOne);
            }

            throw new ArgumentException($"Unsupported rounding mode '{mode}'.", nameof(mode));

            BigDecimal AddToRoundedResultPosition(BigDecimal val)
            {
                var shifted = val / shift;
                var added = result + shifted;
                Console.WriteLine(added);
                return added;
            }
        }

        #endregion

        #region Standard Object Methods

        /// <summary>
        /// Compares this to another <see cref="BigDecimal"/>.
        /// </summary>
        public int CompareTo(BigDecimal other)
        {
            return _exponent > other._exponent ? AlignExponent(this, other).CompareTo(other._mantissa) : _mantissa.CompareTo(AlignExponent(other, this));
        }

        /// <summary>
        /// Indicates whether this value and the specified other value are equal.
        /// </summary>
        public bool Equals(BigDecimal other) => other._mantissa.Equals(_mantissa) && other._exponent == _exponent;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is BigDecimal bigDecimal && Equals(bigDecimal);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(_mantissa, _exponent);

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
        ///   <item><term>G</term><description>General. Precision specifier determines the number of significant digits. If the precision specifier is omitted
        ///   then the value is written out in full precision in standard decimal form. If a precision specifier is provided then the more compact of either
        ///   fixed-point or scientific notation is used.</description></item>
        ///   <item><term>E</term><description>Exponential (scientific) notation. Precision specifier determines the number of decimal places.</description></item>
        /// </list>
        /// </remarks>
        public string ToString(string? format, IFormatProvider? formatProvider = null)
        {
            format = format?.Trim().ToUpperInvariant();

            char formatSpecifier;
            int? precisionSpecifier = null;

            if (string.IsNullOrEmpty(format)) {
                formatSpecifier = 'G';
            }
            else {
                formatSpecifier = format[0];

                if (format.Length > 1) {
                    if (int.TryParse(format.AsSpan()[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps) && ps > 0)
                        precisionSpecifier = ps;
                    else
                        throw new FormatException($"Invalid precision specifier: '{format[1..]}'");
                }
            }

            if (formatSpecifier == 'E') {
                return GetExponentialString(this, precisionSpecifier, formatProvider);
            }
            else if (formatSpecifier == 'G') {
                BigDecimal value;

                if (precisionSpecifier == null) {
                    value = this;
                }
                else {
                    value = RoundToPrecision(this, precisionSpecifier.GetValueOrDefault());

                    if (GetEstimatedFullDecimalLength(value) > GetEstimatedExponentialLength(value)) {
                        int exponentDigits = Math.Min(value.Precision, precisionSpecifier.GetValueOrDefault()) - 1;
                        return GetExponentialString(value, exponentDigits, formatProvider);
                    }
                }

                if (value._exponent >= 0)
                    return GetNonDecimalString(value, "G", formatProvider);

                return GetDecimalString(value, "G", formatProvider);
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

                // TODO: replace hacky way to get formatted -0?

                if (wholePart.IsZero && value.Sign < 0)
                    wholePartString = (-1).ToString(wholePartFormat, formatProvider).Replace('1', '0');
                else
                    wholePartString = GetNonDecimalString(wholePart, wholePartFormat, formatProvider);

                string decimalPartString = GetNonDecimalString(decimalPart, "G", formatProvider);

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

            static string GetNonDecimalString(BigDecimal value, string format, IFormatProvider? formatProvider)
            {
                Debug.Assert(value._exponent >= 0, "value contains decimal digits");
                return value._mantissa.ToString(format, formatProvider) + new string('0', value._exponent);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Returns the mantissa of value, aligned to the reference exponent. Assumes the value exponent is larger than the reference exponent.
        /// </summary>
        private static BigInteger AlignExponent(BigDecimal value, BigDecimal reference)
        {
            Debug.Assert(value._exponent >= reference._exponent, "value exponent must be greater than or equal to reference exponent");
            return value._mantissa * BigInteger.Pow(10, value._exponent - reference._exponent);
        }

        #endregion

        #region Nested types

        private class IntHolder
        {
            public int Value { get; set; }
        }

        #endregion
    }
}