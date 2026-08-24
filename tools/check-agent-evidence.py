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
  evidence of a compile. Required gates missing is exit 1, not a shrug. Applied to the
  claims gate below: a run that touched IR and declares no `claims` is refused, because
  "I took no reservation" and "I did not say" produce the same silence otherwise.

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

# THE SHARED CLAIMS STORE ROOT. The tool appends the project slug itself, so this is the
# ROOT and never the resolved path - passing `…\claims\test-project001` builds
# `…\claims\test-project001\test-project001`, a second empty store that grants every claim
# and looks exactly like success (ClaimStore.RejectDoubledRoot, measured 2026-08-14).
#
# 🔴 A PER-WORKTREE ROOT MAKES THIS WHOLE GATE A NO-OP. An empty store answers "no such
# claim" to every question, so pointing this somewhere private would turn every claim
# check into a refusal - which fails closed, and is the reason the default is the real
# shared path rather than anything relative to this checkout. LADDER_CLAIMS_DIR overrides
# it (the converter's own resolution order, and what the test suite drives), and the
# resolved root is PRINTED in the report so a reader can see which store answered.
CLAIMS_ROOT = os.environ.get("LADDER_CLAIMS_DIR") or r"C:\ProgramData\Ladder-AI\claims"

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

# --- the claims gate, joined to the files on disk -----------------------------------
#
# The registry (FI-65) works and, until Phase 8 track B, nothing required anyone to use
# it: `grep` for `converter claim` across .claude/ returned zero hits. The skills now call
# it; this is the half that recomputes rather than reads. An agent can write any `claims`
# array it likes - what it cannot do is make the SHARED STORE contain a claim it never
# took, or make the `NUMBER` line on disk agree with a block number it never claimed.
def store_claims(project_dir):
    """Every claim the shared store holds for one project. (rows, store_path, error)."""
    if not os.path.exists(CONVERTER):
        return None, None, "converter Release binary not built"
    try:
        proc = subprocess.Popen(
            [CONVERTER, "claims", "--project", project_dir, "--claims", CLAIMS_ROOT, "--json"],
            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        )
        raw, err = proc.communicate()
    except OSError as exc:
        return None, None, "could not run `converter claims` (%s)" % exc
    if proc.returncode != 0:
        # Exit 2 here is the doubled-root refusal or an unreadable project - either way
        # the store did not answer, and an unanswered store is NOT CHECKED, never clean.
        first = (err.decode("utf-8", "replace").strip().split("\n") or [""])[0]
        return None, None, "`converter claims` exited %d - %s" % (proc.returncode, first)
    try:
        parsed = json.loads(raw.decode("utf-8", "replace"))
    except ValueError:
        return None, None, "`converter claims` did not emit JSON"
    rows = parsed.get("claims") if isinstance(parsed, dict) else None
    if not isinstance(rows, list):
        return None, None, "`converter claims` emitted no claims[] array"
    return rows, parsed.get("store"), None


def ir_block_number(path):
    """'FB3' / 'DB9010' for an .ir file, or None when it declares no block number.

    The join that makes this gate worth having. An IR file names its space on its first
    line (`BLOCK FB <name>`, or `DB <name>` for a data block) and its number on a `NUMBER`
    line a couple of lines down - indented for a DB, flush for a code block, so both forms
    are stripped before splitting. A `TYPE` (UDT) has no number and returns None rather
    than a guess.
    """
    try:
        with io.open(os.path.join(ROOT, path), encoding="utf-8", errors="replace") as fh:
            head = [fh.readline() for _ in range(12)]
    except (IOError, OSError):
        return None
    space, number = None, None
    for raw in head:
        parts = raw.strip().split()
        if not parts:
            continue
        if space is None:
            if parts[0] == "TYPE":
                return None
            if parts[0] == "BLOCK" and len(parts) >= 2:
                space = parts[1]
            elif parts[0] == "DB":
                space = "DB"
        if parts[0] == "NUMBER" and len(parts) >= 2:
            number = parts[1]
    return None if (space is None or number is None) else space + number


ir_files = [e.get("path") for e in files
            if e.get("path") and str(e.get("path")).endswith(".ir")]
declared_claims = doc.get("claims")
claims_verified = 0
numbers_joined = 0
store_path = None
store_held = 0

if ir_files:
    if declared_claims is None:
        fail("required gate absent: no `claims` section, and this run touched IR. Every "
             "block number, network and block edit is reserved BEFORE the IR is written "
             "(CLAUDE.md; the gen-block-* skills) - an absent gate is not a passed gate")
    elif not isinstance(declared_claims, list) or not declared_claims:
        fail("`claims` is empty on a run that touched IR - EMPTY IS NOT CLEAN. A run that "
             "genuinely reserved nothing wrote no IR")
elif declared_claims:
    fail("`claims` declared but this evidence names no .ir file - nothing to join them to")

