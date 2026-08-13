using Harness.Map;

namespace Harness.Wire;

/// <summary>One FC03 over the control region: the build stamp, the scan counter, the start bools and the echo.</summary>
/// <param name="Version">The build stamp the running program publishes (§9).</param>
/// <param name="ScanCounter">The free-running scan counter. Wraps; stamps are differences from T=0.</param>
/// <param name="StartBools">The start-bool registers as read back — what the client COMMANDED.</param>
/// <param name="StartEcho">
/// The echo registers — what the program ACTUALLY RAN, latched by the copy layer from each block's own
/// start condition (X-E). Different fact from <paramref name="StartBools"/>, and only this one is
/// evidence.
/// </param>
public sealed record ControlSnapshot(uint Version, long ScanCounter, ushort[] StartBools, ushort[] StartEcho)
{
    /// <summary>Whether slot <paramref name="index"/>'s start bool reads as raised — the COMMAND.</summary>
    public bool StartRaised(int index) => BitSet(StartBools, index);

    /// <summary>Whether slot <paramref name="index"/>'s block was seen to run — the EXECUTION.</summary>
    public bool Executed(int index) => BitSet(StartEcho, index);

    /// <summary>
    /// Bit <paramref name="index"/> of a per-slot bit region.
    ///
    /// <para>The bit's position WITHIN the register value is not in doubt. What is inferred is which
    /// <c>%M</c> byte carries it, and that inference lives in exactly one place —
    /// <c>MirrorGeometry.BitAddressOf</c>. Both the command side and the echo side route through it, so
    /// a correction is one edit; and because they do, a wrong inference makes a commanded slot fail to
    /// execute rather than a different slot appear to (see <c>CoRunningLog</c>).</para>
    /// </summary>
    private static bool BitSet(ushort[] registers, int index) =>
        (registers[index / RegisterMap.SlotsPerStartRegister] & (1 << (index % RegisterMap.SlotsPerStartRegister))) != 0;
}

/// <summary>
/// The map-aware client: write a slot's vector, raise the start bools, poll, read a slot's results.
///
/// <para><b>Every address comes from the map and there is no API that takes a raw register.</b> DB-6's
/// third protection is "the client instance holds only its own region's bounds, so a write outside it
/// is UNADDRESSABLE — not rejected by a check that could be skipped". That is why this class exposes
/// slot indices rather than registers: there is nothing to skip, because there is nothing to ask for.</para>
///
/// <para><b>The version register is checked on every control read</b> (DB-6 rule 1: "checked before
/// EVERY transaction batch, not only at connect — a download can land mid-session"), and it is free to
/// check because it shares the FC03 the poll was making anyway. <see cref="ReadControlUnverified"/> is
/// the one way past that, and it exists only for <see cref="VersionCheck"/>, which runs BEFORE the
/// expected stamp can be assumed to be there.</para>
///
/// <para><b>Cost is counted in round trips</b> (<see cref="RoundTrips"/>), because registers were
/// measured free and round trips were measured to be the whole cost.</para>
/// </summary>
public sealed class MirrorClient
{
    private readonly RegisterMap _map;
    private readonly IRegisterTransport _transport;

    public MirrorClient(RegisterMap map, IRegisterTransport transport, BuildStamp expected,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Expected = expected;
        WordOrder = wordOrder;

        if (expected.Value == 0)
            throw new WireException("the expected build stamp is zero, which is what bit memory reads before anything writes it — a client holding one cannot tell a running program from an absent one.");
    }

    /// <summary>The build stamp this client will accept. A different one means a download landed mid-session.</summary>
    public BuildStamp Expected { get; }

    public RegisterWordOrder WordOrder { get; }

    /// <summary>Round trips issued. The cost figure — see <c>RegisterMap.PollRoundTrips</c>.</summary>
    public int RoundTrips { get; private set; }

