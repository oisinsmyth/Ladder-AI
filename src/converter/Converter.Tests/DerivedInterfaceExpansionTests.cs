using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter.Compare;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// *** A GATE THAT COULD NOT PASS ON REAL INPUT *** (2026-08-23).
///
/// <para>`drift-check` blocked a real deployment with <c>5 drifted, 8 match</c>. All five were
/// RENDERING differences: TIA's export inlines a typed member's members under it, regenerates an
/// instance DB's member list from its FB, attaches a system-maintained <c>AttributeList</c> to a
/// member whose importable IR form carries none, and re-renders <c>0.10</c> as <c>0.1</c>. The
/// converter emits none of that. So the gate could not pass on any corpus containing an FB with a
/// UDT-typed static or a multi-instance — i.e. on most real blocks. See
/// <see cref="DerivedInterfacePlan"/> for the rules and, per rule, the class of real divergence
/// each one makes the check blind to.</para>
///
/// <para><b>The severity, measured, and it is not "noise".</b> On the same corpus, shrinking a
/// served Modbus area from <c>WORD 1024</c> to <c>WORD 576</c> — a genuine semantic regression —
/// changed the report by not one character, because the object carrying the width was ALREADY
/// permanently red from the structural false positive. An object that always drifts is an object
/// whose drift can no longer be read. The false positives had DISABLED the gate on the five objects
/// they landed on.</para>
///
/// <para><b>Fixtures are synthetic, with invented names.</b> Every shape here was measured on real
/// `Live Runs/` artifacts and then rebuilt from scratch with invented vocabulary — nothing from a
/// live job is reproduced (docs/13 data boundary: use anything, commit nothing).</para>
///
/// <para><b>Written against the pre-change binary first and observed to fail.</b> Built into a
/// throwaway source tree with the two touched files reverted from HEAD: the four
/// <c>…_IsEquivalent</c> cases and <c>Compare_AgreesWithTheNormalizer…</c> all failed there; every
/// negative control below passed there and passes here, which is what makes them controls rather
/// than post-hoc confirmations.</para>
/// </summary>
public class DerivedInterfaceExpansionTests : IDisposable
{
    private readonly string _dir;

    public DerivedInterfaceExpansionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"derived-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private const string InterfaceNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    private const string FlgNetNs = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4";

    // The expansion TIA writes under a typed member: the referenced type's own member list.
    private static string Expansion(params string[] members) => $"""
        <Sections>
          <Section Name="None">
            {string.Join("\n", members.Select(m => $"<Member Name=\"{m}\" Datatype=\"Bool\" />"))}
          </Section>
        </Sections>
        """;

    // The four attributes TIA attaches to a typed member, every one marked SystemDefined.
    private const string SystemAttributes = """
        <AttributeList>
          <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
          <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
          <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
          <BooleanAttribute Name="SetPoint" SystemDefined="true">false</BooleanAttribute>
        </AttributeList>
        """;

