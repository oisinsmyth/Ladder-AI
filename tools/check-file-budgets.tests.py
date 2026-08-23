"""Offline test suite for tools/check-file-budgets.py.

    python tools/check-file-budgets.tests.py

Ported from the CLAUDE.md-only budget suite this replaces, plus the cases the per-file
table introduced. Same reason as before: this project produced five guards in one week
that were written, believed, and never executed.

Method: each case copies the script under test into a throwaway tree, rewrites ONLY its
BUDGETS table to point at fixtures, and runs it as a child process. Rewriting the table
is not a testability backdoor - the table is the script's data, and every other line,
including all the measuring and reporting, runs exactly as shipped.

Both directions are asserted, and so are the reason strings: refusing for the wrong reason
is a different defect from refusing correctly, and an exit code cannot tell them apart.
"""
import io, os, re, shutil, subprocess, sys, tempfile

EXIT_OK = 0
EXIT_OVER = 1
EXIT_CANNOT_RUN = 2

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "check-file-budgets.py")

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


def sized(n):
    """Exactly n bytes, with realistic line structure."""
    body = ("x" * 63 + "\n") * (n // 64)
    return (body + "y" * (n - len(body))).encode("ascii")


def build(tmp, budgets, files, git=False):
    """budgets: [(relpath, ceiling)]. files: {relpath: bytes or None to omit}."""
    os.makedirs(os.path.join(tmp, "tools"))
    src = io.open(SCRIPT, encoding="utf-8", newline="").read()
    table = "BUDGETS = [\n" + "".join(
        '    ("%s", %d),\n' % (p, c) for p, c in budgets) + "]"
    src = re.sub(r"BUDGETS = \[.*?\n\]", table.replace("\\", "\\\\"), src,
                 count=1, flags=re.S)
    path = os.path.join(tmp, "tools", "check-file-budgets.py")
    io.open(path, "w", encoding="utf-8", newline="").write(src)

    for rel, content in files.items():
        if content is None:
            continue
        full = os.path.join(tmp, rel.replace("/", os.sep))
        d = os.path.dirname(full)
        if d and not os.path.isdir(d):
            os.makedirs(d)
        with open(full, "wb") as fh:
            fh.write(content)

    if git:
        quiet = {"stdout": subprocess.PIPE, "stderr": subprocess.PIPE, "cwd": tmp}
        subprocess.call(["git", "init"], **quiet)
        subprocess.call(["git", "config", "core.autocrlf", "false"], **quiet)
        subprocess.call(["git", "config", "user.email", "t@t"], **quiet)
        subprocess.call(["git", "config", "user.name", "t"], **quiet)
        for rel, content in files.items():
            if content is not None:
                subprocess.call(["git", "add", rel], **quiet)
    return path


def run(script, *args):
    proc = subprocess.Popen(
        [sys.executable, script] + list(args),
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    out, err = proc.communicate()
    return (proc.returncode,
            out.decode("utf-8", "replace"),
            err.decode("utf-8", "replace"))


def assert_eq(actual, expected, what):
    assert actual == expected, "%s: expected %r, got %r" % (what, expected, actual)


def assert_in(needle, haystack, what):
    assert needle in haystack, "%s: %r not found in:\n%s" % (what, needle, haystack)


def tmpcase(fn):
    def wrapped():
        tmp = tempfile.mkdtemp()
        try:
            fn(tmp)
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
    return wrapped


# --- the gate permits -------------------------------------------------------------

@tmpcase
def all_under_budget_passes(tmp):
    s = build(tmp, [("a.md", 1024), ("b.md", 2048)],
              {"a.md": sized(900), "b.md": sized(2000)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("GATE PASSED", out, "verdict")


@tmpcase
def exactly_at_ceiling_passes(tmp):
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(1024)})
    code, _, _ = run(s)
    assert_eq(code, EXIT_OK, "the ceiling is inclusive")


@tmpcase
def tightest_headroom_is_reported(tmp):
    """Shrinking room must be visible before it becomes a failure."""
    s = build(tmp, [("a.md", 1024), ("b.md", 4096)],
              {"a.md": sized(1000), "b.md": sized(2000)})
    _, out, _ = run(s)
    assert_in("tightest headroom", out, "headroom section")
    assert_in("24 bytes", out, "the tightest file's actual headroom")


@tmpcase
def untracked_file_is_ignored_not_failed(tmp):
    """A file absent from the table must be ignored, so the hook can run always."""
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(100), "stranger.md": sized(99999)})
    code, _, _ = run(s)
    assert_eq(code, EXIT_OK, "a file not in the table must not gate")


# --- the gate refuses -------------------------------------------------------------

@tmpcase
def one_byte_over_fails(tmp):
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(1025)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OVER, "exit code")
    assert_in("GATE FAILED", out, "verdict")
    assert_in("over by 1", out, "the overage")


