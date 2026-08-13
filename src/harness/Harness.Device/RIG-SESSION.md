# THE RIG SESSION — the exact command sequence the deployment gateway executes

Written 2026-08-13 by the harness lane, alongside `Harness.Device`. **Every command below is one
`OpennessDeviceGateway` emits**, in this order, and each flag is traced to the argument parser that
accepts it. `ArgumentVocabularyTests` asserts that trace mechanically by reading those parsers, so
this document and the code cannot drift apart silently.

> ## 🔴 NOTHING BELOW HAS EVER BEEN RUN
>
> The gateway is built, its ordering is tested, its argument vectors are checked against the real
> parsers and its exit-code reading is tested. **No step of it has been executed against Portal or a
> controller.** A gateway that compiles is not a gateway that deploys. Read every "expected" in this
> document as a prediction.

---

## 0. WHAT THE OPERATOR MUST SUPPLY — none of it is guessable, and the gateway refuses rather than defaulting

| value | how to obtain it | status |
|---|---|---|
| `<project>` — the `.ap20` | **Must be named in an ALLOWLIST.** Both the probe and this gateway compare the RESOLVED PATH against `tools/download-probe.allowlist` (committed) and `%ProgramData%\Ladder-AI\download-probe.allowlist` (machine-local). No flag, no environment variable, no override. | ⚠️ **The rig's own project needs one line in the MACHINE-LOCAL file** — its path is a copy of a live engineering job and may not be committed. **That file is the owner's to write and must never be created by an agent**: writing it is granting a download target. `GenProject1.ap20` is already in the committed list. |

> #### ⚠️ CORRECTION TO AN EARLIER VERSION OF THIS DOCUMENT
>
> §0 previously said **"NO SUCH PROJECT EXISTS ON THIS MACHINE"** under the old file-name-suffix
> guard. **That was wrong**, and it was this lane's own over-generalisation: the measurement was that
> `GenProject1.ap20` and `SampleProject.ap20` — the two entries in `confirm-roundtrip.allowlist` —
> did not match the suffix. **A `Live Runs/` scratch copy did**, because the suffix had been fitted to
> exactly those projects. Had the fence been rebuilt as a repo-only allowlist on the strength of that
> sentence, it would have broken the one project that worked. Hence two allowlist files, and hence
> this correction rather than a quiet edit.
| `--group <device>/<path>` | `openness-cli list <project>` — copy the `Path` column **verbatim**. | OPERATOR |
| `--pc-interface "<name>"` | `openness-cli download-plan <project> --json` — it prints the configured PC interfaces with their `#<n>`. | OPERATOR |
| `--target "<name>"` | same report; needed only if the chosen PC interface offers more than one. | OPERATOR, optional |
| Modbus host / port / unit | the rig's IP. | OPERATOR |
| `--options` | `Software` or `SoftwareOnlyChanges`. **`SoftwareOnlyChanges` is the only one measured to complete on this rig** (`docs/notes/test-environment-build-plan.md`, the 19-object run). `Software` with `--disruptive` is **unmeasured here**. | ⚠️ GUESS if `Software` is chosen |

### Binaries

```
src\converter\Converter\bin\Release\net8.0\converter.exe
src\openness-cli\OpennessCli\bin\Release\net48\openness-cli.exe
src\openness-cli\DownloadProbe\bin\Release\net48\download-probe.exe
```

### ✅ THE TWO BINARY PRECONDITIONS ARE SATISFIED (2026-08-13)

Both were done in a window where `portal-status` reported **0 processes / 0 orphans / 0 strays**:
a full `dotnet build -c Release src/openness-cli/openness-cli.sln`, then
`openness-approve-build.ps1 -Exe` on `download-probe.exe`. Verified by hash —
**`Entry (11) THIS BUILD`, 1 match, across TIA 17.0 / 19.0 / 20.0.**

So `Release\net48\download-probe.exe` now carries the allowlist fence and emits the first-class
`loadManifest`, and it is approved.

