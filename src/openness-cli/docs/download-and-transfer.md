# openness-cli — download and transfer

🔴 The only path in this repository that can move a program onto a controller. Read `docs/notes/live-project-readiness.md` before relying on anything here, and note ADR-0013: `download-probe` is no longer fenced.

> Split out of `src/openness-cli/README.md` on 2026-09-17. That file remains the index and the invocation contract.

## `download-plan` — what a download would comprise, and why it cannot perform one (2026-08-11)

### Granularity, the provider acquisition path, and what the connection is NOT (migrated from CLAUDE.md 2026-08-21)

READ-ONLY, DRY-RUN ONLY. No `--yes`, no `--force` (both refused BY NAME), no confirmed form; nothing
in the binary reaches `DownloadProvider.Download`, and a test walks the compiled IL of EVERY method
in the assembly asserting so (negative-tested by retargeting it at `ICompilable.Compile`, which it
correctly caught).

**GRANULARITY IS DEVICE-LEVEL AND THAT IS THE POINT.** `Download()`'s three overloads each take a
connection, two callbacks and a `DownloadOptions` — **NO block, group, selection or exclusion** — so
"download my one new DB" IS NOT A THING THIS API DOES; the smallest real unit is THE WHOLE PLC
SOFTWARE. `SoftwareOnlyChanges` means "the parts TIA finds different from the controller", decided
by TIA at download time, **NOT** "the blocks you edited" — which is why the default is the
unambiguous `Software`.

**Reports the PROVIDER ACQUISITION PATH.** `DownloadProvider`'s only ctor is internal and it
implements `IEngineeringService`, so **`IEngineeringServiceProvider.GetService<DownloadProvider>()`
is the ONLY route there is.** Which object answers is unstated by the API, so it asks outward from
the software-bearing item and reports EVERY object tried with its `GetServiceInfos()`.

**Reports the CONNECTION from `DownloadProvider.Configuration`**
(Modes→PcInterfaces→TargetInterfaces→Addresses) — a **PROJECT-MODEL read, never a connect.**
`GetAccessibleDevices()` (a live scan) and `ApplyConfiguration()` are **never called**, so these are
CONFIGURED addresses that say nothing about what answers.

REFUSES safety (exit 6): device-level granularity means no download of an F-capable PLC excludes its
safety program, so planning one is planning to write it. **Exit 16 = no provider obtainable** — the
plan ran and describes nothing (empty is not clean). Says NOTHING about whether a download would be
PERMITTED — that is the write fence's question (ADR-0009).

```
openness-cli download-plan <project> [--device <name>] [--options Software|SoftwareOnlyChanges|Hardware] [--json]
```

**This command plans and cannot download.** There is no `--yes`, no `--force` and no confirmed form;
`--yes` and `--force` are *refused by name* rather than ignored, because someone who types one has
concluded this command can be talked into it. No argument, environment variable or build
configuration in this binary reaches `DownloadProvider.Download` — the one method that would,
`IOpennessGateway.PerformDownload`, has a body consisting entirely of a `throw`, and a unit test
walks the compiled IL of **every method in the shipped assembly** asserting that no call to a
`Download` member on a `Siemens.Engineering.Download` type exists anywhere in it. That test was
negative-tested by temporarily retargeting it at `ICompilable.Compile` and confirming it *fails*,
naming `OpennessGateway.RunCompile` — a scanner that has never been shown to detect anything is not
a check.

### Granularity is DEVICE-LEVEL. There is no per-block download.

The single most important thing this command exists to say. `DownloadProvider.Download` has three
overloads and every one of them takes a connection, a pair of configuration callbacks and a
`DownloadOptions` value. **None takes a block, a group, a selection or an exclusion.** So "download
my one new DB" is not something this API does — the smallest real unit is the whole PLC software.

`SoftwareOnlyChanges` does **not** mean "the blocks you edited". It means the parts TIA finds
different from what is in the controller, decided by TIA at download time, neither chosen by nor
visible to this tool beforehand. Because that value is the one most easily misread as a per-block
selector, the **default is `Software`** — the unambiguous whole-software value. The misconception has
to be typed in; it is not inherited by leaving a flag off. Siemens's own enum also has `None`, which
transfers nothing; this command refuses it, since a confident plan for a transfer of nothing is the
most dangerous output it could produce — it reads as reassurance.

