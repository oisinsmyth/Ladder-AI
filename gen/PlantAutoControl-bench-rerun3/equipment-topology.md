# PlantAutoControl-bench-rerun3 — Equipment topology and per-equipment interlocks (rung A)

Produced by `/gen-pid-analysis` (rung A of the structured spec pipeline A → B → C → D).
**Process relations only** — no signals, no IO addresses, no tag names, no booleans, no interface
members. Signals enter at rung C; booleans/interfaces at rung D.

## Provenance

- **Produced:** 2026-08-05, `gen-pid-analysis` (skill-driven).
- **Topology source (the "layout"):** `gen/PlantAutoControl-bench/requirements.md` §*Equipment inventory*
  → the table **"Equipment instances and interlock links (given Phase-2 boundary)"** (rows 1–20)
  plus its column legend immediately above it.
  SHA-256 `c0de9b797b75085b40c167ad9e8b672ae25d48b0e74ed4a4f4f9e157ad46ecba`.
- **Deliberate source fence.** That same file's **REQ prose is the rung-B source** and was *not*
  consumed here. Rung A used only the inventory table, its legend, and the class references. Where
  the table alone does not settle something, it is a `Q-nn` below — not a fact borrowed from the REQ
  text. (Recorded because one document serving two rungs makes leakage easy and invisible.)
- **Class references used:**
  - `references/FilterUnitSystem/reference.md` — SHA-256 `277c509404475f35cc4e02bbe14c01ca7110d2e820b6f878e8b118563b4b9ded` (v0-derived)
  - `references/vsd-motor/reference.md` — SHA-256 `77a3b52d2c7e1d1f87821665581835ba4acf438f5a87b6edf8030963fd1386d6` (v0-derived)
  - `references/optical-sorter/reference.md` — SHA-256 `63884354bc8537a3baecf1868202caba5169c7eeaeea4ca587b86cf9d594b6b1` (v0-derived)
  - Each reference's **§As-built provenance** section was **not read** — it is rung-C material by its
    own instruction. The **§Class requirement set**, **§Class deltas** and (for `optical-sorter`)
    **§Observed as-built anomalies** headings were used.
  - **All three references carry a header disclaiming that they are engineering standards** — they
    are reverse-derived from the as-built FB code. See **Q-A2 (blocking)**: completeness-by-
    construction, the property this rung exists to deliver, is *not* actually delivered here.
- **Scope:** six instances — `FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1`,
  `MotorVSDInst1`, `MotorVSDInst3`, `TomraControlInst1`. Neighbouring machines are *referenced* by
  the relations below but are not specified here.
- **Safety:** no F-block, F-runtime, safety-program or E-stop content was read or referenced. Hard
  rule 2 was not triggered.

## Convention statement (applied plant-wide, without exception)

Taken verbatim from the source table's own legend, not invented here:

1. **Start permissive ("starts after X").** A machine is permitted to start automatically only while
   the neighbour whose up-to-speed **enable** is named has declared that enable. The receiving
   machine is up before the machine feeding it starts.
2. **Shutdown hold ("holds through shutdown until X").** On a plant controlled shutdown a machine
   keeps running until the named neighbour has declared its **shutdown complete**, then stops.
3. **Enable consumer ("serves").** A machine's own enable exists to permit the machine(s) whose
   start permissive names it. Derived by sweeping all 20 rows' start-permissive column, not assumed.
4. **Every relation is one line.** Composite source cells are split; nothing is joined by `/`, `&`,
   `+` or a comma in what is written below.

Applied without exception below. Where an instance departs from 1–3, that departure is a **delta**,
recorded in the instance's `deltas` field — never smoothed over to keep the convention tidy.

## Ambiguous-separator dispositions (FI-37)

