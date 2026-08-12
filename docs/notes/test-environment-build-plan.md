# TEST ENVIRONMENT — BUILD PLAN

Source of truth for the design: `PC-Client-Modbus-Spec-Draft-final.txt`. This document does not
restate it. It answers one question: **in what order do we build this so that no significant
body of code is written on an assumption that later turns out to be wrong?**

Written 2026-08-12. **Status updated 2026-08-12 (later the same day).**

---

## WHERE WE ARE

**Phase 0 is nearly done, and it went better than planned** — three assumptions retired, one of
them (A3) in our favour by a factor of two, and one whole class of work deleted by ruling.

| | Retired | How |
|---|---|---|
| **A3** `%MW` capacity | ✅ | **8192 B / 4096 words**, double the assumption, **and separate from work memory** — so the mirror costs zero work budget |
| **A7** run-state read | ✅ | Measured in **both** CPU states; `ReadRunState` built and tested (`93835e8`). Also **mechanises R8's device-side confirmation**, so the first unattended disruptive boundary needs no person |
| **A9** memory budget | ✅ | Scoped by ruling to **retain only**; work/load/block-count governed by "keep the PLC footprint minimal" instead |
| — S7 variable access | ✅ | Refused **CPU-wide** in both states (PUT/GET, not the STOP). **Moot** — all data reads go over Modbus |

**Still open, and unchanged in priority:**

| | Assumption | Why it still matters |
|---|---|---|
| **A1** | `MB_SERVER` single-scan atomicity | **The critical path.** The whole X-A model, the map, the copy layer and the client write path rest on it |
| **A2** | Modbus round-trip rate | Poll budget, slot width, O11 — and see 0.3, the assumed figure may not be a Modbus figure at all |
| **A4** | Block driveable by its own command signal | D37, inert, the start-bool mechanism |
| **A5** | Two slots do not interfere | Everything multi-agent |
| **A6** | Delegate throw leaves the CPU untouched (G4) | D32's guard model. A **safety** item |
| **A8** | Structural DB change vs retentives (G2) | DB-1's change-class table, therefore routing |

**PHASE 0 IS CLOSED.** 0.3 deferred by ruling (1.2 measures it as a by-product); 0.4 done and it
redesigned DB-12; everything else retired. The only item carried forward is **0.1b**, which is a
*rule to enforce* when harness objects are generated, not an experiment to run — it lands in
phase 2 with the copy-layer generator.

**The critical path is now A1**, and it runs through the phase-1 spike.

---

## THE ORGANISING PRINCIPLE

> **Every phase must retire the assumptions the NEXT phase's code depends on.**

The risk this plan exists to manage is not "the design is wrong" — it is **"we wrote three weeks
of coordinator before discovering `MB_SERVER` doesn't behave the way the map assumed."**

Two consequences run through everything below:

1. **Cheap experiments come before expensive code, always** — even when the experiment feels
   like a detour and the code feels like progress.
2. **Some code is written to be deleted, and it is labelled that way up front.** A spike that is
   honestly disposable costs a day. A spike that quietly becomes the foundation costs a month,
   and you find out late.

### What is actually load-bearing

Ranked by *how much code dies if it is wrong*, not by how likely it is to be wrong:

| # | Assumption | Status | What dies if wrong |
|---|---|---|---|
| A1 | `MB_SERVER` applies one request's registers within a single scan | **UNMEASURED** | The whole X-A atomicity model → map design, copy layer, client write path |
| A2 | Modbus TCP performs on this rig at roughly the assumed rate | partly measured | Poll budget, slot width, O11/D29 arithmetic |
| A3 | ~~`%MW` has ~2048 words on a 1214C~~ | ✅ **VERIFIED — it is 4096 words / 8192 bytes**, double the assumption, and **separate from work memory** | *(was: slot sizing, slot count, map layout — the ceiling doubled and the mirror costs no work memory)* |
| A4 | A block can be driven by its own existing command signal | untested | D37, inert, the entire start-bool mechanism |
| A5 | Two slots genuinely do not interfere | untested | Everything multi-agent |
| A6 | A delegate throw leaves the CPU untouched (G4) | **UNMEASURED** | D32's guard model — this one is a *safety* item, not a schedule item |
| A7 | ~~`PlcGetStatus` reports STOP (X-K)~~ | ✅ **MEASURED in both states.** STOP reads as raw `0x03` via Sharp7's catch-all | *(closed; also mechanises R8's device-side confirmation)* |
| A8 | Structural DB change: whole-DB reinit or added tags only (G2) | **VENDOR-CONTRADICTORY** | DB-1's change-class table, therefore routing |

**A1 and A3 are the two that must fall first.** They are cheap to test and expensive to be wrong
about. A6 is scheduled early for a different reason: it is the one whose failure mode is a
damaged rig rather than a wasted week.

---

## PHASE 0 — DESK AND DATASHEET

**Cost: hours. Code written: none. Assumptions retired: A3, A8, part of A2.**

Nothing here needs the rig writing to, and every item can invalidate a design decision.

| # | Task | Retires | Why it cannot wait |
|---|---|---|---|
| 0.1 | ~~`%MW` capacity on the 1214C~~ | A3 | ✅ **DONE 2026-08-12.** **8192 bytes / 4096 words** — double the assumed figure — and **separate from work memory**, so the mirror costs zero work memory. Place it **above** the retentive `M` range (retentive `M` is contiguous from MB0 and counts against retain) |
| 0.1a | Measure the per-slot object cost empirically | — | **Downgraded to informational by ruling:** only **retain** is gated; work/load/block-count are governed by "keep the PLC footprint minimal" instead. Still worth knowing, no longer a gate input |
| 0.1b | **Assert every harness object is non-retentive** | **A9** | **The one hard memory restriction.** Checkable from the IR before any device is involved. Watch the trap: retain is per-tag on optimized blocks but **all-or-nothing on standard-access ones**, and the harness deliberately creates standard-access blocks |
| 0.2 | ~~`PlcGetStatus` in RUN over Sharp7~~ | A7 (half) | ✅ **DONE 2026-08-12.** Returned Run from the rig at 79–94 ms. PUT/GET was **not** a blocker |
| 0.3 | ~~Establish what the 79–108 ms round-trip actually measured~~ | A2 | ⏸ **DEFERRED BY RULING 2026-08-12.** The design is committed and the owner knows the rough update times; the exact figure is not a decision input. Phase 1.2 measures it anyway as a by-product, so nothing is lost by not chasing it separately |
| 0.4 | ~~`C-122` conformance sweep~~ | — | ✅ **DONE 2026-08-12, and it redesigned DB-12.** C-122 is **not** a general "presets shall be data" rule (step-dwell timers only), its mechanised form **cannot see a literal PT at all**, and it passes **vacuously** on the live corpus (zero `.Step` leaves). Practice is good anyway — every process-behaviour timer is data-driven — but as house style, not enforcement. **And the longest behaviours are seconds accumulators, not timers**, so the tick oscillator is the real compression lever |
| 0.5 | ~~Confirm Sharp7 presence, version and connection path~~ | — | ✅ **DONE.** Sharp7 1.1.82.0; rack 0 / slot 1; address from the device allowlist |
| 0.6 | ~~Add read-only run-state read to `IS7Client` + `Sharp7Client`~~ | A7 | ✅ **DONE 2026-08-12**, commit `93835e8`. `ReadRunState`; no `Stopped` member; 221 tests pass; verified against the stopped rig |
| 0.7 | ~~Paired `DBRead` re-read in RUN~~ | — | ✅ **DONE 2026-08-12. It is NOT the STOP state.** `DBRead` and `MBRead` fail `0x00040000` in RUN too while SZL works — S7 variable access is refused **CPU-wide**, consistent with PUT/GET being off. **Moot by ruling:** all data reads go over Modbus TCP, so no Sharp7 read path is needed and PUT/GET need not be enabled |

> ### ⏸ 0.3 — deferred, but the doubt is recorded rather than dismissed
>
> The S7 status call measured **79–94 ms** and the spec's assumed Modbus round trip is
> **79–108 ms** — an overlap close enough to suspect the original figure was measured over
> **S7comm or ICMP rather than Modbus** and has been carried as a Modbus number since.
>
> **Deferred by ruling:** the design is committed and the update times are roughly known, so this
> is not a decision input. **Phase 1.2 measures the real figure as a by-product**, so the doubt
> resolves itself without a separate task.
>
> **What to do when 1.2 lands:** if the true Modbus figure differs materially, O11 and the
> tensor-width arithmetic need re-deriving — not the design. Recorded so a surprising number is
> recognised rather than absorbed.

