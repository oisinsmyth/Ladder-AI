# PlantAutoControl-bench-rerun — Equipment topology and per-equipment interlocks (rung A)

Produced by `/gen-pid-analysis` (rung A of the structured spec pipeline A→B→C→D).
**Process relations only.** No signals, no IO, no tag names, no booleans, no interface members.

## Provenance

- **Produced:** 2026-08-04, `gen-pid-analysis`.
- **Layout / topology source:** `gen/PlantAutoControl-bench/requirements.md`
  (SHA-256 `c0de9b797b75085b40c167ad9e8b672ae25d48b0e74ed4a4f4f9e157ad46ecba`) —
  specifically its **Equipment inventory** section: the twenty-row *Equipment instances and
  interlock links* table (columns *Starts after (permissive)*, *Holds through shutdown until*,
  *Extra gate*), supported by the REQ text where it names a specific machine.
- **What this source is NOT:** it is not a P&ID and not a plant layout. It is a **dependency
  table**. It records which machine's enable gates which machine's start, and which machine's
  shutdown-complete releases which machine — it does **not** record what feeds what. The source
  itself flags this (its Q-02: material-flow direction is inferred, unconfirmed). See **Q-03**.
- **Equipment class references used:**
  - `references/FilterUnitSystem/reference.md` v0-derived (from `ir/PlantAutoControl-bench/FilterUnitSystem.ir`,
    SHA-256 `aaa186b79abd0bf5789644808e4abcfe6575c589fa437353a4bf521252af9080`)
  - `references/vsd-motor/reference.md` v0-derived (from `ir/PlantAutoControl-bench/MotorVSDSystem.ir`,
    SHA-256 `aaa67a05084d60c57460bc86a16dfd43d99aaa873039d81569fe44c5980d3885`)
  - `references/optical-sorter/reference.md` v0-derived (from `ir/PlantAutoControl-bench/TomraControlSystem.ir`,
    SHA-256 `fb5914e73ec4c298295970e5eea75bb0c1560a9f57004a14a1cb1ca29e7fc219`)

  **All three were written by this run because no reference library exists in this repo.** They are
  reverse-derived from as-built FB code, not from an engineering standard. This defeats the purpose
  the references serve at this rung — see **Q-01**, blocking.
- **Scope of this run:** six instances (`FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1`,
  `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1`). Their neighbours are *referenced* by name
  where an interlock points at them; they are not specified here.
- **Safety:** no F-block, F-runtime, safety-program or E-stop content was read or referenced. The
  source records the E-stop function as hardwired and out of scope. Hard rule 2 not triggered.

## Convention statement (applied without exception)

This rung normally *derives* interlocks from material flow. **It cannot here** (Q-03), so a
different, narrower convention is declared and applied to every instance:

> **C-A1 — Verbatim transcription.** Every interlock relation is transcribed from the named cell of
> the source's inventory table (or from REQ text that names the machine). No relation is derived
> from, added to, or removed from that table by reasoning about flow.
>
> **C-A2 — Split, never join.** Where a source cell joins two conditions with an ambiguous
> separator (`/`, `&`, `+`, a comma, "and/or"), it is split into separate relations and **both are
> retained**. Where retaining both would assert the existence of an additional *thing* rather than
> an additional *condition*, split-and-retain is unsafe and a blocking `Q-nn` is raised instead.
>
> **C-A3 — Reciprocity is a check, not a licence.** The source's two columns are observed to be
> near-reciprocal: for almost every pair, *"M holds through shutdown until N has shut down"* appears
> together with *"N starts after M is enabled"*. Read in process terms, that is: **a machine enables
> the machine that feeds it once it is up to speed, and keeps running through a controlled shutdown
> until the machine it enabled has finished shutting down.** This reciprocity is used **only** to
> detect gaps. Where it fails, a question is raised. **A missing relation is never added by
> reciprocity.**

## Instances in scope

---

### `FilterUnitInst2` — Dust Filter Unit 1 : `FilterUnitSystem`

