# Stage S4 — Convention review — full evidence record

> Narrative and acceptance-test evidence for stage S4, split out of `docs/notes/stage-gates.md` on 2026-07-18 per `docs/16-future-ideas.md` FI-20.
> `stage-gates.md` remains the live status index; this file is the append-only
> detail record. Content below is verbatim as it stood in `stage-gates.md` at
> commit d8572c2. Append new S4 entries here, not to the index.

---

## S4 Phase 1: mechanical rule-checking built and pilot-proven (2026-07-15)

Built exactly the 8-rule Phase 1 slice scoped in the approved plan: `converter review <file>
[<file> ...] [--ignore-errors] [--json]`, five discriminating rules (C-003 naming prefix, C-005
charset, C-201 title/comment presence, C-301+C-501 absolute-addressing with all three documented
exceptions, C-406 timer-kind checked in both declaration and usage form) plus three rules built and
explicitly labeled `CheckedVacuous` rather than `Checked` (C-102/C-401/C-404 — no jump/counter/
built-in-edge construct exists anywhere in the current IR model, confirmed against
`FlgNetParser`'s own whitelist, so these can structurally never fire; reported honestly as "cannot
fire against current IR capability" rather than implying real verification happened).
`--ignore-errors` (added mid-session, on request) makes a batch review record a per-file
`FileError` and continue rather than aborting the whole run on the first unparseable file — the
abort-on-first-error default (matching `to-ir`/`to-xml`) is unchanged without it. 46 new unit tests
(true-positive + true-negative per rule, including the exact `DataHandling.ir`-shaped C-301
exception case); full converter suite green throughout (412/412).

**Live pilot** against all 14 committed `ir/reference/*.ir` files reproduced every finding expected
from planning, exactly — arithmetic double-checked against the tool's own summary line (29
findings, 14 error/15 warn): 13/14 blocks missing their naming prefix (only `DB_Timers` already
compliant); the 5 DB-kind blocks missing a header comment, plus `TimerSample` for a genuinely
different reason (a real, previously-undetected gap — S3 gave it a block-level *Title*, never a
*Comment*, and C-201 checks the latter; a distinction this project has otherwise been careful
about); `NodeStatusAlarms` (15-bit alarm word, untitled — C-301+C-501 both fire) and
`PerimeterSafetyAlarms` (8-bit alarm word, *has* title/comment from S3's own work, still fails
C-501's literal "exactly one bit" condition — expected, not a reversal of that sign-off, a
different axis than what S3 evaluated); `FBTimers`/`TimingAndCalls`'s C-406 declaration/usage
split, one finding on each side. Confirmed `DataHandling.ir`'s own C-301 exemption directly against
its real content (not just the synthetic unit test written for it): it has three genuine `.%X`
slice-access writes (`StatusWord.%X0/.%X3/.%X7`) that would otherwise all be flagged, correctly
exempted because its header comment says "Data-handling reference examples" — the exact scoping
bug the plan-agent review caught before any code was written, now proven against real corpus
content.

**Exit criterion explicitly NOT yet met.** `02-roadmap.md`'s own S4 exit is "review of the reference
project matches the engineer's own review on a sample; false-positive rate acceptable" — that needs
an actual blind comparison against the project owner's independent judgment on the same content,
which hasn't happened (the pilot above used findings already narrated during planning, acknowledged
upfront as not a clean blind test). Full report presented for review 2026-07-15; project owner's
read: good enough to pause active work here and move to S5, explicitly *not* the same as calling S4
done — S4 stays **ACTIVE**, Phase 1 complete and pilot-proven, formal exit validation deferred
rather than skipped silently. Two of the pilot's own findings (the packed alarm words in
`NodeStatusAlarms`/`PerimeterSafetyAlarms`) are open questions about whether the *rule* or the
*practice* is right, not resolved either way — carried forward, not force-closed.

