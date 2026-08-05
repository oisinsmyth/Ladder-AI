EQUIPMENT SPEC — TomraControlInst1 ("Optical Sorter")
  class: optical-sorter (ref v0-derived)   fed-by: NOT ESTABLISHED [A Q-03]   discharges-to: NOT ESTABLISHED [A Q-03]
  produced 2026-08-04 by /gen-equipment-spec (rung C). Alarms out of scope at this rung.
  CLASS CAVEAT: single-instance class — nothing here can be shown to be class rather than
  instance behaviour (BLOCKING A Q-02).

IO BINDING                                                                         [io]
  run command out          DiscreteOutputs.TomraRun                        exists
  machine ready in         DiscreteInputs.TomraReady                       exists
  machine running in       DiscreteInputs.TomraRunning                     exists
  comms fault in           DiscreteInputs.TomraComFlt                      exists
  isolator in              NONE IN IO TABLE — no TomraIsoFB member exists              Q-C18
  fault reset out          NONE IN IO TABLE — the reset goes to the machine over the data
                           link, not as a discrete output (unlike the filter units)
  data link in / out       NOT IN THE IO TABLE — 8 status words in, 2 control words out,
                           exchanged over the machine's process-data link       BLOCKING Q-C19
  byte-order selector      PlantControl.Test[5]                            exists      Q-C20
  plant run state          PlantControl.Status                             exists
  pre-start complete       PlantControl.PreStartComplete                   exists
  global fault reset       HMIControlSignals.SystemReset                   exists

CONTROL REQUIREMENTS   (complete by construction from the class reference — all 19 instantiated)
  C1  (OS-01) runs on the plant's automatic start command, or on the operator's hand start
      command while taken to hand                                                    [ref]
      NO IO BINDING — see Q-C09.
  C2  (OS-02) does not start while inhibited (isolated / locked off)                 [ref]
      NO SIGNAL EXISTS — every conveyor and motor in the plant has an isolator feedback; this
      machine does not. BLOCKING Q-C18. Candidate delta.
  C3  (OS-03) does not start while faulted, and not while a fail-to-run fault stands [ref]
  C4  (OS-04) does not start until the plant pre-start warning phase has completed
      — from PlantControl.PreStartComplete                                   [ref+B-05+io]
  C5  (OS-05) commanded both over the data link and by a hardwired run signal, both driven
      from the same run decision — hardwired part from DiscreteOutputs.TomraRun
                                                                           [ref+B-28+io]
      the data-link part is unresolved — BLOCKING Q-C19
  C6  (OS-06) condition read both over the data link and from hardwired ready / running /
      comms-fault signals — from DiscreteInputs.TomraReady, TomraRunning, TomraComFlt
                                                                           [ref+B-28+io]
      Candidate sets are clean here: three roles, three plainly-named signals, 1:1. This is
      the ONE machine in this run whose feedback binding is unambiguous.
  C7  (OS-07) the data link is supervised by a liveness indication from the machine; loss for
      the watchdog period latches a communications-lost condition until reset    [ref+set]
      liveness indication rides on the data link — BLOCKING Q-C19
  C8  (OS-08) while the link is healthy the machine counts as running only when link and
      hardwired running agree; while the link is dead the hardwired running signal alone
      decides, and the same fallback applies to the comms-fault indication     [ref+B-22]
      NOTE: this is a genuine fallback behaviour for THIS machine — contrast MotorVSDInst1,
      whose bypassed motion sensor has no fallback at all (Q-C13). Recorded because the two
      are the same shape of problem answered two different ways in the same plant.
  C9  (OS-09) once running for its up-to-speed time, and while it reports itself ready,
      declares itself enabled; withdraws that enable immediately on loss of running
                                                                                [ref+set]
  C10 (OS-10) on a controlled shutdown request the machine stops and declares its shutdown
      complete once its shutdown time has elapsed and it has stopped running    [ref+set]
      NOTE the class delta: this machine's shutdown-complete has no fault escape term, so a
      faulted sorter does NOT release MotorVSDInst3, which is waiting on it (P8). Carried
      from the class reference, not resolved here.                                   [ref]
  C11 (OS-11) fail-to-run fault, latched                                         [ref+set]
  C12 (OS-12) fail-to-stop fault, latched                                        [ref+set]
  C13 (OS-13) any machine-reported fault, plus loss of the data link, raises a latched fault
                                                                            [ref+B-22]
      the machine-reported fault set rides on the data link — BLOCKING Q-C19
  C14 (OS-14) a fault-reset command clears all latched faults and is passed to the machine
      over the data link — reset from HMIControlSignals.SystemReset          [ref+B-15+io]
      the outbound path is on the data link — BLOCKING Q-C19
  C15 (OS-15) running hours totalised — NO IO BINDING                                 [ref]
  C16 (OS-16) operator status indication published — NO IO BINDING                    [ref]
  C17 (OS-17) alarm indication published — OUT OF SCOPE AT THIS RUNG, instantiated not dropped
  C18 (OS-18) the sorting program and the machine's belt on/off delays are operator-settable
                                                                                [ref+set]
      NO IO BINDING. The class reference records that these are declared but not implemented
      (its anomaly A-3) — recorded as a delta, not silently dropped. Q-C21.
  C19 (OS-19) the byte order of the data exchanged with the machine is selectable at plant
      level — from PlantControl.Test[5]                                        [ref+io]
      BLOCKING Q-C20 — a production byte-order selector bound to a member of an array named
      "Test" is either a mis-binding or a commissioning switch left in the running plant.

