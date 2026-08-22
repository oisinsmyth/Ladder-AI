using System.Reflection;
using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>THE RESULT REGISTERS ARE UNWRITABLE BY CONSTRUCTION, AND THIS IS WHERE THAT IS PROVED.</b>
///
/// <para>Until now "the harness never writes the result registers" was a property of our code and of
/// nothing else — <i>a convention, and a convention is not a fence</i>. The client's write took a bare
/// register number, so any future line inside it could address a result register; and the failure mode
/// is not a crash. The observation is overwritten and the run reports <b>the harness's own value as the
/// block's behaviour</b>, quietly and plausibly.</para>
///
/// <para><b>The wire cannot help.</b> <c>MB_SERVER</c> serves four spaces and FC04 input registers are
/// real and read-only — but the registered <c>MB_SERVER</c> 5.3 instruction has exactly ONE area pointer
/// (<c>MB_HOLD_REG</c>), so results cannot be placed anywhere read-only. The fence moves to the type
/// surface, which is where this component's other guards already live.</para>
/// </summary>
public class MirrorWriteTargetTests
{
    private static readonly BuildStamp Stamp = new(0xA93F2C71);

    private static RegisterMap Map(int slots = 2, int vector = 3, int result = 4) =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", vector, result)).ToArray())).Require();

    // ---------------------------------------------------------------------------------------------
    // 1 — what became UNREPRESENTABLE
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THERE_IS_NO_FACTORY_THAT_PRODUCES_A_RESULT_TARGET()
    {
        // *** THE FENCE ITSELF. *** Not "no factory currently used" — no factory that CAN. Every public
        // way to make a target is enumerated and each one is asserted to name a writable region.
        var factories = typeof(MirrorWriteTarget)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(MirrorWriteTarget))
            .ToArray();

        Assert.NotEmpty(factories);

        var map = Map();

