# PlantAutoControl-bench-rerun — Plant-level behaviours (rung B)

Produced by `/gen-functional-analysis` (rung B of the structured spec pipeline A→B→C→D).
**Plant intent only.** No signals, no IO addresses, no booleans, no interface members, no block
names.

## Provenance

- **Produced:** 2026-08-04, `gen-functional-analysis`.
- **Behaviour source:** `gen/PlantAutoControl-bench/requirements.md`
  (SHA-256 `c0de9b797b75085b40c167ad9e8b672ae25d48b0e74ed4a4f4f9e157ad46ecba`) — its **REQ text**,
  REQ-001 … REQ-022, plus the "Sequence classification" section. Citations below take the form
  `[REQ-nnn — "<leading words of the requirement text>"]`.
- **What this source is:** a numbered requirements register whose own provenance section states it
  was **reverse-derived entirely from as-built logic, with no independent functional description
  behind it**. It is treated here as the supplied functional description because it is the only
  behaviour source available — but it carries that document's own caveat: every behaviour in it was
  read back out of code, so a behaviour the code never implemented cannot appear in it. See **Q-B1**.
- **Rung A artifact consumed:** `gen/PlantAutoControl-bench-rerun/equipment-topology.md` (this run), used
  only to scope `applies-to`. **Not edited.** Where this rung appears to contradict rung A, both
  readings are recorded and a question raised — never corrected here.
- **Scope:** six instances — `FilterUnitInst2` (Dust Filter Unit 1), `FilterUnitInst3` (Dust Filter
  Unit 2), `FilterUnitInst1` (Cyclone Filter Unit), `MotorVSDInst1` (Discharge Conveyor VSD),
  `MotorVSDInst3` (Sorter Conveyor VSD), `TomraControlInst1` (Optical Sorter). Behaviours the source
  states plant-wide are recorded as such, with the in-scope instances enumerated.
- **Safety:** the plant emergency-stop function and safety interlocking are recorded below as
  explicitly outside PLC scope (G8). Their internals are not read, elaborated or referenced.

---

## G1 — Plant coordination and modes

```
B-01  The plant's equipment train — twenty named machines — is run as one coordinated system under
      automatic control, commanded by the plant's own run state rather than by a per-machine
      operator run command.
      [REQ-001 — "The plant equipment train — twenty named pieces of equipment..."]
      applies-to: plant; all six in-scope instances

B-02  While the plant is in its running state, every machine is commanded to start and run
      automatically, subject to its own start permissives.
      [REQ-002 — "While the plant is in its running state, each piece of equipment is commanded..."]
      applies-to: plant; all six in-scope instances

B-03  The plant distinguishes at least three run states: stopped/idle, running, and a controlled
      shutdown in progress. The automatic control layer consumes this state; it does not decide it.
      [REQ-002 — notes: "The plant run state is carried by..."; source register Q-01]
      applies-to: plant
      NOTE: whether finer sub-states exist within "running" is unresolved in the source — Q-B2.

B-04  Certain machines only run when their plant sub-system is separately enabled by the operator;
      a machine whose sub-system enable is off does not start even while the plant is running.
      [REQ-013 — "Some machines only start when their plant sub-system is separately enabled..."]
      applies-to: named sub-system gates are the magnet circuit, the drum separator, and a general
      enable. None of the six in-scope instances is named as carrying one — Q-B3.
```

## G2 — Start sequence

