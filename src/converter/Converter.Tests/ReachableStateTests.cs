using System.Text.Json;
using Converter.Ir;
using Converter.ReachableState;
using Ladder.Wave;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-14. `converter reachable-state` — D9's MISSING PRODUCER.
///
/// <para>D9 says two slots are independent when their reachable-state closures do not overlap,
/// <b>computed from the reference graph and never declared</b>. Every consumer of that rule already
/// existed — <c>SlotConflictDerivation.OverlappingReachableState</c> makes the edges,
/// <c>WaveSetAdmission</c> refuses a closure with no provenance — but <b>nothing computed the sets</b>.
/// They arrived from <c>wave-cli submit --reaches</c>, i.e. from the submitting agent. *Ask of any
/// rule: who computes its inputs? If the answer is "the party the rule constrains", it is not a rule.*</para>
///
/// <para>*** THE TWO PROOFS THAT MATTER ARE AT THE BOTTOM, AND THEY RUN AGAINST THE REAL CORPUS. ***
/// Six slots on one block must come out as SIX WAVE SETS OF ONE; four disjoint blocks must come out as
/// ONE WAVE SET OF FOUR. Both answers were known by hand first — *a producer that finds conflicts
/// everywhere passes every test that only checks for conflicts*, which is why the second is here.</para>
/// </summary>
public class ReachableStateTests : IDisposable
{
    private readonly string _dir;

    public ReachableStateTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"reachable-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static string Corpus => Path.Combine(RepoRoot(), "ir", "test-project001");

    private void Write(string file, string text) => File.WriteAllText(Path.Combine(_dir, file), text);

    private static IReadOnlyList<string> StateOf(ReachableStateReport report, string block) =>
        report.Blocks.Single(b => b.Block == block).ReachableState;

    // =============================================================================================
    // RULE 1 — CANONICALISE INSTANCE ALIASES BEFORE INTERSECTING
    // =============================================================================================

    /// <summary>
    /// *** THE DEFECT THIS PRODUCER WOULD HAVE HAD IF THE ALIAS REWRITE WERE OMITTED. *** An FB writes
    /// its own interface member with NO ROOT; its caller writes the SAME STORAGE as
    /// <c>iDB_X.&lt;member&gt;</c>. Two different strings, one location. Without the rewrite a slot
    /// testing the FB and a slot testing the caller intersect on nothing and are reported DISJOINT —
    /// *a confident false assurance about a hazard the system can no longer see*, which is how two
    /// agents end up on one FB instance.
    /// </summary>
    [Fact]
    public void CallerWritingAnInstanceMember_SharesStorageWithTheFbsOwnBareForm()
    {
        Write("FB_Thing.ir", Block("FB", "FB_Thing", 20, statics: new[] { "Level : Bool" }, body: "  COIL Level := TRUE"));
        Write("iDB_Thing.ir", "DB iDB_Thing\n  ROOTID 0\n  NUMBER 20\n  INSTANCEOF FB_Thing\n  INPUT\n  OUTPUT\n  MEMBERS\n    Level : Bool\n");
        Write("FC_Caller.ir", Block("FC", "FC_Caller", 30, body: "  COIL iDB_Thing.Level := DB_In.Raw"));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());

        Assert.True(report.Computed, report.NotComputedReason + string.Join("; ", report.Warnings));
        Assert.Contains("FB_Thing|Level", StateOf(report, "FB_Thing"));
        Assert.Contains("FB_Thing|Level", StateOf(report, "FC_Caller"));

        // And the rewrite is REPORTED, not applied silently: a narrowing nobody can see becomes a
        // place to hide, and this one is a WIDENING that a reader must be able to check.
        Assert.Contains(
            "iDB_Thing.Level -> FB_Thing|Level",
            report.Blocks.Single(b => b.Block == "FC_Caller").AliasCanonicalisations);

