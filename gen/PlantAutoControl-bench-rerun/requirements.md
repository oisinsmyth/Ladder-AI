# PlantAutoControl-bench-rerun — Requirements Register (derived view, rung C)

**Derived, not authored.** This register is a mechanical view over the `C-nn` / `P-nn` sets in
`gen/PlantAutoControl-bench-rerun/equipment-specs/`. It exists because `review-functional` STOPs without
a register: a project specified through the A→B→C→D rungs cannot be functionally reviewed at all
unless this view exists. The specs are the source of truth; if the two ever disagree, the specs win
and this file is stale.

## Provenance

- **Produced:** 2026-08-04 by `/gen-equipment-spec` (rung C), from
  `gen/PlantAutoControl-bench-rerun/equipment-topology.md` (rung A),
  `gen/PlantAutoControl-bench-rerun/plant-behaviours.md` (rung B), the IO table in
  `ir/PlantAutoControl-bench/` and the three class references in `references/`.
- **Scope:** six instances. `FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1`,
  `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1`.
- **IDs:** `REQ-nnn`, stable forever, never renumbered or reused. Each carries its originating
  instance and local spec id.
- **Tag status:** every tag named in the specs was checked with
  `converter tagstatus --project ir/PlantAutoControl-bench`. **0 proposed, all exists.** Nothing in this
  register is coded against an invented tag. Where a requirement *needs* a signal that does not
  exist, no tag was proposed at all — the requirement is carried with an explicit
  `NONE IN IO TABLE` and a blocking question (hard rule 3).
- **Alarms:** out of scope at this rung by the skill's own calibration. Each class's alarm
  requirement is instantiated and marked out-of-scope rather than dropped.

## Completeness by set-difference

| | count |
|---|---|
| distinct `C-nn` / `P-nn` ids across `equipment-specs/` | **166** |
| REQ entries in this register | **166** |
| set-difference (specs minus register) | **0** |
| set-difference (register minus specs) | **0** |

Per-instance: FilterUnitInst2 17 C + 8 P = 25 · FilterUnitInst3 17 + 8 = 25 ·
FilterUnitInst1 17 + 8 = 25 · MotorVSDInst1 20 + 11 = 31 · MotorVSDInst3 20 + 12 = 32 ·
TomraControlInst1 19 + 9 = 28. Total 166.

## Register

Class key: `control` sequencing/interlocks/drives · `mode` auto/hand/enable · `HMI` operator ·
`timing` named delay as the ask · `alarm` (out of scope here) · `comms` data-link behaviour.
Provenance key: `[ref]` class reference · `[A]` topology · `[B-nn]` behaviour · `[io]` IO table ·
`[set]` as-built setting.

### FilterUnitInst2 — Dust Filter Unit 1 (FilterUnitSystem)

