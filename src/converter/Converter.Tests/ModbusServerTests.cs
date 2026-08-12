using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// MAKING `MB_SERVER` EXPRESSIBLE IN IR (2026-08-12).
///
/// <para>Grounded on a genuine TIA V20 export of an S7-1200 (classic 1214C) Modbus TCP FB.
/// <c>Fixtures/ModbusServerInterfaceBlock.xml</c> reproduces its XML SHAPE exactly — same elements,
/// same attribute order, same nesting — with every identifier renamed and every address value
/// invented, per the data boundary. Element COUNTS are trimmed (2 table rows rather than 10, 4
/// registers rather than 90); shapes are not.</para>
///
/// <para>The owner's ruling that motivated it: <b>NO IR THE AI CANNOT CHANGE.</b> A block the
/// converter cannot express is a block the AI can never modify. Five gaps stood between
/// <c>MB_SERVER</c> and that, and two more were found on the way:</para>
///
/// <list type="number">
///   <item>`Array[1..n] of Struct` with inline nested members — a hard error.</item>
///   <item>Doubly-nested structured members — a hard error, and on the critical path: `CONNECT`
///         must point at a `TCON_IP_v4`, which nests an `IP_V4`, which nests an array.</item>
///   <item>The `MB_SERVER` 5.3 Part template — unregistered.</item>
///   <item>The multi-instance member writer DROPPED `Version` and the `AttributeList` — silently.</item>
///   <item>`&lt;Subelement&gt;` array start values were read by NO parse path and written by NO
///         write path. <b>The block reached `exit 0` with its whole configuration table zeroed.</b></item>
///   <item>Parameter-section members were re-emitted in the bare shape, losing their
///         `AttributeList` — FI-59's remedy was wider than its evidence.</item>
///   <item>A multi-line network comment was a permanent hard error, for want of an escape sequence.</item>
/// </list>
/// </summary>
public class ModbusServerTests
{
    private const string Fixture = "ModbusServerInterfaceBlock.xml";

    private static XDocument LoadFixture(string name = Fixture) => XDocument.Load(Path.Combine("Fixtures", name));

    private static (IrBlock Block, List<NetworkSidecar> Sidecars) ToIr(XDocument document)
    {
        var block = BlockSourceParser.Parse(document);
        var networks = new List<IrNetwork>();
        var sidecars = new List<NetworkSidecar>();
        foreach (var compileUnit in block.CompileUnits)
        {
            var reduced = GraphReducer.Reduce(compileUnit.Network, networks.Count + 1, compileUnit.Title ?? string.Empty, compileUnit.UId);
            networks.Add(reduced.Network with { Comment = compileUnit.Comment });
            sidecars.Add(reduced.Sidecar);
        }

        var irBlock = new IrBlock(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, networks,
            block.StaticMembers, block.TempMembers, block.Title, block.InputMembers, block.OutputMembers,
            block.InOutMembers, block.ConstantMembers, block.SecondaryType, block.MemoryLayout);
        return (irBlock, sidecars);
    }

    private static string ToIrText(XDocument document)
    {
        var (block, sidecars) = ToIr(document);
        return IrSerializer.SerializeBlock(block, sidecars);
    }

    private static XDocument ToXml(string irText)
    {
        var (block, sidecars) = IrParser.ParseBlock(irText);
        return XDocument.Parse(Program.BuildBlockXml(block, sidecars).ToString());
    }

    private static readonly XNamespace Ifc = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    private static XElement StaticMemberElement(XDocument document, string name) =>
        document.Descendants(Ifc + "Member").First(m => (string?)m.Attribute("Name") == name);

    private static DbMember StaticMember(XDocument document, string name) =>
        BlockSourceParser.Parse(document).StaticMembers!.First(m => m.Name == name);

    // ------------------------------------------------ gap 1: Array[…] of Struct, inline members

    [Fact]
    public void ArrayOfStruct_WithInlineNestedMembers_Parses()
    {
        var table = StaticMember(LoadFixture(), "NodeTable");

        Assert.Equal("Array[1..2] of Struct", table.Datatype);
        Assert.True(table.Retain);
        Assert.Equal(new[] { "NodeAddr", "NodeId" }, table.NestedMembers!.Select(m => m.Name));
    }

