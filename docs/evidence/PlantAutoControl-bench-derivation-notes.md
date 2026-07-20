# PlantAutoControl-bench derivation notes — abstraction-judgment log

Phase 1 (Examiner) of the S6-Killer-Plan answer-key validation
(`docs/notes/S6-Killer-Plan.md`). This is the honest, specific record of every place where deriving
`gen/PlantAutoControl-bench/requirements.md` from the sealed answer key
(`docs/evidence/PlantAutoControl-answerkey/PlantAutoControl.ir`) forced a "requirement vs implementation
detail?" call. It pre-seeds Phase-3 grading. The register is **reverse-derived with no independent
source**, so these calls — not source-derivation — are the whole defense against leaking HOW.

## The governing structural fact

`PlantAutoControl` is 20 networks, one per equipment, and each network is a near-verbatim instance
of **one repeated pattern**: map field feedback → assert healthy → inhibit on isolator → gate on
pre-start-complete → compute an auto-start permissive from plant `Status` plus a named neighbour's
enable → compute the inverse shutdown request → route the global fault reset → drive the physical
output → call the equipment FB. The single biggest abstraction decision was therefore:

**Decision 1 — pattern-level requirements + one dependency table, NOT 20 near-identical per-network
REQs.** Writing "REQ: Link conveyor auto-starts when…", "REQ: Overband magnet auto-starts when…"
×20 would have (a) effectively enumerated the networks one-for-one (a structural tell — the very
"one REQ per network" shape the anti-leakage rule warns against) and (b) buried the genuine intent.
Instead the common contract is stated once (REQ-001…010, 022) as plant behaviour, and the
per-machine *differences that matter* — the interlock links — are captured as process facts in the
equipment interlock table. Rationale: a controls engineer specifying this plant would write "each
conveyor starts only when the one receiving its material is running" once, then a dependency
list — not 20 paragraphs. The dependency graph itself is WHAT (process interlock ordering), so it
belongs in the register; the ladder that encodes it is HOW and stays out.

## Requirement-vs-implementation calls

- **`Status >= 1` / `Status = -1` → "running state" / "controlled-shutdown state" (REQ-002/003).**
  The literal integer comparisons are how the block reads the plant word. I abstracted to the plant
  *states* they denote and put the numeric encoding only in a REQ-002 note plus Q-01. Kept as
  behaviour ("while the plant is running…"), not "when Status ≥ 1". The encoding is genuinely
  inferred, hence the open question rather than an asserted fact.

- **The `AutoStartSignal` OR/AND expression → REQ-004 (downstream-ready) + REQ-003 (cascade hold).**
  The expression `(Status>=1 OR (Status=-1 AND NOT neighbour.ShutdownComplete)) AND permissive` is
  pure structure. I decomposed it into the two behaviours it encodes: a start interlock (receiving
  equipment must be enabled first) and a staged shutdown (hold until the neighbour has finished).
  Both are things a process spec states. The `NOT … ShutdownComplete` mechanic is not named; only
  "keep running until X has completed its shutdown" is.

- **`Shutdown := NOT(core) AND NOT HandIntervention AND Status=-1` → REQ-003 + REQ-006.** The
  "NOT the run condition" inverse is implementation. The extractable intent is (a) shutdown request
  only exists in the shutdown state, folded into REQ-003, and (b) hand intervention suppresses it,
  REQ-006. The Boolean inversion itself is not a requirement.

- **`SystemHealthy := TRUE` on every machine → REQ-022 (out-of-scope).** A constant-true
  assignment is meaningless as a "requirement" on its face. The genuine WHAT is a *scope boundary*:
  this layer does not do health/safety gating — that lives in the hardwired circuit and other
  blocks. Recording it as out-of-scope stops the Candidate (and reviewers) expecting health logic
  here. This is the clearest example of a literal instruction that is NOT a requirement but whose
  *absence of meaning* is itself a requirement-level fact.

- **`RunningFB := field AND RotSen OR field AND Bypass` → REQ-011.** The OR-of-two-ANDs with a
  shared term is structure. Intent: belt motion must be independently confirmed, and the check is
  operator-bypassable per belt. I named the bypass as an HMI feature (WHAT) and left the Boolean
  shape out. Also recorded which machines *lack* a rotation sensor (magnet, drums, spreaders,
  filters) — a genuine per-equipment difference, not structure.

- **Discharge-VSD S/R of `RunningFwdFB` from the rotation sensor → REQ-012.** The set/reset coil
  pair is implementation; the WHAT is "the VSD uses its rotation sensor as its running-confirmation,
  unless bypassed." Kept the behaviour, dropped the latch mechanism (I did note in the C-113 table
  that a latch exists, because *statefulness* is a legitimate classification fact, not a HOW leak —
  I名 no coil).

