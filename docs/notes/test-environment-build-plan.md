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
| ~~**A1**~~ | ~~`MB_SERVER` single-scan atomicity~~ | ✅ **FULLY RETIRED 2026-08-13 ON THE RIG.** `NO TEAR OBSERVED` at **123 registers** — the widest slot the design can use — on **both** call orders: 3,000 writes VARIANT 1, 3,200 VARIANT 2, `TEAR_LATCH` 0 throughout, `CHANGE_COUNT` exact on every run. **1.4 answered too:** 0 torn reads in 3,000, 3,000 distinct generations. *Still "no tear observed", never "atomic"* — the one surviving limit is that **two writes have never been inside one scan**, which is a round-trip property (78 ms vs a 23 ms scan), not a width one |
| ~~**A2**~~ | ~~Modbus round-trip rate~~ | ✅ **MEASURED (1.2, which also settles 0.3): median 71–78 ms, p99 136–173 ms, and the marginal cost per register is ~zero.** The assumed 79–108 ms understates the tail by ~60% — re-derive O11 and the poll budget on the p99, not the median |
| **A4** | Block driveable by its own command signal | D37, inert, the start-bool mechanism |
| **A5** | Two slots do not interfere | Everything multi-agent |
| **A6** | Delegate throw leaves the CPU untouched (G4) | D32's guard model. A **safety** item |
| **A8** | Structural DB change vs retentives (G2) | DB-1's change-class table, therefore routing |

**PHASE 0 IS CLOSED.** 0.3 deferred by ruling (1.2 measures it as a by-product); 0.4 done and it
redesigned DB-12; everything else retired. The only item carried forward is **0.1b**, which is a
*rule to enforce* when harness objects are generated, not an experiment to run — it lands in
phase 2 with the copy-layer generator.

**The critical path is now A1**, and it runs through the phase-1 spike.

> ### 📍 STANDING STATUS — keep this current; it is the ONE place "where are we" lives
>
> *** PHASES 0, 1 AND 2 ARE CLOSED — PHASE 2 VALIDATED ON THE DEVICE. PHASE 4.3/4.4 BUILT. ***
> Last updated 2026-08-13.
>
> **A1 fully retired** — no tear in 3,000 writes of 123 registers on VARIANT 1, 3,200 on VARIANT 2,
> and 1.4 answered with 0 torn reads in 3,000. **Phase 2's exit criterion met**, GREEN/RED shown by
> executing the generated IR and the defect shown invisible under another vector. **Phase 4.3/4.4 +
> queue persistence built.**
>
> | assumption | state |
> |---|---|
> | A1 `MB_SERVER` atomicity | ✅ retired at full width, both call orders |
> | A2 round-trip rate | ✅ measured; `RTT_typ 78`, `RTT_p99 201`, F-4 says the tail is **not** width-sensitive |
> | A3 `%MW` capacity | ✅ 4096 words, outside work memory |
> | A7 run-state read | ✅ measured in both CPU states |
> | A9 memory budget | ✅ scoped to retain-only by ruling |
> | ~~**A4**~~ block driveable by its own command | ✅ **RETIRED ON THE DEVICE** — all four exit-criterion cells, no interpreter |
> | **A5** two slots do not interfere | ⏳ phase 3, in build |
> | **A6** delegate throw (G4) | 🔴 **UNMEASURED — needs the owner present.** Widest blast radius left |
> | **A8** structural DB change (G2) | ⏸ deferred by the owner |
>
> **Owed on the device:** only the **32-bit build stamp**, blocked by a converter defect (every hex
> literal is typed `Int`) now in fix. The **bit order** and **word order** are measured — `BitAddressOf`
> is right, word order is `HighWordFirst`, and both `[I]` markers come off.
>
> **Adopted and part-implemented:** **F-1** — spec half landed (X-A now says a read never *splits* a
> slot); **code half queued** for `Harness.Map` + `MirrorClient`. **The drain ruling** needs no code.
>
> **Open for the owner:** *** MAY AN AUTHOR CHANGE A BLOCK'S INTERFACE PURELY TO MAKE IT TESTABLE? ***
> (5.1 found the design answers this both ways, and it decides whether design-for-testability is real
> here); **A6/1.7**; **F-2 + F-6 together**; the **queue/marker joint-consistency** ruling; **F-3**;
> **F-5**. *(F-4 closed — nothing argues for capping slot width on timing grounds.)*
>
> **The rule that has earned its place five times in one day:** *a guard written, tested around, and
> never executed.* See `autonomous-working-agreement.md`.

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
| A1 | ~~`MB_SERVER` applies one request's registers within a single scan~~ | ✅ **RETIRED AT FULL WIDTH AND BOTH CALL ORDERS, 2026-08-13.** 3,000 writes × 123 registers VARIANT 1; 3,200 VARIANT 2; 0 torn reads in 3,000 (1.4). Not "atomic": two writes have still never been inside one scan | *(was: the whole X-A atomicity model → map design, copy layer, client write path — **now clear to build at any slot width up to the FC16/FC03 limits**)* |
| A2 | ~~Modbus TCP performs on this rig at roughly the assumed rate~~ | ✅ **MEASURED: median 71–78 ms, p90 89–115, p99 136–173, one 2,216 ms outlier in 2,000. Per-register cost ≈ 0** | *(was: poll budget, slot width, O11/D29 arithmetic — the arithmetic needs re-deriving on the p99, the design does not)* |
| A3 | ~~`%MW` has ~2048 words on a 1214C~~ | ✅ **VERIFIED — it is 4096 words / 8192 bytes**, double the assumption, and **separate from work memory** | *(was: slot sizing, slot count, map layout — the ceiling doubled and the mirror costs no work memory)* |
| A4 | A block can be driven by its own existing command signal | untested | D37, inert, the entire start-bool mechanism |
| A5 | Two slots genuinely do not interfere | untested | Everything multi-agent |
| A6 | A delegate throw leaves the CPU untouched (G4) | **UNMEASURED** | D32's guard model — this one is a *safety* item, not a schedule item |
| A7 | ~~`PlcGetStatus` reports STOP (X-K)~~ | ✅ **MEASURED in both states.** STOP reads as raw `0x03` via Sharp7's catch-all | *(closed; also mechanises R8's device-side confirmation)* |
| A8 | Structural DB change: whole-DB reinit or added tags only (G2) | **VENDOR-CONTRADICTORY** | DB-1's change-class table, therefore routing |

**A1 and A3 are the two that must fall first.** They are cheap to test and expensive to be wrong
about. A6 is scheduled early for a different reason: it is the one whose failure mode is a
damaged rig rather than a wasted week.

*** BOTH HAVE NOW FALLEN, AND SO HAS A2. *** A1 was answered on the rig on 2026-08-12 — the entry
is at the end of phase 1. **A6 is the one that is still unmeasured and is now the widest-blast-radius
unknown left**, and it is still the one that must be done attended.

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

### ✅ THE SPIKE IS ASSEMBLED AND COMPILING — `MB_SERVER` AUTHORED IN IR, 2026-08-12

*** THE FIRST `MB_SERVER` EVER AUTHORED IN IR IS IN THE CONTROLLER. *** Import ✅, compile ✅,
`sanity-check` **PASS** — 73 blocks, `inconsistentBlocks` = the pre-existing sample FC only.

    --port 503        <- NOT 502
    --pattern-count 16

**The four values, grounded not guessed:**

| | value | how |
|---|---|---|
| `InterfaceId` | **64** | Read from the project's **own** `TCON_IP_v4`, via a fresh `export-all`. ⚠️ *That FB has no instance DB and is called from nowhere, so its 64 was never validated by a connection that actually came up — and a wrong `InterfaceId` fails at RUNTIME, not compile.* |
| Connection `ID` | **16#0010** | Enumerated across all 108 exports; the one existing `CONN_OUC` uses `16#0001`. **Residual: connections in the Devices & Networks table are invisible to Openness**, so that space cannot be proven empty. |
| TCP port | **503** | *** 502 WAS NOT TAKEN — the brief's expectation was wrong. *** The existing connection is OUTBOUND (`LocalPort 2000` / `RemotePort 502`, `ActiveEstablished TRUE`). 503 chosen anyway: a collision fails at runtime only, the connection table is invisible, and it costs nothing. Checked against the CPU's reserved passive-TCP list. |
| `MB_HOLD_REG` | `P#M1000.0 WORD 209` | **Span verified before use:** 209 words from MB1000 = MW1000…MW1416, so offsets 0–15 are the pattern and 200–208 the status. **Exact fit, no slack.** |

**The instance decision — a wrapper FB, not a bare `MB_SERVER` instance DB.** `CONNECT` needs a
`TCON_IP_v4` somewhere, and a single-instance DB has no room for it — which would have forced a new
**global DB**, *** EXACTLY WHERE THE STILL-OPEN `MemoryLayout` HOLE BITES *** (an IR-authored DB
imports `Optimized` silently with every check green). The wrapper is also the shape the converter
was just proven against. **Deviating from the proven shape on a first-ever authoring buys nothing.**

**`compare` through the controller:** `Main` **EQUIVALENT**; FB DIFFERS by 1 — TIA *adding* the
`MB_SERVER` type definition (`0→n`, an addition); iDB DIFFERS by 2, both TIA rebuilding the
interface from the FB. *** NO LOSSES ON ANY OF THE THREE, AND `--allow-silent-layout` WAS NEVER
NEEDED *** — the FB's IR declares `MEMORYLAYOUT Optimized`, so the guard was **satisfied rather
than escaped**. `converter diff` through TIA on OB1: `0 changed, 2 added, 0 removed, 7 identical —
INVARIANCE OK`.

