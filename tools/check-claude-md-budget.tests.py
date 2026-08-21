"""Offline test suite for tools/check-claude-md-budget.py.

    python tools/check-claude-md-budget.tests.py

Written because this project produced five guards in one week that were written,
believed, and never executed - the same reason confirm-roundtrip-fence.tests.ps1
exists. A budget gate that has never been shown to REFUSE anything is decoration.

Method: the script under test derives its repo root from __file__, so each case copies
it - unmodified, byte for byte - into a temporary tree beside a fixture CLAUDE.md of a
known size, and runs it as a child process. Nothing is stubbed and no testability hook
was added to the script; a gate with a "point me at a different file" flag can be
aimed away from the file it guards.

Both directions are asserted. A gate that refuses everything looks identical to a
working one from the failing side, so the passing cases carry equal weight. Reason
strings are asserted too, not just exit codes: refusing for the wrong reason is a
different defect from refusing correctly, and the exit code cannot tell them apart.
"""
import os, shutil, subprocess, sys, tempfile

EXIT_OK = 0        # within budget
EXIT_OVER = 1      # over budget - the gate refused
EXIT_CANNOT_RUN = 2  # unreadable / not staged. NOT a pass.

CEILING = 20480

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "check-claude-md-budget.py")

passed, failed, failures = 0, 0, []


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


def build(tmp, content=None, git=False):
    """A minimal repo-shaped tree: <tmp>/tools/<script> and optionally <tmp>/CLAUDE.md."""
    os.makedirs(os.path.join(tmp, "tools"))
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "check-claude-md-budget.py"))
    if content is not None:
        with open(os.path.join(tmp, "CLAUDE.md"), "wb") as handle:
            handle.write(content)
    if git:
        quiet = {"stdout": subprocess.PIPE, "stderr": subprocess.PIPE, "cwd": tmp}
        subprocess.call(["git", "init"], **quiet)
        # Pin autocrlf so the blob is exactly the bytes written, making the
        # CRLF-equivalent arithmetic deterministic rather than machine-dependent.
        subprocess.call(["git", "config", "core.autocrlf", "false"], **quiet)
        if content is not None:
            subprocess.call(["git", "add", "CLAUDE.md"], **quiet)
    return os.path.join(tmp, "tools", "check-claude-md-budget.py")


def run(script, *args):
    proc = subprocess.Popen(
        [sys.executable, script] + list(args),
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    out, err = proc.communicate()
    return proc.returncode, out.decode("utf-8", "replace"), err.decode("utf-8", "replace")


def sized(n):
    """Exactly n bytes, with realistic line structure."""
    body = ("x" * 63 + "\n") * (n // 64)
    return (body + "y" * (n - len(body))).encode("ascii")


def assert_eq(actual, expected, what):
    assert actual == expected, "%s: expected %r, got %r" % (what, expected, actual)


def assert_in(needle, haystack, what):
    assert needle in haystack, "%s: %r not found in:\n%s" % (what, needle, haystack)


# --- the gate permits -------------------------------------------------------------

def under_budget_passes():
    tmp = tempfile.mkdtemp()
    try:
        code, out, _ = run(build(tmp, sized(CEILING - 887)))
        assert_eq(code, EXIT_OK, "exit code")
        assert_in("GATE PASSED", out, "verdict")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def headroom_is_reported():
    """Shrinking room must be visible before it becomes a failure."""
    tmp = tempfile.mkdtemp()
    try:
        _, out, _ = run(build(tmp, sized(CEILING - 42)))
        assert_in("headroom                       : 42", out, "headroom line")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def exactly_at_ceiling_passes():
    """The ceiling is inclusive. A file of exactly 20,480 bytes is within budget."""
    tmp = tempfile.mkdtemp()
    try:
        code, out, _ = run(build(tmp, sized(CEILING)))
        assert_eq(code, EXIT_OK, "exit code at exactly the ceiling")
        assert_in("headroom                       : 0", out, "zero headroom")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --- the gate refuses -------------------------------------------------------------

def one_byte_over_fails():
    tmp = tempfile.mkdtemp()
    try:
        code, out, _ = run(build(tmp, sized(CEILING + 1)))
        assert_eq(code, EXIT_OVER, "exit code one byte over")
        assert_in("GATE FAILED", out, "verdict")
        assert_in("OVER BUDGET BY                 : 1", out, "overage amount")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def failure_names_the_rule():
    """Refusing for the wrong reason is a different defect. The message must say where
    the fact should go, or the cheapest way to pass the gate is deleting a hard rule."""
    tmp = tempfile.mkdtemp()
    try:
        _, out, _ = run(build(tmp, sized(CEILING + 500)))
        assert_in("README or docs/notes", out, "remedy")
        assert_in("Do not fix this by deleting", out, "the wrong-fix warning")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --- the gate cannot run ----------------------------------------------------------

def missing_file_is_exit_2_not_0():
    """EMPTY IS NOT CLEAN. A gate that cannot find its target has examined nothing,
    and must not report that as within budget."""
    tmp = tempfile.mkdtemp()
    try:
        code, out, err = run(build(tmp, content=None))
        assert_eq(code, EXIT_CANNOT_RUN, "exit code with no CLAUDE.md")
        assert_in("cannot read", err, "diagnostic")
        assert "GATE PASSED" not in out, "a missing target must never read as a pass"
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def unstaged_is_exit_2_not_0():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, sized(100), git=False)
        code, out, _ = run(script, "--staged")
        assert_eq(code, EXIT_CANNOT_RUN, "exit code for --staged outside an index")
        assert "GATE PASSED" not in out, "an unreadable index must never read as a pass"
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --- the CRLF-equivalent arithmetic ------------------------------------------------

def staged_blob_reports_worktree_bytes():
    """The subtle one. git stores LF; the project's whole record is in CRLF bytes.
    --staged must add the newlines back, or the gate and the commit log disagree."""
    tmp = tempfile.mkdtemp()
    try:
        content = sized(4096)
        expected = len(content) + content.count(b"\n")
        script = build(tmp, content, git=True)
        code, out, _ = run(script, "--staged")
        assert_eq(code, EXIT_OK, "exit code")
        assert_in("bytes                          : %d" % expected, out, "CRLF-equivalent size")
        assert_in("staged blob, CRLF-equivalent", out, "measurement mode is stated")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def crlf_blob_is_not_inflated_twice():
    """If a clone has autocrlf off, the blob already carries CRLF. Adding newline
    count again would invent bytes and fail a file that is genuinely within budget."""
    tmp = tempfile.mkdtemp()
    try:
        content = b"line one\r\nline two\r\n"
        script = build(tmp, content, git=True)
        code, out, _ = run(script, "--staged")
        assert_eq(code, EXIT_OK, "exit code")
        assert_in("bytes                          : %d" % len(content), out, "no double count")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


for name, body in [
    ("under budget passes", under_budget_passes),
    ("headroom is reported on success", headroom_is_reported),
    ("exactly at the ceiling passes", exactly_at_ceiling_passes),
    ("one byte over is refused", one_byte_over_fails),
    ("failure names the rule, not just the size", failure_names_the_rule),
    ("missing CLAUDE.md exits 2, never 0", missing_file_is_exit_2_not_0),
    ("--staged outside an index exits 2, never 0", unstaged_is_exit_2_not_0),
    ("staged LF blob reports CRLF-equivalent bytes", staged_blob_reports_worktree_bytes),
    ("staged CRLF blob is not inflated twice", crlf_blob_is_not_inflated_twice),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
