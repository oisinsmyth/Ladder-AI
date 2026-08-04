# Four-rung spec pipeline — adversarial design validation, ROUND 2

**Purpose.** Round 1 (`docs/evidence/four-rung-design-validation.md`) broke the A→B→C→D pipeline on
two of the four real failures and raised ten weaknesses. Amendments R1–R8 were then applied
(commit `b66c1f8`, *"skills: apply R1-R8 amendments from the adversarial design validation"*). This
round re-tests the **amended** contracts against the same four ground-truth failures
(`docs/evidence/PlantAutoControl-bench-grading.md`, diagnosed in
`docs/evidence/PlantAutoControl-bench-autopsy.md`) and hunts specifically for **survival paths the
amendments left open** and **new weaknesses the amendments introduced**.

**Method.** Same as round 1: walk each failure A→B→C→D quoting the clause that acts, verbatim, from
the four `SKILL.md` files as they read today; rate **PREVENTED** / **CAUGHT** / **SURVIVES**. Every
IR claim is grep-grounded against `ir/PlantAutoControl-bench/` and quoted.

**Run type.** Manual (skill-less) design validation performed inside `lad-coder` per CLAUDE.md hard
rule 8 — a read/reason pass over LAD/IR content and its governing skills. No IR was written, so no
compile gate applies and no `gen/<project>/telemetry.log` line was appended (not a generation-stage
run). No skill file was edited.

---

## Headline

**4 CAUGHT, 0 SURVIVES, 0 PREVENTED — inside the rungs.** The amendments genuinely closed both of
round 1's SURVIVES. But the win is narrower than the scorecard suggests:

- Every catch is an **escalation to the engineer** (a blocking `Q-nn`) or **an argued ledger row**,
  and the *trigger* for every one of them is still a **single reader's self-judgement**. Nothing
  mechanical computes "there was a choice here."
- **Failure 1 re-opens through rung B**, which never received R4's input-side rule (NW-1).
- **Two new silent-ship paths were created by the amendments themselves**, both at the new derived-view
  seams R6 introduced (NW-2, NW-3) — one of which can make the reviewer *recommend deleting* a
  correctly-rendered interlock.

| # | Failure | Round 1 | Round 2 | Acting clause (round 2) |
|---|---|---|---|---|
| 1 | REQ-003 filter cascade hold (REGRESSION) | CAUGHT | **CAUGHT** *(survives via rung B — NW-1)* | A explicitness §1 input-side split; C §5 merge guard; D2 `unverifiable` ⇒ render |
| 2 | REQ-012 rotation-sensor bypass (DEFECT) | CAUGHT *(conditional on a nonexistent reference)* | **CAUGHT** *(now reference-free; conditional on instance scoping)* | C §8 unclaimed-signal sweep (`Bypass*` ⇒ BLOCKING); D1 undriven-input audit |
| 3 | REQ-017 "not faulted" narrowed (DEFECT) | **SURVIVES** | **CAUGHT** | C §2 candidate set (FB status members now enumerable); D1 binding audit `rebind` |
| 4 | REQ-019 feedback pairing swapped (DEFECT) | **SURVIVES** | **CAUGHT** *(single-thread, no backstop)* | C §2: *"Two requirements competing for two same-shaped signals is always a candidate-set of two"* |

---

## Failure 1 — REQ-003, FilterUnitSystem cascade hold dropped (REGRESSION)

**Ground truth.** The answer key holds each filter running during controlled shutdown on **both**
`PlantControl.FansShutdownReady` **and** the dust-generating neighbour's `ShutdownComplete`; the
generated block kept only the global flag (grading §2, AK L47 vs gen L48). Source form: one cell,
`"Fans-shutdown-ready / Air-separator VSD shut down"`.

### Rung A — the `/` now bites on the input side (R4 applied)

> **One relation per line — on the way in as well as the way out.** … **never resolve one by
> reading** in what you consume. An ambiguous separator in a SOURCE is **split into two relations,
> both retained** (the default), or raised as a blocking `Q-nn`. **Dropping either side always
> requires the `Q-nn`** — an appositive reading ("A, i.e. B") is a resolution and is never taken
> silently.

Round 1's W9 (an output-formatting rule solving an input-parsing failure) is **genuinely closed for
rung A**. The rule is token-keyed (`/`, "and/or", a comma) and the documented trigger token is
exactly the one that caused the regression. An analyst reading `"fans ready / air separator down"`
in a layout note must now split or raise `Q-nn`; the appositive reading is named and forbidden.

Independent redundancy: A §3 derives the neighbour hold from topology anyway — *"controlled shutdown
holds until the receiving machine reports complete"*, stated once and applied *"without exception"*.
For the failure to reach C, **both** A's parse and A's convention must miss it.

### Rung B — the hole (NW-1)

**R4 was applied to rung A only.** Rung B's contract has no ambiguous-separator rule at all. Its
nearest clause is §6:

> **Never resolve a contradiction** — inside the document, or against rung A. Record both readings
> and raise a `Q-nn`.