#### 🔴 THREE FINDINGS, ONE OF THEM A DESTRUCTIVE TOOL DEFECT

  1. *** `openness-cli create-instance-db` IS BROKEN ON THIS PROJECT AND FAILS DESTRUCTIVELY. ***
     `set_Number` throws under automatic numbering; FI-63's repair path then fires, and the
     `finally { SaveProject(); }` *** COMMITS THE BROKEN `DB0` ***, which can neither compile nor
     export. Three delete-and-retry cycles; FI-63's documented workaround did not help.
     **A repair turned a recoverable state into a hard one** — deserves its own FI.
  2. **An iDB whose FB holds a system-FB instance cannot be authored in IR with its members** —
     `DbSourceWriter` always emits `Remanence`, which TIA refuses there, and `MEMORYLAYOUT` on an
     iDB yields *"Missing XML attribute 'ReadOnly'"* (a real iDB export carries no `MemoryLayout`).
     *** THE WORKAROUND IS ARGUABLY THE BETTER PATTERN: an iDB with an EMPTY member list and no
     `MEMORYLAYOUT`, so TIA rebuilds the interface from the FB and it CANNOT DRIFT BY
     CONSTRUCTION. *** Consequence flagged, not laundered: DB 100 was chosen by us, not assigned.
  3. `to-xml` needed no `--allow-blind-types` — so **CLAUDE.md's note calling `MB_SERVER`
     "characterised but deliberately not registered" is now stale.**

  ⚠️ **Accepted, not fixed:** `review` reports C-001 (`MB_Server` not PascalCase). The name is
  verbatim from the byte-exact template this spike is measured against, and *identity with that
  template is what makes the byte-identical round-trip result mean anything* on a block that exists
  to be deleted. **The only finding of any kind; `Main` is clean.**

### 🧾 THE A1 RUN-SHEET — client verified 2026-08-12, parameters pinned

Client **builds, 52 tests pass**, and the wire format was reviewed offline (`frames`): one FC16 per
iteration, 16 registers, 45 bytes, generation in every register, address windows declared as
`pattern 0..15` / `status 200..208`. Nothing has been sent.

