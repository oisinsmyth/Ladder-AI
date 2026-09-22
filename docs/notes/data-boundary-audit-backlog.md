# DATA-BOUNDARY AUDIT BACKLOG — findings deferred, not dismissed

**Opened 2026-08-18.** This file exists so a finding that was consciously *not acted on* still meets
the next audit. Everything here is a **deferral with a named decision-maker**, never a backlog item
that quietly accumulated.

> 🔴 **THIS DOCUMENT DELIBERATELY DOES NOT LIST THE IDENTIFIERS IT IS ABOUT.**
> Writing them down here would commit the very strings the finding is about, into a new committed
> file, in the name of recording that they are committed. **The finding is reproducible by running
> the check below**, which regenerates the list on demand from the job folder — where the vocabulary
> already lives and is already ignored. *A record of a leak must not be a copy of it.*

---

## AB-1. Plant-specific identifiers in committed source — ✅ REMEDIATED 2026-08-27 (deferred 2026-08-18)

> **Status.** Deferred by the owner on 2026-08-18; **cleanup authorised by the owner and performed on
> 2026-08-27** — *"fix it how you see fit, don't break it."* **285 sites across 49 tracked files**
> were rewritten to invented vocabulary. The section is kept, not deleted: a finding is never
> silently removed, the "how this got in" analysis below is the part worth keeping, and the check is
> the thing that has to keep being run. **The remediation record is at the end of this section.**

**Found:** a repo-wide sweep on 2026-08-18 matched **82 sites across 15 tracked files** against the
vocabulary of the live job `JOB9004`. Bare job codes are **not** the issue — `docs/13-data-boundary.md`
permits those explicitly, and they were deliberately restored the same day. The issue is
**block, type, DB and member names** specific to that production plant.

**Where.** The file list is stable and is safe to record, because a path names no organisation:

```
CLAUDE.md
CHANGELOG.md
docs/16-future-ideas.md
docs/notes/compile-error-playbook.md
src/converter/Converter/CrossCheck/ProjectUsageGraph.cs
src/converter/Converter/Ir/SidecarSynthesizer.cs
src/converter/Converter/Program.cs
src/converter/Converter/UndrivenScan/UndrivenScanRunner.cs
src/converter/Converter.Tests/ConvertOutputSafetyTests.cs
src/converter/Converter.Tests/DbConverterTests.cs
src/converter/Converter.Tests/MultiInstanceCallTests.cs
src/converter/Converter.Tests/NamedTypeExpansionReadBackTests.cs
src/converter/README.md
src/device-guard/DeviceGuard.Tests/DeviceWriteGuardTests.cs
src/harness/Harness.Tests/VectorRunnerTests.cs
```

**Owner's decision (2026-08-18):** *do not remediate now; record it for the next audit's attention.*
Grounds, as far as they were stated: no git remote is configured, so nothing has left this machine,
and the remediation touches `CLAUDE.md` and `CHANGELOG.md` — a rewrite with real cost and real risk
of destroying load-bearing explanation. **That is a deferral of the CLEANUP. It is not a finding that
the content is acceptable, and it must not be read as one.**

### How this got in, which is the part worth keeping

Every lane involved was briefed that nothing from the job folder is committed, and every lane honoured
that **for artifacts**. The identifiers arrived in **explanatory comments** — as the concrete case a
comment reaches for when it explains a defect it actually measured. ***That does not feel like job
content; it feels like rigour.*** The same mechanism produced an earlier, smaller instance
(13 files, remediated 2026-08-17) — so this is a **recurrence, not a one-off**, and the recurrence is
the argument for a mechanical check rather than a briefing.

Note also *where* it recurred: the 2026-08-17 remediation swept `src/harness/`, because that is where
that day's work happened. This finding is mostly in `src/converter/` and `docs/`. **A sweep scoped to
the lane that was working catches the lane; only a repo-wide sweep catches the repo.**

### The check — re-run it, do not trust this list's age

🔴 **A job folder has MORE THAN ONE IR directory** — a rework, a second project, a `-new` — and step
1 must take *all* of them. On 2026-08-27 the job that produced this finding had two, and a sweep
against only the older one would have reported a clean repo. `ls "Live Runs/<JOB>"` first, always.

