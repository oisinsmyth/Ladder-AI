# HMI capability record — what this tooling can and cannot do, as measured

**Date: 2026-08-09.** One page, so nobody has to reconstruct it from eleven sections of
`openness-hmi-write-api.md`. Every line is either **MEASURED** (run live against a real WinCC Unified
panel, read back from a fresh process) or explicitly marked otherwise. Detail and evidence live in
the documents listed at the bottom; this is the index, not the record of record.

> ## Scope — read before using any of this
>
> ✅ **ADR-0007 WAS ACCEPTED 2026-08-17** (`docs/adr/adr-0007-hmi-engineering-scope.md:3-5`). HMI
> engineering **left** `docs/10-non-goals.md`'s "Not now" list the same day (`:21-23`) and is active
> work at full screen-authoring size. ⚠️ **This block read *"HMI engineering is a NON-GOAL … pending
> ADR-0007, which is Proposed and undecided"* until 2026-08-23 — six days after the decision, and
> three delivered waves later.** Corrected rather than overwritten: this is the page written so
> *"nobody has to reconstruct it from eleven sections"*, so a stale scope line here is reconstructed
> onward by everyone who trusts it.
>
> 🔴 **WHAT THAT DOES *NOT* CHANGE: THIS PAGE IS STILL NOT THE PROGRAMME, AND STILL NOT A LICENCE.**
> Two limits survive the ADR intact and matter more now that the scope line no longer stops a reader
> at the door:
>
> - **Everything measured below is a UNIFIED capability probe**, run under `docs/13-data-boundary.md`'s
>   JOB9002 write extension against a **scratch copy**, using invented `ZZ_AI_*` names only. Nine
>   `hmi-*` commands exist and **five of them write**.
> - 🔴 **THE ACCEPTED PROGRAMME TARGETS CLASSIC BASIC, WHICH IS A DIFFERENT API.** Classic and Unified
>   are disjoint Openness surfaces sharing no types — *Classic is a file pipeline with no screen
>   object model; Unified is an object model with no file pipeline.* **A Unified measurement on this
>   page is not evidence about the Classic path**, and the Classic path is the one being built. Its
>   record is `hmi/PLAN.md` and `hmi/wave-1-results.md` … `wave-3-results.md`; its tooling is
>   `src/hmi-cli`; its authoring agent is `hmi-designer`.
>
> **Read this page as the measured Unified surface. Read `hmi/` for what the programme is doing.**

---

## 1. What works

### Read — proven, unrestricted, and the safest thing here

| Command | What it gives you |
|---|---|
| `hmi <project> [--screen <name>\|*] [--schema]` | device tree and family (Classic/Unified); screens **including nested groups**; per-screen items with position/size; per-property dynamizations with the tag **and** PLC-tag join; event handlers with script previews; counts of tags, alarms, alarm classes. `--schema` dumps creatable item types and every attribute's access mode + **create-relevance** |
| `hmi-inventory <project> [--kind K]` | census across all object kinds; flags surviving `ZZ_AI_*` artifacts |
| `library <project> [--master-copies]` | project library: types with **CLR class name**, consistency status, `GetSupportedExportFormats()`, and every version with state/default flag |
| `portal-status` | Portal process health; never attaches |

**`--schema` is the substitute for a screen XML.** Unified has no screen export, so the metamodel
(`GetAttributeInfos`/`GetCreationInfos`) is the only self-description — and it tells you things an
exported example could not, like which attributes are `Mandatory` vs `Relevant` at create time.

> ⚠️ **Qualified 2026-08-09: it self-describes the USED surface, not the CREATABLE one.** `--schema`
> lists all 56 creatable type *names*, but dumps **attributes only for types that already appear on
> a screen in that project** — 8 of 56 here. It could therefore say nothing at all about
> `HmiFaceplateContainer.ContainedType`, the exact attribute a faceplate probe needed, because no
> existing screen used one. Where the metamodel is silent, the only way to learn an attribute is to
> create the object and try.

