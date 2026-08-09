# Faceplates are LIBRARY types — and the library has the export/import surface the HMI device lacks

**Status: REFLECTION ONLY, 2026-08-09. Nothing here has been verified against a live project.**
Written up immediately because it contradicts two claims this project has already committed, and
because the last two times a reflection reading went unverified for a while it turned out wrong (see
`openness-hmi-write-api.md` §4m). Treat every line below as a hypothesis with a named test.

**Prompted by the project owner's own observation:** *"faceplates live in the library in the TIA UI,
so that may be a hint."* It was. Every previous search for a faceplate type looked under
`HmiSoftware` — the wrong container entirely, which is the third instance of that exact error.

## 1. What was claimed before, and why it was wrong

`openness-hmi-write-api.md` §2 says:

> **Faceplate types cannot be authored, only instantiated.** There is no Unified faceplate *type*
> class in the assembly, and `HmiFaceplateInterfaceComposition` has `Find` and no `Create`.

**The reasoning is invalid.** `HmiFaceplateInterfaceComposition` is the **instance's parameter list**
— it hangs off `HmiFaceplateContainer` and holds `HmiFaceplateInterface` entries, each a
`PropertyName` (read-only) plus a settable `Value`. That is the list of parameters you fill in *when
placing* a faceplate. It was never the faceplate type, so its lack of a `Create` says nothing at all
about authoring types. The conclusion was drawn from the wrong object.

"There is no Unified faceplate type class in the assembly" is **literally true and misleading**:
there is no type under `Siemens.Engineering.HmiUnified.*`, but there is one under
`Siemens.Engineering.Hmi.Faceplate.*`, which is where the search never looked because that namespace
had been filed as "classic".

## 2. What the assembly actually says

```
Siemens.Engineering.Hmi.Faceplate.FaceplateLibraryType        : Library.Types.LibraryType
Siemens.Engineering.Hmi.Faceplate.FaceplateLibraryTypeVersion : Library.Types.LibraryTypeVersion
```

Faceplates are **library types**, exactly as the TIA UI presents them. Everything a library type can
do, a faceplate can do — and the base classes carry a lot:

```
LibraryType.Versions            : LibraryTypeVersionComposition
LibraryType.GetSupportedExportFormats() -> IEnumerable<...>       // queryable AT RUNTIME, per type
LibraryTypeVersion.Edit()       -> LibraryTypeVersion             // check out for editing
LibraryTypeVersion.Release(dependenciesMode, version, author, comment)
LibraryTypeVersion.Discard()  ·  SetAsDefault()  ·  Delete()
LibraryTypeVersion.FindInstances(IInstanceSearchScope) -> IList<...>
LibraryTypeVersion.CompareTo(other) -> DetailedCompareResult
LibraryType.UpdateProject(scope, ...)                              // push a new version to instances
```

The `Edit()` → modify → `Release()` cycle is the versioned-type workflow, exposed.

### 2a. The part that matters most — there IS a document round trip

```
LibraryTypeVersion.ExportAsDocuments(DirectoryInfo, fileNameWithoutExtension, exportFormat, LibraryExportOptions)
      -> ExportTransferResult
LibraryTypeComposition.CreateFromDocuments(DirectoryInfo, fileNameWithoutExtension, LibraryImportOptions)
      -> TypeCreateTransferResults
LibraryTypeVersionComposition.CreateFromDocuments(DirectoryInfo, fileNameWithoutExtension, CreateOptions, LibraryImportOptions)
      -> VersionCreateTransferResults      // CreateOptions: None | Override
```

An exhaustive sweep for these method names across the whole assembly returns **only** these, plus
`PlcBlock`/`PlcType`/`PlcDocument.ExportAsDocuments` — the PLC-side pair this repo's entire IR
pipeline is built on.

**If a Unified faceplate exports as a document, then a Unified UI artifact IS serialisable** — and
the survey's headline (§8, *"decode the XML — there isn't one"*) needs narrowing from "Unified UI
content" to "Unified **screens**". Screens have no export. Faceplates may be a different story
entirely, because they are not device content at all; they are library content.

## 3. Why this matters more than it looks

