using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// What the D32 classifier decided about one raised configuration, in a form a caller can act on
    /// AND log. Immutable, self-describing, and constructible only by
    /// <see cref="ConfigurationClassifier"/>.
    /// </summary>
    public sealed class ConfigurationVerdict
    {
        private ConfigurationVerdict(
            RaisedConfiguration configuration,
            ConfigurationClass classification,
            LadderRung nextRung,
            string reason,
            string evidence,
            string? answeredSelection,
            UnknownReason? unknownReason,
            DelegateStage raisedIn)
        {
            Configuration = configuration;
            Class = classification;
            NextRung = nextRung;
            Reason = reason;
            Evidence = evidence;
            AnsweredSelection = answeredSelection;
            UnknownReason = unknownReason;
            RaisedIn = raisedIn;
        }

        /// <summary>The configuration that was classified.</summary>
        public RaisedConfiguration Configuration { get; }

        /// <summary>The class. <see cref="ConfigurationClass.Unknown"/> (Class C) is the default everywhere.</summary>
        public ConfigurationClass Class { get; }

        /// <summary>'A', 'B' or 'C' — the letter D32 argues in, for logs and human reports.</summary>
        public char ClassLetter
        {
            get
            {
                switch (Class)
                {
                    case ConfigurationClass.RefusedOnlyBecauseNonDisruptive: return 'A';
                    case ConfigurationClass.NeverPermitted: return 'B';
                    default: return 'C';
                }
            }
        }

        /// <summary>
        /// Which rung of D32's ladder this verdict sends the caller to. *** PER ENTRY SINCE
        /// 2026-08-13, NOT PER CLASS *** — see <see cref="ClassAEntry.Rung"/>.
        /// </summary>
        public LadderRung NextRung { get; }

        /// <summary>Which delegate raises this configuration; <see cref="DelegateStage.Unknown"/> for B and C.</summary>
        public DelegateStage RaisedIn { get; }

        /// <summary>What a policy throw on it leaves the controller in [M 2026-08-13].</summary>
        public AbortAftermath Aftermath => AbortAftermathTable.For(RaisedIn);

        /// <summary>
        /// TRUE when refusing this configuration leaves the CPU STOPPED, so whatever comes next must
        /// carry a start step. Only a POST-delegate entry does.
        /// </summary>
        public bool AbortLeavesTheCpuStopped => Aftermath == AbortAftermath.CpuLeftStoppedWithACompleteProgram;

        /// <summary>
        /// FALSE when this verdict's rung rests on an aftermath nobody has measured. Nothing here
        /// currently produces it — Class A entries must name a stage, and B/C never reach a rung whose
        /// correctness depends on one — but a caller may check rather than assume.
        /// </summary>
        public bool RungRestsOnAMeasuredAftermath =>
            Class != ConfigurationClass.RefusedOnlyBecauseNonDisruptive || AbortAftermathTable.IsMeasured(RaisedIn);

        /// <summary>
        /// TRUE ONLY FOR CLASS A. The single question the ladder's step 6 turns on: would spending a
        /// disruptive full download buy anything? For Class B a disruptive download refuses the same
        /// selection and aborts identically; for Class C we do not know what it entails, and a full
        /// download has a LARGER delta than a differential so it raises MORE configurations, never
        /// fewer.
        /// </summary>
        /// <remarks>
        /// *** NARROWED 2026-08-13: A POST-DELEGATE CLASS A ENTRY IS EXCLUDED. *** For those the
        /// transfer has already happened and the CPU is already stopped, so a fresh disruptive download
        /// does not "resolve" anything the current one could not — the rung is to answer it where it
        /// was raised. Leaving this true for POST entries is exactly the circularity D32's own defect
        /// note flagged.
        /// </remarks>
        public bool DisruptiveDownloadWouldResolveIt =>
            Class == ConfigurationClass.RefusedOnlyBecauseNonDisruptive &&
            NextRung == LadderRung.Step6DisruptiveFullDownload;

        /// <summary>
        /// The selection a disruptive download would answer, for Class A. Null for B and C — there
        /// is nothing we are willing to answer, which is the point.
        /// </summary>
        public string? AnsweredSelection { get; }

        /// <summary>Why this class, in a sentence fit for an unattended log.</summary>
        public string Reason { get; }

        /// <summary>Where the claim behind <see cref="Reason"/> comes from.</summary>
        public string Evidence { get; }

        /// <summary>
        /// For Class C only: which flavour of unknown. Every flavour goes to a human; the flavours
        /// call for different fixes, which is the same distinction D32 caveat 1 insists on between
        /// "no block is responsible" and "the locator failed to find one".
        /// </summary>
        public UnknownReason? UnknownReason { get; }

        /// <summary>
        /// One line, ready to write to an unattended run's log. Carries the class letter, the rung,
        /// the configuration and its offered selections, so a later reader can tell WHICH decision
        /// was made without also having the object graph.
        /// </summary>
        public string ToLogLine()
        {
            var selections = Configuration.OfferedSelections.Count == 0
                ? "<none reported>"
                : string.Join("|", Configuration.OfferedSelections.ToArray());

            var name = Configuration.Name.Length == 0 ? "<unnamed>" : Configuration.Name;

            var rung = NextRung == LadderRung.Step6DisruptiveFullDownload
                ? "step 6 (disruptive full download)"
                : "step 7 (total test abort, human)";

            var answered = AnsweredSelection == null ? string.Empty : " answers=" + AnsweredSelection;

            return "CONFIG-CLASS " + ClassLetter + " " + name + " selections=" + selections + answered +
                   " -> " + rung + " :: " + Reason + " [" + Evidence + "]";
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();

        internal static ConfigurationVerdict ClassA(RaisedConfiguration configuration, ClassAEntry entry)
        {
            return new ConfigurationVerdict(
                configuration,
                ConfigurationClass.RefusedOnlyBecauseNonDisruptive,
                entry.Rung,
                "Refused only because the wave loop is non-disruptive; " + DescribeOptions(entry.EntailedBy) +
                " already entails it (" + entry.Entailment + "). A disruptive full download ANSWERS it with '" +
                entry.AnsweredSelection + "'.",
                entry.Evidence + " | aftermath: " + AbortAftermathTable.EvidenceFor(entry.RaisedIn),
                entry.AnsweredSelection,
                null,
                entry.RaisedIn);
        }

        internal static ConfigurationVerdict ClassB(RaisedConfiguration configuration, ClassBEntry entry)
        {
            return new ConfigurationVerdict(
                configuration,
                ConfigurationClass.NeverPermitted,
                LadderRung.Step7TotalTestAbort,
                "Never permitted in any mode: '" + entry.ConfigurationName + " -> " + entry.DeniedSelection +
                "' destroys beyond what the download option entails (" + entry.DestroysBeyond +
                "). A disruptive download refuses it identically and aborts identically, so escalating " +
                "buys nothing and costs a CPU stop.",
                entry.Evidence,
                null,
                null,
                DelegateStage.Unknown);
        }

        internal static ConfigurationVerdict ClassC(RaisedConfiguration configuration, UnknownReason reason, string detail)
        {
            return new ConfigurationVerdict(
                configuration,
                ConfigurationClass.Unknown,
                LadderRung.Step7TotalTestAbort,
                detail + " We cannot know what it entails, and guessing is the thing this policy exists " +
                "to forbid. A full download has a LARGER delta than a differential and so raises MORE " +
                "configurations, never fewer — escalating cannot make it go away.",
                "D32 caveat 3, Class C",
                null,
                reason,
                DelegateStage.Unknown);
        }

        private static string DescribeOptions(DownloadOption options)
        {
            var named = new List<string>();
            if ((options & DownloadOption.SoftwareOnlyChanges) != 0) named.Add("SoftwareOnlyChanges");
            if ((options & DownloadOption.Software) != 0) named.Add("Software");
            if ((options & DownloadOption.Hardware) != 0) named.Add("Hardware");

            if (named.Count == 0)
            {
                // Unreachable: ClassAEntry's constructor rejects DownloadOption.None. Kept so a future
                // change to that constructor produces a visible string rather than an empty clause.
                throw new InvalidOperationException("A Class A entry reached the verdict with no download option named.");
            }

            return "the " + string.Join(" / ", named.ToArray()) + " download";
        }
    }
}
