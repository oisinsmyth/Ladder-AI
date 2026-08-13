namespace Harness.Device;

/// <summary>
/// The three literals <c>download-probe --options</c> accepts. Spelled exactly as
/// <c>DownloadOptionChoices.TryParseLiteral</c> matches them; a fourth value cannot be expressed.
/// </summary>
public enum DownloadOption
{
    /// <summary>The whole PLC software. Unambiguous, and what <c>download-plan</c> defaults to.</summary>
    Software,

    /// <summary>
    /// Whatever TIA finds different from the controller — decided BY TIA at download time, NOT "the
    /// blocks you edited". This is the one measured to complete on this rig (`download-probe
    /// --options SoftwareOnlyChanges --disruptive`, exit 0, 19 objects in the manifest).
    /// </summary>
    SoftwareOnlyChanges,

    /// <summary>Hardware configuration. The harness never needs it; it exists so the enum is total.</summary>
    Hardware,
}

/// <summary>
/// Everything the real gateway cannot derive. <b>Nothing here has a default that could be mistaken
/// for a measurement</b> (D36): the PC interface, the project, the group path and the Modbus host
/// are all facts about a particular rig, and a gateway that guessed one would write to the wrong
/// thing and report success.
/// </summary>
/// <param name="ProjectPath">
/// The <c>.apNN</c> file. <b>Fenced to the scratch project</b> — see <see cref="Refusals"/>. The
/// gateway IMPORTS and DOWNLOADS, so hard rule 5's "do not import into the real project" is a
/// property this type enforces rather than a sentence somebody remembers.
/// </param>
/// <param name="GroupPath">
/// <c>&lt;device&gt;/&lt;path&gt;</c>, matching <c>openness-cli list</c>'s own <c>Path</c> column
/// VERBATIM. A device item's real name can contain spaces and an embedded article number as one
/// literal string; a shortened guess fails with a message that does not point at the mismatch.
/// </param>
/// <param name="PcInterface">
/// The PC adapter, by EXACT name, as the project's own ConnectionConfiguration spells it — optionally
/// with a trailing <c>" #&lt;n&gt;"</c>. Required by <c>download-probe</c> whenever the project
/// declares more than one, and there is no default because the candidate list on this engineering PC
/// includes a PLCSIM virtual adapter.
/// </param>
/// <param name="AllowCpuStop">
/// <b>Must be true, and the gateway refuses rather than defaulting it.</b> Without
/// <c>--disruptive</c> the probe's policy answers every configuration with <c>NoAction</c> and the
/// download ABORTS — every previous run of that tool succeeded by refusing. So a deployment is only
/// possible with the CPU stop permitted, and the caller has to say so in as many words.
/// </param>
public sealed record DeviceGatewayOptions(
    string ConverterExe,
    string OpennessCliExe,
    string DownloadProbeExe,
    string ProjectPath,
    string GroupPath,
    string PcInterface,
    DownloadOption DownloadOption,
    bool AllowCpuStop,
    string ModbusHost,
    string StagingDirectory,
    string? Device = null,
    string? TargetInterface = null,
    string? IrProjectDirectory = null,
    string? ProbeLogDirectory = null,
    int ModbusPort = 502,
    byte ModbusUnitId = 1,
    int TimeoutConnectSeconds = 180,
    int TimeoutOpenSeconds = 1800)
{
    /// <summary>
    /// The only project name this gateway will write to.
    ///
    /// <para><b>Kept byte-identical to <c>DownloadProbe.ScratchProjectGuard.RequiredSuffix</c></b>,
    /// and a test asserts that by reading that file — because a gateway whose fence was LOOSER than
    /// the probe's would pass its own guard and then be refused at exit 3 after a full import and
    /// compile had already been written into a real project.</para>
    ///
    /// <para>The leading space is deliberate: <c>"…\Anything scratch.ap20"</c> passes and
    /// <c>"…\myscratch.ap20"</c> does not.</para>
    /// </summary>
    public const string RequiredProjectSuffix = " scratch.ap20";

    /// <summary>Where the probe's per-run log goes, if the caller did not name a directory.</summary>
    public string ResolvedProbeLogDirectory =>
        string.IsNullOrWhiteSpace(ProbeLogDirectory)
            ? Path.Combine(StagingDirectory, "probe-logs")
            : ProbeLogDirectory!;

    /// <summary>Where generated <c>.ir</c> is written before conversion.</summary>
    public string IrStagingDirectory => Path.Combine(StagingDirectory, "ir");

    /// <summary>Where <c>converter to-xml</c> is told to put its output, and what <c>import-all</c> is given.</summary>
    public string XmlStagingDirectory => Path.Combine(StagingDirectory, "xml");

    /// <summary>
    /// Every reason this configuration cannot be used, or empty. Computed WITHOUT touching the
    /// filesystem, so the plan is testable; <see cref="MissingBinaries"/> is the separate check that
    /// does touch it.
    /// </summary>
    public IReadOnlyList<string> Refusals
    {
        get
        {
            var refusals = new List<string>();

            Required(refusals, ConverterExe, nameof(ConverterExe));
            Required(refusals, OpennessCliExe, nameof(OpennessCliExe));
            Required(refusals, DownloadProbeExe, nameof(DownloadProbeExe));
            Required(refusals, ProjectPath, nameof(ProjectPath));
            Required(refusals, GroupPath, nameof(GroupPath));
            Required(refusals, PcInterface, nameof(PcInterface));
            Required(refusals, ModbusHost, nameof(ModbusHost));
            Required(refusals, StagingDirectory, nameof(StagingDirectory));

            if (!string.IsNullOrWhiteSpace(ProjectPath) && !IsScratchProject(ProjectPath))
            {
                refusals.Add(
                    $"'{ProjectPath}' is not the scratch project: this gateway opens only a project whose FILE NAME ends in '{RequiredProjectSuffix}' (case-insensitive). "
                    + "It imports, compiles and downloads, so a real project is refused here for the same reason download-probe refuses it — and refused EARLIER, before anything is written.");
            }

            if (!AllowCpuStop)
            {
                refusals.Add(
                    "AllowCpuStop is false, so no download is possible. Without --disruptive the probe answers every configuration with NoAction and the download ABORTS; "
                    + "with it, EXPECT THE CPU TO BE LEFT STOPPED. A deployment therefore requires the caller to permit that explicitly rather than acquire it by default.");
            }

            if (!string.IsNullOrWhiteSpace(GroupPath) && !GroupPath.Contains('/'))
            {
                refusals.Add(
                    $"GroupPath '{GroupPath}' has no '/'. It must be <device>/<path>, copied VERBATIM from `openness-cli list`'s Path column — a device item's real name can itself contain spaces and an article number.");
            }

            if (ModbusPort is < 1 or > 65535)
                refusals.Add($"ModbusPort {ModbusPort} is not a port number.");

            return refusals;
        }
    }

    /// <summary>The binaries that are not where the options say they are. Empty is the healthy answer.</summary>
    public IReadOnlyList<string> MissingBinaries =>
        new[]
        {
            (nameof(ConverterExe), ConverterExe),
            (nameof(OpennessCliExe), OpennessCliExe),
            (nameof(DownloadProbeExe), DownloadProbeExe),
        }
        .Where(b => string.IsNullOrWhiteSpace(b.Item2) || !File.Exists(b.Item2))
        .Select(b => $"{b.Item1}: '{b.Item2}' does not exist.")
        .ToArray();

    /// <summary>Same rule as the probe's own guard, applied to the FILE NAME and never the whole path.</summary>
    public static bool IsScratchProject(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return false;

        try
        {
            return Path.GetFileName(projectPath!.Trim())
                .EndsWith(RequiredProjectSuffix, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            // An unusable path is not the scratch project. This guard answers "no" whenever it
            // cannot answer "yes".
            return false;
        }
    }

    private static void Required(List<string> refusals, string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            refusals.Add($"{name} is required and has no default — it is a fact about a particular rig, and a guess would write to the wrong thing.");
    }
}
