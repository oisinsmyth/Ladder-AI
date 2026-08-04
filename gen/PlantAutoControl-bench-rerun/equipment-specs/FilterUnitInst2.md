EQUIPMENT SPEC — FilterUnitInst2 ("Dust Filter Unit 1")
  class: FilterUnitSystem (ref v0-derived)   fed-by: NOT ESTABLISHED [A Q-03]   discharges-to: NOT ESTABLISHED [A Q-03]
  produced 2026-08-04 by /gen-equipment-spec (rung C). Alarms out of scope at this rung.

IO BINDING                                                                         [io]
  start command out        DiscreteOutputs.FilterUnit1Start                exists
  fault reset out          DiscreteOutputs.FilterUnit1Reset                exists
  unit fault in            DiscreteInputs.FilterUnit1Flt                   exists
  unit feedback in (A)     DiscreteInputs.FilterUnit1Ready                 exists    UNRESOLVED ROLE — Q-C01
  unit feedback in (B)     DiscreteInputs.FilterUnit1Op                    exists    UNRESOLVED ROLE — Q-C01
  isolator in              NONE IN IO TABLE                                          Q-C02
  plant run state          PlantControl.Status                             exists
  pre-start complete       PlantControl.PreStartComplete                   exists
  fans shutdown ready      PlantControl.FansShutdownReady                  exists
  global fault reset       HMIControlSignals.SystemReset                   exists
  fan up-to-speed time     ProcessTimings.NormalFanStartTime = 10.0        exists

CONTROL REQUIREMENTS   (complete by construction from the class reference — all 17 instantiated)
  C1  (FU-01) runs on the plant's automatic start command, or on the operator's hand start
      command while the operator has taken it to hand                                  [ref]
      NO IO BINDING — the auto/hand selection is not present in the IO table. See Q-C09.
  C2  (FU-02) does not start while inhibited (isolated / locked off)                   [ref]
      NO SIGNAL EXISTS. Every motor in the IO table has an isolator feedback; no filter
      unit does. BLOCKING Q-C02. Candidate delta.
  C3  (FU-03) does not start while it has an active fault                              [ref]
      fault source per C12                                                             [ref+io]
  C4  (FU-04) does not start unless reported system-healthy                            [ref]
      BINDING AMBIGUOUS — DiscreteInputs.ControlHealthy exists and is unbound, while
      rung B (B-33) states the layer asserts health unconditionally. BLOCKING Q-C03.
  C5  (FU-05) does not start unless the unit is in remote (not local) mode              [ref]
      candidate set {FilterUnit1Ready, FilterUnit1Op} — BLOCKING Q-C01
  C6  (FU-06) does not start until the plant pre-start warning phase has completed
      — from PlantControl.PreStartComplete                                        [ref+B-05+io]
  C7  (FU-07) once running and confirmed running for the fan up-to-speed time, declares
      itself enabled — time from ProcessTimings.NormalFanStartTime (10 s)     [ref+B-30+io]
  C8  (FU-08) on a controlled shutdown request the unit stops, and declares its shutdown
      complete once its shutdown time has elapsed and its running feedback has cleared  [ref]
      running feedback per C14 — BLOCKING Q-C01
  C9  (FU-09) declares shutdown complete immediately if faulted, or if under hand control
      with the operator not calling it to run                                          [ref]
  C10 (FU-10) commanded but not reporting running within the fail-to-run time raises a
      latched fail-to-run fault                                                   [ref+set]
  C11 (FU-11) reporting running while not commanded, for the same time, raises a latched
      fail-to-stop fault                                                          [ref+set]
  C12 (FU-12) a fault reported by the unit raises a latched fault
      — from DiscreteInputs.FilterUnit1Flt                                  [ref+B-21+io]
  C13 (FU-13) a fault-reset command clears all latched faults
      — from HMIControlSignals.SystemReset                                  [ref+B-15+io]
  C14 (FU-14) the unit's running state is taken from its own running feedback; this class
      has no motion sensor                                                             [ref]
      candidate set {FilterUnit1Ready, FilterUnit1Op} — BLOCKING Q-C01
      Confirmed consistent with rung B: B-24 lists the dust filter units among the machines
      with no motion sensor, and no FilterUnit1RotSen exists in the IO table.
  C15 (FU-15) running hours are totalised                                              [ref]
      NO IO BINDING (published on the instance, not to field IO)
  C16 (FU-16) an operator status indication is published                               [ref]
      NO IO BINDING (published on the instance, not to field IO)
  C17 (FU-17) alarm indication published for fail-to-run, fail-to-stop, unit fault and
      not-in-remote                                                                    [ref]
      OUT OF SCOPE AT THIS RUNG — alarms are a separate artifact. Instantiated, not dropped.

PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  start permitted only while the Sorter Conveyor VSD (MotorVSDInst3) is enabled      [A]
      NOTE: rung B's stated reason for this class of permissive (the receiving machine is
      already enabled) is implausible for a dust extraction unit — BLOCKING Q-B5 stands.
  P2  on controlled shutdown, hold until PlantControl.FansShutdownReady            [A+B-12+io]
  P3  on controlled shutdown, hold until the Air-Separator VSD (AirStarInst1) reports its
      shutdown complete                                                            [A+B-11]
      P2 and P3 are TWO relations and stay two: they resolve to different signals, so §5
      forbids merging them here. Any reduction is a rung-D discharge. (A Q-09.)
  P4  commanded to start automatically while the plant run state is running
      — from PlantControl.Status                                                 [B-02+io]
  P5  the automatic controlled-shutdown request for this machine is suppressed while this
      machine is under hand intervention                                            [B-13]
  P6  the resulting run command drives DiscreteOutputs.FilterUnit1Start          [B-23+io]
  P7  the global reset is additionally driven back to the unit on
      DiscreteOutputs.FilterUnit1Reset                                           [B-16+io]
  P8  REVERSE — this unit's enable is one of the start permissives of the Air-Separator VSD,
      jointly with FilterUnitInst3 and MotorVSDInst1                              [A+B-10]
      Obligation lands on AirStarInst1, not on this machine. Carried for rung D.

SETTINGS
  fan up-to-speed time   owner: engineering   value: 10.0 s   source: ProcessTimings.NormalFanStartTime  [io+B-30]
                         NOTE: the instance also carries its own default of 10.0 s for the same
                         setting. Two sources, same value — Q-C10 (non-blocking).
  fail-to-run time       owner: HMI           value: 2.0 s    as-built instance default        [io]
  shutdown time          owner: HMI           value: 5.0 s    as-built instance default        [io]

UNCLAIMED IO (§8 sweep, opposite direction)
  Instance-scoped signals in the IO table: FilterUnit1Start, FilterUnit1Reset, FilterUnit1Flt,
  FilterUnit1Ready, FilterUnit1Op. All five appear above (two of them only as an unresolved
  candidate set, Q-C01). No instance-scoped signal is unclaimed.
  Plant-scoped signals that could be scoped to this machine and are bound to NOTHING anywhere —
  each is a control-implying name and therefore a BLOCKING question, not a silent omission:
    HMIControlSignals.HandFansShutdown          — Q-C04 (a hand fans-shutdown command; this is a
                                                  fan unit, and rung B never mentions it)
    HMIControlSignals.GlobalSetAllToAuto        — Q-C09
    HMIControlSignals.GlobalFTTimeOverwrite     — Q-C05
    HMIControlSignals.GlobalUPSEnableTimeOverwrite      — Q-C05
    HMIControlSignals.GlobalShutdownCompleteTimeOverwrite — Q-C05
    HMIControlSignals.AntiConEnabled / RunAntiConNxtStart / SkipAntiConNxtStart
    PlantControl.AntiConRunRequired / ProcessTimings.AntiCondensationTime = 5.0  — Q-C06
    DiscreteInputs.ControlHealthy                — Q-C03
  E-stop / safety-circuit feedback members present in the IO table are out of scope by hard
  rule 2 and by B-33; they are not enumerated, bound, or elaborated here.

DELTAS
  D1  PLUS — up-to-speed time is taken from a plant-level parameter shared by all three filter
      units rather than being a per-unit setting                                    [A+B-30]
  D2  ASYMMETRY — its start permissive (MotorVSDInst3) has no reciprocal hold; that machine
      holds on the ECS, not on this unit                                          [A, A Q-10]
  D3  CANDIDATE MINUS — no isolator signal exists for this unit where every motor has one;
      C2 may not be realisable                                                        [io]
  searched: rung-A delta block for this instance, FilterUnitSystem reference §Class requirement set
  and §Class deltas, the §8 unclaimed sweep above, the instance's own as-built setting values
  (compared against FilterUnitInst3 and FilterUnitInst1).

OPEN
  Q-C01 (BLOCKING) · Q-C02 (BLOCKING) · Q-C03 (BLOCKING) · Q-C04 (BLOCKING) · Q-C05 (BLOCKING)
  Q-C06 (BLOCKING) · Q-C09 (BLOCKING) · Q-C10
  Carried unresolved from earlier rungs: A Q-01, A Q-03, A Q-09, A Q-10, B Q-B1, B Q-B5, B Q-B7.
