# ADR-0007 — Should HMI engineering leave the "not now" list?

- **Status:** **Proposed** — put to the owner, undecided. Nothing in this document changes scope, and
  nothing may be built on it until it is accepted. `10-non-goals.md`'s "not now" line for HMI
  engineering stands until then.
- **Date:** 2026-08-08
- **Relates to:** `10-non-goals.md` "Not now" (HMI engineering — screens, scripts, faceplates) ·
  `16-future-ideas.md` **FI-18** (PLC/HMI boundary skill), **FI-35** (alarm-list generation),
  **FI-10** (HMI-importable alarm exports), **FI-54** (the probe programme) ·
  evidence: `docs/notes/openness-hmi-api-survey.md`, `docs/notes/openness-hmi-write-api.md`.

## Context

`10-non-goals.md` puts HMI engineering in "Not now (revisit only via ADR)": it may become a goal by
conscious decision, never by drift. Three entries in `16-future-ideas.md` are parked against that
line — FI-10 explicitly says "the line to hold is data export, not HMI engineering", FI-18's stated
risk is scope creep from *defining* the boundary into *building* screens, and FI-35 §5 recorded the
Openness question as "not investigated, should be answered before building".

On 2026-08-07/08 that question was investigated, and then the write path was probed live. The
capability probe was built and run under the `docs/13-data-boundary.md` JOB9002 write extension, and
CLAUDE.md already records what it is: **`hmi-create-screen`/`hmi-edit-screen` are a capability probe,
not an HMI capability — the non-goal stands and still needs an ADR.** This is that ADR. It exists
because the probing produced facts the non-goal was written without, and those facts point in both
directions.

Three constraints frame everything below, and none of them is a preference:

- **The panel family is forced by the hardware, and the reference job's panel is Unified.** The
  classic path — the one that would reuse this repo's existing SimaticML/IR/round-trip machinery
  almost verbatim — **does not apply to that hardware at all** (survey §7).
- **Unified has no screen export in any form.** Verified four independent ways (survey §8). There is
  no document to convert, so the IR/converter pattern does not transfer; a capability here is a
  builder driving a live object model.
- **Hard rule 4 has no HMI analogue today.** The PLC side's central guarantee is "nothing is
  presented until the tooling has proven it valid". What the HMI side has instead is set out below,
  and it is weaker.

## Update — the probe programme finished (2026-08-09)

This ADR was drafted after phase 1. FI-54's remaining Unified phases have since run
(`docs/notes/hmi-capability-probe-plan.md`, transcripts in
`docs/evidence/hmi-capability-probes.md`). **The arguments below are left as they were written**; the
four things the measurement changed are recorded here, and marked in place where they contradict a
paragraph.

1. **The alarm case — the strongest single argument in "The case for" — is materially weakened.**
   Alarm classes and discrete/analog alarms all create, and `Priority`, `StateMachine` and
   `AlarmClass` all set. But **the alarm text cannot be written at all**: `MultilingualTextItem
   .set_Text` throws. `RaisedStateTagBitNumber` also refuses on a fresh alarm. So "alarms need no
   screen work at all" still holds, and "the API supports them where the spreadsheet did not" now has
   an exception covering the two fields that carry the content. Whether this is a fixable ordering
   problem or a hard limit is **unknown** and is one targeted probe away.
2. **"Only ~2% has been walked" is answered, and answered in the ADR's own favour as an argument.**
   Live coverage moved to ~3% of members — but item types and dynamization kinds are now
   **exhaustively attempted**, and the exhaustive results are negatives: **21 of 56 item types
   refuse**, **3 of 6 dynamization kinds refuse** (including `Flashing`, which is how alarm state is
   displayed), and every refusal gives the same message with no reason. The predicted base rate held.
3. **Deletion — "0 of 184, the unrecoverable half" — is walked, and the answer is bad.** Deleting an
   object that is still referenced neither cascades nor refuses: **it orphans silently**, and only
   the compile notices. Automating deletion therefore requires a mandatory post-delete compile.
