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
  inputs              tag name -> value    -- written before T=0; range-checked against the
                                              signal's MIRROR ELEMENT, never against a range
                                              stated here (§2.6)
  startBool           the slot's start bool        -- §6
  expectations        [ { signal, nature, mode, windowScans, expected } ]   -- §4; `expected` is §2.2
  settlingCondition   what makes the observed value FINAL   -- §5. PER VECTOR, not per expectation
  settlingSignals     [ tag ]                               -- §5
  maxDurationScans    in SCANS, with a wall-clock backstop (X-B, §12a derivation 4)
  completionSignal    the block's own done-signal              -- §2.2
  completionValue     the value on it that means "finished"    -- §2.2; range from §2.6
  blacklist           [ { block, reason } ]        -- §7, add-only
  compressionFactor   the `comp` this vector's scan counts are stated at  -- §4.4
  assertedBehaviours  [ behaviour ]   -- set-differenced against the model's `represents` (M4)
  kills               the wrong implementation this vector would catch    -- §2.2
  boundsUsed          bound name -> the value THIS VECTOR WAS WRITTEN AGAINST   -- §2.5, AMB-19

Submission                    -- the top-level object the vectors are submitted inside
  blockAuthor         agent identity, for D6
  runtimeCompression  ***the `comp` THE WAVE WILL ACTUALLY RUN AT***  -- §2.3. Not any vector's
  slotsInWaveSet          \__ feed §12a derivation 1's floor, which scales with tensor width
  resultRegistersPerSlot  /
  model               { id, represents, doesNotRepresent, validatedAgainstPlantData, compStable }
  enumeration         { clauses, assertions, forms, enumerator,      -- §3
                        normalisedTexts, requiredObservations, bounds }   -- §2.5 and §10's 3g/3h/3i
  map                 { providedFor: signal -> [ modes ],          -- §4.3, HOW it is watched
                        storage:     signal -> { owner?, path },   -- §2.7, WHERE it lives
                        harnessOnly: [ signal ] }                  -- §2.7, occupies no PLC storage
  computedConflicts   [ block ]  -- bare names, NO provenance. *** NEVER EMITTED *** — §2.7
  conflictEdges       [ { blockA, blockB, provenance, signal, class } ]   -- X-G, provenanced.
                      *** OMITTED, never [] and never null, when the graph did not run *** — §2.7
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
completionValue    integer. REQUIRED; absent is a REFUSAL.
                   *** THERE IS DELIBERATELY NO DEFAULT OF 1. ***
                   Its admissible RANGE is not stated here: it comes from the completion
                   signal's own row in the mirror element table -- §2.6.
