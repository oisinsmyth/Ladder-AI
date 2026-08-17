# Wave 2 — results

**Executed 2026-08-17.** Plan: [`PLAN.md`](PLAN.md) §3. Everything below was run, not designed.

---

## Headline — the two halves of the converter met

**A screen authored as HTML is now in TIA, and it is provably what was written.**

```
home.html --T1 flatten--> screen-ir.json --T5 EMIT--> SimaticML --import--> TIA
                                                                    |
                                        export <--------------------+
                                          |
                    compare: 13 objects, 0 differences
```

| step | result |
|---|---|
| `check` (T2+T3, house rules) | **0 errors over 12 items** |
| `emit` (T5) | 13 SimaticML objects + 1 hand-off item |
| `import` | ✅ **exit 0** in 4.8 s |
| `hmi-compile` | ✅ **0 errors**, 39 warnings (all pre-existing project ones) |
| `export` (read-back) | ✅ exit 0 |
| **emitted vs read-back** | ✅ **13 objects compared, 0 differences** |

Full round trip: **22 seconds.**

Geometry survived exactly — every `Left/Top/Width/Height`, including a 752×1 line and a
752×150 placeholder:

```
Button_4    24,70  140x112  ->  24,70  140x112
Line_11     24,262 752x1    ->  24,262 752x1
Rectangle_1 0,0    800x40   ->  0,0    800x40
... 13 of 13 identical
```

**Wave 1's report had to admit the thin spike never ran and the two halves had never met. They have
now**, on a real 800×480 screen with three buttons, seven text fields, two rectangles and a line.

---

## Four bugs, and what each one taught

Every failure was mine. TIA's import diagnostics were excellent for three of them and useless for
the fourth — and that difference is the finding.

### 1. Truncated document — `XmlWriter` not flushed

`EmitResult` was constructed **inside** the `using` block, so `ToString()` ran before the writer
flushed. TIA: *"the following elements are not closed: AttributeList, Hmi.Screen.TextField … Line
539, position 28."* Precise, and fixed in a minute.

### 2. Wrong composition name — a Button has no `Text`

Copied `TextField`'s structure into `Button`. TIA: *"The 'Text' composition at line number 181 …
is not supported."*

**A Button carries `TextOff` and `TextOn`** — it is a two-state object even when used as a momentary
command. The anchor project's own baseline compile had been saying so all along
(*"Button … has no 'Off' text defined"*); the warning was legible before the bug existed.

### 3. Invalid enum values — three of them, none guessable

TIA: *"'Center' is not a supported value for 'VerticalAlignment' …"*. Rather than fix one per Portal
round trip, every enum value used in the real export was extracted at once:

| attribute | I guessed | actually |
|---|---|---|
| `VerticalAlignment` (Button) | `Center` | **`Middle`** — the vertical enum is Top/Middle/Bottom; `Center` exists only horizontally |
| `EndStyle` / `StartStyle` (Line) | `None` | **`NoEnd`** |
| `LineEndShapeStyle` (Line) | `None` | **`Round`** |

**The export is a corpus of known-valid values. Reading it wholesale beats being corrected one round
trip at a time.** Now recorded in `Emitter.cs`, with the two *deliberate* departures also written
down: Button `EdgeStyle` is `Solid` where the project uses `Style3D` (H-204 forbids 3-D), and
`BackFillStyle` is `Solid` so a command button shows its fill.

### 4. 🔴 A Line with relative coordinates CRASHES TIA PORTAL

**A new failure class, and the expensive one.** My line:

```
Left=40 Top=400 Width=400 Height=1   StartLeft=0 StartTop=0 EndLeft=400 EndTop=1
```

Start/End are **absolute screen coordinates**, not offsets — every line in the real export satisfies
`StartLeft == Left` and `EndLeft == Left + Width`. Emitting them relative puts the endpoints outside
the object's own bounding box.

**TIA does not reject that. It dies.** The error is
`EngineeringObjectDisposedException: Access to a disposed object of type 'Siemens.Engineering.Project'`
— which describes the aftermath and says nothing about the cause. Confirmed by watching the JOB9003
Portal PID change on each attempt (20888 → 18696 → …).

**Only findable by bisection**, at roughly one Portal session per hypothesis: `ZZ_Minimal` (one
rectangle) imported clean, then Text ✅ / Button ✅ / **Line 💥** isolated it.

> **The lesson for the emitter: the import gate is excellent at catching MALFORMED documents and
> useless at catching INCOHERENT ones.** A document can be schema-valid, enum-valid, and still
> describe an object whose parts contradict each other. That needs validating **before** anything
> reaches TIA — a crash costs a Portal session and yields no diagnostic.

Second entry in the "TIA crashes rather than errors" class, after `MappingTableEntrySimple`.

---

## Instrumentation — two mistakes, one root cause

Flagged by the owner mid-run, and both were the same error: **instrumenting outcomes instead of
progress.**

- A monitor with a 30-minute timeout, watching for a completion sentinel. It tells you something
  took too long; it cannot tell you *which* step, and it discards whatever the run had proved.
- A log written only *after* each step returned. A run sat for five minutes with a **zero-byte log**
  while healthy, and "slow first step" was indistinguishable from "wedged" without inspecting the
  process list.

Now: deadlines at **every** layer (`--timeout-connect 60`, `--timeout-open 180`, a `subprocess`
hard kill above them), timestamped `STARTED`/`exit=` lines per step, flushed incrementally, and the
monitor **streams** lines rather than waiting for a sentinel. A killed run now reports what it
established.

⚠️ Worth recording alongside this: the import that took **4.8 s** on a warm Portal was the same
operation that had appeared to hang for five minutes earlier. **7 Portal processes** were running,
4 of them invisible to Openness. Slowness here is contention, not cost.

---

## Still open

1. 🔴 **`docs/17-hmi-conventions.md` claims 26 mechanized rules; the checker implements about half.**
   I wrote "mechanized" meaning *can be*; the honest reading is *is*. That is exactly the
   `ReviewRunner.AllRuleIds` trap the document itself warns about. Fix by splitting the count into
   **implemented** vs **specified**, or by closing the gap.
2. **`hmi-cli` still has no automated test project.** Everything is verified by running it.
3. **T7 compare is manual** — the fidelity check above is a script, not a tool.
4. **Coherence validation** (bug 4's lesson) is not implemented.
5. **T8 preflight** — tag existence, duplicate names — not built.
6. **Scratch project** holds `ZZ_Minimal`, `ZZ_TText`, `ZZ_TButton`, `ZZ_DemoHome`, plus wave-1's
   `Screen_3`/`Screen_4` and a `Screen_1` master copy.
