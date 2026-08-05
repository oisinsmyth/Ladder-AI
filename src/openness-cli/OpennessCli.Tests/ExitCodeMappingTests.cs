using System;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// The exception -> exit-code classification (added 2026-08-05, audit F-09).
///
/// Before this, every `--group` resolution failure inside <c>OpennessGateway</c> threw a bare
/// <see cref="InvalidOperationException"/>, so <c>Program</c>'s catch-all reported the single
/// commonest user mistake this tool has — a mis-typed <c>--group</c>, which must match
/// <c>openness-cli list</c>'s own Path column verbatim, article number and all — as exit 5
/// (UnexpectedError): an internal fault. Four sibling domain errors (type / tag-table not-found and
/// ambiguous) were in no catch clause either, and reported the same way.
///
/// The classification is a pure function specifically so it can be checked here, with no Portal
/// session, no project, and no COM. That was the reason the defect survived: the only way to
/// observe it before was to run the real CLI against a real project and read the exit code.
/// </summary>
public class ExitCodeMappingTests
{
    public static IEnumerable<object[]> CommandErrors() => new[]
    {
        new object[] { GroupNotFoundException.EmptyPath() },
        new object[] { GroupNotFoundException.NoDeviceItem("PLC1/Program blocks/Control") },
        new object[] { GroupNotFoundException.GroupMissing("Block", "Control", "PLC1") },
        new object[] { new NotAPlcSoftwareContainerException("HMI_1") },
        new object[] { new BlockNotFoundException("PlantAutoControl") },
        new object[] { new AmbiguousBlockException("PlantAutoControl", new[] { "a", "b" }) },
        new object[] { new TypeNotFoundException("UDT_MotorIO") },
        new object[] { new AmbiguousTypeException("UDT_MotorIO", new[] { "a", "b" }) },
        new object[] { new TagTableNotFoundException("Default tag table") },
        new object[] { new AmbiguousTagTableException("Default tag table", new[] { "a", "b" }) },
        new object[] { new DeviceNotFoundException("station_2") },
        new object[] { new ExportProducedNoFileException(@"C:\out.xml") },
    };

    // Every one of these is fixable by re-running with a different argument, so it is the user's
    // error (7), not the tool's (5).
    [Theory]
    [MemberData(nameof(CommandErrors))]
    public void DomainError_MapsToCommandError(Exception ex)
    {
        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
    }

    [Fact]
    public void ConnectTimeout_KeepsItsOwnExitCode()
    {
        Assert.Equal(ExitCodes.ConnectTimeout, ExitCodes.ForException(new ConnectTimeoutException(TimeSpan.FromMinutes(2))));
    }

    [Fact]
    public void ProjectOpenTimeout_KeepsItsOwnExitCode()
    {
        Assert.Equal(ExitCodes.ProjectOpenTimeout, ExitCodes.ForException(new ProjectOpenTimeoutException(TimeSpan.FromMinutes(5))));
    }

    [Fact]
    public void SafetyRefusal_KeepsItsOwnExitCode()
    {
        Assert.Equal(ExitCodes.SafetyRefused, ExitCodes.ForException(new SafetyContentRefusedException("F_Block", "F_LAD")));
    }

    // The half of the fix that is easy to lose: only exceptions that genuinely describe a user's
    // mistake were reclassified. A bare InvalidOperationException still means "this tool hit a state
    // it did not expect" — 15 precondition guards and seven Openness-API invariant checks still throw
    // one, and quietly relabelling those as user error would hide real bugs behind a tidy message.
    [Fact]
    public void UnrecognizedException_StaysUnexpectedError()
    {
        Assert.Equal(ExitCodes.UnexpectedError, ExitCodes.ForException(new InvalidOperationException("Connect must be called before OpenProject.")));
        Assert.Equal(ExitCodes.UnexpectedError, ExitCodes.ForException(new NullReferenceException()));
    }

    // The message is the other half of the fix: a user who mis-typed --group needs to be told what to
    // copy it from, not handed a stack-shaped internal error string.
    [Fact]
    public void GroupNotFound_MessageNamesThePathAndPointsAtTheListCommand()
    {
        var ex = GroupNotFoundException.NoDeviceItem("PLC1/Program blocks/Control");

        Assert.Contains("PLC1/Program blocks/Control", ex.Message, StringComparison.Ordinal);
        Assert.Contains("openness-cli list", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotAPlcSoftwareContainer_MessageNamesTheOffendingPath()
    {
        Assert.Contains("S7-1200 G2 station_2/HMI_1", new NotAPlcSoftwareContainerException("S7-1200 G2 station_2/HMI_1").Message, StringComparison.Ordinal);
    }
}