```

> 🔴 ***CORRECTION, 2026-08-13: THIS SECTION SAID `integer 0..65535 (it is compared against ONE result
> register)`, AND THAT WAS WRONG TWICE.*** It is recorded rather than quietly replaced, because the
> wrong version was acted on. **(a) The range was UNSIGNED where the element is SIGNED** — a
> single-register element holds −32768..32767, so `40000` never fit it and `0..65535` admitted values
> that could not arrive. **(b) It assumed ONE register**, which made a 32-bit completion signal
> *inexpressible rather than mis-expressed*. **The framing was the real mistake**: a completion signal
> is not a special kind of thing, it is *just another mirrored element*, and giving it a private range
> in this document gave it **a second notion of width that could disagree with the mirror's**. §2.6 has
> the one notion, in one place.

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
| any `inputs` value, or `completionValue`, **out of its element's range** | **REFUSED BY NAME, never a modulo** | §2.6. The refusal prints *what the value would have become* — `75000` arrives as `9464`, a plausible dwell nobody would question, and every boundary keyed on it fires early |
| an `inputs` value that is present but **empty** | **REFUSED** | ***zero is a value a block could legitimately be driven with***, so supplying one invents the stimulus |
| a signal the vector says **nothing about** | **not checked here — legitimate** | leaving an input undriven is the *inert declaration's* business, not the width rule's. Absent ≠ present-and-empty |
| a mirrored signal's **element type** | **REFUSED** | the table's zero value is `Unstated`; a value cannot be range-checked against a type with no width |
| a **completion signal wider than one register** | **REFUSED as INEXPRESSIBLE** | §2.6 — comparing it would test the high half and report `TIMED-OUT` forever on a block that finished, and ***a spurious `TIMED-OUT` is worse than a spurious `FAIL` because it is believed*** |
| `kills` | **REFUSED** | §10 requires mutation and this is the only mechanism for it |
| a vector's `assertionForm` (i.e. `Unstated`) | **REFUSED** | the vector had one field to fill and left it. *A dropped form fails the same comparison as a wrong one* |
| `enumeration.enumerator` | ***NOT CHECKED*** | a property of the **enumeration**; reporting it refused sends the author to edit the wrong artifact |
| `enumeration.forms` | ***NOT CHECKED*** | the flat projection simply carries no form to be the authority |
| a form for the **cited** assertion | **REFUSED** | the comparison cannot be made, and an uncomparable form is not a passing one |
| `computedConflicts` **and** `conflictEdges` both | ***NOT CHECKED*** | a blacklist compared against an absent graph is a blacklist nobody checked |
| provenance on a conflict edge | ***NOT CHECKED*** | *"0 multi-writer findings"* and *"nobody recorded why these conflict"* produce identical empty reports |
| `conflictEdges` present as `null` | ***REFUSED*** | §2.7. **Not the same as omitted**: a lenient deserializer turns a null back into an empty collection one layer down, restoring the false *"ran and found nothing"* claim after the refusal was correctly made |
| a signal in neither `map.storage` nor `map.harnessOnly` | ***NOT CHECKED*** | §2.7. The join from a logical name to PLC storage was never stated, so no conflict could be looked for. ***Measured: 16 of 17 signals in the deliverable submission*** |
| a signal in **both** | **REFUSED**, naming it | a contradiction: it cannot both occupy storage and occupy none |
| a `storage` entry's `owner` | **not an omission — a POSITIVE claim** | the path is **global** (DB member, PLC tag, `iDB_…`, physical address) and is already unique |
| a declared join that still resolves to **more than one** storage | **REFUSED, naming EVERY candidate** | ***never resolved to one.*** Instance aliases are collapsed first, so this means genuinely different storage — and picking a candidate is the aliasing that manufactured fictional multi-writers |
| a conflict edge's **signal class** | ***NOT CHECKED*** (carried through as `Unstated`) | it is **derived from the writing blocks, never declared** — there is no field for it, and an unclassifiable signal fails the gate closed rather than being guessed |
| a mirrored signal's `specName` | ***NOT CHECKED***, naming the tag | §2.8. A property of the **binding**, so resubmitting the vector cannot supply it. ***It does NOT mean "same as `tag`"*** — that identity **is** the assumption being removed, and as a default it would match by accident where the names coincide and miss everywhere else. **If the two names genuinely are the same, STATE that they are** |
| a signal's **instrumentation mode** | **CHECKED — DERIVED, a real pass** | there is no field for it. The mode is read off what the copy layer generates, which emits no per-signal latch, so `Sampled` is the derived answer and a `Latched` expectation on such a signal is refused |
| `latchedBy` | **the binding claims NO latch** — a derivation, not a default | the generator emits no latch, so an unnamed latcher is the *true* state. It fails closed: `Sampled` stands and any `Latched` expectation is refused |
| **verification that a named `latchedBy` block is deployed** | ***never performed — the gate takes the NAME, not the FACT*** | §2.8. Stated in this document so it survives independently of any rendering path, after an admission rendered **only on the no-problems branch** let four claims through unremarked |
| ***an UNKNOWN field, anywhere in a submission*** | ***REFUSED, NAMING IT*** | see below — the complement of this whole table |
| `blockCompression`, at `runtimeCompression` > 1 | ***NOT CHECKED*** | three ceilings compared against nothing |
| `blockCompression`, at `runtimeCompression` = 1 | **CHECKED, a real pass** | nothing is scaled, so none of the three *can* bind — **computed from the submission, not assumed** |
| a preset's `source` | **REFUSED** | the two real answers push OPPOSITE ways; there is no fail-safe guess |
| `negligibleFraction` | **NOT DECLARED, never invented** | the spec works an example and never says where negligible ends |
| `model.compStable`, at `runtimeCompression` > 1 | **REFUSED** | the plan is asking a model to run at a rate nobody declared *(and see §2.3's 🔴 — the runner keys this on `comp_min` today, which is a hole)* |
| `model.compStable`, at `runtimeCompression` = 1 | **reported, does not gate** | nothing is scaled, so it cannot bind — computed, not assumed |
| a latched or stamped expectation's `windowScans` | **NOT DECLARED** | legal (exempt from the floor, §4.2) and it still yields no assertion ceiling |
| a **sampled** expectation's `windowScans` | **REFUSED** | undeclared is not exempt |
| `deployment` (§4.5) | ***NOT CHECKED*** | a property of the **download**; resubmitting the vector cannot supply it. Absent ≠ `s7Objects: []` |
| `deployment.s7Objects` = `[]` | **CHECKED — a claim** | *no classic-S7comm path reaches a data block*, which is the normal mirror-only state |
| an `s7Objects` row's `layout` | **REFUSED** | absence means "no opinion", and TIA resolves no opinion to **Optimized** — which is invisible on the wire. Undetermined is not clean |
| `layoutSetAfterImport`, or one naming an older `importStamp` | **REFUSED** | the layout reverts at **every** import; a stale stamp says it was re-asserted and has reverted since |
| an `s7Objects` row naming a **deliverable** block | **REFUSED** | the invariant itself, §4.5. Not a finding — the harness touches harness-generated objects only |
| a vector's `boundsUsed` (§2.5) | ***NOT CHECKED*** | ***this is the AMB-19 hole itself, not a formality*** — a vector that records no bound **can never be found stale by anything**, so it survives a retune with every gate green. It keeps this status even beside a provably stale sibling |
| `enumeration.bounds` | ***NOT CHECKED*** | a property of the **enumeration**; an absent table is not an agreeing one |
| a declared bound that **differs** from the table | **REFUSED, reported as `STALE`** | ***never `FAIL`*** — the block may be perfectly correct and the vector predates a retune. §2.5 |
| a declared bound the table does **not contain** | **REFUSED, reported as `Unknown`** | a disagreement about which bounds *exist*, whose repair precedes any question about a value — so it outranks `Stale` |
| `enumeration.normalisedTexts` | ***NOT CHECKED*** | the stamper's output is then taken on trust, and **the omission is exactly what a compromised stamper would emit** |
| `enumeration.requiredObservations` | ***NOT CHECKED*** | a relational assertion's second signal goes unobserved and the relation is untested while everything reports green (AMB-14) |

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

#### 🔴 AND THE COMPLEMENT: ***AN UNKNOWN FIELD IS A REFUSAL NAMING IT, NEVER SILENTLY IGNORED***

Everything above decides what an **absent** field means. This decides what an **unrecognised** one does,
and it is the rule that makes a divergence between this document and the runner harmless in either
direction.

> *** A SILENTLY-IGNORED FIELD IS WORSE THAN A REJECTED ONE, BECAUSE IT READS AS ACCEPTED. *** An author
> who writes a field this contract specifies and the runner has not yet implemented gets a green,
> believes the thing was checked, and has in fact submitted a document with a hole in exactly the place
> they were most careful.

**This is not hypothetical and it is not rare.** It has happened in one direction or the other **every
single time these two artifacts have been compared** — the contract ahead of the code, the code ahead of
the contract, a gate table short by three rows, then by four, a field specified with three keys and built
with one. **The refusal is what makes staleness announce itself instead of accumulating.**

- **A field the runner does not recognise is REFUSED, and the refusal NAMES the field**, so the author
  can tell "not implemented yet" from "misspelled".
- It applies **anywhere in the document** — a vector, an expectation, the map, a binding row, the
  enumeration, the deployment.
- ***It has no relaxing flag.*** The precedent is every other gate here: a skip flag is a flag that gets
  used at 2 a.m.

---

### 2.5 `boundsUsed` — ***WHICH NUMBER THIS VECTOR WAS WRITTEN AGAINST.*** ADDED 2026-08-13 (AMB-19)

**REQUIRED FOR ADMISSIBILITY.** A vector that omits it is not admitted.

> ***THE HOLE THIS CLOSES IS MADE ENTIRELY OUT OF CORRECT DECISIONS, WHICH IS WHY IT SURVIVED.***

Two rulings, each right on its own:

1. **No hashed assertion text contains a numeric bound.** That is what makes a re-issue of an
   enumeration cost zero re-hashes when an output is renamed, and it is what lets a clause refer to a
   bounds **table** rather than to a value.
2. **Retuning the table therefore re-hashes nothing.** Also correct — no assertion's *text* changed.

Put them together:

> *** A RETUNE CHANGES THE TRUTH CONDITIONS OF EVERY ASSERTION THAT REFERS TO THE TABLE WHILE MOVING
> ZERO ASSERTION IDs. *** On the hopper enumeration that is 22 of 27. **A vector written against
> `T#60S` goes on passing after the bound becomes `T#90S`** — no citation dangles, nothing recomputes
> wrong, and no `Stale` fires, because staleness keys on assertion IDs and not on bound values. *The
> vector tests the wrong number and every mechanical check agrees it is fine.*

