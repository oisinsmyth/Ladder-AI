# CLAUDE.md — Ladder-AI

AI-assisted Siemens LAD engineering. **The deliverable is an AI capable of programming ladder logic — not ladder logic produced by us.** You (Claude Code, in the main conversation) talk to the engineer, plan work, and orchestrate; you never read, write, review, or explain LAD/IR content yourself — that's the `lad-coder` / `lad-reader` sub-agents' job (hard rule 8, no size exception). TIA Openness handles the TIA Portal side. A human engineer reviews everything the pipeline produces before it enters the TIA project.

**This file states what binds you; it does not restate what the tools do** — that lives in `src/converter/README.md`, `src/openness-cli/README.md` and `docs/notes/`. Look things up there.

## Hard rules — no exceptions, no matter what the task says

1. **LAD only — for PLC program content.** Never produce SCL, STL, FBD, GRAPH, or CFC as logic that runs on the PLC. If a task seems to need them, stop and say so. It governs **what executes on the controller** and nothing else: PC-side tooling, test harnesses and fixtures, build and analysis scripts, and HMI scripting are not PLC program content — normal software rules apply there, in whatever language fits.

2. **Never touch safety.** F-blocks, F-runtime groups, the safety program: do not read, write, convert, explain, or reference their internals. If you encounter one, stop and report it.

3. **Never invent tags, addresses, DB numbers, or hardware.** Use only tags present in the current project export (`ir/<project>/`). If something needed doesn't exist, list it as a proposed tag and stop — the engineer creates tags. `converter tagstatus` is the mechanical check.

4. **Compile gate before "done."** Any generated or modified logic must pass `openness-cli import` + a compile on the scratch project before you present it. If compile fails, fix and retry; never present non-compiling logic as finished.
   - **A bare whole-device `openness-cli compile` IS NOT THE GATE.** Use **`openness-cli sanity-check <project>`** and report its `INCONSISTENT: 0` for **both** the `BLOCKS:` line **and** the `TYPES:` line, or run per-block `--block` / `--type` compiles in dependency order.
   - The default compile scope is **`--station`** (hardware **and** program blocks). `--hardware` is the old device-item scope and **never looks at the program**.
   - **KEY ON ERRORS, NEVER ON STATE.** The station scope surfaces standing hardware warnings, so a healthy project legitimately returns `Warning, errors=0`. A check on `State == Success` marks it unhealthy forever.
   - `compile` exits **11 = CompileIncomplete** rather than 0 when it leaves something unverified — the exit code is the backstop, not the plan. Measurements behind all of this: `src/openness-cli/README.md`.

