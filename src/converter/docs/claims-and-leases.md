# converter — claims and leases

Reserving a shared resource before writing IR, and taking a real lock on Portal or the rig. `claim --allocate` is the enforcing half of `claim` and travels with it.

> Split out of `src/converter/README.md` on 2026-09-17. That file remains the index.

## `claim` / `claims` — reserve a shared resource before writing IR (2026-08-07, FI-65 component 1)

```
converter claim  --project <ir-dir> --claims <dir> --agent <id> --kind <k>
                 (--value <v> | --allocate [--type FB|FC|OB|DB] [--floor <n>] [--in <word|block>])
                 [--purpose <text>] [--json]
converter claims --project <ir-dir> --claims <dir> [--check] [--agent <id>] [--json]
converter claims --project <ir-dir> --claims <dir> --release --agent <id> (--kind <k> --value <v> | --all) [--force]
```

Lets several agents work one project without stepping on each other. The collisions this prevents are
decided **while writing IR** and never reach TIA: two agents each scan the corpus for the next free FB
number and both pick 51; both verify alarm bit `%X9` is free and both take it; both append "network 8"
to the same shared FC. Each agent's own `converter diff --only` invariance check passes — the conflict
exists only between them. See `docs/16-future-ideas.md` FI-65.

**Six kinds, two semantics.** *Allocation* (`block-number`, `alarm-bit`, `db-member`, `block-network`,
`tag`) reserves something not yet used, and is refused if the corpus already uses it. *Exclusive*
(`block-edit`) reserves write access to something that does exist, and is refused only if another agent
holds it.

**`--claims <dir>` is required** (or `LADDER_CLAIMS_DIR`) and there is deliberately **no default**. It
must be a directory shared by every agent on the project: agents run in separate git worktrees, so a
per-worktree claims directory is always empty, grants every claim, and turns the registry into a no-op
that looks like success — FI-44's "empty is not clean" in its purest form.

**Acquisition is atomic** — a claim is written to a temp file and `File.Move`d into its slot, so the
filesystem decides the winner and a claim file is never observed half-written or locked. One file per
claim, never a table: a shared table would be a read-modify-write race between processes and a merge
conflict per claim once committed. Claims are machine state and are never committed.

**`--allocate` takes the lowest free value atomically** and prints what it took. There is no read-only
"suggest" mode on purpose: a non-binding suggestion is the exact race the tool removes — two agents are
both told "FB51 is free", both act on it, and one finds out only after doing the work.

**`--check` gates on conflicts only.** A *fulfilled* allocation claim (the agent wrote the block, so the
resource now exists) is the normal end state and is reported without gating — a check that failed on
success would be ignored on failure. What gates: an exclusive claim on a block that no longer exists,
a malformed value, and the **cross-kind conflict the filesystem cannot see** — agent A holding
`block-edit FC_ControlMain` while agent B holds `block-network FC_ControlMain:8`. Those are two
different values, so both acquisitions legitimately succeed; only the check relates them.

Stale claims are reported, never auto-released — an agent can legitimately hold one across a long
stage, and expiring a claim out from under live work causes the collision the registry exists to stop.
Releasing another agent's claim needs `--force` and says that it was forced.

**Exit codes:** `0` acquired / clean · `1` refused, or `--check` found a conflict · `2` unusable input
(no `--claims`, missing project, unknown kind, empty corpus). The 1-vs-2 split is for the calling
agent: 1 is a real answer ("someone has it, pick another"), 2 means nothing was decided.

**Boundary:** claims prevent only the kinds someone enumerated. The six cover every collision source
this repo has evidence for; a resource class outside them is still an undetected collision.


## `converter lease` — a real lock on Portal and the rig (FI-65 component 3, 2026-08-22)

```
converter lease acquire --resource portal:<project>|rig:<address> --leases <dir> --holder <id> --pid <n> [--ttl <minutes>] [--purpose <text>] [--portal-evidence <file.json>] [--json]
converter lease release --resource portal:<project>|rig:<address> --leases <dir> --holder <id> [--json]
converter lease status  --leases <dir> [--json]
```