    /// <summary>
    /// An FB carrying one Static member, whose body the caller supplies verbatim.
    /// <paramref name="version"/> models a system/library type (`MB_SERVER` 5.3) — the other way a
    /// member names a type declared elsewhere, and the one that has no quotes to key on.
    /// </summary>
    private static XDocument Fb(string datatype, string memberBody, string memberName = "Bus", string? version = null) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.FB ID="0">
            <AttributeList>
              <Name>FB_RigStimulus</Name>
              <Interface>
                <Sections xmlns="{InterfaceNs}">
                  <Section Name="Input" />
                  <Section Name="Output" />
                  <Section Name="InOut" />
                  <Section Name="Static">
                    <Member Name="{memberName}" Datatype="{datatype}"{(version is null ? "" : $" Version=\"{version}\"")} Accessibility="Public">
                      {memberBody}
                    </Member>
                  </Section>
                </Sections>
              </Interface>
            </AttributeList>
          </SW.Blocks.FB>
        </Document>
        """);

    /// <summary>An instance DB whose Static section the caller supplies verbatim.</summary>
    private static XDocument InstanceDb(string staticBody, string instanceOf = "FB_RigStimulus") => XDocument.Parse($"""
        <Document>
          <SW.Blocks.InstanceDB ID="0">
            <AttributeList>
              <InstanceOfName>{instanceOf}</InstanceOfName>
              <InstanceOfType>FB</InstanceOfType>
              <Interface>
                <Sections xmlns="{InterfaceNs}">
                  <Section Name="Input" />
                  <Section Name="Output" />
                  <Section Name="InOut" />
                  <Section Name="Static">{staticBody}</Section>
                </Sections>
              </Interface>
              <Name>iDB_RigStimulus</Name>
              <Number>9100</Number>
            </AttributeList>
          </SW.Blocks.InstanceDB>
        </Document>
        """);

    /// <summary>A global DB with the same Static shape — the carve-out control for rule 3.</summary>
    private static XDocument GlobalDb(string staticBody) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.GlobalDB ID="0">
            <AttributeList>
              <Interface>
                <Sections xmlns="{InterfaceNs}">
                  <Section Name="Static">{staticBody}</Section>
                </Sections>
              </Interface>
              <Name>DB_RigSettings</Name>
              <Number>9101</Number>
            </AttributeList>
          </SW.Blocks.GlobalDB>
        </Document>
        """);

    /// <summary>
    /// One LAD network: a contact on <paramref name="operand"/> feeding a coil, plus one typed
    /// constant. Both are things the interface rules must never reach.
    /// </summary>
    private static XDocument BlockWithNetwork(string operand, string constantType, string constantValue) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.FB ID="0">
            <AttributeList>
              <Name>FB_RigStimulus</Name>
              <Interface>
                <Sections xmlns="{InterfaceNs}">
                  <Section Name="Static">
                    <Member Name="Bus" Datatype="&quot;UDT_RigBus&quot;" Accessibility="Public" />
                  </Section>
                </Sections>
              </Interface>
            </AttributeList>
            <ObjectList>
              <SW.Blocks.CompileUnit ID="4" CompositionName="CompileUnits">
                <AttributeList>
                  <NetworkSource>
                    <FlgNet xmlns="{FlgNetNs}">
                      <Parts>
                        <Access Scope="GlobalVariable" UId="21">
                          <Symbol><Component Name="{operand}" /></Symbol>
                        </Access>
                        <Access Scope="TypedConstant" UId="22">
                          <Constant>
                            <ConstantType>{constantType}</ConstantType>
                            <ConstantValue>{constantValue}</ConstantValue>
                          </Constant>
                        </Access>
                        <Part Name="Contact" UId="23" />
                        <Part Name="Move" UId="24" />
                      </Parts>
                      <Wires>
                        <Wire UId="30"><Powerrail /><NameCon UId="23" Name="in" /></Wire>
                        <Wire UId="31"><IdentCon UId="21" /><NameCon UId="23" Name="operand" /></Wire>
                        <Wire UId="32"><NameCon UId="23" Name="out" /><NameCon UId="24" Name="en" /></Wire>
                        <Wire UId="33"><IdentCon UId="22" /><NameCon UId="24" Name="in" /></Wire>
                      </Wires>
                    </FlgNet>
                  </NetworkSource>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
              </SW.Blocks.CompileUnit>
            </ObjectList>
          </SW.Blocks.FB>
        </Document>
        """);

    /// <summary>A member carrying a Real start value — the other place a Real literal appears.</summary>
    private static XDocument DbWithStartValue(string datatype, string startValue) => XDocument.Parse($"""
        <Document>
          <SW.Blocks.GlobalDB ID="0">
            <AttributeList>
              <Interface>
                <Sections xmlns="{InterfaceNs}">
                  <Section Name="Static">
                    <Member Name="Threshold" Datatype="{datatype}">
                      <StartValue>{startValue}</StartValue>
                    </Member>
                  </Section>
                </Sections>
              </Interface>
              <Name>DB_RigSettings</Name>
            </AttributeList>
          </SW.Blocks.GlobalDB>
        </Document>
        """);

    // ================================================================= RULE 1: type expansion

    /// <summary>
    /// THE measured case, in both of its real forms: two FBs with a UDT-typed static, and one whose
    /// static is a versioned library type (`MB_SERVER` 5.3 — no quotes to key on). The converter
    /// references the type; TIA inlines it.
    /// </summary>
    [Theory]
    [InlineData("&quot;UDT_RigBus&quot;", null)]
    [InlineData("MB_SRV", "5.3")]
    public void TypedMemberInlinedOnOneSideOnly_IsEquivalent(string datatype, string? version)
    {
        var reference = Fb(datatype, "", version: version);
        var inlined = Fb(datatype, Expansion("Start", "Ready"), version: version);

        Assert.True(Normalizer.AreSemanticallyEquivalent(reference, inlined));
        Assert.True(Normalizer.AreSemanticallyEquivalent(inlined, reference));
    }

    /// <summary>
    /// NEGATIVE CONTROL — the inlining is equivalent, the CONTENT is not. Where both sides carry an
    /// expansion, an added or removed member is a difference and stays one. This is what stops rule
    /// 1 from being "ignore typed members".
    /// </summary>
    [Fact]
    public void BothSidesInlined_AnAddedMember_StillDrifts()
    {
        var two = Fb("&quot;UDT_RigBus&quot;", Expansion("Start", "Ready"));
        var three = Fb("&quot;UDT_RigBus&quot;", Expansion("Start", "Ready", "Inhibit"));

        Assert.False(Normalizer.AreSemanticallyEquivalent(two, three));
        Assert.False(Normalizer.AreSemanticallyEquivalent(three, two));
    }

    /// <summary>
    /// NEGATIVE CONTROL — the same at the type's OWN definition site, which is where a bare
    /// reference sends the question. A UDT is nothing but its interface; a removed member there
    /// must drift, or rule 1 would have moved the check to a place that is not looking either.
    /// </summary>
    [Fact]
    public void UdtWithAMemberRemoved_StillDrifts()
    {
        static XDocument Udt(params string[] members) => XDocument.Parse($"""
            <Document>
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Name>UDT_RigBus</Name>
                  <Interface>
                    <Sections xmlns="{InterfaceNs}">
                      <Section Name="None">
                        {string.Join("\n", members.Select(m => $"<Member Name=\"{m}\" Datatype=\"Bool\" />"))}
                      </Section>
                    </Sections>
                  </Interface>
                </AttributeList>
              </SW.Types.PlcStruct>
            </Document>
            """);

        Assert.False(Normalizer.AreSemanticallyEquivalent(Udt("Start", "Ready", "Inhibit"), Udt("Start", "Ready")));
    }

    /// <summary>
    /// NEGATIVE CONTROL — an INLINE STRUCT has no definition site anywhere else, so a missing body
    /// is a converter gap, not a projection of something checked elsewhere. `Struct` carries neither
    /// a quoted type name nor a Version, which is exactly the predicate rule 1 keys on.
    /// </summary>
    [Fact]
    public void StructMemberWithBodyOnOneSideOnly_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Fb("Struct", ""),
            Fb("Struct", Expansion("Start", "Ready"))));
    }

    /// <summary>
    /// NEGATIVE CONTROL — a member that exists on one side only is a difference whatever it carries.
    /// The plan is keyed on members present in BOTH documents precisely so it can never erase one.
    /// </summary>
    [Fact]
    public void MemberPresentOnOneSideOnly_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Fb("&quot;UDT_RigBus&quot;", "", memberName: "Bus"),
            Fb("&quot;UDT_RigBus&quot;", Expansion("Start"), memberName: "OtherBus")));
    }

    /// <summary>
    /// NEGATIVE CONTROL — dropping the expansion must not drop the member's declared TYPE with it.
    /// A retype under a one-sided expansion still drifts.
    /// </summary>
    [Fact]
    public void TypedMemberRetypedUnderAOneSidedExpansion_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Fb("&quot;UDT_RigBus&quot;", ""),
            Fb("&quot;UDT_RigBusV2&quot;", Expansion("Start", "Ready"))));
    }

    // ============================================================ RULE 2: system attribute list

    /// <summary>
    /// THE measured case (a real instance DB, twelve FB-typed statics): the importable `BAREPARAM`
    /// IR form emits no AttributeList; TIA attaches four SystemDefined attributes.
    /// </summary>
    [Fact]
    public void SystemDefinedAttributeListOnOneSideOnly_IsEquivalent()
    {
        var bare = Fb("&quot;FB_RigValve&quot;", "");
        var attributed = Fb("&quot;FB_RigValve&quot;", SystemAttributes);

        Assert.True(Normalizer.AreSemanticallyEquivalent(bare, attributed));
        Assert.True(Normalizer.AreSemanticallyEquivalent(attributed, bare));
    }

    /// <summary>
    /// NEGATIVE CONTROL — one author-set attribute and the whole list is compared again. The rule
    /// requires EVERY child to be a SystemDefined BooleanAttribute.
    /// </summary>
    [Fact]
    public void AttributeListCarryingAnAuthorSetAttribute_StillDrifts()
    {
        const string authored = """
            <AttributeList>
              <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
              <BooleanAttribute Name="SetPoint">true</BooleanAttribute>
            </AttributeList>
            """;

        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Fb("&quot;FB_RigValve&quot;", ""),
            Fb("&quot;FB_RigValve&quot;", authored)));
    }

    /// <summary>
    /// NEGATIVE CONTROL — the rule is gated on the member declaring a NAMED TYPE. A plain Bool
    /// losing its attribute list is not a type projection and still drifts.
    /// </summary>
    [Fact]
    public void PrimitiveMemberLosingItsAttributeList_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Fb("Bool", ""),
            Fb("Bool", SystemAttributes)));
    }

    /// <summary>
    /// NEGATIVE CONTROL — where BOTH sides declare an attribute list, its VALUES are compared. This
    /// is the one that keeps a flipped SetPoint visible on every member the converter does emit a
    /// list for, which is all of them except the BAREPARAM shape.
    /// </summary>
    [Fact]
    public void BothSidesAttributed_AFlippedSetPoint_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            Fb("&quot;FB_RigValve&quot;", SystemAttributes),
            Fb("&quot;FB_RigValve&quot;", SystemAttributes.Replace(">false<", ">true<"))));
    }

    // ============================================================ RULE 3: instance-DB member list

    /// <summary>
    /// THE measured case (a real instance DB of a comms FB): the `.ir` declares `INSTANCEOF` and an empty
    /// `MEMBERS`; TIA regenerates the member list from the FB.
    /// </summary>
    [Fact]
    public void InstanceDbMembersRegeneratedOnOneSideOnly_IsEquivalent()
    {
        var empty = InstanceDb("");
        var regenerated = InstanceDb("""
            <Member Name="Server" Datatype="MB_SRV" Version="5.3" Accessibility="Public" />
            <Member Name="Link" Datatype="TCON_IPV4" Version="1.0" Accessibility="Public" />
            """);

        Assert.True(Normalizer.AreSemanticallyEquivalent(empty, regenerated));
        Assert.True(Normalizer.AreSemanticallyEquivalent(regenerated, empty));
    }

    /// <summary>
    /// NEGATIVE CONTROL — a GLOBAL DB's members are authored, not derived from anything. The same
    /// one-sided-empty shape must still drift there.
    /// </summary>
    [Fact]
    public void GlobalDbWithAnEmptyMemberList_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            GlobalDb(""),
            GlobalDb("<Member Name=\"Setpoint\" Datatype=\"Real\" />")));
    }

    /// <summary>
    /// NEGATIVE CONTROL — an instance DB that DOES declare its members is compared member by member,
    /// exactly as before. The rule fires on an empty section, not on an instance DB.
    /// </summary>
    [Fact]
    public void InstanceDbDeclaringMembers_AMissingOne_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            InstanceDb("<Member Name=\"Server\" Datatype=\"MB_SRV\" Version=\"5.3\" />"),
            InstanceDb("""
                <Member Name="Server" Datatype="MB_SRV" Version="5.3" />
                <Member Name="Link" Datatype="TCON_IPV4" Version="1.0" />
                """)));
    }

    /// <summary>
    /// NEGATIVE CONTROL — the FB an instance DB claims to instantiate is the authored half and stays
    /// compared. Without this, rule 3 would let an instance DB of the wrong FB pass.
    /// </summary>
    [Fact]
    public void InstanceDbOfADifferentFb_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            InstanceDb("", instanceOf: "FB_RigStimulus"),
            InstanceDb("<Member Name=\"Server\" Datatype=\"MB_SRV\" Version=\"5.3\" />", instanceOf: "FB_RigOther")));
    }

    // ============================================================ RULE 4: Real literal rendering

    /// <summary>
    /// THE measured case (a real FB): the author wrote <c>0.10</c>, TIA re-rendered it
    /// <c>0.1</c>. A canonicalization, not an ignore — its blind spot is empty.
    /// </summary>
    [Theory]
    [InlineData("0.10", "0.1")]
    [InlineData("1.00", "1.0")]
    [InlineData("12.3400", "12.34")]
    [InlineData("-0.50", "-0.5")]
    public void RealLiteralWrittenWithTrailingZeros_IsEquivalent(string authored, string reRendered)
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(
            BlockWithNetwork("TagAlpha", "Real", authored),
            BlockWithNetwork("TagAlpha", "Real", reRendered)));
    }

    /// <summary>NEGATIVE CONTROL — a different NUMBER is a different number.</summary>
    [Theory]
    [InlineData("0.1", "0.2")]
    [InlineData("1.0", "10.0")]
    [InlineData("0.10", "0.01")]
    public void RealLiteralWithADifferentValue_StillDrifts(string first, string second)
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            BlockWithNetwork("TagAlpha", "Real", first),
            BlockWithNetwork("TagAlpha", "Real", second)));
    }

    /// <summary>
    /// NEGATIVE CONTROL — one fractional digit is always kept, so a Real literal never collapses
    /// into something an Int literal could equal.
    /// </summary>
    [Fact]
    public void RealLiteralNeverCollapsesToAnInteger()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            BlockWithNetwork("TagAlpha", "Real", "1.0"),
            BlockWithNetwork("TagAlpha", "Real", "1")));
    }

    /// <summary>
    /// NEGATIVE CONTROL — THE WIDTH CASE, and the reason this whole fix exists. An `MB_HOLD_REG`
    /// area pointer is an untyped constant carrying text that happens to end in digits; the
    /// canonicalization is gated on ConstantType Real/LReal and cannot reach it. A shrunk area
    /// still drifts.
    /// </summary>
    [Fact]
    public void AreaPointerWidthChange_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            BlockWithNetwork("TagAlpha", "Any", "P#M1000.0 WORD 1024"),
            BlockWithNetwork("TagAlpha", "Any", "P#M1000.0 WORD 576")));
    }

    /// <summary>
    /// NEGATIVE CONTROL — a changed rung is a changed rung, in a document that also carries an
    /// ignored type expansion. The interface rules must not reach a network.
    /// </summary>
    [Fact]
    public void ChangedRungOperand_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            BlockWithNetwork("TagAlpha", "Real", "0.10"),
            BlockWithNetwork("TagBeta", "Real", "0.1")));
    }

    /// <summary>A Real START VALUE is the same literal under the same type gate.</summary>
    [Fact]
    public void RealStartValueWrittenWithTrailingZeros_IsEquivalent()
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(
            DbWithStartValue("Real", "2.50"),
            DbWithStartValue("Real", "2.5")));
    }

    /// <summary>
    /// NEGATIVE CONTROL — the type gate is what keeps this off anything that is not a number. A
    /// String member's start value is text, and <c>0.10</c> is not <c>0.1</c> there.
    /// </summary>
    [Fact]
    public void StartValueOnANonFloatingMember_StillDrifts()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(
            DbWithStartValue("String[8]", "0.10"),
            DbWithStartValue("String[8]", "0.1")));
    }

    // ======================================================= the two derivations must not diverge

    /// <summary>
    /// `converter compare` builds the SAME plan, and this is a regression guard for a bug that was
    /// actually hit: with the plan threaded into `AreSemanticallyEquivalent` only, `compare`'s walk
    /// localized nothing while the Normalizer said "not equivalent", and every affected object came
    /// back <c>NOT COMPARED</c> — the tool's own cross-check firing on the tool. Two derivations of
    /// one rule that can disagree is a defect class this project has already paid for.
    /// </summary>
    [Fact]
    public void Compare_AgreesWithTheNormalizerOnADerivedExpansion()
    {
        var reference = Write("reference.xml", Fb("&quot;UDT_RigBus&quot;", ""));
        var inlined = Write("inlined.xml", Fb("&quot;UDT_RigBus&quot;", Expansion("Start", "Ready")));

        var report = CompareRunner.Run(reference, inlined, allowSilentLayout: true);

        Assert.Equal(CompareStatus.Equivalent, report.Status);
        Assert.Empty(report.Differences);
    }

    /// <summary>
    /// And it still NAMES a real difference in the same document shape — the half that proves the
    /// agreement above is not agreement by blindness.
    /// </summary>
    [Fact]
    public void Compare_StillNamesARealDifferenceBesideADerivedExpansion()
    {
        var first = Write("first.xml", Fb("&quot;UDT_RigBus&quot;", "", memberName: "Bus"));
        var second = Write("second.xml", Fb("&quot;UDT_RigBusV2&quot;", Expansion("Start"), memberName: "Bus"));

        var report = CompareRunner.Run(first, second, allowSilentLayout: true);

        Assert.Equal(CompareStatus.Differs, report.Status);
        Assert.Contains(report.Differences, d =>
            d.Kind == DifferenceKind.AttributeDiffers
            && d.Path.EndsWith("/@Datatype", StringComparison.Ordinal)
            && d.First == "\"UDT_RigBus\""
            && d.Second == "\"UDT_RigBusV2\"");
    }

    /// <summary>
    /// An emptied element and a natively empty one must be the same shape. XElement's Value setter
    /// appends an XText node even for the empty string, so an element whose only children were
    /// dropped compared UNEQUAL to one that never had any — the concrete bug behind the NOT COMPARED
    /// verdicts above, pinned here at the level it actually lives at.
    /// </summary>
    [Fact]
    public void AnElementEmptiedByAPlan_MatchesANativelyEmptyOne()
    {
        var emptied = Normalizer.Strip(
            InstanceDb("<Member Name=\"Server\" Datatype=\"MB_SRV\" Version=\"5.3\" />").Root!,
            compareMemoryLayout: false,
            DerivedInterfacePlan.For(InstanceDb("").Root!, InstanceDb("<Member Name=\"Server\" Datatype=\"MB_SRV\" Version=\"5.3\" />").Root!));
        var natively = Normalizer.Strip(InstanceDb("").Root!, compareMemoryLayout: false, DerivedInterfacePlan.Nothing);

        var emptiedSection = emptied.Descendants().First(e => (string?)e.Attribute("Name") == "Static");
        var nativelySection = natively.Descendants().First(e => (string?)e.Attribute("Name") == "Static");

        Assert.True(XNode.DeepEquals(emptiedSection, nativelySection));
    }

    private string Write(string name, XDocument document)
    {
        var path = Path.Combine(_dir, name);
        document.Save(path);
        return path;
    }
}
