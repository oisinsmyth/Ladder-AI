# FI-65 component 1 — `converter claim` / `claims`: build and test record

**Date:** 2026-08-07 · **Branch:** `worktree-fi-48-openness-manager` · **Entry:** `docs/16-future-ideas.md` FI-65
**Commits:** `23faf49` (implementation) · `fa2f318` (renumber FI-48 → FI-65) · `c0aaeaf` (merge master)

What this records: what was built, what was tested, **what the standing testing requirements are**, and
— explicitly — what has *not* been tested. The last section is the load-bearing one: this is a
concurrency tool, and a concurrency tool that is only tested the easy way is a concurrency tool that
looks correct.

---

## 1. Scope

Built: FI-65b component 1 only — the claims registry. Six resource kinds, corpus validation, atomic
acquisition, `--check`, `--release`.

Not built, deliberately: components 2–5 (per-agent workspaces, canonical lease, integration, ledger).
Several need live Portal verification, which was out of scope for this pass by the owner's instruction.

Not built, and worth naming separately because it is the difference between a tool existing and a tool
mattering: **adoption**. Nothing yet *requires* an agent to claim before writing. The registry is
available, not binding.

> 🔴 **SUPERSEDED 2026-08-24 — THE REGISTRY IS NOW BINDING.** `7de3ac0` wired the claim at the seam
> where a block number is first chosen in all three `gen-block-*` skills, added a release step to each,
> made `claims` a required section of `evidence.json` (`.claude/agents/lad-coder.md`), and put the
> command syntax and the exit contract into CLAUDE.md (`bb091b7`). The verifier
> `tools/check-agent-evidence.py` **recomputes rather than reads**: it resolves the real store, confirms
> each declared claim exists and is held by the declared agent, and **joins every `block-number` claim
> to the `NUMBER` line of the `.ir` on disk** — the half an agent cannot satisfy by writing a plausible
> evidence file. Absent `claims` on a run that touched IR is exit 1, on the script's existing rule that
> an absent gate is not a passed gate.
>
> ⚠️ **Binding is not the same as working, and nothing here should be read as saying it is.** No
> generation run has yet gone through a claiming skill and been checked by that gate — see **§5.8**,
> and requirement **§4.8**, which is half closed rather than closed.

**Hard rules:** rule 8 is not engaged — this is PC-side tooling (`src/converter/`), where normal
software rules apply. No `.ir`, `patterns/`, `ir/` or `gen/` file is touched by the branch (verified:
`git diff --name-only master...HEAD | grep -E "\.ir$|^patterns/|^ir/|^gen/"` returns nothing).

---

## 2. What changed

### New — `src/converter/Converter/Claims/`

| File | Role |
|---|---|
| `ClaimsModel.cs` | `Claim`, `ClaimKind` (6), `ClaimSemantics` (allocation vs exclusive), `ClaimResult`, `ClaimOutcome`, `ClaimConflict`, `ClaimsReport` |
| `ClaimStore.cs` | The **only** code touching the claims directory: filename encoding, atomic acquire, parse, list, release |
| `ClaimValidator.cs` | `ClaimCorpus` (one build shared by every operation) + per-kind validation against the project |
| `ClaimsRunner.cs` | Acquire / allocate / check orchestration, and the cross-kind conflict pass |
| `ClaimsOutputFormatter.cs` | Text + JSON, the existing "one record, two renderers" pattern |

### New tests — `src/converter/Converter.Tests/`

`ClaimsTestCorpus.cs` (shared fixture), `ClaimConcurrencyTests.cs`, `ClaimStoreTests.cs`,
`ClaimValidatorTests.cs`, `ClaimsRunnerTests.cs`.

### Modified

- **`Preflight/ProjectIndex.cs`** — additive: records `(Kind, Number)` per block/DB and each block's
  network numbers, plus `IndexedAnything`. Extended rather than given a second scanner because
  `SignalInventory.cs:45` states the invariant that the corpus is read one way "so the three can never
  disagree about what a corpus contains". No existing accessor or caller changed.
- **`Review/Rules.cs`** — three C-501 slice helpers (`IsSliceAccessTag`, `SliceWordPath`,
  `SliceBitToken`) `private` → `internal`. Copying three lines would let a claim registry split a
  slice path differently from the rule that audits it.
