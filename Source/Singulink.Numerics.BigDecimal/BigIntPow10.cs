using System.Numerics;

namespace Singulink.Numerics
{
    /// <summary>
    /// Provides a cache of BigInteger powers of 10.
    /// </summary>
    internal static class BigIntPow10
    {
        // Num bytes needed to store a number with x digits = Log(10)/Log(2)/8 * x = 0.415 * x
        // 1000 entries * 500 avg entry digits = max 500k digits in memory * 0.415 = ~208kb max cache memory usage if all numbers are used.
        private const int MaxCachedPower = 1000;

        private static readonly BigInteger[] _cache = new BigInteger[MaxCachedPower + 1];
        private static readonly BigInteger Ten = new BigInteger(10);

        public static BigInteger Get(int exponent)
        {
            BigInteger value;

            if (exponent <= MaxCachedPower) {
                value = _cache[exponent];

                if (!value.IsZero)
                    return value;
            }

            value = BigInteger.Pow(Ten, exponent);

            if (exponent <= MaxCachedPower)
                _cache[exponent] = value;

            return value;
        }
    }
}