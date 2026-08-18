# The observation-window model — *what shape an expectation has in time*

**Written 2026-08-18, before the code, as the design half of blocker 2.** Scope: the evaluation half only
(`src/harness/Harness.Results/SeriesEvaluation.cs`). The vector-format half is a **proposal** at the end
and is not implemented here — see §7.

---

## 1. The problem, stated exactly

The harness retains the whole poll series per index and folds it three ways
(`SeriesEvaluation.Evaluate`): every considered frame agrees ⇒ `Held`; none agrees ⇒ `Disagreed`; **some
did and some did not ⇒ `Inconclusive`.**

That third branch is correct and it is not the complaint. The complaint is that **it is reached by four
different authors meaning four different things**, and nothing anywhere lets them say which:

| what the author meant | is `51 of 65` a pass? |
|---|---|
| true **throughout** the phase | **no** — it dropped, and dropping is the defect |
| true **at some point** in the window | **yes** — it was seen |
| true **at a defined instant** | *undecidable until the instant is defined* |
| **becomes** true and **stays** true | **yes if the 51 are the tail**, no if they are the head |

All four collapse to one verdict today. Measured on a live wave (JOB9004, 3 vectors, 1 slot): **2 of 3
packages `Inconclusive`, 4 of the 14 assertion rows `Inconclusive`, `conclusiveAboutTheBlock: 0`.** At one
slot that is an annoyance. At plant scale it is most of the results, and a campaign that mostly returns
"inconclusive" costs full wall-clock and teaches nothing.

### 1a. The rule that governs every decision below

> ***WIDEN WHAT CAN BE SAID. NEVER WIDEN WHAT PASSES.***

A rule of the form *"if most observations agree, call it a pass"* converts an honest refusal into a
fabricated green. **An expectation whose shape was never stated stays `Inconclusive`, with today's text,
byte for byte.** Every shape below is a claim the author had to type.

### 1b. What is already there, and what is missing

Retained and per-frame: the **value**, the **scan counter**, the **poll round**, and the **window state**
(`InWindow` / `OutOfWindow` / `Unknown`) taken from the arm register in the *same* read. Frames arrive in
poll order.

So the series already supports *universal*, *existential*, *ordering* and *single-instant* questions.
**Nothing was missing from the data. What was missing was the question.**

One near-miss worth naming: `expectations[].windowScans` already reads *"how long the condition is
expected to HOLD, in scans"*. **It is consumed only by the observability floor and the compression
ceiling — never by the evaluator.** It is a *sizing* input, not a temporal claim, and it must not be
retrofitted into one (§6d).

---

## 2. The one asymmetry the whole model rests on

The harness sees **the frames it took, inside a window it may not know the bounds of.** Two consequences,
and they point in opposite directions:

- **Presence of a contradicting sample is evidence.** If a frame *inside the declared phase* disagrees,
  the block did that. That may accuse.
- **Absence of an agreeing sample is weaker.** The event may have fallen in a poll gap. That is what the
  observability floor exists to bound, and it is why `Sampled` needs a declared window at all.

And the tail. **A well-built stimulus model returns the block to inert *before* it raises its completion
flag** — that is required, and it is what stopped the first live wave dying after one vector. So the
frames after the phase are, by construction, the ones where every commanded member is inert. They are in
the retained series and **nothing separates them from the phase unless the binding declares `armedBy`.**

> ### The accusation rule
>
> **A shape may accuse only on the strength of a frame known to be inside the phase.**
>
> Where a shape's refutation rests on a *disagreeing* (or *forbidden*) frame and **no arm window was
> declared**, that frame may be a tail frame, and the verdict is `Inconclusive` — naming `armedBy` as the
> repair. Where every frame agrees, or none does, the answer is the same with or without the window, and
> no window is required.

This rule is what keeps the widening one-directional. It is also why declaring a shape **is not by itself
enough to turn today's measured `Inconclusive` rows into failures**: the sharpest shapes still need the
binding to publish the window.