| REQ | id | Requirement | Class | Provenance | Q |
|---|---|---|---|---|---|
| REQ-001 | C1 | runs on plant auto start, or operator hand start while in hand | mode | ref | Q-C09 |
| REQ-002 | C2 | does not start while inhibited/isolated | control | ref | **Q-C02** |
| REQ-003 | C3 | does not start while faulted | control | ref+io | |
| REQ-004 | C4 | does not start unless system-healthy | control | ref | **Q-C03** |
| REQ-005 | C5 | does not start unless the unit is in remote mode | mode | ref+io | **Q-C01** |
| REQ-006 | C6 | does not start until pre-start warning complete (`PlantControl.PreStartComplete`) | control | ref+B-05+io | |
| REQ-007 | C7 | declares itself enabled after the fan up-to-speed time (`ProcessTimings.NormalFanStartTime`, 10 s) | control | ref+B-30+io | |
| REQ-008 | C8 | shutdown complete after the shutdown time with running feedback cleared | control | ref | **Q-C01** |
| REQ-009 | C9 | shutdown complete immediately if faulted or hand-stopped | control | ref | |
| REQ-010 | C10 | fail-to-run fault, latched | control | ref+set | |
| REQ-011 | C11 | fail-to-stop fault, latched | control | ref+set | |
| REQ-012 | C12 | unit fault from `DiscreteInputs.FilterUnit1Flt` | control | ref+B-21+io | |
| REQ-013 | C13 | fault reset from `HMIControlSignals.SystemReset` | HMI | ref+B-15+io | |
| REQ-014 | C14 | running from the unit's own running feedback; no motion sensor | control | ref+io | **Q-C01** |
| REQ-015 | C15 | running hours totalised | HMI | ref | |
| REQ-016 | C16 | operator status indication published | HMI | ref | |
| REQ-017 | C17 | alarm indication published | alarm | ref | out of scope this rung |
| REQ-018 | P1 | start only while MotorVSDInst3 is enabled | control | A | B Q-B5 |
| REQ-019 | P2 | shutdown holds until `PlantControl.FansShutdownReady` | control | A+B-12+io | A Q-09 |
| REQ-020 | P3 | shutdown holds until AirStarInst1 reports shutdown complete | control | A+B-11 | A Q-09 |
| REQ-021 | P4 | auto-start commanded while plant run state is running (`PlantControl.Status`) | control | B-02+io | B Q-B2 |
| REQ-022 | P5 | auto shutdown suppressed while this machine is in hand | mode | B-13 | |
| REQ-023 | P6 | run command drives `DiscreteOutputs.FilterUnit1Start` | control | B-23+io | |
| REQ-024 | P7 | reset echoed on `DiscreteOutputs.FilterUnit1Reset` | HMI | B-16+io | |
| REQ-025 | P8 | REVERSE: its enable is a start permissive of AirStarInst1 | control | A+B-10 | |

### FilterUnitInst3 — Dust Filter Unit 2 (FilterUnitSystem)

| REQ | id | Requirement | Class | Provenance | Q |
|---|---|---|---|---|---|
| REQ-026 | C1 | runs on plant auto start, or operator hand start while in hand | mode | ref | Q-C09 |
| REQ-027 | C2 | does not start while inhibited/isolated | control | ref | **Q-C02** |
| REQ-028 | C3 | does not start while faulted | control | ref+io | |
| REQ-029 | C4 | does not start unless system-healthy | control | ref | **Q-C03** |
| REQ-030 | C5 | does not start unless the unit is in remote mode | mode | ref+io | **Q-C01** |
| REQ-031 | C6 | does not start until pre-start warning complete | control | ref+B-05+io | |
| REQ-032 | C7 | declares itself enabled after the fan up-to-speed time (10 s) | control | ref+B-30+io | |
| REQ-033 | C8 | shutdown complete after the shutdown time with running feedback cleared | control | ref | **Q-C01** |
| REQ-034 | C9 | shutdown complete immediately if faulted or hand-stopped | control | ref | |
| REQ-035 | C10 | fail-to-run fault, latched | control | ref+set | |
| REQ-036 | C11 | fail-to-stop fault, latched | control | ref+set | |
| REQ-037 | C12 | unit fault from `DiscreteInputs.FilterUnit2Flt` | control | ref+B-21+io | |
| REQ-038 | C13 | fault reset from `HMIControlSignals.SystemReset` | HMI | ref+B-15+io | |
| REQ-039 | C14 | running from the unit's own running feedback; no motion sensor | control | ref+io | **Q-C01** |
| REQ-040 | C15 | running hours totalised | HMI | ref | |
| REQ-041 | C16 | operator status indication published | HMI | ref | |
| REQ-042 | C17 | alarm indication published | alarm | ref | out of scope this rung |
| REQ-043 | P1 | start only while MotorVSDInst3 is enabled | control | A | B Q-B5 |
| REQ-044 | P2 | shutdown holds until `PlantControl.FansShutdownReady` | control | A+B-12+io | A Q-09 |
| REQ-045 | P3 | shutdown holds until AirStarInst1 reports shutdown complete | control | A+B-11 | A Q-09 |
| REQ-046 | P4 | auto-start commanded while plant run state is running | control | B-02+io | |
| REQ-047 | P5 | auto shutdown suppressed while this machine is in hand | mode | B-13 | |
| REQ-048 | P6 | run command drives `DiscreteOutputs.FilterUnit2Start` | control | B-23+io | |
| REQ-049 | P7 | reset echoed on `DiscreteOutputs.FilterUnit2Reset` | HMI | B-16+io | |
| REQ-050 | P8 | REVERSE: its enable is a start permissive of AirStarInst1 | control | A+B-10 | |

