namespace Harness.Cleanup;

/// <summary>
/// <c>harness-cleanup</c>'s exit codes.
///
/// <para><b>Every non-zero code here is a DIFFERENT reason nothing may be removed</b>, and they are
/// separate for the reason this project keeps re-earning: a caller that cannot tell <i>"the graph
/// says everything is still in use"</i> from <i>"nobody told me whether tests are draining"</i> from
/// <i>"the corpus directory was empty"</i> will eventually read all three as a clean sweep.</para>
/// </summary>
public enum CleanupExit
{
    /// <summary>
    /// A batch was planned against a real corpus, a real graph and a stated drain. <b>It may contain
    /// zero removals</b> — that is a finding, not an error, and the denominator on the SCOPE line is
    /// what makes the difference readable.
    /// </summary>
    Planned = 0,

    /// <summary>A flag was missing, malformed, or contradicted another. Nothing was read.</summary>
    Usage = 1,

    /// <summary>
    /// An input DB-7 requires was absent, so eligibility was provable for nothing: no drain report,
    /// no reference graph, or no vector artifacts.
    /// </summary>
    Refused = 2,

    /// <summary>
    /// <b>Empty is not clean.</b> The corpus held no objects, or held none this stage owns. A cleanup
    /// that looked at nothing is not a cleanup that found nothing to do.
    /// </summary>
    NothingExamined = 3,

    /// <summary>Tests are still in flight. DB-7's first rule, and the loudest of the refusals.</summary>
    TestsNotDrained = 4,

    /// <summary>
    /// <c>--yes</c> (or any other confirmed form) was passed. <b>Refused by name</b>: this binary
    /// plans and cannot delete, so a flag that looks like a confirmation must not be quietly ignored —
    /// silence would let a caller believe a deletion happened.
    /// </summary>
    ConfirmationRefused = 5,

    /// <summary>An input file existed but could not be read or parsed. Distinct from absent.</summary>
    InputUnreadable = 6,
}
