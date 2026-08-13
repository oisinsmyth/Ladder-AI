using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Results;

/// <summary>One assertion as the stamper found it, and what it stamped.</summary>
/// <param name="PreviousId">
/// The <c>id:</c> that was already in the file, or null when there was none. <b>Carried so a re-stamp is
/// a visible event</b> — §3.2's dangling-ID consequence — rather than an invisible overwrite.
/// </param>
public sealed record StampedAssertion(
    string ClauseId,
    string Ordinal,
    string NormalisedText,
    string Id,
    string? PreviousId,
    AssertionForm Form)
{
    /// <summary>True when a text edit moved the ID. Every prior citation to <see cref="PreviousId"/> is now STALE.</summary>
    public bool ReStamped => PreviousId is not null && !string.Equals(PreviousId, Id, StringComparison.Ordinal);

    /// <summary>The display ordinal, for listings only. NEVER citable (§3.3).</summary>
    public string Display => $"{ClauseId}.{Ordinal}";
}

/// <summary>The stamped file, or every reason there is not one.</summary>
public sealed record StampResult(
    string? StampedText,
    IReadOnlyList<StampedAssertion> Assertions,
    IReadOnlyList<string> Errors,
    string Summary)
{
    public bool Stamped => StampedText is not null;

    /// <summary>IDs whose text changed. Every citation to one of these is STALE and must be re-read.</summary>
    public IReadOnlyList<StampedAssertion> Dangling => Assertions.Where(a => a.ReStamped).ToArray();
}

/// <summary>
/// *** THE STAMPER (`assertion-enumeration.md` §3.4). ***
///
/// <para>The enumerator issues its artifact with <c>normalised_text:</c> and <b>no <c>id:</c> key</b>,
/// because <c>assertion-enumerator</c> is denied <c>Bash</c> deliberately — that fence is what stops the
/// implementation contaminating a spec-side denominator, and an ID is a SHA-256, so the same fence
/// removes every way to produce one. This computes the hashes and writes them in, and the stamped file
/// is what gets published and cited.</para>
///
/// <para><b>WHY A STAMPER NEEDS NO INDEPENDENCE, AND WHAT MUST STAY TRUE FOR THAT.</b> Stamping is a
/// pure function of text that is already fixed, and <b>the gate recomputes every ID from
/// <c>normalised_text</c> and refuses a mismatch</b> (<see cref="SubmissionGate"/>). A wrong hash is
/// caught and a right one is what anybody would have produced — so this needs no independence from the
/// block author or the vector author, unlike the decomposition itself. <b>If that recomputation ever
/// stops happening, this becomes trusted, and it was never designed to be.</b></para>
///
/// <para>*** IT NEVER PRESERVES AN ID WHOSE TEXT HAS CHANGED. *** That is §3.2's silent-survival failure
/// and the one thing a careless implementation would do to be helpful. A changed text produces a new ID,
/// the old one dangles, a <c>supersedes:</c> link is written, and the re-stamp is reported.</para>
///
/// <para><b>The parse is line-oriented and FAILS CLOSED.</b> There is no YAML library here, so the
/// reader could silently see fewer assertions than the file has — which would stamp 18 of 19 and look
/// clean. The file's own <c>denominator: assertions: N</c> is therefore cross-checked against the number
/// parsed, and a mismatch is a refusal. Block scalars and quoting styles the reader does not implement
/// are refusals too, never skips.</para>
/// </summary>
public static class EnumerationStamper
{
    // The shape the enumerator emits. Indentation is part of the contract: a clause key sits at two
    // spaces under `clauses:`, an assertion at six, its fields at eight.
    private static readonly Regex ClauseKey = new(@"^  (?<clause>[A-Za-z0-9][A-Za-z0-9_.\-]*):\s*$", RegexOptions.Compiled);
    private static readonly Regex AssertionStart = new(@"^      - ordinal:\s*(?<ordinal>\S+)\s*$", RegexOptions.Compiled);
    private static readonly Regex Field = new(@"^        (?<key>[A-Za-z0-9_]+):(?<value>.*)$", RegexOptions.Compiled);
    private static readonly Regex DenominatorAssertions = new(@"^  assertions:\s*(?<count>\d+)\s*$", RegexOptions.Compiled);
    private static readonly Regex ProjectionPlaceholder = new(@"^  assertions:\s*""<\d+ computed IDs>""\s*$", RegexOptions.Compiled);

