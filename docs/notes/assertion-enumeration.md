# THE SPEC-DERIVED ASSERTION ENUMERATION — definition, derivation, IDs, buckets

**Status: DRAFT, 2026-08-13.** Written because `test-environment-contract.md` §3 requires a vector's
`Basis` to cite an assertion ID **"drawn from the spec-derived enumeration"**, and that enumeration
had no definition. Without one, the skill enforcing that citation can only check that *something* was
written in the field — ***the difference between a gate and a formality.***

**What §7 and D35 already settle, and this document does not revisit:**

- ***THE DENOMINATOR COMES FROM THE SPECIFICATION, NEVER FROM THE TEST SUITE.*** Any unit defined by
  what somebody wrote a vector for is self-referential: you cannot be missing an assertion nobody
  wrote, so coverage is always 100% and the gate is theatre.
- The unit is the **assertion, qualified by instance** — and *both axes* are spec-side: the
  assertion set **and** the instance set are the enumerator's (R4, amended 2026-08-13; see the
  correction under §1.2, which is the sharpest thing in this document). Decomposition is a **spec-side** operation,
  complete before any vector exists. A vector **cites into** the enumeration and cannot extend it.
- Four buckets; the gate is **`UNCLASSIFIED = 0`**, not a percentage; it **stops the claim, not the
  run**.

***WHAT THIS DOCUMENT ADDS IS THE ONE THING THAT MAKES ANY OF THAT REAL: WHAT AN ASSERTION IS,
PRECISELY ENOUGH THAT TWO PEOPLE ENUMERATING THE SAME REQUIREMENT PRODUCE THE SAME SET.*** If the
decomposition is ambiguous then the denominator is negotiable, and a negotiable denominator makes
coverage unfalsifiable — you can always reach 100% by counting generously. **The definition is the
deliverable. The ID scheme is bookkeeping.**

---

## 1. The definition

> ***AN ASSERTION IS THE SMALLEST STATEMENT ABOUT A BLOCK'S OBSERVABLE BEHAVIOUR THAT CAN BE
> FALSIFIED ON ITS OWN.***
>
> Formally: a statement `A` derived from clause `C` is an assertion of `C` if there exists a possible
> implementation defect that makes `A` false **while leaving every other assertion of `C` true**.

**Independent falsifiability is the whole test**, and it is deliberately a test about *defects*
rather than about grammar. It is what stops both failure directions:

- **Too coarse** — "shall open when X and close when Y" as one assertion. A wrong contact on the open
  rung falsifies half of it, so it fails the test and must split. (This is §7's own worked example of
  a clause going silently half-covered.)
- **Too fine** — "shall open when X" split into "the coil energises" and "the coil stays energised".
  No defect falsifies one without the other under the same trigger, so it is one assertion.

### 1.1 The canonical form — and it does most of the work

Every assertion is written in exactly one of two forms:

```
WHEN <trigger> THEN <observable response> [ WITHIN <bound> | FOR <duration> ]
NEVER <forbidden observable state>
```

***THE TEMPLATE IS NOT STYLE. IT IS WHAT MAKES THE DECOMPOSITION RULES MECHANICALLY VISIBLE*** — one
`WHEN`, one `THEN`, and anything that will not fit that shape is a clause that has not been
decomposed yet. An assertion that does not match one of the two forms is **rejected**, which converts
most of the hard judgement into a check.

The `NEVER` form exists for interlocks and prohibitions — *"the pump shall not run with the guard
open"* — which have no natural trigger and would otherwise be forced into a contorted `WHEN`. It is
independently falsifiable in exactly the same sense.

*(Hard rule 2 is untouched: safety-instrumented functions are not enumerated, tested, or described
here at all. `NEVER` is for ordinary control interlocks.)*

### 1.2 The five decomposition rules

