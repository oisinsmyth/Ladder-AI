# test-project001 — Hx Corpus — Requirements Register

Requirements register for the **Hx corpus**: four deliberately trivial blocks written as *material to
hammer the tooling with*, so that the campaign in `docs/notes/tooling-hammer-plan.md` never has to
re-run the real hopper block. That block is now evidence — seven predicted divergences and a static
FAIL — and re-running it would change what those results mean.

Scope is **these four blocks only**. Nothing here restates or amends the whole-project register
(`gen/test-project001/requirements.md`) or the hopper-blockage register
(`gen/test-project001/hopper-blockage-alarm/requirements.md`), and no clause here is a plant
requirement. These blocks are not called by the plant program and describe no plant equipment.

## Provenance

- **Produced:** 2026-08-14, by the `lad-coder` sub-agent, **manually** — no skill covers the
  "author artificial test material" stage, and per CLAUDE.md hard rule 8 the absence of a skill is
  not a reason to do it anywhere else. Held to the same contract a skill would have followed.
- **Source:** the campaign lane brief and `docs/notes/tooling-hammer-plan.md` §1–§3. There is no
  restricted document and no plant behaviour behind any clause. Where a clause states a behaviour, the
  behaviour was **chosen** to exercise a named path through the tooling, and the clause says which.
- **Data boundary:** Green tier throughout. Nothing here derives from a live run, a restricted
  document, or the sanitization map.
- **Tag grounding:** these blocks reference **no PLC tag, no global DB member and no other block's
  data at all** — every signal named below is a member of the block's own instance DB, declared in
  the same commit. Hard rule 3 is therefore satisfied without a `proposed` entry: nothing is
  invented because nothing external is referenced. See "Isolation" below for the evidence.

## What this register deliberately does NOT contain

**No assertions and no vectors.** The spec-derived assertion enumeration is a *third party's* work
(`enumerate-assertions`, run inside the `assertion-enumerator` agent), and the vectors are a fourth
party's. D6 is lost at the denominator the moment the block's author supplies both: an enumeration
derived from the same head that chose the logic is a correlated check, and it will agree with the
implementation for reasons that have nothing to do with the requirement.

So this file stops at clause text. It states **what each block must do**, in sentences intended to be
decomposable into independently falsifiable assertions by somebody who has not read the IR — and it
does not state how many assertions that is, nor what any of them should be.

## Format

Follows the whole-project register's contract (its "Format" section is the defining instance), with
the same conventions the hopper register adopted:

- **IDs:** `REQ-HXE-nnn` (Bool echo), `REQ-HXD-nnn` (dwell timer), `REQ-HXS-nnn` (Int sum),
  `REQ-HXL-nnn` (seal latch) — four disjoint prefixes so that four independently-driven blocks never
  share an ID namespace, in the same way they never share a signal. Assigned in source order,
  **stable forever**, never renumbered, never reused.
- **Classes:** `control`, `timing`, `alarm`, `HMI`, `mode`, `out-of-scope` — same meanings as the
  whole-project register. Every clause here is `control` or `timing`; there is no plant to alarm.
- **Numeric values are carried BESIDE a clause, never welded into its sentence.** See "Commanded
  values and their domains" below. This corpus takes that discipline further than the hopper register
  had to: **every number these blocks act on is commanded per run by the test vector, and not one is
  a constant in the block.**
- 🔴 **A bare parameter phrase means the value the vector COMMANDED, never a value the block
  chose.** "The commanded preset" means the value written into `DwellPreset` for that run.
  A block cannot satisfy any clause here by defining its own terms, because it holds no terms of its
  own to define — the presets, thresholds and operands are all inputs. This is the same rule as the
  hopper register's AR-HBA-14, arrived at structurally instead of by wording: *a requirement that
  references the implementation's own parameter is vacuous*, so this corpus gives the implementation
  no parameter to reference.
- **Cite a clause by what it claims, never by an ordinal.**

