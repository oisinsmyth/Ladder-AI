namespace Harness.Map;

/// <summary>Which part of the mirror a register belongs to. <b>Every register has exactly one.</b></summary>
public enum MirrorRegion
{
    /// <summary>Outside every named region. Not writable, not readable as anything.</summary>
    Unmapped,

    /// <summary>The build stamp. Written by the COPY LAYER, never by a client.</summary>
    Version,

    /// <summary>The free-running scan counter. Written by the COPY LAYER, never by a client.</summary>
    ScanCounter,

    /// <summary>The start bools — the client's commit. <b>Client-writable.</b></summary>
    StartBools,

    /// <summary>
    /// The start-echo latches (X-E) — what the PROGRAM ran.
    ///
    /// <para><b>An observation that the client nevertheless writes, and the exception is D33's:</b>
    /// "latches are released, and the release must COMPLETE before the first scan of the test". A latch
    /// nobody can clear is a latch that reports the previous test forever. So this is writable — but only
    /// to CLEAR, and only during inert, while the start bools are low and the copy layer's set-coils
    /// cannot be firing. It is not a result register and the fence below is not about it.</para>
    /// </summary>
    StartEcho,

    /// <summary>The vector registers — the client's stimulus. <b>Client-writable.</b></summary>
    Vector,

    /// <summary>
    /// 🔴 <b>THE RESULT REGISTERS. THE OBSERVATION. THE CLIENT MUST NEVER WRITE ONE.</b>
    ///
    /// <para>A client write here does not crash anything: it overwrites an observation, and the run then
    /// reports <b>the harness's own value as the block's behaviour</b>. That is evidence corruption, and it
    /// fails quietly and plausibly — the worst combination this harness can produce.</para>
    /// </summary>
    Result,
}

