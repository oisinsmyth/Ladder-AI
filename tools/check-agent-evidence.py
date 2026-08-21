"""Verify a sub-agent's hand-back without re-reading its work.

CLAUDE.md hard rule 8 requires the dispatching agent to verify the sub-agent's ACTUAL
diff and compile evidence - "a summary is not proof". Until now that meant re-reading
the work, which is the single largest source of the recheck-each-other cost the
2026-08-21 context cut set out to attack. Nearly every tool here already emits --json.
So the agent writes what the tools said, and this reads it.

    python tools/check-agent-evidence.py <path-to-evidence.json>

Exit 0 = every gate in the file passed and every hash still matches. Exit 1 = a gate
failed, a hash drifted, or a required gate is absent. Exit 2 = the file could not be
read or is not this schema, so NOTHING WAS VERIFIED.

WHAT THIS DELIBERATELY DOES NOT DO. It does not judge the logic, re-derive the diff, or
form an opinion on whether the change was a good idea. Those are the reviewer's job and
the engineer's, and an automated second opinion on them would be the correlated check
this project exists to avoid. This checks gates: exit codes, and content hashes it
recomputes itself. An agent can lie in its summary; it cannot lie about an ir-hash that
this script recomputes from the file on disk.

THREE RULES IT ENFORCES THAT ARE EASY TO GET WRONG:

  EMPTY IS NOT CLEAN. Across the mechanical floor, exit 1 = found something and exit
  2 = EXAMINED NOTHING - a --scope that matched nothing, an --fb with no instances. A
  green that compared nothing is not a pass, and evidence naming zero files touched is
  not a clean run.

  AN ABSENT GATE IS NOT A PASSED GATE. Evidence that simply omits the compile is not
  evidence of a compile. Required gates missing is exit 1, not a shrug.

  KEY ON ERRORS, NEVER ON STATE. The station compile scope surfaces standing hardware
  warnings, so a healthy project legitimately returns Warning with errors=0. Gating on
  state == Success marks a healthy project unhealthy forever.
"""
import io, json, os, subprocess, sys

ARGS = [a for a in sys.argv[1:] if not a.startswith("-")]
if len(ARGS) != 1:
    sys.stderr.write("usage: check-agent-evidence.py <path-to-evidence.json>\n")
    raise SystemExit(2)

EVIDENCE = ARGS[0]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONVERTER = os.path.join(
    ROOT, "src", "converter", "Converter", "bin", "Release", "net8.0", "converter.exe"
)

SCHEMA = "ladder-ai/agent-evidence/1"

# Exit codes that count as a pass, per tool. Anything else fails. Sourced from the
# tools' own help text and READMEs, not from memory:
#   converter diff --only : 1 = a network outside the named set changed
#   openness-cli compile  : 8 = errors, 11 = CompileIncomplete, 14 = nothing examined
#   the mechanical floor  : 1 = found something, 2 = EXAMINED NOTHING
PASS_EXIT = 0

# Gates that must be present. Omitting one is not a pass - see the docstring.
#
# The set depends on `kind`, because a NEW block has nothing to diff against: requiring
# `diff --only` of gen-block-new would make its evidence permanently unverifiable, and
# waiving it for everyone would drop the invariance proof that IS the S7 deliverable on a
# modification. `kind` is therefore REQUIRED - an unstated kind means the applicable gate
# set is unknown, and guessing it would either waive a real gate or invent one.
GATES_ALWAYS = [
    ("converter preflight", ["preflight"]),
    ("a compile gate (openness-cli compile or sanity-check)", ["compile", "sanity-check"]),
]
GATES_MODIFY = [
    ("converter diff --only (the invariance proof)", ["diff"]),
]
KINDS = ("new", "modify")

out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
problems = []


def fail(msg):
    problems.append(msg)


try:
    with io.open(EVIDENCE, encoding="utf-8") as handle:
        doc = json.load(handle)
except (IOError, OSError):
    sys.stderr.write("cannot read %s - NOTHING VERIFIED\n" % EVIDENCE)
    raise SystemExit(2)
except ValueError as exc:
    sys.stderr.write("%s is not valid JSON (%s) - NOTHING VERIFIED\n" % (EVIDENCE, exc))
    raise SystemExit(2)

if not isinstance(doc, dict) or doc.get("schema") != SCHEMA:
    sys.stderr.write(
        "%s is not %s - NOTHING VERIFIED\n" % (EVIDENCE, SCHEMA)
    )
    raise SystemExit(2)

kind = doc.get("kind")
if kind not in KINDS:
    sys.stderr.write(
        "%s has kind=%r; expected one of %s. The applicable gate set is therefore\n"
        "unknown and NOTHING WAS VERIFIED - a new block has no diff to prove invariance\n"
        "against, a modification must have one, and guessing between them would either\n"
        "waive a real gate or invent one.\n" % (EVIDENCE, doc.get("kind"), list(KINDS))
    )
    raise SystemExit(2)

files = doc.get("files") or []
checks = doc.get("checks") or []

# --- EMPTY IS NOT CLEAN -----------------------------------------------------------
if not files:
    fail("evidence names ZERO files touched - that is not a clean run, it is no run")
if not checks:
    fail("evidence carries ZERO checks - nothing was gated")

