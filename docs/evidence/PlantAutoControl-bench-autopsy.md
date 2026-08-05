# Autopsy — why the interlock gaps were missed (S6-Killer-Plan)

The Grader (with the sealed answer key) found a REGRESSION and several DEFECTs on the freeform
networks. The pipeline's own functional review, one step earlier, had declared the block
"implements the spec — 18/22, 0 contradicted, 0 unimplemented." **How did a dropped interlock pass a
machine-by-machine functional review?** This is the autopsy. It matters more than the block: it is a
finding about the *pipeline*, not this one job.

## 1. Were the gaps in the requirement the coder was given?

Traced each back to `gen/PlantAutoControl-bench/requirements.md` (the register the coder had — it never
saw the answer key):

| Net(s) | Gap | In the register? | Form |
|---|---|---|---|
| 3, 4, 19 | Filter cascade **neighbour-`ShutdownComplete` hold dropped** (REGRESSION) | **YES** — the equipment-inventory "Holds through shutdown until" column said **"Fans-shutdown-ready / Air-separator VSD shut down"** (two conditions) | **Ambiguous** — the `/` reads as either "A or B" or "A, i.e. B" |
| 6 | Discharge-VSD rotation-sensor **bypass clause dropped** (DEFECT) | **YES** — REQ-012 states it explicitly: "unless that sensor is bypassed, in which case it is not used" | Explicit |
| 17 | Feed-conveyor **reverse latch** de-latched to combinational | **YES** — REQ-016 text ("direction only changes while running & not in hand") + the C-113 table classes it *latched* | Explicit |
| 17 | Feed-conveyor **reverse** motion-confirm bypass dropped (partial DEFECT) | **IMPLIED** — REQ-011 states belt bypass; doesn't spell out "and the reverse direction" | Implied |
| 10 | Sorter "not faulted" interlock **narrowed** (`FaultActive`→`ComFlt`) (DEFECT) | **UNDERSPECIFIED** — REQ-017 says "not faulted" but never pins the signal | Underspecified |
| 3, 4 | Dust-filter `RemoteOp`/`RunningFB` pairing **swapped** | **UNDERSPECIFIED** — REQ-019 names "remote-operational, running, fault" without pinning the pairing | Underspecified |

**So: every gap was traceable to the register.** None was invented reality. But the *form* varied —
explicit, ambiguous, implied, underspecified — and that is what determined why each slipped through.

## 2. Why didn't the reviewer catch them? Three distinct failure modes.