## Isolation — the property the whole campaign rests on

*** SLOTS THAT SHARE AN FB INSTANCE, A STIMULUS SURFACE OR A RESET ARE MUTUALLY CONFLICTING UNDER
D9, SO THE COLOURING SEPARATES EVERY PAIR AND A "FOUR-SLOT WAVE" WOULD RUN AS FOUR WAVES OF ONE. ***

These four blocks are therefore **independently drivable by construction**:

- Each is a separate FB with **its own single instance DB**. Every signal each block reads or writes
  is a member of that DB and of nothing else.
- Each has **its own command inputs, its own reset input and its own outputs**. No signal appears in
  two blocks. No block reads another's outputs. No block writes anything either of the others reads.
- No block references a PLC tag, a global DB, a UDT, or any block outside itself.
- Member **leaf names are distinct between the four blocks**, deliberately: `converter cross-check`
  carries a live defect by which FB-internal paths are unqualified, so two members sharing a leaf
  name can alias across instances and report as a cross-block multi-writer that does not exist. The
  naming means a reader of that report is not reading a false finding. The only leaf names shared
  with anything in the project are `PT`/`ET`/`IN`/`Q`, which every IEC timer instance declares and
  which are only ever reached qualified by `DwellTimer` — a name nothing else in the project uses.

Evidence recorded in the lane report, all mechanical: pairwise-empty intersection of the four blocks'
reference sets taken from the generated SimaticML; `cross-check` reporting no cross-block fact for
any of the four; and a before/after `cross-check` diff in which every pre-existing line is
byte-identical.

**What the isolation does NOT cover, stated so nobody relies on it for more than it says:** the four
blocks still share the **CPU**, the **scan**, the **mirror address space** and the **Portal token**.
Isolation here is a claim about *signals*, which is what D9's colouring is computed from. It is not a
claim that four concurrent slots cannot interfere through any mechanism whatsoever — if a wave of
four still serialises, the reference sets are not the place to look.

## Observability — every signal must survive the copy layer

Checked against `docs/notes/test-environment-contract.md` §2.7, §4 and §4.5 on 2026-08-14 via
`/design-for-testability`. **No vector is being submitted and no gate was run** — what follows is the
block author's half of the declaration: the facts about these blocks that a submission's `map` and
`deployment` blocks need, and which nobody but the block author can state.

Every signal named in every clause below is one of the three types the mirror carries (`Harness.Map`'s
`MirrorValueType`): `Bool` (one register, bit 0, copied by a `COIL`), `Int` (one register, copied by a
`MOVE`), `Time` (**two** registers at a `%MD`, copied by a `MOVE`, under the word order that is still
uncalibrated).

### `map.storage` — the declared join (contract §2.7)

**Every signal in this corpus occupies real PLC storage, and every path is GLOBAL**, so `owner` is
omitted and `path` is written verbatim, per §2.7. Nothing here belongs in `harnessOnly`.

***Do not resolve any of these by name shape.*** §2.7 forbids suffix matching outright — it is what
manufactured two of the four cross-block multi-writer findings this project has ever recorded. The
table is the join; it is not a hint that a leaf name can be matched against.

🔴 ***`FB_HxIntStep` USES A DIFFERENT VOCABULARY IN THE SPECIFICATION FROM THE ONE THE BLOCK USES,
AND IT IS DELIBERATE.*** Its clauses below say `AddendA`, `AddendB`, `Limit`, `ClearRequest`, `Total`
and `LimitExceeded`; the block's members are `StepInput`, `StepIncrement`, `StepThreshold`,
`StepReset`, `StepSum` and `StepOverThreshold`. Nothing is wrong — **the divergence is the point.**

