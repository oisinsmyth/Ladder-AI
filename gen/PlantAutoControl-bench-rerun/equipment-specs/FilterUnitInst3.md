EQUIPMENT SPEC — FilterUnitInst3 ("Dust Filter Unit 2")
  class: FilterUnitSystem (ref v0-derived)   fed-by: NOT ESTABLISHED [A Q-03]   discharges-to: NOT ESTABLISHED [A Q-03]
  produced 2026-08-04 by /gen-equipment-spec (rung C). Alarms out of scope at this rung.

IO BINDING                                                                         [io]
  start command out        DiscreteOutputs.FilterUnit2Start                exists
  fault reset out          DiscreteOutputs.FilterUnit2Reset                exists
  unit fault in            DiscreteInputs.FilterUnit2Flt                   exists
  unit feedback in (A)     DiscreteInputs.FilterUnit2Ready                 exists    UNRESOLVED ROLE — Q-C01
  unit feedback in (B)     DiscreteInputs.FilterUnit2Op                    exists    UNRESOLVED ROLE — Q-C01
  isolator in              NONE IN IO TABLE                                          Q-C02
  plant run state          PlantControl.Status                             exists
  pre-start complete       PlantControl.PreStartComplete                   exists
  fans shutdown ready      PlantControl.FansShutdownReady                  exists
  global fault reset       HMIControlSignals.SystemReset                   exists
  fan up-to-speed time     ProcessTimings.NormalFanStartTime = 10.0        exists

CONTROL REQUIREMENTS   (complete by construction from the class reference — all 17 instantiated)
  C1  (FU-01) runs on the plant's automatic start command, or on the operator's hand start
      command while the operator has taken it to hand                                  [ref]
      NO IO BINDING — auto/hand selection is not present in the IO table. See Q-C09.
  C2  (FU-02) does not start while inhibited (isolated / locked off)                   [ref]
      NO SIGNAL EXISTS — BLOCKING Q-C02. Candidate delta.
  C3  (FU-03) does not start while it has an active fault                         [ref+io]
  C4  (FU-04) does not start unless reported system-healthy — BINDING AMBIGUOUS, Q-C03  [ref]
  C5  (FU-05) does not start unless the unit is in remote (not local) mode             [ref]
      candidate set {FilterUnit2Ready, FilterUnit2Op} — BLOCKING Q-C01
  C6  (FU-06) does not start until the plant pre-start warning phase has completed
      — from PlantControl.PreStartComplete                                   [ref+B-05+io]
  C7  (FU-07) once running and confirmed running for the fan up-to-speed time, declares
      itself enabled — time from ProcessTimings.NormalFanStartTime (10 s)     [ref+B-30+io]
  C8  (FU-08) on a controlled shutdown request the unit stops, and declares its shutdown
      complete once its shutdown time has elapsed and its running feedback has cleared  [ref]
      running feedback per C14 — BLOCKING Q-C01
  C9  (FU-09) declares shutdown complete immediately if faulted, or if under hand control
      with the operator not calling it to run                                          [ref]
  C10 (FU-10) fail-to-run fault, latched                                          [ref+set]
  C11 (FU-11) fail-to-stop fault, latched                                         [ref+set]
  C12 (FU-12) a fault reported by the unit raises a latched fault
      — from DiscreteInputs.FilterUnit2Flt                                   [ref+B-21+io]
  C13 (FU-13) a fault-reset command clears all latched faults
      — from HMIControlSignals.SystemReset                                   [ref+B-15+io]
  C14 (FU-14) running state taken from the unit's own running feedback; no motion sensor [ref]
      candidate set {FilterUnit2Ready, FilterUnit2Op} — BLOCKING Q-C01
  C15 (FU-15) running hours totalised                                                  [ref]
      NO IO BINDING
  C16 (FU-16) operator status indication published                                     [ref]
      NO IO BINDING
  C17 (FU-17) alarm indication published — OUT OF SCOPE AT THIS RUNG, instantiated not dropped

PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  start permitted only while the Sorter Conveyor VSD (MotorVSDInst3) is enabled      [A]
      Same Q-B5 caveat as FilterUnitInst2.
  P2  on controlled shutdown, hold until PlantControl.FansShutdownReady            [A+B-12+io]
  P3  on controlled shutdown, hold until the Air-Separator VSD (AirStarInst1) reports its
      shutdown complete                                                            [A+B-11]
      P2 and P3 stay two relations (§5 — different signals). Any reduction is a rung-D discharge.
  P4  commanded to start automatically while the plant run state is running
      — from PlantControl.Status                                                 [B-02+io]
  P5  the automatic controlled-shutdown request is suppressed while this machine is under
      hand intervention                                                              [B-13]
  P6  the resulting run command drives DiscreteOutputs.FilterUnit2Start          [B-23+io]
  P7  the global reset is additionally driven back to the unit on
      DiscreteOutputs.FilterUnit2Reset                                           [B-16+io]
  P8  REVERSE — this unit's enable is one of the start permissives of the Air-Separator VSD,
      jointly with FilterUnitInst2 and MotorVSDInst1                              [A+B-10]

SETTINGS
  fan up-to-speed time   owner: engineering   value: 10.0 s   source: ProcessTimings.NormalFanStartTime  [io+B-30]
  fail-to-run time       owner: HMI           value: 3.0 s    as-built instance default        [io]
  shutdown time          owner: HMI           value: 5.0 s    as-built instance default        [io]

UNCLAIMED IO (§8 sweep, opposite direction)
  Instance-scoped signals: FilterUnit2Start, FilterUnit2Reset, FilterUnit2Flt, FilterUnit2Ready,
  FilterUnit2Op. All five appear above (two only as an unresolved candidate set). None unclaimed.
  Plant-scoped control-implying signals unbound anywhere — identical list to FilterUnitInst2:
  HandFansShutdown (Q-C04), GlobalSetAllToAuto (Q-C09), the three Global*Overwrite settings
  (Q-C05), the anti-condensation set (Q-C06), ControlHealthy (Q-C03).
  E-stop / safety-circuit members: out of scope by hard rule 2 and B-33; not enumerated.

DELTAS
  D1  PLUS — plant-level shared up-to-speed time, as FilterUnitInst2                [A+B-30]
  D2  ASYMMETRY — start permissive has no reciprocal hold                         [A, A Q-10]
  D3  CANDIDATE MINUS — no isolator signal exists                                      [io]
  D4  DIFFERENCE FROM SIBLING — its fail-to-run time is 3.0 s where Dust Filter Unit 1 uses
      2.0 s. This is the ONLY difference found between two units the source treats as an
      identical pair (B-32). No documentary basis for the difference — Q-C11 (non-blocking).  [io]
  searched: rung-A delta block for this instance, FilterUnitSystem reference §Class requirement set
  and §Class deltas, the §8 sweep above, and a value-by-value comparison of this instance's
  as-built settings against FilterUnitInst2 and FilterUnitInst1 (which is what surfaced D4).

OPEN
  Q-C01 (BLOCKING) · Q-C02 (BLOCKING) · Q-C03 (BLOCKING) · Q-C04 (BLOCKING) · Q-C05 (BLOCKING)
  Q-C06 (BLOCKING) · Q-C09 (BLOCKING) · Q-C11
  Carried unresolved: A Q-01, A Q-03, A Q-09, A Q-10, B Q-B1, B Q-B5, B Q-B7.
