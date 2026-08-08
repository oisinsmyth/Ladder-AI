# WinCC Unified — the write/capability map (2026-08-07)

Companion to `openness-hmi-api-survey.md`, which establishes *what the HMI API is*. This one answers
the follow-on question the owner actually asked: **how much of an HMI could be driven programmatically,
and where are the walls?**

**Method and status.** Four parallel reflection surveys of the installed V20 assembly, plus web
research for the runtime script surface. **Reflection gives type shape, not runtime behaviour** — the
same caveat the parent survey carries. Where something has been executed against a real device it is
marked **[LIVE]**; everything else is shape-only. Agent findings that contradicted or extended the
parent survey were **re-verified directly** before being written down here, and two of them corrected
it (§6).

---

## 1. The capability map

| Area | Create | Modify | Delete | Status |
|---|---|---|---|---|
| Screens | yes | yes | yes | **[LIVE]** create + modify |
| Screen items | yes | yes | yes | **[LIVE]** create + modify |
| Event handlers | yes | yes (idempotent) | yes | **[LIVE]** |
| Event scripts | yes | yes | — | **[LIVE]**, `SyntaxCheck()` available |
| **Dynamizations (tag binding)** | yes | replace-only | yes | shape-only |
| HMI tags / tag tables / groups | yes | yes | yes | shape-only |
| Alarms (discrete/analog) + classes | yes | yes | yes | shape-only |
| Screen groups | yes | name only | yes | shape-only |
| Connections | yes | yes (by address string) | yes | shape-only |
| Data logs / alarm logs / logging tags | yes | yes | yes | shape-only |
| OPC UA alarm types | yes | yes | yes | shape-only |
| Audit trail | **NO** (TIA singleton) | configure only | **NO** | wall |
| Connection driver properties | **NO** (fixed by driver) | `.Value` only | — | wall |
| Text lists | **NO** | import only | — | wall |
| Graphic lists | **n/a — no such type in Unified** | — | — | wall |
| Plant views / view nodes | **yes** | — | yes | shape-only |
| Runtime settings (start screen, resolution) | n/a | yes | n/a | shape-only |
| **Script modules** | **NO** | import only | **NO** | wall |
| **Faceplate types** | **NO** | — | — | wall |
| **Nested screen items** | **NO** | — | — | wall |
| **Moving a screen between groups** | **NO** | — | — | wall |
| Plant *object* model (`Cpm` interfaces/members) | **NO** | — | — | wall |
| Runtime languages | **NO** (toggle/font only) | partial | — | wall |
| System tags / system text lists / audit trails | **NO** | — | **NO** | wall |

**Read the walls, not the yeses.** Most of the surface is creatable; the interesting engineering
question is what is not, because that is what an "AI designs the HMI" story would have to work around.

## 2. The four walls that shape any design

**Screen item trees are ONE LEVEL DEEP.** Sweeping every public type for a property of type
`HmiScreenItemBaseComposition` returns **exactly one hit: `HmiScreen.ScreenItems`** (verified
directly). Container types expose no child-item composition at all — `HmiContainerBase`,
`HmiCustomWebControlContainer` and `HmiFaceplateContainer` reference an *external* type by name
through `ContainedType : String`, which is what the second `Create<T>(name, containedTypeValue)`
overload sets. So through Openness a screen is a **flat list of absolutely-positioned items**, not a
tree. Any layout intelligence has to be expressed as coordinates, and grouping is a naming
convention, not a structure.

**Faceplates can be instantiated, never authored.** There is no Unified faceplate *type* class at all;
the only faceplate types in the assembly are Classic (`Hmi.Faceplate.FaceplateLibraryType`) and add
nothing over generic library plumbing. `HmiFaceplateInterfaceComposition` has `Find` and no `Create`.
This is the sharpest limit on reuse: the natural "define a pump faceplate once, stamp it 40 times"
approach cannot have its *first half* automated. Stamping is available; authoring is not.

**Script modules cannot be created or deleted** (verified directly: no `Create` on the composition,
no `Delete()` on `HmiScriptModule`, and `Name` is get-only). The composition offers only
`Import`/`Export(DirectoryInfo[, String])`, and the file format they use is **undocumented** — no
doc-comment in the shipped XML, no schema in the install. Shared script libraries are therefore
import-only and their format is a reverse-engineering job nobody should start casually.

**Screens cannot move between groups.** `Parent` is get-only and an assembly-wide sweep for
`Move`/`Reparent` found nothing applicable. Reorganising a screen hierarchy means delete-and-recreate,
which destroys the screen's contents. Get the grouping right at creation time.

## 3. Dynamizations — the PLC↔HMI coupling

The highest-value gap in the parent survey, and the shape is now clear.

```csharp
// Composition is keyed by PROPERTY NAME; PropertyName is get-only, so "re-bind" is delete+create.
var existing = item.Dynamizations.Find("ProcessValue");
existing?.Delete();                                             // DynamizationBase.Delete() exists

var d = item.Dynamizations.Create<TagDynamization>("ProcessValue");
d.Tag = "SomeHmiTag";      // plain string — NO compile-time or assign-time existence check
d.ReadOnly = false;

_ = d.PlcTag;              // GET-ONLY read-back. Empty => unresolved.
_ = d.Address;             // GET-ONLY
_ = d.DataType;            // GET-ONLY
```

