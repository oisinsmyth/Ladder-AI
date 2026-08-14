using Harness.Map;

namespace Harness.Wire;

/// <summary>What the version register said. Every one of these is a distinct thing to do next.</summary>
public enum VersionOutcome
{
    /// <summary>Stable and equal to the stamp the coordinator generated. The code carrying it is running.</summary>
    Confirmed,

    /// <summary>Stable at zero. Nothing has ever written it — the copy layer is not running at all.</summary>
    Absent,

    /// <summary>Stable at a different stamp. The download aborted, was refused, or never reached the device.</summary>
    Stale,

    /// <summary>
    /// Stable, and equal to the expected stamp WITH ITS TWO HALVES SWAPPED. Almost certainly the
    /// word order rather than a failed download — see <see cref="RegisterWordOrder"/>, where the order is
    /// now measured but the outcome is deliberately kept.
    /// </summary>
    WordOrderSuspect,

    /// <summary>Never settled within the bound. The settling window is longer than we allowed, or it is flapping.</summary>
    Unsettled,
}

/// <summary>
/// The post-download verdict, WITH the settling window it measured.
///
/// <para>§9: the value "flaps or reads old until integration completes, so waiting for stability
/// MEASURES the window rather than guessing it". <see cref="ReadsToSettle"/> is that measurement, and it
/// is the reason this returns a report rather than a bool.</para>
/// </summary>
public sealed record VersionReport(
    VersionOutcome Outcome,
    uint Observed,
    uint Expected,
    int Reads,
    int ReadsToSettle,
    string Detail)
{
    public bool Confirmed => Outcome == VersionOutcome.Confirmed;
}

/// <summary>
/// Build-plan item 2.6's client half: read the version register, wait for it to settle, and say which
/// of five things happened.
///
/// <para><b>What the mechanism is.</b> The CPU will not state its own identity, so the PROGRAM publishes
/// it: the coordinator computes a build stamp over what it is about to download and generates it as a
/// LITERAL into the copy layer. That constant lives in the code, so it can only be present if that code
/// is running, and verification becomes one register read — no upload, no Portal session, sub-second.</para>
///
/// <para><b>What it catches:</b> a download that aborted or was refused (the register still shows the
/// OLD stamp — external gap G4's undocumented half-loaded case, detected without needing Siemens to
/// document it), a download that never reached the device, and the settling window.
/// <b>What it does not catch:</b> individual block corruption. TIA performed the download; if the
/// version constant is live, the code carrying it is live.</para>
///
/// <para><b>Why "stable" is not "read it once".</b> A single read during integration returns the old
/// value and looks exactly like a failed download. Requiring N identical consecutive reads turns that
/// ambiguity into a measurement.</para>
/// </summary>
public static class VersionCheck
{
    /// <summary>Read until the version register settles, then classify.</summary>
    /// <param name="stableReads">Consecutive identical reads that count as settled. Two is not a trend.</param>
    /// <param name="maxReads">Bound, so a flapping register ends the check rather than hanging it.</param>
    public static VersionReport Confirm(MirrorClient client, BuildStamp expected, int stableReads = 3, int maxReads = 60)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (stableReads < 2)
            throw new ArgumentOutOfRangeException(nameof(stableReads), stableReads, "one read is not stability; it is a sample.");

        if (maxReads < stableReads)
            throw new ArgumentOutOfRangeException(nameof(maxReads), maxReads, "the bound must allow at least one chance to settle.");

        uint last = 0;
        var run = 0;
        var reads = 0;

        while (reads < maxReads)
        {
            // Deliberately the UNVERIFIED read: this runs before the expected stamp can be assumed present,
            // and a client that threw on the mismatch could never report which mismatch it was.
            var observed = client.ReadControlUnverified().Version;
            reads++;

            run = reads > 1 && observed == last ? run + 1 : 1;
            last = observed;

            if (run >= stableReads)
                return Classify(last, expected, reads, reads - stableReads + 1);
        }

        return new VersionReport(VersionOutcome.Unsettled, last, expected.Value, reads, 0,
            $"the version register did not read the same value {stableReads} times in {maxReads} reads. Either the settling window is longer than this bound, or the value is flapping — and an unsettled register is NOT a confirmed download.");
    }

    private static VersionReport Classify(uint observed, BuildStamp expected, int reads, int readsToSettle)
    {
        if (observed == expected.Value)
        {
            return new VersionReport(VersionOutcome.Confirmed, observed, expected.Value, reads, readsToSettle,
                $"16#{observed:X8} is the stamp generated for this download, and it settled after {readsToSettle} read(s). The code carrying that literal is running.");
        }

        if (observed == 0)
        {
            return new VersionReport(VersionOutcome.Absent, observed, expected.Value, reads, readsToSettle,
                "the version register reads zero, which is what bit memory reads before anything writes it. The copy layer is not running: either the download did not land at all, or the CPU is not in RUN.");
        }

        if (RegisterWords.Swapped(observed) == expected.Value)
        {
            return new VersionReport(VersionOutcome.WordOrderSuspect, observed, expected.Value, reads, readsToSettle,
                $"16#{observed:X8} is the expected stamp 16#{expected.Value:X8} with its two halves swapped. That is a register WORD ORDER difference, not a failed download. The configured order was MEASURED on this rig (2026-08-13 against a known pattern, 2026-08-14 against the deployed stamp), so this reading says the device in front of you presents them the other way round — MB_SERVER's byte-to-register presentation is Siemens' behaviour and one rig's measurement is not a property of the instruction. Set RegisterWordOrder to match this device rather than re-downloading a program that is already correct.");
        }

        return new VersionReport(VersionOutcome.Stale, observed, expected.Value, reads, readsToSettle,
            $"the version register reads 16#{observed:X8}, not the 16#{expected.Value:X8} generated for this download. The device is running a DIFFERENT build: the download aborted, was refused, or never reached it. Note that an excision changes the stamp too — a stale-looking register after an excision that was not regenerated is this same message with a different cause.");
    }
}
