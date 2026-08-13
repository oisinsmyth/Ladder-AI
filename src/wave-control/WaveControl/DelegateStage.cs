using System;

namespace Ladder.Wave
{
    /// <summary>
    /// Which download delegate a configuration is raised in. *** THIS IS NOW A LOAD-BEARING FACT, NOT
    /// A DETAIL: D32's Class-A rung is TRUE FOR PRE AND FALSE FOR POST. ***
    /// </summary>
    /// <remarks>
    /// <para>
    /// The zero value is <see cref="Unknown"/>, and a Class A entry that cannot say which delegate
    /// raises it is REFUSED — because Class A is the only class that spends anything, and after
    /// 2026-08-13 its rung is derived from this.
    /// </para>
    /// </remarks>
    public enum DelegateStage
    {
        /// <summary>Not established. Refused on a Class A entry; treated as uncharacterised everywhere else.</summary>
        Unknown = 0,

        /// <summary>
        /// Raised BEFORE the transfer, while the download is being planned. <c>StopModules</c> and
        /// <c>DataBlockReinitialization</c> are raised here.
        /// </summary>
        PreTransfer = 1,

        /// <summary>
        /// Raised AFTER the transfer. <c>StartModules</c> is raised here, and only when the download
        /// actually stopped the CPU — earlier runs never raised it because they never stopped anything.
        /// </summary>
        PostTransfer = 2,
    }

    /// <summary>What a policy throw at a given stage leaves the controller in.</summary>
    /// <remarks>
    /// <para>
    /// *** MEASURED ON THE RIG 2026-08-13, WITH THE OWNER PRESENT, ONE OBSERVATION OF EACH KIND, IN
    /// OPPOSITE DIRECTIONS RATHER THAN INFERRED ONCE. *** This retires assumption A6/G4, which had
    /// blocked D32's ladder.
    /// </para>
    /// <para>
    /// ONE SENTENCE ACCOUNTS FOR BOTH: *** the configuration answer is a SELECTION RECORDED DURING
    /// PLANNING; the ACTION is applied AFTER the delegate returns. A throw between those points aborts
    /// before the action takes effect. *** So a PRE throw pre-empts the stop and a POST throw pre-empts
    /// the start — and the two measurements are the same mechanism seen from each end:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>StopModules</c> was answered <c>StopAll</c> — "stop everything" — and the CPU STAYED
    ///     RUNNING, because the throw came before the stop was applied.
    ///   </description></item>
    ///   <item><description>
    ///     <c>StartModules</c> was answered <c>StartModule</c> — "start it" — and the CPU STAYED
    ///     STOPPED, because the throw came before the start was applied.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** AND THE FEARED FAILURE MODE IS NOT THE REAL ONE. *** A6's stated hazard was a HALF-LOADED
    /// CPU. What was measured after a POST throw is a CPU holding a COMPLETE PROGRAM THAT WAS NOT
    /// RUNNING — fully loaded and stopped, with the project bit-for-bit unchanged. The genuinely
    /// half-loaded case would need a throw DURING the transfer, and *** there is no configuration
    /// raised there to hang one on ***, so nothing in this design can produce it and nothing here may
    /// claim to handle it. See <see cref="Uncharacterised"/>.
    /// </para>
    /// </remarks>
    public enum AbortAftermath
    {
        /// <summary>
        /// *** NOT MEASURED, AND NOT REACHABLE FROM A CONFIGURATION. *** The zero value, so anything
        /// that fails to establish a stage lands here rather than on a state somebody could act on. Any
        /// rung whose correctness depends on this is UNPROVEN and must say so.
        /// </summary>
        Uncharacterised = 0,

        /// <summary>
        /// [M] The CPU was left <c>Running (8)</c>, the project bit-for-bit unchanged, the program
        /// live. The abort pre-empted the stop. *** THIS IS THE STATE D32's CLASS-A RUNG ASSUMES. ***
        /// </summary>
        CpuLeftRunning = 1,

        /// <summary>
        /// [M] The CPU was left <c>NotRunning (4)</c> — STOPPED — holding a COMPLETE program, the
        /// project bit-for-bit unchanged. The abort pre-empted the start. *** D32's CLASS-A RUNG IS
        /// FALSE HERE: the ladder cannot claim "no disruption is spent" when the CPU is already
        /// stopped. ***
        /// </summary>
        CpuLeftStoppedWithACompleteProgram = 2,
    }

    /// <summary>The measured mapping from delegate stage to aftermath, with its evidence.</summary>
    public static class AbortAftermathTable
    {
        /// <summary>What a throw at <paramref name="stage"/> leaves behind.</summary>
        public static AbortAftermath For(DelegateStage stage)
        {
            switch (stage)
            {
                case DelegateStage.PreTransfer: return AbortAftermath.CpuLeftRunning;
                case DelegateStage.PostTransfer: return AbortAftermath.CpuLeftStoppedWithACompleteProgram;
                default: return AbortAftermath.Uncharacterised;
            }
        }

        /// <summary>Where the claim comes from, for the log.</summary>
        public static string EvidenceFor(DelegateStage stage)
        {
            switch (stage)
            {
                case DelegateStage.PreTransfer:
                    return "[M] 2026-08-13 rig, owner present — PRE-delegate throw left the CPU Running (8), " +
                           "project bit-for-bit unchanged, program live; StopModules had been answered StopAll " +
                           "and the stop never took effect";

                case DelegateStage.PostTransfer:
                    return "[M] 2026-08-13 rig, owner present — POST-delegate throw left the CPU NotRunning (4) " +
                           "holding a COMPLETE program, project bit-for-bit unchanged; StartModules had been " +
                           "answered StartModule and the start never took effect";

                default:
                    return "NOT MEASURED — a throw during the transfer is the genuinely half-loaded case, and " +
                           "there is no configuration raised during transfer to hang one on, so it is not " +
                           "reachable from this design and has never been observed";
            }
        }

        /// <summary>
        /// TRUE when the controller needs starting again before testing can resume. Only the POST case
        /// does, and it is why a Class-A abort landing after the transfer must carry a start step.
        /// </summary>
        public static bool RequiresCpuStart(DelegateStage stage) =>
            For(stage) == AbortAftermath.CpuLeftStoppedWithACompleteProgram;

        /// <summary>
        /// TRUE when the aftermath rests on a measurement rather than on an assumption. FALSE for
        /// <see cref="DelegateStage.Unknown"/>, and a rung derived from it must be reported UNPROVEN.
        /// </summary>
        public static bool IsMeasured(DelegateStage stage) => For(stage) != AbortAftermath.Uncharacterised;

        /// <summary>
        /// The recovery, measured: a normal disruptive download raises <c>StartModules</c> in POST,
        /// answering it returns the CPU to <c>Running (8)</c>. 34 seconds, no owner needed.
        /// </summary>
        /// <remarks>
        /// *** WHY THAT ROUTE WORKS AT ALL, AND IT IS NOT INCIDENTAL: `StartModules` IS RAISED ONLY
        /// WHEN THE DOWNLOAD ACTUALLY STOPPED THE CPU. *** Earlier runs never raised it because they
        /// never stopped anything — so the recovery download raises it precisely because the CPU is
        /// stopped, which is the condition being recovered from.
        /// </remarks>
        public const string RecoveryEvidence =
            "[M] 2026-08-13 — recovery from the stopped state took 34 s with no owner: a normal " +
            "--disruptive download raised StartModules in POST, it was answered, and the CPU returned to " +
            "Running (8). StartModules is raised ONLY when the download actually stopped the CPU.";
    }
}