`Dynamizations` is declared exactly once, on `UI.UIBase`, so every screen, item and part inherits it
identically — one mechanism, universally.

**This sharpens the tag-naming finding in the parent survey (§7).** `PlcTag` is **not settable on the
dynamization** — it is a read-back derived from the HMI tag's own `PlcTag`. So the chain is:

```
dynamization.Tag  ->  HmiTag.PlcTag  ->  PLC tag
   (you set this)     (set on the tag)    (the PLC's own name)
```

Which is exactly why the two names differed on the real project: they are set in two different places
and nothing forces them to agree. It also means the join an alarm/report tool needs is **derivable**,
because both halves are readable — but only by reading the *tag*, not the dynamization alone.

`ValueConverter`, `Trigger` and `MappingTable` have **no public constructor** — you configure the
object the getter returns, in place. `Trigger.Tags` reflects as `System.Object`; its real type is
UNKNOWN offline and needs `GetAttributeInfos()` against a live object.

**UNVERIFIED and important:** whether binding to a *nonexistent* tag fails at assignment, at
`Validate()`, or never. `Tag` being a plain string means nothing checks it at compile time. Until
tested, treat tag existence as a **precondition the caller must enforce** — which is the HMI analogue
of hard rule 3, on a surface where the tool cannot currently see the tag list at write time.

## 4. Tags and alarms

Every composition `Create` takes only a name (tags also accept `(name, tagTableName)`).

```csharp
var table = hmi.TagTables.Find("Process") ?? hmi.TagTables.Create("Process");
var tag   = hmi.Tags.Create("SomeTag", "Process");
tag.Connection = "<connection name>";
tag.AccessMode = HmiAccessMode.SymbolicAccess;
tag.PlcTag     = "<PLC tag>";          // the PLC hop lives HERE, not on the dynamization
tag.HmiDataType = "Bool";
```

`TagTableName` and `TagType` are **read-only** — a tag's table is fixed at creation. Tag tables nest
arbitrarily through `HmiTagTableGroup`.

**Alarm text is the awkward part.** The text property is a `MultilingualText`, which is get-only and
has **no `Create` on its `Items`** — you must `Find(language)` an existing project language and set
`.Text` on it. So alarm generation depends on the project's language set already containing what you
need, and runtime languages themselves cannot be added (`LanguageAndFonts` has no `Create`).

**Alarms have no import/export at all**, so unlike tags there is no bulk path — every alarm is an
individual API call. On a device with 311 discrete alarms that is the difference between a spreadsheet
and a loop, and it is an argument *for* driving them programmatically rather than by hand.

## 4b. Event scripts — the runtime surface, and two traps

Event handlers are the behaviour half of an HMI, and their bodies are JavaScript evaluated by the
Unified runtime, not by Openness. Two different worlds meet at `IHmiScript.ScriptCode`, and the
mismatch between them is where the traps are.

**Engineering side (reflection-confirmed).** `IHmiScript` is `ScriptCode`,
`GlobalDefinitionAreaScriptCode`, `Async` and `SyntaxCheck()`. **All 42 event-handler types expose
`Script : IHmiScript`** — no exceptions — so every event on every item type can carry code.
`SyntaxCheck()` returns `HmiValidationResult`, whose `Errors`/`Warnings` are plain
`IEnumerable<string>`: **no severity, no line numbers.** Useful as a yes/no, poor as a diagnostic.

**Runtime side (Siemens documentation).** The pieces that matter for self-contained behaviour:

```javascript
HMIRuntime.Trace("message");                       // goes to the RTIL Trace Viewer
let item = Screen.FindItem("Button_4");            // by-name lookup
item.Text = "Changed";
for (let scritem of Screen.Items) { … }            // Screen.Items is the ITERABLE
scritem.BackColor = HMIRuntime.Math.RGB(255,0,0);
Screen.ParentScreen.Windows("Window2").Screen = "OtherScreen";   // navigation
UI.RootWindow.Screen = "OtherScreen";
```

`alert()` is unavailable and `console` is undocumented. **`OpenScreenInScreenWindow` does not exist in
V20** — it is absent from the system-function list, so older examples using it are stale.

**Trap 1 — `Screen.Items` is not a by-name accessor.** `Screen.Items` iterates; the by-name lookup is
`Screen.FindItem(name)`. Calling `Screen.Items("SomeName")` **aborts the handler at runtime**. This
bit here: the first version of the probe script used `Screen.Items("…")` and would have failed
silently on tap. It was caught only because the runtime API was researched *after* the script was
written — and the write happened to fail on an unrelated Portal wedge before it landed, which is luck,
not process. **Write the script against the runtime docs first.**