**The fix is one comparison, and it lives outside the register:** the vector states which bound it was
written against, and submission compares that against the table.

```
Vector
  boundsUsed        bound name -> the value THIS VECTOR WAS WRITTEN AGAINST
                    e.g. { "persistence_threshold": "T#60S" }

enumeration
  bounds            bound name -> the value the specification CURRENTLY STATES
                    e.g. { "persistence_threshold": "T#60S" }
```

***Carry the BARE VALUE on both sides.*** The enumeration's YAML writes provenance prose beside the
number — `"T#60S  (Q-HBA-01, owner) — the SPECIFIED value"` — and these fields want `"T#60S"`. A
mis-transcription then surfaces as a **loud STALE naming both strings**, never as a silent pass.

#### 🔴 A mismatch is `STALE`, and it is NEVER `FAIL`

**A `FAIL` says the block disagreed with the specification.** Here the block may be perfectly correct
and the vector simply predates a retune nobody told it about. ***Reporting that as a disagreement would
send an agent to edit correct logic*** — which is the missing-predicate defect (§2.2) exactly one field
over: a fact nobody supplied, surfacing as a verdict about the block.

The refusal ends ***"Do NOT edit the block on the strength of this finding"***, and a test asserts that
sentence. **It is load-bearing text, not decoration.**

#### It outranks BOTH its neighbours in the result package, and each precedence is separately argued

| it comes before | because |
|---|---|
| **admissibility** (`Refused`) | `Refused` reads as ***the author broke a rule***, and this author broke none — the number moved underneath them. And there is no useful sense in which a well-formed test of the wrong number is "admissible" |
| **content** (`Fail`/`Pass`) | ***a retuned bound is `Stale` even when an assertion disagreed***, because a disagreement measured against the wrong number is not evidence of anything |

#### Two roads to `Stale`, and they need two different instructions

| road | it is a … | what to do |
|---|---|---|
| a frozen mirror / no stimulus | ***RIG*** problem | the experiment never ran; nothing here says anything about the block |
| an expired premise (`boundsUsed`) | ***VECTOR*** problem | re-read the vector against the current table and re-submit. **Do not touch the block** |

**They are not collapsed into one sentence**, deliberately: telling somebody *"the experiment never ran"*
when it was the premise that expired sends them to the wrong half of the system entirely.

#### The comparison rule is ASYMMETRIC ON PURPOSE — say why, or somebody will "fix" it

> ***LAX WHERE LAXITY FAILS CLOSED. STRICT WHERE LAXITY WOULD FAIL OPEN.***

- **A bound's NAME is compared case-insensitively.** Getting a name wrong yields a **refusal**, so a
  lenient match there can only ever turn a refusal into a real comparison. Laxity fails closed.
- **A bound's VALUE is compared ordinally, case preserved.** A lenient match here would turn a real
  difference into a ***PASS*** — the one direction this check may never move in.

***And it is deliberately NOT a duration parser: `T#60S` and `T#1M` are the same interval and it reports
them as different.*** Teaching it to equate them means teaching it to equate things, which is how a
comparator starts passing. The report prints both strings, so **the cost of the strictness is a human
reading two values; the cost of the leniency would be a silent green.**

#### The four states, and only one is a pass

| state | means | is it a pass? |
|---|---|---|
| **Current** | every declared bound matches the table — *and the pass NAMES the values it checked* | ✅ the only one |
| **Stale** | a declared bound differs from the table's current value | ❌ refuses. **Not the block's fault** |
| **Unknown** | a declared bound names nothing in the table — *a disagreement about which bounds EXIST*, not about a value. Its repair comes first, so it **outranks Stale** | ❌ refuses. Not the block's fault |
| **NotDeclared** / **NoTable** | the vector said nothing, or the enumeration supplied no table | ❌ ***NOT CHECKED*** |

***`NotDeclared` KEEPS ITS OWN STATUS EVEN BESIDE A PROVABLY STALE SIBLING.*** A submission containing
both is reported as **NOT CHECKED**, with the stale ones named separately underneath — because
*"we compared and refused"* must never be allowed to hide *"and these we could not compare at all."*
Both refuse, so nothing is admitted either way; **only the report differs, and the report is the build
list.**

**What it cannot tell apart, stated rather than hidden:** a genuine retune and a mis-transcribed table
entry produce the same finding. Both are `Stale`, both need a person, and **neither is the block's
fault** — so the verdict is right in both cases even though the diagnosis is not determined. What it
must never do is resolve that ambiguity by guessing in the passing direction.