A `/` inside a single statement is **not a contradiction between two readings** — it is one sentence
an analyst reads one way. §6 does not fire. B writes one behaviour line, scopes it `applies-to: dust
filter 1, 2, cyclone`, and the second condition never exists in any artifact.

This matters because of where the ambiguity actually lives in practice. In the four-rung world there
is no requirements register to carry an inventory table; the sources are the **P&ID (A)** and the
**supplied functional description (B)** — and prose ambiguity is overwhelmingly a property of the
*functional description*. R4 was installed on the rung that reads drawings and omitted from the rung
that reads prose.

**Named survival path.** Source ambiguity enters via B → one behaviour line → C has one line to layer
(§4) → C §5's merge guard never fires (there is nothing to merge) → D2's ledger set-differences to
empty (the relation was never a relation) → nothing fails anywhere. Requires A's topology convention
to *also* miss the relation — a compound miss, which is why this rates CAUGHT rather than SURVIVES,
but it is a complete silent-ship path and it is one edit away from closed.

### Rung C — merge guard holds (R5 applied)

> **A merge is permitted only when both lines resolve to the same signal or the same named plant
> condition.** Two conditions that are both true, both must hold, and resolve to **different**
> signals are **two `P-nn` lines**, always — however closely related their process meaning. …
> *A plant-level "fans ready to shut down" flag and a specific neighbour's "shutdown complete" are
> not the same fact.*

Round 1's W10 is **closed**, and closed with the exact case named. One residual note: the guard's
test is *"resolve to the same signal"*, but the neighbour term resolves to an **FB interface member**
(`AirStarInst1.Outputs.ShutdownComplete`) which C is forbidden to write. In practice the two are
plainly differently-named so the guard still fires, but the test is phrased in a vocabulary C is only
half-allowed to use (see NW-4).

### Rung D — `unverifiable` ⇒ render (R2 applied), with a live citation loophole

> **A discharge must be explicit, argued, and preconditioned — and its precondition must be
> VERIFIABLE FROM ARTIFACTS IN SCOPE.** Classify every precondition as one of:
> **`verified-in-block`** (cite the network), **`verified-cross-block`** (cite the block, file and
> line in `ir/<project>/`), or **`unverifiable`**. **An `unverifiable` precondition is not a valid
> discharge — render the relation as a term instead.**

Ground truth check, grep-run this session:

```
$ grep -rn "FansShutdownReady" ir/PlantAutoControl-bench/
ir/PlantAutoControl-bench/Control.ir:14:    FansShutdownReady : Bool
$ grep -rn "COIL.*FansShutdownReady" ir/
(no matches)
```

`FansShutdownReady` has **no writer anywhere in the IR corpus** — only a DB member declaration
(`Control.ir` is `DB PlantControl`, NUMBER 39). So the honest classification of *"FansShutdownReady
aggregates AirStarInst1.Outputs.ShutdownComplete"* is `unverifiable`, and R2 forces the term to be
rendered. **Round 1's survival path 3 is closed on the honest path.**

**But the loophole is real and demonstrable with this exact case.** `verified-cross-block`'s stated
requirement is a *citation form*: "cite the block, file and line in `ir/<project>/`". A row reading

```
P4 | discharged | FansShutdownReady encodes fan-side completion — ir/PlantAutoControl-bench/Control.ir:14 | verified-cross-block
```

satisfies the letter of the rule exactly: that is a block, a file, and a line, in `ir/<project>/`.
Nothing in D2 requires the cited line to be a **writer** of the member, nor to **demonstrate** the
semantic claim. R2 constrains the *form* of the evidence, not its *probative value*. This is the
precise answer to the stress-test question: **yes, `verified-cross-block` can be claimed with a
citation that does not prove the precondition, and the failure-1 case is the worked example.**

What stops it: only `review-functional`'s new Inputs bullet (R7) — *"every `discharged` row is a
**claim to be independently re-derived from the IR**, never accepted as stated: check the cited
precondition actually holds"*. That is a genuine assigned reader (round 1's W7 closed), but it is a
single judgement step with no mechanical assist, and it carries its own new problem (NW-5).

### Verdict — **CAUGHT**

Mechanisms: A's input-side split, A's topology convention, C §5's guard, D2's `unverifiable` rule,
R7's ledger reader. Not PREVENTED: rung B has no input-side rule (NW-1) and `verified-cross-block`
accepts a non-probative citation (RW-1).

---

## Failure 2 — REQ-012, discharge-VSD rotation-sensor bypass dropped (DEFECT)

**Ground truth.** Answer key drives `MotorVSDInst1.IO.RotationSensor := NOT
HMIControlSignals.BypassAirStarDCRotSen` and S/R-latches `RunningFwdFB` on the bypass; the generated
block wrote `RunningFwdFB := DiscreteInputs.AirStarDCRotSen` unconditionally and never referenced the
bypass tag (grading §2, REQ-012). Grep-confirmed this session:

```
ir/PlantAutoControl-bench/HMIControlSignals.ir:29:    BypassAirStarDCRotSen : Bool RETAIN
ir/PlantAutoControl-bench/Input.ir:45:    AirStarDCRotSen : Bool
ir/PlantAutoControl-bench/MotorVSDSystem.ir:56:      RotationSensor : Bool
```

