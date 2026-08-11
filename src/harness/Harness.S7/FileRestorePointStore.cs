using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeviceGuard;

namespace Harness.S7;

/// <summary>
/// A restore point that exists as a file, so that "there is a way back" is a thing you can look at.
///
/// <para>Until now <see cref="IRestorePointStore"/> had exactly one implementation,
/// <see cref="NoRestorePoints"/>, which answers false to everything and therefore refuses every
/// write. That is the right default and a useless production answer, so this is the first store that
/// can say yes — and the whole design question is what it must be true for it to say yes.</para>
///
/// <para><b>Four conditions, all of which a naive store would skip.</b></para>
/// <list type="number">
/// <item><b>Captured, not requested.</b> The manifest is written with <c>verified: false</c>, read
/// back off the disk, its content hashed against what was meant to be there, and only then rewritten
/// as verified. A file that failed to flush, or landed truncated, never reaches the verified
/// state. <see cref="IRestorePointStore"/>'s own contract insists on this distinction.</item>
///
/// <item><b>Covering what the run will write.</b> The store is constructed with the areas this run
/// declared, and refuses a restore point that does not cover all of them. A restore point for a
/// different DB is not a restore point for this write, and "a restore point exists" is exactly the
/// kind of question that gets answered yes too easily.</item>
///
/// <item><b>Of the right kind.</b> This store captures DB byte ranges. It cannot capture program
/// blocks, and it cannot capture the retentive/actual-value reinitialisation a download performs —
/// the hard case `10-non-goals.md` #4(b) names explicitly. So it only ever produces
/// <see cref="RestorePointScope.ProcessDataOnly"/>, and a caller that declares it needs
/// <see cref="RestorePointScope.ProgramAndData"/> is refused, permanently, rather than being handed
/// something that looks close enough. When a download path is built it will need its own store, and
/// this refusal is what will make that impossible to forget.</item>
///
/// <item><b>Still current.</b> An optional maximum age. A restore point captured last month describes
/// a device that has been changed since, and restoring it would be its own incident.</item>
/// </list>
///
/// <para><b>What it does not do.</b> It does not detect that the device changed between capture and
/// write — nothing short of re-reading everything would. It does not capture non-DB memory (M, I, Q),
/// nor CPU configuration, nor the retentivity flags themselves. Each of those is a real gap and each
/// is named here rather than in a ticket, because the failure mode of a restore-point store is
/// believing it covers more than it does.</para>
/// </summary>
public sealed class FileRestorePointStore : IRestorePointStore
{
    private const string FileSuffix = ".restorepoint.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly IReadOnlyList<string> _requiredAreas;
    private readonly RestorePointScope _requiredScope;
    private readonly TimeSpan? _maxAge;
    private readonly Func<DateTimeOffset> _clock;

