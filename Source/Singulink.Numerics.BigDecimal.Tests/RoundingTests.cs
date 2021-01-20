﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests
{
    [TestClass]
    public class RoundingTests
    {
        [TestMethod]
        public void RoundLower()
        {
            Assert.AreEqual(1234m, BigDecimal.Round(1234.1m, 0));
            Assert.AreEqual(-1234m, BigDecimal.Round(-1234.1m, 0));

            Assert.AreEqual(1234.567m, BigDecimal.Round(1234.5671m, 3));
            Assert.AreEqual(-1234.567m, BigDecimal.Round(-1234.5671m, 3));

            Assert.AreEqual(1230, BigDecimal.RoundToPrecision(1234.456m, 3));
            Assert.AreEqual(-1230, BigDecimal.RoundToPrecision(-1234.456m, 3));
        }

        [TestMethod]
        public void RoundHigher()
        {
            Assert.AreEqual(1235m, BigDecimal.Round(1234.7m, 0));
            Assert.AreEqual(-1235m, BigDecimal.Round(-1234.7m, 0));

            Assert.AreEqual(1234.567m, BigDecimal.Round(1234.5677m, 3));
            Assert.AreEqual(-1234.567m, BigDecimal.Round(-1234.5677m, 3));

            Assert.AreEqual(1240, BigDecimal.RoundToPrecision(1236.456m, 3));
            Assert.AreEqual(-1240, BigDecimal.RoundToPrecision(-1236.456m, 3));
        }

        [TestMethod]
        public void MidPointAwayFromZero()
        {
            Assert.AreEqual(1235m, BigDecimal.Round(1234.5m, 0, MidpointRounding.AwayFromZero));
            Assert.AreEqual(1235m, BigDecimal.Round(1234.5675m, 0, MidpointRounding.AwayFromZero));
        }
    }
}
