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
        _root = Path.Combine(claimsRoot, ProjectSlug);
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
    }

    public string ProjectSlug { get; }

    public string Directory => _root;

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
        $"{ClaimKinds.ToToken(kind)}-{Sanitize(value)}-{ShortHash(value)}{Extension}";

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
