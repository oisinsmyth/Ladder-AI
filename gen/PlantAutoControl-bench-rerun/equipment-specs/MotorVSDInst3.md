EQUIPMENT SPEC — MotorVSDInst3 ("Sorter Conveyor VSD Unit")
  class: vsd-motor (ref v0-derived)   fed-by: NOT ESTABLISHED [A Q-03]   discharges-to: NOT ESTABLISHED [A Q-03]
  produced 2026-08-04 by /gen-equipment-spec (rung C). Alarms out of scope at this rung.

IO BINDING                                                                         [io]
  isolator in              DiscreteInputs.OSCIsoFB                         exists
  motion sensor in         DiscreteInputs.OSCRotSen                        exists      Q-C16
  motion-sensor bypass     NONE IN IO TABLE — no BypassOSCRotSen member exists         Q-C16
  running feedback in      NONE IN IO TABLE — no OSCRunning member exists              Q-C16
  start command out        NONE IN IO TABLE — no OSCStart member exists
                           (consistent with B: VSDs are commanded over the drive interface)
  drive fault reset out    DiscreteOutputs.VSDFaulrReset                   exists      Q-C14
  sorter ready in          DiscreteInputs.TomraReady                       exists   (for P1)
  sorter comms fault in    DiscreteInputs.TomraComFlt                      exists   (for P2, Q-C17)
  plant run state          PlantControl.Status                             exists
  pre-start complete       PlantControl.PreStartComplete                   exists
  global fault reset       HMIControlSignals.SystemReset                   exists

  BINDING BASIS for the OSC prefix: this instance is the "Sorter Conveyor VSD Unit"; the plant
  uses OSEC / OSRC / MotorStarterInst5 for the other optical-sorter-area machines and Tomra* for the
  sorter itself, leaving OSC as this conveyor. Candidate set of one after that basis. Unlike
  MotorVSDInst1, NO requirement text anywhere names an OSC signal, so this binding rests on a
  naming convention alone — weaker evidence, recorded as such.

