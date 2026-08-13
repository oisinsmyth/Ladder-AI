using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>Why an excision could not be performed, or should not be.</summary>
    public enum ExcisionDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>Nothing was named to excise.</summary>
        NothingNamed = 1,

        /// <summary>The named object is not in the wave set at all.</summary>
        NotInTheWaveSet = 2,

        /// <summary>
        /// *** THE CLOSURE EXCEEDS THE THRESHOLD. *** Excising the named object drags so much of the
        /// wave set with it that "an ordinary boundary, no disruption spent" stops being true — what is
        /// left is not the wave anybody submitted.
        /// </summary>
        ClosureExceedsTheThreshold = 3,

        /// <summary>The excision would empty the wave set. There is nothing left to re-download.</summary>
        ClosureIsTheWholeWaveSet = 4,
    }

    /// <summary>What excising one object would actually cost.</summary>
    public sealed class ExcisionPlan
    {
        internal ExcisionPlan(
            string requested,
            IReadOnlyList<string> closure,
            IReadOnlyList<string> remaining,
            ExcisionDefect defect,
            string detail)
        {
            Requested = requested;
            Closure = closure;
            Remaining = remaining;
            Defect = defect;
            Detail = detail;
        }

        /// <summary>The object D32 step 4 attributed the configuration to.</summary>
        public string Requested { get; }

        /// <summary>
        /// *** EVERYTHING THAT MUST GO WITH IT. *** Not just the named object: the whole dependency
        /// group, because a wave set that keeps an object whose dependency was excised is no longer
        /// dependency-closed and the re-download is inconsistent (DB-4).
        /// </summary>
        public IReadOnlyList<string> Closure { get; }

        /// <summary>What would still be downloaded.</summary>
        public IReadOnlyList<string> Remaining { get; }

        /// <summary>Why it is refused, or <see cref="ExcisionDefect.None"/>.</summary>
        public ExcisionDefect Defect { get; }

        /// <summary>The reasoning, for the log.</summary>
        public string Detail { get; }

        /// <summary>TRUE when the excision may proceed.</summary>
        public bool Permitted => Defect == ExcisionDefect.None;

        /// <summary>One line for the log.</summary>
        public string ToLogLine() =>
            (Permitted ? "EXCISE " : "EXCISION REFUSED (" + Defect + ") ") + Requested +
            " — closure " + Closure.Count + " object(s) [" + string.Join(", ", Closure.ToArray()) + "], " +
            Remaining.Count + " remaining :: " + Detail;

        /// <inheritdoc />
        public override string ToString() => ToLogLine();
    }

    /// <summary>
    /// D32 step 5 — EXCISE the attributable block from the wave set, route it to the deferred queue,
    /// and re-download without it. *** THE EXCISION IS A CLOSURE, NOT AN OBJECT. ***
    /// </summary>
    /// <remarks>
    /// <para>
    /// Removing one object from a dependency-closed batch leaves the rest NOT closed: DB-4's own
    /// sentence is that "an FB, its instance DB and its UDT must land together or the program is
    /// inconsistent between downloads". So an excision takes the whole WEAKLY-CONNECTED COMPONENT the
    /// object belongs to — computed by the same rule <see cref="WaveBoundaryBatchPlanner"/> uses, and
    /// re-verified afterwards by <see cref="DependencyClosure"/>, which is written from the definition
    /// rather than from the component algebra.
    /// </para>
    /// <para>
    /// *** THE THRESHOLD IS A REQUIRED ARGUMENT WITH NO DEFAULT, AND THAT IS DELIBERATE. *** D32 calls
    /// step 5 "an ordinary boundary; no disruption is spent", which is true when the closure is a
    /// block and its instance DB and false when it is nineteen objects out of twenty — at that point
    /// the wave being re-downloaded is not the wave anybody submitted, and continuing to excise is a
    /// way of reaching a green by removing everything that could fail. *** NO MEASURED VALUE FOR THE
    /// THRESHOLD EXISTS ***, so this type will not supply one: the caller chooses it, the choice is
    /// recorded in the plan's own detail line, and nothing here pretends the number came from anywhere.
    /// </para>
    /// </remarks>
    public static class ExcisionClosure
    {
        /// <summary>Work out what excising <paramref name="objectName"/> would cost.</summary>
        /// <param name="objectName">The object D32 step 4 attributed the configuration to.</param>
        /// <param name="waveSet">The wave set's changed objects, with their declared dependencies.</param>
        /// <param name="deployed">The baseline, so the remaining set can be re-checked for closure.</param>
        /// <param name="maxClosureFraction">
        /// The largest fraction of the wave set an excision may take, exclusive of 1.0. REQUIRED — no
        /// measured value exists and this type will not invent one.
        /// </param>
        public static ExcisionPlan Plan(
            string? objectName,
            IEnumerable<ChangedObject>? waveSet,
            DeployedProgram deployed,
            double maxClosureFraction)
        {
            if (deployed == null)
            {
                throw new ArgumentNullException(nameof(deployed));
            }

            if (maxClosureFraction <= 0.0 || maxClosureFraction > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxClosureFraction),
                    "The threshold must be a fraction in (0, 1]. There is no default because no measured " +
                    "value exists: D32 says step 5 is 'an ordinary boundary', which is true for a small " +
                    "closure and false for one that removes most of the wave, and nobody has measured " +
                    "where that turns over.");
            }

            var objects = (waveSet ?? Enumerable.Empty<ChangedObject>())
                .Where(o => o != null && o.Name.Length > 0)
                .ToArray();

            var requested = (objectName ?? string.Empty).Trim();

            if (requested.Length == 0)
            {
                return new ExcisionPlan(
                    "<nothing named>",
                    new string[0],
                    objects.Select(o => o.Name).ToArray(),
                    ExcisionDefect.NothingNamed,
                    "D32 step 5 requires an ATTRIBUTED block. Excising nothing and re-downloading would " +
                    "repeat the download that just aborted, against an unchanged situation.");
            }

            var byName = new Dictionary<string, ChangedObject>(ChangedObject.NameComparer);
            foreach (var o in objects)
            {
                byName[o.Name] = o;
            }

            if (!byName.ContainsKey(requested))
            {
                return new ExcisionPlan(
                    requested,
                    new string[0],
                    objects.Select(o => o.Name).ToArray(),
                    ExcisionDefect.NotInTheWaveSet,
                    "'" + requested + "' is not in the wave set, so there is nothing to excise from it. " +
                    "D32 caveat 1: this is where the log must distinguish 'no block is responsible' from " +
                    "'the locator failed to find one' — they take the same branch and call for opposite " +
                    "fixes.");
            }

            var closure = ComponentContaining(requested, byName);
            var remaining = objects
                .Select(o => o.Name)
                .Where(n => !closure.Contains(n, ChangedObject.NameComparer))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (remaining.Length == 0)
            {
                return new ExcisionPlan(
                    requested,
                    closure,
                    remaining,
                    ExcisionDefect.ClosureIsTheWholeWaveSet,
                    "The closure is the entire wave set — every object depends, directly or transitively, " +
                    "on the attributed one. There is no reduced download to make, so step 5 cannot make " +
                    "progress and the ladder must move on rather than re-download nothing.");
            }

            var fraction = (double)closure.Count / objects.Length;
            if (fraction > maxClosureFraction)
            {
                return new ExcisionPlan(
                    requested,
                    closure,
                    remaining,
                    ExcisionDefect.ClosureExceedsTheThreshold,
                    "Excising '" + requested + "' takes " + closure.Count + " of " + objects.Length +
                    " objects (" + fraction.ToString("0.00") + ") and the caller's threshold is " +
                    maxClosureFraction.ToString("0.00") + ". D32 calls step 5 an ordinary boundary, which " +
                    "it stops being once the wave being re-downloaded is not the wave anybody submitted — " +
                    "and excising until the failures are gone is a way of reaching a green by removing " +
                    "everything that could fail. NOTE: the threshold is the caller's choice; no measured " +
                    "value exists.");
            }

            // Re-verify with the checker that did not build the closure — the same separation
            // WaveBoundaryBatchPlanner uses against its own packer.
            var remainingObjects = objects.Where(o => remaining.Contains(o.Name, ChangedObject.NameComparer)).ToArray();
            var violations = DependencyClosure.Verify(new[] { (IEnumerable<string>)remaining }, remainingObjects, deployed);

            if (violations.Count > 0)
            {
                return new ExcisionPlan(
                    requested,
                    closure,
                    remaining,
                    ExcisionDefect.ClosureExceedsTheThreshold,
                    "The set left after excising '" + requested + "' is NOT dependency-closed, checked " +
                    "independently of the component walk that produced the closure: " +
                    string.Join(" | ", violations.Select(v => v.ToString()).ToArray()) +
                    ". Re-downloading it would be inconsistent, which is the thing step 5 exists to avoid.");
            }

            return new ExcisionPlan(
                requested,
                closure,
                remaining,
                ExcisionDefect.None,
                "The whole dependency group goes, because a set that keeps an object whose dependency was " +
                "excised is no longer closed (DB-4). " + closure.Count + " of " + objects.Length +
                " objects, within the caller's threshold of " + maxClosureFraction.ToString("0.00") +
                ". The remainder was re-verified closed by the independent checker.");
        }

        private static IReadOnlyList<string> ComponentContaining(string seed, Dictionary<string, ChangedObject> byName)
        {
            var component = new HashSet<string>(ChangedObject.NameComparer) { seed };
            var frontier = new Queue<string>();
            frontier.Enqueue(seed);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                // Undirected on purpose: an object and anything it depends on must land together, and so
                // must anything that depends on IT — a dependent left behind is the open batch.
                var neighbours = byName[current].DependsOn
                    .Where(byName.ContainsKey)
                    .Concat(byName.Values
                        .Where(o => o.DependsOn.Contains(current, ChangedObject.NameComparer))
                        .Select(o => o.Name));

                foreach (var neighbour in neighbours)
                {
                    if (component.Add(neighbour))
                    {
                        frontier.Enqueue(neighbour);
                    }
                }
            }

            return component.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