```
B-05  No machine starts automatically until the plant's pre-start warning phase has completed.
      Every machine waits on that completion before its automatic run is permitted.
      [REQ-005 — "No equipment starts automatically until the plant pre-start warning phase..."]
      applies-to: all six in-scope instances
      NOTE: the source states no duration for the warning phase, and states the warning timer
      itself belongs to another part of the plant. No value invented — Q-B4.

B-06  The pre-start warning is requested automatically, once per start, when automatic start is
      first requested; the request clears once the warning has become active.
      [REQ-015 — "When automatic start is first requested (link conveyor commanded to auto-start..."]
      applies-to: plant (the request is raised by one designated first machine — not one of the six)

B-07  A machine is permitted to start automatically only while the machine(s) receiving its
      material downstream are already enabled and up to speed, so that material is never delivered
      onto a stopped machine.
      [REQ-004 — "A machine is permitted to start automatically only while the machine(s) receiving
      its material downstream are already enabled..."]
      applies-to: all six in-scope instances
      NOTE: the per-machine links are transcribed at rung A. This behaviour states the *reason* for
      them (downstream receiver readiness). Rung A could not establish material flow at all, so the
      reason cannot be checked against the links, and for at least one in-scope machine the two
      appear to disagree — Q-B5, blocking.

B-08  The start cascade therefore runs downstream-machine-first: the last machine in a material
      path starts first and each feeding machine follows once its receiver is enabled.
      [REQ-004 — notes: "Cascade direction (which machine is 'downstream')..."; source register Q-02]
      applies-to: all six in-scope instances
      NOTE: the source marks this direction as inferred, not confirmed — carried as Q-B5.

B-09  The optical sorter's feed conveyor is permitted to start only while the optical sorter is
      ready and not faulted — a readiness condition on the machine it feeds, over and above that
      machine's up-to-speed enable.
      [REQ-017 — "The optical sorter's hardwired ready, running, and communications-fault signals..."]
      applies-to: MotorVSDInst3 (permissive), TomraControlInst1 (source of the permissive)

B-10  A machine may have an alternative start path: the air separator may start either on the
      discharge conveyor VSD plus both dust filter units being enabled, or on the discharge conveyor
      VSD plus a third-party link-out indication.
      [REQ-021 — "The air-separator VSD is permitted to start either when the discharge-conveyor
      VSD and both dust filter units are enabled..."]
      applies-to: Air-Separator VSD (out of scope). In scope only as the consumer of the enables of
      FilterUnitInst2, FilterUnitInst3 and MotorVSDInst1.
      NOTE: the process meaning of the alternative path — apparently a degraded running mode in
      which the dust filters are not required — is inferred, not stated. Q-B6.
```

## G3 — Controlled shutdown sequence

```
B-11  On a plant controlled shutdown, machines do not all stop together. Each machine keeps running
      until a specific neighbouring machine in its shutdown chain reports that it has completed its
      own shutdown, then stops — so material already on the line is cleared in order rather than
      stranded.
      [REQ-003 — "On a plant controlled shutdown, equipment does not all stop at once..."]
      applies-to: all six in-scope instances

B-12  The filter units additionally hold through a controlled shutdown on a plant-level indication
      that the extraction fans are ready to shut down, so extraction outlives the machines it
      serves.
      [REQ-013 — notes: "...filter units on <plant fans-shutdown-ready>"]
      applies-to: FilterUnitInst1, FilterUnitInst2, FilterUnitInst3
      NOTE: B-11 says a machine holds on *a* named neighbour (singular); B-12 adds a second,
      plant-level hold for these three machines. Both retained, not merged — Q-B7.

B-13  A machine under hand/manual intervention has its automatic controlled-shutdown request
      suppressed: the plant shutdown cascade does not override the operator's hand control of that
      machine.
      [REQ-006 — "While a machine is under hand / manual intervention, the automatic
      controlled-shutdown request for that machine is suppressed..."]
      applies-to: all six in-scope instances
      NOTE: the source records that two machines instead take a *different* machine's hand state,
      flags it as a probable defect, and asks for an owner ruling. Both of those machines are in
      scope. Not resolved — Q-B8, blocking.

B-14  A controlled shutdown is not an emergency stop; the two are different functions with
      different scope.
      [REQ-003 — notes: "This is distinct from an E-stop (out of scope, REQ-020)"]
      applies-to: plant
      NOTE: that cross-reference points at REQ-020, which is the fan start-up time, not the E-stop
      requirement (which is REQ-022). Recorded as a source citation error, not corrected — Q-B9.
```

## G4 — Operator interaction

