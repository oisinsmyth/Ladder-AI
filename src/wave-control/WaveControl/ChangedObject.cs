using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// One object in a change set: what it is, what class of change it carries, what it depends on, and
    /// the hash of the artifact the change was authored from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THIS TYPE ACCEPTS BAD INPUT ON PURPOSE. *** It is the shape a change arrives in from DB-1 or
    /// from an agent's submission, so a blank name, an <see cref="ObjectKind.Unknown"/> kind and a
    /// <see cref="ChangeClass.Unknown"/> class are all constructible — and every one of them is a
    /// REFUSAL WITH A REASON further down (<see cref="ChangeRouter"/>, <see cref="AdmissionController"/>),
    /// never a throw here. The precedent is <see cref="RaisedConfiguration"/>, which accepts an unnamed
    /// configuration so the classifier can report why it is unusable rather than the caller crashing
    /// before the report exists.
    /// </para>
    /// <para>
    /// DEPENDENCIES ARE DECLARED, NOT COMPUTED. `converter cross-check` computes the reference graph and
    /// DB-1 owns the blast radius; neither is built into this library, which references nothing. What is
    /// built here is what CONSUMES that graph: the closure rule that decides whether a batch may be
    /// downloaded (DB-4). A dependency list that is wrong produces a batch plan that is wrong, and the
    /// cross-check that would catch it is §9a's load manifest against DB-1's prediction — named in the
    /// spec, and not this component's.
    /// </para>
    /// <para>
    /// WHAT A DEPENDENCY MEANS HERE: "must already be present, or land in the same download". An
    /// instance DB depends on its FB; a DB depends on the UDT it is built from; a block depends on the
    /// blocks it calls. DB-4's sentence is the definition — "an FB, its instance DB and its UDT must
    /// land together or the program is inconsistent between downloads".
    /// </para>
    /// </remarks>
    public sealed class ChangedObject
    {
        /// <summary>Object names are compared case-insensitively, as elsewhere in this library.</summary>
        internal static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        /// <param name="name">The object's name as TIA knows it.</param>
        /// <param name="kind">What kind of object it is.</param>
        /// <param name="changeClass">DB-1's class for THIS change to it.</param>
        /// <param name="dependsOn">
        /// Names of the objects that must be present, or land in the same download, for this one to be
        /// consistent. Blank entries are dropped; duplicates collapse; a self-reference is dropped,
        /// because an object trivially lands with itself and treating it as an edge would only make
        /// every component one bigger.
        /// </param>
        /// <param name="artifactHash">
        /// The `converter ir-hash` of the artifact this change was authored from, or any other stable
        /// content hash the caller uses consistently. It exists so admission evidence can be shown to be
        /// about THIS content rather than about whatever the object looked like two edits ago. May be
        /// blank here; <see cref="AdmissionController"/> refuses a blank one, with a reason.
        /// </param>
        public ChangedObject(
            string? name,
            ObjectKind kind,
            ChangeClass changeClass,
            IEnumerable<string>? dependsOn = null,
            string? artifactHash = null)
        {
            Name = (name ?? string.Empty).Trim();
            Kind = kind;
            ChangeClass = changeClass;
            ArtifactHash = (artifactHash ?? string.Empty).Trim();

            var dependencies = (dependsOn ?? Enumerable.Empty<string>())
                .Select(d => (d ?? string.Empty).Trim())
                .Where(d => d.Length > 0)
                .Where(d => !NameComparer.Equals(d, Name))
                .Distinct(NameComparer)
                .ToArray();

            DependsOn = dependencies;
        }

        /// <summary>The object's name. May be empty — that is a refusal downstream, not a throw here.</summary>
        public string Name { get; }

        /// <summary>What kind of object it is.</summary>
        public ObjectKind Kind { get; }

        /// <summary>DB-1's class for this change.</summary>
        public ChangeClass ChangeClass { get; }

        /// <summary>What must land with it, or already be present.</summary>
        public IReadOnlyList<string> DependsOn { get; }

        /// <summary>Content hash of the artifact this change was authored from. May be empty.</summary>
        public string ArtifactHash { get; }

        /// <summary>
        /// TRUE when this change resets data values, retentives included — DB-1's RUN (Init) row. It
        /// still flows through a wave boundary; what it invalidates is every result obtained before it
        /// (DB-2's validity stamp).
        /// </summary>
        public bool ResetsData => ChangeClass == ChangeClass.RunInit;

        /// <inheritdoc />
        public override string ToString()
        {
            var name = Name.Length == 0 ? "<unnamed>" : Name;
            var dependencies = DependsOn.Count == 0
                ? "no declared dependencies"
                : "depends on " + string.Join(", ", DependsOn.ToArray());

            return name + " (" + Kind + ", " + ChangeClass + ", " + dependencies + ")";
        }
    }
}