| | rule | |
|---|---|---|
| **R1** | **SPLIT ON TRIGGER.** A distinct trigger condition is a distinct assertion. | Includes the **negative case** wherever the spec states one — *"and shall not open otherwise"* is its own assertion, and it is the one most often lost. |
| **R2** | **SPLIT ON RESPONSE.** A distinct observable output or state change is a distinct assertion. | Two responses to one trigger are two assertions: a defect can produce one and not the other. |
| **R3** | ***DO NOT SPLIT ON QUALIFIERS.*** Timing bounds, tolerances, units, hysteresis, and persistence are **attributes of a response**, not separate assertions. | *"Opens within 2 s when X"* is **one** assertion, not "opens" plus "within 2 s". A timing defect is a failure **of that assertion**, reported as observed-versus-expected. |
| **R4** | **SPLIT ON INSTANCE.** The coverage unit is `(assertion, instance)` — §7/D35, with `relation-reconcile`'s `(instance, relation-id)` as precedent. | ***THE INSTANCE SET IS DECLARED BY THE ENUMERATOR, FROM THE CLAUSE TEXT*** (`EnumeratedClause.Instances`). The assertion itself is written once, per **class**; the declared instance set multiplies it into coverage units. **A citation SELECTS from that set. It cannot add to it, and an instance a citation names that the enumerator did not declare is a FINDING, not a new unit.** |
| **R5** | ***NEVER SPLIT ON IMPLEMENTATION.*** An assertion may not name a rung, an operator, an address, a block or a tag internal to the design. | If a proposed split can only be *described* in terms of **how** the thing is built, it is not a split — it is a peek at the implementation, and it re-correlates the check (the same disease M2 names for models). |

