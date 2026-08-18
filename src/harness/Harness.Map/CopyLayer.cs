namespace Harness.Map;

/// <summary>
/// The type of a signal the copy layer mirrors — <b>and it decides the rung shape, not just the tag.</b>
///
/// <para>🔴 <b>THIS EXISTS BECAUSE A HARD-CODED <c>"Int"</c> REACHED A CONTROLLER AND TIA REFUSED IT:</b>
/// <c>Data type Bool is not permitted here.</c> Every result register was declared <c>Int</c> and every
/// result rendered as a plain <c>MOVE</c>, and <b>a Bool does not move</b> — it is copied by a coil. That
/// was not a corner: <b>every signal both conformance vector sets observe is a Bool</b>, so the copy
/// layer could not mirror a single asserted signal.</para>
///
/// <para><b><see cref="Unstated"/> is the ZERO VALUE and it is refused by name</b>, following
/// <c>AssertionForm.Unstated</c> and <c>PresetSource.Unstated</c>. A default of <see cref="Int"/> is
/// precisely what produced the defect: the case the field exists for gets handled, and the case where
/// nobody said gets handled the same way and looks identical. There is deliberately no implicit
/// conversion from a bare tag name.</para>
/// </summary>
public enum MirrorValueType
{
    /// <summary><b>Nobody said.</b> A refusal that names the signal — never a silent <see cref="Int"/>.</summary>
    Unstated = 0,

    /// <summary>
    /// A single bit. Declared <c>Bool</c> at a BIT address inside its own register, and copied by a
    /// <c>COIL</c>. <b>Occupies a whole register</b> — packing is an explicit non-goal of this generator,
    /// and one register carrying one value is what keeps the client's register-index arithmetic true.
    /// </summary>
    Bool,

    /// <summary>A 16-bit word. Declared <c>Int</c> at a word address, and copied by a <c>MOVE</c>.</summary>
    Int,

    /// <summary>
    /// A 32-bit duration. Declared <c>Time</c> at a <c>%MD</c> address, copied by a <c>MOVE</c>, and
    /// <b>occupying TWO holding registers</b>.
    ///
    /// <para><b>FORCED, not a nicety:</b> scenarios run to 120 000 ms and a holding register is 16 bits,
    /// so a scenario time cannot be an <c>Int</c> at all. It is the second half of the same defect as
    /// <see cref="Bool"/> — both were the same hard-coded <c>"Int"</c> — which is why the shape and the
    /// width are now read out of one table rather than handled as two special cases.</para>
    ///
    /// <para><b>ITS TWO REGISTERS INHERIT THE 32-BIT WORD ORDER, WHICH IS NOW MEASURED.</b> The value is one
    /// <c>%MD</c> on the PLC and two registers on the wire, exactly like the version register and the
    /// scan counter — and which half lands in the lower register is <c>Harness.Wire</c>'s
    /// <c>RegisterWordOrder</c> — <b>measured HighWordFirst on 2026-08-13 and again on 2026-08-14</b>, so
    /// the default is evidence rather than an inference. <b>A Time read under the wrong order would be out
    /// by 65 536 ms and read as a plausible timing bug</b>, which is why it was worth measuring. A Time
    /// result is exactly as trustworthy as the version register is — and that is now a statement about
    /// two measurements rather than about two guesses.</para>
    /// </summary>
    Time,
}

/// <summary>How a value is copied in a rung. <b>A Bool does not move; a word does not coil.</b></summary>
public enum MirrorCopyShape
{
    /// <summary><c>COIL &lt;destination&gt; := &lt;source&gt;</c>. The only shape TIA accepts for a Bool.</summary>
    Coil,

    /// <summary><c>MOVE(EN := TRUE, IN := &lt;source&gt;) =&gt; &lt;destination&gt;</c>.</summary>
    Move,
}

/// <summary>
/// How a value is addressed in <c>%M</c>, <b>and therefore how many holding registers it occupies.</b>
///
/// <para>The width is DERIVED from this rather than declared beside it, so the two cannot disagree —
/// a table with an address form saying <c>%MD</c> and a width saying 1 would allocate half a value.</para>
/// </summary>
public enum MirrorAddressForm
{
    /// <summary><c>%M&lt;byte&gt;.&lt;bit&gt;</c> — one bit, inside one register.</summary>
    Bit,

    /// <summary><c>%MW&lt;byte&gt;</c> — one register.</summary>
    Word,

    /// <summary><c>%MD&lt;byte&gt;</c> — <b>TWO registers</b>, the same span the version register and the scan counter already occupy.</summary>
    DoubleWord,
}

/// <summary>
/// Everything the copy layer needs to know about one element type, <b>in one row.</b>
///
/// <para>🔴 <b>THIS TABLE IS THE FIX, AND THE TYPES ARE ITS ROWS.</b> <c>Bool</c> and <c>Time</c> were
/// not two bugs — they were one idea the generator did not have: <i>the mirror's element type is not
/// always Int</i>. Both came from the same two hard-coded <c>"Int"</c> literals. Adding a third type is
/// therefore a ROW here, not another branch somewhere; and a type with no row REFUSES BY NAME rather
/// than falling through to Int, which is precisely how the first two survived every test.</para>
/// </summary>
public sealed record MirrorElement(MirrorValueType Type, string IrDataType, MirrorAddressForm Form, MirrorCopyShape Shape)
{
    /// <summary>Holding registers this element occupies. Derived from <see cref="Form"/> — never stated twice.</summary>
    public int Registers => Form == MirrorAddressForm.DoubleWord ? 2 : 1;

    /// <summary>Lowest value this element can carry. Bool is 0..1; Int is S7's signed 16-bit; Time is signed 32-bit ms.</summary>
    public long Minimum => Form switch
    {
        MirrorAddressForm.Bit => 0,
        MirrorAddressForm.Word => short.MinValue,
        _ => int.MinValue,
    };

    /// <summary>Highest value this element can carry.</summary>
    public long Maximum => Form switch
    {
        MirrorAddressForm.Bit => 1,
        MirrorAddressForm.Word => short.MaxValue,
        _ => int.MaxValue,
    };

    /// <summary>True when <paramref name="value"/> survives this element intact.</summary>
    public bool Fits(long value) => value >= Minimum && value <= Maximum;
}

/// <summary>The supported element types. <b>Everything not here is a refusal that names the signal.</b></summary>
public static class MirrorElements
{
    private static readonly IReadOnlyDictionary<MirrorValueType, MirrorElement> Table =
        new[]
        {
            new MirrorElement(MirrorValueType.Bool, "Bool", MirrorAddressForm.Bit, MirrorCopyShape.Coil),
            new MirrorElement(MirrorValueType.Int, "Int", MirrorAddressForm.Word, MirrorCopyShape.Move),

            // Time moves like a word and is addressed like the version register. `MOVE` with Time
            // operands is confirmed against a real TIA-accepted block in the committed corpus
            // (FB_HopperBlockageMonitor NETWORK 3 moves a Time member into a Time member).
            new MirrorElement(MirrorValueType.Time, "Time", MirrorAddressForm.DoubleWord, MirrorCopyShape.Move),
        }.ToDictionary(e => e.Type);

    /// <summary>Every supported type, in declaration order. Also the order networks are emitted in.</summary>
    public static IReadOnlyList<MirrorElement> All => Table.Values.ToArray();

    /// <summary>The row for a type, or <b>null when there is none</b> — which is a refusal, never a default.</summary>
    public static MirrorElement? For(MirrorValueType type) => Table.TryGetValue(type, out var element) ? element : null;

