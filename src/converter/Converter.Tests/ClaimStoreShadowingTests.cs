using Converter.Claims;
using Ladder.Wave;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-14, SELF-1. *** TWO PROCESSES CLAIMED ONE RESOURCE ON ONE PROJECT AND BOTH WERE GRANTED
/// AT EXIT 0. *** One passed the shared root <c>…\claims</c>; the other passed
/// <c>…\claims\test-project001</c> — the path a lane report had quoted as "the shared claims store".
/// That second value is the RESOLVED store, not the argument: <see cref="ClaimStore"/> appends the
/// project slug, so it yields <c>…\claims\test-project001\test-project001</c>, a second, empty,
/// private registry that grants everything.
///
/// <para><b>Nothing downstream can catch it and nothing ever will</b> — each store is individually
/// well-formed, correctly named and legitimately empty, and a registry has no second copy to be
/// compared against. So the refusal happens where the argument is interpreted.</para>
///
/// <para>*** BOTH DIRECTIONS. *** Every refusal here is paired with a root that must still WORK,
/// including one whose last segment merely resembles a project name — this lane has twice shipped a
/// test that could not tell a working fence from a missing one, and will not do it a third time.</para>
/// </summary>
public class ClaimStoreShadowingTests : IDisposable
{
    private readonly string _corpusDir = ClaimsTestCorpus.Create();
    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();

    public void Dispose() => ClaimsTestCorpus.Delete(_corpusDir, _claimsRoot);

    // The slug is the project directory's own last segment, so a corpus at …/claims-corpus-<guid>
    // has that as its slug. Derived rather than hardcoded, so these tests follow SlugOf if it changes.
    private string Slug => new ClaimStore(_claimsRoot, _corpusDir).ProjectSlug;

    // ---- The refusal --------------------------------------------------------------------------

    [Fact]
    public void ARootEndingInTheProjectName_IsRefusedByName()
    {
        var doubled = Path.Combine(_claimsRoot, Slug);

        var ex = Assert.Throws<ClaimFormatException>(() => new ClaimStore(doubled, _corpusDir));

        Assert.Contains("the project name TWICE", ex.Message);
        Assert.Contains(Slug, ex.Message);
        // It must say what the store WOULD have been and what to pass instead — a refusal that names
        // neither leaves the caller to guess which of the two paths was wrong.
        Assert.Contains(Path.Combine(doubled, Slug), ex.Message);
        Assert.Contains(_claimsRoot, ex.Message);
    }

    // Windows paths are case-insensitive, so a capital letter doubles just as effectively.
    [Fact]
    public void TheRefusalIsCaseInsensitive()
    {
        var doubled = Path.Combine(_claimsRoot, Slug.ToUpperInvariant());

        Assert.Throws<ClaimFormatException>(() => new ClaimStore(doubled, _corpusDir));
    }

    // A trailing separator is the same argument typed differently and must not slip past.
    [Fact]
    public void ATrailingSeparatorDoesNotEvadeTheRefusal()
    {
        var doubled = Path.Combine(_claimsRoot, Slug) + Path.DirectorySeparatorChar;

        Assert.Throws<ClaimFormatException>(() => new ClaimStore(doubled, _corpusDir));
    }

    // Every entry point builds a store, so every entry point inherits the refusal. Asserted rather
    // than assumed: a guard on one command is a guard the next command routes around.
    [Fact]
    public void EveryEntryPointInheritsTheRefusal_BecauseTheyAllBuildAStore()
    {
        var doubled = Path.Combine(_claimsRoot, Slug);

        // acquire / allocate / list / check / release all begin here.
        Assert.Throws<ClaimFormatException>(() => new ClaimStore(doubled, _corpusDir));
        Assert.Throws<ClaimFormatException>(() => new ClaimStore(doubled, _corpusDir, () => DateTime.UtcNow));
    }

    // ---- 🔴 THE DID-NOT-RUN HALF: what must STILL WORK ------------------------------------------

    // *** A FENCE THAT REFUSES EVERYTHING PASSES EVERY TEST THAT ONLY CHECKS REFUSALS. ***
    [Fact]
    public void AnOrdinarySharedRoot_IsAccepted()
    {
        var store = new ClaimStore(_claimsRoot, _corpusDir);

        Assert.Equal(Path.Combine(_claimsRoot, Slug), store.Directory);
    }

