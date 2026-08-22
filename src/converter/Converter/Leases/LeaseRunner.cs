using System;

namespace Ladder.Converter.Leases;

/// <summary>A parsed <c>--resource</c> token, or the reason it could not be parsed.</summary>
public sealed record LeaseResourceRef(LeaseResource Resource, string Target, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// The decision layer between the CLI and <see cref="LeaseStore"/>.
///
/// <para><b>It performs no I/O and reads no clock.</b> The store owns the files, <c>Program</c> owns
/// reading the evidence and timing it, and everything decided here is decided from arguments — which is
/// what makes the Portal gate's branches reachable from a test without a Portal, a filesystem or a
/// wall clock.</para>
/// </summary>
public static class LeaseRunner
{
    /// <summary>
    /// <c>portal:&lt;project&gt;</c> or <c>rig:&lt;address&gt;</c>.
    ///
    /// <para>Split on the FIRST colon only, because a Windows project path contains one of its own
    /// (<c>portal:C:\…</c>) and splitting on all of them would silently truncate the target to a drive
    /// letter — a lease on <c>C</c>, granted, protecting nothing.</para>
    /// </summary>
    public static LeaseResourceRef ParseResource(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new LeaseResourceRef(LeaseResource.Unstated, string.Empty,
                "--resource is required, as portal:<project> or rig:<address>.");
        }

        var text = token.Trim();
        var split = text.IndexOf(':');
        if (split <= 0 || split == text.Length - 1)
        {
            return new LeaseResourceRef(LeaseResource.Unstated, string.Empty,
                $"--resource '{text}' has no kind. Expected portal:<project> or rig:<address> — the kind is not inferred from the "
                + "shape of the target, because a lease on the wrong kind of thing is granted just as readily as a right one.");
        }

        var kind = text[..split];
        var target = text[(split + 1)..].Trim();

        if (target.Length == 0)
        {
            return new LeaseResourceRef(LeaseResource.Unstated, string.Empty,
                $"--resource '{text}' names a kind but no target. A lease on 'a Portal' or 'a rig' locks nothing.");
        }

        return kind.ToLowerInvariant() switch
        {
            "portal" => new LeaseResourceRef(LeaseResource.Portal, target, null),
            "rig" => new LeaseResourceRef(LeaseResource.Rig, target, null),
            _ => new LeaseResourceRef(LeaseResource.Unstated, string.Empty,
                $"--resource kind '{kind}' is not one of: portal, rig."),
        };
    }

    /// <summary>Whether this resource requires portal evidence before it may be taken.</summary>
    public static bool RequiresPortalEvidence(LeaseResource resource) => resource == LeaseResource.Portal;

    /// <summary>
    /// Take the lease, applying the human gate first where one applies.
    ///
    /// <para><b>The gate runs BEFORE the store is touched</b>, so a refused acquire leaves no file
    /// behind and no lease to release. A person holding the project is not a transient another agent
    /// can wait out, and recording our intent next to theirs would only make the store disagree with
    /// reality.</para>
    /// </summary>
    /// <param name="evidence">
    /// The judged <c>portal-status</c> evidence, or <c>null</c> when none was supplied.
    /// <b>Null is a refusal for a Portal lease, never a pass</b> — see <see cref="PortalEvidence"/>.
    /// </param>
    public static LeaseOutcome Acquire(
        LeaseStore store,
        LeaseResource resource,
        string target,
        string holder,
        int processId,
        TimeSpan ttl,
        string? purpose,
        PortalEvidenceResult? evidence)
    {
        if (RequiresPortalEvidence(resource))
        {
            if (evidence is null)
            {
                return new LeaseOutcome(LeaseResult.Invalid, null,
                    "a Portal lease needs --portal-evidence <file.json>, the output of `openness-cli portal-status --json`. "
                    + "*** THIS IS NOT A FORMALITY: *** the converter cannot see which project a Portal process has open — that fact lives "
                    + "behind Siemens.Engineering, in a net48 assembly it does not reference — so without the evidence this command would be "
                    + "locking agents out of a project while a person sat in it, which is the exact failure a lock is supposed to remove.");
            }

            if (evidence.Verdict == PortalEvidenceVerdict.Unusable)
            {
                return new LeaseOutcome(LeaseResult.Invalid, null, "the portal evidence is unusable: " + evidence.Detail);
            }

            if (evidence.Verdict == PortalEvidenceVerdict.HeldOutsideTheTool)
            {
                return new LeaseOutcome(LeaseResult.HeldOutsideTheTool, null, evidence.Detail);
            }

            if (evidence.Verdict != PortalEvidenceVerdict.Clear)
            {
                return new LeaseOutcome(LeaseResult.EvidenceInconclusive, null, evidence.Detail);
            }
        }

        return store.TryAcquire(resource, target, holder, processId, ttl, purpose);
    }

    /// <summary>
    /// What a caller must be told about a resource whose exclusivity nothing can observe.
    ///
    /// <para><b>Printed on every rig acquire, not only on a refusal.</b> The rig lease coordinates
    /// agents and nothing else: <c>MB_SERVER</c> accepts one connection and a second one is discovered
    /// BY FAILURE — a viewer attached during a wave once cost it 0 of 22 vectors. A lease that stayed
    /// quiet about that would read as a guarantee it cannot make.</para>
    /// </summary>
    public static string RigCaveat =>
        "NOTE: this lease coordinates AGENTS ONLY. Nothing detects a rig in use — MB_SERVER accepts one connection and a second "
        + "is discovered by failure, and download-probe writes a log rather than a lock. Holding this lease does not mean the rig is free.";
}