| Source cell | Disposition | Why |
|---|---|---|
| `Fans-shutdown-ready / Air-separator VSD shut down` (rows 3, 4) | **Split-and-retain** — two relations, both kept | Both sides are *conditions* over things the source establishes (a plant-level fans-shutdown-ready condition named in the source's own signal table; a neighbour's shutdown-complete defined by the legend). Neither side asserts a signal/device/channel that is not established, so the carve-out does not apply and dropping either side is not permitted. **Their combination (AND vs OR) is a separate, unsettled question → Q-A1 (blocking).** |
| `Fans-shutdown-ready / Shredder shut down` (row 19) | **Split-and-retain** — two relations, both kept | Same reasoning as above. Combination → Q-A1. |
| `Optical Sorter ready & not faulted + Ejected & Residual conveyors enabled` (row 10) | **Full enumeration** — four relations | `&` and `+` are stated conjunctions, not ambiguous separators. Enumerated rather than compacted; compaction is rung D's problem. |
| `Overband Magnet + ECS enabled` (row 6) | **Full enumeration** — two relations | As above. |
| `Ejected & Residual conveyors enabled` (row 11) | **Full enumeration** — two relations | As above. |
| `Discharge-Conveyor VSD + both Dust Filters enabled (or Discharge-Conveyor VSD + third-party link-out)` (row 5, the machine the dust filters serve) | **Explicit disjunction, retained as two alternative permissive sets** | The source writes the word "or". No reading was required. Consequence recorded as a delta on `FilterUnitInst2`/`FilterUnitInst3`. |

---

## FilterUnitInst2 — Dust Filter Unit 1 : FilterUnitSystem

```
FilterUnitInst2 (Dust Filter Unit 1) : FilterUnitSystem
  fed-by        : not-stated-by-source — the source is an equipment/interlock inventory, not a
                  P&ID; no material- or air-path is given. See Q-A4 (non-blocking).
  discharges-to : not-stated-by-source — as above, Q-A4.
  serves        : Air-Separator VSD (this unit's enable is named in that machine's start permissive)
  interlock     : start requires the Sorter Conveyor VSD enabled
  interlock     : controlled shutdown holds until the plant fans-shutdown-ready condition is present
  interlock     : controlled shutdown holds until the Air-Separator VSD reports shutdown complete
  interlock     : the Air-Separator VSD may not start until this unit is enabled (reciprocal of
                  `serves`, on the primary of that machine's two permissive paths only)
  deltas        : PLUS — the machine this unit serves has an alternative start path that does NOT
                  require this unit's enable (Discharge-Conveyor VSD + third-party link-out). The
                  unit's enable is therefore a bypassable permissive, not an absolute one.
  deltas        : MINUS — no reciprocal shutdown-hold: sweeping all 20 rows' shutdown-hold column,
                  no machine holds its shutdown on this unit's shutdown-complete. The unit is
                  terminal in the shutdown chain while being non-terminal in the start chain.
  deltas        : MINUS — its start permissive (Sorter Conveyor VSD) is not the machine it serves
                  (Air-Separator VSD); the start chain through this unit is not a single line.
  deltas        : none-further-found — searched: source table rows 3 and 5, full 20-row sweep of the
                  start-permissive and shutdown-hold columns, extra-gate column ("fan start time"),
                  `FilterUnitSystem` reference §Class requirement set (FU-01..FU-17) and §Class deltas.
                  NOT visible at this rung by construction: any delta carried only by an
                  operator/bypass or IO signal — re-hunt at rung C (`gen-equipment-spec` §8).
  identical-to  : FilterUnitInst3 — every source column is identical for the two dust filter units;
                  no distinguishing delta exists at this rung. (Stated so rung D's grouping decision
                  is made on a recorded fact, not on a resemblance.)
  provenance    : relations <- source table row 3 + reverse sweep of rows 1–20;
                  class reqs <- FilterUnitSystem reference v0-derived (FU-01..FU-17, inherited whole);
                  extra gate "fan start time" <- row 3 extra-gate column (a class timing, FU-07).
```

## FilterUnitInst3 — Dust Filter Unit 2 : FilterUnitSystem

