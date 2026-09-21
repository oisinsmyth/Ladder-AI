# Builds the agent-evidence bundle for the three-slot dispatch. Sanitized copy of a real one.
#
# WHY THIS IS COMMITTED AT ALL. It is a worked example of `ladder-ai/agent-evidence/1` produced by an
# actual dispatch rather than written to illustrate the schema, and the difference shows in the one
# field that matters: the compile gate is DEFERRED and says so at length, because the dispatch was
# offline. An invented example would have had every gate green.
#
# SANITIZED, AND THE MAPPING IS NOT IN THIS REPOSITORY. Job code, project folder, work lane and the
# three slot FC names were substituted per the scheme in the gitignored `sanitization/` maps; the
# replacements are deliberately OBVIOUSLY INVENTED so nobody mistakes them for a real equipment
# list. Owner-approved, 2026-09-21. The paths below therefore point at nothing real.
#
# THE ROOT IS NO LONGER HARDCODED. The original carried an absolute path on a machine that no longer
# exists, which is how a script becomes a fossil. It is derived, with an env override.
import json, os, subprocess, sys

ROOT = os.environ.get("LADDER_AI_ROOT") or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CONV = os.path.join(ROOT, "src", "converter", "Converter", "bin", "Release", "net8.0", "converter.exe")

# The job lane this ran against. Pass a different one as argv[1]; the default is the sanitized
# original and exists nowhere.
LANE  = sys.argv[1] if len(sys.argv) > 1 else \
    "Live Runs/JOB9004/JOB9004 - Widget Line New Project/work-wgtlift"
IRDIR = LANE + "/import-ir"
MAIN  = IRDIR + "/Main.ir"
BEFORE= LANE + "/3slot-final/Main-before.ir"
STAGE = LANE + "/3slot-final"

def run(args):
    p = subprocess.Popen([CONV]+args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    o,e = p.communicate()
    return p.returncode, o.decode("utf-8","replace"), e.decode("utf-8","replace")

def runjson(args):
    rc,o,e = run(args)
    try: j = json.loads(o)
    except ValueError: j = {"_rawStdout": o, "_rawStderr": e}
    return rc, j

rc_pf, j_pf = runjson(["preflight", MAIN, "--project", IRDIR, "--json"])
rc_pfc, j_pfc = runjson(["preflight", BEFORE, "--project", IRDIR, "--json"])
rc_df, j_df = runjson(["diff", BEFORE, MAIN, "--only","4","5","6","7","--insert","5","--json"])
rc_ih, j_ih = runjson(["ir-hash", MAIN, "--json"])
rc_rv, o_rv, e_rv = run(["review", MAIN, "--project", IRDIR])
rc_tx, o_tx, e_tx = run(["to-xml", MAIN, "--project", IRDIR, "--out", STAGE+"/xml"])

print("preflight", rc_pf, "| control", rc_pfc, "| diff", rc_df, "| irhash", rc_ih, "| review", rc_rv, "| to-xml", rc_tx)
h = j_ih["hashes"][0]["hash"]
print("HASH", h)

AGENT = "<session-id>/lad-coder"
ev = {
  "schema": "ladder-ai/agent-evidence/1",
  "task": "OB Main: insert three conformance-slot CALL networks (FC_HarnessSharedWidgetSlot, FC_HarnessModelSlot, FC_HarnessPhaseSlot) as networks 5/6/7, ahead of the copy layer which moves 5 -> 8; repair the block comment's count and enumeration (five -> eight) and correct network 4's stale copy-layer claim. OFFLINE ONLY - no Portal, no import, no compile, no rig (the dispatcher holds the Portal lane).",
  "kind": "modify",
  "skill": "gen-block-modify-purpose",
  "files": [ { "path": MAIN, "ir_hash": h } ],
  "claims": [
    { "kind": "block-edit",    "value": "Main",   "agent": AGENT },
    { "kind": "block-network", "value": "Main:5", "agent": AGENT },
    { "kind": "block-network", "value": "Main:6", "agent": AGENT },
    { "kind": "block-network", "value": "Main:7", "agent": AGENT },
    { "kind": "block-network", "value": "Main:8", "agent": AGENT }
  ],
  "checks": [
    { "tool": "converter preflight", "exit": rc_pf, "json": j_pf },
    { "tool": "converter preflight (CONTROL, on the as-built pre-edit snapshot - establishes the clean result above is not inherited)", "exit": rc_pfc, "json": j_pfc },
    { "tool": "converter diff --only 4 5 6 7 --insert 5", "exit": rc_df, "json": j_df },
    { "tool": "converter review", "exit": rc_rv, "json": {"stdoutTail": o_rv.strip().splitlines()[-3:]} },
    { "tool": "converter to-xml (synthesizability pre-check for the deferred import; the whole block re-derives)", "exit": rc_tx, "json": {"stdout": o_tx.strip()} },
    { "tool": "openness-cli sanity-check (the compile gate)",
      "deferred": "The dispatch is OFFLINE ONLY and the dispatcher holds the Portal lane - no TIA Portal, no import, no compile, no rig was permitted on this run. Nothing here has been compile-gated; hard rule 4 is NOT satisfied and this IR must not be presented as finished until an import + sanity-check run reports INCONSISTENT: 0 on BOTH the BLOCKS: and TYPES: lines with errors=0. converter to-xml exit 0 above establishes only that the block is synthesizable, which is a precondition for that gate and not the gate." }
  ]
}
outdir = os.path.join(ROOT, "agent-tasks", "main-3slot-calls")
os.makedirs(outdir, exist_ok=True)
p = os.path.join(outdir, "evidence.json")
with open(p, "w", encoding="utf-8") as f:
    json.dump(ev, f, indent=2)
print("WROTE", p)