### Write — proven end to end, each verified by read-back from a fresh process

| Object | Capability | Command |
|---|---|---|
| Screens | create, delete; groups create; screen windows create and target a screen | `hmi-create-screen`, `hmi-new`, `hmi-delete` |
| Screen items | **35 of 56** types instantiate; add to existing screen; delete | `--item`, `--add-item`, `--delete-item` |
| Attributes | set with type coercion, applied value reported **with its converted type**; **nested paths** reach into dynamizations | `--set`, `hmi-set` |
| Events | attach/update handlers with JavaScript bodies; `SyntaxCheck()`; delete | `--event`, `--delete-event` |
| Tags | tag tables and tags; bind property→tag; replace and delete bindings | `hmi-create-tag`, `--bind`, `--delete-bind` |
| Dynamizations | **5 of 6 kinds**: Tag, Script, Expression, Flashing, ResourceList | `--bind-kind` |
| Mapping tables | value→colour ranges with **per-entry flashing** — the real alarm-display mechanism | `--map`, `--map-clear` |
| Alarms | classes, discrete, analog; `Priority`, `StateMachine`, `AlarmClass` settable | `hmi-new`, `hmi-set` |
| Data plumbing | data logs, alarm logs, connections | `hmi-new` |
| Any of 80 kinds | generic metamodel-driven create/delete/set by composition name | `hmi-new`/`hmi-delete`/`hmi-set --kind` |
| **Faceplate INSTANCES** | **create a container, point `ContainedType` at a human-authored library type, compile clean, and set its parameters.** The instance **adopts the type's own geometry**. Needed no new tooling — 2026-08-09. Reads back **with the resolved version** (`V0.0.11\<Name>`) and the full parameter list | `--item HmiFaceplateContainer`, `--set …ContainedType=`, `--set …Interface[n].Value=str:…` |
| **Event-handler SCRIPTS** | read the **full body** of every handler. Until 2026-08-10 all 274 in the reference project reported empty — the preview took the first line rather than the first non-blank one, so the walker described the skeleton and omitted the animal. **On a real Unified project the behaviour is almost entirely here** (274 handlers, zero script modules) | `hmi --screen … --scripts` |
| **Library type DOCUMENT** | `LibraryTypeVersion.Export` runs and writes a file — **but for HMI types the payload is metadata only** (author, version, empty comment). Reports how the call was bound, and exits 7 if nothing is written | `library --export-version <T> [--version <v>] --out <dir>` |
| Gate | compiles the HMI device | `hmi-compile` |

### The gate

**The device compile is a genuine reference-integrity gate**, and it earned that **six independent
times** — it names dangling tags *by property*, locates script syntax errors by line and column, and
for a bare alarm or log enumerates exactly which fields are missing, precisely enough to serve as a
specification for a generator.

**It also gates FACEPLATE WIRING** (2026-08-09): both the type reference and the parameter binding,
located screen → item → property — e.g. *`Interface.IO: The object "…" at the property "IO" does not
exist.`* For a faceplate-first architecture this is a real analogue of hard rule 4, because what the
compiler checks is exactly what a generator produces. It does **not** gate layout or aesthetics.

**Sharpened 2026-08-10, in both directions.** *Stronger* than recorded: the check is **structural**,
not existential — it compares the bound tag's user-data-type structure *and version* against the
interface's, and states the remedy. *Weaker* than recorded: **an entirely unwired parameter compiles
clean.** So the compile catches *wrong*, never *missing* — the PLC side's `undriven-scan` lesson
repeating exactly. That check is ours to build, and is now buildable because the read-back reports
`(unset)`. **Only the CONTAINER route is checked at all**: a scripted `OpenFaceplateInPopup` passes
its tag as a string inside JavaScript, so nothing verifies it until a live panel runs it.

**`Validate()` is NOT a gate** — it accepts a zero-pixel-wide screen. It is a per-property checker
and structurally incapable of cross-object questions.