```
B-15  One operator system-reset command clears faults across every machine at once; there is no
      per-machine reset command.
      [REQ-007 — "One operator system-reset command clears faults across all equipment at once..."]
      applies-to: all six in-scope instances

B-16  For the filter units, that same reset is additionally passed back to the unit itself as a
      reset command to the machine.
      [REQ-019 — "Each dust filter unit and the cyclone filter unit brings in its remote-operational,
      running, and fault feedbacks, and drives a fault-reset output back to the unit"]
      applies-to: FilterUnitInst1, FilterUnitInst2, FilterUnitInst3

B-17  The operator can individually bypass a belt conveyor's motion-sensor check — for a failed
      sensor or during commissioning — so the conveyor can still run without it.
      [REQ-011 — "Belt conveyors require a motion/rotation sensor to confirm the belt is actually
      turning... Each such conveyor's rotation-sensor check can be individually bypassed..."]
      applies-to: MotorVSDInst1 (the only in-scope machine the source names as bypassable)

B-18  An operator may take an individual machine to hand control, in which case that machine runs
      on the operator's own start command rather than the plant's.
      [REQ-006 — "While a machine is under hand / manual intervention..."; equipment class
      references FU-01 / VSD-01 / OS-01 via rung A]
      applies-to: all six in-scope instances
```

## G5 — Fault handling

```
B-19  Each machine raises a fault if it is commanded to run and does not report running within a
      configurable time, or if it reports running while not commanded for that same time.
      [rung A class requirement sets FU-10/FU-11, VSD-12/VSD-13, OS-11/OS-12 — inherited by every
      in-scope instance]
      applies-to: all six in-scope instances
      NOTE: this behaviour is NOT stated anywhere in the behaviour source. It reaches this rung only
      through the class references, which rung A records as reverse-derived from as-built code
      (its Q-01). Flagged so nobody reads it as site-stated — Q-B1.

B-20  A faulted machine does not start, and stays faulted until the operator reset.
      [rung A class requirement sets FU-03/FU-13, VSD-03/VSD-15, OS-03/OS-14]
      applies-to: all six in-scope instances
      NOTE: as B-19, class-derived rather than source-stated — Q-B1.

B-21  Each of the filter units reports its own fault back to the plant. The cyclone filter unit's
      fault is described as the inverse of a system-healthy indication from the unit.
      [REQ-019 — "...brings in its remote-operational, running, and fault feedbacks... The cyclone
      filter unit's fault is taken from the inverse of its system-OK signal"]
      applies-to: FilterUnitInst1 (inverted description), FilterUnitInst2, FilterUnitInst3
      NOTE: whether the cyclone unit has one fault source or two is unresolved at rung A (its Q-12)
      and is not resolved here.

B-22  The optical sorter's communications health is monitored, and a communications fault from the
      machine is treated as a fault of that machine.
      [REQ-017 — "The optical sorter's hardwired ready, running, and communications-fault signals
      are brought into its control"]
      applies-to: TomraControlInst1
```

## G6 — Running confirmation from the field

```
B-23  Each machine's running status is confirmed from its own field running feedback.
      [REQ-009 — "Each machine's running status is confirmed from its field running feedback signal"]
      applies-to: all six in-scope instances

B-24  Belt conveyors require a motion/rotation sensor confirming the belt is actually turning, in
      addition to the run feedback, before the belt counts as running.
      [REQ-011 — "Belt conveyors require a motion/rotation sensor to confirm the belt is actually
      turning, in addition to the run feedback..."]
      applies-to: MotorVSDInst1. The source explicitly lists the dust filter units among the
      machines with no motion sensor; the cyclone filter unit and the optical sorter are not
      conveyors.

B-25  The discharge conveyor VSD instead treats its motion sensor as the *source* of its
      running-forward confirmation — motion sensed means running — and when that sensor is bypassed
      it is not used.
      [REQ-012 — "The discharge-conveyor VSD treats its rotation sensor as the source of its
      'running forward' confirmation..."]
      applies-to: MotorVSDInst1
      NOTE: B-24 says the motion sensor is required *in addition to* the run feedback; B-25 says for
      this machine it *is* the confirmation. Both readings retained, not merged — Q-B10, blocking.
      What confirms running while the bypass is applied is stated nowhere — carried from rung A Q-07.

B-26  Each motor is inhibited from running while its local isolator feedback shows the machine is
      isolated.
      [REQ-010 — "Each motor is inhibited from running while its local isolator feedback indicates
      the machine is isolated"]
      applies-to: MotorVSDInst1, MotorVSDInst3. Whether the three filter units and the optical
      sorter count as "motors" for this behaviour is not stated — Q-B11.

B-27  The plant brings in each filter unit's remote-operational and running indications.
      [REQ-019 — "Each dust filter unit and the cyclone filter unit brings in its remote-operational,
      running, and fault feedbacks..."]
      applies-to: FilterUnitInst1, FilterUnitInst2, FilterUnitInst3

B-28  The optical sorter's ready and running indications are brought in from the machine, and its
      run command is driven to the machine.
      [REQ-017 — "...and its run command is driven to the sorter"]
      applies-to: TomraControlInst1
```

