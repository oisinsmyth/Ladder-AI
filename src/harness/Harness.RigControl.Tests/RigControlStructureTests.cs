using System.Reflection;
using Harness.RigControl;

namespace Harness.RigControl.Tests;

/// <summary>
/// THE STRUCTURAL CLAIMS ABOUT THE <c>rig-control</c> ASSEMBLY, WALKED RATHER THAN DECLARED.
///
/// <para>🔴 <b>Why this file is not a <c>const bool</c>.</b> Measured in this repository on
/// 2026-08-14: the harness's strongest safety claim — <i>"the capability is absent from the
/// assembly"</i> — was backed by <c>Assert.False(Arming.CompiledIn)</c> where <c>CompiledIn</c> is a
/// <c>const bool</c>. A class constructing a live socket client was planted in that assembly and all
/// 202 tests stayed green. *** A CONSTANT STAYS TRUE EXACTLY AS LONG AS SOMEBODY REMEMBERS TO CHANGE
/// IT, WHICH IS THE ONE THING A FENCE MUST NEVER DEPEND ON. ***</para>
///
/// <para><b>Both halves, because neither is sufficient alone.</b> A DENOMINATOR
/// (<c>BodiesExamined &gt; 0</c>) proves the walk LOOKED — <c>Hits.Count == 0</c> is equally true of
/// <i>nothing found</i> and of <i>nothing looked at</i>. A live positive CONTROL proves it can DETECT.
/// The control is PARAMETERISED rather than a hand-edit, because *** a check whose exercise requires
/// editing the check will not be exercised *** — that is how this repository's IL-walk control
/// previously decayed into a comment describing a manual run somebody once did.</para>
/// </summary>
public class RigControlStructureTests
{
    /// <summary>
    /// The capabilities Sharp7 offers on the very object <c>Sharp7RunTransition</c> holds, and which
    /// this binary must not be able to reach. Their absence is therefore a property of the compiled
    /// assembly, not of a comment: <c>PlcHotStart</c> and <c>PlcStop</c> are one method call apart.
    /// </summary>
    public static TheoryData<string> ForbiddenMembers => new()
    {
        "Sharp7.S7Client.PlcStop",
        "Sharp7.S7Client.PlcColdStart",
        "Sharp7.S7Client.DBWrite",
        "Sharp7.S7Client.MBWrite",
        "Sharp7.S7Client.WriteArea",
        "Sharp7.S7Client.Delete",
        "Sharp7.S7Client.Download",
        "Sharp7.S7Client.PlcCopyRamToRom",
        "Sharp7.S7Client.SetPlcDateTime",
        "Sharp7.S7Client.SetPlcSystemDateTime",
        // Reached through Harness.S7's own client, which does expose data writes.
        "Harness.S7.Sharp7Client.WriteDataBlock",
        "Harness.S7.Sharp7Client.WriteBit",
        // The socket itself. Sharp7 owns it; nothing here should reach one directly.
        "System.Net.Sockets.",
    };