- **Feed-conveyor `Reverse` S/R + the fwd/rev `AutoStartSignal` branches → REQ-016.** Hardest
  behavioural extraction. The set/reset on `NOT Shredder.UPSEnable` / `Shredder.UPSEnable`, and the
  two-branch auto-start (fwd requires not-reverse + shredder-ready; rev requires reverse +
  general-enable), together encode: reverse to back feed off when the shredder can't take it,
  forward when it can, change direction only while already running in auto and not in hand. I stated
  that behaviour and flagged the *purpose* (anti-blockage) as inferred (Q-05). Judgment call: the
  "not in hand, already running" guard is behaviour (a real operating constraint), so it stayed;
  the S/R coils did not.

- **Auto-pre-start one-shot (`SCOIL`/`RCOIL AutoPreStart` + `PositiveEdgeArray[4]`) → REQ-015.**
  The edge-memory array element and the set/reset coils are textbook HOW. The WHAT is a one-shot
  request: pre-start is asked for once when auto-start is first requested, cleared when the
  pre-start output fires. I named `AutoPreStart` / `RunPreStart` (boundary members) and the one-shot
  behaviour; I did **not** name the edge array or the set/reset. The C-113 table records it as an
  edge-triggered one-shot (statefulness = classification fact, allowed).

- **`MOVE(NormalFanStartTime) => EnableUPSTime` → REQ-020 (timing).** MOVE is an instruction —
  banned from text. But the *value it moves* is a genuine process parameter (a 10 s fan spin-up
  before the fan counts as "enabling" downstream). I read 10.0 from `ProcessTimings` and stated it
  as a requirement value, exactly like test-project001's settings table. I explicitly checked that
  **only** `NormalFanStartTime` is referenced by this block — the other `ProcessTimings` members
  (AntiCondensationTime, PreStartRunTime, Drums/Magnets/GeneralDelay, PreStartTimeout, the Global*
  overrides) are NOT touched here, so I did **not** invent requirements for them; they are other
  blocks' data. This is a place where over-reading the boundary DB would have manufactured
  false requirements.

- **`DiscreteOutputs.*Start := Inst.IO.Run` → REQ-008.** Borderline. A direct coil assignment is
  structure, but "the computed run command drives the physical start output" is the observable
  boundary behaviour a spec states. Kept it generic (one REQ over all machines), not per-network.

- **Optical-sorter CALL word mapping (`inputWord0…7`, `OutputWord0/1` ← `Tag_45…54`) → dropped to
  Q-06.** This is FB-call plumbing over a given comms buffer. It is not a derivable process
  requirement (I cannot say what the words mean), and the tags are legacy comms residue, not
  equipment I can name. I recorded the *existence* of the data-word interface as a boundary fact and
  raised whether reconstructing it is even in scope — rather than fabricate requirement text for it.

- **Cross-wired hand-intervention references (N10 gates its shutdown on the sorter's hand; N11 on
  the ejected-conveyor's hand) → Q-07, NOT a requirement.** These two networks reference a
  *different* machine's `HandIntervention` than their own. Encoding that literally would be encoding
  a likely as-built copy/paste defect as if it were intent. Per the S6 convention that
  defect-documented blocks are acceptable, I wrote the *intended* per-machine behaviour (REQ-006)
  and flagged the deviation as a possible defect needing an owner ruling — I did not launder the
  quirk into a requirement, and I did not silently "fix" it either.

## Things I deliberately did NOT put in the register

- Any network number, coil/contact/SCOIL/RCOIL/MOVE/CALL/compare mnemonic, `PositiveEdgeArray`
  index, wire/UId/compileunit artifact, or "branch/rail/step" structure — all HOW.
- The master start/stop sequencer, the pre-start timer, the E-stop/safety circuit, and the
  computation of `PlantControl.Status` — these are other blocks / hardwired, explicitly scoped out
  (provenance + REQ-022) so the Candidate does not try to build them.
- The equipment FB internals (shredder overcurrent/reversal, motor-starter sequencing, etc.) — the
  block only *calls* these; their behaviour is not this block's requirement.

## Safety check

Every network was read for F-block / safety-program / E-stop references. `PlantAutoControl`
references only standard equipment FB instances and global-DB members; it does **not** touch any of
the E-stop feedback members present in the given `DiscreteInputs`/`PlantControl` DBs. Hard rule 2
not triggered. No safety content entered the register.

## Deferred-dependency check

I did **not** need any deferred Phase-2 export to derive intent. The equipment interface UDT member
names (`UPSEnable`, `Run`, `ShutdownComplete`, `AutoStartSignal`, `InhibitMotor`, `RunningFB`,
`Reverse`, `EnableUPSTime`, `HandIntervention`, …) are self-describing at the boundary, and this
block's intent is the interlock/mapping *between* those members, not their internal meaning. If
Phase-3 grading needs to confirm the interface member semantics, the 8 FB types and 20 instance DBs
are the exports to pull — but the register stands without them.
