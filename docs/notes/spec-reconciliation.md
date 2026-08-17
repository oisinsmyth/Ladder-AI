# SPEC RECONCILIATION — every item of `PC-Client-Modbus-Spec-Draft-final.txt` against what exists

**Written 2026-08-14.** Subject: `docs/notes/PC-Client-Modbus-Spec-Draft-final.txt`, 4,528 lines,
read end to end for this pass. The spec is the design's source of truth; a great deal has been built
since it was written, and some of what was built deliberately differs from it because measurement
said so. This document is the three-way comparison, plus the fourth bucket that must not be folded
into the other three.

> ***PREFER THE CODE TO ANY DOCUMENT, INCLUDING THIS ONE.*** Every `AS SPECIFIED` and `DIVERGED`
> row below cites a file and a line, or a measurement. Where a row cites only a document, it says so
> and is weaker for it. Three documents were found stale on 2026-08-13 alone, and two more were
> found stale by this pass (§X below).

---

## THE BUCKETS

| bucket | means |
|---|---|
| **AS SPECIFIED** | built, and it matches. Cited. |
| **DIVERGED** | built differently, deliberately, with evidence. ***The spec has been corrected to match***, with the date and the evidence, at the section named in the row. |
| **NOT BUILT** | nothing implements it. The row carries the **consequence**. |
| 🔴 **NOT CHECKED** | ***I did not examine it.*** Not compliant, not clean, not a pass. *Empty is not clean* is this repo's most re-earned rule and an audit that quietly omits what it could not reach is the exact failure it exists to prevent. |

**The unit** is one numbered or lettered spec item (`D-n`, `DB-n`, `R-n`, `M-n`, `X-n`, `G-n`, `O-n`,
`F-n`), or one named section-level normative claim. Sub-items are split and counted separately only
where the halves land in different buckets — otherwise the row carries the whole item and its
consequence names the missing half.

---

## THE COUNTS

🔴 ***CORRECTED 2026-08-14. THE ORIGINAL COUNTS WERE WRONG IN ALL FOUR BUCKETS, AND THIS SECTION
CLAIMED THEY WERE COMPUTED FROM THE TABLES. THEY WERE NOT.*** A mechanical census of every data row
in §0–§16 — bucket cell read literally — gives the left column. The right column is what this
document asserted for its first day of life, including in the reports built on it.

| bucket | **measured, by census** | ~~as originally claimed~~ |
|---|---|---|
| **AS SPECIFIED** | **123** | ~~86~~ |
| **DIVERGED** (spec corrected) | **30** *(29 + `12.3`, re-bucketed 2026-08-14)* | ~~33~~ |
| **NOT BUILT** | **22** *(21 + `X-H`, "NOT BUILT (the warning)"; was 23 before `12.3` moved out)* | ~~38~~ |
| 🔴 **NOT CHECKED** | **14** | ~~21~~ |
| **TOTAL ROWS** | **189** | ~~178~~ |

> **Re-counted mechanically 2026-08-14 after `12.3` moved**, by reading the bucket cell of every data
> row in §0–§16 literally — **123 / 30 / 22 / 14 = 189**, which reproduces the census above shifted by
> exactly the one row that moved. *The total is the invariant here: a re-bucket must conserve it, and
> a census that does not reproduce is the finding.*

**Two corroborating self-contradictions, both now confirmed:** §NC's named list sums to **20, not
21**, and the *"six residual rows marked 🔴"* it invokes **do not exist as rows** — the other 🔴 marks
are inline caveats inside `DIVERGED` rows.

### ⚠️ WHERE THE FAMOUS "38" ACTUALLY LIVES

**It is not in this document and never was.** It is reconstructible only from
`tooling-test-plan.md` §H, and there it reconstructs *exactly*: **23 entries derived from these
tables + 15 harness/contract-level absences the test plan found for itself = 38.** §H's own stated
explanation of that arithmetic — *"splitting D9/D20/O14 and the §11 triple"* — **is also wrong**;
that adds about four, not fifteen. ***§H's 38 is a DIFFERENT SET from the 38 this document claimed,
and this document's 38 does not exist anywhere.*** The register is **43 rows**, not the 42 reported.

> 🔴 **THE LESSON, WHICH IS THE POINT OF RECORDING THIS RATHER THAN QUIETLY FIXING IT.** These counts
> were reported onward as a *result* — into a campaign summary, into `CLAUDE.md`'s neighbourhood, into
> a plan — when they were a **claim**. *A number is evidence only if something counted it.* The census
> that found this took one pass over the tables; it was never run because the total was already
> written down. **Re-run `SELF-4` before quoting any figure from this document or the test plan.**

🔴 **The `NOT CHECKED` rows are enumerated by name in §NC below.** A reconciliation reporting no
`NOT CHECKED` had better have examined everything; this one did not, and says which — **though note
that §NC's own total is one of the numbers that did not survive the census.**

---

## §0 — THE REVERSAL

| item | bucket | evidence / consequence |
|---|---|---|
| 0.1 Build a change **classifier**, not a batch lock | AS SPECIFIED | `src/wave-control/WaveControl/ChangeRouter.cs`, `ChangeClass`, `DownloadQueue`, `ChangedObject`. |
| 0.2 The download **option** does not decide STOP/RUN | AS SPECIFIED | Measured, §9b's own table; `DownloadOption` carries no stop semantics. |

## §1 — THE LOOPS

| item | bucket | evidence / consequence |
|---|---|---|
| 1.1 Loop 1 — test loop bracketed by two review passes, final test cycle | **NOT BUILT** | The review skills exist; **nothing sequences them around a test loop**. `Harness.Loop.LoopRun` is the 8-step *inner* loop and knows nothing of reviews. *Consequence: the bracket is a human/skill discipline, so "its last verified state would not be its final state" is unprevented.* |
| 1.2 Loop 2 — admission: preflight + compile clean in isolation | AS SPECIFIED | `src/wave-control/WaveControl/AdmissionController.cs`; `converter preflight`; `openness-cli compile --block`. |
| 1.3 Loop 3 — the wave: inert → verify → raise start bools → observe → distribute | AS SPECIFIED | `Harness.Wire/InertPhase.cs` (`Establish`/`Commit`), `SlotRun.cs`, `WaveRun.cs`, `Harness.Loop/WaveSetSequence.cs`. Built; **never yet driven end to end** — see 15a.4. |
| 1.4 Two download queues; STOP-class accumulates | AS SPECIFIED | `ChangeRouter`, `DownloadQueue`, `WaveQueues`, `DrainPolicy.cs`. |

## §2 — HOW `lad-coder` INTERACTS