```bash
# 1. The job's vocabulary, from EVERY ir dir in the job folder (gitignored — never enters the repo)
ls "Live Runs/<JOB>"/*/ir*/*.ir "Live Runs/<JOB>"/ir*/*.ir 2>/dev/null \
  | xargs -n1 basename | sed 's/\.ir$//' | sort -u > /tmp/job.txt

# 2. Everything Green-tier, so conventional names do not read as leaks
{ ls ir/*/ patterns/*/ gen/*/ simatic-ml/*/ ; } | sed 's/\.[a-z]*$//' | sort -u > /tmp/green.txt

# 3. Names the job has and the Green corpora do not
comm -23 /tmp/job.txt /tmp/green.txt | awk 'length($0)>=8' > /tmp/candidates.txt

# 4. Whole-word match against TRACKED files only
grep -n -w -F -f /tmp/candidates.txt $(git ls-files '*.cs' '*.md' '*.py' '*.ps1' '*.json')
```

**Steps 1–4 catch BLOCK names only, and that is not the whole finding.** Two more passes are needed,
and both found real sites on 2026-08-27 that steps 1–4 could not:

```bash
# 5. MEMBER names, from inside the .ir files rather than their filenames. Very noisy — a member
#    called `Heartbeat` collides with ordinary code — so triage it hard. The distinctive ones are
#    invented compound names no second author would coin independently.
grep -ohE '^[ \t]+[A-Za-z_][A-Za-z0-9_]{7,}[ \t]*:' "<every ir dir>"/*.ir | tr -d ' \t:' | sort -u

# 6. CASE-INSENSITIVE, over .html/.py too, on the equipment STEMS the triage in step 3 identified.
#    HMI element ids and screen titles are lower-case and hyphenated, so no identifier-shaped
#    pattern reaches them; they are invisible to every step above.
grep -rn -i -E '<stem>|<stem>' --include=*.cs --include=*.md --include=*.txt \
   --include=*.py --include=*.html --include=*.json .
```

🔴 **AND THE REPLACEMENT VOCABULARY IS PART OF THE CHECK.** Before keeping an invented name, grep the
job folder for it. On 2026-08-27 one replacement already existed verbatim in the job's own IR — a
fix that would have introduced a fresh leak while closing an old one.

### ⚠️ THE CHECK IS A CANDIDATE GENERATOR, NOT A VERDICT — and it cannot be made into one

Step 3 yields names absent from every Green corpus. **That is not the same as "specific to a
site".** Measured on this run: names like the DOL-motor block, the motor UDT, and the alarm
category FCs came out as "job-only" purely because no `.ir` file of that name is committed — yet they
are **conventional**, some of them *mandated by the convention rules themselves*, and one appears in
the S6 sandbox's own architecture document, written weeks before this job existed.

Separating *"this job happens to use the standard name for a motor starter"* from *"this is the
site's own equipment"* is **a judgement about what identifies a plant**, and no name-matching rule
reaches it. So:

- **Treat the output as a worklist to triage, never as a count to report.** A raw match total
  overstates the leak, and quoting one as a headline would be its own small dishonesty.
- **The plant-specific subset is the real finding**, and on this run it was roughly half the
  candidates. Establish it by asking, per name, *"could this plausibly be the standard name for this
  kind of object on somebody else's plant?"* — if yes it is convention, if no it is the site's.
  The subset is not written down here for the reason given at the top of this file.
- Same shape as the rest of this project's tooling: **facts, not verdicts.** Automate the assembly of
  the inputs; leave the call to a person.

### What would actually close this

Not a bigger grep. The durable fix is the one recorded as `M-5` in
[`mechanisation-backlog.md`](mechanisation-backlog.md): **run the sweep before any commit during a
live run**, so the question is asked while the comment is being written and the author still
remembers whether the concrete case was load-bearing. Retrofitting it afterwards means re-deriving
intent from prose, which is why this deferral is cheap to record and expensive to discharge.

**The remediation below discharged the 2026-08-18 instance. It did NOT close AB-1's cause** — `M-5`
is still open, and until it lands the next live run recurs the same way for the same reason.

### ✅ Remediation, 2026-08-27 — authorised by the owner and performed

