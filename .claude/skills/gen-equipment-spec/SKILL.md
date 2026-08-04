---
name: gen-equipment-spec
description: Rung C of the structured spec pipeline — merges the P&ID topology (rung A), the plant behaviours (rung B) and the IO table into one per-equipment CONTROL SPEC (gen/<project>/equipment-specs/). Use when asked to produce the per-equipment specification, bind requirements to IO, or combine topology + functional analysis into the artifact the code-structure stage builds from. Signals enter here; booleans and interface members do NOT. Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
  - Edit
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe tagstatus:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe tagstatus:*)
---

# /gen-equipment-spec — rung C: the per-equipment control spec

Ladder-AI project. Third rung of the structured spec pipeline (A → B → C → D):
A `gen-pid-analysis` → B `gen-functional-analysis` → **C this skill** → D `gen-code-structure`.
Read `CLAUDE.md` first; its hard rules bind you.

**Signals enter at this rung. Booleans and interface members do NOT.**

- **Allowed here:** real IO/tag names bound to requirements (`running confirmed from DI_23`).
- **Banned here:** `AND` / `OR` / `NOT` expressions, interface members (`.UPSEnable`, `.Run`), block
  names, network structure. Those are rung D. *Writing the boolean form here is the abstraction leak
  this pipeline exists to prevent.*

## Inputs (all REQUIRED unless stated)

- **`gen/<project>/equipment-topology.md`** (rung A) — instances, classes, interlock relations, deltas.
- **`gen/<project>/plant-behaviours.md`** (rung B) — `B-nn` behaviours with their `applies-to` scope.
- **The IO table** — the real signals. **Stop condition:** a requirement needing a signal the IO table
  does not contain → the tag is `proposed`; record it as a **blocking `Q-nn`** and never invent it
  (hard rule 3). Verify with `converter tagstatus` where an IR export exists.
- **The equipment engineering references** — the per-class standard control requirement set. These
  give **completeness by construction**: every instance inherits its class's full requirement set.

## Method

1. **For each instance from rung A**, start from its class reference and instantiate the **complete**
   standard requirement set (`C1…Cn`). A standard requirement is never omitted — if it does not apply
   to this instance, that is a recorded **delta**, not a silent absence.
2. **Bind IO**: attach the real signal to each requirement that needs one.
3. **Carry rung A's interlocks** verbatim as `P-nn`, **fully enumerated**, one relation per line.
4. **Layer rung B's behaviours** onto the instances in their `applies-to` scope.
5. **Reconcile A vs B — never silently.** Where a rung-A relation and a rung-B behaviour describe the
   same fact, merge them and cite both. Where they **contradict** (e.g. A's staged shutdown vs B's
   simultaneous all-stop), **do not choose**: emit a **BLOCKING `Q-nn`** stating both readings, their
   sources, the affected instances, and the likely resolution. *A silent merge here is a documented
   defect path.*
6. **Settings**: record each with its **owner** (HMI / engineering) and its value. A value supplied by
   the reference rather than the plant documents is marked `reference-proposed` (a tunable
   commissioning default — legitimate, unlike an invented tag).
7. **Tag provenance** on every line: `[A]` topology · `[B]` behaviour id · `[io]` IO table ·
   `[ref]` class reference.

## Output — `gen/<project>/equipment-specs/<Instance>.md` (one per instance)

```
EQUIPMENT SPEC — Conveyor-07
  class: dol-motor (ref v1)     fed-by: Conveyor-06     discharges-to: Conveyor-08

IO BINDING                                             [io]
  run command       DQ_11
  running confirm   DI_23
  isolator healthy  DI_24
  motor fault       DI_25

CONTROL REQUIREMENTS   (complete by construction from the class reference)
  C1  run output driven while the machine is commanded to run           [ref]
  C2  running confirmed from DI_23                                      [ref+io]
  ...
PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  start permitted only while Conveyor-08 is confirmed running       [A]
  P3  on controlled shutdown, hold until Conveyor-08 reports complete   [A]   (Q-01)
  P4  a fault on Conveyor-08 stops this machine                         [B5+A]
  P5  a fault on Conveyor-09 stops this machine                         [B5+A]
  ...
SETTINGS
  start-confirm-time   owner: HMI   default: 10 s   reference-proposed    [ref]
DELTAS:  none
OPEN:    Q-01 (BLOCKING) — shutdown behaviour conflict A vs B4
```

## Calibration

- **This is a CONTROL spec.** **Alarms are out of scope** — a separate artifact, defined later.
- **Completeness over brevity.** Full enumeration is correct here even when verbose; compacting it is
  rung D's job, via a declared and argued discharge — never by omission at this rung.
- **A delta is first-class** — carried from rung A into the spec, never dropped in the merge.
- **Safety content = stop** (hard rule 2). **Never invent tags/addresses** (hard rule 3).
- **Telemetry:** append one line to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`.