### A. Caught but not blocking — REQ-012, REQ-016
The review **did** flag these (both marked *partial*): "the 'unless bypassed' clause is not
implemented" (REQ-012) and "the register's latched-state classification is not realized in-block"
(REQ-016). The review worked. They shipped anyway because a functional **"partial" was treated as a
presentable footnote, not a blocking fail** — and the run-through gate (orchestrator-approved,
owner's choice) accepted the block with them open. **Process failure, not a detection failure.**

### B. Correlated misreading — REQ-003 (the REGRESSION, the worst one)
The register said "Fans-shutdown-ready **/** Air-separator VSD shut down." The coder implemented only
the first term. The reviewer then **checked the block against the register and declared "every one
matches — N3/N4/N19→FansShutdownReady"** — i.e. it read the `/` the same way the coder did, collapsed
the two conditions to one, and verified the block against *its own* reading. **The reviewer and the
coder shared the same spec and resolved its ambiguity identically, so the check confirmed the error
instead of catching it.** This is the core finding: *an AI reviewer reading the same register as the
AI coder is a correlated check, not an independent one.* It can catch "unimplemented," but not
"implemented against a wrong reading both of them share." Only the Grader — holding the **answer key**
— broke the correlation and saw the real block held on *both* terms.

### C. Underspecification absorbed as inference — REQ-011, REQ-017, REQ-019
The register left the detail to inference (which fault signal; which feedback pairing; the reverse
direction). The coder inferred toward the **simpler / weaker** option (the narrow `ComFlt`; one hold
term; the swapped pairing; forward-only bypass). The reviewer, reading the same underspecified text,
had **no basis to distinguish the coder's inference from the correct one** — "not faulted" is
satisfied by `ComFlt`; the pairing is plausible either way — so it passed them as *implemented*. Same
correlation as B, but sourced in silence rather than ambiguity.

## 3. Root causes (ranked by leverage)

1. **The reviewer is not independent of the spec.** B and C are the same disease: coder and reviewer
   both derive truth from the register, so any ambiguity/silence/looseness in the register produces a
   **correlated failure** — the coder mis-implements, the reviewer mis-confirms. The functional
   review's real guarantee is "the block matches the register *as the reviewer reads it*," which is
   weaker than it looks whenever the coder reads it the same way. This is why the whole answer-key
   validation was worth doing: ground truth is the only thing that broke the correlation.
2. **Generation under-constrains outside pattern coverage.** Every gap is a condition the
   `chained-permissive-enable` pattern did not template (compound cascade hold, bypass-on-VSD,
   fwd/rev latch). The coder faithfully reproduced the pattern's *documented* variations and, on the
   machines needing an *undocumented* compound guard, fit them to the nearest templated shape and
   **shed the residue**. It errs toward dropping a guard, never toward adding a wrong one — a
   consistent, characterizable bias.
3. **The register's notation permitted ambiguity.** A `/` in the interlock table carried a
   two-condition requirement in a form both downstream stages collapsed.
4. **A dropped stated interlock was a footnote, not a fail.** Under the stricter generated-code bar,
   an unimplemented guard is a functional defect; it was presented as an acceptable "partial."

## 4. How to stop it (mapped to the causes)

**Break the coder/reviewer correlation (cause 1 — highest leverage):**
- Add a **per-instance interlock-completeness trace** to `review-functional`: for each machine, take
  the register's *explicit* condition list (the inventory "starts-after" / "holds-until" columns) and
  verify **every listed condition appears in the block's guard** — a mechanical set-difference, not a
  re-interpretation. A missing condition is a finding regardless of how the reviewer would "read" the
  requirement. Reuse the existing substrate: FI-22 `cross-check` (reader/writer graph) + FI-25
  `trace` (forward REQ→IR binding) already build the per-machine writer facts; drive them off the
  register's interlock table. This is the fix that would have caught REQ-003 deterministically.
- Add a **"strongest-available-guard" check**: when a REQ says "not faulted"/"not running" and the
  wired FB exposes both a narrow and a broad signal, flag use of the narrower one (REQ-017).

**Fix the spec surface (causes 3, and the C-class underspecification):**
- `requirements.md` format rule: **no ambiguous conjunctions** in the interlock table — one explicit
  condition per cell, or spelled-out AND/OR. (A `gen-spec-analysis` discipline.) This removes the
  ambiguity B fed on.
- The Examiner must mark **underspecified interlock details as explicit blocking `Q-nn`** (which fault
  signal? which feedback pairing?), the same way a `proposed` tag stops the run — so the coder cannot
  silently pick the weaker reading (REQ-011/017/019).

**Constrain generation (cause 2):**
- `gen-block-new` discipline: **trace each REQ's full condition set into the network**, don't
  fit-to-pattern-and-drop; on any machine whose required guard exceeds the pattern's template, treat
  the extra condition as mandatory, not optional.
- Grow the pattern library (FI-06 territory) to document the compound-interlock variations
  (compound cascade hold, bypass-on-VSD, fwd/rev latch) — turning today's under-constrained freeform
  into pattern-covered logic.

**Process (cause 4):**
- Under the generated-code bar, a functional **"partial" that drops a stated interlock is a blocking
  fail**, requiring implementation or an explicitly recorded owner waiver *before* presentation — not
  a footnote in the bundle.

## 5. The one-sentence lesson

The functional reviewer, reading the same register as the coder, can only catch what the register
made *unambiguous*; every gap here lived in the register's **ambiguity, silence, or looseness**, where
coder and reviewer failed together — so the durable fixes are **mechanical completeness-tracing**
(independent of interpretation) and **removing the ambiguity at the spec surface**, not "a better AI
read of the same spec." Ground truth (the answer key) is what exposed it, and where a real job has no
answer key, the completeness trace is its stand-in.
