using System.Xml.Linq;
using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// <c>data-hmi-set</c> - a SINGLE staged operand write - and <c>data-hmi-string</c> - an IOField
/// showing a String tag. Both landed 2026-08-18 for the JOB9004 recipe chooser.
///
/// The tests that matter here are the REFUSALS, not the emissions. A staged write is one
/// <c>SetTag</c> away from being a hand-rolled command channel write in an author-chosen order,
/// which is precisely what <c>data-hmi-cmd</c> exists to make inexpressible; and a string field is
/// one disagreeing attribute away from the crash family that has already cost this project a Portal
/// session. So each guard is asserted to FIRE, and the emission tests are the controls that prove
/// the guards are not simply refusing everything.
/// </summary>
public class StagedWriteTests
{
    private static IrItem Button(string? set = null, string? cmd = null, string? code = null, string? goTo = null) => new()
    {
        Type = "Button",
        Left = 100, Top = 100, Width = 96, Height = 51,
        SetTag = set,
        Cmd = cmd,
        CmdCode = code,
        GoTo = goTo,
        Text = "SELECT",
        FontSizePx = 17,
    };

    private static IrItem Field(string? bind = "Recipe_01_Name", string? stringLength = null, string? format = null, string? type = "IOField") => new()
    {
        Type = type,
        Left = 100, Top = 200, Width = 250, Height = 34,
        Bind = bind,
        StringLength = stringLength,
        Format = format,
        FontSizePx = 14,
    };

    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700",
        CanvasWidth = 800,
        CanvasHeight = 480,
        Items = items.ToList(),
    };

    private static XDocument Emit(ScreenIr ir) => XDocument.Parse(Emitter.Emit(ir, "TestScreen", 1).Xml);

    private static List<XElement> Functions(XDocument d) =>
        d.Descendants().Where(e => e.Name.LocalName == "Hmi.Event.FunctionListEntry").ToList();

    private static string? FunctionName(XElement f) =>
        f.Element("AttributeList")?.Element("Name")?.Value;

    // ---- what it emits ------------------------------------------------------------------------

    [Fact]
    public void A_staged_write_emits_one_SetTag_and_NO_sequence_bump()
    {
        var doc = Emit(Ir(Button(set: "Cmd_Silo_W_Int1=@Recipe_01_SRID")));

        var functions = Functions(doc);
        Assert.Single(functions);
        Assert.Equal("SetTag", FunctionName(functions[0]));

        // THE ABSENCE IS THE POINT. An IncreaseTag here would make this press a command.
        Assert.DoesNotContain(functions, f => FunctionName(f) == "IncreaseTag");
    }

    [Fact]
    public void A_staged_write_carrying_an_at_sign_emits_a_LINK_and_not_a_literal()
    {
        var doc = Emit(Ir(Button(set: "Cmd_Silo_W_Int1=@Recipe_01_SRID")));

        var parameters = doc.Descendants().Where(e => e.Name.LocalName == "Hmi.Event.FunctionListEntryParameter").ToList();
        var value = parameters.Single(p => p.Element("AttributeList")?.Element("Name")?.Value == "Value");

        var link = value.Descendants().FirstOrDefault(e => e.Name.LocalName == "Value" && e.Attribute("TargetID") is not null);
        Assert.NotNull(link);
        Assert.Equal("Recipe_01_SRID", link!.Element("Name")?.Value);
    }

    [Fact]
    public void A_staged_write_is_emitted_BEFORE_the_navigation_on_the_same_button()
    {
        var doc = Emit(Ir(Button(set: "Cmd_Silo_W_Int1=@Recipe_01_SRID", goTo: "02 Silo Detail W")));

        var names = Functions(doc).Select(FunctionName).ToList();
        Assert.Equal(new[] { "SetTag", "ActivateScreen" }, names);
    }

    [Fact]
    public void A_button_carrying_only_a_staged_write_is_NOT_reported_inert()
    {
        var result = Emitter.Emit(Ir(Button(set: "Cmd_Silo_W_Int1=5")), "TestScreen", 1);
        Assert.DoesNotContain(result.HandOff, h => h.Contains("INERT"));
    }

    // ---- what it refuses ----------------------------------------------------------------------

    [Fact]
    public void A_staged_write_aimed_at_a_sequence_tag_is_REFUSED()
    {
        var ex = Assert.Throws<OperandStagingException>(() => Emit(Ir(Button(set: "Cmd_Silo_W_Seq=1"))));
        Assert.Contains("_Seq", ex.Message);
    }

    [Fact]
    public void A_staged_write_aimed_at_a_code_tag_is_REFUSED()
    {
        Assert.Throws<OperandStagingException>(() => Emit(Ir(Button(set: "Cmd_Silo_W_Code=12"))));
    }

    [Fact]
    public void A_staged_write_on_a_button_that_also_commands_is_REFUSED()
    {
        Assert.Throws<OperandStagingException>(() =>
            Emit(Ir(Button(set: "Cmd_Silo_W_Int1=3", cmd: "Cmd_Silo_W", code: "12"))));
    }

    [Fact]
    public void A_staged_write_with_no_equals_sign_is_REFUSED()
    {
        Assert.Throws<OperandStagingException>(() => Emit(Ir(Button(set: "Cmd_Silo_W_Int1"))));
    }

    [Fact]
    public void A_staged_write_on_something_that_is_not_a_button_is_REFUSED()
    {
        var text = new IrItem
        {
            Type = "Text", Left = 10, Top = 10, Width = 100, Height = 20,
            SetTag = "Cmd_Silo_W_Int1=3", Text = "X", FontSizePx = 14,
        };
        Assert.Throws<OperandStagingException>(() => Emit(Ir(text)));
    }

    // ---- the string field ---------------------------------------------------------------------

    [Fact]
    public void A_string_field_emits_DataFormat_String_and_a_question_mark_pattern_whose_length_is_FieldLength()
    {
        var doc = Emit(Ir(Field(stringLength: "32")));

        var attrs = doc.Descendants().First(e => e.Name.LocalName == "Hmi.Screen.IOField").Element("AttributeList")!;
        Assert.Equal("String", attrs.Element("DataFormat")?.Value);
        Assert.Equal(new string('?', 32), attrs.Element("FormatPattern")?.Value);
        Assert.Equal("32", attrs.Element("FieldLength")?.Value);

        // Words read from the left. A number would be right-aligned here.
        Assert.Equal("Left", attrs.Element("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void A_numeric_field_is_unchanged_by_the_string_path()
    {
        var doc = Emit(Ir(Field(format: "999.9")));

        var attrs = doc.Descendants().First(e => e.Name.LocalName == "Hmi.Screen.IOField").Element("AttributeList")!;
        Assert.Equal("Decimal", attrs.Element("DataFormat")?.Value);
        Assert.Equal("999.9", attrs.Element("FormatPattern")?.Value);
        Assert.Equal("5", attrs.Element("FieldLength")?.Value);
        Assert.Equal("Right", attrs.Element("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void A_field_declaring_BOTH_a_string_length_and_a_numeric_format_is_REFUSED()
    {
        var ex = Assert.Throws<StringFieldException>(() => Emit(Ir(Field(stringLength: "32", format: "9999"))));
        Assert.Contains("crashes the Portal process", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_string_length_outside_1_to_255_is_REFUSED()
    {
        Assert.Throws<StringFieldException>(() => Emit(Ir(Field(stringLength: "0"))));
        Assert.Throws<StringFieldException>(() => Emit(Ir(Field(stringLength: "256"))));
        Assert.Throws<StringFieldException>(() => Emit(Ir(Field(stringLength: "thirty-two"))));
    }

    [Fact]
    public void A_string_length_on_something_that_is_not_an_IOField_is_REFUSED()
    {
        Assert.Throws<StringFieldException>(() => Emit(Ir(Field(stringLength: "32", type: "Text"))));
    }

    // ---- the coherence gate, negative-tested --------------------------------------------------

    private const string Skeleton =
        "<Document><Hmi.Screen.Screen ID=\"0\"><ObjectList>" +
        "<Hmi.Screen.IOField ID=\"1\" CompositionName=\"ScreenItems\"><AttributeList>" +
        "<DataFormat>{0}</DataFormat><FieldLength>{1}</FieldLength><FormatPattern>{2}</FormatPattern>" +
        "<Height>34</Height><Left>10</Left><ObjectName>F</ObjectName><Top>10</Top><Width>80</Width>" +
        "</AttributeList></Hmi.Screen.IOField></ObjectList></Hmi.Screen.Screen></Document>";

    [Fact]
    public void The_coherence_gate_CATCHES_a_FieldLength_that_disagrees_with_its_pattern()
    {
        // Hand-built, because the emitter can no longer produce this - which is exactly why the gate
        // has to be tested against a document rather than through the emitter.
        var xml = string.Format(Skeleton, "Decimal", "4", "9999.9");
        Assert.Contains(Coherence.Check(xml, 800, 480), f => f.RuleId == "C-IOFIELD");
    }

    [Fact]
    public void The_coherence_gate_CATCHES_a_String_DataFormat_carrying_a_numeric_pattern()
    {
        var xml = string.Format(Skeleton, "String", "4", "9999");
        Assert.Contains(Coherence.Check(xml, 800, 480), f => f.RuleId == "C-IOFIELD");
    }

    [Fact]
    public void The_coherence_gate_CATCHES_a_Decimal_DataFormat_carrying_a_question_mark_pattern()
    {
        var xml = string.Format(Skeleton, "Decimal", "4", "????");
        Assert.Contains(Coherence.Check(xml, 800, 480), f => f.RuleId == "C-IOFIELD");
    }

    [Fact]
    public void The_coherence_gate_PASSES_a_well_formed_string_field()
    {
        var xml = Emitter.Emit(Ir(Field(stringLength: "32")), "TestScreen", 1).Xml;
        Assert.DoesNotContain(Coherence.Check(xml, 800, 480), f => f.RuleId == "C-IOFIELD");
    }
}
