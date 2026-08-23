# THE HAMMER CAMPAIGN — FULL RESULTS

**2026-08-13 → 2026-08-14, overnight, autonomous.** Owner's directive: *close the old plan
completely, then hammer the new tooling — test after test — until it is fit to use on a live project
in the morning.*

**This is the full record.** The one-page operational answer is
`docs/notes/live-project-readiness.md`; read that first if you are about to use the tooling.

---

> # THE RESULT IN ONE PARAGRAPH
>
> **Every write fence held. Nothing let anything through.** What the campaign actually found is that
> ***a large number of the checks certifying that were incapable of noticing if it stopped being
> true.*** **42 defects** across five components — and the single most common shape, by a wide
> margin, was **a check that examines nothing and reports success.** It appeared in the hard-rule-4
> gate, in the hard-rule-3 tool, in the IL walk certifying the tooling cannot download, in a JSON
> formatter three lines from a comment stating the rule, and in two lanes' own test harnesses while
> they were hunting it elsewhere.

---

## PART 1 — THE STEPS OF THE CAMPAIGN

### Stage 1 — Close the old plan (`test-environment-build-plan.md`, 4,830 lines)

Closed with a **four-bucket final state**, deliberately separating *built and proven against the
controller* from *built and unit-proven only* — **different claims, and this project's most expensive
failures have all been a green that examined nothing.** The fourth bucket is `NOT CHECKED`, kept apart
from a pass.

***The item that did not land is named in full rather than deferred into silence:*** the 27
conformance vectors were authored, admitted as far as the gates, and **never executed.** *A plan that
closes by quietly dropping its last item teaches the next plan to do the same.*

Alongside it: **`test-log.tsv`** — the log the owner asked the tooling to keep. Append-only, CRLF,
7 fields, **refusals recorded like any other run**, because *a log that only records successes cannot
show a regression*. **116 rows at close.**

### Stage 2 — Reconcile the spec against the tooling

**178 items.** 86 as specified · 33 diverged (**spec corrected in place, 379 insertions / 0
deletions**) · 38 not built · 21 not checked, each named individually.

### Stage 3 — Derive the test plan from the corrected spec

**107 rows, 30 of them multi-agent**, plus a **42-entry NOT BUILT consequence register**. Its own best
row: ***a stale AS SPECIFIED row fails when run; a stale NOT BUILT row is never run at all.*** That
row has since caught **nine** stale absence claims.

### Stage 4 — Hammer, in five lanes

| lane | scope | outcome |
|---|---|---|
| **A** | Harness gates, copy layer, submission contract | 10 defects · gate list driven to **one** honest `NOT CHECKED` |
| **B** | Wave control, claims registry, admission | 7 defects · concurrency measured **1 → 128** |
| **C** | The converter's 14 check subcommands | 11 defects · *"I did not run out of things that broke"* |
| **D** | The device-write fences | 8 defects · **every fence held; four of their guards did not** |
| **E** | The read-only Portal surface | 4 defects · including the worst one found |

**Rules of engagement, set before the first run** (`tooling-hammer-plan.md`), because a vacuous pass
was the likeliest outcome:

- ***Artificial materials only*** — `FB/DB 9010–9013`, generated, disposable. Real blocks are
  evidence; re-running them changes what their results mean.
- ***The claims store must be SHARED or the registry coordinates nothing*** — and the campaign's first
  assertion was about itself: **prove it by observing a refusal.**
- ***N slots against one FB instance is ONE slot*** — vary the blocks, or the concurrency is nominal.
- ***Portal is a token, not a component.***

---

## PART 2 — FULL RESULTS: 42 DEFECTS

Severity is *what it would have cost on a live job*, not how hard it was to fix.

### 🔴 CRITICAL — a safety or correctness claim that was not true

