namespace Converter.Claims;

// FI-48 component 1 — the reservation registry that lets several agents work one project.
//
// The collisions this exists to prevent are decided WHILE WRITING IR and never cross the TIA
// boundary: two agents each scan the corpus for the next free FB number and both pick 51; both
// verify alarm bit %X9 is free and both take it; both append "network 8" to the same shared FC.
// Each agent's own `converter diff --only` invariance check passes — the conflict exists only
// between them. A log cannot prevent any of it, because a log records what already happened;
// prevention needs a reservation taken BEFORE the work (docs/16-future-ideas.md, FI-48c).
public enum ClaimKind
{
    BlockNumber,    // "FB51"                          — allocation
    AlarmBit,       // "DB_Alarms.ShredderAlarm0.%X9"  — allocation
    DbMember,       // "DB_Settings.NewMember"         — allocation
    BlockNetwork,   // "FC_ControlMain:8"              — allocation
    Tag,            // "MotorRunFeedback"              — allocation
    BlockEdit,      // "FC_ControlMain"                — exclusive
}

// Two semantics, deliberately named rather than left implicit, because they have opposite corpus
// checks and a reader who conflates them will misread every refusal message.
//
//   Allocation — reserve something NOT YET USED. Refused if the corpus already uses it: that is not
//                a claim, it is a collision that already happened and needs a human, not a retry.
//   Exclusive  — reserve write access to something that DOES exist. Refused only if another agent
//                holds it; the corpus containing it is the precondition, not the objection.
public enum ClaimSemantics
{
    Allocation,
    Exclusive,
}

public sealed record Claim(
    string Project,
    ClaimKind Kind,
    string Value,
    string Agent,
    string? Purpose,
    DateTime CreatedUtc);

public enum ClaimResult
{
    Acquired,
    HeldByAnother,      // another agent got there first — the collision, prevented
    AlreadyUsedInCorpus,// an allocation claim on something the project already uses
    NotInCorpus,        // an exclusive claim on something that does not exist
    Invalid,            // malformed value for the kind
    NothingExamined,    // FI-44: the corpus was empty, so "free" would mean nothing
}

public sealed record ClaimOutcome(
    ClaimResult Result,
    Claim? Claim,
    Claim? Holder,
    string Reason)
{
    public bool Ok => Result == ClaimResult.Acquired;
}

// A claim whose premise has since stopped being true. Reported by `claims --check`, never acted on:
// the registry's job is to say so, and deleting another agent's coordination state on a guess is
// exactly the kind of helpfulness that loses work.
public sealed record ClaimConflict(Claim Claim, string Detail);

public sealed record ClaimsReport(
    string ProjectDir,
    string ClaimsDir,
    IReadOnlyList<Claim> Claims,
    IReadOnlyList<ClaimConflict> Conflicts,
    IReadOnlyList<ClaimConflict> Fulfilled,
    IReadOnlyList<Claim> Stale,
    IReadOnlyList<string> Warnings,
    bool CorpusEmpty)
{
    // Only conflicts gate. Fulfilled and stale are reported because someone should look at them, not
    // because they are wrong: an allocation claim whose resource now exists is the NORMAL end state —
    // the agent did the work and the block landed. Gating on it would make `--check` fail at exactly
    // the moment the work succeeded, and a check that cries wolf on success gets ignored on failure.
    public bool HasFindings => Conflicts.Count > 0;
}

public static class ClaimKinds
{
    public static ClaimSemantics SemanticsOf(ClaimKind kind) =>
        kind == ClaimKind.BlockEdit ? ClaimSemantics.Exclusive : ClaimSemantics.Allocation;

    // The wire form. Kept explicit rather than derived from the enum name so the CLI vocabulary and
    // the C# identifiers can move independently — the flag values are a published contract.
    public static string ToToken(ClaimKind kind) => kind switch
    {
        ClaimKind.BlockNumber => "block-number",
        ClaimKind.AlarmBit => "alarm-bit",
        ClaimKind.DbMember => "db-member",
        ClaimKind.BlockNetwork => "block-network",
        ClaimKind.Tag => "tag",
        ClaimKind.BlockEdit => "block-edit",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static bool TryParse(string token, out ClaimKind kind)
    {
        foreach (ClaimKind candidate in Enum.GetValues(typeof(ClaimKind)))
        {
            if (string.Equals(ToToken(candidate), token, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    public static string AllTokens =>
        string.Join(" | ", Enum.GetValues(typeof(ClaimKind)).Cast<ClaimKind>().Select(ToToken));
}
