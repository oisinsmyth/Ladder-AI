using Harness.Wire;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// *** THE LIVE POSITIVE CONTROL FOR THE IL WALK. DO NOT DELETE, AND DO NOT MOVE IT INTO THE SHIPPED
/// ASSEMBLY. ***
///
/// <para>This class calls <c>IRegisterTransport.WriteHoldingRegisters</c> — the exact member
/// <c>MirrorReadStructureTests</c> asserts is absent from <c>Harness.MirrorRead</c>. The walk is run
/// over THIS assembly as well, and must FIND it.</para>
///
/// <para><b>Why a planted write and not "a type the tool provably uses".</b> A control that detects a
/// member the binary legitimately references proves the token resolver works. It does not prove the
/// write PREDICATE works, and the predicate is the part that could be wrong — a needle spelled with a
/// typo, a namespace filter that excludes the very namespace it is meant to search, a name comparison
/// that never matches. Both failures produce zero hits, which is the most reassuring output this file
/// could emit. The reason the whole technique is here is that a <c>const bool</c> plus
/// <c>Assert.False</c> let a class constructing a live socket client sit inside
/// <c>Harness.RigWrite</c> with all 202 tests green (2026-08-14). A control that could not have
/// caught that repeats the mistake one level up.</para>
///
/// <para>Nothing calls this method. That is deliberate — the walk is over method BODIES, not over
/// reachable paths, and a control that had to be invoked would be testing something else.</para>
/// </summary>
internal static class PlantedWriteControl
{
    /// <summary>Never called. Exists so the walk has something real to find.</summary>
    internal static void WriteSomething(IRegisterTransport transport, ushort[] values) =>
        transport.WriteHoldingRegisters(0, values);
}
