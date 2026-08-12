using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

public class NormalizerTests
{
    [Fact]
    public void AreSemanticallyEquivalent_IdenticalDocuments_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><Name>Foo</Name></Block>");
        var b = XDocument.Parse("<Block><Name>Foo</Name></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingModifiedDate_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><Name>Foo</Name><ModifiedDate>2026-01-01T00:00:00Z</ModifiedDate></Block>");
        var b = XDocument.Parse("<Block><Name>Foo</Name><ModifiedDate>2026-07-10T09:30:00Z</ModifiedDate></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingCompileDate_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><CompileDate>2026-01-01T00:00:00Z</CompileDate></Block>");
        var b = XDocument.Parse("<Block><CompileDate>2026-07-10T09:30:00Z</CompileDate></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingHeaderVersion_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><HeaderVersion>0.1</HeaderVersion><Name>Foo</Name></Block>");
        var b = XDocument.Parse("<Block><HeaderVersion>0.2</HeaderVersion><Name>Foo</Name></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingActualContent_ReturnsFalse()
    {
        var a = XDocument.Parse("<Block><Name>Foo</Name></Block>");
        var b = XDocument.Parse("<Block><Name>Bar</Name></Block>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingWireUId_SameEndpoints_ReturnsTrue()
    {
        // Reverses the original assumption behind this test — disproven by real data,
        // 2026-07-10 (first live reference-project round-trip): TIA reassigns Wire UId on every
        // import/compile cycle regardless of what the source/sidecar held (the shared rail wire
        // went from UId 126 to a fresh 81, every other wire shifted too), even though the set of
        // connections it makes was byte-identical. A wire's own UId isn't part of its identity —
        // see docs/notes/openness-quirks.md.
        var a = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/><NameCon UId=\"5\" Name=\"in\"/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"99\"><Powerrail/><NameCon UId=\"5\" Name=\"in\"/></Wire></Wires>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingWireEndpoint_ReturnsFalse()
    {
        // What actually must never be normalized away: which Access/Part UId a wire connects to
        // — that's a real semantic drift (rewired logic), not a volatile ID. Only the wire's own
        // UId is volatile; the UIds it references are a different thing and stay significant.
        var a = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/><NameCon UId=\"5\" Name=\"in\"/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/><NameCon UId=\"6\" Name=\"in\"/></Wire></Wires>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_ReorderedWires_ReturnsTrue()
    {
        // <Wire> order within <Wires> isn't semantically meaningful either — confirmed real,
        // 2026-07-10, same round-trip: TIA relocates the shared rail wire earlier in the list on
        // re-export instead of leaving it last (where BlockSourceWriter puts it).
        var a = XDocument.Parse(
            "<Wires>" +
            "<Wire UId=\"1\"><NameCon UId=\"5\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"2\"><Powerrail/></Wire>" +
            "</Wires>");
        var b = XDocument.Parse(
            "<Wires>" +
            "<Wire UId=\"1\"><Powerrail/></Wire>" +
            "<Wire UId=\"2\"><NameCon UId=\"5\" Name=\"in\"/></Wire>" +
            "</Wires>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_CodeBlockInterface_IsStripped()
    {
        // Confirmed real, 2026-07-10: BlockSourceParser.RequireDefaultInterface already
        // hard-errors upstream if a code block's Interface is anything but the standard
        // parameterless-FC boilerplate, so by the time a document reaches here it's known-inert
        // — safe to strip, same as it's always been.
        var a = XDocument.Parse("<SW.Blocks.FC><AttributeList><Interface><Sections><Section Name=\"Input\"/></Sections></Interface></AttributeList></SW.Blocks.FC>");
        var b = XDocument.Parse("<SW.Blocks.FC><AttributeList><Interface><Sections><Section Name=\"Output\"/></Sections></Interface></AttributeList></SW.Blocks.FC>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DbInterface_IsNotStripped()
    {
        // The bug this guards against, caught live 2026-07-10 building DB support: a blanket
        // "Interface" strip (correct for code blocks) would make a DB round-trip trivially pass
        // without ever comparing its actual member declarations — a DB's Interface always has a
        // "Static" Section (a code block's never does), which is what distinguishes the two.
        var a = XDocument.Parse("<SW.Blocks.GlobalDB><AttributeList><Interface><Sections><Section Name=\"Static\"><Member Name=\"Foo\"/></Section></Sections></Interface></AttributeList></SW.Blocks.GlobalDB>");
        var b = XDocument.Parse("<SW.Blocks.GlobalDB><AttributeList><Interface><Sections><Section Name=\"Static\"><Member Name=\"Bar\"/></Section></Sections></Interface></AttributeList></SW.Blocks.GlobalDB>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingPartUId_SameTopology_ReturnsTrue()
    {
        // Confirmed real, 2026-07-14 (full export/convert/import/compile/re-export cycle against
        // every SampleProject block, memory note "Part UId volatility, Normalizer gap"): TIA
        // reassigns a Part's own UId too, not just Wire/Access. Contact(31)/Coil(32) here become
        // Contact(50)/Coil(51) in "b", with every Wire/Access UId also renumbered around them —
        // same topology throughout, so this must still compare equal.
        var a = XDocument.Parse(
            "<FlgNet><Parts>" +
            "<Access Scope=\"GlobalVariable\" UId=\"21\"><Symbol><Component Name=\"Sensor1\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"22\"><Symbol><Component Name=\"Output1\"/></Symbol></Access>" +
            "<Part Name=\"Contact\" UId=\"31\"/><Part Name=\"Coil\" UId=\"32\"/>" +
            "</Parts><Wires>" +
            "<Wire UId=\"41\"><Powerrail/><NameCon UId=\"31\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"42\"><IdentCon UId=\"21\"/><NameCon UId=\"31\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"43\"><NameCon UId=\"31\" Name=\"out\"/><NameCon UId=\"32\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"44\"><IdentCon UId=\"22\"/><NameCon UId=\"32\" Name=\"operand\"/></Wire>" +
            "</Wires></FlgNet>");
        var b = XDocument.Parse(
            "<FlgNet><Parts>" +
            "<Access Scope=\"GlobalVariable\" UId=\"60\"><Symbol><Component Name=\"Sensor1\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"61\"><Symbol><Component Name=\"Output1\"/></Symbol></Access>" +
            "<Part Name=\"Contact\" UId=\"50\"/><Part Name=\"Coil\" UId=\"51\"/>" +
            "</Parts><Wires>" +
            "<Wire UId=\"70\"><Powerrail/><NameCon UId=\"50\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"71\"><IdentCon UId=\"60\"/><NameCon UId=\"50\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"72\"><NameCon UId=\"50\" Name=\"out\"/><NameCon UId=\"51\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"73\"><IdentCon UId=\"61\"/><NameCon UId=\"51\" Name=\"operand\"/></Wire>" +
            "</Wires></FlgNet>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_TwoSameKindParts_SwappedNumbering_ReturnsTrue()
    {
        // The real test of graph-based identity, not just "renumbered in the same relative
        // order": two independent Contact->Coil chains, reading different tags and writing
        // different tags. In "b" the *numerically lower* UId now belongs to the chain that was
        // numerically higher in "a" — a naive "match by position in document order" heuristic
        // would get this wrong; only true topology (which Access each Contact/Coil is actually
        // wired to) identifies them correctly.
        var a = XDocument.Parse(
            "<FlgNet><Parts>" +
            "<Access Scope=\"GlobalVariable\" UId=\"21\"><Symbol><Component Name=\"TagA\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"22\"><Symbol><Component Name=\"OutA\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"23\"><Symbol><Component Name=\"TagB\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"24\"><Symbol><Component Name=\"OutB\"/></Symbol></Access>" +
            "<Part Name=\"Contact\" UId=\"31\"/><Part Name=\"Coil\" UId=\"32\"/>" +
            "<Part Name=\"Contact\" UId=\"33\"/><Part Name=\"Coil\" UId=\"34\"/>" +
            "</Parts><Wires>" +
            "<Wire UId=\"50\"><Powerrail/><NameCon UId=\"31\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"51\"><IdentCon UId=\"21\"/><NameCon UId=\"31\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"52\"><NameCon UId=\"31\" Name=\"out\"/><NameCon UId=\"32\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"53\"><IdentCon UId=\"22\"/><NameCon UId=\"32\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"54\"><Powerrail/><NameCon UId=\"33\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"55\"><IdentCon UId=\"23\"/><NameCon UId=\"33\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"56\"><NameCon UId=\"33\" Name=\"out\"/><NameCon UId=\"34\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"57\"><IdentCon UId=\"24\"/><NameCon UId=\"34\" Name=\"operand\"/></Wire>" +
            "</Wires></FlgNet>");
        var b = XDocument.Parse(
            "<FlgNet><Parts>" +
            "<Access Scope=\"GlobalVariable\" UId=\"21\"><Symbol><Component Name=\"TagA\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"22\"><Symbol><Component Name=\"OutA\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"23\"><Symbol><Component Name=\"TagB\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"24\"><Symbol><Component Name=\"OutB\"/></Symbol></Access>" +
            // Chain B (TagB/OutB) now gets the *lower* Part UIds (31/32); Chain A gets the higher ones (33/34) — reversed from "a".
            "<Part Name=\"Contact\" UId=\"33\"/><Part Name=\"Coil\" UId=\"34\"/>" +
            "<Part Name=\"Contact\" UId=\"31\"/><Part Name=\"Coil\" UId=\"32\"/>" +
            "</Parts><Wires>" +
            "<Wire UId=\"50\"><Powerrail/><NameCon UId=\"33\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"51\"><IdentCon UId=\"21\"/><NameCon UId=\"33\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"52\"><NameCon UId=\"33\" Name=\"out\"/><NameCon UId=\"34\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"53\"><IdentCon UId=\"22\"/><NameCon UId=\"34\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"54\"><Powerrail/><NameCon UId=\"31\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"55\"><IdentCon UId=\"23\"/><NameCon UId=\"31\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"56\"><NameCon UId=\"31\" Name=\"out\"/><NameCon UId=\"32\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"57\"><IdentCon UId=\"24\"/><NameCon UId=\"32\" Name=\"operand\"/></Wire>" +
            "</Wires></FlgNet>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_SameKindPartsDifferentWiring_ReturnsFalse()
    {
        // Guards against over-normalizing into a false positive: same Part kinds and count as
        // "a", but Contact(31) is now wired to a *different* tag (TagB instead of TagA) — a real
        // rewire, not a volatile-numbering artifact, and must still be reported as a difference.
        var a = XDocument.Parse(
            "<FlgNet><Parts>" +
            "<Access Scope=\"GlobalVariable\" UId=\"21\"><Symbol><Component Name=\"TagA\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"22\"><Symbol><Component Name=\"TagB\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"23\"><Symbol><Component Name=\"Output1\"/></Symbol></Access>" +
            "<Part Name=\"Contact\" UId=\"31\"/><Part Name=\"Coil\" UId=\"32\"/>" +
            "</Parts><Wires>" +
            "<Wire UId=\"41\"><Powerrail/><NameCon UId=\"31\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"42\"><IdentCon UId=\"21\"/><NameCon UId=\"31\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"43\"><NameCon UId=\"31\" Name=\"out\"/><NameCon UId=\"32\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"44\"><IdentCon UId=\"23\"/><NameCon UId=\"32\" Name=\"operand\"/></Wire>" +
            "</Wires></FlgNet>");
        var b = XDocument.Parse(
            "<FlgNet><Parts>" +
            "<Access Scope=\"GlobalVariable\" UId=\"21\"><Symbol><Component Name=\"TagA\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"22\"><Symbol><Component Name=\"TagB\"/></Symbol></Access>" +
            "<Access Scope=\"GlobalVariable\" UId=\"23\"><Symbol><Component Name=\"Output1\"/></Symbol></Access>" +
            "<Part Name=\"Contact\" UId=\"31\"/><Part Name=\"Coil\" UId=\"32\"/>" +
            "</Parts><Wires>" +
            "<Wire UId=\"41\"><Powerrail/><NameCon UId=\"31\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"42\"><IdentCon UId=\"22\"/><NameCon UId=\"31\" Name=\"operand\"/></Wire>" +
            "<Wire UId=\"43\"><NameCon UId=\"31\" Name=\"out\"/><NameCon UId=\"32\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"44\"><IdentCon UId=\"23\"/><NameCon UId=\"32\" Name=\"operand\"/></Wire>" +
            "</Wires></FlgNet>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    // ---- Wire endpoint ORDER carries DIRECTION (2026-08-12) ----------------------------------
    // A wire's first endpoint is its PRODUCER and the rest are its CONSUMERS, and NOTHING ELSE in
    // the document says so — no attribute, no child element. `Strip` used to sort a <Wire>'s whole
    // endpoint list, which normalized an input wire and an output wire on the same port to the
    // same thing; `compare`, `drift-check` and the `--no-sidecar` derivability check all ran
    // through it and all inherited the blindness.

    [Fact]
    public void AreSemanticallyEquivalent_SwappedIdentConNameCon_DirectionFlip_ReturnsFalse()
    {
        // THE GUARD. Two wires with the IDENTICAL endpoint set, differing only in which endpoint
        // is listed first: `<IdentCon>` first means the Access drives the port (an INPUT),
        // `<NameCon>` first means the port drives the Access (an OUTPUT). Reversing them reverses
        // the flow of data through that port, so these must never compare equal.
        var a = XDocument.Parse("<Wires><Wire UId=\"41\"><IdentCon UId=\"21\"/><NameCon UId=\"31\" Name=\"P\"/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"41\"><NameCon UId=\"31\" Name=\"P\"/><IdentCon UId=\"21\"/></Wire></Wires>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_CallParameterDirectionFlip_ReturnsFalse()
    {
        // The case the whole fix exists for. On an instruction port the direction survives the old
        // sort anyway, because a port name is conventionally always-input or always-output
        // (measured: across all 34 real TIA exports in simatic-ml/, all 106 (part, port) pairs sit
        // exclusively at index 0 or exclusively in the tail, never both). A CALL's parameter names
        // are block-author-chosen and carry no such convention, and `ir/SPEC.md` §Interface records
        // that a call site does not mark a parameter Input/Output/InOut at all — so for the SAME
        // parameter name, endpoint order is the only thing distinguishing "the callee reads this
        // tag" from "the callee writes this tag", and it cannot distinguish an input from an InOut
        // at all.
        var a = XDocument.Parse("<Wires><Wire UId=\"70\"><IdentCon UId=\"9\"/><NameCon UId=\"60\" Name=\"Setpoint\"/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"70\"><NameCon UId=\"60\" Name=\"Setpoint\"/><IdentCon UId=\"9\"/></Wire></Wires>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_ReorderedFanoutConsumers_ReturnsTrue()
    {
        // The 2026-07-14 measurement the endpoint sort was added for, and it still holds. A
        // converter-only round trip (to-ir -> to-xml, no live TIA) of MotorStarter reported a false
        // mismatch purely from two electrically-identical wires listing the same endpoints in a
        // different order (docs/evidence/stage-S1.md, "S1 item 26 continued again"). This is that
        // block's own real shared rail wire — one producer, four consumers — with the CONSUMERS
        // permuted. A wire has exactly one producer, so any reordering that preserves the endpoint
        // set can only ever permute the tail, which is why sorting the tail alone still collapses
        // this while leaving the producer position meaningful.
        var a = XDocument.Parse(
            "<Wires><Wire UId=\"60\">" +
            "<Powerrail/>" +
            "<NameCon UId=\"39\" Name=\"in\"/><NameCon UId=\"40\" Name=\"in\"/>" +
            "<NameCon UId=\"44\" Name=\"in\"/><NameCon UId=\"52\" Name=\"in\"/>" +
            "</Wire></Wires>");
        var b = XDocument.Parse(
            "<Wires><Wire UId=\"60\">" +
            "<Powerrail/>" +
            "<NameCon UId=\"52\" Name=\"in\"/><NameCon UId=\"44\" Name=\"in\"/>" +
            "<NameCon UId=\"40\" Name=\"in\"/><NameCon UId=\"39\" Name=\"in\"/>" +
            "</Wire></Wires>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_ReorderedFanoutConsumers_PartOutputProducer_ReturnsTrue()
    {
        // Same fan-out tolerance where the producer is a Part output port rather than the rail —
        // MotorStarter's own Move-tap/OR-merge shape, the one the 2026-07-14 note calls out.
        var a = XDocument.Parse(
            "<Wires><Wire UId=\"71\">" +
            "<NameCon UId=\"52\" Name=\"out\"/>" +
            "<NameCon UId=\"53\" Name=\"in\"/><NameCon UId=\"55\" Name=\"in\"/>" +
            "</Wire></Wires>");
        var b = XDocument.Parse(
            "<Wires><Wire UId=\"71\">" +
            "<NameCon UId=\"52\" Name=\"out\"/>" +
            "<NameCon UId=\"55\" Name=\"in\"/><NameCon UId=\"53\" Name=\"in\"/>" +
            "</Wire></Wires>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_FanoutProducerDemotedIntoConsumers_ReturnsFalse()
    {
        // The two halves together: identical endpoint SET, tail permuted AND the producer moved
        // out of index 0. Tolerating the permutation must not tolerate the demotion — this is the
        // multi-endpoint form of the direction flip, and it is what a blanket sort could not see.
        var a = XDocument.Parse(
            "<Wires><Wire UId=\"71\">" +
            "<NameCon UId=\"52\" Name=\"out\"/>" +
            "<NameCon UId=\"53\" Name=\"in\"/><NameCon UId=\"55\" Name=\"in\"/>" +
            "</Wire></Wires>");
        var b = XDocument.Parse(
            "<Wires><Wire UId=\"71\">" +
            "<NameCon UId=\"53\" Name=\"in\"/>" +
            "<NameCon UId=\"52\" Name=\"out\"/><NameCon UId=\"55\" Name=\"in\"/>" +
            "</Wire></Wires>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void Strip_RemovesOnlyVolatileElements_LeavesStructureIntact()
    {
        var element = XElement.Parse("<Block><Name>Foo</Name><ModifiedDate>x</ModifiedDate><Number>2</Number></Block>");

        var stripped = Normalizer.Strip(element);

        Assert.Null(stripped.Element("ModifiedDate"));
        Assert.Equal("Foo", stripped.Element("Name")?.Value);
        Assert.Equal("2", stripped.Element("Number")?.Value);
    }
}
