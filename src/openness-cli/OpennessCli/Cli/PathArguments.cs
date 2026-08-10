using System;
using System.IO;

namespace OpennessCli.Cli;

// FI-68. Openness rejects a RELATIVE path, and does it with an exception that names something else
// entirely: `openness-cli export --out <relative>` fails with EngineeringTargetInvocationException,
// which reads exactly like the "Inconsistent blocks and PLC data types (UDT) cannot be exported"
// refusal — a real and frequent condition on a live job. The agent that hit it only avoided a long
// detour because it happened to run sanity-check between attempts and got HEALTHY both times.
//
// The constraint was already known for the project-open path (docs/notes/openness-quirks.md, "Project
// paths": a relative path throws "The argument 'path' cannot be a relative path"). It applies to the
// EXPORT target too, and was neither handled nor documented.
//
// Resolved at the argument boundary rather than at each call site, so `--out`, the import file list
// and `--tia-install` are covered by one rule instead of a special case each. The standing preference
// on this project is one general fix over stacked special cases — and the failure mode being fixed is
// precisely that the NEXT path argument nobody thought about behaves the same way.
//
// THE PROJECT IDENTIFIER IS DELIBERATELY EXCLUDED, and that is not an oversight. It is documented as
// "either a full .apNN path or a BARE PROJECT NAME" (OpennessGateway.ProjectIdentifierMatches), which
// is how an already-open Portal session is matched. Resolving a bare name against the current
// directory would silently turn a name into a path that does not exist, so it is left as the user
// typed it. If a relative project path ever turns out to fail the same way, it needs its own fix that
// can tell a name from a path — not this one.
public static class PathArguments
{
    /// <summary>
    /// Resolves a user-supplied path against the current directory. A path that is already absolute
    /// is returned unchanged in meaning (normalised only), so this is safe to apply to every path
    /// argument unconditionally.
    ///
    /// Returns the input untouched when it cannot be resolved — an empty string, or a path holding
    /// characters the platform rejects. Refusing to resolve is deliberate: this helper exists to
    /// remove a confusing failure, and swallowing an invalid path into a different exception here
    /// would replace one misleading message with another. The real validation happens downstream,
    /// where the message can name the argument.
    /// </summary>
    public static string ToAbsolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return path;
        }
    }

    /// <summary>Null-tolerant <see cref="ToAbsolute(string)"/>, for optional path arguments.</summary>
    public static string? ToAbsoluteOrNull(string? path) => path is null ? null : ToAbsolute(path);
}
