using System.Reflection;
using Harness.Wire;

namespace Harness.Verify.Tests;

/// <summary>
/// THE STRUCTURAL CLAIM BEHIND <c>harness-verify</c>'s banner, CHECKED RATHER THAN DECLARED.
///
/// <para>The banner says <i>"READ-ONLY: TCP connect, FC03, disconnect. No write function code exists in
/// this binary."</i> That is a claim about the COMPILED ASSEMBLY, and the only thing entitled to make it
/// is a walk over the compiled assembly. A <c>const bool</c> with an <c>Assert.False</c> on it is what
/// this replaces: measured 2026-08-14 on <c>Harness.RigWrite</c>, planting a class that constructed a
/// live socket client left all 202 tests green, because a constant stays true exactly as long as
/// somebody remembers to change it.</para>
///
/// <para>🔴 <b>THE CLAIM HERE IS NARROWER THAN <c>harness-mirror-read</c>'s, AND IT HAS TO BE.</b> That
/// tool asserts it never touches <c>MirrorClient</c> at all. This one MUST hold a <c>MirrorClient</c> —
/// exercising the client's own DB-6 guard is the entire purpose — and the client carries
/// <c>WriteVector</c>, <c>Commit</c>, <c>LowerAllStartBools</c> and <c>ClearStartEcho</c>. So the property
/// checked is per-MEMBER rather than per-TYPE, and <see cref="PlantedWriteControl"/> plants all five write
/// members so the predicate is proved able to fire on each shape.</para>
///
/// <para><b>Four halves, and none subsumes the others.</b>
/// <list type="number">
/// <item>A <b>denominator</b> — <c>BodiesExamined &gt; 0</c>. <c>Hits.Count == 0</c> is true of <i>nothing
/// found</i> and of <i>nothing looked at</i>, and only the count separates them.</item>
/// <item>A <b>live positive control</b> — the same walk, same predicate, over an assembly that really does
/// contain the forbidden calls. This proves the PREDICATE fires, not merely that the resolver runs.</item>
/// <item>A <b>resolution control in the target module</b> — the walk must find something the shipped
/// assembly certainly does reference (<c>MirrorClient.ReadControl</c>). Without it, a module whose tokens
/// resolve to nothing would report a clean sweep.</item>
/// <item>The <b>residual, pinned</b> — the write capability still EXISTS on <c>MirrorClient</c>. If it
/// ever stops existing, this file and the banner both describe something that has ceased to be true.</item>
/// </list></para>
///
/// <para><b>The predicate is a parameter, never a literal in the walk.</b> A check whose exercise requires
/// editing the check will not be exercised — that is how the equivalent control in <c>openness-cli</c>
/// decayed into a comment describing a manual run somebody once did.</para>
/// </summary>
public class VerifyStructureTests
{
    private static Assembly ShippedAssembly => typeof(VerifyRun).Assembly;

    private static Assembly ThisTestAssembly => typeof(PlantedWriteControl).Assembly;

    /// <summary>
    /// The namespaces whose write verbs are the hazard. Scoped, because <c>TextWriter.WriteLine</c> is how
    /// this tool reports and a predicate that caught it would fire on every run — and a gate that fires on
    /// cases outside its scope is noise, and noise gets switched off.
    /// </summary>
    private static readonly string[] WireNamespaces = { "Harness.Wire", "Harness.Map", "NModbus" };

    /// <summary>
    /// Member-name prefixes that move bytes towards the device, or towards a latch the harness owns.
    /// <c>Commit</c>, <c>LowerAll</c> and <c>ClearStart</c> are on the list because <c>MirrorClient</c>
    /// spells three of its four writes that way and a "Write"-only rule would miss all three.
    /// </summary>
    private static readonly string[] WriteVerbs = { "Write", "Commit", "LowerAll", "ClearStart" };

