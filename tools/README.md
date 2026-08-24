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

## `check-doc-migration.py` — prove a doc cut dropped nothing (2026-08-21)

Every time content is moved out of a file that grew too big, the same question follows: did
anything fall on the floor? This answers it for any `(baseline, file, destinations)`.

```
python tools/check-doc-migration.py <baseline-ref> <file> <dest> [<dest> ...]
                                    [--fenced-section "## Heading"] [--allow <fragments-file>]
```

Markers are backticked identifiers (4–60 chars) and long `--flags`. **Prose is deliberately not
checked** — on a cut whose purpose is removing narration, holding the narration as a requirement
would fail the gate by design. That is a real limit, and it means a fact stated only in prose is
outside the gate's reach; say so when you rely on it rather than implying coverage it lacks.

Exit **0** nothing dropped, **1** at least one marker unaccounted for, **2** the gate could not run
(bad ref, unreadable file, absent section, **a destination path that does not exist**). Exit 2 is
not a pass — a mistyped destination contributes an empty haystack, which without that check would
either fail everything or quietly shrink what was searched.

`--allow` names a file of audited regex artefacts, one per line. **Do not add to it to silence a
real drop** — migrate the fact, then confirm it greps positive in its new home. An allowlist that
grows every time the gate complains is a gate that has been turned off slowly.

**`check-claude-md-migration.py` is now a thin wrapper around this**, passing the CLAUDE.md
baseline (`a1eca27`), its nine destinations, `--fenced-section "## Commands"` and
`tools/claude-md-migration.fragments`. The filename, default baseline and output are unchanged
because `AITODO.md` and `docs/notes/context-cut-handoff.md` both cite it. Verified
behaviour-preserving across the refactor: still **252 markers, 249 findable, 3 fragments, 0
unaccounted, exit 0** — identical to before.

### It has been executed, in both directions

`check-doc-migration.tests.py` — 10 cases, offline, each building a throwaway git repo and running
the script unmodified as a child process.

```
python tools/check-doc-migration.tests.py
```

Negative-tested by disabling the drop detection: **3 of 10 go red** — the three that assert a
refusal. The other seven cover the permit direction and the three cannot-run paths, which a
disabled comparison does not affect.

## `check-file-budgets.py` — the size ratchet on everything injected (2026-08-21)

`CLAUDE.md`, the four agent briefs and the fourteen skills are loaded before an agent does any
work, so their bytes are a tax on **every** dispatch. `CLAUDE.md` grew **45,130 → 96,245 bytes in
eleven days and not one commit in that span reduced it** — because no commit ever had to justify
growth. This makes growth explicit.

```
python tools/check-file-budgets.py [--staged]
```

**Ceilings are a ratchet, not a judgement about the right size.** Each was seeded on 2026-08-21 from
that file's size, rounded up to a 512-byte boundary **with at least 256 bytes of slack**. Nobody can
say what the right size for a skill is; everybody can say whether it grew, and growth is the failure
mode that actually happened. Raising one is a commit of its own, saying what earned it.

The slack floor is not cosmetic: a first pass rounded to the next 512 alone and left one file **20
bytes** under its ceiling, where a typo fix would have failed the gate. A gate that cries wolf on
trivial edits is one people learn to bypass.

Exit **0** all within ceiling, **1** something is over *or the table names a file that can no longer
be read*, **2** the gate could not run. A file **not** in the table is ignored, so the hook can run
unconditionally; a file **in** the table that vanished is a failure rather than a skip, or the table
rots and a deletion quietly removes a budget nobody notices is gone.

Bytes are CRLF-equivalent **on both paths**, as for every size figure in this project:
`core.autocrlf=true`, so both `--staged` and the default add the newline count back rather than
reporting the smaller LF figure. Both call the same `crlf_equivalent`, which is what stops them
drifting apart.

