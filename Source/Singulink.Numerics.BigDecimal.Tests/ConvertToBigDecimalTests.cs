using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Numerics.Tests
{
    [TestClass]
    public class ConvertToBigDecimalTests
    {
        [TestMethod]
        public void FromDoubleExact()
        {
            Assert.AreEqual("0.1000000000000000055511151231257827021181583404541015625", BigDecimal.FromDouble(0.1, FloatConversion.Exact).ToString());
        }

        [TestMethod]
        public void FromSingleExact()
        {
            Assert.AreEqual("0.100000001490116119384765625", BigDecimal.FromSingle(0.1f, FloatConversion.Exact).ToString());
        }

        [TestMethod]
        public void FromDoubleTruncate()
        {
            Assert.AreEqual("0.1", BigDecimal.FromDouble(0.1, FloatConversion.Truncate).ToString());
        }

        [TestMethod]
        public void FromSingleTruncate()
        {
            Assert.AreEqual("0.1", BigDecimal.FromSingle(0.1f, FloatConversion.Truncate).ToString());
        }

        [TestMethod]
        public void FromDoubleParseString()
        {
            Assert.AreEqual("0.1", BigDecimal.FromDouble(0.1, FloatConversion.ParseString).ToString());
        }

        [TestMethod]
        public void FromSingleParseString()
        {
            Assert.AreEqual("0.1", BigDecimal.FromSingle(0.1f, FloatConversion.ParseString).ToString());
        }
    }
}
