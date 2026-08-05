# Simplicity review — gen/PlantAutoControl-bench/PlantAutoControl.ir (2026-07-20)

Blindness: **blind** — fresh context, no author rationale in this session. I did read
`docs/06-lad-conventions.md` (required) and the `chained-permissive-enable` pattern (`pattern.md`
+ its `network-2`/`network-8` examples) to separate *pattern-derived shape* from *novel defect*,
per the skill's pattern-tension calibration; every finding below is re-derived from the target IR
itself. `docs/evidence/` was **not** read (blindness mandate honoured).

Blocks reviewed:
- `PlantAutoControl` (FC, 20 networks) — **generated** (header: `Author: lad-coder
  (gen-block-new). Rev 1`). Stricter generated-code bar applied. No `SIDECAR` line present — the
  whole file is readable content.

Regime note: reviewed as `generated`, so "the real site block / the admitted pattern does the
same" is explicitly **not** a pass — but where a readability cost is baked into an *admitted*
pattern, I raise it as a **pattern-vs-rule tension for owner ruling** rather than a unilateral
defect demand, per the skill calibration.

---

## Findings (most-severe first)

### PlantAutoControl (generated)

**F1 — [C-101 / C-602 / one-reading test, warn — PATTERN TENSION] every `Shutdown` coil (all 20
networks): mechanically-negated readiness with `PlantControl.Status = -1` appearing in two roles.**
- Evidence (network 3, representative):
  `COIL FilterUnitInst2.IO.Shutdown := NOT (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT PlantControl.FansShutdownReady) AND NOT FilterUnitInst2.IO.HandIntervention AND PlantControl.Status = -1`
- Why it fails one reading: the expression is the literal boolean negation of the `AutoStartSignal`
  readiness group **plus** a second, trailing `AND PlantControl.Status = -1`, so `Status = -1`
  appears once *inside* the negation and once *outside* it. Its actual meaning reduces to
  "`Status = -1` AND `FansShutdownReady` AND NOT hand" — but a reader cannot see that in one pass,
  and cannot tell at a glance whether the trailing `Status = -1` is redundant (it is **not** — it
  is load-bearing; without it the coil would also assert at idle `Status = 0`). Correct-but-harder-
  to-read-than-needed, which the doc-06 priority-order preamble explicitly licenses flagging.
- Pattern status: this is the pattern's own fixed skeleton (`pattern.md` "The shape",
  `Shutdown := NOT (<own-readiness>) AND NOT <Inst>.IO.HandIntervention AND PlantControl.Status = -1`),
  so it is **pattern-derived, not a one-off generation slip**. Raised as a tension: the admitted
  pattern bakes in a form that fails the stricter generated one-reading bar.
- Suggested fix (owner ruling): let the generator name the readiness once per network to a block
  TEMP bool (e.g. `RunPermissive`) and write `Shutdown := Status = -1 AND NOT RunPermissive AND
  NOT HandIntervention` — or amend the pattern to state the double-`Status` idiom in a network
  comment so the reader is told the shape is deliberate.

**F2 — [C-604-spirit / C-605 "all generated code commented", warn — PATTERN TENSION] bare
`SystemHealthy := TRUE` constant into a block input on 17 networks, with no stated reason
anywhere.**
- Evidence: `COIL <Inst>.IO.SystemHealthy := TRUE` (networks 1,2,3,4,5,6,7,8,10,12,13,14,15,16,
  17,19,20). The block header comment does not mention it; no per-network comment names why it is
  constant.
- Why it fails the bar: C-604's principle — a deliberate constant into a block input must come from
  a named source or carry a comment saying *why* it is constant — so a skeptical reader can tell
  "these units genuinely expose no health feedback (design)" from "stub to be wired later (debt)."
  17 identical bare `TRUE`s give the reader no way to make that call.
- Pattern status: `pattern.md` documents `SystemHealthy := TRUE` as "genuinely fixed (unless this
  equipment has a real health check)" — again pattern-sanctioned, so raised as tension, not a
  demand. Under the strict generated bar the sanction alone is not enough; the *reason* is what is
  missing.
