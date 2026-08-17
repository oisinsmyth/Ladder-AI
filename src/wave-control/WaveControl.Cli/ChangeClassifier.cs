using System;
using System.Collections.Generic;
using Ladder.Wave;

namespace Ladder.Wave.Cli
{
    /// <summary>What happened to an object. The second key of DB-1's change-class table.</summary>
    /// <remarks>
    /// THE ZERO VALUE IS <see cref="Unknown"/>, for the reason <see cref="ChangeClass.Unknown"/> is: a
    /// dropped field or a caller who never set it must land on the branch that refuses. DB-1's table is
    /// keyed on KIND *and* NATURE — "new / deleted DB" is RUN while "DB interface change" is RUN (Init)
    /// — so a change set that says only WHICH object changed has supplied half a key.
    /// </remarks>
    public enum ChangeNature
    {
        /// <summary>Nobody said what happened. Refused, never assumed.</summary>
        Unknown = 0,

        /// <summary>The object did not exist before.</summary>
        Added = 1,

        /// <summary>The object existed and is going away.</summary>
        Deleted = 2,

        /// <summary>The object existed and its content changed.</summary>
        Modified = 3,
    }

    /// <summary>Why <see cref="ChangeClassifier"/> would not put a class on a change.</summary>
    public enum ClassificationRefusal
    {
        /// <summary>Not refused.</summary>
        None = 0,

        /// <summary>
        /// No <c>.ir</c> in the corpus declares this name, so its KIND is unknowable and DB-1's table
        /// cannot be entered. MEASURED, NOT HYPOTHETICAL: the 2026-08-14 reconciliation found two such
        /// objects in the controller.
        /// </summary>
        ObjectNotInCorpus = 1,

        /// <summary>Two files claim this name. Picking one would be a guess about which was meant.</summary>
        AmbiguousCorpusIdentity = 2,

        /// <summary>The change set did not say whether the object was added, deleted or modified.</summary>
        UnknownChangeNature = 3,

        /// <summary>
        /// DB-1's table has NO ROW for this (kind, nature). Named rather than filled in: G7 in the
        /// spec's own gap list is exactly such a hole ("whether a start-values-only edit is RUN or
        /// RUN (Init) — no row exists in Siemens' table, checked in both language renderings"), so the
        /// table is known to be incomplete and a classifier that closed it by inference would be
        /// inventing vendor behaviour.
        /// </summary>
        NoRuleForThisKindAndNature = 4,

        /// <summary>
        /// An operator declared a class for an object the table DOES cover, and it disagrees. Refused,
        /// not applied — <see cref="ChangeRouter"/>'s own argument: a declaration this wrong means the
        /// thing that produced it is wrong about more than this object.
        /// </summary>
        DeclaredClassContradictsTheTable = 5,
    }

    /// <summary>What the classifier decided about one change, or why it would not decide.</summary>
    public sealed class ClassificationVerdict
    {
        internal ClassificationVerdict(
            string name,
            ObjectKind kind,
            ChangeNature nature,
            ChangeClass changeClass,
            ClassificationRefusal refusal,
            string rule,
            string reason,
            bool fromDeclaration)
        {
            Name = name;
            Kind = kind;
            Nature = nature;
            ChangeClass = changeClass;
            Refusal = refusal;
            Rule = rule;
            Reason = reason;
            FromDeclaration = fromDeclaration;
        }

        /// <summary>The object.</summary>
        public string Name { get; }

        /// <summary>Its kind, as the corpus declares it. <see cref="ObjectKind.Unknown"/> if unlookupable.</summary>
        public ObjectKind Kind { get; }

        /// <summary>What happened to it.</summary>
        public ChangeNature Nature { get; }

        /// <summary>DB-1's class, or <see cref="ChangeClass.Unknown"/> when refused.</summary>
        public ChangeClass ChangeClass { get; }

        /// <summary>Why it was refused, or <see cref="ClassificationRefusal.None"/>.</summary>
        public ClassificationRefusal Refusal { get; }

        /// <summary>The DB-1 row that decided it — cited, so the verdict can be argued with.</summary>
        public string Rule { get; }

        /// <summary>The refusal's reason, in a sentence fit for an unattended log. Empty when classified.</summary>
        public string Reason { get; }

        /// <summary>TRUE when an operator supplied the class because the table had no row.</summary>
        public bool FromDeclaration { get; }

        /// <summary>TRUE only when a class was put on the change.</summary>
        public bool Classified => Refusal == ClassificationRefusal.None && ChangeClass != ChangeClass.Unknown;
    }

