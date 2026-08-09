# HMI + AI — a menu of design options (2026-08-09)

**This is an OPTIONS MENU, not a plan of record, and not a recommendation.** It exists to feed
`docs/adr/adr-0007-hmi-engineering-scope.md`, which is **Proposed and undecided**.
`docs/10-non-goals.md` still lists HMI engineering (screens, scripts, faceplates) under "Not now
(revisit only via ADR)", and **nothing here changes that**. Every write-side option below is written
in the conditional on purpose: *if* scope were granted, this is what the shape would be. Reading this
document is not scope. Building anything in it without the ADR being accepted is the "by drift"
failure `10-non-goals.md` exists to prevent.

The read-side options are a different case and the document says so where it applies:
`openness-cli hmi` is read-only, already built, and already useful under the existing non-goal —
`10-non-goals.md` puts HMI *documentation* (data extraction) explicitly **in** scope. That line is
where most of the cheap value in this menu sits, and it is not an accident.

**Data boundary.** The reference device is a live engineering job (`docs/13-data-boundary.md`). **Every
tag, screen, equipment and alarm name in this document is invented.** Structural facts, counts and
API behaviour are real and are already committed in the two survey notes. If you extend this
document, keep to invented vocabulary — a paraphrase specific enough to identify the plant is still
a leak.

---

## Reading key

Three labels, applied per claim, because the difference is the whole value of these notes:

| Label | Meaning |
|---|---|
| **[MEASURED]** | this repo executed it against a real device and read the result back. Cited to a section of `openness-hmi-write-api.md` (write-api) or `openness-hmi-api-survey.md` (survey). |
| **[DOCUMENTED]** | Siemens, a standards body, or a third party states it. Not verified here. |
| **[SPECULATIVE]** | my proposal or inference. Nobody has measured it and nobody has documented it. |

Sizes are **rough**, in the units this repo actually plans in:

| Size | Rough meaning |
|---|---|
| **XS** | hours — one probe, one flag, one report |
| **S** | 1–3 days of PC-side work |
| **M** | ~1 week, one new artifact format or one new subcommand family |
| **L** | multi-week, a new dialect / a new reviewer family / a new pipeline rung |
| **XL** | a second pipeline, comparable to what S1–S6 cost on the LAD side |

---

## 0. The eight facts every option below is shaped by

Not a summary of the surveys — the specific constraints that decide what is and is not possible.
Each is measured; each kills or enables specific options downstream.

1. **There is no screen export, in any form, for WinCC Unified.** [MEASURED] survey §8, verified four
   independent ways, and now corroborated from outside by a third-party vendor (§9c, Copia). **So
   there is no document to convert, no diff, no hash, no golden round-trip, and no text artifact a
   human can review in git.** Every review-discipline asset this repo owns assumes a serialisable
   representation and none of them transfer for free.
   **⚠ One unresolved external check against this, recorded rather than smoothed over:** Siemens'
   Openness manual carries pages titled *"Exporting all screens of an HMI device"* in both the v20 and
   v21 trees, and **their bodies could not be read** (§9c). The strong reading is that they are the
   Classic SimaticML paths — consistent with everything else — but this is **the load-bearing fact of
   the document** and it deserves a five-minute local check (probe **P-8**) rather than confidence.

   > **✅ P-8 RESOLVED, 2026-08-09 — fact 1 HOLDS, and is now confirmed a fifth way.** Rather than
   > chase the unreadable manual pages, the assembly was swept exhaustively for every
   > `Export*`/`Import*` method under both HMI namespaces (`openness-hmi-faceplate-library.md` §7).
   > **Classic** has the full screen export surface — `Screen.Export(FileInfo, ExportOptions)`,
   > `ScreenComposition.Import`, plus popup/slidein/template/overview/global-elements — which is
   > certainly what those manual pages describe. **Unified `HmiScreenComposition` has `Create(name)`,
   > `Find`, `Contains`, `IndexOf`, `GetEnumerator` and NOTHING ELSE: no `Export`, no `Import`.**
   >
   > **But the surrounding sentence needs narrowing, and it changes some options below.** "No export"
   > is true of **screens**, not of Unified. `HmiTagComposition`, `HmiTextListComposition`,
   > `HmiSystemTextListComposition` and `HmiScriptModuleComposition` all carry `DirectoryInfo`-based
   > `Export`/`Import` pairs, and library types carry `ExportAsDocuments`/`CreateFromDocuments`. So:
   > **tags, text lists and script modules DO round-trip**; bulk tag import exists and this project
   > has never used it; and **"script modules and text lists cannot be authored (no `Create`)"** —
   > stated in the write-api gap register and repeated in §5/§7 below — **is wrong**: both have
   > `Import`, so the constructor is a document. Group V's serialiser is needed for less than it
   > assumes. All REFLECTION; none of these round trips has been run.
2. **A screen is a FLAT list of absolutely-positioned items.** [MEASURED] write-api §2 —
   `HmiScreen.ScreenItems` is the only child-item composition in the entire assembly. Grouping is a
   naming convention, not a structure. **All layout intelligence is ours.**
