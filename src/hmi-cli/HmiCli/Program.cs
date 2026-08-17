using System.Text.Json;
using HmiCli;

// Exit codes, following openness-cli's convention that a code means one thing:
//   0 clean · 1 findings · 2 usage/input error · 9 NOTHING EXAMINED (empty is not clean)
const int ExitClean = 0;
const int ExitFindings = 1;
const int ExitUsage = 2;
const int ExitNothingExamined = 9;

const string Usage = """
hmi-cli - HTML -> HMI screen tooling (WinCC Classic Basic)

  hmi-cli flatten     <screen.html> --panel <name> [--out <file.json>]
  hmi-cli lint        (<screen.html> | <screen-ir.json>) --panel <name> [--json]
  hmi-cli style-check (<screen.html> | <screen-ir.json>) [--json]
  hmi-cli check       (<screen.html> | <screen-ir.json>) --panel <name> [--json]
  hmi-cli emit        (<screen.html> | <screen-ir.json>) --panel <name> --screen-name <name>
                      [--number <n>] [--out <file.xml>] [--handoff <file.md>]
  hmi-cli compare     <first.xml> <second.xml> [--json]
  hmi-cli panels      [--bands]
  hmi-cli brand-check <#RRGGBB | r,g,b>

  --panel is REQUIRED and has no default. The KTP700 and KTP900 Basic share a resolution and
  differ ~28% physically, so a guessed panel is a quarter-scale sizing error that passes every
  pixel-based check silently (H-407).

  --browser <path>   override browser discovery (Chrome or Edge, headless)

  data-hmi-override="H-104: reason"  sets a rule aside on one element. The rules GUIDE; the
  engineer directs. Overrides are always REPORTED. Design rules need only the ID; SAFETY rules
  need a stated reason; CORRECTNESS rules (off-canvas, degenerate, sub-pixel) cannot be
  overridden - that screen is broken rather than different.

Rules are docs/17-hmi-conventions.md. Sizing derivation is hmi/sizing-standard.md.

NOTE: these checks do NOT subsume looking at the render, and the render does not subsume them.
Measured directly: the linter missed a visible text collision (glyph overflow without box
intersection) and an 8-cycle render loop missed 2 off-canvas elements. Run both.
""";

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return ExitUsage;
}

var command = args[0];
var positional = args.Skip(1).Where(a => !a.StartsWith("--")).ToList();
var asJson = args.Contains("--json");

// brand-check takes a colour, not a screen, so it runs before any file handling.
if (command == "brand-check")
{
    var colour = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
    if (colour is null)
    {
        Console.Error.WriteLine($"brand-check needs a colour: #RRGGBB or r,g,b.{Environment.NewLine}{Usage}");
        return ExitUsage;
    }

    var v = Brand.Check(colour);
    if (v is null)
    {
        Console.Error.WriteLine($"Could not read '{colour}' as a colour. Use #RRGGBB or r,g,b.");
        return ExitUsage;
    }

    Console.WriteLine($"COLOUR: {colour}");
    Console.WriteLine($"  hue {v.Hue:0}deg  saturation {v.Saturation:0.00}  lightness {v.Lightness:0.00}");
    Console.WriteLine($"  distance from alarm hues: red {v.DistanceFromRed:0}deg, amber {v.DistanceFromAmber:0}deg "
                    + $"(>= {Brand.MinimumSeparationDegrees:0} required)");
    Console.WriteLine();
    Console.WriteLine(v.AccentAllowed
        ? "  ACCENT: allowed - usable on chrome AND as the primary-command accent (H-604, one per screen)"
        : "  ACCENT: REFUSED - chrome only, and desaturate it");
    Console.WriteLine($"  CHROME TEXT: rgb({v.ChromeTextColour}) at {v.ChromeTextContrast:0.0}:1 contrast");
    Console.WriteLine();

    foreach (var f in v.Findings)
    {
        Console.WriteLine("  " + f);
    }

    // Exit 1 when the colour cannot be used as an accent: this is a gate for a generator, not
    // advice for a human, and a caller needs to branch on it.
    return v.AccentAllowed && v.ChromeTextContrast >= Brand.MinimumContrast ? ExitClean : ExitFindings;
}

