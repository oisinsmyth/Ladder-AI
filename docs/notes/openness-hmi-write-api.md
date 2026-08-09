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

## 4k. Item-type breadth — `GetCreationInfos` OVERSTATES by 21 of 56 (2026-08-09) [LIVE]

P2. All 56 concrete screen-item types attempted on one throwaway screen, each independently so a
refusal reports rather than aborting the rest. One Portal round trip, 56 verdicts.

**35 created. 21 refused.**

### The metamodel's creatable list is not trustworthy

`GetCreationInfos("ScreenItems")` reports **56** creatable types (§8). Only **35** actually create.
The API advertises 60% more than it will deliver, and `Create<T>` carries **zero generic
constraints**, so nothing rejects the other 21 until runtime.

The 21 refusals are:

- **All 17 `*Base` types** — `HmiScreenItemBase`, `HmiWidgetBase`, `HmiShapeBase`, `HmiWindowBase`,
  `HmiSurfaceShapeBase`, `HmiCentricShapeBase`, `HmiCircularShapeBase`, `HmiEllipticalShapeBase`,
  `HmiPointBasedShapeBase`, `HmiTextWidgetBase`, `HmiScaleWidgetBase`, `HmiSelectionGroupBase`,
  `HmiControlWindowBase`, `HmiCompanionBase`, `HmiContainerBase`, `HmiSimpleScreenItemBase`,
  `HmiTrendControlBase`. **None of these is marked `abstract` in the assembly** — so neither the CLR,
  nor the generic signature, nor the metamodel will tell you they are not instantiable. Only trying
  will.
- **Four concrete types**: `HmiCustomWebControlContainer`, `HmiCustomWidgetContainer`, **`HmiLabel`**
  and **`HmiProcessControl`**.

The two custom containers are explicable — they are the types whose `ContainedType` names an external
control, and the composition has a second `Create<T>(name, containedTypeValue)` overload precisely
for them. **`HmiLabel` and `HmiProcessControl` are not explicable** and are recorded as UNKNOWN. A
label refusing to be created is genuinely odd.

**Every refusal produces the same opaque error**, with no distinguishing detail:

```
EngineeringTargetInvocationException: Error when calling method 'Create'
of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.
```

So the API will not even tell you *why* it refused — abstract-in-spirit, needs-a-second-argument, and
whatever afflicts `HmiLabel` are indistinguishable.

**Consequence for a generator: the creatable list must be established by trial and cached, not read
from the metamodel.** This qualifies §8's claim that self-description beats an exported example. It
still does for *attributes* — `AccessMode` and `CreateRelevance` are real and useful — but the
*creatable-type* list is aspirational. The honest summary is: the schema tells you the shape of what
exists; it does not reliably tell you what you may do.

### 34 of the 35 are valid bare — and the 35th is the same old story

Compiling with all 35 present produced exactly **one** error:

```
[Error] ZZ_AI_P2Screen: → HmiFaceplateContainer_P2:
  The referenced faceplate type does not exist. Select a valid faceplate type.
```

So **34 of 35 item types are valid with nothing but a name** — no mandatory follow-up configuration,
consistent with the schema's "nothing is `Mandatory`" (§8). A generator can create an item and style
it afterwards.

The one exception is `HmiFaceplateContainer`, and it fails for **the same reason as everything else
that has failed in this survey**: a reference to something that does not exist. That is now the
**third independent instance** of the pattern —

| Route to a dangling reference | Caught by |
|---|---|
| bind a property to a missing tag (§4h) | compile |
| delete a tag that bindings still use (§4j) | compile |
| create a faceplate container with no type (here) | compile |

Three different operations, one failure mode, one detector. **Reference integrity is the whole of HMI
correctness checking, and the device compile is the only thing that performs it.**

## 4l. P3–P6 — dynamizations, alarms, logs, structure (2026-08-09) [LIVE]

Thirty probes in one chained run. Device left with zero artifacts and a clean compile.

### P3 — only 3 of 6 dynamization kinds can be created

