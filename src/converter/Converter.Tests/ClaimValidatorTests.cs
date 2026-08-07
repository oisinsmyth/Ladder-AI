using Converter.Claims;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-50 component 1 — the corpus half. A claim is only meaningful if the thing reserved is actually
/// free, so every kind gets both a grant and a refusal against a real indexed project. The alarm-bit
/// cases reproduce this project's own recorded near-miss: the telemetry line "ShredderAlarm0.%X9 &lt;-
/// HopperBlockedAlarm, X9 verified free" is exactly the check two agents can both pass at once.
/// </summary>
public class ClaimValidatorTests : IDisposable
{
    private readonly string _projectDir = ClaimsTestCorpus.Create();
    private readonly ClaimCorpus _corpus;

    public ClaimValidatorTests() => _corpus = ClaimCorpus.Build(_projectDir);

    private ClaimOutcome? Reject(ClaimKind kind, string value) => ClaimValidator.Reject(_corpus, kind, value);

    [Fact]
    public void BlockNumber_FreeNumberIsGranted() => Assert.Null(Reject(ClaimKind.BlockNumber, "FB77"));

    [Fact]
    public void BlockNumber_UsedNumberIsRefusedAndNamesTheOwner()
    {
        var outcome = Reject(ClaimKind.BlockNumber, "FB50");

        Assert.Equal(ClaimResult.AlreadyUsedInCorpus, outcome!.Result);
        Assert.Contains("FB_Existing", outcome.Reason);
    }

    [Fact]
    public void BlockNumber_NumberSpacesArePerKind() =>
        // FC3 exists; FB3 does not. A shared number space would refuse this and force a pointless gap.
        Assert.Null(Reject(ClaimKind.BlockNumber, "FB3"));

    [Fact]
    public void BlockNumber_MalformedValueIsInvalid() =>
        Assert.Equal(ClaimResult.Invalid, Reject(ClaimKind.BlockNumber, "FB")!.Result);

    [Fact]
    public void AlarmBit_DrivenBitIsRefusedAndNamesTheWriter()
    {
        var outcome = Reject(ClaimKind.AlarmBit, ClaimsTestCorpus.AlarmWord + ".%X0");

        Assert.Equal(ClaimResult.AlreadyUsedInCorpus, outcome!.Result);
        Assert.Contains(ClaimsTestCorpus.SharedBlock, outcome.Reason);
    }

    [Fact]
    public void AlarmBit_UndrivenBitIsGranted() =>
        Assert.Null(Reject(ClaimKind.AlarmBit, ClaimsTestCorpus.AlarmWord + ".%X9"));

    [Fact]
    public void AlarmBit_OutOfRangeForTheWordsTypeIsInvalid()
    {
        // The word is a Word: 16 bits, so %X16 does not exist. Reserving a bit that cannot exist is a
        // silent nonsense a later import would reject far from the decision.
        var outcome = Reject(ClaimKind.AlarmBit, ClaimsTestCorpus.AlarmWord + ".%X16");

        Assert.Equal(ClaimResult.Invalid, outcome!.Result);
        Assert.Contains("out of range", outcome.Reason);
    }

    [Fact]
    public void AlarmBit_UnknownWordIsInvalid() =>
        Assert.Equal(ClaimResult.Invalid, Reject(ClaimKind.AlarmBit, "DB_Alarms.NoSuchWord.%X1")!.Result);

    [Fact]
    public void AlarmBit_NonSlicePathIsInvalid() =>
        Assert.Equal(ClaimResult.Invalid, Reject(ClaimKind.AlarmBit, ClaimsTestCorpus.AlarmWord)!.Result);

    [Fact]
    public void DbMember_NewMemberIsGranted() => Assert.Null(Reject(ClaimKind.DbMember, "DB_Settings.Fresh"));

    [Fact]
    public void DbMember_ExistingMemberIsRefused() =>
        Assert.Equal(ClaimResult.AlreadyUsedInCorpus, Reject(ClaimKind.DbMember, "DB_Settings.Existing")!.Result);

    [Fact]
    public void DbMember_UnknownRootIsInvalid() =>
        Assert.Equal(ClaimResult.Invalid, Reject(ClaimKind.DbMember, "DB_Nope.Member")!.Result);

    [Fact]
    public void BlockNetwork_FreeSlotIsGranted() =>
        Assert.Null(Reject(ClaimKind.BlockNetwork, ClaimsTestCorpus.SharedBlock + ":8"));

    [Fact]
    public void BlockNetwork_ExistingNetworkIsRefused() =>
        Assert.Equal(ClaimResult.AlreadyUsedInCorpus, Reject(ClaimKind.BlockNetwork, ClaimsTestCorpus.SharedBlock + ":7")!.Result);

    [Fact]
    public void BlockNetwork_UnknownBlockIsInvalid() =>
        Assert.Equal(ClaimResult.Invalid, Reject(ClaimKind.BlockNetwork, "FC_Nope:1")!.Result);

    [Fact]
    public void BlockEdit_ExistingBlockIsGranted() =>
        Assert.Null(Reject(ClaimKind.BlockEdit, ClaimsTestCorpus.SharedBlock));

    [Fact]
    public void BlockEdit_AbsentBlockIsNotInCorpus() =>
        // The one kind whose precondition is EXISTENCE rather than absence — the semantics split the
        // model names explicitly, and getting it backwards would make every exclusive claim refuse.
        Assert.Equal(ClaimResult.NotInCorpus, Reject(ClaimKind.BlockEdit, "FC_Nope")!.Result);

    [Fact]
    public void Tag_ExistingTagIsRefused()
    {
        // A DB root resolves as a tag root, so this exercises the "nothing to reserve" path.
        var outcome = Reject(ClaimKind.Tag, "DB_Settings");

        Assert.Equal(ClaimResult.AlreadyUsedInCorpus, outcome!.Result);
    }

    [Fact]
    public void Tag_NewNameIsGranted() => Assert.Null(Reject(ClaimKind.Tag, "BrandNewTag"));

    [Fact]
    public void EmptyCorpus_IsNothingExaminedRatherThanFree()
    {
        var empty = Path.Combine(Path.GetTempPath(), $"claims-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);

        try
        {
            var outcome = ClaimValidator.Reject(ClaimCorpus.Build(empty), ClaimKind.BlockNumber, "FB1");

            // FI-44. Against an empty corpus every resource looks free, which is not an answer — and a
            // mistyped --project is the likeliest way to get here.
            Assert.Equal(ClaimResult.NothingExamined, outcome!.Result);
        }
        finally
        {
            ClaimsTestCorpus.Delete(empty);
        }
    }

    public void Dispose() => ClaimsTestCorpus.Delete(_projectDir);
}
