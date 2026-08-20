using System.Xml.Linq;
using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// T8 TO-IR - the reverse path, added 2026-08-20.
///
/// It exists because the pipeline was one-way: a person edits a screen in TIA Portal, and their
/// work could only be captured as raw SimaticML with no route back into our source form and no
/// structural diff against the HTML it came from. That gap had already cost a real finding - a
/// screen came back with two dozen rebindings and the comparator named none of them.
///
/// The bar here is ROUND-TRIP FIDELITY, not completeness: <c>emit(to-ir(x)) == x</c> for everything
/// the emitter can express. Anything outside that must be a NAMED REFUSAL, because the failure this
/// project keeps meeting is a green over an incomplete denominator - and a reader that quietly
/// dropped an item type would produce exactly one.
///
/// 🔴 NO FIXTURE HERE COMES FROM A SITE JOB. Every document under test is built in this file
/// from invented names. The real corpus was used to derive the mappings and is cited in the
/// comments by SHAPE only.
/// </summary>
public class ToIrTests
{
    // ---- fixtures -----------------------------------------------------------------------------

    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700",
        CanvasWidth = 800,
        CanvasHeight = 480,
        Items = items.ToList(),
    };

    private static string Emit(ScreenIr ir, string screen = "TestScreen") =>
        Emitter.Emit(ir, screen, 1).Xml;

    private static SimaticMlReader.ReadResult Read(string xml, string? panel = null) =>
        SimaticMlReader.Read(xml, "test.xml", panel);

    /// <summary>
    /// One item of every type the emitter supports, each with an id so object names are stable, and
    /// each carrying whatever that type needs to be emittable at all.
    /// </summary>
    private static ScreenIr EveryType() => Ir(
        new IrItem
        {
            Type = "Rectangle", ElementId = "panel-frame",
            Left = 4, Top = 4, Width = 300, Height = 60,
            BackColor = "rgb(150, 150, 150)", BorderColor = "rgb(90, 90, 90)",
        },
        new IrItem
        {
            Type = "Text", ElementId = "caption",
            Left = 10, Top = 10, Width = 200, Height = 24,
            Text = "VESSEL ONE", FontSizePx = 17, FontWeight = 700,
            ForeColor = "rgb(20, 20, 20)", BackColor = "rgb(182, 182, 182)",
        },
        new IrItem
        {
            Type = "Button", ElementId = "btn-open",
            Left = 10, Top = 100, Width = 120, Height = 51,
            Text = "OPEN", FontSizePx = 15,
            Cmd = "CmdVessel", CmdCode = "7", CmdInt1 = "3", CmdReal1 = "@LiveSetpoint",
            GoTo = "OtherScreen",
        },
        new IrItem
        {
            Type = "Button", ElementId = "btn-stage",
            Left = 140, Top = 100, Width = 120, Height = 51,
            Text = "STAGE", FontSizePx = 15,
            SetTag = "StagedNumber=@ChosenNumber",
        },
        new IrItem
        {
            Type = "Line", ElementId = "divider",
            Left = 10, Top = 170, Width = 200, Height = 40,
            BackColor = "rgb(0, 0, 0)", LineDirection = "up",
        },
        new IrItem
        {
            Type = "Circle", ElementId = "lamp",
            Left = 300, Top = 170, Width = 40, Height = 40,
            BackColor = "rgb(90, 160, 90)", BorderColor = "rgb(0, 0, 0)",
        },
        new IrItem
        {
            Type = "IOField", ElementId = "fld-level",
            Left = 10, Top = 230, Width = 96, Height = 34,
            Bind = "VesselLevel", Format = "999.9", Unit = "m", Mode = "Output", FontSizePx = 14,
        },
        new IrItem
        {
            Type = "IOField", ElementId = "fld-note",
            Left = 120, Top = 230, Width = 200, Height = 34,
            Bind = "VesselNote", StringLength = "16", Mode = "Output", FontSizePx = 14,
        },
        new IrItem
        {
            Type = "SymbolicIOField", ElementId = "fld-state",
            Left = 10, Top = 280, Width = 160, Height = 34,
            Bind = "VesselState", TextList = "TL_State", Mode = "Output", FontSizePx = 14,
        },
        new IrItem
        {
            Type = "AlarmPlaceholder", ElementId = "alarm-box",
            Left = 400, Top = 280, Width = 300, Height = 120, FontSizePx = 14,
        });

    // ---- the round trip -----------------------------------------------------------------------

    /// <summary>
    /// THE TEST THIS VERB EXISTS FOR. Emit a document, read it back, emit again, and put the two
    /// documents through <see cref="ScreenCompare"/> - which now walks properties, events and
    /// animations, so it is a real check rather than a geometry check wearing its name.
    ///
    /// Asserting against ScreenCompare rather than against a string equality is deliberate: the
    /// object IDs are reassigned on the second pass (they are positional hex counters), so the two
    /// documents are NOT byte-identical and never will be. What must hold is that every object
    /// pairs by name and every stated field agrees.
    /// </summary>
    [Fact]
    public void Every_supported_type_survives_emit_to_ir_emit_unchanged()
    {
        var first = Emit(EveryType());
        var read = Read(first, "KTP700");
        var second = Emit(read.Ir);

        var cmp = ScreenCompare.Compare(first, second);

        Assert.False(cmp.NothingCompared);
        Assert.Empty(cmp.Changed);
        Assert.Empty(cmp.Dropped);

        // The denominator, asserted rather than assumed: a comparison of two empty documents also
        // reports zero differences, and this repo has been caught by that five times elsewhere.
        Assert.True(cmp.FieldsComparedOnBothSides > 200,
            $"only {cmp.FieldsComparedOnBothSides} fields compared - too few to mean anything");
    }

    /// <summary>
    /// The round trip of a document this tool emitted must leave NOTHING unmapped. If it does, the
    /// emitter can write a construct the reader cannot read - which is the two halves drifting
    /// apart, and the report would be the only place it showed.
    /// </summary>
    [Fact]
    public void Reading_our_own_document_reports_nothing_unmapped_and_nothing_unrecognised()
    {
        var read = Read(Emit(EveryType()));

        Assert.Empty(read.Report.NotRecognised);
        Assert.Empty(read.Report.Unmapped);

        // The AlarmPlaceholder is the one type that does not count 1:1 - it is emitted as a
        // Rectangle plus a labelled TextField, so ten IR items become eleven objects.
        Assert.Equal(11, read.Report.ObjectsFound);
        Assert.Equal(11, read.Report.RecognisedCount);
    }

    [Fact]
    public void The_ir_is_marked_as_partial_and_lists_what_it_could_not_fill()
    {
        var read = Read(Emit(EveryType()));

        Assert.Equal("simaticml", read.Ir.SourceKind);
        Assert.NotEmpty(read.Ir.Unpopulatable);

        // The forward path must stay distinguishable from it by reading the artifact alone.
        Assert.Equal("html", new ScreenIr().SourceKind);
    }

    // ---- the denominator ----------------------------------------------------------------------

    /// <summary>
    /// An item type this pipeline does not know is a NAMED REFUSAL and gates. A silent skip here
    /// would produce an IR that emits a screen missing content nobody was told about - the same
    /// failure the emitter's own UnsupportedItemTypeException exists to prevent, in the other
    /// direction.
    /// </summary>
    [Fact]
    public void An_unknown_item_type_is_named_not_skipped()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.GraphicView ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>pic-logo</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>50</Width><Height>50</Height>
                </AttributeList>
              </Hmi.Screen.GraphicView>
            """);

        var read = Read(xml);

        Assert.Empty(read.Ir.Items);
        Assert.Equal(1, read.Report.ObjectsFound);
        Assert.Equal(0, read.Report.RecognisedCount);
        var refusal = Assert.Single(read.Report.NotRecognised);
        Assert.Equal("pic-logo", refusal.Where);
        Assert.Contains("GraphicView", refusal.Detail);
    }

    /// <summary>
    /// 🔴 A SoftKey IS NOT UNDER "ScreenItems" AND THE OBVIOUS FILTER NEVER SEES IT.
    ///
    /// The physical bezel keys sit in composition "SoftKeys", directly on the screen rather than on
    /// the layer. A walk keyed on ScreenItems does not merely fail to read them - they vanish from
    /// the DENOMINATOR too, so the report would say "all N objects read" while N was wrong.
    /// </summary>
    [Fact]
    public void A_softkey_is_counted_and_refused_rather_than_invisible()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <Hmi.Screen.Screen ID="0">
                <AttributeList><Name>S</Name><Number>1</Number><Width>800</Width><Height>480</Height></AttributeList>
                <ObjectList>
                  <Hmi.Screen.SoftKey ID="1" CompositionName="SoftKeys">
                    <AttributeList><ObjectName>Softkey_F1</ObjectName></AttributeList>
                  </Hmi.Screen.SoftKey>
                </ObjectList>
              </Hmi.Screen.Screen>
            </Document>
            """;

        var read = Read(xml);

        Assert.Equal(1, read.Report.ObjectsFound);
        var refusal = Assert.Single(read.Report.NotRecognised);
        Assert.Contains("SoftKeys", refusal.Detail);
    }

    [Fact]
    public void A_document_with_no_objects_is_not_a_pass()
    {
        var read = Read(ScreenWith(string.Empty));

        Assert.True(read.Report.NothingRead);
        Assert.Equal(0, read.Report.ObjectsFound);
    }

    // ---- panel: declared, never inferred ------------------------------------------------------

    /// <summary>
    /// H-407 on the read path. The KTP700 and KTP900 Basic both render 800x480 and differ ~28%
    /// physically, so a panel inferred from the canvas is a quarter-scale sizing error that passes
    /// every pixel-based check in silence. The document states pixels; it does not state a panel.
    /// </summary>
    [Fact]
    public void The_panel_is_never_inferred_from_the_canvas_size()
    {
        var undeclared = Read(Emit(EveryType()));
        Assert.Equal(string.Empty, undeclared.Ir.Panel);

        var declared = Read(Emit(EveryType()), "KTP900 Basic");
        Assert.Equal("KTP900 Basic", declared.Ir.Panel);
    }

    // ---- events -------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 THE ORDER IS THE PLC HANDSHAKE. A command is the code (and operands) FIRST and the
    /// sequence bump LAST - the controller reads the code when the sequence CHANGES. The decoder
    /// recognises the shape from the end of the list backwards, so this asserts both that the
    /// channel comes back and that the emitted list is in the order the decoder relies on.
    /// </summary>
    [Fact]
    public void A_command_button_round_trips_channel_code_and_operands()
    {
        var xml = Emit(Ir(new IrItem
        {
            Type = "Button", ElementId = "btn-run",
            Left = 10, Top = 10, Width = 120, Height = 51, Text = "RUN", FontSizePx = 15,
            Cmd = "CmdVessel", CmdCode = "7", CmdInt1 = "3", CmdInt2 = "4",
            CmdReal1 = "1.5", CmdReal2 = "@LiveSetpoint",
        }));

        var item = Assert.Single(Read(xml).Ir.Items);

        Assert.Equal("CmdVessel", item.Cmd);
        Assert.Equal("7", item.CmdCode);
        Assert.Equal("3", item.CmdInt1);
        Assert.Equal("4", item.CmdInt2);
        Assert.Equal("1.5", item.CmdReal1);

        // A parameter is EITHER a typed literal in the AttributeList OR an @OpenLink in the
        // LinkList, and the leading @ is how the IR keeps a literal 3 and a tag named "3" apart. A
        // decoder that lost the mark would silently turn a live value into a constant.
        Assert.Equal("@LiveSetpoint", item.CmdReal2);

        // The bump itself is NOT an operand - it is the commit, and it must not reappear as one.
        Assert.DoesNotContain("_Seq", item.SetTag ?? string.Empty);
    }

    [Fact]
    public void The_function_list_puts_the_sequence_bump_last_and_navigation_after_it()
    {
        var xml = Emit(Ir(new IrItem
        {
            Type = "Button", ElementId = "btn-go",
            Left = 10, Top = 10, Width = 120, Height = 51, Text = "GO", FontSizePx = 15,
            Cmd = "CmdVessel", CmdCode = "7", GoTo = "OtherScreen",
        }));

        var names = XDocument.Parse(xml).Descendants()
            .Where(e => e.Name.LocalName == "Hmi.Event.FunctionListEntry")
            .Select(e => e.Element("AttributeList")!.Element("Name")!.Value)
            .ToList();

        Assert.Equal(new[] { "SetTag", "IncreaseTag", "ActivateScreen" }, names);
    }

    /// <summary>
    /// 🔴 WHERE A HAND-BUILT EVENT DOES NOT FIT THE AUTHORING ATTRIBUTES, IT IS REPORTED - NEVER
    /// FORCED. Mislabelling one is worse than reporting it unmapped, because a wrong label reads as
    /// knowledge and would emit a DIFFERENT program from the one on the panel with every gate green.
    ///
    /// The shape below is real in kind: a paging button stepping several index tags at once. It is
    /// not a command (no channel, no sequence bump) and not a staged write (not one write), and
    /// there is no honest way to squeeze it into either field.
    /// </summary>
    [Fact]
    public void An_event_that_does_not_match_our_shapes_is_reported_not_forced()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.Button ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>btn-page</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>60</Width><Height>51</Height>
                </AttributeList>
                <ObjectList>
                  <Hmi.Event.Event ID="A" CompositionName="Events">
                    <AttributeList><Name>Release</Name></AttributeList>
                    <ObjectList>
                      <Hmi.Event.FunctionListEventHandler ID="B" CompositionName="EventHandler">
                        <ObjectList>
                          <Hmi.Event.FunctionListEntry ID="C" CompositionName="FunctionListEntries">
                            <AttributeList><Name>IncreaseTag</Name><Type>SystemFunction</Type></AttributeList>
                            <ObjectList>
                              <Hmi.Event.FunctionListEntryParameter ID="D" CompositionName="Parameters">
                                <AttributeList><Name>Tag</Name></AttributeList>
                                <LinkList><Value TargetID="@OpenLink"><Name>IndexOne</Name></Value></LinkList>
                              </Hmi.Event.FunctionListEntryParameter>
                              <Hmi.Event.FunctionListEntryParameter ID="E" CompositionName="Parameters">
                                <AttributeList><Name>Value</Name><Value Type="System.Double">8</Value></AttributeList>
                              </Hmi.Event.FunctionListEntryParameter>
                            </ObjectList>
                          </Hmi.Event.FunctionListEntry>
                          <Hmi.Event.FunctionListEntry ID="F" CompositionName="FunctionListEntries">
                            <AttributeList><Name>IncreaseTag</Name><Type>SystemFunction</Type></AttributeList>
                            <ObjectList>
                              <Hmi.Event.FunctionListEntryParameter ID="10" CompositionName="Parameters">
                                <AttributeList><Name>Tag</Name></AttributeList>
                                <LinkList><Value TargetID="@OpenLink"><Name>IndexTwo</Name></Value></LinkList>
                              </Hmi.Event.FunctionListEntryParameter>
                              <Hmi.Event.FunctionListEntryParameter ID="11" CompositionName="Parameters">
                                <AttributeList><Name>Value</Name><Value Type="System.Double">8</Value></AttributeList>
                              </Hmi.Event.FunctionListEntryParameter>
                            </ObjectList>
                          </Hmi.Event.FunctionListEntry>
                        </ObjectList>
                      </Hmi.Event.FunctionListEventHandler>
                    </ObjectList>
                  </Hmi.Event.Event>
                </ObjectList>
              </Hmi.Screen.Button>
            """);

        var read = Read(xml);
        var item = Assert.Single(read.Ir.Items);

        // Nothing invented: the button comes back inert rather than wearing a label that is not true.
        Assert.Null(item.Cmd);
        Assert.Null(item.SetTag);
        Assert.Null(item.GoTo);

        var refusal = Assert.Single(read.Report.Unmapped);
        Assert.Equal("event", refusal.Kind);
        // The list is quoted VERBATIM and IN ORDER, so the reader of the report can act on it.
        Assert.Contains("IncreaseTag(Tag=@IndexOne, Value=8)", refusal.Detail);
        Assert.Contains("IncreaseTag(Tag=@IndexTwo, Value=8)", refusal.Detail);
    }

    /// <summary>
    /// A <c>Press</c> is a real event this pipeline cannot author. It is named rather than read as
    /// a Release: the two fire at different moments, and a touch that lands on the wrong control
    /// can still be cancelled before a Release but not before a Press.
    /// </summary>
    [Fact]
    public void An_event_that_is_not_Release_is_named()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.Button ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>btn-stop</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>60</Width><Height>51</Height>
                </AttributeList>
                <ObjectList>
                  <Hmi.Event.Event ID="A" CompositionName="Events">
                    <AttributeList><Name>Press</Name></AttributeList>
                    <ObjectList>
                      <Hmi.Event.FunctionListEventHandler ID="B" CompositionName="EventHandler">
                        <ObjectList>
                          <Hmi.Event.FunctionListEntry ID="C" CompositionName="FunctionListEntries">
                            <AttributeList><Name>SetBit</Name><Type>SystemFunction</Type></AttributeList>
                            <ObjectList>
                              <Hmi.Event.FunctionListEntryParameter ID="D" CompositionName="Parameters">
                                <AttributeList><Name>Tag</Name></AttributeList>
                                <LinkList><Value TargetID="@OpenLink"><Name>StopRequest</Name></Value></LinkList>
                              </Hmi.Event.FunctionListEntryParameter>
                            </ObjectList>
                          </Hmi.Event.FunctionListEntry>
                        </ObjectList>
                      </Hmi.Event.FunctionListEventHandler>
                    </ObjectList>
                  </Hmi.Event.Event>
                </ObjectList>
              </Hmi.Screen.Button>
            """);

        var refusal = Assert.Single(Read(xml).Report.Unmapped);
        Assert.Contains("\"Press\"", refusal.Detail);
        Assert.Contains("SetBit(Tag=@StopRequest)", refusal.Detail);
    }

    // ---- bindings -----------------------------------------------------------------------------

    [Fact]
    public void A_process_value_binding_comes_back_as_bind()
    {
        var item = Assert.Single(Read(Emit(Ir(new IrItem
        {
            Type = "IOField", ElementId = "fld", Left = 10, Top = 10, Width = 96, Height = 34,
            Bind = "VesselLevel", Format = "999.9", FontSizePx = 14,
        }))).Ir.Items);

        Assert.Equal("VesselLevel", item.Bind);
    }

    /// <summary>
    /// The IR's <c>bind</c> means <c>ProcessValue</c> and nothing else. Visible, Enabled and
    /// BackColor are all bindable the same way and have no IR field, so folding one into
    /// <c>bind</c> would move a binding from one property to another on the way out.
    /// </summary>
    [Fact]
    public void A_binding_on_a_property_other_than_ProcessValue_is_reported_not_folded_into_bind()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.Rectangle ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>box</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>60</Width><Height>60</Height>
                </AttributeList>
                <ObjectList>
                  <Hmi.Screen.Property ID="A" CompositionName="Properties">
                    <AttributeList><Name>Visible</Name></AttributeList>
                    <ObjectList>
                      <Hmi.Dynamic.TagConnectionDynamic ID="B" CompositionName="Dynamic">
                        <AttributeList><Indirect>false</Indirect></AttributeList>
                        <LinkList><Tag TargetID="@OpenLink"><Name>ShowBox</Name></Tag></LinkList>
                      </Hmi.Dynamic.TagConnectionDynamic>
                    </ObjectList>
                  </Hmi.Screen.Property>
                </ObjectList>
              </Hmi.Screen.Rectangle>
            """);

        var read = Read(xml);

        Assert.Null(Assert.Single(read.Ir.Items).Bind);
        var refusal = Assert.Single(read.Report.Unmapped);
        Assert.Equal("property", refusal.Kind);
        Assert.Contains("Visible", refusal.Detail);
        Assert.Contains("ShowBox", refusal.Detail);
    }

    /// <summary>
    /// ⚠️ THE SECOND LINK FORM. <c>TargetID="#8A9"</c> is an intra-document reference to another
    /// object's ID - what a multiplexed tag's index uses. A reader that only knew <c>@OpenLink</c>
    /// would report it as EMPTY, which reads as "unbound" and is the opposite of the truth. That
    /// mistake has already been made once here.
    /// </summary>
    [Fact]
    public void An_intra_document_link_resolves_to_the_named_object()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.IOField ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>fld</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>96</Width><Height>34</Height>
                  <FormatPattern>9999</FormatPattern><FieldLength>4</FieldLength><DataFormat>Decimal</DataFormat>
                </AttributeList>
                <ObjectList>
                  <Hmi.Screen.Property ID="A" CompositionName="Properties">
                    <AttributeList><Name>ProcessValue</Name></AttributeList>
                    <ObjectList>
                      <Hmi.Dynamic.TagConnectionDynamic ID="B" CompositionName="Dynamic">
                        <AttributeList><Indirect>false</Indirect></AttributeList>
                        <LinkList><Tag TargetID="#C" /></LinkList>
                      </Hmi.Dynamic.TagConnectionDynamic>
                    </ObjectList>
                  </Hmi.Screen.Property>
                </ObjectList>
              </Hmi.Screen.IOField>
              <Hmi.Screen.Rectangle ID="C" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>MuxIndex</ObjectName>
                  <Left>0</Left><Top>0</Top><Width>1</Width><Height>1</Height>
                </AttributeList>
              </Hmi.Screen.Rectangle>
            """);

        var field = Read(xml).Ir.Items.Single(i => i.ElementId == "fld");
        Assert.Equal("MuxIndex", field.Bind);
    }

    // ---- animations ---------------------------------------------------------------------------

    /// <summary>
    /// The construct the IR gained a field for on 2026-08-20. It is BEHAVIOUR: an object carrying
    /// one looks identical in every geometric check and is invisible to the operator most of the
    /// time. Read it, and emit it again unchanged.
    /// </summary>
    [Fact]
    public void A_visibility_animation_round_trips()
    {
        var source = Ir(new IrItem
        {
            Type = "IOField", ElementId = "fld", Left = 10, Top = 10, Width = 96, Height = 34,
            Bind = "VesselLevel", Format = "9999", FontSizePx = 14,
            Visibility = new IrVisibility
            {
                Tag = "PageIndex", RangeStart = "47", RangeEnd = "100", Visible = false,
            },
        });

        var first = Emit(source);
        var read = Read(first);
        var item = Assert.Single(read.Ir.Items);

        Assert.NotNull(item.Visibility);
        Assert.Equal("PageIndex", item.Visibility!.Tag);
        Assert.Equal("47", item.Visibility.RangeStart);
        Assert.Equal("100", item.Visibility.RangeEnd);
        Assert.False(item.Visibility.Visible);

        var cmp = ScreenCompare.Compare(first, Emit(read.Ir));
        Assert.Empty(cmp.Changed);
        Assert.Empty(cmp.Dropped);
    }

    /// <summary>
    /// A shape has no font and no text, so it is written as a bare AttributeList - which means the
    /// ObjectList that carries an animation has to be OPENED for it. Getting that wrong drops the
    /// animation silently, since there is no other reason for the element to exist.
    /// </summary>
    [Fact]
    public void A_shape_with_an_animation_gains_the_object_list_it_needs()
    {
        var xml = Emit(Ir(new IrItem
        {
            Type = "Rectangle", ElementId = "band", Left = 0, Top = 0, Width = 100, Height = 20,
            Visibility = new IrVisibility { Tag = "ShowBand", RangeStart = "1", RangeEnd = "1", Visible = true },
        }));

        Assert.Contains("Hmi.Dynamic.VisibilityAnimation", xml);
        Assert.Equal("ShowBand", Assert.Single(Read(xml).Ir.Items).Visibility!.Tag);
    }

    /// <summary>
    /// An animation with nothing to trigger it imports and then never fires, leaving the object at
    /// whatever visibility the panel settles on - which looks like a decision somebody made. Same
    /// family as the unbound IOField, refused for the same reason.
    /// </summary>
    [Fact]
    public void An_animation_with_no_trigger_tag_is_refused_at_emit()
    {
        Assert.Throws<UnboundAnimationException>(() => Emit(Ir(new IrItem
        {
            Type = "Rectangle", ElementId = "band", Left = 0, Top = 0, Width = 100, Height = 20,
            Visibility = new IrVisibility { Tag = "  ", RangeStart = "0", RangeEnd = "0" },
        })));
    }

    /// <summary>
    /// A colour-by-value-band animation is real - the third-party corpus uses ten of them - and the
    /// IR has no field for it. Named, so a re-emit losing it is not a surprise.
    /// </summary>
    [Fact]
    public void An_animation_the_ir_cannot_hold_is_named()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.Circle ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>lamp</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>40</Width><Height>40</Height><Radius>20</Radius>
                </AttributeList>
                <ObjectList>
                  <Hmi.Dynamic.RangeAppearanceAnimation ID="A" CompositionName="Animations">
                    <AttributeList><Name>RangeAppearanceAnimation</Name></AttributeList>
                  </Hmi.Dynamic.RangeAppearanceAnimation>
                </ObjectList>
              </Hmi.Screen.Circle>
            """);

        var refusal = Assert.Single(Read(xml).Report.Unmapped);
        Assert.Equal("animation", refusal.Kind);
        Assert.Contains("RangeAppearanceAnimation", refusal.Detail);
    }

    // ---- text ---------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 A BUTTON IS TWO-STATE AND THE IR HOLDS ONE CAPTION. Measured on a hand-edited screen: two
    /// paging buttons came back with a new TextOff and the original TextOn, left over from the
    /// object they were copied from. Reading TextOff and saying nothing would rewrite TextOn on the
    /// way out - a change to the screen made by the tool rather than by anybody.
    /// </summary>
    [Fact]
    public void A_button_whose_two_captions_differ_is_reported()
    {
        var xml = ScreenWith("""
              <Hmi.Screen.Button ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>btn</ObjectName>
                  <Left>10</Left><Top>10</Top><Width>60</Width><Height>51</Height>
                </AttributeList>
                <ObjectList>
                  <MultilingualText ID="A" CompositionName="TextOff">
                    <ObjectList><MultilingualTextItem ID="B" CompositionName="Items">
                      <AttributeList><Culture>en-US</Culture><Text>&lt;body&gt;&lt;p&gt;NEXT&lt;/p&gt;&lt;/body&gt;</Text></AttributeList>
                    </MultilingualTextItem></ObjectList>
                  </MultilingualText>
                  <MultilingualText ID="C" CompositionName="TextOn">
                    <ObjectList><MultilingualTextItem ID="D" CompositionName="Items">
                      <AttributeList><Culture>en-US</Culture><Text>&lt;body&gt;&lt;p&gt;SELECT&lt;/p&gt;&lt;/body&gt;</Text></AttributeList>
                    </MultilingualTextItem></ObjectList>
                  </MultilingualText>
                </ObjectList>
              </Hmi.Screen.Button>
            """);

        var read = Read(xml);

        // The off caption is taken - it is what the button shows at rest - and the divergence is
        // stated rather than absorbed.
        Assert.Equal("NEXT", Assert.Single(read.Ir.Items).Text);
        var refusal = Assert.Single(read.Report.Unmapped);
        Assert.Equal("text", refusal.Kind);
        Assert.Contains("SELECT", refusal.Detail);
    }

    /// <summary>
    /// The emitter writes the rich-text payload ESCAPED; TIA re-exports the same content as LIVE
    /// NESTED XML. Both forms carry the same words and both must read back the same.
    /// </summary>
    [Fact]
    public void Both_escaped_and_live_text_payloads_read_the_same()
    {
        string Screen(string payload) => ScreenWith($"""
              <Hmi.Screen.TextField ID="9" CompositionName="ScreenItems">
                <AttributeList>
                  <ObjectName>t</ObjectName>
                  <Left>0</Left><Top>0</Top><Width>50</Width><Height>20</Height>
                </AttributeList>
                <ObjectList>
                  <MultilingualText ID="A" CompositionName="Text">
                    <ObjectList><MultilingualTextItem ID="B" CompositionName="Items">
                      <AttributeList><Culture>en-US</Culture><Text>{payload}</Text></AttributeList>
                    </MultilingualTextItem></ObjectList>
                  </MultilingualText>
                </ObjectList>
              </Hmi.Screen.TextField>
            """);

        var escaped = Read(Screen("&lt;body&gt;&lt;p&gt;VESSEL ONE&lt;/p&gt;&lt;/body&gt;"));
        var live = Read(Screen("<body><p>VESSEL ONE</p></body>"));

        Assert.Equal("VESSEL ONE", Assert.Single(escaped.Ir.Items).Text);
        Assert.Equal("VESSEL ONE", Assert.Single(live.Ir.Items).Text);
        Assert.Empty(escaped.Report.Unmapped);
        Assert.Empty(live.Report.Unmapped);
    }

    // ---- the consumed-attribute table ---------------------------------------------------------

    /// <summary>
    /// The attribute-level denominator rests on a table that restates what the reader reads, so it
    /// can drift from it. This asserts every name in the table is one the emitter actually writes
    /// for that type - which catches a rename or a typo, the drift that would UNDERSTATE the gap
    /// and make the report quietly optimistic.
    ///
    /// ⚠️ It does not catch the other direction (a name read but not listed); the round-trip tests
    /// above are what stand behind the mapping itself.
    /// </summary>
    [Fact]
    public void Every_consumed_attribute_name_is_one_the_emitter_actually_writes()
    {
        var doc = XDocument.Parse(Emit(EveryType()));

        foreach (var (irType, names) in SimaticMlReader.ConsumedAttributeNames)
        {
            var element = irType == "Text" ? "Hmi.Screen.TextField" : "Hmi.Screen." + irType;
            var stated = doc.Descendants()
                .Where(e => e.Name.LocalName == element
                            && (string?)e.Attribute("CompositionName") == "ScreenItems")
                .SelectMany(e => e.Element("AttributeList")!.Elements().Select(x => x.Name.LocalName))
                .ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(stated);
            foreach (var n in names)
            {
                Assert.True(stated.Contains(n),
                    $"ConsumedAttributes[\"{irType}\"] lists \"{n}\", which the emitter does not write "
                    + "on that type - the attribute denominator is stale.");
            }
        }
    }

    // ---- helper -------------------------------------------------------------------------------

    /// <summary>A minimal classic screen wrapping <paramref name="items"/> in the real nesting -
    /// Screen -> ObjectList -> ScreenLayer -> ObjectList -> items.</summary>
    private static string ScreenWith(string items) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Document>
          <Engineering version="V20" />
          <Hmi.Screen.Screen ID="0">
            <AttributeList>
              <Name>TestScreen</Name><Number>1</Number><Width>800</Width><Height>480</Height>
            </AttributeList>
            <ObjectList>
              <Hmi.Screen.ScreenLayer ID="1" CompositionName="Layers">
                <AttributeList><Index>0</Index><Name /></AttributeList>
                <ObjectList>
        {items}
                </ObjectList>
              </Hmi.Screen.ScreenLayer>
            </ObjectList>
          </Hmi.Screen.Screen>
        </Document>
        """;
}
