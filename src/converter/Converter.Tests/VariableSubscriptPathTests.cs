using Converter.Ir;
using Converter.Preflight;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// The bracket-aware path splitter, tested as a primitive (<see cref="TagPath"/>).
///
/// <para>
/// A '.' inside an array subscript is not a component boundary. That only started mattering on
/// 2026-08-27, when the converter began accepting a VARIABLE subscript: a symbolic index is itself a
/// dotted path, so every plain <c>Split('.')</c> in the codebase began cutting valid paths into
/// pieces that name nothing. Commit 1a6eed0 repaired ONE splitter with a private copy of this logic;
/// a live run then hit the others, which is what earned the shared mechanism.
/// </para>
/// </summary>
public class TagPathTests
{
    // The exact construct from the live run, with the names replaced by synthetic ones.
    private const string Real = "DB_Config.Profile[iDB_Unit_A.Cycle.ChosenIndex].TargetValue";

    [Fact]
    public void Split_SymbolicSubscript_KeepsTheIndexInsideItsOwnComponent()
    {
        Assert.Equal(
            new[] { "DB_Config", "Profile[iDB_Unit_A.Cycle.ChosenIndex]", "TargetValue" },
            TagPath.Split(Real));
    }

    // The measured control from the isolation: the identical path with a LITERAL index behaved
    // correctly all along, so the two must now split to the same SHAPE.
    [Fact]
    public void Split_LiteralSubscript_SplitsToTheSameShape()
    {
        Assert.Equal(
            new[] { "DB_Config", "Profile[1]", "TargetValue" },
            TagPath.Split("DB_Config.Profile[1].TargetValue"));
    }

    [Theory]
    [InlineData("Plain", 1)]
    [InlineData("A.B", 2)]
    [InlineData("A.B.C", 3)]
    [InlineData("Arr[2].B", 2)]
    public void Split_OrdinaryPaths_AreUnchangedByTheBracketAwareness(string path, int expected) =>
        Assert.Equal(expected, TagPath.Split(path).Length);

    // Degrade, never drop: an unbalanced '[' leaves the remainder whole rather than inventing
    // boundaries inside it. A malformed path stays malformed and visible.
    [Fact]
    public void Split_UnbalancedBracket_DegradesToWholeString() =>
        Assert.Equal(new[] { "A", "B[C.D" }, TagPath.Split("A.B[C.D"));

    [Fact]
    public void IndexOfSeparator_SubscriptOnTheROOT_SkipsTheDotInsideTheBrackets() =>
        Assert.Equal("Buffer[iDB.Slot]".Length, TagPath.IndexOfSeparator("Buffer[iDB.Slot].Value"));

    [Fact]
    public void LastIndexOfSeparator_SubscriptOnTheLEAF_SkipsTheDotInsideTheBrackets() =>
        Assert.Equal("IO".Length, TagPath.LastIndexOfSeparator("IO.Profile[iDB.Seq.Slot]"));

    [Fact]
    public void LastIndexOfSeparator_SingleComponent_IsMinusOne() =>
        Assert.Equal(-1, TagPath.LastIndexOfSeparator("Profile[iDB.Seq.Slot]"));

    // A literal and a symbolic index are stripped alike: the callers ask "which declared thing does
    // this name?", and that answer cannot depend on how the element was selected.
    [Fact]
    public void StripSubscripts_StripsLiteralAndSymbolicAlike() =>
        Assert.Equal("A.B", TagPath.StripSubscripts("A[3].B[iDB.Slot]"));

    [Fact]
    public void StripSubscripts_UnbalancedBracket_LeavesThePathUntouched() =>
        Assert.Equal("A[3.B", TagPath.StripSubscripts("A[3.B"));

    // The count-capped overload is the bracket-aware generalization of `string.Split('.', count)`:
    // excess separators merge into the LAST piece, which is what keeps the `Clock_0.5Hz` system tags
    // (one component whose own name contains a dot) intact for the sanitizer.
    [Fact]
    public void SplitWithCount_OneComponent_LeavesALiteralDotNameWhole() =>
        Assert.Equal(new[] { "Clock_0.5Hz" }, TagPath.Split("Clock_0.5Hz", 1));