> #### 🔴 IF YOU EVER REDO THIS: **BUILD FIRST, APPROVE SECOND, `-Status` THIRD.**
>
> **The order was got wrong the first time and the runbook did not warn about it.** The binary was
> approved and *then* the solution build was run. **The build changed the file, so the approval
> instantly became `same path, older hash`** — a stale grant.
>
> A stale grant fails **at attach**, in exactly the way the box below describes, so it presents as a
> wedged Portal rather than as "you approved the wrong file". An operator who approves early gets a
> refusal that looks like a completely different problem.
>
> ```
> openness-cli portal-status                       # 1. must show no lane holding Portal
> dotnet build -c Release src\openness-cli\openness-cli.sln          # 2. BUILD FIRST
> tools\openness-approve-build.ps1 -Exe src\openness-cli\DownloadProbe\bin\Release\net48\download-probe.exe   # 3. THEN approve
> tools\openness-approve-build.ps1 -Status         # 4. CONFIRM THE HASH MATCHES, not just the path
> ```
>
> `-Status` must report the entry as **THIS BUILD**. *"N entries, 2 name this exact path, **0 match
> this file's hash**"* is the stale-grant reading, and it is genuinely unapproved.

> ⚠️ **APPROVED IS NOT VERIFIED.** The approval tool says so itself: only an **attach** proves the
> grant works. That attach is step 0 of the session (`openness-cli list`), and it is the first thing
> that can still go wrong.

> ⚠️ **Until it is approved the first attach either hangs to the connect timeout or throws
> `EngineeringSecurityException`. BOTH MEAN "NEEDS APPROVAL", NOT "PORTAL IS WEDGED".** That
> misreading has cost this project an hour before, and an operator discovering it mid-session is
> exactly the person most likely to make it.

> ⚠️ **DO NOT REBUILD `openness-cli` OR `download-probe` NOW.** TIA whitelists by `(Path, FileHash)`,
> and the approval above is keyed to **that exact hash** — a rebuild voids it and puts you back in the
> stale-grant case. `dotnet test src/openness-cli/openness-cli.sln` **IS** a rebuild. `converter.sln`
> and `harness.sln` are safe.

---

## 0b. THE SESSION AGENDA — AND ITS ORDER IS NOT ARBITRARY

**Full agenda and rationale: `docs/notes/test-environment-build-plan.md`, *"THE SESSION'S ORDER IS
NOT ARBITRARY — THE DESTRUCTIVE PROBES GO LAST"*.** Not duplicated here; what follows is the shape
and the one constraint that governs it.

> ### 🔴 THE DESTRUCTIVE PROBES END THE HEALTHY WINDOW, AND THEY DO NOT GIVE IT BACK
>
> **A6's POST-abort throw leaves the CPU STOPPED with a recovery download owed — 34 s, measured.**
> Everything that needs a *healthy, loaded* controller must therefore happen **before the first
> deliberate abort**, or every repetition costs a recovery cycle and the session becomes
> download-recover-download.

| # | what | why it sits here |
|---|---|---|
| 1 | **Deploy** — §1 below, the loop putting an object on a controller for the first time | everything else needs a loaded CPU |
| 2 | **5.2's test run** — vectors against the block under test, result package back | the milestone; needs the deployment intact. Fold in the two 6.6/6.7 items — no wave has ever run compressed, and X-D's `k ≈ 5` timer floor is the spec's number rather than a measured one |
| 3 | **The three named read-only exports** | free while the project is open, and each closes a class with no new test code |
| 4 | **A6 repetition, both abort kinds** | ⚠️ **DESTRUCTIVE — the first abort ends the healthy window** |
| 5 | **Does an ORDINARY delegate exception kill Portal?** | same class, and the one that matters operationally: D32's throw-on-unhandled fires in production |

**Steps 1–3 are the healthy window. Step 4 closes it.** Nothing after step 3 should be scheduled on
the assumption that a loaded controller is still available.

> ### RECORD THE RUN STATE AT EVERY STEP, NOT JUST THE OUTCOME
>
> **A6's two abort kinds differ in exactly that**, so a session log that carries only outcomes
> cannot tell a CPU that kept running from one that stopped and was recovered — and those are the
> two findings the repetition exists to separate. `download-probe`'s report already carries
> `cpuRunStateAdvisory` and the feedback parser's `runStateTransitions` / `runStateDisclosed`;
> **`RunStateDisclosed = false` is "the download said nothing about it", which is a gap and not a
> reassurance.** Read it back independently after each step rather than inferring it.

---

## 1. THE SEQUENCE

Substitute: `%P%` = project path, `%G%` = group path, `%D%` = device name, `%IF%` = PC interface,
`%S%` = staging directory.

### Step 1 — CONVERT (no Portal, free, safe any time)

