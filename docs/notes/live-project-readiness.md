# LIVE-PROJECT READINESS — read this first

**Written 2026-08-14, overnight, at the end of the hammer campaign.** The owner's ask was: *have the
tooling ready to use in the morning on a live project.* This is the answer, and it is written to be
**read once, quickly, before you start** — not to be complete.

> 🔴 **THE ONE-LINE ANSWER.** The **deployment, read-back and analysis** path is ready and has been
> run against a real controller. The **closed-loop test path is NOT** — no conformance wave has ever
> executed end to end. Use the first. Do not build a plan on the second today.

> ✅ **THE RELEASE BINARY IS NOW CURRENT — rebuilt and verified, nothing owed.** It was stale and it is
> not any more. See *THE RELEASE BINARY WAS STALE* below for how that was proved.

> ✅ **NOTHING IN THIS DOCUMENT IS WAITING ON YOU.** The coordinator binding has been **independently
> reviewed and committed** — see *THE COORDINATOR BINDING* below. **Two open questions are recorded
> there for when you next touch that block; neither blocks anything today.**

---

## ✅ THE RELEASE BINARY WAS STALE — REBUILT AND VERIFIED, 2026-08-14

**Resolved.** `dotnet build -c Release src/openness-cli/openness-cli.sln` — **build succeeded, 0
warnings, 0 errors**, hash `6554775…` → `49351c4b…`.

***AND VERIFIED BEHAVIOURALLY, NOT BY TIMESTAMP*** — this machine has read a stale apphost before:

| check | result |
|---|---|
| **It attaches** — `openness-cli list GenProject1` | **exit 0, 36 blocks listed.** ***Self-approval worked with nobody at the machine***, which is the claim FI-74 makes and this is another live confirmation of it |
| `portal-status` false alarm | ***GONE.*** Zero occurrences of *"cannot be true"*; the real OS-only finding survives |
| `compile --json` | ***STDOUT NOW PARSES AS JSON*** (`state, errors, warnings, consistentAfterCompile, messages`) with the prose paragraph on **stderr**. It had never emitted valid JSON on this project, because the permanent hardware warning sent **every** run down the prose branch |
| bogus `--device` | Now names ***`DeviceNotFoundException: No PLC device matching 'NoSuchDevice' found`*** instead of reporting 43 present blocks as absent |

⚠️ **One residual, cosmetic, not a false claim:** a bogus `--device` still emits **one row per block**
(43 of them) for a single cause. **The reason each row gives is now correct** — it was the *claim* that
was wrong before, and that is fixed. **Noisy, not lying.**

## 🔴 THE DEFECT THAT MATTERS MOST FROM THAT SWEEP

***`sanity-check` — THE HARD-RULE-4 GATE — RETURNED `OVERALL: HEALTHY`, EXIT 0, ON A PROJECT IT NEVER
EXAMINED.*** `IsHealthy`'s last term is `DeviceCompiles.All(…)`, and ***`All` over an empty sequence is
`true`*** — so no PLC device meant a clean pass with nothing compiled. Reachable via an HMI-only
project, or a device walk emptied by a concurrent Openness session. **Fixed, with a converse test so a
device with zero blocks is still legitimately healthy.**

**And its sibling:** `compile-all --json` reported `"clean": true` on a run that examined nothing —
`0 && 0` is vacuously true. **The TEXT formatter had guarded this all along** and printed *"NOTHING
EXAMINED … proves nothing about the project"*; **the JSON formatter had no guard, three lines from a
comment stating the rule.** *Two formatters, one verdict, one of them honest.*

### All four fixes re-verified in the SHIPPED Release binary, 07:05

Each was only ever seen in Debug before, and *Release is what everything actually invokes* — which
is the entire reason the staleness mattered. MD5 `49351C4B7B3E0132F2DDA70DB3880294`:

| fix | confirmed in Release |
|---|---|
| `compile-all --json` | `clean=false  nothingExamined=true  compiled=0` at **exit 14**. The same run emitted `clean: true` before |
| `compile --json` | stdout **parses** (`state=Warning`, `errors=0`); `PASSED WITH WARNINGS` is on **stderr** and absent from stdout. Checked with **separate file redirects, never `2>&1`** — merging the streams would have hidden exactly what the fix changed |
| bogus `--device` | `No PLC device matching 'NoSuchDevice' found`, and it no longer claims the block is absent. **Control: the same export without the flag still exits 0**, so it does not over-fire |
| `sanity-check` | `healthy=true  nothingExamined=false  blocks=36  types=7`, exit 0, text still `OVERALL: HEALTHY` |

⚠️ ***THAT LAST ROW CONFIRMS ONLY THE ANTI-OVER-FIRE HALF.*** The **defect** case — zero PLC devices —
**cannot be constructed on this project** and is proven only by unit test. Observing it live needs an
HMI-only or device-less project. **Read the row as "the fix does not break the normal path", never as
"the guard was seen to fire."**

⚠️ **And the lesson worth keeping now the symptom is gone:** for most of the night those two
diagnostics were **lying in the shipped binary while their fixes sat committed, tested and green.**
***A COMMIT IS NOT A DEPLOYMENT***, and here the gap between them was one build command. The cheap
re-check after any future rebuild is `compile <project> --block <any> --json` through a JSON parser —
it parsed as **nothing at all** before the fix, so it is an unambiguous currency probe.

---

## USE THESE — proven against the controller, not just against their own tests