        // The relation that matters downstream, stated as the requirement rather than as a count.
        Assert.NotEmpty(SlotConflictDerivation.OverlappingReachableState(new[]
        {
            Slot("A", report, "FB_Thing"),
            Slot("B", report, "FC_Caller"),
        }));
    }

    /// <summary>
    /// The converse, and *** IT IS THE ONE THE PROJECT HAS ALREADY PAID FOR. *** Two FBs each declaring
    /// their own <c>IO.Step</c> are two storage locations that happen to share a spelling — pooling them
    /// is precisely the aliasing that made `cross-check` report three multi-writers that did not exist.
    /// Here it would fabricate a conflict edge and serialise two slots that never conflicted.
    /// </summary>
    [Fact]
    public void TwoFbsWithTheSameBareMemberName_DoNotShareStorage()
    {
        Write("FB_One.ir", Block("FB", "FB_One", 21, statics: new[] { "IO : Bool" }, body: "  COIL IO := TRUE"));
        Write("FB_Two.ir", Block("FB", "FB_Two", 22, statics: new[] { "IO : Bool" }, body: "  COIL IO := TRUE"));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());

        Assert.Equal(new[] { "FB_One|IO" }, StateOf(report, "FB_One"));
        Assert.Equal(new[] { "FB_Two|IO" }, StateOf(report, "FB_Two"));
        Assert.Empty(SlotConflictDerivation.OverlappingReachableState(new[]
        {
            Slot("A", report, "FB_One"),
            Slot("B", report, "FB_Two"),
        }));
    }

    /// <summary>
    /// Two tests driving different ELEMENTS of one injection array are not independent of each other —
    /// they share one declared member of one type, and `DB_Input.Test[0]` disconnects every physical
    /// terminal for both. Subscripts are stripped, which is also the granularity the D9 finding was
    /// recorded at by hand ("one `DB_Input.Test[]`").
    /// </summary>
    [Fact]
    public void ArrayElementsOfOneMember_AreOneStorageLocation()
    {
        Write("FC_A.ir", Block("FC", "FC_A", 31, body: "  COIL DB_Out.A := DB_Input.Test[3]"));
        Write("FC_B.ir", Block("FC", "FC_B", 32, body: "  COIL DB_Out.B := DB_Input.Test[9]"));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());

        Assert.Contains("DB_Input.Test", StateOf(report, "FC_A"));
        Assert.Contains("DB_Input.Test", StateOf(report, "FC_B"));
        Assert.NotEmpty(SlotConflictDerivation.OverlappingReachableState(new[]
        {
            Slot("A", report, "FC_A"),
            Slot("B", report, "FC_B"),
        }));
    }

    /// <summary>
    /// The closure is TRANSITIVE through CALL, and closes DOWNWARD only. A caller reaches its callee's
    /// storage; the callee does not reach its caller's — closing upward reaches OB1 from any leaf and
    /// would make every pair of slots in a plant program conflict.
    /// </summary>
    [Fact]
    public void CallClosure_IsTransitiveAndDownwardOnly()
    {
        Write("FC_Top.ir", Block("FC", "FC_Top", 40, body: "  COIL DB_Top.Own := TRUE\n  CALL FC_Mid(EN := TRUE)"));
        Write("FC_Mid.ir", Block("FC", "FC_Mid", 41, body: "  COIL DB_Mid.Own := TRUE\n  CALL FC_Leaf(EN := TRUE)"));
        Write("FC_Leaf.ir", Block("FC", "FC_Leaf", 42, body: "  COIL DB_Leaf.Own := TRUE"));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());

        Assert.Contains("DB_Leaf.Own", StateOf(report, "FC_Top"));
        Assert.Contains("DB_Mid.Own", StateOf(report, "FC_Top"));
        Assert.DoesNotContain("DB_Top.Own", StateOf(report, "FC_Leaf"));
        Assert.Equal(new[] { "FC_Leaf" }, report.Blocks.Single(b => b.Block == "FC_Leaf").CallClosure);
    }

    // =============================================================================================
    // RULE 2 — ABSENT VERSUS EMPTY
    // =============================================================================================

    /// <summary>
    /// A block that touches nothing gets <c>[]</c> AND a provenance. That is the POSITIVE claim
    /// "computed, and it reaches nothing", and admission must admit it.
    /// </summary>
    [Fact]
    public void ABlockThatTouchesNothing_IsComputedAndEmpty_NotWithheld()
    {
        Write("FC_Empty.ir", Block("FC", "FC_Empty", 50, body: string.Empty));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());
        var block = report.Blocks.Single();

        Assert.True(block.Computed);
        Assert.Empty(block.ReachableState);
        Assert.NotEqual(string.Empty, block.Provenance);

        // The distinction is only worth anything if the consumer honours it. It does.
        var plan = WaveSetAdmission.Admit(
            new[] { Slot("A", report, "FC_Empty") },
            Array.Empty<ConflictEdge>(),
            maxSlotsPerWaveSet: 4,
            capProvenance: "test");
        Assert.DoesNotContain(plan.Findings, f => f.Defect == ColouringDefect.ReachableStateNotComputed);
    }

    /// <summary>
    /// *** THE OTHER HALF, AND IT IS THE ONE THAT SILENTLY CONVERTS A REFUSAL INTO A PASS. *** A
    /// closure that could NOT be computed arrives as the SAME empty set — so it must carry NO
    /// provenance, and admission must refuse the slot rather than treat it as independent of
    /// everything.
    /// </summary>
    [Fact]
    public void ABlockWhoseClosureCouldNotBeComputed_CarriesNoProvenance_AndAdmissionRefusesIt()
    {
        Write("FC_Top.ir", Block("FC", "FC_Top", 60, body: "  COIL DB_Top.Own := TRUE\n  CALL FC_Absent(EN := TRUE)"));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());
        var block = report.Blocks.Single(b => b.Block == "FC_Top");

        Assert.False(block.Computed);
        Assert.Empty(block.ReachableState);
        Assert.Equal(string.Empty, block.Provenance);
        Assert.Contains("FC_Absent", block.UnresolvedCalls);

        var plan = WaveSetAdmission.Admit(
            new[] { Slot("A", report, "FC_Top") },
            Array.Empty<ConflictEdge>(),
            maxSlotsPerWaveSet: 4,
            capProvenance: "test");
        Assert.Contains(
            plan.Findings,
            f => f.Defect == ColouringDefect.ReachableStateNotComputed && f.Subject == "A");
    }

    /// <summary>
    /// The JSON is what a driver reads. When a closure was not computed the KEY IS ABSENT — not
    /// <c>[]</c>, and not <c>null</c>, which a lenient deserializer would round to <c>[]</c> and
    /// restore the false claim one layer down.
    /// </summary>
    [Fact]
    public void Json_OmitsReachableStateAndProvenance_WhenTheClosureWasNotComputed()
    {
        Write("FC_Ok.ir", Block("FC", "FC_Ok", 61, body: "  COIL DB_A.X := TRUE"));
        Write("FC_Bad.ir", Block("FC", "FC_Bad", 62, body: "  CALL FC_Absent(EN := TRUE)"));

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());
        using var doc = JsonDocument.Parse(ReachableStateOutputFormatter.FormatJson(report));

        var blocks = doc.RootElement.GetProperty("blocks").EnumerateArray().ToList();
        var ok = blocks.Single(b => b.GetProperty("block").GetString() == "FC_Ok");
        var bad = blocks.Single(b => b.GetProperty("block").GetString() == "FC_Bad");

        Assert.True(ok.TryGetProperty("reachableState", out _));
        Assert.True(ok.TryGetProperty("provenance", out _));
        Assert.False(bad.TryGetProperty("reachableState", out _));
        Assert.False(bad.TryGetProperty("provenance", out _));
        Assert.True(bad.TryGetProperty("notComputed", out _));
    }

    /// <summary>
    /// A file that did not parse can hold the reference that makes two closures intersect. The whole
    /// report is withheld rather than emitted over the rest — an under-large closure fails toward the
    /// dangerous direction, and *`blocks` present at all is a positive claim*.
    /// </summary>
    [Fact]
    public void AnUnparseableFile_WithholdsTheWholeReport()
    {
        Write("FC_Ok.ir", Block("FC", "FC_Ok", 63, body: "  COIL DB_A.X := TRUE"));
        Write("FC_Junk.ir", "BLOCK FC FC_Junk\nthis is not IR at all\n");

        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());

        Assert.False(report.Computed);
        Assert.Empty(report.Blocks);
        Assert.Equal(string.Empty, report.Provenance);

        using var doc = JsonDocument.Parse(ReachableStateOutputFormatter.FormatJson(report));
        Assert.False(doc.RootElement.TryGetProperty("blocks", out _));
        Assert.True(doc.RootElement.TryGetProperty("notComputed", out _));
    }

    /// <summary>
    /// FI-44. A `--block` naming something the corpus does not contain is UNJUDGEABLE, never clean —
    /// an empty closure for an absent block would report it as the most independent slot in the set.
    /// </summary>
    [Fact]
    public void ANamedBlockThatIsNotInTheCorpus_IsNotComputed()
    {
        Write("FC_Ok.ir", Block("FC", "FC_Ok", 64, body: "  COIL DB_A.X := TRUE"));

        var report = ReachableStateRunner.Run(_dir, new[] { "FC_NotHere" });

        Assert.False(report.Computed);
        Assert.Contains("FC_NotHere", report.NotComputedReason);
    }

    /// <summary>An empty directory is a wrongly-aimed run, not a clean project.</summary>
    [Fact]
    public void AnEmptyCorpus_IsNotComputed()
    {
        var report = ReachableStateRunner.Run(_dir, Array.Empty<string>());

        Assert.False(report.Computed);
        Assert.Empty(report.Blocks);
    }

    /// <summary>
    /// The provenance carries WHAT the closure was established against, so *"nobody computed it"* and
    /// *"computed against a different corpus"* are distinct facts rather than the same empty set with a
    /// string beside it. Changing one byte of one block must change the stamp.
    /// </summary>
    [Fact]
    public void TheProvenanceStamp_MovesWhenTheCorpusMoves()
    {
        Write("FC_Ok.ir", Block("FC", "FC_Ok", 65, body: "  COIL DB_A.X := TRUE"));
        var before = ReachableStateRunner.Run(_dir, Array.Empty<string>()).CorpusHash;

        Write("FC_Ok.ir", Block("FC", "FC_Ok", 65, body: "  COIL DB_A.Y := TRUE"));
        var after = ReachableStateRunner.Run(_dir, Array.Empty<string>()).CorpusHash;

        Assert.NotEqual(before, after);
    }

    // =============================================================================================
    // THE PROOFS — AGAINST THE REAL CORPUS, NOT A FIXTURE
    // =============================================================================================

    /// <summary>
    /// *** THE PROOF THAT MATTERS. *** Six methodology slots against one FB must come out as SIX WAVE
    /// SETS OF ONE. That answer was reached by hand and written into
    /// `conformance-vectors-b.json` as a correction (`slotsInWaveSet` 6 → 1) BY AN AUTHOR WHO SAID
    /// OUTRIGHT IT WAS NOT VERIFIED AGAINST `ir/`. This reaches it from the IR alone.
    ///
    /// <para><b>The invariant is asserted, not the count</b> — "do these two slots share a wave set?"
    /// is the requirement; "are there six waves?" is a fact about the current colourer. Both are here,
    /// but the pairwise statement is the one that survives a better colouring algorithm.</para>
    /// </summary>
    [Fact]
    public void SixSlotsOnOneBlock_AreSixWaveSetsOfOne_ComputedFromTheIrAlone()
    {
        var report = ReachableStateRunner.Run(Corpus, new[] { "FB_HopperBlockageMonitor" });
        Assert.True(report.Computed, report.NotComputedReason);

        var closure = report.Blocks.Single();
        Assert.True(closure.Computed, closure.NotComputedReason);

        // Non-empty is load-bearing: an EMPTY closure would make all six pairwise disjoint, and the
        // six-waves answer would be reached for the opposite reason.
        Assert.NotEmpty(closure.ReachableState);

        var slots = new[]
        {
            "SLOT-HBA-RAISE", "SLOT-HBA-CLEAR", "SLOT-HBA-LATCH",
            "SLOT-HBA-RESET", "SLOT-HBA-STARTUP", "SLOT-HBA-PERSIST",
        }.Select(id => new TestSlot(id, "agent-" + id, closure.ReachableState, closure.Provenance)).ToArray();

        var edges = SlotConflictDerivation.AllComputedEdges(slots);

        // Every one of the 15 pairs, and every one of them an OverlappingReachableState edge — this is
        // D9's own kind, not a model or blacklist edge standing in for it.
        Assert.Equal(15, edges.Count);
        Assert.All(edges, e => Assert.Equal(ConflictEdgeKind.OverlappingReachableState, e.Kind));

        var plan = WaveSetAdmission.Admit(slots, edges, maxSlotsPerWaveSet: 16, capProvenance: "test");
        Assert.True(plan.Usable);

        // The requirement: NO TWO of the six share a wave set.
        foreach (var wave in plan.WaveSets)
        {
            Assert.Single(wave.Slots);
        }

        Assert.Equal(6, plan.WaveSets.Count);
    }

    /// <summary>
    /// *** THE CONVERSE, AND IT IS NOT OPTIONAL: A PRODUCER THAT FINDS CONFLICTS EVERYWHERE PASSES
    /// EVERY TEST THAT ONLY CHECKS FOR CONFLICTS. *** The four Hx corpus blocks were authored to be
    /// genuinely disjoint — each names only members of its own instance DB — and they must yield NO
    /// edges and ONE wave set of four.
    /// </summary>
    [Fact]
    public void TheFourHxBlocks_AreGenuinelyDisjoint_AndYieldNoEdges()
    {
        var names = new[] { "FB_HxBoolEcho", "FB_HxDwellTimer", "FB_HxIntStep", "FB_HxSealLatch" };
        var report = ReachableStateRunner.Run(Corpus, names);
        Assert.True(report.Computed, report.NotComputedReason);

        var slots = names.Select(n =>
        {
            var closure = report.Blocks.Single(b => b.Block == n);
            Assert.True(closure.Computed, closure.NotComputedReason);

            // Each must actually reach something. Four empty closures would also produce no edges,
            // and that would be the same green for the opposite reason.
            Assert.NotEmpty(closure.ReachableState);
            return new TestSlot("SLOT-" + n, "agent-" + n, closure.ReachableState, closure.Provenance);
        }).ToArray();

        Assert.Empty(SlotConflictDerivation.AllComputedEdges(slots));

        var plan = WaveSetAdmission.Admit(slots, Array.Empty<ConflictEdge>(), maxSlotsPerWaveSet: 16, capProvenance: "test");
        Assert.True(plan.Usable);
        Assert.Single(plan.WaveSets);
        Assert.Equal(4, plan.WaveSets[0].Slots.Count);
    }

    /// <summary>
    /// The corpus contains the exact shape that manufactured three multi-writers that did not exist:
    /// `FB_PusherControl` and `FB_ShredderSequencer` each declare their own `IO.Step`. They must not
    /// intersect on it.
    /// </summary>
    [Fact]
    public void TwoCorpusFbsSharingABareMemberSpelling_DoNotIntersectOnIt()
    {
        var report = ReachableStateRunner.Run(Corpus, new[] { "FB_PusherControl", "FB_ShredderSequencer" });
        Assert.True(report.Computed, report.NotComputedReason);

        var pusher = StateOf(report, "FB_PusherControl");
        var shredder = StateOf(report, "FB_ShredderSequencer");

        // Both really do have the collided member — a test that passes because neither has it would
        // be evidence about nothing.
        Assert.Contains("FB_PusherControl|IO.Step", pusher);
        Assert.Contains("FB_ShredderSequencer|IO.Step", shredder);
        Assert.DoesNotContain(pusher.Intersect(shredder, StringComparer.Ordinal), p => p.EndsWith("|IO.Step"));
    }

    /// <summary>
    /// The whole corpus computes — no block's closure is withheld. Reported as a swept COUNT, because
    /// *a sweep that cannot say how many cases it ran cannot say that they passed*, and because a
    /// producer that quietly withheld everything would satisfy every conflict assertion above.
    /// </summary>
    [Fact]
    public void TheWholeCorpus_Computes_AndTheSweptCountIsStated()
    {
        var report = ReachableStateRunner.Run(Corpus, Array.Empty<string>());

        Assert.True(report.Computed, report.NotComputedReason);
        Assert.Empty(report.NotComputedBlocks);
        // 18 → 21 on 2026-09-18, ratifying `eba7033`'s three-slot foundation: FB_PusherStim,
        // FB_ShredderSequencerStim and FC_HarnessStimArbiter. Counted from the files, not arithmetic
        // from the delta — and the two assertions either side already prove all 21 compute cleanly.
        Assert.Equal(21, report.Blocks.Count);
        Assert.All(report.Blocks, b => Assert.NotEqual(string.Empty, b.Provenance));
    }

    // =============================================================================================

    private static TestSlot Slot(string id, ReachableStateReport report, string block)
    {
        var closure = report.Blocks.Single(b => b.Block == block);
        return new TestSlot(id, "agent-" + id, closure.ReachableState, closure.Provenance);
    }

    private static string Block(
        string kind, string name, int number, IReadOnlyList<string>? statics = null, string body = "")
    {
        var text = $"BLOCK {kind} {name}\nROOTID 0\nNUMBER {number}\nLANGUAGE LAD\nTITLE \"{name}\"\n\nINTERFACE\n  INPUT\n  OUTPUT\n";
        if (statics is { Count: > 0 })
        {
            text += "  STATIC\n" + string.Join("\n", statics.Select(s => "    " + s)) + "\n";
        }

        if (body.Length > 0)
        {
            text += $"\nNETWORK 1 \"Body\"\n{body}\n";
        }

        return text;
    }
}
