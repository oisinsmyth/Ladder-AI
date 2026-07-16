# 06 — LAD Conventions & Style Guide

**Status: POPULATED — all rules below are confirmed site conventions (sessions of 2026-07-09) unless marked otherwise. Remaining gaps are listed in "To fill in".**

**Mechanical review (S4):** a `converter review` tool checks a subset of these rules automatically against IR content — see `docs/notes/stage-gates.md`'s "S4 Phase 1" section for which rules are actually mechanically checkable (vs. needing cross-block context or genuine human judgment), and for specific findings about individual rules below (C-113/C-504/C-112's severity-vs-checkability mismatch, C-301's three-part exception scope, C-406's two checkable forms).

Every rule gets an ID (`C-xxx`) so review findings can cite it, plus a severity: **error** (must fix), **warn** (should fix), **info** (style).

Site mantra: **simple, simple, simple** — the test for control logic is that an electrician with a multimeter and a spanner can look at a rung and get a rough idea of what's going on. Reuse is achieved at the *block* level (standard FBs, UDTs, the pattern library), never through cleverness inside rungs.

**Priority order** (adopted 2026-07-16, ADR-0004): LAD is judged in this order — **1. Function**
(it does what the requirement says: compile gate + functional review, later S9 sim), **2.
Readability & simplicity** (the mantra above: simplicity review against written rules), **3.
Efficiency** (only on a *measured* scan-time/memory problem — on an S7-1200 running plant logic,
nearly never). "Correct but harder to read than it needs to be" is a real finding, and a reviewer
may cite this ordering to recommend an optimization be deleted. Enforcement structure:
`docs/15-generation-pipeline.md`.

**Generated code answers to a stricter bar than existing site practice** (owner ruling,
2026-07-16 — `docs/notes/genproject1-retrospective.md` §9): AI-generated logic faces harsher
scrutiny than a human author's — one failed reading discredits the pipeline, not just the block —
so it must survive a skeptical reader's *single* attempt to understand it. Real site blocks
calibrate the rules below, but "the real block does the same" is never a defense for generated
logic; reviewers err toward flagging, and "defensible" is not a pass.

## Naming

- C-001 *(error)* — Tag naming is layered:
  - **Blocks/types:** `FB_`/`FC_`/`DB_`/`UDT_` prefix + PascalCase function name (`FB_ConveyorControl`, `UDT_Motor`).
  - **Equipment instances:** the frozen equipment identifier (C-004) verbatim (`iDB_Motor_FCC`).
  - **Variables/UDT members:** short PascalCase (`Run`, `FltHigh`, `PosOk`) — context comes from the structure, so short names are correct here. Long names are only needed where the name is the *only* context. *(Revised 2026-07-16 from camelCase, which nothing — site blocks, patterns, or generated code — actually followed; PascalCase is universal site practice. Retrospective §5.3. Underscore-free member names; the physical-IO tag format below keeps its underscores by design.)*
  - **Physical IO tags:** `<DI/DO/AI/AO><n>_<Equipment>_<Signal>` (e.g. `DI3_FCC_RunFb`).