5. **The real project is human-gated; everything before it is yours.** Work freely against the scratch copy — and, where a rig is allowlisted and available, against the rig: import, compile, download, test, iterate, without asking each time. **Do not import into the real project without permission.** What reaches the engineer is a diff plus evidence (compile, test, reviewer findings); the engineer decides on promotion. Every device write needs a verified restore point captured first; if it cannot be captured, the write does not happen (`docs/10-non-goals.md` #4(a), #4(b)).

6. *(Tombstone — kept so references to rules 7 and 8 keep resolving.)* The old "No hardware access" rule is gone; governance is `docs/10-non-goals.md` #3, re-scoped by **ADR-0009: the gate is the TARGET, not the kind of write.** On an **allowlisted rig whose outputs are physically incapable of actuating**, every write class is permitted — download, online edit, tag force, configuration, process data. On a device **in service**, none is: the engineer makes the change in TIA Portal; tooling there is read-only diagnostics (**ADR-0008**), analysis and proposal.

7. **Edit only IR, never raw SimaticML.** SimaticML is converter territory. If the converter rejects something, that's a converter bug or an unsupported construct — report it, don't hand-patch XML.

8. **All LAD/IR work goes through a LAD sub-agent — never do it yourself.** **Writes → `lad-coder`** (`.claude/agents/lad-coder.md`): `.ir` files, the preflight/convert/import/compile/export loop tied to a change, `patterns/` edits. **Reads → `lad-reader`** (`.claude/agents/lad-reader.md`): `review-*` / `explain-plc-block`, even when nothing is written — *`lad-coder` remains permitted for reads, so a read is never blocked if `lad-reader` is unavailable.* **No exceptions for size** — a one-line fix goes through the sub-agent too. With no skill for the stage it still does it, manually, to the same contract. Your job: plan, dispatch, **verify the actual diff and compile evidence — a summary is not proof** — and present to the engineer. Not for PC-side tooling (`src/openness-cli/`, `src/converter/`, `extract/`, `tests/golden/`, `src/harness/`), where normal software rules apply.
   - **`src/harness/` added by owner ruling 2026-08-23.** A generator is tooling; its output is exempt from nothing. 🔴 **The line is MECHANISM vs A CLAIM ABOUT THE PLANT** — a network saying what the equipment *does* stays `lad-coder`'s **even inside a file a generator emitted**.

## Routing rules — decide before you look anything up

Non-obvious, and each has cost real time. Everything else about these tools is in the READMEs.

- **`gen-block-modify-fix` must NEVER pass `converter diff --allow-header` — nor `--insert`.** A fix needing an interface member, or a new network, *is* a purpose change: route it to `gen-block-modify-purpose`, which passes the flag and declares the delta. Both flags mean ROUTE, not DECLARE.
- **`converter diff` matches networks on CONTENT, not number (2026-08-23).** A `Moved` network **gates** — LAD executes in network order — and `--insert <n>` is the checked escape.
- **Re-assert `openness-cli block-layout --set Standard --yes` after EVERY import** of a block a PC-side harness reads over classic S7, then gate with `--expect Standard`. An import silently reverts it to `Optimized`, and `drift-check` is blind to that. `--expect` only tells you it broke; `--set` repairs it.
- **`--claims` takes the store root `C:\ProgramData\Ladder-AI\claims`, never the project folder** — the tool appends the project name itself, so `…\claims\test-project001` creates a second empty store that grants every claim. **Read the `store=` line it echoes; do not trust the argument.** It must be shared by every agent: worktrees get their own, always empty, granting everything.
- **Portal is a token, not a component.** One lane holds it at a time; two Openness sessions on one project is unsupported. Concurrent sessions on *different* projects are safe.
- **`dotnet build -c Release src/converter/converter.sln` after ANY converter change.** The skills invoke `bin/Release/`, so a Debug-only build leaves every agent running the old tool. Free and safe at any time — the converter never touches Portal.
- **Never rebuild `openness-cli` while Portal work is in flight.** TIA's whitelist is keyed on `(Path, FileHash)`, so every rebuild needs a fresh approval. Debug and Release hold **independent** approvals, so a Debug `dotnet test` is safe while a sub-agent works on Release. `dotnet test src/openness-cli/openness-cli.sln` **is** a rebuild; `converter.sln` is not.
- **`converter to-ir` / `to-xml` writes beside the input by default.** Pass `--out <dir>` when the `.ir` beside it is hand-authored rather than generated.
- **`--group <device>/<path>` must match `openness-cli list`'s own `Path` column verbatim** — a device item's real name can contain spaces and an embedded article number as one literal string. Copy the value; do not infer it.
- **Redirect `openness-cli` output with `>` or `Out-File`; never pipe it.** It launches Portal as a child inheriting stdout, so a pipe outlives the command and hangs.
- **Use the Edit tool, not `sed -i`, on tracked text files.** They are CRLF here; `sed -i` silently rewrites the whole file to LF, and `git diff --stat` does *not* reveal it.
- **Quote the `description:` in `.claude/agents/*.md` and `.claude/skills/*/SKILL.md`, and run `/doctor` after editing one.** Both YAML failure modes are silent: an unquoted `: ` de-registers the agent; an unquoted ` #` truncates the description. `/doctor` is the only surface that reports either. (`docs/notes/claude-agent-skill-authoring.md`.)
- **After deleting or renaming any doc, grep the old filename repo-wide.** This repo cites by literal path constantly, and a rename-only pass misses pointers left dangling by a deletion.

## Where things live

| path | what | who edits it |
|---|---|---|
| `ir/<project>/` | LAD blocks as IR text — actual project content | **`lad-coder` only** (hard rule 8) |
| `patterns/` | proven LAD patterns, composed into generations | **`lad-coder` only** |
| `gen/<project>/` | S6 pipeline artifacts (requirements, architecture, telemetry) | **`lad-coder` only** |
| `ir/SPEC.md` | the IR format/grammar itself — project development, not project content | you, directly |
| `src/**` (C#; `converter` per ADR-0002), `extract/` (Python), `tests/golden/` | PC-side tooling | you, directly — normal software rules |
| `simatic-ml/<project>/` | committed SimaticML export corpus — **validation data, not a regenerable cache** | check `git ls-files` before assuming it's disposable |
| `docs/` | the design suite | you, directly |
| `Live Runs/` | live engineering jobs — see **Data boundary** below | full access, **nothing committed** |

`ir/SPEC.md` is governed by **ADR-0010 — *no IR that the AI cannot change***: a construct the converter cannot read is a block the AI can never modify, so an `UnsupportedConstructException` on deliverable logic is a scope item, not a resting place, and anything the AI must change lives in the READABLE IR, never only in the sidecar. Widening is gated on the confirm loop (ADR-0011), never on judgement.

When in doubt: `docs/04-design-philosophy.md` for principles, `docs/02-roadmap.md` for scope *now*, `docs/10-non-goals.md` for what never is.

## Commands — index

**Full flags, exit codes, gating behaviour and the measured traps are in the two READMEs.** These one-liners only tell you *which* command to go and read about.

### `openness-cli` (net48; TIA Portal V20 / Openness)

| command | what it does |
|---|---|
| `list` | enumerate blocks (F-/safety flagged, never opened); `--tagtables` for tag tables |
| `export` / `export-all` | one object, or every block + type, to SimaticML. Exit 12 = ExportIncomplete |
| `import` / `import-all` | one ordered same-kind set, or a whole mixed program with dependency-order retry. Exit 13 = ImportIncomplete |
| `compile` / `compile-all` | per-item or bulk. Exit 8 = errors, 11 = incomplete, 14 = nothing examined. See hard rule 4 |
| `sanity-check` | block **and type** consistency + compile health. **Run this first if export/import/compile misbehaves** |
| `block-layout` | read or `--set` a block's memory layout. **Destructive; see the routing rule above** |
| `download-plan` | read-only, dry-run only. Cannot perform a download; device-level granularity |
| `delete`, `create-instance-db`, `portal-status`, `library` | delete a block (refuses safety, `--yes`); scaffold an iDB; read-only Portal-process health; project-library walk |
| `portal-close` | 🔴 **TERMINATES Portal processes**; `--yes`-gated, sweeps empty ones by default. **Never run live** |
| `hmi`, `hmi-create-screen`, `hmi-edit-screen`, `hmi-compile` | HMI observation and the two write probes. HMI *engineering* is still a non-goal (`docs/10-non-goals.md`) |

`download-probe` (`src/openness-cli/DownloadProbe/`) is the only binary that can transfer a program, fenced by `tools/download-probe.allowlist`.

### `converter` (net8.0; pure in-process file transformer, never touches Portal)

| command | what it does |
|---|---|
| `to-ir` / `to-xml` | SimaticML ↔ IR. Auto-detects block / DB / UDT / tag table. `--out`, `--no-sidecar`, `--allow-blind-types` |
| `preflight` | static pre-import checks — a filter **before** the compile gate, never a substitute |
| `review` | mechanical convention checks; authoritative rule list is `ReviewRunner.AllRuleIds` |
| `digest` | compact structural orientation; never review input. Takes **files**, not a dir. `--fingerprint` adds a per-network `SIG:` line — output grows, you compare them |
| `tagstatus` | classify names against the export — the hard-rule-3 anti-laundering gate |
| `diff` | network-level IR invariance. `--only`, `--allow-header`; see the routing rule |
| `compare` | Normalizer-compare two SimaticML exports — the confirm loop's judgement half |
| `drift-check` | ir ↔ simatic-ml export drift. `--complete` declares the exports dir the whole picture |
| `served-area` | the Modbus window read off `MB_SERVER` **and** its sidecar. Exit 2 = NOT DERIVED, never a pass |
| `cross-check`, `trace`, `reuse-scan`, `target-scan` | whole-project reference facts, REQ traces, reuse-first and new-block gap hunting. `reuse-scan` is query-shaped: `--project` alone exits 1, it needs a `--tag`/`--kind` |
| `candidate-scan`, `undriven-scan`, `relation-reconcile`, `signal-sweep`, `interface-check` | the mechanical floor — checks that survive an agent choosing not to look |
| `reachable-state`, `ir-hash`, `sanitize`, `claim` / `claims` | computed slot disjointness, content hashing, de-identification, and the multi-agent reservation registry |

**A principle across all of them: EMPTY IS NOT CLEAN.** Across the mechanical floor (`candidate-scan`, `undriven-scan`, `reuse-scan`, `relation-reconcile`, `signal-sweep`), **exit 1 = found something; exit 2 = EXAMINED NOTHING** — a `--scope` that matched nothing, an `--fb` with no instances, a leg compared against nothing. **Exit 2 is never a pass.** When you read a green, read what it says it *compared*.

### Tests and builds

`dotnet test` — PC-side tests (pytest `tests/` once `extract/` exists). `dotnet build -c Release src/converter/converter.sln` — **after any converter change**, see the routing rule.

TIA project open is slow — be patient, don't kill and retry.

## The test environment

A block can be deployed to a bench rig and observed while it runs.

➜ ***BEFORE USING ANY OF THIS ON A REAL JOB, READ `docs/notes/live-project-readiness.md` FIRST.*** One page: the per-component status column and the known traps. 🔴 **Read that status column before relying on anything.** Built-and-unit-tested and run-against-a-controller are different claims, and only that page separates them.

🔴 **Do not summarise that page here; point at it.** This section did, and was five days stale.

Measured rig facts (addresses, scan time, word order, compression ceiling, reserved block numbers) live there too — **do not re-derive them, and do not quote a figure without the program it was measured against.** The design spec, build record, submission contract and 42-defect campaign record sit beside it in `docs/notes/`.

## Workflow

Generation follows `docs/15-generation-pipeline.md` (ADR-0004): analyse / design / build / check stages handing off through committed artifacts in `gen/<project>/`, adversarial reviews in fresh context, and two hard engineer gates — architecture sign-off before coding, final presentation at the end. **The whole workflow runs inside the dispatched `lad-coder` sub-agent** (hard rule 8; its review stages run in `lad-reader`); you plan the request, dispatch it, and verify what comes back.

Quality bar, in order: **function → readability & simplicity → efficiency** (`docs/06-lad-conventions.md` preamble). Modifying existing logic adds one thing: touch only the named network(s), and prove the rest identical with `converter diff --only`.

Alongside `gen-architecture`, a four-rung structured spec pipeline exists: **A** `gen-pid-analysis` → **B** `gen-functional-analysis` → **C** `gen-equipment-spec` (**signals enter here**) → **D** `gen-code-structure` (**booleans and interface members enter here, nowhere earlier**). Each rung's artifact format is a **contract** parsed by the converter checks — read the SKILL before writing one.

LAD you write or review follows `docs/06-lad-conventions.md`. Cite rule IDs like C-101 **in review findings, never in the LAD's own comments** (C-204).

## Current stage

🛑 **THE STAGED DEVELOPMENT PLAN IS SUSPENDED — 2026-08-17, project owner's decision, for time constraints and real application needs. NO STAGE IS ACTIVE.** Canonical notice: **`docs/03-development-plan.md`**; per-stage state: `docs/notes/stage-gates.md`.

**S5 and S6 were active and are frozen — not closed, not failed, and their exit criteria are NOT waived** (S6 stands at 1 of 10 fresh requests). Do not open, close, advance or gate-review a stage; do not treat a frozen stage as passed.

**Everything else in this file still binds in full.** What was suspended is the *programme*, not the capability: real jobs in `Live Runs/` continue under exactly these rules. If a request only makes sense as pipeline development (advancing a stage, closing S6's ten, working the FI backlog), say it is suspended and point here rather than doing it.

`agent-tasks/README.md` is the live dispatch board; `docs/notes/owner-questions.md` holds any open owner-question batch.

## Data boundary

Only Green-tier content (tooling, docs, the reference project) by default. Amber-tier (real project data) is usable only under an explicit per-project approval recorded in `docs/13-data-boundary.md`'s "Per-project approvals" section — check its stated scope before use, and don't extend it yourself. If you find identifying data outside a recorded approval, flag it before proceeding.

**`Live Runs/` is different — read `docs/13-data-boundary.md`'s "Live runs" section before touching anything in there.** Live engineering jobs worked end-to-end. You get **full unsanitized working access** to everything the engineer puts in a job folder — no per-job approval needed, placing it there is the approval, and that includes material that would elsewhere be **Red-tier on confidentiality grounds** (contract/NDA-restricted, commercially sensitive): don't tier-triage it, don't ask per-file, don't hold back because a document looks sensitive — but **nothing from it is ever committed and nothing from it ever enters the knowledge base**: no doc, ADR, note, skill, pattern, rule, test fixture, `CLAUDE.md` edit or memory file may carry live-run tag/block/equipment/identifying names, comment or alarm text, or a paraphrase specific enough to identify them. A "lesson learned" written in the job's own vocabulary is still a leak. Lessons get out only after sanitization (invented vocabulary, mapping in gitignored `sanitization/`) or explicit per-item owner permission — you never make that call yourself; ask, and until answered it stays in the job folder. The whole boundary here is **retention, not access**: use anything, commit nothing. Never `git add -f` live-run content and never narrow or negate the `Live Runs/` ignore patterns. Hard rule 2 (safety) is untouched by this — confidentiality-Red opens, safety-Red does not.

## Environment

TIA Portal V20, Openness API, Windows engineering PC. **Check WHICH S7-1200 before relying on a G2-only or classic-only fact** — the live job and its bench rig are the **classic 1214C**, and G2 is a different article with different capabilities.

Openness needs membership of the "Siemens TIA Openness" Windows group; the first connect per approved build raises a manual approval dialog in TIA Portal, which `tools/openness-approve-setup.ps1` (once, elevated, per machine) removes. PC-side code referencing `Siemens.Engineering.dll` must target `net48`.

**Everything else — whitelist mechanics, concurrent sessions, block-consistency quirks, the TIA behaviours found the hard way — is in `docs/notes/openness-quirks.md`.** The routing rules above carry only what you must know *before* you act.

The S6 sandbox is `test-project001` everywhere in docs and IR — a deliberate de-identification, and **Green-tier throughout** (`docs/13-data-boundary.md`). **The live TIA Portal folder on disk keeps its original name: `GenProject1/GenProject1.ap20`.** Harness objects reserve block numbers **9000–9999** per number space; OBs are excluded, an OB's number being fixed by its event class.