    /// <summary>The row for a type, or a throw. Callers that have already passed the refusal gate use this.</summary>
    public static MirrorElement Require(MirrorValueType type) =>
        For(type) ?? throw new InvalidOperationException(
            $"element type {type} has no row in MirrorElements and reached rendering. The type refusal should have "
            + "stopped it; treating it as Int is what put an unmirrorable copy layer on a controller.");

    /// <summary>The names of every supported type, for a refusal that offers a route rather than only a wall.</summary>
    public static string Supported => string.Join(", ", Table.Keys.Select(t => t.ToString()));
}

/// <summary>Where a signal's <c>Latched</c> mode comes from. <b>Generated is COMPUTED; HandAuthored is TAKEN ON TRUST.</b></summary>
public enum LatchSource
{
    /// <summary>Nothing latches it. The copy layer mirrors it with a plain MOVE or COIL, so only Sampled is available.</summary>
    None,

    /// <summary>
    /// <b>The copy layer emits the latch.</b> Derived from the signal being declared transient — so
    /// <c>Latched</c> is a computed fact about the artifact this harness produces, and it can be read out
    /// of the generated IR rather than believed.
    /// </summary>
    Generated,

    /// <summary>
    /// A block outside the copy layer latches it, named by the binding. <b>Taken on trust and reported as
    /// such</b> — the gate takes the name, not the fact.
    /// </summary>
    HandAuthored,
}

