using System.Text.Json;
using System.Text.Json.Nodes;
using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>DB-8's SEVEN NAMED CONTENTS MUST REACH THE ARTIFACT — the schema/round-trip guard.</b>
///
/// <para><b>The defect these exist for.</b> The first wave that has ever run wrote a result file
/// carrying <c>vector</c>, <c>verdict</c>, <c>conclusive</c>, <c>whatToDoNext</c>, <c>caveats</c> and
/// <c>assertions</c>. <b>Five of DB-8's seven named contents were absent from it</b> — the validity
/// stamp (DB-2's entire purpose), the manifest presence, the co-running slice, the basis citation and
/// the fidelity declaration. The console rendered several of them, which is the trap:
/// <i>"a consumer reads THAT, never this rendering — anything a renderer drops is gone before a scraper
/// sees it."</i></para>
///
/// <para><b>Every test here goes through a real serialise and a real parse</b>, never through the
/// <see cref="JsonObject"/> the renderer returns. A renderer that builds a correct object and a
/// serialiser that drops half of it are different failures, and only one of them is visible from the
/// object.</para>
///
/// <para><b>And the absent cases are tested as deliberately as the present ones.</b> A content that is
/// genuinely unavailable must be PRESENT AND SAY SO — an absent key reads downstream as "there was
/// none", which is the same absent-versus-empty error this repository keeps re-earning.</para>
/// </summary>
public class ResultPackageJsonTests
{
    /// <summary>U+FFFD, the character a mis-decoded byte becomes. Named, never typed into this file.</summary>
    private static readonly string Replacement = char.ConvertFromUtf32(0xFFFD);

    private static readonly BuildStamp Build = new(0xA93F2C71);

    private static readonly AssertionEnumeration Enumeration =
        AssertionEnumeration.Of(new[] { "REQ-14" }, new[] { "REQ-14.a", "REQ-14.b" });

