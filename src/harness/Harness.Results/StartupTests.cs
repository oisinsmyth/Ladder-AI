namespace Harness.Results;

/// <summary>
/// A disruptive boundary that is <b>already scheduled</b> — the full download that STOPS AND RESTARTS the
/// CPU (D25, R8, measured in §9b).
///
/// <para><b>X-F's whole economy rests on this being an existing event.</b> That restart is precisely the
/// stimulus a first-scan requirement needs, and it happens on a schedule the design already manages, so a
/// startup test <i>costs no additional CPU stop</i>. Scheduling one where no boundary exists would mean
/// stopping the CPU in order to test it, which is a different proposition and is refused.</para>
/// </summary>
public sealed record DisruptiveBoundary(string Id, string Reason);

/// <summary>Why a startup schedule was refused. Each names a different thing to change.</summary>
public enum StartupRefusal
{
    /// <summary>Unusable zero value.</summary>
    Unstated = 0,

    /// <summary>
    /// No disruptive boundary was available to ride on. X-F converts these requirements from untestable to
    /// routine <b>because the restart already happens</b>; without one, this is a request for a CPU stop.
    /// </summary>
    NoBoundaryToRideOn,

    /// <summary>
    /// The same vector appears in the ordinary wave set. X-F is explicit: startup tests are <b>not packed
    /// into ordinary tensors</b> — the inert phase establishes a start STATE and cannot produce a first
    /// SCAN, and OB100 never runs again once the CPU is in RUN.
    /// </summary>
    PackedIntoAnOrdinaryTensor,

    /// <summary>
    /// An expectation is not LATCHED. <b>The harness is DISCONNECTED across the download (D18)</b>, so
    /// first-scan evidence must be latched in the copy layer and read after reconnect — it can never be
    /// sampled live.
    /// </summary>
    EvidenceNotLatched,

    /// <summary>No startup vectors were supplied. Empty is not clean.</summary>
    NothingScheduled,
}

/// <summary>One refusal, against the vector that caused it.</summary>
public sealed record StartupFinding(string VectorId, StartupRefusal Refusal, string Detail);

/// <summary>The schedule, or the reasons there isn't one.</summary>
public sealed record StartupPlan(
    DisruptiveBoundary? Boundary,
    IReadOnlyList<string> Scheduled,
    IReadOnlyList<StartupFinding> Findings)
{
    /// <summary>Scheduled only when something was scheduled AND nothing was refused.</summary>
    public bool IsScheduled => Findings.Count == 0 && Scheduled.Count > 0;

    public string Render() =>
        IsScheduled
            ? $"SCHEDULED — {Scheduled.Count} startup test(s) ride the disruptive boundary '{Boundary!.Id}' ({Boundary.Reason}). "
              + "The restart that boundary already performs IS the stimulus, so this costs no additional CPU stop. Every expectation is LATCHED and is read after reconnect."
            : $"NOT SCHEDULED — {Findings.Count} refusal(s): " + string.Join(" | ", Findings.Select(f => $"{f.VectorId}: {f.Refusal} — {f.Detail}"));
}

