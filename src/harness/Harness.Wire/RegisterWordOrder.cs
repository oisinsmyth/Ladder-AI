namespace Harness.Wire;

/// <summary>
/// Which of a 32-bit value's two halves occupies the lower holding register.
///
/// <para><b>THIS IS A CONFIGURABLE TRANSFORM BECAUSE THE SPEC REQUIRES IT TO BE — AND IT IS NOW
/// CALIBRATED.</b> §11 asked for exactly one thing: "Build swap as a configurable transform and
/// CALIBRATE with a known bit pattern — one measurement, once." That has been done, twice.</para>
///
/// <para>✅ <b>MEASURED: HIGH-WORD-FIRST. <see cref="HighWordFirst"/> IS CORRECT, AND IT IS NO LONGER AN
/// INFERENCE.</b> Both measurements are cited rather than one, because a single reading is what the
/// previous claim in this comment effectively was:</para>
/// <list type="number">
/// <item><b>2026-08-13 — a known bit pattern.</b> <c>16#00001111</c> written and read back off the
/// device. The two halves are distinguishable (<c>0000</c> against <c>1111</c>), so the pair identifies
/// the order rather than merely agreeing with itself.</item>
/// <item><b>2026-08-14 — the deployed build stamp.</b> Registers 0–1 decoded against the stamp the
/// coordinator generated, whose two halves are also distinguishable. <b>This is the stronger of the
/// two:</b> the pattern was not chosen for the experiment, the value came from the program actually
/// running, and it was read through the ordinary production path rather than a probe.</item>
/// </list>
///
/// <para><b>What the calibration does NOT retire.</b> The transform stays configurable and
/// <see cref="VersionCheck"/> still reports a halves-swapped match as its own outcome
/// (<c>WordOrderSuspect</c>) rather than as a failed download. That is not leftover caution: the
/// measurement is of THIS rig's <c>MB_SERVER</c> presentation, and the thing §11 flagged — that the
/// presentation is Siemens' behaviour and not ours — is unchanged by measuring one instance of it. A
/// distinct outcome costs nothing and is what stops somebody re-downloading a device that is already
/// correct.</para>
///
/// <para><b>The reasoning that made this an inference is kept, because it is why the measurement was
/// needed at all:</b> a round trip through our own code proves only that our two halves agree with
/// each other — the very shape of check this project exists to distrust. Both readings above are off
/// the DEVICE, which is the part that makes them evidence.</para>
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
