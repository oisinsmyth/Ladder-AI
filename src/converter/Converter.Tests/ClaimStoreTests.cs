using Converter.Claims;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-65 component 1 — the storage layer: filename encoding, record round-trip, and release policy.
/// The encoding tests matter more than they look: sanitisation alone maps distinct values onto one
/// filename, and two different claims collapsing into one file is a coordination tool losing a claim.
/// </summary>
public class ClaimStoreTests : IDisposable
{
    private readonly string _projectDir = ClaimsTestCorpus.Create();
    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();

    [Fact]
    public void FileName_DistinguishesValuesThatSanitiseIdentically()
    {
        // "A/B" and "A_B" both sanitise to "A_B"; the hash of the exact value is what keeps them apart.
        var first = ClaimStore.FileNameFor(ClaimKind.Tag, "A/B");
        var second = ClaimStore.FileNameFor(ClaimKind.Tag, "A_B");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FileName_IsStableForTheSameValue() =>
        Assert.Equal(
            ClaimStore.FileNameFor(ClaimKind.AlarmBit, "DB_Alarms.Word0.%X3"),
            ClaimStore.FileNameFor(ClaimKind.AlarmBit, "DB_Alarms.Word0.%X3"));

    // REGRESSION, measured on JOB9004 2026-08-24. An agent read candidate values out of a CRLF file,
    // so every value carried a trailing '\r'. FileNameFor hashed the value WITH the CR while Parse
    // strips '\r' on read — so the claim was keyed under a name it did not record, a later lookup
    // on the clean name computed a different filename and missed, and the value stayed claimable by
    // the next agent. Mutual exclusion was lost SILENTLY: `claims --json` rendered the store clean,
    // because the read path had already dropped the CR.
    [Fact]
    public void FileName_IgnoresSurroundingWhitespace_SoACrlfReadCannotShadowAClaim() =>
        Assert.Equal(
            ClaimStore.FileNameFor(ClaimKind.Tag, "AQ1_DrumA_SpeedRef"),
            ClaimStore.FileNameFor(ClaimKind.Tag, "AQ1_DrumA_SpeedRef\r"));

    [Fact]
    public void Acquire_WithTrailingCarriageReturn_IsFoundByTheCleanValue()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);

        var (acquired, _) = store.TryAcquire("ir/p", ClaimKind.Tag, "DI1_Auger_Running\r", "agent-a", null);
        Assert.True(acquired);

        // The whole point: the clean name must now be TAKEN, not free.
        Assert.NotNull(store.Find(ClaimKind.Tag, "DI1_Auger_Running"));

