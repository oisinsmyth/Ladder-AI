# ADR-0013 — Remove the download project fence

- **Status:** **ACCEPTED — 2026-08-28**, on the project owner's explicit and repeated instruction,
  given four times across one session and finally as *"delete the guard, write the ADR."* The
  enacting edit has been applied: `ScratchProjectGuard` and `ScratchAllowlist` are deleted, both
  allowlist files are removed, and `download-probe` now refuses no project.
- **Date:** decided and enacted 2026-08-28.

## The decision in one line

**`download-probe` no longer restricts which project it may open and download.** Any TIA project on
this machine may be transferred to whatever controller that project's own hardware configuration
points at, by any agent, unattended, with no allowlist entry and no approval step.

- **Relates to:** **ADR-0009** (test-rig write access — *the gate is the TARGET, not the class of
  write*; this ADR does not change that principle, it records that **the mechanism enforcing it is
  gone**) · **ADR-0011** (which specified the allowlist-never-denylist pattern being removed here) ·
  `10-non-goals.md` **#3/#4** · CLAUDE.md **hard rule 5** (restore points — unchanged, and now the
  only surviving discipline on this path).

## What was removed

| | |
|---|---|
| `DownloadProbe/ScratchProjectGuard.cs` | the project allowlist, both files, `repo:`/`root:` resolution, the junction and 8.3 refusals, the "no allowlist = refuse" rule |
| `tools/download-probe.allowlist` | the committed allowlist |
| `Harness.Device/ScratchAllowlist.cs` | the deployment gateway's own copy of the same fence |

## 🔴 The blast radius, stated plainly because that is this document's job

**This machine carries roughly nineteen REAL PRODUCTION `.ap20` PROJECTS** in folders beside the
scratch ones — the fence's own header said so, and it was the reason the fence existed. Each is
configured with the address of a controller at a deployment site.

**A download is device-level.** It stops the CPU, transfers the whole program, and restarts it. On a
plant in service that is running equipment stopping — potentially with material in process — and
there is no undo: git does not restore a PLC, and the restore points this project keeps are exports
of *projects*, not of *devices*.

**Nothing downstream catches it.** This is the part that makes the removal load-bearing rather than
cosmetic, and it was measured, not assumed:

- **`download-probe` consults no device allowlist.** `src/device-guard/` exists and gates
  `rig-read`/`rig-write`; the probe never referenced it.
- **A target-scoped fence cannot be built in its place.**
  `ConnectionTargetSelection.ConfiguredAddresses` is **empty** on this project — the human downloads
  through TIA's *Extended download to device* dialog and that scan result never lands in the project
  model. Its own comment records this as *"the reason an address-keyed selection could not work
  here."* **So no code inside this binary can know which controller a download reaches.**

The consequence: **the project was the only thing identifying the site, and it is no longer checked.**
The remaining barrier is the operator — or the agent — naming the right path.

## Why it was decided anyway

The owner's reasoning, as given:

- The fence never restricted *him*. A download from TIA Portal by hand was always unfenced; the
  allowlist constrained tooling only.
- It was costing a round trip per deployment and a per-project config edit, on a machine where the
  rig is a purpose-built isolated test space he built for exactly this work.
- He is the controls engineer who owns these commercial relationships and is accepting the risk
  knowingly.

**The counter-argument is recorded rather than won:** the fence's value was never against a
deliberate act, it was against an *accidental* one — an agent resolving the wrong project. That
failure occurred in the same session this was decided: a lane was one step from importing a
37-register Modbus server over a resident 1024-register one, which would have put a virtual panel's
entire publish band past the end of its served area while the blocks stayed loaded and executing. It
was caught by a check that no longer exists in this path.

## What did NOT change

- **CLAUDE.md hard rule 5 stands: every device write needs a verified restore point captured first,
  and if it cannot be captured the write does not happen.** It is now the *only* discipline on this
  path, and it is a rule rather than a mechanism — nothing enforces it.
- **ADR-0009's principle stands.** Writes to a device in service remain *not permitted*. What changed
  is that this is now a statement of intent with nothing implementing it for downloads.
- **Hard rule 2 (safety) is untouched.**
- The device allowlist still gates `rig-read` / `rig-write`.

## Restoring it

`git revert` of the enacting commit restores the fence, its tests and the committed allowlist in one
step. The machine-local allowlist path (`%ProgramData%\Ladder-AI\download-probe.allowlist`) is
outside the repository and is unaffected by a revert; it was never created.

**The one thing a restore cannot give back** is the interval during which this was open. If a
download reaches a device in service while this ADR is in force, that is the event this document
exists to make attributable — and reverting afterwards does not make it not have happened.

## Disclosure kept in place of the refusal

The probe still **names the project and the resolved path it is about to download**, prominently, in
its log and its JSON. That is not a fence — it blocks nothing and prompts nobody — but it means a
transfer to an unintended target is *visible in the record afterwards*, which is the last property
worth keeping once the refusal is gone.
