namespace Harness.S7;

/// <summary>One contiguous stretch of device memory that a restore point covers.</summary>
/// <param name="Area">
/// The named area, matching the vocabulary the write fence is scoped on (<see cref="S7Tag.Area"/>).
/// This is what lets the store answer "does this restore point cover what the run is about to write?"
/// rather than merely "does a restore point exist?".
/// </param>
public sealed record RestoreRegion(string Area, int DbNumber, int StartByte, int Size)
{
    public string Describe() => $"'{Area}' (DB{DbNumber}.DBB{StartByte}, {Size} byte(s))";
}

/// <summary>
/// What a restore point covers — and therefore what class of write it can authorize.
///
/// <para>`10-non-goals.md` #4(b) records the hard case: <i>a download can silently reinitialise DB
/// actual values and retentive data, so a restore point that captures only blocks is not a restore
/// point.</i> The inverse is equally true and is the case that applies here — a copy of some DB bytes
/// does not let you undo a download. Making the scope an explicit part of the artifact is what stops
/// a data-only snapshot from quietly standing in for the other kind.</para>
/// </summary>
public enum RestorePointScope
{
    /// <summary>DB byte ranges only. Enough to undo process-data writes; NOT enough to undo a
    /// download, an online edit or a hardware-configuration change.</summary>
    ProcessDataOnly,

    /// <summary>Program blocks AND the data that a download would reinitialise. Nothing in this
    /// repository produces one yet — see <see cref="FileRestorePointStore"/>.</summary>
    ProgramAndData,
}

/// <summary>
/// Reading and writing raw regions, so the store can capture and restore without knowing about
/// Sharp7, tags or the transport. <see cref="S7RegionAccess"/> is the real implementation; tests
/// supply their own.
/// </summary>
public interface IRegionAccess
{
    byte[] Read(RestoreRegion region);
    void Write(RestoreRegion region, byte[] data);
}

/// <summary>Raw region access over an <see cref="IS7Client"/>.</summary>
/// <remarks>
/// Note what this deliberately bypasses: the write fence. That is correct here and nowhere else — a
/// restore writes back exactly the bytes that were already on the device, so scoping it to the run's
/// declared areas would make a restore fail precisely when it is most needed (a run that wrote
/// outside its scope could never be undone). The protection that matters for restore is that the
/// bytes came from this same device, which the manifest's target key enforces.
/// </remarks>
public sealed class S7RegionAccess : IRegionAccess
{
    private readonly IS7Client _client;

    public S7RegionAccess(IS7Client client) => _client = client;

    public byte[] Read(RestoreRegion region)
    {
        var buffer = new byte[region.Size];
        var status = _client.ReadDataBlock(region.DbNumber, region.StartByte, buffer);
        if (!status.Ok)
            throw new S7TransportException($"could not read {region.Describe()} for a restore point: {status}.");

        return buffer;
    }

    public void Write(RestoreRegion region, byte[] data)
    {
        if (data.Length != region.Size)
            throw new S7TransportException(
                $"restore payload for {region.Describe()} is {data.Length} byte(s), not {region.Size}.");

        var status = _client.WriteDataBlock(region.DbNumber, region.StartByte, data);
        if (!status.Ok)
            throw new S7TransportException($"could not restore {region.Describe()}: {status}.");
    }
}

/// <summary>One region as stored in a manifest.</summary>
public sealed record CapturedRegion(
    string Area,
    int DbNumber,
    int StartByte,
    int Size,
    string Base64,
    string Sha256);

/// <summary>
/// The on-disk restore point. Plain JSON on purpose: it has to be inspectable and restorable by a
/// person on a day when none of this tooling runs.
/// </summary>
public sealed record RestorePointManifest(
    int FormatVersion,
    string Target,
    string CapturedUtc,
    string CapturedBy,
    string Scope,
    bool Verified,
    string VerificationNote,
    IReadOnlyList<CapturedRegion> Regions)
{
    public const int CurrentFormatVersion = 1;

    public IEnumerable<string> Areas =>
        Regions.Select(r => r.Area).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Does this restore point hold the exact bytes a write is about to change?
    ///
    /// <para><b>Why the area check is not enough.</b> <see cref="FileRestorePointStore"/> answers the
    /// fence's question — "is there a verified restore point?" — by comparing AREA NAMES, because an
    /// area is the vocabulary the fence is scoped on and the store cannot know what a run will write
    /// within one. That leaves a real hole: a two-byte capture labelled <c>DB_RigMarker</c> satisfies
    /// the fence for a thirty-four-byte write into the same area, and the restore would then put back
    /// two of the thirty-four bytes and report success. This is the byte-level question the fence
    /// cannot ask, offered to callers that know their own write extent.</para>
    ///
    /// <para><b>Regions are not stitched.</b> One region must cover the whole range on its own. Two
    /// abutting captures might genuinely be contiguous, but proving that is exactly where an
    /// off-by-one puts an unrecorded gap in the middle of a "covered" range, so a range spanning two
    /// regions is reported as not covered.</para>
    /// </summary>
    public bool Covers(int dbNumber, int startByte, int size) =>
        size > 0
        && Regions.Any(r => r.DbNumber == dbNumber
                            && r.StartByte <= startByte
                            && r.StartByte + r.Size >= startByte + size);
}

/// <summary>The outcome of putting a restore point back. Never assumed — always re-read.</summary>
public sealed record RestoreResult(bool Restored, string Message, IReadOnlyList<string> Problems)
{
    public static RestoreResult Ok(string message) => new(true, message, Array.Empty<string>());
    public static RestoreResult Fail(string message, IReadOnlyList<string> problems) => new(false, message, problems);
}
