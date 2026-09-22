# PlantAutoControl-bench-rerun2 — Plant-level behaviours (rung B)

Produced by `/gen-functional-analysis` (rung B of the structured spec pipeline A→B→C→D).
**Plant intent only** — no signals, no IO addresses, no booleans, no interface members, no block
names.

## Provenance

- **Produced:** 2026-08-05, `gen-functional-analysis`, run `PlantAutoControl-bench-rerun2`.
- **Behaviour source (REQUIRED input):** `gen/PlantAutoControl-bench/requirements.md` — the **REQ text**
  (REQ-001 … REQ-022) and its *Sequence classification* section. SHA-256
  `c0de9b797b75085b40c167ad9e8b672ae25d48b0e74ed4a4f4f9e157ad46ecba`.
- **Source kind: DERIVED.** The source's own Provenance section states it is *"Reverse-derived, NO
  independent source document … derived entirely from the as-built `PlantAutoControl` logic … There
  is no functional description, spec sheet, or other independent source behind it."* Per this rung's
  three-way stop condition, that is **proceed-with-a-blocking-question**, not stop. The circularity
  is recorded as **Q-B01 (blocking)**, and every behaviour below carries an explicit
  **citation-status** mark.
- **Rung A artifact consumed (for scoping only):**
  `gen/PlantAutoControl-bench-rerun2/equipment-topology.md` (this run). Rung A was **not** edited.
  Where this rung appears to contradict rung A, both readings are recorded and raised as a question
  for rung C — never merged.
- **Scope:** the same six instances as rung A. Plant-wide behaviours are recorded plant-wide and then
  scoped to the six; neighbouring machines are named in scope lines only where the behaviour
  genuinely lands on them.
- **Question IDs** are rung-prefixed (`Q-Bnn`).
- **Safety:** the source records the E-stop function and safety interlocking as hardwired, outside
  this PLC layer. Recorded below as explicitly out of PLC scope (B-33) and **not elaborated**
  (hard rule 2).

## Citation-status marks (read these before the behaviours)

Every behaviour carries one:

- **`[derived]`** — cited to REQ text in the source register. The citation is real, but the register
  itself was read out of the as-built code, so the behaviour is *evidence of what the code does*,
  not independent evidence of what the plant is meant to do. This is the best available status in
  this run; it is not a clean one.
- **`[derived, no-independent-citation]`** — the behaviour is **not present in the source register at
  all**. It reached this rung only through rung A's class references, which are themselves
  reverse-derived from FB code. These are the weakest items in the artifact and are listed together
  in §Citation gaps. *A behaviour in this class has passed through two reverse-derivations and no
  human statement of intent.*
- **`[source-scoped-out]`** — the source explicitly places the behaviour in another block or in the
  hardwired circuit. Recorded so nothing downstream expects plant-layer logic for it.

---

## G1 — Plant run state and automatic operation

**B-01** The plant equipment train runs under automatic control as one coordinated system, commanded
by the plant run state rather than by an individual operator run command per machine. `[derived]`
`[requirements.md REQ-001 — "is run under automatic control as one coordinated system, commanded by
the plant run state rather than by an individual operator run command per machine"]`
*applies-to:* plant; all six in-scope instances (`FilterUnitInst1/2/3`, `MotorVSDInst1`,
`MotorVSDInst3`, `TomraControlInst1`).

**B-02** While the plant is in its running state, each piece of equipment is commanded to start and
run automatically, subject to its own permissives. `[derived]`
`[requirements.md REQ-002 — "While the plant is in its running state, each piece of equipment is
commanded to start and run automatically"]`
*applies-to:* all six in-scope instances.

**B-03** The plant has a distinct controlled-shutdown state, separate from its running state and from
being stopped. `[derived]`
`[requirements.md REQ-002 notes / REQ-003 — "On a plant controlled shutdown"]`
*applies-to:* plant; all six in-scope instances.
*note:* the source states plainly that the *encoding* of these plant states is reverse-derived and
possibly incomplete (its own Q-01: are there finer sub-states this layer does not distinguish?).
Carried here as **Q-B02**.

**B-04** This layer reads the plant run state; it does not compute it. The master start/stop
sequencer that produces the run state is another block. `[source-scoped-out]`
`[requirements.md Provenance §"Scope of THIS block" — "It does not contain … the plant master
start/stop sequencer, the value/computation of the plant run-state word"]`
*applies-to:* plant.

## G2 — Start sequence and the start-permissive cascade

**B-05** No equipment starts automatically until the plant pre-start warning phase has completed;
every machine waits on that condition before its automatic run is permitted. `[derived]`
`[requirements.md REQ-005 — "No equipment starts automatically until the plant pre-start warning
phase has completed"]`
*applies-to:* all six in-scope instances.

