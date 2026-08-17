using Harness.Wire;

namespace Harness.Verify.Tests;

/// <summary>
/// *** THE LIVE POSITIVE CONTROL FOR THE IL WALK. DO NOT DELETE, AND DO NOT MOVE IT INTO THE SHIPPED
/// ASSEMBLY. ***
///
/// <para>These methods name the exact members <see cref="VerifyStructureTests"/> asserts are absent from
/// <c>Harness.Verify</c>: the transport's <c>WriteHoldingRegisters</c> and <c>MirrorClient</c>'s four
/// write verbs. The same walk is run over THIS assembly and must FIND all of them.</para>
///
/// <para><b>Why a planted write and not "a type the tool provably uses".</b> A control that detects a
/// member the binary legitimately references proves the token resolver works. It does not prove the WRITE
/// PREDICATE works, and the predicate is the part that could be wrong — a needle spelled with a typo, a
/// namespace filter that excludes the namespace it is meant to search, a name comparison that never
/// matches. Every one of those produces zero hits, which is the most reassuring output this file could
/// emit. Measured 2026-08-14 on <c>Harness.RigWrite</c>: a <c>const bool</c> plus an <c>Assert.False</c>
/// let a class constructing a live socket client sit in the shipped assembly with all 202 tests green.</para>
///
/// <para>Nothing calls these methods. That is deliberate — the walk is over method BODIES, not over
/// reachable paths, and a control that had to be invoked would be testing something else.</para>
/// </summary>
internal static class PlantedWriteControl
{
    /// <summary>Never called. Exists so the walk has a transport-level write to find.</summary>
    internal static void WriteSomething(IRegisterTransport transport, ushort[] values) =>
        transport.WriteHoldingRegisters(0, values);

    /// <summary>
    /// Never called. Exists so the walk has <c>MirrorClient</c>'s OWN write verbs to find — which is the
    /// harder half here, because unlike <c>harness-mirror-read</c> this binary legitimately HOLDS a
    /// <c>MirrorClient</c>. "Does not touch the type" is not available as the claim, so the claim is
    /// "does not name these four members", and this is what proves that predicate can fire.
    /// </summary>
    internal static void CommandSomething(MirrorClient client, ushort[] values)
    {
        client.WriteVector(0, values);
        client.Commit(new[] { 0 });
        client.LowerAllStartBools();
        client.ClearStartEcho();
    }
}
