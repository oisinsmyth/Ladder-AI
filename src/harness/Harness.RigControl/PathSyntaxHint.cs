using System.Text.RegularExpressions;

namespace Harness.RigControl;

/// <summary>
/// A DIAGNOSTIC for a path that did not resolve — never a repair of one.
///
/// <para><b>The real user error it exists for, 2026-08-14.</b> The documented command was written in
/// <c>cmd</c> form and run in PowerShell:</para>
///
/// <code>
/// allowlist   : %USERPROFILE%\.ladder\device-allowlist.json
/// gate    : AllowlistUnusable
/// verdict : Allowlist file not found: %USERPROFILE%\.ladder\device-allowlist.json
/// </code>
///
/// <para><c>%VAR%</c> is <c>cmd</c>/batch syntax and PowerShell does not expand it, so the tool was
/// handed the literal string. *** THE FENCE BEHAVED CORRECTLY — it refused and opened nothing — AND
/// ECHOING THE LITERAL BACK IS WHAT MADE IT DIAGNOSABLE IN SECONDS. *** What was missing is only the
/// last step: saying WHY a path that looks right did not resolve.</para>
///
/// <para>🔴 <b>IT DOES NOT EXPAND THE VARIABLE, AND THAT IS THE WHOLE DESIGN.</b> A fence that repairs
/// its own input is a fence that can be argued with — the next person to hit a path problem would be
/// asking which of two strings was actually used, and the answer would depend on a repair rule nobody
/// reads. It refuses exactly as it did before and adds ONE LINE the reader can act on. This changes no
/// behaviour and no exit code; a run that refused before refuses identically now.</para>
///
/// <para><b>Gated on the file NOT EXISTING, not merely on the <c>%</c>.</b> <c>%</c> is a legal
/// character in a Windows filename, so a path that contains one and RESOLVES is somebody's real
/// directory and must be left alone. *** A DIAGNOSTIC THAT FIRES ON WORKING INPUT IS NOISE, AND NOISE
/// GETS SWITCHED OFF — after which the cases it was right about go through unhelped. *** There is a
/// test for the working case as deliberately as for the broken one.</para>
///
/// <para><b>CONSIDERED AND DELIBERATELY NOT BUILT: the mirror case</b> — a PowerShell-form path
/// (<c>$env:USERPROFILE\...</c>) pasted into <c>cmd</c>, which fails the same way in the other
/// direction. It is not built because *** AN ERROR MESSAGE'S ADVICE IS A CLAIM ***, and this lane has
/// not run that case; printing a <c>cmd</c> incantation nobody here has executed would be a guess
/// offered to somebody already in trouble. Build it the day somebody hits it and can confirm the
/// wording. This is a decision, not an omission.</para>
/// </summary>
public static class PathSyntaxHint
{
    /// <summary>
    /// Matches a <c>cmd</c>/batch variable reference: <c>%NAME%</c>, where NAME starts with a letter or
    /// underscore. Deliberately not <c>%.*%</c> — that would match <c>my%20file%20name</c> and other
    /// ordinary uses of the character.
    /// </summary>
    private static readonly Regex CmdVariable = new(@"%[A-Za-z_][A-Za-z0-9_()]*%", RegexOptions.Compiled);

    /// <summary>
    /// A hint to append to a refusal about <paramref name="path"/>, or the empty string when there is
    /// nothing useful to say. Never throws — a diagnostic that can fail is a second failure on top of
    /// the one being explained.
    /// </summary>
    /// <param name="path">The path exactly as it was given, unexpanded.</param>
    /// <param name="exists">
    /// Whether that path resolves. Injected rather than probed so the rule is testable without touching
    /// a filesystem, and so the caller's own existence check and this one can never disagree.
    /// </param>
    public static string For(string? path, bool exists)
    {
        if (exists || string.IsNullOrWhiteSpace(path)) return string.Empty;

        var match = CmdVariable.Match(path);
        if (!match.Success) return string.Empty;

        var name = match.Value.Trim('%');
        var powershellForm = path.Replace(match.Value, "$env:" + name);

        return Environment.NewLine +
               $"  hint    : that path contains {match.Value}, which is cmd/batch variable syntax. " +
               "PowerShell does NOT expand it, so the literal string above is what this tool was " +
               "handed — it was not the file that was missing, it was the substitution." +
               Environment.NewLine +
               $"            In PowerShell, quote it: \"{powershellForm}\"" +
               Environment.NewLine +
               "            Nothing here expanded it for you, on purpose: a fence that repairs its own " +
               "input is one you cannot tell what it actually read.";
    }

    /// <summary>Convenience overload that performs the existence check itself.</summary>
    public static string For(string? path)
    {
        bool exists;
        try
        {
            exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
        catch (Exception)
        {
            // An unusable path is exactly the case this hint is for; treat it as not existing.
            exists = false;
        }

        return For(path, exists);
    }
}
