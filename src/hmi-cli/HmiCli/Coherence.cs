using System.Globalization;
using System.Xml.Linq;

namespace HmiCli;

/// <summary>
/// Validates an emitted SimaticML document for INTERNAL CONSISTENCY before it is ever handed to TIA.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a measured lesson: <b>the import gate is excellent at catching MALFORMED
/// documents and useless at catching INCOHERENT ones.</b> Three emitter bugs produced precise
/// rejections naming the element, the SimaticML ID, the line and the column. The fourth - a Line
/// whose Start/End points sat outside its own bounding box - <b>crashed the Portal process</b>,
/// surfacing only as "Access to a disposed object of type 'Siemens.Engineering.Project'", which
/// describes the aftermath and not the cause. It took a bisection at roughly one Portal session per
/// hypothesis to find.
/// </para>
/// <para>
/// So: a document that is schema-valid and enum-valid can still describe an object whose parts
/// contradict each other, and TIA's response to that is to die. Everything checkable without Portal
/// gets checked here, where a failure costs nothing.
/// </para>
/// </remarks>
public static class Coherence
{
    public static IReadOnlyList<Finding> Check(string xml, int canvasWidth, int canvasHeight)
    {
        var findings = new List<Finding>();
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            // Not a coherence problem but a fatal one, and worth its own message: an unflushed
            // writer produced exactly this once.
            findings.Add(new Finding("C-XML", Severity.Error, $"emitted document is not well-formed XML: {ex.Message}"));
            return findings;
        }

        var items = doc.Descendants()
            .Where(e => e.Name.LocalName.StartsWith("Hmi.Screen.", StringComparison.Ordinal)
                        && (string?)e.Attribute("CompositionName") == "ScreenItems")
            .ToList();

        foreach (var item in items)
        {
            var type = item.Name.LocalName["Hmi.Screen.".Length..];
            var name = Read(item, "ObjectName") ?? "(unnamed)";

            var left = Int(item, "Left");
            var top = Int(item, "Top");
            var width = Int(item, "Width");
            var height = Int(item, "Height");

            if (left is null || top is null || width is null || height is null)
            {
                findings.Add(new Finding("C-GEOM", Severity.Error,
                    $"{type} '{name}' is missing one of Left/Top/Width/Height"));
                continue;
            }

            if (width < 0 || height < 0)
            {
                findings.Add(new Finding("C-NEG", Severity.Error,
                    $"{type} '{name}' has negative size ({width}x{height})"));
            }

            if (left < 0 || top < 0 || left + width > canvasWidth || top + height > canvasHeight)
            {
                findings.Add(new Finding("C-BOUNDS", Severity.Error,
                    $"{type} '{name}' at {left},{top} {width}x{height} extends beyond the "
                    + $"{canvasWidth}x{canvasHeight} screen"));
            }

            // 🔴 THE SECOND MEASURED CRASH, same class as the Line one below. A Circle declares
            // BOTH a bounding box AND a Radius, and if they disagree the object is incoherent -
            // TIA does not reject it, the Portal process dies with "Access to a disposed object".
            //
            // Found on the first screen that ever used a Circle: 36 x 34 with Radius 17. A radius
            // of 17 is a 34 x 34 circle, so the width was two pixels of nonsense. The Line rule
            // below was written from the first crash and did not generalise - one rule per shape
            // is how this gate stays honest, because "the parts contradict each other" takes a
            // different form in every shape.
            if (type == "Circle")
            {
                var radius = Int(item, "Radius");
                if (radius is null)
                {
                    findings.Add(new Finding("C-CIRCLE", Severity.Error,
                        $"Circle '{name}' declares no Radius"));
                }
                else if (width != height || width != radius * 2)
                {
                    findings.Add(new Finding("C-CIRCLE", Severity.Error,
                        $"Circle '{name}' is {width}x{height} with Radius {radius}: a radius-{radius} "
                        + $"circle is {radius * 2}x{radius * 2}. The bounding box and the radius must "
                        + "agree - TIA does not reject a circle that contradicts itself, IT CRASHES "
                        + "THE PORTAL PROCESS on import."));
                }
            }

            // 🔴 THE ONE THAT CRASHES PORTAL. A Line carries its endpoints as ABSOLUTE screen
            // coordinates, and they must agree with the bounding box the same object declares.
            if (type == "Line")
            {
                var sl = Int(item, "StartLeft");
                var st = Int(item, "StartTop");
                var el = Int(item, "EndLeft");
                var et = Int(item, "EndTop");

                if (sl is null || st is null || el is null || et is null)
                {
                    findings.Add(new Finding("C-LINE", Severity.Error,
                        $"Line '{name}' is missing one of StartLeft/StartTop/EndLeft/EndTop"));
                }
                else
                {
                    var minX = Math.Min(sl.Value, el.Value);
                    var maxX = Math.Max(sl.Value, el.Value);
                    var minY = Math.Min(st.Value, et.Value);
                    var maxY = Math.Max(st.Value, et.Value);

                    // Either diagonal is coherent: what matters is that the endpoints span exactly
                    // the declared bounding box, not which corner the line starts from.
                    if (minX != left || minY != top || maxX != left + width || maxY != top + height)
                    {
                        findings.Add(new Finding("C-LINE", Severity.Error,
                            $"Line '{name}': endpoints ({sl},{st})-({el},{et}) do not match its own "
                            + $"bounding box {left},{top} {width}x{height}. Start/End are ABSOLUTE screen "
                            + "coordinates, not offsets. TIA does not reject this - IT CRASHES THE PORTAL "
                            + "PROCESS on import, reporting only a disposed Project."));
                    }
                }
            }
        }

        // Empty is not clean: a document with no screen items is not a screen, and validating one
        // says nothing. Reported so a broken flatten cannot pass as a clean coherence run.
        if (items.Count == 0)
        {
            findings.Add(new Finding("C-EMPTY", Severity.Error,
                "the emitted document contains no screen items - nothing was validated"));
        }

        return findings;
    }

    public static int ItemCount(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Descendants()
                .Count(e => e.Name.LocalName.StartsWith("Hmi.Screen.", StringComparison.Ordinal)
                            && (string?)e.Attribute("CompositionName") == "ScreenItems");
        }
        catch (System.Xml.XmlException)
        {
            return 0;
        }
    }

    private static string? Read(XElement item, string name) =>
        item.Element("AttributeList")?.Element(name)?.Value;

    private static int? Int(XElement item, string name) =>
        int.TryParse(Read(item, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
