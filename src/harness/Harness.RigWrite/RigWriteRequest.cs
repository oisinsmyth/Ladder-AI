using Harness.S7;

namespace Harness.RigWrite;

/// <summary>
/// One governed write, stated completely enough to be planned, refused, captured and undone — all
/// before anybody decides whether it may happen.
///
/// <para><b>The payload is bytes, and the extent is the payload's length.</b> There is no separate
/// "size" field to disagree with it, and no partial write: a region is written whole. That is what
/// makes the verification step a byte-for-byte comparison rather than an interpretation, and it is
/// what makes the restore point the exact inverse of the write — the same region, the bytes that were
/// there before.</para>
/// </summary>
/// <param name="Target">Address used to reach the device. A routing hint, never an authorization.</param>
/// <param name="Area">
/// The name the write fence is scoped on — in practice the DB's symbolic name. It must be the same
/// vocabulary the run's <see cref="DeviceGuard.WriteScope"/> and the restore point use, or the fence
/// authorizes one thing while the bytes land in another.
/// </param>
/// <param name="Purpose">What this run is for, recorded in the scope and in the refusal messages.</param>
/// <param name="What">A human sentence describing the write, for the plan a person reads.</param>
public sealed record RigWriteRequest(
    string Target,
    string Area,
    int DbNumber,
    int ByteOffset,
    byte[] Payload,
    string Purpose,
    string What)
{
    public int Size => Payload.Length;

    /// <summary>The region a restore point must hold to make this write reversible.</summary>
    public RestoreRegion Region => new(Area, DbNumber, ByteOffset, Size);

    public string Describe() => $"DB{DbNumber}.DBB{ByteOffset}, {Size} byte(s) — {What}";

    /// <summary>
    /// The proposed first governed write: a token into the marker DB's RESERVED
    /// <c>SerialNumber</c> member.
    ///
    /// <para><b>Why this target.</b> The member is declared, allocated, initialised empty, marked
    /// <c>NonRetain</c>, and read by no logic and written by no logic — so changing it changes no
    /// behaviour, and nothing overwrites it between the write and the read-back. That second property
    /// is not incidental: <see cref="FileRestorePointStore.Restore"/> records the case where a region
    /// the program writes every scan cannot be verified OR restored, and a reserved member is the one
    /// place that cannot happen. It also sits in the only block on this device known to have
    /// standard (non-optimized) access, which is the precondition classic S7comm needs to see a block
    /// at all.</para>
    ///
    /// <para><b>The value is written to be removed.</b> The intended sequence ends by restoring the
    /// member to the empty string it started as and confirming that by re-reading — so the first
    /// governed write is also the first governed restore, and the device is left byte-identical to how
    /// it was found.</para>
    /// </summary>
    public static RigWriteRequest MarkerSerialProbe(
        string target,
        string text,
        int dbNumber = MarkerDbLayout.DbNumber,
        int byteOffset = MarkerDbLayout.SerialNumberOffset,
        int declaredMax = MarkerDbLayout.StringDeclaredMax,
        string area = MarkerDbLayout.AreaName) =>
        new(target,
            area,
            dbNumber,
            byteOffset,
            S7StringCodec.Encode(text, declaredMax),
            Purpose: "first governed device write — marker DB reserved-field round trip",
            What: $"set {area}.SerialNumber (String[{declaredMax}], reserved) to '{text}'");
}
