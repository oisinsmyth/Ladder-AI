# Four-rung spec pipeline — adversarial design validation

**Purpose.** The A→B→C→D spec pipeline (`gen-pid-analysis` → `gen-functional-analysis` →
`gen-equipment-spec` → `gen-code-structure`) was designed to prevent the failure class measured in
`docs/evidence/PlantAutoControl-bench-grading.md` and diagnosed in
`docs/evidence/PlantAutoControl-bench-autopsy.md`. This document tests it **against those four real
failures**, adversarially — the goal is to break it, not to confirm it.

**Method.** For each failure, walk the ground-truth condition through A→B→C→D **as the four
SKILL.md contracts actually specify** (rules quoted verbatim), and ask: at which rung, if any, does
this failure become *structurally* impossible? Where a survival path exists, name it. Ratings:
**PREVENTED** (structurally impossible), **CAUGHT** (still possible, but forced into the open as a
blocking `Q-nn` / ledger entry / hard fail), **SURVIVES** (could still ship silently).

**Run type.** Manual (skill-less) design validation performed inside `lad-coder` per CLAUDE.md hard
rule 8 — this is a read/reason pass over LAD/IR content and its governing skills; no IR was written,
so no compile gate applies and no `gen/<project>/telemetry.log` line was appended (this is not a
generation-stage run).

**Headline result: 0 PREVENTED, 2 CAUGHT (both conditional on preconditions that are currently
unmet), 2 SURVIVES.** The design closes the *notation* half of the autopsy's diagnosis and leaves
the *interpretation* half — the half that produced three of the four failures — substantially open.

| # | Failure | Rating | One-line reason |
|---|---|---|---|
| 1 | REQ-003 filter cascade hold (REGRESSION) | **CAUGHT** | A's one-relation-per-line + D2 ledger engage — but C §5's merge rule and D2's unverifiable-precondition hole are live survival paths |
| 2 | REQ-012 rotation-sensor bypass (DEFECT) | **CAUGHT (conditional)** | Only via C §1 "complete by construction from the class reference"; no `references/` file exists anywhere in the repo and nothing checks a reference for completeness |
| 3 | REQ-017 "not faulted" narrowed (DEFECT) | **SURVIVES** | Rung C's ban on interface members *forces* the binding onto the raw IO fault tag; the correct aggregated signal only becomes visible at D1, which has no rule to revisit it |
| 4 | REQ-019 feedback pairing swapped (DEFECT) | **SURVIVES** | The ledger is a coverage check, not a correctness check; both relations render, so a swapped 1:1 assignment passes every gate cleanly |

---

## Failure 1 — REQ-003, FilterUnitSystem cascade hold dropped (REGRESSION)

**Ground truth** (`PlantAutoControl-bench-grading.md` §2, REQ-003): the answer key holds each filter unit
running during controlled shutdown until **both** the global `PlantControl.FansShutdownReady` **and**
the specific dust-generating neighbour's `ShutdownComplete`; the generated block kept only the global
flag on nets 3/4/19. Register form: the inventory cell
`"Fans-shutdown-ready / Air-separator VSD shut down"` — one cell, two conditions, ambiguous `/`.

### Walkthrough

**Rung A.** The two conditions have *different provenance* under the new decomposition. The
neighbour term is a topology relation, derived by A §3 from the material-flow graph plus the stated
convention (`"controlled shutdown holds until the receiving machine reports complete"`). The
`FansShutdownReady` term is a plant-level flag — process intent, i.e. rung-B material. So A should
emit, for `DustFilter-1`:

```
  interlock : controlled shutdown holds until Air-Separator-VSD reports shutdown complete
```

The rule that acts is A's explicitness block: *"**One relation per line.** Never join two conditions
with an ambiguous separator (`/`, "and/or", a comma). Two conditions = two lines."* — and the rule
even names this exact regression as its motivating case.

**Survival path 1 (input-side parse).** The rule as written governs **A's own output notation**, not
A's **reading of an ambiguous source**. If a P&ID note or a restricted document contains
`"holds until fans ready / air separator down"`, an analyst who parses that as *one* condition
("fans ready, i.e. the air separator is down") writes one line and violates nothing. A §"Underspecified
= blocking `Q-nn`" would have to fire — but it fires on the analyst's subjective sense that the text
*is* underspecified, and an appositive reading of `/` feels perfectly specified. **This is precisely
the correlated-misreading mechanism the autopsy identified (§2-B), relocated one rung earlier.**