**The compiler's own counts lie in both directions** (reported 0 warnings against 156; 1 error
against 6). Gate on `State`; count from the message tree.

---

## 2. What does not work — measured, not assumed

| Limit | Consequence |
|---|---|
| 🔴 **`MappingTableEntrySimple` CRASHES TIA Portal** | Forbidden. Use `MappingTableEntryRange`. Three occurrences with controls isolating it from attributes and target |
| **Alarm text cannot be written** — `MultilingualTextItem.set_Text` throws | Blocks FI-35's alarm generation. The spreadsheet route remains the only demonstrated path for text |
| **Deletion ORPHANS silently** | Bindings survive pointing at nothing; only the compile notices. **A post-delete compile is mandatory** |
| **21 of 56 item types refuse**, with no reason given | The creatable set must be established by trial and cached; `GetCreationInfos` overstates by 60%. ⚠️ **Put in proportion 2026-08-10:** a full sweep of the reference project (49 screens, 1,404 items) uses **12 item types, and all 12 are creatable**. The refusals fall entirely outside what a real plant HMI uses |
| **No Unified screen export FUNCTION** — confirmed **five** ways | ⚠️ **The consequence originally recorded here — *"no diff, no hash, no golden round-trip"* — was WRONG (2026-08-09).** A **serialiser over the property walk `hmi --screen` already performs** recovers all three, and there are **two independent existence proofs**: Siemens' own Openness-based exporter (SIOS 109792619, simple *and* complex properties, dynamizations, events, fonts) and a commercial JSON one for V15–V21. This is a **format** wall, not an **inspection** wall. See `hmi-web-tooling-research.md` |
| **No faceplate document round trip via `ExportAsDocuments`** | ⚠️ **"Types cannot be authored" is NOT established (2026-08-09).** `GetSupportedExportFormats()` is empty — but `CreateFromDocuments` takes **no format argument**, and `LibraryTypeVersion.Export(FileInfo, ExportOptions)` exists and was **never called**. P10's inference linked two calls that share no parameter. **Reopened**; see `hmi-faceplate-gap-probe.md` |
| **Master copies are unavailable on Unified** | `MasterCopyComposition.Create(IMasterCopySource)` exists, but **none** of the 45 `IMasterCopySource` and 30 `IMasterCopyTarget` implementers is under `HmiUnified.*`, and `HmiScreenComposition` has no `CreateFrom`. The obvious fallback to faceplates is **closed**. Classic HMI has the full surface |
| **Unified cannot propagate a type update, or find a type's instances** | `HmiSoftware` implements none of `IUpdateProjectScope`, `IInstanceSearchScope`, `ILibraryTypeInstantiationTarget` — classic `HmiTarget` and `PlcSoftware` implement all three. Type-version propagation and instance discovery are **UI-only**. A real cost to any "author once, stamp hundreds" path |
| **Screens are FLAT** — one level deep, absolutely positioned | All layout intelligence would be ours |
| **No transaction** | A failed command keeps what it already did. Commands must be re-runnable |
| Screens cannot be created into a group, or moved between groups | Grouping is programmatically unreachable |
| Bitmask entries come only from `Create(BitDynamizationType)` | And **cannot be deleted** |
| `TagParameterDynamization` refuses **even INSIDE a resolved faceplate's parameter** | Tested 2026-08-09 on a container bound to a real type — the negative control P7 could never run. P7's conclusion survives its first genuine test, and **what the kind is FOR remains unknown**: the refusal names no reason |
| **Faceplate parameter writes need an explicit `str:` tag** | A bare value fails with `FormatException: String was not recognized as a valid Boolean` — on a property whose own read-back reports `String`. Misleading enough to cost a round trip; worth a usability fix |
| **An UNWIRED faceplate parameter compiles clean** | The compile catches *wrong*, never *missing*. A generated screen of correctly-typed but unconnected faceplates passes the gate looking finished. The PLC side's `undriven-scan` lesson repeating — that check is ours, and is now buildable because the read-back reports `(unset)` |
| **A scripted `OpenFaceplateInPopup` is NOT statically checked at all** | Its type name, version and tag are strings inside JavaScript. Syntax is checked; identity is not. A wrong tag surfaces only on a live panel — the cost of the popup route's diffability |
| **Faceplate TYPES cannot be authored** | Not because content is withheld: `LibraryTypeVersion.Export` emits a `ContentObject` for PLC types and **nothing** for any plain `LibraryType`, faceplates *and images* alike. The content is not in the library object — it lives in the project's binary `.rdf` store (`hmi-rdf-store.md`). **Openness ships no content serialiser for HMI-side library types** |
| **Classic HMI: zero live contact** | No classic device exists locally, and adding one is its own non-goal |

