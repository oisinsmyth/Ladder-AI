# ir/PlantAutoControl-bench — Green given-boundary (S6-Killer-Plan)

Sanitized (invented-name) JOB9002 station_2 global DBs that form the **fixed IO + data boundary**
handed to the Phase-2 Candidate (Level-1 scope, `docs/notes/S6-Killer-Plan.md`). The regenerate
target (`PlantAutoControl`) is **not** here — it is sealed under
`docs/evidence/PlantAutoControl-answerkey/` and the Candidate is never pointed at it.

Blocks (all sanitized via `sanitization/<name>.map.json`, leakage-gated):
`Control` (→PlantControl), `HMIControlSignals` (→HMIControlSignals), `Input` (→DiscreteInputs),
`Output` (→DiscreteOutputs), `PLC` (→InterlockData), `Timings` (→ProcessTimings).

Deferred to Phase 2 (compile): the 8 equipment FB types + 20 instance DBs (given library blocks).
