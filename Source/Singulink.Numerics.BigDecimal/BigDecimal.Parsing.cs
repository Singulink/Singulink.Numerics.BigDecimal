using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Singulink.Numerics.Utilities;

namespace Singulink.Numerics;

/// <content>
/// Contains parsing functionality for <see cref="BigDecimal"/>.
/// </content>
partial struct BigDecimal
{
    /// <inheritdoc cref="Parse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?)"/>
    public static BigDecimal Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc cref="Parse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?)"/>
    public static BigDecimal Parse(string s, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null) => Parse(s.AsSpan(), style, provider);

    /// <inheritdoc cref="Parse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?)"/>
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

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?, out BigDecimal)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, out BigDecimal result) => TryParse(s.AsSpan(), out result);

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?, out BigDecimal)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BigDecimal result) => TryParse(s.AsSpan(), provider, out result);

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?, out BigDecimal)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out BigDecimal result) => TryParse(s.AsSpan(), style, provider, out result);

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?, out BigDecimal)"/>
    public static bool TryParse(ReadOnlySpan<char> s, out BigDecimal result) => TryParse(s, NumberStyles.Number, null, out result);

    /// <inheritdoc cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider?, out BigDecimal)"/>
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

            if (exponent is not 0)
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
                    if (sign is not 0)
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
                    if (sign is not 0)
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

            if (f.Length is 0)
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
            if (s.Length is 0)
            {
                result = null;
                return true;
            }

            s = s.TrimStart('0');

            if (s.Length is 0)
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
}