**What DOES round-trip on Unified:** tags, text lists and script modules all carry
`DirectoryInfo`-based `Export`/`Import`. So *"script modules and text lists cannot be authored"* —
stated in earlier notes — **is wrong**: the constructor is a document, not a `Create`. **UNVERIFIED
— none of these round trips has been run.**

---

## 3. The rules that cost the most to learn

1. **A refusal is evidence about that CALL, never about the capability.** The message never says why.
   Twice this project generalised from a single refusal and was wrong — and once the capability
   genuinely was absent. **Vary the target, and pair every negative result with a negative control.**
2. **`Create` is not the only constructor.** `Import`, `CreateFromDocuments` and `CreateFrom` all
   author things. Four times, "X cannot be done" meant "X was not found where I looked."
3. **Report the outcome, not the intent.** Three tool defects in one day were all this shape: a
   discarded `--in` reported as success, refusals counted as changes, partial refusal exiting 0.
4. **Writability is contextual.** The schema predicts what *may* be writable, not what is writable
   *now* — a fresh tag's `HmiDataType` throws `Set is not allowed for disabled fields`.
5. **Every rebuild of `openness-cli` needs a fresh Openness approval** — the whitelist is keyed on
   `(Path, FileHash)`. The refusal has two faces, a silent hang (exit 3) and a thrown
   `EngineeringSecurityException` (exit 2); **on a rebuilt binary both mean "needs approval"**, not
   contention. `dotnet test` on `openness-cli.sln` IS a rebuild.
6. **Never pipe `openness-cli` output** — it launches Portal as a child inheriting stdout, so the
   pipe outlives the command and hangs forever. Redirect on the outer invocation.

### Added 2026-08-09/10, each paid for in this programme

7. **An empty ADVERTISEMENT is not a REFUSAL.** `GetSupportedExportFormats()` returning empty was
   treated as a gate from P10 onward and **never once invoked** to see whether it actually refuses.
   Rule 2's sharper form: before concluding a capability is absent, **call the thing and read the
   throw** — a refusal that names a reason is data; an assumption never is.
8. **The negative control must span KINDS, not just instances.** Faceplate export looked like "HMI
   content is withheld" until the *same call* was run against a PLC type (content present) and an
   **image** type (content absent). One comparison collapsed three suspected walls into one fact:
   the CLR class has no content serialiser. **When a result looks type-specific, vary the type.**
9. **Anonymisation that preserves structure can still destroy the category the next question turns
   on.** P10's evidence rendered every library type as `HmiType_n`, erasing *faceplate vs image*. A
   later agent read it, saw three `Consistent` types, and recommended probing a "Consistent
   faceplate" — a set that does not exist. **Keep the KIND, erase the NAME.**
10. **Attribute a failure to the item that failed, not the first one in the list.** A create of two
    container items threw `'ContainedTypeValue' parameter is missing`; it was reported here as the
    faceplate container's refusal. It was the **Custom Web Control** container's — the faceplate had
    succeeded. One cheap read-back settled it. **With no transaction, always read what survived.**
