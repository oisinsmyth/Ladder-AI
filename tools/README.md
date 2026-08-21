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
| **`-Arm` is fenced to an allowlisted scratch project** (`confirm-roundtrip.allowlist`, ADR-0011) | A mutating loop that can be pointed at the real project is a way around CLAUDE.md hard rule 5 that nobody decided to build — and it would be reached by a mistyped path, not by a decision |
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

### The scratch fence (ADR-0011, armed 2026-08-13)

The loop was **built dry-run-only**. ADR-0011 ruled it *adopted* — it is the only proof in this
toolchain with an authority independent of the converter in its loop — **and fenced in the same
breath**, because the two halves do not work apart.

`-Arm` refuses any project not listed in **`tools/confirm-roundtrip.allowlist`**:

- **An allowlist, never a denylist.** This engineering PC carries about **nineteen real site
  `.ap20` projects in folders beside the two scratch ones**; a denylist fails open on the one nobody
  thought to list.
- **The refusal is exit `4`, not a warning**, and it is evaluated **before any binary check, before
  any directory is created, and before stage 1** — so on a refusal Portal is never contacted.
- **The resolved canonical path is compared**, so casing, a relative path or a `..` cannot walk
  around it. Anything that cannot be positively resolved — a bare project name, the project *folder*
  instead of the `.apNN` file, a junction, an 8.3 short name, a missing or empty allowlist — is a
  **refusal, not a pass**.
- **No override.** No `-Force`, no environment variable; `-IsScratchProject` (the old caller
  assertion) is refused **by name**, the same shape as `download-plan` refusing `--yes`. Adding a
  project is an owner decision recorded in the allowlist file.

**`confirm-roundtrip-fence.tests.ps1` tests the fence, offline, in both directions** — 11 cases, and
it never contacts Portal. *"Portal was never contacted"* is **asserted, not assumed**: the tests hand
the script a **stub `openness-cli`** that appends to a sentinel file, and assert the sentinel does not
exist. That is a direct observation that the process was never launched, and it is sound because
`Start-Process $OpennessCliPath` is the script's **only** route to Portal. The instrument is itself
controlled — one permitted case asserts the sentinel *is* written, so its absence elsewhere means
something. Negative-tested by disabling the fence: **9 of 11 go red**, six of them reporting
*"openness-cli WAS launched - Portal would have been contacted."*

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\confirm-roundtrip-fence.tests.ps1
```

## `check-claude-md-budget.py` — the anti-regrowth gate (2026-08-21)

`CLAUDE.md` is injected into every dispatch, so its size is a tax on every request in the project.
It grew **45,130 → 96,245 bytes in eleven days, and not one commit in that span reduced it** — the
mechanism being that new findings land in `CLAUDE.md` because it is the file everyone reads. Prose
asking people to keep it small was in place for that entire span. This is the gate that replaces
the prose.

**Ceiling: 20,480 bytes (20 KiB)**, the value held throughout the context cut. Bytes are always
reported CRLF-equivalent: `core.autocrlf=true` here, so git stores the file LF and materialises it
CRLF, and `--staged` adds the newline count back rather than reporting the smaller blob figure.
Every size in the project's record is the worktree number, so without that the gate and the commit
log would disagree by 149 bytes.

Exit **0** within budget, **1** over budget, **2** the gate could not run (unreadable, or not
staged when `--staged` was asked for). **Exit 2 is not a pass** — same 1-vs-2 split as the
mechanical floor.

```
python tools/check-claude-md-budget.py [--staged]
```

It checks size only. It does **not** check whether a fact was dropped — that is
`check-claude-md-migration.py`, which should be run before any trim.

### Making it fail closed

The script alone is advisory, and this repo's own maxim is *a warning is not a gate*. `hooks/pre-commit`
runs it with `--staged` whenever `CLAUDE.md` is in the staged set and **refuses the commit** on
non-zero. Git will not install that for you — once per clone:

```
git config core.hooksPath hooks
```

The hook fails closed by design: if `python` is missing or the script is unreadable, the non-zero
exit refuses the commit rather than waving it through. `hooks/*` is pinned `eol=lf` in
`.gitattributes`, because with `core.autocrlf=true` a tracked hook would otherwise check out CRLF
and `sh` would fail on the shebang — silently disabling the gate. `--no-verify` bypasses it, which
is the behaviour the gate exists to prevent.

### It has been executed, in both directions

`check-claude-md-budget.tests.py` — 9 cases, offline, no framework, ~1 s. Each case copies the
script under test **unmodified** into a temporary tree beside a fixture `CLAUDE.md` of known size
and runs it as a child process; no testability flag was added to the script, since a gate with a
"point me at a different file" option can be aimed away from the file it guards. Both directions
are asserted, and so are the reason strings — refusing for the wrong reason is a different defect
from refusing correctly, and an exit code cannot tell them apart.

```
python tools/check-claude-md-budget.tests.py
```

Negative-tested by disabling the size comparison: **2 of 9 go red** — the two that assert refusal.
The other seven legitimately still pass, because they cover the permit direction and the
cannot-run paths, which a disabled comparison does not affect. Note the honest ratio rather than a
flattering one: it is a nine-case suite of which two are the refusal itself.

**End-to-end, the gate was observed refusing a real commit**, not merely exiting non-zero: a padded
20,993-byte `CLAUDE.md` was staged and `git commit` returned 1 with `COMMIT REFUSED`, leaving `HEAD`
unmoved. Deviation from convention worth noting: this suite is `.tests.py`, not `.tests.ps1`,
matching the language of the script under test.

## Benchmarks

`bench-machine.ps1` measures a machine on the axes that matter for this repo; `bench-compare.ps1`
diffs two result files. See the comment-based help in each.