    /// <summary>
    /// *** THE ASSERTION. *** No method anywhere in the shipped assembly touches any of these. It fails
    /// if such a reference is added ANYWHERE — including in a branch no test exercises and in a class
    /// nothing calls, which is exactly the mutation that slipped past a whole suite before.
    /// </summary>
    [Theory]
    [MemberData(nameof(ForbiddenMembers))]
    public void NothingInTheRigControlAssembly_CanReachThisCapability(string member)
    {
        var scan = Scan(member);

        Assert.True(
            scan.BodiesExamined > 0,
            $"The walk examined NO method bodies, so finding nothing proves nothing. " +
            $"Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(
            scan.Hits.Count == 0,
            $"rig-control must not be able to reach {member}. Found: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// *** THE CONTROL, AND THE REASON THE ASSERTION MEANS ANYTHING. *** The same walk, over the same
    /// assembly, must FIND things this assembly provably uses. Without it, "no hits" is equally what a
    /// walk that resolves no tokens produces — the most reassuring output this file could emit, and
    /// evidence of nothing.
    ///
    /// <para>Parameterised so the control can be pointed at anything without editing the test.</para>
    /// </summary>
    [Theory]
    [InlineData("Harness.S7.S7RunStateReading")]      // the run-state decoder, used on every path
    [InlineData("DeviceGuard.DeviceAccessGuard")]     // the fence itself
    [InlineData("DeviceGuard.AllowlistFile")]
    public void TheWalk_ReallyDetectsATypeThatIsThere(string member)
    {
        var scan = Scan(member);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.True(
            scan.Hits.Count > 0,
            $"The walk found no reference to {member}, which this assembly certainly makes. The scanner " +
            "is broken, so the zero results reported by the assertions above prove nothing.");
    }

    /// <summary>
    /// *** THE CONTROL THAT MATTERS MOST: CAN THE WALK RESOLVE A <c>Sharp7</c> MEMBER TOKEN AT ALL? ***
    ///
    /// <para>Every forbidden member above is a Sharp7 one. If Sharp7 tokens simply did not resolve in
    /// this walk, all ten would report zero hits and the file would be pure decoration — the exact
    /// shape of a control that does not control for the right thing. So: when the live transport is
    /// compiled in, <c>Sharp7.S7Client.PlcHotStart</c> MUST be found, because that is the one Sharp7
    /// call this binary makes.</para>
    ///
    /// <para>When Sharp7 was absent at build time the transport is excluded from the assembly and this
    /// control cannot run. *** THAT IS ASSERTED RATHER THAN SKIPPED *** — the absence of the transport
    /// is pinned, so the day it appears without Sharp7 resolving, this fails and demands the comparison
    /// that could not be made.</para>
    /// </summary>
    [Fact]
    public void TheWalk_ReallyDetectsTheOneSharp7CallThisBinaryMakes()
    {
        var assembly = typeof(RigControlCli).Assembly;
        var liveTransport = assembly.GetType("Harness.RigControl.Sharp7RunTransition");

        if (liveTransport is null)
        {
            // Assert the absence rather than passing silently: a control that cannot run must leave a
            // fact behind, not a gap.
            Assert.DoesNotContain(assembly.GetTypes(), t => t.Name.Contains("Sharp7", StringComparison.Ordinal));
            return;
        }

        var scan = Scan("Sharp7.S7Client.PlcHotStart");

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies — the walk is broken.");
        Assert.True(
            scan.Hits.Count > 0,
            "The walk could not find Sharp7.S7Client.PlcHotStart, which Sharp7RunTransition.RequestRun " +
            "certainly calls. Sharp7 member tokens are therefore not resolving, and EVERY forbidden-member " +
            "assertion in this file is vacuous.");
    }

    /// <summary>
    /// The read-back has no escape hatch, and that is a property of the parser rather than of a
    /// comment. Every flag that would weaken this tool is refused BY NAME, so the refusal is a position
    /// rather than a spelling error.
    /// </summary>
    [Fact]
    public void EveryWeakeningFlagIsRefusedByName_AndTheListIsNotEmpty()
    {
        Assert.NotEmpty(RigControlCli.RefusedFlags);
        Assert.Contains("--skip-readback", RigControlCli.RefusedFlags.Keys);
        Assert.Contains("--stop", RigControlCli.RefusedFlags.Keys);
        Assert.Contains("--force", RigControlCli.RefusedFlags.Keys);
        Assert.All(RigControlCli.RefusedFlags.Values, why => Assert.False(string.IsNullOrWhiteSpace(why)));
    }

    // ---- the walk ------------------------------------------------------------------------------

    private static IlScan Scan(string spec)
    {
        var assembly = typeof(RigControlCli).Assembly;
        var hits = new List<string>();
        var types = 0;
        var methods = 0;
        var bodies = 0;

        foreach (var type in assembly.GetTypes())
        {
            types++;
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                methods++;

                byte[]? il;
                try
                {
                    il = method.GetMethodBody()?.GetILAsByteArray();
                }
                catch (Exception)
                {
                    // Abstract, extern and generated members have no readable body.
                    continue;
                }

                if (il is null) continue;

                bodies++;
                if (References(il, method, spec, out var detail))
                {
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
                }
            }
        }

        return new IlScan(hits, types, methods, bodies);
    }

    private sealed record IlScan(IReadOnlyList<string> Hits, int TypesEnumerated, int MethodsEnumerated, int BodiesExamined);

    /// <summary>
    /// Deliberately coarse: every 4-byte window is offered to the token resolver rather than the IL
    /// being decoded precisely. That OVER-reports candidate tokens and UNDER-reports nothing, and since
    /// the load-bearing assertion is that a count is ZERO, over-reporting is the safe direction — a
    /// false positive fails a test and gets read; a false negative would let the one thing this guards
    /// against through unnoticed.
    ///
    /// <para>Matching is at MEMBER granularity, not type-prefix, because this assembly legitimately
    /// uses <c>Sharp7.S7Client</c> — the whole question is WHICH of its methods it touches.</para>
    /// </summary>
    private static bool References(byte[] il, MethodInfo owner, string spec, out string detail)
    {
        detail = string.Empty;
        var module = owner.Module;
        var typeArgs = SafeGenericArguments(owner.DeclaringType);
        var methodArgs = owner.IsGenericMethodDefinition ? owner.GetGenericArguments() : Type.EmptyTypes;

        for (var i = 0; i + 4 <= il.Length; i++)
        {
            var token = BitConverter.ToInt32(il, i);

            try
            {
                var member = module.ResolveMember(token, typeArgs, methodArgs);
                var composed = member is Type t
                    ? t.FullName
                    : member?.DeclaringType?.FullName + "." + member?.Name;

                if (composed is null) continue;

                if (composed.Equals(spec, StringComparison.Ordinal) ||
                    composed.StartsWith(spec, StringComparison.Ordinal) &&
                    (spec.EndsWith('.') || composed.Length == spec.Length || composed[spec.Length] == '.'))
                {
                    detail = $"references {composed}";
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a member token. Expected constantly — see the remarks on coarseness.
            }
        }

        return false;
    }

    private static Type[] SafeGenericArguments(Type? type)
    {
        try
        {
            return type is { IsGenericTypeDefinition: true } ? type.GetGenericArguments() : Type.EmptyTypes;
        }
        catch (Exception)
        {
            return Type.EmptyTypes;
        }
    }
}
