using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>
    /// One queue entry as it was read back off disk — before it has been re-gated, and therefore NOT
    /// yet a <see cref="QueuedSubmission"/>.
    /// </summary>
    /// <remarks>
    /// *** THE TYPE DISTINCTION IS THE GUARD. *** A restored entry is not a queued one until
    /// <see cref="QueueRehydration"/> has re-run the admission gate against the content's CURRENT
    /// hash. Parsing straight into <see cref="QueuedSubmission"/> would make "read from disk" and
    /// "admitted" the same state, and the reload would silently promote a gated item into an ungated
    /// one — which is exactly the hole this whole component exists to close.
    /// </remarks>
    public sealed class PersistedQueueEntry
    {
        internal PersistedQueueEntry(
            Submission submission,
            IReadOnlyList<AdmissionEvidence> evidence,
            DownloadQueue queue,
            string reason,
            DateTimeOffset enqueuedUtc,
            int sequence,
            int waveBoundariesWaited,
            bool excised)
        {
            Submission = submission;
            Evidence = evidence;
            Queue = queue;
            Reason = reason;
            EnqueuedUtc = enqueuedUtc;
            Sequence = sequence;
            WaveBoundariesWaited = waveBoundariesWaited;
            Excised = excised;
        }

        /// <summary>The submission, with the objects and the artifact hashes it was admitted with.</summary>
        public Submission Submission { get; }

        /// <summary>The admission evidence it was admitted on.</summary>
        public IReadOnlyList<AdmissionEvidence> Evidence { get; }

        /// <summary>The queue it was in when the state was written.</summary>
        public DownloadQueue Queue { get; }

        /// <summary>The routing argument, or D32 step 5's excision record.</summary>
        public string Reason { get; }

        /// <summary>When it entered the queues.</summary>
        public DateTimeOffset EnqueuedUtc { get; }

        /// <summary>Arrival order.</summary>
        public int Sequence { get; }

        /// <summary>How many wave boundaries it had already waited.</summary>
        public int WaveBoundariesWaited { get; }

        /// <summary>
        /// TRUE when it reached the deferred queue by EXCISION (D32 step 5) rather than by its change
        /// class. See <see cref="QueueRehydrator"/> — this is what makes the reload's queue cross-check
        /// asymmetric rather than an equality.
        /// </summary>
        public bool Excised { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Submission.Id + " (" + Queue + ", " + Submission.Objects.Count + " object(s), waited " +
            WaveBoundariesWaited + ")";
    }

    /// <summary>
    /// The queues' on-disk form: a line-oriented UTF-8 text file with a DECLARED ENTRY COUNT and a
    /// trailing sentinel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** BOTH TRUNCATION DETECTORS ARE HERE ON PURPOSE, AND THEY CATCH DIFFERENT TEARS. *** The
    /// sentinel catches a write that stopped part way through the file — the case
    /// <see cref="WaveMarkerFormat"/> already argues. The declared <c>entries=</c> count catches the
    /// case a sentinel cannot: a file whose entries were serialised short. A queue file's whole
    /// content is a COUNT of things, so "parsed cleanly, carrying fewer entries than were written" is
    /// the failure mode that would otherwise look exactly like a shorter queue — and a shorter queue
    /// is work that silently never happens.
    /// </para>
    /// <para>
    /// Same format choices and the same reasons as the marker: no JSON (the library references
    /// nothing, so it stays referenceable from <c>openness-cli</c> on net48 without dragging a package
    /// graph into the one tool whose every rebuild costs a TIA (Path,FileHash) re-approval), and it is
    /// readable by a human standing at the machine. UNKNOWN KEYS ARE A PARSE FAILURE, NOT A SKIP.
    /// </para>
    /// <para>
    /// ONE HONEST LIMIT OF THE ESCAPING, and it fails in the safe direction. Object and evidence lines
    /// pack five fields separated by TAB, and the line escape turns a tab into <c>\t</c> whether it is
    /// a separator or part of a value. So a name genuinely containing a tab would come back as SIX
    /// fields — which is a PARSE REFUSAL naming the line, never a silently mis-split value. A refusal
    /// on a pathological name is the correct trade against a value that reassembles wrongly.
    /// </para>
    /// </remarks>
    internal static class WaveQueueFormat
    {
        internal const int CurrentFormatVersion = 1;
        internal const string Sentinel = "end";

        private const string HeaderComment =
            "# ladder wave queues (spec D23) - the RUN queue and the DEFERRED queue, persisted.\n" +
            "# Its ABSENCE is not an empty queue: absent and unreadable are different states and\n" +
            "# neither may be read as 'nothing was deferred' (FI-44).\n" +
            "# Every entry here was ADMITTED - preflight passed, isolated compile passed, hash matched.\n" +
            "# It is re-gated against the content's CURRENT hash on reload before it may drain.\n";

        internal static string Serialize(IReadOnlyList<QueuedSubmission> entries)
        {
            var sb = new StringBuilder();
            sb.Append(HeaderComment);
            Append(sb, "format", CurrentFormatVersion.ToString(CultureInfo.InvariantCulture));
            Append(sb, "entries", entries.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var entry in entries.OrderBy(e => e.Sequence))
            {
                Append(sb, "entry", entry.Submission.Id);
                Append(sb, "agent", entry.Submission.Agent);
                Append(sb, "queue", entry.Queue.ToString());
                Append(sb, "reason", entry.Reason);
                Append(sb, "enqueued-utc", entry.EnqueuedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                Append(sb, "sequence", entry.Sequence.ToString(CultureInfo.InvariantCulture));
                Append(sb, "waited", entry.WaveBoundariesWaited.ToString(CultureInfo.InvariantCulture));
                Append(sb, "excised", entry.Excised ? "true" : "false");

                foreach (var o in entry.Submission.Objects)
                {
                    Append(sb, "object", string.Join("\t", new[]
                    {
                        o.Name,
                        o.Kind.ToString(),
                        o.ChangeClass.ToString(),
                        o.ArtifactHash,
                        string.Join(",", o.DependsOn.ToArray()),
                    }));
                }

                foreach (var e in entry.Evidence)
                {
                    Append(sb, "evidence", string.Join("\t", new[]
                    {
                        e.ObjectName,
                        e.ArtifactHash,
                        e.Preflight.ToString(),
                        e.CompiledCleanInIsolation.ToString(),
                        e.Source,
                    }));
                }
            }

            sb.Append(Sentinel).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Parses queue-state text. Returns null and sets <paramref name="problem"/> for anything it
        /// does not fully understand — the caller turns that into "the queue state is UNREADABLE",
        /// never into "the queues were empty".
        /// </summary>
        internal static IReadOnlyList<PersistedQueueEntry>? TryParse(string text, out string problem)
        {
            problem = string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                problem = "the queue-state file is present but empty — the write did not complete";
                return null;
            }

            var meaningful = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
                .ToList();

            if (meaningful.Count == 0)
            {
                problem = "the queue-state file carries no content lines — the write did not get past its header";
                return null;
            }

            if (!string.Equals(meaningful[meaningful.Count - 1], Sentinel, StringComparison.Ordinal))
            {
                problem = "the queue-state file has no '" + Sentinel + "' terminator — the write was torn off part way";
                return null;
            }

            meaningful.RemoveAt(meaningful.Count - 1);

            string? formatText = null;
            string? declaredCountText = null;
            var entries = new List<PersistedQueueEntry>();

            string? id = null;
            string? agent = null;
            string? queueText = null;
            string? reason = null;
            string? enqueuedText = null;
            string? sequenceText = null;
            string? waitedText = null;
            string? excisedText = null;
            var objects = new List<ChangedObject>();
            var evidence = new List<AdmissionEvidence>();

            Func<string?> flush = () =>
            {
                if (id == null)
                {
                    return null;
                }

                DownloadQueue queue;
                if (queueText == null || !TryParseQueue(queueText, out queue))
                {
                    return "entry '" + id + "' states no readable queue";
                }

                DateTimeOffset enqueuedUtc;
                if (enqueuedText == null ||
                    !DateTimeOffset.TryParse(
                        enqueuedText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out enqueuedUtc))
                {
                    return "entry '" + id + "' states no readable enqueue time";
                }

                int sequence;
                int waited;
                if (sequenceText == null || !int.TryParse(sequenceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence) ||
                    waitedText == null || !int.TryParse(waitedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out waited))
                {
                    return "entry '" + id + "' states no readable sequence or wait count";
                }

                if (objects.Count == 0)
                {
                    return "entry '" + id + "' carries no objects — an admitted submission always has at least one";
                }

                // Not defaulted to false: excised=false and "the writer never said" are opposite claims
                // about whether a RUN-class entry belongs in the deferred queue, and guessing the
                // permissive one would let a tampered or truncated entry through the reload's queue
                // cross-check.
                if (excisedText != "true" && excisedText != "false")
                {
                    return "entry '" + id + "' does not state whether it was excised";
                }

                entries.Add(new PersistedQueueEntry(
                    new Submission(id, agent, objects.ToArray()),
                    evidence.ToArray(),
                    queue,
                    reason ?? string.Empty,
                    enqueuedUtc,
                    sequence,
                    waited,
                    excisedText == "true"));

                id = null;
                agent = null;
                queueText = null;
                reason = null;
                enqueuedText = null;
                sequenceText = null;
                waitedText = null;
                excisedText = null;
                objects = new List<ChangedObject>();
                evidence = new List<AdmissionEvidence>();
                return null;
            };

            foreach (var line in meaningful)
            {
                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    problem = "the queue-state file carries a line that is not 'key=value': '" + Truncate(line) + "'";
                    return null;
                }

                var key = line.Substring(0, split).Trim();
                var value = Unescape(line.Substring(split + 1));

                switch (key)
                {
                    case "format":
                        formatText = value;
                        break;

                    case "entries":
                        declaredCountText = value;
                        break;

                    case "entry":
                        var flushProblem = flush();
                        if (flushProblem != null)
                        {
                            problem = flushProblem;
                            return null;
                        }

                        id = value;
                        break;

                    case "agent": agent = value; break;
                    case "queue": queueText = value; break;
                    case "reason": reason = value; break;
                    case "enqueued-utc": enqueuedText = value; break;
                    case "sequence": sequenceText = value; break;
                    case "waited": waitedText = value; break;
                    case "excised": excisedText = value; break;

                    case "object":
                    {
                        var fields = value.Split('\t');
                        if (fields.Length != 5)
                        {
                            problem = "an object line has " + fields.Length + " fields, not 5: '" + Truncate(value) + "'";
                            return null;
                        }

                        ObjectKind kind;
                        ChangeClass changeClass;
                        if (!TryParseKind(fields[1], out kind) || !TryParseClass(fields[2], out changeClass))
                        {
                            problem = "an object line states a kind or change class this build does not know: '" +
                                      Truncate(value) + "'";
                            return null;
                        }

                        var dependsOn = fields[4].Length == 0 ? new string[0] : fields[4].Split(',');
                        objects.Add(new ChangedObject(fields[0], kind, changeClass, dependsOn, fields[3]));
                        break;
                    }

                    case "evidence":
                    {
                        var fields = value.Split('\t');
                        if (fields.Length != 5)
                        {
                            problem = "an evidence line has " + fields.Length + " fields, not 5: '" + Truncate(value) + "'";
                            return null;
                        }

                        EvidenceOutcome preflight;
                        EvidenceOutcome compile;
                        if (!TryParseOutcome(fields[2], out preflight) || !TryParseOutcome(fields[3], out compile))
                        {
                            problem = "an evidence line states an outcome this build does not know: '" +
                                      Truncate(value) + "'";
                            return null;
                        }

                        evidence.Add(new AdmissionEvidence(fields[0], fields[1], preflight, compile, fields[4]));
                        break;
                    }

                    default:
                        problem = "the queue-state file carries a key this build does not know ('" + Truncate(key) +
                                  "') — either corruption, or a file written by a newer format that did not " +
                                  "bump its version";
                        return null;
                }
            }

            var tailProblem = flush();
            if (tailProblem != null)
            {
                problem = tailProblem;
                return null;
            }

            int format;
            if (formatText == null || !int.TryParse(formatText, NumberStyles.Integer, CultureInfo.InvariantCulture, out format))
            {
                problem = "the queue-state file states no readable format version";
                return null;
            }

            if (format != CurrentFormatVersion)
            {
                problem = "the queue-state file is format " + format + "; this build reads format " + CurrentFormatVersion;
                return null;
            }

            int declaredCount;
            if (declaredCountText == null ||
                !int.TryParse(declaredCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out declaredCount))
            {
                problem = "the queue-state file declares no readable entry count";
                return null;
            }

            // *** THE CHECK THE SENTINEL CANNOT MAKE. *** A file that parses cleanly but carries fewer
            // entries than it declares is a SHORTER QUEUE, and a shorter queue is admitted work that
            // silently never happens.
            if (declaredCount != entries.Count)
            {
                problem = "the queue-state file declares " + declaredCount + " entries and carries " +
                          entries.Count + ". A queue read short is work that quietly never happens, so " +
                          "this is refused rather than accepted as a shorter queue";
                return null;
            }

            var duplicate = entries
                .GroupBy(e => e.Submission.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                problem = "the queue-state file carries submission id '" + duplicate.Key + "' more than once";
                return null;
            }

            return entries;
        }

        private static bool TryParseQueue(string value, out DownloadQueue queue)
        {
            switch (value)
            {
                case "RunQueue": queue = DownloadQueue.RunQueue; return true;
                case "DeferredQueue": queue = DownloadQueue.DeferredQueue; return true;
                default: queue = DownloadQueue.Unassigned; return false;
            }
        }

        private static bool TryParseKind(string value, out ObjectKind kind)
        {
            // Deliberately NOT Enum.Parse: "Unknown" is a legal enum name and would round-trip a
            // kindless object back into the queues as though it had been checked.
            foreach (ObjectKind candidate in Enum.GetValues(typeof(ObjectKind)))
            {
                if (candidate != ObjectKind.Unknown && string.Equals(candidate.ToString(), value, StringComparison.Ordinal))
                {
                    kind = candidate;
                    return true;
                }
            }

            kind = ObjectKind.Unknown;
            return false;
        }

        private static bool TryParseClass(string value, out ChangeClass changeClass)
        {
            foreach (ChangeClass candidate in Enum.GetValues(typeof(ChangeClass)))
            {
                if (candidate != ChangeClass.Unknown && string.Equals(candidate.ToString(), value, StringComparison.Ordinal))
                {
                    changeClass = candidate;
                    return true;
                }
            }

            changeClass = ChangeClass.Unknown;
            return false;
        }

        private static bool TryParseOutcome(string value, out EvidenceOutcome outcome)
        {
            switch (value)
            {
                case "Passed": outcome = EvidenceOutcome.Passed; return true;
                case "Failed": outcome = EvidenceOutcome.Failed; return true;
                case "NotProvided": outcome = EvidenceOutcome.NotProvided; return true;
                default: outcome = EvidenceOutcome.NotProvided; return false;
            }
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append('=').Append(Escape(value)).Append('\n');
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        private static string Unescape(string value)
        {
            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    sb.Append(value[i]);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    default: sb.Append(value[i]); break;
                }
            }

            return sb.ToString();
        }

        private static string Truncate(string value) =>
            value.Length <= 40 ? value : value.Substring(0, 40) + "...";
    }
}