> **WRONG — SUPERSEDED BY P7 (§4m, 2026-08-09). Left standing because the mistake is the lesson.**
> **5 of 6 kinds create.** The gate is the **target property's type**, not the kind: this probe bound
> all three "refused" kinds to `Visible`, a Boolean, and read the resulting refusals as a property of
> the API. `Flashing` works on any colour property; `ResourceList` works on any text property. The
> paragraph below calling `Flashing` unreachable — and the alarm-display consequence drawn from it —
> is the reverse of the truth. Read §4m instead; keep reading here only for how the error was made.

| Kind | Result |
|---|---|
| `TagDynamization` | ✅ (§4h) |
| `ScriptDynamization` | ✅ created, `DynamizationType=Script` |
| `ExpressionDynamization` | ✅ created, `DynamizationType=Expression` |
| `FlashingDynamization` | ❌ refused |
| `ResourceListDynamization` | ❌ refused |
| `TagParameterDynamization` | ❌ refused |

All three refusals give the same opaque `Error when calling method 'Create'` — the identical
non-message the item-type refusals give (§4k). **Half the dynamization vocabulary is unreachable
through `Dynamizations.Create<T>`.** Whether Flashing/ResourceList/TagParameter need a different
route, a precondition, or a different property is UNKNOWN; the API declines to say.

This matters for alarm-state display specifically: **`Flashing` is how a real HMI shows an
unacknowledged alarm**, and it is one of the three that cannot be created this way.

Compile with the two new kinds present flagged only the script one:
`[Error] HmiIOField_2: The configured tag is invalid.` — a `ScriptDynamization` on `ProcessValue`
with no script yet is treated as an invalid tag configuration. Same pattern as everything else: a
reference that resolves to nothing, caught by the compile.

### P4 — alarms create, but the useful fields REFUSE

Alarm class, discrete alarm and analog alarm all created. Then:

| Attempt | Result |
|---|---|
| `AlarmClass = ZZ_AI_AlarmClass` on the alarm | ✅ set |
| `Priority = 7` on the class | ✅ set (Byte) |
| `StateMachine = RaiseClearRequiresAcknowledgement` | ✅ set (enum parsed) |
| **`RaisedStateTagBitNumber = 3`** | ❌ **REFUSED** — `set_RaisedStateTagBitNumber` threw |
| **`EventText` via `MultilingualText`** | ❌ **REFUSED** — `set_Text` on `MultilingualTextItem` threw |

**The two axes I corrected the survey about (§6) — `Priority` and `StateMachine` — are settable.
The two things an alarm actually needs are not.**

The bit number almost certainly refuses for the same reason `HmiDataType` did on a fresh tag (§4h):
**contextual writability** — the field is disabled until a trigger tag exists. That is consistent,
but it means alarm creation has an ordering requirement the schema does not express.

