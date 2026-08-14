using Converter.CandidateScan;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-39 check 1 (2026-08-05): `converter candidate-scan` — compute every signal that could satisfy a
/// requirement, so "more than one candidate" is a COMPUTED fact rather than a judgement call.
///
/// Why it exists: two defects shipped because a requirement phrase ("not faulted", "running feedback")
/// admitted more than one signal and the single reader who resolved it never noticed there was a choice
/// (`docs/evidence/PlantAutoControl-bench-autopsy.md` §2-C). Nothing computed a candidate set, so nothing
/// could flag the ambiguity.
/// </summary>
public class CandidateScanTests : IDisposable
{
    private readonly string _dir;

    public CandidateScanTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"candscan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // Two same-typed field signals in one name family — the transposition shape.
        WriteDb("DB_In.ir", new DbSource("0", "DB_In", 1, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Unit1Op", "Bool", Retain: false, StartValue: null),
            new DbMember("Unit1Ready", "Bool", Retain: false, StartValue: null),
            new DbMember("Unit1Level", "Real", Retain: false, StartValue: null),
            new DbMember("Other", "Bool", Retain: false, StartValue: null),
        }));

        // The FB's reportable status members live under STATIC inside interface-UDT structs — NOT in the
        // OUTPUT section. A tool that filtered by interface section would miss Outputs.FaultActive, which
        // is exactly the member the narrowed-fault-gate defect was about.
        WriteBlock("FB_Unit.ir",
            statics: new[]
            {
                new DbMember("Outputs", "Struct", false, null, NestedMembers: new[]
                {
                    new DbMember("FaultActive", "Bool", Retain: false, StartValue: null),
                }),
                new DbMember("Inputs", "Struct", false, null, NestedMembers: new[]
                {
                    new DbMember("AutoStartSignal", "Bool", Retain: false, StartValue: null),
                }),
            },
            networks: new[]
            {
                // The FB WRITES FaultActive (a status it reports) and READS AutoStartSignal (a command).
                new IrNetwork(15, "Faults", new[]
                {
                    new CoilAssignment("Outputs.FaultActive", new Expr.TagRef("Inputs.AutoStartSignal")),
                }),
            });
    }

    private void WriteDb(string fileName, DbSource db) =>
        File.WriteAllText(Path.Combine(_dir, fileName), DbIrSerializer.Serialize(db));

    private void WriteBlock(string fileName, DbMember[] statics, IrNetwork[] networks) =>
        File.WriteAllText(
            Path.Combine(_dir, fileName),
            IrSerializer.SerializeBlockReadable(
                new IrBlock("0", "FB", "FB_Unit", 1, "LAD", null, networks, StaticMembers: statics)));

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

    private CandidateScanReport Run(string? scope, string? type = null, string direction = "any",
        params string[] phrases) =>
        CandidateScanRunner.Run(_dir, "FB_Unit", instance: null,
            scopes: scope is null ? Array.Empty<string>() : new[] { scope },
            typeFilter: type, direction: direction, phrases: phrases);

    [Fact]
    public void TwoSameTypedSignalsInScope_YieldsChoice_AndTheFamilySignature()
    {
        var report = Run("DB_In.Unit1", type: "Bool", direction: "command");

        Assert.Equal(2, report.IoCandidates.Count); // Unit1Op + Unit1Ready; Unit1Level is Real, Other out of scope
        Assert.Contains(report.IoCandidates, c => c.Path == "DB_In.Unit1Op");
        Assert.Contains(report.IoCandidates, c => c.Path == "DB_In.Unit1Ready");
        Assert.Equal(2, report.Family.SameTypedIoSignals);
        Assert.True(report.HasChoice);
    }

    // Exactly one thing in scope is not a finding — the tool flags a CHOICE, not a binding.
    // `Real` selects the one analogue signal and no FB member (all of which are Bool).
    [Fact]
    public void SingletonScope_HasNoChoice()
    {
        var report = Run("DB_In.Unit1Level", type: "Real");

        Assert.Single(report.IoCandidates);
        Assert.Empty(report.FbCandidates);
        Assert.Equal(1, report.Size);
        Assert.False(report.HasChoice);
    }

    // The regression that section-filtering would cause: the members that matter live under STATIC.
    [Fact]
    public void FbStatusMemberUnderStatic_IsEnumerated_AndClassifiedByComputedDirection()
    {
        var report = Run(scope: null, direction: "status");

        var fault = Assert.Single(report.FbCandidates, c => c.Member == "Outputs.FaultActive");
        Assert.Equal(MemberRole.Status, fault.Role);
        Assert.Contains("FB_Unit N15", fault.WrittenAt);
    }

    [Fact]
    public void DirectionStatus_ExcludesMembersTheFbOnlyReads()
    {
        var status = Run(scope: null, direction: "status");
        var command = Run(scope: null, direction: "command");

        Assert.DoesNotContain(status.FbCandidates, c => c.Member == "Inputs.AutoStartSignal");
        Assert.Contains(command.FbCandidates, c => c.Member == "Inputs.AutoStartSignal");
    }

    // Filtering a candidate set by name resemblance is the reasoning that produced the swapped-pairing
    // defect. The phrase is reported, never applied.
    [Fact]
    public void Phrase_IsAdvisoryOnly_AndNeverChangesTheExitCondition()
    {
        var unfiltered = Run("DB_In.Unit1", type: "Bool", direction: "command");
        var phrased = Run("DB_In.Unit1", "Bool", "command", "Ready");

        Assert.Equal(unfiltered.Size, phrased.Size);
        Assert.Equal(unfiltered.HasChoice, phrased.HasChoice);
        Assert.Contains("DB_In.Unit1Ready", phrased.PhraseMatches);
        Assert.DoesNotContain("DB_In.Unit1Op", phrased.PhraseMatches);
    }

    // Every absence claim needs its denominator: an empty set in a partial export is a scope fact.
    [Fact]
    public void Report_StatesItsDenominator()
    {
        var report = Run("DB_In.Unit1", type: "Bool");

        Assert.Equal(2, report.FilesScanned); // DB_In.ir + FB_Unit.ir
        Assert.Contains("2 file(s) scanned", CandidateScanOutputFormatter.FormatText(report));
    }

    [Fact]
    public void UnparseableFile_DegradesToWarning_NotCrash()
    {
        File.WriteAllText(Path.Combine(_dir, "broken.ir"), "DB Broken\n  this is not valid IR\n");

        var report = Run("DB_In.Unit1", type: "Bool");

        Assert.NotEmpty(report.Warnings);
        Assert.Equal(2, report.IoCandidates.Count); // the good files still contribute
    }

    [Fact]
    public void FormatJson_IsValid_AndCarriesSizeAndFamily()
    {
        var report = Run("DB_In.Unit1", type: "Bool", direction: "command");

        var json = CandidateScanOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(report.Size, doc.RootElement.GetProperty("size").GetInt32());
        Assert.True(doc.RootElement.GetProperty("hasChoice").GetBoolean());
        Assert.Equal(2, doc.RootElement.GetProperty("family").GetProperty("sameTypedIoSignals").GetInt32());
    }

    // ---- FI-44: --scope could not address a C-001 physical-IO tag, and said "clean" about it ----

    // C-001 physical-IO form is <DI|DQ|AI|AQ><n>_<Equipment>_<Signal>, so the equipment token sits in
    // the MIDDLE. --scope was a pure path prefix, so scoping to a piece of equipment matched nothing
    // and returned EXIT 0 — a silent false clean on precisely the binding a spec stage needs judged.
    [Fact]
    public void ScopeMatchesTheC001EquipmentToken_NotOnlyAPathPrefix()
    {
        WriteProjectFile("IO_Unit.ir", TagTableIrSerializer.Serialize(
            new PlcTagTableSource("7", "IO_Unit", new[]
            {
                new PlcTagSource("1", "DQ8_UnitX_ValveOpen", "Bool", "%Q0.7", true, true, true, null),
                new PlcTagSource("2", "DQ9_UnitX_PumpRun", "Bool", "%Q1.0", true, true, true, null),
                new PlcTagSource("3", "DQ10_UnitY_ValveOpen", "Bool", "%Q1.1", true, true, true, null),
            })));

        var report = CandidateScanRunner.Run(_dir, "FB_Unit", instance: null,
            scopes: new[] { "UnitX" }, typeFilter: "Bool", direction: "any", phrases: Array.Empty<string>());

        Assert.Equal(2, report.IoCandidates.Count);
        Assert.True(report.HasChoice);           // two signals could satisfy it — the real answer
        Assert.False(report.ScopedButFoundNothing);
        Assert.DoesNotContain(report.IoCandidates, c => c.Path.Contains("UnitY", StringComparison.Ordinal));
    }

    // The guard itself. A scope that matches nothing is not evidence the binding is unambiguous —
    // it is evidence the question did not land, and it must not read as success.
    [Fact]
    public void ScopeMatchingNothing_IsUnjudgeable_NotClean()
    {
        var report = Run("NoSuchEquipment");

        Assert.Empty(report.IoCandidates);
        Assert.True(report.ScopedButFoundNothing);
        Assert.False(report.HasChoice);  // distinct signals: "ambiguous" and "unasked" are not the same
    }

    // Asking no scope at all is a different thing again: the caller did not ask about IO, so there is
    // nothing unanswered. This must stay a clean pass or every FB-only query starts failing.
    [Fact]
    public void NoScopeGiven_IsNotAnUnansweredQuestion()
    {
        var report = Run(scope: null);

        Assert.Empty(report.IoCandidates);
        Assert.False(report.ScopedButFoundNothing);
    }

    // Guard the true positive: the prefix form still works, so DB-qualified scopes are untouched.
    [Fact]
    public void PathPrefixScope_StillMatches()
    {
        var report = Run("DB_In.Unit1", type: "Bool");

        Assert.Equal(2, report.IoCandidates.Count);
        Assert.False(report.ScopedButFoundNothing);
    }

    // FI-44, the OTHER door (2026-08-14). `ScopedButFoundNothing` guards the --scope axis and nothing
    // guarded --fb, which is the MANDATORY argument: an FB name that exists nowhere in the corpus
    // produced zero candidates, "CANDIDATE SET SIZE: 0" and EXIT 0 — byte-identical to the answer a
    // real FB with an unambiguous binding gives. `undriven-scan` already refuses exactly this
    // condition ("there is no reading of 'scan a block that does not exist' that ends in success");
    // the fix landed on one of the two tools and not the other. Measured on the real 43-file
    // test-project001 export, not on a fixture.
    [Fact]
    public void UnknownFb_IsUnjudgeable_NotClean()
    {
        var report = CandidateScanRunner.Run(_dir, "FB_DoesNotExistAnywhere", instance: null,
            scopes: Array.Empty<string>(), typeFilter: null, direction: "any",
            phrases: Array.Empty<string>());

        Assert.True(report.FilesScanned > 0);       // the corpus WAS read - this is not an empty project
        Assert.True(report.UnknownFb);
        Assert.True(report.ExaminedNothing);
        Assert.False(report.HasChoice);             // "unasked" is not "unambiguous"
    }

    // The unaffected case, tested as deliberately as the refused one: a real FB must stay a clean
    // pass, or the guard is noise and gets switched off.
    [Fact]
    public void KnownFb_IsNotUnknown_EvenWhenItYieldsNoIoCandidates()
    {
        var report = Run(scope: null);

        Assert.False(report.UnknownFb);
        Assert.False(report.ExaminedNothing);
    }

    // A block with no FB-interface leaves at all must still not read as "unknown" — the property has
    // to be derived from the corpus's block names, never from the candidate count, or an FB with an
    // empty interface is misreported as absent.
    [Fact]
    public void KnownFbWithNoInterfaceLeaves_IsStillKnown()
    {
        WriteProjectFile("FB_Bare.ir",
            "BLOCK FB FB_Bare\nROOTID 0\nNUMBER 99\nLANGUAGE LAD\nTITLE \"bare\"\nCOMMENT \"c\"\n\n"
            + "INTERFACE\n  INPUT\n  OUTPUT\n\nNETWORK 1 \"n\"\n  COMMENT \"c\"\n"
            + "  COIL DB_In.Other := DB_In.Unit1Op\n");

        var report = CandidateScanRunner.Run(_dir, "FB_Bare", instance: null,
            scopes: Array.Empty<string>(), typeFilter: null, direction: "any",
            phrases: Array.Empty<string>());

        Assert.Empty(report.FbCandidates);
        Assert.False(report.UnknownFb);
    }

    private void WriteProjectFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_dir, fileName), content);
}
