using Converter;
using Converter.Ir;
using Converter.SignalSet;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// <b>FI-88 — a UDT-typed STATIC member used 251 times reported <c>unused</c>, and the binding scaffold
/// went blind.</b>
///
/// <para>Measured on a real program while choosing which blocks a conformance harness could reach:
/// <c>converter signal-set</c> was run over eight function blocks, and on five of them the block's
/// PRINCIPAL CALLER-VISIBLE INTERFACE — one UDT-typed <c>STATIC</c> member — came back as a single
/// unexpanded row reading <c>direction = unused, writers = [], readers = []</c>, at
/// <c>partial: false</c>, <c>examinedNothing: false</c>, exit 0. A confident, complete, wrong answer.
/// Of eight blocks, exactly TWO could be harnessed; the other six were excluded on the strength of
/// that output, and from the outside the result is indistinguishable from "those blocks have no
/// observable interface".</para>
///
/// <para><b>THE CAUSE, AND THE HALF THAT IS NOT A BUG.</b> The inventory expanded a member IFF its
/// sub-members were PHYSICALLY INLINED in the block's own <c>.ir</c> — true of a file round-tripped
/// through a TIA export, false of one a generation pipeline authored, and that difference is the whole
/// defect. It is fixed by resolving the named type. It is NOT fixed by relaxing
/// <c>ProjectUsageGraph.UsagesReaching</c>'s descendant exclusion: once the member expands, the leaf
/// keys match exactly and the struct root stops being a leaf, so nothing is left needing a descendant
/// rule — and admitting descendants generally is the shape that once turned 20 genuine undriven
/// members into 168 driven ones.</para>
///
/// <para>Every fixture here is HAND-WRITTEN and that is not a shortcut: the committed corpus inlines
/// everything, so it cannot exercise the cross-file descent at all (see
/// <c>InterfaceCheckTests.RealCorpus_…_SoCommittedDataCannotExerciseTheCrossFileDescent</c> and
/// <c>SignalInventoryTests</c>). These fixtures are the ONLY coverage the branch has.</para>
/// </summary>
public class SignalSetUdtExpansionTests : IDisposable
{
    private readonly List<string> _dirs = new();

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"signalset-udt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    private static void Write(string dir, string name, string text) =>
        File.WriteAllText(Path.Combine(dir, name), text);

    // The type both corpora below share.
    private const string ThingIoType =
        "TYPE UDT_ThingIO\n" +
        "  ROOTID 0\n" +
        "  MEMBERS\n" +
        "    LevelHigh : Bool\n" +
        "    ThingAlarm : Bool\n" +
        "    Spare : Bool\n";

    /// <summary>
    /// THE DEFECT'S SHAPE: a UDT-typed STATIC with NO inlined body — what a generation pipeline emits,
    /// and what a TIA re-export never does.
    /// </summary>
    private string NonInlinedCorpus()
    {
        var dir = NewDir();
        Write(dir, "FB_Thing.ir",
            "BLOCK FB FB_Thing\n" +
            "ROOTID 0\n" +
            "NUMBER 65\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    IO : \"UDT_ThingIO\"\n" +
            "\n" +
            "NETWORK 1 \"Alarm\"\n" +
            "  COIL IO.ThingAlarm := IO.LevelHigh\n");
        Write(dir, "UDT_ThingIO.ir", ThingIoType);
        return dir;
    }

    /// <summary>The WORKING CONTROL: byte-for-byte the same interface, with the body inlined.</summary>
    private string InlinedCorpus()
    {
        var dir = NewDir();
        Write(dir, "FB_Thing.ir",
            "BLOCK FB FB_Thing\n" +
            "ROOTID 0\n" +
            "NUMBER 65\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    IO : \"UDT_ThingIO\"\n" +
            "      LevelHigh : Bool\n" +
            "      ThingAlarm : Bool\n" +
            "      Spare : Bool\n" +
            "\n" +
            "NETWORK 1 \"Alarm\"\n" +
            "  COIL IO.ThingAlarm := IO.LevelHigh\n");
        Write(dir, "UDT_ThingIO.ir", ThingIoType);
        return dir;
    }

