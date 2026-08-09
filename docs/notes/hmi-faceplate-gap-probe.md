# The faceplate gap — what P10 did not test

> # 🔬 P13 — THE LIBRARY-WIDE COMPARISON, AND WHY AUTHORING IS NOT DEAD (2026-08-10) [LIVE]
>
> Prompted by the owner: *"don't give up on authoring faceplates; investigate ALL library types, not
> just faceplates — they may lead to a clue."* **That was the right instinct**, and it converted a
> faceplate-shaped dead end into a structural rule with an identifiable single point of failure.
>
> ## The negative control that had never been run
>
> The same call — `LibraryTypeVersion.Export(FileInfo, ExportOptions.None)` — across three kinds:
>
> | Library type | CLR class | Bytes | `ContentObject`? |
> |---|---|---|---|
> | PLC data type | `PlcTypeLibraryType` | 4,244 | ✅ **full definition** — `SW.Types.PlcStruct` with the complete `Interface`/`Sections`/`Member` tree |
> | Faceplate | `LibraryType` | 2,642 | ❌ absent |
> | **Image file** | `LibraryType` | 2,641 | ❌ absent |
>
> **The image is the discriminating case.** An icon is about as un-secret as content gets, and it is
> withheld identically to the faceplate. So the difference is **not** "HMI content is protected" and
> **not** "faceplates are special". It is the **CLR class**:
>
> > **Empty `GetSupportedExportFormats()` ⟺ plain `LibraryType` ⟺ no `ContentObject`.**
> > Three views of ONE fact: **Openness ships no content serialiser for HMI-side library types.**
>
> This also **corrects a claim made a few hours earlier in P12**, that `LibraryTypeVersion.Export`
> "writes a wrapper with no payload". Wrong as a general statement — it writes payloads perfectly
> well. Only this one CLR class lacks the serialiser.
>
> ## Why that is a better position than "refuted"
>
> The three symptoms are **one** point of failure, not three independent walls. Everything around it
> is demonstrably functional: versions, the document schema, and `CreateFromDocuments` all work for
> PLC types. And **export and import are separate code paths** — nothing requires both to exist.
>
> ## The two live leads, in priority order
>
> 1. **`CreateFromDocuments` with a hand-authored document.** We now hold two real worked examples of
>    the wrapper schema — one WITH a `ContentObject` and one WITHOUT — from the same TIA version. If
>    TIA accepts a `LibraryTypeVersion` document carrying a `ContentObject` we write ourselves, the
>    missing *exporter* stops mattering, because only the *importer* would be needed. **This is the
>    prize, and it is only reachable because the PLC document showed what a valid one looks like.**
> 2. **`ExportAsDocuments` called anyway, despite the empty format list.** Since P10 that emptiness
>    has been treated as a gate and **never once tested as one**. An empty ADVERTISEMENT is not a
>    REFUSAL — this project has concluded "impossible" from not finding the right method four times.
>
> ## Status of lead 2: tooling BUILT, probe NOT RUN
>
> `openness-cli library --probe-documents <TypeName> --out <dir>` is implemented and unit-tested. It
> invokes **every** `Export*` overload on the type, including `ExportAsDocuments` with the first enum
> value when nothing is advertised, and reports each binding and outcome. It deliberately does not
> gate — a throw is a RESULT and the whole output is the finding.
>
> 🔴 **It has never executed.** The build that carries it revoked the Openness whitelist approval
> (FI-61: keyed on `(Path, FileHash)`), and in this state TIA refuses **silently** — no dialog, so it
> cannot be approved reactively; the connect simply burns the timeout. Running it needs an approved
> build. **Do not record any conclusion about lead 2 — it is untested, not negative.**
>
> ## Also still open
>
> - A **third data point**: does `CodeBlockLibraryType` carry a `ContentObject` too? That confirms
>   whether "content-bearing" tracks PLC specifically or merely tracks having a typed subclass.
> - The **global** library rather than the project library — different storage, possibly different
>   serialisation.

