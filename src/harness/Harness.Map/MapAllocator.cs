namespace Harness.Map;

/// <summary>
/// A map, or the reasons there is not one. Never both, and never neither.
/// </summary>
public sealed record MapResult(RegisterMap? Map, IReadOnlyList<string> Refusals)
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
/// that would not fit one transaction, a mirror that overflows bit memory. Nothing here warns; a check
/// that only warns gets skimmed.</para>
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

        if (refusals.Count > 0)
            return new MapResult(null, refusals);

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

        return new MapResult(map, Array.Empty<string>());
    }
}