    private static SignalSetReport Run(string dir, string block = "FB_Thing",
        string origin = "any", string? type = null, string direction = "any") =>
        SignalSetRunner.Run(dir, block, origin, type, direction);

    // ------------------------------------------------------------------ 1. the fix

    /// <summary>
    /// 🔴 THE DEFECT, AND ITS EXACT INVERSE. The member is referenced by the block's own logic; before
    /// the repair the document carried ONE row named <c>IO</c>, <c>unused</c>, no writers, no readers.
    /// After it, the leaves are there with their real directions — and there is NO row named
    /// <c>IO</c>, because a struct root that has been opened is not a signal.
    /// </summary>
    [Fact]
    public void NonInlinedUdtStatic_ExpandsToLeavesWithRealDirections()
    {
        var report = Run(NonInlinedCorpus());

        Assert.Equal(SignalDirection.Read, report.Entries.Single(e => e.Member == "IO.LevelHigh").Direction);
        Assert.Equal(SignalDirection.Written, report.Entries.Single(e => e.Member == "IO.ThingAlarm").Direction);
        Assert.Equal(SignalDirection.Unused, report.Entries.Single(e => e.Member == "IO.Spare").Direction);

        Assert.DoesNotContain(report.Entries, e => e.Member == "IO");
        Assert.False(report.Partial);
        Assert.False(report.ExaminedNothing);
    }

    /// <summary>
    /// The writer site is real and named, not merely a non-empty list — this is the column a harness
    /// reads to know what it is about to contend with.
    /// </summary>
    [Fact]
    public void NonInlinedUdtStatic_CarriesTheWriterSite() =>
        Assert.Contains("FB_Thing N1",
            Run(NonInlinedCorpus()).Entries.Single(e => e.Member == "IO.ThingAlarm").Writers);

    // ------------------------------------------------------------------ 2. the regression control

    /// <summary>
    /// 🔴 THE "CANNOT BE EXPAND-EVERYTHING, CANNOT REGRESS THE WORKING CASE" TEST. The inlined form
    /// already worked; the repair must reach the SAME document from the non-inlined form, not merely
    /// a non-empty one. Compared field by field rather than by count, because two documents with the
    /// same number of wrong rows compare equal on a count.
    /// </summary>
    [Fact]
    public void InlinedAndNonInlined_ProduceTheIdenticalEntrySet()
    {
        static IEnumerable<string> Shape(SignalSetReport r) => r.Entries
            .Select(e => string.Join('|', e.Member, e.Path, e.Type, e.Retain, e.StartValue,
                e.Direction, e.Origin, string.Join(',', e.Writers), string.Join(',', e.Readers)))
            .OrderBy(s => s, StringComparer.Ordinal);

        Assert.Equal(Shape(Run(InlinedCorpus())), Shape(Run(NonInlinedCorpus())));
    }

    // ------------------------------------------------------------------ 3-5. the gate

    private string OpaqueCorpus()
    {
        var dir = NewDir();
        Write(dir, "FB_Opaque.ir",
            "BLOCK FB FB_Opaque\n" +
            "ROOTID 0\n" +
            "NUMBER 66\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    IO : \"UDT_NotInThisProject\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");
        return dir;
    }

    /// <summary>
    /// A type the corpus does not define means the member's LEAVES ARE MISSING from a document that
    /// otherwise reads complete — the precise shape of the defect, so it gates. The row is still
    /// EMITTED: dropping it would shorten the set silently, and the dropped entry is exactly the one
    /// nothing else would mention.
    /// </summary>
    [Fact]
    public void TypeNotInTheProject_IsPartialAndNamesTheMember_AndTheRowSurvives()
    {
        var dir = OpaqueCorpus();
        var report = Run(dir, "FB_Opaque");

        Assert.True(report.Partial);
        Assert.False(report.ExaminedNothing);
        Assert.Contains(report.Entries, e => e.Member == "IO");

        var opaque = Assert.Single(report.OpaqueMembers);
        Assert.Equal("FB_Opaque.IO", opaque.Path);
        Assert.Contains("UDT_NotInThisProject", opaque.Reason);
    }