> ### 📌 IR AUTHORING FOR THE SPIKE — hard rule 8 explicitly overruled, scope-limited
>
> The project's hard rule 8 sends **all** LAD/IR work through the `lad-coder` sub-agent. **The
> owner has overruled that for the phase-1 spike IR** (Modbus TCP blocks): a general agent with
> `lad-coder`'s *resources* — the conventions, the skills, the docs — is judged the better fit,
> because this is new IR territory rather than the pattern-composition work `lad-coder` is tuned
> for.
>
> **Scope, so this does not quietly become general practice:** it covers **the phase-1 spike IR
> only**. That work is disposable by construction (phase 1's rule), which is what bounds the
> risk. Everything else — the reference project, deliverable logic, `review-*` reads, `patterns/`
> — stays under hard rule 8. The 0.4 sweep in this same phase went to `lad-coder` accordingly.

**Deliberately NOT in phase 0:** the G2 bench test. It needs a download, so it rides with phase 1.

**Exit criterion:** slot size and slot count are *derived numbers with a source*, not estimates.

---

## PHASE 1 — THE SPIKE

**Cost: 1–2 days. Code written: throwaway, and labelled so. Assumptions retired: A1, A2, A6, A7, A8.**

> **EVERYTHING IN THIS PHASE IS WRITTEN TO BE DELETED.** It exists to answer questions, not to
> become the product. If any of it survives into phase 2, that is a decision made explicitly,
> not by drift.

The smallest program that can answer the wire questions:

- **PLC side:** `MB_SERVER` + `MB_HOLD_REG` pointed at `%MW`, plus one pattern-checker block that
  watches a register range for a *torn* pattern and latches the violation.
- **PC side:** a script that writes a known multi-register pattern in a tight loop and reads the
  violation latch.

| # | Experiment | Answers |
|---|---|---|
| 1.1 | Multi-register write, pattern check for tearing | **A1** — the single most load-bearing unknown |
| 1.2 | Round-trip timing, Modbus specifically, at realistic register counts | **A2** |
| 1.3 | Actual scan time with `MB_SERVER` in the program | the observability floor, for real |
| 1.4 | Read a register set while the program writes it — read-side tearing | X-A's read partition |
| 1.5 | `MB_SERVER` called first vs last in the scan; observe the difference | proves the ordering rule is real and controllable |

**And three that ride along with this phase's download**, because it needs one anyway:

| # | Experiment | Answers |
|---|---|---|
| ~~1.6~~ | ~~`PlcGetStatus` across a CPU stop~~ | ✅ **DONE 2026-08-12 — A7 CLOSED, and it never needed the download.** The owner stopped the CPU manually. Also mechanises R8's confirmation |
| 1.7 | Throw from the download delegate; inspect what the device is left in | **A6 / G4** — do this attended |
| 1.8 | Structural DB change, observe what reinitialises | **A8 / G2** — the ten bench minutes the research asked for |

### ✅ The PC side is built (2026-08-12)

`C:\Users\User\.claude\jobs\f24f6b1a\tmp\modbus-spike\` — outside the repo, in no solution, not
committed, every file headed `WRITTEN TO BE DELETED`. 52 tests pass, nothing was contacted.
Raw sockets rather than NModbus, deliberately and for this experiment only: **a library that
silently retries or splits a request would tear for reasons that have nothing to do with
`MB_SERVER`, and we would blame the PLC.** Phase 2 still uses NModbus.

Nothing is armed without `--arm`; without it, it prints the frames it would send and exits 10.

### 📋 THE REGISTER CONTRACT — what the spike IR must implement

`modbus-spike contract` prints this in full. The essentials:

**Pattern block** — `--pattern-base` (default 0), `--pattern-count` (default 100, hard limit
123), contiguous, inside `MB_HOLD_REG`. **The checker must inspect *exactly* `--pattern-count`
registers — the two counts are one number.** More, and the surplus hold stale values forever and
the latch trips permanently, which reads as "`MB_SERVER` tears". Fewer, and a tail tear is
invisible.

**Status block** — `--status-base` (default 200), 9 contiguous registers, PLC-written, one FC03:

| off | name | purpose |
|---|---|---|
| +0 | `VERSION` | constant 1; client refuses the mirror on any other value |
| +1 | `VARIANT` | 1 = `MB_SERVER` first in scan, 2 = last. **Published by the program**, not typed on the command line, so every run is stamped with the build that produced it |
| +2 | `SCAN_COUNTER` | free-running, +1 per scan, wraps, never reset (1.3) |
| +3 | `TEAR_LATCH` | sticky — a one-scan tear is ~10× below what a poll can see |
| +4 | `TEAR_INDEX` | first register disagreeing with `pattern[0]` |
| +5/+6 | `TEAR_VALUE_A/B` | turns "it tore" into "it tore between generations A and B at index N" — i.e. **how much of the request landed** |
| +7 | `CHANGE_COUNT` | +1 per scan where `pattern[0]` changed. **Liveness** |
| +8 | `CONTROL` | PC writes 1 to clear; **the PLC writes it back to 0**, so the reset is confirmed rather than assumed |

**Checker order, every scan:** bump `SCAN_COUNTER` → handle `CONTROL` **before** the check, so a
reset cannot swallow a same-scan violation → update `CHANGE_COUNT` → if not latched, compare
every `pattern[i]` against `pattern[0]`.

**Four non-optional requirements:** the whole pattern scan happens **in one PLC scan** (an
incremental checker tears on its own and manufactures the finding); it reads `%MW` **directly**,
not a copy or process image; **nothing retentive**, mirror above the retentive `M` range; and
`MB_HOLD_REG` points at `%MW` or a standard-access DB (optimized → status `16#818C`).

**Two builds for 1.5**, identical but for OB1 call order. **For 1.4**, a generator writing an
incrementing generation into every register once per scan, checker **off**.

> ### 🎯 The spike independently rediscovered DB-8's stimulus check
>
> "Every register agrees" is **also** what a mirror no write ever reached looks like — so a
> checker with only pass/fail returns green from an experiment that never ran. The client
> therefore has **four** verdicts (`Intact` / `IntactPreviousGeneration` / **`Stale`** / `Torn`)
> and exits 4 rather than reporting a clean latch when `CHANGE_COUNT` did not advance.
>
> That is DB-8's stimulus check — *evidence the input arrived and the block ran* — arrived at
> independently from the other end. **Corroboration worth having:** the same trap is waiting in
> the real result package, and it is invisible precisely when it fires.

### 🧭 THE CONFIRM LOOP — the invariance principle, stated properly (owner, 2026-08-12)

```
export ──► to-ir ──► to-xml ──► import ──► compile ──► export
   │                                                      │
   └──────────────── COMPARE THESE TWO ───────────────────┘
```

**Compare the very first XML against the very last XML.** That is the invariance claim, and it is
*** STRICTLY STRONGER THAN WHAT WE HAVE BEEN DOING. *** `drift-check` compares converter output
against the original export — a loop that never leaves the PC. The confirm loop goes **through
TIA**, so it also catches whatever TIA does to the content on import and compile.

*** AND IT WOULD HAVE CAUGHT THE `MemoryLayout` HOLE THAT drift-check CALLED A MATCH. *** Original
export says `Standard`; converter output says nothing; TIA applies `Optimized`; the re-export then
says `Optimized` — **first ≠ last, caught.** `drift-check` said MATCH because the Normalizer
ignores the attribute. The two checks are not redundant and the PC-only one is the weaker.

  ➜ Adopt the confirm loop as the acceptance gate for any converter capability change. It needs
    the block compiled and exportable, which the permission rule now allows.

#### 🔴 THE TRAP: BUILT NAIVELY TODAY, THE LOOP WOULD MISS `MemoryLayout` TOO

The loop compares the first export against the last. Both are TIA-produced XML — first says
`Standard`, last says `Optimized`. *** IF THAT COMPARISON USES THE NORMALIZER, IT RETURNS MATCH,
because the Normalizer ignores the attribute — which is exactly why `drift-check` missed it. ***
A byte-compare would catch it and would also fire on every UId reshuffle, and TIA reassigns Part
UIds unprompted, so byte-comparison is unusable as a gate.

*** SO THE ORCHESTRATION IS NOT THE WORK. THE COMPARISON IS. *** The loop is stronger in
principle and only realises that once the compare step is sensitive to the right things.

#### DECIDED 2026-08-12 — SHAPE AND SEQUENCE

**Keep BOTH checks; do not fold one into the other.** They answer different questions:
`drift-check` asks *"has the committed export drifted from the IR?"* — PC-only, fast, runnable
constantly. The confirm loop asks *"does this survive a real trip through TIA?"* — expensive,
mutating, and the right gate for capability changes.

*** THE LOOP CANNOT BE A CONVERTER SUBCOMMAND — FI-24. *** The converter is a pure in-process
file transformer and Portal/env-touching belongs in `openness-cli` or scripts; that invariant was
held deliberately. Making the loop an expensive *option on* `drift-check` would put Portal inside
the converter, which is the one thing FI-24 forbids. The decomposition that keeps both tools
honest:

  - **`converter compare <first.xml> <last.xml>`** — pure, in-process, Normalizer-based. Small,
    and it carries the actual judgement.
  - **`tools/confirm-roundtrip.ps1`** — orchestrates export → to-ir → to-xml → import → compile →
    export and calls that compare.

**TWO COSTS THE LOOP CARRIES THAT `drift-check` DOES NOT**, and both shape where it can be used:
  - *** IT MUTATES. *** The import writes, and we know it is not a no-op because the layout
    flips. So it runs against a SCRATCH project, or restores afterwards. Not a casual check.
  - Minutes per block, and it needs Portal exclusively — a gate for capability changes and
    pre-promotion, never something an agent runs per edit.

*** SEQUENCE, AND IT IS NOT OPTIONAL: THE NORMALIZER/`MemoryLayout` FIX LANDS FIRST. *** Building
the loop before the compare can see layout gives a gate that passes when it should fail, which is
worse than having no gate at all.

### 🎯 DESIGN DECISION — *** NO IR THAT THE AI CANNOT CHANGE *** (owner, 2026-08-12)

*** A CONSTRUCT THE CONVERTER CANNOT READ IS A BLOCK THE AI CAN NEVER MODIFY. *** That is not a
tooling inconvenience, it is a hole in the product's core promise — this project exists to build
"an AI capable of programming ladder logic", and a permanent no-go region in the corpus
contradicts that directly.

**This reframes every `UnsupportedConstructException` from "a correct hard error" to "a scope
item".** The hard error remains correct *behaviour* — failing loud beats failing silent — but it
is no longer an acceptable resting place for anything that appears in deliverable logic.

  ➜ ⚠️ **THE ORIGINAL CONSEQUENCE DRAWN HERE WAS WRONG, AND IT IS INSTRUCTIVE.** It read:
    *"`SupportedCallParameterSections` gains `InOut` … `MB_SERVER` calls will appear in real
    blocks."* *** `MB_SERVER` IS A `<Part>`, NOT A `<Call>` — MEASURED — so that whitelist has
    nothing to do with it. *** The ruling stands untouched; only the thing it was pointed at was
    misidentified. **What it actually requires** is the four gaps in "`MB_SERVER` MEASURED":
    `Array of Struct`, doubly-nested structured members, the Part template, and the silent
    `Version` loss. The InOut whitelist remains worth doing on its own merits and is **not** on
    this critical path.
  ➜ **The gate is the confirm loop above**, not a judgement call. Widening any capability without
    proving the round trip would replace a loud correct error with silent infidelity — the same
    failure class as the `MemoryLayout` hole. The loop is what makes widening safe rather than
    reckless, and it is the owner's own principle doing the work.
  ➜ *** THIS DECISION LIKELY DESERVES PROMOTION out of this build plan — into an ADR or a
    CLAUDE.md line — since it governs converter scope permanently and well beyond the harness.
    Flagged for the owner rather than done unilaterally. ***

### 🎯 `MB_SERVER` MEASURED, 2026-08-12 — the architecture fork is CLOSED

*** `MB_SERVER` IS A `<Part>`, VERSION 5.3. *** Zero `<Call>` elements and zero
`<Parameter Section=…>` elements in the whole 55,783-byte export, counted mechanically. **So the
`<Call>`/`Section="InOut"` whitelist is IRRELEVANT to it** — this is the fixed-shape-Part template,
as the serial pair also turned out to be. The InOut ports are wired as **ordinary symbolic
`<Access>` operands in normal input wire order**: `MB_HOLD_REG` points at a Static
`Array[1..90] of Int` with no pointer syntax whatsoever.

Ports: `en`, `DISCONNECT` (in), `MB_HOLD_REG` (**InOut**), `CONNECT` (**InOut**), `NDR`, `DR`,
`ERROR`, `STATUS`. This is the `CONNECT`-**structure** branch — no `CONNECT_ID`/`IP_PORT` on 5.3.

*** A THIRD SPELLING, AND THE WHITELIST MATCHES NONE OF THE REAL ONES: ***

    whitelist carries : Modbus_Master     Modbus_Comm_Load     (from hand-authored fixtures)
    the FC emits      : MB_MASTER  2.2    MB_COMM_LOAD  2.1
    the FB emits      : MB_SERVER  5.3

Version is clearly not incidental — 5.3 against 2.1/2.2 — so a template keyed on name alone would
silently accept a port list that does not match.

**`<OpenCon>` NARROWED, usefully:** all four in the FB are on **OUTPUTS and convert clean**. The
FC's failure was `REQ`, an **input**. *** THE GAP IS AN UNCONNECTED INPUT PORT — a port with no
`IdentCon` SOURCE — not `<OpenCon>` in general. ***

**`MEMORYLAYOUT` round-trips correctly on this block** (`Optimized` in, `Optimized` out) — the fix
is confirmed working on real content.

#### FOUR GAPS, IN THE ORDER THE FB NEEDS THEM

  1. **`Array[1..10] of Struct`** with inline nested members in an interface.
  2. **Doubly-nested structured members** — *** ON THE CRITICAL PATH: `CONNECT` MUST point at a
     `TCON_IP_v4`, so no IR can express `MB_SERVER` on this branch without it. ***
  3. The **`MB_SERVER` Part template** — name, version 5.3, 8 ports.
  4. 🔴 *** THE SILENT ONE: THE BARE-INSTANCE-MEMBER WRITER DROPS `Version`. *** The multi-instance
     member converts **without error** but lossily — IR carries `VERSION 5.3`, and `to-xml` emits
     `<Member Name="…" Datatype="MB_SERVER" Accessibility="Public" />` with **no `Version` and no
     `<AttributeList>`**. The `TON_TIME` member beside it keeps its `Version="1.0"`, so this is
     specific to that writer path. An import would declare a **versionless** `MB_SERVER` instance.
     *** NOTHING WARNS. *** By the "no IR the AI cannot change" ruling this is the same class as
     the ANY-pointer defect: a block that converts and cannot be safely written back.

With all three hard errors removed the block converts **exit 0** — nothing else is hiding behind
them.

#### A POSITIVE RESULT THAT DATES A SPEC LINE

*** THE INTERFACE PARSER ALREADY ACCEPTS `MB_SERVER` V5.3's MULTI-INSTANCE DECLARATION *** —
including a populated `<Section Name="InOut">` carrying `MB_HOLD_REG : Variant` / `CONNECT :
Variant`, and three levels down its own `TCON`/`TSEND`/`TRCV` V4.0 sub-instances with their own
populated InOut Variant sections. It parses without complaint.

*** THIS IS THE FIRST POPULATED `<Section Name="InOut">` EVER SEEN IN A REAL EXPORT HERE, SO
`ir/SPEC.md:44` — "never seen populated in any real block" — IS NOW OUT OF DATE *** and should be
corrected.

**Also confirmed supported, having been suspected:** `<Access Scope="TypedConstant">` with no
`<ConstantType>`, `<TemplateValue>` on a `TON`, and two `<NameCon>` sinks off one power rail.

#### 🔴 GAP 5 — I RECORDED `<Subelement>` AS SUPPORTED AND THAT WAS WRONG. IT IS SILENTLY DROPPED.

*** THE STRING `Subelement` APPEARS ZERO TIMES IN ALL OF `src/converter` *** — code, tests and
docs. The export carries **200+ of them**, holding the entire per-node configuration table: IP
addresses, node numbers, register addresses, lengths, and the remote IP inside
`RemoteAddress.ADDR`. No parse path reads them; no write path emits them.

*** SO THE "BLOCK CONVERTS EXIT 0 ONCE THE THREE HARD ERRORS ARE REMOVED" RESULT WAS REACHED WITH
THE CONFIGURATION TABLE ZEROED. ***

**The mistake was mine, and it is the same mistake three times today.** I recorded "converts
fine" from a run that produced no error. *** AN ABSENCE OF ERRORS IS NOT A POSITIVE RESULT. ***
`MemoryLayout` was lost while `drift-check` said MATCH; the instance `Version` is dropped while
nothing warns; and now the whole subelement table vanishes at exit 0. **Three silent-loss defects,
all found by looking at content rather than at exit codes**, and each one was sitting behind a
green check.

  ➜ Fixed as gap 5. New IR line form `[1] = 16#0A` / `[7,3] = 16#00` at the member's child indent,
    discriminated by the leading `[` — a SIMATIC member name can never start with one.
  ➜ *** AND THE ACCEPTANCE TEST IS SHARPER FOR IT: a round trip that loses the subelement table
    MUST FAIL `compare`. If it passes while the table is empty, the comparison is not seeing
    something it must see — and THAT is the finding. ***

#### GAP 6 — parameter-section members are re-emitted bare

FI-59 forces `bareShape` on every Input/Output/InOut member, but this FB's `reset : Bool` genuinely
carries `Remanence` **and** a 3-attribute `AttributeList`. *** FI-59's PROVEN constraint was only
that TIA rejects `Remanence` — the `AttributeList` was collateral. *** Measure with `compare`
first, then decide; do not widen FI-59 on a hunch.

#### One consequence of gap 2 worth keeping

The fix is **recurse-and-keep**, *not* FI-56's collapse — because the doubly-nested subtree holds
`ADDR`'s subelement start values, so *** COLLAPSING WOULD TRADE ONE SILENT LOSS FOR ANOTHER. ***
FI-56's quoted-UDT collapse and the anonymous-`Struct` refusal are untouched.

And gap 3 needs `Program.BuildBlockXml`'s `multiInstanceStatics` extended beyond `Calls` to
fixed-shape instances — without it, hand-authored IR emits `Remanence` and is **rejected at
import**, which would present as a mystery import failure rather than a known consequence.

⚠ **The FC has gone BACK to inconsistent** since its export — it is being edited in TIA. The
`12-fc-export-ORIGINAL.xml` baseline remains valid *as a baseline* but no longer matches the
project.

### ✅ THE SPIKE CHECKER IR IS AUTHORED (2026-08-12) — and it surfaced two more defects

In the job scratch dir, not the repo. **16 registers, not 100** — one FC16, 15 comparisons instead
of 122, and *** STATUS BASE STAYS AT 200 SO SCALING UP LATER MOVES NO ADDRESS. *** Verified:
`to-xml`/`to-ir` round-trip **byte-identical**, `--no-sidecar` derivability byte-identical,
`review` 18/18 with **0 findings**, `preflight` clean. Not compiled or imported — hard rule 4's
gate is unmet by design.

**The neat part, worth keeping:** "first disagreeing index" without a loop is obtained by emitting
the MOVEs in **descending** index order, so the lowest disagreeing index is written **last** and
survives. That avoids the O(n²) prefix-agreement terms the naive form needs. Confirmed present in
the generated XML, not just the IR.

**Readability cost, stated rather than discovered at review:** the capture network is 32
statements and fails C-101/C-602 on size. It passes on *subject* — one network, one thing — and
acceptable for a disposable spike. *** At 100 registers it becomes 200 MOVEs and is NOT a shape to
carry into phase 2. ***

*** ONE CONSTRAINT THAT MUST HOLD ON THE RIG: KEEP `MB_SERVER` IN OB1. *** The check spans two
networks of one call — atomic against OB1, but **not against an interrupt OB**. If anything ever
calls `MB_SERVER` outside OB1, the checker can be interrupted mid-check and *** MANUFACTURE the
tear it exists to detect. ***

#### 🔴 CONVERTER DEFECT — a comparison's literal is typed by MAGNITUDE, not by the compare's own type

Measured. With registers declared `UInt` — the honest type for a Modbus holding register — the
converter emitted **nine comparison literals as `Int`/`DInt` against `SrcType="UInt"` compare
boxes**. `BuildCompareStep` calls `ResolveOperand` with no `constantTypeOverride`, so *** FI-55
FIXED THE SrcType HALF OF THIS AND LEFT THE LITERAL HALF. *** Real TIA types them consistently
(`FB_MotorFwdRevSystem.xml` carries `<ConstantType>UDInt</ConstantType>` against a `UDInt`
compare), so this is very likely an import/compile rejection reached by a different door —
"data type Int of the actual parameter does not match the data type UInt of the formal parameter".

*** THE CORPUS HAS ZERO `UInt`/`Word` COMPARISONS, WHICH IS WHY NOTHING HAD HIT IT. *** Same shape
as the wrong-spelling Modbus names: a capability that looked covered because nothing had exercised
it.

  ✅ **FIXED 2026-08-12 in commit `4090b6f`** — `BuildCompareStep` now resolves `SrcType` first and
    passes it to both operands, general over `UInt`/`Word`/`USInt`/`SInt`/`UDInt`/`LInt`. *** THE
    `Int` WORKAROUND BELOW SHOULD NOW BE UNNECESSARY — confirm at the next import rather than
    assuming. ***

  ➜ ~~Worked around, not fixed:~~ every register is declared `Int`, which the converter's
    magnitude inference agrees with. Bit patterns on the wire are unchanged and the client reads
    `ushort`, so it is invisible to the PC side. *** THE WORKAROUND IS NOT A DESIGN CHOICE *** —
    the fix belongs in `SidecarSynthesizer.BuildCompareStep`, and `Word`-typed comparisons will
    hit it again.

#### ❓ AN UNKNOWN WORTH ONE RIG MEASUREMENT: DOES S7-1200 `ADD` WRAP OR SATURATE ON OVERFLOW?

Not established, so the counters wrap by an **explicit rung** rather than relying on overflow.
*** IF `ADD` SATURATES RATHER THAN WRAPPING, `SCAN_COUNTER` STALLS AT MAX AFTER ~5.5 MINUTES AT A
10 ms SCAN — INSIDE AN EXPERIMENT RUN, AND IT WOULD READ AS A STOPPED PLC. *** Cost of the
explicit wrap: exactly one value skipped per 65536 scans. **If the rig shows `ADD` wraps cleanly,
delete the two MOVEs.**

#### Hard rule 3 — flagged, not laundered

There is no `ir/<project>/` export for the spike, so *** ALL 26 TAGS ARE PROPOSED *** — invented
from the register contract. `to-xml` on the block **fails closed without the tag table** (FI-71),
so the two must travel together. **The engineer creates or imports the tag table; nothing here is
grounded.** `NUMBER 100` on the FC is likewise a guess and may collide.

**Deliberately not authored:** the `MB_SERVER` call (*** THE HAND-AUTHORING DECISION IS WITHDRAWN
— the owner has ruled the converter must express it; four gaps in "`MB_SERVER` MEASURED" ***),
OB1, and the experiment-1.4 generator — which needs the checker *** DISABLED, not
merely ignored. ***

### ✅ `compare` IS DIRECTION-AWARE, AND FI-75 CLOSES THE FOURTH SILENT LOSS — 2026-08-12

**`compare` now explains a flip instead of describing its debris.** Four `ATTR-DIFFERS` lines
became one `WIRE-DIRECTION` finding naming both roles. *** AND THE ROOT CAUSE OF THE OLD MISREAD IS
WORTH KEEPING: the Normalizer pins endpoint 0, so the flip survives — but it sorts `<Wires>` BY
RENDERED CONTENT, so a flip changes that content, the wire moves index, and positional pairing then
compared two DIFFERENT wires. *** That is why it read as two wires swapping names.

Classification is **all-or-nothing** and falls back rather than guessing; the one real fallback —
a reversal arriving alongside another edit in the same network — is asserted as a test so the limit
is on record. **An unexplained real difference beats a confidently mislabelled one.**

⚠️ **AND A CORRECTION TO MY OWN RECORD: the `MemoryLayout` parenthetical defect I logged was ALREADY
FIXED** (`8c1f8d1`, earlier the same day). *** THE REPORT I RECORDED IT FROM HAD BEEN MEASURED
AGAINST A PRE-FIX BINARY. *** Same class as FI-73, one level up: **verify a defect against the
current binary before recording it**, or the log accumulates ghosts.

**FI-75 — the fourth silent loss, and its blast radius is zero.** FI-56 accepted TIA's named-UDT
expansion by *discarding* it — but the values inside an expansion belong to the *** USE SITE, NOT
THE TYPE. *** Measured: a UDT declaring no start values, whose instance DB sets a fail-to-run time
and two reversal timings **existing nowhere else**.

  - *** IT WAS AN ASYMMETRY, NOT A PRINCIPLE: *** top-level `ParseMember` always kept the
    expansion; only the two **nested** positions discarded it — a shortcut made redundant by the
    recurse-and-keep machinery gap 2 built.
  - **Blast radius zero, measured three ways:** all 6 quoted-UDT expansions in the corpus sit at
    depth 0 with **zero** nested, so the collapse never fired on committed content; `drift-check`
    **unmoved at its exact baselines**; and `to-ir` over all 38 committed exports produced **0
    files changed, byte for byte**.
  - *** THE WRITE SIDE NEEDED A MATCHING CHANGE, AND THIS IS THE PART THAT MATTERED: ***
    `WriteTypeMember` emitted nested members in the anonymous-`Struct` shape, so keeping the
    expansion on the read side alone would have *** TRADED A SILENT LOSS FOR A SILENT
    CORRUPTION. ***

**996 → 1010 tests**, golden harness 46, `drift-check` at exact baselines throughout, Release
rebuilt in the main checkout and behaviour-verified — so this is the binary the tooling runs.

### ✅ D32's CLASSIFIER AND X-C's WAVE MARKER — built 2026-08-12, `src/wave-control/`

Standalone solution, library references **nothing** — no Openness, no package, no network. Not
wired into `openness-cli`. **58 tests**, and the negative tests were run *** BY MUTATING THE REAL
PRODUCTION CODE, not a fake ***: making the classifier's fallthrough return Class A turned 5 red;
making an unreadable marker read as clean turned 10 red, including one that walks **every prefix
length** of a serialised marker.

**Two design moves worth keeping:**

  - *** THE MARKER IS WRITTEN IN PLACE, DELIBERATELY *NOT* WRITE-TO-TEMP-AND-RENAME. *** Rename is
    the usual atomicity trick and is **the wrong shape here**: a crash mid-write would leave no
    file, and no file reads as *"no wave in progress"* — the dangerous answer. In place means that
    the instant `BeginWave` touches disk, a crash reads as **a wave WAS in progress**. Every
    default follows the same rule: `NoWaveInProgress` is deliberately *not* the enum's zero value,
    and `Read()` avoids `File.Exists` because it swallows every error and returns false.
  - *** THE FORBIDDEN ACT CANNOT BE RECORDED. *** `EscalationRecord.AfterDisruptiveDownload`
    **throws** for a non-Class-A verdict — so a caller has no route to log the thing D32 forbids,
    rather than merely being told not to. Class A entries likewise cannot be constructed without
    naming the download option, stating the entailment, and citing `[M]`/`[R]` evidence.

**Pinned on the way:** `StopAll` is **Class A and not on the deny list** — three spec passages
cited a deny list containing it, and putting it there would make R8 impossible and D25
unimplementable.

#### 🔴 A DEFECT IN D32 IT FOUND, NOW RECORDED IN THE SPEC

*** `StartModules` IS CLASS A BUT ITS RUNG IS WRONG. *** It is raised in the **POST** delegate —
after the download has already stopped the modules — so refusing it does not call for a *new*
disruptive download, it calls for answering it **within the current one, or the CPU is left
stopped**. "Go to step 6" is circular for this entry alone. **The ladder needs a per-entry rung,
not one rung per class.** Implemented as written and flagged rather than silently deviated from.

#### Four more it flagged, carried rather than closed

  - **Class A's payoff still rests on G4**, which the audit reopened — step 6 is only reachable
    through the throw-from-delegate abort, and that has **one** observation, not six. *** CLASS A
    IS THE ONLY CLASS THAT SPENDS ANYTHING, AND ITS RUNG IS THE UNMEASURED ONE. ***
  - X-C says results are discarded **by slot**, but *** THE CRASH THAT PRODUCES AN UNREADABLE
    MARKER IS EXACTLY THE ONE WHERE THE SLOTS ARE UNKNOWN *** — X-C does not say what to do then.
    Ruled the whole rig state invalid; **that is the agent's ruling, not the spec's**, and needs an
    owner decision if discard-by-slot ever becomes the real mechanism.
  - **The marker only helps a coordinator that RESTARTS.** X-C's stated fear is a rig left running,
    and nothing here touches that — the PLC-side watchdog is still held for later.
  - One durability gap `WriteThrough` does not close, recorded in a code note: flush covers the
    file's *content*, not the *directory entry* of a newly created file, and .NET has no portable
    directory fsync on Windows. **Does not apply to process death** — the case X-C actually names.

### ✅ `MB_SERVER` IS EXPRESSIBLE IN IR — 2026-08-12. SEVEN GAPS, THREE OF THEM SILENT.

`compare` against the byte-exact original: **DIFFERS, 2 differences**, and *** BOTH ARE
DELIBERATE, DOCUMENTED AND PRE-DATE THIS WORK *** — `Remanence` on an Input parameter (TIA
**exports** it and **refuses it at import**, FI-59, so diverging is required for the file to import
at all) and the multi-instance's inline expanded interface (the *callee's* declaration, discarded
by design and regenerated by TIA on every export). Nothing the block owns is lost.

