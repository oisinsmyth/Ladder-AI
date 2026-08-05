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
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  # tagstatus, candidate-scan and signal-sweep are covered by the wildcard above; named here so the
  # wiring is greppable. signal-sweep is the mandatory pre-finish self-check below — it was missing
  # from this list until 2026-08-05, so the rung could not run its own required check.
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe tagstatus:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe tagstatus:*)
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe candidate-scan:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe candidate-scan:*)
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe signal-sweep:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe signal-sweep:*)
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
- **Carve-out — signal POLARITY is a signal fact, not a logic shape.** "The fault is asserted when the
  system-OK signal is low" or "`DI4` is a normally-closed contact" states what the field device *does*;
  it is not a rung-D expression and must not be paraphrased away. The ban is on composing conditions
  (`A AND NOT B`), not on describing one signal's sense. *A run phrased around this rule and produced
  something more obscure than the plain sentence — that is the rule failing, not the writer.*

## Inputs (all REQUIRED unless stated)

- **`gen/<project>/equipment-topology.md`** (rung A) — instances, classes, interlock relations, deltas.
- **`gen/<project>/plant-behaviours.md`** (rung B) — `B-nn` behaviours with their `applies-to` scope.
- **The IO table** — the real signals. **Stop condition:** a requirement needing a signal the IO table
  does not contain → the tag is `proposed`; record it as a **blocking `Q-nn`** and never invent it
  (hard rule 3). Verify with `converter tagstatus` where an IR export exists — it classifies to
  **member** level, so `MEMBER-NOT-FOUND` means the DB is real but the member you named is not, and
  `INDEX-OUT-OF-RANGE` means the member is real but that array element does not exist (fix the
  binding — it is not a gap to raise as a `Q-nn`). Members inside an array of UDT — the usual shape
  for N identical vessels — resolve indexed or unindexed (`DB.Vessel[0].MaxNet`, `DB.Vessel.MaxNet`).
  A `MEMBER-UNCHECKED` result is *not* a pass: it means the tool could not enumerate that namespace,
  so verify by reading the type before binding. This rung binds **members**, which is exactly where a
  root-level-only check would launder an invented one.
- **The equipment engineering references** — the per-class standard control requirement set. These
  give **completeness by construction**: every instance inherits its class's full requirement set.
  **Status caveat — nominal, not delivered.** Every reference currently in `references/` self-declares
  as derived from as-built code rather than from an engineering standard, so the completeness it
  confers is only as complete as one plant's existing code. Treat the inherited set as a floor to
  check against, never as proof that nothing is missing (rung A raises this as a blocking `Q-nn`).

## Method

1. **For each instance from rung A**, start from its class reference and instantiate the **complete**
   standard requirement set (`C1…Cn`). A standard requirement is never omitted — if it does not apply
   to this instance, that is a recorded **delta**, not a silent absence.
