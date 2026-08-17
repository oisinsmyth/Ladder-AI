# DB-4 GETS AN ENTRY POINT, AND THE 45-OBJECT DEPLOY GETS AN ANSWER

**2026-08-17.** `wave-cli batch` — `src/wave-control/WaveControl.Cli/BatchCommand.cs`, 23 tests in
`WaveControl.Tests/BatchCommandTests.cs`, producer `tools/derive-db4-change-set.py`.

---

## 1. THE STATE THIS STARTED FROM: BUILT, TESTED, AND UNREACHABLE

`WaveBoundaryBatchPlanner` and `BatchPlan` implement DB-4 — *at most twenty dependency-closed objects
per download* — and **no executable reached them.** Verified by grep over `src/`, excluding the
planner's own tests:

- `AdmissionController.cs:44`, `ExcisionClosure.cs:88`/`:206`, `WaveSetAdmission.cs:466` — **all four
  are doc comments.** `WaveSetAdmission.Admit` does not call the planner.
- `src/harness/**` and `src/openness-cli/**` do not reference the `WaveControl` assembly at all
  (only `src/converter` does, and only for `HarnessNumberRange`'s declared band).
- `wave-cli` exposed `submit` and `status` (and `reset`).

***`spec-reconciliation.md` recorded DB-4 as `AS SPECIFIED`, which was true of the code and silent
about whether anything ran it.*** That row has been corrected.

## 2. THE VERB

```
wave-cli batch --change-set <file.json>
               (--deployed <file.json> | --deployed-empty <reason>)
               [--max-objects <n> --max-objects-provenance <text>]
               [--json]

EXIT: 0 planned · 1 refused · 2 unusable (nothing was examined)
```

Design points that are not decoration:

- **THE DENOMINATOR IS PRINTED ON EVERY PATH** — `EXAMINED: <n>`, `BASELINE: <n>`, `LIMIT: <n>`, each
  with its provenance. Every other number the verb prints is a reason a batch did *not* happen.
- **AN EMPTY CHANGE SET IS EXIT 2**, with `NOTHING TO BATCH - this is not a pass`. Zero changed
  objects is also what a broken change tracker produces (FI-44).
- **THE LIMIT MAY BE LOWERED AND NEVER RAISED.** `--max-objects 45` is exit 2 *before any file is
  opened*, and prints nothing on stdout — so a plan of one batch of 45 is unreachable through this
  verb by construction, not by a check that could be argued with. The library still accepts a larger
  limit (a test needs to build an over-budget plan cheaply), so `BatchCommand.OverBudgetBatches` is a
  backstop that is **exposed to be run rather than read**, and is tested in both directions.
- **ABSENT IS NOT EMPTY, AT TWO LEVELS.** A document with no `objects` key is a refusal (it never
  said); `"objects": []` is a claim and reaches the planner. A `kind`/`changeClass` that is *absent*
  reads as `Unknown` and is refused by the planner with the planner's reason; one that is *present
  and unrecognised* is refused by the reader, naming the token.
- **DEPENDENCIES HAVE A PRODUCER.** `ChangedObject` says outright that dependencies are declared and
  a wrong list yields a wrong plan — the shape D9 forbids for reachable state. So the documents carry
  a required `provenance`, and `tools/derive-db4-change-set.py` derives them from committed
  artifacts rather than from an agent's memory.
- **The caller-side closure re-check is NOT an independent authority** and the output says so: it is
  the same `DependencyClosure` the packer used, so it checks the *plan*, not the *definition*.

### What the producer reads, and what it refuses to decide

| | from |
|---|---|
| kind | the object's own IR declaration (`BLOCK FB/FC/OB`, `DB`, `TYPE`, `TAGTABLE`) |
| name | **the DECLARED name, not the filename** — `DefaultTagTable.ir` declares `Default tag table` |
| iDB → FB | the iDB's own `INSTANCEOF` line |
| anything → UDT | a quoted `"UDT_x"` that a corpus `TYPE` declares |
| caller → callee, caller → iDB | `converter cross-check --json` `siblingRefs.calls` / `.instanceDbRoots` |
| **change class** | ***NOT DECIDED HERE.*** DB-1 owns it and DB-1 is not built. Only the classes the spec FIXES are applied (an OB is STOP by R1, whatever the caller asks); everything else is stated on the command line. An object with no `.ir` gets **no kind and no class** — the fields are omitted so they read `Unknown` and are refused, rather than defaulted into something plausible |

## 3. THE REAL RUNS

Corpus: `ir/test-project001`, **43 `.ir` files, counted rather than quoted.** Baseline: those 43 plus
the two objects the 2026-08-14 reconciliation found in the controller with no `.ir` (`MotorIOSet`,
`MotorVSDIOSet`) = **45**, which independently reproduces `export-all`'s recorded 45.

| run | input | result |
|---|---|---|
| **A** | the 4 DRIFTED objects vs the 45-object baseline | **exit 0** — 1 batch of 4, closure 0 violations, batch flagged `RESETS DATA` |
| **B** | the same 4 **plus** the 2 with no `.ir` | **exit 1** — `StopClassObjectInAWaveBoundaryBatch [MotorIOSet, MotorVSDIOSet]`, both `(Unknown)` |
| **C** | **the 2026-08-14 deploy's 45 objects** as a wave-boundary change set | **exit 1** — `StopClassObjectInAWaveBoundaryBatch [Main, OB100, MotorIOSet, MotorVSDIOSet]` |
| **C2** | the RUN-class remainder (41) | **exit 0** — 3 batches (20/20/1), closure 0 violations |
| **C3** | counterfactual: the 43 with both OBs declared RUN | **exit 1** — `DependencyGroupExceedsObjectLimit`, **34 objects in one dependency group** |
| **D** | `"objects": []` | **exit 2** — `NOTHING TO BATCH - this is not a pass` |
| **E** | `--max-objects 45` | **exit 2** — `NOTHING WAS EXAMINED`, stdout empty |

## 4. 🔴 THE RULING: THE 45-OBJECT DEPLOY AND DB-4's TWENTY

**The question:** the 2026-08-14 deploy moved 45 objects in ONE download. DB-4 says more than 20
changed objects cannot be integrated consistently in one program cycle. Did that deploy bypass DB-4,
or does DB-4 not apply to a full download the way it applies to a delta?

### ***BOTH. THEY ARE SEPARATE FACTS AND ONLY ONE OF THEM IS BENIGN.***

**(a) THE DEPLOY BYPASSED DB-4 ENTIRELY, IN THE PLAINEST SENSE: NOTHING CONSULTED IT.** `Harness.Device`
does not reference `Ladder.Wave`, the planner had no caller anywhere, and no budget check existed on
the deployment path. This is true regardless of what the rule says, and it would still be true of a
delta download. ***The rule was not overridden; it was never asked.***

**(b) AND THE RULE WOULD NOT HAVE BOUND THAT DOWNLOAD ANYWAY — already ruled 2026-08-13
(`test-environment-build-plan.md` §"DB-4's TWENTY DOES NOT APPLY TO THE DRAIN"), and this run
corroborates it from a direction that ruling did not use.** DB-4's stated mechanism is consistent
integration *"in one program cycle"* — DB-3 writes it in Siemens' own notation as `RUN (<21)` — and
the hazard is a program **running** as a mixture of old and new blocks. The 2026-08-14 deploy ran
`DownloadOption.Software` (the whole PLC software), **raised and answered `StopModules` and
`StartModules`, and the CPU went `PLC_1 stopped` → `PLC_1 started`.** There is no executing program
for the rule to protect. §9b already records a **full download of 99 objects** on the same tooling,
which could not have happened if the limit bound full downloads.

**The new corroboration, and it is mechanical rather than interpretive:** run **C** put the deploy's
own 45 objects through the planner and it refused *for a reason that has nothing to do with counting*
— the set contains **`Main` (OB1) and `OB100`**, and DB-1 FIXES an OB's change class to **STOP**. A
change set containing an OB is not a wave boundary at all; it is drain-class by construction. **The
2026-08-14 deploy could never have been a wave-boundary download, whatever its object count.**

**And the count is not innocent either — run C3 settles that separately.** Set the class question
aside and ask only about connectivity: with `Main` included (it CALLs 12 blocks and references 7
instance DBs), the program collapses into **ONE weakly-connected dependency group of 34 objects**.
DB-4's limit is a limit on *groups*, so that is `DependencyGroupExceedsObjectLimit` — **not "split it
into two downloads", but "there is no consistent RUN-mode download of this set at all."** Remove the
two OBs and the graph falls apart into groups that fit: 41 objects, 3 batches (run C2).

> ***So: a full initial deploy is outside DB-4's scope, and this one is outside it twice over. What is
> NOT outside anything is that no code asked. `wave-cli batch` is the ask.***

### What this does NOT establish

- **It says nothing about a real delta download**, because none has been planned or performed through
  this verb. The runs above are a *reconstruction* of a recorded deploy, not a new measurement of one.
- **Openness's download granularity is device-level** (`download-plan`: the smallest real unit is the
  whole PLC software). A ≤20-object batch is therefore realised by controlling **what is staged into
  the project** before a `SoftwareOnlyChanges` download, not by selecting objects at download time.
  ***That wiring does not exist*** — `wave-cli batch` produces the plan and nothing consumes it.
