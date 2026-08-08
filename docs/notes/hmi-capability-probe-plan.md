# Programme — HMI capability probes (live), phases and results

Modelled on `concurrent-portal-test-plan.md`: numbered phases, per-probe IDs, and **open questions
answered in place** as they resolve. Findings go to `openness-hmi-write-api.md`; **raw transcripts to
`docs/evidence/hmi-capability-probes.md`** (all six phases, append-only); this file is the
programme's own state.

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
| **P2** | Item-type breadth | 3 of 56 item types | **DONE 2026-08-09 — 35 create, 21 refuse; see §4k** |
| **P3** | Dynamization kinds | 1 of 6 | **DONE — only 3 of 6 creatable; §4l** |
| **P4** | Alarms (+ `MultilingualText`) | FI-35's own use case | **DONE — alarms create, TEXT REFUSED; §4l** |
| **P5** | Data plumbing (connections, logs) | never created | **DONE — create trivially, config is the work; §4l** |
| **P6** | Structure (groups, windows, plant views) | read-only so far | **DONE — works; `--in` found to be a no-op; §4l** |

**All six phases complete, 2026-08-09.** Device returned to zero probe artifacts and a clean compile
after every phase.

### What the programme changed

It was run so ADR-0007's decision would rest on measurement. It produced four findings that a
reflection map could not have, and three of them are **negative**:

1. **Deletion orphans silently.** Automatable only with a mandatory post-delete compile.
2. **Alarm text cannot be written.** `MultilingualTextItem.set_Text` throws. This is FI-35's own use
   case, and it is blocked on an unexplained refusal rather than on missing tooling.
3. **Half the dynamization vocabulary refuses**, including `Flashing` — which is how alarm state is
   shown. Compounds (2).
4. **`GetCreationInfos` overstates creatability by 21 of 56.** The creatable set is discoverable only
   by trial.

The positive result is that everything else worked: create/modify/bind/script/compile is proven end
to end, and **the compile is a genuine reference-integrity gate** — across P1–P6 it caught five
independent routes to a dangling reference, and in each case named the missing field precisely enough
to serve as a specification for a generator.

The honest summary for the ADR: *additively capable, destructively unsafe-by-default, and bounded by
a refusal set that only trial reveals — which happens to contain what alarm generation needs most.*

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
4. ~~**Do the five untested dynamization kinds resolve at all?**~~ — **ANSWERED 2026-08-09: only two
   do.** `Script` and `Expression` create; `Flashing`, `ResourceList` and `TagParameter` refuse with
   the same opaque error the item-type refusals give. **Half the dynamization vocabulary is
   unreachable** — including `Flashing`, which is how a real HMI shows an unacknowledged alarm (§4l).
6. ~~**Is `GetCreationInfos`' creatable list honest?**~~ — **ANSWERED 2026-08-09: no.** It reports 56
   creatable screen-item types; 35 create. The 21 refusals are the 17 `*Base` types (none marked
   `abstract`) plus `HmiLabel`, `HmiProcessControl` and the two custom containers, and every refusal
   gives the same opaque error. The creatable list must be established by trial and cached (§4k).
7. **Why do `HmiLabel` and `HmiProcessControl` refuse?** — UNKNOWN. The containers plausibly need
   the two-argument `Create<T>(name, containedTypeValue)`; these two have no such explanation.
   Worth one targeted probe with the second overload.
5. ~~**Can alarm text be written via `MultilingualText.Items.Find(language)`?**~~ — **ANSWERED
   2026-08-09: NO, not on this project.** `MultilingualTextItem.set_Text` threw even with a language
   item present. So alarm generation through Openness currently **cannot produce alarm text**, which
   is most of FI-35's value. Cause UNKNOWN — worth one targeted probe (read-only until a trigger tag
   exists? wrong language item? project editing language vs first language?).
8. **Why does `RaisedStateTagBitNumber` refuse on a fresh alarm?** — likely the same contextual
   writability as `HmiDataType` on a fresh tag (§4h): disabled until a trigger tag exists. If so,
   alarm creation has an ordering requirement the schema does not express.
9. **Can a screen be created inside a group?** — **not through the device-level composition.**
   `HmiScreenComposition.Create` takes a name only, so `--in` was a silent no-op (now warned about).
   It would need the composition resolved on the *group*. With screens also unable to move between
   groups, screen grouping is currently unreachable programmatically.

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
- **2026-08-09** — **P2 COMPLETE.** 56 item types attempted, 35 created, 21 refused; `GetCreationInfos`
  overstates by 60%. 34 of the 35 compile clean bare — the single error was a faceplate container
  with no type, i.e. the third independent route to a dangling reference and the third time the
  compile was the only detector. Screen deleted, device left clean with zero artifacts.
  **The contention guard earned its keep here**: the first P2 attempt was refused because the other
  session had resumed with two live-run `export` commands. Rather than block, an autonomous waiter
  polled for two consecutive quiet intervals and launched the sweep unattended when the machine
  freed up — the right shape for this programme, since Portal availability is the binding constraint
  and it is not predictable.
- **2026-08-09** — **P3–P6 COMPLETE**, run as one chained 30-probe sweep, again launched unattended
  by the quiet-poll waiter. Two negative results dominate: **only 3 of 6 dynamization kinds can be
  created**, and **alarm text cannot be written at all** (`MultilingualTextItem.set_Text` throws) —
  the second directly undercuts FI-35's alarm use case and is the most consequential single finding
  of the programme. Alarms/logs/connections all *create* trivially and are useless bare; the compile
  named exactly which fields were missing each time — the fourth and fifth independent routes to a
  dangling reference caught only by the compile. P6 also caught **a defect in my own tooling**:
  `--in` was silently discarded while the success message claimed the object had been grouped. Fixed
  to warn; recorded in §4l as a correction, since a report that echoes intent instead of outcome is
  worse than none. **Programme closed: all six phases done, device left with zero artifacts and
  `STATE: Success`.**
- **2026-08-09 — data-boundary near-miss, worth recording.** The raw transcripts **enumerate real
  restricted content** (311 discrete alarm names, equipment and screen names) because
  `hmi-inventory` and the read-backs list the whole device. Committing them verbatim to
  `docs/evidence/` would have breached `docs/13`'s JOB9002 rule — *example content in a committed doc
  must be genericized* — even though every object the probes **touched** was invented. **The
  transcripts of a probe are not as safe as the probe.** They are now filtered by a fail-closed
  whitelist, with elisions counted and labelled in the file. **The first version of that filter
  leaked two real tag names**: PowerShell's `-match` is case-insensitive, so an all-caps `WARNING`
  entry matched an alarm class named `Warning` and a bare `SET` matched real tags named `Settings*`.
  Rebuilt with `-cmatch` and anchored patterns, then verified by searching the output for known real
  equipment tokens. A whitelist matched case-insensitively, or on unanchored prefixes, is not
  fail-closed — it only looks it.