11. **A preview that shows nothing reads as "there is nothing".** The script preview took
    `Split('\n')[0]` rather than the first non-blank line, so **274 of 274** handlers reported a
    script and displayed empty. The walker described the skeleton and omitted the animal, silently,
    in every read this project had ever done. **A field that is empty for 100% of real data is a
    defect, not a finding.**
12. **Read how the project was actually BUILT before recommending an architecture.** Faceplate
    *containers* were probed for two days because that is what the API surfaces. The reference
    project contains **zero** of them — it opens faceplates as popups from JavaScript. The human's
    choice was better evidence than the API's shape.
13. **Read a truncated log as truncated.** `git log --oneline -8` was misread as a complete history
    and produced a false "master lost the work" alarm. `merge-base --is-ancestor` is the authority;
    a log excerpt is not.

---

## 4. Coverage, honestly

| Axis | Walked | Of |
|---|---|---|
| Distinct API members invoked | ~140 | 4549 (**~3%**) |
| Creatable kinds created | 15 | 80 |
| Screen-item types | **35 created / all 56 attempted** | 56 |
| Dynamization kinds | **5 created / all 6 attempted** | 6 |
| Deletions | ~20 across 9 kinds | 184 types |
| Event values attached | **2** | 246 |
| Library type kinds export-tested | **3 of 3** (PLC / faceplate / image) | 3 |

The "all attempted" rows are the useful ones: those refusal sets are **facts**, not gaps.

> **Put the item-type coverage in proportion (2026-08-10).** A full sweep of the reference
> project — **49 screens, 1,404 items** — uses **12 item types, and all 12 are creatable**:
> Text 421, Circle 301, Rectangle 254, Button 147, Line 94, Polygon 84, GraphicView 41, IOField 38,
> ScreenWindow 10, ToggleSwitch 8, SymbolicIOField 3, AlarmControl 3. **The 21 refusing types fall
> entirely outside what a real plant HMI uses.** That limit had been reported as a headline
> constraint; it is closer to a footnote.
>
> The same sweep found **274 event handlers and zero script modules** — so on a real Unified project
> the behaviour is almost entirely in handler scripts, which is the axis that *does* matter and which
> was unreadable until 2026-08-10.

---

## 5. Owed, and not claimed

### The live leads, in priority order (2026-08-10)

1. **`CreateFromDocuments` with a hand-authored `ContentObject` — UNTESTED, and the best remaining
   route to faceplate type authoring.** Export and import are separate code paths; nothing requires
   both to exist. Two real worked examples of the wrapper schema are now in hand, one with a
   `ContentObject` (PLC) and one without (faceplate), from the same TIA version.
2. **`library --probe-documents <T> --out <dir>` — BUILT, UNIT-TESTED, NEVER RUN.** It invokes every
   `Export*` overload including `ExportAsDocuments` with an empty format list, and reports each
   binding and outcome. Blocked only on an approved binary — see the FI-68 self-approve route, which
   may remove that blocker entirely.
3. **`HmiCustomWebControlContainer` placement — code written, never run.** It requires the
   two-argument `Create<T>(name, containedType)` overload, which `openness-cli` now supports. Running
   it turns the Custom Web Control route from documentation into measured capability.
4. **`.rdf` hash idempotence under edit-and-revert.** Untouched-object stability is measured; whether
   editing a screen and reverting restores identical bytes is not. Decides whether the invariance
   claim is "provably identical" or the weaker "provably untouched".

### Still owed

- The nested `--set` fix is **unit-tested but never live-verified**.
- **A FULLY wired faceplate compiling clean was never achieved.** One binding was accepted; the other
  failed because the reference project's faceplate types are **stale against the current motor UDT**
  — every faceplate reports `DefaultVersionInconsistent` while every PLC type reports `Consistent`,
  which is the same fact the compile error states. A project-maintenance matter, not a tooling limit,
  and **not touched** because it is restricted content.
- **An "undriven faceplate parameter" check** — the compile will not catch it and nothing else does.
- **Interface arity is known for two faceplate types only** (2 params and 1). An out-of-range index
  reports arity for free, so the remaining seven are cheap.