    /// <summary>Stamp an enumeration file's text. Pure: no filesystem, no clock, no randomness.</summary>
    public static StampResult Stamp(string? fileText)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fileText))
            return Refuse(errors, "the enumeration file is empty. An empty enumeration has nothing citable in it (FI-44: empty is not clean).");

        var crlf = fileText!.Contains("\r\n", StringComparison.Ordinal);
        var lines = fileText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

        var clausesAt = lines.FindIndex(l => l == "clauses:");
        if (clausesAt < 0)
            return Refuse(errors, "no `clauses:` key at column 0. This reader is line-oriented and refuses a shape it does not recognise rather than stamping whatever it happened to match.");

        var declaredCount = DeclaredAssertionCount(lines);
        if (declaredCount is null)
        {
            return Refuse(errors,
                "no `assertions: <n>` line under `denominator:`. *** THAT LINE IS THIS READER'S ONLY CROSS-CHECK ON ITS OWN PARSE. *** "
                + "Without it a reader that saw 18 of 19 assertions would stamp 18 and report success.");
        }

        var found = Parse(lines, clausesAt, errors);

        if (errors.Count > 0)
            return Refuse(errors);

        if (found.Count != declaredCount)
        {
            return Refuse(errors,
                $"the file declares {declaredCount} assertion(s) and this reader found {found.Count}. One of the two is wrong and "
                + "neither can be assumed, so nothing is stamped. Check for an indentation the reader does not accept, or a "
                + "`denominator: assertions:` that was not updated.");
        }

        Validate(found, errors);
        if (errors.Count > 0)
            return Refuse(errors);

        var stampedAssertions = found
            .Select(a => new StampedAssertion(
                a.ClauseId, a.Ordinal, a.NormalisedText,
                AssertionId.Compute(a.ClauseId, a.NormalisedText),
                a.ExistingId, a.Form))
            .ToArray();

        var text = Write(lines, found, stampedAssertions, crlf);

        var restamped = stampedAssertions.Count(a => a.ReStamped);
        var fresh = stampedAssertions.Count(a => a.PreviousId is null);

        return new StampResult(text, stampedAssertions, Array.Empty<string>(),
            $"{stampedAssertions.Length} assertion(s) across {stampedAssertions.Select(a => a.ClauseId).Distinct(StringComparer.Ordinal).Count()} clause(s): "
            + $"{fresh} newly stamped, {restamped} RE-STAMPED (their previous IDs now dangle and every citation to them is STALE), "
            + $"{stampedAssertions.Length - fresh - restamped} unchanged.");
    }

    // ---------------------------------------------------------------------------------------------

    private sealed class Raw
    {
        public string ClauseId = string.Empty;
        public string Ordinal = string.Empty;
        public string NormalisedText = string.Empty;
        public string? Text;
        public string? ExistingId;
        public AssertionForm Form;
        public int StartLine;
        public int NormalisedTextLine = -1;
        public int IdLine = -1;
        public int SupersedesLine = -1;
    }

    private static int? DeclaredAssertionCount(IReadOnlyList<string> lines)
    {
        var denominatorAt = lines.ToList().FindIndex(l => l == "denominator:");
        if (denominatorAt < 0)
            return null;

        for (var i = denominatorAt + 1; i < lines.Count; i++)
        {
            if (lines[i].Length > 0 && !char.IsWhiteSpace(lines[i][0]) && !lines[i].StartsWith("#", StringComparison.Ordinal))
                break;

            var match = DenominatorAssertions.Match(lines[i]);
            if (match.Success)
                return int.Parse(match.Groups["count"].Value);
        }

        return null;
    }

    private static List<Raw> Parse(IReadOnlyList<string> lines, int clausesAt, List<string> errors)
    {
        var found = new List<Raw>();
        var clause = string.Empty;
        Raw? current = null;

        for (var i = clausesAt + 1; i < lines.Count; i++)
        {
            var line = lines[i];

            // Column 0 and non-blank ends the clauses block.
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                break;

            var clauseMatch = ClauseKey.Match(line);
            if (clauseMatch.Success)
            {
                clause = clauseMatch.Groups["clause"].Value;
                current = null;
                continue;
            }

            var assertionMatch = AssertionStart.Match(line);
            if (assertionMatch.Success)
            {
                if (clause.Length == 0)
                {
                    errors.Add($"line {i + 1}: an assertion appears before any clause key. An assertion outside a clause has no clause identity, and an ID needs one.");
                    continue;
                }

                current = new Raw { ClauseId = clause, Ordinal = assertionMatch.Groups["ordinal"].Value, StartLine = i };
                found.Add(current);
                continue;
            }

            if (current is null)
                continue;

            var field = Field.Match(line);
            if (!field.Success)
                continue;

            switch (field.Groups["key"].Value)
            {
                case "normalised_text":
                    current.NormalisedText = Scalar(field.Groups["value"].Value, i, "normalised_text", errors);
                    current.NormalisedTextLine = i;
                    break;
                case "text":
                    current.Text = Scalar(field.Groups["value"].Value, i, "text", errors, tolerateBlock: true);
                    break;
                case "id":
                    current.ExistingId = Scalar(field.Groups["value"].Value, i, "id", errors);
                    current.IdLine = i;
                    break;
                case "supersedes":
                    current.SupersedesLine = i;
                    break;
                case "form":
                    current.Form = field.Groups["value"].Value.Trim() switch
                    {
                        "When" => AssertionForm.When,
                        "Never" => AssertionForm.Never,
                        var other => Unknown(other, i, errors),
                    };
                    break;
            }
        }

        return found;
    }

    private static AssertionForm Unknown(string value, int line, List<string> errors)
    {
        errors.Add($"line {line + 1}: `form: {value}` is neither When nor Never. There are exactly two canonical forms, and anything that fits neither is a clause that has not been decomposed.");
        return AssertionForm.Unstated;
    }

    /// <summary>
    /// A single-line YAML scalar, plain or double-quoted. <b>Block scalars are a refusal, not a skip</b> —
    /// the ID is a hash of this text, so reading it approximately is worse than not reading it.
    /// </summary>
    private static string Scalar(string raw, int line, string key, List<string> errors, bool tolerateBlock = false)
    {
        var value = raw.Trim();

        if (value.Length == 0)
        {
            errors.Add($"line {line + 1}: `{key}:` has no value on the same line. This reader does not implement block scalars, and an ID hashed over a partially-read text would be silently wrong.");
            return string.Empty;
        }

        if (value[0] is '>' or '|')
        {
            if (tolerateBlock)
                return string.Empty;

            errors.Add($"line {line + 1}: `{key}:` is a block scalar ({value[0]}). Refused rather than approximated — the ID is a hash of this exact text.");
            return string.Empty;
        }

        if (value[0] == '\'')
        {
            errors.Add($"line {line + 1}: `{key}:` is single-quoted. This reader implements plain and double-quoted scalars only, and guessing at an escaping rule would change a hash.");
            return string.Empty;
        }

        if (value[0] != '"')
            return value;

        if (value.Length < 2 || value[^1] != '"')
        {
            errors.Add($"line {line + 1}: `{key}:` opens a double-quoted scalar that does not close on the same line.");
            return string.Empty;
        }

        var body = value[1..^1];
        var sb = new StringBuilder(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '\\' && i + 1 < body.Length)
            {
                var next = body[++i];
                sb.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => next,
                });
                continue;
            }

            sb.Append(body[i]);
        }

        return sb.ToString();
    }

    private static void Validate(IReadOnlyList<Raw> found, List<string> errors)
    {
        foreach (var a in found)
        {
            if (a.NormalisedText.Length == 0)
            {
                errors.Add($"{a.ClauseId}.{a.Ordinal} (line {a.StartLine + 1}) has no `normalised_text:`. There is nothing to hash, and an ID over an empty string is the same ID for every empty assertion.");
                continue;
            }

            // The enumerator hand-writes `normalised_text:`. If it is not the normalisation of `text:`,
            // the ID names something the assertion does not say — caught here rather than discovered by
            // whoever later cannot reproduce the hash.
            if (AssertionId.Normalise(a.NormalisedText) != a.NormalisedText)
            {
                errors.Add($"{a.ClauseId}.{a.Ordinal}: `normalised_text:` is not itself normalised (§3.1: trim, collapse whitespace runs, strip one trailing '.' or ';'). Normalising it here would silently change the published ID.");
            }

            if (a.Text is { Length: > 0 } && AssertionId.Normalise(a.Text) != AssertionId.Normalise(a.NormalisedText))
            {
                errors.Add($"{a.ClauseId}.{a.Ordinal}: `text:` does not normalise to `normalised_text:`. The ID would name a sentence the assertion does not contain."
                    + Environment.NewLine + $"      text normalises to: {AssertionId.Normalise(a.Text)}"
                    + Environment.NewLine + $"      normalised_text is: {a.NormalisedText}");
            }

            if (a.Form != AssertionForm.Unstated && !AssertionId.TextMatchesForm(a.NormalisedText, a.Form))
            {
                errors.Add($"{a.ClauseId}.{a.Ordinal}: the text does not wear its declared form {a.Form} (WHEN … THEN … / NEVER … with no THEN). "
                    + "The stamped file is what gets published and cited, and F-3's enforcement rests on the form.");
            }
        }

        // *** A DUPLICATE, NOT A COLLISION (§3.1). *** Two assertions in one clause that normalise
        // identically are one assertion written twice — an error in the enumeration, reported as such,
        // never two units sharing an ID.
        foreach (var clause in found.GroupBy(a => a.ClauseId, StringComparer.Ordinal))
        {
            foreach (var duplicate in clause
                         .Where(a => a.NormalisedText.Length > 0)
                         .GroupBy(a => a.NormalisedText, StringComparer.Ordinal)
                         .Where(g => g.Count() > 1))
            {
                errors.Add($"{clause.Key}: assertions {string.Join(" and ", duplicate.Select(d => d.Ordinal))} normalise identically. "
                    + "That is a DUPLICATE in the enumeration, not a hash collision — they would share an ID, and the denominator would count one behaviour twice.");
            }
        }
    }

    private static string Write(List<string> lines, IReadOnlyList<Raw> found, IReadOnlyList<StampedAssertion> stamped, bool crlf)
    {
        // Edits are applied from the BOTTOM UP so earlier line indices stay valid.
        var edits = found
            .Select((raw, i) => (Raw: raw, Stamped: stamped[i]))
            .OrderByDescending(e => e.Raw.NormalisedTextLine)
            .ToArray();

        var indent = new string(' ', 8);

        foreach (var (raw, entry) in edits)
        {
            if (raw.IdLine >= 0)
            {
                lines[raw.IdLine] = $"{indent}id: {entry.Id}";

                if (entry.ReStamped)
                {
                    var supersedes = $"{indent}supersedes: {entry.PreviousId}";
                    if (raw.SupersedesLine >= 0)
                        lines[raw.SupersedesLine] = supersedes;
                    else
                        lines.Insert(raw.IdLine + 1, supersedes);
                }

                continue;
            }

            lines.Insert(raw.NormalisedTextLine + 1, $"{indent}id: {entry.Id}");
        }

        // The enumerator leaves `assertions: "<n> computed IDs>"` in its gate projection as a placeholder.
        // Filling it is the whole point of stamping: the projection is what the gate consumes.
        var placeholderAt = lines.FindIndex(ProjectionPlaceholder.IsMatch);
        if (placeholderAt >= 0)
        {
            lines[placeholderAt] = "  assertions: [" + string.Join(", ", stamped.Select(a => a.Id)) + "]";
        }

        var text = string.Join("\n", lines);
        return crlf ? text.Replace("\n", "\r\n") : text;
    }

    private static StampResult Refuse(List<string> errors, string? add = null)
    {
        if (add is not null)
            errors.Add(add);

        return new StampResult(null, Array.Empty<StampedAssertion>(), errors,
            $"NOTHING WAS STAMPED. {errors.Count} refusal(s). An unstamped enumeration has nothing citable in it, and a citation into one is a hard error rather than a lookup miss (§3.4).");
    }
}
