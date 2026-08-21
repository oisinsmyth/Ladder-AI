---
name: gen-functional-analysis
description: Rung B of the structured spec pipeline — turns a supplied functional description into logically-grouped plant-level behaviours (gen/<project>/plant-behaviours.md), each scoped to the equipment it applies to. Use when asked to analyse a functional description or spec document, extract plant behaviours, sequences, modes or stop/fault handling. Produces plant intent only — no signals, no IO, no booleans. Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
  - Edit
---

# /gen-functional-analysis — rung B: plant-level behaviours

Ladder-AI project. Second rung of the structured spec pipeline (A → B → C → D):
A `gen-pid-analysis` → **B this skill** → C `gen-equipment-spec` → D `gen-code-structure`.
`CLAUDE.md` is already in your context — do not re-read it; its hard rules bind you.

**You produce plant INTENT, at process abstraction. Nothing lower.**

- **No signals, no IO addresses, no booleans, no interface members, no block names.**
- **Language of this rung:** *"On operator stop, all equipment stops at the same time."*

## Inputs

- **The functional description / supplied spec — REQUIRED.** The behaviour source. **Stop condition
  is three-way, by source kind:**
  - **No source at all → STOP.** Never write behaviours from memory or inference about what a plant
    "probably" does.
  - **A DERIVED source** (a reverse-derived register, an extracted summary — anything whose own
    provenance says it was read out of the as-built) → **proceed, with a blocking `Q-nn` recording
    the circularity**, and **mark every behaviour that has no independent citation**. *Stopping here
    produces nothing while the circularity is exactly the interesting defect: a real run on a derived
    register surfaced three behaviours with no source citation at all — including the up-to-speed
    enable the entire start cascade rests on.*
  - **A genuine independent source** → proceed normally.
- **Rung A's `gen/<project>/equipment-topology.md` — consumed if present**, only so behaviours can be
  scoped to real instances (`applies-to`). **A must not be edited here**, and where B appears to
  contradict A, that is a Q for rung C, not a correction.

## Method

1. **Read the whole document** before writing anything.
2. **Extract behaviours, grouped logically** — start sequence, stop sequence, fault handling, modes,
   operator interactions, per-equipment special behaviours. Group by function, not by document order.
3. **ID every behaviour** `B-nn`, stable forever, and **cite its source** (section + leading words),
   the same citation discipline as `requirements.md`.
4. **Scope each behaviour**: `applies-to` — which equipment instances (from A) it lands on. A
   behaviour with no identifiable scope is a `Q-nn`.
5. **Capture quantitative process parameters** as stated values (a 10 s warning, a 5-trip limit).
   These are requirements. Do **not** invent a number the document does not state — that is a `Q-nn`.
6. **Never resolve a contradiction** — inside the document, or against rung A. Record both readings
   and raise a `Q-nn`. *Silent merging of a conflicting stop behaviour is a documented failure mode.*

## Output — `gen/<project>/plant-behaviours.md`

Provenance header (date, source document + identity hash, rung-A artifact consumed), then grouped
behaviours:

```
G1  Start sequence
  B1  plant start is operator-commanded                       [FuncDesc §"Start" — "press start"]
      applies-to: plant
  B2  a pre-start warning sounds for 10 s before any equipment starts
      applies-to: all conveyors
G2  Stop sequence
  B4  operator stop stops all equipment at the same time      [FuncDesc §"Stop" — "stop all"]
      applies-to: all conveyors
```

Then an **open questions** section (`Q-nn`, blocking ones marked), including every contradiction
found — against the document itself or against rung A.

## Calibration

- **WHAT, never HOW.** No block content, no logic shapes, no signal names.
- **Duplication with rung A is expected and fine** — the same fact may appear as a per-equipment
  relation (A) and as plant intent (B). Rung C reconciles them; do not pre-merge or drop either.
- **Safety content:** record hardwired/safety functions as explicitly out of PLC scope; never
  elaborate their internals (hard rule 2).
- **Telemetry:** append one line to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`.