**The alarm-text refusal is the significant one.** `MultilingualTextItem.set_Text` threw even though
a language item existed to write into. So the awkward path the survey predicted is not merely
awkward — on this project it did not work at all. Until that is solved, **alarm generation through
Openness cannot produce alarm text**, which is most of the value in FI-35's use case. Cause UNKNOWN;
worth a targeted probe (is the item read-only until the alarm has a trigger? does it need the
project's editing language rather than the first language?).

Compile confirmed what was missing rather than what was set:
`Trigger tag: No trigger tag is configured` on both alarms, and `The trigger value is invalid`.

### P5 — logs and connections create; their settings are the work

Data log, alarm log and connection all created cleanly. The compile then named exactly what bare
objects lack:

```
ZZ_AI_AlarmLog: Database of the log must be on the same medium as the main database for alarm logging.
ZZ_AI_AlarmLog: No alarm class is configured for the alarm log. Assign at least one alarm class.
ZZ_AI_DataLog:  Database of the log must be on the same medium as the main database for tag logging.
```

Creation is trivial; **configuration is the entire task**, and the compile enumerates the required
fields precisely — which makes it a usable specification for what a log generator must set.

### P6 — structure works, and one of my own reports was wrong

Screen group created; screen window created and successfully pointed at another screen
(`HmiScreenWindow_1.Screen = ZZ_AI_GroupedScreen`); **compile clean with both present** — the only
phase whose compile passed, because nothing here left a dangling reference.

**Correction — `--in` did nothing.** P6.2 reported `created Screens 'ZZ_AI_GroupedScreen' in
'ZZ_AI_Group'`. That was **my tool repeating my intent, not reporting what happened.**
`HmiScreenComposition.Create` takes **only a name** (verified by reflection), so the two-argument
path silently fell back to the one-argument one and the screen was created **at the root**. Only
`HmiTagComposition` has a two-argument `Create(name, tagTableName)`.

So: **creating a screen inside a group is not reachable through the device-level `Screens`
composition at all.** It would require resolving the composition on the *group* (`group.Screens`),
which the generic `--kind` resolver — which only walks `HmiSoftware` — cannot currently do. Combined
with the earlier finding that screens **cannot be moved between groups**, grouping is currently
unreachable programmatically by this tool.

The tool now prints an explicit `WARNING: --in was IGNORED` in that case. A message that repeats the
caller's intent instead of what happened is worse than no message, and this one would have been
recorded as a successful grouping.

### The sweep found two more of the same defect — in the reporting, not the API

Re-reading the exit codes after the run: **P3.2 and P4.4 both exited 0** while printing refusals, and
both counted the refusals as work done — `changes applied: 5` for two successes and three refusals,
`3 change(s)` for one success and two refusals.

Three instances of one defect class in a single day, all mine: **the tool reported what was asked for
rather than what happened.** `--in` was the loudest, but the exit code is the worse one, because it
is the only signal a script reads and it said success.

Fixed: the count now reads `2 (3 REFUSED)`, and **any** refusal exits `7 = CommandError`. The
previous rule — exit 0 if at least one change landed — was defensible for a probe that wants every
verdict, but the verdicts print either way, so it bought nothing and cost the exit code its meaning.
Guarded by `DescribeAppliedCount_ExcludesRefusalsAndSaysHowMany`.

**Raw transcripts for all six phases: `docs/evidence/hmi-capability-probes.md`.**

## 4m. P7 — the dynamization refusals were MY PROBE, not the API (2026-08-09) [LIVE]

**P3's headline was wrong.** It reported three of six dynamization kinds as refused and concluded
half the vocabulary was unreachable. It is not. **A dynamization kind is gated on the type of the
property it is bound to**, and P3 bound all three to `Visible` — a Boolean — because holding the
property constant felt like the clean experimental design. It was the opposite: it confounded the
kind with the target.

Measured, one session, hypothesis and negative control together:

| Probe | Kind → property | Result |
|---|---|---|
| P7.2 | `FlashingDynamization` → `HmiRectangle.BackColor` | ✅ **created**, `DynamizationType=Flashing` |
| P7.3 | `ResourceListDynamization` → `HmiText.Text` | ✅ **created**, `DynamizationType=ResourceList` |
| P7.6 | `FlashingDynamization` → `BorderColor`, `HmiButton.BackColor` | ✅ both created |
| P7.7 | `ResourceListDynamization` → `HmiButton.Text` | ✅ created |
| **P7.4** | **`FlashingDynamization` → `HmiText.Visible`** | ❌ **REFUSED** (negative control) |
| **P7.5** | **`ResourceListDynamization` → `BorderColor`** | ❌ **REFUSED** (negative control) |
| P7.8 | `TagParameterDynamization` → `ProcessValue`, `Text`, `Width` | ❌ all three refused |

The negative controls are what make this conclusive rather than suggestive: the *same kinds* that
succeeded moments earlier refuse again the moment the property type is wrong, in the same session,
against the same screen.

**So the creatable tally is 5 of 6, not 3 of 6**, and the two kinds this project called unreachable
are not only reachable but reachable on every item that has a colour or a text property.

### Why the reflection map already said so, and why it was missed

`FlashingDynamization`'s own properties are `Color`, `AlternateColor`, `FlashingRate`,
`FlashingCondition`. `ResourceListDynamization` carries `ResourceList` (a name string) and `Tag`.
Neither has any meaning on a Boolean. The type shapes were dumped in P3's own session and read as
*what the object holds* rather than as *what it can attach to* — the same reading error as the
alarm-class "states" (§6) and the plant model. **Third time. The reflection map keeps being right
about structure and being read wrong about scope.**

Siemens documents it directly. The *Engineering Guideline for WinCC Unified* (SIOS entry 109827603):
*"Depending on the property it is also possible to use a resource list or flashing as a
dynamization"* — flashing for **colour** properties, resource lists for **text** properties. Recorded
as DOCUMENTED-and-now-MEASURED; Siemens' server 403s a direct fetch, but the same sentence surfaced
via two independent searches and the live probe agrees with it.

### `TagParameterDynamization` — refused in every position tried, and probably correctly

Three property shapes, all refused. The likely reason is not the property type but the **context**:
a *tag parameter* is a faceplate concept — a property bound to a parameter of the faceplate
interface, resolved per instance. Community sources state that tag parameters work inside faceplate
instances and not in screen windows. Every P7 target was an ordinary item on an ordinary screen, so
there was no faceplate interface for a parameter to refer to.

**UNVERIFIED** — no faceplate instance was tested. And it may stay unverified in practice: faceplate
*types* cannot be authored through Openness at all (§2), so this kind is reachable, if ever, only on
instances of faceplate types a human already built.

### Compile, again, named exactly what was missing

```
[Error] HmiButton_4:
[Error] No tag is configured for dynamization of the property 'Text'. Select an existing tag.
[Error] No resource list is selected for dynamization of the property 'Text'. Select an existing resource list.
```

A bare `ResourceListDynamization` needs both a tag and a resource list, and the compile says so by
name. **Sixth independent route to a dangling reference caught only by the compile.**

### A tooling gap this exposed

P7.9 tried `--set HmiRectangle_1.BackColor.FlashingRate=Fast` and was rejected: `hmi-edit-screen`
resolves a target as an **item name only**, so it cannot reach a property *of a dynamization*.
Creating a flashing dynamization is therefore currently possible while configuring its colours and
rate is not — an incomplete capability, not a broken one. `--set` needs a nested-path target
(`<Item>.<Property>.<DynAttr>`) before flashing is actually usable.

### The correction that matters most

**`Flashing` is available on every colour property**, and there is a *second* route to flashing that
P3 never touched: `TagDynamization.ValueConverter` → `MappingTable` → `Entries`, where each entry
carries `Value`, `AlternateValue`, **`Flashing`**, `FlashingRate`, with `ConditionType` ∈
{`Range`, `Bitmask`, `Singlebit`, `Expression`}. That is the value-range-to-colour-and-flash
mechanism a real alarm display is built from, hanging off a kind that already works. ~~**UNVERIFIED
live**~~ — **MEASURED 2026-08-09, and it works: see §4n.** The route is real, it compiles clean, and
it reaches flashing on property types where `FlashingDynamization` itself refuses.

So the FI-35 picture improves on the display side and is unchanged on the text side: alarm *state
display* is expressible; alarm *text* still cannot be written (§4l).

## 4n. P8 — the mapping table IS how an HMI flashes (2026-08-09) [LIVE]

**The hypothesis §4m raised is TRUE.** A `TagDynamization` on a property, with a mapping table whose
entries map tag values to values and set `Flashing`, creates, saves, reads back from a fresh process,
and **compiles clean**. 47 probe records across three chained runs (two of them one sweep resumed
after a harness fault); device returned to zero artifacts and `STATE: Success`.

It is also **strictly more capable than `FlashingDynamization`**, which is the finding that matters
most and the one nobody expected.

```
bind    HmiRectangle_1.BackColor <- tag 'ZZ_AI_MapTag'        (TagDynamization — colour property, accepted)
map     Create<MappingTableEntryRange>()
        From=1 To=5 Value=#FFFF0000 AlternateValue=#FF0000FF Flashing=True FlashingRate=Fast
read back, fresh process:
        mapping: ConditionType=Range entries=1 { MappingTableEntryRange From=1<Int32> To=5<Int32>
          RangeType=Range<RangeType> Value=#FFFF0000<Color> AlternateValue=#FF0000FF<Color>
          Flashing=True<Boolean> FlashingRate=Fast<FlashingRate> }
compile: STATE: Success   ERRORS: 0
```

### The seven questions, answered

**1. Which entry types does `Entries.Create<T>()` create?** One of four — and one of the failures is
not a refusal.

| Entry type | `Create<T>()` | Note |
|---|---|---|
| `MappingTableEntryRange` | ✅ **creates** | the workhorse; every positive result below is one of these |
| `MappingTableEntryBitmask` | ❌ refused | opaque `Error when calling method 'Create'` — but reachable another way, see Q5 |
| `MappingTableEntryBase` (**not** marked abstract) | ❌ refused | same non-message; §4k's "*Base types refuse" pattern again |
| **`MappingTableEntrySimple`** | 🔴 **CRASHES TIA PORTAL** | the Portal process dies; the client then throws `EngineeringObjectDisposedException: Access to a disposed object of type 'Siemens.Engineering.Project'` |

**The crash is isolated, not incidental.** Three occurrences, and the controls separate every
confound: with attributes set and without them (so it is the *create*, not a subsequent write); on
`HmiButton_3.BackColor` and again on a different screen's `HmiRectangle_2.BackColor` (so it is not
the target); and — the decisive one — **a `Range` entry on the exact target `Simple` had just killed
Portal on succeeded immediately afterwards** (so it is not a poisoned object, it is the type).