4. **The compile gate got stronger evidence, not weaker.** Across six phases it caught **five
   independent routes to a dangling reference** and named the missing field each time
   (`Trigger tag: No trigger tag is configured`, `No alarm class is configured for the alarm log`,
   …). Bare alarms and logs are useless, and the compile enumerates precisely what a generator would
   have to set — which is a usable specification, not just a gate.

**Option 4 ("defer pending FI-54's later phases") is now largely spent for Unified** — those phases
have run. What remains deferrable is classic, which is externally gated on hardware, and three narrow
follow-up probes. **No decision is taken here either; the point of the update is that the question can
now be answered on measurement rather than on a projection.**

## Decision

**None taken.** The question put to the owner is:

1. Should HMI engineering move out of `10-non-goals.md`'s "Not now" list and into `02-roadmap.md`?
2. If yes, **at what size** — the full screen-authoring capability, or a bounded alarm-and-contract
   capability (FI-18 + FI-35) with screens explicitly left excluded?
3. If no, what happens to the probe commands already built — frozen, kept read-only, or removed?

## The case for

**The write path is proven end to end, live.** Not reflected — executed against a real device and
read back from a fresh process: create a screen, create items, set attributes with type coercion,
attach and update event handlers, set and syntax-check script bodies, create a tag table and a tag,
**bind a property to a tag**, replace a binding, validate, compile, save (write-api §4i). The
create/modify/bind/script/compile chain is not a plan; it ran.

**The device compile is a genuine gate, and it catches the failure mode that matters most for
generated content.** Measured with a positive control, then re-run with a single variable isolated
(write-api §4e–4f): a deliberately broken event script produced
`SyntaxError: Unexpected identifier 's' in line 12, in column 8`, nested screen → item → error. And
it catches a **dangling reference by name, naming the property too**:
`The tag 'ZZ_AI_NoSuchTag_Dangling' for dynamization of the property 'Visibility' does not exist`
(§4h). A generator's characteristic defect is a reference to something that is not there; the
compiler finds it and says where.

**A fast pre-write check exists too, and does not need inventing.** `PlcTag`/`Address`/`DataType` are
derived read-backs: on a resolved binding `DataType` came back `'Int'`, on the dangling one both came
back empty. So the `tagstatus`/`preflight`-then-compile shape the PLC side uses has both halves
available on the HMI side — the fast one just has to be assembled from a read-back rather than called
by name (§4h). `SyntaxCheck()` likewise located the syntax fault at write time, before any compile.

**The API is self-describing, so the schema is queryable rather than documented.**
`GetAttributeInfos`/`GetCompositionInfos`/`GetCreationInfos` state the creatable types, each
attribute's access mode, and its **create-relevance** (`Mandatory`/`Relevant`/`None`) — which no
exported example could ever tell you. Measured on the real device: **56 creatable screen-item types,
nothing `Mandatory`, and the only `Relevant` attribute is `Name`** (survey §8). There is no
constructor-argument puzzle, and the item catalogue does not need pre-documenting because
`hmi --schema` dumps any of it on demand.

**Alarms are the concrete near-term use case, and the API supports them where the spreadsheet did
not.** FI-35 came out of a real delivery job. The reference device carries **311 discrete alarms and
20 alarm classes**, enumerable through the API; `DiscreteAlarms.Create(name)` and
`AlarmClasses.Create(name)` exist with typed trigger and bit-number fields. Two things the
spreadsheet route got wrong become expressible rather than assumed: bit numbering within a Word
trigger tag is a first-class field on Unified, and the `Class` column's collapse of severity against
acknowledgement behaviour is an artifact of the spreadsheet, not the domain — the API models three
independent axes. The `Tag`/`PlcTag` join that falsified the live job's name-equality assumption is
**readable** (survey §6, §7). Alarms need no screen work at all.

> **Contradicted in part, 2026-08-09 (update §1).** The typed fields exist and the classes create,
> but **`EventText` cannot be written** and `RaisedStateTagBitNumber` refuses on a fresh alarm. Read
> this paragraph as "the API *models* them where the spreadsheet did not" — modelling is confirmed,
> writing the text is not.