### FilterUnitInst1 — Cyclone Filter Unit (FilterUnitSystem)

| REQ | id | Requirement | Class | Provenance | Q |
|---|---|---|---|---|---|
| REQ-051 | C1 | runs on plant auto start, or operator hand start while in hand | mode | ref | Q-C09 |
| REQ-052 | C2 | does not start while inhibited/isolated | control | ref | **Q-C02** |
| REQ-053 | C3 | does not start while faulted | control | ref+io | |
| REQ-054 | C4 | does not start unless system-healthy | control | ref | **Q-C03** |
| REQ-055 | C5 | does not start unless in remote mode (`DiscreteInputs.CycloneDustRemOp`) | mode | ref+B-27+io | Q-C07 |
| REQ-056 | C6 | does not start until pre-start warning complete | control | ref+B-05+io | |
| REQ-057 | C7 | declares itself enabled after the fan up-to-speed time (10 s) | control | ref+B-30+io | A Q-13 |
| REQ-058 | C8 | shutdown complete after the shutdown time with running feedback cleared | control | ref | **Q-C08** |
| REQ-059 | C9 | shutdown complete immediately if faulted or hand-stopped | control | ref | |
| REQ-060 | C10 | fail-to-run fault, latched | control | ref+set | **Q-C12** |
| REQ-061 | C11 | fail-to-stop fault, latched | control | ref+set | |
| REQ-062 | C12 | fault indicated by absence of `DiscreteInputs.CycloneDustSysOk` | control | ref+B-21+io | A Q-12 closed |
| REQ-063 | C13 | fault reset from `HMIControlSignals.SystemReset` | HMI | ref+B-15+io | |
| REQ-064 | C14 | running from `DiscreteInputs.CycloneDustAutoRunning/Stop` | control | ref+io | **Q-C08** |
| REQ-065 | C15 | running hours totalised | HMI | ref | |
| REQ-066 | C16 | operator status indication published | HMI | ref | |
| REQ-067 | C17 | alarm indication published | alarm | ref | out of scope this rung |
| REQ-068 | P1 | NO equipment start permissive is specified — gap carried as a gap | control | A | **A Q-04** |
| REQ-069 | P2 | shutdown holds until `PlantControl.FansShutdownReady` | control | A+B-12+io | A Q-09 |
| REQ-070 | P3 | shutdown holds until ShredderControlInst1 reports shutdown complete | control | A+B-11 | A Q-09 |
| REQ-071 | P4 | auto-start commanded while plant run state is running | control | B-02+io | |
| REQ-072 | P5 | auto shutdown suppressed while this machine is in hand | mode | B-13 | |
| REQ-073 | P6 | run command drives `DiscreteOutputs.CycloneDustFilterStart` | control | B-23+io | |
| REQ-074 | P7 | reset echoed on `DiscreteOutputs.CycloneDustFilterReset` | HMI | B-16+io | |
| REQ-075 | P8 | REVERSE: unresolved whether its enable gates ShredderControlInst1 | control | A | **A Q-05** |

### MotorVSDInst1 — Discharge Conveyor VSD (vsd-motor)

