# Tools

AutoHotkey macros and one-off scripts. Each remaining AHK macro is a candidate for replacement as Openness coverage grows (docs/03-development-plan.md).

## Openness approval — removing the human from a rebuild (FI-74, 2026-08-10)

TIA approves an Openness caller by `(Path, FileHash)`, so every rebuild is a caller it has never seen
and needs a person at the machine to accept (FI-61). Accepting that dialog only writes a registry
entry, so these write it directly and no dialog is raised. Full record, the security trade, and the
open gaps: `docs/notes/openness-quirks.md`, *"Approving a rebuilt binary WITHOUT a human at the
machine"*.

| Script | Purpose |
|---|---|
| `openness-approve-setup.ps1` | **Run once, elevated, per machine.** Grants one account write on one whitelist key so the script below needs no elevation. Reversible with `-Revoke`; reads the DACL back and prints it rather than assuming the grant landed. |
| `openness-approve-build.ps1` | Approves a built exe. `-Status` (read-only) says whether a binary is approved *right now* — worth asking before blaming Portal for a hang. `-Prune` drops stale hashes, `-Forget` revokes a path entirely, `-WhatIf` shows the exact entry it would add. |
| `openness-approve-watch-dialog.ps1` | Fallback for a dialog already on screen. Win32, because UI Automation cannot see this dialog's buttons at all. **Click path untested against a live dialog** — run without `-Click` first. |

Builds of `src/openness-cli` self-approve through the `ApproveForOpenness` post-build target; opt out
with `-p:AutoApproveOpenness=false` or `LADDER_AUTO_APPROVE_OPENNESS=0`. It never fails the build.

**The setup is per-machine, not per-repo.** On a machine where it has not been run, FI-61's
"never rebuild unattended" rule still applies in full.

## `confirm-roundtrip.ps1` — the confirm loop (2026-08-12)

```
export ──► to-ir ──► to-xml ──► import ──► compile ──► export
   └──────────────── compare THESE TWO ───────────────────┘
```

The owner's invariance principle, run against one block. **Strictly stronger than
`converter drift-check`**, which never leaves the PC: this goes through TIA, so it also catches what
TIA does on import and compile — the class the `MemoryLayout` hole belonged to, where the DB
round-tripped *equal and still wrong* and every PC-side check stayed green.

The **judgement is not in this script**: `converter compare` carries it, in-process, because FI-24
holds the converter to being a pure file transformer. This script owns Portal, the filesystem and
the sequence, and nothing else.

**It mutates**, and that governs the design:

| Guard | Why |
|---|---|
| **Dry run by default**, stopping before the import and printing the exact commands it would run | The import writes, and is measurably not a no-op — the layout flip happens there |
| `-Arm` requires `-IsScratchProject`, an explicit caller assertion | Nothing here guesses at "scratch". A project name is not evidence and a wrong guess writes to a real project |
| `-Arm` requires `-Group`, copied verbatim from `openness-cli list` | A shortened guess fails with an error that does not point at the mismatch |
| The first export is `Copy-Item`d, never load-and-saved, and its md5 is re-checked after every stage | A comparison against a reformatted reference is worthless |
| Live-run content + a scratch dir inside the repo is refused | Live-run access is full; **retention** is what the boundary governs (docs/13) |
| `Start-Process` with file redirection, never a pipe | `openness-cli` launches Portal as a child inheriting stdout; a pipe outlives the command and hangs |

**It deliberately does NOT re-assert `block-layout --set Standard` after the import.** CLAUDE.md
requires that in ordinary work because a re-import reverts a block to Optimized. Here that revert is
the measurement, and repairing it would make the loop pass on the exact defect it exists to detect.

Exit codes keep **a stage failure and a content change apart** — "the import failed" and "the
content changed" are different findings: `0` invariant, `1` changed, `2` not compared, `3` a stage
failed (the loop did not complete and says nothing about invariance), `4` refused before anything
ran, `10` dry run (nothing was attempted, so nothing is proven).

`-FirstExport <path>` reuses an export already in hand, which makes the dry run **fully offline** —
no Portal contact at all.

## Benchmarks

`bench-machine.ps1` measures a machine on the axes that matter for this repo; `bench-compare.ps1`
diffs two result files. See the comment-based help in each.
