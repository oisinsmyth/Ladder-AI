using System.Text;

namespace Harness.Verify;

/// <summary>
/// 🔴 <b>TEXT THIS TOOL DID NOT WRITE, MADE SAFE TO PRINT — because a report that quotes another component
/// inherits that component's encoding hazard.</b>
///
/// <para><b>Measured on this tool's own first live run, and it is the half an ASCII rule over one's own
/// literals does not reach.</b> Every literal in <c>Harness.Verify</c> is ASCII, and the report was still
/// mangled: <c>VersionCheck</c>'s <c>Stale</c> detail carries an em dash, and so do
/// <c>MirrorClient</c>'s refusal, <c>GuardDecision</c>'s message and half the exception texts in the
/// harness. Those belong to other assemblies, several of which other lanes are working in, and reaching
/// into them to change prose would be both out of scope and a merge hazard.</para>
///
/// <para><b>The conversion is FAITHFUL AND VISIBLE, never lossy-and-quiet.</b> Known punctuation is mapped
/// to its obvious ASCII equivalent; anything else is printed as <c>\uXXXX</c> rather than as a question
/// mark. A substitution character would turn "this text had something here" into "this text is like that",
/// which is the same silent-degradation this class exists to stop.</para>
///
/// <para><b>It is applied ONLY to foreign text</b> — detail strings, refusals, exception messages, guard
/// decisions. This tool's own literals are held to ASCII directly, and pinned by <c>OutputEncodingTests</c>,
/// so nothing here can quietly become the place the rule is enforced.</para>
/// </summary>
public static class Ascii
{
    private static readonly Dictionary<char, string> Known = new()
    {
        ['—'] = "-",     // em dash
        ['–'] = "-",     // en dash
        ['‘'] = "'",     // left single quote
        ['’'] = "'",     // right single quote
        ['“'] = "\"",    // left double quote
        ['”'] = "\"",    // right double quote
        ['…'] = "...",   // ellipsis
        ['§'] = "section ",
        ['\u00A0'] = " ",     // non-breaking space
        ['→'] = "->",
        ['←'] = "<-",
        ['·'] = "*",     // middle dot
        ['•'] = "*",     // bullet
    };

    /// <summary>Convert foreign text to ASCII. Null becomes an empty string, never the word "null".</summary>
    public static string Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // The overwhelmingly common case, and worth not allocating for: the text is already ASCII.
        var needsWork = false;
        foreach (var c in text)
        {
            if (c > '') { needsWork = true; break; }
        }

        if (!needsWork)
            return text;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c <= '')
                builder.Append(c);
            else if (Known.TryGetValue(c, out var replacement))
                builder.Append(replacement);
            else
                builder.Append("\\u").Append(((int)c).ToString("X4"));
        }

        return builder.ToString();
    }
}