### Rung A — recording, still not discovery (R3 applied, partially effective)

> **`deltas : none` is NEVER written bare.** Write `deltas : none-found — searched: <sources>`,
> naming what you actually checked … *A bare `none` records an absence of looking, not an absence of
> deltas — and rung A cannot see the IO table, so a delta carried only by an operator/bypass signal
> is invisible here by construction and must be re-hunted at rung C (`gen-equipment-spec` §8).*

The honest and important part of R3 is the second half: the rule now **admits rung A cannot find this
class of delta** and **forwards the duty to a named downstream clause**. That converts round 1's W1
from a silent hole into a declared handoff. The `searched:` list itself is close to cosmetic — see the
stress-test answer below — but the forwarding is what carries the failure.

### Rung C §8 — the real catch, and it is now reference-free

> 8. **Unclaimed-signal sweep (the Pass-2 of this rung).** … Every signal in the IO table scoped to
>    this instance is either **bound** to a requirement above, or listed under `UNCLAIMED` with a
>    reason. An unclaimed signal whose name implies a control function — `Bypass*`, `*Select`,
>    `*Inhibit`, `*Enable`, `*Override` — is a **BLOCKING `Q-nn`** and a candidate **delta**, never a
>    silent omission.

`BypassAirStarDCRotSen` matches `Bypass*` verbatim → BLOCKING `Q-nn` + candidate delta. **This is the
most important single improvement in the amendment set**, because round 1's catch for this failure
depended entirely on C §1's *"complete by construction from the class reference"* — and

```
$ find . -iname "reference.md" -not -path "./.git/*"
(no results)      $ ls references
NO references dir
```

`references/<class>/reference.md` **still does not exist anywhere in the repo** (round-1 W3 is
untouched). C §8 catches this failure without needing one. That is a genuine structural upgrade, not
a paper one.

### Rung D — the backstop exists but is *coupled* to the belt

> **Undriven-input audit.** Enumerate the chosen FB's input/interface members and mark each
> **driven** (cite the D3 term) or **defaulted** (state why). An undriven input whose name matches an
> unclaimed IO signal is a **HARD FAIL.**

`MotorVSDSystem.ir:56 RotationSensor : Bool` is undriven in the generated block, and `AirStarDCRotSen` /
`BypassAirStarDCRotSen` are name-matches. So the hard fail fires — **but only if the sweep already
listed the signal as unclaimed.** The escalation clause reads "*matches an **unclaimed** IO signal*".
If C §8 never scoped the signal to this instance (see the survival path below), the audit's honest
disposition is `defaulted` with a plausible why ("this instance's rotation sensor is wired direct; the
FB input is unused"), and nothing fails. **The two mechanisms R3 installed as belt-and-braces are not
independent: the braces are conditioned on the belt.**

### Named survival path — who scopes a plant-level HMI DB to an instance?

C §8 sweeps *"every signal in the IO table **scoped to this instance**"*. The bypass lives in
`DB HMIControlSignals` (NUMBER 38) — a **plant-level operator DB**, not an instance IO block. An
analyst who scopes `DiscreteInputs.AirStarDC*` to `MotorVSDInst1` and treats `HMIControlSignals` as
plant-level material sweeps it under **no instance at all**, and there is **no project-level residual
pass** anywhere in rung C requiring that the union of the per-instance sweeps cover the whole IO
table. The signal that caused the real defect sits in exactly the DB most likely to fall through this
gap — and it has a sibling (`HMIControlSignals.ir:28 BypassIFCRotSen : Bool RETAIN`) that the grading
already flagged as a second, partially-repeated instance of the same omission.

### Verdict — **CAUGHT**

Upgrade in *kind* from round 1: no longer conditional on an artifact class that does not exist.
Now conditional on instance scoping (RW-2) and on the belt/braces coupling above.

---

## Failure 3 — REQ-017, "not faulted" narrowed to `TomraComFlt` (DEFECT)

**Ground truth.** Answer key gates on `NOT TomraControlInst1.Outputs.FaultActive` (aggregated);
generated gated on raw `NOT DiscreteInputs.TomraComFlt`. `ir/PlantAutoControl-bench/TomraControlSystem.ir:63`
declares `FaultActive : Bool` as an FB `Outputs` member and the block OR-aggregates many sources into
it. Register form: **underspecified** ("not faulted").

### Rung C §2 — the candidate set (R1 applied) — this is what flips the verdict

> 2. **Bind IO — with a candidate set.** For each requirement needing a signal, list **every** signal
>    in scope whose name, type or role could plausibly satisfy the phrase — the IO table **and** the
>    exposed status members of the block class the instance will use (read-only, for enumeration; you
>    still may not write an interface member into a requirement line). If the candidate set has **more
>    than one member**, the binding is a **BLOCKING `Q-nn`** unless a documentary basis is cited for
>    the choice.

Round 1's W6 (the rung-boundary inversion that made the *wrong* binding the *compliant* one) is
**closed**: C may now see `Outputs.FaultActive`, so the candidate set is `{DiscreteInputs.TomraComFlt,
TomraControlInst1.Outputs.FaultActive}` — size 2 — and the binding is a blocking `Q-nn`.

### Rung D1 — the sanctioned back-edge, and it is independent of C's judgement

> **Binding audit (mandatory).** For every rung-C binding, check it against the FB interface you have
> just read. If the FB exposes a signal that satisfies the relation **more broadly or more strongly**
> than C's binding (an aggregated `FaultActive` vs a raw comms fault; a confirmed-running member vs a
> ready member), you may **neither** silently keep C's binding **nor** silently re-bind: record a
> `rebind` ledger entry naming both candidates … and prefer the **stronger/broader guard**.

