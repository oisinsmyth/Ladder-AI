using Harness.Map;
using Xunit;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE BUILD STAMP STATES ITS OWN COVERAGE — because until now it stated only what it DID hash,
/// and a SHORT list is indistinguishable from a complete one.</b>
///
/// <para>*** MEASURED, AND WRITTEN DOWN IN <c>docs/18-project-workbench.md</c> §5 <b>"Phase 10 — Wave
/// time"</b>, under <i>"THE BUILD STAMP DOES COVER THE PARAMETER DB"</i> — ⚠️ <b>read its RETRACTION
/// 2026-08-24 before quoting a figure: the deployed set was NINE objects, stamp <c>622F3EB7</c>, not the
/// eight first written.</b> *** A wave's stamp was derived over a set the parameter DB was not in, so
/// <i>"compressing them changes the controller without changing the stamp"</i>. Two result packages
/// describing materially different programs would carry the SAME stamp, and the verifying gateway —
/// whose entire job is to refuse a program that is not the one described — would not notice.
/// <see cref="ProgramManifest.HashedNothing"/> already told the ZERO case apart. <b>Nothing told the
/// SHORT case apart</b>, and short is the case that happened.</para>
///
/// <para><b>The denominator is the staged corpus, NEVER "the program".</b> An object EXECUTING ON THE
/// DEVICE that appears in no corpus and no lane manifest is outside this count entirely — the virtual
/// panel is exactly that — and closing that needs Portal. So the residual is asserted here as hard as
/// the count is: a check that reads as closed when it is not is the failure this whole phase is about.</para>
/// </summary>
public class StampCoverageTests
{
    private static readonly CopyLayerNaming Naming =
        new(BlockName: "FC_TheCopyLayer", BlockNumber: 900, TagTableName: "TheMirrorTable");