    [Fact]
    public void ArrayOfStruct_SerializesAsIndentedMemberLines()
    {
        var ir = ToIrText(LoadFixture());

        Assert.Contains("    NodeTable : Array[1..2] of Struct RETAIN\n", ir);
        Assert.Contains("      NodeAddr : Array[1..4] of Byte\n", ir);
    }

    [Fact]
    public void ArrayOfStruct_RoundTripsToDirectNestedMembersNotASectionsWrapper()
    {
        var table = StaticMemberElement(ToXml(ToIrText(LoadFixture())), "NodeTable");

        // Direct <Member> children, exactly like a bare `Struct` — NOT <Sections><Section Name="None">.
        Assert.Equal(2, table.Elements(Ifc + "Member").Count());
        Assert.Empty(table.Elements(Ifc + "Sections"));
    }

    // -------------------------------------------- gap 2: doubly-nested structured members

    [Fact]
    public void DoublyNestedStructuredMember_ParsesAndKeepsItsInnerMembers()
    {
        var link = StaticMember(LoadFixture(), "Link");

        Assert.Equal("TCON_IP_v4", link.Datatype);
        var remoteAddress = link.NestedMembers!.First(m => m.Name == "RemoteAddress");

        // The second level of nesting — the shape that used to hard-error with
        // "a doubly-nested structured member ... is outside this slice".
        Assert.Equal("IP_V4", remoteAddress.Datatype);
        Assert.Equal("1.0", remoteAddress.Version);
        var addr = Assert.Single(remoteAddress.NestedMembers!);
        Assert.Equal("ADDR", addr.Name);
    }

    [Fact]
    public void DoublyNestedStructuredMember_SerializesAsAThirdIndentLevel()
    {
        var ir = ToIrText(LoadFixture());

        Assert.Contains("    Link : TCON_IP_v4 VERSION 1.0 RETAIN SETPOINT\n", ir);
        Assert.Contains("      RemoteAddress : IP_V4 VERSION 1.0\n", ir);
        Assert.Contains("        ADDR : Array[1..4] of Byte\n", ir);
    }

    [Fact]
    public void DoublyNestedStructuredMember_RoundTripsWithItsOwnSectionsWrapper()
    {
        var remoteAddress = StaticMemberElement(ToXml(ToIrText(LoadFixture())), "RemoteAddress");

        Assert.Equal("1.0", (string?)remoteAddress.Attribute("Version"));
        var section = Assert.Single(Assert.Single(remoteAddress.Elements(Ifc + "Sections")).Elements(Ifc + "Section"));
        Assert.Equal("None", (string?)section.Attribute("Name"));
        Assert.Equal("ADDR", (string?)Assert.Single(section.Elements(Ifc + "Member")).Attribute("Name"));
    }

    // ------------------------------------------------------- gap 3: the MB_SERVER 5.3 template

    [Fact]
    public void MbServer53_ReducesToAFixedShapeStatementWithTheTemplatePortsInOrder()
    {
        var (block, _) = ToIr(LoadFixture());

        var statement = Assert.Single(block.Networks[0].FixedShapes);
        Assert.Equal("MB_SERVER", statement.Instruction);
        Assert.Equal("Server", statement.InstancePath);
        Assert.Equal(
            new[] { "DISCONNECT", "MB_HOLD_REG", "CONNECT", "NDR", "DR", "ERROR", "STATUS" },
            statement.Arguments.Select(a => a.Port));
    }

    [Fact]
    public void MbServer53_InOutPortsBindAsOrdinarySymbolicOperands()
    {
        var (block, _) = ToIr(LoadFixture());
        var statement = Assert.Single(block.Networks[0].FixedShapes);

        // Genuinely InOut on the instruction; wired as plain reads, with no pointer syntax and no
        // <Parameter Section=…> anywhere in the document. See PortDirection's own doc comment.
        var holdReg = Assert.Single(statement.Arguments, a => a.Port == "MB_HOLD_REG");
        Assert.Equal("HoldReg", Assert.IsType<Expr.TagRef>(Assert.IsType<PortBinding.Value>(holdReg.Binding).Expr).Path);

        var connect = Assert.Single(statement.Arguments, a => a.Port == "CONNECT");
        Assert.Equal("Link", Assert.IsType<Expr.TagRef>(Assert.IsType<PortBinding.Value>(connect.Binding).Expr).Path);
    }