**B-06** The pre-start warning phase itself — the siren timer and its sequence — is another block,
not this layer. `[source-scoped-out]`
`[requirements.md REQ-005 notes — "The pre-start timer/sequence itself is another block, not this
one"]`
*applies-to:* plant.

**B-07** The plant's pre-start warning is *requested automatically* when automatic start is first
requested, once per start, and the request is cleared once the pre-start output has become active.
`[derived]`
`[requirements.md REQ-015 — "the plant pre-start warning is requested automatically, once per start;
the request is cleared again once the pre-start output has become active"]`
*applies-to:* plant. **Not** any of the six in-scope instances — the source keys this to the Link
Conveyor's first auto-start, which is out of scope for this run. Recorded because it is the plant
behaviour that makes B-05 satisfiable, and because the source itself does not know whether the Link
Conveyor is the designated first machine by design or incidentally (**Q-B03**).

**B-08** A machine is permitted to start automatically only while the machine(s) receiving its
material downstream are already enabled / up to speed — receiving equipment runs before the
equipment feeding it, so material is never delivered onto a stopped machine. `[derived]`
`[requirements.md REQ-004 — "Receiving equipment must be running before the equipment feeding it is
allowed to start, so material is never delivered onto a stopped machine"]`
*applies-to:* all six in-scope instances (per-machine links in rung A).
*contradiction held open:* the source states this as a *material-handling* rule, and its own Q-02
admits the material-flow direction is inferred. Rung A observation O-1 records at least three of the
in-scope permissive links that do not read as material flow at all (a conveyor's enable permitting
two dust filter units and the Equipment Control System; a conveyor's enable permitting an
air-separator). **Both readings retained — Q-B04.**

**B-09** "Enabled" means a machine has run and been confirmed running for its own configurable
up-to-speed time; that enable is what permits the machine it serves to start. `[derived]`
`[requirements.md REQ-020 — "before they are treated as enabling downstream equipment"; REQ-004
Source — "gated by a named neighbour's up-to-speed enable"]`
*applies-to:* all six in-scope instances.
*note:* this is the single behaviour the entire start cascade rests on. In the source it appears only
inside REQ-020 (about filter fan timing) and inside REQ-004's *Source* field — never as a
requirement in its own right. Flagged as a **structural weakness of the source**, not a gap in this
rung: **Q-B05**.

**B-10** Some machines start only when their plant sub-system is separately enabled; a machine whose
sub-system enable is off does not start even when the plant is running. `[derived]`
`[requirements.md REQ-013 — "A machine whose sub-system enable is off does not start even when the
plant is running"]`
*applies-to:* of the six in scope, **none directly** — the source names the metal-collection
conveyor (magnet circuit), the drum separator (drum enable), and the ejected-material, residual and
discharge conveyors plus the feed-conveyor reverse path (general enable). Two of those — the ejected
and residual material conveyors — are start permissives for `MotorVSDInst3` and `TomraControlInst1`,
so the behaviour reaches the in-scope machines **indirectly**: a sub-system enable being off prevents
an in-scope machine from ever seeing its own permissive. Recorded for that reason.

**B-11** The optical sorter's readiness gates the conveyor that feeds it: the sorter-feed conveyor is
permitted to start only while the sorter is ready and not faulted. `[derived]`
`[requirements.md REQ-017 — "The sorter-feed conveyor (VSD) is only permitted to start while the
optical sorter is ready and not faulted"]`
*applies-to:* `MotorVSDInst3` (gated), `TomraControlInst1` (gating).
*note:* this is the one start permissive in the plant expressed as *ready-and-not-faulted* rather
than as the standard *enabled* relation (rung A Q-A15). Both readings retained.

## G3 — Controlled shutdown

**B-12** On a plant controlled shutdown, equipment does not all stop at once. Each machine keeps
running until the specific neighbouring machine in its shutdown chain has signalled that it has
completed its own shutdown, and only then stops. `[derived]`
`[requirements.md REQ-003 — "Each machine keeps running until the specific neighbouring machine in
its shutdown chain has signalled that it has completed its own shutdown, and only then stops"]`
*applies-to:* all six in-scope instances.

**B-13** The purpose of the staged shutdown is that material already on the line is cleared in order
rather than stranded. `[derived]`
`[requirements.md REQ-003 — "so material already on the line is cleared in order rather than
stranded"]`
*applies-to:* plant.
*note:* this is a stated *purpose*, and it is the only place the source explains why the cascade
exists. It is in tension with B-08's material-flow reading for the same reason (Q-B04): a dust filter
unit held running by an air-separator's shutdown is not obviously "clearing material off a line".

