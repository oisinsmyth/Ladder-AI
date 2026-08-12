using System.Text;

namespace Converter.Ir;

/// <summary>
/// The quoted-string escape used by every readable-IR text form — block/network `TITLE` and
/// `COMMENT`, a member's own `COMMENT`/`INFORMATIVE`, a tag comment, a `CALC` equation.
///
/// <para><b>Why it is one class and not five copies.</b> It WAS five copies of
/// <c>value.Replace("\\", "\\\\").Replace("\"", "\\\"")</c> and two of its inverse, spread across
/// <c>IrSerializer</c>, <c>DbIr</c>, <c>DbMemberLineFormat</c>, <c>TagTableIr</c>, <c>TypeIr</c> and
/// <c>IrParser</c> — so a fix to the escape had to be made, correctly, in seven places or the two
/// halves would silently disagree. Extending it for newlines (below) is exactly such a fix.</para>
///
/// <para><b>NEWLINES, added 2026-08-12.</b> Previously a raw <c>\n</c>/<c>\r</c> in a quoted value
/// was a HARD ERROR, on the correct reasoning that the whole `.ir` document is split on <c>'\n'</c>
/// before any quoted-string parsing runs, so an unescaped newline desyncs reparsing. What was
/// missing was the other half: no escape sequence existed, so the refusal was permanent rather than
/// a guard. **A real TIA V20 export settles that a multi-line comment is real** — an S7-1200 Modbus
/// TCP FB whose first network's comment is three lines of engineer's notes (baud rate, an error
/// code, a trailing blank). `to-ir` refused the whole block over it, which under the standing "no IR
/// the AI cannot change" ruling makes the block unmodifiable for a reason that is presentation, not
/// logic. So <c>\n</c> and <c>\r</c> now escape as <c>\\n</c>/<c>\\r</c> and the value survives the
/// round trip on one line.</para>
///
/// <para><b>An unrecognised escape is passed through verbatim</b> (backslash plus the character),
/// deliberately matching the behaviour of the naive <c>Replace</c> pair this supersedes — a
/// hand-authored `.ir` containing a stray <c>\t</c> read that way before and must keep reading that
/// way, or the change would silently rewrite existing comment text.</para>
/// </summary>
internal static class IrStringEscape
{
    public static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    public static string Unescape(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                sb.Append(value[i]);
                continue;
            }

            switch (value[i + 1])
            {
                case '\\': sb.Append('\\'); i++; break;
                case '"': sb.Append('"'); i++; break;
                case 'n': sb.Append('\n'); i++; break;
                case 'r': sb.Append('\r'); i++; break;
                default: sb.Append(value[i]); break;
            }
        }

        return sb.ToString();
    }
}