    [Fact]
    public void MbServer53_UnconnectedOutputsAreShownAsOpenNotHidden()
    {
        var ir = ToIrText(LoadFixture());

        Assert.Contains("MB_SERVER(Server, EN := TRUE, DISCONNECT := Server.DISCONNECT, " +
                        "MB_HOLD_REG := HoldReg, CONNECT := Link, NDR => OPEN, DR => OPEN, " +
                        "ERROR => OPEN, STATUS => Server.STATUS)", ir);
    }

    // ------------------------------------- gap 4: the multi-instance member's Version/AttributeList

    [Fact]
    public void MultiInstanceMember_KeepsItsVersionThroughTheWholeRoundTrip()
    {
        var ir = ToIrText(LoadFixture());
        Assert.Contains("    Server : MB_SERVER VERSION 5.3\n", ir);

        // The IR already said VERSION 5.3 before this fix; the XML did not. Assert the ELEMENT.
        var server = StaticMemberElement(ToXml(ir), "Server");
        Assert.Equal("5.3", (string?)server.Attribute("Version"));
        Assert.Single(server.Elements(Ifc + "AttributeList"));

        // The one thing TIA genuinely refuses on a multi-instance.
        Assert.Null(server.Attribute("Remanence"));
    }

    [Fact]
    public void MultiInstanceOfAFixedShapeInstruction_IsRecognisedWithoutACall()
    {
        // multiInstanceStatics was derived from CALL statements only, and MB_SERVER is a <Part>, not
        // a <Call> — so its static would have been emitted WITH Remanence and refused at import.
        var server = StaticMemberElement(ToXml(ToIrText(LoadFixture())), "Server");

        Assert.Null(server.Attribute("Remanence"));
    }

    // ------------------------------------------------------ gap 5: <Subelement> array start values

    [Fact]
    public void Subelements_OnATopLevelArrayMember_AreParsed()
    {
        var holdReg = StaticMember(LoadFixture(), "HoldReg");

        Assert.Equal(new[] { "1", "4" }, holdReg.Subelements.Select(s => s.Path));
        Assert.Equal(new[] { "11", "-101" }, holdReg.Subelements.Select(s => s.StartValue));
    }

    [Fact]
    public void Subelements_SerializeAsIndexedStartValueLines()
    {
        var ir = ToIrText(LoadFixture());

        Assert.Contains("      [1] = 11\n", ir);
        Assert.Contains("      [4] = -101\n", ir);
        // Multi-dimensional path: outer array index first, then the inner one.
        Assert.Contains("        [2,4] = 16#0C\n", ir);
    }

    /// <summary>
    /// THE ONE THAT MATTERS. All three member shapes carry subelements in the real export — a
    /// top-level Static array (`HoldReg`), an array nested in an `Array[…] of Struct` (`NodeAddr`,
    /// the type-member shape) and an array inside a doubly-nested structured member
    /// (`RemoteAddress.ADDR`, the bare shape). Every one of them was dropped in silence.
    /// </summary>
    [Fact]
    public void Subelements_SurviveTheRoundTripOnAllThreeMemberShapes()
    {
        var regenerated = ToXml(ToIrText(LoadFixture()));

        static IEnumerable<(string Path, string Value)> Subelements(XElement member) =>
            member.Elements(Ifc + "Subelement").Select(s => ((string)s.Attribute("Path")!, s.Value));

        Assert.Equal(
            new[] { ("1", "11"), ("4", "-101") },
            Subelements(StaticMemberElement(regenerated, "HoldReg")));

        Assert.Equal(
            new[] { ("1,1", "16#C0"), ("1,2", "16#A8"), ("1,3", "16#01"), ("1,4", "16#0B"),
                    ("2,1", "16#C0"), ("2,2", "16#A8"), ("2,3", "16#01"), ("2,4", "16#0C") },
            Subelements(StaticMemberElement(regenerated, "NodeAddr")));

        Assert.Equal(
            new[] { ("1", "16#C0"), ("2", "16#A8"), ("3", "16#01"), ("4", "16#0A") },
            Subelements(StaticMemberElement(regenerated, "ADDR")));
    }