3. **Faceplate TYPES cannot be authored, only instantiated.** [MEASURED] write-api §2. "Define a pump
   faceplate once, stamp it forty times" can have its second half automated and not its first. This
   is the opposite of this repo's pattern-library premise.

   > **✅ RESOLVED 2026-08-09 — FACT 3 STANDS. The challenge below was tested live and defeated.**
   > `openness-cli library` walked the reference project's library: every Unified faceplate is a
   > plain `LibraryType` (**not** a `FaceplateLibraryType`) and `GetSupportedExportFormats()` returns
   > **empty** for all of them, so there is no document round trip. Negative control in the same
   > read: PLC types and code blocks in that same library return full format lists, so the mechanism
   > exists and is simply not offered for HMI content. **Every option in this menu that was
   > provisionally weakened by the challenge — G-6, V-1, V-2, the faceplate probe P-1 — reverts to
   > its original standing**, and the layout problem (fact 2) is *not* relieved by a reusable
   > component. The challenge text is kept below because its critique of write-api §2's *reasoning*
   > was correct even though its conclusion was wrong.
   >
   > **⚠ FACT 3 WAS CONTESTED, and the challenge landed while this document was being written.**
   > `docs/notes/openness-hmi-faceplate-library.md` (2026-08-09, **REFLECTION ONLY — nothing in it
   > verified live**) argues that write-api §2's reasoning is invalid: `HmiFaceplateInterface
   > Composition` is the *instance's parameter list*, not the type, so its lack of a `Create` says
   > nothing about authoring. It finds that faceplates are **library types**
   > (`Siemens.Engineering.Hmi.Faceplate.FaceplateLibraryType : LibraryType`), and that library types
   > carry `ExportAsDocuments` / `CreateFromDocuments` — i.e. **a document round trip in the same
   > shape the PLC side's whole IR pipeline is built on**.
   >
   > **If that holds, it is the single largest change to this menu**: it would give faceplate types
   > both an authoring route (G-6 stops being stamping-only) *and* a serialisable artifact (part of
   > fact 1's consequence lifts, for library content). It also supplies the reusable component whose
   > absence is most of the layout problem (§4).
   >
   > **It does not hold yet.** Its own §4 lists the load-bearing unknown plainly — whether a
   > *Unified* faceplate is a `FaceplateLibraryType` at all, since that class sits in the classic
   > namespace — plus whether `GetSupportedExportFormats()` returns anything, what an exported
   > document contains, whether the round trip is lossless, and whether the reference project's
   > library holds any faceplate types to test against. **I have left every option below written
   > against fact 3 as measured**, because rewriting a menu on an unverified reading is exactly the
   > mistake that note itself warns about ("an argument overturned by an unverified reading is worse
   > than the original"). Where an option would change, it says so in place.
   >
   > Its named tests supersede this document's probe P-1: steps 1–2 there are read-only and answer
   > the question in one Portal read.
4. **The device compile is a real gate for reference integrity and script content, and is BLIND to
   geometry.** [MEASURED] write-api §4e/§4f. Across six probe phases it caught five independent
   routes to a dangling reference and named the missing field each time. A zero-width screen compiles
   clean. **So the one automated gate available covers exactly the failure mode a generator is prone
   to, and none of the failure modes a *designer* is prone to.**
5. **`Validate()` is not a gate and never will be.** [MEASURED] write-api §4c —
   `HmiValidationResult` carries a `PropertyName`, so it is a per-property checker, structurally
   incapable of cross-object questions. Do not design around it.
6. **The compiler's own `ErrorCount`/`WarningCount` are wrong in both directions.** [MEASURED]
   write-api §4e/§4f — `WARNINGS: 0` against 156 real warnings; `ERRORS: 1` against 6. Gate on
   `State`, walk `Messages` recursively. `openness-cli` collects the tree correctly but currently
   *reports* the header counts — a known defect to fix before any of this is built on.
7. **There is no transaction and no rollback, and deletion orphans silently.** [MEASURED] write-api
   §4h, §4j. A failed command may have already committed part of its work; deleting a referenced
   object leaves live bindings pointing at nothing, and only a later compile notices. **Anything that
   deletes must compile in the same breath.**
8. **Three specific things refuse, and none of them says why.** [MEASURED] write-api §4k, §4l —
   21 of 56 screen-item types, 3 of 6 dynamization kinds (including `Flashing`, which is how a real
   HMI shows an unacknowledged alarm), and **alarm text** (`MultilingualTextItem.set_Text` throws).
   The creatable set is discoverable only by trial.

   > **✅ PARTLY RESOLVED 2026-08-09 (P7 + P8, write-api §4m/§4n) — the dynamization third of this
   > fact was WRONG.** Kinds are gated on the **target property's type**, and the probe had bound all
   > three to a Boolean. **5 of 6 create**: `Flashing` on colour properties, `ResourceList` on text
   > properties. Better still, a `TagDynamization` **mapping table** carries a per-entry `Flashing`
   > flag that works on *any* bound property — so it is strictly more capable than
   > `FlashingDynamization`, it compiles clean, and **alarm-state DISPLAY is fully expressible**.
   > `TagParameter` remains refused (faceplate-scoped) and is likely unreachable, since faceplate
   > types cannot be authored.
   >
   > **Still true and still blocking: alarm TEXT, and 21 of 56 item types.** So G-4 loses its display
   > blocker but keeps its text blocker; probe **P-2** is now the single highest-value unknown left.
   > 🔴 And one new hard limit: **`MappingTableEntrySimple` CRASHES TIA Portal** — forbidden, use
   > `MappingTableEntryRange`.

One more, from the read side, because it is what makes Group A possible at all:

9. **The PLC↔HMI join is READABLE, per property, and the names do not match.** [MEASURED] survey §7,
   write-api §3 — `dynamization.Tag → HmiTag.PlcTag → PLC tag`, set in two different places with
   nothing forcing agreement, and measured *differing* on a real project where one panel faces two
   PLC stations. The join is extractable; assuming name equality is measurably wrong.

---

## 1. The menu at a glance

**47 options in seven groups** — A 11 · B 3 · L 6 · G 10 · S 4 · V 7 · W 6.
"Scope?" says what the option would need from ADR-0007:
**read** = nothing beyond today's read-only `hmi` command (and `10-non-goals.md` already allows HMI
data extraction) · **write** = the ADR must be accepted · **none** = pure PC-side, no Portal contact.

| # | Option | Scope? | Size | Depends on |
|---|---|---|---|---|
| **A — Read & audit** | | | | |
| A-1 | Full HMI census / inventory artifact | read | S | — |
| A-2 | Dangling & unresolved-binding sweep | read | S | — |
| A-3 | Unbound-item ("displays nothing") audit | read | S | A-1 |
| A-4 | PLC↔HMI tag-boundary reconciliation | read | M | A-1, `ir/` export |
| A-5 | Alarm-coverage audit (PLC alarm words vs HMI alarms) | read | M | FI-35 `alarm-scan` |
| A-6 | Navigation-reachability graph | read | M | A-1 |
| A-7 | Style/consistency audit against an encodable style guide | read | M–L | L-4 |
| A-8 | Geometry linter (overlap, off-canvas, zero-size) | read | S | A-1 |
| A-9 | Cross-screen presentation consistency | read | M | A-1, A-4 |
| A-10 | `explain-hmi-screen` — the S2 explanation skill, re-aimed | read | S | A-1 |
| A-11 | Authorization / operator-writable surface report | read | S | A-1 |
| **B — The PLC/HMI boundary (FI-18)** | | | | |
| B-1 | `hmi-interface.md` — the boundary contract as a spec artifact | none | M | — |
| B-2 | Boundary conformance check (as-built vs contract) | read | M | A-4, A-6, B-1 |
| B-3 | Reverse contract extraction from an existing HMI | read | S | A-4, A-11 |
| **L — Layout** | | | | |
| L-1 | Screen-template library (the `patterns/` analogue) | none | M | L-4 |
| L-2 | A layout DSL that deterministically compiles to x/y/w/h | none | M–L | — |
| L-3 | Constraint solver (Cassowary-class) | none | L | — |
| L-4 | Harvest the site's de-facto style guide from as-built screens | read | S | A-1 |
| L-5 | Item-catalogue geometry defaults table | read | XS | — |
| L-6 | Vision-in-the-loop layout iteration | none | L | W-1 |
| **G — Generation** | | | | |
| G-1 | Per-equipment detail screens from rung C specs (a "rung E") | write | L | L-1/L-2, V-1 |
| G-2 | Diagnostic/IO screens from the tag list | write | M | L-2 |
| G-3 | Overview/mimic screen from the P&ID topology (rung A) | write | L | L-2/L-3 |
| G-4 | Alarm objects from the PLC alarm words (FI-35's HMI half) | write | S* | **BLOCKED** — fact 8 |
| G-5 | Navigation scaffold (frame + window + buttons) | write | M | S-2, fact on groups |
| G-6 | Faceplate **stamping** (instantiate a human-authored type) | write | S | **probe P-1** |
| G-7 | Compile-driven bulk retrofit of existing screens | write | S–M | V-1, V-4 |
| G-8 | Trend/log page generation from a logged-tag list | write | M | — |
| G-9 | Multi-language text population | write | ? | **BLOCKED** — fact 8 |
| G-10 | **SiVArc as the engine; the AI authors its rules and templates** | ? | M | SiVArc licence |
| **S — Scripting** | | | | |
| S-1 | Generated event handlers (navigation, simple commands) | write | S | S-2 |
| S-2 | A vetted JS snippet library for Unified runtime | none | S | — |
| S-3 | Script audit of existing handlers | read | S | A-1 |
| S-4 | Script-to-logic push-back check (behaviour hiding in the HMI) | read | M | S-3, A-4 |
| **V — Verification & round-trip** | | | | |
| V-1 | **The canonical serialiser** | read | M | — |
| V-2 | An applicable build-plan format (the serialiser's inverse) | write | L | V-1 |
| V-3 | Geometry/semantic gate (A-7 + A-8 as a hard gate) | read | S–M | A-7, A-8 |
| V-4 | Fix the compile-result reporting (count from the tree) | none | XS | — |
| V-5 | Serialised regression baseline + drift check | read | S | V-1 |
| V-6 | Pre-write reference check (`DataType` read-back) as a preflight | write | XS | — |
| V-7 | Evaluate Siemens' own Excel Importer/Exporter (109792619) as V-1+V-2 | read | S | — |
| **W — Human-in-the-loop** | | | | |
| W-1 | Schematic renderer (serialised screen → SVG wireframe) | none | M | V-1 |
| W-2 | Review in TIA Portal (the ground truth, manual) | write | XS | — |
| W-3 | Runtime-simulation preview + screenshot | ? | L | ruling needed |
| W-4 | Independent-reader review report | read | S | V-1, A-10 |
| W-5 | The staged proposal→build→review workflow | write | M | V-1, W-1, W-2 |
| W-6 | Deletion policy (prefix guard + mandatory compile) | write | XS | fact 7 |

`*` G-4 is S-sized **only if** the alarm-text refusal turns out to be an ordering problem. Today it
is blocked, not small.

---

## 2. Group A — Read and audit

**Weigh this group first.** It is the answer to "highest value per unit of risk", and the argument is
not subtle:

- It needs **no scope grant**. `openness-cli hmi` is read-only, built, and exercised on a real device
  [MEASURED] survey §7. `10-non-goals.md` already places HMI *documentation* in scope as data
  extraction.
- It cannot break anything. There is no write, so facts 7 (no transaction) and 8 (silent refusals)
  do not apply.
- **It catches classes of defect that nothing currently on the machine catches** — not the compile
  (blind to geometry, blind to navigation intent), not `Validate()` (blind to everything
  cross-object), and not a human reading 48 screens.
- The as-built project it would run against has **156 compiler diagnostics that were produced by
  hand-authored screens** [MEASURED] write-api §4e. The findings are not hypothetical.

The honest counterweight: an audit produces *findings*, and findings need an owner willing to act on
them. A report nobody acts on is make-work, which is FI-54's own cost 5 applied to a different
activity.

### A-1 — Full HMI census / inventory artifact
**What.** Extend the existing read walk into a complete structured inventory: every screen (with its
group path), every item with type/geometry/key attributes, every dynamization with its resolved
`PlcTag`/`DataType`, every event handler with its script body, every tag with its connection and PLC
counterpart, every alarm with its class/priority/state-machine, every alarm class, log and
connection.
**Produces.** One machine-readable artifact per device (JSON) plus a human summary. It is also
V-1's input and A-2..A-11's substrate — build this once, not per audit.
**Rests on.** [MEASURED] survey §7 — the walk runs against a real Unified device and returns screens,
groups, items, geometry and dynamizations; write-api §4i lists what is and is not walked.
**Missing.** Alarms and tags are currently **counted, not enumerated** (survey §7 "Walker gaps").
Events were a stated gap and were fixed for screens (survey §10b) but the item-level coverage should
be re-verified. Runtime settings and plant views are read-only-reflected, never walked.
**Size.** S.
**What could go wrong.** **A walker that under-reports silently** — the measured precedent is
brutal: the first walk reported **3 screens where there were 48**, a 16× undercount, because
`HmiSoftware.Screens` is root-only and the walker never descended into the 5 groups (survey §7
correction). The output looked complete and internally consistent. Any inventory must carry
self-consistency assertions (a group count next to a suspiciously small screen count is the tell) and
should be cross-checked against the compiled runtime folder's file count as a smell test — which is
what actually caught it.

### A-2 — Dangling & unresolved-binding sweep
**What.** For every dynamization on the device, report whether it resolves. The detector already
exists and did not need inventing: after setting `Tag`, the derived read-backs `PlcTag` / `Address` /
`DataType` come back **empty when the name did not resolve**.
**Produces.** A per-screen, per-item, per-property list of bindings that point at nothing.
**Rests on.** [MEASURED] write-api §4h — measured on a live device: a good binding returned
`DataType='Int'`, the dangling one returned both fields empty. `hmi-edit-screen --bind` already
reports `resolved:` / `UNRESOLVED` on this basis.
**Missing.** The read path currently reports bindings but does not classify them. Small.
**Size.** S.
**What could go wrong.** The discriminator's strength is **not fully established**: `PlcTag` was
empty on the *good* binding too (an internal HMI tag has no PLC counterpart), so `DataType` is doing
all the work, and "whether that holds for every tag kind is unverified" (write-api §4h caveat, stated
there, repeated here). Also, the device compile already finds these and names the property — so A-2's
value is *locating them without a compile* and *reporting them as a list rather than as 8 errors*,
not finding something new.

### A-3 — Unbound-item audit ("this field displays nothing")
**What.** An `HmiIOField` whose `ProcessValue` is empty **and** which has no `ProcessValue`
dynamization is a field that shows nothing. Same shape for `HmiBar`, `HmiGauge`, `HmiSymbolicIOField`
and the trend controls.
**Produces.** A list of decorative-by-accident items.
**Rests on.** [MEASURED] survey §8.3 — on a real dynamized `HmiIOField`, `ProcessValue` reads as a
`String` with **no value at all**, because the binding lives in the item's `Dynamizations`
composition, not in the property. "Value and binding are separate surfaces and both must be read" is
measured, and a reader that walked only attributes would conclude the field displays nothing.
**Missing.** A per-item-type rule table: which property is the "content" property for each of the 35
creatable types. Derivable from `--schema` (which self-describes), but somebody has to decide it.
**Size.** S.
**What could go wrong.** False positives on legitimately-static items, and on items whose content
arrives by `ScriptDynamization` or `ExpressionDynamization` rather than `TagDynamization` — both of
which are real and both of which create [MEASURED] write-api §4l P3.

### A-4 — PLC↔HMI tag-boundary reconciliation
**What.** Join the HMI tag list to the PLC export through `HmiTag.PlcTag`, and report both
directions: HMI tags whose `PlcTag` names nothing in `ir/<project>/`, and PLC signals the HMI never
reads or writes at all.
**Produces.** Two lists that are today produced by nobody: *the HMI is pointing at PLC tags that do
not exist* and *these PLC signals are invisible to the operator*.
**Rests on.** [MEASURED] survey §7 and write-api §3 — the join is readable, per tag, and the two
names **differ on a real project**. The PLC half is already solved: `converter tagstatus` classifies
names against the export to member level and exits non-zero on invented ones.
**Missing.** Nothing structural. The `--roots-only` vs member-level distinction matters here, and so
does the multi-station case below.
**Size.** M.
**What could go wrong.** **One panel facing two PLC stations breaks a naive by-name join.**
[MEASURED] survey §7: two distinctly-named HMI tags both resolve to a PLC tag of the *same* name, one
per station, disambiguated only by the tag's `Connection`. A join keyed on the PLC name alone will
merge them, and the merge will look clean. **The join must be `(connection, plcTag)`, never
`plcTag`.** This is the single most dangerous detail in the whole boundary story, and it is the one
that already falsified a stated assumption in `hmi-alarm-generation.md` §5.2.

### A-5 — Alarm-coverage audit
**What.** Cross the PLC's alarm surfaces against the HMI's alarm objects. Which alarm bit has no HMI
alarm; which HMI alarm's trigger points at a bit nothing drives; which alarm's class contradicts the
site severity taxonomy.
**Produces.** The alarm gap list, which is FI-35's use case approached from the *review* side rather
than the *generation* side — and the review side is not blocked by fact 8.
**Rests on.** [MEASURED] survey §7 — 311 discrete alarms and 20 alarm classes enumerable through the
API on a real device; write-api §4l P4 — `Priority` and `StateMachine` are readable and settable.
PLC side: `docs/06` C-501 (category words in `DB_Alarms`) and C-503 (per-instance UDT alarm words).
**Missing.** `converter alarm-scan` (FI-35's extraction half) does not exist. The HMI-side bit
numbering within a Word trigger tag is a first-class field on Unified
(`RaisedStateTagBitNumber`) — readable, so this audit does not have to assume it.
**Size.** M (S if `alarm-scan` lands first).
**What could go wrong.** **The two-surfaces trap, which already bit live.** C-503 says outright that
category words are *not* duplicated per instance, so a tool flagging "instance alarm bits not wired
into `DB_Alarms`" false-positives on **every conformant project** (`hmi-alarm-generation.md` §2 —
this misreading happened on the real run and only reading C-503 caught it). The genuine defect is the
inverse. Second risk: an `INCONSISTENT` PLC block cannot be exported at all, so a project-wide alarm
sweep can be **silently incomplete** — it must enumerate what it could not read (same note, §6).

### A-6 — Navigation-reachability graph
**What.** Build the screen graph: which screen loads which, via screen windows and via event-handler
scripts. Report unreachable screens, links to nonexistent screens, and navigation depth.
**Produces.** A directed graph artifact + findings. Unreachable screens are dead engineering effort;
a link to a deleted screen is an operator dead-end.
**Rests on.** [MEASURED] survey §7 — the reference composition is a frame screen carrying a
`HmiScreenWindow`, with content screens loaded into it; write-api §4l P6 — a screen window created
and successfully pointed at another screen, compiling clean. [DOCUMENTED] write-api §4b — the runtime
navigation idioms are `UI.RootWindow.Screen = "…"` and
`Screen.ParentScreen.Windows("…").Screen = "…"`, and `OpenScreenInScreenWindow` **does not exist in
V20**, so older examples are stale.
**Missing.** Extracting navigation targets from JavaScript is string analysis, and it is only as good
as the code is literal.
**What could go wrong.** **On the measured reference project this audit is partial by construction.**
The frame's screen-window target is itself driven by an **Expression dynamization** (survey §7,
[MEASURED]) — i.e. the destination is *computed at runtime*, not a literal. A static graph cannot
resolve it. The audit must report "N edges resolved, M targets computed and unresolvable" and never
present a reachability verdict as complete. An audit that silently treats a computed target as no
edge would declare live screens unreachable — the worst possible false positive.
**Size.** M.

### A-7 — Style / consistency audit against an encodable style guide
**What.** Mechanical checks over geometry and appearance: colours drawn from an approved palette;
colour reserved for abnormal conditions rather than decoration; font sizes from a set; consistent
margins and item pitch; minimum touch-target size; units and decimal places present on value fields;
consistent button labelling.
**Produces.** The check that fills the compile's blind spot (fact 4). Nothing else on the machine
looks at appearance at all.
**Rests on.** [MEASURED] survey §8 — geometry, colours, fonts, padding and tooltips are all readable
attributes, and `--schema` self-describes any item type on demand. Fact 4 — the compile ignores all
of it.
**Missing.** **The style guide itself.** This is not a tooling gap, it is an ownership gap: the rules
have to be somebody's, and the site does not have them written down. See L-4 for the cheap way to get
a first draft, and §9a for the external standards that could ground it — noting that **ISA-101 does
not supply rules, it obliges the site to write its own**, which makes this an explicitly governance-
shaped dependency. The corollary is a design instruction: **the checker should consume a style guide
as data, not hard-code one.**
**Size.** M–L, and almost all of it is rule authoring, not code.
**What could go wrong.** The failure mode of an unowned style checker is that it encodes *the
author's* taste and then fails 48 screens an engineer is happy with. This repo already has the
correct instinct written down for the LAD side — conventions are cited by rule ID against a document
the site owns (`docs/06`), and a reviewer's finding names the rule. An HMI style checker with no
`docs/06`-equivalent is a machine for generating arguments.

### A-8 — Geometry linter
**What.** The subset of A-7 that needs **no** style guide because it is arithmetic: items overlapping
each other, items outside the screen rectangle, zero-width/zero-height items, content screens sized
differently from the window that loads them, items whose text cannot fit their box.
**Produces.** Hard findings with no taste content.
**Rests on.** [MEASURED] write-api §4c — a zero-pixel-wide screen was accepted by `Validate()`,
saved, read back, and **produced no compile message whatsoever** in two separate runs (§4f). This is
the definitive demonstration that nothing else will catch it.
**Missing.** Nothing. This is the cheapest genuine new check in the menu.
**Size.** S.
**What could go wrong.** **"Off-canvas" is probably legal, not an error.** write-api §4c carries an
explicit self-correction on exactly this: Siemens documentation indicates Unified **deliberately
supports screen content outside the viewport**, so an item at 99999,99999 may be perfectly valid.
Report it as a warning with that caveat attached, never as an error. "Items whose text cannot fit"
needs font metrics we do not have and should be scoped out or made very conservative.

### A-9 — Cross-screen presentation consistency
**What.** The same PLC signal displayed with different units, decimal places, colour semantics or
label wording on two different screens; the same equipment class rendered differently on different
pages.
**Produces.** The inconsistency list — the thing a human reviewer of 48 screens genuinely cannot do.
**Rests on.** A-1 + A-4 (the join gives signal identity across screens).
**Missing.** Equipment identity across screens is a naming-convention inference, not a fact. On the
PLC side this repo insists that title-based attribution is a heuristic and the source tag is the fact
(`hmi-alarm-generation.md` §2) — the same rule applies: group by the resolved `(connection, plcTag)`,
not by label text.
**Size.** M.
**What could go wrong.** Deliberate difference misread as inconsistency (a summary page legitimately
shows fewer decimals than a detail page). Emit facts, not verdicts — the shape `converter cross-check`
already uses.

### A-10 — `explain-hmi-screen`
**What.** The HMI analogue of the existing `explain-plc-block` skill: read one screen and produce a
plain-language description for an engineer — what it shows, what it is bound to, what an operator can
press and what happens when they do.
**Produces.** A reviewable prose artifact per screen.
**Rests on.** The read path plus the event/script read (survey §10b fixed the screen-level handler
gap; the note is explicit that "no dynamizations on a button" previously meant *not looked at*, not
*not bound*).
**Missing.** Nothing structural.
**Size.** S.
**What could go wrong.** **An LLM narrating a screen it cannot see will confabulate spatial
relations.** The explanation must be generated strictly from the coordinate table and must say so —
"the layout is described from coordinates; this is not a rendering". And per the repo's own
reviewer/fixer direction rule, an explanation produced by the same agent that generated the screen is
a correlated check and is worth nothing as review (see W-4).

### A-11 — Authorization / operator-writable surface report
**What.** Which properties are operator-*writable* rather than display-only, and under what
authorization. `TagDynamization.ReadOnly` exists, and every item carries `Authorization`.
**Produces.** "Here is everything an operator can change from this panel, and who is allowed to." For
a plant, that is a genuinely safety-adjacent document, and nobody has it today.
**Rests on.** [MEASURED] write-api §3 — `ReadOnly` is a real field on `TagDynamization`; survey §4 —
`Authorization` is a real attribute on items.
**Missing.** Live verification of what `ReadOnly` means in practice — reflection-shaped only.
**Size.** S.
**What could go wrong.** Reporting *configuration* as though it were *effect*. A field can be
writable in configuration and unreachable in practice (unreachable screen, hidden by a visibility
binding), and the reverse. Pair with A-6, and state the limit.

---

## 3. Group B — The PLC/HMI boundary (FI-18)

This is the group ADR-0007's **option 2** describes: "bounded to alarms and the PLC/HMI boundary
contract, screens explicitly excluded". FI-18's own stated risk is scope creep from *defining* the
boundary into *building screens*, and the survey sharpens it: on Unified, screen-building is one API
call away from boundary-defining, so **the guard rail has to be a written decision, not an API
limitation** (survey §6).

### B-1 — `hmi-interface.md` as a first-class spec artifact
**What.** A per-project artifact pinning down exactly what the PLC exposes to the HMI: tags and their
value ranges, mode legends, alarm words and their bit maps, which settings are HMI-owned and which
are engineering-owned, which commands the HMI may issue.
**Produces.** The artifact several open `test-project001` questions actually need (FI-18 cites Q-03
selector semantics, Q-13 reversal display, Q-14 control-on step, Q-15 hand/jog overlay). It gives
`gen-architecture` and the requirements register a clean boundary to design against instead of
guessing at operator-facing intent.
**Rests on.** **Nothing in the HMI API — this option needs no Portal contact at all.** Two pieces
already exist in the pipeline: `gen-equipment-spec`'s `SETTINGS` table already carries an **Owner
(HMI / engineering)** column, and `docs/06` C-307 already rules where a setting lives based on who
owns it (per-instance settings in the instance's UDT, because faceplates bind the UDT).
**Missing.** The artifact format and where it slots into the A→B→C→D rungs. Most naturally a
*sibling* of rung C (it is signal-level, and it is per-project rather than per-instance).
**Size.** M.
**What could go wrong.** Two failure modes, opposite directions. (i) It becomes a screen spec by
accretion — FI-18's own warning. (ii) It becomes a document nobody consumes: this repo's evidence is
that an artifact only stays honest when a mechanical check parses it (`signal-sweep`,
`relation-reconcile`), so B-1 without B-2 will drift.

### B-2 — Boundary conformance check
**What.** Verify the as-built HMI honours the contract: every HMI-owned setting in B-1 is actually
writable from some reachable screen; every PLC status B-1 says the operator needs is actually
displayed somewhere; nothing outside the contract is bound at all.
**Produces.** The mechanical check that keeps B-1 honest, and a real finding class: *the spec says
the operator sets the dwell time; no screen exposes it.*
**Rests on.** A-4 (the join), A-6 (reachability), A-11 (writability). All read-only.
**Missing.** All three of its dependencies.
**Size.** M.
**What could go wrong.** It inherits A-6's expression-driven-navigation limit exactly: "reachable"
cannot be proven where the destination is computed. State the residue.

### B-3 — Reverse contract extraction
**What.** Derive a first draft of B-1 *from* an existing HMI rather than writing it: everything the
panel reads, everything it writes, with PLC counterparts.
**Produces.** A brownfield on-ramp — the boundary contract for a plant that already exists.
**Rests on.** A-4 + A-11.
**Size.** S–M.
**What could go wrong.** **The same circularity rung B already handles explicitly.**
`gen-functional-analysis` has a three-way stop condition and treats a *derived* source (anything read
out of the as-built) as "proceed, with a **blocking** `Q-nn` recording the circularity, and mark every
item that has no independent citation". A contract reverse-derived from the HMI has exactly that
status: it records what the panel does, not what the plant requires, and it will faithfully reproduce
whatever is wrong. Adopt rung B's rule verbatim rather than inventing a softer one.

---

## 4. Group L — The layout problem

**The API contributes nothing to layout.** A screen is a flat list of absolutely-positioned items
(fact 2), faceplate types cannot be authored (fact 3), screens cannot be created inside a group and
cannot be moved between groups [MEASURED] write-api §4l P6 and §2. Compile is blind to geometry
(fact 4) and `Validate()` is blind to everything cross-object (fact 5).

So layout is **entirely** the LLM's problem, and — this is the part that matters — **it is also
entirely unverified by any automated gate.** A screen that is a jumbled mess of overlapping boxes
compiles `STATE: Success`. That combination is the hardest single thing in this menu, and the
options below are ranked by how much of the problem they solve *by construction* rather than by
checking afterwards.

**The design principle I would put to the owner:** given no verifier, prefer approaches that make
bad layout **unrepresentable** over approaches that make it detectable. A box model that cannot
overlap is worth more than a checker that finds overlaps, because the checker's coverage is a guess
and the box model's is a theorem.

### L-1 — Screen-template library (the `patterns/` analogue)
**What.** A small number of human-authored screen shapes — frame/header, equipment detail, area
overview, trend page, alarm page — each expressed as a named grid of slots. Generation fills slots;
it does not invent layouts.
**Produces.** `patterns/hmi/<template>.md` (or similar) plus a filler. Deterministic coordinates,
because the template carries them.
**Rests on.** Nothing in the API — pure repo-side. The premise is this repo's own: a generator that
produces impressive freeform output without pattern grounding is *explicitly worse* than a smaller
pattern-based one (`10-non-goals.md` anti-goals, `04-design-philosophy.md` §6).
**Missing.** The templates. And crucially the admission rule: `docs/07`'s pattern-library spec
requires a pattern to have been *instantiated in reviewed, working* content — proven, not
speculative. An HMI template must be **harvested from a screen an engineer built and accepted**
(L-4), never invented by the AI that will then use it.
**Size.** M.
**What could go wrong.** **Fact 3 makes templates the *only* reuse mechanism, and it is a weak one.**
With no authorable faceplate type, "40 pump screens from one template" means 40 independent copies of
the same geometry. Change the template and you must re-stamp 40 screens — and you cannot diff what
changed, because there is no export (fact 1). Template reuse without V-1 is a maintenance trap, not a
reuse story.

### L-2 — A layout DSL that compiles to absolute coordinates
**What.** A small text language the LLM writes — rows, columns, stacks, spacers, spans, fixed and
flexible sizes, padding — compiled **deterministically** by PC-side code into the flat absolute
x/y/w/h list the API demands. The LLM never writes a coordinate.
**Produces.** A reviewable text artifact (the DSL source) *and* the coordinate list. Note the second
benefit and it is large: **the DSL is the diffable artifact that fact 1 says does not exist.** It is
not a serialisation of the device (V-1 is that), but it is a stable, human-readable source for
anything this pipeline generates.
**Rests on.** Nothing in the API. Precedent is everywhere in software (see §9).
**Missing.** The language, the compiler, and the discipline that nothing bypasses it.
**Size.** M–L.
**What could go wrong.**
- **A second dialect to own.** ADR-0007's Consequences already name this cost: "a second dialect, a
  second reviewer family, and a second set of conventions", competing for build-order slots against
  closing S6 and opening S7. That objection is real and this option is the concrete form of it.
- **It only constrains what it expresses.** A DSL with an "absolute" escape hatch has none of the
  by-construction guarantee; one without an escape hatch will fail to express something real. Pick
  deliberately.
- **The compiler is now safety-relevant in a mundane way**: a bug in it puts every generated screen
  slightly wrong at once. It needs golden tests of its own, which is exactly the shape
  `tests/golden/` already is.
- **Text metrics.** Any layout language that sizes to content needs font measurement, and we do not
  have Siemens' font metrics. Restrict to explicit sizes, or accept that content may clip — and say
  which.

### L-3 — Constraint solver (Cassowary-class)
**What.** Express layout as constraints (`gauge.left = label.right + 8`, `all rows equal height`) and
solve.
**Produces.** The most expressive option and the least predictable.
**Rests on.** Nothing here; established elsewhere (§9).
**Size.** L.
**What could go wrong.** **Under-constrained systems produce technically-valid, visually-wrong
results, and the failure is hard to explain to an engineer.** "Why is that button there?" answered by
"the solver put it there" is not an answer this repo's review model can accept — every LAD finding
cites a rule ID and every discharge must be *argued* (`gen-code-structure` D2). I would list this
option to reject it in favour of L-2, not to build it; a box model's behaviour can be explained in a
sentence and a solver's cannot.

### L-4 — Harvest the site's de-facto style guide from as-built screens
**What.** Measure the existing hand-authored screens: the actual grid pitch and margins, the item
size clusters, the colour set and where each colour is used, the font sizes, the header/footer band
geometry, the naming conventions. Emit it as a candidate style guide.
**Produces.** A first draft of the `docs/06`-equivalent that A-7 needs and L-1 must be grounded in —
mechanically, from real screens, in a day.
**Rests on.** [MEASURED] survey §7 — geometry and item types are read live; item types observed
included rectangle, text, line, IO field, graphic view, button, screen window and alarm control, and
layout is absolutely positioned. 1404 items across 48 screens is a large enough sample to cluster.
**Missing.** Nothing. **This is the cheapest high-leverage option in the whole document.**
**Size.** S.
**What could go wrong.** **The as-built encodes mistakes as faithfully as intent** — and this repo
has the exact precedent written down: every file in `references/` self-declares as *derived from
as-built code rather than from an engineering standard*, and both rung A and rung C are required to
raise a blocking `Q-nn` saying the "completeness by construction" it confers is **nominal, not
delivered**. A harvested style guide inherits that status precisely: it is a **floor to check
against, never a standard**, and it must self-declare exactly like the references do. Second risk:
the sample is *one* project — "one project is not a population" (survey §9).

### L-5 — Item-catalogue geometry defaults table
**What.** A cached table of what each of the 35 creatable item types looks like bare: default size,
default colours, which property is its "content".
**Rests on.** [MEASURED] survey §10 — created items arrive with sensible per-type defaults (a
rectangle 96×48, a text and a button 160×40) **rather than 0×0**, so a builder does not have to know
a type's geometry to produce a valid object. [MEASURED] write-api §4k — 34 of the 35 creatable types
are valid **bare**, the single exception being a faceplate container with no type.
**Missing.** A single sweep to record it (much of it already sits in the P2 transcript).
**Size.** XS.
**What could go wrong.** Little. Note only that **`GetCreationInfos` overstates creatability by 21 of
56** [MEASURED] write-api §4k, so this table must be built by *trial and cached*, never read from the
metamodel — which is the same finding stated as a build instruction.

### L-6 — Vision-in-the-loop layout iteration
**What.** Render the built screen (W-1 or W-3), show the image to the model, let it correct the
layout, repeat.
**Produces.** The closest thing to how a human designs.
**Rests on.** [SPECULATIVE] entirely. Depends on a renderer existing and being faithful.
**Size.** L.
**What could go wrong.** **The model would be looking at our renderer's output, not at WinCC's.** If
the renderer is approximate (and W-1 says it must be), then vision-guided iteration optimises the
screen against an approximation — converging confidently on something that looks right in the wrong
renderer. That is worse than not iterating, because it manufactures confidence. Only viable on top of
W-3 (a real runtime screenshot), and W-3's feasibility is unestablished.

---

## 5. Group G — Generation

Everything in this group needs ADR-0007 accepted. Ordered roughly cheapest-and-most-defensible first,
which is deliberately **not** the order of how impressive they sound.

### G-7 — Compile-driven bulk retrofit of existing screens
*(Listed first on purpose: it is the generation option with the best ratio, and it is not a design
act.)*
**What.** Apply a mechanical, compiler-identified fix across many existing screens — add the missing
release button to the objects the compiler names, correct a systematically wrong property, re-point a
renamed tag binding across the device.
**Produces.** A closed backlog rather than new content. **The reference device carries 154
"No release button is defined for the object X in screen Y" warnings** [MEASURED] write-api §4e — a
per-object, per-screen finding list the compiler produced itself, on screens authored entirely by
hand.
**Rests on.** [MEASURED] survey §10b — attribute modification on an existing screen with schema-driven
type coercion, applied, validated, saved, read back. [MEASURED] write-api §4e — the compiler names
the object and the screen, so the target list is not inferred.
**Missing.** V-1 (so a bulk edit can be diffed), V-4 (so the compile result can be trusted), and
`hmi-edit-screen` currently addresses one screen at a time.
**Size.** S–M.
**What could go wrong.**
- **No transaction, and a partial failure persists** [MEASURED] write-api §4h. A 48-screen edit that
  throws on screen 30 leaves 29 changed and no record. **Every such command must be written
  re-runnable and idempotent**, and must not treat a failure as "nothing happened".
- **`EngineeringSecurityException` on writes while reads succeed** [MEASURED] survey §10b — operation
  specific, arrives with **no dialog**, circumstantially linked to the Portal editor holding the
  object. A bulk run must expect it mid-way.
- **No before/after.** With no export there is nothing to diff, so "prove the untouched screens
  identical" — the S7 discipline this repo insists on for LAD — has no analogue until V-1 exists.

### G-2 — Diagnostic / IO screens from the tag list
**What.** Generate utility screens: one page per DB or tag table, a table of name + live value + unit,
paged. Commissioning and fault-finding screens, not operator screens.
**Produces.** The screens engineers build by hand and resent building by hand.
**Rests on.** [MEASURED] survey §8 — nothing is `Mandatory`, only `Name` is `Relevant`, so the build
shape is `Create<T>(name)` → set attributes → `Dynamizations.Create<TagDynamization>(property)`.
[MEASURED] write-api §4h — that binding chain executed live.
**Missing.** Only a grid layout, which is the degenerate case of L-2.
**Size.** M.
**What could go wrong.** **This is the cheapest genuine generation win precisely because the layout
problem collapses to a table** — so do not let it become the argument that layout is solved. It is
solved for tables. Second: a 300-row IO page bound to 300 tags is a real runtime load; nothing in
this toolchain measures panel performance, and that is a genuine unknown, not a small one.

### G-6 — Faceplate stamping (instantiate a human-authored type)
**What.** The human authors **one** faceplate type in TIA by hand (fact 3 says they must). The AI
then creates N `HmiFaceplateContainer` items, points each at that type, and binds each to its
instance's UDT.
**Produces.** The reuse story the API otherwise denies us — and it maps exactly onto this project's
existing house style, where per-instance settings live in the instance's UDT **because faceplates
bind the UDT** (`docs/06` C-307, an owner ruling).
**Rests on.** [MEASURED] write-api §2 — `HmiFaceplateContainer` references its type by
`ContainedType : String`, and `ScreenItems` has a second `Create<T>(name, containedTypeValue)`
overload precisely for container types. [MEASURED] write-api §4k — the container **creates**, and its
single compile error was `The referenced faceplate type does not exist. Select a valid faceplate
type` — i.e. the mechanism resolved the reference and found it empty, which is a *working* reference
check, not a broken creation.
Also note the instance side is clearer than it was: `HmiFaceplateContainer.Interface` is an
`HmiFaceplateInterfaceComposition` with `Find(propertyName)` returning an entry whose `Value` is
**settable** — so **placing a faceplate and filling in its parameters is reachable with what already
exists**, and it explains P2's `The referenced faceplate type does not exist` (the container names
its type by string, like every other container) and P3's `TagParameterDynamization` refusal (tag
parameters *are* faceplate interface parameters, and there was no faceplate)
(`docs/notes/openness-hmi-faceplate-library.md` §5, reflection).
**Missing.** **One probe.** Whether `Create<HmiFaceplateContainer>(name, "<an existing type>")` — or
setting `ContainedType` afterwards — actually resolves to a real library faceplate type has
**never been tested**. It is P-1 in §12.
**Size.** XS for the probe; S for the capability if it passes.
**What could go wrong.** If the probe fails, this option evaporates and Group G's equipment-screen
story reverts to L-1's 40-copies problem. Also note that under fact 3 *as measured*, even a passing
probe leaves the *type* to be authored, versioned and maintained by a human — the AI's contribution is
stamping and binding, and that is worth being honest about rather than selling as "AI designs the
faceplate".
**If the faceplate-library reading holds (fact 3's contest), this option grows a second half.**
`LibraryTypeVersion.Edit()` → modify → `Release()`, and `CreateFromDocuments`, would make the *type*
authorable too, and `FindInstances` / `UpdateProject` would make "change the template, push to 40
instances" a supported operation rather than a re-stamp. That is a materially bigger option than the
one written above, and it is **not** what is being proposed here — it is what would need re-scoping
if steps 1–2 of that note's test list come back positive.
**Why it is in my top three anyway:** it is the option where the smallest experiment could collapse
the hardest wall in the menu — and that was true before the faceplate-library note, which only makes
the experiment more obviously worth running.

### G-4 — Alarm objects from the PLC alarm words (FI-35's HMI half)
**What.** Create `HmiDiscreteAlarm` per PLC alarm bit, with class, priority, acknowledgement
behaviour and trigger.
**Produces.** FI-35's actual deliverable, without the spreadsheet hop.
**Rests on.** [MEASURED] write-api §4l P4 — alarm class, discrete alarm and analog alarm all create;
`AlarmClass`, `Priority` (Byte) and `StateMachine` (enum) all set. [MEASURED] survey §6 — the
spreadsheet's single `Class` column collapses what the API models as **three independent axes**
(severity `Priority`, acknowledgement `StateMachine`, per-state appearance `AlarmStatusVisuals`), so
the C-506/C-507 conflation that produced a wrong E-Stop row live is **not expressible** against this
API.
**Missing — and this is a block, not a gap.**
- **Alarm TEXT cannot be written.** [MEASURED] write-api §4l P4 — `MultilingualTextItem.set_Text`
  **threw**, with a language item present. Since C-505 alarm text *is* the deliverable, alarm
  generation through Openness currently **cannot produce the thing it exists to produce**.
- **`RaisedStateTagBitNumber` refuses on a fresh alarm** [MEASURED] same run — probably contextual
  writability (disabled until a trigger tag exists), which if confirmed means alarm creation has an
  ordering requirement the schema does not express.
**The honest workaround today.** The **TIA UI's own `DiscreteAlarms` XLSX export/import** is the only
demonstrated path for the text (`hmi-alarm-generation.md` §1 — a real job did exactly this,
end to end, with a generic Python library). So a hybrid is available and should be named as such:
**structure and classes via the API, text via the workbook** — at the cost of a two-tool workflow and
the workbook's own collapsed `Class` column. There is also a PLC-side find worth ten minutes first:
`Siemens.Engineering.SW.Alarm` provides **first-class XLSX export for PLC alarm texts**
(`PlcAlarmTextXlsxExportOption` etc., survey §6) — whether it produces a better-formed workbook than
hand-writing one, or is aimed only at ProDiag/supervision alarms, was never determined.
**Size.** S once unblocked; **blocked today**.
**What could go wrong.** Beyond the block: the trigger-tag join (A-4's `(connection, plcTag)` rule)
is where a wrong alarm silently points at the *other station's* fault bit — measured false assumption,
`hmi-alarm-generation.md` §5.2. And `Flashing` — how a real HMI shows an unacknowledged alarm — is
one of the **3 of 6 dynamization kinds that refuse** [MEASURED] write-api §4l P3, which compounds it.

### G-5 — Navigation scaffold
**What.** Generate the frame screen with its header band and screen window, the content screens, and
the navigation buttons with their handlers.
**Produces.** The skeleton of an HMI, matching the composition pattern the reference project actually
uses [MEASURED] survey §7 (a frame screen sized to the panel carrying a header strip and a screen
window; content screens sized to the *window*, not the panel).
**Rests on.** [MEASURED] write-api §4l P6 — screen window created, pointed at another screen,
**compile clean** (the only probe phase whose compile passed, because nothing left a dangling
reference). [MEASURED] survey §10b — a `Tapped` handler with a script body created and read back.
**Missing.** S-2 (vetted scripts).
**Size.** M.
**What could go wrong — and this one is serious.** **Screen grouping is programmatically
unreachable.** [MEASURED] write-api §4l P6: `HmiScreenComposition.Create` takes **only a name**, so a
screen cannot be created inside a group through the device-level composition; and [MEASURED]
write-api §2: `Parent` is get-only with no `Move`/`Reparent` anywhere in the assembly, so screens
**cannot be moved between groups** afterwards. Reorganising means delete-and-recreate, which destroys
the screen's contents. **So a generator that emits 40 screens puts all 40 at the root, permanently,
and the engineer cannot tidy them up without rebuilding them.** On a device whose real structure is
5 groups holding 45 of 48 screens, that is not cosmetic. Get grouping right at creation time — except
you cannot, so either the human creates the groups-and-screens skeleton by hand first and the AI fills
them, or the flat-root outcome is accepted explicitly.

### G-1 — Per-equipment detail screens from rung C specs ("rung E")
**What.** The headline option: a **fifth rung** of the structured spec pipeline. A → B → C → D
produces the *control* structure; a rung E would consume the same upstream artifacts and emit the
*operator interface* for the same instances.
**The input artifact already exists and is well-shaped.** `gen/<project>/equipment-specs/<Instance>.md`
carries, per instance: the class, the `fed-by`/`discharges-to` topology, the **IO BINDING table**
(role → signal → EXISTS), the complete `C-nn` control requirements, the `P-nn` plant interlocks, and
a **SETTINGS table with an explicit Owner column marking HMI-owned settings**. That is very nearly a
screen specification already: the IO binding table says what to display, the settings table says what
the operator may change, and the interlock list says what to explain when the equipment will not
start.
**Produces.** One detail screen per instance, bound to that instance's UDT.
**Rests on.** [MEASURED] the whole create/attribute/bind chain (write-api §4h, survey §10/§10b).
[In-repo] the rung C artifact format, which is already a machine-parsed contract
(`converter signal-sweep` and `relation-reconcile` both read it).
**Missing.** All of Group L; V-1; G-6's faceplate answer; and a rung-E artifact contract with its own
mechanical checker, because this repo's evidence is that an artifact without a parser drifts.
**Size.** L — and honestly XL if the layout half is built from scratch rather than templated.
**What could go wrong.**
- **Rung boundaries.** The four rungs exist because *an AI reviewer reading the same register as the
  AI coder is a correlated check* (`docs/evidence/PlantAutoControl-bench-autopsy.md`). A rung E that reads
  rung C and is reviewed by something that also reads rung C reproduces the exact failure the
  pipeline was built to break. **The reviewer of a generated screen must read the built screen (via
  V-1), not the spec it came from.**
- **Rung C explicitly excludes alarms** ("This is a CONTROL spec. **Alarms are out of scope** — a
  separate artifact, defined later"), so rung E cannot get alarm content from it. That "separate
  artifact, defined later" is a real gap sitting between rung C and both G-4 and A-5.
- Fact 3 again: without G-6, 40 instances means 40 hand-maintained copies.

### G-3 — Overview / mimic screen from the P&ID topology
**What.** A plant overview laid out to reflect the process: vessels, pumps and conveyors positioned by
material flow, with connecting lines.
**Rests on.** Rung A's `equipment-topology.md` gives `fed-by` / `discharges-to` per instance —
**a directed graph, ready to lay out**.
**Missing — and it is a structural mismatch, not a tooling gap.** **Rung A deliberately throws
geometry away.** It produces *process relations only* — "no signals, no IO addresses, no tag names,
no booleans" — and its output block is a flow list, not a diagram. The artifact that actually
contains positions is the P&ID document itself, which is not in the pipeline. So a mimic generated
from rung A is a **graph-layout drawing, not a P&ID**, and operators expect a mimic to look like the
plant they know.
**Size.** L.
**What could go wrong.** A plausible-looking mimic that contradicts the operator's mental model of
the plant is worse than a table. Three possible resolutions: (i) [SPECULATIVE] extend rung A with an
optional positional hint field — a change to a rung whose whole discipline is *no lower
abstraction*, so it needs an owner ruling, not a decision made here; (ii) [SPECULATIVE] read the
P&ID's geometry directly with vision, a different capability with its own accuracy problem;
(iii) **[DOCUMENTED, and the best of the three] a machine-readable P&ID — DEXPI** (§9c) carries the
interchange model *including graphics*, so on a job that supplies one the geometry problem
disappears. Note the caveat that goes with it: published "P&ID → SCADA screens" guidance from the
platform vendors describes a **manual** workflow, which is evidence that the automated version is not
standard practice anywhere. **NAMUR NE 150** (§9a) sits in the same input slot for engineering data
more broadly.

### G-8 — Trend / log page generation
**What.** From a list of tags to be logged: create the logging tags, the data log, and a trend page.
**Rests on.** [MEASURED] write-api §4l P5 — data log, alarm log and connection all created cleanly,
and **the compile then enumerated precisely what bare objects lack**
(`Database of the log must be on the same medium…`, `No alarm class is configured for the alarm
log…`). That message list is effectively a **specification for what a generator must set**, which is
an unusually good starting position.
**Missing.** Logging *tags* remain unwalked (write-api §4i gap 3).
**Size.** M.
**What could go wrong.** "Creation is trivial; configuration is the entire task" is the measured
summary. Storage-medium constraints are environment-specific and a generator will get them wrong
without the site's storage design.

### G-9 — Multi-language text population
**What.** Populate item texts and tooltips in the project's languages.
**Status.** **Effectively blocked, same root as G-4.** [MEASURED] write-api §4 — `MultilingualText`
is get-only with **no `Create` on its `Items`**, so you must `Find(language)` an existing project
language; runtime languages cannot be added (`LanguageAndFonts` has no `Create`); and P4 measured
`set_Text` **throwing** on a found item. [MEASURED] write-api §4i gap 7 — multi-language anything is
100% unwalked and every string ever written by this repo has been invariant-culture.
**Size.** Unknown until the same probe that unblocks G-4 runs.

### G-10 — SiVArc as the generation engine; the AI authors its rules and templates
**What.** Do not build a generator. Use Siemens' own **SIMATIC Visualization Architect**, which
already generates Unified visualization from PLC program blocks driven by **generation rules** and
master copies — and put the AI where the difficulty actually is: **authoring, adapting and reviewing
those rules and templates** against a plant's specs.
**Produces.** Screens generated by supported vendor tooling, inside TIA, with the plant-specific
judgement contributed by the AI as *rules a human can read and version*.
**Rests on.** [DOCUMENTED] §9c — SiVArc "automatically creates visualization from program blocks and
generation templates for multiple HMI devices and PLCs", with explicit WinCC Unified support and
SiVArc expressions usable in Unified screens.
**Missing.** A SiVArc licence; nobody here has ever run it; the rule language is unexamined; and it
is unknown how much of the layout problem SiVArc's templates actually solve versus push back onto the
template author.
**Size.** M to evaluate.
**What could go wrong.** It could turn out that SiVArc's templates are as flat and as unreusable as
everything else, in which case it buys nothing but a licence cost. It also sits outside this repo's
tooling entirely — no `openness-cli`, no compile gate we control, no serialiser — so it trades
control for support.
**Why it is in the menu anyway, and why it matters more than its tier suggests:** **this document
owes the owner a "why not just SiVArc?" answer.** The honest one is that SiVArc's rules and master
copies are authored by a human and encode the plant-specific judgement, which is exactly the part an
LLM might contribute. But that answer also sets a floor: **anything bespoke we build has to be better
than SiVArc at something specific, and we should be able to name it before building.** If we cannot,
the correct recommendation to the owner may be "buy the option package".

---

## 6. Group S — Scripting

Event handlers are the behaviour half of an HMI, and their bodies are JavaScript evaluated by the
Unified runtime, not by Openness. **All 42 event-handler types expose `Script : IHmiScript`**
[MEASURED] write-api §4b — every event on every item type can carry code.

The measured position on checking that code:

| Check | What it actually does | Evidence |
|---|---|---|
| `IHmiScript.SyntaxCheck()` | **locates syntax faults precisely** — returned `Unexpected identifier 's' in Line 12 at Col 8` at write time | [MEASURED] write-api §4f |
| device compile | catches the same fault, nested screen → item → error, with line and column | [MEASURED] write-api §4f |
| **name resolution** | **nothing checks it** — `SyntaxCheck()` checks syntax, not names | [MEASURED] write-api §4b trap 2 |

### S-2 — A vetted snippet library for Unified runtime JavaScript
*(Listed before S-1 because S-1 should not exist without it.)*
**What.** A small set of proven, reviewed handler bodies — navigate to screen, write a tag, toggle,
confirm-then-command — with the known traps designed out.
**Produces.** The LAD-pattern-library move applied to HMI behaviour: generation composes proven
snippets rather than writing freeform JS.
**Rests on.** [DOCUMENTED + MEASURED] write-api §4b, which already documents the trap list:
- **`Screen.Items` is not a by-name accessor.** The by-name lookup is `Screen.FindItem(name)`, which
  **is documented and was independently verified** (§9d; it takes an object path, supports relative
  `..`/`.` and absolute `/`/`~`/`//` forms, and excludes screen names from the path). Calling
  `Screen.Items("SomeName")` **aborts the handler at runtime** — and
  this repo's own first probe script did exactly that, and would have failed silently on tap. It was
  caught only because the runtime API was researched *after* the script was written, and the write
  happened to fail on an unrelated Portal wedge before it landed. **That is luck, not process.**