**Scale.** **285 sites across 49 tracked files**, covering 30 block/type/DB names, 12 member and HMI
tag names, and 4 prose descriptions of the plant's process. Against the 2026-08-18 figure of 82/15:
the finding was **larger than recorded**, because work landed in the nine days between, and because
this sweep added two passes the original did not have (a member-name pass, and a case-insensitive
pass over `.html`/`.py` that caught HMI tag fixtures no `.ir` filename would ever match). The
original file list was a floor, not a census — which is the whole reason it says not to trust its age.

**Method — SUBSTITUTE VOCABULARY, NEVER DELETE EXPLANATION.** Each identifier was replaced with an
invented, obviously-synthetic one; every comment kept its measurement, its date, its reasoning and
its shape. Where the replacement made a neighbouring word stale (a valve described by what it did on
the plant), the *word* was generalised and the sentence left standing. **No paragraph was removed**,
and no comment lost its concrete case — it kept a concrete case with a different name on it. Where a
file already carried a sanitised fixture, the new vocabulary was aligned to it rather than invented
again, so comment and fixture now agree where they previously disagreed.

**Traps that were live on this run, all three measured rather than anticipated:**

- **A blanket token replace would have corrupted Green-tier content.** One equipment word in the job
  vocabulary is *also* the name of a machine in the reference project, in `patterns/`, in
  `CHANGELOG.md` and in `docs/13-data-boundary.md`'s own worked example of this exact confusion. Only
  the composite identifiers were rewritten; the bare word was left alone deliberately.
- **A replacement can BE a leak.** One invented member name turned out to exist verbatim in the job's
  own IR. Every replacement was checked back against the job folder before it was kept, and the
  colliding one was changed. **Choosing the new vocabulary is part of the check, not after it.**
- **One test asserted an order that the rename made trivially true.** `DeclaredAreas` sorts, and the
  old fixture names happened to make sorted ≠ first-seen. Renaming collapsed the two. The fixture was
  adjusted so the assertion still distinguishes them — *a rename must not silently weaken a test.*

**Judged NOT leaks, and deliberately left untouched** — each is either mandated by
`docs/06-lad-conventions.md`, present in Green-tier corpora, or an ordinary industrial word the
tooling invented for its own fixtures: the conventional alarm-category FCs, the buffer DBs, the motor
type and DOL pattern block, the startup OB, the generic vessel/recipe/weigher test fixtures in
`wave-control`, `harness` and `hmi-cli`, the harness's own generated stimulus vocabulary (**the repo
is that vocabulary's origin, not the job**), and the HMI symbol-library exercise in `hmi/`, whose
subject genuinely is the drawing of a generic industrial shape.

**Verification.** The check re-run at the top of this section returns only that conventional
residual — an **earned** zero: it compared 1,656 tracked files against the vocabulary of both of the
job's IR directories. `converter` 1,783 passed; `harness` 3,077 passed across 16 assemblies;
`openness-cli` 871; `device-guard` 65 + 12; `hmi-cli` 161. `tools/check-file-budgets.py` passed with
no ceiling raised. Line endings and BOM state unchanged (the substitution was applied byte-wise).

**One thing was found and NOT acted on**, because it is outside this finding's scope and acting on it
alone would have edited a convention rule's rationale: `docs/06-lad-conventions.md` illustrates
C-124/C-128 with a **process description** — a named dwell time and a named measurement history —
rather than with any identifier. It names no block, tag, equipment or site; it was not on the
finding's file list; and it reads as the engineer's own batch-plant reasoning. **Flagged for the
owner to rule on, not decided here** — this section exists to record that it was seen.

### ✅ RULED AND DISCHARGED 2026-09-17 — it was a leak, and the vocabulary is generalised

**Owner ruling: generalise the vocabulary, keep the reasoning.**

The 2026-08-27 flag was right to hesitate and wrong on one point of fact. It records that the passage
*"names no block, tag, equipment or site"* — true — and concludes it reads as the engineer's own
reasoning. It does. But re-read against the `Live Runs` rule rather than against the identifier list,
it fails: that rule bars **"a paraphrase specific enough to identify them"** and states that **"a
lesson learned written in the job's own vocabulary is still a leak."** The passage named the live
job's process step, its vessel type and a specific dwell time, plus the legacy job's equipment
domain — four sites across C-124 and C-128. Together those describe a plant precisely enough that
anyone in the industry would place it. **An identifier list is the wrong instrument for this class:
it is looking for names, and this leak is made of nouns that are not names.**

