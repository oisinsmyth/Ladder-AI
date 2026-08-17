# THE DB CAMPAIGN — every design block exercised against something real

**2026-08-17.** The ask: *run a live test of every design block in the test-tooling spec, bar the
demoted ones.* Subject: `docs/notes/PC-Client-Modbus-Spec-Draft-final.txt` §4 / §4a, **DB-1 … DB-13**.
DB-10 (agent scheduling) and DB-11 (the rig pool) are deliberately demoted by the spec and were
excluded by the ask, leaving **eleven**.

> 🔴 **THE DISTINCTION THIS DOCUMENT EXISTS TO KEEP.** `spec-reconciliation.md`'s `AS SPECIFIED`
> bucket means *the code exists and matches the spec*. It says **nothing** about whether the code has
> ever run. Before this campaign, nine of the thirteen blocks were `AS SPECIFIED` and **six of those
> had never executed outside their own unit tests.** *A green suite over an unreachable component is
> this repo's most re-earned defect, and it had it in six places at once.*

---

## THE STARTING POSITION, MEASURED RATHER THAN QUOTED

Traced from the project reference graph and the CLI dispatch, not from documentation:

- `wave-cli` exposed **two** verbs (`submit`, `status`) and drove `SlotConflictDerivation` →
  `WaveSetAdmission` → `WaveStore`. **That is DB-13, and nothing else.**
- `harness-gate` drove `SubmissionGate` — **DB-9's gate half only**.
- `harness-run` drove `LoopRun`, where **DB-2, DB-8 and DB-12** live. ***It had never been run.***
- **DB-1, DB-4 and DB-7 were reachable from no binary at all.** `WaveSetAdmission`'s only mention of
  `WaveBoundaryBatchPlanner` was a **doc comment** (`WaveSetAdmission.cs:466`); `Cleanup` had **zero**
  references outside its own file and its own tests.

**The split followed the executables exactly.** That is the campaign's first result and it was
available before any test ran.

---

## OUTCOME BY BLOCK

| block | what it now has | what was measured |
|---|---|---|
| **DB-1** | `wave-cli route` | Real change sets routed. `Main` drift is **STOP-class**; the controller change set is 4 × RunQueue, three of them `RUN (Init)`. Two objects **refused by name**. |
| **DB-2** | — | 🔴 **The validity stamp does not reach the written result.** See findings. |
| **DB-3** | already had one | Download to the rig: **`state=Success, errors=0, warnings=0`, 53 items loaded by name**, confirmed on the wire. |
| **DB-4** | `wave-cli batch` | Ruling on the 45-object deploy; the program collapses into **one connected group of 34**. |
| **DB-5** | already had one | Fence refused in **both** modes with a positive control, Portal never contacted. |
| **DB-6** | `harness-verify` | 🔴 **A defect that made the version register useless.** Fixed; guard then measured refusing **and** admitting, live. |
| **DB-7** | `harness-cleanup` | Zero eligible over the real corpus, checked three ways; a new gap in X-J. |
| **DB-8** | — | ***The first result package that has ever existed*** — and it drops five of its seven named contents. |
| **DB-9** | already had one | 27 vectors × 25 gates; gate 4b's D6 violation resolved by an independent party, which then refused the vectors. |
| **DB-12** | — | Gates 10a/10b ran on the real submission. **No compressed wave; `k` still unmeasured.** |
| **DB-13** | already had one | **Both directions**: 4 disjoint slots → 1 wave set, concurrency 4; 3 slots on one FB → 3 wave sets, concurrency 1, 3 edges. |

---

## THE FINDINGS THAT MATTER

### 1. 🔴 The build stamp was hashed over the copy layer it is written into — DB-6, §9

The rig read `16#F52ECEAD`. Re-running **the exact command the promoting commit records** gave
`16#4ED5E68D`. Every ordinary explanation was eliminated: `ir/` unchanged since that commit (it *is*
the most recent commit touching it), no code change after it under `Harness.Map/Run/Loop`, 43 files
all tracked and none dirty, three consecutive runs identical. **The pre-promotion tree reproduced the
deployed stamp exactly.**

`FC_HarnessCopyLayer` and the `HarnessMirror` tag table are **files in `ir/test-project001`**, so
`--program <ir-dir>` supplied them as objects under test. `BuildStamp`'s own summary forbids exactly
this — *"the copy layer is not one of its own inputs … hashing the copy layer would be circular"* — it
guarded its own arguments and not the side door.

