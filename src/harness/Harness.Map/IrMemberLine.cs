namespace Harness.Map;

/// <summary>
/// 🔴 <b>THE MEMBER-LINE GRAMMAR, READ RATHER THAN RE-INVENTED — and it is a SECOND COPY of a grammar
/// whose first copy lives in <c>src/converter/Converter/Ir/DbMemberLineFormat.cs</c>.</b>
///
/// <para><b>Why a second copy exists at all.</b> <c>Harness.Map</c> takes no project reference (see its
/// own <c>.csproj</c>, and <c>Directory.Build.props</c> for ruling C2 — a converter reference is
/// PERMITTED but pulls <c>WaveControl</c> into the harness build graph behind an <c>Exe</c>, which is a
/// build-layout decision this file is not the place to take). So the projection here reads the same
/// lines the converter writes, using the same fixed delimiters, in the same peel order.</para>
///
/// <para>🔴 <b>WHAT KEEPS THE COPY FROM DRIFTING: IT REFUSES WHAT IT DOES NOT UNDERSTAND.</b> Every
/// method here either recognises a line completely or throws. A grammar this file has fallen behind on
/// therefore surfaces as a REFUSAL naming the line, never as a member projected wrong — which is the one
/// failure mode a duplicated parser must not have. <c>IrMemberLineGrammarTests</c> pins the token order
/// against lines copied verbatim out of the committed corpus.</para>
///
/// <para>The grammar, from <c>DbMemberLineFormat.SerializeLine</c>:
/// <c>&lt;name&gt; : &lt;Datatype&gt;[ BAREPARAM][ INFORMATIVE "…"][ VERSION &lt;v&gt;][ RETAIN][ SETPOINT]
/// [ EXTERNALACCESSIBLE=FALSE][ EXTERNALVISIBLE=FALSE][ EXTERNALWRITABLE=FALSE][ = &lt;start&gt;]
/// [ COMMENT "…"]</c>. <c>COMMENT</c> is appended LAST specifically so it can be peeled FIRST — comment
/// prose routinely contains <c>=</c>, and peeling in the other order misreads it as the start-value
/// separator.</para>
/// </summary>
/// <param name="Indent">The line's leading spaces, verbatim. Nesting depth is indentation and nothing else.</param>
/// <param name="Name">The member's own name.</param>
/// <param name="Body">
/// Everything after <c> : </c> with the comment and the start value already peeled off — the datatype
/// and its markers, as one opaque string. <b>Opaque is deliberate</b>: a multi-word datatype
/// (<c>Array[0..14] of Bool</c>) never needs escaping because nothing here splits on whitespace.
/// </param>
/// <param name="StartValue">The declared start value, or null. 🔴 <b>This is the PRESET</b> — see <see cref="InstanceDbGenerator"/>.</param>
internal sealed record IrMemberLine(string Indent, string Name, string Body, string? StartValue)
{
    /// <summary>The datatype with every trailing marker stripped — what the projectability rule keys on.</summary>
    public string Datatype
    {
        get
        {
            var rest = Body;
            foreach (var marker in TrailingMarkers)
            {
                if (rest.EndsWith(marker, StringComparison.Ordinal))
                    rest = rest[..^marker.Length];
            }

            var version = rest.IndexOf(" VERSION ", StringComparison.Ordinal);
            if (version >= 0)
                rest = rest[..version];

            return rest;
        }
    }

    /// <summary>True when the member declares a <c>VERSION</c> — the marker of a SYSTEM TYPE instance.</summary>
    public bool HasVersion => Body.Contains(" VERSION ", StringComparison.Ordinal);

    /// <summary>Re-emit, with the start value replaced (or dropped when null). The comment is never re-emitted.</summary>
    public string Render(string? startValue) =>
        Indent + Name + " : " + Body + (startValue is null ? string.Empty : " = " + startValue);

    // Peeled rightmost-first, exactly as DbMemberLineFormat.ParseLine peels them.
    private static readonly string[] TrailingMarkers =
    {
        " EXTERNALWRITABLE=FALSE", " EXTERNALVISIBLE=FALSE", " EXTERNALACCESSIBLE=FALSE", " SETPOINT", " RETAIN",
    };
}

/// <summary>Reads the member-line grammar and the section headers around it. See <see cref="IrMemberLine"/>.</summary>
internal static class IrMemberLineReader
{
    /// <summary>
    /// One member line, or a throw. <b>A line this does not fully recognise is an error, not a skip</b>:
    /// a member silently dropped from an instance DB is a member the block writes into somebody else's
    /// storage.
    /// </summary>
    public static IrMemberLine Parse(string line)
    {
        var indent = line[..(line.Length - line.TrimStart(' ').Length)];
        var content = line[indent.Length..];

        var colon = content.IndexOf(" : ", StringComparison.Ordinal);
        if (colon < 0)
        {
            throw new ArgumentException(
                $"'{line.Trim()}' is not a member line the projection understands. The grammar is "
                + "`<name> : <Datatype>[ markers][ = <start>][ COMMENT \"…\"]`, and this line has no ` : ` at all. "
                + "The generator refuses rather than guessing what the line meant.");
        }

        var name = content[..colon];
        var rest = content[(colon + 3)..];

        // COMMENT first — it is the last token appended and free-text prose contains '='.
        var comment = rest.LastIndexOf(" COMMENT \"", StringComparison.Ordinal);
        if (comment >= 0 && rest.EndsWith('"'))
            rest = rest[..comment];

        string? startValue = null;
        var equals = rest.IndexOf(" = ", StringComparison.Ordinal);
        if (equals >= 0)
        {
            startValue = rest[(equals + 3)..];
            rest = rest[..equals];
        }

        return new IrMemberLine(indent, name, rest, startValue);
    }

    /// <summary>
    /// An array element's start value — <c>[&lt;path&gt;] = &lt;value&gt;</c>. 🔴 <b>An array has no scalar
    /// start value of its own, so this is the ONLY place an array's initial data lives</b>; a projection
    /// that walked past these would zero a configuration table silently, which is a defect the converter
    /// has already paid for once.
    /// </summary>
    public static (string Path, string Value) ParseSubelement(string line)
    {
        var content = line.TrimStart(' ');
        var close = content.IndexOf("] = ", StringComparison.Ordinal);
        if (!content.StartsWith('[') || close < 0)
        {
            throw new ArgumentException(
                $"'{content}' is not an array start-value line. The grammar is `[<path>] = <value>`.");
        }

        return (content[1..close], content[(close + 4)..]);
    }

    /// <summary>True for an array element start-value line rather than a nested member line.</summary>
    public static bool IsSubelement(string line) => line.TrimStart(' ').StartsWith('[');
}