/// <summary>
/// One signal the copy layer mirrors, <b>with the type it actually is</b>.
///
/// <para>The type is carried per signal rather than per slot because a real slot mixes them — the hopper
/// block publishes two Bools, and a counter-style block publishes Ints. <b>There is no constructor that
/// takes a name alone</b>: use <see cref="Bool"/> or <see cref="Int"/>, which put the type in the call.</para>
/// </summary>
public sealed record MirroredSignal(
    string Tag,
    MirrorValueType Type,

    /// <summary>
    /// 🔴 <b>THE SPECIFICATION'S NAME FOR THIS SIGNAL, WHICH IS NOT ALWAYS THE BLOCK'S TAG NAME — AND
    /// UNTIL NOW THE HARNESS ASSUMED IT WAS.</b>
    ///
    /// <para>*** MEASURED THREE INDEPENDENT WAYS IN ONE RUN. *** Gate 5 refused 17 declarations, the
    /// static interface check reported a signal absent from the corpus, and the conflict graph resolved
    /// <b>1 of 17</b> signals to storage — and the one that resolved was the ONLY signal whose spec name
    /// and block name coincide. Wherever the two differ, every mechanical path fails.</para>
    ///
    /// <para><b>The translation had nowhere to live but a prose markdown table, and prose is what no gate
    /// reads.</b> That is why a D1 finding could be laundered: a sentence absorbs a finding, a field does
    /// not. This is the field.</para>
    ///
    /// <para><b>NULL DOES NOT MEAN "SAME AS TAG".</b> That silent identity is the assumption being
    /// removed, so re-introducing it as a default would remove nothing. Null means <i>the binding says
    /// nothing about this signal's specification name</i>, and <see cref="SpecNameStated"/> makes that
    /// visible: a citation that needs the join is NOT CHECKED, naming the signal, rather than being
    /// matched by accident. Two names that genuinely coincide are STATED as coinciding.</para>
    /// </summary>
    string? SpecName = null,

    /// <summary>
    /// <b>The block that LATCHES this signal, when something does — a positive claim, naming who.</b>
    ///
    /// <para>*** FOUR OF THE SEVENTEEN GATE-5 REFUSALS WERE FALSE, AND THIS IS WHY. *** Those signals
    /// genuinely ARE latched on the device, by a hand-authored latch block that the copy-layer generator
    /// did not emit — and the schema had no way to say so, so <b>a real, deployed latch was structurally
    /// undeclarable</b> while the map constructor of the day hard-coded every signal to Sampled. Gate 5
    /// failed in BOTH directions in one run: 13 correct refusals and 4 false ones, from one missing field.</para>
    ///
    /// <para>🔴 <b>IT NAMES A BLOCK RATHER THAN ASSERTING A MODE, AND THAT DISTINCTION IS THE WHOLE
    /// POINT.</b> Replacing the generator's assumption with a caller's assertion would not be a fix — a
    /// caller assertion is forgotten exactly when it matters. A BLOCK NAME IS PROVENANCE: it says which
    /// deployed object does the latching, so the claim is falsifiable against the object set and a
    /// reviewer can go and look. <c>latched: true</c> would have been the caller assertion; this is not
    /// that.</para>
    ///
    /// <para>Null is not "not latched" in the world — it is <b>"this binding claims no latch"</b>, and the
    /// derived observability then offers Sampled alone, which is what the generator actually provides.</para>
    /// </summary>
    string? LatchedBy = null,

    /// <summary>
    /// 🔴 <b>THE SIGNAL IS MOMENTARY — true for about one scan — SO THE COPY LAYER MUST LATCH IT.</b>
    ///
    /// <para>*** THIS IS A PROPERTY OF THE SIGNAL, NOT AN INSTRUCTION TO THE GENERATOR, *** and that is
    /// what keeps the mode DERIVED. The binding states a fact about the block's behaviour, exactly as
    /// <see cref="MirrorValueType"/> does; the generator then DERIVES that a momentary signal needs a
    /// sticky bit and emits one. <c>Latched</c> becomes a computed consequence, never a declaration.</para>
    ///
    /// <para><b>Why false is a safe default, stated rather than assumed.</b> Forgetting to declare a
    /// transient signal yields NO latch, so a <c>Latched</c> expectation on it is REFUSED by gate 5 —
    /// loud, and at the gate, before anything is spent. Over-latching would be the silent direction:
    /// wasted registers, nothing reported. <b>The default's failure mode is a refusal, not a pass.</b></para>
    ///
    /// <para>⚠️ <b>DO NOT LATCH EVERYTHING.</b> A latch on a signal that does not need one costs a
    /// register and hides nothing — and there is a converse trap worth carrying: <b>a block latching its
    /// own output is a VALUE UNDER TEST, not instrumentation.</b> Those stay Sampled; latching them again
    /// in the copy layer would mean the harness observing its own latch rather than the block's.</para>
    /// </summary>
    bool Transient = false,

    /// <summary>
    /// 🔴 <b>THE SIGNAL FIRES ONCE PER VECTOR INDEX, AND EACH FIRING MUST BE DISTINGUISHABLE FROM THE
    /// LAST.</b> A property of the signal, exactly as <see cref="Transient"/> is — the generator DERIVES
    /// the consequence.
    ///
    /// <para>*** THE CONSEQUENCE IT DERIVES IS A PHASE-ARMED LATCH, AND UNTIL 2026-08-14 IT WAS A
    /// REFUSAL. *** The generated latch used to be an UNCONDITIONAL <c>SCOIL</c>: it set on the first
    /// firing and stayed set for the rest of the wave. That is correct for a signal asserted once per
    /// WAVE and wrong for one asserted once per INDEX — index 2's latch would already be high from index
    /// 1, so every later firing reads identical to the first and a signal that never fired again reads as
    /// one that did. Rather than emit that, the generator refused by name.</para>
    ///
    /// <para><b>What it emits instead</b> — the shape the hand-authored <c>FB_HarnessViolationLatch</c>
    /// already uses, and which that block's own comments justify:</para>
    /// <code>
    ///   SCOIL &lt;latch&gt; := &lt;slot start bool&gt; AND &lt;ArmedBy&gt; AND &lt;signal&gt;
    ///   RCOIL &lt;latch&gt; := NOT &lt;slot start bool&gt;
    /// </code>
    ///
    /// <para><b>THE PER-INDEX WINDOW IS THE START BOOL, WHICH THE MIRROR ALREADY CARRIES AND THE CLIENT
    /// ALREADY DRIVES.</b> <c>InertPhase.Establish</c> calls <c>LowerAllStartBools()</c> before EVERY
    /// index and <c>Commit</c> raises it after the verify, so the slot's start bool is already a
    /// client-written level that goes low between indices. A second client-written arm band would have
    /// been a new mirror register, a new <c>MirrorClient</c> verb and a new wave step, all doing what the
    /// start bool already does. <b>The arm band costs ZERO registers because the seam already ships.</b></para>
    ///
    /// <para><b>The RESET is on a LEVEL, not on an edge</b>, for the reason
    /// <c>FB_HarnessViolationLatch</c> network 6 states: a latch cleared on the start EDGE is cleared
    /// again by a harness restart mid-run and the evidence goes with it, whereas a level re-clears
    /// whenever the harness is not running an index.</para>
    ///
    /// <para>⚠️ <b>THE DEFAULT'S FAILURE MODE IS SILENT, AND THAT IS THE ASYMMETRY WITH
    /// <see cref="Transient"/>.</b> Forgetting <c>Transient</c> yields no latch and gate 5 refuses a
    /// Latched expectation — loud, and before anything is spent. Forgetting THIS yields the one-shot
    /// latch, which compiles, deploys and reads plausibly. <b>It still cannot be defaulted the other
    /// way</b> without making every ordinary once-per-wave transient re-arm, so the asymmetry is
    /// recorded here rather than closed — but the gap it pointed at is now closed, and the loud half is
    /// no longer the only half that works.</para>
    ///
    /// <para><b>WIDTH:</b> one register per latch, in the existing latch band, unchanged. On the
    /// deliverable hopper vector set that is <b>35 → 37 registers</b> (two block outputs wanting
    /// <c>Latched</c>), so it needs a re-deploy of a mirror that is exactly full at 35.</para>
    ///
    /// <para><b>WHAT IS STILL REFUSED, BY NAME:</b> a re-arming transient on a slot whose
    /// <see cref="SlotBinding.StartCondition"/> is null. D37 makes that null a CLAIM — "this block is
    /// purely reactive and has no start gate" — and such a slot has no per-index level of any kind, so
    /// there is nothing to arm against and nothing to clear on.</para>
    /// </summary>
    bool RearmsEachIndex = false,

    /// <summary>
    /// 🔴 <b>THE SIGNAL THAT OPENS THE OBSERVATION WINDOW INSIDE AN INDEX — a tag in the program, named
    /// by the binding, never invented here.</b>
    ///
    /// <para>The start bool re-arms the latch per INDEX. This narrows the window further, INSIDE one
    /// index, and it is what the coordinator's own capability request asked for in those words:
    /// <i>"THE LATCHES MUST BE ARMED BY THE STIMULUS MODEL'S PHASE FLAG (HBA_Stim.Armed), not
    /// free-running from T=0"</i>. The reason is in <c>FB_HarnessViolationLatch</c>'s block comment:
    /// <i>every run legitimately begins by driving the monitor through a clear-down, and a free-running
    /// latch would fill from that and read violated whatever the monitor did.</i></para>
    ///
    /// <para><b>It is ANDed WITH the start bool, not instead of it</b>, and that is deliberate: an arm
    /// flag is a static of the block under test, and <b>the harness must not assume the block clears its
    /// own phase flag at inert.</b> Holding the window closed at both ends costs one contact and removes
    /// an assumption about an implementation the harness is supposed to be testing.</para>
    ///
    /// <para>Null is not "always armed" and not "no window" — it is <b>the binding claiming no in-index
    /// window</b>, which leaves the whole index armed. That is the correct reading for a signal whose
    /// every firing inside an index is of interest.</para>
    ///
    /// <para><b>It is inert on a signal with no generated latch, so it is REFUSED there</b> rather than
    /// accepted and ignored: a caller who declares an arm and gets no latch believes they have a
    /// phase-armed observation and has an unconditional one, or none.</para>
    /// </summary>
    string? ArmedBy = null,

    /// <summary>
    /// 🔴 <b>HOW A CITED VALUE BECOMES THE INTEGER THIS MEMBER HOLDS — and its absence is why 81 values
    /// of the deliverable could not be written at all.</b>
    ///
    /// <para>Null is <b>"the cited text IS the value"</b>, which is right for every plain duration and is
    /// the shape every existing binding entry has. It is not a claim that no encoding exists; it is the
    /// absence of one, and a symbolic value meeting it is refused by <see cref="MirrorValueFit"/> rather
    /// than coerced.</para>
    ///
    /// <para><b>TWO ENTRIES MAY SHARE A <see cref="SpecName"/>, EACH WITH ITS OWN ENCODING.</b> That is
    /// the whole of the two-tag form <c>harness-binding.md:57</c> needs: one set-B field, two IR members,
    /// one cited value read twice under two different rules. Nothing special-cases it — the join key is
    /// the spec name and an encoding is per entry, so the capability falls out of both.</para>
    /// </summary>
    ValueEncoding? Encoding = null,

    /// <summary>
    /// 🔴 <b>WHAT THIS SIGNAL READS WHEN NOTHING IS RUNNING — D33's first check, declared per signal
    /// instead of assumed for the whole band.</b>
    ///
    /// <para>*** THE ONLY PRODUCTION CALLER USED TO HARDCODE ZERO FOR EVERY RESULT REGISTER. *** See
    /// <see cref="InertRest"/> for the measured consequence; the short form is that a hardcoded 0 fails in
    /// BOTH directions — it refuses a signal that legitimately rests at a <c>-1</c> sentinel, and it
    /// ACCEPTS a stale <c>0</c> on a signal where 0 is a measured PASS verdict.</para>
    ///
    /// <para><b>NULL IS NOT ZERO AND IT IS NOT "DON'T CARE".</b> It is the absence of a claim, and the loop
    /// REFUSES on it — <c>Harness.Wire.InertRestPlan</c> carries the ruling and its reasoning. That is the
    /// same treatment <see cref="SpecName"/>, <c>MirrorValueType.Unstated</c> and
    /// <see cref="SlotBinding.StartCondition"/> already get, for the same reason: a default that is right
    /// for the binding somebody happens to be looking at is wrong for the next one, silently.</para>
    ///
    /// <para><b>It is meaningful only on a RESULT source.</b> A vector target is written by the harness at
    /// every inert phase, so its resting value is whatever the next index declares — a fact the harness
    /// already holds and never needs to be told.</para>
    /// </summary>
    InertRest? Rest = null)
{
    /// <summary>
    /// True when the binding has stated a specification name. <b>Distinct from the names being equal</b> —
    /// an unstated name is a join nobody made, and a stated identity is a join somebody made and found
    /// trivial.
    /// </summary>
    public bool SpecNameStated => !string.IsNullOrWhiteSpace(SpecName);

    /// <summary>The name a vector cites, or null when the binding never said. <b>Never falls back to <see cref="Tag"/>.</b></summary>
    public string? CitableName => SpecNameStated ? SpecName!.Trim() : null;

    /// <summary>
    /// 🔴 <b>THE KEY A VECTOR'S <c>inputs</c> DICTIONARY IS ACTUALLY WRITTEN UNDER — ONE DEFINITION, USED
    /// BY EVERY PATH THAT JOINS THE TWO DOCUMENTS.</b>
    ///
    /// <para>*** IT EXISTS BECAUSE TWO PATHS DERIVED IT SEPARATELY AND ONLY ONE OF THEM WAS FIXED. ***
    /// <c>MirrorValueFit</c> was corrected on 2026-08-14 to join on the SPEC name, with the finding that
    /// joining on the tag made the width gate examine ZERO of the deliverable's 27 vectors.
    /// <c>LoopRun.ToWireVector</c> — <b>the code that actually writes the stimulus</b> — kept joining on
    /// <c>Tag</c>, so on the deliverable it matched nothing, wrote nothing, and left every stimulus
    /// register at ZERO while the width gate reported the values it had checked as fitting. <b>A gate that
    /// examines the right thing and a writer that writes the wrong one is worse than both being wrong</b>,
    /// because the gate's green is then evidence for the writer.</para>
    ///
    /// <para>The tag is used <b>only</b> where the binding stated no spec name at all, which is a weaker
    /// join and is reported as such wherever it is made.</para>
    /// </summary>
    public string JoinKey => CitableName ?? Tag;

    /// <summary>
    /// <b>The instrumentation modes this signal can actually be watched in — DERIVED, never declared.</b>
    ///
    /// <para><c>Sampled</c> is the FLOOR, and it is computed rather than declared: the generator emits a
    /// result-register MOVE or COIL for every mirrored signal, and <b>no scan stamp</b> — a named absence
    /// in its own documentation, not an oversight.</para>
    ///
    /// <para>🔴 <b>IT NO LONGER SAYS "THE GENERATOR EMITS NO PER-SIGNAL LATCH", BECAUSE THE LINE BELOW
    /// FALSIFIES IT.</b> <see cref="LatchClaimed"/> reads <c>… || Transient</c>, and the generator has
    /// emitted <c>ResultLatch</c> networks since the transient latch landed. The stale sentence was cited
    /// by <c>docs/notes/spec-reconciliation.md</c> item 2.1 as its authority for "no latches are
    /// generated", so <i>both went stale together and the citation kept reading as corroboration while
    /// pointing at changed text</i> — a comment something else cites is not a comment, it is a
    /// load-bearing claim with a footnote.</para>
    ///
    /// <para><b>A latch is claimed two ways, and the difference is the whole point of
    /// <see cref="LatchSource"/>.</b> <see cref="Transient"/> makes it GENERATED — a computed fact about
    /// the artifact this harness emits, readable out of the emitted IR. <see cref="LatchedBy"/> makes it
    /// HAND-AUTHORED, naming the block that does it, which the generator cannot know because that latch is
    /// not its output; that one is admitted on PROVENANCE rather than on a caller's word.</para>
    ///
    /// <para><b>The mode SET is assembled in <c>Harness.Results</c>, not here</b>, because
    /// <c>InstrumentationMode</c> lives downstream and this assembly stays dependency-free. What lives
    /// here is the FACT — whether a latch is claimed, and by whom.</para>
    /// </summary>
    public bool LatchClaimed => !string.IsNullOrWhiteSpace(LatchedBy) || Transient;

    /// <summary>
    /// Where the latch comes from — <b>and the distinction is the whole point of keeping the mode derived.</b>
    /// </summary>
    public LatchSource LatchSource =>
        Transient ? LatchSource.Generated
        : !string.IsNullOrWhiteSpace(LatchedBy) ? LatchSource.HandAuthored
        : LatchSource.None;

    /// <summary>
    /// The extra mirror register this signal needs for its latch, or 0. <b>A latch is a Bool and takes a
    /// register of its own</b> — packing is an explicit non-goal, and the client's index is the offset.
    /// </summary>
    public int LatchRegisters => Transient ? 1 : 0;

    /// <summary>
    /// <b>This signal's generated latch is PHASE-ARMED</b> — set only inside the armed window and cleared
    /// whenever the slot is not running an index.
    ///
    /// <para>Both terms are required and neither implies the other: <see cref="Transient"/> alone is the
    /// once-per-WAVE latch, which the unconditional form expresses correctly and must keep getting.</para>
    /// </summary>
    public bool PhaseArmed => Transient && RearmsEachIndex;

    /// <summary>True when the binding names an in-index arm window. <b>Distinct from the window being open.</b></summary>
    public bool ArmWindowStated => !string.IsNullOrWhiteSpace(ArmedBy);

    /// <summary>The arm tag, or null when the binding named none. <b>Never falls back to anything.</b></summary>
    public string? ArmWindow => ArmWindowStated ? ArmedBy!.Trim() : null;

    /// <summary>
    /// Who latches it — <b>the provenance a reviewer can check.</b>
    ///
    /// <para>For a GENERATED latch this is the copy layer itself, which is a stronger answer than a block
    /// name: the latch is in the artifact this harness emits and can be read out of it. For a
    /// hand-authored one it is the block the binding named, taken on trust and reported as such.</para>
    /// </summary>
    public string? LatchProvenance => LatchSource switch
    {
        LatchSource.Generated when PhaseArmed =>
            "the generated copy layer, PHASE-ARMED (derived: this signal is declared transient and re-arming"
            + (ArmWindowStated ? $", armed by '{ArmWindow}'" : ", armed by the slot's start bool alone") + ")",

        LatchSource.Generated => "the generated copy layer (derived: this signal is declared transient)",
        LatchSource.HandAuthored => LatchedBy!.Trim(),
        _ => null,
    };

    /// <summary>A Bool signal — mirrored to one bit of its own register, by a coil.</summary>
    public static MirroredSignal Bool(string tag) => new(tag, MirrorValueType.Bool);

    /// <summary>An Int signal — mirrored to a whole register, by a MOVE.</summary>
    public static MirroredSignal Int(string tag) => new(tag, MirrorValueType.Int);

    /// <summary>A Time signal — 32-bit, mirrored to a <c>%MD</c> spanning TWO registers, by a MOVE.</summary>
    public static MirroredSignal Time(string tag) => new(tag, MirrorValueType.Time);

    /// <summary>Several Time signals, in order.</summary>
    public static IReadOnlyList<MirroredSignal> Times(params string[] tags) => tags.Select(Time).ToArray();

    /// <summary>
    /// Holding registers this signal occupies — <b>1 for a Bool or an Int, 2 for a Time.</b>
    ///
    /// <para>Zero when the type is unsupported, so a caller summing widths over an unchecked binding
    /// under-counts rather than silently reserving one register for something that has no shape. The
    /// generator refuses such a binding before any arithmetic depends on it.</para>
    /// </summary>
    public int Registers => MirrorElements.For(Type)?.Registers ?? 0;

    /// <summary>Several Bool signals, in order.</summary>
    public static IReadOnlyList<MirroredSignal> Bools(params string[] tags) => tags.Select(Bool).ToArray();

    /// <summary>Several Int signals, in order.</summary>
    public static IReadOnlyList<MirroredSignal> Ints(params string[] tags) => tags.Select(Int).ToArray();

    public override string ToString() =>
        $"{Tag} : {Type}" + (SpecNameStated ? $" (spec '{SpecName}')" : " (spec name UNSTATED)")
        + (string.IsNullOrWhiteSpace(LatchedBy) ? string.Empty : $" latched by {LatchedBy}");
}