> # 🔎 P12 — THE GAPS CLOSED, AND THE ARCHITECTURE THIS PROJECT ACTUALLY USES (2026-08-10) [LIVE]
>
> Run against the reference project's scratch copy, `ZZ_AI_*` names, restored to baseline
> afterwards (`Success`, 0 errors, 156 warnings, 0 residue). Names below **genericized**.
>
> ## The headline: faceplates here are opened from SCRIPT, not placed in containers
>
> A full sweep — **49 screens, 1,404 items — contains ZERO faceplate containers.** The nine
> faceplate types are authored and never instantiated as items. They are opened as **popups from
> JavaScript**, from hot-zone polygons on the overview screens:
>
> ```js
> let data = {IO:{Tag:"<MotorTagA>"}, ScreenSelection:{Tag:"Constant0"}};
> let po = UI.OpenFaceplateInPopup("<MotorHolderDOL>_V_0_0_11", "Motor Pop Up", data);
> po.Left = 490; po.Top = 80; po.Visible = true; po.WindowFlags = 67;
> ```
>
> **This is §14b's spine, already built by a human**, and every part except authoring the type is
> inside measured write capability: polygon, `Tapped` handler, six lines of JS, one tag name. The
> per-instance delta between call sites is **a single identifier**.
>
> ## The two routes, and the trade-off is real (this corrects two claims made while probing)
>
> | | Faceplate CONTAINER | Scripted POPUP |
> |---|---|---|
> | Type reference checked at compile | ✅ | ❌ it is a string in JS |
> | Parameter **structure + UDT version** checked | ✅ **with the reason** | ❌ none |
> | Version choice at write time | ❌ takes current | ✅ pinned in the string |
> | Version *readable* afterwards | ✅ stores `V0.0.11\<Name>` | ✅ it is text |
> | Per-instance artifact | object state | reviewable text |
> | Unwired parameter detected | ❌ **ours to check** | ❌ ours to check |
>
> Mid-probe this note claimed containers "drift silently, and nothing can tell you which". **Wrong**
> — the container stores the resolved version and it reads back. It also claimed the popup route was
> therefore better; that is **at best half true**, because the popup is not statically checked at
> all. A wrong tag in a popup is a runtime failure on a live panel.
>
> ## Gaps closed
>
> | Gap | Result |
> |---|---|
> | **Type authoring** | **CLOSED.** `LibraryTypeVersion.Export` **works** and writes a real document — of **2,642 bytes containing author, version number and an empty comment. No content.** The owner supplied a screenshot of the type's actual contents (selector, Start/Stop, status lamps, command buttons) as ground truth. The call succeeds; the payload is not in it. P10's conclusion survives, now for a *demonstrated* reason |
> | **Reading `Interface` back** | **BUILT.** Reports contained type **with version**, parameter names, values, `(unset)`, and per-parameter dynamizations. Returns null for non-containers so "not a container" ≠ "container with no parameters" |
> | **Script bodies** | **FIXED — and this was the big one.** All **274** handlers reported empty. Root cause: the preview took `Split('\n')[0]` rather than the first NON-BLANK line, and scripts are conventionally written with a leading newline. The walker had been describing the skeleton and omitting the animal. `--scripts` now dumps full bodies |
> | **Interface arity** | `<MotorHolderDOL>` = 2 (`IO`, `ScreenSelection`); `<FP_MotorMain>` = 1 (`IO`). Both `String` |
> | **Version pinning** | Container: write a bare name, TIA stores `V0.0.11\<Name>`. The JS `_V_0_0_11` form is **REFUSED** by `set_ContainedType` — different syntax for different mechanisms |
> | **Item types in real use** | **12 of 56**, and **all 12 are creatable**: Text 421, Circle 301, Rectangle 254, Button 147, Line 94, Polygon 84, GraphicView 41, IOField 38, ScreenWindow 10, ToggleSwitch 8, SymbolicIOField 3, AlarmControl 3. **The 21 refusing types fall entirely outside what a real plant HMI uses** |
>
> ## What the compile does and does not gate — corrected twice, in both directions
>
> | Defect | Caught? |
> |---|---|
> | Faceplate type does not exist | ✅ |
> | Parameter names a non-existent object | ✅ by property |
> | Parameter bound to the **wrong structure / UDT version** | ✅ **with the remedy** |
> | **Parameter left entirely unwired** | ❌ **compiles clean** — ours to check, and now checkable via `(unset)` |
> | Right structure, wrong instance | ❌ requirements-level |
> | Layout, legibility | ❌ |
>
> The unwired case is the PLC side's `undriven-scan` lesson repeating exactly: a thing that is never
> driven compiles perfectly well, and the check has to be built separately.
>
> ## A real finding about the reference project, for the engineer
>
> **The faceplate types are STALE against the current motor UDT.** Binding a real motor tag to `IO`
> fails on *"The structure of the user data type configured at tag '…' does not match the structure
> defined at interface tag 'IO'. Select a valid user data type version."* — and **every** faceplate
> type reports `DefaultVersionInconsistent` while **every** PLC type reports `Consistent`. Those are
> one fact seen twice. This is why a fully-wired container could not be made to compile clean here,
> and it is a project-maintenance matter, not a tooling limit. **Not touched** — real content.
>
> ## Still open, honestly
>
> - **A fully-wired faceplate compiling clean was NOT achieved.** One binding was accepted
>   (`ScreenSelection`); `IO` failed on the staleness above. Partial, not complete.
> - **Writing a parameter needs the explicit `str:` tag.** A bare value fails with
>   `FormatException: String was not recognized as a valid Boolean` — on a property whose read-back
>   says `String`. Misleading; worth a usability fix.
> - Re-pointing `ContainedType` on a wired container, and geometry override, remain untested.
>
> P11's verdict box follows, unchanged.

