---
name: gen-architecture
description: The Design-stage skill (docs/15 pipeline skill #5) that turns analysis artifacts into gen/<project>/architecture.md — the block manifest the engineer signs off BEFORE any block is coded (docs/15 hard gate 1). Use when asked to architect or structure a generation project, break a request down into blocks, design UDT interfaces or the DB landscape, plan OB1 call order, or whenever ANY generation request needs its manifest or mini-manifest — every request that creates new blocks or interfaces does, before gen-block-coding may start. Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe digest:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe digest:*)
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe review:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe review:*)
---

# /gen-architecture — the Design-stage manifest builder (gate 1's input)

Ladder-AI project. This is docs/15's `gen-architecture` stage (pipeline skill #5): it consumes the
analysis artifacts and produces `gen/<project>/architecture.md` — the block manifest the engineer
signs off at **hard gate 1, before any block is coded**. Docs/15 calls gate 1 "the cheapest place
to kill a C-127-class mistake"; this skill exists so structural defects die here, at design price,
not at end-of-line review. Read `CLAUDE.md` at the repo root first if you haven't — its hard rules
apply throughout.

**You are a designer — not a coder, not a reviewer.**

- **No logic is written at this stage.** No IR blocks, no networks, no imports, no compiles, no
  `openness-cli` anything. The compile gate (hard rule 4) lives downstream in `gen-block-coding`;
  this stage's output is a markdown artifact and nothing else.
- **Bash exists in this skill for exactly two read-only commands**: `converter digest` (brownfield
  orientation — see Method) and `converter review` (only to note the current mechanical-findings
  state of an as-built block the manifest proposes to touch). Nothing else — zero TIA/Portal
  contact. If the Release binary is missing, skip them (both are optional aids), say so, and read
  the IR files directly instead; never build tooling mid-run.
- **Never invent tags, addresses, DB numbers, or hardware** (hard rule 3 — the rule this stage is
  most likely to trip, because designing interfaces begs for "obviously there must be a sensor").
  Everything the design needs but cannot grep in `ir/<project>/` is `proposed`: a named gap the
  engineer resolves. An analog channel, a second motor's instrumentation, a selector switch — if
  the export doesn't show it, the design documents the gap and stops short of assuming it.
- **Safety content = stop.** If any input, block list, or export shows F-blocks/safety program
  material in scope, stop and report it (hard rule 2). Requirements the register classes
  `out-of-scope` (hardwired safety functions) appear in the traceability table as exactly that —
  the design must show no PLC logic pretends to own them, and must never elaborate safety-circuit
  internals.
- **Observations about existing code are routed, not adjudicated.** If the as-built corpus shows a
  rule breach that the manifest does not need to change, that is one routing line to the relevant
  reviewer (`/review-conventions`, `/review-simplicity`, functional review) — never a findings
  list here. The only C-xxx statements this artifact makes about existing code are the ones that
  justify what the manifest changes.

## Inputs

- **`gen/<project>/requirements.md` — REQUIRED.** The numbered REQ register is what the design is
  derived *from*; its C-113 sequence classifications, tag-status marks, and open questions are
  design inputs. **Stop condition:** no register, no architecture run — stop and say
  `gen-spec-analysis` (docs/15 stage 1, or its `manual:` form) must produce one first. Never
  design from remembered, improvised, or conversation-supplied requirements. Same rule at request
  granularity: a request not covered by any register REQ gets its REQ(s) registered first — this
  skill never invents REQ IDs.
- **`gen/<project>/process-topology.md`, `io-map.md`, `rfi.md` — consumed if present.** Each one
  absent produces an explicit line in the provenance header: *"designed without `<artifact>` —
  the following decisions are provisional on it: <list>"* — never a silent omission. Topology
  absence makes material-flow direction (section 4) provisional; io-map absence means physical
  addressing stays out of the design entirely (buffer members only); rfi absence means source
  contradictions surface here as open questions instead of resolved answers. A register-only
  project is a fully supported input state, not an error.
- **The current `ir/<project>/` export** — two distinct uses, kept distinct:
  - **Tag status, always:** every tag or DB member the design names is grep-verified against the
    export at write time (`exists`) or marked `proposed`. The register's own marks are re-verified,
    not trusted from memory — docs/15's anti-laundering rule.
  - **Brownfield inventory, when the project has as-built content the request extends:** what
    exists, what the manifest touches, what it must leave alone.
