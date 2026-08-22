using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Ladder.Converter.Leases;

/// <summary>
/// What the evidence permits. <b>Every value except <see cref="Clear"/> is a refusal</b> — there is no
/// "probably fine".
/// </summary>
public enum PortalEvidenceVerdict
{
    /// <summary>The zero value, and unusable.</summary>
    Unstated = 0,

    /// <summary>Nothing outside this tooling holds the project, and every process could be judged.</summary>
    Clear,

    /// <summary>A process this tooling did not launch has the target project open. Refused, by PID.</summary>
    HeldOutsideTheTool,

    /// <summary>
    /// 🔴 <b>The question could not be answered, which is NOT the same as answering "nobody has it".</b>
    ///
    /// <para>The demonstrated case: a Portal that Openness cannot see reports <c>projectPath: null</c>
    /// — <i>exactly</i> what a Portal with no project open reports. Only <c>opennessVisible</c>
    /// separates them, so a gate that reads the path alone fails open on the one process that is
    /// already misbehaving. <c>portal-status</c> has measured this: it reported one process while the
    /// OS showed two.</para>
    /// </summary>
    CannotDecide,

    /// <summary>The evidence is not portal-status output, or is too old to describe now.</summary>
    Unusable,
}

/// <summary>The verdict and the sentence that justifies it. The detail is never empty.</summary>
public sealed record PortalEvidenceResult(PortalEvidenceVerdict Verdict, string Detail)
{
    public bool Clear => Verdict == PortalEvidenceVerdict.Clear;
}

/// <summary>
/// Reads an <c>openness-cli portal-status --json</c> document and decides whether a Portal lease may be
/// taken on one project.
///
/// <para><b>Why the evidence arrives as a FILE.</b> Which project a Portal process has open comes from
/// <c>TiaPortalProcess.ProjectPath</c>, i.e. from <c>Siemens.Engineering.dll</c>, i.e. from a net48
/// assembly the converter cannot reference. There is no file, registry key, command line or window
/// title that answers it — <c>LaunchedInstanceRegistry</c> holds PIDs only, and its live half is erased
/// at the moment a project opens, which is the moment it would become interesting. So the tool that CAN
/// compute the fact produces it and the tool that needs it consumes it, exactly as
/// <c>wave-cli submit --reachable-state</c> already does.</para>
///
/// <para><b>Absent is not clear.</b> A Portal lease with no evidence is refused rather than granted; the
/// whole point of FI-65's objection 11 is that an advisory lock over a resource a person can take is
/// "a lie that eventually gets believed".</para>
///
/// <para>This class performs NO file I/O and reads no clock — the caller supplies the text and the age.
/// That keeps every branch below reachable from a test without staging a filesystem.</para>
/// </summary>
public static class PortalEvidence
{
    /// <summary>
    /// How old the evidence may be. <b>A judgement, and deliberately short</b>: the fact being asserted
    /// is "no person has this open <i>right now</i>", and a person can open a project between the
    /// producing command and this one. It cannot be eliminated — only bounded, and named when it bites.
    /// </summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(2);

    /// <param name="json">The verbatim stdout of <c>openness-cli portal-status --json</c>.</param>
    /// <param name="targetProject">The project the lease is being taken on: an <c>.ap20</c> file or the folder holding it.</param>
    /// <param name="age">How long ago the evidence was produced.</param>
    public static PortalEvidenceResult Judge(string? json, string targetProject, TimeSpan age, TimeSpan? maxAge = null)
    {
        var limit = maxAge ?? DefaultMaxAge;

        if (string.IsNullOrWhiteSpace(json))
        {
            return new PortalEvidenceResult(PortalEvidenceVerdict.Unusable,
                "the portal evidence file is empty. An empty document is not an empty Portal list — nothing was examined.");
        }

        if (age > limit)
        {
            return new PortalEvidenceResult(PortalEvidenceVerdict.Unusable,
                $"the portal evidence is {Describe(age)} old and the limit is {Describe(limit)}. "
                + "*** THE AGE IS TAKEN FROM THE FILE'S LAST-WRITE TIME, because `portal-status` stamps no run time into its "
                + "JSON *** — so a copied or touched file reads as fresh. Re-run `openness-cli portal-status --json` into a new file.");
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            root = document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            return new PortalEvidenceResult(PortalEvidenceVerdict.Unusable,
                $"the portal evidence is not readable JSON: {error.Message}");
        }

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("processes", out var processes)
            || processes.ValueKind != JsonValueKind.Array)
        {
            return new PortalEvidenceResult(PortalEvidenceVerdict.Unusable,
                "the portal evidence has no `processes` array, so it is not `openness-cli portal-status --json` output. "
                + "A document that names no processes is not a document reporting none.");
        }

