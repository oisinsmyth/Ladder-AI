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
screen, and it must not be cited as one.

### What this overturns

**`openness-hmi-api-survey.md` §5.B's central claim is withdrawn.** That section argued Unified's
"better verification story" — per-object, per-property errors *and* warnings, checkable before
committing — was the strongest argument for the Unified architecture, and §6 used it to revise the
alarm note's "there is no compile gate on the HMI side". That revision was **wrong**, and the
original claim in `hmi-alarm-generation.md` was right:

> **There is no compile gate on the HMI side.** Not on classic, and — now measured — not on Unified
> either. `Validate()` exists and returns a well-shaped result; it simply has nothing to say.

Both halves of the automated-checking story are now known to be shallow:

| Check | Depth | Evidence |
|---|---|---|
| `UIBase.Validate()` | passes structurally invalid screens | measured, above |
| `IHmiScript.SyntaxCheck()` | parses only; no name resolution | §4b |

### Why this matters more than it looks

The PLC side of this project is built on hard rule 4: nothing is presented until the tooling has
*proven* it valid, and FI-52 exists because a device compile that looked green was not proof enough.
**The HMI side has no equivalent and cannot be given one from the API.** Any HMI generation capability
therefore inherits a fundamentally weaker guarantee than the LAD pipeline, and that difference should
be stated to the engineer every time, not buried.

It also changes the architecture comparison. The parent survey framed the choice as *classic = fits
our machinery but weak gate* versus *Unified = doesn't fit but better gate*. The second half of that
is now false. Unified's real advantages are its object model and its readable tag/PlcTag join — not
verification.

### Why `Validate()` is shallow — it is structural, not a bug

`HmiValidationResult` carries a **`PropertyName`**. That is the tell: `Validate()` is a
**per-property** checker. It is therefore *structurally incapable* of answering cross-object
questions — "does this tag exist", "does this screen window point at a real screen", "do these two
items overlap" — because none of those belong to a single property.

So this is not a shortcoming that a later TIA version might fix, and not something to be worked
around by calling it differently. **`Validate()` will never be the reference-checking gate**, and any
design that assumed it might should stop.

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

Put that next to §4c and the consequence is sharp:

> **Every reference in a Unified HMI is an unchecked string, and the only thing that could check
> them — `Validate()` — has been measured not to.**

A typo in a tag name, a connection name, a data-log name or a screen name is therefore invisible to
every automated check available at engineering time. Nothing fails; the object simply refers to
something that is not there. This is precisely the failure mode the PLC side spends `tagstatus`,
`preflight` and the compile gate defending against — and on the HMI side none of those defences has
an equivalent.

**For a generation capability this is the central design constraint**, more than the flat item tree or
the faceplate wall: any tool that writes HMI content must **verify its own references before writing
them**, because nothing downstream will. That is the HMI analogue of hard rule 3, and it has to be
built rather than borrowed.

**What is creatable here** (reflection-confirmed): `HmiConnection`, `HmiDataLog`, `HmiAlarmLog`,
`HmiLoggingTag` (on `HmiTag.LoggingTags`), `HmiAlarmAuditClass` (`Create()`, no parameters),
`HmiOpcUaAlarmType(nodeId, connection, name)`. **Not creatable:** `HmiAuditTrail` (no `Create`, no
`Find`, no `Delete` — a TIA-made singleton reached by index), and `DriverProperty` (the set is fixed
by the chosen `CommunicationDriver`; only `.Value` is writable).

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