    [Fact]
    public void Subelements_CountIsPreservedExactly_NoSilentNToZero()
    {
        var original = LoadFixture();
        var regenerated = ToXml(ToIrText(original));

        var before = original.Descendants(Ifc + "Subelement").Count();
        Assert.Equal(16, before);
        Assert.Equal(before, regenerated.Descendants(Ifc + "Subelement").Count());
    }

    // -------------------------------------------------- gap 6: parameter-section member shape

    [Fact]
    public void ParameterMember_KeepsItsAttributeListButNeverItsRemanence()
    {
        var clear = ToXml(ToIrText(LoadFixture())).Descendants(Ifc + "Member")
            .First(m => (string?)m.Attribute("Name") == "clear");

        // FI-59's proven constraint — TIA refuses this one and only this one.
        Assert.Null(clear.Attribute("Remanence"));

        // The half that was collateral: TIA's own export carries these three, so re-emitting them
        // is faithful, not a widening.
        var attributes = Assert.Single(clear.Elements(Ifc + "AttributeList"));
        Assert.Equal(
            new[] { "ExternalAccessible", "ExternalVisible", "ExternalWritable" },
            attributes.Elements().Select(e => (string)e.Attribute("Name")!));
    }

    // --------------------------------------------------------- gap 7: multi-line network comment

    [Fact]
    public void MultiLineNetworkComment_SurvivesOnOneIrLine()
    {
        var ir = ToIrText(LoadFixture());

        Assert.Contains(
            "  COMMENT \"Serve holding registers to the line controller.\\nSecond line of the note.\\n\"\n",
            ir);

        var (block, _) = IrParser.ParseBlock(ir);
        Assert.Equal(
            "Serve holding registers to the line controller.\nSecond line of the note.\n",
            block.Networks[0].Comment);
    }

    // ------------------------------------------------------------------ round trip and stability

    /// <summary>
    /// The whole-block gate, and it pins EXACTLY TWO known divergences — both deliberate, both
    /// pre-dating this work, neither a loss of anything the block itself owns. Everything else,
    /// including all 16 subelement start values, the doubly-nested `TCON_IP_v4`, the
    /// `Array[…] of Struct` table and the multi-instance's `Version`, is equivalent.
    ///
    /// <para>Stated as a subtraction on purpose: each removal below is a CLAIM about why a
    /// difference is benign, and if a third one ever appears this test fails rather than absorbing
    /// it.</para>
    /// </summary>
    [Fact]
    public void WholeBlock_ToIrThenToXml_DiffersOnlyByTheTwoKnownDeliberateDivergences()
    {
        var original = LoadFixture();
        var regenerated = ToXml(ToIrText(original));

        // (1) The callee's inline-expanded interface under a multi-instance is DISCARDED BY DESIGN —
        // it is the CALLED block's own declaration, owned and re-emitted by TIA on every export, and
        // duplicating it into the caller would let the two disagree.
        var expansion = StaticMemberElement(original, "Server").Element(Ifc + "Sections");
        Assert.NotNull(expansion);
        expansion!.Remove();

        // (2) `Remanence` on a PARAMETER is never emitted (FI-59): TIA exports it but REFUSES it at
        // import — "The attribute 'Remanence' cannot be set" — because a parameter has no storage of
        // its own. Unlike (1) this is a divergence from TIA's export that is REQUIRED for the file to
        // import at all, so it cannot be closed without breaking the thing it protects.
        original.Descendants(Ifc + "Member").First(m => (string?)m.Attribute("Name") == "clear")
            .Attribute("Remanence")!.Remove();

        Assert.True(Normalizer.AreSemanticallyEquivalent(original, regenerated));
    }

