using System.Text.Json;

namespace Harness.Cleanup;

/// <summary>What is known about the block-number claim a removal would strand.</summary>
public enum ClaimDisposition
{
    /// <summary>Unusable zero value.</summary>
    Unstated = 0,

    /// <summary>
    /// No claims document was supplied, so <b>nothing is known</b> about whether this removal strands a
    /// reservation. Distinct from <see cref="NoClaimHeld"/> in the way that matters: not-looked is not
    /// nothing-there.
    /// </summary>
    NotChecked,

    /// <summary>A claim is held on this object's number. Removing it without releasing leaks the number.</summary>
    Held,

    /// <summary>The store was read and holds no claim on this number.</summary>
    NoClaimHeld,
}

/// <summary>One claim as <c>converter claims --json</c> reports it.</summary>
public sealed record HeldClaim(string Kind, string Value, string Agent, string Purpose, string Created);

/// <summary>
/// <b>Spec §16.12c — <i>"every removal path must release its claim, or numbers leak until the range is
/// exhausted"</i>.</b>
///
/// <para>The release VERB has always existed (<c>converter claims --release</c>); what NB-18 records as
/// missing is the CALLER. This is the caller's honest half: the plan reads the shared store, states per
/// removal whether a claim would be stranded, and emits the exact release command.</para>
///
/// <para>🔴 <b>IT DOES NOT RELEASE, AND THAT IS A DESIGN POSITION RATHER THAN AN OMISSION.</b> This
/// binary cannot delete (see <c>Program</c>), so a release issued here would free a number for a removal
/// that has not happened — handing it to the next allocator while the block is still in the project.
/// <b>The leak is a slow failure; releasing early is an immediate collision.</b> Release belongs with
/// whoever executes the deletion, which is why the command is emitted rather than run.</para>
///
/// <para>⚠️ <b>Reading the store is not the same as reading the right store.</b> Passing
/// <c>…\claims\&lt;project&gt;</c> where the root was wanted yields a second empty store with the same
/// name that grants every claim, and two parties making that mistake agree with each other perfectly. So
/// the <c>store</c> field is taken from the document and ECHOED — the claims document reports the path
/// the converter actually resolved, which is the artifact rather than anyone's argument.</para>
/// </summary>
public sealed record ClaimLedger(string StorePath, IReadOnlyList<HeldClaim> Claims)
{
    public static ClaimLedger Parse(string json, string label)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new CleanupInputException($"{label}: not valid JSON ({ex.Message}).");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
                throw new CleanupInputException($"{label}: has no 'claims' array, so it is not `converter claims --json` output. An unrecognised document read as an empty store would report every number as free.");

            if (!root.TryGetProperty("store", out var store) || store.ValueKind != JsonValueKind.String)
                throw new CleanupInputException($"{label}: has no 'store' field. The store path is the one thing that distinguishes the shared registry from a shadowed empty copy, and a ledger that cannot say which it read cannot be trusted about a zero.");

            var parsed = claims.EnumerateArray().Select(c => new HeldClaim(
                Str(c, "kind") ?? string.Empty,
                Str(c, "value") ?? string.Empty,
                Str(c, "agent") ?? string.Empty,
                Str(c, "purpose") ?? string.Empty,
                Str(c, "created") ?? string.Empty)).ToArray();

            return new ClaimLedger(store.GetString()!, parsed);
        }
    }

    /// <summary>
    /// The claim on an object's block number, if one is held.
    ///
    /// <para>Matching is on the claim VALUE against the two spellings the registry uses for a block
    /// number — the bare number and the type-prefixed form (<c>FB9020</c>) — and on the object's own
    /// name, which is how a <c>block-edit</c> or <c>tag</c> claim is spelled. Deliberately broad: a
    /// false match reports a release command that turns out to be unnecessary, whereas a missed match
    /// reports a leak as clean.</para>
    /// </summary>
    public IReadOnlyList<HeldClaim> For(IrObject o)
    {
        var candidates = new List<string> { o.Name };
        if (o.Number is { } n)
        {
            candidates.Add(n.ToString());
            foreach (var prefix in new[] { "FB", "FC", "DB", "OB" })
                candidates.Add($"{prefix}{n}");
        }

        return Claims
            .Where(c => candidates.Any(v => string.Equals(v, c.Value, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static string? Str(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
