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

Claims fixtures are planted into a TEMPORARY store root handed over via LADDER_CLAIMS_DIR,
never into the shared C:\\ProgramData\\Ladder-AI\\claims one - see the comment above
STORE_ROOT. One case deliberately runs with no override, to assert that the shared root is
what the checker reaches for when nobody tells it otherwise.
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
PROJECT = "ir/test-project001"
SLUG = "test-project001"
BLOCK_NUMBER = "FB3"          # the `NUMBER 3` line in BLOCK, under `BLOCK FB` - the join
AGENT = "test-session-0001/lad-coder"

passed, failed, skipped, failures = 0, 0, 0, []

# --- the claims store the checker is pointed at -------------------------------------
#
# NOT the real C:\ProgramData\Ladder-AI\claims: these cases plant and mutate claims, and a
# test suite that writes into the store real agents coordinate through would be a test
# suite that causes the collisions it is checking for. LADDER_CLAIMS_DIR is the converter's
# own override and the checker honours the same one, so the whole path under test is the
# real one - only the root moves.
#
# A claim file is planted directly rather than acquired through `converter claim`, because
# an ALLOCATION claim on a number the corpus already uses is (correctly) REFUSED - and the
# state this gate checks is exactly that one: the claim was taken before the block existed,
# and the block exists now. `complete_evidence_verifies` is the control that proves the
# planting matches what the tool reads back; if the format were wrong it fails first.
STORE_ROOT = tempfile.mkdtemp(prefix="ladder-claims-")
EMPTY_ROOT = tempfile.mkdtemp(prefix="ladder-claims-empty-")


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


plant(STORE_ROOT, "block-edit", "FB_PusherControl")
plant(STORE_ROOT, "block-number", BLOCK_NUMBER)
plant(STORE_ROOT, "block-edit", "FC_ControlMain", agent="someone-else/lad-coder")


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


def real_hash():
    raw = subprocess.check_output([CONVERTER, "ir-hash", BLOCK, "--json"], cwd=ROOT)
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


def run(doc_or_text, store=STORE_ROOT):
    """store=None runs with LADDER_CLAIMS_DIR UNSET, i.e. against the real shared root."""
    handle, path = tempfile.mkstemp(suffix=".json")
    os.close(handle)
    with io.open(path, "w", encoding="utf-8") as fh:
        if isinstance(doc_or_text, str):
            fh.write(doc_or_text)
        else:
            fh.write(json.dumps(doc_or_text))
    env = dict(os.environ)
    env.pop("LADDER_CLAIMS_DIR", None)
    if store is not None:
        env["LADDER_CLAIMS_DIR"] = store
    try:
        proc = subprocess.Popen(
            [sys.executable, SCRIPT, path],
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
    assert_in("block numbers joined to IR     : 1", out, "the NUMBER join ran")


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
    assert_in("matches no NUMBER line", out, "the reason")
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

for temp in (STORE_ROOT, EMPTY_ROOT):
    shutil.rmtree(temp, ignore_errors=True)

print("=" * 70)
print("RESULT: %d passed, %d failed, %d skipped" % (passed, failed, skipped))
for f in failures:
    print("  failed: %s" % f)
if skipped:
    print("  NOTE: skipped cases did not run. That is not a pass.")
raise SystemExit(1 if (failed or skipped) else 0)