> ⚠️ **This sentence was FALSE until 2026-08-24, and in the permissive direction.** `measure_staged`
> returned `crlf_equivalent(blob)`; `measure_worktree` returned the raw on-disk length. So one commit
> measured two different ways — and the **strict** path was the hook's while the **permissive** one
> was the path a human runs by hand. It only diverges where a budgeted file actually sits on disk with
> LF, which is why it went unnoticed in the worktrees (all CRLF there) while the **main checkout**
> carried `.claude/agents/hmi-designer.md` at **7,364** LF bytes against a **7,476** CRLF-equivalent:
> 316 bytes of headroom by hand versus 204 to the hook, so the by-hand run listed **one** tight file
> where `--staged` on the same commit listed **two**. Normalising the worktree path — rather than
> dropping the arithmetic from `--staged` — is the direction that cannot silently loosen a gate.
> **No budgeted file changed side of its ceiling**: the one behaviour change is that
> `hmi-designer.md` now appears on the TIGHT report by hand, as it always did to the hook.

### What belongs in the table

**Scope: files injected into an agent's context** — `CLAUDE.md`, `.claude/agents/*.md`,
`.claude/skills/*/SKILL.md`. As of 2026-08-24 the table is **exhaustive over that scope**: 1 + 4 + 14
= 19, matching the `files in budget table` line the script prints.

🔴 **Nothing enforces that, and the script cannot notice.** The table is its only input, so a skill or
agent added tomorrow is simply unbudgeted and the gate passes — exhaustive **today** is an
observation, not an invariant. Adding the file to `BUDGETS` is a manual step in the commit that
creates it.

**Source files are not candidates however large they get.** `src/`, `tools/`, `tests/` are read on
demand rather than preloaded, so their bytes are not a per-dispatch tax and the ratchet has no claim
on them — the six `src/harness/*.cs` files added on 2026-08-23 by `82af95f` and `7bf400b` are
out of scope, not omissions.

### Making it fail closed

`hooks/pre-commit` runs it with `--staged` on every commit and **refuses** on non-zero. Git will not
install that for you — once per clone:

```
git config core.hooksPath hooks
```

All the logic, including working out which budgeted files are actually staged, lives in the Python
where the tests cover it. The hook stays a three-line invocation on purpose: a hook that grows a
loop per budgeted path is a hook nobody tests. `hooks/*` is pinned `eol=lf` in `.gitattributes`,
because with `core.autocrlf=true` a tracked hook would check out CRLF and `sh` would fail on the
shebang — silently disabling the gate.

### It has been executed, in both directions

`check-file-budgets.tests.py` — **23 cases**, offline. Each copies the script into a throwaway tree
and rewrites **only its `BUDGETS` table** to point at fixtures; every other line, including all the
measuring and reporting, runs as shipped.

```
python tools/check-file-budgets.tests.py
```

Two fixture builders, and picking the wrong one hides a defect: `sized(n)` writes **LF**, so the
script measures it as `n + n//64`; `sized_crlf(n)` writes **CRLF**, so its raw and measured lengths
are the same number. Boundary cases use `sized_crlf` so the assertion is about the boundary. One
case was green on the wrong number until 2026-08-24 for exactly this reason — an LF fixture put it
17 bytes over and `over by 1` matched `over by 17` as a **prefix**; it now asserts the whole line.

Negative-tested twice, both re-derived 2026-08-24:

| comparison disabled | red |
|---|---|
| `if size > ceiling` → `if False` | **6 of 23** |
| `measure_worktree` back to raw `len()` (the asymmetry) | **2 of 23** |

The rest cover the permit direction, the staged-set selection and the CRLF arithmetic, which a
disabled size comparison does not affect.

**End-to-end, the gate was observed refusing a real commit** — see the commit that introduced it.
It replaces the earlier CLAUDE.md-only budget script, whose cases are ported here.

## Benchmarks

`bench-machine.ps1` measures a machine on the axes that matter for this repo; `bench-compare.ps1`
diffs two result files. See the comment-based help in each.
