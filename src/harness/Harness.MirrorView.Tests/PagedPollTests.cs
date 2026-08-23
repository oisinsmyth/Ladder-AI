using Harness.Map;
using Harness.MirrorRead;
using Harness.Wire;
using NModbus;

namespace Harness.MirrorView.Tests;

/// <summary>
/// 🔴 <b>THE VIEWER REPORTED TRANSACTIONS IT NEVER ISSUED.</b>
///
/// <para>Every label in <c>MirrorPoller</c> was hardcoded <c>FC03(0, n)</c> — one request over the whole
/// declared area — while the read underneath it had been paged since <c>ae11c3c</c>. On the rig's real
/// width that is nine transactions reported as one, and on a refusal at register 1000 the page said the
/// server had turned down <c>FC03(0, 1024)</c>: a request nobody made, at an address nobody asked about,
/// followed by an inference about the DEVICE drawn from a page boundary the reader was never told existed.
/// This is the same defect <c>0c3b43a</c> fixed in <c>harness-mirror-read</c>, and these tests follow that
/// commit's shape.</para>
///
/// <para>⚠️ <b>WHY A NEW FIXTURE, AND WHY THE 95 EXISTING ATTRIBUTES COULD NOT HAVE CAUGHT THIS.</b>
/// <see cref="Fixtures.Registers"/> is 6, so <c>PerformPaged</c> short-circuits to a single
/// <c>Perform</c> and the loop never runs; and <see cref="ScriptedSource"/> ignores <c>count</c>
/// altogether, so a nine-page read would concatenate the same array nine times and produce nonsense
/// rather than a failure. Every fixture in this assembly answered any width happily. The one thing that
/// would not is the real transport — which is exactly the blindness that hid the mirror-read defect from
/// a 47-test suite. <see cref="ProtocolLimitedSource"/> enforces the two protocol rules and keeps them
/// APART, the way the real path does.</para>
/// </summary>
public class PagedPollTests
{
    /// <summary>Three pages of 125: 125 + 125 + 50. Wide enough that the page count is not 1 or 2.</summary>
    private const int WideRegisters = 300;

    private const int LastWideRegister = WideRegisters - 1;

    /// <summary>
    /// A server holding <paramref name="available"/> registers that enforces BOTH protocol rules, and
    /// keeps them apart the way the real path does.
    ///
    /// <para>The quantity rule is the CLIENT'S — a <see cref="WireException"/> thrown before any packet
    /// leaves, which lands on <c>TransportFailed</c>. The address rule is the SERVER'S — a
    /// <see cref="SlaveException"/> with exception code 2, which lands on <c>RefusedByServer</c>. One is a
    /// failed measurement and the other is a fact about the address space; a fixture that blurred them
    /// would exercise the wrong branch of the switch under test. Modelled on
    /// <c>Harness.MirrorRead.Tests.WholeAreaCheckTests.ProtocolLimitedSource</c>.</para>
    /// </summary>
    private sealed class ProtocolLimitedSource : IRegisterSource
    {
        private readonly ushort[] _registers;
        private readonly Action? _afterRead;

        internal ProtocolLimitedSource(int available, Action? afterRead = null)
        {
            _registers = new ushort[available];
            _afterRead = afterRead;

            // The build stamp, so the model has something real to render.
            if (available > 1)
            {
                _registers[0] = 0xF52E;
                _registers[1] = 0xCEAD;
            }
        }

        /// <summary>Every request, in order. "Nothing was split" is then a fact rather than an inference.</summary>
        internal List<(int Start, int Count)> Requests { get; } = new();