This clause names the exact pair from the real defect, and it fires from an activity D1 must perform
anyway (*"Read the full IR of any block you propose to reuse"*). Combined with R8's calibration fix —
*"where forced to choose on **any protective term — interlock, permissive, inhibit or fault gate** —
prefer the **stronger/fail-safe** guard"* — round 1's W8-adjacent scope-word problem is closed too.

**Two mechanisms, and the second does not depend on the first.** That is the only failure of the four
with genuine defence-in-depth.

### Residual survival path — the "documentary basis" hatch has no discrimination requirement

C §2's blocking `Q-nn` is waived *"unless a documentary basis is cited for the choice."* The clause
does not require the cited basis to **exclude the other candidates** — only to exist. A coder can
honestly cite "the IO table lists `TomraComFlt` as the sorter's fault input to this PLC" — a real
document, correctly cited, that establishes the tag's existence and says nothing whatever about why
the aggregated FB output is the wrong choice. Same defect class as the `verified-cross-block` citation
loophole: **the rule audits the presence of a citation, not its probative force.** Backstopped here by
D1's audit; *not* backstopped in failure 4.

### Verdict — **CAUGHT** (upgraded from SURVIVES)

---

## Failure 4 — REQ-019, dust-filter `RemoteOp`/`RunningFB` pairing swapped (DEFECT)

**Ground truth.** Answer key maps `FilterUnit1Op → IO.RunningFB` and `FilterUnit1Ready → RemoteOp`;
generated mapped them the other way. Grep-confirmed this session:

```
ir/PlantAutoControl-bench/Input.ir:78:    FilterUnit1Flt : Bool
ir/PlantAutoControl-bench/Input.ir:79:    FilterUnit1Op : Bool
ir/PlantAutoControl-bench/Input.ir:80:    FilterUnit1Ready : Bool
```

Consequence (grading §2 REQ-019): `RunningFB` seeds the fan up-to-speed enable —
`ir/PlantAutoControl-bench/FilterUnitSystem.ir:145: TON(EnableUpstreamTimer, IN := (NOT IO.InHand OR NOT
IO.HandIntervention) AND IO.Run AND IO.RunningFB, PT := EnableUPSTimeMS)` — so the swap starts the
10 s timer from *Ready*, before the fan physically runs.

### The one clause that acts

> **Two requirements competing for two same-shaped signals is always a candidate-set of two, never a
> coin flip.**

This is the only sentence in the four rungs that reaches this failure, and for this exact
configuration it is non-discretionary: two class requirements ("running confirmed from field
feedback", "remote-operational confirmed from field feedback"), two same-typed instance-scoped Bool
DIs → candidate set of two → BLOCKING `Q-nn`. Correctly, the disambiguating evidence does **not**
exist in scope (`FilterUnitSystem.ir` tells you what the FB does with `RunningFB` — L133/L145/L149 timers —
and with `RemoteOp` — L93 `TryRunMotor` gate, L166 alarm — but nothing in scope tells you whether the
*field tag* `FilterUnit1Op` means "operating" or "operational"), so the `Q-nn` correctly reaches the
engineer rather than being waived by a documentary basis.

### D1's binding audit does NOT reach this failure

Direct answer to the stress-test question. The audit's trigger is *"the FB exposes a signal that
satisfies the relation **more broadly or more strongly** than C's binding."* Here both bindings drive
FB **inputs** from IO tags; there is no broader/narrower relation, only a transposition. The
undriven-input audit does not fire either — `.RemoteOp` and `.RunningFB` are **both driven**, just
crossed. D2's ledger is a coverage check and a swapped bijection has perfect coverage (round 1's
analysis is unchanged and still correct).

**So failure 4 has exactly one guard, at rung C, with zero downstream backstop.** The binding audit
reaches failure 3 only.

### Residual survival path — the clause is still self-triggered

"Two requirements competing for two same-shaped signals" requires the analyst to **perceive the
competition first**. An analyst who reads `Op`→running and `Ready`→remote as each self-evident
produces two honest candidate sets of size one and judges the competing-pair sentence inapplicable
("these aren't competing — each has an obvious match"). That is precisely the reasoning that produced
the real defect. **Nothing mechanical detects that N same-typed signals from one instance-scoped name
family were assigned 1:1 by name resemblance.** The sentence narrows the discretion; it does not
remove it.

### Verdict — **CAUGHT (single-thread)** — upgraded from SURVIVES, and the weakest of the four.

---

