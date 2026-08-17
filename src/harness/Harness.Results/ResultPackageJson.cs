using System.Text.Json.Nodes;

namespace Harness.Results;

/// <summary>
/// 🔴 <b>DB-8's result package AS THE ARTIFACT A CONSUMER READS — all seven named contents, every time.</b>
///
/// <para><b>Why this type exists.</b> The first wave that has ever run wrote a <c>result.json</c>
/// carrying six keys: <c>vector</c>, <c>verdict</c>, <c>conclusive</c>, <c>whatToDoNext</c>,
/// <c>caveats</c> and <c>assertions</c>. Of DB-8's seven named contents — observed-versus-expected per
/// assertion, the BASIS citation, the FIDELITY declaration of every model involved, the stimulus check,
/// the CO-RUNNING log slice, the VALIDITY STAMP (DB-2: program version + map hash) and MANIFEST PRESENCE
/// (§9a) — <b>five never reached the file at all.</b> The console <i>rendered</i> several of them, which
/// is precisely the trap <c>download-probe</c>'s own output names: <i>"a consumer reads THAT, never this
/// rendering — anything a renderer drops is gone before a scraper sees it."</i></para>
///
/// <para>🔴 <b>DB-2 WAS UNEXERCISED BY THAT OMISSION, IN THE EXACT WAY DB-2 EXISTS TO PREVENT.</b> The
/// validity stamp is what stops a green obtained BEFORE an invalidating change from being read as
/// current. <see cref="ValidityStamp"/> is on the TYPE — only the serialisation dropped it — so nothing
/// was missing but the last step, and the last step is the only one a consumer can see.</para>
///
/// <para><b>A content that is genuinely unavailable is PRESENT AND SAYS SO, never absent.</b> An absent
/// key reads downstream as "there was none", which is the same absent-versus-empty error this repository
/// keeps re-earning. So <c>basis</c> is <c>{ "cited": false, ... }</c> rather than missing, <c>fidelity</c>
/// is <c>{ "declared": false, ... }</c>, and manifest presence has a fourth state — <c>NotAsked</c> —
/// which is not <c>NotAvailable</c>.</para>
///
/// <para><b><see cref="Db8Contents"/> is the denominator, and it is asserted rather than described.</b> A
/// list of the seven keys lets a test iterate them instead of naming six of seven by hand and calling it
/// coverage — the same reason every other check here prints what it examined.</para>
/// </summary>
public static class ResultPackageJson
{
    /// <summary>
    /// 🔴 <b>Spec §4a DB-8's seven named contents, as the keys they render under.</b>
    ///
    /// <para>Held as data so a schema test can walk it. <b>Do not shorten this list to match what the
    /// renderer currently emits</b> — the direction of the check is the other way round, and the whole
    /// defect it exists for was a renderer that emitted fewer contents than the spec names.</para>
    /// </summary>
    public static IReadOnlyList<string> Db8Contents { get; } = new[]
    {
        "assertions",       // observed versus expected, per assertion
        "basis",            // the basis citation
        "fidelity",         // the fidelity declaration of every model involved
        "stimulus",         // the stimulus check
        "coRunning",        // the co-running log slice
        "validityStamp",    // DB-2: program version + map hash (+ the caveats they rest on)
        "manifestPresence", // section 9a
    };

    /// <summary>One package, whole. Every key in <see cref="Db8Contents"/> is present on every call.</summary>
    public static JsonObject Of(ResultPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return new JsonObject
        {
            ["vector"] = package.VectorId,
            ["slotIndex"] = package.SlotIndex,
            ["waveIndex"] = package.WaveIndex,
            ["verdict"] = package.Verdict.ToString(),
            ["conclusive"] = package.ConclusiveAboutTheBlock,
            ["whatToDoNext"] = package.WhatToDoNext,
            ["summary"] = package.Summary(),

            // The run's own outcome and settling state. Not among DB-8's seven, but they are what the
            // verdict precedence actually turned on, and a consumer reading only the verdict cannot
            // recover them.
            ["runOutcome"] = package.RunOutcome.ToString(),
            ["settling"] = package.Settling.ToString(),
            ["admissibility"] = Admissibility(package.Admissibility),

            // ---- DB-8's seven -----------------------------------------------------------------------
            ["assertions"] = Assertions(package.Assertions),
            ["basis"] = Basis(package.Basis),
            ["fidelity"] = Fidelity(package.Fidelity),
            ["stimulus"] = Stimulus(package.Stimulus),
            ["coRunning"] = CoRunning(package.CoRunners),
            ["validityStamp"] = Stamp(package.Stamp),
            ["manifestPresence"] = Manifest(package.Stimulus),

            // AMB-19. Null is "the question was never asked", which is a caveat rather than a pass, so it
            // is rendered as a state rather than left out.
            ["boundsCurrency"] = BoundsCurrency(package.BoundsCurrency),
        };
    }