*** THE `n→0` SIGNATURE CHECK WORKED, AND IT IS THE VERIFICATION TO KEEP USING: *** a
whole-document element tally shows **subelements 182 → 182**, every other `n→0` is a Normalizer
volatile, and the `Member 157→63 / Section 43→13 / Sections 17→8` deltas are accounted for
**exactly** by that one discarded expansion (94/30/9). `to-ir → to-xml → to-ir` byte-identical;
original md5 unchanged.

**Root causes worth keeping:**
  - **Gap 4's `Version` drop was a READ defect, not a write one.** A multi-instance was read as a
    *"bare parameter"* — a shape with nowhere to put a `Version` or `AttributeList`. Fixed on the
    read side.
  - *** GAP 5 WENT UNNOTICED BECAUSE MEMBERS HAD NO UNKNOWN-CHILD GUARD AT ALL. *** That is the
    structural finding, not the subelement itself: the format silently accepted anything it did
    not recognise. Now a named refusal.
  - **Gap 7, new: a multi-line network comment was a permanent hard error** — no `\n` escape
    existed, so `to-ir` refused *the whole block over its documentation*. Fixed after collapsing
    five copy-pasted `EscapeString` implementations into one, so the halves cannot drift.

**Tests 968 → 996**, golden harness 39/39, and `drift-check` reports the **identical 6 pre-existing
drifts** before and after — no corpus regression. Four failures during the work were obsolete
assertions from intended changes, repurposed rather than deleted — including one whose "unknown
instruction" stand-in was literally `MB_SERVER`, which correctly failed the moment it became known.

