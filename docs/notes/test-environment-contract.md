# THE TEST-ENVIRONMENT CONTRACT — what a block must satisfy to be testable

**Status: DRAFT, 2026-08-13. Phase 5.1.** This is the contract half only. **The skill that enforces it
now exists — `.claude/skills/design-for-testability/`** — and §10 below is the surface it and
`harness-gate` implement between them. The skill's body deliberately does not live in this document.

> ***AND THE TWO DRIFT.*** §10's table and the skill's gate table are *claims about code*, and the code
> is under construction: on 2026-08-13 the built gate emitted three gates neither table listed (8c, 10a,
> 10b), and the runner consumed four fields §2 did not name (§2.2, §2.3). **The fix for that is not to
> trust either table** — the skill's Step 0 says to verify each verifier against `src/harness/` before
> calling any gate CHECKED, and that instruction outranks both tables including this one.

**What this is.** `PC-Client-Modbus-Spec-Draft-final.txt` §2.6 says a design can be *unobservable*,
and that the runner will refuse a vector rather than return a meaningless green. It does not say what
an author must therefore supply. This document is that: **the contract between a block's author, the
vector's author, and the runner.**

**It carries no timing constants.** Every number lives in §12a, which is the only place a timing
constant is chosen. *A contract with a stale constant in it is worse than one with a pointer* — so
where a rule needs a figure, it names the §12a derivation and stops.

**Companion documents:** `PC-Client-Modbus-Spec-Draft-final.txt` (the design; §2.6, §7, §12a, D6,
D13, D33, D35, D37, X-B, X-D) · `modbus-tcp-client-spec.md` (the client) ·
`modbus-plc-side-contract.md` (the PLC side).

---

## 1. The contract at a glance

Seven elements. **The right-hand column is the important one** — this project's thesis is that a
mechanical floor survives an agent choosing not to look, so every element is marked with what
actually checks it.

| # | element | mechanically checkable? | by what |
|---|---|---|---|
| 1 | **Vector format** | ✅ fully | schema: required fields, types, one start bool, `MaxDuration` present |
| 2 | **`Basis` — clause** | ⚠️ existence only | citation must **resolve** to a written clause (the `relation-reconcile` precedent). *That it is the right clause is judgement.* |
| 3 | **`Basis` — assertion** | ✅ existence + coverage | assertion ID must come from the **spec-derived enumeration** (§7/D35, defined in `assertion-enumeration.md`), never free text; ordinal form rejected; response signal must appear in `Expectations` |
| 4 | **Observability declaration** | ✅ **fully — and it REFUSES** | declared window ≥ §12a's floor; mode ∈ {latched, sampled, stamped}; signal present in the map; declared **before** the generating download |
| 5 | **Settling declaration** | ⚠️ partial, and the partial half bites | that one exists, and that it is **not merely the completion flag** — both checkable. *Whether it is the right condition is judgement.* |
| 6 | **Start-bool binding** | ✅ fully | exactly one per slot; raised on a later scan (D37); T=0 recorded from the observed scan counter |
| 7 | **Blacklist** | ✅ add-only; ⚠️ density | an entry may only ADD an exclusion; reason string non-empty. *Over-blacklisting is measurable, not preventable.* |

Plus two guards that are not "elements" but are checked at the same gate:

| | | | |
|---|---|---|---|
| **Authorship (D6)** | vector author ≠ block author | ✅ fully | recorded agent identity; a match is a **refusal**, not a warning |
| **Model fidelity (M4)** | a vector may only assert behaviours the model claims to represent | ✅ fully | set-difference against the model's fidelity declaration |

---

## 2. The vector format

A vector is **data**, submitted atomically with its wave set (D31/DB-9). It never contains a register
number — the author speaks **tag names** (D8).

**The names below are the wire names** — the JSON an author actually writes, read
case-insensitively. *An abstract field list is a field list an author has to guess the spelling of, and
this document is the agreement, not a sketch of one.*

```
Vector                        -- one element of the submission's `vectors` array
  id                  stable identifier, unique in the submission
  slot                which slot (methodology) this vector is an index of
  index               its position in the slot's column (D26a)
  author              agent identity. Checked against the block's author (D6)
  clause              `Basis` half one: the specification clause      -- §3
  assertion           `Basis` half two: the assertion ID              -- §3
  assertionForm       When | Never -- the form the ENUMERATION records for it. §3; `Unstated` is REFUSED
  inputs              tag name -> value            -- what is written before T=0
  startBool           the slot's start bool        -- §6
  expectations        [ { signal, nature, mode, windowScans, expected } ]   -- §4; `expected` is §2.2
  settlingCondition   what makes the observed value FINAL   -- §5. PER VECTOR, not per expectation
  settlingSignals     [ tag ]                               -- §5
  maxDurationScans    in SCANS, with a wall-clock backstop (X-B, §12a derivation 4)
  completionSignal    the block's own done-signal              -- §2.2
  completionValue     the value on it that means "finished"    -- §2.2
  blacklist           [ { block, reason } ]        -- §7, add-only
  compressionFactor   the `comp` this vector's scan counts are stated at  -- §4.4
  assertedBehaviours  [ behaviour ]   -- set-differenced against the model's `represents` (M4)
  kills               the wrong implementation this vector would catch    -- §2.2

Submission                    -- the top-level object the vectors are submitted inside
  blockAuthor         agent identity, for D6
  runtimeCompression  ***the `comp` THE WAVE WILL ACTUALLY RUN AT***  -- §2.3. Not any vector's
  slotsInWaveSet          \__ feed §12a derivation 1's floor, which scales with tensor width
  resultRegistersPerSlot  /
  model               { id, represents, doesNotRepresent, validatedAgainstPlantData, compStable }
  enumeration         { clauses, assertions, forms, enumerator }   -- §3
  map                 { providedFor: signal -> [ modes ] }         -- §4.3
  computedConflicts   [ block ]        -- D9's graph as bare names, carrying NO provenance
  conflictEdges       [ { blockA, blockB, provenance, signal, class } ]   -- X-G, provenanced
  blockCompression    { plantMs, budgetMs, presets, negligibleFraction }  -- §2.3
  vectors             [ Vector ]
```