**Rung B.** `FansShutdownReady` appears here as a plant behaviour — e.g. `B-nn: on controlled
shutdown, filtration is held until the fans are ready to shut down`, `applies-to: dust filter 1,
dust filter 2, cyclone`. B §"Duplication with rung A is expected and fine — ... do not pre-merge or
drop either" is correct and helpful here.

**Rung C.** C §3 (*"Carry rung A's interlocks verbatim as `P-nn`, **fully enumerated**, one relation
per line"*) preserves the neighbour term; C §4 layers B's flag on. Two `P-nn` lines. Good.

**Survival path 2 — C §5's merge rule (the serious one).** C §5 reads: *"Where a rung-A relation and
a rung-B behaviour describe the same fact, merge them and cite both. Where they **contradict** ...
emit a **BLOCKING `Q-nn`**."* These two conditions do **not** contradict — both are true, both must
be satisfied to release the hold. But an analyst optimising for a clean spec can easily judge
"filtration holds until the fans are ready to shut down" (B) and "holds until the air separator
reports shutdown complete" (A) to be *the same fact* — the air separator **is** the fan-side machine.
C §5 then licenses collapsing them to one `P-nn` citing `[A][B]`. Once that happens:

- D2's ledger is **satisfied** — the single relation is rendered as a single term.
- D3's "every term traces to an undischarged relation" is **satisfied**.
- Nothing anywhere downstream can recover the lost term, because no artifact records that two ever
  existed.

**C §5 is a silent-merge licence sitting exactly where the regression lives.** The skill's own
warning — *"A silent merge here is a documented defect path"* — is attached only to the
*contradiction* branch, not to the *same-fact* branch, which is the branch that actually fires.

**Rung D.** Assuming the two `P-nn` survived C, D2 is the strongest mechanism in the whole pipeline:
*"Every spec relation (`C-nn`, `P-nn`) must be accounted for exactly once ... **A relation with
neither a term nor a ledger entry is a HARD FAIL.**"* The original regression was, in the autopsy's
words, an *undeclared* discharge; D2 makes undeclared discharge structurally impossible.

**Survival path 3 — a declared-but-unverifiable discharge.** D2 requires a discharge be *"explicit,
argued, and preconditioned"* — but **nothing checks the argument, and nothing constrains the
precondition to be verifiable.** The plausible false argument here is already written down in this
repo: grading §4-A floats *"`PlantControl.FansShutdownReady` (computed in another block, out of
scope) already encodes the neighbour `ShutdownComplete`."* A coder can write:

```
P4  hold until Air-Separator-VSD shutdown complete
    -> discharged by shape "cascade-hold-via-plant-ready-flag"
       argument: FansShutdownReady is asserted only once all fan-side machines report complete
       precondition: FansShutdownReady aggregates AirStarInst1.Outputs.ShutdownComplete
```

That precondition is **unverifiable within scope** (`FansShutdownReady` is written by another block,
outside this generation's boundary) — and D2 has no rule rejecting an unverifiable precondition. The
result is the same dropped term with a paper trail. That is genuinely better than silence (a reader
*can* see it), which is why this rates CAUGHT rather than SURVIVES — but note there is **no assigned
reader**: the four rungs end at D with a gate-1 engineer sign-off, and no reviewer skill consumes
`code-structure.md` (see Weakness W7).

### Verdict — **CAUGHT**

Mechanism: A's one-relation-per-line kills the `/` in the artifact; C §3's verbatim full enumeration
carries it; D2's hard fail makes an *undeclared* drop impossible. Not PREVENTED, because of three
live paths: (1) the `/` rule constrains A's writing, not A's reading; (2) C §5 licenses a same-fact
merge of two non-contradicting conditions; (3) D2 accepts a declared discharge on an unverifiable
precondition.

---

## Failure 2 — REQ-012, discharge-VSD rotation-sensor bypass clause dropped (DEFECT)

**Ground truth** (grading §2, REQ-012): the answer key drives `MotorVSDInst1.IO.RotationSensor :=
NOT HMIControlSignals.BypassAirStarDCRotSen` and S/R-latches `RunningFwdFB` gated on `NOT Bypass...`;
the generated block wrote `RunningFwdFB := DiscreteInputs.AirStarDCRotSen` unconditionally, never
referenced the bypass tag, and dropped the `RotationSensor` interface coil. The register stated the
clause **explicitly**. This is the pipeline's own "delta" case: an instance that is its class **plus**
something.

### Walkthrough

**Rung A — the delta has a recording mechanism but no discovery mechanism.** A §4 is emphatic:
*"**Record deltas.** ... Deltas are FIRST-CLASS: a named field per instance, never buried in prose.
*Outliers are where guards get lost — an unrecorded delta is the failure mode this field exists to
prevent.*"* But look at what rung A is allowed to see. Its declared inputs are **the P&ID / plant
layout** and **the class references** — and its abstraction rule bans signals outright: *"No signals,
no IO addresses, no tag names, no booleans."* The bypass is an HMI/operator signal
(`HMIControlSignals.BypassAirStarDCRotSen : Bool RETAIN`, grep-verified in
`ir/PlantAutoControl-bench/HMIControlSignals.ir` L28). **A P&ID does not show operator bypass flags, and the
class reference by definition does not carry the instance's deltas.** So `deltas : none` at rung A is
honestly written, unfalsifiable, and wrong — and it is the *default* value in the skill's own output
template.

**This is a genuine gap between "the IO table has a bypass signal" and "the spec records a delta":
the rung that owns the delta field cannot see the IO table, and the rung that sees the IO table
(C) has no rule that turns an unclaimed signal into a delta.**

**Rung B.** A functional description may or may not mention bypasses. If it does, B §2 captures it
and scopes it. If it doesn't (the autopsy's "explicit in the register" was a reverse-derivation
artifact; a real functional description often omits commissioning bypasses), B contributes nothing.
No rule in B hunts for missing behaviour.

