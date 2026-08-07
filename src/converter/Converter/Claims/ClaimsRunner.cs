namespace Converter.Claims;

public static class ClaimsRunner
{
    // A claim older than this is reported as stale by --check. Reported only: an agent can legitimately
    // hold a claim across a long stage (the telemetry has stages running 75 minutes), and a registry
    // that expires claims out from under live work causes the collision it exists to prevent.
    public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromHours(24);

    public static ClaimOutcome Acquire(
        ClaimCorpus corpus,
        ClaimStore store,
        string projectDir,
        ClaimKind kind,
        string value,
        string agent,
        string? purpose)
    {
        var rejection = ClaimValidator.Reject(corpus, kind, value);
        if (rejection is not null)
        {
            return rejection;
        }

        var (acquired, winner) = store.TryAcquire(projectDir, kind, value, agent, purpose);

        if (acquired)
        {
            return new ClaimOutcome(ClaimResult.Acquired, winner, null,
                $"claimed {ClaimKinds.ToToken(kind)} '{value}' for agent '{agent}'");
        }

        // Re-claiming your own claim is idempotent. An agent that retries after a crash, or that runs
        // the same step twice, should not be told it lost a race against itself.
        if (string.Equals(winner.Agent, agent, StringComparison.Ordinal))
        {
            return new ClaimOutcome(ClaimResult.Acquired, winner, winner,
                $"already claimed by agent '{agent}' at {winner.CreatedUtc:yyyy-MM-ddTHH:mm:ssZ} — no change");
        }

        return new ClaimOutcome(ClaimResult.HeldByAnother, null, winner,
            $"{ClaimKinds.ToToken(kind)} '{value}' is held by agent '{winner.Agent}' " +
            $"since {winner.CreatedUtc:yyyy-MM-ddTHH:mm:ssZ}" +
            (winner.Purpose is null ? "" : $" ({winner.Purpose})"));
    }

    // Allocation walks the candidates lowest-first and takes the first one that is BOTH free in the
    // corpus and un-raced in the store. There is deliberately no read-only "suggest" mode: a
    // non-binding suggestion is the exact race this tool exists to remove — two agents are both told
    // "FB51 is free", both act on it, and one discovers the collision only after doing the work.
    public static ClaimOutcome Allocate(
        ClaimCorpus corpus,
        ClaimStore store,
        string projectDir,
        ClaimKind kind,
        string? type,
        int floor,
        string? within,
        string agent,
        string? purpose)
    {
        if (corpus.IsEmpty)
        {
            return new ClaimOutcome(ClaimResult.NothingExamined, null, null,
                "the project directory indexed no blocks, DBs, types or tags — refusing to allocate " +
                "against an empty corpus (FI-44: empty is not clean)");
        }

        var candidates = ClaimValidator.Candidates(corpus, kind, type, floor, within, out var error).ToList();
        if (error is not null)
        {
            return new ClaimOutcome(ClaimResult.Invalid, null, null, error);
        }

        foreach (var candidate in candidates)
        {
            if (ClaimValidator.Reject(corpus, kind, candidate) is not null)
            {
                continue;
            }

            var outcome = Acquire(corpus, store, projectDir, kind, candidate, agent, purpose);
            if (outcome.Ok)
            {
                return outcome;
            }
        }

        return new ClaimOutcome(ClaimResult.HeldByAnother, null, null,
            $"no free {ClaimKinds.ToToken(kind)} found among {candidates.Count} candidates — " +
            "every one is either used in the corpus or already claimed");
    }

    public static ClaimsReport Check(
        ClaimCorpus corpus,
        ClaimStore store,
        string projectDir,
        DateTime nowUtc,
        TimeSpan? staleAfter = null)
    {
        var claims = store.All();
        var conflicts = new List<ClaimConflict>();
        var fulfilled = new List<ClaimConflict>();
        var warnings = new List<string>();

        // A claims directory holding only OTHER projects' claims is the FI-44 trap in its most likely
        // real form: the path was mistyped or a slug changed, so this project's claims all read as
        // absent and every check passes by examining nothing.
        if (claims.Count == 0 && Directory.Exists(store.Directory) is false)
        {
            warnings.Add($"no claims directory at '{store.Directory}' — 0 claims examined");
        }

        foreach (var claim in claims)
        {
            if (!string.Equals(claim.Project, projectDir, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"claim on {ClaimKinds.ToToken(claim.Kind)} '{claim.Value}' records project " +
                    $"'{claim.Project}' but was checked against '{projectDir}'");
            }

            var rejection = ClaimValidator.Reject(corpus, claim.Kind, claim.Value);
            if (rejection is null)
            {
                continue;
            }

            // An allocation claim whose resource now exists is the normal, successful end state — the
            // agent wrote the block. Separated from genuine conflicts so a successful run does not
            // fail the check that is supposed to catch failures.
            if (rejection.Result == ClaimResult.AlreadyUsedInCorpus &&
                ClaimKinds.SemanticsOf(claim.Kind) == ClaimSemantics.Allocation)
            {
                fulfilled.Add(new ClaimConflict(claim, rejection.Reason + " — release this claim if the work is yours and landed"));
                continue;
            }

            conflicts.Add(new ClaimConflict(claim, rejection.Reason));
        }

        conflicts.AddRange(CrossKindConflicts(claims));

        var threshold = staleAfter ?? DefaultStaleAfter;
        var stale = claims.Where(c => nowUtc - c.CreatedUtc > threshold).ToList();

        return new ClaimsReport(
            projectDir,
            store.Directory,
            claims,
            conflicts,
            fulfilled,
            stale,
            warnings,
            corpus.IsEmpty);
    }

    // The filesystem stops two agents claiming the same VALUE, but not two agents claiming the same
    // BLOCK by different routes: A takes exclusive `block-edit FC_ControlMain` while B reserves
    // `block-network FC_ControlMain:8`. Both succeed, and neither learns about the other until their
    // edits meet. This is the one conflict class the store cannot prevent, so the check has to find it.
    private static IEnumerable<ClaimConflict> CrossKindConflicts(IReadOnlyList<Claim> claims)
    {
        var edits = claims
            .Where(c => c.Kind == ClaimKind.BlockEdit)
            .ToDictionary(c => c.Value, c => c, StringComparer.Ordinal);

        if (edits.Count == 0)
        {
            yield break;
        }

        foreach (var claim in claims.Where(c => c.Kind == ClaimKind.BlockNetwork))
        {
            var colon = claim.Value.LastIndexOf(':');
            var block = colon > 0 ? claim.Value[..colon] : claim.Value;

            if (edits.TryGetValue(block, out var edit) &&
                !string.Equals(edit.Agent, claim.Agent, StringComparison.Ordinal))
            {
                yield return new ClaimConflict(claim,
                    $"agent '{claim.Agent}' reserved a network in '{block}', but agent '{edit.Agent}' " +
                    $"holds an exclusive block-edit claim on it");
            }
        }
    }
}