```
fed-by                : NOT ESTABLISHED — source is a dependency table, not a P&ID (Q-03)
discharges-to         : NOT ESTABLISHED — same (Q-03)
process role          : dust extraction filter unit (from equipment title only)

interlock  : may start only while the Sorter Conveyor VSD is enabled (up to speed)
interlock  : controlled shutdown holds until the plant declares its extraction fans ready to shut down
interlock  : controlled shutdown holds until the Air-Separator VSD has completed its shutdown
relation   : this unit's enable is one of the start permissives of the Air-Separator VSD
             (jointly with Dust Filter Unit 2 and the Discharge Conveyor VSD)

class reqs : inherits FilterUnitSystem FU-01 … FU-17 in full

deltas     : PLUS — its up-to-speed (fan start-up) time is set from a plant-level commissioning
             parameter shared by all three filter units, value 10 s, rather than being a per-unit
             setting.
deltas     : ASYMMETRY (see C-A3) — this unit's start permissive (Sorter Conveyor VSD) has no
             reciprocal hold: the Sorter Conveyor VSD holds until the Equipment Control System, not
             until this unit. So on a controlled shutdown the machine that gates this unit's start
             may stop while this unit is still running. Non-blocking Q-10.
deltas     : searched — inventory table row 3 (all six columns); REQ-003, REQ-004, REQ-005,
             REQ-011, REQ-013, REQ-019, REQ-020 text; FilterUnitSystem reference v0-derived
             (§Class requirement set, §Class deltas). NOT searchable at this rung: the IO table —
             a delta carried only by an operator/bypass signal is invisible here by construction
             and MUST be re-hunted at rung C (`gen-equipment-spec` §8).
provenance : start permissive <- inventory row 3 "Starts after"; both hold relations <- inventory
             row 3 "Holds through shutdown until" (split per C-A2, Q-09); Air-Separator relation
             <- inventory row 5 "Starts after"; fan time <- inventory row 3 "Extra gate" + REQ-020;
             class reqs <- FilterUnitSystem reference v0-derived
```

---

### `FilterUnitInst3` — Dust Filter Unit 2 : `FilterUnitSystem`

```
fed-by                : NOT ESTABLISHED (Q-03)
discharges-to         : NOT ESTABLISHED (Q-03)
process role          : dust extraction filter unit (from equipment title only)

interlock  : may start only while the Sorter Conveyor VSD is enabled (up to speed)
interlock  : controlled shutdown holds until the plant declares its extraction fans ready to shut down
interlock  : controlled shutdown holds until the Air-Separator VSD has completed its shutdown
relation   : this unit's enable is one of the start permissives of the Air-Separator VSD
             (jointly with Dust Filter Unit 1 and the Discharge Conveyor VSD)

class reqs : inherits FilterUnitSystem FU-01 … FU-17 in full

deltas     : PLUS — shared plant-level fan start-up time, 10 s (as Dust Filter Unit 1).
deltas     : ASYMMETRY (C-A3) — start permissive has no reciprocal hold (as Dust Filter Unit 1).
             Non-blocking Q-10.
deltas     : DIFFERENCE-FROM-SIBLING — none found. Rows 3 and 4 of the inventory are identical in
             every column, and no REQ text distinguishes Dust Filter Unit 1 from Dust Filter Unit 2.
             Two nominally identical units whose *only* recorded difference is their number are
             exactly where a real per-unit deviation hides; re-hunt at rung C.
deltas     : searched — inventory table row 4 (all six columns) and row 3 for comparison;
             REQ-003/004/005/011/013/019/020 text; FilterUnitSystem reference v0-derived. NOT searchable
             at this rung: the IO table (rung C §8 re-hunt required).
provenance : as Dust Filter Unit 1, from inventory row 4
```

---

### `FilterUnitInst1` — Cyclone Filter Unit : `FilterUnitSystem`

