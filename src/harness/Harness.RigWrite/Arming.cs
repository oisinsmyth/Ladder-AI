namespace Harness.RigWrite;

/// <summary>
/// Whether this build is capable of writing to a device. It is not, and this is the one place that
/// says so.
///
/// <para><b>How the refusal is implemented, and why this way.</b> The brief offered two options: a
/// flag that does not exist, or a flag that is hard-disabled. This build takes BOTH, in the order that
/// makes the property checkable rather than merely stated:</para>
///
/// <list type="number">
/// <item><b>No socket-owning code is reachable.</b> Nothing in this assembly constructs an
/// <see cref="Harness.S7.IS7Client"/>. The composition root hands the CLI a factory that throws
/// (<c>Program.cs</c>), the CLI never calls it, and there is no other route to one. A capability that
/// is not present cannot be re-enabled by a flag — the same reasoning
/// <see cref="Harness.S7.IS7Client"/> gives for leaving <c>PlcStop</c> and <c>Download</c> off its own
/// surface.</item>
///
/// <item><b>The verb is absent from the parser.</b> There is no <c>--arm</c>, no <c>--execute</c>, no
/// <c>--yes</c>. Asking for one is not an unknown-flag error but a named refusal that explains the
/// state of the build, because "unknown option" would read as a typo and send somebody looking for the
/// right spelling.</item>
/// </list>
///
/// <para><b>What arming this would actually take</b> — recorded here so the next person does not have
/// to infer it: a live <see cref="Harness.S7.IS7Client"/> factory in <c>Program.cs</c>
/// (<c>Sharp7Client</c>, which needs Sharp7 present at build time), an execute path that captures the
/// restore point through <see cref="Harness.S7.S7RegionAccess"/> before it writes anything, and an
/// allowlist entry with <c>writeEligible: true</c>. Each of those is a deliberate act by a person, and
/// no two of them together are enough.</para>
/// </summary>
public static class Arming
{
    /// <summary>False in this build, and there is no configuration that makes it true.</summary>
    public const bool CompiledIn = false;

    public const string WhyNot =
        "This build of rig-write CANNOT write to a device. It contains no code that opens a socket: " +
        "the CLI is handed a client factory that throws, and it never calls it. Arming is not a flag " +
        "that was left off — the capability is absent from the assembly. See src/harness/" +
        "Harness.RigWrite/Arming.cs.";

    /// <summary>The refusal, as an exception, for any path that reaches for a device anyway.</summary>
    public static NotSupportedException Refuse() => new(WhyNot);
}