CONTROL REQUIREMENTS   (complete by construction from the class reference — all 20 instantiated)
  C1  (VSD-01) runs on the plant's automatic start command, or on the operator's hand start
      command while taken to hand                                                    [ref]
      NO IO BINDING — see Q-C09.
  C2  (VSD-02) does not start while inhibited — from DiscreteInputs.OSCIsoFB
                                                                          [ref+B-26+io]
  C3  (VSD-03) does not start while faulted, and not while a fail-to-run fault stands [ref]
  C4  (VSD-04) does not start unless reported system-healthy — Q-C03                 [ref]
  C5  (VSD-05) does not start until the plant pre-start warning phase has completed
      — from PlantControl.PreStartComplete                                   [ref+B-05+io]
  C6  (VSD-06) runs to an automatic speed setpoint in auto and an operator setpoint in hand,
      floored at a minimum running speed and scaled against rated maximum speed  [ref+set]
      NO IO BINDING — over the drive interface.
  C7  (VSD-07) can run forward or reverse; running confirmed from a forward or reverse
      running feedback                                                               [ref]
      NO RUNNING FEEDBACK EXISTS IN THE IO TABLE. See C17 and BLOCKING Q-C16.
  C8  (VSD-08) once running and confirmed running for its up-to-speed time, declares itself
      enabled                                                                   [ref+set]
  C9  (VSD-09) on a controlled shutdown request the motor stops and declares its shutdown
      complete once its shutdown time has elapsed and running feedback has cleared    [ref]
      running feedback per C17 — BLOCKING Q-C16
  C10 (VSD-10) declares shutdown complete immediately if faulted                      [ref]
  C11 (VSD-11) run-confirmation supervision is not armed until the start-up time has elapsed
      and is re-armed on a direction change                                      [ref+set]
  C12 (VSD-12) fail-to-run fault after the allowance, latched                    [ref+set]
  C13 (VSD-13) fail-to-stop fault, latched                                       [ref+set]
  C14 (VSD-14) a drive error raises a latched fault — NO IO BINDING (drive interface)  [ref]
  C15 (VSD-15) a fault-reset command clears all latched faults
      — from HMIControlSignals.SystemReset                                   [ref+B-15+io]
      Second candidate for the reset path to the drive: DiscreteOutputs.VSDFaulrReset,
      unbound and unable to serve three VSD instances distinctly — BLOCKING Q-C14.
  C16 (VSD-16) the drive's own condition is brought back for the operator             [ref]
      NO IO BINDING — over the drive interface.
  C17 (VSD-17) a motion/rotation sensor input exists on this class; its use is an
      instance-level decision                                                        [ref]
      UNRESOLVED FOR THIS INSTANCE. DiscreteInputs.OSCRotSen exists and is bound to nothing;
      no running feedback exists to pair with it; and unlike its sibling this machine has no
      bypass signal. The candidate set for "what confirms this conveyor is running" is
      {OSCRotSen, the drive's own running status over the interface} — two members, no
      documentary basis for either. BLOCKING Q-C16.
  C18 (VSD-18) running hours totalised — NO IO BINDING                                [ref]
  C19 (VSD-19) operator status indication published — NO IO BINDING                   [ref]
  C20 (VSD-20) alarm indication published — OUT OF SCOPE AT THIS RUNG, instantiated not dropped

PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  start permitted only while the Optical Sorter (TomraControlInst1) reports itself ready
      — from DiscreteInputs.TomraReady                                       [A+B-09+io]
  P2  start permitted only while the Optical Sorter is free of faults               [A+B-09]
      BINDING AMBIGUOUS — candidate set {DiscreteInputs.TomraComFlt (the raw comms-fault
      input), the sorter's own aggregated fault status}. Two members, no documentary basis:
      the source says "ready and not faulted" without saying which fault. BLOCKING Q-C17.
      P1 and P2 stay two relations (§5 — different signals, and under one candidate reading
      P2 is not a signal at all but an aggregate).
  P3  start permitted only while the Ejected Material Conveyor (MotorStarterInst6) is
      enabled                                                                          [A]
  P4  start permitted only while the Residual Material Conveyor (MotorStarterInst7) is
      enabled                                                                          [A]
  P5  on controlled shutdown, hold until the Equipment Control System (ECSControlInst1)
      reports its shutdown complete                                              [A+B-11]
  P6  commanded to start automatically while the plant run state is running
      — from PlantControl.Status                                                 [B-02+io]
  P7  the automatic controlled-shutdown request is suppressed while this machine is under
      hand intervention                                                              [B-13]
      CONTRADICTED BY THE SOURCE'S OWN RECORD — the as-built takes ANOTHER machine's hand
      state here, and the source asks for a ruling. Not resolved. BLOCKING A Q-06 / B Q-B8.
  P8  the resulting run command is delivered over the drive interface; there is no discrete
      start output for this machine                                              [B-23+io]
  P9  REVERSE — this machine's enable is a start permissive of FilterUnitInst2            [A]
  P10 REVERSE — this machine's enable is a start permissive of FilterUnitInst3            [A]
  P11 REVERSE — this machine's enable is a start permissive of the Equipment Control System
      (ECSControlInst1)                                                                [A]
  P12 REVERSE — the Optical Sorter holds through shutdown until this machine reports its
      shutdown complete                                                                [A]

SETTINGS
  automatic speed        owner: HMI   value: 100.0 %   as-built instance default        [io]
  hand speed             owner: HMI   value: 20.0 %    as-built instance default        [io]
  rated maximum speed    owner: engineering   value: 1500 rpm                           [io]
  drive start-up time    owner: HMI   value: 5.0 s     as-built instance default        [io]
  up-to-speed time       owner: HMI   value: 2.0 s     as-built instance default        [io]
  fail-to-run time       owner: HMI   value: 5.0 s     as-built instance default        [io]
  shutdown time          owner: HMI   value: 5.0 s     as-built instance default        [io]

UNCLAIMED IO (§8 sweep, opposite direction)
  Instance-scoped signals: OSCIsoFB (bound at C2), OSCRotSen (UNCLAIMED — see below).
    DiscreteInputs.OSCRotSen — UNCLAIMED. A rotation sensor exists for this conveyor, but no
    requirement in this spec binds it, no bypass signal exists for it, and no running feedback
    exists to complement it. Every other bypassable conveyor has the triplet
    {sensor, bypass, running}; this machine has the sensor alone. This is a control-implying
    signal left unbound and is therefore a BLOCKING question and a candidate delta, not a
    silent omission — Q-C16. This finding exists ONLY because the sweep runs in the opposite
    direction; binding requirements to signals would never have reached it.
  Plant-scoped control-implying signals unbound anywhere: VSDFaulrReset (Q-C14),
  GlobalSetAllToAuto (Q-C09), the six Global*Overwrite settings (Q-C05), ControlHealthy (Q-C03).
  E-stop / safety-circuit members: out of scope by hard rule 2 and B-33; not enumerated.

DELTAS
  D1  PLUS — carries a readiness-and-health permissive on a different machine (the Optical
      Sorter), not merely that machine's up-to-speed enable                       [A+B-09]
  D2  CANDIDATE DEFECT — its own automatic shutdown is gated on another machine's hand state;
      owner ruling requested and not given                                 [A Q-06, B Q-B8]
  D3  ASYMMETRY — three of its four start permissives have no reciprocal hold  [A, A Q-11]
  D4  UNCLAIMED SENSOR — OSCRotSen exists, is bound to nothing, and has neither a bypass nor
      a running feedback beside it                                                [io, Q-C16]
  D5  MINUS — no running feedback and no discrete start output exist                   [io]
  D6  CANDIDATE MINUS — the class's reverse-direction capability has no recorded plant use
      for this instance                                                                [io]
  searched: rung-A delta block for this instance, vsd-motor reference §Class requirement set
  and §Class deltas, the §8 sweep above (which surfaced D4), and a setting-by-setting
  comparison against MotorVSDInst1.

OPEN
  Q-C03 (BLOCKING) · Q-C05 (BLOCKING) · Q-C09 (BLOCKING) · Q-C14 (BLOCKING) · Q-C16 (BLOCKING)
  Q-C17 (BLOCKING)
  Carried unresolved: A Q-01, A Q-03, A Q-06, A Q-11, B Q-B1, B Q-B8, B Q-B11.
