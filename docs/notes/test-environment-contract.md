# THE TEST-ENVIRONMENT CONTRACT — what a block must satisfy to be testable

**Status: DRAFT, 2026-08-13. Phase 5.1.** This is the contract half only. **The skill that enforces
it (`.claude/skills/`) is deliberately not written here** — that frontmatter is strict YAML with two
silent failure modes, and it is being done separately and deliberately. §10 below says what the skill
would have to enforce.

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
| 3 | **`Basis` — assertion** | ✅ existence + coverage | assertion ID must come from the **spec-derived enumeration** (§7/D35), never free text |
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

```
Vector
  Id                  stable identifier, unique in the submission
  Slot                which slot (methodology) this vector is an index of
  Index               its position in the slot's column (D26a)
  Author              agent identity. Checked against the block's author (D6)
  Basis               { Clause, Assertion }        -- §3, both required
  Inputs              tag name -> value            -- what is written before T=0
  StartBool           the slot's start bool        -- §6
  Expectations        [ { tag, predicate, Observability, Settling } ]   -- §4, §5
  MaxDuration         in SCANS, with a wall-clock backstop (X-B, §12a derivation 4)
  Blacklist           [ { block, reason } ]        -- §7, add-only
  CompressionFactor   the `comp` this vector's scan counts are stated at  -- §4.4
```

**`MaxDuration` is not optional and is not a formality.** X-B makes it double as the per-test timeout,
and DB-13 needs it to compute wave length, so a vector without one cannot be packed *or* bounded. Its
wall-clock backstop is **computed, not guessed** — the formula is §12a derivation 4, and it includes
an outlier allowance that must not be trimmed.

**Values are engineering values, not register contents.** Byte and word order are the transport's
problem (`modbus-tcp-client-spec.md` §5), and the version register plus a round-trip calibration are
what make that safe.

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
Settling
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

**9.1 — There is no route from "unobservable" back to "testable", and this is the big one.**
§2.6 says the runner refuses the vector. It does not say what the author then *does*. The declaration
is frozen for the wave set and must precede the generating download, so an author who discovers
mid-wave that they need a latch waits a **full download boundary** to get one. Worse, the obvious
fix — *add a status output to the block so its behaviour is visible* — collides with D13/§2.1, which
put instrumentation in the copy layer and say `lad-coder` never writes observability code.
***SO: MAY AN AUTHOR CHANGE A BLOCK'S INTERFACE PURELY TO MAKE IT TESTABLE? *** That is the central
question of a document called *design for testability*, and the design currently answers both ways.
**Owner's.**

**9.2 — "Assertion" is not defined in §2.6.** D35 counts per assertion and §7 requires the enumeration
be spec-derived, but §2.6 predates both and an author reading only §2.6 will write prose. This
contract requires an ID from the enumeration (§3); **that requirement is new here and should be
confirmed.**

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
| **liveness** *(post-run)* | stimulus check present; counter advanced by the expected amount; manifest presence | verdict `STALE`, never `PASS` |

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
