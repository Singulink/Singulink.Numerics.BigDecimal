using System;
using System.Runtime.InteropServices;

namespace Singulink.Numerics;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DecimalData
{
    private const int SignMask = unchecked((int)0x80000000);
    private const int ScaleMask = 0x00FF0000;
    private const int ScaleShift = 16;

    public int Flags { get; }

    public uint Hi { get; }

    public ulong Lo { get; }

    public int Scale => unchecked((byte)(Flags >> ScaleShift));

    public bool IsPositive => (Flags & SignMask) is 0;

    public DecimalData(int flags, uint hi, ulong lo)
    {
        if (!IsValid(flags))
            throw new ArgumentException("Invalid decimal flag data.", nameof(flags));

        Flags = flags;
        Hi = hi;
        Lo = lo;
    }

    public static bool IsValid(int flags) => unchecked((flags & ~(SignMask | ScaleMask)) is 0 && ((uint)(flags & ScaleMask) <= (28 << ScaleShift)));
}