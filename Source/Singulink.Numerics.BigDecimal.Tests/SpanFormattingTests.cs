using System;
using System.Globalization;
using System.Text;

namespace Singulink.Numerics.Tests;

[PrefixTestClass]
public class SpanFormattingTests
{
    [TestMethod]
    public void MatchesToString()
    {
        foreach (var (name, info) in FormattingTestData.FormatInfos)
        foreach (string format in FormattingTestData.StandardFormats)
        foreach (string s in FormattingTestData.AllValues)
        {
            var value = FormattingTestData.ParseValue(s);
            string expected = value.ToString(format, info);
            string context = $"value: {s}, format: '{format}', info: {name}";

            Assert.IsTrue(expected.Length > 0, context);

            // Oversized destination

            char[] buffer = new char[expected.Length + 16];
            Assert.IsTrue(value.TryFormat(buffer, out int charsWritten, format.AsSpan(), info), context);
            Assert.AreEqual(expected, new string(buffer, 0, charsWritten), context);

            // Exactly sized destination

            buffer = new char[expected.Length];
            Assert.IsTrue(value.TryFormat(buffer, out charsWritten, format.AsSpan(), info), context);
            Assert.AreEqual(expected.Length, charsWritten, context);
            Assert.AreEqual(expected, new string(buffer), context);

            // Undersized destination

            buffer = new char[expected.Length - 1];
            Assert.IsFalse(value.TryFormat(buffer, out charsWritten, format.AsSpan(), info), context);
            Assert.AreEqual(0, charsWritten, context);

            // Empty destination

            Assert.IsFalse(value.TryFormat(Span<char>.Empty, out charsWritten, format.AsSpan(), info), context);
            Assert.AreEqual(0, charsWritten, context);
        }
    }