```
converter.exe to-xml %S%\ir\<Object1>.ir %S%\ir\<Object2>.ir ... [--project <ir-dir>] --out %S%\xml
```

* Source: `src/converter/Converter/Program.cs` → `RunConvert`. `to-xml` is one of the two accepted
  modes; `--project` and `--out` are read in its own parse loop (`rest[i] == "--out"`).
* Every object in ONE invocation, so the batch resolves its own callee/tag types.
* **`--allow-blind-types` is deliberately NOT passed.** FI-71's refusal is wanted: a guessed member
  type imports and is rejected by TIA at compile, after a full round trip.
* **Exit 0** required. **Exit 1** → `Failed`, nothing imported. The likeliest cause is FI-71.

### Step 2 — IMPORT

```
openness-cli.exe import-all %P% --group %G% %S%\xml --timeout-connect 180 --timeout-open 1800
```

* Source: `ArgumentParser.ParseImportAll`. Case arms: `--group`, `--json`, `--dry-run`,
  `--tia-install`, `--timeout-connect`, `--timeout-open`; everything else positional.
* `import-all`, not `import`: `import` takes its files as ONE kind in the order given and stops at the
  first failure. A harness deployment is a **mixed set** (tag table + block, and a DB when the program
  under test has one) in a dependency order filenames cannot express. `import-all` reads the kind from
  each file's SimaticML root element and retries to a fixpoint.
* **Exit 0** = everything went in. **13 = ImportIncomplete** → `Failed` (the project does not contain
  everything supplied, *including a file never attempted*). **3** → `NotProven`, read as *needs
  approval* on a freshly built binary.
* This is **not** a compile gate: everything imported is flagged inconsistent.

### Step 3 — RE-ASSERT THE MEMORY LAYOUT, PER DATA BLOCK

**Only emitted when the deployment contains a `DataBlock`.** See §3 below — on the harness's own
waves it currently emits nothing, and the plan says so out loud rather than staying silent.

```
openness-cli.exe block-layout %P% --block <DB> --set Standard [--device %D%] --yes
openness-cli.exe block-layout %P% --block <DB> --expect Standard [--device %D%]
```

* Source: `ArgumentParser.ParseBlockLayout` — `--block`, `--device`, `--set`, `--expect`, `--json`,
  `--yes`, `--tia-install`, `--timeout-connect`, `--timeout-open`. `Standard` is matched by
  `TryParseMemoryLayout`, which deliberately is not `Enum.TryParse` (that would accept `"0"`/`"1"`).
* **Order matters and is asserted:** `--expect` only tells you it broke; `--set` is what repairs it.
  Both come **after** the import, because the import is what reverts the block to `Optimized`.
* **Exit 15** → `Failed`. An optimized block is **not an error** on classic S7comm — it is simply
  ABSENT, and the read fails at the first DATA access rather than at connect, which presents as a
  wiring problem.

### Step 4 — THE GATE (hard rule 4)

```
openness-cli.exe compile-all %P% [--device %D%] --timeout-connect 180 --timeout-open 1800
openness-cli.exe sanity-check %P% --timeout-connect 180 --timeout-open 1800
```

* Sources: `ParseCompileAll`; `ParseSanityCheck`, which delegates to `ParseList` (hence
  `--timeout-*` are accepted there — the vocabulary test follows that delegation explicitly).
* **`compile-all`, not `compile`:** FI-52 — only a per-item compile clears an imported block's
  inconsistent flag, and after a bulk import that is every object. `compile-all` does them in one
  Portal session, types first, retrying while progress is made.
* **Exit reading — this is the half that matters:**
  * `0` → Ok.
  * `8` → **Failed**, *examined and wrong*.
  * `11` → **Failed**, *something is still inconsistent — never examined at all*.
  * `14` → **NotProven**, `NOTHING EXAMINED`. **This stops the deployment exactly as a failure does.**
    An item that compiled WITH ERRORS is still flagged CONSISTENT and errors do not survive the
    process, so the run right after a failed one is the one that examines nothing and looks cleanest.
  * `sanity-check` `9` → Failed. Read **both** the `BLOCKS:` line and the `TYPES:` line (FI-62).
* **Nothing keys on a `State` string anywhere.** A healthy project with standing hardware warnings
  returns `Warning, errors=0`.

### Step 5 — DOWNLOAD