**`MaxDuration` is not optional and is not a formality.** X-B makes it double as the per-test timeout,
and DB-13 needs it to compute wave length, so a vector without one cannot be packed *or* bounded. Its
wall-clock backstop is **computed, not guessed** — the formula is §12a derivation 4, and it includes
an outlier allowance that must not be trimmed.

**Values are engineering values, not register contents.** Byte and word order are the transport's
problem (`modbus-tcp-client-spec.md` §5), and the version register plus a round-trip calibration are
what make that safe.

### 2.1 ✅ RULED 2026-08-13 — ***A SUBMISSION VECTOR MAY NOT BE STEPPED, AND THE REASON IS ARCHITECTURAL***

A vector's `Inputs` are written **once**, before T=0, while nothing is running. **There is no
mechanism for a second set of inputs to arrive part-way through a test, and this is not a schema
limitation to be relaxed later.**

Two reasons, and the second is the owner's and is the load-bearing one:

1. **A mid-test stimulus has no defined relationship to T=0.** T=0 is the start bool's rising edge
   (D37) and every scan-stamp is a difference from it. A second write lands whenever the wire
   delivers it — subject to the round-trip distribution, not to the controller's scan — so a test
   whose behaviour depends on *when* it arrived is not reproducible, and the result would be a
   function of the tunnel rather than of the block.
2. ***AND THE PC IS THE WRONG PLACE FOR IT: A MODEL IN THE PLC SHOULD SUPPLY THE STIMULUS.*** Owner,
   2026-08-13. This is the same argument §11 already makes for models generally — *driving raw
   values over the wire cannot express time* — applied to stimulus rather than to dynamics.
   **Dynamic stimulus belongs to a model running in the controller, on the controller's own
   timebase**, where it is generated at scan rate, is reproducible, and is independent of the link.
   A stepped vector is a request to build a slow, jittery, PC-side model out of the wire protocol.

**What follows for the interface, stated so the refusal is not read as a missing feature:**

- ***THE RUNNER'S STEPPED VECTOR — IF ONE EXISTS INTERNALLY — IS NOT THE SUBMISSION SURFACE.*** A
  `TestVector` as submitted has a single `Inputs` set. Any stepping the runner performs internally
  (an inert establish, then a commit) is **protocol**, not test data, and is not authorable.
