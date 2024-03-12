using System.Numerics;

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
}