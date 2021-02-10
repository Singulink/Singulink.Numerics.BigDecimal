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
            Assert.AreEqual(2000m, (BigDecimal)1800 + 200);
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

        [TestMethod]
        public void ExtendedDivision()
        {
            Assert.AreEqual(0.33333m, BigDecimal.Divide(1, 3, 5));
            Assert.AreEqual(0.66667m, BigDecimal.Divide(2, 3, 5));
        }
    }
}
