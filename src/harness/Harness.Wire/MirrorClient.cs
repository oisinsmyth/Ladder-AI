using Harness.Map;

namespace Harness.Wire;

/// <summary>One FC03 over the control region: the build stamp, the scan counter, the start bools and the echo.</summary>
/// <param name="Version">The build stamp the running program publishes (§9).</param>
/// <param name="ScanCounter">
/// The free-running scan counter. <b>Wraps</b>, so it is a <see cref="ScanCount"/> and not a number:
/// the difference is modular and the wrong subtraction does not compile. See that type for why the
/// absorption that existed on <c>S7Transport</c> did nothing for the live path.
/// </param>
/// <param name="StartBools">The start-bool registers as read back — what the client COMMANDED.</param>
/// <param name="StartEcho">
/// The echo registers — what the program ACTUALLY RAN, latched by the copy layer from each block's own
/// start condition (X-E). Different fact from <paramref name="StartBools"/>, and only this one is
/// evidence.
/// </param>
public sealed record ControlSnapshot(uint Version, ScanCount ScanCounter, ushort[] StartBools, ushort[] StartEcho)
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
    private readonly IMirrorFeedPublisher? _feed;
    private readonly Func<DateTimeOffset> _clock;

    /// <param name="feed">
    /// 🔴 <b>OPTIONAL, OPT-IN, AND IT NEVER CAUSES A READ.</b> When present, every read this client makes
    /// is FORWARDED to it at the moment the read returns, with the instant it returned — so a viewer can
    /// show exactly the bytes this client acted on without opening a second socket to the device.
    /// <c>MB_SERVER</c> accepts one connection, and two connections would in any case be two samples at
    /// two instants. See <see cref="IMirrorFeedPublisher"/>.
    /// </param>
    /// <param name="clock">
    /// Stamps each forwarded read. Injectable so the feed's timestamps are testable without waiting;
    /// <b>it is read at the moment the transport returns and nowhere else</b>, so the published instant is
    /// the read's own and never a render time or a publish time.
    /// </param>
    public MirrorClient(RegisterMap map, IRegisterTransport transport, BuildStamp expected,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
        IMirrorFeedPublisher? feed = null,
        Func<DateTimeOffset>? clock = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _feed = feed;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        Expected = expected;
        WordOrder = wordOrder;

        if (expected.Value == 0)
            throw new WireException("the expected build stamp is zero, which is what bit memory reads before anything writes it — a client holding one cannot tell a running program from an absent one.");

        // The feed learns what it describes before it carries a single register, so a viewer attached
        // before the first poll shows "a publisher is running and no read has landed yet" rather than a
        // table of zeros or an absence indistinguishable from "no wave has ever run".
        _feed?.Begin(new MirrorFeedIdentity(expected.Value, map.MapHash, map.TotalRegisters, wordOrder));
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
            // *** UNSIGNED, NEVER SIGN-EXTENDED. *** `unchecked((int)...)` into a long is what turned a
            // one-scan advance across the DInt boundary into -4 294 967 295.
            ScanCount.FromRegisters(registers[scanOffset], registers[scanOffset + 1], WordOrder),
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

        // The chunking arithmetic is the one place a write span is computed rather than named, so each
        // chunk is re-derived through MirrorWriteTarget.Vector, which refuses anything leaving the vector
        // region. The region after the vectors is the RESULTS.
        for (var written = 0; written < values.Length; written += ModbusLimits.MaxWriteRegisters)
        {
            var chunk = values[written..Math.Min(written + ModbusLimits.MaxWriteRegisters, values.Length)];
            Write(MirrorWriteTarget.Vector(_map, slotIndex, written, chunk.Length), chunk);
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
        Write(MirrorWriteTarget.StartBools(_map), word);
    }

    /// <summary>Lower every start bool. The reset level D33 holds asserted for the whole inert period.</summary>
    public void LowerAllStartBools() => Write(MirrorWriteTarget.StartBools(_map), new ushort[_map.StartBools.Length]);

    /// <summary>
    /// Clear every echo latch. D33: "latches are released, and the release must COMPLETE before the
    /// first scan of the test" — so this happens during inert, while the start bools are low and the
    /// copy layer's set-coils cannot be firing.
    /// </summary>
    public void ClearStartEcho() => Write(MirrorWriteTarget.StartEcho(_map), new ushort[_map.StartEcho.Length]);

    /// <summary>Read one slot's results. One FC03.</summary>
    public ushort[] ReadResults(int slotIndex) => ReadResults(new SlotSpan(slotIndex, 1))[0];

    /// <summary>
    /// <b>Read a run of WHOLE slots in ONE FC03</b> (X-A as amended by F-1), returning one array per
    /// slot in run order.
    ///
    /// <para><b>A split read is unexpressible here, not rejected here.</b> There is no register
    /// parameter to pass and no register-addressed overload to reach for; the range is derived by
    /// <c>RegisterMap.ResultRead</c>. And the RESULT is per-slot rather than a flat register array, so a
    /// caller cannot receive half a slot either — both ends of the call are in slot units.</para>
    ///
    /// <para><b>Why that shape rather than a check.</b> X-A: a <c>read(start, length)</c> guarded by an
    /// assert "satisfies the letter of the rule and abandons its mechanism — the unsafe call still
    /// exists, still compiles, and is one refactor from being reached". The precedent is this class's
    /// own write side, where an out-of-region write is unaddressable rather than caught.</para>
    ///
    /// <para><b>What it buys:</b> one transaction where there were <c>slotCount</c>, and round trips are
    /// the scarce resource. The coherence argument is untouched — the hazard was never touching two
    /// slots, it was touching half of one, and each slot inside one transaction is wholly before or
    /// wholly after any publish.</para>
    /// </summary>
    public IReadOnlyList<ushort[]> ReadResults(SlotSpan run)
    {
        if (run.FirstSlot < 0 || run.SlotCount < 1 || run.End > _map.Slots.Count)
            throw new WireException($"{run} is not in this map, which holds {_map.Slots.Count} slot(s).");

        var range = _map.ResultRead(run);
        var registers = Read(range.Register, range.Length);
        var width = _map.ResultRegistersPerSlot;

        return Enumerable.Range(0, run.SlotCount)
            .Select(i => registers[(i * width)..((i + 1) * width)])
            .ToArray();
    }

    /// <summary>Read every named slot's results in the fewest whole-slot transactions the map allows.</summary>
    public IReadOnlyDictionary<int, ushort[]> ReadResults(IEnumerable<int> slotIndices)
    {
        ArgumentNullException.ThrowIfNull(slotIndices);

        var results = new Dictionary<int, ushort[]>();

        foreach (var run in _map.ReadPlan(slotIndices))
        {
            var read = ReadResults(run);
            for (var i = 0; i < run.SlotCount; i++)
                results[run.FirstSlot + i] = read[i];
        }

        return results;
    }

    private SlotAllocation Slot(int index)
    {
        if (index < 0 || index >= _map.Slots.Count)
            throw new WireException($"slot {index} is not in this map, which holds {_map.Slots.Count}.");

        return _map.Slots[index];
    }

    /// <summary>
    /// 🔴 <b>EVERY READ THIS CLIENT MAKES GOES THROUGH HERE, WHICH IS WHY THE FEED IS PUBLISHED HERE.</b>
    ///
    /// <para>The control read, the version read and every result read all land on this one line, so a
    /// viewer following the feed sees the whole picture the harness has — and sees it as the SAME BYTES,
    /// at the SAME INSTANTS, that the harness made its own decisions from. Publishing anywhere higher
    /// would mean picking which reads a viewer is entitled to see; publishing on a clock of its own would
    /// mean two samples again, minus the second socket.</para>
    ///
    /// <para><b>The stamp is taken when the transport RETURNS</b>, not before the call and not when the
    /// document is written. A read costs ~72 ms to this rig, so those are visibly different instants and
    /// only one of them is when the values existed.</para>
    ///
    /// <para><b>A publish never affects the read.</b> The values are returned whatever the feed does, and
    /// <see cref="IMirrorFeedPublisher"/> is contracted not to throw — the feed is a view and the wave is
    /// the work.</para>
    /// </summary>
    private ushort[] Read(int register, int count)
    {
        RoundTrips++;
        var values = _transport.ReadHoldingRegisters(register, count);
        _feed?.Publish(register, values, _clock());
        return values;
    }

    /// <summary>
    /// 🔴 <b>THE ONLY WRITE IN THIS CLIENT, AND IT CANNOT BE POINTED AT A RESULT REGISTER.</b>
    ///
    /// <para><b>It used to take a bare register number.</b> That made "the harness never writes the result
    /// registers" a property of our code and nothing else — <i>a convention, and a convention is not a
    /// fence</i>. Any future line in this class could have addressed a result register, and the failure
    /// mode is not an exception: the observation is overwritten and the run reports <b>the harness's own
    /// value as the block's behaviour</b>, quietly and plausibly.</para>
    ///
    /// <para><b>Now it takes a <see cref="MirrorWriteTarget"/>, which has a private constructor and no
    /// factory that produces a result span.</b> A result write is therefore not refused here — it is
    /// unaddressable, the same shape as this class's split-read guard and as <c>WireTiming.BackstopMs</c>
    /// having no bare-<c>int</c> overload. <c>MirrorWriteTargetTests</c> pins that by reflection, because
    /// re-adding a register-taking overload beside this one is the entire hole and no behavioural test
    /// would notice it.</para>
    ///
    /// <para><b>The assertion below is deliberately unreachable</b> and is kept as the last line of the
    /// argument rather than as a working check: if a factory is ever added that can produce a result span,
    /// this throws instead of writing. It is tested directly, since nothing can reach it by construction.</para>
    ///
    /// <para>⚠️ <b>THIS PROTECTS THE EVIDENCE FROM US, AND FROM NOBODY ELSE.</b> FC03 holding registers are
    /// read-write to every Modbus master on the network and <c>MB_SERVER</c> 5.3 exposes exactly one area
    /// pointer (<c>MB_HOLD_REG</c>), so results cannot be moved to a read-only space. The copy layer
    /// rewriting every result register every scan is <b>self-correcting, not a fence</b>: a stray write is
    /// erased within ~23 ms, and a read inside that window still sees the forged value.</para>
    /// </summary>
    private void Write(MirrorWriteTarget target, ushort[] values)
    {
        if (target.Region == MirrorRegion.Result)
        {
            throw new WireException(
                $"a write was addressed at {target}, which is a RESULT region. No factory on MirrorWriteTarget can produce one, "
                + "so reaching this means a factory was added that can — and a client write to a result register does not fail, "
                + "it overwrites an observation and makes the run report the harness's own value as the block's behaviour.");
        }

        RoundTrips++;
        _transport.WriteHoldingRegisters(target.Register, values);
    }
}
