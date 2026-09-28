using System;
using System.Globalization;

namespace Singulink.Numerics.Tests;

[PrefixTestClass]
public class StringFormattingTests
{
    [TestMethod]
    public void General()
    {
        Assert.AreEqual("12000", ((BigDecimal)12000m).ToString());
        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString());
        Assert.AreEqual("-12000.123", ((BigDecimal)(-12000.123m)).ToString());

        Assert.AreEqual("0.00123", ((BigDecimal)0.00123m).ToString());
        Assert.AreEqual("-0.00123", ((BigDecimal)(-0.00123m)).ToString());

        Assert.AreEqual("120000000000000", ((BigDecimal)120000000000000m).ToString());
    }

    [TestMethod]
    public void GeneralWithPrecision()
    {
        Assert.AreEqual("12000", ((BigDecimal)12000m).ToString("G2"));
        Assert.AreEqual("1.2E+07", ((BigDecimal)12000000.123m).ToString("G2"));

        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString("G8"));
        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString("G10"));

        Assert.AreEqual("12000.123", ((BigDecimal)12000.12345678901234m).ToString("G8"));
        Assert.AreEqual("12000.12346", ((BigDecimal)12000.12345678901234m).ToString("G10"));

        Assert.AreEqual("1.2E+10", ((BigDecimal)12000000000.12345m).ToString("G8"));
        Assert.AreEqual("1.2E+10", ((BigDecimal)12000000000.12345m).ToString("G10"));

        Assert.AreEqual("0.00123", ((BigDecimal)0.00123m).ToString("G3"));
        Assert.AreEqual("-0.00123", ((BigDecimal)(-0.00123m)).ToString("G3"));

        Assert.AreEqual("0.001", ((BigDecimal)0.00123m).ToString("G1"));
        Assert.AreEqual("-0.001", ((BigDecimal)(-0.00123m)).ToString("G1"));

        Assert.AreEqual("1.23E-08", ((BigDecimal)0.0000000123m).ToString("G3"));
        Assert.AreEqual("-1E-08", ((BigDecimal)(-0.00000001m)).ToString("G1"));
    }

    [TestMethod]
    public void Currency()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Assert.AreEqual("¤12,000.00", ((BigDecimal)12000m).ToString("C"));
        Assert.AreEqual("¤0.12", ((BigDecimal)0.123456m).ToString("C"));

        Assert.AreEqual("¤12,000.0000", ((BigDecimal)12000m).ToString("C4"));

        Assert.AreEqual("(¤12,000.00)", ((BigDecimal)(-12000m)).ToString("C"));
        Assert.AreEqual("(¤0.12)", ((BigDecimal)(-0.123456m)).ToString("C"));

        Assert.AreEqual("(¤12,000.0000)", ((BigDecimal)(-12000m)).ToString("C4"));

        Assert.AreEqual("¤12,000", ((BigDecimal)12000m).ToString("C0"));
        Assert.AreEqual("(¤12,000)", ((BigDecimal)(-12000m)).ToString("C0"));
    }

    [TestMethod]
    public void Percent()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Assert.AreEqual("50.00 %", ((BigDecimal)0.50m).ToString("P"));
        Assert.AreEqual("-50 %", ((BigDecimal)(-0.50m)).ToString("P0"));

        Assert.AreEqual("12,000.00 %", ((BigDecimal)120m).ToString("P"));
        Assert.AreEqual("0.12 %", ((BigDecimal)0.00123456m).ToString("P"));

        Assert.AreEqual("12,000.0000 %", ((BigDecimal)120m).ToString("P4"));

        Assert.AreEqual("-12,000.00 %", ((BigDecimal)(-120m)).ToString("P"));
        Assert.AreEqual("-0.12 %", ((BigDecimal)(-0.00123456m)).ToString("P"));

        Assert.AreEqual("-12,000.0000 %", ((BigDecimal)(-120m)).ToString("P4"));

        Assert.AreEqual("12,000 %", ((BigDecimal)120m).ToString("P0"));
        Assert.AreEqual("-12,000 %", ((BigDecimal)(-120m)).ToString("P0"));
    }

    [TestMethod]
    public void FixedPoint()
    {
        Assert.AreEqual("1.00", ((BigDecimal)1m).ToString("F"));
        Assert.AreEqual("1.0000", ((BigDecimal)1m).ToString("F4"));

        Assert.AreEqual("0.1", ((BigDecimal)0.05m).ToString("F1"));
        Assert.AreEqual("0.05", ((BigDecimal)0.05m).ToString("F2"));

        Assert.AreEqual("-1000", ((BigDecimal)(-1000.05m)).ToString("F0"));
        Assert.AreEqual("-1000.0500", ((BigDecimal)(-1000.05m)).ToString("F4"));
    }

    [TestMethod]
    public void Number()
    {
        Assert.AreEqual("1.00", ((BigDecimal)1m).ToString("N"));
        Assert.AreEqual("1.0000", ((BigDecimal)1m).ToString("N4"));

        Assert.AreEqual("0.1", ((BigDecimal)0.05m).ToString("N1"));
        Assert.AreEqual("0.05", ((BigDecimal)0.05m).ToString("N2"));

        Assert.AreEqual("-1,000", ((BigDecimal)(-1000.05m)).ToString("N0"));
        Assert.AreEqual("-1,000.0500", ((BigDecimal)(-1000.05m)).ToString("N4"));
    }

    [TestMethod]
    public void GeneralLowercase()
    {
        var inv = CultureInfo.InvariantCulture;

        Assert.AreEqual("1.2e+07", ((BigDecimal)12000000.123m).ToString("g2", inv));
        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString("g", inv));
        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString("g8", inv));
        Assert.AreEqual("-1e-08", ((BigDecimal)(-0.00000001m)).ToString("g1", inv));
    }

    [TestMethod]
    public void GeneralRounding()
    {
        var inv = CultureInfo.InvariantCulture;

        Assert.AreEqual("1000", ((BigDecimal)999.995m).ToString("G2", inv));
        Assert.AreEqual("100", ((BigDecimal)99.95m).ToString("G1", inv));
        Assert.AreEqual("0.13", ((BigDecimal)0.125m).ToString("G2", inv));
        Assert.AreEqual("-0.13", ((BigDecimal)(-0.125m)).ToString("G2", inv));
        Assert.AreEqual("0.0005", ((BigDecimal)0.0005m).ToString("G3", inv));
        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString("G0", inv));
        Assert.AreEqual("12000.123", ((BigDecimal)12000.123m).ToString("G100", inv));
        Assert.AreEqual("1E+06", ((BigDecimal)1000000m).ToString("G10", inv));
        Assert.AreEqual("100000", ((BigDecimal)100000m).ToString("G10", inv));
        Assert.AreEqual("1E-06", ((BigDecimal)0.000001m).ToString("G10", inv));
        Assert.AreEqual("0.00001", ((BigDecimal)0.00001m).ToString("G10", inv));
        Assert.AreEqual("1.23E+10", BigDecimal.Parse("12345678901.5", inv).ToString("G3", inv));
        Assert.AreEqual("0", BigDecimal.Zero.ToString("G5", inv));
        Assert.AreEqual("0", BigDecimal.Zero.ToString("G", inv));
    }

    [TestMethod]
    public void Exponential()
    {
        var inv = CultureInfo.InvariantCulture;

        Assert.AreEqual("0.000000E+000", BigDecimal.Zero.ToString("E", inv));
        Assert.AreEqual("1.000000E+000", BigDecimal.One.ToString("E", inv));
        Assert.AreEqual("-1.000000E+000", BigDecimal.MinusOne.ToString("E", inv));
        Assert.AreEqual("1.500000E+000", ((BigDecimal)1.5m).ToString("E", inv));
        Assert.AreEqual("1.000000E+001", ((BigDecimal)10m).ToString("E", inv));
        Assert.AreEqual("5.000000E-001", ((BigDecimal)0.5m).ToString("E", inv));
        Assert.AreEqual("1.234567E+006", ((BigDecimal)1234567m).ToString("E", inv));
        Assert.AreEqual("1.234568E+007", ((BigDecimal)12345678m).ToString("E", inv));
        Assert.AreEqual("-1.234568E+007", ((BigDecimal)(-12345678m)).ToString("E", inv));
        Assert.AreEqual("1.230000E-003", ((BigDecimal)0.00123m).ToString("E", inv));
        Assert.AreEqual("1.500000e+000", ((BigDecimal)1.5m).ToString("e", inv));
        Assert.AreEqual("0e+000", BigDecimal.Zero.ToString("e0", inv));

        Assert.AreEqual("2E+000", ((BigDecimal)1.5m).ToString("E0", inv));
        Assert.AreEqual("1E+001", ((BigDecimal)9.5m).ToString("E0", inv));
        Assert.AreEqual("1.0E+003", ((BigDecimal)999.995m).ToString("E1", inv));
        Assert.AreEqual("-1.4E-001", ((BigDecimal)(-0.135m)).ToString("E1", inv));
        Assert.AreEqual("1.2345000000E+004", ((BigDecimal)12345m).ToString("E10", inv));

        Assert.AreEqual("1.000000E+030", BigDecimal.Parse("1E+30", NumberStyles.Float, inv).ToString("E", inv));
        Assert.AreEqual("1.000000E-030", BigDecimal.Parse("1E-30", NumberStyles.Float, inv).ToString("E", inv));
        Assert.AreEqual("1.000000E+1000", BigDecimal.Parse("1E+1000", NumberStyles.Float, inv).ToString("E", inv));
        Assert.AreEqual("-1.000000E-1000", BigDecimal.Parse("-1E-1000", NumberStyles.Float, inv).ToString("E", inv));
        Assert.AreEqual("9.99E+099", BigDecimal.Parse("9.99E+99", NumberStyles.Float, inv).ToString("E2", inv));
        Assert.AreEqual("1.0E+100", BigDecimal.Parse("9.99E+99", NumberStyles.Float, inv).ToString("E1", inv));
    }

    [TestMethod]
    public void RoundTrip()
    {
        var inv = CultureInfo.InvariantCulture;

        Assert.AreEqual("0", BigDecimal.Zero.ToString("R", inv));
        Assert.AreEqual("123E-2", ((BigDecimal)1.23m).ToString("R", inv));
        Assert.AreEqual("-123E-2", ((BigDecimal)(-1.23m)).ToString("r", inv));
        Assert.AreEqual("1E1", ((BigDecimal)10m).ToString("R", inv));
        Assert.AreEqual("-5", ((BigDecimal)(-5m)).ToString("R", inv));
        Assert.AreEqual("1E-30", BigDecimal.Parse("1E-30", NumberStyles.Float, inv).ToString("R", inv));

        // Always invariant regardless of the format provider:
        Assert.AreEqual("-123E-2", ((BigDecimal)(-1.23m)).ToString("R", FormattingTestData.Weird));

        foreach (string s in FormattingTestData.AllValues)
        {
            var value = FormattingTestData.ParseValue(s);
            Assert.AreEqual(value, BigDecimal.Parse(value.ToString("R", FormattingTestData.Euro), NumberStyles.Float, inv), $"value: {s}");
        }
    }

    [TestMethod]
    public void FormatStringParsing()
    {
        var inv = CultureInfo.InvariantCulture;
        var value = (BigDecimal)1.5m;

        Assert.AreEqual("1.5", value.ToString(null, inv));
        Assert.AreEqual("1.5", value.ToString(string.Empty, inv));
        Assert.AreEqual("1.5", value.ToString("   ", inv));
        Assert.AreEqual("1.50", value.ToString(" f2 ", inv));
        Assert.AreEqual("1.50", value.ToString("F2", inv));
        Assert.AreEqual("1.50", value.ToString("F02", inv));
        Assert.AreEqual("2", value.ToString("n0", inv));
        Assert.AreEqual("¤1.50", value.ToString("c", inv));
        Assert.AreEqual("150.00 %", value.ToString("p", inv));
        Assert.AreEqual("15E-1", value.ToString("r", inv));

        Assert.ThrowsException<FormatException>(() => value.ToString("X", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("F 2", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("G-1", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("F2abc", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("Gx", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("G99999999999", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("E+1", inv));
        Assert.ThrowsException<FormatException>(() => value.ToString("F2.0", inv));
    }

    [TestMethod]
    public void NegativeZeroWholePart()
    {
        var inv = CultureInfo.InvariantCulture;

        Assert.AreEqual("-0.5", ((BigDecimal)(-0.5m)).ToString("F1", inv));
        Assert.AreEqual("-0.05", ((BigDecimal)(-0.05m)).ToString("F2", inv));
        Assert.AreEqual("-0.1", ((BigDecimal)(-0.05m)).ToString("F1", inv));
        Assert.AreEqual("-1", ((BigDecimal)(-0.5m)).ToString("F0", inv));
        Assert.AreEqual("0.00", ((BigDecimal)(-0.001m)).ToString("F2", inv));
        Assert.AreEqual("0", ((BigDecimal)(-0.4m)).ToString("F0", inv));
        Assert.AreEqual("(¤0.05)", ((BigDecimal)(-0.05m)).ToString("C", inv));
        Assert.AreEqual("¤0.00", ((BigDecimal)(-0.0005m)).ToString("C", inv));
        Assert.AreEqual("-0.05 %", ((BigDecimal)(-0.0005m)).ToString("P", inv));
        Assert.AreEqual("-0.5", ((BigDecimal)(-0.5m)).ToString("N1", inv));
        Assert.AreEqual("-0.5", ((BigDecimal)(-0.5m)).ToString("G", inv));
        Assert.AreEqual("-0.5", ((BigDecimal)(-0.5m)).ToString("G1", inv));

        Assert.AreEqual("(0<d>5000)", ((BigDecimal)(-0.5m)).ToString("N", FormattingTestData.Weird));
        Assert.AreEqual("(USD 0<cd>500)", ((BigDecimal)(-0.5m)).ToString("C", FormattingTestData.Weird));
        Assert.AreEqual("0.5 -", ((BigDecimal)(-0.5m)).ToString("N1", FormattingTestData.Trailing));
        Assert.AreEqual("(0.5 $)", ((BigDecimal)(-0.5m)).ToString("C1", FormattingTestData.Trailing));
    }

    [TestMethod]
    public void CustomFormatInfo()
    {
        var value = FormattingTestData.ParseValue("-1234.5678");
        var euro = FormattingTestData.Euro;

        Assert.AreEqual("−1234,5678", value.ToString("G", euro));
        Assert.AreEqual("−1234,57", value.ToString("F", euro));
        Assert.AreEqual("−1.234,57", value.ToString("N", euro));
        Assert.AreEqual("−1.234,57 €", value.ToString("C", euro));
        Assert.AreEqual("−123.456,78 %", value.ToString("P", euro));
        Assert.AreEqual("−1,234568E+003", value.ToString("E", euro));
        Assert.AreEqual("−1200", value.ToString("G2", euro));
        Assert.AreEqual("−1,2E+07", FormattingTestData.ParseValue("-12345678.9").ToString("G2", euro));
        Assert.AreEqual("−0,00012", FormattingTestData.ParseValue("-0.000123").ToString("G2", euro));
        Assert.AreEqual("−1,2E−08", FormattingTestData.ParseValue("-0.0000000123").ToString("G2", euro));
        Assert.AreEqual("1,230000E−004", FormattingTestData.ParseValue("0.000123").ToString("E", euro));

        value = FormattingTestData.ParseValue("-1234567.891");
        var weird = FormattingTestData.Weird;

        Assert.AreEqual("neg1234567<d>891", value.ToString("G", weird));
        Assert.AreEqual("neg1234567<d>8910", value.ToString("F", weird));
        Assert.AreEqual("(12<g>34<g>567<d>8910)", value.ToString("N", weird));
        Assert.AreEqual("(USD 12<cg>345<cg>67<cd>891)", value.ToString("C", weird));
        Assert.AreEqual("pct1<pg>2<pg>3<pg>4<pg>5<pg>6<pg>7<pg>8<pg>9<pd>1neg", value.ToString("P", weird));
        Assert.AreEqual("neg1<d>234568Epos006", value.ToString("E", weird));
        Assert.AreEqual("neg1200000", value.ToString("G2", weird));
        Assert.AreEqual("neg1<d>2Epos07", FormattingTestData.ParseValue("-12345678.9").ToString("G2", weird));
        Assert.AreEqual("USD 12<cg>35", FormattingTestData.ParseValue("1234.567").ToString("C0", weird));
        Assert.AreEqual("pct1<pg>2<pg>3<pg>4<pg>5<pg>6<pd>7", FormattingTestData.ParseValue("1234.567").ToString("P", weird));

        var trailing = FormattingTestData.Trailing;

        Assert.AreEqual("-1234567.891", value.ToString("G", trailing));
        Assert.AreEqual("-1234568", value.ToString("F", trailing));
        Assert.AreEqual("12345,68 -", value.ToString("N", trailing));
        Assert.AreEqual("(123,4568 $)", value.ToString("C", trailing));
        Assert.AreEqual("123456,789- %", value.ToString("P", trailing));
        Assert.AreEqual("-1.234568E+006", value.ToString("E", trailing));
        Assert.AreEqual("123,4567.89$", FormattingTestData.ParseValue("1234567.891").ToString("C2", trailing));
        Assert.AreEqual("% 123456,789", FormattingTestData.ParseValue("1234567.891").ToString("P", trailing));
    }
}