- **There is no `Click`.** Events are enum-keyed per item type and touch-first: `Tapped`,
  `ContextTapped`, `KeyDown`, `KeyUp` (+`Down`/`Up` on buttons), screens use `Loaded`/`Unloaded`,
  controls `Initialized`/`CommandFired` [MEASURED] survey §10b. **Anyone generating HMI behaviour
  from mouse-based web intuition gets this wrong on the first attempt.**
- **`OpenScreenInScreenWindow` does not exist in V20** — absent from the system-function list, so
  older examples are stale.
- **`.Text` means two different things**: a `MultilingualText` in Openness, a plain string at
  runtime. Identical-looking code behaves differently by side of the fence.
- `alert()` is unavailable; `console` is undocumented; **`HMIRuntime.Trace(message, severity)`** is
  the trace route — [DOCUMENTED and verified, §9d] severities `Info(0, default)` / `Verbose(1)` /
  `Warning(2)` / `Error(3)` / `Fatal(4)`, output to the trace viewer.
- **Do not build on iteration.** Our own write-api §4b calls `Screen.Items` "the ITERABLE", but the
  research pass **could not find `Screen.Items` in the V20 JavaScript object-model index at all**, and
  community sources describe `FindItem()` as the only supported way to get a screen-item handle at
  runtime (§9e). Until probe **P-9** settles it, snippets should use `FindItem`/`UI.FindItem`
  exclusively. *The trap above is unaffected either way — it is the advice that survives whichever
  answer P-9 gives.*