> 🔴 ***R4 WAS AMENDED ON 2026-08-13 BY OWNER RULING, AND THE WORDING IT REPLACES WAS MINE AND WAS
> EXPLOITABLE. IT IS RECORDED RATHER THAN REWRITTEN BECAUSE THE FAILURE IS SUBTLE ENOUGH TO BE
> REINTRODUCED BY SOMEONE TIDYING THE SENTENCE.***
>
> R4 used to read: *"Instance qualification happens at citation time; the enumeration itself is per
> class, and instances multiply it."* Read literally — and the coverage lane did read it literally —
> ***THAT MAKES THE DENOMINATOR'S SIZE DEPEND ON WHAT THE CITATIONS MENTION.*** If instances enter
> the count when a vector names one, then ***AN INSTANCE NOBODY WROTE A VECTOR FOR COULD NOT BE
> MISSING***, because it was never in the denominator to begin with. Three feeders with vectors for
> two reports 100%.
>
> **That is §7's self-referential trap arriving by a side door** — the exact thing §7 rejects by
> name, reintroduced one level down at the instance axis while the assertion axis stayed clean.
>
> ***AND IT IS THE ONE INFLATION ROUTE THAT NEEDS NO SECOND PARTY AT ALL, WHICH IS WHY IT MATTERS
> MORE THAN ITS SIZE SUGGESTS.*** Every other route in §5 has to get past somebody: a coarse
> decomposition faces the vector author's dispute channel, a bucket laundering faces the gate-1
> signer. This one needs no accomplice and no untruth — the vector author simply writes vectors for
> the instances they thought of, and the denominator obligingly shrinks to fit. **The structural
> defence every other route relies on does not apply to it.**
>
> **The ruling: the instance set is the enumerator's, declared from the clause text.** `Each of the
> three feeders` declares three, whether or not anyone ever cites the third.
> **The code already took this reading** (`EnumeratedClause.Instances`); this doc was the only thing
> that made the other reading available, and it should not be the thing that reopens it.

### 1.3 Worked examples

| clause text | assertions | why |
|---|---|---|
| *"The valve shall open when the level is above setpoint and close when it falls below the deadband."* | **2** | R1/R2 — a defect on the open rung leaves the close behaviour intact |
| *"The pump shall start within 3 s of the start command."* | **1** | R3 — the bound qualifies the response |
| *"On a fault the drive shall stop and the alarm shall latch."* | **2** | R2 — two responses, independently breakable |
| *"The conveyor shall not start unless the guard is closed."* | **1**, `NEVER` form | prohibition, no natural trigger |
| *"Each of the three feeders shall stop on low level."* | **1 assertion × 3 instances** | R4 — one class-level statement; the enumerator declares the three, giving three coverage units |
| *"The mixer shall run for 30 s then stop."* | **2** | R1 — *start-on-command* and *stop-after-30-s* have different triggers; a preset defect breaks only the second |

---

## 2. Why two independent enumerators would agree

**They would not, always — and the design does not depend on it.** Claiming otherwise would be the
same optimism this project keeps having to retract. Three mechanisms, in increasing order of how much
weight they carry:

**2.1 The template and the rules make most cases syntactic.** R1 and R2 are readable off the clause
text: count the triggers, count the responses. R3 removes the largest source of honest disagreement
(does a timing bound count separately?) by ruling it out. R5 removes the largest source of
*dishonest* disagreement. On the corpus of clause shapes above, the decomposition is determined.

**2.2 The falsifiability test is a procedure, not an intuition.** Where the rules leave a choice, the
tie-breaker is concrete: ***name the defect that breaks this half and not the other.*** If you cannot
name one, it is one assertion. Two enumerators running that procedure on the same clause are doing
the same work, not exercising the same taste.

**2.3 — AND THIS IS THE ONE THAT ACTUALLY MATTERS — *DISAGREEMENT IS MADE VISIBLE RATHER THAN
PREVENTED*.** An enumeration only its author can reproduce is a correlated check; the fix is not to
demand agreement but to route the disagreement:

- ***THE VECTOR AUTHOR IS A SECOND READER BY CONSTRUCTION*** (D6 — a different agent from the block's
  author). The citation step is where their reading of the clause meets the enumeration. **If they
  need to cite an assertion that is not there, that is a decomposition dispute**, and it is raised as
  an event — *never* resolved by the vector author writing prose into `Basis`, which the contract
  already forbids.
- ***A RE-DECOMPOSITION THAT YIELDS A DIFFERENT COUNT IS REPORTED AS ITS OWN EVENT*** (§7). A clause
  previously read as one assertion that later decomposes into two produces a **new, UNCLASSIFIED
  assertion**, and the gate fails until somebody rules on it. It is explicitly *not* absorbed as a
  classification change.
- **The assertions-per-clause ratio is reported.** An enumeration far off the corpus norm is visible
  without anyone having to audit it.

> ***SO THE HONEST CLAIM IS: TWO ENUMERATORS WILL USUALLY AGREE BECAUSE THE RULES ARE MOSTLY
> SYNTACTIC, AND WHERE THEY DISAGREE THE SYSTEM SURFACES IT INSTEAD OF SILENTLY PICKING ONE.***

---

## 3. The ID scheme

### 3.1 Form

```
REQ-014:3f9a1c                 an assertion of clause REQ-014
REQ-014:3f9a1c@Feeder_02       the coverage unit — assertion qualified by instance (R4).
                              *** `Feeder_02` MUST BE ONE OF THE CLAUSE'S DECLARED INSTANCES;
                              a citation naming an undeclared one is a finding, not a new unit ***