        foreach (var factory in factories)
        {
            // Arguments are chosen BY PARAMETER NAME so the sweep stays valid as factories are added. A
            // name this does not know throws rather than being skipped: an un-invoked factory is one this
            // test did not check, and silently passing over it is how the fence would rot.
            var arguments = factory.GetParameters()
                .Select(p => p.ParameterType == typeof(RegisterMap)
                    ? (object)map
                    : p.Name switch
                    {
                        "slotIndex" or "offset" => 0,
                        "length" => 1,
                        _ => throw new InvalidOperationException(
                            $"MirrorWriteTarget.{factory.Name} takes a parameter '{p.Name}' this sweep does not know how to "
                            + "supply, so it could not be invoked and its region was never checked. Teach this test the "
                            + "parameter — do not let a factory go unexamined."),
                    })
                .ToArray();

            var produced = (MirrorWriteTarget)factory.Invoke(null, arguments)!;

            Assert.NotEqual(MirrorRegion.Result, produced.Region);
        }
    }

    [Fact]
    public void THERE_IS_NO_PUBLIC_CONSTRUCTOR_so_a_target_cannot_be_hand_rolled()
    {
        // The analogue of WireTiming.BackstopMs having no bare-int overload: a caller holding a register
        // number would otherwise simply find something that accepts one.
        var constructors = typeof(MirrorWriteTarget)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(constructors);
    }

    [Fact]
    public void THE_CLIENTS_WRITE_TAKES_A_TARGET_AND_THERE_IS_NO_OVERLOAD_THAT_TAKES_A_BARE_REGISTER()
    {
        // *** THE HOLE THIS PINS. *** Re-adding `Write(int register, ushort[] values)` beside the safe one
        // would restore the whole defect, and NO behavioural test would notice — every existing test would
        // still pass, because they all exercise the safe path. Private methods are included deliberately:
        // the risk here is our own next line, not an external caller.
        var writes = typeof(MirrorClient)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == "Write")
            .ToArray();

        var write = Assert.Single(writes);

        Assert.Equal(typeof(MirrorWriteTarget), write.GetParameters()[0].ParameterType);
        Assert.DoesNotContain(writes, m => m.GetParameters()[0].ParameterType == typeof(int));
    }

    // ---------------------------------------------------------------------------------------------
    // 2 — the DID-NOT-RUN test: a legitimate write must still work through the same path
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_LEGITIMATE_COMMAND_WRITE_STILL_REACHES_THE_WIRE_AT_THE_RIGHT_REGISTER()
    {
        // 🔴 *** A FENCE THAT REFUSES EVERYTHING PASSES EVERY TEST THAT ONLY CHECKS REFUSALS. *** This is
        // the positive case, and it asserts the ADDRESS as well as the values — a guard that quietly
        // shifted every write by one register would satisfy a test that only checked "it wrote something".
        var map = Map();
        var transport = new RecordingTransport();
        var client = new MirrorClient(map, transport, Stamp);

        client.WriteVector(1, new ushort[] { 11, 22, 33 });

        var write = Assert.Single(transport.Writes);

        Assert.Equal(map.Slots[1].Vector.Register, write.Register);
        Assert.Equal(new ushort[] { 11, 22, 33 }, write.Values);
    }

    [Fact]
    public void And_the_commit_and_the_echo_clear_still_work_too()
    {
        var map = Map();
        var transport = new RecordingTransport();
        var client = new MirrorClient(map, transport, Stamp);

        client.Commit(new[] { 0 });
        client.LowerAllStartBools();

        // The echo is an OBSERVATION that the client nevertheless clears — D33 requires the release to
        // complete before the first scan, and a latch nobody can clear reports the previous test forever.
        // It is not a result register, and the fence is not about it.
        client.ClearStartEcho();

        Assert.Equal(3, transport.Writes.Count);
        Assert.Equal(map.StartBools.Register, transport.Writes[0].Register);
        Assert.Equal(map.StartBools.Register, transport.Writes[1].Register);
        Assert.Equal(map.StartEcho.Register, transport.Writes[2].Register);
    }

    [Fact]
    public void A_LONG_VECTOR_THAT_CHUNKS_ACROSS_SEVERAL_FC16s_STILL_LANDS_CONTIGUOUSLY_IN_ITS_OWN_SLOT()
    {
        // The chunking arithmetic is the one place a write span is COMPUTED rather than named, so it is the
        // one place that could walk into the results. Every chunk is re-derived through the factory — and
        // this proves the derivation is right, not merely that it refuses.
        var wide = ModbusLimits.MaxWriteRegisters + 5;
        var map = Map(slots: 1, vector: wide, result: 2);
        var transport = new RecordingTransport();
        var client = new MirrorClient(map, transport, Stamp);

        client.WriteVector(0, Enumerable.Range(0, wide).Select(i => (ushort)i).ToArray());

        Assert.True(transport.Writes.Count > 1, "the fixture was meant to force more than one FC16.");

        var expected = map.Slots[0].Vector.Register;
        foreach (var write in transport.Writes)
        {
            Assert.Equal(expected, write.Register);
            Assert.Equal(MirrorRegion.Vector, map.RegionOf(write.Register));
            Assert.Equal(MirrorRegion.Vector, map.RegionOf(write.Register + write.Values.Length - 1));
            expected += write.Values.Length;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 3 — the runtime refusal that remains, and it is REACHABLE
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 99)]
    [InlineData(2, 5)]
    [InlineData(-1, 1)]
    public void A_VECTOR_SPAN_THAT_LEAVES_ITS_REGION_IS_REFUSED_BY_NAME_never_clamped(int offset, int length)
    {
        // The register comes from the map, but the OFFSET and LENGTH are the caller's numbers — so this
        // boundary really does take an address as data, and the refusal really is reachable.
        var map = Map(slots: 1, vector: 3, result: 4);

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => MirrorWriteTarget.Vector(map, 0, offset, length));

        Assert.Contains("REFUSED, NOT CLAMPED", thrown.Message, StringComparison.Ordinal);

        // It says which region the span would have landed in, because the region after the vectors is the
        // RESULTS and that is the whole reason this refuses.
        Assert.Contains("RESULT", thrown.Message.ToUpperInvariant(), StringComparison.Ordinal);
    }


    [Fact]
    public void A_ZERO_LENGTH_SPAN_IS_ITS_OWN_REFUSAL_rather_than_a_sentence_about_no_registers()
    {
        // It names no registers, so "would have landed in the results" would be a claim about nothing —
        // and an empty FC16 would still cost a round trip while writing nothing.
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => MirrorWriteTarget.Vector(Map(), 0, 0, 0));

        Assert.Contains("A zero-length write is not a write", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_refusal_NAMES_THE_REGION_the_bad_span_would_have_reached()
    {
        // Concretely: slot 0's vector is 3 registers and the results follow, so a 4-register write at
        // offset 0 reaches into Result. The message says so rather than saying "out of range".
        var map = Map(slots: 1, vector: 3, result: 4);

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => MirrorWriteTarget.Vector(map, 0, 0, 4));

        Assert.Contains(MirrorRegion.Result.ToString(), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegionOf_classifies_EVERY_register_in_the_map_so_a_refusal_can_always_name_one()
    {
        var map = Map();

        for (var register = 0; register < map.TotalRegisters; register++)
            Assert.NotEqual(MirrorRegion.Unmapped, map.RegionOf(register));

        // And past the end it says Unmapped rather than guessing.
        Assert.Equal(MirrorRegion.Unmapped, map.RegionOf(map.TotalRegisters));
    }

    // ---------------------------------------------------------------------------------------------
    // 4 — the UNREACHABLE assertion, tested directly, or it is decoration
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_LAST_LINE_OF_THE_ARGUMENT_FIRES_if_a_result_target_is_ever_forged()
    {
        // *** ASK THE QUESTION OF YOUR OWN GUARD: COULD ANY INPUT REACH THE FORBIDDEN LINE? *** No — no
        // factory makes a result target, so `Write`'s region check is unreachable through the public API.
        // An unreachable check that is never exercised is decoration, so it is invoked here through
        // reflection: the private constructor is used to forge exactly the target the fence forbids, and
        // the private Write is called with it.
        //
        // This is also the regression test for the day somebody adds a factory that CAN produce one.
        var map = Map();
        var transport = new RecordingTransport();
        var client = new MirrorClient(map, transport, Stamp);

        var constructor = typeof(MirrorWriteTarget)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single();

        var forged = constructor.Invoke(new object[] { map.ResultBlock.Register, 1, MirrorRegion.Result });

        var write = typeof(MirrorClient)
            .GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var thrown = Assert.Throws<WireException>(() =>
        {
            try
            {
                write.Invoke(client, new[] { forged, new ushort[] { 1 } });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        });

        Assert.Contains("RESULT region", thrown.Message, StringComparison.Ordinal);

        // *** AND NOTHING REACHED THE WIRE. *** A guard that throws after writing has protected nothing.
        Assert.Empty(transport.Writes);
    }

    // ---------------------------------------------------------------------------------------------

    private sealed class RecordingTransport : IRegisterTransport
    {
        public List<(int Register, ushort[] Values)> Writes { get; } = new();

        public ushort[] ReadHoldingRegisters(int register, int count) => new ushort[count];

        public void WriteHoldingRegisters(int register, ushort[] values) => Writes.Add((register, values));

        public void Dispose()
        {
        }
    }
}