**Durable checkability findings** (owed from the plan's own "don't let this decay into
conversation-only narrative" discipline — both prior project audits, 2026-07-11 and 2026-07-14,
flagged exactly that failure mode as this project's most-repeated documentation bug):

- Severity/checkability mismatches: **C-113** and **C-504** are *error*-severity rules that are
  genuinely Bucket C (human design-intent judgment — "was this the right paradigm," "is this really
  a known consequence of that alarm") — no code-property check can validate them even in principle,
  only whether a stated *decision* exists (C-113's own "the chosen paradigm is stated in the header
  comment" clause is separately Bucket-A checkable, but that's a materially weaker claim than "the
  right paradigm was chosen"). **C-112** is *error*-severity and needs a WinCC HMI artifact this
  pipeline never ingests at all — structurally out of reach, not just unbuilt.
- **C-301's exception scope is three-part, not one**: alarm words (C-501's own conditions), comms
  mapping, and data-handling blocks (C-105). A pre-implementation review caught a draft that only
  modeled the first and would have false-positived on `DataHandling.ir`'s legitimate usage — all
  three now share one `IsSelfIdentifiedExemptBlock` keyword-match mechanism (`Rules.cs`),
  deliberately loose and Phase-1-appropriate, not a full C-105 compliance check.
- **C-406 needs checking in two structurally different places**: the *declaration* form
  (`DbMember.Datatype == "TONR_TIME"/"TOF_TIME"`, what `FBTimers.ir` has — zero networks, so a
  usage-only check would have silently missed it) and the *usage* form (`TimerBinding.Kind` inside
  a network's own `Timers` list, what `TimingAndCalls.ir` has instead). Both built and both fired
  correctly in the pilot.
- New, found while writing this section rather than during original planning: **C-408** ("`ET`
  never compared against a constant to produce a boolean trigger") looks Bucket-A checkable after
  all — a single-block scan for any `Expr.Compare` with one operand's `TagRef.Path` ending `.ET` —
  worth a look for Phase 2, not built this round.
- Rough re-estimate of the full ~50-rule set, done fresh against the actual rule text and the
  now-built `TagReferences`/`Rules` infrastructure (supersedes the looser live-conversation estimate
  from initial planning — deliberately not reconciled line-by-line against it, since that estimate's
  own per-rule reasoning was never itself recorded anywhere durable): beyond the 8 rules built here,
  candidates that look genuinely Bucket-A/single-block-checkable without new infrastructure include
  C-103 (Set/Reset pairing within a block), C-107 (edge-memory array one-write-per-element
  discipline), C-110 (OB1 input/output mapping call order), and C-408 above. A similar-sized group
  needs cross-block or whole-project context that doesn't exist yet in this tooling (C-114's own
  enable-chain cycle check is explicitly called out as mechanically checkable *in the rule's own
  text*, once that cross-block graph exists; C-308's "`DB_Settings` never written from logic" is
  fully mechanical in principle but needs scanning every block in a project, not one file at a
  time). A firm remainder (e.g. C-002, C-004, C-101, C-108, C-113, C-117, C-202, C-309, C-405,
  C-409, C-504, C-507) stays genuine human judgment regardless of tooling investment. Not
  re-verified to the same rigor as the 8 rules actually built and tested this round — a starting
  point for scoping a future Phase 2, not a committed spec.

## S4: real-content validation against JOB9002, `StatusAlarms` (2026-07-15)

Data-boundary approval extended first (own dated entry above) to cover `converter review` against
real JOB9002 content — a third, distinct kind of activity from S2's read/explain and S3's
write/comment-generation. Picked `FC StatusAlarms` (station_1, `JOB9001_PLC`) specifically because
that whole station had never come up anywhere in this project before (every prior touch — S1's
round-trip proof, S2's four explanations, S3's `PlantAutoControl` proof — used station_2, `JOB9002_PLC`),
so review of it is unambiguously fresh, and because it's alarm-category, the same family as
`PerimeterSafetyAlarms` (already explained during S2) — a good stress test for the C-301/C-501 exception
logic specifically.

Exported, converted to IR, and reviewed cleanly — 8 error/4 warn findings: missing `FC_` naming
prefix, no header comment, all networks with real logic left untitled, and three networks each
packing multiple alarm bits into one network (C-301+C-501 both firing) rather than satisfying the
"exactly one bit per network, network titled" exception — the same shape already seen in
`NodeStatusAlarms`/`PerimeterSafetyAlarms` during the reference-corpus pilot.

**Methodology note, stated honestly rather than glossed over:** this was not the blind comparison
S4's own exit criterion calls for. The plan was to hold the tool's output back until the project
owner had independently reviewed the same block and reported their own findings first; instead,
asked to see the tool's output immediately, with their own independent judgment to follow (flagged
this distinction explicitly before revealing, so it was a known tradeoff, not a silent one).
Verdict: **"That's what I got to, mark that as a pass."** — real signal that the findings hold up
against the project owner's own judgment on genuine, previously-untouched real-project content, but
a "shown first, then confirmed" match is a weaker result than an independent match would have been
(the standard confound: agreeing with an answer already seen is easier than reaching it
independently) — recorded as such, not inflated into "the blind test passed." Whether this is
sufficient to call S4's exit criterion met, or whether a genuinely blind pass is still wanted before
that claim, was left to the project owner rather than decided here.