if (command == "panels")
{
    Console.WriteLine($"{"panel",-16} {"order no.",-22} {"mm",-16} {"px",-12} {"px/mm H",-9} px/mm V");
    foreach (var p in new[]
             {
                 Panels.Ktp400Basic, Panels.Ktp700Basic, Panels.Ktp900Basic,
                 Panels.Mtp700, Panels.Mtp1000, Panels.Mtp1200,
             })
    {
        Console.WriteLine($"{p.Name,-16} {p.OrderNumber,-22} {$"{p.WidthMm} x {p.HeightMm}",-16} "
                        + $"{$"{p.WidthPx}x{p.HeightPx}",-12} {p.PxPerMmH,-9:0.00} {p.PxPerMmV:0.00}");
    }

    Console.WriteLine();
    Console.WriteLine("px/mm is a PAIR: Basic panels do not have square pixels. The vertical figure is");
    Console.WriteLine("the tighter axis and is what a minor-axis touch-target check must use (H-406).");

    // --bands makes the TOOL the single source for the sizing table. The first version of that
    // table was computed by hand with rounding, while the code uses Ceiling - a minimum threshold
    // must round UP, because 50 px at 5.59 px/mm is 8.95 mm and therefore below a 9 mm floor. The
    // two disagreed in six places and a unit test found it. A table a human retypes is a table that
    // drifts from the checker enforcing it.
    if (args.Contains("--bands"))
    {
        var bands = new (string Name, double Mm)[]
        {
            ("absolute minimum interactive", 9),
            ("standard button", 12),
            ("primary / frequent action", 15),
            ("safety-critical (STOP)", 20),
            ("minimum gap", 3),
            ("recommended gap", 5),
        };

        Console.WriteLine();
        Console.WriteLine("Thresholds are declared in MILLIMETRES; pixels are derived per panel and per");
        Console.WriteLine("AXIS, rounded UP (a floor that rounds down is not a floor).");
        Console.WriteLine();
        Console.WriteLine($"{"band",-30} {"mm",4}  {"KTP700 (target)",-18} {"KTP900 (anchor)",-18}");
        foreach (var (name, mm) in bands)
        {
            var t = $"{Panels.Ktp700Basic.MmToPxH(mm)} x {Panels.Ktp700Basic.MmToPxV(mm)} px";
            var a = $"{Panels.Ktp900Basic.MmToPxH(mm)} x {Panels.Ktp900Basic.MmToPxV(mm)} px";
            Console.WriteLine($"{name,-30} {mm,4}  {t,-18} {a,-18}");
        }
    }

    return ExitClean;
}

// compare takes two SimaticML documents rather than a screen, so it is handled before the IR load.
if (command == "compare")
{
    if (positional.Count < 2)
    {
        Console.Error.WriteLine($"compare needs two files: <first.xml> <second.xml>.{Environment.NewLine}{Usage}");
        return ExitUsage;
    }

    foreach (var f in positional.Take(2))
    {
        if (!File.Exists(f))
        {
            Console.Error.WriteLine($"No such file: {f}");
            return ExitUsage;
        }
    }

    if (string.Equals(Path.GetFullPath(positional[0]), Path.GetFullPath(positional[1]), StringComparison.OrdinalIgnoreCase))
    {
        // The same path twice is a comparison that cannot fail, which makes it worse than no
        // comparison: it reports a pass and examined nothing meaningful.
        Console.Error.WriteLine("Both paths are the same file - NOT COMPARED.");
        return 2;
    }

    var cmp = ScreenCompare.Compare(File.ReadAllText(positional[0]), File.ReadAllText(positional[1]));

    foreach (var d in cmp.Changed)
    {
        Console.WriteLine("CHANGED  " + d);
    }

    foreach (var d in cmp.Dropped)
    {
        Console.WriteLine("DROPPED  " + d);
    }

    if (cmp.DefaultedByTia > 0)
    {
        // Reported, never gated: an attribute only the read-back carries is TIA filling in a
        // default the emitter never stated, which is expected rather than a fidelity loss.
        Console.WriteLine($"DEFAULTED-BY-TIA: {cmp.DefaultedByTia} attribute(s) the first document never stated");
    }

    Console.WriteLine($"IGNORED: {string.Join("; ", ScreenCompare.Ignored)}");

    // The DENOMINATOR is the field count, not the object count. An earlier version of this tool
    // printed only objects while comparing four numbers each, and reported IDENTICAL over a screen
    // whose label text had been changed.
    Console.WriteLine($"COMPARED: {cmp.Items} object(s), "
                    + $"{cmp.FieldsComparedOnBothSides} attribute/text field(s) stated on BOTH sides");

    if (cmp.NothingCompared)
    {
        Console.Error.WriteLine("NOTHING COMPARED - this is not a pass");
        return ExitNothingExamined;
    }

    Console.WriteLine(cmp.Differences == 0
        ? $"IDENTICAL: {cmp.Items} object(s), {cmp.FieldsComparedOnBothSides} field(s), 0 changed, 0 dropped"
        : $"SUMMARY: {cmp.Changed.Count} changed, {cmp.Dropped.Count} dropped "
          + $"over {cmp.FieldsComparedOnBothSides} field(s)");

    return cmp.Differences == 0 ? ExitClean : ExitFindings;
}

