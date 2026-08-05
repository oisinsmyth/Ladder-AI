---
name: gen-block-new
description: The Build-stage skill (docs/15 pipeline skill #7) that turns ONE gate-1-signed architecture manifest item into ONE new, compile-clean block's IR. Use whenever asked to code, write, build, or generate a NEW ladder block — "implement this manifest item", "generate the FB/FC for <equipment>", "turn the architecture into ladder", "code up the motor block", "write the IR for <block>" — or after a gate-1 architecture sign-off when coding is the next step. This is the reuse-first coder: it composes from patterns/ and CALLs proven blocks, it does not re-architect. NOT for modifying or fixing an EXISTING block (that's the S7 modify pair, gen-block-modify-purpose / gen-block-modify-fix) and NOT for designing the manifest (that's gen-architecture). Runs inside the lad-coder sub-agent (CLAUDE.md hard rule 8). Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
  - Edit
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  - Bash(./src/openness-cli/OpennessCli/bin/Release/net48/openness-cli.exe:*)
  - Bash(src/openness-cli/OpennessCli/bin/Release/net48/openness-cli.exe:*)
---

# /gen-block-new — the Build-stage coder (one signed manifest item → one compiling block)

Ladder-AI project. This is docs/15's `gen-block-new` stage (pipeline skill #7): it consumes **one
gate-1-signed item from `gen/<project>/architecture.md`** and produces **one new block's IR**,
imported and compiled clean on the scratch project. It is the skill that finally moves S6 from
"pipeline built" to "generating" — and the one that closes S6's exit criterion (ten fresh
plain-language generation requests). Read `CLAUDE.md` at the repo root in full first if you haven't;
its hard rules bind you and this doc assumes them.

**You run inside `lad-coder`** (CLAUDE.md hard rule 8). If you were reached any other way, stop — a
human-facing agent must dispatch this to `lad-coder`, never run it inline. The whole point of the
pipeline is that the AI exercises it; an agent quietly hand-coding rungs defeats the deliverable.

**You are a coder — not a designer, not a reviewer.**

- **The manifest is the design; you implement it, you don't re-open it.** The block set, interfaces,
  wiring, and pattern-tier for your item were decided at gate 1 (`gen-architecture`, the cheapest
  place to kill a C-127-class mistake). If you find a genuine design problem while coding — the
  manifest can't be built as written, an interface member is missing, two REQs conflict — that is a
  **stop-and-report**, an open question back to the design, never a silent re-architecture in code.
- **You never review your own work with the AI reviewers.** docs/15's isolation model exists because
  the author is the worst-placed party to catch their own blind spots. Your job ends at
  compile-clean IR + evidence; `review-conventions` / `review-functional` / `review-simplicity` run
  **separately, in fresh context** (the Check stage). You get mechanical review for free via
  `converter preflight` (which folds in `converter review`) — that is a filter, not the AI review.
- **Never invent tags, addresses, DB numbers, or hardware** (hard rule 3 — the rule this stage trips
  most, because writing a rung begs for "there must be a start button"). Every tag you reference is
  grep-verified `exists` in the current `ir/<project>/` export, or it is a `proposed` gap that
  **stops the run** — the engineer creates tags, you never write against a proposed one.
- **Safety content = stop** (hard rule 2). Any F-block / safety-program material in scope: stop and
  report, never read or reference its internals.
- **No edits to existing networks** (docs/15 boundary). This skill creates *new* blocks/networks
  only. Touching an as-built network is the S7 modify pair's job (`gen-block-modify-purpose` /
  `gen-block-modify-fix`), and `converter diff`'s invariance check exists to enforce that line — do
  not cross it here because it seems convenient.

## Scope — what "one new block" means (owner ruling, 2026-07-18)

**You emit:** the callable code block itself (FB / FC / OB) *including its own interface* (Static /
Temp / Input / Output / InOut / Constant members the block declares), plus **instance-DB
scaffolding** for any FB it introduces (`openness-cli create-instance-db`).

