using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Map;

namespace Harness.Gate;

/// <summary>
/// The binding document — <b>the coordinator's half, and it is not in the submission on purpose.</b>
///
/// <para>Per D13 instrumentation is a property of the COPY LAYER, not of the block under test, and the
/// vector author is fenced from it. So which signal sits in which register, and what TYPE it is, is
/// stated here rather than by whoever wrote the vectors.</para>
/// </summary>
public sealed class BindingDocument
{
    public List<SlotBindingDocument>? Slots { get; set; }

    /// <summary>Copy-layer block name. Defaults to the generator's own.</summary>
    public string? BlockName { get; set; }

    /// <summary>Block number. <b>No default that works</b> — the generator refuses zero, because harness block numbers come from a reserved range the caller allocates from (hard rule 3).</summary>
    public int? BlockNumber { get; set; }

    public string? TagTableName { get; set; }
    public string? TagPrefix { get; set; }

    /// <summary>First <c>%M</c> byte of the mirror. The rig's is 1000.</summary>
    public int? BaseByte { get; set; }

    /// <summary>Retentive <c>%M</c> extent, for the 0.1b non-retentive assertion.</summary>
    public int? RetentiveBytes { get; set; }

    public static BindingDocument Read(string json) =>
        JsonSerializer.Deserialize<BindingDocument>(json, Options)
        ?? throw new InvalidDataException("the binding document is empty.");

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>One slot's binding.</summary>
public sealed class SlotBindingDocument
{
    public string? SlotId { get; set; }
    public List<MirroredSignalDocument>? VectorTargets { get; set; }

    /// <summary><b>Null is a CLAIM</b> — "this block has no start gate" (D37) — never a blank.</summary>
    public string? StartCondition { get; set; }

    public List<MirroredSignalDocument>? ResultSources { get; set; }
}

/// <summary>One mirrored signal. <b><c>Type</c> absent parses as <c>Unstated</c>, which the generator refuses by name.</b></summary>
public sealed class MirroredSignalDocument
{
    public string? Tag { get; set; }

    /// <summary>
    /// The element type. <b>There is deliberately no default:</b> a hard-coded <c>Int</c> is what put an
    /// unmirrorable copy layer on a controller, and <c>Unstated</c> refuses naming the signal.
    /// </summary>
    public MirrorValueType Type { get; set; } = MirrorValueType.Unstated;

    /// <summary>
    /// 🔴 <b>THE SPECIFICATION'S NAME FOR THIS SIGNAL — the join the harness never had.</b>
    ///
    /// <para><c>tag</c> is what the BLOCK calls it; this is what the SPECIFICATION calls it, and a vector
    /// cites the second. *** MEASURED: WHEREVER THEY DIFFER, EVERY MECHANICAL PATH FAILED *** — gate 5,
    /// the static interface check and the conflict graph, all in one run, with 1 of 17 signals resolving
    /// and that one being the only name collision. The translation had lived only in a prose table, and
    /// prose is what no gate reads.</para>
    ///
    /// <para><b>Absent is NOT "the same as tag".</b> That identity is the assumption being removed. Absent
    /// means the binding said nothing, gate 5 reports NOT CHECKED and names the tag, and a genuinely
    /// identical pair is STATED as identical.</para>
    /// </summary>
    public string? SpecName { get; set; }

    /// <summary>
    /// <b>The block that latches this signal, when one does.</b> Absent means this binding claims no latch.
    ///
    /// <para>*** FOUR TRUE LATCHES WERE REFUSED IN ONE RUN BECAUSE THIS COULD NOT BE SAID. *** The
    /// copy-layer generator emits no per-signal latch, so <c>Sampled</c> is all it can derive; a
    /// hand-authored latch block on the device is real and was undeclarable.</para>
    ///
    /// <para>🔴 <b>It names a BLOCK, not a mode.</b> <c>latched: true</c> would be a caller asserting the
    /// answer, and a caller assertion is forgotten exactly when it matters. A block name is PROVENANCE —
    /// checkable against the deployed object set, and printed in the gate's own report.</para>
    /// </summary>
    public string? LatchedBy { get; set; }
}
