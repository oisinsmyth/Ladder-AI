# HMI capability record — what this tooling can and cannot do, as measured

**Date: 2026-08-09.** One page, so nobody has to reconstruct it from eleven sections of
`openness-hmi-write-api.md`. Every line is either **MEASURED** (run live against a real WinCC Unified
panel, read back from a fresh process) or explicitly marked otherwise. Detail and evidence live in
the documents listed at the bottom; this is the index, not the record of record.

> ## Scope — read before using any of this
>
> **HMI engineering is a NON-GOAL** (`docs/10-non-goals.md`), pending **ADR-0007**, which is
> **Proposed and undecided**. Everything below is a **capability probe** run under
> `docs/13-data-boundary.md`'s JOB9002 write extension, against a **scratch copy**, using invented
> `ZZ_AI_*` names only. Nine `hmi-*` commands exist and **five of them write**. Their disposition —
> keep, freeze, or remove — is part of what ADR-0007 decides. Do not treat this page as a licence.

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
| **Faceplate INSTANCES** | **create a container, point `ContainedType` at a human-authored library type, compile clean, and set its parameters.** The instance **adopts the type's own geometry**. Needed no new tooling — 2026-08-09 | `--item HmiFaceplateContainer`, `--set …ContainedType=`, `--set …Interface[n].Value=` |
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
| **21 of 56 item types refuse**, with no reason given | The creatable set must be established by trial and cached; `GetCreationInfos` overstates by 60% |
| **No Unified screen export FUNCTION** — confirmed **five** ways | ⚠️ **The consequence originally recorded here — *"no diff, no hash, no golden round-trip"* — was WRONG (2026-08-09).** A **serialiser over the property walk `hmi --screen` already performs** recovers all three, and there are **two independent existence proofs**: Siemens' own Openness-based exporter (SIOS 109792619, simple *and* complex properties, dynamizations, events, fonts) and a commercial JSON one for V15–V21. This is a **format** wall, not an **inspection** wall. See `hmi-web-tooling-research.md` |
| **No faceplate document round trip via `ExportAsDocuments`** | ⚠️ **"Types cannot be authored" is NOT established (2026-08-09).** `GetSupportedExportFormats()` is empty — but `CreateFromDocuments` takes **no format argument**, and `LibraryTypeVersion.Export(FileInfo, ExportOptions)` exists and was **never called**. P10's inference linked two calls that share no parameter. **Reopened**; see `hmi-faceplate-gap-probe.md` |
| **Master copies are unavailable on Unified** | `MasterCopyComposition.Create(IMasterCopySource)` exists, but **none** of the 45 `IMasterCopySource` and 30 `IMasterCopyTarget` implementers is under `HmiUnified.*`, and `HmiScreenComposition` has no `CreateFrom`. The obvious fallback to faceplates is **closed**. Classic HMI has the full surface |
| **Unified cannot propagate a type update, or find a type's instances** | `HmiSoftware` implements none of `IUpdateProjectScope`, `IInstanceSearchScope`, `ILibraryTypeInstantiationTarget` — classic `HmiTarget` and `PlcSoftware` implement all three. Type-version propagation and instance discovery are **UI-only**. A real cost to any "author once, stamp hundreds" path |
| **Screens are FLAT** — one level deep, absolutely positioned | All layout intelligence would be ours |
| **No transaction** | A failed command keeps what it already did. Commands must be re-runnable |
| Screens cannot be created into a group, or moved between groups | Grouping is programmatically unreachable |
| Bitmask entries come only from `Create(BitDynamizationType)` | And **cannot be deleted** |
| `TagParameterDynamization` refuses outside a faceplate | Faceplate-scoped. ⚠️ *"Likely unreachable in practice"* was **premature** — a faceplate **container's** interface entries are themselves dynamization hosts (`HmiFaceplateInterface` derives from `UIBase`, which carries `Dynamizations`), so binding one uses the mechanism already proven live. No probe has yet had a resolved faceplate to try it on |
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

---

## 4. Coverage, honestly

| Axis | Walked | Of |
|---|---|---|
| Distinct API members invoked | ~130 | 4549 (**~3%**) |
| Creatable kinds created | 15 | 80 |
| Screen-item types | **35 created / all 56 attempted** | 56 |
| Dynamization kinds | **5 created / all 6 attempted** | 6 |
| Deletions | ~20 across 9 kinds | 184 types |
| Event values attached | **2** | 246 |

The two "all attempted" rows are the useful ones: those refusal sets are **facts**, not gaps.
Event breadth is the largest untouched axis — but the *mechanism* is proven, and the catalogue is
regular, so it is breadth rather than risk.

---

## 5. Owed, and not claimed

- The nested `--set` fix is **unit-tested but never live-verified**.
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
| `docs/notes/hmi-ai-design-options.md` | 47 options for using this in engineering, ranked — an options menu, not a plan. **§14 is a recommended path**, added separately and separable from the menu |
| `docs/evidence/hmi-capability-probes.md` | transcripts, redacted/anonymised per `docs/13` |
| `docs/adr/adr-0007-hmi-engineering-scope.md` | **the open decision** |