```
FilterUnitInst3 (Dust Filter Unit 2) : FilterUnitSystem
  fed-by        : not-stated-by-source — Q-A4.
  discharges-to : not-stated-by-source — Q-A4.
  serves        : Air-Separator VSD (this unit's enable is named in that machine's start permissive)
  interlock     : start requires the Sorter Conveyor VSD enabled
  interlock     : controlled shutdown holds until the plant fans-shutdown-ready condition is present
  interlock     : controlled shutdown holds until the Air-Separator VSD reports shutdown complete
  interlock     : the Air-Separator VSD may not start until this unit is enabled (reciprocal of
                  `serves`, on the primary of that machine's two permissive paths only)
  deltas        : PLUS — the machine this unit serves has an alternative start path that does NOT
                  require this unit's enable (Discharge-Conveyor VSD + third-party link-out).
  deltas        : MINUS — no reciprocal shutdown-hold; no machine holds its shutdown on this unit's
                  shutdown-complete (full 20-row column sweep).
  deltas        : MINUS — its start permissive (Sorter Conveyor VSD) is not the machine it serves.
  deltas        : none-further-found — searched: source table rows 4 and 5, full 20-row sweep of the
                  start-permissive and shutdown-hold columns, extra-gate column, `FilterUnitSystem`
                  reference §Class requirement set and §Class deltas. Operator/bypass-carried deltas
                  are invisible at this rung — re-hunt at rung C §8.
  identical-to  : FilterUnitInst2 — see that entry.
  provenance    : relations <- source table row 4 + reverse sweep of rows 1–20;
                  class reqs <- FilterUnitSystem reference v0-derived (FU-01..FU-17).
```

## FilterUnitInst1 — Cyclone Filter Unit : FilterUnitSystem

```
FilterUnitInst1 (Cyclone Filter Unit) : FilterUnitSystem
  fed-by        : not-stated-by-source — Q-A4.
  discharges-to : not-stated-by-source — Q-A4.
  serves        : NOBODY — sweeping all 20 rows' start-permissive column, no machine's start
                  permissive names this unit's enable. Recorded as a delta, not smoothed.
  interlock     : no neighbour start permissive — the source states "(runs with plant)": this unit
                  starts on the plant run state alone, with no upstream/downstream machine gate.
  interlock     : controlled shutdown holds until the plant fans-shutdown-ready condition is present
  interlock     : controlled shutdown holds until the Shredder reports shutdown complete
  deltas        : MINUS — no neighbour start permissive at all (convention rule 1 does not apply to
                  this instance). It is the only in-scope machine with none.
  deltas        : MINUS — its enable permits no machine. The class requirement that the enable is
                  "what permits the machine it serves to start" (FilterUnitSystem FU-07) has **no
                  consumer** for this instance: the enable is produced and consumed by nothing in
                  the given topology. Either a machine's permissive is missing from the source, or
                  this unit genuinely serves nothing — the source does not say which. See Q-A7.
  deltas        : MINUS — no machine holds its shutdown on this unit's shutdown-complete.
  deltas        : ODD-PAIRING — its shutdown hold names the **Shredder**, a machine in the material
                  train, whereas the two dust filters (same class, same extra gate) hold on the
                  Air-Separator VSD. The source gives no reason for the different partner; recorded
                  rather than normalised.
  deltas        : none-further-found — searched: source table row 19, full 20-row sweep of both
                  interlock columns, extra-gate column ("fan start time"), `FilterUnitSystem` reference
                  §Class requirement set and §Class deltas. Operator/bypass-carried deltas invisible
                  at this rung — re-hunt at rung C §8.
  provenance    : relations <- source table row 19 + reverse sweep of rows 1–20;
                  class reqs <- FilterUnitSystem reference v0-derived (FU-01..FU-17).
```

## MotorVSDInst1 — Discharge Conveyor VSD : vsd-motor

