namespace Converter;

/// <summary>
/// The dotted-tag-path syntax primitives every layer shares: where a path's component boundaries
/// actually are, and what a component's subscript is.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 <b>A '.' INSIDE A SUBSCRIPT IS NOT A COMPONENT BOUNDARY.</b> Splitting a tag path with a plain
/// <c>Split('.')</c> was safe for exactly as long as an array subscript could only be an integer
/// literal — an integer contains no dot. That stopped being true on 2026-08-27, when the converter
/// began accepting a VARIABLE subscript
/// (<see cref="SimaticMl.AccessNode.IsSymbolicIndex"/>): a symbolic index IS a dotted path, so
/// <c>DB_Config.Profile[iDB_Unit_A.Cycle.ChosenIndex]</c> cut into
/// <c>["…Profile[iDB_Unit_A", "Sequence", "ChosenIndex]"]</c> — pieces that name nothing.
/// </para>
/// <para>
/// The first repair (<c>AccessNode.FromDottedPath</c>, commit 1a6eed0) fixed one splitter with a
/// private copy of this logic. A live run then hit the OTHERS: <c>tagstatus</c> reported
/// "no member 'Sequence'" and <c>review</c> reported "C-005 Name component 'ChosenIndex]' contains
/// characters other than letters/digits/underscore" — both false positives on wiring
/// <c>tagstatus</c> itself confirms exists. Measured 2026-08-27: the identical edit with the
/// subscript written as a literal <c>[1]</c> produced zero findings, so the dotted index alone was
/// the trigger.
/// </para>
/// <para>
/// This type is the general fix, per this project's standing rule that a SECOND instance of a bug
/// class earns one shared mechanism rather than another special case (the same ruling that retired
/// <c>AccessNode</c>'s positional special-casing outright rather than extending it). It lives in the
/// root <c>Converter</c> namespace, dependency-free, because path syntax is a primitive rather than
/// a layer: <c>Converter.Review</c>, <c>Converter.TagStatus</c>, <c>Converter.CrossCheck</c> and
/// <c>Converter.Ir</c> all reach it by enclosing-namespace lookup with no <c>using</c> and, more to
/// the point, with no analysis layer taking a dependency on the SimaticML serialization model just
/// to learn where a dot is.
/// </para>
/// <para>
/// Nesting cannot occur — <c>IsSymbolicIndex</c> rejects a bracket inside an index — so a simple
/// depth counter is exact rather than a heuristic. An UNBALANCED bracket degrades to the old
/// whole-string behaviour (no separator is found past the unclosed '[') rather than silently
/// dropping text: a malformed path stays malformed and visible instead of becoming a different,
/// plausible-looking path.
/// </para>
/// </remarks>
public static class TagPath
{
    /// <summary>
    /// Split <paramref name="path"/> on '.' at bracket depth zero only, so a dotted symbolic
    /// subscript stays inside the component that carries it. Always yields at least one element.
    /// </summary>
    public static string[] Split(string path) => Split(path, int.MaxValue);

    /// <summary>
    /// <see cref="Split(string)"/>, capped at <paramref name="count"/> pieces — the bracket-aware
    /// generalization of <c>string.Split('.', count)</c>, merging any excess separators into the
    /// final piece. Used where the caller already knows the true component count and a path may
    /// carry a component whose own name contains a literal dot (the <c>Clock_0.5Hz</c> system tags).
    /// </summary>
    public static string[] Split(string path, int count)
    {
        if (count <= 1)
        {
            return new[] { path };
        }

        var components = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < path.Length && components.Count < count - 1; i++)
        {
            switch (path[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    if (depth > 0) depth--;
                    break;
                case '.' when depth == 0:
                    components.Add(path[start..i]);
                    start = i + 1;
                    break;
            }
        }

        components.Add(path[start..]);
        return components.ToArray();
    }

    /// <summary>
    /// Index of the FIRST component-boundary '.', or -1 when the path is a single component. Not the
    /// same as <c>string.IndexOf('.')</c> whenever the ROOT component carries a symbolic subscript
    /// (<c>Buffer[iDB.Slot].Value</c>), where the first raw dot sits inside the brackets.
    /// </summary>
    public static int IndexOfSeparator(string path)
    {
        var depth = 0;
        for (var i = 0; i < path.Length; i++)
        {
            switch (path[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    if (depth > 0) depth--;
                    break;
                case '.' when depth == 0:
                    return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Index of the LAST component-boundary '.', or -1 when the path is a single component. Not the
    /// same as <c>string.LastIndexOf('.')</c> whenever the LEAF component carries a symbolic
    /// subscript (<c>IO.Profile[iDB.Sequence.ChosenIndex]</c>), where the last raw dot sits inside
    /// the brackets and a naive leaf extraction returns <c>ChosenIndex]</c>.
    /// </summary>
    public static int LastIndexOfSeparator(string path)
    {
        var depth = 0;
        var last = -1;
        for (var i = 0; i < path.Length; i++)
        {
            switch (path[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    if (depth > 0) depth--;
                    break;
                case '.' when depth == 0:
                    last = i;
                    break;
            }
        }

        return last;
    }

    /// <summary>
    /// Remove every component's subscript, whatever is inside it: <c>A[3].B[iDB.Slot]</c> →
    /// <c>A.B</c>. A LITERAL and a SYMBOLIC index are stripped alike on purpose — the callers that
    /// use this are asking "which declared thing does this name?", and that answer cannot depend on
    /// how the element was selected. An unbalanced '[' leaves the path untouched.
    /// </summary>
    public static string StripSubscripts(string path)
    {
        if (path.IndexOf('[') < 0)
        {
            return path;
        }

        var sb = new System.Text.StringBuilder(path.Length);
        var depth = 0;

        foreach (var c in path)
        {
            switch (c)
            {
                case '[':
                    depth++;
                    break;
                case ']' when depth > 0:
                    depth--;
                    break;
                default:
                    if (depth == 0) sb.Append(c);
                    break;
            }
        }

        return depth == 0 ? sb.ToString() : path;
    }

    /// <summary>The subscript text of one COMPONENT (<c>Profile[iDB.Slot]</c> → <c>iDB.Slot</c>), or null.</summary>
    public static string? SubscriptOf(string component)
    {
        var open = component.IndexOf('[');
        var close = component.LastIndexOf(']');
        return open < 0 || close < open ? null : component[(open + 1)..close];
    }

    /// <summary>
    /// One COMPONENT without its subscript (<c>Profile[iDB.Slot]</c> → <c>Profile</c>). Cutting at the
    /// FIRST '[' is exact: the first '[' in a component is always the depth-zero opener, so this is
    /// correct for a symbolic index without needing depth tracking.
    /// </summary>
    public static string StripComponentSubscript(string component)
    {
        var idx = component.IndexOf('[');
        return idx < 0 ? component : component[..idx];
    }
}
