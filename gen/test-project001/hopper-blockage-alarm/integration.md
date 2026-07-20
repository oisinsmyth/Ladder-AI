# Hopper-Blockage Alarm — Integration Record (S6 request #1)

Alarm-live integration of `FB_HopperBlockageMonitor` into the as-built control program (owner-approved,
2026-07-20). CALL + annunciation only; the stop/inhibit demand is deliberately left unconsumed for now
(owner scope call). S7 modify discipline: only new networks appended, every pre-existing network proven
byte-identical, compiled clean.

## What was wired

**`FC_ControlMain` (FC3) — appended NW8 + NW9** (after the existing NW7 Output Mapping, to keep NW1–7
byte-identical; functionally safe — this FB's outputs feed only `FC_AlarmsMain`, never NW7's buffer
output map, so scan position relative to NW7 is immaterial):
- NW8 "Hopper-Blockage Monitor Input Wiring" — three input COILs:
  - `iDB_HopperBlockageMonitor.IO.HopperLevelHigh` ← `DB_Input.Hopper_Level_High`
  - `iDB_HopperBlockageMonitor.IO.PlantRunning` ← `DB_Input.Shredder_Run_Fwd_FB OR DB_Input.Shredder_Run_Rev_FB`
  - `iDB_HopperBlockageMonitor.IO.FaultReset` ← `DB_Controls.FaultReset`
- NW9 "Call Hopper-Blockage Monitor" — `CALL FB_HopperBlockageMonitor(iDB_HopperBlockageMonitor, EN := TRUE)`
- **`HopperBlockStopReq` intentionally NOT wired** — alarm-only scope; the stop demand is a documented
  later step.

**`FC_AlarmsMain` (FC4) — appended NW10:**
- NW10 "Shredder Hopper - Blocked, Not Clearing - Reset Required" —
  `COIL DB_Alarms.ShredderAlarm0.%X9 := iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm`
- `%X9` verified free (X0–X8 in use; X9–X15 free). Alarm text follows C-505 (`<equipment> — <fault> —
  <action hint>`) and the block's existing per-network-title convention (C-501 slice-access exception).

## Gates & evidence

- **Tag grounding** (all `exists`, grep-verified in `ir/test-project001/`): `DB_Input.Hopper_Level_High`,
  `DB_Input.Shredder_Run_Fwd_FB`, `DB_Input.Shredder_Run_Rev_FB`, `DB_Controls.FaultReset`,
  `DB_Alarms.ShredderAlarm0` (X9 free), `iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm`. No invented tags.
- **Invariance:** `converter diff --only 8 9 FC_ControlMain(old→new)` → exit 0 (NW1–7 identical);
  `converter diff --only 10 FC_AlarmsMain(old→new)` → exit 0 (NW1–9 identical).
- **Preflight:** `FC_ControlMain` CLEAN; `FC_AlarmsMain` has one **pre-existing** C-201 (no block header
  comment — present before this edit, unrelated to NW10). Routed, not fixed drive-by (out of the
  named-network scope; does not block compile). *Routing note for a future conventions pass:*
  `FC_AlarmsMain` lacks a block header comment (C-201).
- **Compile (scratch `GenProject1`):** `FC_ControlMain (FC3)` — State Warning, **0 errors**, 1 warning;
  `FC_AlarmsMain (FC4)` — State Warning, **0 errors**, 1 warning. Both "Block was successfully compiled."
  The single warning each is the pre-existing device-level "inputs/outputs not in configured hardware"
  general warning, not attributable to these edits.

## Outcome

The hopper-blockage alarm is now live in the scan: `FC_ControlMain` drives the FB every cycle from the
buffered inputs, and `FC_AlarmsMain` annunciates `HopperBlockedAlarm` on `DB_Alarms.ShredderAlarm0.%X9`.
Remaining documented later step: consume `HopperBlockStopReq` (wire the stop/inhibit demand into the
plant stop logic) when the owner scopes it.