---

### 2.6 ***EVERY VALUE TRAVELS IN A MIRROR ELEMENT, AND THE ELEMENT DECIDES ITS RANGE.*** ADDED 2026-08-13

> *** A COMPLETION SIGNAL IS NOT A SPECIAL KIND OF THING — IT IS JUST ANOTHER MIRRORED ELEMENT. ***

This governs **every** value a vector writes or compares: each entry of `inputs`, and `completionValue`.
They are all held to one rule, from one table.

#### The type is declared on the SIGNAL, never on the value

***A vector does not give `completionValue` a type, and that is the point.*** The element type belongs to
the **signal's row in the mirror's element table**, alongside the width, the address form and the rung
shape the copy layer generates for it.

> **Putting a width anywhere else gives that value a SECOND, PRIVATE NOTION OF WIDTH THAT CAN DISAGREE
> WITH THE MIRROR'S** — which is the defect shape the element table exists to remove. **One notion of
> width, one place.** A new element type then inherits its bound from the same row that gives it its
> width and its rung, so there is no second table of limits to fall out of step with the first.

#### The shape

```
element type        address form   registers   range
  Bool                 Bit             1       true | false   (1 / 0 accepted; nothing else is coerced)
  Int                  Word            1       *** SIGNED 16-BIT ***
  Time                 DoubleWord      2       signed 32-bit
```

***The numbers are deliberately not restated here.*** They are derived from the **address form** and are
read from the element table's own bounds — this document names the derivation and stops, as it does for
§12a. What an author must take away is the shape, and one fact:

> ⚠️ ***`Int` IS SIGNED. ITS CEILING IS 32 767, NOT 65 535.*** A value of `40000` does not fit an `Int`
> element and needs a `Time`. **Measured on the deliverable vector set: 81 duration values exceed
> 65 535 ms**, ten distinct figures between 70 000 and 120 000 — so **32-bit values are the norm in this
> system, not the exception**, and a design that assumed one register was wrong about the ordinary case.

**An element type nobody stated is unusable, not a default.** The table's zero value is `Unstated`, and
a value cannot be range-checked against a type with no width.

#### Out of range is a REFUSAL BY NAME, and it says what the value WOULD have become

> *** NO MODULO. ANYWHERE. ***

The refusal names the signal, the value, the declared type, the register count and the range — **and for
a single-register element it also prints what the value would have arrived as.** That sentence is the
whole point:

> `75000` through a single-register mapping arrives as ***`9464`***. **That is not an error and not a
> timeout.** Every boundary keyed on it fires early and the run returns a **plausible-looking `FAIL`
> against a block that did nothing wrong.** 9 464 ms is a perfectly reasonable dwell, which is precisely
> why nobody would question it.

***AND THE TWO 32-BIT HAZARDS ARE NOT SYMMETRIC, WHICH IS WHY ONLY ONE OF THEM IS MADE LOUD HERE.*** A
swapped **word order** announces itself — 75 s becomes about 7 days, the scenario never reaches its
boundary, and the run `TIMED-OUT` noisily. A truncated **width** passes quietly with the wrong number.
**The width failure is the dangerous half, so it is the one refused by name**; the order hazard is
already loud and is left to the rig's calibration step.

#### An absent value refuses. It does not become 0

***Zero is a value a block could legitimately be driven with***, so writing one on the author's behalf
would be **inventing the stimulus**. Same doctrine as `completionValue`'s removed default of 1, and as
the missing predicate: a field the runner fills in for you is a fact nobody supplied, arriving later as
a verdict about the block.

**One exception, and it is not a default:** a signal the vector says *nothing about at all* is not
checked here. **Leaving an input undriven is a legitimate choice**, and it is the inert declaration's
business rather than the width rule's. *Absent from the map is a different statement from present and
empty* — the absent-versus-empty distinction this document uses throughout.

#### 🔴 Current built behaviour: a multi-register completion signal is REFUSED, not compared

The wave compares completion against **one** register holding a 16-bit value, so a completion signal
whose element is wider **cannot be honoured**. ***The loop refuses it by name rather than comparing
anyway***, and the reason is a verdict asymmetry worth stating on its own:

> Comparing a 32-bit completion signal against one register tests only its **high half** and reports
> ***`TIMED-OUT` forever on a block that actually finished***. And *** A SPURIOUS `TIMED-OUT` IS WORSE
> THAN A SPURIOUS `FAIL`, BECAUSE IT IS BELIEVED *** — a `FAIL` invites an argument with the
> specification, while a `TIMED-OUT` reads as *the condition simply never occurred* and closes the
> question.

**So this is INEXPRESSIBLE, not mis-expressed**, and the refusal says so. The remedy is either a
single-register completion signal or widening the wave's completion comparison to carry an element
width the way every other mirrored value now does — **owed by the harness, and the contract's rule is
already the widened one.**

---

### 2.7 `map.storage` — ***WHERE A SIGNAL IS, NOT ONLY HOW IT IS WATCHED.*** ADDED 2026-08-14

**Gates 8 and 8c could not be fed from a real submission at all** — *not merely unsupplied, but
inexpressible* — and this is the field that closes it.

> *** MEASURED ON THE DELIVERABLE SUBMISSION: 16 OF 17 SIGNALS RESOLVE TO NO PLC STORAGE PATH. *** They
> are **harness-side logical names**, and the join from a logical name to the storage it occupies was
> **stated nowhere in the submission.** `map.providedFor` carries *observability modes* only — it says
> **how** a signal can be watched and never **where it is.**

Both gates consume a conflict graph, and a conflict graph is a statement about **storage**: two blocks
conflict because they write *the same location*. With no join, the graph is withheld and both gates
report `NOT CHECKED` for ever.

#### The join is DECLARED, and it is unambiguous BY CONSTRUCTION