**What changed, by `AB-1`'s own method — SUBSTITUTE VOCABULARY, NEVER DELETE EXPLANATION:** the
process step became a generic multi-hour irreversible phase, the measurement history became a generic
one, the equipment domain became a generic size-reduction unit and main drive, and the per-equipment
qualifier became a generic process unit. **Every clause, both amendments, all three conditions and
the safety argument are untouched.** The rule still argues from a concrete case — the contrast that
carries it, a long cycle destroyed by a two-second dip, survives intact — and the case now has a
different name on it.

**Residual check:** four matches remain repo-wide and all four are adjudicated non-leaks, three of
them by this document's own 2026-08-27 list. Two are a Green-tier sandbox block name from
`test-project001` (the *invented* vocabulary), one is a generic per-vessel alarm example already
named in "Judged NOT leaks", and one is the ordinary English word *steeped* in an unrelated sentence
about LLMs. **No mechanical check would have found this finding, and none would have cleared it
either** — which is the whole reason it sat open for three weeks.

---

## AB-2. Four directories documented as Green, and all four carry live vocabulary — 🔴 OPEN (found 2026-09-19)

> **Status.** Found while closing open item 9 of the publication plan, which had recorded a single
> false tier claim in one file. Measuring it properly found the same defect in three more places and
> a common cause. **Not a publication blocker** and **nothing is leaking** — see *What is not wrong*
> below. Decision-maker: project owner. Durable fix filed as **M-22**.

**The finding.** `docs/13-data-boundary.md`'s two 2026-07-20 per-project approvals mandate
sanitize-to-Green-first and then assert the outcome — *"Every downstream artifact stays Green and
committable"*, *"every committed artifact ... is Green"*. `gen/_validation/MotorVSDSystem-purpose/spec.md`
asserted the same thing in four words. Measured against this project's derived de-identification
vocabulary, over tracked content at `HEAD`:

| directory | tracked files carrying it | distinct hunted needles | of which DECLARED (T1) |
|---|---|---|---|
| `gen/_validation/MotorVSDSystem-purpose` | 3 of 10 | 1 | 0 |
| the bench IR corpus under `ir/` | 2 of 35 | 14 | 2 |
| its derived register under `gen/` | 6 of 8 | 3 | 1 |
| `docs/evidence` | 13 of 24 | 36 | 4 |

> 🔴 **Two rows are described rather than named, and the reason is this entry's own subject.** Those
> two directories are **named after the live block they were derived from**, so their paths carry an
> inferred (T2) identifier — writing them here would have committed the very string the finding is
> about, into this file, in the name of recording that it is committed. That is the failure this
> document's header predicts in its second paragraph, and **`M-5` refused the commit that tried it.**
> The exact paths are in `docs/13-data-boundary.md`'s two 2026-07-20 approvals, which already name
> the block and are the Amber-access record where that belongs.

Per this document's own rule, **the terms are not listed**; the measurement is reproducible from the
gitignored vocabulary with the scrub tooling.

**What is NOT wrong, stated first because it changes how urgent this reads.** The *approvals* were
correctly scoped and are not withdrawn — they authorised an Amber read in order to export and
sanitize, and that is what happened. The published artifact is clean: Gate 3 returns **T1 0 / T2 0**
over exactly this content, because the scrub rewrites it on the way out. No restricted data has left
this machine. **The defect is the label, not the data.**

**Why a label is worth an AB entry.** A false *already sanitised* mark is precisely what licenses a
copy into somewhere the scrub does not run — into `patterns/`, a committed doc, a test fixture, a
published artifact. That is the mechanism of **AB-1**, arriving by a different road: AB-1 was content
that nobody checked; this is content that a document said had already been checked.

**How it survived, and it is written in the entry that failed.** The second of the two approvals
names its own enforcement: *"a map-key leakage grep before commit"*. That is a per-lane instrument, and AB-1's
central finding is that **a per-lane check catches the lane's own work; only a repo-wide sweep
catches the repo.** The control was the wrong shape, not carelessly applied — which is exactly why
the answer is M-22 and not "be more careful".

