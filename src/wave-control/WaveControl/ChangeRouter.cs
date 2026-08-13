using System;
using System.Collections.Generic;

namespace Ladder.Wave
{
    /// <summary>
    /// D23 — the router. Decides which of the TWO queues a change belongs in: RUN-class flows through
    /// normal wave boundaries, STOP-class accumulates in the deferred queue and lands only at a drain
    /// (D24).
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THERE ARE TWO QUEUES AND NO THIRD. *** A change this router cannot place is REFUSED, not
    /// filed somewhere safe-looking. Two refusals carry the weight:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     AN UNCLASSIFIED CHANGE IS REFUSED. The temptation is to send it to the deferred queue on the
    ///     grounds that a drain pays for a CPU stop anyway, so deferring can only be conservative. That
    ///     is true about the stop and false about everything else: it launders "nobody worked out what
    ///     this is" into "this is a STOP-class change", and the log then records a decision that was
    ///     never made. D32 Class C settles the shape of this — we cannot know what it entails, and
    ///     guessing is what the policy exists to forbid. Admission is also the CHEAP place to refuse
    ///     (X-L): the agent fixes a declaration instead of a wave being spent.
    ///   </description></item>
    ///   <item><description>
    ///     A DECLARED CLASS THAT CONTRADICTS THE SPEC'S FIXED CLASS FOR THAT KIND IS REFUSED, NOT
    ///     CORRECTED. An organisation block declared RUN would, if silently corrected, produce the
    ///     right routing from a classifier that is demonstrably broken — and the next thing it
    ///     mis-declares will not be one of the four kinds this table covers.
    ///   </description></item>
    /// </list>
    /// <para>
    /// WHAT THIS TYPE DOES NOT DO: it does not CLASSIFY. DB-1 computes the class from its baseline and
    /// the reference graph, and DB-1 is not built. This consumes the answer, cross-checks it where the
    /// spec fixes it, and picks a queue.
    /// </para>
    /// </remarks>
    public static class ChangeRouter
    {
        /// <summary>
        /// The kinds whose class the spec FIXES, with the rule that fixes it. A declaration that
        /// disagrees with this table is refused.
        /// </summary>
        /// <remarks>
        /// R1 is the authority for <see cref="ObjectKind.OrganizationBlock"/> and it is BROADER than
        /// DB-1's table: DB-1 enumerates "new / deleted OB, or OB property change" while R1 says
        /// plainly that "an OB change ... is a STOP-class change and routes to the deferred queue
        /// (D23)". An OB CODE change is named by neither, so the two readings differ exactly there.
        /// This table takes R1 — every OB change is STOP — because it is the routing rule, because it
        /// is the fail-closed direction (the cost is an agent waiting for a drain, against the cost of
        /// stopping the CPU mid-wave), and because a rule that had to ask which KIND of OB edit it was
        /// looking at would need a change-nature distinction nothing else in this design carries.
        /// </remarks>
        public static readonly IReadOnlyDictionary<ObjectKind, string> KindsWithAFixedStopClass =
            new Dictionary<ObjectKind, string>
            {
                { ObjectKind.OrganizationBlock, "R1 / DB-1 — an OB change is STOP-class and routes to the deferred queue; new or deleted OB is STOP on this CPU [R]" },
                { ObjectKind.TextList, "DB-1 — new / revised text lists are STOP [R, Siemens' RUN-download table, S7-1200 V4 column]" },
                { ObjectKind.HardwareConfiguration, "DB-1 — hardware configuration is STOP [R, Siemens' RUN-download table, S7-1200 V4 column]" },
                { ObjectKind.RetentivitySetting, "DB-1 — modified retentivity settings are STOP [R, Siemens' RUN-download table, S7-1200 V4 column]" },
            };

        /// <summary>Route one changed object. Never throws for bad content; returns a refusal instead.</summary>
        public static RoutingVerdict Route(ChangedObject changedObject)
        {
            if (changedObject == null)
            {
                throw new ArgumentNullException(nameof(changedObject));
            }

            if (changedObject.Name.Length == 0)
            {
                return RoutingVerdict.Refuse(
                    changedObject,
                    RoutingRefusal.UnnamedObject,
                    "The change carried no object name. Nothing downstream — a batch, a load-manifest " +
                    "cross-check, a deferred-queue entry — can refer to it, so it cannot be routed.");
            }

            if (changedObject.Kind == ObjectKind.Unknown)
            {
                return RoutingVerdict.Refuse(
                    changedObject,
                    RoutingRefusal.UnknownObjectKind,
                    "'" + changedObject.Name + "' does not say what kind of object it is. The only " +
                    "cross-check on a declared change class is the table of kinds whose class the spec " +
                    "fixes, and an object with no kind cannot be held to it.");
            }

            string fixedRule;
            if (KindsWithAFixedStopClass.TryGetValue(changedObject.Kind, out fixedRule) &&
                changedObject.ChangeClass != ChangeClass.Stop)
            {
                return RoutingVerdict.Refuse(
                    changedObject,
                    RoutingRefusal.ContradictsTheFixedClassForThatKind,
                    "'" + changedObject.Name + "' is a " + changedObject.Kind + " declared as " +
                    changedObject.ChangeClass + ", and the spec fixes that kind at Stop. " + fixedRule +
                    ". Refused rather than corrected: a classifier that got this wrong is wrong about " +
                    "more than the four kinds this table covers.");
            }

            switch (changedObject.ChangeClass)
            {
                case ChangeClass.Run:
                    return RoutingVerdict.Route(
                        changedObject,
                        DownloadQueue.RunQueue,
                        "RUN-class: loaded with nothing disturbed, so it flows through a normal wave " +
                        "boundary (D23 / §1.4).");

                case ChangeClass.RunInit:
                    return RoutingVerdict.Route(
                        changedObject,
                        DownloadQueue.RunQueue,
                        "RUN (Init): still a RUN download, so it flows through a normal wave boundary " +
                        "(D23) — but it RESETS DATA INCLUDING RETENTIVES, which invalidates every " +
                        "result obtained before it (DB-2's validity stamp, D16 for a shared UDT).");

                case ChangeClass.Stop:
                    return RoutingVerdict.Route(
                        changedObject,
                        DownloadQueue.DeferredQueue,
                        "STOP-class: cannot be loaded without stopping the CPU, so it does NOT flow " +
                        "through a wave boundary. It accumulates until the deferred queue is drained " +
                        "because no test can make progress (D23 / D24).");

                default:
                    // The one fallthrough, and it refuses. Do not add an "otherwise" branch below this.
                    return RoutingVerdict.Refuse(
                        changedObject,
                        RoutingRefusal.UnknownChangeClass,
                        "'" + changedObject.Name + "' carries no change class. Routing it would be " +
                        "guessing whether loading it stops the CPU; there are two queues and no third " +
                        "for 'not sure'.");
            }
        }
    }
}