**Out of scope — a prerequisite, not something you create:** shared `DB_*` and UDT emission, and tag
tables. If your item needs a shared DB, UDT, or tag that the export doesn't already show, that is a
**named gap that stops the run** (same discipline as a `proposed` tag) — the data landscape belongs
to the design / `gen-integration` / `gen-io-tags`, and inventing it here would launder invented
reality into code. Report the gap and stop; don't improvise the DB.

## Inputs

- **`gen/<project>/architecture.md` — the specific manifest item, gate-1 SIGNED. REQUIRED.** Read the
  item, its **carving tier** (section 6: (a) whole library block / (b) pattern-composed / (c) modify
  — *not yours* / (d) freeform), the **REQ(s) it implements** (section 7), its interface (section 2),
  and its wiring (section 8). **Stop conditions:** no architecture artifact, or the gate-1 sign-off
  line not signed → stop and say gate 1 must be signed first (docs/15 gate 1 is never skipped). A
  tier-(c) item is not yours — route it to `gen-block-modify-purpose`. Never code from a remembered
  or conversation-supplied design.
- **`gen/<project>/code-structure.md` §D3 — REQUIRED where the project was specified through the
  A–D rungs.** The per-instance render is the term-level contract: every term carries the relation id
  it satisfies, and you implement **all** of them. Fitting the render to the nearest pattern shape and
  shedding the residue is the documented generation failure
  (`docs/evidence/PlantAutoControl-bench-autopsy.md` cause 2) — a term you cannot place in the pattern is a
  stop-and-report, never a drop.
- **`patterns/` + `docs/07-pattern-library-spec.md`** — your composition vocabulary. Read the
  relevant pattern's `pattern.md` in full, including its **admission status** and its `examples/`.
  The three kinds behave differently (below).
