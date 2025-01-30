using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

#if NET

/// <content>
/// Contains .NET7+ generic math support for <see cref="BigDecimal"/>.
/// </content>
partial struct BigDecimal : IFloatingPoint<BigDecimal>
{
    /// <summary>
    /// Creates an instance of <see cref="BigDecimal"/> from a value.
    /// </summary>
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

    /// <summary>
    /// Creates an instance of <see cref="BigDecimal"/> from a value.
    /// </summary>
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

    /// <summary>
    /// Creates an instance of <see cref="BigDecimal"/> from a value.
    /// </summary>
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

    #region Explicit Generic Math Implementations

    private static BigDecimal _e;
    private static BigDecimal _pi;
    private static BigDecimal _tau;

    /// <inheritdoc />
    static BigDecimal IFloatingPointConstants<BigDecimal>.E
    {
        get {
            if (_e.IsZero)
                _e = Parse("2.7182818284590452353602874713526624977572470936999");

            return _e;
        }
    }

    /// <inheritdoc />
    static BigDecimal IFloatingPointConstants<BigDecimal>.Pi
    {
        get {
            if (_pi.IsZero)
                _pi = Parse("3.1415926535897932384626433832795028841971693993751");

            return _pi;
        }
    }

    /// <inheritdoc />
    static BigDecimal IFloatingPointConstants<BigDecimal>.Tau
    {
        get {
            if (_tau.IsZero)
                _tau = Parse("6.2831853071795864769252867665590057683943387987502");

            return _tau;
        }
    }

    /// <inheritdoc />
    static BigDecimal ISignedNumber<BigDecimal>.NegativeOne => MinusOne;

    /// <inheritdoc />
    static int INumberBase<BigDecimal>.Radix => 10;

    /// <inheritdoc />
    static BigDecimal IAdditiveIdentity<BigDecimal, BigDecimal>.AdditiveIdentity => Zero;

    /// <inheritdoc />
    static BigDecimal IMultiplicativeIdentity<BigDecimal, BigDecimal>.MultiplicativeIdentity => One;

    /// <inheritdoc />
    int IFloatingPoint<BigDecimal>.GetExponentByteCount() => sizeof(int);

    /// <inheritdoc />
    int IFloatingPoint<BigDecimal>.GetExponentShortestBitLength() => ((IBinaryInteger<int>)_exponent).GetShortestBitLength();

    /// <inheritdoc />
    int IFloatingPoint<BigDecimal>.GetSignificandBitLength() => _mantissa.GetByteCount(false) * 8;

    /// <inheritdoc />
    int IFloatingPoint<BigDecimal>.GetSignificandByteCount() => _mantissa.GetByteCount();

    /// <inheritdoc />
    static BigDecimal IFloatingPoint<BigDecimal>.Round(BigDecimal x, int digits, MidpointRounding mode) => Round(x, digits, (RoundingMode)mode);

    /// <inheritdoc />
    bool IFloatingPoint<BigDecimal>.TryWriteExponentBigEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<int>)_exponent).TryWriteBigEndian(destination, out bytesWritten);
    }

    /// <inheritdoc />
    bool IFloatingPoint<BigDecimal>.TryWriteExponentLittleEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<int>)_exponent).TryWriteLittleEndian(destination, out bytesWritten);
    }

    /// <inheritdoc />
    bool IFloatingPoint<BigDecimal>.TryWriteSignificandBigEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<BigInteger>)_mantissa).TryWriteBigEndian(destination, out bytesWritten);
    }

    /// <inheritdoc />
    bool IFloatingPoint<BigDecimal>.TryWriteSignificandLittleEndian(Span<byte> destination, out int bytesWritten)
    {
        return ((IBinaryInteger<BigInteger>)_mantissa).TryWriteLittleEndian(destination, out bytesWritten);
    }

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsCanonical(BigDecimal value) => true;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsComplexNumber(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsFinite(BigDecimal value) => true;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsImaginaryNumber(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsInfinity(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsNaN(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsNegativeInfinity(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsNormal(BigDecimal value) => !value.IsZero;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsPositiveInfinity(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsRealNumber(BigDecimal value) => true;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsSubnormal(BigDecimal value) => false;

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.IsZero(BigDecimal value) => value.IsZero;

    /// <inheritdoc />
    static BigDecimal INumberBase<BigDecimal>.MaxMagnitudeNumber(BigDecimal x, BigDecimal y) => MaxMagnitude(x, y);

    /// <inheritdoc />
    static BigDecimal INumberBase<BigDecimal>.MinMagnitudeNumber(BigDecimal x, BigDecimal y) => MinMagnitude(x, y);

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.TryConvertFromChecked<TOther>(TOther value, out BigDecimal result) => TryConvertFromChecked(value, out result);

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.TryConvertFromSaturating<TOther>(TOther value, out BigDecimal result) => TryConvertFrom(value, out result);

    /// <inheritdoc />
    static bool INumberBase<BigDecimal>.TryConvertFromTruncating<TOther>(TOther value, out BigDecimal result) => TryConvertFrom(value, out result);

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
}

#endif