**Rung C — the one real catch, and it is conditional.** C §1: *"For each instance from rung A, start
from its class reference and instantiate the **complete** standard requirement set (`C1…Cn`). A
standard requirement is never omitted — if it does not apply to this instance, that is a recorded
**delta**, not a silent absence."* If the `vsd-motor` (or belt-conveyor) class reference carries
*"motion confirmation from a rotation sensor, individually bypassable by the operator"* as a standard
requirement, then C instantiates it for this instance, C §2 binds
`HMIControlSignals.BypassAirStarDCRotSen` from the IO table, and D2's hard fail then forces the term
or an argued discharge. **That chain works.** It is the "completeness by construction" claim, and on
this failure it is load-bearing.

**Why the condition is currently unmet.** `find . -iname "reference.md"` over the repo returns
**nothing**. No `references/` directory exists. Neither A nor C defines the reference format, who
authors it, what "complete" means for one, or any check that a reference is complete. A thin
reference produces thin completeness **silently** — the instantiated set looks complete because it
matches the reference, and no artifact records what the reference omitted. Rung A's stop condition
(*"an equipment class with no reference → a blocking `Q-nn`, never an improvised requirement set"*)
is a good fail-safe and means the pipeline **cannot run at all today**; the practical pressure that
creates is to improvise a reference, which is exactly the silent-thinning path.

**Rung C — no signals→requirements reverse pass.** C's only stop condition on IO is the *absence*
direction: *"a requirement needing a signal the IO table does not contain → the tag is `proposed`;
record it as a **blocking `Q-nn`**."* There is no rule in the opposite direction — an IO point that
exists and that **no requirement claims** passes unnoticed. `BypassAirStarDCRotSen` is exactly such a
point. Compare `review-functional`, whose Pass 2 exists precisely because *"Skipping Pass 2 is how
gold-plating and dead wiring survive"* — rung C has a Pass 1 and no Pass 2.

**Rung D — a free mechanical catch the skill does not take.** D1 says *"Read the full IR of any block
you propose to reuse; its real interface is ground truth."* `ir/PlantAutoControl-bench/MotorVSDSystem.ir` L56
declares `RotationSensor : Bool` as an FB interface member. The generated block left it **undriven**.
D1's coverage table runs one way only (`C-nn` → satisfied inside the FB / wired by orchestration);
there is no requirement to enumerate the chosen FB's *inputs* and account for each as driven or
deliberately defaulted. Such an audit is purely mechanical, needs no judgment, and would have flagged
REQ-012 deterministically. D3's converse rule already exists and is good (*"a term tracing to nothing
is a finding"*) — the missing half is the undriven-input side.

### Verdict — **CAUGHT (conditional)**

Mechanism: rung C §1's complete-by-construction instantiation from the class reference, backstopped
by D2's hard fail. **The condition — a class reference that actually carries the bypassable-rotation-
sensor requirement — is unverified by any rule and unmet by any file in this repo.** If the reference
omits it, the failure **SURVIVES** untouched: A's `deltas : none` is honest, C never binds the tag, D
never has a relation to discharge, and the block ships with an unreferenced bypass tag exactly as
before.

---

## Failure 3 — REQ-017, "not faulted" narrowed from `FaultActive` to `TomraComFlt` (DEFECT)

