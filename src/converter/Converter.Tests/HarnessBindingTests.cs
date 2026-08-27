using System.Text.Json;
using Converter.HarnessBinding;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `converter harness-binding` (2026-08-27) — the mechanically-derivable half of a harness binding
/// document, emitted instead of transcribed. The committed
/// `gen/test-project001/hopper-blockage-alarm/harness-binding.json` is 297 lines for ONE slot of 20
/// signals, hand-typed, and a transcription is the one step in this pipeline with no mechanical check
/// behind it.
///
/// <para>The corpus below is the shape the derivation has to survive: a stimulus head whose caller-
/// facing members sit under a STATIC struct (C-132), one member it both reads AND writes, one it
/// latches with SET/RESET coils, one it drives with a plain coil, one nothing touches, one whose type
/// the mirror cannot carry, and an observed block whose INPUTS must not be mistaken for things the
/// harness drives.</para>
/// </summary>
public class HarnessBindingTests : IDisposable
{
    private readonly string _dir;

    public HarnessBindingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"harnessbinding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        var stim = new[]
        {
            new DbMember("Stim", "Struct", false, null, NestedMembers: new[]
            {
                // read by the head and by nothing else — the only shape the harness DRIVES
                new DbMember("Preset", "Int", Retain: false, StartValue: null),
                new DbMember("Span", "Time", Retain: false, StartValue: null),
                // written by a plain coil — observed, and NOT a latch however it is sealed
                new DbMember("Armed", "Bool", Retain: false, StartValue: null),
                // SET + RESET in one block — the only shape that earns `latchedBy`
                new DbMember("Done", "Bool", Retain: false, StartValue: null),
                // read AND written by its own block — observed, never driven
                new DbMember("Scratch", "Bool", Retain: false, StartValue: null),
                // nothing touches it
                new DbMember("Spare", "Bool", Retain: false, StartValue: null),
                // a type the mirror has no element for
                new DbMember("Ratio", "Real", Retain: false, StartValue: null),
            }),
        };

        WriteBlock("FB_Stim", "FB", stim, new[]
        {
            new IrNetwork(1, "drive", new[]
            {
                new CoilAssignment("Stim.Armed", new Expr.TagRef("Stim.Preset")),
                new CoilAssignment("Stim.Scratch", new Expr.TagRef("Stim.Span")),
            }),
            new IrNetwork(2, "latch", new[]
            {
                new CoilAssignment("Stim.Done", new Expr.TagRef("Stim.Scratch"), CoilKind.Set),
            }),
            new IrNetwork(3, "unlatch", new[]
            {
                new CoilAssignment("Stim.Done", new Expr.TagRef("Stim.Ratio"), CoilKind.Reset),
            }),
        });

        WriteInstanceDb("iDB_Stim", "FB_Stim", stim);

        var uut = new[]
        {
            new DbMember("IO", "Struct", false, null, NestedMembers: new[]
            {
                // an INPUT of the observed block: the stimulus model drives it, not the harness
                new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                new DbMember("Alarm", "Bool", Retain: false, StartValue: null),
            }),
        };

        WriteBlock("FB_Uut", "FB", uut, new[]
        {
            new IrNetwork(1, "act", new[]
            {
                new CoilAssignment("IO.Alarm", new Expr.TagRef("IO.Cmd")),
            }),
        });

        WriteInstanceDb("iDB_Uut", "FB_Uut", uut);