**Trap 2 — `SyntaxCheck()` cannot see any of this.** It checks syntax, not names. A script that
references a nonexistent screen item, or uses `Screen.Items` as a function, is syntactically perfect
and will pass. So a green `SyntaxCheck()` says *"this parses"*, never *"this works"* — and it is the
only automated check available on the script half. Treat it exactly as far as it goes.

**Trap 3 — `.Text` means two different things.** In Openness it is a `MultilingualText` (get-only,
written via `Items.Find(language).Text`). At runtime it is a plain string. Code that looks identical
behaves differently depending on which side of the fence it runs on.

**Also confirmed walls on the script/list side:** `HmiScriptModuleComposition` and
`HmiTextListComposition` both have **no `Create`** — import-only, undocumented format — and **Unified
has no graphic-list type at all**.

## 4c. **`Validate()` is SHALLOW — measured, 2026-08-07** [LIVE]

The load-bearing question of both notes, finally tested, and the answer is the unwelcome one.

Two deliberately invalid states were written to a real screen and `Validate()` was called on each:

| Probe | State created | `Validate()` result | Is it actually invalid? |
|---|---|---|---|
| 1 | Screen `Width = 0` — a zero-pixel-wide screen | **no errors, no warnings** | yes — a zero-width screen is nonsense on any reading |
| 2 | Item at `Left = 99999, Top = 99999` | **no errors, no warnings** | **probably NOT** — see below |

Both were accepted, saved, and read back. `Validate()` ran in each case (it is not a no-op that
throws), and it objected to nothing.

> **Self-correction on probe 2.** Siemens documentation indicates Unified **deliberately supports
> screen content outside the viewport**, so an off-screen item may be perfectly legal rather than an
> error. If so, `Validate()` passing it proves nothing, and probe 2 is not evidence of anything. The
> conclusion below therefore rests on **probe 1 alone** — which is still decisive, but it is one
> probe, not two, and it was presented as two. A single clean result is weaker evidence than a pair.

**So `Validate()` is not a semantic gate.** It does not check geometry, bounds, or the coherence of a
screen. Whatever it does check — probably per-property type/format legality, which the `SetAttribute`
coercion has already enforced by the time it runs — it is not a substitute for a human looking at the
screen, and it must not be cited as one. It also stayed silent on a screen carrying a **deliberately
unparseable script** (§4f), which puts its uselessness beyond doubt.

### What this overturns — and what §4e/§4f then put back

**`openness-hmi-api-survey.md` §5.B's claim about `Validate()` is withdrawn.** That section argued
Unified's "better verification story" rested on per-object validation checkable before committing.
`Validate()` does not provide that.

**But the conclusion originally drawn from this — "there is no compile gate on the HMI side" — is
ALSO wrong**, as §4e and §4f then measured. A device compile *is* a gate: it rejects a broken script
with a located diagnostic and emits per-object semantic warnings. The correct statement is narrower
than either earlier version:

> **`Validate()` is not a gate. The device compile is** — for script content and per-object
> configuration. It does **not** check geometry, and its coverage of dangling references is untested.

The three checks, in order of usefulness:

| Check | Depth | Evidence |
|---|---|---|
| `UIBase.Validate()` | per-property; passes invalid screens *and* broken scripts | §4c, §4f |
| `IHmiScript.SyntaxCheck()` | locates syntax faults (line/col); resolves no names | §4b, §4f |
| **device compile** | **script errors + per-object semantics; blind to geometry** | §4e, §4f |

### Why this matters

The PLC side is built on hard rule 4: nothing is presented until the tooling has *proven* it valid,
and FI-52 exists because a green device compile was not proof enough. The HMI side now has a
comparable gate in the device compile — **but with the same class of trap, and worse**: its
`ErrorCount`/`WarningCount` are demonstrably wrong (§4e, §4f), so a naive gate reading those numbers
would pass a project with 156 warnings and, in the wrong direction, misreport error volume. Gate on
`State` and walk the message tree.

The architecture comparison also stands corrected: Unified's advantages are its object model, its
readable `Tag`/`PlcTag` join, **and a real compile gate** — just not the per-object validation that
was first claimed.

### Why `Validate()` is shallow — it is structural, not a bug

`HmiValidationResult` carries a **`PropertyName`**. That is the tell: `Validate()` is a
**per-property** checker. It is therefore *structurally incapable* of answering cross-object
questions — "does this tag exist", "does this screen window point at a real screen", "do these two
items overlap" — because none of those belong to a single property.

So this is not a shortcoming that a later TIA version might fix, and not something to be worked
around by calling it differently. **`Validate()` will never be the reference-checking gate**, and any
design that assumed it might should stop.

### 4e. **The HMI compile DOES check screen content — measured 2026-08-08** [LIVE]

Baseline compile of the real HMI device, everything in a valid state:

```
STATE: Success
ERRORS: 0  WARNINGS: 0

[Information] HMI_1:
[Success]     Hardware configuration:
[Information] Software compilation started.
[Information] Software compilation completed.
[Warning] No release button is defined for the object '<item>' in screen '<screen>'.   (x154)
[Warning] Zooming is centrally enabled/disabled in the device Runtime settings …
[Warning] The user "Anonymous" is not supported by WinCC Unified Runtime devices …
```