    [TestMethod]
    public void DefaultFormatIsGeneral()
    {
        var value = FormattingTestData.ParseValue("-12345.678");
        char[] buffer = new char[32];

        Assert.IsTrue(value.TryFormat(buffer, out int charsWritten));
        Assert.AreEqual(value.ToString(), new string(buffer, 0, charsWritten));

        Assert.IsTrue(value.TryFormat(buffer, out charsWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("-12345.678", new string(buffer, 0, charsWritten));

        Assert.IsTrue(value.TryFormat(buffer, out charsWritten, "  ".AsSpan(), CultureInfo.InvariantCulture));
        Assert.AreEqual("-12345.678", new string(buffer, 0, charsWritten));
    }

    [TestMethod]
    public void LargeOutput()
    {
        var info = CultureInfo.InvariantCulture;

        var value = FormattingTestData.ParseValue("1E+1000");
        string expected = "1" + new string('0', 1000);
        Assert.AreEqual(expected, value.ToString(null, info));
        AssertTryFormat(value, "G", info, expected);

        value = FormattingTestData.ParseValue("-1E+1000");
        expected = "-1" + new string('0', 1000);
        Assert.AreEqual(expected, value.ToString("F0", info));
        AssertTryFormat(value, "F0", info, expected);

        value = FormattingTestData.ParseValue("1E-1000");
        expected = "0." + new string('0', 999) + "1";
        Assert.AreEqual(expected, value.ToString(null, info));
        AssertTryFormat(value, "G", info, expected);

        value = FormattingTestData.ParseValue("-1E-1000");
        expected = "(¤0." + new string('0', 999) + "1000)";
        Assert.AreEqual(expected, value.ToString("C1003", info));
        AssertTryFormat(value, "C1003", info, expected);

        value = FormattingTestData.ParseValue("1E+300");
        expected = "1" + string.Concat(System.Linq.Enumerable.Repeat(",000", 100)) + ".00";
        Assert.AreEqual(expected, value.ToString("N", info));
        AssertTryFormat(value, "N", info, expected);

        value = FormattingTestData.ParseValue("123.456");
        expected = "123.456" + new string('0', 997);
        Assert.AreEqual(expected, value.ToString("F1000", info));
        AssertTryFormat(value, "F1000", info, expected);

        expected = "1.23456" + new string('0', 995) + "E+002";
        Assert.AreEqual(expected, value.ToString("E1000", info));
        AssertTryFormat(value, "E1000", info, expected);

        string digits = new string('7', 2000);
        value = FormattingTestData.ParseValue(digits + "." + digits);
        expected = digits + "." + digits;
        Assert.AreEqual(expected, value.ToString(null, info));
        AssertTryFormat(value, "G", info, expected);
        AssertTryFormat(value, "R", info, digits + digits + "E-2000");

        static void AssertTryFormat(BigDecimal value, string format, IFormatProvider provider, string expected)
        {
            char[] buffer = new char[expected.Length];

            Assert.IsTrue(value.TryFormat(buffer, out int charsWritten, format.AsSpan(), provider));
            Assert.AreEqual(expected, new string(buffer, 0, charsWritten));

            Assert.IsFalse(value.TryFormat(new char[expected.Length - 1], out charsWritten, format.AsSpan(), provider));
            Assert.AreEqual(0, charsWritten);
        }
    }

    [TestMethod]
    public void InvalidFormat()
    {
        var value = FormattingTestData.ParseValue("1.5");
        char[] buffer = new char[64];

        Assert.ThrowsException<FormatException>(() => value.TryFormat(buffer, out _, "X".AsSpan(), CultureInfo.InvariantCulture));
        Assert.ThrowsException<FormatException>(() => value.TryFormat(buffer, out _, "F 2".AsSpan(), CultureInfo.InvariantCulture));
        Assert.ThrowsException<FormatException>(() => value.TryFormat(buffer, out _, "G-1".AsSpan(), CultureInfo.InvariantCulture));
        Assert.ThrowsException<FormatException>(() => value.TryFormat(buffer, out _, "G99999999999".AsSpan(), CultureInfo.InvariantCulture));
    }

#if NET

    [TestMethod]
    public void SpanFormattableInterface()
    {
        var value = FormattingTestData.ParseValue("-1234.5678");
        ISpanFormattable formattable = value;

        Assert.AreEqual("-1,234.57", string.Create(CultureInfo.InvariantCulture, $"{value:N2}"));
        Assert.AreEqual("-1200", string.Create(CultureInfo.InvariantCulture, $"{value:G2}"));
        Assert.AreEqual("-1.2E+07", string.Create(CultureInfo.InvariantCulture, $"{FormattingTestData.ParseValue("-12345678.9"):G2}"));
        Assert.AreEqual("(¤1,234.568)", string.Create(CultureInfo.InvariantCulture, $"{value:C3}"));
        Assert.AreEqual("-1234.5678", string.Create(CultureInfo.InvariantCulture, $"{value}"));
        Assert.AreEqual("[−1.234,568]", string.Create(CultureInfo.InvariantCulture, $"[{value.ToString("N3", FormattingTestData.Euro)}]"));

        char[] buffer = new char[32];
        Assert.IsTrue(formattable.TryFormat(buffer, out int charsWritten, "E2".AsSpan(), FormattingTestData.Euro));
        Assert.AreEqual("−1,23E+003", new string(buffer, 0, charsWritten));
    }

    [TestMethod]
    public void Utf8SpanFormattableInterface()
    {
        var value = FormattingTestData.ParseValue("-1234.5678");
        IUtf8SpanFormattable formattable = value;

        byte[] buffer = new byte[64];
        Assert.IsTrue(formattable.TryFormat(buffer, out int bytesWritten, "C".AsSpan(), FormattingTestData.Euro));
        Assert.AreEqual("−1.234,57 €", Encoding.UTF8.GetString(buffer, 0, bytesWritten));

        Assert.IsFalse(formattable.TryFormat(new byte[5], out bytesWritten, "C".AsSpan(), FormattingTestData.Euro));
        Assert.AreEqual(0, bytesWritten);
    }

#endif
}