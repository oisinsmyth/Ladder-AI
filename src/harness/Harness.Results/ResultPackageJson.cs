using System.Text.Json.Nodes;
using Harness.Map;

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
            ["settling"] = package.Settling.State.ToString(),

            // *** THE STATE AND THE REGISTERS IT WAS TAKEN OVER, AS TWO KEYS. *** `settling` keeps its
            // shape so no consumer of the artifact breaks; the detail is a sibling rather than a longer
            // string in the same key, because it is the half that names WHICH register moved and from
            // what to what — and a reader parsing the state must not have to parse prose to get it.
            ["settlingDetail"] = package.Settling.Detail,
            ["admissibility"] = Admissibility(package.Admissibility),

            // *** THE FLOOR THIS RESULT'S EXPECTATIONS WERE JUDGED AGAINST. *** Rendered because it was
            // computed with a hardcoded one-read-per-cycle until 2026-08-18 while the gate used the wave
            // set's real figure, and nothing in the delivered artifact said which number had been used.
            // Null is "no observability report was supplied", which is a caveat and not a pass.
            ["observabilityFloorScans"] = package.ObservabilityFloorScans,

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

            // 🔴 WHAT THE STAMP WAS COMPUTED OVER, AND WHAT IT WAS NOT. `validityStamp.programVersion`
            // above is a hash and cannot be inverted, so on its own it makes a claim nobody can reproduce
            // or bound. This is both halves of the answer: the objects it hashed, and the staged corpus it
            // is measured against.
            ["programUnderTest"] = Program(package.Program),
        };
    }

    /// <summary>
    /// 🔴 <b>THE STAMP'S INPUT SIDE AND ITS DENOMINATOR — measured twice, in two different ways.</b>
    ///
    /// <para><b>2026-08-21:</b> a wave that ran green could not be re-run, because nothing recorded which
    /// program set its stamp came from. That put <c>objects</c> here.</para>
    ///
    /// <para><b><c>docs/18-project-workbench.md</c> §5 "Phase 10 — Wave time", under <i>"THE BUILD STAMP
    /// DOES COVER THE PARAMETER DB"</i>:</b> a wave's stamp was derived over a set the parameter DB was not
    /// in, so <i>"compressing them changes the controller without changing the stamp"</i> — two packages
    /// describing materially different programs, one stamp, and a verifying gateway that would not notice.
    /// That puts <c>coverage</c> here. ⚠️ <b>The eight-object figure that entry first carried was retracted
    /// 2026-08-24: the deployed set was nine, stamp <c>622F3EB7</c>.</b></para>
    ///
    /// <para><b>Three absences, three renderings, none of them an absent key.</b> <c>recorded: false</c> is
    /// "no manifest was recorded for this result". <c>hashedNothing</c> with a null
    /// <c>stagedCorpusSize</c> is "it hashed nothing and nothing said what it should have hashed".
    /// <c>hashed: 0</c> against a stated <c>stagedCorpusSize</c> is the accusation: objects were staged and
    /// the stamp covered NONE of them.</para>
    /// </summary>
    private static JsonObject Program(ProgramManifest? manifest)
    {
        if (manifest is null)
        {
            return new JsonObject
            {
                // NOT an absent key, and not an empty object list either: "nobody recorded it" and "it
                // hashed nothing" are different facts and only the second says anything about a run.
                ["recorded"] = false,
                ["detail"] = "no program manifest was recorded for this result, so what the build stamp was computed over is unknown — this is NOT a claim that the stamp covered the deployment.",
                ["coverage"] = null,
            };
        }

        var objects = new JsonArray();
        foreach (var o in manifest.Objects)
            objects.Add(new JsonObject { ["kind"] = o.Kind, ["name"] = o.Name, ["sha256"] = o.Sha256 });

        return new JsonObject
        {
            ["recorded"] = true,
            ["stamp"] = $"16#{manifest.Stamp:X8}",
            ["objectCount"] = manifest.Objects.Count,
            ["hashedNothing"] = manifest.HashedNothing,
            ["objects"] = objects,
            ["excludedAsSelfReferential"] = Strings(manifest.ExcludedAsSelfReferential),
            ["coverage"] = CoverageOf(manifest.Coverage),
        };
    }

    /// <summary>
    /// <b>Every count, on every run, including when it is zero.</b> <c>MirrorViewModel.RegistersStale</c>
    /// makes the argument: a count that appears only when it is non-zero teaches a reader that its absence
    /// means everything was covered — and then a run that counted nothing reads like a run that covered
    /// everything.
    ///
    /// <para><b>Public because the RUN document renders the same object.</b> <c>LoopCli.Render</c> emits a
    /// <c>programUnderTest</c> block of its own, and a second hand-rolled copy of these keys is how two
    /// artifacts describing ONE derivation come to disagree — the exact split this file's own
    /// <see cref="Strings"/> note records at the serialiser level.</para>
    /// </summary>
    public static JsonNode? CoverageOf(StampCoverage? coverage)
    {
        if (coverage is null)
            return null;

        return new JsonObject
        {
            ["line"] = coverage.Line,
            ["hashed"] = coverage.Hashed,

            // Null is NO DENOMINATOR, never zero. An empty gap list underneath a null here is not a pass.
            ["stagedCorpusSize"] = coverage.CorpusSize,
            ["stagedCorpusStated"] = coverage.CorpusStated,
            ["complete"] = coverage.Complete,

            // Correct, and kept SEPARATE from the gap: hashing the block the stamp is rendered into would
            // be circular, so these are not something anybody should go and close.
            ["excludedAsSelfReferential"] = Strings(coverage.ExcludedAsSelfReferential),

            // 🔴 The defect, named.
            ["presentAndNotHashed"] = Strings(coverage.PresentAndNotHashed),

            // The other direction: the stamp covers something no staged document names.
            ["hashedNotInCorpus"] = Strings(coverage.HashedNotInCorpus),

            // 🔴 In the artifact, not only in the source. A reader of the file is the person most likely to
            // mistake this for coverage of the program.
            ["residual"] = StampCoverage.DeviceResidual,
        };
    }

    /// <summary>
    /// 🔴 <b>ASSERTION COVERAGE IN THE ARTIFACT — because it was in NO artifact at all.</b>
    ///
    /// <para><b>The measured gap.</b> The numerator shipped with two renderings, both to a terminal:
    /// <c>GateCli</c>, and <c>harness-run --generate-only</c>. The run that spends rig time printed
    /// <see cref="StampCoverage"/> and wrote <c>programUnderTest.coverage</c> — a different measurement —
    /// so the assertion figure survived only in scrollback, for one lane of a batch, and no reviewer
    /// opening a result package weeks later could find it or compare two lanes.</para>
    ///
    /// <para>🔴 <b>NO AGGREGATE IS EMITTED, AND THAT IS THE POINT.</b> There is no top-level numerator,
    /// denominator or fraction here, because two subjects summed is the denominator of neither and a
    /// fraction is the shape a consumer would most want to add up. The only fractions in this document are
    /// per subject, and each carries the party that produced its denominator.</para>
    ///
    /// <para><b>Both renderings come from one object.</b> <c>lines</c> is <see cref="AssertionCoverage.Lines"/>
    /// verbatim and <c>blindSpots</c> is <see cref="AssertionCoverage.BlindSpots"/> verbatim, for the same
    /// reason <see cref="CoverageOf"/> is public: two hand-rolled copies of one measurement is how two
    /// artifacts describing one run come to disagree.</para>
    /// </summary>
    public static JsonNode AssertionCoverageOf(AssertionCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        var subjects = new JsonArray();
        foreach (var subject in coverage.Subjects)
        {
            subjects.Add(new JsonObject
            {
                ["subject"] = subject.Subject,

                // Beside the fraction on purpose: deleting assertions is the one remaining way to move the
                // ratio, so the party that owns the denominator is named wherever the number is.
                ["enumerator"] = subject.Enumerator,
                ["numerator"] = subject.Numerator,
                ["denominator"] = subject.Denominator,
                ["fraction"] = subject.Fraction,
                ["vectorsResolvedHere"] = subject.VectorsResolvedHere,

                // The fact the vector count hides: vectors spent beyond the first on an assertion already
                // cited. Emitted as zero too — a key that vanishes when nothing is redundant teaches its
                // reader that absence means it was checked.
                ["redundantVectors"] = subject.RedundantVectors,
                ["covered"] = Cited(subject.Covered),
                ["multiplyCited"] = Cited(subject.MultiplyCited),

                // NEVER in the numerator. A citation the enumeration does not answer is an error in the
                // vector, not an extension of the denominator.
                ["citedNotEnumerated"] = Cited(subject.CitedNotEnumerated),
            });
        }

        return new JsonObject
        {
            // NotComputed / NoDenominator / Computed — three states, none of which is a zero fraction.
            ["state"] = coverage.State.ToString(),
            ["vectorsExamined"] = coverage.VectorsExamined,
            ["vectorsCitingNothing"] = Strings(coverage.VectorsCitingNothing),
            ["vectorsUnresolved"] = Strings(coverage.VectorsUnresolved),
            ["subjects"] = subjects,

            // 🔴 In the artifact, not only on the terminal. A reader of the file is the person most likely
            // to quote the fraction onward as a completeness claim.
            ["blindSpots"] = Strings(AssertionCoverage.BlindSpots),
            ["lines"] = Strings(coverage.Lines()),
        };
    }

    private static JsonArray Cited(IReadOnlyList<CitedAssertion> cited)
    {
        var array = new JsonArray();
        foreach (var assertion in cited)
        {
            array.Add(new JsonObject
            {
                ["assertionId"] = assertion.AssertionId,

                // Every vector, not a count: two vectors citing one assertion is two vectors and ONE unit
                // of coverage, and naming them is what makes the redundancy actionable.
                ["vectors"] = Strings(assertion.Vectors),
            });
        }

        return array;
    }

    /// <summary>
    /// A list of names as JSON strings.
    ///
    /// <para><c>JsonArray.Add(string)</c> binds to the GENERIC overload and boxes a
    /// <c>JsonValueCustomized&lt;string&gt;</c>, which then throws on serialise for want of a
    /// <c>TypeInfoResolver</c> — a renderer that builds a correct object and a serialiser that cannot
    /// write it, which is exactly the split these tests go through a real serialise to catch.
    /// <c>JsonValue.Create</c> produces the plain string node.</para>
    /// </summary>
    private static JsonArray Strings(IReadOnlyList<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(JsonValue.Create(value));

        return array;
    }

    private static JsonArray Assertions(IReadOnlyList<AssertionOutcome> assertions)
    {
        var array = new JsonArray();

        foreach (var a in assertions)
        {
            var row = new JsonObject
            {
                ["assertionId"] = a.AssertionId,
                ["signal"] = a.Signal,
                ["expected"] = a.Expected,
                ["observed"] = a.Observed,
                ["state"] = a.State.ToString(),

                // Spelled out because NotObserved is neither a pass nor a failure, and a consumer
                // counting states must not have to know that from a doc comment it cannot read.
                //
                // 🔴 *** IT READ `State != NotObserved` UNTIL 2026-08-17, WHICH SILENTLY ADMITTED THE NEW
                // Inconclusive STATE AS EVIDENCE ABOUT THE BLOCK. *** The property is now on the outcome
                // itself, so there is one definition of "says something" rather than a negation that a
                // future state joins by default — a fail-open enumeration is how the count that mattered
                // (`conclusiveAboutTheBlock`) would have been wrong again in a new way.
                ["saysSomethingAboutTheBlock"] = a.SaysSomethingAboutTheBlock,
            };

            if (a.Detail is not null)
                row["detail"] = a.Detail;

            // 🔴 *** WHEN IT WAS OBSERVED, AND OUT OF HOW MANY OBSERVATIONS. *** Absent on outcomes taken
            // by a path that does not record it, and ABSENT rather than zero-filled: a window of all zeros
            // would read as "measured, and nothing was seen".
            if (a.Window is { } w)
            {
                row["observation"] = new JsonObject
                {
                    ["source"] = w.Source.ToString(),
                    ["framesRead"] = w.FramesRead,
                    ["framesConsidered"] = w.FramesConsidered,
                    ["framesAgreed"] = w.FramesAgreed,
                    ["framesOutOfWindow"] = w.FramesOutOfWindow,
                    ["windowWasDeclared"] = w.WindowWasDeclared,
                    ["windowAtFinalFrame"] = w.WindowAtFinalFrame.ToString(),
                    ["firstScan"] = w.FirstScan,
                    ["lastScan"] = w.LastScan,
                    ["decidingScan"] = w.DecidingScan,
                    ["pollsObserved"] = w.PollsObserved,
                    ["distinctFrames"] = w.DistinctFrames,
                    ["retainedFrames"] = w.RetainedFrames,
                    ["seriesTruncated"] = w.SeriesTruncated,

                    // 🔴 *** WHICH PART OF THE INDEX THE VERDICT WAS TAKEN OVER. *** `seriesTruncated: true`
                    // was on every row of the measured package including the three that accused a
                    // site's block from frames taken four minutes before the stimulus — it says only
                    // THAT frames were dropped. 1 = every change retained; higher = a uniform sample at
                    // that resolution, spanning the whole index. Emitted on EVERY row, so its absence
                    // cannot be read as "not sampled".
                    ["retentionStride"] = w.RetentionStride,

                    // 🔴 *** WHICH QUESTION THE FOLD WAS ASKED. *** Emitted on EVERY row including
                    // `Unstated`, rather than only when a shape was declared: an absent key reads as "fine"
                    // to everyone who did not write it, and `Unstated` is a positive fact about the vector
                    // — it is the reason a mixed series came back Inconclusive rather than judged. A
                    // consumer comparing two Held rows needs it, because Held-under-AtSomePoint and
                    // Held-under-Throughout are different claims about the block.
                    ["temporalShape"] = w.Shape.ToString(),
                    ["detail"] = w.Describe(),
                };
            }

            array.Add(row);
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
            // *** `checked` IS EMITTED BESIDE `state` BECAUSE A READER CANNOT DERIVE IT FROM THE NAME. ***
            // Two of seven states are passes and the rest are not, and a consumer reasoning "not
            // premiseOutOfDate, therefore fine" would read every NOT-CHECKED state as a pass.
            ["checked"] = bounds.Checked,
            ["premiseOutOfDate"] = bounds.PremiseOutOfDate,
            // Null unless the enumeration was consulted about a specific assertion — a verified no-bound
            // claim must name what it was verified against, or "verified" is just a word in a report.
            ["citedAssertion"] = bounds.CitedAssertion,
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