Contract §2.8 exists because *the harness assumed the specification's signal name is the block's tag
name*, and three mechanical paths broke on it in one live run, with **one signal in seventeen
resolving because its two names happened to be the same string.** The other three blocks in this
corpus are that coincidence, twenty-in-twenty, because I wrote both the spec and the blocks — so
without this one divergence **the corpus could not exercise §2.8's translation at all**, and a
campaign green against it would mean nothing. Owner-ruled 2026-08-14: this is artificial material and
the specification is ours to set, so introducing the divergence is not the spec drift it would be on
a real register.

**No block member was renamed and no IR changed.** The divergence is created in the *specification*,
which is the only side that can create one — renaming the block's members would move both names
together and leave them equal.

| Block | Commanded in — `path` | Observed out — `path` |
|---|---|---|
| `FB_HxBoolEcho` | `iDB_HxBoolEcho.EchoCommand` (Bool), `iDB_HxBoolEcho.EchoReset` (Bool) | `iDB_HxBoolEcho.EchoResponse` (Bool), `iDB_HxBoolEcho.EchoInverse` (Bool) |
| `FB_HxDwellTimer` | `iDB_HxDwellTimer.DwellCommand` (Bool), `iDB_HxDwellTimer.DwellReset` (Bool), `iDB_HxDwellTimer.DwellPreset` (Time) | `iDB_HxDwellTimer.DwellDone` (Bool), `iDB_HxDwellTimer.DwellElapsed` (Time), `iDB_HxDwellTimer.DwellPresetEcho` (Time) |
| `FB_HxIntStep` 🔴 | `AddendA` → `iDB_HxIntStep.StepInput` (Int), `AddendB` → `iDB_HxIntStep.StepIncrement` (Int), `Limit` → `iDB_HxIntStep.StepThreshold` (Int), `ClearRequest` → `iDB_HxIntStep.StepReset` (Bool) | `Total` → `iDB_HxIntStep.StepSum` (Int), `LimitExceeded` → `iDB_HxIntStep.StepOverThreshold` (Bool) |
| `FB_HxSealLatch` | `iDB_HxSealLatch.SealTrigger` (Bool), `iDB_HxSealLatch.SealClear` (Bool) | `iDB_HxSealLatch.SealHeld` (Bool), `iDB_HxSealLatch.SealPriority` (Bool) |

**The copy layer is generated, not hand-written**, so binding these slots is `Harness.Map`'s job and
no networks for them appear in `FC_HarnessCopyLayer` yet.

### Modes — every observation in this corpus is persistent state

**No clause here turns on a one-scan output pulse, and none on a same-scan coincidence.** That is a
design choice, not luck: contract §4.1 says a one-scan event is unobservable at *any* polling rate and
a same-scan coincidence is unobservable by sampling at all, so a corpus written to be run must not
contain either. Concretely —

- Every output holds its value for as long as the commanded inputs hold theirs, so every observation
  is `PersistentState` and every expectation is admissible as `Latched` or as `Sampled` with a window.
- **Nothing requires `Stamped`.** No clause asks *when* something happened relative to T=0.
- ***There is no transient anywhere in this corpus, on either side, and that is enforced by a
  constraint rather than by care.*** This corpus has **no stimulus model** — its inputs are commanded
  straight from the mirror — so the finest stimulus a client can produce is one *held across at
  least one poll round trip*, never one scan. A clause needing a one-scan stimulus would therefore be
  **unstimulatable**, exactly as a clause needing a one-scan observation is unobservable, and both
  classes are excluded below.
- 🔴 **`SealHeld` and `SealPriority` are latches, and an expectation on them must NOT be declared
  `Latched`.** Contract §2.8's thirteen correct gate-5 refusals are this mistake: the mode describes
  the **instrument**, and the copy layer emits a plain coil for these. The block latching its own
  output is a *value under test*, not instrumentation. Declare `Sampled`; the window is generous
  because the value persists until cleared.
- ***Do not carry a window figure from this file.*** The floor is read from §12a derivation 1 at the
  `comp` actually used, and it has moved twice; a restated constant would one day refuse the wrong
  vectors with confidence.