## Adversarial stress-test answers

**1. The candidate-set rule — who decides "plausibly"? Is there a mechanical floor?**
No mechanical floor exists. "Plausibly" and "in scope" are both self-judged, no tool computes a
candidate set (`converter` has `tagstatus` for *existence*, nothing for *candidacy*), and the artifact
records only the set the analyst chose to write. **A size-1 candidate set can be produced honestly and
is unfalsifiable from the artifact.** The one place discretion is removed is the competing-pair
sentence, and only after the competition is admitted. Two escapes compound this: the
"unless a documentary basis is cited" hatch has no discrimination requirement (above), and the FB-side
half of the enumeration depends on C having correctly presumed rung D's block choice (NW-4).

**2. Does the D1 binding audit reach failures 3 and 4?** Failure 3 yes, failure 4 **no** (worked
above). Its trigger vocabulary — "more broadly or more strongly" — is a *lattice* test; a transposition
is not a lattice relation. **Is the interface-member boundary now incoherent?** Not literally
incoherent — "read for enumeration, never write into a requirement line" is a stateable rule — but it
is no longer crisp, and it is now *circular*: see NW-4.

**3. Can `verified-cross-block` be claimed with a citation that doesn't prove the precondition?**
Yes — demonstrated above with `ir/PlantAutoControl-bench/Control.ir:14`, a real block/file/line whose
content is a bare `FansShutdownReady : Bool` declaration. The rule specifies citation *form*, never
that the cited line must be a **writer** or must **entail** the claim. Nothing in D2 stops it;
only `review-functional`'s R7 re-derivation does, unassisted.

**4. Does the C §8 sweep have a non-name-based backstop?** Partially, and worth crediting: the
*enumeration* duty is non-name-based — every scoped signal is either bound or listed `UNCLAIMED` with
a reason. Only the **escalation to BLOCKING** is name-pattern-keyed. So a control-relevant signal
matching no pattern is *visible* but *not blocking*, and its "reason" is self-authored and read by
nobody with a mandate. Two gaps: no non-name escalation trigger, and no project-level residual pass to
catch signals scoped to no instance (RW-2).

**5. Does `deltas: none-found — searched: <sources>` force discovery?** Mostly it forces a sentence.
Nothing requires the named sources to be *capable* of containing the delta class in question, nothing
checks they were opened, and no downstream clause consumes the `searched:` list as an input. Its real
value is the second half of the rule — the explicit admission that rung A **cannot** see operator/bypass
signals plus the named forward-pointer to C §8. The forwarding does work; the source list is close to
decorative.

**6. New seams — are R6's derived views lossy?** Yes, both, and this is where the amendments created
new silent-ship paths: NW-2 (C → `requirements.md`) and NW-3 (D → `architecture.md` → `gen-block-new`).
Detailed below.

