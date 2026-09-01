"""Offline test suite for tools/check-agent-evidence.py.

    python tools/check-agent-evidence.tests.py

Same reason as check-file-budgets.tests.py: a verifier nobody has watched REFUSE
anything is a verifier nobody should trust. This one matters more than most, because
its whole purpose is to let the orchestrator stop re-reading the sub-agent's work - so
if it waves through a bad hand-back, the check it replaced was better than it is.

Fixtures are built against a real block in ir/test-project001, whose ir-hash is read
once from the Release converter and then deliberately corrupted in one case. Requires
that binary; skips the hash-dependent cases with a loud note if it is absent rather
than reporting green on tests that did not run - "EXAMINED NOTHING" is not a pass here
either.

Claims fixtures are planted into a TEMPORARY store root handed over via `--claims-root`
plus its sentinel file, never into the shared C:\\ProgramData\\Ladder-AI\\claims one - see
the comment above STORE_ROOT. Some cases deliberately run with no override, to assert that
the shared root is what the checker reaches for when nobody tells it otherwise, and that
LADDER_CLAIMS_DIR cannot move it.
"""
import hashlib, io, json, os, shutil, subprocess, sys, tempfile

EXIT_OK = 0
EXIT_PROBLEMS = 1
EXIT_NOT_VERIFIED = 2

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "check-agent-evidence.py")
CONVERTER = os.path.join(
    ROOT, "src", "converter", "Converter", "bin", "Release", "net8.0", "converter.exe"
)
BLOCK = "ir/test-project001/FB_PusherControl.ir"
BLOCK_NAME = "FB_PusherControl"
OTHER = "ir/test-project001/FC_AlarmsMain.ir"       # NUMBER 4, i.e. FC4
OTHER_NAME = "FC_AlarmsMain"
DB = "ir/test-project001/DB_Alarms.ir"              # NUMBER 5, member ShredderAlarm0
UDT = "ir/test-project001/UDT_HopperBlockageIO.ir"  # TYPE - no NUMBER line at all
PROJECT = "ir/test-project001"
SLUG = "test-project001"
BLOCK_NUMBER = "FB3"          # the `NUMBER 3` line in BLOCK, under `BLOCK FB` - the join
AGENT = "test-session-0001/lad-coder"
SENTINEL = "SELFTEST-CLAIMS-ROOT"

# --- `--staged-only`: the pre-commit gate on the gate --------------------------------
#
# F1 through F5 all shipped because a change to tools/check-agent-evidence.py had no gate
# on it whatsoever, and a COMMIT is the right event for that one - unlike the evidence
# check itself, which deliberately does NOT live in a commit hook. The distinction is the
# whole argument and it is worth keeping next to the code: an evidence file is a
# point-in-time hand-back artifact whose hashes legitimately drift once later commits touch
# the same block, and whose claims are legitimately released the moment the dispatcher has
# verified them - so re-checking it at commit time refuses work that was correct
# (agent-tasks/README.md). A CHANGE TO THE CHECKER has neither property. It is a normal
# source edit, it is always in the index when it happens, and it either passes its tests or
# it does not.
#
# The decision lives HERE, in Python, and not in the hook, for the reason hooks/pre-commit's
# own header gives: "a hook that grows a loop per budgeted path is a hook nobody tests".
# The hook stays a three-line invocation and this is covered by
# tools/check-file-budgets.tests.py, which is where the hook's logic has always been tested.
#
# FAILS CLOSED, the same way the hook does: an unreadable index exits 2 and refuses the
# commit rather than quietly deciding the suite was not needed.
GATED_PATHS = ("tools/check-agent-evidence.py", "tools/check-agent-evidence.tests.py")

if "--staged-only" in sys.argv[1:]:
    os.chdir(ROOT)
    try:
        _raw = subprocess.check_output(["git", "diff", "--cached", "--name-only"])
    except (subprocess.CalledProcessError, OSError):
        sys.stderr.write("cannot read the staged set - NOTHING CHECKED\n")
        raise SystemExit(2)
    # git's plumbing emits forward slashes on every platform, so there is no separator
    # normalisation here. A `.replace("\\", "/")` was written and then deleted: the
    # mutation campaign could not make it matter, and an untestable line in a gate is
    # exactly the sort of thing that gets believed. check-file-budgets.py's staged_paths
    # normalises the TABLE side for the same reason.
    _staged = set(_raw.decode("utf-8", "replace").split("\n"))
    if not (_staged & set(GATED_PATHS)):
        print("the agent-evidence checker is not staged - its self-tests were not run.")
        raise SystemExit(0)
    print("the agent-evidence checker IS staged - running its self-tests.")

passed, failed, skipped, failures = 0, 0, 0, []

# --- the claims store the checker is pointed at -------------------------------------
#
# NOT the real C:\ProgramData\Ladder-AI\claims: these cases plant and mutate claims, and a
# test suite that writes into the store real agents coordinate through would be a test
# suite that causes the collisions it is checking for.
#
# THE OVERRIDE IS `--claims-root` PLUS A SENTINEL FILE, NOT LADDER_CLAIMS_DIR (2026-08-24).
# It used to be the env var, and that was the F4 defect: `converter claim` reads the SAME
# variable, so one export moved the writer and the reader together and a private root
# produced a green with zero cross-agent coordination in it. The checker now resolves a
# constant, and only a directory carrying the sentinel this suite plants can move it - a
# thing no real store has and no ambient environment produces.
#
# A claim file is planted directly rather than acquired through `converter claim`, because
# an ALLOCATION claim on a number the corpus already uses is (correctly) REFUSED - and the
# state this gate checks is exactly that one: the claim was taken before the block existed,
# and the block exists now. `complete_evidence_verifies` is the control that proves the
# planting matches what the tool reads back; if the format were wrong it fails first.
STORE_ROOT = tempfile.mkdtemp(prefix="ladder-claims-")
EMPTY_ROOT = tempfile.mkdtemp(prefix="ladder-claims-empty-")
NO_SENTINEL_ROOT = tempfile.mkdtemp(prefix="ladder-claims-nosentinel-")


