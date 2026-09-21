# PORTFOLIO PUBLICATION PLAN — taking Ladder-AI public

**Opened 2026-09-17. Rev 2 — executable. Rev 3 — audited. Rev 4 — Phase 2 part-built. Rev 5 — builder
reviewed and repaired. Rev 6 — the verifier and the trial rewrite. Rev 7 — the verifier's tests.
Rev 8 (2026-09-18) — Phase 0 done, and Gate 3's build half made real. Rev 9 (2026-09-18) — F16–F19
discharged; the builder's default scan mode is tested. Rev 10 (2026-09-18) — F4, F9, F11, F13
discharged; the review backlog is closed. Rev 11 (2026-09-18) — FI-93 discharged; the red suite is
ratified, 19 → 2. Rev 12 (2026-09-18) — Phase 5 built: CI, the demo, and an exclusion
list that was wrong. Rev 13 (2026-09-18) — the rewrite ran and Gate 3 refused it.** Scouting pass and phase
plan for turning this repository into a public portfolio piece on GitHub, without the private
repository ever becoming public and without any restricted identifier reaching a public remote.

> 🔴 **THIS DOCUMENT DELIBERATELY DOES NOT NAME THE IDENTIFIERS IT IS ABOUT.**
> Same rule as [`data-boundary-audit-backlog.md`](data-boundary-audit-backlog.md): *a record of a
> leak must not be a copy of it.* **Actionability is preserved by giving the regenerating command
> instead of the list** — every worklist in this plan is reproducible on demand from
> `sanitization/*.map.json` and the job folders, both gitignored, both already canonical. Where this
> plan must refer to them it says **"the identifier set"** and gives the command.

**Revision note.**

*Rev 2* corrected three defects in rev 1: it missed that identifiers appear in **path names** as well
as file contents; it overstated the sanitization maps as a drop-in replacement list; and it carried
no effort estimates. Rev 1's `--replace-text` / `--replace-message` claim was **verified against the
installed tool** rather than asserted.

*Rev 3* is the output of an audit of rev 2 against the repository. Seven findings, one serious:

| # | finding | severity |
|---|---|---|
| A8 | **CamelCase word-boundary rules match none of the lower-hyphen directory forms.** Rev 2's "useful coupling" — that scrubbing prose repairs the path citations for free — was **false**, and believing it produces a scrub that reports success while 93 path names and 211 citations still carry the identifier. §3 rewritten; Phase 2 step 6, Gate 3 and the risk table all changed. | **serious** |
| A6 | SHA breakage both over- and under-stated. "~290 SHA-shaped tokens" was a regex population, not citations. Real figure: **253 real commits cited across 80 tracked markdown files**, only 29 of them in the stage-evidence docs rev 2 named. | material |
| A5 | Phase 5 claimed the demo corpus is untouched by the scrub. It returns zero identifier hits, **but 12 of its files match a map source key as a whole word** — safe only *if* the bare-word quarantine holds. Claim made conditional; a byte-comparison added to Gate 3. | material |
| A1 | Population (a) understated as 75 files — that was a four-term grep. Six terms give **131**. | material |
| A2 | "13 ADRs" → **12** (the 13th file is the template). | minor |
| A3 | "six full project audits" → **four** (six files; two are fixlists). | minor |
| A7 | "16+ assemblies" → **24 test projects**. | minor |

*Every count in this document has now been re-derived from the repository rather than carried
forward from an earlier draft.*

---

## WHERE THINGS STAND — 2026-09-17

**This section is the status of record.** The dated rev-notes below it are kept as the
account of *how* each thing was found, which is the part worth keeping; where they disagree with
this table, this table is current.

| phase | state | what remains |
|---|---|---|
| **0 — build baseline** | ✅ **DONE 2026-09-18** | SDK **8.0.425** installed; baseline captured to `sanitization/build-baseline.json` — **23 assemblies, now 5,894 passed / 2 failed** after rev 11's ratification (was 5,877 / 19). Gate 3's build half is now **mechanised, not merely recorded**: `--build-baseline` used to test `os.path.isfile` and compare nothing. Two targets still cannot build here and are recorded rather than hidden: `openness-cli` (no `Siemens.Engineering.dll`) and `Harness.RigRead` (no machine-local `Sharp7.dll`) |
| **1 — hygiene** | ✅ **DONE** | — |
| **2 — the scrub tooling** | ✅ **DONE 2026-09-18 — rev 14** | ✅ `verify-scrub.tests.py` **35 cases**. ✅ `capture-build-baseline.py` + 22 tests. ✅ **The review backlog is CLOSED** — all twelve findings discharged (rev 9, rev 10). `build-scrub-rules.tests.py` is **45 cases** after the declared-breadth gate. ✅ **`check-staged-identifiers.py` (M-5) is built** — 16 cases, six mutations, wired into `hooks/pre-commit`. **Nothing outstanding** |
| **2b — the red suite** | ✅ **DONE 2026-09-18** | **FI-93 discharged, rev 11.** 19 red tests → **2**. The 2 are one fact: `Main.ir` gained three networks and `Main.xml` was never re-exported; **one TIA re-export clears both** |
| **3 — rewrite history** | ✅ **DONE 2026-09-19 — rev 16, EARNED ZERO** | **Gate 3 returns an EARNED ZERO over 1,366 commits, 6,386 blobs and 1,830 paths.** T1 0 / T2 0, over-scrub 0, instrument control 1719/1719, must-survive 5 of 5, build **5,894 → 5,894 — not one test lost**. Demo 14 + 1 known; budgets 19/0 over; IR verified through `lad-coder` (29,752 lines and 21,673 literals matching as multisets). Verifier stamp `subject=1c2de19518e0`. **It refused twice first, and both refusals were right** — see rev 16. Source untouched, no remote. ⚠️ **That stamp is NOT the published one.** The mailmap was rewritten afterwards to map both author identities, which required a fresh cycle; the artifact that went public carries stamp **`bb6d61c6544d`** over 1,367 commits / 6,387 blobs / 1,830 paths. Same gates, same earned zero, one commit later |
| **4 — restructure** | ✅ **DONE** | — |
| **5 — CI and demo** | ✅ **DONE 2026-09-19 — Gate 4 GREEN, rev 17; both workflows green, rev 17b** | `.github/workflows/ci.yml` + `nightly.yml`, `global.json` (SDK pinned), `demo/run-demo.py`, `tests/ci-baseline.json`. CI has passed on **every** published sha. The step that matters is `capture-build-baseline.py --expect`, which fails on a lost assembly, a fallen pass count, a risen failure count *or* an unbuildable target that is not on the recorded list. **`Nightly (slow suites)` is now confirmed green on hosted hardware too** — the 66-case scrub-builder suite, kept out of CI at ~33 minutes. ⚠️ `cannotBuild` is **1**, not 2, since the Sharp7 decision (rev 17b); `openness-cli` is the sole exclusion and stays one until a licensed engineering seat exists |
| **6 — publish** | ✅ **DONE 2026-09-19 — rev 17, extended by revs 17a and 17b** | Published to `github.com/oisinsmyth/Ladder-AI`. **Currently at `e8eb66af38af`** after **four cycles** — first `bb6d61c6544d`, then `6362bbc5acfc`, then `65a09157e920` (🔴 the one forced push, rev 17b), then this one. In every cycle **remote `main` = local clone `HEAD` = Gate 3's verdict stamp**, and every cycle earned its own zero rather than inheriting one. Pushed private, CI confirmed green, then made public by the owner — the arrangement was chosen precisely so a mistake stayed recoverable. Steps 2 and 3 discharged: this repo still has **no remote at all**, and the publication is recorded in `data-boundary-audit-backlog.md` as an `AB-1` discharge **for the public artifact only** |

### What is actually built and proven

- **`build-scrub-rules.py`** — 23 tests; emits **119 rules + 22 path-renames** from a full-history
  run.
- **`verify-scrub.py`** — the Gate 3 oracle. Returns an **earned zero on a real `filter-repo`
  rewrite** while **still failing the unscrubbed source** (T1 10, T2 79). Its instrument control
  reports **1719 of 1719** needles matching themselves.
- **The rewrite recipe, proven — and TWO STEPS WERE MISSING FROM IT until rev 17a, both of which
  cost a full cycle:**
  1. **rebuild the rules** (`build-scrub-rules.py`, same `--green`/`--job-folder` as the manifest
     records) and **diff them against the previous set** — identical rules are what make the push a
     fast-forward instead of a public history rewrite;
  2. 🔴 **re-record the canary at the exact HEAD being published** — `verify-scrub.py --make-canary`.
     A canary from an older tree carries smaller must-survive counts, so it weakens the gate while
     still printing `ok`. Gate 3 refuses it as NOTHING EXAMINED, correctly;
  3. `clone --no-local --single-branch --branch <b>`;
  4. `filter-repo --replace-text --replace-message <renames…> --mailmap` **plus 🔴
     `--path-glob 'hmi/examples/*/build/*' --invert-paths`, which is NOT in the rule manifest
     because it is a filter-repo argument rather than a rule.** Reconstructing this command from the
     manifest alone silently produces a different history;
  5. rename the branch to the publishing name **after** filter-repo (renaming first breaks its
     reflog freshness check), then `reflog expire --all --expire=now && gc --prune=now`;
  6. **check the published commit is an ancestor of the new HEAD before pushing** — and push without
     `--force`, so git refuses too;
  7. Gate 3 → stamp must equal the clone's `git rev-parse HEAD` → identity sweep → push.

  `git-filter-repo` is a pip package here and its `Scripts/` directory is **not** on the shell PATH:
  `git filter-repo` fails with *"not a git command"*. Invoke the exe by absolute path. ~7 min rewrite, ~30 s verify.

### Rev 17b — cycles THREE and FOUR: a leak that was suppressing its own rewrite rule

**Four cycles now, and the stamps are the spine of the record.** In every one, remote `main` = the
clone's `HEAD` = Gate 3's verdict stamp. That identity is the claim; everything else is how it was
earned.

| # | date | source HEAD | published stamp | rules | push |
|---|---|---|---|---|---|
| 1 | 09-19 | `8a9db57` | `bb6d61c6544d` | 115 | new branch |
| 2 | 09-19 | `e50265b` | `6362bbc5acfc` | 115 | fast-forward |
| 3 | 09-19 | `38ce54e` | `65a09157e920` | **116** | 🔴 **forced** |
| 4 | 09-20 | `967a1c0` | `e8eb66af38af` | 116 | fast-forward |

#### 🔴 Cycle 3 — the rule set moved, and the reason is the best finding in this document

`M-22` found a live job code in `gen/test-project001`, a corpus `CLAUDE.md` calls "Green-tier
throughout". Sanitizing it also removed a live **site-block name** from that corpus. Then this
happened:

> **The builder withholds a rewrite rule for any term present in Green content — repo-wide.**
> So the leak had been **suppressing its own rewrite rule everywhere.** Removing it re-armed the
> rule: 115 → 116.

Corroborated independently by the canary: `tier3Baseline` 341 → 340, one term moving out of
*conventional* and into *hunted*. **What was public until that moment carried a live name the scrub
should have been rewriting**, which is why the force-push replaced a worse artifact rather than
merely a different one.

**The divergence was predicted before it was observed, then confirmed.** The rewrite is a pure
function of (history, rules), so a changed rule set moves every SHA from the term's first appearance
(2026-07-12) onward. The ancestry gate was switched from *refuse* to *report* for this one cycle
only — and still refused an **unexpected** shape, because "the history changed" and "the history
changed the way I predicted" are different claims.

**Authorised on measured facts, not on convenience:** 0 forks, 0 stars, 0 watchers, 0 issues, repo
two hours old. Pushed with `--force-with-lease` so git would still refuse if the remote had moved.
Gate 3 returned its own earned zero first — 6,409 blobs, 1,373 commits, `cannotBuild` 2 before /
2 after — and the identity sweep 14,612 objects, 0 employer-domain occurrences, 1 author.

#### Cycle 4 — rules stable, so the push went back to being boring

Rules rebuilt **byte-identical**, confirming the 115 → 116 move was caused specifically by editing
Green-corpus content and nothing else. Fast-forward, 2 commits on top, 1,375 total. Gate 3 earned
zero over 6,417 blobs; identity sweep 14,634 objects, 0, 1.

**It carried the Sharp7 decision, and `cannotBuild` fell 2 → 1.** `harness.sln` now builds on a
hosted runner. ⚠️ The version number hides a **binary swap** — the 1.1.82 package ships 44,544 bytes,
the DLL it replaced was 57,288 — so **no existing rig measurement transfers**. Recorded in
`Directory.Packages.props`, in `ci.yml`, and in the commit.

**Two near-misses, both caught by tooling rather than by judgement:**

- **The first baseline re-capture was contaminated by my own build.** Having hand-built
  `harness.sln` first, MSBuild had nothing to do for part of the tree, and
  `capture-build-baseline.py` correctly excluded what it had not seen built — silently dropping
  `DeviceGuard.Tests` and 65 tests. Its stale-`bin` guard is the only reason a smaller, greener
  baseline was not committed. Re-captured clean: 23 assemblies, 5,894 passed, nothing lost.
- 🔴 **The obvious fix for that would have destroyed something irreplaceable.** Wiping every `bin/`
  would have deleted `src/openness-cli/**/bin/Siemens.Engineering.dll` — six copies of a licensed
  assembly that exist **only** as copy-local build output here, because no TIA install on this
  machine can produce another. The clean excluded that tree, and the deletion set was checked for
  Siemens DLLs before it ran.

#### Both workflows are now green on real hardware

`CI` and `Nightly (slow suites)` both pass on `e8eb66af38af`. The nightly is the **66-case
scrub-builder suite**, deliberately kept out of CI at ~33 minutes — this is the first observed
confirmation it runs green on a hosted runner, and the stale step name corrected in rev 17a is what
made its result legible.