2. **Bind IO — with a COMPUTED candidate set.** For each requirement needing a signal, run
   ```
   converter candidate-scan --project ir/<project>/ --fb <FBType> --scope <path-prefix> [--type <T>] [--direction status|command]
   ```
   It enumerates every in-scope signal that could satisfy the phrase — the IO half from the project's
   signal inventory, the FB half from that block's own interface, with each member classified
   status/command by **whether the FB writes or reads it** (computed, not read off the interface
   section — the members that matter live under STATIC). **Exit 1 means the set has more than one
   member, and that is the trigger: the binding is a BLOCKING `Q-nn`** unless a documentary basis is
   cited for the choice. Missing binary → enumerate by hand to the same rule, and say you did.
   *The point of computing it is that the size is no longer yours to judge.* Two defects shipped
   because a phrase ("not faulted", "running feedback") admitted more than one signal and the single
   reader resolving it never noticed a choice existed (`docs/evidence/PlantAutoControl-bench-autopsy.md`
   §2-C). Note the `family` line too — N same-typed IO signals facing N FB members of matching
   direction is the transposition signature a 1:1 by-name assignment gets wrong.
   **`--phrase` is advisory only** and never narrows the set; the exit code keys off the unfiltered
   size, because filtering by name resemblance is the very reasoning that produced the swapped pairing.
   **The tool computes the fact; you still own the choice** — it says a choice exists, never which
   signal the requirement means. *"Not faulted" satisfied by both a raw comms-fault input and an aggregated FB fault
   output, and a "running"/"remote-operational" pair facing two ambiguously-named feedbacks, are the
   two documented cases (`docs/evidence/PlantAutoControl-bench-autopsy.md` §2-C).* **Two requirements
   competing for two same-shaped signals is always a candidate-set of two, never a coin flip.**
   **How to record it (the narrow carve-out).** Requirement lines stay **signal-level**: an interface
   member may **never** appear in one. But an unresolved requirement carries a dedicated
   **`CANDIDATES:`** field, and that field **may** name interface members, tagged `[iface,
   named-only]` — **naming a candidate is never a binding**. The decision belongs to rung D's binding
   audit, which has the interface as ground truth; this rung's job is to **prove the choice existed**
   and raise the blocking `Q-nn`. *Without this field the rung that discovers a
   `{raw input, aggregated FB output}` choice cannot write it down, and the discovery is lost.*
   ```
   C5  running confirmed from <unresolved>                                  [ref+io]
       CANDIDATES: DiscreteInputs.FilterUnit1Op
                   FilterUnitInst2.IO.RunningFB   [iface, named-only]
       → BLOCKING Q-C01 — decision deferred to D1
   ```
3. **Carry rung A's interlocks** verbatim as `P-nn`, **fully enumerated**, one relation per line.
4. **Layer rung B's behaviours** onto the instances in their `applies-to` scope.
5. **Reconcile A vs B — never silently.** Where a rung-A relation and a rung-B behaviour describe the
   same fact, merge them and cite both. Where they **contradict** (e.g. A's staged shutdown vs B's
   simultaneous all-stop), **do not choose**: emit a **BLOCKING `Q-nn`** stating both readings, their
   sources, the affected instances, and the likely resolution. *A silent merge here is a documented
   defect path.*
   **A merge is permitted only when both lines resolve to the same signal or the same named plant
   condition.** Two conditions that are both true, both must hold, and resolve to **different**
   signals are **two `P-nn` lines**, always — however closely related their process meaning. Reducing
   two such conditions to one is a **discharge**, and discharges belong to rung D's ledger where they
   must be argued, never to this rung's merge. *A plant-level "fans ready to shut down" flag and a
   specific neighbour's "shutdown complete" are not the same fact; collapsing them here destroys the
   evidence D2 needs.*
   **A candidate set spanning a signal and an interface member is never resolved by merge** — it is a
   `CANDIDATES:` record (§2) plus a blocking `Q-nn`, decided at D1. This rung cannot see what the FB
   does with either member, so any merge it made would be a guess wearing a merge's clothes.
6. **Settings**: record each with its **owner** (HMI / engineering) and its value. A value supplied by
   the reference rather than the plant documents is marked `reference-proposed` (a tunable
   commissioning default — legitimate, unlike an invented tag).
7. **Tag provenance** on every line: `[A]` topology · `[B]` behaviour id · `[io]` IO table ·
   `[ref]` class reference.
