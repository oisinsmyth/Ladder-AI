# TEST ENVIRONMENT — BUILD PLAN

Source of truth for the design: `PC-Client-Modbus-Spec-Draft-final.txt`. This document does not
restate it. It answers one question: **in what order do we build this so that no significant
body of code is written on an assumption that later turns out to be wrong?**

Written 2026-08-12.

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
| A3 | `%MW` has ~2048 words on a 1214C | **UNVERIFIED** | Slot sizing, max concurrent slots, map layout |
| A4 | A block can be driven by its own existing command signal | untested | D37, inert, the entire start-bool mechanism |
| A5 | Two slots genuinely do not interfere | untested | Everything multi-agent |
| A6 | A delegate throw leaves the CPU untouched (G4) | **UNMEASURED** | D32's guard model — this one is a *safety* item, not a schedule item |
| A7 | `PlcStatus()` reports STOP (X-K) | in progress | Fault detection only. Small blast radius |
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
| 0.1 | `%MW` capacity on the 1214C, from the datasheet | A3 | Slot size and slot count are derived from it. Guessing means re-laying-out the map later |
| 0.2 | ~~`PlcGetStatus` in RUN over Sharp7~~ | A7 (half) | ✅ **DONE 2026-08-12.** Returned Run from the rig at 79–94 ms. PUT/GET was **not** a blocker |
| 0.3 | Establish what the 79–108 ms round-trip actually measured — Modbus, TCP or ICMP | A2 | The poll budget and every tensor-width number descend from it. **Now more urgent, see below** |
| 0.4 | `C-122` conformance sweep: do existing blocks already carry PT as data? | — | Decides whether time compression is nearly free or a rewrite |
| 0.5 | ~~Confirm Sharp7 presence, version and connection path~~ | — | ✅ **DONE.** Sharp7 1.1.82.0; rack 0 / slot 1; address from the device allowlist |
| 0.6 | Add read-only `ReadCpuStatus` to `IS7Client` + `Sharp7Client` | A7 | **New.** The interface is a deliberate reduction of Sharp7 and has no status member, so the harness cannot call this yet. Keep the reduction — it is what holds `PlcStop()` out of reach |

> ### ⚠ 0.3 got more interesting, not less
>
> The S7 status call measured **79–94 ms**, and the spec's assumed Modbus round trip is
> **79–108 ms**. Those overlap almost exactly — which raises the possibility that the original
> figure was measured over **S7comm or ICMP rather than Modbus**, and is being used as a Modbus
> number. If so, the real Modbus round trip is unknown, and with it the poll budget, the tensor
> width and O11.
>
> **This does not change the plan's order — it raises 0.3's priority within phase 0**, and phase
> 1.2 measures the real figure regardless.

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
| 1.6 | `PlcStatus()` before / during / after a CPU stop | **A7**, and supplies R8's outstanding confirmation |
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

### Where it could still go wrong

- **Phase 1's spike becomes the product.** Guard: it lives outside the source tree, and phase 2
  starts from the specification, not from the spike.
- **Phase 2's failure gate gets skipped** because the happy path already works. Guard: the
  deliberate bug is a *deliverable* of phase 2, not a nice-to-have.
- **Phase 3 proves non-interference on a pair too simple to interfere.** Guard: construct the
  interfering pair deliberately and confirm it is caught.
- **A2 comes back much worse than assumed.** That does not invalidate the design, but it narrows
  slots and re-opens O11 — which is why 0.3 is a desk task and not a phase-4 discovery.