```
MotorVSDInst1 (Discharge Conveyor VSD) : vsd-motor
  fed-by        : not-stated-by-source — Q-A4.
  discharges-to : not-stated-by-source — Q-A4.
  serves        : Air-Separator VSD (named in both of that machine's alternative start permissives)
  interlock     : start requires the Overband Magnet enabled
  interlock     : start requires the Equipment Control System enabled
  interlock     : controlled shutdown holds until the Air-Separator VSD reports shutdown complete
  interlock     : the Overband Magnet holds its controlled shutdown until this machine reports
                  shutdown complete (reciprocal, source row 8)
  interlock     : the Equipment Control System holds its controlled shutdown until this machine
                  reports shutdown complete (reciprocal, source row 9)
  interlock     : the Air-Separator VSD may not start until this machine is enabled (reciprocal of
                  `serves`; present on BOTH of that machine's permissive paths, so unlike the dust
                  filters this permissive is not bypassable)
  deltas        : PLUS — this instance takes its running confirmation from a motion/rotation sensor
                  rather than from the drive's run feedback alone (source row 6 extra-gate column:
                  "rotation-sensor running FB"). Per the `vsd-motor` reference (VSD-17 and its
                  §Class deltas note), the class declares a rotation-sensor input but never consumes
                  it inside the class — so any use of it is in the CALLING layer and is by the
                  reference's own instruction an **instance** delta, not class behaviour. This is
                  the only in-scope VSD with the extra gate stated.
  deltas        : none-further-found — searched: source table row 6, full 20-row sweep of both
                  interlock columns, extra-gate column, `vsd-motor` reference §Class requirement set
                  (VSD-01..VSD-20) and §Class deltas. Whether the sensor is operator-bypassable is
                  NOT visible at this rung (it would be carried by an operator signal) — re-hunt at
                  rung C §8.
  provenance    : relations <- source table row 6 + reverse sweep of rows 1–20;
                  class reqs <- vsd-motor reference v0-derived (VSD-01..VSD-20, inherited whole);
                  rotation-sensor delta <- row 6 extra-gate + vsd-motor reference VSD-17 note.
```

## MotorVSDInst3 — Sorter Conveyor VSD : vsd-motor

```
MotorVSDInst3 (Sorter Conveyor VSD) : vsd-motor
  fed-by        : not-stated-by-source — Q-A4.
  discharges-to : not-stated-by-source — Q-A4.
  serves        : Dust Filter Unit 1 (FilterUnitInst2)
  serves        : Dust Filter Unit 2 (FilterUnitInst3)
  serves        : Equipment Control System (ECSControlInst1)
  interlock     : start requires the Optical Sorter to report itself ready
  interlock     : start requires the Optical Sorter to be not faulted
  interlock     : start requires the Ejected Material Conveyor enabled
  interlock     : start requires the Residual Material Conveyor enabled
  interlock     : controlled shutdown holds until the Equipment Control System reports shutdown
                  complete
  interlock     : the Optical Sorter holds its controlled shutdown until this machine reports
                  shutdown complete (reciprocal, source row 11)
  interlock     : Dust Filter Unit 1 may not start until this machine is enabled (reciprocal)
  interlock     : Dust Filter Unit 2 may not start until this machine is enabled (reciprocal)
  interlock     : the Equipment Control System may not start until this machine is enabled (reciprocal)
  deltas        : CONVENTION-DEVIATION — its permissive on the Optical Sorter is stated as "ready &
                  not faulted", NOT as that machine's up-to-speed enable (convention rule 1). It is
                  the only start permissive among the six in-scope machines that is not an enable.
                  Two conditions, both retained, neither converted into "enabled". See Q-A6.
  deltas        : ASYMMETRY — its start permissives on the Ejected and Residual Material Conveyors
                  have no reciprocal shutdown-hold: those two conveyors release on the **Optical
                  Sorter's** shutdown-complete (rows 13, 14), not on this machine's. The start chain
                  and the shutdown chain therefore do not mirror each other at this machine.
  deltas        : none-further-found — searched: source table row 10, full 20-row sweep of both
                  interlock columns, extra-gate column (empty for this row), `vsd-motor` reference
                  §Class requirement set and §Class deltas. **Rotation-sensor usage is NOT settled
                  for this instance**: the class declares the input (VSD-17) and the source's
                  extra-gate cell is empty, which is an absence of a statement, not a statement of
                  absence — re-hunt at rung C §8.
  provenance    : relations <- source table row 10 + reverse sweep of rows 1–20;
                  class reqs <- vsd-motor reference v0-derived (VSD-01..VSD-20).
```

