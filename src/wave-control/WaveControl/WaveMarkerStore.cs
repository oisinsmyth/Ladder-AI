using System;
using System.IO;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>
    /// The persisted wave-in-progress marker — spec §16.3 X-C item 2, "the one piece that must
    /// actually be built, and it is a file".
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT IT IS FOR. If the coordinator dies, every test and result in flight is INVALID. That
    /// ruling is only safe if a restarted coordinator can TELL that it died mid-wave; without a
    /// persisted marker it cannot distinguish "I crashed" from "I am starting clean", and will treat
    /// stale rig state as a fresh start.
    /// </para>
    /// <para>
    /// *** SURVIVING THE PROCESS IS THE ENTIRE REQUIREMENT, AND TWO DESIGN CHOICES CARRY IT. ***
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     THE FILE IS CREATED BEFORE ITS CONTENT IS WRITTEN, IN PLACE — not written to a temporary
    ///     file and renamed. Write-then-rename is the usual way to get an atomic update, and it is
    ///     the WRONG shape here: a crash mid-write would leave no file at the marker path, and no
    ///     file reads as "no wave was in progress" — a fail-open, on the one question this component
    ///     exists to answer. Creating in place means the instant <c>BeginWave</c> touches the disk,
    ///     a crash reads as "a wave was in progress", and the payload only sharpens that answer.
    ///   </description></item>
    ///   <item><description>
    ///     THE HANDLE IS OPENED <see cref="FileOptions.WriteThrough"/> AND FLUSHED WITH
    ///     <c>Flush(flushToDisk: true)</c>. A buffered write that never reached the platter is a
    ///     marker that does not exist after a power cut, which is the failure this whole component is
    ///     insurance against.
    ///   </description></item>
    /// </list>
    /// <para>
    /// AND THE READ SIDE COMPLETES THE ARGUMENT: a marker that is present but truncated, empty,
    /// unparseable or unopenable reads as <see cref="WaveMarkerState.WaveInProgressDetailsUnreadable"/>
    /// — a wave WAS in progress. Empty is not clean (FI-44). Only the file's genuine absence is
    /// clean, and clearing it is the last thing a completed wave does.
    /// </para>
    /// <para>
    /// ONE HONEST RESIDUAL ON DURABILITY. <see cref="FileOptions.WriteThrough"/> and
    /// <c>Flush(flushToDisk: true)</c> make the file's CONTENT durable; they say nothing about the
    /// DIRECTORY ENTRY of a newly created file, and .NET offers no portable way to flush a directory
    /// on Windows. So a power cut in the microseconds around <c>BeginWave</c> could in principle
    /// leave no file at all. That gap does NOT apply to the case X-C actually names — a coordinator
    /// process that dies while the machine keeps running, where the entry is already visible to
    /// every other process on the system the instant the handle is created.
    /// </para>
    /// <para>
    /// CLEARING IS A DELETE, and a crash before the delete commits leaves the marker in place — which
    /// reads as "a wave was in progress" for a wave that had actually finished. That is a false
    /// positive, it costs one discarded wave's results, and it is the direction the whole design
    /// leans deliberately.
    /// </para>
    /// <para>
    /// This type touches ONE file and nothing else. It does not force inert (X-C item 1) and does not
    /// discard results (X-C item 3) — those are the coordinator's, and this is what tells it to do
    /// them.
    /// </para>
    /// </remarks>
    public sealed class WaveMarkerStore
    {
        /// <summary>The conventional file name, when a caller supplies only a directory.</summary>
        public const string DefaultFileName = "wave-in-progress.marker";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly CoordinatorIdentity _identity;

        /// <param name="markerPath">Full path to the marker file.</param>
        /// <param name="identity">
        /// Whose marker this is. Defaults to the current process; injectable so a test can play the
        /// part of a coordinator that has since died.
        /// </param>
        public WaveMarkerStore(string markerPath, CoordinatorIdentity? identity = null)
        {
            if (string.IsNullOrWhiteSpace(markerPath))
            {
                throw new ArgumentException("A marker path is required.", nameof(markerPath));
            }

            MarkerPath = Path.GetFullPath(markerPath);
            _identity = identity ?? CoordinatorIdentity.Current;
        }

        /// <summary>Creates a store for <see cref="DefaultFileName"/> inside <paramref name="directory"/>.</summary>
        public static WaveMarkerStore InDirectory(string directory, CoordinatorIdentity? identity = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A directory is required.", nameof(directory));
            }

            return new WaveMarkerStore(Path.Combine(directory, DefaultFileName), identity);
        }

        /// <summary>Where the marker lives.</summary>
        public string MarkerPath { get; }

        /// <summary>The identity this store writes into markers and compares found ones against.</summary>
        public CoordinatorIdentity Identity => _identity;

        /// <summary>
        /// THE STARTUP CALL. Reports whether a wave was in flight, and what it was. Never throws for
        /// a missing, empty, corrupt or locked marker — every one of those is a state, and two of the
        /// three mean a wave was in progress.
        /// </summary>
        public WaveStatus Read()
        {
            string text;

            // Deliberately NOT File.Exists first: File.Exists swallows every error and returns false,
            // so an unreadable directory or a permissions problem would read as "no wave in progress"
            // — precisely the fail-open this component exists to prevent. Opening the file makes the
            // distinction the operating system already knows about visible to us.
            try
            {
                using (var stream = new FileStream(MarkerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException)
            {
                return WaveStatus.None(MarkerPath, _identity);
            }
            catch (DirectoryNotFoundException)
            {
                return WaveStatus.None(MarkerPath, _identity);
            }
            catch (IOException ex)
            {
                return WaveStatus.Unreadable(
                    MarkerPath,
                    "the marker file exists but could not be opened (" + ex.Message + ")",
                    _identity);
            }
            catch (UnauthorizedAccessException ex)
            {
                return WaveStatus.Unreadable(
                    MarkerPath,
                    "the marker file exists but could not be opened (" + ex.Message + ")",
                    _identity);
            }

            var marker = WaveMarkerFormat.TryParse(text, out var problem);
            return marker == null
                ? WaveStatus.Unreadable(MarkerPath, problem, _identity)
                : WaveStatus.InProgress(MarkerPath, marker, _identity);
        }

        /// <summary>
        /// Writes the marker. Call BEFORE the wave starts — the marker's presence is the claim that a
        /// wave may be in flight, so it must precede anything that could crash mid-wave.
        /// </summary>
        /// <exception cref="WaveAlreadyInProgressException">
        /// A marker is already present. Overwriting it would erase the evidence that a previous wave
        /// never completed; acknowledge it with <see cref="DiscardInterruptedWave"/> first.
        /// </exception>
        public void BeginWave(WaveMarker marker)
        {
            if (marker == null)
            {
                throw new ArgumentNullException(nameof(marker));
            }

            var existing = Read();
            if (existing.WaveWasInProgress)
            {
                throw new WaveAlreadyInProgressException(existing);
            }

            var directory = Path.GetDirectoryName(MarkerPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            var payload = Utf8NoBom.GetBytes(WaveMarkerFormat.Serialize(marker));

            try
            {
                // FileMode.Create publishes a ZERO-LENGTH file at the marker path before a single byte
                // of payload is written. From this instant forward, a crash reads as "a wave was in
                // progress" — unreadable, but in progress. That ordering is the requirement.
                using (var stream = new FileStream(
                    MarkerPath,
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
                    "Could not write the wave-in-progress marker to '" + MarkerPath + "'. The wave MUST NOT " +
                    "start: without the marker, a crash during it would look like a clean start and stale " +
                    "rig state would be trusted.",
                    ex);
            }
        }

        /// <summary>
        /// Convenience overload — builds the marker and writes it.
        /// </summary>
        public WaveMarker BeginWave(string waveId, System.Collections.Generic.IEnumerable<string> slots, string programVersion, DateTimeOffset? startedUtc = null)
        {
            var marker = new WaveMarker(waveId, slots, programVersion, startedUtc ?? DateTimeOffset.UtcNow, _identity);
            BeginWave(marker);
            return marker;
        }

        /// <summary>
        /// CLEAN COMPLETION. Clears the marker, which is the ONLY thing that makes the next startup
        /// read as clean.
        /// </summary>
        /// <exception cref="WaveMarkerException">
        /// No marker was present. Completing a wave that was never begun means the marker was cleared
        /// by something else, or never written — either way the next startup's "starting clean" would
        /// be an unearned claim, and that is worth surfacing rather than swallowing.
        /// </exception>
        public void CompleteWave()
        {
            var status = Read();
            if (!status.WaveWasInProgress)
            {
                throw new WaveMarkerException(
                    "CompleteWave was called but no wave-in-progress marker exists at '" + MarkerPath +
                    "'. Either the wave was never begun, or something else cleared the marker while the " +
                    "wave was running — and in the second case a crash between then and now would have " +
                    "read as a clean start.");
            }

            Clear();
        }

        /// <summary>
        /// THE RECOVERY PATH. Acknowledges a marker found on startup and clears it, so a new wave can
        /// begin. Separate from <see cref="CompleteWave"/> on purpose: these two are the same file
        /// operation but opposite claims — one says the wave finished, the other says its results are
        /// being thrown away (X-C item 3), and a log that cannot tell them apart cannot be audited.
        /// </summary>
        /// <param name="found">The status returned by <see cref="Read"/>.</param>
        /// <param name="reason">What the coordinator is recording about the interruption.</param>
        /// <returns>A line for the log, naming what was discarded.</returns>
        public string DiscardInterruptedWave(WaveStatus found, string reason)
        {
            if (found == null)
            {
                throw new ArgumentNullException(nameof(found));
            }

            if (!found.WaveWasInProgress)
            {
                throw new WaveMarkerException(
                    "DiscardInterruptedWave was called with a status that reports no wave in progress. " +
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

            Clear();

            return "DISCARDED INTERRUPTED WAVE: " + found.Describe() + " Reason: " + reason.Trim();
        }

        private void Clear()
        {
            try
            {
                File.Delete(MarkerPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new WaveMarkerException(
                    "Could not clear the wave-in-progress marker at '" + MarkerPath + "'. It will be found " +
                    "on the next startup and that wave's results will be discarded — a false positive, in " +
                    "the safe direction, but the file needs removing before the next run.",
                    ex);
            }
        }
    }
}
