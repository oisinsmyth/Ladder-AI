using System.Reflection;
using Harness.Wire;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// THE STRUCTURAL CLAIM BEHIND <c>harness-mirror-read</c>'s banner, CHECKED RATHER THAN DECLARED.
///
/// <para>The banner says <i>"This binary contains no write path"</i>. That is a claim about the
/// COMPILED ASSEMBLY, and the only thing entitled to make it is a walk over the compiled assembly.
/// A <c>const bool</c> with an <c>Assert.False</c> on it is what this replaces: measured 2026-08-14
/// on <c>Harness.RigWrite</c>, planting a class that constructed a live socket client left all 202
/// tests green, because a constant stays true exactly as long as somebody remembers to change it.</para>
///
/// <para><b>Three halves, and none of them subsumes the others.</b>
/// <list type="number">
/// <item>A <b>denominator</b> — <c>BodiesExamined &gt; 0</c>. <c>Hits.Count == 0</c> is true of
/// <i>nothing found</i> and of <i>nothing looked at</i>, and only the count separates them.</item>
/// <item>A <b>live positive control</b> — the same walk, with the same predicate, over an assembly that
/// really does contain the forbidden call (<see cref="PlantedWriteControl"/>). This is what proves the
/// PREDICATE can fire, not merely that the resolver runs.</item>
/// <item>A <b>resolution control in the target module</b> — the walk must find something it certainly
/// does reference inside <c>Harness.MirrorRead</c> itself. Without it, a module whose tokens resolve to
/// nothing would report a clean sweep.</item>
/// </list></para>
///
/// <para><b>The predicate is a parameter, never a literal in the walk.</b> A check whose exercise
/// requires editing the check will not be exercised — that is how the equivalent control in
/// <c>openness-cli</c> decayed into a comment describing a manual run somebody once did. Every test
/// here passes its own predicate to the same <see cref="Scan"/>.</para>
/// </summary>
public class MirrorReadStructureTests
{
    private static Assembly ShippedAssembly => typeof(MirrorReadRun).Assembly;

    private static Assembly ThisTestAssembly => typeof(PlantedWriteControl).Assembly;

    /// <summary>
    /// The namespaces whose write verbs are the hazard. Scoped, because <c>TextWriter.Write</c> is
    /// how this tool reports and a predicate that caught it would fire on every run — and a gate that
    /// fires on cases outside its scope is noise, and noise gets switched off.
    /// </summary>
    private static readonly string[] WireNamespaces = { "Harness.Wire", "Harness.Map", "NModbus" };

    /// <summary>
    /// Member-name prefixes that move bytes towards the device, or towards a latch the harness owns.
    /// <c>Commit</c>, <c>LowerAllStartBools</c> and <c>ClearStartEcho</c> are on the list because
    /// <c>MirrorClient</c> spells its writes that way and a "Write"-only rule would miss all three.
    /// </summary>
    private static readonly string[] WriteVerbs = { "Write", "Commit", "LowerAll", "ClearStart" };

    // ---- 1. THE ASSERTION -----------------------------------------------------------------------