**This is the gate `Validate()` is not.** "No release button is defined for the object X in screen Y"
is a **per-object, per-screen semantic finding** — precisely the class of question a per-property
validator is structurally unable to ask (§4c). The compiler walks screen contents and reasons about
them. 156 diagnostics on a project whose screens were all authored by hand in TIA.

So the picture is now three-layered, and only the last one is worth anything:

| Check | Depth |
|---|---|
| `IHmiScript.SyntaxCheck()` | parses; resolves no names |
| `UIBase.Validate()` | per-property; blind to everything cross-object |
| **device compile** | **walks screen contents, produces semantic diagnostics** |

### ⚠ `CompilerResult.WarningCount` IS WRONG — do not gate on it

The header says `WARNINGS: 0`. The message tree contains **156 warnings**. The aggregate counts on
`CompilerResult` do not reflect the messages beneath it.

This is the FI-52 trap again, in a nastier form: PLC-side, a green device compile was *incomplete*;
here the count is **flatly false**. Anything gating on `ErrorCount`/`WarningCount` sees a clean
result and discards 156 real findings. **Walk `Messages` recursively and count them yourself** —
`openness-cli` collects the tree correctly but currently *reports* the header counts, which is a
defect to fix rather than a quirk to document.

`STATE: Success` alongside 156 warnings is defensible (warnings are not errors); `WARNINGS: 0` is
not.

### 4f. The positive control — compile IS a gate, with a hole [LIVE]

The control state: a deliberately unparseable event handler **and** `Screen.Width = 0`, on the same
screen, in one edit. Then compile.

```
STATE: Error
ERRORS: 1  WARNINGS: 0

[Error] ZZ_AI_TestScreen:
[Error]   HmiButton_3:
[Error]     SyntaxError: Unexpected identifier 's' in line 12, in column 8
```

**Compile is a real gate.** It rejected the broken script with a precise diagnostic, located to line
and column, nested screen → item → error. The positive control did its job: this result makes the
baseline interpretable, because we now know a clean compile means "checked and found nothing" rather
than "did not look".

**Re-run isolated (single variable), 2026-08-08.** The run above changed two things at once — broken
script *and* `Width = 0` — so it pointed at the script without isolating it. Repeated with `Width`
left at 1000 and **only** the script broken:

```
STATE: Error
ERRORS: 6  WARNINGS: 156        (counted from the message tree)
NOTE: compiler reported ErrorCount=1, WarningCount=0 — these disagree … and are not reliable

[Error] ZZ_AI_TestScreen:
[Error]   HmiButton_3:
[Error]     SyntaxError: Unexpected identifier 's' in line 12, in column 8
```

Identical diagnostic, no width message, single variable. **The broken script alone causes the
compile error** — the gate finding now rests on an isolated experiment rather than an inference from
a combined one.

The same run confirmed both fixes live: the injection **exited 8** (a broken script now fails the
edit command, where it previously exited 0 with the fault as commentary), and the recount plus its
disagreement note fired exactly as intended against the real compiler.

**But `Width = 0` produced no message whatsoever.** So the gate has a defined shape:

| Fault | Caught by compile? |
|---|---|
| Script syntax error | **YES** — exact line/column |
| **Dangling tag reference in a dynamization** | **YES** — names the tag *and* the property (§4h) |
| Missing release button (per object) | **YES** — as a warning (baseline, 154 of them) |
| Zero-width screen | **NO** — silent, in both runs |

So compile checks **script content, reference integrity, and per-object configuration**, and does
**not** sanity-check geometry. That is a genuinely useful gate — it covers the failure mode that
matters most for generated content (a reference to something that is not there) and misses the one
that matters least (a shape a human would spot instantly).

**`SyntaxCheck()` also caught it, and better than expected** — it returned
`Unexpected identifier 's' in Line 12 at Col 8` at *write* time, before any compile. So the
script-side check is more useful than §4b concluded: it does not resolve names, but it does locate
syntax faults precisely. Use it as a fast pre-check; use compile as the gate.

### ⚠ Second count defect: `ERRORS: 1` against 6 error messages

The invalid run reported `ERRORS: 1` while the message tree held **6** `[Error]` entries, and
`WARNINGS: 0` against 156 warnings. Both aggregate counts are unreliable in both directions. Gate on
`State`, and walk `Messages` yourself for anything you intend to show or count.

### The compile question, and how to test it properly

**Siemens documents no list of what a Unified HMI compile checks.** The compiling, screens,
cross-reference, scripting and readme chapters contain no such list; the Classic RT Professional page
*does* claim consistency checking, and the Unified pages carry no equivalent sentence. So the answer
cannot be read off the documentation — it has to be measured.

Two documented facts argue that compile will *not* catch our probes: Siemens explicitly declares
dangling cross-references harmless, and `Info > Compile` is documented as incomplete (some errors
surface under `Info > General` instead).

