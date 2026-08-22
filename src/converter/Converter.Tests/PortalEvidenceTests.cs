using System;
using Ladder.Converter.Leases;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// 🔴 <b>The half of the lease that stops it lying.</b>
///
/// <para>FI-65's objection 11: <i>"An engineer can open the project in Portal by hand at any moment
/// (and does). A queue that does not include them is advisory, and an advisory lock over a resource
/// another actor can take is a lie that eventually gets believed."</i></para>
///
/// <para><b>Every test here asserts a REFUSAL except one.</b> That is the shape of the thing: the only
/// path to a Portal lease is evidence that positively rules a person out, and every other outcome —
/// no evidence, stale evidence, unreadable evidence, a process nobody can classify — has to land on
/// "no". The failure being designed against is not a wrong refusal; it is a grant issued while somebody
/// was sitting in the project.</para>
/// </summary>
public sealed class PortalEvidenceTests
{
    private const string Target = @"C:\projects\GenProject1";
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(5);

    private static string Evidence(params string[] processes) =>
        "{ \"processes\": [" + string.Join(",", processes) + "], \"counts\": { \"total\": " + processes.Length + " } }";

    private static string Process(string? projectPath, bool opennessVisible = true, bool launchedByThisTool = false, int pid = 1234) =>
        "{ \"pid\": " + pid
        + ", \"projectPath\": " + (projectPath is null ? "null" : "\"" + projectPath.Replace("\\", "\\\\") + "\"")
        + ", \"opennessVisible\": " + (opennessVisible ? "true" : "false")
        + ", \"launchedByThisTool\": " + (launchedByThisTool ? "true" : "false") + " }";

    // ---------------------------------------------------------------------------------------------
    // The one path that clears.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_empty_but_well_formed_process_list_clears_because_it_positively_reports_no_Portal()
    {
        var verdict = PortalEvidence.Judge(Evidence(), Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Clear, verdict.Verdict);
        Assert.Contains("0 Portal process(es) examined", verdict.Detail);
    }

    [Fact]
    public void A_visible_Portal_holding_a_DIFFERENT_project_does_not_block_this_one()
    {
        var verdict = PortalEvidence.Judge(Evidence(Process(@"C:\projects\SomethingElse\x.ap20")), Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Clear, verdict.Verdict);
    }

    // ---------------------------------------------------------------------------------------------
    // A person has it.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_Portal_we_did_not_launch_holding_the_target_is_refused_AND_NAMES_THE_PID()
    {
        var verdict = PortalEvidence.Judge(
            Evidence(Process(@"C:\projects\GenProject1\GenProject1.ap20", pid: 8814)), Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.HeldOutsideTheTool, verdict.Verdict);
        Assert.Contains("pid 8814", verdict.Detail);
        Assert.Contains("no TTL on a human", verdict.Detail);
    }

    /// <summary>
    /// The lease target may be written either way; Portal always reports the file. Both spellings have
    /// to match the same process, or a lease taken on the folder would sail past a person holding it.
    /// </summary>
    [Fact]
    public void The_target_matches_whether_it_names_the_ap20_or_the_folder_holding_it()
    {
        var evidence = Evidence(Process(@"C:\projects\GenProject1\GenProject1.ap20"));

        Assert.Equal(PortalEvidenceVerdict.HeldOutsideTheTool,
            PortalEvidence.Judge(evidence, @"C:\projects\GenProject1", Fresh).Verdict);
        Assert.Equal(PortalEvidenceVerdict.HeldOutsideTheTool,
            PortalEvidence.Judge(evidence, @"C:\projects\GenProject1\GenProject1.ap20", Fresh).Verdict);
    }

    /// <summary>
    /// A sibling project in the same parent folder must NOT match. A prefix comparison would make one
    /// lease silently cover its neighbours — and this machine carries about nineteen real site
    /// projects in sibling folders.
    /// </summary>
    [Fact]
    public void A_SIBLING_project_in_the_same_parent_folder_is_not_the_same_project()
    {
        var verdict = PortalEvidence.Judge(
            Evidence(Process(@"C:\projects\GenProject1Backup\GenProject1Backup.ap20")), Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Clear, verdict.Verdict);
    }