- **The current `ir/<project>/` export** — full IR of anything your block references or calls (read
  it, don't guess its interface); the tag-status grep source. `converter digest` is allowed for
  *orientation only* ("which block do I open?") — never as the basis for writing a rung or matching a
  callee's parameters; read the full IR of anything you actually wire to.
- **`docs/06-lad-conventions.md`** — read fresh this run, never from memory: the preamble (priority
  order function → readability → efficiency, and the stricter bar for generated code), C-106/C-108
  (equipment FBs), C-109/C-110/C-111 (call structure, OB order, simulation gating), C-115 (handshake
  vocabulary), C-118–C-125 (sequence/step shape, if freeform), C-126 (grouping — see below),
  C-30x (data landscape you read from), C-401/C-402/C-404/C-407 (instructions), C-501–C-507 (alarms).
- **`docs/notes/compile-error-playbook.md`** — first lookup on any compile failure; entries are
  grounded hypotheses to verify against the actual error, never answers to trust blindly.

## Method — compose per tier, then the inner loop to a clean compile

**Work from the signed manifest item toward IR — never invent structure the manifest didn't carve.**

1. **Read the item, its tier, its REQs, its interface, its wiring.** Read the full IR of every block
   it calls or wires to (a callee's real parameter interface is ground truth — never assume it).
2. **Compose per carving tier:**
   - **(a) Whole library block** (pattern kind 1, e.g. `motor-dol`): the block already exists —
     your work is the **instantiation**: `openness-cli create-instance-db` for the instance, a
     `CALL` with real arguments wired per the manifest (section 8), and the calling network. TIA's
     compiler type-checks every argument for free. Do not re-implement the library block's internals.
   - **(b) Pattern-composed** (pattern kind 2, repeated rung-shape, e.g. `chained-permissive-enable`,
     `input-mapping`): draft the new instance **by analogy from the pattern's real `examples/`**,
     mapping this equipment's real tags into the documented shape. `pattern.md` says what's fixed and
     what varies — hold to it; a structurally new case the pattern doesn't document is a note worth
     surfacing (S8 harvest / FI-05), not a licence to freelance.
   - **(d) Freeform**: only when the manifest carved the item (d), and only under the **gate-1
     freeform go-ahead** (the >20% flag / CLAUDE.md workflow step 3). Freeform is never
     convention-free — write to the exact doc-06 rules the manifest named for it (C-118..C-125 for a
     sequencer, C-501/C-504 for an alarm FC, …).
   - A tier-(c) item is not yours — stop and route to `gen-block-modify-purpose`.
3. **Tag-status gate (anti-laundering, hard rule 3).** Run
   `converter tagstatus <every tag you will write> --project ir/<project>/`. It classifies to
   **member** level: `PROPOSED` (root absent) **and** `MEMBER-NOT-FOUND` (root exists, member
   invented) both **stop the run** — report the gap; the engineer creates it, you never code against
   it. So does `INDEX-OUT-OF-RANGE` (the member is real, the array element you subscripted is not) —
   that one is a fix to your own binding, not a gap for the engineer.
   A `MEMBER-UNCHECKED` result means the export cannot enumerate that namespace (an unexported
   UDT, an instance-DB stub): the tool did not verify it, so **you** must, by reading the type.
   Re-verify the manifest's own `exists` marks here — trust the tool, not the artifact's memory.
   *(Until 2026-08-05 this check was root-level only and would pass an invented member; do not rely
   on a remembered "it said EXISTS".)*
4. **Write the IR, grouped by function (C-126) at write time — see below.**
5. **Inner loop to a clean compile** (CLAUDE.md workflow step 4; the loop `lad-coder` owns):
   `converter preflight <files> --project ir/<project>/` — **zero-findings bar** (a consciously
   accepted finding needs the engineer's explicit recorded OK; preflight is a filter *before* the
   compile gate, never a substitute — hard rule 4) → `converter to-xml` → `openness-cli import` to
   the **scratch** project → `openness-cli compile` → iterate until clean. On any failure, check the
   compile-error-playbook first, verify its hypothesis against the actual error, and harvest any new
   proven error→fix pair back into it. Respect the `agent-tasks/README.md` Portal queue before any
   import/compile against a shared scratch project.

## Authoring new (sidecar-less) IR — the mechanics that bite

A block you write from scratch has **no `SIDECAR` section** (that's machine-owned round-trip data,
minted from a real TIA export — you don't have one). So convert it with **`converter to-xml
--synthesize`**, not plain `to-xml`; the synthesizer mints the sidecar for you. These facts cost real
time on the first run if you don't know them going in:

- **Statement-kind ordering within a network.** The synthesizer emits by kind (timers, then coils,
  then moves, then arithmetic), not in the order you typed them. For **ENO-chained MUL/ADD→CONVERT
  pairs** it pairs `mul[i]`↔`convert[i]` by list index and interleaves their execution
  (`mul0→convert0→mul1→convert1…`, each CONVERT enabled by its own MUL's ENO) — so a **single shared
  TEMP across those pairs is safe** (each CONVERT reads it before the next MUL overwrites; this is the
  proven `motor-dol` "HMI Times" shape). Write the pairs in matching list order; you don't need a
  distinct temp per pair, but you do need the MULs and CONVERTs in corresponding order.
- **A compound operand must lead its AND chain.** An OR-group (or `NOT` of a compound) has to be the
  **rail-most / first** operand — `(X OR Y) AND <rest>`, never `<rest> AND (X OR Y)`. Appending a compound
  to the end of an AND chain is un-synthesizable (`UnsupportedSynthesisConstructException`); AND is
  commutative, so lead with it (the conventional LAD shape). Bites most when adding a permissive to an
  existing chain.
- **The synthesizable subset is narrower than the converter's read side.** `--synthesize` covers plain
  Contact/Coil (incl. SCoil/RCoil, OR/NOT), **TON/TONR/TOF**, MOVE, **MUL/ADD/SUB/DIV**,
  **ABS/SWAP/WAND/CALC/T_SUB/T_CONV/MOVE_BLK_VARIANT**, comparisons and literals (magnitude- and
  registry-typed via the `TagTypeRegistry`, so UDInt/DInt constants **and** Real tag-vs-tag compares work —
  Gaps B/E), **registry-typed CONVERT** (typed from operand types, no longer only Real→DInt), and **CALLs**
  — zero-argument (STATIC-struct convention) **or with wired Input/Output arguments** (the argument types
  come from the callee, so its `.ir` must be in the same `--synthesize` batch or a `--project <ir-dir>`).
  Still out of subset: **InOut call params, and `Limit`/`Wait`/`FillBlockI`/`Modbus*`**. Anything outside
  that hard-errors at synthesis. If your design
  needs a construct the synthesizer can't mint, that's a **converter gap to report** (hard rule 7 —
  never hand-patch the XML), not a coding failure; note it and, where the register allows, ship the
  supported equivalent with the gap flagged.
- **Portal mechanics.** A cold `openness-cli` open needs the **absolute `.ap20` path** (a bare project
  name only resolves if it's already open). A `SampleProject` open is slow — run it **backgrounded**
  rather than foreground (it can exceed the 10-min cap). Copy the device `--group` value **verbatim**
  from `openness-cli list`'s Path column (it can embed spaces and an article number as one literal
  string).
- **Naming vs. the manifest.** If the manifest's literal block name lacks the C-001/C-003 `FB_`/`FC_`
  prefix but the manifest also commits to "names follow C-001/C-003," the convention wins: **apply the
  prefix and proceed**, don't stop on the apparent conflict. Note the applied name in your report.

## C-126 at write time — group by function, not by instruction kind

The whole pipeline exists because test-project001's first build was compile-clean and *obtuse*
(docs/15 "Why this exists"). The signature defect was structural: rungs grouped by instruction kind
— a block-wide "Timers" network, all MOVEs together — so no single network read as one coherent
piece of equipment behaviour. **Write each network as one function/equipment story** (start/stop
seal-in, this motor's fault handling, this step's transition), the way an electrician with a
multimeter reads a rung and gets the gist. This is cheaper to do at write time than to restructure a
finished block into — which is exactly the "design-stage mistake caught at code-stage prices" the
pipeline is built to avoid. Every network gets a title (why, not what).

## Gates and handoff

- **Compile gate (hard rule 4):** nothing is "done" until it imports and compiles clean on scratch.
  Preflight passing is not the compile gate — it is the filter in front of it.
- **You do not run the AI reviewers.** Hand off compile-clean IR; the Check stage
  (`review-conventions` / `review-functional` / `review-simplicity`, + `audit-artifact` for
  artifacts) runs in **fresh context**, given only the IR and its binding docs, never your reasoning.
- **You never import into the real project** (hard rule 5) — scratch only; your output is a proposal.

## Exit

Hand back (per `lad-coder`'s "what you hand back" contract — your summary is not proof):

- the **IR diff** (the new block; for a tier-(a) instantiation, the calling network + instance DB);
- a **one-paragraph intent statement** (what it does, which REQ(s) it implements, which pattern/tier);
- **preflight evidence** (zero findings) and **compile evidence** (pass, error/warning counts);
- the tag-status result (all `exists`), and any gap that stopped the run.

Then **append one telemetry line** to `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`
(`gen-block-new` in the skill column; include blocked/abandoned runs — the most informative rows;
never backfill or rewrite rows). Then **stop.** The Check stage and gate 2 (final presentation,
`docs/11-review-workflow.md`) belong to others; never treat your own output as reviewed or approved.

## Calibration

- **This skill codes; it does not design or review.** Design problems → open question back to the
  manifest, not a code workaround. Findings about existing code → route to the reviewers, don't fix
  drive-by.
- **Reuse first, freeform last.** Prefer the tier the manifest carved; a tier-(a)/(b) fit that
  genuinely covers the REQ group beats freeform every time (freeform is where obtuseness breeds,
  docs/15 cause 3). Never widen scope beyond the item's REQ(s) — C-606: added capability needs a REQ
  or a cited convention, never "it seemed useful".
- **Manual-coding note:** this skill is validated — it is the path for new-block coding. Ad hoc manual
  coding to the CLAUDE.md 5-step contract still needs a per-case owner waiver (docs/15 Boundaries, A-3).
- **Telemetry always**, including a run that stopped at a gap — a stopped run with a named blocker is
  a more useful row than a silent one.