```
fed-by                : NOT ESTABLISHED (Q-03)
discharges-to         : NOT ESTABLISHED (Q-03)
process role          : cyclone dust extraction filter unit (from equipment title only)

interlock  : NO equipment start permissive is recorded. The source cell reads "(runs with plant)".
             This is the permissive reading and is NOT adopted — BLOCKING Q-04.
interlock  : controlled shutdown holds until the plant declares its extraction fans ready to shut down
interlock  : controlled shutdown holds until the Shredder has completed its shutdown
relation   : no machine in the twenty-row inventory names this unit's enable as a start permissive.
             Under C-A3 the reciprocal of its hold on the Shredder would be "the Shredder starts
             after this unit is enabled" — the Shredder's start permissive cell instead reads
             "Discharge Conveyor enabled", whose referent is ambiguous. UNRESOLVED — BLOCKING Q-05.

class reqs : inherits FilterUnitSystem FU-01 … FU-17 in full

deltas     : MINUS (candidate) — no upstream-machine start permissive at all, unlike its two
             sibling units. BLOCKING Q-04.
deltas     : CHANGED (candidate) — its fault condition is described as the inverse of a
             system-healthy indication from the unit, where the dust filter units take a direct
             fault indication. Whether that *replaces* the class fault feedback (FU-12) or is an
             *additional* second source is not settled by the source. Split-and-retain (C-A2)
             would assert a second physical input, so it is not taken — BLOCKING Q-12.
deltas     : PLUS — shared plant-level fan start-up time, 10 s.
deltas     : searched — inventory table row 19 (all six columns); REQ-003/004/005/013/019/020 text;
             FilterUnitSystem reference v0-derived (§Class requirement set, §Class deltas). NOT
             searchable at this rung: the IO table (rung C §8 re-hunt required). Specifically
             flagged for rung C: the source's REQ-019 note lists a distinct automatic-running and
             stopped indication pair for this unit that its siblings do not have — that is a
             signal-level observation this rung must not adopt, and rung C must resolve.
provenance : hold relations <- inventory row 19 "Holds through shutdown until" (split per C-A2,
             Q-09); absent start permissive <- inventory row 19 "Starts after"; fault description
             <- REQ-019 text; fan time <- inventory row 19 "Extra gate" + REQ-020
```

---

### `MotorVSDInst1` — Discharge Conveyor VSD : `vsd-motor`

```
fed-by                : NOT ESTABLISHED (Q-03)
discharges-to         : NOT ESTABLISHED (Q-03)
process role          : variable-speed discharge conveyor associated with the air separator
                        (from equipment title only)

interlock  : may start only while the Overband Magnet is enabled
interlock  : may start only while the Equipment Control System is enabled
interlock  : controlled shutdown holds until the Air-Separator VSD has completed its shutdown
relation   : this machine's enable is one of the start permissives of the Air-Separator VSD
             (jointly with Dust Filter Units 1 and 2)
relation   : the Overband Magnet holds through shutdown until this machine has completed its shutdown
relation   : the Equipment Control System holds through shutdown until this machine has completed
             its shutdown
relation   : whether the Shredder's start permissive refers to this machine is UNRESOLVED —
             the plant contains three similarly-named machines. BLOCKING Q-05.

class reqs : inherits vsd-motor VSD-01 … VSD-20 in full

deltas     : PLUS — this instance confirms that it is running forward from a belt motion/rotation
             sensor (motion sensed = running), which its class does not do on its own; the class
             declares the sensor but never acts on it, so this behaviour lives wholly in the
             plant control layer.
deltas     : PLUS — the operator can individually bypass that motion sensor. What confirms running
             forward while the bypass is applied is NOT settled by the source, which says only that
             the sensor "is not used". BLOCKING Q-07.
deltas     : searched — inventory table row 6 (all six columns) and rows 5, 8, 9, 18 for reverse
             relations; REQ-004/008/009/011/012 text; vsd-motor reference v0-derived (§Class
             requirement set, §Class deltas, incl. the note that the rotation-sensor member is
             never consumed inside the class). NOT searchable at this rung: the IO table (rung C
             §8 re-hunt required) — note that this instance's operator bypass is precisely the
             "delta carried only by an operator signal" case the method warns about, and it was
             only visible here because the REQ text happens to state it in prose.
provenance : start permissives <- inventory row 6 "Starts after" (split per C-A2); hold <- inventory
             row 6 "Holds through shutdown until"; reverse relations <- inventory rows 5, 8, 9;
             motion-sensor deltas <- inventory row 6 "Extra gate" + REQ-011 + REQ-012;
             class reqs <- vsd-motor reference v0-derived
```

---

### `MotorVSDInst3` — Sorter Conveyor VSD : `vsd-motor`