    /// <summary>
    /// 🔴 THE REASON MUST SEPARATE "ONE DIRECTORY DOWN" FROM "DOES NOT EXIST". The scan is
    /// top-directory-only, so a type sitting in a subfolder is absent for a reason the reader can fix
    /// in one move — but only if the message says the scan does not recurse, and names the root and
    /// the file count it actually looked at.
    /// </summary>
    [Fact]
    public void OpaqueReason_NamesTheSearchRootTheFileCountAndTheNoRecursionRule()
    {
        var dir = OpaqueCorpus();
        var reason = Assert.Single(Run(dir, "FB_Opaque").OpaqueMembers).Reason;

        Assert.Contains(dir, reason);
        Assert.Contains("TOP-DIRECTORY-ONLY", reason);
        Assert.Contains(".ir file(s)", reason);
    }

    /// <summary>
    /// The exit code is the only part of this contract a generator downstream reads, and
    /// <c>harness-binding</c> is the generator. <b>1, not 0.</b>
    /// </summary>
    [Fact]
    public void ProgramExit_IsOneForAnOpaqueCorpus() =>
        Assert.Equal(1, Program.RunSignalSet(
            new[] { "--project", OpaqueCorpus(), "--block", "FB_Opaque" }));

    /// <summary>
    /// 🔴 "TYPE ABSENT" AND "TYPE PRESENT BUT BROKEN" USED TO PRODUCE BYTE-IDENTICAL OUTPUT, because
    /// the inventory returned on every TYPE file without parsing it. They need different actions —
    /// supply the file, versus fix the file — so they are told apart: a broken type raises a WARNING
    /// naming the file, which an absent one never does.
    /// </summary>
    [Fact]
    public void UnparseableTypeFile_IsPartialAndDistinguishableFromAnAbsentType()
    {
        var dir = NewDir();
        Write(dir, "FB_Opaque.ir",
            "BLOCK FB FB_Opaque\n" +
            "ROOTID 0\n" +
            "NUMBER 66\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    IO : \"UDT_Broken\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");
        Write(dir, "UDT_Broken.ir",
            "TYPE UDT_Broken\n" +
            "  ROOTID 0\n" +
            "  NOT_A_MEMBERS_SECTION\n");

        var broken = Run(dir, "FB_Opaque");
        var absent = Run(OpaqueCorpus(), "FB_Opaque");

        Assert.True(broken.Partial);
        Assert.NotEmpty(broken.Warnings);
        Assert.Contains(broken.Warnings, w => w.Contains("UDT_Broken.ir", StringComparison.Ordinal));

        // And the contrast that makes the distinction real: an ABSENT type produces no warning at all.
        Assert.Empty(absent.Warnings);
    }

    // ------------------------------------------------------------------ 6-8. the discriminator

    /// <summary>
    /// A multi-instance is an INSTANCE IN ITS OWN RIGHT, not a struct field of its owner: its members
    /// belong to the instantiated block's document, not to this one. One row, and no expansion.
    /// </summary>
    [Fact]
    public void MultiInstanceStatic_IsOneRowAndDoesNotExpand()
    {
        var dir = NewDir();
        Write(dir, "FB_Owner.ir",
            "BLOCK FB FB_Owner\n" +
            "ROOTID 0\n" +
            "NUMBER 70\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    ValveA : \"FB_Valve\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");
        Write(dir, "FB_Valve.ir",
            "BLOCK FB FB_Valve\n" +
            "ROOTID 0\n" +
            "NUMBER 71\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    OpenCmd : Bool\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.B := DB_Input.C\n");

        var report = Run(dir, "FB_Owner");

        Assert.Contains(report.Entries, e => e.Member == "ValveA");
        Assert.DoesNotContain(report.Entries, e => e.Member.StartsWith("ValveA.", StringComparison.Ordinal));
        Assert.False(report.Partial);
    }

