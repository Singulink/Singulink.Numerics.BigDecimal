﻿using System;

namespace Singulink.Numerics.Utilities;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

internal static class StringHelper
{
    public static unsafe string Concat(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2, ReadOnlySpan<char> s3)
    {
#if NETSTANDARD
        return s1.ToString() + (s2.ToString() + s3.ToString());
#else
        var p1 = &s1;
        var p2 = &s2;
        var p3 = &s3;

        var data = (p1: (nint)p1, p2: (nint)p2, p3: (nint)p3);

        return string.Create(s1.Length + s2.Length + s3.Length, data, (span, data) => {
            var s1 = *(ReadOnlySpan<char>*)data.p1;
            var s2 = *(ReadOnlySpan<char>*)data.p2;
            var s3 = *(ReadOnlySpan<char>*)data.p3;

            s1.CopyTo(span);
            s2.CopyTo(span = span[s1.Length..]);
            s3.CopyTo(span[s2.Length..]);
        });
#endif
    }
}