> # ✅ VERDICT: INSTANTIATION WORKS. RUN LIVE 2026-08-09 [LIVE]
>
> **Q1 — the load-bearing question this whole document was written around — is answered, and the
> answer is YES.** An AI can place a faceplate instance on a screen, point it at a human-authored
> library type, and have TIA's own compiler accept it. **It needed no new tooling.** The capability
> was already there and nobody had tried it.
>
> Measured against a real Unified panel, in the reference project's scratch copy, with invented
> `ZZ_AI_*` names. Library type names below are **genericized** per `docs/13-data-boundary.md`.
>
> | # | Step | Result |
> |---|---|---|
> | 1 | `hmi-create-screen --item HmiFaceplateContainer` | ✅ created — the **one-argument** `Create<T>` works for faceplates |
> | 2 | `--set <item>.ContainedType=<FP_MotorMain>` | ✅ **writable after creation**, coerced to `String`, saved |
> | 3 | `hmi-compile` | ✅ **`STATE: Success`, 0 errors.** P2's *"The referenced faceplate type does not exist"* is **GONE** |
> | 4 | read back, fresh process | ✅ **the item auto-resized 120x80 → 300x430**, the type's own size |
> | 5 | `--set <item>.Interface[0].Value=…` | ✅ the parameter list **populates once a type resolves** |
> | 6 | `--bind-kind …Interface[0].Value=TagParameterDynamization` | ❌ REFUSED — see below |
> | 7 | `--bind-kind …Interface[1].Value=…` | ❌ out of range: **the interface holds exactly ONE element** |
> | 8 | compile with a bogus parameter value | ✅ **CAUGHT**: `Interface.IO: The object "ZZ_AI_TEST" at the property "IO" does not exist.` |
> | 9 | delete screen, re-compile | ✅ `Success`, 0 errors, 156 warnings — **identical to baseline** |
>
> **Three independent confirmations, not one.** The previously-observed error disappeared; the
> instance physically adopted the type's geometry; and a deliberately-invalid parameter produced a
> precise, located error. Any one alone would be weak. Together they are conclusive.
>
> ### The four findings that matter more than the yes
>
> 1. **A faceplate exposes ONE parameter here, named `IO`, and it wants an EXISTING OBJECT.** So
>    wiring an instance is *(type, position, one binding)* — three facts a human can check at a
>    glance and a serialiser can diff exactly. This is what makes "stamp hundreds" economically real,
>    and it collapses screen review from *inspect a picture* to *check a table*. Measured on one type;
>    the other eight may differ, and an out-of-range index reports the arity for free.
> 2. **The compile is a REAL GATE for faceplate work** — it validates the type reference *and* the
>    parameter binding, and locates failures screen → item → property. **This retracts §14e of
>    `hmi-ai-design-options.md`**, written hours earlier, which said hard rule 4 has no clean HMI
>    analogue. For a faceplate-first architecture it has one, and the error text doubles as a
>    specification a generator can consume.
> 3. **The type owns the layout** (step 4's auto-resize). Placement is a grid decision, not a design
>    decision — exactly the division of labour §14b proposed, now observed instead of argued.
> 4. **`Validate()` passed the bogus parameter, twice.** Only the compile caught it. Another
>    confirmation of a split this project has now measured six-plus times.
>
> ### What is still NOT established — do not let the yes leak into these
>
> - **Type AUTHORING remains untested.** `LibraryTypeVersion.Export` has still never been called
>   (§3). Instantiation working says nothing about it.
> - **Maintenance is still UI-only** (§5): no `IUpdateProjectScope`, no `IInstanceSearchScope`. Stamp
>   a hundred instances and you cannot push a type update to them, or find them.
> - **`TagParameterDynamization` refused INSIDE a real faceplate parameter** — the negative control
>   §2 asked for, finally run. P7's conclusion survives its first genuine test, and what the kind is
>   *for* remains unknown. The refusal named no reason.
> - **Custom Web Control containers are a different story**: `HmiCustomWebControlContainer` IS in the
>   creatable list, but refuses the one-argument create with `'ContainedTypeValue' parameter is
>   missing` — the two-argument overload is **mandatory** for it. Tooling support written and
>   unit-tested; **not yet run**.
> - Every faceplate in this project is `DefaultVersionInconsistent`, so the clean-variable version of
>   this experiment was **never available** — see the redaction lesson in §8.
>
> Original reflection-stage analysis below, unedited.

**Date: 2026-08-09.** Closes the five questions left open by probe P10
(`openness-hmi-faceplate-library.md`), which tested one hypothesis and answered it, leaving the
load-bearing half — **instantiation** — untouched.

> ## 🔴 STATUS: REFLECTION COMPLETE, LIVE WORK BLOCKED — NEEDS THE OWNER
>
> **No approved `openness-cli` binary exists on this machine.** TIA whitelists Openness callers by
> `(Path, FileHash)`; the worktree binary was rebuilt at **14:53** today, after its last approval at
> **11:19**, and the main-checkout binary's current hash is not in the whitelist either (107 entries
> checked, neither hash present). An `Openness access` dialog is **currently open** on the Portal
> process holding the JOB9002 scratch copy, waiting for a human click that no agent may perform.
>
> Consequently **every verdict below marked `[REFLECTION]` is shape-only** — read off
> `Siemens.Engineering.dll` / `Siemens.Engineering.Hmi.dll` by reflection, which needs no Portal and
> carries no risk, but which cannot tell you whether a call the API *declares* will actually
> *succeed*. This project has been burned by exactly that gap before (§4k: `GetCreationInfos`
> overstates the creatable set by 21 of 56). **A declared method is a hypothesis.**
>
> The one thing needed to finish this is a human approving the binary. Nothing was rebuilt in the
> course of this work, and nothing should be.
>
> ### The refusal, measured `[LIVE]` — and one correction to the CLI's own advice
>
> A single read-only `openness-cli library <scratch project>` was attempted. It is the only Portal
> contact made in this work, it wrote nothing, and it failed:
>
> ```
> WARNING: this executable is NOT approved for TIA Openness — it has been REBUILT since it was last approved.
>   17 earlier build(s) of this exact path are approved, but none of them matches this file's current hash.
>   EXPECT THE CONNECT BELOW TO HANG UNTIL --timeout-connect EXPIRES, WITH NO DIALOG AND NO ERROR.
> …
> Openness refused this client: EngineeringSecurityException ("Security error").
> EXIT: 2
> ```
>
> **The advisory overstates one of the two faces.** It predicts "no dialog and no error"; what
> happened was the *other* face — a dialog titled `Openness access (0033:000666)` appeared on the
> Portal process holding the project (confirmed by enumerating window titles, the §4n diagnostic),
> the call waited on it for roughly 35 minutes, and then ended with a thrown
> `EngineeringSecurityException` and exit **2**, not a silent timeout and exit 3. Both faces are
> documented in `CLAUDE.md`; the warning text asserts one of them as though it were the only
> outcome. Worth softening to "expect **either** a silent hang **or** a security exception — on a
> rebuilt binary both mean *needs approval*."
>
> Nothing else about this diagnosis is inferred: the whitelist was read directly from
> `HKLM\SOFTWARE\Siemens\Automation\Openness\20.0\Whitelist\openness-cli.exe` (107 entries), and
> neither the worktree binary's hash nor the main checkout's is present.

