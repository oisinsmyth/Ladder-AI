using System.Security.Cryptography;
using System.Text;

namespace Converter.Claims;

public sealed class ClaimFormatException : Exception
{
    public ClaimFormatException(string message) : base(message) { }
}

// The only code that touches the claims directory.
//
// ACQUISITION IS `FileMode.CreateNew` — the filesystem is the mutex. That is the whole concurrency
// design: NTFS (and POSIX O_EXCL) guarantee exactly one creator of a given path, so two agents racing
// for FB51 resolve without a lock file, a daemon, a retry loop, or a second source of truth. The loser
// gets an IOException, reads the file that beat it, and reports who holds it.
//
// ONE FILE PER CLAIM, never one table. Agents work in separate git worktrees and separate processes;
// a shared table would be a read-modify-write race and, once committed, a merge conflict per claim. A
// directory of disjoint files is conflict-free by construction and needs no coordination to write.
public sealed class ClaimStore
{
    private const string Extension = ".claim";

    private readonly string _root;
    private readonly Func<DateTime> _nowUtc;

    public ClaimStore(string claimsRoot, string projectDir, Func<DateTime>? nowUtc = null)
    {
        ProjectSlug = SlugOf(projectDir);
        RejectDoubledRoot(claimsRoot, ProjectSlug);
        _root = Path.Combine(claimsRoot, ProjectSlug);
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// 🔴 *** THE REGISTRY-FORKING ARGUMENT, REFUSED BY NAME (2026-08-14). ***
    ///
    /// <para>MEASURED, not feared. Two processes claiming ONE resource on ONE project were BOTH
    /// granted at exit 0 — because one passed the shared root <c>…\claims</c> and the other passed
    /// <c>…\claims\test-project001</c>, the path a report had quoted as "the shared claims store".
    /// The second is the RESOLVED store, not the argument: this constructor appends the project slug,
    /// so passing it yields <c>…\claims\test-project001\test-project001</c> — *** A SECOND, EMPTY,
    /// PRIVATE REGISTRY THAT GRANTS EVERY CLAIM AND LOOKS EXACTLY LIKE SUCCESS. ***</para>
    ///
    /// <para><b>Nothing downstream can catch this and nothing ever will:</b> each store is
    /// individually well-formed, correctly named and legitimately empty. There is no state to compare
    /// against — the whole point of a registry is that it is the only copy. So the refusal has to
    /// happen HERE, at the moment the argument is interpreted, and it is placed in the CONSTRUCTOR so
    /// every entry point inherits it: acquire, allocate, list, check and release all build a store
    /// first, and a guard on one command is a guard the next command routes around.</para>
    ///
    /// <para><b>What it does NOT refuse</b>, because a fence that refuses everything is worse than no
    /// fence: a root whose last segment merely resembles a project name (<c>…\claims\test-project002</c>
    /// while working <c>ir/test-project001</c>) is a perfectly good shared root and is accepted. Only
    /// the EXACT doubling — last segment equals THIS project's slug — is refused, because only that
    /// one silently forks the registry for this project.</para>
    ///
    /// <para>Case-insensitively, deliberately: Windows paths are, so <c>…\claims\Test-Project001</c>
    /// doubles just as effectively and would otherwise slip through on a capital letter.</para>
    /// </summary>
    private static void RejectDoubledRoot(string claimsRoot, string slug)
    {
        var trimmed = (claimsRoot ?? string.Empty)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var lastSegment = Path.GetFileName(trimmed);

        if (lastSegment.Length == 0 ||
            !string.Equals(lastSegment, slug, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ClaimFormatException(
            $"--claims '{claimsRoot}' ends with the project name '{slug}', so the store would be " +
            $"'{Path.Combine(claimsRoot!, slug)}' — the project name TWICE. That is almost always the " +
            "RESOLVED STORE PATH passed back in as the ROOT, and it is the one mistake this registry " +
            "cannot detect later: the doubled store is well-formed, correctly named and empty, so it " +
            "GRANTS EVERY CLAIM while another agent on the real store holds the same resource. " +
            "Measured 2026-08-14: two agents, one resource, both granted, both convinced. " +
            $"Pass the shared ROOT instead — '{Path.GetDirectoryName(trimmed)}' — and this tool will " +
            $"append '{slug}' itself. (If the root really is meant to be named after the project, " +
            "rename it: one shared root serves every project.)");
    }

    public string ProjectSlug { get; }

    public string Directory => _root;

    /// <summary>
    /// 🔴 <b>WHY THIS STORE MIGHT NOT BE ABOUT YOUR PROJECT — null when the bucket name identifies one.</b>
    ///
    /// <para><b>The hazard, ruled 2026-08-24 and recorded in <c>docs/notes/multi-agent-operating-guide.md</c>
    /// §1.</b> <see cref="SlugOf"/> keys the bucket on the LAST PATH SEGMENT of <c>--project</c>. A project
    /// whose IR lives in its own named directory (<c>ir/test-project001</c>) gets a bucket nobody else can
    /// reach. A project whose IR directory is simply called <c>ir</c> gets a bucket called <c>ir</c> —
    /// <b>and so does every other project shaped that way.</b> Two unrelated jobs then share one registry.</para>
    ///
    /// <para><b>REPORTED, NEVER GATED, and that is the ruling rather than an omission.</b> The failure
    /// direction is OVER-REFUSAL: the second project's agents are refused against the first project's
    /// reservations, never granted alongside them, which is the safe direction. Refusing on a generic slug
    /// would stop work that is otherwise correct — and there is a live job holding claims in exactly such
    /// a bucket right now, so a gate here would refuse it mid-work. <b>And <see cref="SlugOf"/> is
    /// deliberately unchanged: re-keying orphans reservations that agents are holding while they work.</b></para>
    ///
    /// <para>⚠️ <b>A heuristic, and it says so.</b> The list below cannot know which names are project
    /// names; it recognises the LAYOUT names a repository uses for the directory rather than for the
    /// project. A project genuinely called <c>logic</c> gets a line it does not need, which costs a read;
    /// a generic name not on the list gets no line, which is the state every caller was in before this
    /// existed. Neither is a gate, so neither can stop anything.</para>
    /// </summary>
    public string? BucketAmbiguity => GenericBucketNames.Contains(ProjectSlug)
        ? $"the bucket is named '{ProjectSlug}', which is a GENERIC DIRECTORY NAME and not a project-identifying one. "
          + "The store keys on the LAST SEGMENT of --project, so EVERY project whose IR directory is also called "
          + $"'{ProjectSlug}' shares this one bucket — another job's reservations may be in here, and yours are in with them. "
          + "REPORTED, NOT REFUSED: the direction is over-refusal (you are blocked by a stranger's claim, never granted "
          + "alongside one), and gating here would stop correct work. Read the `project` line inside the claims before "
          + "believing this store is about your project. The slug rule is deliberately unchanged — re-keying would orphan "
          + "reservations that live agents are holding (docs/notes/multi-agent-operating-guide.md, section 1)."
        : null;

    // Layout names, not project names. Deliberately short: every entry here is a name this repository (or
    // an obvious variant of it) uses for the DIRECTORY rather than for the project, and a long speculative
    // list would produce a line on buckets that are perfectly well identified — which is how a report
    // teaches its reader to skip it.
    private static readonly HashSet<string> GenericBucketNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ir", "src", "source", "blocks", "plc", "program", "logic", "code",
        "project", "projects", "export", "exports", "simatic-ml", "xml", "out", "build", "tmp", "temp",
    };

    // The claims directory is keyed by the project's own directory name (`ir/test-project001` ->
    // `test-project001`), so one shared claims root serves every project without the caller inventing
    // an id. A path that yields no name (a bare root) falls back to a hash rather than colliding
    // everything into one bucket.
    private static string SlugOf(string projectDir)
    {
        var trimmed = projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? "project-" + ShortHash(projectDir) : Sanitize(name);
    }

    // Sanitisation alone can map two distinct values onto one filename ("A/B" and "A_B"), which would
    // silently merge two different claims into one — a coordination tool losing a claim is worse than
    // not having one. The hash of the EXACT value makes the mapping injective in practice; the exact
    // value is stored inside the file and re-verified on read, so a collision is detected rather than
    // trusted.
    internal static string FileNameFor(ClaimKind kind, string value) =>
        $"{ClaimKinds.ToToken(kind)}-{Sanitize(NormalizeValue(value))}-{ShortHash(NormalizeValue(value))}{Extension}";

    // A claim value arriving with surrounding whitespace is always an input artefact — the measured
    // case was an agent reading candidate values out of a CRLF text file, so every value carried a
    // trailing '\r'.
    //
    // That broke mutual exclusion SILENTLY, which is the worst way for a coordination tool to fail:
    // FileNameFor hashed the value WITH the '\r', while Parse strips '\r' from every line on read.
    // So the claim was stored under a filename derived from "X\r", its record read back as "X", and
    // a later Find(kind, "X") computed a DIFFERENT filename, missed, and reported the value free.
    // Two agents could then hold the same value, and `claims --json` rendered the store clean
    // because the read path had already dropped the CR.
    //
    // Normalising here makes the filename and the recorded value agree by construction. It is
    // deliberately the same tolerance Parse already applies, so the write path and the read path
    // can no longer disagree about what a value is. A claim value is an identifier — a block name,
    // a tag name, a number — so leading/trailing whitespace is never significant in one.
    private static string NormalizeValue(string value) => value.Trim();

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_');
        }

        return sb.ToString();
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    // Returns the acquired claim, or the claim already holding the slot. Never overwrites: there is no
    // "force acquire", because the only reason a claim exists is that someone else's claim must be
    // able to stop you.
    public (bool Acquired, Claim Winner) TryAcquire(string project, ClaimKind kind, string value, string agent, string? purpose)
    {
        System.IO.Directory.CreateDirectory(_root);

        // Normalise ONCE, here, so the record and the filename are built from the same string.
        // See NormalizeValue: writing the raw value while keying the file on a different one is
        // how mutual exclusion was silently lost.
        value = NormalizeValue(value);

        var claim = new Claim(project, kind, value, agent, Normalize(purpose), _nowUtc());
        var path = Path.Combine(_root, FileNameFor(kind, value));

        // Write to a private temp file first, then MOVE it into the claim slot. The move is the
        // atomic step and it fails if the slot is taken, so it is still the filesystem deciding the
        // winner — but a claim file now only ever appears complete and unlocked.
        //
        // Writing straight into the slot with CreateNew looked equivalent and was not: the winner
        // holds the new file open while it writes, so a loser that immediately tried to read the
        // holder hit a sharing violation and could not say who beat it. Found by the parallel
        // acquisition test, not by reasoning — which is the argument for having it.
        var temp = Path.Combine(_root, $".{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temp, Serialize(claim), new UTF8Encoding(false));

            try
            {
                // overwrite:false — File.Move throws rather than clobbering an existing claim.
                File.Move(temp, path, overwrite: false);
                return (true, claim);
            }
            catch (IOException)
            {
                // Lost the race, or a claim was already sitting there. The file that exists is the
                // authority — read it rather than assuming who won.
                var existing = ReadWithRetry(path);
                if (existing is null)
                {
                    throw new ClaimFormatException(
                        $"claim slot '{path}' exists but could not be read — refusing to overwrite a claim " +
                        "this tool cannot identify. Inspect it by hand.");
                }

                return (false, existing);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                    // A leftover temp is inert: enumeration only ever matches *.claim.
                }
            }
        }
    }