**A sibling of the claims registry, not an extension of it.** It reuses the atomic
write-temp-then-`File.Move(overwrite: false)` primitive and deliberately does **not** share the
semantics. A claim is held until released and stale ones are never auto-released — correct for a block
number, and fatal for a gate, because one crashed agent would wedge the rig forever. So a lease
expires. Keeping the two apart is the point: a later tidy-up that gave claims a TTL would silently
start dropping reservations.

What it replaces is a 3,365-line hand-edited text file whose own README calls it *"a cooperative
convention … not a real lock — it only works if every agent actually follows it"*, and which has been
raced: two entries carried the same generic holder name, were indistinguishable, and the collision was
*"caught by chance when re-reading git log."*

**Reclaim needs BOTH halves — the holder provably gone AND the lease expired.** A live holder past its
TTL is reported and never evicted: a long download is not a dead one, and evicting it would put two
writers on the gate. Identity is holder + pid + **process start time**, because pids are reused and a
bare pid check would eventually evict a live holder that inherited a dead one's number. A reclaim is
reported as `RECLAIMED`, distinct from `ACQUIRED`, because somebody's run died holding the gate and
smoothing that into an ordinary success destroys the only evidence it happened.

🔴 **`--pid` is required and is NOT this process.** Measured while building the verb: defaulting it to
the converter's own pid records a holder that is dead the instant the command returns, because a CLI
invocation is short-lived. Every later acquire then reads *"its process is gone"*, the liveness half of
the reclaim rule becomes dead code, and reclaim degenerates into the pure timer the store was written
to avoid — while still looking evidence-based. Pass the process that holds the gate for the lease's
lifetime. PowerShell `--pid $PID`; Git Bash **`--pid $(cat /proc/$$/winpid)`** and *not* `$$`, which
under MSYS is an emulated pid with no Windows process behind it.

**`--leases <dir>` is required and has no default** (or `LADDER_LEASES_DIR`), for the reason `--claims`
has none: agents work in separate worktrees, so a per-worktree store is always empty, grants every
lease, and looks exactly like success. The shared root is `C:\ProgramData\Ladder-AI\leases`. The store
echoes a `leases=` line on every act — read that, not the argument. Unlike `ClaimStore` it appends no
project segment, so the doubled-root refusal has nothing to act on here.

### The Portal gate — evidence, and every branch fails closed

A `portal:` lease **refuses without `--portal-evidence <file.json>`**, the output of `openness-cli
portal-status --json`.

The converter cannot answer the question itself. Which project a Portal process has open comes only
from `TiaPortalProcess.ProjectPath` via `TiaPortal.GetProcesses()` — net48, `Siemens.Engineering.dll`.
There is no file, registry key, command line or window title that carries it; `LaunchedInstanceRegistry`
holds **PIDs only**, and its live half is *erased at the moment a project opens*, which is the moment it
would become interesting. So the tool that can compute the fact produces it and the tool that needs it
consumes it — the same producer/consumer shape as `wave-cli submit --reachable-state`, and **absence is
a refusal, never a pass**.

FI-65's objection 11 is why: *"An engineer can open the project in Portal by hand at any moment (and
does). A queue that does not include them is advisory, and an advisory lock over a resource another
actor can take is a lie that eventually gets believed."* There is no TTL on a person and no queue they
are standing in, so a human holder is **refused by PID**, never scheduled.

| the evidence says | verdict |
|---|---|
| a process holds the target and `launchedByThisTool == false` | `HeldOutsideTheTool` — refused, naming the PID |
| **any** process has `opennessVisible == false` | `EvidenceInconclusive` — refused |
| the file is older than 2 minutes | `Invalid` — refused, naming the age |
| unreadable, empty, or carrying no `processes` array | `Invalid` — refused |
| every process visible, none holding the target | proceed |

🔴 **The OS-ONLY row is the one that matters.** A Portal that Openness cannot see reports
`projectPath: null` — character for character what a Portal with nothing open reports. Only
`opennessVisible` separates them, so a gate reading the path alone **fails open on exactly the process
it knows least about**, and `portal-status` has measured that case: it reported one process while the OS
showed two. `EvidenceInconclusive` is kept apart from `HeldOutsideTheTool` because "somebody has it" and
"I could not tell" send a reader to different places.