## TomraControlInst1 — Optical Sorter : optical-sorter

```
TomraControlInst1 (Optical Sorter) : optical-sorter
  fed-by        : not-stated-by-source — Q-A4.
  discharges-to : not-stated-by-source — Q-A4.
  serves        : Sorter Conveyor VSD — but by its ready/not-faulted state, not by an enable (see
                  the deviation delta on MotorVSDInst3 and Q-A6)
  interlock     : start requires the Ejected Material Conveyor enabled
  interlock     : start requires the Residual Material Conveyor enabled
  interlock     : controlled shutdown holds until the Sorter Conveyor VSD reports shutdown complete
  interlock     : the Ejected Material Conveyor holds its controlled shutdown until this machine
                  reports shutdown complete (reciprocal, source row 13)
  interlock     : the Residual Material Conveyor holds its controlled shutdown until this machine
                  reports shutdown complete (reciprocal, source row 14)
  interlock     : the Sorter Conveyor VSD may not start while this machine is not ready (reciprocal)
  interlock     : the Sorter Conveyor VSD may not start while this machine is faulted (reciprocal)
  deltas        : PLUS — exchanges process data with the machine over a data link in addition to
                  hardwired signals (source row 11 extra-gate: "comms words"; `optical-sorter`
                  reference OS-05/OS-06 — commanded both over the link and by a hardwired run
                  signal, and read back over both).
  deltas        : MINUS (class-level, consequence is cross-equipment) — this class's shutdown-
                  complete has no fault or hand escape term, unlike the sibling classes. A **faulted
                  sorter therefore does not release the two conveyors that hold their shutdown on
                  its shutdown-complete** (Ejected and Residual Material Conveyors). Recorded as a
                  process consequence, not as a requirement; see Q-A5.
  deltas        : MINUS (class-level) — no system-healthy permissive and no remote/local permissive
                  (`optical-sorter` reference §Class deltas). Its start-permissive set is one
                  condition shorter than the sibling classes'.
  deltas        : PLUS (class-level) — its enable is withdrawn immediately on loss of running,
                  unlike the sibling classes whose enable falls only with a timer.
  deltas        : NOT-SEPARABLE — this class has exactly ONE instance in the plant, so nothing
                  peculiar to this machine can be distinguished from a class property by observation.
                  Every "class-level" delta above may in truth be an instance delta and vice versa.
                  **This field cannot be completed by construction → Q-A3 (blocking).**
  deltas        : none-further-found — searched: source table row 11, full 20-row sweep of both
                  interlock columns, extra-gate column, `optical-sorter` reference §Class
                  requirement set (OS-01..OS-19), §Class deltas, §Observed as-built anomalies.
  not-inherited : the `optical-sorter` reference's §Observed as-built anomalies A-1, A-2 and A-3 are
                  behaviours **inside the given FB**. They are cited by ID only and are NOT written
                  as requirements or relations here, so no downstream rung inherits them as intent.
  provenance    : relations <- source table row 11 + reverse sweep of rows 1–20;
                  class reqs <- optical-sorter reference v0-derived (OS-01..OS-19, inherited whole).
```

---

## Relation summary (all in-scope relations, one line each)

