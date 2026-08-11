using DeviceGuard;

namespace Harness.S7;

/// <summary>
/// The composition root: an <see cref="ITransport"/> over classic S7comm, with the write fence wired
/// in behind it.
///
/// <para><b>Why the fence lives here and nowhere else.</b> <c>Harness</c> holds no reference to
/// <c>DeviceGuard</c> and <c>DeviceGuard</c> holds no reference to <c>Harness</c> — deliberately, so
/// neither drags the other's dependencies and both stay testable alone. That leaves exactly one place
/// where the two must meet, and this is it. <see cref="ITransport"/> says it in its own comment:
/// <i>"Implementations own the fence. A transport that writes to a real device must consult the write
/// guard before every write; the runner does not do that for it, and does not know the guard
/// exists."</i></para>
///
/// <para><b>Identity is re-read on every connection, not once at startup.</b> Not defensiveness — an
/// observed incident (2026-08-11): a remote tunnel injected 10.10.10.0/24 at metric 0 and shadowed a
/// local segment carrying the same range, so the box answering an address changed with no signal
/// anywhere in the routing table. A transport that identifies its device once and then silently
/// reconnects has verified nothing. Every reconnect here re-runs the identity sources, and the
/// identity handed to the guard is always the one read on the current session.</para>
///
/// <para><b>Capabilities are derived, never declared.</b> See <see cref="Capabilities"/>.</para>
/// </summary>
public sealed class S7Transport : ITransport, IDisposable
{
    private readonly IS7Client _client;
    private readonly S7ConnectionSettings _settings;
    private readonly S7TagMap _tags;
    private readonly DeviceAccessGuard _readGuard;
    private readonly DeviceWriteGuard? _writeGuard;
    private readonly WriteScope _scope;
    private readonly IReadOnlyList<IDeviceIdentitySource> _identitySources;

    private DeviceIdentity? _identity;

    // Scan-counter wrap tracking — see ReadScanCounter.
    private long _lastRawScan = -1;
    private long _scanWraps;

    private S7Transport(
        IS7Client client,
        S7ConnectionSettings settings,
        S7TagMap tags,
        AllowlistFile.Result allowlist,
        IEnumerable<IDeviceIdentitySource> identitySources,
        DeviceWriteGuard? writeGuard,
        WriteScope? scope)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        _readGuard = new DeviceAccessGuard(allowlist);
        _writeGuard = writeGuard;
        _scope = scope ?? WriteScope.Nothing;
        _identitySources = (identitySources ?? Array.Empty<IDeviceIdentitySource>()).ToArray();

        if (_identitySources.Count == 0)
            throw new S7ConfigurationException(
                "no identity sources configured. Identity is read on every connection even for a " +
                "read-only transport: reading the right values off the wrong device is a wrong " +
                "measurement, and it costs one request to rule out.");

        if (_writeGuard is not null && _scope.IsEmpty)
            throw new S7ConfigurationException(
                "a writable transport was constructed with no declared write scope, so every write " +
                "would be refused at the fence. Declare the areas this run will write — normally " +
                "VectorSet.DeclaredAreas(vectors) — or build a read-only transport.");

        if (_settings.ScanCounterTag is not null && !_tags.TryResolve(_settings.ScanCounterTag, out _))
            throw new S7ConfigurationException(
                $"the scan counter tag '{_settings.ScanCounterTag}' is not in the tag map. The " +
                "ScanCounter capability is derived from that tag existing, so a missing one would " +
                "advertise an observation the transport cannot make.");