**B-14** A machine declares its own shutdown complete once a configurable shutdown time has elapsed
and it has stopped running. `[derived, no-independent-citation]`
*applies-to:* all six in-scope instances. Reached only via rung A (class requirement sets
FU-08 / VSD-09 / OS-10). The source register describes the *cascade* (B-12) but never states what
"shutdown complete" means or how a machine decides it.

**B-15** Two of the three equipment classes additionally declare shutdown complete *immediately* when
faulted (and the filter class also when under hand control and not being called to run) — so a
faulted machine releases the machine waiting on it rather than stalling the cascade.
`[derived, no-independent-citation]`
*applies-to:* `FilterUnitInst1/2/3`, `MotorVSDInst1`, `MotorVSDInst3`. Reached only via rung A
(FU-09 / VSD-10).

**B-16** The optical sorter is the exception: it has **no** fault escape on its shutdown-complete, so
a faulted sorter does not release the machines waiting on it. `[derived, no-independent-citation]`
*applies-to:* `TomraControlInst1`; effect lands on the ejected and residual material conveyors
(out of scope). Reached only via rung A (optical-sorter class deltas, observation O-2). The class
reference states plainly that whether this is intended is not derivable. **Q-B06.**

**B-17** Filter units are additionally held through the shutdown by a plant-level condition
signalling that the filter fans are ready to shut down, as well as by their named neighbour's
shutdown-complete. `[derived]`
`[requirements.md REQ-013 notes — "filter units on PlantControl.FansShutdownReady"; equipment
inventory rows 3, 4, 19 "Holds through shutdown until" column]`
*applies-to:* `FilterUnitInst1`, `FilterUnitInst2`, `FilterUnitInst3`.
*unresolved:* **how the two hold conditions combine (both, or either) is not stated anywhere in the
source** — rung A Q-A04, restated here as **Q-B07 (blocking)** because the two readings are different
plant behaviours: under "either", one condition releases a fan early; under "both", a stuck condition
runs a fan through the entire shutdown.

**B-18** A controlled shutdown is a distinct plant function from an emergency stop; the E-stop is not
this behaviour. `[derived]`
`[requirements.md REQ-003 notes — "This is distinct from an E-stop (out of scope, REQ-020)"]`
*applies-to:* plant.
*note:* the source's own cross-reference here points at REQ-020, which is the *fan start-up time*
requirement, not the E-stop one (that is REQ-022). Recorded as a source defect, **Q-B08**, rather
than silently corrected.

## G4 — Modes: automatic, hand, and operator intervention

**B-19** While a machine is under hand / manual intervention, the automatic controlled-shutdown
request for that machine is suppressed — the operator's hand control is not overridden by the plant
shutdown cascade. `[derived]`
`[requirements.md REQ-006 — "the operator's hand control is not overridden by the plant shutdown
cascade"]`
*applies-to:* all six in-scope instances.

**B-20** A machine taken to hand runs on the operator's hand start command instead of the plant's
automatic start command. `[derived, no-independent-citation]`
*applies-to:* all six in-scope instances. Reached only via rung A (FU-01 / VSD-01 / OS-01). The
source register mentions hand *intervention* (B-19) but never states what running in hand means.

**B-21** **Contradiction, held open.** The source states the hand-suppression rule per machine
(B-19), and separately records that two machines — the sorter conveyor VSD and the optical sorter —
gate their *own* automatic shutdown on the *other* machine's hand-intervention state. The source
calls this a possible as-built copy/paste quirk and asks for an owner ruling.
`[derived]` `[requirements.md REQ-006 notes + Q-07 — "Two networks reference a different machine's
hand flag than their own … Needs an owner ruling: deliberate grouping, or a defect to correct?"]`
*applies-to:* `MotorVSDInst3`, `TomraControlInst1`.
**Neither reading adopted — Q-B09 (blocking).** Rung A carries the same item as a delta on both
machines; the two artifacts agree that it is unresolved, which is not the same as resolving it.

**B-22** The equipment reports its own mode (auto / hand / setting) back to the plant for at least one
machine class. `[derived]`
`[requirements.md REQ-018 — "its fault, mode (auto/hand/setting), ready, running … signals are
brought in"]`
*applies-to:* Equipment Control System (out of scope). Recorded because it establishes that mode
reporting is a plant behaviour, which bears on whether the in-scope machines report mode too — the
source does not say. **Q-B10** (non-blocking).

