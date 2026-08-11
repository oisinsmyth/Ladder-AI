using System;
using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

/// <summary>
/// Which <c>Siemens.Engineering.Download.DownloadOptions</c> a download would be asked for.
///
/// Deliberately NOT a one-to-one copy of the Siemens enum: that one also has <c>None</c>, which
/// selects neither hardware nor software and so describes a download that transfers nothing. There
/// is no plan worth printing for it, and accepting it would mean the tool's most reassuring-looking
/// answer ("nothing would be transferred") came from a value nobody would type on purpose.
///
/// None of these three is a per-block selector. See <see cref="DownloadPlanResult"/>.
/// </summary>
public enum DownloadOptionKind
{
    /// <summary>
    /// The whole PLC software: every block, every PLC data type, every tag table, as one transfer.
    /// The default for this command precisely BECAUSE it is unambiguous — see
    /// <see cref="SoftwareOnlyChanges"/>.
    /// </summary>
    Software,

    /// <summary>
    /// Still the whole PLC software; TIA works out which parts of it differ from the controller and
    /// transfers those. "Changes" here means "changes TIA detected between the project and the
    /// device", NOT "the blocks you edited" — the selection is TIA's, made at download time, and
    /// nothing about it is under this tool's control or visible to it beforehand. This is the value
    /// most easily misread as a per-block download, which is why it is not the default.
    /// </summary>
    SoftwareOnlyChanges,

    /// <summary>
    /// The hardware configuration. On a real controller this generally implies a STOP. Nothing in
    /// this command performs one — it is listed because it is a value the API accepts and a plan
    /// that could not name it would be describing a smaller API than the one that exists.
    /// </summary>
    Hardware,
}

/// <summary>
/// Where a <c>DownloadProvider</c> was found, or how the search for one failed.
///
/// This is reported rather than assumed because the acquisition path is the single least documented
/// thing about download in Openness. <c>DownloadProvider</c>'s only constructor is internal, and it
/// implements <c>IEngineeringService</c>, so it can be obtained in exactly one way:
/// <c>IEngineeringServiceProvider.GetService&lt;DownloadProvider&gt;()</c>. What that leaves open is
/// WHICH object in the device tree answers — and Openness's own answer to that is
/// <c>GetServiceInfos()</c>, which is a read.
/// </summary>
/// <param name="Found">Whether a provider was obtained at all.</param>
/// <param name="SourcePath">The object that supplied it, described by its position in the tree.</param>
/// <param name="SourceClrType">That object's CLR class name — the fact a later reader wants.</param>
/// <param name="ProviderParentClrType">
/// The provider's own <c>Parent</c> class name. Not the same question as
/// <paramref name="SourceClrType"/>: one is who was asked, the other is what the API says the
/// provider hangs off, and they are not guaranteed to agree.
/// </param>
/// <param name="Attempts">Every object asked, in order, with what it answered. Kept even on success:
/// "the station refused and the CPU answered" is the finding, and a report that printed only the
/// winner would have to be re-derived by hand the next time someone asks where downloads live.</param>
public sealed record DownloadProviderSource(
    bool Found,
    string? SourcePath,
    string? SourceClrType,
    string? ProviderParentClrType,
    IReadOnlyList<DownloadProviderAttempt> Attempts);

/// <param name="ObjectPath">The object asked.</param>
/// <param name="ClrType">Its class name.</param>
/// <param name="Outcome">"provider" | "no-provider" | "not-a-service-provider" | "threw".</param>
/// <param name="Detail">Advertised services, or the exception, depending on the outcome.</param>
public sealed record DownloadProviderAttempt(
    string ObjectPath,
    string ClrType,
    string Outcome,
    string? Detail);

/// <summary>
/// One <c>ConfigurationAddress</c> or <c>ConfigurationTargetInterface</c> address, read from the
/// PROJECT'S connection configuration — not from the network.
/// </summary>
public sealed record DownloadConnectionAddress(string Name, string Address);

/// <param name="Name">The PC interface's own name, e.g. the NIC as TIA names it.</param>
/// <param name="Number">Its ordinal within the mode.</param>
/// <param name="Addresses">Addresses configured ON THE PC SIDE of this interface.</param>
/// <param name="Subnets">Subnet names this interface is attached to.</param>
/// <param name="TargetInterfaces">
/// The device-side interfaces reachable through it, each with its own addresses. This is the leg
/// that carries the controller's address, and it is the closest thing to "where would this go".
/// </param>
public sealed record DownloadPcInterface(
    string Name,
    int Number,
    IReadOnlyList<DownloadConnectionAddress> Addresses,
    IReadOnlyList<string> Subnets,
    IReadOnlyList<DownloadTargetInterface> TargetInterfaces);

public sealed record DownloadTargetInterface(string Name, IReadOnlyList<DownloadConnectionAddress> Addresses);

/// <param name="Name">The mode's name — PN/IE, PROFIBUS, and so on.</param>
public sealed record DownloadConnectionMode(string Name, IReadOnlyList<DownloadPcInterface> PcInterfaces);