**The sharpest fact in the finding:** the tooling never believed the claim. The scrub builder's
`--green` list is a machine-readable tier register, and **none of the four directories has ever been
on it.** A register and a prose claim disagreed for two months and nothing ever compared them.

### AB-2 addendum — M-22 was built, and it found a FIFTH directory on its first run

**`gen/test-project001` — declared Green in `CLAUDE.md` itself, not in a forgotten note.** The
Environment section reads "Green-tier throughout". Measured by `tools/check-green-claims.py`: a
**5-character job code, DECLARED (T1), in three files**, plus an inferred map key in a fourth. It is
the one corpus in this repository that everything treats as clean by construction, and it is on
**both** `--green` lists — so unlike the four above, the tooling *did* believe this claim.

Nothing was leaking: the scrub rewrites it and Gate 3 returns T1 0 on the published artifact. **The
label was wrong, not the data** — the same finding as the four above, arrived at from the opposite
direction.

**✅ REMEDIATED by substitution, `AB-1`'s method — SUBSTITUTE VOCABULARY, NEVER DELETE
EXPLANATION.** Six occurrences across four files, each replaced with the invented name the maps and
term list already specify; every sentence, provenance clause and JSON field survived unchanged. The
embedded occurrence was replaced as a whole token so it still reads as one identifier rather than a
splice, and the map key's replacement is the name the rest of the sandbox already uses, so the
document is now *consistent* with the corpus it describes instead of inconsistent with it. Performed
through a `lad-coder` dispatch (`gen/` content, hard rule 8); diff verified here before commit.
`check-green-claims.py` now exits 0.

**What makes this the most useful thing in the entry:** a gate that only ever passes proves nothing,
and this one refused on its first contact with the thing it was built for. The register would have
been a nicely-documented no-op otherwise.

### AB-2 addendum 2 — the practice question, audited 2026-09-20. **Verdict: UNVERIFIED.**

The open item was framed as a *data-tier* question. Audited as a **validity** question — could the
blind generator have seen the answer key? — it comes back **neither cleared nor impeached**, and the
reasons on both sides are worth keeping.

**The hypothesis that prompted the audit was WRONG, and refuted on timing.** I suspected the tracked
`ir/` twin of the quarantined key. It was added **8½ hours after the run commit**, in a separate
later campaign; `git ls-tree` at the run commit returns zero entries for that directory. The
duplicate is real and the quarantine is genuinely defeated by it *today* — but it did not exist on
the day and cannot have been read then.

**A different duplicate did exist, and nothing recorded it.** A byte-identical sanitized copy of the
answer key sat in the gitignored `scratch/` directory from roughly an hour before the generated
block was authored. `scratch/` is fenced from **commit**, exactly as `answerkey/` is — but the
quarantine's stated goal, in its own commit message, was that the generator *"never be able to
discover it in the tree"*, and **a gitignore does not remove a file from the tree.** The commit that
created the quarantine discloses that `scratch/` held raw exports; it does not disclose that it also
held a full copy of the key.

**Why it is NOT impeached.** The output's shape is affirmative evidence against copying, and it is
independent of any agent's self-report: the generator **missed a feature the key contains**,
**avoided reproducing a defect the key has**, and logged a design question that could not arise if
the key were visible.

**Why it is NOT cleared.** `telemetry.log` — the only genuine run record — has **no field for
readable scope and never did**. Its schema is `date | skill | wall_clock | portal_roundtrips |
tokens | outcome | note`. Everything else is self-reported prose: the spec grants a three-file
permission, the architecture note asserts the key "was never accessed". Neither records what the
context actually contained.

🔴 **The larger finding, which the audit was not sent to look for: THE SPEC WAS WRITTEN FROM THE
ANSWER KEY, and says so** — the non-transcribed items were *"grounded from the answer key"*. So even
under perfect generator blindness this measures *"can the skill reconstruct from a key-derived
spec"*, not *"from an independent spec"*. Later bench re-runs logged exactly this circularity as
**blocking**; this run logged no such caveat. **The pipeline got stricter after this result was
banked**, which is the honest way to say the result predates the standard it is cited under.