---

## 1. Verdicts

| # | Question | Verdict | Basis |
|---|---|---|---|
| 1 | Can a faceplate container be pointed at a real library type, bound, and compiled? | **UNTESTED** — every link in the chain is **shape-CONFIRMED**, none is run | `[REFLECTION]` |
| 2 | Does `LibraryTypeVersion` (vs `LibraryType`) or `InWork`/`Committed` state explain the empty export formats? | **REFUTED** — and P10's inference chain is a **non-sequitur** | `[REFLECTION]` + `[LIVE]` data P10 already collected |
| 3 | Are master copies the faceplate substitute for Unified? | **REFUTED for Unified** at the type system; **CONFIRMED-shape for Classic HMI and PLC** | `[REFLECTION]`, exhaustive |
| 4 | Global vs project library; every method that CONSTRUCTS a type or version | **ANSWERED** — `CreateFromDocuments` is the **only** constructor of a library type in the whole API, and the global library adds none | `[REFLECTION]`, exhaustive |
| 5 | Can a faceplate type be created from an existing screen, as the TIA UI does? | **REFUTED** — and not just for faceplates: **no API path anywhere turns an object into a type** | `[REFLECTION]`, exhaustive |

**The headline is Q2 + Q4 together, and it is a correction, not a discovery:** P10's *conclusion*
("faceplate types cannot be authored") most likely still stands, but the *reason it gave* does not
follow. `GetSupportedExportFormats()` governs `ExportAsDocuments`, which takes a format string.
Neither `LibraryTypeVersion.Export(FileInfo, ExportOptions)` nor
`LibraryTypeComposition.CreateFromDocuments(DirectoryInfo, String, LibraryImportOptions)` takes a
format at all. An empty format list closes one door and was read as closing all three. **That is the
fifth instance of this project's own named pattern** (`openness-hmi-faceplate-library.md` §7: *"the
capability existed under a different verb, and the search stopped at the first plausible negative"*)
— this time committed by the very note that named the pattern.

---

## 2. Q1 — instantiation. Shape-complete, and it needs **no new tooling** to test

**This is the most valuable thing in this document and it is the thing that is not yet known.** Say
it plainly: *nobody has yet pointed an `HmiFaceplateContainer` at a real faceplate type.* The
recommended path in `hmi-ai-design-options.md` §14b rests on it entirely, and it is still resting on
an untested assumption.