This is a **new failure class for this survey**. Everything that has failed so far *refused* — loudly,
changing nothing. This one takes the engineering tool down, and every subsequent command pays a full
Portal relaunch and project reopen. `MappingTableEntrySimple` must be treated as forbidden by any
generator.

The entry itself did **not** survive: the read-back shows `HmiButton_3` carrying no mapping at all,
so the crash happened before anything was committed. Everything created *before* that command did
survive, as always.

**2. Must `ConditionType` be set before creating a matching entry? NO** — and the expectation that it
would was wrong.

| Order | Result |
|---|---|
| `ConditionType=Range` then `Create<MappingTableEntryRange>()` | ✅ created |
| `Create<MappingTableEntryRange>()` with `ConditionType` still `None` | ✅ **created, identical read-back** |
| setting `ConditionType=Range` afterwards | ✅ accepted, entry unaffected |

So this is *not* another instance of §4h's contextual writability. Both orders work, and entries live
happily under `ConditionType=None` — three of the probe's mapping tables ended that way and compiled
clean. Testing both orders was worth it precisely because it produced the negative answer.

*(UNVERIFIED: the `Create(BitDynamizationType)` path was only ever exercised with `ConditionType`
already `Bitmask`. Whether it works from `None` was not tested.)*

**3. What CLR type do `Value`/`AlternateValue` take?** They are declared `object`, so the metamodel
says nothing — and the answer is **the target property's own type**, discoverable only by trying.

