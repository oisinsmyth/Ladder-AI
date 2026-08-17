using System.Reflection;

namespace Harness.Cleanup.Tests;

/// <summary>
/// <b>THE BANNER'S CLAIM, CHECKED RATHER THAN DECLARED.</b>
///
/// <para><c>harness-cleanup</c> prints <i>"this binary cannot delete"</i> on every run. That is a claim
/// about the COMPILED ASSEMBLY, and the only thing entitled to make it is a walk over the compiled
/// assembly. A <c>const bool</c> with an <c>Assert.False</c> on it is what this replaces: measured
/// 2026-08-14 on <c>Harness.RigWrite</c>, planting a class that constructed a live socket client left
/// all 202 tests green, because a constant stays true exactly as long as somebody remembers to change
/// it.</para>
///
/// <para><b>Three halves, none of which subsumes the others:</b> a DENOMINATOR (bodies examined &gt; 0,
/// because <c>Hits.Count == 0</c> is true of <i>nothing found</i> and of <i>nothing looked at</i>); a
/// LIVE POSITIVE CONTROL (<see cref="PlantedDestructiveControl"/>, which proves the predicate can fire);
/// and a RESOLUTION CONTROL inside the module under test (which proves tokens resolve there at all).
/// </para>
///
/// <para><b>The predicate is a parameter, never a literal in the walk</b> — a check whose exercise
/// requires editing the check will not be exercised, which is how <c>openness-cli</c>'s equivalent
/// decayed into a comment describing a manual run somebody once did.</para>
/// </summary>
public class CleanupStructureTests
{
    private static Assembly ShippedAssembly => typeof(CleanupRun).Assembly;

    private static Assembly ThisTestAssembly => typeof(PlantedDestructiveControl).Assembly;

    /// <summary>
    /// The members that destroy or launch. <c>File.Delete</c>/<c>Directory.Delete</c>/<c>File.Move</c>
    /// and every <c>Write*</c> on <c>System.IO.File</c> — the tool reads inputs and writes to stdout, so
    /// it has no business writing a file either. <c>Process</c> is on the list because the cheapest way
    /// to acquire a delete would be to shell out to <c>openness-cli</c>.
    /// </summary>
    private static bool IsDestructiveOrLaunching(MemberInfo member)
    {
        if (member is Type) return false;

        var declaring = member is Type t ? t.FullName : member.DeclaringType?.FullName;
        if (declaring is null) return false;

        if (declaring is "System.IO.File" or "System.IO.Directory")
            return member.Name is "Delete" or "Move" or "Replace"
                || member.Name.StartsWith("Write", StringComparison.Ordinal)
                || member.Name.StartsWith("Append", StringComparison.Ordinal)
                || member.Name is "Create" or "CreateText";

        return declaring.StartsWith("System.Diagnostics.Process", StringComparison.Ordinal);
    }

    // ---- 1. THE ASSERTION -----------------------------------------------------------------------

    [Fact]
    public void NothingInTheShippedAssembly_DeletesWritesOrLaunches()
    {
        var scan = Scan(ShippedAssembly, IsDestructiveOrLaunching);

        Assert.True(
            scan.BodiesExamined > 0,
            "The walk examined NO method bodies, so finding nothing proves nothing. "
            + $"Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(
            scan.Hits.Count == 0,
            "harness-cleanup claims it cannot delete and does not shell out. Found: " + string.Join("; ", scan.Hits));
    }

    // ---- 2. THE CONTROLS ------------------------------------------------------------------------

    [Fact]
    public void TheWalk_FindsAPlantedDeleteAndAPlantedProcessLaunch()
    {
        // The same walk, same predicate, over an assembly that really does both. Without this, "no hits"
        // is equally what a mistyped needle or a resolver that never runs would produce.
        var scan = Scan(ThisTestAssembly, IsDestructiveOrLaunching);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, h => h.Contains(nameof(PlantedDestructiveControl.DeleteSomething), StringComparison.Ordinal));
        Assert.Contains(scan.Hits, h => h.Contains(nameof(PlantedDestructiveControl.LaunchSomething), StringComparison.Ordinal));
    }

    [Fact]
    public void TheWalk_ResolvesSystemIoTokensInsideTheShippedAssembly()
    {
        // The planted control proves the predicate fires; it does not prove tokens resolve inside
        // Harness.Cleanup's own module, which is a per-module operation. CorpusScan certainly calls
        // File.ReadAllText, so the walk must see it there.
        var scan = Scan(ShippedAssembly, m =>
            m is not Type && m.Name == "ReadAllText" && (m.DeclaringType?.FullName == "System.IO.File"));

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(
            scan.Hits.Count > 0,
            "The walk found no reference to File.ReadAllText, which CorpusScan certainly makes. The scanner "
            + "cannot resolve tokens in this module, so every zero it reports above proves nothing.");
    }

    // ---- 3. THE RESIDUAL, PINNED ----------------------------------------------------------------

    [Fact]
    public void TheResidualIsReal_TheDeleteVerbStillExistsElsewhereAndThisToolMerelyDeclinesToHoldIt()
    {
        // What the claim above does NOT cover: deletion has not been abolished, it lives in
        // `openness-cli delete`. The plan text must keep naming it, or the tool has quietly become a
        // report with no executable consequence and nobody would notice.
        using var corpus = new TempCorpus();
        corpus.Fc("FC_Orphan", 9098);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(new CleanupOptions(
            corpus.Ir, xc, new[] { corpus.File_("v.json", "{}") }, corpus.DrainedReport(),
            "lane-c", null, Array.Empty<string>()));

        Assert.Contains("openness-cli delete", outcome.Text, StringComparison.Ordinal);
    }

    // ---- the walk -------------------------------------------------------------------------------

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
    /// Deliberately coarse: every 4-byte window is offered to the token resolver rather than the IL being
    /// decoded precisely. That OVER-reports candidate tokens and UNDER-reports nothing, and since the
    /// assertion is that the count is ZERO, over-reporting is the safe direction.
    ///
    /// <para>⚠️ The direction reverses for the CONTROLS, which assert a count above zero — so both match
    /// on a SPECIFIC expected member rather than on "found something".</para>
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
                    detail = $"references {(member is Type mt ? mt.FullName : member.DeclaringType?.FullName)}.{member.Name}";
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
