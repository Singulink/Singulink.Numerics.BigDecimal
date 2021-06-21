using System;
using System.Diagnostics;
using System.Linq;

namespace Singulink.Numerics
{
    internal sealed class SharedPrecisionHolder
    {
        private static readonly SharedPrecisionHolder[] _cache = new SharedPrecisionHolder[] { null! }
            .Concat(Enumerable.Range(1, 200)
                .Select(i => new SharedPrecisionHolder(i)))
            .ToArray();

        private int _value;

        /// <summary>
        /// Gets a cached or new precision holder with the given value.
        /// </summary>
        public static SharedPrecisionHolder Get(int value)
        {
            Debug.Assert(value >= 0, "invalid value");

            return value < _cache.Length ? _cache[value] : new SharedPrecisionHolder(value);
        }

        public static SharedPrecisionHolder Create() => new SharedPrecisionHolder();

        private SharedPrecisionHolder()
        {
        }

        private SharedPrecisionHolder(int value)
        {
            Debug.Assert(value > 0, "specified value should be greater than 0");
            _value = value;
        }

        public int Value {
            get => _value;
            set {
                // Better to leave this as a runtime check for safety as very bad things can happen if this is not honored
                if (_value != 0)
                    throw new InvalidOperationException("Shared value has already been set.");

                Debug.Assert(value > 0, "specified value should be greater than 0");
                _value = value;
            }
        }
    }
}