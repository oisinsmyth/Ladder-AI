namespace Harness.Wire;

/// <summary>
/// One poll's reading of one slot's whole result band, <b>with WHEN it was taken.</b>
///
/// <para>The scan counter is carried because nothing else in this system could answer <i>when was this
/// observed, relative to the thing it is about</i> — and that question going unasked is what produced a
/// confident FAIL against a correct block on 2026-08-17.</para>
/// </summary>
/// <param name="Scan">The control region's scan counter, read in the SAME poll round as these registers.</param>
/// <param name="PollRound">Which poll round of this index produced it. 1-based; the denominator is <see cref="ObservationSeries.PollsObserved"/>.</param>
/// <param name="Registers">The slot's result band as read. A fresh array per poll — never a shared buffer.</param>
public sealed record ResultFrame(ScanCount Scan, int PollRound, ushort[] Registers);

/// <summary>
/// 🔴 <b>EVERY OBSERVATION ONE INDEX PRODUCED, NOT JUST THE ONE THAT HAPPENED TO RECOGNISE COMPLETION.</b>
///
/// <para><b>The defect this exists to close, measured on JOB9004's first live wave 2026-08-17.</b> <c>SlotRun</c> and
/// <c>WaveRun</c> polled in a loop, reassigned a local <c>results</c> each round, and returned only the
/// round that saw the completion register reach its value. Every earlier poll was discarded, and the
/// package then evaluated <i>every</i> expectation against that single snapshot.</para>
///
/// <para><b>Why that is fatal rather than merely lossy.</b> A well-built stimulus model returns the block
/// to inert BEFORE it raises its completion flag — that is required, it is what stops a wave dying after
/// one vector. So <b>the one instant the harness looked at is, by construction, the one instant at which
/// every commanded member is inert.</b> An assertion expecting a commanded state to be TRUE was sampled ~600 ms
/// after the scenario ended, read false, and the package reported FAIL against a block that had been
/// measured doing the right thing on the device from an earlier frame.</para>
///
/// <para><b>THE RETENTION RULE, AND WHY IT IS THIS ONE.</b> A wave has already cost 7,912 round trips, so
/// unbounded history is not an option. What is kept:</para>
/// <list type="number">
/// <item><b>Consecutive-duplicate frames are collapsed.</b> A result band that reads the same as the poll
/// before it carries no new information about VALUE, and the overwhelming majority of polls in a real
/// index are exactly that — the deliverable's 23-register band holds phase codes, latches and commanded
/// states, which change a handful of times across thousands of scans. This is what makes the common case
/// cheap without discarding anything an expectation could need.</item>
/// <item><b>The LAST frame is always retained</b>, even when it duplicates the frame before it, because
/// its SCAN is different and the completion instant is the one every previous version of this code used.
/// Losing it would make the new behaviour incomparable with the old.</item>
/// <item><b>A cap of <see cref="DefaultCap"/> retained frames, and passing it is REPORTED rather than
/// silent.</b> <see cref="Truncated"/> plus <see cref="DistinctFrames"/> against <c>Frames.Count</c> is
/// the denominator: a reader can always tell "the band changed 12 times and we kept 12" from "it changed
/// 900 times and we kept 34".</item>
/// <item>🔴 <b>WHAT THE CAP DROPS IS RESOLUTION, NEVER A PART OF THE INDEX — SEE
/// <see cref="Stride"/>.</b> The retained set is a UNIFORM SAMPLE from the first change to the last, not
/// a prefix of them.</item>
/// </list>
///
/// <para>🔴 <b>THE PREFIX RULE THIS REPLACES, AND WHAT IT COST (D1, measured 2026-08-18).</b> Until this
/// date the cap kept the FIRST 64 distinct frames and dropped every later one, so <b>64 frames bought a
/// fixed ~42–46 seconds of any index on the measured slot</b> — the change rate held at 1.39–1.52 /s across
/// three very different scenarios. On a 369-second index whose observation window opened at 313.8 s the
/// retained series and the window <b>missed each other by ~268 seconds</b>: <b>zero</b> retained frames
/// were inside the phase under test. Five signals were nonetheless folded over those 65 head-and-tail
/// frames and returned verdicts — one <c>Held</c> earned entirely outside the phase, and <b>three
/// <c>Disagreed</c>: accusations against a site's block built from frames taken more than four minutes
/// before the stimulus meant to make them true.</b> <c>Truncated</c> was <c>true</c> on every one of those
/// rows and prevented none of them, which is why the flag alone is not the fix.</para>
///
/// <para><b>THE REPLACEMENT: UNIFORM DECIMATION WITH A DOUBLING STRIDE.</b> Distinct frame <c>d</c> is
/// retained iff <c>(d-1) % stride == 0</c>; when the retained list passes the cap, every second retained
/// frame is dropped and the stride doubles. The retained set is therefore always
/// <c>{d : (d-1) % stride == 0}</c> over the WHOLE index, holds between <c>Cap/2</c> and <c>Cap</c> frames,
/// and costs the same memory as the prefix rule it replaces.</para>
///
/// <para><b>WHY NOT "RESERVE BUDGET FOR FRAMES TAKEN WHILE THE ARM REGISTER IS HIGH", THE OTHER SHAPE
/// PROPOSED.</b> It would have been sharper where it applies, and it does not apply where the damage was
/// done. The arm value is indeed in this same read — but WHICH register holds it is
/// <c>SlotBinding.ArmRegisterOf</c>'s answer, and neither poll loop (<c>SlotRun</c>, <c>WaveRun</c>) is
/// given a binding; so it is new data HERE even though it is not new data on the wire. Worse, it is
/// per-signal and optional: <b>the five signals that produced the false verdicts declared no arm window at
/// all</b>, so on that very slot an arm-reserved budget would have degenerated to exactly the prefix that
/// caused the defect. <i>A retention rule that is only correct when the binding is fully declared fails
/// precisely where a binding is under-declared, which is where this failed.</i> Decimation is
/// unconditional and needs nothing declared. (The two are not exclusive — an arm reservation could be
/// layered on later; it would sharpen the sample, not rescue it.)</para>
///
/// <para>⚠️ <b>WHAT IS STILL LOST, SAID PLAINLY:</b> a band value that stands for fewer than
/// <see cref="Stride"/> distinct changes can fall between two retained frames. That is a loss of TIME
/// RESOLUTION spread evenly over the index, and <see cref="Coverage"/> states it in those words on every
/// report. It is a different and much weaker claim than the old one — <i>this series is not a record of
/// the index</i> — and the fold in <c>SeriesEvaluation</c> is what refuses to convict on the difference.</para>
/// </summary>
/// <param name="Frames">The retained frames, in poll order. At most <see cref="Cap"/> + 1 (the cap, plus the always-kept last).</param>
/// <param name="PollsObserved">How many poll rounds actually read this slot. <b>The denominator.</b></param>
/// <param name="DistinctFrames">How many times the band's VALUE changed (including the first reading). Compare against <c>Frames.Count</c>.</param>
/// <param name="Truncated">Whether the cap was reached, so the index is represented by a SAMPLE rather than by every change.</param>
/// <param name="Cap">The cap this series was recorded under, carried so a report never has to guess it.</param>
public sealed record ObservationSeries(
    IReadOnlyList<ResultFrame> Frames,
    int PollsObserved,
    int DistinctFrames,
    bool Truncated,
    int Cap)
{
    /// <summary>
    /// Distinct frames retained per slot per index.
    ///
    /// <para><b>Sized from the measured cost rather than picked.</b> The deliverable's slot publishes 23
    /// result registers, so 64 frames is 64 × 23 × 2 = ~3 kB per index per slot, and a 27-vector wave
    /// holds ~80 kB. The un-collapsed alternative for the one measured index — 2,833 scans, ~1,978 polls —
    /// is ~91 kB for a band that changed a couple of dozen times.</para>
    ///
    /// <para><b>It is a constant and not a parameter on purpose.</b> One cap, one definition, nothing to
    /// pass differently at two call sites — <c>SlotRun</c> and <c>WaveRun</c> are two poll loops over the
    /// same protocol and a per-call-site cap is how they would come to disagree.</para>
    /// </summary>
    public const int DefaultCap = 64;

    /// <summary>
    /// 🔴 <b>ONE RETAINED FRAME PER THIS MANY DISTINCT BAND CHANGES — the number that says WHICH PART OF
    /// THE INDEX IS REPRESENTED, which <see cref="Truncated"/> on its own never did.</b>
    ///
    /// <para><b>1 means every change was kept.</b> Anything higher means the retained frames are a uniform
    /// sample spread from the first change to the last: the RANGE is still the whole index, and what was
    /// given up is resolution. That distinction is the entire difference between this rule and the prefix
    /// rule it replaced, so it is a field on the series rather than a sentence in a log line — a consumer
    /// deciding whether it may convict has to be able to read it.</para>
    ///
    /// <para><b>An init-only property with a default rather than a positional member</b>, deliberately:
    /// every existing construction site keeps compiling and keeps producing 1, which is exactly right for
    /// the fixtures that state a series by hand and never truncate.</para>
    /// </summary>
    public int Stride { get; init; } = 1;

    /// <summary>
    /// <b>WHICH PART OF THE INDEX THESE FRAMES REPRESENT</b> — printed on every report, truncated or not,
    /// because a coverage line that appears only on bad news teaches its reader that its absence means the
    /// question was not asked.
    /// </summary>
    public string Coverage =>
        PollsObserved == 0
            ? "NO COVERAGE: no poll round read this slot, so no part of this index is represented."
            : Stride <= 1
                ? $"WHOLE INDEX, EVERY CHANGE: all {DistinctFrames} distinct band value(s) are retained."
                : $"WHOLE INDEX, SAMPLED 1 IN {Stride}: the {Frames.Count} retained frame(s) are spread evenly across all "
                  + $"{DistinctFrames} distinct band value(s), from the first change to the last, plus the completing frame. "
                  + $"*** WHAT WAS LOST IS RESOLUTION, NOT A PART OF THE INDEX: a band value standing for fewer than {Stride} "
                  + "distinct change(s) can fall between two retained frames. ***";

    /// <summary>
    /// 🔴 <b>NOTHING WAS OBSERVED — and it is a positive value, not a null.</b>
    ///
    /// <para>Used where the test never started (inert could not be established), which is the one case in
    /// which no poll round ever ran. Its <see cref="PollsObserved"/> is 0, so a consumer reading
    /// <c>Frames.Count == 0</c> can tell "the loop ran and the band never changed" — impossible, since the
    /// first reading is always distinct — from "no loop ran at all".</para>
    /// </summary>
    public static ObservationSeries Empty { get; } =
        new(Array.Empty<ResultFrame>(), 0, 0, false, DefaultCap);

    /// <summary>
    /// A series holding EXACTLY ONE frame — <b>the shape this whole type exists to replace.</b>
    ///
    /// <para>⚠️ <b>DO NOT REACH FOR THIS IN A TEST ABOUT OBSERVATION.</b> A one-frame series cannot tell
    /// the completing instant from any other, so every fold over it agrees with every other fold — which
    /// is precisely the fixture trap that hid the defect: <i>a fixture in which two things coincide is a
    /// fixture that cannot tell them apart.</i> A test about the observation path must build a series
    /// <b>whose last frame and whose in-window frame DISAGREE</b>, or it proves nothing.</para>
    ///
    /// <para>It exists for fixtures that predate the series and are about something else entirely — a
    /// scan-counter wrap, a JSON shape, a vector accounting — where stating a one-frame series is honest
    /// and stating <see cref="Empty"/> would be a lie about a result that plainly holds registers.</para>
    /// </summary>
    public static ObservationSeries OfSingleFrame(ushort[] registers, ScanCount scan) =>
        new(new[] { new ResultFrame(scan, 1, registers ?? Array.Empty<ushort>()) }, 1, 1, false, DefaultCap);

    /// <summary>The last frame observed, or null when nothing was. <b>The instant every earlier version of this code used.</b></summary>
    public ResultFrame? Final => Frames.Count > 0 ? Frames[^1] : null;

    /// <summary>Whether any observation was made at all. <b>Empty is not clean</b> — a caller must ask.</summary>
    public bool Any => Frames.Count > 0;

    /// <summary>The denominator line, printed on every report rather than only on truncated ones.</summary>
    public string Describe() =>
        PollsObserved == 0
            ? "NOTHING OBSERVED: no poll round read this slot at this index."
            : $"{PollsObserved} poll round(s) read this slot; the band changed {DistinctFrames} time(s); "
              + $"{Frames.Count} frame(s) retained (cap {Cap})"
              + (Truncated
                  ? $". *** TRUNCATED at 1 frame in {Stride}: the index is represented by a SAMPLE, not by every change. ***"
                  : ".")
              // *** THE COVERAGE LINE IS UNCONDITIONAL. *** The old truncation warning was on every one of
              // the rows that carried a false accusation and stopped none of them, because it said only
              // THAT frames were dropped. This says WHICH PART OF THE INDEX IS REPRESENTED, which is the
              // question a reader was actually asking.
              + " " + Coverage;
}

