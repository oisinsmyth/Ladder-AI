using System.Text.Json;

namespace Converter.Trace;

// FI-25 (docs/16-future-ideas.md): the per-REQ trace binding the `review-functional` reviewer emits
// (Layer A — the semantic anchor: REQ → concrete IR anchors). `converter trace` (Layer B) consumes it
// and walks the reader/writer graph, emitting facts + candidate verdicts per hop. A plain JSON file,
// loaded exactly like Sanitize/SanitizationMap.cs (mutable DTO, PropertyNameCaseInsensitive, null-check
// → typed exception). Every anchor field is optional — a binding runs whichever hops it names.
public sealed class BindingFile
{
    public List<Binding> Bindings { get; set; } = new();

    public static BindingFile Load(string path)
    {
        var json = File.ReadAllText(path);
        // snake_case policy so `out_tag`/`iface_member` map to OutTag/IfaceMember (case-insensitive
        // alone does not bridge the underscore); case-insensitive kept as a tolerance bonus.
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };
        return JsonSerializer.Deserialize<BindingFile>(json, options)
            ?? throw new BindingFileException($"'{path}' did not deserialize to a trace binding file.");
    }
}

public sealed class Binding
{
    // The REQ this binding traces (e.g. "REQ-004"). The reviewer owns the REQ→anchor mapping.
    public string Req { get; set; } = string.Empty;

    // Hop 1: the expected output tag — must be written somewhere (else no output path).
    public string? OutTag { get; set; }

    // Hop 2: the expected interface source member — must be written somewhere (else broken chain,
    // the in-cycle-lamp class). Give the path form as it appears in logic (v1 does not FB↔iDB-alias).
    public string? IfaceMember { get; set; }

    // Hop 4: a numeric setpoint constraint — the DB member and the spec-stated value.
    public NumberConstraint? Number { get; set; }

    // Hop 5 (v2): a timing constraint — the seconds settings member must reach the named timer's PT via
    // the site ×1000 s→ms MUL/CONVERT idiom. (Value is left to the number hop; this checks the chain.)
    public TimingConstraint? Timing { get; set; }
}

public sealed class TimingConstraint
{
    // The timer whose PT the seconds value must reach (e.g. "OvercurrentMediumTimer") — the "right timer"
    // endpoint the s→ms pair-crossing trap is about.
    public string Timer { get; set; } = string.Empty;

    // The seconds settings member that should feed it via ×1000 (e.g. "DB_Settings.OvercurrentMediumDelay").
    public string SecondsMember { get; set; } = string.Empty;
}

public sealed class NumberConstraint
{
    // The DB member holding the setpoint (e.g. "DB_Settings.DischargeConveyorTimeout").
    public string Member { get; set; } = string.Empty;

    // The spec-stated value (e.g. "10.0"). Compared against the member's DB start value; numeric when
    // both parse as numbers, else normalized-string.
    public string Expected { get; set; } = string.Empty;
}

public sealed class BindingFileException : Exception
{
    public BindingFileException(string message)
        : base(message)
    {
    }
}