        string canonicalTarget;
        try
        {
            canonicalTarget = Canonical(targetProject);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new PortalEvidenceResult(PortalEvidenceVerdict.Unusable,
                $"the lease target '{targetProject}' is not a usable path, so it cannot be compared against what Portal reports: {error.Message}");
        }

        // Reported BEFORE the invisibility sweep because it is the more specific answer: naming the PID
        // that actually has the project beats "something here could not be judged".
        foreach (var process in processes.EnumerateArray())
        {
            var projectPath = StringOrNull(process, "projectPath");
            if (projectPath is null)
            {
                continue;
            }

            if (!SamePath(canonicalTarget, projectPath))
            {
                continue;
            }

            // Launched by our own tooling is NOT a human, and is exactly the contention the lease itself
            // arbitrates. A self-launched process holding the project with no lease is a hygiene matter
            // for `portal-status`, not a reason for this gate to refuse.
            if (BoolOr(process, "launchedByThisTool", false))
            {
                continue;
            }

            var pid = IntOr(process, "pid", -1);
            return new PortalEvidenceResult(PortalEvidenceVerdict.HeldOutsideTheTool,
                $"pid {pid} has this project open and was NOT launched by this tooling — treat it as a person. "
                + "There is no TTL on a human and no queue they are standing in: close it in TIA Portal, or lease a different project. "
                + $"(reported project: {projectPath})");
        }

        foreach (var process in processes.EnumerateArray())
        {
            if (BoolOr(process, "opennessVisible", true))
            {
                continue;
            }

            var pid = IntOr(process, "pid", -1);
            return new PortalEvidenceResult(PortalEvidenceVerdict.CannotDecide,
                $"pid {pid} is a Portal process Openness cannot see (class OS-ONLY), so WHICH PROJECT IT HAS OPEN IS UNKNOWN — "
                + "and an unknown project reads as `projectPath: null`, identical to a Portal with nothing open. "
                + "This is refused rather than waved through because the process that cannot be judged is the one most likely to be holding something. "
                + "Close it, or establish by hand that it does not have this project open.");
        }

        var examined = processes.GetArrayLength();
        return new PortalEvidenceResult(PortalEvidenceVerdict.Clear,
            $"{examined} Portal process(es) examined, all visible to Openness, none holding this project outside this tooling.");
    }

    private static string Describe(TimeSpan span) =>
        span.TotalSeconds < 90
            ? $"{span.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s"
            : $"{span.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture)} min";

    private static string Canonical(string path) =>
        Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// <b>The target may be the <c>.ap20</c> or the folder holding it; Portal always reports the file.</b>
    /// Both spellings are accepted, and nothing weaker — a prefix match would make a lease on one project
    /// silently cover its sibling in the same parent folder.
    /// </summary>
    private static bool SamePath(string canonicalTarget, string reported)
    {
        string canonicalReported;
        try
        {
            canonicalReported = Canonical(reported);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (string.Equals(canonicalTarget, canonicalReported, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var reportedFolder = Path.GetDirectoryName(canonicalReported);
        return reportedFolder is not null
            && string.Equals(canonicalTarget, reportedFolder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static string? StringOrNull(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // Defaults chosen so a MISSING field never reads as permission: an absent `opennessVisible` is
    // treated as visible (so the sweep does not refuse every well-formed document produced before that
    // field existed), while an absent `launchedByThisTool` is treated as NOT ours — the conservative
    // half of each pair.
    private static bool BoolOr(JsonElement element, string name, bool fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => fallback,
            }
            : fallback;

    private static int IntOr(JsonElement element, string name, int fallback) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;
}