#### 🔴 THREE THINGS LEFT OPEN, ALL WORTH CARRYING

  1. *** WHAT A NEXT GAP LOOKS LIKE — the class is now nameable: "a shape the format cannot
     represent, where the guard against it was never paired with a way to express it." *** Both
     gap 5 and gap 7 were found that way — not by a failing check, but by the pipeline refusing
     something real. Named candidates not yet hit: a `<Sections>` with a name other than `"None"`,
     a `<Subelement>` on a nested section member with multiple `<StartValue>`s, and `MemoryLayout`
     on an IR-authored block.
  2. *** FI-56's QUOTED-UDT COLLAPSE SILENTLY DISCARDS PER-USE-SITE `StartValue`s. *** Untouched —
     it is load-bearing for the committed corpus's fixed point — but it is **the same class as
     gap 5, one door along**, and deserves its own FI rather than living in a report.
  3. **FI-59 may be too wide, and one live import would settle it.** Its `Remanence` suppression is
     grounded on an **FC** rejection, but this is an **FB** whose export carries `Remanence` on an
     Input parameter — and block-kind-dependent section legality is already real here (`Return` is
     FC-only; `Output`/`InOut` are illegal on OB). Narrowing FI-59 to FC-only would close
     difference #1. *** NOT NARROWED ON A HUNCH — the same discipline as not widening it. ***

