# Conventions review — gen/PlantAutoControl-bench/PlantAutoControl.ir (2026-07-20)

Blindness: **Blind.** I did not author or restructure this block earlier in this session; no author
rationale or prior review of this content is in my context. Two expected non-contaminating caveats
apply as designed: I ran `converter review`/`converter cross-check` (the mechanical half of this
review), and I read doc 06's rule rationales (which cite this project's own historical corpus) —
every AI-pass finding below is re-derived from the IR itself, not from a rationale summary. I did
**not** open anything under `docs/evidence/` (answer-key quarantine); this review stands on doc 06
alone.

Tool: `./src/converter/Converter/bin/Release/net8.0/converter.exe`
Invocations:
- `converter.exe review gen/PlantAutoControl-bench/PlantAutoControl.ir --project ir/PlantAutoControl-bench/ --ignore-errors`
- `converter.exe cross-check --project ir/PlantAutoControl-bench/` (FI-22 facts)

Blocks reviewed: `PlantAutoControl` (FC 100) — **generated** (Author: lad-coder, gen-block-new,
Rev 1). Cross-block context read from the `ir/PlantAutoControl-bench/` export (interface UDTs, boundary
DBs, `ProcessTimings`, `PlantControl`, `HMIControlSignals`) — all **imported-real / given
boundary**.

**Scope note on cross-check:** `converter cross-check` reads the committed `ir/PlantAutoControl-bench/`
export. Its DEAD-MEMBERS table reports *every* `DiscreteInputs.*` / `DiscreteOutputs.*` member as
"unused (no writer, no reader)" — because at the time it parsed the graph the new orchestrator's
reads/writes were not represented in the reference-graph it built (the block is the generation
artifact under review, not yet a settled part of the as-built export). I therefore did **not** rely
on that dead-member table for the new block; the Group-2 facts about `PlantAutoControl` below are
hand-derived by grep against both `ir/` and `gen/`. (An identical copy of `PlantAutoControl.ir`
exists in `ir/PlantAutoControl-bench/`; the two are byte-for-byte the same on every line I checked.)

---

## Mechanical findings (converter review — verbatim, not re-derived)

```
FILE: gen/PlantAutoControl-bench/PlantAutoControl.ir
  BLOCK: PlantAutoControl

  C-001: checked, clean
  C-003: checked, 1 finding(s)
  C-005: checked, clean
  C-103: checked, clean
  C-118: checked, clean
  C-121: checked, clean
  C-119: checked, clean
  C-120: checked, clean
  C-122: checked, clean
  C-125: checked, clean
  C-201: checked, clean
  C-301: checked, clean
  C-501: checked, clean
  C-406: checked, clean
  C-408: checked, clean
  C-102: checked, vacuous (cannot fire against current IR capability)
  C-401: checked, vacuous (cannot fire against current IR capability)
  C-404: checked, vacuous (cannot fire against current IR capability)

  [Warn] C-003 (block level)
    Block name 'PlantAutoControl' does not start with the required 'FC_' prefix for a FC.
    fix: Rename to 'FC_PlantAutoControl' (coordinate with any callers/references first).

SUMMARY: 1 file(s), 1 finding(s) (0 error, 1 warn)
```

Vacuous rules (C-102/C-401/C-404) are reported exactly as the tool states them — they cannot fire
against current IR capability and are **not** upgraded to "verified clean." C-003 is the tool's one
finding and is genuine (see AI findings — surfaced, expected).

---

## AI findings

### PlantAutoControl (generated)