---

## 3. The shapes

Five. Four from the brief, one added (§3e), one rejected candidate promoted to §6.

Names are chosen to be unmistakable against the two enums already in this file's neighbourhood —
`InstrumentationMode` (`Latched`/`Sampled`/`Stamped`) and `SignalNature`
(`PersistentState`/`Transient`/`Coincidence`). **In particular the latching shape is *not* called
`Latched`**: see §5.

Throughout, *considered* means what `SeriesEvaluation` already means by it — the in-window readable frames
where a window was declared, otherwise all readable frames.

### 3a. `Throughout` — universally quantified over the phase

*"This signal is expected to be true THROUGHOUT this phase."* Commanded states, held outputs, a mode that
must persist.

| evidence | verdict |
|---|---|
| every considered frame agrees | **`Held`** — *no counterexample in N samples*, which is all sampling can ever mean |
| no considered frame agrees | **`Disagreed`** — unchanged from today |
| mixed, **window declared** | **`Disagreed`** — a frame inside the declared phase contradicted it. ***This is the new accusation, and it is the whole point of the shape*** |
| mixed, **no window** | **`Inconclusive`** — the disagreeing frame may be a tail frame. Repair: `armedBy` |

### 3b. `AtSomePoint` — existentially quantified over the phase

*"Expected to be true AT SOME POINT in this window."* Events, acknowledgements, a step that must be
entered, anything whose exit is not part of the claim.

| evidence | verdict |
|---|---|
| any considered frame agrees | **`Held`** |
| no considered frame agrees | **`Disagreed`** — *exactly as strong as today's, not stronger* |

No window is needed in either direction. Passing rests on an occurrence *inside the considered set*, and
narrowing to the window (when one is declared) is exactly the right interval — an occurrence outside the
phase does not discharge the claim. Failing rests on absence across that same considered set, **which is
the set today's zero-agreement `Disagreed` already uses**, so this shape's accusation is *identical in
strength to the one the code makes today*, not a new one.

> ⚠️ **The trap, stated at the site.** An existential expectation on a **ramping numeric** is discharged
> by a transient pass-through: a counter climbing to the *wrong* value passes *through* the right one.
> `AtSomePoint` is for **event-shaped** signals. On a value that ramps, the shape you want is `AtEnd` or
> `Throughout`. The evaluator reports the number of distinct observed values so a pass-through is visible
> in the row rather than inferred later.
>
> This is also why the fold **still does not consult `AssertionForm`**. The tempting inference *"WHEN-form
> ⇒ existential"* would apply this shape to every WHEN vector including the ramping ones, from a field
> that is `Unstated` on the deliverable. `AtSomePoint` must be **typed**, never derived.

### 3c. `BecomesAndHolds` — ordered: false-then-true, and it stays

*"Expected to become true and STAY true."* Latching alarms, fault flags, sequence-complete bits, anything
with a rising edge that is not supposed to fall back inside the phase. **This is the shape that reads
`51 of 65` correctly**: a pass if the 51 are the tail, a defect if they are the head.

| evidence | verdict |
|---|---|
| the agreeing frames are a **non-empty suffix** of the considered set | **`Held`** |
| no considered frame agrees | **`Disagreed`** — it never became true |
| it agreed and then stopped, **window declared** | **`Disagreed`** — an observed fall-back inside the phase |
| it agreed and then stopped, **no window** | **`Inconclusive`** — the fall-back may be the model's own recovery. Repair: `armedBy` |

Strictly stronger than `AtSomePoint`; strictly weaker than `Throughout`. It is the only shape that reads
the *order* of the frames, and the only one whose verdict changes when the frames are reversed — which is
what makes it a distinct shape rather than a tuning of the other two.

### 3d. `AtEnd` — one defined instant

*"Expected true at this INSTANT"* — and the instant is defined as **the last considered frame**:
end-of-window where a window is declared, the completion instant where it is not. Post-conditions:
*the block returns to inert*, *the sequence leaves the valve closed*, a terminal state number.