**Size.** S.
**What could go wrong.** A snippet library is only as good as its provenance. `docs/07`'s admission
rule applies: a snippet earns its place by having run on a real panel, not by looking right.

### S-1 — Generated event handlers
**What.** Emit handlers as part of screen generation, composed from S-2.
**Rests on.** [MEASURED] survey §10b — a button came back from a fresh read carrying
`on Tapped script: HMIRuntime.Trace(...)`; write-api §4f — a deliberately broken script **failed the
edit command with exit 8** and was caught by compile with line/column.
**Missing.** Whether the compile resolves *names* — e.g. a handler calling
`Screen.FindItem("NotOnThisScreen")` — is **UNTESTED**. Probe P-3 in §12.
**Size.** S.
**What could go wrong.** **The dangerous class is a script that passes every available check and
still does nothing.** `SyntaxCheck()` is explicitly "this parses", never "this works". A handler that
references a nonexistent item is syntactically perfect. Until P-3 answers whether compile catches it,
**generated scripts have no name-level verification at all** and must be treated as unverified
proposals, however green the checks look. Beyond that: an LLM writing JS is writing code that will
run on a live panel in front of an operator, where a blocking loop degrades the interface and nothing
in this toolchain simulates it.

### S-3 — Script audit of existing handlers
**What.** Read every `ScriptCode` on the device and flag: `Screen.Items(` used as a function (the
measured runtime-abort trap), `OpenScreenInScreenWindow` (absent in V20), references to items not
present on the owning screen, navigation to screens that do not exist, and unbounded loops.
**Produces.** A defect class **nothing else on the machine catches** — `Validate()` is silent,
`SyntaxCheck()` passes all of it, and (pending P-3) the compile probably does too.
**Rests on.** Read path + write-api §4b's measured/documented trap list.
**Missing.** Nothing structural; it is text analysis over data the reader already returns.
**Size.** S.
**What could go wrong.** Static analysis of dynamic JS is heuristic — a computed item name defeats
it. Report facts with a stated residue, in the `cross-check` style.