- **[C-308, error, bucket B] Networks 3, 4, 19 — `MOVE(EN := TRUE, IN := ProcessTimings.NormalFanStartTime) => FilterUnitInstN.IO.EnableUPSTime`.**
  A cyclic (unconditional `EN := TRUE`, every scan) scan-copy from a plant-wide settings member
  into a per-instance settings member. `IO.EnableUPSTime : Real` is the FilterUnitSystem's own UPS-enable
  delay preset (read by `FilterUnitSystem` N6 `MUL … IN1 := IO.EnableUPSTime` → `EnableUPSTimeMS` →
  the `EnableUpstreamTimer` PT), i.e. a per-instance, faceplate-tunable setting in C-307/C-122
  scope; `ProcessTimings.NormalFanStartTime : Real RETAIN` is a plant-wide setting. This is the
  exact shape doc 06 C-308 names as the trap: "the setting exists twice with a copy in between, the
  worst of both homes … a cyclic MOVE from DB_Settings over a faceplate-written UDT member silently
  reverts every HMI edit one scan later." Any operator edit to a filter unit's own `EnableUPSTime`
  faceplate value is overwritten on the next scan.
  Evidence: N3 `MOVE(EN := TRUE, IN := ProcessTimings.NormalFanStartTime) => FilterUnitInst2.IO.EnableUPSTime`;
  identical into `FilterUnitInst3.IO.EnableUPSTime` (N4) and `FilterUnitInst1.IO.EnableUPSTime` (N19).
  Rule text basis: C-308 (error) — "the same applies to settings members inside an instance UDT
  (C-307's per-instance scope) — logic never writes them, **and orchestrating FCs never scan-copy
  values into them**."
  Design tension for owner (below): the underlying question is *where fan-time belongs* — genuinely
  plant-wide (then the FB's PT should read `ProcessTimings` directly and the per-instance member
  should not exist / not be copied) or per-instance (then it is faceplate-owned and must not be
  sourced from `ProcessTimings` at all). The current hybrid is the C-308 violation.
  Suggested fix: pick one home per the owner ruling; if plant-wide, drive the timer preset from
  `ProcessTimings.NormalFanStartTime` without a per-instance duplicate; if per-instance, drop the
  MOVE and let the faceplate own `EnableUPSTime` (commissioning default as the iDB start value).
  *(The block's own network comments say "see block header re C-308" but the header carries no C-308
  discussion — the justification pointer is dangling, so there is no recorded owner acceptance.)*

- **[C-403, error, bucket B — project/generated-integration scope] Network 2 — `SCOIL PlantControl.AutoPreStart` with no dedicated startup-reset block anywhere in the project.**
  The block introduces a new Set/Reset-driven bit (`SCOIL PlantControl.AutoPreStart` set on the
  auto-pre-start edge, `RCOIL` reset by `DiscreteOutputs.RunPreStart`). C-403 requires every
  S/R-written bit (except documented settings) to also appear in a dedicated startup-reset block
  executed once at PLC startup (OB100 or equivalent), regardless of retentivity. No OB/startup/init/
  reset block exists in `ir/PlantAutoControl-bench/` or `gen/PlantAutoControl-bench/` (verified: no block name
  matches, no `ROOTID` startup block). Per the review-conventions absence rule, a project that
  contains S/R-written bits but no OB100 machinery gets this filed as a real generated-integration
  finding — the imported-real FBs' own internal S/R usage stays context, but the reset block the new
  S/R write requires is genuinely absent. The same OB100 gap also leaves the block's edge-memory
  `PlantControl.PositiveEdgeArray[4]` unforced at restart (C-124 force-to-idle family).
  Evidence: N2 `SCOIL PlantControl.AutoPreStart := MotorStarterInst4.IO.AutoStartSignal AND NOT PlantControl.PositiveEdgeArray[4]` / `RCOIL PlantControl.AutoPreStart := DiscreteOutputs.RunPreStart`; no startup block found.
  Rule text basis: C-403 (error) — "Every bit written by any S/R mechanism (except documented
  settings/parameters) must also appear in the dedicated startup-reset block … executed once at PLC
  startup … regardless of retentivity."
  Suggested fix: add/extend an OB100-class startup-reset block that force-resets `PlantControl.AutoPreStart`
  (and forces `PlantControl.PositiveEdgeArray[4]` and other transient run-state to idle) at startup.

- **[C-005, error, bucket B — context, given boundary — NOT a defect in this block] Boundary DB member `DiscreteInputs.CycloneDustAutoRunning/Stop` contains a `/`.**
  The `/` breaks C-005 (letters/digits/underscore only) and makes the tag un-referenceable from IR.
  This lives in the given input-buffer DB (`Input.ir`), not in `PlantAutoControl`. The block handles
  it **correctly**: Network 19 deliberately leaves `FilterUnitInst1.IO.RunningFB` unwired and states
  why in its comment. Reported here as a real convention breach in the fixed boundary (for the owner
  to rename at source) plus a downstream functional consequence (see Tier-1), not as a fix demand on
  the generated block.
  Rule text basis: C-005 (error) — "Names use letters, digits, and underscore only … No spaces or
  special characters."

- **[C-104, info, bucket C — judgment] All 20 networks write the physical-output buffer *before* the `CALL` that produces the value.**
  E.g. N2: `COIL DiscreteOutputs.HFLCStart := MotorStarterInst4.IO.Run` precedes `CALL MotorStarter(...)`,
  so the mapped-out start command reflects the previous scan's `IO.Run` (one-scan lag). C-104's
  "outputs written once, at the end" is honoured in position but the value read is one scan stale
  because the producing CALL runs after. Judgment: negligible on an S7-1200 (one scan, ms) and
  consistent across every network, but a stricter reading prefers CALL-then-map ordering. Low
  priority.
  Rule text basis: C-104 (info) — "Standard block layout: inputs read first, outputs written once,
  at the end."

