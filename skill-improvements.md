# Skill & lad-coder tooling improvements — findings

**Date:** 2026-07-18 · **Scope:** read-only audit of `explain-plc-block`, `gen-architecture`,
`review-conventions`, `review-functional`, `review-simplicity`, and the `lad-coder` agent — asking
where scripts or existing/new `converter` tooling could make them more efficient or reliable.

## Headline

The skills and the `lad-coder` agent are well-designed; the efficiency lever is **not** rewriting
them. It is **moving deterministic hand-work out of the AI passes and into the `converter` tool** —
which the skills are explicitly built to accept. `review-conventions`' "NotApplicable-gap /
self-retiring" clause already states that a hand-sweep auto-retires the moment `converter review`
covers that rule ID for that content kind. So growing the tool *shrinks the skills automatically*;
no skill edit is needed for each rule that lands.

**Grounding of current state:** `converter review` (`src/converter/Converter/Review/Rules.cs`)
mechanizes only ~8 rules today — C-003, C-005, C-201 (presence), C-301, C-406, plus three
structurally-vacuous checks (C-102/C-401/C-404). Everything else in the three review skills is the
AI hand-executing grep sweeps, every run, at the skills' own self-budgeted ~20 min / ~200k tokens
for a full-corpus conventions run. The IR is fully parsed and `Ir/TagReferences.cs` already
enumerates every tag reference in a network, so most of these sweeps are re-deriving by hand what a
rule method could compute deterministically.

## Ranked opportunities

### 1. Grow `converter review` with the deterministic sweeps the skills spell out by hand
**Already tracked as FI-09 (parked).** This finding adds *specificity* about which rules pay off.
`review-conventions` writes out exact mechanical recipes that are pure structural checks on the
already-parsed IR model. Highest-leverage, genuinely mechanizable:

- **C-121** — a Step-write must be a `MOVE` whose `EN` contains `Step = <from> AND …`. The skill
  gives the exact recipe; the model exposes every MOVE and its EN expression.
- **C-118 / C-119 / C-120 / C-122 / C-125** — sequencing structure (Step is one `Int` in the
  interface UDT; step 0 present and explicitly returned to; steps ascend by 10; dwell-timer shape).
- **C-408** — `.ET` used inside a comparison expression. Trivial over `TagReferences` / `Expr.Compare`.
- **C-103** — SCOIL/RCOIL set-vs-reset pairing per target bit (`CoilAssignment.Kind` already carries
  Assign/Set/Reset).
- **C-107 / C-402** — edge-memory single-writer discipline (within-block, mechanical).
- **C-001 non-prefix** — PascalCase / no-underscore on member and variable names (members and
  `TagReferences` already enumerated).

Each new rule follows the existing "one method per rule" pattern in `Rules.cs` plus a
`ReviewRulesTests.cs` fixture — cheap, tested, self-documenting. Each one that lands removes a whole
hand-sweep from `review-conventions` via the self-retiring clause. `AITODO.md:103` already flags one
such gap (C-003's `iDB_<FBName>_<Instance>` sub-clause).

**Revisit trigger (from FI-09):** mechanize the rules that real `review-conventions` use shows fire
often — not all ~42 at once. The re-estimate found genuine severity/checkability mismatches; some
rules are not mechanical at any reasonable cost.

### 2. Add a whole-project review mode — the gap FI-09 does *not* name
The cross-block rules (`review-conventions` Group 2: **C-127, C-304, C-308, C-307, C-407, C-115**)
are pure reference-graph analysis, but `converter review` is per-file, so the AI does project-wide
greps by hand. The infrastructure to fix this already exists: `Preflight/PreflightRunner.cs` builds
a `ProjectIndex` over the whole export. A `converter review --project ir/<project>/` (or a new
`cross-check` subcommand) reusing that index could emit the exact tables the skill's report already
asks for:

- **C-308** one-writer table — enumerate every write destination targeting `DB_Settings.*` or an
  instance-UDT settings member. Fully model-supported (coil / move / call-output destinations).
- **C-115** handshake-vocabulary table (equipment-FB UDT in/out names vs `enable`/`ready`/`running`).
- **C-127** reusable-FB sibling-reference check (`iDB_`/`CALL` inside a reusable FB body).
- **C-304** IO-boundary scan (physical-IO tag refs / raw `%I`/`%Q` outside Map FCs).

Emitted as verbatim tool output the reviewer reasons over — same status as today's `converter
review` dump, not a new judgment source.

### 3. `review-functional` Pass-2 dead-structure detection is scriptable (same index as #2)
The reverse pass — "every interface-UDT / buffer-DB member, checked for readers AND writers, both
directions; consumed-but-never-written and written-but-never-consumed" — is a whole-project
reader/writer set-difference. That is exactly the in-cycle-lamp bug class the skill exists to catch,
and it is mechanical. The REQ *tracing* stays AI (needs register semantics); the dead-wiring
detection should not be hand-grep.

### 4. `explain-plc-block` — a template-instance structural-fingerprint helper (untracked, net-new)
The skill's core method is "describe the repeating template once, then verify *every* instance —
copy-paste drift is where the real findings are." That is a mechanical job: emit a normalized
structural signature per network so the outlier instance surfaces instead of being hand-compared
across N networks. `Digest/DigestBuilder.cs` already computes per-network statement summaries; a
`--fingerprint` mode extending that would directly serve the skill's stated main failure mode.
**Caveat:** keep it distinct from `digest`, which is policy-banned *as review input* — this is an
orientation aid for explanation, which is allowed.

### 5. `gen-architecture` — mechanize the two rote, error-prone bookkeeping steps
- **Provenance header** requires `git log -1 --format=%h -- <path>` per input. A tiny wrapper
  emitting the whole provenance block would remove a hand-repeated, easy-to-fumble step.
- **Tag-status classification** ("every named tag `exists`/`proposed`") is exactly
  `ProjectIndex.ResolvesAsTagRoot`. A `converter tagstatus <names…> --project ir/<project>/` mode
  would let the designer mechanically classify a proposed-tag list instead of hand-grepping — the
  same primitive `preflight` already uses.

## What should stay AI — do not script these
The skills already draw this line correctly; affirming it so nobody over-mechanizes. All bucket-C
judgment rules (C-113 paradigm choice, C-504 suppression intent, C-002/C-004 semantic naming, the
one-reading test), blindness/regime disposition, owner-ruling routing, and the docs/evidence report
split. Scripting these would manufacture false confidence — worse than the hand-work.

## Relationship to existing backlog
- **FI-09** — mechanize more rules (parked). Opportunity #1 is this, made specific.
- **FI-11** — presentation bundling; **FI-13** — `preflight`, already built and adopted;
  **FI-20** — the docs/evidence report split (already baked into the skills).
- **Net-new (not seen tracked anywhere):** opportunity #2 (whole-project review mode) and
  opportunity #4 (template-fingerprint helper for `explain-plc-block`).

## Note
Audit was read-only against the working tree at commit `137e101`. No skill, agent, or tool file was
modified. Any build here is normal PC-side tooling work (`src/converter/`) under normal software
rules — not `lad-coder`-dispatched, and it changes no PLC/IR content.