    /// <summary>
    /// DB-1's CLASSIFIER — the half of DB-1 that computes a change class, which
    /// <see cref="ChangeRouter"/> explicitly does not do ("WHAT THIS TYPE DOES NOT DO: it does not
    /// CLASSIFY. DB-1 computes the class from its baseline and the reference graph, and DB-1 is not
    /// built.").
    ///
    /// <para>🔴 <b>NOTHING FALLS THROUGH TO RUN.</b> Every (kind, nature) pair either matches a row of
    /// Siemens' RUN-download table as DB-1 transcribes it, or is REFUSED BY NAME. Defaulting an
    /// unclassified change to RUN is how a STOP-class change reaches a wave boundary and stops the CPU
    /// mid-test; defaulting it to STOP is no better, because it launders "nobody worked out what this
    /// is" into a decision (ChangeRouter's own first refusal makes that argument).</para>
    ///
    /// <para>⚠️ <b>THE FIXED-KIND CROSS-CHECK IN <see cref="ChangeRouter"/> IS NOT INDEPENDENT OF THIS
    /// TABLE, AND MUST NOT BE REPORTED AS IF IT WERE.</b> Both read the same DB-1 rows. Routing a
    /// COMPUTED class through the router demonstrates self-consistency and nothing else — a proof is
    /// only as strong as the most independent authority in its loop. The cross-check has real force in
    /// exactly one place: a class an operator DECLARED through <c>--class</c>, which this classifier did
    /// not produce.</para>
    /// </summary>
    public static class ChangeClassifier
    {
        /// <summary>
        /// The kinds whose class DB-1 fixes REGARDLESS OF NATURE. For these the nature is not merely
        /// unnecessary, it is irrelevant — R1 says plainly that "an OB change ... is a STOP-class
        /// change", and hardware configuration, retentivity settings and text lists have a single STOP
        /// row each. So an unknown nature does NOT refuse here, and that is the fail-closed direction:
        /// the worst outcome is an agent waiting for a drain boundary it did not strictly need.
        /// </summary>
        public static readonly IReadOnlyDictionary<ObjectKind, string> FixedStopKinds =
            new Dictionary<ObjectKind, string>
            {
                { ObjectKind.OrganizationBlock, "DB-1 / R1 — new / deleted OB, or OB property change ... STOP. R1 is broader still: 'an OB change ... is a STOP-class change and routes to the deferred queue'. Nature is not consulted." },
                { ObjectKind.TextList, "DB-1 — new / revised text lists ... STOP. Nature is not consulted." },
                { ObjectKind.HardwareConfiguration, "DB-1 — hardware configuration ... STOP. Nature is not consulted." },
                { ObjectKind.RetentivitySetting, "DB-1 — modified retentivity settings ... STOP. Nature is not consulted." },
            };

        /// <summary>
        /// Classify one change. <paramref name="declaredClass"/> is the operator's escape for a
        /// (kind, nature) the table has no row for; it is REFUSED where the table does have one.
        /// </summary>
        public static ClassificationVerdict Classify(
            string name,
            ChangeNature nature,
            IrCorpus corpus,
            ChangeClass declaredClass = ChangeClass.Unknown)
        {
            if (corpus == null)
            {
                throw new ArgumentNullException(nameof(corpus));
            }

            var objectName = (name ?? string.Empty).Trim();

            if (objectName.Length == 0)
            {
                return Refuse(objectName, ObjectKind.Unknown, nature, ClassificationRefusal.ObjectNotInCorpus,
                    "The change set carried an object with no name. Nothing can be looked up, routed or " +
                    "referred to in a load manifest.");
            }

            if (corpus.IsAmbiguous(objectName))
            {
                return Refuse(objectName, ObjectKind.Unknown, nature, ClassificationRefusal.AmbiguousCorpusIdentity,
                    "Two files in " + corpus.Directory + " declare the name '" + objectName + "'. Neither " +
                    "wins: the kind decides the class, and choosing a file would be a guess about which " +
                    "object the change set meant.");
            }

            CorpusObject? found;
            if (!corpus.TryLookup(objectName, out found) || found == null)
            {
                return Refuse(objectName, ObjectKind.Unknown, nature, ClassificationRefusal.ObjectNotInCorpus,
                    "No .ir in " + corpus.Directory + " declares '" + objectName + "', so its KIND is " +
                    "unknown and DB-1's table cannot be entered. NOT defaulted: the table's rows differ " +
                    "by kind between RUN and STOP, so guessing the kind is guessing whether loading it " +
                    "stops the CPU.");
            }

            var kind = found.Kind;

            string? fixedRule;
            if (FixedStopKinds.TryGetValue(kind, out fixedRule) && fixedRule != null)
            {
                return Decide(objectName, kind, nature, ChangeClass.Stop, fixedRule, declaredClass);
            }

            if (nature == ChangeNature.Unknown)
            {
                return Refuse(objectName, kind, nature, ClassificationRefusal.UnknownChangeNature,
                    "'" + objectName + "' is a " + kind + " and the change set did not say whether it was " +
                    "added, deleted or modified. DB-1's table is keyed on BOTH — 'new / deleted DB' is RUN " +
                    "while 'DB interface change' is RUN (Init) — so half a key selects no row.");
            }

            string rule;
            var computed = Row(kind, nature, out rule);

            if (computed == ChangeClass.Unknown)
            {
                var refusal = Refuse(objectName, kind, nature, ClassificationRefusal.NoRuleForThisKindAndNature,
                    "DB-1's change-class table has NO ROW for a " + nature.ToString().ToUpperInvariant() +
                    " " + kind + ". " + rule + " Declare it with --class " + objectName + "=<Run|RunInit|Stop> " +
                    "if you know what the vendor does here; it is not inferred, because the table is known " +
                    "to be incomplete (spec gap G7) and filling a hole by inference invents vendor behaviour.");

                // The declaration is applied ONLY here — where the table has no row. It is never an
                // override, and the verdict it produces is marked FromDeclaration so the router's
                // fixed-kind cross-check on it can be reported as the one check in this path that is
                // NOT reading the same table the class came from.
                return Declared(refusal, declaredClass);
            }

            return Decide(objectName, kind, nature, computed, rule, declaredClass);
        }