if (positional.Count == 0)
{
    Console.Error.WriteLine($"Missing input file.{Environment.NewLine}{Usage}");
    return ExitUsage;
}

var input = positional[0];
if (!File.Exists(input))
{
    Console.Error.WriteLine($"No such file: {input}");
    return ExitUsage;
}

var panelName = ValueOf("--panel");
var browserOverride = ValueOf("--browser");
var outPath = ValueOf("--out");

// The panel requirement follows the INPUT, not the command: flattening HTML needs one to size the
// canvas (so even the panel-independent style-check needs it when handed HTML), while a
// screen-ir.json already carries the panel it was flattened for and must not be told a different
// one. Getting this backwards made style-check unusable on its primary input.
var isHtml = input.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
             || input.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

Panel? panel = null;
if (isHtml)
{
    if (!Panels.TryResolve(panelName, out var resolved, out var panelError))
    {
        Console.Error.WriteLine(panelError);
        return ExitUsage;
    }

    panel = resolved;
}

ScreenIr ir;
try
{
    ir = LoadIr(input, panel, browserOverride);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not produce IR from '{input}': {ex.Message}");
    return ExitUsage;
}

// From a .json the panel comes from the IR itself, never from the command line.
if (panel is null && command is "lint" or "check")
{
    if (!Panels.TryResolve(ir.Panel, out var fromIr, out var irPanelError))
    {
        Console.Error.WriteLine($"The IR declares panel '{ir.Panel}', which cannot be resolved. {irPanelError}");
        return ExitUsage;
    }

    panel = fromIr;
}

switch (command)
{
    case "flatten":
    {
        var json = JsonSerializer.Serialize(ir, new JsonSerializerOptions { WriteIndented = true });
        if (outPath is not null)
        {
            File.WriteAllText(outPath, json);
            Console.WriteLine($"FLATTENED: {ir.Items.Count} item(s) -> {outPath}");
            Console.WriteLine($"PANEL: {ir.Panel}  CANVAS: {ir.CanvasWidth}x{ir.CanvasHeight}  BROWSER: {ir.ChromeVersion}");
        }
        else
        {
            Console.WriteLine(json);
        }

        return ir.Items.Count == 0 ? Report(new CheckResult("flatten", 0, Array.Empty<Finding>()), asJson) : ExitClean;
    }

    case "emit":
    {
        var screenName = ValueOf("--screen-name");
        if (screenName is null)
        {
            Console.Error.WriteLine("Missing required flag: --screen-name <name>.");
            return ExitUsage;
        }

        var numberText = ValueOf("--number");
        var number = numberText is null ? 1 : int.Parse(numberText);

        Emitter.EmitResult result;
        try
        {
            result = Emitter.Emit(ir, screenName, number);
        }
        catch (Exception ex) when (ex is UnsupportedItemTypeException or UntypedElementException or UnrepresentableStylingException)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitFindings;
        }

        // COHERENCE GATE - runs before the document can reach TIA. An incoherent document is not
        // rejected by the import, it CRASHES the Portal process (measured: a Line whose endpoints
        // sat outside its own bounding box), and a crash yields no diagnostic and costs a session.
        // Everything checkable without Portal is checked here, where failing is free.
        var coherence = Coherence.Check(result.Xml, ir.CanvasWidth, ir.CanvasHeight);
        if (coherence.Count > 0)
        {
            foreach (var f in coherence)
            {
                Console.Error.WriteLine(f.ToString());
            }

            Console.Error.WriteLine($"COHERENCE: {coherence.Count} problem(s) over "
                                  + $"{Coherence.ItemCount(result.Xml)} item(s) - NOTHING WRITTEN.");
            return ExitFindings;
        }

        if (outPath is not null)
        {
            File.WriteAllText(outPath, result.Xml);
            Console.WriteLine($"EMITTED: {result.ItemCount} item(s) -> {outPath}");
            Console.WriteLine($"COHERENCE: clean over {Coherence.ItemCount(result.Xml)} item(s)");
        }
        else
        {
            Console.WriteLine(result.Xml);
        }

        // The hand-off is the RECORD; the placeholder on the screen is only a reminder. A reminder
        // that exists solely inside the artifact is lost the moment somebody works from the artifact.
        if (result.HandOff.Count > 0)
        {
            var handOffPath = ValueOf("--handoff");
            var lines = new List<string>
            {
                $"# Manual steps required for screen '{screenName}'",
                string.Empty,
                "These CANNOT be generated - classic HMI cannot author them and SimaticML cannot",
                "represent them. Each has a visible placeholder on the screen.",
                string.Empty,
            };
            lines.AddRange(result.HandOff.Select(h => "- [ ] " + h));

            if (handOffPath is not null)
            {
                File.WriteAllLines(handOffPath, lines);
                Console.WriteLine($"HAND-OFF: {result.HandOff.Count} manual step(s) -> {handOffPath}");
            }
            else
            {
                Console.Error.WriteLine(string.Join(Environment.NewLine, lines));
            }
        }

        // Empty is not clean: a document with no items is not a screen, and reporting it as a
        // success is how a broken flatten reaches TIA looking like a working pipeline.
        if (result.ItemCount == 0)
        {
            Console.Error.WriteLine("NOTHING EMITTED - this is not a pass.");
            return ExitNothingExamined;
        }

        return ExitClean;
    }

    case "lint":
        return Report(Linter.Run(ir, panel!), asJson, ir);

    case "style-check":
        return Report(StyleChecker.Run(ir), asJson, ir);

    case "check":
    {
        var lint = Linter.Run(ir, panel!);
        var style = StyleChecker.Run(ir);
        var combined = new CheckResult(
            "check",
            Math.Max(lint.Examined, style.Examined),
            lint.Findings.Concat(style.Findings).ToList());
        return Report(combined, asJson, ir);
    }

    default:
        Console.Error.WriteLine($"Unknown command '{command}'.{Environment.NewLine}{Usage}");
        return ExitUsage;
}