    /// <summary>
    /// 🔴 <b>THE TEST THAT FAILS IF ANYONE RE-KEYS THE DISCRIMINATOR ON
    /// <c>TagTypeRegistry._fbInterfaces</c>.</b> That index is built with
    /// <c>IrParser.ParseBlockWithoutSidecar</c>, which THROWS on any file carrying a <c>SIDECAR</c>
    /// section, and the throw is swallowed — so every RE-EXPORTED block is silently missing from it.
    /// A multi-instance of one would then resolve as neither a block nor a type and be reported
    /// OPAQUE: a false gate, on exactly the round-tripped corpora this repair serves. The block-name
    /// set is therefore read off the <c>BLOCK &lt;KIND&gt; &lt;Name&gt;</c> header line instead.
    /// </summary>
    [Fact]
    public void MultiInstanceOfASidecarCarryingBlock_IsStillNotOpaque()
    {
        var dir = NewDir();
        Write(dir, "FB_Owner.ir",
            "BLOCK FB FB_Owner\n" +
            "ROOTID 0\n" +
            "NUMBER 70\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    ValveA : \"FB_Valve\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        // The premise, asserted rather than assumed: this file really does carry a SIDECAR section,
        // and TagTypeRegistry really cannot see the block inside it.
        var withSidecar =
            "BLOCK FB FB_Valve\n" +
            "ROOTID 0\n" +
            "NUMBER 71\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    OpenCmd : Bool\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.B := DB_Input.C\n" +
            "\n" +
            "SIDECAR\n" +
            "NETWORK 1\n" +
            "  compileunit = 3\n";
        Assert.True(Converter.Ir.IrParser.HasSidecarSection(withSidecar));
        Write(dir, "FB_Valve.ir", withSidecar);

        var report = Run(dir, "FB_Owner");

        Assert.Empty(report.OpaqueMembers);
        Assert.False(report.Partial);
        Assert.Contains(report.Entries, e => e.Member == "ValveA");
    }

    /// <summary>
    /// <c>IO : "UDT_Valve"</c> and <c>Valve : "FB_Valve"</c> are BOTH QUOTED, so the datatype string
    /// cannot separate them — only which namespace the name resolves in can, and the BLOCK wins,
    /// because <c>ProjectUsageGraph.ResolveMultiInstances</c> already decides it that way. Two walks
    /// answering this differently is the disagreement this classifier exists to prevent.
    /// </summary>
    [Fact]
    public void WhenAUdtAndABlockShareAName_TheBlockWins()
    {
        var dir = NewDir();
        Write(dir, "FB_Owner.ir",
            "BLOCK FB FB_Owner\n" +
            "ROOTID 0\n" +
            "NUMBER 70\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    Thing : \"Ambiguous\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");
        Write(dir, "Ambiguous_block.ir",
            "BLOCK FB Ambiguous\n" +
            "ROOTID 0\n" +
            "NUMBER 72\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    OpenCmd : Bool\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.B := DB_Input.C\n");
        Write(dir, "Ambiguous_type.ir",
            "TYPE Ambiguous\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    UdtOnlyMember : Bool\n");

        var report = Run(dir, "FB_Owner");

        Assert.Contains(report.Entries, e => e.Member == "Thing");
        Assert.DoesNotContain(report.Entries, e => e.Member == "Thing.UdtOnlyMember");
        Assert.Empty(report.OpaqueMembers);
    }

    // ------------------------------------------------------------------ 9-11. the other terminators

    /// <summary>
    /// An IEC timer's <c>.Q</c>/<c>.ET</c> are written by the INSTRUCTION, not by a caller, so its
    /// definition is not something a corpus can be expected to carry. Without this branch every
    /// generated sequencer — a dwell timer per step, none of them inlined — false-gates.
    /// </summary>
    [Fact]
    public void IecTimerStaticWithNoInlinedBody_IsOneLeafAndNoGate()
    {
        var dir = NewDir();
        Write(dir, "FB_Seq.ir",
            "BLOCK FB FB_Seq\n" +
            "ROOTID 0\n" +
            "NUMBER 73\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    DwellTimer : TON_TIME VERSION 1.0\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        var report = Run(dir, "FB_Seq");

        Assert.Contains(report.Entries, e => e.Member == "DwellTimer");
        Assert.DoesNotContain(report.Entries, e => e.Member.StartsWith("DwellTimer.", StringComparison.Ordinal));
        Assert.Empty(report.OpaqueMembers);
        Assert.False(report.Partial);
    }

    /// <summary>
    /// An array of a UDT is ONE leaf, not opaque and not expanded. Expanding it would lose the element
    /// index, and "is element N unused" is a different question with a different answer shape — which
    /// <c>ProjectUsageGraph.UsagesCovering</c> already says in its own words.
    /// </summary>
    [Fact]
    public void ArrayOfUdt_IsOneAggregateLeafAndNotOpaque()
    {
        var dir = NewDir();
        Write(dir, "FB_Bays.ir",
            "BLOCK FB FB_Bays\n" +
            "ROOTID 0\n" +
            "NUMBER 74\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    Bay : Array[0..3] of \"UDT_ThingIO\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");
        Write(dir, "UDT_ThingIO.ir", ThingIoType);

        var report = Run(dir, "FB_Bays");

        Assert.Contains(report.Entries, e => e.Member == "Bay");
        Assert.DoesNotContain(report.Entries, e => e.Member.StartsWith("Bay.", StringComparison.Ordinal));
        Assert.Empty(report.OpaqueMembers);

        var leaf = Assert.Single(Converter.SignalInventory.SignalInventory.Build(dir).Leaves
            .Where(l => l.Path == "FB_Bays.Bay"));
        Assert.True(leaf.IsAggregate);
    }

    /// <summary>
    /// A UDT that (illegally) references itself terminates at the depth cap and is reported OPAQUE
    /// rather than silently truncated — a truncated set that reads complete is the whole failure this
    /// classifier exists to stop. The test is also a hang detector: before the cap it does not return.
    /// </summary>
    [Fact]
    public void SelfReferentialUdt_IsOpaqueAtTheDepthCap_NotInfinite()
    {
        var dir = NewDir();
        Write(dir, "FB_Loop.ir",
            "BLOCK FB FB_Loop\n" +
            "ROOTID 0\n" +
            "NUMBER 75\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    Head : \"UDT_Loop\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");
        Write(dir, "UDT_Loop.ir",
            "TYPE UDT_Loop\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    Next : \"UDT_Loop\"\n");

        var report = Run(dir, "FB_Loop");

        Assert.True(report.Partial);
        var opaque = Assert.Single(report.OpaqueMembers);
        Assert.Contains("nesting deeper than", opaque.Reason);

        // Bounded, and bounded by the CAP rather than by luck.
        Assert.Equal(Converter.Ir.MemberExpansion.MaxDepth + 1, opaque.Path.Count(c => c == '.'));
    }

    // ------------------------------- 12-13. the false-green guards, on the NEW path

    /// <summary>
    /// The corpus the two guards below need: a non-inlined interface, an instance DB, an orchestrator
    /// that CALLs the block, and a SECOND block declaring an identically-spelled member.
    /// </summary>
    private string AliasCorpus()
    {
        var dir = NewDir();

        Write(dir, "UDT_UnitIO.ir",
            "TYPE UDT_UnitIO\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    Cmd : Bool\n" +
            "    Status : Bool\n" +
            "    Spare : Bool\n");

        // The interface member carries NO inlined body — the whole point. Serialized rather than
        // hand-written so the CALL and the INSTANCEOF forms come from the same writer the tool reads.
        var iface = new[] { new DbMember("IO", "\"UDT_UnitIO\"", Retain: false, StartValue: null) };

        File.WriteAllText(Path.Combine(dir, "FB_Unit.ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", "FB", "FB_Unit", 80, "LAD", null, new[]
            {
                new IrNetwork(1, "act", new[] { new CoilAssignment("IO.Status", new Expr.TagRef("IO.Cmd")) }),
            },
            StaticMembers: iface)));

        // A SECOND block with its own `IO.Cmd`. `_usages` is keyed VERBATIM, so both blocks' bare
        // references land on ONE key.
        File.WriteAllText(Path.Combine(dir, "FB_Other.ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", "FB", "FB_Other", 81, "LAD", null, new[]
            {
                new IrNetwork(1, "other", new[] { new CoilAssignment("IO.Cmd", new Expr.TagRef("DB_Input.B")) }),
            },
            StaticMembers: iface)));

        // A scaffolded instance DB with an EMPTY member section — legitimate, and the shape that made
        // the FB the source of truth for an instance's member tree in the first place.
        File.WriteAllText(Path.Combine(dir, "iDB_Unit.ir"), DbIrSerializer.Serialize(
            new DbSource("0", "iDB_Unit", 2, InstanceOfName: "FB_Unit", Comment: null,
                Members: Array.Empty<DbMember>())));

        File.WriteAllText(Path.Combine(dir, "FC_Orchestrator.ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", "FC", "FC_Orchestrator", 82, "LAD", null, new[]
            {
                new IrNetwork(1, "drive", new[]
                {
                    new CoilAssignment("iDB_Unit.IO.Cmd", new Expr.TagRef("DB_Input.A")),
                },
                Calls: new[]
                {
                    new CallStatement("FB_Unit", "iDB_Unit", new Expr.And(Array.Empty<Expr>()),
                        Array.Empty<CallArgument>()),
                }),
            })));

