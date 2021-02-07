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
            Assert.AreEqual("0.1000000000000000055511151231257827021181583404541015625", BigDecimal.FromDouble(0.1, true).ToString());
        }

        [TestMethod]
        public void FromSingleExact()
        {
            Assert.AreEqual("0.100000001490116119384765625", BigDecimal.FromSingle(0.1f, true).ToString());
        }
    }
}
