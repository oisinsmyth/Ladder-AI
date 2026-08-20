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

### M-5. A committed-content boundary check

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