        /// <summary>
        /// THE TABLE. One method, so that the rows are in one place and a mutation to any of them is a
        /// mutation to the classifier rather than to one call site among several.
        /// </summary>
        /// <returns><see cref="ChangeClass.Unknown"/> when no row matches; <paramref name="rule"/> then
        /// carries WHY there is no row.</returns>
        /// <remarks>
        /// *** PUBLIC, AND CARRYING THE FIXED-STOP KINDS TOO, BECAUSE OF A MUTATION THAT STAYED
        /// GREEN. *** It was internal, and <see cref="Classify"/> short-circuits the four fixed-STOP
        /// kinds above it while the corpus lookup rejects <see cref="ObjectKind.Unknown"/> below it —
        /// so the <c>default:</c> arm could not be reached from ANY caller, and mutating it to return
        /// RUN left all 54 tests green. That is the "when a mutation you expected to go red stays
        /// green, the finding is in the assertion, not in the mutation" case: the arm was dead code
        /// wearing a fail-closed arm's clothes, in the one place a fail-closed arm matters. Publishing
        /// the table and giving it EVERY row makes the arm reachable, and a test now walks every
        /// <see cref="ObjectKind"/> value through it.
        /// </remarks>
        public static ChangeClass Row(ObjectKind kind, ChangeNature nature, out string rule)
        {
            string? fixedStop;
            if (FixedStopKinds.TryGetValue(kind, out fixedStop) && fixedStop != null)
            {
                rule = fixedStop;
                return ChangeClass.Stop;
            }

            switch (kind)
            {
                case ObjectKind.FunctionBlock:
                case ObjectKind.Function:
                    // "FB/FC code change ... RUN, nothing disturbed" and "New / deleted FB, FC, DB, UDT ... RUN".
                    rule = "DB-1 — " + (nature == ChangeNature.Modified
                        ? "FB/FC code change ... RUN, nothing disturbed"
                        : "new / deleted FB, FC, DB, UDT ... RUN");
                    return ChangeClass.Run;

                case ObjectKind.GlobalDataBlock:
                case ObjectKind.InstanceDataBlock:
                    if (nature == ChangeNature.Added || nature == ChangeNature.Deleted)
                    {
                        rule = "DB-1 — new / deleted FB, FC, DB, UDT ... RUN";
                        return ChangeClass.Run;
                    }

                    // *** THE NARROWING IS DELIBERATE AND IT IS THE ONLY PLACE THIS CLASSIFIER GOES
                    // BEYOND WHAT ITS INPUT PROVES. *** A change set says "this DB changed"; it does not
                    // say whether the change touched the INTERFACE (a row: RUN (Init)) or only START
                    // VALUES (spec gap G7: "no row exists in Siemens' table, checked in both language
                    // renderings"). RUN (Init) is taken because the two candidates share a QUEUE — both
                    // flow through a wave boundary, D23 routes them identically — so the only thing at
                    // stake is whether earlier results are invalidated, and OVER-invalidating costs a
                    // re-run while under-invalidating ships a stale green. Note what this does NOT do:
                    // it never turns a STOP into a RUN, which is the direction that stops a CPU.
                    rule = "DB-1 — DB interface change (no memory reserve) ... RUN (Init), resets, " +
                           "retentives included. NARROWED: the change set cannot distinguish an interface " +
                           "change from a start-values-only edit (spec gap G7, no vendor row); RUN (Init) " +
                           "is taken because both candidates share the RUN queue and it is the " +
                           "over-invalidating of the two. Spec gap G2 additionally disputes whether the " +
                           "reset reaches retentives on this CPU.";
                    return ChangeClass.RunInit;

                case ObjectKind.DataType:
                    if (nature == ChangeNature.Added || nature == ChangeNature.Deleted)
                    {
                        rule = "DB-1 — new / deleted FB, FC, DB, UDT ... RUN";
                        return ChangeClass.Run;
                    }

                    rule = "DB-1 — modified UDT ... RUN (Init) on EVERY DB built on it";
                    return ChangeClass.RunInit;

                case ObjectKind.TagTable:
                    if (nature == ChangeNature.Added)
                    {
                        rule = "DB-1 — comments, new PLC tags ... RUN";
                        return ChangeClass.Run;
                    }

                    // *** REFUSED ON PURPOSE, AND THIS IS THE ROW MOST LIKELY TO BE 'TIDIED' INTO RUN. ***
                    // DB-1's row is "comments, NEW PLC tags ... RUN". A modified tag table may carry a new
                    // tag (RUN) or a RETYPED or READDRESSED one, which the vendor table does not cover at
                    // all — and readdressing a tag is precisely the kind of change that is not free.
                    rule = "DB-1's only tag row is 'comments, NEW PLC tags ... RUN'. A " +
                           nature.ToString().ToUpperInvariant() + " tag table may carry a retyped or " +
                           "readdressed tag, which no row covers.";
                    return ChangeClass.Unknown;

                default:
                    // Reached only by ObjectKind.Unknown. Classify() never arrives here — the corpus
                    // lookup refuses an unstated kind first — but the TABLE is public, and a caller
                    // asking it about an unstated kind must be answered "no row", never "RUN".
                    rule = "No row: DB-1's table does not name this kind of object.";
                    return ChangeClass.Unknown;
            }
        }

