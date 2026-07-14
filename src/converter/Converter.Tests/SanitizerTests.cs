using System.Xml.Linq;
using Converter.Sanitize;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class SanitizerTests
{
    private static BlockSource LoadFixture(string name)
    {
        var document = XDocument.Load(Path.Combine("Fixtures", name));
        return BlockSourceParser.Parse(document);
    }

    private static SanitizationMap FullMap() => new()
    {
        Names = new Dictionary<string, string> { ["RealBlockName"] = "SanitizedBlockName" },
        Comments = new Dictionary<string, string> { ["RealBlockName"] = "Sanitized block comment." },
        NetworkComments = new Dictionary<string, string> { ["RealBlockName#1"] = "Sanitized network comment." },
        NetworkTitles = new Dictionary<string, string> { ["RealBlockName#1"] = "Sanitized network title." },
        Tags = new Dictionary<string, string>
        {
            ["RealProcess.RealTag"] = "SanitizedProcess.SanitizedTag",
            ["RealOutput"] = "SanitizedOutput",
        },
    };

    [Fact]
    public void Apply_WithCompleteMap_RenamesEverythingIdentifying()
    {
        var block = LoadFixture("SanitizeSource.xml");

        var sanitized = Sanitizer.Apply(block, FullMap());

        Assert.Equal("SanitizedBlockName", sanitized.Name);
        Assert.Equal("Sanitized block comment.", sanitized.Comment);
        Assert.Equal("Sanitized network comment.", sanitized.CompileUnits[0].Comment);
        Assert.Equal("Sanitized network title.", sanitized.CompileUnits[0].Title);

        var accessPaths = sanitized.CompileUnits[0].Network.AccessNodes.Select(a => a.DottedPath).ToList();
        Assert.Contains("SanitizedProcess.SanitizedTag", accessPaths);
        Assert.Contains("SanitizedOutput", accessPaths);
        Assert.DoesNotContain(accessPaths, p => p.Contains("Real", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_PreservesStructuralData_UIdsAndNumber()
    {
        var block = LoadFixture("SanitizeSource.xml");

        var sanitized = Sanitizer.Apply(block, FullMap());

        Assert.Equal(block.RootUId, sanitized.RootUId);
        Assert.Equal(block.Number, sanitized.Number);
        Assert.Equal(block.Kind, sanitized.Kind);
        Assert.Equal(block.CompileUnits[0].UId, sanitized.CompileUnits[0].UId);
    }

    [Fact]
    public void Apply_MissingTagMapping_HardErrorsListingIt()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.Tags.Remove("RealOutput");

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("RealOutput", ex.Message);
    }

    [Fact]
    public void Apply_MissingBlockName_HardErrors()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.Names.Clear();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("Names[\"RealBlockName\"]", ex.Message);
    }

    [Fact]
    public void Apply_MissingNetworkComment_HardErrors()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.NetworkComments.Clear();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("NetworkComments", ex.Message);
    }

    [Fact]
    public void Apply_MissingNetworkTitle_HardErrors()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.NetworkTitles.Clear();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("NetworkTitles", ex.Message);
    }

    // Block-level Title (S1 item 17, 2026-07-12) — confirmed real after being assumed
    // always-empty at block level (S1 item 16's own grounding only found network-level Title
    // populated). SanitizeSource.xml has no block-level Title element (matches every block seen
    // before PlantAutoControl's own dependency FBs), so these use a small standalone BlockSource
    // rather than the shared fixture.
    [Fact]
    public void Apply_WithMappedBlockTitle_SanitizesIt()
    {
        var block = new BlockSource("0", "FB", "RealBlockName", 1, "LAD", null, new[]
        {
            new CompileUnitSource("3", null, null, new FlgNetwork(Array.Empty<AccessNode>(), Array.Empty<PartNode>(), Array.Empty<WireNode>())),
        }, Title: "Real block title mentioning RealSite.");
        var map = FullMap();
        map.Titles["RealBlockName"] = "Sanitized block title.";

        var sanitized = Sanitizer.Apply(block, map);

        Assert.Equal("Sanitized block title.", sanitized.Title);
    }

    [Fact]
    public void Apply_MissingBlockTitle_HardErrors()
    {
        var block = new BlockSource("0", "FB", "RealBlockName", 1, "LAD", null, new[]
        {
            new CompileUnitSource("3", null, null, new FlgNetwork(Array.Empty<AccessNode>(), Array.Empty<PartNode>(), Array.Empty<WireNode>())),
        }, Title: "Real block title mentioning RealSite.");
        var map = FullMap();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("Titles[\"RealBlockName\"]", ex.Message);
    }

    [Fact]
    public void Apply_CollectsAllMissingEntriesInOneError()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = new SanitizationMap();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("Names[\"RealBlockName\"]", ex.Message);
        Assert.Contains("Comments[\"RealBlockName\"]", ex.Message);
        Assert.Contains("NetworkComments[\"RealBlockName#1\"]", ex.Message);
        Assert.Contains("RealProcess.RealTag", ex.Message);
        Assert.Contains("RealOutput", ex.Message);
    }

    // Real bug, found live 2026-07-14 (MotorDOL/MotorStarter): a Part's own Instance (a TON's
    // timer instance, or a CALL's own FB instance) was never sanitized — SanitizeNetwork only
    // walked network.AccessNodes, never network.Parts, so a Part.Instance's own ComponentPath
    // passed through unmodified even though the exact same name got correctly renamed at its
    // Interface-declaration site (a separate code path, SanitizeMember). An ordinary Access-based
    // rename worked everywhere by contrast, since it never happens to also be a Part's own
    // Instance — this was invisible until a real, non-identity-mapped Instance rename was tried.
    [Fact]
    public void Apply_TimerInstanceReference_IsSanitized()
    {
        var block = LoadFixture("SanitizeSourceWithTimerInstance.xml");
        var map = FullMap();
        map.Tags["RealTimerInstance"] = "SanitizedTimerInstance";

        var sanitized = Sanitizer.Apply(block, map);

        var tonPart = Assert.Single(sanitized.CompileUnits[0].Network.Parts, p => p.Name == "TON");
        Assert.Equal("SanitizedTimerInstance", tonPart.Instance!.DottedPath);
    }

    // Real bug, found live 2026-07-13 (`FB MotorVSDSystem`'s own call to sanitized `Scale` ->
    // `AnalogScale`): a Call Part's own BlockName (the *callee*'s name) is exactly the same
    // identifying-name category as a DB/UDT name (sanitized via map.Names elsewhere) but lives
    // only on PartNode, never reached by SanitizeNetwork's own pass — a caller compiled clean
    // standalone yet failed to import once its callee was independently renamed, because the
    // caller's own network body still said `CallInfo Name="Scale"`. Only Instance (the callee's
    // own instance-DB reference) was sanitized before this fix; BlockName was not.
    [Fact]
    public void Apply_CallBlockName_IsSanitizedViaNamesMap()
    {
        var block = LoadFixture("SanitizeSourceWithCall.xml");
        var map = FullMap();
        map.Names["RealCalleeName"] = "SanitizedCalleeName";

        var sanitized = Sanitizer.Apply(block, map);

        var callPart = Assert.Single(sanitized.CompileUnits[0].Network.Parts, p => p.Name == "Call");
        Assert.Equal("SanitizedCalleeName", callPart.BlockName);
    }

    [Fact]
    public void Apply_CallBlockName_MissingMapping_HardErrors()
    {
        var block = LoadFixture("SanitizeSourceWithCall.xml");
        var map = FullMap();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("Names[\"RealCalleeName\"]", ex.Message);
    }

    // Real bug, found live 2026-07-14 (`FB ShredderControlSystem`'s own reference to the real Siemens
    // system tag `Clock_0.5Hz`): a single real component's own name can contain a literal '.' —
    // blindly splitting the invented value on every '.' turned an identity-mapped `Clock_0.5Hz`
    // into two bogus components (`Clock_0`/`5Hz`), which TIA then reported as an undefined tag.
    // Confirmed real via `FC ShredderControlSystem`'s own fresh export: `<Component Name="Clock_0.5Hz"
    // />` — one component, not two.
    [Fact]
    public void Apply_SingleComponentAccessWithDotInInventedName_IsNotSplit()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.Tags["RealOutput"] = "Clock_0.5Hz";

        var sanitized = Sanitizer.Apply(block, map);

        var access = Assert.Single(
            sanitized.CompileUnits[0].Network.AccessNodes,
            a => a.DottedPath == "Clock_0.5Hz");
        Assert.Single(access.ComponentPath);
    }

    private static DbSource LoadDbFixture(string name)
    {
        var document = XDocument.Load(Path.Combine("Fixtures", name));
        return DbSourceParser.Parse(document);
    }

    private static SanitizationMap FullDbMap() => new()
    {
        Names = new Dictionary<string, string> { ["RealDbName"] = "SanitizedDbName" },
        Comments = new Dictionary<string, string> { ["RealDbName"] = "Sanitized DB comment." },
        Tags = new Dictionary<string, string>
        {
            ["RealDbName.RealFlag"] = "SanitizedDbName.SanitizedFlag",
            ["RealDbName.RealWord"] = "SanitizedDbName.SanitizedWord",
            ["RealDbName.RealArray"] = "SanitizedDbName.SanitizedArray",
            ["RealDbName.RealName"] = "SanitizedDbName.SanitizedName",
        },
        StartValues = new Dictionary<string, string>
        {
            ["RealDbName.RealName"] = "'Sanitized Site Name'",
        },
    };

    [Fact]
    public void ApplyToDb_WithCompleteMap_RenamesEverythingIdentifying()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");

        var sanitized = Sanitizer.ApplyToDb(db, FullDbMap());

        Assert.Equal("SanitizedDbName", sanitized.Name);
        Assert.Equal("Sanitized DB comment.", sanitized.Comment);

        var memberNames = sanitized.Members.Select(m => m.Name).ToList();
        Assert.Equal(new[] { "SanitizedFlag", "SanitizedWord", "SanitizedArray", "SanitizedName" }, memberNames);

        var nameMember = sanitized.Members.Single(m => m.Name == "SanitizedName");
        Assert.Equal("'Sanitized Site Name'", nameMember.StartValue);
    }

    [Fact]
    public void ApplyToDb_PreservesStructuralData()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");

        var sanitized = Sanitizer.ApplyToDb(db, FullDbMap());

        Assert.Equal(db.RootUId, sanitized.RootUId);
        Assert.Equal(db.Number, sanitized.Number);
        Assert.Equal(db.Members[0].Datatype, sanitized.Members[0].Datatype);
        Assert.Equal(db.Members[1].Retain, sanitized.Members[1].Retain);
        // Non-string start value (hex word) is structural, never mapped.
        Assert.Equal("16#0041", sanitized.Members[1].StartValue);
        Assert.Equal(db.Members[2].Datatype, sanitized.Members[2].Datatype);
    }

    [Fact]
    public void ApplyToDb_MissingStringStartValueMapping_HardErrors()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");
        var map = FullDbMap();
        map.StartValues.Remove("RealDbName.RealName");

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToDb(db, map));
        Assert.Contains("StartValues[\"RealDbName.RealName\"]", ex.Message);
    }

    [Fact]
    public void ApplyToDb_MissingMemberTagMapping_HardErrors()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");
        var map = FullDbMap();
        map.Tags.Remove("RealDbName.RealFlag");

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToDb(db, map));
        Assert.Contains("RealDbName.RealFlag", ex.Message);
    }

    [Fact]
    public void ApplyToDb_InstanceDb_RenamesInstanceOfNameViaNamesMap()
    {
        var db = LoadDbFixture("InstanceDbWithStructuredMembers.xml");
        var map = new SanitizationMap
        {
            Names = new Dictionary<string, string> { ["ConveyorMotor1"] = "SanitizedConveyor", ["MotorDOL"] = "SanitizedMotorFB", ["TypeDOL"] = "SanitizedType" },
            Tags = new Dictionary<string, string>
            {
                ["ConveyorMotor1.IO"] = "SanitizedConveyor.SanitizedIO",
                ["ConveyorMotor1.FaultTripTimer2"] = "SanitizedConveyor.SanitizedTimer",
            },
        };

        var sanitized = Sanitizer.ApplyToDb(db, map);

        Assert.Equal("SanitizedConveyor", sanitized.Name);
        Assert.Equal("SanitizedMotorFB", sanitized.InstanceOfName);
    }

    [Fact]
    public void ApplyToDb_InstanceDb_MissingInstanceOfNameMapping_HardErrors()
    {
        var db = LoadDbFixture("InstanceDbWithStructuredMembers.xml");
        var map = new SanitizationMap
        {
            Names = new Dictionary<string, string> { ["ConveyorMotor1"] = "SanitizedConveyor" },
            Tags = new Dictionary<string, string>
            {
                ["ConveyorMotor1.IO"] = "SanitizedConveyor.SanitizedIO",
                ["ConveyorMotor1.FaultTripTimer2"] = "SanitizedConveyor.SanitizedTimer",
            },
        };

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToDb(db, map));
        Assert.Contains("Names[\"MotorDOL\"]", ex.Message);
    }

    [Fact]
    public void ApplyToDb_StructuredMember_PreservesNestedMemberNamesAsStructural()
    {
        var db = LoadDbFixture("InstanceDbWithStructuredMembers.xml");
        var map = new SanitizationMap
        {
            Names = new Dictionary<string, string> { ["ConveyorMotor1"] = "SanitizedConveyor", ["MotorDOL"] = "SanitizedMotorFB", ["TypeDOL"] = "SanitizedType" },
            Tags = new Dictionary<string, string>
            {
                ["ConveyorMotor1.IO"] = "SanitizedConveyor.SanitizedIO",
                ["ConveyorMotor1.FaultTripTimer2"] = "SanitizedConveyor.SanitizedTimer",
            },
        };

        var sanitized = Sanitizer.ApplyToDb(db, map);

        var io = sanitized.Members.Single(m => m.Name == "SanitizedIO");
        // Nested member names are the UDT/timer's own reusable field names, not site-specific
        // identifying data — same category as Datatype, so they pass through unmapped.
        Assert.Equal(new[] { "InHand", "Running" }, io.NestedMembers!.Select(m => m.Name));
    }

    [Fact]
    public void ApplyToDb_QuotedUdtDatatype_IsSanitizedViaNamesMap()
    {
        var db = LoadDbFixture("GlobalDbWithUdtTypedMember.xml");
        var map = new SanitizationMap
        {
            Names = new Dictionary<string, string> { ["RealDbName"] = "SanitizedDbName", ["TypeDOL"] = "SanitizedType" },
            Tags = new Dictionary<string, string> { ["RealDbName.IO"] = "SanitizedDbName.SanitizedIO" },
        };

        var sanitized = Sanitizer.ApplyToDb(db, map);

        var io = Assert.Single(sanitized.Members);
        Assert.Equal("\"SanitizedType\"", io.Datatype);
    }

    [Fact]
    public void ApplyToDb_QuotedUdtDatatype_MissingMapping_HardErrors()
    {
        var db = LoadDbFixture("GlobalDbWithUdtTypedMember.xml");
        var map = new SanitizationMap
        {
            Names = new Dictionary<string, string> { ["RealDbName"] = "SanitizedDbName" },
            Tags = new Dictionary<string, string> { ["RealDbName.IO"] = "SanitizedDbName.SanitizedIO" },
        };

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToDb(db, map));
        Assert.Contains("Names[\"TypeDOL\"]", ex.Message);
    }

    [Fact]
    public void ApplyToDb_NestedMemberStringStartValue_IsSanitized()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Blocks.GlobalDB ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="Static">
                <Member Name="IO" Datatype="&quot;TypeDOL&quot;" Remanence="NonRetain" Accessibility="Public">
                  <AttributeList>
                    <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="SetPoint" SystemDefined="true">true</BooleanAttribute>
                  </AttributeList>
                  <Sections>
                    <Section Name="None">
                      <Member Name="EquipmentName" Datatype="String"><StartValue>'Real Equipment Name'</StartValue></Member>
                    </Section>
                  </Sections>
                </Member>
              </Section>
            </Sections></Interface>
                  <Name>RealDbName</Name>
                  <Namespace />
                  <Number>7</Number>
                  <ProgrammingLanguage>DB</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>
                  <MultilingualText ID="1" CompositionName="Comment">
                    <ObjectList>
                      <MultilingualTextItem ID="2" CompositionName="Items">
                        <AttributeList>
                          <Culture>en-US</Culture>
                          <Text />
                        </AttributeList>
                      </MultilingualTextItem>
                    </ObjectList>
                  </MultilingualText>
                </ObjectList>
              </SW.Blocks.GlobalDB>
            </Document>
            """);
        var db = DbSourceParser.Parse(xml);
        var map = new SanitizationMap
        {
            Names = new Dictionary<string, string> { ["RealDbName"] = "SanitizedDbName", ["TypeDOL"] = "SanitizedType" },
            Tags = new Dictionary<string, string> { ["RealDbName.IO"] = "SanitizedDbName.IO" },
            StartValues = new Dictionary<string, string> { ["RealDbName.IO.EquipmentName"] = "'Sanitized Equipment Name'" },
        };

        var sanitized = Sanitizer.ApplyToDb(db, map);

        var io = Assert.Single(sanitized.Members);
        var equipmentName = Assert.Single(io.NestedMembers!);
        Assert.Equal("EquipmentName", equipmentName.Name);
        Assert.Equal("'Sanitized Equipment Name'", equipmentName.StartValue);
    }
}
