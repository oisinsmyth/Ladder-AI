using Converter.Ir;
using Converter.SimaticMl;
using Converter.UndrivenScan;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-18 — the two write mechanisms <c>undriven-scan</c> could not see, and the third shape of
/// "examined nothing" it reported as a pass. Found by running it across a 101-file live corpus and
/// hand-verifying every line.
///
/// <list type="number">
/// <item><b>An ABSOLUTE instance-path write to a MULTI-INSTANCE member was not joined.</b> A member of
/// a multi-instance is addressed two ways: bare and local from inside its owning FB
/// (<c>ValveB.IO.InHand</c>), absolute and rooted on the owner's instance DB from anywhere else
/// (<c>iDB_Cell_North.ValveB.IO.InHand</c>). Only the first was ever looked up, so every
/// write from an orchestrator, a command decoder or a startup block was invisible. Measured: two
/// members on sixteen placements of one valve FB, and six members on eight placements of a motor FB —
/// 80 false UNDRIVEN reports from this mechanism alone.</item>
/// <item><b>A WHOLE-STRUCT write was not attributed to the struct's members.</b> Fifty
/// <c>MOVE(…) =&gt; Selected</c> statements left every <c>Selected.*</c> member reported UNDRIVEN on all
/// four instances of its FB — and worse than noise: those members are ones the FB ITSELF writes, so
/// they should never have been in the caller-driven scope at all. 40 more false reports.</item>
/// <item><b>Zero rows over real instances exited 0.</b> See <see cref="ExaminedNothingTests"/> below.</item>
/// </list>
///
/// <para><c>cross-check</c>, reading the SAME graph, got both joins right, which is the strongest
/// available evidence that this was the scan's defect and not a corpus quirk. The repair therefore
/// went into <c>ProjectUsageGraph.UsagesReaching</c> — shared, on the graph — rather than into a
/// second resolver here.</para>
/// </summary>
public class UndrivenScanJoinTests : IDisposable
{
    private readonly string _dir;

    public UndrivenScanJoinTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"undriven-join-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    // ---------------------------------------------------------------------------------------------
    // Fixture builders. Invented vocabulary throughout — nothing here names live-run plant.
    // ---------------------------------------------------------------------------------------------

    private void WriteBlock(string name, IrNetwork[] networks, DbMember[]? statics = null) =>
        File.WriteAllText(
            Path.Combine(_dir, name + ".ir"),
            IrSerializer.SerializeBlockReadable(new IrBlock(
                "0", name.StartsWith("FC", StringComparison.Ordinal) ? "FC" : "FB",
                name, 1, "LAD", null, networks,
                StaticMembers: statics ?? Array.Empty<DbMember>())));

    private void WriteInstanceDb(string name, string fb, DbMember[] members) =>
        File.WriteAllText(Path.Combine(_dir, name + ".ir"), DbIrSerializer.Serialize(
            new DbSource("0", name, 2, InstanceOfName: fb, Comment: null, Members: members)));

    private static DbMember Struct(string name, params string[] leaves) =>
        new(name, "Struct", false, null,
            NestedMembers: leaves.Select(l => new DbMember(l, "Bool", Retain: false, StartValue: null)).ToArray());

    private static IrNetwork Coil(int n, string target, string source) =>
        new(n, $"drive {target}", new[] { new CoilAssignment(target, new Expr.TagRef(source)) });

