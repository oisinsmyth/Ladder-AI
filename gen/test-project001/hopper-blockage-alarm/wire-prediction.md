# THE WIRE PREDICTION — `GenProject1` on the bench rig

**Written 2026-08-13, BEFORE any download.** Commit `92fa300`. This is the document to check the wire
against; an explanation assembled afterwards is worth much less.

Rig: `10.10.10.10:503`, unit 1. Currently running the JOB9004-scratch spike's **read-side generator**,
span **121 registers** (0–120), every register rewritten every scan.

> ## 🛑 STATUS: NOT COMPILE-GATED
> Import into `GenProject1` is refused by the auto-mode classifier — a permission gap, with the
> owner. **Everything below rests on preflight, conversion and arithmetic, never on TIA.** When the
> gap clears: `import-all` → `compile-all` → `sanity-check` (**keyed on errors, never on State**).

---

## 0. THE TWO THINGS MOST LIKELY TO BE MISDIAGNOSED AS A SPAN PROBLEM

Both are runtime-only. Neither is settled by any check run so far.

1. **Unit ID 1 is ASSUMED.** `MB_SERVER 5.3`'s CONNECT-structure branch has no unit-ID port; the
   filter is the instance's own `MB_UNIT_ID` static, left at its default. **If the connect succeeds
   but every request returns exception 2 — including test B — that is the cause, not the span.**
2. **`InterfaceId = 64` is UNVERIFIED.** The local PROFINET interface's hardware identifier, carried
   across from the spike's 1214C. *** IT IS A DB START VALUE, SO THE COMPILE IS NOT EVIDENCE FOR IT ***
   — a plain number the compiler never resolves. It surfaces as a non-zero
   `iDB_Comms_ModbusServer.MbServer.STATUS` and a socket that never accepts. **If `:503` refuses,
   look here first** — before the span, the map or the cable.

---

## 1. THE MIRROR — 35 registers, `%M1000`–`%M1069`

`MB_HOLD_REG = P#M1000.0 WORD 35`. Build stamp **`16#21D74D35`**. Map hash `f50e6b44…`.

| reg | %M | contents | type |
|---|---|---|---|
| 0–1 | `%MD1000` | **build stamp `16#21D74D35`** | `DWord` |
| 2–3 | `%MD1004` | free-running scan counter | `DInt` |
| 4 | `%M1009.0` | start bool | `Bool` |
| 5 | `%M1011.0` | start echo (latched) | `Bool` |
| 6 / 7 / 8 | `%MW1012/14/16` | `Profile` / `Precondition` / `ResetMode` | `Int` |
| 9, 11, 13, 15 | `%MD1018/22/26/30` | `P1 P2 P3 P4` | `Time` |
| 17, 19, 21, 23, 25 | `%MD1034/38/42/46/50` | `PreBoundaryHighRunning ArmAt ArmUntil EndAt ResetAt` | `Time` |
| 27 | `%M1055.0` | `HopperBlockedAlarm` | `Bool` |
| 28 | `%M1057.0` | `HopperBlockStopReq` | `Bool` |
| 29 | `%M1059.0` | `Armed` | `Bool` |
| 30 | `%M1061.0` | `ScenarioDone` | `Bool` |
| 31 | `%M1063.0` | `AlarmFellWithoutReset` | `Bool` |
| 32 | `%M1065.0` | `AlarmLowUnderHeldReset` | `Bool` |
| 33 | `%M1067.0` | `InhibitLowWhileAlarmHigh` | `Bool` |
| 34 | `%M1069.0` | `InhibitHighWhileAlarmLow` | `Bool` |

**Nine `Time` values occupy two registers each** and the odd registers between them are their second
halves. Register offsets are a running sum of widths, never a list index.

---

## 2. THE TESTS, IN THE ORDER TO RUN THEM