| # | defect | evidence |
|---|---|---|
| 1 | ***`sanity-check` — THE HARD-RULE-4 GATE — returned `OVERALL: HEALTHY`, exit 0, on a project it never examined.*** `IsHealthy`'s last term is `DeviceCompiles.All(…)`, and **`All` over an empty sequence is `true`** | Reachable via an HMI-only project, or **a device walk emptied by a concurrent Openness session** — an already-observed condition. Fixed; converse test keeps a zero-block device legitimately healthy |
| 2 | ***Committed wave sets were lost silently, and the surviving submission reported success.*** `File.Replace` is not atomic against process death; a reader in the window read the store as **empty** | *Correct exactly once, before anyone has ever submitted, and catastrophic every time after.* 27 agents killed mid-write → 8 sets gone. Fixed; **demonstrated in the live race: 90 kills, guard fired 30×, zero loss** |
| 3 | ***`rig-write`'s arming — "the capability is absent from the assembly" — was `Assert.False` on a `const bool`.*** A test of a literal | **A class constructing a live socket client was planted in that assembly and all 202 tests stayed green.** Replaced with a structural walk + live control |
| 4 | ***The IL walk certifying `openness-cli` cannot reach `DownloadProvider.Download` passed while examining ZERO method bodies*** | `offenders.Count == 0` is true of *nothing found* and *nothing looked at*. Its negative control existed **only as a comment**; its sibling walker had a live one. Fixed with denominator **and** control — proved to catch different failures |
| 5 | ***The `--yes` gate on `block-layout --set` could be disconnected with a one-token mutation — 712/712 tests stayed green*** | One `if` stands between a missing flag and a write that **destroys retained data**; the test called the refusal helper *directly*, pinning the message not the routing. Now routed through `Main`, asserting Portal was never contacted |
| 6 | ***`tagstatus` — THE ANTI-LAUNDERING TOOL — returned the hard-rule-3 verdict on legitimate C-501 alarm-bit slices*** | **154 false findings across all 92 committed `.ir` files.** A `.%X3` is a slice, not a member. *A gate that accuses correct work of the most serious offence in the project is one that gets disbelieved.* Found **only** by a whole-corpus sweep |
| 7 | ***`preflight` resolved tag references to the DB ROOT only*** — a block reading three invented members passed **`CLEAN`, exit 0** | Hard rule 3 was enforced only by a tool someone had to remember to run by hand. **Nothing in the static pipeline caught an invented member.** Now resolves to member level; 0 findings across the corpus after the fix |
| 8 | ***`diff --only` — the S7 invariance tool — claimed invariance it never checked.*** Retyping `Bool` → `Int` with no network touched printed `INVARIANCE OK … exit 0` | Now fails closed; `--allow-header` is the named escape, **which `gen-block-modify-fix` must never pass** — *a fix needing an interface member routes, it does not declare* |

### 🔴 HIGH — a green that meant nothing, or a false claim in the product

| # | defect | note |
|---|---|---|
| 9 | **The lease crashed at 48 concurrent agents — and was clean at 8, 16 and 32** | `Dispose` deleted the lease file; a Windows delete-pending file answers `CreateFile` with **ACCESS_DENIED**, not the `IOException` a sharing violation raises, so it escaped an `IOException`-only retry. ***The delete was doing a job the OS already does*** |
| 10 | **`compile-all --json` reported `"clean": true` on a run that examined nothing** | `0 && 0`. ***The TEXT formatter had guarded this all along***, printing *"NOTHING EXAMINED … proves nothing about the project"* — the JSON formatter had no guard, three lines from a comment stating the rule. **Machines read the JSON** |
| 11 | **`compile --json` had never once emitted valid JSON on this project** | A prose paragraph followed the object on **stdout**. The project's permanent hardware warning meant **every** run took that branch |
| 12 | **`drift-check` exited 0 having compared nothing** — three routes, including under `--complete`, including every `.ir` unparseable, including **either directory pointed one level up** (both walks are top-level only) | The likeliest real-world mistake, rewarded with a pass. Now `NOTHING COMPARED — this is not a pass`, with `COMPARED: <n>` printed every run |
| 13 | **`candidate-scan --fb <name in no block>`** scanned all 43 files, printed `CANDIDATE SET SIZE: 0`, **exit 0** | **Byte-identical to a real FB with an unambiguous binding.** FI-44's own prescription named both tools; only its sibling got the guard |
| 14 | **`reuse-scan --tag <absent>` → exit 0, no denominator** | Licenses *"write a new block"*. Now exit 2 when **every** queried root is absent — keyed on *all*, not *any*, because a Design-stage query legitimately mixes real and proposed tags |
| 15 | **A bogus `--device` named 43 present blocks and types as absent from the project** | Its JSON gave each a `path` naming the device it claimed not to find them under. `download-plan` named the device correctly **in the same session, same binary** |
| 16 | **The lease-timeout message asserted *"the holder is stuck rather than busy"*** — a judgement the code cannot make | *A hung holder and a busy one are indistinguishable, permanently.* **A human reads that message while deciding what to do, under time pressure** — it would send them to kill a process that was merely slow. Removed; tests pin its absence |
| 17 | **`portal-status` raised an impossibility alarm on correctly-ordered timestamps** | ***A gateway honestly left an unknowable value at `default` — refusing to fabricate a plausible one, correctly — and the consumer compared that default as a measurement.*** Doing the right thing at layer N created the defect at layer N+1 |
| 18 | **Gate 8's blacklist assertion was tautological** — `effective = computed ∪ declared`, then `computed ⊆ effective`, **true for every input by construction** | ***An assertion that cannot fail is documentation wearing a check's clothes — and it occupies the slot where someone would notice a check is owed.*** Found because the obvious mutation could not go red |
| 19 | **A test asserted the defect as the contract**, with a comment explaining it | *"Nothing is COMPARED here at all — and without `--complete` none of it fails."* **The bug was not undetected; it was documented, asserted and defended.** Anyone fixing it would have gone red and assumed they broke something |