The gate runs **before** the store is touched, so a refused acquire leaves nothing behind — otherwise a
person holding the project would also acquire a phantom lease in our own store.

A `rig:` lease takes no evidence **and says so on every acquire**. Nothing observes a rig in use:
`MB_SERVER` accepts one connection and a second is discovered *by failure* — a viewer attached during a
wave once cost it 0 of 22 vectors — and `download-probe` writes a log, not a lock. The rig lease
coordinates agents and must not be read as more.

**Exit codes:** `0` acquired / reclaimed / released · `1` refused (`HeldByAnother`,
`HeldOutsideTheTool`, `EvidenceInconclusive`) · `2` unusable input (no `--leases`, no `--pid`, an
unparseable `--resource`, a pid that is not running, missing or stale evidence). Same 1-vs-2 split as
`claim`: 1 is a real answer about the world, 2 means nothing was decided.

### FI-24, narrowed in one place rather than eroded

The invariant — *"the converter is a pure in-process file transformer and never shells out or touches
the environment"* — is asserted in prose in five files and was, until now, enforced by nothing. It had
already been dented unremarked: lease liveness reads a pid's start time, the converter's first and only
contact with `System.Diagnostics.Process`.

**The narrowing: the converter starts no child process and mutates no environment. Reading the start
time of a pid it was handed is permitted**, because lease liveness cannot be decided without it and the
alternative — the caller asserting "the holder is alive" — is a claim nobody can check, made by the
party with the motive to get it wrong.

`ConverterProcessInvariantTests` pins it with the same IL walk `JoinSiteWalkTests` uses: no
`Process.Start` anywhere in the assembly, `GetProcessById`/`StartTime` the only permitted contact, a
positive control proving the walk sees what it is aimed at, and a denominator proving it examined the
assembly. Negative-tested — a planted `Process.Start` reddens it by name.

### What is deliberately absent

**`--wait`.** The design lists it, and it is wrong for `portal:` outright: objection 11 says you cannot
queue behind a person. For `rig:` it is defensible but unneeded until two agents actually contend. If it
is added, copy `WaveControl/WaveStore.cs`'s exclusive lease, which **reports what the wait cost** — *"a
lock that works silently would let a serialised run be reported as a parallel one."*

**A daemon.** Objection 5: *"a long-lived daemon is a stale-process factory with a heartbeat."*

### How it is proven

Two tests, covering different things, neither substituting for the other.

`LeaseProcessRaceTests` runs the **real CLI as concurrent OS processes** — the first process-spawning
test in this suite — and asserts exactly one winner per round, every refusal naming that same winner,
one lease file left behind, and a negative control where four processes take four *different* resources
and all succeed (a fence that refuses everything passes every test that only checks for refusals). It
also asserts the racers actually **overlapped in time**, because a race that ran in sequence would prove
nothing.

🔴 **That test is blind to atomicity, and was measured being blind.** Against a deliberately broken
store whose acquire was `File.Exists` then write, it passed — twice, over 96 contended launches. The
window between the check and the write is tens of microseconds while process start-up jitter is
milliseconds wide, so the racers never land inside it.

`LeaseStoreTests.Thirty_two_callers_released_TOGETHER_produce_exactly_one_holder` is what covers that:
32 dedicated threads on a `Barrier`, released at one instant. Against the same broken store, **23 of 32
callers threw `UnauthorizedAccessException`** colliding on the destination. Dedicated threads rather
than `Parallel.For` — the pool ramps a few workers at a time, so a barrier of 32 waited 20 seconds for
the very simultaneity the test exists to create.

## 🔴 `claim` — X-J's enforcing half: `--allocate` now knows the reserved band (2026-08-14)

X-J's treatment reads *"a NUMBER RANGE IS RESERVED for harness-generated objects **and the claim tool
refuses allocations inside it**"*. The range existed; **`--allocate` had no knowledge of it and would
hand out 9000–9999 to a deliverable without comment.** This is the missing half.

