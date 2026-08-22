using System;
using System.IO;
using Ladder.Converter.Leases;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// The decision layer: what <c>--resource</c> means, and the gate a Portal lease must pass before the
/// store is touched at all.
/// </summary>
public sealed class LeaseRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lease-runner-" + Guid.NewGuid().ToString("N"));

    private const int LivePid = 4242;
    private static readonly DateTime T0 = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LiveStart = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);

    private LeaseStore Store() => new(_root, () => T0, pid => pid == LivePid ? LiveStart : null);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private LeaseOutcome AcquirePortal(PortalEvidenceResult? evidence) =>
        LeaseRunner.Acquire(Store(), LeaseResource.Portal, @"C:\projects\P", "agent-a", LivePid,
            TimeSpan.FromMinutes(30), null, evidence);

    private static PortalEvidenceResult Clear => new(PortalEvidenceVerdict.Clear, "nothing holding it");

    // ---------------------------------------------------------------------------------------------
    // Resource parsing.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A Windows project path contains a colon of its own.</b> Splitting on every colon would
    /// reduce <c>portal:C:\projects\P</c> to a lease on the drive letter — granted, well-formed, and
    /// protecting nothing.
    /// </summary>
    [Fact]
    public void A_portal_target_keeps_its_drive_letter()
    {
        var parsed = LeaseRunner.ParseResource(@"portal:C:\projects\P");

        Assert.True(parsed.Ok);
        Assert.Equal(LeaseResource.Portal, parsed.Resource);
        Assert.Equal(@"C:\projects\P", parsed.Target);
    }

    [Fact]
    public void A_rig_target_is_an_address()
    {
        var parsed = LeaseRunner.ParseResource("rig:10.10.10.10");

        Assert.True(parsed.Ok);
        Assert.Equal(LeaseResource.Rig, parsed.Resource);
        Assert.Equal("10.10.10.10", parsed.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10.10.10.10")]      // no kind — and it must NOT be guessed from the shape
    [InlineData("portal:")]          // a kind with no target
    [InlineData("plc:10.10.10.10")]  // a kind that does not exist
    public void An_unusable_resource_token_is_refused_rather_than_interpreted(string? token)
    {
        var parsed = LeaseRunner.ParseResource(token);

        Assert.False(parsed.Ok);
        Assert.Equal(LeaseResource.Unstated, parsed.Resource);
    }

    // ---------------------------------------------------------------------------------------------
    // The Portal gate. Every one of these must refuse WITHOUT writing a lease.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Absent evidence is a refusal, never a pass.</b> This is the whole producer/consumer contract:
    /// the converter cannot see which project a Portal has open, so a lease taken without the evidence
    /// would be a lock issued in ignorance of the one actor that can ignore it.
    /// </summary>
    [Fact]
    public void A_Portal_lease_with_NO_evidence_is_refused()
    {
        var outcome = AcquirePortal(null);

        Assert.Equal(LeaseResult.Invalid, outcome.Result);
        Assert.Contains("--portal-evidence", outcome.Detail);
    }

    [Fact]
    public void A_Portal_lease_on_UNUSABLE_evidence_is_refused_as_unusable_input()
    {
        var outcome = AcquirePortal(new PortalEvidenceResult(PortalEvidenceVerdict.Unusable, "too old"));

        Assert.Equal(LeaseResult.Invalid, outcome.Result);
    }

    [Fact]
    public void A_Portal_lease_against_a_human_holder_is_refused_as_HeldOutsideTheTool()
    {
        var outcome = AcquirePortal(new PortalEvidenceResult(PortalEvidenceVerdict.HeldOutsideTheTool, "pid 8814 has it"));

        Assert.Equal(LeaseResult.HeldOutsideTheTool, outcome.Result);
        Assert.Contains("8814", outcome.Detail);
    }

    /// <summary>
    /// Kept apart from the case above on purpose: "somebody has it" and "I could not tell" send a reader
    /// to different places, and merging them would have them hunting a holder nobody identified.
    /// </summary>
    [Fact]
    public void An_inconclusive_verdict_is_its_own_result_and_not_folded_into_HeldOutsideTheTool()
    {
        var outcome = AcquirePortal(new PortalEvidenceResult(PortalEvidenceVerdict.CannotDecide, "an OS-ONLY process is present"));

        Assert.Equal(LeaseResult.EvidenceInconclusive, outcome.Result);
    }

    /// <summary>
    /// <b>The gate runs before the store is touched.</b> A refused acquire must leave nothing behind —
    /// otherwise a person holding the project would also acquire a phantom lease in our own store, and
    /// the next reader would find the registry disagreeing with the world.
    /// </summary>
    [Fact]
    public void A_refused_Portal_acquire_writes_NOTHING_to_the_store()
    {
        var store = Store();
        LeaseRunner.Acquire(store, LeaseResource.Portal, @"C:\projects\P", "agent-a", LivePid,
            TimeSpan.FromMinutes(30), null, new PortalEvidenceResult(PortalEvidenceVerdict.HeldOutsideTheTool, "a person"));

        Assert.Empty(store.All());
        Assert.Null(store.Read(LeaseResource.Portal, @"C:\projects\P"));
    }

    [Fact]
    public void A_Portal_lease_on_CLEAR_evidence_is_taken()
    {
        var outcome = AcquirePortal(Clear);

        Assert.Equal(LeaseResult.Acquired, outcome.Result);
        Assert.True(outcome.Held);
    }

    // ---------------------------------------------------------------------------------------------
    // The rig, which no evidence can speak to.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Nothing observes a rig in use — <c>MB_SERVER</c>'s single connection is discovered by failure —
    /// so requiring evidence there would be requiring a document nobody can produce.
    /// </summary>
    [Fact]
    public void A_rig_lease_needs_no_evidence()
    {
        Assert.False(LeaseRunner.RequiresPortalEvidence(LeaseResource.Rig));
        Assert.True(LeaseRunner.RequiresPortalEvidence(LeaseResource.Portal));

        var outcome = LeaseRunner.Acquire(Store(), LeaseResource.Rig, "10.10.10.10", "agent-a", LivePid,
            TimeSpan.FromMinutes(30), null, evidence: null);

        Assert.Equal(LeaseResult.Acquired, outcome.Result);
    }

    /// <summary>
    /// And it must SAY that it observed nothing. A lease that stayed quiet would be read as a guarantee
    /// the rig is free, which it cannot make.
    /// </summary>
    [Fact]
    public void The_rig_caveat_states_that_nothing_detects_a_rig_in_use()
    {
        Assert.Contains("AGENTS ONLY", LeaseRunner.RigCaveat);
        Assert.Contains("discovered by failure", LeaseRunner.RigCaveat);
    }
}