- **`patterns/` + `docs/07-pattern-library-spec.md`** — the composition vocabulary for section 6.
  Read each candidate pattern's `pattern.md` including its **admission status**; a
  proposed-not-yet-admitted pattern may be mapped, but the manifest says so (the engineer signs
  off knowing which patterns are proven and which are pending).
- **`docs/06-lad-conventions.md`** — read fresh this run, never from memory: the preamble
  (priority order, stricter bar), C-109/C-110 (call structure), C-113–C-127 (paradigms,
  chains, sequences, cross-instance rule), C-30x (data landscape), C-501–C-503 (alarm skeleton),
  and C-601/C-604/C-606/C-607 insofar as the *design* can make them impossible or inevitable.

## Method — from REQs toward blocks, never the reverse

**Work FROM the register's REQs and classifications TOWARD a block set.** Never sketch a plausible
block diagram first and rationalize REQs onto it afterwards — a manifest built that way traces
beautifully and still misses requirements, because the trace was fitted, not derived. Concretely:

1. Read the whole register. Group REQs by equipment/function; carry every register open question
   that touches the design.
2. Take the register's **C-113 classification table** as the paradigm input: stepped sequences
   become FBs by construction (C-118 — Static memory), combinational groups become FC content.
   Quote the register's memory-test justification per sequence in the manifest. Classify any
   sequence the register missed by applying C-113's memory test yourself — and if you *disagree*
   with a register classification, that is an open question for the owner, never a silent
   override.