| | what it is proven to do |
|---|---|
| **Deploy** (`Harness.Device`: stage → `to-xml` → `import-all` → layout re-assert → `compile-all` → `sanity-check`) | **45 objects loaded by name**, CPU read back `Running (8)` **from the device** |
| **`rig-read`** | Run state, device identity, DB read. **Needs a device allowlist or it opens no socket at all** |
| **The mirror + copy layer** | Deployed and **read back over the wire**. `Bool`→bit/1 reg, `Int`→word/1, `Time`→double word/**2** |
| **`download-probe`** | The only binary that can transfer a program. **Fenced; exit 3 before Portal is contacted** on a refusal |
| **The converter's analysis set** | `preflight`, `drift-check`, `compare`, `review`, `tagstatus`, `cross-check` — the daily working tools |
| **The claims registry** | **Verified connected by observing a refusal**, not by assuming one. See the trap below |

## DO NOT RELY ON THESE TODAY

| | why |
|---|---|
| **The conformance loop end to end** | ***A WAVE HAS NEVER RUN.*** The 27 vectors were authored, admitted as far as the gates, and never executed. There is no result package in existence. **This was a DECISION, not a drift** — see below |
| **The phase-armed latch** | **Not built.** The generator now **refuses by name** rather than emitting the unconditional form — *which would silently delete any finding that turns on a signal FALLING* |
| **Wave duration as a planning figure** | Measured on one rig over one tunnel. **Reference only.** Nothing is scheduled against it |
| **The permit half of the write fences** | Tonight's fence work attacked **refusals**. The permit path is `NOT CHECKED` |

---

## 🔴 THE SCRATCH PROJECT'S IR DOES NOT DESCRIBE WHAT IS IN THE CONTROLLER

**Measured 2026-08-14 05:10** — `export-all --tagtables` (45 objects, 0 refused, 0 failed) then
`drift-check --complete`, which is the only comparison that puts the **IR on disk** against **what is
actually in the controller**. Result: **38 match, `exit 1`.**

| | |
|---|---|
| **DRIFTED — 4** | `DB_PLC` · `FB_Comms_ModbusServer` · `iDB_Comms_ModbusServer` · `iDB_HopperBlockageStim` |
| **IN THE CONTROLLER, NO `.ir` AT ALL — 2** | `MotorIOSet` · `MotorVSDIOSet` |

***CONSEQUENCE: RE-IMPORTING FROM IR WOULD CHANGE THOSE FOUR BLOCKS IN THE CONTROLLER.*** The four are
the comms and stimulus blocks touched during deployment, so this is most likely authored-vs-imported
divergence rather than corruption — **but it has not been reconciled, and until it is, the `.ir` is
not a description of what is running.** ***Reconcile before importing anything into `GenProject1`.***

⚠️ **And a `drift-check` defect found by the same run:** a tag table whose TIA name contains spaces
fails to pair, and is then reported **both** as `EXPORT-ONLY` *and* as `SKIPPED` — **one object, two
contradictory absence rows**, one of which reads as the serious finding *"in the controller and no
`.ir` describes it"*. Being fixed. **Treat a tag-table absence row with suspicion until it is.**

## THE READ-ONLY `openness-cli` SURFACE — hammered 2026-08-14, four defects, all fixed

The subcommands a live job uses every day (`list` · `export` · `export-all` · `sanity-check` ·
`portal-status` · `block-layout` · `library` · `download-plan` · `hmi` · `compile` / `compile-all`)
had never been attacked. **Every defect found was a REPORTING defect** — nothing computed a wrong
answer; all four *handed* one to a caller.

| found | what it did |
|---|---|
| 🔴 **`compile-all --json` said `"clean": true` about a run that examined nothing** | `IsClean` was `WithErrors == 0 && StillInconsistent == 0` — vacuously true at `0 && 0`. The **text** output of the same run said `NOTHING EXAMINED … proves nothing about the project`. Exit 14 was the only thing separating them |
| 🔴 **`compile --json` did not emit JSON** | A prose paragraph followed the object **on stdout**, so the output parsed as nothing at all. Not an edge case: this project carries a permanent hardware warning, so **every** per-block compile took that branch and **every** `--json` run was unparseable |
| 🔴 **A `--device` that matched nothing was reported as a missing BLOCK** | `export-all --device NoSuchDevice` named **43 present blocks and types as absent from the project**, and its JSON gave each a `path` naming the device it claimed not to have found them under. *"This block is not in the controller"* is the most consequential wrong conclusion this tool can produce — it is what `drift-check --complete` exists to treat as a finding |
| 🔴 **`sanity-check` passed a project it had never examined** | `IsHealthy`'s last term is `DeviceCompiles.All(…)`, and `All` over an empty sequence is **true** — so a run finding **no PLC device** returned `OVERALL: HEALTHY`, exit 0, having compiled nothing. **Hard rule 4 names this command as THE gate** |

**Fixed, each with the mutation that turns its test red, each run and confirmed red.** 741 tests
pass. ⚠️ **All four fixes are in the STALE Release binary's blind spot** — see above.

### What HELD, and what that is worth

- **`compile-all`'s empty-work-set guard fired on the real thing** (exit 14, `NOTHING EXAMINED`) —
  not merely in unit tests. **`compile --block` correctly exited 0 on a warnings-only state**, so
  that fix is live and working against the project's permanent hardware warning.
- **`download-plan --device NoSuchDevice` named the device correctly** in the same session that
  `export` blamed the block. *One flag, one binary, two behaviours* — the disagreement between two
  commands is what located the defect, and it was worth more than either one's pass.
- **Four commands agree on the inventory**: `list` 36 blocks; `sanity-check` 36 blocks + 7 types;
  `export-all` 43 (+2 tag tables = 45); `download-plan` `WOULD CARRY 36 + 7`. No contradiction.
- ✅ **`compile-all --force`: 43 compiled, 0 with errors, 0 still inconsistent, 1 pass.** The scratch
  project is genuinely healthy **on a full denominator**, not only on an empty work set. This is a
  stronger statement than `sanity-check` alone can make and it is new as of tonight.

### ⚠️ WHAT THIS LANE DID NOT ESTABLISH

- **Only refusals and reads were exercised.** No `import`, `delete`, `--set`, or write of any kind.
  Read everything above as *"these read paths behave"*, never as *"the surface is correct"*.
- **`hmi --screen <no match>` is UNMEASURED.** `GenProject1` has **no HMI device at all**, so the
  screen filter is unreachable here and `hmi`/`hmi --screen NoSuchScreen` are **indistinguishable —
  both exit 0** with `(no HMI devices found)`. Whether a screen filter matching nothing is a silent
  zero-denominator **cannot be answered on this project**, and the other projects on this machine
  are live engineering jobs. *This is the gap I would attack next, and it needs an HMI scratch project.*
- **`library` and `hmi` return exit 0 on genuinely empty inventories.** Left alone deliberately —
  they are censuses that *say* they are empty, not verdicts claiming a pass. Flagging that as a
  defect would be a gate firing outside its scope.
- **`compile`'s "these counts are not reliable" NOTE is itself wrong, and was left alone.** It fires
  on every compile of this project and blames TIA: the tool's own message-tree count (`WARNINGS: 4`)
  is inflated by counting **rollup nodes**, while TIA's `WarningCount=1` matches the single leaf
  warning *and* the tree's own summary line. **It is not a false green** — the verdict takes
  `Math.Max` of both counts, which is fail-closed — so it is a cosmetic defect in a permanently-firing
  note, and *a note that is always on is a note nobody reads.* Recorded, not fixed.
- **The `block-layout --set` help text still says the re-import revert is `UNVERIFIED`.** CLAUDE.md
  records it as **MEASURED on 2026-08-11**, and the help steers the reader to `--expect`, which only
  *detects* the revert — `--set` is what repairs it. **The binary's own help gives the insufficient
  remedy.** Not fixed here; it is a text change in a command this lane was fenced from writing with.

## 🔴 THE FOUR TRAPS THAT HAVE EACH COST A DAY

**1. `--claims <dir>` must be the SHARED ROOT — `C:\ProgramData\Ladder-AI\claims` — and NOT the
project folder.** The tool appends the project name itself. Passing `…\claims\<project>` yields a
**second, empty store that grants every claim**, and it is invisible because *two agents making the
same mistake agree with each other perfectly.* ***READ THE `store=` LINE THE TOOL ECHOES. DO NOT TRUST
THE ARGUMENT.***

**2. Portal is a token, not a component.** One lane at a time. Two Openness sessions on one project
has already produced `Collection was modified` with every block reporting inconsistent.

**3. A bare whole-device `compile` is not the gate.** Use `sanity-check` — and read **both** the
`BLOCKS:` and `TYPES:` lines.

**4. Re-assert `--set Standard --yes` after EVERY import of a block that must be readable over classic
S7.** A re-import silently reverts it to `Optimized`, `drift-check` is structurally blind to it, and
the block goes invisible on the wire while every check stays green. **It bites on the second import.**

---

## WHAT THE CAMPAIGN FOUND, THAT YOU SHOULD EXPECT TO MATTER

- 🔴 ***COMMITTED WAVE SETS WERE BEING LOST SILENTLY, AND THE SURVIVING SUBMISSION REPORTED SUCCESS.***
  Found by killing agents mid-write. `File.Replace` is not atomic against process death; a reader
  landing in the window read the store as **empty** — *correct exactly once, before anyone has ever
  submitted, and catastrophic every time after.* **Fixed and demonstrated in the live race** (90 kills,
  guard fired 30 times, zero loss). ***If you ever see a wave store report zero slots after a
  submission has been made, STOP — do not resubmit*** — the newest `.tmp-*` beside it is a complete
  store and restoring it has been shown to recover cleanly.
- ⚠️ **A killed agent LEAKS ITS SLOT permanently.** The colouring stays consistent and fails in the
  safe direction, but there is **no withdraw verb**, so clearing one dead agent's slot currently costs
  **every other agent's submission**. **Known, raised, deliberately not fixed overnight** — it is a
  change to the multi-agent ownership contract, not a defect.
- ***THE LEASE CRASHED AT 48 CONCURRENT AGENTS AND WAS CLEAN AT 8, 16 AND 32.*** Fixed. Now measured
  flat to 128 (69–122 ms per agent across a 16× range, no knee). **The practical ceiling is the
  caller's timeout — roughly 330 agents at the 30 s default — and the failure there is a NAMED
  retryable refusal, not a breakdown.**
- **There is no stale-lease state to detect.** The lock is the OS handle; a killed holder releases
  immediately. **A hung holder and a busy one are indistinguishable, permanently** — if a wait is long,
  that is a fact for a human, and the tool is no longer allowed to claim otherwise.
- **A slot set that all drives one FB instance is ONE slot, not N.** If you submit N slots against one
  block and see `SERIALISED`, ***that is a correct result, not a failure.***
- **The gate count is 25**, not the 23 quoted in the test plan.
- 🔴 ***`tagstatus` WAS ACCUSING LEGITIMATE ALARM-BIT SLICES OF BEING INVENTED MEMBERS*** — `DB_X.Alarm0.%X0`
  returned `MEMBER-NOT-FOUND`, the **hard-rule-3 verdict**, on the one construct doc 06 documents an
  exception for. **154 false findings across the 92 committed `.ir` files.** *Fixed*, and slices are now
  bounds-checked (`.%X16` of a `Word` is `IndexOutOfRange` — a **different fact** from an invented
  member), while widths the tool cannot know are **accepted unchecked, never accused.**
  **If you saw that verdict before today and dismissed it, you were right to.**
- ✅ **`preflight` now resolves tag references to MEMBER level.** Until today it stopped at the DB root,
  so **a block reading three invented members passed `CLEAN, exit 0`** while `tagstatus` refused them.
  ***Nothing in the static pipeline caught an invented member; now `preflight` does.***
- ✅ **`diff --only` no longer claims invariance it did not check.** Retyping an interface member with
  no network touched used to print `INVARIANCE OK … exit 0`. It now **fails closed**, with
  `--allow-header` as the named escape — **which `gen-block-modify-fix` must never pass**, since a fix
  needing an interface member *routes*, it does not *declare*.
- ✅ **Three more checks stopped passing on an empty denominator, and each will bite an old habit.**
  `drift-check` exits 1 with **`NOTHING COMPARED — this is not a pass`** where three routes used to
  give a green over **zero comparisons**: both dirs empty (*even with `--complete`*), every `.ir`
  unparseable, or ***either path one level ABOVE the files*** — both walks are top-level only, and that
  is the likeliest real mistake of the three. `COMPARED: <n>` now prints on **every** run.
  `reuse-scan` exits **2** when every queried `--tag` is absent from the corpus — it used to print
  `0 block(s) matched` with **no denominator at all**, and *exit 0 there licenses "nothing to reuse,
  write a new block"*. `candidate-scan` exits **2** for an `--fb` in no block — it used to scan all 43
  files and print `CANDIDATE SET SIZE: 0`, **byte-identical to a real FB with an unambiguous binding**.
- ⚠️ **Two tools now state what they do NOT cover, on every run — read those lines.**
  ***A `drift-check` MATCH is silent about `MemoryLayout`, and `compare` REFUSES (exit 2) the very pair
  it calls a MATCH*** (converter output declares no layout; a TIA export declares `Optimized` — measured
  on `FB_PusherControl`). **Do not "fix" that in the `Normalizer`** — it breaks every export-vs-output
  comparison wholesale until the converter can *emit* the attribute. And **`cross-check` multi-writer
  lines count writes from blocks that may never execute**, now annotated `[NOT REACHABLE from any OB: …]`
  — ***reported, never subtracted***, because wiring the block up restores the contention. Zero of these
  fire on `ir/test-project001`.
- 🔴 ***THE `%Xn` DEFECT WAS FOUND BY A WHOLE-CORPUS SWEEP AND BY NOTHING ELSE.*** Every targeted probe
  passed, because **the input you would have to guess was already sitting in the repository.** So when
  you validate a check, **run it over all 92 committed `.ir` files and require silence** — one command,
  a validation Portal cannot add to, and the only reason this was caught.

## 📋 OPEN — what the converter lane would attack next, in order

**None of this blocks a live project today.** Recorded so it is not re-derived from scratch.

1. **Tag-name comparison is case-sensitive (`Ordinal`); TIA's symbol resolution is not.** Full write-up
   with the reproduction: `16-future-ideas.md` → *"OPEN QUESTION — the converter's tag resolution is
   CASE-SENSITIVE"*. **Contained today** — `preflight` refuses a case-varied reference outright, so the
   pipeline fails closed — **but `cross-check`'s C-308 analysis is structurally blind to a case-varied
   second writer** that entered the project another way (hand-authored in TIA, or an import whose casing
   differs). ***One Portal measurement settles it***: import two blocks writing the same DB member in
   different casing, and compile. 🔴 **Do NOT close it with a `ToLower()`** — *a comparator that learns
   to equate representations is how a comparator starts passing things*, and that would silently merge
   two paths in the usage graph on **every** project, for a defect nobody has yet seen on real data.
2. **`preflight` cannot check members under a LOCAL root whose type the export does not carry** — an
   IEC-timer static, or a UDT absent from the export. It reports **nothing** rather than guessing
   (`NotEnumerable` deliberately does not gate, so the fix cannot manufacture the opposite false
   accusation). Honest, and still a hole.
3. **`review` and `digest` have never had a whole-corpus validation of their own.** The sweep that found
   the `%Xn` defect covered only `preflight`/`tagstatus`. **The same 92-file run against those two is one
   command and has not been done** — and item 1 of this list exists because the first sweep found
   something.
4. **`compare` is the only check in this family with an authority outside the converter** — it has been
   through TIA. `drift-check`, `diff` and `ir-hash` are converter-vs-converter and are **structurally
   blind to anything the round trip preserves.** A property to remember when quoting them, not a defect
   to fix.

## ✅ THE COORDINATOR BINDING — transcribed, independently reviewed, COMMITTED

`gen/test-project001/hopper-blockage-alarm/harness-binding.json` is the coordinator's binding — which
existed only as prose (`harness-binding.md`) — in the data form `--binding` requires. **18 signal
entries + 4 latch provenances bound.**

***REVIEWED BY SOMEONE OTHER THAN THE TRANSCRIBER, WHICH IS WHAT D6 ACTUALLY REQUIRES.*** The concern
was never that a *particular person* had to look at it — it is that ***a transcription must not become
the authority without an independent reading***, since after it is committed it *is* the source. The
review is recorded in the file's own `_review` block, and it was done **against the cited prose lines
directly, not against the transcriber's notes about them.**

**Verdict: FAITHFUL** on the half gate 5 reads — all 8 `resultSources` bind verbatim to `md:73-80`,
**including the D1 row**, where `HopperBlockedInhibit` binds to `HopperBlockStopReq`, *the tag the
block actually has.* That is correct and deliberate: **binding the spec name to the real tag is what
lets the behavioural assertions run at all, and D1 is reported by the independent check rather than
laundered into a rename.**

### The triage: 5 unbound items were never 5 open questions

| item | verdict |
|---|---|
| `ModelThreshold` | ***NOT A GAP — an INTENDED absence.*** The prose deliberately gives it no set-B name, with its reason: read from the block under test, it stops being a *stimulus decision* and becomes a *reading of the thing being tested* |
| `HBA_Scenario_Done.latch` | ***NOT A GAP IN THE TRANSCRIPTION*** — the schema demands **provenance** (`latchedBy` takes a block *name*, checkable against the deployed object set) where the prose states only the **mode**. The fix belongs in the coordinator's prose |
| `slotPartition` + `startBoolPerSlot` | ***ONE QUESTION, NOT TWO.*** And the prose supports exactly one reading without invention — all six drive the same instance and the same members, strictly serial. `SLOT-HBA-ALL` is correctly flagged as the single invented token |
| `ResetAtMs` | ***A SCHEMA QUESTION, NOT AN AUTHORITY ONE.*** The coordinator's intent is fully stated; what is missing is how one spec name binds to two tags in the loadable form. It is an *input*, and gate 5 reads results only |

➜ **Residual: one coordinator question** (state the slot partition explicitly, even if the answer is
*"one slot"*) **and one tooling question** (does the schema admit a multi-tag entry?). **Neither
blocks gate 5 or anything today.**

⚠️ ***IF A COPY LAYER IS EVER GENERATED FROM THIS FILE, THE SLOT PARTITION MUST BE STATED FIRST.***
Gate 5 flattens slots so its verdict is unaffected — **but generation is not gate 5.**

**What it already bought:** run against gate 5, it took `SignalNotInMap` from **33 to 0** — the name
join is complete — and returned a real verdict, ***`REFUSED — 13 × MapDoesNotProvideIt`***. Those 13
are the same signals wanting `Latched` where the map provides `Sampled`: **the gate independently
identified the phase-armed latch gap from the opposite end, without reading the analysis that found
it.** It was not tuned to produce that.

## 🔴 WHY NO WAVE WAS RUN OVERNIGHT — a decision, with its reasoning

**The rig was serving and reachable all night, and a download to it is permitted** (allowlisted bench
rig, outputs physically incapable of actuating, ADR-0009). ***I chose not to.*** The reasoning, so it
can be disagreed with:

1. **Widening the mirror is an IR edit + import + download**, and the mirror is exactly full at 35/35.
   That is a *deploy*, not a test run.
2. ***AND IT WOULD NOT HAVE PRODUCED THE RESULT ANYWAY.*** The phase-armed latch does not exist, so
   13 of the vectors' signals still could not be instrumented. **The wave would have returned a
   partial result whose gaps are precisely the ones already known** — near-zero new information.
3. **Against that: leaving a known-good, verified-serving rig in place for the morning.**

***THE MARGINAL INFORMATION WAS SMALL AND THE RISK WAS REAL, SO THE RIG WAS LEFT AS IT IS.*** The
first wave should be run **with a person present**, and it is the single highest-value next step.

## THE WRITE FENCES — attacked overnight, and they HELD

**Nothing let anything through.** Every malformed device allowlist (11 forms), every non-allowlisted
`download-probe` target (**exit 3 in 53–504 ms, zero log artifacts, Portal process count unchanged**),
every hostile path form against `confirm-roundtrip -Arm` (**exit 4, the stub `openness-cli` never
launched, the scratch dir never created**) — **including a junction pointing at the real allowlisted
project, refused BY NAME as a junction.**

**"No socket" was observed rather than quoted:** a refusal returns in **120–135 ms**; the permitted
control costs the full **10 155 ms** connect timeout. *That gap is the evidence.*

⚠️ ***BUT THE PERMIT HALF IS `NOT CHECKED`.*** The lane deliberately never tried a form that would
have been permitted, because that would have started a real download. **Read the above as "these
forms are refused", never as "the fence is correct".**

🔴 ***THE HARNESS'S STRONGEST SAFETY CLAIM WAS CERTIFIED BY A CONSTANT.*** `rig-write`'s arming — *"the
capability is absent from the assembly"* — rested on `Assert.False(Arming.CompiledIn)` where
`CompiledIn` is a **`const bool`**. A class constructing a live socket client was **planted in that
assembly and all 202 tests stayed green.** ***`rig-write` still cannot write — what changed is that we
now KNOW it, instead of having been told it.*** Replaced with a structural walk carrying a denominator
and a live control. **A constant stays true exactly as long as someone remembers to change it, which
is the one thing a fence must never depend on.**

🔴 **Three more gaps — none a hole in a fence, all holes in the GUARD ON THE GUARD**, now fixed:
the IL walk certifying the tooling cannot download **passes while examining zero method bodies**; the
`--yes` gate on `block-layout --set` can be **silently disconnected with 712/712 tests still green**;
and a `null` entry in a device allowlist **crashes unnamed** instead of refusing *(it does still fail
closed — the stack ends at the guard and no socket opens)*.

⚠️ **A `repo:`-prefixed allowlist entry means DIFFERENT THINGS to the C# fence and the PowerShell
fence.** Both fail closed today. **Prefer absolute paths in allowlists until that is reconciled** —
the divergence table is written into both allowlist files, and current behaviour is pinned by tests,
so a future correction is a decision rather than a drift.

➜ **The lesson the `Arming` constant and the IL walk teach together, because it is the same defect in
different clothes: *a check whose exercise requires editing the check will not be exercised.*** The
walk's searched member name was a **literal**, so the only way to run its negative control was to
hand-edit it — ***which is exactly why that control had decayed into a comment describing a manual run
done once, months earlier.*** Making the name a **parameter** is what makes the control runnable, and
therefore what makes it survive. **Ask it of any new guard: can it be exercised without being
modified?**

### ⚠️ WHAT THE FENCE LANE DID NOT ESTABLISH — read this before quoting anything above

- ***THE PERMIT HALF IS NOT CHECKED.*** Restated here because it is the limit most likely to be lost
  when the rest of the section reads so green. **No form that would have been PERMITTED was ever
  tried, on any fence.** Doing so needs a throwaway allowlist entry and a supervised session; it was
  **refused deliberately overnight, not overlooked.** Everything above means *"these forms are
  refused"*, never *"the fence is correct"*.
- 🔴 ***`openness-cli`'s `Program.Main` NOW DELEGATES TO AN INTERNAL `Run(args, gatewayFactory)`.***
  A behaviour-preserving extraction — same order, same returns — and it is what makes *"Portal was
  never contacted"* **observable** instead of inferred. **But it changes the entry point of the binary
  that talks to Portal, and it was written overnight. It wants a look in daylight before the live
  job.** Nothing else in the fix set touches a code path that runs in normal use.
- `{"entries": null}` and a top-level `null` in a device allowlist refuse as **`AllowlistEmpty`**
  rather than `AllowlistUnreadable`. Both refuse and both are named, and the classification was
  **deliberately left alone** — reclassifying a refusal *reason* is a decision, not a tidy.
- `rig-read`'s new `FenceFault` (exit 5) is ***correct, wired, and unfalsifiable in place***: after
  the null-entry fix, **no input in the 21-document fuzz sweep reaches it.** It is kept for the
  members of the family the sweep could not enumerate. **Do not read its existence as evidence the
  class is covered** — that limit is stated in the code as well as here.
- The fuzz sweep is **21 documents, not a proof.** It is evidence about the range it covered.

## HOW TO READ A GREEN FROM THIS TOOLING

This is the habit the whole project is built around, and it is worth thirty seconds:

1. ***EMPTY IS NOT CLEAN.*** A check that examined nothing must not exit 0, and most of this
   codebase's worst defects were exactly that. If a result looks clean, **ask what its denominator
   was.**
2. **`NOT CHECKED` is not a pass.** It is printed separately, before the gate list, deliberately.
3. ***A LOG LINE IS A CLAIM, NOT EVIDENCE.*** `docs/notes/test-log.tsv` records that a run happened
   and what it said. **When it matters, re-measure — do not cite the line.**
4. **Prefer the artifact to any report about it** — and not to your own model of what it contains
   either.

---

## THE STANDING MEASURED FACTS

Rig `10.10.10.10:503` unit 1 via the Talk2m tunnel; `:102` open, `:502` refused. Scan **23.33 ms**.
Round trip **min 63 / med 72 / max 106 ms**. **32-bit word order is HIGH-WORD-FIRST — measured off a
build stamp with distinguishable halves.** Mirror: **35 registers at `%M1000`, exactly full, zero
spare.** ***S7 variable access is refused CPU-wide on this rig, so every data read goes over Modbus.***
Harness objects reserve block numbers **9000–9999** per number space; **OBs excluded.**

⚠️ **X-D's compression ceiling of ~4.3× is HALF-MEASURED** — the scan is measured, `k ≈ 5` is X-D's own
number and has never been. Treat it as *derived from one measured and one assumed input.*

## WHERE TO GO NEXT

| for | read |
|---|---|
| What the tooling is vs what the spec says | `spec-reconciliation.md` |
| What must be tested, and what deliberately is not | `tooling-test-plan.md` |
| The build history and why the vectors did not run | `test-environment-build-plan.md` (**CLOSED** — *it quotes the implementation; a vector author must not read it*) |
| What has actually been run, and when | `test-log.tsv` |