**The experiment needs a POSITIVE CONTROL**, and this is the methodological point worth keeping: if a
compile of a broken screen returns Success, that result is **uninterpretable on its own** — it cannot
distinguish "compile checked and found nothing wrong" from "compile does not check this at all, or
did not run". A known-bad state that compile *must* reject has to be included in the same run. A
deliberate JavaScript syntax error in an event handler is the strongest candidate, since script code
is genuinely built into the runtime.

Reflection facts for whoever runs it: `ICompilable` has exactly one member, parameterless `Compile()`
(no options overload — claims otherwise are wrong for V20). `CompilerResult` has `State`,
`ErrorCount`, `WarningCount`, `Messages` and **no `Success` flag**. `CompilerResultMessage` has its
own nested `Messages`, so **messages nest arbitrarily and counts are subtree aggregates — any gate
must walk recursively** (`openness-cli` already does; verified rather than assumed).
`CompilerResultState` is `Success | Information | Warning | Error` — there is **no "not attempted"
value**, which is exactly the gap FI-52 exists to cover PLC-side and which cannot be covered here.

Until that experiment runs, treat "it saved without complaint" as meaning exactly that.

## 4d. Everything binds by NAME STRING — and that compounds §4c

The data-plumbing half (connections, logs, logging tags, audit, OPC UA alarms) was surveyed
separately, and it produced a structural finding that matters more than any individual signature:

**Not one typed cross-link exists anywhere in this subtree.** A tag names its connection with a
string. A logging tag names its data log with a string. A dynamization names its tag with a string.
An alarm names its alarm class with a string. A screen window names its screen with a string. The
partner PLC of a connection is not even a reference — `Partner`/`Station`/`Node` are read-only, and
the writable knob is `InitialAddress`, a semicolon-separated `key=value` **string** (e.g.
`CommunicationInterface=…;HostAddress=…;PlcAddress=…;Rack=…;ExpansionSlot=…`).

Put that next to §4c and the alarming reading is that nothing checks any of it. **That reading was
wrong, and §4h measured it wrong** — recorded here rather than quietly rewritten, because the
corrected version is the useful one:

> **CORRECTED 2026-08-08.** A dangling reference is invisible to `Validate()`, but **the device
> compile catches it**, by name, with the property named too:
>
> ```
> [Error] ZZ_AI_TestScreen: → HmiRectangle_1:
>   The tag 'ZZ_AI_NoSuchTag_Dangling' for dynamization of the property 'Visibility'
>   does not exist. Select an existing tag.
> ```
>
> So the string-typed model is **not** unchecked — it is unchecked *at write time* and checked *at
> compile time*. That is a materially different, and much better, position.

What remains true, and still matters: **nothing checks a reference at the moment you write it.** The
API accepts `Tag = "<anything>"` without complaint, and `Validate()` stays silent. So a generator
still wants its own pre-write check — but it now has two ways to get one, neither requiring
invention:

1. **Ask the object.** The derived read-backs (`DataType`, `PlcTag`, `Address`) come back empty when
   the name did not resolve — §4h. Immediate, per-binding, free.
2. **Compile.** Slower and project-wide, but authoritative and it names the offender precisely.

The PLC analogy therefore holds better than first thought: `tagstatus`/`preflight` are the fast
pre-write checks, and the compile is the gate. The HMI side has both — they are just less obvious,
and the fast one has to be assembled from a read-back rather than called by name.

**What is creatable here** (reflection-confirmed): `HmiConnection`, `HmiDataLog`, `HmiAlarmLog`,
`HmiLoggingTag` (on `HmiTag.LoggingTags`), `HmiAlarmAuditClass` (`Create()`, no parameters),
`HmiOpcUaAlarmType(nodeId, connection, name)`. **Not creatable:** `HmiAuditTrail` (no `Create`, no
`Find`, no `Delete` — a TIA-made singleton reached by index), and `DriverProperty` (the set is fixed
by the chosen `CommunicationDriver`; only `.Value` is writable).

## 4h. Dynamizations, proven live — and how to detect a dangling tag (2026-08-08) [LIVE]

The largest untested capability, now exercised. A throwaway tag (`ZZ_AI_*`, invented, in its own
invented table) was created as a bind target, then two bindings were made on the test screen: one to
that tag, one to a name that does not exist.

```
bind created HmiText.Visible      <- tag 'ZZ_AI_TestTag'              [resolved: plcTag='' dataType='Int']
bind created HmiRectangle.Visible <- tag 'ZZ_AI_NoSuchTag_Dangling'   [UNRESOLVED (PlcTag and DataType both empty)]
Validate(): ran, returned no errors and no warnings
```

**Creating a dynamization works** — `Dynamizations.Create<TagDynamization>(propertyName)` then
`Tag = "<name>"`, exactly as §3's recipe predicted. The PLC↔HMI coupling is drivable.

**Binding to a nonexistent tag is accepted silently.** No exception at assignment, and `Validate()`
had nothing to say — confirming §4d's fear directly rather than by inference.

### But the derived read-backs ARE a usable detector

