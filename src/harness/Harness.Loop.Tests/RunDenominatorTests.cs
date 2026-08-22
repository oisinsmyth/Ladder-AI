using System.Text.Json;
using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Run;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE DENOMINATOR, END TO END — through the real loop, the real wave and the real report.</b>
///
/// <para><see cref="VectorAccountingTests"/> proves the decision procedure. This proves it is WIRED:
/// that a wave which stops short really does produce a full account, that the console report and the
/// JSON artifact both read against the submitted total, and that <b>the number of packages and the
/// number of <c>Ran</c> dispositions are the same number by construction</b> rather than by two pieces
/// of code happening to agree.</para>
///
/// <para><b>Why both halves are needed.</b> A pure function with no caller is the shape this project has
/// been bitten by repeatedly — a component that can only be asserted about has a class of defect no
/// assertion reaches. The report is the deliverable; the account is only how it is computed.</para>
/// </summary>
public class RunDenominatorTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    private const int Submitted = 6;

    /// <summary>
    /// Transactions allowed through before the mirror freezes.
    ///
    /// <para><b>Chosen so that at least one index completes and at least one does not.</b> The tests
    /// below never assert this number or the index it lands on — they assert the RELATION between the
    /// counts, because pinning "the slot stopped at index 2" would be a fact about how many round trips
    /// the client happens to make, not about the accounting.</para>
    /// </summary>
    private const int LiveTransactions = 40;

    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(256, MirrorBase);

    private static AssertionEnumeration Enumeration(IReadOnlyDictionary<string, string>? bounds) =>
        AssertionEnumeration.Of(
            new[] { ClauseId },
            new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c",
            new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { TrivialBlock.CountTag },
            },
            bounds);

    private static SubmissionVector Vector(int index)
    {
        const int limit = 20;
        var step = index + 2;
        var expected = ((limit + step - 1) / step) * step;

        return new SubmissionVector(
            $"V-{index + 1:000}", "S0", index, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TrivialBlock.StepTag] = step.ToString(),
                [TrivialBlock.LimitTag] = limit.ToString(),
            },
            TrivialBlock.StartTag,
            new[] { new ObservabilityDeclaration(TrivialBlock.CountTag, SignalNature.PersistentState, InstrumentationMode.Sampled, 20, expected.ToString()) },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 consecutive scans",
                new[] { TrivialBlock.CountTag, TrivialBlock.DoneTag }, 3),
            MaxDurationScans: 20,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "ramp-to-limit" },
            CompletionSignal: TrivialBlock.DoneTag,
            Kills: "a ramp that overshoots the limit by one step",
            BoundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = limit.ToString() });
    }

    private static LoopRequest Request()
    {
        var vectors = Enumerable.Range(0, Submitted).Select(Vector).ToArray();

        var binding = new SlotBinding("S0",
            new[]
            {
                new MirroredSignal(TrivialBlock.StepTag, MirrorValueType.Int),
                new MirroredSignal(TrivialBlock.LimitTag, MirrorValueType.Int),
            },
            TrivialBlock.StartTag,
            new[]
            {
                new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int, SpecName: TrivialBlock.CountTag,
                    Rest: InertRest.At("0", "the ramp block's network 1 holds the count at 0 while the start command is off")),
                new MirroredSignal(TrivialBlock.DoneTag, MirrorValueType.Int, SpecName: TrivialBlock.DoneTag,
                    Rest: InertRest.At("0", "the ramp block's network 1 holds the done flag at 0 while the start command is off")),
            });

        return new LoopRequest(vectors,
            Enumeration(vectors[0].BoundsUsed),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            ConflictGraph.Empty,
            Geometry(),
            new[] { new SlotRequest("S0", 2, 2) },
            new[] { binding },
            new CopyLayerNaming(BlockNumber: 900),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, TrivialBlockDefect.None),
            RuntimeCompression: RuntimeCompression.Uncompressed,
            Deployment: new DeploymentDeclaration("loop-test-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach: TagMapReach.Of(Array.Empty<S7Reach>()),
            SignalStorage: SignalStorageMap.Of(new[]
            {
                (TrivialBlock.CountTag, new SignalStorage("DemoUnit", TrivialBlock.CountTag)),
                (TrivialBlock.DoneTag, new SignalStorage("DemoUnit", TrivialBlock.DoneTag)),
            }),
            UnknownFields: Array.Empty<string>(),
            AnnotationFields: Array.Empty<string>(),

            // No submission document, so no derivable field was hand-authored. Gate 0c's claim, stated.
            Derivation: DerivationEvidence.NoDocument,

            // Gate 1b's flat ceiling — no scenario clock on these vectors. See LoopRunTests.Request.
            MaxIndexScans: 200);
    }

    private static string Console(LoopResult result)
    {
        var writer = new StringWriter();
        LoopCli.Write(result, writer);
        return writer.ToString();
    }

    private static JsonElement Artifact(LoopResult result) =>
        JsonDocument.Parse(LoopCli.Render(result)).RootElement.Clone();

    // -------------------------------------------------------------------------------------------------
    // THE WAVE STOPS SHORT
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE MEASURED DEFECT, DRIVEN THROUGH THE WHOLE LOOP.</b> A wave that reaches only part of its
    /// tensor accounts for every submitted vector, names the slot that stopped, says at which index, and
    /// says how many vectors were consequently never attempted.
    /// </summary>
    [Fact]
    public void AWaveThatStopsShort_AccountsForEverySubmittedVector()
    {
        var result = LoopRun.Execute(Request(), new FreezeAfterGateway(Geometry(), LiveTransactions));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);

        // The whole point: the run is INCOMPLETE and the fixture is only useful if it really is.
        Assert.InRange(result.Packages.Count, 1, Submitted - 1);

        Assert.Equal(Submitted, result.Account.Submitted);
        Assert.Equal(Submitted, result.Account.Vectors.Count);
        Assert.Equal(result.Packages.Count, result.Account.Ran);
        Assert.Equal(Submitted - result.Packages.Count, result.Account.NeverAttempted);

        var exit = Assert.Single(result.Account.SlotExits);
        Assert.Equal(0, exit.SlotIndex);
        Assert.Equal(Submitted, exit.TensorLength);
        Assert.Equal(result.Packages.Count, exit.IndicesRun);
        Assert.Equal(exit.IndicesRun - 1, exit.LastIndex);
        Assert.Equal(Submitted - result.Packages.Count, exit.VectorsNeverAttempted);
        Assert.False(string.IsNullOrWhiteSpace(exit.LastDetail));
    }

    /// <summary>
    /// *** A PACKAGE EXISTS IF AND ONLY IF THE ACCOUNT SAYS `Ran`. *** One predicate decides both, which
    /// is what stops the report and the packages from drifting apart — the previous arrangement had the
    /// package loop skip silently and nothing else learn that it had.
    /// </summary>
    [Fact]
    public void ThePackagesAndTheRanDispositions_AreTheSameVectors()
    {
        var result = LoopRun.Execute(Request(), new FreezeAfterGateway(Geometry(), LiveTransactions));

        Assert.Equal(
            result.Packages.Select(p => p.VectorId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            result.Account.By(VectorDisposition.Ran).Select(v => v.VectorId).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// 🔴 <b>THE HEADLINE LINE SAID "the wave ran to 22 index(es)" ABOUT A RUN THAT REACHED TWO.</b>
    /// <c>wave.Length</c> is the longest tensor — what the wave SET OUT to run. Both numbers are now
    /// stated and the short run says so out loud.
    /// </summary>
    [Fact]
    public void TheOutcomeDetail_SaysWhatWasPlannedAndWhatWasReached()
    {
        var result = LoopRun.Execute(Request(), new FreezeAfterGateway(Geometry(), LiveTransactions));

        Assert.Contains("STOPPED EARLY", result.Detail, StringComparison.Ordinal);
        Assert.Contains($"of {Submitted} planned index(es)", result.Detail, StringComparison.Ordinal);
        Assert.Contains($"of {Submitted} submitted vector(s) were attempted", result.Detail, StringComparison.Ordinal);
    }

    /// <summary>The console report counts against the SUBMITTED total and names every unattempted vector.</summary>
    [Fact]
    public void TheConsoleReport_CountsAgainstTheSubmittedTotal()
    {
        var result = LoopRun.Execute(Request(), new FreezeAfterGateway(Geometry(), LiveTransactions));
        var console = Console(result);

        Assert.Contains($"VECTORS SUBMITTED: {Submitted}", console, StringComparison.Ordinal);
        Assert.Contains($"of {Submitted} SUBMITTED vector(s) say anything about the block at all", console, StringComparison.Ordinal);
        Assert.Contains("SLOTS THAT STOPPED SHORT OF THEIR OWN TENSOR", console, StringComparison.Ordinal);
        Assert.Contains("were consequently NEVER ATTEMPTED", console, StringComparison.Ordinal);

        // Every submitted vector is named in the report, whether it ran or not. The count IS the property.
        foreach (var vector in result.Account.Vectors)
            Assert.Contains(vector.VectorId, console, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>COVERAGE IS COMPUTABLE FROM THE ARTIFACT ALONE.</b> A consumer reads the JSON, never the
    /// console, so the dispositions and the denominator have to be there — this is the same lesson as
    /// DB-8's five dropped contents, one level up.
    /// </summary>
    [Fact]
    public void TheArtifact_CarriesADispositionForEverySubmittedVector()
    {
        var result = LoopRun.Execute(Request(), new FreezeAfterGateway(Geometry(), LiveTransactions));
        var json = Artifact(result);

        Assert.Equal(Submitted, json.GetProperty("vectorsSubmitted").GetInt32());
        Assert.Equal(result.Packages.Count, json.GetProperty("packagesProduced").GetInt32());
        Assert.Equal(result.Account.Ran, json.GetProperty("vectorsRan").GetInt32());
        Assert.Equal(result.Account.NeverAttempted, json.GetProperty("vectorsNeverAttempted").GetInt32());
        Assert.Equal(Submitted, json.GetProperty("indicesPlanned").GetInt32());
        Assert.Equal(result.Account.IndicesRun, json.GetProperty("indicesRun").GetInt32());

        var dispositions = json.GetProperty("dispositions").EnumerateArray().ToArray();
        Assert.Equal(Submitted, dispositions.Length);

        // The denominator is recomputable from the rows themselves, not merely stated in a header.
        Assert.Equal(result.Account.Ran, dispositions.Count(d => d.GetProperty("attempted").GetBoolean()));

        var unattempted = dispositions.First(d => !d.GetProperty("attempted").GetBoolean());
        Assert.Equal(nameof(VectorDisposition.SlotExitedFirst), unattempted.GetProperty("disposition").GetString());
        Assert.Contains("NEVER ATTEMPTED", unattempted.GetProperty("detail").GetString()!, StringComparison.Ordinal);

        var exit = Assert.Single(json.GetProperty("slotExits").EnumerateArray().ToArray());
        Assert.Equal(Submitted, exit.GetProperty("tensorLength").GetInt32());
        Assert.Equal(result.Account.NeverAttempted, exit.GetProperty("vectorsNeverAttempted").GetInt32());
    }

    /// <summary>
    /// And DB-8's seven contents reach the artifact through the REAL loop, not only through a constructed
    /// package. The renderer and the loop are different code, and only one of them was ever exercised.
    /// </summary>
    [Fact]
    public void TheArtifactsPackages_CarryAllSevenDb8Contents()
    {
        var result = LoopRun.Execute(Request(), new FreezeAfterGateway(Geometry(), LiveTransactions));
        var packages = Artifact(result).GetProperty("packages").EnumerateArray().ToArray();

        Assert.NotEmpty(packages);

        foreach (var package in packages)
        {
            foreach (var content in ResultPackageJson.Db8Contents)
                Assert.True(package.TryGetProperty(content, out _), $"a package written by the real loop is missing DB-8's '{content}'.");
        }

        // DB-2, from a real run: the stamp the loop computed reaches the file.
        Assert.Equal(
            $"16#{result.Packages[0].Stamp.ProgramVersion:X8}",
            packages[0].GetProperty("validityStamp").GetProperty("programVersion").GetString());
    }

    // -------------------------------------------------------------------------------------------------
    // THE UNAFFECTED CASE, TESTED AS DELIBERATELY AS THE SHORT ONE
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// *** A COMPLETE RUN MUST NOT REPORT A SHORTFALL. *** A gate that fires on ordinary input is noise,
    /// and noise gets switched off — after which the cases it was right about go through unchecked.
    /// </summary>
    [Fact]
    public void ACompleteRun_ReportsNoShortfallAndNoSlotExit()
    {
        var result = LoopRun.Execute(Request(), new SimulatedGateway(Geometry()));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(Submitted, result.Packages.Count);
        Assert.Equal(Submitted, result.Account.Ran);
        Assert.Equal(0, result.Account.NeverAttempted);
        Assert.Empty(result.Account.SlotExits);

        var console = Console(result);
        Assert.Contains($"VECTORS SUBMITTED: {Submitted}", console, StringComparison.Ordinal);
        Assert.DoesNotContain("SLOTS THAT STOPPED SHORT", console, StringComparison.Ordinal);
        Assert.DoesNotContain("NEVER ATTEMPTED", console.Replace("  NEVER ATTEMPTED 0", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);

        Assert.Contains("ran all", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("STOPPED EARLY", result.Detail, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // THE RUN THAT NEVER STARTED
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A LOOP THAT STOPPED BEFORE THE WAVE ACCOUNTS FOR EVERY SUBMITTED VECTOR, NOT NONE OF THEM.</b>
    /// This is the run whose old report was an empty package list — which reads as "no results yet"
    /// rather than as "six vectors were never attempted".
    /// </summary>
    [Fact]
    public void ARunThatNeverReachedTheWave_StillAccountsForEverySubmittedVector()
    {
        var result = LoopRun.Execute(Request(), new RefusingDeviceGateway());

        Assert.NotEqual(LoopOutcome.Ran, result.Outcome);
        Assert.Empty(result.Packages);

        Assert.Equal(Submitted, result.Account.Submitted);
        Assert.Equal(0, result.Account.Ran);
        Assert.All(result.Account.Vectors, v => Assert.Equal(VectorDisposition.WaveDidNotRun, v.Disposition));

        var console = Console(result);
        Assert.Contains($"VECTORS SUBMITTED: {Submitted}", console, StringComparison.Ordinal);
        Assert.Contains($"EMPTY IS NOT CLEAN: {Submitted} vector(s) were submitted and NONE of them produced a result",
            console, StringComparison.Ordinal);

        Assert.Equal(Submitted, Artifact(result).GetProperty("dispositions").EnumerateArray().Count());
        Assert.Equal(Submitted, Artifact(result).GetProperty("vectorsNeverAttempted").GetInt32());
    }

    /// <summary>
    /// The same for a stop at the DEPLOYMENT rather than at the gateway — every path out of the loop
    /// carries the account, and a path that forgot it would report a silent zero.
    /// </summary>
    [Fact]
    public void EveryStopPath_CarriesTheAccount()
    {
        var refused = new SimulatedGateway(Geometry()) { Refuse = true };
        var wrongBuild = new SimulatedGateway(Geometry()) { PublishVersionInstead = 0xDEADBEEF };

        foreach (var gateway in new IDeviceGateway[] { refused, wrongBuild })
        {
            var result = LoopRun.Execute(Request(), gateway);

            Assert.NotEqual(LoopOutcome.Ran, result.Outcome);
            Assert.Equal(Submitted, result.Account.Submitted);
            Assert.Equal(0, result.Account.Ran);
            Assert.Contains(result.Outcome.ToString(), result.Account.Vectors[0].Detail, StringComparison.Ordinal);
        }
    }
}

/// <summary>
/// A gateway whose mirror FREEZES after a stated number of transactions — <b>the cheapest way to make a
/// real wave stop part-way through a real tensor.</b>
///
/// <para>It is a frozen mirror rather than a broken link on purpose: a frozen mirror is perfectly
/// self-consistent and is the hazard DB-8's liveness check exists for, so the wave stops at the inert
/// phase exactly as the live run did.</para>
/// </summary>
internal sealed class FreezeAfterGateway : IDeviceGateway
{
    private readonly SimulatedGateway _inner;
    private readonly int _liveTransactions;

    public FreezeAfterGateway(MirrorGeometry geometry, int liveTransactions)
    {
        _inner = new SimulatedGateway(geometry);
        _liveTransactions = liveTransactions;
    }

    public DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp) => _inner.Deploy(objects, stamp);

    public IRegisterTransport Open() => new FreezingTransport(_inner.Open(), _liveTransactions);

    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// Passes transactions through until the budget runs out, then serves the last value it saw for each
/// register run — so the SCAN COUNTER stops advancing and the next inert phase cannot be established.
/// </summary>
internal sealed class FreezingTransport : IRegisterTransport
{
    private readonly IRegisterTransport _inner;
    private readonly Dictionary<(int Start, int Count), ushort[]> _last = new();
    private int _remaining;

    public FreezingTransport(IRegisterTransport inner, int liveTransactions)
    {
        _inner = inner;
        _remaining = liveTransactions;
    }

    public ushort[] ReadHoldingRegisters(int startRegister, int count)
    {
        if (_remaining > 0)
        {
            _remaining--;
            var live = _inner.ReadHoldingRegisters(startRegister, count);
            _last[(startRegister, count)] = live;
            return live;
        }

        // A run never seen before the freeze still has to answer with the right SHAPE — it is served from
        // the device once and then frozen like everything else.
        if (!_last.TryGetValue((startRegister, count), out var frozen))
        {
            frozen = _inner.ReadHoldingRegisters(startRegister, count);
            _last[(startRegister, count)] = frozen;
        }

        return frozen;
    }

    public void WriteHoldingRegisters(int startRegister, ushort[] values)
    {
        if (_remaining > 0)
            _inner.WriteHoldingRegisters(startRegister, values);
    }

    public void Dispose() => _inner.Dispose();
}
