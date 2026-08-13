using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>
    /// What a read of the coordinator's persisted state found. The zero value REFUSES.
    /// </summary>
    public enum CoordinatorStateFileState
    {
        /// <summary>
        /// *** THE FILE IS THERE AND COULD NOT BE READ *** — truncated, unparseable, short of the entry
        /// count it declares, self-contradictory, locked, or written by a format this build does not
        /// know. The zero value, so a dropped or defaulted state refuses rather than resuming.
        /// <para>
        /// Under the merge this voids BOTH the wave and the queue. See
        /// <see cref="CoordinatorStateStore"/> on why that is a deliberate strengthening rather than an
        /// over-reach.
        /// </para>
        /// </summary>
        Unreadable = 0,

        /// <summary>
        /// *** NO STATE FILE EXISTS. *** NOT an empty queue and NOT "no wave in flight" — both of those
        /// are statements the file makes, and this is the absence of the file that would make them. It
        /// is legitimate on a coordinator's first ever start and a serious finding on any other, and
        /// this component cannot tell those apart: the caller can, through
        /// <see cref="CoordinatorStateStore.AcceptNoPersistedState"/>.
        /// </summary>
        NoStateFileFound = 1,

        /// <summary>The file was read and everything in it parsed. Queue entries still have to be re-gated.</summary>
        Restored = 2,

        /// <summary>
        /// *** STATE FROM THE TWO-FILE ERA IS PRESENT. *** A <c>wave-in-progress.marker</c> or a
        /// <c>wave-queues.state</c> was found beside the merged file. Refused, never half-read — see
        /// <see cref="CoordinatorStateStore"/> on migration.
        /// </summary>
        LegacyTwoFileStatePresent = 3,
    }

    /// <summary>The result of reading the coordinator's persisted state.</summary>
    public sealed class CoordinatorStateResult
    {
        internal CoordinatorStateResult(
            CoordinatorStateFileState state,
            string path,
            WaveStatus wave,
            IReadOnlyList<PersistedQueueEntry> entries,
            string problem)
        {
            State = state;
            Path = path;
            Wave = wave;
            Entries = entries;
            Problem = problem;
        }

        /// <summary>What the read found.</summary>
        public CoordinatorStateFileState State { get; }

        /// <summary>Where it looked.</summary>
        public string Path { get; }

        /// <summary>
        /// X-C's answer, unchanged by the merge: was a wave in flight? An unreadable or legacy state
        /// yields <see cref="WaveMarkerState.WaveInProgressDetailsUnreadable"/>, which is what X-C
        /// requires — the crash where the slots are unknown is exactly the crash where the rig state is
        /// invalid.
        /// </summary>
        public WaveStatus Wave { get; }

        /// <summary>
        /// The queue entries, for <see cref="CoordinatorStateFileState.Restored"/> only. EMPTY for
        /// every other state — and an empty list here never means "the queues were empty", which is why
        /// <see cref="State"/> has to be read first.
        /// </summary>
        public IReadOnlyList<PersistedQueueEntry> Entries { get; }

        /// <summary>Why the state could not be read, when it could not.</summary>
        public string Problem { get; }

        /// <summary>How many queue entries were read back.</summary>
        public int Count => Entries.Count;

        /// <summary>TRUE only when the queue may be used — i.e. the file was fully read.</summary>
        public bool QueueIsUsable => State == CoordinatorStateFileState.Restored;

        /// <summary>Both halves, in the affirmative, for the startup log.</summary>
        public string Describe()
        {
            switch (State)
            {
                case CoordinatorStateFileState.Restored:
                    return "COORDINATOR STATE RESTORED from '" + Path + "'. " + Wave.Describe() +
                           " Queue: " + Count + " entry/ies (" +
                           Entries.Count(e => e.Queue == DownloadQueue.RunQueue) + " run, " +
                           Entries.Count(e => e.Queue == DownloadQueue.DeferredQueue) +
                           " deferred), NOT yet re-gated.";

                case CoordinatorStateFileState.NoStateFileFound:
                    return "NO COORDINATOR STATE FILE at '" + Path + "'. This is NOT 'no wave in flight' " +
                           "and NOT an empty queue — both of those are statements the file makes. It is " +
                           "expected on a first start and is lost state on any other, and nothing here " +
                           "can tell those apart.";

                case CoordinatorStateFileState.LegacyTwoFileStatePresent:
                    return "TWO-FILE STATE PRESENT beside '" + Path + "': " + Problem +
                           " Refused, not migrated: reading it would need the reconciliation rule the " +
                           "merge exists to delete, at the one moment nobody can validate it.";

                default:
                    return "COORDINATOR STATE UNREADABLE at '" + Path + "': " + Problem +
                           ". A WAVE WAS IN PROGRESS and its details are unknown; the queue is unknown " +
                           "too. Void both, force inert, and do not read any result from this rig.";
            }
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>
    /// The coordinator's whole persisted state — X-C's wave-in-progress marker AND D23's two queues —
    /// in ONE file, written ATOMICALLY.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** ONE FILE, BY OWNER'S RULING 2026-08-13. *** The two states are only ever read together, on
    /// restart. Splitting them bought nothing and cost a reconciliation rule that nobody would remember
    /// when it mattered — and the crash that produces a disagreement between them is exactly the one
    /// where the coordinator cannot be asked what it was doing. The two failure modes now structurally
    /// impossible: QUEUE WRITTEN + MARKER NOT CLEARED (a restart voids results that were valid), and
    /// MARKER CLEARED + QUEUE NOT WRITTEN (an item dequeued in memory but never persisted runs twice,
    /// or is lost).
    /// </para>
    /// <para>
    /// *** THE ATOMICITY MECHANISM, AND WHAT IT DOES AND DOES NOT GUARANTEE. *** A save writes a
    /// TEMPORARY file in the SAME DIRECTORY, flushes it to disk, and then renames it over the
    /// destination — <see cref="File.Replace(string,string,string)"/> when a destination exists,
    /// <see cref="File.Move(string,string)"/> for the very first save. What that buys, precisely:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     *** A READER NEVER SEES A HALF-NEW FILE. *** The rename swaps a directory entry; a reader
    ///     opens either the old file or the new one. The torn-write case that both previous formats had
    ///     to DETECT is now not produced at all.
    ///   </description></item>
    ///   <item><description>
    ///     THE CONTENT IS ON THE PLATTER BEFORE THE RENAME. The temp handle is
    ///     <see cref="FileOptions.WriteThrough"/> and is flushed with <c>Flush(flushToDisk: true)</c>
    ///     before the rename is attempted, so the file being swapped in is complete, not merely
    ///     buffered. The ORDER is the point.
    ///   </description></item>
    ///   <item><description>
    ///     SAME DIRECTORY, DELIBERATELY. A rename across volumes is a copy, and a copy is not atomic.
    ///     If the temp cannot be created there, the save FAILS — it never falls back to writing in place.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** WHAT IT DOES NOT GUARANTEE, in the same register as X-C's own residual. *** .NET offers no
    /// portable way to flush a DIRECTORY on Windows, so the rename's own durability across a POWER CUT
    /// is not something this code can force: after a power loss you may find the PREVIOUS state even
    /// though <c>Save</c> returned. That is the safe direction and it is the whole point — a lost save
    /// leaves a coherent older state, where a half-write would leave an incoherent new one. It is the
    /// same class of residual as the note that <see cref="FileOptions.WriteThrough"/> covers a file's
    /// content and not a new file's directory entry, and it does not apply to the case X-C actually
    /// names: a coordinator PROCESS that dies while the machine keeps running.
    /// </para>
    /// <para>
    /// AND ONE MORE, WORTH SAYING BECAUSE IT IS A WINDOWS FACT RATHER THAN A DESIGN CHOICE: an
    /// antivirus scanner or an indexer holding the destination open can make the rename fail. That is a
    /// THROWN <see cref="WaveMarkerException"/>, not a silent retry and not a fallback to an in-place
    /// write — the save fails loudly and the previous whole state stands.
    /// </para>
    /// <para>
    /// *** THE FOUR DISTINCTIONS THE MERGE HAD TO CARRY, ALL OF THEM INTACT. *** (1) Absent, unreadable
    /// and empty are three different states, and the file is NEVER DELETED — a completed wave with
    /// empty queues is written as <c>wave=none</c> plus <c>entries=0</c>, so "no file" keeps meaning
    /// "nobody ever wrote one here". (2) Both truncation detectors survive in
    /// <see cref="CoordinatorStateFormat"/>. (3) The re-gate against the CURRENT content hash is
    /// unchanged and still runs in <see cref="QueueRehydrator"/>. (4) The asymmetric excision rule is
    /// unchanged. This is a change of container, not of semantics.
    /// </para>
    /// <para>
    /// *** POINT 3 — AN UNREADABLE FILE NOW VOIDS BOTH THE WAVE AND THE QUEUE. IS THAT AN OVER-REACH?
    /// NO: IT IS A DELIBERATE STRENGTHENING, AND THE CASE IT COSTS IS ONE THE MERGE ALSO STOPS
    /// PRODUCING. *** Before the merge, a corrupt marker beside an intact queue file let you void the
    /// wave and keep the queue. That state was reachable because a torn write hits ONE file. With a
    /// single atomically-renamed file, a torn write cannot produce a partly-damaged file at all — so
    /// the only remaining way to get "one section corrupt, the other intact" is media corruption or
    /// tampering, and neither is a reason to trust the section that still parses: a file damaged in one
    /// region is not evidence about another region. The cost of voiding the queue is bounded and
    /// visible — the admitted work is re-submitted by the agents that hold it, and the re-gate would
    /// have re-verified it anyway. The cost of trusting half of a damaged file is running a wave
    /// against a queue that may be wrong, silently.
    /// </para>
    /// <para>
    /// IF IT EVER HAD TO BE SEPARATED AGAIN WITHIN ONE FILE, the mechanism would be PER-SECTION
    /// INTEGRITY: the wave section and the queue section each carrying their own checksum and their own
    /// terminator, so a reader could say "the queue section verifies, the wave section does not". That
    /// is implementable and is deliberately NOT built, because what it buys is permission to trust part
    /// of a file that is known to be damaged.
    /// </para>
    /// <para>
    /// *** MIGRATION: THE TWO-FILE FORM IS REFUSED, NOT READ. *** If a <c>wave-in-progress.marker</c>
    /// or a <c>wave-queues.state</c> is found in the state directory, the read returns
    /// <see cref="CoordinatorStateFileState.LegacyTwoFileStatePresent"/> and NOTHING is loaded — not
    /// even the half that would parse. Reading it would require exactly the reconciliation rule this
    /// merge exists to delete, applied at the one moment nobody can check it. The refusal names both
    /// files so an operator can record their contents and remove them deliberately. The legacy check
    /// runs BEFORE the merged file is opened, so a directory holding both is refused rather than
    /// silently preferring one.
    /// </para>
    /// </remarks>
    public sealed class CoordinatorStateStore
    {
        /// <summary>The conventional file name, when a caller supplies only a directory.</summary>
        public const string DefaultFileName = "coordinator.state";

        /// <summary>The two-file era's marker file. Its presence is a refusal, never a migration.</summary>
        public const string LegacyMarkerFileName = "wave-in-progress.marker";

        /// <summary>The two-file era's queue file. Its presence is a refusal, never a migration.</summary>
        public const string LegacyQueueFileName = "wave-queues.state";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly CoordinatorIdentity _identity;

        /// <param name="statePath">Full path to the state file.</param>
        /// <param name="identity">
        /// Whose state this is. Defaults to the current process; injectable so a test can play the part
        /// of a coordinator that has since died.
        /// </param>
        public CoordinatorStateStore(string statePath, CoordinatorIdentity? identity = null)
        {
            if (string.IsNullOrWhiteSpace(statePath))
            {
                throw new ArgumentException("A state path is required.", nameof(statePath));
            }

            StatePath = System.IO.Path.GetFullPath(statePath);
            _identity = identity ?? CoordinatorIdentity.Current;
        }

        /// <summary>Creates a store for <see cref="DefaultFileName"/> inside <paramref name="directory"/>.</summary>
        public static CoordinatorStateStore InDirectory(string directory, CoordinatorIdentity? identity = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A directory is required.", nameof(directory));
            }

            return new CoordinatorStateStore(System.IO.Path.Combine(directory, DefaultFileName), identity);
        }

        /// <summary>Where the state lives.</summary>
        public string StatePath { get; }

        /// <summary>
        /// TEST SEAM — invoked with the temporary file's path after it has been written and flushed,
        /// and BEFORE it is renamed over the destination.
        /// </summary>
        /// <remarks>
        /// *** THIS EXISTS BECAUSE THE CENTRAL GUARANTEE OF THIS CLASS WAS OTHERWISE UNTESTABLE, AND A
        /// MUTATION PROVED IT. *** Replacing the whole temp-then-rename with a plain in-place write left
        /// the entire suite GREEN: the debris check and the failed-publish check both pass under an
        /// in-place write, because a locked destination fails at OPEN and so leaves the old file
        /// untouched either way. Nothing observed the one thing that matters — that at the moment
        /// before publication the NEW state is already complete somewhere else while the destination
        /// still holds the PREVIOUS whole state. This seam makes exactly that observable, and it is the
        /// difference between an argued guarantee and a demonstrated one.
        /// </remarks>
        internal Action<string>? OnTemporaryWritten { get; set; }

        /// <summary>The identity this store writes into the wave section and compares found ones against.</summary>
        public CoordinatorIdentity Identity => _identity;

        /// <summary>
        /// THE STARTUP CALL. One read, both answers. Never throws for a missing, empty, corrupt or
        /// locked file — each of those is a STATE, and only one of them permits resuming.
        /// </summary>
        public CoordinatorStateResult Read()
        {
            var legacy = FindLegacyFiles();
            if (legacy.Count > 0)
            {
                return new CoordinatorStateResult(
                    CoordinatorStateFileState.LegacyTwoFileStatePresent,
                    StatePath,
                    WaveStatus.Unreadable(StatePath, "two-file state is present and is refused", _identity),
                    new PersistedQueueEntry[0],
                    "found " + string.Join(" and ", legacy.ToArray()) +
                    ". Record their contents and remove them deliberately; nothing is read from them.");
            }

            string text;

            // Deliberately NOT File.Exists first: it swallows every error and returns false, so an
            // unreadable directory or a permissions problem would read as "no state file" — the most
            // benign-looking of the four states, and therefore the worst one to land on by accident.
            try
            {
                using (var stream = new FileStream(StatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException)
            {
                return NoFile();
            }
            catch (DirectoryNotFoundException)
            {
                return NoFile();
            }
            catch (IOException ex)
            {
                return Unreadable("the state file exists but could not be opened (" + ex.Message + ")");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unreadable("the state file exists but could not be opened (" + ex.Message + ")");
            }

            WaveMarker? wave;
            IReadOnlyList<PersistedQueueEntry> entries;
            string problem;

            if (!CoordinatorStateFormat.TryParse(text, out wave, out entries, out problem))
            {
                return Unreadable(problem);
            }

            var status = wave == null
                ? WaveStatus.None(StatePath, _identity)
                : WaveStatus.InProgress(StatePath, wave, _identity);

            return new CoordinatorStateResult(
                CoordinatorStateFileState.Restored,
                StatePath,
                status,
                entries,
                string.Empty);
        }

        /// <summary>
        /// Write the whole state — the wave (or the explicit absence of one) and both queues — in ONE
        /// atomic operation. Call after every mutation a crash must not lose.
        /// </summary>
        /// <param name="wave">What is in flight, or NULL for the explicit "no wave in flight".</param>
        /// <param name="queues">The two queues.</param>
        /// <exception cref="WaveMarkerException">
        /// The write failed. Thrown rather than swallowed: continuing means running with state that
        /// exists only in memory, which is the situation this component was built to remove.
        /// </exception>
        public void Save(WaveMarker? wave, WaveQueues queues)
        {
            if (queues == null)
            {
                throw new ArgumentNullException(nameof(queues));
            }

            var directory = System.IO.Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            var payload = Utf8NoBom.GetBytes(CoordinatorStateFormat.Serialize(wave, queues.All));

            // Same directory as the destination, so the rename is a directory-entry swap on one volume
            // rather than a copy across two.
            var temporaryPath = StatePath + ".tmp-" + Guid.NewGuid().ToString("N");

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.WriteThrough))
                {
                    stream.Write(payload, 0, payload.Length);

                    // The ORDER is the point: the content is durable BEFORE the rename publishes it.
                    stream.Flush(flushToDisk: true);
                }

                var seam = OnTemporaryWritten;
                if (seam != null)
                {
                    seam(temporaryPath);
                }

                Publish(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                TryDelete(temporaryPath);

                throw new WaveMarkerException(
                    "Could not write the coordinator state to '" + StatePath + "'. The coordinator MUST " +
                    "NOT continue: from here on its wave marker and its queues exist only in memory, and " +
                    "a crash would lose admitted work that nothing downstream would notice was missing. " +
                    "The previous whole state is still on disk and is still readable.",
                    ex);
            }
        }

        /// <summary>
        /// Begin a wave, writing the marker and the queues together.
        /// </summary>
        /// <exception cref="WaveAlreadyInProgressException">
        /// A wave is already in flight — including when the state is unreadable, because a state we
        /// cannot read is not one we can overwrite. Acknowledge it with
        /// <see cref="DiscardInterruptedWave"/> first.
        /// </exception>
        public void BeginWave(WaveMarker marker, WaveQueues queues)
        {
            if (marker == null)
            {
                throw new ArgumentNullException(nameof(marker));
            }

            var existing = Read();
            if (existing.Wave.WaveWasInProgress)
            {
                throw new WaveAlreadyInProgressException(existing.Wave);
            }

            Save(marker, queues);
        }

        /// <summary>
        /// CLEAN COMPLETION. Records that no wave is in flight, and the queues as they now stand — in
        /// one write, so the wave cannot be cleared without the queue being persisted alongside it.
        /// </summary>
        /// <exception cref="WaveMarkerException">No wave was in progress.</exception>
        public void CompleteWave(WaveQueues queues)
        {
            var status = Read();
            if (!status.Wave.WaveWasInProgress)
            {
                throw new WaveMarkerException(
                    "CompleteWave was called but no wave was in progress at '" + StatePath +
                    "'. Either the wave was never begun, or something else cleared it while the wave " +
                    "was running — and in the second case a crash between then and now would have read " +
                    "as a clean start.");
            }

            Save(null, queues);
        }

        /// <summary>
        /// THE RECOVERY PATH. Acknowledges a wave found on startup, records that its results are being
        /// discarded (X-C item 3), and writes the state the coordinator is continuing with.
        /// </summary>
        /// <param name="found">The result returned by <see cref="Read"/>.</param>
        /// <param name="reason">What the coordinator is recording about the interruption.</param>
        /// <param name="queues">
        /// The queues to continue with. After an UNREADABLE state that must be empty queues: the queue
        /// was voided along with the wave, and the returned log line says so.
        /// </param>
        /// <returns>A line for the log, naming what was discarded.</returns>
        public string DiscardInterruptedWave(CoordinatorStateResult found, string reason, WaveQueues queues)
        {
            if (found == null)
            {
                throw new ArgumentNullException(nameof(found));
            }

            if (!found.Wave.WaveWasInProgress)
            {
                throw new WaveMarkerException(
                    "DiscardInterruptedWave was called with a result that reports no wave in progress. " +
                    "There is nothing to discard, and calling it anyway would put a discard in the log " +
                    "that never happened.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Discarding an interrupted wave must be recorded with a reason. This is the record " +
                    "that a wave's results were thrown away rather than never produced.",
                    nameof(reason));
            }

            var queueNote = found.QueueIsUsable
                ? " The queue was readable and is unaffected."
                : " THE QUEUE WAS VOIDED TOO — the state was unreadable, so every admitted submission " +
                  "in it is unknown and its agents must re-submit.";

            Save(null, queues);

            return "DISCARDED INTERRUPTED WAVE: " + found.Wave.Describe() + queueNote +
                   " Reason: " + reason.Trim();
        }

        /// <summary>
        /// The ONLY way to proceed from <see cref="CoordinatorStateFileState.NoStateFileFound"/> — an
        /// explicit declaration, with a reason, that there was nothing to restore.
        /// </summary>
        /// <remarks>
        /// The same shape as <c>DeployedProgram.Empty</c>, and for the same reason: a first ever start
        /// and a lost state produce the identical absence, so somebody has to say which this is.
        /// </remarks>
        public static string AcceptNoPersistedState(CoordinatorStateResult found, string reason)
        {
            if (found == null)
            {
                throw new ArgumentNullException(nameof(found));
            }

            if (found.State != CoordinatorStateFileState.NoStateFileFound)
            {
                throw new WaveMarkerException(
                    "AcceptNoPersistedState was called with a result whose state is " + found.State +
                    ", not NoStateFileFound. Declaring 'there was nothing to restore' over a state file " +
                    "that exists — readable or not — would discard whatever it holds.");
            }

            var stated = (reason ?? string.Empty).Trim();
            if (stated.Length == 0)
            {
                throw new ArgumentException(
                    "Accepting an absent state requires a reason. This is the claim that no wave was in " +
                    "flight AND no admitted work was lost, and on any run but the first it is a claim " +
                    "that needs somebody behind it.",
                    nameof(reason));
            }

            return "ACCEPTED ABSENT COORDINATOR STATE at '" + found.Path + "'. Reason: " + stated;
        }

        /// <summary>
        /// The rename. <see cref="File.Replace(string,string,string)"/> needs an existing destination
        /// and <see cref="File.Move(string,string)"/> needs an absent one, so the choice is made from
        /// the destination — and the race between checking and acting is handled by falling through to
        /// the other call rather than by trusting the check.
        /// </summary>
        private void Publish(string temporaryPath)
        {
            if (File.Exists(StatePath))
            {
                File.Replace(temporaryPath, StatePath, destinationBackupFileName: null);
                return;
            }

            try
            {
                File.Move(temporaryPath, StatePath);
            }
            catch (IOException)
            {
                // The destination appeared between the check and the move. Replace is the correct call
                // for that, and reaching it here is the whole reason the check is not trusted.
                File.Replace(temporaryPath, StatePath, destinationBackupFileName: null);
            }
        }

        private List<string> FindLegacyFiles()
        {
            var directory = System.IO.Path.GetDirectoryName(StatePath);
            if (string.IsNullOrEmpty(directory))
            {
                return new List<string>();
            }

            var found = new List<string>();

            foreach (var name in new[] { LegacyMarkerFileName, LegacyQueueFileName })
            {
                var candidate = System.IO.Path.Combine(directory!, name);

                // File.Exists is right HERE and wrong in Read(): a false from a permissions problem
                // means "we cannot see a legacy file", which loses nothing — the merged file's own read
                // will hit the same problem and report it.
                if (File.Exists(candidate))
                {
                    found.Add("'" + name + "'");
                }
            }

            return found;
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A leaked temp file beside the state file is not worth masking the real failure over.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private CoordinatorStateResult NoFile() =>
            new CoordinatorStateResult(
                CoordinatorStateFileState.NoStateFileFound,
                StatePath,
                WaveStatus.None(StatePath, _identity),
                new PersistedQueueEntry[0],
                string.Empty);

        private CoordinatorStateResult Unreadable(string problem) =>
            new CoordinatorStateResult(
                CoordinatorStateFileState.Unreadable,
                StatePath,
                WaveStatus.Unreadable(StatePath, problem, _identity),
                new PersistedQueueEntry[0],
                problem);
    }
}
