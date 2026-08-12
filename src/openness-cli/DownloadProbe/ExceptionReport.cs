using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Siemens.Engineering;

namespace DownloadProbe;

/// <summary>
/// Renders an exception AND EVERY EXCEPTION INSIDE IT.
///
/// THIS EXISTS BECAUSE THE TOOL ONCE THREW AWAY THE ONE LINE THAT MATTERED. A live run logged
/// <c>TargetInvocationException: Exception has been thrown by the target of an invocation.</c> and
/// stopped there. That sentence is the reflection layer's own boilerplate — it is emitted verbatim by
/// <see cref="TargetInvocationException"/> for every failure it ever wraps, and it carries no
/// information whatsoever about what actually went wrong. The real cause was in
/// <c>InnerException</c>, and the log did not contain the word "Inner", the arrow "--->" or an
/// HResult anywhere. A tool whose entire purpose is to make an undocumented API legible had discarded
/// the only legible part of the failure.
///
/// So: every catch site in this program renders through here, and the rendering walks the whole
/// chain. <c>--->
/// </c> is used as the level separator in <see cref="Summarise"/> deliberately, because that is what
/// .NET's own <c>ToString()</c> uses and therefore what someone greps a log for.
///
/// Three chain shapes are followed, not just the obvious one:
///   * <c>InnerException</c> — the ordinary case, and the one that was being lost.
///   * <see cref="AggregateException.InnerExceptions"/> — several causes, of which
///     <c>InnerException</c> alone would report the first and silently drop the rest.
///   * <see cref="ReflectionTypeLoadException.LoaderExceptions"/> — which hangs off no
///     <c>InnerException</c> at all, so a chain walk that ignored it would render "Unable to load one
///     or more of the requested types" and nothing else. That is the same class of loss all over
///     again.
///
/// AND A FOURTH CHANNEL THAT IS NOT A .NET ONE, MEASURED AGAINST THE INSTALLED V20 ASSEMBLY.
/// <c>Siemens.Engineering.EngineeringException</c> DOES NOT USE <c>InnerException</c> AT ALL. Its
/// public <c>(string text, Exception exception)</c> constructor folds the exception's message into
/// its own <c>Message</c> as extra lines and DISCARDS THE EXCEPTION OBJECT — constructed live, such
/// an exception reports <c>InnerException == null</c>. So a reader who checked <c>InnerException</c>
/// on an Openness abort and found nothing would conclude there was no further detail, and be wrong
/// twice over: the folded text is in <c>Message</c> (which must therefore be rendered in full, every
/// line, never just the first), and Openness' actual structured detail is in its own
/// <c>MessageData</c>/<c>DetailMessageData</c> properties, which nothing in this program used to
/// read.
/// </summary>
internal static class ExceptionReport
{
    /// <summary>Depth guard. A chain this long is a bug in itself; truncating is stated, never silent.</summary>
    private const int MaxDepth = 24;

    /// <summary>
    /// The full rendering: one block per level, with type, message, HResult, source, throwing member
    /// and stack trace. Returned as lines so the caller controls where they go, and so a test can
    /// assert on them without a log file.
    /// </summary>
    internal static IReadOnlyList<string> Describe(Exception? exception, string indent = "")
    {
        if (exception is null)
        {
            return new[] { indent + "(no exception)" };
        }

        var lines = new List<string>();
        var level = 0;

        foreach (var (label, ex) in Walk(exception))
        {
            level++;
            if (level > MaxDepth)
            {
                lines.Add($"{indent}[{level}+] *** CHAIN TRUNCATED at {MaxDepth} levels — the rest is not shown. ***");
                break;
            }

            lines.Add($"{indent}[{level}] {label}{ex.GetType().FullName ?? ex.GetType().Name}");
            AddBlock(lines, $"{indent}    message : ", ex.Message);
            lines.Add($"{indent}    hresult : 0x{ex.HResult:X8}");
            lines.Add($"{indent}    source  : {ex.Source ?? "(null)"}");
            lines.Add($"{indent}    site    : {DescribeSite(ex)}");
            AddEngineeringDetail(lines, ex, indent);
            AddBlock(lines, $"{indent}    stack   : ", ex.StackTrace ?? "(no stack trace — the exception was never thrown, or was rethrown)");
        }

        return lines;
    }

    /// <summary>
    /// Openness' own detail channel, for the exceptions that have one. This is where an
    /// <c>EngineeringException</c> keeps what other libraries would put in <c>InnerException</c> —
    /// see the note on the class. Read defensively: a rendering that threw while explaining a failure
    /// would lose the failure.
    /// </summary>
    private static void AddEngineeringDetail(List<string> lines, Exception exception, string indent)
    {
        if (exception is not EngineeringException engineering)
        {
            return;
        }

        try
        {
            // ExceptionMessageData is a STRUCT, so there is nothing to null-check on the entries —
            // an absent detail is an empty string, not a null reference.
            if (!string.IsNullOrEmpty(engineering.MessageData.DetailText))
            {
                AddBlock(lines, $"{indent}    detail  : ", engineering.MessageData.DetailText);
            }

            var details = engineering.DetailMessageData;
            if (details is not { Count: > 0 })
            {
                return;
            }

            lines.Add($"{indent}    openness detail messages ({details.Count}) — this type's substitute for InnerException:");
            var ordinal = 0;
            foreach (var detail in details)
            {
                ordinal++;
                AddBlock(lines, $"{indent}      [{ordinal}] text   : ", detail.Text ?? "(none)");
                if (!string.IsNullOrEmpty(detail.DetailText))
                {
                    AddBlock(lines, $"{indent}      [{ordinal}] detail : ", detail.DetailText);
                }
            }
        }
        catch (Exception readFailure)
        {
            lines.Add($"{indent}    <<reading EngineeringException detail FAILED: {readFailure.GetType().Name}: {readFailure.Message}>>");
        }
    }