string? ValueOf(string flag)
{
    var i = Array.IndexOf(args, flag);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static ScreenIr LoadIr(string path, Panel? panel, string? browserOverride)
{
    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
        var loaded = JsonSerializer.Deserialize<ScreenIr>(File.ReadAllText(path))
                     ?? throw new InvalidOperationException("the IR file parsed to null");

        // H-407 again, on the read path: an IR that declares no panel cannot be checked, and
        // defaulting one is the silent quarter-scale error the flag exists to prevent.
        if (string.IsNullOrWhiteSpace(loaded.Panel))
        {
            throw new InvalidOperationException(
                "this screen-ir.json declares no panel. It cannot be size-checked and a panel will not be assumed.");
        }

        return loaded;
    }

    if (panel is null)
    {
        throw new InvalidOperationException("--panel is required to flatten HTML.");
    }

    var browser = Flattener.FindBrowser(browserOverride)
                  ?? throw new InvalidOperationException(
                      "no headless browser found. Pass --browser <path> to chrome.exe or msedge.exe.");

    return Flattener.Flatten(path, panel, browser);
}

static int Report(CheckResult result, bool json, ScreenIr? ir = null)
{
    // The house rules GUIDE the generator; the engineer directs it. An element may carry
    // data-hmi-override="H-104: reason" to set a rule aside - but the suppression is always
    // REPORTED, never silent, because a check nobody can see being disabled is worse than no check.
    if (ir is not null && result.Findings.Count > 0)
    {
        var applied = Overrides.Apply(result.Findings, ir);
        if (applied.Honoured.Count > 0 || applied.Refused.Count > 0)
        {
            foreach (var h in applied.Honoured)
            {
                Console.WriteLine("OVERRIDDEN  " + h);
            }

            foreach (var r in applied.Refused)
            {
                Console.WriteLine(r.ToString());
            }

            result = result with { Findings = applied.Remaining.Concat(applied.Refused).ToList() };

            if (applied.Honoured.Count > 0)
            {
                Console.WriteLine($"OVERRIDES HONOURED: {applied.Honoured.Count} "
                                + "(stated on the element, and listed above)");
            }
        }
    }

    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        foreach (var f in result.Findings.OrderBy(f => f.RuleId))
        {
            Console.WriteLine(f.ToString());
        }

        // The denominator, on every run. Five separate tools in this repo have reported a false
        // pass over zero comparisons; this line is the cheapest thing that prevents a sixth.
        Console.WriteLine($"EXAMINED: {result.Examined} item(s)");
        Console.WriteLine(result.NothingExamined
            ? "NOTHING EXAMINED - this is not a pass"
            : $"SUMMARY: {result.ErrorCount} error(s) over {result.Examined} item(s)");
    }

    if (result.NothingExamined)
    {
        return ExitNothingExamined;
    }

    return result.ErrorCount > 0 ? ExitFindings : ExitClean;
}
