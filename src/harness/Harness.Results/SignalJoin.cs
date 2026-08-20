using Harness.Map;

namespace Harness.Results;

/// <summary>
/// One vector's cited signal that the binding serving its slot does not carry.
/// </summary>
/// <param name="VectorId">The vector that cited it.</param>
/// <param name="Signal">The name it cited.</param>
/// <param name="Role">Where it was cited — an expectation, or the completion signal. The two fail differently.</param>
public sealed record UnjoinedSignal(string VectorId, string Signal, SignalRole Role);

/// <summary>Where a cited signal was used. <b>Both are consumed as a REGISTER, which is why both must join.</b></summary>
public enum SignalRole
{
    /// <summary>An <c>expectations[].signal</c>. Unjoined, it can only ever come back <c>&lt;never read&gt;</c>.</summary>
    Expectation,

    /// <summary>
    /// The vector's <c>completionSignal</c>. <b>The dangerous one</b> — it is what the poll loop watches,
    /// and an unjoined name used to fall back to <b>result register 0</b>, which is some other signal.
    /// </summary>
    Completion,

    /// <summary>
    /// 🔴 <b>A <c>settlingSignals[]</c> entry. Checkable only since 2026-08-20, because until then nothing
    /// resolved a settling name to a register at all.</b>
    ///
    /// <para><b>It is a REFUSAL rather than a per-vector caveat for the reason the other two are:</b>
    /// settling is now evaluated over exactly the registers these names resolve to, so an unjoined one
    /// would leave the check comparing FEWER registers than were declared — and a settling check over zero
    /// registers passes over anything. Empty is not clean, and the failure would present as a clean
    /// <c>Settled</c>.</para>
    /// </summary>
    Settling,
}

/// <summary>
/// Every unjoined cited signal, and the refusal that names them.
/// </summary>
/// <param name="Signals">How many (vector, signal) citations failed to join.</param>
/// <param name="Names">How many DISTINCT signal names failed.</param>
/// <param name="Examined">
/// <b>The denominator.</b> How many citations were checked at all. <c>Clean</c> over zero citations is
/// not a pass and says so — empty is not clean.
/// </param>
/// <param name="Detail">The whole refusal, ready to print, naming both sides.</param>
public sealed record SignalJoinReport(
    int Signals, int Names, int Examined, IReadOnlyList<UnjoinedSignal> Unjoined, string Detail)
{
    /// <summary>True when at least one cited signal is not carried by the binding that serves its slot.</summary>
    public bool Any => Signals > 0;
}

/// <summary>
/// 🔴 <b>A SIGNAL NAME IS THE SECOND JOIN BETWEEN A SUBMISSION AND A BINDING, AND NOTHING COMPARED THEM
/// AT ALL — SO AN UNJOINED NAME WAS ANSWERED WITH SILENCE INSTEAD OF A REFUSAL.</b>
///
/// <para><see cref="SlotJoin"/> checks that a vector's <c>slot</c> resolves to a binding. This checks the
/// level below: that each signal the vector NAMES resolves to a result source of that binding. They are
/// separate failures with separate remedies, and the second one had no check.</para>
///
/// <para><b>MEASURED ON JOB9004'S FIRST LIVE WAVE, 2026-08-17.</b> Both vectors ran to completion against the
/// bench rig, the wave cost 7,912 round trips, and the published mirror feed records the entire
/// 23-register result band being read with a UTC instant on it — <b>including the register holding the
/// exact value one vector expected.</b> Every declared assertion nonetheless came back
/// <c>observed: "&lt;never read&gt;", state: NotObserved</c>. Two distinct causes, both of them this
/// join:</para>
/// <list type="number">
/// <item><b>The expectations</b> cited <c>SPEC.FaultAlarm</c>, <c>SPEC.HoldCommand</c>,
/// <c>SPEC.EngageCommand</c> — the binding's
/// own <c>specName</c>s — while <c>SlotBinding.ResultRegisterOf</c> matched on <c>tag</c>. That is fixed
/// at the lookup, and those four names now resolve.</item>
/// <item><b>The completion signal</b> cited <c>SPEC.Scenario_Done</c>, which matches NEITHER the tag NOR
/// the spec name (<c>SPEC.ScenarioDone</c>) — a genuine disagreement between the two documents that
/// no lookup can repair. <b>It fell back to result register 0</b>, a wholly different signal, and one
/// vector was therefore declared complete <b>8 scans</b> into a 41-second scenario.</item>
/// </list>
///
/// <para><b>So the fix is BOTH: one key, and a refusal for the names that still do not join.</b> Fixing
/// only the lookup would have left the completion citation silently pointing at register 0 — a
/// plausible-looking run, finished early, with no error anywhere.</para>
///
/// <para><b>It runs before anything is spent</b>, beside <see cref="SlotJoin"/>, for the reason recorded
/// there: a cross-reference failure that costs a download is a check in the wrong place.</para>
///
/// <para><b>IT IS DELIBERATELY ONE-SIDED, exactly as <see cref="SlotJoin"/> is.</b> A cited signal the
/// binding does not carry cannot be observed. A CARRIED signal no vector cites is a different fact
/// entirely — a binding publishes diagnostic registers on purpose (this deliverable carries twenty and
/// cites four) — and refusing it would fire far outside this check's scope.</para>
///
/// <para>✅ <b>SETTLING SIGNALS ARE CHECKED HERE SINCE 2026-08-20 — THE DAY THIS PARAGRAPH NAMED.</b> It
/// used to read <i>"settling signals are not checked here, and that is a named limit"</i>, on the stated
/// ground that <c>SettlingDeclaration.Signals</c> was resolved to a register by nothing and
/// <c>LoopRun.Settling</c> compared whole result arrays, so an unjoined settling name cost nothing — and
/// it said the limit would end the day settling was evaluated per signal. <c>LoopRun.Settling</c> now
/// resolves each declared name through <c>SlotBinding.ResultRegisterOf</c> and compares ONLY those
/// registers, so an unjoined name would silently SHRINK the set the check is taken over. <b>A settling
/// check over zero registers passes over anything</b>, and it would present as a clean <c>Settled</c> —
/// which is why this is a refusal and not a caveat.</para>
/// </summary>
public static class SignalJoin
{
    /// <summary>
    /// Check every vector's cited signals against the binding that serves its slot.
    /// </summary>
    /// <param name="vectors">The submitted vectors.</param>
    /// <param name="bindingFor">
    /// The binding serving a cited slot id, or null when none does. <b>Null is skipped, not refused</b> —
    /// that is <see cref="SlotJoin"/>'s finding and reporting it twice under two names sends the reader
    /// looking for a signal problem that does not exist.
    /// </param>
    public static SignalJoinReport Check(
        IEnumerable<SubmissionVector> vectors,
        Func<string, SlotBinding?> bindingFor)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(bindingFor);

