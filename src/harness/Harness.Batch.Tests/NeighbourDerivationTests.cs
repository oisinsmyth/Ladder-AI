using System.Text;
using Harness.Batch;
using Harness.Device;
using Harness.Loop;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>THE NEIGHBOUR LIST, DERIVED FROM THE PROGRAM RATHER THAN FROM SOMEBODY'S MEMORY (workbench Y1).</b>
///
/// <para><b>The defect, measured 2026-08-23 on the rig.</b> A generated mirror and a hand-authored
/// virtual panel both claimed registers 256–323 of one <c>%M</c> area and <b>53 tags collided bit for
/// bit</b>, the panel's master enable among them. <see cref="ReservedRegion"/> now guards that on every
/// path — <b>but only for a neighbour somebody wrote down</b>, which
/// <c>BindingDocument.cs:63-66</c> states in as many words: <i>"a place to put the knowledge rather than
/// a way to obtain it."</i> These tests are the obtaining.</para>
///
/// <para>🔴 <b>THE POSITIVE FIXTURE IS INVENTED, AND A GREEN SUITE HERE IS NOT EVIDENCE THAT THE
/// DERIVATION REPRODUCES THE REAL COLLISION.</b> The committed corpus
/// (<c>ir/test-project001</c>) contains <b>no FOREIGN occupant</b> of the area: every one of the 26
/// <c>%M</c> claims <c>converter neighbours</c> finds inside <c>%M1000..%M1073</c> belongs to the
/// mirror's OWN tag table, which this consumer excludes by name. So the collision case below is
/// CONSTRUCTED — the band, the owner and the tag count are modelled on the measurement, they are not
/// the measurement. The evidence that the derivation reproduces the real 256–323 collision can only
/// come from re-running it against the live corpus in the job folder, and that run is not committable.
/// Phase 4's W4 put this sentence in three places and this is one of them.</para>
///
/// <para>🔴 <b>W5's FALLBACK SHAPE IS DELIBERATELY NOT COPIED.</b> <c>UnionPreflight</c> falls back to a
/// weaker check and says so, which is right for a REPORT. A <b>refusal input</b> that goes missing must
/// REFUSE, and every "the converter could not be consulted" path below gates. There is exactly one
/// named escape, <c>--neighbours declared-only</c>, and its use is COUNTED — because if it becomes
/// routine the guard is back to declared-only and nobody would notice.</para>
/// </summary>
public sealed class NeighbourDerivationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "neighbours-" + Guid.NewGuid().ToString("N"));

    public NeighbourDerivationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    // =============================================================================================
    // Fixtures.
    // =============================================================================================

    /// <summary>
    /// A runner that answers each verb with its own canned document, keyed on <c>args[0]</c>. Keyed
    /// rather than sequenced because the two probes are independent and a sequence would make a test
    /// that changed the call ORDER fail for the wrong reason.
    /// </summary>
    private sealed class ByVerb : IProcessRunner
    {
        private readonly Dictionary<string, ProcessResult> _answers = new(StringComparer.Ordinal);

        internal List<IReadOnlyList<string>> Calls { get; } = new();

        internal ByVerb Answering(string verb, ProcessResult result)
        {
            _answers[verb] = result;
            return this;
        }

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls.Add(arguments);

            return arguments.Count > 0 && _answers.TryGetValue(arguments[0], out var answer)
                ? answer
                : new ProcessResult(true, false, 0, string.Empty, string.Empty, "no canned answer for " + (arguments.Count > 0 ? arguments[0] : "<nothing>"));
        }
    }

    private static ProcessResult Ok(string json) => new(true, false, 0, json, string.Empty, "canned");

    private static string ServedAreaJson(int baseByte, int registers) => $$"""
        {
          "derived": true,
          "baseByte": {{baseByte}},
          "registers": {{registers}},
          "denominator": "served area: base {{baseByte}}, {{registers}} register(s), derived from ir/x/FB_Comms_ModbusServer.ir:31 + sidecar ir/x/FB_Comms_ModbusServer.ir:39"
        }
        """;

    /// <summary>One claim in the producer's own JSON shape — spans in <c>%M</c> BYTES, as it emits them.</summary>
    private static string Claim(string container, string name, string address, int startByte, int byteLength, string kind = "tag") => $$"""
        {
          "owner": "tag table '{{container}}' tag '{{name}}' ({{address}})",
          "kind": "{{kind}}",
          "container": "{{container}}",
          "name": "{{name}}",
          "address": "{{address}}",
          "startByte": {{startByte}},
          "byteLength": {{byteLength}},
          "endByteExclusive": {{startByte + byteLength}},
          "file": "ir/x/{{container}}.ir",
          "line": 4
        }
        """;

    private static string NeighboursJson(int tagTables, int blocks, params string[] claims) => $$"""
        {
          "verb": "neighbours",
          "derived": true,
          "area": { "baseByte": 1000, "bytes": 1152, "registers": 576, "topByteExclusive": 2152 },
          "scanned": { "files": 43, "tagTables": {{tagTables}}, "blocks": {{blocks}}, "otherObjects": 0, "unparseable": 0 },
          "excluded": { "belowBase": 0, "atOrAboveTop": 0, "nonMarkerPointers": 0, "areaDeclarations": 1, "boundedByAddressAlone": 0 },
          "neighbours": [ {{string.Join(",", claims)}} ],
          "areaDeclarations": [],
          "denominator": "neighbours: {{claims.Length}} region(s) derived from {{tagTables}} tag table(s) + {{blocks}} block(s) + 0 other object(s) in 43 file(s); 0 file(s) unparseable; area %M1000..%M2151 (base 1000, 576 register(s))",
          "exclusions": "excluded: 0 %M claim(s) below base 1000, 0 at or above %M2152; 0 area pointer(s) outside marker memory; 1 declaration(s) of the area itself."
        }
        """;

    /// <summary>The producer's exit-2 document: <b>the <c>neighbours</c> key is ABSENT</b>, not empty.</summary>
    private static string NotDerivedJson(params string[] unparseable) => $$"""
        {
          "verb": "neighbours",
          "derived": false,
          "scanned": { "files": 43, "tagTables": 2, "blocks": 18, "otherObjects": 0, "unparseable": {{unparseable.Length}} },
          "notDerived": "one or more files in the corpus could not be parsed, and an unread file can declare a claim on this area.",
          "unparseable": [ {{string.Join(",", unparseable.Select(u => "\"" + u + "\""))}} ],
          "denominator": "NOT DERIVED — 43 file(s) scanned, {{unparseable.Length}} unparseable"
        }
        """;

    /// <summary>The producer's exit-1 document: a defect IN THE CORPUS, and the key is absent here too.</summary>
    private const string RefusedJson = """
        {
          "verb": "neighbours",
          "derived": false,
          "scanned": { "files": 43, "tagTables": 2, "blocks": 18, "otherObjects": 0, "unparseable": 0 },
          "refusals": [ "ir/x/FB_Odd.ir:12 declares P#M1200.0 in a unit this verb cannot convert to a width." ],
          "denominator": "REFUSED — 43 file(s) scanned"
        }
        """;

    private string ProgramDir(string lane)
    {
        var dir = Path.Combine(_root, lane, "ir");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "FC_" + lane + ".ir"), $"BLOCK FC FC_{lane}\nEND_BLOCK\n");
        return dir;
    }

    private Lane LaneNamed(string name, string bindingKey) =>
        new(name, bindingKey, name + ".submission.json", new[] { ProgramDir(name) });

    /// <summary>
    /// A binding over <paramref name="slots"/> slots. <b>100 slots of one <c>Time</c> vector and one
    /// <c>Int</c> result is the shape that produced the measured 318-register mirror</b> — the extent is
    /// COMPUTED, which is how it walked into a band nobody had written down as a batch went from one lane
    /// to two. That is the fixture the collision cases below need; one slot is the fixture everything
    /// else needs.
    /// </summary>
    private static string Binding(
        int slots = 1,
        int declaredRegisters = 576,
        string? tagTableName = "HarnessMirror",
        string? blockName = "FC_HarnessCopyLayer",
        string? reservedRegions = null)
    {
        var rows = string.Join(",", Enumerable.Range(0, slots).Select(i =>
            $"{{ \"slotId\": \"S{i:D3}\", \"startCondition\": \"Demo_Start_{i:D3}\", "
            + $"\"vectorTargets\": [{{ \"tag\": \"Demo_In_{i:D3}\", \"specName\": \"In\", \"type\": \"Time\" }}], "
            + $"\"resultSources\": [{{ \"tag\": \"Demo_Out_{i:D3}\", \"specName\": \"Out\", \"type\": \"Int\" }}] }}"));

        var sb = new StringBuilder("{ ");
        sb.Append($"\"blockName\": \"{blockName}\", \"blockNumber\": 9001, ");
        sb.Append($"\"tagTableName\": \"{tagTableName}\", \"tagPrefix\": \"HX_\", ");
        sb.Append($"\"retentiveBytes\": 256, \"baseByte\": 1000, \"declaredRegisters\": {declaredRegisters}, ");

        if (reservedRegions is not null)
            sb.Append($"\"reservedRegions\": {reservedRegions}, ");

        sb.Append($"\"slots\": [{rows}] }}");
        return sb.ToString();
    }

    private static Func<string, string> Reader(Dictionary<string, string> bindings) =>
        path => bindings.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException($"no fixture binding at {path}");

    private static ServedAreaFact Served(int baseByte = 1000, int registers = 576) => new(
        true, baseByte, registers,
        $"served area: base {baseByte}, {registers} register(s), derived from ir/x/FB_Comms_ModbusServer.ir:31 + sidecar",
        Array.Empty<string>());

    // =============================================================================================
    // 1 — THE PROBE. What the producer said, carried without re-derivation, and GATED ON THE KEY.
    // =============================================================================================

    [Fact]
    public void The_probe_carries_every_derived_claim_with_its_owner_container_and_line()
    {
        var runner = new ByVerb().Answering("neighbours",
            Ok(NeighboursJson(2, 18, Claim("VirtualPanel", "VP_Enable", "%M1512.0", 1512, 1))));

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576, runner);

        Assert.True(fact.Derived);
        Assert.NotNull(fact.Neighbours);
        var only = Assert.Single(fact.Neighbours!);
        Assert.Equal("VirtualPanel", only.Container);
        Assert.Equal("VP_Enable", only.Name);
        Assert.Equal(1512, only.StartByte);
        Assert.Equal(1, only.ByteLength);
        Assert.Equal("ir/x/VirtualPanel.ir", only.File);
        Assert.Equal(4, only.Line);
        Assert.Contains("tag table 'VirtualPanel' tag 'VP_Enable'", only.Owner, StringComparison.Ordinal);
    }

    /// <summary>The area comes from <c>served-area</c>, so the two derivations cannot describe two areas.</summary>
    [Fact]
    public void The_probe_asks_the_producer_about_the_area_the_served_area_derivation_established()
    {
        var runner = new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18)));

        NeighbourProbe.Derive("converter.exe", new[] { "ir/a", "ir/b" }, 1000, 576, runner);

        var call = Assert.Single(runner.Calls);
        Assert.Equal("neighbours", call[0]);
        Assert.Equal(2, call.Count(a => a == "--project"));
        Assert.Equal("1000", call[call.ToList().IndexOf("--base") + 1]);
        Assert.Equal("576", call[call.ToList().IndexOf("--registers") + 1]);
        Assert.Contains("--json", call);
    }

    /// <summary>
    /// 🔴 <b>The key's ABSENCE and an EMPTY list are different facts, and the gate is on the key.</b>
    /// <c>[]</c> under <c>derived: true</c> is the EARNED ZERO — a positive claim that the corpus was
    /// read and declared nothing in the area. A missing key is "nobody derived anything".
    /// </summary>
    [Fact]
    public void An_empty_list_under_derived_true_is_the_earned_zero_and_is_NOT_the_absent_key()
    {
        var earned = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18))));

        var absent = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NotDerivedJson("ir/x/FC_Broken.ir"))));

        Assert.True(earned.Derived);
        Assert.NotNull(earned.Neighbours);
        Assert.Empty(earned.Neighbours!);
        Assert.False(earned.Gates);

        Assert.False(absent.Derived);
        Assert.Null(absent.Neighbours);
        Assert.True(absent.Gates);
        Assert.Contains("ir/x/FC_Broken.ir", absent.Why, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>W5's shape, deliberately not copied.</b> The converter being unavailable is not a weaker
    /// check that gets reported — it is a refusal input that went missing.
    /// </summary>
    [Theory]
    [InlineData("did-not-start")]
    [InlineData("timed-out")]
    [InlineData("not-json")]
    public void A_derivation_that_could_not_be_CONSULTED_refuses_rather_than_falling_back(string how)
    {
        var result = how switch
        {
            "did-not-start" => new ProcessResult(false, false, 0, string.Empty, string.Empty, "no such file"),
            "timed-out" => new ProcessResult(true, true, 0, string.Empty, string.Empty, "timed out"),
            _ => Ok("this is not json"),
        };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", result));

        Assert.False(fact.Derived);
        Assert.True(fact.Gates);
        Assert.Null(fact.Neighbours);
    }

    [Fact]
    public void A_producer_refusal_is_carried_as_a_refusal_and_gates()
    {
        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(RefusedJson)));

        Assert.True(fact.Gates);
        Assert.Null(fact.Neighbours);
        Assert.Contains(fact.Refusals, r => r.Contains("FB_Odd.ir:12", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 2 — THE EXCLUSION. The mirror's own objects are not neighbours, and the set is CLOSED.
    // =============================================================================================

    /// <summary>
    /// The mirror's own tag table and copy layer are excluded through the closed set on
    /// <see cref="CopyLayerNaming"/> — <c>BuildStamp.cs:192-199</c>'s precedent, and never a hardcoded
    /// list. Proved by RENAMING both and watching the exclusion follow.
    /// </summary>
    [Fact]
    public void The_mirrors_own_objects_are_excluded_by_the_CLOSED_SET_and_a_rename_follows_it()
    {
        var claims = new[]
        {
            Claim("TheMirrorTable", "HX_Anything", "%MW1100", 1100, 2),
            Claim("FC_TheCopyLayer", "P#M1102.0 WORD 1", "P#M1102.0 WORD 1", 1102, 2, kind: "areaPointer"),
            Claim("VirtualPanel", "VP_Enable", "%M1512.0", 1512, 1),
        };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18, claims))));

        var naming = new CopyLayerNaming(BlockName: "FC_TheCopyLayer", BlockNumber: 900, TagTableName: "TheMirrorTable");

        var reconciled = NeighbourReconciler.Of(
            fact, MirrorGeometry.ForCpu1214C(256, 1000, 576), naming, Array.Empty<ReservedRegion>());

        Assert.Equal(2, reconciled.ExcludedAsOurs.Count);
        var kept = Assert.Single(reconciled.Derived);
        Assert.Contains("VP_Enable", kept.Owner, StringComparison.Ordinal);
        Assert.Equal(256, kept.Register);
    }

    /// <summary>Under the DEFAULT naming those same two containers are ordinary neighbours — the set is not a list of strings this code knows.</summary>
    [Fact]
    public void Under_a_different_naming_the_same_containers_are_ordinary_neighbours()
    {
        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18,
                Claim("TheMirrorTable", "HX_Anything", "%MW1100", 1100, 2)))));

        var reconciled = NeighbourReconciler.Of(
            fact, MirrorGeometry.ForCpu1214C(256, 1000, 576), new CopyLayerNaming(), Array.Empty<ReservedRegion>());

        Assert.Empty(reconciled.ExcludedAsOurs);
        Assert.Single(reconciled.Derived);
    }

    /// <summary>
    /// The byte→register conversion is <see cref="MirrorGeometry.ReservingBytes"/>'s, not a second one:
    /// a one-byte claim at <c>%M1512</c> rounds OUTWARD onto the whole register the mirror would write.
    /// </summary>
    [Fact]
    public void The_byte_span_is_converted_through_ReservingBytes_and_rounds_outward()
    {
        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(1, 0,
                Claim("VirtualPanel", "VP_Odd", "%M1513.0", 1513, 1)))));

        var reconciled = NeighbourReconciler.Of(
            fact, MirrorGeometry.ForCpu1214C(256, 1000, 576), new CopyLayerNaming(), Array.Empty<ReservedRegion>());

        var region = Assert.Single(reconciled.Derived);
        Assert.Equal(256, region.Register);
        Assert.Equal(1, region.Length);
    }

    // =============================================================================================
    // 3 — THE THREE OUTCOMES, WHICH MUST NOT COLLAPSE INTO EACH OTHER.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>OUTCOME 1 — a derived region nobody declared is USED AND NAMED.</b> This is the point of
    /// the item: nothing in the binding mentioned this band and the mirror is stopped from entering it.
    /// </summary>
    [Fact]
    public void A_derived_region_nobody_declared_is_used_and_the_refusal_names_its_owner()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding(slots: 100) };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18,
                Claim("VirtualPanel", "VP_Enable", "%M1512.0", 1512, 136)))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("VP_Enable", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("reserved registers", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>OUTCOME 2 — a DECLARED region the derivation does not corroborate is REPORTED, not
    /// refused.</b> They are different facts. The declaration may be stale, or it may name an occupant
    /// that reaches <c>%M</c> without declaring a tag — which is precisely what this derivation cannot
    /// see. Refusing it would punish the only party who wrote anything down.
    /// </summary>
    [Fact]
    public void A_declared_region_the_derivation_does_not_corroborate_is_REPORTED_not_refused()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding(reservedRegions: """[{ "register": 500, "length": 4, "owner": "legacy totaliser band" }]"""),
        };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        Assert.True(result.Planned);
        Assert.Equal(1, result.NeighbourReconciliation!.DeclaredCount);
        Assert.Equal(0, result.NeighbourReconciliation!.CorroboratedCount);
        Assert.Contains(result.NeighbourReconciliation!.Reports,
            r => r.Contains("legacy totaliser band", StringComparison.Ordinal));

        var report = BatchPlanner.Describe(result);
        Assert.Contains("1 declared, 0 corroborated", report, StringComparison.Ordinal);
        Assert.Contains("legacy totaliser band", report, StringComparison.Ordinal);
    }

    /// <summary>A declared region the derivation DOES corroborate is not reported, and does not become a second overlapping reservation.</summary>
    [Fact]
    public void A_corroborated_declaration_is_counted_and_does_not_collide_with_the_claim_that_corroborates_it()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding(reservedRegions: """[{ "register": 256, "length": 68, "owner": "virtual panel command band" }]"""),
        };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18,
                Claim("VirtualPanel", "VP_Enable", "%M1512.0", 1512, 2)))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        Assert.True(result.Planned);
        Assert.Equal(1, result.NeighbourReconciliation!.DeclaredCount);
        Assert.Equal(1, result.NeighbourReconciliation!.CorroboratedCount);
        Assert.Empty(result.NeighbourReconciliation!.Reports);
        Assert.Contains("1 declared, 1 corroborated", BatchPlanner.Describe(result), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE MEASURED CASE — AND THE FIXTURE IS INVENTED.</b>
    ///
    /// <para>A mirror reaching register 323 and a foreign band at 256–323, the shape measured live on
    /// 2026-08-23 (53 tags overwritten bit for bit, the panel's master enable among them). <b>The
    /// committed corpus has no foreign occupant of the area, so this band, its owner and its width are
    /// CONSTRUCTED from the measurement rather than read from it.</b> This test proves the mechanism
    /// refuses when it is given the shape; it does NOT prove the derivation finds the real one. Only
    /// re-running <c>converter neighbours</c> against the live corpus does that, and that run is not
    /// committable.</para>
    /// </summary>
    [Fact]
    public void THE_REAL_COLLISION_SHAPE_refuses_and_names_the_owner_and_the_overlapping_registers()
    {
        // 100 slots, Time vectors: the measured 318-register mirror. Its result block lands at 218..317,
        // straight through the band below.
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding(slots: 100) };

        // %M1512 is register 256 at base %M1000; 136 bytes is 68 registers, i.e. 256..323 inclusive.
        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18,
                Claim("VirtualPanel", "VP_CommandBand", "%MW1512", 1512, 136)))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        Assert.False(result.Planned);

        var refusal = Assert.Single(result.Refusals, r => r.Contains("VP_CommandBand", StringComparison.Ordinal));
        Assert.Contains("VirtualPanel", refusal, StringComparison.Ordinal);
        Assert.Contains("256", refusal, StringComparison.Ordinal);
    }

    // =============================================================================================
    // 4 — THE DENOMINATOR, ON EVERY RUN INCLUDING THE ZERO.
    // =============================================================================================

    /// <summary>
    /// A corpus with no neighbour in the area plans cleanly <b>and prints the zero</b>. An absent line
    /// would be indistinguishable from a check that never ran, which is the whole class of defect this
    /// item belongs to.
    /// </summary>
    [Fact]
    public void A_corpus_with_no_neighbour_in_the_area_plans_cleanly_AND_PRINTS_THE_ZERO()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        Assert.True(result.Planned);

        var report = BatchPlanner.Describe(result);
        Assert.Contains("neighbours: 0 region(s) derived from 2 tag table(s) + 18 block(s)", report, StringComparison.Ordinal);
        Assert.Contains("0 file(s) unparseable", report, StringComparison.Ordinal);
        Assert.Contains("0 declared, 0 corroborated", report, StringComparison.Ordinal);
    }

    /// <summary>The mirror's own claims are counted OUT LOUD, so a zero cannot be read as "the corpus said nothing".</summary>
    [Fact]
    public void The_claims_excluded_as_the_mirrors_own_are_stated_rather_than_silently_dropped()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18,
                Claim("HarnessMirror", "HX_ProgramVersion", "%MD1000", 1000, 4),
                Claim("HarnessMirror", "HX_ScanCount", "%MD1004", 1004, 4)))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        var report = BatchPlanner.Describe(result);
        Assert.Contains("neighbours: 0 region(s) derived", report, StringComparison.Ordinal);
        Assert.Contains("2 %M claim(s)", report, StringComparison.Ordinal);
        Assert.Contains("HarnessMirror", report, StringComparison.Ordinal);
    }

    /// <summary>What the derivation cannot see is printed where the reader meets the zero, not filed elsewhere.</summary>
    [Fact]
    public void The_report_states_what_the_derivation_cannot_see()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18))));

        var report = BatchPlanner.Describe(
            BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact));

        Assert.Contains("WITHOUT DECLARING", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not the CPU", report, StringComparison.OrdinalIgnoreCase);
    }

    // =============================================================================================
    // 5 — THE GATE, AND THE ONE NAMED ESCAPE.
    // =============================================================================================

    [Fact]
    public void A_derivation_that_went_missing_REFUSES_the_batch()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NotDerivedJson("ir/x/FC_Broken.ir"))));

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), fact);

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("FC_Broken.ir", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("--neighbours declared-only", StringComparison.Ordinal));
    }

    /// <summary>
    /// The escape does not gate — and it says <c>NEIGHBOURS: NOT DERIVED</c> where the plan is read,
    /// rather than leaving a plan that looks exactly like a derived one.
    /// </summary>
    [Fact]
    public void The_escape_plans_and_says_NOT_DERIVED_in_the_plan()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") }, Reader(bindings), Served(), NeighbourFact.DeclaredOnly);

        Assert.True(result.Planned);

        var report = BatchPlanner.Describe(result);
        Assert.Contains("NEIGHBOURS: NOT DERIVED", report, StringComparison.Ordinal);
        Assert.Contains("--neighbours declared-only", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>AND IT REACHES THE RESULT PACKAGE.</b> A plan is read once; the package outlives the run.
    /// The wave step carries the sentence so <c>harness-run</c> can put it in the package's caveats.
    /// </summary>
    [Fact]
    public void The_escape_reaches_the_RESULT_PACKAGE_through_the_wave_step()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };
        var lanes = new[] { LaneNamed("valve", "a.json") };

        var batch = BatchPlanner.Plan(lanes, Reader(bindings), Served(), NeighbourFact.DeclaredOnly);

        var plan = BatchRunPlan.For(batch, lanes, new BatchRunOptions(
            ConverterExe: "converter", HarnessRunExe: "harness-run", OpennessCliExe: "openness-cli",
            LeasesDirectory: Path.Combine(_root, "leases"), PortalProject: @"C:\p\P.ap20",
            RigAddress: "10.0.0.1", Holder: "agent-a", HolderPid: 1, StagingDirectory: _root,
            MergedBindingPath: Path.Combine(_root, "merged.json"),
            PortalEvidencePath: Path.Combine(_root, "portal.json")));

        var wave = Assert.Single(plan.Steps.Where(s => s.Kind == BatchStepKind.Wave));
        var index = wave.Arguments.ToList().IndexOf("--neighbours-not-derived");

        Assert.True(index >= 0, "the wave step must carry the sentence, or the package cannot state it.");
        Assert.Contains("NOT DERIVED", wave.Arguments[index + 1], StringComparison.Ordinal);
    }

    /// <summary>A DERIVED batch adds no such flag — a caveat that fires on every package is a caveat nobody reads.</summary>
    [Fact]
    public void A_derived_batch_puts_no_caveat_flag_on_the_wave_step()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding() };
        var lanes = new[] { LaneNamed("valve", "a.json") };

        var fact = NeighbourProbe.Derive("converter.exe", new[] { "ir/x" }, 1000, 576,
            new ByVerb().Answering("neighbours", Ok(NeighboursJson(2, 18))));

        var plan = BatchRunPlan.For(BatchPlanner.Plan(lanes, Reader(bindings), Served(), fact), lanes,
            new BatchRunOptions(
                ConverterExe: "converter", HarnessRunExe: "harness-run", OpennessCliExe: "openness-cli",
                LeasesDirectory: Path.Combine(_root, "leases"), PortalProject: @"C:\p\P.ap20",
                RigAddress: "10.0.0.1", Holder: "agent-a", HolderPid: 1, StagingDirectory: _root,
                MergedBindingPath: Path.Combine(_root, "merged.json"),
                PortalEvidencePath: Path.Combine(_root, "portal.json")));

        var wave = Assert.Single(plan.Steps.Where(s => s.Kind == BatchStepKind.Wave));
        Assert.DoesNotContain("--neighbours-not-derived", wave.Arguments);
    }

    // =============================================================================================
    // 6 — THE ESCAPE IS COUNTED, BECAUSE A ROUTINE ESCAPE IS AN UNGUARDED AREA NOBODY NOTICED.
    // =============================================================================================

    [Fact]
    public void Each_use_of_the_escape_is_recorded_and_the_running_count_is_printed()
    {
        var queue = Path.Combine(_root, "queue");
        Directory.CreateDirectory(queue);

        var first = NeighbourEscapeLog.Record(queue, "plan");
        var second = NeighbourEscapeLog.Record(queue, "run --yes");

        Assert.Equal(1, first.Uses);
        Assert.Equal(2, second.Uses);
        Assert.Equal(2, NeighbourEscapeLog.Count(queue));
        Assert.Contains("use #2", second.Line, StringComparison.Ordinal);
        Assert.Contains("declared-only", second.Line, StringComparison.Ordinal);
    }

    /// <summary>The tally survives a fresh process — a count held in memory would reset every invocation and never become visible.</summary>
    [Fact]
    public void The_tally_is_durable_against_the_queue_rather_than_per_process()
    {
        var queue = Path.Combine(_root, "queue-durable");
        Directory.CreateDirectory(queue);

        NeighbourEscapeLog.Record(queue, "plan");
        NeighbourEscapeLog.Record(queue, "plan");

        Assert.Equal(2, NeighbourEscapeLog.Count(queue));
        Assert.Equal(0, NeighbourEscapeLog.Count(Path.Combine(_root, "queue-other")));
    }

    // =============================================================================================
    // 7 — THE CLI. The flag, its two values, and what each does to the report.
    // =============================================================================================

    private string QueueWith(string bindingJson)
    {
        var queue = Path.Combine(_root, "q-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(queue);

        var binding = Path.Combine(_root, "binding.json");
        var submission = Path.Combine(_root, "submission.json");
        File.WriteAllText(binding, bindingJson);
        File.WriteAllText(submission, "{}");

        new LaneQueue(queue).Enqueue(new Lane("valve", binding, submission, new[] { ProgramDir("cli") }));
        return queue;
    }

    [Fact]
    public void Plan_with_neighbours_derive_runs_the_verb_and_refuses_a_collision()
    {
        var queue = QueueWith(Binding(slots: 100));
        var writer = new StringWriter();

        var runner = new ByVerb()
            .Answering("served-area", Ok(ServedAreaJson(1000, 576)))
            .Answering("neighbours", Ok(NeighboursJson(2, 18,
                Claim("VirtualPanel", "VP_CommandBand", "%MW1512", 1512, 136))));

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--converter", "converter.exe", "--neighbours", "derive" },
            writer, File.ReadAllText, File.WriteAllText, runner);

        Assert.Equal(BatchExit.Refused, exit);
        Assert.Contains("VP_CommandBand", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_with_the_escape_says_NOT_DERIVED_counts_the_use_and_starts_no_neighbour_process()
    {
        var queue = QueueWith(Binding());
        var writer = new StringWriter();
        var runner = new ByVerb().Answering("served-area", Ok(ServedAreaJson(1000, 576)));

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--converter", "converter.exe", "--neighbours", "declared-only" },
            writer, File.ReadAllText, File.WriteAllText, runner);

        var output = writer.ToString();
        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("NEIGHBOURS: NOT DERIVED", output, StringComparison.Ordinal);
        Assert.Contains("use #1", output, StringComparison.Ordinal);
        Assert.DoesNotContain(runner.Calls, c => c[0] == "neighbours");
        Assert.Equal(1, NeighbourEscapeLog.Count(queue));
    }

    /// <summary>
    /// <c>--neighbours derive</c> with no <c>--converter</c> is refused as an argument error rather than
    /// quietly downgraded — the downgrade IS the fallback shape this item exists to avoid.
    /// </summary>
    [Fact]
    public void Neighbours_derive_without_a_converter_is_refused_rather_than_downgraded()
    {
        var queue = QueueWith(Binding());
        var writer = new StringWriter();

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--neighbours", "derive" },
            writer, File.ReadAllText, File.WriteAllText, new ByVerb());

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("--converter", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_neighbours_mode_is_refused_by_name()
    {
        var queue = QueueWith(Binding());
        var writer = new StringWriter();

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--converter", "c.exe", "--neighbours", "maybe" },
            writer, File.ReadAllText, File.WriteAllText, new ByVerb());

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("maybe", writer.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Naming no <c>--neighbours</c> at all is the state every plan was in before this existed. It does
    /// not gate — but it is NOT silent, and the line it prints is the same <c>NOT DERIVED</c> a reader
    /// would see from the escape, so an omitted flag cannot read as a derivation that found nothing.
    /// </summary>
    [Fact]
    public void Naming_no_mode_at_all_still_says_NOT_DERIVED_and_starts_no_neighbour_process()
    {
        var queue = QueueWith(Binding());
        var writer = new StringWriter();
        var runner = new ByVerb().Answering("served-area", Ok(ServedAreaJson(1000, 576)));

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--converter", "converter.exe" },
            writer, File.ReadAllText, File.WriteAllText, runner);

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("NEIGHBOURS: NOT DERIVED", writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(runner.Calls, c => c[0] == "neighbours");

        // The escape counter must not move: it counts a DELIBERATE decline, and inflating it with every
        // ordinary plan would make the number that is supposed to raise an eyebrow unreadable.
        Assert.Equal(0, NeighbourEscapeLog.Count(queue));
    }
}