This is the practically useful discovery. `PlcTag`, `Address` and `DataType` are get-only fields
*derived from the resolved tag*. On the good binding `DataType` came back `'Int'`; on the dangling
one **both came back empty**. So although nothing *checks* the reference for you, the API will tell
you whether it resolved — if you ask immediately after setting it:

```csharp
d.Tag = tagName;
if (string.IsNullOrEmpty(d.DataType) && string.IsNullOrEmpty(d.PlcTag))
    // the name did not resolve — treat as an error
```

That is the "verify your own references before writing them" mechanism §4d said had to be built. It
turns out not to need building from scratch: **the API supplies the evidence, it just does not act on
it.** `openness-cli hmi-edit-screen --bind` now reports `resolved:` / `UNRESOLVED` per binding on
this basis.

Caveat on the check's strength: `plcTag` was empty on the *good* binding too (an internal tag has no
PLC counterpart), so `PlcTag` alone is not the signal — `DataType` is the discriminator here, and
whether that holds for every tag kind is unverified.

### Two API behaviours worth knowing

**Writability is CONTEXTUAL, not what the schema says.** Setting `HmiDataType` on a freshly created
tag threw `Set is not allowed for disabled fields`. `GetAttributeInfos()` reports a static
`AccessMode`; actual writability depends on the object's *current state* (here, presumably that a
connection/tag type is not yet established). So the schema (§8 of the parent survey) predicts what
*may* be writable, not what is writable *now* — a generator must tolerate refusal on individual
properties rather than treating the schema as a contract.

**There is no transaction, and a failed command can leave a partial object.** The tag-creation run
that threw on `HmiDataType` had already called `Tags.Create`, and never reached `Save()` — yet the
tag existed on the next run. Openness has no rollback: **anything a command did before it failed may
persist.** Commands must therefore be written to be re-runnable, and a failure must never be read as
"nothing happened".

## 4j. Deletion, measured — it ORPHANS (2026-08-08) [LIVE]

P1 of the probe programme. Thirteen probes; the whole deletion lifecycle, ending with the device
returned to a clean compile and **zero surviving probe artifacts**.

### The answer that mattered: delete-in-use orphans silently

`ZZ_AI_TestTag` had **two live bindings** on the test screen. Deleting it:

```
deleted Tags 'ZZ_AI_TestTag' [HmiTag]; confirmed absent on re-read
```

It did **not** refuse, and it did **not** cascade. Reading the screen back afterwards:

```
HmiRectangle_1   Visible <- Tag  tag=ZZ_AI_TestTag      <-- the tag no longer exists
HmiText_2        Visible <- Tag  tag=ZZ_AI_TestTag
```

**The bindings survive as orphans, pointing at nothing.** Then compiling:

```
STATE: Error   ERRORS: 8
```

So the three-way question — cascade, orphan, or refuse — resolves to **orphan**, with the compile as
the only thing that notices.

**Operational consequence, and it is not optional: a compile is MANDATORY after any delete.** Nothing
at delete time warns you; `Validate()` stays silent (as ever); and the only signal is a compile error
that arrives whenever someone next compiles — which, on a project nobody compiles for a week, is a
week later. Any generated-HMI workflow that deletes must compile in the same breath.

The mirror-image finding to §4h: **binding to a missing tag and deleting a bound tag produce exactly
the same end state** — a dangling reference — reached from opposite directions, and caught by exactly
the same check.

### The rest of the lifecycle works, and self-verifies

| Probe | Result |
|---|---|
| P1.1 delete screen item | deleted, confirmed absent |
| P1.2 delete dynamization | deleted, item intact |
| P1.3 delete event handler | deleted, script gone with it |
| P1.4 delete screen | deleted |
| P1.5 delete tag table | deleted |
| P1.6 delete nonexistent | clean, actionable error |
| P1.9 final inventory | **0 probe artifacts** |
| P1.10 final compile | **`STATE: Success`, 0 errors** |

Every delete re-reads afterwards; all reported "confirmed absent on re-read". Deletion appears to be
immediate and durable — no separate `Save()` was needed for it to take (unlike the *failed create*
in §4h, which persisted without one; both point the same way: **Openness commits eagerly**).

**The prefix guard was verified against a real object.** Attempting to delete `MainScreen` — one of
the 48 real screens — was refused:

> Refusing to delete 'MainScreen': this tool only deletes its own probe artifacts, whose names start
> with 'ZZ_AI_'.

A safety mechanism that has never been seen to refuse is not yet a safety mechanism; this one has.

### A defect in my own code, found by the probe, worth recording as process

P1.6 and P1.6a both exited **5 (UnexpectedError)** instead of 7. The cause: the entire `Hmi*`
exception family was mapped to `CommandError` that *morning* — and six new exceptions were added
that *afternoon* for the metamodel commands without being mapped. **The same defect, recreated within
hours of fixing it.**

The existing reflection guard covers `ParseResult` variants, not exception types, so nothing caught
it. Fixed, and a second guard added (`HmiExceptionsAreClassified`) that enumerates the exception
family by reflection and fails if any member falls through to `UnexpectedError`.