    private static JsonArray Assertions(IReadOnlyList<AssertionOutcome> assertions)
    {
        var array = new JsonArray();

        foreach (var a in assertions)
        {
            array.Add(new JsonObject
            {
                ["assertionId"] = a.AssertionId,
                ["signal"] = a.Signal,
                ["expected"] = a.Expected,
                ["observed"] = a.Observed,
                ["state"] = a.State.ToString(),

                // Spelled out because NotObserved is neither a pass nor a failure, and a consumer
                // counting states must not have to know that from a doc comment it cannot read.
                ["saysSomethingAboutTheBlock"] = a.State != AssertionState.NotObserved,
            });
        }

        return array;
    }

    /// <summary>
    /// The basis citation, or the positive fact that there was none.
    ///
    /// <para>The clause says where the requirement came from; <b>the assertion says what would be
    /// observed if the block were correct</b>, and that is the decorrelating half. Both are rendered
    /// because a citation of a clause alone is admissible and nearly worthless.</para>
    /// </summary>
    private static JsonObject Basis(Basis? basis) =>
        basis is null
            ? new JsonObject
            {
                ["cited"] = false,
                ["detail"] = "NO BASIS WAS CITED. This result is not traceable to a requirement clause or to an assertion "
                             + "within one, so a disagreement could only be argued from the code - which is the correlated "
                             + "check this pipeline exists to prevent. The submission gate refuses this, so a package "
                             + "reaching here without one means the gate was bypassed.",
            }
            : new JsonObject
            {
                ["cited"] = true,
                ["clauseId"] = basis.ClauseId,
                ["assertionId"] = basis.AssertionId,
            };

    /// <summary>
    /// The fidelity declaration of the model involved, <b>including what it does NOT represent</b> — which
    /// is the half that decides how far a Pass generalises.
    /// </summary>
    private static JsonObject Fidelity(FidelityDeclaration? fidelity) =>
        fidelity is null
            ? new JsonObject
            {
                ["declared"] = false,
                ["detail"] = "NO MODEL FIDELITY DECLARATION REACHED THIS RESULT (M4). Nothing here says what the stimulus "
                             + "model represents or omits, so no verdict in this package may be generalised beyond the "
                             + "literal registers it read. Unrecorded is NOT CHECKED and is never a pass.",
            }
            : new JsonObject
            {
                ["declared"] = true,
                ["modelId"] = fidelity.ModelId,
                ["declaredBy"] = fidelity.DeclaredBy.ToString(),
                ["validatedAgainstPlantData"] = fidelity.ValidatedAgainstPlantData,
                ["represents"] = Strings(fidelity.Represents),
                ["doesNotRepresent"] = Strings(fidelity.DoesNotRepresent),
            };

    private static JsonObject Stimulus(StimulusReport stimulus) => new()
    {
        ["outcome"] = stimulus.Outcome.ToString(),
        ["confirmed"] = stimulus.Confirmed,
        ["detail"] = stimulus.Detail,
    };

    /// <summary>
    /// §9a's manifest presence — <b>its own key, because DB-8 names it separately from the stimulus
    /// check</b>, and because it answers a different question: the stimulus check asks whether the block
    /// RAN, the manifest asks whether it was ever LOADED.
    /// </summary>
    private static JsonObject Manifest(StimulusReport stimulus) => new()
    {
        // "NotAsked" is a fourth state that ManifestPresence deliberately does not have: the enum's three
        // values are all answers, and this is the absence of the question.
        ["state"] = stimulus.Manifest?.ToString() ?? "NotAsked",
        ["asked"] = stimulus.Manifest is not null,
        ["evidencesTransfer"] = stimulus.Manifest == ManifestPresence.Loaded,
        ["detail"] = stimulus.ManifestDetail,
    };

