namespace Harness.Wire;

/// <summary>
/// Which of a 32-bit value's two halves occupies the lower holding register.
///
/// <para><b>THIS IS A CONFIGURABLE TRANSFORM BECAUSE THE SPEC REQUIRES IT TO BE, AND IT IS
/// UNCALIBRATED.</b> §11: "Because we author both ends the convention is ours and a round-trip proves
/// the halves agree. ONE RESIDUAL: <c>MB_SERVER</c>'s own byte-to-register presentation is Siemens'
/// behaviour, which no fake we write can validate. Build swap as a configurable transform and CALIBRATE
/// with a known bit pattern — one measurement, once."</para>
///
/// <para><b>So the default below is an inference, not a measurement.</b> A round trip through our own
/// code proves only that our two halves agree with each other — the very shape of check this project
/// exists to distrust. The one thing that settles it is a known pattern written to the version register
/// and read back off the device, and until that happens <see cref="VersionCheck"/> reports a mismatch
/// that WOULD have matched under the other order as a distinct outcome, rather than as a failed
/// download.</para>
/// </summary>
public enum RegisterWordOrder
{
    /// <summary>Most significant word in the lower-numbered register. The S7 <c>%MD</c> convention.</summary>
    HighWordFirst,

    /// <summary>Least significant word in the lower-numbered register.</summary>
    LowWordFirst,
}

/// <summary>Assembling and splitting 32-bit values across a register pair.</summary>
public static class RegisterWords
{
    /// <summary>Combine a register pair into a 32-bit value.</summary>
    public static uint To32(ushort first, ushort second, RegisterWordOrder order) => order switch
    {
        RegisterWordOrder.HighWordFirst => ((uint)first << 16) | second,
        RegisterWordOrder.LowWordFirst => ((uint)second << 16) | first,
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "unknown word order."),
    };

    /// <summary>Split a 32-bit value into the register pair that carries it.</summary>
    public static ushort[] From32(uint value, RegisterWordOrder order) => order switch
    {
        RegisterWordOrder.HighWordFirst => new[] { (ushort)(value >> 16), (ushort)(value & 0xFFFF) },
        RegisterWordOrder.LowWordFirst => new[] { (ushort)(value & 0xFFFF), (ushort)(value >> 16) },
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "unknown word order."),
    };

    /// <summary>The same pair read under the other order — what a calibration error would have produced.</summary>
    public static uint Swapped(uint value) => (value >> 16) | (value << 16);
}
