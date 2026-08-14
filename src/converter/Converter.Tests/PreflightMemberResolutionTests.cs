using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;
using Converter.TagStatus;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Tooling-hammer campaign, 2026-08-14. <c>preflight</c> resolved every tag reference to its ROOT and
/// stopped there, so a block reading three invented members reported <c>CLEAN, exit 0</c> — while
/// <c>tagstatus</c>, given the same three paths and the same <c>--project</c>, refused two of them by
/// name at exit 1. Hard rule 3 is "never invent tags"; <c>preflight</c> is the gate the coding
/// workflow actually runs (workflow step 4, "must pass with zero findings"), and <c>tagstatus</c> is
/// the one somebody has to remember to run by hand, on a name they already suspect.
/// <c>PreflightRunner.cs:163</c> took <c>ComponentPath[0]</c> and nothing else;
/// <c>TagStatusTests.cs:11</c> asserted in prose that the two "never disagree".
///
/// <para>
/// The fix reuses <c>MemberPathResolver</c> — the same walk, the same registry, the same corpus as
/// <c>tagstatus</c> — so the two cannot drift. <c>NotEnumerable</c> deliberately does not gate.
/// </para>
///
/// <para>
/// <b>The corpus sweep that validated it fired, and that is why these tests exist in this shape.</b>
/// Run over all 92 committed <c>.ir</c> files (PlantAutoControl-bench 34, reference 15, test-project001
/// 43), the first version reported <b>154 member-path findings, every one of them false</b>: a
/// trailing <c>.%X3</c> is a C-501 alarm-bit SLICE, not a member, and <c>MemberPathResolver</c> walked
/// it as a component. <b>That defect was tagstatus's and predates preflight ever calling it</b> —
/// <c>DB_Alarms.ShredderAlarm0.%X0</c> reported MEMBER-NOT-FOUND, the hard-rule-3 alarm verdict, on a
/// documented construct. Nothing offline had touched it because no probe had swept the whole corpus.
/// After the slice fix the sweep reports <b>0 member-path findings across all 92 files</b>.
/// </para>
/// </summary>
public class PreflightMemberResolutionTests : IDisposable
{
    private readonly string _projectDir;
    private readonly List<string> _tempPaths = new();

    public PreflightMemberResolutionTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"preflight-member-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);

        WriteProjectFile("UDT_Unit.ir", TypeIrSerializer.Serialize(new PlcTypeSource("0", "UDT_Unit", null, new[]
        {
            new DbMember("Running", "Bool", Retain: false, StartValue: null),
            new DbMember("Alarm", "Word", Retain: false, StartValue: null),
        })));

