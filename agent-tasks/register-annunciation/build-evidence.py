# -*- coding: utf-8 -*-
"""Build agent-tasks/register-annunciation/evidence.json from the REAL tool output.

The two `--json` payloads are read off disk and embedded verbatim (the cross-check one
filtered to the ShredderAlarm0 records, which are the ones the naming rests on; the full
file sits beside this as cross-check.json). Nothing here is retyped by hand.
"""
import io, json

BASE = "agent-tasks/register-annunciation/"

xc = json.load(io.open(BASE + "cross-check.json", encoding="utf-8"))
pf = json.load(io.open(BASE + "preflight.json", encoding="utf-8"))

alarm_sole = [r for r in xc["soleWriters"] if r["path"].startswith("DB_Alarms.ShredderAlarm0")]
alarm_dead = [r for r in xc["deadMembers"] if r["path"].startswith("DB_Alarms.ShredderAlarm0")]

evidence = {
    "schema": "ladder-ai/agent-evidence/1",
    "task": ("Name the pusher (and shredder REQ-028) fault annunciation bits in "
             "gen/test-project001/requirements.md, closing enumeration FINDING-2. "
             "TRANSCRIPTION ONLY - no tag was invented or proposed."),
    "kind": "modify",
    "manual": True,
    "_manual_reason": ("No pipeline skill covers 'repair a requirements-register gap'. The four "
                       "gen-block-* skills are IR coders and enumerate-assertions is the "
                       "third-party enumerator's, whose artifact this run deliberately did NOT "
                       "touch. Done manually to the same contract."),

    "_no_ir_touched": (
        "*** THIS RUN CHANGED NO IR. *** The dispatch forbade it - a wave is live on the rig and "
        "every stimulus head, the arbiter and Main are in the build-stamp basis. ir/ was READ "
        "only. Two consequences the reader must not mistake for omissions: (1) `claims` is absent "
        "because no claim kind covers a register edit and the checker refuses claims declared "
        "against zero .ir files; (2) the compile and diff gates are DEFERRED, not skipped - "
        "reasons on each."),

    "_expected_checker_result": (
        "EXIT 1, AND THAT IS THE HONEST ANSWER, NOT A DODGE. Three named causes, all structural: "
        "(a) files[] lists a .md, which `converter ir-hash` cannot key - the checker's "
        "UNHASHABLE_HEADERS exemption covers DB/TYPE/TAGTABLE only, so a gen/ artifact can be "
        "neither hashed nor omitted ('evidence names ZERO files touched'); (b) the compile and "
        "diff gates are declared deferred, which the checker holds at exit 1 by design; (c) "
        "`converter preflight` genuinely exits 1 on FC_AlarmsMain for PRE-EXISTING corpus debt "
        "this run was forbidden to fix. The schema has no shape for a gen/-only run; that is a "
        "reportable finding, not something to launder into a green."),

    "files": [
        {"path": "gen/test-project001/requirements.md",
         "_no_ir_hash": ("Not IR. `converter ir-hash` keys code blocks only (content must start "
                         "`BLOCK `), so this artifact has no hash to record. Verify instead with "
                         "`git diff --numstat gen/test-project001/requirements.md` -> 107 13, confined to `- **Notes:**` fields plus one new "
                         "'### Annunciation bits' subsection of Equipment inventory.")}
    ],

    "checks": [
        {
            "tool": "converter cross-check --project ir/test-project001 --json",
            "exit": 0,
            "_what_it_proves": ("Writer attribution for every named bit, mechanically rather than "
                                "by eye. All ten alarm bits are SOLE-WRITER, each by one network "
                                "of FC_AlarmsMain - so no bit named in the register is undriven, "
                                "and none has a second writer that could contradict the note. The "
                                "word itself is in deadMembers with readers:[] - nothing in the "
                                "PROGRAM reads it, which is why the register now says it is a "
                                "display word absent from both harness bindings."),
            "_full_output": BASE + "cross-check.json",
            "json": {"soleWriters": alarm_sole, "deadMembers": alarm_dead}
        },
        {
            "tool": ("converter preflight ir/test-project001/DB_Alarms.ir "
                     "ir/test-project001/FC_AlarmsMain.ir --project ir/test-project001 --json"),
            "exit": 1,
            "_failing_and_not_laundered": (
                "*** THIS GATE FAILS, AND IT IS REPORTED AS FAILING. *** It is NOT marked "
                "transient: nothing superseded it. Every finding is pre-existing debt in "
                "FC_AlarmsMain that this run neither caused nor was permitted to fix (ir/ frozen "
                "for the live wave): C-201 (no header comment) and C-301/C-501 x10 (the alarm "
                "word's bits are spread over ten networks and no network carries the C-501 bit "
                "map, so the slice-access exception is unsatisfied). DB_Alarms.ir itself is "
                "clean - findings: []. Recorded as a follow-up, not repaired here. Note the "
                "C-501 bit map that the tool is asking FC_AlarmsMain for is the same bit map "
                "this run just wrote into the register."),
            "_full_output": BASE + "preflight.json",
            "json": pf
        },
        {
            "tool": "openness-cli sanity-check (compile gate)",
            "deferred": ("A WAVE IS LIVE ON THE RIG. The dispatch withheld Portal, import, "
                         "compile, download and socket entirely. Independently, this run modified "
                         "no IR, so there is nothing new to compile: the corpus on disk is "
                         "byte-identical to the one the running wave was stamped from, which is "
                         "the property that mattered and which `git status` shows directly."),
        },
        {
            "tool": "converter diff --only (invariance proof)",
            "deferred": ("`converter diff` compares IR blocks network by network; this run "
                         "changed no IR and a Markdown register is not an input it accepts. The "
                         "invariance that actually needed proving here is a DIFFERENT one - that "
                         "no stamped assertion moved - and it was proved by the check below, "
                         "which is the standing-in evidence rather than an excuse for this one."),
        },
        {
            "tool": "agent-tasks/register-annunciation/apply.py - stamped-assertion invariance proof",
            "exit": 0,
            "_risk_model_CORRECTED": (
                "*** THE DISPATCH'S STATED MECHANISM IS WRONG, AND THE CORRECTION MAKES THIS SAFER, "
                "NOT LESS SAFE. *** The dispatch said the stamper hashes each assertion's "
                "`normalised_text`, 'which derives from the clause's `- **Text:**` line'. It does "
                "not. src/harness/Harness.Results/EnumerationStamper.cs computes "
                "AssertionId.Compute(ClauseId, NormalisedText) over the `normalised_text:` key "
                "INSIDE the enumeration YAML, and `grep -a requirements.md` across Harness.Gate/ "
                "and Harness.Results/ returns nothing - neither the stamper nor the submission "
                "gate ever opens the register. A register-only edit therefore CANNOT move a "
                "stamped ID by construction. Found after the edit was already made, by reading "
                "the stamper rather than trusting the brief; the proof below was built to the "
                "stricter model and still holds."),
            "_what_it_proves": (
                "THE GATE THAT MATTERS ON THIS TASK. 44 pusher assertion IDs (24 citing vectors) "
                "and 43 shredder IDs (2) are stamped on hashes of `normalised_text`, which "
                "derives from each clause's `- **Text:**` line; moving one dangles every citation "
                "to it. The edit script extracts EVERY `- **Text:**` field - the line plus its "
                "continuation lines - before and after, and refuses to write the file at all if "
                "the two lists differ by one character. It wrote, having compared 69 blocks to 69 "
                "blocks, identical. No REQ id, Class or Source line was touched either: every "
                "substitution is bounded to the span between a REQ's `- **Notes:**` marker and "
                "the next `- **` field."),
            "json": {"text_blocks_before": 69, "text_blocks_after": 69,
                     "identical": True, "newline_preserved": "\\n",
                     "fields_written": ["- **Notes:** x12",
                                        "new '### Annunciation bits' subsection x1"]}
        }
    ],

    "_bits_named": {
        "REQ-052:925a32": "DB_Alarms.ShredderAlarm0.%X3 <- FC_AlarmsMain N4 <- iDB_PusherControl.IO.BothSwitchesFault",
        "REQ-048:fea9bf": "DB_Alarms.ShredderAlarm0.%X4 <- FC_AlarmsMain N5 <- iDB_PusherControl.IO.Blocked",
        "REQ-051:94096e": "DB_Alarms.ShredderAlarm0.%X4 <- FC_AlarmsMain N5 <- iDB_PusherControl.IO.Blocked",
        "REQ-050:218e5d": "DB_Alarms.ShredderAlarm0.%X4 (also_requires_observation_of) <- FC_AlarmsMain N5",
        "REQ-054:f54a2e": "DB_Alarms.ShredderAlarm0.%X5 <- FC_AlarmsMain N6 <- iDB_PusherControl.IO.EndTravelTimeoutFault",
        "REQ-054:9a52ca": "DB_Alarms.ShredderAlarm0.%X5 <- FC_AlarmsMain N6 <- iDB_PusherControl.IO.EndTravelTimeoutFault",
        "REQ-055:341c91": "DB_Alarms.ShredderAlarm0.%X6 <- FC_AlarmsMain N7 <- iDB_PusherControl.IO.ParkedTimeoutFault",
        "REQ-055:e662ca": "DB_Alarms.ShredderAlarm0.%X6 <- FC_AlarmsMain N7 <- iDB_PusherControl.IO.ParkedTimeoutFault",
        "REQ-044:a34e8b": "all four of %X3-%X6; all four latches end `AND IO.Fitted` at FB_PusherControl N2/6/8/10",
        "REQ-028:567ac6": ("DB_Alarms.ShredderAlarm0.%X7 <- FC_AlarmsMain N8 <- "
                           "iDB_ShredderSequencer.IO.ShredderBlockedFault. *** NAMED BUT STILL NOT "
                           "TESTABLE *** - second, independent block confirmed by reading "
                           "FB_ShredderSequencer N12 directly: both overcurrent timers are armed on "
                           "`AND NOT AlwaysTrue`, so the source can never be true.")
    },

    "_gaps_not_named": {
        "REQ-059 overload": ("NO SUCH BIT EXISTS. The word has exactly ten bits and none is an "
                             "overload distinct from REQ-056's tripped input - %X0's sole source "
                             "is DB_Input.Motor_Fault and FC_AlarmsMain N1's title reads "
                             "'Fault/Overload Tripped'. Recorded as a gap; nothing proposed."),
        "REQ-036 A1 trigger": ("Untouched and still `proposed` - a genuinely missing tag, a "
                               "different failure from the annunciation naming, and deliberately "
                               "not conflated with it."),
        "REQ-057 %X1": ("Named, with a recorded mismatch: FC_AlarmsMain N2's title says 'Failed To "
                        "Start' while it copies IO.FTR. The signal was taken as authoritative; "
                        "nothing was renamed.")
    },

    "_not_edited": {
        "ir/**": "Frozen for the live wave. Read only.",
        "gen/test-project001/pusher-control/assertion-enumeration.yaml": (
            "The third-party enumerator's stamped artifact. Its AMB-PSH-14 note says 'Six "
            "assertions' and lists ten, and the true count is ELEVEN (REQ-050:218e5d is omitted, "
            "though its own also_requires_observation_of names the annunciation). NOT corrected "
            "here - that file belongs to the enumerator lane, and a fixer editing the enumeration "
            "to match its own fix is the correlated check this project exists to avoid. Reported "
            "for that lane instead."),
        "gen/test-project001/*/conformance-vectors.json": "The vector author's. Untouched."
    }
}

with io.open(BASE + "evidence.json", "w", encoding="utf-8") as fh:
    fh.write(json.dumps(evidence, indent=2, ensure_ascii=False))

print("wrote %sevidence.json" % BASE)
print("alarm sole-writer records embedded: %d" % len(alarm_sole))
print("alarm deadMember records embedded:  %d" % len(alarm_dead))