    /// <summary>
    /// The one-line form, for a summary line that must still name the real cause: every level's type
    /// and first message line, joined by <c>---&gt;</c>.
    /// </summary>
    internal static string Summarise(Exception? exception)
    {
        if (exception is null)
        {
            return "(no exception)";
        }

        var parts = Walk(exception)
            .Take(MaxDepth)
            .Select(entry => $"{entry.Exception.GetType().Name}: {FirstLine(entry.Exception.Message)}");

        return string.Join(" ---> ", parts);
    }

    /// <summary>
    /// The innermost exception on the primary <c>InnerException</c> spine — the real cause once the
    /// reflection wrappers are stripped. Never null: an exception with no inner is its own cause.
    /// </summary>
    internal static Exception Unwrap(Exception exception)
    {
        var current = exception;
        for (var depth = 0; depth < MaxDepth && current.InnerException is { } inner; depth++)
        {
            current = inner;
        }

        return current;
    }

    /// <summary>
    /// True when the exception is only a wrapper the runtime added — i.e. its message tells the
    /// reader nothing and the level below it is the answer. Used to say so in the log rather than
    /// leaving a reader to wonder why the top line is uninformative.
    /// </summary>
    internal static bool IsReflectionWrapper(Exception exception) =>
        exception is TargetInvocationException && exception.InnerException is not null;

    /// <summary>Writes the full rendering to the log under a heading.</summary>
    internal static void Write(ProbeLog log, string heading, Exception? exception, string indent = "")
    {
        log.Line(indent + heading);
        if (exception is not null && IsReflectionWrapper(exception))
        {
            log.Line(indent + "  (level [1] is the reflection wrapper — its message is boilerplate and says nothing;");
            log.Line(indent + "   the cause is the level below it.)");
        }

        foreach (var line in Describe(exception, indent + "  "))
        {
            log.Line(line);
        }
    }

    /// <summary>
    /// Depth-first over the three chain shapes, each entry labelled with where it came from so a
    /// reader can tell "inner of the one above" from "one of several aggregated causes".
    /// </summary>
    private static IEnumerable<(string Label, Exception Exception)> Walk(Exception root)
    {
        var seen = new List<Exception>();
        return WalkFrom(root, string.Empty, seen);
    }

    private static IEnumerable<(string Label, Exception Exception)> WalkFrom(
        Exception exception, string label, List<Exception> seen)
    {
        // Reference-equality guard: an exception that contains itself would otherwise loop forever.
        if (seen.Any(s => ReferenceEquals(s, exception)) || seen.Count > MaxDepth)
        {
            yield break;
        }

        seen.Add(exception);
        yield return (label, exception);

        if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
        {
            var ordinal = 0;
            foreach (var inner in aggregate.InnerExceptions)
            {
                ordinal++;
                foreach (var entry in WalkFrom(inner, $"(aggregated cause {ordinal} of {aggregate.InnerExceptions.Count}) ", seen))
                {
                    yield return entry;
                }
            }

            yield break;
        }

        if (exception is ReflectionTypeLoadException typeLoad && typeLoad.LoaderExceptions is { Length: > 0 })
        {
            var ordinal = 0;
            foreach (var loader in typeLoad.LoaderExceptions)
            {
                ordinal++;
                if (loader is null)
                {
                    continue;
                }

                foreach (var entry in WalkFrom(loader, $"(loader exception {ordinal} of {typeLoad.LoaderExceptions.Length}) ", seen))
                {
                    yield return entry;
                }
            }
        }

        if (exception.InnerException is { } single)
        {
            foreach (var entry in WalkFrom(single, "(inner) ", seen))
            {
                yield return entry;
            }
        }
    }

    private static string DescribeSite(Exception exception)
    {
        try
        {
            return exception.TargetSite is { } site
                ? $"{site.DeclaringType?.FullName ?? "(unknown type)"}.{site.Name}"
                : "(unknown)";
        }
        catch (Exception ex)
        {
            // TargetSite resolves metadata and can itself throw when the declaring assembly is not
            // loadable. Reporting that beats losing the whole rendering to it.
            return $"<<READ FAILED: {ex.GetType().Name}>>";
        }
    }

    /// <summary>
    /// Multi-line values one log line at a time, so an embedded newline in a Siemens message or a
    /// stack trace cannot break the file's line structure — and nothing is trimmed to fit.
    /// </summary>
    private static void AddBlock(List<string> lines, string prefix, string text)
    {
        var parts = text.Replace("\r\n", "\n").Split('\n');
        var continuation = new string(' ', prefix.Length - 2) + "| ";
        for (var i = 0; i < parts.Length; i++)
        {
            lines.Add((i == 0 ? prefix : continuation) + parts[i]);
        }
    }

    private static string FirstLine(string text)
    {
        var newline = text.IndexOfAny(new[] { '\r', '\n' });
        return newline < 0 ? text : text.Substring(0, newline) + " [...]";
    }
}