```
map
  providedFor    signal -> [ modes ]          -- §4.3. HOW it can be watched
  storage        signal -> { owner?, path }   -- WHERE it lives, when it lives in the PLC
      owner        the block (or tag table) that DECLARES the root.
                   *** OMITTED for a global path *** — a DB member, a PLC tag, an `iDB_…`
                   member or a physical address is already unique and is written verbatim
      path         the path within that owner, or the global path verbatim
  harnessOnly    [ signal ]                   -- a POSITIVE claim: occupies NO PLC storage
```

***`owner` and `path` are separate keys and are deliberately NOT one dotted string.*** The corrected
analysis keeps the owner beside the path for exactly this reason, in its own words: **an emitted string
is not a schema.** A consumer handed `A.B.C` cannot tell whether `A` is an owning block or a DB without
parsing, and a parse is a lookup — which is the thing this field exists to remove.

#### 🔴 A BARE LEAF NAME IS NOT A STORAGE REFERENCE, AND MATCHING ON ONE IS FORBIDDEN

The obvious shortcut — take the signal's name and find the storage whose path *ends with* it — was built,
examined and **declined for the right reason**:

> ***RESOLVING A SIGNAL BY NAME SHAPE RE-INTRODUCES THE ALIASING DEFECT THAT MANUFACTURED FICTIONAL
> MULTI-WRITERS*** — `IO.Step` declared in two different UDTs, a `Time` temp declared separately in three
> FBs — **at the gate boundary instead of inside the analysis.** Two of the four cross-block multi-writer
> findings this project has ever recorded were fiction produced exactly that way.

So the declared join **replaces** suffix matching rather than supplementing it. **Nothing in this
contract may resolve a signal by the shape of its name.**

***An ambiguous resolution is REFUSED NAMING EVERY CANDIDATE, never silently resolved to one.*** Instance
aliases of a single storage are collapsed first, so a refusal here means **genuinely different storage** —
and picking one of them is the fiction, not the fix. Qualify the signal, or the graph does not run.

#### Three states, and the middle one is a claim rather than a silence

| | means | effect on gates 8 / 8c |
|---|---|---|
| in `storage` | this signal occupies **that** PLC storage | resolvable; may produce edges |
| in `harnessOnly` | ***a positive claim: it occupies no PLC storage at all*** — a mirror-side logical name | **no edge is possible, and that is a computed fact** |
| in neither | **nobody stated the join** | ***NOT CHECKED*** |
| in **both** | a contradiction | **REFUSED**, naming the signal |

**`harnessOnly` exists because the tooling names this ambiguity and cannot settle it**: an unresolved
signal reports *"this may be a mirror-only signal, or the name may be wrong"* — two entirely different
repairs behind one silence. **The claim is the author's to make**, and making it is what turns a
`NOT CHECKED` into a fact.

#### The refusal semantics of `conflictEdges`, exactly

> ***WHEN THE GRAPH DID NOT RUN, THE KEY IS OMITTED — NOT `[]`, AND NOT `null`.***

- **`conflictEdges: []` is the EARNED positive claim** that the graph ran over a whole corpus and found
  nothing. Two authors have already refused to write it unearned; the format must keep that refusal
  expressible.
- **Omitting the key** leaves gates 8 and 8c `NOT CHECKED`, which is the weaker and *true* statement.
- ***And `null` is not the same as omitted.*** A lenient deserializer can turn a null back into an empty
  collection **one layer down**, restoring the false claim after the refusal was correctly made. The key
  is absent, or it carries edges.

A partial corpus is the case that makes this concrete: **a file that could not be parsed may hold the
second writer that makes a signal a conflict**, so a graph built over it cannot honestly say *no
conflicts*. The key is withheld.

#### `computedConflicts` is never emitted, and that is deliberate

**Write this down or somebody will "fix" the omission.** A bare block name records no provenance, so it
can only ever be `Unstated` — and the multi-writer gate's provenance test is ***all-or-nothing***:

> *** ONE UNPROVENANCED EDGE TURNS GATE 8c TO `NOT CHECKED` FOR THE ENTIRE SUBMISSION. *** A
> helpful-looking extra edge — a call-graph coupling, say, which is about **no signal** and so could
> carry no signal class — would **silently disable the multi-writer report it was added beside.**

Gate 8's packing set derives from the edges. **Call-graph and model-sharing coupling belong to the
author's blacklist**, which is add-only for precisely this reason (§7).

**A signal's CLASS is derived, never declared.** Whether a conflict ships is read off the writing blocks
themselves; there is no flag and no field for it, and an unclassifiable one carries `Unstated` through to
the gate rather than being guessed. *The refusal is carried across, not resolved.*

> ⚠️ **§2.7 treats a symptom. The root cause is §2.8** — the harness assumed a specification's signal
> name *is* the block's tag name. Of the 17 signals in the deliverable submission the conflict graph
> resolved **one**, and it is `HopperBlockedAlarm`: ***the only signal whose spec name and block name
> coincide.***

---

### 2.8 ***THE MAP IS A TRANSLATION, AND THE MODE IS DERIVED.*** ADDED 2026-08-14

> *** THE HARNESS ASSUMED THE SPECIFICATION'S SIGNAL NAME **IS** THE BLOCK'S TAG NAME. *** Three
> mechanical paths broke on that single assumption in one live run: gate 5 refused 17 `Latched`
> declarations, the static interface check reported a signal missing, and the conflict graph resolved
> **1 of 17** signals.

> 🔴 *** AND THE ONLY PLACE THE TRANSLATION HAD EVER BEEN RECORDED WAS A PROSE MARKDOWN TABLE — WHICH IS
> WHY A PREDICTED FINDING COULD BE LAUNDERED IN IT. *** There was nowhere else for it to live, and
> **prose is what no gate reads.** A fact that only exists in prose is a fact that can be quietly
> restated; this section is that fact given somewhere to live.

#### `specName` beside `tag`

```
resultSources[ ]                -- one mirrored signal, in the coordinator's binding
  tag          the tag the signal occupies ON THE CONTROLLER
  specName     THE SPECIFICATION'S NAME for the same signal
  type         the element type (§2.6)
  latchedBy    the BLOCK that latches this signal, when one does
```