**Consequence, and it is the worst available shape:** every promotion invalidated the stamp it had
just written, so `VersionCheck` classified a **correctly deployed** program as `Stale`, whose text
reads *"the download aborted, was refused, or never reached it"* — sending a person to re-download a
PLC that is already right. **An unconfirmable device reporting as a wrong one.**

Fixed in `BuildStamp.Derive` by excluding objects named by the naming's own `BlockName`/`TagTableName`
— derived, never a hardcoded list. Verified as the defect was found: the pre- and post-promotion trees
differ in exactly those two files and now stamp **identically**. Six tests; mutation-checked, reddening
exactly the three that assert the exclusion while three converse tests stay green.

**Closed end to end.** The corrected stamp was promoted (one line), imported, compiled, downloaded, and
**read back off the wire as `16#33434A68`** — with the scan counter restarted from near zero, which is
what a genuine CPU stop-and-restart looks like and is independent of any exit code.

### 2. 🔴 The result package drops five of its seven named contents — DB-8, DB-2

The first wave ever run produced the first `ResultPackage` that has ever existed. The **written
artifact** (`result.json`) carries `vector`, `verdict`, `conclusive`, `whatToDoNext`, `caveats`,
`assertions` — and of DB-8's seven named contents only the stimulus check survives.
***Absent: the validity stamp (DB-2's entire purpose), the manifest presence, the co-running slice,
the basis citation and the fidelity declaration.***

The console *renders* several of them. The JSON does not carry them. This is precisely the hazard
`download-probe` names in its own output — *"a consumer reads THAT, never this rendering — anything a
renderer drops is gone before a scraper sees it."*

**DB-2 is unexercised as a result, and it is unexercised in the specific way DB-2 exists to prevent:**
its stamp is what stops a green obtained *before* an invalidating change from being read as current.

> ✅ **CLOSED 2026-08-17 (`8584f5a`), OFFLINE.** `ResultPackageJson` renders all seven contents on every
> package, and a content that is genuinely unavailable is **present and says so** rather than absent.
> Two supporting gaps were found while fixing it: **manifest presence was not on the package type at
> all** (`StimulusCheck` consumed it and dropped it), and **an unrecorded co-running slice rendered as
> the positive claim "ran alone"**. ⚠️ **The wave was NOT re-run — that needs the rig.** The finding
> above stands as the record of what the artifact of 2026-08-17 contains.

### 3. 🔴 The wave reported on 2 of 22 vectors, using 2 as its own denominator

`OUTCOME: Ran — the wave ran to 22 index(es) over 1 slot(s), costing 890 round trip(s).` Then **two**
packages, then: `0 of 2 package(s) say anything about the block at all.`

**Nothing anywhere states what became of vectors 3–22.** The summary's denominator is *the number of
packages*, not the number of vectors submitted. A slot exiting early is legitimate behaviour; a report
that silently renarrows its own denominator to match what it managed to produce is not.

Both packages were honest about themselves — `UNSETTLED` and `STALE`, the latter saying outright
*"THE EXPERIMENT NEVER RAN … Reading this as a failure would send an agent editing correct logic."*
**The per-package honesty is real; the run-level denominator is the gap.**

> ✅ **CLOSED 2026-08-17 (`8584f5a`), OFFLINE.** The denominator is now the SUBMITTED count everywhere,
> every submitted vector carries a disposition and a reason in both the console and the JSON, and a slot
> that stops short is reported with the index it reached and the number of vectors consequently never
> attempted. `wave.Length` — *what the wave SET OUT to run* — is no longer printed as what it ran.
>
> 🔴 **AND THE DISPOSITION OF VECTORS 3–22 IS NOW ESTABLISHED FROM THE CODE, THOUGH NOT ITS CAUSE.**
> `WaveRun.Run` has exactly **one** `break`, and it is the only route by which a slot's collected results
> can be shorter than its tensor: an inert phase that could not be ESTABLISHED records one `NotInert`
> result and stops the wave. Two collected results therefore means **the wave broke at index 1**, and
> vectors 3–22 were never submitted to the device. That also accounts for the second package's
> `CommandedButDidNotRun` — on that path the echo is cleared and never committed.
> ⚠️ ***WHICH of the four `InertOutcome` values it was is NOT recoverable from the artifact***, because
> `inert.Detail` was never serialised. It is now (`slotExits[].lastOutcome` / `lastDetail`), so the next
> run answers it. **Do not read a cause into this one.**

### 4. 🔴 Gate 4 compares free prose, so it cannot survive an independent author — DB-9, M4

Gate 4b refused all 27 vectors because the fidelity list was written by `vector-author-b-5.2`, the
party whose vectors it licenses. An independent declarer derived the list from
`FB_HopperBlockageStim` **without reading the vectors**, and found:

- ***The pre-boundary "wait for the CPU restart" does not exist.*** N9 gates `InPreBoundaryWait` on
  `NOT PreBoundaryDone` while N10 sets `PreBoundaryDone := InPreBoundaryWait` — the state sets the
  flag its own guard forbids, so it is **one scan wide (~2 ms)** and cannot be waited in. Both the
  block comment and the member comment say otherwise.
- **Reverse running feedback is force-cleared** (N22); the model drives forward only, while **all 27**
  vectors asserted *"forward or reverse"*.
- **`VB-HBA-016`'s "reset held from before T=0" is not producible** — the model presents exactly one
  rising edge at scenario start, where the vector's own text said *"there is no rising edge."*

Then gate 4 refused all 27 — because it is
`assertedBehaviours.Where(b => !fidelity.Represents.Contains(b))`, **exact ordinal string membership
over free prose**. An independent author using their own words refuses everything, mixing real
contradictions with a vocabulary artefact. ***A check that cannot survive an independent author is not
checking the case it exists for.*** The shared vocabulary needs identifiers, not sentences.

**Reconciled: 22 of 27 survive — and `kept unchanged = 0`.** Every survivor was **narrowed**, not
reworded. Five dropped, uncovering **the whole of REQ-HBA-007**: the lost defect classes are *a
retentive accumulator surviving a power cycle* and *a latched alarm failing to survive one*. Neither
is reachable by any vector this model can drive — **a capability request, not a vector-authoring task.**

⚠️ **And gate 4's green is weaker than it looks:** the model's R1 entry covers three signals in one
string, so *"hopper held high"* and *"running feedback held"* now collapse onto the **same** string.
Granularity survives only in each vector's provenance field. Separately, **no vector exercises reverse
feedback in either direction**, so an implementation ignoring `Shredder_Run_Rev_FB` passes all 22.

### 5. DB-4 was bypassed, and would not have bound the download anyway

Both are true and they are separate facts. **Nothing consulted it** — no caller existed and
`Harness.Device` does not reference the assembly at all; that would be equally true of a delta
download, so it is a real defect in the deployment path. **And** the 45-object set contains `Main` and
`OB100`, and an OB is **fixed STOP-class**, so that download was drain-class by construction whatever
its count. The count is not innocent either: `Main`'s 12 calls collapse the program into **one
weakly-connected group of 34 objects** — *no consistent RUN-mode download of that set exists at all.*

### 6. X-J's ownership rule cannot reach a TYPE or a TAGTABLE — DB-7

Neither has a number space, so `HarnessMirror` and `UDT_HopperBlockageStim` are genuinely
harness-owned yet **neither owned nor provably not owned** by the 9000–9999 band. Surfaced only by
running against the real corpus.

---

## WHAT WAS **NOT** ESTABLISHED

*Listed because a campaign that reports only what it reached is the failure it exists to prevent.*

- **DB-12 has never run compressed.** `runtimeCompression: 1` on the wave that ran. Its ceiling is
  `k × scan`; the scan is measured four ways (~2.11–2.14 ms) but ***`k ≈ 5` is the spec's own assumed
  number and has still never been measured.***
- **DB-2 is unexercised** — see finding 2. The stamp exists in the type and not in the artifact.
- **§9's post-download settling window was never observed.** `ReadsToSettle = 1` was measured, but in
  **steady state**: `harness-verify` cannot download, and it was not watching across the one real
  boundary. On a *disruptive* download the CPU is stopped, so the server is absent rather than
  flapping — §9's description may not even apply to that class. Untested either way.
- **20 of 22 vectors produced no package**, so the block under test has had **nothing** said about it.
  `0 of 2 package(s) say anything about the block at all` is the tool's own verdict and it is correct.
- **`harness-verify`'s IL walk is not a call-graph closure** — it names the two entry points that can
  reach a write and asserts their absence; a write through a third path in a referenced assembly is
  not covered.
- **DB-7's drain state is declared, not computed.** Nothing in this repo computes the in-flight set.
- **DB-1's change-class table is transcribed from vendor documentation with known holes** (G2, G7 both
  open) and **no row was tested against a controller.**