    /// <param name="directory">Where manifests live. Created on capture if absent.</param>
    /// <param name="requiredAreas">
    /// The areas this run will write — normally <c>VectorSet.DeclaredAreas(vectors)</c>, the same list
    /// the write scope is built from, so the coverage question is asked against what will actually be
    /// touched rather than against a hand-maintained list.
    /// </param>
    /// <param name="requiredScope">
    /// What class of write this run performs. Leave at <see cref="RestorePointScope.ProcessDataOnly"/>
    /// for the conformance harness; anything that downloads a block must ask for
    /// <see cref="RestorePointScope.ProgramAndData"/> and will be refused, correctly, until a store
    /// exists that can capture it.
    /// </param>
    /// <param name="clock">Injected so the age check is testable.</param>
    public FileRestorePointStore(
        string directory,
        IReadOnlyList<string> requiredAreas,
        RestorePointScope requiredScope = RestorePointScope.ProcessDataOnly,
        TimeSpan? maxAge = null,
        Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new S7ConfigurationException("a restore point store needs a directory to keep manifests in.");

        _directory = directory;
        _requiredAreas = requiredAreas ?? Array.Empty<string>();
        _requiredScope = requiredScope;
        _maxAge = maxAge;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Why the last <see cref="HasVerifiedRestorePoint"/> said no.
    ///
    /// The interface returns a bare bool, which is right for a fence — but a write refused for want of
    /// a restore point is deeply confusing without knowing whether the file was missing, stale, or
    /// covering the wrong DB. The guard does not read this; a person does.
    /// </summary>
    public string? LastRefusal { get; private set; }

    public string PathFor(string normalizedTarget) =>
        Path.Combine(_directory, SafeFileName(normalizedTarget) + FileSuffix);

    /// <summary>
    /// Read the manifest for a target, if one is on disk and parses. Reports NOTHING about whether it
    /// is acceptable — that is <see cref="HasVerifiedRestorePoint"/>'s job and this must never be
    /// mistaken for it. It exists so a caller can ask the byte-level question the fence cannot
    /// (<see cref="RestorePointManifest.Covers"/>), and so a person can be shown what is actually held
    /// rather than only that something is.
    /// </summary>
    public bool TryLoad(string normalizedTarget, out RestorePointManifest? manifest)
    {
        manifest = null;

        if (string.IsNullOrWhiteSpace(normalizedTarget)) return false;

        var path = PathFor(normalizedTarget);
        if (!File.Exists(path)) return false;

        try
        {
            manifest = JsonSerializer.Deserialize<RestorePointManifest>(File.ReadAllText(path), ReadOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return manifest is not null;
    }

    // ---------------------------------------------------------------- the fence's question

    public bool HasVerifiedRestorePoint(string normalizedTarget)
    {
        LastRefusal = null;

        if (string.IsNullOrWhiteSpace(normalizedTarget))
            return No("no target was given, so no restore point can be looked up.");

        // Refused before touching the disk: this store can never produce a program-and-data restore
        // point, so no file it holds could satisfy the requirement. Failing here rather than at a
        // coverage check makes the reason unmistakable.
        if (_requiredScope == RestorePointScope.ProgramAndData)
        {
            return No(
                "this run declares it needs a PROGRAM AND DATA restore point (a download, an online " +
                "edit or a hardware-configuration change), and FileRestorePointStore captures DB byte " +
                "ranges only. Per 10-non-goals.md #4(b) a partial restore point is not a restore " +
                "point, so this is refused rather than approximated.");
        }

        var path = PathFor(normalizedTarget);
        if (!File.Exists(path))
            return No($"no restore point file at '{path}'. Capture one before writing.");

        RestorePointManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RestorePointManifest>(File.ReadAllText(path), ReadOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return No($"restore point '{path}' could not be read: {ex.Message}");
        }

        if (manifest is null)
            return No($"restore point '{path}' parsed to nothing.");

        if (manifest.FormatVersion != RestorePointManifest.CurrentFormatVersion)
            return No($"restore point '{path}' is format version {manifest.FormatVersion}; this build " +
                      $"understands {RestorePointManifest.CurrentFormatVersion}.");

        // The target is inside the file as well as in its name, so a renamed or copied manifest cannot
        // authorize a write to a device it was never captured from.
        if (!string.Equals(manifest.Target?.Trim(), normalizedTarget.Trim(), StringComparison.OrdinalIgnoreCase))
            return No($"restore point '{path}' was captured from '{manifest.Target}', not '{normalizedTarget}'.");

        if (!manifest.Verified)
            return No($"restore point '{path}' records a capture that was never verified. A requested " +
                      "capture is not a captured restore point.");

        if (!Enum.TryParse<RestorePointScope>(manifest.Scope, ignoreCase: true, out var scope))
            return No($"restore point '{path}' declares an unrecognised scope '{manifest.Scope}'.");

        if (!Satisfies(scope, _requiredScope))
            return No($"restore point '{path}' covers {scope}, but this run needs {_requiredScope}.");

        var missing = _requiredAreas
            .Where(a => !manifest.Areas.Contains(a, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missing.Count > 0)
            return No($"restore point '{path}' does not cover " +
                      $"{string.Join(", ", missing.Select(m => $"'{m}'"))}, which this run declared it " +
                      "will write. A restore point for other areas is not a way back from this write.");

        if (_maxAge is { } age)
        {
            if (!DateTimeOffset.TryParse(manifest.CapturedUtc, out var captured))
                return No($"restore point '{path}' has an unparseable capture time '{manifest.CapturedUtc}'.");

            var elapsed = _clock() - captured;
            if (elapsed > age)
                return No($"restore point '{path}' is {elapsed.TotalHours:F1} h old, past the " +
                          $"{age.TotalHours:F1} h limit this run set.");
        }

        foreach (var region in manifest.Regions)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(region.Base64 ?? string.Empty);
            }
            catch (FormatException)
            {
                return No($"restore point '{path}': region {region.Area} DB{region.DbNumber} holds " +
                          "content that is not valid base64.");
            }

            if (bytes.Length != region.Size)
                return No($"restore point '{path}': region {region.Area} DB{region.DbNumber} declares " +
                          $"{region.Size} byte(s) but holds {bytes.Length}.");

            if (!string.Equals(Hash(bytes), region.Sha256, StringComparison.OrdinalIgnoreCase))
                return No($"restore point '{path}': region {region.Area} DB{region.DbNumber} does not " +
                          "match its recorded hash — the file has been altered or corrupted.");
        }

        if (manifest.Regions.Count == 0)
            return No($"restore point '{path}' contains no regions, so it restores nothing.");

        return true;
    }

    // ---------------------------------------------------------------- capture

    /// <summary>
    /// Read the named regions off the device and write a verified restore point.
    ///
    /// <para>Throws rather than returning a failure: `10-non-goals.md` #4(b) says that if the restore
    /// point cannot be captured, the write does not happen, and a caller that has to remember to check
    /// a return value is a caller that will one day forget.</para>
    ///
    /// <para><b>What "verified" means here, precisely.</b> The manifest is persisted, re-read from
    /// disk, and every region's bytes are hashed against what was captured. It does NOT mean the
    /// device data was stable while it was read — on a running plant it will not have been, and
    /// demanding stability would make capture impossible exactly where it is needed. It is a snapshot
    /// of a moving thing, honestly labelled with the moment it was taken.</para>
    /// </summary>
    public RestorePointManifest Capture(
        string normalizedTarget,
        IReadOnlyList<RestoreRegion> regions,
        string capturedBy,
        IRegionAccess device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (string.IsNullOrWhiteSpace(normalizedTarget))
            throw new S7ConfigurationException("a restore point must name the device it came from.");

        if (string.IsNullOrWhiteSpace(capturedBy))
            throw new S7ConfigurationException(
                "a restore point must record who or what captured it — the same reasoning the " +
                "allowlist applies to an isolation assertion: an unattributed record is not a record.");

        if (regions is null || regions.Count == 0)
            throw new S7ConfigurationException("a restore point covering no regions restores nothing.");

        foreach (var r in regions)
        {
            if (r.Size <= 0)
                throw new S7ConfigurationException($"restore region {r.Describe()} has no size.");
            if (r.DbNumber <= 0 || r.StartByte < 0)
                throw new S7ConfigurationException($"restore region {r.Describe()} is not addressable.");
            if (string.IsNullOrWhiteSpace(r.Area))
                throw new S7ConfigurationException(
                    $"restore region DB{r.DbNumber}.DBB{r.StartByte} names no area, so coverage of a " +
                    "run's declared areas could never be checked against it.");
        }

        // Refuse up front if the capture would not cover what the run intends to write. Discovering
        // that at the fence, after a slow capture, tells you the same thing far less usefully.
        var uncovered = _requiredAreas
            .Where(a => !regions.Any(r => string.Equals(r.Area?.Trim(), a?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (uncovered.Count > 0)
            throw new S7ConfigurationException(
                $"the regions given do not cover {string.Join(", ", uncovered.Select(u => $"'{u}'"))}, " +
                "which this run declared it will write. Capturing a restore point that cannot undo the " +
                "write it is about to authorize is worse than having none, because the fence would pass.");

        var captured = regions.Select(r =>
        {
            var bytes = device.Read(r);
            if (bytes.Length != r.Size)
                throw new S7TransportException(
                    $"reading {r.Describe()} returned {bytes.Length} byte(s); a short read must not " +
                    "become a short restore point.");

            return new CapturedRegion(r.Area, r.DbNumber, r.StartByte, r.Size,
                Convert.ToBase64String(bytes), Hash(bytes));
        }).ToList();

        Directory.CreateDirectory(_directory);
        var path = PathFor(normalizedTarget);

        var unverified = new RestorePointManifest(
            RestorePointManifest.CurrentFormatVersion,
            normalizedTarget.Trim(),
            _clock().UtcDateTime.ToString("o"),
            capturedBy.Trim(),
            // Never anything else. See the class comment: this store cannot capture the other kind.
            RestorePointScope.ProcessDataOnly.ToString(),
            Verified: false,
            VerificationNote: "written, not yet read back",
            captured);

        File.WriteAllText(path, JsonSerializer.Serialize(unverified, WriteOptions), Encoding.UTF8);

        // Read back off the disk, not out of memory. The point is to catch the file being truncated,
        // unwritable, or intercepted by something else — none of which the in-memory object knows.
        var readBack = JsonSerializer.Deserialize<RestorePointManifest>(File.ReadAllText(path), ReadOptions)
            ?? throw new S7TransportException(
                $"the restore point at '{path}' could not be read back after writing, so the capture " +
                "is not verified and the write must not proceed.");

        ConfirmRegionsMatch(path, captured, readBack.Regions);

        var verified = readBack with
        {
            Verified = true,
            VerificationNote = $"read back from disk and hash-compared at {_clock().UtcDateTime:o}",
        };

        File.WriteAllText(path, JsonSerializer.Serialize(verified, WriteOptions), Encoding.UTF8);

        var final = JsonSerializer.Deserialize<RestorePointManifest>(File.ReadAllText(path), ReadOptions);
        if (final is null || !final.Verified)
            throw new S7TransportException(
                $"the restore point at '{path}' did not persist its verified state; treating the " +
                "capture as failed.");

        ConfirmRegionsMatch(path, captured, final.Regions);
        return final;
    }

    private static void ConfirmRegionsMatch(
        string path, IReadOnlyList<CapturedRegion> expected, IReadOnlyList<CapturedRegion> actual)
    {
        if (expected.Count != actual.Count)
            throw new S7TransportException(
                $"the restore point at '{path}' read back with {actual.Count} region(s), not {expected.Count}.");

        for (var i = 0; i < expected.Count; i++)
        {
            if (expected[i].Sha256 != actual[i].Sha256 || expected[i].Base64 != actual[i].Base64)
                throw new S7TransportException(
                    $"the restore point at '{path}' read back with region {i} altered; the capture is " +
                    "not verified.");
        }
    }

    // ---------------------------------------------------------------- restore

    /// <summary>
    /// Put a restore point back, in one operation, and CONFIRM it by re-reading the device.
    ///
    /// <para>`10-non-goals.md` #4(b): <i>"Restore is a single operation, and its success is confirmed
    /// by re-reading the target, never assumed."</i> Both halves are load-bearing — the confirmation
    /// is what distinguishes this from a write that reported no error.</para>
    /// </summary>
    public RestoreResult Restore(string normalizedTarget, IRegionAccess device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var path = PathFor(normalizedTarget);
        if (!File.Exists(path))
            return RestoreResult.Fail($"no restore point at '{path}'.", Array.Empty<string>());

        var manifest = JsonSerializer.Deserialize<RestorePointManifest>(File.ReadAllText(path), ReadOptions);
        if (manifest is null || !manifest.Verified)
            return RestoreResult.Fail(
                $"the restore point at '{path}' is missing or unverified; refusing to write unverified " +
                "content back to a device.", Array.Empty<string>());

        var problems = new List<string>();

        foreach (var r in manifest.Regions)
        {
            var region = new RestoreRegion(r.Area, r.DbNumber, r.StartByte, r.Size);
            var bytes = Convert.FromBase64String(r.Base64);

            try
            {
                device.Write(region, bytes);
            }
            catch (Exception ex)
            {
                problems.Add($"{region.Describe()}: write failed — {ex.Message}");
                continue;
            }

            byte[] readBack;
            try
            {
                readBack = device.Read(region);
            }
            catch (Exception ex)
            {
                problems.Add($"{region.Describe()}: restored but could not be re-read — {ex.Message}");
                continue;
            }

            if (!readBack.AsSpan().SequenceEqual(bytes))
            {
                // Worth stating why this can happen without anything being broken: if the program
                // itself writes these bytes every scan, the restored value is overwritten before it
                // can be read back. That is not a restore failure so much as a sign the region was
                // the wrong thing to snapshot — either way, it is not a confirmed restore.
                problems.Add(
                    $"{region.Describe()}: re-read does not match what was restored. Either the write " +
                    "did not take, or the program overwrites this region every scan and it was never " +
                    "restorable in the first place.");
            }
        }

        return problems.Count == 0
            ? RestoreResult.Ok($"restored {manifest.Regions.Count} region(s) to '{normalizedTarget}' and " +
                               "confirmed every one by re-reading it.")
            : RestoreResult.Fail(
                $"restore of '{normalizedTarget}' is NOT confirmed: {problems.Count} of " +
                $"{manifest.Regions.Count} region(s) could not be verified.", problems);
    }

    // ---------------------------------------------------------------- helpers

    private bool No(string reason)
    {
        LastRefusal = reason;
        return false;
    }

    private static bool Satisfies(RestorePointScope have, RestorePointScope need) =>
        have == need || have == RestorePointScope.ProgramAndData;

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// A file name derived from the target, plus a short hash of the original.
    ///
    /// The hash is not decoration: an IPv6 address and a hostname can flatten to the same string once
    /// the punctuation is replaced, and two devices sharing one restore point file is the single worst
    /// bug this class could have.
    /// </summary>
    private static string SafeFileName(string target)
    {
        var cleaned = new string(target.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.Trim())))[..8].ToLowerInvariant();
        return $"{cleaned}-{digest}";
    }
}
