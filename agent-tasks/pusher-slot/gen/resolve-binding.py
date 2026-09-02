"""Resolve the holes in the GENERATED pusher binding that are actually derivable, and leave the rest.

Run AFTER `converter harness-binding --emit`. It never invents a value: every field it writes is
either transcribed from a committed artifact (cited per field) or is a fact about this head that the
head's own emitted rungs state. Everything else stays in `unresolvedHoles`, which is what keeps the
document refused by gate 0b until a person who is allowed to answer has answered.
"""
import collections
import io
import json
import sys

path = sys.argv[1]
doc = json.load(io.open(path, encoding="utf-8"), object_pairs_hook=collections.OrderedDict)
slot = doc["slots"][0]

# ---- the copy layer's identity: TRANSCRIBED from the deployed artifacts, not chosen ----------------
ident = collections.OrderedDict()
ident["_blockIdentity"] = (
    "TRANSCRIBED, NOT CHOSEN. This slot joins the mirror that is already deployed rather than "
    "standing up a second one: ir/test-project001/FC_HarnessCopyLayer.ir declares `BLOCK FC "
    "FC_HarnessCopyLayer` and `NUMBER 9001`, and ir/test-project001/HarnessMirror.ir declares "
    "`TAGTABLE HarnessMirror` whose every tag is prefixed HX_. A SECOND copy layer is not an option "
    "and the reason is in the comms block: FB_Comms_ModbusServer serves ONE window, and converter "
    "served-area refuses a corpus holding two MB_SERVER calls because which one serves the mirror is "
    "then not derivable. So blockNumber 9001 is NOT claimed by this lane - it is the number the "
    "existing block already holds."
)
ident["blockName"] = "FC_HarnessCopyLayer"
ident["blockNumber"] = 9001
ident["tagTableName"] = "HarnessMirror"
ident["tagPrefix"] = "HX_"
ident["_baseByte"] = (
    "EMITTED BY THE SCAFFOLD from `converter served-area`, which reads it off "
    "ir/test-project001/FB_Comms_ModbusServer.ir NETWORK 1: `MB_HOLD_REG := P#M1000.0 WORD 1024`. "
    "Not narrowed here, and it must not be: the window on the wire is 1024 registers whatever this "
    "map allocates, and a binding declaring less would have the client and the server disagree about "
    "where the mirror ends."
)

out = collections.OrderedDict()
for key, value in doc.items():
    if key == "baseByte":
        out.update(ident)
    out[key] = value
doc = out

# ---- the start bool: a fact about this head, stated by the shell's own emitted rung ----------------
START = "iDB_PusherStim.Stim.Start"
before = len(slot["vectorTargets"])
slot["vectorTargets"] = [v for v in slot["vectorTargets"] if v.get("tag") != START]
assert len(slot["vectorTargets"]) == before - 1, "the start bool was not in vectorTargets"

resolved = collections.OrderedDict()
for key, value in slot.items():
    if key == "vectorTargets":
        resolved["_startCondition"] = (
            "DERIVED FROM THE EMITTED SHELL, not chosen: network 1 of FB_PusherStim reads "
            "`COIL Running := (Stim.Start OR Running) AND NOT Stim.ScenarioDone`, so this member and "
            "no other is what starts an index. It is REMOVED FROM vectorTargets above and appears "
            "only here: the copy layer refuses a generation in which one signal is both a vector "
            "target and the start condition, because the start bool is written by the control band "
            "and a vector register writing it too would be two writers of one line."
        )
        resolved["startCondition"] = START
    resolved[key] = value
slot = resolved
doc["slots"][0] = slot

# ---- register order: there is no prior order to transcribe, and that is checkable -----------------
slot["_registerOrder"] = (
    "RESOLVED, and the resolution is that there is nothing to transcribe. The hole exists so that a "
    "re-order cannot silently move signals to different registers on a redeploy; that risk needs a "
    "PREVIOUS deployment of this slot, and there is none - ir/test-project001/HarnessMirror.ir "
    "carries HX_HBA_* tags and not one HX_PSH_* tag, so no PSH register has ever been on the wire "
    "and no client mirror is written against an older layout. This scaffold's ordinal-by-tag order "
    "is therefore the FIRST order, not a re-ordering of one. Once it is deployed this annotation "
    "stops being true and the deployed order becomes the thing to transcribe."
)