## G7 — Equipment-specific behaviours in scope

```
B-29  Once a machine has been running and confirmed running for a configurable up-to-speed time, it
      declares itself enabled — and that enable is what permits the machine feeding it to start.
      [rung A convention C-A3, derived from the source's inventory table; class requirement sets
      FU-07, VSD-08, OS-09]
      applies-to: all six in-scope instances
      NOTE: class-derived, not source-stated — Q-B1.

B-30  The three filter units use a configurable fan start-up (up-to-speed) time before they are
      treated as enabling the equipment that depends on them. The commissioning value is 10 s.
      [REQ-020 — "The dust filter units and the cyclone filter unit use a configurable fan start-up
      (up-to-speed) time before they are treated as enabling downstream equipment; the commissioning
      value is 10 s"]
      applies-to: FilterUnitInst1, FilterUnitInst2, FilterUnitInst3
      QUANTITATIVE PARAMETER: fan start-up time = 10 s (stated). It is the only numeric process
      parameter the source states for any in-scope machine.

B-31  The optical sorter exchanges process-data words with the plant in addition to its hardwired
      signals.
      [REQ-017 — notes: "The sorter also exchanges process-data words with the plant"; source
      register Q-06]
      applies-to: TomraControlInst1
      NOTE: content and scope of that exchange unresolved — carried from rung A Q-08, blocking.

B-32  The dust filter units are treated as a matched pair: both are named together as joint
      enablers of the air separator, and the source states no behaviour that distinguishes one from
      the other.
      [REQ-021 — "...when the discharge-conveyor VSD and both dust filter units are enabled..."]
      applies-to: FilterUnitInst2, FilterUnitInst3
```

## G8 — Explicitly outside PLC scope

```
B-33  Plant health, the emergency-stop function and safety interlocking are handled by the hardwired
      safety circuit and by other parts of the system. No logic for them belongs in the automatic
      control layer, which treats every machine as system-healthy.
      [REQ-022 — "This automatic-control layer treats every machine as system-healthy..."]
      applies-to: plant
      Recorded, not elaborated (hard rule 2). Internals of the safety function are not read here.
      NOTE: rung A's class reference for the optical sorter records that this class has no
      system-healthy input at all, so "asserts healthy for every machine" cannot be literally true
      for one of the six — Q-B12.

B-34  The plant master start/stop sequencer, the computation of the plant run state, the pre-start
      warning timer itself, and each machine's own internal sequence are outside the automatic
      control layer.
      [source register, Provenance §"Scope of THIS block" — "It does not contain, and this register
      does not specify: the plant master start/stop sequencer..."]
      applies-to: plant
```

---

## Open questions

### Blocking

- **Q-B1 — BLOCKING. The behaviour source is not an independent functional description, and several
  behaviours have no source at all.** The source register states in its own provenance that it was
  reverse-derived entirely from as-built logic with no functional description behind it. The method
  for this rung requires a supplied functional description and forbids writing requirements from
  inference about what a plant "probably" does. Consequences visible in this artifact: **B-19, B-20
  and B-29 have no citation in the behaviour source** — they arrive only through rung A's class
  references, which are themselves reverse-derived from FB code (rung A Q-01). Fail-to-run /
  fail-to-stop supervision, fault latching, and the up-to-speed enable that the entire start cascade
  is built on are therefore *unsourced* at plant level. *Needed:* the real functional description,
  or an explicit ruling that a reverse-derived register is accepted as the behaviour source with
  this circularity acknowledged.