#### What else these cycles carried

`M-22` and its tracked register; the `AB-2` sanitization; the Sharp7 decision; and the **3-B audit**
of the blind-validation run, whose verdict is **UNVERIFIED — neither cleared nor impeached** and
whose durable fix is `M-23`. All recorded in `data-boundary-audit-backlog.md` and
`mechanisation-backlog.md`; not repeated here.

### Rev 17a — the SECOND cycle, and the two things that tried to go wrong

**`6362bbc5acfc`, a FAST-FORWARD over the published commit.** Four source commits (the `.gitignore`
gap, the Node 24 action pins, and the `AB-2` / `M-22` documents) reached GitHub by re-running the
whole cycle — the only route, because this repository's history carries the live vocabulary and the
employer identities, which is why it has no remote.

```
push           bb6d61c..6362bbc      (no "+" — a fast-forward, not a forced update)
remote main    6362bbc5acfc2773662ffb34cb94cc925e89a715
verdict stamp  6362bbc5acfc
CI             green, 11 steps, 0 annotations
```

**Zero annotations is the confirmation that the Node 24 pins worked** — the previous run's only
annotation was the Node 20 deprecation, and it is gone.

#### 🔴 Finding 1 — the invocation was reconstructed from the wrong artifact

I rebuilt the `filter-repo` command from `sanitization/scrub/manifest.json`. **The manifest records
the RULES; the path exclusion is a filter-repo ARGUMENT**, so it is not in there and cannot be. The
result reproduced the published history byte-for-byte to commit **1014** and diverged at **1015**.