### 🔧 PROCESS — `git add` + `git commit` IS NOT ISOLATION. USE `git commit -- <paths>`.

Happened twice on 2026-08-12, the second time to me. *** THE INDEX IS SHARED ACROSS EVERY AGENT IN
A WORKTREE. *** Staging by explicit path does not protect you, because a *bare* `git commit` in
any lane commits **the whole index** — including whatever another lane has staged and not yet
committed. Commit `4f01d32` therefore carries a docs change **and** four `openness-cli` files
belonging to a different lane, under the docs message.

  ➜ *** THE PRIMITIVE THAT ACTUALLY ISOLATES IS `git commit -- <path> [<path>…]` ***, which
    commits only the named paths regardless of what else is staged. `git add <path>` followed by a
    bare `git commit` does **not**.
  ➜ **And do not fix a sweep by rewriting history while other lanes are live** — a reset or rebase
    under an actively-writing agent is far worse than a mislabelled commit. Record it instead: an
    empty commit carrying the intended message and naming where the content landed (`f75aa7f`
    does this) costs nothing and leaves the history honest.

### ✅ `hmi-compile` NOW KEYS ON ERRORS — and the HMI asymmetry is now measured, not believed

Reuses `Program.EffectiveErrorCount` rather than a second copy, so the fail-closed
`max(ErrorCount, Error messages in the tree)` behaviour is identical to `compile`'s. Warnings
still print; only the exit code changed. **539 → 550 tests**, Debug only, Release never touched.
Both guards negative-tested: reinstating `State != Success` failed 5 of 11; keying on
`result.ErrorCount` alone failed the fail-closed case.

