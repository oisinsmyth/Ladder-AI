# MECHANISATION BACKLOG — where the process is still done by hand, and what it costs

**Opened 2026-08-17**, at the end of the first end-to-end conformance run against a real job. Every
item below is grounded in something that **actually cost time or produced a defect that day** — this is
not a list of things that feel tedious. Where a cost is quoted it was measured.

> **How to read the ranking.** The unit that matters is not "minutes of typing" but **ROUND TRIPS
> BETWEEN PARTIES**. The pipeline's independence rules (D6, M1) mean the enumerator, the vector author,
> the model author and the coordinator deliberately cannot read each other's documents — so every
> disagreement between two artifacts costs a full hand-back, and a hand-back costs 10–20 minutes
> because the lane re-reads its context. *An automation that turns four hand-backs into one is worth
> more than one that saves an hour of typing.*

> **What puts an item on this page — and it is not tedium.** ***THE TRIGGER IS "THIS WAS FOUND BY
> LUCK."*** The most expensive defect of 2026-08-18 surfaced because somebody happened to run a
> search: silent, fatal to the block it sat in, and **structurally detectable** — and no mechanised
> check covered it. There is one now, and it examines every block on every run. *A defect a person
> found by chance is one the mechanical floor should have been finding on schedule.* So the question
> to ask of any find is not how long it took to fix, but ***what would have found it if nobody had
> looked?*** When the answer is *nothing*, that is the backlog item — regardless of how quick the fix
> turned out to be.

---

## STATUS — added 2026-08-23, because this page had none and four items had already shipped

🔴 **This backlog ran for six days with no status field at all.** Every one of its 21 items read as
open, including the one marked **HIGHEST LEVERAGE**, which was built. Nobody was misled by a wrong
status; they were misled by the *absence* of one — the page invited a reader to re-derive what was
already done, and the item that genuinely was still open (**M-6**) sat in the same undifferentiated
list as three that were not. **Leaving four closed items unmarked is what makes the fifth
unfindable.**

| item | status | evidence — a source line, not another document |
|---|---|---|
| **M-1** — one join report instead of four hand-backs | ✅ **DONE** | slot id → `Harness.Results/SlotJoin.cs`; vector targets → `Harness.Map/MirrorValueFit.cs` (`CheckAll`); `SignalJoin` took the settling role 2026-08-20 |
| **M-6** — measure the scan time rather than assuming a band | ✅ **DONE 2026-08-23** | `Harness.Wire/ScanPeriodMeter.cs`, emitted as `scanPeriod` in the run JSON. ⚠️ It **measures and compares**; it does **not** replace `WireTiming.ScanPeriodMs`, and M-13 is why |
| **M-8** — derive the assertion-bounds projection | ✅ **DONE** | `Harness.Results/BoundsCurrency.cs` |
| **M-9** — settling per index, not only the wave's last | ✅ **DONE** | `Harness.Loop.Tests/SettlingEveryIndexTests.cs`; confirmed on the controller 2026-08-22, three vectors all `Settled` |
| **M-11** — retentive `%M` extent | 🚫 **NOT MECHANISABLE** | no Openness path reads the PLC-tags retain setting; it needs a person in TIA. Recorded so it is not re-attempted |
| everything else | **open** | — |

⚠️ **The rule this establishes: an item here gets a status line in the same commit that closes it.**
A backlog whose entries only ever get added is a backlog that stops being read — and this page's own
trigger for an entry is *"this was found by luck"*, which is precisely what an unread page produces.

---

## TIER 1 — the ones that paid for themselves the day they were found

### M-1. One join report instead of four hand-backs 🔴 HIGHEST LEVERAGE

**What happened.** Four separate name disagreements between the submission and the binding were
discovered ONE AT A TIME, each costing a round trip: the slot id; the vector-target prefix; the
observable vocabulary; and the completion signal. Same seam, same root cause, four hand-backs.

**Why it happens and why it is not anybody's fault.** The submission speaks the SPECIFICATION's
vocabulary and the binding speaks the CONTROLLER's, and the two documents are written by parties who
must not read each other. **The disagreement is structural, expected, and the coordinator is the only
party that can resolve it** — so the only question is whether they learn about all of it at once.

**Mechanise:** one check that reports EVERY unjoined name across all four categories in a single pass,
with the candidate set from the other document beside each. `SignalJoin` (built 2026-08-17) does this
for expectations and the completion signal and immediately caught a real one — **extend it to the slot
id and the vector targets, and run all four before the gate.**

**Measured saving: 4 round trips → 1.** The single highest-value item on this page.

### M-2. Binary-currency check before any run 🔴 BIT US TWICE IN ONE DAY

**What happened.** Twice, a fix was committed and green in Debug while the Release binary the run
actually invoked was hours old. Once it made a *passing* gate report as a refusal, and the state that
was described to the owner did not reproduce. Cost: one wrong diagnosis and one wasted gate cycle.
A THIRD instance the same day, in a different shape: a rebuilt binary TIA had not approved turned a
35-second download into a 10-minute timeout.

***A COMMIT IS NOT A DEPLOYMENT.***

**Mechanise:** before a run, compare the invoked binary's timestamp against the newest source under
the assemblies it loads, and say so. It need not refuse — a warning naming the file would have caught
all three. Pair it with the existing approval `-Status` probe for the Portal-side binaries.

🔴 **AND TWICE MORE ON 2026-08-18, IN TWO SHAPES THE TIMESTAMP CHECK ABOVE DOES NOT COVER.** This is
now a repeated class rather than a recurring instance, and each shape has a different last mile:

- **Built, but not into the configuration anyone runs.** A rule was written, tested and committed —
  and only the **Debug** binary was built, while every consumer invokes **Release**. The commit is
  green, the tests are green, the source is right, and *the running tool does not have the rule.*
- **Loaded into the project, but never onto the device.** A repair was imported and compiled into
  the project and ***never downloaded***. Everything in the engineering tool agreed it was there.
  **It was caught only by a build-stamp hash read back off the controller** — i.e. by an artifact
  that can only be produced by the thing actually running.

➜ ***THE GENERAL FORM: EVERY HAND-OFF IN THE CHAIN `source → build → configuration → project →
device` IS A PLACE THE CHANGE CAN STOP, AND EACH ONE LOOKS GREEN FROM UPSTREAM.*** A check that
compares source against binary answers one of four questions. **Prefer a stamp read back from the
far end** — the version the *device* reports, the wording the *invoked* binary prints — over any
comparison made among the artifacts on this side of the boundary.