8. **Unclaimed-signal sweep (the Pass-2 of this rung) — TWO passes, both required.**
   Sweep the IO table in the **opposite** direction to the binding pass. *Binding requirements to
   signals without ever sweeping signals for requirements is what let a discharge-VSD rotation-sensor
   bypass tag sit unclaimed and unreferenced through a whole generation run
   (`docs/evidence/PlantAutoControl-bench-grading.md`, REQ-012).*
   - **(a) Per-instance — SCOPED signals only.** Every signal scoped to *this* instance is either
     **bound** to a requirement above, or listed under `UNCLAIMED` with a reason. **A plant-scoped
     signal does not belong here** — point at (b) instead. *Listing plant-wide signals per instance
     copies the same finding into three-to-five specs, which is longer to review and drifts apart.*
   - **(b) Plant-level residual — run ONCE per project**, into `gen/<project>/unclaimed-signals.md`:
     every IO / global-DB signal claimed by **no** instance spec, each with a reason or a blocking
     `Q-nn`. *The richest findings are plant-scoped and ownerless — a plant health input, a shared
     fault-reset output, whole unspecified feature sets, a production selector wired to a test array.
     Per-instance sweeping alone cannot reach them.*

     **Its disposition tables are a CONTRACT** — `converter signal-sweep` parses them:

     ```
     ## Full disposition table

     ### `DiscreteInputs` (DB 15)

     | Members | Disposition |
     |---|---|
     | `FilterUnit1Ready`, `FilterUnit1Op` | bound — `FilterUnitInst2` |
     | **`OSCRotSen`** | **UNCLAIMED — Q-C09** |
     ```

     One `### \`<Container>\`` heading per **DB *or PLC tag table*** — the heading is the qualifier
     for the bare names beneath it, so it must be spelled exactly as the export spells that container
     (a tag is qualified by its TAG TABLE, never by a DB). A heading matching no container in the
     export is reported as a warning, and everything under it reads as unaccounted. And
     **every member name in backticks**. A member dispositioned in PROSE — a row reading
     `E-stop members | safety` — is **not machine-checkable and will read as unaccounted**, so
     backtick them even when the disposition is "excluded". Narrative `### Q-nn` findings stay
     narrative; only these tables are parsed.
   **A residual signal that implies a control function is BLOCKING regardless of its name.** Name
   patterns (`Bypass*`, `*Select`, `*Inhibit`, `*Enable`, `*Override`) are a prompt for attention,
   **never the test** — a differently-named control signal defeats a pattern list, so judge by role.

## Output — `gen/<project>/equipment-specs/<Instance>.md` (one per instance)

**The spec file's own shape is a CONTRACT.** Two mechanical-floor checks parse these files, and both
fail *quietly* on a malformed one — so a spec that merely reads well is not enough:

| What | Read by | Required shape | If you get it wrong |
|---|---|---|---|
| **The instance** | `relation-reconcile` | the **filename** — `<Instance>.md`, matching the instance id exactly | the instance reconciles against nothing |
| **Every relation** | `relation-reconcile` | a markdown bullet `- **C1**` / `- **P12**` — hyphen, bold, unhyphenated id, at the start of the line | `parsed 0 relations … refusing to report a clean reconcile`, a hard error |
| **Every signal name** | `signal-sweep` | wrapped in **backticks**, everywhere it appears — the IO table, relation text, disposition tables | `claimed = 0`, and the run **warns rather than errors**: every signal silently reads as unaccounted |

The backtick rule is the dangerous one, because nothing stops the run. Write signals as
`` `DiscreteOutputs.FilterUnit1Start` ``, never bare. Continuation lines of a relation are indented
under its bullet and are not parsed — put the id and its statement on the bullet line itself.

