using System.Text.RegularExpressions;

namespace Converter.TargetScan;

// Parses a requirements.md register (gen/<project>/requirements.md) into the structured facts
// target-scan cross-joins. The register's own "## Format" section is a strict contract — REQs are
// `### REQ-nnn — <title>` with fixed `- **Class:**` / `- **Notes:**` bullets; open questions are
// `- **Q-nn — <title>. <STATUS>...` under a `## Open questions` heading — so a focused line/regex
// parser is bounded and safe (no general markdown parsing). It extracts only what the join needs;
// everything semantic stays with the human.

public enum QuestionStatus
{
    Resolved,
    Open,
    Partial,
    Unknown,
}

public sealed record ParsedReq(
    string Id,
    string Title,
    string ReqClass,
    bool Withdrawn,
    IReadOnlyList<string> NamedTags,
    IReadOnlyList<string> LinkedQuestions);

public sealed record ParsedQuestion(string Id, QuestionStatus Status, string RawStatus);

public sealed record RequirementsDocument(
    IReadOnlyList<ParsedReq> Requirements,
    IReadOnlyDictionary<string, ParsedQuestion> Questions);

public static class RequirementsParser
{
    // Dash separators the register uses between an ID and its title: hyphen, en-dash, em-dash.
    private static readonly Regex ReqHeader = new(@"^###\s+(REQ-\d+)\s*[-–—]\s*(.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex QuestionBullet = new(@"^\s*-\s*\*\*(Q-\d+)\s*[-–—]\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex ClassBullet = new(@"^\s*-\s*\*\*Class:\*\*\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex Level2Or3Header = new(@"^#{2,3}\s", RegexOptions.Compiled);
    private static readonly Regex OpenQuestionsHeader = new(@"^##\s+Open questions\b", RegexOptions.Compiled);
    private static readonly Regex BacktickToken = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex QuestionRef = new(@"\bQ-\d+\b", RegexOptions.Compiled);
    // An identifier we would classify against the export: a tag/DB/block/UDT name, a DB.member path,
    // or a raw IO address. Excludes multi-word backtick phrases and Q-nn/REQ-nnn refs (the hyphen
    // isn't in the class), so prose and status words never leak in.
    private static readonly Regex Identifier = new(@"^[%A-Za-z_][A-Za-z0-9_.%]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> TagStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "exists", "proposed", "control", "alarm", "hmi", "mode", "timing", "withdrawn",
    };

    public static RequirementsDocument Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        var requirements = new List<ParsedReq>();
        var questions = new Dictionary<string, ParsedQuestion>(StringComparer.Ordinal);

        var inOpenQuestions = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (OpenQuestionsHeader.IsMatch(line))
            {
                inOpenQuestions = true;
                continue;
            }

            // Any other level-2 header closes the open-questions section.
            if (inOpenQuestions && line.StartsWith("## ", StringComparison.Ordinal) && !OpenQuestionsHeader.IsMatch(line))
            {
                inOpenQuestions = false;
            }

            if (inOpenQuestions)
            {
                var qMatch = QuestionBullet.Match(line);
                if (qMatch.Success)
                {
                    var id = qMatch.Groups[1].Value;
                    // Status lives on the bullet's own line (sometimes inside the bold span); scan the
                    // rest of the bullet too in case it wraps, stopping at the next bullet/header.
                    var body = CollectQuestionBody(lines, i, qMatch.Groups[2].Value);
                    var (status, raw) = ClassifyQuestion(body);
                    questions[id] = new ParsedQuestion(id, status, raw);
                }

                continue;
            }

            var reqMatch = ReqHeader.Match(line);
            if (reqMatch.Success)
            {
                var id = reqMatch.Groups[1].Value;
                var title = reqMatch.Groups[2].Value;
                var body = CollectReqBody(lines, i + 1);
                requirements.Add(BuildReq(id, title, body));
            }
        }

        return new RequirementsDocument(requirements, questions);
    }

    // Body of a REQ = every line after its header up to the next level-2/3 header.
    private static string CollectReqBody(string[] lines, int start)
    {
        var body = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            if (Level2Or3Header.IsMatch(lines[i]))
            {
                break;
            }

            body.Add(lines[i]);
        }

        return string.Join("\n", body);
    }

    // The Q bullet's own line plus any continuation lines (indented, non-bullet) until the next bullet
    // or header — so a status that wraps to the next line is still seen.
    private static string CollectQuestionBody(string[] lines, int headerIndex, string firstLineRest)
    {
        var body = new List<string> { firstLineRest };
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("## ", StringComparison.Ordinal) || QuestionBullet.IsMatch(line) ||
                Regex.IsMatch(line, @"^\s*-\s*\*\*"))
            {
                break;
            }

            body.Add(line);
        }

        return string.Join("\n", body);
    }

    private static ParsedReq BuildReq(string id, string title, string body)
    {
        var reqClass = "unknown";
        foreach (var line in body.Split('\n'))
        {
            var classMatch = ClassBullet.Match(line);
            if (classMatch.Success)
            {
                // Class value is a single token ("control", "out-of-scope"); strip any trailing prose.
                reqClass = classMatch.Groups[1].Value.Split(' ', ',', '.')[0].Trim();
                break;
            }
        }

        // Tolerate the register's markdown bold around the label: `- **Status:** withdrawn`.
        var withdrawn = Regex.IsMatch(body, @"Status:\**\s*withdrawn", RegexOptions.IgnoreCase);

        var namedTags = BacktickToken.Matches(body)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(IsClassifiableIdentifier)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var linkedQuestions = QuestionRef.Matches(body)
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(q => q, StringComparer.Ordinal)
            .ToList();

        return new ParsedReq(id, title, reqClass, withdrawn, namedTags, linkedQuestions);
    }

    private static bool IsClassifiableIdentifier(string token) =>
        token.Length >= 2 &&
        Identifier.IsMatch(token) &&
        !TagStopwords.Contains(token) &&
        !token.All(char.IsDigit);

    // "Still open" and "Partially resolved" are the two not-fully-resolved states; check them before
    // "RESOLVED" because "Partially resolved" contains "resolved". Anything with no recognizable
    // marker is Unknown (treated conservatively as blocking downstream, but shown as such).
    private static (QuestionStatus, string) ClassifyQuestion(string body)
    {
        if (body.Contains("Still open", StringComparison.OrdinalIgnoreCase))
        {
            return (QuestionStatus.Open, "Still open");
        }

        if (body.Contains("Partially resolved", StringComparison.OrdinalIgnoreCase))
        {
            return (QuestionStatus.Partial, "Partially resolved");
        }

        if (body.Contains("RESOLVED", StringComparison.OrdinalIgnoreCase))
        {
            return (QuestionStatus.Resolved, "RESOLVED");
        }

        return (QuestionStatus.Unknown, "unknown");
    }
}