### M-3. Derive the conflict graph instead of hand-building its inputs

**What happened.** Gates 8/8c report NOT CHECKED without a computed graph. Producing one by hand took:
running the tool, discovering it needed a corpus merging the deliverable with the harness objects,
building that corpus with `cp`, discovering the submission's names do not resolve to storage paths,
extracting the storage-resolvable tags from the binding into a signals file, and re-running.
**Every input was already known to the caller.**

**Mechanise:** the runner holds the binding, the program directory and the harness objects. It can
build the merged corpus and the signals list itself. ⚠️ **Keep the refusal** — the tool correctly
refused `21 of 21 unresolved` rather than emitting a partial graph, and that refusal is the feature.
Automate the INPUTS, never the verdict.

### M-4. Persist coordinator-supplied fields across a regenerate

**What happened.** The `deployment` declaration was written by hand, then **silently discarded** when
the submission was regenerated by its author's builder — twice. The same rebuild also destroyed a
computed `conflictEdges` value. It was found by comparing byte counts while checking something else;
***nothing reports it.***

**The general lesson, recorded by the lane that caused it:** *once a second party can edit a generated
file, the generator is a CLAIM about that file rather than a description of it, and the first rebuild
wins silently.*

**Mechanise:** either sourcing coordinator fields from their own artifacts at build time (which is how
`conflictEdges` was repaired), or a checked manifest of who-owns-which-key so a regenerate that drops
a foreign key REFUSES instead of succeeding.

### M-5. A committed-content boundary check ✅ BUILT 2026-09-18

**What happened.** Live-job identifiers reached committed source in **13 files across several lanes**
on the first day of live-job work. Every lane was told "nothing from the job folder is committed" and
every lane honoured it *for artifacts* — then quoted job identifiers into code comments as evidence
for a technical claim, because **that does not feel like job content; it feels like rigour**.

One lane audited its own files, caught them, and correctly declined to rewrite another lane's — which
is exactly right, and is precisely why the leak survived: **a per-lane check catches the lane's own
work; only a repo-wide sweep catches the repo.**

**Mechanise:** one grep over tracked files for the active job's vocabulary, run before any commit
during a live run. Mechanically trivial; it is the only item here that is a governance control rather
than a time saving, and it should be the first one built.

**✅ `tools/check-staged-identifiers.py`, wired as the third block of `hooks/pre-commit`.** 16 cases,
six mutations each reddening the case named for it, documented in `tools/README.md`.

Two things the entry above got wrong, both worth recording because they are the difference between
the tool as imagined and the tool as built:

- **Not "over tracked files" — over the INDEX.** `git show :<path>`. Sweeping tracked files answers
  a question about the working tree, which passes a leak that was staged and then tidied away and
  refuses one that is only unstaged. The second is the kind of wrong that gets a hook switched off.
- **Not "one grep" — tiered.** A flat any-hit rule produced 394 residuals on the first trial, of
  which 317 were identity mappings and 59 of the remaining 61 were the same identifier seen inside
  a longer one. A gate that cries wolf 59 times in 61 gets learned-ignored, and a gate people have
  learned to ignore is worse than none because it reports green. It reuses the Gate 3 oracle's
  T1/T2/T3 derivation rather than re-deriving a third vocabulary.

**"Mechanically trivial" was the part that aged worst.** The matcher was; the *trigger* was not.
`sanitization/` is git-ignored and machine-local, so the naive build refuses every commit in every
clone that has never worked a live job — and the alternative, skipping quietly when the vocabulary
is absent, is the failure this repository has already paid for once. The rule is now: no vocabulary
**and** no live-job material is nothing to check; no vocabulary **with** live-job material on disk
is exit 2.

### M-24. THE BUILDER AND THE VERIFIER READ THE SAME TERM LIST WITH DIFFERENT STRICTNESS ✅ BUILT 2026-09-21

**What happened.** 2026-09-21, adding three terms to `sanitization/scrub-terms.md` with a `class`
value that does not exist (`process`, `worklane` — the real set is `jobcode, company, site,
modelline, block, member, pathstem`).

**`build-scrub-rules.py` refused the whole run** — exit 2, `NOTHING EXAMINED`, all three lines
named, previous rule set left untouched. Exactly right, and its own reason says why: *"a malformed
row is refused rather than skipped: a term list that silently shrinks is the failure this design
exists to prevent."* Had it skipped them, it would have emitted a **116-rule set indistinguishable
from a correct one.**

🔴 **`verify-scrub.py`'s `derive_needles` ACCEPTED all three and tiered them T1.** Same file, same
rows, opposite verdicts. So between the edit and the rebuild, the verifier was hunting a vocabulary
the builder had never agreed to — and I used that verifier, directly, to certify three files as
`T1=0 T2=0` before the builder had accepted the rows those files were being cleared against.

**Why it matters more than it looks.** The two tools are *deliberately* independent — that
independence is the whole reason a verifier is worth running against a builder's output. But
independence of JUDGEMENT is not licence to disagree about **what the input file means**. A row
either is a term or it is not. Today the answer depends on which tool you ask, and the permissive
one is the one that hands out clean bills of health.

**The near-miss is the finding.** Nothing was published wrong: the builder's refusal stopped the
cycle, and it stopped it *because of the exit code*, not because anything looked unusual — the
printed summary above the refusal is the same shape a good run prints. **Exit 2 was the only signal
there was.** Had that run been read as "rules unchanged, carry on", a publication would have gone
out with a vocabulary three terms short of the one its own clearance checks had used.

**Mechanise.** One parser for the term list, shared, refusing identically — the same move M-22 made
by sharing `derive_needles` rather than hand-rolling a second vocabulary. Until then, the rule is:
**a `T1=0` from the verifier means nothing until the builder has accepted the same term list**, and
a builder exit of 2 invalidates every clearance taken since the last exit 0.

> ✅ **BUILT THE SAME DAY.** `tools/term_list.py` is now the only reader of the term list, imported
> by both tools; the builder's local copy is deleted and the verifier's lax inline loop is gone.
> `derive_needles` **raises `MalformedTermList`** rather than skipping — it is a library to three
> other tools, so it cannot print a worklist and exit 2 itself, but continuing would rebuild the
> exact defect. 12 self-tests, in CI.
>
> **The shared surface is deliberately tiny:** it reads the table and says what is in it. No
> variants, no rules, no tiers, no repository. Independence of *judgement* between builder and
> verifier is the reason running one against the other proves anything, and that is untouched —
> what is no longer permitted is disagreeing about what a row **means**, which is a fact about a
> file rather than a judgement about a repository.
>
> **Two cases carry the whole point** and they run the real tools, not stubs: the verifier must
> *raise* on a row the builder refuses, and one malformed file handed to both must be refused by
> both. The second one failed on its first run — and correctly: the builder exited **3**, not 2,
> refusing to write rules anywhere git can see, before it ever reached the parse. **The fixture was
> wrong, not the tool**, and a test that had asserted "non-zero" would have passed while measuring
> the wrong gate.
>
> One consequence worth knowing: a test fixture that copies a tool into a throwaway repo must now
> copy `term_list.py` with it. Five copy sites updated. A missing parser does not degrade to the old
> behaviour — the import fails loudly, which is the right failure.