    /// <summary>
    /// The co-running slice. <b>Three states, not two</b> — see <see cref="ResultPackage.CoRunners"/>: an
    /// empty measurement and an absent one are different facts and only one of them is "ran alone".
    /// </summary>
    private static JsonObject CoRunning(IReadOnlyList<int>? coRunners)
    {
        if (coRunners is null)
        {
            return new JsonObject
            {
                ["recorded"] = false,
                ["ranAlone"] = null,
                ["slots"] = new JsonArray(),
                ["detail"] = "NO CO-RUNNING SLICE WAS RECORDED FOR THIS INDEX. This is NOT 'it ran alone': nothing "
                             + "measured what else was running, so a result obtained under interference would look "
                             + "exactly like one obtained in isolation.",
            };
        }

        var slots = new JsonArray();
        foreach (var slot in coRunners)
            slots.Add(JsonValue.Create(slot));

        return new JsonObject
        {
            ["recorded"] = true,
            ["ranAlone"] = coRunners.Count == 0,
            ["slots"] = slots,
            ["detail"] = coRunners.Count == 0
                ? "measured: no other slot was running alongside this vector at this index (X-E)."
                : $"measured: ran alongside slot(s) {string.Join(", ", coRunners)} (X-E). A green here does not mean the "
                  + "co-running slice was benign - only that nothing detected interference.",
        };
    }

    /// <summary>
    /// 🔴 <b>DB-2's validity stamp: what makes this result trustworthy, and for how long.</b>
    ///
    /// <para>The program version is what was RUNNING and the map hash is what this client ADDRESSED — a
    /// mismatch in the second means every address the run held was a guess. Both are rendered in the form
    /// a reader compares against: the build stamp as the <c>16#XXXXXXXX</c> literal the copy layer
    /// publishes, and also raw, so a consumer need not parse the literal.</para>
    /// </summary>
    private static JsonObject Stamp(ValidityStamp stamp)
    {
        var caveats = new JsonArray();
        foreach (var caveat in stamp.Caveats)
            caveats.Add(JsonValue.Create(caveat));

        return new JsonObject
        {
            ["programVersion"] = $"16#{stamp.ProgramVersion:X8}",
            ["programVersionRaw"] = stamp.ProgramVersion,
            ["mapHash"] = stamp.MapHash,
            ["caveats"] = caveats,
            ["detail"] = "DB-2. A result carrying a program version other than the one now deployed is a result about a "
                         + "program that is no longer running; a result carrying a different map hash addressed a "
                         + "different mirror. Compare BOTH before reading any verdict above as current.",
        };
    }

    private static JsonObject BoundsCurrency(VectorBoundsCurrency? bounds)
    {
        if (bounds is null)
        {
            return new JsonObject
            {
                ["asked"] = false,
                ["state"] = "NotAsked",
                ["premiseOutOfDate"] = null,
                ["detail"] = "NOTHING ASKED WHETHER THIS VECTOR STILL TESTS THE SPECIFIED BOUND (AMB-19). A retune moves no "
                             + "assertion ID, so no citation would dangle and no other check here would notice.",
            };
        }

        var agreed = new JsonArray();
        foreach (var name in bounds.Agreed)
            agreed.Add(JsonValue.Create(name));

        var disagreements = new JsonArray();
        foreach (var d in bounds.Disagreements)
        {
            disagreements.Add(new JsonObject
            {
                ["bound"] = d.Bound,
                ["declaredValue"] = d.DeclaredValue,
                ["specifiedValue"] = d.SpecifiedValue,
            });
        }

        return new JsonObject
        {
            ["asked"] = true,
            ["state"] = bounds.State.ToString(),
            ["premiseOutOfDate"] = bounds.PremiseOutOfDate,
            ["agreed"] = agreed,
            ["disagreements"] = disagreements,
            ["detail"] = bounds.Detail,
        };
    }

    private static JsonObject Admissibility(Admissibility admissibility)
    {
        var refusals = new JsonArray();
        foreach (var (reason, detail) in admissibility.Refusals)
            refusals.Add(new JsonObject { ["reason"] = reason.ToString(), ["detail"] = detail });

        return new JsonObject
        {
            ["admissible"] = admissibility.Admissible,
            ["refusals"] = refusals,
        };
    }

    private static JsonArray Strings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(JsonValue.Create(value));

        return array;
    }
}