if ir_files and isinstance(declared_claims, list) and declared_claims:
    # One store query per project the evidence touched. The project is DERIVED from the
    # file paths, not declared: a declared project name is one more field an agent could
    # point at a store where its claims happen to live.
    projects = sorted({os.path.dirname(p).replace("\\", "/") for p in ir_files})
    held = []
    for project in projects:
        rows, resolved, err = store_claims(project)
        if err:
            fail("claims NOT CHECKED for %s - %s (store root %s). A store that did not "
                 "answer is not a store that said yes" % (project, err, CLAIMS_ROOT))
            continue
        store_path = resolved or store_path
        held.extend(rows)
    store_held = len(held)

    # THE VACUITY THAT WOULD MAKE THIS GATE A NO-OP, NAMED. Every branch below already
    # fails closed on a missing claim, so an empty store cannot produce a green - but it
    # would produce a wall of "not in the store" lines that read like an agent's mistake
    # rather than a misconfigured root. Say which it is.
    if store_held == 0 and not any("NOT CHECKED" in p for p in problems):
        fail("the claims store answered with ZERO claims for %s (root %s, store %s). An "
             "empty store grants everything and proves nothing - check the root is the "
             "SHARED one and not a per-worktree path" % (", ".join(projects), CLAIMS_ROOT,
                                                         store_path or "<none>"))

    by_key = {}
    for row in held:
        if isinstance(row, dict):
            by_key[(row.get("kind"), row.get("value"))] = row

    on_disk = {}
    for path in ir_files:
        number = ir_block_number(path)
        if number:
            on_disk.setdefault(number, path)

    for entry in declared_claims:
        if not isinstance(entry, dict):
            fail("a claims[] entry is not an object")
            continue
        # ckind, not kind: `kind` is the RUN's kind (new / modify) and selects the gate
        # set. Shadowing it here would silently swap one for the other below.
        ckind = entry.get("kind")
        value = entry.get("value")
        agent = entry.get("agent")
        if not (ckind and value and agent):
            fail("claims[] entry %r needs all of kind, value and agent - an unattributed "
                 "claim coordinates nothing (agent-tasks/README.md's own lesson)" % (entry,))
            continue

        row = by_key.get((ckind, value))
        if row is None:
            fail("claim %s '%s' is NOT IN THE STORE - the evidence says it was taken and "
                 "the registry has no record of it" % (ckind, value))
            continue
        holder = row.get("agent")
        if holder != agent:
            fail("claim %s '%s' is held by '%s', not by the declared '%s'"
                 % (ckind, value, holder, agent))
            continue
        claims_verified += 1

        # 🔴 THE JOIN. A block number exists first as the `NUMBER` line the agent typed;
        # this is the one part of the claims gate that an agent cannot satisfy by writing a
        # plausible evidence file, because it is checked against the IR on disk.
        if ckind == "block-number":
            if value in on_disk:
                numbers_joined += 1
            else:
                fail("claim block-number '%s' matches no NUMBER line in the .ir files this "
                     "evidence lists (on disk: %s) - the reservation and the work disagree"
                     % (value, ", ".join(sorted(on_disk)) or "none"))

    # The kind-specific floor, the same shape as the required-gate scan above. A new block
    # allocates a number; a modification takes exclusive hold of the block it edits. Both
    # are what the skills now do, so evidence showing neither did not follow them - and
    # without this, declaring one irrelevant claim would satisfy the whole gate.
    kinds_declared = {e.get("kind") for e in declared_claims if isinstance(e, dict)}
    needed = "block-number" if kind == "new" else "block-edit"
    if needed not in kinds_declared:
        fail("required claim absent: a %r run declares no %s claim (declared: %s)"
             % (kind, needed, ", ".join(sorted(k for k in kinds_declared if k)) or "none"))

out.write("evidence                       : %s\n" % EVIDENCE)
out.write("task                           : %s\n" % doc.get("task", "<unnamed>"))
out.write("kind                           : %s\n" % kind)
out.write("skill                          : %s\n"
          % (doc.get("skill") or ("manual run" if doc.get("manual") else "<unstated>")))
out.write("files touched                  : %d\n" % len(files))
out.write("hashes recomputed here         : %d\n" % checked_hashes)
out.write("gates recorded                 : %d\n" % len(checks))
# ASSERT THE DENOMINATOR. "0 problems" is true of nothing found and of nothing looked at,
# so the report says what the claims gate compared: how many the evidence declared, how
# many the SHARED store confirmed, how many block numbers were joined to a NUMBER line on
# disk, and - the line that exposes a wrong root - how many claims that store holds at all.
out.write("claims declared                : %s\n"
          % (len(declared_claims) if isinstance(declared_claims, list) else "ABSENT"))
out.write("claims confirmed in store      : %d\n" % claims_verified)
out.write("block numbers joined to IR     : %d\n" % numbers_joined)
out.write("claims store root              : %s\n" % CLAIMS_ROOT)
out.write("store consulted / holds        : %s / %d\n" % (store_path or "<none>", store_held))
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