**Ground truth** (grading §2, REQ-017): answer key gates the sorter-feed conveyor on
`NOT TomraControlInst1.Outputs.FaultActive` (the FB's aggregated fault); the generated block gated on
raw `NOT DiscreteInputs.TomraComFlt` (comms only). Grep-confirmed in
`ir/PlantAutoControl-bench/TomraControlSystem.ir` L205: `Outputs.FaultActive := Outputs.FTR OR Outputs.FTS OR
LostLifeBit OR StatusWord0.%X2 OR ...` — thirteen OR'd sources, strictly broader than the comms path.
Register form: **underspecified** — it said only "not faulted".

### Walkthrough

**Rung A.** Process language, correctly: `interlock : Sorter-Conveyor-VSD may start only while
Optical-Sorter is not faulted`. This is the right abstraction and A is right not to name a signal.
No failure here, and no possibility of one — A cannot get the signal wrong because A has no signals.

**Rung B.** Same statement as plant intent if the functional description says it. No signal.

**Rung C — the rung boundary actively causes the defect.** C is where the phrase must acquire a
signal (C §2: *"**Bind IO**: attach the real signal to each requirement that needs one"*). Now read
C's abstraction rule: *"**Banned here:** `AND` / `OR` / `NOT` expressions, **interface members**
(`.UPSEnable`, `.Run`), block names, network structure. Those are rung D."*

The correct binding **is** an interface member — `TomraControlInst1.Outputs.FaultActive`. Rung C is
contractually forbidden from writing it. The only artifact C is allowed to consult for signals is
**the IO table**, and the only fault-shaped entry there is `DiscreteInputs.TomraComFlt`. So a rung-C
analyst following the contract to the letter writes:

```
  P-nn  start permitted only while the optical sorter is not faulted   [A]
        not faulted <- DiscreteInputs.TomraComFlt                      [io]
```

…and has produced the defect **while fully compliant**. `converter tagstatus` confirms the tag
`exists`. Nothing objects.

**This is a rung-ordering inversion, and it is the pipeline's sharpest structural flaw:** the decision
"which signal means *not faulted*" is forced at rung C, but the information needed to make it
correctly — the reused FB's exposed status outputs — is declared ground truth only at rung D1.

**Rung D.** Two things could in principle catch it; neither is a rule.

- D1 (*"Read the full IR of any block you propose to reuse; its real interface is ground truth"*)
  puts `Outputs.FaultActive` in front of the coder's eyes. But D1's job is stated as **coverage**
  (each `C-nn` → satisfied inside the FB / wired by orchestration). There is no instruction to
  re-examine a rung-C binding against what D1 just learned, and no permitted back-edge to C.
- D's calibration: *"where forced to choose on a **safety interlock**, prefer the **stronger/fail-safe**
  guard and record the choice (FI-34)."* Two problems. First, D is **not** "forced to choose" — C
  already chose, and D's job is to render C. Second, the word **"safety interlock"** is doing damage
  in a repo where hard rule 2 defines *safety* as F-content that must be refused outright; a strict
  reader excludes a process interlock like this one from FI-34's scope entirely.

The autopsy's own prescribed fix for this exact failure — *"Add a **'strongest-available-guard'
check**: when a REQ says 'not faulted'/'not running' and the wired FB exposes both a narrow and a
broad signal, flag use of the narrower one (REQ-017)"* — was aimed at `review-functional` and **was
not carried into any of the four rungs.** Grepping the four SKILL.md files for `strongest`, `narrower`
or `candidate signal` returns nothing.

D2's ledger does not help: the relation **is** rendered, as one explicit term, tagged with its
relation id. Coverage is perfect. The ledger has no opinion about whether the rendered term is the
*right* term.

### Verdict — **SURVIVES**

There is no rung at which this becomes impossible, and rung C's abstraction rule makes the wrong
binding the *compliant* one. It ships silently, exactly as it did.

---

## Failure 4 — REQ-019, dust-filter `RemoteOp`/`RunningFB` pairing swapped (DEFECT)

**Ground truth** (grading §2, REQ-019): answer key maps `FilterUnit1Op → IO.RunningFB` and
`FilterUnit1Ready → RemoteOp`; the generated block mapped them the other way round. Register form:
**underspecified** — three feedbacks named, pairing not pinned. Functional consequence: `RunningFB`
seeds the fan up-to-speed timing (REQ-020 → `UPSEnable`), so the swap starts the 10 s timer from
*Ready*, before the fan physically runs, enabling downstream equipment early.

### Walkthrough

**Rung A.** Says nothing — a feedback pairing is not a topology relation. Correct by design, no
failure possible, and no help.

**Rung B.** At most `B-nn: each filter unit reports remote-operational, running and fault status`,
`applies-to: dust filter 1, 2, cyclone`. Which physical signal is which is not plant intent. B §5's
quantitative-parameter rule doesn't reach it. No help.

**Rung C — where the failure is born, and where nothing catches it.** C §1 instantiates two distinct
class requirements — "running confirmed from field feedback", "remote/auto-operational confirmed from
field feedback". C §2 binds them. The IO table offers (grep-verified,
`ir/PlantAutoControl-bench/Input.ir` L78–80):