    [Fact]
    public void SplitWithCount_CountsCOMPONENTSNotRawDots() =>
        Assert.Equal(
            new[] { "DB", "Profile[iDB.Slot]", "Target" },
            TagPath.Split("DB.Profile[iDB.Slot].Target", 3));

    [Fact]
    public void SplitWithCount_ExcessSeparatorsMergeIntoTheLastPiece() =>
        Assert.Equal(new[] { "A", "B.C" }, TagPath.Split("A.B.C", 2));
}

/// <summary>
/// The defect as the live run reported it, at the two surfaces that reported it — and both
/// directions, because a fix that silences a false positive by weakening the rule is worse than the
/// bug it removes.
///
/// <para>
/// How it was isolated (2026-08-27): <c>preflight</c> on the pre-edit file exited 0 with 0 findings;
/// the identical edit with the subscript written as a literal <c>[1]</c>, nothing else changed,
/// also exited 0 with 0 findings; <c>tagstatus</c> independently confirmed the underlying tags
/// exist. So the DOTTED INDEX ALONE was the trigger, and both findings —
/// <c>member path … does not resolve: no member 'Sequence'</c> and
/// <c>C-005 Name component 'ChosenIndex]' …</c> — were false positives on valid wiring.
/// </para>
/// </summary>
public class VariableSubscriptFalsePositiveTests : IDisposable
{
    private readonly string _projectDir;
    private readonly List<string> _tempPaths = new();

    // `<GlobalDb>.<Array>[<instanceDb>.<nested>.<member>]` — the shape that failed. Synthetic names.
    private const string ValidPath = "DB_Config.Profile[iDB_Unit_A.Cycle.ChosenIndex].Enabled";

    public VariableSubscriptFalsePositiveTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"varsubscript-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);

        WriteProjectFile("UDT_ProfileSlot.ir", TypeIrSerializer.Serialize(new PlcTypeSource("0", "UDT_ProfileSlot", null, new[]
        {
            new DbMember("Enabled", "Bool", Retain: false, StartValue: null),
            new DbMember("TargetValue", "Real", Retain: false, StartValue: null),
        })));

        WriteProjectFile("UDT_SeqState.ir", TypeIrSerializer.Serialize(new PlcTypeSource("0", "UDT_SeqState", null, new[]
        {
            new DbMember("ChosenIndex", "Int", Retain: false, StartValue: null),
        })));