| item | bucket | evidence / consequence |
|---|---|---|
| 2.1 The coordinator generates the mirror, copy layer, latches, scan-stamps, block-enable bitmask, OB80, the register map | **DIVERGED** *(scope narrowed 2026-08-14 — ***this row was stale and was not on the brief***)* | `Harness.Map/CopyLayerGenerator.cs` + `MapAllocator` generate the mirror, copy layer and map. ⚠️ **This row used to say it generates *"no latches"*, and cited the generator's own doc comment as the authority. BOTH halves went stale together:** latches **are** now generated (§12.3, `ca9819b`), and the generator's absence list no longer names them — *so the citation would have gone on reading as corroboration while pointing at text that had changed underneath it.* ***It generates no scan-stamps, no block-enable bitmask and no OB80*** — those three remain deliberate absences. §12 corrected. |
| 2.1b Agents speak TAG NAMES, never a register number (D8) | AS SPECIFIED | Gate 7 refuses an absolute address by regex (`SubmissionGate.cs:67`, :701). |
| 2.2a Does not write its own vectors (D6) | AS SPECIFIED | Gate 2 authorship, `SubmissionGate.cs:246`. |
| 2.2b Does not touch the test project | **NOT BUILT** | Governance only; no mechanism in `src/`. *Consequence: `SoftwareOnlyChanges` safety rests on discipline.* |
| 2.2c Does not take the project online | **NOT BUILT** | Measured that Openness refuses a download while online; nothing prevents an agent going online. *Consequence: one agent online blocks the wave loop and the failure appears as a download refusal.* |
| 2.2d Does not revise a shared UDT mid-wave-set (D16) | 🔴 **NOT CHECKED** | I did not look for a UDT freeze. *Consequence if absent: a UDT revision is `RUN (Init)` for every DB on it — the widest blast radius available — with no gate.* |
| 2.3 Memory-layout revert bites standard-access harness scaffolding only | AS SPECIFIED | Gate 11 (`SubmissionGate.cs:963`) checks `layout: Standard` **and** `layoutSetAfterImport == importStamp`. |
| 2.4 Results carry observed-vs-expected + the co-running log | AS SPECIFIED | `ResultPackage.cs`, `Harness.Wire/CoRunningLog.cs` — built from the start **echo**, never from the plan (X-E's own rule). |
| 2.5 Blacklist is add-only | AS SPECIFIED | Gate 8; `BlacklistEntry` has no negation, allow or override member. |
| 2.6 Design-for-testability: "a future skill"; the runner refuses an unobservable vector | **DIVERGED** | It exists: `docs/notes/test-environment-contract.md` + the `design-for-testability` skill + **23 gates** in `Harness.Results/SubmissionGate.cs`. The contract grew surfaces the spec never described — `blockCompression` (§2.3), `boundsUsed` (§2.5), `deployment` (§4.5), `requiredObservations`, and gates **3d/3e/3f/3g/3h/3i/8c/10a/10b/11**. §2.6 corrected. |

## §3 — DECISIONS D1–D37

| item | bucket | evidence / consequence |
|---|---|---|
| D1 (superseded by D9) | AS SPECIFIED | Retained for numbering; carries no obligation, and the document does exactly that. |
| D2 map authored PC-side and authoritative | AS SPECIFIED | `Harness.Map/MapAllocator.cs`, `RegisterMap.cs`. |
| D3 identity comes from the PLC, program-published | **DIVERGED** | `BuildStamp` + `VersionCheck` over **Modbus**. The marker-DB identity path over classic S7comm is **dead on this rig** — `DBRead`/`MBRead` return `0x00040000` in RUN and STOP while SZL answers (§16.11). Recorded in §16.11; the D3 line itself now points there. |
| D4 a vector may parametrise a PLC-side model | AS SPECIFIED | `Harness.Skeleton/TrivialBlockModel`, `PeakBlockModel`; vector `Stim.*` fields. |
| D5 inert is returned to and **verified** | AS SPECIFIED | `InertPhase.Establish` — two checks (declared values, then unchanged), scan re-read after the second. |
| D6 the block author does not author its vectors | **DIVERGED** | Gate 2 enforces it — **and gate 3d extends it to the ENUMERATOR** (`SubmissionGate.cs:315/327`), closing §7's own open question "WHO PERFORMS THE ENUMERATION". §7 corrected. |
| D7 (superseded by D15) | AS SPECIFIED | Retained for numbering. |
| D8 tag names, never registers | AS SPECIFIED | See 2.1b. |
| D9 independence = non-overlapping reachable state, **computed** | **AS SPECIFIED** (2026-08-14) | ✅ **BUILT AND RUN END TO END.** `converter reachable-state --project <ir-dir> [--block <name>]... [--json]` emits, per block, the transitive closure through its CALL tree of every storage location it touches — reads as well as writes — keyed on storage identity, with a corpus-stamped provenance. `wave-cli submit --reachable-state <file> --reachable-block <name>` reads that into `TestSlot`, and `SlotConflictDerivation.OverlappingReachableState` makes the edges by set intersection with no further work. **`--reaches`/`--reaches-from` survive as the DECLARED path and are refused in combination with the computed one.** ⚠️ **THIS ROW USED TO SAY THREE THINGS AND TWO OF THEM WERE ALREADY STALE WHEN IT WAS ACTED ON:** the EDGE producer *did* exist (`SlotConflictDerivation.AllComputedEdges`) and `cross-check`'s FB-internal paths *were* qualified (`MultiWriterFact.Owner`, 2026-08-14). What was genuinely missing was one level further back — **nothing computed `TestSlot.ReachableState`**, and it arrived from the submitting agent. **Measured, from the IR alone, through the Release binaries:** six slots on `FB_HopperBlockageMonitor` → **15 `OverlappingReachableState` edges → SIX WAVE SETS OF ONE**, achieved concurrency 1, which is the answer `slotsInWaveSet` was hand-corrected 6 → 1 to match *by an author who said outright it was not verified against `ir/`*. The converse is measured too and is not optional — *a producer that finds conflicts everywhere passes every test that only checks for conflicts*: the four `FB_Hx*` blocks → **NO edges, one wave set of four**, each closure asserted non-empty. Whole corpus: 18 blocks, 18 computed, 0 withheld, 229 alias rewrites. |
| D10 per-slot instance DBs (one per tensor, not per vector) | AS SPECIFIED | `MapAllocator` allocates per slot; `openness-cli create-instance-db` scaffolds. |
| D11 execution is concurrent **within a tensor** | AS SPECIFIED | `WaveRun.Observe` polls all active slots per index. |
| D12 models support time compression; vectors declare the factor; over-declaration REFUSED | AS SPECIFIED | `Harness.Results/TimeCompression.cs`; gates 10a/10b. |
| D13 instrumentation is a property of the copy layer | AS SPECIFIED | `CopyLayerGenerator`; no vector may add a block input. |
| D14 post-download verification is a program-version constant over Modbus | AS SPECIFIED | `BuildStamp`, `VersionCheck`, `RegisterMap.VersionRegisters = 2`. Measured live: `16#21D74D35` at registers 0–1. |
| D15 the map is frozen for a wave set | AS SPECIFIED | `RegisterMap.MapHash`; excision does not move it (`RegisterMap.cs:429`). |
| D16 shared UDTs frozen for a wave set | 🔴 **NOT CHECKED** | See 2.2d. |
| D17 stop-class refusals are two-stage | AS SPECIFIED | `converter preflight` (stage 1) + `ChangeRouter`/`AdmissionController` against `DeployedProgram` (stage 2). |
| D18 the harness disconnects across a download; reconnect + version check is the normal path | AS SPECIFIED | `Harness.Loop/LoopRun.cs` steps 5–6; `VersionCheck`. |
| D19 time compression is the throughput lever | **DIVERGED** | Superseded inside the spec itself by §8/D26 (the lever is tensor count) and narrowed by DB-12. Built as the narrow lever only. No further correction needed — the spec already carries it. |
| D20 loop 1 brackets the test loop | **NOT BUILT** | See 1.1. |
| D21 results carry a co-running log | AS SPECIFIED | `CoRunningLog.cs`, measured from the echo. |
| D22 a blacklist may only add | AS SPECIFIED | Gate 8. |
| D23 changes routed into two queues | AS SPECIFIED | `ChangeRouter`, `RoutingVerdict`. |
| D24 the deferred queue drains when no test can progress | AS SPECIFIED | `DrainPolicy.cs:270`, `ProgressBlockKind`; zero value authorises nothing. |
| D25 a disruptive download must be FULL, never differential | AS SPECIFIED | `DownloadConfigurationPolicy`, `DownloadMode`. |
| D26 a wave is a sequence of tensors | AS SPECIFIED | `WaveRun`, `SlotTensor`. |
| D26a the wave model — column/row, null=inert, per-slot exit | AS SPECIFIED | `WaveRun.cs`, `SlotDistribution`; an unraised start bool is the null encoding. |
| D26b a slot is per (agent, methodology) | AS SPECIFIED | `TestSlot`, `Submission`. |
| D27 tensor packing is a graph colouring | **DIVERGED** | Built as `WaveSetAdmission.Admit` — greedy, deterministic — but under D26a it **colours SLOTS into WAVE SETS**, not tests into tensors, exactly as DB-13 corrects. The spec already carries the correction at DB-13; D27's own wording still says "pack the submitted tests into the fewest tensors". §3/D27 corrected to point at DB-13. |
| D28 models instanced per test | AS SPECIFIED | Per-slot model + model iDB in the object budget (`BatchPlan`). |
| D29 tensor width bounded by conflict-freedom, capped by poll bandwidth | **DIVERGED** | Built as `WireTiming.MaxTensorWidth` = `R × floor(S_min × scan / RTT_p99)` — **REPORTED, NEVER ENFORCED**, because D36's free variable `S_min` has never been observed. And the cap is an **input** to `WaveSetAdmission`, whose absence is `ColouringDefect.WidthCapNotSupplied` = *unchecked, never passed*. §12a corrected to say the cap is reported and not enforced. |
| D30 the lever is information density per wave | AS SPECIFIED | `ResultPackage` built first, as the build order says. |
| D31 submission is atomic and precedes packing | AS SPECIFIED | `Submission`, `AdmissionController`, `AdmissionDecision`. |
| D32 every unhandled configuration aborts, and **we** abort it | AS SPECIFIED | `DownloadConfigurationPolicy` + `DownloadAbortedByPolicyException` (throw from the delegate); `EscalationLadder`, `EscalationRecord`, `AttributionOutcome`, `LadderAttempts`. |
| D32 caveat 1 — attribution structurally impossible for project-level configurations | AS SPECIFIED | `AttributionOutcome` separates "no block is responsible" from "the locator failed". |
| D32 caveat 2 — the ladder can loop; a second abort is a TOTAL TEST ABORT | AS SPECIFIED | `LadderAttempts`, bounded; `EscalationOutcome`. |
| D32 caveat 3 — Class A/B/C decides the rung; `StartModules` needs a per-entry rung | AS SPECIFIED | `ConfigurationClassifier`, `ClassAEntry`, `ClassBEntry`, `LadderRung`, `UnknownReason`; `MutantClassifiers` mutation doubles in the tests. |
| D33 inert = the next test's start state, dynamics untriggered, resets **held** | AS SPECIFIED | `InertPhase.Establish` lowers all start bools as one level write, then verifies twice. |
| D34 the wave alternates inert/test unconditionally | AS SPECIFIED | `WaveRun`; a failing slot does not divert the sequence. |
| D35 coverage per **assertion**, enumerated from the specification | AS SPECIFIED | `Ladder.Wave/AssertionEnumeration`, `CoverageAnalyser`, `CoverageBucket`; `AssertionId` content-derived. |
| D36 O11 and O14 are parameters to be MEASURED, never constants assumed now | AS SPECIFIED | `WaveSetAdmission` refuses a cap with no provenance; `MirrorGeometry.RetentiveBytes` has **no default**, explicitly on D36's grounds. |
| D37 each vector carries a start bool gating the **start condition**, on a later scan | AS SPECIFIED | `InertPhase.Commit` requires a scan strictly later than the verify; gate 7 enforces one per slot, bound by name. |

## §4 — DESIGN BLOCKS DB-1..DB-7

| item | bucket | evidence / consequence |
|---|---|---|
| DB-1 change tracker **and router** — which/what class/blast radius | AS SPECIFIED | `ChangeRouter`, `ChangedObject`, `DependencyClosure`, `DeployedProgram`. |
| DB-2 test/block dependency; results carry a validity stamp | AS SPECIFIED | `ValidityStamp(ProgramVersion, MapHash, Caveats)` — `ResultPackage.cs:42`. |
| DB-3 download tracking, settling window | AS SPECIFIED | `Harness.Device/DeploymentPlan`, `VersionCheck` makes the window observable. |
| DB-4 the ≤20-object limit; dependency-closed batches | AS SPECIFIED | `BatchPlan`, `WaveBoundaryBatchPlanner`, `ClosureViolation`, `CpuStopRequirement`. ⚠️ **AS SPECIFIED WAS TRUE OF THE CODE AND SAID NOTHING ABOUT WHETHER ANYTHING RAN IT — and until 2026-08-17 nothing did.** Outside its own tests the planner's name occurred only in prose and two doc comments; `WaveSetAdmission.Admit` does not call it, and `src/harness` does not reference the assembly at all. Entry point now built: **`wave-cli batch`** (`WaveControl.Cli/BatchCommand.cs`), driven against the real corpus — `docs/notes/db-4-batch-verb.md`. ***A row in this column is a claim about what is BUILT; read it as a claim about what is REACHABLE and it is wrong.*** |
| DB-5 two projects: IR dev and test environment | AS SPECIFIED | Process; `Harness.Device/ScratchAllowlist` fences which project may be written. |
| DB-6 isolation by construction (version check per batch; map hash; unaddressable out-of-region write) | AS SPECIFIED | `RegisterMap.MapHash` folded into `BuildStamp`; `MirrorWriteTarget` has a private ctor and **no factory that produces a result region** — pinned by reflection in `MirrorWriteTargetTests`. |
| DB-7 cleanup | AS SPECIFIED | `Harness.Results/Cleanup.cs` — `CleanupEligibility`, `ReferenceGraph`, `RemovalKind`, graph-proven, recorded. **Its claim-release half is NOT BUILT** — see §16.12. |

## §4a — DESIGN BLOCKS DB-8..DB-13

| item | bucket | evidence / consequence |
|---|---|---|
| DB-8 the result package (7 named contents) | AS SPECIFIED | `ResultPackage`/`ResultPackageBuilder` carry observed-vs-expected per assertion, `Basis`, fidelity, `StimulusCheck`, co-running slice, `ValidityStamp`, `ManifestPresence`. |
| DB-9 the submission unit, atomic | AS SPECIFIED | `Submission`, `AdmissionEvidence`, `AdmissionDecision`; partial = refusal with reasons. |
| DB-10 agent scheduling | **NOT BUILT** | Deliberately demoted by the spec. *Consequence: none at a minute a wave; agents wait.* |
| DB-11 the rig pool | **NOT BUILT** | Deliberately demoted. *Consequence: one rig, one test project — which is what the version register requires anyway.* |
| DB-12 time compression, narrowed | **DIVERGED** | Built (`TimeCompression`) with **four** ceilings — assertion, timer, model, **ratio distortion** — and the measured timer floor `5 × 23.33 = 116.65 ms`, so a 500 ms preset caps at **4.29×**, not the ~10× the spec assumed. §12a derivation 5 / §16.4 corrected. Levers A/B (setpoints, tick period) are **NOT BUILT**. |
| DB-13 wave-set admission | AS SPECIFIED | `WaveSetAdmission` colours slots into wave sets; `Verify` is public *because deleting it as a private loop left the suite green*. |

## §5 — DESIGN RULES

| item | bucket | evidence / consequence |
|---|---|---|
| R1 OB set pre-allocated; OB change is deferred, not forbidden | AS SPECIFIED | `ChangeRouter` routes OB add/delete/property to the deferred queue. |
| R2 shared UDTs frozen for a wave set | 🔴 **NOT CHECKED** | See D16. |
| R3 stop-class handling is two-stage | AS SPECIFIED | See D17. |
| R4 never write a delegate that accepts everything; the guard is "refuse to answer" | AS SPECIFIED | `DownloadConfigurationPolicy` + throw; `ConfigurationResponse` has no accept-all. |
| R4a never infer safety from the current selection (zero values differ in direction) | AS SPECIFIED | `ClassAEntry`/`ClassBEntry` are enumerated per selection, not derived from a zero value. |
| R5 never use folder-scope downloads | AS SPECIFIED | `DownloadOption` offers device scope only. |
| R6 admission control | AS SPECIFIED | `AdmissionController`. |
| R7 `Software` (all) only at the disruptive boundary | AS SPECIFIED | `DownloadMode`. |
| R8 the sanctioned disruptive exception; **confirm RUN at the device once, by eye** | **DIVERGED** | The eyeball requirement is **retired**: `Harness.S7/S7RunState.Decode` keys on `== 8` and the rig answered `Running (8)` on **2026-08-14** after the deploy. §16.11 already predicted this closer; R8 corrected to say it is mechanised and exercised. |

## §6 — `%MW` IS STRUCTURAL

| item | bucket | evidence / consequence |
|---|---|---|
| §6 the mirror is `%MW`, not a DB — four arguments plus the measured layout revert | **DIVERGED** | Held, and **strengthened by the deployed form**: `MB_HOLD_REG := P#M1000.0 WORD 35` is an **area pointer over marker memory**, so no DB is on the wire at all and the `Array[..] of Struct` the spec's sample block implied never arises. `MirrorGeometry` refuses an odd base and a base inside the retentive window. §6 corrected. |

## §7 — WHAT THE TEST ENVIRONMENT REPLACES (COVERAGE)

| item | bucket | evidence / consequence |
|---|---|---|
| 7.1 the unit is the spec-derived assertion, qualified by instance | AS SPECIFIED | `Ladder.Wave/AssertionEnumeration`, `CoverageUnit`, `VectorCitation`. |
| 7.2 the four buckets, exactly one each | AS SPECIFIED | `CoverageBucket`, `BucketAssignment`, `AssignmentDefect`. |
| 7.3 the gate is `UNCLASSIFIED = 0`, computed as a set difference, and it stops the **claim** not the run | AS SPECIFIED | `CoverageAnalyser`, `ClaimVerdict`, `CoverageFigure`. |
| 7.4 IDs are `REQ-014:3f9a1c` — clause ID + hash, nothing positional; **who performs the enumeration is unruled** | **DIVERGED** | `AssertionId` is content-derived and gate **3f** refuses a display ordinal; gate **3g** recomputes every ID. The open question is **ruled and enforced**: gate **3d** requires the enumerator to be neither the block author nor a vector author. §7 corrected. |

## §7a — FAILURE ATTRIBUTION

| item | bucket | evidence / consequence |
|---|---|---|
| 7a.1 five causes; the free evidence attached to every result | AS SPECIFIED | `CoRunningLog`, `ValidityStamp`, `ManifestPresence`, `StimulusCheck` (the stimulus discriminator, `StimulusCheck.cs:128`). |
| 7a.2 hard rule: a failing test may not be closed by changing the block until the basis is re-confirmed | **NOT BUILT** | No mechanism. *Consequence: the worked example — engineering a correct pre-act offset out of a block — is prevented only by discipline.* |
| 7a.3 order of elimination: deployment, interference, vector, model, then block | AS SPECIFIED | `ResultPackage`'s verdict precedence — *admissibility, then liveness, then the run, then settling, then content* (`ResultPackage.cs:104`). |

## §8 — THROUGHPUT

| item | bucket | evidence / consequence |
|---|---|---|
| 8.1 the wave cost model and its three worked rows | 🔴 **NOT CHECKED** | **No wave has ever run**, so none of the rows has been validated. *Consequence: "a wave is order-of-a-minute" remains a prediction, and every throughput argument in the document descends from it.* |
| 8.2 the wire term `K × ceil(W/123) + P × ceil(K/R) + 1` | AS SPECIFIED | `WireTiming.RoundTripsPerIndex` (`WireTiming.cs:123`), same expression, same F-1 read term. |

## §9 / §9a / §9b / §9c — VERSION REGISTER, MANIFEST, OPTIONS, PARSER

| item | bucket | evidence / consequence |
|---|---|---|
| §9 the version register, `%MD`, two registers, three purposes | AS SPECIFIED | `BuildStamp`, `RegisterMap.VersionRegisters = 2`, `VersionCheck`; live stamp `16#21D74D35` read at registers 0–1. |
| §9a the load manifest — per-object, report the COUNT | AS SPECIFIED | `Ladder.Download.DownloadFeedback.LoadedObjects` / `LoadedObjectCount`; 45 objects loaded by name on the 2026-08-14 deploy. |
| §9b the three download options table | AS SPECIFIED | Unchanged; the pinned fixtures (`differential-one-object`, `full-ninety-nine-objects`, `hardware-three-wordings`) are the same runs. |
| §9c the download feedback parser — 5 extractions, 6 design rules | **DIVERGED** | Built as `src/download-feedback/` and it **excludes both forbidden readings by construction**: `state=Success` is reported and never consulted, and transfer is never inferred from an absent phrase. ***It is not wired into `openness-cli`*** — `DownloadProbe/TransferVerdict.cs` still carries a duplicate. The harness gateway does use it. §9c corrected. |

## §10 — PROTOCOL COEXISTENCE

| item | bucket | evidence / consequence |
|---|---|---|
| §10 disconnect → download → reconnect → version check | AS SPECIFIED | `LoopRun` steps 5–6. |

## §11 — MODELS, INERT, FAILURE HANDLING, PROVENANCE, WORD ORDER

| item | bucket | evidence / consequence |
|---|---|---|
| M1 an independent `lad-coder` authors the model | AS SPECIFIED | Same rule as D6; gate 2's identity machinery covers it. |
| M2 the physical analysis is grounded in the equipment, not the control spec | **NOT BUILT** | No mechanism, and none is possible mechanically. *Consequence: the spec's blind spots stay untestable; this is the residual §16.9 names.* |
| M3 every model carries a fidelity declaration + scan-time budget | AS SPECIFIED | `FidelityDeclaration` (`Admissibility.cs:148`); reproduced in the result package. |
| M4 a vector may only assert behaviours the model claims | AS SPECIFIED | Gate 4 (`SubmissionGate.cs:283`), `FidelityExceeded` / `FidelityUnusable` / `NothingExamined`. |
| M5 a model gets **more** review than a block | 🔴 **NOT CHECKED** | I did not verify any model has been through the review path. *Consequence: a wrong model silently corrupts every vector that uses it.* |
| M6 models compete for scan time; declared budget | 🔴 **NOT CHECKED** | I did not find a scan-budget consumer for models. *Consequence: the ceiling is discovered as a cycle-time trip mid-wave rather than before.* |
| M7 models are provisional until reviewed and validated | 🔴 **NOT CHECKED** | No promotion mechanism examined. |
| §11 inert state — a generated reset routine, read back and confirmed | AS SPECIFIED | `InertPhase`; `InertOutcome.{Established,StartConditionsWrong,NotQuiescent,ScanCounterStalled}`. |
| §11 failure handling — generate OB80; "block currently executing" register; enable bitmask | **NOT BUILT** | No OB80 generator anywhere in `src/`; OB80 appears only as a number-band *exemption*. No executing-block register, no enable bitmask. *Consequence: an overrun STOPs the CPU, and unattended one bad block turns a wave into a dead night — the exact cost asymmetry §11 argues from.* |
| §11 provenance — `TestVector.Basis` required | AS SPECIFIED | Gate 3; `Basis` carries clause **and** assertion. |
| §11 byte and word order — build swap as a configurable transform and **CALIBRATE**, one measurement, once | **DIVERGED** | ***Calibrated. It is HIGH-WORD-FIRST.*** The unambiguous measurement is **2026-08-13** on the phase-2 build: `16#00001111` read back as reg0 `0x0000` / reg1 `0x1111`, cross-checked against the scan counter (745 versus an absurd 48,824,320). **Corroborated 2026-08-14** on the deployed 35-register mirror: the build stamp `16#21D74D35` — halves `0x21D7` / `0x4D35`, distinguishable constants — was read at registers 0–1 and **matched the literal the copy layer writes**, which it could not have done under the other order. §11 corrected. 🔴 **Two caveats, recorded rather than smoothed:** one lane report states the word-order transform *"was never applied — high-word-first is carried forward unused"*, and **no lane report records the `wire-prediction.md` §2 calibration suite as having been run**; and `src/harness/Harness.Wire/RegisterWordOrder.cs` still declares the transform **UNCALIBRATED** (a stale comment on the harness lane's file, not mine to edit). |

## §12 — THE OBSERVABILITY FLOOR

| item | bucket | evidence / consequence |
|---|---|---|
| 12.1 a one-scan event is invisible to any sampler; no polling rate recovers it | AS SPECIFIED | `WireTiming.ObservabilityFloorScans`; holds at 3.0 / 4.4 / 8.6 scans — a claim that survives every constant. |
| 12.2 the copy layer provides a **free-running scan counter**, always | AS SPECIFIED | `HX_ScanCount : DInt @ %MD1004`; generated (`CopyLayerGenerator.cs:218`). |
| 12.3 **latched transients, default on for coil-shaped signals** | **DIVERGED** *(re-bucketed from NOT BUILT 2026-08-14 — `ca9819b`, `4738e21`, `b9bc470` landed after this document was written)* | **The generator now emits per-signal latches.** `CopyLayerGenerator`'s `ResultLatch` network emits `SCOIL <latch> := <slot start bool> AND [<armedBy>] AND <signal>` and `RCOIL <latch> := NOT <slot start bool>` — RCOILs after SCOILs so **reset dominates**, and on a **level, never an edge**, so a harness restart mid-run does not take the evidence with it. `MirrorObservability.FromBindings` derives `Latched` **with provenance**, and says whether it was *derived* (generated) or *taken on trust* (`LatchedBy` names a block). Round-tripped through the real converter — `SCoil`/`RCoil` parts in SimaticML, byte-identical read-back (`CopyLayerConverterRoundTripTests.cs:157`). 🔴 ***THE RESIDUAL DIVERGENCE IS THE POLARITY, AND IT IS THE OPPOSITE OF THE SPEC'S.*** The spec says **default ON for coil-shaped signals**; the implementation is **OPT-IN** — `Transient` defaults `false`, and `MirroredSignal.Bool(tag)` creates a **non-latched** Bool. The code argues that default deliberately (`CopyLayer.cs:228`): forgetting a transient yields no latch, so a `Latched` expectation is **refused by gate 5** — loud, at the gate, before anything is spent — where over-latching would be the silent direction. ⚠️ **That argument holds for `Transient` and NOT for its neighbour `RearmsEachIndex`**, whose omission yields a one-shot latch that compiles, deploys and reads plausibly (see the working agreement's two-flags entry). **Still true:** the four `HBA_Violation_*` latches on the rig are a **hand-authored** `FB_HarnessViolationLatch` (FB 9003) — ***and no GENERATED latch has been shown running on the rig; the evidence is a converter round trip, not a device.*** |
| 12.4 **event scan-stamps**, opt-in per signal | **NOT BUILT** | Same source. `InstrumentationMode.Stamped` is declarable and refused. *Consequence: "when, relative to T=0" is unanswerable, so coincidence and ordering assertions are inadmissible.* |
| 12.5 T=0 is the start bool's rising edge | AS SPECIFIED | `InertPhase.Commit`; stamps would be differences from it. |
| 12.6 the scan counter wraps; handle it in the subtraction | 🔴 **NOT CHECKED** | I did not verify wrap handling in a long test. |

## §12a — THE MEASURED TIMING BASIS

| item | bucket | evidence / consequence |
|---|---|---|
| `RTT_typ = 78` (duration-shaped only) | AS SPECIFIED | `WireTiming.RttTypicalMs = 78`. |
| `RTT_p90 = 102.79` (duration-shaped only) | **DIVERGED** | ***There is no `RttP90` constant in the code at all.*** It exists only as an **anti-assertion** in tests (`Assert.NotEqual(102.79, TimeCompression.PollPeriodMs(1), 2)`). So the kind rule is enforced by making the p90 **unavailable**, which is stronger than the spec's "MAY key on" — and the consequence is that **no duration-shaped figure is computed in code**. §12a corrected. |
| `exceed_250 = 0.352%` (an exposure rate; sizes nothing) | **NOT BUILT** | No consumer in code. *Consequence: the "expected over-threshold exchanges per operation" figure the spec asks a bound to be quoted with is not produced by anything.* |
| `RTT_p99 = 201` (bound-shaped only) | AS SPECIFIED | `WireTiming.RttP99Ms = 201`, with the 173→201 provenance in the doc comment. |
| `RTT_max = 2216` (timeouts only) | AS SPECIFIED | `WireTiming.RttMaxObservedMs = 2216`; the backstop's untrimmable last term. |
| `scan = 23.33` (loaded row) | AS SPECIFIED | `WireTiming.ScanPeriodMs = 23.33` (22.64 idle, n=101; 23.33 loaded, n=16). |
| The constants hold **on the path actually in use** — live reads min 63 / median 72 / max 106 ms, write fn6 68 ms, fn16 ×8 80 ms | AS SPECIFIED | ⚠️ **Corrected against the brief.** This is a **corroboration, not a re-derivation, and the record dates its heading 2026-08-13, not -14** — taken against the **old 121-register generator build**, over the Talk2m tunnel. The figures are *faster* than every recorded constant (median 72 vs `RTT_typ` 78; max 106 vs `RTT_p90` 102.79) and **no constant was moved on them**, which is the correct treatment. |
| **The F-5 kind rule** — duration-shaped may key on p90; bound-shaped MUST keep p99; `RTT_max` is the outlier term | AS SPECIFIED | Enforced at every bound: `BackstopMs`, `MaxTensorWidth`, `ObservabilityFloorScans`, `TimeCompression.PollPeriodMs`, and `ModbusPolicy` refuses a timeout below `RttMaxObservedMs`. |
| Derivation 1 — the floor in scans | **DIVERGED** | Built, and **quantised on ROUND TRIPS rather than slots**: `LoopRun.cs:110` passes `map.ReadPlan(...).Count` — i.e. `ceil(K/R)` reads — not the slot count. Measured: correcting `slotsInWaveSet` 6 → 1 moved the floor **not at all** (6 × 8 result registers = 48, inside one 125-register read), and every gate-5/10a figure came back byte-identical — *that identity is the evidence, not a suspicion*. A test pins the floor monotonically non-decreasing in slot count over 1..48. ***And the permissive direction is the opposite of the obvious one:*** over-declaring slots gives a floor that is too **high**; under-declaring is what admits an unobservable window. §12a corrected. |
| Derivation 2 — `K_max = R × floor(S_min × scan / RTT_p99)` | **DIVERGED** | Built as `WireTiming.MaxTensorWidth` but ***reported, never enforced***, on D36's grounds. §12a corrected. |
| Derivation 3 — the wire term | AS SPECIFIED | `WireTiming.RoundTripsPerIndex`. |
| Derivation 4 — the 2,216 ms outlier; ≥3,000 ms per-request timeout; the computed backstop | AS SPECIFIED | `PerRequestTimeoutMs = 3000`; `BackstopMs` has **no `int` overload**, so a bare scan count is unexpressible; `ModbusPolicy.Default` sets `Retries: 0` against NModbus's default of 3. |
| Derivation 5 — X-D's compression constants | **DIVERGED** | Timer floor `k × scan = 5 × 23.33 = 116.65 ms`; a 500 ms preset caps at **4.29×** (asserted to two places, `TimeCompressionTests.cs:54`, alongside `NotEqual(10.0)`; mutating the floor back to 50 ms reddens six tests), against the spec's rounded 4.3× and X-D's original 10×. §12a corrected to the computed figure. 🔴 **And the figure is HALF-measured, which the spec does not say: the scan term is `[M]`, `k ≈ 5` is X-D's own number and has never been measured.** Also: **no wave has ever run compressed** — every compression ceiling is arithmetic over measured inputs, exercised against the simulator only. |
| Derivation 6 — batching got stronger | AS SPECIFIED | `ModbusLimits.MaxReadRegisters = 125` / `MaxWriteRegisters = 123`; `SlotsPerRead = floor(125/Wr)`. |
| F-1 adopted — a read never SPLITS a slot | AS SPECIFIED | `RegisterMap.ResultRead(SlotSpan)`; **no register-range overload exists**, so a split read is unexpressible rather than validated. |
| F-2 DB-13's max-width input shape | **NOT BUILT** | Still a scalar input with a provenance requirement. *Consequence: one number per wave set must be the worst case, discarding most of the width the link sustains.* |
| F-3 should SAMPLED assertions be admissible at all | **NOT BUILT** *(bucket unchanged; consequence corrected 2026-08-14 — ***stale, and not on the brief***)* | Unruled. ⚠️ **The consequence here used to read *"sampled is ... currently the only mode the generator provides"*, which is no longer true** (§12.3 — `Latched` is generated as of `ca9819b`). *Corrected consequence: sampled is no longer the only mode, but it is still the **DEFAULT** one, because latching is opt-in — so F-3's correctness question stays live for every signal nobody explicitly declared `Transient`, which is all of them by default.* **The ruling is still owed**, and note that the closure made it *easier* to ignore: a question that once applied to everything now applies only to what nobody thought about, which is the harder class to notice. |
| F-4 answered — no width effect on the tail | AS SPECIFIED | Recorded in `WireTiming`'s own doc comment. |
| F-5 adopted — the tail as p90 + an exceedance rate | **DIVERGED** | ***The spec contradicts itself here and this pass corrects it.*** The constants block records `RTT_p90 = 102.79` and `exceed_250 = 0.352%` as **extracted 2026-08-13**, while F-5's own entry still says *"ADOPTION DID NOT PRODUCE THEM, SO THEY ARE STILL MISSING"* and that budgets key on the p99 *"until they are extracted"*. Both cannot stand. §12a F-5 corrected. |
| F-6 should admission group by slot size | **NOT BUILT** | Deferred. `SlotSizeReport` computes the collapse and surfaces it as `LoopCaveat("F-6-collapse-seam")` — ***a report, never a gate***. *Consequence: one 123-register slot in a set takes R from 6 to 1 and sextuples every read, and nothing refuses it.* |

## §13 / §13a — G-7

| item | bucket | evidence / consequence |
|---|---|---|
| §13 `DataBlockReinitialization` is raised on a restructured standard-access DB | AS SPECIFIED | Measured; unchanged. |
| §13a the historical pre-run plan | AS SPECIFIED | Superseded in place and marked DO NOT ACT ON — which is what the document does. |

## §14 — EXTERNAL UNKNOWNS

| item | bucket | evidence / consequence |
|---|---|---|
| G1 which V4+ CPUs lack download-without-reinit | 🔴 **NOT CHECKED** | External; I did not look for a resolution. |
| G2 whole-DB vs added-tags reinit; does a memory reserve protect retentives | **NOT BUILT** | Still open and now tracked as `docs/notes/deferred-items.md` "A8 / G2". *Consequence: DB-1's change-class table asserts "retentives included" and tags it `[R]` on exactly the half the vendor contradicts itself on.* |
| G3 does a hardware download wipe retentive data | 🔴 **NOT CHECKED** | External. |
| G4 does an aborted download leave the CPU half-loaded | **DIVERGED** | ***Materially answered for OUR abort, measured in both directions (A6).*** A throw from the **PRE** delegate leaves the CPU **RUNNING**; a throw from the **POST** delegate leaves it **STOPPED with a complete program**. ***Never half-loaded.*** Both also kill the Portal process; recovery is a 34 s download, measured. §14/§15a corrected. **What is still open is a TIA-caused abort**, which is what G4 literally asks and what nothing here exercises. |
| G5 online block editing — dropped deliberately | AS SPECIFIED | Recorded as dropped. |
| G6 `MB_HOLD_REG` refusing optimized DBs as a primary-sourced fact | 🔴 **NOT CHECKED** | Vendor sentence still not retrieved. **Materially defused**: the deployed mirror is an area pointer over `%M`, so no DB is on the wire and the question no longer gates anything. |
| G7 is a start-values-only edit RUN or RUN (Init) | 🔴 **NOT CHECKED** | External. |
| G8 closed | AS SPECIFIED | See §13. |

## §15 — WHAT REMAINS

| item | bucket | evidence / consequence |
|---|---|---|
| 15a.1 the throw-from-delegate abort, and VERIFY it leaves the CPU untouched | **DIVERGED** | Built (`DownloadAbortedByPolicyException`) and **verified in both directions** (A6, above): PRE leaves the CPU RUNNING, POST leaves it STOPPED with a **complete** program, never half-loaded. The spec's *"Until that is measured, D32 is a plan"* no longer holds. §15a corrected. |
| 15a.2 the download feedback parser | AS SPECIFIED | Built; both named defects excluded by construction. |
| 15a.3 the configuration classifier, Class A/B/C + an explicit unknown branch | AS SPECIFIED | `ConfigurationClassifier`, `UnknownReason`. |
| 15a.4 DB-13 + DB-8 first | **DIVERGED** | Both built — **and a driver was not**. `Harness.Loop` is a library with no entry point; the only executables are `harness-gate`, `rig-read`, `rig-write`. *Running a vector set end to end needs new PC-side code.* §15a corrected. |
| 15a.5 DB-9 | AS SPECIFIED | Built. |
| 15a.6 DB-12 narrowed | AS SPECIFIED | Built. |
| 15a.7 DB-10 / DB-11 last, and only if measurement says so | AS SPECIFIED | Not built, as intended. |
| 15a.8 measure O14 conflict density | **NOT BUILT** | Unmeasured. *Consequence: §8's 6× packing figure rests on tests packing 6–10 wide, and nothing has ever measured whether they do.* |
| 15a.9 measure O11's `S_min` | **NOT BUILT** | Unmeasured, and the deployed wave set has one slot. *Consequence: the cap's free variable has never been observed, which is why `MaxTensorWidth` reports and does not enforce.* |
| 15b.1 inert failing to establish | AS SPECIFIED | `InertOutcome` distinguishes it; treated as wave-blocking, not a test failure. |
| 15b.2 Class B/C configurations reach a human | AS SPECIFIED | `EscalationOutcome`. |
| 15b.3 blocks with no discoverable start condition | AS SPECIFIED | The contract's start-bool section; gate 7. |
| 15b.4 assertion decomposition drift | **DIVERGED** | The sticky key is **not** `(clause hash, assertion index, assertion text hash)` — the positional middle term was removed and IDs are `clause:hash6` with **nothing positional**, enforced by gate 3f. §15b still carries the superseded key; corrected. |
| 15b.5 the scan-counter wrap | 🔴 **NOT CHECKED** | See §12.6. |
| O3 closed — a download can restart the CPU | AS SPECIFIED | Re-confirmed 2026-08-14. |
| O4 closed — the fidelity declaration | AS SPECIFIED | M3/M4 built. |
| O5 closed — independent equipment-grounded model author | AS SPECIFIED | Rule in force; see M2 for the unbuildable half. |
| O6 closed — D34 | AS SPECIFIED | Built. |
| O8 closed — §7's denominator, buckets, gate | AS SPECIFIED | Built. |
| O9 ruled — D32; verification depends on G4 | **DIVERGED** | Policy built and exercised; verification still owed. Same correction as 15a.1. |
| O10 closed structurally | AS SPECIFIED | Nothing is left unhandled, so TIA never applies an unseen selection. |
| O11 deferred, arithmetic answered | AS SPECIFIED | See derivation 2. |
| O12 closed — D33 | AS SPECIFIED | Built. |
| O13 closed without answering it | AS SPECIFIED | Built. |
| O14 deferred | **NOT BUILT** | See 15a.8. |

## §16 — GAP ANALYSIS X-A .. X-L

| item | bucket | evidence / consequence |
|---|---|---|
| X-A.1 the start bools are the commit; a torn data write leaves them unraised | AS SPECIFIED | `InertPhase.Commit` raises all start bools in one write; `RegisterMap.SlotsPerStartRegister = 16`; `CommitTransactions => 1`. |
| X-A.2 a read never SPLITS a slot; reads addressed as `read(first_slot, slot_count)` | AS SPECIFIED | `RegisterMap.ResultRead(SlotSpan)` — no register offset exists to pass. |
| X-A.3 `MB_SERVER` is called FIRST in the scan, before any copy-layer read | 🔴 **NOT CHECKED** | I did not verify the call order in the deployed `Main`. *Consequence if wrong: a torn read ACROSS BLOCKS despite a perfectly atomic write.* |
| X-A.4 the `%M` ceiling — 8,192 bytes, separate from work memory, mirror above the retentive range | AS SPECIFIED | `MirrorGeometry.Cpu1214CBitMemoryBytes = 8192`; a base inside the retentive window is a refusal; `RetentionCheck` re-checks every generated tag. |
| X-A.5 `MB_SERVER` applies one request's registers within a single scan `[I]` | **DIVERGED** | ***The spec's own named open item is CLOSED.*** §16.1 still says A1's evidence *"was measured at 16 REGISTERS PER FC16"* and calls the gap to full width *"a PRE-EXISTING item, neither widened nor closed here."* It has since been measured **at 123 registers**: no tear in 3,000 writes (variant 1) and 3,200 (variant 2), and **no torn READ in 3,000 reads across 3,000 distinct generations**. Still *"no tear observed"*, never *"atomic"*. §16.1 corrected. |
| X-B per-test timeout; TIMED-OUT distinct from FAILED; the computed backstop | AS SPECIFIED | `SlotOutcome.{Completed,NotInert,TimedOut}`; `WireTiming.BackstopMs`; per-index backstop in `WaveRun.Observe` measured after re-expression at the wave's `comp`. |
| X-C coordinator death; on connect assume nothing; a persisted marker; discard interrupted results | AS SPECIFIED | `CoordinatorStateStore` (one atomically-renamed file, `WriteThrough` + flush before rename); legacy two-file form **refused, not read**; `WaveMarker`, `QueueRehydrator`. An unreadable file voids the wave **and** the queue — the 2026-08-13 ruling, implemented. |
| X-C residual — a PLC-side watchdog | **NOT BUILT** | Held open deliberately. *Consequence: nothing survives the PC dying; safe only while the rig's outputs cannot actuate (ADR-0009), and that dependency is the condition to revisit on.* |
| X-D comp_min/comp_max; take the minimum, never the ceiling | **DIVERGED** | Built with four ceilings and no member that returns `CompMax` as a recommendation. **The ceiling is lower than X-D assumed on the term X-D says binds first** — 4.29× at a 500 ms preset. §16.4 corrected. |
| X-E excision is transitive; closure + threshold + bounded attempts; publish the co-running log from executed start bools | AS SPECIFIED | `ExcisionClosure`, `ExcisionPlan`, `ExcisionDefect`; `CoRunningLog` built from the start **echo**; `RegisterMap` allocates a start-echo block; excision does not move `MapHash`. |
| X-F a STARTUP TEST class at the disruptive boundary; evidence must be LATCHED | **DIVERGED** *(bucket unchanged, REASON REPLACED 2026-08-14 — the blocker it named is gone)* | The class is built — `Harness.Results/StartupTests.cs`, `DisruptiveBoundary`, `StartupRefusal`. ⚠️ **This entry used to read *"latching is not generated (§12.3), so the one thing X-F says the evidence must be is unavailable"* — that is now FALSE:** §12.3 re-buckets to DIVERGED and latches **are** generated. **What remains divergent is narrower and must not be read as the old blocker:** (a) latching is **opt-in**, so a startup vector gets latched evidence only if its signal was declared `Transient` — nothing makes the startup class latched *by virtue of being* the startup class, which is what X-F asks for; and (b) **the startup class cannot be declared in the submission document at all** — `SLOT-HBA-STARTUP` is a naming convention, not a gate, and the coordinator must pass `startupVectors` out of band. So the evidence *can* now be latched, and **nothing checks that it is**. §16.6 corrected. |
| X-G conflict edges carry provenance; multi-writer on a deliverable signal is a FINDING | **DIVERGED** | Built as gate 8c with `ConflictProvenance`/`SignalClass` — and it **reports rather than refuses**, with the code recording that whether it should refuse is an open owner question. It also prints on a clean graph. Currently `NOT CHECKED` in practice because no provenanced graph is produced — ⚠️ **and that is now a SIGNAL-NAME JOIN problem, not a producer gap: `converter conflict-graph` exists and refuses correctly (16 of 17 submission signals resolved to no storage path), and D9's slot↔slot producer landed 2026-08-14. Gate 8c's block↔block graph still needs the submission's logical names bound to storage.** §16.7 corrected. |
| X-H withdrawn; per-slot instancing stands unconditionally; warn when two slots model one physical instance | **NOT BUILT** (the warning) | The rule stands; no equipment-identity warning exists. *Consequence: case 3 written as two case-2 slots passes against two independent tanks and the coupling is never exercised.* |
| X-I models go through their own test wave; four rules | AS SPECIFIED | `ModelReadiness`, `ModelReadinessCheck`, `ModelOrdering`, `OrderingDefect` (7 members, **none informational** — pinned over the whole enum). |
| X-I rule 2 — reading (b) permitted only once a stop-on-failed-wave-set gate demonstrably exists | **DIVERGED** | Built exactly as ruled: `RunLoopGateState` whose **zero value is `NotDeclared` and refuses**, with `DeclaredForADifferentRunLoop` kept separate from it, and `OrderingDefect.ReadingBRefusedBecauseTheGateIsNotEstablished`. **The gate is not established**, so (b) fails closed today. §16.9 corrected. |
| X-J a reserved number range for harness-generated objects, **and the claim tool refuses allocations inside it** | **DIVERGED** | ***The band is declared: 9000–9999, independently per number space, FB/FC/DB, OBs EXCLUDED*** (`HarnessNumberRange.Declared()`; `HarnessScope.ReservedBandLow/High` in the reviewer). It was measured rather than feared — TIA accepted an import declaring `FC 910` while another block held 910 and created **two blocks at that number**, with import, per-block compile, device compile and `sanity-check` all green. ***The second half is NOT BUILT: `converter claim --allocate` has no knowledge of the band and will not refuse an allocation inside it*** (`ClaimValidator.Candidates` takes a bare `--floor`). §16.10 corrected. |
| X-K a stopped CPU and a dropped link; `PlcGetStatus` over S7comm; key on `== 8` | AS SPECIFIED | `S7RunState.Decode` keys on `RunValue = 8`; `S7RunState` has **no `Stopped` member** and a pinning test fails if one is added. Re-confirmed on the rig **2026-08-14**: `Running (8)`. |
| X-K residual — S7 variable access refused CPU-wide; all data reads go over Modbus | AS SPECIFIED | Re-measured 2026-08-14: `DBRead(38)` and `MBRead(0)` both `0x00040000` while SZL answers. |
| 16.12a stable-but-unexpected version value must refuse to test | AS SPECIFIED | `VersionOutcome` separates it from "still settling" and from `WordOrderSuspect`. |
| 16.12b models and instance DBs share the ≤20 budget | AS SPECIFIED | `BatchPlan` counts them. |
| 16.12c every removal path must release its claim | **HALF BUILT** (2026-08-17) | `Cleanup` computes eligibility; **`harness-cleanup` now reads the shared claims store and emits the exact `converter claims --release` command per removal**, with `Held` / `NoClaimHeld` / `NotChecked` kept apart. It does **not run it**, on purpose: that binary cannot delete, and a release ahead of the deletion hands the number to the next allocator while the block is still in the project. *Consequence: the leak is narrowed to a human step between delete and release, not closed.* |
| X-L retain is a HARD restriction; work/load/block-count are governed by minimality; read the budget, never hard-code | AS SPECIFIED | `RetentionCheck`/`RetentionVerdict`; `MirrorGeometry.RetentiveBytes` has **no default**; the mirror is placed above the retentive window because a `%M` tag carries no per-tag retain flag at all. |

---

## §NC — THE 21 `NOT CHECKED`, NAMED

Nothing below is a pass. Each is something this reconciliation did not reach.

1. **2.2d / D16 / R2** — shared-UDT freeze for a wave set *(3 items)*. Nothing looked for the freeze.
2. **§8.1** — the wave cost model's three worked rows. No wave has run.
3. **M5** — models get more review than blocks.
4. **M6** — model scan-time budgets have a consumer.
5. **M7** — model promotion is a decision.
6. **§12.6 / 15b.5** — scan-counter wrap inside a long test *(2 items)*.
7. **G1, G3, G7** — external vendor unknowns; no search for a resolution *(3 items)*.
8. **G6** — `MB_HOLD_REG` refusing optimized DBs, primary-sourced.
9. **X-A.3** — `MB_SERVER` called first in the scan, in the deployed `Main`.
10. Plus the six residual rows marked 🔴 in the tables above that are not restated here: §2.2d's siblings are counted once each, and the totals in THE COUNTS are computed from the tables, not from this list.

> The honest shape of this bucket: it is dominated by **process rules with no artifact** (M5/M6/M7, the
> UDT freeze) and **external vendor questions** (G1/G3/G6/G7). The one that would most repay ten
> minutes is **X-A.3** — a checkable rule about call order in a block that is on the controller right
> now, whose failure mode is a torn read across blocks behind a perfectly atomic write.

---

## §X — DIVERGENCES FOUND THAT WERE NOT ON THE BRIEF

1. ⚠️ ***PARTLY OVERTAKEN 2026-08-14 — HALF OF THIS ITEM IS CLOSED AND THE OTHER HALF IS NOT.***
   **The struck half, kept visible because a reader who remembers this finding must be able to see it
   was RULED ON rather than quietly dropped:** ~~latched transients are not generated~~ — they are, as
   of `ca9819b` / `4738e21` / `b9bc470`; §12.3 is re-bucketed **DIVERGED** and X-F's *"requires latched
   evidence it cannot have"* no longer holds. **The half that stands:** 🔴 ***event scan-stamps are
   still not generated*** (§12.4, NOT BUILT), so *"when, relative to T=0"* is unanswerable and
   coincidence and ordering assertions stay inadmissible. **And a NEW divergence this closure created,
   which is the reason to re-read rather than tick:** the spec says latching is **default on for
   coil-shaped signals** and the implementation made it **OPT-IN** — *** THE OPPOSITE POLARITY ***, so
   a signal nobody thought about is SAMPLED, exactly as before. The poll-gap exposure and O11's cap
   therefore still bind **by default**; what changed is that they are now **escapable per signal**
   rather than unavoidable. The four `HBA_Violation_*` latches on the rig remain hand-authored IR, and
   **no generated latch has yet been shown running on a device.**
2. 🔴 ***§12a contradicts itself on F-5.*** The constants block records `RTT_p90 = 102.79` and
   `exceed_250 = 0.352%` as extracted, while F-5's own entry says they *"are still missing"* and that
   budgets key on the p99 *"until they are extracted"*. Both cannot stand, and the second reads as a
   temporary state when the kind rule makes the p99 **permanent** for bounds.
3. ***The kind rule is enforced by ABSENCE, which is stronger than the spec asks.*** There is no
   `RttP90` constant in the code; 102.79 exists only as an anti-assertion in tests. A p90 cannot be
   substituted into a bound because it cannot be named. The cost is that **no duration-shaped figure
   is computed anywhere** — §8's planning numbers have no implementation.
4. ***X-J's second half is missing.*** The band is declared and consumed by two independent
   readers, but `converter claim --allocate` does not refuse allocations inside it — the exact
   mechanism X-J's treatment named.
5. ***The observability floor is quantised on READS, not slots.*** `LoopRun` passes
   `map.ReadPlan(...).Count` — `ceil(K/R)` — to `ObservabilityFloorScans`, so F-1's factor divides the
   floor as well as the read cost. The spec's derivation 1 says *"multiply both by K"*.
6. ***`D27` still says "pack tests into the fewest tensors"*** while DB-13 corrects it to colouring
   **slots into wave sets**. The code implements DB-13. A reader arriving at D27 first gets the
   superseded model with no marker.
7. ***`§15b.4` still carries the superseded positional sticky key*** `(clause hash, assertion index,
   assertion text hash)` that §7's own correction removed and gate 3f refuses.
8. ***`§9c`'s parser is built but not wired into `openness-cli`.*** `DownloadProbe/TransferVerdict.cs`
   carries a duplicate implementation. The harness gateway does use the real one.
9. ***`§11`'s OB80 half is entirely unbuilt*** — no generator, no executing-block register, no enable
   bitmask — while `§2.1` lists OB80 among the things the coordinator generates.
10. ***No driver exists.*** `Harness.Loop` is a library with no entry point, and `LoopRun.Execute`
    **always deploys** (step 5 calls `gateway.Deploy` unconditionally). Running an already-deployed
    vector set requires new code and a design decision about what a gateway may assert — *a gateway
    that reports `Loaded` without loading is one edit away from a gateway that lies.*

11. ***Gate 5's authority differs between its two paths, and the weaker one is the one an author runs.***
    `harness-gate check <submission.json>` reads `document.Map.ProvidedFor` — **inside the submission
    the vector author wrote** — while `LoopRun.Execute` reads the **coordinator's** bindings. *A
    declaration checked against itself is the failure §7's whole denominator argument exists to
    prevent, appearing one level out — and it is consulted first and believed.*
12. ***Gate 11 exposed a schema gap it cannot resolve.*** *"The S7 map reaches no DB"* and *"there is
    no S7 transport in this run"* are different claims and the document can only express the first, so
    the gate demands a tag map to verify a claim a map would be meaningless for. **No tag map exists
    on disk.** This is `null` vs `[]` care, one level further out.
13. ***A measured harness defect against contract §2.3:*** a wave at `runtimeCompression = 8` whose
    `comp_min` is 1 **passes with no `comp_stable` declared at all**. The contract's rule is the
    **runtime** factor; until it is fixed, a green on that bound at `comp_min = 1` is worth less than
    it looks.
14. ***D6's gate was passed by whitespace.*** Identity was `StringComparison.Ordinal` on an unspecified
    string, so `"Agent-A "` was *independent of* `"agent-a"`. The one check the pipeline exists for,
    defeated by a trailing space.
15. ***The element table was forced by a defect, not designed ahead of it.*** A hard-coded `"Int"`
    reached the controller and TIA refused it — `Data type Bool is not permitted here` — because
    **`MOVE` will not take a `Bool` into an `Int` on an S7-1200**, and *every* signal both conformance
    vector sets observe is a `Bool`. The table (Bool→COIL, Int/Time→MOVE, width derived from the
    address form) is the repair, corroborated against TIA's own exports (`FC_Outputs`: 18 Coil parts,
    zero Move).
16. ***The mirror's width was derived by counting the wrong thing.*** 16 signals were read as 16
    registers; ten of them are 32-bit, so the span went `8 → 31 → 35`. *The gap between those two ways
    of counting is the defect.*

### And two stale artifacts found by this pass, on files this lane may not edit

- 🔴 `src/harness/Harness.Wire/RegisterWordOrder.cs` still declares the transform **UNCALIBRATED** and
  its default *"an inference, not a measurement"*. It was measured, twice.
  `src/harness/Harness.Map/MirrorGeometry.BitAddressOf` likewise still carries `[I] — INFERRED, NOT
  MEASURED` for the bit-within-register order, which was settled by writing `0x0001` and `0x0100`.
- 🔴 `src/harness/Harness/TestVector.cs:11` (the legacy `Harness` project, **0 tests**) still documents
  `RTT_p99 = 173 ms` and *"7.4 scans"*. It is the pre-2026-08-13 constant, in a project nothing tests.

---

## METHOD, SO THE WEAK ROWS ARE VISIBLE

- The spec was read in full — all 4,528 lines — in this pass.
- Code evidence is first-hand for: the instruction registry, the reserved band, the mirror IR and its
  `MB_HOLD_REG` pointer, `WireTiming`, `TimeCompression`, `MirrorGeometry`, `MirrorClient`,
  `HarnessScope`, `HarnessNumberRange`, `ModelOrdering`, `RunLoopGate`, `ClaimValidator`, and the gate
  labels in `SubmissionGate`.
- Code evidence is **second-hand, from a survey agent**, for the fuller type inventories of
  `Harness.Map`, `Harness.Results`, `Harness.S7`, `Harness.Device`, `device-guard` and
  `download-feedback`. Rows resting only on that are marked AS SPECIFIED on a type's existence, which
  is weaker than a behavioural check and is not claimed as one.
- Measurement evidence is cited to `docs/notes/test-environment-build-plan.md`, which **quotes the
  implementation** and is therefore treated as evidence, never as spec.
