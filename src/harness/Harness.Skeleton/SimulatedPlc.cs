using Harness.Map;
using Harness.Wire;

namespace Harness.Skeleton;

/// <summary>
/// A bit-memory image with a scan loop over a <see cref="LadProgram"/>.
///
/// <para><b>Block order is the OB1 call order, and it is load-bearing.</b> The copy layer is loaded
/// FIRST, so within one scan the vector arrives, the start bool is applied, the block under test runs,
/// and the results-out rungs — which ran earlier in the same scan — publish the PREVIOUS scan's outputs.
/// <b>The minimal copy layer therefore lags results by exactly one scan.</b> That is a real property of
/// the 2.2 design rather than an artefact of this simulator: it is invisible to a poller, whose fastest
/// sustained observation is 3.3 scans (§12a derivation 1), and it is written down here rather than
/// discovered later.</para>
/// </summary>
public sealed class SimulatedPlc
{
    private readonly LadProgram _program;

    public SimulatedPlc(LadProgram program, int bitMemoryBytes = MirrorGeometry.Cpu1214CBitMemoryBytes)
    {
        _program = program ?? throw new ArgumentNullException(nameof(program));
        Memory = new byte[bitMemoryBytes];
    }

    /// <summary>The bit-memory image. The mirror and the program under test both live in here.</summary>
    public byte[] Memory { get; }

    /// <summary>Scans executed since power-up.</summary>
    public long Scans { get; private set; }

    /// <summary>Whether the CPU is executing. A stopped CPU still answers Modbus in this model; it just does nothing.</summary>
    public bool Running { get; set; } = true;

    public void Run(int scans)
    {
        for (var i = 0; i < scans; i++)
        {
            if (!Running)
                return;

            _program.Scan(Memory);
            Scans++;
        }
    }
}

/// <summary>
/// An <see cref="IRegisterTransport"/> over a <see cref="SimulatedPlc"/>'s bit memory.
///
/// <para><b>The register-to-byte rule here is the WIRE's, not the map's.</b> A Modbus holding register
/// travels high byte first, and the <c>MB_HOLD_REG</c> pointer makes register <c>r</c> the two bytes at
/// <c>base + 2r</c>; so the high byte lands in <c>M[base+2r]</c> and the low byte in
/// <c>M[base+2r+1]</c>. The copy layer's tag table states the same thing from the other side
/// (<c>MirrorGeometry.BitAddressOf</c> puts register bit 0 in <c>M[byte+1].0</c>).</para>
///
/// <para><b>THAT AGREEMENT IS NOT A MEASUREMENT AND MUST NOT BE READ AS ONE.</b> Both statements are
/// inferences from the same big-endian premise, so this simulator can only show that the two halves of
/// OUR reasoning agree — precisely the shape of check this project distrusts. <c>MB_SERVER</c>'s own
/// byte-to-register presentation is Siemens' behaviour and no fake we write can validate it. One write
/// of <c>16#0001</c> to the start-bool register, and a read of which <c>%M</c> bit rose, settles it.</para>
///
/// <para><b>Scans elapse during a transaction, which is the point.</b> A round trip was measured at 3.3
/// to 7.4 scans, so a client that could read twice inside one scan would be able to observe things a
/// real one cannot. Modelling the gap is what makes the inert phase's quiescence check and the poll loop
/// mean anything here.</para>
/// </summary>
public sealed class SimulatedTransport : IRegisterTransport
{
    private readonly SimulatedPlc _plc;
    private readonly MirrorGeometry _geometry;

    /// <param name="scansPerTransaction">
    /// Scans the PLC executes per round trip. The default of 4 sits inside the measured 3.3–7.4 band
    /// (§12a derivation 1). Zero models a stopped scan counter, which is a case the client must survive.
    /// </param>
    public SimulatedTransport(SimulatedPlc plc, MirrorGeometry geometry, int scansPerTransaction = 4)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));

        if (scansPerTransaction < 0)
            throw new ArgumentOutOfRangeException(nameof(scansPerTransaction), scansPerTransaction, "a round trip cannot run a negative number of scans.");

        ScansPerTransaction = scansPerTransaction;
    }

    public int ScansPerTransaction { get; set; }

    /// <summary>Round trips this transport has served.</summary>
    public int Transactions { get; private set; }

    public ushort[] ReadHoldingRegisters(int startRegister, int count)
    {
        Bounds(startRegister, count, ModbusLimits.MaxReadRegisters, "FC03 read");
        Elapse();

        var registers = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            var b = _geometry.ByteAddressOf(startRegister + i);
            registers[i] = (ushort)((_plc.Memory[b] << 8) | _plc.Memory[b + 1]);
        }

        return registers;
    }

    public void WriteHoldingRegisters(int startRegister, ushort[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Bounds(startRegister, values.Length, ModbusLimits.MaxWriteRegisters, "FC16 write");
        Elapse();

        for (var i = 0; i < values.Length; i++)
        {
            var b = _geometry.ByteAddressOf(startRegister + i);
            _plc.Memory[b] = (byte)(values[i] >> 8);
            _plc.Memory[b + 1] = (byte)values[i];
        }
    }

    private void Elapse()
    {
        Transactions++;
        _plc.Run(ScansPerTransaction);
    }

    private void Bounds(int startRegister, int count, int limit, string what)
    {
        if (count <= 0)
            throw new WireException($"{what} of {count} register(s) is not a transaction.");

        if (count > limit)
            throw new WireException($"{what} of {count} register(s) exceeds the {limit}-register protocol limit.");

        var last = _geometry.ByteAddressOf(startRegister + count - 1) + 1;
        if (startRegister < 0 || last >= _plc.Memory.Length)
            throw new WireException($"{what} at register {startRegister} for {count} register(s) runs outside the CPU's {_plc.Memory.Length} bytes of bit memory.");
    }

    public void Dispose() { }
}