| REQ | id | Requirement | Class | Provenance | Q |
|---|---|---|---|---|---|
| REQ-076 | C1 | runs on plant auto start, or operator hand start while in hand | mode | ref | Q-C09 |
| REQ-077 | C2 | does not start while inhibited — `DiscreteInputs.AirStarDCIsoFB` | control | ref+B-26+io | |
| REQ-078 | C3 | does not start while faulted or while fail-to-run stands | control | ref | |
| REQ-079 | C4 | does not start unless system-healthy | control | ref | **Q-C03** |
| REQ-080 | C5 | does not start until pre-start warning complete | control | ref+B-05+io | |
| REQ-081 | C6 | speed setpoint auto/hand, floored at minimum, scaled to rated max | control | ref+set | |
| REQ-082 | C7 | forward/reverse capable; running confirmed from a running feedback | control | ref | **Q-C13** |
| REQ-083 | C8 | declares itself enabled after its up-to-speed time | control | ref+set | |
| REQ-084 | C9 | shutdown complete after the shutdown time with running cleared | control | ref | **Q-C13** |
| REQ-085 | C10 | shutdown complete immediately if faulted | control | ref | |
| REQ-086 | C11 | run supervision armed only after the start-up time; re-armed on direction change | timing | ref+set | |
| REQ-087 | C12 | fail-to-run fault after that allowance, latched | control | ref+set | |
| REQ-088 | C13 | fail-to-stop fault, latched | control | ref+set | |
| REQ-089 | C14 | drive error raises a latched fault | control | ref | |
| REQ-090 | C15 | fault reset from `HMIControlSignals.SystemReset` | HMI | ref+B-15+io | **Q-C14** |
| REQ-091 | C16 | drive condition brought back for the operator | HMI | ref | |
| REQ-092 | C17 | running-forward confirmed from `DiscreteInputs.AirStarDCRotSen` | control | ref+B-25+io | **Q-C13** |
| REQ-093 | C18 | running hours totalised | HMI | ref | |
| REQ-094 | C19 | operator status indication published | HMI | ref | |
| REQ-095 | C20 | alarm indication published | alarm | ref | out of scope this rung |
| REQ-096 | P1 | start only while MotorStarterInst8 (Overband Magnet) is enabled | control | A | |
| REQ-097 | P2 | start only while ECSControlInst1 is enabled | control | A | |
| REQ-098 | P3 | shutdown holds until AirStarInst1 reports shutdown complete | control | A+B-11 | |
| REQ-099 | P4 | auto-start commanded while plant run state is running | control | B-02+io | |
| REQ-100 | P5 | auto shutdown suppressed while this machine is in hand | mode | B-13 | |
| REQ-101 | P6 | operator may bypass the motion-sensor check — `HMIControlSignals.BypassAirStarDCRotSen` | HMI | B-17+io | **Q-C13** |
| REQ-102 | P7 | run command delivered over the drive interface; no discrete start output | control | B-23+io | |
| REQ-103 | P8 | REVERSE: its enable is a start permissive of AirStarInst1 | control | A+B-10 | |
| REQ-104 | P9 | REVERSE: MotorStarterInst8 holds until this machine's shutdown complete | control | A | |
| REQ-105 | P10 | REVERSE: ECSControlInst1 holds until this machine's shutdown complete | control | A | |
| REQ-106 | P11 | REVERSE: unresolved whether ShredderControlInst1's permissive is this machine | control | A | **A Q-05** |

### MotorVSDInst3 — Sorter Conveyor VSD (vsd-motor)

