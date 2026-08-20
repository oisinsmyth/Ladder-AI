using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// *** THE LIVE POSITIVE CONTROL FOR THE IL WALK. DO NOT DELETE, AND DO NOT MOVE IT INTO THE SHIPPED
/// ASSEMBLY. ***
///
/// <para>This class calls <see cref="IInjectionTransport.WriteRegisters"/> — the exact member
/// <c>InjectionStructureTests</c> counts in the shipped assembly (it must be named by EXACTLY ONE method
/// there). The walk is run over THIS assembly too, and must FIND this call. Without it, "the shipped
/// assembly names it once" is equally what a mistyped needle or a namespace filter that excludes its own
/// subject would produce.</para>
///
/// <para>Nothing calls this method. The walk is over method BODIES, not reachable paths, so a control that
/// had to be invoked would be testing something else.</para>
/// </summary>
internal static class PlantedInjectionWrite
{
    /// <summary>Never called. Exists so the walk has a real write to find.</summary>
    internal static void WriteSomething(IInjectionTransport transport, ushort[] values) =>
        transport.WriteRegisters(0, values);
}
