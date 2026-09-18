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

## `build-scrub-rules.py` — the de-identification rule set for the public export (2026-09-17)

The publication plan (`docs/notes/portfolio-publication-plan.md`) rewrites this repository's history
into a public artifact. Its audit found that the obvious way to do that **looks right and is not**:
a `\bCamelCase\b` rule scrubs the identifier out of prose and matches none of the lower-hyphenated
directory names built from the same word, so the scrub reports success while the paths still carry
it (finding **A8**). This emits the rule set instead of assembling it by hand.

```
python tools/build-scrub-rules.py [--repo .] [--maps sanitization]
       [--terms sanitization/scrub-terms.md] [--out sanitization/scrub]
       [--green <corpus> ...] [--job-folder <folder> ...]
       [--scan history|head] [--min-global-length 8] [--name-collisions]
```

It emits `replace-text.txt`, `replace-message.txt`, `path-renames.args` and `manifest.json`. Last
full-history run: **106 rules and 22 `--path-rename` pairs** covering 113 changed paths, over 6,345
blobs. **It rewrites nothing** —
`git-filter-repo` performs the rewrite and `verify-scrub.py` judges the result, deliberately as a
separate script sharing no derivation code. If the emitter and the verifier computed their
vocabulary the same way, a bug would produce a rule that misses X and then hunt for X the same wrong
way and find nothing: a confident, earned-looking zero over the wrong population.

Exit **0** rules emitted, **1** a blocking finding (non-injective map, unresolvable collision,
pre-existing sentinel), **2** NOTHING EXAMINED (no maps, no term rows, no surviving rule), **3**
REFUSED BEFORE READING ANYTHING (the term list is tracked, or `--out` is in tracked space). **On 1,
2 and 3 nothing is written** — a partial rule set is the worst possible artifact, because it looks
like a rule set and scrubs some of what it names.

**What it deliberately does NOT do.** It does not decide whether a name identifies a site. The
only mechanical filters are a length floor and membership of the already-sanitized Green corpora,
and AB-1 measured roughly half of its own candidates as conventional names. So a conventional name
absent from Green *will* be replaced, and a identifying name present in Green *will not*. Measured on
the one identifier-bearing path the rules leave alone is Green-present. That is the documented cost
of removing human adjudication, not a defect to file.

⚠️ **A correction, recorded because the wrong version of it was published here first.** An earlier
revision of this section claimed "92 of 93 identifier-bearing paths are matched", measured by
applying the emitted regexes to path strings in Python. **That measurement was meaningless.**
`--replace-text` rewrites blob *contents*; filter-repo never applies those regexes to a path. Paths
are renamed only by `--path-rename`, which this tool did not emit at all until 2026-09-17. The claim
was true about Python and false about the artifact — the most comfortable kind of wrong.

### Three traps, all measured here rather than anticipated

- **Breadth is a REPORT, not a filter.** Withholding a variant for appearing in many files looks
  principled and is backwards — a path stem cited across thirty-eight files is wide *because it is
  load-bearing*. Withholding on breadth left **77 of 93 identifier-bearing paths unmatched**.
- **Path names are part of the corpus.** `git cat-file` never shows a path, so a content-only scan
  calls a directory-only identifier dead and emits no rule for it.
- **Breadth must be counted at HEAD, never over history.** Counted over history a file edited thirty
  times contributes thirty blobs, manufacturing an ordinary-looking count out of editing activity:
  8 variants at HEAD versus 22 over history, and those 14 were rules *not emitted*.

On its first complete run the tool **refused**, having found that five replacements in the existing
maps occur verbatim in the live job's own IR — AB-1's trap 2, *a replacement can itself be a leak*.
Under the no-human ruling those now resolve deterministically and are re-checked against both the
job folder and this repository.

### Six defects found by an adversarial review, 2026-09-17, all fixed

The review's headline was that a run **exited 0 — "98 rules, every check passed" — while emitting no
rule for 18 of the 19 identifiers in the owner's own term list.** Every one of these was live in a
shipped artifact:

