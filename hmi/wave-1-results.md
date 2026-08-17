# Wave 1 — results

**Executed 2026-08-17**, on the owner's instruction, immediately after ADR-0007 was accepted.
Plan: [`PLAN.md`](PLAN.md) §3. Everything below was run, not designed.

---

## Headline

**K1 came back positive, and it came back bigger than the question asked.** A Classic Basic screen
exports as SimaticML **with its full content** — every item, absolute geometry, colours, layers and
groups. The Classic architecture in `PLAN.md` §0 is not merely viable; the file it depends on is
richer than the plan assumed.

**The one assumption that broke was mine, not the plan's:** Basic panels do not have square pixels.

---

## Lane P — Portal queue

### K1 — does a Classic Basic screen export as SimaticML? ✅ YES

Two runs against the anchor, both read-only, both exit 0.

**Walk:** device `[Classic]`, **5 screens**. The family classification in
`docs/notes/openness-hmi-api-survey.md` is confirmed against a real Basic panel for the first time.

**Export:** one screen → **224 KB of SimaticML**, root element `<Hmi.Screen.Screen>`.

```
<ActiveLayer>0</ActiveLayer>   <BackColor>182, 182, 182</BackColor>
<Width>800</Width>             <Height>480</Height>
```

Element census from that one screen:

| type | n | | type | n |
|---|---|---|---|---|
| Circle | 31 | | Button | 6 |
| Rectangle | 30 | | SoftKey | 5 |
| Line | 30 | | Group | 5 |
| GraphicView | 9 | | Switch | 2 |
| TextField | 6 | | **ScreenLayer** | 1 |

### 🔴 The finding that changes how the survey should be read

`openness-hmi-api-survey.md` says Classic "models nothing inside a screen". **That is true of the
OBJECT MODEL and false of the FILE**, and the difference is the whole programme:

- `<Hmi.Screen.ScreenLayer>` — **layers exist**, which was Unified probe P1's question and is
  answered here in the format itself.
- `<Hmi.Screen.Group>` — **groups exist**, the `<div>` analogue the research listed as open question 8.
- Every item carries absolute `Left` / `Top` / `Width` / `Height` — **exactly T1's output shape**.

A sample Button's attributes map almost one-for-one onto CSS: `BackColor`/`ForeColor`/`BorderColor`/
`BorderWidth`/`CornerRadius`/`Flashing`/`Enabled`/`Visible`. The research's §5.2 predicted this
mapping; it is now observed rather than predicted.

### New capability built to get there

`openness-cli export --screen <name>` (Classic only). It did not exist — `export` was PLC-shaped
(`--block`/`--type`/`--tagtable`), so **`PLAN.md` §7's claim that "T6 already exists" was wrong** and
is corrected. Unified is refused **by name**, never by returning an empty file: silence there would
read downstream as "no such screen", which is a different and wrong conclusion.

---

## Lane C — Sources ✅

Panel datasheets retrieved. Both Basic rows move `[INFERENCE]` → `[VERIFIED]`:

| panel | order no. | active area | px/mm H | px/mm V |
|---|---|---|---|---|
| KTP700 Basic (target) | 6AV2123-2GB03-0AX0 | 154.1 × 85.9 mm | 5.19 | **5.59** |
| KTP900 Basic (anchor) | 6AV2123-2JB03-0AX0 | 198 × 111.7 mm | 4.04 | **4.30** |

### 🔴 Correction — and it was a correction to my own work

`sizing-standard.md` asserted square pixels, generalising from the **Unified MTP700** (152 × 91 mm,
genuinely square). The Basic panels are **16:9 physical carrying a 5:3 pixel grid** — a pixel is
~7.7% wider than tall on the 7", ~6.4% on the 9".

**So px/mm is a PAIR, not a scalar**, and the error runs the wrong way: the *vertical* axis is
tighter, and height is what a touch target usually has least of. Now rule **H-406**, and
`Panels.PxPerMmV` is what the minor-axis check uses.

Textbook `target-differences.md` material: a plausible inference from a verified neighbouring panel,
written down confidently, and wrong.

### Sizing corroborated against real practice

Measured off the anchor's own screen (geometry only, no restricted content):

| class | px | on the 9" | vs the 9 mm floor |
|---|---|---|---|
| main buttons | 100 × 100 | 24.8 × 23.3 mm | well clear |
| nav buttons | 117 × 40 | 29.0 × **9.3 mm** | clears by 0.3 mm |
| switches | 64 × 22 | 15.8 × **5.1 mm** | 🔴 **violates, by 1.8×** |