| REQ | id | Requirement | Class | Provenance | Q |
|---|---|---|---|---|---|
| REQ-107 | C1 | runs on plant auto start, or operator hand start while in hand | mode | ref | Q-C09 |
| REQ-108 | C2 | does not start while inhibited — `DiscreteInputs.OSCIsoFB` | control | ref+B-26+io | |
| REQ-109 | C3 | does not start while faulted or while fail-to-run stands | control | ref | |
| REQ-110 | C4 | does not start unless system-healthy | control | ref | **Q-C03** |
| REQ-111 | C5 | does not start until pre-start warning complete | control | ref+B-05+io | |
| REQ-112 | C6 | speed setpoint auto/hand, floored at minimum, scaled to rated max | control | ref+set | |
| REQ-113 | C7 | forward/reverse capable; running confirmed from a running feedback | control | ref | **Q-C16** |
| REQ-114 | C8 | declares itself enabled after its up-to-speed time | control | ref+set | |
| REQ-115 | C9 | shutdown complete after the shutdown time with running cleared | control | ref | **Q-C16** |
| REQ-116 | C10 | shutdown complete immediately if faulted | control | ref | |
| REQ-117 | C11 | run supervision armed only after the start-up time; re-armed on direction change | timing | ref+set | |
| REQ-118 | C12 | fail-to-run fault after that allowance, latched | control | ref+set | |
| REQ-119 | C13 | fail-to-stop fault, latched | control | ref+set | |
| REQ-120 | C14 | drive error raises a latched fault | control | ref | |
| REQ-121 | C15 | fault reset from `HMIControlSignals.SystemReset` | HMI | ref+B-15+io | **Q-C14** |
| REQ-122 | C16 | drive condition brought back for the operator | HMI | ref | |
| REQ-123 | C17 | motion-sensor use for this instance UNRESOLVED — `DiscreteInputs.OSCRotSen` unbound | control | ref+io | **Q-C16** |
| REQ-124 | C18 | running hours totalised | HMI | ref | |
| REQ-125 | C19 | operator status indication published | HMI | ref | |
| REQ-126 | C20 | alarm indication published | alarm | ref | out of scope this rung |
| REQ-127 | P1 | start only while TomraControlInst1 reports ready — `DiscreteInputs.TomraReady` | control | A+B-09+io | |
| REQ-128 | P2 | start only while TomraControlInst1 is free of faults | control | A+B-09 | **Q-C17** |
| REQ-129 | P3 | start only while MotorStarterInst6 (Ejected Material Conveyor) is enabled | control | A | |
| REQ-130 | P4 | start only while MotorStarterInst7 (Residual Material Conveyor) is enabled | control | A | |
| REQ-131 | P5 | shutdown holds until ECSControlInst1 reports shutdown complete | control | A+B-11 | |
| REQ-132 | P6 | auto-start commanded while plant run state is running | control | B-02+io | |
| REQ-133 | P7 | auto shutdown suppressed while this machine is in hand | mode | B-13 | **A Q-06** |
| REQ-134 | P8 | run command delivered over the drive interface; no discrete start output | control | B-23+io | |
| REQ-135 | P9 | REVERSE: its enable is a start permissive of FilterUnitInst2 | control | A | |
| REQ-136 | P10 | REVERSE: its enable is a start permissive of FilterUnitInst3 | control | A | |
| REQ-137 | P11 | REVERSE: its enable is a start permissive of ECSControlInst1 | control | A | |
| REQ-138 | P12 | REVERSE: TomraControlInst1 holds until this machine's shutdown complete | control | A | |

### TomraControlInst1 — Optical Sorter (optical-sorter)