What reflection establishes is that **every link exists as a public, writable member**, and that one
of them is a stronger fact than was previously recorded:

```
HmiScreenItemBaseComposition.Create<T>(String name)
HmiScreenItemBaseComposition.Create<T>(String name, String containedTypeValue)   // ← second overload,
                                                                                 //   for container types
HmiContainerBase.ContainedType   : String {get;set;}          // ← settable, also after creation
HmiFaceplateContainer.Interface  : HmiFaceplateInterfaceComposition   // Find(String propertyName)
HmiFaceplateInterface.PropertyName : String {get;}
HmiFaceplateInterface.Value        : Object {get;set;}
```

### The new fact — a faceplate parameter is itself a dynamization target

`HmiFaceplateInterface` derives `HmiScreenPartBase → UIBase`, and **`UIBase` carries
`Dynamizations : DynamizationBaseComposition`**. Every entry in a faceplate's parameter list is
therefore a full dynamization host, exactly like an ordinary screen item:

```
UIBase.Dynamizations : DynamizationBaseComposition
DynamizationBaseComposition.Create<T>(String propertyName)
```

So **binding a faceplate parameter to a tag is shape-supported by the identical mechanism this
project has already proven live on ordinary items** (`openness-hmi-write-api.md` §4h) — nothing new
has to be invented, and the whole mapping-table/flashing vocabulary of §4n would come with it. This
was not in the earlier notes, which recorded only the `Find`-then-set-`Value` shape.

`TagParameterDynamization` carries exactly one member — `DynamicTagName : String {get;set;}`. Its
shape is consistent with P7's reading (§4m): it names a tag to be resolved *through* a faceplate
parameter, i.e. it belongs to items **inside** a type, not to the container. Worth trying on a
container's `Interface` entry anyway, as the negative control P7 could never run.

### What would falsify the shape argument