The lesson is about the shape of the defence, not this instance: **a guard that enumerates a family
survives someone adding to it; a list that must be remembered does not.** Two of this session's
defects were the same shape — a second switch nobody updated, and a second family nobody extended.

## 4i. Gap register — what has actually been WALKED, and what has not (2026-08-08)

"Mapped" and "walked" are different questions and give very different answers. The surface, measured:

| Denominator | Count |
|---|---|
| Public HMI types | 551 |
| Declared public members | 4549 |
| Creatable composition kinds (`Create` exists) | **80** |
| Deletable types (`Delete()` exists) | **184** |
| Concrete screen-item types | **56** |
| Event values across 40 event enums | **246** |
| Dynamization kinds | **6** |

### Walked live — the honest tally

| Axis | Walked | Of | Share |
|---|---|---|---|
| Distinct API members invoked | ~85 | 4549 | **~2%** |
| Creatable kinds actually created | **7** | 80 | 9% |
| Screen-item types instantiated | **3** (Rectangle, Text, Button) | 56 | 5% |
| Event values attached | **2** (`Tapped`, `Loaded`) | 246 | <1% |
| Dynamization kinds created | **1** (`TagDynamization`) | 6 | 17% |
| **Deletions performed** | **0** | 184 | **0%** |

### But count MECHANISMS, not instances

Instance coverage understates it, because the catalogue is repetitive. The distinct *mechanisms* of
driving a Unified HMI are nearly all walked:

read the device tree · enumerate screens (incl. groups) · read items, properties, dynamizations and
events · dump the creation/attribute schema · create a screen · create items · set attributes with
type coercion · attach and update event handlers · set and syntax-check script bodies · create tag
tables and tags · **bind a property to a tag** · replace a binding · validate · **compile** · save ·
read back from a fresh process.

What is *not* walked is mostly **more of the same shape**: instantiating a `HmiGauge` exercises the
same `Create<T>` as `HmiButton`; attaching `KeyDown` uses the same enum-keyed `Create` as `Tapped`.

### The real gaps — zero live contact, ranked by consequence

1. **Deletion — 0 of 184 types.** Nothing has ever been deleted. The whole destructive half of the
   lifecycle is unexercised, and it is the half where mistakes are unrecoverable. Highest-value
   remaining probe.
2. **Alarms — never created.** 311 exist on this device and the alarm use case (FI-35) is the most
   concrete one this project has. `MultilingualText.Items.Find(language)` for alarm text is entirely
   unverified, and it is the awkward part.
3. **Connections, data logs, alarm logs, logging tags** — never created. This is the data-plumbing
   half; a generated HMI that logs anything needs it.
4. **The other 5 dynamization kinds** — `Script`, `Flashing`, `Expression`, `ResourceList`,
   `TagParameter`. `Flashing` and `Script` are how real HMIs express alarm state.
5. **Screen groups (create), plant views, runtime settings, faceplate containers, text lists** —
   read or reflected only.
6. **Classic HMI — 100% unwalked live.** 64 types, zero live contact, because no classic device
   exists in the available project. Its entire SimaticML round trip is untested.
7. **Multi-language anything.** Every string written has been invariant-culture; `MultilingualText`
   has never been written to.

### What that means for "could you do anything to it?"

**Additively, on Unified: close to yes** — the create/modify/bind/script/compile chain is proven end
to end. **Destructively: unknown**, and that is a real hole. **On classic: no evidence at all.**

The three findings that most changed the design picture all came from *walking*, not reading:
`Validate()` being useless, the compile catching dangling references, and writability being
contextual. That ratio — three consequential surprises from ~2% of the surface — is the argument for
walking more of it before trusting any of the remaining 98%.

## 4g. Coverage — how much of the HMI surface is actually mapped (2026-08-08)

Measured, not estimated: **551 public HMI types, 4549 declared public members** in the V20 assembly
(`Siemens.Engineering.Hmi.*` 64 types / 633 members; `Siemens.Engineering.HmiUnified.*` 487 / 3916).

Four levels of "mapped", because they are worth very different amounts:

| Level | Meaning | Types | Share |
|---|---|---|---|
| **L3 live-verified** | exercised against a real device | ~10 types, ~60 members | **~1% of members** |
| **L2 member-detailed** | every property/method read and written down | ~213 | ~39% |
| **L1 enumerated** | type names and bases only | ~337 | ~61% |
| **L0 untouched** | never looked at | ~0 | — |

**What is L2 or better:** all of classic (64 types), and Unified's `Common`, `UI.Base`, `UI.Screens`,
`UI.ScreenGroup`, all four `UI.Dynamization*`, `HmiTags`, `HmiAlarm(+Common)`, `HmiConnections`,
`HmiLogging(+Common)`, `LoggingTags`, `HmiAudit`, `HmiOpcUaAlarm`, `Scripts`, `TextGraphicList`,
`Library`, and `Cpm` (partly).