### Settling — what this corpus offers, and what it deliberately does not

***None of these blocks publishes a settling signal, and none should.*** Contract §5 refuses a
settling condition that is the block's own completion flag, because the block's opinion of its own
progress is a claim under test. These blocks have no ramp and no multi-scan convergence: a value is
final one scan after the inputs that produce it.

So the settling condition available to a vector here is **scan-count-based against the mirror's own
free-running counter** (`HX_ScanCount`, `HarnessMirror`), which is written by the copy layer and is
not any block's opinion of anything. Stating which count is the vector author's call, not this
register's.

### Memory layout (contract §4.5) — `s7Objects: []`, and that is the earned claim

**No classic-S7comm path reaches any Hx instance DB.** The whole observation surface is the `%M`
mirror, served over Modbus TCP, and bit memory has no `MemoryLayout` attribute to revert.
Consequently:

- The correct `deployment` declaration for a wave against this corpus is **`s7Objects: []`** — the
  positive claim that no S7comm path reaches a data block, which §4.5 calls the normal, correct state.
  It is not the same as an absent `deployment`, which is nobody-said and `NOT CHECKED`.
- **These four iDBs stay `Optimized`, the S7-1200 default.** There is no
  `block-layout --set Standard --yes` re-assertion step for them after each import, and adding one
  would be work done against an invariant that already holds. *If a future transport reaches one of
  these DBs directly, that is not a vector to fix — it breaks the §4.5 invariant and is an
  escalation.*
- `drift-check` is structurally blind to the attribute in both directions and must never be cited as
  the gate for this, here or anywhere.

## Commanded values and their domains

No block holds a constant. What follows are the domains a vector must command inside; a value outside
one is **not** specified by this register and any result from it is unjudgeable, not a failure.

| Value | Type | Domain a vector must stay inside | Why the bound exists |
|---|---|---|---|
| `DwellPreset` | `Time` | `T#0MS` … `T#24D20H31M23S647MS` | The `Time` element is 32-bit signed milliseconds. |
| `AddendA`, `AddendB`, `Limit` | `Int` | −32768 … 32767 each | 16-bit signed. |
| `AddendA` + `AddendB` | — | the **sum** must also lie in −32768 … 32767 | An `Int` addition that leaves the range does not error at the register — *a width error wraps, and a wrapped sum is a confident wrong answer*. The corpus refuses to specify it rather than specifying a wrap. |

**`DwellPreset` above 65 535 ms is not merely permitted, it is the point.** A preset that fits in one
register cannot distinguish a correct two-register transform from a broken one, and a broken *word
order* multiplies by 65 536 and times out rather than answering early. Vectors that only ever command
short presets leave the whole 32-bit path unexercised.

---

## Requirements — FB_HxBoolEcho

Exercises the `Bool` `COIL` path in both directions and nothing else. It holds no memory, so a run
against it needs no clear-down, no arming window and no settling beyond one scan.

### REQ-HXE-001 — The response follows the command
- **Text:** While `EchoCommand` is true and `EchoReset` is false, `EchoResponse` is true.
- **Class:** control
- **Source:** Lane brief — "one with a `Bool` → `Bool` response (exercises the COIL mirror path)".

### REQ-HXE-002 — The response follows the command down
- **Text:** While `EchoCommand` is false, `EchoResponse` is false, whatever `EchoReset` is doing.
- **Class:** control
- **Source:** Lane brief; the testable inverse of REQ-HXE-001.
- **Notes:** Stated separately from REQ-HXE-001 rather than as one biconditional, because a block
  that never de-asserts satisfies REQ-HXE-001 perfectly.

### REQ-HXE-003 — The reset dominates the command
- **Text:** While `EchoReset` is true, `EchoResponse` is false and `EchoInverse` is false, whatever
  `EchoCommand` is doing.