- **The change classes in runs C/C2/C3 are the producer's, applied from DB-1's fixed table plus a
  command-line default.** DB-1's classifier is not built, so "every object is new, therefore RUN" is
  a stated assumption about an initial deploy, not a computed classification.
- **The dependency edges are as good as `cross-check` plus two IR rules.** They are computed, not
  typed, and they have not been cross-checked against §9a's load manifest — which is the check the
  spec names for exactly this and which nothing performs.

## 5. THE TESTS CAN FAIL — five mutations, each run and confirmed red

| mutation | red |
|---|---|
| **M1** `DefaultMaxObjectsPerBatch` 20 → 45 | **6** tests, 5 of them mine |
| **M2** closure verifier accepts a dependency anywhere in the change set (blind to a split group) | `BatchPlannerTests.The_real_closure_checker_satisfies_the_contract` |
| **M3** packer stops joining components (every object its own group) | **3**, incl. two of mine |
| **M4** the CLI's raise-the-limit gate disconnected (`> 20` → `> 1000`) | `A_limit_above_db4s_twenty_is_refused_before_anything_is_examined` |
| **M5** `OverBudgetBatches` disconnected | `The_over_budget_backstop_names_an_oversized_batch_and_stays_silent_on_a_legitimate_one` |

⚠️ **M3 exposed a test of mine that passed for the wrong reason** and it was fixed rather than noted:
`An_fb_its_instance_db_and_its_udt_land_in_one_batch` survived a component-blind packer, because at
limit 20 first-fit puts three singletons in one bin anyway. `A_group_that_cannot_fit_is_refused_as_a
_group_not_split_and_caught_afterwards` squeezes the limit to 2, where the two behaviours diverge —
and it asserts the **reason**, because both produce exit 1.

Restores were by **byte copy from a pre-mutation backup, never `git checkout`** — a `git checkout`
restore has already silently reverted uncommitted work in this repo — and every restore was confirmed
by `md5sum` against the backup.
