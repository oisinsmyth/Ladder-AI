using DeviceGuard;

namespace Harness.S7;

/// <summary>
/// Something went wrong talking to the device, or in the data around that conversation.
///
/// <para><b>Why exceptions and not a result type.</b> <see cref="Harness.ITransport"/> returns bare
/// strings and <c>void</c>; it has no channel for a failure. <see cref="Harness.VectorRunner"/> knows
/// this and catches everything, turning it into <see cref="Harness.VectorOutcome.Errored"/> with the
/// exception's type name and message — an outcome that is explicitly NOT a pass and that spoils the
/// run's green. So an exception here surfaces in the report rather than being swallowed, which is
/// exactly what a refusal needs to do.</para>
/// </summary>
public class S7TransportException : Exception
{
    public S7TransportException(string message) : base(message) { }
    public S7TransportException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// The map, the settings or the wiring is wrong — a mistake made before anything was sent. Separated
/// from <see cref="S7TransportException"/> because the fix is in a file, not on the network.
/// </summary>
public sealed class S7ConfigurationException : S7TransportException
{
    public S7ConfigurationException(string message) : base(message) { }
    public S7ConfigurationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// The read fence refused this target. Carries the guard's own decision so the reason is not
/// paraphrased on its way out.
/// </summary>
public sealed class S7AccessRefusedException : S7TransportException
{
    public S7AccessRefusedException(GuardDecision decision)
        : base(decision.Message) => Decision = decision;

    public GuardDecision Decision { get; }
}

/// <summary>
/// The write fence refused this write.
///
/// <para>The guard's <see cref="WriteDecision"/> travels intact rather than being flattened to a
/// string, for two reasons. A caller that wants to distinguish "this rig was never marked
/// write-eligible" from "the device at that address is not the device we expected" needs
/// <see cref="WriteDecision.Reason"/>, not prose. And keeping the decision object makes it impossible
/// for this layer to soften the wording on the way past — the message the engineer reads is the one
/// the fence wrote.</para>
/// </summary>
public sealed class S7WriteRefusedException : S7TransportException
{
    public S7WriteRefusedException(WriteDecision decision)
        : base(decision.Message) => Decision = decision;

    public WriteDecision Decision { get; }

    public WriteRefusal Reason => Decision.Reason;
}
