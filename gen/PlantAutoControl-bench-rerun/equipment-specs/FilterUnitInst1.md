EQUIPMENT SPEC — FilterUnitInst1 ("Cyclone Filter Unit")
  class: FilterUnitSystem (ref v0-derived)   fed-by: NOT ESTABLISHED [A Q-03]   discharges-to: NOT ESTABLISHED [A Q-03]
  produced 2026-08-04 by /gen-equipment-spec (rung C). Alarms out of scope at this rung.

IO BINDING                                                                         [io]
  start command out        DiscreteOutputs.CycloneDustFilterStart          exists
  fault reset out          DiscreteOutputs.CycloneDustFilterReset          exists
  system-OK in             DiscreteInputs.CycloneDustSysOk                 exists
                           (fault is indicated by the ABSENCE of this signal — B-21)
  remote-operational in    DiscreteInputs.CycloneDustRemOp                 exists    Q-C07
  running in               DiscreteInputs.CycloneDustAutoRunning/Stop      exists    POLARITY UNRESOLVED — Q-C08
  unit fault in            NONE IN IO TABLE — no CycloneDustFlt member exists
  isolator in              NONE IN IO TABLE                                          Q-C02
  plant run state          PlantControl.Status                             exists
  pre-start complete       PlantControl.PreStartComplete                   exists
  fans shutdown ready      PlantControl.FansShutdownReady                  exists
  global fault reset       HMIControlSignals.SystemReset                   exists
  fan up-to-speed time     ProcessTimings.NormalFanStartTime = 10.0        exists

  RESOLVES rung-A Q-12 ("one fault source or two?") with evidence: the IO table contains no
  CycloneDustFlt member. The candidate set for a direct fault indication is EMPTY, so the two
  sentences in the source describe one thing, not two, and the class fault feedback FU-12 is
  realised here as the absence of CycloneDustSysOk. No hardware was invented to make both
  readings true. Recorded so the resolution is auditable rather than assumed.

CONTROL REQUIREMENTS   (complete by construction from the class reference — all 17 instantiated)
  C1  (FU-01) runs on the plant's automatic start command, or on the operator's hand start
      command while taken to hand                                                    [ref]
      NO IO BINDING — see Q-C09.
  C2  (FU-02) does not start while inhibited (isolated / locked off)                 [ref]
      NO SIGNAL EXISTS — BLOCKING Q-C02. Candidate delta.
  C3  (FU-03) does not start while it has an active fault                       [ref+io]
  C4  (FU-04) does not start unless reported system-healthy                          [ref]
      BINDING AMBIGUOUS — and additionally confusable here with CycloneDustSysOk, which is
      a per-unit health signal already bound to the fault requirement C12. BLOCKING Q-C03.
  C5  (FU-05) does not start unless the unit is in remote (not local) mode
      — from DiscreteInputs.CycloneDustRemOp                                  [ref+B-27+io]
      candidate set was {CycloneDustRemOp, CycloneDustAutoRunning/Stop}; split on the
      documentary basis that "RemOp" names remote-operation and B-27 lists a
      remote-operational indication for every filter unit. Residual risk logged as Q-C07.
  C6  (FU-06) does not start until the plant pre-start warning phase has completed
      — from PlantControl.PreStartComplete                                   [ref+B-05+io]
  C7  (FU-07) once running and confirmed running for the fan up-to-speed time, declares
      itself enabled — time from ProcessTimings.NormalFanStartTime (10 s)     [ref+B-30+io]
      NOTE: no machine in the plant consumes this enable — A Q-13.
  C8  (FU-08) on a controlled shutdown request the unit stops, and declares its shutdown
      complete once its shutdown time has elapsed and its running feedback has cleared  [ref]
      running feedback per C14 — polarity BLOCKING Q-C08
  C9  (FU-09) declares shutdown complete immediately if faulted, or if under hand control
      with the operator not calling it to run                                          [ref]
  C10 (FU-10) fail-to-run fault, latched                                          [ref+set]
  C11 (FU-11) fail-to-stop fault, latched                                         [ref+set]
  C12 (FU-12) a fault raised from the unit — indicated by the absence of
      DiscreteInputs.CycloneDustSysOk                                         [ref+B-21+io]
  C13 (FU-13) a fault-reset command clears all latched faults
      — from HMIControlSignals.SystemReset                                   [ref+B-15+io]
  C14 (FU-14) running state taken from the unit's own running feedback; no motion sensor [ref]
      — from DiscreteInputs.CycloneDustAutoRunning/Stop, POLARITY UNRESOLVED, BLOCKING Q-C08
  C15 (FU-15) running hours totalised — NO IO BINDING                                  [ref]
  C16 (FU-16) operator status indication published — NO IO BINDING                     [ref]
  C17 (FU-17) alarm indication published — OUT OF SCOPE AT THIS RUNG, instantiated not dropped

PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  NO EQUIPMENT START PERMISSIVE IS SPECIFIED. Rung A refused to adopt the source's
      "(runs with plant)" as a stated absence — BLOCKING A Q-04, unresolved. This line exists
      so the gap is rendered as a gap, not as a machine that starts unconditionally.  [A]
  P2  on controlled shutdown, hold until PlantControl.FansShutdownReady            [A+B-12+io]
  P3  on controlled shutdown, hold until the Shredder (ShredderControlInst1) reports its
      shutdown complete                                                            [A+B-11]
      P2 and P3 stay two relations (§5 — different signals). Any reduction is a rung-D discharge.
  P4  commanded to start automatically while the plant run state is running
      — from PlantControl.Status                                                 [B-02+io]
  P5  the automatic controlled-shutdown request is suppressed while this machine is under
      hand intervention                                                              [B-13]
  P6  the resulting run command drives DiscreteOutputs.CycloneDustFilterStart    [B-23+io]
  P7  the global reset is additionally driven back to the unit on
      DiscreteOutputs.CycloneDustFilterReset                                     [B-16+io]
  P8  REVERSE — UNRESOLVED. Whether this unit's enable gates the Shredder's start is the
      subject of BLOCKING A Q-05 (three candidate referents for "Discharge Conveyor enabled").
      No reverse relation is asserted.                                                 [A]

SETTINGS
  fan up-to-speed time   owner: engineering   value: 10.0 s   source: ProcessTimings.NormalFanStartTime  [io+B-30]
  fail-to-run time       owner: HMI           value: 60.0 s   as-built instance default        [io]
  shutdown time          owner: HMI           value: 5.0 s    as-built instance default        [io]

UNCLAIMED IO (§8 sweep, opposite direction)
  Instance-scoped signals: CycloneDustFilterStart, CycloneDustFilterReset, CycloneDustSysOk,
  CycloneDustRemOp, CycloneDustAutoRunning/Stop. All five appear above. None unclaimed.
  Plant-scoped control-implying signals unbound anywhere — same list as the dust filter units:
  HandFansShutdown (Q-C04), GlobalSetAllToAuto (Q-C09), the three Global*Overwrite settings
  (Q-C05), the anti-condensation set (Q-C06), ControlHealthy (Q-C03).
  E-stop / safety-circuit members: out of scope by hard rule 2 and B-33; not enumerated.

DELTAS
  D1  PLUS — plant-level shared up-to-speed time (10 s), as its siblings                [A+B-30]
  D2  CHANGED — its fault is indicated by the absence of a system-OK signal where its two
      siblings have a direct fault input. Confirmed at this rung against the IO table
      (no CycloneDustFlt exists), which is what let rung A's Q-12 be closed.       [A+B-21+io]
  D3  CHANGED — its running indication is a single signal whose name joins two opposite
      meanings ("AutoRunning/Stop"), unlike the siblings' plainly-named feedbacks       [io]
  D4  MINUS — no equipment start permissive at all, unlike both siblings          [A, A Q-04]
  D5  MINUS — its enable is consumed by no machine in the plant                  [A, A Q-13]
  D6  OUTLIER SETTING — its fail-to-run time is 60.0 s against 2.0 s and 3.0 s on the two
      dust filter units: twenty to thirty times its siblings' value, with no documentary
      basis anywhere in the source. Either a deliberate allowance for a slow cyclone fan
      run-up or a commissioning value nobody revisited. BLOCKING Q-C12 — a fail-to-run
      time this long is effectively no fail-to-run supervision for a minute.            [io]
  D7  CANDIDATE MINUS — no isolator signal exists                                       [io]
  searched: rung-A delta block for this instance, FilterUnitSystem reference §Class requirement set
  and §Class deltas, the §8 sweep above, and a value-by-value comparison of this instance's
  as-built settings against FilterUnitInst2 and FilterUnitInst3 (which is what surfaced D6).

OPEN
  Q-C02 (BLOCKING) · Q-C03 (BLOCKING) · Q-C04 (BLOCKING) · Q-C05 (BLOCKING) · Q-C06 (BLOCKING)
  Q-C07 · Q-C08 (BLOCKING) · Q-C09 (BLOCKING) · Q-C12 (BLOCKING)
  Carried unresolved: A Q-01, A Q-03, A Q-04, A Q-05, A Q-09, A Q-13, B Q-B1, B Q-B7.
  CLOSED at this rung: A Q-12 (see the IO BINDING note above).