- **Alarm text still cannot be written** (`MultilingualTextItem.set_Text` throws) — unchanged, and
  still the blocker for FI-35's alarm generation.
- Mapping-table round-trip **losslessness** is untested.
- The tag / text-list / script-module `Export`/`Import` pairs have **never been run**.
- Why `set_Text` refuses, and whether `RaisedStateTagBitNumber` is merely contextual — **unknown**,
  one targeted probe each.
- **Nobody can review an AI-designed screen before it reaches a panel.** ⚠️ **Reframed 2026-08-09:**
  the original wording — *"no export, no renderer, no diff"* — treated this as a capability limit. It
  is not. The serialiser that recovers diff/hash/round-trip is **unbuilt, not impossible**, and two
  shipped products prove it works. Still the open problem; no longer an open *question*.
- ~~**Faceplate INSTANTIATION has never been attempted**~~ — **DONE 2026-08-09, and it WORKS.** See
  `hmi-faceplate-gap-probe.md`'s verdict box. Left visible because the gap was real for a day and the
  path rested on it unmeasured for longer than that.
- **Faceplate interface ARITY is known for exactly one type** (one parameter, named `IO`, wanting an
  existing object). The other eight are unread — and an out-of-range index reports arity for free.
- **`HmiCustomWebControlContainer` requires the two-argument `Create<T>(name, containedType)`** —
  measured. Tooling support is written and unit-tested but **has never been run**; running it is the
  probe that would turn the Custom Web Control route from documentation into capability.
- **`LibraryTypeVersion.Export` has never been called.** It is the untried route to a faceplate
  document, and P10's refutation did not touch it.
- Every faceplate type in the reference project reports `DefaultVersionInconsistent` while every PLC
  type reports `Consistent`. Whether that is normal or a real finding about the project is
  **unknown** and worth an engineer's eye.

---

## 6. Where the detail lives

| Document | What it holds |
|---|---|
| `docs/notes/openness-hmi-write-api.md` | the capability map, §4a–§4n, with corrections visible in place |
| `docs/notes/openness-hmi-api-survey.md` | the read survey; §8 is "decode the XML — there isn't one" |
| `docs/notes/hmi-capability-probe-plan.md` | the probe programme, phases P1–P10, questions answered in place |
| `docs/notes/openness-hmi-faceplate-library.md` | the faceplate hypothesis and its live refutation — **whose reasoning is now known to be unsound**, see the next row |
| `docs/notes/hmi-faceplate-gap-probe.md` | what P10 did **not** test: instantiation (untested, needs no new code), the untried `LibraryTypeVersion.Export`, master copies closed for Unified, and the instance-discovery cost |
| `docs/notes/hmi-web-tooling-research.md` | the external-research answer to "how far does web tooling carry over": **Custom Web Controls**, the browser-delivered runtime, the two shipped screen serialisers, ISA-101 vs web-design defaults, and a 39-item build/buy/reject ranking |
| `docs/notes/hmi-rdf-store.md` | **why there is no screen export** — Unified stores each screen and faceplate as its own binary `.rdf` in the project's HMI store, so no document exists to hand out. Also: a change rewrites **only** its own object's file (measured over ~15 save cycles), which supplies **per-screen invariance checking without decoding anything** |
| `docs/notes/hmi-ai-design-options.md` | 47 options for using this in engineering, ranked — an options menu, not a plan. **§14 is a recommended path**, added separately and separable from the menu |
| `docs/evidence/hmi-capability-probes.md` | transcripts, redacted/anonymised per `docs/13` |
| `docs/adr/adr-0007-hmi-engineering-scope.md` | ✅ **the decision — ACCEPTED 2026-08-17**, not open. *(This row read "the open decision" until 2026-08-23.)* |
| `hmi/PLAN.md` + `hmi/wave-1-results.md` … `wave-3-results.md` | **the accepted programme and what it has actually run — CLASSIC BASIC.** Not on this page, and not measurable from it |