        WriteProjectFile("DB_Plant.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Plant", 20, InstanceOfName: null, Comment: "Plant.", Members: new[]
            {
                new DbMember("Present", "Bool", Retain: false, StartValue: null),
                new DbMember("AlarmWord", "Word", Retain: false, StartValue: null),
                new DbMember("Unit", "\"UDT_Unit\"", Retain: false, StartValue: null),
            })));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_projectDir, recursive: true);
        }
        catch (IOException)
        {
        }

        foreach (var path in _tempPaths)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }

    private void WriteProjectFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_projectDir, fileName), content);

    private string WriteBatchFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"preflight-member-batch-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, content);
        _tempPaths.Add(path);
        return path;
    }

    // A block whose one network reads `path`, with a UDT-typed local `IO` so local roots are testable.
    private string BlockReading(string path)
    {
        var block = new IrBlock("0", "FC", "FC_Probe", 41, "LAD", "Header.", new[]
        {
            new IrNetwork(1, "Read", new[]
            {
                new CoilAssignment("DB_Plant.Present", new Expr.TagRef(path)),
            }),
        },
        TempMembers: new[] { new DbMember("IO", "\"UDT_Unit\"", Retain: false, StartValue: null) });

        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        return WriteBatchFile(IrSerializer.SerializeBlock(block, sidecars));
    }

    private IReadOnlyList<PreflightFinding> TagFindings(string path)
    {
        var report = PreflightRunner.Run(new[] { BlockReading(path) }, _projectDir);
        return Assert.Single(report.Files).Findings.Where(f => f.Check == "tag").ToList();
    }

    // --- the defect, all three root kinds it was blind to -------------------------------------

    [Theory]
    [InlineData("DB_Plant.NotAMember")]           // global DB
    [InlineData("DB_Plant.Unit.NotAMember")]      // one level into a UDT-typed member
    [InlineData("IO.NotAMember")]                 // the block's OWN UDT-typed local (C-132 house style)
    public void InventedMember_IsAFinding(string path)
    {
        var finding = Assert.Single(TagFindings(path));

        Assert.Contains(path, finding.Description);
        Assert.Contains("does not resolve", finding.Description);
    }

    // --- the unaffected cases, tested as deliberately as the refused ones ----------------------

    [Theory]
    [InlineData("DB_Plant.Present")]
    [InlineData("DB_Plant.Unit.Running")]
    [InlineData("DB_Plant.AlarmWord")]
    [InlineData("IO.Running")]
    public void RealMember_IsNotAFinding(string path)
    {
        Assert.Empty(TagFindings(path));
    }

    // The honest gap. A root whose member namespace cannot be enumerated (a local typed by an IEC
    // timer, a UDT absent from the export) must produce NO finding — gating on it manufactures the
    // opposite false accusation to the one this check removes, and that is how a gate gets ignored.
    [Fact]
    public void UnenumerableNamespace_IsNotAFinding()
    {
        WriteProjectFile("iDB_Stub.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "iDB_Stub", 21, InstanceOfName: "FB_Whatever", Comment: "No member tree exported.",
            Members: Array.Empty<DbMember>())));

        Assert.Empty(TagFindings("iDB_Stub.AnythingAtAll"));
    }

    // --- C-501 bit slices: what the corpus sweep found ----------------------------------------

    // A trailing ".%Xn" is an access-level slice, not a member. Walking it as a component produced
    // 154 false findings over the committed corpus — every alarm bit in the project.
    [Theory]
    [InlineData("DB_Plant.AlarmWord.%X0")]
    [InlineData("DB_Plant.AlarmWord.%X15")]
    [InlineData("DB_Plant.Unit.Alarm.%X7")]
    [InlineData("IO.Alarm.%X3")]
    public void C501BitSlice_IsNotAnInventedMember(string path)
    {
        Assert.Empty(TagFindings(path));
    }

    // And the slice is bounds-checked, which the old walk could not do at all: bit 16 of a Word is a
    // real defect, precisely located, and NOT the same fact as "invented member".
    [Fact]
    public void BitBeyondTheWidthOfTheSlicedType_IsOutOfRange_NotAbsent()
    {
        var finding = Assert.Single(TagFindings("DB_Plant.AlarmWord.%X16"));

        Assert.Contains("outside the declared bounds", finding.Description);
        Assert.Contains("bit 16", finding.Description);
    }

    // A slice on a member that does not exist reports the MEMBER, not the slice: one defect, one
    // vocabulary. Reporting both would send the reader after the wrong half.
    [Fact]
    public void SliceOnAnInventedMember_ReportsTheMember()
    {
        var finding = Assert.Single(TagFindings("DB_Plant.NoSuchWord.%X0"));

        Assert.Contains("no member 'NoSuchWord'", finding.Description);
    }

    // --- the invariant TagStatusTests asserted in prose and nothing enforced -------------------
    //
    // "reusing ProjectIndex + AccessNode.FromDottedPath (the same primitive preflight uses, so the
    // two never disagree)" — TagStatusTests.cs:11, written while they DID disagree on every invented
    // member. Now a test, so the next divergence fails rather than reads correctly.
    [Theory]
    [InlineData("DB_Plant.NotAMember", true)]
    [InlineData("DB_Plant.Unit.NotAMember", true)]
    [InlineData("DB_Plant.NoSuchWord.%X0", true)]
    [InlineData("DB_Plant.AlarmWord.%X16", true)]
    [InlineData("DB_Plant.Present", false)]
    [InlineData("DB_Plant.Unit.Running", false)]
    [InlineData("DB_Plant.AlarmWord.%X15", false)]
    public void PreflightAndTagstatus_AgreeOnEveryGlobalPath(string path, bool expectedBlocking)
    {
        var tagstatus = TagStatusRunner.Run(new[] { path }, _projectDir);
        var preflight = TagFindings(path);

        Assert.Equal(expectedBlocking, tagstatus.HasBlocking);
        Assert.Equal(expectedBlocking, preflight.Count > 0);
    }
}
