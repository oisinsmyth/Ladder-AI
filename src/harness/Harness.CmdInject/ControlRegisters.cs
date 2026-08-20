using Harness.Map;
using Harness.Wire;

namespace Harness.CmdInject;

/// <summary>
/// The two elements at the bottom of every harness mirror — <b>the build stamp and the free-running scan
/// counter</b> — and the only registers this binary addresses without being told to by a binding.
///
/// <para><b>Why these are protocol and not map.</b> Everything else this tool touches is job data and
/// arrives in a binding (see <see cref="InjectionRole"/>). These four registers are not: the copy layer's
/// allocator places the version element first and the scan counter immediately after it, always, for
/// every job (<c>Harness.Map.MapAllocator</c>, <c>RegisterMap.ScanCounterRegisters</c>), and
/// <c>Harness.MirrorView</c> reads them at exactly these indices. A binding that had to declare them
/// could declare them wrong, and the thing they are for is catching a device that is not the one the
/// binding describes — a check whose own address came from the same file would be circular.</para>
/// </summary>
public static class ControlRegisters
{
    /// <summary>First register of the 32-bit build stamp.</summary>
    public const int BuildStamp = 0;

    /// <summary>First register of the 32-bit free-running scan counter.</summary>
    public const int ScanCounter = 2;

    /// <summary>How many registers the two of them occupy together, from register 0.</summary>
    public const int Count = 4;

    /// <summary>Decode the stamp from a control read that starts at register 0.</summary>
    public static BuildStamp StampFrom(IReadOnlyList<ushort> control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Require(control);
        return new BuildStamp(RegisterWords.To32(control[BuildStamp], control[BuildStamp + 1], Order));
    }

    /// <summary>Decode the scan counter from a control read that starts at register 0.</summary>
    public static ScanCount ScanFrom(IReadOnlyList<ushort> control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Require(control);
        return ScanCount.FromRegisters(control[ScanCounter], control[ScanCounter + 1], Order);
    }

    /// <summary>
    /// The measured word order for this link — high word in the lower register. Named here rather than
    /// re-derived so the injection client and the view cannot disagree about it while both looking right.
    /// </summary>
    private const RegisterWordOrder Order = RegisterWordOrder.HighWordFirst;

    private static void Require(IReadOnlyList<ushort> control)
    {
        if (control.Count < Count)
        {
            throw new ArgumentException(
                $"a control read must carry {Count} register(s) (the stamp and the scan counter); it carried {control.Count}. " +
                "A short read is a different answer, not a partial one.", nameof(control));
        }
    }
}
