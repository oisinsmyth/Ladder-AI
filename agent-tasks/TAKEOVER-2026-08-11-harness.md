# TAKEOVER — test harness and device write access (queued 2026-08-11)

You are picking up work whose conversation has been deleted. Nothing about it is in your context.
This file plus the two documents it names is the whole handover — read all three before touching
anything.

## Read these first, in this order

1. **`docs/notes/2026-08-11-write-fence-and-harness-session.md`** — what was built, what moved in
   governance, the eight claims a verification pass refuted, and the lessons. Organised by
   **evidence level**, because the gap between "built and green" and "known to work" is the thing
   you are most likely to misread.
2. **`Live Runs/JOB9004/00-HANDOVER-2026-08-11-tooling.txt`** — the rig, the network hazard, the
   allowlist, and the marker DB spec. Job folder: read freely, commit nothing, and never carry its
   vocabulary into the repo.
3. **`docs/adr/adr-0009-test-rig-write-access.md`** — the governing decision for anything that writes
   to a device.

Then run `git status` and `git log --oneline -10`. Do not trust that the repo is where these
documents describe; other agents may have moved it.

## The one sentence that matters most

**190 tests pass and nothing in the chain has ever written to a PLC.** One read, of one value,
through the fence, is the entire sum of what has been proven against reality. Treat every green test
count accordingly.

## Immediate actions, in order

1. **Unbind the PLCSIM filter** (needs an elevated shell — likely the engineer's to run):
   `Disable-NetAdapterBinding -Name 'OpenVPN TAP-Windows6' -ComponentID 'plcsim_ndislwf'`
   Leave `Ethernet 3` bound — that is PLCSIM's own adapter and is meant to be. Until this is done,
   whether a VPN session survives is decided by which TAP adapter OpenVPN happens to grab.
2. **The marker DB** — the single blocking item for everything else. Spec is in the job handover
   §4. **Must be standard (non-optimized) block access**; classic S7comm cannot see optimized blocks
   at all, so getting this wrong makes the identity mechanism fail *silently*. Dispatch to
   `lad-coder` (hard rule 8, no exceptions) — or the engineer creates it by hand in TIA, which is
   ordinary engineering and outside rule 8's scope.
3. **Find out whether the job's DBs are optimized or standard access.** This decides whether the S7
   transport can read them at all, and whether an OPC UA licence is worth buying. Cheap, and it
   gates a spending decision.
4. **The engineer downloads the marker DB by hand.** The first download cannot be governed by the
   fence, because the fence needs the marker to verify identity. That gap is by design — do not
   weaken the fence to close it.
5. **Then** add the marker to the allowlist entry, set `writeEligible`, declare a write scope, and
   attempt the first real write. Nothing before this point has ever exercised the write path.

## Constraints that will silently break things

- **Standard block access** for anything the harness reads — see above.
- **Do not hold a git worktree while subagents are running.** The orchestrator's worktree state leaks
  into theirs and kills their Bash and PowerShell mid-run. Two agents lost shell access this way and
  only mentioned it in passing.
- **Do not relax the allowlist entry to order-code-only** to make identity pass. Every CPU 1214C
  returns the same order code; that verifies a *model*, not a *device*, and the whole hazard is two
  different devices at the same address.
- **Never probe a device address blind.** On this machine `10.10.10.10` is a standard PLC address
  across several site networks and resolves differently depending on which tunnel is up.
- **Read `ERRORS:`, not the exit code**, on any compile — a clean block exits 8 on the live job
  because of four pre-existing hardware warnings.
- **The claims dir must be the shared one.** A per-worktree claims dir is always empty, grants every
  claim, and turns the registry into a no-op that looks like success.
- **Hard rule 8**: all LAD/IR work goes through `lad-coder`, no exceptions for size. If the agent is
  unavailable, that is a blocker to report — not a licence to do it inline. (Its definition file is
  valid; when it was missing it was a registry-load problem.)

## Known-unfixed, carried forward

- **`VectorRunner.WaitScans` has a latent wrap bug.** It computes `ReadScanCounter() - start`; a
  UDInt counter wrapping at 2³² makes that hugely negative, and the wait spins to its poll limit and
  errors. Not a false pass, but baffling. `S7Transport` absorbs the symptom by returning a monotonic
  value — **the runner is the layer actually holding the assumption**, and that is where it should
  be fixed.
- **`CpuInfoIdentitySource` is written, tested and deliberately not wired in.** It would read the CPU
  serial via `GetCpuInfo`, which would be a better identifier than a marker DB. It does not work on
  this CPU — measured: `GetCpuInfo` returns "Invalid CPU answer" and SZL `0x001C` is refused at every
  index. Left in place in case a different CPU is ever targeted.
- Unbuilt, unblocked: a `write-check` verb for the `device-guard` CLI (only the read path exists),
  and a JSON loader for vectors (the model is the contract; serialization was deferred).

## Decisions waiting on the engineer

- **How Sharp7 should be referenced.** Deliberately not committed; it sits at
  `%USERPROFILE%\.ladder\lib\Sharp7.dll` behind an overridable path property, and if absent the
  adapter file is excluded and everything else still builds. Three options are laid out in
  `src/harness/Harness.S7/README.md`.
- **Where the real device allowlist should live.** Currently outside the repo. Outside keeps
  environment config out of version control; inside would answer ADR-0009's *"how did this address
  get on the write list"*.
- **Whether to buy an OPC UA licence.** Its advantage is not speed — the 100 ms floor suits almost
  every vector — but that it works with *optimized* block access, so DBs would not need converting
  to standard and losing per-tag retentivity granularity. Answer item 3 above first; if those DBs are
  already standard, the advantage largely evaporates. **Do not pursue PLCSIM Advanced** — it excludes
  the S7-1200 family at any price, in every version, and that is a product exclusion rather than a
  licensing one.

## Still owed from an earlier session

Six documents (`01-scope`, `02-roadmap`, `08-testing-strategy`, `09-risk-register`, `12-glossary`,
`16-future-ideas`) still treat "does PLCSIM Advanced support S7-1200?" as an open risk (R-07). It is
answered — **no, in every version** — and three of them gate stage S9's entry on resolving it. S9 is
separately waived for live projects (`docs/notes/stage-gates.md`), so this is tidying rather than a
blocker, but it is stale text that will mislead.

## How to finish

Per `agent-tasks/README.md`: fold outcomes back into the permanent docs and delete this file when the
queue it describes is closed. Do not let finished tasks accumulate here.
