# PlantAutoControl-bench-rerun3 — Plant behaviours (rung B)

Produced by `/gen-functional-analysis` (rung B of the structured spec pipeline A → B → C → D).
**Plant intent only** — no signals, no IO addresses, no booleans, no interface members, no block
names.

## Provenance

- **Produced:** 2026-08-05, `gen-functional-analysis` (skill-driven).
- **Behaviour source:** `gen/PlantAutoControl-bench/requirements.md` — the **REQ prose** (REQ-001 … REQ-022,
  the *Sequence classification* table, and the *Open questions* section).
  SHA-256 `c0de9b797b75085b40c167ad9e8b672ae25d48b0e74ed4a4f4f9e157ad46ecba`.
  The same file's *equipment-inventory table* was rung A's source and is deliberately not re-read as
  a behaviour source here.
- **Source kind: DERIVED.** The source's own Provenance section states: *"Reverse-derived, NO
  independent source document … derived entirely from the as-built `PlantAutoControl` logic … There
  is no functional description, spec sheet, or other independent source behind it."* Per this rung's
  three-way stop condition that means **proceed, with a blocking `Q-nn` recording the circularity,
  and mark every behaviour that has no independent citation.** That is done: **Q-B1 is that blocking
  question, and every behaviour below carries the `[D]` mark** — see the marker note.
- **Rung A artifact consumed:** `gen/PlantAutoControl-bench-rerun3/equipment-topology.md` (this run), used
  **only** to scope `applies-to` onto real instances. Rung A was **not edited**. Where this rung
  appears to contradict rung A, that is recorded as a `Q-nn` for rung C to reconcile — never
  corrected here.
- **Scope:** behaviours are recorded for the six in-scope instances (`FilterUnitInst1/2/3`,
  `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1`) and for the plant level where a behaviour
  lands on all equipment. Behaviours landing only on out-of-scope machines are **listed by ID in §G9
  and not elaborated**, so nothing is silently dropped.
- **Safety:** the source's REQ-022 records the E-stop function, plant health and safety interlocking
  as hardwired / other-block material. That is recorded below as explicitly out of PLC scope and its
  internals are **not** elaborated (hard rule 2). No F-content was read.

### The `[D]` marker — read this before reading any behaviour

`[D]` = **this behaviour has no independent citation.** Its only source is a register that was itself
read back out of the as-built code, so `[D]` means *"the plant does this because the code does this"*,
not *"the plant requires this"*. **Every behaviour in this artifact carries `[D]`. There is not one
exception**, because the source has no independent citation to give. The mark is repeated per line
rather than stated once, because a blanket disclaimer at the top is exactly what gets skimmed past
three rungs later.

---

## G1 — Plant-level automatic control