- **The claim-release half of DB-7 (§16.12c) is half built** and the leak cannot be demonstrated here:
  the shared store holds zero claims, because every 9000–9013 number predates the registry.

---

## ONE ERROR OF MY OWN, RECORDED

The coordinator's join of the independent declaration into the submission **dropped four of the
model's `_`-prefixed annotations** — including `_scanTimeBudget` (a declared M3 gap) and
`_notComputable` (*marker retentivity across a restart*), **which is the exact question the five
dropped vectors turn on**. M3 exists so that caveats travel with the result; mine did not. Caught by
the reconciliation lane, which declined to repair it because repairing it meant editing the model
object — the right call.

I also instructed a lane to re-assert `Standard` memory layout on `FC_HarnessCopyLayer`. It declined,
with evidence: an **FC has no data area**, `DeploymentPlan` targets data blocks only, the block was
already Optimized before the import, and it gated the claim both ways (`--expect Standard` → exit 15,
`--expect Optimized` → exit 0). **I had applied a DB rule to a block where the property is inert.**

---

## THE NON-START, DIAGNOSED — AND IT IS A DESIGN GAP, NOT A TOOLING BUG

**2026-08-17, after both reporting fixes landed and were re-run against the rig.** Finding 3 left the
slot exit *unexplained*, and I recorded a refuted hypothesis rather than a guess: `cross-check` shows
**no multi-writer on `iDB_HopperBlockageStim.Stim.Start`**, so the copy layer and the model are not
fighting over the start signal.