| # | Subject | Relation | Object |
|---|---|---|---|
| 1 | FilterUnitInst2 | start requires enabled | Sorter Conveyor VSD |
| 2 | FilterUnitInst2 | shutdown holds until | plant fans-shutdown-ready condition |
| 3 | FilterUnitInst2 | shutdown holds until | Air-Separator VSD shutdown complete |
| 4 | FilterUnitInst2 | enable permits | Air-Separator VSD (bypassable path exists) |
| 5 | FilterUnitInst3 | start requires enabled | Sorter Conveyor VSD |
| 6 | FilterUnitInst3 | shutdown holds until | plant fans-shutdown-ready condition |
| 7 | FilterUnitInst3 | shutdown holds until | Air-Separator VSD shutdown complete |
| 8 | FilterUnitInst3 | enable permits | Air-Separator VSD (bypassable path exists) |
| 9 | FilterUnitInst1 | starts with the plant, no neighbour permissive | — |
| 10 | FilterUnitInst1 | shutdown holds until | plant fans-shutdown-ready condition |
| 11 | FilterUnitInst1 | shutdown holds until | Shredder shutdown complete |
| 12 | FilterUnitInst1 | enable permits | NOBODY (Q-A7) |
| 13 | MotorVSDInst1 | start requires enabled | Overband Magnet |
| 14 | MotorVSDInst1 | start requires enabled | Equipment Control System |
| 15 | MotorVSDInst1 | shutdown holds until | Air-Separator VSD shutdown complete |
| 16 | MotorVSDInst1 | enable permits | Air-Separator VSD (both paths) |
| 17 | MotorVSDInst1 | is held by (shutdown) | Overband Magnet, Equipment Control System |
| 18 | MotorVSDInst3 | start requires ready | Optical Sorter |
| 19 | MotorVSDInst3 | start requires not-faulted | Optical Sorter |
| 20 | MotorVSDInst3 | start requires enabled | Ejected Material Conveyor |
| 21 | MotorVSDInst3 | start requires enabled | Residual Material Conveyor |
| 22 | MotorVSDInst3 | shutdown holds until | Equipment Control System shutdown complete |
| 23 | MotorVSDInst3 | enable permits | FilterUnitInst2, FilterUnitInst3, Equipment Control System |
| 24 | MotorVSDInst3 | is held by (shutdown) | Optical Sorter |
| 25 | TomraControlInst1 | start requires enabled | Ejected Material Conveyor |
| 26 | TomraControlInst1 | start requires enabled | Residual Material Conveyor |
| 27 | TomraControlInst1 | shutdown holds until | Sorter Conveyor VSD shutdown complete |
| 28 | TomraControlInst1 | ready/not-faulted permits | Sorter Conveyor VSD |
| 29 | TomraControlInst1 | is held by (shutdown) | Ejected Material Conveyor, Residual Material Conveyor |

## Open questions

Blocking questions are marked **BLOCKING** and state exactly what they block. None is resolved here;
none was resolved in order to keep moving.

- **Q-A1 — BLOCKING — how do a filter unit's two shutdown-hold conditions combine?**
  The source writes the hold as `Fans-shutdown-ready / Air-separator VSD shut down` (rows 3, 4) and
  `Fans-shutdown-ready / Shredder shut down` (row 19). Both sides are retained above as separate
  relations (FI-37 split-and-retain). What the source does **not** settle is whether the unit stops
  when **both** are true (AND) or when **either** is true (OR). The readings are materially
  different: under OR, a filter unit stops as soon as the plant fans-shutdown-ready condition
  appears, even while the machine it is holding for is still shutting down.
  **Blocks:** rung C's binding of the shutdown-hold for `FilterUnitInst1/2/3`, and rung D's shape
  for that leg. *Applies to all three in-scope filter units.*