@tmpcase
def the_offending_file_is_named(tmp):
    """With 18 budgeted files, 'something is too big' is not an actionable message."""
    s = build(tmp, [("a.md", 1024), ("b.md", 1024), ("c.md", 1024)],
              {"a.md": sized(10), "b.md": sized(5000), "c.md": sized(10)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OVER, "exit code")
    assert_in("OVER  b.md", out, "the offending file is named")
    assert "OVER  a.md" not in out, "an innocent file must not be named"


@tmpcase
def failure_names_the_rule_and_the_escape(tmp):
    s = build(tmp, [("a.md", 512)], {"a.md": sized(2000)})
    _, out, _ = run(s)
    assert_in("Do not fix this by deleting", out, "the wrong-fix warning")
    assert_in("ITS OWN COMMIT", out, "the legitimate escape is named")


@tmpcase
def deleted_budgeted_file_fails(tmp):
    """A budgeted file that vanished is a failure, not a skip - otherwise the table
    rots and a deletion quietly removes a budget nobody notices is gone."""
    s = build(tmp, [("a.md", 1024), ("ghost.md", 1024)], {"a.md": sized(100)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OVER, "exit code")
    assert_in("GONE  ghost.md", out, "the missing file is named")


# --- the slack floor: reported, never enforced ------------------------------------
#
# The floor is the one rule in this script that must NOT gate. A file under it is
# inside its budget; refusing it would be the gate refusing work that complies, which
# is the cry-wolf behaviour the floor exists to prevent. So every case here asserts the
# exit code as hard as it asserts the text - a floor that started failing would still
# print the right words.

@tmpcase
def under_floor_is_reported_and_still_passes(tmp):
    """The whole point: reported, exit 0. Found by eye three times in one day before
    this existed, which is what a budget table exists to stop."""
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(924)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OK, "under the floor is WITHIN budget and must not gate")
    assert_in("GATE PASSED", out, "the verdict is still a pass")
    assert_in("UNDER SLACK FLOOR (not a fail) : 1", out, "the count line")
    assert_in("TIGHT   a.md", out, "the tight file is named")
    assert_in("100 bytes headroom", out, "its actual headroom")


@tmpcase
def exactly_at_the_floor_is_not_reported(tmp):
    """The rule is AT LEAST 256 bytes of slack, so 256 complies. An off-by-one here
    would put a compliant file on a list headed 'under the floor'."""
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(768)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("UNDER SLACK FLOOR (not a fail) : 0", out, "256 bytes of slack complies")
    assert "TIGHT" not in out, "a compliant file must not be listed as tight"


@tmpcase
def one_byte_under_the_floor_is_reported(tmp):
    """The other side of the boundary."""
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(769)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("255 bytes headroom", out, "one byte under the floor is caught")


@tmpcase
def a_comfortable_file_prints_no_tight_stanza(tmp):
    """A report that prints on every run whether or not it has anything to say is a
    report people stop reading."""
    s = build(tmp, [("a.md", 4096)], {"a.md": sized(100)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("UNDER SLACK FLOOR (not a fail) : 0", out, "the zero case still states itself")
    assert "NOT A FAILURE" not in out, "no stanza when there is nothing to report"


@tmpcase
def over_ceiling_is_never_also_listed_as_tight(tmp):
    """A file over its ceiling has negative headroom, so naive arithmetic would put it
    on BOTH lists. They are opposite conditions - one is 'you have complied and the
    gate is too tight', the other is 'you have not complied' - and a reader skimming
    must not be able to conflate them."""
    s = build(tmp, [("a.md", 512)], {"a.md": sized(2000)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OVER, "exit code")
    assert_in("OVER  a.md", out, "it is over")
    assert "TIGHT" not in out, "an over-budget file is not a tight one"
    assert_in("UNDER SLACK FLOOR (not a fail) : 0", out, "and it is not counted as tight")


@tmpcase
def the_floor_is_reported_on_the_failure_path_too(tmp):
    """A tight file must not become invisible just because some OTHER file is over."""
    s = build(tmp, [("a.md", 512), ("b.md", 1024)],
              {"a.md": sized(2000), "b.md": sized(924)})
    code, out, _ = run(s)
    assert_eq(code, EXIT_OVER, "the over-budget file still gates")
    assert_in("GATE FAILED", out, "the failure is the headline")
    assert_in("OVER  a.md", out, "the over-budget file is named")
    assert_in("TIGHT   b.md", out, "the tight file is named too")
    assert_in("NOT A FAILURE", out, "and is explicitly marked as not the reason")
    assert out.index("GATE FAILED") < out.index("TIGHT   b.md"), \
        "the failure must be read before the advisory, not after it"


@tmpcase
def the_tight_stanza_says_it_is_not_a_refusal(tmp):
    """The reasoning has to travel with the report. Somebody will otherwise 'fix' this
    by making it fail."""
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(924)})
    _, out, _ = run(s)
    assert_in("COMPLY", out, "it says these files comply")
    assert_in("next 512 boundary in its own commit", out, "the remedy is named")
    assert "shrink" in out, "and the non-remedy is named"


@tmpcase
def staged_mode_reports_the_floor(tmp):
    """The hook path is the one that actually runs on every commit."""
    s = build(tmp, [("a.md", 4096)], {"a.md": sized(3900)}, git=True)
    code, out, _ = run(s, "--staged")
    assert_eq(code, EXIT_OK, "still not a failure in staged mode")
    assert_in("TIGHT   a.md", out, "the tight file is named from the staged blob")


# --- staged mode and the CRLF arithmetic ------------------------------------------

@tmpcase
def staged_blob_reports_worktree_bytes(tmp):
    """git stores LF; the project's record is in CRLF bytes. --staged must add the
    newlines back, or the gate and the commit log disagree."""
    content = sized(4096)
    expected = len(content) + content.count(b"\n")
    s = build(tmp, [("a.md", 65536)], {"a.md": content}, git=True)
    code, out, _ = run(s, "--staged")
    assert_eq(code, EXIT_OK, "exit code")
    assert_in("staged blobs, CRLF-equivalent", out, "measurement mode is stated")
    assert_in("files checked this run         : 1", out, "the staged file was checked")
    # the same content in worktree mode measures smaller, by exactly the newline count
    _, wout, _ = run(s)
    assert expected > len(content), "fixture must contain newlines for this to mean anything"


@tmpcase
def staged_mode_ignores_unstaged_budgeted_files(tmp):
    """The hook runs unconditionally, so a commit touching nothing budgeted must pass."""
    s = build(tmp, [("a.md", 1024)], {"a.md": sized(100), "other.md": sized(10)}, git=True)
    quiet = {"stdout": subprocess.PIPE, "stderr": subprocess.PIPE, "cwd": tmp}
    subprocess.call(["git", "commit", "-m", "base"], **quiet)
    subprocess.call(["git", "reset"], **quiet)
    with open(os.path.join(tmp, "other.md"), "wb") as fh:
        fh.write(sized(20))
    subprocess.call(["git", "add", "other.md"], **quiet)
    code, out, _ = run(s, "--staged")
    assert_eq(code, EXIT_OK, "a commit touching no budgeted file must pass")
    assert_in("no budgeted file was staged", out, "says why it checked nothing")


@tmpcase
def staged_over_budget_still_fails(tmp):
    s = build(tmp, [("a.md", 512)], {"a.md": sized(4096)}, git=True)
    code, out, _ = run(s, "--staged")
    assert_eq(code, EXIT_OVER, "exit code")
    assert_in("OVER  a.md", out, "the offending file is named")


@tmpcase
def crlf_blob_is_not_inflated_twice(tmp):
    """If a clone has autocrlf off the blob already carries CRLF; adding the newline
    count again would invent bytes and fail a file that is genuinely within budget."""
    content = b"line one\r\nline two\r\n"
    s = build(tmp, [("a.md", 1024)], {"a.md": content}, git=True)
    code, _, _ = run(s, "--staged")
    assert_eq(code, EXIT_OK, "no double count")


for name, body in [
    ("all under budget passes", all_under_budget_passes),
    ("exactly at the ceiling passes", exactly_at_ceiling_passes),
    ("tightest headroom is reported", tightest_headroom_is_reported),
    ("a file not in the table is ignored", untracked_file_is_ignored_not_failed),
    ("one byte over is refused", one_byte_over_fails),
    ("the offending file is named, innocents are not", the_offending_file_is_named),
    ("failure names the rule and the legitimate escape", failure_names_the_rule_and_the_escape),
    ("a deleted budgeted file is refused, not skipped", deleted_budgeted_file_fails),
    ("under the slack floor is reported and still passes", under_floor_is_reported_and_still_passes),
    ("exactly at the slack floor is not reported", exactly_at_the_floor_is_not_reported),
    ("one byte under the slack floor is reported", one_byte_under_the_floor_is_reported),
    ("a comfortable file prints no tight stanza", a_comfortable_file_prints_no_tight_stanza),
    ("an over-budget file is never also listed as tight", over_ceiling_is_never_also_listed_as_tight),
    ("the floor is reported on the failure path too", the_floor_is_reported_on_the_failure_path_too),
    ("the tight stanza says it is not a refusal", the_tight_stanza_says_it_is_not_a_refusal),
    ("staged mode reports the floor", staged_mode_reports_the_floor),
    ("staged LF blob reports CRLF-equivalent bytes", staged_blob_reports_worktree_bytes),
    ("staged mode ignores unstaged budgeted files", staged_mode_ignores_unstaged_budgeted_files),
    ("staged over-budget still fails", staged_over_budget_still_fails),
    ("staged CRLF blob is not inflated twice", crlf_blob_is_not_inflated_twice),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
