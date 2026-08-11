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
}

/// <summary>The outcome of putting a restore point back. Never assumed — always re-read.</summary>
public sealed record RestoreResult(bool Restored, string Message, IReadOnlyList<string> Problems)
{
    public static RestoreResult Ok(string message) => new(true, message, Array.Empty<string>());
    public static RestoreResult Fail(string message, IReadOnlyList<string> problems) => new(false, message, problems);
}