Comparing every path that ever existed in each rewrite: **54 files in three directories** present in
mine and absent from the published one, and **nothing** present in the published one missing from
mine. 39 + 8 + 7 = 54 — exactly the count Phase 1 recorded, and the requirement is written in red in
Phase 1's own result note. Adding `--path-glob 'hmi/examples/*/build/*' --invert-paths` closed it.

**The ancestry gate is what caught this**, and it is worth keeping: it refuses to push unless the
already-published commit comes out as an ancestor, so the failure mode was a refusal rather than a
force-push over a public repository.

#### 🔴 Finding 2 — a stale canary is a WEAKENING bug, not a bookkeeping one

Gate 3 exited **2 — NOTHING EXAMINED**: the canary was recorded against `8a9db57` and the clone was
rewritten from `e50265b`. Every substantive number was already green, which is precisely the trap.
Re-recording raised **every** must-survive threshold:

```
C-001                       2893 -> 2895      converter preflight   1125 -> 1131
NOTHING EXAMINED            1862 -> 1866      docs/06-lad-...md      749 ->  759
EMPTY IS NOT CLEAN          3005 -> 3010
```

An older tree contains **fewer** occurrences of everything, so a stale canary carries **smaller**
expected counts and the gate gets quietly weaker the staler it is — while still printing `ok` on
all five. must-vanish (410/52) and the T3 denominator (341) were unchanged, as they should be with
no new vocabulary.

#### The by-product worth more than either finding

The first 1,367 commits rewrote to **byte-identical SHAs**. The pipeline's determinism had been
assumed; it is now measured, and it is what makes a fast-forward possible at all.

#### One measurement, made while deciding whether to watch CI from here

On a **public** repo, anonymously: run conclusion ✅, per-step conclusions ✅, annotations ✅ — **raw
logs ❌ (API 403), and the web UI says "Sign in to view logs".** The case where the logs matter is
exactly the case where anonymous access stops working, so publishing to gain observability buys a
status icon and not a diagnosis.

### Rev 17 — ✅ PUBLISHED. Gate 4 green on the first run, and the thing that was checked is the thing that shipped

**`github.com/oisinsmyth/Ladder-AI`, commit `bb6d61c6544d7096e62fb8459a66b2bfde648db4`.**

```
remote main    bb6d61c6544d7096e62fb8459a66b2bfde648db4
local  HEAD    bb6d61c6544d7096e62fb8459a66b2bfde648db4
verdict stamp  bb6d61c6544d
```

**All three agree, and that identity is the whole point.** A verifier that passes a rewrite and a
push that publishes *a* rewrite are two facts about two artifacts unless something ties them
together. Gate 3's stamp is the subject it examined; it is byte-identical to what is public.

**The sequence, and why it was this way round.** Pushed to a **private** repo → CI ran → confirmed
green → owner flipped it public. Chosen deliberately over publishing first and watching CI on the
public repo: the only arrangement where a mistake is still recoverable. **The visibility flip was
the owner's to make and was never automated.**

**Gate 4, first run, on real GitHub hardware:** 1 job, 11 steps, all success, 9m 27s. The step that
carries the gate is `capture-build-baseline.py --expect tests/ci-baseline.json`, which fails on a
lost assembly, a fallen pass count, a risen failure count *or* a third unbuildable target — the two
known failures and two unbuildable targets run every time and are tolerated **only because the
baseline records exactly two of each**. One annotation, and it was infrastructure rather than code:
Node 20 deprecation on four actions.

**A measurement worth keeping, made while deciding whether to watch CI from here.** On a *public*
repo, anonymously: run conclusion ✅, per-job and per-step conclusions ✅, annotations ✅ —
**raw logs ❌ (API 403), and the web UI says "Sign in to view logs"**. So going public would have
bought the ability to see *that* something failed and never *why*. **The case where you need the
logs is exactly the case where anonymous access stops working**, which is an argument against
publishing in order to gain observability.

**The Node 20 fix, and the one-major trap.** Rather than bumping to latest, each action's
`runs.using` was read at each tag. Minimum Node-24 majors: checkout v5, setup-dotnet v5,
setup-python v6 — and **upload-artifact v6, because v5 is still Node 20.** A habitual one-major bump
would have left the warning in place and looked like a fix. Latest was v7/v6/v7/v7 and was
deliberately not taken: extra majors are behaviour changes this workflow would have to re-earn.
⚠️ **These edits are in the source repo and are NOT in the published artifact** — reaching it needs
another rewrite cycle.

**Open item 9 was closed and grew four times in the closing.** It recorded one false Green claim in
one file. Measuring it properly found the same assertion about **four** directories — including
`docs/evidence`, 13 of 24 tracked files, 36 needles, 4 declared — all of them made by
`docs/13-data-boundary.md`'s own approvals. **Nothing leaks:** Gate 3 returns T1 0 over exactly this
content. The defect is the label, and a false *already sanitised* label is what licenses a copy into
somewhere the scrub does not run. Now **AB-2**, with **M-22** as the fix: the builder's `--green`
list has been a machine-readable tier register all along, none of the four was ever on it, and
nothing ever compared the register to the prose.

**🔴 M-5 REFUSED THE COMMIT THAT RECORDED ALL THIS, AND IT WAS RIGHT.** Writing `AB-2` up meant
naming the directories, and two of them are **named after the live block they were derived from** —
so an 11-character inferred identifier went into the audit backlog inside a directory path. That
file's own header predicts this in its second paragraph: *"writing them down here would commit the
very strings the finding is about, into a new committed file, in the name of recording that they
are committed."* **A finding about false sanitisation labels, leaking while being recorded.** The
two rows are now described by role, with the exact paths left in the Amber-access record where they
already live. Triaged without printing the term — its length, tier and containing string were
enough to pick the remedy, which is what `--name-terms` being off by default is for.

**Also found while checking this:** `.gitignore` covered `scratch/`, `scratch-*/`, `.lane-*/` and
`.scratch-*/` — and not `.scratch/`, which existed, holding an `.ir` file and real tool output. Two
comments in that file describe this exact lesson, each written by someone who had just been bitten,
and the base case was still the gap. Fixed.

### Rev 16 — ✅ PHASE 3 IS DONE. Gate 3 returns an EARNED ZERO on a complete artifact

```
T1 DECLARED      0        T3 over-scrub     0 fell        demo      14 + 1 known
T2 whole-token   0        must-survive      5 of 5 ok     budgets   19, 0 over
instrument control  1719 of 1719            build     23 assemblies, 5894 -> 5894
```

1,366 commits, 6,386 blobs, 1,830 paths. **Not one test lost.** Verifier stamp
`subject=1c2de19518e0 objects=14554`; the source repo is untouched at `600d664` with no remote.

**It refused twice before it passed, and both refusals were right.** The first found 8 broken tests
and 2 declared residuals; the second found 2 conventional names wiped. Everything below came out of
those two refusals.

**Five defects in the builder, none visible to the identifier half:**

| defect | how it showed |
|---|---|
| Substitution stopped at whole strings, so one live name left as two | 5 disagreements, 8 broken tests |
| The length floor withheld heads a dotted rule was already renaming | 48 divergences (**reverted** — see below) |
| A dotted rule renamed members nothing else renamed | the `IO`/`IOSignals` break |
| `mustNotAppear` gated on presence | Gate 3 **structurally unpassable** on its own repository |
| A replacement can land on a name the corpus already uses | two FBs under one name |

🔴 **THE FLOOR EXEMPTION WAS MY MISREADING, AND THE OVER-SCRUB DETECTOR CAUGHT IT.**
`MIN_GLOBAL_LENGTH = 8` and the verifier's `len(low) < 8 → T3` are **the same threshold**: a short
map key is neither scrubbed nor hunted, and the two tools agree by design. I read one half of that
symmetry as an oversight and closed a "leak" that was not one. Cost: two conventional names wiped,
one from **6,681 occurrences to zero**. The detector had never fired before. Reverted; the case that
asserted the exemption is **inverted rather than deleted**, because the argument for it will read
just as well the next time somebody has it.

**Three checks had to learn the same lesson — state versus change.** `mustNotAppear` gated on
presence rather than a rise. M-5 gated on what a file *contained* rather than what a commit
*introduced*, and refused a three-line fix over six needles that had been there for months. The
merge gate reported three merges where there was one. Same shape, same cost, three different tools:
**a refusal nobody can act on is how a gate gets switched off.**

**What no string gate could see.** A `lad-coder` verification of the rewritten IR — run on *both*
corpora and compared, because "a result from the clone alone proves nothing" — confirmed 139 files
parse identically, 124 still convert, 29,752 IR lines and 21,673 literals match as multisets, and
`MEMBER-NOT-FOUND: 0` against 190 resolved tag paths. It also found two FBs about to share one name.
Every string involved was correct; **what was lost was a distinction, and a distinction is not a
string.**

`lad-reader` then established by **byte-identity** (`md5 9d8dca41…`) that the bench block *is* the
answer key of a blind-validation case and the other file is the pipeline's generated attempt at it —
the same FB. My gate had read differing block numbers and fingerprints as "structurally different",
which an independent reimplementation of one specification produces **whatever the answer**: it asked
a real question and answered it from a signal with no bearing on it. Recorded in
`sanitization/accepted-merges.txt` with the evidence, printed on every run.

**Three tests were edited to suit the transform, and that is said out loud rather than buried.**
SimaticML stores tag paths componentised; the assertions hard-coded the joined form. The alternative
was 49 T2 residuals — measured, not argued.

🔴 **A TIER CLAIM THAT DOES NOT MATCH ITS CONTENTS.** `gen/_validation/MotorVSDSystem-purpose/spec.md`
asserts Green-tier, invented-names-only. Measured: **3 of its 10 files carry live vocabulary**, and
`ir/PlantAutoControl-bench` — which its answer key is a byte copy of — carries **14 distinct needles
across 2 files, 2 of them declared site terms**. Neither directory is in the builder's `--green`
list, so the *tooling* has never believed the claim; only the document does. Nothing leaks, because
Gate 3 returns T1 0 over exactly this content. The risk is that a false "already sanitised" label is
what lets content be copied somewhere the scrub does not run. **Open — see item 9.**

### Rev 15 — Gate 2 is closed, and the builder exits 0

**All three decisions from Rev 14 are applied**, on evidence rather than judgement, and
`build-scrub-rules.py` now emits: **115 rules, 22 `--path-rename` pairs, 0 rows editing source from
inside a token.** The rule that took three solutions from 5,894 passing tests to 2,196 is gone from
the artifact — nothing now rewrites inside a member access. The decisions and their reasons are in
**Open item 8**.

**Row (c) was not what the finding said it was.** `--explain-cuts` printed 11 candidate forms: one
real, and **ten base64 blobs** in `hmi/comparison.html` and `hmi/brand-comparison.html` that happen
to contain the term's three letters. The one real form already contained a **4-character** term that
has its own row — and since rules are emitted longest-first, that row rewrites it first. The
3-character row was redundant, not under-specified.

🔴 **WHICH MEANS THE CHECK'S STATED REASON WAS WRONG, AND THE FIX IS THE POINT OF THIS REV.** The
CUTS check measured each needle against the untouched corpus, so it named one build-source token as
collateral that the longer rule had already made unreachable. Right verdict, wrong reason — and a
gate that overstates its reason gets argued with, then disbelieved on the day it is right. The
collateral computation now runs **after** filtering, against the finished needle set, rewriting each
candidate token with every longer emitted rule before looking for the shorter needle in what
remains. 52 cases.

**Three guards refused this work before it succeeded, and none of the three came from reading code:**

| refusal | what it caught |
|---|---|
| "expected exactly one row" | An invented replacement is **not unique** — two rows share one, being two spellings of a single site. The finding said `term row 'X'` and pointed at both. Findings now carry class and length, which disambiguate and disclose nothing |
| "the sentinel is not absent" | The absent form I chose occurred **twice in the tree**, because I had committed it in a test fixture an hour earlier. A test string cannot double as production data |
| variant-collision check | Both discharged rows given the **same** sentinel claimed one written form with two replacements. `claims` is built before dead variants are dropped, so an absent form collides exactly like a present one |

**Phase 3 is unblocked.** The rewrite is a pure function of (source, rules) and re-runs in about
seven minutes.

### Rev 14 — Phase 2 closes, and the Phase 3 blocker shrinks from twelve rows to three decisions

**Everything here was done without the owner, which was the brief.** Nothing below decides a term
row; the point of it is to turn rev 13's prose blocker into a refusal the builder issues itself,
with the evidence attached.

**1. The builder could not see the fault that refused the artifact.** `rule_line` emits a declared
term as `(?i)<term>` with no word boundary, decided by declaredness and nothing else — right, and
four real terms need it. But the filter loop gives declared terms an unconditional `continue`
**before the length floor, the Green test and the breadth report**, so they were the one population
reaching the widest matcher in the tool having been measured by none of its breadth tests. It now
measures them and refuses.

🔴 **REV 13 OVERSTATED THE BLOCKER AND THIS CORRECTS IT.** It reported *"12 rows, 840 substitutions
across 37 files, breaks the build"*, treating one figure as the cause of the other. Re-measured: the
841 substitutions across 38 files are real and **nearly all land in `.ir`, `.xml` and `.md`**, where
an anchorless rewrite is exactly the job. **Only one of the twelve touches build source at all.**

**The discriminator, and what the obvious ones cost.** Length does not separate the populations — a
3-character term had 12 enclosing tokens and a legitimate 5-character job code had 13. The Green
corpus is blind here: `--green` defaults to the reference TIA project and does not cover `src/`.
Dottedness catches only the sharpest case. What separates them is whether the term **is ever a
source token in its own right**: if it is, its rule is doing real work and embedded hits ride along;
if it never is, every edit it makes in source is to the inside of somebody else's identifier, and
**no amount of that hides a job code — the symbol being cut was not the secret.**

**2. The evidence for the Gate 2 decision**, swept across all 1,810 tracked files at HEAD. Rows are
named by their **invented** replacement, which is what the term list's second column says; no live
term appears.

| row | class | len | whole-token files | embedded-only | in build source | verdict |
|---|---|---|---|---|---|---|
| `K5` | modelline | 2 | **0** | 3 | 0 | never a whole token |
| `K7` | modelline | 2 | **0** | 3 | 0 | never a whole token |
| `KS` | site | 3 (dotted) | **0** | 4 | **4** | 🔴 **GATES** |
| `K15` | modelline | 3 | **0** | 14 | 1 | 🔴 **GATES** |
| `K75` | modelline | 3 | **0** | 2 | 0 | never a whole token |
| `K150` | modelline | 4 | 11 | 0 | 0 | present in its own right |
| `JOB9001` | jobcode | 5 | 10 | 11 | 0 | present in its own right |
| `JOB9002` | jobcode | 5 | 60 | 9 | 7 | present in its own right |
| `JOB9003` | jobcode | 5 | 15 | 0 | 0 | present in its own right |
| `JOB9004` | jobcode | 5 | 33 | 6 | 1 | present in its own right |

**Not one term of 2–3 characters appears as a whole token anywhere in the repository.** They can
only ever edit the insides of longer words. For `K5`, `K7` and `K75` that is plausibly the A8 case
working as intended — a model designation inside a longer product name, in `.ir` and prose. For `KS`
and `K15` it reaches build source, and the fix is a `variants` cell naming the longer forms that
*should* be rewritten. **The `variants` column already exists and already parses** (`| live |
invented | class | scope | variants |`, `;`-separated); a forced list **replaces** the auto-derived
one, which is exactly how a dangerous bare form stops being emitted. Nothing needs building for the
owner to act.

**3. A third finding, and nothing was hunting for it.** While assembling the table above, the `site`
row printed `None` in its invented column. Two rows declare the same 5-character term — as `site`
and as `jobcode` — with different replacements, and `chosen[r["live"]] = r["invented"]` is a plain
assignment in row order. **F9 again, one line earlier, in a different dict.** Today the lucky row is
last, so the term becomes `JOB9003`. **Reorder the table and a site name becomes the literal token
`None` throughout the corpus** — in prose, in `.ir` tag names, everywhere — with nothing printed and
exit 0. It now gates, case-insensitively because the emitted rule is `(?i)`. Agreeing duplicates are
reported rather than refused; the list has one of those too.

**4. Phase 2 is closed: `check-staged-identifiers.py` (M-5) is built** — 16 cases, six mutations each
reddening the case named for it, installed as the third block of `hooks/pre-commit`. Its subject is
the **index**, not the working tree.

🔴 **ITS FIRST REAL RUN REFUSED ITS OWN COMMIT, AND IT WAS RIGHT.** *This document* carried a live
three-character declared term, quoted verbatim inside a fenced code block, as the worked example of
what broke the build in Phase 3 — written eight commits earlier, in the document that explains the
de-identification, while describing the rule that was rewriting that very term. It did not read as
job content; it read as evidence. That is **verbatim the failure M-5 was filed for**, and the
strongest available argument that the control had to be mechanical is that its author leaked while
building it. The passage above is rewritten with an invented needle.

One mutation was a **real bug found by its own case**: the corroboration that makes *"no staged
paths"* safe to call clean asked `git diff --cached --quiet` while the path list asked
`--diff-filter=ACMR`, so a commit that only **deleted** files was refused. A corroborating question
asked in a different scope does not corroborate — it invents disagreement, and it invented it in the
direction that refuses correct work.

**5. `src/hmi-cli` has a solution** — and the `Directory.Build.props` that subtree was the only one
in `src/` to lack, so `HmiCli.Tests` had been compiling **without `TreatWarningsAsErrors`** while the
code it tests had it. 0 warnings, 161 passing before and after, full `--expect` capture green at 23
assemblies / 5,894 passed. The orphan hunt stays: an orphan is made by forgetting, not by deciding.

**Where that leaves Phase 3.** Still blocked, still on Gate 2, and now **three named decisions**
instead of a paragraph — the duplicate row, `KS`, `K15`. After them the rewrite re-runs in about
seven minutes. `build-scrub-rules.tests.py` is **48 cases**.

### Rev 13 — the rewrite ran, and Gate 3 refused the artifact

**The rewrite executed end to end.** 1,346 commits in 234 s on a `--no-local --single-branch` clone,
finished with `reflog expire && gc --prune=now`, branch renamed to `main`, no remote. **All 1,346
author identities are now `@users.noreply.github.com`** — the employer domain is gone from every
commit, which is the single largest identifier in the repository.

**The de-identification itself is clean.** T1 declared **0**, T2 inferred whole-token **0**,
instrument control **1,719 of 1,719**, all five must-survive controls intact. `unsearchableBlobs`
fell **15 → 7** on the path exclusion.

🔴 **AND GATE 3 REFUSED IT ANYWAY, ON THE BUILD HALF — the half that was decorative until rev 8.**
Eight assemblies absent, two targets newly unbuildable, `Harness.Map.Tests` 451 → 442 with 9 new
failures. **5,894 passing became 2,196.** Had the build comparison still been `os.path.isfile`, this
artifact would have passed every identifier check and been publishable.

**The cause: declared term rows emitting anchorless, case-insensitive rules short enough to match
ordinary text.** Declared rows bypass the length floor **by design** — finding F1 established that,
because job codes are five characters and applying the floor discarded 15 of 19 term rows. That
bypass is right for a distinctive five-character job code and wrong for a two- or three-character
fragment.

🔴 **REV 14 CORRECTS THE SCOPE THIS PARAGRAPH ORIGINALLY CLAIMED.** It said twelve rows made 840
substitutions across 37 files and broke the build, presenting one figure as the cause of the other.
Re-measured: the 841 substitutions across 38 files are real, and nearly all of them land in `.ir`,
`.xml` and `.md`, where an anchorless rewrite is precisely the job. **Only one of the twelve touches
build source at all.** The builder now refuses that population by measurement rather than by length,
and it names **two rows**, not twelve.

One rule is what broke the build: three characters, dotted, anchorless. It matched across a token
boundary in ordinary C# and collapsed a property access into a single identifier —

```
ab.SampleKeys   ->   KSampleKeys
```

— after which `CS0103` followed in three solutions.

🔴 **THE NEEDLE ABOVE IS INVENTED** (`ab.S`, standing in for a real three-character declared
term). The original text of this passage quoted the live one, and
`tools/check-staged-identifiers.py` caught it on its first real run against this repository. That is
exactly the failure M-5 documents: a job identifier written into a document as evidence for a
technical claim, because that does not feel like job content, it feels like rigour. It was written
by the same author who then built the check, which is the strongest argument available that the
control had to be mechanical.

**This is A8 in a third guise.** A8 was "the rule matches the wrong surface"; the quarantine that
answers it — the 368 bare words, Phase 2 step 4 — filters **inferred map keys only**. A declared term
passes the floor, the Green test and the breadth demotion untouched, so nothing in the pipeline
looked at these twelve at all.

**Stopped here, and this is Gate 2's business rather than a repair to make quietly.** *"The owner
reviews and signs off the replacement list, item by item, before it touches anything."* Twelve rows
of the term list cannot be applied as bare anchorless substrings; the `variants` column exists for
exactly this, and which form each row should take is the owner's call, not a tooling decision.

**The systemic fix is a builder gate**: a declared term short enough to match ordinary text, emitted
anchorless, should be a **blocking finding** unless the row supplies explicit variants. The builder
currently emits it without comment — the one class of rule it never questions.

Two false positives in the same output, recorded so the next reader does not chase them: `QQSCRUBQQ`
and `***REMOVED***` are reported as must-not-appear, and both are the **tooling's own documentation
quoting those sentinels**. The same check reports them on the unscrubbed source too (3 and 6 there,
12 and 27 now, the growth being this session's documentation).

### Rev 12 — Phase 5 built: CI, the demo, and an exclusion list that was wrong

🔴 **The exclusion list in Phase 5 step 2 was built from a grep and never verified.** It named
`Harness.Device`, `Harness.Map` and `WaveControl.Cli` as TIA-gated. All three match
`Siemens.Engineering` **only inside comments asserting they deliberately do not reference it** —
`Harness.Map`'s reads *"no packages, no project references, no network, no Portal, no
Siemens.Engineering."* Excluding them would have dropped **586 passing tests** for no reason. The
real gated set is `src/openness-cli/` (licensed DLL) and `Harness.RigRead` (machine-local Sharp7,
vendoring left open by ruling).

**Three traps that would have made the first CI run red for reasons unrelated to the code.**
`TreatWarningsAsErrors=true` in all seven solution trees with **no `global.json`** — a runner on a
newer SDK turns one new analyzer warning into a build failure, so the SDK is now pinned to the
8.0.425 every recorded figure was measured on. `harness.sln` **does not contain `Converter.csproj`**
while two of its test projects spawn `converter.exe` from disk and call its absence *"A FAILURE, NOT
A REASON TO SKIP"* — so CI builds the converter first. And `ToolPaths.cs` defaults to `bin/Debug`, so
a Release-only build must set `CONVERTER_EXE`.

**The mechanism for the two known failures changed, and the reason is measured.** The ruling was to
exclude them by fully-qualified name. But the failing case is **one parameter of a `[Theory]`**, so a
name filter matches the method — it would have hidden **16 passing round-trips to suppress 1
failure**, which is the `continue-on-error` outcome the same ruling rejected. Instead nothing is
filtered: every test runs, and `capture-build-baseline.py --expect` compares the result against
`tests/ci-baseline.json`. The baseline records exactly two failures, so a **third fails the build**.

**CI's whole test step is one command**, and not a loop over `*.sln` — that loop would skip
`src/hmi-cli`'s 161 tests in silence, which is the trap this plan flagged at rev 8 and which the
capture tool was already built to avoid.

🔴 **A vacuous test, found by mutation again.** `a MISSING baseline is exit 2` asserted only the exit
code, and its fixture never produced an assembly — so the run exited 2 at *"NOTHING EXAMINED"* and
never reached the check at all. Disabling the guard turned 0 of 22 red. `--expect` is now validated
**before** the build, which makes the case reachable and also means a typo refuses in a second rather
than after five minutes.

**The demo is `demo/run-demo.py`**, on the Green-tier `ir/reference` corpus: one block through six
commands, then the whole corpus round-tripped. It reports **14 of 15**, and the exception is the point
— `NodeStatusAlarms` is a seed artifact whose *answer key* is missing an `<Interface>`, so the single
difference is an addition of TIA's own defaults. `--check` mode asserts that result, so the demo fails
CI rather than rotting into an example nobody runs. It states on every run what it did not show:
nothing here goes through TIA, so import and compile behaviour is uncovered.

### Rev 11 — the red suite ratified: 19 → 2

`FI-93` recorded nine red tests and asked for a ruling rather than a repair: *"re-pointing them
ratifies the widening; leaving them red does not preserve the old value, it only stops anyone finding
out."* Ruled: the widened corpus **is** the intended long-term reference. Converter **1820/0**,
harness **3121/0**, golden **204/2**, baseline **5,894 / 2**.

**FI-93 was wrong three ways, each corrected by measurement.** There were **two** causes, not one —
`eba7033` added three code blocks the same day as the Modbus widening, and that is what moved every
census number. It was **nineteen**, not nine or fifteen — the four in `GoldenHarness.Tests` were
recorded in no FI item, note or gate and surfaced only because `capture-build-baseline.py` measures
all 23 assemblies at once. And its prescribed fix — *"37 → 1024 and the derived counts"* — **would
have damaged two tests.**

🔴 **The rule that mattered: FIX THE STALE INPUTS, NOT THE OBSERVED OUTPUTS.** `NeighbourTests`
expects 26 and reported 27, and the 26 was *correct*: the 27th was the comms block's own area pointer
falling out of `AreaDeclarations` because a helper constant still fed in 37. Raising it would have
asserted that an area's own declaration counts as an occupant of itself — the single thing
`IsDeclarationOf` exists to prevent.

**Two tests had predicted their own failure and were right.** The cyclic-OB test asserted
`DoesNotContain("COMMENT ")` so the day the corpus gained a header comment would be "a failure with a
reason instead of a confusing diff"; it came, the gap closed in the good direction, and the splice is
gone. And `EveryDeclaredRegister_IsCoveredByExactlyOneTag` said an unmapped register should be "a
decision somebody made rather than a surprise on the screen" — it was, recorded in the block itself,
so it now asserts declared 1024 / maintained 37 / exactly 987 unmapped.

**The 2 that remain need Portal, not editing.** `Main.ir` gained three networks; `Main.xml` was never
re-exported. One re-export clears both. Both escape hatches are blocked by design, correctly.

### Rev 10 — F4, F9, F11, F13 discharged: four defects that changed nothing

The last four review findings, and the review backlog is now closed. Each changes what the builder
*emits*, so each was expected to move the artifact. **None of them did** — and every one has a
measured reason why not rather than a shrug:

- **F13** — 20 duplicate-stem pairs exist, but only 3 keys have competing replacements and **rung 3
  decides none of them**.
- **F11** — 14 keys gain a section class, 4 would gain a spaced variant, and **all four are already
  in the owner's term list** with a spaced class. The corpus happens not to expose the defect.
- **F9** — **5 collisions**, every one the harmless same-replacement shape.
- **F4** — **0 overlaps**. The artifact really was clean; it is now clean *by the check* rather than
  by luck.

🔴 **F9's recorded "5 live instances" was a number nobody kept the derivation of** — no run log, no
manifest field, nothing in git. Counting claimants at the collision site gives **exactly 5**, and
the method now lives in the tool and prints on every run. The figure was right; it had simply
stopped being checkable.

**The serious one was F9, and not for the reason its one-line summary gives.** `setdefault` kept
whichever key sorted first and discarded the rest in silence. A losing key got the wrong replacement
or **no rule at all** while being counted in no tally — the run's arithmetic balanced perfectly with
a key missing. And a map key could squat a **declared** term's written form, which then lost its
anchorless case-insensitive rule: **F1 reintroduced through a path neither of F1's guards covers.**

**The trap in F11.** `klass_of` does not mean "this key's class", it means *the owner wrote this key
down*, and three other decisions read it that way. Folding map keys into it would have promoted
~1,390 inferred keys to declared status — bypassing the length floor and the Green test — a far
larger change than the fix, wearing the costume of a one-line edit. A separate dict carries the
section class and a case asserts the promotion did not happen.

**What F4's substitution cannot do**, stated because it decides whether a run gates. An *equality*
overlap against an inferred needle is fixable by suffixing. A *substring* overlap, or any overlap
against a declared needle, is not: a declared rule is anchorless, and **no suffix removes a
substring from a string**. Those gate and say to choose a replacement by hand, which is honest
rather than a substitution that does not work.

**`resolve_conflict` had never run past its first line.** Every fixture built a single map, so it
returned rung 0 immediately and rungs 1–4 were unexecuted — which is how F13 sat in rung 3
undisturbed. **Rung 2 was left alone deliberately**: it reads like a type confusion and is not, since
**41 of 43 map stems are themselves keys** and the live run shows `rung2=1`.

🔴 **A defect in the test harness, found by a mutation that made the suite VANISH rather than turn
red.** `case()` caught only `AssertionError`, so an unexpected exception killed the run mid-way and
printed no RESULT line at all — a harness unable to tell "one case failed" from "the run died".

Sixteen mutations, every one caught by the case named for it, restoring byte-identical. Suite is
**41 cases**.

### Rev 9 — F16–F19 discharged: the builder's default mode was tested by nothing

🔴 **Every one of the builder's 23 cases ran `--scan head`. The default is `--scan history`, and it
is the mode that produced the published artifact** — 6,413 blobs, 119 rules. One word inside the
shared runner did it, and an override nobody can see at the call site is the kind that survives a
review. `run()` no longer injects a mode; each case names its own.

**The fixture could not have expressed the difference either.** `build()` makes exactly one commit,
so the object database and HEAD held identical blobs — a `--scan history` case over that fixture
would have passed while proving nothing. The new `bury()` helper is the exact inverse of the
verifier suite's `scrub()`: that one amends and prunes so the old blob is *gone*, this one commits
again so it *remains* in the ODB while absent from HEAD.

Seven history cases now exist. The load-bearing one is **one repository, two modes, opposite
verdicts**: head sees a clean tree and refuses with exit 2, history finds the buried identifier and
emits a rule — and the case asserts the *disagreement*, because if the modes ever agree the fixture
has stopped exercising history and every case beside it is worthless.

**Two defects in the history path, found by planning the tests rather than by running them.** The
EMPTY-IS-NOT-CLEAN guard **could not fire**: the path corpus was chained in with the blobs and is a
`str`, never `None`, so `searched >= 1` unconditionally and `if not searched:` was unreachable dead
code — a repository whose history holds not one blob reported "blobs scanned: 1" and carried on.
And `proc.wait()` discarded git's exit status, so a `cat-file` that failed outright handed back a
perfectly clean, perfectly empty corpus. In the tool whose whole doctrine is that empty is not
clean, on the one path no test entered.

**The vacuous assertions were derived, not guessed.** The five were never enumerated anywhere, so
rather than pick five that looked weak, each behaviour a case claims was broken in turn and the
suite watched for which case kept printing PASS. That found `path-renames.args` **checked by
nothing at all** (suppressing the whole file: 0 of 30 red — the artifact whose absence was F3);
`emits_three_files` passing four **empty** files; `longest_first_ordering_holds` discarding the exit
code and then asserting `[] == sorted([])`; `every_line_carries_an_explicit_arrow` sleeping through
the removal of `==>` because its fixture exercised only one of the emitter's two branches; and the
structured-section case testing "counted" while ignoring "ignored".

**One prediction was wrong and one repair was itself vacuous.** The ordering case *did* catch a
reversed sort — it was weak in a different direction than expected. And the first repair to the
structured-section case used the realistic `<block>#1` key form, which stayed green under mutation
because `#` is outside the token class, so the key could never be found in the corpus whether the
section was ignored or not. Both were caught by re-running the detector, not by reading the code.