```

- **`REQ-014`** — the clause's **stable ID from the requirements register**.
- **`3f9a1c`** — the first 6 lowercase hex of `SHA-256(normalised assertion text)`, scoped to the
  clause.

**Normalisation, specified so two implementations agree:** trim; collapse internal whitespace runs to
a single space; strip one trailing `.` or `;`; **preserve case and everything else**; hash the UTF-8
bytes. *(Case is preserved deliberately — folding it risks merging two distinct signal names.)*

**Two assertions in one clause that normalise identically are a duplicate, not a collision** — an
error in the enumeration, reported as such.

### 3.2 The stability property, stated as a property

> ***AN ID DEPENDS ONLY ON (CLAUSE IDENTITY, ASSERTION CONTENT). IT DEPENDS ON NOTHING POSITIONAL —
> not on its index within the clause, not on how many siblings it has, not on the order of the
> document.***

Consequences, which are the point:

- Inserting a clause **above** shifts nothing.
- Inserting an assertion **into** a clause shifts none of its siblings.
- **Editing an assertion's text DOES change its ID — deliberately.** The old ID becomes dangling,
  every prior citation to it is flagged **stale**, and the enumeration records a `supersedes:` link
  so the change is a visible event rather than a silent inheritance. ***A CITATION THAT SILENTLY
  SURVIVES A REWORDING OF WHAT IT CITES IS THE FAILURE THIS AVOIDS*** — the vector was written
  against the old words and nobody re-read it.

> 🔴 ***THIS SUPERSEDES §7's SKETCH OF THE STICKY KEY, AND THE REASON IS THE ONE THE ID SCHEME EXISTS
> TO PREVENT.*** §7 proposes `(clause hash, assertion index, assertion text hash)`. **The middle term
> is positional** — insert an assertion at index 1 and every later index shifts, so a stored
> classification, or a citation, silently comes to name a different assertion. §7 uses that key for
> *stickiness of classification* rather than for citation, so the hazard is narrower there than it
> looks — but the two must not use different keys, and only the content-derived one is safe. **The
> index is retained as a DISPLAY ORDINAL and never as an identifier** (§3.3).

**Precondition, and it is worth checking before any of this is built:** the clause IDs themselves must
be stable. `REQ-014` is; *"§3.2, fourth paragraph"* is not. If a requirements register addresses
clauses positionally, **pin stable clause IDs first** — otherwise the assertion IDs inherit the
instability they were designed to avoid.

### 3.3 The display ordinal, and why it may never be cited

Listings show `REQ-014.A2` because `REQ-014:3f9a1c` is unreadable aloud. ***THE ORDINAL IS DISPLAY
ONLY. A `Basis` citation in the ordinal form is REJECTED*** — mechanically, by shape — precisely
because it is the readable one and would otherwise be the one people type.

---

## 4. The buckets, and what makes `UNCLASSIFIED = 0` enforceable

The four buckets are §7's: **COVERED**, **OUT-OF-SCOPE**, **DEFERRED**, **UNTESTABLE-ON-RIG**.

### 4.1 The mechanism: UNCLASSIFIED is computed, never written

> ***NOBODY EVER WRITES "UNCLASSIFIED". IT IS A SET DIFFERENCE.***
>
> ```
> UNCLASSIFIED = enumerated − (COVERED ∪ OUT-OF-SCOPE ∪ DEFERRED ∪ UNTESTABLE-ON-RIG)
> ```

**This is what makes the zero enforceable rather than aspirational.** An assertion cannot be *left
lying around* in a state nobody noticed, because "unclassified" is not a state anyone can write —
it is what remains when the enumeration is differenced against the classification. Forgetting one
does not produce a missing tick; **it produces a non-zero residual and a failed gate.**

The precedent is exact and recent: phase 2's `AddressesExamined` became a **separate denominator**
because a rule that was correct and *never established that it had run* is not a check. Same shape
here: **the enumeration is the denominator, the classification is a separate artifact, and the
residual is computed across them.**

> ***AND EMPTY IS NOT CLEAN (FI-44).*** An enumeration that parses **zero** assertions is
> *nothing examined* and fails — never a pass. A clause with no assertions is an error in the
> decomposition, not a clause with nothing to say.

### 4.2 STARTUP is a routing tag, not a fifth bucket

§7 says first-scan and power-up requirements are *"classified as a STARTUP TEST class, never as
UNTESTABLE-ON-RIG"*, which reads as a fifth bucket and would break *exactly one bucket each*.
**Proposed resolution: STARTUP is a scheduling attribute, orthogonal to the bucket** — it says
*where* the assertion runs (X-F's disruptive boundary), not whether it is covered. A STARTUP
assertion is still COVERED or DEFERRED like any other. Flagged in §6 as needing confirmation.

### 4.3 Who may assign the two escape-hatch buckets

§7 rules it and it is worth repeating here because it is the whole defence against laundering:
**OUT-OF-SCOPE and UNTESTABLE-ON-RIG are assigned by whoever signs off the architecture at gate 1 —
never by the block's author and never by the vector's author**, both of whom have an incentive to
reach for them under pressure. **Mechanically checkable:** the assigning identity is recorded, and a
match against either author is a refusal.

---

## 5. ***How an agent would inflate coverage without lying, and what closes each route***

***COVERAGE IS THE EASIEST NUMBER IN THIS PROJECT TO FAKE AND THE ONE MOST LIKELY TO BE QUOTED.***
Each row is an attack that requires no dishonesty — only a generous reading.

| # | the attack | what closes it | residual risk |
|---|---|---|---|
| 1 | **Coarse decomposition.** Enumerate a two-assertion clause as one, write one vector, report 100%. | R1/R2 + the falsifiability procedure; the template makes multiple triggers/responses visible; the vector author's dispute channel (§2.3); assertions-per-clause ratio reported | a determined coarse reading survives if **nobody** disputes it — mitigated, not eliminated |
| 2 | **Fine decomposition of the easy parts.** Split trivially-covered behaviour into five, leave the hard clause as one. | same rules, applied symmetrically; the ratio report makes an outlier clause visible in **both** directions | as above |
| 3 | ***Bucket laundering*** — push hard assertions into OUT-OF-SCOPE or UNTESTABLE-ON-RIG. | §4.3 — neither author may assign them; §7: UNTESTABLE-ON-RIG *should be nearly empty, and a large one is a finding to chase*; bucket populations and their **trend** reported every wave | judgement, but not the interested party's |
| 4 | ***Deferral drift*** — everything becomes DEFERRED and the gate still passes. | §7's own honest limit. **DEFERRED carries owner + date; count AND AGE reported every wave.** *Ageing deferrals are the signal, not the classification* | real, and explicitly accepted — visibility is the whole defence |
| 5 | **Citing an assertion the vector does not actually test.** | **Partly mechanical:** the assertion's response signal must appear in the citing vector's `Expectations` — a set-difference, the same shape as `trace`'s guard-containment hop | mentioning a signal is not testing it — the rest is judgement |
| 6 | **Quoting a bare percentage.** | ***A COVERAGE FIGURE MAY NOT BE REPORTED WITHOUT ALL FOUR BUCKET COUNTS AND THE OLDEST DEFERRAL'S AGE.*** Mechanically enforced in the report generator: a bare percentage is not an available output | none — this one closes cleanly |
| 7 | **Padding the denominator.** | No incentive: padding *lowers* the covered fraction. And a "assertion" nothing could falsify fails the §1 definition | none |
| 8 | 🔴 ***INSTANCE SHRINKAGE — write vectors for two of three feeders and let the third never enter the denominator.*** Found 2026-08-13, and it needed no dishonesty and no accomplice | **R4 as amended:** the instance set is declared by the ENUMERATOR from the clause text, so the third feeder is in the denominator whether or not anyone cites it. A citation **selects**; an undeclared instance is a finding | closed **only because R4 was amended** — the previous wording left it open |

**The structural reason this is defensible at all:** coverage is a ratio of **two spec-side counts**
(enumerated, classified) and **one vector-side count** (cited). ***ONLY THE VECTOR-SIDE COUNT IS
UNDER THE TEST AUTHOR'S CONTROL, AND IT IS THE NUMERATOR.*** The denominator is produced before any
vector exists, by someone else, and is reviewed.

> 🔴 ***THIS PARAGRAPH USED TO END: "Every attack above is an attack on the denominator, which is why
> they all have to route through a second party." ROUTE 8 FALSIFIED THAT, AND THE CORRECTION IS THE
> USEFUL PART.*** Route 8 attacked the denominator **without routing through anyone**, because the
> old R4 let the *numerator's* authors decide part of the denominator's *size*. The second-party
> defence is real but it is **not automatic** — it holds only where the denominator is genuinely
> produced elsewhere, and any wording that lets a citation influence what counts as complete quietly
> removes it.
>
> ***SO THE STANDING TEST FOR ANY FUTURE CHANGE TO THIS DOCUMENT IS: CAN A VECTOR AUTHOR, ACTING IN
> GOOD FAITH AND WRITING NOTHING FALSE, MAKE THE DENOMINATOR SMALLER?*** If yes, the change is
> wrong however reasonable it reads. That question would have caught R4's original wording, and
> nothing else did for the length of time it stood.

---

## 6. Mechanically checkable, versus judgement

Matching `test-environment-contract.md`'s column, because the skill needs exactly this split.

### ✅ Mechanical

| check | by what |
|---|---|
| Assertion matches one of the two canonical forms | shape |
| ID recomputes from the normalised text | **rehash and compare — a hand-edited ID is caught** |
| IDs unique within a clause; identical normalisations are a duplicate error | set |
| No `Basis` citation names an ID absent from the enumeration | dangling-citation check |
| **No citation names an INSTANCE the clause did not declare** (R4) | set-membership against `EnumeratedClause.Instances` — **a finding, never an added unit** |
| **Every declared instance appears in the denominator** whether or not anything cites it | count = assertions × declared instances, computed spec-side |
| No `Basis` citation uses the display-ordinal form | shape |
| The cited assertion's response signal appears in the vector's `Expectations` | set-difference |
| Every assertion in **exactly one** bucket; `UNCLASSIFIED` residual = 0 | **computed, never declared** |
| Enumeration is non-empty | *empty is not clean* |
| OUT-OF-SCOPE / UNTESTABLE-ON-RIG assigner ≠ block author, ≠ vector author | recorded identity |
| DEFERRED carries owner + date; age computed and reported | fields |
| Re-decomposition count differs from last time → reported event | compare |
| Report carries all four bucket counts and the oldest deferral age | report generator |
| Assertion text names no rung, operator, address, block or internal tag (R5) | keyword/reference scan against the design |

### ⚖️ Judgement — and stated as such rather than dressed up

- **Whether the decomposition is faithful and at the right grain.** The rules constrain it; they do
  not determine it for every clause. This is the irreducible one.
- **Whether a bucket assignment is honest** (§4.3 puts it with a disinterested party; it does not
  make it mechanical).
- **Whether a vector genuinely exercises the assertion it cites** — only the signal-mention half is
  mechanical.
- **Whether an assertion is a correct reading of the clause at all.**

> ***A COVERAGE FIGURE RESTING ON AN UNENUMERABLE DENOMINATOR MUST SAY SO RATHER THAN PRINT A
> PERCENTAGE.*** If a requirements register has no stable clause IDs (§3.2's precondition), or a
> clause has not been decomposed, the correct output is **"not enumerable"** and not a number. The
> gate proves someone **looked**, never that they looked **well** (§7's own honest limit).

---

## 7. What this leaves open

1. ***STARTUP: fifth bucket or routing tag?*** §4.2 proposes routing tag, so *exactly one bucket*
   survives. **Needs confirmation** — it is a one-word change to §7 either way.
2. **The dispute channel has no home yet.** §2.3 makes the vector author's failed citation the
   decorrelating mechanism, but nothing says *where* that dispute is recorded or *who* rules on it.
   The natural answer is the gate-1 signer, by analogy with §4.3. **Not decided here.**
3. **Who performs the enumeration?** §7 says it is spec-side and complete before any vector exists;
   it does not say by whom. If it is the block's author, D6's independence is lost at the
   denominator — which would undo most of this document. ***THIS IS THE ONE THAT MATTERS MOST OF THE
   THREE, AND IT SHOULD BE RULED BEFORE THE SKILL IS BUILT.***
   ➜ 🔴 **R4's amendment (2026-08-13) makes this MORE load-bearing, not less.** The enumerator now
   owns the **instance set** as well as the assertion set, so a compromised enumerator can shrink
   the denominator on *two* axes rather than one. Whatever independence rule is chosen has to cover
   the whole enumeration, not just the decomposition.
4. **The assertions-per-clause norm is unmeasured.** §5 rows 1 and 2 lean on an outlier report, and
   there is no corpus yet to be an outlier against. It becomes useful after the first real
   enumeration, not before — recorded so nobody quotes a threshold that was never measured.