```
download-probe.exe %P% --options SoftwareOnlyChanges [--device %D%] --pc-interface "%IF%" [--target "..."] --log-dir %S%\probe-logs --disruptive --json --timeout-connect 180 --timeout-open 1800
```

* Source: `DownloadProbe/ProbeArguments.cs` → `ProbeArgumentParser.Parse`. Every flag above is a bare
  literal on that ordinal switch. **There is no `--yes`, no `--force`, no `--confirm`** — the gateway
  never emits one, and an unknown option is a hard usage error (exit 1).
* **`--disruptive` is required for the download to complete at all.** Without it the policy answers
  every configuration with `NoAction` and the download **aborts** — every previous run of that tool
  succeeded by refusing. `DeviceGatewayOptions.AllowCpuStop` must be `true` or no plan is produced.
* ***EXPECT THE CPU TO BE LEFT STOPPED. BE AT THE MACHINE.***
* **Granularity is DEVICE-LEVEL.** `Download()` takes a connection, two callbacks and a
  `DownloadOptions` — no block, no group, no selection. There is no per-block download; this
  transfers the whole PLC software.
* The gateway **never** passes `--to-folder`, `--throw-from-pre-delegate` or
  `--throw-from-post-delegate` (experiment 1.7 breaks the download on purpose; a POST throw can leave
  the CPU stopped with no route to start it from inside that download).

**Exit reading:**

| exit | reading | what the loop does |
|---|---|---|
| `0` Completed | **Ok — and explicitly NOT evidence of transfer.** `DownloadResult.State` was `Success` on a live run that carried nothing. | reads the load manifest, which decides `Loaded` |
| `8` CompletedWithErrors | Failed | `Attempted = true`, `Loaded = false` |
| `7` AbortedByUnhandledConfiguration | Failed **for a deployment**, even though it is a successful *experiment* for the probe | `Attempted = true` |
| `10` SelectionApplyFailed | **NotProven — the run is VOID.** The tool stopped answering after the first configuration it could not apply; device state unknown | `Attempted = true`, must be read before trusted |
| `3` RefusedByPath | Failed. Portal not contacted. **If this fires, the gateway's own fence is looser than the probe's** — that is the bug to fix, and `Harness.Device.Tests` asserts the property that prevents it | nothing happened |
| `6` SafetyRefused | Failed. Hard rule 2: stop and report | nothing happened |
| `4` NoProvider / `5` NoDownloadTarget | NotProven / Failed | |
| `1` UsageError | Failed — **a flag the gateway emits is not one that binary accepts.** Check against `ProbeArguments.cs`, never a README | |

### Step 6 — THE LOAD MANIFEST (the only positive evidence of transfer)

Not a command. The gateway reads `download-probe --json`'s **`loadManifest`** object — built by
`DownloadResultAdapter` off the live Openness `DownloadResult`, which is the first-hand path
`ProbeLogReader`'s own documentation names. `loadManifest.source` says which authority produced it and
the gateway reports that verbatim; **`available: false` is read as "nobody looked", never as "TIA
loaded nothing"**.

*Fallback:* on an older probe build with no `loadManifest`, the gateway parses the embedded `log`
array through `ProbeLogReader` instead and labels the source accordingly. **The Release binary is no
longer that older build** (§0), so `loadManifest.source` should read `DownloadResultAdapter` —
**first-hand**. If it reads `ProbeLogReader`, something is running a stale binary and the manifest is
a re-derivation from a rendering rather than the object itself.

`Loaded` is true only when **both**:

1. the re-derived verdict is `Transferred` (never `Undetermined`, never `NothingTransferred` — the
   latter means the controller already held this build, and every turn of the loop generates a new
   build stamp, so up-to-date means what was generated is *not* what is running); **and**
2. every **downloadable** object supplied is named in the manifest.

**Tag tables are excluded from (2), and that is measured, not assumed.** The recorded 19-object
manifest from this rig names an FC, an FB, its instance DB, OB1, `MB_SERVER` and nine `TCP_MB_*`
helpers — and no tag table.

### Step 7 — OPEN THE MIRROR

`NModbusTransport.Connect(host, port, unitId)`. Refused unless the last deployment was both
`Attempted` and `Loaded`.

---

## 🔴 STEPS 8 AND 9 — TWO CALIBRATIONS, ONE WRITE EACH, AND THEY MUST COME FIRST

