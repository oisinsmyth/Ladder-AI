# PRE-FLIGHT INTERPRETER — the classification that was supposed to decide the phase

**Measured 2026-08-23.** `docs/18-project-workbench.md` §5 Phase 5 said the decisive measurement had
never been taken and named it: *take the failures we actually had and classify each as an existing
PC-side check would have caught this / only an interpreter would / only the rig would.* **If the
middle bucket is small, Phase 5 is not worth a second IR parser.** This is that measurement.

**Data boundary.** Two of the three corpora are live-run material from job `JOB9004`. This page carries
**counts, denominators and the names of our own checks** under the per-item owner permission recorded
in `docs/13-data-boundary.md` (2026-08-23, *"the pre-flight classification's COUNTS"*). **The per-row
table stays in the job folder** and is not reproduced, summarised per row, or paraphrased here. Where
a bucket-B row's shape could not be stated without naming a signal, the row was left out of the prose
and stayed in the count — see *What is deliberately not written down*, below.

---

## 1. 🔴 The rubric is the finding, not the method

The measurement was commissioned with **three** buckets. Run with three, it returns the wrong answer,
and it returns it confidently.

Three buckets have **no bucket for "not a defect in the block at all"**. Every failure that came from
an instrument, a declaration, a model or a deployment has nowhere to go but *only the rig would have
caught it* — because it is true that no PC-side reading of the block's IR would have caught it, for
the reason that **the block was not what was wrong**. On this corpus that is **56 of 93 classified
rows, 60%**. A three-bucket run would have reported a 60% "only the rig would" majority and argued for
more rig time **on the strength of our own instruments' bugs.**

The five buckets actually used:

| | bucket | means |
|---|---|---|
| **A** | an existing PC-side check catches it | a named producer already emits the finding — `preflight`, `undriven-scan`, `cross-check`, a `converter review` rule |
| **B** | only an interpreter would | requires executing the block's logic against a stimulus on the PC |
| **C** | only the rig would | requires the controller — scan semantics, process image, retentive behaviour, timing |
| **D** | not a defect in the block | instrument, declaration, model, deployment or harness fault |
| **U** | unclassifiable | the record does not carry enough to decide, and guessing would manufacture the answer |

**D and U are not padding.** D is the bucket whose absence inverts the recommendation. U is the bucket
that stops a thin record from being rounded into whichever bucket the reader wants.

---

## 2. The counts

**93 classified of 115 recorded.**

| | A | B | C | D | U | classified | recorded |
|---|---|---|---|---|---|---|---|
| **rig runs** | 2 | 0 | 0 | 30 | 5 | 37 | 59 |
| **block-defect register** | 8 | 17 | 0 | 26 | 5 | 56 | 56 |
| **total** | **10** | **17** | **0** | **56** | **10** | **93** | **115** |
| | 11% | **18%** | **0%** | 60% | 11% | | |

**The rig corpus's denominator, stated because the gap between 37 and 59 is not a shortfall.** 27
result packages carry 59 verdicts: **22 Pass**, 13 Inconclusive, 10 Stale, 7 Unsettled, 3 TimedOut, 2
NotObserved, 2 Fail. The 22 Passes are not failures and are not classified. **So the 37 classified rows
are the entire non-Pass set** — the rig corpus is complete, not sampled.

**Only 2 of those 37 non-Pass rig verdicts were defects in the block under test.** The other 35 were
instrument, declaration, model or deployment problems. **And both of the two are already caught by
`converter review` C-410** (`docs/06-lad-conventions.md:515` — a timer's `IN` reading that same timer's
own output; mechanised, both severities gating).

---

## 3. Both controls ran, and both passed

A classification run by the party that wants a particular answer is not evidence unless it can come
out the other way. Two controls:

- **A seeded bucket-B row** — an inverted comparison sense on a rung that a committed vector already
  covers — was inserted into the input and **landed in B**. So *"B is empty"* was a reachable outcome,
  and B's 17 is a measurement rather than a definition.
- **Three deliberately tempting rows** drawn from `docs/notes/hammer-campaign-results.md` — failures
  that read at first glance as controller behaviour — **all three landed in D, and none leaked into
  C.** C's zero is not an artefact of a rubric that cannot reach C.

---

## 4. 🔴 C = 0 is STRUCTURAL, not evidential

**The delivered plant program has never been executed.** A defect that only running on a 1214C could
expose therefore **cannot appear in the register**, because nothing has run to expose one. C = 0 is a
statement about what the corpus is able to contain, not a discovery that the controller catches
nothing.

The consequence points at B, not at C: **some rows classified B may turn out to need the controller.**
An interpreter that is not a CPU model — no scan timing, no process-image boundary, no ENO chaining,
no overflow, no optimized-vs-standard access, no retentive behaviour — will find that some of its 17
are C rows wearing a B coat. **B = 17 is therefore an upper bound on what an interpreter catches, and
the measurement cannot say how much lower the real figure is.**

---

## 5. 🔴 What the measurement cannot see at all

**Every row in both corpora is a defect somebody wrote down.** The backlog's own stated trigger for an
entry is *"this was found by luck"* (`docs/notes/mechanisation-backlog.md`, the entry-criteria block).
A defect that nobody noticed is in **no bucket**.

