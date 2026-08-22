using System.Reflection;
using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// F-1's code half — <b>a read never SPLITS a slot</b>, and the enforcement is the shape of the call.
///
/// <para><b>These tests are written to be able to fail.</b> A whole-slot rule exercised only against
/// maps that already satisfy it proves nothing: the interesting question is whether a wrong derivation
/// or a new overload could produce a split read, so the checks below inspect the register ranges that
/// actually reached the transport, and the public surface that could name one.</para>
/// </summary>
public class WholeSlotReadTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;

    private static RegisterMap Map(int slots, int result, int vector = 2) =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", vector, result)).ToArray())).Require();

    private static (MirrorClient Client, RecordingTransport Wire) Wired(RegisterMap map)
    {
        var wire = new RecordingTransport(map, Stamp);
        return (new MirrorClient(map, wire, Stamp), wire);
    }

    /// <summary>Every FC03 that touched the result block, as (offset from the block, length).</summary>
    private static IEnumerable<(int Offset, int Length)> ResultReads(RegisterMap map, RecordingTransport wire) =>
        wire.Log
            .Where(t => !t.IsWrite && t.StartRegister >= map.ResultBlock.Register && t.StartRegister < map.ResultBlock.End)
            .Select(t => (t.StartRegister - map.ResultBlock.Register, t.Count));

    // ---------------------------------------------------------------------------------------------
    // The arithmetic
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 125)]
    [InlineData(12, 10)]
    [InlineData(20, 6)]
    [InlineData(25, 5)]
    [InlineData(62, 2)]
    [InlineData(63, 1)]
    [InlineData(123, 1)]
    [InlineData(125, 1)]
    public void R_is_floor_125_over_the_slot_size(int slotSize, int expected)
    {
        Assert.Equal(expected, Map(1, slotSize).SlotsPerRead);
    }

    [Fact]
    public void A_read_covering_several_whole_slots_is_ONE_transaction()
    {
        var map = Map(slots: 6, result: 20);
        var (client, wire) = Wired(map);

        var results = client.ReadResults(new SlotSpan(0, 6));

        Assert.Equal(6, results.Count);
        var read = Assert.Single(ResultReads(map, wire));
        Assert.Equal(0, read.Offset);
        Assert.Equal(120, read.Length);
    }

    [Fact]
    public void The_result_is_PER_SLOT_so_a_caller_cannot_receive_half_a_slot_either()
    {
        // Both ends of the call are in slot units. A flat register array would let a caller slice it
        // wrongly and would put the split back within reach on the way out.
        var map = Map(slots: 3, result: 4);
        var (client, wire) = Wired(map);
        wire.SetResult(1, 2, 77);

        var results = client.ReadResults(new SlotSpan(0, 3));

        Assert.All(results, r => Assert.Equal(map.ResultRegistersPerSlot, r.Length));
        Assert.Equal(77, results[1][2]);
    }

    [Fact]
    public void Reading_many_slots_uses_the_fewest_whole_slot_transactions_the_map_allows()
    {
        var map = Map(slots: 13, result: 25);   // R = 5
        var (client, wire) = Wired(map);

        var results = client.ReadResults(Enumerable.Range(0, 13));

        Assert.Equal(13, results.Count);
        Assert.Equal(3, ResultReads(map, wire).Count());   // ceil(13 / 5)
    }

    // ---------------------------------------------------------------------------------------------
    // THE PROPERTY, CHECKED AT THE WIRE — this is the one that can catch a wrong derivation
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void EVERY_read_the_client_issues_starts_and_ends_on_a_slot_boundary()
    {
        // Walked over a spread of shapes and a spread of requested subsets, and asserted against the
        // ranges that ACTUALLY REACHED THE TRANSPORT rather than against the map's own arithmetic — a
        // check derived from the same expression it is checking would agree with it by construction.
        foreach (var (slots, result) in new[] { (1, 1), (2, 2), (6, 20), (13, 25), (5, 62), (4, 123), (3, 125) })
        {
            var map = Map(slots, result);
            var (client, wire) = Wired(map);

            foreach (var subset in Subsets(slots))
            {
                client.ReadResults(subset);

                foreach (var (offset, length) in ResultReads(map, wire))
                {
                    Assert.Equal(0, offset % map.ResultRegistersPerSlot);
                    Assert.Equal(0, length % map.ResultRegistersPerSlot);
                    Assert.True(length > 0 && length <= ModbusLimits.MaxReadRegisters,
                        $"a read of {length} register(s) is not a legal FC03.");
                    Assert.True(offset + length <= map.ResultBlock.Length,
                        $"a read at +{offset} for {length} runs past the result block.");
                }

                wire.Log.Clear();
            }
        }
    }

    [Fact]
    public void Every_requested_slot_is_covered_and_no_read_is_issued_for_a_slot_nobody_asked_about()
    {
        // A group may cover a slot nobody asked about — that rides along free inside a transaction that
        // was happening anyway — but no read may exist that covers NO requested slot.
        var map = Map(slots: 13, result: 25);

        foreach (var subset in Subsets(13))
        {
            var plan = map.ReadPlan(subset);

            Assert.All(subset, s => Assert.Contains(plan, run => s >= run.FirstSlot && s < run.End));
            Assert.All(plan, run => Assert.Contains(subset, s => s >= run.FirstSlot && s < run.End));
            Assert.All(plan, run => Assert.True(run.SlotCount <= map.SlotsPerRead));
            Assert.All(plan, run => Assert.True(run.End <= map.Slots.Count));
        }
    }

    private static IEnumerable<int[]> Subsets(int slots)
    {
        yield return Enumerable.Range(0, slots).ToArray();
        yield return new[] { 0 };
        yield return new[] { slots - 1 };
        yield return Enumerable.Range(0, slots).Where(i => i % 2 == 0).ToArray();
        yield return Enumerable.Range(0, slots).Where(i => i % 3 == 0).ToArray();
        yield return Enumerable.Range(0, slots).Reverse().ToArray();
    }

    // ---------------------------------------------------------------------------------------------
    // Refusals: a span of too MANY whole slots is checked; a span of PART of one cannot be written
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_span_wider_than_one_FC03_is_refused_and_names_R()
    {
        var map = Map(slots: 6, result: 25);   // R = 5

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => map.ResultRead(new SlotSpan(0, 6)));

        Assert.Contains("at most 5 whole slot(s)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_span_running_off_the_end_of_the_map_is_refused()
    {
        var map = Map(slots: 3, result: 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.ResultRead(new SlotSpan(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ResultRead(new SlotSpan(-1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ResultRead(new SlotSpan(0, 0)));
    }

    [Fact]
    public void The_derived_range_is_exactly_the_slots_it_names()
    {
        var map = Map(slots: 6, result: 20);

        for (var first = 0; first < 6; first++)
        {
            for (var count = 1; count + first <= 6 && count <= map.SlotsPerRead; count++)
            {
                var range = map.ResultRead(new SlotSpan(first, count));

                Assert.Equal(map.Slots[first].Result.Register, range.Register);
                Assert.Equal(map.Slots[first + count - 1].Result.End, range.End);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // *** UNEXPRESSIBLE, NOT REJECTED — the property that decides whether any of this is safe ***
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void No_public_member_of_MirrorClient_can_name_a_register()
    {
        // X-A: "there is no parameter in which a partial slot could be named… a caller wanting half a
        // slot has nothing to type", and explicitly NOT a validator over a register-range read, because
        // "the unsafe call still exists, still compiles, and is one refactor from being reached".
        //
        // RegisterRange is the register-addressed type in this codebase. If a register-addressed read
        // were ever added, it would carry one — or it would be two bare ints, which the surface snapshot
        // below is what catches.
        var signatures = typeof(MirrorClient)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SelectMany(Types)
            .ToArray();

        Assert.DoesNotContain(typeof(RegisterRange), signatures);
    }

    [Fact]
    public void MirrorClients_PUBLIC_SURFACE_IS_PINNED_so_a_register_addressed_read_cannot_be_added_quietly()
    {
        // The surface is snapshotted deliberately. Adding a public `Read(int, int)` — the exact shape
        // X-A forbids — is indistinguishable by TYPE from `ReadResults(firstSlot, slotCount)`, so a type
        // rule cannot catch it and only an inventory can. A change here is a design decision and this
        // test is where it has to be made on purpose.
        var actual = typeof(MirrorClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[]
        {
            "ClearStartEcho()",
            "Commit(IEnumerable`1)",
            "LowerAllStartBools()",
            "ReadControl()",
            "ReadControlUnverified()",
            "ReadResults(IEnumerable`1)",
            "ReadResults(Int32)",
            "ReadResults(SlotSpan)",
            "WriteVector(Int32,UInt16[])",
        }, actual);
    }

    [Fact]
    public void The_only_thing_a_caller_can_name_is_a_COUNT_OF_WHOLE_SLOTS()
    {
        // SlotSpan carries a first slot and a slot COUNT. There is no register offset and no register
        // length in it, so "half a slot" has no representation — which is a different and stronger
        // statement than "half a slot is rejected".
        var fields = typeof(SlotSpan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "End", "FirstSlot", "SlotCount", "Slots" }, fields);
    }

    private static IEnumerable<Type> Types(MemberInfo member) => member switch
    {
        MethodInfo method => method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
        PropertyInfo property => new[] { property.PropertyType },
        FieldInfo field => new[] { field.FieldType },
        ConstructorInfo ctor => ctor.GetParameters().Select(p => p.ParameterType),
        _ => Array.Empty<Type>(),
    };
}
