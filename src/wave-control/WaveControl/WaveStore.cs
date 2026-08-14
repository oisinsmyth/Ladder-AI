using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>One slot as it sits in the shared store, with who put it there and when.</summary>
    public sealed class StoredSlot
    {
        internal StoredSlot(TestSlot slot, string submittedBy, DateTimeOffset submittedUtc)
        {
            Slot = slot;
            SubmittedBy = submittedBy;
            SubmittedUtc = submittedUtc;
        }

        /// <summary>The slot.</summary>
        public TestSlot Slot { get; }

        /// <summary>Which agent submitted it.</summary>
        public string SubmittedBy { get; }

        /// <summary>When.</summary>
        public DateTimeOffset SubmittedUtc { get; }

        /// <inheritdoc />
        public override string ToString() => Slot.Id + " by " + SubmittedBy;
    }

    /// <summary>What acquiring the store's lease cost — the observable half of serialisation.</summary>
    public sealed class LeaseAcquisition : IDisposable
    {
        private readonly FileStream _handle;
        private readonly string _leasePath;

        internal LeaseAcquisition(FileStream handle, string leasePath, string agent, TimeSpan waited, int attempts)
        {
            _handle = handle;
            _leasePath = leasePath;
            Agent = agent;
            Waited = waited;
            Attempts = attempts;
        }

        /// <summary>Who holds it.</summary>
        public string Agent { get; }

        /// <summary>
        /// *** HOW LONG THIS AGENT QUEUED BEHIND ANOTHER. *** Reported rather than merely endured: the
        /// campaign is measuring whether submissions actually serialise, and a correct-but-invisible
        /// lock produces a run that looks concurrent and was not.
        /// </summary>
        public TimeSpan Waited { get; }

        /// <summary>How many attempts it took. More than one means somebody else held it.</summary>
        public int Attempts { get; }

        /// <summary>TRUE when this acquisition actually queued.</summary>
        public bool Contended => Attempts > 1;

        /// <inheritdoc />
        /// <remarks>
        /// 🔴 *** THE LEASE FILE IS NOT DELETED, AND THAT IS A FIX RATHER THAN AN OMISSION. *** It
        /// used to be, and at 48 concurrent agents that produced an UNHANDLED
        /// <see cref="UnauthorizedAccessException"/> — measured 2026-08-14, 3 crashes at 48 and 1 at
        /// 64, none at 32 or below. *** THE LOCK IS THE EXCLUSIVE HANDLE, NOT THE FILE'S EXISTENCE ***
        /// (<see cref="FileShare.None"/>), so deleting it bought nothing and opened a race: on Windows
        /// a file that is deleted while another handle is open enters DELETE-PENDING, and a concurrent
        /// <c>CreateFile</c> on it returns ACCESS_DENIED — surfaced as
        /// <see cref="UnauthorizedAccessException"/>, NOT the <see cref="IOException"/> a sharing
        /// violation raises, so it escaped the retry loop entirely.
        /// <para>
        /// Leaving the file also makes the LAST HOLDER readable after the fact, which is the direction
        /// this type was already going: the queueing is meant to be observable, not merely correct.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            _handle.Dispose();
        }

        /// <inheritdoc />
        public override string ToString() =>
            "lease held by " + Agent + " after " + (int)Waited.TotalMilliseconds + " ms and " +
            Attempts + " attempt(s)" + (Contended ? " — CONTENDED" : string.Empty);
    }

    /// <summary>Raised when the store cannot be used at all.</summary>
    public class WaveStoreException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public WaveStoreException(string message) : base(message)
        {
        }

        /// <summary>Creates the exception with an inner cause.</summary>
        public WaveStoreException(string message, Exception inner) : base(message, inner)
        {
        }

        /// <summary>
        /// FALSE for an unusable store. *** THE DISTINCTION IS THE EXIT CODE, AND IT DECIDES WHAT A
        /// HARNESS DOES NEXT. *** See <see cref="WaveStoreContendedException"/>.
        /// </summary>
        public virtual bool Retryable => false;
    }

    /// <summary>
    /// Raised when the store's lease could not be taken because ANOTHER AGENT HELD IT. *** THIS IS
    /// THE MECHANISM WORKING, AND IT IS RETRYABLE. ***
    /// </summary>
    /// <remarks>
    /// <para>
    /// 🔴 *** RULED 2026-08-14, AND THIS TYPE EXISTS BECAUSE THE FIRST BUILD GOT THE EXIT CODE
    /// WRONG. *** A lease timeout originally raised a plain <see cref="WaveStoreException"/>, which
    /// the driver reported as exit 2. The campaign's contract defines exit 2 as *"nothing was decided
    /// and RETRYING WILL NOT HELP"* — and here retrying WOULD help, because the contention is
    /// transient by construction. *** A HARNESS KEYING ON THAT CODE GIVES UP WHERE IT SHOULD BACK
    /// OFF, which is a wrong answer rather than a rough edge. ***
    /// </para>
    /// <para>
    /// So a lease timeout is now *** exit 1 — REFUSED, and the reason names it RETRYABLE. *** Exit 2
    /// keeps its meaning for a store that is genuinely unusable: malformed, inside a worktree, or an
    /// argument that cannot be honoured. Widening exit 2 to cover contention would have made the code
    /// useless for the case it exists for.
    /// </para>
    /// </remarks>
    public sealed class WaveStoreContendedException : WaveStoreException
    {
        /// <summary>Creates the exception.</summary>
        public WaveStoreContendedException(string message) : base(message)
        {
        }

        /// <summary>TRUE — the contention is transient and the caller should back off, not give up.</summary>
        public override bool Retryable => true;
    }

    /// <summary>
    /// THE SHARED WAVE STORE — the submitted slots, in a place every agent can see.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** A DECISION THAT CANNOT OUTLIVE THE PROCESS CANNOT COORDINATE TWO AGENTS. *** Admission and
    /// colouring were, until now, computable only inside one process: correct, tested, and reachable by
    /// nothing. This is the state both agents can see, and it is the same argument that made the claims
    /// directory shared.
    /// </para>
    /// <para>
    /// 🔴 *** THE STORE DIRECTORY IS REQUIRED AND HAS NO DEFAULT, AND A PATH INSIDE A WORKTREE IS
    /// REFUSED. *** This is the exact failure `--claims` was given no default to avoid: *a per-worktree
    /// store is always empty, admits everything, and looks exactly like success.* Two agents in two
    /// worktrees would each colour their own slot alone, each report "1 wave, admitted", and the
    /// campaign would record thirty green multi-agent rows that never shared anything. The live claims
    /// registry sits at <c>C:\ProgramData\Ladder-AI\claims\...</c>, outside every worktree, and this
    /// belongs beside it. <see cref="AllowWorktreeStore"/> is the named escape, for tests only.
    /// </para>
    /// <para>
    /// WRITES ARE ATOMIC — temp file in the same directory, flushed, then renamed — so a reader never
    /// sees a half-written store, and a torn write leaves the previous whole state. The same mechanism
    /// and the same honest residual as <see cref="CoordinatorStateStore"/>: the rename's durability
    /// across a power cut cannot be forced from .NET on Windows, which is the safe direction.
    /// </para>
    /// <para>
    /// *** AND THE LEASE IS OBSERVABLE, NOT MERELY CORRECT. *** Every mutation takes an exclusive
    /// file lease and REPORTS what it cost — how long it queued and how many attempts it took. The
    /// campaign is testing whether concurrent submission serialises; a lock that works silently would
    /// let a serialised run be reported as a parallel one, which is the vacuity this whole component
    /// keeps guarding against.
    /// </para>
    /// </remarks>
    public sealed class WaveStore
    {
        /// <summary>The slots file inside the store directory.</summary>
        public const string SlotsFileName = "wave-slots.state";

        /// <summary>The lease file inside the store directory.</summary>
        public const string LeaseFileName = "wave-store.lease";

        /// <summary>
        /// Written once, the first time the store is ever published. *** ITS PURPOSE IS TO MAKE THE
        /// ABSENCE OF THE SLOTS FILE MEAN SOMETHING. ***
        /// </summary>
        public const string InitialisedFileName = "wave-store.initialised";

        private const string Sentinel = "end";
        private const int FormatVersion = 1;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <param name="storeDirectory">Where the shared state lives. Required; no default.</param>
        /// <param name="allowWorktreeStore">
        /// Permit a store inside a git worktree. FOR TESTS ONLY — see this type's remarks.
        /// </param>
        public WaveStore(string? storeDirectory, bool allowWorktreeStore = false)
        {
            if (string.IsNullOrWhiteSpace(storeDirectory))
            {
                throw new WaveStoreException(
                    "A wave-store directory is required and has no default. It must be shared by every " +
                    "agent working the same project: a per-worktree store is always empty, admits " +
                    "everything, and looks exactly like success — which is why `converter claim`'s " +
                    "--claims has no default either.");
            }

            Directory = Path.GetFullPath(storeDirectory!.Trim());
            AllowWorktreeStore = allowWorktreeStore;

            if (!allowWorktreeStore)
            {
                var worktree = FindEnclosingWorktree(Directory);
                if (worktree != null)
                {
                    throw new WaveStoreException(
                        "The wave store '" + Directory + "' is inside the git worktree '" + worktree +
                        "'. Two agents in two worktrees would each get their own empty store, each " +
                        "colour their own slot alone, and each report a clean admission — thirty green " +
                        "multi-agent rows that never shared anything. Put it beside the claims registry, " +
                        "outside every worktree.");
                }
            }
        }

        /// <summary>Where the shared state lives.</summary>
        public string Directory { get; }

        /// <summary>Whether the worktree guard was waived.</summary>
        public bool AllowWorktreeStore { get; }

        /// <summary>The slots file.</summary>
        public string SlotsPath => Path.Combine(Directory, SlotsFileName);

        /// <summary>The lease file.</summary>
        public string LeasePath => Path.Combine(Directory, LeaseFileName);

        /// <summary>The marker recording that this store has been published at least once.</summary>
        public string InitialisedPath => Path.Combine(Directory, InitialisedFileName);

        /// <summary>
        /// Take the exclusive lease. Reports what it cost.
        /// </summary>
        /// <param name="agent">Who is asking.</param>
        /// <param name="timeout">How long to queue before giving up.</param>
        /// <exception cref="WaveStoreException">The lease could not be taken within the timeout.</exception>
        public LeaseAcquisition AcquireLease(string agent, TimeSpan timeout)
        {
            System.IO.Directory.CreateDirectory(Directory);

            var who = string.IsNullOrWhiteSpace(agent) ? "<unnamed agent>" : agent.Trim();
            var clock = Stopwatch.StartNew();
            var attempts = 0;
            var sawSharingViolation = false;
            var lastAccessDenied = (UnauthorizedAccessException?)null;

            while (true)
            {
                attempts++;

                try
                {
                    var handle = new FileStream(LeasePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    var note = Utf8NoBom.GetBytes(
                        "held-by=" + who + "\nacquired-utc=" +
                        DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) +
                        "\nwaited-ms=" + ((int)clock.Elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + "\n");

                    handle.Write(note, 0, note.Length);
                    handle.Flush();

                    return new LeaseAcquisition(handle, LeasePath, who, clock.Elapsed, attempts);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // *** ACCESS_DENIED IS TRANSIENT HERE, AND IT IS NOT THE EXCEPTION YOU EXPECT. ***
                    // A lease file in Windows' delete-pending state answers CreateFile with
                    // ACCESS_DENIED rather than a sharing violation. Releasing no longer deletes the
                    // file, so this should not arise — it is kept because "should not arise" is a claim
                    // about a mechanism, and an unhandled exception here crashes an agent mid-campaign
                    // with a stack trace instead of a named refusal.
                    lastAccessDenied = ex;

                    if (clock.Elapsed >= timeout)
                    {
                        // Persistent ACCESS_DENIED with no sharing violation ever seen is a PERMISSIONS
                        // problem, not contention — and retrying will NOT help, which is exit 2's
                        // meaning. Reporting it as retryable would send a harness into a backoff loop
                        // against a directory it will never be allowed to write.
                        if (!sawSharingViolation)
                        {
                            throw new WaveStoreException(
                                "'" + who + "' was denied access to the wave-store lease at '" + LeasePath +
                                "' for the whole " + (int)timeout.TotalMilliseconds + " ms (" + attempts +
                                " attempts) and never once saw a sharing violation. That is a PERMISSIONS " +
                                "problem rather than contention: another agent holding the lease produces " +
                                "a sharing violation, not an access denial. Retrying will not help.",
                                lastAccessDenied);
                        }

                        throw new WaveStoreContendedException(
                            "'" + who + "' did not obtain the wave-store lease at '" + LeasePath +
                            "' in " + (int)timeout.TotalMilliseconds + " ms (" + attempts +
                            " attempts). OBSERVED: sharing violations, and access denied on the last " +
                            "attempt. NOT OBSERVABLE FROM HERE: whether the holder is working or hung. " +
                            "BACK OFF AND RETRY. If it persists, read the lease file — it names its " +
                            "last writer — and judge the holder yourself.");
                    }

                    System.Threading.Thread.Sleep(25);
                }
                catch (IOException)
                {
                    sawSharingViolation = true;

                    if (clock.Elapsed >= timeout)
                    {
                        // *** THIS MESSAGE STATES WHAT WAS OBSERVED AND HANDS THE JUDGEMENT OVER,
                        // AND THAT IS A CORRECTION. *** It used to assert "another agent holds it" and
                        // "the contention is transient" — and earlier still, that a long timeout "means
                        // the holder is stuck rather than busy". *** NONE OF THOSE ARE REACHABLE FROM
                        // HERE. *** What the code knows is that an exclusive open failed with a sharing
                        // violation, repeatedly; it cannot see whose handle it is, whether that process
                        // is progressing, or whether it is hung. A human reads this AT THE MOMENT THEY
                        // ARE DECIDING WHAT TO DO, under time pressure — and a sentence asserting a
                        // conclusion the code could not reach will send them to kill a process that was
                        // merely slow. An error message that ships a false claim is worse than a wrong
                        // comment, because the comment is read at leisure and this is not.
                        throw new WaveStoreContendedException(
                            "'" + who + "' did not obtain the wave-store lease at '" + LeasePath +
                            "' in " + (int)timeout.TotalMilliseconds + " ms (" + attempts +
                            " attempts). OBSERVED: the lease file was open elsewhere with exclusive " +
                            "access on every attempt. NOT OBSERVABLE FROM HERE: whether the holder is " +
                            "working normally or is hung — a busy holder and a stuck one are " +
                            "indistinguishable from this side, and no timeout length distinguishes " +
                            "them. Serialised submission is the design, so this is a REFUSAL (exit 1), " +
                            "not an unusable store (exit 2). BACK OFF AND RETRY. If it persists, read " +
                            "the lease file — it names its last writer — and judge the holder yourself.");
                    }

                    // Unconditional: this used to sit behind #if NETSTANDARD2_0, which would have
                    // become a BUSY SPIN the moment the library gained a second target framework.
                    System.Threading.Thread.Sleep(25);
                }
            }
        }

        /// <summary>Read the submitted slots. An absent store is EMPTY, and that is a legitimate state here.</summary>
        /// <remarks>
        /// Unlike the coordinator state, an absent slots file genuinely means "nobody has submitted
        /// yet" — there is no prior state a crash could have lost, because a submission that was never
        /// written was never admitted. What is NOT legitimate is a store that cannot be PARSED, which
        /// throws rather than reading as empty.
        /// </remarks>
        public IReadOnlyList<StoredSlot> Read()
        {
            string text;

            try
            {
                using (var stream = new FileStream(SlotsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException)
            {
                return NoSlotsFile();
            }
            catch (DirectoryNotFoundException)
            {
                return NoSlotsFile();
            }

            return Parse(text);
        }

        /// <summary>
        /// The slots file is not there. *** THAT IS ONLY "NOBODY HAS SUBMITTED YET" IF THE STORE WAS
        /// NEVER PUBLISHED — otherwise it is a WRITE THAT DIED MID-REPLACE, AND READING IT AS EMPTY
        /// DISCARDS THE WHOLE WAVE SET. ***
        /// </summary>
        /// <remarks>
        /// 🔴 *** MEASURED 2026-08-14, AND IT IS THE WORST DEFECT THIS COMPONENT HAS HAD. *** 27 agents
        /// were killed mid-write against a store holding 8 committed slots. The file never tore — the
        /// temp-then-rename held — but the store came back holding ONE slot, a LATER one, and the eight
        /// seeds were gone. <c>File.Replace</c> is not atomic against process death: it leaves
        /// <c>~RF*.TMP</c> backups and has a window in which the DESTINATION DOES NOT EXIST. A reader
        /// landing in that window got <see cref="FileNotFoundException"/>, treated it as an empty store,
        /// and the next submission wrote only its own slot — *** REPORTING SUCCESS. ***
        /// <para>
        /// So absence now has to be qualified, exactly as <c>DeployedProgram.From</c> refuses an empty
        /// list: "the device holds nothing" and "we could not read the device" are the same empty list
        /// and opposite actions. Here it is "nobody has submitted" versus "a write died". The marker
        /// file is what separates them, and it is written BEFORE the first publish so that a crash
        /// during the very first write is caught too.
        /// </para>
        /// </remarks>
        private IReadOnlyList<StoredSlot> NoSlotsFile()
        {
            if (File.Exists(InitialisedPath))
            {
                throw new WaveStoreException(
                    "The wave store at '" + Directory + "' has been published before — '" +
                    InitialisedFileName + "' is present — but '" + SlotsFileName + "' is ABSENT. *** THIS " +
                    "IS NOT AN EMPTY STORE. *** A write was interrupted between removing the old file and " +
                    "publishing the new one, and reading it as empty would DISCARD EVERY SUBMITTED SLOT " +
                    "and report success. Recover the newest '" + SlotsFileName + ".tmp-*' or " +
                    "'" + SlotsFileName + "~RF*.TMP' beside it, or reset the store deliberately.");
            }

            return new StoredSlot[0];
        }

        /// <summary>Replace the store's contents. Atomic: temp file, flushed, then renamed.</summary>
        public void Write(IEnumerable<StoredSlot> slots)
        {
            System.IO.Directory.CreateDirectory(Directory);

            // BEFORE the first publish, so a crash during the very first write is caught too. An
            // absent marker is the ONLY thing that makes an absent slots file mean "nobody submitted".
            if (!File.Exists(InitialisedPath))
            {
                using (var marker = new FileStream(InitialisedPath, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
                {
                    var note = Utf8NoBom.GetBytes(
                        "This store has been published at least once. Its presence is what makes an ABSENT\n" +
                        "wave-slots.state a REFUSAL rather than an empty store: a write interrupted mid-replace\n" +
                        "leaves no slots file, and reading that as empty discards every submitted slot.\n");

                    marker.Write(note, 0, note.Length);
                    marker.Flush(flushToDisk: true);
                }
            }

            var payload = Utf8NoBom.GetBytes(Serialize(slots.ToArray()));
            var temporary = SlotsPath + ".tmp-" + Guid.NewGuid().ToString("N");

            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(SlotsPath))
                {
                    File.Replace(temporary, SlotsPath, null);
                }
                else
                {
                    File.Move(temporary, SlotsPath);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (IOException)
                {
                }

                throw new WaveStoreException(
                    "Could not write the wave store at '" + SlotsPath + "'. The previous whole state is " +
                    "still on disk and still readable.",
                    ex);
            }
        }

        /// <summary>Empty the store. For a campaign run's reset between scenarios.</summary>
        public void Clear() => Write(new StoredSlot[0]);

        /// <summary>Builds a stored slot.</summary>
        public static StoredSlot Stored(TestSlot slot, string submittedBy, DateTimeOffset submittedUtc) =>
            new StoredSlot(slot, submittedBy, submittedUtc);

        private static string Serialize(IReadOnlyList<StoredSlot> slots)
        {
            var sb = new StringBuilder();
            sb.Append("# ladder wave store - the SUBMITTED SLOTS every agent shares.\n");
            sb.Append("# A per-worktree copy of this file is always empty, admits everything, and looks\n");
            sb.Append("# exactly like success. It belongs beside the claims registry, outside every worktree.\n");
            sb.Append("format=").Append(FormatVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("slots=").Append(slots.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');

            foreach (var stored in slots.OrderBy(s => s.Slot.Id, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("slot=").Append(string.Join("\t", new[]
                {
                    Escape(stored.Slot.Id),
                    Escape(stored.SubmittedBy),
                    stored.SubmittedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture),
                    Escape(stored.Slot.Agent),
                    Escape(string.Join(",", stored.Slot.ReachableState.ToArray())),
                    Escape(stored.Slot.ReachableStateProvenance),
                    Escape(string.Join(",", stored.Slot.ModelInstances.ToArray())),
                    stored.Slot.WidthRegisters.ToString(CultureInfo.InvariantCulture),
                    Escape(stored.Slot.TestsModel),
                })).Append('\n');
            }

            sb.Append(Sentinel).Append('\n');
            return sb.ToString();
        }

        private static IReadOnlyList<StoredSlot> Parse(string text)
        {
            var normalised = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

            if (!normalised.EndsWith("\n", StringComparison.Ordinal))
            {
                throw new WaveStoreException(
                    "The wave store does not end with a newline — the write was torn off before the '" +
                    Sentinel + "' terminator was complete. Refused rather than read short: a store read " +
                    "short is a submission that silently never happens.");
            }

            // *** NOT Trim(). *** A slot line's LAST field is TestsModel, which is empty for every
            // ordinary block slot — so the line ends in a tab, and trimming it removes the field
            // entirely. Split then yields 8 fields instead of 9, and EVERY store written by a
            // non-model slot is unreadable. Found by RUNNING the driver, not by a unit test: the tests
            // round-trip through objects, and this defect lives strictly between the file and the
            // parser.
            var lines = normalised
                .Split('\n')
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
                .ToList();

            if (lines.Count == 0 || !string.Equals(lines[lines.Count - 1], Sentinel, StringComparison.Ordinal))
            {
                throw new WaveStoreException("The wave store has no '" + Sentinel + "' terminator.");
            }

            lines.RemoveAt(lines.Count - 1);

            var declared = -1;
            var slots = new List<StoredSlot>();

            foreach (var line in lines)
            {
                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    throw new WaveStoreException("The wave store carries a line that is not 'key=value'.");
                }

                var key = line.Substring(0, split);
                var value = line.Substring(split + 1);

                switch (key)
                {
                    case "format":
                        if (value != FormatVersion.ToString(CultureInfo.InvariantCulture))
                        {
                            throw new WaveStoreException("The wave store is format " + value + "; this build reads " + FormatVersion + ".");
                        }

                        break;

                    case "slots":
                        declared = int.Parse(value, CultureInfo.InvariantCulture);
                        break;

                    case "slot":
                    {
                        var f = value.Split('\t');
                        if (f.Length != 9)
                        {
                            throw new WaveStoreException("A slot line has " + f.Length + " fields, not 9.");
                        }

                        var slot = new TestSlot(
                            Unescape(f[0]),
                            Unescape(f[3]),
                            Split(Unescape(f[4])),
                            Unescape(f[5]),
                            Split(Unescape(f[6])),
                            int.Parse(f[7], CultureInfo.InvariantCulture),
                            Unescape(f[8]));

                        slots.Add(new StoredSlot(
                            slot,
                            Unescape(f[1]),
                            DateTimeOffset.Parse(f[2], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)));
                        break;
                    }

                    default:
                        throw new WaveStoreException("The wave store carries a key this build does not know: '" + key + "'.");
                }
            }

            if (declared != slots.Count)
            {
                throw new WaveStoreException(
                    "The wave store declares " + declared + " slots and carries " + slots.Count +
                    ". A store read short is a submission that silently never happens.");
            }

            return slots;
        }

        private static IEnumerable<string> Split(string value) =>
            value.Length == 0 ? new string[0] : value.Split(',');

        private static string Escape(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n");

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
                    case 't': sb.Append('\t'); break;
                    case 'n': sb.Append('\n'); break;
                    default: sb.Append(value[i]); break;
                }
            }

            return sb.ToString();
        }

        private static string? FindEnclosingWorktree(string directory)
        {
            var current = new DirectoryInfo(directory);

            while (current != null)
            {
                if (System.IO.Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }
    }
}
