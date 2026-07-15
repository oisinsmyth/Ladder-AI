using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Regression tests for two real bugs found 2026-07-15 building the Kestrel Shredder System's own
/// OB1 Main (the first time this project ever wrote *new* content into an OB, rather than just
/// round-tripping one that was always empty):
///
/// 1. WriteInterface used to emit an Output/InOut &lt;Section&gt; unconditionally for every block
///    kind, even when empty — harmless for FB/FC (which tolerate an empty Output/InOut section)
///    but rejected outright by real TIA Import() for an OB ("Section 'Output' is not valid for
///    this block"). Fixed by omitting Output/InOut entirely for OB-kind blocks.
/// 2. Return was gated `block.Kind != "FB"` — true for OB, so an OB still got a Return/Ret_Val
///    section, which real TIA Import() also rejects ("Section 'Return' is not valid for this
///    block"). Fixed by gating `block.Kind == "FC"` instead — Return is an FC-only section, not
///    merely an FB exclusion; FB and OB both omit it.
/// </summary>
public class BlockSourceWriterTests
{
    private static BlockSource MinimalBlock(string kind, IReadOnlyList<DbMember>? inputMembers = null) =>
        new(RootUId: "0", Kind: kind, Name: "Test", Number: 1, Language: "LAD", Comment: null,
            CompileUnits: Array.Empty<CompileUnitSource>(), InputMembers: inputMembers);

    [Fact]
    public void Write_ObBlock_OmitsOutputAndInOutSections()
    {
        var block = MinimalBlock("OB", new[] { new DbMember("Initial_Call", "Bool", false, null, IsBareParameter: true) });

        var doc = BlockSourceWriter.Write(block, Array.Empty<FlgNetwork>(), Array.Empty<string>(), Array.Empty<string?>(), Array.Empty<string?>());

        var sections = doc.Descendants().Where(e => e.Name.LocalName == "Section").Select(e => e.Attribute("Name")?.Value).ToList();
        Assert.Contains("Input", sections);
        Assert.DoesNotContain("Output", sections);
        Assert.DoesNotContain("InOut", sections);
    }

    [Fact]
    public void Write_ObBlock_OmitsReturnSection()
    {
        var block = MinimalBlock("OB", new[] { new DbMember("Initial_Call", "Bool", false, null, IsBareParameter: true) });

        var doc = BlockSourceWriter.Write(block, Array.Empty<FlgNetwork>(), Array.Empty<string>(), Array.Empty<string?>(), Array.Empty<string?>());

        var sections = doc.Descendants().Where(e => e.Name.LocalName == "Section").Select(e => e.Attribute("Name")?.Value).ToList();
        Assert.DoesNotContain("Return", sections);
    }

    [Fact]
    public void Write_FbBlock_OmitsReturnSection()
    {
        var block = MinimalBlock("FB", new[] { new DbMember("In1", "Bool", false, null) });

        var doc = BlockSourceWriter.Write(block, Array.Empty<FlgNetwork>(), Array.Empty<string>(), Array.Empty<string?>(), Array.Empty<string?>());

        var sections = doc.Descendants().Where(e => e.Name.LocalName == "Section").Select(e => e.Attribute("Name")?.Value).ToList();
        Assert.DoesNotContain("Return", sections);
    }

    [Fact]
    public void Write_FcBlock_StillEmitsOutputAndInOutSections()
    {
        var block = MinimalBlock("FC");

        var doc = BlockSourceWriter.Write(block, Array.Empty<FlgNetwork>(), Array.Empty<string>(), Array.Empty<string?>(), Array.Empty<string?>());

        var sections = doc.Descendants().Where(e => e.Name.LocalName == "Section").Select(e => e.Attribute("Name")?.Value).ToList();
        Assert.Contains("Output", sections);
        Assert.Contains("InOut", sections);
    }

    [Fact]
    public void Write_FcBlock_StillEmitsReturnSection()
    {
        var block = MinimalBlock("FC");

        var doc = BlockSourceWriter.Write(block, Array.Empty<FlgNetwork>(), Array.Empty<string>(), Array.Empty<string?>(), Array.Empty<string?>());

        var sections = doc.Descendants().Where(e => e.Name.LocalName == "Section").Select(e => e.Attribute("Name")?.Value).ToList();
        Assert.Contains("Return", sections);
    }
}