| REQ | id | Requirement | Class | Provenance | Q |
|---|---|---|---|---|---|
| REQ-139 | C1 | runs on plant auto start, or operator hand start while in hand | mode | ref | Q-C09 |
| REQ-140 | C2 | does not start while inhibited/isolated | control | ref | **Q-C18** |
| REQ-141 | C3 | does not start while faulted or while fail-to-run stands | control | ref | |
| REQ-142 | C4 | does not start until pre-start warning complete | control | ref+B-05+io | |
| REQ-143 | C5 | commanded over the data link and by `DiscreteOutputs.TomraRun`, from one decision | comms | ref+B-28+io | **Q-C19** |
| REQ-144 | C6 | condition read from link and from `TomraReady` / `TomraRunning` / `TomraComFlt` | control | ref+B-28+io | |
| REQ-145 | C7 | data link supervised by a liveness indication; loss latches comms-lost | comms | ref+set | **Q-C19** |
| REQ-146 | C8 | running needs link+hardwired agreement; hardwired alone while the link is dead | comms | ref+B-22 | |
| REQ-147 | C9 | declares itself enabled after its up-to-speed time while ready; withdraws on loss of running | control | ref+set | |
| REQ-148 | C10 | shutdown complete after the shutdown time once stopped running | control | ref+set | |
| REQ-149 | C11 | fail-to-run fault, latched | control | ref+set | |
| REQ-150 | C12 | fail-to-stop fault, latched | control | ref+set | |
| REQ-151 | C13 | machine-reported faults plus link loss raise a latched fault | control | ref+B-22 | **Q-C19** |
| REQ-152 | C14 | fault reset from `HMIControlSignals.SystemReset`, passed to the machine over the link | HMI | ref+B-15+io | **Q-C19** |
| REQ-153 | C15 | running hours totalised | HMI | ref | |
| REQ-154 | C16 | operator status indication published | HMI | ref | |
| REQ-155 | C17 | alarm indication published | alarm | ref | out of scope this rung |
| REQ-156 | C18 | sorting program and belt on/off delays operator-settable | HMI | ref+set | Q-C21 |
| REQ-157 | C19 | byte order of the exchanged data selectable — `PlantControl.Test[5]` | comms | ref+io | **Q-C20** |
| REQ-158 | P1 | start only while MotorStarterInst6 (Ejected Material Conveyor) is enabled | control | A | |
| REQ-159 | P2 | start only while MotorStarterInst7 (Residual Material Conveyor) is enabled | control | A | |
| REQ-160 | P3 | shutdown holds until MotorVSDInst3 reports shutdown complete | control | A+B-11 | |
| REQ-161 | P4 | auto-start commanded while plant run state is running | control | B-02+io | |
| REQ-162 | P5 | auto shutdown suppressed while this machine is in hand | mode | B-13 | **A Q-06** |
| REQ-163 | P6 | run command drives `DiscreteOutputs.TomraRun` | control | B-28+io | |
| REQ-164 | P7 | REVERSE: its readiness and freedom from faults gate MotorVSDInst3's start | control | A+B-09 | **Q-C17** |
| REQ-165 | P8 | REVERSE: MotorStarterInst6 holds until this machine's shutdown complete | control | A | |
| REQ-166 | P9 | REVERSE: MotorStarterInst7 holds until this machine's shutdown complete | control | A | |

## Open questions raised at rung C

Never silently resolved. Blocking questions must be answered before any coding.

- **Q-C01 — BLOCKING. Dust filter feedback roles: which of `*Ready` / `*Op` is "running" and which
  is "remote-operational"?** Affects `FilterUnitInst2` and `FilterUnitInst3`. Each unit has exactly
  three feedbacks (`Flt`, `Op`, `Ready`) and the source names exactly three roles (fault, running,
  remote-operational). `Flt` is unambiguous; the other two are not, and no document assigns them.
  This is a **two requirements / two same-shaped signals** case, which the method rules is always a
  candidate set of two and never a coin flip. Getting it backwards inverts a start permissive and a
  running confirmation simultaneously — the machine would be permitted to start on a running signal
  and confirmed running on a mode signal. *Needed:* the field wiring schedule or the unit's
  terminal list.

- **Q-C02 — BLOCKING. No isolator signal exists for any of the three filter units.** Class
  requirement FU-02 (does not start while inhibited) has an EMPTY candidate set for
  `FilterUnitInst1/2/3`. Every motor and conveyor in the plant has an `*IsoFB` input; no filter unit
  does. No tag was proposed. *Needed:* confirmation that filter units are genuinely not isolator-
  interlocked in the PLC (in which case FU-02 is a recorded minus-delta for the class in this plant),
  or the missing signals.

- **Q-C03 — BLOCKING. `DiscreteInputs.ControlHealthy` is unbound while every machine claims a
  system-healthy requirement.** Rung B (B-33) states the layer asserts health unconditionally, yet a
  plant health input exists and is bound to nothing. The candidate set for FU-04 / VSD-04 is
  {`ControlHealthy`, an unconditional assertion} — two members, and the second is what the source
  says while the first is what the IO table offers. Affects five of the six instances; the sixth
  (`TomraControlInst1`) has no healthy condition at all, which is B Q-B12. *Needed:* what
  `ControlHealthy` is for.

- **Q-C04 — BLOCKING. `HMIControlSignals.HandFansShutdown` is unbound.** An operator command whose
  name is precisely a hand shutdown of the fans, in a plant with three fan/filter units, and rung B
  never mentions it. §8 requires this to be a blocking question and a candidate delta, not a silent
  omission. *Needed:* what it commands and which units it lands on.

