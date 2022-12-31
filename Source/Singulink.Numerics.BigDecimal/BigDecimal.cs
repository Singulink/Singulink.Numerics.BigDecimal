using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <summary>
/// Represents an arbitrary precision decimal.
/// </summary>
/// <remarks>
/// <para>
/// All operations on <see cref="BigDecimal"/> values are exact except division in the case of a repeating decimal result. If the result of the division
/// cannot be exactly represented in decimal form then the largest of the dividend precision, divisor precision and the specified maximum extended precision
/// is used to represent the result. You can specify the maximum extended precision to use for each division operation by calling the <see
/// cref="Divide(BigDecimal, BigDecimal, int, RoundingMode)"/> method or use the <see cref="DivideExact(BigDecimal, BigDecimal)"/> / <see
/// cref="TryDivideExact(BigDecimal, BigDecimal, out BigDecimal)"/> methods for division operations that are expected to return exact results. The standard
/// division operator (<c>/</c>) first attempts to do an exact division and falls back to extended precision division using <see
/// cref="MaxExtendedDivisionPrecision"/> as the maximum extended precision parameter.</para>
/// <para>
/// Addition and subtraction are fully commutitive and associative for all converted data types. This makes <see cref="BigDecimal"/> a great data type to
/// store aggregate totals that can freely add and subtract values without accruing inaccuracies over time.</para>
/// <para>
/// Conversions from floating-point types (<see cref="float"/>/<see cref="double"/>) default to <see cref="FloatConversion.Truncate"/> mode in order to
/// match the behavior of floating point to <see cref="decimal"/> conversions, but there are several conversion modes available that are each suitable in
/// different situations. You can use the <see cref="FromSingle(float, FloatConversion)"/> or <see cref="FromDouble(double, FloatConversion)"/> methods to
/// specify a different conversion mode.</para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>, IComparable, IFormattable
#if NET7_0_OR_GREATER
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IFloatingPoint<BigDecimal>
#pragma warning restore SA1001
#endif
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

    private readonly BigInteger _mantissa;
    private readonly int _exponent;
    private readonly int _precision;

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
    public int Precision => _precision == 0 ? 1 : _precision;

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
        if (mantissa.IsZero)
        {
            _mantissa = mantissa;
            _exponent = 0;
            _precision = 0;

            return;
        }

        _mantissa = mantissa;
        _exponent = exponent;

        int trailingZeros;
        (_precision, trailingZeros) = mantissa.CountDigitsAndTrailingZeros();

        if (trailingZeros > 0)
        {
            _mantissa /= BigIntegerPow10.Get(trailingZeros);
            _exponent += trailingZeros;
            _precision -= trailingZeros;
        }
    }

    // Trusted private constructor

    private BigDecimal(BigInteger mantissa, int exponent, int precision)
    {
        _mantissa = mantissa;
        _exponent = exponent;
        _precision = precision;
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    #region Conversions to BigDecimal

    public static implicit operator BigDecimal(BigInteger value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(byte value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(sbyte value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(short value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(ushort value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(int value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(uint value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(long value) => new BigDecimal(value, 0);

    public static implicit operator BigDecimal(ulong value) => new BigDecimal(value, 0);

    public static explicit operator BigDecimal(float value) => FromSingle(value);

    public static explicit operator BigDecimal(double value) => FromDouble(value);

    public static implicit operator BigDecimal(decimal value)
    {
        ref var decimalData = ref Unsafe.As<decimal, DecimalData>(ref value);

        var mantissa = (new BigInteger(decimalData.Hi) << 64) | decimalData.Lo;

        if (!decimalData.IsPositive)
            mantissa = -mantissa;

        return new BigDecimal(mantissa, -decimalData.Scale);
    }

    #endregion

    #region Conversions from BigDecimal

    public static explicit operator BigInteger(BigDecimal value)
    {
        return value._exponent < 0 ? value._mantissa / BigIntegerPow10.Get(-value._exponent) : value._mantissa * BigIntegerPow10.Get(value._exponent);
    }

    public static explicit operator byte(BigDecimal value) => (byte)(BigInteger)value;

    public static explicit operator sbyte(BigDecimal value) => (sbyte)(BigInteger)value;

    public static explicit operator short(BigDecimal value) => (short)(BigInteger)value;

    public static explicit operator ushort(BigDecimal value) => (ushort)(BigInteger)value;

    public static explicit operator int(BigDecimal value) => (int)(BigInteger)value;

    public static explicit operator uint(BigDecimal value) => (uint)(BigInteger)value;

    public static explicit operator long(BigDecimal value) => (long)(BigInteger)value;

    public static explicit operator ulong(BigDecimal value) => (ulong)(BigInteger)value;

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

    #endregion

    #region Mathematical Operators

    public static BigDecimal operator +(BigDecimal value) => value;

    public static BigDecimal operator -(BigDecimal value) => new(BigInteger.Negate(value._mantissa), value._exponent, value._precision);

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

#if NET7_0_OR_GREATER

    /// <inheritdoc cref="INumberBase{TSelf}.CreateChecked{TOther}(TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigDecimal CreateChecked<TOther>(TOther value)
        where TOther : INumberBase<TOther>
    {
        BigDecimal result;

        if (typeof(TOther) == typeof(BigDecimal))
            result = (BigDecimal)(object)value;
        else if (!TryConvertFromChecked(value, out result) && !TOther.TryConvertToChecked(value, out result))
            Throw.NotSupportedEx();

        return result;
    }

    /// <inheritdoc cref="INumberBase{TSelf}.CreateSaturating{TOther}(TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigDecimal CreateSaturating<TOther>(TOther value)
        where TOther : INumberBase<TOther>
    {
        BigDecimal result;

        if (typeof(TOther) == typeof(BigDecimal))
            result = (BigDecimal)(object)value;
        else if (!TryConvertFrom(value, out result) && !TOther.TryConvertToSaturating(value, out result))
            Throw.NotSupportedEx();

        return result;
    }

    /// <inheritdoc cref="INumberBase{TSelf}.CreateTruncating{TOther}(TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigDecimal CreateTruncating<TOther>(TOther value)
        where TOther : INumberBase<TOther>
    {
        BigDecimal result;

        if (typeof(TOther) == typeof(BigDecimal))
            result = (BigDecimal)(object)value;
        else if (!TryConvertFrom(value, out result) && !TOther.TryConvertToTruncating(value, out result))
            Throw.NotSupportedEx();

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFromChecked<TOther>(TOther value, out BigDecimal result) where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(float))
        {
            result = (BigDecimal)(float)(object)value;
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            result = (BigDecimal)(double)(object)value;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            result = (BigDecimal)(decimal)(object)value;
            return true;
        }
        else if (typeof(TOther).IsAssignableTo(typeof(IBinaryInteger<>)))
        {
            if (TOther.TryConvertToChecked<BigInteger>(value, out var intValue))
            {
                result = (BigDecimal)intValue;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static bool TryConvertFrom<TOther>(TOther value, out BigDecimal result) where TOther : INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(float))
        {
            float actualValue = (float)(object)value;
            result = float.IsNaN(actualValue) ? Zero : (BigDecimal)actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            double actualValue = (double)(object)value;
            result = double.IsNaN(actualValue) ? Zero : (BigDecimal)actualValue;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            result = (BigDecimal)(decimal)(object)value;
            return true;
        }
        else if (TOther.IsInteger(value))
        {
            if (TOther.TryConvertToChecked<BigInteger>(value, out var integer))
            {
                result = (BigDecimal)integer;
                return true;
            }
        }

        result = default;
        return false;
    }

#endif

    /// <summary>
    /// Gets a <see cref="BigDecimal"/> representation of a <see cref="float"/> value.
    /// </summary>
    public static BigDecimal FromSingle(float value, FloatConversion conversionMode = FloatConversion.Truncate)
    {
        return conversionMode switch
        {
            FloatConversion.Roundtrip => FromFloat(value, 9),
            FloatConversion.Truncate => FromFloat(value, 7),
            FloatConversion.Exact => FromFloat(value, 0),
            FloatConversion.ParseString => Parse(value.ToString(FromFloatFormat, CultureInfo.InvariantCulture).AsSpan(), FromFloatStyle, CultureInfo.InvariantCulture),
            _ => Throw.ArgumentOutOfRangeEx<BigDecimal>(nameof(conversionMode)),
        };
    }

    /// <summary>
    /// Gets a <see cref="BigDecimal"/> representation of a <see cref="double"/> value.
    /// </summary>
    public static BigDecimal FromDouble(double value, FloatConversion conversionMode = FloatConversion.Truncate)
    {
        return conversionMode switch
        {
            FloatConversion.Roundtrip => FromFloat(value, 17),
            FloatConversion.Truncate => FromFloat(value, 15),
            FloatConversion.Exact => FromFloat(value, 0),
            FloatConversion.ParseString => Parse(value.ToString(FromFloatFormat, CultureInfo.InvariantCulture).AsSpan(), FromFloatStyle, CultureInfo.InvariantCulture),
            _ => Throw.ArgumentOutOfRangeEx<BigDecimal>(nameof(conversionMode)),
        };
    }

    private static BigDecimal FromFloat(double value, int precision)
    {
        if (double.IsNaN(value))
            Throw.ArgumentOutOfRangeEx(nameof(value), "Floating point NaN values cannot be converted to BigDecimal.");

        if (double.IsNegativeInfinity(value) || double.IsPositiveInfinity(value))
            Throw.ArgumentOutOfRangeEx(nameof(value), "Floating point infinity values cannot be converted to BigDecimal.");

        Debug.Assert(precision is 0 or 7 or 9 or 15 or 17, "unexpected precision value");

        unchecked
        {
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

            while ((mantissa & 1) == 0)
            {
                // mantissa is even
                mantissa >>= 1;
                exponent++;
            }

            if (negative)
                mantissa = -mantissa;

            var resultMantissa = (BigInteger)mantissa;
            int resultExponent;
            bool trimTrailingZeros;

            if (exponent == 0)
            {
                resultExponent = 0;
                trimTrailingZeros = false;
            }
            else if (exponent < 0)
            {
                resultMantissa *= BigIntegerPow5.Get(-exponent);
                resultExponent = exponent;
                trimTrailingZeros = false;
            }
            else
            {
                // exponent > 0
                resultMantissa <<= exponent; // *= BigInteger.Pow(BigInt2, exponent);
                resultExponent = 0;
                trimTrailingZeros = true;
            }

            if (precision > 0)
            {
                int digits = resultMantissa.CountDigits();
                int extraDigits = digits - precision;

                if (extraDigits <= 0)
                    return new BigDecimal(resultMantissa, resultExponent, digits);

                resultMantissa = resultMantissa.Divide(BigIntegerPow10.Get(extraDigits));
                resultExponent += extraDigits;
                trimTrailingZeros = true;
            }

            if (trimTrailingZeros)
                return new BigDecimal(resultMantissa, resultExponent);

            return new BigDecimal(resultMantissa, resultExponent, resultMantissa.CountDigits());
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
            Throw.DivideByZeroEx();

        if (maxExtendedPrecision <= 0)
            Throw.ArgumentOutOfRangeEx(nameof(maxExtendedPrecision));

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
            Throw.ArithmeticEx("The result of the division could not be represented exactly as a decimal value.");

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
            Throw.DivideByZeroEx();

        if (dividend.IsZero)
        {
            result = Zero;
            return true;
        }

        if (BigInteger.Abs(divisor._mantissa).IsOne)
        {
            result = divisor switch
            {
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

        if (remainder.IsZero)
        {
            result = new BigDecimal(mantissa, dividend._exponent - divisor._exponent - exponentChange);
            return true;
        }

        result = Zero;
        return false;
    }

    /// <summary>
    /// Returns the specified basis raised to the specified exponent. Exponent must be greater than or equal to 0.
    /// </summary>
    public static BigDecimal Pow(BigDecimal basis, int exponent) => exponent switch
    {
        < 0 => Throw.ArgumentOutOfRangeEx<BigDecimal>(nameof(exponent)),
        0 => One,
        1 => basis,
        _ => new BigDecimal(BigInteger.Pow(basis._mantissa, exponent), basis._exponent * exponent),
    };

    /// <summary>
    /// Returns ten (10) raised to the specified exponent.
    /// </summary>
    public static BigDecimal Pow10(int exponent) => exponent == 0 ? One : new BigDecimal(BigInteger.One, exponent, 1);

    /// <summary>
    /// Determines whether a value represents an integral value.
    /// </summary>
    public static bool IsInteger(BigDecimal value) => value._exponent >= 0;

    /// <summary>
    /// Determines whether a value represents an odd integral value.
    /// </summary>
    public static bool IsOddInteger(BigDecimal value) => value._exponent == 0 && !value._mantissa.IsEven;

    /// <summary>
    /// Determines whether a value represents an even integral value.
    /// </summary>
    public static bool IsEvenInteger(BigDecimal value) => value._exponent > 0 || (value._exponent == 0 && value._mantissa.IsEven);

    /// <summary>
    /// Determines if a value is negative.
    /// </summary>
    public static bool IsNegative(BigDecimal value) => value._mantissa.Sign < 0;

    /// <summary>
    /// Determines if a value is positive.
    /// </summary>
    public static bool IsPositive(BigDecimal value) => value._mantissa.Sign > 0;

    /// <summary>
    /// Compares to values to compute which has a greater magnitude.
    /// </summary>
    public static BigDecimal MaxMagnitude(BigDecimal x, BigDecimal y)
    {
        var ax = Abs(x);
        var ay = Abs(y);

        if (ax > ay)
        {
            return x;
        }

        if (ax == ay)
        {
            return IsNegative(x) ? y : x;
        }

        return y;
    }

    /// <summary>
    /// Compares to values to compute which has a lesser magnitude.
    /// </summary>
    public static BigDecimal MinMagnitude(BigDecimal x, BigDecimal y)
    {
        var ax = Abs(x);
        var ay = Abs(y);

        if (ax < ay)
        {
            return x;
        }

        if (ax == ay)
        {
            return IsNegative(x) ? x : y;
        }

        return y;
    }

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
            Throw.ArgumentOutOfRangeEx(nameof(precision));

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
    /// Rounds the value to the nearest integer using the given rounding mode.
    /// </summary>
    public static BigDecimal Round(BigDecimal value, RoundingMode mode = RoundingMode.MidpointToEven) => Round(value, 0, mode);

    /// <summary>
    /// Rounds the value to the specified number of decimal places using the given rounding mode.
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
    /// Rounds the value to the specified precision using the given rounding mode.
    /// </summary>
    public static BigDecimal RoundToPrecision(BigDecimal value, int precision, RoundingMode mode = RoundingMode.MidpointToEven)
    {
        if (precision < 1)
            Throw.ArgumentOutOfRangeEx(nameof(precision));

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
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    public static BigDecimal Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Converts the string representation of a number to its decimal equivalent.
    /// </summary>
    /// <param name="s">The string representation of the number to convert.</param>
    /// <param name="style">A combination of <see cref="NumberStyles"/> values that indicate the styles that can be parsed.</param>
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    public static BigDecimal Parse(string s, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null) => Parse(s.AsSpan(), style, provider);

    /// <summary>
    /// Converts the string representation of a number to its decimal equivalent.
    /// </summary>
    /// <param name="s">The string representation of the number to convert.</param>
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    /// <param name="result">The parsed decimal value if parsing was successful, otherwise zero.</param>
    /// <returns><see langword="true"/> if parsing was successful, otherwise <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BigDecimal result) => TryParse(s.AsSpan(), provider, out result);

    /// <summary>
    /// Converts the string representation of a number to its decimal equivalent.
    /// </summary>
    /// <param name="s">The string representation of the number to convert.</param>
    /// <param name="style">A combination of <see cref="NumberStyles"/> values that indicate the styles that can be parsed.</param>
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    /// <param name="result">The parsed decimal value if parsing was successful, otherwise zero.</param>
    /// <returns><see langword="true"/> if parsing was successful, otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out BigDecimal result) => TryParse(s.AsSpan(), style, provider, out result);

    /// <summary>
    /// Converts the string representation of a number to its decimal equivalent.
    /// </summary>
    /// <param name="s">The string representation of the number to convert.</param>
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    public static BigDecimal Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, NumberStyles.Number, provider);

    /// <summary>
    /// Converts the string representation of a number to its decimal equivalent.
    /// </summary>
    /// <param name="s">The string representation of the number to convert.</param>
    /// <param name="style">A combination of <see cref="NumberStyles"/> values that indicate the styles that can be parsed.</param>
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    public static BigDecimal Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null)
    {
        if (!TryParse(s, style, provider, out var result))
            Throw.FormatEx("Input string was not in a correct format.");

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
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    /// <param name="result">The parsed decimal value if parsing was successful, otherwise zero.</param>
    /// <returns><see langword="true"/> if parsing was successful, otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BigDecimal result) => TryParse(s, NumberStyles.Number, provider, out result);

    /// <summary>
    /// Converts the string representation of a number to its decimal equivalent.
    /// </summary>
    /// <param name="s">The string representation of the number to convert.</param>
    /// <param name="style">A combination of <see cref="NumberStyles"/> values that indicate the styles that can be parsed.</param>
    /// <param name="provider">A format provider that supplies culture-specific parsing information.</param>
    /// <param name="result">The parsed decimal value if parsing was successful, otherwise zero.</param>
    /// <returns><see langword="true"/> if parsing was successful, otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out BigDecimal result)
    {
        if (style.HasFlag(NumberStyles.AllowHexSpecifier))
            Throw.ArgumentEx("Hex number styles are not supported.", nameof(style));

        var formatInfo = NumberFormatInfo.GetInstance(provider);
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
            if (allowParenthesis && s.Length >= 3 && s[0] == '(')
            {
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
            while (s.Length > 0 && !char.IsDigit(s[0]) && !s.StartsWith(formatInfo.NumberDecimalSeparator.AsSpan(), cmp))
            {
                if (allowCurrencySymbol && s.StartsWith(formatInfo.CurrencySymbol.AsSpan(), cmp))
                {
                    if (currency)
                        return false;

                    currency = true;
                    s = s[formatInfo.CurrencySymbol.Length..];
                }
                else if (allowLeadingSign && StartsWithSign(s, out int parsedSign, out int signLength))
                {
                    if (sign != 0)
                        return false;

                    sign = parsedSign;
                    s = s[signLength..];
                }
                else
                {
                    return false;
                }

                TrimStart(ref s);
            }

            return true;

            bool StartsWithSign(ReadOnlySpan<char> s, out int sign, out int signLength)
            {
                if (s.StartsWith(formatInfo.PositiveSign.AsSpan(), cmp))
                {
                    sign = 1;
                    signLength = formatInfo.PositiveSign.Length;
                    return true;
                }
                else if (s.StartsWith(formatInfo.NegativeSign.AsSpan(), cmp))
                {
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
            while (s.Length > 0 && !char.IsDigit(s[^1]) && !s.EndsWith(formatInfo.NumberDecimalSeparator.AsSpan(), cmp))
            {
                if (allowCurrencySymbol && s.EndsWith(formatInfo.CurrencySymbol.AsSpan(), cmp))
                {
                    if (currency)
                        return false;

                    currency = true;
                    s = s[..^formatInfo.CurrencySymbol.Length];
                }
                else if (allowTrailingSign && EndsWithSign(s, out int parsedSign, out int signLength))
                {
                    if (sign != 0)
                        return false;

                    sign = parsedSign;
                    s = s[..^signLength];
                }
                else
                {
                    return false;
                }

                TrimEnd(ref s);
            }

            return true;

            bool EndsWithSign(ReadOnlySpan<char> s, out int sign, out int signLength)
            {
                if (s.EndsWith(formatInfo.PositiveSign.AsSpan(), cmp))
                {
                    sign = 1;
                    signLength = formatInfo.PositiveSign.Length;
                    return true;
                }
                else if (s.EndsWith(formatInfo.NegativeSign.AsSpan(), cmp))
                {
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
            if (allowExponent)
            {
                int index = s.LastIndexOfAny('E', 'e');

                if (index >= 0)
                {
                    var e = s[(index + 1)..];
                    s = s[..index];
#if NETSTANDARD2_0
                    return int.TryParse(e.ToString(), NumberStyles.AllowLeadingSign, provider, out result);
#else
                    return int.TryParse(e, NumberStyles.AllowLeadingSign, provider, out result);
#endif
                }
            }

            result = 0;
            return true;
        }

        bool TryParseFractional(ref ReadOnlySpan<char> s, out BigDecimal? result)
        {
            if (!allowDecimalPoint || !SplitFractional(ref s, out var f))
            {
                result = null;
                return true;
            }

            f = f.TrimEnd('0');

            if (f.Length == 0)
            {
                result = Zero;
                return true;
            }

            int exponent = -f.Length;
            f = f.TrimStart('0');

#if NETSTANDARD2_0
            if (!BigInteger.TryParse(f.ToString(), NumberStyles.None, provider, out var mantissa))
#else
            if (!BigInteger.TryParse(f, NumberStyles.None, provider, out var mantissa))
#endif
            {
                result = null;
                return false;
            }

            result = new BigDecimal(mantissa, exponent, f.Length);
            return true;

            bool SplitFractional(ref ReadOnlySpan<char> s, out ReadOnlySpan<char> f)
            {
                string decimalSeparator = currency ? formatInfo.CurrencyDecimalSeparator : formatInfo.NumberDecimalSeparator;
                int decimalIndex = s.IndexOf(decimalSeparator.AsSpan(), cmp);

                if (decimalIndex >= 0)
                {
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
            if (s.Length == 0)
            {
                result = null;
                return true;
            }

            s = s.TrimStart('0');

            if (s.Length == 0)
            {
                result = Zero;
                return true;
            }

            int preTrimLength = s.Length;
            s = s.TrimEnd('0');
            int exponent = preTrimLength - s.Length;

            var (wholeStyle, wholeFormatInfo) = GetWholeStyleAndInfo();

#if NETSTANDARD2_0
            if (!BigInteger.TryParse(s.ToString(), wholeStyle, wholeFormatInfo, out var mantissa))
#else
            if (!BigInteger.TryParse(s, wholeStyle, wholeFormatInfo, out var mantissa))
#endif
            {
                result = null;
                return false;
            }

            if (allowThousands)
                result = new BigDecimal(mantissa, exponent);
            else
                result = new BigDecimal(mantissa, exponent, s.Length);

            return true;

            (NumberStyles Style, NumberFormatInfo FormatInfo) GetWholeStyleAndInfo()
            {
                if (allowThousands)
                {
                    if (currency && formatInfo.CurrencyGroupSeparator != formatInfo.NumberGroupSeparator)
                    {
                        var copy = (NumberFormatInfo)formatInfo.Clone();
                        copy.NumberGroupSeparator = formatInfo.CurrencyGroupSeparator;

                        return (NumberStyles.AllowThousands, copy);
                    }
                    else
                    {
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
    /// Returns a full-precision decimal form string representation of this value using the current culture.
    /// </summary>
    public override string ToString() => ToString(null);

    /// <summary>
    /// Returns a full-precision decimal form string representation of this value.
    /// </summary>
    /// <param name="formatProvider">The format provider that will be used to obtain number format information. The current culture is used if none is
    /// provided.</param>
    public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);

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
#if NETSTANDARD2_0
                if (int.TryParse(format[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
#else
                if (int.TryParse(format.AsSpan()[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int ps))
#endif
                    precisionSpecifier = ps;
                else
                    Throw.FormatEx($"Invalid precision specifier: '{format[1..]}'");
            }
        }

        if (formatSpecifier == 'G')
        {
            BigDecimal value;

            if (precisionSpecifier == null || precisionSpecifier.GetValueOrDefault() == 0)
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

        if (formatSpecifier == 'F' || formatSpecifier == 'N')
        {
            string wholePartFormat = formatSpecifier == 'F' ? "F0" : "N0";

            int decimals = precisionSpecifier.HasValue ? precisionSpecifier.GetValueOrDefault() : formatInfo.NumberDecimalDigits;
            var value = Round(this, decimals, RoundingMode.MidpointAwayFromZero);

            if (decimals == 0)
                return GetIntegerString(value, wholePartFormat);

            return GetDecimalString(value, wholePartFormat, decimals);
        }

        if (formatSpecifier == 'E')
            return GetExponentialString(this, precisionSpecifier);

        if (formatSpecifier == 'C' || formatSpecifier == 'P')
        {
            BigDecimal value = this;

            if (formatSpecifier == 'P')
            {
                // Convert percentage format info params to currency params and write it out as a currency value:

                formatInfo = new NumberFormatInfo()
                {
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

        if (formatSpecifier == 'R')
        {
            if (_exponent == 0)
                return _mantissa.ToString(CultureInfo.InvariantCulture);

            return ((FormattableString)$"{_mantissa}E{_exponent}").ToString(CultureInfo.InvariantCulture);
        }

        Throw.FormatEx($"Format specifier was invalid: '{formatSpecifier}'.");
        return default;

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

            for (insertPoint = wholeString.Length; insertPoint > 0; insertPoint--)
            {
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

        static int PositivePercentagePatternToCurrencyPattern(int positivePercentagePattern) => positivePercentagePattern switch
        {
            0 => 3,
            1 => 1,
            2 => 0,
            3 => 2,
            _ => Throw.NotSupportedEx<int>("Unsupported positive percentage pattern."),
        };

        static int NegativePercentagePatternToCurrencyPattern(int negativePercentagePattern) => negativePercentagePattern switch
        {
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

    #endregion

    #region Equality and Comparison Methods

    /// <summary>
    /// Compares this to another <see cref="BigDecimal"/>.
    /// </summary>
    public int CompareTo(BigDecimal other)
    {
        return _exponent > other._exponent ? AlignMantissa(this, other).CompareTo(other._mantissa) : _mantissa.CompareTo(AlignMantissa(other, this));
    }

    /// <inheritdoc cref="IComparable.CompareTo(object?)"/>
    int IComparable.CompareTo(object? obj)
    {
        if (obj == null)
            return 1;

        return CompareTo((BigDecimal)obj);
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

    /// <summary>
    /// Returns the mantissa of value, aligned to the reference exponent. Assumes the value exponent is larger than the reference exponent.
    /// </summary>
    private static BigInteger AlignMantissa(BigDecimal value, BigDecimal reference)
    {
        Debug.Assert(value._exponent >= reference._exponent, "value exponent must be greater than or equal to reference exponent");
        return value._mantissa * BigIntegerPow10.Get(value._exponent - reference._exponent);
    }

    #endregion

#if NET7_0_OR_GREATER

    #region Explicit Generic Math Implementations

    private static BigDecimal _e;
    private static BigDecimal _pi;
    private static BigDecimal _tau;

    /// <inheritdoc cref="IFloatingPointConstants{TSelf}.E"/>
    static BigDecimal IFloatingPointConstants<BigDecimal>.E
    {
        get {
            if (_e.IsZero)
                _e = Parse("2.7182818284590452353602874713526624977572470936999");

            return _e;
        }
    }

    /// <inheritdoc cref="IFloatingPointConstants{TSelf}.Pi"/>
    static BigDecimal IFloatingPointConstants<BigDecimal>.Pi
    {
        get {
            if (_pi.IsZero)
                _pi = Parse("3.1415926535897932384626433832795028841971693993751");

            return _pi;
        }
    }

    /// <inheritdoc cref="IFloatingPointConstants{TSelf}.Tau"/>
    static BigDecimal IFloatingPointConstants<BigDecimal>.Tau
    {
        get {
            if (_tau.IsZero)
                _tau = Parse("6.2831853071795864769252867665590057683943387987502");

            return _tau;
        }
    }

    /// <inheritdoc cref="ISignedNumber{TSelf}.NegativeOne"/>
    static BigDecimal ISignedNumber<BigDecimal>.NegativeOne => MinusOne;

    /// <inheritdoc cref="INumberBase{TSelf}.Radix"/>
    static int INumberBase<BigDecimal>.Radix => 10;

    /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity"/>
    static BigDecimal IAdditiveIdentity<BigDecimal, BigDecimal>.AdditiveIdentity => Zero;

    /// <inheritdoc cref="IMultiplicativeIdentity{TSelf, TResult}.MultiplicativeIdentity"/>
    static BigDecimal IMultiplicativeIdentity<BigDecimal, BigDecimal>.MultiplicativeIdentity => One;

    /// <inheritdoc cref="IFloatingPoint{TSelf}.GetExponentByteCount"/>
    int IFloatingPoint<BigDecimal>.GetExponentByteCount() => sizeof(int);

    /// <inheritdoc cref="IFloatingPoint{TSelf}.GetExponentShortestBitLength"/>
    int IFloatingPoint<BigDecimal>.GetExponentShortestBitLength() => ((IBinaryInteger<int>)_exponent).GetShortestBitLength();

    /// <inheritdoc cref="IFloatingPoint{TSelf}.GetSignificandBitLength"/>
    int IFloatingPoint<BigDecimal>.GetSignificandBitLength() => _mantissa.GetByteCount(false) * 8;

    /// <inheritdoc cref="IFloatingPoint{TSelf}.GetSignificandByteCount"/>
    int IFloatingPoint<BigDecimal>.GetSignificandByteCount() => _mantissa.GetByteCount();

    /// <inheritdoc cref="IFloatingPoint{TSelf}.Round(TSelf, int, MidpointRounding)"/>
    static BigDecimal IFloatingPoint<BigDecimal>.Round(BigDecimal x, int digits, MidpointRounding mode) => Round(x, digits, (RoundingMode)mode);

    /// <inheritdoc cref="IFloatingPoint{TSelf}.TryWriteExponentBigEndian(Span{byte}, out int)"/>
    bool IFloatingPoint<BigDecimal>.TryWriteExponentBigEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<int>)_exponent).TryWriteBigEndian(destination, out bytesWritten);
    }

    /// <inheritdoc cref="IFloatingPoint{TSelf}.TryWriteExponentLittleEndian(Span{byte}, out int)"/>
    bool IFloatingPoint<BigDecimal>.TryWriteExponentLittleEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<int>)_exponent).TryWriteLittleEndian(destination, out bytesWritten);
    }

    /// <inheritdoc cref="IFloatingPoint{TSelf}.TryWriteSignificandBigEndian(Span{byte}, out int)"/>
    bool IFloatingPoint<BigDecimal>.TryWriteSignificandBigEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<BigInteger>)_mantissa).TryWriteBigEndian(destination, out bytesWritten);
    }

    /// <inheritdoc cref="IFloatingPoint{TSelf}.TryWriteSignificandLittleEndian(Span{byte}, out int)"/>
    bool IFloatingPoint<BigDecimal>.TryWriteSignificandLittleEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<BigInteger>)_mantissa).TryWriteLittleEndian(destination, out bytesWritten);
    }

    /// <inheritdoc cref="INumberBase{TSelf}.IsCanonical(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsCanonical(BigDecimal value) => true;

    /// <inheritdoc cref="INumberBase{TSelf}.IsComplexNumber(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsComplexNumber(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsFinite(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsFinite(BigDecimal value) => true;

    /// <inheritdoc cref="INumberBase{TSelf}.IsImaginaryNumber(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsImaginaryNumber(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsInfinity(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsInfinity(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsNaN(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsNaN(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsNegativeInfinity(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsNegativeInfinity(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsNormal(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsNormal(BigDecimal value) => !value.IsZero;

    /// <inheritdoc cref="INumberBase{TSelf}.IsPositiveInfinity(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsPositiveInfinity(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsRealNumber(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsRealNumber(BigDecimal value) => true;

    /// <inheritdoc cref="INumberBase{TSelf}.IsSubnormal(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsSubnormal(BigDecimal value) => false;

    /// <inheritdoc cref="INumberBase{TSelf}.IsZero(TSelf)"/>
    static bool INumberBase<BigDecimal>.IsZero(BigDecimal value) => value.IsZero;

    /// <inheritdoc cref="INumberBase{TSelf}.MaxMagnitudeNumber(TSelf, TSelf)"/>
    static BigDecimal INumberBase<BigDecimal>.MaxMagnitudeNumber(BigDecimal x, BigDecimal y) => MaxMagnitude(x, y);

    /// <inheritdoc cref="INumberBase{TSelf}.MinMagnitudeNumber(TSelf, TSelf)"/>
    static BigDecimal INumberBase<BigDecimal>.MinMagnitudeNumber(BigDecimal x, BigDecimal y) => MinMagnitude(x, y);

    /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromChecked{TOther}(TOther, out TSelf)"/>
    static bool INumberBase<BigDecimal>.TryConvertFromChecked<TOther>(TOther value, out BigDecimal result) => TryConvertFromChecked(value, out result);

    /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromSaturating{TOther}(TOther, out TSelf)"/>
    static bool INumberBase<BigDecimal>.TryConvertFromSaturating<TOther>(TOther value, out BigDecimal result) => TryConvertFrom(value, out result);

    /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromTruncating{TOther}(TOther, out TSelf)"/>
    static bool INumberBase<BigDecimal>.TryConvertFromTruncating<TOther>(TOther value, out BigDecimal result) => TryConvertFrom(value, out result);

    /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToChecked{TOther}(TSelf, out TOther)"/>
    static bool INumberBase<BigDecimal>.TryConvertToChecked<TOther>(BigDecimal value, [MaybeNullWhen(false)] out TOther result)
    {
        if (typeof(TOther) == typeof(float))
        {
            result = (TOther)(object)(float)value;
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            result = (TOther)(object)(double)value;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            result = (TOther)(object)(decimal)value;
            return true;
        }
        else if (typeof(TOther).IsAssignableTo(typeof(IBinaryInteger<>)))
        {
            var intValue = (BigInteger)value;
            return TOther.TryConvertFromChecked(intValue, out result);
        }

        result = default;
        return false;
    }

    /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToSaturating{TOther}(TSelf, out TOther)"/>
    static bool INumberBase<BigDecimal>.TryConvertToSaturating<TOther>(BigDecimal value, [MaybeNullWhen(false)] out TOther result)
    {
        if (typeof(TOther) == typeof(float))
        {
            result = (TOther)(object)(float)value;
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            result = (TOther)(object)(double)value;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            result = value < decimal.MinValue ? (TOther)(object)decimal.MinValue :
                     value > decimal.MaxValue ? (TOther)(object)decimal.MaxValue :
                     (TOther)(object)(decimal)value;

            return true;
        }
        else if (typeof(TOther).IsAssignableTo(typeof(IBinaryInteger<>)))
        {
            var intValue = (BigInteger)value;
            return TOther.TryConvertFromSaturating(intValue, out result);
        }

        result = default;
        return false;
    }

    /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToTruncating{TOther}(TSelf, out TOther)"/>
    static bool INumberBase<BigDecimal>.TryConvertToTruncating<TOther>(BigDecimal value, [MaybeNullWhen(false)] out TOther result)
    {
        if (typeof(TOther) == typeof(float))
        {
            result = (TOther)(object)(float)value;
            return true;
        }
        else if (typeof(TOther) == typeof(double))
        {
            result = (TOther)(object)(double)value;
            return true;
        }
        else if (typeof(TOther) == typeof(decimal))
        {
            result = value < decimal.MinValue ? (TOther)(object)decimal.MinValue :
                     value > decimal.MaxValue ? (TOther)(object)decimal.MaxValue :
                     (TOther)(object)(decimal)value;

            return true;
        }
        else if (typeof(TOther).IsAssignableTo(typeof(IBinaryInteger<>)))
        {
            var intValue = (BigInteger)value;
            return TOther.TryConvertFromTruncating(intValue, out result);
        }

        result = default;
        return false;
    }

    /// <inheritdoc cref="ISpanFormattable.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // TODO: Implement better performing option

        string s = ToString(format.ToString(), provider);

        if (destination.Length < s.Length)
        {
            charsWritten = 0;
            return false;
        }

        s.CopyTo(destination);
        charsWritten = s.Length;
        return true;
    }

    #endregion

#endif
}