- **Class:** control
- **Source:** Lane brief — "its own reset".
- **Notes:** This is the clause that makes the reset observable at all. Without it, a block that
  ignored its reset entirely would satisfy every other clause here.

### REQ-HXE-004 — The complement output is high in the complementary state
- **Text:** `EchoInverse` is true exactly when `EchoCommand` is false and `EchoReset` is false.
- **Class:** control
- **Source:** Lane brief; chosen so the mirror carries a signal that is **high** in the state where
  `EchoResponse` is low.
- **Notes:** Its value here is that it pins the reading of the result registers to something outside
  a single bit: a result register stuck low reads as a correct answer for `EchoResponse` in the
  command-low state, and as a wrong one for `EchoInverse` in the same state. *A relational claim is
  blind to any error its operands share*, so this clause is written against the input state, not
  against `EchoResponse`.

### REQ-HXE-005 — No state is carried between scans
- **Text:** The two outputs are functions of the same scan's `EchoCommand` and `EchoReset` alone. No
  sequence of earlier inputs changes what the outputs are for a given pair of present inputs.
- **Class:** control
- **Source:** Lane brief — "trivial"; chosen so this block needs no clear-down between runs.

---

## Requirements — FB_HxDwellTimer

Exercises the 32-bit `Time` path in **both** directions — a preset written down through the mirror
and two `Time` values read back up it — and a timed response against a commanded preset.

### REQ-HXD-001 — The dwell completes after the commanded preset
- **Text:** When `DwellCommand` has been held continuously true, with `DwellReset` continuously
  false, for the commanded preset, `DwellDone` becomes true and stays true for as long as both
  conditions continue to hold.
- **Class:** timing
- **Source:** Lane brief — "one with a timed response, `Time` preset".
- **Notes:** "The commanded preset" is the value the vector wrote into `DwellPreset` for that run —
  never a value the block chose, because the block holds none. A vector commanding `T#0MS` is
  commanding a dwell that completes on the first qualifying scan; that is inside the domain and is
  not an exception to this clause.

### REQ-HXD-002 — The dwell does not complete early
- **Text:** Before the commanded preset has elapsed with `DwellCommand` held continuously true and
  `DwellReset` continuously false, `DwellDone` is false.
- **Class:** timing
- **Source:** Lane brief; the testable inverse of REQ-HXD-001.
- **Notes:** Written against the **commanded** preset for the reason given in the Format section. A
  clause written against "its threshold" would be true of every implementation, including one whose
  preset is a tenth of what was asked for — which is the most ordinary commissioning error there is.

### REQ-HXD-003 — An interrupted command does not accumulate
- **Text:** If `DwellCommand` goes false at any point before the dwell has completed, the elapsed
  time returns to zero, and a subsequent dwell must run the full commanded preset from that point.
  Time held before the interruption does not count towards it.
- **Class:** timing
- **Source:** Lane brief — a timed response; chosen as the non-cumulative counterpart to the hopper
  block's deliberately cumulative one, so the two corpora do not test the same timing semantics.

### REQ-HXD-004 — The reset returns the dwell to zero
- **Text:** While `DwellReset` is true, `DwellDone` is false and `DwellElapsed` is `T#0MS`, whatever
  `DwellCommand` is doing. After `DwellReset` returns false, a dwell must run the full commanded
  preset before `DwellDone` becomes true again.
- **Class:** timing
- **Source:** Lane brief — "its own reset".

### REQ-HXD-005 — The elapsed time is published while the dwell runs
- **Text:** `DwellElapsed` reports how long the current uninterrupted dwell has run, and is `T#0MS`
  whenever no dwell is running.
- **Class:** timing
- **Source:** Lane brief — "every signal must be observable through the copy layer".
- **Notes:** This is the corpus's **slowly-changing observable**. A duration alone cannot tell a
  correct 32-bit transform from a broken one, because both produce a plausible-looking number; a
  value that advances monotonically while a run proceeds can.