| | defect | consequence |
|---|---|---|
| **F1** | the length floor was applied to *declared* terms, not just inferred map keys | 15 of 19 term rows discarded, **including all four job codes** (five characters each); 11 of the 12 identifier occurrences in the tracked `.gitignore` survived every rule |
| **F3** | **no `--path-rename` set was emitted at all** | every identifier-bearing directory and filename survived the rewrite, while path names were scanned, counted and turned into rules that could not touch them |
| **F2** | `--out` tested *tracked*, not *ignored* | `--out docs/scrub-out` wrote the file naming every identifier into tracked space, one `git add -A` from a commit |
| **F5** | a JSON `null` map value | the literal string `None` as a replacement, or a bare `TypeError` exiting 1 with none of the gate prose |
| **F6** | `git cat-file` emits `<oid> missing`; the parser broke on it | **silently abandoned the rest of history** with a non-zero scanned count, so the empty-is-not-clean guard never fired |
| **F12** | rows shorter than three cells were skipped before validation | the silent term-list shrink this design exists to prevent |

**A declaration is not a candidate** is the principle F1 cost. The length floor and the Green test
exist to stop a name *inferred* from a map colliding with ordinary code; neither reasoning survives
contact with a term the owner wrote down by hand, and applying them anyway discarded exactly the
vocabulary the term list exists for.

### It has been executed, in both directions

```
python tools/build-scrub-rules.tests.py
```

41 cases, offline, each building a throwaway git repo and running the script unmodified as a child
process. The suite is slow (roughly half a minute per case on Windows) because of the temp-repo
setup, not the script, which runs in well under a second.

The load-bearing case is **`A8: lower-concatenated variant matches a hyphenated directory`**,
asserted on the string a rule must match rather than on the intent. Two further regressions are
pinned because both were live defects during construction: `a path-only occurrence still gets a
rule`, and `a wide variant is reported but still emitted`.

Negative-tested five ways, all re-derived 2026-09-17 by mutating the script, running the suite and
restoring it byte-identical:

| comparison disabled | red |
|---|---|
| Green-membership test → `if False` | **1 of 21** |
| lower-concatenated variant removed (the A8 fix) | **1 of 21** |
| path names dropped from the corpus | **1 of 21** |
| length floor → never withhold | **1 of 21** |
| injectivity check disabled | **1 of 21** |

One red per mutation is the intended shape here rather than a weak result: each guard has exactly
one case watching it, and each has now been observed refusing. The remaining cases cover the permit
direction and the four cannot-run paths, which a single disabled comparison does not affect.

**A defect this suite did not catch, recorded because it was real.** The harness itself deadlocked:
`subprocess.call` with a `PIPE` nobody reads blocks once the child fills the buffer, and `git add`
emits one CRLF warning per file, so the 40-file case hung forever while every smaller case passed.
Test harnesses in this directory use `DEVNULL` for exactly this reason.

### F16–F19 discharged — the default mode was tested by nothing (2026-09-18)

🔴 **Every case above ran `--scan head`. The default is `--scan history`, and it is the mode that
produced the published artifact** — 6,413 blobs, 119 rules. One word inside the shared runner:

```python
proc = subprocess.Popen([sys.executable, script, "--repo", tmp, "--scan", "head"] + list(args), ...)
```

An override nobody can see at the call site is the kind that survives a review. `run()` no longer
injects a mode; each case names its own through `run_head` or `run_history`.

**The fixture could not have expressed the difference either.** `build()` makes exactly one commit,
so the object database and HEAD held identical blobs — a `--scan history` case over that fixture
would have passed while proving nothing. The new `bury()` helper is the **exact inverse** of
`verify-scrub.tests.py`'s `scrub()`: that one amends and prunes so the old blob is *gone*; this one
commits a second time so the old blob *remains* in the ODB while being absent from HEAD.

Seven history cases. The load-bearing one is **`an identifier only in an old commit`**: one
repository, two modes, **opposite verdicts** — head sees a clean tree and refuses with exit 2,
history finds the buried identifier and emits a rule. The case asserts the disagreement, because if
the modes ever agree the fixture has stopped exercising history and every case beside it is
worthless. The others cover the unreachable-object walk (with a control proving
`rev-list --objects --all` cannot see it), non-UTF-8 blobs, a corrupted loose object, the
blobless-history refusal, manifest provenance, and the head-scoped breadth pass.

#### Two defects in the history path, both found by planning the tests rather than by running them