- Suggested fix: one header-comment line ("`SystemHealthy` hardwired TRUE — these units expose no
  external health feedback; wired from the FB's internal checks only"), or a named constant per
  C-604's permanent-fact form.

**F3 — [C-601 / C-602, warn] networks 5 and 17: the multi-term readiness group is duplicated
inline across the two OR-branches of one `AutoStartSignal` coil.**
- Evidence (network 5):
  `AutoStartSignal := (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT MotorVSDInst2.IO.ShutdownComplete) AND MotorVSDInst1.IO.UPSEnable AND FilterUnitInst2.IO.UPSEnable AND FilterUnitInst3.IO.UPSEnable OR (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT MotorVSDInst2.IO.ShutdownComplete) AND MotorVSDInst1.IO.UPSEnable AND InterlockData.LinkOut3rdParty`
  The 3-term readiness group **and** `MotorVSDInst1.IO.UPSEnable` are written twice. Network 17's
  `AutoStartSignal` (line 228) repeats the *dual-neighbour* (longer) readiness group across both OR
  branches identically.
- Why it fails one reading: >2 effective OR-branches with a ≥3-term compound repeated verbatim
  inside them — exactly the C-601 near-match hazard (a later edit to one copy silently diverges),
  and past C-602's soft guide. Factors cleanly to `readiness AND MotorVSDInst1.UPSEnable AND
  (FilterUnit2.UPS AND FilterUnit3.UPS OR LinkOut3rdParty)`.
- Note: network 5's comment *does* explain the alternative permissive ("...OR discharge-conveyor
  VSD plus the third-party link-out signal"), which softens it — but the comment justifies the
  *logic*, not the *verbatim duplication* of the readiness compound.
- Suggested fix: factor the shared readiness (and shared `UPSEnable`) out of the OR.

**F4 — [C-601 near-match / C-607 one-policy, info] `PlantControl.Status = -1` term-ordering is
inconsistent between single-neighbour and dual-neighbour networks.**
- Evidence: single-neighbour nets (3–16,18–20) write `PlantControl.Status = -1 AND NOT
  <X>.ShutdownComplete` (Status term **leads**); dual-neighbour nets **1** and **17** write
  `(NOT <X>.ShutdownComplete OR NOT <Y>.ShutdownComplete) AND PlantControl.Status = -1` (Status
  term **trails**).
- Why it matters: in a 20-network near-repeating block, a reader diffs networks to spot the
  outlier; the same logical construct written with its terms in two different orders adds noise to
  that diff for no functional reason. Low severity — the OR-grouping in the dual case forces *some*
  reordering, but `Status = -1 AND (NOT X OR NOT Y)` would keep the term leading everywhere.

---

## Least-readable networks (the outlier hunt)

- **Network 17 "Feed Conveyor" — least readable.** Freeform fwd/rev (no admitted pattern), carries
  the dual-neighbour (longest) readiness group **and** duplicates it across both OR-branches of
  `AutoStartSignal` (F3), plus a separate `Reverse` coil and two direction outputs. Densest single
  network; needs the header's fwd/rev note to parse. Its complexity is *disclosed* (comment flags
  FREEFORM + C-116/C-117), which is the right call — but it is the network a skeptic stalls on.
- **Network 5 "Air-Separator VSD" — second.** Two-branch alternative-permissive `AutoStartSignal`
  with the readiness compound duplicated (F3).
- **Network 2 "Link Conveyor" — subtlest mechanism (not a defect).** Unique `SCOIL`/`RCOIL`
  one-shot on `PlantControl.AutoPreStart` gated by `NOT PlantControl.PositiveEdgeArray[4]`, with
  the edge-memory `COIL PositiveEdgeArray[4]` updated *after* the `SCOIL` in the same network. The
  edge is correct (the SCOIL reads the prior-scan value), but the reader must know intra-network
  execution order to see that. Well-commented; noted as most-complex-but-justified. NB: this
  generated network is a **cleaner** rewrite of the pattern's own `network-2` example — it factors
  `InterlockData.JOB9001Interlock` out to a single trailing AND rather than repeating it inside the
  OR, and omits it from `Shutdown` (correct — shutdown shouldn't wait on the third-party interlock).
  That is generation improving on the source pattern, not drift.

Positive: no kind-batching (C-126 clean) — every network keeps its feedback maps, permissives,
output coils and `CALL` together as one equipment story; the `MOVE(... => EnableUPSTime)` sits with
its own consumer (nets 3/4/19). Titles are short with detail in comments (C-203 clean). Every
network is titled and richly commented with the *why* (C-201/C-202 clean).

## Clean declarations
- `PlantAutoControl`: C-126 (no kind-batching), C-201 (all titled), C-202/C-203 (why-comments,
  short titles), C-603 (no ranged **Step** predicates — `Status >= 1` is a plant-status tri-state
  sign convention, not a stepped-sequence phase; noted, not flagged), C-608 (no comment contradicts
  its rung — checked nets 2 and 19 specifically), C-610 (network 19's unwired `RunningFB` and net 6
  bypass-deferral both carry explicit known-gap comments).

## Cross-block
- **AirStar interface vocabulary (C-115 context, not this block's defect):** network 1 reads
  `AirStarInst1.Outputs.EnableUPS` where every other network reads `<Inst>.IO.UPSEnable` (and net 6
  reads `ECSControlInst1.Outputs.UPSEnable`). The `EnableUPS` vs `UPSEnable` member-name divergence
  lives in the AirStar FB's own UDT, not in `PlantAutoControl`, but it does make network 1 read
  differently from its siblings. Belongs to the equipment-FB conventions review; flagged here as a
  readability drag this block inherits.
- Idiom table (settings/edge/time): consistent within this block — `PreStartDone` always from
  `PlantControl.PreStartComplete`; `FaultReset` always from `HMIControlSignals.SystemReset`; the
  `MOVE(EN := TRUE, IN := ProcessTimings.NormalFanStartTime)` time idiom is identical across nets
  3/4/19. No C-607 intra-block policy split.

## Not checkable here (other tiers)
- **C-003 (naming, error) — other tier:** `converter review` flags block name `PlantAutoControl`
  lacks the `FC_` prefix (should be `FC_PlantAutoControl`). Naming tier — belongs to
  `review-conventions`, recorded here only so it is not lost.
- **Network 19 functional gap — tier 1:** `FilterUnitInst1.IO.RunningFB` is unwired because its
  source tag name contains `/` (C-005-breaching DB member, un-referenceable in IR). The comment
  documents this thoroughly (readability side satisfied, C-610 met), but *whether a filter unit
  with no running feedback is acceptable* is a functional-review call, not a simplicity one.
- **S/R usage (C-403) — mechanical tier:** network 2's `SCOIL`/`RCOIL` on `PlantControl.AutoPreStart`
  and the `PositiveEdgeArray[4]` edge memory are C-403/C-402/C-404 territory (startup-reset
  coverage, dedicated edge bit) — for the conventions/mechanical review to rule on, not this one.

## Verdict

**Clears the readability bar — conditionally.** Structurally the block is exactly what the
convention wants: one coherent, titled, well-commented equipment story per network, read top-to-
bottom like a table of contents, no kind-batching, no undocumented dead signals. **No readability
defect rises to a block-level fail.**

The reservation: the block carries the `chained-permissive-enable` pattern's inherent
boolean-density cost, and under the *stricter generated bar* three of the four findings (F1 Shutdown
double-`Status` negation, F2 bare `SystemHealthy := TRUE`, F3 in-coil readiness duplication) are
real one-reading costs — two of them (F1, F2) baked into the admitted pattern itself. These are
raised as **pattern-vs-rule tensions for owner ruling**, not as demands against this block in
isolation, because fixing them properly means amending the pattern (or teaching the generator to
name the readiness bit), which affects every future 20-network generation, not just this one.
Recommended before this shape becomes the scaled template.