**The control that licenses all of it**: the builder re-run over this repository emits
`replace-text.txt`, `replace-message.txt` and `path-renames.args` **byte-identical** to before —
119 rules, 22 rename pairs. The only changed number is `blobsScanned`, and running the pre-change
builder against the same tree gives **6414 against 6413**: exactly one phantom blob removed.

### Rev 8 — Phase 0 done, and the half of Gate 3 that was decorative

The SDK went in and the baseline was captured, which was the expected work. Three things were not
expected, and each is the same shape: **a check that reported success without performing one.**

🔴 **`--build-baseline` compared nothing.** It ran `os.path.isfile` and printed the path. Handing it
any file at all removed the *"THIS RUN SAYS NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST"* banner
while verifying exactly as much as passing nothing. Had Phase 0 simply been completed as planned —
capture a baseline, hand it over, watch the banner disappear — **Gate 3 would have been declared
closed on a gate that greens on a file existing.** It now takes both halves and compares per
assembly; a baseline alone keeps the banner up.

**Keyed per assembly, never on the total**, because 10+5 before and 5+10 after nets to zero: a whole
project's tests moved and a total-only check calls it clean. And what gates is **asymmetric** — a
lost pass, a risen failure count, a vanished assembly and a newly-unbuildable target all gate, while
gains are reported and left alone. A test that started passing is not evidence of anything and must
never cancel a loss elsewhere.

🔴 **The capture's own first run reported 809 passed / 24 failed out of a solution that does not
build here.** `dotnet test --no-build` runs whatever is in `bin/`, and that DLL was three weeks old,
from the previous machine. The failure runs in the direction that *manufactures* a finding: a fresh
clone has no `bin/`, so the stale assembly is absent on the other side and reads as a whole test
project destroyed by the scrub. Timestamps cannot answer this — an incremental build does not
rewrite a current DLL, so the timestamp fix condemned everything. MSBuild's `Project -> ...dll` line
is the signal: printed for a rebuild **and** for an up-to-date confirmation, absent for a project it
never reached.

🔴 **Two mutations turned 0 of 31 red**, and the reason is worth keeping. Deleting the missing-path
refusal and the JSON parse refusal both still produced exit 2 — the loader fails to open the file a
moment later, and an emptied capture trips the zero-assemblies refusal. **The verdict was defended
twice over and the diagnostic not at all**, so a malformed capture would have reported *"listed ZERO
assemblies"* and sent the reader hunting for missing tests. The cases now assert the reason.

**All 19 baseline failures are pre-existing**, established by re-running the suites at `446d450`
rather than by inspection — which mattered, because Phase 1 *did* rewrite `agent-tasks/**` JSON and
two `.cs` files in `tests/golden`, and every failing test reads committed artifacts. They are corpus
drift: `80098e7` widened the Modbus served window from 37 to 1024 registers and proved it on the
controller, and the assertions counting against the corpus were never updated. **The tools are
right; the tests are stale.**

**`src/hmi-cli` belongs to no solution.** Its 161 tests pass, and the loop over `*.sln` that Phase 5
would naturally write skips them silently. The capture hunts orphan projects deliberately for this
reason and flags them in the output.

### Rev 7 — the verifier's tests, and what mutation testing found

`tools/verify-scrub.tests.py`: **18 cases**, each running a real pre-scrub → scrub → verify cycle,
because the canary means nothing unless it was recorded against the unscrubbed repository. The five
**tier cases** are the point — the three-tier model decides every verdict and was proven by nothing.

🔴 **Mutation testing found a wholly untested surface.** Disabling the entire non-ODB carrier scan
(`.git/config`, reflogs, `packed-refs`) turned **0 of 18** cases red: the branch-name case reaches
refs through `for-each-ref` and never touches those files, so the surface *looked* covered and was
not. A case now plants a identifying name in a remote URL, which is how that leak actually arrives. All
six mutations now turn at least one case red.

**A fixture defect worth recording.** Two cases failed at first against a *correct* tool: the helper
standing in for filter-repo edited a file and committed, leaving the old blob in the object database
— which this gate walks. **Editing and committing is not a scrub.** The helper now amends, expires
the reflog and prunes, which is the state filter-repo actually leaves and is the tail of its own
recipe. *A test that fails against a correct tool is as dangerous as one that passes against a broken
one, and it was the second kind this suite existed to prevent.*

### The three things that would most change the picture

*(All three were done on 2026-09-17/18 and are kept here as the record of what was chosen and why.)*

1. ✅ **Install the .NET SDK.** Done 2026-09-18 — and the install itself was not the interesting
   part. `winget` never reached the SDK: it stalled registering **its own package catalogue**
   through a wedged MSIX deployment service, and the 3.44 MB that appeared to be .NET was
   `source2.msix`. UAC was never the blocker. Microsoft's signed `.exe` bypasses that machinery
   entirely.
2. ✅ **Write `verify-scrub.tests.py`.** Done 2026-09-17, now **31 cases**.
3. ✅ **Decide the `docs/06` C-124/C-128 ruling.** Ruled 2026-09-17 — generalise the vocabulary,
   keep the reasoning.

### What most changes the picture now

1. ~~**The remaining Phase 2 debt is now one item.**~~ ✅ **PHASE 2 IS CLOSED, 2026-09-18.**
   `check-staged-identifiers.py` (M-5) is built, tested and installed in `hooks/pre-commit`. The
   review backlog was already closed (Rev 9, Rev 10); M-5 was the last item and it was a new tool
   rather than a finding.
2. **Restore `Sharp7.dll`.** One machine-local file keeps `Harness.RigRead` unbuildable. Copies
   survive in stale `bin/` folders from the previous machine, so this is likely a one-file fix —
   but its provenance should be confirmed rather than assumed, and the vendoring question the
   project file itself calls open is still open.
3. ~~**Put `src/hmi-cli` in a solution, before CI is written rather than after.**~~ ✅ **DONE
   2026-09-18.** `src/hmi-cli/hmi-cli.sln` plus the `Directory.Build.props` that subtree was the
   only one in `src/` to lack — so `HmiCli.Tests` had been compiling **without
   `TreatWarningsAsErrors`** while the code it tests had it. 161 passing before and after, 0
   warnings, and the full `--expect` capture gates clean at 23 assemblies / 5,894 passed. The
   orphan hunt in `capture-build-baseline.py` is **kept**: an orphan is made by forgetting, not by
   deciding, so the tree is never reliably free of one.

---

### Rev 4 — Phase 2's rule builder is built, and building it corrected the plan again

`tools/build-scrub-rules.py` + `build-scrub-rules.tests.py` (21 cases, 22 s, all passing;
negative-tested five ways) now exist, and `tools/README.md` carries their section. **98 rules emitted
from a full-history run**: 6,345 blobs, 2m05s. `core.hooksPath` is repaired — it pointed at the
previous machine, so *both* existing commit gates had been silently inert since the move.

Four things were wrong in the plan or in the first implementation, all found by measurement:

| | finding |
|---|---|
| **Section scope** | Only `names` + `tags` (+ the three non-standard sections) can drive a text pass. Rev 3's "1,887 keys" counted comment-shaped sections that are keyed on parsed XML structure. **The real text-pass key set is 1,391**, of which only ~105 occur anywhere. |
| **Breadth was backwards** | Withholding a variant for appearing in many files reads as principled and is inverted — a path stem cited across 38 files is wide *because it is load-bearing*. Withholding on it left **77 of 93 identifier-bearing paths unmatched**, which is A8 all over again. Breadth is now a REPORT. With it demoted to a report: **92 of 93 matched.** |
| **Path names were not in the corpus** | `git cat-file` never shows a path, so a variant occurring only in a directory name was classed dead and got no rule. Paths are now scanned explicitly. |
| **Breadth must be counted at HEAD** | Over history a file edited 30 times contributes 30 blobs, manufacturing an ordinary-looking count from editing activity: 8 demotions at HEAD vs 22 over history — 14 rules not emitted. |