**The EMPTY-IS-NOT-CLEAN guard could not fire.** The path corpus was chained in with the blobs and
is a `str`, never `None`, so `searched >= 1` unconditionally and `if not searched:` was unreachable
dead code. A repository whose history holds not one blob reported `blobs scanned: 1` and carried on.
Paths are now a `prelude` — scanned for vocabulary, not counted as blobs. Measured on this
repository: **6414 → 6413, exactly one phantom blob removed, with all 119 rules byte-identical.**

**`proc.wait()` discarded git's exit status.** A `git cat-file` that fails outright closes stdout
immediately, so the walk ends after zero blobs and the caller receives a perfectly clean, perfectly
empty corpus. It now raises `ScanFailed`, which `main()` turns into exit 2.

*Recorded rather than faked:* **no fixture here induces a non-zero `cat-file` exit.** Removing
`.git/objects`, and pointing `GIT_OBJECT_DIRECTORY` at nothing, both make the whole repository
undiscoverable, so the builder refuses at exit 3 long before it scans. The guard is right and the
case does not exist; saying so is better than a case that pretends to cover it.

#### The vacuous assertions were derived, not guessed

The five were never enumerated anywhere. Rather than pick five that "look weak", each behaviour a
case claims was broken in turn and the suite watched for which case kept printing PASS — vacuity by
measurement. Re-derived 2026-09-18, restoring byte-identical:

| mutation | guard case | red | caught by its own guard |
|---|---|---|---|
| longest-first ordering reversed | longest-first ordering holds | 1 of 30 | yes |
| identity mappings no longer dropped | identity mappings are dropped | 1 of 30 | yes |
| term rows no longer override map keys | a term row overrides a map key | 1 of 30 | yes |
| Green-present variants no longer withheld | a Green-present variant is withheld | 1 of 30 | yes |
| structured sections no longer ignored | a structured section is counted but ignored | 1 of 30 | **was 0** |
| every artifact written **empty** | emits the artifacts | 16 of 30 | **was no** |
| `path-renames.args` never written | emits the artifacts | 1 of 30 | **was 0 of 30** |
| `==>` dropped from declared-term rules | every line carries an explicit `==>` | 2 of 30 | **was no** |

What that found:

- **`path-renames.args` was checked by nothing at all.** Suppressing the whole file turned 0 of 30
  red — the same artifact whose total absence was defect F3.
- **`emits_three_files` asserted existence, never content.** Four empty files passed it; sixteen
  other cases went red and the one case *named* for the artifacts did not.
- **`longest_first_ordering_holds` discarded the exit code**, then asserted
  `lens == sorted(lens, reverse=True)` — and `[] == sorted([])` is true, so a run that exited 2 and
  wrote nothing satisfied it.
- **`every_line_carries_an_explicit_arrow` slept through the removal of `==>`.** The emitter has two
  branches and the fixture produced no declared-term rule, so it never saw the one that was broken.
- **`a_structured_section_is_counted_but_ignored` tested only the "counted" half.**

**One prediction was wrong and one repair was itself vacuous**, both worth recording. The ordering
case *did* catch a reversed sort — it was weak in a different direction than expected. And the
first repair to the structured-section case used the realistic `<block>#1` key form, which stayed
green under mutation because `#` is outside the token class, so the key could never be found in the
corpus whether the section was ignored or not. Caught by re-running the detector, not by reading it.

### F4, F9, F11, F13 discharged — four defects that changed nothing (2026-09-18)

The last four review findings. Each changes what the builder *emits*, so each was expected to move
the artifact. **None of them did** — and the interesting part is that every one has a measured
reason why not, rather than a shrug.

| | defect | consequence | why the artifact did not move |
|---|---|---|---|
| **F13** | the vote list is appended to once per **(section, key)**, not per file, so one map declaring a key in `names` *and* `tags` voted twice | rung 3 asks "how many maps agree" and was counting how often one map repeated itself — **one map could outvote two** | 20 duplicate-stem pairs exist, but only 3 keys have competing replacements and **rung 3 decides none of them** |
| **F11** | the map **section** is in scope at the point the pair is stored and was thrown away | every map key reached `variants_for` as `"block"`, so **no map key could ever get a spaced form** — the only form prose writes a identifying name in | 14 keys gain a class, 4 would gain a spaced variant, and **all four are already in the owner's term list** with a spaced class |
| **F9** | 🔴 `candidates.setdefault(v, key)` — first writer wins, **in silence** | a losing key got the wrong replacement or **no rule at all**, counted in no tally; and a map key could squat a **declared** term's form, which then lost its anchorless `(?i)` rule — **F1 reintroduced through a path neither guard covers** | **5 collisions**, all of them the harmless same-replacement shape |
| **F4** | `replacements & searchable` — exact, case-sensitive, whole-string set intersection | cannot see a needle matching *inside* a replacement, **which is the case the emitter deliberately creates**: declared terms are emitted anchorless and case-insensitive by design | **0 overlaps**: the artifact really was clean, and is now clean *by the check* rather than by luck |