**The band is READ, never restated.** `Converter.csproj` references `WaveControl` (netstandard2.0,
references nothing) so `HarnessNumberRange.Declared()` — the one place the ruling names, overturnable
at the one line the ruling says — is the only declaration. *Two declarations of one band is how they
diverge*, and this repo had grown a second the previous day: `HarnessScope` carried its own
`9000`/`9999` constants, correct at the time, which would have gone on agreeing with the ruling right
up until somebody overturned it in the place the ruling names and not in the copy. Deleted.

### What is enforced — needing no judgement about who is asking

| | behaviour |
|---|---|
| **plain `--allocate`** | **cannot** return a band number — the band is **removed** from the candidate set, not deprioritised. A floor just below the band **steps over** it and lands on the first number past it. |
| **`--allocate --floor 9000`** (fed by `HarnessNumberRange.AllocationFloor`) | the search is **confined to the band** and stops at its last number. |
| **band exhausted** | `BandExhausted` — its own result, naming the band and its declarer. **Not** `HeldByAnother`, because that invites a retry at a higher floor, and *** a higher floor is exactly what must not happen: it walks out of the reserved range and hands a harness object a deliverable number while every downstream check stays green *** — the measured `FC 910` collision by a different road. |
| **OB** | **excluded structurally**, via the declaration's own `CoversSpace`: no skip, no confinement, no exhaustion rule, no annotation. An OB's number is fixed by its event class and the spec names `OB80` as a harness object; a band applied to OBs emits a false finding on a correct project. |

The skip is written as an **explicit walk**, not an arithmetic shift: a shift has to leave a floor
*below* the band alone until the walk reaches it and leave a floor *above* the band alone entirely,
and `n < First ? n : n + Capacity` displaces the second case into numbers nobody asked for.

### What is NOT enforced, and why a flag would have been worse than the gap

An explicit `--value` inside the band is **accepted and announced**, not refused:

- **Nothing at claim time can derive harness-ness.** A block-number claim is an **allocation** — the
  block does not exist yet, that being the definition, and `ClaimValidator` refuses the claim outright
  if it does. So `HarnessScope`'s number-derived classification has nothing to read. A `--harness`
  switch would close the gap in appearance only: *a caller assertion is forgotten exactly when it
  matters.*
- **Refusing it would break X-J's own interoperation point.** `HarnessNumberRange.ClaimArgumentsFor`
  renders `claim … --value FC9001`, so the harness ledger could no longer record its own allocations —
  the collision the band exists to prevent, reintroduced by the fence.

What is available instead is **attribution**: the outcome says the claim is in-band, quotes the
declaration, and says it was *accepted, not verified*.

### ⚠️ The obvious closure is a tautology — written down so it is not added back

*"Once the block exists, classify it and check the claim against it"* **cannot work**: `HarnessScope`
decides harness-ness **by reading this same band**, so the comparison reduces to `band(n) == band(n)`
and could not fire on any input, in either direction. *** A check that shares its subject's blind spot
is not a check ***, and a guard that is correct, wired in and unfalsifiable in place is one of this
project's named failure modes. Closing it needs an authority that classifies a block by something
**other than its number** — the harness ledger's own record of what it generated would be one — and no
such authority is reachable from the converter today.

### Testing

19 tests, **every refusal paired with a legitimate case that must still succeed** — above all a
harness object taking 9000, and an explicit `--value FC9000`. *A fence that refuses everything passes
every test that only checks refusals.* Mutation-tested four ways: band not skipped (2 red), **band
allocation unconfined so it escapes past 9999** (2 red), band unreachable (6 red), OB carve-out
removed (2 red).

*** TWO OF THOSE TESTS EXIST BECAUSE MUTATION FOUND THEM MISSING. *** The first draft's
"plain allocate never returns a band number" asserted a property of the number *returned*, and on a
small corpus a plain allocate takes `FC1` and never goes near 9000 — so it held with the fence
removed. Worse, **the confinement mutation left the entire suite green**: the exhaustion test used
floor 9000, where a 512-wide walk ends at 9511 and never leaves the band. Both are now asserted over
the **candidate set**, from a floor high enough that the walk must cross or stop.