*** THE DEFAULT `--pattern-count` IS 100 AND THE AUTHORED CHECKER IS 16. THE DEFAULT IS WRONG FOR
THIS RIG. *** The contract's own words: *the two counts are one number*. Run it at the default and
the checker inspects 16 registers the client never writes — the surplus hold stale values forever,
the latch trips permanently, and *** IT READS AS "MB_SERVER TEARS" ***, which is the exact false
positive that would send the whole design down a wrong path.

    --pattern-base  0     register 0  == %MW1000   (MB_HOLD_REG = P#M1000.0 WORD 209)
    --pattern-count 16    *** MUST MATCH THE CHECKER. NOT the default 100. ***
    --status-base   200   register 200 == %MW1400, control at 208 == %MW1416
    --port          ???   *** 502 IS LIKELY TAKEN by the existing Modbus server — use whatever
                          the IR lane reports, and do not assume. ***
    --host          the rig, from the device allowlist. No default, deliberately.
    --arm           REQUIRED to open a socket at all; without it the frames print and it exits 10.

The base addresses need no override: the checker's author placed the mirror so the client's
defaults land correctly. **Only `--pattern-count`, `--host` and `--port` change.**

**Order:** `probe` → `tear-write` (1.1) → `timing` (1.2, also settles deferred 0.3) → `scan` (1.3)
→ `tear-read` (1.4) → rebuild with `MB_SERVER` last and repeat for VARIANT 2 (1.5).

*** READ THE VERDICT, NOT THE EXIT CODE: `Stale` MEANS THE EXPERIMENT NEVER RAN *** —
`CHANGE_COUNT` did not advance — and must never be read as a pass. And the finding is worded **"no
tear observed in N writes", never "atomic"**: a green run is absence of evidence at *that* count,
*that* rate and *that* call position.

**Exit criterion:** A1 answered yes or no. If **no**, the map and copy-layer design change *before*
either is written — which is the entire point of this phase.

**And the verdict wording is deliberate: "no tear observed in N writes", never "atomic".** A green
run is absence of evidence at *that* register count, *that* rate and *that* call position. The
client says so, and says what to re-run before the map and copy layer are built on it.

### ✅ A1 IS ANSWERED — 2026-08-12, ON THE RIG. *** NO TEAR OBSERVED IN 3,340 WRITES. ***

*** VERDICT WORD: `NO TEAR OBSERVED`, NEVER `ATOMIC`. *** Three armed runs, VARIANT **1**
(`MB_SERVER` first in scan), **16 registers per FC16, one request never split**: 1,000 + 2,000 +
340 writes, `TEAR_LATCH` **0** at the end of every one, `TEAR_INDEX`/`TEAR_VALUE_A`/`TEAR_VALUE_B`
all 0. **The map, the copy layer and the client write path may be built on the X-A model** — at
this register count, this rate and this call position, and no further.

*** AND THE LIVENESS IS EXACT, WHICH IS THE PART THAT MAKES THE GREEN READABLE: *** `CHANGE_COUNT`
advanced by **precisely the number of writes issued** on every run (1000/1000, 2000/2000,
340/340 — the last confirmed by a follow-up probe reading 2000 → 2340). Not merely "it moved":
**every request landed, in its own scan, and none was lost or coalesced.** No run returned
`Stale`.

*** THE HONEST LIMIT, AND IT IS THE ONE TO RE-TEST BEFORE PHASE 2 WIDENS ANYTHING: *** the round
trip is ~70 ms and the scan is ~23 ms, so the client could never issue more than one FC16 per
**~3 scans**. The experiment therefore samples the *phase* of a write against the scan boundary
3,340 times — which is what makes it meaningful — but it has **never put two writes inside one
scan**. Re-run at the widest slot the design actually uses, and against VARIANT 2.

**1.2 — A2, and it settles the deferred 0.3.** Median **71–78 ms** across the whole sweep;
p90 **89–115 ms**; p99 **136–173 ms**; one **2,216 ms** outlier in 2,000 samples. *** THE
ASSUMED 79–108 ms IS NOT WRONG BUT IT IS THE WRONG SHAPE: it sits between this median and this
p90, and it understates the tail by ~60%. *** Poll budgets keyed on it will hold typically and
miss at the p99. **And the marginal cost per register is indistinguishable from zero** — 1, 4, 8
and 16 registers all cost the same within noise, so *** SLOT WIDTH IS FREE UP TO THE FC03/FC16
LIMITS AND WHAT COSTS IS THE NUMBER OF ROUND TRIPS. *** (Measured over the tunnel, not on a LAN.)

**1.3 — the observability floor is real and small.** Scan **22.64 ms** median idle (n=101, spread
22.20–23.02), **23.33 ms** median under 20 full-block writes per poll (n=16, spread 23.16–23.39).
*** `MB_SERVER` UNDER TRAFFIC COSTS ABOUT +0.7 ms, ~3%. *** The 16-register unrolled checker is
inside that figure.

**1.4 — A NON-RESULT, CORRECTLY REPORTED AS ONE.** Exit 4, `INVALID — the generation never changed`:
385 reads, 0 torn, **1 distinct generation**. *** THAT IS THE STIMULUS CHECK FIRING, NOT A PASS ***
— 1.4 needs the generator build (checker OFF), which does not exist, exactly as 1.5 needs the
VARIANT 2 build. **Zero torn reads against a frozen block is evidence of nothing**, and the client
refused to say otherwise. Both remain open.

#### 🔴 A TRAP FOUND BY WALKING INTO IT: `timing` WITH A SWEEP POINT BELOW `--pattern-count` TRIPS THE LATCH

`timing --sweep 1,4,8,16` against `--pattern-count 16` writes **sub-ranges** of the pattern block,
so registers outside the sweep point keep the previous run's generation — and the checker, which
compares all 16, correctly latches. *** IT PRESENTS EXACTLY AS "MB_SERVER TEARS". *** The client
refuses a sweep point **larger** than the window and says why; it accepts a **smaller** one
silently.

  ➜ **It is diagnosable, and the diagnosis is the useful part:** the latch read
    `TEAR_VALUE_A = 1`, `TEAR_VALUE_B = 1000` at `TEAR_INDEX = 1` — the *first* generation of the
    timing run beside the *last* generation of the write run. *** A GENUINE TEAR PUTS TWO ADJACENT
    GENERATIONS SIDE BY SIDE (g, g−1). 1 AGAINST 1000 CANNOT COME FROM ONE IN-FLIGHT REQUEST. ***
    That is what `TEAR_VALUE_A/B` are for, and without them this run would have been a false
    "A1 fails" — the most expensive wrong answer available here.
  ➜ **Cleared and re-proved rather than argued away:** the next `tear-write` reset the latch, it
    read back 0, and 2,000 further writes left it there. *** THAT ALSO PROVES THE LATCH IS NOT
    STUCK-ON, which a run that never latched could not have shown. ***
  ➜ Either refuse a short sweep point in the client, or have `timing` pad to the full window. The
    register contract's *"the two counts are one number"* has a second edge nobody had named.

**The download that carried it** (`download-probe --options SoftwareOnlyChanges --disruptive`,
exit 0): *** VERIFIED FROM THE LOAD MANIFEST, NOT FROM `state=Success`. *** **19** objects reported
`'X' was loaded successfully` — the checker FC, the wrapper FB, its iDB, OB1, `MB_SERVER` and its
nine `TCP_MB_*` helpers. `StopModules→StopAll` and `StartModules→StartModule` were both answered by
the disruptive allowance; `DataBlockReinitialization` was **never raised**. Run state read back
independently afterwards: *** `Running (8)` *** — keyed on 8, not on the decoder's catch-all.

---

### ✅ A1 RE-OPENED AT THE WIDTH THE DESIGN WILL ACTUALLY USE — 123 REGISTERS, BUILT AND GATED, 2026-08-13

A1's recorded limits were **untested above 16 registers** and **untested on VARIANT 2**. A2 then
measured the marginal cost per register at **~zero**, which pushes the design toward WIDE slots —
so *** THE WIDTH LIMIT IS THE ONE THAT NOW MATTERS ***, and it is the one the phase-2 allocator is
being written against. This lane therefore went straight to **N = 123**, the FC16 write limit
(FC03 reads at most 125), rather than repeating narrow variants the design will never use.

The checker IR is generated from a single `N`, so widening it was cheap. Three findings from doing it:

  1. *** THE ONE-RUNG COMPARISON DOES NOT SCALE, AND WAS CHUNKED. *** At N=16 the checker compared
     all 15 registers in ONE rung and captured in ONE network of 32 MOVEs. At N=123 that is a rung
     of 122 branches and a network of 244 MOVEs — far past anything this project has put through
     TIA. Both are now chunked at sizes already proven to import: **15 comparisons per rung** (9
     rungs OR'd into one `TearNow`) and **≤32 MOVEs per capture network** (8 networks). Every
     register is still read in the same scan, so a disagreement is still a one-scan snapshot — the
     chunking is a transcription change, not a semantic one.
  2. **The capture order survives the chunk boundary.** Registers are still tested from the highest
     index downwards *across* networks as well as within them (122→107, 106→91, … 10→1), so the
     lowest disagreeing index is written last and survives. `TEAR_INDEX` still names the FIRST
     disagreeing register — which is what distinguished a genuine tear from our own sweep artefact
     on 2026-08-12, so it was worth preserving deliberately rather than by luck.
  3. **The server block needed no change at all.** `MB_SERVER`'s `MB_HOLD_REG` is the raw pointer
     `P#M1000.0 WORD 209`, not a tag reference — so renaming every pattern tag and growing the set
     from 16 to 123 left `FB_Comms_ModbusServer` untouched. Placing the status block at offset 200
     was done for exactly this and it paid off.

**Gate: `sanity-check`, never the compile exit code (hard rule 4).** `OVERALL: HEALTHY`,
`BLOCKS: 73 INCONSISTENT: 0`, `TYPES: 33 INCONSISTENT: 0` — both lines, per FI-62. `Main` went
inconsistent when its callee changed (expected) and cleared on a per-block compile. Preflight was
clean on both files. **FC 101 confirmed free by enumeration, not trust.** Also worth recording: the
pre-existing `FC_ModbusTCP_Sample` inconsistency **has cleared** on its own.

**Three builds are prepared, and only one can occupy the project at a time:**

| build | what | state |
|---|---|---|
| **C** | checker, N=123, VARIANT 1 | ✅ imported, compiled, gated HEALTHY. **Awaiting download.** |
| **D** | checker, N=123, VARIANT 2 | IR + XML ready. Differs from C by **exactly one literal**, as predicted. OB1 swap passes `diff --only 8 9`: **7 of 9 networks provably identical**. |
| **A** | generator FC 101, N=123 | IR + XML ready, preflight CLEAN. Writes one incrementing generation into all 123 registers per scan; **holds the tear registers at zero** so a value left by the checker build cannot read as a live detection; republishes the generation as `CHANGE_COUNT` so the client's liveness check works. Checker not called, per 1.4's stated requirement. OB1 passes `diff --only 9`. |

> #### 🔴 THE PROJECT AND THE RIG NOW DISAGREE
>
> The download has **not** happened, so the scratch project holds the **123**-register build while
> the rig still runs the **16**-register one. *** THE CLIENT'S `--pattern-count` FOR BUILD C IS 123,
> NEVER THE OLD 16 *** — at 16 the other 107 registers hold stale values and **latch a tear that
> never happened**, which is the exact false positive that would send the design down a wrong path.

### 🔴 THE `compile` EXIT-CODE FIX HAD NEVER REACHED THE BINARY — FOUND, THEN FIXED, 2026-08-13

Every clean per-block compile in the lane above exited **8** while reporting `ERRORS: 0` and
`Block was successfully compiled`. CLAUDE.md states this was fixed on 2026-08-12: `compile` used to
key on `State != Success` where `compile-all` keys on `ErrorCount`, so on a project carrying a
**permanent hardware warning** — which this one does — **every per-block compile exits 8 regardless
of the block.**

*** THE FIX WAS IN SOURCE AND NOT IN THE BINARY. *** The Release exe was dated **Aug 11**; commit
`76e9f8f` landed **Aug 12**. **This is the FI-73 class, one tool along** — and the reason it
persisted is the opposite of carelessness: Release is deliberately not rebuilt during Portal work,
because a rebuild needs a fresh whitelist approval, so the fix could not land by the ordinary route.

  ➜ *** FI-74's AUTO-APPROVAL CLOSED IT, AND THIS IS THE FIRST TIME IT HAS BEEN USED IN ANGER. ***
    Rebuilt from the clean tested tree once the `openness-cli` lane landed, with a **byte-exact
    backup of the old binary taken first** — approval is keyed on `(Path, FileHash)`, so restoring
    the exact bytes would have restored the old approval had the new build not approved. It was not
    needed: `openness whitelist: 3 approved, 0 already approved`, **with nobody at the machine.**
  ➜ **Verified behaviourally, not by timestamp:** same block, same project, same warning —
    **exit 8 → 0**, and a new `CONSISTENT: yes (re-read after the compile)` line appeared. That
    read-back is FI-52's converse, which shipped in the same commit and **had also never reached a
    caller. Two guards written and tested on 2026-08-12 were reaching nothing.**
  ➜ **The rule this generalises to:** a fix is not landed when it is committed and tested; it is
    landed when the binary the callers actually invoke has it. FI-73 says this for the converter.
    It is now measured true for `openness-cli`, and the approval friction is what made it likelier
    here, not less.

### ✅ THREE MORE SILENT-CHECK HOLES CLOSED, 2026-08-13 — all the same class

  - *** A TAG TABLE WAS NEVER REVIEWED, AND THE REPORT SAID SO IN WORDS NOBODY READ AS A PROBLEM. ***
    All 18 rules reported `not applicable — TAGTABLE rule support not implemented in Phase 1`, and
    the run exited 0. Three rules were meaningful and silently unimplemented (**C-001**, C-005,
    C-406); 15 are genuinely inapplicable and now say *why*, individually. `Skipped` now means "had
    a subject, was not judged" and **gates at exit 2 = REVIEW INCOMPLETE**, because that line had
    been printed on every tag-table review since Phase 1 and read as clean every time — a warning
    demonstrably did not work here. **1010 → 1069 tests.**
  - *** `preflight` NEVER HANDED THE REVIEWER THE REGISTRY IT HAD ALREADY BUILT *** — so **C-118,
    C-122 and C-125 recorded themselves unrunnable on every preflight this tool has ever done.** A
    stepper block reported `0 finding(s)`, exit 0, with three of its most relevant rules never run.
    One call site from the tag-table hole, same shape.
  - **`/review-conventions` was one of those callers** and is fixed (`5031983`): its own scope list
    already named C-118/C-122/C-125 as in scope while its invocation omitted `--project`. Checked
    for further instances — there are none; `gen-block-new` goes through `preflight`, which was
    fixed at source.

### ✅ PHASE 2 STARTED — 2.1, 2.2 AND 0.1b BUILT, 2026-08-13 (`deb2332`, `3ed93ff`)

`src/harness/Harness.Map`. **221 → 280 tests**, all green. The generated IR was put through the real
converter and survives `to-xml` → `to-ir --no-sidecar` **byte-identically**, so the golden strings
are a form the toolchain accepts rather than one that merely looks plausible.

**Three places where measurement contradicted the plan:**

  1. *** 0.1b IS TWO RULES, NOT ONE, AND THE PLAN'S FRAMING FITS ONLY ONE OF THEM. *** It assumes
     retain is an *attribute* (per-tag on optimized blocks, all-or-nothing on standard ones). **A
     `%M` tag has no retain attribute at all** — `ir/SPEC.md`'s TAGTABLE grammar has no Remanence
     concept. For the mirror, **non-retentiveness IS THE ADDRESS**. A checker implementing only the
     attribute rule **passes a mirror sitting on MB0**, which is the failure it exists to prevent.
  2. **0.1b is not fully checkable from IR**, contrary to its plan entry. The `MemoryLayout` branch
     cannot be: a re-import silently reverts Standard→Optimized, the export carries no element, and
     `drift-check` is structurally blind. It needs a device-side `block-layout --set Standard` +
     `--expect Standard` leg. Not yet exposed (phase 2 generates no DB) — **it bites at the first
     harness DB.**
  3. **X-A's "forty slots" only holds with the base near MB0**, which X-A's own placement rule
     pushes against — at MB4000 it is **twenty**. The program's retentive extent is therefore a
     direct input to slots-per-wave, and `RetentiveBytes` is now required with no default.

  ➜ **Unmeasured and load-bearing, one write settles it:** the bit order *within* the start-bool
    register is **inferred from big-endianness, not measured** — A1 compared whole words and never
    tested bit order. Benign at one slot (a start bool that never rises is a detectable non-event);
    **wrong at 9+ slots, where slot 0 and slot 8 swap.** Isolated to `MirrorGeometry.BitAddressOf`.

### ✅ THE ARITHMETIC RE-DERIVED ON THE MEASURED NUMBERS — new §12a, 2026-08-13 (`34fdd4d`)

The spec now has **one place** where a timing constant is chosen (`RTT_typ 78`, **`RTT_p99 173` for
every budget/cap/timeout**, `RTT_max 2216` for timeouts, `scan 23.33` loaded); every other site
defers to it, and a `[D]` marker was added so derived figures stop wearing `[M]`.

  - *** O11's SHAPE INVERTS. *** `K_max = floor(S_min × 23.33 / 173)`: S=10 → **1 slot**, 50 → 6,
    100 → 13 — and **all-latched has no cap at all.** On the median the old table reads 3–5× too
    generous, and its failure mode is **a missed assertion reported as a pass.**
  - **A poll IS one round trip.** There was never a "~100 ms poll" parameter to tune; the period is
    `K × RTT`. §12's conclusion survives but its margin is under half what was argued.
  - **The 1-in-2,000 outlier is decisive for timeouts**, not for duration: +3% on a wave, but X-B's
    backstop must be `(scans × 23.33) + (round trips × 173) + 2216`. **Without the last term a
    healthy test reports TIMED-OUT every ~22nd wave.**
  - **X-D's compression ceiling falls from 10× to 4.3×** (timer floor 50 → 116.7 ms).
  - **`D29 does not invert`** — its `K × RTT` was always round-trip-shaped. What changes is that
    "width" means *slots*, never registers, and the cap is a function rather than a constant.
  - *** THE EXTRAPOLATION IS NAMED RATHER THAN BURIED: "width is free to 125/123" is `[I]`, measured
    only to 16 registers. *** Which is exactly what build C above is for.

### ✅✅ A1 IS FULLY RETIRED, AND 1.4 IS ANSWERED — 2026-08-13, ON THE RIG

Three builds, three downloads, all three verdicts read from the device.

| # | run | verdict |
|---|---|---|
| **1.1** | N=123, **VARIANT 1** | *** NO TEAR OBSERVED in 3,000 writes of 123 registers. *** `TEAR_LATCH` 0 on both runs |
| **1.1 / 1.5** | N=123, **VARIANT 2** | *** NO TEAR OBSERVED in 3,200 writes. *** Variant confirmed **from the device** — `--expect-variant 2` against a published `VARIANT = 2`, which is what proves the OB1 swap actually reached the controller rather than merely the project |
| **1.4** | generator build, N=123 | *** NO TORN READ OBSERVED in 3,000 reads — 0 torn, 3,000 DISTINCT GENERATIONS. *** The earlier exit-4 non-result was the stimulus check working; with a generator running it passes decisively |

**Liveness was exact on every run** — `CHANGE_COUNT` matched the issued count precisely, every time.
No run returned `Stale`. **Both of A1's recorded limits are retired.** The limit that survives is
unchanged and is a *round-trip* property rather than a width one: **two writes have still never been
put inside one scan**, because the round trip is ~78 ms against a ~23 ms scan.

  - ✅ *** "SLOT WIDTH IS FREE" NOW HOLDS AT THE LIMIT RATHER THAN BEING EXTRAPOLATED TO IT ***:
    ~0.040 ms per register on writes, no trend at all on reads. §12a's `[I]` on this retires.
  - ✅ **The 123-register chunked checker costs ~+0.17 ms of scan** against the 16-register one —
    **under 1%.** V1 and V2 scan times are indistinguishable. The chunking was free.
  - ✅ **Confirmed across all three downloads: no stop, no start, and `M` MEMORY SURVIVES.**
    `SCAN_COUNTER` continued unbroken and `CHANGE_COUNT` held at exactly 400 across a download. The
    2-object result from earlier generalises.

### 🔴 `RTT_p99 = 173` IS TOO LOW AT FULL WIDTH — the tail did not scale like the median

§12a's p99 was derived from the **16-register** sweep. Re-measured at **123**: **157 / 201 / 168 /
185 ms** across four runs — **two of the four exceed 173**, n-weighted **~183**. `RTT_typ = 78` is
confirmed (medians 75–77) and needs no change.

*** THIS IS THE CASE THE `[D]`/`[M]`/`[I]` CONVENTION WAS ADDED FOR: a figure derived at one width
and applied at another. *** The median scaled and the tail did not. Correction in flight; O11, X-B's
timeout backstop, X-D's ceiling and §8's wave durations all key on this constant, and phase 4's
admission control is already built against it.

### 🔴 TWO WAYS THE RIG HANDED BACK A CONFIDENT WRONG ANSWER

  1. **The first probe read `TEAR_LATCH = 1` and it was not a tear.** `INDEX 16, A=340, B=0` — a new
     **wide** checker meeting the **old 16-wide** mirror, whose stale values survived the download.
     `TEAR_VALUE_A/B` diagnosed it in one read, for the **third** time. *** IT IS ALSO POSITIVE
     EVIDENCE THAT THE CHECKER REALLY DOES INSPECT ALL 123 *** — a checker still looking at 16 could
     not have seen index 16 at all.
  2. 🔴 *** THE CLIENT PRINTED "A1 IS FALSE — MB_SERVER TORE" OFF A PRE-EXISTING LATCH. ***
     `tear-write --no-reset` computed a full tear verdict from a latch set before write 1, and
     **fabricated a plausible supporting detail** — "first observed after ~5 writes" — for an event
     that predated the run. That detail is what would have made someone believe it. **One flag away
     from the most expensive wrong answer available in this project.** Being fixed: the run must
     refuse rather than interpret prior state as its own result, and exit 4 — which is not a
     redefinition, since exit 4 already means *"it ran, but the result proves nothing"*.

### 🔴 THE `timing` MIRROR-RESTORE COULD NEVER HAVE RUN — a guard written, believed, never executed

Added earlier the same day to stop a short sweep leaving a latched checker. It never worked on **any**
invocation: `CmdTiming` **declared no status window**, so the client's own address fence refused its
own `CONTROL` write — and the tool exited 1 on a usage error *before* its own "MIRROR WAS NOT LEFT
CLEAN" check could fire. So the mirror was restored and the latch was not, and the guard that existed
to say so was itself unreachable.

*** THIRD INSTANCE OF THE SAME CLASS IN ONE DAY *** — after `compile`'s exit-code fix that never
reached its binary and the tag-table rules that reported "not applicable". **Writing a guard, testing
the code around it, and never executing the guard itself is this project's most reliable failure
mode.** Fixed and **re-verified on the rig**, which is the only thing that could have established it.

### 📍 RIG AND PROJECT STATE

**Build D on the rig** (checker, N=123, VARIANT 2), CPU `Running (8)`, latch clear, project and rig in
sync. Deliberately restored off build A rather than left on it, and the reason is worth keeping:
**the generator holds `TEAR_LATCH` at 0 *and* republishes `CHANGE_COUNT`, so a `tear-write` against
build A would report "no tear observed" with full liveness while no checker runs** — the stimulus
check passing because the thing publishing liveness is not the thing that detects the fault.
`FC_Comms_ModbusTearGen` (FC 101) remains in the project **uncalled**, so `BLOCKS:` is now **74**.

### 🔧 `RTT_p99` CORRECTED TO 201 ms — and the width explanation was DECLINED, 2026-08-13 (`51e3730`)

`RTT_p99` was derived from the 16-register sweep and re-measured at 123: **157 / 201 / 168 / 185**.
It is now **201 — the worst observed run p99, not the n-weighted 183** — and the choice is argued in
§12a rather than made silently:

  1. **The statistics land there anyway.** Mean 177.75, SD 19.3, SE 9.7 → a ~95% upper bound on the
     mean run-p99 is **≈197**. Taking the worst observation is not pessimism stacked on statistics,
     it is where the statistics already point.
  2. *** THE COST ASYMMETRY IS ONE-SIDED. *** Too high costs a few slots of width and slightly longer
     backstops — both cheap, and derivation 4 had already established that a generous timeout is
     free. Too low costs **a missed assertion reported as a pass.**
  3. **The run-to-run spread (44 ms) exceeds the correction being made (10–28 ms)** — the dominant
     uncertainty is sampling, not the constant.

**Recorded as the weakest constant in §12a**, confidence low-to-moderate: it is a 1-in-100 statistic
from four runs, and no figure derived from it should be quoted to three significant figures. More
runs is the only thing that firms it up.

> #### 🚩 THE EXPLANATION WAS REFUSED, AND THAT REFUSAL IS THE POINT
>
> It would have been natural to write *"the tail widens with register width"*. **That is not
> supportable from this data:** one of the four full-width runs came in at **157, below the old 173**,
> and the run-to-run spread exceeds the shift being explained. The honest statement is a comparison
> of two measurements, not a mechanism.
>
> *** THE SETTLING MEASUREMENT IS INTERLEAVED NARROW AND WIDE RUNS IN ONE SESSION *** — same rig,
> same session, alternating, so session-level variation cannot masquerade as a width effect. Until
> that exists, "width causes it" is inference wearing a measurement's clothes, which is exactly what
> the working agreement forbids.

### 🔴 "THE MARGINAL COST PER REGISTER IS ZERO" IS NOW FALSE AS STATED — the conclusion survives by 16x

Measured at full width: **~0.040 ms per register**, i.e. **~4.9 ms across a 123-register slot**. That
is **~6% of a round trip**, against **100%** for a second round trip.

*** SO THE DESIGN CONCLUSION IS UNCHANGED AND IS NOW MEASURED RATHER THAN ASSUMED: WIDTH IS CHEAP,
ROUND TRIPS ARE NOT — by a factor of ~16, not by infinity. *** Batching wins by a measured 16x/41x
rather than an assumed unboundedness. Anywhere the phrase "free" or "indistinguishable from zero"
appears, it should now read **"~6% of a round trip"**, because a claim of *zero* invites a design
that adds width without limit and there is a real, if small, per-register term.

### 📐 SHAPE OR MAGNITUDE — *** NO CONCLUSION CHANGED SHAPE ***

§12a now carries this table explicitly for the lanes building on it:

| | was → now |
|---|---|
| `floor_p99` | 7.4 → **8.6** scans (persist **≥9**, was ≥8) |
| `K_max` at S ≥ 50 | **−14%**: 6→5, 13→11, 26→23 |
| `K_max` at S = 10 / 20 | *** UNCHANGED — and these are the rows that bite hardest *** |
| wire term / §8 fastest row | 16.4 → **19.1 s** / 71 → **74 s** |
| X-B backstop | +16%, the `RTT_p99` term only |
| X-D **timer** term | **unchanged — and it is the term that binds first** |
| `RTT_typ`, `scan`, `RTT_max`, the 3,000 ms timeout floor, the outlier rate | **all unchanged** |

  ➜ **Who must act:** a lane holding a *named* `RTT_p99` needs a one-line change. *** ANYTHING
    HOLDING A BAKED-IN `K_max` IS NOW OVER-ADMITTING AT S ≥ 50 — WHICH IS RE-COLOURING, NOT
    RE-COSTING *** — so phase 4's admission control needs a look. Batching's conclusion is untouched.
    The harness allocator needs no action unless it derives a poll budget from width.
  ➜ **F-3 strengthened:** an all-latched wave set is *indifferent* to whether the p99 is 173, 201 or
    250 — worth having while the constant is this uncertain.
  ➜ 🚩 **NEW, OWNER'S: F-4 — is the tail width-sensitive, and should slot width be capped below the
    FC03 limit?** First action is the interleaved measurement, **not a policy**: deciding it from
    four confounded runs is precisely the inference-as-measurement this project forbids.

### 🔴 THE GUARD REFUSED ITS OWN PRESCRIBED RECOVERY — 2026-08-13, found only by executing it

The `tear-write` guard added earlier the same day (refuse a verdict computed from a latch the run did
not cause) was **mutation-tested four ways and correct every time.** Then its advice was followed
verbatim on the device:

```
*** exit=4 ***  VERDICT: INVALID - THE TEAR LATCH PREDATES THIS RUN.
```

*** THE RECOVERY IT PRINTED NAMED THE ONE COMMAND THE GUARD BLOCKS IN EXACTLY THAT STATE. ***
`tear-write` is the only obvious full-width writer, and the guard refuses it precisely when it is
needed. The advice was circular and the recovery **could not be performed at all.**

  ➜ **The tests checked the DECISION. Nobody had read the SENTENCE while standing in the situation it
    describes.** A refusal message is a second artifact and it is untested by anything that tests the
    logic. *** THIS IS THE FOURTH INSTANCE OF THE DAY'S CLASS *** and the standing rule in
    `autonomous-working-agreement.md` now carries it: execute the guard's own advice, out loud, on
    the device.
  ➜ **Fixed and re-verified by re-provoking and following the printed text verbatim.** The advice now
    names `timing --sweep <N> --op write` as the primer — `timing` has no verdict to protect — states
    outright that `tear-write --no-reset` is refused *"which is why it is not the advice"*, and warns
    the sweep must equal `--pattern-count`. 70 → 72 tests.
  ➜ **All three `LatchOrigin` outcomes are now observed on the device**, not only in tests, including
    the `ClearedByReset` branch. And the documented principle held under live conditions: the reset is
    acknowledged once the block agrees, and was not before.

**The trap is confirmed real rather than merely remembered.** `--pattern-count 16` against the
123-wide checker latched immediately (`TEAR_INDEX 16, A=1, B=20`) and printed `A1 IS FALSE` — a
legitimate command with one parameter wrong. What is new is that **the verdict now arrives carrying
the reason to disbelieve it**: the adjacency warning fires on the run's own result and the onset line
reports its resolution honestly. The guard correctly does *not* suppress that one — that run genuinely
caused that latch.

> #### 📌 The one inexact liveness figure of the day, and why it is benign
>
> `CHANGE_COUNT = 29` for 30 writes. **Mechanism established by controlled contrast, not inferred from
> sequence:** a primer of `--iterations 1` leaves the block on generation 1, and `tear-write` also
> *starts* at generation 1, so its first write does not change `pattern[0]`. A primer of
> `--iterations 2` gives 30/30. Benign — the verdict is unaffected and the liveness gate only fails on
> `changes == 0`. **Deliberately not code-fixed:** the remedy is one flag at the call site, and
> hard-coding `--iterations 2` into the advice would be a magic number defended by nothing. Recorded
> because exact liveness was claimed earlier and this is its one documented exception.

### 🔧 `RTT_p99 = 201` RE-BASED ON PROVENANCE, NOT ON VALUE — 2026-08-13 (`98bd6b3`)

F-4 did not change the constant. *** IT CHANGED WHAT KIND OF NUMBER IT IS. *** 201 is a **session**
figure, not a width figure — session-to-session variation alone accounts for the whole 173 → 201
move — and the pessimistic choice stands on its original cost-asymmetry argument, now strengthened
by the 136–562 ms range measured *within a single width*.

  ➜ *** THE REVISIT TRIGGER IS REWRITTEN FROM "MORE RUNS" TO **BREADTH** — many sessions, at
    different times of day. *** The old trigger was disproved by F-4 itself: 16,200 round trips in
    one session could not resolve 28 ms at p99, so **one session measures one draw of what the link
    was doing that afternoon, however large it is.**
  ➜ **Three tail caveats withdrawn** (derivation 6, X-A, and the shape-or-magnitude table's *"unless
    it derives a poll budget from width"*), and *** NOTHING ARGUES FOR CAPPING SLOT WIDTH ON TIMING
    GROUNDS *** is now stated plainly in three places.
  ➜ *** NO LANE HAS TO ACT — AND THAT IS THE POINT. *** An unchanged constant needs no propagation,
    which is precisely why the causal claim was worth refusing rather than writing down. Had "the
    tail widens with width" been recorded, every lane would have been asked to revisit a cap that
    the measurement does not support.
  ➜ **The +1.0 ms median move is kept as a real, tiny body effect** — and noted as ~4x *smaller*
    than the across-session per-register estimate it refines (~0.009 vs ~0.040 ms/register). The
    conservative figure stays the headline everywhere, so **nothing in the document turns on which
    is right.**

### 📐 F-4 ANSWERED — NO WIDTH EFFECT ON THE TAIL IS DETECTABLE (2026-08-13)

Interleaved cycles of **16 → 64 → 123** registers, never blocked, 10 repeats read / 8 write, 300
iterations each — **16,200 round trips in one session**, percentiles nearest-rank so they compare
directly with everything already recorded. A middle width was included to separate a graded effect
from a step.

| pooled p99 | w16 | w64 | w123 |
|---|---|---|---|
| **READ** | **188** | 164 | **158** |
| **WRITE** | **156** | 152 | **195** |

*** THE HIGHEST TAIL IS AT WIDTH 16 IN ONE ARM AND WIDTH 123 IN THE OTHER. *** Per-run p99 gives
**−47.5 ms** and **+51.3 ms** — opposite signs of similar magnitude, both confidence intervals
spanning zero. All six cells fall in 152–195, **bracketing both 173 and 201.**

**Two design choices that make the comparison mean something:**

  - **The read arm led**, because `timing --op read` never writes and therefore **cannot latch at any
    width** — no priming, no recovery, and the checker does identical work in every run.
  - **The write arm was pre-latched deliberately.** A partial-width write latches, and *a latched
    checker skips its comparison* — so a narrow arm would otherwise run against a **lighter PLC scan**
    than a wide one. Pre-latching, with `--pattern-count == --sweep` so the restore never fired
    mid-experiment, made that condition **uniform across widths rather than confounded with them.**

#### The limits, stated rather than glossed

  - *** THE DESIGN IS NOT POWERED FOR 28 ms AT p99 *** — smallest detectable difference **83 ms**
    (read) and **145 ms** (write), because a per-run p99 is itself an extreme-value statistic (per-run
    SDs of 92 and 146 ms). Reaching 10 ms of standard error that way needs ~170 runs per arm, roughly
    **17 hours**. This is not a strong null and is not presented as one.
  - **p90 IS adequately powered** (13 and 21 ms detectable) and gives **+0.5 and +3.4 ms** — *** a
    28 ms width effect at p90 is EXCLUDED in both arms. *** That is where the conclusion rests.
  - **And one observation needs no power argument at all:** at a **single fixed width**, inside one
    session, per-run p99 ranged **136 → 371 ms** (read w16) and **136 → 562 ms** (write w123). *** THE
    SPREAD WITHIN ONE WIDTH IS AN ORDER OF MAGNITUDE LARGER THAN THE 28 ms BEING EXPLAINED. ***
  - The **body** does move, reproducibly and tinily: median **74.8 → 75.7 ms** from width 16 to 123 —
    **the same +1.0 ms in both arms.**

#### What follows

  ➜ *** THE 173 → 201 MOVE IS NOT SUPPORTABLE AS A WIDTH FINDING. *** Session-to-session variation
    alone can produce it. **That does not make 201 wrong — it makes its provenance different from what
    §12a implied.** The constant should be revisited on **BREADTH** (many sessions, different times of
    day), **not on width**: one session cannot answer how bad the tail gets across days.
  ➜ **Nothing here argues for capping slot width on timing grounds.** F-4 asked two questions and only
    the first is answered; the cap remains the owner's call.
  ➜ 🚩 **PROPOSED, OWNER'S: specify the tail as p90 PLUS AN EXPLICIT EXCEEDANCE RATE** ("fraction of
    round trips over 250 ms") rather than as a p99 point estimate. **An exceedance rate pools across
    runs without inheriting an order statistic's variance** — whereas a p99 point estimate is exactly
    what defeated a 16,200-round-trip experiment. Persuasive, and deliberately **not adopted**: it
    changes how every budget in §12a is expressed.

### 🎯 OWNER RULINGS, 2026-08-13 — F-1 ADOPTED, AND DB-4's TWENTY DOES NOT BIND THE DRAIN

Both were flagged for the owner rather than decided, and both are now **adopted**.

#### ✅ ADOPTED — DB-4's twenty does not apply to the drain

The reasoning stands as the phase-4 lane put it, and the part worth keeping is that *** IT IS
SETTLED BY A MEASUREMENT ALREADY IN THE SPEC RATHER THAN BY INFERENCE. *** DB-4's own mechanism is
integration **"in one program cycle"** — DB-3 names it in Siemens' notation, `RUN (<21)`, and the
hazard is a program running as a **mixture of old and new blocks**. The drain's download stops the
CPU, so **there is no executing program for the rule to protect**; and §9a already records a full
download that loaded **99 objects**, predicted count matching manifest count exactly — *a run that
could not have happened if the limit bound full downloads*.

  ➜ **The alternative reading is not merely unnecessary, it is incoherent with D25:** *"download
    everything"* and *"download at most twenty objects"* cannot both hold on a 99-object project.
  ➜ **Replaced by a positive check that is already specified**, not by a precautionary rule: compare
    `download-plan`'s predicted count against §9c's load-manifest count (1 vs 1 and 99 vs 99 on the
    measured runs). **What would nail it down:** one full download of a project materially larger
    than 99 objects, same comparison.
  ➜ **Costs no code change** — the planner is `WaveBoundaryBatchPlanner` and the drain path never
    calls it. *Rejecting* the ruling would have required writing a new planner.

#### ✅ ADOPTED — F-1: one FC03 may cover several WHOLE slots

*** X-A's RULE CHANGES FROM "A READ NEVER STRADDLES A SLOT" TO "A READ NEVER **SPLITS** A SLOT". ***
The coherence argument that motivated the original wording survives intact — what must never happen
is a read returning *part* of a slot, and a read covering several **entire** slots does not do that.

**Why it is worth the change:** with per-register cost measured at ~0.040 ms (~6% of a round trip)
and **round trips the scarce resource**, a slot narrower than the FC03 limit wastes most of a read.
Reading several whole slots in one FC03 is a **5x-class win on narrow slots** — and it is the same
lesson as §12a's batching derivation, applied to the map rather than to tag reads.

**What it touches, and the order to do it in:**

  1. **The spec** — X-A's wording, and any clause that phrases the rule as "straddles". §12a's
     round-trip budgets get *cheaper*, not dearer, so no budget breaks; the derivations that count
     round trips per wave need re-running with slots-per-read as a parameter rather than 1.
  2. **The map allocator** (`Harness.Map`) — result regions are already laid out contiguously as
     `[vectors N×W][results N×R]`, so this is **additive rather than a re-layout**: what is needed is
     the rule that a read is expressed in whole slots and the arithmetic for how many fit in 125.
  3. **`MirrorClient`** — it already exposes slot indices rather than registers, which is exactly the
     abstraction this needs; it gains a multi-slot read that cannot express a partial one.

  ➜ *** THE PROPERTY TO PRESERVE IS THAT A SPLIT READ REMAINS UNEXPRESSIBLE, NOT MERELY REJECTED. ***
    `MirrorClient` was deliberately built so a write outside the region is *unaddressable* rather
    than caught by a check someone could skip. The multi-slot read must be built the same way — the
    win is a performance change, and it must not quietly become a hole in the coherence guarantee
    that A1 was measured to establish.

### ✅ F-1's SPEC HALF LANDED — and padding a slot is no longer free (2026-08-13, `f1afac9`)

**X-A's rule is now *"a read never SPLITS a slot"*.** The old wording had propagated to **five sites**;
all rewritten, two historical references kept and marked. The coherence argument is carried *in* the
new wording rather than left implicit: *** THE HAZARD WAS NEVER TOUCHING TWO SLOTS, IT WAS TOUCHING
HALF OF ONE *** — so the old rule forbade a **superset** of the real hazard, at a cost in round trips.

*** WRITTEN AS A PROPERTY OF EXPRESSION, NOT AS A CONSTRAINT TO VALIDATE. *** Reads are addressed in
slot units — `read(first_slot, slot_count)` — and the register range is **derived, never supplied**,
so there is no parameter in which a partial slot could be named. The spec says outright that this must
**not** be built as a validator over a register-range read, because `read(start, length)` plus an
assert *keeps the unsafe call alive and one refactor from being reached*. `MirrorClient` is cited as
the precedent. Mirrored into the client spec as a new §6a.

**The derivations now carry `R = floor(125/W)` as a parameter:**

    round trips/index = K·ceil(W/123) + P·ceil(K/R) + 1
    K_max             = R · floor(S_min·scan / RTT_p99)

At §8's shape, **19 → 11 → 9** round trips per index at W = 123 / 25 / 20:

| | was → now | factor |
|---|---|---|
| read term alone | 12 → 4 or 2 | **3–6x** |
| round trips per index | 19 → 11 or 9 | **1.7–2.1x** |
| wave duration | 74 s → 66 s or 64 s | **11–13%** |

**Diluted by three real things**, and worth knowing so the 5x is not quoted as a slogan: writes and
commit are untouched; `ceil(K/R)` is a **step**, so 6 slots at R=5 needs *two* reads, not 1.2; and a
wave is mostly inert-and-test time. *** THE FULL FACTOR LANDS UNDILUTED IN EXACTLY ONE PLACE — O11's
CAP. *** At S=10 scans, the row where the cap bit hardest and where the p99 correction could not help,
**a 20-register slot admits six slots where a padded 123-register one admits one.**

  ➜ **What F-1 does NOT change**, stated explicitly in X-A: the write side, A1's guarantee, per-slot
    coherence, the 125-register ceiling. Also recorded honestly: **A1's evidence is narrower than the
    design already uses** (16 registers per FC16, one request never split) — a pre-existing gap,
    neither widened nor closed here.
  ➜ **F-1 RELOCATES THE BOTTLENECK: writes are now 6 of 9 round trips.** Recorded so the next person
    hunting wire savings does not look at the read term — and explicitly *not* an invitation to batch
    writes, which is where A1's guarantee lives.

> #### 🚩 NEW, OWNER'S — F-6: PADDING A SLOT IS NO LONGER FREE
>
> X-A's *"the small slots pay nothing for the padding"* was written yesterday and was **true when
> every slot cost one FC03**. Under F-1 it is **false**: *** ONE WIDE SLOT IN A FIXED-SIZE WAVE SET
> COLLAPSES `R` FOR EVERY SLOT IN IT. *** So whether DB-13 should group admission **by slot size** is
> now a live question rather than an optimisation.
>
> **F-2 and F-6 are the same conversation — rule them together.** F-2 (DB-13's max-width input is
> per-set, not the scalar D36 requires) is *strengthened and more urgent*, because the cap now depends
> on **two** per-set quantities and a scalar input is further from adequate than it was.

  ➜ **F-3** narrows to a purely correctness-shaped decision: its throughput argument is weakened
    (sampled assertions are R× more affordable), its correctness argument untouched — the 95-scan gap
    is an **outlier** property, not a read-cycle one.
  ➜ **F-5** is independent, and F-1 makes its case no harder.
  ➜ *** F-4 IS WHAT MAKES F-1 SAFE, AND THE TWO SHOULD BE READ AS A PAIR. *** Had the tail grown with
    width, a 125-register read would have cost tail latency and the saving would have been partly
    repaid **in the exact currency the budgets are denominated in.**

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

### ✅ PHASE 2 IS BUILT — 2.3 THROUGH 2.6, AND THE EXIT CRITERION IS DEMONSTRATED (2026-08-13)

`8f9e65e`, `2a4573c`. **280 → 375 tests**, and `harness.sln` now runs **five** assemblies rather than
three — worth knowing, because a `tail` of `dotnet test` shows only the last one.

  - **2.3** `Harness.Skeleton` — an FC that ramps a count to a limit while its own start command is
    on, plus a model written from the block's **specification**: it never names an operator, a rung or
    an address. That is what keeps the model an independent statement of intent rather than a
    transcription of the code it checks.
  - **2.4** `Harness.Wire`, on **NModbus** (phase 1's raw sockets were a deliberate one-off so a
    library silently retrying or splitting could not be mistaken for the PLC tearing). `MirrorClient`
    exposes **slot indices, not registers**, so a write outside the region is *unaddressable* rather
    than rejected by a check someone could skip.
  - **2.5** Both of D33's checks and D37's later-scan commit, enforced against the observed scan
    counter.
  - **2.6** Version register: a `%MD` literal in the copy layer, and a client check classifying
    Confirmed / Absent / Stale / **WordOrderSuspect** / Unsettled, with a settling window it measured.

> #### 🎯 THE EXIT CRITERION, AND HOW IT WAS MET
>
> *** GREEN/RED IS DEMONSTRATED BY EXECUTING THE GENERATED IR TEXT *** through a small interpreter for
> the subset the generators emit — **not by flipping a C# flag**, which would have tested the harness
> and not the logic. The two builds differ by **one line**, which converts to a different SimaticML
> `Part` (`Lt` → `Le`, verified through the real converter).
>
> **And the defect is also shown INVISIBLE under a different vector** — so the RED is not a harness
> that reddens everything, which is the way this gate is usually failed while looking passed.

**Still owed on the device**, and stated as owed rather than assumed: that both builds compile and
download and give the same two results; the **start-bool bit order** (still `[I]` — the simulator and
`BitAddressOf` agree, but *both from the same premise*, so they are not independent); the 32-bit word
order; and `MB_SERVER` against this map.

#### 🔴 The 0.1b correction — and the brief that asked for it was half wrong

The dispatch said the address rule was unimplemented. **It was not — the previous lane had built it.**
What was real is the second half: *** THE ADDRESS RULE COULD BE SATISFIED VACUOUSLY THREE WAYS *** —
an object set with no address at all (passed with the mirror rule never executing), a declared tag line
with no parseable address (silently skipped), and an absolute address written outside a tag table
(never scanned). `AddressesExamined` is now a **separate denominator** and `Passed` requires both to be
non-zero, with no relaxing flag. Which is the same shape as everything else found today: the rule was
right, and *nothing established that it had run.*

#### Two admissions from the lane's own testing, both worth keeping

  1. *** ONE OF ITS OWN TESTS COULD NOT FAIL. *** Neutering the one-transaction commit into a
     per-register loop left the suite green — the test used a **3-slot** map whose start bools fit in
     a single register, so the loop and the transaction were indistinguishable. Rewritten against 17
     slots. **A test that cannot fail has proven nothing**, and only mutation found it.
  2. **The negative-test script itself had a defect:** restoring a file with `mv` gave it an older
     mtime than the build output, so MSBuild served a **stale binary** and a clean tree reported a
     failure. Every break still compiled, and the two runs that could have been contaminated were
     re-run clean and agreed. *Recorded rather than quietly re-run* — the tooling that verifies the
     tests is not itself above suspicion.

#### Four things the spec and plan do not say

  1. **The minimal copy layer lags results by one scan** (it runs before the block). Invisible beneath
     the 3.3-scan sampling floor, but a real property of 2.2's design rather than an accident.
  2. *** A COMPLETION FLAG IS NOT A SETTLING SIGNAL. *** The defective build raises `Done` at 10 and
     then ramps on to 15. **A faster-than-floor poll would read mid-ramp and report a WRONG ANSWER,
     not an error** — the worst available outcome, and the one the observability floor otherwise
     hides. Asserted, so a future change fails a test.
  3. §9's two-register version register is only free to check **if placed in the control region**;
     anywhere else DB-6's per-batch check costs a round trip.
  4. `DWord` tags and `16#........` literals round-trip byte-identically — **§9's own worked line had
     never been through the toolchain, and its pre-audit `%MW` form would not have compiled.**

All five generated artifacts round-trip `to-xml` → `to-ir --no-sidecar` byte-identically through the
Release converter. Nothing written into `ir/`; no rig or Portal contact; `NModbusTransport.Connect`
has never been called.

### ✅✅ PHASE 2 IS VALIDATED ON THE DEVICE — ALL FOUR CELLS, 2026-08-13

| build | vector | count | model | verdict |
|---|---|---|---|---|
| GREEN (`<`) | step 5 | **10** | 10 | **GREEN** |
| GREEN | step 3 | 12 | 12 | GREEN |
| RED (`<=`) | step 5 | **15** | 10 | **RED** |
| RED | step 3 | 12 | 12 | *** GREEN — THE DEFECT IS INVISIBLE *** |

Through the real map, copy layer, `MB_SERVER`, `MirrorClient`, `SlotRun` and model — **no
interpreter.** *** BOTH BUILDS RAN UNDER ONE BYTE-IDENTICAL `Main`, NEVER RE-IMPORTED, SO THE RED
CANNOT HAVE COME FROM THE CALL STRUCTURE *** — only FC 900/901 changed. Version register corroborated
(`0x1111` GREEN, `0x2222` RED), and `outcome Completed` on both RED runs: **the run succeeds and the
*comparison* fails**, which is the distinction the whole result package rests on.

**Every gate HEALTHY on both lines** — `BLOCKS: 76 / TYPES: 33`, `INCONSISTENT: 0`.

### ✅ THE BIT ORDER AND WORD ORDER ARE MEASURED — the `[I]` comes off

*** `BitAddressOf` IS RIGHT, AND THE PLC WAS THE INSTRUMENT, SO THIS IS INDEPENDENT OF THE PREMISE. ***
Writing `0x0001` ran the block (count 12); `0x0100` did not (count 0). Slot 0's start bool is
`%M4009.0` — the low byte landing in the **second** `%M` byte. The simulator and `BitAddressOf` had
agreed *from the same premise*, so their agreement was worth nothing; this is worth something.

**Word order is `HighWordFirst`**, the existing default. `16#00001111` read back as reg0 `0x0000`,
reg1 `0x1111`, cross-checked on the scan counter (745 versus an absurd 48,824,320).

### 🔴 A CONVERTER DEFECT THAT A BYTE-IDENTICAL ROUND TRIP CANNOT SEE

*** THE CONVERTER EMITS `<ConstantType>Int</ConstantType>` FOR EVERY HEX LITERAL, REGARDLESS OF
MAGNITUDE OR OF THE DESTINATION'S TYPE. *** A 32-bit build stamp is 32 bits by construction, so
**every real stamp fails to import** — including §9's own worked `16#A93F2C71`. Isolated cleanly: an
Int-range literal imports fine and is *still* typed `Int`.

  ➜ *** THE PHASE-2 LANE'S BYTE-IDENTICAL ROUND-TRIP PROOF PASSED, BECAUSE THE WRONG TYPE ROUND-TRIPS
    FAITHFULLY. *** A round-trip check is structurally blind to any error the round trip **preserves**
    — the same shape as the Normalizer being blind in exactly the way the converter was wrong. Worth
    asking what else currently rests on a round-trip proof.
  ➜ **It is the SECOND instance of one class:** `4090b6f` fixed *"a comparison's literal is typed by
    MAGNITUDE, not by the compare's own type"*. This is that bug one site along — typed by magnitude
    rather than by **the destination**. Being fixed as **one general rule**, not a second special case.
  ➜ The runs used a **declared substituted stamp**, so *** THE 32-BIT CASE REMAINS UNPROVEN ON A
    DEVICE *** and closing that rides with the fix.

### 🔴 TWO DEFECTS THAT WOULD HAVE FAILED CLEANLY, BELIEVABLY AND WRONGLY

  1. *** `MB_SERVER` POINTED AT THE WRONG MEMORY *** — `P#M1000.0 WORD 209` against the map's `%M4000`.
     As shipped, **every harness register would have read zero and `VersionCheck` would have reported
     `Absent`.** Clean, believable, wrong.
  2. **The set contained no OB1**, so nothing called FC 900/901 at all.

### ⚠️ THE SCRATCH ARTIFACTS WERE A MOVING TARGET — and the hazard bit the lane's own setup first

`scratch/harness-phase2/` is **gitignored**. The files snapshotted for validation had been written at
10:18 by the *phase-3* lane and encoded a map HEAD does not produce; they have since been **deleted**
and that directory now holds different blocks entirely. Validation was redone against `src/harness`
**extracted at HEAD**.

  ➜ *** THE SAME HAZARD HIT THE LANE'S OWN DRIVER: it referenced the other lane's in-progress Debug
    DLLs and computed vectors ONE REGISTER OFF. *** Caught before the rig — *"I'd have been debugging
    the PLC."* **Two agents sharing a build output is the collision the one-agent-per-component rule
    exists to prevent, and a gitignored scratch directory is outside that rule's reach.**
  ➜ `lad-coder` contradicted its brief **correctly**: `Main` has **nine** networks, not two — seven
    plant calls — and it kept 1–7 verbatim rather than dropping them.

**Rig: the phase-2 GREEN build, project in sync, CPU `Running (8)`.** Build D deliberately **not**
restored — leaving the defective RED build would be a trap, and reverting would undo the
`MB_HOLD_REG` retarget further harness work needs. Build D is one import away and the original server
pointer was exported before it was touched.

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

### ✅ PHASE 3 IS BUILT — and the interfering pair had to be built subtly to be real (2026-08-13, `0c3cb93`)

**375 → 436 tests**, five assemblies green. `WaveRun` with max-tensor length, uncommanded null slots
and per-slot distribution on exit (3.4/3.5); the echo region plus an `SCOIL` latch of each block's own
start condition, cleared at inert (3.3).

> #### 🎯 HOW THE INTERFERING PAIR WAS MADE TO GENUINELY INTERFERE
>
> The second block gains **one network writing into the first block's accumulator** — overlapping
> reachable state (D9), the edge DB-13's conflict graph is defined by, and a real extra `Add` part in
> the SimaticML.
>
> *** THE LOAD-BEARING DETAIL IS THAT THE RUNG IS GATED ON THE SECOND BLOCK'S OWN START CONDITION. ***
> Every block is called every scan, **including throughout inert** (D37) — so a coupling written the
> obvious way would corrupt the first block's **solo** run too, and *** A DIFFERENTIAL CANNOT SEE A
> FAULT PRESENT IN BOTH OF ITS ARMS. IT WOULD HAVE COME BACK CLEAN. *** Gating makes the coupling exist
> only when both slots are active, which is what A5 actually asks.
>
> Held by three assertions: the pair diverges (`[10,1]` solo versus `[12,1]` together); **each block
> alone still matches its model**; and the two builds differ by **exactly one rung**. Detection is the
> exit criterion's own differential, and **the models catch it independently and agree** — asserted
> deliberately, because a coupling only the differential could see would be one the models were blind
> to.

**A second interference source closed:** `RegisterMap` now **refuses to exist** with overlapping
regions — a throw, not a report, since an aliased map is a derivation defect with no caller to tell.

**14 guards broken and confirmed red**, plus *** A SECOND TEST THAT COULD NOT FAIL, IN TWO PHASES. ***
The disjointness check began as a post-condition **inside `MapAllocator`** — deleting it left the suite
green, because the allocator can never produce an overlapping map. Moved into `RegisterMap`'s
construction, where a hand-built bad map goes red. *(Second-order trap met on the way: a `with`
expression **skips a record's validation**, so the test rebuilds through the real constructor.)*

#### What the spec does not say

  - 🔴 *** THE ECHO LATCH IS NOT BEHAVIOURALLY EXERCISED AND CANNOT BE, PC-SIDE. *** The copy layer
    re-drives the start condition every scan, so within an index a level `COIL` reads **identically**
    to an `SCOIL` at any poll rate. Kept, because X-E's reasoning implies it and it matters for a start
    condition the copy layer does not drive — but **labelled unexercised rather than left looking
    proven.** The *clearing* at inert is exercised.
  - **The echo guards the map's remaining `[I]`:** a wrong `BitAddressOf` byte now presents as
    `CommandedButDidNotRun` rather than as an unexplained timeout.
  - *** "ONE INERT PHASE PER TENSOR" IS ONLY WELL-DEFINED BECAUSE THE TENSOR'S SLOTS ARE CONFLICT-FREE
    *** — an assumption the code depends on and **cannot check**.

> #### ⚠️ A5 IS NOT RETIRED, AND THE DISTINCTION MATTERS
>
> What is shown is that **the harness does not couple slots, and that a coupling is caught.** What is
> *not* shown is *** WHETHER TWO INDEPENDENT BLOCKS INTERFERE ON A 1214C ***, which is what A5 is and
> what everything multi-agent rests on. Still owed on the device: A5 itself, `MB_SERVER` against the
> wider two-slot map, and **the intermittent, poll-timing-dependent form of interference** — the one
> PC-side work explicitly cannot reach.

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

### ✅ PHASE 4.3 AND 4.4 BUILT — admission control, the two queues, the drain (2026-08-13, `cba90e9`)

`src/wave-control/`, **58 → 140 tests**. 4.2's ladder deliberately untouched: its Class A rung rests
on **A6/G4**, which is unmeasured and needs the owner present, and nothing built here assumes an
answer to it — the drain stops at *"the boundary is due"*.

**4.3 admission control is a GATE OVER ATTESTED RESULTS, not a runner of checks.** It admits only
objects carrying a *passed* preflight and a *passed* isolated compile, **and only when the
evidence's `converter ir-hash` matches the content being submitted.** *** WITHOUT THAT HASH
COMPARISON THE GATE IS A FORMALITY ANY STALE RUN SATISFIES *** — which is the same defect class as
the compile fix that never reached its binary, one layer up. Atomic per DB-9: one bad object refuses
the whole submission, and every finding is reported rather than the first.

**A consequence of DB-4's twenty that had not been drawn out:** *dependency-closed* means every
declared dependency of every object in a batch is in that batch or already on the device — and
because batches **partition** the change set, that forces each weakly-connected component of the
dependency graph into **ONE** batch. *** SO THE TWENTY IS A LIMIT ON GROUPS, AND A GROUP OF 21 IS A
REFUSAL — NOT A SPLIT, NOT A WARNING. *** The case is real rather than hypothetical: DB-1's blast
radius makes a modified UDT `RUN (Init)` on every DB built on it.

  ➜ **The packer's output is re-verified by a `DependencyClosure` written from the definition rather
    than from components.** The packer "knows" its batches are closed by construction, and that
    conviction is exactly the blind spot to guard — *the check must not share its subject's
    reasoning.* **It earned its keep immediately:** with the size rule deliberately disabled, the
    plan was *still* refused, by the independent verifier, naming 20 `DependencyInAnotherBatch`
    violations. The test only went red because it pins *which* refusal fires.

**4.4** — `ChangeRouter` (D23) **refuses what it cannot place** rather than filing it somewhere safe;
`DrainPolicy` (D24) computes "no test can make progress" across **both queues plus the baseline**,
because D24's own justification is that the deferred queue holds other dependencies. **Declining to
drain requires a witness** — `DoNotDrain` is unconstructible without naming the submission that can
still run. Draining takes a decision object rather than a flag, since R8 demands an explicitly
declared disruptive mode.

**Negative testing: 12 source mutations plus 2 committed mutant checkers**, each restored
byte-for-byte and the tree verified clean after each. Largest signal: mixing a submission across
queues → **12 red**.

**"Empty is not clean" at four sites**, including one worth naming: `DeployedProgram.From` **refuses
an empty list**, because *"the device holds nothing"* and *"we could not read the device"* arrive as
the same empty list and call for opposite actions. Also `StalledWithNothingToDrain` is kept distinct
from the healthy `NothingToDrain` — both have an empty deferred queue, one is a stall.

**Today's two download measurements are encoded rather than averaged.** `CpuStopRequirement` records
that a **2-object** download left a running CPU running while the **19-object** one needed the stop —
so the stop is a function of *what* changed, not of downloading and not of object count. *** EVERY
PLANNED BATCH THEREFORE REPORTS `Undetermined` ***, pinned by a test against both a 2-object and a
19-object plan; `Required`/`NotRequired` exist only for a download-time determination, which is
D32's and blocked.

#### Spec ambiguities, with the reading taken

  1. *** A SUBMISSION THAT ROUTES BOTH WAYS IS HELD TO THE LATER QUEUE AND DEFERRED AS A UNIT. ***
     §1.4 routes *changes*; DB-9 admits *submissions*. Splitting would leave the project holding
     **half a test set that a wave could then test against — a silent wrong green**, against a delay
     §1.4 has already priced. Load-bearing (12 tests) and **agreed on review: it fails safe, which
     is the tie-breaker.**
  2. **An OB *code* change** is in neither DB-1's table (new/deleted/property) nor R1's flat "an OB
     change". Took **R1** — fail-closed, and it is the routing rule.
  3. 🔴 **OPEN: does the ≤20 limit apply to the drain's full download?** DB-4's mechanism is
     integration *"in one program cycle"*, a RUN-mode property, and the drain is not that.
     Deliberately not answered — the planner is scoped `WaveBoundaryBatchPlanner` and the drain path
     does not call it. **Close this before the first drain.**
  4. **Unbounded deferred-queue starvation is permitted by D24's condition.** Wait count is recorded
     and logged; nothing acts on it, because a threshold would be a policy the spec does not have.
  5. **An unclassified change is refused, not deferred** — deferring launders an unmade decision
     into the log.

  ➜ 🔴 **NAMED GAP — the deferred queue is not persisted.** X-C's marker records the *wave*, not the
    queues, so **a coordinator crash loses the deferred queue.** No format was invented for it,
    deliberately. This sits beside X-C's existing unreadable-marker case rather than inside it.

### ✅ THE DEFERRED QUEUE IS PERSISTED — and the reload check had to become ASYMMETRIC (2026-08-13, `2d3a0dd`)

`WaveQueueFormat` + `WaveQueueStore` + `QueueRehydrator`, **140 → 168 tests**, 9 mutations each
restored byte-for-byte. The gap it closes: X-C's marker records the *wave*, not the queues, so a
coordinator crash lost work that had **already passed the gate** — and nothing would have noticed. It
simply never happens.

**Absent ≠ unreadable ≠ empty, carried structurally rather than by discipline:**
`QueueRestoreState.Unreadable` is the **zero value**; the file is **never deleted**, so an empty queue
is a file declaring zero entries and *"no file"* keeps meaning *"nobody ever wrote one here"*;
`Queues` is **null, not empty**, for both non-restored states; and the only way onward from "no file"
is `AcceptNoPersistedState(result, reason)`, which **requires a reason and refuses to be called over a
file that exists.**

  ➜ **Two truncation detectors, because one is not enough.** A sentinel catches a torn write. The
    **declared `entries=` count** catches what a sentinel cannot: *** A FILE THAT PARSES PERFECTLY BUT
    CARRIES FEWER ENTRIES THAN WERE WRITTEN. *** A queue file's content is a *count of things*, so a
    short read looks exactly like a shorter queue — and a shorter queue is admitted work that silently
    never happens.
  ➜ **The gate is re-run, not re-implemented.** Every entry is rebuilt with the **current** hash and
    passed back through `AdmissionController.Admit`. A hash that cannot be determined now is a
    refusal; **nothing falls back to the persisted hash.** `PersistedQueueEntry` is a *different type*
    from `QueuedSubmission`, so "read from disk" and "admitted" cannot be the same state.

> #### 🔬 THE FINDING THE TESTS PRODUCED — a symmetric check was the wrong shape
>
> The first build required the re-derived queue to **equal** the persisted one. That **rejected every
> excised entry on reload**: D32 step 5 excises a *RUN-class* submission *into* the deferred queue, so
> re-routing correctly gives the "wrong" answer.
>
> The check is now **asymmetric**, and the asymmetry is the safety argument: deferred → routes-RUN is
> permitted **only** for an entry flagged excised (worst case, it waits a boundary it needn't) —
> whereas *** RUN → routes-DEFERRED IS ALWAYS REJECTED, because that is a STOP-class change about to
> stop the CPU mid-wave. *** `Excised` is an explicit persisted flag rather than a substring match on
> the reason, and a missing `excised=` is `Unreadable`, never defaulted to false.

  ➜ 🔴 **NAMED, NOT BUILT: the queue file and the X-C marker are two files with no shared
    transaction.** Each fails safe alone; they are **not jointly consistent**, and no ordering rule is
    specified. Worth a ruling before the coordinator writes both in one loop.

### 📐 RULED: DB-4's TWENTY DOES NOT APPLY TO THE DRAIN — settled by a measurement, not by inference

DB-4 states its own mechanism: more than 20 objects cannot be integrated consistently **"in one
program cycle"** — DB-3 names it in Siemens' own notation, **`RUN (<21)`**, and the hazard is a program
running as a *mixture of old and new blocks*.

*** THE DRAIN'S DOWNLOAD STOPS THE CPU (D25 full download; R8 measured `StopModules` raised and
`NoAction` inapplicable), SO THERE IS NO EXECUTING PROGRAM FOR THE RULE TO PROTECT. *** And §9a
already records a **full download that loaded 99 objects**, predicted count matching manifest count
exactly — **that run could not have happened if the limit bound full downloads.**

So the alternative reading is not merely unnecessary, it is **incoherent with D25**: *"download
everything"* and *"download at most twenty objects"* cannot both hold on a 99-object project.

  ➜ **Replaced with a positive check that is already specified** rather than a precautionary batching
    rule: compare `download-plan`'s predicted count against §9c's load-manifest count (1 vs 1 and 99
    vs 99 on the measured runs). **What would nail it down:** one full download of a project
    materially larger than 99 objects, same comparison.
  ➜ **Adopting it needs no code change** — the planner is named `WaveBoundaryBatchPlanner` and the
    drain path never calls it. **Rejecting it would require writing a new planner.**

### ✅ `RTT_p99` 173 → 201 TOUCHES NO BUILT PHASE-4 CODE — checked, not assumed

The spec lane flagged that anything holding a baked-in `K_max` would be over-admitting at S ≥ 50.
**Verified by grep against `src/wave-control/`: zero occurrences of any timing figure, `K_max`, `p99`
or `RTT`.** The lane chose no timing constant at all, so nothing built needs revisiting. `K_max` is
consumed by DB-13's slot colouring, which is 6.1 and not yet written — **the correction lands before
the code that would have used it**, which is the whole point of retiring assumptions in phase order.

### ✅ 5.1 DRAFTED — the test-environment contract (2026-08-13, `adce070`)

`docs/notes/test-environment-contract.md`. **Seven elements plus two guards, each carrying a column
for WHAT ACTUALLY CHECKS IT** — since this project's thesis is that the mechanical floor survives an
agent choosing not to look. It carries **no timing constants**; every figure cites §12a, on the stated
grounds that *a contract with a stale constant is worse than one with a pointer*.

  - **Mechanically checkable:** vector format · assertion ID drawn from the spec-derived enumeration ·
    **observability, in full, and it REFUSES** · start-bool binding (one per slot, bound *by name*,
    later-scan rule enforced against the **observed** counter) · blacklist add-only · *** AUTHORSHIP
    (D6) — a match between vector author and block author is a REFUSAL, not a warning *** · model
    fidelity (M4) as a set-difference.
  - **Judgement:** that the cited clause is the right one · that the assertion is a faithful reading ·
    that a settling condition really implies finality · whether over-blacklisting is happening
    (**measurable, not preventable**).

**Observability is a refusal built on "a poll IS one round trip"** — so there is no rate to turn up —
and **latched is the preferred mode as the only one immune to the tail**, not merely the cheaper one.

**Completion ≠ settling is written as *a confidently wrong verdict, not an error*,** and the checkable
half is that *** A SETTLING CONDITION WHICH MERELY RE-CITES THE DONE-FLAG IS REFUSED. ***

**`Basis` carries the autopsy as its reason:** a clause citation lets an ambiguous reading be reused
silently, where naming the **assertion** forces two readings to produce two *visibly different*
assertions instead of two greens.

**Reading a result is a verdict table with a "does NOT mean" column**, and `Stale` gets its own
section: *** A FROZEN MIRROR IS PERFECTLY SELF-CONSISTENT ***, so liveness must be established
independently of content — stimulus check, counter advanced **by the expected amount** rather than
merely moved, manifest presence.

> #### 🚩 THE CENTRAL QUESTION OF A DESIGN-FOR-TESTABILITY DOCUMENT, AND THE DESIGN ANSWERS IT BOTH WAYS
>
> *** THERE IS NO ROUTE FROM "UNOBSERVABLE" BACK TO "TESTABLE". *** The observability declaration is
> frozen and must precede the download, so a vector that cannot be observed is simply refused — and
> the obvious remedy, **exposing the evidence as a block output**, collides head-on with **D13/§2.1**,
> which place instrumentation in the copy layer and say `lad-coder` never writes observability code.
>
> **MAY AN AUTHOR CHANGE A BLOCK'S INTERFACE PURELY TO MAKE IT TESTABLE?** *** OWNER'S — it decides
> whether design-for-testability is a real practice here or only a naming convention ***, and it
> touches deliverable content rather than harness scaffolding, which is why it is not ours.

  ➜ **Also needing confirmation:** "assertion" is **undefined in §2.6**, which predates D35/§7 — so an
    author reading only §2.6 writes prose. The draft **requires an enumeration ID**, and that
    requirement is new here rather than inherited.
  ➜ **A scan count with no `comp` attached silently crosses the floor under compression.** Fix
    proposed rather than assumed.
  ➜ Minor: the copy layer's one-scan lag leaves an unspecified off-by-one for **stamped** assertions.

**For the skill** (deliberately not written — skill frontmatter is strict YAML with two silent failure
modes and deserves its own pass): §10 gives the gate-by-gate surface and two properties — *** EVERY
GATE FAILS CLOSED WITH NO RELAXING FLAG *** (precedent: phase 2's `AddressesExamined`, a correct rule
with nothing establishing it had run), **empty is not clean**, and **test it by mutation, not by
example.**

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