| # | request | old build (span 121) | **new build (span 35)** | proves |
|---|---|---|---|---|
| **A** | TCP connect `:503` | accepts | **accepts** | §0.2 — if this fails, `InterfaceId` |
| **B** | FC03 start 0, count 35 | succeeds | **succeeds** | nothing on its own |
| **C** | FC03 start 0, **count 36** | succeeds | 🔴 **EXCEPTION 2** | **the span is ours** |
| **D** | FC03 start **35**, count 1 | succeeds | 🔴 **EXCEPTION 2** | same, one round trip — **cheapest** |
| **E** | FC03 start 120, count 1 | succeeds | 🔴 **EXCEPTION 2** | corroborates C/D |
| **F** | FC03 start 0, count 2, **twice, ≥1 s apart** | **changes** | *** IDENTICAL, `= 16#21D74D35` *** | **this exact code is loaded** |
| **G** | FC03 start 2, count 2, twice | changes | **advances monotonically** | **it is scanning** |
| **H** | FC03 start 27, count 1, **with the alarm true** | n/a | **`1`** (or `256` — see §4) | **closes `BitAddressOf`** |

**C and D are the pair to key on.** Neither can be produced by a build whose span is 121. *All-zeros
is not evidence* — any silent failure produces it.

**F is the strongest single test and it is positive, not an absence.** The stamp is a constant
*generated into the code*, so it is present only if that exact code runs; the old build's registers
0–1 are a rolling generation counter and cannot sit still. **F proves identity, G proves liveness.**

---

## 3. WORD ORDER — DECODED BY F, IN ONE READ

The 32-bit transform is **inherited, not invented**, and **unverified until calibration**. The stamp
settles it outright because both halves are **known constants and distinguishable**:

| register 0 reads | order | then register 1 reads |
|---|---|---|
| `0x21D7` (8663) | **high word first** | `0x4D35` (19765) |
| `0x4D35` (19765) | **low word first** | `0x21D7` (8663) |

**One read. No timing, no second observation, no inference.** Keep the scan counter as the
cross-check (its low half moves fast), but the stamp is the instrument.

> *** DO NOT CALIBRATE AGAINST A DURATION. *** A `Time` cannot tell a correct transform from a broken
> one: every scenario duration is 1 000–120 000 ms, so a swap multiplies the low half by 65 536 and
> `P1 = 75 000 ms` arrives as about seven days. **In the command direction the failure is loud** —
> the scenario never reaches `P1`, `ScenarioDone` never latches, and the run times out rather than
> answering wrongly. Loud is safe; it is also useless for calibration.

---

## 4. TEST H — CLOSING `BitAddressOf`, AT NO COST

Device-owed item 4 is open with a damning entry: *the simulator and `BitAddressOf` agree **from the
same premise**, so their agreement is worth nothing.* A `Bool` sits at bit 0 of its own register, and
`MirrorGeometry.BitAddressOf` **infers** — never measured — that bit 0 lands in the register's
**second** byte (`%M1055.0` for register 27).

**With `HopperBlockedAlarm` true, read register 27 as a word:**

| reads | verdict |
|---|---|
| **`1`** | the inference is **right** |
| **`256`** | the two bytes **swap** — `BitAddressOf` is out by one byte, and every `Bool` in the mirror is at the wrong address |

**No extra step and no extra risk: the run produces this observation anyway.** It is the first
evidence about bit order this project will have that does not come from its own premise.

---

## 5. WHAT A GREEN HERE DOES *NOT* LICENSE

- **A, C, D, F, G, H together prove the server, the span, the identity, the liveness and the bit
  order.** They prove **nothing about the monitored block** — no vector has run.
- The mirror's data half is only meaningful once a scenario runs, which needs the import gate cleared
  and a client that writes the vector region.
- **`slotsInWaveSet` is 1, not 6.** All slots drive one monitor instance, one `DB_Input.Test` array
  and one `FaultReset`, so D9 separates every pair. Anything derived from 6 needs recomputing; the
  observability floor does not move (8.6 scans — it quantises on round trips, not slot count).