```
    FilterUnit1Flt   : Bool
    FilterUnit1Op    : Bool
    FilterUnit1Ready : Bool
```

`Flt` is unambiguous. `Op` and `Ready` are both plausible for both remaining requirements — the
grading itself records the ambiguity (*"'Op' reads as *operational* → the generated `RemoteOp`
pairing, **or** as *operating/running* → the answer key's `RunningFB` pairing"*). Now audit C's stop
conditions against this situation:

- *"a requirement needing a signal the IO table does not contain → blocking `Q-nn`"* — **does not
  fire**; both signals exist.
- `converter tagstatus` — **does not fire**; both are `exists`. Tag-status checks existence, never
  meaning.
- C §5's reconciliation — **does not fire**; there is no A-vs-B contradiction, because neither A nor B
  said anything.
- C §7's provenance rule (`[io]` on every line) — **does not fire**; writing `[io]` next to a guess
  does not mark it as a guess.

**Nothing in rung C distinguishes "I bound this from documentation" from "I bound this because the
name sounded right."** The ambiguity here is of *assignment*, not of *absence*, and rung C's entire
stop-condition vocabulary is built around absence.

**Rung D — the ledger is a coverage check, not a correctness check.** Both relations are rendered:

```
  .RemoteOp   := DiscreteInputs.FilterUnit1Op      [C-n]
  .RunningFB  := DiscreteInputs.FilterUnit1Ready   [C-m]
```

D2 is satisfied: every relation accounted for exactly once. D3 is satisfied: every term traces to an
undischarged relation, none traces to nothing. A **swapped bijection is invisible to a coverage
ledger by construction** — the set of relations and the set of terms both have the right cardinality
and the right members.

**The disambiguating evidence exists and no rung is told to look at it.** `ir/PlantAutoControl-bench/FilterUnitSystem.ir`
shows what the FB *does* with each member: `RunningFB` drives the up-to-speed enable timer
(L145: `TON(EnableUpstreamTimer, IN := ... AND IO.Run AND IO.RunningFB, ...)`) and the fault-trip
timer (L133: `TON(FaultTripTimer, IN := IO.Run AND NOT IO.RunningFB, ...)`) — i.e. it means *"the
machine physically started after we commanded it"*. `RemoteOp` gates `TryRunMotor` (L93) and raises an
alarm when false (L166: `COIL IO.Alarm.%X2... IO.Alarm.%X3 := NOT RemoteOp`) — i.e. *"the unit is in
remote and available"*. That reading turns a coin-flip into an answerable question ("which of `Op` /
`Ready` goes true only once the fan is physically turning?") — the kind of question that is worth
raising as a blocking `Q-nn` to the engineer. D1 has this IR open in front of it and no instruction
to use it this way.

### Verdict — **SURVIVES**

No rung makes it impossible, and no rung makes it visible. This is the cleanest silent-ship path in
the pipeline: an underspecified 1:1 assignment between two same-shaped requirements and two
ambiguously-named signals, passing every gate with full marks.

---

## Weaknesses (from the stress test)

**W1 — `deltas: none` has a recording mechanism, no discovery mechanism.** Rung A owns the delta
field but cannot see the IO table or HMI signals (its inputs are the layout and the class references;
its abstraction rule bans tag names). A P&ID does not show a commissioning bypass. So `deltas: none`
is honest, unfalsifiable, and the template default. *(Failure 2.)*

**W2 — rung C binds requirements→signals and never signals→requirements.** C's only IO stop condition
is the absence direction (a requirement with no signal). An IO point that exists and that no
requirement claims — `BypassAirStarDCRotSen` — passes unremarked. `review-functional` has a Pass 2 for
exactly this reason; rung C does not. *(Failure 2.)*

**W3 — "completeness by construction" rests on an artifact class that does not exist.** No
`references/<class>/reference.md` exists anywhere in the repo. Neither A nor C defines the format,
the author, what "complete" means, or any check that a reference is complete. A thin reference yields
thin completeness **silently**, and no artifact records what the reference omitted. Rung A's
"no reference → blocking Q" stop is a correct fail-safe, but it means the pipeline cannot run today,
which creates pressure to improvise references — the exact silent-thinning path. *(Failure 2; the
single largest unverified assumption in the design.)*

**W4 — a declared-but-wrong discharge passes D2.** D2 requires "explicit, argued, and preconditioned"
but nothing checks the argument and nothing constrains the precondition to be verifiable in scope.
The false-but-plausible argument for failure 1 already exists in writing (grading §4-A:
`FansShutdownReady` may subsume the neighbour flag) and rests on a block outside the generation's
boundary. Worse, the *same agent* writes the relation, drops it, and argues the drop — the autopsy's
cause-1 correlation, reproduced inside the ledger. *(Failure 1.)*

**W5 — nothing detects underspecification; the rungs only say what to do once you have.** All three
upper rungs carry a "underspecified → blocking `Q-nn`" rule, but the *trigger* is the analyst's
subjective sense that something is underspecified. "Not faulted" and "running feedback" read as fully
specified right up until you know the candidate set has two members. The detectable, mechanical
signature — **more than one available signal could satisfy this phrase** — is never computed by any
rung, because computing it needs the IO table (C has it) *and* the FB's exposed members (only D has
them). *(Failures 3 and 4 — the two SURVIVES.)*