**The direction of that bias is the uncomfortable part.** An unnoticed defect is disproportionately
likely to be one that no PC-side reading would surface — i.e. a C row. **So the omission can only make
C look smaller than it is, which is the direction that flatters the recommendation this measurement
was commissioned to support.** Written here rather than in a caveat at the end, because it is the one
error in the study that argues against the study's own conclusion.

---

## 6. ⚠️ "The check names it" is true and weak

Bucket A means *a producer already emits this finding*. It does **not** mean anybody would have read
it. On this corpus `converter cross-check` emits **351 multi-writer facts and 250 dead-member facts**.
Most A rows are therefore a needle in a haystack of the tool's own output.

**Exactly one A row was actually found by running the tool.** The rest were found some other way and
were only afterwards confirmed to be inside a producer's output. That is a real property of bucket A
and it is not the interpreter's fault: **an interpreter's output is one verdict per assertion, not 601
facts**, and signal-to-noise is a genuine advantage it has over the static floor. It is also a warning
about A itself — *A = 10* is an upper bound on what the existing floor **could** catch, not a count of
what it **did**.

### ⚠️ And one A row was catchable by nothing on the day

**C-410 did not exist when the defect it now catches shipped.** The rule was written hours later,
*because* of that defect. Two readings, both defensible, and the choice moves a count:

- **Prospectively A** — the check exists now, so a repeat is caught now. Counted this way above.
- **Retrospectively uncatchable** — on the day, no PC-side check covered it, and *"found once
  expensively, then mechanised"* is the mechanism that produced the rule rather than an alternative to
  it.

Recorded rather than resolved. Under the second reading A is 9 and the "nothing would have found it"
population is 1 larger.

---

## 7. The counter-argument, recorded in full rather than buried

**An interpreter with no vectors catches nothing.** Every one of the 17 B rows is defined as *needs
the block executed against a stimulus* — and the stimulus is the **authored** half that §3.1 says
cannot be derived, because deriving the expected value from the block makes every test pass.

**Vector supply is the measured bottleneck.** On the only two blocks that have run a wave, coverage
against the spec-derived assertion enumeration was **2 of 96 and 3 of 96 assertions**. At that supply
rate an interpreter is offered ~3% of the stimuli its 17 rows would need.

This does not strike Phase 5. It **relocates the constraint**: the interpreter's value is bounded by
vector supply, so any plan that funds the interpreter and not the enumeration→vector path is funding
an instrument it will not feed. It also cuts the other way, and that is the stronger form of the
argument — **a vector is far cheaper to exercise against an interpreter than against a rig cycle**, so
the same vector supply buys more iterations. The bottleneck is the same either way; the question is
what each vector costs to run.

---

## 8. The cheaper alternative — and the judgement the recommendation rests on

**8 of the 10 A rows follow one pattern: found once, expensively → mechanised as a `converter` rule or
review rule.** That pattern is the existing, proven, cheap route. It costs a table entry or a rule,
not a second IR parser, and it examines every block on every run forever after.

So the honest question is not *"is B large?"* — it is ***"does B mechanise the same way?"***

**The assessment is that the 17 B rows do not cluster into two or three rules.** 🔴 **This is a
judgement, not a fact, and it is the load-bearing element of the whole recommendation.** It is stated
here as a judgement so that it can be attacked directly, because if it is wrong the recommendation is
wrong: if the 17 collapse into a small number of mechanisable shapes, the correct action is to write
those rules and **not** build an interpreter.

What would settle it is not more classification — it is an attempt to write the rules. **That attempt
has not been made.** Anyone reversing this recommendation should reverse it by producing two or three
rule specifications that cover the 17, not by re-reading this page.

---

## 9. What this decides

**B = 17 of 56 on the register is large enough that Phase 5 is not struck.** But the 2-of-37 rig
result says the phase's *stated purpose* is the weak one:

- **As a pre-filter before a rig cycle** — what `docs/18` §4.4 and §5 Phase 5 describe — the evidence
  is **weak**: 2 of 37 rig failures were block defects, and both were already covered by a static rule.
  An interpreter placed there filters almost nothing, because almost nothing that fails on the rig is
  the block.
- **As an instrument in the design and review loop** — evaluating a block against the specification's
  assertions before any harness, binding, slot or download exists — the evidence is **strong**: 17 of
  56, currently caught only by fresh-context adversarial review, which is expensive, correlated with
  the coder that produced the block, human, and demonstrably incomplete.

The redirect is recorded in `docs/18-project-workbench.md` §5 Phase 5 and §4.4.

---

## What is deliberately not written down

- **The per-defect row table**, for both corpora. It stays in the job folder. Every count above can be
  checked against its denominator there.
- **The shape descriptions for the bucket-B rows.** A B row's description is *what an interpreter would
  have needed* — and that sentence is where a real signal name gets in, because naming it is the most
  natural way to be precise. The generic classes are safe to state (*a stimulus; a plant model; timer
  semantics; an execution order*) and are stated. **Anything narrower than that class level was left
  out entirely rather than blurred**, because a blurred description of one defect is still a
  description of one defect.
- **The aggregate as a statement about the job.** *"N defects in this plant's blocks would only have
  been caught on the rig"* is a claim about a site's engineering. It is confined to the tooling
  question it was asked for — whether to build an interpreter — and is not repeated elsewhere.
