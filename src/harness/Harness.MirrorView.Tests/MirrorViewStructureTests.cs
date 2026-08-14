using System.Net;
using System.Reflection;
using Harness.Wire;

namespace Harness.MirrorView.Tests;

/// <summary>
/// THE STRUCTURAL CLAIMS BEHIND <c>harness-mirror-view</c>'s BANNER, CHECKED RATHER THAN DECLARED.
///
/// <para>This binary points at a physical PLC. <b>Read-only has to be a property of the ASSEMBLY, not
/// an intention</b> — and so does loopback-only. Both are claims about the compiled assembly, and the
/// only thing entitled to make one is a walk over the compiled assembly.</para>
///
/// <para><b>Three halves per claim, and none subsumes the others.</b>
/// <list type="number">
/// <item>A <b>denominator</b> — <c>BodiesExamined &gt; 0</c>. <c>Hits.Count == 0</c> is true of
/// <i>nothing found</i> and of <i>nothing looked at</i>, and only the count separates them.</item>
/// <item>A <b>live positive control</b> — the same walk, the same predicate, over an assembly that
/// really does contain the forbidden reference (<see cref="PlantedWriteControl"/>,
/// <see cref="PlantedBindAnyControl"/>). This proves the PREDICATE can fire.</item>
/// <item>A <b>resolution control in the target module</b> — the walk must find something the shipped
/// assembly certainly does reference. Without it, a module whose tokens resolve to nothing reports a
/// clean sweep.</item>
/// </list></para>
///
/// <para><b>The predicate is a parameter, never a literal inside the walk.</b> A check whose exercise
/// requires editing the check will not be exercised — that is how the equivalent control in
/// <c>openness-cli</c> decayed into a comment describing a manual run somebody once did.</para>
///
/// <para>This replaces a <c>const bool</c> with an <c>Assert.False</c> on it. Measured 2026-08-14 on
/// <c>Harness.RigWrite</c>: planting a class that constructed a live socket client left all 202 tests
/// green.</para>
/// </summary>
public class MirrorViewStructureTests
{
    private static Assembly ShippedAssembly => typeof(MirrorPoller).Assembly;

    private static Assembly ThisTestAssembly => typeof(PlantedWriteControl).Assembly;

    /// <summary>
    /// The namespaces whose write verbs are the hazard. Scoped, because <c>TextWriter.Write</c> is how
    /// this tool reports and a predicate that caught it would fire on every run — a gate firing outside
    /// its scope is noise, and noise gets switched off.
    /// </summary>
    private static readonly string[] WireNamespaces = { "Harness.Wire", "Harness.Map", "NModbus" };

    /// <summary>
    /// Member-name prefixes that move bytes towards the device, or towards a latch the harness owns.
    /// <c>Commit</c>, <c>LowerAll</c> and <c>ClearStart</c> are on the list because <c>MirrorClient</c>
    /// spells its writes that way and a "Write"-only rule would miss all three.
    /// </summary>
    private static readonly string[] WriteVerbs = { "Write", "Commit", "LowerAll", "ClearStart" };

    // ---- 1. NO WRITE PATH ---------------------------------------------------------------------------

    /// <summary>
    /// *** THE CLAIM. *** No method anywhere in the shipped assembly names a write member of the wire
    /// stack. It fails if such a reference appears ANYWHERE — in a branch no test exercises, in a private
    /// helper, in a class nothing calls. That last case is exactly the mutation that walked past
    /// <c>Harness.RigWrite</c>'s whole suite.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_NamesAWriteMemberOfTheWireStack()
    {
        var scan = Scan(ShippedAssembly, IsWireWriteMember);

        Assert.True(scan.BodiesExamined > 0,
            "The walk examined NO method bodies, so finding nothing proves nothing. " +
            $"Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(scan.Hits.Count == 0,
            "harness-mirror-view claims no write path exists in this binary. Found: " + string.Join("; ", scan.Hits));
    }