**And one risk in rev 3 is now retracted.** Rev 3 listed stale `ir-hash` values as Phase 3 breakage.
Measured: **zero literal hash values are recorded anywhere** — 57 files mention the `ir-hash`
*command*, none record its output. That repair does not exist and should not be budgeted.

**The builder refused on its first complete run**, correctly: five replacements in the existing maps
occur verbatim in the live job's own IR — `AB-1`'s trap 2, *a replacement can itself be a leak*.
Under the no-human ruling those now resolve deterministically and are re-checked against both the job
folder and this repository.

**Still outstanding in Phase 2:** `verify-scrub.py` (the Gate 3 oracle and its two-sided canary) and
`check-staged-identifiers.py` (M-5). **The builder has proved nothing about a rewrite** — by design,
and the verifier must not share derivation code with it.

### Rev 5 — an adversarial review of the builder, and what it cost

An independent review of `build-scrub-rules.py` found that a run had **exited 0 — "98 rules, every
check passed" — while emitting no rule for 18 of the 19 identifiers in the term list.** Six defects,
all live in a shipped artifact, all now fixed and regression-tested. Current state: **23 tests
passing, 106 rules and 22 `--path-rename` pairs** from a full-history run.

| | defect | consequence |
|---|---|---|
| **F1** | the length floor applied to *declared* terms, not just inferred map keys | 15 of 19 term rows discarded, **including all four job codes**; 11 of 12 identifier occurrences in the tracked `.gitignore` survived every rule |
| **F3** | 🔴 **no `--path-rename` set was emitted at all** | every identifier-bearing directory and filename survived the rewrite |
| **F2** | `--out` tested *tracked*, not *ignored* | the file naming every identifier could be written into tracked space |
| **F5** | a JSON `null` map value | literal `None` as a replacement, or a bare `TypeError` exiting 1 with no gate prose |
| **F6** | the `git cat-file` parser broke on `<oid> missing` | **silently abandoned the rest of history** and still exited 0 |
| **F12** | short term rows skipped before validation | the silent term-list shrink this design exists to prevent |

🔴 **A CORRECTION TO REV 4, AND THE MOST INSTRUCTIVE ERROR IN THIS DOCUMENT SO FAR.** Rev 4 recorded
*"With breadth demoted to a report: 92 of 93 matched."* **That measurement was meaningless.** It was
taken by applying the emitted regexes to path strings in Python — but `--replace-text` rewrites blob
*contents*, and filter-repo never applies those regexes to a path. Paths are renamed only by
`--path-rename`, which the tool did not emit until rev 5. The claim was true about Python and false
about the artifact, and it read as a confirmation of the A8 fix while the A8 population was
untouched. **A8 keeps finding new ways to be true: it is not really about case, it is about
verifying the wrong surface.**

**Principle earned by F1, worth carrying:** *a declaration is not a candidate.* The length floor and
the Green test exist to stop a name **inferred** from a map colliding with ordinary code. Neither
reasoning survives contact with a term the owner wrote down by hand.

### Rev 5 — git plumbing findings that change the verifier's design

Measured on this repository, and each one invalidates an obvious implementation:

| finding | consequence |
|---|---|
| `git cat-file --batch-all-objects --batch` with **no stdin** yields every blob, commit message and author/committer identity in **one ~0.8 s pass** | it is also immune to the stdin deadlock that cost 99.5 s to diagnose earlier |
| `git rev-list --objects --all` **misses 8 real path names** here | unreachable trees are never traversed, and a tree is printed once even when two directories share its content |
| `git log --all` sees **1,339 of 1,373 commits** | 34 commits, each a potential residual author email or message, are invisible to it |
| `--batch-all-objects` is a **strict superset** of reachable + unreachable (14,336 + 227 = 14,563 exactly) | a separate `fsck` pass is redundant for enumeration, useful only as an independent cross-check |
| **227 unreachable objects exist right now** | 61 blobs, 28 commits, 138 trees |
| the **Windows pipe** is the bottleneck, not git — 242 MB costs ~15 s through an MSYS pipe and 0.8 s read directly from Python | never shell out through `sh -c 'git … \| …'` |
| `%an/%ae` are `.mailmap`-rewritten | **use raw object bytes** — a mailmap can mask the very identifier being hunted |
| **non-ODB text carriers** no plumbing reaches: `.git/config`, reflogs (2,158 entries carrying branch names *and* commit subjects), `.git/description`, `.git/info/exclude`, `.git/packed-refs`, `.git/COMMIT_EDITMSG`, `.git/filter-repo/` | these must be checked explicitly or they are simply never examined |

### Rev 5 — the canary needs three sides, counts, and a version stamp

Three open mechanisation-backlog items bear directly on the verifier and make the naive design wrong:

- **M-20** — *a gate that checks set membership cannot see an entry that became false.* A canary
  string legitimately deleted by ordinary work reads as a scrub failure. Each entry must carry the
  subject version it was verified against; a stale entry is **UNVERIFIED — a reportable state, not a
  pass**.
- **M-21** — *"cannot be reached from here" and "there is nothing there to reach" look identical.* A
  canary at zero means either the scrub over-matched or the string was never there, so MUST-SURVIVE
  entries carry **expected counts**, not just strings.
- **M-17** — a generated artifact with hand-added keys gets eaten by its generator, so the canary
  list must not live in `sanitization/scrub/`.

Verified MUST-SURVIVE candidates, none colliding with any emitted rule: `C-001` (327 occurrences /
62 files), `NOTHING EXAMINED` (116/61), `converter preflight` (100/60), `EMPTY IS NOT CLEAN` (71/62),
`docs/06-lad-conventions.md` (57/45), `ladder-ai/agent-evidence/1` (25/25). The sentinel `QQSCRUBQQ`
has **0** pre-existing occurrences, which makes it the MUST-NOT-APPEAR control rather than a survivor.

**Also decided at rev 5:** all 1,334 commits carry an employer-domain author email, which
`filter-repo` does not touch on its own and Gate 3 did not check. The rewrite will map it to a
**GitHub noreply address** via `--mailmap`; the exact address joins the licence as an open item.

### Rev 6 — the verifier exists, the trial rewrite ran, and the gap is 394 → 1

`tools/verify-scrub.py` is built and has been run against a real `filter-repo` rewrite end to end
(clone → 117 rules + 22 path-renames + `--mailmap` → verify). It walks the object database in
~30 s.

**Its instrument control caught a bug in itself on its first run**, which is the whole reason it
exists: 1,505 of 1,719 needles could not match *anything*, because the tokeniser split on
`[a-z0-9_]+` and most needles are dotted tag paths. The tool had already reported a plausible
"50 residuals" over the 12% of its vocabulary that happened to be single words. Nothing else would
have found this — the canary counts use a different code path and stayed healthy throughout.

**The 394-residual gap was three problems, two of them defects in my own tools:**

| | |
|---|---|
| **317 identity mappings** | the maps declare `X → X`; the builder correctly emits no rule. Not leaks — now **tier T3: reports, never gates, and gates on a DECREASE instead**, which is the over-scrub detector |
| **59 of 61 "inferred" residuals were FALSE POSITIVES** | the verifier is boundary-optional, so it flagged `Foo.Bar` inside `Foo.BarBaz` — a *different tag sharing a prefix*. Whole-token and embedded hits are now returned separately; only whole-token gates for inferred keys |
| **7 declared terms genuinely survived** | the builder emitted `\bterm\b`; a short declared term occurring only inside a token is never rewritten. **A8 in mirror image.** Declared terms now emit **anchorless** rules and use substring presence |

**Result:** T1 declared 7 → **1** · T2 whole-token 2 → **0** · over-scrub **0**. The gate still
correctly **fails the unscrubbed source**, which is the regression that matters most.

**The over-scrub detector fired on its first real run and was right to.** An 11-character
conventional name went from 1,438 whole-token occurrences to 0. Investigation showed the scrub was
working and **the verifier's tiering was wrong**: its Green-membership test was substring-based
while the builder's is token-based, so a name that merely appears inside a longer name in clean
content was tiered "conventional" and its legitimate removal read as damage. *The verifier may be
wider than the builder about what it hunts; it must agree with it about what counts as
conventional.*

### Rev 6b — ✅ GATE 3 REACHES AN EARNED ZERO ON A REAL REWRITE

```
EARNED ZERO: zero residuals over a vocabulary of 1719 needles, with the positive control
intact, across 6288 blobs, 1334 commits and 1842 paths, with 15 blobs unsearchable.
```

And the regression that matters more than the pass: **the same gate still exits 1 against the
unscrubbed source** (T1 10, T2 whole-token 79). A gate that only ever passes is not a gate.

Closing the last declared term took three more fixes, each a different way for a tokeniser or a
matcher to be narrower than the thing it is hunting:

| | defect | fix |
|---|---|---|
| 1 | the builder's declared-presence check used `[A-Za-z0-9_]+`, so a term **containing a dot can never be a substring of it** — it returned False for every dotted term however often it occurred | a second, wider run `[A-Za-z0-9_.@/-]+` for the presence vocabulary |
| 2 | `variants_for` returns a dotted key **verbatim, with no case variants**, and `filter-repo` compiles rules **case-sensitively** while the verifier searches case-insensitively. The term had a correct anchorless rule, occurred 33 times, and every occurrence was in a case the rule could not match | declared rules emit `(?i)`. *A declaration is about a NAME, not a spelling of it.* |
| 3 | the final residual was a **branch name**. `filter-repo` renames paths, not refs, so the rewrite structurally cannot reach it | the clone's disposable branches are deleted and reflogs expired — already Phase 1 step 2 and Phase 3 step 2 of this plan |

Defect 3 is the interesting one: **the gate caught a real leak that the rewrite could not have
fixed**, and the remedy was a step this plan had already written down and not yet performed. That is
the gate doing precisely its job.

**Trial-rewrite recipe, now proven end to end:** `git clone --no-local` →
`filter-repo --replace-text --replace-message @path-renames.args --mailmap` → delete all but the
publishing branch → `reflog expire --all --expire=now && gc --prune=now` → `verify-scrub.py`.
Rewrite ~7 min, verification ~30 s.

### Rev 5 — review findings NOT yet fixed

Recorded so they meet the next pass rather than evaporating: ~~**F4** the overlap check cannot see a
needle matching *inside* a replacement (the artifact is clean by luck, not by the check) · **F9**
variant collisions are first-writer-wins, 5 live instances · **F11** map section class is discarded,
so site/site keys never get their spaced form · **F13** rung-3 conflict votes are
double-counted~~ — **DISCHARGED 2026-09-18, see Rev 10.**
· ~~**F16–F19** five vacuous test assertions, and **no test exercises `--scan history`**, the default
path~~ — **DISCHARGED 2026-09-18, see Rev 9.**

✅ **The review backlog is closed.** All twelve findings are discharged: F1, F2, F3, F5, F6, F12 on
2026-09-17; F16–F19 at Rev 9; F4, F9, F11, F13 at Rev 10.

---

## 1. Decisions taken (owner, 2026-09-17)

Settled. The plan assumes them and does not re-open them.

| # | Decision | Consequence |
|---|---|---|
| D1 | **This repository stays private and stays the working repo.** A separate public repo is exported from it. | `Live Runs/` work continues here unchanged. A stray commit here can never reach GitHub. |
| D2 | **Full history rewritten with `git-filter-repo`**, not squashed. Runs on a **clone**. | All 1,334 commits survive publicly, scrubbed. This repo is never rewritten. |
| D3 | **Own IP, published on the owner's terms.** | No employer sign-off gate. Site *data* stays out regardless — that is the data boundary, not a rights question. |
| D4 | **Scrub everything**: job codes, site and site names, real block/type/DB/member names. | Deeper than `docs/13`'s "job codes only", which governs the *private* repo. Public gets no identifiers at all. |
| D5 | **Publish all four core areas** — `src/` + tests, `.claude/` + `CLAUDE.md`, the `docs/` suite, and the `ir/` `patterns/` `gen/` `simatic-ml/` corpus. | Effectively "publish the repo, scrubbed". |
| D6 | **Publish the second tier too** — `CHANGELOG.md`, `AITODO.md`, `agent-tasks/`, `hmi/`, `research/`, `tools/`, `hooks/`, `extract/`. | Nothing withheld for tidiness. |
| D7 | **CI plus a runnable demo.** | Green badge and a one-command end-to-end run, with no licensed Siemens assembly. |
| D8 | **Audience: software / AI engineering employers.** | README leads with agent architecture, safety rails, test discipline. LAD is the domain, not the pitch. |
| D9 | **Publish the data-boundary docs, scrubbed.** | The tiered boundary, audit backlog and remediation record are evidence of judgement. |
| D10 | **Status reframed as a completed body of work.** | The 🛑 suspension banner becomes an honest "active development paused" line, not the opening sentence. |
| D11 | **Gated phases**, hard verification gate before anything leaves the machine. | Mirrors how the rest of the project works. |
| D12 | **Licence: open item.** | Must be settled before Phase 6. See §7. |

---

## 2. Verified state of the repository — scouted 2026-09-17

### Scale