/// <summary>
/// 🔴 <b>A DESTINATION THE CLIENT MAY WRITE — AND THERE IS NO WAY TO CONSTRUCT ONE THAT NAMES A RESULT
/// REGISTER.</b>
///
/// <para><b>Why a type and not a check.</b> Until now "the harness never writes the result registers" was
/// a property of our code rather than of anything enforceable — <i>a convention, and a convention is not a
/// fence</i>. The client's raw write took a BARE REGISTER NUMBER, so any future line inside it could
/// address a result register, and the failure mode is not an exception: it is a plausible wrong answer.</para>
///
/// <para>*** THE WIRE CANNOT HELP, AND THAT WAS MEASURED. *** <c>MB_SERVER</c> does serve four spaces —
/// FC01 coils, FC02 discrete inputs, FC03 holding, FC04 input registers — and FC04 is real, served, and
/// returns different data from FC03, so a read-only space genuinely exists on the wire. <b>But the
/// registered <c>MB_SERVER</c> 5.3 instruction has exactly ONE area pointer, <c>MB_HOLD_REG</c></b>
/// (its ports are <c>DISCONNECT</c>, <c>MB_HOLD_REG</c>, <c>CONNECT</c>, <c>NDR</c>, <c>DR</c>,
/// <c>ERROR</c>, <c>STATUS</c>, and that is all). FC01/02/04 are bound to the process image, which is why
/// they read zeros on this rig. <b>So results cannot be placed in a read-only Modbus space, and the fence
/// has to move from the wire to the type surface.</b></para>
///
/// <para><b>The construction rule:</b> there is no public constructor and no factory that takes a bare
/// register. Every factory names a WRITABLE REGION and derives the register from the map. A result
/// register is therefore not refused — <b>it is unaddressable</b>, which is the same shape as this
/// component's split-read guard and as <c>WireTiming.BackstopMs</c> having no bare-<c>int</c> overload.</para>
///
/// <para>⚠️ <b>WHAT THIS DOES NOT BUY, stated here rather than only in a report.</b> It stops OUR client
/// corrupting evidence. <b>It stops nothing else on that network</b> — FC03 holding registers are
/// read-write to every Modbus master alive, and the instruction offers no alternative. And the copy
/// layer's rewriting of every result register every scan is <b>self-correcting, not a fence</b>: a stray
/// write is erased within about 23 ms, but a read landing inside that window still sees the forged value
/// and cannot tell.</para>
/// </summary>
public readonly record struct MirrorWriteTarget
{
    private MirrorWriteTarget(int register, int length, MirrorRegion region)
    {
        Register = register;
        Length = length;
        Region = region;
    }

    /// <summary>First holding register of the span.</summary>
    public int Register { get; }

    /// <summary>How many registers the span covers.</summary>
    public int Length { get; }

    /// <summary>
    /// The region this span lies in. <b>Never <see cref="MirrorRegion.Result"/></b> — no factory produces
    /// one, and the constructor is private.
    /// </summary>
    public MirrorRegion Region { get; }

    public override string ToString() => $"{Region} [{Register}..{Register + Length - 1}]";

    /// <summary>
    /// One slot's vector span, or part of it — <b>the stimulus, which the client is supposed to write.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>THE RUNTIME REFUSAL LIVES HERE, AND IT IS REACHABLE.</b> The register comes from the map,
    /// but <paramref name="offset"/> and <paramref name="length"/> are the caller's numbers — chunking a
    /// long vector across several FC16s is exactly the arithmetic that could walk off the end. A span that
    /// leaves the vector region is refused BY NAME, saying which region it would have landed in, because
    /// the next region after the vectors is the RESULTS.</para>
    /// </remarks>
    public static MirrorWriteTarget Vector(RegisterMap map, int slotIndex, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (slotIndex < 0 || slotIndex >= map.Slots.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, $"this map holds {map.Slots.Count} slot(s).");

        var slot = map.Slots[slotIndex];

        // A degenerate span is its own answer: it names no registers, so there is no region to report and
        // "would have landed in the results" would be a sentence about nothing.
        if (length < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), length,
                "a write span must cover at least one register. A zero-length write is not a write, and treating it as one "
                + "would put an empty FC16 on the wire and count a round trip for it.");
        }

        if (offset < 0 || offset + length > slot.Vector.Length)
        {
            var first = slot.Vector.Register + offset;
            var last = first + Math.Max(1, length) - 1;

            throw new ArgumentOutOfRangeException(
                nameof(length), length,
                $"a {length}-register write at offset {offset} of slot {slotIndex}'s {slot.Vector.Length}-register vector span "
                + $"would cover registers {first}..{last}, which lies in {Describe(map, first)}..{Describe(map, last)}. "
                + "*** A WRITE THAT LEAVES THE VECTOR REGION IS REFUSED, NOT CLAMPED. *** The region after the vectors is the "
                + "RESULTS, and a write that lands there does not fail — it overwrites an observation, and the run then reports "
                + "the harness's own value as the block's behaviour.");
        }

        return new MirrorWriteTarget(slot.Vector.Register + offset, length, MirrorRegion.Vector);
    }

    /// <summary>The whole start-bool span — the commit, always one transaction.</summary>
    public static MirrorWriteTarget StartBools(RegisterMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new MirrorWriteTarget(map.StartBools.Register, map.StartBools.Length, MirrorRegion.StartBools);
    }

    /// <summary>
    /// The whole start-echo span, <b>for CLEARING during inert (D33)</b>.
    ///
    /// <para>The echo is an observation, and this is the one place a client legitimately writes one — a
    /// latch nobody can clear reports the previous test forever. It is not a result register.</para>
    /// </summary>
    public static MirrorWriteTarget StartEcho(RegisterMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return new MirrorWriteTarget(map.StartEcho.Register, map.StartEcho.Length, MirrorRegion.StartEcho);
    }

    private static string Describe(RegisterMap map, int register) => map.RegionOf(register).ToString();
}