- **A test needing changing inputs is a MODEL TASK**, and routes the way M4 already routes fidelity
  gaps: the vector is refused, the refusal **names the missing model capability**, and that is a
  model task rather than an untestable requirement (§7's DEFERRED sub-case).
- **Mechanically checkable:** a submitted vector carrying more than one `Inputs` set, or any
  time-indexed input structure, is rejected at schema. This is the cheapest gate in the contract.

> **Flagged for the design's own consistency:** nothing in §2 or D33 was found to *assume* PC-supplied
> stepping — D33's inert establish and D37's later-scan commit are both protocol steps performed by
> the runner, not authored stimulus, so they are unaffected. **The one place the reading could go
> wrong is X-A's write phase** — *"write modbusTensor(i) across AS MANY TRANSACTIONS AS IT TAKES"* —
> which describes **one** vector's data spanning several transactions **while nothing is running**,
> and must not be read as licence for successive stimuli during a test. Worth a word in X-A if it is
> ever rewritten.

---

### 2.2 The four fields the runner requires and §2 did not name — ADDED 2026-08-13

**Each of these was already consumed by the runner while this document said nothing about it**, which
is the worse half of the failure: not a field an author might omit, but a field an author could not
know to write. All four come from the same shape.

> ***A FIELD THE RUNNER READS AND THE CONTRACT DOES NOT NAME IS A DEFAULT NOBODY CHOSE.***

**`expected` — the predicate, one per expectation.** §2's format sketch always listed a `predicate` and
nothing ever specified it, so the checked type carried the field and no document could supply one.

```
expectations[ ].expected    string. The value this expectation asserts.
                            REQUIRED. Absent is a REFUSAL at the schema gate.
```

***And the refusal is not pedantry — measured.*** With no predicate the runner mapped the absent value
to the literal string `<no predicate>`, compared the observed value against it, and produced a
**disagreement → FAIL**. *A vector that never said what right looks like told its author the block was
wrong.* An expectation with nothing to compare against cannot fail, so its pass says nothing and its
fail says something untrue. Written as a **string** and compared as declared; §2's rule stands — these
are engineering values, never register contents.

**`completionSignal` and `completionValue` — what the poll is waiting for.** §2 named neither. The
runner watches one result register and calls the test complete when it reads a stated value; everything
else is `TIMED-OUT`.

```
completionSignal   tag. The block's own done-signal. REQUIRED; absent is a REFUSAL.
completionValue    integer 0..65535 (it is compared against ONE result register).
                   REQUIRED; absent is a REFUSAL. *** THERE IS DELIBERATELY NO DEFAULT OF 1. ***
```

> 🔴 ***THE DEFAULT OF 1 IS REMOVED, AND THIS IS A DECISION AGAINST THE CONVENIENT READING.*** A
> completion flag reading `1` is nearly universal, which is exactly what makes the default dangerous:
> the rare block that signals completion with a state number (`Step = 90`) is compared against a value
> nobody stated, never reaches it, and returns **`TIMED-OUT`** — whose own message says *"the condition
> may simply never have occurred."* **That is the predicate hole one field over**: a verdict about the
> block, produced from a fact nobody supplied. The two are the same class and cannot have different
> treatments without the distinction being arbitrary.
>
> **What would reverse it:** not inconvenience, and not a corpus in which the answer is always 1 — that
> is the case *for* requiring it. Only a runner that stops comparing the completion signal **by value**
> (watching a rising edge instead) would leave the field with no consumer, and it should then be deleted
> rather than defaulted.

**`kills` — the wrong implementation this vector would catch.** Required by §10's mutation rule, carried
by the checked type, and absent from §2's format. *A vector that no credible wrong implementation would
fail only measures uptime.* REQUIRED; absent is a REFUSAL. **This one is a `JUDGEMENT` in substance and
a schema check in mechanism** — that the string is non-empty is checkable, that it names a credible
mutant is not.

---

### 2.3 `blockCompression` — X-D's block-level and model-level inputs. ADDED 2026-08-13

**The gap this closes, stated plainly:** the runner computes four X-D ceilings, three of which are
properties of the **block** and the **model** rather than of the vectors — the timer bound (the term
X-D says *often binds first*), the model's declared `comp_stable`, and the ratio-distortion bound on
unscaled literals. Until now **a submission document could express none of them**, so the compressed
path was permanently `NOT CHECKED` and the gate's own refusal said *"supply them"* against a document
with nowhere to put them. *A dead end wearing the costume of a build list.*

```
blockCompression                 OBJECT. Required whenever `runtimeCompression` > 1; ignored at 1.
  plantMs             number > 0     T_plant  — how long the behaviour takes IN THE PLANT
  budgetMs            number > 0     T_budget — how long the wave may spend on it
                                     (together: comp_min = T_plant / T_budget, and comp_min is the
                                      number to RUN AT — there is no field for comp_max, by design)
  presets             [ { name, presetMs, source } ]   the block's dwell/debounce presets
      name            tag or member name
      presetMs        number > 0, the preset in MILLISECONDS
      source          "Data" | "Literal"   -- REQUIRED. Absent is a REFUSAL; see below
  negligibleFraction  number in (0,1]      -- ***A FRACTION, NOT A PERCENTAGE***

model
  compStable          number >= 1    -- the model's declared comp_stable (M3/M4)
```

**Why `compStable` sits on `model` and not here.** It is **the model author's number**, declared
alongside `represents` / `doesNotRepresent` and travelling with every result as part of the fidelity
declaration. Duplicating it into a block-scoped object would create two authorities for one figure and
nothing would catch them disagreeing. *(Judgement. **What would reverse it:** a submission that
legitimately carries block compression inputs and no model at all — at which point `compStable` has no
declaring artifact and must move.)*

***`negligibleFraction` is a fraction: 2.5% is `0.025`, not `2.5`.*** An author who writes `2.5` has
declared 250% and will get a ceiling 100x too permissive, silently. **The specification names no value
for it** — it works an example (0.003% of a four-hour interval becoming 2.5% of a twenty-second one) and
calls the good state "negligible" without saying where negligible ends. So it has **no default and is
never invented**: absent, the ratio-distortion bound reports `NOT DECLARED`, which above `comp = 1` is a
refusal, not an exemption.

**`source` has no fail-safe guess, which is why it is refused rather than assumed.** The two real
answers push in **opposite directions**: a `Data` preset scales with the factor and lowers the *timer*
ceiling, a `Literal` one does not scale at all and lowers the *ratio-distortion* ceiling. There is no
direction in which guessing is conservative.

**`presets: []` is a POSITIVE CLAIM and is not the same as an absent `presets`.** An empty array says
*this block has no dwell or debounce preset*, so the timer and ratio-distortion ceilings genuinely
cannot bind and the gate says so as a computed fact. An **absent** `presets` says nobody enumerated
them, which leaves `blockCompression` incomplete — `NOT CHECKED`. *The empty claim is the author's, and
nothing verifies it today.* **What would change that:** the block's timers are already enumerable from
its IR, so an empty declaration contradicted by a `TON`/`TONR`/`TOF` in the block should become a
refusal as soon as the gate can read the IR. Until then it is reported as a claim, never as a check.

> 🔴 ***`model.compStable` IS REQUIRED AT `runtimeCompression > 1`, NOT AT `comp_min > 1` — AND THE
> RUNNER TODAY KEYS ON THE WRONG ONE.*** The two are not the same number. `comp_min` is derived from
> `plantMs / budgetMs` and is `1` whenever the behaviour already fits its budget; `runtimeCompression`
> is what the wave is actually driven at, and it may legitimately exceed `comp_min` (the gate even
> prints a note when it does). **Measured on the built code: a wave at `runtimeCompression = 8` whose
> `comp_min` is 1 passes with no `comp_stable` declared at all** — the model is being asked to behave at
> 8x on nobody's authority, and the plan's model bound is filed *reported-but-not-gating* because
> `comp_min` said 1. That contradicts the runner's own recorded resolution that these ceilings key on
> the **runtime** factor. **The contract's rule is the runtime factor.** Named here rather than worked
> around: it is a harness defect, and until it is fixed a green on this bound at `comp_min = 1` is worth
> less than it looks.

**No timing constant appears above, deliberately.** The timer floor is `k x scan_period`; read `k` and
the scan period from **§12a derivation 5**, which is also where the correction lives that moved this
floor and cut a 500 ms preset's ceiling from the 10x X-D assumed to roughly 4.3x. *A contract that
restates it will one day refuse the wrong vectors with great confidence.*

---

### 2.4 ***EVERY ABSENT FIELD HAS A DECIDED TREATMENT, AND NONE OF THEM IS A DEFAULT***

> ***"0 FINDINGS" AND "NOBODY SAID" MUST NEVER PRODUCE THE SAME OUTPUT.***

This is the one table to read before omitting anything. Four treatments, and they are different facts:

| treatment | means | verdict effect |
|---|---|---|
| **REFUSED** | the check needs it and the vector is where it belongs. *A pass would say nothing and a fail would lie* | NOT ADMISSIBLE — **fix the vector** |
| ***NOT CHECKED*** | the input belongs to another artifact (the enumeration, the graph, the block) and a resubmission of the *vector* cannot supply it | NOT ADMISSIBLE — **fix that artifact**. Never a pass |
| **NOT DECLARED** | a bound nobody computed. ***It is not a ceiling of infinity*** | blocks whenever compression is applied |
| **reported, does not gate** | computed to be unable to bind, and printed anyway so an absent line never reads as a check that passed | admissible |

| what is absent | treatment | why that one |
|---|---|---|
| an expectation's `expected` (predicate) | **REFUSED** | the check needs it; a pass would say nothing and a fail would lie |
| `completionValue` / `completionSignal` | **REFUSED** | §2.2 — the same shape, one field over: an unstated value yields `TIMED-OUT` on a healthy block |
| `kills` | **REFUSED** | §10 requires mutation and this is the only mechanism for it |
| a vector's `assertionForm` (i.e. `Unstated`) | **REFUSED** | the vector had one field to fill and left it. *A dropped form fails the same comparison as a wrong one* |
| `enumeration.enumerator` | ***NOT CHECKED*** | a property of the **enumeration**; reporting it refused sends the author to edit the wrong artifact |
| `enumeration.forms` | ***NOT CHECKED*** | the flat projection simply carries no form to be the authority |
| a form for the **cited** assertion | **REFUSED** | the comparison cannot be made, and an uncomparable form is not a passing one |
| `computedConflicts` **and** `conflictEdges` both | ***NOT CHECKED*** | a blacklist compared against an absent graph is a blacklist nobody checked |
| provenance on a conflict edge | ***NOT CHECKED*** | *"0 multi-writer findings"* and *"nobody recorded why these conflict"* produce identical empty reports |
| `blockCompression`, at `runtimeCompression` > 1 | ***NOT CHECKED*** | three ceilings compared against nothing |
| `blockCompression`, at `runtimeCompression` = 1 | **CHECKED, a real pass** | nothing is scaled, so none of the three *can* bind — **computed from the submission, not assumed** |
| a preset's `source` | **REFUSED** | the two real answers push OPPOSITE ways; there is no fail-safe guess |
| `negligibleFraction` | **NOT DECLARED, never invented** | the spec works an example and never says where negligible ends |
| `model.compStable`, at `runtimeCompression` > 1 | **REFUSED** | the plan is asking a model to run at a rate nobody declared *(and see §2.3's 🔴 — the runner keys this on `comp_min` today, which is a hole)* |
| `model.compStable`, at `runtimeCompression` = 1 | **reported, does not gate** | nothing is scaled, so it cannot bind — computed, not assumed |
| a latched or stamped expectation's `windowScans` | **NOT DECLARED** | legal (exempt from the floor, §4.2) and it still yields no assertion ceiling |
| a **sampled** expectation's `windowScans` | **REFUSED** | undeclared is not exempt |

**One field is a genuine default and is named as such: none.** *(`presets: []` is a claim, not a
default; `runtimeCompression`, `slotsInWaveSet` and `resultRegistersPerSlot` default to 1 as the
smallest real wave, and each is reported in the gate's own output rather than assumed silently.)*

***A partially-supplied object is not a partially-checked one.*** `blockCompression` missing a
`plantMs` or a `budgetMs` is an **incomplete object**, and the whole of gate 10b is then `NOT CHECKED` —
not a plan computed from a zero. **The runner today reaches this by a different road** (a non-positive
`plantMs` throws, and the CLI reports `NOTHING EXAMINED`, exit 2, rather than `NOT ADMISSIBLE`, exit 1).
Both fail closed and neither is a pass, so the gap between them is between **two failing states** — but
the diagnostic is worse than it should be, and the fix is the document's own stated rule: *every field
nullable on the way in, so a missing one reaches the gate as MISSING rather than as a default.* Owed by
the harness.

---

## 3. `Basis` — the clause AND the assertion, and why both

> **A vector that cannot cite its specification clause is not admissible into a wave** (D6). **A
> vector that cites only a clause is admissible and nearly worthless.**

**The reason is the autopsy this whole pipeline was rebuilt around.** An AI reviewer reading the same
register as the AI coder is a **correlated check**, and both failed together on an ambiguous `/`
(`docs/evidence/PlantAutoControl-bench-autopsy.md`). Using a different *agent* does not decorrelate them if
both agents read the same sentence and are free to read it the same way.

- **The clause** says *where the requirement came from*. It is what makes a failure traceable to a
  source rather than argued from the code.
- **The assertion** says *what would be observed if the block were correct*. **This is the
  decorrelating half**: a clause citation lets an ambiguous reading be silently reused, while naming
  the assertion forces the vector to commit to an observable consequence — at which point two
  different readings of the same clause produce two *visibly different* assertions, instead of two
  green results.

**The assertion must be an ID drawn from the spec-derived enumeration** (§7's rule, D35's counting
unit). Never free text. §7 is explicit that a denominator defined by what somebody wrote a vector for
is always 100%; the same trap applies here — an author who *writes* an assertion rather than *citing*
one has re-created the correlated check with extra steps.

> ✅ **The enumeration is now defined — `docs/notes/assertion-enumeration.md` (2026-08-13).** When
> this contract was drafted it required a citation into an enumeration that did not exist, which
> would have left the skill able to check only that *something* was written in the field. An
> assertion is **the smallest statement about observable behaviour that can be falsified on its
> own**, written as `WHEN … THEN … [WITHIN …]` or `NEVER …`; IDs are `REQ-014:3f9a1c` — clause ID
> plus a content hash, **nothing positional**, so inserting a clause or an assertion shifts no
> existing citation. **A citation in the display-ordinal form (`REQ-014.A2`) is rejected**, and one
> naming an ID the enumeration does not contain is a dangling-citation error — *an error in the
> vector, never an extension of the denominator*.
>
> **One further check that document adds and this one should carry:** the cited assertion's
> **response signal must appear in this vector's `Expectations`** — a set-difference, the same shape
> as `trace`'s guard-containment hop. It does not prove the vector tests the assertion, but it
> catches a citation that could not possibly be testing it.

**Mechanically checkable:** that the clause resolves to something written, and that the assertion ID
exists in the enumeration. **Not mechanically checkable:** that the assertion is a faithful reading of
the clause. That is what the independent author is for, and it is why element 2 is marked ⚠️.

---

## 4. The observability declaration — *the one with teeth*

> ***THE RUNNER REFUSES A VECTOR WHOSE OBSERVABILITY THE TRANSPORT CANNOT SUPPORT. The alternative is
> a green result that means nothing, and a green that cannot mean anything is worse than a refusal.***

This is the element that makes the rest worth having. **It is also the only one where the author's
convenience and the result's meaning are in direct conflict**, which is why it is a refusal and not a
warning ("a warning is not a gate").

### 4.1 The floor is physical, and it is not negotiable by polling harder

**A poll IS one round trip.** There is no "poll rate" to turn up: the period is set by the wire and by
how many slots share a read (§12a derivation 2, as amended by F-1). Consequently:

- **A one-scan event is unobservable at any polling rate.** Not "hard to catch" — *structurally
  invisible*, and no protocol choice changes it.
- **A same-scan coincidence is unobservable by sampling at all.**

***Read the current floor from §12a derivation 1. This document deliberately does not carry the
number*** — it has already moved twice, and a contract that restates it will one day refuse the wrong
vectors with great confidence.

### 4.2 The three modes, and what each can answer

| mode | answers | cost | when the floor applies |
|---|---|---|---|
| **LATCHED** | *did it happen?* | cheap; default for coil-shaped signals | **exempt** — a latch holds until cleared at test start, so no window has to be caught |
| **SAMPLED** | *was it true when we looked?* | free | **fully** — the declared window must exceed the floor |
| **STAMPED** | *when, relative to T=0?* | ~32x the memory of a latch | **exempt for occurrence**, applies to the *resolution* of the answer |

***PREFER LATCHED. IT IS NOT MERELY CHEAPER — IT IS THE ONLY MODE IMMUNE TO THE TAIL.*** §12a
derivation 1 measures a small but real fraction of poll gaps that are enormous; a sampled assertion
falling in one is **a silent wrong answer, not an error**. A latch cannot fall in a gap. (Whether
sampled assertions should be admissible *at all* is §12a flag F-3, open with the owner. Until it is
ruled, they are admissible **with a declared window**.)

### 4.3 What must be declared, per expectation

```
Observability
  Mode        Latched | Sampled | Stamped
  Window      for Sampled: how long the condition is expected to HOLD, in scans
  Signal      the tag, which must appear in the map's observability declarations
```

***AND THE DECLARATION MUST EXIST BEFORE THE DOWNLOAD THAT GENERATES THE COPY LAYER*** (D15 + D31).
The copy layer's latches and scan-stamps are *generated* from these declarations, and the set is
**frozen for the wave set**. This is the single most common way an author will be surprised — see §9.1.

### 4.4 Scan counts are meaningless without the compression factor

**Not in §2.6, and an author will trip over it.** A window declared in scans is only valid at the
compression factor it was computed at. Under X-D, presets that are *data* scale with `comp`; a
behaviour occupying 20 scans at `comp = 1` occupies 2 at `comp = 10` — **crossing the floor without
anybody editing the vector.**

> ***SO A VECTOR DECLARES THE `comp` ITS SCAN COUNTS ARE STATED AT, AND THE RUNNER RE-CHECKS THE
> FLOOR AT THE `comp` ACTUALLY USED.*** Mechanically checkable, and cheap. Without it the
> observability check is sound at authoring time and silently void at run time.

---

## 5. Settling — ***a completion flag is NOT a settling signal***

**This is phase 2's finding and it is the sharpest thing in this contract.** The deliberately
defective build **raised `Done` at 10 and then went on ramping to 15**.

> ***A FASTER-THAN-FLOOR POLL READS MID-RAMP AND REPORTS A WRONG ANSWER, NOT AN ERROR.***

Sit with what that means. It is not a missed observation — the harness observes a value, believes it,
compares it against the expectation, and returns a **confidently wrong verdict**. It is the worst
outcome available in this system, and it is *exactly* the failure the observability floor otherwise
conceals: below the floor you get nothing, but at the boundary you get something plausible.

**So the contract requires a settling declaration distinct from the completion condition:**

```
Settling      -- PER VECTOR (`settlingCondition` + `settlingSignals`), not per expectation
  Condition   what makes the observed value FINAL
              e.g. "value unchanged across N consecutive scans"
                   "the block's own <tag> has fallen"
                   "a stated number of scans after T=0"
  NotDoneFlag the completion flag alone is REFUSED as a settling condition
```

***AN AUTHOR MUST DECLARE WHEN A VALUE HAS SETTLED, NOT MERELY WHEN THE BLOCK SAYS IT IS DONE.***
The block's own opinion of its progress is a claim under test, not evidence about the observation.

**Mechanically checkable, both halves — and the second is the useful one:**
1. that a `Settling` declaration exists at all; and
2. **that it is not simply the completion flag re-cited.** If the settling condition names the same
   tag as the block's done-signal and nothing further, it is refused. *This exact defect exists as a
   test in phase 2, so a future change that reintroduces it fails rather than passes.*

**Not mechanically checkable:** that the declared condition really does imply the value is final. That
is judgement, informed by the model's fidelity declaration (M3/M4).

### 5.1 ✅ RULED 2026-08-13 — keep the caller-supplied model, with `NotEstablished` as the third value

**Recorded as a decision so it is not later re-opened as an oversight.** The settling condition is
**supplied by the caller**, and where settling cannot be determined the result carries
**`NotEstablished`** rather than a value — a third outcome beside settled and not-settled.

**The owner's reason, in the owner's words: *"honesty is the best policy."*** A runner that guessed
at settling would be inventing the one fact the reader most needs, and it would do so invisibly.
`NotEstablished` says *nobody knows whether this value was final*, which is a usable statement; a
fabricated settled-value is not.

> 🔴 ***AND THE RECORDED GAP STAYS VISIBLE, BECAUSE THE DECISION DOES NOT CLOSE IT: `WaveRun`
> OBSERVES COMPLETION, NOT SETTLING.*** Phase 2 measured what that costs — a completion flag raised
> at 10 while the value ramps on to 15, so **a fast poll reports a wrong answer, not an error**.
> Keeping the caller-supplied model means the *contract* asks for a settling condition while the
> *runner* still watches completion, and until those meet, the settling declaration is a promise the
> harness does not yet enforce. **Recorded here rather than in a backlog, because a gap inside a
> ruling is the kind that gets read as closed.**

---

## 6. Start-bool binding

The start bool is the **commit** (X-A) and its rising edge is **T=0** (D37). Everything a result says
about "when" is a difference from that edge.

```
StartBool
  one per slot, and exactly one
  bound by NAME, never by bit position
  raised in ONE transaction with every other slot's start bool  (X-A: the commit)
  raised on a LATER SCAN than the inert-establish, never the same one  (D33/D37)
```

**Mechanically checkable in full**, and it is: exactly-one-per-slot, the later-scan rule enforced
against the *observed* scan counter rather than assumed, and T=0 recorded rather than inferred.

> ⚠️ **The bit order *within* the start-bool register is still `[I]`, not `[M]`** — and the build
> plan records that the simulator and `BitAddressOf` agree *from the same premise*, so their agreement
> is worth nothing. **Bind by name.** An author who reasons about bit positions is relying on an
> unverified premise, and this is the contract's one live "do not do that" on the write side.

---

## 7. The blacklist

Names blocks that must **not** run concurrently with this test.

> ***A BLACKLIST MAY ONLY EVER ADD EXCLUSIONS, NEVER REMOVE THEM.*** Computed disjointness (D9) is
> the floor; the blacklist sits on top for coupling the reference graph cannot see — plant-model
> state being the obvious case. **An agent must never be able to declare itself compatible with
> something the graph says it conflicts with.**

**Every entry carries a reason.** Not bureaucracy: the failure mode here is **defensive
over-blacklisting** — agents excluding everything whenever a test looks flaky, concurrency collapsing
toward serial, and nobody noticing *because it still works*. A recorded reason makes that visible; a
blacklist density metric makes it measurable. **Neither makes it preventable**, which is why element 7
is half ⚠️.

**One thing §2.6 does not say and an author will get wrong:** the blacklist names **blocks**, but
admission colours **slots** (DB-13 as corrected by D26a). Naming a block excludes *every slot testing
it*, which is usually what was meant and occasionally much wider than intended.

---

## 8. How to read a result — ***including what it does not mean***

A result is DB-8's package: observed vs expected **per assertion**, the basis citation, the fidelity
declaration of every model involved, the stimulus check, the co-running log slice, the validity stamp,
and manifest presence.

### 8.1 The verdict vocabulary, and the three that are not failures

| verdict | means | does **not** mean |
|---|---|---|
| **PASS** | every assertion in this vector was observed as expected | the block is correct — only that *these* assertions held, under *this* model, at *this* fidelity |
| **FAIL** | an assertion was observed and disagreed | the vector is right. **Fix the block against the SPECIFICATION, not against the vector** (§2.4) |
| **TIMED-OUT** | the condition never occurred within `MaxDuration` | the wrong thing happened. X-B keeps these distinct precisely because an agent told only FAIL will go and fix the wrong thing |
| **UNSETTLED** | the value never met its settling condition | the value was wrong — **nothing was legitimately read at all** |
| ***STALE*** | ***the experiment never ran*** | anything whatsoever about the block |
| **REFUSED** | the vector was inadmissible (observability, fidelity, authorship, basis) | a defect in the block |

### 8.2 ***`Stale` must be unmistakable, and here is why it is hard***

> ***"EVERY REGISTER AGREES" IS ALSO WHAT A MIRROR NO WRITE EVER REACHED LOOKS LIKE.***

A frozen mirror is *perfectly self-consistent*. It passes every coherence check, every comparison
between registers, every "did the values agree" test — because nothing ever changed them. **Phase 1
walked into exactly this and the client was built to refuse it**: a `Stale` verdict when the change
counter did not advance, reported as a verdict and not as an exit code, with the standing instruction
***read the verdict, not the exit code***.

**So a result is only readable if its liveness is established independently of its content:**

- the **stimulus check** (DB-8) — evidence the input arrived and the block ran;
- a **change/scan counter that advanced by the expected amount**, not merely "moved";
- **manifest presence** — was the object under test even in the download? This separates *wrong* from
  *never loaded*, and it is cheap.

***AN ABSENCE OF DISAGREEMENT IS NOT A RESULT.*** Zero torn reads against a frozen block is evidence
of nothing, and phase 1's client refused to say otherwise. This contract inherits that: **empty is not
clean.**

### 8.3 What a green never licenses

- **It does not generalise past the model's fidelity declaration.** A pass against a model declared
  approximate is a different fact from one against a model declared exact (M3), and the declaration
  travels with every result so a green is never read without its caveats.
- **It does not survive its validity stamp.** Program version and map version are part of the result
  (DB-2); a green obtained before the change that invalidated it is not a green.
- **It does not mean the co-running slice was benign** — only that nothing detected interference.

---

## 9. What §2.6 leaves ambiguous — the things an author will trip over

Raised here rather than resolved; three of them need the owner.

**9.1 — There is no route from "unobservable" back to "testable". ✅ RULED 2026-08-13: THE AUTHOR MAY
CHANGE THE INTERFACE — WITH A LIMIT, AND THE LIMIT IS THE SUBSTANCE.**

The question was: §2.6 says the runner refuses the vector but not what the author then *does*. The
declaration is frozen for the wave set and must precede the generating download, so an author who
discovers mid-wave that they need a latch waits a **full download boundary**. The obvious fix — add
a status output so the behaviour is visible — collided with D13/§2.1.

> ***THE RULING: AN AUTHOR MAY CHANGE A BLOCK'S INTERFACE PURELY TO MAKE IT TESTABLE.***
>
> ***AND THE OWNER'S QUALIFICATION, WHICH IS THE HALF THAT MATTERS: THERE MUST BE OCCASIONS WHERE
> CHANGING THE INTERFACE WOULD INVALIDATE THE TESTING — AND THE JUDGEMENT IS THAT IT IS NOT WORTH
> THE RISK.***

**Both halves are recorded because a permission without its counter-case becomes automatic.** The
counter-case is real and it is not hypothetical: an interface change alters what the block *is*, so
past that point the thing being tested is not the thing that was specified. Concretely — a member
added to expose an internal value changes the block's memory layout and its instance DB; a member
that is *read* by anything shifts the block from observed to instrumented; and a block whose
interface was reshaped to suit its test has had its test participate in its design, which is the
correlated check this whole pipeline exists to prevent, arriving by a new door.

***SO THE PERMISSION IS FOR THE CASE WHERE THE EXPOSURE IS FREE, AND THE JUDGEMENT IS THE AUTHOR'S
TO LOSE.*** A proposed convention making it a requirement *where possible* is drafted, unnumbered, in
`docs/06-lad-conventions.md` under **Proposed rules** — including the reason "where possible" is hard
to write without making the rule unenforceable.

**9.1a — Reconciling the ruling against D13 and §2.1, and one thing that does NOT reconcile.**

D13 says instrumentation is a property of the **copy layer**; §2.1 says `lad-coder` **never writes
observability code** and never sees a register number. The ruling does not overturn either — but it
does not sit inside them unchanged either, so here is the scoping, with the unresolved part named
rather than smoothed.

| | status |
|---|---|
| **§2.1's "never sees a register number"** | ✅ **Untouched.** An exposed member is a **tag**, not a register. The map still assigns registers, the coordinator still generates the mirror, and the author still speaks tag names (D8). |
| **D13's "instrumentation lives in the copy layer"** | ⚠️ **Needs scoping, and here is the line proposed:** the copy layer still owns **latches, scan-stamps and the mirror** — everything that *observes*. What the ruling permits is an author **exposing a value that already exists inside the block**, so there is something for the copy layer to observe. *Exposing is not instrumenting.* |
| **§2.1's "never writes observability code"** | ⚠️ **Needs amending, narrowly.** As written it forbids the ruling. Proposed amendment: `lad-coder` writes no **observation logic** — no latching, no stamping, no sampling, no register handling — but **may add a read-only interface member whose only purpose is to make an existing internal value reachable.** |

> 🔴 ***WHAT I COULD NOT RECONCILE, STATED AS UNRESOLVED RATHER THAN PAPERED OVER: "expose an
> existing value" AND "add a value that does not exist yet" ARE DIFFERENT ACTS, AND THE RULING DOES
> NOT DISTINGUISH THEM.*** Surfacing a Static that the logic already computes is nearly free and
> clearly inside the permission. But a requirement whose evidence is a *one-scan coincidence* often
> has **no existing internal value at all** — making it observable means computing something new,
> which is new logic, which is squarely what §2.1 forbids and is also the case where the owner's
> counter-case bites hardest. **That is precisely the case §2.6 raised in the first place**, so the
> ruling as stated may not reach the motivating example. ***OWNER'S — and worth asking before the
> proposed convention is numbered, because the convention inherits the same ambiguity.***

**9.2 — "Assertion" is not defined in §2.6. ✅ RESOLVED 2026-08-13.** D35 counts per assertion and §7
requires the enumeration be spec-derived, but §2.6 predates both, and *neither said what an assertion
IS* — so an author reading only §2.6 writes prose. Definition, decomposition rules, ID scheme and
bucket enforcement now live in `docs/notes/assertion-enumeration.md`. **Three things it leaves open,
and the third should be ruled before the skill is built: whether STARTUP is a fifth bucket or a
scheduling attribute; where a decomposition dispute is recorded; and *who performs the enumeration* —
because if it is the block's author, D6's independence is lost at the denominator.**

**9.3 — Scan counts have no compression factor attached** (§4.4). Mechanically fixable, and this
document proposes the fix rather than assuming it: declare the `comp`, re-check the floor at run time.

**9.4 — The copy layer lags results by one scan.** It runs *before* the block (phase 2's finding).
That is invisible beneath the sampling floor and therefore harmless for latched and sampled modes —
but a **stamped** assertion resolves to a single scan, so a stamp difference of 1 sits inside an
unspecified off-by-one. Nothing in §2.6 or §12 says whether a stamp is "the scan the copy layer
observed it" or "the scan the block did it". **Needs stating before anyone asserts on a stamp
difference of 1 or 2.**

**9.5 — `Stale`, `Absent`, `Unsettled` and `WordOrderSuspect` are the *version register's* vocabulary**
(phase 2.6). This contract reuses `Stale` and `Unsettled` for **test results**, which is right — the
distinction is the same one — but the two vocabularies have never been reconciled in one place, and an
author meeting `Unsettled` in a result will look for it in §9 and not find it.

**9.6 — The blacklist names blocks; admission colours slots** (§7 above). Minor, and purely a matter
of saying so.

---

## 10. What the skill would have to enforce

**Not written here, by instruction.** Its enforcement surface, in the order a submission meets it:

| gate | check | on failure |
|---|---|---|
| **schema** | all §2 fields present and typed; `MaxDuration` non-empty | reject, naming the field |
| **authorship** | vector author ≠ block author (D6) | ***refuse.*** Not a warning — this is the correlated check the pipeline exists to prevent |
| **basis** | clause resolves to written text; assertion ID is in the spec-derived enumeration | reject, naming which half failed |
| **fidelity** | every asserted behaviour ∈ the model's fidelity declaration (M4) | reject with both sets shown |
| **observability** | mode valid; signal in the map; window ≥ §12a's floor **at the run-time `comp`**; declaration predates the generating download | ***refuse the vector*** — §2.6's own rule |
| **settling** | declaration exists; **is not the completion flag alone** | reject, citing phase 2's `Done`-at-10-ramps-to-15 |
| **start bool** | exactly one per slot; bound by name; later-scan rule against the observed counter | reject |
| **blacklist** | add-only against computed disjointness; every entry has a reason | reject the *entry*, not the vector |
| **8c multi-writer provenance** (X-G) | every conflict edge records WHY the two blocks conflict, on which signal, and whether that signal is part of the deliverable | ***the FINDING is reported, not refused*** — the defect is in the deliverable, not in the submission. **An UNPROVENANCED graph is `NOT CHECKED`** and fails closed |
| **10a assertion ceiling** (X-D) | the run-time `comp` is under every vector's own `T_event / scan_period` ceiling | reject, naming the binding signal and the ceiling. **Catches what gate 5 structurally cannot: LATCHED is exempt from the observability floor, never from the scan-period term** |
| **10b timer / model / ratio ceilings** (X-D) | at `runtimeCompression` > 1, the block's presets, the model's `comp_stable` and the ratio-distortion threshold (§2.3) | reject with `comp_min` **and** `comp_max` shown. Absent inputs are `NOT CHECKED`, never a pass. At `comp` = 1 a real computed pass |
| **liveness** *(post-run)* | stimulus check present; counter advanced by the expected amount; manifest presence | verdict `STALE`, never `PASS` |

**Two properties of this table, both learned by getting them wrong.** *(a)* **The gate that reports on
clean input too.** 8c prints its finding line on a clean graph as well, for the same reason F-6's
collapse report prints its no-collapse line: *a report that appears only on bad news teaches its reader
that absence means "not run".* *(b)* **10a and 10b are not one gate split for tidiness.** 10a is a
property of the **vectors** and a submission can always answer it; 10b is a property of the **block and
the model**, and §2.3 exists because a submission could not answer it at all.

**Absent inputs: §2.4 is the single table**, and no gate above may invent a treatment that is not in it.

**Two properties the skill must have, both learned the hard way here:**

1. ***EVERY GATE FAILS CLOSED, AND NONE OF THEM HAS A RELAXING FLAG.*** The precedent is phase 2's
   `AddressesExamined`: a rule that was correct and *nothing established that it had run*. A gate with
   a skip flag is a gate that will be skipped at 2 a.m.
2. ***EMPTY IS NOT CLEAN*** (FI-44). A submission that reaches a gate with nothing to check must
   report *nothing examined* and fail, never pass. Zero vectors, zero assertions, an empty
   enumeration, a blacklist compared against an absent graph — each of those is a refusal.

**And the way to test the skill is mutation, not example.** Phase 2 found one of its own tests could
not fail — a 3-slot map whose start bools fit in one register made a per-register loop and a single
transaction indistinguishable. **A test that cannot fail has proven nothing**, and only mutation found
it.
