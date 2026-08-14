using Harness.RigControl;
using Harness.S7;

namespace Harness.RigControl.Tests;

/// <summary>
/// A transport that FAILS THE TEST if anything at all is asked of it.
///
/// <para>Not a fake device — the point is that no member is reachable, so a run that reached for a
/// controller shows up as a named failure rather than as a socket. Used together with a factory that
/// counts its own calls, because the two catch different things: the counter proves the transport was
/// never CONSTRUCTED, this proves it was never USED. A refusal that constructs a transport and then
/// declines to talk to it has still moved past the fence.</para>
/// </summary>
public sealed class NeverTouchedTransport : IRunTransitionTransport
{
    public bool Connected => throw Used(nameof(Connected));

    public S7Status Connect(string address, int rack, int slot, int connectTimeoutMs) =>
        throw Used(nameof(Connect));

    public void Disconnect() => throw Used(nameof(Disconnect));
    public S7Status ReadOrderCode(out string orderCode) => throw Used(nameof(ReadOrderCode));
    public S7Status ReadRunState(out S7RunStateReading runState) => throw Used(nameof(ReadRunState));
    public S7Status RequestRun() => throw Used(nameof(RequestRun));
    public void Dispose() { }

    private static Exception Used(string member) =>
        new InvalidOperationException($"the run touched the device: IRunTransitionTransport.{member} was called.");
}

/// <summary>
/// A scripted CPU with no socket. Every member records that it was called, so a test can assert what
/// the tool did to the device as well as what it printed.
///
/// <para>The run state is a QUEUE rather than a field, so the before-read and the after-read can differ
/// — which is the whole subject here. A CPU that is asked to run passes through a non-running startup
/// state first, and a scripted transport that could not express that would only ever be able to test
/// the easy case.</para>
/// </summary>
public sealed class ScriptedTransport : IRunTransitionTransport
{
    private readonly Queue<S7RunStateReading> _states;
    private readonly S7RunStateReading _afterQueue;

    public ScriptedTransport(
        IEnumerable<S7RunStateReading>? states = null,
        S7RunStateReading? settledState = null)
    {
        _states = new Queue<S7RunStateReading>(states ?? Array.Empty<S7RunStateReading>());
        _afterQueue = settledState ?? S7RunStateReading.Decode(S7RunStateReading.RunValue);
    }

    public static S7RunStateReading Running => S7RunStateReading.Decode(S7RunStateReading.RunValue);
    public static S7RunStateReading Stopped => S7RunStateReading.Decode(3);

    public S7Status ConnectResult { get; set; } = S7Status.Success;
    public S7Status OrderCodeResult { get; set; } = S7Status.Success;
    public S7Status RunStateResult { get; set; } = S7Status.Success;
    public S7Status RequestRunResult { get; set; } = S7Status.Success;
    public string OrderCode { get; set; } = "6ES7 214-1AG40-0XB0";

    public int Connects { get; private set; }
    public int OrderCodeReads { get; private set; }
    public int RunStateReads { get; private set; }
    public int RunRequests { get; private set; }
    public bool Disposed { get; private set; }

    public bool Connected { get; private set; }

    public S7Status Connect(string address, int rack, int slot, int connectTimeoutMs)
    {
        Connects++;
        Connected = ConnectResult.Ok;
        return ConnectResult;
    }

    public void Disconnect() => Connected = false;

    public S7Status ReadOrderCode(out string orderCode)
    {
        OrderCodeReads++;
        orderCode = OrderCodeResult.Ok ? OrderCode : string.Empty;
        return OrderCodeResult;
    }

    public S7Status ReadRunState(out S7RunStateReading runState)
    {
        RunStateReads++;
        if (!RunStateResult.Ok)
        {
            runState = S7RunStateReading.Unread;
            return RunStateResult;
        }

        runState = _states.Count > 0 ? _states.Dequeue() : _afterQueue;
        return RunStateResult;
    }

    public S7Status RequestRun()
    {
        RunRequests++;
        return RequestRunResult;
    }

    public void Dispose() => Disposed = true;
}
