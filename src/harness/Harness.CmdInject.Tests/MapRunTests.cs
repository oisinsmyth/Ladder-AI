using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>The <c>map</c> verb end to end, over real files — it resolves and prints, or refuses and prints why.</summary>
public class MapRunTests
{
    [Fact]
    public void AValidMapAndBinding_ResolveAndPrintTheWholeThing()
    {
        using var files = new FixtureFiles();
        var output = new StringWriter();

        var exit = MapRun.Execute(files.TagsPath, files.AreaPath, files.BindingPath, output);

        Assert.Equal(CmdInjectExit.Ok, exit);
        var text = output.ToString();
        Assert.Contains("RESOLVED", text, StringComparison.Ordinal);
        Assert.Contains(Fixtures.ChannelName, text, StringComparison.Ordinal);
        Assert.Contains("written ALONE, second", text, StringComparison.Ordinal);
        Assert.Contains("contacts nothing", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ABindingThatDoesNotResolve_IsRefusedAndPrintsWhy()
    {
        using var files = new FixtureFiles(FixtureFiles.DefaultBindingJson().Replace(Fixtures.AckCountTag, "NOPE_Missing"));
        var output = new StringWriter();

        var exit = MapRun.Execute(files.TagsPath, files.AreaPath, files.BindingPath, output);

        Assert.Equal(CmdInjectExit.MapRefused, exit);
        Assert.Contains("NOT RESOLVED", output.ToString(), StringComparison.Ordinal);
    }
}
