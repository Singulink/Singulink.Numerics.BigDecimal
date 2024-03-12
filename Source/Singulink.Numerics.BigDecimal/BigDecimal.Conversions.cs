using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <content>
/// Contains conversion operators and methods for <see cref="BigDecimal"/>.
/// </content>
partial struct BigDecimal
{
    private const string ToDecimalOrFloatFormat = "R";
    private const NumberStyles ToDecimalOrFloatStyle = NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;

    private const string FromFloatFormat = "G";
    private const NumberStyles FromFloatStyle = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;

    #region Conversions to BigDecimal

    /// <summary>
    /// Defines an implicit conversion from a <see cref="BigInteger"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(BigInteger value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="byte"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(byte value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="sbyte"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(sbyte value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="short"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(short value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="ushort"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(ushort value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="int"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(int value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="uint"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(uint value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="long"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(long value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="ulong"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(ulong value) => new BigDecimal(value, 0);

    /// <summary>
    /// Defines an implicit conversion from a <see cref="decimal"/> value to a <see cref="BigDecimal"/>.
    /// </summary>
    public static implicit operator BigDecimal(decimal value)
    {
        ref var decimalData = ref Unsafe.As<decimal, DecimalData>(ref value);

        var mantissa = (new BigInteger(decimalData.Hi) << 64) | decimalData.Lo;

        if (!decimalData.IsPositive)
            mantissa = -mantissa;

        return new BigDecimal(mantissa, -decimalData.Scale);
    }

    /// <summary>
    /// Defines an explicit conversion from a <see cref="float"/> value to a <see cref="BigDecimal"/> using the <see cref="FloatConversion.Truncate"/> mode.
    /// </summary>
    public static explicit operator BigDecimal(float value) => FromSingle(value);

    /// <summary>
    /// Defines an explicit conversion from a <see cref="double"/> value to a <see cref="BigDecimal"/> using the <see cref="FloatConversion.Truncate"/> mode.
    /// </summary>
    public static explicit operator BigDecimal(double value) => FromDouble(value);

    #endregion

    #region Conversions from BigDecimal

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="BigInteger"/>.
    /// </summary>
    public static explicit operator BigInteger(BigDecimal value)
    {
        return value._exponent < 0 ? value._mantissa / BigIntegerPow10.Get(-value._exponent) : value._mantissa * BigIntegerPow10.Get(value._exponent);
    }

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="byte"/>.
    /// </summary>
    public static explicit operator byte(BigDecimal value) => (byte)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="sbyte"/>.
    /// </summary>
    public static explicit operator sbyte(BigDecimal value) => (sbyte)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="short"/>.
    /// </summary>
    public static explicit operator short(BigDecimal value) => (short)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="ushort"/>.
    /// </summary>
    public static explicit operator ushort(BigDecimal value) => (ushort)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="int"/>.
    /// </summary>
    public static explicit operator int(BigDecimal value) => (int)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="uint"/>.
    /// </summary>
    public static explicit operator uint(BigDecimal value) => (uint)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="long"/>.
    /// </summary>
    public static explicit operator long(BigDecimal value) => (long)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="ulong"/>.
    /// </summary>
    public static explicit operator ulong(BigDecimal value) => (ulong)(BigInteger)value;

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="float"/>.
    /// </summary>
    public static explicit operator float(BigDecimal value)
    {
        return float.Parse(value.ToString(ToDecimalOrFloatFormat), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="double"/>.
    /// </summary>
    public static explicit operator double(BigDecimal value)
    {
        return double.Parse(value.ToString(ToDecimalOrFloatFormat), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Defines an explicit conversion from a <see cref="BigDecimal"/> value to a <see cref="decimal"/>.
    /// </summary>
    public static explicit operator decimal(BigDecimal value)
    {
        return decimal.Parse(value.ToString(ToDecimalOrFloatFormat), ToDecimalOrFloatStyle, CultureInfo.InvariantCulture);
    }

    #endregion

    #region Conversion Methods

    /// <summary>
    /// Gets a <see cref="BigDecimal"/> representation of a <see cref="float"/> value.
    /// </summary>
    public static BigDecimal FromSingle(float value, FloatConversion conversionMode = FloatConversion.Truncate)
    {
        return conversionMode switch {
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
        return conversionMode switch {
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

            // Subnormal numbers: exponent is effectively one higher, but there's no extra normalization bit in the mantissa Normal numbers.
            // Leave exponent as it is but add extra bit to the front of the mantissa
            if (exponent is 0)
                exponent++;
            else
                mantissa |= 1L << 52;

            if (mantissa is 0)
                return Zero;

            // Bias the exponent. It's actually biased by 1023, but we're treating the mantissa as m.0 rather than 0.m, so we need to subtract another 52
            // from it.
            exponent -= 1075;

            // Normalize

            while ((mantissa & 1) is 0)
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

            if (exponent is 0)
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
}