    // The case the refusal must NOT swallow: a shared root whose own last segment looks like a project
    // name, but is not THIS project's. It is a perfectly good root and forks nothing.
    [Fact]
    public void ARootWhoseLastSegmentResemblesADifferentProjectName_IsAccepted()
    {
        var lookalike = Path.Combine(_claimsRoot, Slug + "-other");

        var store = new ClaimStore(lookalike, _corpusDir);

        Assert.Equal(Path.Combine(lookalike, Slug), store.Directory);
    }

    // And a root that merely CONTAINS the project name deeper in the path is untouched — only the
    // final segment doubles.
    [Fact]
    public void ARootContainingTheProjectNameButNotEndingInIt_IsAccepted()
    {
        var nested = Path.Combine(_claimsRoot, Slug, "shared");

        var store = new ClaimStore(nested, _corpusDir);

        Assert.Equal(Path.Combine(nested, Slug), store.Directory);
    }

    // ---- SELF-1 as a regression test, so it does not depend on being remembered ------------------

    /// <summary>
    /// SELF-1's positive case, mechanised: two agents, ONE root, ONE resource — exactly one grant and
    /// one refusal, and the refusal names the holder. This is the assertion the campaign makes about
    /// itself, and it was previously only ever run by hand.
    /// </summary>
    [Fact]
    public void TwoAgentsOneRootOneResource_ExactlyOneGrantAndOneRefusalNamingTheHolder()
    {
        var corpus = ClaimCorpus.Build(_corpusDir);
        var storeA = new ClaimStore(_claimsRoot, _corpusDir);
        var storeB = new ClaimStore(_claimsRoot, _corpusDir);

        var a = ClaimsRunner.Acquire(corpus, storeA, _corpusDir, ClaimKind.BlockNumber, "FB9030", "A", "first");
        var b = ClaimsRunner.Acquire(corpus, storeB, _corpusDir, ClaimKind.BlockNumber, "FB9030", "B", "contender");

        Assert.True(a.Ok);
        Assert.False(b.Ok);
        Assert.Equal(ClaimResult.HeldByAnother, b.Result);
        Assert.Equal("A", b.Holder!.Agent);
        Assert.Contains("first", b.Reason); // the holder's PURPOSE, which is what made the refusal legible

        // ...and the grant path still works after a release, the other half of SELF-1's method.
        Assert.True(storeA.Release(ClaimKind.BlockNumber, "FB9030", "A", force: false, out _));
        Assert.True(ClaimsRunner.Acquire(corpus, storeB, _corpusDir, ClaimKind.BlockNumber, "FB9030", "B", "re-acquire").Ok);
    }

    /// <summary>
    /// *** AND THE FORK, DEMONSTRATED — the state the refusal now makes unreachable. *** Built by
    /// bypassing the argument entirely (two roots that are simply different), because the doubled form
    /// can no longer be constructed. It exists so the fork's SHAPE stays visible: two stores, one
    /// resource, two grants, neither wrong on its own terms.
    /// </summary>
    [Fact]
    public void TwoDifferentRoots_StillForkTheRegistry_WhichIsWhyTheDoubledFormIsRefused()
    {
        var corpus = ClaimCorpus.Build(_corpusDir);
        var other = Path.Combine(_claimsRoot, "a-different-root");

        var a = ClaimsRunner.Acquire(corpus, new ClaimStore(_claimsRoot, _corpusDir), _corpusDir,
            ClaimKind.BlockNumber, "FB9030", "A", "on root one");
        var b = ClaimsRunner.Acquire(corpus, new ClaimStore(other, _corpusDir), _corpusDir,
            ClaimKind.BlockNumber, "FB9030", "B", "on root two");

        Assert.True(a.Ok);
        Assert.True(b.Ok); // BOTH granted — no process can see the other's store
    }

    // ---- ⚠️ The refusal must not break the harness's own rendered invocation ----------------------

    /// <summary>
    /// <see cref="HarnessNumberRange.ClaimArgumentsFor"/> renders REAL `converter claim` invocations
    /// for the harness ledger. Checked explicitly: it renders no <c>--claims</c> at all — the caller
    /// supplies the shared root — so the doubled-root refusal cannot reach it in any form.
    /// </summary>
    [Fact]
    public void TheHarnessRenderedInvocationIsUnaffected_BecauseItSuppliesNoClaimsRoot()
    {
        var rendered = HarnessNumberRange.Declared().ClaimArgumentsFor(
            new NumberedBlock("FC_HarnessCopyLayer", ObjectKind.Function, 9001, BlockOwner.Harness),
            "harness");

        Assert.DoesNotContain("--claims", rendered);
        Assert.Contains("--value FC9001", rendered);
    }
}
