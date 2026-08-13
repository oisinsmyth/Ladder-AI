using System;

namespace DownloadProbe;

/// <summary>
/// The exception this tool throws ON PURPOSE from inside a download-configuration delegate.
///
/// Its own type, and a message that says so in its first six words, because the single worst outcome
/// of this feature is a log somebody reads next month and takes for a genuine defect. Nothing in
/// Siemens.Engineering can produce this type, so its presence anywhere in an exception chain is
/// proof the failure was ours and intended.
/// </summary>
internal sealed class DeliberateProbeInjectionException : Exception
{
    internal DeliberateProbeInjectionException(DelegatePhase phase, int ordinal, string configurationType)
        : base($"DELIBERATE TEST INJECTION by download-probe — this is not a defect. Thrown from the " +
               $"{phase.ToString().ToUpperInvariant()} download-configuration delegate on invocation #{ordinal}, " +
               $"immediately after recording and answering '{configurationType}'. " +
               $"Experiment 1.7: what does Openness do with an exception raised inside its own callback?")
    {
        Phase = phase;
        Ordinal = ordinal;
        ConfigurationType = configurationType;
    }

    internal DelegatePhase Phase { get; }

    internal int Ordinal { get; }

    internal string ConfigurationType { get; }
}

/// <summary>Which of the two delegates. They are two different experiments — see <see cref="ThrowInjection"/>.</summary>
internal enum DelegatePhase
{
    Pre,
    Post,
}

/// <summary>What the API did with an exception thrown out of its own callback.</summary>
internal enum InjectionOutcome
{
    /// <summary>The flag was given and the delegate was never invoked. Nothing was learned.</summary>
    NeverFired,

    /// <summary>Thrown, and the same object came back out of <c>Download</c> unwrapped.</summary>
    PropagatedVerbatim,

    /// <summary>Thrown, and it came back inside another exception — the wrapper and depth are reported.</summary>
    Wrapped,

    /// <summary>
    /// Thrown, and <c>Download</c> RETURNED NORMALLY. The API discarded it. The most consequential
    /// of the four: it would mean a callback cannot refuse a download by failing.
    /// </summary>
    Swallowed,

    /// <summary>
    /// Thrown, and something else came out that does not contain it anywhere. Distinct from
    /// <see cref="Wrapped"/> — the exception that surfaced is not ours and not carrying ours.
    /// </summary>
    ReplacedByAnotherFailure,
}

/// <summary>
/// *** EXPERIMENT 1.7: THE DELEGATE THROW. THE CAPABILITY, NOT THE ACT. ***
///
/// Openness hands this tool two callbacks during a download and reads a selection back from each. It
/// is undocumented what happens if one of them THROWS: whether the exception propagates, is wrapped,
/// or is swallowed and the download proceeds anyway. That is worth knowing because it decides
/// whether a callback can refuse a download by failing — and because a swallow would mean the
/// fail-closed posture this tool is built on has a hole in it.
///
/// <b>THE TWO DELEGATES ARE NOT THE SAME EXPERIMENT, AND ARE NOT SELECTABLE TOGETHER.</b>
/// <list type="bullet">
/// <item><b>PRE</b> fires BEFORE anything transfers. The least destructive real case, and the one to
/// run first.</item>
/// <item><b>POST</b> fires AFTER the transfer, and carries a known complication:
/// <c>StartModules</c> is raised in the POST delegate, <i>after</i> the download has already stopped
/// the modules. So a throw there can leave the CPU stopped <b>with no route to start it from inside
/// that download</b> — D32's recorded circularity meeting a real controller.</item>
/// </list>
/// A single <c>--throw</c> flag firing from whichever delegate came first would conflate two
/// experiments with very different blast radii and produce an unattributable result. Hence two
/// flags, refused in combination.
///
/// <b>WHAT THIS TYPE DOES NOT DO: decide to fire.</b> It fires only when a flag naming its phase was
/// spelled exactly, and there is no default, no environment variable and no config file that can
/// reach it.
/// </summary>
internal sealed class ThrowInjection
{
    private readonly DelegatePhase? _phase;
    private int _preInvocations;
    private int _postInvocations;

    internal ThrowInjection(DelegatePhase? phase) => _phase = phase;

    /// <summary>No injection at all — what every ordinary run gets.</summary>
    internal static ThrowInjection None { get; } = new(null);

    internal bool IsArmed => _phase is not null;

    internal DelegatePhase? Phase => _phase;

    /// <summary>Set when the throw actually happened, so "armed" and "fired" can never be confused.</summary>
    internal DeliberateProbeInjectionException? Fired { get; private set; }

    /// <summary>
    /// Called from inside each delegate, AFTER the configuration has been recorded and answered — so
    /// the log still shows what was raised, and the run remains readable as the experiment it is.
    /// Throws on the FIRST invocation of the armed phase: deterministic, minimal, and the ordinal is
    /// reported so a later run against a different configuration list is comparable.
    /// </summary>
    internal void MaybeThrow(DelegatePhase phase, string configurationType, ProbeLog log)
    {
        var ordinal = phase == DelegatePhase.Pre ? ++_preInvocations : ++_postInvocations;

        if (_phase != phase || Fired is not null)
        {
            return;
        }

        var injection = new DeliberateProbeInjectionException(phase, ordinal, configurationType);
        Fired = injection;

        log.Blank();
        log.Line($"*** INJECTING THE DELIBERATE THROW NOW — {phase.ToString().ToUpperInvariant()} delegate, invocation #{ordinal}, after answering '{configurationType}'. ***");
        log.Line("    Everything that follows is a consequence of this tool, not of the project or the device.");
        throw injection;
    }

    /// <summary>
    /// Classifies what came back out of <c>Download</c>. Keys on the OBJECT — is our exception in the
    /// chain, at what depth — and never on a state or a message string, because a classification that
    /// reads text is a classification that changes meaning when Siemens rewords something.
    /// </summary>
    internal (InjectionOutcome Outcome, string Detail) Classify(Exception? thrown)
    {
        if (Fired is null)
        {
            return (InjectionOutcome.NeverFired,
                _phase is null
                    ? "no injection was armed"
                    : $"the {_phase.ToString()!.ToUpperInvariant()} delegate was NEVER INVOKED, so the throw never happened and this run answers nothing about it");
        }

        if (thrown is null)
        {
            return (InjectionOutcome.Swallowed,
                "Download RETURNED NORMALLY after the delegate threw — the API discarded the exception. " +
                "A callback therefore CANNOT refuse a download by failing.");
        }

        if (ReferenceEquals(thrown, Fired))
        {
            return (InjectionOutcome.PropagatedVerbatim, "the identical exception object came back out of Download, unwrapped");
        }

        var depth = 0;
        for (var current = thrown; current is not null; current = current.InnerException, depth++)
        {
            if (ReferenceEquals(current, Fired))
            {
                return (InjectionOutcome.Wrapped,
                    $"wrapped {depth} level(s) deep inside {thrown.GetType().FullName}");
            }
        }

        return (InjectionOutcome.ReplacedByAnotherFailure,
            $"{thrown.GetType().FullName} came out and the injected exception is NOWHERE in its inner chain — " +
            "the API replaced it rather than carrying it");
    }
}