- C-002 *(warn)* — Block names describe function, not sequence numbers alone (`FB_ConveyorControl`, not `FB12`).
- C-003 *(warn)* — Prefixes: `FB_`/`FC_`/`DB_`/`UDT_`; instance DBs named `iDB_<FBName>_<Instance>`.
- C-004 *(error)* — An **equipment identifier list is agreed and frozen at project start** (from site drawings/P&ID where they exist, invented sensibly where they don't). Those identifiers are used verbatim in all tag, instance, and HMI names; the PLC never invents a second alias for the same equipment.
- C-005 *(error)* — Names use **letters, digits, and underscore only**, starting with a letter. No spaces or special characters — they break WinCC Unified scripting, CSV toolchains, and other PLC platforms even where TIA tolerates them. HMI-facing structures keep nesting shallow (2–3 levels) so names stay readable in alarm/event views; note WinCC Unified cannot dynamically index PLC-tag arrays from scripts — individually named tags only (consistent with C-105).
- C-006 *(error)* — **English only** — all tags, comments, block names, and HMI-facing texts. No multilingual provisioning.

## Structure

- C-101 *(warn)* — One function per network; no mega-rungs. Rule of thumb: a network should be explainable in one sentence.
- C-102 *(error)* — No jumps (JMP/LBL). No documented exceptions currently exist; any future exception must be documented here before use.
- C-103 *(warn)* — Set/Reset pairs in the same block, ideally adjacent networks. *(See also C-403 — S/R use itself is restricted.)*
- C-104 *(info)* — Standard block layout: inputs read first, outputs written once, at the end.
- C-105 *(error)* — No loops, indirect addressing, or array-index iteration in equipment control logic. Indexed access is permitted only in **documented data-handling blocks** (examples: recipe handling, comms mapping such as Modbus, queues/ordered requests). Such blocks are named to show what they are (`FB_Comms_…`, `FB_Recipe_…`) and carry a header comment stating they contain indexed access. Everything outside these fenced blocks obeys the plain-rung rule; a reader can skip the fenced blocks entirely.
- C-106 *(warn)* — Repeated equipment uses a standard FB + UDT interface, one call per equipment instance — never copy-pasted rung variants that drift apart. Sameness is a simplicity feature: same block, same shape, this instance's tags.
- C-107 *(info — soft rule)* — Edge previous-scan memory may live in a dedicated bool array (e.g. `aEdgeMem[]`) where it keeps networks tidy. Elements are **statically referenced only** (no looped/indexed access) and each element is written in exactly one place — which makes C-402 directly auditable via cross-reference on the array. Drop this rule if it ever conflicts with a stronger one.
- C-108 *(warn)* — Before writing a new block, reuse an existing site-proven block or pattern that solves the same problem. A new block for an already-solved problem needs a stated reason. (Human-side mirror of `07-pattern-library-spec.md`: proven code over fresh invention, for people and AI alike.)
- C-109 *(warn)* — **OB1 contains only calls to area Main FCs** (`FC_ComsMain`, `FC_MapIOMain`, `FC_AlarmsMain`, `FC_ControlMain`, …); no working logic directly in OB1. Each area Main calls only its own area's blocks (one `Map` FC per IO source, one alarm FC per category, etc.). One level of dispatch — the call tree reads like a table of contents. **Documented exception (2026-07-16, owner ruling — retrospective §5.4): IO mapping.** The `Map` FCs may be called directly from OB1 without an `FC_MapIOMain` wrapper — their first/last position is already pinned by C-110; wrapper FCs remain the rule for every other area.
- C-110 *(warn)* — **Input mapping is the first call in OB1; output mapping is the last.** All logic in between sees the current scan's fresh inputs, and the field receives the current scan's final decisions. Mapping mid-sequence introduces a silent one-scan latency — usually invisible, occasionally not, always undocumented.
- C-111 *(error)* — **Simulation mode** is implemented entirely at the mapping layer; logic blocks are untouched and unaware (possible because of C-304). `DB_PLC.Simulation` (see C-305):
  - gates the `FC_…InputMap` calls off via `-|/|-`, and enables `FC_Simulation`, which drives the input buffer DBs from the output buffers plus configured settings/event triggers;
  - the **output map FCs keep running**, but every physical-output write has a `-|/|-` on `DB_PLC.Simulation` between buffer and raw output, so all physical outputs **de-energize** while simulating.
  *Why:* gating the output map *call* off instead just freezes %Q at last state — the stuck-outputs failure (C-403's incident) by another door. Outputs must go to a defined safe state, not an accidental one.
- C-112 *(error)* — `DB_PLC.Simulation` active is **permanently visible on the HMI** (banner and/or standing alarm) the entire time it is true. The plant must never be silently lying to the operator.
- C-113 *(error)* — Sequence control uses one of two paradigms, **chosen consciously per application by the memory test**: *does the logic need to remember what phase it's in to know what to do next?*
  - **No** (every run condition is expressible from current signals — neighbours running, material present, enables): use **chained permissives** (C-114). Typical: conveying/flow plants.
  - **Yes** (the same inputs mean different actions depending on phase): use an **explicit stepped sequence**. Typical: batch/phased processes.
  Forcing steps onto a flow process bloats it; forcing permissives onto a phased process hides state in scattered latches (which C-403 outlaws anyway). The chosen paradigm is stated in the block header comment.
- C-114 *(error)* — Chained permissives: enables flow in **one consistent direction** (with material flow) — start-up ripples down the chain from a single conditional head. **No circular enables** (A enables B enables A = deadlock or a lie; reconvergence of parallel branches is topology, not a loop).
  *Scope:* the ban applies to the **enable chain only**. Process/safety interlocks are exempt and may feed back against flow direction (downstream-blocked stopping an upstream belt is an interlock, not an enable). Because enables are identifiable by name (C-115), the enable graph is statically checkable for cycles — a review target, human or AI, with no need to reason over belt configurations.
- C-115 *(warn)* — Every equipment FB exposes the **same handshake vocabulary** through its UDT (e.g. `enable` in; `ready`, `running` out), so the chain wires identically everywhere. Run-on/stop delays for material clearing are named TONs per C-406.
- C-116 *(error)* — Bidirectional equipment has **one enable chain per direction**, each independently satisfying C-114 (the reviewer checks one acyclic graph per mode). The direction mode is explicit and mutually exclusive — never both directions, defined behaviour when neither is selected. An equipment FB takes its enable from exactly one chain at a time, selected by the mode; a rung never mixes conditions from both chains. (A belt feeding onto a bidirectional belt belongs to whichever chain(s) its material serves — chain membership follows material routing, not just belt orientation.)
- C-117 *(error)* — Direction mode may change only when, **at minimum, all equipment in the affected section is stopped** (not-running feedback in the mode-change permissive). Whether additional conditions apply (section empty, perpendicular feeders held) is **defined per section by its material topology** and documented — an inline feeder and a perpendicular feeder have different consequences on reversal, so no blanket emptiness rule fits all layouts. No on-the-fly reversal, ever.
- C-118 *(error)* — A stepped sequence's phase is exactly one `Step : Int` tag, living inside
  the block's own caller-visible interface UDT (the same struct C-115 already puts
  `enable`/`ready`/`running` in) — never a bare private Static, never a `DB_Controls`/`DB_Settings`
  member. Nothing else stands in for "which phase are we in." A block that fails C-113's memory
  test is an FB by construction (only Static memory survives a scan), even if called once.
- C-119 *(error)* — Idle/home is always step `0` — free from Int's own default, and returned to
  explicitly (C-124) on stop, fault recovery, and restart. One physical "parked" state uses one
  step number even if reached from multiple triggers.
- C-120 *(warn)* — Steps ascend in multiples of 10, so a later revision can insert one without
  renumbering. One step is one one-sentence phase (C-101's test, applied to phases). The block
  header comment (C-201) carries a step legend — number, name, one line of meaning.
- C-121 *(error)* — Transitions are a plain Int comparison gating a `MOVE` to the target step:
  `MOVE(EN := Step = <from> AND <condition>, IN := <to>) => Step`. Never `JMP`/`LBL` (C-102, no
  exception). Not a new mechanism — the same `MOVE`-cascade `MotorStarter`'s own status-telemetry
  network already uses, applied to the register that drives the sequence instead of one that only
  reports on it. A `MOVE` that doesn't fire leaves `Step` untouched — no separate latch needed.
  Multiple possible exits from one step are mutually exclusive by construction (lower-priority exit
  explicitly excludes the higher-priority one), never arbitrated by network order.
- C-122 *(error)* — A step's own maximum dwell gets a dedicated timer, multi-instance inside
  the block's own Static section per C-407 (it belongs to this instance, not `DB_Timers`), `IN`
  gated by `Step = <that step>` — self-resets the instant the step changes. `PT` comes from a
  settings member of the owning block's own interface UDT (C-307's per-instance scope — the
  block's faceplate tunes its own sequence); `DB_Settings` holds a stage timing only when it is
  genuinely plant-wide with no single owning block. *(Reworded 2026-07-16 with C-307 —
  retrospective §5.2.)* `Q` always drives a fault (C-123), never a silent "carry on."
- C-123 *(error)* — A **hold** and a **fault** are never the same bit. A hold is live/
  non-latching (freezes the current step, no C-121 transition fires, clears itself the instant its
  own condition clears, never writes `Step`). A fault is latched, cleared only by a named
  `FaultReset`, handled by an explicit transition to a defined recovery/abort step — never a silent
  freeze. They compose (a fault is often "this hold recurred too many times") but stay two bits.
- C-124 *(error)* — On PLC restart (OB100, same block as C-403/C-305), `Step` and every other
  transient run-state (`Hold`, edge-memory, in-progress event counters) force-write back to idle,
  regardless of retentivity — mirrors C-403's own reasoning. Scoped narrowly: a genuine fault latch
  in the same UDT (`MotorDOL`'s own `FaultActive`/`FTR`/`FTS`, already `RETAIN`) is deliberately
  excluded — a fault needing human acknowledgement still needs it after a power cycle. *(This
  implies a carve-out C-403's own text doesn't currently spell out for `MotorDOL`'s own admitted
  content — worth folding back into C-403 itself later, not done here.)*
- C-125 *(warn)* — HMI exposure is satisfied by C-118's own placement plus two things that
  aren't automatic: the step legend (C-120), and any C-122 timeout's own fault bit living in the
  same interface UDT, not a private Static.
- C-126 *(warn)* — Extends C-101's "one function, one network" across a whole feature that spans
  several statements: a timer, the fault or output it drives, and the transitions that read it are
  positioned together — adjacent networks at minimum, merged into one network wherever
  `ir/SPEC.md`'s statement-kind ordering allows it without changing same-scan behavior. Never batch
  by instruction kind (a block-wide "Timers" network, a block-wide "Faults" network) separated from
  their own consumers by unrelated content in between.
  *Why:* project owner's own direct correction (2026-07-15, `FB_PusherControl`) — the site mantra
  (an electrician with a multimeter and a spanner) fails the moment understanding one function
  requires cross-referencing distant networks, even when the underlying logic is correct. Grouping
  by kind instead of by function is itself a readability bug, not a neutral choice.
  **Documented exception (2026-07-16, owner ruling — retrospective §5.1): the block-top
  HMI-time-conversion network** ("HMI Times" — one MUL+CONVERT pair per timer into `<Name>MS`
  shadows; site idiom predating this project, in `MotorDOL`/`MotorFwdRev` and the `motor-dol`
  pattern). It stays batched up front — one place to see every preset's unit conversion — on one
  condition: the network comment states the one-pair-per-timer scheme, because the IR renders the
  pairs kind-grouped and index-matched (`ir/SPEC.md`, statement-kind ordering) and the comment is
  what keeps that readable in one attempt.
- C-127 *(error)* — A reusable equipment FB never references another specific instance by name
  inside its own logic — no hardcoded `iDB_<Other>` access, no `CALL` to a sibling equipment's own
  instance. Everything an FB needs comes in through its own UDT interface (C-115), wired by its
  caller; the FB itself never knows what else exists in the project. Deciding what wires to what
  across multiple named instances is an orchestrating FC's job (`FC_ControlMain`, per C-109), never
  the reusable FB's own.
  *Why:* project owner's own direct correction (2026-07-15) — a hardcoded instance reference makes
  the FB usable exactly once, silently defeating C-106's whole reuse premise, and hides a real
  dependency on another piece of equipment inside logic instead of in the one place (the calling
  FC) a reader would actually look for it.

## Commenting

- C-201 *(error)* — Every network has a title; every block has a header comment (purpose, author, revision).
- C-202 *(warn)* — Comments say *why*, not what (the rungs already say what).
- C-203 *(warn)* — **Titles are short; comments carry the detail.** A block or network title is a
  short description — what this is, in a phrase. The detailed explanation (the *why*, the
  justification, the scheme) lives in the corresponding comment, never crammed into the title.
  *(Owner ruling, 2026-07-16 — retrospective C-606/C-607 notes, generalized. C-606/C-607 point
  their justification text here.)*

## Data

- C-301 *(error)* — No absolute addressing (%M, %DBx.DBWy) in logic; symbolic access only. **Documented exception:** slice access (`.%Xn`, `.%Bn`, …) is permitted in encode/decode contexts — alarm words (per C-501's conditions), comms mapping, and data-handling blocks (C-105) — never in equipment control logic.
- C-302 *(warn)* — UDTs for repeated equipment structures; no parallel loose-tag families (three conveyors as `FCC_Run`/`BC1_Run`/`BC2_Run` flat-tag copies silently diverge — one `UDT_Conveyor`, three instances). Data-side counterpart of C-106.
- C-303 *(error)* — Optimized block access on unless a comms interface requires otherwise.
- C-304 *(error)* — **Control logic never reads physical inputs or writes physical outputs directly** — applies to all IO addressing, local and remote/Profinet alike. All physical IO passes through buffer DBs, **one buffer pair per IO source**: local PLC IO via `DB_Inputs`/`DB_Outputs` (mapped by `FC_InputMap`/`FC_OutputMap`), remote nodes via their own (`DB_Rem0Inputs`/`FC_Rem0InputMap`, …); analog IO in its own dedicated DBs. Mapping happens only in the `Map` FCs (called per C-109's mapping exception / C-110's ordering).
  *Why:* logic that only touches buffer DBs is trivially simulatable (S9) and portable — swapping hardware or vendor changes the mapping layer, never the logic.
- C-305 *(warn)* — Every project has a **`DB_PLC`** holding PLC-specific system data (system time, misc. tracked state — contents vary per project; the DB and name do not). It always contains `Simulation : Bool`, start value `FALSE`. `DB_PLC.Simulation` is **force-reset in the OB100 startup block** (the same block as C-403), regardless of retentivity — simulation mode must never survive a power cycle; start values only apply at download, not restart.
- C-306 *(warn)* — Operator/system commands live in **`DB_Controls`**: system-level commands (typically `SystemStart`, `SystemStop`, `SystemReset` — names may vary per project) and mode selections such as direction modes (C-116). This is the HMI/operator command surface; equipment FBs consume from it, logic-internal state does not live in it.
- C-307 *(warn)* — Plant/system-level parameters live in **`DB_Settings`** — retentive by default; non-retentive members are documented exceptions. Scope split *(sharpened 2026-07-16, owner ruling — retrospective §5.2)*: **a setting owned by a single equipment instance lives in that instance's UDT** (HMI↔PLC per C-503 — faceplates bind the UDT instance, and nearly all equipment gets a faceplate, so per-instance settings must sit where the faceplate's settings page can reach them; this includes a sequencing block's own step timings). `DB_Settings` holds only what **no single faceplate owns** — genuinely plant-wide setpoints and parameters. Commissioning defaults per C-309 then live as the owning iDB's start values. This is the home of C-403's "documented settings/parameters" exemption: settings are the one legitimately-retentive category, which is why they're exempt from the startup reset (a startup-reset generator may treat a UDT's settings members as excluded by construction).
- C-308 *(error)* — **A settings member has exactly one writer: the HMI.** `DB_Settings` is written by the HMI/operator side only; PLC logic never writes it (read-only from logic). *(Extended 2026-07-16 — retrospective §5.2:)* the same applies to settings members inside an instance UDT (C-307's per-instance scope) — logic never writes them, **and orchestrating FCs never scan-copy values into them**: a cyclic `MOVE` from `DB_Settings` over a faceplate-written UDT member silently reverts every HMI edit one scan later (the GenProject1 `FC_ControlMain` trap — the setting *exists twice* with a copy in between, the worst of both homes).
  *Why:* a logic bug that writes a retentive setting silently re-tunes the plant and *persists across restarts* — the retentive cousin of the stuck-output failure. One-writer makes the whole class impossible and is statically checkable (cross-reference shows no logic writes).
- C-309 *(info)* — Settings are **not range-validated PLC-side**, by site policy: protection is HMI-side (password-protected settings screens), and operator misconfiguration is a chargeable fix. Reviewers (human or AI) should not flag missing clamps as findings. `DB_Settings` start values are maintained as the **commissioning defaults** — downloading the DB is the de facto factory reset.

## Instructions

- C-401 *(error)* — **No counter instructions** — neither IEC (CTU/CTD/CTUD) nor legacy. Counting is done with integer types (`Int`/`UInt`/`DInt`/`UDInt`) incremented explicitly (ADD/INC) on a constructed edge (C-404).
  *Why:* simpler to read (the count is a plain variable, visible anywhere), one less instruction black box, and portable to every PLC platform (C-405).
- C-402 *(error)* — Edge memory bits are dedicated, never reused. *(C-107's one-write-per-element array is the recommended implementation.)*
- C-403 *(error)* — Outputs and state bits are driven by **plain coils** (rung true → coil on) wherever possible. Set/Reset — including S/R coils, SR/RS flip-flop boxes, and `SET_BF`/`RESET_BF` — is used only where genuinely unavoidable. Every bit written by any S/R mechanism (except documented settings/parameters) must also appear in the **dedicated startup-reset block**: one block that resets all such bits, executed **once at PLC startup** (OB100 or equivalent — implementation doesn't matter, but it must never run cyclically). This applies **regardless of retentivity** — retentivity is sometimes incidental (e.g. a bit inside a retentive UDT), so non-retentivity is never relied on to clear state.
  *Why:* site incident — PLC power-cycled and S/R-driven outputs stuck on because their reset conditions never occurred. Plain coils fail safe by construction; S/R state must be explicitly cleared at startup.
- C-404 *(error)* — Never use the built-in `-|P|-` / `-|N|-` (or equivalent) edge instructions. Edge detection is constructed explicitly: compare the signal against its stored previous-scan bit, act, then update the stored bit (storage per C-107/C-402).
  *Why:* (1) site incident — a built-in edge held true for multiple scans in front of an increment, corrupting a count; (2) the hidden edge-memory operand is easy to duplicate and invisible in casual reading; (3) the explicit construction costs nothing extra; (4) `-|P|-`/`-|N|-` are not available on all PLC platforms — the explicit form is portable.
- C-405 *(warn)* — Prefer basic constructions with direct equivalents on other PLC platforms over vendor-specific convenience instructions, where the basic form is not materially worse. (Generalization of the portability argument in C-403/C-404; supports the project's vendor-neutral goal.)
- C-406 *(error)* — **TON is the only timer instruction used.** Off-delay, pulse, and retentive behaviour are constructed explicitly from TON plus inversion/edge logic (per C-404) rather than using TOF, TP, or TONR.
  *Why:* passed-on site wisdom — one timer type means every timed rung reads the same way; TOF's run-while-input-false behaviour reads backwards at the cabinet; TONR is Siemens-specific (no IEC equivalent — C-405).
  *Action item:* build a site-standard, cross-platform retentive-timer FB to cover the old TONR use cases (runtime counters, duty totals).
- C-407 *(warn)* — Timer instances inside reusable equipment FBs are **multi-instance** (they live in that equipment's iDB). All standalone timers outside equipment FBs live in the single shared **`DB_Timers`**, individually named — one audit surface, no scattered timer iDBs.
- C-408 *(error)* — `ET` is never compared against a constant to produce a boolean trigger. Each time threshold is its own named timer, activated via `Q`; staged sequences **chain timers** (Q of stage n enables stage n+1's timer), so each preset is the gap between stages and each stage has a name. Reading `ET` as a *value* (HMI display, diagnostics, proportional/analog use) is permitted — copy it to a named variable rather than wiring `ET` mid-rung.
- C-409 *(error)* — **A time interval is measured in exactly one place.** If a sequence is chained (A→B→C→D), no separate timer may duplicate a cumulative span of that chain (e.g. a standalone A→D timer) — the total exists only as the chain itself. A genuinely needed cumulative value (display, logging) is derived from the chain's members, not re-timed.
  *Why:* a parallel timer's preset silently desynchronizes from the chain the moment any stage preset is tuned — two disagreeing definitions of the same moment.
  *Scope:* within a block. Cross-block, an apparently duplicate timer is usually an independent fact and is fine; if another block's timing genuinely must track the chain, that logic belongs in the same block or consumes an exported done-bit — related timers live together.

## Alarms

- C-501 *(warn)* — All category alarms live in **`DB_Alarms`**, packed into Words per category, named `<Category>Alarm0`, `<Category>Alarm1`, … extending by Word as counts exceed 16.
  *Why:* WinCC Unified discrete alarms trigger cleanly off Word tags (Ints misbehave), and Words group related alarms meaningfully for HMI and comms mapping.
  Alarm bits are written via slice access (`DB_Alarms.EStopAlarm0.%X3`) — a **documented exception to C-301**, on two conditions: exactly one alarm bit per network, and the network title states the alarm text (matching the HMI alarm text), so the rung is read by its title, never by decoding `%Xn`.
- C-502 *(warn)* — `FC_AlarmsMain` (called from OB1 per C-109) calls one monitoring FC per monitored function/category. Categories vary per project, but `FC_GeneralAlarms` (catch-all) and `FC_EStopAlarms` always exist.
- C-503 *(warn)* — Repeatable equipment (motors, VSDs, …) monitors its own alarms **inside its FB, per instance**. Alarm and faceplate data reaches the HMI through the equipment's UDT interface — the UDT carries everything the HMI needs; category words in `DB_Alarms` are not duplicated per instance.
- C-504 *(error)* — **One alarm rung monitors one fault, and one root cause raises one alarm.** Where a fault is a known *consequence* of another alarm (e.g. E-Stop drops a drive's Ready), the consequence alarm is suppressed by a `-|/|-` on the causal alarm bit, extended by a short TON (per C-406) so slow-recovering equipment doesn't flash consequence alarms while the cause resets. Suppression lasts only for the causal alarm's duration (+ delay) — a genuine second fault that persists beyond it still alarms.
  *Why:* site incident — E-Stop presses flooded the alarm list with "Not Ready" echoes, burying the actual cause. Same principle as C-409: one fact, one place.
- C-505 *(warn)* — Alarm text format: **`<Equipment> — <fault> — <action hint>`**. Equipment identifier per C-004, English per C-006.
- C-506 *(warn)* — Fixed three-class severity taxonomy, assigned by the **operator-action test** (not perceived badness): **Fault** (equipment stopped/stopping; intervention before restart), **Warning** (running degraded/approaching limit; action soon), **Info/Event** (record only). Per-alarm class assignment happens in the project alarm list at kickoff (alongside the C-004 equipment list); the classes and their HMI presentation (colours, filtering) never vary between projects — fixed container, variable contents, same move as C-305.
- C-507 *(warn)* — Alarms **self-clear when their condition clears; no acknowledgment required** — except documented per-alarm exceptions (expected to be Class 1 faults where proof a human saw it matters, e.g. E-Stop events; each exception is documented individually regardless).

## Simplicity & readability

Adopted 2026-07-16 from the GenProject1 retrospective (`docs/notes/genproject1-retrospective.md`),
owner-reviewed rule by rule. These are the written form of the priority order's tier 2 and the
stricter-bar principle in the preamble — the rules a simplicity reviewer cites.

- C-601 *(warn)* — **Name a condition used twice.** A compound condition (≥3 terms) consumed by
  more than one network is written once to a named bit (`StopCmd`, `CycleStartOk`) and read by
  name — never duplicated inline. One write, many reads; the name is the documentation; near-match
  divergence (two copies differing by one term, invisible at a glance) becomes impossible.
  *Why:* GenProject1's 7-term cycle-start condition duplicated across two networks differing only
  in an `OR FaultReset` tail (retrospective F-1) — while the same build's own `StopCmd` shows the
  rule done right.
- C-602 *(warn)* — **One-sentence rungs.** C-101's test applied to a single coil: if one coil's
  expression can't be explained in one sentence, split it into named intermediate bits or
  restructure. Soft guide (not a hard limit): >2 OR-branches or >6 contacts in one expression is
  the point to justify. **Documented exception (owner, 2026-07-16): one condition fanning out to
  several coils within a single network** (e.g. interface coils) may exceed the guide rather than
  force an intermediate bit — splitting there would create the duplication C-601 prevents. Other
  exceptions state their reason in the network comment.
- C-603 *(warn)* — **Step membership is enumerated, not ranged.** Conditions over a stepped
  sequence's phase enumerate the steps they mean (`Step = 30 OR Step = 40 OR Step = 50`);
  ordered-range predicates (`>=`, `<=`, spans) only where "every future step inserted in this span
  belongs here too" is the stated intent (comment). `Step <> 0` and `Step = n` are always fine
  (C-119 fixes idle = 0).
  *Why:* C-120's insert-without-renumbering interacts with ranges silently — an inserted step 45
  joins `>= 30 AND <= 50` with no visible decision.
- C-604 *(error)* — **Constants and placeholders are visually distinct.** A genuinely-constant
  block input ("not fitted", "always permitted") is wired from a named, documented source — a
  `Fitted`/mode setting, or a commented constant — stating *why* it's constant. The
  `NOT AlwaysTrue` idiom **with a comment naming the gap** is reserved for known-unbuilt
  placeholders. A bare, uncommented `AlwaysTrue`-derived constant is a finding: the reader can't
  tell design from debt, and the loud-TODO idiom only stays loud if it's *only* used for TODOs.
- C-605 *(error)* — **Interface members carry comments.** Every member of an equipment FB's
  interface UDT (the C-115/C-125/C-503 HMI-facing surface) carries a one-line member comment —
  role, units where numeric, and for settings the C-307 scope. *(Severity set to error by owner,
  2026-07-16: "this should always be true.")*
- C-606 *(warn)* — **Justify size against the requirement.** A block whose behavior exceeds the
  requirement's literal ask carries, in its header **comment** (title stays short, per C-203), one
  line per extra feature saying why it exists. Unrequested capability is a complexity cost like
  any other; the functional review (docs/15) flags logic that traces to no requirement, and this
  rule puts the justification where a reader meets the block.
- C-607 *(warn)* — **One problem, one policy per project.** When two blocks in one project solve
  the same recurring problem (settings access, edge storage, time conversion), they solve it the
  same way; a deviation states its reason in the deviating block's header **comment** (C-203).
  *Why:* C-106's "sameness is a simplicity feature," applied to design idioms — GenProject1's two
  sibling FBs solved settings access two different ways (retrospective F-5, resolved by
  C-307/C-308's 2026-07-16 sharpening).

## To fill in (owner: Oisin)

Restart/first-scan behaviour beyond C-403/C-305 (full OB100 contents) · reserved OB usage · alarm ack exception list template.