*** THE CONSISTENCY READ-BACK GENUINELY DOES NOT EXIST FOR HMI, CHECKED RATHER THAN ASSUMED. ***
Across the V20 API surface every `*Consisten*` member is PLC-side — `PlcBlock`, `PlcType`,
`PlcForceTable`, `PlcWatchTable`.`IsConsistent` — with **zero** in either HMI namespace. So **no
always-null field was added**, and two tests assert that *absence* deliberately, *** so nobody
later "completes the symmetry" with a field that would read as a check that ran and found nothing
wrong. ***

  ➜ `hmi-compile` success is **weaker than `compile` success**, the difference cannot be closed
    from our side, and the README now says so outright with the API evidence. An undocumented
    asymmetry between two commands with matching flags is how someone later trusts an HMI compile
    the way they trust a PLC one.
  ➜ CLAUDE.md's *"No FI-52 backstop"* sentence **survives verbatim** — it is now measured true
    rather than merely believed.

### ✅ THE SPIKE CHECKER IS IN THE PROJECT AND COMPILES — 2026-08-12

*** THE IR PIPELINE WORKS END TO END INTO TIA. *** First time IR-authored content has made the
full trip. Tag table then block, both exit 0; `sanity-check` after compile: 71 blocks,
`inconsistentBlocks` = **the pre-existing sample FC only** — ours is not in it. 33 types, 0
inconsistent. Device compile Success, 0/0.

**The per-block compile exited 8 with `errors: 0`** and *"Block was successfully compiled"* — the
known `State`-vs-`ErrorCount` defect firing on this project's permanent hardware warning, exactly
as predicted. *** READING THAT EXIT CODE AS FAILURE WOULD HAVE BEEN WRONG, WHICH IS WHY THE
INSTRUCTION NOT TO TRUST IT EXISTS. ***

**Verified POSITIVELY rather than resting on the green**, since an absence of errors is not a
result:
  - 7 networks, 135 parts, **identical histogram sent vs returned** (`Move` 43, `Ne` 47,
    `Contact` 32, `Eq` 9, `Add` 2, `Coil` 1, `O` 1). FC number **100** preserved.
  - *** THE DESCENDING-INDEX MOVE ORDERING SURVIVED *** — the reference sequence is identical end
    to end, so "lowest disagreeing index written last" is intact **in the controller**, not just
    in the IR. That trick is what makes `TEAR_INDEX` meaningful.
  - Text content byte-identical. *** ELEMENT-COUNT DIFF IS 0→n IN EVERY CASE, NEVER n→0 — TIA
    REMOVED NOTHING ***, adding only export metadata and block-attribute defaults.
  - **All 26 tags landed, all `Int`, every `%MW` address exact.** *** `%M` NOW WORKS END TO END
    THROUGH THE TAG-TABLE IR PATH — an explicitly untested area, now measured rather than
    assumed. ***

*** THE `MemoryLayout` HOLE REPRODUCES ON AN FC, NOT JUST DBs. *** `compare` first exited **2 —
NOT COMPARED**, its guard firing because the re-export declares `Optimized` and our converter
output declares none. Re-run with `--allow-silent-layout` (documented for exactly this case, one
side being converter output): **exit 0, EQUIVALENT**. The tag table compared EQUIVALENT with no
flag. *** THE GUARD DID ITS JOB — it refused to compare rather than quietly comparing weakly. ***

  ➜ **Assessed harmless for the spike**, and the reasoning is INFERRED not measured: the checker
    reads and writes `%MW` marker memory directly, which is always classic-S7comm addressable, so
    the FC's own layout does not affect the mirror's wire visibility.
  ➜ `Int` caused no trouble at import or compile — **VERIFIED that `Int` works; INFERRED nothing
    about whether `UInt` is now viable** after `4090b6f`, since the `Int` form is what was imported.

**A cosmetic defect in `compare`'s own output, worth fixing because it misleads:** the
`--allow-silent-layout` line prints `MEMORYLAYOUT: (none declared) -> Optimized (NOT compared —
neither document declares one)`. The parenthetical **contradicts the value printed beside it**,
since the second document plainly declares one. Substance right, message wrong.

**Two limits carried forward, so "0 findings" is not over-read:** all 18 `review` rules report
`not applicable` on a tag table, so *** THE TAG TABLE IS UNREVIEWED RATHER THAN CLEAN ***; and
`compare` runs through the Normalizer, so a **port-direction flip stays invisible** until the
pending fix lands.

### ✅ CLOSED 2026-08-12 (`1edf376`) — AND IT WAS TWO BUGS HIDING EACH OTHER

*** THE CONVERTER WAS EMITTING WIRE ENDPOINTS IN THE WRONG ORDER, AND THE NORMALIZER WAS BLIND TO
IT IN EXACTLY THE SAME WAY, SO EVERY CHECK PASSED. *** Regenerating `FC_Inputs.ir` inverted
**100% of its 132 Contact/Coil operand wires** against the real export — and nothing caught it.
*** THE TWO BLINDNESSES CANCELLED EXACTLY. ***

That is the sharpest instance yet of the pattern that has run through this whole day: **a check
that shares its subject's blind spot is not a check.** `drift-check`, `compare` and the
`--no-sidecar` derivability check all ran green against output that did not match TIA.

**There were TWO 2026-07-14 measurements, not one** — my brief named only the first:
  1. **The Normalizer's** (`docs/evidence/stage-S1.md`): a converter-only round trip reporting a
     false mismatch that was 100% endpoint order within individual `<Wire>` elements.
  2. **`FlgNetBuilder`'s own** — *** A LIVE TIA IMPORT REJECTION ***: *"the connections in the
     power rail … with more than two I/Os must be located in the same sequence"*. Fixed by sorting
     endpoints **by UId** — and that is the sort that inverted direction, because *** UId ORDER
     DECIDES PRODUCER-VS-CONSUMER BY NUMBERING: TIA numbers an Access BELOW the Part reading it,
     the synthesizer numbers the Contact BELOW its Access. ***

**So pin-the-first/sort-the-tail was right and could not land alone.** Applied to the Normalizer
by itself it turned **17 MATCH into 17 DRIFTED**. Both sides had to be fixed together: the builder
now emits **producer-first** (21 output-wire sites, *** INCLUDING THE CALL OUTPUT PARAMETER — the
case that matters most, since a callee's parameter names are author-chosen and carry no direction
convention ***) and sorts only the consumer tail, preserving the import constraint.

**Grounded across all 34 real TIA exports: 106 `(part, port)` pairs, ZERO appearing in both slots.**
`out`/`OUT`/`DEST`/`Ret_Val`/`eno`/`Q`/`ET` only ever at index 0; `operand`/`in`/`en`/`PT`/`R` only
ever in the tail.

**996 converter + 46 golden green, `drift-check` at its exact baselines** — *** AND THAT RESULT IS
NOW DIRECTION-SENSITIVE, so it proves the regenerated XML reproduces TIA's endpoint order rather
than merely surviving a sort that hid the difference. *** `compare` on a flipped CALL-output wire:
**before EQUIVALENT exit 0, after DIFFERS exit 1.**

  ⚠ **`compare`'s message is not direction-aware.** Because `<Wires>` children are content-sorted,
    one flip surfaces as **four** `ATTR-DIFFERS` lines that read like *rewiring* rather than
    "this port's direction reversed". Actionable — it names the right network and ports — but a
    direction-aware finding would be a genuine improvement. Left as a separate concern.
  ✅ **The stale caveat elsewhere in this file** — "a port-direction flip stays invisible until the
    pending fix lands" — **is now closed by this entry.**

**One discipline point worth keeping:** the frozen answer keys were snapshots of our own *pre-fix*
output and the only surviving record of those blocks' stored wiring. They were rewritten
**order-only, in place** (98/98, endpoint sets asserted unchanged) rather than regenerated from
synthesis — *** which would have made the test compare synthesis against itself. ***

### ~~🔴 OPEN DECISION~~ — the original entry, kept for its reasoning

Found 2026-08-12 while building `compare`, and it is the `MemoryLayout` class of defect one level
down. Two facts, each measured independently, that had not been put together:

  1. `Normalizer.Strip` sorts a `<Wire>`'s **own endpoints** by content
     (`element.Name.LocalName is "Wires" or "Parts" or "Wire"`), justified by a 2026-07-14
     measurement about multi-endpoint fan-out ordering.
  2. On a real TIA export, *** PORT DIRECTION IS CARRIED ONLY BY ENDPOINT ORDER *** —
     `<IdentCon>` first = input-like, `<NameCon>` first = output. There is no attribute, no child
     and no other marker.

*** SO SORTING THE ENDPOINTS DESTROYS THE ONLY SIGNAL THAT ENCODES DIRECTION *** — an input wire
and an output wire on the same port name normalize to the same thing. And because `compare`,
`drift-check` and the `--no-sidecar` derivability check ALL run through the Normalizer, *** ALL
THREE INHERIT THE BLINDNESS. ***

