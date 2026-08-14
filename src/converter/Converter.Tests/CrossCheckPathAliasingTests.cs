using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-14. <b>`cross-check` REPORTED MULTI-WRITERS THAT DID NOT EXIST.</b>
///
/// <para>The usage graph keys every path VERBATIM, and an FB addresses its own interface member with
/// no root at all — `IO.Step`, `Time`. So three FBs, each with its own `IO` member of its own UDT
/// type and its own `Time : Real`, all landed on one key, and the tool reported them as CROSS-BLOCK
/// multi-writers. <b>They are different members of different instances that happen to share a leaf
/// name.</b></para>
///
/// <para>A false finding is treated here as the equal of a false green — <i>the first one is what
/// gets a check switched off</i> — and this one was found only because a lane refused to feed the
/// output into a submission gate it did not trust. Trusted, it would have put fictitious edges into
/// a conflict graph and separated slots that never conflicted.</para>
///
/// <para>*** THE WHOLE POINT OF THIS FILE IS THAT IT TESTS BOTH DIRECTIONS. *** A fix that silences
/// the false finding by silencing everything is the obvious failure mode, so every aliasing
/// assertion below is paired with a GENUINE cross-block multi-writer that must still be found. The
/// pre-existing suite could not tell the two apart: it stayed green through both the defect and the
/// fix.</para>
/// </summary>
public class CrossCheckPathAliasingTests : IDisposable
{
    private readonly string _dir;

    public CrossCheckPathAliasingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crosscheck-alias-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // ---- The aliasing shape, taken from the real corpus ------------------------------------
        // Two FBs, each declaring its OWN `IO` static of its OWN UDT and its OWN `Time` temp. Both
        // write `IO.Step` twice and `Time` once. Nothing is shared between them but the spelling.
        WriteBlock("FB_One.ir", Fb("FB_One", 1, "\"UDT_OneIO\""));
        WriteBlock("FB_Two.ir", Fb("FB_Two", 2, "\"UDT_TwoIO\""));

        WriteDb("iDB_One.ir", new DbSource("0", "iDB_One", 10, InstanceOfName: "FB_One", Comment: null, Members: new[]
        {
            new DbMember("IO", "\"UDT_OneIO\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
            {
                new DbMember("Step", "Int", Retain: false, StartValue: null),
                new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
            }),
        }));

