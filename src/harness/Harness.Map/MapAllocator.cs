namespace Harness.Map;

/// <summary>
/// A map, or the reasons there is not one. Never both, and never neither.
/// </summary>
/// <param name="SizeReport">
/// What the wave set's slot widths cost it (F-6). <b>A report, never a gate</b> — <see cref="Allocated"/>
/// does not consult it and nothing refuses on it. Null only when the request was refused before widths
/// could be read at all.
/// </param>
public sealed record MapResult(RegisterMap? Map, IReadOnlyList<string> Refusals, SlotSizeReport? SizeReport = null)
{
    /// <summary>True only when a map was produced. Equivalent to <c>Refusals.Count == 0</c> by construction.</summary>
    public bool Allocated => Map is not null;

    /// <summary>The map, or an exception carrying every refusal — for callers that cannot proceed without one.</summary>
    public RegisterMap Require() =>
        Map ?? throw new InvalidOperationException(
            "map derivation refused:" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", Refusals));
}

/// <summary>
/// Derives the register map for a wave set.
///
/// <para><b>Every failure here is a DESIGN-TIME refusal.</b> X-A is explicit that a slot exceeding one
/// FC03 read is "refused at MAP-DERIVATION time — a design-time error, never a runtime one", and the
/// same standard is applied to the rest: an empty wave set, a duplicate slot id, a start-bool commit
/// that would not fit one transaction, a mirror that overflows bit memory, and a mirror that lands in
/// registers a declared neighbour already holds (<see cref="ReservedRegion"/>). Nothing here warns; a
/// check that only warns gets skimmed.</para>
///
/// <para><b>What this allocator deliberately does NOT do: optimise for registers.</b> It never narrows
/// a region, never packs two values into a register, never merges two slots into one read. Registers
/// were measured free up to the FC03/FC16 ceilings and round trips were measured to be the whole cost,
/// so trading width for round trips is the wrong trade in both directions. The only width limits
/// enforced are the two protocol ceilings and the size of bit memory.</para>
/// </summary>
public static class MapAllocator
{
    /// <summary>Derive the map, or report every reason it cannot be derived.</summary>
    public static MapResult Allocate(WaveSetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refusals = new List<string>();
        var geometry = request.Geometry;
        refusals.AddRange(geometry.Refusals);

        var slots = request.Slots ?? Array.Empty<SlotRequest>();

        // Empty is not clean (FI-44). A map over no slots allocates cleanly, hashes stably, and describes
        // nothing — and it is the result most likely to be believed, because every field reads as valid.
        if (slots.Count == 0)
            refusals.Add("wave set contains no slots. A map over an empty wave set is not a clean map, it is a map of nothing.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (slot, i) in slots.Select((s, i) => (s, i)))
        {
            var label = string.IsNullOrWhiteSpace(slot.SlotId) ? $"slot #{i}" : $"slot '{slot.SlotId}'";

            if (string.IsNullOrWhiteSpace(slot.SlotId))
                refusals.Add($"{label}: slot id is blank. Slot ids are part of the map hash and the copy layer's tag names.");
            else if (!seen.Add(slot.SlotId))
                refusals.Add($"{label}: duplicate slot id. Ordinals decide addresses, so two slots of one name cannot both be addressed.");

            if (slot.VectorRegisters < 0)
                refusals.Add($"{label}: vector width cannot be negative; got {slot.VectorRegisters}.");

            if (slot.ResultRegisters < 1)
                refusals.Add($"{label}: result width is {slot.ResultRegisters}. A slot that publishes nothing cannot be judged, and its results would read as an unbroken run of zeros indistinguishable from a real result.");

            if (slot.ResultRegisters > ModbusLimits.MaxReadRegisters)
                refusals.Add($"{label}: result width {slot.ResultRegisters} exceeds the {ModbusLimits.MaxReadRegisters}-register FC03 limit. Each slot's result region must fit in ONE read and a read must never straddle two slots (X-A), so this is a design-time refusal, not something to split at runtime.");
        }

        if (refusals.Count > 0)
            return new MapResult(null, refusals);

        // F-6's cost, computed from what the wave set ASKED for — before the fixed-size rule flattens
        // every slot to the widest and the original widths stop being visible anywhere. A REPORT: it is
        // carried on the result and consulted by nothing.
        var sizeReport = SlotSizeReport.For(slots);

        // Fixed-size slots, sized to the widest in the wave set (X-A). Not narrowed per slot: a client's
        // bounds check is then base + index x slot_size, and an excised slot leaves its hole in place.
        var vectorPerSlot = slots.Max(s => s.VectorRegisters);
        var resultPerSlot = slots.Max(s => s.ResultRegisters);

        var startRegisters = (slots.Count + RegisterMap.SlotsPerStartRegister - 1) / RegisterMap.SlotsPerStartRegister;

        // The commit must be ONE transaction. That single property is what makes a torn data-phase write
        // a detectable non-event instead of a plausible wrong answer, so it is a refusal rather than a
        // fallback to two writes.
        if (startRegisters > ModbusLimits.MaxWriteRegisters)
            refusals.Add($"{slots.Count} slots need {startRegisters} start-bool registers, which exceeds the {ModbusLimits.MaxWriteRegisters}-register FC16 limit. The commit must raise every start bool in ONE transaction (X-A); split across two it stops being a commit.");

        // Version first (DB-6: checked before every transaction batch, so it must be free to read — it
        // rides on the control poll rather than costing a round trip of its own).
        var version = new RegisterRange(0, RegisterMap.VersionRegisters);
        var scanCounter = new RegisterRange(version.End, RegisterMap.ScanCounterRegisters);
        var startBools = new RegisterRange(scanCounter.End, startRegisters);

        // The echo (X-E): what the program ACTUALLY ran, published by the copy layer from each block's
        // own start condition. Same width as the bools and adjacent to them, so the control poll returns
        // commanded and executed together and the two can be compared without a second round trip.
        var startEcho = new RegisterRange(startBools.End, startRegisters);
        var vectorBlock = new RegisterRange(startEcho.End, slots.Count * vectorPerSlot);
        var resultBlock = new RegisterRange(vectorBlock.End, slots.Count * resultPerSlot);

        if (resultBlock.End > geometry.AvailableRegisters)
            refusals.Add($"the map needs {resultBlock.End} registers but only {geometry.AvailableRegisters} are addressable from %M{geometry.BaseByte} to the top of {geometry.TotalBytes} bytes of bit memory — over by {resultBlock.End - geometry.AvailableRegisters}.");

        // 🔴 THE CEILING THAT ACTUALLY BINDS, AND UNTIL 2026-08-22 NOTHING CHECKED IT. The check above is
        // against BIT MEMORY — 3,596 registers at base %M1000 — while MB_HOLD_REG on the rig declares
        // 576. One lane uses ~165, so the gap never bit; a batch of four overflows it.
        //
        // A design-time refusal rather than a runtime discovery, because the runtime symptom is the
        // worst kind: the map allocates, the copy layer generates, the download succeeds, and the wave
        // then gets reads REFUSED BY THE SERVER partway through the band — which reads as a device fault
        // and sends a reader to the controller instead of to the area pointer.
        if (resultBlock.End > geometry.DeclaredRegisters)
            refusals.Add($"the map needs {resultBlock.End} registers but MB_HOLD_REG declares only {geometry.DeclaredRegisters} — over by {resultBlock.End - geometry.DeclaredRegisters}. Registers past the declared area exist in %M and CANNOT BE READ OVER MODBUS AT ALL: the server refuses them, so the overflow would appear as a failing wave rather than as a map that does not fit. Widen the area pointer in the comms block (P#M{geometry.BaseByte}.0 WORD n) and redeploy, or run fewer slots in this wave set.");

        if (refusals.Count > 0)
            return new MapResult(null, refusals, sizeReport);

        var allocations = slots.Select((s, i) => new SlotAllocation(
            SlotId: s.SlotId,
            Index: i,
            Vector: new RegisterRange(vectorBlock.Register + (i * vectorPerSlot), vectorPerSlot),
            Result: new RegisterRange(resultBlock.Register + (i * resultPerSlot), resultPerSlot),
            StartBoolRegister: startBools.Register + (i / RegisterMap.SlotsPerStartRegister),
            StartBitInRegister: i % RegisterMap.SlotsPerStartRegister)).ToArray();

        // Constructing the map is itself the disjointness check: RegisterMap refuses to exist with
        // overlapping regions, so an arithmetic slip above cannot produce a map that allocates cleanly,
        // hashes stably, and silently aliases two agents onto one register (DB-6).
        var map = new RegisterMap(
            geometry,
            version,
            scanCounter,
            startBools,
            startEcho,
            vectorBlock,
            resultBlock,
            vectorPerSlot,
            resultPerSlot,
            allocations);

        // 🔴 THE NEIGHBOUR CHECK, AND EVERY CHECK ABOVE IT IS CLOSED WITH RESPECT TO IT. The map is
        // bounded against the declared area — the WHOLE area — and RegisterMap has just proved its
        // regions disjoint FROM EACH OTHER. Both examine something real; neither has ever been able to
        // see that anything else lives in the same %M. On 2026-08-23 a mirror and a hand-authored
        // virtual panel both held registers 256..323 and 53 panel tags were overwritten every scan,
        // through both of those checks, green.
        //
        // Run AFTER construction and against map.Regions rather than against the locals above, so the
        // comparison covers exactly what the map declares itself to occupy — a region added to the
        // layout later is covered here with nobody remembering to come back. Constructing a map and then
        // refusing it costs nothing: RegisterMap is a value and this is the last word on whether it may
        // be handed out.
        var intrusions = geometry.Intrusions(map.Regions);
        if (intrusions.Count > 0)
            return new MapResult(null, intrusions, sizeReport);

        return new MapResult(map, Array.Empty<string>(), sizeReport);
    }
}