def sentinel(root):
    with io.open(os.path.join(root, SENTINEL), "w", encoding="utf-8") as fh:
        fh.write("planted by tools/check-agent-evidence.tests.py\n")


sentinel(STORE_ROOT)
sentinel(EMPTY_ROOT)


def plant(root, kind, value, agent=AGENT, project=PROJECT):
    folder = os.path.join(root, SLUG)
    if not os.path.isdir(folder):
        os.makedirs(folder)
    safe = "".join(c if (c.isalnum() or c in "._-") else "_" for c in value)
    digest = hashlib.sha256(value.encode("utf-8")).hexdigest()[:8]
    name = "%s-%s-%s.claim" % (kind, safe, digest)
    with io.open(os.path.join(folder, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("project %s\nkind %s\nvalue %s\nagent %s\npurpose fixture\n"
                 "created 2026-08-24T00:00:00Z\n" % (project, kind, value, agent))


plant(STORE_ROOT, "block-edit", BLOCK_NAME)
plant(STORE_ROOT, "block-number", BLOCK_NUMBER)
plant(STORE_ROOT, "block-edit", "FC_ControlMain", agent="someone-else/lad-coder")
plant(STORE_ROOT, "block-edit", OTHER_NAME)
plant(STORE_ROOT, "block-edit", "DB_Alarms")
plant(STORE_ROOT, "block-edit", "UDT_HopperBlockageIO")
plant(STORE_ROOT, "block-network", "FB_PusherControl:2")
plant(STORE_ROOT, "block-network", "FB_PusherControl:97")
plant(STORE_ROOT, "db-member", "DB_Alarms.ShredderAlarm0")
plant(STORE_ROOT, "db-member", "DB_Alarms.NoSuchMember")
plant(STORE_ROOT, "alarm-bit", "DB_Alarms.ShredderAlarm0.%X9")
plant(STORE_ROOT, "tag", "MotorRunFeedback")
plant(STORE_ROOT, "block-number", "DB5")


def case(name, body):
    global passed, failed
    try:
        body()
    except AssertionError as exc:
        failed += 1
        failures.append("%s: %s" % (name, exc))
        print("FAIL  %s" % name)
        print("      %s" % exc)
    else:
        passed += 1
        print("PASS  %s" % name)


def real_hash(path=BLOCK):
    raw = subprocess.check_output([CONVERTER, "ir-hash", path, "--json"], cwd=ROOT)
    return json.loads(raw.decode("utf-8"))["hashes"][0]["hash"]


def good_doc(digest):
    return {
        "schema": "ladder-ai/agent-evidence/1",
        "task": "test-fixture",
        "kind": "modify",
        "skill": "gen-block-modify-fix",
        "files": [{"path": BLOCK, "ir_hash": digest}],
        "claims": [
            {"kind": "block-edit", "value": "FB_PusherControl", "agent": AGENT},
            {"kind": "block-number", "value": BLOCK_NUMBER, "agent": AGENT},
        ],
        "checks": [
            {"tool": "converter preflight", "exit": 0},
            {"tool": "converter diff --only", "exit": 0},
            {"tool": "openness-cli sanity-check", "exit": 0},
        ],
    }


def run(doc_or_text, store=STORE_ROOT, env_claims_dir=None):
    """store=None passes no --claims-root, i.e. gates against the real shared root.

    env_claims_dir sets LADDER_CLAIMS_DIR, which must NOT move the store the checker
    consults - that is F4, and there is a case for it below.
    """
    handle, path = tempfile.mkstemp(suffix=".json")
    os.close(handle)
    with io.open(path, "w", encoding="utf-8") as fh:
        if isinstance(doc_or_text, str):
            fh.write(doc_or_text)
        else:
            fh.write(json.dumps(doc_or_text))
    env = dict(os.environ)
    env.pop("LADDER_CLAIMS_DIR", None)
    if env_claims_dir is not None:
        env["LADDER_CLAIMS_DIR"] = env_claims_dir
    argv = [sys.executable, SCRIPT, path]
    if store is not None:
        argv += ["--claims-root", store]
    try:
        proc = subprocess.Popen(
            argv,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=ROOT, env=env,
        )
        out, err = proc.communicate()
        return (proc.returncode,
                out.decode("utf-8", "replace"),
                err.decode("utf-8", "replace"))
    finally:
        os.remove(path)


def assert_eq(actual, expected, what):
    assert actual == expected, "%s: expected %r, got %r" % (what, expected, actual)


def assert_in(needle, haystack, what):
    assert needle in haystack, "%s: %r not found in:\n%s" % (what, needle, haystack)


# --- it verifies -------------------------------------------------------------------

def complete_evidence_verifies():
    code, out, _ = run(good_doc(real_hash()))
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("VERIFIED", out, "verdict")


def verdict_does_not_overclaim():
    """The pass text must not imply the logic was judged. It was not."""
    _, out, _ = run(good_doc(real_hash()))
    assert_in("NOTHING about whether the logic is correct", out, "scope disclaimer")


# --- it refuses --------------------------------------------------------------------

def drifted_hash_fails():
    doc = good_doc("0" * 64)
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("DRIFTED", out, "the reason")


def zero_files_fails():
    doc = good_doc(real_hash())
    doc["files"] = []
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("ZERO files", out, "the reason")


def exit_2_is_not_a_pass():
    """EMPTY IS NOT CLEAN - the whole point of the 1-vs-2 split."""
    doc = good_doc(real_hash())
    doc["checks"][1] = {"tool": "converter undriven-scan", "exit": 2}
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("EXAMINED NOTHING", out, "the reason")


def new_block_needs_no_diff():
    """A new block has nothing to diff against. Requiring one would make gen-block-new
    evidence permanently unverifiable."""
    doc = good_doc(real_hash())
    doc["kind"] = "new"
    doc["skill"] = "gen-block-new"
    doc["checks"] = [c for c in doc["checks"] if "diff" not in c["tool"]]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a new-block hand-back without a diff must verify")
    assert_in("VERIFIED", out, "verdict")


def modify_without_diff_fails():
    """The other half. Waiving diff for everyone would drop the invariance proof that
    IS the deliverable on a modification."""
    doc = good_doc(real_hash())
    doc["checks"] = [c for c in doc["checks"] if "diff" not in c["tool"]]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("invariance proof", out, "the reason")


def unstated_kind_is_exit_2():
    doc = good_doc(real_hash())
    del doc["kind"]
    code, out, err = run(doc)
    assert_eq(code, EXIT_NOT_VERIFIED, "exit code")
    assert_in("NOTHING WAS VERIFIED", err, "diagnostic")
    assert "VERIFIED:" not in out, "an unstated kind must never read as verified"


def absent_compile_gate_fails():
    """An omitted gate is not a passed gate."""
    doc = good_doc(real_hash())
    doc["checks"] = [c for c in doc["checks"] if "sanity-check" not in c["tool"]]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("required gate absent", out, "the reason")


def inconsistent_after_compile_fails():
    doc = good_doc(real_hash())
    doc["checks"].append({
        "tool": "openness-cli compile", "exit": 0,
        "json": {"consistentAfterCompile": False},
    })
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("cannot be exported", out, "the reason")


def error_counter_fails():
    doc = good_doc(real_hash())
    doc["checks"].append({
        "tool": "openness-cli compile", "exit": 0, "json": {"errors": 3},
    })
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("errors = 3", out, "the reason")


def warning_state_does_not_fail():
    """KEY ON ERRORS, NEVER ON STATE. A healthy station compile returns Warning with
    errors=0; gating on state would mark a healthy project unhealthy forever."""
    doc = good_doc(real_hash())
    doc["checks"].append({
        "tool": "openness-cli compile", "exit": 0,
        "json": {"state": "Warning", "errors": 0, "warnings": 17},
    })
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a warning-with-zero-errors compile must verify")
    assert_in("VERIFIED", out, "verdict")


def db_file_cannot_be_hash_claimed():
    """ir-hash keys code blocks only. Evidence claiming a hash for a DB is the defect."""
    doc = good_doc(real_hash())
    doc["files"].append(
        {"path": "ir/test-project001/DB_Settings.ir", "ir_hash": "f" * 64})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("not a block file", out, "ir-hash's own reason is surfaced")


# --- the claims gate ----------------------------------------------------------------
#
# Both directions on every rule: the refusal, and the control that must still verify. The
# control is `complete_evidence_verifies` above, which carries two real claims and passes.

def the_claims_gate_actually_compared_something():
    """ASSERT THE DENOMINATOR. A green whose claims counters read 0/0 examined nothing."""
    _, out, _ = run(good_doc(real_hash()))
    assert_in("claims declared                : 2", out, "declared count")
    assert_in("claims confirmed in store      : 2", out, "confirmed count")
    assert_in("claims joined to files on disk : 2 of 2 joinable", out, "the join ran")
    assert_in("edited blocks covered by claim : 1 of 1", out, "the coverage pass ran")


def absent_claims_section_fails():
    """AN ABSENT GATE IS NOT A PASSED GATE, applied to the newest gate."""
    doc = good_doc(real_hash())
    del doc["claims"]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("required gate absent", out, "the reason")
    assert_in("no `claims` section", out, "which gate")


def empty_claims_array_fails():
    doc = good_doc(real_hash())
    doc["claims"] = []
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("EMPTY IS NOT CLEAN", out, "the reason")


def a_claim_not_in_the_store_fails():
    """The evidence says it reserved FB77; the registry has never heard of it."""
    doc = good_doc(real_hash())
    doc["claims"].append({"kind": "block-number", "value": "FB77", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("NOT IN THE STORE", out, "the reason")


def f7_a_non_empty_store_holding_none_of_them_says_wrong_store():
    """🔴 THE WRONG STORE IS NOT THE EMPTY STORE, AND IT USED TO READ AS A LOST REGISTRY.

    MEASURED 2026-09-01: a lane staged into `work-runstate/ir/`. `converter claims` names the
    store from the LEAF of --project, and --project is derived from the directory the .ir files
    sit in - so leaf `ir` selected `…\\claims\\ir`, a DIFFERENT project's store left over from an
    earlier generation. It was not empty, so the vacuity guard (which keys on ZERO claims) stayed
    quiet, and all five of the lane's correctly-held claims were reported NOT IN THE STORE. The
    lane's own conclusion was that the registry had lost them. Renaming the scratch directory
    fixed it.

    Deriving the project from the paths STAYS - a declared project name would be one more field
    an agent could point at a store that agrees with it. What changes is the diagnosis when the
    answer is unanimous: a store that held claims and matched NOT ONE of the declared set is far
    more likely to be the wrong store than a registry that lost one lane's entire reservation set
    while keeping everybody else's.
    """
    doc = good_doc(real_hash())
    doc["claims"] = [
        {"kind": "block-number", "value": "FB77", "agent": AGENT},
        {"kind": "block-edit", "value": "FB_NotInThisStore", "agent": AGENT},
    ]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("WRONG STORE", out, "it names the likely cause")
    assert_in("LEAF", out, "it says where the store name comes from")
    assert "ZERO claims" not in out, (
        "the vacuity guard must NOT fire - this store is populated, and conflating "
        "'wrong store' with 'empty store' is what hid this for a whole lane")


def f7_control_a_partial_match_is_still_a_plain_absent_claim():
    """The control: one bad claim among good ones is an ABSENT CLAIM, not a wrong store.

    Without this, the case above could pass by shouting WRONG STORE at every missing
    reservation - which would bury the finding it exists to sharpen.
    """
    doc = good_doc(real_hash())
    doc["claims"].append({"kind": "block-number", "value": "FB77", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("NOT IN THE STORE", out, "the plain accusation still stands")
    assert "WRONG STORE" not in out, (
        "some declared claims WERE matched, so the store is the right one and a single "
        "absent claim must read as an absent claim")


def a_claim_held_by_another_agent_fails():
    """Declaring someone else's live claim as your own is the collision, not the cure."""
    doc = good_doc(real_hash())
    doc["claims"].append({"kind": "block-edit", "value": "FC_ControlMain", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("held by 'someone-else/lad-coder'", out, "the real holder is named")


def a_claim_missing_its_agent_fails():
    doc = good_doc(real_hash())
    doc["claims"][0] = {"kind": "block-edit", "value": "FB_PusherControl"}
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("coordinates nothing", out, "the reason")


def block_number_disagreeing_with_the_ir_fails():
    """🔴 THE JOIN. FB_PusherControl.ir says NUMBER 3; the evidence claims FB51. Both the
    claim and the store agree with each other and BOTH are wrong about the file on disk -
    which is the only reason this check is worth having."""
    plant(STORE_ROOT, "block-number", "FB51")
    doc = good_doc(real_hash())
    doc["claims"][1] = {"kind": "block-number", "value": "FB51", "agent": AGENT}
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("which is none of the .ir files", out, "the reason")
    assert_in("FB3", out, "what the file on disk actually says")


def a_new_run_without_a_block_number_claim_fails():
    doc = good_doc(real_hash())
    doc["kind"] = "new"
    doc["skill"] = "gen-block-new"
    doc["checks"] = [c for c in doc["checks"] if "diff" not in c["tool"]]
    doc["claims"] = [{"kind": "block-edit", "value": "FB_PusherControl", "agent": AGENT}]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("required claim absent", out, "the reason")
    assert_in("block-number", out, "which kind")


def a_modify_run_without_a_block_edit_claim_fails():
    """The other half - a modification takes exclusive hold of what it edits."""
    doc = good_doc(real_hash())
    doc["claims"] = [{"kind": "block-number", "value": BLOCK_NUMBER, "agent": AGENT}]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("required claim absent", out, "the reason")
    assert_in("block-edit", out, "which kind")


def an_empty_store_root_is_never_a_pass():
    """🔴 THE VACUITY THAT WOULD MAKE THE WHOLE GATE A NO-OP. A per-worktree root is empty,
    an empty store answers nothing, and 'no claims found' must never read as 'no claims
    needed'. The evidence here is otherwise perfect."""
    code, out, _ = run(good_doc(real_hash()), store=EMPTY_ROOT)
    assert_eq(code, EXIT_PROBLEMS, "an empty store must refuse, not wave through")
    assert_in("ZERO claims", out, "it says the store is empty")
    assert_in("empty store grants everything", out, "and why that is not a pass")


def a_store_that_cannot_answer_is_not_checked():
    """The doubled root - the one misconfiguration the registry itself refuses (a root
    ending in the project slug resolves to <slug>/<slug>, a second empty private store).
    The checker must report NOT CHECKED, not silence."""
    doubled = os.path.join(STORE_ROOT, SLUG)
    sentinel(doubled)   # so the refusal under test is the registry's, not the sentinel's
    code, out, _ = run(good_doc(real_hash()), store=doubled)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("NOT CHECKED", out, "the reason")


def the_default_store_root_is_the_shared_one():
    """With no override the checker must consult C:\\ProgramData\\Ladder-AI\\claims - the
    root every agent shares. Reading anything else would make this gate check a store no
    agent writes to."""
    _, out, _ = run(good_doc(real_hash()), store=None)
    assert_in(r"claims store root              : C:\ProgramData\Ladder-AI\claims",
              out, "the resolved root is reported")


def claims_without_any_ir_file_fails():
    """Nothing to join them to. Reported rather than quietly skipped."""
    doc = good_doc(real_hash())
    doc["files"] = [{"path": "gen/test-project001/architecture.md", "ir_hash": "x" * 64}]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("nothing to join them to", out, "the reason")


# --- the 2026-08-24 adversarial audit: one case per finding, each written so it would
# --- have FAILED against the checker as it stood that morning. These five are the spec.

def f1_a_run_holding_its_claims_verifies():
    """F1. The gate reded on every run that followed the skills, because all three told the
    agent to release every claim before hand-back while verification happens AFTER it. The
    store was therefore empty by the time this ran and the message it printed accused the
    reader of a misconfigured root.

    Half the fix is in the skills (they now hand back HOLDING the claims); the half that
    lives here is that the report must name the release-order cause FIRST when the store is
    empty, instead of only the wrong-root one. The control is the whole rest of this file:
    a run whose claims are still held verifies."""
    code, out, _ = run(good_doc(real_hash()))
    assert_eq(code, EXIT_OK, "a hand-back still HOLDING its claims must verify")
    code, out, _ = run(good_doc(real_hash()), store=EMPTY_ROOT)
    assert_eq(code, EXIT_PROBLEMS, "and one whose claims are gone must not")
    assert_in("RELEASED before hand-back", out, "the release-order cause is named")
    assert out.index("RELEASED before hand-back") < out.index("different root"), \
        "the likely cause must be named before the wrong-root one, not after it"


def f2_a_block_edit_claim_must_name_an_edited_file():
    """🔴 F2. `block-edit FB_PusherControl` while the only edited file is FC_AlarmsMain.ir.
    Measured before the fix: PROBLEMS 0, VERIFIED - the join ran only for block-number, so
    the kind the modify floor REQUIRES was joined to nothing at all."""
    doc = good_doc(real_hash())
    doc["files"] = [{"path": OTHER, "ir_hash": real_hash(OTHER)}]
    doc["claims"] = [{"kind": "block-edit", "value": BLOCK_NAME, "agent": AGENT}]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("which is none of the .ir files", out, "the claim joined to nothing")
    assert_in("claims joined to files on disk : 0 of 1", out, "and the counter says so")


def f2_every_edited_block_needs_its_own_claim():
    """The other direction of F2. One claim used to cover any number of edited blocks."""
    doc = good_doc(real_hash())
    doc["files"] = [{"path": BLOCK, "ir_hash": real_hash()},
                    {"path": OTHER, "ir_hash": real_hash(OTHER)}]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("NO declared claim names it", out, "the uncovered file is named")
    assert_in(OTHER, out, "which file")
    assert_in("edited blocks covered by claim : 1 of 2", out, "the denominator")


def f2_control_two_blocks_two_claims_verifies():
    """The control. Claim both and edit both, and it must still verify - otherwise the fix
    above is just a gate that refuses everything."""
    doc = good_doc(real_hash())
    doc["files"] = [{"path": BLOCK, "ir_hash": real_hash()},
                    {"path": OTHER, "ir_hash": real_hash(OTHER)}]
    doc["claims"].append({"kind": "block-edit", "value": OTHER_NAME, "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "two claimed blocks, two edited blocks")
    assert_in("edited blocks covered by claim : 2 of 2", out, "the denominator")


def f2_a_block_network_claim_joins_to_the_network():
    """block-network reserves ONE network. Without the second half of the join, any edit at
    all to the block would satisfy it."""
    doc = good_doc(real_hash())
    doc["claims"].append(
        {"kind": "block-network", "value": "FB_PusherControl:97", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("no `NETWORK 97` line", out, "the reason")

    doc = good_doc(real_hash())
    doc["claims"].append(
        {"kind": "block-network", "value": "FB_PusherControl:2", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a network that exists must verify")
    assert_in("claims joined to files on disk : 3 of 3", out, "all three joined")


def f2_a_db_member_claim_joins_to_the_member():
    doc = good_doc(real_hash())
    doc["files"].append({"path": DB})
    doc["claims"].append({"kind": "block-edit", "value": "DB_Alarms", "agent": AGENT})
    doc["claims"].append(
        {"kind": "db-member", "value": "DB_Alarms.NoSuchMember", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("appears nowhere in", out, "the reason")

    doc = good_doc(real_hash())
    doc["files"].append({"path": DB})
    doc["claims"].append({"kind": "block-edit", "value": "DB_Alarms", "agent": AGENT})
    doc["claims"].append(
        {"kind": "alarm-bit", "value": "DB_Alarms.ShredderAlarm0.%X9", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a member that exists must verify")


def f2_an_unjoinable_kind_is_named_not_skipped():
    """`tag` cannot be joined to an edited block - the ENGINEER creates tags (hard rule 3).
    Saying so by name is the honest version; passing silently is what F2 was."""
    doc = good_doc(real_hash())
    doc["claims"].append({"kind": "tag", "value": "MotorRunFeedback", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "an unjoinable kind alongside joinable ones is not a failure")
    assert_in("claim kinds NOT joinable       : tag x1", out, "it is named, by kind")


def f2_a_run_whose_only_claims_are_unjoinable_fails():
    """ASSERT THE DENOMINATOR. If nothing joinable was declared, the join examined nothing -
    and examining nothing is never a pass."""
    doc = good_doc(real_hash())
    doc["claims"] = [{"kind": "tag", "value": "MotorRunFeedback", "agent": AGENT}]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("EXAMINED NOTHING", out, "the reason")
    assert_in("claims joined to files on disk : 0 of 0", out, "the zero denominator")


def f3_an_instance_db_can_be_listed_and_claimed():
    """🔴 F3, THE DOUBLE BIND. gen-block-new claims the iDB's number; lad-coder.md says a DB
    has no hash, so list it without one. Before the fix: listing it failed with `no ir_hash
    recorded`, and omitting it failed because the block-number claim matched no NUMBER line.
    Both doors shut on every new FB."""
    doc = good_doc(real_hash())
    doc["kind"] = "new"
    doc["skill"] = "gen-block-new"
    doc["checks"] = [c for c in doc["checks"] if "diff" not in c["tool"]]
    doc["files"].append({"path": DB})            # no ir_hash - correct for a DB
    doc["claims"].append({"kind": "block-number", "value": "DB5", "agent": AGENT})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a DB listed without a hash must verify")
    assert_in("no hash (DB/UDT/tag table)     : 1", out, "counted, not silently skipped")
    assert_in("listed without a hash, and correctly so", out, "and named")


def f3_a_code_block_still_needs_its_hash():
    """The other half. The exemption is decided from the file's own header, so it must not
    reach a code block - otherwise F3's fix is a hole where the hash check used to be."""
    doc = good_doc(real_hash())
    del doc["files"][0]["ir_hash"]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("no ir_hash recorded", out, "the reason")


def f4_ladder_claims_dir_cannot_move_the_store():
    """🔴 F4. The docstring argued a private root "fails closed". It did not: `converter
    claim` reads the SAME env var (Program.cs ResolveClaimsDir), so one export moved the
    writer and the reader together - measured green, PROBLEMS 0, zero coordination. The
    checker now resolves a constant and REFUSES a run whose environment points the claim
    tool somewhere else."""
    code, out, _ = run(good_doc(real_hash()), store=None, env_claims_dir=STORE_ROOT)
    assert_eq(code, EXIT_PROBLEMS, "an env-var-relocated store must never verify")
    assert_in("LADDER_CLAIMS_DIR is set to", out, "the divergence is named")
    assert_in("coordinated with nobody", out, "and what it means")
    assert_in(r"claims store root              : C:\ProgramData\Ladder-AI\claims", out,
              "the store consulted is still the shared one")


def f4_a_private_root_without_the_sentinel_is_refused():
    code, out, err = run(good_doc(real_hash()), store=NO_SENTINEL_ROOT)
    assert_eq(code, EXIT_NOT_VERIFIED, "exit code")
    assert_in("NOTHING VERIFIED", err, "diagnostic")
    assert "VERIFIED:" not in out, "a refused root must never read as verified"


def f4_a_selftest_store_never_prints_the_plain_banner():
    """Every other case in this file runs against the self-test root, so the one thing that
    must be true of all of them is that none can be quoted as proof a reservation existed."""
    code, out, _ = run(good_doc(real_hash()))
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("VERIFIED (SELF-TEST STORE)", out, "the qualified banner")
    assert_in("NOT the shared registry", out, "and why it is qualified")
    assert "\nVERIFIED: " not in out, "the plain banner must be unreachable from here"


def f5_an_uppercase_ir_extension_still_enters_the_gate():
    """🔴 F5. The gate was entered via `endswith(".ir")`, case-sensitively, on Windows paths
    that are not. Measured with the extension typed `.IR`: claims ABSENT, PROBLEMS 0,
    VERIFIED, exit 0 - while ir_hash() found and hashed the very same file. One character of
    path case turned the whole gate off."""
    doc = good_doc(real_hash())
    doc["files"][0]["path"] = BLOCK.replace(".ir", ".IR")
    del doc["claims"]
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "a .IR path must not skip the claims gate")
    assert_in("no `claims` section", out, "the gate was entered")
    assert "VERIFIED:" not in out, "and it must not read as verified"


def f5_a_mixed_case_directory_is_one_project_not_two():
    """The same sweep. `ir/` and `IR/` are one directory on Windows and one bucket in the
    store; querying it twice would report double the claims it holds, which is the kind of
    inflated denominator this report exists to stop."""
    doc = good_doc(real_hash())
    doc["files"] = [{"path": BLOCK, "ir_hash": real_hash()},
                    {"path": OTHER.replace("ir/", "IR/", 1), "ir_hash": real_hash(OTHER)}]
    doc["claims"].append({"kind": "block-edit", "value": OTHER_NAME, "agent": AGENT})
    planted = len([f for f in os.listdir(os.path.join(STORE_ROOT, SLUG))
                   if f.endswith(".claim")])
    code, out, _ = run(doc)
    held = [line for line in out.splitlines() if line.startswith("store consulted")]
    assert held, "no store line in:\n%s" % out
    assert held[0].endswith("/ %d" % planted), \
        "the store was counted more than once: %s (expected %d)" % (held[0], planted)
    assert_eq(code, EXIT_OK, "and the run itself must still verify")


def f5_an_uppercase_ir_extension_verifies_when_claimed():
    """The control: the case fix must let a correct `.IR` run through, not refuse it."""
    doc = good_doc(real_hash())
    doc["files"][0]["path"] = BLOCK.replace(".ir", ".IR")
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a claimed .IR run must verify")
    assert_in("claims joined to files on disk : 2 of 2", out, "the join ran on it")


# --- it cannot run -----------------------------------------------------------------

def declared_deferral_is_reported_and_still_fails():
    """The whole point: a split run can now produce a hand-back that SAYS what it deferred,
    without that becoming a way to get a green."""
    doc = good_doc(real_hash())
    doc["checks"] = [c for c in doc["checks"] if "sanity-check" not in c["tool"]]
    doc["checks"].append({"tool": "openness-cli sanity-check",
                          "deferred": "Portal withheld by the dispatcher; IR half only"})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "a deferred gate is NOT a pass")
    assert_in("DEFERRED, declared rather than missing", out, "its own section")
    assert_in("Portal withheld", out, "the reason is shown")
    assert "required gate absent" not in out, \
        "a declared deferral must not also read as absent - that was the old, wrong output"


def deferral_without_a_reason_fails():
    doc = good_doc(real_hash())
    doc["checks"] = [c for c in doc["checks"] if "sanity-check" not in c["tool"]]
    doc["checks"].append({"tool": "openness-cli sanity-check", "deferred": "  "})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("just an omission", out, "the reason")


def deferral_carrying_an_exit_fails():
    """It either ran or it did not. Both would let a real result hide behind a deferral."""
    doc = good_doc(real_hash())
    doc["checks"].append({"tool": "openness-cli compile", "exit": 0,
                          "deferred": "did not run"})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("either ran or it did", out, "the reason")


def the_word_deferred_in_tool_launders_nothing():
    """THE SUBSTRING TRAP. Required gates are matched by substring over tool names, so a
    marker written into `tool` satisfies the token test. It must still fail."""
    doc = good_doc(real_hash())
    doc["checks"] = [c for c in doc["checks"] if "sanity-check" not in c["tool"]]
    doc["checks"].append({"tool": "openness-cli sanity-check DEFERRED (portal busy)"})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "a marker in `tool` declares nothing")
    assert_in("no exit code recorded", out, "it fails as an unrecorded exit, not as a deferral")
    assert "DEFERRED, declared rather than missing" not in out, \
        "a string in `tool` must never reach the deferral bucket"


def transient_is_context_and_does_not_gate():
    doc = good_doc(real_hash())
    doc["checks"].append({"tool": "openness-cli sanity-check (post-import cascade)",
                          "exit": 9,
                          "transient": "ordinary dependent-block cascade, cleared by the "
                                       "recompile recorded above"})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_OK, "a superseded en-route failure must not gate")
    assert_in("ran and failed en route", out, "its own section")
    assert_in("VERIFIED", out, "verdict")


def transient_does_not_satisfy_a_required_gate():
    """The structural protection against relabelling a genuine failure: the real passing
    gate must still be present."""
    doc = good_doc(real_hash())
    doc["checks"] = [c for c in doc["checks"] if "sanity-check" not in c["tool"]]
    doc["checks"].append({"tool": "openness-cli sanity-check", "exit": 9,
                          "transient": "cascade"})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("required gate absent", out, "a transient check is not the gate")


def transient_without_a_reason_fails():
    doc = good_doc(real_hash())
    doc["checks"].append({"tool": "openness-cli compile", "exit": 9, "transient": ""})
    code, out, _ = run(doc)
    assert_eq(code, EXIT_PROBLEMS, "exit code")
    assert_in("transient with no reason", out, "the reason")


# --- F6: "I could not look" is not "I looked and it is absent" ----------------------
#
# Measured 2026-08-24. A lane recorded its file paths RELATIVE ("ir-new/X.ir"), so the
# project resolved against THIS script's cwd rather than the evidence file's, `converter
# claims` exited 2 (directory not found), and the report printed ONE honest "claims NOT
# CHECKED" line followed by FORTY-EIGHT lines saying each claim was not in the store.
# They were all in the store - 48 files there carried that agent id. The dispatcher very
# nearly released the lane's work on the strength of a false accusation.
#
# This is the project's own EMPTY-IS-NOT-CLEAN rule inverted: exit 2 means examined
# nothing, and a check that converts "examined nothing" into a negative finding is worse
# than one that stays quiet, because it gets believed.

def f6_an_unreadable_store_does_not_accuse():
    doc = good_doc(real_hash())
    # A path whose directory does not exist -> the store lookup for that project fails.
    doc["files"] = [{"path": "no-such-dir/FB_PusherControl.ir", "ir_hash": real_hash()}]
    _, out, _ = run(doc)

    assert_in("NOT CHECKED", out, "it must say it could not look")
    assert "NOT IN THE STORE" not in out, (
        "an unreadable store must NOT accuse the claim of being absent; got:\n%s" % out)


def f6_a_readable_store_still_reports_an_absent_claim():
    """The other half: softening must not blunt the real finding."""
    doc = good_doc(real_hash())
    doc["claims"] = [
        {"kind": "block-edit", "value": "FB_NeverClaimedAtAll", "agent": AGENT},
    ]
    _, out, _ = run(doc)

    assert_in("NOT IN THE STORE", out, "a readable store must still accuse")


def wrong_schema_is_exit_2():
    code, out, err = run({"schema": "something-else", "files": [], "checks": []})
    assert_eq(code, EXIT_NOT_VERIFIED, "exit code")
    assert_in("NOTHING VERIFIED", err, "diagnostic")
    assert "VERIFIED:" not in out, "a wrong schema must never read as verified"


def malformed_json_is_exit_2():
    code, out, err = run("{ this is not json")
    assert_eq(code, EXIT_NOT_VERIFIED, "exit code")
    assert_in("NOTHING VERIFIED", err, "diagnostic")


def missing_file_is_exit_2():
    proc = subprocess.Popen(
        [sys.executable, SCRIPT, os.path.join(ROOT, "no-such-evidence.json")],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=ROOT,
    )
    out, err = proc.communicate()
    assert_eq(proc.returncode, EXIT_NOT_VERIFIED, "exit code")
    assert_in("NOTHING VERIFIED", err.decode("utf-8", "replace"), "diagnostic")


CASES = [
    ("complete evidence verifies", complete_evidence_verifies, True),
    ("the pass text does not overclaim", verdict_does_not_overclaim, True),
    ("a drifted ir-hash is refused", drifted_hash_fails, True),
    ("zero files touched is refused", zero_files_fails, True),
    ("exit 2 is refused, never passed", exit_2_is_not_a_pass, True),
    ("a new block needs no diff gate", new_block_needs_no_diff, True),
    ("a modify without a diff gate is refused", modify_without_diff_fails, True),
    ("an unstated kind exits 2", unstated_kind_is_exit_2, True),
    ("an absent compile gate is refused", absent_compile_gate_fails, True),
    ("consistentAfterCompile=false is refused", inconsistent_after_compile_fails, True),
    ("a non-zero error counter is refused", error_counter_fails, True),
    ("Warning state with errors=0 still verifies", warning_state_does_not_fail, True),
    ("a DB hash claim is refused", db_file_cannot_be_hash_claimed, True),
    ("a declared deferral is reported and still fails", declared_deferral_is_reported_and_still_fails, True),
    ("a deferral with no reason fails", deferral_without_a_reason_fails, True),
    ("a deferral carrying an exit code fails", deferral_carrying_an_exit_fails, True),
    ("the word DEFERRED in `tool` launders nothing", the_word_deferred_in_tool_launders_nothing, True),
    ("a transient check is context and does not gate", transient_is_context_and_does_not_gate, True),
    ("a transient check does not satisfy a required gate", transient_does_not_satisfy_a_required_gate, True),
    ("a transient with no reason fails", transient_without_a_reason_fails, True),
    ("the claims gate reports what it compared", the_claims_gate_actually_compared_something, True),
    ("an absent claims section is refused", absent_claims_section_fails, True),
    ("an empty claims array is refused", empty_claims_array_fails, True),
    ("a claim that is not in the store is refused", a_claim_not_in_the_store_fails, True),
    ("a claim held by another agent is refused", a_claim_held_by_another_agent_fails, True),
    ("a claim with no agent is refused", a_claim_missing_its_agent_fails, True),
    ("a block number disagreeing with the .ir is refused", block_number_disagreeing_with_the_ir_fails, True),
    ("a new run with no block-number claim is refused", a_new_run_without_a_block_number_claim_fails, True),
    ("a modify run with no block-edit claim is refused", a_modify_run_without_a_block_edit_claim_fails, True),
    ("an empty store root is never a pass", an_empty_store_root_is_never_a_pass, True),
    ("a store that cannot answer is NOT CHECKED", a_store_that_cannot_answer_is_not_checked, True),
    ("the default store root is the shared one", the_default_store_root_is_the_shared_one, True),
    ("claims with no .ir file are refused", claims_without_any_ir_file_fails, True),

    # The 2026-08-24 audit. One per finding, plus the controls.
    ("F1 a hand-back still holding its claims verifies", f1_a_run_holding_its_claims_verifies, True),
    ("F2 a block-edit claim must name an edited file", f2_a_block_edit_claim_must_name_an_edited_file, True),
    ("F2 every edited block needs its own claim", f2_every_edited_block_needs_its_own_claim, True),
    ("F2 control: two blocks, two claims, verifies", f2_control_two_blocks_two_claims_verifies, True),
    ("F2 a block-network claim joins to the network", f2_a_block_network_claim_joins_to_the_network, True),
    ("F2 a db-member/alarm-bit claim joins to the member", f2_a_db_member_claim_joins_to_the_member, True),
    ("F2 an unjoinable kind is named, not skipped", f2_an_unjoinable_kind_is_named_not_skipped, True),
    ("F2 only-unjoinable claims examined nothing", f2_a_run_whose_only_claims_are_unjoinable_fails, True),
    ("F3 an instance DB can be listed and claimed", f3_an_instance_db_can_be_listed_and_claimed, True),
    ("F3 a code block still needs its hash", f3_a_code_block_still_needs_its_hash, True),
    ("F4 LADDER_CLAIMS_DIR cannot move the store", f4_ladder_claims_dir_cannot_move_the_store, True),
    ("F4 a private root with no sentinel is refused", f4_a_private_root_without_the_sentinel_is_refused, True),
    ("F4 a self-test store never prints the plain banner", f4_a_selftest_store_never_prints_the_plain_banner, True),
    ("F5 an uppercase .IR still enters the gate", f5_an_uppercase_ir_extension_still_enters_the_gate, True),
    ("F5 a mixed-case directory is one project", f5_a_mixed_case_directory_is_one_project_not_two, True),
    ("F5 control: an uppercase .IR verifies when claimed", f5_an_uppercase_ir_extension_verifies_when_claimed, True),

    ("F6 an unreadable store says NOT CHECKED, never NOT IN THE STORE",
     f6_an_unreadable_store_does_not_accuse, True),
    ("F6 control: a readable store still accuses a genuinely absent claim",
     f6_a_readable_store_still_reports_an_absent_claim, True),

    ("F7 a populated store matching NONE of them says WRONG STORE",
     f7_a_non_empty_store_holding_none_of_them_says_wrong_store, True),
    ("F7 control: a partial match is still a plain absent claim",
     f7_control_a_partial_match_is_still_a_plain_absent_claim, True),

    ("a wrong schema exits 2", wrong_schema_is_exit_2, False),
    ("malformed JSON exits 2", malformed_json_is_exit_2, False),
    ("a missing evidence file exits 2", missing_file_is_exit_2, False),
]

for name, body, needs_converter in CASES:
    if needs_converter and not os.path.exists(CONVERTER):
        skipped += 1
        print("SKIP  %s  (Release converter not built)" % name)
        continue
    case(name, body)

for temp in (STORE_ROOT, EMPTY_ROOT, NO_SENTINEL_ROOT):
    shutil.rmtree(temp, ignore_errors=True)

print("=" * 70)
print("RESULT: %d passed, %d failed, %d skipped" % (passed, failed, skipped))
for f in failures:
    print("  failed: %s" % f)
if skipped:
    print("  NOTE: skipped cases did not run. That is not a pass.")
raise SystemExit(1 if (failed or skipped) else 0)