**Exposure, stated honestly:** in the ordinary case direction still survives via the `<NameCon>`'s
`Name` attribute, because a given port is conventionally always an input or always an output. The
gap is any port where the same name can be both — which is exactly the InOut case, since wire
order *** cannot distinguish an input from an InOut either. ***

  ➜ **Likely fix, and it is narrow:** sort only the DESTINATION endpoints and pin the FIRST one.
    That keeps the 2026-07-14 fan-out stability the sort was added for, and restores direction.
  ✅ *** RULED 2026-08-12: FIX IT. *** `compare` is the confirm loop's judgement, and a blind
    comparator is worse than none. Whoever takes it *** MUST RE-READ THE 2026-07-14 MEASUREMENT
    FIRST *** — the sort was added on evidence, and the fix must not discard what that evidence
    was protecting. Not yet started.

### ⚠️ POWERSHELL 5.1 WILL MISREAD AN EM DASH IN A `.ps1`, AND THE ERROR POINTS SOMEWHERE ELSE

Measured 2026-08-12. A BOM-less `.ps1` is read as **ANSI**, so a UTF-8 em dash decodes to `”`
(U+201D) — *** which the parser treats as a QUOTE DELIMITER. *** The script failed with an error
pointing at an unrelated line **a hundred lines away** from the actual character.

This matters here because this project's prose uses em dashes constantly, so a script written in
the house voice is a live hazard. *** KEEP `.ps1` FILES ASCII-ONLY AND CRLF ***, matching
`tools/openness-approve-*.ps1`, and put a comment in the file saying why so nobody "improves" the
punctuation later. Belongs in a more general hazards note than this build plan — recorded here so
it is not lost.

### 🔬 IR discovery run, 2026-08-12 — three findings, one of them a live hazard

The owner imported blocks and deliberately left them **uncompiled** so they could be identified
by `IsConsistent = false`. Captured before anything could compile — and captured with
`list --json` rather than `sanity-check`, because **`sanity-check` runs device compiles and would
have destroyed the marker it was being used to read**. Three blocks: a sample Modbus TCP FC, a
standard-access global DB, and a hardware-interrupt OB. **`TYPES: INCONSISTENT: 0` — no new UDT.**

**FI-52 reproduced live, again:** the device compile reported `Success, 0 errors, 0 warnings`
with all three blocks sitting inconsistent behind it.

**1. ✅ The DB round-trips completely.** `export → to-ir → to-xml → drift-check --complete` gives
`0 drifted, 1 match, 0 export-only, 0 error`, and `to-ir --no-sidecar` succeeded (ADR-0005
derivability) producing byte-identical IR. Shape: one member, `Array[0..67] of Byte` — 68 bytes,
34 holding registers.

**2. 🔴 AND THE ROUND TRIP IS EQUAL AND STILL WRONG — `MemoryLayout` IS LOST.**

```
original export       : <MemoryLayout>Standard</MemoryLayout>
regenerated xml       : NO MemoryLayout element at all
drift-check           : MATCH        (Normalizer ignores the attribute)
block-layout --expect : Standard, exit 0
```

Re-importing IR-derived XML states **no opinion** on layout, so TIA applies the S7-1200 default —
**Optimized** — and `MB_HOLD_REG` then rejects the DB with `16#818C`. *** EVERY CHECK IN THE
PIPELINE STAYS GREEN: drift-check is structurally blind to it, import does not error, compile
does not error. *** This is the hazard CLAUDE.md records for `block-layout`, now **measured on
exactly the DB shape the register contract needs**.

  ➜ **Mitigation is required, not precautionary:** re-assert `block-layout --set Standard --yes`
    after **every** import of that DB, then gate with `--expect Standard`.
  ➜ ***BETTER: USE THE `%MW` MIRROR AND NOT A DB-BACKED ONE FOR PHASE 1.*** It sidesteps the hole
    entirely rather than policing it — and `%MW` costs no work memory either (§16.1).

**3. 🔴 REFUTED BY MEASUREMENT — DO NOT ACT ON THE PARAGRAPH BELOW.** It was written from a source
read before any Modbus block had been exported, and *** BOTH ITS PREMISE AND ITS CONCLUSION ARE
WRONG. *** `MB_SERVER` is a **`<Part>`**, not a `<Call>` — zero `<Call>` elements in the entire
export — so `SupportedCallParameterSections` is **irrelevant to it**, and the owner has since
**withdrawn** the hand-authoring answer ("we make it possible then"). *** THE AUTHORITATIVE
SECTION IS "`MB_SERVER` MEASURED" ABOVE. *** Kept only as the record of what was believed:

> ~~`MB_SERVER` will not convert to IR, and the reason is not its name. It is a library FB, so it
> appears as `<Call BlockType="FB">`… THE OBSTACLE IS THE PARAMETER SECTION:
> `SupportedCallParameterSections = {Input, Output}`, and `MB_HOLD_REG` (and `CONNECT`) are
> InOut/VARIANT… **Predicted, not yet confirmed.**~~
>
> ~~**Open decision:** (a) author the `MB_SERVER` call by hand in TIA…; or (b) extend
> `SupportedCallParameterSections`. **(a) is the phase-1 answer.**~~

*** THE LESSON, WHICH IS THE REASON THIS IS KEPT RATHER THAN DELETED: the prediction was
code-grounded, specific, confidently worded, and wrong — because it reasoned about what the
converter refuses without ever looking at what TIA emits. *** Two rounds of planning were scoped
around a whitelist that was never the obstacle.

### ⚙️ TWO `openness-cli` DEFECTS FOUND IN PASSING, 2026-08-12 — both affect any caller

**1. 🔴 `compile --block` EXITS 8 ON `errors: 0`, AND WILL DO SO FOR EVERY BLOCK IN THIS PROJECT.**
`RunCompile` keys its verdict on `State != Success`, where `compile-all` keys on `ErrorCount` —
and CLAUDE.md says `compile-all` does that *precisely because* "a project with pre-existing
hardware warnings returns non-Success on a clean block". **This project has exactly such a
permanent warning** ("Inputs or outputs are used that do not exist in the configured hardware"),
so *** EVERY per-block compile here exits 8 regardless of the block. *** A caller trusting the
exit code reads every clean compile as a failure. `compile` should key on `ErrorCount` the way
`compile-all` already does.

**2. A CLEAN PER-BLOCK COMPILE DOES NOT IMPLY EXPORTABLE — the converse of FI-52.** FI-52
established that a *device* compile can leave blocks inconsistent. This adds the other direction: a
**per-block** compile reporting "Block was successfully compiled", `errors: 0`, that *** STILL
LEAVES `IsConsistent = false` *** because a referenced block is absent. `sanity-check` caught it;
the compile's own output did not. So "compiled clean" is not a sufficient precondition for export —
consistency must be checked separately.

### ✅ RESOLVED — the FC export blocker (kept for the diagnosis, which recurs)