- **Q-C05 — BLOCKING. Six `Global*Overwrite` HMI flags and their `ProcessTimings` partners are
  unbound.** `GlobalFTTimeOverwrite`, `GlobalUPSEnableTimeOverwrite`,
  `GlobalShutdownCompleteTimeOverwrite`, `GlobalVSDStartUpTimeOverwrite`,
  `GlobalVSDAutoSpeedOverwrite`, `GlobalVSDHandSpeedOverwrite`, each paired with a `Global*` value in
  `ProcessTimings`. They are an operator facility to override, plant-wide, exactly the per-instance
  settings this register records as per-instance. If they are live, every SETTINGS block in every
  spec is conditional. *Needed:* are they in scope for this layer?

- **Q-C06 — BLOCKING. The anti-condensation feature is entirely unspecified.**
  `HMIControlSignals.AntiConEnabled`, `RunAntiConNxtStart`, `SkipAntiConNxtStart`,
  `PlantControl.AntiConRunRequired` and `ProcessTimings.AntiCondensationTime` (5.0 s) exist, are
  unbound, and appear nowhere in rung A or rung B. `*Enabled` is on §8's control-implying list.
  Anti-condensation is a running-the-machine behaviour, so it plausibly scopes onto the motors and
  fans in this run. *Needed:* is it this layer's job, and which machines?

- **Q-C07 — Cyclone remote-mode binding rests on a name.** `CycloneDustRemOp` was bound to FU-05 on
  the basis that "RemOp" names remote-operation and B-27 lists a remote-operational indication for
  every filter unit. The candidate set was two (`CycloneDustRemOp`, `CycloneDustAutoRunning/Stop`)
  and was split on documentary basis, which the method permits. Recorded so the basis is auditable.

- **Q-C08 — BLOCKING. `DiscreteInputs.CycloneDustAutoRunning/Stop` — which state does TRUE mean?**
  The signal name joins two opposite meanings. It is bound to the Cyclone Filter Unit's running
  confirmation (C14) and through it to its shutdown-complete (C8) and both fail-to-run and
  fail-to-stop supervision. Inverted, the unit is "running" whenever it is stopped. *Needed:* the
  signal's polarity.

- **Q-C09 — BLOCKING. There is no auto/hand selection anywhere in the IO table, and
  `HMIControlSignals.GlobalSetAllToAuto` is unbound.** Every one of the six instances carries a class
  requirement to run on auto or hand command (C1 in each spec) with **no** binding available. The
  only related signal in the whole IO table is a global set-all-to-auto flag, bound to nothing.
  *Needed:* where the per-machine hand selection comes from, and what the global flag does.

- **Q-C10 — Filter fan time has two sources with the same value.** `ProcessTimings.NormalFanStartTime`
  (10.0, engineering-owned, cited by the source) and each unit's own instance default (also 10.0).
  Harmless while they agree; a silent divergence if one is ever changed. Non-blocking.

- **Q-C11 — Dust Filter Unit 2's fail-to-run time is 3.0 s where Unit 1's is 2.0 s.** The only
  difference found between two units the source treats as an identical pair. No documentary basis.
  Non-blocking, but it is the kind of difference that is either meaningful or a typo.

- **Q-C12 — BLOCKING. The Cyclone Filter Unit's fail-to-run time is 60.0 s.** Its two siblings use
  2.0 s and 3.0 s. Sixty seconds of commanded-but-not-running before a fault is raised is close to
  no run supervision at all, on a unit whose start is already unspecified (A Q-04) and whose enable
  nothing consumes (A Q-13). Either a deliberate allowance for a slow cyclone fan or a value nobody
  revisited. *Needed:* the intended value.

- **Q-C13 — BLOCKING. The Discharge Conveyor VSD has no running feedback other than its motion
  sensor, and the operator can bypass that sensor.** The IO table contains no `AirStarDCRunning`.
  Rung A asked what confirms running while bypassed (its Q-07); the IO table now answers that
  **nothing does** — the candidate set for the bypassed case is empty in the IO table, leaving only
  the drive's own status over the interface. So the bypass does not degrade a redundant check, it
  removes the only one. *Needed:* the intended bypassed-case behaviour. This is the single most
  consequential unresolved binding in this run.