| evidence | verdict |
|---|---|
| the last considered frame agrees | **`Held`** |
| it disagrees | **`Disagreed`** |

No `Inconclusive` branch, because there is no *set* to disagree with itself: there is exactly one frame,
and the row names its scan and its window state.

> 🔴 **THIS IS THE SHAPE WITH THE SHARP EDGE, AND IT IS THE ORIGINAL DEFECT WITH A DECLARATION ATTACHED.**
> The behaviour before 2026-08-17 *was* `AtEnd`, applied to every expectation, chosen by nobody. What
> makes it admissible now is that **the author typed it and the row names the instant.** A verdict taken
> at an instant the reader can see and argue with is a different object from one taken at an instant
> nobody knew was being used.
>
> With **no window declared** the named instant is the last frame before completion — i.e. **the inert
> tail** for exactly the class of well-built model this system requires. The row says so in those words.
> An author reaching for `AtEnd` to mean "at the end of the active phase" wants `armedBy` first.

### 3e. `AtNoPoint` — the forbidden value (added; argued)

*"This value must never be observed in this phase."* The `expected` field names the **forbidden** value.

**Why it is not redundant.** For a Bool, *never true* and *always false* coincide, so `Throughout` covers
it. **For anything else they do not**: *never state 7* is not *always state 3*, and the predicate model
here is string equality with no inequality operator. Without this shape a `NEVER`-form assertion over a
non-Bool has no expressible expectation at all.

**Why it earns its place rather than waiting.** `AssertionForm.Never` is already first-class in this
system, `ObservabilityCheck.ModesThatCanAnswer` already treats `Never` as its own case, and the live wave
recorded **four `Never`-form assertions among the nine it could not cover**. The shape is not speculative.

| evidence | verdict |
|---|---|
| the forbidden value appears in **no** considered frame | **`Held`** — ⚠️ *a pass produced by seeing nothing, which is also what a poll gap produces* |
| it appears, **window declared** | **`Disagreed`** — an observed occurrence inside the phase |
| it appears, **no window** | **`Inconclusive`** — the occurrence may be the model's own reset transient. Repair: `armedBy` |

**The rendering hazard, handled rather than noted.** Every other shape reads `expected: X / observed: Y`.
Here a `Held` row would read *"expected true … held"* about a signal that **was never true** — actively
misleading to a skimming reader. So on this shape alone the outcome's `Observed` text is written as an
explicit sentence (`<'true' was never observed — which is what this expectation requires>`) and the detail
states the inversion. The `Expected` field keeps the value the author declared, so the row still joins to
the vector.

---

## 4. What every shape does when the evidence disagrees — the summary table

| shape | all agree | none agree | mixed, window declared | mixed, no window |
|---|---|---|---|---|
| **`Unstated`** (default) | `Held` | `Disagreed` | **`Inconclusive`** | **`Inconclusive`** |
| `Throughout` | `Held` | `Disagreed` | **`Disagreed`** | `Inconclusive` |
| `AtSomePoint` | `Held` | `Disagreed` | `Held` | `Held` |
| `BecomesAndHolds` | `Held` | `Disagreed` | `Held` if suffix, else **`Disagreed`** | `Held` if suffix, else `Inconclusive` |
| `AtEnd` | `Held` | `Disagreed` | last frame decides | last frame decides |
| `AtNoPoint` | *(inverted)* `Disagreed` | `Held` | **`Disagreed`** | `Inconclusive` |

Read the first row twice. **It is today's behaviour and nothing in this change touches it.**

Three cases sit *before* the shape and are unchanged by all of it, because none of them is a question
about temporal shape:

1. **Nothing decoded from any frame** ⇒ `NotObserved`, `<never read>`.
2. **A window was declared and never opened in any retained frame** ⇒ `NotObserved`,
   `<observed only OUTSIDE the declared window>`. *Not a disagreement — every reading held is one the
   binding itself says is outside the window.*