        return dir;
    }

    /// <summary>
    /// 🔴 THE SIBLING OF <c>SignalSetTests.CallAtTheInstanceRoot_DoesNotMarkEveryMemberWritten</c>, ON
    /// A NON-INLINED INTERFACE. That guard covers the inlined path only, and the new branch produces
    /// its leaves by a different route — so it needs its own. <c>CALL FB_Unit(iDB_Unit)</c> records a
    /// WRITE at the bare instance path, which is an ancestor of every member in it; admitting it is
    /// what turned 20 genuinely undriven members into 168 driven ones.
    /// </summary>
    [Fact]
    public void NonInlined_CallAtTheInstanceRoot_DoesNotMarkEveryMemberWritten()
    {
        var report = Run(AliasCorpus(), "FB_Unit");

        // The premise: the descent really did happen, so this is a statement about the new path.
        Assert.Contains(report.Entries, e => e.Member == "IO.Spare");

        Assert.DoesNotContain("FC_Orchestrator N1",
            report.Entries.Single(e => e.Member == "IO.Spare").Writers);
        Assert.Equal(SignalDirection.Unused, report.Entries.Single(e => e.Member == "IO.Spare").Direction);
    }

    /// <summary>
    /// 🔴 THE MIRROR OF <c>SignalSetTests.AnotherBlocksIdenticallyNamedMember_IsNotPooledIntoThisOne</c>
    /// ON A NON-INLINED INTERFACE. The owner restriction in <c>SignalSetRunner.InterfaceUsages</c> must
    /// survive the new leaves: two FBs each declaring their own <c>IO.Cmd</c> land on ONE verbatim key,
    /// and pooling them puts a phantom contender on a signal a harness is about to drive.
    /// </summary>
    [Fact]
    public void NonInlined_AnotherBlocksIdenticallyNamedMember_IsNotPooledIntoThisOne()
    {
        var report = Run(AliasCorpus(), "FB_Unit");

        Assert.DoesNotContain("FB_Other N1", report.Entries.Single(e => e.Member == "IO.Cmd").Writers);

        // And the real external writer, through the instance alias, IS there — the guard must not have
        // been achieved by dropping external writers altogether.
        Assert.Contains("FC_Orchestrator N1", report.Entries.Single(e => e.Member == "IO.Cmd").Writers);
    }

    // ------------------------------------------------------------------ the filter cannot hide it

    /// <summary>
    /// 🔴 A FILTER NARROWS WHAT IS REPORTED AND NEVER WHAT IS GATED ON. An opaque member is precisely
    /// the entry a <c>--type</c> filter drops — its type is the thing that could not be resolved — so
    /// collecting the opaque set after the filters would let any caller turn the gate off by asking a
    /// narrower question.
    /// </summary>
    [Fact]
    public void OpaqueGate_SurvivesATypeFilterThatExcludesTheOpaqueRow()
    {
        var dir = OpaqueCorpus();
        var report = Run(dir, "FB_Opaque", type: "Bool");

        Assert.DoesNotContain(report.Entries, e => e.Member == "IO");
        Assert.NotEmpty(report.OpaqueMembers);
        Assert.True(report.Partial);
    }

    // --------------------------------------- a sized string is elementary, not a type the corpus lacks

    /// <summary>
    /// 🔴 <b>A STRING'S DECLARED LENGTH IS NOT A TYPE ANY CORPUS CAN DEFINE, AND TREATING IT AS ONE
    /// BUILT A GATE NOBODY COULD EVER CLEAR.</b>
    ///
    /// <para><b>MEASURED on a live corpus the first time this classifier met one.</b> `String[32]`
    /// reached neither the elementary set - which holds bare `String` - nor the type registry, because
    /// no corpus defines `String[32]` and none ever could. It fell through to Opaque, and two entirely
    /// ordinary members took a whole block's run to partial, exit 1.</para>
    ///
    /// <para><b>Why that is the worst kind of gate:</b> at the exit code it is indistinguishable from
    /// the real finding this gate exists for - a member whose type is genuinely absent - and the remedy
    /// it names, add the type's .ir, cannot be carried out. A gate whose instruction cannot be followed
    /// teaches its reader to ignore gates.</para>
    /// </summary>
    [Fact]
    public void ASizedString_IsElementary_AndDoesNotGateTheRun()
    {
        var dir = NewDir();
        Write(dir, "FB_Thing.ir", @"BLOCK FB FB_Thing
ROOTID 0
NUMBER 65
LANGUAGE LAD

INTERFACE
  STATIC
    Label : String[32]
    Wide : WString[16]
    Plain : String
");

        var report = Run(dir);

        Assert.Empty(report.OpaqueMembers);
        Assert.False(report.Partial);
        Assert.Contains(report.Entries, x => x.Member == "Label");
        Assert.Contains(report.Entries, x => x.Member == "Wide");
    }

    /// <summary>
    /// THE CONTROL THAT KEEPS THE STRIP HONEST. Only `String` and `WString` shed a bracketed suffix. A
    /// NAMED type that happens to carry one is still a named type, and if the corpus does not define it
    /// the gate must still fire. Without this control, "strip anything in brackets" would quietly
    /// disarm the gate for every unresolvable type spelled with a suffix.
    /// </summary>
    [Fact]
    public void ABracketedSuffixOnANonStringType_StillGates()
    {
        var dir = NewDir();
        Write(dir, "FB_Thing.ir", @"BLOCK FB FB_Thing
ROOTID 0
NUMBER 65
LANGUAGE LAD

INTERFACE
  STATIC
    Odd : ""UDT_NotHere[4]""
");

        var report = Run(dir);

        Assert.Single(report.OpaqueMembers);
        Assert.True(report.Partial);
    }
}
