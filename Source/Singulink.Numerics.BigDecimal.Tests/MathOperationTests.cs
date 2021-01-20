using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests
{
    [TestClass]
    public class MathOperationTests
    {
        [TestMethod]
        public void Addition()
        {
            Assert.AreEqual(1000m, (BigDecimal)800 + 200);
        }

        [TestMethod]
        public void Subtraction()
        {
            Assert.AreEqual(0.123m, (BigDecimal)12000.123m - 12000m);
        }

        [TestMethod]
        public void NonExtendedDivision()
        {
            Assert.AreEqual(1m, BigDecimal.One / BigDecimal.One);
            Assert.AreEqual(0.1m, BigDecimal.One / 10);
            Assert.AreEqual(0.01m, BigDecimal.One / 100);
            Assert.AreEqual(0.001m, BigDecimal.One / 1000);
        }
    }
}