    // Bounded, short, and only on the losing path. The winner's write is already complete before its
    // move, so this is defence against a slow or networked filesystem rather than the local race —
    // and a claim that still cannot be read after this is reported, never overwritten.
    private static Claim? ReadWithRetry(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var claim = TryRead(path);
            if (claim is not null)
            {
                return claim;
            }

            Thread.Sleep(10);
        }

        return null;
    }

    public IReadOnlyList<Claim> All()
    {
        if (!System.IO.Directory.Exists(_root))
        {
            return Array.Empty<Claim>();
        }

        return System.IO.Directory
            .EnumerateFiles(_root, "*" + Extension, SearchOption.TopDirectoryOnly)
            .Select(TryRead)
            .Where(c => c is not null)
            .Select(c => c!)
            .OrderBy(c => ClaimKinds.ToToken(c.Kind), StringComparer.Ordinal)
            .ThenBy(c => c.Value, StringComparer.Ordinal)
            .ToList();
    }

    public Claim? Find(ClaimKind kind, string value) =>
        TryRead(Path.Combine(_root, FileNameFor(kind, value)));

    // Releasing another agent's claim needs --force, and even then it is logged as what it is. The
    // default refusal is not politeness: an agent that can silently release a peer's claim reintroduces
    // exactly the collision the registry removes.
    public bool Release(ClaimKind kind, string value, string agent, bool force, out string reason)
    {
        // Normalised so the reason text names the value the store actually holds (see NormalizeValue).
        value = NormalizeValue(value);
        var path = Path.Combine(_root, FileNameFor(kind, value));
        var existing = TryRead(path);

        if (existing is null)
        {
            reason = $"no claim on {ClaimKinds.ToToken(kind)} '{value}'";
            return false;
        }

        if (!string.Equals(existing.Agent, agent, StringComparison.Ordinal) && !force)
        {
            reason = $"claim on {ClaimKinds.ToToken(kind)} '{value}' is held by agent '{existing.Agent}', not '{agent}' — pass --force to override";
            return false;
        }

        File.Delete(path);
        reason = string.Equals(existing.Agent, agent, StringComparison.Ordinal)
            ? $"released {ClaimKinds.ToToken(kind)} '{value}'"
            : $"FORCED release of {ClaimKinds.ToToken(kind)} '{value}' held by agent '{existing.Agent}'";
        return true;
    }

    internal static string Serialize(Claim claim)
    {
        var sb = new StringBuilder();
        sb.Append("project ").Append(claim.Project).Append('\n');
        sb.Append("kind ").Append(ClaimKinds.ToToken(claim.Kind)).Append('\n');
        sb.Append("value ").Append(claim.Value).Append('\n');
        sb.Append("agent ").Append(claim.Agent).Append('\n');
        sb.Append("purpose ").Append(claim.Purpose ?? string.Empty).Append('\n');
        sb.Append("created ").Append(claim.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append('\n');
        return sb.ToString();
    }

    internal static Claim Parse(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var space = line.IndexOf(' ');
            // A key with no value is still a key: `purpose` is routinely empty, and treating the
            // whole line as a malformed record would make an ordinary claim unreadable.
            var key = space < 0 ? line : line[..space];
            var value = space < 0 ? string.Empty : line[(space + 1)..];
            fields[key] = value;
        }

        foreach (var required in new[] { "project", "kind", "value", "agent", "created" })
        {
            if (!fields.ContainsKey(required))
            {
                throw new ClaimFormatException($"claim record is missing '{required}'");
            }
        }

        if (!ClaimKinds.TryParse(fields["kind"], out var kind))
        {
            throw new ClaimFormatException($"claim record has unknown kind '{fields["kind"]}'");
        }

        if (!DateTime.TryParse(
                fields["created"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var created))
        {
            throw new ClaimFormatException($"claim record has unparseable created '{fields["created"]}'");
        }

        var purpose = fields.TryGetValue("purpose", out var p) && p.Length > 0 ? p : null;
        return new Claim(fields["project"], kind, fields["value"], fields["agent"], purpose, created);
    }

    private static Claim? TryRead(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is ClaimFormatException or IOException)
        {
            return null;
        }
    }

    // A newline in a purpose would forge a second field on read, so it is folded rather than escaped —
    // the field is a human note, and no note needs a line break badly enough to justify an escaping
    // grammar in a coordination record.
    private static string? Normalize(string? purpose) =>
        purpose is null ? null : purpose.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