**W6 — rung-boundary inversion: C must choose signals it cannot see.** C is required to bind every
requirement to a signal, is forbidden from naming interface members, and runs before D1 establishes
the reused FB's interface as ground truth. Where the correct signal is an FB output (`Outputs.FaultActive`)
rather than an IO point, **C's contract makes the wrong binding the compliant one**, and D has no
sanctioned back-edge to revise it. *(Failure 3.)*

**W7 — no one reads the ledger.** The four rungs terminate at D's gate-1 engineer sign-off. No
reviewer skill consumes `code-structure.md`, and `audit-artifact` (docs/15 skill #14, the natural
ledger auditor) is not built. D2's discharge arguments — the design's main defence against failure 1
— currently have no assigned adversarial reader.

**W8 — the four rungs are not wired into the pipeline, and they break the Check phase.**
`grep` over `docs/` and `.claude/skills/` finds **no** reference to `equipment-topology.md`,
`plant-behaviours.md`, `equipment-specs/` or `code-structure.md` outside the four SKILL.md files
themselves. Concretely: (a) docs/15's build-order table still lists `gen-pid-analysis` as *"Not
built"* producing `process-topology.md`, and B/C/D appear nowhere in its 15-skill table; (b)
`gen-block-new`'s stop condition is *"no architecture artifact, or the gate-1 sign-off line not
signed → stop"* and it reads `architecture.md`, which rung D does not produce; (c) **`review-functional`'s
stop condition is "if no register exists for the project, STOP" and it hard-requires
`gen/<project>/requirements.md`** — which the four-rung pipeline never produces. So a project run
through A→B→C→D cannot be functionally reviewed at all, or must have a `requirements.md` improvised
for the reviewer, re-introducing the very ambiguity surface the rungs removed.

**W9 — the `/` rule is an output-formatting rule solving an input-parsing failure.** A's
*"Never join two conditions with an ambiguous separator"* constrains what A writes. The autopsy's
regression was caused by how the `/` was **read**. An analyst who parses `"A / B"` as one condition
writes one line and breaks no rule. *(Failure 1.)*

