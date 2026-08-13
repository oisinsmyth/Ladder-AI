using System.Text.Json;
using Ladder.Download;

namespace Harness.Device;

/// <summary>
/// What <c>download-probe --json</c> said, recovered from its stdout.
///
/// <para><b>The load manifest is recovered from the EMBEDDED LOG, and that is a known compromise.</b>
/// <c>ProbeLogReader</c>'s own documentation says the live path is <c>DownloadResultAdapter</c>,
/// straight off the Openness object, and warns "never route a live download through a log file: the
/// log is the probe's RENDERING of the result, and anything the renderer drops is gone before this
/// reader sees it".</para>
///
/// <para>That warning is addressed to code running INSIDE the probe. This gateway is a different
/// process — it targets net8.0 and could not reference <c>Siemens.Engineering</c> if it wanted to —
/// so the rendered log is the only artifact it can reach. The probe's <c>--json</c> payload embeds
/// the log verbatim rather than pointing at it, which is why the embedded array is preferred over
/// re-reading the file. <b>Recorded as a lane finding rather than papered over:</b> the fix is for
/// the probe to emit the manifest as first-class JSON, and until it does, anything the renderer drops
/// is invisible here.</para>
/// </summary>
public sealed record ProbeReport(
    bool JsonParsed,
    string? TransferVerdictText,
    bool? SoftwareLoaded,
    string? LogFilePath,
    string LogText,
    DownloadFeedback? Feedback,
    string Detail,
    IReadOnlySet<string>? DirectManifest = null,
    TransferVerdict? DirectVerdict = null,
    string? ManifestSource = null)
{
    /// <summary>
    /// Which authority produced the manifest.
    ///
    /// <para><c>DownloadResultAdapter</c> is FIRST-HAND — the live Openness object. <c>ProbeLogReader</c>
    /// is RE-DERIVED from the probe's rendering of that object, so anything the renderer dropped is gone
    /// before it is read. <b>Recorded rather than inferred</b>, because the two are not equally strong and
    /// a result that does not say which it used invites the stronger reading.</para>
    /// </summary>
    public string Source => ManifestSource ?? (Feedback is null ? "none" : nameof(ProbeLogReader));

    /// <summary>Object names TIA reported loading. Empty when nothing could be recovered.</summary>
    public IReadOnlySet<string> Manifest =>
        DirectManifest
        ?? (Feedback is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(Feedback.LoadedObjects, StringComparer.Ordinal));

    /// <summary>
    /// The three-valued verdict. <see cref="TransferVerdict.Undetermined"/> when no result could be
    /// recovered at all — <b>never</b> <see cref="TransferVerdict.NothingTransferred"/>, which is a
    /// positive statement that TIA skipped the transfer.
    /// </summary>
    public TransferVerdict Verdict => DirectVerdict ?? Feedback?.Verdict ?? TransferVerdict.Undetermined;

    /// <summary>True when a manifest was recovered at all — from either authority.</summary>
    public bool ManifestAvailable => DirectManifest is not null || Feedback is not null;

    /// <summary>Parse the probe's stdout. Never throws — an unreadable report is a REPORTED gap.</summary>
    public static ProbeReport FromStdout(string? stdout, Func<string, string?>? readLogFile = null)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return Empty("download-probe produced no stdout, so neither its verdict nor its load manifest could be read. "
                + "Nothing here says whether anything was transferred, and that is not the same as nothing having been.");
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(stdout!);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return Empty($"download-probe's stdout is not JSON ({ex.Message}). The first 200 characters were: "
                + stdout!.Substring(0, Math.Min(200, stdout!.Length)));
        }

        var verdictText = String(root, "transferVerdict");
        var softwareLoaded = Bool(root, "softwareLoaded");
        var logFile = String(root, "logFile");

        // *** FIRST-CLASS MANIFEST FIRST (2026-08-13). *** `download-probe --json` now emits a
        // `loadManifest` object built by DownloadResultAdapter — straight off the live Openness
        // DownloadResult, which is the path ProbeLogReader's own docs name and which nothing had ever
        // used. Reading it removes this gateway's only scrape. The log path stays as the fallback,
        // because an older probe build is a real thing to be handed and losing the manifest silently
        // would be worse than reading a rendering.
        if (TryReadLoadManifest(root, out var direct))
            return direct with { TransferVerdictText = verdictText, SoftwareLoaded = softwareLoaded, LogFilePath = logFile };

        var logText = JoinLog(root);
        if (string.IsNullOrEmpty(logText) && logFile is not null && readLogFile is not null)
            logText = readLogFile(logFile) ?? string.Empty;

        if (string.IsNullOrEmpty(logText))
        {
            return new ProbeReport(true, verdictText, softwareLoaded, logFile, string.Empty, null,
                "the probe's JSON carried no log, so the load manifest could not be recovered. The verdict line alone is not a manifest.");
        }

        var feedback = DownloadFeedbackParser.Parse(ProbeLogReader.Read(logText));

        return new ProbeReport(true, verdictText, softwareLoaded, logFile, logText, feedback,
            $"verdict={feedback.Verdict}, {feedback.LoadedObjectCount} object(s) in the load manifest, "
            + $"{feedback.NonObjectLoadCount} non-object load(s), {feedback.UnrecognisedMessageCount} unrecognised message(s). {feedback.VerdictReason}");
    }

    /// <summary>
    /// Read <c>loadManifest</c>, the probe's first-class manifest object.
    ///
    /// <para><b><c>available: false</c> is honoured as "nobody looked", never as "TIA loaded nothing".</b>
    /// The probe emits every list as <c>null</c> in that case precisely so the two cannot be confused, and
    /// reading a null as an empty set here would undo that at the first consumer.</para>
    /// </summary>
    private static bool TryReadLoadManifest(JsonElement root, out ProbeReport report)
    {
        report = null!;

        if (!root.TryGetProperty("loadManifest", out var manifest) || manifest.ValueKind != JsonValueKind.Object)
            return false;

        var available = Bool(manifest, "available");
        var source = String(manifest, "source") ?? "unstated";

        if (available is not true)
        {
            report = new ProbeReport(true, null, null, null, string.Empty, null,
                $"the probe emitted a loadManifest with available={available?.ToString() ?? "(unstated)"} (source: {source}), meaning NO DownloadResult existed — an abort, a throw, or a folder download. "
                + "Nothing was recovered, and that is 'nobody looked', not 'TIA loaded nothing'.",
                DirectManifest: null, DirectVerdict: TransferVerdict.Undetermined, ManifestSource: source);
            return true;
        }

        if (!manifest.TryGetProperty("loadedObjects", out var loaded) || loaded.ValueKind != JsonValueKind.Array)
            return false;

        var names = loaded.EnumerateArray()
            .Select(e => e.GetString())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

        var verdictText = String(manifest, "verdict");
        var verdict = Enum.TryParse<TransferVerdict>(verdictText, ignoreCase: true, out var parsed)
            ? parsed
            : TransferVerdict.Undetermined;

        var reason = String(manifest, "verdictReason");
        var unrecognised = manifest.TryGetProperty("unrecognisedMessageCount", out var u) && u.ValueKind == JsonValueKind.Number
            ? u.GetInt32()
            : 0;

        report = new ProbeReport(true, null, null, null, string.Empty, null,
            $"verdict={verdict} from the probe's own loadManifest (source: {source}), {names.Count} object(s) named, {unrecognised} unrecognised message(s)."
            + (reason is null ? string.Empty : " " + reason)
            + (string.Equals(source, "DownloadResultAdapter", StringComparison.Ordinal)
                ? " FIRST-HAND: read off the live DownloadResult rather than re-derived from a rendering."
                : $" NOT first-hand — the probe says this manifest came from '{source}', so anything that renderer dropped is already gone."),
            DirectManifest: names, DirectVerdict: verdict, ManifestSource: source);

        return true;
    }

    private static ProbeReport Empty(string detail) =>
        new(false, null, null, null, string.Empty, null, detail);

    private static string JoinLog(JsonElement root)
    {
        if (!root.TryGetProperty("log", out var log) || log.ValueKind != JsonValueKind.Array)
            return string.Empty;

        return string.Join("\n", log.EnumerateArray().Select(e => e.GetString() ?? string.Empty));
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? Bool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => (bool?)null,
            }
            : null;
}
