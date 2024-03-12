using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <content>
/// Contains the rounding methods for the <see cref="BigDecimal"/> struct.
/// </content>
partial struct BigDecimal
{
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
}