**Status: filed and BUILT 2026-09-21.** Found by an error of mine, caught by the tool designed to
catch it.

### M-22. A GREEN CLAIM IS PROSE, AND NOTHING EVER COMPARED IT TO THE TOOL THAT KNOWS ✅ BUILT 2026-09-19

**What happened.** `docs/13-data-boundary.md` records two 2026-07-20 per-project approvals, both of
which mandate *sanitize-to-Green-first* and then assert the outcome — "Every downstream artifact
stays Green and committable", "every committed artifact ... is Green". A third document,
`gen/_validation/MotorVSDSystem-purpose/spec.md`, carried the same claim in four words: "**Green
artifact** — invented names only". Measured 2026-09-19 against the derived vocabulary, over tracked
content at `HEAD`, **all four directories those sentences name carry live vocabulary** — between 1
and 36 distinct hunted needles each, and 7 `T1 DECLARED` across them. Counts and the full table are
in the correction note appended to that approval block; the terms are not listed anywhere.

**The part that makes this an M-item rather than a typo.** The tooling *already knew*. The scrub
builder takes a `--green` list, and **not one of the four directories has ever been on it**. So a
machine-readable tier register and a prose tier claim have coexisted, disagreeing, for two months,
and **nothing in this repository has ever compared them**. The documents were not checked against
the tool because there was no step at which that comparison happens.

**Why M-5 does not cover this.** `check-staged-identifiers.py` would refuse a *new* commit that
introduced this vocabulary, and that is the right job. But its subject is the **index**, and it asks
*what does this commit introduce*. It cannot see content committed before it existed, and — more to
the point — it has no opinion about a **claim**. A file can be full of vocabulary and perfectly
honest about it; a file can be clean and lying. M-5 measures the content. This measures the
**agreement between the content and the label**, which is a different question and the one that
actually bit.

**Mechanise.** A check that, for every directory a tracked document asserts to be Green:
1. asserts the directory appears in the builder's `--green` list, and
2. asserts it carries no `T1`/`T2` needles, reusing `derive_needles` rather than a third vocabulary.
Both halves are needed: (1) alone passes a directory somebody added to the list wrongly, (2) alone
never notices the register and the prose have drifted. Report paths and counts, never terms.

**The harder half is finding the claims**, and it should not be a regex over the word "Green" — the
word is load-bearing in fifty innocent sentences. The tractable shape is to invert it: make a Green
claim a **declaration** (a marker file, or a line in one register) so that asserting Green is an act
the tool can enumerate, and prose that merely uses the word carries no authority. That is the same
move as `accepted-merges.txt`: an escape becomes safe once it must be *written down somewhere the
gate reads*.

**✅ `tools/check-green-claims.py` + the tracked register `tools/green-claims.txt`.** 20 cases, ten
mutations, wired into CI — both the self-tests and the check itself. Documented in `tools/README.md`.

**IT FIRED ON ITS FIRST CONTACT WITH THE REAL REPOSITORY**, which is the only way to know a gate is
not decorative. `gen/test-project001` — declared "Green-tier throughout" in `CLAUDE.md`, not in some
forgotten note — carried a **5-character job code (T1 DECLARED) in three files**, plus an inferred
map key in a fourth. Nothing was leaking: the scrub rewrites it and Gate 3 returns T1 0 on the
published artifact. **The label was wrong, not the data.** Remediated by substitution, AB-1's method.

Three things the entry above got wrong, worth recording because they are the difference between the
tool as imagined and the tool as built:

- 🔴 **"assert it carries no T1/T2 needles" is not implementable as written, and both obvious
  readings are wrong.** `derive_needles` demotes a needle to T3 when it appears in the Green
  corpora. Pass them, and a directory's own content demotes its own needles — **the check passes
  vacuously, always**. Pass nothing, and every Green corpus fails on conventional map keys it is
  entitled to hold. The answer is **leave-one-out**: checking `D`, build the token set from every
  *other* registered directory, so "is this conventional?" is answered by content that is not the
  content under test. That one decision is the whole design and it is the case a naive
  implementation fails.
- **"assert the directory appears in the `--green` list" needed to be PREFIX, not equality**, and
  bidirectional. Membership is `git ls-files <corpus>`, a prefix walk. Equality would report a
  directory as undeclared while the scrub was in fact treating it as Green — a finding in the wrong
  direction, which is the kind that gets a gate switched off. The reverse direction matters too: an
  entry on the list that nothing declares is a **silent exemption**, because the list withholds a
  rewrite rule for every term present in it.
- **The "harder half" turned out not to need solving.** The entry worried about *finding* the claims
  and proposed making a claim a declaration. That is exactly right, and once the register exists the
  enumeration problem disappears entirely — there is nothing to find. What the entry did not
  anticipate is the **third** check that falls out for free: the two `--green` defaults are
  duplicated literals in two files with no shared constant, and **neither tool can report a
  divergence about itself.**

**One limit, named rather than papered over.** A claim about an **untracked** directory can never be
checked — there is no content at `HEAD`. Two exist, both asserting the gitignored reference TIA
project is Green. They are probably true and are deliberately left alone: editing a true sentence to
satisfy a tool that cannot see its subject is worse than recording the limit.

Tracked as **AB-2**.

### M-23. A BLIND RUN'S BLINDNESS IS UNRECORDABLE, so no audit can ever clear one — ⚙️ STATIC HALF BUILT 2026-09-21