The permission rule landed and the compiles ran. **The FC still cannot be exported**, for a
different reason: it compiles with `errors: 0` but stays `IsConsistent = false` because
*** network 1 references an instance DB that does not exist anywhere in the project *** —
`Called block DB110 in network is not available`, against a project whose highest block number is
48. TIA then refuses the export outright (exit 5: *"Inconsistent blocks and PLC data types (UDT)
cannot be exported"*).

A per-block compile cannot invent the missing DB, so the inconsistency is permanent until someone
creates it. `create-instance-db` was blocked by the classifier and *** would likely have failed
anyway: the gateway calls `CreateInstanceDB(name, isAutoNumbered: true, …)` and therefore cannot
place a DB at number 110 *** — so if the FC references by number rather than name, no CLI route
exists at all.

  ➜ **CHEAPEST UNBLOCK — the owner, in TIA, about a minute:** open the FC and assign or create the
    instance DB for the call in network 1. Then `compile --block` and `export --block` both work
    with no further permission changes.

  ⚠ **AND ONE ASSUMPTION TO CHECK WHILE THERE:** *nothing has read the FC's contents*, so **that it
  contains an `MB_SERVER` call at all is inferred** — from the block's name and the surrounding
  design notes, not from evidence. The dangling DB110 reference is the only measured fact.

### ✅ The hardware-interrupt OB is fully supported

Exports and converts clean (exit 0), no new construct. An empty marker block —
`<SecondaryType>HardwareInterrupt</SecondaryType>`, one empty compile unit, and a TIA-supplied
Input section of informative members (`LADDR : HW_IO`, `USI : Word`, `IChannel : USInt`,
`EventType : Byte`). All of it round-trips. Its export is preserved as the **first XML** for a
confirm-loop baseline.

### ⛔ SUPERSEDED — the earlier permission block

TIA **refuses to export an inconsistent block** (verified live, exit 5: *"Inconsistent blocks and
PLC data types (UDT) cannot be exported"*). So each marker block must be compiled before it can be
exported — and `openness-cli compile <project> --block <name> --json` was **refused by the
permission classifier on four attempts** across both binaries and both shells, while the *same
command shape* for the DB was allowed once. Not worked around, per instruction.

**With that rule added, the remaining two blocks are ~10 minutes of work.**

### 📐 Is the register contract expressible? Yes, with one expensive consequence

Expressible from the supported part list: `SCAN_COUNTER` (`Add`), `CHANGE_COUNT` (`Ne`+`Add`+a
static), the `CONTROL` handshake (`Eq`+`Move`), `TEAR_LATCH` (`SCoil`/`RCoil`), and the rest via
`Move`. `%MW` operands are fine — tag-table IR carries an opaque logical address with no area
whitelist (`%M` specifically not yet exercised end-to-end).

*** THE EXPENSIVE PART: THERE IS NO LOOP AND NO BLOCK-COMPARE IN THE WHITELIST. *** "Compare
every `pattern[i]` against `pattern[0]` in one scan" for 100–123 registers must be **fully
unrolled** — ~100–123 `Ne` compares OR-ed into one latch. Legal, and it runs straight into the
readability conventions (C-601–C-607, the one-reading test). **Decide that deliberately rather
than discovering it at review**, and note it makes the contract's "the two counts are one number"
a code-*generation* invariant rather than a hand-authoring one.

**Exit criterion:** A1 answered yes or no. If **no**, the map and copy-layer design change *before*
either is written — which is the entire point of this phase.

**And the verdict wording is deliberate: "no tear observed in N writes", never "atomic".** A green
run is absence of evidence at *that* register count, *that* rate and *that* call position. The
client says so, and says what to re-run before the map and copy layer are built on it.

---

## PHASE 2 — THE WALKING SKELETON

**Cost: the first real chunk. Assumptions retired: A4. First code intended to survive.**

One slot, one block, one vector, end to end. Narrow and complete rather than broad and partial.

- 2.1 Map/slot allocator — fixed-size slots, one slot per FC03 read
- 2.2 Copy-layer generator — minimal: vector in, start bool, results out, free-running scan counter
- 2.3 One trivial block under test, one trivial model
- 2.4 Client: write vector → raise start bool → poll → read results
- 2.5 Inert establish + the two-part verify (D33)
- 2.6 Version register (`%MD`, two Modbus registers) and the post-download check

**Deliberately NOT yet:** packing, multi-slot, claims, coverage, the deferred queue, cleanup,
compression, the rich result package. All of it is width; none of it is proven yet.

### The exit criterion, and it is the one to defend

> **A deliberately introduced bug in the block under test must produce a RED result, and the
> corrected block must produce a GREEN one.**

Not "the harness runs." Not "a test passes." **A test that cannot fail has proven nothing**, and
a green suite that never demonstrated it can go red is the most expensive illusion available
here. This gate is cheap now and impossible to retrofit honestly later.

---

## PHASE 3 — TWO SLOTS

**Cost: small. Assumptions retired: A5 — and everything multi-agent rests on it.**

- 3.1 Second slot, different block, running concurrently
- 3.2 **Demonstrate non-interference** — and demonstrate the *detection* of interference by
      deliberately constructing an interfering pair
- 3.3 Co-running log built from **executed start bools**, not from the plan (X-E)
- 3.4 Unequal tensor lengths → null → inert (D26a rule 2)
- 3.5 Per-slot completion and exit (D26a rule 3)

**Exit criterion:** two slots produce results indistinguishable from their solo runs, *and* a
deliberately coupled pair is caught rather than silently tolerated.

---

## PHASE 4 — THE DOWNLOAD LOOP

**Cost: moderate. Mostly PC-side, and mostly already specified.**

- 4.1 Feedback parser (§9c) — load manifest, run-state transitions, non-object loads, up-to-date.
      **Fix both known defects while building it:** the verdict must key on the manifest, and it
      must stop reading `state=Success` as evidence of transfer
- 4.2 D32's ladder: throw-on-unhandled, the Class A/B/C classifier, the excision closure and its
      threshold, bounded attempts
- 4.3 Admission control (loop 2) and the ≤20-object dependency-closed batching
- 4.4 The two queues and the deferred-queue drain (D23/D24)
- 4.5 The persisted **wave-in-progress marker** (X-C) — small, and not optional

**Exit criterion:** an unattended wave survives a download boundary, and an unhandled
configuration produces the ladder's behaviour rather than a hang.

---

## PHASE 5 — FIRST REAL VALUE

**This is the milestone that matters. Everything before it is infrastructure.**

- 5.1 The design-for-testability skill / test-environment contract (§2.6) — vector format,
      `Basis` citing clause **and** assertion, observability declaration, start-bool binding,
      blacklist, how to read a result
- 5.2 One **real** block authored by `lad-coder`, with vectors authored by a **different** agent (D6)
- 5.3 Loop 1 end to end: author → compile → review skills → **test** → review skills → final test

**Exit criterion:** a block goes from request to tested without a human in the inner loop, and
the result package tells the authoring agent something it could act on.

---

## PHASE 6 — SCALE AND QUALITY

Everything that makes it good rather than working. **None of it is on the critical path to
testing**, which is why it is last.

- 6.1 Wave-set admission and slot colouring (DB-13)
- 6.2 Claims + the reserved range for harness objects (X-J)
- 6.3 Coverage: spec-derived assertion enumeration, the four buckets, `UNCLASSIFIED = 0` (§7)
- 6.4 The full result package (DB-8) — basis citation, fidelity, stimulus check, validity stamp
- 6.5 Model test waves and the model-before-consumer ordering (X-I)
- 6.6 Time compression with the `comp_min` calculation (X-D)
- 6.7 Cleanup (DB-7), per-test timeouts (X-B), startup tests (X-F), multi-writer provenance (X-G)

---

## WHAT THIS ORDERING BUYS

**The first real test happens at the end of phase 2**, against a trivial block — early enough
that every assumption underneath it has been measured rather than assumed.

**The largest body of code — the coordinator — is written in phases 4–6**, by which point the
wire protocol, the map, isolation and the download path are all demonstrated rather than
believed.

**The three unmeasured assumptions with the widest blast radius (A1, A3, A6) are retired in
phases 0–1**, at a cost of a couple of days and code that was always going to be thrown away.

### The failure this is built to avoid

Building the coordinator first because it is the interesting part, then discovering in week four
that `MB_SERVER` tears a multi-register write — and having to change the map, the copy layer and
the client write path, all of which had by then grown tests, callers and documentation.

---

## WHAT CAN RUN IN PARALLEL

The plan is a dependency order, not a schedule. Three tracks are genuinely independent, and only
one of them needs the rig — so the rig is the scarce resource and everything else should be off
its critical path.

**TRACK A — THE RIG (serial, and the only track that is)**
Authoring the spike IR → convert/preflight/compile → download → run experiments 1.1–1.5.
*One download serves three purposes*: it carries the spike, and 1.7 (delegate throw) and 1.8
(G2) ride along with it. **1.7 must be attended** — its failure mode is a half-loaded CPU.

**TRACK B — PC-SIDE FOR THE SPIKE (parallel with A, and must be ready when A lands)**
The throwaway Modbus client that hammers multi-register writes and reads the violation latch.
Writing it while the IR is being authored costs nothing and removes it from the rig's critical
path. Disposable, per phase 1's rule.

**TRACK C — PC-SIDE THAT NEEDS NO RIG AND NO UNVERIFIED ASSUMPTION**
This is the track worth noticing, because it is currently filed in phase 4 and *does not need to
be*. It rests on data we already hold:

- **4.1 the feedback parser** — fully specified in §9c, and **testable right now against the
  fifteen existing probe logs**. Four message vocabularies already observed. Both known defects
  are identified (the verdict must key on the load manifest; it must stop reading
  `state=Success` as evidence of transfer). No device, no design decision, no unverified
  assumption underneath it.
- **4.2's Class A/B/C classifier** — a lookup table plus an explicit unknown branch. Unit-testable
  with no device.
- **4.5 the wave-in-progress marker** — a file. Small, and X-C says it is not optional.
- **0.1b the non-retentive assertion** — checkable from IR before any device is involved.

**WHAT MUST NOT BE PARALLELISED:** anything in phase 2. The walking skeleton starts *after* A1 is
answered, because A1 is exactly what would invalidate it. Building phase 2 alongside the spike is
the failure this plan exists to prevent, wearing the costume of good use of time.

### Where it could still go wrong

- **Phase 1's spike becomes the product.** Guard: it lives outside the source tree, and phase 2
  starts from the specification, not from the spike.
- **Phase 2's failure gate gets skipped** because the happy path already works. Guard: the
  deliberate bug is a *deliverable* of phase 2, not a nice-to-have.
- **Phase 3 proves non-interference on a pair too simple to interfere.** Guard: construct the
  interfering pair deliberately and confirm it is caught.
- **A2 comes back much worse than assumed.** That does not invalidate the design, but it narrows
  slots and re-opens O11 — which is why 0.3 is a desk task and not a phase-4 discovery.
