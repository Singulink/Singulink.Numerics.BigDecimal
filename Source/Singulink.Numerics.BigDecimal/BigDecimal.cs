﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <summary>
/// Represents an arbitrary precision decimal.
/// </summary>
/// <remarks>
/// <para>
/// Note that this implementation of big decimal always stores values with the minimum precision possible to accurately represent the value in order to conserve
/// memory and maintain optimal performance of operations on values.</para>
/// <para>
/// All operations on <see cref="BigDecimal"/> values are exact except division in the case of a repeating decimal result. If the result of the division cannot
/// be exactly represented in decimal form then the largest of the dividend precision, divisor precision and the specified maximum extended precision is used to
/// represent the result. You can specify the maximum extended precision to use for each division operation by calling the <see cref="Divide(BigDecimal,
/// BigDecimal, int, RoundingMode)"/> method. The <see cref="DivideExact(BigDecimal, BigDecimal)"/> and <see cref="TryDivideExact(BigDecimal, BigDecimal, out
/// BigDecimal)"/> methods can be used for division operations that are expected to return exact results.</para>
/// <para>
/// The standard division operator (<c>/</c>) first attempts to do an exact division and falls back to extended precision division using <see
/// cref="MaxExtendedDivisionPrecision"/> as the maximum extended precision parameter. It is recommended that you always specify the maximum extended precision
/// instead of depending on the default of the operator.</para>
/// <para>
/// Addition and subtraction are fully commutative and associative for all converted data types. This makes <see cref="BigDecimal"/> a great data type to store
/// aggregate totals that can freely add and subtract values without accruing inaccuracies over time.</para>
/// <para>
/// Conversions from floating-point types (<see cref="float"/>/<see cref="double"/>) default to <see cref="FloatConversion.Truncate"/> mode in order to match
/// the behavior of floating point to <see cref="decimal"/> conversions, but there are several conversion modes available that are each suitable in different
/// situations. You can use the <see cref="FromSingle(float, FloatConversion)"/> or <see cref="FromDouble(double, FloatConversion)"/> methods to specify a
/// different conversion mode.</para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly partial struct BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>, IComparable
{
    #region Static Contants/Fields/Properties

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
    public bool IsOne => _mantissa.IsOne && _exponent is 0;

    /// <summary>
    /// Gets a number indicating the sign (negative, positive, or zero) of the current value.
    /// </summary>
    public int Sign => _mantissa.Sign;

    /// <summary>
    /// Gets the precision of this value, i.e. the total number of digits it contains (excluding any leading/trailing zeros). Zero values have a precision of 1.
    /// </summary>
    public int Precision => _precision is 0 ? 1 : _precision;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="BigDecimal"/> struct. Trusted private constructor.
    /// </summary>
    private BigDecimal(BigInteger mantissa, int exponent, int precision)
    {
        _mantissa = mantissa;
        _exponent = exponent;
        _precision = precision;
    }

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
    public static BigDecimal Pow10(int exponent) => exponent is 0 ? One : new BigDecimal(BigInteger.One, exponent, 1);

    /// <summary>
    /// Determines whether a value represents an integral value.
    /// </summary>
    public static bool IsInteger(BigDecimal value) => value._exponent >= 0;

    /// <summary>
    /// Determines whether a value represents an odd integral value.
    /// </summary>
    public static bool IsOddInteger(BigDecimal value) => value._exponent is 0 && !value._mantissa.IsEven;

    /// <summary>
    /// Determines whether a value represents an even integral value.
    /// </summary>
    public static bool IsEvenInteger(BigDecimal value) => value._exponent > 0 || (value._exponent is 0 && value._mantissa.IsEven);

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
        if (obj is null)
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
}