    /// <summary>
    /// Our own tooling holding the project is the contention the LEASE arbitrates, not a human. Refusing
    /// here would make the gate refuse every agent that legitimately left a Portal open.
    /// </summary>
    [Fact]
    public void A_Portal_THIS_TOOLING_launched_is_not_treated_as_a_person()
    {
        var verdict = PortalEvidence.Judge(
            Evidence(Process(@"C:\projects\GenProject1\GenProject1.ap20", launchedByThisTool: true)), Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Clear, verdict.Verdict);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 The fail-open case, and the reason this class exists at all.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>An Openness-invisible Portal reports <c>projectPath: null</c> — character for character what a
    /// Portal with nothing open reports.</b> Only <c>opennessVisible</c> separates them. A gate reading
    /// the path alone would clear the one process it knows least about, and <c>portal-status</c> has
    /// measured this happening: it reported one process while the OS showed two.
    /// </summary>
    [Fact]
    public void An_OS_ONLY_Portal_is_CANNOT_DECIDE_and_never_clear()
    {
        var verdict = PortalEvidence.Judge(
            Evidence(Process(null, opennessVisible: false, pid: 5150)), Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.CannotDecide, verdict.Verdict);
        Assert.Contains("pid 5150", verdict.Detail);
    }

    /// <summary>
    /// The same shape as the test above, spelled out separately because it is the assertion that would
    /// fail if someone "simplified" the sweep into the path comparison: a null path is indistinguishable
    /// from an idle Portal, so the two documents differ ONLY in the visibility flag.
    /// </summary>
    [Fact]
    public void A_null_project_path_alone_clears_only_when_the_process_IS_visible()
    {
        Assert.Equal(PortalEvidenceVerdict.Clear,
            PortalEvidence.Judge(Evidence(Process(null)), Target, Fresh).Verdict);

        Assert.Equal(PortalEvidenceVerdict.CannotDecide,
            PortalEvidence.Judge(Evidence(Process(null, opennessVisible: false)), Target, Fresh).Verdict);
    }

    /// <summary>
    /// A definite holder outranks an unjudgeable process in the message, because naming the PID that
    /// actually has the project is more actionable than "something here could not be classified". Both
    /// refuse, so this is about which sentence the reader gets.
    /// </summary>
    [Fact]
    public void A_definite_holder_is_reported_even_when_an_unjudgeable_process_is_also_present()
    {
        var verdict = PortalEvidence.Judge(
            Evidence(
                Process(null, opennessVisible: false, pid: 1),
                Process(@"C:\projects\GenProject1\GenProject1.ap20", pid: 2)),
            Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.HeldOutsideTheTool, verdict.Verdict);
        Assert.Contains("pid 2", verdict.Detail);
    }

    // ---------------------------------------------------------------------------------------------
    // Evidence that cannot be used.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Evidence_older_than_the_limit_is_refused_and_says_the_age_came_from_the_file_mtime()
    {
        var verdict = PortalEvidence.Judge(Evidence(), Target, TimeSpan.FromMinutes(9));

        Assert.Equal(PortalEvidenceVerdict.Unusable, verdict.Verdict);
        Assert.Contains("LAST-WRITE TIME", verdict.Detail);
    }

    /// <summary>
    /// The staleness check runs BEFORE the document is parsed, so an old file cannot clear on the
    /// strength of its contents. Ordering, not decoration: a stale document describing an empty machine
    /// is exactly the one most likely to be believed.
    /// </summary>
    [Fact]
    public void Staleness_beats_a_clean_looking_document()
    {
        var verdict = PortalEvidence.Judge(Evidence(), Target, PortalEvidence.DefaultMaxAge + TimeSpan.FromSeconds(1));

        Assert.Equal(PortalEvidenceVerdict.Unusable, verdict.Verdict);
    }

    [Fact]
    public void An_empty_file_is_not_an_empty_Portal_list()
    {
        var verdict = PortalEvidence.Judge("   ", Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Unusable, verdict.Verdict);
        Assert.Contains("nothing was examined", verdict.Detail);
    }

    [Fact]
    public void Unreadable_JSON_is_refused_rather_than_treated_as_no_processes()
    {
        var verdict = PortalEvidence.Judge("{ not json", Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Unusable, verdict.Verdict);
    }

    /// <summary>
    /// Well-formed JSON that is not <c>portal-status</c> output — the wrong file passed by mistake. It
    /// parses, it has no processes, and on a naive reading it looks like the cleanest possible machine.
    /// </summary>
    [Fact]
    public void A_document_with_no_processes_array_is_refused_because_it_is_not_portal_status_output()
    {
        var verdict = PortalEvidence.Judge("{ \"blocks\": [] }", Target, Fresh);

        Assert.Equal(PortalEvidenceVerdict.Unusable, verdict.Verdict);
        Assert.Contains("is not a document reporting none", verdict.Detail);
    }
}
