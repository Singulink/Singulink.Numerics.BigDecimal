﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests
{
    [TestClass]
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
}
