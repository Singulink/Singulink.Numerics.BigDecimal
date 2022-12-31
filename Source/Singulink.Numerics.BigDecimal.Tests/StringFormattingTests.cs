using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests;

[TestClass]
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
        Assert.AreEqual("1.2E+7", ((BigDecimal)12000000.123m).ToString("G2"));

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

        Assert.AreEqual("1.23E-8", ((BigDecimal)0.0000000123m).ToString("G3"));
        Assert.AreEqual("-1E-8", ((BigDecimal)(-0.00000001m)).ToString("G1"));
    }

    [TestMethod]
    public void Currency()
    {
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
}