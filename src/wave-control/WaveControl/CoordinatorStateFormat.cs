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
    /// <see cref="QueueRehydrator"/> has re-run the admission gate against the content's CURRENT
    /// hash. Parsing straight into <see cref="QueuedSubmission"/> would make "read from disk" and
    /// "admitted" the same state, and the reload would silently promote a gated item into an ungated
    /// one — which is exactly the hole this component exists to close.
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
    /// The coordinator's whole persisted state — X-C's wave-in-progress marker AND D23's two queues —
    /// as ONE line-oriented UTF-8 file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHY ONE FILE (owner's ruling, 2026-08-13). *** The two states are only ever read TOGETHER,
    /// on restart. Splitting them buys nothing and costs a reconciliation rule — and the crash that
    /// produces a disagreement between them is exactly the one where you cannot ask the coordinator
    /// what it was doing, so a rule requiring interpretation at that moment is a rule that fails when
    /// it is needed. The two disagreements now structurally impossible:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     QUEUE WRITTEN, MARKER NOT CLEARED — a restart believes a wave is in flight that is not, and
    ///     VOIDS RESULTS THAT WERE VALID.
    ///   </description></item>
    ///   <item><description>
    ///     MARKER CLEARED, QUEUE NOT WRITTEN — an item dequeued in memory but never persisted RUNS
    ///     TWICE, OR IS LOST.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** THIS IS A CHANGE OF CONTAINER, NOT OF SEMANTICS. *** Everything both formats did survives:
    /// the trailing sentinel, the declared entry count, unknown keys as a parse failure, and — the
    /// point that merging is most likely to lose — *** AN EXPLICIT STATEMENT THAT NO WAVE IS IN
    /// FLIGHT. *** The file always says <c>wave=none</c> or <c>wave=in-progress</c>. A MISSING
    /// <c>wave=</c> KEY IS UNREADABLE, never defaulted to "none": otherwise "no wave in flight" and "a
    /// file that lost that line" become the same read, which is the trap
    /// <c>DeployedProgram.From</c> refuses an empty list to avoid.
    /// </para>
    /// <para>
    /// TWO TRUNCATION DETECTORS, still both earning their place. The sentinel catches a write that
    /// stopped part way; the declared <c>entries=</c> count catches what a sentinel cannot — a file
    /// that parses cleanly and carries FEWER entries than were written. A queue's content is a COUNT
    /// of things, so "read short" looks exactly like a shorter queue, and a shorter queue is admitted
    /// work that silently never happens. Under the atomic write in <see cref="CoordinatorStateStore"/>
    /// neither tear should be reachable any more; they are kept because "should be unreachable" is a
    /// claim about a mechanism, and these two are the check on it.
    /// </para>
    /// <para>
    /// No JSON, and deliberately: the library references nothing, so it stays referenceable from
    /// <c>openness-cli</c> (net48) without dragging a package graph into the one tool whose every
    /// rebuild costs a TIA Openness (Path,FileHash) re-approval. The format is also readable by a
    /// human standing at the machine at 3am, which is exactly who finds one of these.
    /// </para>
    /// <para>
    /// ONE HONEST LIMIT OF THE ESCAPING, and it fails in the safe direction. Object and evidence lines
    /// pack five fields separated by TAB, and the line escape turns a tab into <c>\t</c> whether it is
    /// a separator or part of a value. So a name genuinely containing a tab comes back as SIX fields —
    /// a PARSE REFUSAL naming the line, never a silently mis-split value.
    /// </para>
    /// </remarks>
    internal static class CoordinatorStateFormat
    {
        /// <summary>
        /// Format 2 IS the merge. Format 1 was the two-file era and is refused by the version check
        /// like any other unknown version — see <see cref="CoordinatorStateStore"/> on migration.
        /// </summary>
        internal const int CurrentFormatVersion = 2;

        internal const string Sentinel = "end";

        internal const string WaveNone = "none";
        internal const string WaveInProgress = "in-progress";

        private const string HeaderComment =
            "# ladder COORDINATOR STATE - X-C's wave-in-progress marker AND D23's two queues, in ONE file.\n" +
            "# Written atomically (temp + rename): a torn write leaves the PREVIOUS whole state, never half.\n" +
            "#\n" +
            "# 'wave=in-progress' means a wave was in flight. Every test and result from it is INVALID\n" +
            "# and is discarded, never re-read. Finding that on startup is not an error - it is the\n" +
            "# expected signal after a coordinator death.\n" +
            "# 'wave=none' is an EXPLICIT statement that none was in flight. The absence of this file is\n" +
            "# NOT the same thing, and neither is a file that cannot be read (FI-44).\n" +
            "#\n" +
            "# Every queue entry here was ADMITTED - preflight passed, isolated compile passed, hash\n" +
            "# matched - and is re-gated against the content's CURRENT hash on reload before it drains.\n";

        internal static string Serialize(WaveMarker? wave, IReadOnlyList<QueuedSubmission> entries)
        {
            var sb = new StringBuilder();
            sb.Append(HeaderComment);
            Append(sb, "format", CurrentFormatVersion.ToString(CultureInfo.InvariantCulture));

            if (wave == null)
            {
                Append(sb, "wave", WaveNone);
            }
            else
            {
                Append(sb, "wave", WaveInProgress);
                Append(sb, "wave-id", wave.WaveId);
                Append(sb, "started-utc", wave.StartedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                Append(sb, "program-version", wave.ProgramVersion);
                Append(sb, "coordinator", wave.Coordinator.ToToken());

                foreach (var slot in wave.Slots)
                {
                    Append(sb, "slot", slot);
                }
            }

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
        /// Parses the whole state. Returns false and sets <paramref name="problem"/> for anything it
        /// does not fully understand — the caller turns that into "the state is UNREADABLE", which
        /// means a wave WAS in progress and the queue is unknown, never into "clean start, empty queue".
        /// </summary>
        internal static bool TryParse(
            string text,
            out WaveMarker? wave,
            out IReadOnlyList<PersistedQueueEntry> entries,
            out string problem)
        {
            wave = null;
            entries = new PersistedQueueEntry[0];
            problem = string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                problem = "the state file is present but empty — the write did not complete";
                return false;
            }

            var normalised = text.Replace("\r\n", "\n").Replace('\r', '\n');

            // *** THE SENTINEL MUST BE TERMINATED, NOT MERELY PRESENT — AND THIS IS A HOLE THE OLD
            // TWO-FILE FORMATS BOTH HAD. *** A write torn at the VERY LAST BYTE loses only the newline
            // after "end"; the line-trimming parse below then sees "end" as the last meaningful line and
            // reads the file as complete. Measured on the merged format: a 2,360-byte state truncated to
            // 2,359 parsed as fully restored. The marker format's own truncation walk could never have
            // caught it, because it asserted only "a wave was in progress" — which is equally true of a
            // file that parsed perfectly. It took the queue half, whose torn state must read as
            // UNUSABLE rather than merely present, to make the assertion strong enough to bite.
            if (!normalised.EndsWith("\n", StringComparison.Ordinal))
            {
                problem = "the state file does not end with a newline — the write was torn off before the '" +
                          Sentinel + "' terminator was complete";
                return false;
            }

            var meaningful = normalised
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
                .ToList();

            if (meaningful.Count == 0)
            {
                problem = "the state file carries no content lines — the write did not get past its header";
                return false;
            }

            if (!string.Equals(meaningful[meaningful.Count - 1], Sentinel, StringComparison.Ordinal))
            {
                problem = "the state file has no '" + Sentinel + "' terminator — the write was torn off part way";
                return false;
            }

            meaningful.RemoveAt(meaningful.Count - 1);

            string? formatText = null;
            string? waveText = null;
            string? declaredCountText = null;

            string? waveId = null;
            string? startedText = null;
            string? programVersion = null;
            string? coordinatorToken = null;
            var slots = new List<string>();

            var parsed = new List<PersistedQueueEntry>();
            var entriesHaveBegun = false;

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

                parsed.Add(new PersistedQueueEntry(
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
                    problem = "the state file carries a line that is not 'key=value': '" + Truncate(line) + "'";
                    return false;
                }

                var key = line.Substring(0, split).Trim();
                var value = Unescape(line.Substring(split + 1));

                // A wave field appearing after the queue entries have begun is not a field we can place:
                // it belongs to a section that has already ended. Refused rather than folded in, because
                // guessing which section a line belongs to is how a corrupt file reassembles into a
                // plausible-looking one.
                if (entriesHaveBegun && IsWaveKey(key))
                {
                    problem = "the state file carries the wave field '" + key + "' after the queue entries began";
                    return false;
                }

                switch (key)
                {
                    case "format": formatText = value; break;
                    case "wave": waveText = value; break;
                    case "wave-id": waveId = value; break;
                    case "started-utc": startedText = value; break;
                    case "program-version": programVersion = value; break;
                    case "coordinator": coordinatorToken = value; break;
                    case "slot": slots.Add(value); break;
                    case "entries": declaredCountText = value; break;

                    case "entry":
                        entriesHaveBegun = true;
                        var flushProblem = flush();
                        if (flushProblem != null)
                        {
                            problem = flushProblem;
                            return false;
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
                            return false;
                        }

                        ObjectKind kind;
                        ChangeClass changeClass;
                        if (!TryParseKind(fields[1], out kind) || !TryParseClass(fields[2], out changeClass))
                        {
                            problem = "an object line states a kind or change class this build does not know: '" +
                                      Truncate(value) + "'";
                            return false;
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
                            return false;
                        }

                        EvidenceOutcome preflight;
                        EvidenceOutcome compile;
                        if (!TryParseOutcome(fields[2], out preflight) || !TryParseOutcome(fields[3], out compile))
                        {
                            problem = "an evidence line states an outcome this build does not know: '" +
                                      Truncate(value) + "'";
                            return false;
                        }

                        evidence.Add(new AdmissionEvidence(fields[0], fields[1], preflight, compile, fields[4]));
                        break;
                    }

                    default:
                        problem = "the state file carries a key this build does not know ('" + Truncate(key) +
                                  "') — either corruption, or a file written by a newer format that did not " +
                                  "bump its version";
                        return false;
                }
            }

            var tailProblem = flush();
            if (tailProblem != null)
            {
                problem = tailProblem;
                return false;
            }

            int format;
            if (formatText == null || !int.TryParse(formatText, NumberStyles.Integer, CultureInfo.InvariantCulture, out format))
            {
                problem = "the state file states no readable format version";
                return false;
            }

            if (format != CurrentFormatVersion)
            {
                problem = "the state file is format " + format + "; this build reads format " + CurrentFormatVersion +
                          (format == 1
                              ? ". Format 1 is the two-file era (a separate marker and a separate queue file) and " +
                                "is refused rather than half-read"
                              : string.Empty);
                return false;
            }

            // *** THE DISTINCTION THE MERGE IS MOST LIKELY TO LOSE. *** "No wave in flight" is a
            // STATEMENT the file makes, not the absence of one. A missing wave= line is a file that
            // lost a line, and reading that as "none" would make a damaged file indistinguishable from
            // a clean one.
            if (waveText == null)
            {
                problem = "the state file does not say whether a wave was in flight — there is no 'wave=' line. " +
                          "'none' is a statement this file has to make; its absence is not that statement";
                return false;
            }

            var waveFieldsPresent = waveId != null || startedText != null || programVersion != null ||
                                    coordinatorToken != null || slots.Count > 0;

            if (string.Equals(waveText, WaveNone, StringComparison.Ordinal))
            {
                if (waveFieldsPresent)
                {
                    problem = "the state file says 'wave=none' and also carries wave fields — it contradicts itself";
                    return false;
                }
            }
            else if (string.Equals(waveText, WaveInProgress, StringComparison.Ordinal))
            {
                DateTimeOffset startedUtc;
                if (string.IsNullOrEmpty(waveId) || string.IsNullOrEmpty(programVersion) || slots.Count == 0)
                {
                    problem = "the state file says a wave was in progress but is missing a required field " +
                              "(wave-id, program-version, or at least one slot)";
                    return false;
                }

                if (startedText == null ||
                    !DateTimeOffset.TryParse(
                        startedText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out startedUtc))
                {
                    problem = "the state file states no readable wave start time";
                    return false;
                }

                var coordinator = CoordinatorIdentity.TryParse(coordinatorToken);
                if (coordinator == null)
                {
                    problem = "the state file states no readable coordinator identity";
                    return false;
                }

                try
                {
                    wave = new WaveMarker(waveId!, slots, programVersion!, startedUtc, coordinator);
                }
                catch (ArgumentException ex)
                {
                    problem = "the state file's wave fields do not form a valid marker: " + ex.Message;
                    return false;
                }
            }
            else
            {
                problem = "the state file's 'wave=" + Truncate(waveText) + "' is neither '" + WaveNone +
                          "' nor '" + WaveInProgress + "'";
                return false;
            }

            int declaredCount;
            if (declaredCountText == null ||
                !int.TryParse(declaredCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out declaredCount))
            {
                problem = "the state file declares no readable entry count";
                return false;
            }

            if (declaredCount != parsed.Count)
            {
                problem = "the state file declares " + declaredCount + " entries and carries " +
                          parsed.Count + ". A queue read short is work that quietly never happens, so " +
                          "this is refused rather than accepted as a shorter queue";
                return false;
            }

            var duplicate = parsed
                .GroupBy(e => e.Submission.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                problem = "the state file carries submission id '" + duplicate.Key + "' more than once";
                return false;
            }

            entries = parsed;
            return true;
        }

        private static bool IsWaveKey(string key)
        {
            switch (key)
            {
                case "wave":
                case "wave-id":
                case "started-utc":
                case "program-version":
                case "coordinator":
                case "slot":
                    return true;
                default:
                    return false;
            }
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