| measure | value |
|---|---|
| tracked files | 1,836 |
| commits | 1,334 (first 2026-07-10) |
| `.git` size | 25 MB |
| tracked C# files | 997 |
| test files / test methods | 493 / ~5,810 `[Fact]`+`[Theory]` |
| tracked markdown | 266 |
| tracked `.ir` / `.xml` | 139 / 141 |
| solutions | 7 under `src/` (`converter`, `openness-cli`, `harness`, `device-guard`, `download-feedback`, `wave-control`, `hmi-cli`) + `tests/golden/GoldenHarness.sln`. `hmi-cli` had projects but **no `.sln`** until 2026-09-18 |
| branches | 34 — **33 merged into `master`, 1 unmerged** (`wip-settling-2026-08-20`) |
| stale worktrees | 22, all `prunable`, all pointing at the previous machine's path |
| git remotes | **none configured** |

### Toolchain on this machine

```
.NET SDKs:        8.0.425   INSTALLED 2026-09-18 (runtimes 6.0.11, 8.0.26, 9.0.7)
TIA Portal:       not installed
Python:           3.14.4,  pip 26.0.1
git-filter-repo:  INSTALLED 2026-09-17 (a40bce548d2c)
                  → C:\Users\O\AppData\Local\Python\pythoncore-3.14-64\Scripts\git-filter-repo.exe
                  NOT on git's PATH; prepend the Scripts dir or invoke the .exe directly
```

**Phase 0 is done (2026-09-18).** The baseline is `sanitization/build-baseline.json`: **23
assemblies, 5,877 passed, 19 failed**, SDK 8.0.425, Release.

`winget` never reached the SDK. It stalled registering its own package catalogue through the MSIX
deployment service, which was itself wedged on a leftover package it had been failing to delete
every six minutes for hours (`error 0x12C`, `ERROR_OPLOCK_NOT_GRANTED`). The SDK came from
Microsoft's signed `.exe` instead, which touches none of that machinery. **UAC was never the
blocker** — the install had not got that far.

**All 19 failures are pre-existing**, verified by running the same suites at `446d450`, the commit
before this branch: identical names, identical counts. They are corpus drift — `80098e7` widened the
Modbus served window from 37 to 1024 registers and proved it on the controller, and the assertions
counting against the corpus were never updated (37 → 1024, 18 → 21, 26 → 27). The tools are right
and the tests are stale. `AB-1` measured a case where a rename silently weakened a test assertion,
which is why this is a per-assembly baseline rather than a total.

TIA-gated projects (reference `Siemens.Engineering`, **cannot build off a TIA machine**):
`OpennessCli`, `OpennessCli.Tests`, `DownloadProbe`, `Harness.Device`, `Harness.Map`,
`WaveControl.Cli`. `Converter.csproj` matches the grep only inside a comment explaining that its one
project reference pulls in nothing — the converter's purity invariant is intact.

### What is missing entirely

No `LICENSE`. No `.github/`, no CI. No `.editorconfig`. No public-facing README — the current one is
an internal orientation page.

### What is stale or wrong

- **`README.md` advertises `extract/`** as "Python extractors (alarms, IO, xref) + templates".
  `extract/` contains exactly one file: `README.md`. The extractors were never built.
- **`README.md` describes `tools/` as "AHK leftovers, one-off scripts".** It is in fact tested
  Python and PowerShell checks (`check-file-budgets.py`, `check-agent-evidence.py`,
  `check-doc-migration.py`, the Openness approval scripts). The description undersells real work.
- **39 tracked files carry the previous machine's absolute path** (141 occurrences).
- **54 tracked build-output files** under `hmi/examples/**/build/`.
- **Default branch is `master`; `CLAUDE.md` names `main`.**

### The readability problem

| file | size |
|---|---|
| `src/converter/README.md` | 353 KB |
| `docs/16-future-ideas.md` | 347 KB |
| `CHANGELOG.md` | 196 KB |
| `src/openness-cli/README.md` | 181 KB |

Content is good; packaging destroys it. Per D-choice these are **split into navigable sections,
losing nothing**.

---

## 3. The identifier problem — measured, not estimated

### Two populations

**(a) The legacy reference/Amber-tier projects.** Live in the working tree today, **never
remediated** — `AB-1` was scoped to the live job, not to these. Concentrated in `docs/evidence/`,
`docs/13-data-boundary.md`, the `gen/` bench-run trees, `ir/`, `patterns/`, `CHANGELOG.md` and
`src/converter/README.md`. **131 tracked files by content** — note that rev 2 first recorded 75, which was the count for a four-term grep; adding the block-name and site terms raises it to 131. *A term list is part of the measurement; quoting a count without it is meaningless.*

**(b) The live job.** Working tree **remediated 2026-08-27** (`AB-1`, 285 sites across 49 files).
The site *name* is **verified absent from every tracked file and every commit, present and
historical — zero occurrences.** The bare job code remains in ~36 tracked files, permitted by
`docs/13` for the private repo and removed for the public one by D4.

### 🔴 Identifiers are in PATH NAMES too — rev 1 missed this

**93 tracked paths carry an identifier in a directory or file name.** `--replace-text` rewrites blob
*contents only*; it will not touch these. They need `--path-rename`.

The five directory prefixes requiring rename, and the citation load each carries:

| prefix (identifier elided) | files citing it | literal hits |
|---|---|---|
| `gen/<legacy-bench>` | 38 | 80 |
| `ir/<legacy-bench>` | 38 | 111 |
| `docs/evidence/<legacy-answerkey>` | 9 | 11 |
| `gen/_validation/<legacy-vsd>` | 5 | 8 |
| `gen/_validation/<legacy-shredder>` | 1 | 1 |
| **distinct files citing any** | **57** | **211** |

Regenerate the exact list:

```bash
git ls-files | grep -i -f <(ls sanitization/*.map.json | xargs -n1 basename | sed 's/\.map\.json$//')
```

> 🔴 **THE OBVIOUS COUPLING DOES NOT WORK, AND ASSUMING IT DOES IS THE MOST DANGEROUS ERROR IN THIS
> PLAN.** It is tempting to reason that because each prefix *contains* the identifier, the
> `--replace-text` rule scrubbing it in prose also repairs all 211 path citations for free. **It does
> not.** The map keys are `CamelCase`; the paths are `lower-hyphenated`. A `regex:\bCamelCase\b` rule
> matches **none** of the directory forms — measured, not supposed:
>
> ```
> gen/<legacy-bench>                 \bCamelCase\b  → NO MATCH
> ir/<legacy-bench>/CamelCase.ir     \bCamelCase\b  → match (the FILE, not the directory)
> docs/evidence/<legacy-answerkey>   \bCamelCase\b  → NO MATCH
> ```
>
> This is `AB-1`'s step-6 warning arriving in a new place: *lower-case hyphenated forms are invisible
> to every identifier-shaped pattern.* **The replacement list must carry explicit lower-hyphen stem
> variants** alongside the CamelCase keys, and each `--path-rename` target must equal what those
> variants produce. Get this wrong and the scrub reports success while 93 path names and 211
> citations still carry the identifier.

### The legacy corpus is bounded, and its maps already exist

The single most useful fact for scoping:

- `ir/` holds three corpora: `<legacy-bench>` (**35 files, unsanitized**), `reference` (16 files,
  **already sanitized**), `test-project001` (50 files, **Green by construction**).
- **34 of the 43 `sanitization/*.map.json` files correspond 1:1 by name to those 35 files.**

So the unsanitized IR is a *closed set of 35 blocks whose replacement vocabulary already exists.*
This is not an open-ended hunt.

### 🔴 But the maps are NOT a drop-in replacement list — rev 1 overstated this

Measured across all 43 maps (rev 1's figure of "~2,649 mappings" was wrong — it counted section
headers and cross-file duplicates):

| property | count | why it matters |
|---|---|---|
| distinct source keys | **1,887** | the real size of the triage |
| keys with **conflicting** replacements across maps | **43** | a single global list cannot be derived mechanically; each needs adjudication |
| identity mappings (key == value) | **326** | no-ops that would pad any "sites fixed" figure |
| keys under 8 chars | **92** | `AB-1`'s own check filters at length ≥ 8 because these collide with ordinary code |
| bare words, no dot | **368** | the dangerous class for word-boundary replacement |
| files with a **UTF-8 BOM** | **25 of 43** | breaks naive JSON tooling — it broke the first parse of this scout; read with `utf-8-sig` |

**Phase 2 is therefore a triage task over ~1,887 keys with 43 genuine ambiguities, not a generation
step.**

### History is unscrubbed for both populations

Per-term commit counts (`git log -S`, all refs) run 5–82 per term; commit *messages* matching the
identifier set: **46 + 5**. Treat every figure as a **floor, not a census** — the same caveat `AB-1`
records. A whole-word grep is a candidate generator.

> **`AB-1`'s deferral was justified on the grounds that "no git remote is configured, so nothing has
> left this machine."** That premise is exactly what publication removes. This plan discharges that
> deferral for the public artifact.

---

## 4. Verified `git-filter-repo` semantics

Checked against the installed build on 2026-09-17, not assumed:

| flag | verified behaviour |
|---|---|
| `--replace-text FILE` | **Blob contents only.** Expressions are literal by default; `regex:` and `glob:` prefixes supported; `==>` sets the replacement, else it defaults to `***REMOVED***` — **always supply `==>`, the default is not what we want.** |
| `--replace-message FILE` | **Commit and tag messages.** Same file syntax. **Required in addition to `--replace-text`** — rev 1's claim, now confirmed. |
| `--path-rename OLD:NEW` | Renames a file or directory. Repeatable. Caveat in its own help: *if you combine filtering options with renaming ones, do not rely on a rename argument to select paths; you also need a filter to select them.* |
| `--preserve-commit-hashes` | **Correction to rev 1.** By *default* filter-repo already rewrites old SHA references **inside commit messages** to the new hashes. So the SHA-citation problem is confined to **tracked markdown** — `docs/evidence/stage-S0…S6.md` — and does not extend to commit messages. Do **not** pass this flag. |
| `--analyze`, `--dry-run` | Read-only rehearsal. Both must be run before the real pass. |
| fresh-clone requirement | It **refuses to rewrite unless run from a clean fresh clone** (or `--force`). This *enforces* the Phase 3 design rather than merely permitting it. **Never pass `--force`** — the refusal is a safety feature protecting this repo. |

---

## 5. Phase plan

Each phase ends at a gate. Nothing crosses a gate without the named evidence. Effort figures are
**estimates for a focused operator**, not measurements; the Phase 2 and 3 figures are the
load-bearing ones and the most uncertain.

### Phase 0 — Make the machine able to verify · ✅ DONE 2026-09-18

1. ✅ **.NET 8 SDK 8.0.425** installed. The `net48` targeting packs turned out to be **already
   present** — `openness-cli` reaches type resolution and fails only on `Siemens.Engineering`, so
   the second risk this step anticipated does not exist.
2. ✅ `git-filter-repo` already installed; `Scripts` still needs to be on `PATH`.
3. ✅ Baseline captured **by script, not by hand** — `tools/capture-build-baseline.py`, run twice
   and byte-comparable per assembly. 23 assemblies, 5,877 passed, 19 failed.
4. ✅ `tools/check-file-budgets.py` passes.
5. ✅ Unbuildable targets recorded in the capture itself under `cannotBuild`, with the error code
   and the project that raised it.

**Why the capture is a script.** Two captures either side of a rewrite are only comparable if they
were taken the same way; one assembled by hand from a terminal scroll and another a week later
differ in which solution was remembered and which orphan project was noticed, and every one of
those differences reads downstream as a scrub that changed something.

🔴 **The capture's first run was wrong, and wrong in the dangerous direction.** It reported
`OpennessCli.Tests` at 809 passed / 24 failed **out of a solution that does not build here** —
`dotnet test --no-build` runs whatever is in `bin/`, and that DLL was built on the previous machine
three weeks earlier. A fresh clone has no `bin/` at all, so the stale assembly would be absent on
the other side of the comparison and read as **a whole test project destroyed by the scrub**.
Timestamps cannot detect this and trying was worse — an incremental build does not rewrite an
already-current DLL, so a re-run condemns everything. The signal that works is MSBuild's own
`Project -> ...dll` line, printed for a project it rebuilt *and* for one it confirmed up to date,
absent for one it never reached.

**Gate 0 — a recorded green baseline exists for every project that can build here.** Without it,
"the scrub broke nothing" is an assertion, not a measurement.

---

### Phase 1 — Hygiene, in the private repo · ✅ DONE 2026-09-17

> **Result.** 22 stale worktrees pruned · 32 merged branches deleted (**including the one whose name
> carried a job code — the residual Gate 3 caught and `filter-repo` structurally cannot fix**) ·
> 54 build outputs untracked and ignored · 126 old-machine path occurrences across 26 files replaced
> with a `<user>` placeholder, byte-wise so CRLF survived · `README.md`'s two stale layout claims
> corrected · `.editorconfig` added · `core.hooksPath` repaired.
>
> **`wip-settling-2026-08-20` was KEPT**, after review: its three files
> (`SettlingMomentTests.cs`, `PostCompletionRecoveryBlock.cs`,
> `BuildStampDeclarationScopeTests.cs`) exist nowhere on master and their concepts appear in zero
> tracked files. Deleting it would have lost ~650 lines with no other copy. It is a preservation
> branch and it is doing its job.
>
> 🔴 **CARRIED INTO PHASE 3:** the rewrite must add
> `--path-glob '!hmi/examples/*/build/*'` (or the `--invert-paths` equivalent) so those 54 files
> leave **history**, not just the tip. That exclusion is what removes Gate 3's *"15 blobs
> unsearchable, NOT OCR'd"* caveat — the gate stops having a blind spot instead of documenting one.
>
> **Deliberately NOT done here:** `master` → `main`, which happens only in the published clone.
> `CLAUDE.md` turns out never to have claimed a default branch, so there was nothing to correct.