> ✅ **NARROWED 2026-08-14 TO MATCH THE CODE, AND THE CODE IS THE SAFER SHAPE.** This section first
> specified `modes`, a `modeSource` of `Generated`/`HandAuthored`/`SelfDeclared`/`Unstated`, and an
> `instrumentedBy`. What was built is a **strict subset**: `specName` and `latchedBy`, and nothing else.
>
> ***That subset is stronger, because it makes `SelfDeclared` INEXPRESSIBLE rather than merely NOT
> CHECKED.*** There is no field in which a vector author can assert a mode at all, so there is no
> assertion to catch, report or forget to catch. **A field that cannot be written cannot be written
> wrongly** — the same property that makes the blacklist add-only and the split read unnameable. The
> ruling is that the contract narrows to the code, not the other way round.

> 🔴 ***AN ABSENT `specName` DOES NOT MEAN "SAME AS `tag`". IT IS A REFUSAL.***
>
> **That defaulting rule is precisely the assumption being removed**, and writing it into the format
> would reinstate it invisibly — with the difference that it would then look like a decision somebody
> made. The two names coincide *sometimes*, and the case where they coincide is exactly the case that
> hid this for as long as it did: **one signal in seventeen resolved, and it resolved because its two
> names happen to be the same string.**

This is also what §2.7's join needs to be *about*: `map.storage` says where a **tag** lives, and the
specification cites a **spec name**. Without the translation the join has nothing to key on.

#### The instrumentation mode — ***DERIVED WHEREVER IT CAN BE***

Gate 5 failed in **both directions at once**, which is the fact that shapes this field. Of **17
`Latched` expectations across 11 of 27 vectors**:

| | |
|---|---|
| **13 refusals were CORRECT** | `Latched` was claimed where the copy layer emits a plain **coil**. A coil is not a latch, and the claim was a caller assertion nothing checked |
| 🔴 **4 refusals were FALSE** | those signals ***genuinely are latched on the device***, by a deployed hand-authored block — and the document has **no mode field at all**, while the code hard-codes every signal to `Sampled`. *** A REAL, DEPLOYED LATCH WAS STRUCTURALLY UNDECLARABLE. *** |

So the rule is not "let the author declare the mode". It is:

> ***THE MODE IS DERIVED FROM WHAT THE COPY LAYER GENERATES.*** The generator emits **no per-signal
> latch**, so `Sampled` is all it can derive — and there is no field in which anyone may say otherwise.
> A declared mode would be a caller assertion, and a caller assertion is **forgotten exactly when it
> matters**: the 13 wrong claims are what that looks like at scale.

#### `latchedBy` names a BLOCK, not a mode — and that is why it is admissible at all

Derivation cannot reach the other four, because the generator did not emit them: they are hand-authored
IR already on the rig. So one thing is statable — **not the mode, but who performs it.**

> ***`latched: true` WOULD SWAP THE GENERATOR'S ASSUMPTION FOR A CALLER'S***, which is no improvement.
> **A block name is PROVENANCE**: it is checkable against the deployed object set, it is printed in the
> gate's own report, and it either exists and does the latching or it does not. That is the entire
> reason this field is allowed to exist where a mode flag is not.

**Absent `latchedBy` means this binding claims no latch** — so `Sampled` stands, and a `Latched`
expectation on that signal is refused. That fails closed, and it is a derivation rather than a default:
*the copy layer emits no latch, so the absence of a named latcher is the true state.*

#### 🔴 THE GATE TAKES THE NAME, NOT THE FACT

**Stated here so the caveat survives independently of any rendering path**, which is the lesson as much
as the limit:

> The gate **does not verify that the named block is deployed, or that it latches this signal.** It
> records the claim and prints it. ***Verify those blocks are in the deployment yourself.***

> ⚠️ ***AND THE ADMISSION WAS RENDERED ONLY WHEN THE REPORT HAD NO PROBLEMS*** — so four claims were
> admitted on an unverified block name and **no reader was told**. *** AN ADMISSION THAT APPEARS ONLY
> WHEN EVERYTHING PASSED IS MISSING FROM EVERY REPORT ANYONE READS CLOSELY. *** The reports people study
> are the ones with findings in them. A caveat attached to the clean branch is a caveat nobody meets.

#### A block that latches ITS OWN OUTPUT is a VALUE UNDER TEST, not instrumentation

**This is the one place a reader is tempted to name the block under test as its own instrumenter**, and
an artificial corpus invites the mistake unusually strongly.

> If the sealing, the latching or the holding **is the behaviour the vector is asserting**, then the
> latch is ***what is being tested***, not what makes it observable. Such expectations are declared
> **`Sampled`**, and `latchedBy` is left absent.

Naming the block under test in `latchedBy` claims that the thing being tested is the reason the test can
see anything — which would make the assertion true by construction, and is the correlated check this
pipeline exists to prevent arriving through a new door. **`latchedBy` names an INSTRUMENT: something
that exists so a value can be observed, and would be pointless otherwise.**

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

> ⚠️ ***DECLARING `Latched` DOES NOT MAKE A SIGNAL LATCHED.*** Measured on one live run: **17 `Latched`
> expectations across 11 of 27 vectors**, of which **13 named signals the copy layer emits as a plain
> coil.** The mode is a property of **what was generated or deployed**, not of what a vector wants — it
> is derived, and a declaration that contradicts the generated shape is refused naming both. **§2.8.**

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

***The map has two further halves, and neither is this one.*** `providedFor` says **how** a signal can
be watched. It says nothing about **where the signal is** (§2.7), and nothing about **what the
specification calls it** (§2.8) — and the mode above is ***derived from what the copy layer generates***,
never taken from an author's declaration. **`Signal` here is the `tag`; the specification cites the
`specName`.**

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

### 4.5 ***MEMORY LAYOUT — THE PROPERTY THAT DECIDES WHETHER THERE IS ANYTHING TO OBSERVE AT ALL***