### S-4 — "Behaviour hiding in the HMI" push-back check
**What.** Find control logic implemented in HMI scripts that arguably belongs in the PLC — a handler
that sequences, interlocks, or computes a control value rather than issuing a command.
**Produces.** A finding class with real engineering weight: logic in a panel script is invisible to
every PLC-side review this repo performs, is not in the IR, and does not run when the panel is off.
**Rests on.** S-3 + A-4.
**Size.** M.
**What could go wrong.** It is a judgment call dressed as a check, and the boundary between "issues a
command" and "implements logic" is genuinely arguable. Emit candidates for a human, never verdicts —
and note that this is really a B-1 question (where does the boundary sit?) surfacing as a code smell.

---

## 7. Group V — Verification, serialisation and round-trip

Fact 1 says there is no export. **Every piece of this repo's review discipline assumes a serialisable
representation** — IR diffs, `ir-hash`, golden round-trips, `drift-check`, "prove the untouched
networks identical". ADR-0007 and both surveys agree on the conclusion: **the serialiser is the long
pole, not the generation.**

### V-1 — The canonical serialiser
**What.** Walk the live model and emit deterministic, canonically-ordered text: device → screens (in
group path order) → items (sorted stably) → every attribute with a non-default value → dynamizations
with their resolved read-backs → event handlers with their script bodies.
**Produces — four things at once, which is why it is in my top three.**
1. **A diff surface.** Before/after on any write; the S7 "prove everything else identical" discipline
   becomes possible.
2. **A regression baseline** (V-5) — a committed snapshot that drift can be measured against, the
   `drift-check` idea re-aimed.
3. **A content hash** — the `ir-hash` analogue, keying anything derived.
4. **The review artifact** — the thing a human or an independent reviewer agent actually reads
   (W-4), and the input to any renderer (W-1).
**Rests on.** [MEASURED] survey §7 — `openness-cli hmi` already reads screens (through groups),
items, geometry, properties and dynamizations from a real device, and survey §7 itself calls it "the
first cut of §5.B's serialiser". [MEASURED] survey §10b — screen-level event handlers are read too,
after a gap was found and fixed.
**Missing.** Canonical ordering; full attribute coverage rather than the interesting subset; a
stable, churn-immune identity for items; the hash. The reader exists; the *canonicality* does not.
**Size.** M.
**What could go wrong.**
- **It is a DERIVED artifact, not an authoritative one.** Unlike IR — where hard rule 7 says the IR
  is the only editable surface and the XML is generated — a serialised screen **cannot be edited and
  imported back**, because there is no import. So the discipline does not transfer wholesale: this is
  a *report*, and treating it as a source of truth people edit would be a category error. V-2 is what
  would change that, and V-2 is L-sized.
- **Ordering churn.** The PLC side already learned that TIA reassigns `UId`s and that a Normalizer is
  needed (memory: part-UId volatility). Any HMI serialiser needs the same defence or every read will
  diff against the last one for no reason.