- **`Program.cs`** — `claim` / `claims` dispatch, argument parsing, exit-code mapping, usage lines.
- **Docs** — `src/converter/README.md` (command section), `CLAUDE.md` (command block), `CHANGELOG.md`,
  FI-65 entry status.

### Two design decisions that changed during the build

Both are recorded because a reader of the plan will otherwise think the plan was implemented as written.

1. **`--suggest` was dropped for `--allocate`.** A non-binding suggestion is the exact race the tool
   exists to remove: two agents are both told "FB51 is free", both act on it, and one discovers the
   collision only after doing the work. `--allocate` takes the lowest free value atomically and prints
   what it took. There is no read-only allocation mode.
2. **Acquisition writes a temp file and `File.Move`s it into the slot**, rather than opening the slot
   with `FileMode.CreateNew`. The two look equivalent and are not: the winner holds its newly created
   file open while writing, so a loser hit a sharing violation and **could not read who had beaten
   it** — it threw instead of reporting the holder. Found by `ContestedValue_ExactlyOneAgentWins` on
   its first run, not by review.

---

## 3. Testing performed

### 3.1 Unit — 46 tests, `dotnet test`

| Class | Tests | What it proves |
|---|---:|---|
| `ClaimConcurrencyTests` | 3 | Contested value yields exactly one winner and every loser names *that* winner; parallel allocation yields distinct values and steps over corpus-used numbers; re-claiming your own value is idempotent and does not rewrite the original record |
| `ClaimStoreTests` | 10 | Filename encoding distinguishes values that sanitise identically; record round-trips; empty purpose survives; missing field rejected; a newline in `purpose` cannot forge a second field; release refuses another agent's claim without `--force` and says "FORCED" with it; project slugs separate, and ignore a trailing separator |
| `ClaimValidatorTests` | 20 | Grant **and** refusal for every one of the six kinds, plus per-kind malformed input, alarm-bit width bounds, per-kind number spaces (FB3 free while FC3 exists), and empty corpus → `NothingExamined` |
| `ClaimsRunnerTests` | 13 | Allocation skips corpus-used values for all three allocatable kinds; missing `--in`/`--type` is invalid; `--check` clean case; fulfilled ≠ conflict; exclusive claim on a vanished block gates; **cross-kind conflict** gates; same agent holding both claims does not; stale reported without gating; warnings for absent store and foreign project |