/// <summary>
/// What one slot's registers are wired to in the program under test.
///
/// <para>The coordinator owns this side entirely: per D13 instrumentation is a property of the COPY
/// LAYER, not of the blocks under test, so nothing here asks the block's author for anything except
/// the names of signals that already exist.</para>
/// </summary>
/// <param name="VectorTargets">
/// Tags the vector registers are moved INTO, in register order. May be empty — a pure observation
/// vector writes nothing (see <c>TestVector.IsReadOnly</c>).
/// </param>
/// <param name="StartCondition">
/// The block's EXISTING start condition, bound to the slot's start bool (D37).
///
/// <para><b>Null is a claim, not a blank.</b> D37: "not every block has one, and the absence is
/// meaningful rather than a default" — a purely reactive block (a comparator, a level alarm) has no
/// start gate and is held inert by its input values alone. So a null here asserts "this block has no
/// start gate", which <see cref="CopyLayerPlan.NoStartGateAsserted"/> records so the assertion is
/// visible rather than looking like a generator that forgot.</para>
///
/// <para>It binds to a start condition the block already has, discovered — never to a test-only input
/// added for the purpose, which is scaffolding inside the block under test and exactly what DB-5
/// forbids: what ships would then not be what was tested.</para>
/// </param>
/// <param name="ResultSources">
/// Signals copied OUT into the result registers, in register order — <b>each with its type</b>, which
/// decides both the mirror tag and the rung shape. One signal occupies one register whatever its width;
/// packing is an explicit non-goal, and the client's register index is this list's index.
/// </param>
public sealed record SlotBinding(
    string SlotId,
    IReadOnlyList<MirroredSignal> VectorTargets,
    string? StartCondition,
    IReadOnlyList<MirroredSignal> ResultSources)
{
    /// <summary>
    /// 🔴 <b>THE SPECIFICATION SLOT IDS THIS ONE BINDING SLOT SERVES — the many-to-one map, and it is what
    /// makes six ids resolve to one mirror region.</b>
    ///
    /// <para>*** FOUR INDEPENDENT AUTHORITIES SAY THE HOPPER SET'S SIX IDS ARE ONE SLOT'S SERIAL PHASES,
    /// AND NONE OF THEM IS AN AGENT'S OPINION: *** the coordinator's prose
    /// (<c>harness-binding.md:173-186</c>, <c>:179-181</c> — all six drive the SAME instance and the SAME
    /// members); D9's computed 15 conflict edges; the submission's own <c>slotsInWaveSet: 1</c>, whose note
    /// reads <i>"how many slots run CONCURRENTLY, not how many exist"</i>; and the multi-writer refusal in
    /// <see cref="CopyLayerGenerator"/>, which makes six real slots UNBUILDABLE on one stimulus instance.</para>
    ///
    /// <para><b>It needs NO PARTITION, which is part of why it is right.</b> A many-to-one map assigns no
    /// signal to any group: every served id addresses the same vector band, the same result band and the
    /// same start bool. The alternative — six real slots — would need six monitor instances, six stimulus
    /// instances and six input buffers, which the prose itself calls a project-shape decision
    /// (<c>md:187-189</c>).</para>
    ///
    /// <para><b>Empty means the slot serves ITSELF</b> — the ordinary one-id case, unchanged, and the
    /// shape every binding had before this existed. When it is non-empty the slot's own
    /// <see cref="SlotId"/> stops being citable: it is then an internal key (it is the tag fragment, and
    /// the map's hash input), and a vector citing it would have no group rank at all. <b>There is no
    /// correct rank to give such a vector, and inventing one is the defect this whole field exists to
    /// avoid</b>, so it falls out as unbound and <c>SlotJoin</c> says so by name.</para>
    /// </summary>
    public IReadOnlyList<string> Serves { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 🔴 <b>A POSITIVE CLAIM THAT <see cref="Serves"/>' ORDER IS THE ORDER THE GROUPS RUN IN. Without it
    /// a multi-group slot REFUSES, and it does not fall back to the order the ids happen to be listed
    /// in.</b>
    ///
    /// <para>*** THE SUBMISSION CARRIES NO TOTAL ORDER, MEASURED: *** each group restarts <c>index</c> at
    /// 0, so the deliverable's 27 vectors carry only six distinct index values. The wave reads
    /// <c>distribution.Results[ordinal]</c>, so merging six groups without a total order gives <b>six
    /// vectors all reading <c>Results[0]</c> and 21 of 27 packages carrying another vector's run</b> —
    /// reported confidently, with no error anywhere. The id therefore has to become the MAJOR key of a
    /// two-level ordinal <c>(group, index)</c>.</para>
    ///
    /// <para><b>WHY THE ARRAY'S ORDER IS NOT ENOUGH ON ITS OWN.</b> A list is a set until somebody says it
    /// is a sequence. Reading the order out of it anyway would be a default — and <i>a default here is a
    /// guess wearing a mechanism's clothes</i>, which is precisely the class of thing that produces a
    /// plausible artifact instead of a refusal. The group order is also not a free choice: one of the
    /// hopper groups is a CPU stop/restart across a disruptive download boundary, so where it sits changes
    /// what the groups after it start from.</para>
    ///
    /// <para><b>When the coordinator states it, the DATA supplies the answer and nothing in this code
    /// changes.</b> That is the test of whether this is a mechanism or a decision.</para>
    /// </summary>
    public bool ServesRunInOrder { get; init; }

    /// <summary>
    /// 🔴 <b>SERVED GROUPS THAT DO NOT RUN INLINE, BECAUSE A DOWNLOAD BOUNDARY HAPPENS INSIDE THEM.</b>
    ///
    /// <para>*** THE PROPERTY IS THE VECTORS' OWN AND IT IS WRITTEN IN THEIR OWN FIELDS. *** The hopper
    /// set's <c>STARTUP</c> vectors declare that they ride an already-scheduled disruptive boundary which
    /// <b>stops and restarts the CPU</b>, and that the harness is <b>disconnected across the download</b>
    /// with nobody polling while the first scan happens.</para>
    ///
    /// <para><b>They are not a late group in a serial sequence — they are not IN one.</b> Folding such a
    /// group into the inline order runs a restart-spanning scenario with no restart, and leaves every
    /// group after it running against a cleared accumulator and a reconnected harness that the sequence
    /// never accounted for. It reads as a clean pass, which is why the model has to be able to say this
    /// rather than the scheduler having to be careful.</para>
    ///
    /// <para><b>Position inside <see cref="Serves"/> is not read</b> — a boundary-spanning group takes no
    /// inline ordinal at all — so it may be listed anywhere.</para>
    /// </summary>
    public IReadOnlyList<string> BoundarySpanning { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 🔴 <b>THE NAMED MIGRATION ESCAPE FOR THIS SLOT'S UNDECLARED RESTING VALUES — and it is a
    /// DECLARATION, not a fallback.</b>
    ///
    /// <para><b>The ruling it exists under:</b> a result register whose signal declares no
    /// <see cref="MirroredSignal.Rest"/> REFUSES the run. That is the honest reading of
    /// <c>InertDeclaration</c>'s own contract sentence, and it is the only one of the three candidate
    /// readings whose failure mode is loud — see <c>Harness.Wire.InertRestPlan</c>, which states all three
    /// and why the other two were rejected.</para>
    ///
    /// <para><b>Why an escape exists at all:</b> the ruling lands on a slot that is DEPLOYED AND RUNNING
    /// against the hardcoded-zero behaviour. A fail-closed gate that refuses working submissions on the day
    /// it lands is removed within a week, by someone who is right to — so the migration is a single stated
    /// line in the document that already owns every other instrumentation fact, rather than a revert.</para>
    ///
    /// <para><b>What it costs, so that it is not free:</b> it is per SLOT, so it cannot be set project-wide
    /// by accident; it requires <see cref="AssumedZeroRestBasis"/> and refuses without one; every register
    /// it covers is counted as DEFAULTED rather than declared, listed by index in the run's inert-rest
    /// report, and named as a default in the text of any inert failure that fires on it — <b>so a reader who
    /// sees the check fail is told the expectation may be the wrong half of the disagreement.</b></para>
    ///
    /// <para>It does NOT cover a signal that declares its own rest: an explicit declaration always wins, and
    /// the escape only ever fills a hole.</para>
    /// </summary>
    public bool AssumedZeroRest { get; init; }

    /// <summary>Why this slot's undeclared resting values may be assumed zero. <b>Required when <see cref="AssumedZeroRest"/> is set.</b></summary>
    public string? AssumedZeroRestBasis { get; init; }

    /// <summary>
    /// The specification slot ids a vector may cite for this slot, in the order they were declared.
    /// <b>Never empty</b> — a slot always answers to at least its own id.
    ///
    /// <para><b>It includes the boundary-spanning ones.</b> Such a vector IS bound — it addresses this
    /// slot's mirror region perfectly well — and it is UNSCHEDULABLE here, which is a different fact.
    /// Dropping it from the citable set would make <c>SlotJoin</c> report it as naming a slot nobody
    /// bound, sending its reader to fix the wrong document.</para>
    /// </summary>
    public IReadOnlyList<string> CitableSlotIds =>
        Serves.Count > 0 ? Serves : new[] { SlotId };

    /// <summary>True when this slot answers to more than one specification id, and therefore needs an order.</summary>
    public bool ServesSeveralGroups => Serves.Count > 1;

    /// <summary>
    /// The register offset of each result signal, and the total width — <b>a running sum, not the list
    /// index.</b>
    ///
    /// <para>🔴 <b>THE TWO STOPPED BEING THE SAME NUMBER THE MOMENT A TIME EXISTED.</b> A Time occupies
    /// two registers, so the signal at list position 1 after a Time sits at register 2. Anything still
    /// using <c>IndexOf(signal)</c> as a register offset reads the WRONG HALF of the value ahead of it —
    /// silently, with a plausible number. Every register offset in this system comes from here.</para>
    /// </summary>
    public IReadOnlyList<int> ResultRegisterOffsets => Offsets(ResultSources);

    /// <summary>The same running sum over the vector targets.</summary>
    public IReadOnlyList<int> VectorRegisterOffsets => Offsets(VectorTargets);

    /// <summary>
    /// Total result registers this binding needs. <b>Not the signal count, and not the value width alone</b>
    /// — a transient signal also carries a LATCH register, so the budget moves when a latch is added.
    /// </summary>
    public int ResultRegistersNeeded =>
        (ResultSources ?? Array.Empty<MirroredSignal>()).Sum(s => s.Registers)
        + LatchRegistersNeeded;

    /// <summary>
    /// Registers this binding's LATCHES occupy — one per transient result signal, appended AFTER the
    /// values so the value offsets are unchanged by adding a latch.
    /// </summary>
    public int LatchRegistersNeeded => (ResultSources ?? Array.Empty<MirroredSignal>()).Sum(s => s.LatchRegisters);

    /// <summary>
    /// Register offset of each transient signal's latch, keyed by the signal's tag. <b>Latches live in
    /// their own band after the values</b>: interleaving them would move every later value offset the
    /// moment a latch was added, and the client's arithmetic is the value offsets.
    /// </summary>
    public IReadOnlyDictionary<string, int> LatchRegisterOffsets
    {
        get
        {
            var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
            var next = (ResultSources ?? Array.Empty<MirroredSignal>()).Sum(s => s.Registers);

            foreach (var signal in (ResultSources ?? Array.Empty<MirroredSignal>()).Where(s => s.Transient))
            {
                offsets[signal.Tag] = next;
                next += 1;
            }

            return offsets;
        }
    }

    /// <summary>Total vector registers this binding needs.</summary>
    public int VectorRegistersNeeded => (VectorTargets ?? Array.Empty<MirroredSignal>()).Sum(s => s.Registers);

    /// <summary>
    /// The register offset a result signal sits at, <b>keyed on the name a VECTOR cites</b> — see
    /// <see cref="MirroredSignal.JoinKey"/> — or -1 when this binding does not carry it.
    ///
    /// <para>🔴 <b>THIS READ <see cref="MirroredSignal.Tag"/> UNTIL 2026-08-17, AND IT IS THE THIRD
    /// INDEPENDENT DERIVATION OF THE SAME JOIN TO BE FOUND WRONG.</b> <c>MirrorValueFit</c> and
    /// <c>LoopRun.ToWireVector</c> were both moved onto <see cref="MirroredSignal.JoinKey"/> on
    /// 2026-08-14, with the finding that joining on the tag matched NOTHING on a real deliverable. This —
    /// <b>the OBSERVE side, where an assertion is turned into a register to read</b> — was not, so the
    /// STIMULUS reached the block and the OBSERVATION never left the PC.</para>
    ///
    /// <para><b>Measured on the first live wave (2026-08-17):</b> the wave polled 7,912 times, the mirror
    /// feed records the whole 23-register result band being read, and <b>all four declared assertions came
    /// back <c>&lt;never read&gt;</c></b> — because the vectors cite <c>SPEC.FaultAlarm</c>,
    /// <c>SPEC.HoldCommand</c>, <c>SPEC.EngageCommand</c> while this method was asking for
    /// <c>iDB_Widget.IO.Fault</c> and its
    /// siblings. Every one of the four resolves under <see cref="MirroredSignal.JoinKey"/>.</para>
    ///
    /// <para><b>-1 rather than 0</b>, because 0 is a real offset and a caller that cannot tell them apart
    /// reads the first register for every signal it does not have. <b>No caller may default it</b> —
    /// <c>LoopRun</c> refuses the submission above the device boundary instead.</para>
    /// </summary>
    public int ResultRegisterOf(string citedSignal)
    {
        var offsets = ResultRegisterOffsets;

        for (var i = 0; i < ResultSources.Count; i++)
        {
            if (string.Equals(ResultSources[i].JoinKey, citedSignal, StringComparison.Ordinal))
                return offsets[i];
        }

        return -1;
    }

    /// <summary>
    /// 🔴 <b>THE LATCH REGISTER A CITED RESULT SIGNAL'S LATCH SITS AT, or -1 when this binding generates
    /// none for it. THE OBSERVE-SIDE CONSUMER OF <see cref="LatchRegisterOffsets"/>, WHICH UNTIL
    /// 2026-08-17 HAD NONE AT ALL.</b>
    ///
    /// <para>*** THE LATCHES WERE ARGUED FOR, SIZED, GENERATED, DEPLOYED AND READ OFF THE WIRE — AND NEVER
    /// CONSULTED. *** <c>LatchRegisterOffsets</c>' only consumer was <c>CopyLayerGenerator</c>, i.e. the
    /// code that EMITS the latch rungs. <c>LoopRun</c> resolved every expectation through
    /// <see cref="ResultRegisterOf"/> — the VALUE offsets — whatever mode the expectation declared, so
    /// <c>Latched</c> and <c>Sampled</c> were the same thing at the wire: one read of the value register at
    /// completion. Measured on the first live wave 2026-08-17, where all three of one vector's expectations declared
    /// <c>Latched</c>, all three signals had generated phase-armed latches sitting in the very same FC03
    /// the harness read every poll, and not one of them was looked at.</para>
    ///
    /// <para><b>The two keys are bridged HERE and nowhere else.</b> A vector cites the SPECIFICATION's name
    /// (<see cref="MirroredSignal.JoinKey"/>); <see cref="LatchRegisterOffsets"/> is keyed on the IR
    /// <see cref="MirroredSignal.Tag"/>, because that key is emitted into LAD as the latch rung's own
    /// source expression. So this resolves cited name → signal → tag → latch offset, in one place, rather
    /// than leaving a caller to do it and get the key wrong for the fourth time.</para>
    ///
    /// <para>⚠️ <b>-1 COVERS TWO DIFFERENT FACTS AND A CALLER MUST NOT COLLAPSE THEM WITH A FALLBACK:</b>
    /// this binding does not carry the cited name at all, or it carries it and the signal is not
    /// <see cref="MirroredSignal.Transient"/> so no latch was generated. <b>Neither may fall back to the
    /// value register</b> — that fallback is precisely how <c>Latched</c> became a synonym for
    /// <c>Sampled</c>, silently, for the whole life of the feature.</para>
    /// </summary>
    public int LatchRegisterOf(string citedSignal)
    {
        var signal = ResultSignal(citedSignal);
        if (signal is null)
            return -1;

        return LatchRegisterOffsets.TryGetValue(signal.Tag, out var offset) ? offset : -1;
    }

    /// <summary>
    /// 🔴 <b>THE REGISTER CARRYING THE ARM SIGNAL THIS RESULT SIGNAL'S OBSERVATION WINDOW IS DEFINED BY, or
    /// -1 when there is no such register to read.</b>
    ///
    /// <para><b>What it answers:</b> <i>was this observation taken inside the window the binding declared
    /// for it?</i> Nothing asked that before, and on the measured run nothing needed to be broken for the
    /// answer to be "no": the model's own arm flag was in the same 23-register read, and the
    /// snapshot every assertion was evaluated against has it reading <b>0</b>.</para>
    ///
    /// <para><b>It is derived from the signal's OWN <see cref="MirroredSignal.ArmedBy"/> and from nothing
    /// else.</b> Several signals in a binding may name the same arm tag, and it is tempting to treat that
    /// tag as the SLOT's window and apply it to signals that declared none. That would be an inference
    /// about a signal from its neighbours, and inferring a window is how an observation acquires a
    /// confidence it was not given: a signal with no declared window gets <c>-1</c>, and the outcome then
    /// says the window was UNKNOWN rather than assuming it was open.</para>
    ///
    /// <para><b>-1 also covers an arm tag that is not itself mirrored in this slot's result band</b> — the
    /// binding may legitimately arm a latch from a tag it never publishes, in which case the latch still
    /// works on the controller and the PC simply cannot see the window. That is a limit, not a failure,
    /// and it reads as UNKNOWN for the same reason.</para>
    /// </summary>
    public int ArmRegisterOf(string citedSignal)
    {
        if (ResultSignal(citedSignal)?.ArmWindow is not { } armTag)
            return -1;

        var offsets = ResultRegisterOffsets;

        for (var i = 0; i < ResultSources.Count; i++)
        {
            if (string.Equals(ResultSources[i].Tag, armTag, StringComparison.Ordinal))
                return offsets[i];
        }

        return -1;
    }

    /// <summary>
    /// The result signal a vector's cited name resolves to — so a reader can ask what type it is about to
    /// decode. <b>Keyed on <see cref="MirroredSignal.JoinKey"/>, exactly as
    /// <see cref="ResultRegisterOf"/> is</b>: the two are read together on every observation, and a pair
    /// that joined on different keys would return a register for one name and a type for another.
    /// </summary>
    public MirroredSignal? ResultSignal(string citedSignal) =>
        ResultSources.FirstOrDefault(s => string.Equals(s.JoinKey, citedSignal, StringComparison.Ordinal));

    /// <summary>
    /// The result signal at a given <b>IR TAG</b> — the generator's lookup, and deliberately NOT the same
    /// method as <see cref="ResultSignal"/>.
    ///
    /// <para><b>The two keys are not interchangeable and the distinction is load-bearing.</b>
    /// <see cref="LatchRegisterOffsets"/> is keyed on <see cref="MirroredSignal.Tag"/> because its key is
    /// then emitted into LAD as the latch rung's own source expression — a spec name there would render a
    /// coil reading a symbol no PLC tag table declares. So the generator asks by tag, the observation path
    /// asks by cited name, and each says which it means in its own signature rather than one method
    /// silently trying both.</para>
    /// </summary>
    public MirroredSignal? ResultSignalByTag(string tag) =>
        ResultSources.FirstOrDefault(s => string.Equals(s.Tag, tag, StringComparison.Ordinal));

    private static IReadOnlyList<int> Offsets(IReadOnlyList<MirroredSignal>? signals)
    {
        var offsets = new List<int>();
        var next = 0;

        foreach (var signal in signals ?? Array.Empty<MirroredSignal>())
        {
            offsets.Add(next);
            next += signal?.Registers ?? 0;
        }

        return offsets;
    }
}

/// <summary>What one generated network does. The plan is inspectable before any IR is rendered.</summary>
public enum CopyLayerNetworkKind
{
    /// <summary>
    /// Publishes the build stamp into the version register (§9). Unconditional, one per program, and a
    /// LITERAL — the constant lives in the code, so it can only be present if that code is running.
    /// </summary>
    Version,

    /// <summary>Increments the free-running scan counter. Unconditional, one per program.</summary>
    ScanCounter,

    /// <summary>Moves mirror registers into the block's inputs.</summary>
    VectorIn,

    /// <summary>Drives the block's start condition from the slot's start bool.</summary>
    StartBool,

    /// <summary>
    /// Latches the block's OWN start condition back into the echo register (X-E) — what the program
    /// actually ran, as opposed to what the client commanded.
    /// </summary>
    StartEcho,

    /// <summary>Moves the block's outputs into the slot's result registers.</summary>
    ResultsOut,

    /// <summary>
    /// <b>Latches a MOMENTARY result into a sticky bit the client reads and clears.</b>
    ///
    /// <para>The alternative was downgrading such assertions to Sampled, which *** LOOKS LIKE PROGRESS AND
    /// IS NOT: *** it converts a strong assertion into one that can silently miss. A sampled assertion
    /// landing in a poll gap is a silent wrong answer, not an error — and a poll IS one round trip, so no
    /// polling rate recovers a one-scan event. That is why this network exists rather than a relaxation.</para>
    ///
    /// <para><b>It carries BOTH forms</b> — the unconditional latch for a once-per-wave transient, and the
    /// PHASE-ARMED latch (<c>SCOIL</c> under an arm, <c>RCOIL</c> under the start bool's inverse) for one
    /// that re-arms each index. They are one network kind because <c>ir/SPEC.md</c> groups
    /// <c>COIL</c>/<c>SCOIL</c>/<c>RCOIL</c> as ONE statement kind, so the generator's
    /// one-kind-per-network rule is not broken by putting the resets beside the sets.</para>
    /// </summary>
    ResultLatch,
}

/// <summary>
/// One copy: where the value comes from, where it goes, and <b>what type it is</b> — which is what
/// decides whether the rung is a <c>MOVE</c> or a <c>COIL</c>.
/// </summary>
public sealed record CopyLayerCopy(string From, string To, MirrorValueType Type);

/// <summary>
/// One generated latch rung set: the mirror bit, the signal that sets it, and <b>the window it is set
/// inside</b>.
///
/// <para><b><see cref="ClearLevel"/> is what separates the two forms, and it is a level rather than a
/// bool on purpose.</b> An unconditional latch has none — nothing in the copy layer ever clears it, and
/// the client does that over the wire during inert. A phase-armed one names the signal whose ABSENCE
/// clears it, which is what makes the emitted <c>RCOIL</c> derivable from this record instead of from a
/// flag plus a separately-remembered tag.</para>
/// </summary>
/// <param name="LatchTag">The mirror bit the latch drives.</param>
/// <param name="Signal">The observed signal.</param>
/// <param name="ArmTerms">
/// Every term ANDed AHEAD of the signal, in emission order. Empty for the unconditional form.
/// </param>
/// <param name="ClearLevel">
/// The level whose inverse resets the latch, or null for the unconditional form.
/// </param>
public sealed record CopyLayerLatch(
    string LatchTag,
    string Signal,
    IReadOnlyList<string> ArmTerms,
    string? ClearLevel)
{
    /// <summary>True when this latch is armed and cleared by the copy layer rather than only by the client.</summary>
    public bool PhaseArmed => ClearLevel is not null;

    /// <summary>
    /// The <c>SCOIL</c> right-hand side. <b>The signal is LAST</b>, so a reader meets the window before
    /// the thing being observed — the same order <c>FB_HarnessViolationLatch</c> writes its guards in.
    /// </summary>
    public string SetExpression => string.Join(" AND ", ArmTerms.Append(Signal));
}

/// <summary>
/// One generated network: its kind, its title, and every copy in it.
///
/// <para><b>A network holds copies of ONE type.</b> IR groups statements within a network by kind in a
/// fixed order (<c>ir/SPEC.md</c>: COIL before MOVE), so a mixed slot emits one network per type rather
/// than one network that has to be ordered correctly. The generator's standing rule — one KIND per
/// network makes the ordering rule unreachable rather than merely satisfied.</para>
/// </summary>
/// <param name="Latches">
/// The latch rungs, when this is a <see cref="CopyLayerNetworkKind.ResultLatch"/> network. <b>Empty on
/// every other kind, and the network's <see cref="Moves"/> are DERIVED from it</b> at the one place both
/// are built — two independently-populated views of one rung set is exactly how a report comes to
/// disagree with what was emitted.
/// </param>
public sealed record CopyLayerNetwork(
    int Number,
    CopyLayerNetworkKind Kind,
    string Title,
    IReadOnlyList<CopyLayerCopy> Moves,
    IReadOnlyList<CopyLayerLatch>? Latches = null)
{
    /// <summary>The latch rungs, never null.</summary>
    public IReadOnlyList<CopyLayerLatch> LatchRungs => Latches ?? Array.Empty<CopyLayerLatch>();
}

/// <summary>One artifact the generator emits, as IR text ready to hand to the converter.</summary>
public sealed record HarnessObject(string Name, HarnessObjectKind Kind, string Ir);

/// <summary>The kinds of object the harness generates. Used by <see cref="RetentionCheck"/> to pick its rules.</summary>
public enum HarnessObjectKind
{
    /// <summary>A PLC tag table declaring the mirror's symbolic names at their <c>%M</c> addresses.</summary>
    TagTable,

    /// <summary>A code block — the copy layer FC.</summary>
    Block,

    /// <summary>A data block. The harness generates none in phase 2; the rule exists because it is where retain bites.</summary>
    DataBlock,

    /// <summary>
    /// 🔴 <b>A PLC DATA TYPE (a UDT) — and its absence is why the hopper program could not be supplied to
    /// <c>--program</c> WHOLE.</b>
    ///
    /// <para>*** MEASURED: <c>FB_HopperBlockageMonitor</c> AND ITS INSTANCE DB BOTH REFERENCE
    /// <c>UDT_HopperBlockageIO</c>. *** With no member for a type, <c>ProgramUnderTest</c> refused a
    /// <c>TYPE</c> header by name — correctly, since the nearest member would have handed a UDT a data
    /// block's retention rules and put it in the downloadable set the load manifest is compared against —
    /// but the refusal blocked the block under test from being declared at all, and <b>the build stamp is
    /// a hash of what is about to run</b>.</para>
    ///
    /// <para><b>It is imported and compiled like a block, and it produces NO LOAD MESSAGE, like a tag
    /// table.</b> It is a distinct member rather than a re-use of either because it is on a different side
    /// of each of those two lines, and a kind that is wrong about one of them is wrong silently.</para>
    /// </summary>
    DataType,
}

/// <summary>
/// The copy layer for one slot: the mirror tags it declares, the networks it will emit, and the two
/// facts about the binding that must not be inferred from silence.
/// </summary>
public sealed record CopyLayerPlan(
    RegisterMap Map,
    IReadOnlyList<SlotBinding> Bindings,
    BuildStamp Stamp,
    IReadOnlyList<MirrorTag> Tags,
    IReadOnlyList<CopyLayerNetwork> Networks,
    IReadOnlyList<string> SlotsAssertingNoStartGate)
{
    /// <summary>
    /// How every slot id became the identifier fragment inside its tag names — <b>one entry per slot,
    /// transliterated or not.</b> See <see cref="SlotTagToken"/> for why the two are different things.
    /// </summary>
    public IReadOnlyList<SlotTokenDerivation> SlotTokens { get; init; } = Array.Empty<SlotTokenDerivation>();

    /// <summary>
    /// 🔴 <b>Result-source tags that MORE THAN ONE slot observes — admitted, counted, and named.</b>
    ///
    /// <para>Shared OBSERVATION is legitimate and is the case this generator exists to admit: the copy
    /// layer only READS a result source, and each slot writes it into its own register band, so N slots
    /// watching one signal is N reads of one tag and no interference of any kind. The deliverable's six
    /// hopper slots are exactly this shape — <c>harness-binding.md:179-181</c> states that all six drive
    /// the same instance and the same members.</para>
    ///
    /// <para><b>Shared STIMULUS is the opposite and is refused</b> — see the multi-writer refusal in
    /// <see cref="CopyLayerGenerator"/>. The two directions look symmetrical and are not, so this list is
    /// reported on every run rather than only when it is non-empty: a report that appears only when it has
    /// something to say cannot be told from one that has stopped running.</para>
    /// </summary>
    public IReadOnlyList<string> SharedObservations { get; init; } = Array.Empty<string>();

    /// <summary>The single binding, for a one-slot wave set.</summary>
    public SlotBinding Binding => Bindings.Count == 1
        ? Bindings[0]
        : throw new InvalidOperationException($"this plan covers {Bindings.Count} slots; ask for the one you mean.");

    /// <summary>Vector registers actually wired for a slot. Registers beyond this are declared by nothing and read by nothing.</summary>
    public int BoundVectorRegisters(string slotId) => Bindings.Single(b => b.SlotId == slotId).VectorTargets.Count;

    /// <summary>Result registers actually wired for a slot.</summary>
    public int BoundResultRegisters(string slotId) => Bindings.Single(b => b.SlotId == slotId).ResultSources.Count;

    /// <summary>
    /// True when EVERY slot asserts it has no start gate. D37: a null start condition is a CLAIM about
    /// the block ("this one is purely reactive and is held inert by its input values alone"), never a
    /// blank, so it is recorded per slot rather than collapsed to one flag.
    /// </summary>
    public bool NoStartGateAsserted => SlotsAssertingNoStartGate.Count == Bindings.Count;
}

/// <summary>One symbolic name in the mirror tag table, with the <c>%M</c> address it sits at.</summary>
public sealed record MirrorTag(string Name, string DataType, string Address, int ByteAddress, string Comment);

/// <summary>A copy layer, or the reasons there is not one.</summary>
public sealed record CopyLayerResult(CopyLayerPlan? Plan, IReadOnlyList<HarnessObject> Objects, IReadOnlyList<string> Refusals)
{
    public bool Generated => Plan is not null;

    public CopyLayerPlan Require() =>
        Plan ?? throw new InvalidOperationException(
            "copy-layer generation refused:" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", Refusals));
}