- **Q-A2 — BLOCKING — the class references are not engineering standards, so this rung's
  completeness-by-construction is not actually achieved.**
  All three references (`FilterUnitSystem`, `vsd-motor`, `optical-sorter`) carry a header stating they
  were reverse-derived from the as-built FB code on 2026-08-04 and are "not a site standard, a
  vendor document, or a reviewed engineering reference". The guarantee this rung is supposed to
  deliver — *an instance inherits its class's full requirement set, so a standard requirement cannot
  be silently forgotten* — degrades to *an instance inherits whatever the as-built FB happens to do*.
  A standard requirement the FB does not implement is invisible here **and stays invisible in every
  rung below**. Concrete example of the exposure, not hypothetical: `optical-sorter` has no
  system-healthy permissive, which the reference itself can only report as an observation, not
  adjudicate.
  **Blocks:** any claim that the requirement sets inherited above are complete. It does **not** block
  the relations in this artifact (those come from the topology source, not from the references).
  **Needs:** a reviewed class standard, or an explicit owner acceptance that "as-built = the
  standard" for this plant.

- **Q-A3 — BLOCKING — `optical-sorter` deltas cannot be separated from class properties.**
  The class has exactly one instance (`TomraControlInst1`). By observation, "this machine is unusual"
  and "this class is unusual" are indistinguishable, so the `deltas` field for that instance — a
  first-class field precisely because outliers are where guards get lost — **cannot be completed by
  construction**. It is filled above with what is observable and marked NOT-SEPARABLE rather than
  being left to imply "no deltas".
  **Blocks:** trusting the delta set for `TomraControlInst1` as complete at rungs C and D.
  **Needs:** a second instance, or a class standard (see Q-A2), or an owner statement of which of the
  four listed properties are machine-specific.

- **Q-A5 — non-blocking — is a faulted optical sorter meant to strand the conveyors waiting on it?**
  The class's shutdown-complete has no fault or hand escape term (unlike both sibling classes), so
  the Ejected and Residual Material Conveyors, which hold their controlled shutdown on this
  machine's shutdown-complete, are not released when it faults. Recorded as an observed process
  consequence. Not blocking: the relation is stated by the source either way and no artifact below
  changes with the answer — but it is a plant-behaviour question an engineer should rule on.

- **Q-A6 — non-blocking — why is the Sorter Conveyor VSD's permissive on the Optical Sorter
  "ready & not faulted" rather than the sorter's enable?**
  Every other start permissive in the source is an up-to-speed enable (convention rule 1). This one
  is not, and the source gives no reason. Recorded as a first-class convention-deviation delta on
  `MotorVSDInst3`. Not blocking: the source states the relation explicitly, so nothing had to be
  inferred; only the rationale is unknown.

- **Q-A7 — non-blocking — the Cyclone Filter Unit's enable permits nothing. Is a permissive missing?**
  Sweeping all 20 rows, no machine's start permissive names `FilterUnitInst1`'s enable, yet its class
  produces one (FU-07) and it is subject to the same fan-start-time gate as the two dust filters
  whose enables *are* consumed. Either the consuming machine's permissive is absent from the source,
  or this unit genuinely serves nothing. Recorded as a delta. Not blocking: nothing below needs the
  answer to be built correctly — but if a permissive is missing from the source, everything below
  inherits the gap silently, so it should be answered before build.

- **Q-A4 — non-blocking — material-flow direction is not given.**
  The source is an equipment/interlock inventory, not a P&ID: it states dependency links but never
  which machine feeds which. `fed-by` / `discharges-to` are therefore recorded as
  `not-stated-by-source` for all six instances rather than being inferred from the dependency graph.
  Not blocking: every relation above comes from a stated dependency, and none of them changes with
  the flow direction — only the *rationale* ("because it is downstream") would. Deliberately **not**
  resolved by reading the dependency graph backwards, which is the inference the register's own Q-02
  already flags as unconfirmed.

## What this rung did NOT do

- Did not read the REQ prose of the source document (reserved for rung B).
- Did not read any `§As-built provenance` section of any class reference (reserved for rung C).
- Did not read the IO tables or any `.ir` file (signals are rung C).
- Did not group instances into FB types (that is rung D's job) — `FilterUnitInst2`/`FilterUnitInst3`
  are recorded as source-identical, which is an input to that decision, not the decision.
