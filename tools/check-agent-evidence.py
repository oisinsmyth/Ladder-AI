"""Verify a sub-agent's hand-back without re-reading its work.

CLAUDE.md hard rule 8 requires the dispatching agent to verify the sub-agent's ACTUAL
diff and compile evidence - "a summary is not proof". Until now that meant re-reading
the work, which is the single largest source of the recheck-each-other cost the
2026-08-21 context cut set out to attack. Nearly every tool here already emits --json.
So the agent writes what the tools said, and this reads it.

    python tools/check-agent-evidence.py <path-to-evidence.json>

Exit 0 = every gate in the file passed and every hash still matches. Exit 1 = a gate
failed, a hash drifted, a required gate is absent, or a gate was DEFERRED. Exit 2 = the
file could not be read or is not this schema, so NOTHING WAS VERIFIED.

TWO FIELDS FOR THE TWO WAYS A GATE LEGITIMATELY DOES NOT READ AS A PASS:

  `"deferred": "<why>"` on a check - it did not run, and the run says so. A split run (the
  Portal-free half, which agent-tasks/README.md explicitly supports) could not produce a
  verifiable hand-back at all before this, and the only routes to green were to fabricate
  a compile entry or name a check "compile" with exit 0. **The exit stays 1.** A deferred
  gate is not a pass; the declaration only makes the report say WHICH and WHY rather than
  reading identically to a gate somebody quietly omitted. Carries no `exit`.

  `"transient": "<why>"` on a check - it ran, failed, and was superseded. The post-import
  dependent-block cascade exits 9 and clears on the recompile that follows. Reported as
  context, not gated. It does NOT satisfy a required gate, so the real passing one must
  still be there - which is what stops this relabelling a genuine failure.

Both require a reason. A deferral or a transient without one is just an omission with a
field name on it.

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
deferred = []   # (tool, why) - declared, not run. Keeps the exit NON-zero.
transient = []  # (tool, exit, why) - ran, failed en route, superseded. Context only.


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
# A DEFERRED check counts as present here (the gate is named and accounted for), so the
# report says "deferred" rather than the misleading "absent". A TRANSIENT one does NOT -
# a gate that ran and failed en route is not the gate; the real passing one must exist.
seen = " ".join(str(c.get("tool", "")) for c in checks
                if not c.get("transient")).lower()
required = list(GATES_ALWAYS) + (list(GATES_MODIFY) if kind == "modify" else [])
for label, tokens in required:
    if not any(t in seen for t in tokens):
        fail("required gate absent: %s" % label)

# --- exit codes --------------------------------------------------------------------
for check in checks:
    tool = str(check.get("tool", "<unnamed>"))

    # DECLARED DEFERRAL. A run may legitimately be split - agent-tasks/README.md says the
    # IR half is exclusive of nothing, so a Portal-free half is a supported shape. Before
    # this existed such a run could not produce a verifiable hand-back at all, and the only
    # routes to green were to fabricate a compile entry or name a check "compile" with
    # exit 0. Both launder an unrun gate into a passed one.
    #
    # THE EXIT STAYS NON-ZERO. A deferred gate is not a pass and this must never become a
    # way to get one. What changes is only that the report distinguishes
    # deferred-and-declared from silently-missing - two different facts that used to
    # produce the same output. The declaration must be THIS FIELD: writing "DEFERRED" into
    # `tool` declares nothing, and still fails below for having no exit code.
    if "deferred" in check:
        why = str(check.get("deferred") or "").strip()
        if not why:
            fail("%s: deferred with no reason - a deferral without one is just an omission"
                 % tool)
        elif "exit" in check:
            fail("%s: declared deferred AND carries an exit code - it either ran or it did "
                 "not" % tool)
        else:
            deferred.append((tool, why))
        continue

    if "exit" not in check:
        fail("%s: no exit code recorded - an unrecorded exit is not a pass" % tool)
        continue
    code = check.get("exit")

    # RAN AND FAILED EN ROUTE. The ordinary post-import dependent-block cascade exits 9 and
    # is cleared by the recompile that follows; both real runs hit it and had nowhere to put
    # it, so one used a free-form `notes` array that nothing reads. Recorded as context, not
    # as a gate - and because a transient check is excluded from the required-gate scan
    # above, the real passing gate must still be present. That is what stops this being a
    # way to relabel a genuine failure.
    if "transient" in check:
        why = str(check.get("transient") or "").strip()
        if not why:
            fail("%s: marked transient with no reason" % tool)
        else:
            transient.append((tool, code, why))
        continue

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
out.write("deferred (declared, not run)   : %d\n" % len(deferred))
out.write("transient (ran, superseded)    : %d\n" % len(transient))
out.write("PROBLEMS                       : %d\n" % len(problems))

if transient:
    out.write("\n--- ran and failed en route, superseded by a later passing gate ---\n")
    for tool, code, why in transient:
        out.write("  %s (exit %s)\n      %s\n" % (tool, code, why))

if deferred:
    out.write("\n--- DEFERRED, declared rather than missing ---\n")
    for tool, why in deferred:
        out.write("  %s\n      %s\n" % (tool, why))
    out.write("\nThis is NOT a pass. A deferred gate is one that did not run, and the run is\n")
    out.write("incomplete until it does. What the declaration buys is that this report names\n")
    out.write("WHICH gate and WHY, instead of reading identically to one quietly left out.\n")

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

raise SystemExit(1 if (problems or deferred) else 0)
