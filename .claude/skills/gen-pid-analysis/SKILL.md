---
name: gen-pid-analysis
description: Rung A of the structured spec pipeline — turns a P&ID / plant layout plus the equipment engineering references into a per-instance topology + interlock artifact (gen/<project>/equipment-topology.md). Use when asked to analyse a P&ID, derive plant topology, work out which equipment can't start without what, or produce the per-equipment interlock relations that later stages build on. Produces PROCESS relations only — no signals, no IO, no booleans. Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
  - Edit
---

# /gen-pid-analysis — rung A: topology and per-equipment interlocks

Ladder-AI project. First rung of the structured spec pipeline (A → B → C → D):
**A this skill** → B `gen-functional-analysis` → C `gen-equipment-spec` → D `gen-code-structure`.
`CLAUDE.md` is already in your context — do not re-read it; its hard rules bind you.

**You produce PROCESS relations, at process abstraction. Nothing lower.**

- **No signals, no IO addresses, no tag names, no booleans, no interface members.** Those enter at
  rung C (signals) and rung D (booleans/interfaces). If you write `DI_23` or `AND` or `.UPSEnable`,
  you have leaked a lower rung into this one — the exact leak this pipeline exists to prevent.
- **Language of this rung:** *"Conveyor-07 may start only while Conveyor-08 is running."*

## Inputs

- **The P&ID / plant layout / equipment list — REQUIRED.** The topology source: what equipment exists
  and what feeds what. **Stop condition:** no layout source → stop; do not infer a plant.
- **When ONE document serves both rung A and rung B, declare a fence.** On a brownfield project the
  same source often carries topology (a table) and behaviour (prose). Say in the provenance header
  which part of it is rung-A material and which is reserved for rung B — e.g. *"inventory table only;
  REQ prose reserved for B"* — and hold to it. Without a stated fence, A and B silently read the same
  sentences and their "independent" agreement proves nothing, which is the correlated-reading failure
  this pipeline exists to break. If the two parts disagree, that is a `Q-nn` for rung C, not something
  either rung resolves.
- **The equipment engineering references** (`references/<class>/reference.md`) — the per-class
  standard control requirement set ("how a DOL motor is normally controlled"). These make the output
  **complete by construction**: an instance of a class inherits the class's full requirement set, so
  a standard requirement cannot be silently forgotten. **Stop condition:** an equipment class with no
  reference → a blocking `Q-nn`, never an improvised requirement set.
  **Status caveat — this property is nominal, not delivered.** Every reference currently in
  `references/` opens by self-declaring *derived from as-built code, not from an engineering
  standard*. A set derived from what one plant happened to build cannot establish what a class
  *normally* requires, so it cannot make anything complete by construction. Raise it as a blocking
  `Q-nn` on every run until a standards-grounded reference exists — do not let the phrase above
  stand in for a completeness you have not got.

## Method

1. **Enumerate every equipment instance** with its class (`dol-motor`, `vsd-motor`, `valve`,
   `pusher`, …). One row per instance — expand per instance, never "20× the same". Grouping instances
   back into FB types is rung D's job, deliberately separate.
2. **Record material flow** per instance: `fed-by` / `discharges-to` (or the equivalent process
   relation for non-conveying equipment).
3. **Derive the interlock relations** from the topology, using a plant-wide, consistently-applied
   convention (e.g. "start needs the receiving machine running; controlled shutdown holds until the
   receiving machine reports complete"). **State the convention once, at the top, and apply it
   without exception** — an inconsistency here becomes a defect everywhere downstream.
4. **Record deltas.** A delta is an instance that is its class **plus** something (a bypass, a second
   direction, an extra permissive) or **minus** something. Deltas are FIRST-CLASS: a named field per
   instance, never buried in prose. *Outliers are where guards get lost — an unrecorded delta is the
   failure mode this field exists to prevent.*
   **`deltas : none` is NEVER written bare.** Write `deltas : none-found — searched: <sources>`,
   naming what you actually checked (layout notes, class reference §, equipment schedule). *A bare
   `none` records an absence of looking, not an absence of deltas — and rung A cannot see the IO
   table, so a delta carried only by an operator/bypass signal is invisible here by construction and
   must be re-hunted at rung C (`gen-equipment-spec` §8).*
5. **Tag provenance** on every relation (which layout element / which reference it came from).

## Explicitness rules (non-negotiable — FI-37)

- **One relation per line — on the way in as well as the way out.** Never join two conditions with
  an ambiguous separator (`/`, "and/or", a comma) in what you write; and **never resolve one by
  reading** in what you consume. An ambiguous separator in a SOURCE is **split into two relations,
  both retained** (the default), or raised as a blocking `Q-nn`. **Dropping either side always
  requires the `Q-nn`** — an appositive reading ("A, i.e. B") is a resolution and is never taken
  silently. *A `/` joining two hold-conditions, read as one, is the documented cause of a real
  dropped-interlock regression (`docs/evidence/PlantAutoControl-bench-autopsy.md`).*
  **Carve-out — split-and-retain applies to CONDITIONS, not to candidate SIGNALS.** Retain both sides
  only where both are conditions over signals whose existence is established. Where either side would
  assert a **signal, device or channel** that is not established to exist, split-and-retain is
  **forbidden** (hard rule 3 — it invents hardware): raise the blocking `Q-nn` instead. **State which
  branch you took and why.** *A fault described across a `/` can mean two fault conditions (split) or
  one fault with two candidate sources (Q) — retaining both in the second case asserts a second
  physical input that may not exist.*
- **Full enumeration.** Where a relation spans a set of machines, list them. Compactness is rung D's
  problem to solve (by a declared, argued discharge), never this rung's to fudge.
- **Underspecified = blocking `Q-nn`.** If the layout does not settle a relation, raise a question.
  Never pick a reading, and never pick the weaker/permissive reading silently.

## Output — `gen/<project>/equipment-topology.md`

Provenance header (date, layout source + hash, references used, convention statement), then one
block per instance:

```
Conveyor-07 : dol-motor
  fed-by        : Conveyor-06
  discharges-to : Conveyor-08
  interlock     : start requires Conveyor-08 running
  interlock     : controlled shutdown holds until Conveyor-08 reports shutdown complete
  interlock     : a fault on Conveyor-08 stops this machine
  interlock     : a fault on Conveyor-09 stops this machine
  ...
  deltas        : none-found — searched: layout notes, dol-motor reference §3, equipment schedule
  provenance    : interlocks <- layout flow order; class reqs <- dol-motor reference v1
```

Then an **open questions** section (`Q-nn`, blocking ones marked).

## Calibration

- **This rung analyses; it does not design or specify signals.** Equipment-to-equipment relations
  only.
- **Never invent equipment or a flow path** that the layout does not show (hard rule 3's spirit).
- **Safety content = stop** (hard rule 2).
- **Telemetry:** append one line to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`.
