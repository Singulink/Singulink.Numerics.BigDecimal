using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests
{
    [TestClass]
    public class AssociativeTests
    {
        [TestMethod]
        public void AddAndSubtract()
        {
            // Arbitrary order of the same addition operations and subtraction operations should always exactly represent the resulting value

            double[] values = new double[] { 0.001, 0.1, 0.00002, 10000000, 0.1, 10000000000 };

            var valueList = Enumerable.Range(0, 500).SelectMany(i => values).ToList();

            BigDecimal total = 0;

            foreach (double v in values)
                total += v;

            BigDecimal zeroTest1 = total;
            BigDecimal zeroTest2 = total;

            foreach (double v in values)
                zeroTest1 -= v;

            foreach (double v in values.Reverse())
                zeroTest2 -= v;

            Assert.AreEqual(BigDecimal.Zero, zeroTest1);
            Assert.AreEqual(BigDecimal.Zero, zeroTest2);
        }

        [TestMethod]
        public void AddAndSubtractDouble()
        {
            // Comparison with double to ensure the test above is a valid case that would fail for a double.

            double[] values = new double[] { 0.001, 0.1, 0.00002, 10000000, 0.1, 10000000000 };

            var valueList = Enumerable.Range(0, 500).SelectMany(i => values).ToList();

            double total = 0;

            foreach (double v in values)
                total += v;

            double zeroTest1 = total;
            double zeroTest2 = total;

            foreach (double v in values)
                zeroTest1 -= v;

            foreach (double v in values.Reverse())
                zeroTest2 -= v;

            Assert.AreEqual(0, zeroTest1);
            Assert.IsTrue(zeroTest2 < 0.00001);
            Assert.AreNotEqual(0, zeroTest2);
        }
    }
}