- **B-01** `[D]` The plant equipment train runs under automatic control as one coordinated system,
  commanded by a plant-wide run state rather than by an operator run command at each machine.
  [requirements.md REQ-001 — *"The plant equipment train — twenty named pieces of equipment … is run
  under automatic control as one coordinated system"*]
  `applies-to:` all six in-scope instances (as members of the train)

- **B-02** `[D]` While the plant is in its running state, each machine is commanded to start and run
  automatically, subject to its own permissives.
  [REQ-002 — *"While the plant is in its running state, each piece of equipment is commanded to start
  and run automatically"*]
  `applies-to:` FilterUnitInst1, FilterUnitInst2, FilterUnitInst3, MotorVSDInst1, MotorVSDInst3,
  TomraControlInst1

- **B-03** `[D]` The plant run state distinguishes at least a running state and a controlled-shutdown
  state; the automatic-control layer reads that state and does not compute it.
  [REQ-002 Notes — *"The plant run state is carried by the given … word; the running state
  corresponds to … and the controlled-shutdown state to … This block reads Status; it does not
  compute it."*]
  `applies-to:` plant
  `note:` the source flags the encoding itself as inferred (its Q-01). Carried, not resolved.

- **B-04** `[D]` No machine starts automatically until the plant pre-start warning phase has
  completed; every machine waits on that plant-level condition.
  [REQ-005 — *"No equipment starts automatically until the plant pre-start warning phase has
  completed."*]
  `applies-to:` all six in-scope instances
  `note:` the pre-start timer/sequence itself is another block (REQ-005 Notes) — out of scope here.

## G2 — Controlled shutdown

- **B-05** `[D]` On a plant controlled shutdown equipment does not all stop at once: each machine
  keeps running until the specific neighbouring machine in its shutdown chain has signalled that its
  own shutdown is complete, and only then stops — so material already on the line is cleared in
  order rather than stranded.
  [REQ-003 — *"On a plant controlled shutdown, equipment does not all stop at once."*]
  `applies-to:` all six in-scope instances
  `per-instance chain:` as recorded in rung A (relations 2, 3, 6, 7, 10, 11, 15, 22, 27)

- **B-06** `[D]` The filter units additionally hold through a controlled shutdown on a plant-level
  "fans shutdown ready" condition.
  [REQ-013 Notes — *"The drum separator also holds through shutdown on … ; filter units on
  FansShutdownReady"*]
  `applies-to:` FilterUnitInst1, FilterUnitInst2, FilterUnitInst3
  `contradiction-watch:` this is the **second phrasing** of the condition rung A recorded as the
  unsettled `/` in Q-A1. The word *"also"* is suggestive of an additional (AND) term but **does not
  settle it**, and taking it as settling would be exactly the silent resolution FI-33 forbids.
  **Q-A1 stays open and is restated here as Q-B7.**

- **B-07** `[D]` A machine's controlled shutdown is a different thing from an emergency stop; the
  E-stop function is not part of this behaviour.
  [REQ-003 Notes — *"This is distinct from an E-stop (out of scope …)"*]
  `applies-to:` all six in-scope instances

## G3 — Start permissives and interlocking

- **B-08** `[D]` A machine is permitted to start automatically only while the machine(s) receiving its
  material downstream are already enabled / up to speed — receiving equipment runs before the
  equipment feeding it is allowed to start, so material is never delivered onto a stopped machine.
  [REQ-004 — *"A machine is permitted to start automatically only while the machine(s) receiving its
  material downstream are already enabled / up to speed."*]
  `applies-to:` FilterUnitInst2, FilterUnitInst3, MotorVSDInst1, MotorVSDInst3, TomraControlInst1
  `not-applying-to:` FilterUnitInst1 — rung A records it as having no neighbour start permissive
  ("runs with plant"). Recorded as a scope exclusion, not as a contradiction; the source's own
  inventory says the same.
  `note:` REQ-004's Notes concede the downstream/upstream direction is reverse-derived (its Q-02);
  rung A's Q-A4 records the same gap. Neither is resolved.

- **B-09** `[D]` A machine's own "up to speed" declaration, once it has run and been confirmed running
  for a configured time, is what permits the machine it serves to start.
  [REQ-004 Source — *"each machine's auto-start is gated by a named neighbour's up-to-speed enable"*;
  class references FU-07 / VSD-08 / OS-09]
  `applies-to:` all six in-scope instances
  `flag:` this behaviour — the hinge the entire start cascade turns on — has **no independent
  citation whatsoever**: it appears in the register only inside a *Source* field describing the
  as-built, and in class references that are themselves code-derived (rung A Q-A2). It is marked
  `[D]` like everything else, and called out here because its weight is out of proportion to its
  evidence.

- **B-10** `[D]` The sorter-feed conveyor is permitted to start only while the optical sorter reports
  itself ready and is not faulted.
  [REQ-017 — *"The sorter-feed conveyor (VSD) is only permitted to start while the optical sorter is
  ready and not faulted."*]
  `applies-to:` MotorVSDInst3 (condition), TomraControlInst1 (subject of the condition)
  `agrees-with rung A:` yes — independently stated in the REQ prose and in the inventory table, so
  the deviation rung A recorded (a permissive on *ready/not-faulted* rather than on an *enable*) is
  real and not a transcription artefact. Its **rationale** remains unknown → rung A Q-A6 stands.

- **B-11** `[D]` Some machines only start when their plant sub-system is separately enabled; a machine
  whose sub-system enable is off does not start even when the plant is running.
  [REQ-013 — *"Some machines only start when their plant sub-system is separately enabled … A machine
  whose sub-system enable is off does not start even when the plant is running."*]
  `applies-to:` **unsettled for the in-scope set → Q-B2 (blocking).** The named list is *"the
  metal-collection conveyor … the drum separator … and the ejected-material conveyor,
  residual/discharge conveyors and the feed-conveyor reverse path"*. Whether "discharge conveyors"
  reaches `MotorVSDInst1` (the Discharge Conveyor VSD) is not settled.

- **B-12** `[D]` A dust filter unit's "enabled" state is one of the permissives for the air separator,
  but the air separator has a second, alternative start path that does not require the dust filters
  to be enabled — it requires the discharge-conveyor drive enabled together with a third-party
  link-out indication instead.
  [REQ-021 — *"The air-separator VSD is permitted to start either when the discharge-conveyor VSD and
  both dust filter units are enabled, or (alternatively) when the discharge-conveyor VSD is enabled
  and a third-party link-out signal is present."*]
  `applies-to:` FilterUnitInst2, FilterUnitInst3, MotorVSDInst1 (as the machines whose enables are
  consumed); the air separator itself is out of scope for this run
  `note:` the source concedes the process meaning of the alternative path is inferred (its Q-08).
  Consistent with rung A's bypassable-permissive delta on both dust filters.

## G4 — Modes and operator intervention

- **B-13** `[D]` While a machine is under hand / manual intervention, the automatic controlled-shutdown
  request for that machine is suppressed — the operator's hand control is not overridden by the plant
  shutdown cascade.
  [REQ-006 — *"While a machine is under hand / manual intervention, the automatic controlled-shutdown
  request for that machine is suppressed"*]
  `applies-to:` all six in-scope instances
  `contradiction:` the source records that **two machines gate their own automatic shutdown on a
  different machine's hand-intervention state** — and both of them are in scope for this run
  (`MotorVSDInst3` and `TomraControlInst1`). REQ-006 states the per-machine behaviour; the source's
  own Q-07 records the cross-reference as a possible as-built defect and asks for an owner ruling.
  **Both readings are recorded, neither is resolved → Q-B3 (blocking).**

- **B-14** `[D]` A machine may be taken to hand, in which case it runs on the operator's hand start
  command instead of the plant automatic start command.
  [class references FU-01 / VSD-01 / OS-01, inherited at rung A]
  `applies-to:` all six in-scope instances

## G5 — Faults and reset

- **B-15** `[D]` One operator system-reset command clears faults across all equipment at once; every
  machine's fault reset is driven from the same plant-wide reset.
  [REQ-007 — *"One operator system-reset command clears faults across all equipment at once"*]
  `applies-to:` all six in-scope instances

- **B-16** `[D]` The filter units additionally echo that plant-wide reset back to the unit itself as a
  fault-reset command to the machine.
  [REQ-007 Notes — *"Filter units and the ECS additionally echo this reset to a physical reset
  output"*; REQ-019 — *"drives a fault-reset output back to the unit"*]
  `applies-to:` FilterUnitInst1, FilterUnitInst2, FilterUnitInst3

- **B-17** `[D]` Each filter unit brings in its own remote-operational, running and fault indications
  from the unit.
  [REQ-019 — *"Each dust filter unit and the cyclone filter unit brings in its remote-operational,
  running, and fault feedbacks"*]
  `applies-to:` FilterUnitInst1, FilterUnitInst2, FilterUnitInst3

- **B-18** `[D]` The cyclone filter unit has no direct fault indication: its fault condition is the
  negation of a "system OK" indication from the unit.
  [REQ-019 — *"The cyclone filter unit's fault is taken from the inverse of its system-OK signal."*]
  `applies-to:` FilterUnitInst1
  `note:` an instance delta rung A could not see (it is carried by the signal set, invisible above
  rung C). Recorded here so rung C's §8 re-hunt has it in hand.

- **B-19** `[D]` The optical sorter's own reported machine faults, and loss of its data link, raise a
  latched fault on the sorter; its hardwired communications-fault indication is brought into the
  control.
  [REQ-017 — *"The optical sorter's hardwired ready, running, and communications-fault signals are
  brought into its control"*; class reference OS-07/OS-13]
  `applies-to:` TomraControlInst1

- **B-20** `[D]` A machine that is commanded to run and does not report running within a configured
  time raises a latched fail-to-run fault; a machine that reports running while not commanded for
  that same time raises a latched fail-to-stop fault.
  [class references FU-10/FU-11, VSD-12/VSD-13, OS-11/OS-12, inherited at rung A]
  `applies-to:` all six in-scope instances

## G6 — Running confirmation and feedback

- **B-21** `[D]` Each machine's running status is confirmed from its field running feedback.
  [REQ-009 — *"Each machine's running status is confirmed from its field running feedback signal"*]
  `applies-to:` FilterUnitInst2, FilterUnitInst3 (explicitly in REQ-009's cited scope);
  FilterUnitInst1 via B-17; MotorVSDInst1 see B-23; TomraControlInst1 see B-24;
  MotorVSDInst3 **unsettled → Q-B5**

- **B-22** `[D]` Belt conveyors additionally require a motion/rotation sensor to confirm the belt is
  actually turning before the belt counts as running, and each such conveyor's motion check can be
  individually bypassed by the operator (failed sensor, commissioning) so the conveyor can still run
  on its run feedback alone. Machines without a motion sensor confirm on run feedback only, and the
  dust filter units are named as machines without one.
  [REQ-011 — *"Belt conveyors require a motion/rotation sensor to confirm the belt is actually
  turning … Each such conveyor's rotation-sensor check can be individually bypassed by the
  operator"*; Notes — *"Machines without a rotation sensor (… dust filter units) confirm on run
  feedback only."*]
  `applies-to:` MotorVSDInst1 (see B-23); **not** FilterUnitInst2/FilterUnitInst3 (named as without);
  **not** FilterUnitInst1 (class has no motion sensor, FU-14); MotorVSDInst3 **unsettled → Q-B5**

- **B-23** `[D]` The discharge-conveyor drive treats its motion sensor as the source of its "running
  forward" confirmation — motion sensed means running — unless that sensor is bypassed, in which case
  it is not used at all.
  [REQ-012 — *"The discharge-conveyor VSD treats its rotation sensor as the source of its 'running
  forward' confirmation (motion sensed = running), unless that sensor is bypassed, in which case it
  is not used."*]
  `applies-to:` MotorVSDInst1
  `agrees-with rung A:` yes — rung A recorded the same as a PLUS delta from the inventory's extra-gate
  column, and the class reference notes the class never consumes the sensor internally, so this
  behaviour lives in the calling layer.
  `note:` "in which case it is not used" is a stated behaviour, not an inference: on bypass the
  running-forward confirmation loses its source rather than falling back. Recorded verbatim because
  the fallback question is exactly what a reader would otherwise assume.

- **B-24** `[D]` While the sorter's data link is healthy, the sorter counts as running only when both
  the link and the hardwired running signal say so; while the link is dead, the hardwired running
  signal alone decides. The same fallback applies to its communications-fault indication.
  [class reference OS-08, inherited at rung A; REQ-017 — *"hardwired ready, running, and
  communications-fault signals are brought into its control"*]
  `applies-to:` TomraControlInst1

- **B-25** `[D]` Each motor is inhibited from running while its local isolator indicates the machine is
  isolated.
  [REQ-010 — *"Each motor is inhibited from running while its local isolator feedback indicates the
  machine is isolated."*]
  `applies-to:` MotorVSDInst1, MotorVSDInst3 (both inside REQ-010's cited scope);
  **unsettled for FilterUnitInst1/2/3 and TomraControlInst1 → Q-B4 (blocking)** — their classes
  require the permissive (FU-02, OS-02) but REQ-010's cited scope excludes them.

## G7 — Stated quantitative parameters

- **B-26** `[D]` The filter units use a configurable fan start-up (up-to-speed) time before they are
  treated as enabling downstream equipment. **The commissioning value is 10 s.**
  [REQ-020 — *"use a configurable fan start-up (up-to-speed) time before they are treated as enabling
  downstream equipment; the commissioning value is 10 s."*]
  `applies-to:` FilterUnitInst1, FilterUnitInst2, FilterUnitInst3
  `value:` 10 s, stated by the source — not invented here. It is the **only** timing value the source
  states for any in-scope machine.

- **B-27** `[D]` Every other configured time these machines depend on — up-to-speed time for the two
  drives and the sorter, shutdown time, fail-to-run / fail-to-stop time, the sorter's data-link
  watchdog, the drive start-up (ramp) allowance — is **referred to as configurable but never given a
  value** by the source.
  [class references FU-07/FU-08/FU-10, VSD-08/VSD-09/VSD-11/VSD-12, OS-07/OS-09/OS-10/OS-11]
  `applies-to:` MotorVSDInst1, MotorVSDInst3, TomraControlInst1, FilterUnitInst1/2/3
  `note:` recorded as a gap, **not** filled with a plausible number → Q-B6 (non-blocking; the values
  are equipment-interface settings on a boundary the source declares given, so they may already be
  carried there — rung C is where that is checked, not guessed).

## G8 — Command path out to the machines

- **B-28** `[D]` A machine's resulting run command drives its physical start output.
  [REQ-008 — *"Each machine's resulting run command (from its control FB) drives the corresponding
  physical motor/starter start output."*]
  `applies-to:` FilterUnitInst1, FilterUnitInst2, FilterUnitInst3, TomraControlInst1

- **B-29** `[D]` The two in-scope drives are commanded through their drive interface rather than
  through a discrete physical start bit.
  [REQ-008 Notes — *"The incline feed VSD, air-separator VSD, sorter-conveyor VSD and shredder are
  commanded through their FB/VSD interface rather than a discrete start bit."*]
  `applies-to:` MotorVSDInst3 (named); **MotorVSDInst1 is named in neither list → Q-B8
  (non-blocking)** — it is absent from REQ-008's cited network scope and absent from this Notes list,
  so how the discharge-conveyor drive is commanded is not stated either way.

- **B-30** `[D]` The optical sorter is commanded both over its data link and by a hardwired run signal,
  both driven together from the same run decision, and its run command is driven to the sorter.
  [REQ-017 — *"its run command is driven to the sorter"*; class reference OS-05]
  `applies-to:` TomraControlInst1

- **B-31** `[D]` The optical sorter exchanges process-data words with the plant in addition to its
  hardwired signals.
  [REQ-017 Notes — *"The sorter also exchanges process-data words with the plant"*]
  `applies-to:` TomraControlInst1
  `note:` the source declares what those words carry, and whether reconstructing them is even in
  scope, as unclear (its Q-06). Recorded as an existing exchange, with no content claimed.

## G9 — Explicitly out of PLC scope, and behaviours not landing in scope

- **B-32** `[D]` Plant health, the E-stop function and safety interlocking are handled by the hardwired
  safety circuit and other blocks. **No PLC logic for them belongs in this layer**, and this layer
  treats every machine as system-healthy.
  [REQ-022 — *"Plant health, the E-stop function, and safety interlocking are handled by the
  hardwired safety circuit and other blocks — no PLC logic for them belongs in this block."*]
  `applies-to:` all six in-scope instances
  `hard rule 2:` recorded as an exclusion only. No safety internals are elaborated here or below.

- **B-33** `[D]` The plant master start/stop sequencer, the computation of the plant run-state value,
  the pre-start (siren) timer itself, and each equipment's internal sequence are **other blocks**, not
  this layer.
  [Provenance §*Scope of THIS block* — *"It does not contain … the plant master start/stop
  sequencer, the value/computation of the plant run-state word, the pre-start (siren) timer itself,
  the E-stop / safety circuit, or any equipment's internal sequence"*]
  `applies-to:` plant

- **Behaviours landing only on out-of-scope machines** — listed so they are visibly *excluded*, not
  lost: REQ-014 (third-party interlock on the incline feed and link conveyors), REQ-015 (automatic
  pre-start request keyed off the link conveyor), REQ-016 (shredder feed conveyor reversal), REQ-018
  (Equipment Control System integration). Each is named here and deliberately not elaborated; each
  concerns a neighbour that this run may reference in an interlock but does not specify.

---

## Open questions

- **Q-B1 — BLOCKING — the behaviour source is circular: every behaviour above was read out of the
  as-built code.**
  The source register states plainly that it was *"reverse-derived … entirely from the as-built
  `PlantAutoControl` logic"* with *"no functional description, spec sheet, or other independent
  source"*. Consequences, stated rather than glossed:
  1. **No behaviour above can falsify the code** — if the as-built does the wrong thing, this rung
     reproduces the wrong thing as an intention. `[D]` marks every one of the 33 behaviours; the
     count of behaviours with an independent citation is **zero**.
  2. **The most load-bearing behaviour is among the least evidenced.** B-09 (a machine's up-to-speed
     enable is what permits the machine it serves to start) is the hinge of the whole start cascade
     and exists only inside a *Source* field describing the as-built plus code-derived class
     references (rung A Q-A2 is the same finding one rung up).
  3. **The source's own defect list is inherited, not cleaned.** Its Q-01…Q-08 are inferences it
     flags itself; three of them (Q-02, Q-07, Q-08) touch in-scope equipment.
  **Blocks:** treating this artifact as a specification of what the plant *should* do. It is a
  faithful statement of what the plant *is described as doing*. **Needs:** a genuine functional
  description, or a recorded owner acceptance that the as-built is the specification.

- **Q-B2 — BLOCKING — does the general sub-system enable gate the Discharge Conveyor VSD
  (`MotorVSDInst1`)?**
  REQ-013 lists the general-enable machines as *"the ejected-material conveyor, residual/discharge
  conveyors and the feed-conveyor reverse path"*. Three problems, all in one clause:
  - `residual/discharge conveyors` joins two machine names with an ambiguous `/`. Split-and-retain
    would assert the gate on **both** the Residual Material Conveyor and a discharge conveyor; the
    appositive reading ("residual, i.e. discharge") would assert it on one. **Neither reading is
    taken** (FI-33: dropping either side requires this question).
  - The plant has **two** machines whose name contains "discharge conveyor": the Discharge Conveyor
    (a starter) and the Discharge Conveyor VSD, `MotorVSDInst1`, **which is in scope for this run**.
    The plural *"conveyors"* does not disambiguate.
  - REQ-013's own cited scope excludes both the Residual Material Conveyor and `MotorVSDInst1`, and
    **rung A records `MotorVSDInst1` with no sub-system-enable gate at all** — so the prose and the
    inventory table, two readings of the same document, disagree about an in-scope machine.
  **Blocks:** rung C's permissive set for `MotorVSDInst1` (a whole start permissive present or
  absent), and therefore rung D's shape for it. Recorded as a contradiction against rung A, not
  corrected here.

- **Q-B3 — BLOCKING — do the sorter conveyor drive and the optical sorter gate their own automatic
  shutdown on another machine's hand-intervention state, deliberately or by defect?**
  REQ-006 states the per-machine behaviour (B-13). The source's own Q-07 records that *"two machines
  (the sorter conveyor VSD and the optical sorter) gate their own automatic shutdown on a different
  machine's hand-intervention flag rather than their own"*, calls it *"a possible as-built copy/paste
  quirk rather than intended 'grouped hand' behaviour"*, and asks for an owner ruling. **Both machines
  are in scope for this run** — this is not a distant curiosity, it lands on two of six. Both readings
  are recorded; neither is adopted.
  **Blocks:** rung C's shutdown-suppression condition for `MotorVSDInst3` and `TomraControlInst1`.
  Building either reading silently would either bake in a defect or silently "fix" as-built behaviour
  without a decision — both unacceptable.

- **Q-B4 — BLOCKING — does an isolator inhibit apply to the three filter units and the optical
  sorter?**
  REQ-010 says *"each motor"* is inhibited while its local isolator indicates isolation, and its
  cited scope covers the two in-scope drives but **not** the filter units or the sorter. Their class
  references, however, inherit the permissive as a class requirement (FilterUnitSystem FU-02, optical-
  sorter OS-02 — *"does not start while it is inhibited (isolated / locked off)"*). So either those
  four machines have an isolator that REQ-010's scope omits, or they have the permissive with nothing
  driving it. Not resolved by reading "motor" narrowly, which is the tempting silent resolution.
  **Blocks:** rung C's permissive set for `FilterUnitInst1/2/3` and `TomraControlInst1`.
  **Note for rung D:** if the answer is "permissive present, nothing driving it", that is precisely an
  undriven-input finding — `undriven-scan` at D1 will show it as a fact, but the *intent* still needs
  the ruling.

- **Q-B7 — BLOCKING (restatement of rung A's Q-A1, carried forward unresolved) — how do a filter
  unit's two shutdown-hold conditions combine?**
  Rung A found the source's inventory writing the hold as `Fans-shutdown-ready / <neighbour> shut
  down`. This rung found the REQ prose phrasing the same thing as *"filter units **also** hold through
  shutdown on FansShutdownReady"*. "Also" leans toward an additional (AND) term — **and that lean is
  explicitly not taken as an answer**, because a lean is what a silent resolution is made of and the
  two readings differ materially (under OR a filter unit stops as soon as the plant-level condition
  appears, while the machine it is holding for is still shutting down).
  **Blocks:** rung C's shutdown-hold binding for all three in-scope filter units.

- **Q-B5 — non-blocking — does the sorter conveyor drive (`MotorVSDInst3`) use a motion sensor, and is
  it operator-bypassable?**
  REQ-011's positive list of motion-confirmed belt conveyors does not include it; REQ-011's Notes list
  of machines *without* a motion sensor does not include it either. It falls between the two lists.
  Its class declares the input but never consumes it (rung A's open item for this instance).
  Not blocking at this rung: the question is answerable from the signal set, which is rung C's
  material — **carried explicitly into rung C's §8 delta re-hunt** rather than being guessed here.

- **Q-B6 — non-blocking — the configured times other than the fan start-up time are never given
  values.**
  Up-to-speed, shutdown, fail-to-run/fail-to-stop, data-link watchdog and drive start-up allowance are
  all described as configurable, with no value stated for any in-scope machine (only the 10 s fan
  start-up time is stated, B-26). No number is invented here. Not blocking because these are settings
  on the equipment interface, which the source declares a *given* boundary — rung C establishes
  whether they are already carried there. If rung C finds them unset, this becomes blocking.

- **Q-B8 — non-blocking — how is the Discharge Conveyor VSD (`MotorVSDInst1`) commanded?**
  It is absent from REQ-008's cited scope (run command → physical start output) and absent from
  REQ-008's Notes list of machines commanded through their drive interface instead. The source states
  neither path for it. Not blocking at this rung — rung C reads the signal set and will show which
  path exists — but recorded because "absent from both lists" is the shape a genuine omission takes.

### Contradictions against rung A (recorded, none corrected)

| # | Rung A says | Rung B source says | Disposition |
|---|---|---|---|
| 1 | `MotorVSDInst1` has no sub-system-enable gate (inventory row 6) | REQ-013 prose lists "residual/discharge conveyors" under the general enable | **Q-B2, blocking.** Both retained. |
| 2 | Filter-unit shutdown hold = two conditions, combination unsettled (Q-A1) | REQ-013 Notes: filter units *"also"* hold on fans-shutdown-ready | **Q-B7, blocking.** The lean toward AND is not taken. |
| 3 | `TomraControlInst1` and `MotorVSDInst3` suppress their own auto-shutdown under their own hand control (class inheritance, B-13) | Source Q-07: both gate on *another* machine's hand state | **Q-B3, blocking.** Both readings retained. |
| 4 | Filter units and sorter inherit an isolation permissive (FU-02 / OS-02) | REQ-010's scope covers neither | **Q-B4, blocking.** |
| 5 | `MotorVSDInst3` rotation-sensor use unsettled | REQ-011 lists it in neither the positive nor the negative list | **Q-B5, non-blocking**, carried to rung C §8. |

**Agreements worth recording** (rung A and rung B independently state the same thing, which raises
confidence that these are real and not transcription artefacts): the sorter-conveyor permissive on the
sorter's ready/not-faulted state (B-10 vs rung A relations 18–19, 28); the air separator's bypassable
dust-filter permissive (B-12 vs rung A's PLUS delta on both dust filters); the discharge-conveyor
drive's motion-sensor running confirmation (B-23 vs rung A's PLUS delta on `MotorVSDInst1`).