**The 5 for F9 is the recorded figure, re-derived.** The review wrote down "5 live instances" and
nobody kept the derivation — no run log, no manifest field, nothing. Counting claimants at the
collision site gives **exactly 5**, and the method is now in the tool and printed on every run.

**What F4's substitution can and cannot fix**, because it decides whether the run gates. An
*equality* overlap against an inferred needle is fixable by suffixing: `\bGenericFoo\b` no longer
matches `GenericFoo1`, because a digit is a word character and kills the right boundary. A
*substring* overlap, or any overlap against a **declared** needle, is not — a declared rule is
anchorless, so it still matches inside `GenericFoo1`, and **no suffix removes a substring from a
string.** Those gate, and the message says to choose a replacement by hand. That is the honest
answer rather than a substitution that does not work.

**The trap in F11, and why `klass_of` was left alone.** It does not mean "this key's class". It
means **the owner wrote this key down**, and three other decisions read it that way: a declared key
bypasses the length floor, bypasses the Green test, and is emitted anchorless and case-insensitive.
Giving map keys a class by folding them into `klass_of` would have promoted ~1,390 inferred keys to
declared status — a far larger change than the fix, wearing the costume of a one-line edit. A
separate dict carries the section class, and a case asserts the promotion did not happen.

**`resolve_conflict` had never run past its first line.** Every fixture built a single map, so
`len(candidates) == 1` always and the function returned rung 0 immediately. Rungs 1–4 — the whole
ladder deciding which invented name a contested key gets — were unexecuted, which is how F13 sat in
rung 3 undisturbed.

**Rung 2 was left alone, for a measured reason.** `own = [c for c, stems in ... if key in stems]`
reads like a type confusion — `stems` holds map filenames and `key` is a vocabulary key. It is not:
the maps are named after the blocks they describe, **41 of 43 stems are themselves keys**, and the
live run shows `rung2=1`. It means "a block's own map is authoritative for its own name".

Negative-tested sixteen ways, 2026-09-18, restoring byte-identical — **every mutation caught by the
case named for it**. The eight new ones:

| mutation | guard case | red |
|---|---|---|
| F13: votes counted per occurrence again | one map cannot outvote two | 1 of 41 |
| F13: rung 3 removed entirely | a real majority still wins at rung 3 | 2 of 41 |
| F11: the section class is discarded again | a company key gets its spaced form | 1 of 41 |
| F11: every section becomes spaced-eligible | a names key gets NO spaced form | 1 of 41 |
| F9: a declared claimant no longer wins | a DECLARED term wins and keeps its anchorless rule | 1 of 41 |
| F9: unresolved collisions no longer gate | different replacements GATES | 1 of 41 |
| F4: overlap back to whole-string equality | a declared needle INSIDE a replacement GATES | 1 of 41 |
| F4: auto-substitution disabled | an equality overlap is auto-substituted | 1 of 41 |

🔴 **A defect in the harness, found by a mutation that made the suite VANISH rather than turn red.**
`case()` caught only `AssertionError`, so a case raising anything else — an `IndexError` from
parsing a rule line that no longer had the shape it assumed — killed the run mid-way and printed
**no RESULT line at all**. A harness that cannot tell "one case failed" from "the run died" is the
empty-is-not-clean failure wearing a test-runner costume. It now catches `Exception` and names the
type.

**Principle earned by F9, worth carrying:** *a silent resolution is worse than a wrong one.* All
five collisions turned out to be harmless — and nothing in the tool could have told anyone that,
which is the actual defect.

## `verify-scrub.py` — the Gate 3 oracle (2026-09-17)