        var unjoined = new List<UnjoinedSignal>();
        var carried = new SortedSet<string>(StringComparer.Ordinal);
        var examined = 0;

        foreach (var vector in vectors)
        {
            if (vector is null)
                continue;

            var binding = bindingFor(vector.Slot ?? string.Empty);
            if (binding is null)
                continue;

            foreach (var source in binding.ResultSources ?? Array.Empty<MirroredSignal>())
            {
                if (source is not null)
                    carried.Add(source.JoinKey);
            }

            foreach (var expectation in vector.Expectations ?? Array.Empty<ObservabilityDeclaration>())
            {
                examined++;
                if (binding.ResultRegisterOf(expectation.Signal ?? string.Empty) < 0)
                    unjoined.Add(new UnjoinedSignal(vector.Id, expectation.Signal ?? "<null>", SignalRole.Expectation));
            }

            // 🔴 *** THE SETTLING NAMES, CHECKED HERE SINCE 2026-08-20. *** This file's own summary called
            // their absence "a named limit rather than an oversight", on the stated ground that
            // `LoopRun.Settling` compared whole result arrays so an unjoined settling name cost nothing —
            // and said the day settling became per-signal was the day they became checkable. It has.
            foreach (var signal in vector.Settling?.Signals ?? Array.Empty<string>())
            {
                examined++;
                if (binding.ResultRegisterOf(signal ?? string.Empty) < 0)
                    unjoined.Add(new UnjoinedSignal(vector.Id, signal ?? "<null>", SignalRole.Settling));
            }

            examined++;
            if (binding.ResultRegisterOf(vector.CompletionSignal ?? string.Empty) < 0)
                unjoined.Add(new UnjoinedSignal(vector.Id, vector.CompletionSignal ?? "<null>", SignalRole.Completion));
        }

        var names = unjoined.Select(u => u.Signal).Distinct(StringComparer.Ordinal).Count();

        // EMPTY IS NOT CLEAN. Zero citations examined means this check answered nothing, and it says so
        // rather than reporting the same clean line a real join produces. It does not GATE on that — a
        // submission with no vectors is stopped elsewhere — but a reader must not read it as a pass.
        if (unjoined.Count == 0)
        {
            return new SignalJoinReport(0, 0, examined, Array.Empty<UnjoinedSignal>(),
                examined == 0
                    ? "NOTHING EXAMINED - no signal citation was checked, so this is not a pass."
                    : $"every one of the {examined} cited signal(s) resolves to a result register of the binding serving its slot.");
        }

        var byRole = unjoined
            .GroupBy(u => u.Role)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: " + string.Join(", ",
                g.Select(u => $"'{u.Signal}' (vector {u.VectorId})").Distinct(StringComparer.Ordinal).Take(8)));

        var completion = unjoined.Any(u => u.Role == SignalRole.Completion);

        return new SignalJoinReport(unjoined.Count, names, examined, unjoined,
            $"{unjoined.Count} of {examined} cited signal(s) name {names} signal(s) that the binding serving their slot does not carry. "
            + "THE SIGNAL NAME IS THE JOIN, and the two documents are written by parties who deliberately do not read each other's: "
            + "a vector cites the SPECIFICATION's name and the binding states it as `specName` beside the block's own tag. "
            + string.Join(" | ", byRole)
            + ". CARRIED BY THE BINDING: "
            + (carried.Count == 0
                ? "NOTHING - the binding declares no result sources at all"
                : string.Join(", ", carried.Select(c => $"'{c}'")))
            + ". *** THIS IS A BINDING/SUBMISSION DISAGREEMENT, NOT A FAULT IN EITHER FILE'S SYNTAX. *** "
            + (completion
                ? "*** ONE OF THEM IS A COMPLETION SIGNAL, WHICH IS THE POLL'S OWN WATCH REGISTER. *** Before this refusal existed it "
                  + "defaulted to result register 0 - a different signal - so the wave declared a vector finished on whatever that register "
                  + "happened to read. Fix the name; there is no safe default. "
                : string.Empty)
            + (unjoined.Any(u => u.Role == SignalRole.Settling)
                ? "*** ONE OF THEM IS A SETTLING SIGNAL, AND SETTLING IS NOW EVALUATED OVER EXACTLY THE REGISTERS THESE NAMES RESOLVE TO. *** "
                  + "An unjoined name does not weaken the check by a little - it removes that signal from it, and a settling check over no "
                  + "registers is satisfied by anything. Fix the name; there is no safe default. "
                : string.Empty)
            + "Either the binding is short of the signals the vectors were written against, or the vectors cite names the coordinator did not state.");
    }
}
