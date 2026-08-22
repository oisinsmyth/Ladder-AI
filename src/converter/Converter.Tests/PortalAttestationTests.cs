using System;
using System.IO;
using Ladder.Converter.Leases;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// 🔴 <b>The named escape from the Portal gate, and the one case it deliberately does not cover.</b>
///
/// <para>Measured live on 2026-08-22: the gate refused a real batch run because two Portal processes
/// were <c>OS-ONLY</c> — Openness could not see them, so they reported <c>projectPath: null</c>,
/// indistinguishable from a Portal with nothing open. That refusal was correct and the tool could not
/// resolve it, because the question is answerable only by a person looking at the machine.</para>
///
/// <para><b>So the escape takes an ATTESTATION, not a flag.</b> The sentence is carried into the lease's
/// purpose and shows up in <c>lease status</c> for as long as the lease is held: a gate opened on
/// somebody's word should say whose, and on what basis. The same shape as <c>--allow-header</c> and
/// <c>--allow-silent-layout</c>, with the addition that this one has to be justified in words.</para>
/// </summary>
public sealed class PortalAttestationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "attest-" + Guid.NewGuid().ToString("N"));

    private const int LivePid = 4242;
    private static readonly DateTime T0 = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LiveStart = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);

    private LeaseStore Store() => new(_root, () => T0, pid => pid == LivePid ? LiveStart : null);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private LeaseOutcome Acquire(PortalEvidenceResult evidence, string? attestation) =>
        LeaseRunner.Acquire(Store(), LeaseResource.Portal, @"C:\projects\P", "agent-a", LivePid,
            TimeSpan.FromMinutes(30), "deploy", evidence, attestation);

    private static PortalEvidenceResult Inconclusive =>
        new(PortalEvidenceVerdict.CannotDecide, "pid 17564 is a Portal process Openness cannot see");

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Without_an_attestation_an_unjudgeable_Portal_still_refuses()
    {
        var outcome = Acquire(Inconclusive, attestation: null);

        Assert.Equal(LeaseResult.EvidenceInconclusive, outcome.Result);

        // And it names the way out, or the refusal is a dead end rather than a decision point.
        Assert.Contains("--attest-portal-unjudgeable", outcome.Detail);
    }

    [Fact]
    public void An_attestation_lets_the_lease_be_taken()
    {
        var outcome = Acquire(Inconclusive, "owner confirmed pids 17564 and 17264 are stale");

        Assert.Equal(LeaseResult.Acquired, outcome.Result);
    }

    /// <summary>
    /// 🔴 <b>The whole point: the attestation is RECORDED on the lease.</b> An override that left no
    /// trace would turn "the gate passed" and "somebody waved the gate through" into the same report,
    /// and the second is the one a later reader needs to see.
    /// </summary>
    [Fact]
    public void The_attestation_is_carried_into_the_lease_and_visible_in_status()
    {
        var store = Store();
        LeaseRunner.Acquire(store, LeaseResource.Portal, @"C:\projects\P", "agent-a", LivePid,
            TimeSpan.FromMinutes(30), "deploy", Inconclusive, "owner confirmed pids 17564 and 17264 are stale");

        var held = store.Read(LeaseResource.Portal, @"C:\projects\P");

        Assert.NotNull(held);
        Assert.Contains("ATTESTED, NOT MEASURED", held!.Purpose);
        Assert.Contains("17564", held.Purpose);

        // The original purpose survives alongside it rather than being replaced.
        Assert.Contains("deploy", held.Purpose);
    }

    /// <summary>
    /// 🔴 <b>AND IT DOES NOT COVER A MEASURED HOLDER.</b> <c>HeldOutsideTheTool</c> is a FACT — a named
    /// pid has the project open — and an attestation that contradicts a measurement is not an
    /// attestation, it is an override of the measurement. There is deliberately no flag for that, and
    /// this test is what stops one being added by widening the branch above.
    /// </summary>
    [Fact]
    public void An_attestation_CANNOT_override_a_measured_human_holder()
    {
        var outcome = Acquire(
            new PortalEvidenceResult(PortalEvidenceVerdict.HeldOutsideTheTool, "pid 8814 has this project open"),
            "I promise nobody is using it");

        Assert.Equal(LeaseResult.HeldOutsideTheTool, outcome.Result);
        Assert.DoesNotContain("ATTESTED", outcome.Detail);
    }

    /// <summary>
    /// Nor does it cover stale or unreadable evidence, which is checked FIRST. The attestation is about
    /// the unjudgeable PROCESS, not about the document's age — a person cannot vouch for what a
    /// two-hour-old snapshot says is true now.
    /// </summary>
    [Fact]
    public void An_attestation_CANNOT_make_stale_evidence_usable()
    {
        var outcome = Acquire(
            new PortalEvidenceResult(PortalEvidenceVerdict.Unusable, "the portal evidence is 63.9 min old"),
            "I looked just now");

        Assert.Equal(LeaseResult.Invalid, outcome.Result);
    }

    /// <summary>Whitespace is not an attestation.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_attestation_is_not_one(string attestation)
    {
        Assert.Equal(LeaseResult.EvidenceInconclusive, Acquire(Inconclusive, attestation).Result);
    }
}
