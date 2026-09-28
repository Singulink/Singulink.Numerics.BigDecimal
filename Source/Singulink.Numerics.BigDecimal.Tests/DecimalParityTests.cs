using System;
using System.Globalization;

namespace Singulink.Numerics.Tests;

/// <summary>
/// Validates that formatting produces identical output to <see cref="decimal"/> formatting for all the standard formats where the behavior is expected to
/// match, across a variety of number format infos.
/// </summary>
[PrefixTestClass]
public class DecimalParityTests
{
    [TestMethod]
    public void ToStringMatchesDecimal()
    {
        foreach (var (name, info) in FormattingTestData.FormatInfos)
        foreach (string format in FormattingTestData.DecimalParityFormats)
        foreach (string s in FormattingTestData.DecimalValues)
        {
            decimal d = decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            BigDecimal value = d;

            Assert.AreEqual(d.ToString(format, info), value.ToString(format, info), $"value: {s}, format: '{format}', info: {name}");
        }
    }

    [TestMethod]
    public void TryFormatMatchesDecimal()
    {
        char[] buffer = new char[256];

        foreach (var (name, info) in FormattingTestData.FormatInfos)
        foreach (string format in FormattingTestData.DecimalParityFormats)
        foreach (string s in FormattingTestData.DecimalValues)
        {
            decimal d = decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            BigDecimal value = d;
            string context = $"value: {s}, format: '{format}', info: {name}";

            Assert.IsTrue(value.TryFormat(buffer, out int charsWritten, format.AsSpan(), info), context);
            Assert.AreEqual(d.ToString(format, info), new string(buffer, 0, charsWritten), context);
        }
    }

    [TestMethod]
    public void DefaultDecimalDigitsMatchDecimal()
    {
        var info = new NumberFormatInfo {
            NumberDecimalDigits = 5,
            CurrencyDecimalDigits = 3,
            PercentDecimalDigits = 1,
        };

        decimal d = 1234.56789m;
        BigDecimal value = d;

        foreach (string format in new[] { "F", "N", "C", "P" })
            Assert.AreEqual(d.ToString(format, info), value.ToString(format, info), $"format: '{format}'");

        Assert.AreEqual("1234.56789", value.ToString("F", info));
        Assert.AreEqual("1,234.56789", value.ToString("N", info));
        Assert.AreEqual("¤1,234.568", value.ToString("C", info));
        Assert.AreEqual("123,456.8 %", value.ToString("P", info));
    }

    [TestMethod]
    public void CurrentCultureIsUsedByDefault()
    {
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat = FormattingTestData.Euro;
            CultureInfo.CurrentCulture = culture;

            decimal d = -1234.5m;
            BigDecimal value = d;

            Assert.AreEqual(d.ToString(), value.ToString());
            Assert.AreEqual("−1234,5", value.ToString());

            foreach (string format in FormattingTestData.DecimalParityFormats)
            {
                Assert.AreEqual(d.ToString(format), value.ToString(format), $"format: '{format}'");
                Assert.AreEqual(d.ToString(format, null), value.ToString(format, null), $"format: '{format}'");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}