# -*- coding: utf-8 -*-
"""Name the annunciation bits in gen/test-project001/requirements.md.

TRANSCRIPTION ONLY. Every name written here was read out of ir/test-project001/ and
confirmed by `converter cross-check` sole-writer attribution. Nothing is proposed.

Touches ONLY `- **Notes:**` fields, plus ONE new Equipment-inventory subsection.
Proves it by extracting every `- **Text:**` block before and after and refusing to
write if the list differs by a single character.
"""
import io, re, sys

PATH = "gen/test-project001/requirements.md"

with io.open(PATH, encoding="utf-8", newline="") as fh:
    src = fh.read()

NL = "\r\n" if "\r\n" in src else "\n"


def text_blocks(doc):
    """Every `- **Text:**` field, line and continuations, as one comparable list."""
    out, cur = [], None
    for line in doc.split(NL):
        if line.startswith("- **Text:**"):
            cur = [line]
        elif cur is not None:
            if line.startswith("- **") or line.startswith("#") or line.strip() == "":
                out.append(NL.join(cur))
                cur = None
            else:
                cur.append(line)
    if cur:
        out.append(NL.join(cur))
    return out


BEFORE = text_blocks(src)


def notes_span(doc, req):
    """(start, end) character offsets of REQ's `- **Notes:**` field."""
    h = doc.index("### %s " % req)
    nxt = doc.find(NL + "### ", h)
    if nxt == -1:
        nxt = len(doc)
    seg = doc[h:nxt]
    m = re.search(r"^- \*\*Notes:\*\*", seg, re.M)
    if not m:
        sys.exit("%s: no Notes field" % req)
    start = h + m.start()
    rest = doc[h + m.end():nxt]
    m2 = re.search(re.escape(NL) + r"- \*\*", rest)
    end = (h + m.end() + m2.start()) if m2 else nxt
    return start, end


def sub_in_notes(doc, req, old, new):
    """Replace `old` inside REQ's Notes field only.

    The anchor is matched WHITESPACE-TOLERANTLY: several of these notes wrap the
    sentence being replaced across a line break with a two-space continuation
    indent, and a literal match silently finds nothing there. Any run of
    whitespace in the anchor matches any run of whitespace in the file.
    """
    a, b = notes_span(doc, req)
    seg = doc[a:b]
    pat = re.compile(r"\s+".join(re.escape(p) for p in old.split()))
    hits = pat.findall(seg)
    if len(hits) != 1:
        sys.exit("%s: anchor matched %d times (want 1) -> %r" % (req, len(hits), old[:70]))
    return doc[:a] + pat.sub(lambda _m: new, seg, count=1) + doc[b:]


def W(*lines):
    return NL.join(lines)


doc = src

# ---------------------------------------------------------------- pusher fault clauses
doc = sub_in_notes(
    doc, "REQ-052",
    "An annunciation bit for this exists in the corpus alarm word (grep-verified).",
    W("**Annunciation bit: `DB_Alarms.ShredderAlarm0.%X3`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 4 - `COIL DB_Alarms.ShredderAlarm0.%X3 :=",
      "  iDB_PusherControl.IO.BothSwitchesFault`; source latch is `FB_PusherControl` NETWORK 2.",
      "  See Equipment inventory / Annunciation bits."))

doc = sub_in_notes(
    doc, "REQ-053",
    "Annunciation bit exists (grep-verified).",
    W("**Annunciation bit: `DB_Alarms.ShredderAlarm0.%X4`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 5 - `COIL DB_Alarms.ShredderAlarm0.%X4 :=",
      "  iDB_PusherControl.IO.Blocked`; source latch is `FB_PusherControl` NETWORK 6.",
      "  See Equipment inventory / Annunciation bits."))

doc = sub_in_notes(
    doc, "REQ-054",
    "Annunciation bit exists (grep-verified).",
    W("**Annunciation bit: `DB_Alarms.ShredderAlarm0.%X5`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 6 - `COIL DB_Alarms.ShredderAlarm0.%X5 :=",
      "  iDB_PusherControl.IO.EndTravelTimeoutFault`; source latch is `FB_PusherControl`",
      "  NETWORK 8. See Equipment inventory / Annunciation bits."))

doc = sub_in_notes(
    doc, "REQ-055",
    "Annunciation bit exists (grep-verified).",
    W("**Annunciation bit: `DB_Alarms.ShredderAlarm0.%X6`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 7 - `COIL DB_Alarms.ShredderAlarm0.%X6 :=",
      "  iDB_PusherControl.IO.ParkedTimeoutFault`; source latch is `FB_PusherControl`",
      "  NETWORK 10. See Equipment inventory / Annunciation bits."))

# ---------------------------------------- clauses whose RESPONSE signal is a fault bit
doc = sub_in_notes(
    doc, "REQ-048",
    "Annunciation is REQ-053.",
    W("Annunciation is REQ-053 - bit `DB_Alarms.ShredderAlarm0.%X4`, driven by",
      "  `FC_AlarmsMain` NETWORK 5 from `iDB_PusherControl.IO.Blocked`."))

