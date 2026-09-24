using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

// to-ir of an export holding a VARIABLE array subscript used to drop the nested index's scope when it
// built the sidecar, so the sidecar round trip it had just produced was refused by to-xml ("nothing
// resolved that index's scope"): a block read INTO the IR that could not be written back out — the
// split ADR-0010 forbids. Found 2026-09-24 on a live run, where it pushed an edited block onto the
// sidecar-less synthesis path (and so into the rung-order bug StatementOrderTests covers).
public class VariableSubscriptSidecarTests
{
    private const string Xml = """
        <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
          <Parts>
            <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="DemoIn" /><Component Name="Gate" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="22">
              <Symbol>
                <Component Name="DemoData" />
                <Component Name="Table" AccessModifier="Array">
                  <Access Scope="GlobalVariable"><Symbol><Component Name="DemoIn" /><Component Name="Slot" /></Symbol></Access>
                </Component>
              </Symbol>
            </Access>
            <Part Name="Contact" UId="31" />
            <Part Name="Coil" UId="32" />
          </Parts>
          <Wires>
            <Wire UId="41"><Powerrail /><NameCon UId="31" Name="in" /></Wire>
            <Wire UId="42"><IdentCon UId="21" /><NameCon UId="31" Name="operand" /></Wire>
            <Wire UId="43"><NameCon UId="31" Name="out" /><NameCon UId="32" Name="in" /></Wire>
            <Wire UId="44"><IdentCon UId="22" /><NameCon UId="32" Name="operand" /></Wire>
          </Wires>
        </FlgNet>
        """;

    [Fact]
    public void ToIrThenToXml_KeepsTheNestedIndexScope()
    {
        var reduced = GraphReducer.Reduce(FlgNetParser.Parse(XElement.Parse(Xml)), networkNumber: 1, title: "T", compileUnitUId: "3");
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("access DemoData.Table[DemoIn.Slot] = 22 GlobalVariable idx1=GlobalVariable\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(parsedBlock.Networks[0], parsedSidecars[0]));

        var nested = flgNet.Descendants().Single(e => e.Name.LocalName == "Component" && e.Attribute("AccessModifier") is not null)
            .Elements().Single(e => e.Name.LocalName == "Access");
        Assert.Equal("GlobalVariable", nested.Attribute("Scope")!.Value);
    }
}
