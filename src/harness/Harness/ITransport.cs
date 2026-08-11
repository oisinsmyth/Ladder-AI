namespace Harness;

/// <summary>
/// What a transport can actually observe. Declared, not assumed — the runner uses this to refuse
/// vectors it cannot honestly evaluate.
///
/// These are properties of the PROGRAM UNDER TEST as much as of the wire. A scan counter,
/// event scan-stamps and latched transients are all instrumentation that has to exist in the PLC
/// program; no transport can synthesise them.
/// </summary>
[Flags]
public enum TransportCapabilities
{
    None = 0,

    /// <summary>
    /// The program exposes a free-running scan counter, so the runner can OBSERVE scan boundaries.
    /// Required by any step that waits a number of scans. Without it, "after N scans" is a wall-clock
    /// guess, which on a non-real-time host is not a measurement.
    /// </summary>
    ScanCounter = 1 << 0,

    /// <summary>
    /// The program records the scan number at which events of interest occurred, so a same-scan
    /// coincidence can be asserted as equality of two persistent integers. Required for
    /// <see cref="Observability.Coincidence"/>.
    /// </summary>
    EventScanStamps = 1 << 1,

    /// <summary>
    /// The program latches momentary conditions so a one-scan pulse survives to be read. Required for
    /// <see cref="Observability.Transient"/>.
    /// </summary>
    LatchedTransients = 1 << 2,

    /// <summary>The transport may write. A read-only transport refuses stimulus by construction.</summary>
    Write = 1 << 3,
}

/// <summary>
/// The harness's only view of a device. Symbolic reads and writes, plus the scan counter.
///
/// Deliberately minimal, and deliberately WITHOUT any notion of stepping the program: an S7-1200
/// supports no breakpoint or single-step debugging, and the one deterministic stepper that exists
/// for it (PLCSIM's Scan Control) is reachable only through a GUI. Pretending otherwise in the
/// interface would invite vectors that cannot run.
///
/// Implementations own the fence. A transport that writes to a real device must consult the write
/// guard before every write; the runner does not do that for it, and does not know the guard exists.
/// </summary>
public interface ITransport
{
    TransportCapabilities Capabilities { get; }

    /// <summary>Current value of the program's free-running scan counter.</summary>
    /// <exception cref="NotSupportedException">If <see cref="TransportCapabilities.ScanCounter"/> is absent.</exception>
    long ReadScanCounter();

    /// <summary>Read one symbolic value. Returns its string form; comparison is the runner's job.</summary>
    string Read(string tag);

    /// <summary>Write one symbolic value into a named area.</summary>
    /// <exception cref="NotSupportedException">If <see cref="TransportCapabilities.Write"/> is absent.</exception>
    void Write(string area, string tag, string value);
}

/// <summary>
/// An in-memory transport for authoring and testing vectors with no device present.
///
/// This is not only a test double. Most of what goes wrong in a vector — a typo'd tag, an
/// unreachable step, an expectation that contradicts an earlier one, a coincidence assertion nobody
/// can honour — is found here, in milliseconds, before a rig is involved at all.
/// </summary>
public sealed class FakeTransport : ITransport
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private long _scan;

    public FakeTransport(TransportCapabilities capabilities =
        TransportCapabilities.ScanCounter | TransportCapabilities.Write)
        => Capabilities = capabilities;

    public TransportCapabilities Capabilities { get; }

    /// <summary>Every write the runner issued, in order — so a test can assert on stimulus itself.</summary>
    public List<TagWrite> Writes { get; } = new();

    /// <summary>
    /// Scans to advance automatically on each scan-counter read. 1 by default so waits terminate.
    /// Set to 0 to model a stalled PLC and exercise the wait timeout.
    /// </summary>
    public long AutoAdvancePerRead { get; set; } = 1;

    public void Set(string tag, string value) => _values[tag] = value;

    public void AdvanceScans(long n) => _scan += n;

    public long ReadScanCounter()
    {
        if (!Capabilities.HasFlag(TransportCapabilities.ScanCounter))
            throw new NotSupportedException("this transport exposes no scan counter.");

        var now = _scan;
        _scan += AutoAdvancePerRead;
        return now;
    }

    public string Read(string tag) =>
        _values.TryGetValue(tag, out var v) ? v : string.Empty;

    public void Write(string area, string tag, string value)
    {
        if (!Capabilities.HasFlag(TransportCapabilities.Write))
            throw new NotSupportedException("this transport is read-only.");

        Writes.Add(new TagWrite(area, tag, value));
        _values[tag] = value;
    }
}