- **Q-B5 — BLOCKING. The stated reason for the start interlocks cannot be reconciled with the
  interlocks themselves.** B-07 states the permissive means "the machine receiving my material is
  already enabled". Rung A could establish no material flow at all (its Q-03), and one in-scope pair
  makes the stated reason implausible: both dust filter units are gated on the **Sorter Conveyor
  VSD** being enabled, which under B-07 would mean the sorter conveyor receives the dust filters'
  material. A dust extraction unit does not discharge onto a sorter feed conveyor. Either B-07's
  rationale does not apply to extraction plant, or the link is wrong. Both readings retained; not
  resolved. *Needed:* material-flow confirmation (same answer as rung A Q-03), and confirmation of
  what gates the dust filter units.

- **Q-B8 — BLOCKING. Hand-intervention scope on two in-scope machines.** B-13 states per-machine
  behaviour; the source records that the sorter conveyor VSD and the optical sorter instead take a
  different machine's hand state, calls it a probable copy/paste defect, and asks for an owner
  ruling. Both are in scope for this run. Not resolved. *Needed:* the ruling. (Same question as
  rung A Q-06.)

- **Q-B10 — BLOCKING. Contradiction inside the source on motion-sensor use.** B-24 (REQ-011) says
  the motion sensor is required **in addition to** the run feedback before a belt counts as running.
  B-25 (REQ-012) says the discharge conveyor VSD treats the motion sensor **as** its running-forward
  confirmation, and that when bypassed it is "not used". These are two different behaviours for the
  same machine, and neither states what confirms running while the bypass is applied. Both retained,
  neither adopted. *Needed:* the actual running-confirmation behaviour for this machine, bypassed
  and not bypassed. (Extends rung A Q-07.)

### Non-blocking

- **Q-B2 — Plant run-state granularity.** B-03 records three states. The source flags that finer
  sub-states within "running" may exist and be consumed elsewhere. No in-scope behaviour depends on
  the distinction as written; confirmation requested.

- **Q-B3 — Sub-system enables and the six in-scope machines.** B-04 names a magnet enable, a drum
  enable and a general enable, and lists the machines they gate — none of the six. Recorded because
  "not gated" is an absence-of-mention, not a stated absence, and the three filter units in
  particular have an obvious candidate (an extraction/fans enable) that the source never mentions.

- **Q-B4 — Pre-start warning duration not stated.** B-05 requires the warning to complete before any
  machine starts; the source states no duration and places the timer outside this layer. No value
  invented. Confirmation requested so a downstream rung does not assume one.

- **Q-B6 — Air-separator alternative start path meaning.** B-10's second path drops the requirement
  for both dust filter units. If it is a degraded/link-out running mode, that is a plant mode this
  register never states as such — and it directly weakens what the two in-scope dust filter units
  are for. The source marks the meaning as inferred.

- **Q-B7 — Two hold conditions for the filter units, singular in the general statement.** B-11 says
  each machine holds on *the specific* neighbouring machine; B-12 adds a plant-level fans-ready hold
  for the three filter units. Both retained, not merged. Whether the two are conjunctive is rung A's
  Q-09. Recorded here because the general statement's singular phrasing is exactly what makes the
  second condition easy to lose.

- **Q-B9 — Source citation error.** The source's REQ-003 note refers the reader to "REQ-020" for the
  out-of-scope E-stop; REQ-020 is the fan start-up time and REQ-022 is the out-of-scope health and
  safety requirement. Recorded, not corrected — correcting a restricted document is not this rung's
  job, and the error affects the traceability of an out-of-scope boundary.

- **Q-B11 — Does "each motor" include the filter units and the optical sorter?** B-26 states an
  isolator inhibit for "each motor". The three filter units and the optical sorter are machines, not
  obviously motors, and the source neither includes nor excludes them. Rung A's class references
  give all four an inhibit condition, but say nothing about where it comes from. Scope of B-26 is
  therefore uncertain for four of the six in-scope instances.

- **Q-B12 — "Every machine is treated as system-healthy" vs a class with no healthy input.** B-33
  states the automatic layer asserts health for every machine; rung A's optical-sorter class
  reference records that this class has no system-healthy input at all. A contradiction between rung
  B's source and rung A's class reference — recorded for rung C to reconcile, per this rung's rule
  that an A-vs-B disagreement is a question, not a correction.
