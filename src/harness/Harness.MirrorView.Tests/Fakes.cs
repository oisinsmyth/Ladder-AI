using DeviceGuard;
using Harness.MirrorRead;
using NModbus;

namespace Harness.MirrorView.Tests;

/// <summary>A source that answers from a script. Counts its reads, so "nothing was read" is a fact.</summary>
internal sealed class ScriptedSource : IRegisterSource
{
    private readonly Queue<Func<ushort[]>> _answers = new();

    public int Reads { get; private set; }

    public bool Disposed { get; private set; }

    /// <summary>Always answer with these registers.</summary>
    public ushort[] Standing { get; set; } = Array.Empty<ushort>();

    public ScriptedSource Then(Func<ushort[]> answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    /// <summary>Answer with a whole area whose scan counter (registers 2:3) reads <paramref name="scan"/>.</summary>
    public static ushort[] Area(int registers, uint stamp = 0xF52ECEADu, uint scan = 0)
    {
        var values = new ushort[registers];
        if (registers > 1)
        {
            values[0] = (ushort)(stamp >> 16);
            values[1] = (ushort)(stamp & 0xFFFF);
        }

        if (registers > 3)
        {
            values[2] = (ushort)(scan >> 16);
            values[3] = (ushort)(scan & 0xFFFF);
        }

        return values;
    }

    public ushort[] Read(int startRegister, int count)
    {
        Reads++;
        return _answers.Count > 0 ? _answers.Dequeue()() : Standing;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>
/// The ONLY route to a connection in the tests, and it counts.
///
/// <para>That is the point: a test asserting <see cref="Opens"/> == 0 has proved the fence held as an
/// observable consequence, not as a status string a disconnected fence could also produce.</para>
/// </summary>
internal sealed class RecordingFactory : IRegisterSourceFactory
{
    private readonly Func<IRegisterSource> _open;

    public RecordingFactory(Func<IRegisterSource> open) => _open = open;

    public RecordingFactory(ScriptedSource source) : this(() => source) { }

    public int Opens { get; private set; }

    public IRegisterSource Open(string host, int port, byte unitId)
    {
        Opens++;
        return _open();
    }
}

/// <summary>A fence that always allows. The control for every refusal test.</summary>
internal sealed class AllowingFence : IDeviceFence
{
    public GuardDecision Check(string address) =>
        GuardDecision.Allow(new AllowlistEntry(address, "test rig", AllowlistEntry.TestRigKind));
}

/// <summary>A fence that refuses everything — the "the allowlist says no" mutation.</summary>
internal sealed class RefusingFence : IDeviceFence
{
    public GuardDecision Check(string address) =>
        GuardDecision.Refuse(GuardReason.TargetNotListed, $"'{address}' is not on the test-rig allowlist.");
}

/// <summary>A fence that faults. Neither a refusal nor a reading — nothing examined the target.</summary>
internal sealed class ThrowingFence : IDeviceFence
{
    public GuardDecision Check(string address) => throw new InvalidOperationException("the allowlist store exploded.");
}

/// <summary>A source whose reads never answer — a timeout, not a refusal.</summary>
internal sealed class SilentSource : IRegisterSource
{
    public ushort[] Read(int startRegister, int count) => throw new TimeoutException("no answer within 3000 ms.");

    public void Dispose() { }
}

/// <summary>A source whose reads come back as a Modbus exception response — a refusal BY THE SERVER.</summary>
internal sealed class ServerRefusingSource : IRegisterSource
{
    public ushort[] Read(int startRegister, int count) =>
        throw new SlaveException("Function Code: 131\nException Code: 2 - Illegal Data Address");

    public void Dispose() { }
}

/// <summary>A clock the test moves by hand. Staleness is a function of time, so time must be an input.</summary>
internal sealed class TestClock
{
    public TestClock(DateTimeOffset start) => Now = start;

    public DateTimeOffset Now { get; set; }

    public Func<DateTimeOffset> Read => () => Now;

    public void Advance(TimeSpan by) => Now += by;
}