# ---- what remains a hole, and why nothing here may answer it --------------------------------------
KEEP = {"specName", "encoding", "inertRest", "declaredBy", "retentiveBytes"}
kept = [h for h in doc["unresolvedHoles"] if h["field"] in KEEP]
assert len(kept) == len(KEEP), sorted(h["field"] for h in doc["unresolvedHoles"])
for hole in kept:
    hole["_notResolvedBy"] = {
        "specName": "NOT RESOLVED BY THE BLOCK AUTHOR, DELIBERATELY. It is the translation between "
                    "what a vector cites and what the copy layer carries, and the vector side is "
                    "being written in parallel by a third party this lane must not read. Filling it "
                    "from the IR member names would be this lane inventing the other party's "
                    "vocabulary and then agreeing with itself.",
        "encoding": "NOT RESOLVED. UDT_PusherStim states what Mode and ResetMode MEAN (0/1/2 in each "
                    "case, in the members' own comments), but an encoding is the mapping from the "
                    "SYMBOLS a vector cites to those numbers, and no symbol vocabulary exists on this "
                    "side. If the vectors cite the numbers themselves, `whenNumeric: Literal` with no "
                    "table is the answer - and that is still the coordinator's sentence, not this "
                    "lane's.",
        "inertRest": "NOT RESOLVED, AND NOT ESCAPED WITH assumedZeroRest EITHER. Every resting value "
                     "here would be a claim about what the block reads at rest, and nothing has "
                     "measured one: this slot has never run. The head is designed so that the answer "
                     "should be zero for all 30 registers - the tail cleardown runs INSIDE the index, "
                     "so the block is already verified inert before the start bool falls - but "
                     "'should be' is the reasoning, not the measurement, and the register that reads "
                     "0 because nothing ever wrote it is exactly the failure the inert check exists "
                     "to catch. Resolve it from the first wave's own idle sample, the way the hopper "
                     "slot's basis was.",
        "declaredBy": "NOT RESOLVED, AND THIS IS THE ONE FIELD THIS LANE MUST NOT FILL. Gate 5c "
                      "exists because a party who decides both what the block does and what can be "
                      "observed of it is the correlated reading the pipeline breaks. FB_PusherStim, "
                      "UDT_PusherStim, iDB_PusherStim and the regenerated Main were all written by "
                      "lad-coder, so lad-coder signing this document would make the gate compare a "
                      "party against itself. It needs the coordinator's own signature.",
        "retentiveBytes": "NOT RESOLVED, and the deployed hopper binding does not resolve it either "
                          "(its retentiveBytes is null). It is a property of the PROGRAM's retentive "
                          "%M extent, which no IR corpus states, and it feeds the map hash and "
                          "therefore the build stamp - so a guess here changes a stamp that is "
                          "supposed to identify a download.",
    }[hole["field"]]
doc["unresolvedHoles"] = kept

doc["_resolvedByLadCoder"] = (
    "Three of the scaffold's eight holes are resolved above and each is a TRANSCRIPTION or a "
    "DERIVATION, never a decision: the copy layer's identity (read off the two committed harness "
    "artifacts), the start condition (read off the emitted shell's own network 1, and removed from "
    "vectorTargets so it is not written twice), and the register order (there is no earlier order, "
    "which is checkable against HarnessMirror.ir). The five that remain are named above with the "
    "reason this lane is the wrong party to answer them. THE DOCUMENT IS THEREFORE STILL REFUSED BY "
    "GATE 0b, which is the correct state for it: the mechanism is built, the claims about the spec "
    "and the plant are not made."
)

io.open(path, "w", encoding="utf-8", newline="\n").write(json.dumps(doc, indent=2, ensure_ascii=False) + "\n")
print("resolved 3 hole(s); %d remain: %s" % (len(kept), ", ".join(h["field"] for h in kept)))
print("vectorTargets=%d resultSources=%d" % (len(slot["vectorTargets"]), len(slot["resultSources"])))
