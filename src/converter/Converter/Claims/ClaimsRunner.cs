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
                $"claimed {ClaimKinds.ToToken(kind)} '{value}' for agent '{agent}'{BandNote(kind, value)}");
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

    // *** AN IN-BAND CLAIM IS ANNOUNCED, BECAUSE IT IS ACCEPTED RATHER THAN CHECKED. ***
    //
    // An explicit `--value FC9001` cannot be verified as a harness claim: a block-number claim is an
    // ALLOCATION, so the block does not exist yet — that is the definition, and ClaimValidator refuses
    // the claim outright if it does — which leaves HarnessScope's number-derived classification with
    // nothing to read. A `--harness` flag would be a caller assertion, forgotten exactly when it
    // matters, so there is none.
    //
    // What is available instead is ATTRIBUTION: the claim is recorded against an agent with a purpose,
    // and this line makes an in-band claim VISIBLE in the outcome rather than indistinguishable from
    // any other. A silent acceptance and a checked one look identical, which is the failure this
    // project keeps finding; a stated one at least has a reader.
    private static string BandNote(ClaimKind kind, string value)
    {
        if (kind != ClaimKind.BlockNumber)
        {
            return string.Empty;
        }

        var (space, number) = ClaimValidator.BlockNumberParts(value);
        if (space is null)
        {
            return string.Empty;
        }

        return ReservedBand.PositionOf(space, number) == BandPosition.Inside
            ? $" — NOTE: this is INSIDE the reserved harness band ({ReservedBand.Describe()}). Accepted, not verified: the block does " +
              "not exist yet, so nothing derivable says whether this is a harness object or a deliverable one. Claim a band number only " +
              "for harness-generated content."
            : string.Empty;
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

        // 🔴 BAND EXHAUSTION IS ITS OWN OUTCOME, NOT "no free number". *** THE FAILURE MODE THE BAND
        // EXISTS TO PREVENT IS SILENTLY ALLOCATING OUTSIDE IT ***: a search that walked past 9999 would
        // hand a harness generator a deliverable number, and every check downstream would stay green —
        // the measured `FC 910` collision arriving by a different road. So the candidate list is
        // confined to the band (ClaimValidator.NumbersFrom) and running out of it says exactly that,
        // naming the band, its declarer and what to do about it. A caller must not be able to read this
        // as "try a higher floor".
        if (kind == ClaimKind.BlockNumber && type is not null && ReservedBand.IsBandAllocation(type, Math.Max(floor, 1)))
        {
            return new ClaimOutcome(ClaimResult.BandExhausted, null, null,
                $"THE RESERVED HARNESS BAND IS EXHAUSTED for {type}: all {candidates.Count} number(s) from {Math.Max(floor, 1)} to " +
                $"{ReservedBand.Declared.LastNumber} are used in the corpus or already claimed. {ReservedBand.Describe()}. " +
                "This allocation was CONFINED to the band and did NOT continue past its end — allocating outside it would hand a " +
                "harness object a deliverable number while every downstream check stayed green. Free band numbers by releasing " +
                "fulfilled claims or deleting dead harness objects, or have the band widened where it is declared.");
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

        // 🔴 THE BUCKET MAY BE SHARED WITH ANOTHER PROJECT, AND THIS IS WHERE A READER CAN ACT ON IT: the
        // listing is the surface that shows the claims themselves, so the caveat belongs next to them
        // rather than only at the moment one is taken. A WARNING deliberately — HasFindings counts only
        // Conflicts, so this cannot gate; the direction is over-refusal and a live job is holding claims
        // in exactly such a bucket. See ClaimStore.BucketAmbiguity for the ruling.
        if (store.BucketAmbiguity is { } ambiguity)
        {
            warnings.Add(ambiguity);
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
        conflicts.AddRange(SharedObjectConflicts(claims, corpus));

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

    /// <summary>
    /// 🔴 2026-08-14. *** EDITING ONE OBJECT SILENTLY CHANGES ANOTHER, AND TWO DIFFERENT NAMES ARE
    /// TWO DIFFERENT FILES, SO THE FILESYSTEM REFUSES NEITHER. *** The same shape as
    /// <see cref="CrossKindConflicts"/>, one relation out — and the one that will bite under
    /// contention, because the artificial corpus is made almost entirely of DBs and UDTs.
    ///
    /// <list type="bullet">
    /// <item><b>A UDT against anything declaring it.</b> A shared UDT is the widest blast radius in
    /// the project: agent A holds <c>block-edit UDT_HopperBlockageIO</c> and agent B holds
    /// <c>block-edit FB_HopperBlockageMonitor</c>, which declares a member of that type. Both
    /// acquisitions are legitimate, both `diff --only` invariance checks pass, and the conflict exists
    /// only BETWEEN them.</item>
    /// <item><b>An FB against its instance DBs.</b> An iDB mirrors its FB's interface, so editing the
    /// FB moves the iDB with it.</item>
    /// </list>
    ///
    /// <para><b>Same agent is never a conflict</b> — an agent editing a UDT and its instantiating
    /// block is doing one coordinated change, and refusing that would make the ordinary case
    /// unworkable, after which the check gets switched off and the cases it was right about go
    /// through unchecked. Only DIFFERENT agents conflict.</para>
    /// </summary>
    private static IEnumerable<ClaimConflict> SharedObjectConflicts(IReadOnlyList<Claim> claims, ClaimCorpus corpus)
    {
        var edits = claims.Where(c => c.Kind == ClaimKind.BlockEdit).ToList();
        if (edits.Count < 2)
        {
            yield break;
        }

        foreach (var held in edits)
        {
            // The UDT relation. DeclarersOfType is empty for a block or DB name, so this loop simply
            // does not fire unless the claim really is on a type.
            foreach (var declarer in corpus.Index.DeclarersOfType(held.Value))
            {
                foreach (var other in edits.Where(e =>
                             string.Equals(e.Value, declarer, StringComparison.Ordinal) &&
                             !string.Equals(e.Agent, held.Agent, StringComparison.Ordinal)))
                {
                    yield return new ClaimConflict(other,
                        $"agent '{other.Agent}' holds an exclusive edit on '{other.Value}', which DECLARES a member of type " +
                        $"'{held.Value}' — and agent '{held.Agent}' holds an exclusive edit on that type. Editing the type changes " +
                        $"'{other.Value}' underneath them; the filesystem cannot see this because the two claims name two different objects.");
                }
            }

            // The instance relation, in the iDB -> FB direction.
            if (!corpus.Index.InstanceOf.TryGetValue(held.Value, out var instantiatedFb))
            {
                continue;
            }

            foreach (var other in edits.Where(e =>
                         string.Equals(e.Value, instantiatedFb, StringComparison.Ordinal) &&
                         !string.Equals(e.Agent, held.Agent, StringComparison.Ordinal)))
            {
                yield return new ClaimConflict(held,
                    $"agent '{held.Agent}' holds an exclusive edit on instance DB '{held.Value}', while agent '{other.Agent}' holds one on " +
                    $"'{instantiatedFb}', the FB it instantiates. An instance DB mirrors its FB's interface, so the FB edit moves this DB with it.");
            }
        }
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
