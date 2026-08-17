using Converter.ConflictGraph;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-17. <b>WHICH PLACEMENT — the two cases a coordinator hit on the first real use of the
/// declared join, and both were resolver gaps rather than bad declarations.</b>
///
/// <list type="number">
/// <item><b>A fully-qualified instance path came back AMBIGUOUS over every instance of the FB.</b> The
/// first fix walked a TRANSITIVE closure over "these two spellings name one storage", and that relation
/// <i>is not transitive</i>: <c>FB|IO.Cmd</c> names the same storage as <c>iDB_A.IO.Cmd</c> and as
/// <c>iDB_B.IO.Cmd</c>, but those two are <b>different memory</b>. Closing over it pooled every
/// instance and then — correctly — refused to guess between them. <b>The bound was right and the
/// pooling should never have happened.</b></item>
/// <item><b>A nested multi-instance path resolved to nothing.</b> An FB placed as a STATIC of another
/// FB is a real instance with real per-instance state and no DB of its own.
/// <c>ProjectUsageGraph</c> has resolved those to a fixpoint since FI-50, nested ones included, and
/// this resolver had never asked.</item>
/// </list>
///
/// <para>*** THE FIXTURE TRAP FOR THIS FILE SPECIFICALLY: a fixture whose FB has ONE instance, or whose
/// declared path is unqualified, proves nothing about case A. *** <c>FB_Unit</c> below has THREE
/// instances, each with its OWN second writer, and every assertion is about which of them a declaration
/// reaches.</para>
/// </summary>
public class ConflictGraphInstanceScopeTests : IDisposable
{
    private readonly string _dir;

    public ConflictGraphInstanceScopeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cg-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // FB_Unit: writes its own `IO.Cmd`, and drives its nested FB_Pump's `IO.Run` through the
        // multi-instance's local root — the second of the two spellings FI-50 records.
        Write("FB_Unit.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Unit", 80, "LAD", null,
            new[]
            {
                new IrNetwork(1, "Own member and the nested pump", new[]
                {
                    new CoilAssignment("IO.Cmd", new Expr.TagRef("IO.Fb")),
                    new CoilAssignment("Pump.IO.Run", new Expr.TagRef("IO.Fb")),
                }),
            },
            StaticMembers: new[]
            {
                new DbMember("IO", "\"UDT_UnitIo\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                    new DbMember("Fb", "Bool", Retain: false, StartValue: null),
                }),
                // *** THE MULTI-INSTANCE: an FB as a STATIC, declared BARE. *** Its members are read
                // off FB_Pump's own interface, never off this declaration site.
                new DbMember("Pump", "\"FB_Pump\"", Retain: false, StartValue: null),
            })));

        // FB_Pump writes its own `IO.Fault` internally — the ONLY reference to that member anywhere.
        Write("FB_Pump.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Pump", 81, "LAD", null,
            new[]
            {
                new IrNetwork(1, "Fault follows run", new[]
                {
                    new CoilAssignment("IO.Fault", new Expr.TagRef("IO.Run")),
                }),
            },
            StaticMembers: new[]
            {
                new DbMember("IO", "\"UDT_PumpIo\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Run", "Bool", Retain: false, StartValue: null),
                    new DbMember("Fault", "Bool", Retain: false, StartValue: null),
                }),
            })));

        // THREE instances of FB_Unit. One instance is not a test of case A.
        foreach (var (instance, number) in new[] { ("iDB_UnitA", 82), ("iDB_UnitB", 83), ("iDB_UnitC", 84) })
        {
            Write($"{instance}.ir", DbIrSerializer.Serialize(new DbSource(
                "0", instance, number, InstanceOfName: "FB_Unit", Comment: null, Members: new[]
                {
                    new DbMember("IO", "\"UDT_UnitIo\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                    {
                        new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                        new DbMember("Fb", "Bool", Retain: false, StartValue: null),
                    }),
                    new DbMember("Pump", "\"FB_Pump\"", Retain: false, StartValue: null),
                })));
        }

        // *** EACH INSTANCE GETS ITS OWN SECOND WRITER, AND THEY ARE DIFFERENT BLOCKS. *** That is what
        // makes "which placement did it reach" answerable from the EDGES rather than from a label.
        Write("FC_DrivesA.ir", Caller("FC_DrivesA", 85, "iDB_UnitA.IO.Cmd"));
        Write("FC_DrivesB.ir", Caller("FC_DrivesB", 86, "iDB_UnitB.IO.Cmd"));
        Write("FC_DrivesPumpA.ir", Caller("FC_DrivesPumpA", 87, "iDB_UnitA.Pump.IO.Run"));
        Write("FC_DrivesPumpB.ir", Caller("FC_DrivesPumpB", 88, "iDB_UnitB.Pump.IO.Run"));
    }

    private static string Caller(string name, int number, string path) =>
        IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FC", name, number, "LAD", null,
            new[] { new IrNetwork(1, "Drive", new[] { new CoilAssignment(path, new Expr.TagRef("Enable")) }) }));

    private void Write(string file, string content) => File.WriteAllText(Path.Combine(_dir, file), content);

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

    private ConflictGraphReport Declared(string signal, string? owner, string path, bool allowUnresolved = false) =>
        ConflictGraphRunner.Run(
            _dir,
            new[] { new CitedSignal(signal, SignalOrigin.Submission) },
            SubmissionSignalMap.Of(new[] { (signal, new DeclaredStorage(owner, path)) }),
            allowUnresolved);

    // ---- CASE A: the qualifier selects -----------------------------------------------------------

    // *** A DECLARATION THAT NAMES THE INSTANCE RESOLVES TO THAT INSTANCE. *** Before this, the closure
    // fanned out through the FB-internal reference to every sibling and refused.
    [Fact]
    public void AFullyQualifiedInstancePath_ResolvesToThatInstanceAlone()
    {
        var report = Declared("SPEC.UnitACommand", null, "iDB_UnitA.IO.Cmd");

        Assert.True(report.Computed);
        var fact = Assert.Single(report.Signals);
        Assert.Equal(SignalResolution.Resolved, fact.Resolution);
        Assert.Equal(new[] { "iDB_UnitA.IO.Cmd" }, fact.Candidates);

        // The edge is FB_Unit (internal write, which lands in EVERY instance including this one) and
        // A's own caller. *** FC_DrivesB IS NOT HERE, AND THAT IS THE WHOLE RULING: *** B is different
        // memory, and attributing its writer to A would be a fiction about the plant.
        var edge = Assert.Single(report.Edges);
        Assert.Equal("FB_Unit", edge.BlockA);
        Assert.Equal("FC_DrivesA", edge.BlockB);
    }

    // The sibling resolves to ITS OWN writer — the same question asked of the other instance must give
    // a different answer, or the qualifier is being ignored rather than honoured.
    [Fact]
    public void TheSiblingInstanceResolvesToItsOwnWriter_NotToTheFirstOnes()
    {
        var edge = Assert.Single(Declared("SPEC.UnitBCommand", null, "iDB_UnitB.IO.Cmd").Edges);

        Assert.Equal("FB_Unit", edge.BlockA);
        Assert.Equal("FC_DrivesB", edge.BlockB);
    }

    // An instance NOBODY drives from outside still resolves — the FB's own write reaches it — and has
    // no cross-block conflict. A single writer is not an edge, and an empty edge list here is EARNED.
    [Fact]
    public void AnInstanceWithNoExternalWriter_ResolvesAndSimplyHasNoConflict()
    {
        var report = Declared("SPEC.UnitCCommand", null, "iDB_UnitC.IO.Cmd");

        Assert.True(report.Computed);
        Assert.Equal(SignalResolution.Resolved, Assert.Single(report.Signals).Resolution);
        Assert.Empty(report.Edges);
    }

    // *** AND THE REFUSAL IS KEPT WHERE IT IS EARNED. *** An OWNER-qualified declaration names a member
    // of the CLASS, so with three placements it names three locations and nothing said which. That is a
    // refusal about the declaration, and its repair is in the message: name the placement.
    [Fact]
    public void AnOwnerQualifiedDeclarationOnAMultiPlacementFb_IsStillRefused_NamingEveryPlacement()
    {
        var report = Declared("SPEC.UnitCommand", "FB_Unit", "IO.Cmd");

        Assert.False(report.Computed);
        var fact = Assert.Single(report.Ambiguous);
        Assert.Equal(
            new[] { "iDB_UnitA.IO.Cmd", "iDB_UnitB.IO.Cmd", "iDB_UnitC.IO.Cmd" },
            fact.Candidates.OrderBy(c => c, StringComparer.Ordinal));
        Assert.Contains("NAME THE PLACEMENT AND THIS RESOLVES", fact.Reason);
    }

    // ---- CASE B: nested multi-instances ----------------------------------------------------------

    // *** A NESTED MULTI-INSTANCE PATH RESOLVES, AND THROUGH BOTH SPELLINGS. *** `FB_Pump.IO.Run` is
    // how the pump's own logic would address it and `FB_Unit.Pump.IO.Run` is how its owner does; both
    // land in this placement's memory, so both writers count.
    [Fact]
    public void ANestedMultiInstancePath_Resolves_AndUnionsBothSpellings()
    {
        var report = Declared("SPEC.PumpARun", null, "iDB_UnitA.Pump.IO.Run");

        Assert.True(report.Computed);
        var fact = Assert.Single(report.Signals);
        Assert.Equal(SignalResolution.Resolved, fact.Resolution);
        Assert.Equal(new[] { "iDB_UnitA.Pump.IO.Run" }, fact.Candidates);
        Assert.Contains("FB_Unit.Pump.IO.Run", fact.Reason);

        var edge = Assert.Single(report.Edges);
        Assert.Equal("FB_Unit", edge.BlockA);
        Assert.Equal("FC_DrivesPumpA", edge.BlockB);
    }

    // The nested sibling is different memory too — one level down, the same ruling.
    [Fact]
    public void TheNestedSiblingIsDifferentMemory()
    {
        var edge = Assert.Single(Declared("SPEC.PumpBRun", null, "iDB_UnitB.Pump.IO.Run").Edges);

        Assert.Equal("FC_DrivesPumpB", edge.BlockB);
    }

    // *** THE MEMBER NOTHING OUTSIDE EVER TOUCHES. *** `IO.Fault` is written only by FB_Pump's own
    // logic, so the corpus contains no reference rooted at any instance at all — which is the shape
    // that made a whole slot resolve 0 of 68. It resolves through the placement index.
    [Fact]
    public void ANestedMemberWrittenOnlyInsideItsOwnBlock_StillResolvesPerPlacement()
    {
        foreach (var instance in new[] { "iDB_UnitA", "iDB_UnitB", "iDB_UnitC" })
        {
            var report = Declared("SPEC.PumpFault", null, $"{instance}.Pump.IO.Fault");

            Assert.True(report.Computed);
            var fact = Assert.Single(report.Signals);
            Assert.Equal(SignalResolution.Resolved, fact.Resolution);
            Assert.Equal(new[] { $"{instance}.Pump.IO.Fault" }, fact.Candidates);
            Assert.Contains("FB_Pump.IO.Fault", fact.Reason);
            Assert.Empty(report.Edges);
        }
    }

    // And the owner-form of a nested member is refused over its three placements, exactly as the
    // top-level one is — the rule does not stop applying one level down.
    [Fact]
    public void TheOwnerFormOfANestedMember_IsRefusedOverEveryPlacement()
    {
        var report = Declared("SPEC.PumpRun", "FB_Pump", "IO.Run");

        Assert.False(report.Computed);
        Assert.Equal(
            new[] { "iDB_UnitA.Pump.IO.Run", "iDB_UnitB.Pump.IO.Run", "iDB_UnitC.Pump.IO.Run" },
            Assert.Single(report.Ambiguous).Candidates.OrderBy(c => c, StringComparer.Ordinal));
    }

    // ---- The refusals that must survive ----------------------------------------------------------

    // A declaration naming an instance that does not exist is UNRESOLVED, not quietly rounded to a
    // sibling that does. The qualifier selecting is not the same as the qualifier being ignored.
    [Fact]
    public void ADeclarationNamingAnInstanceThatDoesNotExist_IsUnresolved()
    {
        var report = Declared("SPEC.Ghost", null, "iDB_UnitZ.IO.Cmd");

        Assert.False(report.Computed);
        Assert.Equal(SignalResolution.Unresolved, Assert.Single(report.Signals).Resolution);
    }

    // A member that exists on the FB but not at the named placement's path is unresolved too — the
    // location is checked, not merely the instance name.
    [Fact]
    public void ADeclarationNamingARealInstanceAndAnUnknownMember_IsUnresolved()
    {
        var report = Declared("SPEC.Ghost", null, "iDB_UnitA.IO.NoSuchMember");

        Assert.False(report.Computed);
        Assert.Equal(SignalResolution.Unresolved, Assert.Single(report.Signals).Resolution);
    }
}