doc = sub_in_notes(
    doc, "REQ-051", u"—",
    W("The trips this clause excludes are the ones that raise the pusher-blocked fault, whose",
      "  annunciation bit is `DB_Alarms.ShredderAlarm0.%X4` (`FC_AlarmsMain` NETWORK 5 from",
      "  `iDB_PusherControl.IO.Blocked`, latched at `FB_PusherControl` NETWORK 6). See",
      "  Equipment inventory / Annunciation bits."))

doc = sub_in_notes(
    doc, "REQ-044", u"—",
    W("The four pusher fault bits are `DB_Alarms.ShredderAlarm0.%X3`-`.%X6` - both-switches,",
      "  blocked, end-travel timeout, parked timeout respectively (Equipment inventory /",
      "  Annunciation bits). All four are gated **at source**, not at the annunciation:",
      "  `FB_PusherControl` NETWORKs 2, 6, 8 and 10 each end `... AND IO.Fitted`, so a disabled",
      "  pusher raises none of them and `FC_AlarmsMain` copies only already-gated bits."))

# ------------------------------------------------------------------------ shredder side
doc = sub_in_notes(
    doc, "REQ-058",
    "Annunciation bit exists (grep-verified).",
    W("**Annunciation bit: `DB_Alarms.ShredderAlarm0.%X7`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 8 - `COIL DB_Alarms.ShredderAlarm0.%X7 :=",
      "  iDB_ShredderSequencer.IO.ShredderBlockedFault`; source latch is",
      "  `FB_ShredderSequencer` NETWORK 5. **The bit is named but its source is unreachable as",
      "  built:** `FB_ShredderSequencer` NETWORK 12 arms both overcurrent timers on",
      "  `... AND NOT AlwaysTrue`, a permanently false placeholder, so `OvercurrentTripped`",
      "  never becomes true, step 60 is never entered, `ReversalCount` never increments and",
      "  `IO.ShredderBlockedFault` can never be true. Naming the bit closes the register gap;",
      "  it does **not** make this fault observable on a rig."))

doc = sub_in_notes(
    doc, "REQ-028", "The annunciation is REQ-058.",
    W("The annunciation is REQ-058 - bit `DB_Alarms.ShredderAlarm0.%X7`, driven by",
      "  `FC_AlarmsMain` NETWORK 8 from `iDB_ShredderSequencer.IO.ShredderBlockedFault`. See",
      "  REQ-058 for the standing unreachability of that source."))

# ------------------------------------------------------------ text-display motor faults
doc = sub_in_notes(
    doc, "REQ-056",
    "an annunciation bit exists (grep-verified)",
    W("**annunciation bit: `DB_Alarms.ShredderAlarm0.%X0`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 1 - `COIL DB_Alarms.ShredderAlarm0.%X0 :=",
      "  DB_Input.Motor_Fault`"))

doc = sub_in_notes(
    doc, "REQ-057",
    "A failed-to-run annunciation bit exists (grep-verified).",
    W("**Annunciation bit: `DB_Alarms.ShredderAlarm0.%X1`** (DB 5), driven by",
      "  `FC_AlarmsMain` (FC 4) NETWORK 2 - `COIL DB_Alarms.ShredderAlarm0.%X1 :=",
      "  iDB_MotorFwdRevSystem_Shredder.IO.FTR`. **Title/signal mismatch recorded, not",
      "  resolved:** that network's title reads \"Failed To Start\" while the signal it copies is",
      "  `IO.FTR` (fail-to-run); NETWORK 3's title reads \"Failed To Stop\" and copies `IO.FTS`.",
      "  The signal is what the logic does, so `%X1` is named here as the failed-to-run bit.",
      "  Which of the two is wrong is a corpus question for the alarm-design stage - nothing",
      "  was renamed."))

doc = sub_in_notes(
    doc, "REQ-059", "is Q-10.",
    W("is Q-10.",
      "  **Mechanically answered for the bit half (2026-09-02, `converter cross-check`): there",
      "  is NO distinct overload annunciation bit.** `DB_Alarms.ShredderAlarm0` carries exactly",
      "  ten bits (`%X0`-`%X9`, full map in Equipment inventory / Annunciation bits) and none is",
      "  an overload separate from REQ-056's tripped input: `%X0`'s sole source is",
      "  `DB_Input.Motor_Fault`, and `FC_AlarmsMain` NETWORK 1's own title reads",
      "  \"Fault/Overload Tripped\" - the corpus conflates the two into one bit. **This is a gap,",
      "  and no bit is proposed for it here.** Distinguishing them needs an owner ruling and, if",
      "  it is a separate condition, a tag the engineer creates."))