PLANT INTERLOCKS   (fully enumerated, one relation per line)
  P1  start permitted only while the Ejected Material Conveyor (MotorStarterInst6) is
      enabled                                                                          [A]
  P2  start permitted only while the Residual Material Conveyor (MotorStarterInst7) is
      enabled                                                                          [A]
  P3  on controlled shutdown, hold until the Sorter Conveyor VSD (MotorVSDInst3) reports its
      shutdown complete                                                            [A+B-11]
  P4  commanded to start automatically while the plant run state is running
      — from PlantControl.Status                                                 [B-02+io]
  P5  the automatic controlled-shutdown request is suppressed while this machine is under
      hand intervention                                                              [B-13]
      CONTRADICTED BY THE SOURCE'S OWN RECORD — as-built takes ANOTHER machine's hand state.
      Not resolved. BLOCKING A Q-06 / B Q-B8.
  P6  the resulting run command drives DiscreteOutputs.TomraRun                 [B-28+io]
  P7  REVERSE — this machine's readiness and freedom from faults is a start permissive of
      the Sorter Conveyor VSD (MotorVSDInst3); which fault is meant is BLOCKING Q-C17 [A+B-09]
  P8  REVERSE — the Ejected Material Conveyor holds through shutdown until this machine
      reports its shutdown complete                                                    [A]
  P9  REVERSE — the Residual Material Conveyor holds through shutdown until this machine
      reports its shutdown complete                                                    [A]

SETTINGS
  up-to-speed time       owner: HMI   value: 2.0 s     as-built instance default        [io]
  fail-to-run time       owner: HMI   value: 3.0 s     as-built instance default        [io]
  shutdown time          owner: HMI   value: 2.0 s     as-built instance default        [io]
                         NOTE: the shortest shutdown time of the six machines in this run.
  sorting program        owner: HMI   value: 0         as-built instance default        [io]
                         declared but not implemented per the class reference — Q-C21
  belt on / off delays   owner: HMI   value: not set   declared, one of the two never scaled
                         at all per the class reference — Q-C21
  comms watchdog         owner: engineering   value: 5 s   reference-proposed             [ref]
                         a fixed value inside the class, not a plant setting — recorded as
                         reference-proposed because no plant document states it.

UNCLAIMED IO (§8 sweep, opposite direction)
  Instance-scoped signals: TomraRun, TomraReady, TomraRunning, TomraComFlt. All four bound
  above. None unclaimed. The sweep also confirms two ABSENCES that matter: no isolator input
  (Q-C18) and no discrete fault-reset output, where the filter units have one each.
  Plant-scoped control-implying signals unbound anywhere:
    PlantControl.Test[0..10]     — partially claimed: Test[5] is bound at C19 as a live
                                   byte-order selector. The rest of the array is unbound and
                                   its name gives no basis for deciding what it drives.
                                   BLOCKING Q-C20.
    HMIControlSignals.GlobalSetAllToAuto  — Q-C09 (BLOCKING)
    DiscreteInputs.ControlHealthy         — Q-C03 (BLOCKING); note this class has no
                                   system-healthy requirement at all, so B-33's claim that
                                   the layer asserts health for every machine cannot hold
                                   literally here — B Q-B12.
  E-stop / safety-circuit members: out of scope by hard rule 2 and B-33; not enumerated.

DELTAS
  D1  PLUS — a third-party machine exchanging process data over a link in addition to its
      hardwired signals; the link's content and whether it is in scope are unresolved
                                                                          [A, A Q-08, Q-C19]
  D2  CANDIDATE DEFECT — its own automatic shutdown is gated on another machine's hand state
                                                                          [A Q-06, B Q-B8]
  D3  CLASS-SCOPE CAVEAT — single instance of its class; class and instance are
      indistinguishable                                                            [A Q-02]
  D4  MINUS — no system-healthy condition exists on this class, contradicting B-33  [ref, B Q-B12]
  D5  MINUS — no isolator signal exists                                          [io, Q-C18]
  D6  CHANGED — its shutdown-complete has no fault escape term, so a faulted sorter does not
      release the machine waiting on it                                              [ref]
  D7  DECLARED-BUT-UNIMPLEMENTED — program selection and belt delays are settings the machine
      offers and the control never uses                                       [ref, Q-C21]
  searched: rung-A delta block for this instance, optical-sorter reference §Class requirement
  set, §Class deltas AND §Observed as-built anomalies (which is what surfaced D6 and D7), the
  §8 sweep above, and the instance's own as-built setting values.

OPEN
  Q-C03 (BLOCKING) · Q-C09 (BLOCKING) · Q-C17 (BLOCKING) · Q-C18 (BLOCKING) · Q-C19 (BLOCKING)
  Q-C20 (BLOCKING) · Q-C21
  Carried unresolved: A Q-01, A Q-02, A Q-03, A Q-06, A Q-08, B Q-B1, B Q-B8, B Q-B12.
