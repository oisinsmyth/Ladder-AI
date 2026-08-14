using System.Net;
using Harness.Wire;

namespace Harness.MirrorView.Tests;

/// <summary>
/// *** THE LIVE POSITIVE CONTROL FOR THE WRITE WALK. DO NOT DELETE, AND DO NOT MOVE IT INTO THE SHIPPED
/// ASSEMBLY. ***
///
/// <para>This class calls <c>IRegisterTransport.WriteHoldingRegisters</c> — the exact member
/// <c>MirrorViewStructureTests</c> asserts is absent from <c>Harness.MirrorView</c>. The walk is run
/// over THIS assembly too, and must FIND it.</para>
///
/// <para>Nothing calls this method. That is deliberate: the walk is over method BODIES, not over
/// reachable paths, and a control that had to be invoked would be testing something else. A
/// <c>const bool</c> plus <c>Assert.False</c> is what this technique replaces — measured 2026-08-14, a
/// class constructing a live socket client sat inside <c>Harness.RigWrite</c> with all 202 tests
/// green.</para>
/// </summary>
internal static class PlantedWriteControl
{
    /// <summary>Never called. Exists so the walk has something real to find.</summary>
    internal static void WriteSomething(IRegisterTransport transport, ushort[] values) =>
        transport.WriteHoldingRegisters(0, values);
}

/// <summary>
/// *** THE LIVE POSITIVE CONTROL FOR THE LOOPBACK WALK. SAME RULES. ***
///
/// <para>The claim "this viewer is not reachable from a network interface" is a claim about the
/// compiled assembly, and it is worth exactly as much as the evidence that the predicate behind it can
/// fire. This class names <c>IPAddress.Any</c>, and the walk must find it here while finding none in
/// the shipped assembly.</para>
/// </summary>
internal static class PlantedBindAnyControl
{
    /// <summary>Never called.</summary>
    internal static IPAddress[] EveryInterface() => new[] { IPAddress.Any, IPAddress.IPv6Any };
}