**W10 — C §5's "same fact" merge branch is unguarded.** The rule's warning (*"A silent merge here is
a documented defect path"*) is attached to the *contradiction* branch. The branch that actually fires
on failure 1 is the *same-fact* branch, which explicitly licenses collapsing an A relation and a B
behaviour into one line — destroying the evidence that two conditions existed, before D2 can see
them. *(Failure 1.)*

### What the design gets right (recorded for balance)

- **D2's hard fail on an unaccounted relation** genuinely eliminates the *undeclared* discharge —
  the autopsy's headline mechanism. It converts silence into either a term or an argument.
- **D3's reverse rule** (*"a term tracing to nothing is a finding"*) would have caught the
  gold-plating the grading found in its reverse pass (unrequested running feedbacks on nets 1/5/10).
- **A's per-instance expansion** with grouping deferred to D1 is the right ordering: it makes the FB
  grouping *derived* rather than assumed, which is what allows a delta to be visible as an outlier at
  all.
- **Rungs A and B cannot commit failures 3 or 4** — their signal ban means they cannot get a signal
  wrong. The abstraction discipline is doing real work; the problem is what happens at the seam.

---

## Recommended skill amendments

Concrete, quotable edits. Not applied — recommendations only.

### R1 (highest leverage) — add a **candidate-set / binding-audit** step. `gen-equipment-spec` §2 and `gen-code-structure` §D1.

Kills the two SURVIVES (failures 3 and 4) and gives W5 a mechanical trigger.

Add to `gen-equipment-spec` **Method §2**:

> 2. **Bind IO — with a candidate set.** For each requirement needing a signal, list **every**
>    signal in scope whose name, type or role could plausibly satisfy the phrase — the IO table
>    **and** the exposed status members of the block class the instance will use (read-only, for
>    enumeration; you still may not write an interface member into a requirement line). If the
>    candidate set has **more than one member**, the binding is a **BLOCKING `Q-nn`** unless a
>    documentary basis is cited for the choice. *"Not faulted" satisfied by both a raw comms-fault
>    input and an aggregated FB fault output, and a "running"/"remote-operational" pair facing two
>    ambiguously-named feedbacks, are the two documented cases
>    (`docs/evidence/PlantAutoControl-bench-autopsy.md` §2-C).* **Two requirements competing for two
>    same-shaped signals is always a candidate-set of two, never a coin flip.**

And to `gen-code-structure` **D1**, a new closing paragraph:

> **Binding audit (mandatory).** For every rung-C binding, check it against the FB interface you have
> just read. If the FB exposes a signal that satisfies the relation **more broadly or more strongly**
> than C's binding (an aggregated `FaultActive` vs a raw comms fault; a confirmed-running member vs a
> ready member), you may **neither** silently keep C's binding **nor** silently re-bind: record a
> `rebind` ledger entry naming both candidates, state what the FB does with each member, and prefer
> the **stronger/broader guard**. Rung C had to choose a signal before this interface was ground
> truth; this is the sanctioned back-edge.

### R2 — close the ledger's argument hole. `gen-code-structure` §D2.

Kills W4, and converts failure 1 from CAUGHT-with-paper-trail to effectively PREVENTED.

Replace *"**A discharge must be explicit, argued, and preconditioned.**"* with:

> **A discharge must be explicit, argued, and preconditioned — and its precondition must be
> VERIFIABLE FROM ARTIFACTS IN SCOPE.** Classify every precondition as one of:
> **`verified-in-block`** (cite the network), **`verified-cross-block`** (cite the block, file and
> line in `ir/<project>/`), or **`unverifiable`**. **An `unverifiable` precondition is not a valid
> discharge — render the relation as a term instead.** *"The plant flag probably already encodes the
> neighbour's shutdown-complete" is exactly such an argument: the flag is computed in another block,
> outside this generation's boundary, and the real regression it would have licensed is recorded in
> `docs/evidence/PlantAutoControl-bench-grading.md` §4-A. Plausibility is not verification.*

Add to the same section:

> **Emit the ledger as a machine-checkable table** — one row per relation:
> `relation-id | disposition (rendered / in-FB / discharged) | evidence | precondition class`.
> The relation-id column must set-difference to **empty** against the union of all `C-nn`/`P-nn` ids
> in `gen/<project>/equipment-specs/`. State the two counts explicitly in the artifact.

### R3 — give the delta field a discovery mechanism. `gen-pid-analysis` §4 and `gen-equipment-spec` (new §8).

Kills W1 and W2; makes failure 2 unconditional rather than reference-dependent.

In `gen-pid-analysis` **Method §4**, replace the bare `deltas : none` convention:

> `deltas : none` is **never** written bare. Write `deltas : none-found — searched: <sources>`,
> naming what you actually checked (layout notes, class reference §, equipment schedule). *A bare
> `none` records an absence of looking, not an absence of deltas — and rung A cannot see the IO
> table, so a delta carried only by an operator/bypass signal is invisible here by construction and
> must be re-hunted at rung C (`gen-equipment-spec` §8).*

Add `gen-equipment-spec` **Method §8 — unclaimed-signal sweep (the Pass-2 of this rung)**:

> 8. **Sweep the IO table in the opposite direction.** Every signal in the IO table scoped to this
>    instance is either **bound** to a requirement above, or listed under `UNCLAIMED` with a reason.
>    An unclaimed signal whose name implies a control function — `Bypass*`, `*Select`, `*Inhibit`,
>    `*Enable`, `*Override` — is a **BLOCKING `Q-nn`** and a candidate **delta**, never a silent
>    omission. *`HMIControlSignals.BypassAirStarDCRotSen` sat unclaimed and unreferenced through a
>    whole generation run (`docs/evidence/PlantAutoControl-bench-grading.md`, REQ-012); binding
>    requirements to signals without ever sweeping signals for requirements is what let it.*

