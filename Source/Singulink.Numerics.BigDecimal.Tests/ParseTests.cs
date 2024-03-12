using System.Globalization;

namespace Singulink.Numerics.Tests;

[PrefixTestClass]
public class ParseTests
{
    [TestMethod]
    public void Integers()
    {
        const NumberStyles style = NumberStyles.Integer;

        Assert.AreEqual(12m, BigDecimal.Parse("12", style));
        Assert.AreEqual(12m, BigDecimal.Parse("00012", style));

        Assert.AreEqual(12000m, BigDecimal.Parse("12000", style));
        Assert.AreEqual(12000m, BigDecimal.Parse("0000012000", style));

        Assert.AreEqual(-12m, BigDecimal.Parse("-12", style));
        Assert.AreEqual(-12m, BigDecimal.Parse("-00012", style));

        Assert.AreEqual(-12000m, BigDecimal.Parse("-12000", style));
        Assert.AreEqual(-12000m, BigDecimal.Parse("-0000012000", style));
    }

    [TestMethod]
    public void Exponents()
    {
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowExponent;

        Assert.AreEqual(12000m, BigDecimal.Parse("1.2E+4", style));
        Assert.AreEqual(12000m, BigDecimal.Parse("120000E-1", style));

        Assert.AreEqual(-12000m, BigDecimal.Parse("-120000E-001", style));
        Assert.AreEqual(-0.00000012000m, BigDecimal.Parse("-1.20000E-7", style));
    }
}