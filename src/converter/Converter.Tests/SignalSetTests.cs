using Converter.Ir;
using Converter.SignalSet;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `converter signal-set` (2026-08-27) — one block's signal set as one machine-readable document: the
/// mechanically-derivable half of a harness binding, which was hand-transcribed until now (297 lines
/// for one slot of 20 signals, roughly half of it a restatement of the block's own IR).
///
/// <para>The corpus below is the shape the document has to survive: an FB whose caller-facing members
/// live under STATIC inside an interface UDT (C-132), a global DB, a tag table, an orchestrator that
/// writes one member through the instance DB, and a SECOND FB declaring an identically-spelled member
/// — the alias/pooling pair that has produced a false finding and a false green in this codebase
/// already.</para>
/// </summary>
public class SignalSetTests : IDisposable
{
    private readonly string _dir;

    public SignalSetTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"signalset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        var iface = new[]
        {
            new DbMember("IO", "Struct", false, null, NestedMembers: new[]
            {
                // read by the FB, written by the orchestrator through the instance DB
                new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                // written by the FB
                new DbMember("Status", "Bool", Retain: false, StartValue: null),
                // neither read nor written by anything
                new DbMember("Spare", "Bool", Retain: false, StartValue: null),
            }),
            new DbMember("Setpoint", "Real", Retain: true, StartValue: "5.0"),
        };

        WriteBlock("FB_Unit", "FB", iface, new[]
        {
            new IrNetwork(1, "act", new[]
            {
                new CoilAssignment("IO.Status",
                    new Expr.And(new Expr[] { new Expr.TagRef("IO.Cmd"), new Expr.TagRef("Start_PB") })),
                new CoilAssignment("DB_Plant.Out", new Expr.TagRef("DB_Plant.Mode")),
                // A referenced path nothing in the corpus declares — a raw marker address.
                new CoilAssignment("DB_Plant.Out", new Expr.TagRef("%M12.3")),
            }),
            new IrNetwork(2, "setpoint", new[]
            {
                new CoilAssignment("IO.Status", new Expr.TagRef("Setpoint")),
            }),
        });

        WriteInstanceDb("iDB_Unit", "FB_Unit", iface);

        // A SECOND block declaring its own `IO.Cmd`. `ProjectUsageGraph._usages` is keyed VERBATIM, so
        // both blocks' bare references land on one key — the shape that made cross-check report
        // multi-writers that did not exist (2026-08-14).
        WriteBlock("FB_Other", "FB", new[]
        {
            new DbMember("IO", "Struct", false, null, NestedMembers: new[]
            {
                new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
            }),
        }, new[]
        {
            new IrNetwork(1, "other", new[]
            {
                new CoilAssignment("IO.Cmd", new Expr.TagRef("Start_PB")),
            }),
        });

        // The orchestrator: calls the FB (naming its instance DB) and drives one member absolutely.
        WriteBlock("FC_Orchestrator", "FC", Array.Empty<DbMember>(), new[]
        {
            new IrNetwork(1, "drive", new[]
            {
                new CoilAssignment("iDB_Unit.IO.Cmd", new Expr.TagRef("Start_PB")),
            },
            Calls: new[]
            {
                new CallStatement("FB_Unit", "iDB_Unit", new Expr.And(Array.Empty<Expr>()),
                    Array.Empty<CallArgument>()),
            }),
        });

        File.WriteAllText(Path.Combine(_dir, "DB_Plant.ir"), DbIrSerializer.Serialize(
            new DbSource("0", "DB_Plant", 10, InstanceOfName: null, Comment: null, Members: new[]
            {
                new DbMember("Mode", "Int", Retain: false, StartValue: "2"),
                new DbMember("Out", "Bool", Retain: false, StartValue: null),
            })));

        File.WriteAllText(Path.Combine(_dir, "IoTags.ir"),
            "TAGTABLE IoTags\n  ROOTID 0\n  TAGS\n    Start_PB 1 : Bool @ %I0.0\n");
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

    private SignalSetReport Run(string block = "FB_Unit", string origin = "any",
        string? type = null, string direction = "any") =>
        SignalSetRunner.Run(_dir, block, origin, type, direction);

    private SignalEntry Entry(string member) =>
        Run().Entries.Single(e => e.Member == member);

    // --- the spec: what the document states about each signal -----------------------------------

    [Fact]
    public void MemberTheBlockOnlyReads_IsDirectionRead() =>
        Assert.Equal(SignalDirection.Read, Entry("IO.Cmd").Direction);

    [Fact]
    public void MemberTheBlockOnlyWrites_IsDirectionWritten() =>
        Assert.Equal(SignalDirection.Written, Entry("IO.Status").Direction);

    [Fact]
    public void MemberNothingTouches_IsDirectionUnused() =>
        Assert.Equal(SignalDirection.Unused, Entry("IO.Spare").Direction);

    [Fact]
    public void MemberType_IsCarriedFromTheDeclaration() =>
        Assert.Equal("Real", Entry("Setpoint").Type);

    [Fact]
    public void MemberRetain_IsCarriedFromTheDeclaration() =>
        Assert.True(Entry("Setpoint").Retain);

    [Fact]
    public void MemberStartValue_IsCarriedFromTheDeclaration() =>
        Assert.Equal("5.0", Entry("Setpoint").StartValue);