    // ---------------------------------------------------------------------------------------------
    // Mechanism 1 — the absolute instance path.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The owner FB holds a multi-instance of the inner FB; an UNRELATED block drives one of the inner
    /// FB's members through the absolute path. Before the fix that write was invisible and the member
    /// was reported UNDRIVEN on every placement.
    /// </summary>
    private void BuildMultiInstanceCorpus()
    {
        // The inner FB reads two members of its interface struct and drives nothing on them.
        WriteBlock("FB_Damper", new[]
        {
            new IrNetwork(1, "act", new[]
            {
                new CoilAssignment("IO.Out", new Expr.TagRef("IO.HandCmd")),
                new CoilAssignment("IO.Out2", new Expr.TagRef("IO.AutoCmd")),

                // Read by the FB, written by NOBODY - the genuine finding the positive control needs.
                new CoilAssignment("IO.Out3", new Expr.TagRef("IO.Spare")),
            }),
        }, new[] { Struct("IO", "HandCmd", "AutoCmd", "Spare", "Out", "Out2", "Out3") });

        // The owner declares the inner FB as a static -> a multi-instance.
        WriteBlock("FB_Cell", new[]
        {
            Coil(1, "DamperA.IO.AutoCmd", "IO.Request"),
        }, new[]
        {
            Struct("IO", "Request"),
            new DbMember("DamperA", "\"FB_Damper\"", Retain: false, StartValue: null),
        });

        WriteInstanceDb("iDB_Cell_North", "FB_Cell", new[]
        {
            Struct("IO", "Request"),
            new DbMember("DamperA", "\"FB_Damper\"", Retain: false, StartValue: null),
        });

        // The orchestrator: calls the owner, and drives the INNER member by its ABSOLUTE path.
        WriteBlock("FC_Line", new[]
        {
            new IrNetwork(1, "call", Array.Empty<CoilAssignment>(),
                Calls: new[] { new CallStatement("FB_Cell", "iDB_Cell_North", new Expr.Literal("TRUE"), Array.Empty<CallArgument>()) }),
            Coil(2, "iDB_Cell_North.DamperA.IO.HandCmd", "DB_Panel.HandPress"),
        });
    }

    [Fact]
    public void AbsoluteInstancePathWrite_DrivesTheMultiInstanceMember()
    {
        BuildMultiInstanceCorpus();

        var report = UndrivenScanRunner.Run(_dir, "FB_Damper", Array.Empty<string>(), Array.Empty<string>());

        var hand = Assert.Single(report.Members.Where(m => m.Member == "IO.HandCmd"));
        Assert.Equal(DriveState.Driven, hand.State);
        Assert.Contains(hand.Writers, w => w.StartsWith("FC_Line ", StringComparison.Ordinal));
    }

    /// <summary>
    /// THE POSITIVE CONTROL. The local form must keep working, and a member NOBODY writes must still be
    /// reported — a fix that made everything look driven would pass the test above and be worthless.
    /// </summary>
    [Fact]
    public void LocalFormStillResolves_AndAGenuinelyUndrivenMemberIsStillReported()
    {
        BuildMultiInstanceCorpus();

        var report = UndrivenScanRunner.Run(_dir, "FB_Damper", Array.Empty<string>(), Array.Empty<string>());

        var auto = Assert.Single(report.Members.Where(m => m.Member == "IO.AutoCmd"));
        Assert.Equal(DriveState.Driven, auto.State);
        Assert.Contains(auto.Writers, w => w.StartsWith("FB_Cell ", StringComparison.Ordinal));

        var spare = Assert.Single(report.Members.Where(m => m.Member == "IO.Spare"));
        Assert.Equal(DriveState.Undriven, spare.State);
        Assert.True(report.HasFindings, "the scan must still find something when something is undriven");
    }

    /// <summary>
    /// 🔴 THE FLOOR, AND THE REASON IT EXISTS. <c>CALL FB_Cell(iDB_Cell_North, …)</c> records a WRITE at
    /// the bare instance path — the CALL naming its own state store, not a data write of the interface.
    /// An ancestor rule without a floor admits it, and then EVERY member of EVERY instance reads as
    /// driven. Measured live while building this fix: a block with 20 genuine undriven members reported
    /// 168 driven ones and exit 0, which is a worse defect than the one being repaired.
    /// </summary>
    [Fact]
    public void CallSiteReferenceToTheInstanceRoot_DoesNotDriveItsMembers()
    {
        // A cell whose damper member NOTHING drives, reached only through a CALL on the owner.
        WriteBlock("FB_Damper", new[]
        {
            new IrNetwork(1, "act", new[] { new CoilAssignment("IO.Out", new Expr.TagRef("IO.HandCmd")) }),
        }, new[] { Struct("IO", "HandCmd", "Out") });

        WriteBlock("FB_Cell", new[] { Coil(1, "IO.Echo", "IO.Request") }, new[]
        {
            Struct("IO", "Request", "Echo"),
            new DbMember("DamperA", "\"FB_Damper\"", Retain: false, StartValue: null),
        });

        WriteInstanceDb("iDB_Cell_North", "FB_Cell", new[]
        {
            Struct("IO", "Request", "Echo"),
            new DbMember("DamperA", "\"FB_Damper\"", Retain: false, StartValue: null),
        });

        WriteBlock("FC_Line", new[]
        {
            new IrNetwork(1, "call", Array.Empty<CoilAssignment>(),
                Calls: new[] { new CallStatement("FB_Cell", "iDB_Cell_North", new Expr.Literal("TRUE"), Array.Empty<CallArgument>()) }),
        });

        var report = UndrivenScanRunner.Run(_dir, "FB_Damper", Array.Empty<string>(), Array.Empty<string>());

        var hand = Assert.Single(report.Members.Where(m => m.Member == "IO.HandCmd"));
        Assert.Equal(DriveState.Undriven, hand.State);
    }

