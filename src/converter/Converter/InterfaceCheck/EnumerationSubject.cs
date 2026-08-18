namespace Converter.InterfaceCheck;

/// <summary>
/// Reads an assertion enumeration's <b>own declared subject</b>, and decides whether it agrees with
/// the subject a caller asserted.
///
/// <para>🔴 <b>WHY THIS EXISTS: <c>interface-check</c> COULD PRODUCE A FALSE ACCUSATION AGAINST A
/// BLOCK.</b> <c>--requires-file</c> scrapes every <c>response_signal:</c> value out of ONE
/// enumeration and never asked what that enumeration was ABOUT, nor compared it to
/// <c>--block</c>. Two enumerations now exist carrying 28 and 15 distinct response signals with an
/// overlap of 2, so aiming a block at the wrong one demands about 26 signals that cannot be present:
/// ~26 MISSING and <c>exit 1</c>, which in this tool's contract is a <b>FAIL AGAINST THE BLOCK</b>.
/// The provenance line named the PATH only, and a path is exactly what a person has already
/// mis-typed.</para>
///
/// <para><b>AND NOTHING DISTINGUISHED IT FROM A REAL FINDING.</b> On this corpus's house style
/// (C-132) an FB's INPUT and OUTPUT sections are both empty and the whole interface is one STATIC
/// UDT, so "nearly every signal missing" is a shape the runner's own summary predicts as a genuine
/// output. A wrong file and a catastrophic block defect rendered identically.</para>
///
/// <para><b>THE OUTCOME IS DELIBERATELY 2, NOT 1.</b> A wrong enumeration is an unjudgeable INPUT,
/// not a defective block — the same ruling as an unparseable required name — and this check's exit
/// contract already reserves 2 for exactly that. Getting this wrong would have the tool blame the
/// one artifact that was correct.</para>
/// </summary>
public static class EnumerationSubject
{
    /// <summary>
    /// The document's top-level <c>subject:</c>, whitespace-normalised, or null when it declares none.
    ///
    /// <para>Handles the plain scalar (<c>subject: VALVE BEHAVIOUR</c>) and YAML's folded/literal block
    /// forms (<c>subject: &gt;-</c> followed by indented lines), which is the form the real artifacts
    /// use. <b>Top-level only, keyed on column zero</b> — an enumeration also carries a per-assertion
    /// <c>subject:</c> under several levels of indentation, and pooling those would make the document's
    /// declared subject depend on which assertion happened to be last.</para>
    /// </summary>
    public static string? Parse(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.StartsWith("subject:", StringComparison.Ordinal))
            {
                continue; // indented, or a different key: not the document's own subject
            }

            var inline = line["subject:".Length..].Trim();

            // A block scalar indicator opens a continuation: the value is the indented lines that
            // follow, not the indicator itself.
            if (inline is ">" or ">-" or ">+" or "|" or "|-" or "|+" or "")
            {
                var collected = new List<string>();
                for (var j = i + 1; j < lines.Count; j++)
                {
                    var next = lines[j];
                    if (next.Trim().Length == 0)
                    {
                        continue;
                    }

                    // A line at column zero ends the block — it is the next top-level key.
                    if (!char.IsWhiteSpace(next[0]))
                    {
                        break;
                    }

                    collected.Add(next.Trim());
                }

                var text = Normalise(string.Join(" ", collected));
                return text.Length == 0 ? null : text;
            }

            var scalar = Normalise(inline.Trim('"', '\''));
            return scalar.Length == 0 ? null : scalar;
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="declared"/> (the file's own words) and <paramref name="asserted"/>
    /// (what the caller said the file should be about) are about the same thing.
    ///
    /// <para><b>Containment in either direction, case-insensitive, whitespace-normalised — and the
    /// rule is stated rather than tuned.</b> A declared subject is a paragraph of prose and an
    /// asserted one is a phrase, so equality would refuse every real file; containment lets
    /// <c>--subject "VALVE BEHAVIOUR"</c> agree with a paragraph that opens with those words. The
    /// direction of error is what makes a coarse rule safe here: disagreement costs a re-run with a
    /// better string, and the flag is opt-in, so nothing is forced through it.</para>
    /// </summary>
    public static bool Agrees(string declared, string asserted)
    {
        var a = Normalise(declared);
        var b = Normalise(asserted);
        return a.Length > 0 && b.Length > 0
            && (a.Contains(b, StringComparison.OrdinalIgnoreCase)
                || b.Contains(a, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Collapses every whitespace run to one space and trims.</summary>
    public static string Normalise(string text) =>
        string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
