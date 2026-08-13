using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// The set of objects believed to be ON THE DEVICE, with the provenance of that belief. It is the
    /// baseline R3 stage 2 measures against, and the thing that decides whether a dependency is
    /// already satisfied or has to land in the same batch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** AN EMPTY BASELINE MUST BE DECLARED, NEVER DEFAULTED. *** There is no parameterless
    /// constructor and no implicit empty instance. "The controller holds nothing" and "we could not
    /// read what the controller holds" produce identical object graphs and opposite correct actions:
    /// the first makes every dependency unsatisfied and every batch enormous, the second must stop the
    /// wave. FI-44's rule — empty is not clean — is enforced structurally, by making the caller say
    /// which one it means through <see cref="DeclaredEmpty"/> and give a reason.
    /// </para>
    /// <para>
    /// *** WHAT THIS IS NOT: EVIDENCE ABOUT THE DEVICE. *** DB-1 records the caveat and it is repeated
    /// here because this type is where it bites — `openness-cli export-all` reads the OFFLINE PROJECT,
    /// Openness has no upload path, and so a baseline built that way describes TIA's idea of the
    /// program. The two things that describe the DEVICE are §9a's load manifest (what the last download
    /// actually moved) and §9's version register (what is running now). Callers should prefer the
    /// manifest, and the provenance string is where they say which they used.
    /// </para>
    /// </remarks>
    public sealed class DeployedProgram
    {
        private readonly HashSet<string> _objects;

        private DeployedProgram(IEnumerable<string> objectNames, string provenance, bool declaredEmpty)
        {
            _objects = new HashSet<string>(objectNames, ChangedObject.NameComparer);
            Provenance = provenance;
            DeclaredEmpty = declaredEmpty;
        }

        /// <summary>Where the belief came from — a load manifest, a baseline export, a version register read.</summary>
        public string Provenance { get; }

        /// <summary>
        /// TRUE when the caller EXPLICITLY declared the device to hold none of the objects in question,
        /// rather than simply handing over an empty list.
        /// </summary>
        public bool DeclaredEmpty { get; }

        /// <summary>The object names, in the order they will be reported.</summary>
        public IReadOnlyCollection<string> Objects => _objects.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

        /// <summary>How many objects the baseline names.</summary>
        public int Count => _objects.Count;

        /// <summary>TRUE when <paramref name="objectName"/> is already on the device per this baseline.</summary>
        public bool Contains(string? objectName) =>
            !string.IsNullOrWhiteSpace(objectName) && _objects.Contains(objectName!.Trim());

        /// <summary>
        /// A baseline built from a real read.
        /// </summary>
        /// <param name="objectNames">The objects the read reported. Must be non-empty — see <see cref="Empty"/>.</param>
        /// <param name="provenance">Where the read came from, e.g. "load manifest of download 2026-08-13T09:14Z".</param>
        public static DeployedProgram From(IEnumerable<string> objectNames, string provenance)
        {
            var names = (objectNames ?? Enumerable.Empty<string>())
                .Select(n => (n ?? string.Empty).Trim())
                .Where(n => n.Length > 0)
                .ToArray();

            var source = (provenance ?? string.Empty).Trim();
            if (source.Length == 0)
            {
                throw new ArgumentException(
                    "A deployed-program baseline must say where it came from. Every dependency decision " +
                    "and every batching decision hangs off it (§9a(c)), and a baseline nobody can trace " +
                    "is one nobody can falsify.",
                    nameof(provenance));
            }

            if (names.Length == 0)
            {
                throw new ArgumentException(
                    "An empty baseline must be declared through DeployedProgram.Empty, with a reason. " +
                    "'the device holds nothing' and 'we could not read what the device holds' are the " +
                    "same empty list and opposite decisions — empty is not clean (FI-44).",
                    nameof(objectNames));
            }

            return new DeployedProgram(names, source, declaredEmpty: false);
        }

        /// <summary>
        /// A baseline the caller EXPLICITLY declares empty — a bare rig, a fresh test project, the
        /// first download of a wave set.
        /// </summary>
        /// <param name="reason">Why it is empty, and how the caller knows.</param>
        public static DeployedProgram Empty(string reason)
        {
            var stated = (reason ?? string.Empty).Trim();
            if (stated.Length == 0)
            {
                throw new ArgumentException(
                    "Declaring the deployed program empty requires a reason. This is the declaration " +
                    "that every dependency is unsatisfied and every changed object must land in the " +
                    "batch, so it must be a claim somebody made rather than a default.",
                    nameof(reason));
            }

            return new DeployedProgram(Enumerable.Empty<string>(), stated, declaredEmpty: true);
        }

        /// <inheritdoc />
        public override string ToString() =>
            (DeclaredEmpty ? "deployed program DECLARED EMPTY" : "deployed program of " + Count + " object(s)") +
            " [" + Provenance + "]";
    }
}