### Phase 1 — Hygiene, in the private repo · ~4–6 h *(original plan, for reference)*

Ordinary commits here. Worth doing whether or not anything is published, and reversible.

1. `git worktree prune` — clears all 22 stale registrations. *(minutes)*
2. Delete the 33 merged branches. **`wip-settling-2026-08-20` is the only unmerged branch** — decide
   it on its merits, separately. **Delete or rename the branch whose name carries the live job
   code**; a branch name is a public ref. *(minutes)*
3. `git rm --cached` the 54 `hmi/examples/**/build/` artifacts; extend `.gitignore`. *(minutes)*
4. Replace the 141 old-machine absolute paths across 39 files with repo-relative paths or a
   documented environment variable. *(1–2 h)*
5. Fix the stale `README.md` claims about `extract/` and `tools/`. Recommend dropping the `extract/`
   advertisement with a one-line "descoped" note rather than building it. *(~1 h)*
6. Add `.editorconfig`. Rename default branch `master` → `main`.
7. ✅ **DONE at rev 4** — `core.hooksPath` pointed at the previous machine's absolute path, so
   **both** existing commit gates had been inert since the move. Repaired to the relative `hooks`.

**Gate 1a — Phase 0's baseline still passes unchanged.**

---

### Phase 2 — Build and sign off the replacement list · ✅ ESSENTIALLY DONE

> **Status, rev 6b.** `tools/build-scrub-rules.py` (23 tests) emits **119 rules + 22 path-renames**;
> `tools/verify-scrub.py` is the Gate 3 oracle and **returns an earned zero on a real rewrite while
> still failing the unscrubbed source**. Steps 1–8 below are mechanised inside the two tools rather
> than performed by hand.
>
> **Outstanding:** `check-staged-identifiers.py` (M-5, a separate deliverable — its subject is the
> staging area at commit time, not a rewritten clone); the verifier's own test suite; and steps 9
> and 10, which remain open questions no tool reaches.

The most important phase. **The list is the whole scrub.**

1. **Load all 43 maps with `utf-8-sig`** (25 carry a BOM). Flatten to source→replacement pairs.
2. **Adjudicate the 43 conflicting keys.** Pick one canonical replacement each. Bias toward whatever
   the already-sanitized `ir/reference/` and `simatic-ml/reference/` corpora use, so public
   vocabulary matches what is already committed rather than inventing a third dialect.
3. **Drop the 326 identity mappings.** They are no-ops and would inflate any "sites fixed" figure.
4. **Quarantine the 92 sub-8-character keys and triage the 368 bare words by hand.** These cannot be
   auto-applied. `AB-1` filters at length ≥ 8 for exactly this reason.
5. **Add what the maps do not carry** — job codes, site and site names.
6. **Emit the `--replace-text` file** in filter-repo syntax: `regex:` with `\b` word boundaries,
   **longest-first**, and an explicit `==>` on every line (the default `***REMOVED***` is not what we
   want). Emit the matching **`--replace-message`** file and the **`--path-rename`** set.
   🔴 **For every CamelCase key, also emit its lower-hyphen stem variant** — `\bcamel-case\b`,
   `\bcamelcase\b` — or every directory name and all 211 path citations survive the scrub untouched
   (§3). The `--path-rename` targets must equal what those variants produce.
7. **Run `AB-1`'s full 6-step check** — steps 1–6 including the member-name pass and the
   case-insensitive `.html`/`.py` pass — against **every** IR directory in **every** job folder.
   `AB-1` records that a job folder can hold more than one and that sweeping only the older one
   reported a clean repo.
8. **Design constraints, each a trap `AB-1` measured live:**
   - **Word-boundary regex, never literal substring.** One equipment word in the identifier set is
     also a Green-tier machine name — present in `patterns/`, in `CHANGELOG.md`, and in `docs/13`'s
     own worked example of this exact confusion. A blanket token replace corrupts Green content.
   - **Longest-first ordering**, so composite names are consumed before their components.
   - **Grep every replacement back against the job folders before keeping it.** One invented name in
     the 2026-08-27 run already existed verbatim in the job's own IR — a fix that would have
     introduced a fresh leak while closing an old one. *Choosing the vocabulary is part of the
     check, not after it.*
   - **The output is a worklist to triage, never a count to report.** Roughly half of `AB-1`'s
     candidates were conventional names mandated by `docs/06`; a raw match total overstates the leak.
   - **Substitute, do not delete.** `AB-1`'s method: every comment keeps its measurement, its date
     and its reasoning, and gets a concrete case under a different name. No paragraph removed.
9. Separately triage the ~54 tracked files using generic process vocabulary. Most predate the live
   job and are ordinary industrial English. **This is a human judgement about what identifies a
   plant**, and no matching rule reaches it.
10. Close the item `AB-1` left open: `docs/06-lad-conventions.md` illustrates C-124/C-128 with a
    **process description** — a named dwell time and a named measurement history. It names no block,
    tag, equipment or site. It was flagged, not decided, and **must be decided before
    publication.**

**Gate 2 — the owner reviews and signs off the replacement list, item by item, before it touches
anything.**

---

### Phase 3 — Fork the public repo and rewrite its history · ~1–2 days

1. `git clone --no-local` this repository to a new working copy. **All rewriting happens in the
   clone.** filter-repo's fresh-clone refusal enforces this; **never pass `--force`.**
2. In the clone: delete every branch but `main`; clear stale worktree refs.
3. **Rehearse: `--analyze`, then `--dry-run`.** Read both reports before the real pass.
4. Run the real pass with `--replace-text`, `--replace-message` **and** the `--path-rename` set.
   Do **not** pass `--preserve-commit-hashes` — the default rewrites stale SHA references in commit
   messages, which is wanted.
5. Re-apply Phase 1 hygiene where the rewrite disturbed it.

**Known breakage, to be repaired in the clone:**

- **SHA citations in tracked markdown — broader than first recorded.** Measured by resolving every
  candidate token against the object database: **253 tokens in tracked markdown are real commits in
  this repository, spread across 80 files** — *not* confined to `docs/evidence/stage-S*.md`, which
  hold only 29 of them. (The earlier "~290 SHA-shaped tokens" was a regex population, not a count of
  citations: 285 tokens matched the shape, 253 resolved to real commits, and **zero** were full
  40-character hashes.) filter-repo fixes such references *in commit messages* automatically but
  **not in file contents**. Decide per file: re-map to new SHAs, or replace with a dated
  description — *re-describing is cheaper and cannot rot again.* At 80 files this is the largest
  single repair in Phase 3 and the main driver of its upper estimate.
- ~~**Recorded `ir-hash` values go stale.**~~ **RETRACTED at rev 4, measured.** 57 files mention the
  `ir-hash` *command*; **none records a literal hash value**. There is nothing to regenerate here and
  the repair should not be budgeted.
- **Byte-comparison and golden tests.** `.gitattributes` pins `*.ir` and `*.xml` to `eol=lf`; the
  substitution must be byte-wise and must not disturb line endings or BOM state. `AB-1` measured one
  case where a rename made a sort-order assertion trivially true — **a rename must not silently
  weaken a test.**
- **Hard rule 8 routing.** The 35 `.ir` files in the legacy corpus are LAD content. The history
  rewrite is a mechanical blob operation, but the **resulting working-tree `.ir` content must be
  verified through `lad-coder`**, not inline, with no size exception.

**Gate 3 — THE HARD GATE. Nothing leaves this machine until every line returns an earned zero or a
green:**

- [ ] Identifier set → **0 matches, CASE-INSENSITIVELY**, across the rewritten working tree.
      *Case sensitivity is not a detail here — a case-sensitive check passes while the lower-hyphen
      forms leak. See §3.*
- [ ] Identifier set → **0 matches** across **all 1,334 rewritten commits** (`git log -S` per term)
      **and** across all commit messages.
- [ ] Identifier set → **0 matches in any tracked path name** (`git ls-files | grep -i -f …`).
- [ ] **The demo corpus (`simatic-ml/reference/`, `ir/reference/`) is byte-identical to before the
      scrub**, or every difference is individually explained.
- [ ] `AB-1`'s 6-step check re-run against every job IR directory, reporting only the conventional
      residual, **with the compared-file count stated.**
- [ ] Phase 0's baseline passes **unchanged** on the rewritten tree — same pass counts, per assembly.
      Mechanised as of 2026-09-18: capture the clone with `tools/capture-build-baseline.py --repo
      <clone> --out sanitization/build-current.json`, then pass **both** halves to `verify-scrub.py`.
      A baseline alone keeps the "says nothing about whether the scrub broke a test" banner up.
- [ ] `tools/check-file-budgets.py` passes with no ceiling raised.
- [ ] **No git remote configured on *this* repository.** Confirmed, not assumed.

---

### Phase 4 — Restructure for a reader · ✅ DONE 2026-09-17

> **Done.** `LICENSE` (**MIT**, settling an open item carried since rev 1) · a public-facing
> `README.md` written for the D8 audience, with the suspension banner moved to where a reader
> reaches it *after* understanding what the project is · `docs/00-README.md` absorbed the
> orientation content and had four measured staleness defects corrected (it claimed
> `adr-0000…0011` against **13** ADR files; called `13-data-boundary.md` a draft when it is in
> force; carried two long-discharged to-dos) · `CHANGELOG.md` gained a table of contents.
>
> **Two of the three splits are complete, both verified by `tools/check-doc-migration.py`:**
>
> | file | before | after | markers | dropped |
> |---|---|---|---|---|
> | `src/converter/README.md` | 4,890 | **102** + 7 docs | 1,729 | **0** |
> | `src/openness-cli/README.md` | 2,398 | **135** + 4 docs | 827 | **0** |
>
> **Each keeps its own path as the index, so all 195 inbound citations still resolve** — the split
> was never in a half-broken state. Three structural defects were fixed on the way, each one a
> section that was invisible where it sat: `## Exit codes` was **584 lines of which 28 were the
> table** and the rest three unrelated investigation logs; a round-trip defect (FI-102) was buried
> *inside* `## Build & test`; and a whole silent-loss class trailed the converter file after it.
> The 13 `— see below` pointers in the openness `## Subcommands` fence were retargeted by command —
> they were the only cross-references that could break silently, since neither file contains a
> single `](#anchor)` link.
>
> ✅ **`docs/16-future-ideas.md` — DONE 2026-09-17.** 3,809 → **229 lines** (the index) plus six
> files grouped by FI-number range. **95 entries, holes at 55–60/74/75 intact, nothing renumbered.**
>
> The migration check returned **2 unaccounted of 991 markers — and that is the correct result, not
> a shortfall.** Both were traced to lines 121–262, the four stale status snapshots the owner chose
> to delete, and to nowhere else in the baseline. The claim proved is *"everything dropped is
> exactly what we chose to delete"*, which is a different and stronger statement than a bare zero.
>
> Three things this split needed that the other two did not: heading levels were inconsistent
> (FI-01–81 as `###` inside a `## Ideas` wrapper, FI-82–103 as `##`) and were normalised, while
> `#### FI-65a/b/c` were left alone as the entry sub-parts they are — a regex treating every
> `##+ FI-` as an entry finds **100 headings where there are 95 entries**. Two addenda were filed
> under the wrong item: FI-93's sat inside FI-99's section and therefore in a *different range file*,
> and both were re-homed with a pointer left behind. And the index derives status **mechanically**,
> marking **27 of 95 UNCLASSIFIED** rather than reading a verdict out of an entry's prose — which is
> precisely the doc-to-doc citation failure this register exists to record.
>
> *(Superseded record of the pre-split state:)* 🔴 **NOT DONE: `docs/16-future-ideas.md`.** It is the most entangled of the four — 45 citing
> files, and a split needs three things the others did not: the FI items are **95 entries with 8
> deliberate holes** (FI-55–60, 74, 75, documented elsewhere — *do not renumber*); **~37 entries
> carry no status field at all**, so a usable index needs a normalising pass; and the file has real
> ordering defects (FI-67 before FI-66, FI-71–73 before FI-68–70, and two addenda physically filed
> under the wrong item). The analysis is done and recorded — split by **FI-number range, not
> theme**: 72% of the 204 entry-to-entry cross-references stay in-file that way, and the 1,316 bare
> `FI-nn` citations repo-wide resolve to a filename by arithmetic.

### Phase 4 — Restructure for a reader · ~1–2 days *(original plan, for reference)*

1. **New public `README.md`** for D8's audience: lead with the hard problem — safely letting an LLM
   author PLC code that will run on real plant — then architecture, then what was proven, then how to
   run the demo. The current README's orientation content moves to `docs/00-README.md`.
2. **Reframe the status banner** per D10 — placed where a reader reaches it *after* understanding
   what the thing is.
3. **Split the oversized documents**, keeping all content: `src/converter/README.md`,
   `src/openness-cli/README.md`, `docs/16-future-ideas.md` → sectioned folders with an index.
   `CHANGELOG.md` keeps its form but gains a navigable header.

   **Measured at rev 4** — this is the largest remaining phase by volume, and the measurement makes
   it much less frightening than it looks:

   | document | lines | words | `##` | `###` | cited by |
   |---|---|---|---|---|---|
   | `src/converter/README.md` | 4,890 | 49,306 | 63 | 63 | 23 files |
   | `docs/16-future-ideas.md` | 3,809 | 50,331 | 30 | 118 | 45 files |
   | `src/openness-cli/README.md` | 2,398 | 25,866 | 20 | 60 | 29 files |
   | `CHANGELOG.md` | 2,435 | 25,669 | 16 | 0 | — |
   | **total** | **13,532** | **151,172** | **129** | **241** | **97 citations** |

   🟢 **ZERO anchor links point into any of them** (`…README.md#some-section`: none exist). So a
   split cannot break a deep link, because there are no deep links — only 97 whole-file path
   citations, which are mechanically rewritable and then checkable by grep. The 370 headings give
   natural split points. **This is bulk, not difficulty.**