/// <summary>
/// <b>Build-plan 6.7 — X-F's startup test class.</b>
///
/// <para>"On start-up, all valves shall be closed"; "retentive setpoints shall survive a restart". These
/// cannot be tested by the wave loop at all — <b>the inert phase establishes a start STATE but cannot
/// produce a first SCAN</b>, and OB100 never runs again once the CPU is in RUN. §7 claims the
/// UNTESTABLE-ON-RIG bucket "should be nearly empty"; this is a real category it did not account for.</para>
///
/// <para><b>The capability is already present and was unrecognised:</b> the disruptive boundary performs a
/// full download that stops and restarts the CPU. So a startup test is not new machinery, it is a
/// SCHEDULING rule plus one hard constraint.</para>
///
/// <para><b>THE CONSTRAINT, AND IT IS READ LITERALLY AND FAIL-CLOSED.</b> X-F says first-scan evidence
/// "must be LATCHED in the copy layer and read after reconnect — it can never be sampled live". SAMPLED is
/// refused for the obvious reason: the harness is disconnected across the download (D18), so there is
/// nobody looking while the first scan happens. <b>STAMPED is refused too, and that is a decision rather
/// than a reading:</b> a scan stamp is a persistent integer that would plausibly survive to the reconnect,
/// but X-F names latching and only latching, and admitting a mode on an inference — at the one boundary
/// where nothing is watching — would be the permissive direction. If the owner rules stamps admissible
/// here, this is one enum member.</para>
///
/// <para><b>There is no "test class" field on a vector, on purpose.</b> Which class a vector belongs to is
/// expressed by WHICH ARGUMENT it is passed as, so a vector cannot carry a blank class that something later
/// resolves permissively — and a vector that appears in both lists is caught by comparison rather than by
/// trusting a label.</para>
/// </summary>
public static class StartupSchedule
{
    /// <summary>Schedule the startup class against a boundary that is already happening.</summary>
    /// <param name="startupVectors">The first-scan / power-up vectors.</param>
    /// <param name="boundary">The already-scheduled disruptive boundary. <b>Null is a refusal</b>, never a "schedule one".</param>
    /// <param name="waveVectors">The ordinary wave set, so double-booking can be DETECTED rather than declared away.</param>
    public static StartupPlan Schedule(
        IReadOnlyList<SubmissionVector> startupVectors,
        DisruptiveBoundary? boundary,
        IReadOnlyList<SubmissionVector> waveVectors)
    {
        ArgumentNullException.ThrowIfNull(startupVectors);
        ArgumentNullException.ThrowIfNull(waveVectors);

        var findings = new List<StartupFinding>();

        if (startupVectors.Count == 0)
        {
            findings.Add(new StartupFinding("<none>", StartupRefusal.NothingScheduled,
                "no startup vectors were supplied, so nothing was scheduled. Empty is not clean: a schedule over nothing must not read like a successful one."));
        }

        if (boundary is null)
        {
            findings.Add(new StartupFinding("<all>", StartupRefusal.NoBoundaryToRideOn,
                "no disruptive boundary is scheduled for these tests to ride. X-F's argument is that the restart ALREADY HAPPENS — it converts a whole requirement category from untestable to routine at no additional CPU stop. Without a boundary, scheduling a startup test means asking for a stop of its own, which is a different decision and not this one."));
        }

        var ordinary = waveVectors.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var vector in startupVectors)
        {
            if (ordinary.Contains(vector.Id))
            {
                findings.Add(new StartupFinding(vector.Id, StartupRefusal.PackedIntoAnOrdinaryTensor,
                    $"'{vector.Id}' is also in the ordinary wave set. A startup test cannot be packed into an ordinary tensor: the inert phase establishes a start STATE and cannot produce a first SCAN, and OB100 never runs again once the CPU is in RUN. Run in a wave it would report a confident result about a stimulus that never occurred."));
            }

            foreach (var expectation in vector.Expectations.Where(e => e.Mode != InstrumentationMode.Latched))
            {
                findings.Add(new StartupFinding(vector.Id, StartupRefusal.EvidenceNotLatched,
                    $"'{vector.Id}' expects '{expectation.Signal}' {expectation.Mode}. THE HARNESS IS DISCONNECTED ACROSS THE DOWNLOAD (D18), so there is nobody polling while the first scan happens: first-scan evidence must be LATCHED in the copy layer and read after reconnect. "
                    + (expectation.Mode == InstrumentationMode.Stamped
                        ? "A STAMP would plausibly survive to the reconnect, but X-F names latching and only latching, and admitting a mode on an inference at the one boundary where nothing is watching is the permissive direction. Refused deliberately, not by oversight."
                        : "A SAMPLED observation of a first scan is an observation nobody was present for.")));
            }

            if (vector.Expectations.Count == 0)
            {
                findings.Add(new StartupFinding(vector.Id, StartupRefusal.EvidenceNotLatched,
                    $"'{vector.Id}' declares no expectations at all, so no latched evidence would exist to read after reconnect. A startup test that latches nothing observes nothing, and the restart is not repeatable on demand."));
            }
        }

        return new StartupPlan(boundary, startupVectors.Select(v => v.Id).ToArray(), findings);
    }
}
