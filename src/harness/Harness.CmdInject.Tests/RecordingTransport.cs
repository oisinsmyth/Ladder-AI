using Harness.CmdInject;
using Harness.Map;
using Harness.Wire;

namespace Harness.CmdInject.Tests;

/// <summary>One write this transport was asked to perform — a span and its values, in the order it arrived.</summary>
internal sealed record RecordedWrite(int StartRegister, IReadOnlyList<ushort> Values)
{
    public int LastRegister => StartRegister + Values.Count - 1;

    public bool Covers(int register) => register >= StartRegister && register <= LastRegister;
}

/// <summary>One transaction of either kind, in the order it happened. Reads matter as well as writes when the claim is "verified by re-read".</summary>
internal sealed record RecordedEvent(bool IsWrite, int StartRegister, int Count)
{
    public override string ToString() => $"{(IsWrite ? "W" : "R")} {StartRegister}+{Count}";
}

/// <summary>
/// A transport that exists only in this process: it records every transaction in order and answers reads
/// from a small backing store. <b>It opens no socket</b> — its whole purpose is to make the ordering and
/// the gating observable without a device.
///
/// <para>Reads are recorded as well as writes, because the restore's claim is "drop the enable, put the
/// band back, <i>then verify by re-reading</i>" — and an ordering claim whose last step is a read cannot
/// be checked against a log that only holds writes.</para>
/// </summary>
internal sealed class RecordingTransport : IInjectionTransport
{
    private readonly Dictionary<int, ushort> _store = new();

    internal List<RecordedWrite> Writes { get; } = new();

    internal List<RecordedEvent> Events { get; } = new();

    internal bool Disposed { get; private set; }

    /// <summary>Set by a test to make the next read throw — a silence, which is never a verdict.</summary>
    internal Func<int, int, Exception?>? FailReads { get; set; }

    /// <summary>Set by a test to make a write throw — the restore-cannot-complete path.</summary>
    internal Func<int, IReadOnlyList<ushort>, Exception?>? FailWrites { get; set; }

    /// <summary>
    /// Registers whose writes are RECORDED but never stored — a register that will not take the value.
    /// This is what a restore that cannot be verified looks like from the client's side: the write returned
    /// without error and the re-read disagrees.
    /// </summary>
    internal HashSet<int> IgnoreWritesTo { get; } = new();

    internal void Poke(int register, ushort value) => _store[register] = value;

    internal ushort Peek(int register) => _store.TryGetValue(register, out var v) ? v : (ushort)0;

    /// <summary>Publish a build stamp at registers 0–1, high word first, as the copy layer does.</summary>
    internal void PublishStamp(BuildStamp stamp)
    {
        var words = RegisterWords.From32(stamp.Value, RegisterWordOrder.HighWordFirst);
        Poke(ControlRegisters.BuildStamp, words[0]);
        Poke(ControlRegisters.BuildStamp + 1, words[1]);
    }

    /// <summary>Publish a scan counter at registers 2–3, high word first.</summary>
    internal void PublishScan(uint scans)
    {
        var words = RegisterWords.From32(scans, RegisterWordOrder.HighWordFirst);
        Poke(ControlRegisters.ScanCounter, words[0]);
        Poke(ControlRegisters.ScanCounter + 1, words[1]);
    }

    /// <summary>
    /// Whether the free-running scan counter advances on every read, as a RUNNING CPU's does.
    ///
    /// <para><b>On by default, because that is what "a transport presenting itself the way a running
    /// harness program does" means.</b> A counter that never moves is a STOPPED CPU — which the arming
    /// path is required to refuse rather than stamp into — so a test that wants that state turns this off
    /// and gets it deliberately, instead of every test getting it by omission.</para>
    /// </summary>
    internal bool ScanAdvancesOnRead { get; set; } = true;

    private void AdvanceScan()
    {
        var now = ScanCount.FromRegisters(
            Peek(ControlRegisters.ScanCounter), Peek(ControlRegisters.ScanCounter + 1), RegisterWordOrder.HighWordFirst);

        PublishScan(unchecked(now.Raw + 1));
    }

    public void WriteRegisters(int startRegister, IReadOnlyList<ushort> values)
    {
        if (FailWrites?.Invoke(startRegister, values) is { } failure) throw failure;

        Events.Add(new RecordedEvent(IsWrite: true, startRegister, values.Count));
        Writes.Add(new RecordedWrite(startRegister, values.ToList()));
        for (var i = 0; i < values.Count; i++)
        {
            if (IgnoreWritesTo.Contains(startRegister + i)) continue;
            _store[startRegister + i] = values[i];
        }
    }

    public ushort[] ReadRegisters(int startRegister, int count)
    {
        if (FailReads?.Invoke(startRegister, count) is { } failure) throw failure;

        // A read that failed advanced nothing, so this sits after the failure and not before it.
        if (ScanAdvancesOnRead) AdvanceScan();

        Events.Add(new RecordedEvent(IsWrite: false, startRegister, count));

        var answer = new ushort[count];
        for (var i = 0; i < count; i++)
            answer[i] = Peek(startRegister + i);
        return answer;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>
/// The sentinel that makes the fence's ordering observable: it counts the one call that would open a
/// session. A test asserting <c>Opens == 0</c> has proved the fence held — an observable fact, not an exit
/// code a disconnected gate could also produce.
/// </summary>
internal sealed class RecordingFactory : IInjectionTransportFactory
{
    private readonly IInjectionTransport _transport;

    internal RecordingFactory(IInjectionTransport? transport = null) => _transport = transport ?? new RecordingTransport();

    internal int Opens { get; private set; }

    internal string? LastHost { get; private set; }

    internal int LastPort { get; private set; }

    /// <summary>Set by a test to make opening throw — the connect-failed path, which writes nothing.</summary>
    internal Exception? OpenThrows { get; set; }

    internal RecordingTransport Transport => (RecordingTransport)_transport;

    internal IInjectionTransport Port => _transport;

    public IInjectionTransport Open(string host, int port, byte unitId)
    {
        Opens++;
        LastHost = host;
        LastPort = port;

        if (OpenThrows is { } failure) throw failure;

        return _transport;
    }
}

/// <summary>A clock that does not wait, so a poll loop's test costs no time.</summary>
internal sealed class InstantClock : IInjectionClock
{
    internal int Waits { get; private set; }

    public void Wait(int milliseconds, CancellationToken cancel) => Waits++;
}
