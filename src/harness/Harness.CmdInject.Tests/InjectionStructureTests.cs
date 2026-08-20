using System.Reflection;
using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// THE STRUCTURAL CLAIMS BEHIND <c>harness-cmd-inject</c>, CHECKED RATHER THAN DECLARED — the INVERSE of
/// <c>Harness.MirrorRead.Tests.MirrorReadStructureTests</c>.
///
/// <para>MirrorRead asserts the write member is named ZERO times, because it is read-only. This tool WRITES,
/// so the assertion flips: <see cref="IInjectionTransport.WriteRegisters"/> must be named by <b>exactly
/// one</b> method — a single chokepoint (<c>InjectionDispatch.ApplyEach</c>) — so the split write and the
/// band containment are properties of the assembly, not of every future call site.</para>
///
/// <para>🔺 <b>TWO OF THESE WALKS CHANGED WHEN THE PORT LANDED, AND ONE OF THEM DID NOT.</b> Phase 1 could
/// not open a socket at all, so it asserted ZERO references to a write member of the wire stack. This build
/// has an adapter, so a zero would be a test asserting the tool does not work. The claim is therefore moved
/// up a level rather than deleted: <b>exactly one</b> method names the wire stack's write, and <b>exactly
/// one</b> names <c>NModbusTransport.Connect</c> — so there is one place the calibrated
/// <c>ModbusPolicy</c> (<c>Retries = 0</c>) could be got wrong, and one place a socket can come from.
/// The walk that did NOT change is the one over <c>System.Net.Sockets</c>: this assembly still names no
/// socket type of its own, because <c>Harness.Wire</c> owns every one of them.</para>
///
/// <para><b>Every walk carries a denominator (bodies examined &gt; 0), a live positive control, and a
/// resolution control in the target module.</b> The reason is on the record: a <c>const bool</c> plus an
/// <c>Assert.False</c> let a live socket client sit inside a "cannot write" binary behind 202 green tests,
/// because a constant stays true exactly as long as somebody remembers to change it. A count that is not
/// grounded by a positive control is the same failure one level up.</para>
/// </summary>
public class InjectionStructureTests
{
    private static Assembly ShippedAssembly => typeof(InjectionDispatch).Assembly;

    private static Assembly ThisTestAssembly => typeof(PlantedInjectionWrite).Assembly;

    private const string TransportType = "Harness.CmdInject.IInjectionTransport";

    private static readonly string[] WireNamespaces = { "Harness.Wire", "Harness.Map", "NModbus" };
    private static readonly string[] WriteVerbs = { "Write", "Commit", "LowerAll", "ClearStart" };

    // ---- 1. EXACTLY ONE method names the transport's write member --------------------------------

