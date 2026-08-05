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
}