3. **`InstrumentationMode.Latched`** resolves through the latch register and never reaches the series
   fold at all.

---

## 5. `BecomesAndHolds` is not `InstrumentationMode.Latched`, and the naming is deliberate

Two ideas one word away from each other, and collapsing them would undo the 2026-08-17 repair:

| | `InstrumentationMode.Latched` | `TemporalShape.BecomesAndHolds` |
|---|---|---|
| what it is | **how the signal is instrumented** — a sticky bit the copy layer emits | **what the author claims about the signal in time** |
| who decides | **derived** from the generated copy layer; a declaration contradicting it is refused | **the author**, and it is a claim that can be wrong |
| where the answer comes from | the **latch register** | the **poll series**, read in order |
| what it survives | a poll gap, and the tail | nothing — it is subject to the observability floor like any sampled reading |

A signal can be `Latched` *and* the assertion `BecomesAndHolds`; then the latch answers and the shape is
not consulted. **`Latched` is an instrument. A shape is a claim. The word `Latched` is spent.**

---

## 6. Rejected

### 6a. "If most observations agree, call it a pass"

The one that would make this worse than useless. A fabricated green from an honest refusal. Rejected
outright, and the fixed-threshold variants (`>90%`, `all but one`) are the same thing with a number.

### 6b. Deriving the shape from `AssertionForm`

*WHEN ⇒ existential, NEVER ⇒ `AtNoPoint`.* Free, requires no format change, and wrong twice: the field is
`Unstated` on the deliverable — so it would key a verdict on a field nobody filled in — and applied to a
ramping numeric the WHEN rule reports `Held` for a block the harness exists to catch (§3b). The existing
code refuses this and gives these reasons; nothing here weakens that.

### 6c. `HoldsForAtLeastNScans` — duration-quantified

*"True for at least 44 consecutive scans."* Tempting, because the frames carry scan counters and the span
of an agreeing run is arithmetic. Rejected: **consecutive frames are not consecutive scans.** A run of
agreeing frames spanning 44 scans is evidence about the frames *taken*, and the claim is about the scans
*between* them — an inference across unobserved time, biased toward pass. Answerable only with `Stamped`
instrumentation ("the only mode that answers *when*"), which the runner does not currently produce.

### 6d. Reusing `windowScans` as the temporal claim

It is already there and it already says *"how long the condition is expected to HOLD"*. Rejected: it is
consumed by the observability floor and the compression ceiling as a **sizing** input, and every existing
vector carries a value chosen for that purpose alone. Reading it as a temporal claim would **retroactively
reinterpret every vector already written**, which is precisely the silent change of meaning the backward
compatibility requirement forbids. It is a duration, not a shape; a shape is not derivable from it.

### 6e. `RespondsWithin <n>` — bounded response

*"Becomes true within n scans of the trigger."* Genuinely valuable and genuinely not available: the
reference instant would have to be the arm window's **opening edge**, and the series carries only the
first *observed* in-window frame. A poll gap puts the true edge earlier, so every measured latency is an
**under**estimate — the direction that makes a slow block look fast. Deferred to `Stamped`, and recorded
in §9 as a runner-side need rather than faked here.

### 6f. A submission gate on shape/mode coherence

E.g. refusing `AtNoPoint` on a `Latched` expectation, or `Throughout` on a `Transient` signal. Sound
ideas, deliberately not built here: the gate set is a **measured count** and adding to it belongs with the
format change and the vector author's agreement (§7), not ahead of it. Listed in §9.

---

## 7. The format addition — ***a proposal, not an imposition***

**Vectors are written by an independent party who deliberately does not read the implementation or the
binding.** That independence is load-bearing — this whole system exists because *"an AI reviewer reading
the same register as the AI coder is a correlated check."* So this section describes a field and asks for
it; it does not ship it.

### The field