    /// <summary>
    /// The two entry points that CAN reach a write, in assemblies this binary references.
    /// <c>LoopRun.Execute</c> writes vectors and raises start bools; <c>LoopCli.Run</c> is the CLI that
    /// drives it. Naming neither is a stronger statement than the member walk alone, because it also
    /// covers a write reached TRANSITIVELY rather than named here.
    /// </summary>
    private static readonly (string Type, string Member)[] WritingEntryPoints =
    {
        ("Harness.Loop.LoopRun", "Execute"),
        ("Harness.Run.LoopCli", "Run"),
    };

    // ---- 1. THE ASSERTIONS ------------------------------------------------------------------------

    /// <summary>
    /// *** THE CLAIM. *** No method anywhere in the shipped assembly names a write member of the wire
    /// stack. It fails if such a reference is added ANYWHERE — in a branch no test exercises, in a private
    /// helper, in a class nothing calls. That last case is exactly the mutation that walked past
    /// <c>Harness.RigWrite</c>'s whole suite.
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
            "harness-verify claims no write path exists in this binary. Found: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// The other half of the same claim, stated separately because it is a different fact: the assembly
    /// must not reach a socket DIRECTLY either, bypassing the wire stack and its measured
    /// <c>ModbusPolicy</c> altogether.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_TouchesSystemNetSockets()
    {
        var scan = Scan(ShippedAssembly, m => DeclaringName(m)?.StartsWith("System.Net.Sockets.", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "the assembly reaches a socket directly: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// 🔴 <b>THE TRANSITIVE HALF.</b> The member walk above catches a write NAMED here. It cannot catch one
    /// reached through a call to something that writes — and this binary references two assemblies that do.
    /// So the two entry points which can reach a write are asserted absent by name.
    /// </summary>
    [Theory]
    [InlineData("Harness.Loop.LoopRun", "Execute")]
    [InlineData("Harness.Run.LoopCli", "Run")]
    public void NothingInTheShippedAssembly_NamesAnEntryPointThatCanWrite(string type, string member)
    {
        var scan = Scan(ShippedAssembly, m => m is not Type && DeclaringName(m) == type && m.Name == member);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(
            scan.Hits.Count == 0,
            $"the assembly names {type}.{member}, which can reach a device write: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// The entry points named above must actually EXIST, or the theory is asserting the absence of
    /// something that was never there and would pass after a rename. <i>Assert an absence so that its
    /// future presence forces the real check</i> — and assert the subject of the absence too.
    /// </summary>
    [Fact]
    public void TheWritingEntryPointsThisBinaryAvoids_StillExist()
    {
        foreach (var (type, member) in WritingEntryPoints)
        {
            var resolved = typeof(Harness.Loop.LoopRun).Assembly.GetType(type)
                           ?? typeof(Harness.Run.LoopCli).Assembly.GetType(type);

            Assert.True(resolved is not null, $"{type} no longer exists; the absence asserted above is vacuous.");
            Assert.True(
                resolved!.GetMethod(member, BindingFlags.Public | BindingFlags.Static) is not null,
                $"{type}.{member} no longer exists; the absence asserted above is vacuous and would survive a rename.");
        }
    }

    // ---- 2. THE CONTROLS --------------------------------------------------------------------------

    /// <summary>
    /// *** THE LIVE POSITIVE CONTROL, AND THE REASON THE ASSERTION MEANS ANYTHING. *** The same walk with
    /// the same predicate, over an assembly that really does call the forbidden members, must FIND them.
    /// Without this, "no hits" is equally what a mistyped needle, or a namespace filter that excludes its
    /// own subject, produces.
    /// </summary>
    [Fact]
    public void TheWalk_FindsThePlantedWriteCalls()
    {
        var scan = Scan(ThisTestAssembly, IsWireWriteMember);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(PlantedWriteControl), StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The predicate fires on EVERY write verb, not merely on the first one it meets.</b> The walk stops
    /// at the first hit per method, so a single planted method proves only that one needle works — and
    /// <c>Commit</c>, <c>LowerAllStartBools</c> and <c>ClearStartEcho</c> are the three a naive
    /// "Write"-prefixed rule would miss entirely.
    /// </summary>
    [Theory]
    [InlineData("WriteHoldingRegisters")]
    [InlineData("WriteVector")]
    [InlineData("Commit")]
    [InlineData("LowerAllStartBools")]
    [InlineData("ClearStartEcho")]
    public void TheWalk_FindsEachWriteMemberIndividually(string member)
    {
        var scan = Scan(ThisTestAssembly, m =>
            m is not Type && m.Name == member &&
            WireNamespaces.Any(ns => DeclaringName(m)?.StartsWith(ns, StringComparison.Ordinal) == true));

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies.");
        Assert.True(scan.Hits.Count > 0, $"the walk cannot see {member} even in an assembly that calls it.");
    }

    /// <summary>
    /// *** THE RESOLUTION CONTROL, IN THE MODULE UNDER TEST. *** The planted control proves the predicate
    /// fires; it does not prove tokens resolve inside <c>Harness.Verify</c>'s own module, which is a
    /// per-module operation. <c>VerifyRun</c> certainly calls <c>MirrorClient.ReadControl</c> — that is the
    /// whole tool — so the walk must see it there.
    /// </summary>
    [Fact]
    public void TheWalk_ResolvesWireTokensInsideTheShippedAssembly()
    {
        var scan = Scan(ShippedAssembly, m =>
            m is not Type && m.Name == nameof(MirrorClient.ReadControl) &&
            DeclaringName(m) == "Harness.Wire.MirrorClient");

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(
            scan.Hits.Count > 0,
            "The walk found no reference to MirrorClient.ReadControl, which VerifyRun certainly makes. " +
            "The scanner cannot resolve tokens in this module, so every zero it reports above proves nothing.");
    }

    // ---- 3. THE RESIDUAL, PINNED ------------------------------------------------------------------

    /// <summary>
    /// <b>What the claim above does NOT cover, asserted so it cannot quietly stop being true.</b>
    ///
    /// <para><c>MirrorClient</c> DOES carry four writes — the harness proper must write vectors and raise
    /// start bools — and this binary holds one of those clients. The property checked is that nothing here
    /// NAMES those members, not that the capability has been deleted from the process.</para>
    ///
    /// <para>If <c>MirrorClient</c> ever loses them, this goes red and forces somebody to re-read the
    /// banner and the walk, rather than leaving a caveat in a document that has already stopped being true.
    /// A caveat in a report decays; a caveat that is a red test cannot.</para>
    /// </summary>
    [Theory]
    [InlineData(nameof(MirrorClient.WriteVector))]
    [InlineData(nameof(MirrorClient.Commit))]
    [InlineData(nameof(MirrorClient.LowerAllStartBools))]
    [InlineData(nameof(MirrorClient.ClearStartEcho))]
    public void TheResidualIsReal_MirrorClientStillCarriesTheWritesThisBinaryAvoids(string member)
    {
        Assert.True(
            typeof(MirrorClient).GetMethod(member) is not null,
            $"MirrorClient no longer has {member}. The structural claim in this file and the banner in VerifyRun both " +
            "describe a residual that no longer exists — re-read both before deleting this test.");
    }

    // ---- the walk ---------------------------------------------------------------------------------

    private static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    private static bool IsWireWriteMember(MemberInfo member)
    {
        // A bare TYPE token is not a write. This binary legitimately names Harness.Wire.MirrorClient —
        // conflating the two here would make the predicate fire on the tool's whole reason for existing.
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
    /// Deliberately coarse, for the reason <c>openness-cli</c>'s equivalent gives: every 4-byte window is
    /// offered to the token resolver rather than the IL being decoded precisely. That OVER-reports
    /// candidate tokens and UNDER-reports nothing, and since the assertion is that the count is ZERO,
    /// over-reporting is the safe direction — a false positive fails a test and gets read, a false negative
    /// would let the one thing this guards against through unnoticed.
    ///
    /// <para>⚠️ The direction reverses for the CONTROLS, which assert a count above zero: there a spurious
    /// token could make a broken walk look armed. Every control matches on a specific expected member
    /// rather than on "found something".</para>
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