The granularity statement prints on **every** run, in table and JSON alike, for every option value.
It is not conditional, because the belief it corrects is what a reader arrives with rather than
something a particular flag triggers. In JSON it is a *field* (`granularity`, alongside
`canDownload: false`), not only prose: a consumer keying on a missing property could not tell "this
build refuses" from "this build predates the flag".

### Where the `DownloadProvider` comes from

`Siemens.Engineering.Download.DownloadProvider` has exactly one constructor and it is `internal`, and
the type implements `IEngineeringService`. `IEngineeringServiceProvider.GetService<T>()` is
constrained `where T : class, IEngineeringService`. Those two facts together mean
**`GetService<DownloadProvider>()` is the only way a client can ever hold one** — it is not returned
by any method, and it cannot be constructed. (Confirmed by reflecting on the installed V20 assembly;
`RHDownloadProvider`, for redundant systems, is the same shape.)

What the API does *not* state is which object answers. So this walks **outward from the device item
carrying the `PlcSoftware`**, asking each ancestor, and reports every object it asked with what each
one advertises via `GetServiceInfos()` — the same shape `CompileHmiTarget` uses, which exists
precisely because the analogous assumption for `ICompilable` ("it will be on the software-bearing
item") was measured wrong and returned null. The objects that *refused* are reported too: "the CPU
answered and the station did not" is the finding, and a report showing only the winner would have to
be re-derived by hand next time.

If no provider is obtainable anywhere in the tree, the command exits **16**, not 0. The report then
answers none of the questions it was asked, and a green exit would present that silence as a clean
bill of health (FI-44 — empty is not clean).

### The connection is readable WITHOUT connecting

`DownloadProvider.Configuration` is a `Siemens.Engineering.Connection.ConnectionConfiguration` — an
ordinary project-model object, walked with plain property reads:

```
ConnectionConfiguration  (IsConfigured, EnableLegacyCommunication)
  └── Modes            ConfigurationMode          (Name)
       └── PcInterfaces   ConfigurationPcInterface  (Name, Number, Addresses, Subnets)
            └── TargetInterfaces  ConfigurationTargetInterface  (Name, Addresses)
                 └── Addresses    ConfigurationAddress  (Name, Address)   ← the device-side address
```

**No socket is opened to produce any of it.** The one member on that tree that *would* touch the
network — `ConfigurationPcInterface.GetAccessibleDevices()`, "Delivers a list of accessible devices",
a live scan — is never called anywhere in this codebase. Neither is
`ConnectionConfiguration.ApplyConfiguration(...)`, which mutates the project's connection
configuration: a plan does not get to change what it is planning.

So the report describes **the route the project has configured, and says nothing about whether
anything is at the far end of it.** A configured address that answers and one that does not look
identical here, and mistaking the first for the second is the only wrong conclusion this section can
produce — which is why it leads with `READ FROM THE PROJECT, NOT FROM THE NETWORK` rather than
closing with it.

### Safety content is refused at the plan (exit 6)

Not the export path's refusal copied across. Nobody names a safety block here, and the plan is
refused anyway — because granularity is device-level, so on an F-capable PLC **there is no download
that excludes the safety program**. There is no option, overload or flag for it. Planning a download
of such a device *is* planning to write safety content (hard rule 2), and the refusal has to happen
while it is still a plan.

### What it also reports

Blocks the project flags `IsConsistent=false` are listed as **unverified content a download would
carry**, with a pointer to `compile-all`. This command gates nothing — it is a plan, not a gate — but
a report that omitted them would be describing the transfer of content no compile has ever examined
(hard rule 4, FI-52).

### What it deliberately does NOT do

- It does **not** ask whether a download would be *permitted*. That is the write fence's question
  (`src/device-guard/`, ADR-0009) and this command never asks it — see the note below.
- It does **not** answer the download's configuration callbacks. `docs/notes/openness-api-survey-plc-online.md` §4
  records the rule for whenever a real `download` is built: an unanswered configuration that would
  prevent the download throws `EngineeringTargetInvocationException`, `DataBlockReinitialization` is
  the destructive one, and the command must **fail closed** — refuse a download that raised a
  configuration it was not explicitly told how to answer, rather than choosing a default.

### The fence is not wired in, and cannot be as things stand

`DeviceWriteGuard` (ADR-0009) is the seven-gate write fence, and it lives in `src/device-guard/`,
which targets **net8.0**. `openness-cli` must target **net48** — `Siemens.Engineering.dll` calls a
.NET-Framework-only `Assembly.Load` overload and fails at runtime on modern .NET. So openness-cli
**cannot reference `DeviceGuard` as it stands**, and no version of this command could have called it.
That is a reason this command plans rather than gates, not an omission from it: a plan needs no
authorization, and adding a fence call that could not compile would have been the worst of the
options. Resolving it is design work recorded with the branch, not something this command decides.

### The download runs a compile that none of ours ran, and its exception does not name the block

Measured 2026-08-13: `compile --block`, the device compile, `compile-all --force` and `sanity-check`
**all reported clean** while a device download failed. TIA's own Info → Compile tab said:

```
An internal consistency error has occurred. Please compile the program in this CPU again.
The following blocks could not be compiled: FC_ModbusTCP_Sample [FC8];
```

The Openness exception for that same failure carries, in total:

```
Siemens.Engineering.EngineeringTargetInvocationException
  Error when calling method 'Download' of type '...DownloadProvider'.
  An error has occured during download: 'Software compiling completed with error.'
  openness detail messages (1): (the same sentence again)
```

**The block name is not in the exception.** `download-probe` already walks the detail-message list
and prints it verbatim, so this is not a reporting gap — the API genuinely does not carry it. There
is also **no `DownloadResult` on the throw path**, so there is no result object to interrogate.

Nor is it on disk. Checked: the project's own `Logs/` folder (empty), every
`%APPDATA%\Siemens\Automation\Portal V20\OnlineService\*.log` (all **0 bytes**), the Openness
telemetry under `%PROGRAMDATA%\Siemens\Automation\TelemetryConnector` (logs API **call names** only —
`objectName`/`methodName`, no results), and TIA's ETW diagnostic trace
`%PROGRAMDATA%\Siemens\ETWEventCollector\TIA_ADiag_*.etl` (8 MB of UTF-16 internal engineering
events; it contains the block's *name* from the search indexer, and **zero** occurrences of
`successfully compiled`, `Compiling finished`, `could not be compiled`, `errors:` or `warnings:`).

**So the route to the block-level detail is not to decode the failure — it is to run the compile
that finds it before the download does.** The software/station scopes report per-block messages
(`FC_ModbusTCP_Sample (FC8): Block was successfully compiled.`), which is precisely the granularity
the exception lacks.

### 🔴 `download-probe`'s fence is an ALLOWLIST, not a file-name suffix (2026-08-13)

**The guard used to accept any project whose file name ended `" scratch.ap20"`. It was wrong in both
directions**, and neither half was fixable by renaming anything:

* **It blocked the legitimate case.** `GenProject1.ap20` (the S6 sandbox, deliberately not renamed —
  CLAUDE.md "Codename note") and `SampleProject.ap20` could never be the target of a download,
  however deliberately somebody chose one.
* **It did not stop the dangerous case.** A file-name suffix is a *convention*, and anything can be
  renamed into one. This machine carries about **nineteen real production `.ap20` projects** beside the
  scratch ones; a fence any of them could satisfy by rename was not protecting them.

*The suffix was not arbitrary* — the scratch **copies of live jobs** on this machine are named that
way, so it was fitted to the projects the tool was being pointed at, and it deliberately avoided
naming any of their paths in the repository. That constraint is real and the replacement still
honours it (below).

**The replacement is ADR-0011's pattern**, because its properties were argued for and measured there:
allowlist never denylist; entries absolute or **`repo:`-prefixed** so a committed file is portable
across worktrees; the check runs **before anything else**, so Portal is never contacted on a refusal;
junctions **detected and refused**, not half-resolved; `..`, casing and 8.3 short names cannot walk
around it; **and no override — no flag, no environment variable, no argument names a different
allowlist.** Refusal is **exit 3 (`RefusedByPath`)**, unchanged.

**Two allowlist files, both fixed paths, both read:**

| file | for |
|---|---|
| `tools/download-probe.allowlist` | projects whose paths may be committed. Tracked, reviewable in `git log`. |
| `%ProgramData%\Ladder-AI\download-probe.allowlist` | a project whose **path may not be committed** — a copy of a live engineering job (CLAUDE.md "Live runs": use anything, commit nothing). |

**Why the second one lives OUTSIDE the working tree rather than being a gitignored companion.** A
gitignored file is protected by a *pattern*, and a pattern protects the file somebody thought of — on
2026-08-13 an agent's `.claude/settings.local.json.bak` sat untracked but **unignored**, one
`git add -A` from committing a site path. A path outside the tree cannot be published by an
ignore-rule gap, a `git add -f`, or any tool that walks the repo. It is the same file with the same
rules, in the one place where a mistake cannot leak it.

**Every "empty is not clean" case is a refusal, never a pass:** no allowlist file, an allowlist of
comments only, a malformed entry, an unresolvable path, a junction, a project that is a directory, a
file that is not `.apNN`. And a **permitted** run prints which entry vouched for it, into the log —
a fence that only speaks when it refuses leaves a successful run unable to say what permitted it.

**Proved the way ADR-0011's fence is proved, not by inspection:** the session delegate — the probe's
only route to Portal — is a stub that **appends to a sentinel file**, and the refusal test asserts the
sentinel does not exist *and* that no log file was created. The instrument is controlled: a permitted
case asserts the sentinel **is** written. Disconnecting the fence turns **4 tests red**, the sentinel
test among them.

### `download-probe --json` carries the LOAD MANIFEST as data (2026-08-13)

**The manifest is the only positive evidence that a download carried anything** — `DownloadResult.State`
was `Success` on a live run that transferred nothing — so it is what the harness's `Loaded` verdict
keys on, and the most load-bearing value this binary produces. It was reaching its consumer **by
scraping**: the deployment gateway is a separate process (net8.0; it must never link
`Siemens.Engineering` or every harness build joins the `(Path, FileHash)` approval cycle), so it
re-parsed the *rendered log* embedded in the JSON, and anything the renderer dropped was invisible to
it.

`download-probe` now references **`Ladder.Download`** (netstandard2.0, no Siemens reference, no
network, no Portal) and calls `DownloadResultAdapter` on the live message tree — **this is the live
path that library's own documentation names, and until now nothing used it.** The result is emitted
under `--json` as `loadManifest`, whose keys are `DownloadFeedback`'s own property names, camel-cased:
`loadedObjects`, `loadedObjectCount`, `duplicateLoadedObjects`, `nonObjectLoadSubjects`,
`transferredItemCount`, `verdict`, `verdictReason`, `runStateDisclosed`, `runStateTransitions`,
`finalRunStateEvent`, `upToDateSignalPresent`, `unrecognisedMessages`, `anomalies`.

* **`source` says which authority produced it** — `DownloadResultAdapter` is first-hand, off the live
  objects; `ProbeLogReader` re-derives from a rendering and is forensics only. A consumer that cannot
  tell them apart cannot tell a first-hand answer from a second-hand one.
* **`available: false` with every list and count `null`** when no `DownloadResult` existed (an abort, a
  throw, a `--to-folder` run). *Empty is not clean:* an empty array would say "TIA loaded nothing",
  which is the opposite of "nobody looked". Mutating those nulls to empty lists turns a test red.
* **Additive.** The embedded `log` array and the `transferVerdict` / `softwareLoaded` / `logFile` keys
  the gateway reads today are untouched, so nothing breaks on the day this lands.
* **Tested against recorded live-rig downloads**, not fixtures authored by reading our own output: the
  99-object full load, the 1-object differential, the up-to-date run and the aborted run, read from
  `src/download-feedback/DownloadFeedback.Tests/Fixtures/`. A missing fixture is a failure, never a
  skip. Removing the `loadManifest` key turns **7 tests red**.

### 🔴 TWO IMPLEMENTATIONS OF THE TRANSFER RULE — measured, then collapsed to one (2026-08-14)

`download-probe` grew its own transfer classifier (`TransferVerdicts`, commit `79f1596`) and
`Ladder.Download`'s `DownloadFeedbackParser` landed **later the same day** (`ccaae5a`) written to
replace it — its remarks name the two things it excludes by construction, and **both were live in the
probe's copy**. The duplicate was simply left behind when `download-probe` gained its reference to the
library for `DownloadResultAdapter`.

**Established before fixing, because "they will diverge" is a prediction and not a measurement:**

| | |
|---|---|
| **Seven recorded downloads** (six `download-feedback` fixtures + the live deployment's stdout) | **AGREE — every one.** |
| **Two constructed shapes the corpus does not contain** | **DISAGREE, in opposite directions.** |

*** THE AGREEMENT WAS A PROPERTY OF THE CORPUS, NOT OF THE RULES. *** No recorded run has a result
that carries messages while naming nothing loaded — and that is exactly where they part:

* **A result naming nothing loaded.** The probe answered **"YES — THE SOFTWARE WAS LOADED"**, inferred
  from the *absence* of an up-to-date phrase. The library answers `Undetermined` from the manifest.
  This is the dangerous direction, and it is the precise defect `DownloadFeedbackParser` exists to
  make impossible.
* **A reworded up-to-date sentence.** The probe matched three *loose substrings* and concluded
  `NothingTransferred`; the library declines to conclude **and reports the message as unrecognised**,
  which is how that parser is designed to go out of date rather than silently wrong.

**The verdict is now `Ladder.Download`'s and is not recomputed here.** `TransferVerdicts.FromFeedback`
maps its three-valued verdict 1:1 and total, so this type cannot hold an opinion the library does not;
what stays is the **presentation** the library does not produce — the headline and the verbatim
evidence block for the log a person reads. `ProbeSession` builds the feedback **once** and uses it for
both the verdict and the manifest, so they can never be two readings of one download.

**`TransferVerdictParityTests` is kept after the answer**, because it is what proves the derivation did
not quietly reintroduce a second opinion: reintroducing the absence-inference turns **4 red**.

> 🔴 **Two existing tests asserted the old rule, and one of them asserted the defect.**
> `ASuccessWithoutUpToDate_IsReportedAsTRANSFERRED` *required* `SoftwareLoaded = true` for a result
> naming nothing loaded. **While it stood, the correct behaviour was a failing build.** It is renamed
> and inverted rather than deleted, so the change of mind is visible in the file. The other encoded
> the wide-substring matching; its *argument* ("a false 'transferred' costs a wrong conclusion") is
> preserved exactly where it matters — an unrecognised wording can never now produce a false
> "transferred", it produces the conservative `Undetermined` **and** surfaces the message.

### 🔴 A FOLDER RUN REPORTED THAT THE SOFTWARE WAS LOADED (found and fixed 2026-08-13)

**Found by the first live rehearsal of the deployment path** — `GenProject1`, `--to-folder`, no wire,
no CPU stop. The premise in the code was wrong:

> *"an abort, a throw **and a folder run** produce no `DownloadResult` at all."*

*** A FOLDER RUN PRODUCES ONE, AND ITS MESSAGE TREE NAMES OBJECTS AS LOADED. *** On the rehearsal:
**27 objects**, 3 non-object items, exit 0. So the report contradicted itself three ways in one
document:

| field | said |
|---|---|
| `transferVerdict` | `Undetermined` — correct, but only because of a post-hoc override |
| `loadManifest.verdict` | **`Transferred`** |
| top-level `verdict` | **"YES — THE SOFTWARE WAS LOADED"** |

The middle two are exactly a gateway's `Loaded` conditions. **A consumer handed that report computes
`Loaded = true` for a run that contacted no controller.**

**Two separate faults, and both are fixed at the source:**

1. **The verdict was decided by reading message text.** `TransferVerdicts.Classify` searches for
   up-to-date phrases and concludes "loaded" from their absence — it **cannot** tell a folder run from
   a device run, because the two produce the same words. Whether anything reached a controller is a
   property of **which overload was called**, so it is now decided from the destination
   (`ProbeSession.ClassifyTransfer`) and the message tree is reported as what it is.
2. **The old correction was applied too late.** `DownloadToFolder` reassigned `Transfer` *after*
   `ReportResult` had already rendered the verdict **sentence** from the un-overridden value — which is
   why the object and the sentence beside it disagreed. **A post-hoc correction only fixes the copy it
   reaches.** The override is gone; the destination is passed *in*, and the log section, the verdict
   string and the JSON are all rendered from one verdict.

**`ReportResult` and `ClassifyTransfer` take the destination as a REQUIRED parameter.** A default
would be a guess about whether a controller was contacted, and a caller that forgets must fail to
compile rather than fall back to "controller".

**What a folder run now reports:** `available: false` (there is no manifest of what reached a
*controller*), `target: "folder"`, `describesDeviceTransfer: false`, `verdict: Undetermined`, every
device-facing field `null` — and **`resultPresent: true`**, which is what keeps a folder run
distinguishable from an abort now that both are `available: false`. The image facts are **kept in
full** under `image` (`objects`, `objectCount`, `folder`, and `parserVerdict`, labelled as the
parser's word about the *image*). Discarding them would trade one dishonesty for another.

> **Why not `available: true` with the device fields nulled?** More honest-looking, and **measured to
> be worse**: the existing consumer treats a missing `loadedObjects` array as "no first-class
> manifest" and **falls back to scraping the embedded log** — which names the same 27 objects. The
> tidy shape would have re-created the false positive one layer down.

**🔴 And the guard for this existed and could not fire.** The consumer's `available is not true`
branch **names "a folder download" in its own message**, and was proved to work by a **hand-authored
`available: false` fixture** — a fixture asserting the very premise this run falsified. Written,
tested around, never executed. The regression tests therefore run on
`OpennessCli.Tests/Fixtures/folder-run-stdout-20260813.json`, **the recorded stdout of that real
folder run**: its message tree is the input, its conclusions are what is under test, and a control
test asserts the recording really does satisfy all three `Loaded` conditions — so "the new output is
honest" is measured against a recording that demonstrably was not.

Mutation-tested both routes: ignoring the destination in the classifier turns **2 red**; removing the
folder branch from the manifest builder turns **4 red**.

### `download-probe --to-folder` — the download's own compile, without the download

`DownloadProvider` has a second overload, `Download(DirectoryInfo, DownloadConfigurationDelegate)`,
which writes hardware and software to a **folder**. No connection, nothing on the wire, no CPU
stopped, no rig involved. Measured to raise the **same `ConsistentBlocksDownload` configuration** the
device download raises, and to return the same shape of `DownloadResult` — so it reaches the
download's own compile path while touching no controller.

**CORRECTION, same day.** The paragraph that stood here said `--to-folder` was *"not a reproducer"*.
That was written from a healthy project, where the folder run and a device download both succeeded —
and it generalised from an absence. On a **genuinely broken** program (controlled reproducer below)
the folder download fails **identically to the device download**:

```
==== *** FOLDER DOWNLOAD THREW *** ====
Siemens.Engineering.EngineeringTargetInvocationException
  An error has occured during download: 'Software compiling completed with error.'
  openness detail messages (1): (the same sentence again)
CONFIGURATIONS RAISED: (none)
```

Same exception, same text, **zero configurations raised** — which is exactly what every original
failing run (`bn-07/33/41/42/54`) recorded. So `--to-folder` **is** a faithful stand-in for the
download's compile gate, and it is the only one that needs no controller, no network and no CPU.

### 🔴 THE 1.7 THROWS KILL THE PORTAL PROCESS — and four reporting defects hid it (2026-08-13)

The owner observed at the machine, and the rig lane measured **by PID**: **both throws kill the Portal
process the download was attached to.** PRE killed 16972 of `11228 16972`; POST killed 8256 of
`8256 13912`. *Why* Portal dies is not established and is not guessed at here.

**A count alone could not see it** — 2→1 reads as "an idle instance closed" unless you hold the PIDs.
**And our own tooling silently relaunches**, so every surface we had showed an ordinary run. Four
defects, all of them *reporting* failures, all now fixed and negative-tested:

1. **`launched-instances.json` is `[]` — and that is the design, not a bug.** `MarkLaunched` writes on
   launch, `Unmark` deletes on successful open, so the record is **erased at exactly the moment it
   becomes interesting to a reader**. The registry only ever tracks *empty orphans*, so
   `MarkedByThisTool` is structurally incapable of being true for a process in use.
   **Which direction does it fail in? SAFE.** The reuse guard is
   `!hasProject && IsMarkedAsLaunchedByThisTool(pid)`; an empty registry makes the second conjunct
   false, so an empty process is **never** reused and a fresh one is launched — safe, and wasteful.
   *Nothing consulted it in a way that fails open.* Fixed by a **second, report-only launch history**
   (`launched-history.json`) that is never unmarked and **consulted by no decision** — deliberately
   not by relaxing the guard, which would have edited a safety rule to satisfy a reporting need.
2. **`portal-status` reported `PROCESSES: 1` while the OS showed two.** It enumerated only
   `TiaPortal.GetProcesses()`, so it agreed with the API and disagreed with the machine, silently —
   and **a process Openness cannot see is precisely the case you reach for this command in.** It now
   cross-checks the OS process list and reports the difference as its own class, `OS-ONLY`. An
   OS-only process is never classified as "empty": its null project is an absence of information, not
   a fact.
3. **It dated PID 16972 `ACQUIRED 14:47:51` when that process started `15:38:09`** — fifty minutes
   before its own existence. `TiaPortalProcess.AcquisitionTime` is **not** the process's age and had
   been documented as one. The table now leads with `STARTED` (the OS start time, which is the age
   signal) and flags `ACQUIRED` with `! BEFORE START` when it contradicts the OS. What
   `AcquisitionTime` *does* mean is not established and is not guessed at. **A plausible wrong
   timestamp is worse than a missing one**, because it invites exactly the reasoning-from-sequence
   this project spent the day avoiding.
4. **The guard failed in the binary written to carry it.** A POST run was **armed**, raised **zero**
   POST configurations, **never invoked the delegate**, completed the download — **and exited 0**,
   where the contract says `12`. The cause was a **missing arm**: the completion path tested "did it
   fire" and had no branch for *armed and never fired*. **Can exit 12 fire today? On the folder path
   yes; on the device path it could not — it was unreachable code.** Now decided in one place,
   `ProbeVerdict.Decide`, which is a pure function precisely so a test can interrogate it without a
   rig — the decision previously lived inline in a method needing Portal, a device and a download, so
   the only assertable part was `Classify`, which was already correct. **And the finding it should
   have reported is real: the raised-configuration set depends on `--options`** — forcing
   `--options Software` made the download stop the CPU and the post delegate *was* then reached.

Every one of the four has a **"did not run" partner test**: an ordinary process must not be flagged
OS-only, an ordinary timestamp must not be flagged impossible, a PID we never launched must be false
in both records, and an unarmed run must never earn exit 12. That shape — *a guard whose non-firing
had nothing testing it* — is now the seventh of its kind found across the lanes.

### Experiment 1.7 — the delegate throw: `--throw-from-pre-delegate` / `--throw-from-post-delegate`

**The capability. Firing it at a device is a separate, owner-present act** (the working agreement puts
that on the STOP list; building the mechanism is not on it).

Openness hands the tool two callbacks during a download. It is undocumented what happens if one
**throws** — and that decides whether a callback can refuse a download by failing, which is what the
whole fail-closed posture assumes.

**Two flags, never one, and not combinable.** They are two experiments with very different blast
radii: PRE fires before anything transfers; POST fires *after*, and **`StartModules` is raised in the
POST delegate, after the download has already stopped the modules** — so a throw there can leave the
CPU stopped with no route to start it from inside that download. A single `--throw` firing from
whichever delegate came first would give an unattributable result. `--throw` is itself a usage error,
deliberately, because it is the flag someone would guess.

**Rehearsed on `--to-folder`, and the answer is already in — at zero risk.** The PRE delegate **is**
reached on the folder path. Measured:

```
injected from : PRE delegate, invocation #1
after answering: ConsistentBlocksDownload

*** WHAT OPENNESS DID WITH IT: REPLACEDBYANOTHERFAILURE ***
    Siemens.Engineering.NonRecoverableException came out and the injected exception is
    NOWHERE in its inner chain — the API replaced it rather than carrying it
```

with the surfaced message being, in full: *"Unexpected exception - no exception message available."*

So Openness **neither propagates nor swallows — it replaces.** Two consequences, and they pull in
opposite directions:

- ✅ **A callback CAN stop a download by throwing.** The download did not proceed. Fail-closed by
  failing works.
- 🔴 **The reason is destroyed.** The caller learns nothing about *why* — the same family as the
  download-compile finding above: the API discards the diagnostic and hands back a sentence that
  fits every failure there has ever been. **A tool relying on this must log its own reason before
  throwing**, because nothing downstream will carry it.

**The session survives.** Despite the name `NonRecoverableException`, a subsequent folder download on
the same Portal session completed normally (`state=Success, errors=0`) and `list` reported 84 blocks,
0 inconsistent. The exception is non-recoverable to the *call*, not to the session.

**POST cannot be rehearsed anywhere** — the folder overload is `Download(DirectoryInfo,
DownloadConfigurationDelegate)`, one delegate, the pre one. `--throw-from-post-delegate --to-folder`
is therefore **refused by name** rather than arming something that can never fire and exiting clean
having tested nothing.

**Exit codes:** `11` = the throw fired (a *deliberate* failure, its own code so no script or reader
can mistake it for a real one); `12` = armed and the delegate was never invoked, so nothing was
learned — emphatically not a pass. The injected exception is its own type
(`DeliberateProbeInjectionException`), which nothing in Siemens.Engineering can produce, and the log
says the failure was deliberate in the banner, at the point of the throw, and in the verdict.
Classification is by **object identity** against the thrown instance — never by reading a state or a
message string.

### The controlled reproducer (2026-08-13) — and what each scope said

`DB_Example` is a global DB that `FC_ModbusTCP_Sample` (FC8) reads. Deleting it creates the dangling
reference, which is the defect class FC8 was described as having. Measured, in order, on the scratch
project:

| check | verdict on the broken program |
|---|---|
| `compile` (**old** default, `--hardware`) | `STATE: Success  ERRORS: 0` — **hardware tree only, says nothing** |
| `compile --software` | `[Error] FC_ModbusTCP_Sample (FC8): Network 1: Tag "DB_Example".DataStore not defined.` |
| `compile --station` | same error, plus the hardware tree |
| `compile --block FC_ModbusTCP_Sample` | same error, and `CONSISTENT: NO` |
| `sanity-check` (**old**, device compile) | `ISSUES FOUND` — but its compile line still read `Success (errors=0, warnings=0)` |
| `compile-all --force` | `116 compiled, 1 with errors, 1 still inconsistent` |
| `download-probe --to-folder` | **threw** — `Software compiling completed with error.` |
| device download | **threw** — identical |

**The error text is better than TIA's own download message**: `Network 1: Tag "DB_Example".DataStore
not defined` names the network and the tag, where the GUI's Info → Compile tab says only *"The
following blocks could not be compiled: FC_ModbusTCP_Sample [FC8]"*.

**Recovery was complete through Openness alone** — no GUI, no rebuild-all. Re-importing `DB_Example`
and running **one** `compile --station` compiled the restored DB *and* FC8 in a single call (the
scope resolves dependency order itself, which per-block compiles cannot), after which
`sanity-check` returned `HEALTHY` on both lines and the download succeeded again.

**What this reproducer does NOT establish, stated plainly.** The original FC8 failure had
`compile --block FC_ModbusTCP_Sample` reporting **clean** with `CONSISTENT: yes`. Here the per-block
compile reports the error. So this reproduces *a* defect the hardware-only gate missed — which is
what the ruling rests on — but **not** the specific class where every per-item compile passes and
only the download's compile fails. That class remains unreproduced, and `--station` is not proven
against it. `--to-folder` is, by construction, because it *is* the download's compile.