`build-scrub-rules.py` ends every run saying *"IT HAS PROVED NOTHING about the result."* This is the
proof. It walks a `git-filter-repo`-rewritten clone and decides whether restricted identifiers survive.

```
python tools/verify-scrub.py --clone <rewritten clone> [--maps sanitization]
       [--terms sanitization/scrub-terms.md] [--canary sanitization/canary.json]
       [--build-baseline <file>] [--make-canary] [--fast] [--name-matches]
```

Exit **0** an EARNED zero · **1** a residual or damaged invariant · **2** NOTHING EXAMINED (any zero
denominator, absent or stale canary, a broken instrument) · **3** refused before reading anything.
~30 s over 14,291 objects.

**It shares no derivation code with the builder, deliberately.** If the emitter and the verifier
worked out their vocabulary the same way, a bug would produce a rule that misses X and then hunt for
X the same wrong way and find nothing. So it re-derives its searches from the same raw inputs,
**wider**: case-insensitive, no length floor, no Green exclusion, substring-capable, and it includes
the 326 identity mappings the builder correctly drops as no-ops — *nothing to rewrite is not nothing
to leak.*

### The instrument control is the check without which the tool is unfalsifiable

Every other check reports zero as good news, so the intolerable failure is a matcher that can never
match. A synthetic object carrying every needle goes through the **same** `tokenise`/`residuals` the
real corpus does; every needle must find itself, and anything less is exit 2.

🔴 **It caught exactly that on its first run.** 1,505 of 1,719 needles could not match anything,
because the tokeniser split on `[a-z0-9_]+` and most needles are dotted tag paths. The tool had
already reported a plausible "50 residuals" over the 12% of its vocabulary that happened to be single
words. **The canary did not catch it** — those counts use a different code path and stayed healthy
throughout. Nothing else would have found it.

### Three tiers, because a flat rule was unusable

A flat "any hit fails" verdict produced 394 residuals: 317 identity mappings, 20 documented builder
decisions, and **59 of the remaining 61 were the same identifier matched inside a longer one**. A
gate that cries wolf 59 times out of 61 gets learned-ignored, which is its own false green.

| tier | membership | gates on |
|---|---|---|
| **T1 DECLARED** | any term-list row | presence **anywhere**, embedded included |
| **T2 INFERRED** | distinctive map keys | **whole-token** presence only |
| **T3 ORDINARY** | identity mappings · Green-present · short | never on presence — **on a DECREASE** |

T3's inversion is not a softening: a conventional name falling from 1,438 occurrences to 0 is the
signature of a rule eating the already-clean corpus, and a flat rule cannot express that check at all.

### What it deliberately cannot see, printed on every pass

An identifier in **neither** input (the builder cannot rule it and this cannot hunt it — independence
of *code* does not remove a shared dependence on *inputs*); the 15 binary blobs, byte-searched and
**not OCR'd**; an identifier split by a line break or markup; and an **inferential** identification —
a plant described precisely enough to be recognised without being named.

### It has been run against a real rewrite, in both directions

Exit 0 on the rewritten clone; **exit 1 on the unscrubbed source** (T1 10, T2 79). The second is the
more important result. Proven recipe: `clone --no-local` → `filter-repo --replace-text
--replace-message @path-renames.args --mailmap` → delete all but the publishing branch → `reflog
expire --all --expire=now && gc --prune=now` → verify.

**A leak the rewrite could not reach.** The final residual was a **branch name**: `filter-repo`
renames paths, not refs. The remedy was a step this project had already written down and not
performed. That is the gate earning its place rather than confirming its author.

### It has been executed, in both directions

```
python tools/verify-scrub.tests.py
```

31 cases, ~50 s, offline. Each builds a throwaway git repo, copies the script in **unmodified** and
runs it as a child process, through a real **pre-scrub → scrub → verify** cycle — the canary means
nothing unless it was recorded against the unscrubbed repository.

The five tier cases are the point of the file, because the three-tier model decides every verdict and
was previously proven by nothing: a **T1 embedded-only hit gates**; a **T2 embedded-only hit does
not** while a **whole-token one does**; **T3 presence does not gate** but a **T3 decrease does**.

Negative-tested six ways, re-derived 2026-09-17 by mutating the script, running the suite and
restoring it byte-identical:

*(Measured 2026-09-17, when the suite was 18 cases. The counts are left as they were taken rather
than rescaled to today's 31 — a re-derived number and a remembered one should not be made to look
alike.)*