Three of ADR-0007's arguments *against* rest on the two claims above:

1. **"No IR, therefore none of the review machinery"** — no export ⇒ no diff, no `ir-hash`, no
   golden round-trip, and *"a canonical serialiser must be written first — it is the long pole."*
   If faceplates round-trip as documents, that long pole is already carried **for faceplates**, by
   Siemens, in the same shape the PLC side uses.
2. **"Reuse cannot be expressed structurally at all"** — screen trees are one level deep and
   faceplate types were believed unauthorable, so *"define a pump faceplate once, stamp it forty
   times" can have its second half automated and not its first*. If types can be created from
   documents, **both halves are automatable**, and the flat-layout objection weakens sharply: a
   faceplate is precisely the container the screen tree does not provide.
3. **The layout problem** — a large part of "all layout intelligence would be ours" is really "we
   have no reusable component". A faceplate is that component.

This does not decide the ADR. It moves specific arguments, and it should be verified before it moves
them: an argument overturned by an unverified reading is worse than the original.

## 4. What is NOT established — the honest column

- **Whether a *Unified* faceplate is a `FaceplateLibraryType` at all.** The class sits in the classic
  namespace. The only `HmiUnified.*` type deriving from `LibraryType` is
  `HmiUnified.Library.ScriptModuleType`. So Unified faceplates might surface as `FaceplateLibraryType`
  (class reused across families), as a plain `LibraryType`, or not appear in the project library in a
  way this API reaches. **UNKNOWN, and it is the load-bearing question.**
- **What `GetSupportedExportFormats()` returns for one.** It may be empty. Everything in §2a is
  conditional on this. Note this method is exactly the kind of runtime self-description that
  `--schema` already relies on — the API will answer it directly rather than being guessed at.
- **What the exported document contains** — a full object graph, or a stub. `Simatic ML` for a
  faceplate has never been seen here.
- **Whether the round trip is lossless**, which is the only property that makes it useful for review.
- **Whether JOB9002's project library contains any faceplate types at all.** If it does not, none of
  this is testable on the available hardware and it joins classic HMI as externally gated.
- **`LibraryTypeVersionState`** is `InWork | Committed`, and `ConsistencyStatus` has six values
  including `NonDefaultVersionInstantiation` and `MultipleVersionsInstantiationInSameDevice`. So
  library types carry their own consistency model, separate from `PlcBlock.IsConsistent` and from the
  HMI compile. **A third gate nobody has looked at.**

## 5. The instance side, which is now clearer

`HmiFaceplateContainer` (a creatable screen item, §4k) carries:

```
Interface   : HmiFaceplateInterfaceComposition     // Find(propertyName) -> entry, entry.Value settable
EventHandlers, Adaption, RotationAngle, RotationCenter{X,Y,Placement}
```

So placing a faceplate and **filling in its parameters** is reachable with what already exists — the
`Find`-then-set-`Value` shape. This also explains P2's finding that `HmiFaceplateContainer` compiles
with `The referenced faceplate type does not exist`: the container references its type **by name
string**, like every other container (§2), so the type must exist first.

And it explains P7's `TagParameterDynamization` refusals (§4m): tag parameters are faceplate
interface parameters, and there was no faceplate.

## 6. Named tests, in order — all need Portal

1. `ProjectLibrary.TypeFolder.Types` on JOB9002: enumerate, and print each type's **CLR type name**.
   Answers §4's load-bearing question in one read. Cheap, read-only, do this first.
2. For any faceplate type found: `GetSupportedExportFormats()`, then `Versions` and each version's
   `State`/`VersionNumber`.
3. `ExportAsDocuments` a faceplate version to a scratch directory; inspect what comes out.
4. `CreateFromDocuments` it back under a `ZZ_AI_*` name; compare with `CompareTo`.
5. Only then: author a trivial faceplate type by writing a document.

Steps 1–2 are read-only and carry no risk. Step 4 is the first write and needs the usual
`ZZ_AI_*`-only discipline plus a compile.

**Do not repeat P3's mistake here:** if step 3 or 4 refuses, that is evidence about *that call*, not
about the capability — vary the format, the options enum and the type before concluding anything.

