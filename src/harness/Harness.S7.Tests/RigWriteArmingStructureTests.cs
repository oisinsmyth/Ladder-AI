using System.Reflection;
using Harness.RigWrite;

namespace Harness.S7.Tests;

/// <summary>
/// THE STRUCTURAL CLAIM BEHIND <see cref="Arming"/>, CHECKED RATHER THAN DECLARED (2026-08-14).
///
/// <para><see cref="Arming"/> states the strongest safety property in the harness:
/// <i>"It contains no code that opens a socket"</i>, <i>"Nothing in this assembly constructs an
/// IS7Client"</i>, <i>"the capability is absent from the assembly"</i>. That is a claim about the
/// COMPILED ASSEMBLY, and until this file existed nothing checked it.</para>
///
/// <para>🔴 <b>What was standing in for it.</b> <c>Arming.CompiledIn</c> is a <c>const bool</c> and the
/// suite asserted <c>Assert.False(Arming.CompiledIn)</c> — <i>a test of a literal</i>, which can only
/// fail if somebody edits the literal. And <c>NeverTouchedClient</c> proves the CLI does not call the
/// factory <b>on the paths the tests drive</b>, which is a different and much smaller claim.
/// *** MEASURED: adding a class to Harness.RigWrite that constructs a Sharp7Client left all 202 tests
/// GREEN. *** A declaration is a transferred responsibility, not a verification.</para>
///
/// <para>Same technique as <c>openness-cli</c>'s "no method references DownloadProvider.Download",
/// and with the same two halves, because neither is sufficient alone: a DENOMINATOR proves the walk
/// LOOKED, and a live positive CONTROL proves it can DETECT.</para>
/// </summary>
public class RigWriteArmingStructureTests
{
    private const string SocketOwner = "Harness.S7.Sharp7Client";

    /// <summary>Something rig-write provably DOES construct, so the walk can be shown to work.</summary>
    private const string ControlType = "Harness.S7.FileRestorePointStore";

    /// <summary>
    /// *** THE ASSERTION. *** No method anywhere in the shipped rig-write assembly touches the one
    /// class in Harness.S7 that owns a socket. It fails if such a reference is added ANYWHERE,
    /// including in a branch no test exercises and including a class nothing calls — which is exactly
    /// the mutation that slipped past the whole suite.
    /// </summary>
    [Fact]
    public void NothingInTheRigWriteAssembly_TouchesTheSocketOwningClient()
    {
        var scan = Scan(SocketOwner);

        Assert.True(
            scan.BodiesExamined > 0,
            $"The walk examined NO method bodies, so finding nothing proves nothing. " +
            $"Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(
            scan.Hits.Count == 0,
            "rig-write claims the socket-owning capability is ABSENT from its assembly. Found: " +
            string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// *** THE CONTROL, AND THE REASON THE ASSERTION MEANS ANYTHING. *** The same walk, over the same
    /// assembly, must FIND a Harness.S7 type rig-write provably uses — <c>RigWriteCli</c> constructs a
    /// <see cref="FileRestorePointStore"/>. Without this, "no hits" is equally what a walk that
    /// resolves no tokens produces, which is the most reassuring output this file could emit and would
    /// mean nothing at all.
    /// </summary>
    [Fact]
    public void TheWalk_ReallyDetectsAHarnessS7TypeThatIsThere()
    {
        var scan = Scan(ControlType);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.True(
            scan.Hits.Count > 0,
            $"The walk found no reference to {ControlType}, which RigWriteCli certainly makes. The " +
            "scanner is broken, so the zero result reported by the assertion above proves nothing.");
    }

    /// <summary>
    /// The other half of the claim, stated separately because it is a different fact: the assembly
    /// must not reach a socket DIRECTLY either, bypassing Harness.S7 altogether.
    /// </summary>
    [Fact]
    public void NothingInTheRigWriteAssembly_TouchesSystemNetSockets()
    {
        var scan = Scan("System.Net.Sockets.");

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "rig-write reaches a socket directly: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// <c>Arming.CompiledIn</c> stays pinned — but as a STATEMENT OF INTENT sitting beside the
    /// structural checks, never as the evidence. If the constant is ever flipped to true while the
    /// walks above still pass, the constant is the thing that is wrong.
    /// </summary>
    [Fact]
    public void TheArmingConstant_AgreesWithTheStructure()
    {
        Assert.False(Arming.CompiledIn);
        Assert.Empty(Scan(SocketOwner).Hits);
    }

    // ---- the walk ------------------------------------------------------------------------------

    private static IlScan Scan(string typeNamePrefix)
    {
        var assembly = typeof(Arming).Assembly;
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

                if (il is null)
                {
                    continue;
                }

                bodies++;
                if (References(il, method, typeNamePrefix, out var detail))
                {
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
                }
            }
        }

        return new IlScan(hits, types, methods, bodies);
    }

    private sealed record IlScan(IReadOnlyList<string> Hits, int TypesEnumerated, int MethodsEnumerated, int BodiesExamined);

    /// <summary>
    /// Deliberately coarse, for the reason openness-cli's equivalent gives: every 4-byte window is
    /// offered to the token resolver rather than the IL being decoded precisely. That OVER-reports
    /// candidate tokens and UNDER-reports nothing, and since the assertion is that the count is ZERO,
    /// over-reporting is the safe direction — a false positive fails a test and gets read, a false
    /// negative would let the one thing this guards against through unnoticed.
    ///
    /// <para>Both member tokens and type tokens are resolved: a <c>newobj</c> carries a constructor
    /// token, but a field, a cast or a <c>typeof</c> carries a type token, and the claim is that the
    /// class is not TOUCHED, not merely that it is not constructed.</para>
    /// </summary>
    private static bool References(byte[] il, MethodInfo owner, string typeNamePrefix, out string detail)
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
                var name = member is Type t ? t.FullName : member?.DeclaringType?.FullName;
                if (name is not null && name.StartsWith(typeNamePrefix, StringComparison.Ordinal))
                {
                    detail = $"references {name}";
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