**What happened.** The `gen-block-modify-purpose` blind validation was audited 2026-09-20 to answer
one question: could the generator have seen the answer key? The verdict was **UNVERIFIED — neither
cleared nor impeached** (AB-2 addendum 2). Not because the audit was shallow, but because **the
answer does not exist anywhere**: `gen/telemetry.log`'s schema is `date | skill | wall_clock |
portal_roundtrips | tokens | outcome | note`, and there is no field for what the agent could read.
There never was one.

**The quarantine was a `.gitignore`, and that is a category error.** Ignoring a file fences it from
**commit**. The quarantine's own commit message states the goal as the generator *"never being able
to discover it in the tree"* — which is a **read** fence, and gitignore is not one. Measured: a
byte-identical copy of the key sat in `scratch/` (also ignored, also present on disk) for about an
hour before the generated block was authored.

**Why this is not "be more careful".** Every artifact asserting blindness is self-reported by the
agent whose blindness is in question — the spec grants a three-file permission, the architecture
note asserts the key "was never accessed". That is the D6 shape the whole agent architecture exists
to prevent: *the party being measured also supplies the measurement.* An assertion of independence
from the party whose independence is at issue is worth exactly nothing, and the repo already knows
this — it is why `assertion-enumerator` exists and never reads the implementation.

**Mechanise.** A dispatched run should record its **readable scope as data, not as prose**:
1. the case declares the paths the generator may read;
2. the run records the paths it actually received — a context manifest, hashed;
3. a checker asserts (2) ⊆ (1), and that no declared-quarantine path is byte-identical to anything
   inside (1). **The second clause is the one that catches this case**, because the breach vector
   here was not a path anybody listed — it was a *copy* of one, under a different name.

**The hard part is (2)**, and it is worth saying plainly: nothing currently emits it, and a field an
agent fills in about itself is the same self-report in a new location. The honest first version may
only be able to record what the DISPATCHER passed, which is at least a different party.

**Cheaper interim that is worth doing on its own:** make the answer key exist **once**. Today a
quarantined key and a tracked corpus file are byte-identical, so editing the corpus silently changes
a validation case's ground truth, and the quarantine is defeated by a file nobody thinks of as the
key.

> ✅ **THE INTERIM IS BUILT — 3-C, 2026-09-21.** `tools/answer-keys.txt` declares each key's
> canonical path and sha256; `tools/check-answer-keys.py` pins it, gates on a second **tracked**
> copy, and prints every byte-identical copy it can find in the working tree. 17 self-tests, in CI.
> **Three things the paragraph above got wrong, found by building it:**
>
> 1. **There were FOUR copies of that key, not two** — the tracked corpus file, the quarantined one,
>    and two more in gitignored `scratch/`. The "cheaper interim" was scoped from a census nobody
>    had taken.
> 2. **A SECOND key had the same defect and is not mentioned anywhere above.** The sealed
>    `PlantAutoControl-bench` key had its own gitignored working copy. Writing the register is what found
>    it, which is the argument for registers over prose in one line.
> 3. **"Exists once" is NOT ACHIEVABLE and aiming at it was the error.** The canonical copy is
>    *tracked reuse corpus* that three other runs cite as ground truth, so it cannot be deleted or
>    hidden — and deleting an ignored working copy does not stop the next one appearing. What was
>    achievable, and is what got built, is **one DECLARED copy, pinned, with every other copy
>    counted out loud every run.** The count is now 2 working copies for one key and 1 for the
>    other, printed, rather than 0 asserted.
>
> The interim does **not** shrink M-23 by one inch. It makes ground truth immovable-in-silence; it
> says nothing about what anybody could read, and its own clean verdict says so in those words.

> ⚙️ **THE STATIC HALF IS BUILT — 2026-09-21.** `tools/readable-scope.txt` declares, per case, what
> the generator was allowed to read and what was supposed to be hidden;
> `tools/check-readable-scope.py` checks it. 16 self-tests, in CI.
>
> 🔴 **THE DESIGN ABOVE NAMES THE WRONG CLAUSE, AND BUILDING IT IS WHAT FOUND THAT OUT.** Clause 3
> above says the catcher is *"no declared-quarantine path is byte-identical to anything inside
> (1)"* — inside the **allowed set**. Measured against the real history: the vector was a copy in
> `scratch/`, and **`scratch/` was never in the allowed set**, so that clause passes it. The
> implemented clause is wider — *no byte-identical twin ANYWHERE in the readable tree* — and the
> first self-test reconstructs the historical shape precisely to pin it, because a check that
> cannot catch the case that motivated it is worse than none: it will be trusted.
>
> **`void` is the other thing building it forced.** On first contact with real data the checker
> gated, correctly, on the one declared fence: 24 byte-identical twins and a **tracked** key. Both
> findings are true and unfixable — the key is cited bench corpus. A check that fails forever gets
> disabled, and "delete the declaration" is the wrong repair. So a case may declare a fence `void`:
> twins are still reported every run, nothing gates, **and the verdict prints, permanently, that
> the case CAN NEVER BE CLEARED.** It is a confession, not an exemption — downgrading to `void`
> trades a red check for a standing public statement that the case's blindness is unprovable, which
> is exactly what AB-2 concluded and what nothing previously recorded.
>
> **The hard half is untouched and stays open.** Without `--manifest` the run half examines
> NOTHING and says so in those words. Nothing emits a context manifest, and an agent's own account
> of what it read is the same self-report in a new location. What is now true that was not before:
> the *scope* is written down, transcribed from the case's own spec rather than asserted, and the
> quarantine's defeat is measured on every run instead of being rediscovered by an audit.

**Status: filed 2026-09-20. STATIC HALF BUILT 2026-09-21; the manifest half remains open and is the
part that would actually clear a run.** Its cheap interim built 2026-09-21 (3-C). Tracked as
**AB-2**. Not a publication blocker and not a leak: Gate 3 returns an earned zero over all of this
content.

### M-13. A stationarity claim needs a SERIES, and the inert basis is taken from ONE sample 🔴 BLOCKING A RUN TODAY

**What happened.** The resting state a wave's inert check is measured against was captured by a
**single read** shortly after download, and written down as "the resting state". It was wrong twice, in
opposite directions, and I made both errors:

- First I recorded that the block *"has no single resting state"*, on the strength of two registers
  differing between two reads taken **twenty minutes apart**. It does settle; it just takes minutes.
- Then I recorded the band as **static**, on the strength of **two reads fifteen seconds apart** being
  byte-identical. A **twenty-sample** sweep of the same band immediately found a signal changing state.

***TWO SAMPLES IS NOT A MEASUREMENT OF STATIONARITY, AND IT READS EXACTLY LIKE ONE.*** Both times the
evidence was a diff that came back clean, and a clean diff over an inadequate sample is indistinguishable
from a clean diff over a good one.

**Why it bites.** The inert declaration is an INPUT to admission — a signal declared inert that is in
fact slow-moving stops the wave at index 0 with `NotInert`, and the disagreement is then attributed to
the program. It is the same shape as *empty is not clean*: **a sample size of one produces a
confident-looking answer with no denominator attached.**

**Mechanise:** the basis capture takes **N samples over a stated window** and records BOTH — so the
declaration carries `stationary over 20 samples / 62 s` instead of a bare value, and a downstream
`NotInert` can be read against how hard anyone actually looked. ⚠️ **Do not mechanise the choice of
window** — how long a plant takes to settle is a process fact and belongs to the person who knows the
plant. Mechanise the *recording of what was sampled*, which is the half that is currently absent.

#### 🔴 THE SAMPLE SIZE WAS NEVER THE DEFECT — SAMPLING THE WRONG STATE WAS (2026-08-18)

A later stationarity claim was taken over **113 frames spanning 252 seconds, with zero of 84 values
moving.** Beautifully stable, and wrong: the system had been running for hours through earlier
attempts, so the sweep measured ***residue, not rest***. **A 3-frame sample taken immediately after a
restart was correct where the 113-frame one was not.**

***MORE N CANNOT FIX A MEASUREMENT TAKEN IN THE WRONG CONDITION*** — and the large clean sample is
**more** convincing than the small correct one, which makes this *worse* than the two-sample error
above rather than a milder version of it. Nobody argues with 113 frames.

➜ **So stating N and the window is necessary and NOT sufficient: the STATE the system was in matters
more than either.** `113 frames / 252 s, after hours of running` and `3 frames, from a cold restart`
are different claims and only one of them is about rest. **Record the precondition beside the sample
count** — what the system had been doing beforehand is a fact the harness already knows and currently
throws away, and it is the half a reader needs to tell a stationary signal from a stuck one.

---

## TIER 2 — real savings, no round trip

### M-6. Measure the scan time rather than assuming a band

The runner reads a free-running scan counter on **every poll** and already reports `ScanAdvance` — yet
the scan period was assumed from a documented band because no measurement existed, and every duration
expressed in scans inherited the error. Measuring it by hand afterwards took one command.
**Mechanise:** derive it from what the wave already reads, and record it in the result package.
⚠️ It is a property of the PROGRAM, not the controller — one measured program's figure was
carried into a document as a rig fact and was wrong by an order of magnitude for the next program.

### M-7. Stage the merged corpus

Loading a program plus its harness needs a directory merging deliverable IR, harness IR and generated
objects, **with the harness copy of a same-named file winning**. Done by hand with `cp`. A duplicate
basename is refused by the importer, and getting the precedence backwards loads a program whose
harness never runs — with every other check still green.

### M-8. Derive the assertion-bounds projection

The bounds table the currency gate needs already exists per-assertion in the enumeration. It was
absent from the submission's projection, so the gate could not run; adding it was mechanical once
identified. ⚠️ **It moves the trust boundary rather than removing it** — a careless empty bounds
list from the enumerator becomes invisible downstream.

### M-9. Settling per index, not only on the wave's last

Settling is evaluated ONLY for a slot's final index, so an earlier vector cannot be conclusive however
perfectly it runs — one held **3 of 3 assertions and still reported `Unsettled`**. The workaround was
to resubmit it alone, where it returned **PASS**. That is a real answer obtained by restructuring the
submission rather than by testing anything, which is the signature of a tooling limit.

---

## TIER 3 — recorded, lower value or blocked

- **M-10. Anti-vacuity by construction.** A `Never` assertion passed VACUOUSLY and only its author's
  hand-added companion observable caught it. A check that every `Never` carries a positive
  co-observable would make that structural rather than a matter of authorial care. **The judgement of
  WHICH observable is not mechanisable; the presence of one is.**
- **M-11. Retentive `%M` extent.** Fed into the map hash and therefore the build stamp, currently
  DEFAULTED and honestly reported as such. **Not mechanisable today** — no Openness path reads the
  PLC-tags retain setting; it needs a person in TIA. Worth a gate that refuses to publish a stamp
  resting on a defaulted value into a *deployed* artifact.
- **M-12. Latch-source trust.** A binding naming a block as the latcher is admitted on trust; a block
  that does not in fact latch would reproduce the original defect with nothing mechanical to say so.

### M-14. One address convention, two independent off-by-ones

**What happened.** A `Bool` result register occupies one register but its bit lives in the **low byte**,
i.e. the ODD address. Two different parties, on two different days, independently wrote the WORD address
where the BIT address was meant — and because the two differ by one byte, the wrong address lands inside
a real, readable, plausible-looking neighbouring register rather than failing.

**Why it is nobody's fault twice.** The element table says `Bool -> 1 register`, which is true and is
what everyone reads; where in that register the bit sits is a separate fact, recorded only in the
generated tag table's own `@ %M....0` suffix. Anyone reasoning from the table rather than from the
generated artifact gets it wrong, and gets a number back.

**Mechanise:** a resolver that turns a result-source **name** into its address by reading the generated
mirror, so nobody computes one by hand. The map is already emitted and already correct — every hand
derivation of it is redundant work with a known failure mode. ⚠️ It also wants the converse: an address
-> name lookup, because the diagnostic direction (*"a byte moved, what is it?"*) is the one where the
mistake was actually made.

---

### M-15. A role fully plumbed and NEVER DRIVEN — the PC-side `undriven-scan`

**Found 2026-08-20, by reading, in a component that had just gone green.** The Modbus command-injection
client declared a `Heartbeat` role, parsed it from the binding, resolved it, type-checked it,
band-checked it, printed it in the `map` report, and gave it a write-target factory. **That factory had
zero callers.** Nothing in the client ever wrote the heartbeat.

The test double, meanwhile, modelled an arming gate that would not process a command until the heartbeat
had changed twice — and every protocol test armed it by calling into the model **directly, past the
client**. So **99 tests passed over a client that could not arm anything**, and the one comment that
described the missing piece named a verb that did not exist.

**Why it survived every check.** Everything a reviewer looks for was present and correct: the role
existed, was validated, was refused when malformed, and appeared in the tool's own output. The only
absent thing was a **call**, and nothing counts calls. The IL walk in the same test file counts *hits* —
it never thought to count a *zero*.

🔴 **And note what the test suite did here: the double supplied the exact capability the subject
lacked.** That is the correlated check one level down from the one this project already guards against —
not two parties reading the same document, but *a test harness closing the loop its subject cannot*. **A
green suite is fully compatible with a tool that has never once performed its function.**

**Mechanise:** the structural IL walk already knows how to count references. Point it the other way —
every public factory on a write-target type, and every declared role, must have **at least one call site
in the shipped assembly**; a zero is a finding, not a pass. Exactly `undriven-scan`'s shape and exactly
its reason: *a declared capability with no producer is indistinguishable from a working one until it
runs.*

🔴 **CONFIRMED TWICE IN ONE COMPONENT, WITHIN A DAY.** Closing the heartbeat gap immediately turned up
**the same defect one field along**: the acknowledgement's **code echo** was likewise declared, parsed,
resolved and type-checked — and then dropped on the floor at the point of use. Two instances in one
small assembly, both found by a person reading, **neither by a test**. That is the argument for
mechanising it: the class is not rare, and nothing in the ordinary review path looks for it.

🔴 **THIRD INSTANCE, 2026-08-24 — AND IT DEFEATS THE CHECK AS PROPOSED ABOVE.** Seven types in
`src/wave-control/WaveControl/` — `AdmissionController`, `QueueRehydrator`, `CoordinatorStateStore`,
`WaveQueues`, `DrainPolicy`, `EscalationLadder`, `StopOnFailedWaveSetGate` — have no caller reachable
from any entry point; they call **each other**, and their tests call them. Recorded row by row in
`docs/notes/spec-reconciliation.md` **§UW**. ***The rule written above — "at least one call site in
the shipped assembly" — passes every one of them***, because `QueueRehydration.cs:185` is a call site
in the shipped assembly and `AdmissionController.Admit` is a call site's target. **A reference count
cannot tell a live component from a closed loop.** The check has to be *reachability from a declared
entry point* — `Main`, a CLI verb, a public API surface — walking forward, not a per-symbol count
walking back. A cycle with no door is exactly the shape a backward count is blind to, and it is
cheaper to find than the two heartbeat instances were: the entry points are enumerable
(`WaveControl.Cli/Program.cs:40-46` lists five verbs).

⚠️ **The general form is worth more than the check.** When a test double implements one half of a
protocol, ask **which half the SUBJECT implements**. If the double can be driven into the state a test
needs without the subject doing anything, that test is about the double.

---

### M-16. "Read the interface but not the behaviour" is NOT achievable by instruction

**Found 2026-08-20, by an enumerator that refused its own task.** A convention-derived assertion pass —
one whose entire value comes from being derived *independently* of the implementation — was briefed to
read "the block's INTERFACE only: what signals exist, their direction and type". The file it was pointed
at was titled an interface report and was in substance a **470-line behavioural specification**:
per-network boolean expressions, timer arming conditions, latch and reset semantics, status-code values,
and a named defect analysis.

**The independence was gone in the first thirty seconds, and it could not have been prevented by care.**
You cannot know what a document contains without opening it. The agent read it before it could know, and
then correctly reported that *every* behaviour category in its brief had been pre-answered — leaving no
residue from which anything genuinely first-principles could be written.

🔴 **IT REFUSED TO EMIT THE ARTIFACT, AND THE REASONING IS THE KEEPER.** A contaminated list looks exactly
like a clean one, is **more** likely to pass, and — once written — permanently displaces the clean one,
because nobody re-runs a pass whose output already exists. *A quarantined artifact that gets cited is this
project's signature failure with a disclaimer attached.* Emitting it "clearly labelled as compromised"
would have been the worse of the two options.

Note the shape of the trap, which is what makes it general: the offending document **opened by declaring
that it deliberately does not do the enumerator's job** — and then described the block thoroughly enough
that nobody who read it could do that job independently. *The separation was designed for, and defeated
by, the same file.*

**Mechanise:** a **redacted signal inventory** — name, type, direction, carriability, and nothing else —
generated as a STANDING artifact at interface-report time, not as a remedy after a contamination. One was
hand-built here to unblock the re-run; the point is that it should never have had to be. Extracting it is
transcription rather than judgement, so it can be produced mechanically and its exhaustiveness is what
stops the extractor's own selection leaking in. **Private working statics must be excluded entirely** —
their *names alone* are implementation vocabulary and seed the restatement by themselves.

⚠️ **The adjacent tooling trap, found in the same pass.** `converter interface-check` reads
`response_signal:` values as the **required** set and exits 1 as a *fail against the block*. Point it at a
convention-derived enumeration and every assumption becomes a requirement the block is failed against —
**exactly inverting "this is a question for the engineer" into "this is a defect".** Convention files now
carry a top-level `assertionClass: convention-derived` marker; the durable fix is for the tool to refuse a
file bearing it, rather than relying on nobody passing the wrong path.

---

### M-17. A generated artifact with hand-added keys — the generator eats them, and it has now done it twice

**Found 2026-08-20, on the second occurrence.** A submission document is produced by a generator script,
and fields have been added to the generated output **by hand** afterwards. A subsequent rebuild silently
destroyed one of them. It was recovered byte-for-byte — and the recovery only happened because the author
noticed, not because anything detected it.

🔴 **It is the SECOND instance of the same mechanism**: a different hand-added key was destroyed by a
rebuild one revision earlier. Two occurrences, same cause, and between them nobody made the generator
aware of the fields being added to its output.

**Why it is worse than a normal lost edit.** The destroyed key is *absent*, not *wrong* — and every
consumer downstream reads an absent key as "not declared" rather than as "lost". In a system where
**absent is a positive claim** in several places (`reachableState: []` versus a withheld key,
`s7Objects: []` versus `noS7Transport`), a silently dropped declaration does not fail: it changes the
meaning of the document and passes.

**Mechanise:** either the generator owns every key — the fix applied here, and the right one — or the
generated file carries a checked manifest of the keys it must contain, so a rebuild that drops one is a
refusal rather than a smaller file. **Do not solve it by remembering to re-add fields after a rebuild.**
That is what was tried, implicitly, twice.

⚠️ Adjacent, same day: a companion submission in the same folder is now **stale with no generator at
all** — it can only be maintained by hand and nothing marks it as diverged from the file it was copied
from. A generated artifact and a hand-maintained one that look alike is how the above happens in the
first place.

### M-18. One measured constant, three homes — and the copy that gets read is the wrong one

**Three instances, all live on 2026-08-20.** The scan period existed as `24.931` in the gate binary,
`24.27` in a job artifact, and `22.63` in an earlier one — three numbers, three files, one physical
quantity. Separately and on the same day, **`CLAUDE.md` carried `~2.1 ms` for the same constant, wrong by
an order of magnitude**, while the code beside it had been right for two days.

**That last pairing is the lesson.** The code was correct, and its own comment even warned that the wrong
figure existed elsewhere in the repository — but the wrong copy lived in the file that is **loaded into
every session**, so the wrong copy is the one that was read, and a dependent conclusion (a compression
ceiling) was drawn from it and written down as fact.

**Mechanise:** a measured constant gets **one home in code** and every other mention cites it rather than
restating it. Where prose must quote a number, the quote should be generated or checked against the
source, because **a hand-copied constant does not drift loudly — it drifts silently and stays plausible.**
The failure is never the copy being wrong; it is the copy being *readable and confident*.

---

### M-19. The observability gate fences the VECTOR author from the map, and nobody fences the BLOCK author ✅ BOTH LIMBS GATED 2026-08-24 · ⚠️ STILL OPEN

**Found 2026-08-20, while closing that gate.** The map a conformance submission is judged observable
against is authored by somebody. The gate's independence argument fences it from the **vector** author —
and that argument is satisfied. **Nothing anywhere compares the map's author against the BLOCK's author**,
and on the submission that surfaced this they were *the same identity string*.

That is the D6 hole one level down. A block author who also authors the observability map decides both
what the block does **and what can be seen of it** — and "what can be seen" is exactly what decides which
assertions are admissible. A signal that would embarrass the block need never appear in the map, and the
gate would pass.

⚠️ **Not a refusal and not an accusation** — the identity string is weak evidence and this project has
already recorded that *what makes two agents different is undefined* (an open contract question). Recorded
because the CHECK does not exist, not because the map is believed compromised.

> ✅ **That cross-reference is out of date as of 2026-08-24, and the update strengthens this entry
> rather than closing it.** The question is now answered — `docs/notes/test-environment-contract.md`
> §1.1: *a different agent means a different context instance; the mechanism is isolation, and the
> identity string is a label for it.* §1.1's graded ceiling **cites the sentence above as the statement
> of the real threat**: the exposure is *accidental correlation — one party doing two jobs without
> noticing* — and against that a normalised comparison is adequate, **because an accident produces the
> same string**. ~~So M-19 remains exactly what it says it is: a missing check, cheap, and **an option
> awaiting a decision — not planned work.**~~ ⚠️ **Overtaken 2026-08-24: it was neither missing nor
> cheap, and it was built rather than decided against — see the two gate notes below.** What survives
> unchanged is §1.1's grading of the threat: the exposure is *accidental correlation*, and a normalised
> comparison is adequate against it because an accident produces the same string.

**Mechanise:** the same normalised-identity comparison the gate already performs against the vector
author, performed against the block author too. ~~It is the cheapest possible closure — the comparison code
exists and is simply not pointed at the second party.~~

> ✅ **BUILT 2026-08-24 AS GATE `5c map authority` (`57432c6`, merged `915b6e8`). AND THE COST LINE
> STRUCK ABOVE WAS FALSE — the commit that closed this says so in its own message.**
> **The operator existed and had no second operand.** `AgentIdentity.SameAs` (`Trim()` +
> `OrdinalIgnoreCase`) was already there; `BindingDocument` carried **no author at any of its four
> levels**. What that line called a repointing was a **wire field, a domain field, a changed signature,
> four call sites and a merge decision** — plus 108 test fixtures that flipped the moment the gate
> landed, every one repaired by attributing the fixture rather than weakening the gate.
> **That one line was retracted TWICE, in two consecutive commit messages** (`57432c6`, then `915b6e8`)
> — and it had already been quoted onward as fact into `docs/18-project-workbench.md` §5 Phase 8, where
> it was doing work: it was the evidence for calling M-19 *"a small specified code change."* ➜ ***A
> backlog entry may state the SHAPE of a closure; it must not state its PRICE, because at the time it is
> written nobody has looked — and a price written here is read as a measurement everywhere else.***
>
> **What 5c does:** compares the binding document's `declaredBy` against the block author, and against
> every vector author, on the map gate-5 adjudicates against. Unrecorded reads **NOT CHECKED, never a
> pass**. Demonstrated on the shipped deliverable through the CLI: unattributed → NOT CHECKED; the real
> block author → REFUSES; the real vector author, via a case-and-space variant → REFUSES naming 27 of
> 27 vectors; a third party → PASSES and prints its denominator.
>
> 🔴 **WHAT 5c DOES NOT ESTABLISH, AND EACH OF THESE IS LIVE.**
> - **It compares a string somebody types.** Non-collision of *names*, not independence of *parties* —
>   the same ceiling as gates 2, 3d and 4b, graded at `docs/notes/test-environment-contract.md` §1.1.
> - **The two committed `gen/` bindings still carry no `declaredBy`, so both read NOT CHECKED today.**
>   Filling one in truthfully **refuses that wave** — that file's own prose says the block author wrote
>   it. Held for an owner ruling rather than resolved by inventing an identity: `docs/notes/owner-questions.md`.
> - **The multi-coordinator batch is NOT CHECKED by construction.** Identical declarers across lanes
>   collapse losslessly; different declarers, or one silent lane, leave it null. The correct answer is a
>   **set**, and a joined string was rejected on evidence because the comparison would match neither
>   party. Unbuilt, and tabled in `owner-questions.md`.
> - **One threading site is read by nothing.** `LoopRun`'s `MapAuthor` reaches
>   `ObservabilityCheck.Evaluate`, which never touches it, so **no mutation of that argument can go
>   red.** The caveat is in the code beside the field; it is repeated here because the reasonable
>   inference from *"the author is passed"* is *"the author is checked"*, and it is wrong.

⚠️ **Second, unrelated limit of the same gate, in its own words: it TAKES THE NAME, NOT THE FACT.** Latch
claims are admitted on *provenance* — "this signal is latched by block X" — with no check that block X is
in the deployment at all. **A submission can therefore be ADMISSIBLE while naming latching blocks that are
not loaded**, which is precisely the state the deliverable was in when this was found. Admissible is not
runnable, and the gate says so out loud rather than pretending otherwise; ~~the check that would close it is
a deployment-manifest comparison nobody has built.~~

> ✅ **BUILT 2026-08-24 AS GATE `5b latch block in the deployment` (`c9b594f`), which landed FIRST — it
> is why the other limb is named 5c and not 5b.** A hand-authored latch claim must now name a block the
> deployment names. **No schema change:** both operands were already in the submission and already
> parsed. Measured on the shipped deliverable through the CLI, not a fixture — **five hand-authored
> latch claims, none of them verifiable**, reported as NOT CHECKED with the output saying that is the
> state the deliverable was found in; pointed at a reconciled deliverable it REFUSES and names each
> missing block; declaring the missing one turns it green.
>
> 🔴 **WHAT 5b DOES NOT ESTABLISH.**
> - **It is scoped to the hand-authored half, deliberately, with a do-not-widen note in its own doc
>   comment.** A *generated* latch is derived from the transient declaration and the copy layer emits
>   the rung, so it names no block; widening would demand a manifest entry for something already in the
>   artifact — a refusal nobody can satisfy, which is how a gate gets switched off.
> - **The generated/hand-authored split reads rendered prose**, so a `latchedBy` that literally opens
>   with the generated prefix is skipped. That is a forgery rather than an accident, and the binding is
>   third-party by gate 5's own provenance requirement — but it is a residual, not nothing.
> - **Three absences are NOT CHECKED, not passes**: no deployment at all (classified as needing the
>   device, because what is on the controller is a property of the download), and a deployment naming no
>   object (*"absent from an empty list"* would answer the same for a real loaded block as for a
>   fiction). **Zero hand-authored claims is a pass, with the denominator printed.**
> - **It is not redundant with gate 11 and that was the risk.** On the reconciled deliverable gate 11
>   passes — its claim is about the wire — and 5b is the only device-bound NOT CHECKED left.
>
> ➜ **So both limbs of M-19 now have a gate, and M-19 is not closed.** Each gate establishes less than
> the limb it answers: 5c compares names rather than parties and reads NOT CHECKED on both committed
> bindings; 5b covers the hand-authored half only. **Leave this entry open, and read the two
> "does not establish" lists above before quoting either gate as coverage.**

---

### M-20. A gate that checks SET MEMBERSHIP cannot see an entry that BECAME false

**Found 2026-08-20 by a ratification review, in a gate that had just passed.** A model-fidelity
declaration is a set of claims about what a test model does and does not represent. The gate over it
validates **exact set membership** — every claim cited must be present, none duplicated, the two sets
disjoint. All of that held.

Then the model changed. **Three claims that were true when written became false**, and the gate saw
nothing, because *"present in the set"* and *"still true of the subject"* are different properties and
only the first is checked. One of the falsified claims — *"presents no change of contents at all for
the whole of a scenario"* — was **being asserted by two live vectors at the time it was found.**

🔴 **THE GENERAL SHAPE: a declaration describes a subject, the subject is versioned, and the
declaration is not.** Every artifact of this kind is exposed — fidelity declarations, resting-value
bases, interface reports, anything whose truth is contingent on something that can be edited
independently. The same day produced four separate instances of the *same* shape: three artifacts and
a binding note all still describing a network that had been repaired two days earlier.

**Mechanise, cheapest first:**

- **Stamp each declaration entry with the subject version it was verified against** — the build stamp
  already exists and already changes when the model changes. An entry whose stamp is older than the
  subject's is **not wrong, but it is UNVERIFIED**, and that is a reportable state rather than a pass.
- **On any change to a model, re-validate the declaration rather than only appending to it.** The
  appending is what happened here: six new entries were added correctly and three existing ones were
  left to rot.

⚠️ **What must NOT be done: soften the gate.** Set membership is exactly right for what it checks, and
it caught real omissions. The failure is that **nothing else checked the other property**, not that
this check is wrong. Adding a second check is the fix; loosening the first would lose what works.

⚠️ **And note where the finding came from: an INDEPENDENT ratification, not the gate and not the
author.** The author had flagged his own authorship as a correlated check and asked for review — the
review then found three defects he could not have seen, because *he was reading the model he had just
changed*. That is the independence property paying for itself in a single pass, and it is the argument
for ratifying declarations rather than merely gating them.

---

### M-21. "Cannot be reached from here" and "there is nothing there to reach" look identical in a coverage table

**Found 2026-08-20, and it had hidden sixteen unimplemented behaviours for weeks.** A scope analysis
marked a group of specified assertions **NOT REACHABLE** and attributed the cause to **slot scope** —
i.e. *a test at this scope cannot observe them*. A later campaign plan read that, and assigned the
group to a **wider** slot, on the reasonable assumption that widening would reach them.

**It would not have.** The behaviour those assertions describe **is not implemented at all**: an entire
operating band exists in the specification, in the scope boundary, and in the state-machine design, and
has no rung anywhere in the delivered program. The wider slot would have been built, deployed and run,
and it would have observed nothing — because there was nothing to observe.

🔴 **The two states are indistinguishable in every artifact we keep.** A coverage table shows an
assertion as uncovered either way. A gap register shows it as blocked either way. Only reading the
implementation separates them, and the enumerator is *forbidden* from reading the implementation — for
excellent reasons that are not in question here.

**Why it matters more than a bookkeeping error:** they are opposite kinds of work. *Cannot reach* is a
**harness** problem and the answer is to build more test infrastructure. *Nothing to reach* is a
**deliverable** finding and the answer is to write it up for the site owner — building test
infrastructure for it is pure waste, and it was about to be built.

**Mechanise:** when a coverage analysis records an assertion as unreachable, it must record **which of
the two** it means, and *"nothing implements this"* must cite the evidence. `converter cross-check`
already produces most of it mechanically — an interface member or DB member with **no writer and no
reader** is exactly the fingerprint, and on the instance above it named every parameter of the missing
band. The check exists; nothing routed the unreachable list through it.

⚠️ **And the adjudication needs a party who did not write the claim.** The lane that discovered this
asked explicitly that its own finding be confirmed elsewhere. The confirming lane upheld two of its
three claims, **refuted the third**, and found that the assertion cited there was implemented under a
different name **with a genuine defect at one input** — a better finding than the one it was sent to
check. *A claim that a production block is missing a feature must survive a party motivated to find it
already present.*

---

## WHAT MUST NOT BE MECHANISED

*Recorded because the pressure to automate these will be strongest exactly when they are working.*

- 🔴 **The independence itself (D6, M1).** Four parties who cannot read each other found: a spec that
  never defines the block as a unit; a model phase that does not exist; a vacuous pass. **A single
  agent doing all four jobs would have produced a coverage figure and no findings.** The round-trip
  cost that M-1 attacks is the cost of that independence — *attack the SYMPTOM, never the property.*
- **Any refusal.** Every gate that refused today refused correctly, including the ones that refused
  ME. Automate the assembly of a gate's INPUTS; never soften its verdict.
- **The basis re-confirmation (7a.2).** A failing test may not be closed by changing the block until
  the citation is re-read. That is a judgement, and on the one FAIL produced today the correct answer
  turned out to be *the harness was wrong*, not the block.