    [Fact]
    public void WholeBlock_IrIsSelfStable()
    {
        var first = ToIrText(LoadFixture());
        var second = ToIrText(ToXml(first));

        Assert.Equal(first, second);
    }

    // ------------------------------------------------- THE GUARDS: widening stayed fail-closed

    /// <summary>
    /// The port list is NOT in the export — the converter supplies it per (name, version). Adding
    /// `MB_SERVER` 5.3 must not have made ANY `MB_SERVER` acceptable: applying 5.3's port template
    /// to another version's wiring imports cleanly and misbehaves on the controller, which is worse
    /// than a refusal.
    /// </summary>
    [Fact]
    public void MbServer_AtAnUnknownVersion_IsStillRefused()
    {
        var document = LoadFixture();
        document.Descendants().First(e => e.Name.LocalName == "Part" && (string?)e.Attribute("Name") == "MB_SERVER")
            .SetAttributeValue("Version", "5.2");

        var error = Assert.Throws<UnsupportedConstructException>(() => ToIrText(document));

        Assert.Contains("Unsupported version '5.2'", error.Message);
        Assert.Contains("5.3", error.Message);
    }

    [Fact]
    public void AnUnknownInstruction_IsStillRefused()
    {
        var document = LoadFixture();
        document.Descendants().First(e => e.Name.LocalName == "Part" && (string?)e.Attribute("Name") == "MB_SERVER")
            .SetAttributeValue("Name", "MB_NOT_A_REAL_INSTRUCTION");

        Assert.Throws<UnsupportedConstructException>(() => ToIrText(document));
    }

    /// <summary>
    /// Gap 1 widened the direct-nested-member gate from `Struct` to `Struct` OR `Array[…] of Struct`
    /// — and to nothing else. An array of a SCALAR carrying nested members is not a shape anything
    /// has produced, and accepting it would mean inventing a structure.
    /// </summary>
    [Fact]
    public void DirectNestedMembersUnderANonStructDatatype_AreStillRefused()
    {
        var document = LoadFixture();
        StaticMemberElement(document, "NodeTable").SetAttributeValue("Datatype", "Array[1..2] of Byte");

        var error = Assert.Throws<UnsupportedConstructException>(() => BlockSourceParser.Parse(document));

        Assert.Contains("Array[1..2] of Byte", error.Message);
    }

    /// <summary>
    /// Gap 2's third disposition must NOT have swallowed the second. An ANONYMOUS `Struct` nested
    /// inside a structured member has no named type to recover it from — its `&lt;Sections&gt;` is
    /// its only definition — so collapsing or mis-reading it would silently discard real members.
    /// </summary>
    [Fact]
    public void DoublyNestedAnonymousStruct_IsStillRefused()
    {
        var document = LoadFixture();
        StaticMemberElement(document, "RemoteAddress").SetAttributeValue("Datatype", "Struct");

        Assert.Throws<UnsupportedConstructException>(() => BlockSourceParser.Parse(document));
    }

    /// <summary>
    /// The guard the whole gap-5 story argues for: an UNRECOGNISED child element is now a named
    /// refusal rather than a silent drop. `&lt;Subelement&gt;` went unnoticed for as long as it did
    /// precisely because no such check existed.
    /// </summary>
    [Fact]
    public void AnUnrecognisedMemberChildElement_IsRefusedRatherThanIgnored()
    {
        var document = LoadFixture();
        StaticMemberElement(document, "HoldReg").Add(new XElement(Ifc + "SomethingNobodyHasModelled"));

        var error = Assert.Throws<UnsupportedConstructException>(() => BlockSourceParser.Parse(document));

        Assert.Contains("SomethingNobodyHasModelled", error.Message);
    }

    [Fact]
    public void ASubelementWithNoStartValue_IsRefusedRatherThanDropped()
    {
        var document = LoadFixture();
        StaticMemberElement(document, "HoldReg").Add(new XElement(Ifc + "Subelement", new XAttribute("Path", "3")));

        var error = Assert.Throws<UnsupportedConstructException>(() => BlockSourceParser.Parse(document));

        Assert.Contains("Subelement", error.Message);
    }
}
