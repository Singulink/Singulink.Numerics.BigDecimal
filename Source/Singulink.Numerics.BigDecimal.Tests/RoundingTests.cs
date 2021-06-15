using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests
{
    [TestClass]
    public class RoundingTests
    {
        [TestMethod]
        public void RoundUnderHalf()
        {
            Assert.AreEqual(1234m, BigDecimal.Round(1234.1m, 0));
            Assert.AreEqual(-1234m, BigDecimal.Round(-1234.1m, 0));

            Assert.AreEqual(1234.567m, BigDecimal.Round(1234.5671m, 3));
            Assert.AreEqual(-1234.567m, BigDecimal.Round(-1234.5671m, 3));

            Assert.AreEqual(1230m, BigDecimal.RoundToPrecision(1234.456m, 3));
            Assert.AreEqual(-1230m, BigDecimal.RoundToPrecision(-1234.456m, 3));
        }

        [TestMethod]
        public void RoundOverHalf()
        {
            Assert.AreEqual(1235m, BigDecimal.Round(1234.7m, 0));
            Assert.AreEqual(-1235m, BigDecimal.Round(-1234.7m, 0));

            Assert.AreEqual(1234.568m, BigDecimal.Round(1234.5678m, 3));
            Assert.AreEqual(-1234.568m, BigDecimal.Round(-1234.5678m, 3));

            Assert.AreEqual(1240m, BigDecimal.RoundToPrecision(1236.456m, 3));
            Assert.AreEqual(-1240m, BigDecimal.RoundToPrecision(-1236.456m, 3));
        }

        [TestMethod]
        public void MidpointAwayFromZero()
        {
            Assert.AreEqual(1235m, BigDecimal.Round(1234.5m, 0, RoundingMode.MidpointAwayFromZero));
            Assert.AreEqual(1235m, BigDecimal.Round(1234.5675m, 0, RoundingMode.MidpointAwayFromZero));
        }

        [TestMethod]
        public void NegativeDecimals()
        {
            Assert.AreEqual(0m, BigDecimal.Round(500m, -3));
            Assert.AreEqual(-1000m, BigDecimal.Round(-500m, -3, RoundingMode.MidpointAwayFromZero));
        }

        [TestMethod]
        public void RoundingToNonExistentDigit()
        {
            // Ensures that rounding to a digit that doesn't exist in the mantissa works, i.e. 0.5 => 1

            Assert.AreEqual(0m, BigDecimal.Round(0.5m, 0));
            Assert.AreEqual(1m, BigDecimal.Round(0.5m, 0, RoundingMode.MidpointAwayFromZero));
            Assert.AreEqual(1m, BigDecimal.Round(0.6m, 0));
            Assert.AreEqual(0m, BigDecimal.Round(0.4m, 0));
            Assert.AreEqual(0m, BigDecimal.Round(0.05m, 0));
            Assert.AreEqual(0m, BigDecimal.Round(0.00005m, 0));
        }
    }
}