| Target property | Written as | Result |
|---|---|---|
| `BackColor` (a `System.Drawing.Color`) | `Color` | ✅ stored, reads back `#FF00FFFF<Color>` |
| `BackColor` | `String` ("Lime") | ❌ `set_Value` throws **`-Invalid value type`** |
| `BackColor` | `Int32` (65280) | ❌ same throw |
| `Visible` (a Boolean) | `Boolean` | ✅ stored, reads back `Value=True<Boolean>` |

**No silent coercion anywhere** — a wrong type throws rather than being converted, which is the
better of the two failure modes and worth recording as such. `AlternateValue` behaves identically.
This is the same shape as §4m's finding one level down: the *entry* is gated on the bound property's
type exactly as the *dynamization kind* is.

Defaults on an entry created bare: `Value=#FF7D7D85` (grey), `AlternateValue=#FFFF0000` (red) on a
colour target; `AlternateValue` comes back **null** on the Boolean target. So a bare entry on a
colour property is already a grey/red pair — never assume unset means empty.

**4. Does `Flashing` + `FlashingRate` stick? Yes, verified by read-back from a FRESH process** — two
separate `hmi --screen` runs in new processes, after save, on both sweeps. `Flashing=True<Boolean>`
and `FlashingRate=Fast|Slow|Medium<FlashingRate>` on entries across three items and two screens. Both
are also settable *after* creation through the nested `--set` path.