**B-23** Filter units additionally require the unit to be in remote (not local) operating mode before
they start. `[derived, no-independent-citation]`
*applies-to:* `FilterUnitInst1`, `FilterUnitInst2`, `FilterUnitInst3`. Reached only via rung A
(FU-05). The source register does bring a remote-operational report in from each filter unit
(REQ-019) but never states it as a start permissive — the two may be the same thing or two different
things (rung A Q-A08).

## G5 — Faults, reset, and supervision

**B-24** One operator system-reset command clears faults across all equipment at once; every
machine's fault reset is driven from the same plant-wide reset. `[derived]`
`[requirements.md REQ-007 — "One operator system-reset command clears faults across all equipment at
once"]`
*applies-to:* all six in-scope instances.

**B-25** The filter units additionally echo that reset to the unit itself as a physical reset command
to the equipment. `[derived]`
`[requirements.md REQ-019 — "drives a fault-reset output back to the unit"; REQ-007 notes — "Filter
units and the ECS additionally echo this reset to a physical reset output"]`
*applies-to:* `FilterUnitInst1`, `FilterUnitInst2`, `FilterUnitInst3`.

**B-26** The optical sorter's reset is also passed to the sorter itself over its data link.
`[derived, no-independent-citation]`
*applies-to:* `TomraControlInst1`. Reached only via rung A (OS-14).

**B-27** A machine that is commanded to run but does not report running within a configurable fault
time raises a latched fail-to-run fault; a machine that reports running while not commanded for that
same time raises a latched fail-to-stop fault. `[derived, no-independent-citation]`
*applies-to:* all six in-scope instances. Reached only via rung A (FU-10/11, VSD-12/13, OS-11/12).
**The source register never mentions fault detection at all** — only fault *reset* (B-24). This is
the largest single citation gap in the run: the plant's entire equipment-fault supervision has no
statement of intent behind it.

**B-28** Run-confirmation supervision on the VSD class is not armed until a configurable start-up
allowance has elapsed after the drive is commanded and its demand has settled, and is re-armed on a
direction change, so a ramp or a reversal does not itself raise a fault.
`[derived, no-independent-citation]`
*applies-to:* `MotorVSDInst1`, `MotorVSDInst3`. Reached only via rung A (VSD-11).

**B-29** A fault reported by the equipment itself raises a latched fault. `[derived]`
`[requirements.md REQ-019 — "brings in its remote-operational, running, and fault feedbacks"]`
*applies-to:* `FilterUnitInst2`, `FilterUnitInst3` (direct fault report); `FilterUnitInst1` — see
B-30; `MotorVSDInst1`, `MotorVSDInst3` (drive error, `[derived, no-independent-citation]`, via
VSD-14); `TomraControlInst1` (its own reported machine-fault set, `[derived,
no-independent-citation]`, via OS-13).

**B-30** The cyclone filter unit's fault is taken from the **absence of its system-OK report** rather
than from a fault report. `[derived]`
`[requirements.md REQ-019 — "The cyclone filter unit's fault is taken from the inverse of its
system-OK signal"]`
*applies-to:* `FilterUnitInst1`.

**B-31** The optical sorter's data link is supervised by a liveness indication; if liveness is lost
for a fixed watchdog period a communications-lost condition is latched until reset, and that loss is
itself a machine fault. `[derived, no-independent-citation]`
*applies-to:* `TomraControlInst1`. Reached only via rung A (OS-07, OS-13). The source register names
a hardwired communications-fault signal (REQ-017) but says nothing about link liveness supervision.
**The watchdog period is a stated-as-fixed but unstated value — Q-B11; no number is invented here.**

**B-32** While the sorter's data link is healthy the sorter counts as running only when both the link
and the hardwired running signal agree; while the link is dead the hardwired signal alone decides.
The same fallback governs its communications-fault indication. `[derived, no-independent-citation]`
*applies-to:* `TomraControlInst1`. Reached only via rung A (OS-08).

**B-33** Plant health, the E-stop function, and safety interlocking are handled by the hardwired
safety circuit and other blocks — **no PLC logic for them belongs in this layer.**
`[source-scoped-out]`
`[requirements.md REQ-022 — "Plant health, the E-stop function, and safety interlocking are handled
by the hardwired safety circuit and other blocks"]`
*applies-to:* plant. Internals deliberately not elaborated (hard rule 2).

**B-34** **Contradiction, held open.** The source states that this layer *treats every machine as
system-healthy — it asserts the healthy input true for each machine* (REQ-022). Rung A records, from
two of the three class references, that a machine **does not start unless it is reported
system-healthy** (FU-04, VSD-04) — a start permissive. If the layer asserts the input true
unconditionally, that permissive can never inhibit anything: it is present in the equipment but
permanently satisfied. `[derived]`
`[requirements.md REQ-022 — "it asserts the healthy input true for each machine"]`
*applies-to:* `FilterUnitInst1/2/3`, `MotorVSDInst1`, `MotorVSDInst3` (the optical-sorter class has
no health permissive at all — a third, different treatment across three classes).
**Neither reading adopted — Q-B12 (blocking).** This is a permissive that a specification asserts and
a plant behaviour that defeats it; the source does not acknowledge the tension.

