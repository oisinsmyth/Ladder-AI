using System.IO;
using System.Linq;
using OpennessCli.Cli;
using Xunit;

namespace OpennessCli.Tests;

// FI-68. Openness rejects a RELATIVE path and does it with an exception naming something else
// entirely: `export --out <relative>` failed with EngineeringTargetInvocationException, which reads
// exactly like the "Inconsistent blocks and PLC data types (UDT) cannot be exported" refusal — a real
// and frequent condition on a live job. The agent that hit it only avoided a long detour because it
// happened to run sanity-check between attempts and got HEALTHY both times.
//
// The constraint was already recorded for the project-open path in docs/notes/openness-quirks.md and
// nobody had connected it to the export target. So these tests cover the CLASS, not the one argument:
// every path that reaches Openness as a file target is resolved at the boundary.
public class PathArgumentTests
{
    private static string Rooted(params string[] parts) => Path.GetFullPath(Path.Combine(parts));

    [Fact]
    public void Export_ResolvesARelativeOutPath()
    {
        var result = ArgumentParser.Parse(new[] { "export", "P", "--block", "FB_X", "--out", @"work\FB_X.xml" });

        var options = Assert.IsType<ParseResult.ExportSuccess>(result).Options;
        Assert.True(Path.IsPathRooted(options.OutPath));
        Assert.Equal(Rooted("work", "FB_X.xml"), options.OutPath);
    }

    [Fact]
    public void ExportAll_ResolvesARelativeOutDir()
    {
        var result = ArgumentParser.Parse(new[] { "export-all", "P", "--out", "dump" });

        var options = Assert.IsType<ParseResult.ExportAllSuccess>(result).Options;
        Assert.True(Path.IsPathRooted(options.OutDir));
        Assert.Equal(Rooted("dump"), options.OutDir);
    }

    // The same class, and the reason this is one fix rather than three: an import file list reaches
    // Openness the same way an export target does.
    [Fact]
    public void Import_ResolvesEveryRelativeFileInTheList()
    {
        var result = ArgumentParser.Parse(new[] { "import", "P", "--group", "PLC_1/Program blocks", @"a\one.xml", @"b\two.xml" });

        var options = Assert.IsType<ParseResult.ImportSuccess>(result).Options;
        Assert.All(options.Files, f => Assert.True(Path.IsPathRooted(f)));
        Assert.Equal(Rooted("a", "one.xml"), options.Files.First());
    }

    [Fact]
    public void TiaInstall_IsResolvedForEveryCommand_NotJustTheOneThatWasHit()
    {
        // Read through CommonOptions, which is where every command's copy is consumed — so a command
        // added later inherits the fix instead of needing its own.
        foreach (var args in new[]
                 {
                     new[] { "list", "P", "--tia-install", "tools" },
                     new[] { "compile", "P", "--tia-install", "tools" },
                     new[] { "sanity-check", "P", "--tia-install", "tools" },
                 })
        {
            var common = ArgumentParser.CommonOptions(ArgumentParser.Parse(args));
            Assert.Equal(Rooted("tools"), common.TiaInstallOverride);
        }
    }

    [Fact]
    public void AnAbsolutePathIsLeftMeaningTheSameThing()
    {
        var absolute = Rooted("dump", "FB_X.xml");
        var result = ArgumentParser.Parse(new[] { "export", "P", "--block", "FB_X", "--out", absolute });

        Assert.Equal(absolute, Assert.IsType<ParseResult.ExportSuccess>(result).Options.OutPath);
    }

    // The project identifier is documented as "either a full .apNN path or a BARE PROJECT NAME", and
    // that is how an already-open Portal session is matched. Resolving a bare name against the current
    // directory would silently turn a name into a path that does not exist, so it is excluded on
    // purpose — this test exists so the exclusion is a decision on the record rather than a gap.
    [Fact]
    public void TheProjectIdentifierIsLeftExactlyAsTyped()
    {
        var result = ArgumentParser.Parse(new[] { "list", "MyProject" });

        Assert.Equal("MyProject", ArgumentParser.ProjectIdentifier(result));
    }

    [Fact]
    public void ToAbsolute_ReturnsUnresolvableInputUntouchedRatherThanThrowing()
    {
        // Refusing to resolve is deliberate: this helper exists to REMOVE a confusing failure, so
        // swallowing an invalid path into a different exception here would replace one misleading
        // message with another. Validation belongs downstream, where the message can name the argument.
        Assert.Equal(string.Empty, PathArguments.ToAbsolute(string.Empty));
        Assert.Equal("   ", PathArguments.ToAbsolute("   "));
        Assert.Null(PathArguments.ToAbsoluteOrNull(null));
    }
}