**5. `Create(BitDynamizationType)` returns `IList<MappingTableEntryBitmask>`** — a whole SET in one
call, and it is the **only** way to get a bitmask entry, since the generic `Create<T>` refuses one.

| Argument | Entries created | Shape |
|---|---|---|
| `SingleBit` | **2** | `Condition=0` and `Condition=1`, `Relevant=1` — the bit-clear and bit-set rows |
| `MultiBit` | **1** | `Condition=1`, `Relevant=1` |

**And they cannot be deleted.** `MappingTableEntryBitmask.Delete()` throws `Error when calling method
'Delete'`. `Delete()` is declared on `MappingTableEntryBase` and works on a `Range` entry, so this is
per-subtype, not per-composition. A bitmask entry is therefore **create-only**: the only route back
is deleting the whole dynamization. Anything that writes bitmask entries has no undo short of that.

**6. Does it compile? `STATE: Success`, 0 errors** — twice, on two different screens, with mapping
tables carrying ranges, colours and flashing on four properties across three items, and only the
device's 156 pre-existing warnings.

That is worth stopping on, because **every previous BINDING this programme created failed to compile
bare**: a `ScriptDynamization` with no script read as "the configured tag is invalid" (§4l), a
`ResourceListDynamization` wanted both a tag and a list by name (§4m), a faceplate container wanted a
type (§4k). A tag dynamization plus a mapping table is **complete on its own** — nothing further has
to be configured for the compiler to accept it. For a generator that is the difference between a
capability and a capability with homework.

*(Not a first for the programme overall — §4l's P6 compiled clean with a screen group and a screen
window, because nothing there left a dangling reference either. The claim is about bindings, which
until now had always left one.)*

**7. Does it work where `FlashingDynamization` refuses? YES — and that is the headline.**

Same session, same screen, minutes apart:

| Probe | What | Result |
|---|---|---|
| P8.14 | mapping-table entry, `Flashing=True`, `FlashingRate=Fast`, on `HmiText_2.**Visible**` (Boolean) | ✅ **created**, read back from a fresh process |
| P8.14b | `FlashingDynamization` on `HmiIOField_4.**Visible**` (Boolean) | ❌ **REFUSED** |

So **§4m's rule is a rule about `FlashingDynamization`, not about flashing.** "Flashing is available
on colour properties" is true of that *kind*; the mapping-table route carries `Flashing`/
`FlashingRate` on **any** property a tag can bind to, because the flashing lives on the entry rather
than on the dynamization. The two routes are not equivalent and the second is broader.

### The negative controls

§4m exists because a refusal was over-generalised without one. What a failure looks like here, so a
success is interpretable:

| Control | Result |
|---|---|
| `--map` onto a `FlashingDynamization` | refused, naming why: *only a TagDynamization carries a ValueConverter* |
| `--map` onto a property with **no** dynamization | refused: *there is no dynamization on that property — create one with --bind first* |
| nested `--set` at `Entries[99]` | refused: *index 99 is out of range — the composition holds 1 element(s)* |
| a `Range` entry on the target `Simple` had just crashed Portal on | **succeeded** — which is what makes the crash a fact about the type |

The refusals are the tool's own, not the API's, and that is the point: the two "no ValueConverter"
cases are structurally impossible rather than experimentally refused, so they had to be distinguished
from the API saying no. The `Entries[99]` control proves the nested path resolves for real rather
than swallowing what it cannot reach.

### What this changes, and what it does not

**Alarm-state DISPLAY is now fully expressible, by a route that compiles clean.** §4m improved the
display side by finding `FlashingDynamization` reachable; this proves the mechanism a real alarm
display is actually built from — value ranges mapping to colours, with per-range flashing — and shows
it is broader than the dedicated kind.