    [Fact]
    public void FilesScanned_IsTheDenominator() =>
        Assert.Equal(6, Run().FilesScanned);

    // The whole point of the referenced half: an external writer is what a harness contends with.
    [Fact]
    public void ExternalWriterOfAMember_IsListedThroughTheInstanceAlias() =>
        Assert.Contains("FC_Orchestrator N1", Entry("IO.Cmd").Writers);

    [Fact]
    public void GlobalDbMemberTheBlockReads_IsOriginGlobalDb() =>
        Assert.Equal(SignalDeclaration.GlobalDb, Entry("DB_Plant.Mode").Origin);

    [Fact]
    public void TagTableTagTheBlockReads_IsOriginTagTable() =>
        Assert.Equal(SignalDeclaration.TagTable, Entry("Start_PB").Origin);

    [Fact]
    public void GlobalDbMemberTheBlockWrites_IsDirectionWritten() =>
        Assert.Equal(SignalDirection.Written, Entry("DB_Plant.Out").Direction);

    [Fact]
    public void OriginFilterInterface_ExcludesTheReferencedHalf() =>
        Assert.All(Run(origin: "interface").Entries,
            e => Assert.Equal(SignalDeclaration.Interface, e.Origin));

    [Fact]
    public void TypeFilter_NarrowsToThatType() =>
        Assert.All(Run(type: "Real").Entries, e => Assert.Equal("Real", e.Type));

    [Fact]
    public void DirectionFilter_NarrowsToThatDirection() =>
        Assert.All(Run(direction: "unused").Entries,
            e => Assert.Equal(SignalDirection.Unused, e.Direction));

    // --- what the spec omits, tested as its own class -------------------------------------------
    //
    // The output shape says "member, type, retain, startValue, direction, writers, readers, origin"
    // and says nothing about which paths are signals at all. Every case below is a way the set can be
    // silently wrong while every named field is populated.

    /// <summary>
    /// 🔴 A bare interface path is keyed VERBATIM in the usage graph, so two FBs each declaring their
    /// own `IO.Cmd` land on one key. Pooling them puts another block's write into this block's
    /// document — the false-multi-writer defect `cross-check` shipped in 2026-08-14, arriving here as
    /// a phantom contender on a signal a harness is about to drive.
    /// </summary>
    [Fact]
    public void AnotherBlocksIdenticallyNamedMember_IsNotPooledIntoThisOne() =>
        Assert.DoesNotContain("FB_Other N1", Entry("IO.Cmd").Writers);

    /// <summary>
    /// `CALL FB_Unit(iDB_Unit, …)` records a WRITE at the bare instance path. It is the call naming
    /// its own state store, not a data write, and listing it would put a row in the binding document
    /// with no value to read or write. (The same reference, admitted as an ancestor, is what turned 20
    /// undriven members into 168 driven ones in `undriven-scan`.)
    /// </summary>
    [Fact]
    public void CallNamingItsOwnInstanceDb_IsNotAReferencedSignal() =>
        Assert.DoesNotContain(Run("FC_Orchestrator").Entries, e => e.Member == "iDB_Unit");

    /// <summary>
    /// The CALL's write at the instance root is an ancestor of every member in it. Admitting it would
    /// report the whole interface as written by the caller.
    /// </summary>
    [Fact]
    public void CallAtTheInstanceRoot_DoesNotMarkEveryMemberWritten() =>
        Assert.DoesNotContain("FC_Orchestrator N1", Entry("IO.Spare").Writers);

    /// <summary>
    /// A referenced path the corpus declares nowhere is EMITTED and labelled, never dropped. A binding
    /// generator handed a silently-shortened signal set produces a binding that looks complete, and
    /// the missing entries are exactly the ones nothing else will mention.
    /// </summary>
    [Fact]
    public void ReferencedPathWithNoDeclaration_IsOriginUndeclared() =>
        Assert.Equal(SignalDeclaration.Undeclared, Entry("%M12.3").Origin);

    /// <summary>Nothing in the corpus states its type, and naming one would be an invented fact.</summary>
    [Fact]
    public void UndeclaredReference_HasNoType() =>
        Assert.Null(Entry("%M12.3").Type);

    /// <summary>
    /// The block's own members are reached through `OwnerOf`, not through a name heuristic: an FB
    /// static and a tag-table tag are both bare single-component references, and only the block's
    /// declaration set separates them. A member appearing in BOTH halves would be double-counted by
    /// every consumer of the count.
    /// </summary>
    [Fact]
    public void BlockLocalMember_AppearsOnceAndOnlyInTheInterfaceHalf() =>
        Assert.Equal(1, Run().Entries.Count(e => e.Member == "Setpoint"));

    /// <summary>
    /// The two halves are counted separately because they answer different questions, and one total
    /// hides which half is empty.
    /// </summary>
    [Fact]
    public void CountsSplitTheTwoHalves() =>
        Assert.Equal(Run().Entries.Count, Run().InterfaceCount + Run().ReferencedCount);

    /// <summary>
    /// An FC declares no interface, so its whole set is the referenced half — the case a check keyed
    /// on interface members alone reports as "no signals" on a block that plainly has some.
    /// </summary>
    [Fact]
    public void BlockWithNoInterface_StillHasItsReferencedSignals() =>
        Assert.NotEmpty(Run("FC_Orchestrator").Entries);
}
