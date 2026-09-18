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
| **3 — rewrite history** | 🔴 **RAN 2026-09-18, GATE 3 REFUSED IT — rev 13, rescoped rev 14** | The rewrite works: 1,346 commits, employer domain gone from every one, T1 0 / T2 0, instrument control 1719/1719. **Blocked on the term list, not the tooling** — and the blocker is now **2 rows, not 12**. Rev 13 conflated two facts: the 841 substitutions across 38 files are real but nearly all land in `.ir`/`.xml`/`.md`, where an anchorless rewrite is the job. The builder now refuses these itself and names them by their invented replacement. **Gate 2 decision — a `variants` cell on each of 2 rows — then re-run (~7 min)** |
| **4 — restructure** | ✅ **DONE** | — |
| **5 — CI and demo** | 🟢 **BUILT 2026-09-18, rev 12** | `.github/workflows/ci.yml` + `nightly.yml`, `global.json` (SDK pinned), `demo/run-demo.py`, `tests/ci-baseline.json`. Every step rehearsed locally and green. **Gate 4 — CI green on a private repo — is the one thing left, and it needs the GitHub account (open item 2)** |
| **6 — publish** | ⬜ **not started** | gated on 0, 3 and 5 |

### What is actually built and proven

- **`build-scrub-rules.py`** — 23 tests; emits **119 rules + 22 path-renames** from a full-history
  run.
- **`verify-scrub.py`** — the Gate 3 oracle. Returns an **earned zero on a real `filter-repo`
  rewrite** while **still failing the unscrubbed source** (T1 10, T2 79). Its instrument control
  reports **1719 of 1719** needles matching themselves.
- **The trial rewrite recipe, proven:** `clone --no-local` → `filter-repo --replace-text
  --replace-message @path-renames.args --mailmap` → delete all but the publishing branch →
  `reflog expire --all --expire=now && gc --prune=now` → verify. ~7 min rewrite, ~30 s verify.

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
- **A lossless domain IR** with converter, golden round-trip corpus and network-level invariance
  diffing.
- **Test discipline at scale** — ~5,810 tests across 24 test projects.
- **Documented engineering judgement** — 12 ADRs (a 13th file is the template), four full project audits (six files — two carry separate fixlists), a risk register, and a
  data-boundary regime with a *deferral backlog that names its decision-maker*. The audit backlog is
  the most persuasive single document in the repository and should be linked from the README.

---

## 7. Open items — must close before Phase 6

1. ~~**Licence.**~~ ✅ **CLOSED 2026-09-17 — MIT**, `LICENSE` written, with a note that it grants
   nothing in respect of Siemens TIA Portal, the Openness API or Siemens-shipped artwork.
2. **GitHub account and repository name** — and with it **the exact GitHub noreply address** the
   `--mailmap` maps the employer-domain author identity to. The trial rewrite used a placeholder.
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

8. 🔴 **THREE GATE 2 DECISIONS — the only thing standing between here and a Phase 3 artifact.**
   The builder refuses until each is resolved and names them by their **invented** replacement, so
   they can be discussed without the live terms being written down. Evidence per row: **Rev 14**.

   | # | row | what to decide |
   |---|---|---|
   | a | `JOB9003` / `None` | Two rows declare the same 5-character term, as `jobcode` and as `site`, with different replacements. **Delete one, or make them agree.** The `None` replacement should not survive under any ordering. |
   | b | `KS` | 3 characters, contains a separator, and its rule edits **6 build-source tokens from inside** while never being a source token itself. This is the one that broke the build. **Give the row a `variants` cell** naming the longer forms that should be rewritten. |
   | c | `K15` | Same shape, 1 source token. 13 of its 14 embedded hits are outside build source and may well be legitimate. **A `variants` cell**, or a narrower scope. |

   After these, `python tools/build-scrub-rules.py` should exit 0, and the rewrite re-runs in about
   seven minutes. **Nothing else in the pipeline is waiting on anything.**

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
