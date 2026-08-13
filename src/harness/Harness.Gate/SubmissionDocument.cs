using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Results;

namespace Harness.Gate;

/// <summary>
/// The JSON a submission is written as, and the reader that turns it into the checked types.
///
/// <para><b>Every field is nullable on the way in, deliberately.</b> A missing field must reach the
/// schema gate as MISSING rather than as a default — a zero <c>MaxDuration</c> that arrived because
/// nobody wrote one and a zero that somebody typed are the same value, and only the gate should decide
/// what to do about it. Deserialising into non-nullable fields would silently supply the answer.</para>
/// </summary>
public sealed class SubmissionDocument
{
    public string? BlockAuthor { get; set; }
    public int RuntimeCompression { get; set; } = 1;

    /// <summary>Slots in the wave set. Feeds §12a derivation 1's floor, which scales with tensor width.</summary>
    public int SlotsInWaveSet { get; set; } = 1;

    /// <summary>Result-region width in registers, for the slots-per-read arithmetic the floor uses.</summary>
    public int ResultRegistersPerSlot { get; set; } = 1;

    public ModelDocument? Model { get; set; }
    public EnumerationDocument? Enumeration { get; set; }
    public MapDocument? Map { get; set; }

    /// <summary>
    /// Blocks the reference graph says conflict with this submission's targets.
    ///
    /// <b>Absent means the graph was not available</b>, and the blacklist gate then reports NOT CHECKED.
    /// An empty ARRAY is a different statement — "the graph ran and found no conflicts" — and is
    /// honoured as such.
    /// </summary>
    public List<string>? ComputedConflicts { get; set; }

    public List<VectorDocument>? Vectors { get; set; }

    public static SubmissionDocument Read(string json) =>
        JsonSerializer.Deserialize<SubmissionDocument>(json, Options)
        ?? throw new InvalidDataException("the submission document is empty.");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed class ModelDocument
{
    public string? Id { get; set; }
    public List<string>? Represents { get; set; }
    public List<string>? DoesNotRepresent { get; set; }
    public bool ValidatedAgainstPlantData { get; set; }
}

/// <summary>
/// The enumeration the gate consumes.
///
/// <para><b>The flat projection is legal and is not free.</b> Clauses plus assertions alone is what the
/// enumeration skill calls its projection, and against it two gates report NOT CHECKED: the assertion's
/// canonical FORM cannot be compared with what the vector declared (F-3's authority), and the
/// ENUMERATOR's independence cannot be established. Supplying Forms and Enumerator is what turns those
/// into checks.</para>
/// </summary>
public sealed class EnumerationDocument
{
    public List<string>? Clauses { get; set; }
    public List<string>? Assertions { get; set; }

    /// <summary>Assertion ID to its canonical form. Absent means the projection, and the form gate is NOT CHECKED.</summary>
    public Dictionary<string, AssertionForm>? Forms { get; set; }

    /// <summary>Who performed the decomposition. Absent means independence cannot be shown, which is NOT CHECKED.</summary>
    public string? Enumerator { get; set; }
}

public sealed class MapDocument
{
    /// <summary>signal → instrumentation modes the copy layer provides for it.</summary>
    public Dictionary<string, List<string>>? ProvidedFor { get; set; }
}

public sealed class VectorDocument
{
    public string? Id { get; set; }
    public string? Slot { get; set; }
    public int Index { get; set; }
    public string? Author { get; set; }
    public string? Clause { get; set; }
    public string? Assertion { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
    public string? StartBool { get; set; }
    public List<ExpectationDocument>? Expectations { get; set; }

    /// <summary>
    /// The cited assertion's canonical form.
    ///
    /// <para><b>It does NOT default to <c>When</c> any more.</b> It used to, on the reasoning that WHEN
    /// is the shape checked hardest — but the form decides whether a SAMPLED observation is admissible
    /// (F-3), so a document that omitted the field was handed the permissive path. The zero value is
    /// <see cref="AssertionForm.Unstated"/>, which <b>fails the same comparison a WRONG form fails</b>.
    /// The enumeration is where an author gets the right answer.</para>
    /// </summary>
    public AssertionForm AssertionForm { get; set; } = AssertionForm.Unstated;

    /// <summary>
    /// The value on the completion signal that means "finished".
    ///
    /// <para><b>Contract §2 has no field for this</b> — it names a completion SIGNAL and never says what
    /// value on it means finished, and the loop was assuming 1. Carrying it as data removes the
    /// assumption from the code. The default of 1 is stated here as the harness's CONVENTION rather than
    /// derived from anything, and whether the contract should carry the field is a spec question.</para>
    /// </summary>
    public int CompletionValue { get; set; } = 1;
    public string? SettlingCondition { get; set; }
    public List<string>? SettlingSignals { get; set; }
    public int MaxDurationScans { get; set; }
    public List<BlacklistDocument>? Blacklist { get; set; }
    public int CompressionFactor { get; set; } = 1;
    public List<string>? AssertedBehaviours { get; set; }
    public string? CompletionSignal { get; set; }
    public string? Kills { get; set; }
}

public sealed class ExpectationDocument
{
    public string? Signal { get; set; }
    public SignalNature Nature { get; set; }
    public InstrumentationMode Mode { get; set; }
    public int WindowScans { get; set; }
}

public sealed class BlacklistDocument
{
    public string? Block { get; set; }
    public string? Reason { get; set; }
}