Paired mechanical backstop in `gen-code-structure` **D1**:

> **Undriven-input audit.** Enumerate the chosen FB's input/interface members and mark each
> **driven** (cite the D3 term) or **defaulted** (state why). An undriven input whose name matches an
> unclaimed IO signal is a HARD FAIL. *`MotorVSDSystem.ir` declares `IO.RotationSensor : Bool`; a
> generation left it undriven while its bypass tag sat unreferenced.*

### R4 — make the `/` rule bite on the input side. `gen-pid-analysis`, Explicitness rules.

Kills W9. Extend the first bullet:

> - **One relation per line — on the way in as well as the way out.** Never join two conditions with
>   an ambiguous separator (`/`, "and/or", a comma) in what you write; and **never resolve one by
>   reading** in what you consume. An ambiguous separator in a SOURCE is **split into two relations,
>   both retained** (the default), or raised as a blocking `Q-nn`. **Dropping either side always
>   requires the `Q-nn`** — an appositive reading ("A, i.e. B") is a resolution and is never taken
>   silently. *A `/` joining two hold-conditions, read as one, is the documented cause of a real
>   dropped-interlock regression (`docs/evidence/PlantAutoControl-bench-autopsy.md`).*

### R5 — guard C §5's same-fact merge branch. `gen-equipment-spec` §5.

Kills W10. Append to §5:

> **A merge is permitted only when both lines resolve to the same signal or the same named plant
> condition.** Two conditions that are both true, both must hold, and resolve to **different**
> signals are **two `P-nn` lines**, always — however closely related their process meaning. Reducing
> two such conditions to one is a **discharge**, and discharges belong to rung D's ledger where they
> must be argued, never to this rung's merge. *A plant-level "fans ready to shut down" flag and a
> specific neighbour's "shutdown complete" are not the same fact; collapsing them here destroys the
> evidence D2 needs.*

### R6 — repair the handoffs. `gen-code-structure` output section, plus `docs/15-generation-pipeline.md`.

Kills W8. Rung D's artifact must satisfy the contracts of what comes next, or nothing comes next:
`gen-block-new` requires a gate-1-signed `architecture.md`, and `review-functional` STOPs without
`gen/<project>/requirements.md`. Either (a) rung D also emits/updates `architecture.md` and rung C
emits a `requirements.md`-shaped register derived from the `C-nn`/`P-nn` sets, or (b) those two
skills' input contracts are amended to accept `code-structure.md` / `equipment-specs/` explicitly.
**Option (a) is preferable** — the `P-nn` set is a strictly better trace target than prose REQs, and
it hands `review-functional` the per-instance interlock list the autopsy asked for. Either way,
docs/15's skill table needs A–D added with their real artifact names (`equipment-topology.md`, not
`process-topology.md`) and their gates.

### R7 — assign a reader to the ledger.

Kills W7. Either build `audit-artifact` (docs/15 #14) with `code-structure.md` §D2 as its first
corpus, or add to `review-functional`'s inputs: *"`gen/<project>/code-structure.md`'s discharge
ledger, where one exists — every `discharged` row is a claim to be independently re-derived from the
IR, never accepted as stated."* **In fresh context**, per the autopsy's cause-1: the agent that wrote
the discharge cannot be the agent that clears it.

### R8 — fix FI-34's scope word. `gen-code-structure`, Calibration.

Replace *"where forced to choose on a **safety interlock**"* with *"where forced to choose on **any
protective term — interlock, permissive, inhibit or fault gate**"*. In this repo "safety" means
F-content that must be refused outright (hard rule 2), so the current wording reads as excluding
every process interlock — including the one it was written for.

---

## Bottom line

The design decisively fixes the failure mode it was aimed at: **an undeclared drop** is now a hard
fail (D2), and **ambiguous notation in the pipeline's own artifacts** is now prohibited (A). Those
were the autopsy's causes 3 and 4.

It does **not** fix the autopsy's cause 1, the one it ranked highest leverage — *"the reviewer is not
independent of the spec"*. The four rungs move the interpretation earlier and split it across more
artifacts, but at each seam the same single reader still resolves ambiguity alone and unchallenged:
rung A parses the `/`, rung C picks between two candidate signals, rung D argues its own discharges.
The two failures that ship silently through the new pipeline (REQ-017, REQ-019) are both
**underspecification absorbed as inference** — the autopsy's §2-C — and the design added no mechanism
that computes a candidate set, so nothing ever notices there was a choice to make. R1, R2 and R3 are
the three amendments that would close it.
