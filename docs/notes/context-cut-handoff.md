# CLAUDE.md context cut — handoff

**Status as of 2026-08-21: ALL PHASES ARE DONE AND COMMITTED — 0, 1, 2, 3 and 4.** The one thing
still outstanding is the **write-path validation**: a real generation or fix run against the cut
file has still never happened. See "What is NOT verified".

| phase | what | commit |
|---|---|---|
| 0 + 1 | the cut itself | `c078fab` … `8d9a178` |
| 4 | anti-regrowth size budget, fail-closed via a pre-commit hook | `4df5b57` |
| 2 | hookify gates for the two day-costing traps | `cc4fe05` |
| 3 | `evidence.json` hand-back + verifier | `970067a` |

**One-time setup a fresh clone needs:** `git config core.hooksPath hooks`, or the budget gate is
not installed. Confirm with `git config core.hooksPath` → `hooks`.

## Why this was done

Work in this repo was slow per request, and fanning out to parallel agents did not help. The
measured cause: **orientation cost per dispatch dominated the actual work, and parallelism
multiplied it rather than amortising it.** A single `lad-coder` dispatch running `gen-block-new`
loaded ~55,000 tokens before reading one line of IR — `CLAUDE.md` injected (96,655 bytes), the
agent file, `CLAUDE.md` **again** because the agent was told to read it in full, and the skill.
Five parallel lanes meant ~275k tokens of setup, none of it shared.

There was a second effect that mattered as much. `CLAUDE.md` had become a changelog read as
instructions — 61 lines carrying date stamps, 29 carrying retractions ("this previously read",
"was wrong", "cost a day") — and it was **actively instructing the re-reading and re-verification
being complained about**: *"its summary is not proof"*, *"you did not inherit a summary of them,
read the actual file"*, *"empty is not clean"* (×6). Agents re-derived everything because they
were told to.

## What was done

| commit | what |
|---|---|
| `c078fab` | `lad-coder` no longer re-reads `CLAUDE.md` in full, and no longer restates hard rules 1–7 |
| `71891b9` | CLAUDE-only command knowledge migrated into the two READMEs, `openness-quirks.md`, `docs/15` |
| `5f4d5a4` | `CLAUDE.md` 96,655 → 20,277 bytes |
| `56656a1` | three things a smoke-test dispatch proved had been cut too far, put back |
| `8d9a178` | remaining narration reworded to plain statements, 20,478 → 19,593 bytes |

**Result: `CLAUDE.md` 96,655 → 19,593 bytes; `lad-coder.md` 8,747 → 6,234.** Per-dispatch
orientation ~55,000 → ~11,400 tokens (79%, 4.9×). Five parallel lanes: ~275k → ~57k.

### The key structural finding

Half of `CLAUDE.md` (48,432 bytes) was a Commands block duplicating `src/converter/README.md`
(268KB, 92 command headings) and `src/openness-cli/README.md` (144KB, 62 headings) — which
`CLAUDE.md` **already named as authoritative**. But ~15% was NOT duplicated: the newest findings
had been landing in `CLAUDE.md` *because it is the file everyone reads*. That is the growth
mechanism, and it is why Phase 4 matters more than it looks.

`openness-cli library` had no README section at all — `--export-version` and `--probe-documents`
existed only in `CLAUDE.md`. It has one now.

### What is in the new CLAUDE.md, and what moved

Resident: hard rules 1–8 (normative text, unweakened), a **new `## Routing rules` section**
holding operational rules previously buried in narration, `Where things live`, a Commands
**index** (name → one-line → "read the README"), the stage-suspension notice, and the Data
boundary **verbatim**.

Moved: command reference → the two READMEs; environment detail → `docs/notes/openness-quirks.md`;
test-environment facts → `docs/notes/live-project-readiness.md` (which already carried every one
of them); the 20% freeform threshold → `docs/15-generation-pipeline.md`. `docs/06`'s C-201 already
carried the network-title rule, so Conventions needed no migration.

## What is verified, and how

Run the gate before committing any further trim of `CLAUDE.md`:

```
python tools/check-claude-md-migration.py
```

Every falsifiable marker the **pre-cut** `CLAUDE.md` carried must still be findable in
`CLAUDE.md` or a destination doc. Currently **249 of 252 found, 3 known markdown fragments
allowlisted, exit 0**. It is negative-tested: removing one destination makes it report 89
unaccounted and exit 1. Do not add to `KNOWN_FRAGMENTS` to silence a real drop — migrate the fact
and confirm it greps positive in its new home.