4. `LICENSE` — see §7.
5. Repo description, topics, social preview.
6. **After any rename or deletion, grep the old filename repo-wide.** This repo cites by literal
   path constantly.

---

### Phase 5 — CI and the runnable demo · ~1 day

1. **GitHub Actions** on `windows-latest`: build and test `converter`, `harness` (minus its two
   TIA-gated projects), `device-guard`, `download-feedback`, `wave-control`, `hmi-cli`,
   `tests/golden`, plus the `tools/` Python checks.
2. **Exclude the TIA-gated projects explicitly and document why** — `Siemens.Engineering.dll` is
   licensed and not redistributable. A visible, explained exclusion reads as competence; a silently
   missing project reads as rot.
3. **The demo. Input already exists and is already sanitized:** `simatic-ml/reference/` and
   `ir/reference/` both return **zero** identifier hits. ⚠️ **But that cleanliness is conditional,
   not free: 12 files in those two corpora match a map *source* key as a whole word**, because
   several keys are ordinary words (the generic controller, input, output and timing names). **If
   Phase 2 step 4 fails to quarantine the 368 bare words, a global apply corrupts the demo corpus
   itself.** Verify the demo input survived untouched as part of Gate 3; do not assume it.
   One command: `to-ir` → `digest` → `review` → `diff` → `to-xml` → `compare` round-trip,
   printing the invariance result. No PLC and no TIA licence required. This is the strongest single
   artifact for D8's audience — lossless IR, mechanical review floor and invariance proof in one run.

**Gate 4 — CI green on a *private* GitHub repo, before the visibility flip.**

---

### Phase 6 — Publish · ~1 h

1. Flip the private GitHub repo to public. **Gates 3 and 4 must both be closed.**
2. Verify this working repository still has **no remote**, or only a private one, so a stray
   `git push` here can never reach the public repo.
3. Record the publication in `data-boundary-audit-backlog.md` as the discharge of `AB-1`'s deferral
   **for the public artifact only**, noting that `M-5` — the pre-commit sweep during live runs —
   **remains open** and still governs this repository.

**Total estimate: ~5–8 working days**, dominated by Phase 2 (triage) and Phase 3 (breakage repair
and re-verification).

---

## 6. What the portfolio piece actually shows

Should drive the README rather than be discovered by a reader:

- **An agent architecture with enforced separation of duties.** Four sub-agents and fourteen skills,
  with a hard routing rule that the orchestrating model may never author or read the domain content
  itself; a third-party assertion enumerator that never sees the implementation, so coverage cannot
  be made unfalsifiable by the author.
- **Safety rails designed as mechanism, not instructions.** A compile gate with a named exit code as
  backstop; a claims registry whose exit codes are a contract; a generator that refuses to emit a
  document with unresolved holes; `EMPTY IS NOT CLEAN` as a cross-cutting principle where exit 2
  means *examined nothing*.
- **Gates that refused their own author, with the refusals kept.** Not a claim of care — a record of
  it. The de-identification gate refused twice before it first passed; the pre-commit sweep refused a
  write-up *of a leak finding* for naming a directory whose path carried the identifier; the tier
  check refused on its first contact with real data, in the one corpus `CLAUDE.md` calls Green; the
  ancestry gate refused a push that would have rewritten public history; and the baseline capture's
  stale-artifact guard caught a re-capture silently missing 65 tests. **The single best finding in
  the repository came out of one of those refusals** — a leaked identifier in a "clean" corpus was
  *suppressing its own rewrite rule repo-wide*, so removing the leak re-armed the scrub.
- **A lossless domain IR** with converter, golden round-trip corpus and network-level invariance
  diffing.
- **Test discipline at scale** — ~5,810 tests across 24 test projects.
- **Documented engineering judgement** — 12 ADRs (a 13th file is the template), four full project audits (six files — two carry separate fixlists), a risk register, and a
  data-boundary regime with a *deferral backlog that names its decision-maker*. The audit backlog is
  the most persuasive single document in the repository and should be linked from the README.

---

## 7. Open items — *(the gating list; Phase 6 closed 2026-09-19 with items 1–8 discharged)*

1. ~~**Licence.**~~ ✅ **CLOSED 2026-09-17 — MIT**, `LICENSE` written, with a note that it grants
   nothing in respect of Siemens TIA Portal, the Openness API or Siemens-shipped artwork.
2. ~~**GitHub account and repository name**~~ ✅ **CLOSED 2026-09-19 — `oisinsmyth/Ladder-AI`**,
   author `oisinsmyth <331246123+oisinsmyth@users.noreply.github.com>` (the **ID-prefixed** noreply
   form, not the legacy `<login>@` one — both are valid, only this one reliably links a commit to
   the account). **The mailmap needed TWO source identities, and the second is the easy one to
   forget:** the employer address accounted for 2,678 author/committer lines, and the `ladder-ai`
   placeholder used while preparing the publication accounted for **66 more**. Mapping only the
   first would have published a history authored by two people, one of them a project name. Final
   sweep over the published object database: **14,559 objects, 0 employer-domain occurrences,
   1 distinct author address.**
3. ~~**`docs/06-lad-conventions.md` C-124/C-128 process description.**~~ ✅ **CLOSED 2026-09-17 —
   ruled a leak, vocabulary generalised.** The `AB-1` flag was right to hesitate and wrong on one
   point: it checked the passage against the *identifier list*, which it passes, rather than against
   the `Live Runs` rule, which bars "a paraphrase specific enough to identify them". Four sites
   across C-124/C-128 named the live job's process step, vessel type and dwell time plus the legacy
   job's equipment domain. **An identifier list is the wrong instrument for this class — it looks for
   names, and this leak was made of nouns that are not names.** Generalised by `AB-1`'s own method,
   with every clause and the whole safety argument intact. Discharge recorded in
   `data-boundary-audit-backlog.md`.
4. ~~**The 43 conflicting map keys.**~~ ✅ **CLOSED — the number was wrong.** 43 counted conflicts
   across *all* map sections; restricted to the sections a text pass can actually use it is **3**,
   and the builder's 4-rung ladder resolves them deterministically. Every candidate is already an
   approved sanitized name, so a conflict is a **consistency** question, not a leak one.
5. ~~**`wip-settling-2026-08-20`.**~~ ✅ **CLOSED — KEPT**, after review. Its three files exist
   nowhere on master and their concepts appear in **zero** tracked files; deleting it would have
   lost ~650 lines with no other copy. Master has moved 21 commits on `LoopRun.cs` since, so it will
   not merge cleanly. It is a preservation branch and is doing its job.
6. ~~**Demo scope** — minimal round-trip, or also `preflight` / `tagstatus` / `cross-check`.~~
   ✅ **CLOSED 2026-09-18 — the plan's own sequence, with one gap fixed.** `to-ir` → `digest` →
   `review` → `diff --only` → `to-xml` → `compare`, then the whole corpus. **The `--only` is the fix:
   the sequence as written used bare `diff`, which exits 0 unconditionally and proves nothing**, so
   the demo would have shown a no-op and called it an invariance check. `preflight`/`tagstatus`/
   `cross-check` were left out deliberately — `cross-check` alone emits hundreds of facts on a real
   corpus, and a demo nobody finishes demonstrates less than a short one.
7. ~~**253 commit-SHA citations across 80 tracked markdown files.**~~ ✅ **CLOSED 2026-09-18 — KEPT
   AND EXPLAINED.** The figure under-scoped by 3×: measured, **768 occurrences, 288 distinct SHAs,
   113 tracked files**, of which 40 are non-markdown and 33 SHAs are cited *only* outside markdown —
   so a markdown-only repair would have missed them. Ruled: leave them. Each is load-bearing in an
   argument — byte budgets and test baselines are justified by pointing at the commit that caused
   them — and replacing them with prose removes the evidence to tidy the citation. `README.md` gains
   a paragraph explaining the rewrite, which is itself of interest to this audience. The one
   **machine-read** SHA, `tests/ci-baseline.json`'s `commit` field, is annotated as pre-rewrite
   provenance; nothing reads it.

8. ~~**Three Gate 2 decisions.**~~ ✅ **ALL THREE CLOSED 2026-09-19, and the builder exits 0** —
   115 rules, 22 `--path-rename` pairs, **0 rows editing source from inside a token**. Decided on
   the evidence in **Rev 14** and **Rev 15**; rows are named by their **invented** replacement, so
   this record carries no live term.

   | # | row | decision | why |
   |---|---|---|---|
   | a | `JOB9003` / `None` | **row deleted** | Two rows declared one 5-character term with different replacements, resolved silently by row order. Both classes derive **identical** variants, so deleting the `site` row is lossless — and it removes a replacement (`None`) that one table re-ordering away becomes a site name in prose and `.ir` tag names |
   | b | `KS` | **`variants` cell, absent form** | Every token containing the term, **in all of history**, is a build-source token its rule would corrupt. No legitimate written form exists here to de-identify, so there is nothing to put in a cell: it names an absent form, emitting no rule while keeping the declaration on record |
   | c | `K15` | **`variants` cell, absent form** | The 3-character term is a **prefix** of a 4-character one with its own row. Rules run longest-first, so the longer rewrites first and already covers every genuine occurrence. Of the 11 candidate forms, **one** was real and contained the longer term; **ten** were base64 blobs in two `hmi/*.html` files |

   🔴 **Each row's sentinel must be distinct.** Giving both discharged rows the same absent form made
   them claim one written form with two replacements, and the variant-collision check refused it —
   correctly, and for the F9 reason: `claims` is built *before* dead variants are dropped, so an
   absent form collides exactly like a present one.

   **What is still short and anchorless is deliberate**: `K150` (4 characters, a whole token in 11
   files), and `K5`/`K7`/`K75`, which make 764 substitutions across 21 files — all in `.ir`, `.xml`
   and `.md`, **none in build source**. That is A8 working as intended, a model designation inside a
   longer product name. Gate 3's per-assembly build comparison is what confirms it.

9. ⚠️ **A TIER CLAIM THAT DOES NOT MATCH ITS CONTENTS — wording FIXED 2026-09-19, and the item grew
   four times when measured. Never a publication blocker.** ➜ **Now tracked as `AB-2` in
   `data-boundary-audit-backlog.md`, with `M-22` as the durable fix.** The two rows below are what
   this entry originally recorded; the real extent is four directories, because
   `docs/13-data-boundary.md`'s own approvals make the same assertion about `gen/PlantAutoControl-bench`
   (6 of 8 files) and `docs/evidence` (**13 of 24, 36 needles, 4 declared**). Corrections were
   written to `spec.md` and to both approvals.
   `gen/_validation/MotorVSDSystem-purpose/spec.md` asserts the validation case is Green-tier,
   invented-names-only. Measured 2026-09-19 against the derived vocabulary:

   | path | files carrying live vocabulary | distinct needles | in `--green` |
   |---|---|---|---|
   | `gen/_validation/MotorVSDSystem-purpose` | 3 of 10 | 1 | no |
   | `ir/PlantAutoControl-bench` — its answer key is a byte copy of this | 2 of 35 | 14, **2 declared** | no |

   **Nothing leaks.** Gate 3 returns T1 0 over exactly this content, and the tooling has never
   believed the claim — neither directory is in the builder's `--green` list, so only the document
   says it. The risk is that a false *already sanitised* label is what lets content be copied
   somewhere the scrub does not run, which is the shape of the 13-file leak `M-5` was filed for.

   Two pieces, and neither was a one-line fix:
   - ✅ **the wording** — `spec.md`'s claim retracted by name and replaced with a measured data-tier
     notice (`lad-coder` dispatch, as `gen/` content requires), and a correction note appended to
     both 2026-07-20 approvals in `docs/13-data-boundary.md`. **Counts and paths only; no term is
     named anywhere** — a record of a leak must not be a copy of it;
   - 🔴 **the practice — still open, and it is the owner's call.** A blind-validation case whose
     answer key is a byte copy of unsanitised bench content is a question about how validation
     cases are *built*, not about one file.

   **The one fact worth carrying out of this:** the scrub builder's `--green` list is already a
   machine-readable tier register, and **none of the four directories has ever been on it.** A
   register and a prose claim disagreed for two months and nothing in the repository ever compared
   them — which is why `M-22` is a check and not a resolution to be more careful.

---

## 8. Risks

| risk | mitigation |
|---|---|
| Blanket substring replace corrupts Green-tier content | Word-boundary regex, longest-first, Gate 2 sign-off |
| A replacement name is itself a leak | Grep every replacement back against the job folders before keeping it |
| 🔴 **CamelCase rules miss lower-hyphen path forms** — the scrub reports success while 93 path names and 211 citations still leak | Emit lower-hyphen stem variants for every key (Phase 2 step 6); run **every** Gate 3 check case-insensitively (§3) |
| `--path-rename` targets disagree with `--replace-text` output | Choose both together, from the same variant set (§3) |
| A global apply corrupts the already-clean demo corpus via ordinary-word keys | Quarantine the 368 bare words (Phase 2 step 4); byte-compare the demo corpus at Gate 3 |
| The scrub silently weakens a test | Phase 0 baseline compared per assembly at Gate 3 |
| History rewrite breaks SHA citations and `ir-hash` records | Enumerated in Phase 3; repair in the clone before publishing |
| `--replace-text` default replacement leaks `***REMOVED***` into prose | Explicit `==>` on every line; verified §4 |
| A later commit here reaches the public repo | D1 + Gate 3 remote check + Phase 6 step 2 |
| The grep says clean but isn't | `AB-1`'s rule: the check is a candidate generator, not a verdict. Human triage is part of Gate 2, not optional |