**RULED 2026-08-13. This is the coordinator's ruling, not the owner's, and it is overturnable at one
line** — the line is the invariant in the box below.

§4.1 says the floor is physical. **This is prior to the floor and it is binary.**

> ***AN S7-1200 BLOCK DEFAULTS TO `MemoryLayout: Optimized`, AND AN OPTIMIZED BLOCK IS INVISIBLE TO
> CLASSIC S7comm — NOT AN ERROR. THE BLOCK IS SIMPLY ABSENT, AND IT FAILS AT THE FIRST *DATA* READ
> RATHER THAN AT CONNECT.***

A failure at connect would be diagnosable. A failure at the first data read, on a link that connected
cleanly, reads as a wiring or addressing fault and sends the reader to the wrong half of the system.
And **the toolchain cannot see it**: `MemoryLayout` is on the `Normalizer`'s ignore list, so
`drift-check` reports MATCH in **both directions** and would never surface it.

#### The invariant

> *** THE HARNESS NEVER TOUCHES A DELIVERABLE BLOCK'S DATA DIRECTLY. IT TOUCHES HARNESS-GENERATED
> OBJECTS ONLY. ***

**This is the isolation the design already rests on** — the copy layer exists precisely so that a test
never reaches into the block under test — so the requirement lands entirely inside what the harness
generates, and ***the block under test stays `Optimized`, the platform default, costing a deliverable
nothing.*** No convention, no IR change, no argument with a block author.

#### The two mechanisms, which are NOT the same and must not be conflated

