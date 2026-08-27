using System.Xml.Linq;
using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// 🔴 AN OBJECT NAME IS AN IDENTITY, AND A POSITIONAL ONE IS NOT STABLE.
///
/// Names were purely positional — `Button_53` meant "the 53rd item emitted" — so inserting one
/// element near the top of a screen renumbered every object after it.
///
/// The cost, measured: comparing a screen against a re-emitted version of itself produced 32 CHANGED
/// and 2 DROPPED lines where the true count was ZERO. Worse, the orchestrator read that as
/// "TIA renumbers objects when a human edits a screen" and reported it as a platform finding. It was
/// our own emitter. **A misleading identity produced a false conclusion about the platform**, which
/// is a more expensive kind of wrong than a noisy diff.
/// </summary>
public class StableNameTests
{
    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700", CanvasWidth = 800, CanvasHeight = 480, Items = items.ToList(),
    };

    private static IrItem Box(string? id = null, string text = "X") => new()
    {
        Type = "Text", Left = 10, Top = 10, Width = 90, Height = 20,
        Text = text, FontSizePx = 14, ElementId = id,
    };

    private static List<string> NamesOf(ScreenIr ir) =>
        XDocument.Parse(Emitter.Emit(ir, "S", 1).Xml)
            .Descendants().Where(e => e.Name.LocalName == "ObjectName")
            .Select(e => e.Value).ToList();

    /// <summary>
    /// The regression this exists for: an element inserted ABOVE a named one must not change its
    /// name. Without an id, it would.
    /// </summary>
    [Fact]
    public void An_id_survives_an_element_being_inserted_above_it()
    {
        var before = NamesOf(Ir(Box(), Box("weight-value")));
        var after = NamesOf(Ir(Box(), Box(), Box("weight-value")));

        Assert.Contains("weight-value", before);
        Assert.Contains("weight-value", after);
    }

    /// <summary>
    /// The control that shows the defect is real: WITHOUT an id, the same insertion does shift the
    /// name. If this ever stops being true the rule above has become unnecessary rather than
    /// satisfied, and the difference matters.
    /// </summary>
    [Fact]
    public void Without_an_id_the_positional_name_still_shifts()
    {
        var before = NamesOf(Ir(Box(), Box(text: "target")));
        var after = NamesOf(Ir(Box(), Box(), Box(text: "target")));

        Assert.Equal("Text_2", before[^1]);
        Assert.Equal("Text_3", after[^1]);
    }

    [Fact]
    public void An_id_is_used_verbatim_as_the_object_name()
    {
        Assert.Contains("bay-a-weight", NamesOf(Ir(Box("bay-a-weight"))));
    }

    /// <summary>
    /// Two elements sharing an id would produce two objects sharing a name, which TIA refuses at
    /// IMPORT — costing a Portal session to discover, with a message naming the document rather than
    /// the element.
    /// </summary>
    [Fact]
    public void Duplicate_ids_are_refused_before_the_document_is_written()
    {
        var ex = Assert.Throws<UnrepresentableStylingException>(
            () => Emitter.Emit(Ir(Box("same"), Box("same")), "S", 1));

        Assert.Contains("same", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Positive control: distinct ids are not refused.</summary>
    [Fact]
    public void Distinct_ids_are_not_refused()
    {
        Assert.Equal(2, Emitter.Emit(Ir(Box("a"), Box("b")), "S", 1).ItemCount);
    }
}
