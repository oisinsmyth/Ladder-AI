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

**Exit criterion:** A1 answered yes or no. If **no**, the map and copy-layer design change *before*
either is written — which is the entire point of this phase.

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
