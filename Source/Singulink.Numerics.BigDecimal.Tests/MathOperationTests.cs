﻿namespace Singulink.Numerics.Tests;

[PrefixTestClass]
public class MathOperationTests
{
    [TestMethod]
    public void Addition()
    {
        Assert.AreEqual(2000m, (BigDecimal)1800 + 200);
    }

    [TestMethod]
    public void Subtraction()
    {
        Assert.AreEqual(0.123m, (BigDecimal)12000.123m - 12000m);
    }

    [TestMethod]
    public void ExactDivision()
    {
        Assert.AreEqual(1m, BigDecimal.DivideExact(1, 1));
        Assert.AreEqual(0.1m, BigDecimal.DivideExact(1, 10));
        Assert.AreEqual(0.01m, BigDecimal.DivideExact(1, 100));
        Assert.AreEqual(0.001m, BigDecimal.DivideExact(1, 1000));

        Assert.AreEqual(-0.001m, BigDecimal.DivideExact(1, -1000));
        Assert.AreEqual(-0.001m, BigDecimal.DivideExact(-1, 1000));

        Assert.AreEqual(-0.001m, BigDecimal.DivideExact(1, -1000));
        Assert.AreEqual(-0.001m, BigDecimal.DivideExact(-1, 1000));
    }

    [TestMethod]
    public void FailedExactDivision()
    {
        Assert.AreEqual(false, BigDecimal.TryDivideExact(2, 3, out _));
        Assert.AreEqual(false, BigDecimal.TryDivideExact(-1000000000000, 3, out _));
    }

    [TestMethod]
    public void ExtendedDivision()
    {
        Assert.AreEqual(0.33333m, BigDecimal.Divide(1, 3, 5));
        Assert.AreEqual(0.66667m, BigDecimal.Divide(2, 3, 5));

        Assert.AreEqual(0.33333m, BigDecimal.Divide(1, 3, 5, RoundingMode.MidpointToPositiveInfinity));
        Assert.AreEqual(0.33334m, BigDecimal.Divide(1, 3, 5, RoundingMode.ToPositiveInfinity));

        Assert.AreEqual(0.66667m, BigDecimal.Divide(2, 3, 5, RoundingMode.MidpointToZero));
        Assert.AreEqual(0.66666m, BigDecimal.Divide(2, 3, 5, RoundingMode.ToZero));
    }

    [TestMethod]
    public void RoundedDivision()
    {
        Assert.AreEqual(3.33333m, BigDecimal.DivideRounded(10, 3, 5));
        Assert.AreEqual(6.66667m, BigDecimal.DivideRounded(20, 3, 5));

        Assert.AreEqual(3.33333m, BigDecimal.DivideRounded(10, 3, 5, RoundingMode.MidpointToPositiveInfinity));
        Assert.AreEqual(3.33334m, BigDecimal.DivideRounded(10, 3, 5, RoundingMode.ToPositiveInfinity));

        Assert.AreEqual(6.66667m, BigDecimal.DivideRounded(20, 3, 5, RoundingMode.MidpointToZero));
        Assert.AreEqual(6.66666m, BigDecimal.DivideRounded(20, 3, 5, RoundingMode.ToZero));

        Assert.AreEqual(20m, BigDecimal.DivideRounded(25, 1, -1, RoundingMode.MidpointToEven));
        Assert.AreEqual(40m, BigDecimal.DivideRounded(35, 1, -1, RoundingMode.MidpointToEven));

        Assert.AreEqual(1m, BigDecimal.DivideRounded(1, 3, 0, RoundingMode.ToPositiveInfinity));
        Assert.AreEqual(0m, BigDecimal.DivideRounded(1, 3, 0, RoundingMode.ToNegativeInfinity));

        Assert.AreEqual(2m, BigDecimal.DivideRounded(5, 2, 0, RoundingMode.MidpointToEven));
        Assert.AreEqual(8m, BigDecimal.DivideRounded(15, 2, 0, RoundingMode.MidpointToEven));

        Assert.AreEqual(25m, BigDecimal.DivideRounded(100, 4, 2));
        Assert.AreEqual(0.5m, BigDecimal.DivideRounded(1, 2, 3));

        Assert.AreEqual(0.67m, BigDecimal.DivideRounded(2, 3, 2));

        Assert.AreEqual(12.34m, BigDecimal.DivideRounded(123.45m, 10m, 2));

        Assert.AreEqual(1.2m, BigDecimal.DivideRounded(123.45m, 100m, 1));

        Assert.AreEqual(0.125m, BigDecimal.DivideRounded(1, 8, 5));
    }

    [TestMethod]
    public void Pow()
    {
        Assert.AreEqual(1m, BigDecimal.Pow(1234, 0));
        Assert.AreEqual(0.0009765625m, BigDecimal.Pow(0.5m, 10));
        Assert.AreEqual(-216000000m, BigDecimal.Pow(-600, 3));
    }

    [TestMethod]
    public void Pow10()
    {
        Assert.AreEqual(1m, BigDecimal.Pow10(0));
        Assert.AreEqual(100000m, BigDecimal.Pow10(5));
        Assert.AreEqual(0.00001m, BigDecimal.Pow10(-5));
    }
}