## 7. The same error a fourth time — an exhaustive Export/Import sweep [REFLECTION, 2026-08-09]

Triggered by a parallel agent finding Siemens manual pages titled *"Exporting all screens of an HMI
device"* in the V20/V21 Openness trees, which would contradict the survey's §8 headline. Rather than
chase the manual (whose bodies would not load, and which may describe either family), the assembly
was swept exhaustively for every `Export*`/`Import*` method under both HMI namespaces. The assembly
is the authority on what the API offers.

### The screen claim SURVIVES — and is now confirmed a fifth way

**Classic** has a full screen export surface: `Screen.Export(FileInfo, ExportOptions)` and
`ScreenComposition.Import`, plus `ScreenPopup`, `ScreenSlidein`, `ScreenTemplate`, `ScreenOverview`
and `ScreenGlobalElements`. That is certainly what the manual pages describe.

**Unified `HmiScreenComposition` has exactly this**: `Create(String name)`, `Find`, `Contains`,
`IndexOf`, `GetEnumerator`. **No `Export`. No `Import`.** So *"WinCC Unified has no screen export"*
holds, now verified a fifth independent way, by exhaustive sweep rather than by targeted search.
(It also re-confirms §4l's `--in` finding from the other direction: `Create` takes a name only.)

### But "Unified has no export" would be FALSE, and three claims built on that are wrong

The sweep found real Unified round trips, all `DirectoryInfo`-based rather than `FileInfo`-based:

| Composition | `Create` | `Export` | `Import` |
|---|---|---|---|
| `HmiScreenComposition` | ✅ name only | ❌ | ❌ |
| `HmiTagTableComposition` | ✅ name only | ❌ | ❌ |
| **`HmiTagComposition`** | ✅ | ✅ `(dir[, name])` | ✅ `(dir[, name])` |
| **`HmiTextListComposition`** | ❌ **none** | ✅ `(dir, filename)` | ✅ `(dir, filename)` |
| **`HmiSystemTextListComposition`** | ❌ **none** | ✅ | ✅ |
| **`HmiScriptModuleComposition`** | ❌ **none** | ✅ `(dir)` | ✅ `(dir[, name])` |

Consequences, in order of how wrong the existing notes are:

1. **"Script modules and text lists cannot be authored (no `Create`)"** — repeated in the write-api
   gap register and in **ADR-0007's explicitly-out-of-scope list** — is **WRONG**. Both have
   `Import`. The authoring route is a **document**, not a `Create`. This is the **fourth** time this
   project has searched a composition for `Create`, not found one, and concluded the capability does
   not exist: screens-into-groups, faceplate types, and now these two.
2. **Bulk tag round trip exists and was never used.** Every tag this project created went through
   `Create(name)` one at a time, then fought contextual writability on `HmiDataType` (§4h). A
   directory-based `Export`/`Import` pair is a different and probably far better route for anything
   at scale — and it is directly relevant to FI-18, where the PLC/HMI tag boundary is the whole
   subject.
3. **There IS a serialisable Unified artifact after all** — three of them. So the ADR's
   *"no export means no diff, no `ir-hash`, no golden round-trip"* is true **for screens** and false
   for tags, text lists and script modules. A canonical serialiser may be needed for less than the
   ADR assumes.

`IChromDataExchangeExport` is the shared Unified export interface (`Export(DirectoryInfo[, String])`)
and only `HmiScriptModule` implements it directly; the others declare the pair on the composition.

**UNVERIFIED:** nothing here has been run. The formats these produce, whether the round trip is
lossless, and whether `Import` creates-or-updates are all unknown — as is whether an import can
introduce an object that does not yet exist, which is the claim that actually matters for authoring.
That last one is a single cheap probe: export a text list, edit the file, import it back under a new
name.

### The pattern worth naming

Four times, the same shape: **the capability existed, under a different verb or on a different
object, and the search stopped at the first plausible negative.** `Create` is not the only
constructor in this API — `Import`, `CreateFromDocuments` and `CreateFrom` all author things. Any
future "X cannot be done" in these notes should be read as "X was not found where I looked", unless
it comes with an exhaustive sweep like this one.