    // ---------------------------------------------------------------------------------------------
    // Mechanism 2 — the whole-struct write.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The FB writes the WHOLE struct with one MOVE. Its members are therefore members the FB itself
    /// writes — outputs it reports, not inputs a caller drives — so they leave the caller-driven scope
    /// entirely rather than being reported UNDRIVEN to a caller that is not supposed to touch them.
    /// </summary>
    [Fact]
    public void WholeStructWrite_TakesTheStructsMembersOutOfCallerScope()
    {
        WriteBlock("FB_Chooser", new[]
        {
            new IrNetwork(1, "select", Array.Empty<CoilAssignment>(),
                Moves: new[] { new MoveStatement(new Expr.TagRef("IO.Pick"), new Expr.TagRef("DB_Book.Entry"), "Chosen") }),
            new IrNetwork(2, "publish", new[] { new CoilAssignment("IO.Ready", new Expr.TagRef("Chosen.Valid")) }),
        }, new[]
        {
            Struct("IO", "Pick", "Ready"),
            Struct("Chosen", "Valid", "Speed", "Mode"),
        });

        WriteInstanceDb("iDB_Chooser_One", "FB_Chooser", new[]
        {
            Struct("IO", "Pick", "Ready"),
            Struct("Chosen", "Valid", "Speed", "Mode"),
        });

        WriteBlock("FC_Line", new[] { Coil(1, "iDB_Chooser_One.IO.Pick", "DB_Panel.Select") });

        var report = UndrivenScanRunner.Run(_dir, "FB_Chooser", Array.Empty<string>(), Array.Empty<string>());

        Assert.DoesNotContain(report.Members, m => m.Member.StartsWith("Chosen.", StringComparison.Ordinal));

        // POSITIVE CONTROL: the real caller-driven member survives, so this is a narrowing of scope and
        // not a scan that stopped looking.
        var pick = Assert.Single(report.Members.Where(m => m.Member == "IO.Pick"));
        Assert.Equal(DriveState.Driven, pick.State);
    }

    /// <summary>
    /// The same mechanism from the CALLER's side: a caller that MOVEs a whole struct into an instance
    /// drives every member of it, and none of them may be reported UNDRIVEN.
    /// </summary>
    [Fact]
    public void CallerWholeStructWrite_DrivesEveryMemberOfTheStruct()
    {
        WriteBlock("FB_Recipe", new[]
        {
            new IrNetwork(1, "use", new[] { new CoilAssignment("IO.Ready", new Expr.TagRef("Params.Speed")) }),
            new IrNetwork(2, "use2", new[] { new CoilAssignment("IO.Ready2", new Expr.TagRef("Params.Mode")) }),
        }, new[]
        {
            Struct("IO", "Ready", "Ready2"),
            Struct("Params", "Speed", "Mode"),
        });

        WriteInstanceDb("iDB_Recipe_One", "FB_Recipe", new[]
        {
            Struct("IO", "Ready", "Ready2"),
            Struct("Params", "Speed", "Mode"),
        });

        WriteBlock("FC_Line", new[]
        {
            new IrNetwork(1, "load", Array.Empty<CoilAssignment>(),
                Moves: new[]
                {
                    new MoveStatement(new Expr.Literal("TRUE"), new Expr.TagRef("DB_Book.Entry"), "iDB_Recipe_One.Params"),
                }),
        });

        var report = UndrivenScanRunner.Run(_dir, "FB_Recipe", Array.Empty<string>(), Array.Empty<string>());

        foreach (var leaf in new[] { "Params.Speed", "Params.Mode" })
        {
            var row = Assert.Single(report.Members.Where(m => m.Member == leaf));
            Assert.Equal(DriveState.Driven, row.State);
        }
    }

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
}