        Capabilities = DeriveCapabilities();
    }

    /// <summary>
    /// A transport that can read and nothing else. Writes throw
    /// <see cref="NotSupportedException"/> and the <see cref="TransportCapabilities.Write"/> flag is
    /// absent, so <see cref="VectorRunner"/> reports any stimulus vector as
    /// <see cref="VectorOutcome.NotObservable"/> — loudly, and not as a pass.
    ///
    /// <para>This is today's real mode. The rig's allowlist entry is <c>writeEligible: false</c> by
    /// design as of 2026-08-11, so a writable transport against it would be constructible and then
    /// refuse every single write. Read-only says the same thing earlier and more honestly.</para>
    /// </summary>
    public static S7Transport ReadOnly(
        IS7Client client,
        S7ConnectionSettings settings,
        S7TagMap tags,
        AllowlistFile.Result allowlist,
        IEnumerable<IDeviceIdentitySource> identitySources) =>
        new(client, settings, tags, allowlist, identitySources, writeGuard: null, scope: null);

    /// <summary>
    /// A transport that may write, subject to the seven gates of <see cref="DeviceWriteGuard"/>.
    ///
    /// <para>Both guards are built here from ONE <see cref="AllowlistFile.Result"/>. That is the whole
    /// reason the factory takes the allowlist rather than two ready-made guards: a read fence and a
    /// write fence configured from different files, or from the same file read at different moments,
    /// is a defect with no symptom until the day it matters.</para>
    /// </summary>
    /// <param name="restorePoints">
    /// Gate 7. Passing <see cref="NoRestorePoints.Instance"/> is legal and refuses every write, which
    /// is the correct behaviour when nothing has been captured — see <see cref="FileRestorePointStore"/>
    /// for a store that can answer yes.
    /// </param>
    /// <param name="scope">
    /// What this run declared it will write. Build it from the vectors themselves
    /// (<c>WriteScope.For(purpose, VectorSet.DeclaredAreas(vectors).ToArray())</c>) rather than by
    /// hand: a hand-maintained scope drifts wide, and the vectors already know the answer.
    /// </param>
    public static S7Transport Writable(
        IS7Client client,
        S7ConnectionSettings settings,
        S7TagMap tags,
        AllowlistFile.Result allowlist,
        IRestorePointStore restorePoints,
        WriteScope scope,
        IEnumerable<IDeviceIdentitySource> identitySources) =>
        new(client, settings, tags, allowlist,
            identitySources,
            new DeviceWriteGuard(allowlist, restorePoints),
            scope);

    // ---------------------------------------------------------------- capabilities

    /// <summary>
    /// What this transport can honestly observe.
    ///
    /// <para>Every flag here is DERIVED from something that exists, never asserted:
    /// <see cref="TransportCapabilities.Write"/> from having been built writable, and
    /// <see cref="TransportCapabilities.ScanCounter"/> from a scan-counter tag being present in the
    /// map. As of 2026-08-11 no program under test provides one, so the flag is absent.</para>
    ///
    /// <para><see cref="TransportCapabilities.EventScanStamps"/> and
    /// <see cref="TransportCapabilities.LatchedTransients"/> are absent and cannot currently be turned
    /// on at all. They are properties of the PROGRAM, not of the wire — the program must record the
    /// scan number at which events occur, and must latch one-scan pulses — and no program under test
    /// does either yet. There is deliberately no constructor argument to claim them, because the only
    /// way to claim them today would be to hope. A false flag here does not produce a wrong answer; it
    /// produces a green one, by converting a vector the runner would have refused to evaluate into a
    /// vector it evaluates against data that cannot carry the answer.</para>
    /// </summary>
    public TransportCapabilities Capabilities { get; }

    private TransportCapabilities DeriveCapabilities()
    {
        var caps = TransportCapabilities.None;
        if (_writeGuard is not null) caps |= TransportCapabilities.Write;
        if (_settings.ScanCounterTag is not null) caps |= TransportCapabilities.ScanCounter;
        return caps;
    }

    // ---------------------------------------------------------------- connection & identity

    /// <summary>The identity read on the CURRENT session, or null if not connected.</summary>
    public DeviceIdentity? ObservedIdentity => _identity;

    /// <summary>
    /// How many times identity has been read. One per connection, so a test — or a person reading a
    /// run log — can tell a silent reconnect happened.
    /// </summary>
    public int IdentityReadCount { get; private set; }

    /// <summary>
    /// Open the session: read fence, connect, read identity, and (when writable) pre-check every
    /// declared area against the write fence.
    ///
    /// <para>The preflight is the same pure function that runs before each individual write, called
    /// early so that a run which cannot possibly write fails at connect rather than three vectors in,
    /// after it has already stimulated the plant with the writes it was allowed.</para>
    /// </summary>
    public void Connect()
    {
        var read = _readGuard.Check(_settings.Address);
        if (!read.Allowed) throw new S7AccessRefusedException(read);

        var status = _client.Connect(_settings.Address, _settings.Rack, _settings.Slot, _settings.ConnectTimeoutMs);
        if (!status.Ok)
            throw new S7TransportException(
                $"could not connect to {_settings.Address} (rack {_settings.Rack}, slot {_settings.Slot}): " +
                $"{status}.");

        try
        {
            _identity = DeviceIdentityReader.Read(_client, _identitySources);
            IdentityReadCount++;
        }
        catch
        {
            // A session we cannot identify is a session we must not use. Dropping it here means a
            // later operation cannot quietly reuse it.
            _client.Disconnect();
            _identity = null;
            throw;
        }

        if (_writeGuard is not null)
        {
            PreflightDeclaredScope();
        }
        else
        {
            VerifyIdentityForReadOnlySession(read.MatchedEntry);
        }
    }

    /// <summary>
    /// Identity checking on a read-only session.
    ///
    /// <para>The read fence authorizes on address alone (ADR-0008), so a read-listed entry need not
    /// declare an identity, and one that declares none is still allowed to read. But if it DOES
    /// declare one, honour it: a measurement taken from the wrong device is a wrong measurement, and
    /// the read has already been paid for.</para>
    ///
    /// <para>This runs ONLY when there is no write guard, so that exactly one component owns the
    /// identity verdict in each mode. On a writable transport the fence owns it —
    /// <see cref="PreflightDeclaredScope"/> asks the same question through
    /// <see cref="DeviceWriteGuard"/>, and every refusal on the write path then carries a
    /// <see cref="WriteRefusal"/> a caller can branch on, rather than two differently-worded errors
    /// for the same condition depending on which check happened to fire first.</para>
    /// </summary>
    private void VerifyIdentityForReadOnlySession(AllowlistEntry? entry)
    {
        if (entry is null) return;

        var comparison = DeviceIdentity.Compare(entry, _identity);
        if (comparison.WasDeclared && !comparison.Matched)
        {
            var observed = _identity;
            _client.Disconnect();
            _identity = null;

            throw new S7TransportException(
                $"the device answering {_settings.Address} is not the one the allowlist declares: " +
                $"{comparison.Problem}. Observed {observed?.Describe()}.");
        }
    }

    private void PreflightDeclaredScope()
    {
        foreach (var area in _scope.Areas)
        {
            var decision = _writeGuard!.Check(_settings.Address, area, _identity, _scope);
            if (!decision.Allowed)
            {
                _client.Disconnect();
                _identity = null;
                throw new S7WriteRefusedException(decision);
            }
        }
    }

    /// <summary>
    /// Connect if the session has dropped — and re-read identity when it does.
    ///
    /// <para>This is the whole point of the reconnect handling. A transport that transparently
    /// reconnects and carries on with the identity it read an hour ago has converted the one check
    /// that catches a swapped device into decoration.</para>
    /// </summary>
    private void EnsureConnected()
    {
        if (_client.Connected && _identity is not null) return;
        Connect();
    }

    public void Disconnect()
    {
        _client.Disconnect();
        _identity = null;
    }

    public void Dispose() => Disconnect();

    // ---------------------------------------------------------------- ITransport

    /// <summary>
    /// The program's free-running scan counter.
    ///
    /// <para>Throws when no scan-counter tag is configured, which is the state as of 2026-08-11. The
    /// runner will not call it in that state — the capability is absent, so it reports any
    /// scan-waiting vector as unobservable first — but the throw matters anyway: it is what makes the
    /// two statements consistent if anyone calls this directly.</para>
    ///
    /// <para><b>Wrap handling.</b> A free-running counter in a UDInt wraps at 2^32. The runner
    /// computes <c>now - start</c> and waits for it to reach N, so a wrap mid-wait yields a large
    /// negative number and the wait runs to its poll limit and errors. Not a false pass, but a
    /// baffling one, so the wrap is absorbed here: raw counter going backwards means one wrap, and the
    /// value this returns is monotonic for the life of the transport.</para>
    /// </summary>
    public long ReadScanCounter()
    {
        if (_settings.ScanCounterTag is null)
            throw new NotSupportedException(
                "this transport exposes no scan counter: the program under test does not provide one. " +
                "A wall-clock delay is not a scan count on a non-real-time host, so there is nothing " +
                "honest to return here. Add a free-running counter to the program, map it as a tag, " +
                "and name it in S7ConnectionSettings.ScanCounterTag.");

        var tag = _tags.Resolve(_settings.ScanCounterTag);
        var raw = S7Values.DecodeAsInteger(tag, ReadRaw(tag));

        if (_lastRawScan >= 0 && raw < _lastRawScan) _scanWraps++;
        _lastRawScan = raw;

        var span = tag.Type.SizeInBytes() * 8;
        return raw + (_scanWraps << span);
    }

    public string Read(string tag)
    {
        var resolved = _tags.Resolve(tag);
        return S7Values.Decode(resolved, ReadRaw(resolved));
    }

    private byte[] ReadRaw(S7Tag tag)
    {
        EnsureConnected();

        var buffer = new byte[tag.Type.SizeInBytes()];
        var status = _client.ReadDataBlock(tag.DbNumber, tag.ByteOffset, buffer);

        if (!status.Ok)
            throw new S7TransportException(
                $"could not read {tag.Describe()}: {status}. If this is the first read of that DB, " +
                "check that its 'Optimized block access' is OFF and that the CPU permits PUT/GET " +
                "communication from a remote partner — both fail at the read, not at the connect.");

        return buffer;
    }

    /// <summary>
    /// Write one value, having asked the fence first.
    ///
    /// <para>The order of the checks is deliberate. The area/tag agreement is checked BEFORE
    /// connecting, because it is a defect in the vector or the map and needs no device. Identity is
    /// established by connecting, because the guard's answer is only meaningful against the identity
    /// of the session the bytes will travel on. Only then is the guard asked — and its answer travels
    /// out intact as <see cref="S7WriteRefusedException"/>, which the runner turns into
    /// <see cref="VectorOutcome.Errored"/> carrying the guard's own words. A refusal that got
    /// swallowed here would be indistinguishable from a write that happened.</para>
    /// </summary>
    public void Write(string area, string tag, string value)
    {
        if (!Capabilities.HasFlag(TransportCapabilities.Write) || _writeGuard is null)
            throw new NotSupportedException(
                "this S7 transport is read-only: it was not built with a write fence, so it has no " +
                "authorization to write and will not attempt one.");

        var resolved = _tags.Resolve(tag);

        // The area is what the fence authorizes. If the caller names one area and the tag lives in
        // another, the fence would be answering a question about the wrong region of the device —
        // a fence that authorizes area X while the bytes land in area Y is a fence in name only.
        if (!string.Equals(resolved.Area.Trim(), (area ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            throw new S7TransportException(
                $"write to '{tag}' was requested under area '{area}', but the tag map puts it in " +
                $"'{resolved.Area}'. Refusing: the write fence is scoped on the area, so authorizing " +
                "one area and writing into another would defeat it.");

        EnsureConnected();

        var decision = _writeGuard.Check(_settings.Address, resolved.Area, _identity, _scope);
        if (!decision.Allowed) throw new S7WriteRefusedException(decision);

        var status = resolved.Type == S7DataType.Bool
            ? _client.WriteBit(resolved.DbNumber, resolved.ByteOffset, resolved.BitOffset,
                               S7Values.DecodeBool(resolved, value))
            : _client.WriteDataBlock(resolved.DbNumber, resolved.ByteOffset, S7Values.Encode(resolved, value));

        if (!status.Ok)
            throw new S7TransportException($"could not write {resolved.Describe()}: {status}.");
    }
}