**Not duplicated:** C-003 (FC_ prefix) is the tool's finding, embedded verbatim above — genuine and
expected (see Judgment items: deliberate regeneration-naming ruling per the dispatch brief).

---

## Cross-block tables

**C-308 settings-write table (hand-derived for the new block; scan-copies into per-instance settings)**

| Target (per-instance setting) | Writer | Network | Source | Cyclic? | Verdict |
|---|---|---|---|---|---|
| `FilterUnitInst2.IO.EnableUPSTime` | PlantAutoControl | 3 | `ProcessTimings.NormalFanStartTime` | yes (`EN := TRUE`) | **C-308 violation** |
| `FilterUnitInst3.IO.EnableUPSTime` | PlantAutoControl | 4 | `ProcessTimings.NormalFanStartTime` | yes | **C-308 violation** |
| `FilterUnitInst1.IO.EnableUPSTime` | PlantAutoControl | 19 | `ProcessTimings.NormalFanStartTime` | yes | **C-308 violation** |

No other logic writes to `DB_Settings`-class members or instance-UDT settings members were found in
the block. `PlantControl.MagEnable`/`GeneralEnable`/`DrumsEnable`/`FansShutdownReady`/etc. are read
only (plant flags, not settings).

**C-127 sibling-reference check** — n/a for this block. `PlantAutoControl` is the **orchestrating
FC** (the `FC_ControlMain`-role block), not a reusable equipment FB. Its many `iDB_*`-instance
references (`MotorVSDInst2.IO.UPSEnable`, `MotorStarterInst4.IO.ShutdownComplete`, …) and per-instance
`CALL`s are exactly the cross-instance wiring C-109/C-127 assign to an orchestrating FC ("Deciding
what wires to what across multiple named instances is an orchestrating FC's job"). **No C-127
finding.**

**C-304 physical-IO boundary** — clean. The block reads only the `DiscreteInputs` buffer DB and
writes only the `DiscreteOutputs` buffer DB; grep for raw `%I`/`%Q` returns nothing. Buffer-only
logic per C-304; mapping is elsewhere (not this block).

**Edge / S-R single-writer check (C-402 / C-103)** — clean.
`PlantControl.PositiveEdgeArray[4]` has exactly one writer (N2 L38), literal index, dedicated to the
`AutoPreStart` edge — C-402/C-107 satisfied. `PlantControl.AutoPreStart` set and reset are adjacent
in the same network (N2) — C-103 satisfied. (The startup-reset obligation is the separate C-403
finding above, not a C-103/C-402 issue.)

**C-406 timer homes / instruction check** — n/a in this block (no timer instructions; the three
`MOVE(EN := TRUE, …)` are value copies). Tool C-406 clean.

---

## Judgment items for owner ruling

- **C-113 paradigm (supporting the stated choice).** The header declares C-113 "no" — combinational
  chained-permissive, no phase memory in this block. Applying the memory test to *this* block: every
  run/hold decision is expressible from current signals (`PlantControl.Status`, neighbour
  `UPSEnable`/`ShutdownComplete`, plant enables); phase memory lives in the plant sequencer
  (`PlantControl.Status`) and inside the equipment FBs. The combinational choice is defensible and
  correctly stated. No finding — reasoning recorded for the owner.

- **C-003 FC_ prefix (tool finding — deliberate ruling per brief).** `PlantAutoControl` lacks the
  `FC_` prefix C-003 requires. The dispatch brief records this as a deliberate regeneration-naming
  ruling. Surfaced because it is real (the tool flags it); not adjudicated here — owner confirms
  whether the regeneration-naming exception stands or a rename to `FC_PlantAutoControl` (coordinating
  callers) is wanted.

- **C-308 fan-time home (the design decision behind the error above).** Owner ruling needed on
  whether the dust-collector fan up-to-speed time is genuinely plant-wide (one `ProcessTimings`
  value, no per-instance faceplate ownership) or per-instance. Whichever is chosen dictates the fix;
  the current copy-in-between shape is unavailable under C-308 either way.

- **C-502 alarm skeleton (scale-down question — out of single-block scope).** Whether the project
  instantiates the `FC_AlarmsMain` / `FC_GeneralAlarms` / `FC_EStopAlarms` skeleton (C-502 says they
  "always exist," no scale-down exception) is not visible from this one-block scope and is not this
  block's concern. Flagged as an owner scale-down question for the project, not a finding on
  `PlantAutoControl`.

---

## Clean declarations

- **PlantAutoControl** — rules checked and clean (against doc 06, generated bar):
  C-001 (member/var naming, no underscores), C-005 (own tags; the `/` is boundary-side),
  C-006 (English only), C-102 (no JMP/LBL — vacuous), C-103 (S/R pairing adjacent),
  C-105 (no iterative/indexed access — only literal `[4]`), C-127 (orchestrator, not a reusable FB),
  C-201 (title + rich header content: purpose, paradigm, author, rev, date), C-301 (symbolic only),
  C-304 (buffer-DB only, no raw IO), C-401/C-404 (no counters/edge instructions — vacuous),
  C-402/C-107 (edge bit single-writer, dedicated), C-406/C-408 (no timers, no `ET`-as-trigger).

---

## Belongs to review-simplicity (routed, not swept here)

- **C-604 (error)** — `IO.SystemHealthy := TRUE` is wired as a bare, uncommented literal `TRUE` in
  every equipment network (N1, N2, N3, …). A permanent-constant / placeholder input wants a named,
  documented source (or the `placeholder_<Var>` form) stating why it is constant. Route to
  /review-simplicity.
- **C-601 / C-602 (warn)** — the plant-status permissive
  (`PlantControl.Status >= 1 OR <hold-cond> AND PlantControl.Status = -1`) is a compound condition
  duplicated between each network's `AutoStartSignal` and its `Shutdown` (as `NOT(<same expr>)`),
  and the `AutoStartSignal`/`Shutdown` expressions run long (multiple OR-branches, many contacts).
  One-sentence-rung / name-a-condition-twice observations belong to /review-simplicity.

*(No C-1xx/2xx/6xx sweeps performed here — citation split respected.)*

---

## Tier-1 candidates for the functional review (evidence, no ruling)

- **Cyclone dust filter (FilterUnitInst1) has no running feedback wired.** Network 19 omits
  `FilterUnitInst1.IO.RunningFB` because its source tag `DiscreteInputs.CycloneDustAutoRunning/Stop`
  is un-referenceable (the `/`). Consequence in `FilterUnitSystem`: `IO.RunningFB` feeds
  fail-to-run (N8), fail-to-stop (N9) and the upstream-enable timer (N11) — with it stuck FALSE, that
  instance can never confirm running / raise FTR-vs-FTS correctly / assert `UPSEnable`. Documented in
  the network comment, but the functional impact should be traced against REQ-019/020.
- **`SystemHealthy` is hardwired TRUE for every machine** — no health/permissive interlock is
  actually applied (the boundary carries a `DiscreteInputs.ControlHealthy` that is currently dead).
  Trace whether a requirement expects a real system-healthy gate here.
- **One-scan latency on all start outputs** (see C-104 above) — flagged for completeness; likely
  benign.

---

## Not checkable here (standing declines + scope limits)

- **C-112 (error)** — simulation-active HMI banner: needs the WinCC artifact; structurally out of
  this pipeline's reach. An error-severity rule this review can never validate.
- **C-303 (error)** — optimized-vs-standard block access is a TIA block property the IR medium does
  not carry.
- **C-506 (warn)** — severity-class assignment needs the project alarm list.
- **HMI-side halves** of C-503 (faceplate binding), C-505 (actual alarm texts), C-125 (HMI display),
  C-307 (settings pages): not in this medium.
- **C-004 compliance half** — needs the frozen equipment-identifier list; only *consistency* is
  checkable and the instance identifiers used (`MotorVSDInst2`, `MotorStarterInst4`, …) are applied
  consistently within the block.
- **C-114 material-flow half** — needs a process-topology artifact; the enable/interlock wiring in
  this block is orchestrator-level and not a chained-permissive cycle within the block.
- **Group-2 whole-project rules at reduced trust** — the cross-check reference graph did not include
  the new orchestrator (see scope note), so C-308/C-403 above were hand-derived; other whole-project
  rules (C-502 alarm skeleton, C-109/C-110 OB1 shape) are noted as owner/project questions rather
  than silently passed.

---

## Verdict

**Does not clear the conventions bar as-is (generated, stricter bar).** Two error-severity items must
be resolved before presentation:

1. **C-308** — cyclic scan-copy of `ProcessTimings.NormalFanStartTime` into the per-instance
   `FilterUnitInstN.IO.EnableUPSTime` settings member (Networks 3, 4, 19). *This is the C-308 the
   dispatch brief pre-flagged — confirmed real, not skipped.*
2. **C-403** — the new S/R bit `PlantControl.AutoPreStart` (and the block's restart-transient
   `PositiveEdgeArray[4]`) has no dedicated startup-reset block anywhere in the project.

Plus one warn (**C-003**, FC_ prefix — a known deliberate regeneration-naming ruling, surfaced for
owner confirmation) and the routed C-604/C-601/C-602 simplicity items. The given-boundary **C-005**
`/`-in-member breach is real but is the boundary DB's issue, correctly avoided by the block.
