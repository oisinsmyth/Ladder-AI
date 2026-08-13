using System.Xml.Linq;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// *** AN INTERFACE IS COMPARED, NOT DISCARDED *** (2026-08-13).
///
/// `Normalizer.IsVolatile` used to skip any `&lt;Interface&gt;` with no `Section Name="Static"`. The
/// skip was justified, in the Normalizer's own words, by
/// `BlockSourceParser.RequireDefaultInterface` having "already hard-errored upstream if it was
/// anything but the standard parameterless-FC boilerplate". ***THAT GUARD WAS REMOVED ON 2026-07-12
/// WHEN FC/FB PARAMETER INTERFACES LANDED*** (S1 item 20) — the same change that made a code block's
/// Interface real content. The premise expired silently and the comment went on justifying it for a
/// month.
///
/// Measured against real exports, the structural test kept a DB (Static) and an FB (which happens to
/// have Static) and **discarded an FC with parameters (no Static) and a UDT (Section "None" only)**.
/// A UDT is nothing but its interface, so the whole object was going uncompared:
/// `drift-check` printed MATCH for `UDT_PusherIO` and `UDT_ShredderSequencerIO` while each declares
/// an `AutoStartSignal : Bool` absent from its committed export.
///
/// **Both directions are tested.** The retype/extra-member cases are the defect; the identical-pair
/// cases are what keeps this from being a blanket un-ignoring — an FC and a UDT whose interfaces
/// agree must still compare equivalent, or the fix would just be noise. Negative-tested by
/// reinstating the discard: the four "differs" cases go green (i.e. fail) and the "equivalent" ones
/// do not move.
/// </summary>
public class NormalizerInterfaceTests
{
    // An FC's real interface shape: Input/Output/InOut/Temp/Constant/Return, and NO Static. That
    // absence is exactly what the old rule keyed on.
    private static XDocument Fc(string scaleFactorType) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.FC ID="0">
            <AttributeList>
              <Name>FC_ScaleValue</Name>
              <Interface>
                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                  <Section Name="Input">
                    <Member Name="RawValue" Datatype="Int" />
                    <Member Name="ScaleFactor" Datatype="{scaleFactorType}" />
                  </Section>
                  <Section Name="Output"><Member Name="Scaled" Datatype="Real" /></Section>
                  <Section Name="InOut" />
                  <Section Name="Temp" />
                  <Section Name="Constant" />
                  <Section Name="Return"><Member Name="Ret_Val" Datatype="Void" /></Section>
                </Sections>
              </Interface>
            </AttributeList>
          </SW.Blocks.FC>
        </Document>
        """);

    // A UDT's real interface shape: a single Section Name="None". The whole object is its interface.
    private static XDocument Udt(params string[] memberNames) => XDocument.Parse($"""
        <Document>
          <SW.Types.PlcStruct ID="0">
            <AttributeList>
              <Name>UDT_PusherIO</Name>
              <Interface>
                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                  <Section Name="None">
                    {string.Join("\n", memberNames.Select(m => $"<Member Name=\"{m}\" Datatype=\"Bool\" />"))}
                  </Section>
                </Sections>
              </Interface>
            </AttributeList>
          </SW.Types.PlcStruct>
        </Document>
        """);

    // A DB: Section Name="Static". This is the case the old structural test existed to protect, and
    // it must keep working - the fix is a deletion, not a re-scoping.
    private static XDocument Db(string memberType) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.GlobalDB ID="0">
            <AttributeList>
              <Name>DB_Settings</Name>
              <Interface>
                <Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
                  <Section Name="Static"><Member Name="Setpoint" Datatype="{memberType}" /></Section>
                </Sections>
              </Interface>
            </AttributeList>
          </SW.Blocks.GlobalDB>
        </Document>
        """);

    // ------------------------------------------------------------------ the defect, both categories

    /// <summary>
    /// THE measured case. An FC parameter retyped Real -> Int compared EQUIVALENT against its real
    /// export, because an FC has no Static section and the whole Interface was discarded.
    /// </summary>
    [Fact]
    public void FcParameterRetyped_IsNotEquivalent()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(Fc("Real"), Fc("Int")));
    }

    /// <summary>
    /// The corpus case: a UDT declaring a member its export does not have. A UDT is nothing but its
    /// interface, so discarding the interface discarded the entire object.
    /// </summary>
    [Fact]
    public void UdtWithAnExtraMember_IsNotEquivalent()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Udt("Extend", "Retract", "AutoStartSignal"),
            Udt("Extend", "Retract")));
    }

    [Fact]
    public void UdtMemberRetyped_IsNotEquivalent()
    {
        var real = XDocument.Parse(Udt("Extend").ToString().Replace("Datatype=\"Bool\"", "Datatype=\"Int\""));
        Assert.False(Normalizer.AreSemanticallyEquivalent(Udt("Extend"), real));
    }

    [Fact]
    public void FcParameterRenamed_IsNotEquivalent()
    {
        var renamed = XDocument.Parse(Fc("Real").ToString().Replace("RawValue", "RawInput"));
        Assert.False(Normalizer.AreSemanticallyEquivalent(Fc("Real"), renamed));
    }

    // ------------------------------------------------- NOT a blanket un-ignoring: equivalence holds

    /// <summary>
    /// The half that would go green on a fix that simply compared everything and produced noise.
    /// Four FCs in the committed corpus have their interfaces compared for the first time by this
    /// change and all four still report MATCH, which is the same claim at corpus scale.
    /// </summary>
    [Fact]
    public void IdenticalFcInterfaces_AreStillEquivalent()
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(Fc("Real"), Fc("Real")));
    }

    [Fact]
    public void IdenticalUdtInterfaces_AreStillEquivalent()
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(
            Udt("Extend", "Retract", "AutoStartSignal"),
            Udt("Extend", "Retract", "AutoStartSignal")));
    }

    // ---------------------------------------- the original evidence, preserved rather than discarded

    /// <summary>
    /// What the structural test was actually protecting: the 2026-07-10 finding that a BLANKET
    /// "Interface" strip "would have made every DB round-trip trivially pass without ever comparing
    /// member content". Removing the skip preserves that protection in full — it does not reinstate
    /// the blanket strip, it removes the carve-out that was only ever needed because of one.
    /// </summary>
    [Fact]
    public void DbMemberRetyped_IsStillNotEquivalent()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(Db("Real"), Db("Int")));
    }

    [Fact]
    public void IdenticalDbInterfaces_AreStillEquivalent()
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(Db("Real"), Db("Real")));
    }
}