### REQ-HXD-006 — The commanded preset is returned unchanged
- **Text:** `DwellPresetEcho` equals the value commanded on `DwellPreset`, in every state of every
  other input.
- **Class:** control
- **Source:** Chosen deliberately: it is the only clause in this corpus whose failure isolates the
  **mirror's 32-bit word order** from the block's timing behaviour.
- **Notes:** *A relational assertion is blind to any error its operands share.* Without this clause,
  a swapped-word mirror and a broken timer are indistinguishable — both show up as a dwell that never
  completes. With it, one of the two is settled before the timing clauses are read at all. Its value
  depends on the vector commanding a preset **above 65 535 ms**; below that, a word-order error is
  invisible in this echo as well.

---

## Requirements — FB_HxIntStep

Exercises the single-register `Int` `MOVE` path, an arithmetic result, and a comparison derived from
a published value rather than recomputed from the inputs.

### REQ-HXS-001 — The sum is the two commanded numbers added
- **Text:** While `ClearRequest` is false, `Total` equals `AddendA` plus `AddendB`, on the
  same scan those values are presented.
- **Class:** control
- **Source:** Lane brief — "one with an `Int` computed output (single-register `MOVE` path)".
- **Notes:** Domain restriction in the bounds table above: the sum must itself lie inside the `Int`
  range. Outside it, this clause states nothing.

### REQ-HXS-002 — The reset holds the sum at zero
- **Text:** While `ClearRequest` is true, `Total` is 0, whatever `AddendA` and `AddendB` are.
  After `ClearRequest` returns false, `Total` is the sum again.
- **Class:** control
- **Source:** Lane brief — "its own reset".
- **Notes:** The second sentence is the load-bearing half. A block that latched zero permanently
  after a reset would satisfy the first sentence and be wrong. It deliberately does **not** say "on
  the first scan after" — see "Excluded by design" below.

### REQ-HXS-003 — The threshold output reports the published sum
- **Text:** `LimitExceeded` is true when `Total` is greater than `Limit`, and false
  otherwise — including while `ClearRequest` holds `Total` at 0, where it is false unless
  `Limit` is itself below 0.
- **Class:** control
- **Source:** Lane brief; chosen so one commanded `Int` is judged against another commanded `Int`
  rather than against a constant.
- **Notes:** Strictly greater than, not greater-or-equal; the boundary case where the sum equals the
  threshold is a case this clause decides, and decides as false.

---

## Requirements — FB_HxSealLatch

Exercises latched output and clear semantics: two seals that differ in **one** respect, so that the
pair discriminates a wrong dominance instead of merely reporting a differently-timed fall.

### REQ-HXL-001 — A trigger seals the output on and it stays on
- **Text:** `SealTrigger` going true, with `SealClear` false, makes `SealHeld` true, and `SealHeld`
  remains true after `SealTrigger` returns false.
- **Class:** control
- **Source:** Lane brief — "one with a latched output requiring a reset".
- **Notes:** It deliberately does **not** say "a single scan of `SealTrigger`". This corpus has no
  stimulus model, so its inputs are commanded straight from the mirror and the finest stimulus
  granularity a client can achieve is *held across at least one poll round trip* — not one scan. A
  clause requiring a one-scan stimulus would be **unstimulatable**, which is the same defect as the
  unobservable clauses in "Excluded by design", reached from the input side. What this clause does
  test is the seal itself: the mutant it kills is a block that follows its trigger instead of
  holding.

### REQ-HXL-002 — Only the clear releases the seal
- **Text:** Once `SealHeld` is true, no input other than `SealClear` makes it false. It remains true
  across any sequence of `SealTrigger` transitions.
- **Class:** control
- **Source:** Lane brief — "requiring a reset".

### REQ-HXL-003 — The clear releases the seal and dominates the trigger
- **Text:** While `SealClear` is true, `SealHeld` is false, **including while `SealTrigger` is also
  true**. After `SealClear` returns false, `SealHeld` stays false until `SealTrigger` is next true.