    /// <summary>The other half: the assembly must not reach a socket directly, bypassing the wire stack.</summary>
    [Fact]
    public void NothingInTheShippedAssembly_TouchesSystemNetSockets()
    {
        var scan = Scan(ShippedAssembly, m => DeclaringName(m)?.StartsWith("System.Net.Sockets.", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "the assembly reaches a socket directly: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// <c>MirrorClient</c> is the write-capable client — vectors, the start-bool commit, the echo clear.
    /// A viewer has no business holding one, and not naming the TYPE at all is stronger and cheaper to
    /// check than not naming its individual write members.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_TouchesTheWriteCapableMirrorClient()
    {
        var scan = Scan(ShippedAssembly, m => DeclaringName(m) == "Harness.Wire.MirrorClient");

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "the assembly holds the write-capable client: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// The narrowed handle is the mechanism, so assert the mechanism: the read port this binary is given
    /// carries exactly one verb. A <c>WriteHoldingRegisters</c> appearing on <c>IRegisterSource</c> would
    /// hand every call site the capability back without a single line of this project changing.
    /// </summary>
    [Fact]
    public void TheOnlyWireHandleThisBinaryHolds_CarriesNoWriteVerb()
    {
        var members = typeof(Harness.MirrorRead.IRegisterSource)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(new[] { nameof(Harness.MirrorRead.IRegisterSource.Read) }, members);
    }

    // ---- 2. LOOPBACK ONLY ---------------------------------------------------------------------------

    /// <summary>
    /// *** A VIEW OF A LIVE CONTROLLER MUST NOT BE REACHABLE FROM A NETWORK INTERFACE. *** Asserted
    /// structurally: the two addresses that would expose it appear nowhere in the binary, so exposing it
    /// is not one careless edit away — it is a change this test fails on.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_BindsAnythingButLoopback()
    {
        var scan = Scan(ShippedAssembly, IsWildcardBindAddress);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0,
            "the viewer names a wildcard bind address, which would serve a live controller's data on a " +
            "network interface: " + string.Join("; ", scan.Hits));
    }

    /// <summary>The resolution control for the bind claim: it certainly DOES name the loopback addresses.</summary>
    [Fact]
    public void TheShippedAssembly_NamesTheLoopbackAddresses()
    {
        var scan = Scan(ShippedAssembly, m =>
            DeclaringName(m) == "System.Net.IPAddress" &&
            m.Name is nameof(IPAddress.Loopback) or nameof(IPAddress.IPv6Loopback));

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count > 0,
            "The walk found no reference to IPAddress.Loopback, which Program certainly makes. The " +
            "scanner cannot resolve System.Net tokens in this module, so the wildcard-bind zero above " +
            "proves nothing.");
    }

    // ---- 3. THE CONTROLS ----------------------------------------------------------------------------

    /// <summary>
    /// *** THE LIVE POSITIVE CONTROL FOR THE WRITE WALK. *** The same walk with the same predicate, run
    /// over an assembly that really does call <c>WriteHoldingRegisters</c>, must FIND it. Without this,
    /// "no hits" is equally what a mistyped needle or a namespace filter excluding its own subject
    /// produces — and both are the most reassuring output this file could emit.
    /// </summary>
    [Fact]
    public void TheWalk_FindsAPlantedWriteCall()
    {
        var scan = Scan(ThisTestAssembly, IsWireWriteMember);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(PlantedWriteControl), StringComparison.Ordinal));
    }

