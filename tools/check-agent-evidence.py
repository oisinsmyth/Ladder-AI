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

WHEN IT RUNS, AND WHY THAT IS PART OF THE CONTRACT. At HAND-BACK, by the dispatcher,
BEFORE the claims are released - `agent-tasks/README.md`'s dispatcher procedure. The
claims half joins the evidence to LIVE reservations in the shared registry, so a run
whose sub-agent released on its way out cannot be verified at all: the store correctly
answers "no such claim" to every question and the report reads like a misconfigured
root. That is why the three gen-block-* skills hand back HOLDING their claims and the
dispatcher releases after this exits 0 (fixed 2026-08-24; before it, the skills released
first and this gate reded on every run that followed them exactly).
"""
import io, json, os, subprocess, sys

# --claims-root exists FOR THIS SCRIPT'S OWN TEST SUITE and for nothing else - see
# SHARED_CLAIMS_ROOT below. Parsed by hand rather than with argparse to keep the
# single-positional usage line unchanged.
ARGV = list(sys.argv[1:])
CLAIMS_ROOT_FLAG = None
POSITIONAL = []
_i = 0
while _i < len(ARGV):
    if ARGV[_i] == "--claims-root":
        if _i + 1 >= len(ARGV):
            sys.stderr.write("--claims-root needs a directory\n")
            raise SystemExit(2)
        CLAIMS_ROOT_FLAG = ARGV[_i + 1]
        _i += 2
        continue
    if not ARGV[_i].startswith("-"):
        POSITIONAL.append(ARGV[_i])
    _i += 1

if len(POSITIONAL) != 1:
    sys.stderr.write("usage: check-agent-evidence.py <path-to-evidence.json>\n")
    raise SystemExit(2)

EVIDENCE = POSITIONAL[0]

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
# 🔴 IT IS A CONSTANT, AND LADDER_CLAIMS_DIR DOES NOT MOVE IT (fixed 2026-08-24).
#
# This used to read `os.environ.get("LADDER_CLAIMS_DIR") or <the shared path>`, with a
# comment arguing that a private root "fails closed" because an empty store refuses
# everything. THAT ARGUMENT WAS WRONG, and the way it was wrong is the only way it could
# matter: `converter claim` reads THE SAME ENV VAR (Program.cs ResolveClaimsDir). One
# export therefore moved the writer and the reader TOGETHER. Measured 2026-08-24: two
# claims taken through the real CLI against a temp root, checker green, PROBLEMS 0, and
# not one byte of cross-agent coordination anywhere in it. A gate whose subject can
# relocate the evidence it is judged against is not a gate.
#
# So the checker resolves the store from a CONSTANT, and separately REFUSES a run whose
# environment points `converter claim` somewhere else - because that environment is one
# where the reservations were taken in a store no other agent can see.
SHARED_CLAIMS_ROOT = r"C:\ProgramData\Ladder-AI\claims"

# The one door, and it is bolted from the inside. `--claims-root` is honoured ONLY when the
# directory contains this sentinel file, which exists so that no ambient environment, no
# stray flag and no copy-pasted command can point the gate at a store that was never
# shared: the self-test creates it, a real store never has one. A run that uses it can
# never print the plain VERIFIED banner - the verdict says SELF-TEST STORE instead, so a
# green from it is not quotable as evidence that a reservation was taken.
SELFTEST_SENTINEL = "SELFTEST-CLAIMS-ROOT"

SELFTEST_STORE = False
CLAIMS_ROOT = SHARED_CLAIMS_ROOT
if CLAIMS_ROOT_FLAG is not None:
    if os.path.isfile(os.path.join(CLAIMS_ROOT_FLAG, SELFTEST_SENTINEL)):
        CLAIMS_ROOT, SELFTEST_STORE = CLAIMS_ROOT_FLAG, True
    else:
        sys.stderr.write(
            "--claims-root %s carries no %s sentinel file - REFUSED, NOTHING VERIFIED.\n"
            "This flag exists for tools/check-agent-evidence.tests.py and for nothing\n"
            "else: the claims gate is only worth running against the store every agent\n"
            "shares (%s). If you meant to point at a private store, you meant to skip\n"
            "the gate, and skipping it is not something this script will do quietly.\n"
            % (CLAIMS_ROOT_FLAG, SELFTEST_SENTINEL, SHARED_CLAIMS_ROOT))
        raise SystemExit(2)

# Reported always, gated only when the claims gate is actually in play (below). An agent
# working under this variable took its reservations in the store it names, so the shared
# registry legitimately has no record of them - which is a real coordination failure and
# not, as the old code had it, a green.
ENV_CLAIMS_DIR = os.environ.get("LADDER_CLAIMS_DIR") or None


def _same_dir(a, b):
    return os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(b))

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


def ir_identity(path):
    """(header, name, number) for an .ir file - 'BLOCK'/'DB'/'TYPE'/'TAGTABLE', its
    declared name, and 'FB3'/'DB9010' where one exists.

    The parse the whole claims join is built on. An IR file names itself on its first
    line - `BLOCK FB FB_PusherControl`, `DB DB_Alarms`, `TYPE UDT_Valve`,
    `TAGTABLE Default tag table` (whose name may contain spaces) - and a code block or DB
    carries its number on a `NUMBER` line a couple of lines down, indented for a DB and
    flush for a code block, so both forms are stripped before splitting. A TYPE or a
    TAGTABLE has no number and returns None for it rather than a guess.
    """
    try:
        with io.open(os.path.join(ROOT, path), encoding="utf-8", errors="replace") as fh:
            head = [fh.readline() for _ in range(12)]
    except (IOError, OSError):
        return None, None, None
    header, name, space, number = None, None, None, None
    for raw in head:
        parts = raw.strip().split()
        if not parts:
            continue
        if header is None:
            if parts[0] == "TAGTABLE":
                header, name = "TAGTABLE", raw.strip()[len("TAGTABLE"):].strip()
            elif parts[0] == "TYPE" and len(parts) >= 2:
                header, name = "TYPE", parts[1]
            elif parts[0] == "BLOCK" and len(parts) >= 3:
                header, space, name = "BLOCK", parts[1], parts[2]
            elif parts[0] == "DB" and len(parts) >= 2:
                header, space, name = "DB", "DB", parts[1]
            else:
                return None, None, None
            continue
        if parts[0] == "NUMBER" and len(parts) >= 2 and space is not None:
            number = space + parts[1]
    return header, name, number


# HEADERS THAT ir-hash CANNOT KEY. ir-hash keys code blocks only - `BLOCK ` - so a DB, a
# UDT or a tag table has no hash to record and `lad-coder.md` tells the agent to list it
# without one. Until 2026-08-24 the checker refused exactly that ("no ir_hash recorded"),
# while omitting the file instead broke the block-number join - both doors shut on every
# new FB, because gen-block-new claims its instance DB's number. The exemption is decided
# from the file's OWN HEADER, not from a field the evidence supplies, and it is checked in
# BOTH directions below: a file this says cannot hash and which then hashes is refused too.
UNHASHABLE_HEADERS = ("DB", "TYPE", "TAGTABLE")

checked_hashes = 0
not_hashable = []
for entry in files:
    path = entry.get("path")
    claimed = entry.get("ir_hash")
    if not path:
        fail("a files[] entry has no path")
        continue
    if not claimed:
        header, _, _ = ir_identity(path)
        if header not in UNHASHABLE_HEADERS:
            fail("%s: no ir_hash recorded" % path)
            continue
        # It says it is a DB/UDT/tag table. Confirm with ir-hash rather than take its word:
        # a file whose header lies about what it is would otherwise buy a free pass on the
        # one check here that an agent cannot fake.
        #
        # ⚠️ THIS BRANCH IS CURRENTLY UNREACHABLE, AND THAT IS SAID HERE RATHER THAN LEFT
        # FOR SOMEONE TO DISCOVER. ir-hash refuses anything whose content does not start
        # with `BLOCK `, and this branch is only entered when the header IS one of DB /
        # TYPE / TAGTABLE - no file can be both, so no fixture can drive it, and the
        # 2026-08-24 mutation campaign recorded it as the one surviving mutation rather
        # than manufacture a test that proved nothing. It is kept because the exemption
        # above is the only place in this script where "no hash" is accepted, and the day
        # ir-hash widens to key DBs, this is what stops that acceptance going silent.
        actual, err = ir_hash(path)
        if err is None:
            fail("%s: declares header %s (no hash expected) yet ir-hash returned one (%s) "
                 "- record it" % (path, header, actual))
        else:
            not_hashable.append((path, header))
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


def file_text(path):
    try:
        with io.open(os.path.join(ROOT, path), encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except (IOError, OSError):
        return ""


def word_in(needle, haystack):
    """`needle` present as a whole identifier - not as a substring of a longer name."""
    idx = 0
    while True:
        idx = haystack.find(needle, idx)
        if idx < 0:
            return False
        before = haystack[idx - 1] if idx else " "
        after = haystack[idx + len(needle):idx + len(needle) + 1] or " "
        if not (before.isalnum() or before == "_") and not (after.isalnum() or after == "_"):
            return True
        idx += len(needle)


# 🔴 CASE. `.endswith(".ir")` was case-SENSITIVE on paths that are not (fixed 2026-08-24).
# Measured with the extension typed `.IR`: the whole claims gate was skipped, `claims
# declared` printed ABSENT, PROBLEMS 0, VERIFIED, exit 0 - while ir_hash() found and hashed
# the very same file. One character of path case turned the gate off and the report said
# nothing. Windows paths are case-insensitive, so the comparison has to be too.
ir_files = [e.get("path") for e in files
            if e.get("path") and str(e.get("path")).lower().endswith(".ir")]
declared_claims = doc.get("claims")
claims_verified = 0
joined = 0
joinable = 0
unjoinable_kinds = {}
covered_files = 0
gated_files = 0
uncovered_note = []
store_path = None
store_held = 0

if ir_files and ENV_CLAIMS_DIR and not _same_dir(ENV_CLAIMS_DIR, CLAIMS_ROOT):
    fail("LADDER_CLAIMS_DIR is set to %s, which is not the shared registry (%s). Every "
         "`converter claim` run in this environment reserved its resources THERE, where "
         "no other agent can see them - so whatever the store below says, this run "
         "coordinated with nobody. Unset it and take the claims again."
         % (ENV_CLAIMS_DIR, CLAIMS_ROOT))

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
    # Deduplicated CASE-INSENSITIVELY, the same reason as the `.ir` test above: `ir/x` and
    # `IR/x` are one directory on Windows and one bucket in the store, so treating them as
    # two would query the same store twice and report double the claims it holds.
    #
    # The claim VALUES below are matched case-SENSITIVELY on purpose, and that is not an
    # inconsistency: ClaimStore.FileNameFor hashes the exact value, so `FB_Pusher` and
    # `fb_pusher` genuinely are two different reservations. Matching loosely here would
    # report a claim as held that the registry would hand to somebody else.
    seen_projects = {}
    for p in ir_files:
        folder = os.path.dirname(p).replace("\\", "/")
        seen_projects.setdefault(folder.lower(), folder)
    projects = sorted(seen_projects.values())
    held = []
    store_unreadable = False
    for project in projects:
        rows, resolved, err = store_claims(project)
        if err:
            store_unreadable = True
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
             "empty store grants everything and proves nothing. TWO CAUSES, AND THE FIRST "
             "IS THE LIKELY ONE: (1) the claims were RELEASED before hand-back - they must "
             "still be held when this runs, and the dispatcher releases them afterwards "
             "(agent-tasks/README.md); (2) the reservations were taken against a different "
             "root than %s" % (", ".join(projects), CLAIMS_ROOT, store_path or "<none>",
                               CLAIMS_ROOT))

    by_key = {}
    for row in held:
        if isinstance(row, dict):
            by_key[(row.get("kind"), row.get("value"))] = row

    # 🔴 THE WRONG STORE, WHICH IS NOT THE SAME AS AN EMPTY ONE (2026-09-01).
    #
    # `converter claims` names the store from the LEAF of --project, and --project here is
    # derived from the directory the evidence's own .ir files sit in. That is deliberate and
    # stays: a DECLARED project name would be one more field an agent could point at a store
    # where its claims happen to live. But it means a lane working in a scratch copy is
    # looked up under that copy's leaf name, not the project's.
    #
    # MEASURED: a lane staged into `work-runstate/ir/`. Leaf `ir` named the store
    # `…\claims\ir` - a DIFFERENT project's store, left over from an earlier generation, and
    # very much not empty. All five of its correctly-held claims were reported NOT IN THE
    # STORE. Renaming the scratch dir to `ir-new/` fixed it. CLAUDE.md warns about exactly
    # this collision for `converter claim`; nothing warned about it here.
    #
    # The vacuity guard above cannot catch it, because it keys on the store being EMPTY and
    # this store answered confidently about somebody else's reservations. So this is the
    # other half: a store that held claims, none of which are the ones declared, is far more
    # likely to be the wrong store than a registry that lost an entire lane's work.
    #
    # It changes no verdict - every claim below still fails closed - it changes the DIAGNOSIS,
    # because the failure mode this replaces is a wall of alarming lines that read as "the
    # registry lost your claims" when the truth is "you asked the wrong registry".
    declared_pairs = [(e.get("kind"), e.get("value")) for e in declared_claims
                      if isinstance(e, dict) and e.get("kind") and e.get("value")]
    matched_pairs = [p for p in declared_pairs if p in by_key]
    if declared_pairs and not matched_pairs and store_held > 0:
        fail("WRONG STORE, ALMOST CERTAINLY - the store answered with %d claim(s) and NOT "
             "ONE of the %d declared here is among them. A registry that lost one lane's "
             "entire reservation set while keeping everyone else's is far less likely than a "
             "lookup pointed at the wrong project. *** THE STORE IS NAMED FROM THE LEAF OF "
             "THE DIRECTORY THE .ir FILES SIT IN, *** which this evidence gives as %s, "
             "resolving to %s. A lane staging into a scratch copy whose leaf differs from the "
             "project's - `work-x/ir/` under a project whose IR directory is `ir-new/` - is "
             "looked up in a different project's store, and that store can be non-empty and "
             "answer with total confidence about claims that are not yours. Check the leaf, "
             "then re-run. The per-claim lines below are reported for completeness and should "
             "NOT be read as missing reservations until the store is confirmed."
             % (store_held, len(declared_pairs), ", ".join(projects),
                store_path or "<none>"))

    # THE FILES ON DISK, INDEXED BOTH WAYS the claim vocabulary can name them: by block
    # number (`FB3`, `DB9010`) and by declared name (`FB_PusherControl`, `UDT_Valve`,
    # `Default tag table`). Every join below resolves a claim to ONE of these entries, and
    # the same index drives the coverage pass in the other direction.
    by_number, by_name, identity = {}, {}, {}
    for path in ir_files:
        header, name, number = ir_identity(path)
        identity[path] = (header, name, number)
        if number:
            by_number.setdefault(number, path)
        if name:
            by_name.setdefault(name, path)

    def owner_of(ckind, value):
        """(the file this claim names, the file-identifying part of its value), or (None, x).

        The claim vocabulary is `ClaimKind` in src/converter/Converter/Claims/ClaimsModel.cs.
        Five of its six kinds name something the evidence's own file list can be checked
        against; `tag` is the one that cannot - a tag is created by the ENGINEER (hard rule
        3) in a table this pipeline does not write, so a tag reservation has no edited file
        to join to and is reported by name rather than pretended over.
        """
        if ckind == "block-number":
            return by_number.get(value), value
        if ckind == "block-edit":
            return by_name.get(value), value
        if ckind == "block-network":          # "FC_ControlMain:8"
            block = value.split(":", 1)[0]
            return by_name.get(block), block
        if ckind in ("db-member", "alarm-bit"):   # "DB_X.Member" / "DB_X.Member.%X9"
            block = value.split(".", 1)[0]
            return by_name.get(block), block
        return None, value

    JOINABLE_KINDS = ("block-number", "block-edit", "block-network", "db-member", "alarm-bit")

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
            # 🔴 "I COULD NOT LOOK" IS NOT "I LOOKED AND IT IS ABSENT" (2026-08-24). When no
            # store answered, this loop used to emit one NOT-IN-THE-STORE line per declared
            # claim - a specific, alarming accusation that the registry had lost them.
            #
            # Measured on a real run: a lane recorded its file paths RELATIVE ("ir-new/X.ir"),
            # the project resolved against this script's own cwd instead of the evidence
            # file's, `converter claims` exited 2 - directory not found, and the checker
            # printed ONE honest "claims NOT CHECKED" line followed by FORTY-EIGHT lines
            # saying the claims did not exist. They existed: 48 files in the store carried
            # that agent id. The dispatcher (me) nearly released work on the strength of it.
            #
            # This is the project's own empty-is-not-clean rule, inverted: exit 2 means
            # EXAMINED NOTHING, and a check that turns "examined nothing" into a negative
            # finding is worse than one that stays silent, because it is believed.
            if store_unreadable:
                fail("claim %s '%s' NOT CHECKED - no claims store answered for this "
                     "evidence, so this says nothing about whether the claim is held. Fix "
                     "the store lookup (see the NOT CHECKED line above) and re-run; do NOT "
                     "read this as a missing claim" % (ckind, value))
            else:
                fail("claim %s '%s' is NOT IN THE STORE %s - the evidence says it was "
                     "taken and that store has no record of it. The store is named from "
                     "the LEAF of the directory the .ir files sit in (%s), so confirm the "
                     "lookup found the right project before reading this as a missing "
                     "reservation"
                     % (ckind, value, store_path or "<none>", ", ".join(projects)))
            continue
        holder = row.get("agent")
        if holder != agent:
            fail("claim %s '%s' is held by '%s', not by the declared '%s'"
                 % (ckind, value, holder, agent))
            continue
        claims_verified += 1

        # 🔴 THE JOIN, AND IT IS NOW REACHABLE ON A MODIFY (fixed 2026-08-24). This used to
        # read `if ckind == "block-number"`, so `block-edit` - the kind the modify floor
        # REQUIRES - was joined to nothing at all, and neither were alarm-bit, db-member,
        # block-network or tag. Measured: evidence declaring `block-edit FB_PusherControl`
        # while the only edited file was FC_AlarmsMain.ir returned PROBLEMS 0, VERIFIED. On
        # every modify run the counter that was supposed to prove the join had happened -
        # "block numbers joined to IR : 0" - was a green BY CONSTRUCTION.
        #
        # So every kind that names something checkable is checked, and the one that cannot
        # be is named in the report instead of passing silently.
        if ckind not in JOINABLE_KINDS:
            unjoinable_kinds[ckind] = unjoinable_kinds.get(ckind, 0) + 1
            continue

        joinable += 1
        owner, part = owner_of(ckind, value)
        if owner is None:
            fail("claim %s '%s' names %r, which is none of the .ir files this evidence "
                 "lists (numbers on disk: %s; names on disk: %s) - the reservation and the "
                 "work disagree"
                 % (ckind, value, part, ", ".join(sorted(by_number)) or "none",
                    ", ".join(sorted(by_name)) or "none"))
            continue

        # The second half of the join, where the kind names something INSIDE the file. A
        # network reserved on a block must exist in that block; a DB member or alarm bit
        # reserved in a DB must appear in that DB. Without this, `block-network FC_X:8`
        # would be satisfied by any edit at all to FC_X.
        detail = None
        if ckind == "block-network":
            _, _, network = value.partition(":")
            if network.strip().isdigit():
                if not any(line.strip().split()[:2] == ["NETWORK", network.strip()]
                           for line in file_text(owner).splitlines() if line.strip()):
                    detail = "no `NETWORK %s` line in %s" % (network.strip(), owner)
        elif ckind in ("db-member", "alarm-bit"):
            segments = [s for s in value.split(".")[1:] if not s.startswith("%")]
            member = segments[-1] if segments else None
            if member and not word_in(member, file_text(owner)):
                detail = "member %r appears nowhere in %s" % (member, owner)
        if detail:
            fail("claim %s '%s' resolves to %s but %s - the reservation and the work "
                 "disagree" % (ckind, value, owner, detail))
            continue
        joined += 1

    # THE OTHER DIRECTION: every edited block must be covered by a claim. One claim used to
    # cover any number of edited blocks; a run could reserve one block and edit five.
    #
    # Gated only for files that declare a NUMBER (a code block or a DB), because those are
    # the ones a claim can always name - `block-number` before they exist, `block-edit`
    # once they do. A UDT or a tag table has no number, and a NEW one cannot be claimed at
    # all (block-edit is exclusive and is refused on anything not yet in the corpus), so
    # gating it would shut a door with nothing behind it. Those are NAMED in the report
    # instead, which is the honest version of "this one is not joinable".
    claimed_files = set()
    for entry in declared_claims:
        if isinstance(entry, dict) and entry.get("kind") in JOINABLE_KINDS and entry.get("value"):
            owner, _ = owner_of(entry.get("kind"), entry.get("value"))
            if owner:
                claimed_files.add(owner)
    for path in ir_files:
        header, name, number = identity.get(path, (None, None, None))
        if number:
            gated_files += 1
            if path in claimed_files:
                covered_files += 1
            else:
                fail("%s (%s) was edited and NO declared claim names it - a reservation "
                     "covers the block it names, not every block the run touched"
                     % (path, number))
        elif path not in claimed_files:
            uncovered_note.append("%s (%s, no NUMBER line - not gated)"
                                  % (path, header or "unrecognised header"))
        else:
            covered_files += 1

    # ASSERT THE DENOMINATORS. Each branch above already fails per claim and per file, so
    # these cannot be the only thing standing between a bad run and a green - they are here
    # because the finding that produced this rewrite was a ZERO that read as a pass. A join
    # that examined nothing is the one shape a per-item loop reports as silence.
    if joinable == 0:
        fail("the claims join EXAMINED NOTHING: %d claim(s) declared and not one reached "
             "the join (kinds declared: %s). Either every kind named is one that cannot be "
             "tied to a file, or every claim failed an earlier check - and a reservation "
             "that cannot be tied to the work proves the work was reserved by nobody"
             % (len(declared_claims),
                ", ".join(sorted(str(e.get("kind")) for e in declared_claims
                                 if isinstance(e, dict) and e.get("kind"))) or "none"))
    elif joined == 0:
        fail("every one of the %d joinable claim(s) failed to join - the claims gate "
             "compared %d and confirmed none of them" % (joinable, joinable))

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
out.write("no hash (DB/UDT/tag table)     : %d\n" % len(not_hashable))
out.write("gates recorded                 : %d\n" % len(checks))
# ASSERT THE DENOMINATOR. "0 problems" is true of nothing found and of nothing looked at,
# so the report says what the claims gate compared: how many the evidence declared, how
# many the SHARED store confirmed, how many were JOINED to a file on disk out of how many
# could be, how many edited blocks a claim covered out of how many must be covered, and -
# the line that exposes a wrong root - how many claims that store holds at all.
out.write("claims declared                : %s\n"
          % (len(declared_claims) if isinstance(declared_claims, list) else "ABSENT"))
out.write("claims confirmed in store      : %d\n" % claims_verified)
out.write("claims joined to files on disk : %d of %d joinable\n" % (joined, joinable))
out.write("claim kinds NOT joinable       : %s\n"
          % (", ".join("%s x%d" % (k, n) for k, n in sorted(unjoinable_kinds.items()))
             or "none"))
out.write("edited blocks covered by claim : %d of %d that carry a NUMBER\n"
          % (covered_files, gated_files))
out.write("claims store root              : %s%s\n"
          % (CLAIMS_ROOT,
             "   [SELF-TEST ROOT - NOT THE SHARED REGISTRY]" if SELFTEST_STORE else ""))
out.write("store consulted / holds        : %s / %d\n" % (store_path or "<none>", store_held))
out.write("deferred (declared, not run)   : %d\n" % len(deferred))
out.write("transient (ran, superseded)    : %d\n" % len(transient))
out.write("PROBLEMS                       : %d\n" % len(problems))

if not_hashable:
    out.write("\n--- listed without a hash, and correctly so (ir-hash keys code blocks) ---\n")
    for path, header in not_hashable:
        out.write("  %s (%s)\n" % (path, header))

if uncovered_note:
    out.write("\n--- NOT JOINED TO ANY CLAIM, and not gated - said by name, not skipped ---\n")
    for note in uncovered_note:
        out.write("  %s\n" % note)
    out.write("A UDT or tag table carries no block number, and a NEW one cannot hold a\n")
    out.write("claim at all (block-edit is exclusive and is refused on anything not yet in\n")
    out.write("the corpus). These are reported so the gap is visible rather than silent.\n")

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
    # A self-test root can never print the plain banner. The claims half of such a run was
    # gated against a store nobody shares, which is exactly the state F4 was about: it
    # proves the checker's logic and nothing whatever about a real reservation.
    out.write("\nVERIFIED%s: every recorded gate passed and every hash still matches.\n"
              % (" (SELF-TEST STORE)" if SELFTEST_STORE else ""))
    out.write("This says the gates ran and the files are what the agent said they were.\n")
    out.write("It says NOTHING about whether the logic is correct - that is the review.\n")
    if SELFTEST_STORE:
        out.write("\n🔴 The claims half ran against %s, a SELF-TEST\n" % CLAIMS_ROOT)
        out.write("store carrying the %s sentinel - NOT the shared registry.\n"
                  % SELFTEST_SENTINEL)
        out.write("This is not quotable as evidence that any reservation was taken. Only a\n")
        out.write("run with no --claims-root is.\n")
out.flush()

raise SystemExit(1 if (problems or deferred) else 0)
