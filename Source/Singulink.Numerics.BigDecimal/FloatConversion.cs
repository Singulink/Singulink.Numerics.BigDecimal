using System;
using System.Collections.Generic;
using System.Text;

namespace Singulink.Numerics;

/// <summary>
/// Specifies floating-point conversion modes.
/// </summary>
public enum FloatConversion
{
    /// <summary>
    /// Indicates that the floating-point value's maximum number of significant digits should be used, i.e. 9 for <see cref="float"/> and 17 for <see
    /// cref="double"/>. This mode is fast and results in a value that can be round-tripped back to the exact same floating-point value (i.e. the
    /// floating-point values will compare as equal) but may contain a couple extra "junk" digits.
    /// </summary>
    Roundtrip,

    /// <summary>
    /// Indicates that the floating-point value's minimum number of significant digits should be used, i.e. 7 for <see cref="float"/> and 15 for <see
    /// cref="double"/>. This matches the conversion behavior of floating-point types to <see cref="decimal"/> values. This mode is fast and produces a
    /// result that does not contain any extra "junk" digits but does not round-trip properly for all floating-point values and may lose a small amount of
    /// precision for some values.
    /// </summary>
    Truncate,

    /// <summary>
    /// Indicates that the floating-point value should be converted to its exact value. This mode is fast but can result in a large number of digits being
    /// produced beyond the significant digits or digits required for proper round-tripping of the floating-point value.
    /// </summary>
    Exact,

    /// <summary>
    /// Indicates that the floating-point value's <c>ToString()</c> implementation should be used to produce the digits. This is the slowest conversion
    /// mode and does not round-trip properly for all floating-point values.
    /// </summary>
    ParseString,
}