    private static RegisterMap OneSlot() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 3, 2) })).Require();

    private static SlotBinding Binding() => new(
        "S0",
        MirroredSignal.Ints("DB_Unit.Setpoint", "DB_Unit.Mode"),
        "DB_Unit.StartCmd",
        MirroredSignal.Ints("DB_Unit.Actual", "DB_Unit.State"));

    private static HarnessObject Block(string name) => new(name, HarnessObjectKind.Block, $"BLOCK {name}\n");

    /// <summary>The nine names a lane's manifests stage. Eight blocks and the parameter DB that got missed.</summary>
    private static readonly string[] NineStaged =
    {
        "FB_Subject", "DB_Subject_iDB", "FC_SlotFc", "FB_StimHead", "DB_StimHead_iDB",
        "FB_Support", "DB_Support_iDB", "OB_Main", "DB_Params",
    };

    private static StagedCorpus Nine() => StagedCorpus.FromNames("lane 'vessel'", NineStaged);

    private static (BuildStamp Stamp, ProgramManifest Manifest) Derive(StagedCorpus? corpus, params string[] program)
    {
        var stamp = BuildStamp.Derive(
            OneSlot(), new[] { Binding() }, Naming,
            program.Select(Block), corpus,
            out _, out var manifest);

        return (stamp, manifest);
    }

    private static StampCoverage Coverage(StagedCorpus? corpus, params string[] program) =>
        Derive(corpus, program).Manifest.Coverage!;

    // ---------------------------------------------------------------------------------------------
    // 🔴 the defect: the short case, named
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_CORPUS_OBJECT_THE_PROGRAM_LIST_MISSES_IS_NAMED()
    {
        // *** THE MEASURED CASE, IN MINIATURE. *** Nine objects staged, eight handed to `--program`, and
        // the one left out is the parameter DB — exactly the object whose absence let a changed
        // controller keep an unchanged stamp.
        var coverage = Coverage(Nine(), NineStaged.Where(n => n != "DB_Params").ToArray());

        Assert.Equal(8, coverage.Hashed);
        Assert.Equal(9, coverage.CorpusSize);
        Assert.Contains(coverage.PresentAndNotHashed, g => g.Contains("DB_Params", StringComparison.Ordinal));
        Assert.Single(coverage.PresentAndNotHashed);
        Assert.False(coverage.Complete);

        // And the RUN says so, in the line a reader actually sees — asserted WHOLE, because the wording is
        // the deliverable here and a substring check cannot catch a clause quietly going missing.
        Assert.Equal(
            "stamp over 8 of 9 object(s) in the staged corpus; "
            + "0 excluded as self-referential (none); "
            + "1 present and not hashed (DB_Params [lane 'vessel']); "
            + "0 hashed that no corpus entry names (none) — "
            + StampCoverage.DeviceResidual,
            coverage.Line);
    }

    [Fact]
    public void THE_GAP_NAMES_WHICH_MANIFEST_STAGED_IT()
    {
        // A gap nobody can trace to a source is a gap nobody can close: the corpus is a UNION over lane
        // manifests, so the name alone does not say which one to go and fix.
        var coverage = Coverage(
            StagedCorpus.FromNames("lane 'valve'", "FB_Valve")
                .Union(StagedCorpus.FromNames("lane 'vessel'", "FB_Vessel", "DB_Params")),
            "FB_Valve", "FB_Vessel");

        var gap = Assert.Single(coverage.PresentAndNotHashed);
        Assert.Contains("DB_Params", gap, StringComparison.Ordinal);
        Assert.Contains("lane 'vessel'", gap, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // the negative controls — the passing case prints its denominator too
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_PASSING_CASE_PRINTS_HASHED_NINE_OF_NINE()
    {
        // *** PRINTED ON THE PASS, NOT ONLY ON THE GAP. *** `MirrorViewModel.RegistersStale` makes exactly
        // this argument about its own counts: a count that appears only when it is non-zero teaches a
        // reader that its absence means everything was covered — and then a run that counted NOTHING
        // reads identically to a run that covered everything.
        var coverage = Coverage(Nine(), NineStaged);

        Assert.Equal(9, coverage.Hashed);
        Assert.Equal(9, coverage.CorpusSize);
        Assert.Empty(coverage.PresentAndNotHashed);
        Assert.True(coverage.Complete);

        Assert.Equal(
            "stamp over 9 of 9 object(s) in the staged corpus; "
            + "0 excluded as self-referential (none); "
            + "0 present and not hashed (none); "
            + "0 hashed that no corpus entry names (none) — "
            + StampCoverage.DeviceResidual,
            coverage.Line);
    }

    [Fact]
    public void NO_PROGRAM_AND_NO_CORPUS_IS_NOT_HASHED_ZERO_OF_NINE()
    {
        // 🔴 *** TWO DIFFERENT FACTS THAT MUST NOT RENDER THE SAME. *** "nobody staged a corpus, so there
        // is no denominator" and "nine objects were staged and NONE of them was hashed" are as far apart
        // as this check gets, and only the second is an accusation.
        var (_, nothing) = Derive(corpus: null);
        var (_, none) = Derive(Nine());

        // HashedNothing is UNCHANGED by any of this — it still means exactly "no object was hashed".
        Assert.True(nothing.HashedNothing);
        Assert.True(none.HashedNothing);

        Assert.False(nothing.Coverage!.CorpusStated);
        Assert.Null(nothing.Coverage!.CorpusSize);
        Assert.Contains("NO STAGED CORPUS WAS SUPPLIED", nothing.Coverage!.Line, StringComparison.Ordinal);
        Assert.DoesNotContain("0 of 0", nothing.Coverage!.Line, StringComparison.Ordinal);
        Assert.DoesNotContain("of 9", nothing.Coverage!.Line, StringComparison.Ordinal);

        Assert.True(none.Coverage!.CorpusStated);
        Assert.Contains("0 of 9 object(s) in the staged corpus", none.Coverage!.Line, StringComparison.Ordinal);
        Assert.Equal(9, none.Coverage!.PresentAndNotHashed.Count);

        Assert.NotEqual(nothing.Coverage!.Line, none.Coverage!.Line);
    }

    [Fact]
    public void A_CORPUS_THAT_STATES_NOTHING_IS_REFUSED_RATHER_THAN_TREATED_AS_ZERO()
    {
        // EMPTY IS NOT CLEAN. A corpus of no objects would make every run read "hashed n of 0" with an
        // empty gap list — the shape of a green that examined nothing.
        Assert.Throws<ArgumentException>(() => StagedCorpus.FromNames("lane 'vessel'"));
    }

    // ---------------------------------------------------------------------------------------------
    // the self-referential exclusions stay separate, and they are correct
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_SELF_REFERENTIAL_EXCLUSIONS_ARE_NOT_COUNTED_AS_A_GAP()
    {
        // The copy layer and the mirror tag table are refused BY NAME and that refusal is right — hashing
        // the layer the stamp is rendered into is circular. So they must never appear as "present and not
        // hashed": a permanent phantom gap is a gap nobody would ever close, and a check with one of those
        // is a check people learn to skim.
        var corpus = StagedCorpus.FromNames("lane 'vessel'", "FB_Subject", "FC_TheCopyLayer", "TheMirrorTable");
        var coverage = Coverage(corpus, "FB_Subject", "FC_TheCopyLayer", "TheMirrorTable");

        Assert.Equal(1, coverage.Hashed);
        Assert.Equal(3, coverage.CorpusSize);
        Assert.Empty(coverage.PresentAndNotHashed);
        Assert.Equal(2, coverage.ExcludedAsSelfReferential.Count);
        Assert.True(coverage.Complete);

        Assert.Contains("1 of 3 object(s) in the staged corpus", coverage.Line, StringComparison.Ordinal);
        Assert.Contains("2 excluded as self-referential", coverage.Line, StringComparison.Ordinal);
        Assert.Contains("FC_TheCopyLayer", coverage.Line, StringComparison.Ordinal);
        Assert.Contains("0 present and not hashed (none)", coverage.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SELF_REFERENTIAL_OBJECT_IN_THE_CORPUS_BUT_NOT_IN_THE_PROGRAM_IS_STILL_NOT_A_GAP()
    {
        // Every lane manifest lists the copy layer it generated, while a `--program` list built from the
        // pre-generation tree legitimately does not. Classifying by NAME rather than by "did the caller
        // hand it over" is what keeps that from reading as a coverage hole on every single run.
        var corpus = StagedCorpus.FromNames("lane 'vessel'", "FB_Subject", "FC_TheCopyLayer");
        var coverage = Coverage(corpus, "FB_Subject");

        Assert.Empty(coverage.PresentAndNotHashed);
        Assert.Contains(coverage.ExcludedAsSelfReferential, e => e.Contains("FC_TheCopyLayer", StringComparison.Ordinal));
        Assert.Contains("1 of 2 object(s) in the staged corpus", coverage.Line, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // the other direction — hashed, and no corpus entry names it
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_OBJECT_HASHED_THAT_NO_CORPUS_ENTRY_NAMES_IS_NAMED_TOO()
    {
        // The stamp is over a set NO LANE DECLARED. That is not a missing object, it is a `--program` the
        // manifests disagree with, and it is the disagreement `LaneManifest.Disagreement` refuses to pick
        // a winner in. Naming it here costs nothing and the alternative is "9 of 9" over the wrong nine.
        var coverage = Coverage(StagedCorpus.FromNames("lane 'vessel'", "FB_Subject"), "FB_Subject", "FB_Stowaway");

        Assert.Contains(coverage.HashedNotInCorpus, g => g.Contains("FB_Stowaway", StringComparison.Ordinal));
        Assert.False(coverage.Complete);
        Assert.Contains("1 hashed that no corpus entry names", coverage.Line, StringComparison.Ordinal);
        Assert.Contains("FB_Stowaway", coverage.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_THAT_COUNT_IS_PRINTED_WHEN_IT_IS_ZERO()
    {
        Assert.Contains("0 hashed that no corpus entry names (none)", Coverage(Nine(), NineStaged).Line, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 the residual — the reason this is not a closed check
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void EVERY_LINE_SAYS_STAGED_CORPUS_AND_STATES_THE_DEVICE_RESIDUAL(bool withCorpus, bool withProgram)
    {
        // 🔴 *** WHAT THIS CHECK CANNOT POSSIBLY SEE: AN OBJECT ON THE DEVICE THAT IS IN NO CORPUS AND NO
        // LANE MANIFEST. *** The virtual panel is exactly that. Closing it needs Portal and this item does
        // not use Portal — so the sentence has to travel with the number, in the SAME line, or the number
        // becomes a claim about the program and the check becomes a closed one.
        var coverage = Coverage(
            withCorpus ? Nine() : null,
            withProgram ? NineStaged : Array.Empty<string>());

        // Case-insensitive: the no-corpus branch shouts "NO STAGED CORPUS WAS SUPPLIED". The claim under
        // test is the WORD, not its case.
        Assert.Contains("staged corpus", coverage.Line, StringComparison.OrdinalIgnoreCase);

        // Ordinal, and deliberately: the residual's own "NOT OF THE PROGRAM" is the sentence that makes
        // this safe to say, so only a lower-case claim would be the mistake this guards against.
        Assert.DoesNotContain("of the program", coverage.Line, StringComparison.Ordinal);
        Assert.Contains(StampCoverage.DeviceResidual, coverage.Line, StringComparison.Ordinal);
        Assert.Contains("EXECUTING ON THE DEVICE", coverage.Line, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 and the hash input is UNCHANGED
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SUPPLYING_A_CORPUS_DOES_NOT_MOVE_THE_STAMP()
    {
        // *** THE ONE THING THIS ITEM MUST NOT DO. *** The corpus is a DENOMINATOR, not an input: it says
        // what SHOULD have been hashed, and nothing about it is on the controller. Hashing it would move
        // every stamp already computed — including the constant compiled into the copy layer currently
        // deployed — for no fact on the device. `MirrorGeometry.cs:76-81` records the same reasoning for
        // why a reservation is not a map-hash input.
        var withoutCorpus = Derive(corpus: null, NineStaged).Stamp;
        var withCorpus = Derive(Nine(), NineStaged).Stamp;
        var withADifferentCorpus = Derive(StagedCorpus.FromNames("lane 'other'", "FB_Nothing_To_Do_With_It"), NineStaged).Stamp;

        Assert.Equal(withoutCorpus.Value, withCorpus.Value);
        Assert.Equal(withoutCorpus.Value, withADifferentCorpus.Value);

        // And the old six-argument overload — the one every existing caller uses — still produces it.
        var throughTheOldPath = BuildStamp.Derive(
            OneSlot(), new[] { Binding() }, Naming, NineStaged.Select(Block), out _, out _);

        Assert.Equal(withoutCorpus.Value, throughTheOldPath.Value);
    }

    [Fact]
    public void AND_THE_MANIFEST_IT_ALREADY_RECORDED_IS_UNCHANGED()
    {
        // The coverage is ADDED BESIDE the manifest, never instead of it. Same objects, same order, same
        // per-object hashes, same exclusions — a consumer reading only what it read yesterday is unaffected.
        var without = Derive(corpus: null, "FB_Subject", "FC_TheCopyLayer").Manifest;
        var with = Derive(Nine(), "FB_Subject", "FC_TheCopyLayer").Manifest;

        Assert.Equal(
            without.Objects.Select(o => (o.Kind, o.Name, o.Sha256)),
            with.Objects.Select(o => (o.Kind, o.Name, o.Sha256)));
        Assert.Equal(without.ExcludedAsSelfReferential, with.ExcludedAsSelfReferential);
        Assert.Equal(without.Stamp, with.Stamp);
    }
}