Also verified at the time of the cut: every path cited in `CLAUDE.md` resolves; all touched files
kept CRLF; agent and skill frontmatter parses with no truncated descriptions; `dotnet build -c
Release src/converter/converter.sln` clean; all 15 normative hard-rule keywords still present.

## What is NOT verified

- **Only a read-only task has been run against the cut file.** One `lad-coder`
  `explain-plc-block` dispatch, instrumented to report gaps. **A generation or fix run has not
  been tried**, and those exercise far more of the file — the READMEs are now on the critical
  path in a way they were not before. That is the next validation worth doing.
- ~~**The session-caching behaviour is inferred, not proven.**~~ **SETTLED 2026-08-21.** The
  mechanism was as suspected: project instructions are read once at session start, so the original
  smoke test ran with the old 96KB copy injected and was measuring only the file on disk. After a
  restart, a second instrumented `explain-plc-block` dispatch reported its own injected
  `CLAUDE.md` carries the `## Routing rules` heading at ~19-20KB. **Subagents receive the cut
  file.** If you cut further, restart before believing any dispatch's report about it.

### What the second run found

Re-run 2026-08-21 against `ir/test-project001/FB_ShredderSequencer.ir`, read-only, instrumented.

- **No regression attributable to the cut.** Orientation cost was two `ls` calls to locate the
  Release converter exe. Both things restored in `56656a1` paid off measurably: the Green-tier
  line for `test-project001` saved a `docs/13` open, and the one-line `digest --fingerprint` index
  entry saved opening the converter README.
- **Dead weight is real but is NOT an argument for trimming further.** The agent reported ~80% of
  the resident file inert for this task - all twelve routing rules, the whole `openness-cli` table,
  the compile-gate detail, the mechanical-floor exit-1/exit-2 principle. That is expected for a
  read-only explain and says nothing about the write path, which is exactly what those rules serve
  and what still has not been tested.
- **One real gap, now recorded as `deferred-items.md` D-10** - and it is not `CLAUDE.md`'s. The
  agent read raw SimaticML to establish execution order, because IR kind-orders statements and
  nothing legible to it says kind-order is not execution order. Owner's ruling: the ladder agent
  has no business in the XML at all. Hard rule 7 as written prohibits only *editing* it, so no
  stated rule was broken. See D-10 - the boundary fix and the IR-legibility fix are coupled, and
  doing the boundary alone makes the next run ship the false defect this one narrowly avoided.

## What was left — all three done 2026-08-21

Kept below as written, because the reasoning is still the reasoning; what each one turned into is
recorded first. Two things were learned in the doing that the plans above did not anticipate:

- **Phase 4 could not be done without Phase 2's mechanism, and then turned out not to want it.**
  Phase 4 asked for a fail-closed check. This repo has no CI, no git hooks and no pre-commit
  config, and **hookify fails open** — its hooks always `sys.exit(0)`, and a missing interpreter or
  wrong CWD disables every rule silently. Worse, hookify can only regex the *text* of an edit, so
  it cannot compute a resulting file size at all. A tracked `hooks/pre-commit` plus
  `core.hooksPath` is the only mechanism here that actually refuses.
- **A gate the size budget alone would not have caught:** `.gitattributes` had to pin `hooks/*` to
  `eol=lf`, or `core.autocrlf=true` checks the hook out CRLF and `sh` fails on the shebang —
  disabling the gate silently, which is the failure mode a gate must never have.

Both hookify rules carry a residual worth knowing before writing a third: the matched field is the
whole command line, so writing *about* a blocked command through `Bash` trips the block. Measured
immediately — the heredoc authoring the rules' own test vectors was denied. Author such content
with the `Write` tool; the rules are `event: bash`.

The original text, in the original priority order:

**Phase 4 — the anti-regrowth size budget. Do this one first if you only do one.** Without it the
file regrows: it went 52KB → 96.6KB over its last 30 commits, by exactly the mechanism described
above. Needs a fail-closed check (this repo's own maxim: *a warning is not a gate*) asserting
`CLAUDE.md` stays under budget — 20,480 bytes was the ceiling held during the cut. `tools/` holds
the precedent for a checked script (`confirm-roundtrip-fence.tests.ps1`), and
`tools/check-claude-md-migration.py` is the shape to follow. The rule to encode alongside it: new
findings land in the README or `docs/notes`; `CLAUDE.md` gets a line only if it changes what an
agent must do *before* it looks anything up.

**Phase 2 — promote deleted warnings to hookify gates.** Mechanism is already proven in this repo:
`.claude/hookify.block-safety-fblock-content.local.md` and `.claude/hookify.block-simaticml-edits.local.md`
both work. Hookify supports `event: bash` with a `pattern`, so both day-costing traps are directly
expressible: the `--claims` store-root shadowing (regex on a `--claims` path whose last segment
repeats the project name) and a Debug-binary invocation where Release is expected.

**Phase 3 — the `evidence.json` hand-back.** Hard rule 8 requires verifying the sub-agent's actual
diff and compile evidence, which currently means the orchestrator re-reads the work. Nearly every
tool already has `--json`. Change the hand-back contract so the agent writes
`agent-tasks/<id>/evidence.json` carrying the raw `--json` of preflight / `diff --only` / compile /
`sanity-check` plus `converter ir-hash` for every file touched. Verification then becomes running
one command against exit codes and hashes, instead of a re-read. This is the change that most
directly attacks the "agents recheck each other's claims" cost.

## Open items for the owner

- **`explain-plc-block` skill defect (pre-existing, not caused by the cut).** Its data-boundary
  trigger says "not the synthetic `ir/reference/` corpus" and never names `test-project001`, so by
  literal reading every explanation of the main sandbox is routed to `docs/13-data-boundary.md`.
  `CLAUDE.md` now states the tier (Green) so it is settleable from context, but the skill should
  name the sandbox alongside `ir/reference/`. One line.
- **Four `FB_Hx*` corpus blocks are called from OB1 but nothing writes their inputs.** Found
  incidentally by the smoke test: `FC_HarnessCopyLayer` has no `Dwell*` reference and
  `HarnessMirror` declares no registers for them; `converter undriven-scan --fb FB_HxDwellTimer`
  exits 1 with 3 undriven members. The agent read this as "copy layer not generated yet" rather
  than a defect, since all four siblings are identical. **Question: is the Hx wiring expected to be
  committed alongside these blocks, or produced at deploy time?** If the latter this is simply
  pending; if the former, four blocks are inert.
- **D-9 in `docs/notes/deferred-items.md`** records the deferred option of pushing the Routing
  rules down into the skills that own them. Not recommended as things stand — several of those
  rules bite an orchestrator that is not running any skill.

## Traps for whoever picks this up

- **The Write tool strips CRLF.** It silently wrote `CLAUDE.md` as LF during this work; caught by
  the `file` check, fixed with a binary rewrite. Use Edit for changes, and if you must Write a
  whole file, convert afterwards. **The Edit tool preserves CRLF** — verified 167/167 on this file
  — so there is no reason to avoid it.
- **Verify CRLF by counting bytes, not with `grep`, `awk`, or `file`.** All three lie here, and two
  of them lie confidently. In this Git Bash, `grep -c $'\r'` returns **0** on a genuine CRLF file
  and `awk '/\r$/'` never matches, because the shell strips CR before the pattern sees it — so the
  obvious check reports "no CRLF" on a file that is entirely CRLF. `file` only reports what
  terminator it saw *somewhere*, so it cannot detect a partly-converted file. The one reliable
  check is to compare counts, which must be equal:

  ```
  tr -cd '\r' < <path> | wc -c
  tr -cd '\n' < <path> | wc -c
  ```

  (Corollary: `cmd_a && cmd_b` after a `grep -c` that legitimately finds nothing never runs
  `cmd_b` — grep exits 1 on zero matches. A verification chain built that way silently skips its
  own checks.)
- **CRLF is not universal here — `.ir` and `.xml` are LF by design.** `.gitattributes` pins
  `*.ir` and `*.xml` to `text eol=lf` so tool-written formats byte-compare in tests (owner-approved
  2026-07-16). "Converting" either back to CRLF is a regression, not a fix. CRLF applies to `.md`,
  `.cs`, `.ps1`, `.py`.
- **A byte figure must say whether it is blob or worktree.** `core.autocrlf=true`, so git stores
  `.md` as LF and materialises CRLF: `CLAUDE.md` is 19,444 bytes as a blob and 19,593 in the
  worktree, one byte per line apart. Every size in this project's record is the **worktree**
  number (`56656a1` quotes 20,478 = blob 20,329 + 149). Quote the same one or the budget gate and
  the commit log disagree by 149 bytes.
- **Run `tools/check-claude-md-migration.py` before committing any further trim.** It is the only
  thing standing between a tidy-up and a silently lost fact.
- **The Data boundary paragraph is deliberately untouched, and should stay that way.** It reads
  redundant — "don't tier-triage it, don't ask per-file, don't hold back" — but each clause closes
  a different behavioural loophole rather than restating one, and it is the only rule here whose
  failure mode is a confidentiality breach rather than a wasted hour.
- **`docs/notes/claude-md-migration-inventory.md`** is a working file recording what was checked
  per command. Delete it once the cut has settled; it will go stale.
