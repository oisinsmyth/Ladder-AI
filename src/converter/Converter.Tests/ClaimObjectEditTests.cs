using Converter.Claims;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-14. *** `converter claim --kind block-edit` COULD NOT NAME A DB OR A UDT, SO THE ENTIRE
/// DB AND UDT SURFACE OF EVERY PROJECT WAS UNRESERVABLE. *** Two agents editing one instance DB, or
/// one shared UDT, collided with both `diff --only` invariance checks passing and nothing to refuse
/// them — verbatim the collision class the registry exists for, in the resource class the artificial
/// corpus is made almost entirely of.
///
/// <para><b>Cause, established before anything changed:</b> ONE LOOKUP.
/// <see cref="Preflight.ProjectIndex"/> already indexed DBs and types in their own sets;
/// <c>RejectBlockEdit</c> asked only <c>ResolvesAsBlock</c>.</para>
///
/// <para>*** BOTH DIRECTIONS, AND THE POSITIVE CONTROL RUNS IN THE SAME PLACE AS THE FIX. *** A fence
/// that accepts everything passes every test that only checks acceptances — the mirror of the trap
/// that has caught this lane twice — so a genuinely absent object must still be refused BY NAME.</para>
/// </summary>
public class ClaimObjectEditTests : IDisposable
{
    private readonly string _dir;
    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();

    public ClaimObjectEditTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"claim-objedit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // A UDT, an FB declaring a member of it, that FB's instance DB, a global DB, and an unrelated
        // FB — the five shapes an edit claim has to tell apart.
        Write("UDT_Io.ir", TypeIrSerializer.Serialize(new PlcTypeSource("0", "UDT_Io", null, new[]
        {
            new DbMember("Step", "Int", Retain: false, StartValue: null),
        })));