    /// <summary>
    /// *** THE CLAIM. *** No method anywhere in the shipped assembly names a write member of the wire
    /// stack. It fails if such a reference is added ANYWHERE — in a branch no test exercises, in a
    /// private helper, in a class nothing calls. That last case is exactly the mutation that walked
    /// past <c>Harness.RigWrite</c>'s whole suite.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_NamesAWriteMemberOfTheWireStack()
    {
        var scan = Scan(ShippedAssembly, IsWireWriteMember);

        Assert.True(
            scan.BodiesExamined > 0,
            "The walk examined NO method bodies, so finding nothing proves nothing. " +
            $"Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(
            scan.Hits.Count == 0,
            "harness-mirror-read claims no write path exists in this binary. Found: " +
            string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// The other half of the same claim, stated separately because it is a different fact: the
    /// assembly must not reach a socket DIRECTLY either, bypassing the wire stack altogether.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_TouchesSystemNetSockets()
    {
        var scan = Scan(ShippedAssembly, m => DeclaringName(m)?.StartsWith("System.Net.Sockets.", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "the assembly reaches a socket directly: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// <c>MirrorClient</c> is the write-capable client — vectors, the start-bool commit, the echo
    /// clear. This tool has no business holding one, and not touching the type at all is a stronger
    /// and much cheaper property to check than not touching its individual write members.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_TouchesTheWriteCapableMirrorClient()
    {
        var scan = Scan(ShippedAssembly, m => DeclaringName(m) == "Harness.Wire.MirrorClient");

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "the assembly holds the write-capable client: " + string.Join("; ", scan.Hits));
    }

    // ---- 2. THE CONTROLS ------------------------------------------------------------------------

    /// <summary>
    /// *** THE LIVE POSITIVE CONTROL, AND THE REASON THE ASSERTION MEANS ANYTHING. *** The same walk
    /// with the same predicate, run over an assembly that really does call
    /// <c>WriteHoldingRegisters</c>, must FIND it. Without this, "no hits" is equally what a mistyped
    /// needle or a namespace filter that excludes its own subject produces.
    /// </summary>
    [Fact]
    public void TheWalk_FindsAPlantedWriteCall()
    {
        var scan = Scan(ThisTestAssembly, IsWireWriteMember);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(PlantedWriteControl), StringComparison.Ordinal));
    }

    /// <summary>
    /// *** THE RESOLUTION CONTROL, IN THE MODULE UNDER TEST. *** The planted control above proves the
    /// predicate fires; it does not prove tokens resolve inside <c>Harness.MirrorRead</c>'s own module,
    /// which is a per-module operation. <c>ModbusRegisterSource</c> certainly calls
    /// <c>IRegisterTransport.ReadHoldingRegisters</c>, so the walk must see it there.
    /// </summary>
    [Fact]
    public void TheWalk_ResolvesWireTokensInsideTheShippedAssembly()
    {
        var scan = Scan(ShippedAssembly, m =>
            m is not Type && m.Name == nameof(IRegisterTransport.ReadHoldingRegisters) &&
            DeclaringName(m)?.StartsWith("Harness.Wire", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(
            scan.Hits.Count > 0,
            "The walk found no reference to Harness.Wire's ReadHoldingRegisters, which " +
            "ModbusRegisterSource certainly makes. The scanner cannot resolve tokens in this module, " +
            "so every zero it reports above proves nothing.");
    }

    // ---- 3. THE RESIDUAL, PINNED ----------------------------------------------------------------

    /// <summary>
    /// <b>What the claim above does NOT cover, asserted so it cannot quietly stop being true.</b>
    ///
    /// <para><c>Harness.Wire</c> DOES contain a write — the harness proper must write vectors and raise
    /// start bools — and this tool references that assembly. The property being checked is that nothing
    /// here NAMES it, not that the capability has been deleted from the process.</para>
    ///
    /// <para>This test pins the residual by asserting the write is still there. If <c>Harness.Wire</c>
    /// ever loses it, this goes red and forces somebody to re-read the banner and the walk above,
    /// rather than leaving a caveat in a document that has already stopped being true. A caveat in a
    /// report decays; a caveat that is a red test cannot.</para>
    /// </summary>
    [Fact]
    public void TheResidualIsReal_HarnessWireStillCarriesTheWriteThisBinaryAvoids()
    {
        var write = typeof(IRegisterTransport).GetMethod(nameof(IRegisterTransport.WriteHoldingRegisters));

        Assert.True(
            write is not null,
            "Harness.Wire.IRegisterTransport no longer has WriteHoldingRegisters. The structural claim " +
            "in this file and the banner in MirrorReadRun both describe a residual that no longer " +
            "exists — re-read both before deleting this test.");
    }

    // ---- the walk -------------------------------------------------------------------------------

    private static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    private static bool IsWireWriteMember(MemberInfo member)
    {
        // A bare TYPE token is not a write. Naming Harness.Wire.MirrorClient is covered by its own
        // test above; conflating the two here would make this predicate fire on ordinary type
        // references and turn the assertion into noise.
        if (member is Type) return false;

        var declaring = DeclaringName(member);
        if (declaring is null) return false;
        if (!WireNamespaces.Any(ns => declaring.StartsWith(ns, StringComparison.Ordinal))) return false;

        return WriteVerbs.Any(verb => member.Name.StartsWith(verb, StringComparison.Ordinal));
    }

    private static IlScan Scan(Assembly assembly, Func<MemberInfo, bool> forbidden)
    {
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
                if (References(il, method, forbidden, out var detail))
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
            }
        }

        return new IlScan(hits, types, methods, bodies);
    }

    private sealed record IlScan(IReadOnlyList<string> Hits, int TypesEnumerated, int MethodsEnumerated, int BodiesExamined);

    /// <summary>
    /// Deliberately coarse, for the reason <c>openness-cli</c>'s equivalent gives: every 4-byte window
    /// is offered to the token resolver rather than the IL being decoded precisely. That OVER-reports
    /// candidate tokens and UNDER-reports nothing, and since the assertion is that the count is ZERO,
    /// over-reporting is the safe direction — a false positive fails a test and gets read, a false
    /// negative would let the one thing this guards against through unnoticed.
    ///
    /// <para>⚠️ The direction reverses for the two CONTROLS, which assert a count above zero: there a
    /// spurious token could make a broken walk look armed. Both controls guard against that by matching
    /// on a specific expected member (<c>PlantedWriteControl</c>, <c>ReadHoldingRegisters</c>) rather
    /// than on "found something".</para>
    /// </summary>
    private static bool References(byte[] il, MethodInfo owner, Func<MemberInfo, bool> forbidden, out string detail)
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
                if (member is not null && forbidden(member))
                {
                    detail = $"references {DeclaringName(member)}.{member.Name}";
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