/// <summary>
/// The connection a download WOULD use, as the project describes it.
///
/// Every field here comes from <c>DownloadProvider.Configuration</c> — a
/// <c>Siemens.Engineering.Connection.ConnectionConfiguration</c>, which is a project-model object
/// like any other, walked with plain property reads. No socket is opened to produce it.
///
/// What is deliberately ABSENT is the one thing on that tree that would require the network:
/// <c>ConfigurationPcInterface.GetAccessibleDevices()</c> ("Delivers a list of accessible devices"),
/// which scans. It is never called. So this describes the route the project has configured, and says
/// nothing whatsoever about whether anything is at the other end of it — a distinction worth keeping,
/// because a configured address that answers and one that does not look identical here.
/// </summary>
/// <param name="IsConfigured">
/// <c>ConnectionConfiguration.IsConfigured</c>. False means the project has no online path set up at
/// all, and a download would have nothing to route through.
/// </param>
/// <param name="ProbedWithoutConnecting">
/// Always true, and stated in the payload rather than only in prose. A consumer of the JSON has to
/// be able to tell that these addresses are declarations, not observations.
/// </param>
public sealed record DownloadConnectionPlan(
    bool IsConfigured,
    bool EnableLegacyCommunication,
    IReadOnlyList<DownloadConnectionMode> Modes,
    bool ProbedWithoutConnecting = true)
{
    /// <summary>Every address anywhere in the tree, deduplicated — the "where would this go" summary.</summary>
    public IReadOnlyList<string> AllTargetAddresses => Modes
        .SelectMany(m => m.PcInterfaces)
        .SelectMany(p => p.TargetInterfaces)
        .SelectMany(t => t.Addresses)
        .Select(a => a.Address)
        .Where(a => !string.IsNullOrWhiteSpace(a))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

/// <summary>
/// What a download of one PLC device would comprise. Produced by <c>download-plan</c>, which plans
/// and cannot download — see <c>IOpennessGateway.PerformDownload</c>, the one method that would, and
/// which throws unconditionally in this build.
///
/// THE FACT THIS RECORD EXISTS TO CARRY: <see cref="Granularity"/>. Openness's download API is
/// device-level. <c>DownloadProvider.Download</c> takes a connection, a pair of configuration
/// callbacks and a <c>DownloadOptions</c> value; it takes no block, no group, and no selection of
/// any kind. There is no per-block download and no "download this DB" — the smallest unit the API
/// offers is the whole PLC software. Anyone who arrives here expecting to push one new block will be
/// wrong about what would happen, so the report says so in every mode rather than leaving it to be
/// inferred from the absence of a <c>--block</c> flag.
/// </summary>
/// <param name="BlockCount">
/// How many blocks are in the device's program. Not a selection — the opposite: it is the size of
/// what a download would carry, printed next to the granularity statement so the two are read
/// together.
/// </param>
/// <param name="TypeCount">Same, for PLC data types.</param>
public sealed record DownloadPlanResult(
    string DevicePath,
    string DeviceName,
    DownloadOptionKind Options,
    DownloadProviderSource Provider,
    DownloadConnectionPlan? Connection,
    int BlockCount,
    int TypeCount,
    IReadOnlyList<string> InconsistentBlocks)
{
    /// <summary>
    /// The unit of a download, stated as a value so it appears in the JSON as well as the table. A
    /// caller scripting against this must be able to read the granularity, not just see it in prose.
    /// </summary>
    public string Granularity => "WHOLE PLC SOFTWARE — the Openness download API has no per-block granularity";

    /// <summary>
    /// Always false. It is a field rather than an omission so that the JSON carries an explicit
    /// negative: a consumer that keys on a missing property cannot tell "this build refuses" from
    /// "this build is older than the flag".
    /// </summary>
    public bool CanDownload => false;

    public string CannotDownloadReason =>
        "device download is not enabled in this build: download-plan plans only, and no code path in " +
        "this binary reaches DownloadProvider.Download.";

    /// <summary>
    /// Blocks the project itself reports as not compiled. A download does not clear these and this
    /// command does not gate on them — it is a plan, not a gate — but a plan that omitted them would
    /// be describing a transfer of content that has never been verified (hard rule 4, FI-52).
    /// </summary>
    public bool HasUnverifiedContent => InconsistentBlocks.Count > 0;
}

/// <summary>
/// Thrown by the one method that would download. Its whole job is to be unreachable.
///
/// It is a real, named exception rather than a commented-out call or a disabled flag because those
/// two are re-enabled by deleting characters, and this project's standing rule is that a warning is
/// not a gate. There is no argument, no environment variable and no build configuration that turns
/// this off: enabling download means adding code, in a reviewed change, on purpose.
/// </summary>
public sealed class DownloadNotEnabledException : NotSupportedException
{
    public const string StandardMessage = "device download is not enabled in this build";

    public DownloadNotEnabledException()
        : base(
            StandardMessage + ". `download-plan` reports what a download would comprise and stops there. " +
            "Nothing in this binary calls DownloadProvider.Download — enabling it requires new code, " +
            "governed by the write fence (ADR-0009): allowlisted, kind == test-rig, write-eligible, " +
            "outputs physically isolated, identity verified against the device, restore point captured, " +
            "write scope declared.")
    {
    }
}