### 🟠 MEDIUM — unreachable guards, wrong-shaped refusals, silent drops

| # | defect |
|---|---|
| 20 | **`transient` and `RearmsEachIndex` had no wire representation** — the capability was **unreachable from a binding**, so the gap was met *on the rig*, where the symptom is predicted findings quietly absent. ***An unreachable refusal is worse than no refusal: the system looks like it has a guard*** |
| 21 | **The binding document had NO extension data at all** — every unknown key silently dropped, *on the half of the submission carrying the instrumentation* |
| 22 | **Gate `0b` refused the deliverable over its own 82 `_`-prefixed annotations.** Rationale right, scope wrong. *A gate that refuses every ordinary submission gets switched off* — and a switched-off gate still appears in the list |
| 23 | **A scan-counter wrap was absorbed only on the S7 read path** — and **S7 access is refused CPU-wide on this rig, so every data read goes over Modbus, which absorbed nothing.** Fixed by making the wrong subtraction *unexpressible* (`ScanCount` has no `operator -`), which immediately surfaced **two more wrong `>` comparisons** |
| 24 | **`review`/`digest`/`preflight` crashed on a missing input file** (`0xE0434352`) while `diff`/`ir-hash` refused it cleanly **in the same binary** |
| 25 | **An uncatchable stack overflow** at ~1,700 nested parens killed whole-project walks mid-run. Bounded at 500; the deepest real expression in the corpus is **3** |
| 26 | **`tagstatus` classified names carrying control characters.** A CRLF name list turned **312 correct paths into 168 `MEMBER-NOT-FOUND` + 143 `PROPOSED`** with nothing saying the input was malformed |
| 27 | **`rig-read` crashed unnamed on `{"entries":[null]}`** — exit `-1073741819`, an exit code no caller vocabulary contains. *It did fail closed*; **a crash is loud without being named** |
| 28 | **`reset` was refused by the guard added to protect the store** — recoverable only by hand-editing `ProgramData`. ***A guard that blocks its own recovery path turns a recoverable incident into an outage*** |
| 29 | **`cross-check` counted writes from blocks reachable from no OB** — almost certainly the *"three multi-writers that do not exist"* recorded in the campaign plan. Now reported, **never subtracted** |
| 30 | **`block-edit` claims did not cover DBs and UDTs.** The lookup asked exactly one question, so *"does not exist"* was **literally true and completely misleading.** ***Widened rather than forked*** — a second kind would have split the registry in the kind dimension |
| 31 | **`drift-check` reported one tag table BOTH as `EXPORT-ONLY` and as `SKIPPED`** — TIA's name has spaces, pairing was by filename. **One object, two contradictory absence rows**, one reading as *"in the controller and no `.ir` describes it"* |
| 32 | **`drift-check` `MATCH`es a pair `compare` refuses** (`MemoryLayout` silence) and **stated no layout scope at all**. Now printed every run, with the *don't fix it in the Normalizer* warning attached |
| 33 | **A committed fixture used `//` keys that gate `0b` refuses** — and **had never once been passed through `--binding`**, the purpose it exists for. *A trap with a date on it* |
| 34 | **`export-all`'s recommendation was over-broad** — `Safe to compare against with drift-check --complete`, true of blocks and types, **silent about tag tables**, offered exactly when the reader decides what to do next |
| 35 | **A `repo:`-prefixed allowlist entry means different things to the C# fence and the PowerShell fence.** Both fail closed today — *luck, not design*. Documented and pinned; semantics deliberately unchanged |
| 36 | **A duplicate JSON key upgrades a device's kind** — `"kind":"plant","kind":"test-rig"` → **ALLOWED**. Owner-authored file, so not an attack path, but *it does not behave as reviewed* |
| 37 | **`compile`'s own warning count is inflated** by counting rollup nodes while blaming TIA. Not a false green — the verdict takes `Math.Max` |
| 38 | **`block-layout --set`'s help still calls the re-import revert `UNVERIFIED`** and steers readers to `--expect`, **which only detects it** |