**Alarms also have no bulk path**, which cuts the same way: no import/export exists for them, so every
alarm is an individual API call. On 311 alarms that is precisely the difference between a spreadsheet
and a loop, and it is an argument *for* driving them programmatically (write-api §4).

## The case against

**Screen trees are one level deep, so all layout intelligence would be ours.** Sweeping every public
type for a child-item composition returns **exactly one hit** — `HmiScreen.ScreenItems`. Containers
reference an external type by *name string*, not by containment. Through Openness a screen is a flat
list of absolutely-positioned items; grouping is a naming convention, not a structure. The API
contributes nothing to layout (write-api §2).

**Faceplate types cannot be authored, only instantiated.** There is no Unified faceplate *type* class
in the assembly, and `HmiFaceplateInterfaceComposition` has `Find` and no `Create`. "Define a pump
faceplate once, stamp it forty times" can have its *second* half automated and not its first. Combined
with the nesting wall, **reuse cannot be expressed structurally at all** — only by repeating flat
items. That is the opposite of this project's whole pattern-library premise.

**`Validate()` is useless as a gate, and structurally so.** It accepted a **zero-pixel-wide screen**
with no errors and no warnings, and stayed silent on a screen carrying a deliberately unparseable
script. `HmiValidationResult` carries a `PropertyName` — it is a per-property checker, therefore
*incapable by construction* of cross-object questions. This is not something a later TIA version
fixes (write-api §4c). The survey's original main argument for the Unified architecture was withdrawn
on this evidence.

**The compile's own counts are wrong in both directions.** The baseline reported `WARNINGS: 0`
against **156** warnings in the message tree. The broken-script run reported `ERRORS: 1` against
**6** error messages. `CompilerResultState` has **no "not attempted" value**, so the FI-52 backstop
cannot be built here. A gate must ignore the aggregates entirely, read `State`, and walk `Messages`
recursively — meaning the one real gate on this side ships with a known-lying instrument attached
(§4e, §4f).

**There is no transaction.** A tag-creation run that threw on `HmiDataType` had already called
`Tags.Create`, never reached `Save()` — and the tag existed on the next run. Openness has no
rollback: **anything a command did before it failed may persist**, and a failure must never be read
as "nothing happened" (§4h). Writability is also **contextual**, not what the schema says: the same
run's `HmiDataType` set threw `Set is not allowed for disabled fields` on a property the schema
reported writable. The schema predicts what *may* be writable, not what is writable *now*.

**Classic HMI is unreachable in practice, and untouched in evidence.** 64 types, **zero live
contact**, because no classic device exists in the available project; its entire SimaticML round trip
is untested. Classic has also not gained or lost a single public type across V17–V20 while the
assembly grew ~28%, and it has **no alarm API at all**. So the architecture that fits this repo's
machinery is the one nobody here can test, and the one that can be tested fits the review machinery
hardly at all.

**Only ~2% of the surface has been walked, and confident readings have already proved wrong three
times.** Measured: **551 public HMI types, 4549 declared public members**; ~85 members invoked live
(**~2%**); **7 of 80** creatable kinds created; **3 of 56** screen-item types instantiated; **2 of
246** event values attached; **1 of 6** dynamization kinds; and **0 of 184** deletable types — the
whole destructive half of the lifecycle is unexercised, and it is the half where mistakes are
unrecoverable. Against that, three readings of the reflection map were confidently written down and
then measured false: alarm-class "states" (visuals, not acknowledgement semantics), the plant model
(half writable, not read-only), and `Validate()` itself. **Three consequential surprises out of 2%**
is the honest base rate for the remaining 98%.

> **Superseded 2026-08-09 (update §§2–3).** The tallies here are the phase-1 ones. Current: ~3% of
> members, **15 of 80** creatable kinds, **35 created of all 56** item types attempted, **3 created
> of all 6** dynamization kinds attempted, ~20 deletions across 9 kinds. The *argument* survives the
> update — the base rate held, and the new surprises (deletion orphans; alarm text refuses; a third
> of the item catalogue and half the dynamization kinds refuse without saying why) are again mostly
> negative. Event breadth is unchanged at **2 of 246**.

