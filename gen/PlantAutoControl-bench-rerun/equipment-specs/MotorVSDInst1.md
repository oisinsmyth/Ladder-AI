EQUIPMENT SPEC — MotorVSDInst1 ("Discharge Conveyor VSD Unit")
  class: vsd-motor (ref v0-derived)   fed-by: NOT ESTABLISHED [A Q-03]   discharges-to: NOT ESTABLISHED [A Q-03]
  produced 2026-08-04 by /gen-equipment-spec (rung C). Alarms out of scope at this rung.

IO BINDING                                                                         [io]
  isolator in              DiscreteInputs.AirStarDCIsoFB                   exists
  motion sensor in         DiscreteInputs.AirStarDCRotSen                  exists
  motion-sensor bypass     HMIControlSignals.BypassAirStarDCRotSen         exists  (RETAIN)
  running feedback in      NONE IN IO TABLE — no AirStarDCRunning member exists         Q-C13
  start command out        NONE IN IO TABLE — no AirStarDCStart member exists
                           (consistent with B: the VSDs are commanded over the drive
                           interface, not by a discrete start bit)
  drive fault reset out    DiscreteOutputs.VSDFaulrReset                   exists      Q-C14
  plant run state          PlantControl.Status                             exists
  pre-start complete       PlantControl.PreStartComplete                   exists
  global fault reset       HMIControlSignals.SystemReset                   exists

CONTROL REQUIREMENTS   (complete by construction from the class reference — all 20 instantiated)
  C1  (VSD-01) runs on the plant's automatic start command, or on the operator's hand start
      command while taken to hand                                                    [ref]
      NO IO BINDING — auto/hand selection not present in the IO table. See Q-C09.
  C2  (VSD-02) does not start while inhibited — from DiscreteInputs.AirStarDCIsoFB
                                                                          [ref+B-26+io]
  C3  (VSD-03) does not start while faulted, and not while a fail-to-run fault stands [ref]
  C4  (VSD-04) does not start unless reported system-healthy — Q-C03                 [ref]
  C5  (VSD-05) does not start until the plant pre-start warning phase has completed
      — from PlantControl.PreStartComplete                                   [ref+B-05+io]
  C6  (VSD-06) runs to an automatic speed setpoint in auto and an operator setpoint in hand,
      floored at a minimum running speed and scaled against the machine's rated maximum
      speed                                                                      [ref+set]
      NO IO BINDING — speed demand goes over the drive interface, not discrete IO.
  C7  (VSD-07) can run forward or reverse; running confirmed from a forward or a reverse
      running feedback                                                               [ref]
      NO RUNNING FEEDBACK EXISTS IN THE IO TABLE for this machine. See C17 and Q-C13.
      The reverse direction has no plant use recorded for this machine — candidate delta D4.
  C8  (VSD-08) once running and confirmed running for its up-to-speed time, declares itself
      enabled                                                                   [ref+set]
  C9  (VSD-09) on a controlled shutdown request the motor stops and declares its shutdown
      complete once its shutdown time has elapsed and running feedback has cleared    [ref]
      running feedback per C17 — BLOCKING Q-C13
  C10 (VSD-10) declares shutdown complete immediately if faulted                      [ref]
  C11 (VSD-11) run-confirmation supervision is not armed until the start-up time has elapsed
      after the drive is commanded and its speed demand has settled, and is re-armed on a
      direction change                                                           [ref+set]
  C12 (VSD-12) after that allowance, commanded-but-not-running for the fail-to-run time
      raises a latched fail-to-run fault                                        [ref+set]
  C13 (VSD-13) running-while-not-commanded for the same time raises a latched fail-to-stop
      fault                                                                     [ref+set]
  C14 (VSD-14) a drive error reported by the VSD raises a latched fault              [ref]
      NO IO BINDING — reported over the drive interface.
  C15 (VSD-15) a fault-reset command clears all latched faults
      — from HMIControlSignals.SystemReset                                   [ref+B-15+io]
      A second candidate exists for the reset PATH TO THE DRIVE: DiscreteOutputs.VSDFaulrReset
      is an unbound output whose name implies exactly this function, and which cannot serve
      three VSD instances distinctly. BLOCKING Q-C14.
  C16 (VSD-16) the drive's own condition is brought back for the operator (ready, operation
      enabled, warning, over/under-speed, over-temperature, thermal overload, torque limit,
      current, speed feedback, speed reached)                                        [ref]
      NO IO BINDING — all over the drive interface.
  C17 (VSD-17) a motion/rotation sensor input exists on this class; its use is an
      instance-level decision                                                        [ref]
      THIS INSTANCE USES IT AS THE RUNNING-FORWARD CONFIRMATION — from
      DiscreteInputs.AirStarDCRotSen, motion sensed meaning running forward   [ref+B-25+io]
      Documentary basis for this binding: rung B B-25 names this machine and this signal
      explicitly. Basis for it being the ONLY confirmation: the candidate set for a running
      feedback is otherwise EMPTY in the IO table.
      BUT the bypassed case is unresolved and now demonstrably worse than rung A could see:
      with BypassAirStarDCRotSen applied there is NO other running signal in the IO table to
      fall back to. BLOCKING Q-C13.
  C18 (VSD-18) running hours totalised — NO IO BINDING                                [ref]
  C19 (VSD-19) operator status indication published — NO IO BINDING                   [ref]
  C20 (VSD-20) alarm indication published — OUT OF SCOPE AT THIS RUNG, instantiated not dropped

PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  start permitted only while the Overband Magnet (MotorStarterInst8) is enabled      [A]
  P2  start permitted only while the Equipment Control System (ECSControlInst1) is enabled [A]
      P1 and P2 stay two relations (§5 — different machines, different signals).
  P3  on controlled shutdown, hold until the Air-Separator VSD (AirStarInst1) reports its
      shutdown complete                                                            [A+B-11]
  P4  commanded to start automatically while the plant run state is running
      — from PlantControl.Status                                                 [B-02+io]
  P5  the automatic controlled-shutdown request is suppressed while this machine is under
      hand intervention                                                              [B-13]
  P6  the operator may individually bypass this machine's motion-sensor check
      — from HMIControlSignals.BypassAirStarDCRotSen                            [B-17+io]
  P7  the resulting run command is delivered over the drive interface; there is no discrete
      start output for this machine                                              [B-23+io]
  P8  REVERSE — this machine's enable is one of the start permissives of the Air-Separator
      VSD, jointly with FilterUnitInst2 and FilterUnitInst3                       [A+B-10]
  P9  REVERSE — the Overband Magnet holds through shutdown until this machine reports its
      shutdown complete                                                                [A]
  P10 REVERSE — the Equipment Control System holds through shutdown until this machine
      reports its shutdown complete                                                    [A]
  P11 REVERSE — UNRESOLVED. Whether the Shredder's start permissive refers to this machine
      is BLOCKING A Q-05 (three candidate referents). No relation asserted.             [A]

SETTINGS
  automatic speed        owner: HMI   value: 100.0 %   as-built instance default        [io]
  hand speed             owner: HMI   value: 30.0 %    as-built instance default        [io]
  rated maximum speed    owner: engineering   value: 1500 rpm                           [io]
  drive start-up time    owner: HMI   value: 8.0 s     as-built instance default        [io]
                         NOTE: the class default is 5.0 s; this instance runs 8.0 s. No
                         documentary basis — Q-C15 (non-blocking).
  up-to-speed time       owner: HMI   value: 2.0 s     as-built instance default        [io]
  fail-to-run time       owner: HMI   value: 3.0 s     as-built instance default        [io]
  shutdown time          owner: HMI   value: 8.0 s     as-built instance default        [io]
                         NOTE: 8.0 s against 5.0 s on the sibling VSD — Q-C15.

UNCLAIMED IO (§8 sweep, opposite direction)
  Instance-scoped signals: AirStarDCIsoFB, AirStarDCRotSen, BypassAirStarDCRotSen. All three
  bound above. None unclaimed. The §8 sweep is what confirms there is NO running feedback and
  NO start output for this machine — an absence that binding-direction-only work would have
  passed over silently, and which is the whole substance of Q-C13.
  Plant-scoped control-implying signals unbound anywhere:
    DiscreteOutputs.VSDFaulrReset                          — Q-C14 (BLOCKING)
    HMIControlSignals.GlobalSetAllToAuto                   — Q-C09 (BLOCKING)
    HMIControlSignals.GlobalFTTimeOverwrite / GlobalUPSEnableTimeOverwrite /
      GlobalShutdownCompleteTimeOverwrite / GlobalVSDStartUpTimeOverwrite /
      GlobalVSDAutoSpeedOverwrite / GlobalVSDHandSpeedOverwrite  — Q-C05 (BLOCKING); the last
      three are VSD-specific and land squarely on this instance's settings
    DiscreteInputs.ControlHealthy                          — Q-C03 (BLOCKING)
  E-stop / safety-circuit members: out of scope by hard rule 2 and B-33; not enumerated.

DELTAS
  D1  PLUS — takes its running-forward confirmation from the belt motion sensor, which the
      class declares but never acts on; the behaviour lives wholly in the plant layer
                                                                              [A+B-25+ref]
  D2  PLUS — that motion sensor is individually operator-bypassable, and no substitute
      running signal exists in the IO table for the bypassed case      [A Q-07, io, Q-C13]
  D3  MINUS — no running feedback and no discrete start output exist for this machine, unlike
      every DOL machine in the plant                                                   [io]
  D4  CANDIDATE MINUS — the class's reverse-direction capability (VSD-07) has no recorded
      plant use for this instance, and no reverse output exists                        [io]
  D5  OUTLIER SETTINGS — start-up time 8.0 s (class default 5.0 s) and shutdown time 8.0 s
      (sibling VSD 5.0 s)                                                              [io]
  searched: rung-A delta block for this instance, vsd-motor reference §Class requirement set
  and §Class deltas (incl. its note that the rotation-sensor member is never consumed inside
  the class), the §8 sweep above, and a setting-by-setting comparison against MotorVSDInst3.

OPEN
  Q-C03 (BLOCKING) · Q-C05 (BLOCKING) · Q-C09 (BLOCKING) · Q-C13 (BLOCKING) · Q-C14 (BLOCKING)
  Q-C15
  Carried unresolved: A Q-01, A Q-03, A Q-05, A Q-07, B Q-B1, B Q-B10, B Q-B11.