| harness object | how visibility is guaranteed | can it revert? |
|---|---|---|
| **the `%MW` mirror** (the wave's whole surface: start bools, inputs, results) | ***it is not a data block at all.*** Bit memory has no `MemoryLayout` attribute, so there is nothing to revert | **No — structurally** |
| **a harness DATA BLOCK** (today: the rig marker DB) | declared `Standard`, and **re-asserted on the device after every import** | **Yes, silently, at every import** |

*Somebody looking for `--expect Standard` on the mirror will not find one and must not read that as a
missing check.* For the mirror the guarantee is **the address**; for a DB it is **the re-assertion**.

#### Two consequences, stated rather than implied

1. ***THE LAYOUT REVERTS AT EVERY IMPORT, SILENTLY.*** The exported `.xml` carries no `MemoryLayout`
   element, so the import states no opinion and TIA applies the S7-1200 default. It bites on the
   **second** import. So the sequence is `openness-cli block-layout --set Standard --yes` **after each
   import**, then `--expect Standard` as the gate. ***`--expect` only tells you it broke; `--set` is
   what repairs it*** — a check without the repair beside it is a gate that reports the same failure
   for ever.
2. ***`drift-check` MUST NEVER BE USED AS THE GATE FOR THIS.*** It is structurally blind to the
   attribute, in both directions. `--expect Standard` is the only check that can see it. *A green from
   a blind check is worse than no check, because a green with a reason attached stops being questioned.*

#### What a submission declares

```
deployment                    OBJECT. A property of the DOWNLOAD, never of a vector.
  importStamp       string    identifies the import the copy layer was generated by.
                              Required whenever `s7Objects` is non-empty; every layout claim is
                              dated against it
  s7Objects   [ ... ]         EVERY object any classic-S7comm path may reach
      area                    the area name the transport addresses it by
      dbNumber                the DB number
      harnessObject           the HARNESS-GENERATED object this is.
                              *** A ROW NAMING A DELIVERABLE BLOCK IS A REFUSAL, NOT A FINDING ***
      layout                  "Standard" | "Optimized" | "NotApplicable"
      layoutSetAfterImport    the `importStamp` that `--set Standard --yes` followed.
                              *** NOT A BOOLEAN, DELIBERATELY *** — see below
```

***`s7Objects: []` is a POSITIVE CLAIM and is the NORMAL, CORRECT state:*** *no classic-S7comm path in
this deployment reaches any data block.* That is what a mirror-only wave looks like, and it is CHECKED
as a claim. **An absent `deployment` is a different statement** — nobody said — and is `NOT CHECKED`.
The same absent-versus-empty distinction the conflict graph and `presets` already use.

**`layoutSetAfterImport` is a stamp and not a boolean, and that is the load-bearing choice.** A bool
would be a caller-supplied verdict — the exact shape this project has now killed three times
(`observabilitySupported`, the drain-state bool, the multi-writer report). A stamp can be **compared**:
if it does not equal the current `importStamp`, the layout was re-asserted against a *previous* import
and has reverted since. That is a computation, and *"nobody re-asserted it"* and *"it was re-asserted
after the wrong import"* both fall out of it as distinct facts.

**`layout: "NotApplicable"` is for objects with no layout attribute** — a `%M` region, a tag table.
`Optimized` is a **REFUSAL**. An absent or unstated `layout` is **also a refusal**, and for the reason
the IR-side check already gives: *absence means "no opinion", never a default, and TIA resolves no
opinion to Optimized.* **Undetermined is not clean.**

#### 🔴 Is the invariant true of the code as written? ***TRUE TODAY, AND NOT ENFORCED***

Established by reading `src/harness/` at `f0fb0cb`, because *a ruling the code happens to break is one
that gets discovered by a failed rig session.* Every classic-S7comm data path in the harness, and what
each targets:

| path | targets | invariant |
|---|---|---|
| `WaveRun` / `SlotRun` / `InertPhase` / `MirrorClient` — the whole wave, ***including the start bools*** | **Modbus holding registers only**, which `MirrorGeometry` places in `%M` bit memory. `BitAddressOf` renders a start bool as `%M<byte>.<bit>` | ✅ **holds structurally** — no data block is involved, so no layout exists to revert |
| the rig-identity read (`MarkerDbIdentitySource`) | the rig marker DB, a harness-generated object whose layout type declares it **STANDARD** and whose offsets are computed from the standard layout rules | ✅ holds |
| restore-point capture/restore, and the rig read/write probes | default to the marker DB; **`--db` and `--area` are free-form overrides** | ⚠️ **unconstrained** |
| ***the symbolic S7 transport*** (`S7Transport` → `WriteArea(S7Area.DB, …)`) | **any DB, resolved through a HAND-WRITTEN JSON tag map**. Its own doc says the DB must have optimized access off and that *"neither of which this code can check"* | 🔴 ***unconstrained — this is the mechanism by which the invariant would be broken*** |

> ***THE START BOOL IS `%M`, NOT A DB.*** §6's gate is therefore not in tension with the invariant, and
> it cannot be — a mirror in a DB is refused before any of this, by the address rule.
>
> ***AND NOTHING READS A DELIVERABLE BLOCK'S DATA TODAY*** — but only because no tag map in the
> repository points at one. The only tag definitions that exist are test fixtures. **The write fence is
> scoped on the AREA NAME, which comes from that same hand-written map**, so it verifies that the
> caller's claimed area matches the tag's — never what *kind of object* the area is. ***The invariant is
> a true statement about the current configuration and is enforced by nothing.***

**So the enforceable point is nameable, and it is the tag map**: every distinct `(area, dbNumber)` a
tag map can reach must appear in `deployment.s7Objects` naming a harness object. That is a
set-difference against an artifact the vector author did not write for this purpose — the same shape as
M4's fidelity check and `trace`'s guard-containment hop.

**Three parts of the harness already encode this ruling and one is in flight** — the mirror geometry
(why `%MW` and not a DB, including the `16#818C` refusal an optimized DB returns), the retention check
(which refuses a harness DB carrying no layout line, or declaring `Optimized`, or a mirror tag outside
`%M`), the marker DB's declared STANDARD layout, and the deployment plan being built now, which already
sequences `--set` then `--expect` after every import. ***The contract was the only place it was not
written down.***

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
| ***STALE*** | ***the experiment never ran*** — **or the vector's premise expired** (§2.5) | anything whatsoever about the block |
| **REFUSED** | the vector was inadmissible (observability, fidelity, authorship, basis) | a defect in the block |

***`STALE` HAS TWO ROADS AND THEY NEED DIFFERENT INSTRUCTIONS*** (§2.5). A frozen mirror is a **RIG**
problem — the experiment never ran. An out-of-date `boundsUsed` is a **VECTOR** problem — the experiment
ran fine and measured the wrong number. **Read which one the result names before acting**, and in
neither case edit the block.

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
| **3f citation shape** | the citation is the content-derived ID, never the **display ordinal** | reject. An ordinal is POSITIONAL, so inserting one assertion above it silently makes the citation name a different one |
| **3g assertion IDs recompute** | every ID re-hashes from the `normalisedTexts` it claims to come from | reject. ***This recomputation is the ONLY reason a stamper is permitted to have no independence*** — without it a hand-written hex string is indistinguishable from a computed one. Absent texts are `NOT CHECKED` |
| **3h required observations** (AMB-14) | every signal the cited assertion depends on appears in this vector's `Expectations` | reject, naming the unobserved signals. A relational assertion has more than one, and observing one tests neither the other nor the relation |
| **3i bounds currency** (AMB-19, §2.5) | each `boundsUsed` value matches `enumeration.bounds` | ***refuse, reported as `STALE` and never `FAIL`***, ending *"Do NOT edit the block on the strength of this finding"*. A vector declaring no bound is `NOT CHECKED` **and keeps that status beside a stale sibling** |
| **fidelity** | every asserted behaviour ∈ the model's fidelity declaration (M4) | reject with both sets shown |
| **observability** | mode valid; signal in the map; window ≥ §12a's floor **at the run-time `comp`**; declaration predates the generating download | ***refuse the vector*** — §2.6's own rule |
| **settling** | declaration exists; **is not the completion flag alone** | reject, citing phase 2's `Done`-at-10-ramps-to-15 |
| **start bool** | exactly one per slot; bound by name; later-scan rule against the observed counter | reject |
| **blacklist** | add-only against computed disjointness; every entry has a reason | reject the *entry*, not the vector. ***Both this gate and 8c are fed from `conflictEdges`, which cannot be computed without `map.storage` (§2.7)*** — until the join is declared they are `NOT CHECKED` for a reason that is not the author's blacklist at all |
| **8c multi-writer provenance** (X-G) | every conflict edge records WHY the two blocks conflict, on which signal, and whether that signal is part of the deliverable | ***the FINDING is reported, not refused*** — the defect is in the deliverable, not in the submission. **An UNPROVENANCED graph is `NOT CHECKED`** and fails closed — ***and it is ALL-OR-NOTHING, so one unprovenanced edge disables the report for the whole submission*** (§2.7, which is why `computedConflicts` is never emitted) |
| **10a assertion ceiling** (X-D) | the run-time `comp` is under every vector's own `T_event / scan_period` ceiling | reject, naming the binding signal and the ceiling. **Catches what gate 5 structurally cannot: LATCHED is exempt from the observability floor, never from the scan-period term** |
| **10b timer / model / ratio ceilings** (X-D) | at `runtimeCompression` > 1, the block's presets, the model's `comp_stable` and the ratio-distortion threshold (§2.3) | reject with `comp_min` **and** `comp_max` shown. Absent inputs are `NOT CHECKED`, never a pass. At `comp` = 1 a real computed pass |
| **liveness** *(post-run)* | stimulus check present; counter advanced by the expected amount; manifest presence | verdict `STALE`, never `PASS` |
| **11 memory layout** (§4.5) | every `(area, dbNumber)` a classic-S7comm path can reach names a **harness-generated** object, declares `layout: Standard` (or `NotApplicable`), and carries a `layoutSetAfterImport` equal to the current `importStamp` | ***refuse.*** A deliverable block named here is the invariant broken; `Optimized`, an unstated layout, or a stale stamp each mean the object is **absent on the wire** and every read fails at the first *data* transfer. **Absent `deployment` is `NOT CHECKED`** |

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
