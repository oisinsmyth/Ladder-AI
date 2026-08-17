namespace Harness.Cleanup;

/// <summary>
/// <b>DB-7's first rule, as a document that must exist and must be whole.</b>
///
/// <para><i>"It runs when tests are DRAINED"</i>, and <i>"if drain state is unknown, that is a REFUSAL,
/// not an assumption"</i>. <see cref="Harness.Results.Cleanup.Plan"/> already refuses a <c>null</c>
/// in-flight set and honours an EMPTY one, so the whole job here is to make sure those two cannot be
/// confused on disk.</para>
///
/// <para><b>Why not a <c>--drained</c> flag.</b> That would be the caller supplying the verdict, which
/// is what <c>Cleanup.Plan</c>'s signature was deliberately shaped to refuse. Why not a bare list file:
/// because an EMPTY FILE and a TRUNCATED WRITE are byte-identical at zero length, and reading the
/// second as "nothing in flight" is the failure mode that ends with a running test's instance DB
/// deleted. So the format carries its own count and its own terminator, and a file that does not add up
/// is unusable rather than empty — the same shape as <c>wave-slots.state</c>'s <c>slots=n … end</c>.</para>
///
/// <para>🔴 <b>THE CEILING, AND IT IS PRINTED WHERE A READER OF RESULTS MEETS IT.</b> This is a
/// DECLARATION, not a measurement. Nothing in this repository computes the in-flight set today, so the
/// document names its author and the moment it was taken and this tool echoes both. A component that
/// cannot execute the thing it is asking about can demand an author, demand a timestamp and detect a
/// truncation. <b>It cannot make a false declaration true.</b></para>
/// </summary>
/// <param name="ComputedBy">Who established this. An empty value is a parse failure, never a default.</param>
/// <param name="TestsInFlight">
/// The outstanding tests. <b>Empty is a positive statement</b> — "the drain ran and nothing is
/// outstanding" — and is only reachable through a document that declared the count as zero.
/// </param>
public sealed record DrainReport(string ComputedBy, string ComputedAt, IReadOnlyList<string> TestsInFlight)
{
    public const int SupportedFormat = 1;

    /// <summary>
    /// Parses the drain report. Throws <see cref="CleanupInputException"/> on anything that is not a
    /// complete, self-consistent document — the caller turns that into a refusal.
    /// </summary>
    public static DrainReport Parse(string text, string label)
    {
        string? format = null, by = null, at = null, declared = null;
        var tests = new List<string>();
        var sawEnd = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim('﻿', '\r', ' ', '\t');
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line == "end") { sawEnd = true; continue; }
            if (sawEnd)
                throw new CleanupInputException($"{label}: content after 'end' ({line}). The terminator is what makes a truncated file distinguishable from an empty one, so it may not be in the middle.");

            var split = line.IndexOf('=');
            if (split <= 0)
                throw new CleanupInputException($"{label}: line '{line}' is neither a key=value nor 'end'.");

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();

            switch (key)
            {
                case "format": format = value; break;
                case "computed-by": by = value; break;
                case "computed-at": at = value; break;
                case "in-flight": declared = value; break;
                case "test": tests.Add(value); break;
                default:
                    // A key nobody recognises is a hard error, not a skip: the likeliest cause is a
                    // NEWER producer whose extra field changes what the file means, and silently
                    // dropping it would read that file as though the field were absent.
                    throw new CleanupInputException($"{label}: unknown key '{key}'. A key this reader does not understand may change what the document means, so it is refused rather than skipped.");
            }
        }

        if (!sawEnd)
            throw new CleanupInputException($"{label}: no 'end' line. The file is truncated or still being written, and a truncated drain report reads as 'nothing in flight' - which is the one wrong answer that deletes something a running test depends on.");
        if (format is null || !int.TryParse(format, out var formatNumber))
            throw new CleanupInputException($"{label}: no 'format=' line.");
        if (formatNumber != SupportedFormat)
            throw new CleanupInputException($"{label}: format={formatNumber}, and this reader understands {SupportedFormat}. Refused rather than read optimistically.");
        if (string.IsNullOrWhiteSpace(by))
            throw new CleanupInputException($"{label}: no 'computed-by='. DB-7 records on whose authority, and a drain state with no declarer cannot be attributed to anyone.");
        if (string.IsNullOrWhiteSpace(at))
            throw new CleanupInputException($"{label}: no 'computed-at='. A prediction inherits the age of the fact it rests on, so an undated drain report is one whose age cannot be judged.");
        if (declared is null || !int.TryParse(declared, out var declaredCount))
            throw new CleanupInputException($"{label}: no 'in-flight=<n>' line. The count is what makes a truncated list detectable.");

        if (declaredCount != tests.Count)
            throw new CleanupInputException(
                $"{label}: declares in-flight={declaredCount} and carries {tests.Count} test line(s). "
                + "The two disagreeing is exactly the half-written file this count exists to catch, and the "
                + "dangerous direction - fewer lines than declared - is the one that reads as drained.");

        return new DrainReport(by!, at!, tests);
    }

    public bool IsDrained => TestsInFlight.Count == 0;
}