**Do these IMMEDIATELY after the mirror opens and BEFORE anything is concluded from a multi-word
read.** Both are `OwedOnTheDevice` items, both are settled by a single write, and it would be absurd
to hold a rig session and not spend the sixty seconds. **A wrong word order silently corrupts every
32-bit reading taken after it** — the value is not obviously wrong, it is a different plausible
number.

### Step 8 — THE START-BOOL BIT ORDER. Write `16#0001`.

`MirrorGeometry.BitAddressOf` is the **one `[I]` in that file**, and its own remark says why it
matters: a Modbus register travels big-endian and an S7 `%MW` is big-endian, so **bit 0 of the
register value lands in the SECOND byte, not the first** — the counter-intuitive half. Today it
renders bit `b < 8` as `%M{byte+1}.{b}`.

> ***THE CURRENT AGREEMENT IS WORTH NOTHING, AND THAT IS THE POINT.*** The simulator and
> `BitAddressOf` agree **from the same premise** — the simulator was written against the same
> mapping — so every green so far is a textbook correlated check. Only the device is an outside
> authority. **Discharge this FIRST: several later readings depend on it.**

| step | do | outcome |
|---|---|---|
| 8a | Write `16#0001` to slot 0's start-bool register (`MirrorClient.RaiseStartBools` for slot 0 does exactly this when its `StartBitInRegister` is 0) | — |
| 8b | Poll `ReadControl().Executed(0)` — the **ECHO** (X-E), which is what the PROGRAM saw, not what the client commanded | **`true` ⇒ `BitAddressOf` IS RIGHT.** Mark it `[M]` and change nothing |
| 8c | Only if 8b stays `false`: lower it, then write `16#0100` and poll the echo again | **`true` here ⇒ THE TWO BYTES SWAP.** `BitAddressOf` becomes `%M{byte}.{b}` for `b < 8` and `%M{byte+1}.{b-8}` for `b ≥ 8` — **and it changes there and NOWHERE ELSE** |
| 8d | If NEITHER raises the echo | Not a bit-order result. The copy layer's start-bool network is not driving the block at all — a deployment or generation fault, and 8 is unanswered rather than answered negatively |

**Read the ECHO, never the start-bool register.** `ControlSnapshot.StartBools` is what the client
wrote and reads back identically under either mapping; `StartEcho` is what the block's own start
condition latched. Only the second can tell the two apart.

### Step 9 — THE 32-BIT WORD ORDER. Read the version register.

`RegisterWordOrder`'s default of `HighWordFirst` is **an inference, not a measurement** — §11 asks
for exactly one calibration with a known bit pattern. **The pattern is already there**: the copy
layer publishes the build stamp the gateway computed, so the known value is in hand and this costs
one read.

`VersionCheck.Confirm` already reports the answer as a distinct outcome — **no new code**:

| `VersionOutcome` | meaning | action |
|---|---|---|
| `Confirmed` | the pair reconstructs the expected stamp under `HighWordFirst` | **the default is CORRECT.** Mark it `[M]` |
| `WordOrderSuspect` | the pair equals the expected stamp **with its two halves swapped** | ***THE ORDER IS `LowWordFirst`.*** Almost certainly the uncalibrated transform and **not** a failed download — set it and re-read |
| `Absent` | stable at zero | the copy layer is not running. Not a word-order result |
| `Stale` | stable at a *different* stamp | the download did not land. Not a word-order result |
| `Unsettled` | never stable | the settling window is longer than allowed, or it is flapping |

**The same transform carries the SCAN COUNTER**, which the liveness check keys on — so an uncalibrated
order does not merely misreport a version, it makes `ScanAdvance` arithmetic wrong and turns liveness
into a number nobody can trust. That is why this precedes step 2 of the agenda and not just step 10.

### What this session CAN and CANNOT take off `OwedOnTheDevice`

**Discharged by the session, if it runs:** *deployment itself* (item 1) and *the load manifest*
(item 2) fall out of a successful step 5–6; **the start-bool bit order** (item 4) and **the 32-bit
word order** (item 5) are steps 8 and 9 above.

**NOT discharged, and nobody should expect the list to empty:**

- **F-1's premise** — that `MB_SERVER` serves a wide multi-slot read as coherently as a narrow
  single-slot one. Reasoned, not measured; needs a deliberate multi-slot experiment, not a by-product.
- **A5, slot non-interference on a 1214C.** What is shown PC-side is that the harness does not itself
  couple two slots and that a coupling which exists would be caught — not that the CPU does not couple
  them.
