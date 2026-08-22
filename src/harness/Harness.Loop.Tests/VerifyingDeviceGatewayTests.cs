using System.Reflection;
using Harness.Loop;
using Harness.Map;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>A GATEWAY THAT REPORTS <c>Loaded</c> WITHOUT LOADING IS ONE EDIT FROM A GATEWAY THAT LIES —
/// SO THIS ONE CANNOT REPORT IT FROM A DECLARATION AT ALL.</b>
///
/// <para>The invariant under test: <c>Loaded</c> is true only when a build stamp was READ FROM THE DEVICE
/// and equals the staged build's. Every negative below is a way of not having that, and each one refuses.</para>
/// </summary>
public class VerifyingDeviceGatewayTests
{
    private static readonly BuildStamp Staged = new(0x21D74D35);

    private static RegisterMap Map() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 1000) / 2),
            new[] { new SlotRequest("HBA", 2, 4) })).Require();

    private static IReadOnlyList<HarnessObject> Objects() => new[]
    {
        new HarnessObject("HarnessMirror", HarnessObjectKind.TagTable, "TAGTABLE HarnessMirror\n"),
        new HarnessObject("FC_HarnessCopyLayer", HarnessObjectKind.Block, "BLOCK FC FC_HarnessCopyLayer\n"),
    };

    // ---------------------------------------------------------------------------------------------
    // The positive case — and it is a MEASUREMENT
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DEVICE_CARRYING_THE_STAGED_STAMP_IS_VERIFIED_and_NOTHING_WAS_WRITTEN()
    {
        // The stamp is a constant generated into the copy layer's own code: it is on the wire ONLY if that
        // exact code is executing. Stronger than a load manifest, which says what TIA reported SENDING.
        var map = Map();
        var transport = new StampTransport(map, Staged.Value, RegisterWordOrder.HighWordFirst);
        using var gateway = new VerifyingDeviceGateway(map, () => transport);

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.True(outcome.Attempted);
        Assert.True(outcome.Loaded);
        Assert.Contains("VERIFIED, NOT DEPLOYED", outcome.Detail, StringComparison.Ordinal);

        // *** VERIFICATION THAT MUTATES WHAT IT VERIFIES IS NOT VERIFICATION. ***
        Assert.Empty(transport.Writes);
        Assert.Equal(1, transport.Reads);

        // The manifest lists what was covered — the claim. The stamp is the evidence.
        Assert.Contains("FC_HarnessCopyLayer", outcome.Manifest);
    }

    // ---------------------------------------------------------------------------------------------
    // THE DID-NOT-RUN TEST: a device carrying a DIFFERENT stamp must be refused, and the refusal must fire
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DEVICE_CARRYING_A_DIFFERENT_BUILD_IS_REFUSED_and_the_refusal_FIRES()
    {
        // 🔴 A verifier that only ever passes is indistinguishable from no verifier. This is the case the
        // whole gateway exists for: the device is up, answering, and running SOMETHING ELSE.
        var map = Map();
        var transport = new StampTransport(map, 0xDEADBEEF, RegisterWordOrder.HighWordFirst);
        using var gateway = new VerifyingDeviceGateway(map, () => transport);

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.True(outcome.Attempted);
        Assert.False(outcome.Loaded);
        Assert.Empty(outcome.Manifest);

        Assert.Contains("THE DEVICE IS NOT RUNNING THIS BUILD", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("DEADBEEF", outcome.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("21D74D35", outcome.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void And_the_refusal_IS_the_condition_the_loop_stops_on_so_no_wave_can_follow()
    {
        // The refusal must reach the loop's own ordering, not merely sit on the outcome. LoopRun's step 5
        // stops on exactly `!Attempted || !Loaded`, so a wave cannot follow a device carrying another
        // build — which matters because such a wave reads the RIGHT registers of the WRONG code and
        // returns a confident wrong answer.
        var map = Map();
        var transport = new StampTransport(map, 0xDEADBEEF, RegisterWordOrder.HighWordFirst);
        using var gateway = new VerifyingDeviceGateway(map, () => transport);

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.True(outcome.Attempted && !outcome.Loaded);

        // *** AND THE SOCKET WAS NOT THE THING THAT FAILED. *** The device answered; it is simply running
        // something else. That distinction is why this is a REFUSAL and not a NOT CHECKED.
        Assert.Equal(1, transport.Reads);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void OPEN_REFUSES_BEFORE_ANY_VERIFICATION_so_a_caller_cannot_take_a_socket_first()
    {
        // A caller that could Open() before Deploy() would have a transport onto a device nothing had
        // checked — and every address it holds was derived for the staged map.
        var map = Map();
        using var gateway = new VerifyingDeviceGateway(map, () => new StampTransport(map, Staged.Value, RegisterWordOrder.HighWordFirst));

        Assert.Throws<InvalidOperationException>(() => gateway.Open());
    }

    // ---------------------------------------------------------------------------------------------
    // Every way of NOT having the measurement, and each one refuses
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_TRANSPORT_THAT_WILL_NOT_OPEN_IS_NOT_CHECKED_and_refuses()
    {
        using var gateway = new VerifyingDeviceGateway(Map(), () => throw new IOException("no route to host"));

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.False(outcome.Loaded);
        Assert.Contains("NOT CHECKED", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("AN UNREACHABLE DEVICE IS NOT A VERIFIED ONE", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_READ_THAT_THROWS_IS_NOT_CHECKED_and_refuses()
    {
        using var gateway = new VerifyingDeviceGateway(Map(), () => new ThrowingTransport());

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.False(outcome.Loaded);
        Assert.Contains("could not be read", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("Empty is not clean", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SHORT_READ_IS_NOT_A_STAMP_because_reassembling_one_would_produce_a_plausible_number()
    {
        using var gateway = new VerifyingDeviceGateway(Map(), () => new ShortTransport());

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.False(outcome.Loaded);
        Assert.Contains("A short read cannot be reassembled into a stamp", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ZERO_STAGED_STAMP_IS_REFUSED_because_unwritten_memory_reads_as_zero()
    {
        var map = Map();
        using var gateway = new VerifyingDeviceGateway(map, () => new StampTransport(map, 0, RegisterWordOrder.HighWordFirst));

        var outcome = gateway.Deploy(Objects(), new BuildStamp(0));

        Assert.False(outcome.Loaded);
        Assert.Contains("nothing to verify against", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SWAPPED_STAMP_IS_REPORTED_AS_A_CALIBRATION_RESULT_and_is_STILL_A_REFUSAL()
    {
        // ⚠️ A value that matches under the OTHER word order is a different fact from a different build,
        // and saying so stops somebody re-downloading a device that is already right. It is still refused:
        // a verifier that accepted a value it had to reinterpret would accept anything.
        var map = Map();
        var transport = new StampTransport(map, Staged.Value, RegisterWordOrder.LowWordFirst);
        using var gateway = new VerifyingDeviceGateway(map, () => transport, RegisterWordOrder.HighWordFirst);

        var outcome = gateway.Deploy(Objects(), Staged);

        Assert.False(outcome.Loaded);
        Assert.Contains("HALVES SWAPPED", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("It is still a REFUSAL", outcome.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The invariant itself, pinned
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NOTHING_ON_THIS_TYPE_CAN_ASSERT_Loaded_WITHOUT_THE_COMPARISON()
    {
        // *** THE HOLE THIS PINS. *** A bool parameter named `assumeLoaded`, a settable property, an
        // overload taking a DeploymentOutcome — any of them restores the "gateway that lies" and no
        // behavioural test would notice, because they all drive the measuring path.
        var type = typeof(VerifyingDeviceGateway);

        Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite));

        Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetParameters())
            .Where(p => p.ParameterType == typeof(bool)));

        Assert.Empty(type.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType == typeof(bool) || p.ParameterType == typeof(DeploymentOutcome)));

        // And exactly one Deploy, so there is no second path to an outcome.
        var deploys = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(VerifyingDeviceGateway.Deploy))
            .ToArray();

        Assert.Single(deploys);
    }

    // ---------------------------------------------------------------------------------------------

    private sealed class StampTransport : IRegisterTransport
    {
        private readonly RegisterMap _map;
        private readonly ushort[] _words;

        public StampTransport(RegisterMap map, uint stamp, RegisterWordOrder order)
        {
            _map = map;
            _words = RegisterWords.From32(stamp, order);
        }

        public int Reads { get; private set; }

        public List<int> Writes { get; } = new();

        public ushort[] ReadHoldingRegisters(int register, int count)
        {
            Reads++;
            Assert.Equal(_map.Version.Register, register);
            Assert.Equal(_map.Version.Length, count);
            return _words;
        }

        public void WriteHoldingRegisters(int register, ushort[] values) => Writes.Add(register);

        public void Dispose() { }
    }

    private sealed class ThrowingTransport : IRegisterTransport
    {
        public ushort[] ReadHoldingRegisters(int register, int count) => throw new IOException("connection reset");

        public void WriteHoldingRegisters(int register, ushort[] values) => throw new InvalidOperationException();

        public void Dispose() { }
    }

    private sealed class ShortTransport : IRegisterTransport
    {
        public ushort[] ReadHoldingRegisters(int register, int count) => new ushort[] { 0x21D7 };

        public void WriteHoldingRegisters(int register, ushort[] values) => throw new InvalidOperationException();

        public void Dispose() { }
    }
}