## Options considered

1. **Accept — HMI engineering in scope, screens included.** Buys the proven write path and the
   alarm case; commits to everything under Consequences below, on a surface 98% unwalked.
2. **Accept — bounded to alarms and the PLC/HMI boundary contract (FI-18 + FI-35), screens
   explicitly excluded.** The survey's §5.C: the smallest step with a real use case, needing no
   layout model, no faceplate story, and no serialiser. The guard rail has to be a written decision
   rather than an API limitation, because on Unified screen-building is one call away from
   boundary-defining.
3. **Reject — keep the non-goal.** The probe commands stay read-only (`hmi`) or are frozen/removed
   (`hmi-create-screen`, `hmi-edit-screen`), and the notes remain reconnaissance.
4. **Defer pending FI-54's later phases.** Decide once deletion, alarms, the data-plumbing half and
   classic have live contact — i.e. once the 2% figure is materially higher.

## Consequences

**Accepting (options 1–2) commits us to a second generation pipeline with weaker guarantees than the
LAD one.** Stated plainly, because it is the crux:

- **No hard-rule-4 analogue.** The device compile is a real gate for script content, reference
  integrity and per-object configuration, and is blind to geometry — a zero-width screen compiles
  clean in both measured runs. Its counts lie. So "proven valid before presentation" would have to be
  redefined for HMI work, not inherited.
- **No IR, therefore none of the review machinery.** No export means no diff, no `ir-hash`, no golden
  round-trip, no "prove the untouched networks identical". A **canonical serialiser must be written
  first** — it is the long pole, not the generation. Hard rule 7 also has no XML to protect here, and
  the `.rdf` runtime format is exactly what it exists to forbid touching.
- **Commands must be written re-runnable**, because a failure can leave partial state and there is no
  rollback.
- **We own the layout model and the reuse story.** Flat, absolutely-positioned items with no
  authorable faceplate types means repetition is the only available reuse mechanism.
- **Build-order cost.** A second dialect, a second reviewer family, and a second set of conventions,
  competing for slots against closing S6, opening S7, and library-filling (FI-21).
- Option 2 avoids the serialiser, the layout model and the faceplate story entirely, and keeps the
  compile gate for what it does cover. It does not avoid the transaction, contextual-writability or
  count-defect problems.

**Rejecting (option 3) costs:**

- **FI-35's HMI half stays blocked on a question that is now answered.** Its §5 said the Openness
  question should be answered before building; it has been, affirmatively for Unified. The 311-alarm
  case keeps going through the spreadsheet, including the failure mode already recorded — an assumed
  HMI/PLC name equality that is false on this project and fails silently, pointing at the wrong
  station's fault bit.
- **FI-18 stays boundary-only.** Which the survey argues is the only thing classic could ever have
  supported anyway, so on classic hardware this costs little; on Unified it is a deliberate refusal.
- **Built write tooling goes unexercised.** Unexercised Portal-touching tooling rots, and the
  measurements decay with it: the reflection map is version-specific and V21 exists, unexamined.
- Nothing is lost on the read side — `openness-cli hmi` is read-only and stays useful regardless.

**Either way, one thing is already true and should be recorded:** the write commands exist. Rejecting
requires an explicit disposition for them, not silence.

## Revisit triggers

~~Revisit if any of: FI-54's phase 2 lands~~ **(done 2026-08-09 — see the update above; this trigger
has fired and the ADR is updated, still undecided)**. Revisit if any of: a real job arrives on
**classic** hardware, which reopens the §5.A architecture
this repo's machinery already fits; a second alarm-extraction job comes up (FI-35's own trigger) and
the spreadsheet route bites again; Siemens ships a Unified screen export, which would collapse the
serialiser cost and most of the review-machinery objection at once; or the owner scopes FI-18, which
forces the option-2 boundary to be drawn explicitly rather than left implicit.
