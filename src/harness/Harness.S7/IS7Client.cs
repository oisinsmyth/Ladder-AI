namespace Harness.S7;

/// <summary>
/// The outcome of one S7 operation.
///
/// Sharp7 reports errors the C way — an <c>int</c> return code, with <c>ErrorText(rc)</c> turning it
/// into prose. That shape is kept here rather than translated into exceptions at the adapter, for one
/// reason: the adapter must stay thin enough to be read and believed without a device, because it is
/// the ONE piece of this project that cannot be unit-tested. Every decision worth testing therefore
/// lives above it, and the adapter does nothing but marshal. The text is resolved at the adapter (it
/// is the only thing that knows Sharp7's error table) so callers never need Sharp7 to explain a
/// failure.
/// </summary>
public readonly record struct S7Status(int Code, string Text)
{
    public bool Ok => Code == 0;

    public static S7Status Success { get; } = new(0, "ok");

    /// <summary>For fakes and for failures raised above the wire (code is non-zero and unmapped).</summary>
    public static S7Status Failure(string text, int code = -1) => new(code, text);

    public override string ToString() => Ok ? "ok" : $"rc={Code}: {Text}";
}

/// <summary>What a CPU says about itself in SZL 0x001C. Fields are whatever the CPU filled in.</summary>
/// <remarks>
/// Read via Sharp7's <c>GetCpuInfo</c>. This is NOT the same read as the order code, and it is NOT
/// known to work on the target rig — see <see cref="CpuInfoIdentitySource"/>, which for that reason
/// is deliberately not wired in by default.
/// </remarks>
public sealed record S7CpuInfo(
    string ModuleTypeName = "",
    string SerialNumber = "",
    string ModuleName = "");

/// <summary>
/// Everything the transport is allowed to do to a device — and, just as importantly, everything it is
/// not.
///
/// <para><b>Why an interface at all.</b> So the transport, the fence wiring, the tag decoding and
/// every refusal path are testable with no PLC in the room. Sharp7's <c>S7Client</c> owns a socket and
/// cannot be substituted.</para>
///
/// <para><b>Why it is this small.</b> Sharp7's real surface includes <c>PlcStop()</c>,
/// <c>Download()</c>, <c>Delete()</c>, <c>PlcCopyRamToRom()</c> and <c>SetPlcDateTime()</c>. None of
/// them appear here. The abstraction is a REDUCTION as much as an indirection: a capability that is
/// not on this interface cannot be reached from the harness at all, whatever a caller intends, and
/// that is a stronger guarantee than a policy note. ADR-0009 permits program writes on a qualifying
/// rig; it does not require the conformance harness to be the thing that performs them, and a test
/// runner that can stop a CPU is a worse tool than one that cannot.</para>
///
/// <para><b>Symbolic access is absent on purpose.</b> Classic S7comm has none: it addresses
/// DB number + byte offset + length, and nothing else. The symbolic layer is <see cref="S7TagMap"/>,
/// above this line, where it can be tested.</para>
/// </summary>
public interface IS7Client
{
    /// <summary>Whether the client currently believes it holds a session.</summary>
    bool Connected { get; }

    S7Status Connect(string address, int rack, int slot, int connectTimeoutMs);

    /// <summary>Idempotent — disconnecting an already-disconnected client is not an error.</summary>
    void Disconnect();

    /// <summary>The CPU's article/order number, e.g. <c>6ES7 214-1AG40-0XB0</c>.</summary>
    S7Status ReadOrderCode(out string orderCode);

    /// <summary>SZL 0x001C. May be unsupported or partially filled depending on CPU and firmware.</summary>
    S7Status ReadCpuInfo(out S7CpuInfo info);

    /// <summary>
    /// Whether the CPU is running. READ-ONLY: it asks for a status and cannot change a mode.
    ///
    /// <para>This is the one member that reads the CPU rather than the program, and it is here because
    /// no other source has the fact — over Modbus a stopped CPU and a dropped link are the same
    /// silence, and Openness exposes no operating mode. It does NOT widen the interface towards mode
    /// CONTROL: Sharp7's <c>PlcStop</c>, <c>PlcHotStart</c> and <c>PlcColdStart</c> stay off this
    /// interface and therefore stay unreachable from the harness.</para>
    ///
    /// <para>The status and the reading answer two different questions and both are needed: the status
    /// says whether the CPU was asked, the reading says what it said. See
    /// <see cref="S7RunStateReading"/>.</para>
    /// </summary>
    S7Status ReadRunState(out S7RunStateReading runState);

    /// <summary>Read <c>buffer.Length</c> bytes from a data block. The size comes from the buffer so
    /// the two can never disagree — a size/buffer mismatch is a classic Sharp7 caller bug.</summary>
    S7Status ReadDataBlock(int dbNumber, int startByte, byte[] buffer);

    /// <summary>Write <c>buffer.Length</c> bytes into a data block.</summary>
    S7Status WriteDataBlock(int dbNumber, int startByte, byte[] buffer);

    /// <summary>
    /// Write ONE bit, at bit granularity on the wire.
    ///
    /// This exists separately because the obvious implementation — read the byte, flip the bit, write
    /// the byte back — is a read-modify-write against a program that is running. Any other bit in
    /// that byte that the PLC changed in between is silently reverted, and the failure is intermittent
    /// and timing-dependent, which is the worst kind to meet inside a conformance suite. S7 supports a
    /// bit-length write natively (<c>WriteArea(..., S7WordLength.Bit, ...)</c>), so the transport uses
    /// that and never does read-modify-write.
    /// </summary>
    S7Status WriteBit(int dbNumber, int byteOffset, int bitOffset, bool value);
}

/// <summary>
/// Where and how to reach the device, plus the one fact about the PROGRAM that the transport cannot
/// discover for itself.
/// </summary>
/// <param name="Address">
/// Routing hint only, never authorization. Which physical controller answers at an address depends on
/// which VPN tunnel is up — see <see cref="DeviceGuard.DeviceIdentity"/> for the observed incident.
/// </param>
/// <param name="Rack">0 for an S7-1200.</param>
/// <param name="Slot">1 for an S7-1200.</param>
/// <param name="ScanCounterTag">
/// Name of a tag in the <see cref="S7TagMap"/> holding the program's free-running scan counter, or
/// null if the program has none. This is what makes
/// <see cref="Harness.TransportCapabilities.ScanCounter"/> appear — the capability is DERIVED from a
/// tag that exists, never asserted. As of 2026-08-11 no program under test provides one, so this is
/// null and the flag is absent.
/// </param>
public sealed record S7ConnectionSettings(
    string Address,
    int Rack = 0,
    int Slot = 1,
    int ConnectTimeoutMs = 5_000,
    string? ScanCounterTag = null);