**Also flagged, not concluded:** the compile gate for this validation was claimed, **retracted fifty
minutes later**, then claimed again, and no `sanity-check` output backing the final claim was found.
Per hard rule 4 an assertion is not the gate. Cutting the other way: a record that publicly retracts
its own overstatement is more credible on the claims it did not retract.

**What this changes.** The practice question is no longer only about tiers. The durable fix is
**M-23**: a validation case should declare its generator's readable scope somewhere a tool can check,
because today that is unrecordable by construction. Deduplicating the key (so it exists once) and
sanitizing the committed corpora remain worth doing; neither is urgent, and nothing leaks.

**Remediated so far:** `spec.md`'s claim retracted and corrected (2026-09-19); a correction note
appended to both approvals in `docs/13-data-boundary.md`; `gen/test-project001` sanitized;
the validity question audited and recorded above (2026-09-20); **the answer key deduplicated and
pinned (3-C, 2026-09-21 — addendum 3 below).**

### AB-2 addendum 4 — 3-D RESOLVED **NOT TO SANITIZE**, 2026-09-21, on measurement

`3-D` was "sanitize `ir/PlantAutoControl-bench`, `gen/PlantAutoControl-bench` and `docs/evidence`, then
register them Green". **Measured, then declined.** The measurements, so the decision can be
disagreed with rather than merely trusted:

| directory | tracked files | files carrying live vocabulary | distinct T1 | distinct T2 |
|---|---|---|---|---|
| `ir/PlantAutoControl-bench` | 35 | 33 | 2 | 19 |
| `gen/PlantAutoControl-bench` | 8 | 8 | 1 | 32 |
| `docs/evidence` | 24 | 20 | 4 | 54 |

**What it would cost, measured not estimated:**

1. 🔴 **It would deliberately do the thing `3-C` was built the same day to detect.** Two of the
   three directories **contain registered answer keys** — `ir/PlantAutoControl-bench/MotorVSDSystem.ir` and
   `docs/evidence/PlantAutoControl-answerkey/PlantAutoControl.ir`. Sanitizing them changes what two
   validation cases grade against and breaks both pins. Building a pin to stop ground truth moving
   silently, then moving it deliberately hours later, is incoherent.
2. **It would break the converter.** The sealed key's own block name and its corpus directory are
   referenced from **9 tracked files under `src/converter/`**, including four test classes. A
   corpus block name is an **interface**, not just content — which is exactly why renaming one is
   not a local edit. *(Named by role here, not spelled: `M-5` refused the first draft of this
   paragraph for writing the block name out, and it was right to.)*
3. **It would falsify the record.** `docs/evidence` is the write-up of work that actually happened.
   Rewriting the names in it does not de-identify a job; it produces a history that says something
   was done to equipment that never existed.
4. **43 of the 61 files are IR or `gen/` content**, so every edit is a `lad-coder` dispatch under
   hard rule 8 — dozens of chances to introduce an error into validation corpora.

**What it would buy: nothing for the published artifact.** The publication scrub already rewrites
every one of these terms, and Gate 3 has returned an earned zero over the whole object database
**six cycles running**. The benefit is defence-in-depth on a *private* source tree that is never
published. That is real, but it is not worth items 1–3.

**And registering them Green would ADD risk, not remove it.** The builder withholds a rewrite rule
for any term present in Green content, repo-wide — the exact mechanism that made a leak suppress
its own rule in cycle 3. Every directory added to the Green list widens that surface.

**Therefore:** the three directories stay **un-Green and un-sanitized**, which is already what
`tools/green-claims.txt` records and why they are deliberately absent from it. **This is a
judgement, not a measurement**, and it is reversible: if the owner wants the source tree sanitized
regardless, the work is well-defined and the first step is re-pinning both answer keys in
`tools/answer-keys.txt` **in their own commit**, so the ground truth moves loudly.

### AB-2 addendum 3 — the key is pinned, 2026-09-21. **3-C, and the census was wrong.**

The interim fix was scoped as *"a quarantined key and a tracked corpus file are byte-identical"*.
Building the register took the census that sentence assumed, and **the census disagreed with it.**

