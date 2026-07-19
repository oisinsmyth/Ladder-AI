# Hopper-Blockage Alarm — Stage-4 Review Findings & Disposition

Permanent review record for S6 generation request #1/10 (`FB_HopperBlockageMonitor` +
`UDT_HopperBlockageIO` + `iDB_HopperBlockageMonitor`). Produced by the docs/15 **Check** stage —
fresh-context adversarial review, all three reviewer skills (`review-conventions`,
`review-functional`, `review-simplicity`) plus mechanical `converter review` (which returned 0
findings; C-406 confirmed clean, no TONR).

- **Reviewed:** 2026-07-20, blocks as at the Stage-3 compile-gate-passed state (3 new `.ir` files,
  no existing block edited; invariance trivially holds).
- **Owner disposition:** 2026-07-20 — present as-is with findings documented as known limitations;
  **no correctness fixes this run**.
- **Not an open task list.** This is the request's stable review record and a feed for a future
  `gen-block-modify-fix` run. Reviewer was blind to the architecture manifest's justifications by
  design; disposition below reconciles each finding against the Gate-1 decisions.

## Findings

Locations are in `ir/test-project001/`.

| ID | Sev | Rule / REQ | Location | Mechanism (one line) |
|---|---|---|---|---|
| **C-605** | error | C-605 | `UDT_HopperBlockageIO.ir:5–11` | All 7 interface-UDT members carry zero per-member comments; the two `Time` settings (`BlockedTimeThreshold`, `ClearDebounceTime`) also need units + C-307 scope. (FB's own extra Statics are commented — only the caller-facing UDT is bare.) |
| **F1** | medium | REQ-HBA-005 | `FB_HopperBlockageMonitor.ir` NW3 + NW5 | `FaultReset` while blockage still physically present is history-dependent: contiguous full-budget trip (`PT=ET=60`) → `Q` stays true → reset **defeated**; post-pause reduced-budget trip (`PT=ET=40`) → NW3 re-arms `RemainingTime=60 > ET` → `Q` drops, alarm clears then re-trips ~20 s later. Same physical state, different reset outcome by invisible timing history. |
| **F2** | medium | REQ-HBA-002 / -003 | `FB_HopperBlockageMonitor.ir` NW3 + NW4 | On the genuine-clear scan, `RemainingTime` is written by both NW3 (`MOVE` re-arm, on `NOT HighQualified`) and NW4 (`T_SUB`, on `PauseEdge`); NW4 is later so it wins → `RemainingTime = threshold − HeldET`. Next-scan re-arm normally cleans it, but is skipped if the hopper re-goes-high on the immediately following scan → fresh episode inherits a reduced budget and can alarm before a full 60 s accumulates. |
| **S1** | readability (warn) | C-101 / single-reading bar | `FB_HopperBlockageMonitor.ir` NW3 + NW4 | `RemainingTime` is a dual-writer state variable across two networks with order-dependent same-scan resolution + a next-scan cleanup — a skeptic cannot verify correctness in one reading. (Readability root of F2.) |
| **S2** | readability (warn) | C-101 / C-126 | `FB_HopperBlockageMonitor.ir` NW4 | NW4 packs 5 statements (TON + 2 coils + MOVE + `T_SUB`) — block complexity high-water mark. Mitigated: C-126 justifies co-locating timer+edge+consumers; C-406 forces the TONR-free construction; comment is thorough. |
| **C-124** | judgment (warn) | C-124 | `FB_HopperBlockageMonitor.ir` interface + `OB100.ir` | FB transient run-state (edge-memory `BlockActiveMem`, plus `RemainingTime`/`HeldET`/`HighQualified`/`BlockActive`) not force-reset in OB100, unlike corpus sibling FBs' edge-memory/counters. "Non-retentive self-clears, no OB100 edit" is the retentivity-reliance C-124's text forecloses. Functionally safe given non-retentivity + clean first-scan recompute. |
| **C-115** | judgment (warn) | C-115 | `UDT_HopperBlockageIO.ir` | UDT omits `enable`/`ready`/`running` handshake vocabulary; C-115 (as clarified) admits no paradigm-based exemption and the corpus sequencer carries it unwired. A pure supervisory monitor may legitimately be outside C-115's "equipment FB" scope. |
| **C-203** | trivial (info) | C-203 | `FB_HopperBlockageMonitor.ir:58` (NW5 title) | Title embeds the *why* ("RETAIN – Survives Power Cycle"); detail belongs in the comment (which already carries it). |

Per-REQ trace summary: REQ-HBA-001/004/006/007 **HELD**; 002/003 HELD **except** the F2 one-scan
gap; 005 HELD **with** the F1 caveat. The pause/resume cumulative timing that was the specifically-
requested scrutiny is **correct** for a genuine plant-stop pause (`HighQualified` stays true → NW3
does not re-arm → `T_SUB` banks `HeldET` → resume times the reduced budget); C-406-clean. No
gold-plating (the `HopperBlockStopReq` demand traces to Q-HBA-05).

## Disposition (owner, 2026-07-20)

- **F1, F2, S1, S2 — ACCEPTED AS KNOWN LIMITATIONS this run; not fixed.** Root cause: all four trace
  to the countdown-budget `RemainingTime` dual-writer (NW3 `MOVE` re-arm vs NW4 `T_SUB`), which
  existed **only** to dodge converter **Gap E** (tag-vs-tag comparison typing) — the reason NW3 was
  not built as the literal `up-accumulator + accumulator ≥ threshold` compare. **Gap E was fixed
  earlier the same day (commit `e7e4980`).** A future fix run can therefore rebuild NW3 as a clean
  `accumulator ≥ threshold` comparison and **dissolve F1/F2/S1 at the root** (removing the dual
  writer removes both the reset history-dependence and the re-arm/subtract race) rather than
  patching around them. S2's density also eases once the `T_SUB`/`HeldET` machinery is gone.
- **C-124, C-115 — reaffirmed Gate-1 decisions, left as-is by design.** C-124 → NEW-HBA-06
  (rely-on-non-retentivity for transient state, no OB100 edit). C-115 → NEW-HBA-07 (supervisory-
  monitor exemption from the handshake vocabulary). Recorded as accepted judgment calls, **not open
  defects**.
- **C-605 — RESOLVED (2026-07-20, comments added).** All 7 `UDT_HopperBlockageIO` members now carry
  one-line role comments; the two `Time` settings (`BlockedTimeThreshold`, `ClearDebounceTime`)
  additionally state units + C-307 per-instance-setting scope, and the two outputs
  (`HopperBlockedAlarm`, `HopperBlockStopReq`) note the RETAIN/survives-power-cycle intent (C-124).
  Comment-only diff — no member name/type/order/retentivity change; `converter preflight` CLEAN
  (0 findings); UDT re-imported and `compile --type` re-run to re-close the gate (evidence in
  `telemetry.log`).

## Pointer for a future `gen-block-modify-fix` run

**F1/F2 (+S1) are a candidate validation corpus for `gen-block-modify-fix`** — a real
reviewer-found bug set with a known clean fix (rebuild NW3 as a post-Gap-E `accumulator ≥ threshold`
comparison, dissolving the `RemainingTime` dual-writer). That makes this an unusually good
end-to-end exercise for the fix-wave stage: genuine defects, a single structural root cause, and a
now-unblocked converter path to the correct implementation.