```markdown
# EQUIPMENT SPEC — Conveyor-07 (Sorter Infeed Conveyor)

    class: dol-motor (ref v1)
    fed-by: Conveyor-06 [A]      discharges-to: Conveyor-08 [A]

## IO BINDING [io]

| Role | Signal | Status |
|---|---|---|
| run command out | `DiscreteOutputs.Conveyor7Run` | EXISTS |
| running feedback | `DiscreteInputs.Conveyor7Running` | EXISTS |
| isolator healthy | `DiscreteInputs.Conveyor7IsoFB` | EXISTS |
| motor fault | `DiscreteInputs.Conveyor7Flt` | EXISTS |

`converter tagstatus --project ir/<project> <all of the above>` → 0 proposed, 0 member-not-found,
0 index-out-of-range.

## CONTROL REQUIREMENTS (complete by construction from the class reference)

- **C1** the run output is driven while the machine is commanded to run [ref DOL-01]
- **C2** running is confirmed from `DiscreteInputs.Conveyor7Running` [ref DOL-02 + io]
- **C3** the machine does not start while it is isolated [ref DOL-03]
  → binding unresolved — two candidates, neither scoped to this machine.
  **BLOCKING Q-C02.** Not dropped, not invented.

## PLANT INTERLOCKS (fully enumerated, one relation per line)

- **P1** start permitted only while Conveyor-08 (`Conveyor8Inst`) is confirmed running [A rel-1]
- **P2** on controlled shutdown, hold until Conveyor-08 reports its shutdown complete [A rel-2]
  → **P1 and P2 stay two lines.** They resolve to different signals, so §5 does not permit a merge.
- **P3** a fault on Conveyor-08 stops this machine [B-05 + A rel-3]

## SETTINGS

| Setting | Owner | Value | Basis |
|---|---|---|---|
| start-confirm time | HMI | **10 s** | reference-proposed [ref DOL-07] |

## UNCLAIMED IO (§8(a) per-instance sweep)

| Signal | Disposition |
|---|---|
| `DiscreteInputs.Conveyor7Ready` | bound — C2 |

## DELTAS

- **none-found** — searched: rung-A deltas, class reference §3, the §8 unclaimed sweep.

## OPEN

- **Q-C02 (BLOCKING)** — isolation binding unresolved; two candidates, neither instance-scoped.
```

**Also emit `gen/<project>/requirements.md`** — a register view derived from the `C-nn`/`P-nn` sets
(one REQ per relation, carrying its id, instance, provenance and any `Q-nn`), in the format of
`gen/test-project001/requirements.md`. **This is a handoff requirement, not a nicety:**
`review-functional` STOPs without a register, so a project specified through these rungs cannot be
functionally reviewed at all unless this view exists. The per-relation set is a strictly better trace
target than prose REQs — it hands the reviewer the per-instance interlock list directly.

**Its shape is a CONTRACT** — `converter relation-reconcile` parses it, so write it exactly: one
`### \`<Instance>\`` heading per instance (instance read from the **backticks**), over a table whose
**second column is the relation id**:

```
### `FilterUnitInst2` — Dust Filter Unit 1 (FilterUnitSystem)

| REQ | Rel | Text | Class | Provenance | Open |
|---|---|---|---|---|---|
| REQ-001 | C1 | Runs on the plant automatic start command … | mode | ref FU-01 | **Q-C04** |
```

The derived register is **complete by set-difference, and it says so**: the REQ set must
set-difference to **empty** against the union of all `C-nn`/`P-nn` ids in
`gen/<project>/equipment-specs/`, and both counts are stated explicitly in the register's provenance
header. *A relation that exists in the specs but not in the register is still rendered by rung D
(which reads the specs) — so the loss does not break the code, it breaks the review: the reviewer
traces a register that never mentions the relation, and its reverse pass then reports the correctly
rendered term as unrequested logic (C-606). A lossy register can recommend deleting a real interlock.*

## Self-check before you finish — run the checker on your OWN artifacts

```
converter signal-sweep --project ir/<project>/ --specs gen/<project>/equipment-specs \
                       --register gen/<project>/requirements.md --unclaimed gen/<project>/unclaimed-signals.md
```

It computes the exact denominator and residue, replacing any approximate count you were about to
write ("~190 swept") with an integer. **An unaccounted signal is a gap in YOUR coverage** — bind it,
disposition it, or raise its `Q-nn`; then re-run. *A real run's sweep revealed two global DBs
attributed to the wrong files — a fact nobody had caught by reading, and one the tool surfaced as 32
unaccounted signals.* Report the final counts; an approximate denominator cannot support a
completeness claim.

## Calibration

- **This is a CONTROL spec.** **Alarms are out of scope** — a separate artifact, defined later.
- **Completeness over brevity.** Full enumeration is correct here even when verbose; compacting it is
  rung D's job, via a declared and argued discharge — never by omission at this rung.
- **A delta is first-class** — carried from rung A into the spec, never dropped in the merge.
- **Safety content = stop** (hard rule 2). **Never invent tags/addresses** (hard rule 3).
- **Telemetry:** append one line to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`.