        WriteBlock("FC_Main", "FC", Array.Empty<DbMember>(), new[]
        {
            new IrNetwork(1, "call", Array.Empty<CoilAssignment>(), Calls: new[]
            {
                new CallStatement("FB_Stim", "iDB_Stim", new Expr.And(Array.Empty<Expr>()),
                    Array.Empty<CallArgument>()),
                new CallStatement("FB_Uut", "iDB_Uut", new Expr.And(Array.Empty<Expr>()),
                    Array.Empty<CallArgument>()),
            }),
        });
    }

    private void WriteBlock(string name, string kind, DbMember[] statics, IrNetwork[] networks) =>
        File.WriteAllText(Path.Combine(_dir, name + ".ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", kind, name, 1, "LAD", null, networks, StaticMembers: statics)));

    private void WriteInstanceDb(string name, string fb, DbMember[] members) =>
        File.WriteAllText(Path.Combine(_dir, name + ".ir"), DbIrSerializer.Serialize(
            new DbSource("0", name, 2, InstanceOfName: fb, Comment: null, Members: members)));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private HarnessBindingReport Run(
        string stimulus = "FB_Stim",
        string[]? observed = null,
        string[]? scopes = null,
        string? instance = null) =>
        HarnessBindingRunner.Run(_dir, stimulus, observed ?? new[] { "FB_Uut" },
            scopes ?? Array.Empty<string>(), "SLOT-A", instance);

    private static string[] Tags(IEnumerable<DerivedSignal> rows) => rows.Select(r => r.Tag).ToArray();

    // --- the partition: which side of the wire each signal puts the harness on -------------------

    [Fact]
    public void MemberTheStimulusHeadOnlyReads_IsSomethingTheHarnessDrives() =>
        Assert.Contains("iDB_Stim.Stim.Preset", Tags(Run().VectorTargets));

    [Fact]
    public void MemberTheStimulusHeadWrites_IsObservedAndNotDriven()
    {
        var report = Run();
        Assert.Contains("iDB_Stim.Stim.Armed", Tags(report.ResultSources));
        Assert.DoesNotContain("iDB_Stim.Stim.Armed", Tags(report.VectorTargets));
    }

    /// <summary>
    /// 🔴 A member its own block READS AND WRITES is never driven — the harness would contend with the
    /// block's own coil, and the writer list is the evidence that it would. It is still offered as an
    /// observation, because reading is non-destructive and driving is not.
    /// </summary>
    [Fact]
    public void MemberReadAndWrittenByItsOwnBlock_IsNeverDriven()
    {
        var report = Run();
        Assert.DoesNotContain("iDB_Stim.Stim.Scratch", Tags(report.VectorTargets));
        Assert.Contains("iDB_Stim.Stim.Scratch", Tags(report.ResultSources));
        Assert.Contains(report.Excluded,
            e => e.Path == "iDB_Stim.Stim.Scratch" && e.Reason == ExclusionReason.ReadWriteContention);
    }

    /// <summary>
    /// An INPUT of an observed block is driven by the STIMULUS MODEL, not by the harness. Driving it
    /// would have the harness testing its own arithmetic — and it is EXCLUDED WITH A REASON rather
    /// than dropped, because a shortened document reads exactly like a shorter block.
    /// </summary>
    [Fact]
    public void InputOfAnObservedBlock_IsExcludedByNameAndNotDriven()
    {
        var report = Run();
        Assert.DoesNotContain("iDB_Uut.IO.Cmd", Tags(report.VectorTargets));
        Assert.DoesNotContain("iDB_Uut.IO.Cmd", Tags(report.ResultSources));
        Assert.Contains(report.Excluded,
            e => e.Path == "iDB_Uut.IO.Cmd" && e.Reason == ExclusionReason.ObservedInput);
    }

    [Fact]
    public void OutputOfAnObservedBlock_IsObserved() =>
        Assert.Contains("iDB_Uut.IO.Alarm", Tags(Run().ResultSources));

    [Fact]
    public void MemberNothingTouches_IsExcludedAsUnused() =>
        Assert.Contains(Run().Excluded,
            e => e.Path == "iDB_Stim.Stim.Spare" && e.Reason == ExclusionReason.Unused);

    // --- type and width, read out of the declaration and never guessed --------------------------

    [Fact]
    public void BoolOccupiesOneRegister() =>
        Assert.Equal(1, Run().ResultSources.Single(r => r.Tag == "iDB_Stim.Stim.Armed").RegisterWidth);

    /// <summary>A Time is 32 bits and occupies TWO holding registers — a width, not a nicety.</summary>
    [Fact]
    public void TimeOccupiesTwoRegisters() =>
        Assert.Equal(2, Run().VectorTargets.Single(r => r.Tag == "iDB_Stim.Stim.Span").RegisterWidth);

    /// <summary>
    /// 🔴 A type with no mirror element is REFUSED BY NAME, never mapped to the nearest thing. A
    /// hard-coded `Int` reached a controller and TIA answered `Data type Bool is not permitted here`
    /// after a full import; that is what naming the nearest type costs.
    /// </summary>
    [Fact]
    public void TypeTheMirrorCannotCarry_IsRefusedByNameRatherThanApproximated()
    {
        var report = Run();
        Assert.DoesNotContain("iDB_Stim.Stim.Ratio", Tags(report.VectorTargets));
        Assert.Contains(report.Refusals, r => r.Subject == "iDB_Stim.Stim.Ratio" && r.Detail.Contains("Real"));
    }

    [Fact]
    public void ASignalItCannotType_RefusesTheWholeScaffold() =>
        Assert.True(Run().Refused, "nothing may be used from a refused scaffold — a partially-emitted "
                                   + "binding is the shape that looks finished.");

    // --- latchedBy: derivable, and the committed artifact records a human deriving it by hand ----

    /// <summary>
    /// SET + RESET coils, every writer in one block. The committed binding's own note says the
    /// provenance "was in the IR all along ... a whole-corpus search finds no other writer" — this is
    /// that search.
    /// </summary>
    [Fact]
    public void MemberWithSetAndResetCoilsInOneBlock_DerivesLatchedBy() =>
        Assert.Equal("FB_Stim", Run().ResultSources.Single(r => r.Tag == "iDB_Stim.Stim.Done").LatchedBy);

    /// <summary>
    /// A plain coil is NOT a latch however it is sealed, and `latchedBy` absent is itself the claim
    /// "this binding claims no latch" — so it is never written speculatively. The real corpus proves
    /// the case matters: `IO.HopperBlockedAlarm` seals itself in its own rung with a plain COIL, and
    /// the hand-typed binding correctly claims no latch for it.
    /// </summary>
    [Fact]
    public void MemberWrittenByAPlainCoil_ClaimsNoLatch() =>
        Assert.Null(Run().ResultSources.Single(r => r.Tag == "iDB_Stim.Stim.Armed").LatchedBy);

    [Fact]
    public void ADerivedLatch_CarriesTheEvidenceThatEarnedIt() =>
        Assert.Contains("SETCOIL",
            Run().ResultSources.Single(r => r.Tag == "iDB_Stim.Stim.Done").LatchEvidence);

    // --- the four claims: named as holes, never defaulted ----------------------------------------

    [Theory]
    [InlineData("specName")]
    [InlineData("encoding")]
    [InlineData("inertRest")]
    [InlineData("startCondition")]
    public void EachClaimAboutThePlantOrASpec_IsANamedHole(string field) =>
        Assert.Contains(Run().Holes, h => h.Field.Contains(field, StringComparison.Ordinal));

    [Fact]
    public void EveryHole_NamesWhoResolvesIt() =>
        Assert.All(Run().Holes, h => Assert.False(string.IsNullOrWhiteSpace(h.ResolvedBy),
            $"hole '{h.Field}' says what is missing but not who supplies it, which moves the work "
            + "without saying where."));

    /// <summary>
    /// 🔴 <c>startCondition</c> is the trap: absent parses as null and null is the POSITIVE claim
    /// "this slot has no start gate". The scaffold cannot tell a slot that HAS no start bool from one
    /// whose start bool nobody has named, so it emits neither answer and names the hole.
    /// </summary>
    [Fact]
    public void StartCondition_IsNeverWrittenIntoTheEmittedDocument()
    {
        using var document = JsonDocument.Parse(HarnessBindingOutputFormatter.FormatBinding(Run()));
        var slot = document.RootElement.GetProperty("slots")[0];
        Assert.False(slot.TryGetProperty("startCondition", out _));
    }

    [Theory]
    [InlineData("specName")]
    [InlineData("encoding")]
    [InlineData("inertRest")]
    public void NoClaimField_IsWrittenOntoASignalRow(string field)
    {
        using var document = JsonDocument.Parse(HarnessBindingOutputFormatter.FormatBinding(Run()));
        var slot = document.RootElement.GetProperty("slots")[0];

        foreach (var list in new[] { "vectorTargets", "resultSources" })
        {
            foreach (var row in slot.GetProperty(list).EnumerateArray())
            {
                Assert.False(row.TryGetProperty(field, out _),
                    $"'{field}' is a claim about the plant or a specification. Emitting one — even an "
                    + "empty object — reads as a decision somebody made.");
            }
        }
    }

    /// <summary>
    /// 🔴 THE HOLES FAIL CLOSED. Gate 0b splits a binding's unmapped keys on the leading underscore:
    /// `_note` is an annotation and is counted, anything else is UNKNOWN and is REFUSED. So
    /// `unresolvedHoles` — deliberately NOT underscore-prefixed — makes an unfinished scaffold
    /// unrunnable rather than merely commented. <i>A warning is not a gate.</i>
    /// </summary>
    [Fact]
    public void UnresolvedHoles_IsANonAnnotationKeySoGate0bRefusesIt()
    {
        using var document = JsonDocument.Parse(HarnessBindingOutputFormatter.FormatBinding(Run()));
        Assert.True(document.RootElement.TryGetProperty("unresolvedHoles", out var holes));
        Assert.NotEqual(0, holes.GetArrayLength());
        Assert.DoesNotContain("_unresolvedHoles", document.RootElement.ToString(), StringComparison.Ordinal);
    }

    // --- refusals: each names what is missing and who resolves it ---------------------------------

    [Fact]
    public void UnknownBlock_IsNothingExaminedAndNotAnEmptySet()
    {
        var report = Run(stimulus: "FB_NotHere");
        Assert.Equal(BindingScope.UnknownBlock, report.Scope);
        Assert.True(report.ExaminedNothing);
        Assert.Contains(report.Refusals, r => r.Subject == "FB_NotHere");
    }

    /// <summary>
    /// A block with no placement has no absolute address to bind, and hard rule 3 forbids inventing
    /// one. The refusal names the engineer, who creates the instance.
    /// </summary>
    [Fact]
    public void BlockWithNoPlacement_IsRefusedAndNamesWhoCreatesTheInstance()
    {
        File.Delete(Path.Combine(_dir, "iDB_Stim.ir"));
        var report = Run();
        Assert.True(report.Refused);
        Assert.Contains(report.Refusals, r => r.Detail.Contains("--instance", StringComparison.Ordinal));
    }

    [Fact]
    public void InstanceThatIsNotAPlacementOfTheBlock_IsRefused() =>
        Assert.Contains(Run(instance: "iDB_Uut").Refusals,
            r => r.Subject == "iDB_Uut" && r.Detail.Contains("not a placement", StringComparison.Ordinal));

    // --- empty is not clean ----------------------------------------------------------------------

    /// <summary>
    /// A `--scope` that matched nothing is a scope fact, not a binding with no signals. Keyed on the
    /// ROW COUNT as well as on the enum, per undriven-scan's third shape.
    /// </summary>
    [Fact]
    public void ScopeThatMatchesNothing_IsNothingExamined()
    {
        var report = Run(scopes: new[] { "iDB_NoSuchThing." });
        Assert.Equal(BindingScope.NothingInScope, report.Scope);
        Assert.True(report.ExaminedNothing);
    }

    [Fact]
    public void ScopeMatchesTheInstanceQualifiedTagNotTheBareMember() =>
        Assert.All(Run(scopes: new[] { "iDB_Stim." }).ResultSources,
            r => Assert.StartsWith("iDB_Stim.", r.Tag, StringComparison.Ordinal));

    [Fact]
    public void EverySignalOutOfScope_IsCountedRatherThanDropped() =>
        Assert.Contains(Run(scopes: new[] { "iDB_Stim." }).Excluded,
            e => e.Path.StartsWith("iDB_Uut.", StringComparison.Ordinal)
                 && e.Reason == ExclusionReason.OutOfScope);

    // --- the served window: a derivation, never a default ----------------------------------------

    /// <summary>
    /// This corpus has no MB_SERVER call, so `served-area` does not derive a window — and NOT DERIVED
    /// is carried as an ABSENCE, never as a number. Exit 2 there is never a pass, and a width written
    /// from a refused derivation is exactly the confident wrong answer the command exists to avoid.
    /// </summary>
    [Fact]
    public void ServedAreaNotDerived_WritesNoWidthAndNamesTheHole()
    {
        var report = Run();
        Assert.Null(report.ServedRegisters);
        Assert.Null(report.ServedBaseByte);
        Assert.Contains(report.Holes, h => h.Field.Contains("declaredRegisters", StringComparison.Ordinal));

        using var document = JsonDocument.Parse(HarnessBindingOutputFormatter.FormatBinding(report));
        Assert.False(document.RootElement.TryGetProperty("declaredRegisters", out _));
        Assert.False(document.RootElement.TryGetProperty("baseByte", out _));
    }

    [Fact]
    public void RequiredRegisters_SumsTheDerivedWidths()
    {
        var report = Run();
        Assert.Equal(report.VectorTargets.Sum(r => r.RegisterWidth)
                     + report.ResultSources.Sum(r => r.RegisterWidth),
            report.RequiredRegisters);
    }

    // --- the emitted document is deterministic ----------------------------------------------------

    [Fact]
    public void TheSameCorpusEmitsTheSameDocument() =>
        Assert.Equal(HarnessBindingOutputFormatter.FormatBinding(Run()),
            HarnessBindingOutputFormatter.FormatBinding(Run()));
}