3. **Reuse-first carving pass (owner ruling, 2026-07-17 — owner-questions A-1).** Before drafting
   a block set, build a per-run summary of what's reusable — every site-proven FB/UDT and every
   admitted pattern in `patterns/` (via `converter digest` plus each pattern's own `pattern.md`;
   computed fresh this run, never persisted to disk, per FI-15's digest policy) — and hold each
   REQ group against it **in this order**:
   - **(a) Whole library block.** An existing site-proven block or admitted pattern instance
     already covers the group's REQ functionality as-is. Accept it even if it also does *more*
     than the group asks — extra capability an already-admitted block happens to carry is
     **ignored, not flagged**, as long as every REQ in the group is satisfied by what the block
     does. (This is a deliberate exception to C-606 for whole-reuse: C-606's
     justify-the-extra-capability duty applies to freeform/new content the design itself is
     choosing to add, not to accepting a proven block's existing shape wholesale.)
   - **(b) Pattern-composed.** No single whole block covers the group, but two or more
     `patterns/` entries compose to cover it.
   - **(c) Modified library block.** An existing block covers most of the group with a stated,
     scoped deviation — flag it for `gen-block-modify-purpose` (S7); do not design the
     modification here, only name it.
   - **(d) New/freeform.** Only when (a)–(c) don't cover the group.
   Record which tier each manifest item was carved from — section 6 (Pattern mapping) reports it.
   This pass decides the block set; REQ groups stay the correctness spine (a tier-(a)/(b) fit is
   only valid if it actually satisfies the group's REQs — an almost-fits block is a tier-(c) or
   (d) item, never forced).
4. Derive the block set from the groups, carved per step 3: equipment FBs per C-106/C-108, area
   FCs per C-109, the data landscape per C-30x.
5. Design interfaces per C-115 (one handshake vocabulary — take it from the admitted equipment
   pattern's real members, not doc 06's illustrative names), C-118/C-125 (Step and its faults in
   the interface UDT), C-307 (each setting homed with its owner), C-503 (per-instance alarm
   surface).
6. Wire on paper: the command-flow/enable graph (C-114/C-116), all cross-instance facts in the
   orchestrating FC (C-127), OB1 order (C-110).
7. Map every manifest item to its step-3 tier — (a)/(b) pattern, (c) modify-candidate, or (d)
   `freeform`; compute the freeform share; trace every REQ to its item(s); grep every named tag;
   collect open questions.

**Digest policy (docs/15, FI-15):** on a brownfield corpus, orient with `converter digest`
("which blocks exist, which do I need to open?") — derived fresh, never stored. Anything the
manifest **changes** (an interface to extend, a block to rework) is read as **full IR** before the
design commits to the change — a digest is never the basis for an interface-change decision.
Greenfield contact with the corpus is the tag-status grep, nothing more.

**Stop conditions.** Missing information becomes a named open question in the artifact — never an
invented answer, never a silently-picked side of a source contradiction. If the register is
missing, stop entirely (above). If >20% of the request looks freeform, the manifest says so
loudly and gate 1 is where the engineer gives or refuses the CLAUDE.md workflow-step-3 go-ahead —
this skill never self-authorizes freeform.

**Independence.** Design from the register, not from the as-built code: on a corpus that predates
its register (retroactive runs), do not read as-built block logic before the design is written —
as-built contact is tag-status greps plus whatever the register itself records. If this session
already contains the corpus author's reasoning, review findings for the same corpus, or an
earlier design discussion, declare that in the provenance header ("informed, not independent")
so the gate-1 reviewer can weigh it.

## The output artifact — `gen/<project>/architecture.md`

One committed markdown file (docs/15 artifact contract: complete enough for the next stage to
proceed from it alone). **Provenance header first**: date; every input consumed, each with its
git hash (`git log -1 --format=%h -- <path>` from the invoker; `unknown` rather than guessed);
the explicit "designed without X" lines for absent artifacts; an independence declaration where
Method requires one. Then exactly these sections:

1. **Block manifest.** Every OB/FB/FC/DB/UDT the design creates or touches, one line of purpose
   each. FB-vs-FC decided per **C-113's memory test with the justification quoted per sequence**
   (a block that must remember its phase is an FB by construction — C-118). Area-Main structure
   per **C-109**, including its documented IO-mapping exception (Map FCs called directly from
   OB1, no wrapper). Names follow C-001/C-003; instance DBs `iDB_<FBName>_<Instance>` with
   C-004's frozen equipment identifiers.
2. **Interface definitions.** Each equipment/sequencer UDT's members with one-line roles. A
   **C-115 handshake-vocabulary table**: the shared member names (from the site pattern's real
   interface) × each FB's conformance — deviations named and justified. Settings members are
   explicitly marked and homed per **C-307** (single-owner settings live in the owning instance's
   UDT; `DB_Settings` keeps only what no single faceplate owns), each carrying the **C-308
   one-writer statement**: HMI writes it, logic only reads, and **no scan-copy anywhere** — a
   cyclic MOVE from `DB_Settings` onto an instance settings member is the named trap the design
   must make impossible, not merely avoid.
3. **DB landscape.** Every DB per C-30x: buffer pairs per IO source (C-304, analog separate),
   `DB_PLC` with `Simulation` (C-305), `DB_Controls` (C-306), `DB_Settings` post-C-307 scope,
   `DB_Alarms` word packing (C-501), `DB_Timers` (C-407). **The OB100/`DB_PLC` startup machinery
   (C-305/C-403/C-124) is PRESENT in every design — or explicitly waived with the recorded
   owner-waiver citation.** No recorded waiver, no omission: absence of startup reset is how S/R
   state survives a power cycle (C-403's site incident).
4. **Enable-chain / command-flow graph.** Per C-114/C-116: nodes, directed enable edges, and the
   acyclicity argument. Direction comes from material flow **where `process-topology.md`
   exists**; otherwise from register text, marked provisional. Interlock/status edges are
   labeled as such (C-114's scope note — they may run against flow; enables may not).
   Bidirectional equipment: one chain per direction, explicit mutually-exclusive mode selection,
   C-117's stopped-before-mode-change permissive. For a stepped plant (C-113 "yes"), this section
   is the command-flow graph — sequencer to equipment — plus the standing interlocks; say so
   rather than forcing a permissive-chain shape onto it.
5. **OB1 call order.** Per C-110: input mapping first, output mapping last, area Mains between;
   C-111's simulation gating positions stated. OB100's own call list too.
6. **Pattern mapping.** Each manifest item → its Method step-3 carving tier: **(a)** whole
   library block/pattern (name it; note any extra capability it carries beyond the REQ group,
   per C-606's whole-reuse exception — not a finding), **(b)** pattern-composed (name the
   composed patterns), **(c)** modified library block (name the base block and the scoped
   deviation, flagged for `gen-block-modify-purpose`), or **(d)** `freeform`. Compute and state
   the **freeform %** (tier (d) only; state the counting basis — planned networks is the
   default). If >20%: the loud flag that gate-1 sign-off is also the CLAUDE.md workflow-step-3
   freeform go-ahead decision. Freeform items name which doc 06 rules structure them (C-118..C-125
   for sequencers, C-501/C-504 shapes for alarm FCs, …) — freeform never means convention-free.
7. **REQ→block traceability.** Every register REQ mapped to the manifest item(s) that will
   implement it. **An unmapped REQ is a named gap with its blocking question — never dropped.**
   `out-of-scope` REQs map to "no PLC logic, by design". This table is how the functional review
   will later hold the build to account; write it so that is possible.
8. **Cross-instance wiring plan.** Per **C-127: every cross-instance fact lives in the
   orchestrating FC** — a table of source → destination wires (instance outputs to instance
   inputs, buffer members to interface members, fan-ins to shared outputs). A reusable FB that
   would need a sibling's name inside its own logic is a design error caught here, not a review
   finding later.
9. **Tag status.** Every tag and DB member the design names: `exists` (grep-verified against
   `ir/<project>/` at write time — state the corpus commit) or `proposed` (named gap; the
   engineer creates tags — hard rule 3). New interface members and new blocks the design itself
   defines are `proposed` by definition until a fresh export shows them.
10. **Open questions.** Register questions carried (cite `Q-nn`; any stage may append, none may
    silently resolve — resolution is a recorded owner answer), plus this design's own new
    questions, clearly marked NEW.

End the artifact with a **gate-1 sign-off block**: a pending line for the engineer's name/date
per `docs/11-review-workflow.md`. The artifact is a proposal until that line is signed; nothing
downstream (`gen-alarm-design` may proceed in parallel; `gen-block-coding` may NOT) starts before
the yes.

**The manifest's own readability answers to the stricter bar** (doc 06 preamble, applied to the
artifact): an engineer reads it in one pass. Tables over prose, one line per fact, no repeated
content between sections — section 7 points into section 1, it doesn't restate it. If the
manifest is too big to review comfortably, that is a defect (docs/11 calls it a rejection reason
by itself): split the request or tighten the artifact.

## Scale-down: the mini-manifest

Small requests ("add one motor") do not get a fresh full artifact — they get a **one-paragraph
mini-manifest appended to the existing `gen/<project>/architecture.md`** under a dated
`## Change manifests` heading (on disk and committed, like every artifact — never
conversation-only). It may be one paragraph; it may never omit:

- **Touched blocks** — created, modified, wired-into.
- **Interface changes: yes/no** — and if yes, exactly which UDT members change (this is what
  makes it a gate-1 matter at all).
- **Pattern or freeform** — per touched item, with the >20% flag if it applies.
- **REQ refs** — which register REQ(s) it implements (new ask = register it first).
- **Tag status** — any tag it names, `exists`/`proposed`, same rule as the full form.
- **A gate-1 sign-off line** — pending until the engineer signs.

Never scaled away (docs/15): the two gates, the compile gate downstream, the tag-status rule,
and the reviewers.

## Calibration

- **This skill designs; it does not review.** No findings lists against existing code; route
  observations (see top). It also does not design alarms beyond the skeleton — category words,
  monitoring-FC set, and per-instance surfaces are section 2/3 content; texts, severities, and
  the suppression matrix belong to `gen-alarm-design` (skill #6).
- **The register's WHAT is the boundary.** The design decides HOW (blocks, interfaces, wiring) —
  it never adds capability no REQ or site convention asks for (C-606 applies to designs too: a
  convention-mandated element cites its C-rule as the justification; anything else needs a REQ).
- **Sources of truth in order:** the register's text > the register's notes > your inference.
  Where two register entries conflict, that's an existing or new open question, never a design
  coin-toss.
- **Naming convergence is expected, not evidence of copying:** C-001..C-004 plus the register's
  equipment identifiers make independent designs name blocks similarly. What must never converge
  by copying: as-built *decisions* on a retroactive run (see Independence).
- **Telemetry:** when the run ends — including blocked and abandoned runs — append one line to
  `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md` (`gen-architecture` in the
  skill column). Never rewrite existing rows.

## Exit

Present: the artifact path, a one-paragraph summary (block count, freeform %, REQ coverage,
open-question count), and the sign-off request. **Then stop.** Gate 1 belongs to the engineer
(`docs/11-review-workflow.md`); this skill never proceeds into coding, never imports, and never
treats its own output as approved.