**7. Does the design fix the autopsy's cause 1 ("the reviewer is not independent of the spec")?**
**Partially, and by substituting a different mechanism for the one the autopsy recommended.** Where a
choice is *detected*, it now escalates to a genuinely independent oracle — the engineer, via a blocking
`Q-nn`. That is stronger independence than any AI cross-check. But **detection remains single-reader
and self-judged at every seam**: A decides whether a source separator is ambiguous, C decides what is
"plausible" and "in scope", D classifies its own preconditions and cites its own evidence, and C writes
the register the reviewer will trace. R7 adds an assigned reader for the ledger — real progress — but
that reader reads C's derived register, so anything lost in C's derivation is invisible to both
(NW-2), and reading the ledger anchors it on the author's own argument (NW-5). Meanwhile **FI-32 — the
autopsy's own ranked-#1 fix, the per-instance interlock-completeness set-difference — is still
`Open`/unbuilt** (`docs/16-future-ideas.md` L520-526: *"the highest-leverage autopsy fix; the one thing
that would have caught the REGRESSION deterministically"*) and none of the four rungs implements it.
The rungs fixed the spec surface (cause 3) and the coder's disposition (causes 2, 4). Cause 1 is
improved from "no independence" to "escalation on detected ambiguity" — which is exactly as good as
detection, and detection is still interpretation.

---

## Round-1 findings — closed, papered over, or open

| Round 1 | Status now | Evidence |
|---|---|---|
| W1 `deltas: none` no discovery mechanism | **Papered over → forwarded** | The `searched:` list is near-cosmetic, but A now explicitly hands the duty to C §8 and says why. Real effect comes from C §8, not from W1's own fix. |
| W2 C binds requirements→signals only | **CLOSED** | C §8 is a real Pass-2 in the opposite direction; enumeration duty is non-name-based. Weakened by scoping (RW-2). |
| W3 "completeness by construction" rests on nonexistent `references/` | **OPEN, untouched** | `find -iname reference.md` → nothing; no `references/` dir. Format, author, and completeness of a reference are still undefined and unchecked. **The pipeline still cannot run at all** (A's stop condition), and the pressure to improvise a thin reference is unchanged. Mitigated only in that failure 2 no longer depends on it. |
| W4 declared-but-wrong discharge passes D2 | **Half closed** | The *unverifiable-argument* half is closed (`unverifiable` ⇒ render). The *non-probative-citation* half is open (RW-1) and the "same agent writes and argues the drop" half is addressed only by R7's reader. |
| W5 nothing detects underspecification | **Half closed** | C §2 computes a candidate set — the mechanical signature round 1 asked for — but computing it is still a self-judged act with no floor and no tool (RW-3). |
| W6 rung-boundary inversion (C can't see FB members) | **CLOSED, at a cost** | C may now enumerate FB status members. Cost: C must presume D's block choice (NW-4). |
| W7 no one reads the ledger | **CLOSED on paper** | `review-functional` Inputs now consumes it *"consumed, never trusted"*. `audit-artifact` is still "Not built" (docs/15 L204). New problem: NW-5. |
| W8 rungs not wired into the pipeline | **Half closed** | The two hard STOPs are fixed *inside the skills* (C emits `requirements.md`, D emits `architecture.md`). **docs/15 is still stale**: L56 still lists `gen-pid-analysis` producing `process-topology.md`, and B/C/D appear nowhere in its 15-skill table (L204 still "Not built"). `grep -rl` for the four artifact names outside the SKILL.md files returns only round-1's own validation doc. |
| W9 `/` rule is output-only | **CLOSED for rung A, OPEN for rung B** | NW-1. |
| W10 C §5 same-fact merge unguarded | **CLOSED** | R5 applied verbatim, naming the exact case. |

---

## Remaining weaknesses (RW) and new weaknesses introduced by the amendments (NW)

**NW-1 — R4 was installed on rung A only; rung B reads the prose and has no input-side rule.**
The supplied functional description is the document class most likely to carry an ambiguous
conjunction, and rung B's only nearby clause (§6) fires on *contradictions between readings*, not on
one ambiguous sentence. Complete survival path for failure 1, documented above. *(Highest-value single
edit remaining — see R9.)*

**NW-2 — C's derived `requirements.md` has no completeness check, and its loss inverts the reviewer.**
D2 is explicit: *"The relation-id column must set-difference to **empty** against the union of all
`C-nn`/`P-nn` ids in `gen/<project>/equipment-specs/`. State both counts explicitly."* The C →
`requirements.md` derivation has **no equivalent rule** — no set-difference, no counts, no
id-preservation check. Consequences, in order of nastiness:
1. `review-functional`'s REQUIRED input is the register, and *"the register's numbering is the report's
   spine"*. A relation dropped in the derivation is never traced by the pipeline's only independent
   functional check.
2. D2 still forces the term to be rendered (D reads `equipment-specs/`, not the register), so the IR is
   correct — and then **Pass 2's reverse trace sees a term with no REQ and reports it as unrequested
   logic / gold-plating (C-606)**. A lossy register derivation does not merely hide an interlock; it
   can make the reviewer recommend **deleting a correctly-rendered one**. That is a strictly worse
   failure mode than the one round 1 measured.

**NW-3 — rung D's boolean render never reaches the coder.** `gen-block-new`'s Inputs list
`architecture.md` (REQUIRED), `patterns/`, the `ir/` export, doc 06, and the compile playbook —
**`code-structure.md` and `equipment-specs/` are not inputs at all.** D's own instruction is that
`architecture.md` be *"the block manifest in `gen-architecture`'s own output shape (manifest,
interfaces, DB landscape, OB1 order, pattern/tier mapping, REQ→block trace, cross-instance wiring, tag
status)"* — a shape with **no slot for the D3 per-instance render or its relation-id term tags**. So
the artifact that carries "every term traces to a relation" is lossy by construction on the last hop,
and the coder is back to composing from the manifest plus patterns — re-opening autopsy **cause 2**
("fit to the nearest templated shape and shed the residue") at the exact stage where it happened.

**NW-4 — C must now presume rung D's block choice (circularity introduced by R1).** C §2 requires
enumerating *"the exposed status members of **the block class the instance will use**"*, but choosing
that block is D1's job (*"For each equipment class, find the library block that covers its requirement
set"*) and A §1 is explicit that *"Grouping instances back into FB types is rung D's job, deliberately
separate."* Three effects: (a) if C presumes the wrong block or none, the FB half of every candidate
set silently vanishes and failure 3's C-side catch evaporates; (b) D1's reuse-first fit degrades toward
rubber-stamping C's presumption; (c) the interface-member ban is now "read yes, write-into-a-
requirement-line no" — but a blocking `Q-nn` naming both candidates *must* name the interface member,
so the spec artifact now contains interface members after all. Defensible, not crisp.

**NW-5 — R7 makes `review-functional` non-blind by its own definition, and anchors it.**
`review-functional`'s Blindness section: *"If you wrote or wired the code under review earlier in this
same session, **or the conversation contains the author's design rationale**, say so at the top: your
review is then 'informed, not blind,' which the project treats as weaker evidence."* The discharge
ledger **is** the author's design rationale, now a mandated input. The Blindness section was amended to
carve out doc-06 rationales and register notes as *"One expected, non-contaminating caveat"* — and was
**not** extended to the ledger. Worse, the ledger's `relation-id | disposition | evidence` table is
functionally the REQ→anchor mapping that the FI-25 assist explicitly forbids importing: *"**your**
evidence-carrying mapping, NOT `architecture.md`'s REQ→block table, or the review stops being blind."*
Net effect: the reviewer reads the coder's argument for the drop *before* forming its own view, in a
skill whose stated purpose is to break exactly that correlation.

**RW-1 — the citation loophole (`verified-cross-block`).** Form is specified; probative value is not.
Worked example above with a DB declaration line. Applies equally to C §2's "documentary basis" waiver:
both rules audit that a citation exists, not that it discriminates.

**RW-2 — sweep scoping has no project-level residual.** C §8 is per-instance; nothing requires the
union of the per-instance sweeps to cover the IO table. Plant-level operator DBs (`HMIControlSignals`,
NUMBER 38 — home of both `Bypass*` tags) are exactly what falls between instances, and the escalation
in D1's undriven-input audit is *conditioned* on the sweep having listed the signal, so belt and braces
fail together.

**RW-3 — no mechanical floor anywhere in the amended design.** Every new gate is a judgement an agent
performs on itself and records in its own artifact: candidate-set size, "plausibly", "in scope",
"same-shaped", "more broadly or more strongly", precondition class, "with a reason". Not one of them is
computed by a tool, and `converter` gained nothing in this amendment round. The autopsy's conclusion —
*"the durable fixes are **mechanical completeness-tracing** (independent of interpretation) … not 'a
better AI read of the same spec'"* — is not yet honoured; FI-32 remains unbuilt.

**RW-4 (carried, unchanged) — W3: `references/` does not exist.** Still the largest unverified
assumption in the design, and still a hard blocker on running the pipeline at all.

**RW-5 (carried, partially) — W8: docs/15 is stale.** The skills self-repaired their handoffs; the
pipeline document did not. docs/15 L56 still names `process-topology.md`; B, C and D are absent from
its skill table and build order.

### What the amendments got right (recorded for balance)

- **C §8** converts the delta problem from "depends on an artifact class that does not exist" to a
  reference-free sweep. Biggest real gain in the set.
- **C §2's competing-pair sentence** is the only clause in the pipeline that removes discretion on an
  assignment ambiguity, and it is aimed precisely at the documented case.
- **D1's binding audit** is genuinely independent of rung C's judgement, and it is triggered by work D
  must do anyway — the cheapest kind of guard.
- **D2's `unverifiable` ⇒ render** kills the plausibility-as-verification argument on the honest path,
  and the failure-1 case is verifiably `unverifiable` (no writer for `FansShutdownReady` in scope).
- **R7 gives the ledger a reader at all**, which is a structural change, not a wording one.

---

## Recommended amendments (quotable; not applied)

### R9 (highest leverage) — port the input-side explicitness rule into rung B. `gen-functional-analysis`, Method.
Closes NW-1, the only complete silent-ship path left inside the rungs.

> 2b. **One behaviour per line — on the way in as well as the way out.** A source sentence joining two
>     conditions with an ambiguous separator (`/`, "and/or", a comma, a parenthetical restatement) is
>     **split into two behaviours, both retained** (the default), or raised as a blocking `Q-nn`.
>     **Dropping either side always requires the `Q-nn`** — an appositive reading ("A, i.e. B") is a
>     resolution and is never taken silently. This is the same rule as `gen-pid-analysis`'s
>     explicitness block, applied to prose: the supplied functional description is the document class
>     most likely to carry it. *A `/` joining two hold-conditions, read as one, is the documented cause
>     of a real dropped-interlock regression (`docs/evidence/PlantAutoControl-bench-autopsy.md`).*

### R10 — make citations probative, not merely present. `gen-code-structure` §D2 and `gen-equipment-spec` §2.
Closes RW-1.

Append to D2's precondition-class list:

> **`verified-cross-block` requires citing the network that WRITES the member relied upon, and quoting
> its full guard.** A declaration line, a DB member list, or any citation that establishes only that
> the member *exists* is **not** a verification — classify it `unverifiable`. If no writer for the
> member exists anywhere in `ir/<project>/`, the precondition is `unverifiable` **by definition**, no
> argument admitted. *`PlantControl.FansShutdownReady` has exactly one occurrence in the corpus —
> `ir/PlantAutoControl-bench/Control.ir:14`, a bare declaration — and citing it "proves" nothing about what
> the flag aggregates.*

Append to C §2's waiver:

> A **documentary basis** must state why **each other candidate is excluded**, not merely why the
> chosen one qualifies. A citation that establishes a signal's existence is not a basis for preferring
> it; absent an exclusion argument the blocking `Q-nn` stands.

### R11 — give the derived register the same set-difference discipline as the ledger. `gen-equipment-spec`, output section.
Closes NW-2, including the gold-plating inversion.

> The derived register is **complete by set-difference, and it says so**: the REQ set must
> set-difference to **empty** against the union of all `C-nn`/`P-nn` ids in
> `gen/<project>/equipment-specs/`, and both counts are stated explicitly in the register's provenance
> header. *A relation that exists in the specs but not in the register is still rendered by rung D
> (which reads the specs) — so the loss does not break the code, it breaks the review: the reviewer
> traces a register that never mentions the relation, and its reverse pass then reports the correctly
> rendered term as unrequested logic (C-606). A lossy register can recommend deleting a real
> interlock.*

### R12 — carry the D3 render to the coder. `gen-code-structure` output section, and `gen-block-new` Inputs.
Closes NW-3.

To `gen-code-structure`:

> Each manifest item in the emitted `architecture.md` **embeds its D3 render verbatim**, with the
> relation-id tag on every term, and cites its D2 ledger rows. The manifest is the coder's only
> required input; a render that lives only in `code-structure.md` does not reach the coder.

To `gen-block-new` Inputs:

> - **`gen/<project>/code-structure.md` §D3 — REQUIRED where the project was specified through the
>   A–D rungs.** The per-instance render is the term-level contract: every term carries the relation id
>   it satisfies, and you implement **all** of them. Fitting the render to the nearest pattern shape and
>   shedding the residue is the documented generation failure
>   (`docs/evidence/PlantAutoControl-bench-autopsy.md` cause 2) — a term you cannot place in the pattern is a
>   stop-and-report, never a drop.

### R13 — close the sweep's scoping gap. `gen-equipment-spec` §8.
Closes RW-2 and decouples D1's hard fail from the sweep.

> **Project-level residual pass.** After the per-instance sweeps, take the union of every signal
> claimed by any instance and set-difference it against the whole IO table. The residue — signals
> scoped to **no** instance — is listed in `gen/<project>/equipment-specs/UNSCOPED.md` with a reason
> each; a plant-level operator/HMI DB is the usual home of a scoped-by-nobody control signal and is
> never assumed out of scope by virtue of living there. **Escalate on role, not only on name:** an
> unclaimed signal that is `RETAIN`/HMI-writable, or that name- or role-matches an **undriven input**
> of the instance's presumed block, is a BLOCKING `Q-nn` whatever its name pattern.

And in `gen-code-structure` D1, decouple the hard fail:

> An undriven input that name- or role-matches **any** IO signal in the project — whether or not rung C
> listed it as unclaimed — is a **HARD FAIL**.

### R14 — restore blindness around the ledger. `review-functional`, Blindness + Inputs.
Closes NW-5.

> **Order of work is load-bearing:** derive your own verdict and evidence for a relation **before**
> opening the ledger row that dispositions it, then check the row against what you already found. The
> ledger is the author's design rationale; reading it first is the correlated-check failure this
> review exists to break. Reading it in this order is the **one** admitted non-contaminating use —
> declare it in your blindness note; reading it first makes the review "informed, not blind."

### R15 — name the presumed block, and make D confirm it. `gen-equipment-spec` §2 / `gen-code-structure` D1.
Closes NW-4.

> The block class used for candidate enumeration is a **presumption recorded in the spec header**
> (`presumed-block: <FB> — provisional, confirmed at D1`), never a decision. D1 states explicitly
> whether it confirms or overrides each presumption; an override **re-opens every candidate set** that
> was enumerated against the superseded interface.

### R16 — build the mechanical floor (FI-32 + a candidate-set tool).
Closes RW-3 — the one weakness that no wording change can close.

FI-32 (per-instance interlock-completeness set-difference) is still `Open` and is the autopsy's own
ranked-#1 fix. Add to it a cheap sibling: `converter candidate-scan --project <ir-dir> --instance
<prefix> --phrase <keywords> [--json]` returning every same-typed signal in the instance's scoped name
family plus the presumed FB's status members — i.e. the candidate set computed, not asserted. That
turns C §2 from a self-report into a check, and it is the only proposal here that survives an agent
choosing not to look.

### R17 — housekeeping carried from round 1: create `references/` (W3/RW-4) and update docs/15's skill table (W8/RW-5).
Both remain untouched. The pipeline cannot legally run until a class reference exists, and docs/15
still documents a `process-topology.md` that no skill produces.

---

## Bottom line

The amendments did real work: **both round-1 SURVIVES are closed**, the delta catch no longer depends
on an artifact class that does not exist, the plausibility-as-verification argument is dead on the
honest path, and the ledger finally has a reader. Round 2 finds no failure that ships silently through
rungs A–D as written.

But the design's guarantee is now precisely: *"an ambiguity that a single reader notices is escalated
to the engineer."* Every new gate is self-triggered, self-assessed and self-recorded, and two of them
audit the **presence** of a citation rather than its **force**. Meanwhile the amendments added three
new seams — rung B unprotected (NW-1), a register derivation with no completeness check that can make
the reviewer recommend deleting a real interlock (NW-2), and a boolean render that never reaches the
coder (NW-3) — and made the pipeline's only independent reviewer non-blind by its own definition
(NW-5).

The autopsy's cause 1 is improved but not closed, and its own ranked-#1 remedy — a mechanical,
interpretation-independent completeness trace (FI-32) — is still unbuilt. Until something in this
pipeline is computed rather than asserted, its floor is the attentiveness of one reader per seam.
R9, R11, R12 and R16 are the four that matter.