**Contextual writability** — §4h's rule, and the most likely failure. If `ContainedType` refuses on
an already-created bare container (as a fresh tag's `HmiDataType` does), the two-argument
`Create<T>(name, containedTypeValue)` becomes mandatory.

🔧 **Tooling gap, recorded now so it is not rediscovered mid-session:**
`OpennessGateway.CreateScreenItem` selects the create method with
`m.GetParameters().Length == 1` — it binds **only** the one-argument overload. The
container-specific two-argument overload is unreachable through `openness-cli` today. If probe step 1
below fails on `ContainedType`, the probe needs new CLI code (and a fresh approval), not another
attempt.

### The probe, in order — steps 1–4 need **no new code**

| # | Command | What it settles |
|---|---|---|
| 1 | `hmi-create-screen … --item HmiFaceplateContainer --yes`, then `hmi-edit-screen --set <item>.ContainedType=<a real faceplate type name>` | whether the reference resolves. Success signal: `hmi-compile` no longer reports *The referenced faceplate type does not exist* (§4k). **The load-bearing test.** |
| 2 | `hmi <project> --screen <that screen>` from a **fresh process** | whether `Interface` populates with entries once a type resolves — the question `Find` cannot answer without a type |
| 3 | `--set <item>.Interface[0].Value=…` and `--bind <item>.Interface[0].Value=<tag>` | whether parameters take literals and tags. The nested resolver already handles `[n]` and both routes go through `ResolveTarget`, so this is free |
| 4 | `--bind-kind <item>.Interface[0].Value=TagParameterDynamization` | P7's missing negative control: the kind refused in every position tried, and there was never a faceplate |
| 5 | delete the screen, `hmi-compile` clean | the mandatory post-delete compile (§4j — deletion orphans silently) |

Use a faceplate type whose status is `Consistent` — P10's transcript shows three such
(`HmiType_5/6/7`, single `Committed` default version). The `DefaultVersionInconsistent` majority
introduces a second variable into the first experiment that has never been run.

---

## 3. Q2 — `LibraryTypeVersion` vs `LibraryType`, and the state hypothesis

**Both halves refuted, and the second by data P10 had already collected without noticing.**

### (a) There is no version-level format query to check

`GetSupportedExportFormats()` is declared on **`LibraryType` only**. `LibraryTypeVersion` has no
format query of any kind. So "P10 read it off the type, check the version" has no answer to give —
there is nothing to read. What the version has instead is a **second export that consults no format
list at all**:

```
LibraryTypeVersion.Export(FileInfo exportFileInfo, ExportOptions exportOptions)
LibraryTypeVersion.Export(FileInfo exportFileInfo, ExportOptions exportOptions, DocumentInfoOptions)
LibraryTypeVersion.ExportAsDocuments(DirectoryInfo, String fileNameWithoutExtension,
                                     String exportFormat, LibraryExportOptions)   // ← the format one
ExportOptions = None | WithDefaults | WithReadOnly
```

`Export(FileInfo, …)` is the classic single-file SimaticML shape — the same pair
`Hmi.Screen.Screen.Export` uses — and it is **inherited by every library type version, faceplates
included**. P10 never called it. This is the single most promising untested call in the document.

### (b) State cannot be the explanation — P10's own transcript is the negative control

`LibraryTypeVersionState = InWork | Committed`. `[LIVE, P10]` the reference project's library holds
faceplate types in **both** states. But three of them — `HmiType_5`, `HmiType_6`, `HmiType_7` — have
**exactly one version, `Committed`, marked `(default)`, with `status=Consistent`**, and still report
`exportFormats: (NONE)`. A hypothesis that "an `InWork` version legitimately refuses to export"
predicts these three would export. They do not.

Structurally it could not have been the explanation anyway: the format query is **type-level**, so a
per-version state cannot change its answer.

### (c) The inference chain, laid out

P10 concluded *"no export format ⇒ no `CreateFromDocuments` route"*. The two calls share no
parameter:

```
ExportAsDocuments(dir, name, exportFormat, options)        ← gated by GetSupportedExportFormats()
CreateFromDocuments(dir, name, LibraryImportOptions)       ← takes NO format
```

Empty formats mean TIA will not *hand you* a faceplate document. They do not establish that
`CreateFromDocuments` would reject one. The practical position is unchanged — **without an export
you have no example of the document schema, so authoring one is guesswork** — but the *reason* is
"no specimen", not "no route", and those fail differently and are worth different amounts of effort.

**Falsifier, one call:** `version.Export(new FileInfo(@"…\ZZ_AI_fp.xml"), ExportOptions.None)` on a
`Consistent` faceplate version. Produces a file, or throws and names why. **Needs new CLI code.**

---

## 4. Q3 — master copies. Closed for Unified by the type system

Master copies are real and they do exactly what the question supposed — for **classic** HMI and for
PLC content. For WinCC **Unified** both directions are closed, and closed harder than a refusal:
**the call cannot be written**, because no Unified type satisfies the parameter.

```
MasterCopyComposition.Create(IMasterCopySource masterCopyObject)   // capture an object as a master copy
MasterCopyComposition.CreateFrom(MasterCopy sourceMasterCopy)      // copy a master copy
MasterCopyFolder.MasterCopies : MasterCopyComposition
MasterCopyUserFolderComposition.Create(String name)
MasterCopyMode = ThrowIfExists | Rename | Replace
```

| Direction | Mechanism | Classic HMI | Unified |
|---|---|---|---|
| **capture** a screen as a master copy | must implement `IMasterCopySource` | ✅ `Hmi.Screen.Screen`, `ScreenPopup`, `ScreenTemplate`, their user folders, `Hmi.Tag.Tag`, `TagTable` | ❌ **none of the 45 implementers is under `HmiUnified.*`** |
| **stamp** a master copy onto a device | composition needs `CreateFrom(MasterCopy)` | ✅ `ScreenComposition`, `ScreenPopupComposition`, `ScreenTemplateComposition`, `TagComposition`, `TagTableComposition` | ❌ `HmiUnified.UI.Screens.HmiScreenComposition` has **only** `Create(String)`, `Find`, `Contains`, `IndexOf`, `GetEnumerator` |
| **receive** a master copy into a folder | must implement `IMasterCopyTarget` | ✅ `ScreenSystemFolder`, `ScreenUserFolder`, `TagSystemFolder`, … | ❌ **none of the 30 implementers is under `HmiUnified.*`** |

There is likewise no `CreateFrom` on any composition of Unified screen *items*, so "capture a group
of items and stamp it" has no route either.

**So master copies are not the faceplate substitute.** The reuse story they would have delivered
without type authoring is available to the *classic* family and to the PLC side only.

**What would falsify it.** This is a compile-time argument about parameter types, which is why it is
stronger than the runtime refusals this survey usually collects — but it is an argument about the
**API**, not about TIA. The TIA UI may well create a master copy from a Unified screen; the API
simply offers no way to ask. `[LIVE, P10]` JOB9002's project library reports `masterCopies: 0`, so
there is nothing there to read back either way. If a human creates one by hand in the UI,
`library --master-copies` would show it, and `MasterCopyContentDescription.ContentType` would name
the CLR type captured — that single read would settle whether the *storage* supports Unified content
even though the *API* cannot produce or consume it. Worth asking the owner for: it costs one manual
right-click.

---

## 5. Q4 — every constructor of a type, and global vs project

The sweep covered every method in both assemblies named `Create*`, `Import*`, `Copy*`,
`Instantiate*`, `Retrieve*` or `Open*` that mentions `MasterCopy`, `LibraryType`, `DirectoryInfo` or
`FileInfo`, **and separately** every method whose *return type* is `LibraryType`,
`LibraryTypeVersion`, `MasterCopy` or `*TransferResults` — the second pass precisely so the answer
does not depend on guessing the verb, which is how this project has been wrong four times.

**The complete set that constructs a library type or version:**

```
LibraryTypeComposition.CreateFromDocuments(DirectoryInfo, String, LibraryImportOptions)
LibraryTypeComposition.CreateFromDocuments(DirectoryInfo, String, IEngineeringObject, LibraryImportOptions)
LibraryTypeVersionComposition.CreateFromDocuments(DirectoryInfo, String, CreateOptions, LibraryImportOptions)
LibraryTypeVersionComposition.CreateFromDocuments(DirectoryInfo, String, IEngineeringObject, CreateOptions, LibraryImportOptions)
LibraryTypeVersion.Edit()  ·  Edit(LibraryTypeInstanceInfo)     // a new InWork version OF AN EXISTING TYPE
CreateOptions = None | Override        LibraryImportOptions = None | SkipInactiveCultures | ActivateInactiveCultures
```

Everything else creates a *container*, not a type: `LibraryTypeUserFolderComposition.Create(String)`
for folders, `GlobalLibraryComposition.Create(DirectoryInfo, String)` for a whole library.

**`CreateFromDocuments` is the only constructor of a library type in the entire API — for every kind,
PLC included.** There is no `Types.Create(name)` and no `Types.Create(someObject)`. That is worth
stating as a general fact: it means the document route is not a faceplate-specific inconvenience, it
is how TIA's library API works.

### Global library adds lifecycle, not authoring

`ILibrary` declares `TypeFolder`, `MasterCopyFolder`, `FindType(Guid)`, `FindVersion(Guid)`,
`UpdateProject`, `UpdateLibrary` — and `ProjectLibrary`, `GlobalLibrary`, `UserGlobalLibrary`,
`SystemGlobalLibrary` and `CorporateGlobalLibrary` all implement it. The **type-creation surface is
identical on both**, because it belongs to `LibraryTypeComposition`, reached through `TypeFolder.Types`
in either case. A global library adds `Create(dir, name)`, `Open`, `OpenWithUpgrade`, `Retrieve`,
`Save`, `SaveAs`, `Archive`, `Close`, and `UpdateLibrary` for pushing types between libraries. **No
authoring route the project library lacks.**

### The finding that hurts the §14b path most, and it is not the one anyone was looking for

Three interfaces gate the operations that make a type library *maintainable*, and none of them
admits a Unified device:

| Interface | What it gates | Implementers — **the complete list** |
|---|---|---|
| `IUpdateProjectScope` | `LibraryType.UpdateProject(…)` — push a new version to its instances | `Hmi.HmiTarget` (**classic**), `SW.PlcSoftware` |
| `IInstanceSearchScope` | `LibraryTypeVersion.FindInstances(…)` — where is this type used? | `Hmi.HmiTarget` (**classic**), `SW.PlcSoftware` |
| `ILibraryTypeInstantiationTarget` | instantiate a type into a folder | classic screen folders, classic VBScript folders, `PlcBlockSystemGroup`/`UserGroup`, `PlcTypeSystemGroup`/`UserGroup` |

`HmiUnified.HmiSoftware` implements **none** of them. So on a Unified device you cannot ask the API
where a faceplate type is instantiated, and you cannot push a type update into it — even though the
types sit in the same project library the API reads happily.

**This qualifies §14b directly.** "A human authors a few types once, the AI instantiates them
hundreds of times" survives (Q1 is the test of that). What does *not* survive is the maintenance
half: when the human revises the type, propagating it to those hundreds of instances is a TIA-UI
operation with no API counterpart, and the AI cannot even enumerate what it would affect. That
should be written into the path as a stated cost, not discovered later.

**Not a blanket rule, which is what makes it a real fact:**
`HmiUnified.Library.ScriptModuleType : LibraryType` is the one Unified class that derives from
`LibraryType`. Unified content *can* be library content. Faceplates specifically are not modelled
that way in this API.

---

## 6. Q5 — a type from an existing screen. No such route, for anything

No method in either assembly takes a screen — Unified `HmiScreen` or classic `Screen` — or any
`IEngineeringObject`, and returns a `LibraryType` or `LibraryTypeVersion`. The sweep for every method
parameterised on `HmiScreen`, `Screen` or `IMasterCopySource` returns only `Contains`/`IndexOf` on
the two screen compositions, plus `MasterCopyComposition.Create(IMasterCopySource)` — which produces
a **master copy**, not a type, and which no Unified object satisfies (§4).

The API offers only the **reverse** direction, instantiate-from-a-type, and only for classic and PLC:

```
Hmi.Screen.ScreenComposition.CreateFrom(ScreenLibraryTypeVersion)      // classic
Hmi.RuntimeScripting.VBScriptComposition.CreateFrom(VBScriptLibraryTypeVersion)
SW.Blocks.PlcBlockComposition.CreateFrom(CodeBlockLibraryTypeVersion)
SW.Types.PlcTypeComposition.CreateFrom(PlcTypeLibraryTypeVersion)
```

There is no `CreateFrom(HmiScreen)` and no Unified member of that family at all. **So the TIA UI
action "make a type out of this" has no API counterpart for any object kind** — the only route to a
new type is a document (§5). Also worth noting for its own sake: `Hmi.Screen.ScreenLibraryType`
exists, so a **classic screen** genuinely can be a library type; that is a classic-only capability.

**Falsifier, and it is a real one.** `UIBase.GetService<T>()` and `IEngineeringServiceProvider` are
an extension point this sweep did not enumerate exhaustively, and Openness does hang real
capabilities off services (`AlarmClassDataProvider`, `SupervisionProvider`, `VersionControlInterface`
are all reached that way). A live `GetCompositionInfos()` / `GetAttributeInfos()` /
`GetServiceInfos()` walk on a faceplate `LibraryTypeVersion` is cheap, read-only, and is the honest
way to close this rather than by exhaustion of method names. It needs new CLI code.

---

## 7. What must be run, and what it costs

| Test | Answers | New code? |
|---|---|---|
| §2 steps 1–5: container → `ContainedType` → `Interface` → bind → compile → delete → compile | **Q1, the load-bearing one** | **No** — existing `hmi-create-screen` / `hmi-edit-screen` / `hmi-compile` |
| `LibraryTypeVersion.Export(FileInfo, ExportOptions.None)` on a `Consistent` faceplate version | Q2 — the untested export | **Yes** |
| `GetCompositionInfos`/`GetAttributeInfos`/`GetServiceInfos` on a faceplate type + version | Q5's falsifier; also what a version can even be navigated to | **Yes** |
| `library --master-copies` after a human hand-creates one from a Unified screen in the UI | Q3's residual — whether the *storage* takes Unified content | **No** (needs a human right-click) |
| `CreateFromDocuments` with a hand-written faceplate document | whether authoring is possible without a specimen | **Yes**, and only worth attempting if the export above produces something |

**All of it is blocked on one human action: approving the binary.** Both remaining `[REFLECTION]`
verdicts that could flip (Q2, Q5) need new CLI code as well, which needs a *further* approval after
the rebuild — so the sequence that wastes the fewest approvals is: **approve once, run the
no-new-code Q1 probe to exhaustion first**, then decide whether the export questions justify a build.

---

## 8. Corrections owed to other documents

> **A redaction lesson, added 2026-08-09 after the live run.** This section's original advice was
> *"use a faceplate type whose status is `Consistent` — P10's transcript shows three
> (`HmiType_5/6/7`)."* **That advice was impossible to follow.** The three `Consistent` entries are
> **image files**, not faceplates; every actual faceplate in the project is
> `DefaultVersionInconsistent`. P10's evidence had been anonymised to `HmiType_n`, which correctly
> removed the site's vocabulary and **also removed the distinction between an image and a
> faceplate** — so a later reader built a recommendation on a set that does not exist.
>
> The redaction was right; the *scheme* was lossy in a way nobody checked. **Anonymisation that
> preserves structure can still destroy the category that the next question turns on.** When
> anonymising, keep the type's KIND even when erasing its name.

Recorded here rather than edited in place, so the correction is reviewable before it propagates.

1. **`openness-hmi-faceplate-library.md`'s verdict box** — "no export format ⇒ no
   `CreateFromDocuments` route" is a non-sequitur; `CreateFromDocuments` takes no format. The
   conclusion may stand; the reasoning does not. Also: it says "there is no `Create` on any faceplate
   composition either" — true, and now known to be true of **every** library type, so it is not
   evidence about faceplates.
2. **`hmi-capability-record.md` §2** — *"`TagParameterDynamization` refuses outside a faceplate ⇒
   likely unreachable in practice"* is premature: the interface entries of a faceplate **container**
   are dynamization hosts (§2 above), and no probe has yet had a resolved faceplate to try it on.
3. **`hmi-capability-record.md` §2** — *"No faceplate document round trip"* should read *"no
   faceplate document round trip **via `ExportAsDocuments`**"*; `LibraryTypeVersion.Export` is
   untried.
4. **`hmi-ai-design-options.md` §14b** — the recommended path should carry the §5 maintenance cost
   explicitly: a Unified device is not an `IUpdateProjectScope` or an `IInstanceSearchScope`, so
   type-version propagation and instance discovery are UI-only.
5. **`src/openness-cli`** — `CreateScreenItem` binds only `Create<T>(String)`; the container-specific
   `Create<T>(String, String)` overload is unreachable. Not a defect today, but it is the fallback if
   `ContainedType` proves contextually read-only.

---

## 9. Scope

HMI engineering remains a **non-goal** (`docs/10-non-goals.md`) pending **ADR-0007**, which is
Proposed and undecided. This is a capability probe. Nothing here is a licence to build a feature, and
nothing here was written to the private project — the live half never connected.