    /// <summary>The control region, WITHOUT the version check. Only <see cref="VersionCheck"/> may use it.</summary>
    public ControlSnapshot ReadControlUnverified()
    {
        var control = _map.Control;
        var registers = Read(control.Register, control.Length);

        var versionOffset = _map.Version.Register - control.Register;
        var scanOffset = _map.ScanCounter.Register - control.Register;
        var startOffset = _map.StartBools.Register - control.Register;
        var echoOffset = _map.StartEcho.Register - control.Register;

        return new ControlSnapshot(
            RegisterWords.To32(registers[versionOffset], registers[versionOffset + 1], WordOrder),
            unchecked((int)RegisterWords.To32(registers[scanOffset], registers[scanOffset + 1], WordOrder)),
            registers[startOffset..(startOffset + _map.StartBools.Length)],
            registers[echoOffset..(echoOffset + _map.StartEcho.Length)]);
    }

    /// <summary>The map this client addresses. Read-only; every address still comes from it.</summary>
    public RegisterMap Map => _map;

    /// <summary>The control region, refusing if the running program is not the one this map describes.</summary>
    public ControlSnapshot ReadControl()
    {
        var snapshot = ReadControlUnverified();

        if (snapshot.Version != Expected.Value)
        {
            throw new WireException(
                $"the version register reads 16#{snapshot.Version:X8} and this client's map was derived for 16#{Expected.Value:X8}. A download landed on the device that this client's mirror does not describe; every address it holds is now a guess (DB-6).");
        }

        return snapshot;
    }

    /// <summary>Write one slot's vector. May span more than one FC16 — the data phase is non-atomic BY DESIGN (X-A).</summary>
    public void WriteVector(int slotIndex, ushort[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var slot = Slot(slotIndex);

        if (values.Length > slot.Vector.Length)
        {
            throw new WireException(
                $"slot {slotIndex} was allocated {slot.Vector.Length} vector register(s) and {values.Length} were offered. The map is the bound, and a write past it would land in the next slot's region.");
        }

        for (var written = 0; written < values.Length; written += ModbusLimits.MaxWriteRegisters)
        {
            var chunk = values[written..Math.Min(written + ModbusLimits.MaxWriteRegisters, values.Length)];
            Write(slot.Vector.Register + written, chunk);
        }
    }

    /// <summary>
    /// Raise the named slots' start bools in ONE transaction — the commit, and the tests' T=0 (X-A, D37).
    ///
    /// <para>Slots not named are left LOW, which is D26a's null: the slot has no vector at this index, its
    /// start bool simply does not rise, and D33's inert holds with the values don't-care. There is no
    /// separate encoding for a null slot and there does not need to be one.</para>
    /// </summary>
    public void Commit(IEnumerable<int> slotIndices)
    {
        ArgumentNullException.ThrowIfNull(slotIndices);

        var word = new ushort[_map.StartBools.Length];
        foreach (var index in slotIndices)
        {
            var slot = Slot(index);
            word[slot.StartBoolRegister - _map.StartBools.Register] |= (ushort)(1 << slot.StartBitInRegister);
        }

        // One FC16, always. The allocator refuses any wave set where it would not be: split across two
        // transactions it stops being a commit, and a torn data phase stops being a detectable non-event.
        Write(_map.StartBools.Register, word);
    }

    /// <summary>Lower every start bool. The reset level D33 holds asserted for the whole inert period.</summary>
    public void LowerAllStartBools() => Write(_map.StartBools.Register, new ushort[_map.StartBools.Length]);

    /// <summary>
    /// Clear every echo latch. D33: "latches are released, and the release must COMPLETE before the
    /// first scan of the test" — so this happens during inert, while the start bools are low and the
    /// copy layer's set-coils cannot be firing.
    /// </summary>
    public void ClearStartEcho() => Write(_map.StartEcho.Register, new ushort[_map.StartEcho.Length]);

    /// <summary>Read one slot's results. ONE FC03, never straddling two slots (X-A).</summary>
    public ushort[] ReadResults(int slotIndex)
    {
        var slot = Slot(slotIndex);
        return Read(slot.Result.Register, slot.Result.Length);
    }

    private SlotAllocation Slot(int index)
    {
        if (index < 0 || index >= _map.Slots.Count)
            throw new WireException($"slot {index} is not in this map, which holds {_map.Slots.Count}.");

        return _map.Slots[index];
    }

    private ushort[] Read(int register, int count)
    {
        RoundTrips++;
        return _transport.ReadHoldingRegisters(register, count);
    }

    private void Write(int register, ushort[] values)
    {
        RoundTrips++;
        _transport.WriteHoldingRegisters(register, values);
    }
}