- **Class:** control
- **Source:** Lane brief — "its own reset"; the clear-dominant half of the pair.

### REQ-HXL-004 — The second seal gives the trigger priority
- **Text:** `SealPriority` seals on and holds exactly as `SealHeld` does, with one difference: while
  `SealTrigger` and `SealClear` are **both** true, `SealPriority` is true. It becomes false only once
  `SealClear` is true and `SealTrigger` is false.
- **Class:** control
- **Source:** Chosen deliberately: two latches, not one, because *one latch cannot say which way
  round a dominance went*.
- **Notes:** The both-inputs-true case is the whole reason this block has two outputs. In every other
  input state the two outputs agree, and a vector exercising only those states establishes nothing
  that REQ-HXL-001..003 has not already established.

### REQ-HXL-005 — Nothing is held across a download
- **Text:** Neither output is retained. After a download, both are false until a `SealTrigger` in the
  run that follows.
- **Class:** control
- **Source:** Chosen so a run against this block needs no clear-down step; deliberately the opposite
  choice from the hopper block's retained alarm latch, so the two corpora do not test the same
  retention semantics.

---

## Excluded by design — clauses this register deliberately does NOT contain

Recorded so the reasoning survives and nobody re-adds them as an oversight.

**A one-scan lag claim, on any block.** An earlier draft carried "`LimitExceeded` reflects the
value `Total` holds on the same scan, not the value it held on the previous one", and the same
shape appeared as "on the first scan after `ClearRequest` returns false" and "on the same scan" in the
preset echo. All three were removed. They are attractive — a one-scan lag is invisible on a
slowly-changing signal, it is exactly what an intra-network ordering mistake produces, and it raises
no error at import or compile — but contract §4.1 is decisive: ***a one-scan event is unobservable at
any polling rate, and the difference between a lagged and an unlagged implementation lasts exactly
one scan.*** A clause no vector can falsify is worse than no clause: it reads as coverage forever,
and it would sit in the enumeration's `UNCLASSIFIED` residual permanently, which is how a gate that
is right becomes noise and then gets switched off.

**The repair that was considered and rejected:** latch the disagreement in a separate observer block,
the way `FB_HarnessViolationLatch` does for the hopper block. ***That observer would have to read
`FB_HxIntStep`'s members, which destroys the isolation the whole corpus exists to provide*** — the
two blocks would then share a signal, D9's colouring would separate every slot pair touching them,
and a wave reporting *N* concurrent would be running serially. **The isolation outranks the clause.**

Putting the observability code inside the block instead is refused by D13 / contract §2.1, and is not
this author's call in any case (contract §9.1 is open with the owner).

## Integration — what this corpus still needs, and why it is not here

The blocks are authored, convert cleanly and pass preflight with zero findings. They are **not yet
runnable**, and the two remaining steps belong to other hands on purpose:

1. **A call site.** Nothing calls these four FBs. `Main` (OB1) needs one `CALL` network per block.
   *Not done here* because a lane was mid-deployment against the same project when this was written,
   and adding calls to `Main` would have made the in-flight deployment reference four blocks it does
   not have. It is also a positional edit: inserting a network at the head of `OB1` makes
   `converter diff` report every following network as touched, which is an artefact of comparing by
   network number and not evidence about logic. Append the four calls at the **end**.
2. **Four copy-layer slot bindings.** `FC_HarnessCopyLayer` and the `HarnessMirror` tag table are
   **generated** by `Harness.Map`'s `CopyLayerGenerator` from a `RegisterMap` plus one `SlotBinding`
   per slot — they are not hand-authored, and hand-authoring a network into them would put a second
   author in a generated file. The observability table above is the input that binding needs.

Until both land, `converter undriven-scan` correctly reports every commanded input of all four blocks
as `UNDRIVEN`. That is the true state, not a defect, and it clears when the copy layer is generated
with these four slots in it.