| comparison disabled | red |
|---|---|
| case-insensitive search → case-sensitive | **7 of 18** |
| substring/embedded pass → whole-token only | **4 of 18** |
| tree-entry path walk → skipped | **1 of 18** |
| positive control (MUST-SURVIVE) → disabled | **1 of 18** |
| T3 over-scrub decrease check → disabled | **1 of 18** |
| non-ODB carrier scan → disabled | **1 of 18** |

🔴 **The last row was 0 of 18 on the first run, and that is the most useful thing this exercise
produced.** The whole `.git/config` / reflog / `packed-refs` surface could be deleted and every test
still passed — the branch-name case reaches refs through `for-each-ref` and never touches those
files, so a wholly untested surface looked covered. The case that now guards it plants a site
name in a remote URL, which is how that leak actually arrives.

**A fixture defect worth recording too.** Two cases failed at first against a *correct* tool, because
the helper standing in for filter-repo edited a file and committed — leaving the old blob in the
object database, which this gate walks. Editing and committing is not a scrub. The helper now amends,
expires the reflog and prunes, which is the state filter-repo actually leaves and is literally the
tail of its own recipe.

### The build comparison — the half of Gate 3 that was decorative (2026-09-18)

`verify-scrub.py` answers *"are there residual identifiers?"*. It never answered *"did the rewrite
break anything?"*, and for a while it looked like it did:

```python
if args.build_baseline and os.path.isfile(args.build_baseline):
    print("build baseline             : %s" % args.build_baseline)
```

🔴 **That parsed nothing and compared nothing.** Handing it any file at all removed the
*"THIS RUN SAYS NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST"* banner and verified exactly as much
as passing nothing — **a gate that greened on a file existing.** It now takes both halves:

```
python tools/capture-build-baseline.py --out sanitization/build-baseline.json          # before
python tools/capture-build-baseline.py --out sanitization/build-current.json --repo <clone>
python tools/verify-scrub.py --clone <clone> \
       --build-baseline sanitization/build-baseline.json \
       --build-current  sanitization/build-current.json
```

A baseline **on its own keeps the banner up**, because one half of a comparison is not a
measurement.

**What gates is asymmetric on purpose.** A lost pass, a risen failure count, a vanished assembly and
a target that used to build all gate; new assemblies and higher pass counts are reported and left
alone. A test that started passing is not evidence of anything and **must never be able to cancel a
loss elsewhere**.

**Keyed per assembly, never on the total.** 10+5 before and 5+10 after nets to zero: a whole
project's tests moved and a total-only check calls it clean. There is a case for exactly this.

#### Two things the capture found about itself

**It reported 809 passed / 24 failed out of a solution that does not build here.** `dotnet test
--no-build` runs whatever is in `bin/`, and `OpennessCli.Tests.dll` was dated three weeks earlier —
built on the previous machine, before this one had an SDK. Its own project was never attempted,
because the project it depends on failed first. The error runs in the dangerous direction: **a fresh
clone has no `bin/` at all**, so the stale assembly is absent on the other side and reads as a whole
test project destroyed by the scrub.

**Timestamps cannot answer this, and trying was worse.** An incremental build does not rewrite an
already-current DLL, so a re-run minutes later condemns every assembly in the repository. The signal
that works is MSBuild's own `Project -> ...dll` line: printed for a project it rebuilt **and** for
one it confirmed up to date, absent for one it never reached. Unverified assemblies are recorded
under `unverifiedAssemblies` and **not counted** — visible, because an omission the reader cannot see
is indistinguishable from a tree that never had those tests.

### `--expect` — the same comparison, wired to CI (2026-09-18)

`capture-build-baseline.py --expect <baseline>` gates a run against a committed baseline:

```
python tools/capture-build-baseline.py --repo . --out ci-capture.json --expect tests/ci-baseline.json
```

**It is the whole test step of `.github/workflows/ci.yml`**, and deliberately not a loop over
`*.sln`. That loop is what a CI script naturally becomes, and it would skip `src/hmi-cli`'s **161
tests in silence** because that project belongs to no solution. This tool discovers orphans, records
targets that cannot build, excludes assemblies MSBuild did not produce in the run, and refuses on an
empty capture.