    /// <summary>*** THE LIVE POSITIVE CONTROL FOR THE BIND WALK. *** Same argument, different predicate.</summary>
    [Fact]
    public void TheWalk_FindsAPlantedWildcardBind()
    {
        var scan = Scan(ThisTestAssembly, IsWildcardBindAddress);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(PlantedBindAnyControl), StringComparison.Ordinal));
    }

    /// <summary>
    /// *** THE RESOLUTION CONTROL IN THE MODULE UNDER TEST. *** The planted controls prove the predicates
    /// fire; they do not prove tokens resolve inside <c>Harness.MirrorView</c>'s own module, which is a
    /// per-module operation. <c>RegisterDecode</c> certainly calls <c>Harness.Wire.RegisterWords.To32</c>,
    /// so the walk must see it there.
    /// </summary>
    [Fact]
    public void TheWalk_ResolvesWireTokensInsideTheShippedAssembly()
    {
        var scan = Scan(ShippedAssembly, m =>
            m is not Type && m.Name == nameof(RegisterWords.To32) &&
            DeclaringName(m)?.StartsWith("Harness.Wire", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count > 0,
            "The walk found no reference to Harness.Wire's RegisterWords.To32, which RegisterDecode " +
            "certainly makes. The scanner cannot resolve tokens in this module, so every zero it reports " +
            "above proves nothing.");
    }

    // ---- 4. THE RESIDUAL, PINNED --------------------------------------------------------------------

    /// <summary>
    /// <b>What the claims above do NOT cover, asserted so it cannot quietly stop being true.</b>
    ///
    /// <para><c>Harness.Wire</c> DOES contain a write — the harness proper must write vectors and raise
    /// start bools — and this binary references that assembly transitively. The property checked is that
    /// nothing here NAMES it, not that the capability has been deleted from the process.</para>
    ///
    /// <para>This test pins the residual by asserting the write is still there. If <c>Harness.Wire</c>
    /// ever loses it, this goes red and forces somebody to re-read the banner and the walk, rather than
    /// leaving a caveat in a document that has already stopped being true. <b>A caveat in a report
    /// decays; a caveat that is a red test cannot.</b></para>
    /// </summary>
    [Fact]
    public void TheResidualIsReal_HarnessWireStillCarriesTheWriteThisBinaryAvoids()
    {
        var write = typeof(IRegisterTransport).GetMethod(nameof(IRegisterTransport.WriteHoldingRegisters));

        Assert.True(write is not null,
            "Harness.Wire.IRegisterTransport no longer has WriteHoldingRegisters. The structural claim in " +
            "this file and the banner in Program both describe a residual that no longer exists — re-read " +
            "both before deleting this test.");
    }

    /// <summary>
    /// <b>The page carries no map of its own.</b> A fallback table baked into the HTML would render
    /// something plausible while the API was refusing — which is the whole failure this lane exists to
    /// prevent, one layer up from the map loader that refuses to invent one.
    /// </summary>
    [Fact]
    public void ThePageMarkup_CarriesNoTagNamesAndNoRegisterValues()
    {
        var map = Fixtures.Map();

        Assert.NotEmpty(map.Tags);
        foreach (var tag in map.Tags)
            Assert.DoesNotContain(tag.Name, MirrorPage.Html, StringComparison.Ordinal);

        Assert.DoesNotContain("16#", MirrorPage.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("%MW", MirrorPage.Html, StringComparison.Ordinal);
    }

    // ---- the walk -----------------------------------------------------------------------------------

    private static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    private static bool IsWireWriteMember(MemberInfo member)
    {
        // A bare TYPE token is not a write. Naming Harness.Wire.MirrorClient has its own test above;
        // conflating them here would make this predicate fire on ordinary type references and turn the
        // assertion into noise.
        if (member is Type) return false;

        var declaring = DeclaringName(member);
        if (declaring is null) return false;
        if (!WireNamespaces.Any(ns => declaring.StartsWith(ns, StringComparison.Ordinal))) return false;

        return WriteVerbs.Any(verb => member.Name.StartsWith(verb, StringComparison.Ordinal));
    }

    private static bool IsWildcardBindAddress(MemberInfo member) =>
        member is not Type &&
        DeclaringName(member) == "System.Net.IPAddress" &&
        member.Name is nameof(IPAddress.Any) or nameof(IPAddress.IPv6Any);

    private static IlScan Scan(Assembly assembly, Func<MemberInfo, bool> forbidden)
    {
        var hits = new List<string>();
        var types = 0;
        var methods = 0;
        var bodies = 0;

        Type[] loadable;
        try
        {
            loadable = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Reported, never absorbed: a type that would not load is a slice of the assembly this walk
            // did NOT examine, and a clean sweep over the remainder would be a green with an unstated
            // denominator.
            var unloadable = ex.LoaderExceptions.Length;
            Assert.Fail($"{unloadable} type(s) in {assembly.GetName().Name} could not be loaded, so the walk " +
                        $"cannot claim to have examined the assembly: " +
                        string.Join("; ", ex.LoaderExceptions.Select(e => e?.Message)));
            return new IlScan(hits, 0, 0, 0);
        }

        foreach (var type in loadable)
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
    /// candidate tokens and UNDER-reports nothing, and since the assertions are that a count is ZERO,
    /// over-reporting is the safe direction — a false positive fails a test and gets read, a false
    /// negative lets the one thing this guards against through unnoticed.
    ///
    /// <para>⚠️ The direction reverses for the CONTROLS, which assert a count above zero: there a
    /// spurious token could make a broken walk look armed. Every control matches on a specific expected
    /// member rather than on "found something".</para>
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