**What is only L1 — and it is the bulk:** `UI.Events` (85 types, **1095 members**), `UI.Parts` (74,
689), `UI.Shapes` (20, 220), `UI.Widgets` (18, 186), `UI.Controls` (10, 106), `UI.Features` (15, 74),
`UI.Enum` (97 enums), `RuntimeSettings` (18, 194). Those namespaces hold **~52% of all declared
members**.

**But raw member count overstates that gap**, and saying so matters more than the number:

- `UI.Events`' 1095 members are ~85 near-identical `HmiXxxEventHandler(Composition)` pairs. The
  *structure* is fully mapped — every handler exposes `Script : IHmiScript`, `Create`/`Find` are
  keyed by a per-type event enum, and the enums are enumerated (§4b/§4f). Reading the other 80 types
  would add almost nothing.
- `UI.Enum`'s 97 types declare zero members by construction; the ones that matter for authoring are
  enumerated already.
- `UI.Parts`/`Shapes`/`Widgets`/`Controls` are the *item catalogue*. Their shapes are highly regular
  and, more usefully, **`hmi --schema` dumps any of them from the live device on demand** — so they
  are queryable rather than needing pre-documentation. 13 of them are already dumped in full.

**The honest headline is the L3 row, not the L2 one.** Roughly **1% of the surface has been proven
against a real device**: screen create/edit, item create, attribute set, event create/update, script
attach, `SyntaxCheck`, `Validate`, device compile, and the read walk. Everything else — every tag,
alarm, connection, log, dynamization and runtime setting — is **reflection-shaped only**, and this
session has already produced three cases where a confident reading of a shape turned out wrong when
tested (alarm-class "states", the plant model, `Validate()` itself).

So: the *map* is good enough to plan with; the *territory* has barely been walked.

## 5. What "do anything to it" would still require

Ordered by what actually blocks a general capability:

1. **Test whether `Validate()` has any depth.** Still the load-bearing unknown; it has only ever been
   shown valid input. Everything called a "gate" here rests on it — and §4b now adds that its
   script-side counterpart, `SyntaxCheck()`, is *definitely* shallow (syntax only, no name
   resolution). If `Validate()` turns out to be the same, then **Unified has no real verification
   story at all**, and §5.B of the parent survey — the main argument for the Unified architecture —
   collapses. That makes this test more important than it looked when it was merely "nice to have".
2. **Prove the dynamization write path live**, including the nonexistent-tag question in §3.
3. **Decide the faceplate story**, because the nesting + faceplate-authoring walls together mean
   reuse cannot be expressed structurally — only by repeating flat items.
4. **A layout model.** One level deep and absolute coordinates means any "design a screen" capability
   owns its own layout reasoning; the API contributes nothing.
5. **A serialiser** (parent survey §5.B) — still the long pole for review, unchanged.

## 6. Corrections to the parent survey

Two claims in `openness-hmi-api-survey.md` were wrong. Both came from reading type *names* rather than
type *shapes*, and both were caught by re-verifying an agent's contradicting report.

**Correction 1 — alarm class states are VISUALS, not acknowledgement semantics.** §6 of the parent
survey said `HmiAlarmClass` keeps severity and acknowledgement apart via "`Priority` on one axis;
`StateMachine`, `AcknowledgedState`, `ClearedState`, `AcknowledgedClearedState` on the other". Wrong:
`RaisedState`/`AcknowledgedState`/`ClearedState`/`AcknowledgedClearedState` are **not enums** — each
is an `AlarmStatusVisuals` subclass carrying `BackColor`, `TextColor` and `Flashing`. They describe
how an alarm *looks* in each state.

The conclusion survives and is in fact cleaner than stated — there are **three** independent axes, not
two:

| Axis | Field |
|---|---|
| Severity | `Priority : Byte` |
| Acknowledgement behaviour | `StateMachine : HmiAlarmStateMachine` — `Raise`, `RaiseClear`, `RaiseRequiresAcknowledgement`, `RaiseClearOptionalAcknowledgement`, `RaiseClearRequiresAcknowledgement`, `RaiseClearRequiresAcknowledgementAndReset` |
| Per-state appearance | the four `AlarmStatusVisuals` properties |

So the parent survey's point against the spreadsheet's single `Class` column stands, and is stronger:
one column has to carry what the API models as three separate things.

**Correction 2 — the plant model is only half read-only.** The parent survey said the `Cpm` namespace
is read-only, "expose `Find(name)` but no `Create`". Verified directly: `PlantViewComposition.Create(String)`
and `PlantViewNodeComposition.Create(String[, String])` **do** exist, and both types have `Delete()`.
What is genuinely `Find`-only is the *object* half — `PlantObjectInterfaceComposition`,
`PlantObjectInterfaceMemberComposition`, `PlantObjectLoggingTagComposition`. So plant *views* can be
built programmatically; plant *object interfaces* cannot. (Note the root is project-level
`Project.PlantViews`, not `HmiSoftware`.)

**Method note worth keeping.** Both errors were of the same kind: a property called `AcknowledgedState`
and a namespace with `Find` methods both *looked* self-explanatory. Neither was checked against its
actual type. When a name implies a semantic, reflect the type — the assembly is the authority, and a
plausible name is not evidence.
