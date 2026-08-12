using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// What was in flight. The payload of the persisted wave-in-progress marker (spec §16.3, X-C
    /// item 2).
    /// </summary>
    /// <remarks>
    /// X-C names the contents: the wave id, the slots, the program version it ran against, and a
    /// timestamp. Everything here is required, because a marker that cannot say WHAT was in flight
    /// only tells a restarted coordinator that something was — and the whole reason the ruling is
    /// defensible ("every test and result in flight is INVALID") is that the coordinator can name
    /// what to invalidate.
    /// </remarks>
    public sealed class WaveMarker
    {
        /// <param name="waveId">Identifies the wave. Must be unique enough to name in a log.</param>
        /// <param name="slots">The slots in flight. At least one — a wave with nothing in flight is not a wave.</param>
        /// <param name="programVersion">
        /// The program version the wave ran against — the version register's key (§9). On restart the
        /// register can confirm the PROGRAM is intact; it says nothing about whether a test completed
        /// (X-C item 3), so this identifies what to re-check, never what to trust.
        /// </param>
        /// <param name="startedUtc">When the wave was begun.</param>
        /// <param name="coordinator">Who began it. Defaults to the current process.</param>
        public WaveMarker(
            string waveId,
            IEnumerable<string> slots,
            string programVersion,
            DateTimeOffset startedUtc,
            CoordinatorIdentity? coordinator = null)
        {
            WaveId = Required(waveId, nameof(waveId));
            ProgramVersion = Required(programVersion, nameof(programVersion));

            if (startedUtc == default(DateTimeOffset))
            {
                throw new ArgumentException(
                    "A wave marker needs a real start time. A default timestamp would make a marker " +
                    "found on startup un-ageable, and the age is how a human tells last night's crash " +
                    "from one five minutes ago.",
                    nameof(startedUtc));
            }

            StartedUtc = startedUtc.ToUniversalTime();

            var list = (slots ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .ToList();

            if (list.Count == 0 || list.Any(s => s.Length == 0))
            {
                throw new ArgumentException(
                    "A wave marker must name at least one slot, and no slot may be blank. A marker that " +
                    "says a wave was in flight but not WHAT was in flight cannot be acted on, and an " +
                    "empty slot list is not the same thing as a clean start (FI-44).",
                    nameof(slots));
            }

            if (list.Distinct(StringComparer.Ordinal).Count() != list.Count)
            {
                throw new ArgumentException("A wave marker's slot list contains a duplicate.", nameof(slots));
            }

            Slots = list.ToArray();
            Coordinator = coordinator ?? CoordinatorIdentity.Current;
        }

        /// <summary>Identifies the wave.</summary>
        public string WaveId { get; }

        /// <summary>The slots that were in flight.</summary>
        public IReadOnlyList<string> Slots { get; }

        /// <summary>The program version the wave ran against.</summary>
        public string ProgramVersion { get; }

        /// <summary>When the wave was begun, in UTC.</summary>
        public DateTimeOffset StartedUtc { get; }

        /// <summary>Who began it.</summary>
        public CoordinatorIdentity Coordinator { get; }

        /// <summary>How long ago the wave was begun, measured against <paramref name="now"/>.</summary>
        public TimeSpan Age(DateTimeOffset now) => now.ToUniversalTime() - StartedUtc;

        /// <inheritdoc />
        public override string ToString() =>
            "wave " + WaveId + " (" + Slots.Count + " slot(s): " + string.Join(",", Slots.ToArray()) +
            ") against program " + ProgramVersion + ", started " +
            StartedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") + " by " + Coordinator;

        private static string Required(string value, string parameterName)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException("A wave marker requires '" + parameterName + "'.", parameterName);
            }

            return trimmed;
        }
    }
}