```
fed-by                : NOT ESTABLISHED (Q-03)
discharges-to         : NOT ESTABLISHED (Q-03)
process role          : variable-speed conveyor feeding the optical sorter (from equipment title
                        and REQ-017's description of it as the sorter-feed conveyor)

interlock  : may start only while the Optical Sorter reports itself ready
interlock  : may start only while the Optical Sorter is free of faults
interlock  : may start only while the Ejected Material Conveyor is enabled
interlock  : may start only while the Residual Material Conveyor is enabled
interlock  : controlled shutdown holds until the Equipment Control System has completed its shutdown
relation   : this machine's enable is a start permissive of Dust Filter Unit 1
relation   : this machine's enable is a start permissive of Dust Filter Unit 2
relation   : this machine's enable is a start permissive of the Equipment Control System
relation   : the Optical Sorter holds through shutdown until this machine has completed its shutdown

class reqs : inherits vsd-motor VSD-01 … VSD-20 in full

deltas     : PLUS — it carries a readiness-and-health permissive on a *different* machine (the
             Optical Sorter), not merely that machine's up-to-speed enable. No other machine in
             scope gates its start on a neighbour's health.
deltas     : CANDIDATE DEFECT — the source records that this machine's own automatic shutdown is
             gated on ANOTHER machine's hand-intervention state rather than its own, and asks for
             an owner ruling on whether that is intended grouping or a copy/paste defect. Not
             resolved here. BLOCKING Q-06.
deltas     : ASYMMETRY (C-A3) — three of its four start permissives are non-reciprocal: the Ejected
             and Residual Material Conveyors hold on the Optical Sorter, not on this machine, and
             its own hold is on the Equipment Control System rather than on any machine it gates.
             Non-blocking Q-11.
deltas     : searched — inventory table row 10 (all six columns) and rows 3, 4, 9, 11, 13, 14 for
             reverse relations; REQ-004/013/017 text and the source's own Q-07; vsd-motor reference
             v0-derived. NOT searchable at this rung: the IO table (rung C §8 re-hunt required) —
             note that unlike the Discharge Conveyor VSD this instance's "Extra gate" cell is
             empty, which is the weakest possible evidence of absence.
provenance : start permissives <- inventory row 10 "Starts after" (split per C-A2 into four);
             hold <- inventory row 10 "Holds through shutdown until"; reverse relations <-
             inventory rows 3, 4, 9, 11; sorter readiness <- REQ-017 text; hand-flag candidate
             defect <- source register Q-07; class reqs <- vsd-motor reference v0-derived
```

---

### `TomraControlInst1` — Optical Sorter : `optical-sorter`

```
fed-by                : NOT ESTABLISHED (Q-03)
discharges-to         : NOT ESTABLISHED (Q-03)
process role          : optical sorting machine (from equipment title only). The source describes
                        the Sorter Conveyor VSD as its feed conveyor (REQ-017), but does not state
                        it as a flow relation — recorded as a hint for Q-03, not as a fact.

interlock  : may start only while the Ejected Material Conveyor is enabled
interlock  : may start only while the Residual Material Conveyor is enabled
interlock  : controlled shutdown holds until the Sorter Conveyor VSD has completed its shutdown
relation   : this machine's readiness and freedom from faults is a start permissive of the
             Sorter Conveyor VSD
relation   : the Ejected Material Conveyor holds through shutdown until this machine has completed
             its shutdown
relation   : the Residual Material Conveyor holds through shutdown until this machine has completed
             its shutdown

class reqs : inherits optical-sorter OS-01 … OS-19 in full

deltas     : PLUS — the machine is a third-party unit exchanging process data over a data link in
             addition to hardwired signals. What that exchange carries, and whether reconstructing
             it is in scope at all, is not settled by the source. BLOCKING Q-08.
deltas     : CANDIDATE DEFECT — as with the Sorter Conveyor VSD, the source records this machine's
             own automatic shutdown as gated on ANOTHER machine's hand-intervention state, and asks
             for an owner ruling. BLOCKING Q-06.
deltas     : CLASS-SCOPE CAVEAT — this is the only instance of its class, so nothing peculiar to it
             can be distinguished from a class property by observation. Every "class requirement"
             it inherits could equally be an instance delta. BLOCKING Q-02.
deltas     : searched — inventory table row 11 (all six columns) and rows 10, 13, 14 for reverse
             relations; REQ-004/017 text and the source's own Q-06 and Q-07; optical-sorter
             reference v0-derived (§Class requirement set, §Class deltas, §Observed as-built
             anomalies). NOT searchable at this rung: the IO table (rung C §8 re-hunt required).
provenance : start permissives <- inventory row 11 "Starts after" (split per C-A2 into two);
             hold <- inventory row 11 "Holds through shutdown until"; reverse relations <-
             inventory rows 10, 13, 14; readiness permissive <- REQ-017 text; data link <-
             inventory row 11 "Extra gate" + source register Q-06; class reqs <- optical-sorter
             reference v0-derived
```

