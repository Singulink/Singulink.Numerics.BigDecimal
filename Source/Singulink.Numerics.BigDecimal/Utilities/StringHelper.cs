using System;

namespace Singulink.Numerics.Utilities;

internal static class StringHelper
{
    public static unsafe string Concat(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2)
    {
#if NETSTANDARD2_0
        return s1.ToString() + s2.ToString();
#else
        fixed (char* p1 = s1)
        fixed (char* p2 = s2) {
            var data = (p1: (IntPtr)p1, p2: (IntPtr)p2, length1: s1.Length, length2: s2.Length);

            return string.Create(s1.Length + s2.Length, data, (span, data) =>
            {
                new ReadOnlySpan<char>((char*)data.p1, data.length1).CopyTo(span);
                new ReadOnlySpan<char>((char*)data.p2, data.length2).CopyTo(span.Slice(data.length1));
            });
        }
#endif
    }

    public static unsafe string Concat(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2, ReadOnlySpan<char> s3)
    {
#if NETSTANDARD2_0
        return s1.ToString() + (s2.ToString() + s3.ToString());
#else
        fixed (char* p1 = s1)
        fixed (char* p2 = s2)
        fixed (char* p3 = s3) {
            var data = (p1: (IntPtr)p1, p2: (IntPtr)p2, p3: (IntPtr)p3, length1: s1.Length, length2: s2.Length, length3: s3.Length);

            return string.Create(s1.Length + s2.Length + s3.Length, data, (span, data) => {
                new ReadOnlySpan<char>((char*)data.p1, data.length1).CopyTo(span);
                new ReadOnlySpan<char>((char*)data.p2, data.length2).CopyTo(span = span.Slice(data.length1));
                new ReadOnlySpan<char>((char*)data.p3, data.length3).CopyTo(span.Slice(data.length2));
            });
        }
#endif
    }
}