    [Fact]
    public void ExactlyOneMethodInTheShippedAssembly_NamesTheTransportWriteMember()
    {
        var scan = Scan(ShippedAssembly, IsTransportWrite);

        Assert.True(scan.BodiesExamined > 0,
            $"The walk examined NO method bodies, so any count proves nothing. Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(scan.Hits.Count == 1,
            "harness-cmd-inject must funnel every write through exactly one method (InjectionDispatch.Apply). " +
            $"Found {scan.Hits.Count}: {string.Join("; ", scan.Hits)}");

        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(InjectionDispatch), StringComparison.Ordinal));
    }

    // ---- 2. the socket, and the wire stack's own write, each have exactly one home ---------------

    [Fact]
    public void NothingInTheShippedAssembly_TouchesSystemNetSockets()
    {
        // UNCHANGED FROM PHASE 1, and it must stay a zero: Harness.Wire owns every socket type, and the
        // moment this assembly names one directly it has grown a second, uncalibrated client.
        var scan = Scan(ShippedAssembly, m => DeclaringName(m)?.StartsWith("System.Net.Sockets.", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 0, "the assembly reaches a socket directly: " + string.Join("; ", scan.Hits));
    }

    [Fact]
    public void ExactlyOneMethodInTheShippedAssembly_NamesAWriteMemberOfTheWireStack()
    {
        var scan = Scan(ShippedAssembly, IsWireWriteMember);

        Assert.True(scan.BodiesExamined > 0,
            $"The walk examined NO method bodies, so any count proves nothing. Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(scan.Hits.Count == 1,
            "exactly one method may reach the wire stack's write (ModbusInjectionTransport.WriteRegisters). More than one means " +
            "a second path to the wire that the chokepoint above does not govern; none means the tool cannot write at all. " +
            $"Found {scan.Hits.Count}: {string.Join("; ", scan.Hits)}");

        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(ModbusInjectionTransport), StringComparison.Ordinal));
    }

    [Fact]
    public void ExactlyOneMethodInTheShippedAssembly_ConstructsATransportToTheWire()
    {
        // ONE place NModbusTransport.Connect is called, so there is ONE place the calibrated ModbusPolicy —
        // Retries = 0, which is NOT the library default — could be got wrong. A retried write after a timeout
        // can duplicate a command whose original still landed, which at this end is indistinguishable from the
        // first having been lost.
        var scan = Scan(ShippedAssembly, m =>
            m is not Type && m.Name == "Connect" &&
            DeclaringName(m)?.StartsWith("Harness.Wire.NModbusTransport", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count == 1,
            "exactly one method may open a Modbus session (ModbusInjectionTransport.Open). " +
            $"Found {scan.Hits.Count}: {string.Join("; ", scan.Hits)}");

        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(ModbusInjectionTransport), StringComparison.Ordinal));
    }

    [Fact]
    public void TheOpenedTransport_CarriesTheCalibratedPolicy()
    {
        // The structural walk proves there is one construction site; this proves what that site would apply.
        // Reading the policy off ModbusPolicy.Default is what ModbusInjectionTransport.Open does, and the one
        // value that is load-bearing and not the library's own default is asserted by name.
        Assert.Equal(0, Harness.Wire.ModbusPolicy.Default.Retries);
        Assert.Empty(Harness.Wire.ModbusPolicy.Default.Refusals);
    }

    // ---- the controls ----------------------------------------------------------------------------

    [Fact]
    public void TheWalk_FindsAPlantedTransportWrite()
    {
        // The same predicate over the test assembly, which really does call WriteRegisters, must find it —
        // this proves the PREDICATE fires, not merely that the resolver runs.
        var scan = Scan(ThisTestAssembly, IsTransportWrite);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(PlantedInjectionWrite), StringComparison.Ordinal));
    }

    [Fact]
    public void TheWalk_ResolvesTokensInsideTheShippedAssembly()
    {
        // CommandFrameBuilder certainly calls Harness.Map.MirrorValueFit.Check. If the walk cannot see that,
        // it cannot resolve tokens in this module and every zero it reports proves nothing.
        var scan = Scan(ShippedAssembly, m =>
            m is not Type && m.Name == "Check" &&
            DeclaringName(m)?.StartsWith("Harness.Map.MirrorValueFit", StringComparison.Ordinal) == true);

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count > 0,
            "the walk found no reference to Harness.Map.MirrorValueFit.Check, which CommandFrameBuilder certainly makes. " +
            "The scanner cannot resolve tokens in this module, so every count it reports proves nothing.");
    }

    // ---- 3. the write-target type: no public ctor, no factory taking a bare register -------------

    [Fact]
    public void InjectionWriteTarget_HasNoPublicConstructor()
    {
        var ctors = typeof(InjectionWriteTarget).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(ctors.Length == 0,
            "InjectionWriteTarget has a public constructor: a bare-register write target could be built directly, " +
            "which is the exact hole the private ctor + role factories exist to close.");
    }

    [Fact]
    public void InjectionWriteTarget_HasNoPublicStaticFactoryTakingABareRegister()
    {
        var integerTypes = new[] { typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(long), typeof(byte) };

        foreach (var method in typeof(InjectionWriteTarget).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var parameter in method.GetParameters())
            {
                Assert.False(integerTypes.Contains(parameter.ParameterType),
                    $"InjectionWriteTarget.{method.Name} takes a bare integer parameter '{parameter.Name}'. A factory that " +
                    "accepts a register index reintroduces the unaddressable-write hole — every factory must name a role and " +
                    "derive the register from the resolved binding.");
            }
        }
    }

    // ---- the walk --------------------------------------------------------------------------------

    private static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    private static bool IsTransportWrite(MemberInfo member)
    {
        if (member is Type) return false;
        return member.Name == nameof(IInjectionTransport.WriteRegisters) && DeclaringName(member) == TransportType;
    }

    private static bool IsWireWriteMember(MemberInfo member)
    {
        if (member is Type) return false;

        var declaring = DeclaringName(member);
        if (declaring is null) return false;
        if (!WireNamespaces.Any(ns => declaring.StartsWith(ns, StringComparison.Ordinal))) return false;

        return WriteVerbs.Any(verb => member.Name.StartsWith(verb, StringComparison.Ordinal));
    }

    private static IlScan Scan(Assembly assembly, Func<MemberInfo, bool> predicate)
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
                if (References(il, method, predicate, out var detail))
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
            }
        }

        return new IlScan(hits, types, methods, bodies);
    }

    private sealed record IlScan(IReadOnlyList<string> Hits, int TypesEnumerated, int MethodsEnumerated, int BodiesExamined);

    private static bool References(byte[] il, MethodInfo owner, Func<MemberInfo, bool> predicate, out string detail)
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
                if (member is not null && predicate(member))
                {
                    detail = $"references {DeclaringName(member)}.{member.Name}";
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a member token. Expected constantly — the walk offers every 4-byte window to the resolver.
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