        Write("FB_Uses.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Uses", 50, "LAD", "declares a member of UDT_Io",
            new[] { new IrNetwork(1, "Only", Array.Empty<CoilAssignment>()) },
            StaticMembers: new[]
            {
                new DbMember("IO", "\"UDT_Io\"", Retain: true, StartValue: null),
            })));

        Write("iDB_Uses.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "iDB_Uses", 30, InstanceOfName: "FB_Uses", Comment: null, Members: new[]
            {
                new DbMember("IO", "\"UDT_Io\"", Retain: true, StartValue: null),
            })));

        Write("DB_Settings.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Settings", 31, InstanceOfName: null, Comment: null, Members: new[]
            {
                new DbMember("Existing", "Bool", Retain: false, StartValue: null),
            })));

        Write("FB_Unrelated.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Unrelated", 51, "LAD", "touches none of the above",
            new[] { new IrNetwork(1, "Only", Array.Empty<CoilAssignment>()) })));

        Write("Tags.ir", TagTableIrSerializer.Serialize(new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "PlantTag", "Bool", "%M0.0", true, true, true, null),
        })));
    }

    private void Write(string file, string content) => File.WriteAllText(Path.Combine(_dir, file), content);

    public void Dispose() => ClaimsTestCorpus.Delete(_dir, _claimsRoot);

    private ClaimOutcome Edit(string value, string agent = "a1") =>
        ClaimsRunner.Acquire(ClaimCorpus.Build(_dir), new ClaimStore(_claimsRoot, _dir), _dir,
            ClaimKind.BlockEdit, value, agent, $"{agent} edits {value}");

    // ---- The fix: DBs and UDTs are nameable ------------------------------------------------------

    [Theory]
    [InlineData("iDB_Uses")]      // an instance DB — what every FB slot touches
    [InlineData("DB_Settings")]   // a global DB
    [InlineData("UDT_Io")]        // a UDT — the widest blast radius in the project
    public void ADbOrUdt_CanNowBeClaimedForEditing(string value)
    {
        var outcome = Edit(value);

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal(value, outcome.Claim!.Value);
    }

    // ---- The positive control, in the same run as the fix ----------------------------------------

    [Theory]
    [InlineData("FB_Uses")]
    [InlineData("FB_Unrelated")]
    public void TheBlockClaimsThatAlreadyWorked_StillWork(string value)
    {
        Assert.True(Edit(value).Ok);
    }

    // ---- 🔴 THE DID-NOT-RUN HALF: an absent object must still be refused, BY NAME -----------------

    // *** A FENCE THAT ACCEPTS EVERYTHING PASSES EVERY TEST THAT ONLY CHECKS ACCEPTANCES. ***
    [Fact]
    public void AGenuinelyAbsentObject_IsStillRefused_NamingIt()
    {
        var outcome = Edit("FB_DoesNotExistAnywhere");

        Assert.False(outcome.Ok);
        Assert.Equal(ClaimResult.NotInCorpus, outcome.Result);
        Assert.Contains("FB_DoesNotExistAnywhere", outcome.Reason);
        Assert.Contains("block, a DB or a PLC data type", outcome.Reason);
    }

    // A TAG is addressable but is not an editable object. Deliberately still refused: widening to
    // `ResolvesAsTagRoot` would have swept tags in as a side effect of adding DBs, since that helper
    // is tags ∪ DBs.
    [Fact]
    public void ATagIsNotAnEditableObject_AndIsStillRefused()
    {
        var outcome = Edit("PlantTag");

        Assert.False(outcome.Ok);
        Assert.Equal(ClaimResult.NotInCorpus, outcome.Result);
    }

    // ---- The cross-object conflicts the filesystem cannot see ------------------------------------

    // *** THE ONE THAT WILL BITE UNDER CONTENTION. *** A holds the UDT, B holds a block declaring a
    // member of it. Two different names, two different files, both acquisitions legitimate.
    [Fact]
    public void AUdtEditAgainstAnEditOfAnObjectDeclaringIt_IsAConflict()
    {
        Assert.True(Edit("UDT_Io", "A").Ok);
        Assert.True(Edit("FB_Uses", "B").Ok); // both granted — that is the point

        var report = ClaimsRunner.Check(ClaimCorpus.Build(_dir), new ClaimStore(_claimsRoot, _dir), _dir, DateTime.UtcNow);

        var conflict = Assert.Single(report.Conflicts, c => c.Detail.Contains("DECLARES a member of type"));
        Assert.Contains("UDT_Io", conflict.Detail);
        Assert.Contains("FB_Uses", conflict.Detail);
    }

    // An instance DB moves with its FB's interface.
    [Fact]
    public void AnInstanceDbEditAgainstItsFbsEdit_IsAConflict()
    {
        Assert.True(Edit("FB_Uses", "A").Ok);
        Assert.True(Edit("iDB_Uses", "B").Ok);

        var report = ClaimsRunner.Check(ClaimCorpus.Build(_dir), new ClaimStore(_claimsRoot, _dir), _dir, DateTime.UtcNow);

        Assert.Single(report.Conflicts, c => c.Detail.Contains("the FB it instantiates"));
    }

    // *** THE CONVERSE, WITHOUT WHICH THE ABOVE PROVES NOTHING. *** One agent editing a UDT and its
    // instantiating block is doing ONE coordinated change; refusing that would make the ordinary case
    // unworkable and the check would be switched off within a week.
    [Fact]
    public void OneAgentHoldingBothIsNotAConflict()
    {
        Assert.True(Edit("UDT_Io", "A").Ok);
        Assert.True(Edit("FB_Uses", "A").Ok);
        Assert.True(Edit("iDB_Uses", "A").Ok);

        var report = ClaimsRunner.Check(ClaimCorpus.Build(_dir), new ClaimStore(_claimsRoot, _dir), _dir, DateTime.UtcNow);

        Assert.Empty(report.Conflicts);
    }

    // And two UNRELATED edits by two agents are not a conflict — the relation has to be real.
    [Fact]
    public void TwoUnrelatedObjectsHeldByTwoAgents_AreNotAConflict()
    {
        Assert.True(Edit("DB_Settings", "A").Ok);
        Assert.True(Edit("FB_Unrelated", "B").Ok);

        var report = ClaimsRunner.Check(ClaimCorpus.Build(_dir), new ClaimStore(_claimsRoot, _dir), _dir, DateTime.UtcNow);

        Assert.Empty(report.Conflicts);
    }

    // The pre-existing cross-kind case must survive the addition.
    [Fact]
    public void TheBlockEditVersusBlockNetworkConflict_StillFires()
    {
        var corpus = ClaimCorpus.Build(_dir);
        var store = new ClaimStore(_claimsRoot, _dir);
        Assert.True(ClaimsRunner.Acquire(corpus, store, _dir, ClaimKind.BlockEdit, "FB_Uses", "A", "edit").Ok);
        Assert.True(ClaimsRunner.Acquire(corpus, store, _dir, ClaimKind.BlockNetwork, "FB_Uses:8", "B", "append").Ok);

        var report = ClaimsRunner.Check(corpus, store, _dir, DateTime.UtcNow);

        Assert.Single(report.Conflicts, c => c.Detail.Contains("exclusive block-edit claim"));
    }

    // ---- db-member is a different question, checked rather than assumed from its name -------------

    // It is ALLOCATION semantics and refuses a member that EXISTS, so it reserves the ADDITION of a
    // new member. It does not and cannot cover editing an existing DB.
    [Fact]
    public void DbMemberIsAboutAddingAMember_NotEditingADb()
    {
        Assert.Equal(ClaimSemantics.Allocation, ClaimKinds.SemanticsOf(ClaimKind.DbMember));
        Assert.Equal(ClaimSemantics.Exclusive, ClaimKinds.SemanticsOf(ClaimKind.BlockEdit));

        var existing = ClaimsRunner.Acquire(ClaimCorpus.Build(_dir), new ClaimStore(_claimsRoot, _dir), _dir,
            ClaimKind.DbMember, "DB_Settings.Existing", "a1", "try to reserve an existing member");

        Assert.False(existing.Ok);
        Assert.Equal(ClaimResult.AlreadyUsedInCorpus, existing.Result);
    }
}