🔴 **NOTHING IS FILTERED OUT, and that was a change of mechanism.** The plan was to exclude the two
known-failing tests by fully-qualified name. Measured: the failing case is **one parameter of a
`[Theory]`**, so a name filter matches the *method* — it would have hidden **16 passing round-trips to
suppress 1 failure**, which is the `continue-on-error` outcome the ruling had explicitly rejected,
arriving by a different door. Instead every test runs and the RESULT is compared: the baseline records
exactly two failures, so a **third fails the build** and the two hide nothing.

**This duplicates the comparison in `verify-scrub.py`, and the duplication is deliberate.** That one
answers *"did the scrub break a test"* mid-rewrite and needs a canary, a needle vocabulary and a
clone; this one answers *"did this commit break a test"* on a runner with none of those — and the
gitignored `sanitization/` inputs a fresh clone does not have. The rules are kept identical on
purpose; change one and change both.

Negative-tested eight ways, 2026-09-18, restoring byte-identical — **every mutation caught by the case
named for it**:

| mutation | guard case | red |
|---|---|---|
| decrease-in-passes check disabled | a DECREASE in passes GATES | 3 of 22 |
| failure-rise check disabled | a RISE in failures GATES | 1 of 22 |
| vanished-assembly check disabled | a VANISHED assembly GATES | 1 of 22 |
| per-assembly keying → totals only | a MOVE between assemblies GATES | 1 of 22 |
| newly-unbuildable check disabled | a NEWLY UNBUILDABLE target GATES | 1 of 22 |
| missing-baseline refusal → no gate | a MISSING baseline is exit 2 | 1 of 22 |
| duplicate-row refusal disabled | a DUPLICATE row is refused | 1 of 22 |
| unparseable JSON returns empty | malformed JSON is refused | 1 of 22 |

🔴 **One of those was 0 of 22 on the first run, and the case was VACUOUS.** `a MISSING baseline is
exit 2` asserted only the exit code, and its fixture never produced an assembly — so the run exited 2
at *"NOTHING EXAMINED"* and never reached the `--expect` check at all. It was watching an entirely
different refusal produce the same number. Two things fixed it: `--expect` is now validated **before**
the build rather than after, which also means a typo refuses in a second instead of after five
minutes; and the case asserts the reason, not the code.

#### It has been executed, in both directions

```
python tools/verify-scrub.tests.py           # 31 cases, ~50 s
python tools/capture-build-baseline.tests.py # 22 cases, ~4 s, no SDK needed
```

The `--expect` gate was also run against the real repository with five perturbed baselines — a lost
pass, a new failure, an assembly that did not run, an equal-total move, and a target that used to
build — and refused all five with a named finding.

Negative-tested nine ways against the comparator, 2026-09-18 — mutate, run, restore byte-identical:

| comparison disabled | red |
|---|---|
| decrease-in-passes check | **2 of 31** |
| per-assembly keying → totals only | **2 of 31** |
| vanished-assembly check | **1 of 31** |
| failure-rise check | **1 of 31** |
| newly-unbuildable-target check | **1 of 31** |
| a lone baseline silences the banner (*the original stub*) | **1 of 31** |
| duplicate-assembly refusal | **1 of 31** |
| missing-baseline-path refusal | **1 of 31** |
| unparseable-JSON refusal | **1 of 31** |

🔴 **The last two rows were 0 of 31 on the first run.** Both mutations still produced exit 2 — the
loader fails to open a missing file a moment later, and an emptied capture trips the zero-assemblies
refusal. **The verdict was defended twice over and the diagnostic not at all**, so a malformed file
would have reported *"listed ZERO assemblies"* and sent the reader hunting for missing tests. The
cases now assert the reason, not only the exit code.

**The baseline this repo starts from**: 23 assemblies, **5,877 passed, 19 failed**, SDK 8.0.425. All
19 failures are pre-existing and were verified as such by running the same suites at the commit
before this branch — identical names, identical counts. They are corpus drift: `80098e7` widened the
Modbus served window from 37 to 1024 registers and proved it on the controller, and the assertions
that count against the corpus were never updated. **The tools are right; the tests are stale.**

## Benchmarks

`bench-machine.ps1` measures a machine on the axes that matter for this repo; `bench-compare.ps1`
diffs two result files. See the comment-based help in each.
