# ir/PlantAutoControl-bench — Green given-boundary (S6-Killer-Plan)

Sanitized (invented-name) JOB9002 station_2 global DBs that form the **fixed IO + data boundary**
handed to the Phase-2 Candidate (Level-1 scope, `docs/notes/S6-Killer-Plan.md`). The regenerate
target (`PlantAutoControl`) is **not** here — it is sealed under
`docs/evidence/PlantAutoControl-answerkey/` and the Candidate is never pointed at it.

Blocks (all sanitized via `sanitization/<name>.map.json`, leakage-gated):
`Control` (→PlantControl), `HMIControlSignals` (→HMIControlSignals), `Input` (→DiscreteInputs),
`Output` (→DiscreteOutputs), `PLC` (→InterlockData), `Timings` (→ProcessTimings).

Phase-2 boundary now present (added 2026-07-20): the 8 equipment FB types (`MotorDOL`, `MotorVSDSystem`,
`MotorFwdRevSystem`, `AirStar`, `TomraControlSystem`, `FilterUnitSystem`, `EquipmentControlSystem`, `ShredderControlSystem`) and the
20 instance DBs (`MotorStarterInst7`, `MotorVSDInst2`, … `TomraControlInst1`) — sanitized via
`sanitization/<name>.map.json`, leakage-gated. These are the given library blocks the Candidate
reuses and compiles against; they are NOT regeneration targets.