- **Data boundary.** A serialised real device **is** restricted content. It cannot be committed to this
  repo (`docs/13`) — baselines live in the job folder, and the retention rule ("use anything, commit
  nothing") applies unchanged. This constrains V-5 more than it constrains V-1.

### V-2 — An applicable build-plan format (the inverse)
**What.** A text format that can be *applied* to the model, so that `text → apply → re-serialise →
compare` closes a golden round trip.
**Produces.** The only path to the round-trip guarantee this repo relies on everywhere else.
**Size.** L.
**What could go wrong.** An "apply" is not a "replace": attributes the text does not mention keep
their current value, so the text is not a complete description and the round trip does not actually
close. Making it a true replace means delete-and-recreate the screen — which is possible [MEASURED]
write-api §4j — but destroys anything a human added since, and fact 7 says there is no transaction to
protect that. **A round trip that silently discards human edits is worse than no round trip.**

### V-3 — Geometry / semantic gate
**What.** Promote A-7 + A-8 from "report" to "gate" for anything generated.
**Rests on.** Fact 4 — the compile's blindness to geometry is measured twice, so this is the only
place a geometry gate can live.
**Size.** S–M.
**What could go wrong.** It gates *generated* content and cannot reasonably gate hand-authored
content (which would fail immediately and loudly on 48 real screens). Two bars, stated — which is
exactly the repo's existing position that generated code is held to a **stricter** bar than site code
(owner ruling, `docs/06` preamble).

### V-4 — Fix the compile-result reporting
**What.** Count from the message tree, gate on `State`, stop reporting `ErrorCount`/`WarningCount`.
**Rests on.** [MEASURED] write-api §4e/§4f — `WARNINGS: 0` against 156; `ERRORS: 1` against 6.
**Size.** XS. **This is a named defect, not an option** — write-api §4e calls it "a defect to fix
rather than a quirk to document". Nothing in Group G should be built on top of it unfixed.

### V-5 — Serialised regression baseline + drift check
**What.** Snapshot the device, and on every subsequent run report what changed — including changes a
human made in TIA.
**Rests on.** V-1.
**Size.** S after V-1.
**What could go wrong.** Where the baseline lives (see V-1's data-boundary note). And a baseline that
diffs noisily because of ordering churn gets ignored within a week.

### V-6 — Pre-write reference check as a preflight
**What.** Before writing a binding, set it and immediately check the derived read-backs; refuse if
unresolved. The HMI analogue of `tagstatus`/`preflight`.
**Rests on.** [MEASURED] write-api §4h, and `hmi-edit-screen --bind` already reports on this basis.
**Size.** XS.
**What could go wrong.** It requires *setting* the value to test it, which under fact 7 means a
failed check may have already left a partial object. The check must be part of a re-runnable command,
not a dry run.

### V-7 — Evaluate Siemens' own Excel Importer/Exporter as V-1 + V-2
**What.** Before writing a serialiser and an apply-format from scratch, read and run Siemens
application example **109792619**, *"Automatically creating and exporting HMI screen objects in WinCC
Unified"* — an Openness-based tool that **exports** screens, screen objects and their properties to
`.xlsx` and **imports** them back to generate screens, objects and properties in TIA Portal, covering
simple properties, complex properties/compositions, **dynamizations**, **events** and fonts.
**Produces.** Either a shortcut (adopt or port the round trip) or, more likely, a *specification* —
somebody at Siemens has already decided which attributes matter, how compositions serialise, and how
an apply-from-table behaves. That is V-1's attribute-coverage question and V-2's apply-semantics
question, answered by prior art.
**Rests on.** [DOCUMENTED] §9c, with the PDF linked there. Built against Unified V16/V17 in 2022, so
it predates V20 and may not run as shipped.
**Missing.** Somebody to read it. **This is a day's work that could save weeks on the longest pole in
the document, and it should be done before V-1 is scoped, not after.**
**Size.** S.
**What could go wrong.**
- **An `.xlsx` is a poor diff surface** — it is not text, it does not diff in git, and this repo has
  already recorded that a generated workbook is an *unverified artifact* handed to an engineer
  (`hmi-alarm-generation.md` §5). So adopting the format wholesale would import a known weakness.
  Adopt the *attribute model*, not necessarily the container.
- **Version drift**: V16/V17 against a V20 install, and the reflection map is version-specific.
- The most useful outcome may be negative — *"Siemens tried this and here is where it stops"* — which
  is still worth a day.

---

## 8. Group W — Human-in-the-loop, and the open problem

**State the problem plainly, because it is the one this menu does not solve: there is no export and
no renderer, so nobody can look at an AI-designed screen before it exists in a real project.**

On the LAD side the engineer reviews an IR diff — text, offline, cheap, before anything is imported.
The HMI equivalent does not exist and cannot be built out of what the API offers. Every option below
is a partial answer, and it is worth being explicit that **the current honest answer is W-2: build it
into a scratch project and have a human open it in TIA Portal.** That is not a workflow, it is a
bottleneck, and treating it as solved would be the drift this document is supposed to prevent.

### W-2 — Review in TIA Portal (the ground truth)
**What.** Build into the scratch copy; the engineer opens the screen in TIA and looks at it.
**Rests on.** Nothing new. Precedent: hard rule 5's whole model — work against a scratch copy, produce
something for the engineer, never import into the real project.
**Size.** XS.
**What could go wrong.** It does not scale past a handful of screens, it requires the write to have
already happened (so "review before it goes near a panel" is really "review before it goes near the
*real project*"), and the review is a human eyeballing a screen with no checklist. Also note the
measured interaction: opening a screen in the TIA editor is the circumstantial cause of the
`EngineeringSecurityException` that refused a write while reads succeeded (survey §10b) — so the
review loop and the write loop interfere.

### W-1 — Schematic renderer
**What.** Take V-1's serialised screen and draw it — as SVG or HTML — from coordinates, sizes,
colours, text and type names.
**Produces.** An offline, diffable, shareable picture of a screen that exists only in a live model.
Would make G-anything reviewable at a distance, and would make a *change* reviewable as two pictures.
**Rests on.** [SPECULATIVE] on faithfulness; the input data is [MEASURED] readable.
**Size.** M.
**What could go wrong — and this is the crux of the whole group.** **A renderer is a second
implementation of Siemens' rendering semantics and it will be wrong**: fonts and text wrapping,
z-order, per-type default styling, what a gauge or a trend control actually looks like, how a
dynamized property renders when the tag has no value. **A reviewer who approves an approximate
render has approved the render, not the screen.**

The mitigation I would argue for is counter-intuitive: **make it deliberately schematic and
unmistakable for a screenshot** — labelled boxes with type names, binding annotations, a visible grid,
no attempt at realistic styling. A wireframe that cannot be confused with the real thing is *safer*
than a good-looking approximation, because the reviewer knows what they are and are not checking.
This repo has the general principle already, from a tooling defect it caught in itself: a report that
echoes the intent instead of the outcome is **worse than no report** (write-api §4l, the `--in`
no-op). A pretty render of a screen we did not actually verify is that same shape.

### W-3 — Runtime-simulation preview
**What.** Compile, start Unified Runtime in simulation, and capture the screen as it actually renders.
**Produces.** The only faithful image available without hardware — and the only credible basis for
L-6.
**Rests on.** [DOCUMENTED, upgraded from speculation by the §9d research] Siemens states Unified
Runtime is accessed "via any modern web browser – without the need to install separate plug-ins" and
is "based on native web technologies such as **HTML5, SVG and JavaScript**", served over HTTPS from
an embedded web server. **So a deployed screen is DOM/SVG, and a headless browser (Playwright or
equivalent) can screenshot it** — this is now a concrete mechanism, not a hope. A community tool
confirms the client is Chrome-DevTools-Protocol inspectable [COMMUNITY].
**Size.** L, and feasibility is now *plausible* rather than unestablished — but licence, runtime
setup and the scope ruling are all still open.
**What could go wrong.** **It needs a scope ruling before it needs an engineer.** Hard rule 6 forbids
downloads, online edits and tag forcing; a *simulation* on the engineering PC is arguably outside
that, but "arguably" is not a ruling and this document must not make it. Practical risks too:
simulation needs a licence, needs the project compiled, and shows *runtime* behaviour with no live PLC
behind it, so every bound field renders as unavailable — which is exactly the state that hides layout
problems caused by real values.

### W-4 — Independent-reader review report
**What.** A reviewer agent reads the **built** screen through V-1 and reports against the spec — never
reading the generator's own build plan.
**Produces.** The review artifact for a screen, in the shape this repo already trusts.
**Rests on.** V-1 + the repo's own reviewer discipline.
**Size.** S.
**What could go wrong.** **Getting the direction wrong.** The memory rule is explicit —
reviewer→fixer yes, fixer→reviewer never; a fixer reviewing its own fixes is the correlated check
this project exists to avoid. And the pipeline's founding autopsy is precisely that *an AI reviewer
reading the same register as the AI coder is a correlated check, so both failed together*. Therefore:
the reviewer reads the **device**, not the plan; and if it also reads the spec, that is the same
correlated read the four rungs were built to break, so it needs an independent check with it (the
geometry gate V-3, and the boundary check B-2).

### W-5 — The staged workflow
**What.** Dry-run proposal (no Portal contact) → engineer approves → build into scratch → serialise +
render + compile + audit → engineer reviews in TIA → engineer imports.
**Rests on.** [MEASURED] survey §10 — the `--yes` gate refusing **without contacting Portal** turned
out to matter in practice, not just in principle: during a Portal wedge every Portal-touching command
failed while the dry run kept answering in 0.4 s. *A confirmation step that needs a working session
is a confirmation step that stops working exactly when things are going wrong.*
**Size.** M.
**What could go wrong.** The review step is W-2 and W-2 is the bottleneck; a staged workflow that
funnels everything through a manual eyeball does not scale, it just makes the queue visible.

### W-6 — Deletion policy
**What.** Any generated workflow that deletes: (i) only deletes objects it created, enforced by a name
prefix; (ii) compiles in the same breath; (iii) re-reads to confirm absence.
**Rests on.** [MEASURED] write-api §4j — deletion **orphans silently**, only the compile notices, and
"a compile is mandatory after any delete" is stated as not optional. The prefix guard **has actually
refused a real object** in a live run, which is the difference between a safety mechanism and a
comment.
**Size.** XS (it exists, in the probe tooling).
**What could go wrong.** Prefix guards are only as good as their matching. The programme's own
near-miss is the lesson: a whitelist filter built with case-insensitive matching and unanchored
prefixes **leaked two real tag names**, and "a whitelist matched case-insensitively, or on unanchored
prefixes, is not fail-closed — it only looks it" (probe plan, 2026-08-09 log).

---

## 9. Standards, external prior art, and what could be encoded

Web research pass, 2026-08-09. **Sourcing caveat that applies to the whole section:** a large share
of the primary documents here are **image-scanned PDFs whose text could not be extracted**, and
several Siemens pages return only navigation or 403. Where that happened it is said so explicitly.
**Anything marked COMMUNITY is a forum, blog or vendor-marketing claim and is not evidence** —
several such claims were checked and one of them is probably fabricated (see touch targets).

### 9a. The standards that could ground a style guide

**ANSI/ISA-101.01-2015 — *Human Machine Interfaces for Process Automation Systems*.** [DOCUMENTED
structure, UNCLEAR specifics] It "addresses the philosophy, design, implementation, operation, and
maintenance" of HMIs across a **lifecycle model**, in nine clauses (clause 4 is the lifecycle), and
"presents mandatory requirements and non-mandatory recommendations as noted"
(https://blog.ansi.org/ansi/ansi-isa-101-01-2015-hmi-for-process-automation/ ,
https://webstore.ansi.org/standards/isa/isatr101012022). **The clause text itself could not be
retrieved** — the ANSI preview returns 403 — so **treat any claim that ISA-101 mandates a specific
colour, pixel size or navigation depth as unverified.**

The decisive point for this menu is what ISA-101 is **not**: it is not a look-and-feel rule set. It
obliges the *organisation* to produce three artifacts — an **HMI philosophy**, an **HMI style guide**
(the philosophy made concrete and consistent), and an **HMI toolkit** (platform-specific graphic
elements implementing the style guide). ISA published a separate technical report,
**ISA-TR101.01-2022 "HMI Philosophy"**, whose stated purpose is guidance on implementing the HMI
Philosophy subclause (4.2.1) of the standard.

**So ISA-101 does not give A-7 its rules — it says the site must write them down, and makes that a
required, auditable document.** Three consequences, and they change the shape of several options:

- **A-7 has a governance dependency, not a tooling one.** A style checker with no ratified style
  guide behind it is the machine-for-generating-arguments A-7 already warns about.
- **L-4 is the cheap first draft of the very document ISA-101 requires** — and must carry exactly the
  status `references/` self-declares: derived from as-built, therefore a floor to check against, not
  a standard.
- **The reviewer should consume the site's style guide as DATA, not hard-code a rule set.** That
  fits this repo's architecture better than a fixed set of rules would, and it is what the standard
  points at.

**Display hierarchy levels 1–4** (L1 area overview showing deviation from normal → L2 process unit →
L3 single-asset detail → L4 diagnostics/trends). [COMMUNITY] Every secondary source describes the
same four levels and several note they are not a strict traversal requirement
(https://hmilibrary.com/standards/isa-101 , https://processcontrolguide.com/isa-101-hmi-design/).
**The model is generally attributed to the High Performance HMI literature rather than to ISA-101
clause text, and its normative presence in ISA-101.01 could not be confirmed.** What *is* well-formed
regardless of provenance, and encodable: "every screen declares its level"; "a level-1 screen
contains no per-device faceplate"; "navigation reaches level 1 in ≤ N hops" — the last of which is
A-6 with a threshold.

**The High Performance HMI approach** (Hollifield / Oliver / Nimmo / Habibi, PAS). [DOCUMENTED as a
body of work; CONTENT NOT EXTRACTED] ISA hosts a primary overview PDF
(https://www.isa.org/getmedia/06130a38-f7af-4b35-8c9c-2c34f25c1977/The-High-Performance-HMI-Overview-v2-01.pdf)
— **image-scanned, text extraction failed, so not one specific grey value or numeric rule was
verified from a primary source.** The consistently reported principles [COMMUNITY, e.g.
https://www.controleng.com/high-performance-hmis-designs-to-improve-operator-effectiveness/ ]: grey /
muted backgrounds; **colour reserved exclusively for abnormal conditions, never for normal state**;
analog and deviation indicators instead of bare numbers; no 3-D, gradients, bevels or decorative
animation; report by exception.

Which of those a machine could actually check, against the attributes measured readable in survey §8:

| Rule | Checkable? | How |
|---|---|---|
| Palette conformance | **yes** | every `BackColor`/`ForeColor`/`BorderColor` ∈ an approved set — a set-membership test |
| **Colour reserved for abnormal** | **mostly** | a saturated colour on an element with **no dynamization** is decorative by definition. Cheap, and it is the single best check in this list |
| No gradients / 3-D | **partly** | depends which attributes express it per item type; `--schema` would answer per type |
| Analog value in context | **weakly** | "does this item have a populated `Thresholds` collection" is checkable; whether it is *meaningful* is not |
| Display hierarchy level | **no** | a screen's level is intent, not an attribute — it would have to be **declared** (B-1 is the natural home) |
| Consistent layout / grid pitch | **yes-ish** | A-9 plus a pitch-conformance check derived from L-4 |

Note that the best check on that list needs **both halves** of the read surface — the attribute *and*
the dynamization — which is precisely the trap survey §8.3 already recorded from a different angle: a
reader that walks only attributes draws the wrong conclusion.

**Alarm-side standards.** [DOCUMENTED existence; numbers NOT verified]
- **EEMUA Publication 191**, *Alarm systems – a guide to design, management and procurement*, first
  published 1999 with UK HSE input, now 4th edition
  (https://www.eemua.org/products/publications/digital/eemua-publication-191). It is the source of
  the quantitative benchmarks — alarm rates per operator hour, standing-alarm counts, priority
  distribution. **The specific numbers could not be extracted from any primary source this session**
  (the obvious ones are scanned or 403), so **do not quote a distribution figure without checking it.**
  What matters for us is that a priority distribution is *computable* from the HMI's own alarm list —
  alarms are enumerable [MEASURED] survey §7 and `Priority` is readable [MEASURED] write-api §4l — so
  **it is a real, cheap check that belongs inside A-5**, once somebody supplies the target numbers.
- **ANSI/ISA-18.2 / IEC 62682** — the alarm-management lifecycle pair.
- **NAMUR NA 102 "Alarm Management"** (2003, supplemented 2005), the chemical-industry worksheet;
  secondary sources state it is based on EEMUA 191's ideas.
- **NAMUR NE 150 — the brief's premise was wrong, and the correction is useful.** [DOCUMENTED, from
  NAMUR] NE 150 is *"Standardised NAMUR-Interface for Exchange of Engineering-Data between CAE-System
  and PCS Engineering Tools"* — a non-proprietary, bidirectional engineering-data container
  (https://www.namur.net/en/publications/news-archive/ne-150-is-newly-published.html). **It has
  nothing to do with HMI design.** Its actual relevance to this menu is as a candidate **input**
  format — the slot a P&ID or IO table occupies for **G-3** and **B-1** — not as a design standard.

**Touch targets — one anchor, and one claim to refuse.** [DOCUMENTED] ISO 9241-9:2000 recommends a
button size equal to the breadth of the distal joint of a 95th-percentile male finger, **≈22 mm**
(https://www.nngroup.com/articles/touch-target-size/). [LIKELY FABRICATED — do not use] Several blogs
assert that "ISA-101 and IEC 61131 recommend 15 mm minimum, 25 mm safety-critical, 3 mm spacing".
**IEC 61131 is the PLC standard — programming languages and hardware requirements — not an HMI
ergonomics standard**, and the attribution does not survive contact. Recording it here specifically so
that nobody in this repo picks it up from a search result later.

**A worked example of an encodable style guide, correctly labelled.** hmilibrary.com publishes a very
tidy palette (P1 `#D32F2F`, P2 `#F57C00`, P3 `#FBC02D`, P4 `#1976D2`, running `#2E7D32`, stopped
`#9E9E9E`), a 14 px minimum font, and P1–P4 response bands (https://hmilibrary.com/standards/isa-101).
**It cites no clause and these values are that site's own invention.** Useful as a demonstration of
*what an encodable style guide looks like*; **not citable as ISA-101**, and a good illustration of how
easily a plausible-looking rule set acquires a standard's name.

### 9b. Layout precedents worth stealing from

| Precedent | Status | Why it matters here |
|---|---|---|
| **Eclipse Layout Kernel (ELK)** | [DOCUMENTED] https://eclipse.dev/elk/ | The cleanest citable precedent for **L-2**: it "doesn't render the drawing but only computes positions (and possibly dimensions) for the diagram elements". That separation — *a declarative description compiles to x/y/w/h; rendering is someone else's problem* — is exactly the architecture a layout DSL wants, and it is also the right engine for **G-3**'s flow graph. |
| **Cassowary** | [DOCUMENTED] Badros, Borning & Stuckey, *ACM TOCHI* 8(4):267–306, Dec 2001, https://dl.acm.org/doi/10.1145/504704.504705 ; http://badros.com/greg/papers/cassowary-tochi.pdf | The **L-3** route. Apple's Auto Layout is a Cassowary solver in production (constraint *priorities* = Cassowary *strengths*). Extension for adaptive layout: ORC Layout, https://arxiv.org/pdf/1912.07827 . **No published use of Cassowary for industrial HMI was found** — that gap is itself a signal. |
| **Flutter box model / CSS flex & grid / TeX boxes-and-glue** | [general software practice; primary sources not fetched this session] | The declarative-relative vocabulary an LLM is most fluent in, and (TeX) the oldest proof that a text source deterministically produces fixed geometry — including the *overfull box* warning, which is the right model for "content did not fit" rather than silently clipping. |

**On LLMs doing GUI layout — this is the strongest external evidence in the whole brief, and it cuts
both ways.**

- **Render-then-look is an established, measured loop.** [DOCUMENTED] *Coding with Eyes: Visual
  Feedback Unlocks Reliable GUI Code Generating and Debugging* (https://arxiv.org/html/2604.19750)
  builds **VF-Coder** as an explicit *visual perception → dynamic interaction → code refactoring*
  cycle. On InteractGUI Bench with Gemini-3-Flash: task success **21.68 % → 28.29 %** (+6.61 pp) and
  visual score **0.4284 → 0.5584**, where text-only agents gained ~3 pp. **So the screenshot-and-look
  loop roughly doubles the improvement over text-only feedback** — which is real support for **L-6**,
  *provided* what is rendered is faithful. That proviso is W-1's whole objection, and this paper does
  not relieve it: their loop renders the real artifact in a real browser.
- **Constraining generation with an explicit grammar is a published approach** — *UI Layout Generation
  with LLMs Guided by UI Grammar* (https://arxiv.org/pdf/2310.15455) — which is **L-2's thesis with a
  citation behind it**.
- **The failure modes are the ones A-8 checks for**: layout overlap, out-of-bounds placement, text
  escaping its box, structural drift (models restructuring layouts unbidden), and difficulty
  localising small elements. There is a benchmark dedicated to dense-overlap layout,
  https://arxiv.org/html/2509.19282 . [Attribution mixed across papers — treat the enumerated list as
  COMMUNITY unless each is pulled.]
- **The literature's own position is that geometric rule-checking and vision review are
  complementary, not substitutes** — rule-based evaluation is described as limited at catching global
  rendering deviations like disproportionate scaling or misalignment. **For this menu that argues for
  A-8/V-3 *and* W-1/L-6, not either.**

### 9c. What Siemens and the industry already do — and this section changes two options

**Siemens application example 109792619 — "Automatically creating and exporting HMI screen objects in
WinCC Unified".** [DOCUMENTED]
(https://cache.industry.siemens.com/dl/files/619/109792619/att_1355001/v1/109792619_Excel_Importer_Exporter_V2_1_1.pdf ,
V2.0 07/2022, built against Unified V16/V17.) An **Excel Importer/Exporter built on TIA Openness**:
the exporter walks screens, screen objects and their properties out to `.xlsx`; the importer reads the
tables back and **generates HMI screens, screen objects and their properties in TIA Portal**. Its own
contents cover accessing object properties via Openness, simple properties, complex
properties/compositions, **dynamizations**, **events**, fonts, and a list of attributes.

**This is the single most decision-relevant external find, for three reasons.**
1. **It is a vendor precedent for this repo's entire approach.** Siemens' own answer to "how do I
   generate Unified screens programmatically" is *walk the object model attribute by attribute through
   Openness* — **precisely because there is no screen document to import.** That independently
   corroborates fact 1 and survey §8's conclusion.
2. **It is a serialiser and its inverse, already written.** That is **V-1 and V-2**, in vendor form,
   with a round trip (export properties → edit externally → import). It does not make our V-1
   unnecessary — an `.xlsx` is a poor diff surface and this repo has already learned that a workbook
   is an unverified artifact — but **it materially changes V-2's size estimate and its risk profile**,
   because someone has already demonstrated that an apply-from-table path works.
3. It is the concrete basis for **V-7** below.

**SiVArc — SIMATIC Visualization Architect.** [DOCUMENTED] A TIA Portal option package that
"automatically creates visualization from program blocks and generation templates for multiple HMI
devices and PLCs", driven by **generation rules** mapping PLC blocks to HMI objects, and it
**explicitly supports WinCC Unified** ("SiVArc supported objects configured for WinCC Unified devices
are termed as Unified objects… WinCC Unified screens support SiVArc expressions").
(https://support.industry.siemens.com/cs/attachments/109826234/TIAPortalSivarcenUS_en-US.pdf ,
Getting Started 109740350.)

> **This document owes the owner a "why not just SiVArc?" answer, and it is a fair question.**
> SiVArc already does PLC-block-driven screen generation for Unified, from Siemens, supported, inside
> TIA. The honest answer is that **SiVArc is rule-and-template driven, and a human authors the rules
> and the master copies** — which is exactly the part that is hard, exactly the part that encodes
> plant-specific judgement, and exactly what an LLM might contribute. That reframing produces a new
> and rather attractive option (**G-10**): *SiVArc is the generation engine; the AI authors and
> maintains its rules and templates.* It also sets a floor: **any bespoke generator we build must be
> better than SiVArc at something specific, and we should be able to say what.**

**Confirmation of the no-export finding from an independent vendor.** [COMMUNITY, but a serious
source] Copia (git source control for TIA projects) states verbatim that *"Since WinCC Unified is not
supported for export by the Siemens TIA Openness API, WinCC Unified HMI details are not supported for
display/differences"* — while listing Screens, Screen Templates and HMI Tag Tables as exportable for
WinCC **Advanced/Professional** (https://docs.copia.io/docs/git-based-source-control/supported-vendors/siemens/s7tiaportal).
That is the Classic/Unified split this repo measured, corroborated from outside.

> **⚠ One live risk to fact 1, and it must be recorded rather than smoothed over.** The Siemens
> Openness manual's online tree contains pages titled **"Exporting all screens of an HMI device"**,
> **"Exporting all screen templates of an HMI device"** and **"Importing a screen with a faceplate
> instance"** — present in **both the v20 and the v21 trees**
> (https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import/importing/exporting-data-of-an-hmi-device/screens/exporting-all-screens-of-an-hmi-device).
> **Their bodies could not be read** — the site returns navigation only for those deep pages and the
> PDF manual (article 109826886) returns 403. The strong reading is that these are the **Classic**
> SimaticML paths, which is consistent with Copia, with the reflection sweep, and with the fact that
> Siemens' own Unified answer is the Excel/Openness importer above. **But "strong reading" is not
> measurement, and fact 1 is the load-bearing fact of this entire document.** Probe **P-8** below.
> Note also that Siemens *does* document a Unified screen-item surface — the manual carries sections
> tagged "(RT Unified)" for dynamization and for events/scripts on screens and screen items — which is
> object-model-shaped, exactly as measured.

**Community/GitHub — essentially empty, and that is both the opportunity and the warning.** The
`wincc-unified` GitHub topic holds ~21 repos and **only one** touches Openness screen generation:
`tia-portal-applications/TIA-Openness-Create-Synoptic-Unified` (C#, ~5 stars), whose README says
outright *"this is just a prototype right now and it is not possible to export everything yet"*.
Everything else is custom web controls, runtime scripting, or Industrial Edge. There is a Siemens
NuGet helper layer (`Siemens.Collaboration.Net.TiaPortal.Openness.Hmi.Extensions`) offering
`screens.AddOrGet(...)` / `screen.ScreenItems.AddOrGet<T>(...)`, **but its example type names look
Classic, not Unified — verify before citing.** [COMMUNITY]

**Faceplate types — no external evidence either way, and the negative is informative.** No
documentation, example, or community report of creating a Unified **faceplate type** programmatically
was found. Openness documents placing a faceplate *instance*; Siemens' faceplate guidance (109812366)
is engineering-GUI guidance and is scanned/unextractable. Reported Unified faceplate limitations
[COMMUNITY, https://www.plctalk.net/forums/threads/wincc-unified-open-a-faceplate-popup-from-inside-another-faceplate-with-different-udts.148115/ ]:
authorization properties cannot be assigned via `HMIRuntime.UI.OpenFaceplateInPopup()`, that call is
screen-scope only and not callable from inside a faceplate, and passing graphic lists is unsupported.
**Assume faceplate-type authoring is human work until measured** — which is exactly what fact 3 says
from the reflection side.

**Template-bound-to-instance is the industry norm.** [DOCUMENTED practice] Ignition does this via
UDTs + templates + scripting — build the plant hierarchy in a database or spreadsheet, run a script
matching production model → equipment type → datatype → template
(https://corsosystems.com/posts/templates-in-ignition-perspective). Notably, the same vendor's
"P&ID → SCADA screens" guidance describes a **manual** workflow
(https://corsosystems.com/posts/p-and-id-drawings-301), which tells you the automated version is not
standard practice anywhere.

> **The industry's normal solution to HMI generation is exactly the half of fact 3 that is blocked.**
> So the mainstream approach is available to us only as "a human authors the type, the AI stamps and
> binds it" — which is **G-6**, and which is why its XS probe matters more than its size suggests.
> *(And note the convergence: the external finding that everyone else does template-bound-to-instance,
> and the internal finding that faceplates may be library types with a document round trip, point at
> the same conclusion from opposite directions — **the faceplate is the missing reusable component,
> and it is worth one Portal read to find out whether we can have it.**)*
> It also reframes **G-1** honestly: a competent HMI engineer would not hand-build 40 bespoke
> equipment screens, they would build one faceplate and stamp it. **An AI capability that generates
> 40 unique screens is imitating the labour, not the practice.** That is a design objection to put to
> the owner, not a technical one.

**Machine-readable P&ID exists, and it is G-3's missing input.** [DOCUMENTED] **DEXPI** provides a
P&ID interchange model including graphics (https://dexpi.org/wp-content/uploads/2020/09/DEXPI-Specification-1.0.pdf),
with SVG/PDF export and a `GraphicBuilder` that renders to PNG **for verification**. Two things
follow: (i) G-3's "rung A threw the geometry away" problem has a standard-shaped answer, *if* a job
ever supplies a DEXPI P&ID; (ii) DEXPI's own render-a-PNG-to-check pattern is independent support for
W-1's existence — a tool that computes geometry rendering a picture so a human can check it is normal
practice, not a workaround.

### 9d. Rendering outside Portal — the clearest external answer in this section

**WinCC Unified Runtime is web-based.** [DOCUMENTED] Siemens states operators access it via "any
modern web browser – without the need to install separate plug-ins", and that the system is "based on
native web technologies such as **HTML5, SVG and JavaScript**", with a runtime server publishing
screens over an embedded web server and clients connecting over HTTPS
(https://www.siemens.com/en-us/products/simatic-hmi/wincc-unified-software/ ,
https://docs.tia.siemens.cloud/r/en-us/v20/operating-unified-pc-rt-unified/starting-and-displaying-runtime-rt-unified/internet-browsers-for-wincc-unified-pc-rt-unified).
A community tool confirms the client is Chrome-DevTools-Protocol inspectable
(`ploxc/wincc-unified-debug-proxy`, Rust) [COMMUNITY].

**What that does and does not buy.** It means a *deployed* screen is DOM/SVG and therefore
**screenshot-able with a headless browser** — a real, concrete mechanism for **W-3**, replacing "run
it somehow and capture it somehow". It does **not** give an offline renderer: the HTML/SVG is produced
by the runtime engine from the compiled project, not by a redistributable library, and **no
open-source or vendor renderer for Unified screens outside Portal/Runtime was found.**

**And the engineering-time route stays closed on principle.** [MEASURED] survey §8.3 — engineering
screens exist in the project only as live Openness objects, and the compiled runtime image stores
them as **`.rdf`, an undocumented Siemens binary serialisation**; the two large `.zip`s beside them
are web-control bundles, not screens. **Parsing `.rdf` is exactly what hard rule 7 exists to
forbid** — the HMI equivalent of hand-patching SimaticML, binding us to a format Siemens can change
without notice.

**No Openness API for rendering or printing a screen to an image was found**; the only surfaced
techniques were manual Windows screen capture. Assume none exists.

**So the preview architecture the evidence actually supports is a two-tier one**, and it happens to
mirror DEXPI's own pattern: **W-1's approximate schematic render for the fast inner loop, and W-3's
runtime screenshot as the acceptance check.** W-1's "make it unmistakably schematic" argument becomes
*more* important under this split, not less — because a faithful check now exists, the inner-loop
render does not need to pretend to be one.

### 9e. What to verify before anything in §9 is relied on

1. **The "Exporting all screens of an HMI device" Openness doc pages** (v20 *and* v21) — read the body
   in the local copy of manual 109826886 and confirm they are Classic-only. **This is the one live
   threat to fact 1**, which is the load-bearing fact of this document. → probe **P-8**.
2. **V21's "Unified Screen Editor (Next Gen.)"** — a new editor is the most likely occasion for a new
   persisted format, and V21 was never examined (survey §9 already flags this).
3. **ISA-101.01 clause text and the High Performance HMI handbook** — every specific rule found was
   second-hand. If anything in this repo ever says "ISA-101 requires X", somebody has to have read the
   standard.
4. **`Screen.Items` at runtime.** [UNCLEAR — and this contradicts a claim in our own notes] The
   research pass could **not find `Screen.Items` in the v20 JavaScript object-model index**, and
   community sources describe `FindItem()` as the only supported way to obtain a screen-item handle at
   runtime. `FindItem`, `UI.FindItem`, `HMIRuntime.Trace(message, severity)` (severities
   Info/Verbose/Warning/Error/Fatal), `HMIRuntime.UI.ActiveScreen` and `UI.RootWindow` **are** all
   documented and were verified. **write-api §4b states `Screen.Items` is "the ITERABLE" — that is
   currently unverified and may be wrong.** The §4b *trap* (calling `Screen.Items("Name")` aborts the
   handler) is unaffected either way and is the operative advice; but S-2's snippet library should use
   `FindItem` exclusively and should not iterate until this is settled. → probe **P-9**.

---

## 10. Ranking

Two axes that matter here: **value per unit of risk**, and **what unblocks what**. The grouping below
is my judgment, not a measurement.

### Tier 1 — cheap, low-risk, needs no scope grant
Do these regardless of how ADR-0007 goes; several are useful even if it is rejected outright, because
`10-non-goals.md` already allows HMI data extraction and `openness-cli hmi` is read-only.

| Option | Why |
|---|---|
| **P-8 / P-9** | XS, no Portal contact, and each checks a claim this repo has already written down as fact — one of them the load-bearing one |
| **V-4** fix compile reporting | XS, a named defect, and everything else that gates on a compile is wrong until it lands |
| **L-5** item geometry defaults | XS, mostly already in the P2 transcript |
| **V-7** read Siemens' 109792619 | S, and it is prior art for the two most expensive options in the document (V-1, V-2). Do it *before* scoping them |
| **A-1** census artifact | S, and it is the substrate for eight other options |
| **A-2** dangling-binding sweep | S, detector already measured and already implemented in the bind path |
| **A-8** geometry linter | S, fills a hole nothing else covers, no taste content |
| **L-4** harvest the house style | S, converts an unwritten convention into a checkable one, in a day |
| **S-3** script audit | S, catches a defect class every available check is blind to |
| **A-10** `explain-hmi-screen` | S, and it makes screens discussable at all |

### Tier 2 — the unlocks
| Option | Why |
|---|---|
| **V-1** canonical serialiser | the single highest-leverage build: diff + baseline + hash + review artifact + renderer input, and its reader half exists |
| **A-4** PLC↔HMI reconciliation | the substance of FI-18, and the only thing that makes the boundary checkable |
| **A-6** navigation graph | required by B-2 and by any honest reachability claim |
| **B-1** boundary contract | needs no Portal contact at all, and unblocks open register questions today |
| **W-1** schematic renderer | the only offline review artifact available; must be deliberately schematic |

### Tier 3 — write-side, defensible first steps *(all conditional on ADR-0007)*
| Option | Why |
|---|---|
| **G-7** compile-driven retrofit | a *fix* driven by the compiler's own 154 findings, not a design act; each change small and re-verifiable |
| **G-2** diagnostic/IO screens | the one place the layout problem legitimately collapses to a table |
| **G-6** faceplate stamping | gated on an XS probe that could collapse the hardest wall in the menu |

### Tier 3½ — the buy-vs-build question, which should be answered before Tier 4 is funded
**G-10** (SiVArc as the engine, AI as the rule author). Siemens already ships PLC-block-driven screen
generation for Unified. **Any bespoke generator in Tier 4 has to be better than it at something
nameable**, and nobody here has run it. Evaluating SiVArc is cheaper than discovering halfway through
G-1 that the option package would have done it.

### Tier 4 — big, and honest about it
L-1/L-2 (the layout story), G-1 (rung E), G-5 (navigation scaffold, hobbled by the grouping wall),
V-2 (the inverse format — **scope only after V-7**), W-3/L-6 (render-and-look, now with a concrete
mechanism per §9d).

### Tier 5 — blocked or rejected
**G-4** and **G-9** are blocked on the alarm-text refusal, not merely expensive. **L-3** (constraint
solver) I would list to reject: its failure mode is unexplainable output, and every finding in this
repo is required to be arguable.

---

## 11. What nothing in this menu solves

Say these plainly; they are the reasons a "no" to ADR-0007 would be defensible.

1. **Nobody can review an AI-designed screen before it is built.** No export, no renderer, no
   simulator in reach. W-1 is an approximation and W-2 is a human opening TIA. This is a genuine open
   problem, not a tooling backlog item.
2. **Reuse cannot be expressed structurally.** Fact 3 + fact 2. Unless G-6's probe passes, the only
   reuse mechanism is repetition, which is the opposite of this repo's pattern-library premise and is
   named as such in ADR-0007's case against. ~~**⚠ This is the one item on this list with a live
   challenge against it**~~ — **the challenge was tested and DEFEATED, 2026-08-09**: Unified
   faceplates are plain `LibraryType` with no export formats, so types genuinely cannot be authored
   (`openness-hmi-faceplate-library.md`). **This item is now confirmed, not merely measured.**
   §14b argues it is survivable anyway — by putting type authoring on the human side of the boundary
   and instantiation on the AI's — but it is survivable, not solved.
3. **Alarm text cannot be written.** Fact 8. FI-35's use case — the one real delivery job that
   motivated all of this — is blocked on an unexplained refusal, and the workaround is the very
   spreadsheet the API was supposed to replace.
4. **Screen organisation is a one-way door.** Screens cannot be created into a group and cannot be
   moved into one afterwards. A generated HMI is a flat root, permanently.
5. **Hard rule 4 has no clean analogue.** The compile is a real gate for references and scripts and
   blind to geometry, and its own counts lie. "Proven valid before presentation" would have to be
   *redefined* for HMI work, not inherited — which is ADR-0007's crux, stated there and unchanged
   here.
6. **~3% of the surface has been walked.** [MEASURED] write-api §4i/§4g. The base rate for the
   remaining 97% is three consequential surprises per 2%, all of them found by *running* the API
   rather than reading it, and most of them negative.
7. **Nobody else has published a Unified screen generator either** (§9c — one abandoned prototype on
   GitHub, self-described as incomplete). That cuts both ways: it is either an opportunity or a
   signal that the people best placed to do it did not, and **Siemens' own answer was to ship a
   spreadsheet round trip and an option package** rather than a screen format. Both readings are
   available and this document does not choose between them.
8. **A "why not just buy SiVArc?" answer is owed and not yet given.** G-10 states the question; only
   an evaluation can answer it.

---

## 12. Cheap probes that would change this menu

Each is XS, each closes a named unknown, and each would move an option between tiers. None is
authorised by this document; they belong to FI-54's residue and to `docs/13`'s JOB9002 extension.

| # | Probe | What it would settle | Moves |
|---|---|---|---|
| **P-1** | **Superseded — use `openness-hmi-faceplate-library.md` §6 steps 1–2 instead**: enumerate `ProjectLibrary.TypeFolder.Types` and print each type's CLR type name; then `GetSupportedExportFormats()` on any faceplate found. Read-only, one Portal read | whether a *Unified* faceplate is a library type at all — and therefore whether **fact 3** stands | G-6, and potentially facts 1 and 3 together. **The highest-value single read in the document** |
| **P-2** | Alarm text again, in a controlled order: create alarm → set trigger tag → *then* `MultilingualText.Items.Find(<project editing language>).Text` | whether the text refusal is an **ordering** problem or a hard limit | unblocks G-4 and G-9, or confirms fact 8 |
| **P-3** | A handler calling `Screen.FindItem("DoesNotExist")` and a navigation to a nonexistent screen; compile | whether the compile resolves **names** in scripts, or only syntax | decides whether S-1 has any verification at all |
| **P-4** | `RaisedStateTagBitNumber` after a trigger tag exists | confirms/denies contextual writability | G-4's ordering requirement |
| **P-5** | Resolve `Screens` on a screen **group** object rather than on `HmiSoftware`, and create there | whether grouping is reachable by another route | un-hobbles G-5, closes open question 9 |
| **P-6** | The three refused dynamization kinds (`Flashing`, `ResourceList`, `TagParameter`) via any other route | whether alarm-state display is reachable | compounds or relieves G-4 |
| **P-7** | Read `EventHandlers` on every item type across the real device | how much behaviour is hiding in scripts today | sizes S-3 and S-4 with a real number |
| **P-8** | Read the bodies of the Openness manual's *"Exporting all screens of an HMI device"* pages in the **local** copy of article 109826886 (the online tree returns navigation only) | whether they are Classic-only | **fact 1** — the load-bearing fact of this document. No Portal contact needed; this is a documentation read |
| **P-9** | Whether `Screen.Items` exists at all in the V20 runtime object model | our own write-api §4b calls it "the ITERABLE"; the doc index does not list it (§9e) | corrects or confirms a claim in our notes; constrains S-2 |

**Note that P-8 and P-9 need no Portal session at all** — they are documentation reads, and both
check a claim this repo has already written down as fact. They should be first.

---

## 13. What this document is asking for

Nothing to be built. Three things to be **decided**, all of them ADR-0007's:

1. **Does the read/audit group (A, and B-1/B-3) need the ADR at all?** My reading is that it does not
   — `10-non-goals.md` allows HMI documentation as data extraction, and every option in Group A is
   read-only. If the owner agrees, Tier 1 is available immediately and independently of the scope
   question. If the owner disagrees, that is worth knowing before anything is built.
2. **If HMI engineering moves into scope, at what size** — ADR-0007's own question 2. This menu maps
   onto it directly: **option 2 of the ADR** (bounded to alarms + boundary) is Groups A and B plus
   G-4; **option 1** (screens included) is everything, and its true cost is Groups L, V and W, not
   Group G.
3. **What happens to the write commands that already exist**, ADR-0007's question 3. Nothing in this
   document changes their status: they are a capability probe, and CLAUDE.md says so.

And one question ADR-0007 does **not** currently ask, which the research surfaced and which should
probably be added to it:

4. **Buy or build?** Siemens already ships PLC-block-driven Unified screen generation (**SiVArc**) and
   an Openness-based screen-property round trip (**application example 109792619**). Neither was known
   when the ADR was drafted. **If HMI engineering moves into scope, the first question is not "what do
   we build" but "what do these two already do, and what would ours do that they do not".** Both
   evaluations are S-sized (**G-10**, **V-7**), neither needs a scope grant, and both are cheaper than
   discovering the answer halfway through Group L.

---

## 14. A recommended path (added 2026-08-09, at the owner's request)

Everything above this section is deliberately a *menu* — it ranks, but it does not choose. This
section does choose. It is **one opinion, written after the probe programme closed**, and it is
separable from the rest of the document: disagree with it and the menu still stands.

### 14a. Reframe the goal, because "total HMI development" names the wrong obstacle

The programme did not find a capability wall. We can create screens and items, set attributes with
coercion, bind properties to tags, build mapping tables with per-value colours and flashing, attach
JavaScript, delete, and compile against a gate that catches dangling references by name. That is most
of an HMI.

**What is missing is not capability. It is verifiability.** There is no screen export, therefore no
diff, no hash, no golden round-trip, and **no way for a human to review a generated screen before it
reaches a panel** (§11.1). Every ambitious option in Group G is downstream of that, so the build
order should follow it rather than follow enthusiasm.

Stated as a rule: **build the thing that makes output reviewable before building the thing that
produces output.** This is the same instinct as hard rule 4 on the PLC side, applied to a side that
does not inherit it.

### 14b. Take the division of labour the API is forcing on you

> ⚠️ **Corrected 2026-08-09 — the premise below is weaker than stated, and the path is stronger.**
> *"Faceplate types cannot be authored"* was inferred from `GetSupportedExportFormats()` returning
> empty. That inference is **unsound**: `CreateFromDocuments` takes no format argument, and
> `LibraryTypeVersion.Export(FileInfo, ExportOptions)` was never called. **Authoring is reopened, not
> refuted** (`hmi-faceplate-gap-probe.md`). Two harder facts landed alongside it, one helping and one
> hurting:
> - **Helping** — `HmiFaceplateInterface` derives from `UIBase`, so a faceplate parameter is itself a
>   dynamization host. Wiring one uses the mechanism already proven live on ordinary items.
> - **Hurting** — `HmiSoftware` implements none of `IUpdateProjectScope`, `IInstanceSearchScope` or
>   `ILibraryTypeInstantiationTarget`. So **you cannot push a type-version update out to its
>   instances, and cannot even ask where a type is used**, through Openness. "Author once, stamp
>   hundreds" survives; *"and maintain them"* is UI-only. Any plan resting on this spine must budget
>   re-stamping, or accept that maintenance leaves the automated path.
>
> Master copies, the obvious fallback, are **closed for Unified**: no `HmiUnified.*` type implements
> `IMasterCopySource` or `IMasterCopyTarget`.

**Faceplate types cannot be authored — measured, not assumed** (`openness-hmi-faceplate-library.md`).
Screens are flat, one level deep, absolutely positioned. Read together, those two facts look fatal to
reuse, and ADR-0007 says so.

They are not, if the boundary is drawn in the right place:

> **A human authors a handful of faceplate types in TIA, once. The AI instantiates and wires them,
> hundreds of times.**

That is how competent HMI engineering already works. It puts the taste-heavy, layout-heavy,
review-heavy work where a human is good and the API is closed, and the repetitive, error-prone,
tag-wiring work where the AI is good and the API is open. It also **dissolves the layout problem**:
layout *inside* a faceplate is the human's; the AI places boxes on a grid and fills in parameters.
And it maps cleanly onto this repo's existing pattern-library premise instead of contradicting it.

**This should be the spine of any HMI plan here.** It is also the cheapest thing on the list to
falsify: nobody has ever pointed an `HmiFaceplateContainer` at a real faceplate type and compiled.
P2 created the container and the compile said *"The referenced faceplate type does not exist"*, which
is consistent with it working, and proves nothing on its own.

### 14c. The path

Phases are gates, not a schedule. Each ends with something checkable.

**Phase 0 — days. No scope grant needed; do it regardless of how ADR-0007 goes.**

| Do | Why |
|---|---|
| **V-4** fix compile-result counting | XS, named defect. Everything that gates on a compile is wrong until it lands |
| **A-1** census · **A-2** dangling bindings · **A-8** geometry linter | the substrate for eight other options, plus two detectors nothing else covers |
| **L-4** harvest the house style · **A-10** `explain-hmi-screen` | converts an unwritten convention into a checkable one; makes screens discussable at all |
| **V-7** read Siemens' 109792619 | prior art for the two most expensive options in this document. Read it *before* scoping them |
| **NEW probe — faceplate stamping** | point a container at a real type, compile. XS, and it is the keystone of §14b |
| **P-2** alarm text, in a controlled order | the single highest-value unknown left; it is what FI-35 is blocked on |

**Phase 1 — the keystone. `V-1` canonical serialiser, then `W-1` schematic renderer.**
One build yields five things: a diff surface, a regression baseline, a content hash, the review
artifact, and the renderer's input. Its reader half already exists. **Scope it only after V-7** —
Siemens may already have shipped half of it.

**Phase 2 — the boundary, still read-only. `A-4` PLC↔HMI reconciliation · `B-1` contract · `A-6`
navigation graph.** This is FI-18's actual substance, and it is what makes a generated HMI
*checkable* rather than merely produced. The `(connection, plcTag)` join is readable and the naive
name-equality assumption is **measurably wrong** on a real project — that alone justifies A-4.

**Phase 3 — first writes, smallest blast radius first.** `W-6` deletion policy (prefix guard +
mandatory compile — deletion orphans silently) → `G-7` compile-driven retrofit (a *fix* driven by the
compiler's own findings, not a design act) → `G-2` IO/diagnostic screens (the one place the layout
problem legitimately collapses to a table) → `G-6` faceplate stamping at scale.

**Phase 4 — only after answering buy-vs-build.** Evaluate **SiVArc** (`G-10`) before funding a
bespoke generator. Anything built here has to be better than it at something nameable.

### 14d. What I would not build, and what stays blocked

- **`L-3` constraint solver — reject.** Its failure mode is unexplainable output, and every finding
  in this repo is required to be arguable.
- **`G-4`/`G-9` stay blocked** until P-2 answers the alarm-text refusal. P7/P8 removed their *display*
  blocker (flashing works, via mapping tables, on any bound property) but not their *text* blocker.
- **`G-5` navigation scaffold stays hobbled**: screens cannot be created into a group or moved into
  one. A generated HMI is a permanently flat root unless P-5 finds another route.

### 14e. The two things this path does not fix

Said plainly, because a path that hides its own holes is worth less than the menu it came from.

1. **Screen grouping is a one-way door.** No phase above solves it.
2. **Hard rule 4 still has no clean analogue.** The compile gates references and scripts and is blind
   to geometry; a zero-width screen compiles clean. "Proven valid before presentation" would have to
   be **redefined** for HMI work, not inherited. `V-1` + `W-1` + `V-3` is the closest this path gets,
   and it is an approximation — a schematic, not a rendering.

Both are ADR-0007's crux, and this path narrows them rather than removing them. **If the owner's
answer to the ADR is "no", Phase 0 and Phase 2 are still worth doing on their own merits** — they are
read-only, they need no scope grant, and they catch defect classes nothing else on the machine
catches.

---

## 15. The web-tooling route (added 2026-08-09, from external research)

Source: `docs/notes/hmi-web-tooling-research.md` (~1,480 lines, sourced and cited). Everything here is
**[EXTERNAL]** — vendor documentation, published products, academic results — unless it repeats
something this project measured live. **None of it has been run on this machine.** Read it as a
corrected map, not as capability.

### 15a. The wall §14a was built around is lower than it was measured to be

§14a's rule stands: *build the thing that makes output reviewable before the thing that produces
output.* What was wrong was the cost estimate. This programme established, five independent ways,
that **WinCC Unified has no screen export**, and then drew the consequence *"therefore no diff, no
hash, no golden round-trip."*

That consequence does not follow. There is no export **function**; there is nothing stopping a
**serialiser** built on the property walk `openness-cli hmi --screen` already performs. And this is
not a hopeful argument — **two shipped products do exactly that**: Siemens' own Openness-based
exporter (SIOS 109792619, covering simple *and* complex properties, dynamizations, events and fonts,
with a command-driven "export all configured HMI screens"), and a commercial JSON exporter for
V15–V21 claiming full preservation of properties, text and dynamizations.

**It is a format wall, not an inspection wall, and other people have walked around it twice.** That
makes the serialiser the highest-value item on this entire menu: diff, content hash, golden round-trip
and version control all sit behind it, and it needs no new Openness capability — only stable ordering
and a canonical form over a read this tooling already performs.

### 15b. Custom Web Controls are where the web toolchain actually lands

The strongest external finding. A **Custom Web Control** is `manifest.json` + `assets/` + `control/`
(HTML/JS/CSS plus Siemens' `webcc.min.js`), zipped as `{GUID}.zip` and installed by **file copy** into
the project's `UserFiles\CustomControls\`. Documented for V20 and V21 with a Siemens system manual and
a V20-specific application example.

Three properties make it decisive, and each is the exact inverse of the screen surface:

1. **It is plain files.** It diffs, it version-controls, it round-trips. The no-export problem does
   not exist there.
2. **Siemens states it "can be displayed as an independent Web page in any browser"** — so the whole
   generate → render → inspect → fix loop runs **outside TIA Portal**, in milliseconds. That is the
   only way any iterative method survives Openness's multi-minute, non-transactional write cycle.
3. **Inside its rectangle there is a real layout engine.** Unified's flat, absolutely-positioned,
   one-level-deep constraint stops at the control boundary.

The architectural consequence: a Unified screen becomes a **thin placement surface** for a few CWC
rectangles plus native controls, instead of a canvas of two hundred hand-placed primitives.

Costs are real and must not be glossed: a CWC does not travel with a project copy, library storage is
unverified, and on Unified Comfort Panels there is **no `fetch`/`XHR`, no external links and no
debugger**. Whether an *instance* can be placed via Openness is **not established** — one
`hmi --schema` probe settles it.

Related, and cheaper for symbol-level work: **Dynamic SVG** is also file-based and diffable, and
unlike a CWC it *can* live in a library. Worth investigating before committing to CWCs everywhere.

### 15c. The correction that matters most — pixels must not be the gate

§14, and the discussion that produced it, treated screenshot diffing as "the closest thing to hard
rule 4 the HMI side can have." **That is wrong, and wrong in this project's own signature failure
mode.** With no independent ground truth, an approved screenshot baseline is generated from the same
source as the artifact it checks — the **correlated check** that
`docs/evidence/PlantAutoControl-bench-autopsy.md` exists to prevent. It fails precisely when the generator
is confidently mistaken, which is the case that matters.

The layering that follows:

- **The gate is render-free and geometric** — a comparator of *intended* item tuples against
  *read-back* item tuples, plus arithmetic over ~200 boxes (overlap, containment, alignment, reading
  order, minimum sizes). Deterministic, explainable, no thresholds, no baselines, no vision model.
  This is `converter diff` and `converter review`, for screens.
- **Pixels are a tripwire**, never an oracle.
- **A vision model is advisory only** — measured at 66% expert agreement against an 84.8% human
  baseline.

A genuinely *independent* second oracle may exist: the runtime is browser-delivered (HTML5/SVG/JS) and
Siemens documents a screen debugger on the standard CDP port, so the live DOM's geometry could be read
back independently of Openness. That is the one route to breaking the correlation — gated on whether
the runtime DOM carries item identity, which a single DevTools session answers.

### 15d. The design rules are not optional and will not emerge

An LLM steeped in web UI will produce a screen that reviews well and operates badly. The collisions
are specific and citable: grey backgrounds rather than dark mode; colour reserved for **abnormal**
state only — **green-for-running is explicitly "an improper use of color"**; no gradients, 3-D or
photorealism; **no animation except alarm flashing**; analog indicators preferred over bare numbers;
a four-level hierarchy whose top level must *not* look like the plant.

Sizing is quantitative and wrong by default: a 2 m viewing distance implies ~9.3 mm minimum character
height ≈ **44 px on an MTP1900**, roughly **4× larger** than the 10–11 pt vendor style guides specify.
WCAG's 4.5:1 is derived for self-selected reading distance and does not transfer unexamined.

Two traps worth naming. **Siemens argues both sides** — its Template Suite commits to flat design with
no 3-D effects, while its own Unified training material teaches binding a tag to an animated flame,
colouring pipes by contents, and rotating a fan. And the top-ranking public "ISA-101 palette"
republishes **Google Material Design hex values** verbatim. Neither an LLM's priors nor a plain web
search lands on the right answer.

The architecture for fixing that already exists in this repo: written-down, cited rules plus a
mechanical checker — `docs/06-lad-conventions.md` and `converter review`. The HMI side needs the same,
storing thresholds in **millimetres** and deriving pixels per panel.

### 15e. What this does to §14's phases

- **Phase 1 changes shape.** It was "serialiser + renderer". It becomes **serialiser + comparator +
  geometric linter**, and the renderer drops out of the critical path entirely — rendering is for
  humans, the gate is arithmetic.
- **A new Phase 0 item:** decide CWC-first versus native primitives, because that determines whether
  the layout problem is ours at all. Two cheap probes settle it — an `hmi --schema` check for a
  custom-control creatable type, and whether a CWC instance can be placed via Openness.
- **§14b's spine is unchanged in direction but now carries a measured maintenance cost** — see the
  correction box in §14b.
- **Buy-before-build moves earlier.** Siemens' free Excel exporter and the free tier of the commercial
  JSON exporter should be **tested for round-trip fidelity before item 1 is written from scratch**. If
  either holds up, the highest-value build becomes an integration instead.

### 15f. What is rejected, and the honest limits

Essentially the whole design-to-code industry is the **wrong direction**: those tools exist to convert
flat-absolute layouts *into* nested-responsive code, and this target needs the inverse. Cloud visual-
testing SaaS is rejected before any technical argument — uploading deployed plant screens collides
with `docs/13-data-boundary.md`. Siemens' own **iX design system** is rejected *as an authority*: it is
MIT, genuinely Siemens, and a design system for industrial **web apps** — accent colours, gradients,
dark mode, no ISA-101 claim — which amounts to a Siemens badge on exactly the vocabulary to steer away
from. And 🔴 **`toHaveScreenshot` does not exist in Playwright .NET or Python**; only the Node runner
has it.

**Limits of this section.** None of it has been run here. The CWC route rests on vendor documentation,
not on a control this project built and installed. Five Siemens PDFs remain unread behind an anti-bot
wall — retrieving them by hand is the highest-yield hour available. The fidelity claims of both shipped
serialisers are **vendor marketing until tested**. Nothing in §15 should be promoted from [EXTERNAL] to
measured without a probe of its own.
