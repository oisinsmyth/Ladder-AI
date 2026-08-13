using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>
    /// What a read of the persisted queue state found. The zero value REFUSES.
    /// </summary>
    public enum QueueRestoreState
    {
        /// <summary>
        /// *** THE STATE FILE IS THERE AND COULD NOT BE READ *** — truncated, unparseable, short of
        /// the entry count it declares, locked, or written by a format this build does not know. The
        /// zero value, so a dropped or defaulted state refuses rather than resuming.
        /// </summary>
        Unreadable = 0,

        /// <summary>
        /// *** NO STATE FILE EXISTS. *** This is NOT an empty queue and must not be treated as one.
        /// It is legitimate on a coordinator's first ever start and a serious finding on any other,
        /// and this component cannot tell those apart — the caller can, and must say which it means
        /// through <see cref="WaveQueueStore.AcceptNoPersistedState"/>.
        /// </summary>
        NoStateFileFound = 1,

        /// <summary>The file was read and every entry parsed. Still has to be re-gated before it drains.</summary>
        Restored = 2,
    }

    /// <summary>The result of reading the persisted queue state.</summary>
    public sealed class QueueRestoreResult
    {
        internal QueueRestoreResult(
            QueueRestoreState state,
            string path,
            IReadOnlyList<PersistedQueueEntry> entries,
            string problem)
        {
            State = state;
            Path = path;
            Entries = entries;
            Problem = problem;
        }

        /// <summary>What the read found.</summary>
        public QueueRestoreState State { get; }

        /// <summary>Where it looked.</summary>
        public string Path { get; }

        /// <summary>
        /// The entries, for <see cref="QueueRestoreState.Restored"/> only. EMPTY for both other states
        /// — and an empty list here never means "the queues were empty", which is why
        /// <see cref="State"/> has to be read first.
        /// </summary>
        public IReadOnlyList<PersistedQueueEntry> Entries { get; }

        /// <summary>Why it was unreadable, for <see cref="QueueRestoreState.Unreadable"/>.</summary>
        public string Problem { get; }

        /// <summary>How many entries were read back.</summary>
        public int Count => Entries.Count;

        /// <summary>One line for the log.</summary>
        public string Describe()
        {
            switch (State)
            {
                case QueueRestoreState.Restored:
                    return "QUEUE STATE RESTORED from '" + Path + "': " + Count + " entry/ies (" +
                           Entries.Count(e => e.Queue == DownloadQueue.RunQueue) + " run, " +
                           Entries.Count(e => e.Queue == DownloadQueue.DeferredQueue) +
                           " deferred). NOT yet re-gated.";

                case QueueRestoreState.NoStateFileFound:
                    return "NO QUEUE STATE FILE at '" + Path + "'. This is NOT an empty queue: it is " +
                           "expected on a first start and is a lost deferred queue on any other, and " +
                           "nothing here can tell those apart.";

                default:
                    return "QUEUE STATE UNREADABLE at '" + Path + "': " + Problem +
                           ". Refused — a queue that cannot be read is not a queue that is empty.";
            }
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>
    /// Persists D23's two queues across a coordinator's death.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHY THIS IS NOT A NICETY. *** The deferred queue holds work that has ALREADY been admitted:
    /// preflight passed, isolated compile passed, hash matched, and it is waiting only for a boundary.
    /// If the coordinator dies and that queue evaporates, the work is neither done nor queued and
    /// *** nothing in the system notices *** — it simply never happens. That is the "empty is not
    /// clean" family (FI-44) raised from a single check to a whole queue, and an absence at that level
    /// is invisible to every check downstream of it.
    /// </para>
    /// <para>
    /// *** ABSENT AND UNREADABLE ARE DIFFERENT STATES, AND NEITHER IS AN EMPTY QUEUE. *** They are the
    /// same zero rows and opposite correct actions, exactly as <c>DeployedProgram.From</c> argues for a
    /// baseline. So the type system carries the distinction:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Unreadable is the ZERO value of <see cref="QueueRestoreState"/>, so a dropped or defaulted
    ///     state refuses rather than resuming on nothing.
    ///   </description></item>
    ///   <item><description>
    ///     THE FILE IS NEVER DELETED — an empty queue is written as a file declaring zero entries, so
    ///     "no file" keeps meaning "no coordinator ever wrote one here" rather than becoming
    ///     ambiguous with "the queues emptied".
    ///   </description></item>
    ///   <item><description>
    ///     There is no route from this store to a populated <see cref="WaveQueues"/> except
    ///     <see cref="QueueRehydration"/>, which refuses both non-restored states; and the only way to
    ///     proceed from "no file" is <see cref="AcceptNoPersistedState"/>, which requires a REASON.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** DURABILITY: THE SAME BAR X-C SET, AND THE SAME HONEST RESIDUAL. *** The write is in place
    /// (<see cref="FileMode.Create"/>), <see cref="FileOptions.WriteThrough"/>, and flushed with
    /// <c>Flush(flushToDisk: true)</c>. That makes the file's CONTENT durable and says NOTHING about
    /// the DIRECTORY ENTRY of a newly created file — .NET offers no portable directory fsync on
    /// Windows — so a power cut in the microseconds around the first ever save could leave no file at
    /// all. *** That gap does not apply to the case this exists for: a coordinator PROCESS that dies
    /// while the machine keeps running, where the entry is visible to every other process the instant
    /// the handle is created. *** Stated rather than papered over.
    /// </para>
    /// <para>
    /// AND THE TEAR THAT MATTERS MORE THAN THE POWER CUT: a save that is torn off part way leaves a
    /// file with no sentinel, or one carrying fewer entries than it declares. Both are
    /// <see cref="QueueRestoreState.Unreadable"/>, which stops the coordinator, rather than a shorter
    /// queue, which would not.
    /// </para>
    /// </remarks>
    public sealed class WaveQueueStore
    {
        /// <summary>The conventional file name, when a caller supplies only a directory.</summary>
        public const string DefaultFileName = "wave-queues.state";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <param name="statePath">Full path to the queue-state file.</param>
        public WaveQueueStore(string statePath)
        {
            if (string.IsNullOrWhiteSpace(statePath))
            {
                throw new ArgumentException("A queue-state path is required.", nameof(statePath));
            }

            StatePath = System.IO.Path.GetFullPath(statePath);
        }

        /// <summary>Creates a store for <see cref="DefaultFileName"/> inside <paramref name="directory"/>.</summary>
        public static WaveQueueStore InDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A directory is required.", nameof(directory));
            }

            return new WaveQueueStore(System.IO.Path.Combine(directory, DefaultFileName));
        }

        /// <summary>Where the state lives.</summary>
        public string StatePath { get; }

        /// <summary>
        /// THE STARTUP CALL. Never throws for a missing, empty, corrupt or locked file — each of those
        /// is a STATE, and only one of the three permits resuming.
        /// </summary>
        public QueueRestoreResult Read()
        {
            string text;

            // Deliberately NOT File.Exists first: it swallows every error and returns false, so an
            // unreadable directory or a permissions problem would read as "no state file" — which is
            // the one of the three states that is closest to being benign, and therefore the worst
            // possible place to land by accident.
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
                return Unreadable("the queue-state file exists but could not be opened (" + ex.Message + ")");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unreadable("the queue-state file exists but could not be opened (" + ex.Message + ")");
            }

            string problem;
            var entries = WaveQueueFormat.TryParse(text, out problem);

            return entries == null
                ? Unreadable(problem)
                : new QueueRestoreResult(QueueRestoreState.Restored, StatePath, entries, string.Empty);
        }

        /// <summary>
        /// Write the queues. Call after EVERY mutation that a crash must not lose — an enqueue, an
        /// excision, a completion, a drain.
        /// </summary>
        /// <exception cref="WaveMarkerException">
        /// The write failed. Thrown rather than swallowed: continuing after a failed save means running
        /// with queues that exist only in memory, which is the situation this component was built to
        /// remove.
        /// </exception>
        public void Save(WaveQueues queues)
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

            var payload = Utf8NoBom.GetBytes(WaveQueueFormat.Serialize(queues.All));

            try
            {
                using (var stream = new FileStream(
                    StatePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.WriteThrough))
                {
                    stream.Write(payload, 0, payload.Length);

                    // WriteThrough should already have carried it; the explicit flush-to-disk is the
                    // belt to that braces, and costs one syscall against a whole wave.
                    stream.Flush(flushToDisk: true);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new WaveMarkerException(
                    "Could not write the queue state to '" + StatePath + "'. The coordinator MUST NOT " +
                    "continue: from here on its queues exist only in memory, and a crash would lose " +
                    "admitted work that nothing downstream would notice was missing.",
                    ex);
            }
        }

        /// <summary>
        /// The ONLY way to proceed from <see cref="QueueRestoreState.NoStateFileFound"/> — an explicit
        /// declaration, with a reason, that there was nothing to restore.
        /// </summary>
        /// <remarks>
        /// The same shape as <c>DeployedProgram.Empty</c>, and for the same reason: a first ever start
        /// and a lost deferred queue produce the identical absence, so somebody has to say which this
        /// is. Returns empty queues and a log line recording the claim.
        /// </remarks>
        public static string AcceptNoPersistedState(QueueRestoreResult found, string reason)
        {
            if (found == null)
            {
                throw new ArgumentNullException(nameof(found));
            }

            if (found.State != QueueRestoreState.NoStateFileFound)
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
                    "Accepting an absent queue state requires a reason. This is the claim that no " +
                    "admitted work was lost, and on any run but the first it is a claim that needs " +
                    "somebody behind it.",
                    nameof(reason));
            }

            return "ACCEPTED ABSENT QUEUE STATE at '" + found.Path + "'. Reason: " + stated;
        }

        private QueueRestoreResult NoFile() =>
            new QueueRestoreResult(QueueRestoreState.NoStateFileFound, StatePath, new PersistedQueueEntry[0], string.Empty);

        private QueueRestoreResult Unreadable(string problem) =>
            new QueueRestoreResult(QueueRestoreState.Unreadable, StatePath, new PersistedQueueEntry[0], problem);
    }
}