The mechanism was then established from code — `WaveRun.cs` has **exactly one `break`**, reached when
an inert phase cannot be ESTABLISHED — and the fixed reporting named the cause outright:

> `slot 0: ran 2 of 22 index(es), stopping at index 1 with NotInert.`
> `the next test's start conditions are not established: slot 0 R000 reads 1, declared 0;`
> `slot 0 R001 reads 1, declared 0`

`R000`/`R001` are `HopperBlockedAlarm` and `HopperBlockStopReq`. **Independently corroborated on the
device before the run** — `harness-mirror-view` decoded both as `TRUE` while `Armed` was `FALSE`, which
is also why the phase-armed latches correctly did *not* capture them.

🔴 ***THE BLOCK LATCHES AN ALARM AND THE HARNESS CANNOT INERT OUT OF IT.*** The alarm clears only via
`DB_Controls.FaultReset`, which only the stimulus model drives, and the model only runs once started —
**and starting is not inert.** So after any vector that alarms, every subsequent index fails its inert
check and the wave stops.

***CONSEQUENCE: THE WAVE CAN RUN EXACTLY ONE ALARMING VECTOR PER CPU RESTART.*** Reproduced: against a
freshly restarted CPU the wave ran **2 of 22** and stopped at index 1 for that reason; against the
already-alarmed rig it ran **1 of 22** and stopped at index 0 for the same reason. Same signals, same
values, both times.

**`rig-control` cannot fix this**: it can start a CPU and **deliberately cannot stop one** — *"reversal
is a person's job"*. The only mechanism that restored inert was a **disruptive download**, which is a
heavy instrument for clearing a test artifact.

**This is D33's contract meeting a real block for the first time.** Inert is *"the next test's start
state, dynamics untriggered, resets HELD"*, and nothing in the wave loop can drive a reset between
indexes. **It belongs to the binding and the stimulus model, not to the harness code** — the model must
return the block to inert at scenario end, or the inert declaration must not require a latched alarm to
be low. ***It is the blocker for using the conformance loop on a real job, and it is now precisely
characterised rather than a mystery.***

⚠️ **And the reporting fixes are what made it findable.** Before them this run read as *"2 packages, 0
conclusive"*. It now names the stopping index, the outcome, the two signals, their read and declared
values, and accounts for all 22 submitted vectors individually.

### Verified live, after the fixes

- All **seven** DB-8 contents present in the artifact; `validityStamp` carries the **actually running**
  `programVersion 16#33434A68`, the map hash and its caveats. **DB-2 is now genuinely exercised.**
- `vectorsSubmitted 22 / ran 2 / neverAttempted 20`, and `dispositions.length == 22` — **coverage is
  computable from the artifact alone.**

### One operational fact worth keeping

A download that had taken **35 s** took **10 minutes and timed out**: `download-probe` had been
**rebuilt** at 09:00:28 and TIA approves callers by `(Path, FileHash)` — **0 of its 15 whitelist
entries matched the new hash** across 17.0/19.0/20.0. `tools/openness-approve-build.ps1 -Exe <path>`
wrote the entry and the next download completed. **The binary's own warning said exactly this before
the stall, in its output, and it was there to be read.**