- **The echo latch's necessity.** The copy layer re-drives the start condition every scan, so within
  an index a level coil reads identically to the SCOIL. The latch guards a start condition the copy
  layer does not drive, and **nothing in this session exercises that**.
- **Scan-accurate settling.** The check compares a recorded value against the value some scans later,
  which catches a value still moving and **cannot see one that moved and came back**. A stronger
  check, not a measurement.

**So a clean session takes the list from 8 to 4.** Anything that claims more has counted something
twice.

---

## 2. WHAT TO DO WHEN A STEP DOES NOT PASS

The gateway stops. **No download is attempted after any step that did not pass** — including one that
returned `NotProven`, because a gate that did not run is not a gate that passed. `LoopResult` then
reports `NotDeployed`, **no packages are produced at all**, and `LoopResult.Deployment.Detail` carries
the command line of every step that ran with its reading.

A `NotProven` on a *timeout* is the one case that needs a human: the command may still hold the
project. **Do not start another Portal command against the same project** — run `openness-cli
portal-status` (read-only, never attaches) first.

---

## 3. THE `MemoryLayout` SEQUENCE — WHAT IT COVERS, AND WHAT IT CURRENTLY DOES NOT

The brief for this lane said a harness DB read over classic S7comm must be `Standard`. **On this
harness that premise does not arise, and the reason is worth stating because it looks like a gap:**

* The mirror is **`%MW` bit memory, not a DB** — `MirrorGeometry`'s own remark, spec §6, chosen
  *precisely because* an IR-authored DB imports with no `MemoryLayout`, TIA applies the S7-1200
  default of `Optimized`, and `MB_HOLD_REG` then refuses it with `16#818C` while import, compile and
  `drift-check` all stay green.
* The loop's transport is **Modbus TCP through `MB_SERVER`**, not classic S7comm. Classic S7comm is
  used by `Harness.S7` (Sharp7) for the rig marker DB, which is a different path and not part of this
  gateway.

So the re-assertion in step 3 exists **for a data block in the program under test**, and on every wave
the harness generates today it fires zero times. That is a guard that could silently stop running, so:

* `DeploymentPlan.LayoutNote` states which case the run was, **on every plan**, including the no-op
  one — and says in as many words that a no-op is *not* evidence the re-assertion works.
* `DeploymentPlanTests` asserts both branches.
* **It has never been exercised against a real import.** ⚠️ Add a DB to the first rig session's
  program under test if you want that path proved.

---

## 4. THE SMALLEST USEFUL FIRST SESSION

Recommended order, cheapest refusal first:

0. **The one precondition still open**: ⚠️ **one line in
   `%ProgramData%\Ladder-AI\download-probe.allowlist`, written by the OWNER**, naming the rig
   project's absolute path. *(The two BINARY preconditions are done — see §0. Do not rebuild:
   the approval is keyed to that exact hash.)*
1. `openness-cli portal-status` — no lane holds Portal.
2. `tools\openness-approve-build.ps1 -Status` — the entry must read **THIS BUILD**, matching the
   hash and not merely the path.
3. `openness-cli list %P%` — copy the `Path` column for `--group`. **This is the first real attach,
   and it is what actually proves the approval** — approved is not verified.
4. `openness-cli download-plan %P% --json > plan.json` — read the exact `--pc-interface` string.
   **Read-only and incapable of downloading**; an IL test asserts it.
5. `download-probe %P% --options SoftwareOnlyChanges --to-folder %S%\image --json`
   — **⚠️ NOT a step the gateway emits, and it is proposed here as a rehearsal**: non-destructive,
   nothing on the wire, no CPU stopped, and it reaches *the compile a download runs*, which is
   measurably not the compile `--block` / device / `compile-all` / `sanity-check` run. If this
   fails, step 5 of the real sequence would have failed on the wire.
6. Then the full sequence, once, with the operator at the machine — followed **immediately** by the
   two calibrations (steps 8 and 9), then the agenda in §0b, **destructive probes last**.

> ⚠️ **Redirect, never pipe.** `openness-cli` and `download-probe` launch Portal as a child that
> inherits their standard handles, so a pipe outlives the command and the read never returns. The
> gateway's `ProcessRunner` therefore runs everything through `cmd /c … > file 2> file` and never sets
> `RedirectStandardOutput`. A rig operator typing these by hand must do the same: `>` / `Out-File`,
> not `|`.
