using Converter.Claims;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-50 component 1 — the storage layer: filename encoding, record round-trip, and release policy.
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
