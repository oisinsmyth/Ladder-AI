# Programme — HMI capability probes (live), phases and results

Modelled on `concurrent-portal-test-plan.md`: numbered phases, per-probe IDs, and **open questions
answered in place** as they resolve. Findings go to `openness-hmi-write-api.md`; raw transcripts to
`docs/evidence/`; this file is the programme's own state.

**What this is for:** `openness-hmi-write-api.md` §4i measured ~2% of 4549 declared HMI members
walked live, 0 of 184 deletions. Three of this project's most consequential HMI findings
(`Validate()` is useless, the compile *is* a gate, writability is contextual) came from *running* the
API rather than reading it. This programme walks more of it, so that ADR-0007's decision rests on
measurement.

**Scope and authorization:** `docs/13-data-boundary.md`, JOB9002 entry, the 2026-08-08 programme
extension. Everything created carries an invented `ZZ_AI_*` name, in JOB9002's scratch copy only;
deletion applies only to this programme's own artifacts; device-level `RuntimeSettings` are read-only.
`10-non-goals.md` is not amended — this stays a probe.

## Operating protocol (per probe, no exceptions)

1. **Check contention** — `Get-CimInstance Win32_Process -Filter "Name='openness-cli.exe'" | % { $_.CommandLine }`.
   Another session's client ⇒ **wait**. Never compete, never kill. *(Both mistakes were made once;
   see `openness-quirks.md`.)*
2. Run with the **worktree** binary — never stage into the shared `bin/Debug` path.
3. **Verify by read-back from a fresh process.** A command's own success report is not evidence.
4. **Compile.** Gate on `State`; count from the message tree, never the compiler's own
   `ErrorCount`/`WarningCount` (measured wrong in both directions).
5. **Clean up**, then compile again and require clean.
6. Record; commit with the evidence quoted.

**Stop conditions:** the device cannot be returned to a clean compile · a probe would touch a
pre-existing object · repeated `EngineeringSecurityException` on writes · the other session is active.

## Tooling built for this programme

| Command | Purpose |
|---|---|
| `hmi-inventory [--kind K]` | read-only census; calls out surviving `ZZ_AI_*` artifacts (the end-of-programme check) |
| `hmi-new --kind K --name N [--in P] --yes` | metamodel-driven create over any of the 80 creatable composition kinds |
| `hmi-delete --kind K --name N --yes` | metamodel-driven delete; **refuses names without the `ZZ_AI_` prefix** unless `--allow-any-name`; re-reads and reports `STILL PRESENT` if the object survived |

Rationale for one generic trio rather than ~80 wrappers, and the delete-path re-read, are in commit
`9da3592`.

## Phases

| # | Phase | Closes | Status |
|---|---|---|---|
| **P1** | Deletion lifecycle | 0 of 184 deletable types | **DONE 2026-08-08 — see §4j** |
| **P2** | Item-type breadth | 3 of 56 item types | not started |
| **P3** | Dynamization kinds | 1 of 6 | not started |
| **P4** | Alarms (+ `MultilingualText`) | FI-35's own use case | not started |
| **P5** | Data plumbing (connections, logs) | never created | not started |
| **P6** | Structure (groups, windows, plant views) | read-only so far | not started |

### P1 — deletion lifecycle

The largest single gap, and the prerequisite for cleaning up every later phase.

| Probe | What | Expected | Result |
|---|---|---|---|
| P1.0 | baseline `hmi-inventory` | census incl. 3 surviving artifacts | ✅ 3 found |
| P1.1 | delete a screen item | gone on read-back | ✅ confirmed absent |
| P1.2 | delete a dynamization | binding gone, item intact | ✅ |
| P1.3 | delete an event handler | handler gone, script with it | ✅ |
| P1.4 | delete a screen | screen count −1 | ✅ |
| P1.5 | delete the tag table | gone | ✅ |
| P1.6 | delete a nonexistent object | clean `CommandError` | ⚠️ correct message, **wrong exit code (5)** — defect, now fixed |
| P1.6a | delete a REAL object (guard test) | refused | ✅ refused `MainScreen` |
| **P1.7** | **delete a tag two live bindings reference** | cascade / orphan / refuse? | 🔴 **ORPHANS — silently** |
| P1.7b | compile after the orphaning | ? | ✅ **caught it — `STATE: Error`** |
| P1.9 | final inventory | zero artifacts | ✅ **0** |
| P1.10 | final compile | clean | ✅ **`STATE: Success`, 0 errors** |

**P1.7's answer: ORPHAN.** Deleting a tag with live bindings neither refuses nor cascades — the
bindings survive pointing at nothing, and only the compile notices. **A compile is therefore
mandatory after any delete**, not optional. Full write-up in `openness-hmi-write-api.md` §4j.

## Open questions — answered in place as they resolve

1. ~~**Does deletion cascade, orphan, or refuse when the object is still referenced?**~~ —
   **ANSWERED 2026-08-08: it ORPHANS, silently.** Bindings survive pointing at a deleted tag; only
   the compile catches it. A compile after any delete is mandatory (§4j).
2. ~~**Does `Delete()` need a `Save()` to persist?**~~ — **ANSWERED: no, it is immediate.** Every
   delete confirmed absent on re-read. Together with the *failed create* that persisted without a
   `Save()` (§4h), this says **Openness commits eagerly** in both directions.
3. ~~**Can a tag table be deleted while it still contains tags?**~~ — **ANSWERED: yes** (P1.5
   deleted `ZZ_AI_TestTags` after its tag was already gone; a fuller test with a populated table is
   worth doing when one exists).
4. **Do the five untested dynamization kinds resolve at all?** — open (P3).
5. **Can alarm text be written via `MultilingualText.Items.Find(language)`?** — open (P4). Known
   awkward: `Items` has no `Create`, and runtime languages cannot be added.

## Programme log

- **2026-08-08** — tooling built and committed (`9da3592`); `docs/13` extended for the programme;
  ADR-0007 drafted (Proposed, no recommendation); FI-54 raised; three reflection catalogues added to
  `docs/evidence/` by parallel tracks, lifting the L2 (member-detailed) coverage from ~39% toward
  ~95% with no Portal contact.
- **2026-08-08** — **P1.0 blocked.** Two `hmi-inventory` attempts hit the connect wedge (25 min and
  earlier). Cause confirmed as contention: a second Claude Code session is driving `openness-cli`
  against a live engineering job on the same machine, intermittently, all day. Per the protocol the
  correct response is to wait rather than compete — retrying in a loop would add load to live
  site work and would not succeed anyway.
- **2026-08-08** — **P1 COMPLETE**, run as one chained 13-probe sweep once the machine was quiet
  (chaining matters: the first attach is slow, subsequent ones reuse it — the whole sweep cost about
  what one probe would have cost separately). Headline: **deletion orphans**. Device left with zero
  probe artifacts and a clean compile. Two tooling gaps closed on the way: screen-scoped deletes
  (`--delete-item`/`--delete-bind`/`--delete-event`, since items hang off a screen not off
  `HmiSoftware`), and an exception-classification guard after P1 caught six unmapped exceptions
  exiting 5.