    private static RegisterMap Map() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 2, 2) })).Require();

    private static readonly ObservabilityReport Supportable = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })), 9, 1, 1);

    private static SlotRunResult Run(SlotOutcome outcome = SlotOutcome.Completed) =>
        new(outcome, new ushort[] { 10, 1 }, new ScanCount(100), new ScanCount(140), 2, 8,
            new InertReport(InertOutcome.Established, new ScanCount(90), Array.Empty<ushort>(), Array.Empty<ushort>(), "established"),
            "stub",
            // These tests hand `assertions` to the builder directly, so they never route through the
            // observation path; one frame states the results honestly rather than claiming a series.
            // See ObservationSeries.OfSingleFrame for why a new observation test must NOT use this.
            ObservationSeries.OfSingleFrame(new ushort[] { 10, 1 }, new ScanCount(140)));

    /// <summary>
    /// 🔴 <b>Every optional content is a REQUIRED parameter here, with no default.</b>
    ///
    /// <para>The sibling fixture in <c>ResultPackageTests</c> defaults <c>coRunners</c> to
    /// <c>Array.Empty&lt;int&gt;()</c>, so no test built through it can ever construct the null case —
    /// <i>a test helper's default is a silent assumption about which inputs are possible</i>. These
    /// tests are ABOUT the absent shapes, so the helper must be able to build them.</para>
    /// </summary>
    private static ResultPackage Package(
        Basis? basis,
        FidelityDeclaration? fidelity,
        IReadOnlyList<int>? coRunners,
        StimulusEvidence? stimulus,
        VectorBoundsCurrency? bounds = null,
        SlotOutcome outcome = SlotOutcome.Completed,
        SettlingState settling = SettlingState.Settled,
        IReadOnlyList<AssertionOutcome>? assertions = null) =>
        ResultPackageBuilder.Build(
            new VectorDeclaration("V-1", basis, fidelity,
                new SettlingDeclaration("count unchanged across 3 consecutive scans", new[] { "Demo_Count" }),
                new[] { "fill-to-setpoint" },
                "Demo_Done",
                new AgentIdentity("agent-b"),
                new AgentIdentity("agent-a"),
                Supportable,
                bounds),
            Enumeration,
            Run(outcome),
            slotIndex: 3,
            waveIndex: 7,
            stimulus,
            stimulus is null ? null : StimulusExpectation.AtLeastOneScanPerRoundTrip(8),
            settling,
            assertions ?? new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10") },
            coRunners,
            Map(),
            Build,
            slotsCoveredByOneRead: 1);

    /// <summary>A HEALTHY package: everything declared, everything measured.</summary>
    private static ResultPackage Complete() =>
        Package(
            new Basis("REQ-14", "REQ-14.a"),
            FidelityDeclaration.Of("M_Weigher", new[] { "fill-to-setpoint" }, new[] { "in-flight-mass" },
                validatedAgainstPlantData: true, declaredBy: "agent-m"),
            new[] { 1, 4 },
            new StimulusEvidence(true, true, 40, 8, ManifestPresence.Loaded,
                new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "confirmed")),
            BoundsCurrencyCheck.Evaluate("V-1",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
                // A declared bound, so the per-assertion relation is never consulted here.
                AssertionBoundsExpectation.NotStated("this fixture declares a bound")));

    /// <summary>Serialise for real and parse back. <b>Never assert against the renderer's own object.</b></summary>
    private static JsonElement RoundTrip(ResultPackage package)
    {
        var text = ResultPackageJson.Of(package).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // -------------------------------------------------------------------------------------------------
    // The denominator itself
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// *** THE COUNT IS ASSERTED, NOT DESCRIBED. *** Spec §4a DB-8 names SEVEN contents. A list that
    /// silently shrank to match a renderer would make every other test here vacuous.
    /// </summary>
    [Fact]
    public void Db8Contents_NamesExactlySevenContents()
    {
        Assert.Equal(7, ResultPackageJson.Db8Contents.Count);

        Assert.Equal(
            new[] { "assertions", "basis", "coRunning", "fidelity", "manifestPresence", "stimulus", "validityStamp" },
            ResultPackageJson.Db8Contents.OrderBy(c => c, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// 🔴 <b>THE TEST THE CAMPAIGN'S FINDING 2 IS.</b> Every one of DB-8's seven contents survives a
    /// serialise and a parse — on a HEALTHY package, where a renderer has the least excuse.
    /// </summary>
    [Fact]
    public void EveryDb8Content_SurvivesSerialiseAndParse()
    {
        var json = RoundTrip(Complete());

        foreach (var content in ResultPackageJson.Db8Contents)
        {
            Assert.True(json.TryGetProperty(content, out var value),
                $"DB-8 names '{content}' as a package content and the serialised artifact does not carry it. "
                + "This is the defect measured on the first live wave: five of seven were missing.");

            Assert.NotEqual(JsonValueKind.Null, value.ValueKind);
            Assert.NotEqual(JsonValueKind.Undefined, value.ValueKind);
        }
    }

    /// <summary>
    /// And on a package where every optional content is ABSENT — because the failure mode is a renderer
    /// that emits a key only when it has something to put in it.
    /// </summary>
    [Fact]
    public void EveryDb8Content_IsPresentEvenWhenTheDatumIsNot()
    {
        var json = RoundTrip(Package(basis: null, fidelity: null, coRunners: null, stimulus: null));

        foreach (var content in ResultPackageJson.Db8Contents)
        {
            Assert.True(json.TryGetProperty(content, out var value),
                $"'{content}' vanished when its datum was absent. A content that is unavailable must be PRESENT AND SAY SO: "
                + "an absent key reads downstream as 'there was none'.");

            Assert.NotEqual(JsonValueKind.Null, value.ValueKind);
        }
    }

    // -------------------------------------------------------------------------------------------------
    // DB-2 — the validity stamp, which is the whole reason this finding mattered
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>DB-2's stamp reaches the artifact WITH ITS TWO VALUES.</b> The stamp is what stops a green
    /// obtained BEFORE an invalidating change from being read as current, and it is useless as a bare
    /// key: a consumer has to be able to compare the program version and the map hash.
    /// </summary>
    [Fact]
    public void ValidityStamp_CarriesProgramVersionAndMapHash()
    {
        var package = Complete();
        var stamp = RoundTrip(package).GetProperty("validityStamp");

        Assert.Equal($"16#{package.Stamp.ProgramVersion:X8}", stamp.GetProperty("programVersion").GetString());
        Assert.Equal(package.Stamp.ProgramVersion, stamp.GetProperty("programVersionRaw").GetUInt32());
        Assert.Equal(package.Stamp.MapHash, stamp.GetProperty("mapHash").GetString());
        Assert.False(string.IsNullOrWhiteSpace(stamp.GetProperty("mapHash").GetString()));
    }

    /// <summary>The caveats the stamp's validity rests on travel WITH the stamp, not somewhere a reader must look up.</summary>
    [Fact]
    public void ValidityStamp_CarriesItsCaveats()
    {
        // No liveness evidence at all, which the builder turns into a caveat naming the frozen-mirror case.
        var package = Package(new Basis("REQ-14", "REQ-14.a"), null, null, stimulus: null);
        var caveats = RoundTrip(package).GetProperty("validityStamp").GetProperty("caveats");

        Assert.Equal(package.Stamp.Caveats.Count, caveats.GetArrayLength());
        Assert.NotEmpty(package.Stamp.Caveats);
        Assert.Contains(caveats.EnumerateArray(), c => c.GetString()!.StartsWith("NO LIVENESS EVIDENCE", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------------------------------
    // §9a — manifest presence, which the type did not even retain
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The manifest answer reaches the artifact.</b> It used to be consumed by
    /// <see cref="StimulusCheck"/> and dropped, so nothing downstream could say whether the object under
    /// test was ever LOADED — a different question from whether it RAN.
    /// </summary>
    [Theory]
    [InlineData(ManifestPresence.Loaded, true)]
    [InlineData(ManifestPresence.NotAvailable, false)]
    public void ManifestPresence_ReachesTheArtifact(ManifestPresence presence, bool evidencesTransfer)
    {
        var package = Package(
            new Basis("REQ-14", "REQ-14.a"), null, Array.Empty<int>(),
            new StimulusEvidence(true, true, 40, 8, presence, null));

        var manifest = RoundTrip(package).GetProperty("manifestPresence");

        Assert.Equal(presence.ToString(), manifest.GetProperty("state").GetString());
        Assert.True(manifest.GetProperty("asked").GetBoolean());
        Assert.Equal(evidencesTransfer, manifest.GetProperty("evidencesTransfer").GetBoolean());
    }

    /// <summary>
    /// *** NOT ASKED IS A FOURTH STATE AND IT IS NOT `NotAvailable`. *** One says a manifest was
    /// consulted and named nothing; the other says nobody looked.
    /// </summary>
    [Fact]
    public void ManifestPresence_NotAskedIsDistinctFromNotAvailable()
    {
        var manifest = RoundTrip(Package(null, null, null, stimulus: null)).GetProperty("manifestPresence");

        Assert.Equal("NotAsked", manifest.GetProperty("state").GetString());
        Assert.False(manifest.GetProperty("asked").GetBoolean());
        Assert.False(manifest.GetProperty("evidencesTransfer").GetBoolean());
        Assert.Contains("NOT ASKED", manifest.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // The co-running slice — three states, not two
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void CoRunning_MeasuredSlotsReachTheArtifact()
    {
        var coRunning = RoundTrip(Complete()).GetProperty("coRunning");

        Assert.True(coRunning.GetProperty("recorded").GetBoolean());
        Assert.False(coRunning.GetProperty("ranAlone").GetBoolean());
        Assert.Equal(new[] { 1, 4 }, coRunning.GetProperty("slots").EnumerateArray().Select(s => s.GetInt32()).ToArray());
    }

    /// <summary>
    /// 🔴 <b>AN UNRECORDED SLICE MUST NOT RENDER AS "RAN ALONE".</b> A result obtained under unrecorded
    /// co-runners would otherwise be indistinguishable from one obtained in isolation — which is the
    /// wrong direction to be wrong in.
    /// </summary>
    [Fact]
    public void CoRunning_UnrecordedIsNotRanAlone()
    {
        var unrecorded = RoundTrip(Package(null, null, coRunners: null, stimulus: null)).GetProperty("coRunning");
        var alone = RoundTrip(Package(null, null, coRunners: Array.Empty<int>(), stimulus: null)).GetProperty("coRunning");

        Assert.False(unrecorded.GetProperty("recorded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, unrecorded.GetProperty("ranAlone").ValueKind);
        Assert.Contains("NOT 'it ran alone'", unrecorded.GetProperty("detail").GetString()!, StringComparison.Ordinal);

        Assert.True(alone.GetProperty("recorded").GetBoolean());
        Assert.True(alone.GetProperty("ranAlone").GetBoolean());
    }

    // -------------------------------------------------------------------------------------------------
    // Basis and fidelity — present and saying so
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void Basis_CarriesBothTheClauseAndTheAssertion()
    {
        var basis = RoundTrip(Complete()).GetProperty("basis");

        Assert.True(basis.GetProperty("cited").GetBoolean());
        Assert.Equal("REQ-14", basis.GetProperty("clauseId").GetString());
        Assert.Equal("REQ-14.a", basis.GetProperty("assertionId").GetString());
    }

    [Fact]
    public void Basis_AbsentIsAPositiveStatement()
    {
        var basis = RoundTrip(Package(basis: null, null, null, null)).GetProperty("basis");

        Assert.False(basis.GetProperty("cited").GetBoolean());
        Assert.Contains("NO BASIS WAS CITED", basis.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The fidelity declaration carries <b>what the model does NOT represent</b> — the half that decides
    /// how far a Pass generalises, and the half a renderer is most tempted to drop.
    /// </summary>
    [Fact]
    public void Fidelity_CarriesRepresentsAndDoesNotRepresent()
    {
        var fidelity = RoundTrip(Complete()).GetProperty("fidelity");

        Assert.True(fidelity.GetProperty("declared").GetBoolean());
        Assert.Equal("M_Weigher", fidelity.GetProperty("modelId").GetString());
        Assert.Equal("agent-m", fidelity.GetProperty("declaredBy").GetString());
        Assert.True(fidelity.GetProperty("validatedAgainstPlantData").GetBoolean());
        Assert.Contains("fill-to-setpoint", fidelity.GetProperty("represents").EnumerateArray().Select(v => v.GetString()!), StringComparer.Ordinal);
        Assert.Contains("in-flight-mass", fidelity.GetProperty("doesNotRepresent").EnumerateArray().Select(v => v.GetString()!), StringComparer.Ordinal);
    }

    [Fact]
    public void Fidelity_AbsentIsAPositiveStatement()
    {
        var fidelity = RoundTrip(Package(null, fidelity: null, null, null)).GetProperty("fidelity");

        Assert.False(fidelity.GetProperty("declared").GetBoolean());
        Assert.Contains("NO MODEL FIDELITY DECLARATION", fidelity.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // The stimulus check and the observed-versus-expected rows
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void Stimulus_CarriesItsOutcomeAndItsDetail()
    {
        var package = Package(null, null, null,
            new StimulusEvidence(Commanded: true, Executed: false, 40, 8, ManifestPresence.Loaded, null));

        var stimulus = RoundTrip(package).GetProperty("stimulus");

        Assert.Equal(nameof(StimulusOutcome.CommandedButDidNotRun), stimulus.GetProperty("outcome").GetString());
        Assert.False(stimulus.GetProperty("confirmed").GetBoolean());
        Assert.Contains("echo", stimulus.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Observed versus expected, per assertion — <b>with the state, and with whether the state says
    /// anything about the block at all.</b> <c>NotObserved</c> is neither a pass nor a failure, and a
    /// consumer counting rows must not have to know that from a doc comment it cannot read.
    /// </summary>
    [Fact]
    public void Assertions_CarryObservedVersusExpectedAndWhetherTheySayAnything()
    {
        var package = Package(null, null, null, null, assertions: new[]
        {
            AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10"),
            AssertionOutcome.Compare("REQ-14.b", "Demo_Done", "true", null),
        });

        var rows = RoundTrip(package).GetProperty("assertions").EnumerateArray().ToArray();

        Assert.Equal(2, rows.Length);

        Assert.Equal("Demo_Count", rows[0].GetProperty("signal").GetString());
        Assert.Equal("10", rows[0].GetProperty("expected").GetString());
        Assert.Equal("10", rows[0].GetProperty("observed").GetString());
        Assert.Equal(nameof(AssertionState.Held), rows[0].GetProperty("state").GetString());
        Assert.True(rows[0].GetProperty("saysSomethingAboutTheBlock").GetBoolean());

        Assert.Equal(nameof(AssertionState.NotObserved), rows[1].GetProperty("state").GetString());
        Assert.False(rows[1].GetProperty("saysSomethingAboutTheBlock").GetBoolean());
    }

    /// <summary>
    /// The verdict-bearing fields the report reads, so that a consumer never has to re-derive the
    /// precedence — and so that <c>runOutcome</c> and <c>settling</c>, which is what the verdict actually
    /// turned on, are recoverable rather than inferred from the verdict word.
    /// </summary>
    [Fact]
    public void Verdict_ArrivesWithWhatItTurnedOn()
    {
        // Fully ADMISSIBLE, so the verdict is decided by settling rather than by a refusal — the point of
        // the test is that `runOutcome` and `settling` are recoverable, and a Refused package would
        // demonstrate that against the wrong branch of the precedence.
        var json = RoundTrip(Package(
            new Basis("REQ-14", "REQ-14.a"),
            FidelityDeclaration.Of("M_Weigher", new[] { "fill-to-setpoint" }, new[] { "in-flight-mass" }, true, "agent-m"),
            Array.Empty<int>(),
            new StimulusEvidence(true, true, 40, 8, ManifestPresence.Loaded, null),
            settling: SettlingState.NotEstablished));

        Assert.Equal(nameof(ResultVerdict.Unsettled), json.GetProperty("verdict").GetString());
        Assert.False(json.GetProperty("conclusive").GetBoolean());
        Assert.Equal(nameof(SlotOutcome.Completed), json.GetProperty("runOutcome").GetString());
        Assert.Equal(nameof(SettlingState.NotEstablished), json.GetProperty("settling").GetString());
        Assert.Equal(3, json.GetProperty("slotIndex").GetInt32());
        Assert.Equal(7, json.GetProperty("waveIndex").GetInt32());
    }

    /// <summary>AMB-19: the un-asked question is a state in the artifact, never a missing key.</summary>
    [Fact]
    public void BoundsCurrency_NotAskedIsRendered()
    {
        var bounds = RoundTrip(Package(null, null, null, null)).GetProperty("boundsCurrency");

        Assert.False(bounds.GetProperty("asked").GetBoolean());
        Assert.Equal("NotAsked", bounds.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, bounds.GetProperty("premiseOutOfDate").ValueKind);
    }

    /// <summary>
    /// The artifact is ASCII. Non-ASCII characters in the detail prose are escaped by the serialiser, and
    /// a U+FFFD has reached a live report from this repository twice — so the property is pinned rather
    /// than assumed from the encoder's default.
    /// </summary>
    [Fact]
    public void RenderedArtifact_IsAscii()
    {
        var text = ResultPackageJson.Of(Complete()).ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        // U+FFFD is checked by NAME rather than written into this source file: a literal replacement
        // character in a .cs file is one re-encoding away from becoming something else, and the test
        // would then pass for the wrong reason. It has reached a live report from here twice.
        Assert.DoesNotContain(Replacement, text, StringComparison.Ordinal);
        Assert.All(text, c => Assert.True(c < 128, $"non-ASCII character U+{(int)c:X4} reached the serialised package."));
    }
}