- **Q-C14 — BLOCKING. `DiscreteOutputs.VSDFaulrReset` is unbound and cannot serve three drives
  distinctly.** One VSD fault-reset output exists for `MotorVSDInst1`, `MotorVSDInst2` and
  `MotorVSDInst3`. Either it resets all of them together, or it belongs to one of them, or it is
  dead. Affects both VSD instances in this run. *Needed:* which drive(s) it resets.

- **Q-C16 — BLOCKING. The Sorter Conveyor VSD's rotation sensor is unclaimed, and has neither a
  bypass nor a running feedback beside it.** `DiscreteInputs.OSCRotSen` exists and no requirement
  binds it. Every other bypassable conveyor in the plant has the triplet {sensor, bypass, running};
  this machine has the sensor alone, and `BypassOSCRotSen` does not exist. So either this conveyor's
  running confirmation is the sensor (with no bypass, unlike its siblings), or the sensor is dead
  wiring. *Needed:* what confirms this conveyor is running. **This finding comes only from the §8
  reverse sweep** — no requirement in the spec pointed at the signal.

- **Q-C17 — BLOCKING. "The optical sorter is not faulted" — which fault?** The Sorter Conveyor VSD's
  start permissive (REQ-128, and its reverse REQ-164) needs a fault condition. Candidate set:
  `DiscreteInputs.TomraComFlt` (the raw comms-fault input) or the sorter's own aggregated fault
  status, which additionally covers fourteen machine-reported fault bits and the link watchdog.
  These are very different permissives: the raw input alone permits the conveyor to feed a sorter
  that is reporting a machine fault over a healthy link. No documentary basis for either. *Needed:*
  which fault gates the conveyor.

- **Q-C18 — BLOCKING. No isolator signal exists for the Optical Sorter.** Class requirement OS-02
  has an empty candidate set. *Needed:* as Q-C02.

- **Q-C19 — BLOCKING. The Optical Sorter's data link is not in the IO table and its scope is
  undecided.** Eight status words in and two control words out, carrying the run command (C5), the
  liveness watchdog (C7), the machine's fault set (C13) and the outbound reset (C14) — four class
  requirements that cannot be bound at all. Rung A raised this (its Q-08) from the topology; the IO
  table confirms none of it is discrete IO. *Needed:* the scope ruling and, if in scope, the link
  definition.

- **Q-C20 — BLOCKING. A production byte-order selector is bound to `PlantControl.Test[5]`.** The
  Optical Sorter's word byte-order (C19) is selected by a member of an array named `Test`, and the
  rest of that array is unbound with no basis for saying what it drives. Either the binding is wrong
  or a commissioning switch is live in the running plant. *Needed:* what `PlantControl.Test` is.

- **Q-C21 — Optical Sorter settings that are offered and never used.** Program selection and belt
  on/off delays are settable on the machine and the control never acts on them (recorded in the
  class reference's anomaly A-3). Carried as a delta rather than dropped. Non-blocking.

## Questions carried unresolved from earlier rungs

Rung A: **Q-01** (no class reference library — all three references are reverse-derived),
**Q-02** (single-instance optical-sorter class), **Q-03** (no material-flow source),
**Q-04** (Cyclone has no start permissive), **Q-05** (Shredder's permissive referent ambiguous and
under-enumerated), **Q-06** (cross-referenced hand intervention), **Q-07** (superseded and sharpened
by Q-C13), **Q-08** (superseded by Q-C19); non-blocking Q-09, Q-10, Q-11, Q-13.
**Q-12 is CLOSED** at this rung — see `equipment-specs/FilterUnitInst1.md`.

Rung B: **Q-B1** (the behaviour source is not an independent functional description; B-19/B-20/B-29
have no source citation at all), **Q-B5** (the stated reason for the start interlocks contradicts the
interlocks themselves), **Q-B8** (hand-intervention scope), **Q-B10** (motion-sensor contradiction
inside the source); non-blocking Q-B2, Q-B3, Q-B4, Q-B6, Q-B7, Q-B9, Q-B11, Q-B12.
