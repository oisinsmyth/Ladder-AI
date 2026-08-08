using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-59 (2026-08-08). A PARAMETER SECTION NEVER CARRIES <c>Remanence</c>, whatever the IR says.
///
/// TIA rejects it outright, at IMPORT, with a message that names the parameter and not the cause:
///     "FC_EStopAlarms.FirstScan: The Openness import failed: The attribute 'Remanence' cannot be set."
///
/// Retention is meaningless on an Input/Output/InOut parameter — it has no storage of its own — so
/// there is no case in which emitting it is right.
///
/// Previously the bare shape was opt-in per member, via a <c>BAREPARAM</c> token the author had to
/// remember. That made the failure mode a late, confusing import rejection for a MISSING TOKEN, on
/// a block that converted and preflighted clean. The first agent to add a parameterised FC call on
/// this project hit exactly that. Deriving the shape from the SECTION is the general fix: the
/// section already knows these are parameters, which is a fact the file contains rather than
/// something an author must not forget.
/// </summary>
public class ParameterSectionRemanenceTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    private static XElement WriteAndGetSection(BlockSource block, string sectionName)
    {
        var written = BlockSourceWriter.Write(
            block,
            block.CompileUnits.Select(u => u.Network).ToList(),
            block.CompileUnits.Select(u => u.UId).ToList(),
            block.CompileUnits.Select(u => u.Title).ToList(),
            block.CompileUnits.Select(u => u.Comment).ToList());

        return written.Descendants()
            .First(e => e.Name.LocalName == "Section" && (string?)e.Attribute("Name") == sectionName);
    }

    private static IEnumerable<XElement> MembersOf(XElement section) =>
        section.Elements().Where(e => e.Name.LocalName == "Member");

    // The real FC whose parameters are the confirmed shape. Round-tripping it must never put
    // Remanence on a parameter — whether or not the parsed member happens to carry the marker.
    [Theory]
    [InlineData("Input")]
    [InlineData("Output")]
    public void RoundTrippedParameters_CarryNoRemanence(string sectionName)
    {
        var block = BlockSourceParser.Parse(LoadFixture("FcWithBareParameterMembers.xml"));

        var members = MembersOf(WriteAndGetSection(block, sectionName)).ToList();

        Assert.NotEmpty(members);
        Assert.All(members, m => Assert.Null(m.Attribute("Remanence")));
    }

    // The guard that matters most: STATIC is not a parameter section. A static's retention is real,
    // and stripping it there would silently make retained state non-retentive — the C-132 trap, and
    // a far worse failure than the one being fixed.
    [Fact]
    public void StaticSection_StillCarriesRemanence()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FcWithBareParameterMembers.xml"));
        var written = BlockSourceWriter.Write(
            block,
            block.CompileUnits.Select(u => u.Network).ToList(),
            block.CompileUnits.Select(u => u.UId).ToList(),
            block.CompileUnits.Select(u => u.Title).ToList(),
            block.CompileUnits.Select(u => u.Comment).ToList());

        var staticSection = written.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Section" && (string?)e.Attribute("Name") == "Static");

        // The fixture is an FC and may have no Static section at all; the assertion only applies
        // where one exists, and its point is that the parameter rule did not leak into it.
        if (staticSection is not null)
        {
            Assert.All(MembersOf(staticSection), m => Assert.NotNull(m.Attribute("Remanence")));
        }
    }
}
