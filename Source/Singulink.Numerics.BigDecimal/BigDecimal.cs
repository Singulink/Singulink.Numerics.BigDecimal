using System;
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
/// This implementation of big decimal always stores values with the minimum precision possible to accurately represent the value in order to conserve memory
/// and maintain optimal performance of operations on values.</para>
/// <para>
/// All operations on <see cref="BigDecimal"/> values are exact except division in the case of a repeating decimal result. If the result of the division cannot
/// be exactly represented in decimal form then the largest of the dividend precision, divisor precision and the specified maximum extended precision is used to
/// represent the result. You can specify the maximum extended precision to use for each division operation by calling the <see cref="Divide(BigDecimal,
/// BigDecimal, int, RoundingMode)"/> method. The <see cref="DivideExact(BigDecimal, BigDecimal)"/> and <see cref="TryDivideExact(BigDecimal, BigDecimal, out
/// BigDecimal)"/> methods can be used for division operations that are expected to return exact results.</para>
/// <para>
/// The standard division operator (<c>/</c>) first attempts to do an exact division and falls back to extended precision division using <see
/// cref="MaxExtendedDivisionPrecision"/> as the maximum extended precision parameter. It is recommended that you always use the division methods and specify
/// the maximum extended precision instead of depending on the default of the operator.</para>
/// <para>
/// Addition and subtraction are fully commutative and associative for all converted data types. Combined with its arbitrary precision, that makes <see
/// cref="BigDecimal"/> a great data type to store aggregate totals that can freely add and subtract values in any order without accruing inaccuracies over time
/// or overflowing.</para>
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
    public static BigDecimal One => new BigDecimal(BigInteger.One, 0, 1);

    /// <summary>
    /// Gets a value representing negative one (-1).
    /// </summary>
    public static BigDecimal MinusOne => new BigDecimal(BigInteger.MinusOne, 0, 1);

    #endregion

    private readonly BigInteger _mantissa;
    private readonly int _exponent;
    private readonly int _precision;

    /// <summary>
    /// Initializes a new instance of the <see cref="BigDecimal"/> struct.
    /// </summary>
    public BigDecimal(BigInteger mantissa, int exponent)
    {
        if (mantissa.IsZero)
        {
            _mantissa = default;
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

    /// <summary>
    /// Gets the number of digits that appear after the decimal point.
    /// </summary>
    public int DecimalPlaces => _exponent >= 0 ? 0 : -_exponent;

    /// <summary>
    /// Gets a value indicating whether the current value is 1.
    /// </summary>
    public bool IsOne => _mantissa.IsOne && _exponent is 0;

    /// <summary>
    /// Gets a value indicating whether the current value is a power of 10 (i.e. 0.1, 1, 10, 100, etc.).
    /// </summary>
    public bool IsPowerOfTen => _mantissa.IsOne;

    /// <summary>
    /// Gets a value indicating whether the current value is 0.
    /// </summary>
    public bool IsZero => _mantissa.IsZero;

    /// <summary>
    /// Gets the precision of this value, i.e. the total number of digits it contains (excluding any leading/trailing zeros). Zero values have a precision of 1.
    /// </summary>
    public int Precision => _precision is 0 ? 1 : _precision;

    /// <summary>
    /// Gets a number indicating the sign (negative, positive, or zero) of the current value.
    /// </summary>
    public int Sign => _mantissa.Sign;

    private bool IsPowerOfTenIgnoringSign => _precision is 1 && BigInteger.Abs(_mantissa).IsOne;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString("G100", CultureInfo.InvariantCulture);

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
}