        WriteProjectFile("DB_Config.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Config", 30, InstanceOfName: null, Comment: "Settings.", Members: new[]
            {
                new DbMember("Present", "Bool", Retain: false, StartValue: null),
                new DbMember("Profile", "Array[0..7] of \"UDT_ProfileSlot\"", Retain: false, StartValue: null),
            })));

        WriteProjectFile("iDB_Unit_A.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "iDB_Unit_A", 31, InstanceOfName: null, Comment: "Silo state.", Members: new[]
            {
                new DbMember("Sequence", "\"UDT_SeqState\"", Retain: false, StartValue: null),
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

    private static IrBlock BlockReading(string path) =>
        new("0", "FC", "FC_Probe", 41, "LAD", "Header.", new[]
        {
            new IrNetwork(1, "Read", new[]
            {
                new CoilAssignment("DB_Config.Present", new Expr.TagRef(path)),
            }),
        });

    // --- preflight: the member walk (tagstatus's own walk, shared) -----------------------------

    private IReadOnlyList<PreflightFinding> TagFindings(string path)
    {
        var block = BlockReading(path);
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();

        var file = Path.Combine(Path.GetTempPath(), $"varsubscript-batch-{Guid.NewGuid():N}.ir");
        File.WriteAllText(file, IrSerializer.SerializeBlock(block, sidecars));
        _tempPaths.Add(file);

        return Assert.Single(PreflightRunner.Run(new[] { file }, _projectDir).Files)
            .Findings.Where(f => f.Check == "tag").ToList();
    }

    [Fact]
    public void Preflight_VariableSubscriptPath_IsNotAFinding() =>
        Assert.Empty(TagFindings(ValidPath));

    // The literal-index control from the isolation: it passed before the fix and must still pass.
    [Fact]
    public void Preflight_LiteralSubscriptPath_IsStillNotAFinding() =>
        Assert.Empty(TagFindings("DB_Config.Profile[1].Enabled"));

    // THE OTHER DIRECTION. The walk must still reach through the subscript and refuse an invented
    // member — hard rule 3's anti-laundering gate is the whole reason this walk exists.
    [Fact]
    public void Preflight_InventedMemberBehindAVariableSubscript_IsStillAFinding()
    {
        var finding = Assert.Single(
            TagFindings("DB_Config.Profile[iDB_Unit_A.Cycle.ChosenIndex].NotAMember"));

        Assert.Contains("does not resolve", finding.Description);
        Assert.Contains("NotAMember", finding.Description);
    }

    [Fact]
    public void Preflight_InventedRootCarryingAVariableSubscript_IsStillAFinding() =>
        Assert.NotEmpty(TagFindings("DB_NoSuchThing.Profile[iDB_Unit_A.Cycle.ChosenIndex].Enabled"));

    // --- review C-005: the name-component charset walk -----------------------------------------

    private static IReadOnlyList<Finding> C005(string path) =>
        Rules.CheckC005Charset(BlockReading(path)).ToList();

    [Fact]
    public void C005_VariableSubscriptPath_IsClean() => Assert.Empty(C005(ValidPath));

    [Fact]
    public void C005_LiteralSubscriptPath_IsStillClean() =>
        Assert.Empty(C005("DB_Config.Profile[1].Enabled"));

    // THE OTHER DIRECTION, three shapes of genuinely-invalid component. The false positive this fix
    // removes was worded "Name component 'ChosenIndex]' contains characters other than
    // letters/digits/underscore" — so a component that really IS `Slot]`, outside any subscript,
    // is the exact inverse case and must still report.
    [Theory]
    [InlineData("DB_Config.Slot].Enabled", "Slot]")]        // stray ']' outside any subscript
    [InlineData("DB_Config.Bad Name.Enabled", "Bad Name")]  // space
    [InlineData("DB_Config.Bad-Name.Enabled", "Bad-Name")]  // hyphen
    public void C005_MalformedComponent_IsStillFlagged(string path, string expected)
    {
        var finding = Assert.Single(C005(path));

        Assert.Equal("C-005", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains($"'{expected}'", finding.Description);
    }

    // A variable subscript is a tag path in its own right — TIA resolves it as one and it is emitted
    // as its own nested <Access> with one <Component> per segment — so C-005 judges its segments as
    // names too. Before the bracket-aware split they were checked BY ACCIDENT, as bogus outer
    // components, which is exactly why a valid one was accused; now they are checked on purpose.
    [Theory]
    [InlineData("DB_Config.Profile[iDB_Unit_A.Cycle.Selected-Slot].Enabled", "Selected-Slot")]
    [InlineData("DB_Config.Profile[iDB_Unit_A.Bad Name.ChosenIndex].Enabled", "Bad Name")]
    public void C005_MalformedSegmentInsideAVariableSubscript_IsFlagged(string path, string expected)
    {
        var finding = Assert.Single(C005(path));

        Assert.Equal("C-005", finding.RuleId);
        Assert.Contains($"'{expected}'", finding.Description);
        Assert.Contains("subscript", finding.Description);
    }

    // A LITERAL subscript is integers, not names: running the identifier test over `[3]` or `[0,1]`
    // would accuse every array element in the corpus.
    [Theory]
    [InlineData("DB_Config.Profile[3].Enabled")]
    [InlineData("DB_Config.Profile[0,1].Enabled")]
    [InlineData("DB_Config.Profile[-1].Enabled")]
    public void C005_LiteralSubscript_IsNeverCharsetChecked(string path) => Assert.Empty(C005(path));
}