---

## Machines referenced but not specified by this run

Named above only as the far end of an interlock. No requirement set was derived for them:
Air-Separator VSD, Overband Magnet, Equipment Control System, Ejected Material Conveyor,
Residual Material Conveyor, Shredder, Discharge Conveyor, Metal Collection Conveyor.

## Open questions

Blocking questions must be answered before rung C binds signals to these relations. This rung does
not resolve any of them.

### Blocking

- **Q-01 — BLOCKING. No equipment class reference library exists.** The method requires
  `references/<class>/reference.md` per class and states that a class with no reference is a
  blocking question, "never an improvised requirement set". No such library exists in this repo.
  Three references were written by this run instead, reverse-derived from the as-built FB code
  (`FilterUnitSystem.ir`, `MotorVSDSystem.ir`, `TomraControlSystem.ir`). **That does not discharge the requirement.**
  The references exist so an instance inherits its class's *standard* requirement set and cannot
  silently forget one; a reference read out of the code cannot contain anything the code omits, so
  the completeness-by-construction property this rung depends on is **not** achieved for any of the
  six instances. *Needed:* the site's real FilterUnitSystem, VSD-motor and optical-sorter control
  standards, or an explicit owner ruling that as-built-derived references are accepted for this run
  with the gap acknowledged.

- **Q-02 — BLOCKING. `optical-sorter` is a single-instance class.** `TomraControlInst1` is the only
  instance, so class properties and instance deltas are indistinguishable by observation. Every
  OS-01…OS-19 requirement could be an instance peculiarity, and any delta this machine has against
  a real optical-sorter standard is invisible. Compounds Q-01. *Needed:* the class standard, or a
  ruling that this machine is specified as a one-off with no class inheritance claimed.

- **Q-03 — BLOCKING. No material-flow source exists.** The method requires `fed-by` /
  `discharges-to` per instance. The only topology source is a start/shutdown dependency table; the
  source register itself records (its Q-02) that flow direction was inferred from that dependency
  graph and is unconfirmed. Deriving flow from the dependency table here would re-inject that same
  inference as if it were evidence. All twelve flow fields above are therefore `NOT ESTABLISHED`,
  and the convention C-A1 (verbatim transcription) was adopted in place of the method's intended
  flow-derived convention. *Needed:* a P&ID, plant layout, or material-flow schedule. *Impact if
  unanswered:* rung B cannot state why any interlock exists, only that it does; every "so material
  is not delivered onto a stopped machine" rationale is unverifiable.

- **Q-04 — BLOCKING. Cyclone Filter Unit has no recorded start permissive.** Row 19's "Starts
  after" cell reads "(runs with plant)" where every other filter unit names a machine. Read
  literally, this unit starts with no equipment interlock whatsoever — the weakest/most permissive
  reading available, which this rung's explicitness rule forbids adopting silently. It is equally
  readable as an editorial gloss over a permissive that was never captured. *Needed:* confirmation
  that the Cyclone Filter Unit genuinely has no equipment start permissive, or the missing one.

- **Q-05 — BLOCKING. The Shredder's start permissive has an ambiguous referent and is
  under-enumerated.** Row 18 reads "Discharge Conveyor enabled". Three machines could be meant:
  the Discharge Conveyor VSD (row 6, in scope), the Discharge Conveyor (row 20), or — under the
  reciprocity observed in C-A3 — the Cyclone Filter Unit (row 19, in scope), because both row 19
  and row 20 hold through shutdown until the Shredder, which under reciprocity implies the
  Shredder's start should be gated by **both**. The cell names one machine. This is simultaneously
  an ambiguous referent and an incomplete enumeration, and it decides a reverse relation for two of
  the six instances in scope. *Needed:* the full, unambiguous list of machines whose enable gates
  the Shredder's start.