**Nothing about alarm TEXT changes.** §4l's block stands: `MultilingualTextItem.set_Text` throws, so
alarm *text* still cannot be written. FI-35's picture is now: state display solved, text blocked.

**A third thing changes, and it is not favourable.** Until now the refusal set was the boundary of
this API, and refusals are safe — they fail loudly and change nothing. `MappingTableEntrySimple`
introduces a call that **destroys the session**. A survey that only records "creates / refuses" has
no column for it. Recorded here as a category, not just an instance: *before trusting a Create on this
API, know that the failure mode may be worse than a refusal.*

### Tooling built for this (`openness-cli`, all shipped and tested)

- **Nested `--set` targets.** `<Item>.<Property>.<DynAttr>`, continuing through engineering objects
  and compositions: `Rect_1.BackColor.ValueConverter.MappingTable.Entries[0].Flashing=True`. This
  closes §4m's recorded gap — a dynamization could be created and not configured. Each step resolves
  a dynamization by property name first, then a CLR property; `[n]` indexes a composition.
- **`--map` / `--map-clear`.** Entry specs `<EntryType>[;<Attr>=<Value>]...`, including `bits:SingleBit`
  / `bits:MultiBit` for the non-generic overload. Every created entry is read back field by field
  **with the CLR type stored**, which is the only way Q3 was answerable at all.
- **Explicit value type tags** (`color:`, `int:`, `bool:`, `str:`, …) for members declared `object`,
  and `System.Drawing.Color` parsing, without which no colour could be set from a command line.
- **Mapping tables in the read path.** `hmi --screen` reports `ConditionType`, the formula flag and
  every entry. Unified has no screen export, so a fresh-process read *is* the evidence.

### A defect in my own tooling, found by this probe

**A `--set` that throws loses the record of the sets that already succeeded in the same command.**
P8.13 chained four value-type experiments; the first bad type aborted the command and the output
showed only the exception — so which of the four failed was unrecoverable from the transcript, and
Q3 had to be re-run one set per command. Openness commits eagerly, so the earlier sets *had*
persisted while the report said nothing about them.

This is the §4l `--in` defect again in a new place: **the output described the request rather than
what happened.** `--map` and `--bind-kind` already catch per item and report `REFUSED`; `--set` did
not. Fixed to match, so one bad set is now a reported refusal and a non-zero exit rather than a
silent partial write.

**Verification status of that last fix: unit-tested, NOT live-verified.** It needed a rebuild, and a
rebuild changes the binary's hash — which TIA treats as a new client and re-prompts a human for,
through the `Openness access` dialog inside Portal. The verification sweep (P9) sat on that dialog
for ten minutes and was then **stopped deliberately**: it is self-cleaning, but leaving an
unsupervised sweep armed to run whenever someone happens to click would put probe artifacts on the
device after this session had already certified it clean. It never connected, so it created nothing.

Everything else in §4n was measured with the binary that had already been approved. The one-line
verification still owed: a command carrying a good `--set`, a bad one, and another good one must
report all three, apply two, and exit 7.

### The approval dialog is a real constraint on unattended work

Worth recording because it shaped this whole session and is not in the operating protocol.
`HKLM\SOFTWARE\Siemens\Automation\Openness\20.0\Whitelist\openness-cli.exe` holds **one entry per
binary HASH**, not per path — 103 of them on this machine, 13 for this worktree's path alone. So
**every rebuild needs a fresh human approval**, and until it is granted `Attach()` simply hangs: no
error, no timeout distinguishable from a busy Portal, no message anywhere.

Two diagnostics turn a 25-minute mystery into a 10-second answer, and both are worth keeping:
compare the binary's base64 SHA-256 against the `FileHash` values in that registry key, and enumerate
window titles of the running Portal processes looking for `Openness access`. A hang with the dialog
up is a human gate; a hang without it is contention (`openness-quirks.md`).

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
| Distinct API members invoked | ~130 | 4549 | **~3%** |
| Creatable kinds actually created | **15** | 80 | 19% |
| Screen-item types instantiated | **35 created / 56 attempted** (§4k) | 56 | **63% created, 100% attempted** |
| Event values attached | **2** (`Tapped`, `Loaded`) | 246 | <1% |
| Dynamization kinds created | **5 created / 6 attempted** (§4m) | 6 | **83% created, 100% attempted** |
| **Deletions performed** | **~20, across 9 kinds** (§4j, §4l) | 184 | ~5% of types |