Two of three classes corroborate the floor independently — one landing within 0.3 mm of it, which
suggests a real constraint rather than a borrowed number. The third violates it, and it is the
fiddliest control on the screen. *(On the 7" target that switch would be 4.2 mm.)*

---

## Lane A — Conventions ✅

**`docs/17-hmi-conventions.md`** — 31 rules, **26 mechanized, 5 advisory**, denominator stated in
the document.

Includes **H-107, the red-STOP carve-out**, scoped to STOP/E-STOP controls only and recorded with
its reasoning, plus H-401…H-408 from the sizing standard.

> The denominator was written "28 / 22 / 6" on first draft — **wrong in all three figures**, caught
> by counting the rules programmatically rather than re-reading them. That is the argument for
> stating a denominator at all, demonstrated on the document that states it, within the hour.

---

## Lane B — Toolchain ✅

**`src/hmi-cli/`** (net8.0). Not in `converter` (T1 shells to Chrome, breaking its in-process
invariant); not in `openness-cli` (net48, and every rebuild needs a TIA whitelist approval).

`flatten` · `lint` · `style-check` · `check` · `panels`. Exit codes: 0 clean · 1 findings ·
2 usage · **9 NOTHING EXAMINED**. `EXAMINED: n` prints on every run.

### Validated against the known baseline

Experiment 2's four arms, checked at their **design** resolution:

| arm | errors | off-canvas | recorded in research §3.10 |
|---|---|---|---|
| direct | 74 | **0** | 0 ✅ |
| tooled | 152 | **2** | 2 ✅ |
| rules-only | 47 | **1** | 1 ✅ |
| **hybrid** | **14** | **0** | 0 ✅ |

**Off-canvas reproduces the recorded scorecards exactly, and the arm ordering is preserved** — the
hybrid arm is best by a factor of 3.4 over the next. The regression baseline holds.

### And the canvas claim is now measured, not asserted

The same four screens checked against the **KTP700 target**:

| arm | off-canvas at 1280×800 | off-canvas at 800×480 |
|---|---|---|
| direct | 0 | **45** |
| tooled | 2 | **65** |
| rules-only | 1 | **48** |
| hybrid | 0 | **60** |

**60 of 84 items on the best arm fall outside a Basic panel.** `PLAN.md` §0 said experiment 2's
layout "does not transfer"; that was an argument from arithmetic and is now a measurement.

### One defect found and fixed by running it

H-205 forbids animation *except an unacknowledged alarm* — and the checker's first build had **no
carve-out**, making the tool stricter than the rule it cites. Now `data-hmi-alarm` declares the
exception; undeclared motion still fires, because "it's the alarm flash" is exactly what an
undeclared decorative animation would also claim.

---

## What wave 1 changed in the plan

| | |
|---|---|
| §7 "T6 already exists" | ❌ **wrong** — `export` was PLC-only; `--screen` had to be built |
| §0 Classic architecture | ✅ confirmed, and the file is richer than assumed |
| Unified probe P1 (layers) | ✅ **answered for Classic** by the format itself |
| Research open question 8 (groups) | ✅ **answered** — `<Hmi.Screen.Group>` exists |
| sizing-standard square pixels | ❌ **wrong** — corrected to a per-axis pair |
| Ledger | rows 10–15 added, **row 8 closed positively** |

## Open going into wave 2

1. ~~**The Classic gate is not settled.**~~ ✅ **SETTLED, AND FAVOURABLY — see the addendum.** The
   fault matrix shows the Classic gate is at least as strong as Unified's, with import as a second
   earlier gate Unified does not have. My "biggest open risk" ranking was wrong.
2. ~~**Import is unproven.**~~ ✅ **PROVEN** — `openness-cli import --screen` built and run; the
   round trip is element-for-element faithful.
3. **Physical-size checks on real content need the 7" panel's own project** — the anchor is the 9".
4. **`SoftKey` carries no geometry** and needs emitter handling as position-less.
5. **`hmi-cli` has no automated test project yet** — validated by running it against the four arms,
   which is evidence but not a regression harness.

6. 🔴 **THE THIN SPIKE WAS NEVER ACTUALLY RUN, and the wave-1 report should have said so.** Wave 1's
   Lane P was specified as *K1 → capability walk → thin spike*. K1 ran; `export --screen` and
   `import --screen` were built and run; the fault matrix exercised import and compile hard. But
   every one of those moved **TIA's own exports**. Nothing has yet gone **HTML → SimaticML**, because
   **T5 EMIT does not exist**. T1 (flatten) is proven and T5 is unwritten, so the two halves of the
   converter have never met. That is wave 2's first job, not a leftover.

## Added to wave 2 by the alarm-view tangent

The tangent was not a detour — it produced four requirements the emitter would otherwise have been
built without:

- **The emitter may only claim to produce what SimaticML can represent**, and must **hard-error** on
  anything else rather than emitting a screen that silently lacks it (ADR-0010's shape).
- **A visible placeholder rectangle** for the alarm view (owner's decision), plus its two guards: a
  **hand-off checklist** and a **regeneration guard** keyed on a content hash.
- **Never re-import a screen this tooling did not author** — now measured, not precautionary:
  `Override` replaces, and the round trip provably destroys unexportable content.
- **A pre-flight gate:** a screen whose export has a `ScreenLayer` but no `Hmi.Screen.*` item carries
  content the exporter cannot represent — refuse to modify it. Incomplete by construction, and the
  placeholder masks it, which is exactly why the regeneration guard is the load-bearing one.

---

# Addendum — the gate-equivalence test (owner question, same day)

**Question:** can we construct a test to see whether a Classic *device compile* catches the same
faults `hmi-compile` catches on Unified?

**Yes — fault injection with a control.** Take the exported screen, produce N copies each carrying
exactly one injected defect plus a distinct name/number, import them, compile once, and record
**where** each fault is caught: import, compile, or nowhere.

The **M0 control** is what makes it a test rather than a demonstration: an unmutated copy that must
import and compile clean. Without it, "every mutant passed" is indistinguishable from "nothing ever
imported" — the empty-is-not-clean failure, one level up.

## Results — measured live

| # | injected fault | caught at | what it said |
|---|---|---|---|
| **M0** | *none (control)* | — | imported, compiled clean ✅ **control valid** |
| M1 | button `Left = 99999` | **COMPILE** | `ZZTest_M1_offcanvas: Button_7: The specified value is invalid: '99999'` |
| M2 | button `Width = 0` | 🔴 **NOWHERE** | silent — not named in any message |
| M3 | binding to a non-existent tag | **COMPILE** | `ZZTest_M3_danglingtag: Circle_5: Animation 'Appearance' has invalid parameters. The tag does not exist.` |
| M5 | `CornerStyle = NotAStyle` | **IMPORT** | names value, attribute, element type, SimaticML ID, line **and column** |
| M6 | duplicate `ObjectName` | **IMPORT** | names the composition, the identifier, the ML ID and the line |

Compile exited **8**, `ERRORS: 9`. *(The CLI's own note fired usefully: the compiler self-reported
`ErrorCount=2` against 9 messages in the tree — counts here are not reliable, messages are.)*

## The verdict, and it reverses my own risk ranking

`wave-1-results.md` called the Classic gate "the biggest open risk". **That was wrong, and the test
says so.** The Classic gate is **at least as strong as Unified's, and in one respect stronger:**

- **Reference integrity** — dangling tag caught, located to screen *and* item. This is the class
  `hmi-compile` catches on Unified via faceplate type/parameter binding.
- **Value validity** — an out-of-range coordinate caught, with the offending value quoted.
- **Import is a SECOND, EARLIER gate that Unified does not have at all.** Bad enums and duplicate
  names never reach the compiler; they are refused at import with a line and column number. Unified
  has no file to reject, so it has no equivalent stage.

**And the one blind spot is shared, not Classic-specific.** M2's zero-width button passed silently —
exactly as Unified's `Validate()` accepts a zero-width screen (`openness-hmi-write-api.md` §4c). Both
families are blind to **degenerate geometry**, which is precisely the hole `hmi-cli`'s H-501/H-502/
H-505 fill. The tools were justified on a measured Unified gap; that justification now transfers to
Classic by measurement rather than by assumption.

## Round-trip fidelity — checked because the owner spotted something

The owner asked why the alarm banner appears missing on the imported screens. Measured:

🔴 **SUPERSEDED THE SAME DAY — read `target-differences.md` row 21 first.** The comparison below is
**self-consistent but not complete**: both sides came from the same exporter, so both are blind to
the same omissions. The owner's follow-up A/B proved the **alarm view does not export at all**.
What the round trip actually shows is that *what does export, exports stably* — not that the export
is complete.

**The round trip is faithful for exported content.** Export → import → re-export, element-for-element:

```
Button 6=6 · Circle 31=31 · GraphicView 9=9 · Group 5=5 · Line 30=30 · Property 2=2
Rectangle 30=30 · Screen 1=1 · ScreenLayer 1=1 · SoftKey 5=5 · Switch 2=2 · TextField 6=6
```

219,305 → 219,313 bytes (the delta is the longer screen name). **The `<Template>` link survives**:
`Template_1` on both sides.

🔴 **RETRACTED 2026-08-17 — the sentence that stood here was wrong, and wrong in an instructive way.**

It read: *"there is no alarm control on it at all. The banner belongs to Template_1."* The first
half is a fact about the EXPORT. The second half was an inference, and the owner has confirmed it is
false: **the alarm view is on the screen directly. The template does not have it.**

**How the reasoning failed:** the export of `Root screen` shows no alarm element, so I concluded the
banner must come from somewhere else. But **the export cannot show an alarm element under any
circumstances** — that is the very finding this section documents. I used an instrument I had just
proved blind as evidence of absence.

The correct reading of the same data: `Root screen`'s export is silent about alarm views because
every export is, so it says **nothing** about whether the screen has one. It very likely does — which
is exactly why the owner saw the banner missing on the imported copies.

🔴 **What is NOT established, and is now a tracked row:** whether TIA *resolves* that template link
for an imported screen the way it does for an original. "The link is in the XML" and "the editor
renders the template" are different claims, and this project has been burned by exactly that gap
before. **Open — see `target-differences.md` row 18.**
