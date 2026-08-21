"""Offline test suite for tools/check-agent-evidence.py.

    python tools/check-agent-evidence.tests.py

Same reason as check-claude-md-budget.tests.py: a verifier nobody has watched REFUSE
anything is a verifier nobody should trust. This one matters more than most, because
its whole purpose is to let the orchestrator stop re-reading the sub-agent's work - so
if it waves through a bad hand-back, the check it replaced was better than it is.

Fixtures are built against a real block in ir/test-project001, whose ir-hash is read
once from the Release converter and then deliberately corrupted in one case. Requires
that binary; skips the hash-dependent cases with a loud note if it is absent rather
than reporting green on tests that did not run - "EXAMINED NOTHING" is not a pass here
either.
"""
import io, json, os, subprocess, sys, tempfile

EXIT_OK = 0
EXIT_PROBLEMS = 1
EXIT_NOT_VERIFIED = 2

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "check-agent-evidence.py")
CONVERTER = os.path.join(
    ROOT, "src", "converter", "Converter", "bin", "Release", "net8.0", "converter.exe"
)
BLOCK = "ir/test-project001/FB_PusherControl.ir"

passed, failed, skipped, failures = 0, 0, 0, []


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
        "checks": [
            {"tool": "converter preflight", "exit": 0},
            {"tool": "converter diff --only", "exit": 0},
            {"tool": "openness-cli sanity-check", "exit": 0},
        ],
    }


def run(doc_or_text):
    handle, path = tempfile.mkstemp(suffix=".json")
    os.close(handle)
    with io.open(path, "w", encoding="utf-8") as fh:
        if isinstance(doc_or_text, str):
            fh.write(doc_or_text)
        else:
            fh.write(json.dumps(doc_or_text))
    try:
        proc = subprocess.Popen(
            [sys.executable, SCRIPT, path],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=ROOT,
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


# --- it cannot run -----------------------------------------------------------------

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

print("=" * 70)
print("RESULT: %d passed, %d failed, %d skipped" % (passed, failed, skipped))
for f in failures:
    print("  failed: %s" % f)
if skipped:
    print("  NOTE: skipped cases did not run. That is not a pass.")
raise SystemExit(1 if (failed or skipped) else 0)
