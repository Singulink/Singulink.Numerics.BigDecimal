using System;
using System.Diagnostics.CodeAnalysis;
using RuntimeNullables;

namespace Singulink.Numerics.Utilities
{
    [NullChecks(false)]
    internal static class Throw
    {
        [DoesNotReturn]
        public static void ArgumentEx(string message, string paramName) => throw new ArgumentException(message, paramName);

        [DoesNotReturn]
        public static void ArgumentOutOfRangeEx(string paramName) => throw new ArgumentOutOfRangeException(paramName);

        [DoesNotReturn]
        public static T ArgumentOutOfRangeEx<T>(string paramName) => throw new ArgumentOutOfRangeException(paramName);

        [DoesNotReturn]
        public static void ArgumentOutOfRangeEx(string paramName, string? message) => throw new ArgumentOutOfRangeException(paramName, message);

        [DoesNotReturn]
        public static void DivideByZeroEx() => throw new DivideByZeroException();

        [DoesNotReturn]
        public static void ArithmeticEx(string message) => throw new ArithmeticException(message);

        [DoesNotReturn]
        public static void FormatEx(string message) => throw new FormatException(message);

        [DoesNotReturn]
        public static void NotSupportedEx() => throw new NotSupportedException();

        [DoesNotReturn]
        public static T NotSupportedEx<T>(string message) => throw new NotSupportedException(message);
    }
}