**Result:** `Passed! - Failed: 0, Passed: 823, Skipped: 0, Total: 823` (master's 777 + these 46).
Baseline before the work was 776; master added one while this branch was building.

### 3.2 Cross-process contention — the CLI-level proof

The xUnit concurrency test uses `Parallel.For` inside **one** process. Agents invoke the CLI as
**separate OS processes**, so the guarantee was re-proved that way: 10 real `Converter.dll claim`
invocations launched together, all claiming `FB123`.

```
launched=10 claimed=1 refused=9 exit0=1 exit1=9 exit_other=0
claim files on disk=1
distinct holders reported by losers: 'agent3'
```

Run 4 times. Every run: exactly one winner, nine refusals, one claim file, and all nine losers naming
the same single holder. **The winner differed between runs** (`agent3`, then `agent1` three times),
which is what makes it a genuine race rather than deterministic ordering.

The script is reproduced in §6 so this is re-runnable; it deliberately lives in the record rather than
in the repo, because it needs a built binary and a scratch directory, not a permanent home.

### 3.3 Smoke against the real corpus — `ir/test-project001`, read-only

Reproduces this project's own recorded near-miss. `gen/test-project001/telemetry.log` contains
`ShredderAlarm0.%X9 <- HopperBlockedAlarm, X9 verified free` — the check two agents can both pass at
the same moment.

| Command | Result |
|---|---|
| `claim --kind block-number --allocate --type FB` | `CLAIMED FB1` — corpus uses FB 3/7/8/50, so FB1 is genuinely free (cross-checked by grep over `NUMBER`) |
| `claim --kind block-number --allocate --type FB --floor 50` | `CLAIMED FB51` — stepped over FB50 |
| `claim --kind block-number --value FB50` | `REFUSED  FB50 is already FB_HopperBlockageMonitor`, exit 1 |
| `claim --kind alarm-bit --value DB_Alarms.ShredderAlarm0.%X9` | `REFUSED  already written by FC_AlarmsMain network 10`, exit 1 |
| `claim --kind alarm-bit --allocate --in DB_Alarms.ShredderAlarm0` | `CLAIMED …%X10` — first free bit |
| second agent claims `…%X10` | `REFUSED  held by agent 'A' since …`, exit 1 |
| `claims --check` with A holding `block-edit FC_ControlMain` and B holding `block-network FC_ControlMain:10` | **1 conflict**, exit 1 — the cross-kind case the filesystem cannot see |
| `claim` with no `--claims` and no `LADDER_CLAIMS_DIR` | hard error, exit 2 |
| `claim --project <empty dir>` | `REFUSED … refusing to report a resource free against an empty corpus`, exit 2 |

The `FC_AlarmsMain network 10` in the refusal matches the telemetry's own `FC_AlarmsMain NW10`.

### 3.4 Regression on the shared code that was touched

`ProjectIndex` and `Rules` are used by other commands, so both were exercised against the real corpus
after the change:

- `converter review ir/test-project001/FC_AlarmsMain.ir --project ir/test-project001` → 21 findings,
  exit 0. The C-501 warning is **pre-existing** (a known rule-vs-practice tension on that block), not
  introduced here; the slice helpers changed visibility only.
- `converter preflight ir/test-project001/FB_HopperBlockageMonitor.ir --project ir/test-project001` →
  `CLEAN, 0 findings`, exit 0.

### 3.5 Stability

`ClaimConcurrencyTests` run 5 consecutive times: 5/5 pass. Cross-process script run 4 times: 4/4.
A race test that passes intermittently is worse than none, so repetition is part of the evidence
rather than a one-off check.

---

## 4. Standing testing requirements

These are obligations on **future** changes, not a description of what was done.

1. **Any change to `ClaimStore` acquisition or release re-runs the concurrency suite at least 5
   times, and the §6 cross-process script at least 3 times.** This is the one area where a change can
   pass every functional test and still destroy the property the tool exists for. The `CreateNew` →
   `File.Move` defect is the precedent: functionally identical, observably broken only under contention.
2. **A new claim kind ships with, at minimum: one grant, one refusal, one malformed-value test**, and
   — if it is allocatable — one allocation test proving it steps over a corpus-used value. The
   existing per-kind tests in `ClaimValidatorTests` are the template.
3. **A new kind must also be considered against `CrossKindConflicts`.** The filesystem stops two
   agents taking the same *value*; it cannot stop them taking the same *thing* by different routes.
   Adding a kind that can alias an existing one without extending that pass reintroduces a silent
   collision — which is the whole failure class.
4. **Any change to `ProjectIndex` re-runs the full suite**, not just the claim tests: `preflight`,
   `review --project`, `tagstatus`, `target-scan` and `cross-check` all resolve through it.
5. **Exit-code changes are contract changes.** `0` acquired/clean, `1` refused or conflict, `2`
   unusable input. Calling agents branch on the 1-vs-2 split — 1 is a real answer ("someone has it,
   pick another"), 2 means nothing was decided. Changing the mapping requires updating
   `src/converter/README.md`, the `CLAUDE.md` command block, and any skill that consumes it.
6. **`--check` must never gate on a fulfilled allocation claim.** A check that fails at the moment the
   work succeeded gets ignored when it matters. `Check_TreatsAFulfilledAllocationAsSuccessNotConflict`
   guards this; it is not an incidental test.
7. **The FI-44 guards must stay tested.** Empty corpus → `NothingExamined`, and missing `--claims` →
   hard error. Both exist so the tool cannot pass by examining nothing; a regression here is invisible
   precisely because it looks like success.
8. **Before adoption** (wiring claims into the coding skills), add an end-to-end test that two
   concurrent agent runs against one project cannot both take the same resource. That is the real
   acceptance criterion for this feature and it does not exist yet.

   🔴 **HALF CLOSED 2026-08-24, AND THE OTHER HALF IS STILL OPEN. READ BOTH SENTENCES.**

   - ✅ **Closed — the primitive.** `src/converter/Converter.Tests/ClaimProcessRaceTests.cs`
     (`bb6f9e8`): twelve rounds of **eight real OS processes** contending one block number through the
     built CLI against a real corpus — argument parsing, corpus build, store resolution,
     `File.Move(overwrite: false)` and the exit-code contract included. Exactly one exit 0, seven exit
     1s, **every loser's stderr naming the actual winner**, a file-count denominator rather than
     "at least one", and an exit 2 anywhere counted as a failure because a racer that examined nothing
     has not raced. **Redden proof:** swap the atomic move for check-then-write and the suite reports
     *"exactly one process must win FB7100, but 2 did."*
   - 🔴 **NOT closed — the workflow, which is what this requirement asked for.** It says *two concurrent
     **agent runs***. That file runs two concurrent **processes of one verb**. Nothing in it dispatches
     an agent; **holding a claim across a stage, releasing it, retrying after a crash, and simply
     forgetting to ask are all unexercised.** An agent run does far more than one verb.

   ➜ **So the words that may be struck from this requirement are *"cannot both take the same resource"*
   for a single contended acquire. The words that may not be struck are *"two concurrent agent runs."***
   Marking the whole requirement satisfied on the strength of that file records a workflow test nobody
   wrote. *(That caveat lived only in the test file's own doc comment until this entry was written —
   which is the reason it is here: a caveat a reader of the requirement never reaches is a caveat that
   does not exist.)*

---

## 5. Not tested — known gaps

Stated plainly rather than left for someone to discover.

1. **Non-local filesystems.** Atomicity rests on `File.Move` failing when the destination exists —
   solid on local NTFS, **not guaranteed on a network share or a sync-backed folder**. This machine
   has OneDrive at `C:\Users\User\OneDrive` (the repo itself is on a plain local path, so the repo is
   unaffected), which makes it entirely plausible that someone points `--claims` at a synced folder.
   **Requirement if that is ever wanted: test it there first.** Until then the claims directory should
   be local.
2. **Multi-machine.** Everything here is single-PC. Two machines sharing a claims directory is
   untested and additionally runs into `10-non-goals.md`'s "multi-user / team deployment" not-now.
3. **Clock skew and TTL.** `created` is the writing process's UTC clock, and staleness is computed by
   subtraction. Two agents with divergent clocks would disagree about staleness. Harmless today —
   stale claims are only ever *reported* — but it becomes real if anything is ever made to act on it.
4. **Crash mid-acquire.** A process killed between writing its temp file and moving it leaves a
   `.tmp` file. Inert by design (enumeration only matches `*.claim`), and not tested by killing a
   real process.
5. **Very large allocation ranges.** Candidates are bounded at 512 numbers past the floor and 64
   network slots; a project exceeding those gets "no free … found" rather than a hang. The bound is
   not exercised by a test.
6. **Adoption.** ~~No skill calls this yet~~ — **three do, as of `7de3ac0` (2026-08-24): `gen-block-new`,
   `gen-block-modify-fix`, `gen-block-modify-purpose`.** 🔴 **The gap did not close; it moved.** What is
   now untested is the wired path itself: **no generation run has been executed through a claiming skill
   and verified by `tools/check-agent-evidence.py`'s claims gate, in either direction — not once.**
   Everything known about that path is known by reading it. See §5.8 and requirement §4.8.
7. **The six kinds are not a proof of completeness.** They cover every collision source this repo has
   evidence for (`NUMBER` lines, the `%X9` telemetry line, `FC_ControlMain` NW8/NW9 appends, shared
   DB members). A resource class nobody enumerated is still an undetected collision.
8. 🔴 **THE GATE IS INVERTED AGAINST A COMPLIANT RUN, AND IT WAS FOUND BY READING THE TWO ARTIFACTS
   AGAINST EACH OTHER RATHER THAN BY RUNNING THEM. Found 2026-08-24, verified at `a3dcd6f`.**

   The two halves, each read from the file it lives in:

   - **The skills release before the dispatcher verifies.** Each coding skill writes `evidence.json`,
     then — *"Then release every claim"* — runs `converter claims … --release --agent <id> --all`, then
     **stops**, explicitly handing the check stage to somebody else
     (`.claude/skills/gen-block-new/SKILL.md`, the release step immediately after the `evidence.json`
     step; the same shape in both `gen-block-modify-*` skills).
   - **The verifier requires the claim to still be held.** `tools/check-agent-evidence.py`'s claims gate
     fails with *"claim %s '%s' is NOT IN THE STORE — the evidence says it was taken and the registry
     has no record of it"* for any declared claim the store no longer holds, and, if the release
     emptied the bucket, with *"the claims store answered with ZERO claims"* instead.

   **So a run that follows the skills exactly is refused, and a run that skips the release passes.** The
   gate rewards the non-compliant path. **It fails closed rather than open**, which is the safe
   direction and is why this is a defect and not an incident — but *a gate that refuses every correct
   run is a gate that gets switched off*, which is this project's own recurring finding about
   `--no-verify`.

   ⚠️ **What this entry deliberately does not do is say how to fix it.** The ordering question —
   whether the release moves after verification, whether the verifier reads a release record instead of
   a live claim, or whether the dispatcher verifies before the sub-agent stops — is a contract change
   across a skill, an agent definition and a tool, and it is being worked elsewhere. **What is recorded
   here is the observation and its date, so that a later green cannot be read as evidence this never
   happened.**

---

## 6. How to re-run everything

```bash
# 1. Full suite (expect 823 passing)
cd src/converter && dotnet test

# 2. Concurrency only, repeated — required after any ClaimStore change
dotnet test --filter "FullyQualifiedName~ClaimConcurrency"   # x5

# 3. Smoke against the real corpus (read-only; use a throwaway claims dir)
DLL=src/converter/Converter/bin/Debug/net8.0/Converter.dll
dotnet $DLL claim  --project ir/test-project001 --claims /tmp/claims --agent A \
                   --kind alarm-bit --value DB_Alarms.ShredderAlarm0.%X9      # expect exit 1
dotnet $DLL claim  --project ir/test-project001 --claims /tmp/claims --agent A \
                   --kind alarm-bit --allocate --in DB_Alarms.ShredderAlarm0  # expect %X10
dotnet $DLL claims --project ir/test-project001 --claims /tmp/claims --check  # expect exit 0

# 4. Regression on the shared code touched
dotnet $DLL review    ir/test-project001/FC_AlarmsMain.ir          --project ir/test-project001
dotnet $DLL preflight ir/test-project001/FB_HopperBlockageMonitor.ir --project ir/test-project001
```

### Cross-process contention script (§3.2)

Needs a built binary. Point `WT` at the working tree, `CLAIMS`/`OUT` at scratch directories.

```bash
#!/usr/bin/env bash
set -u
WT="<path to the working tree>"
DLL="$WT/src/converter/Converter/bin/Debug/net8.0/Converter.dll"
CLAIMS="<scratch>/claims-crossproc"; OUT="<scratch>/crossproc-out"
rm -rf "$CLAIMS" "$OUT"; mkdir -p "$OUT"

N=10
for i in $(seq 1 $N); do
  ( dotnet "$DLL" claim --project "$WT/ir/test-project001" --claims "$CLAIMS" \
      --agent "agent$i" --kind block-number --value FB123 > "$OUT/$i.out" 2>&1
    echo $? > "$OUT/$i.code" ) &
done
wait

echo "claimed=$(grep -l CLAIMED "$OUT"/*.out | wc -l) refused=$(grep -l REFUSED "$OUT"/*.out | wc -l)"
echo "exit0=$(grep -lx 0 "$OUT"/*.code | wc -l) exit1=$(grep -lx 1 "$OUT"/*.code | wc -l)"
echo "claim files=$(ls "$CLAIMS"/test-project001/ | wc -l)"
echo "holders: $(grep -h 'is held by agent' "$OUT"/*.out | sed 's/.*held by agent //;s/ since.*//' | sort -u | tr '\n' ' ')"
```

**Pass criteria:** `claimed=1`, `refused=9`, `exit0=1`, `exit1=9`, exactly one claim file, exactly one
distinct holder named by the losers. Run at least 3 times; the winner should vary between runs.

---

## 7. Note on the FI number

This entry was written as FI-48 and renumbered to FI-65 (`fa2f318`): master took FI-48 (`d8ed95c`,
to-xml coil reordering) and FI-49 (`e5423f5`) while this branch was building. Two agents each scanned
the ideas doc for "the next free number", both picked 48, and nothing detected it — no check failed,
both entries were internally consistent, and the conflict existed only between them.

That is the collision class this tool prevents, occurring on the branch that implements it. It is
recorded here because it is the most direct evidence available that the problem is real rather than
anticipated — and because a `claim --kind` covering doc IDs would have refused the second agent in the
second it asked, which is itself a candidate seventh kind.