# ------------------------------------------- new Equipment-inventory subsection (FINDING-2)
ANCHOR = "### Operator commands (`DB_Controls`, HMI-written)"
SECTION = W(
    "### Annunciation bits (`DB_Alarms`, DB 5)",
    "",
    "Added 2026-09-02 to close the register gap recorded as FINDING-2 in the assertion",
    "enumerations: every fault clause said an annunciation bit \"exists (grep-verified)\" and",
    "none said what it was called, so a third-party enumerator wrote `response_signal:",
    "UNNAMED_IN_REGISTER` and a vector author could not cite one without inventing a name.",
    "**Every row below is transcribed from `ir/test-project001/`, not designed.**",
    "",
    "`DB_Alarms` holds **one member**, `ShredderAlarm0 : Word`. It carries **no named per-alarm",
    "members** - each alarm is a numbered bit slice, so the bit-slice path *is* the citable",
    "name. Every bit is written by exactly one network of `FC_AlarmsMain` (FC 4) as an",
    "unconditional single-`COIL` copy of its source, and `Main` calls `FC_AlarmsMain(EN :=",
    "TRUE)` unconditionally - so each bit equals its source on every scan after that call.",
    "`converter cross-check` reports all ten as sole writers.",
    "",
    "| Bit | Alarm | Driven by (`FC_AlarmsMain`) | Source signal | REQ |",
    "|---|---|---|---|---|",
    "| `%X0` | Shredder motor fault / overload tripped | NETWORK 1 | `DB_Input.Motor_Fault` | REQ-056 (and REQ-059 - the two are **not** separated, see there) |",
    "| `%X1` | Shredder motor failed to run | NETWORK 2 | `iDB_MotorFwdRevSystem_Shredder.IO.FTR` | REQ-057 (title/signal mismatch, see there) |",
    "| `%X2` | Shredder motor failed to stop | NETWORK 3 | `iDB_MotorFwdRevSystem_Shredder.IO.FTS` | no REQ in this register |",
    "| `%X3` | Pusher both switches active | NETWORK 4 | `iDB_PusherControl.IO.BothSwitchesFault` | REQ-052 |",
    "| `%X4` | Pusher blocked (pressure-trip count) | NETWORK 5 | `iDB_PusherControl.IO.Blocked` | REQ-053 (cause REQ-048) |",
    "| `%X5` | Pusher end-travel timeout | NETWORK 6 | `iDB_PusherControl.IO.EndTravelTimeoutFault` | REQ-054 |",
    "| `%X6` | Pusher parked timeout | NETWORK 7 | `iDB_PusherControl.IO.ParkedTimeoutFault` | REQ-055 |",
    "| `%X7` | Shredder blocked (reversal count) | NETWORK 8 | `iDB_ShredderSequencer.IO.ShredderBlockedFault` | REQ-058 (cause REQ-028) - **source unreachable as built** |",
    "| `%X8` | Discharge conveyor start timeout | NETWORK 9 | `iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault` | REQ-004 |",
    "| `%X9` | Shredder hopper blocked | NETWORK 10 | `iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm` | hopper-blockage sub-project |",
    "",
    "**Two caveats to read before citing any row.**",
    "",
    "1. **The alarm word is in neither harness binding, and nothing in the program reads it.**",
    "   `converter cross-check` lists `DB_Alarms.ShredderAlarm0` under `deadMembers` with",
    "   `readers: []` - it is a display word, consumed off the wire, not in ladder. What the",
    "   bindings *do* observe is the **source** signal of each pusher and shredder row:",
    "   `PSH_BothSwitchesFaultLatch`, `PSH_BlockedFaultLatch`, `PSH_EndTravelTimeoutFaultLatch`,",
    "   `PSH_ParkedTimeoutFaultLatch` and `SHR.ShredderBlockedFault`. Because each copy is",
    "   unconditional, observing the source is observing the bit one scan earlier - but that is",
    "   a design-for-testability judgement for the vector author to make and declare, not a",
    "   licence this table grants.",
    "2. **A named bit is not a reachable one.** `%X7`'s source can never be true as built",
    "   (REQ-058). Naming it removes the citation blocker; it does not remove the",
    "   unreachability blocker, and a vector asserting `%X7` is clear would pass vacuously.",
    "",
    "**No overload bit is listed, because none exists** - see REQ-059. Nothing here names a bit",
    "that is not in `FC_AlarmsMain`.",
    "",
    "")
if ANCHOR not in doc:
    sys.exit("inventory anchor not found")
doc = doc.replace(ANCHOR, SECTION + ANCHOR, 1)

# ------------------------------------------------------------------- INVARIANCE PROOF
AFTER = text_blocks(doc)
if BEFORE != AFTER:
    for a, b in zip(BEFORE, AFTER):
        if a != b:
            sys.exit("REFUSED: a **Text:** block moved.\n  was %r\n  now %r" % (a, b))
    sys.exit("REFUSED: **Text:** block COUNT changed %d -> %d" % (len(BEFORE), len(AFTER)))

with io.open(PATH, "w", encoding="utf-8", newline="") as fh:
    fh.write(doc)

print("WROTE %s" % PATH)
print("Text blocks before / after: %d / %d  IDENTICAL" % (len(BEFORE), len(AFTER)))
print("newline preserved: %r" % NL)