One **optional** key per expectation, beside the four that are already there:

```jsonc
{
  "signal":        "SPEC.HoldCommand",
  "nature":        "PersistentState",
  "mode":          "Sampled",
  "windowScans":   875,
  "expected":      "true",
  "temporalShape": "throughout"     // NEW, OPTIONAL. Absent ⇒ exactly today's behaviour.
}
```

Accepted values: `throughout` | `atSomePoint` | `becomesAndHolds` | `atEnd` | `atNoPoint`.
**Absent, empty or unrecognised ⇒ `Unstated` ⇒ today's fold, today's `Inconclusive`, today's text.**

The zero value is `Unstated` and it is *unusable*, following `AssertionForm.Unstated`,
`MapProvenance.Unstated` and `WindowState.Unknown`: **a dropped field must fail the same comparison a
wrong one does**, so that "the field was absent" can never quietly become "the field said the convenient
thing".

### The four cases from the brief, written out

```jsonc
// "true THROUGHOUT this phase" — 51 of 65 IS a failure (given armedBy)
{ "signal": "SPEC.HoldCommand", "expected": "true", "temporalShape": "throughout" }

// "true AT SOME POINT in this window" — 51 of 65 is a PASS
{ "signal": "SPEC.AckPulse",    "expected": "true", "temporalShape": "atSomePoint" }

// "true at this INSTANT" — the instant is the last considered frame, and the row names its scan
{ "signal": "SPEC.StepNumber",  "expected": "7",    "temporalShape": "atEnd" }

// "becomes true and STAYS true" — 51 of 65 passes iff the 51 are the tail
{ "signal": "SPEC.FaultLatched","expected": "true", "temporalShape": "becomesAndHolds" }

// added: the forbidden value, for a NEVER-form assertion over a non-Bool
{ "signal": "SPEC.StateNumber", "expected": "7",    "temporalShape": "atNoPoint" }
```

### 🔴 Sequencing — the parser must learn the field *before* any author may write it

**Gate 0b refuses unknown submission fields.** A vector carrying `temporalShape` today is not ignored — it
is **REFUSED**, and the whole submission with it. So the order is fixed and non-negotiable:

1. the vector author agrees the field and its vocabulary;
2. `Harness.Gate/SubmissionDocument.cs` maps it (one property on `ExpectationDocument`) so gate 0b stops
   seeing it as unknown, and passes it onto `ObservabilityDeclaration.Shape`;
3. the runner passes it to `SeriesEvaluation.Evaluate` (§9);
4. authors may write it.

Steps 2 and 3 are **outside this track's file boundary** and are not done here. Until then the evaluator
accepts the shape on its own parameter and every real vector is `Unstated` — which is the same as today,
correctly.

### What this asks of the author, and what it does not

It asks for **one word about intent**, which is a claim about the specification the author already holds —
not about the block, not about the binding, not about registers. It does **not** ask them to read the
implementation, the copy layer, or the arm-window declarations. The independence is intact.

**And the field cannot buy a pass on its own.** `Throughout`, `BecomesAndHolds` and `AtNoPoint` reach
their *sharpest* verdict only when the binding publishes an arm window — a **coordinator-side** fact the
vector author does not control and cannot fake. Stating a shape widens what can be said; the window is
what makes the sharp half decidable.

---

## 8. What was built, and the mutation check

Built, in `src/harness/Harness.Results/` only:

- `TemporalShape.cs` — the enum, with the rationale at each member.
- `SeriesEvaluation.Evaluate(…, TemporalShape shape = Unstated)` — **trailing and optional**, so the
  runner's existing call compiles and behaves identically untouched. The three-way fold is unchanged for
  `Unstated`; the mixed branch dispatches to one handler per shape.
- `ObservationWindow.Shape` — an **init-only property with a default**, not a positional member, so no
  existing construction site changes. Emitted as `temporalShape` on every assertion row of the result
  JSON, `Unstated` included, because an absent key reads as "fine" to everyone who did not write it.