        private static ClassificationVerdict Decide(
            string name,
            ObjectKind kind,
            ChangeNature nature,
            ChangeClass computed,
            string rule,
            ChangeClass declared)
        {
            if (declared != ChangeClass.Unknown && declared != computed)
            {
                return Refuse(name, kind, nature, ClassificationRefusal.DeclaredClassContradictsTheTable,
                    "'" + name + "' was declared " + declared + " and DB-1's table computes " + computed +
                    " for a " + nature.ToString().ToUpperInvariant() + " " + kind + ". " + rule +
                    ". Refused rather than applied: --class is the escape for a (kind, nature) the table " +
                    "has NO row for, never an override of one it has.");
            }

            return new ClassificationVerdict(name, kind, nature, computed, ClassificationRefusal.None, rule, string.Empty, false);
        }

        /// <summary>
        /// Take an operator's class for a (kind, nature) the table refused. Kept separate from
        /// <see cref="Classify"/> so that the caller has to route a declaration through
        /// <see cref="ChangeRouter"/> knowing it is a DECLARATION — which is where the router's
        /// fixed-kind cross-check has force it does not have over a computed class.
        /// </summary>
        public static ClassificationVerdict Declared(ClassificationVerdict refused, ChangeClass declared)
        {
            if (refused == null)
            {
                throw new ArgumentNullException(nameof(refused));
            }

            if (declared == ChangeClass.Unknown)
            {
                return refused;
            }

            return new ClassificationVerdict(
                refused.Name,
                refused.Kind,
                refused.Nature,
                declared,
                ClassificationRefusal.None,
                "DECLARED by the operator: " + declared + ". DB-1's table had no row (" + refused.Reason + ")",
                string.Empty,
                true);
        }

        private static ClassificationVerdict Refuse(
            string name,
            ObjectKind kind,
            ChangeNature nature,
            ClassificationRefusal refusal,
            string reason) =>
            new ClassificationVerdict(name, kind, nature, ChangeClass.Unknown, refusal, string.Empty, reason, false);
    }
}