## G6 — Field feedback and running confirmation

**B-35** Each machine's running status is confirmed from its field running feedback. `[derived]`
`[requirements.md REQ-009 — "Each machine's running status is confirmed from its field running
feedback signal"]`
*applies-to:* all six in-scope instances.

**B-36** Belt conveyors additionally require a motion/rotation sensor to confirm the belt is actually
turning, in addition to the run feedback, before the belt counts as running. `[derived]`
`[requirements.md REQ-011 — "Belt conveyors require a motion/rotation sensor to confirm the belt is
actually turning, in addition to the run feedback, before the belt counts as running"]`
*applies-to:* `MotorVSDInst1`. The source's list of motion-confirmed machines does **not** include
`MotorVSDInst3`, and explicitly excludes the dust filter units (rung A Q-A14 asks whether the sorter
conveyor's absence from the list is real).

**B-37** Each conveyor's motion-sensor check can be individually bypassed by the operator — for a
failed sensor or during commissioning — so the conveyor can still be run on its run feedback alone.
`[derived]`
`[requirements.md REQ-011 — "Each such conveyor's rotation-sensor check can be individually bypassed
by the operator (for a failed sensor or commissioning) so the conveyor can still be run on its run
feedback alone"]`
*applies-to:* `MotorVSDInst1`.

**B-38** **Contradiction, held open.** For the discharge conveyor VSD specifically, the source says
the motion sensor *is* the source of the running-forward confirmation (motion sensed = running),
"unless that sensor is bypassed, in which case it is not used". B-37 says a bypassed conveyor runs on
its run feedback alone; B-38's wording says the sensor is simply not used, without naming a
replacement. `[derived]`
`[requirements.md REQ-012 — "treats its rotation sensor as the source of its 'running forward'
confirmation (motion sensed = running), unless that sensor is bypassed, in which case it is not
used"]`
*applies-to:* `MotorVSDInst1`. **Neither reading adopted — Q-B13 (blocking)**, mirroring rung A
Q-A11. A machine with no running confirmation while bypassed and a machine falling back to its drive
feedback behave differently under a real belt-slip.

**B-39** Each motor is inhibited from running while its local isolator feedback indicates the machine
is isolated. `[derived]`
`[requirements.md REQ-010 — "Each motor is inhibited from running while its local isolator feedback
indicates the machine is isolated"]`
*applies-to:* `FilterUnitInst2`, `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1` and, per the
class requirement sets, all six. *The source's own list of isolator-bearing machines is given as
network numbers only, which this rung cannot resolve to instances; the class references make the
inhibit universal across all three classes, so it is scoped to all six here.* Divergence recorded as
**Q-B14** (non-blocking) — rung C can settle it against the IO table.

**B-40** Each machine's resulting run command drives the corresponding physical motor/starter start
output; some machines are commanded through their drive/FB interface instead of a discrete start
bit. `[derived]`
`[requirements.md REQ-008 — "Each machine's resulting run command (from its control FB) drives the
corresponding physical motor/starter start output"; notes — "The incline feed VSD, air-separator
VSD, sorter-conveyor VSD and shredder are commanded through their FB/VSD interface rather than a
discrete start bit"]`
*applies-to:* `FilterUnitInst1/2/3` (discrete start), `TomraControlInst1` (discrete run **and** data
link, B-41), `MotorVSDInst3` (drive interface, no discrete start bit), `MotorVSDInst1` (**not named
in either list — Q-B15**, non-blocking).

**B-41** The optical sorter is commanded both over its data link and by a hardwired run signal, both
driven together from the same run decision, and its condition is read both over the link and from
hardwired ready / running / communications-fault signals. `[derived]`
`[requirements.md REQ-017 — "The optical sorter's hardwired ready, running, and communications-fault
signals are brought into its control, and its run command is driven to the sorter"]` (hardwired half)
+ rung A OS-05/OS-06 (data-link half, `[derived, no-independent-citation]`).
*applies-to:* `TomraControlInst1`.

**B-42** The filter units bring in a remote-operational report, a running report and a fault report
from the field. The cyclone unit's reports differ: it reports running and stopped separately, and
reports system-OK rather than a fault. `[derived]`
`[requirements.md REQ-019 + its notes]`
*applies-to:* `FilterUnitInst2`, `FilterUnitInst3` (first sentence); `FilterUnitInst1` (second).

## G7 — Equipment-specific process behaviours (in-scope machines)

**B-43** The dust filter units and the cyclone filter unit use a configurable fan start-up
(up-to-speed) time before they are treated as enabling downstream equipment; **the commissioning
value is 10 s.** `[derived]`
`[requirements.md REQ-020 — "use a configurable fan start-up (up-to-speed) time before they are
treated as enabling downstream equipment; the commissioning value is 10 s"]`
*applies-to:* `FilterUnitInst1`, `FilterUnitInst2`, `FilterUnitInst3`.
*note:* this is the **only quantitative process parameter stated anywhere in the source** for the six
in-scope machines. Every other configurable time and setpoint they need is unstated — see
§Quantitative parameters.

**B-44** Both dust filter units, together with the discharge conveyor VSD, form the normal start
permissive for the air-separator VSD; alternatively the air-separator may start on the discharge
conveyor VSD plus a third-party link-out signal, bypassing the dust-filter enables. `[derived]`
`[requirements.md REQ-021 — "either when the discharge-conveyor VSD and both dust filter units are
enabled, or (alternatively) when the discharge-conveyor VSD is enabled and a third-party link-out
signal is present"]`
*applies-to:* `FilterUnitInst2`, `FilterUnitInst3`, `MotorVSDInst1` (as gating machines); the
air-separator VSD itself is out of scope.
*note:* the source does not know what the alternative path means in process terms (its Q-08 — a
degraded / link-out running mode?). Carried as **Q-B16**. It matters to the in-scope machines because
it is the one path on which a dust filter unit's enable is **not** required.

**B-45** The VSD-class machines run to a speed setpoint — an automatic setpoint in auto, an operator
setpoint in hand, floored at a minimum running speed and delivered to the drive as a demand scaled
against the machine's rated maximum speed. `[derived, no-independent-citation]`
*applies-to:* `MotorVSDInst1`, `MotorVSDInst3`. Reached only via rung A (VSD-06). **The source
register never mentions speed at all** — no setpoint, no source for it, no minimum, no rated maximum.
For two variable-speed drives in a six-machine scope, that is a substantive gap: **Q-B17
(blocking)**, mirroring rung A Q-A10.

**B-46** The VSD class can be commanded to run in reverse as well as forward, and confirms running
from either a forward or a reverse running feedback. `[derived, no-independent-citation]`
*applies-to:* `MotorVSDInst1`, `MotorVSDInst3` — **as a class capability whose use by these two
instances is not established** (rung A Q-A12). The source assigns reversal in this plant to one
machine only (the shredder feed conveyor, out of scope, REQ-016), and that machine is of a different
class.

**B-47** The optical sorter withdraws its enable **immediately** when it stops running, rather than
letting the enable fall with its up-to-speed timer as the sibling classes do.
`[derived, no-independent-citation]`
*applies-to:* `TomraControlInst1`; effect lands on `MotorVSDInst3`, which is gated by the sorter.
Reached only via rung A (OS-09).

**B-48** The optical sorter exchanges process-data words with the plant in addition to its hardwired
signals, and the byte order of that exchange is selectable at plant level. `[derived]`
`[requirements.md REQ-017 notes — "The sorter also exchanges process-data words with the plant";
equipment inventory row 11 "Extra gate: comms words"]` + rung A OS-19.
*applies-to:* `TomraControlInst1`.
**Scope unresolved:** the source states outright that whether reconstructing the word interface is in
scope, and what the words carry, is unclear. **Q-B18 (blocking)**, mirroring rung A Q-A16.

**B-49** The sorting program to run, and the sorter's belt on/off delays, are operator-settable.
`[derived, no-independent-citation]`
*applies-to:* `TomraControlInst1`. Reached only via rung A (OS-18) — where the class reference
records that this is an *interface promise rather than an implemented behaviour* in the as-built.
Recorded here as intent, with that caveat attached: **Q-B19** (non-blocking) asks whether the intent
is real.

## G8 — Operator interactions (HMI)

**B-50** The operator has one plant-wide fault reset (B-24), per-conveyor motion-sensor bypasses
(B-37), and per-machine hand control (B-19/B-20). `[derived]`
`[requirements.md REQ-007, REQ-011, REQ-006]`
*applies-to:* all six in-scope instances (bypasses: `MotorVSDInst1` only).

**B-51** Each machine publishes a status indication for the operator (faulted / stopped / commanded /
running / enabled) and an alarm indication covering at least fail-to-run, fail-to-stop and its own
equipment fault. `[derived, no-independent-citation]`
*applies-to:* all six in-scope instances. Reached only via rung A (FU-16/17, VSD-19/20, OS-16/17).
The source register contains **no HMI indication requirement whatsoever** — its only HMI-class items
are the reset and the bypasses.

**B-52** Running hours are totalised per machine. `[derived, no-independent-citation]`
*applies-to:* all six in-scope instances. Reached only via rung A (FU-15, VSD-18, OS-15).

## G9 — Explicitly outside this layer

**B-53** Each equipment's own internal sequence lives inside that equipment's control FB, not in the
plant automatic-control layer. This layer maps field feedback in, computes the automatic
start/stop/interlock permissives from the plant run state, drives the physical run output, and calls
the equipment's control FB. `[source-scoped-out]`
`[requirements.md Provenance §"Scope of THIS block" — "for each piece of equipment it maps field
feedback in, computes the automatic start/stop/interlock permissives from the plant run state,
drives the physical run output, and calls the equipment's own control FB"]`
*applies-to:* all six in-scope instances.
**Consequence, recorded explicitly:** most of the `[derived, no-independent-citation]` behaviours
above (B-14, B-15, B-20, B-27, B-28, B-31, B-32, B-45, B-47, B-51, B-52) are behaviours the source
places *inside* the equipment FBs. They are still plant behaviours and still in this artifact — but
the layer that implements them is not the layer the source register describes. That is *why* the
source is silent on them, and it is a rung-C scoping question (**Q-B20**), not a licence to drop
them.

**B-54** The plant master start/stop sequencer, the plant run-state computation, the pre-start siren
timer, the E-stop / safety circuit, and the shredder's internal overcurrent/reversal logic are all
other blocks or the hardwired circuit. `[source-scoped-out]`
`[requirements.md Provenance §"Scope of THIS block"]`
*applies-to:* plant.

---

## Quantitative parameters

Every number the six in-scope machines need, and whether the source states it. **No number here is
invented; an unstated number is a question, per this rung's contract.**

| Parameter | Applies to | Stated value | Status |
|---|---|---|---|
| Fan start-up (up-to-speed) time | all three filter units | **10 s** (commissioning value) | **stated** — REQ-020 |
| Up-to-speed (enable) time | `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1` | — | **Q-B21** |
| Shutdown time (before shutdown-complete) | all six | — | **Q-B21** |
| Fail-to-run / fail-to-stop fault time | all six | — | **Q-B21** |
| VSD start-up (ramp) allowance before supervision arms | `MotorVSDInst1`, `MotorVSDInst3` | — | **Q-B21** |
| Minimum running speed | `MotorVSDInst1`, `MotorVSDInst3` | — | **Q-B17** |
| Rated maximum speed | `MotorVSDInst1`, `MotorVSDInst3` | — | **Q-B17** |
| Automatic speed setpoint | `MotorVSDInst1`, `MotorVSDInst3` | — | **Q-B17** |
| Data-link liveness watchdog period | `TomraControlInst1` | — (source says "fixed", gives no figure) | **Q-B11** |
| Sorter belt on/off delays | `TomraControlInst1` | — | **Q-B19** |

## Citation gaps (behaviours with no independent citation)

Listed together because their combined weight is the finding, not any one of them:

**B-14, B-15, B-16, B-20, B-23, B-26, B-27, B-28, B-31, B-32, B-45, B-46, B-47, B-49, B-51, B-52**
— **16 of 54 behaviours (30%)** are not present in the behaviour source at all. They reached this
rung only through rung A's class references, which are themselves reverse-derived from FB code and
say so in their own headers.

Among them: **all equipment fault detection** (B-27), **all operator status and alarm indication**
(B-51), **the meaning of shutdown-complete** (B-14) on which the entire cascade in B-12 depends, and
**the whole speed-control behaviour of two variable-speed drives** (B-45). These are not marginal
behaviours.

## Open questions

None resolved here. Blocking questions must be answered before rung C binds signals to the affected
behaviour.

### Blocking

- **Q-B01 — [BLOCKING] The behaviour source is reverse-derived from the as-built, with no independent
  functional description behind it.** The source says so itself. Every behaviour above is therefore
  evidence of what the code does, not of what the plant is meant to do; a behaviour the as-built gets
  *wrong* is indistinguishable here from a behaviour it gets right, and a behaviour the as-built
  omits is invisible. Compounding it, 30% of the behaviours reached this rung through a *second*
  reverse-derivation (the class references, rung A Q-A01). Needed: the supplied functional
  description, or an explicit owner acceptance that this run is validating pipeline mechanics rather
  than plant intent.
- **Q-B07 — [BLOCKING] How do the filter units' two shutdown-hold conditions combine?** (B-17.) Both
  retained, neither chosen — "hold until both" and "hold until either" are different plant
  behaviours. Mirrors rung A Q-A04.
- **Q-B09 — [BLOCKING] Cross-machine hand intervention on the sorter conveyor VSD and the optical
  sorter.** (B-21.) The source states the per-machine rule *and* records the cross-machine behaviour,
  and asks for a ruling. Both readings recorded, neither adopted. Mirrors rung A Q-A13.
- **Q-B12 — [BLOCKING] System-healthy is asserted true by the layer while being a start permissive in
  the equipment.** (B-34.) A permissive that is unconditionally satisfied is not a permissive.
  Needed: is the health permissive intended to be live (fed from something real), or intended to be
  defeated because health is hardwired?
- **Q-B13 — [BLOCKING] What confirms the discharge conveyor VSD is running while its motion-sensor
  bypass is active?** (B-38.) Two readings in the source, materially different under belt slip.
  Mirrors rung A Q-A11.
- **Q-B17 — [BLOCKING] The entire speed behaviour of both in-scope VSD conveyors is unstated.**
  (B-45.) No setpoint, no source, no minimum, no rated maximum. Mirrors rung A Q-A10.
- **Q-B18 — [BLOCKING] Is the optical sorter's process-data word interface in scope, and what do the
  words carry?** (B-48.) The source declares this unclear itself. Mirrors rung A Q-A16.

### Non-blocking

- **Q-B02 — Plant run-state encoding.** Are there finer sub-states within the running state that this
  layer does not distinguish but another block does? (B-03.) Source's own Q-01.
- **Q-B03 — Which machine triggers the automatic pre-start request, and why?** (B-07.) The source
  keys it to the link conveyor and does not know whether that is by design. Source's own Q-04.
- **Q-B04 — Is the start-permissive cascade really a material-flow relation?** (B-08 vs rung A
  observation O-1.) Both readings retained. Source's own Q-02.
- **Q-B05 — "Enabled" is never stated as a requirement in the source**, only inside a timing
  requirement and a source-citation field, despite the whole start cascade resting on it. (B-09.)
  Recorded as a source-structure defect.
- **Q-B06 — Is the optical sorter's missing fault-escape on shutdown-complete intended?** (B-16.)
- **Q-B08 — Source cross-reference defect:** REQ-003's note points at REQ-020 (fan start time) when
  it means the E-stop requirement (REQ-022). (B-18.) Not silently corrected.
- **Q-B10 — Do the in-scope machines report their operating mode back to the plant?** The source
  establishes mode reporting for one out-of-scope machine only. (B-22.)
- **Q-B11 — What is the sorter data-link watchdog period?** Stated as fixed, value not given. (B-31.)
- **Q-B14 — Which machines actually have an isolator feedback?** The source lists them by network
  number, which this rung cannot resolve; the class references make it universal. (B-39.)
- **Q-B15 — Is the discharge conveyor VSD commanded by a discrete start bit or through its drive
  interface?** The source's two lists in REQ-008 name neither for this machine. (B-40.)
- **Q-B16 — What does the air-separator's alternative start path mean in process terms?** It is the
  one path where a dust filter unit's enable is not required. (B-44.) Source's own Q-08.
- **Q-B19 — Are the sorter's operator-settable program and belt delays real intent?** The class
  reference records them as an interface promise, not an implemented behaviour. (B-49.)
- **Q-B20 — Layer scoping:** most no-independent-citation behaviours are implemented inside the
  equipment FBs rather than in the layer the source describes. Rung C must decide, per behaviour,
  which layer it lands in. (B-53.)
- **Q-B21 — Four configurable times are needed by the in-scope machines and none is stated**
  (enable/up-to-speed for the non-filter machines, shutdown, fault, VSD ramp allowance). Only the
  filter fan time (10 s) has a value.

## Coverage statement

- Behaviours: **54**, in **9 groups**.
- Cited to the behaviour source: **38**. No independent citation (via rung A only): **16 (30%)**.
- Explicitly scoped out of this layer by the source: **6** (B-04, B-06, B-33, B-53, B-54, and the
  scoped-out half of B-40).
- Contradictions recorded and held open: **5** (B-08/O-1, B-21, B-34, B-38, B-18's cross-reference)
  — none resolved, none merged.
- Quantitative parameters needed by the in-scope machines: **10**; stated in the source: **1**.
- Blocking questions: **7**. Non-blocking: **14**.

**Rung status: BLOCKED.** The artifact is complete as a rung-B product, but proceeds under a
recorded circularity (Q-B01) and hands seven blocking questions to rung C. Rung C may bind signals
for behaviours no blocking question governs; it must not bind for those that one does.