**There were four copies of that key, not two.** The tracked bench file, the quarantined
`answerkey/` one, and **two more in gitignored `scratch/`** — one of them the very file addendum 2
identified as the copy that existed on the day. Plus 22 checkouts of the same commit under
`.claude/worktrees/`, which are not independent copies but are readable all the same.

🔴 **A SECOND KEY HAD THE SAME DEFECT AND NOTHING HAD EVER MENTIONED IT.** The sealed
`PlantAutoControl-bench` key — *"frozen, never edited, the ground truth"* — had its own gitignored
working copy. It was found by the act of writing the register down, which is the argument for
registers over prose in a single line. Nobody went looking for it; the format asked a question and
the answer fell out.

**"Exists once" turned out to be the wrong target, and that is the finding.** The canonical copy is
**tracked reuse corpus that three other validation runs cite as ground truth**, so it can be neither
deleted nor hidden; and deleting an ignored working copy does not stop the next one appearing. What
is achievable — and what was built — is **one DECLARED copy, pinned by `sha256`, with every other
copy counted out loud on every run.** The case's own duplicate was removed (verified byte-identical
to the pin first, via a `lad-coder` dispatch, hard rule 8) and replaced by a pointer that states
plainly that the quarantine is defeated and cannot be repaired.

**What this does NOT do, stated because the opposite is the tempting reading:** it does not make the
blind run any more verifiable. Ground truth can no longer move in silence. That is all. The
checker's own clean verdict refuses the stronger claim in those words — *"THIS PROVES IDENTITY OVER
A REGISTER, NOT BLINDNESS"* — because two of the three registered keys are readable corpus in plain
sight, and no hash changes that. **M-23 is not one inch smaller.**

**Deliberately not done:** the ignored working copies in `scratch/` were **not** deleted. Three
reasons, in order of weight. A deletion is a one-off and the count is permanent, so the check is
worth more than the tidy-up. `scratch/PlantAutoControl-green/` is half unique — its 35 `.xml` files exist
nowhere else — so gutting its `.ir` half leaves a corpus that is neither one thing nor the other.
And one of those copies is the physical evidence addendum 2's timing argument rests on; destroying
it to improve a count would be destroying the record to improve the appearance of the record.
The fence around `answerkey/` was also **not** narrowed to publish the pointer: it is a
directory-level ignore, so the pointer is local-only, and weakening a quarantine's ignore rule to
make a document more visible is the wrong trade when the tracked register already carries the story.
**Still open:** the practice question —
a blind-validation answer key that is a byte-identical copy of unsanitised bench content is a
question about how validation cases are *built*, not about any one file. Owner's call. **3-C did
not close this and was not meant to:** it pinned the file, and the practice question is about
whether a case may take its ground truth from corpus the generator is entitled to read at all.

---

## PUBLICATION RECORD — 2026-09-19

**Discharges `AB-1`'s deferral FOR THE PUBLIC ARTIFACT ONLY.** Phase 6 step 3 of
`docs/notes/portfolio-publication-plan.md`.

`Ladder-AI` was published to `github.com/oisinsmyth/Ladder-AI` at commit
`bb6d61c6544d7096e62fb8459a66b2bfde648db4`. **Remote `main`, the local clone's `HEAD`, and Gate 3's
verdict stamp are the same twelve hex characters** — what is public is the object graph the gate
returned a zero over, not a rebuild of it.

| gate | result |
|---|---|
| T1 DECLARED / T2 whole-token | **0 / 0** |
| T3 over-scrub | 0 fell |
| must-survive | 5 of 5 |
| instrument control | 1,719 of 1,719 |
| build | 23 assemblies, 5,894 → 5,894, cannotBuild 2 before / 2 after |
| scope | 1,367 commits · 6,387 blobs · 1,830 paths · 7 unsearchable blobs |
| final identity sweep | 14,559 objects · **0** employer-domain occurrences · **1** author address |
| **Gate 4 — CI** | **green** on that exact sha; 11 steps, all success |

**`M-5` REMAINS OPEN AND STILL GOVERNS THIS REPOSITORY.** Publishing a clean artifact says nothing
about the working repo it was cut from: this repository continues to carry live-job material, and
the pre-commit sweep is the control on it. The discharge above is scoped to the published artifact
and to nothing else.

Verified at the same time: this working repository has **no git remote at all**, so no push from
here can reach the public repository by accident.