        // And a second agent asking for the clean name must be refused.
        var (second, winner) = store.TryAcquire("ir/p", ClaimKind.Tag, "DI1_Auger_Running", "agent-b", null);
        Assert.False(second);
        Assert.Equal("agent-a", winner.Agent);
    }

    [Fact]
    public void Acquire_RecordsTheNormalisedValue_SoTheRecordMatchesItsFilename()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);

        store.TryAcquire("ir/p", ClaimKind.Tag, "  DQ9_MixerA_Start\r", "agent-a", null);

        var found = store.Find(ClaimKind.Tag, "DQ9_MixerA_Start");
        Assert.NotNull(found);
        Assert.Equal("DQ9_MixerA_Start", found!.Value);
    }

    [Fact]
    public void Release_AcceptsTheCleanValue_ForAClaimTakenWithWhitespace()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);
        store.TryAcquire("ir/p", ClaimKind.Tag, "DQ35_Plant_JogEnable\n", "agent-a", null);

        var released = store.Release(ClaimKind.Tag, "DQ35_Plant_JogEnable", "agent-a", force: false, out _);

        Assert.True(released);
        Assert.Null(store.Find(ClaimKind.Tag, "DQ35_Plant_JogEnable"));
    }

    [Fact]
    public void SerializeParse_RoundTrips()
    {
        var claim = new Claim("ir/p", ClaimKind.AlarmBit, "DB_Alarms.Word0.%X3", "agentA", "an alarm", new DateTime(2026, 8, 7, 9, 30, 0, DateTimeKind.Utc));

        var parsed = ClaimStore.Parse(ClaimStore.Serialize(claim));

        Assert.Equal(claim, parsed);
    }

    [Fact]
    public void Parse_KeepsAnEmptyPurpose()
    {
        var claim = new Claim("ir/p", ClaimKind.Tag, "T", "A", null, new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc));

        Assert.Null(ClaimStore.Parse(ClaimStore.Serialize(claim)).Purpose);
    }

    [Fact]
    public void Parse_RejectsARecordMissingARequiredField() =>
        Assert.Throws<ClaimFormatException>(() => ClaimStore.Parse("kind tag\nvalue T\n"));

    [Fact]
    public void Purpose_CannotForgeASecondField()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);

        store.TryAcquire("ir/p", ClaimKind.Tag, "T1", "A", "line one\nagent EVIL");

        // The newline is folded, so the injected "agent EVIL" cannot become a field on read.
        Assert.Equal("A", store.Find(ClaimKind.Tag, "T1")!.Agent);
    }

    [Fact]
    public void Release_RefusesAnotherAgentsClaimWithoutForce()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);
        store.TryAcquire("ir/p", ClaimKind.Tag, "T2", "A", null);

        var released = store.Release(ClaimKind.Tag, "T2", "B", force: false, out var reason);

        Assert.False(released);
        Assert.Contains("held by agent 'A'", reason);
        Assert.NotNull(store.Find(ClaimKind.Tag, "T2"));
    }

    [Fact]
    public void Release_WithForce_SaysItWasForced()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);
        store.TryAcquire("ir/p", ClaimKind.Tag, "T3", "A", null);

        var released = store.Release(ClaimKind.Tag, "T3", "B", force: true, out var reason);

        Assert.True(released);
        Assert.Contains("FORCED", reason);
        Assert.Null(store.Find(ClaimKind.Tag, "T3"));
    }

    /// <summary>
    /// 🔴 <b>A DELIBERATE ASYMMETRY, PINNED SO THAT "consistent" NORMALISATION CANNOT QUIETLY REMOVE
    /// IT.</b> <c>"Agent-A "</c> — different case, trailing space — is not <c>"agent-a"</c>, and
    /// <c>ClaimStore.Release</c> (the <c>StringComparison.Ordinal</c> at <c>ClaimStore.cs:240</c> and
    /// again at <c>:247</c>) refuses it.
    ///
    /// <para><b>Loose comparison is safe for a REFUSAL; strict comparison is safe for a PERMISSION.</b>
    /// Release is a permission — it deletes a claim another agent may be relying on right now — so the
    /// question it asks is "prove you are the holder", and an over-eager match answers yes for someone
    /// who is not. Elsewhere in this tool the opposite convention is right and is used deliberately:
    /// <c>RejectDoubledRoot</c> matches the project slug case-INsensitively, because there the loose
    /// match produces a refusal and the cost of being generous is a false stop, not a false grant.</para>
    ///
    /// <para>Without this test the asymmetry reads as an oversight. A future pass that "consistently
    /// normalises agent identifiers" — trim, lowercase, one comparer everywhere — would leave every
    /// other claim test green while enabling cross-agent release, which is the exact collision the
    /// registry exists to remove. If this test is ever changed to expect a release, that is a policy
    /// change and belongs in <c>docs/evidence/fi-65-claims-build.md</c>, not in a tidy-up.</para>
    /// </summary>
    [Fact]
    public void Release_RefusesAnAgentStringThatMerelyRESEMBLESTheHolder()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);
        store.TryAcquire("ir/p", ClaimKind.Tag, "T4", "agent-a", null);

        var released = store.Release(ClaimKind.Tag, "T4", "Agent-A ", force: false, out var reason);

        Assert.False(released);
        Assert.Contains("held by agent 'agent-a'", reason);
        Assert.NotNull(store.Find(ClaimKind.Tag, "T4"));
    }

    /// <summary>
    /// The other direction, and it is not decoration: a comparison strict enough to refuse
    /// <c>"Agent-A "</c> must still let the actual holder go. A store that refused every release would
    /// pass the test above and be useless.
    /// </summary>
    [Fact]
    public void Release_ByTheExactHolderStillSucceeds()
    {
        var store = new ClaimStore(_claimsRoot, _projectDir);
        store.TryAcquire("ir/p", ClaimKind.Tag, "T5", "agent-a", null);

        var released = store.Release(ClaimKind.Tag, "T5", "agent-a", force: false, out var reason);

        Assert.True(released, reason);
        Assert.DoesNotContain("FORCED", reason);
        Assert.Null(store.Find(ClaimKind.Tag, "T5"));
    }

    [Fact]
    public void Slug_SeparatesProjectsSharingOneClaimsRoot()
    {
        var a = new ClaimStore(_claimsRoot, Path.Combine("ir", "project-a"));
        var b = new ClaimStore(_claimsRoot, Path.Combine("ir", "project-b"));

        Assert.NotEqual(a.Directory, b.Directory);
    }

    [Fact]
    public void Slug_IgnoresATrailingSeparator()
    {
        var bare = new ClaimStore(_claimsRoot, Path.Combine("ir", "p"));
        var trailing = new ClaimStore(_claimsRoot, Path.Combine("ir", "p") + Path.DirectorySeparatorChar);

        Assert.Equal(bare.Directory, trailing.Directory);
    }

    public void Dispose() => ClaimsTestCorpus.Delete(_projectDir, _claimsRoot);
}
