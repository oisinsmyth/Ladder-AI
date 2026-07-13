using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

// PathsMatch is OpennessGateway's own path-comparison helper for FindAlreadyOpenProject — pure
// string/path logic, no COM dependency, unlike the rest of that class (which requires a live
// Portal session and is verified live only, per this project's established discipline). Added
// 2026-07-14 during the concurrent-Portal stability audit
// (docs/notes/concurrent-portal-test-plan.md T5.1) specifically because this exact code was where
// a real bug was found and fixed the same day (a forward-slash vs. backslash mismatch spuriously
// missing an already-open project) with zero test coverage at the time.
public class PathsMatchTests
{
    [Fact]
    public void PathsMatch_IdenticalPaths_ReturnsTrue()
    {
        Assert.True(OpennessGateway.PathsMatch(@"C:\foo\bar.ap20", @"C:\foo\bar.ap20"));
    }

    [Fact]
    public void PathsMatch_SameFileDifferentSlashDirection_ReturnsTrue()
    {
        // The exact real bug, 2026-07-14: a Bash `pwd -W`-style forward-slash identifier failed to
        // match Project.Path.FullName's own native backslash format.
        Assert.True(OpennessGateway.PathsMatch("C:/foo/bar.ap20", @"C:\foo\bar.ap20"));
    }

    [Fact]
    public void PathsMatch_SameFileDifferentCase_ReturnsTrue()
    {
        // Windows paths are case-insensitive; PathsMatch uses OrdinalIgnoreCase deliberately.
        Assert.True(OpennessGateway.PathsMatch(@"C:\Foo\Bar.ap20", @"c:\foo\bar.ap20"));
    }

    [Fact]
    public void PathsMatch_DifferentFiles_ReturnsFalse()
    {
        Assert.False(OpennessGateway.PathsMatch(@"C:\foo\bar.ap20", @"C:\foo\baz.ap20"));
    }

    [Fact]
    public void PathsMatch_RelativePathResolvedAgainstCurrentDirectory_MatchesEquivalentAbsolutePath()
    {
        var absolute = Path.Combine(Environment.CurrentDirectory, "SampleProject.ap20");
        Assert.True(OpennessGateway.PathsMatch("SampleProject.ap20", absolute));
    }

    [Fact]
    public void PathsMatch_MalformedIdentifier_ReturnsFalseRatherThanThrowing()
    {
        // A bare project Name (already checked separately by FindAlreadyOpenProject's own
        // Name-based comparison before it ever calls PathsMatch) can contain characters that
        // aren't valid in a path at all — this must report "not a match", never throw, since a
        // non-path identifier isn't an error, just not the kind of identifier PathsMatch handles.
        Assert.False(OpennessGateway.PathsMatch("SomeProject\0WithANulChar", @"C:\foo\bar.ap20"));
    }
}