/// <summary>
/// Builds an <see cref="ObservationSeries"/> as a poll loop runs — <b>one per slot per index.</b>
///
/// <para>It is a class rather than a fold inside each loop because there are two poll loops
/// (<see cref="SlotRun"/> and <see cref="WaveRun"/>) implementing the same protocol, and the retention
/// rule is exactly the kind of thing that quietly diverges when it is written twice.</para>
/// </summary>
public sealed class ObservationRecorder
{
    private readonly int _cap;
    private readonly List<ResultFrame> _retained = new();

    private ushort[]? _lastValue;
    private ResultFrame? _lastFrame;
    private int _polls;
    private int _distinct;
    private bool _truncated;

    /// <summary>
    /// One retained frame per this many distinct changes. Doubles each time the cap is passed, which is
    /// what keeps the retained set a SAMPLE OF THE WHOLE INDEX rather than a prefix of it.
    /// </summary>
    private int _stride = 1;

    public ObservationRecorder(int cap = ObservationSeries.DefaultCap)
    {
        if (cap < 1)
            throw new ArgumentOutOfRangeException(nameof(cap), cap, "a series that retains nothing is not a series; empty is not clean.");

        _cap = cap;
    }

    /// <summary>Record one poll round's reading of the slot's result band.</summary>
    public void Record(ScanCount scan, int pollRound, ushort[] registers)
    {
        ArgumentNullException.ThrowIfNull(registers);

        _polls++;
        var frame = new ResultFrame(scan, pollRound, registers);
        _lastFrame = frame;

        if (_lastValue is not null && registers.AsSpan().SequenceEqual(_lastValue))
            return;

        _lastValue = registers;
        _distinct++;

        // *** THE SPREAD. *** Distinct frame d is retained iff (d-1) is a multiple of the stride, so the
        // FIRST change is always retained and every retained frame is `stride` changes from its neighbour.
        // The retained set is a uniform sample of the whole index at every moment — including while the
        // index is still running, which matters because the recorder cannot know how long it will be.
        if ((_distinct - 1) % _stride != 0)
            return;

        _retained.Add(frame);

        if (_retained.Count > _cap)
            Thin();
    }