- **Q-06 — BLOCKING. Cross-referenced hand-intervention on the Sorter Conveyor VSD and the Optical
  Sorter.** The source records that these two machines gate their own automatic controlled-shutdown
  on a *different* machine's hand-intervention state, flags it as a probable copy/paste defect
  rather than intended grouped-hand behaviour, and explicitly asks for an owner ruling. Two of the
  six instances in scope are the affected machines, so this rung cannot proceed past it by choosing.
  *Needed:* ruling — deliberate grouping (a real requirement, to be specified), or a defect (to be
  specified as per-machine and the deviation recorded).

- **Q-07 — BLOCKING. Discharge Conveyor VSD: what confirms running while the motion sensor is
  bypassed?** The source states this machine takes its running-forward confirmation *from* its belt
  motion sensor, and that when the operator bypasses that sensor it "is not used". It names no
  substitute. Two readings: (a) it falls back to the drive's own running feedback, as the other
  bypassable conveyors do; (b) running-forward confirmation is simply absent while bypassed. These
  differ in observable plant behaviour and (b) is a materially unsafe reading to adopt by default.
  *Needed:* the bypassed-case behaviour.

- **Q-08 — BLOCKING. Optical Sorter data-link scope and content.** The machine exchanges process
  data with the plant over a link in addition to its hardwired signals. The source (its Q-06) states
  the content is unclear and that whether reconstructing the interface is in scope is itself
  undecided. This is a first-class instance delta and cannot be specified without an answer.
  *Needed:* scope ruling, and if in scope, the data-link definition.

- **Q-12 — BLOCKING. Cyclone Filter Unit: one fault source or two?** The source says in one sentence
  that every filter unit brings in a fault feedback, and in the next that the Cyclone Filter Unit's
  fault is taken from the inverse of its system-healthy indication. Reading these as one fact
  ("its fault feedback, i.e. the inverse of system-OK") is an appositive resolution, which this
  rung's explicitness rule forbids taking silently. The rule's default remedy — split and retain
  both — is **not safe here**, because the two sides are *inputs*, not conditions: retaining both
  would assert a second physical fault input on the machine, which is inventing hardware (hard rule
  3). The blocking question is therefore the only available branch. *Needed:* does the Cyclone
  Filter Unit have a direct fault indication, a system-healthy indication, or both?

### Non-blocking

- **Q-09 — Filter-unit hold conditions: conjunctive or disjunctive?** Rows 3, 4 and 19 all give the
  hold as "Fans-shutdown-ready / Air-separator VSD shut down" (row 19: "/ Shredder shut down"). The
  `/` is the exact ambiguous separator this rung's explicitness rule names. Per C-A2 the default
  was applied: **split into two relations, both retained**, i.e. the unit holds until *both* the
  plant fans-shutdown-ready condition *and* the named neighbour's shutdown-complete. This is the
  stricter (longer-running) reading, not the permissive one, and it is corroborated by the REQ text,
  which states the plant fans-ready hold and the per-machine neighbour hold as two separate
  requirements. Non-blocking, but please confirm the conjunction.

- **Q-10 — Dust filter units: start permissive with no reciprocal hold.** Both dust filter units are
  gated by the Sorter Conveyor VSD's enable, but that machine holds through shutdown on the
  Equipment Control System, not on the filter units. Every other pair in the inventory is
  reciprocal. Plausibly deliberate (extraction should outlive the machines it serves), but it is the
  kind of asymmetry that is also what a mis-transcription looks like. Confirmation requested; no
  relation was added or removed.

- **Q-11 — Sorter Conveyor VSD: three non-reciprocal start permissives.** Its permissives are the
  Optical Sorter, the Ejected Material Conveyor and the Residual Material Conveyor; its own hold is
  on the Equipment Control System, and the two conveyors hold on the Optical Sorter rather than on
  it. Recorded as an observation only.

- **Q-13 — Cyclone Filter Unit's enable is consumed by nobody.** No row in the inventory names it as
  a start permissive. Subsumed by Q-05 if the Shredder's permissive turns out to include it;
  otherwise this unit is enabled-for-no-one, which makes its up-to-speed enable purposeless.
