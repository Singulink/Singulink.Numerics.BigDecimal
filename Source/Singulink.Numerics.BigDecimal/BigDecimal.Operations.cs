using System;
using System.Diagnostics;
using System.Numerics;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Contains operator implementations for <see cref="BigDecimal"/>.
/// </summary>
partial struct BigDecimal
{
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

        if (left.IsPowerOfTenIgnoringSign)
            return ShiftDecimal(right, left._exponent, IsNegative(left));

        if (right.IsPowerOfTenIgnoringSign)
            return ShiftDecimal(left, right._exponent, IsNegative(right));

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

        if (dividend.IsZero)
            return Zero;

        if (divisor.IsOne)
            return dividend;

        // Never reduce precision of the result compared to input values but cap precision extensions to maxExtendedPrecision

        int maxPrecision = Math.Max(Math.Max(maxExtendedPrecision, dividend._precision), divisor._precision);
        int exponentChange = Math.Max(0, maxPrecision - dividend._precision + divisor._precision);
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
    /// Performs a division operation that results in a rounded decimal answer.
    /// </summary>
    /// <param name="dividend">The dividend of the division operation.</param>
    /// <param name="divisor">The divisor of the division operation.</param>
    /// <param name="decimals">The number of decimal places the result should be rounded to.</param>
    /// <param name="mode">The rounding mode to use.</param>
    public static BigDecimal DivideRounded(BigDecimal dividend, BigDecimal divisor, int decimals, RoundingMode mode = RoundingMode.MidpointToEven)
    {
        if (divisor.IsZero)
            Throw.DivideByZeroEx();

        if (dividend.IsZero)
            return Zero;

        if (divisor.IsOne)
            return Round(dividend, decimals, mode);

        int exponentChange = Math.Max(0, decimals + 1 + dividend._exponent - divisor._exponent);
        var scaledDividendMantissa = dividend._mantissa * BigIntegerPow10.Get(exponentChange);
        var intermediateMantissa = scaledDividendMantissa.Divide(divisor._mantissa, mode);
        int intermediateExponent = dividend._exponent - divisor._exponent - exponentChange;

        int extraDigits = -intermediateExponent - decimals;

        if (extraDigits <= 0)
            return new BigDecimal(intermediateMantissa, intermediateExponent);

        var roundedMantissa = intermediateMantissa.Divide(BigIntegerPow10.Get(extraDigits), mode);
        int roundedExponent = intermediateExponent + extraDigits;

        return new BigDecimal(roundedMantissa, roundedExponent);
    }

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
            return x;

        if (ax == ay)
            return IsNegative(x) ? y : x;

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
            return x;

        if (ax == ay)
            return IsNegative(x) ? x : y;

        return y;
    }

    /// <summary>
    /// Returns the specified basis raised to the specified exponent. Exponent must be greater than or equal to 0.
    /// </summary>
    public static BigDecimal Pow(BigDecimal basis, int exponent) => exponent switch {
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
    /// Shifts the decimal point of a value by the specified number of places, effectively multiplying or dividing the value by a power of 10. A positive shift
    /// moves the decimal point to the right (i.e. increases the exponent) and a negative shift moves the decimal point to the left (i.e. decreases the
    /// exponent).
    /// </summary>
    public static BigDecimal ShiftDecimal(BigDecimal value, int shift) => new BigDecimal(value._mantissa, value._exponent + shift, value._precision);

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

        if (divisor.IsPowerOfTenIgnoringSign)
        {
            result = ShiftDecimal(dividend, -divisor._exponent, IsNegative(divisor));
            return true;
        }

        int maxPrecision = (int)Math.Min(dividend._precision + (long)Math.Ceiling(10.0 * divisor._precision / 3.0), int.MaxValue);
        int exponentChange = Math.Max(0, maxPrecision - dividend._precision + divisor._precision);
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

    private static BigDecimal ShiftDecimal(BigDecimal value, int shift, bool negate)
    {
        var result = ShiftDecimal(value, shift);

        if (negate)
            result = -result;

        return result;
    }

    /// <summary>
    /// Returns the mantissa of value, aligned to the reference exponent. Assumes the value exponent is larger than the reference exponent.
    /// </summary>
    private static BigInteger AlignMantissa(BigDecimal value, BigDecimal reference)
    {
        Debug.Assert(value._exponent >= reference._exponent, "value exponent must be greater than or equal to reference exponent");
        return value._mantissa * BigIntegerPow10.Get(value._exponent - reference._exponent);
    }
}