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

    public void Dispose() =>
        ClaimsTestCorpus.Delete(_extraDirs.Concat(new[] { _corpusDir, _claimsRoot }).ToArray());

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

    // ---------------------------------------------------------------------------------------------
    // 🔴 THE SHARED BUCKET — ruled 2026-08-24. `SlugOf` keys on the LAST PATH SEGMENT, so two projects
    // whose IR directory is both called `ir` land in ONE registry. The slug rule is deliberately
    // UNCHANGED (re-keying orphans reservations live agents are holding) and this does not gate (the
    // direction is over-refusal, and a gate would refuse a live job mid-work). What it does is SAY SO
    // where somebody is looking. Both directions, as everything else in this file is.
    // ---------------------------------------------------------------------------------------------

    private readonly List<string> _extraDirs = new();

    /// <summary>A copy of the standard corpus in a directory with a CHOSEN name, so the slug is chosen.</summary>
    private string CorpusInDirectoryNamed(string name)
    {
        var source = ClaimsTestCorpus.Create();
        var parent = Path.Combine(Path.GetTempPath(), $"claims-bucket-{Guid.NewGuid():N}");
        var dir = Path.Combine(parent, name);
        Directory.CreateDirectory(dir);
        _extraDirs.Add(parent);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
        }

        ClaimsTestCorpus.Delete(source);
        return dir;
    }

    [Fact]
    public void AGenericIrDirectoryName_SaysTheBucketIsSHARED_atThePointOfUse()
    {
        var store = new ClaimStore(_claimsRoot, CorpusInDirectoryNamed("ir"));

        Assert.Equal("ir", store.ProjectSlug);
        var note = store.BucketAmbiguity;
        Assert.NotNull(note);

        // The name, the mechanism, and the consequence — a note saying only "this is generic" leaves the
        // reader to work out why they should care.
        Assert.Contains("'ir'", note);
        Assert.Contains("GENERIC DIRECTORY NAME", note);
        Assert.Contains("LAST SEGMENT", note);

        // The direction, so nobody reads it as a corruption risk; and the ruling, so nobody "fixes" the
        // slug and orphans a live job's reservations.
        Assert.Contains("over-refusal", note);
        Assert.Contains("REPORTED, NOT REFUSED", note);
        Assert.Contains("deliberately unchanged", note);
        Assert.Contains("multi-agent-operating-guide.md", note);

        // And the ACTION, which is the only part that changes what the reader does next.
        Assert.Contains("Read the `project` line inside the claims", note);
    }

    [Fact]
    public void APROJECT_IDENTIFYING_NAME_SAYS_NOTHING_because_a_note_on_every_store_is_a_note_nobody_reads()
    {
        // The other direction. `_corpusDir` is `claims-corpus-<guid>` — a name no second project can
        // collide with — and a caveat printed there would be false as well as noisy.
        Assert.Null(new ClaimStore(_claimsRoot, _corpusDir).BucketAmbiguity);
    }

    [Fact]
    public void THE_NOTE_IS_REPORTED_AND_NEVER_GATES_the_claim_still_succeeds_and_the_check_has_no_findings()
    {
        // *** THE HALF THAT MATTERS MOST. *** A live job is holding claims in a bucket named exactly this
        // way; a gate here would refuse it mid-work, which is why the ruling is "report".
        var corpusDir = CorpusInDirectoryNamed("ir");
        var corpus = ClaimCorpus.Build(corpusDir);
        var store = new ClaimStore(_claimsRoot, corpusDir);

        var outcome = ClaimsRunner.Acquire(corpus, store, corpusDir, ClaimKind.BlockNumber, "FB9030", "A", "work");
        Assert.True(outcome.Ok);

        var report = ClaimsRunner.Check(corpus, store, corpusDir, DateTime.UtcNow);
        Assert.Contains(report.Warnings, w => w.Contains("GENERIC DIRECTORY NAME"));
        Assert.False(report.HasFindings); // a warning is not a conflict, and only conflicts gate
        Assert.Empty(report.Conflicts);
    }

    [Fact]
    public void THE_NOTE_REACHES_THE_RENDERED_OUTCOME_and_is_absent_from_an_identified_one()
    {
        var outcome = new ClaimOutcome(ClaimResult.Acquired,
            new Claim("p", ClaimKind.BlockNumber, "FB9030", "A", null, DateTime.UtcNow), null, "claimed");

        var shared = new ClaimStore(_claimsRoot, CorpusInDirectoryNamed("ir"));
        var identified = new ClaimStore(_claimsRoot, _corpusDir);

        var sharedText = ClaimsOutputFormatter.FormatOutcomeText(outcome, shared.Directory, shared.BucketAmbiguity);
        var identifiedText = ClaimsOutputFormatter.FormatOutcomeText(outcome, identified.Directory, identified.BucketAmbiguity);

        Assert.Contains("bucket  ", sharedText);
        Assert.DoesNotContain("bucket", identifiedText);

        // JSON too: the machine-readable surface is the one an agent parses, and a caveat that exists
        // only in the text form is one an automated reader never sees.
        Assert.Contains("GENERIC DIRECTORY NAME",
            ClaimsOutputFormatter.FormatOutcomeJson(outcome, shared.Directory, shared.BucketAmbiguity));
        Assert.DoesNotContain("GENERIC DIRECTORY NAME",
            ClaimsOutputFormatter.FormatOutcomeJson(outcome, identified.Directory, identified.BucketAmbiguity));
    }
}
