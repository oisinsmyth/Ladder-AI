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
/// <para><b>The defect this exists to close, measured on JOB9004 2026-08-17.</b> <c>SlotRun</c> and
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
/// <item><b>A cap of <see cref="DefaultCap"/> distinct frames, and passing it is REPORTED rather than
/// silent.</b> <see cref="Truncated"/> plus <see cref="DistinctFrames"/> against <c>Frames.Count</c> is
/// the denominator: a reader can always tell "the band changed 12 times and we kept 12" from "it changed
/// 900 times and we kept 64".</item>
/// </list>
///
/// <para>⚠️ <b>WHAT IS LOST WHEN THE CAP BITES, SAID PLAINLY:</b> the frames dropped are the LATER
/// distinct ones, not a sample across the index. A band containing a free-running counter changes every
/// scan, so such a slot retains only its first 64 changes and its last frame — and the honest reading of
/// <c>Truncated</c> is <i>this series is not a record of the index</i>, never <i>nothing else
/// happened</i>. It is not silently decimated, because a decimated series looks complete.</para>
/// </summary>
/// <param name="Frames">The retained frames, in poll order. At most <see cref="Cap"/> + 1 (the cap, plus the always-kept last).</param>
/// <param name="PollsObserved">How many poll rounds actually read this slot. <b>The denominator.</b></param>
/// <param name="DistinctFrames">How many times the band's VALUE changed (including the first reading). Compare against <c>Frames.Count</c>.</param>
/// <param name="Truncated">Whether the cap was reached and later distinct frames were NOT retained.</param>
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
                  ? ". *** TRUNCATED: later distinct frames were NOT retained, so this series is not a record of the index. ***"
                  : ".");
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

        if (_retained.Count < _cap)
            _retained.Add(frame);
        else
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

        return new ObservationSeries(frames, _polls, _distinct, _truncated, _cap);
    }
}
