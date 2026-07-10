# 06 — LAD Conventions & Style Guide

**Status: POPULATED — all rules below are confirmed site conventions (sessions of 2026-07-09) unless marked otherwise. Remaining gaps are listed in "To fill in".**

Every rule gets an ID (`C-xxx`) so review findings can cite it, plus a severity: **error** (must fix), **warn** (should fix), **info** (style).

Site mantra: **simple, simple, simple** — the test for control logic is that an electrician with a multimeter and a spanner can look at a rung and get a rough idea of what's going on. Reuse is achieved at the *block* level (standard FBs, UDTs, the pattern library), never through cleverness inside rungs.

## Naming

- C-001 *(error)* — Tag naming is layered:
  - **Blocks/types:** `FB_`/`FC_`/`DB_`/`UDT_` prefix + PascalCase function name (`FB_ConveyorControl`, `UDT_Motor`).
  - **Equipment instances:** the frozen equipment identifier (C-004) verbatim (`iDB_Motor_FCC`).
  - **Variables/UDT members:** short camelCase (`run`, `fltHigh`, `posOk`) — context comes from the structure, so short names are correct here. Long names are only needed where the name is the *only* context.
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
- C-109 *(warn)* — **OB1 contains only calls to area Main FCs** (`FC_ComsMain`, `FC_MapIOMain`, `FC_AlarmsMain`, `FC_ControlMain`, …); no working logic directly in OB1. Each area Main calls only its own area's blocks (one `Map` FC per IO source, one alarm FC per category, etc.). One level of dispatch — the call tree reads like a table of contents.
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

## Commenting

- C-201 *(error)* — Every network has a title; every block has a header comment (purpose, author, revision).
- C-202 *(warn)* — Comments say *why*, not what (the rungs already say what).

## Data

- C-301 *(error)* — No absolute addressing (%M, %DBx.DBWy) in logic; symbolic access only. **Documented exception:** slice access (`.%Xn`, `.%Bn`, …) is permitted in encode/decode contexts — alarm words (per C-501's conditions), comms mapping, and data-handling blocks (C-105) — never in equipment control logic.
- C-302 *(warn)* — UDTs for repeated equipment structures; no parallel loose-tag families (three conveyors as `FCC_Run`/`BC1_Run`/`BC2_Run` flat-tag copies silently diverge — one `UDT_Conveyor`, three instances). Data-side counterpart of C-106.
- C-303 *(error)* — Optimized block access on unless a comms interface requires otherwise.
- C-304 *(error)* — **Control logic never reads physical inputs or writes physical outputs directly** — applies to all IO addressing, local and remote/Profinet alike. All physical IO passes through buffer DBs, **one buffer pair per IO source**: local PLC IO via `DB_Inputs`/`DB_Outputs` (mapped by `FC_InputMap`/`FC_OutputMap`), remote nodes via their own (`DB_Rem0Inputs`/`FC_Rem0InputMap`, …); analog IO in its own dedicated DBs. Mapping happens only in the `Map` FCs under `FC_MapIOMain`, ordered per C-110.
  *Why:* logic that only touches buffer DBs is trivially simulatable (S9) and portable — swapping hardware or vendor changes the mapping layer, never the logic.
- C-305 *(warn)* — Every project has a **`DB_PLC`** holding PLC-specific system data (system time, misc. tracked state — contents vary per project; the DB and name do not). It always contains `Simulation : Bool`, start value `FALSE`. `DB_PLC.Simulation` is **force-reset in the OB100 startup block** (the same block as C-403), regardless of retentivity — simulation mode must never survive a power cycle; start values only apply at download, not restart.
- C-306 *(warn)* — Operator/system commands live in **`DB_Controls`**: system-level commands (typically `SystemStart`, `SystemStop`, `SystemReset` — names may vary per project) and mode selections such as direction modes (C-116). This is the HMI/operator command surface; equipment FBs consume from it, logic-internal state does not live in it.
- C-307 *(warn)* — Plant/system-level parameters live in **`DB_Settings`** — retentive by default; non-retentive members are documented exceptions. Scope split: a setting owned by a single equipment instance lives in that instance's UDT (HMI↔PLC per C-503); `DB_Settings` holds what no single instance owns — prestart parameters, stage/step timings (when the C-113 stepped branch is in use), plant-wide setpoints. This is the home of C-403's "documented settings/parameters" exemption: settings are the one legitimately-retentive category, which is why they're exempt from the startup reset.
- C-308 *(error)* — `DB_Settings` is written by the **HMI/operator side only; PLC logic never writes it** (read-only from logic).
  *Why:* a logic bug that writes a retentive setting silently re-tunes the plant and *persists across restarts* — the retentive cousin of the stuck-output failure. Read-only-from-logic makes the whole class impossible and is statically checkable (cross-reference shows no logic writes).
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

## To fill in (owner: Oisin)

Stepped-sequence style rules (step representation, transitions, fault behaviour, step timeouts, restart, HMI exposure — the C-113 "yes" branch) · restart/first-scan behaviour beyond C-403/C-305 (full OB100 contents) · reserved OB usage · alarm ack exception list template.