### ⚪ The four found in the campaign's OWN instruments

Recorded separately because they are the sharpest evidence that the method works — and that it must
be pointed at itself.

| # | defect |
|---|---|
| 39 | **A torn-store probe silently failed** (Windows Python cannot open a `/c/...` path) so every case read an intact store and exited 0 — ***a clean sweep measuring nothing, produced by the instrument built to detect exactly that*** |
| 40 | **A mutation that did not compile emitted NO result line at all** — *which is not a pass*, and only one of those two announces itself |
| 41 | **A test passed for the wrong reason**: a fixture for *"an unknown key is silently dropped"* used `specname`, which **binds**, because both readers set `PropertyNameCaseInsensitive`. Green, committable, ***and evidence for a proposition it never tested*** |
| 42 | **A release script derived its own inputs from dispatch order** — *allocation order is not dispatch order* — so half its releases silently did nothing. It trusted **its own prediction of a value it could have read** |

---

## PART 3 — WHAT WAS MEASURED

| | |
|---|---|
| **Concurrency** | **1 → 128 agents, flat.** 69–122 ms per agent across a 16× range, **no knee.** Every level re-taken on the post-fix build; the pre-fix rows are struck |
| **Ceiling** | Set by **the caller's own timeout** (~330 agents at the 30 s default), **not a breakdown** — and the failure there is a *named retryable refusal* |
| **Sustained** | 32 agents × 8 rounds = **256 submissions over 30 s**, all exit 0, all on disk, zero crashes |
| **Wave depth** | To **60 waves / 240 slots**. Edge model confirmed **to the unit** (`4·W(W-1)/2` = 7,080, +1 for the probe pair) — but `status` grew **11%** against a 9.3× edge set: *a memory and reporting property, not a latency one* |
| **Store scaling** | Isolated rather than assumed: **87 / 104 / 101 ms** at stores of 1 / 64 / 256. **Flat.** *The lease is the only thing that scales* |
| **Liveness** | `kill -9` on a holder → next acquirer **exit 0 immediately**. ***There is no stale-lease state to detect*** — the lock is the OS handle |
| **Both directions pinned** | 4 disjoint blocks → `CONCURRENCY 4`; 4 slots on one block → ***`SERIALISED` — a correct result, not a failure*** |
| **Controller state** | `export-all --tagtables` → 45 objects, then `drift-check --complete`: **38 match, 4 drifted, 3 in the controller with no `.ir`** — the third being a **false** `Default tag table` row from the pairing defect at row 31. *(This cell read `2` until 2026-08-23; the run's own log names all three — `docs/notes/test-log.tsv:68`.)* |
| **Gate count** | **25 gate rows EMITTED**, taken by running the built CLI over one real deliverable on 2026-08-14. The docs said 23, and both gate tables drift. *On an empty vector set, gate `0` is emitted instead of the other 24.* ⚠️ **An emitted-row count, not a defined-gate count** — not every gate emits on every run. `SubmissionGate.cs` **defined 26** distinct gate names that day (`db9ef27`, 04:24) and **defines 30 today** |

---

## PART 4 — WHAT WAS DELIBERATELY *NOT* DONE

Recorded so each can be disagreed with.

1. **No conformance wave was run.** The rig was serving and a download to it is permitted. **The
   marginal information was small** — the phase-armed latch did not exist, so 13 signals could not be
   instrumented and the wave would have returned a partial result whose gaps were already known —
   **and the risk to a known-good rig was real.** ⚠️ *In hindsight this was right about the deploy and
   too conservative about the generator, which needs no rig at all and is now being built.*
2. **No withdraw verb for a leaked slot.** Analysis found something worse than a gap: with the slot
   removed the store reports ***`NO CONFLICT EDGES: the slots are disjoint on every computed
   relation`*** — **a positive claim of disjointness about a hazard it can no longer see.** ***The
   recovery would have caused the incident.***
3. **No expiry heuristic** anywhere. *A hung holder and a busy one are indistinguishable* — an expiry
   is **a wrong answer delivered on a schedule.**
4. **The permit half of the write fences was never exercised.** Every invocation was one that had to
   be refused, so the lane structurally could not reach the rig. **Read the fence results as *"these
   forms are refused"*, never as *"the fence is correct"*.**
5. **`repo:` and duplicate-key semantics left unchanged.** Documented and pinned instead — *changing
   what an allowlist entry MEANS, unreviewed, overnight, is not an autonomous call.*
6. **Case-sensitivity left open**, with the warning kept verbatim: ***a comparator learning to equate
   representations is how a comparator starts passing things.***

---

## PART 5 — THE PATTERN

**One defect shape accounted for more than half the findings**, and it has a name in this project
already: ***empty is not clean.*** What the campaign added is how many disguises it wears.

| disguise | instance |
|---|---|
| `All` over an empty sequence is `true` | the hard-rule-4 gate |
| `count == 0` after examining nothing | the IL walk certifying no download |
| `0 && 0` is vacuously true | `compile-all --json` |
| An absence read as a valid state | the wave store — **correct exactly once** |
| A tautology that cannot fail | gate 8's superset check |
| A test of a literal | `rig-write`'s arming |
| A control that was never executed | the IL walk's negative control, *as a comment* |
| A scope narrower than its wording | `export-all`'s advice; `drift-check`'s silent `MemoryLayout` |
| An honest default re-read as data | `portal-status` |

**Five rules earned tonight, in the working agreement:**

1. ***An absence that is correct exactly once is a bug for the rest of time.***
2. ***A check whose exercise requires editing the check will not be exercised.*** — the root cause of
   the one control that had already rotted.
3. ***A whole-corpus sweep reaches defects no targeted probe can***, because the input you would have
   to guess is already in the repository. **154 findings from one sweep, after a night of probes.**
4. ***A defence built on a guarantee you already have is pure risk*** — it cannot add safety and it
   can subtract it.
5. ***Deleting an input from a system that reports derived conclusions produces a WRONG conclusion,
   not a smaller one.***

---

## PART 6 — THE ORCHESTRATOR'S OWN ERRORS

Listed because a campaign about false greens that omits its own would be one.

| error | caught by |
|---|---|
| **Diagnosed `portal-status` as a date-blind comparison.** It was exact; a lane found the real cause | a lane, which **checked rather than implementing my diagnosis** |
| **Rewrote a status headline and left the superseded section standing** — *and the stale half was the one with a command in it*, so a reader would have obeyed it | a lane |
| **Applied the CRLF house style to a file `.gitattributes` deliberately exempts.** Harmless only because `.gitattributes` overrode me | myself, after the fact |
| **Swept a lane's uncommitted append into my own commit.** *Path-scoping protects the committer, not another lane's work in the same file* | the lane whose work it was |
| **`git show --stat \| tail -3` read the commit message, not the file list** — *a truncated view of a verification is not a verification* | myself |
| **Ruled twice, wrongly, on the 13 `Latched` declarations** — and both times a lane refused to execute the ruling, on evidence I did not have | two lanes |
| **Left the binding review for the engineer.** D6 requires the reviewer be *someone other than the transcriber*, **not a particular person** | the owner |

***The two refused rulings are the most important line in this table.*** **A lane that cannot refuse
the orchestrator is not a check** — and on both occasions the refusal was correct.

---

## PART 7 — WHAT REMAINS OPEN

| | |
|---|---|
| 🔄 **The phase-armed latch** | In progress. Needs no rig — *it should have been built overnight and was not* |
| 🔴 **The 27 vectors** | Still never run. After the latch, the remaining blocker is the mirror at **35/35**, which needs a re-deploy |
| 🔴 **`GenProject1`'s IR ≠ its controller** | 4 objects drifted, 3 with no `.ir` (one of the three false — see row 31). ***Reconcile before importing anything.*** ⚠️ **The 08-14 set is SUPERSEDED**: today it is **5 drifted**, a different set — `docs/notes/live-project-readiness.md` |
| ⚠️ **The permit half of the fences** | `NOT CHECKED`. Needs a supervised session |
| ⚠️ **`hmi --screen <no match>`** | **Unmeasurable here** — `GenProject1` has no HMI device, so a match-nothing run and a bare run are indistinguishable, both exit 0. Needs an HMI scratch project |
| ⚠️ **Two binding questions** | State the slot partition explicitly; does the schema admit a multi-tag entry? Neither blocks anything |
| ⚠️ **Case-sensitivity** | Open, with the measurement that would settle it recorded |

---

**172 commits · 116 log rows · 42 defects · five components · four lanes that each closed
themselves.**