    /// <summary>
    /// Halve the retained set and double the stride — <b>the operation that makes the cap cost RESOLUTION
    /// instead of costing the end of the index.</b>
    ///
    /// <para>Keeping every SECOND retained frame, starting with the first, leaves exactly the frames whose
    /// distinct-index satisfies the DOUBLED stride. So the invariant in <see cref="Record"/> continues to
    /// describe the whole retained set afterwards: no frame is ever retained under one rule and dropped
    /// under another, and the sample never develops a gap.</para>
    /// </summary>
    private void Thin()
    {
        var kept = new List<ResultFrame>((_retained.Count / 2) + 1);

        for (var i = 0; i < _retained.Count; i += 2)
            kept.Add(_retained[i]);

        _retained.Clear();
        _retained.AddRange(kept);
        _stride *= 2;
        _truncated = true;
    }

    /// <summary>
    /// The series, <b>with the last frame guaranteed present.</b>
    ///
    /// <para>The last frame is appended whenever it is not already the last retained one — which happens
    /// both when the cap bit and, far more commonly, when the final polls simply repeated a value already
    /// retained. Its SCAN is the completion instant, and every consumer that used to read
    /// <c>SlotRunResult.Results</c> is asking about that instant.</para>
    /// </summary>
    public ObservationSeries Build()
    {
        var frames = new List<ResultFrame>(_retained);

        if (_lastFrame is not null && (frames.Count == 0 || !ReferenceEquals(frames[^1], _lastFrame)))
            frames.Add(_lastFrame);

        return new ObservationSeries(frames, _polls, _distinct, _truncated, _cap) { Stride = _stride };
    }
}