        // ---- The genuine cross-block conflicts, which must SURVIVE the fix ----------------------
        // FC_P and OB_Q both write: a global DB member, an instance-DB member through its iDB root,
        // and a bare PLC TAG. All three are real shared storage and none is block-local.
        WriteDb("DB_G.ir", new DbSource("0", "DB_G", 11, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Shared", "Bool", Retain: false, StartValue: null),
        }));

        WriteBlock("FC_P.ir", new IrBlock("0", "FC", "FC_P", 3, "LAD", null, new[]
        {
            new IrNetwork(1, "Writes shared storage", new[]
            {
                new CoilAssignment("DB_G.Shared", new Expr.TagRef("Enable")),
                new CoilAssignment("iDB_One.IO.Cmd", new Expr.TagRef("Enable")),
                new CoilAssignment("PlantWideTag", new Expr.TagRef("Enable")),
            }),
        }));

        WriteBlock("OB_Q.ir", new IrBlock("0", "OB", "OB_Q", 100, "LAD", null, new[]
        {
            new IrNetwork(1, "Also writes the same storage", new[]
            {
                new CoilAssignment("DB_G.Shared", new Expr.TagRef("Enable")),
                new CoilAssignment("iDB_One.IO.Cmd", new Expr.TagRef("Enable")),
                new CoilAssignment("PlantWideTag", new Expr.TagRef("Enable")),
            }),
        }));
    }

    // An FB that writes its own interface member twice (so it is a within-block multi-writer) and its
    // own temp once (so it is a within-block SOLE writer). Both roots are declared, which is what
    // makes them block-local — the IR states it, so the tool does not have to guess from the name.
    private static IrBlock Fb(string name, int number, string ioType) =>
        new("0", "FB", name, number, "LAD", null, new[]
        {
            new IrNetwork(1, "Step transitions", new[]
            {
                new CoilAssignment("IO.Step", new Expr.TagRef("Enable")),
                new CoilAssignment("IO.Step", new Expr.TagRef("Enable")),
            }),
            new IrNetwork(2, "Scratch", new[]
            {
                new CoilAssignment("Time", new Expr.TagRef("Enable")),
            }),
        },
            StaticMembers: new[]
            {
                new DbMember("IO", ioType, Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Step", "Int", Retain: false, StartValue: null),
                    new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                }),
            },
            TempMembers: new[] { new DbMember("Time", "Real", Retain: false, StartValue: null) });

    private void WriteDb(string file, DbSource db) =>
        File.WriteAllText(Path.Combine(_dir, file), DbIrSerializer.Serialize(db));

    private void WriteBlock(string file, IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        File.WriteAllText(Path.Combine(_dir, file), IrSerializer.SerializeBlock(block, sidecars));
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

    private static IEnumerable<MultiWriterFact> CrossBlock(CrossCheckReport report) =>
        report.MultiWriters.Where(m => m.Writers.Select(w => w.Block).Distinct(StringComparer.Ordinal).Count() > 1);

    // ---- The false finding, and it must be gone -------------------------------------------------

    [Fact]
    public void TwoFbsWithTheSameLeafName_AreNotOneCrossBlockMultiWriter()
    {
        var report = CrossCheckRunner.Run(_dir);

        // The defect: `IO.Step` reported once, with writers in BOTH FBs.
        Assert.DoesNotContain(report.MultiWriters, m => m.Path == "IO.Step");
        Assert.DoesNotContain(
            CrossBlock(report),
            m => m.Writers.Any(w => w.Block == "FB_One") && m.Writers.Any(w => w.Block == "FB_Two"));
    }

    [Fact]
    public void EachFbsOwnMemberIsReportedSeparately_QualifiedByItsOwner()
    {
        var report = CrossCheckRunner.Run(_dir);

        foreach (var fb in new[] { "FB_One", "FB_Two" })
        {
            var fact = Assert.Single(report.MultiWriters, m => m.Path == $"{fb}.IO.Step");
            Assert.Equal(fb, fact.Owner);
            Assert.All(fact.Writers, w => Assert.Equal(fb, w.Block));
        }
    }

    // *** THE UNDER-REPORTING HALF, AND IT IS THE MORE DANGEROUS ONE. *** Two FBs each writing their
    // own `Time` ONCE pooled into a single two-writer path, so the member read as multi-written —
    // i.e. NOT vulnerable to a deletion — when each was in fact its own FB's SOLE writer. A back-out
    // consulting soleWriters was told the safe thing about a member that was not safe.
    //
    // Constructed deliberately: `ir/test-project001` does NOT exercise this (every collided bare name
    // there has several writers per FB, so nothing moved between the tables), and a case the corpus
    // happens not to contain is exactly the case that gets designed for wrongly.
    [Fact]
    public void OneWriterInEachOfTwoFbs_IsTwoSoleWriters_NotOneMultiWriter()
    {
        var report = CrossCheckRunner.Run(_dir);

        Assert.DoesNotContain(report.MultiWriters, m => m.Path == "Time");

        foreach (var fb in new[] { "FB_One", "FB_Two" })
        {
            var sole = Assert.Single(report.SoleWriters, s => s.Path == $"{fb}.Time");
            Assert.Equal(fb, sole.Owner);
            Assert.Equal(fb, sole.Writer.Block);
        }
    }

    // ---- The negative half: a fix that silences everything must fail here ------------------------

    [Theory]
    [InlineData("DB_G.Shared")]              // global DB member
    [InlineData("iDB_One.IO.Cmd")]           // instance-DB member addressed through its iDB root
    [InlineData("PlantWideTag")]             // a BARE PLC tag — bareness is not what makes a path local
    public void GenuineCrossBlockMultiWriter_IsStillFound(string path)
    {
        var report = CrossCheckRunner.Run(_dir);

        var fact = Assert.Single(report.MultiWriters, m => m.Path == path);
        Assert.Null(fact.Owner); // global: no owning block, so the writers really are cross-block
        Assert.Equal(
            new[] { "FC_P", "OB_Q" },
            fact.Writers.Select(w => w.Block).Distinct(StringComparer.Ordinal).OrderBy(b => b, StringComparer.Ordinal));
    }

    // The bare PLC tag case deserves its own statement: the fix keys on WHETHER THE BLOCK DECLARES
    // THE ROOT, never on whether the path has a dot. A rule that qualified every rootless path would
    // pass every other test in this file and silently lose every genuine tag-level conflict.
    [Fact]
    public void ABarePlcTagIsNotTreatedAsBlockLocal()
    {
        var report = CrossCheckRunner.Run(_dir);

        Assert.Single(CrossBlock(report), m => m.Path == "PlantWideTag");
    }

    // ---- Instance aliases: reported, never pooled ------------------------------------------------

    [Fact]
    public void AnInterfaceMemberCarriesItsInstanceDbAlias()
    {
        var report = CrossCheckRunner.Run(_dir);

        var fact = Assert.Single(report.MultiWriters, m => m.Path == "FB_One.IO.Step");
        Assert.Equal(new[] { "iDB_One.IO.Step" }, fact.InstanceAliases);
    }

    // A temp is not in the instance DB, so it has no external form — read off the instance's OWN
    // declared members rather than assumed from the path, which is what stops a phantom alias.
    [Fact]
    public void ATempCarriesNoInstanceAlias()
    {
        var report = CrossCheckRunner.Run(_dir);

        var sole = Assert.Single(report.SoleWriters, s => s.Path == "FB_One.Time");
        Assert.Equal("FB_One", sole.Owner);
        Assert.Empty(CrossCheckRunner.Run(_dir).MultiWriters.Where(m => m.Path == "FB_One.Time"));
    }

    // An FB with no instance DB at all must not produce an alias either — `FB_Two` has none.
    [Fact]
    public void AnFbWithNoInstanceDbCarriesNoAlias()
    {
        var report = CrossCheckRunner.Run(_dir);

        var fact = Assert.Single(report.MultiWriters, m => m.Path == "FB_Two.IO.Step");
        Assert.Empty(fact.InstanceAliases);
    }

    // ---- The sibling analyses walk the same graph, so they were checked too ----------------------

    // deadMembers' interface half already restricted the bare form to the owning FB (it always had
    // the qualification this fix adds elsewhere), ioBoundary and siblingRefs both carry the block on
    // every row. Measured on the committed corpus: all three are byte-identical across the fix. This
    // asserts the property rather than the byte-identity, so it survives the corpus changing.
    [Fact]
    public void SiblingAnalysesAttributeEveryRowToABlock()
    {
        var report = CrossCheckRunner.Run(_dir);

        Assert.All(report.IoBoundary, io => Assert.False(string.IsNullOrWhiteSpace(io.Block)));
        Assert.All(report.SiblingRefs, s => Assert.False(string.IsNullOrWhiteSpace(s.Block)));

        // An interface-member dead fact is always FB-rooted, so two FBs' identically-named members
        // cannot collide there either.
        Assert.All(
            report.DeadMembers.Where(d => d.Scope == DeadMemberScope.InterfaceMember),
            d => Assert.Contains('.', d.Path));
    }
}