# --- an absent gate is not a passed gate -------------------------------------------
seen = " ".join(str(c.get("tool", "")) for c in checks).lower()
required = list(GATES_ALWAYS) + (list(GATES_MODIFY) if kind == "modify" else [])
for label, tokens in required:
    if not any(t in seen for t in tokens):
        fail("required gate absent: %s" % label)

# --- exit codes --------------------------------------------------------------------
for check in checks:
    tool = str(check.get("tool", "<unnamed>"))
    if "exit" not in check:
        fail("%s: no exit code recorded - an unrecorded exit is not a pass" % tool)
        continue
    code = check.get("exit")
    if code == 2:
        fail("%s: exit 2 = EXAMINED NOTHING. That is never a pass." % tool)
    elif code != PASS_EXIT:
        fail("%s: exit %s" % (tool, code))

    payload = check.get("json")
    if not isinstance(payload, dict):
        continue

    # Documented verbatim in src/openness-cli/README.md: a clean compile does not imply
    # the block is exportable, so --block/--type re-reads IsConsistent afterwards. The
    # field is emitted even when null so a consumer can tell "not asked" from
    # "not supported" - which means False is a real answer and must gate.
    if payload.get("consistentAfterCompile") is False:
        fail("%s: consistentAfterCompile=false - the block cannot be exported" % tool)

    # KEY ON ERRORS, NEVER ON STATE. Counters only; `state`/`Warning` are ignored on
    # purpose. NOT PINNED TO A CAPTURED PAYLOAD: no --json output from a live compile or
    # sanity-check has been captured yet (that needs Portal). Any integer counter whose
    # name mentions errors or inconsistency must be zero. Pin the exact field names the
    # first time a real payload is in hand, and delete this generic sweep.
    for key, value in payload.items():
        low = key.lower()
        if isinstance(value, bool) or not isinstance(value, int):
            continue
        if ("error" in low or "inconsistent" in low) and value != 0:
            fail("%s: %s = %d" % (tool, key, value))


# --- content hashes, recomputed here ------------------------------------------------
def ir_hash(path):
    if not os.path.exists(os.path.join(ROOT, path)):
        return None, "file does not exist"
    if not os.path.exists(CONVERTER):
        return None, "converter Release binary not built"
    # NOT check_output: ir-hash exits 1 on any error, and it still emits the JSON that
    # says WHY. Letting the non-zero exit raise would discard the tool's own reason and
    # replace it with a CalledProcessError - a correct refusal carrying a useless
    # message. Caught by this script's own test suite asserting the reason string.
    try:
        proc = subprocess.Popen(
            [CONVERTER, "ir-hash", path, "--json"],
            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        )
        raw, _ = proc.communicate()
    except OSError as exc:
        return None, "could not run ir-hash (%s)" % exc
    try:
        parsed = json.loads(raw.decode("utf-8", "replace"))
    except ValueError:
        return None, "ir-hash did not emit JSON"
    # Shape confirmed against the Release binary 2026-08-21:
    #   {"hashes": [{"file": ..., "hash": ... | null, "error": null | "..."}]}
    rows = parsed.get("hashes") if isinstance(parsed, dict) else None
    if not isinstance(rows, list) or not rows:
        return None, "ir-hash emitted no hashes[] entry"
    row = rows[0]
    if row.get("error"):
        # ir-hash keys CODE BLOCKS only - a DB, UDT or tag table reports an error and a
        # null hash. An evidence file claiming a hash for one of those is itself the
        # defect, so this surfaces rather than skips.
        return None, "ir-hash: %s" % row.get("error")
    digest = row.get("hash")
    if not digest:
        return None, "ir-hash returned no hash for this file"
    return digest, None


checked_hashes = 0
for entry in files:
    path = entry.get("path")
    claimed = entry.get("ir_hash")
    if not path:
        fail("a files[] entry has no path")
        continue
    if not claimed:
        fail("%s: no ir_hash recorded" % path)
        continue
    actual, err = ir_hash(path)
    if err:
        fail("%s: could not recompute ir-hash - %s" % (path, err))
        continue
    checked_hashes += 1
    if actual != claimed:
        fail("%s: ir-hash DRIFTED\n      recorded %s\n      on disk  %s"
             % (path, claimed, actual))

out.write("evidence                       : %s\n" % EVIDENCE)
out.write("task                           : %s\n" % doc.get("task", "<unnamed>"))
out.write("kind                           : %s\n" % kind)
out.write("skill                          : %s\n"
          % (doc.get("skill") or ("manual run" if doc.get("manual") else "<unstated>")))
out.write("files touched                  : %d\n" % len(files))
out.write("hashes recomputed here         : %d\n" % checked_hashes)
out.write("gates recorded                 : %d\n" % len(checks))
out.write("PROBLEMS                       : %d\n" % len(problems))
if problems:
    out.write("\n--- the hand-back does not verify ---\n")
    for p in problems:
        out.write("  %s\n" % p)
    out.write("\nDo not present this to the engineer as finished work.\n")
else:
    out.write("\nVERIFIED: every recorded gate passed and every hash still matches.\n")
    out.write("This says the gates ran and the files are what the agent said they were.\n")
    out.write("It says NOTHING about whether the logic is correct - that is the review.\n")
out.flush()

raise SystemExit(1 if problems else 0)