        public ushort[] Read(int startRegister, int count)
        {
            Requests.Add((startRegister, count));

            if (count > ModbusLimits.MaxReadRegisters)
            {
                throw new WireException(
                    $"FC03 read of {count} register(s) exceeds the {ModbusLimits.MaxReadRegisters}-register " +
                    "protocol limit. The client refuses this before a packet leaves.");
            }

            if (startRegister < 0 || startRegister + count > _registers.Length)
            {
                throw new SlaveException(
                    "scripted: Function Code: 3, Exception Code: 2 - Illegal Data Address "
                    + $"(asked for {count} at {startRegister}; this server holds 0..{_registers.Length - 1}).");
            }

            var answer = _registers[startRegister..(startRegister + count)];
            _afterRead?.Invoke();
            return answer;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// A map of <paramref name="registers"/> words, built <b>through the parser from text</b> exactly as
    /// <see cref="Fixtures"/> builds its six-register one — never by constructing tags by hand.
    /// </summary>
    private static MirrorMap MapOf(int registers)
    {
        const string tagText = """
TAGTABLE Wide
  ROOTID 0
  TAGS
    HX_ProgramVersion 1 : DWord @ %MD1000 ACCESSIBLE VISIBLE WRITABLE COMMENT "Build stamp of the downloaded IR set."
    HX_ScanCount 4 : DInt @ %MD1004 ACCESSIBLE VISIBLE WRITABLE COMMENT "Free-running scan counter."
    HX_V000 7 : Int @ %MW1008 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 0."
    HX_R000 A : Bool @ %M1011.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 0."
""";

        var areaText = $"  MB_SERVER(MbServer, EN := TRUE, MB_HOLD_REG := P#M1000.0 WORD {registers}, CONNECT := Comms)";
        var load = MirrorMapParser.Parse(tagText, areaText, "wide.ir", "wide-area.ir");
        Assert.True(load.Ok, string.Join(" | ", load.Refusals));
        return load.Map!;
    }

    private static MirrorViewOptions OptionsFor(int registers, int pollMs = 1000, int staleMs = 3000) =>
        new("10.10.10.10", 503, 1, "allowlist.json", registers, pollMs, staleMs, 8137);

    /// <summary>One poller over one state, so the model can be built from what the poll published.</summary>
    private static (MirrorPoller Poller, MirrorState State) RigWithState(
        MirrorViewOptions options, IRegisterSource source, TestClock clock)
    {
        var state = new MirrorState();
        var poller = new MirrorPoller(options, state, new AllowingFence(), new RecordingFactory(() => source), clock.Read);
        return (poller, state);
    }

    // ---- THE FIXTURE'S OWN CONTROLS. A fake that silently ignores `count` is what hid this. ---------

    /// <summary>
    /// *** THE HOLE THAT MADE THE DEFECT INVISIBLE, ASSERTED SHUT. *** <see cref="ScriptedSource"/>
    /// returns its standing array whatever is asked for. If this fixture did the same, every test below
    /// would pass against a viewer that issued one impossible request.
    /// </summary>
    [Fact]
    public void TheFixture_RefusesAQuantityNoFC03CanCarry()
    {
        var source = new ProtocolLimitedSource(WideRegisters);

        var thrown = Assert.Throws<WireException>(() => source.Read(0, WideRegisters));

        Assert.Contains($"{ModbusLimits.MaxReadRegisters}-register", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>And it answers with EXACTLY what was asked for — the other half of honouring the count.</summary>
    [Fact]
    public void TheFixture_AnswersExactlyTheCountItIsAsked()
    {
        var source = new ProtocolLimitedSource(WideRegisters);

        Assert.Equal(125, source.Read(0, 125).Length);
        Assert.Equal(50, source.Read(250, 50).Length);
    }

    // ---- THE PAGING ITSELF. Already correct; this is the guard, and it holds in both directions. ----

    /// <summary>
    /// The viewer pages. Asserted against the REQUESTS the server saw, not against an outcome — an
    /// outcome would still be green if a future edit sent one 300-register request to a fixture that
    /// tolerated it.
    /// </summary>
    [Fact]
    public void AWideArea_IsReadInPagesNoLargerThanFC03Carries()
    {
        var source = new ProtocolLimitedSource(WideRegisters);
        var (poller, _) = RigWithState(OptionsFor(WideRegisters), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(PollOutcome.Ok, attempt.Outcome);
        Assert.Equal(3, source.Requests.Count);
        Assert.All(source.Requests, r => Assert.True(r.Count <= ModbusLimits.MaxReadRegisters));
    }

    // ---- SITE 1: THE LABEL ON EVERY HEALTHY POLL ---------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE HEALTHY LABEL NAMES WHAT WAS ISSUED.</b> Three FC03s reported as
    /// <c>FC03(0, 300)</c> is a claim about a transaction the protocol cannot carry and this program
    /// never sent — and it sits on screen continuously, which is what makes it the worst of the three.
    /// </summary>
    [Fact]
    public void AHealthyMultiPagePoll_NamesTheTransactionsItIssued_AndNotOneItDidNot()
    {
        var source = new ProtocolLimitedSource(WideRegisters);
        var (poller, _) = RigWithState(OptionsFor(WideRegisters), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Contains($"3 x FC03 -> 0..{LastWideRegister}", attempt.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain($"FC03(0, {WideRegisters})", attempt.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>THE MISSING DENOMINATOR.</b> Follow mode has reported its transaction count since it existed
    /// (<c>FeedProvenance.Frames</c>). Direct mode is now the mode that PAGES and it was the one without
    /// a count. Asserted through the JSON because that is the page's only data source: a property nothing
    /// serialises is a number the reader never sees.
    /// </summary>
    [Fact]
    public void AHealthyMultiPagePoll_StatesHowManyTransactionsComposedTheReading()
    {
        var source = new ProtocolLimitedSource(WideRegisters);
        var (poller, state) = RigWithState(OptionsFor(WideRegisters), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        poller.PollOnce();
        var json = MirrorJson.Serialize(MirrorView.Build(MapOf(WideRegisters), OptionsFor(WideRegisters), state, Fixtures.T0));

        Assert.Contains("\"transactions\": 3", json, StringComparison.Ordinal);
        Assert.Contains("\"transactionsPerPoll\": 3", json, StringComparison.Ordinal);
    }

    // ---- SITE 2: THE REFUSAL, AND THE INFERENCE IT DREW ABOUT THE DEVICE ---------------------------

    /// <summary>
    /// 🔴 <b>A FAILING PAGE IS REPORTED AS ITSELF.</b> The server here holds 200 registers and the map
    /// declares 300, so page 1 (0..124) answers and page 2 (125..249) is refused. Reporting that as
    /// <c>FC03(0, 300)</c> claims a boundary at an address that was never asked for —
    /// <c>RegisterRead.PerformPaged</c> returns the failing page's own Start and Count for exactly this
    /// reason, and the printer was throwing it away.
    /// </summary>
    [Fact]
    public void ARefusalMidArea_NamesTheFailingPage_AndNotTheWholeSpan()
    {
        var source = new ProtocolLimitedSource(200);
        var (poller, _) = RigWithState(OptionsFor(WideRegisters), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(PollOutcome.RefusedByServer, attempt.Outcome);
        Assert.Contains("FC03(125, 125)", attempt.Detail, StringComparison.Ordinal);
        Assert.Contains("page 2 of 3", attempt.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain($"FC03(0, {WideRegisters})", attempt.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>AND IT SAYS WHAT ANSWERED FIRST.</b> Paging is fail-fast, so a refusal at page 2 means page 1
    /// came back — the area is at least 125 registers wide. The old text threw that away and offered
    /// "the area is narrower than the map declares" with no width attached, which is the weaker claim
    /// AND the unfalsifiable one.
    /// </summary>
    [Fact]
    public void ARefusalMidArea_SaysWhatAnsweredBeforeIt()
    {
        var source = new ProtocolLimitedSource(200);
        var (poller, _) = RigWithState(OptionsFor(WideRegisters), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Contains("registers 0..124 answered", attempt.Detail, StringComparison.Ordinal);
        Assert.Contains("at least 125", attempt.Detail, StringComparison.Ordinal);
    }

    /// <summary>The first page refusing has no earlier page to cite, and must not invent one.</summary>
    [Fact]
    public void ARefusalOnTheFirstPage_ClaimsNothingAnsweredBeforeIt()
    {
        var source = new ProtocolLimitedSource(10);
        var (poller, _) = RigWithState(OptionsFor(WideRegisters), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(PollOutcome.RefusedByServer, attempt.Outcome);
        Assert.Contains("FC03(0, 125)", attempt.Detail, StringComparison.Ordinal);
        Assert.Contains("no page answered before it", attempt.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answered before it, so the area is at least", attempt.Detail, StringComparison.Ordinal);
    }

    // ---- THE DEAD PREMISE: "every register shares one instant by construction" ---------------------

    /// <summary>
    /// 🔴 <b>NINE TRANSACTIONS ARE NINE MOMENTS.</b> The null <c>RegisterObservedUtc</c> in direct mode
    /// rested on "one FC03 covers the whole area, so every register shares <c>At</c> by construction".
    /// Above 125 registers that premise is false and the page was asserting a coherence it did not have.
    /// Each register now carries the instant ITS OWN page returned.
    /// </summary>
    [Fact]
    public void AMultiPageReading_GivesEachRegisterTheInstantItsOwnPageReturned()
    {
        var clock = new TestClock(Fixtures.T0);
        var source = new ProtocolLimitedSource(WideRegisters, () => clock.Advance(TimeSpan.FromSeconds(2)));
        var (poller, _) = RigWithState(OptionsFor(WideRegisters), source, clock);
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.NotNull(attempt.RegisterObservedUtc);
        Assert.Equal(WideRegisters, attempt.RegisterObservedUtc!.Length);
        Assert.Equal(Fixtures.T0.AddSeconds(2), attempt.RegisterObservedUtc[0]);
        Assert.Equal(Fixtures.T0.AddSeconds(4), attempt.RegisterObservedUtc[125]);
        Assert.Equal(Fixtures.T0.AddSeconds(6), attempt.RegisterObservedUtc[LastWideRegister]);
    }

    /// <summary>
    /// <b>AND THE PAGE CAN THEN AGE THEM APART.</b> This is the payoff for populating the provenance
    /// rather than printing a caveat: a register read six seconds ago goes grey on its own while the
    /// newest page stays green, instead of the whole table sharing the newest instant.
    /// </summary>
    [Fact]
    public void AMultiPageReading_AgesRowsIndependently_SoAnOldPageCannotLookCurrent()
    {
        var clock = new TestClock(Fixtures.T0);
        var source = new ProtocolLimitedSource(WideRegisters, () => clock.Advance(TimeSpan.FromSeconds(2)));
        var options = OptionsFor(WideRegisters);
        var (poller, state) = RigWithState(options, source, clock);
        using var _p = poller;

        poller.PollOnce();

        // Last page returned at T0+6; the window is 3 s; "now" is T0+7. Page 3 is inside it, page 1 is not.
        var model = MirrorView.Build(MapOf(WideRegisters), options, state, Fixtures.T0.AddSeconds(7));

        Assert.True(model.RegistersStale > 0, "the first page is 5 s old and must not read as current.");
        Assert.True(model.RegistersCurrent > 0, "the last page is 1 s old and must not read as stale.");
        Assert.Equal(WideRegisters, model.RegistersCurrent + model.RegistersStale + model.RegistersNotObserved);
        Assert.False(model.Rows[0].Current);
        Assert.True(model.Rows[LastWideRegister].Current);
    }

    /// <summary>
    /// The page must consult the count rather than asserting one instant unconditionally. Asserted on the
    /// markup because the sentence is a literal in it, and a fix that left the literal standing would
    /// print "every row below shares one instant" over rows the model has just aged apart.
    /// </summary>
    [Fact]
    public void ThePageMarkup_ConsultsTheTransactionCount_RatherThanAssertingOneInstant()
    {
        Assert.Contains("transactionsPerPoll", MirrorPage.Html, StringComparison.Ordinal);
        Assert.Contains("reassembly", MirrorPage.Html, StringComparison.OrdinalIgnoreCase);
    }

    // ---- THE NEGATIVE CONTROLS. A single-page area must read EXACTLY as it did. --------------------

    /// <summary>
    /// *** THE NEGATIVE CONTROL, AND IT MUST BE RUN. *** Six registers is one FC03, the premise holds,
    /// and every word of the old report is still the right one. A fix that gave a single-page area paging
    /// vocabulary would be trading one false claim for another.
    /// </summary>
    [Fact]
    public void ASinglePageArea_KeepsItsExistingOkWording()
    {
        var source = new ProtocolLimitedSource(Fixtures.Registers);
        var (poller, _) = RigWithState(Fixtures.Options(), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(PollOutcome.Ok, attempt.Outcome);
        Assert.StartsWith($"FC03(0, {Fixtures.Registers}) answered in ", attempt.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("x FC03", attempt.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("reassembly", attempt.Detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A single-page refusal keeps its diagnosis: there, the whole area really was one request.</summary>
    [Fact]
    public void ASinglePageRefusal_KeepsItsExistingWording()
    {
        var (poller, _) = RigWithState(Fixtures.Options(), new ServerRefusingSource(), new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(PollOutcome.RefusedByServer, attempt.Outcome);
        Assert.Contains($"the server REFUSED FC03(0, {Fixtures.Registers})", attempt.Detail, StringComparison.Ordinal);
        Assert.Contains("the area is narrower than the map declares", attempt.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("page 1 of", attempt.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>AND IT SHIPS NO MASK.</b> With one transaction the attempt's own instant IS every register's
    /// instant; a per-register copy of it would be a second thing that can disagree with the first.
    /// </summary>
    [Fact]
    public void ASinglePageArea_ShipsNoPerRegisterMask()
    {
        var source = new ProtocolLimitedSource(Fixtures.Registers);
        var (poller, _) = RigWithState(Fixtures.Options(), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Null(attempt.RegisterObservedUtc);
    }

    /// <summary>The single-page sentence on the page itself, kept verbatim.</summary>
    [Fact]
    public void ThePageMarkup_KeepsItsSinglePageSentence()
    {
        Assert.Contains(
            "one FC03 over the whole declared area per poll, so every row below shares one instant.",
            MirrorPage.Html,
            StringComparison.Ordinal);
    }

    /// <summary>A single-page reading still reports its one transaction — the denominator, printed always.</summary>
    [Fact]
    public void ASinglePageReading_StillStatesItsOneTransaction()
    {
        var source = new ProtocolLimitedSource(Fixtures.Registers);
        var (poller, state) = RigWithState(Fixtures.Options(), source, new TestClock(Fixtures.T0));
        using var _p = poller;

        poller.PollOnce();
        var json = MirrorJson.Serialize(MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0));

        Assert.Contains("\"transactions\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"transactionsPerPoll\": 1", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// A poll that never reached the wire composed its reading from NOTHING, and says zero rather than
    /// inheriting the count a healthy poll would have made.
    /// </summary>
    [Fact]
    public void APollTheFenceRefused_ReportsNoTransactionsAtAll()
    {
        var state = new MirrorState();
        var poller = new MirrorPoller(Fixtures.Options(), state, new RefusingFence(),
            new RecordingFactory(() => new ProtocolLimitedSource(Fixtures.Registers)), new TestClock(Fixtures.T0).Read);
        using var _p = poller;

        var attempt = poller.PollOnce();
        var json = MirrorJson.Serialize(MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0));

        Assert.Equal(PollOutcome.RefusedByFence, attempt.Outcome);
        Assert.Contains("\"transactions\": 0", json, StringComparison.Ordinal);
    }
}