*Updated 2026-08-09 after P1–P6. The two "attempted" rows are the useful ones: item types and
dynamization kinds are now **exhaustively** probed, so their refusal sets are facts, not gaps.*

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

*Struck through as P1–P6 closed them; what survives is the residue, 2026-08-09.*

1. ~~**Deletion — 0 of 184 types.**~~ **CLOSED (P1, §4j).** Deletion **orphans silently**; the
   compile is the only detector. Nine kinds deleted across the programme, all confirmed absent on
   read-back.
2. ~~**Alarms — never created.**~~ **CLOSED, badly (P4, §4l).** Class, discrete and analog alarms all
   create; `Priority` and `StateMachine` set. But **alarm text cannot be written at all** —
   `MultilingualTextItem.set_Text` throws. FI-35's alarm case is therefore **blocked on an unexplained
   refusal**, not on missing tooling. This is the programme's most consequential single finding.
3. ~~**Connections, data logs, alarm logs**~~ — **CLOSED (P5, §4l).** All create trivially; the work
   is entirely in configuration, and the compile enumerates the required fields precisely enough to
   serve as a specification. (Logging *tags* still unwalked.)
4. ~~**The other 5 dynamization kinds**~~ — **CLOSED, and POSITIVE once the probe was fixed (P3 then
   P7, §4m).** `Script`, `Expression`, **`Flashing` and `ResourceList` all create** — flashing on any
   colour property, resource lists on any text property. P3's "three refuse" was a confounded probe
   (all three bound to a Boolean). Only `TagParameter` still refuses, and that is a faceplate-context
   concept, plausibly correct. **This gap is closed favourably, not adversely.**
5. **Plant views, runtime settings, faceplate containers, text lists** — still read/reflected only.
   Screen *groups* now create (P6), but **a screen cannot be created into one** through the
   device-level composition, and screens cannot be moved between groups — so grouping is
   programmatically unreachable.
6. **Classic HMI — 100% unwalked live.** 64 types, zero live contact, because no classic device
   exists in the available project. Its entire SimaticML round trip is untested. *(Won't-do: adding
   a device is hardware configuration, its own non-goal.)*
7. **Multi-language anything.** Still unwalked — and P4 suggests it may not merely be awkward but
   refused. Every string written has been invariant-culture.
8. **Event breadth — 2 of 246 values.** Unchanged by P1–P6. The *mechanism* is proven; the catalogue
   is not.

### What that means for "could you do anything to it?"

**Additively, on Unified: close to yes** — the create/modify/bind/script/compile chain is proven end
to end. **Destructively: unknown**, and that is a real hole. **On classic: no evidence at all.**

**Revised after P1–P6 (2026-08-09):** destructively is now **yes, but unsafely** — deletion works and
**orphans silently**, so it is automatable only with a mandatory post-delete compile. Additively the
answer has to come down slightly: not from anything failing to build, but because **three specific
things refuse and none of them say why** — alarm text, three dynamization kinds, and 21 of 56 item
types. "Anything to it" is bounded by a refusal set that only trial-and-error reveals, and that set
happens to contain the two capabilities alarm generation most needs.

**Revised again after P7 (§4m) — and the revision cuts the other way.** Two of those "three specific
things" were **my probe, not the API**: `Flashing` and `ResourceList` create fine on correctly-typed
properties. The additive answer goes back up. What survives is narrower and should be stated
precisely: **alarm text cannot be written**, `TagParameter` needs a faceplate context, and 21 of 56
item types refuse. The general lesson stands and is now better evidenced — *the refusal message never
says why*, so a refusal is only ever evidence about **that call**, never about the capability. Twice
now this project has generalised from one and been wrong.

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
