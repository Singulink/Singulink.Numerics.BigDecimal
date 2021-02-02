using System;
using System.Diagnostics;

namespace Singulink.Numerics
{
    internal sealed class SharedPrecisionHolder
    {
        private int _value;

        public static SharedPrecisionHolder One { get; } = new SharedPrecisionHolder(1);

        /// <summary>
        /// Gets a cached or new precision holder with the given value.
        /// </summary>
        public static SharedPrecisionHolder Get(int value)
        {
            return value == 1 ? One : new SharedPrecisionHolder(value);
        }

        public SharedPrecisionHolder()
        {
        }

        public SharedPrecisionHolder(int value)
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