- `ObservabilityDeclaration.Shape` — the vector-side carrier, optional, **populated by nothing yet** (§7).

**Baseline 480 tests green; 29 added; 509 green.** The three invariants of §1a/§2 are asserted over the
**whole shape set** (`Enum.GetValues<TemporalShape>()`), so a sixth shape added later cannot silently opt
out of them.

### Mutation check — five breaking, two equivalent

Each mutation was applied to `SeriesEvaluation.cs` alone, the suite run, and the file restored byte for
byte.

| # | mutation | result |
|---|---|---|
| **M1** | the `Unstated` mixed branch returns `Held` instead of `Inconclusive` — ***"if most observations agree, call it a pass"***, the failure this must never have | 🔴 **RED, 5 failed** incl. `UNSTATED_IS_BYTE_IDENTICAL_TO_THE_PRE_SHAPE_BEHAVIOUR`, `THE_MEASURED_MIXED_SERIES_STAYS_INCONCLUSIVE_WHILE_NO_SHAPE_IS_STATED`, and **two pre-existing tests** |
| **M2** | `Throughout` accuses without a declared window (condition inverted) | 🔴 **RED, 5 failed** incl. `NO_SHAPE_ACCUSES_ON_A_MIXED_SERIES_WITH_NO_DECLARED_WINDOW…` and its converse `WITH_A_DECLARED_WINDOW_THE_SHARP_SHAPES_DO_ACCUSE` |
| **M3** | `BecomesAndHolds` stops reading the order (suffix test → "any agreement") | 🔴 **RED, 3 failed** incl. `…VERDICT_CHANGES_WHEN_THE_SERIES_IS_REVERSED` |
| **M4** | `AtNoPoint` stops inverting the fold | 🔴 **RED, 7 failed** incl. `NO_SHAPE_TURNS_A_TOTAL_ABSENCE_OF_AGREEMENT_INTO_A_PASS` |
| **M5** | `AtEnd` decides at the *first* considered frame instead of the last | 🔴 **RED, 3 failed** |
| **E1** | the suffix test rewritten as `agreed.Length == considered.Length - rose` — **arithmetically equivalent** | ✅ **GREEN, 509** |
| **E2** | wholesale agreement rewritten as `!considered.Any(f => !equals)` — **logically equivalent** | ✅ **GREEN, 509** |

The two greens are the half that matters as much as the reds: they show the tests pin **behaviour**, not
one particular way of computing it. Both reds and greens are reported, as required.

*(Two first-attempt mutations — `if (true)` / `if (false)` — did not compile at all: this tree treats
`CS0162 unreachable code` as an error. They were reformulated as the inversions above rather than counted
as reds, because **a mutation that does not build has not been tested by anything.**)*

---

## 9. What this needs from elsewhere — *named at the boundary, not reached into*

Nothing here edits `Harness.Loop`, `Harness.Wire` or `Harness.Gate`. Three items are owed by other tracks:

1. **`Harness.Loop` — pass the shape through.** `LoopRun.Assertions(...)` calls
   `SeriesEvaluation.Evaluate(assertionId, e.Signal, expected, frames, accounting)`. The new parameter is
   **trailing and optional**, so that call compiles and behaves identically untouched. To make the feature
   live it becomes `…, accounting, e.Shape)` — **one argument.** Nothing else in the runner changes: no
   new recording, no new register, no new poll.
2. **`Harness.Gate/SubmissionDocument.cs` — map the field** (§7 step 2), or gate 0b refuses every vector
   that uses it.
3. **`Stamped` instrumentation**, if the deferred shapes of §6c/§6e are ever wanted. That is a copy-layer
   and runner capability, not an evaluator one, and it is the honest home for every duration-quantified
   question.

**Nothing else is required.** The observation series already carries value, scan, poll round and window
state per frame, in order — every shape above is answerable from what is already recorded. *The data was
never the gap.*
