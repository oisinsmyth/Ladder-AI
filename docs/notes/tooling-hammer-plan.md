# HAMMERING THE TOOLING — the campaign requirements

**Owner's directive, 2026-08-14.** After the spec-derived test plan is covered, the tooling is
**hammered** — test after test — until it is fit to put on a live project. This file is the
requirements capture for the *second* half of that plan, recorded before the plan is written so it
cannot drift.

> **The purpose is not to demonstrate the tooling works. It is to find out where it stops working.**
> Today's evidence for why that distinction matters: the copy layer's own suite was green and TIA
> refused its output on the first import; a binding passed every gate and laundered a predicted
> finding; a project-wide analysis reported three multi-writers that do not exist. **Every one of
> those was found by contact, none by review.**

---

## 1. SHAPE OF THE CAMPAIGN

| | |
|---|---|
| **Materials** | **Artificial test materials and written blocks** — generated, not the real hopper block. Deliberately reusable: the same materials are re-run under different conditions so the *condition* is the variable |
| **Projects** | **Any project green-lit for scratch development, and only those.** Verified against `tools/confirm-roundtrip.allowlist`: **`GenProject1`** and **`SampleProject`**. Nothing else, ever |
| **The axis that matters** | *** VARYING NUMBERS OF AGENTS SUBMITTING AT THE SAME TIME. *** 1, then 2, then more. Concurrency is the variable this system has never been exercised on |
| **Metric** | **Wave duration, tracked throughout — for reference, not as a gate.** Nothing is scheduled against it yet; it exists so that when something later *is*, the number came from measurement |

**Why artificial materials rather than real ones.** Real blocks are evidence — the hopper block
carries seven predicted divergences and a static FAIL, and re-running it changes what those results
mean. Generated materials can be produced in quantity, varied on purpose, and thrown away.

---

## 2. THE THREE TRAPS THAT WOULD MAKE THIS PASS VACUOUSLY

Any one of these turns the whole campaign into a green that means nothing. **Check each before the
first run, not after a surprising result.**

### 🔴 2.1 The claims directory must be SHARED, or the registry coordinates nothing

`converter claim` requires `--claims <dir>` and has **no default on purpose**. The tool's own error
says it: *"It must be a directory SHARED by every agent working this project — a per-worktree path
would grant every claim and coordinate nothing."*

*** AGENTS WORK IN SEPARATE WORKTREES. A PER-WORKTREE CLAIMS DIR IS ALWAYS EMPTY, GRANTS EVERY
CLAIM, AND LOOKS EXACTLY LIKE SUCCESS. *** This is FI-44's *empty is not clean* pointed at the one
mechanism the whole multi-agent story rests on.

**So the campaign's first assertion is about itself:** prove the claims dir is shared by
demonstrating a **refusal** — two agents, one resource, the second refused. A campaign that never
observes a refusal has not shown the registry is connected.

### 🔴 2.2 Portal is a token, not a component — and two sessions on one project is unsupported

One lane holds Portal at a time. **Concurrent *submission* is the thing under test; concurrent
*Portal work* is not permitted and must be seen to serialise.** The interesting question is not
whether agents can import simultaneously — they must not — but **whether the system queues them
safely or collides.** A collision has been observed once already (`Collection was modified`,
`EngineeringObjectDisposedException`, every block reporting inconsistent).

### 🔴 2.3 A wave set of slots that all drive one instance is ONE slot, not N

Measured 2026-08-13: slots sharing an FB instance, a stimulus surface or a reset are mutually
conflicting under D9, so the colouring separates every pair. **A campaign that generates N slots
against one block and reports "N concurrent" has measured nothing.** Vary the *blocks*, not only the
slot count, or the concurrency is nominal.

---

## 3. WHAT EACH CONCURRENCY LEVEL MUST ESTABLISH

At every level, and stated separately rather than rolled into a pass:

- **The registry refuses what it should** — block numbers, alarm bits, DB members, block edits,
  and the cross-kind conflict the filesystem cannot see (`block-edit X` against `block-network X:8`).
- **Admission colours correctly** — conflicting slots land in different waves; **the unaffected
  case is untouched**, because a gate that refuses everything passes every test that only checks
  refusals.
- **Nothing is lost.** Every submission either ran or was refused **by name**. A submission that
  merely vanished is the failure this campaign exists to find.
- **Wave duration**, recorded with its slot count and concurrency level.

---

## 4. WHAT WOULD MAKE THIS CAMPAIGN DISHONEST

Written down in advance, because these are the outcomes that will be tempting:

- Reporting a green from a run where **the claims dir was per-worktree** (§2.1).
- Reporting **N concurrent agents** where the slots were mutually conflicting and ran serially (§2.3).
- Treating a **`TIMED-OUT`** as a tooling limit without checking calibration — *a spurious
  `TIMED-OUT` is worse than a spurious `FAIL`, because a `FAIL` invites an argument and somebody
  looks, while a timeout reads as "the condition never occurred" and closes the question.*
- Quoting **wave duration** as a capability figure. It is a reference measurement taken under one
  set of conditions, on one rig, over a remote tunnel with a measured median round trip of ~72 ms.
