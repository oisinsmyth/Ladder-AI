"""Offline test suite for tools/check-doc-migration.py.

    python tools/check-doc-migration.tests.py

This gate is the only thing standing between a tidy-up and a silently lost fact, so it
is the last place to accept a guard that has never been watched refuse anything. Same
method as check-claude-md-budget.tests.py: each case builds a throwaway git repo, copies
the script under test into it UNMODIFIED, and runs it as a child process. No testability
hooks were added to the script - a gate that can be aimed elsewhere can be aimed away.

Both directions are asserted, and so are the exit-2 cases, because exit 2 means NOTHING
WAS CHECKED and the one failure this gate must never have is reading as clean when it
examined nothing.
"""
import io, os, shutil, subprocess, sys, tempfile

EXIT_OK = 0
EXIT_DROPPED = 1
EXIT_CANNOT_RUN = 2

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "check-doc-migration.py")

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


def write(path, text):
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    with io.open(path, "w", encoding="utf-8") as fh:
        fh.write(text)


def build(tmp, baseline_text):
    """A throwaway repo with `old.md` committed, plus the script under test."""
    quiet = {"stdout": subprocess.PIPE, "stderr": subprocess.PIPE, "cwd": tmp}
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "check-doc-migration.py")
                if os.path.isdir(os.path.join(tmp, "tools"))
                else _mktools(tmp))
    write(os.path.join(tmp, "old.md"), baseline_text)
    subprocess.call(["git", "init"], **quiet)
    subprocess.call(["git", "config", "user.email", "t@t"], **quiet)
    subprocess.call(["git", "config", "user.name", "t"], **quiet)
    subprocess.call(["git", "add", "old.md"], **quiet)
    subprocess.call(["git", "commit", "-m", "baseline"], **quiet)
    return os.path.join(tmp, "tools", "check-doc-migration.py")


def _mktools(tmp):
    os.makedirs(os.path.join(tmp, "tools"))
    return os.path.join(tmp, "tools", "check-doc-migration.py")


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


BASE = u"# old\n\nSee `tagstatus` and `preflight`, plus `--allow-header`.\n"

# --- it passes -------------------------------------------------------------------

def everything_migrated_passes():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        write(os.path.join(tmp, "new.md"),
              u"`tagstatus` moved here. `preflight` too. `--allow-header` as well.\n")
        code, out, _ = run(script, "HEAD", "old.md", "new.md")
        assert_eq(code, EXIT_OK, "exit code")
        assert_in("GATE PASSED", out, "verdict")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def wrapped_destination_still_matches():
    """Destinations wrap at ~100 chars, so a migrated phrase is routinely split."""
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, u"# old\n\nThe `whole project reference graph` matters.\n")
        write(os.path.join(tmp, "new.md"), u"the whole project\nreference graph is here\n")
        code, out, _ = run(script, "HEAD", "old.md", "new.md")
        assert_eq(code, EXIT_OK, "a line-wrapped match must still count")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def prose_is_deliberately_not_checked():
    """Documents the gate's real limit: a fact stated only in prose is out of reach."""
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, u"# old\n\nEMPTY IS NOT CLEAN and exit 2 means nothing ran.\n")
        write(os.path.join(tmp, "new.md"), u"unrelated\n")
        code, _, _ = run(script, "HEAD", "old.md", "new.md")
        assert_eq(code, EXIT_OK, "prose-only content must not gate")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --- it refuses ------------------------------------------------------------------

def dropped_marker_fails():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        write(os.path.join(tmp, "new.md"), u"`tagstatus` survived. Nothing else did.\n")
        code, out, _ = run(script, "HEAD", "old.md", "new.md")
        assert_eq(code, EXIT_DROPPED, "exit code")
        assert_in("UNACCOUNTED FOR", out, "verdict")
        assert_in("preflight", out, "the dropped marker is named")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def failure_names_the_remedy():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        write(os.path.join(tmp, "new.md"), u"nothing here\n")
        _, out, _ = run(script, "HEAD", "old.md", "new.md")
        assert_in("migrate each of these", out, "remedy named")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def allow_file_suppresses_a_fragment():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        write(os.path.join(tmp, "new.md"), u"`tagstatus` `preflight` here\n")
        write(os.path.join(tmp, "frag.txt"), u"# audited\n--allow-header\n")
        code, out, _ = run(script, "HEAD", "old.md", "new.md", "--allow",
                           os.path.join(tmp, "frag.txt"))
        assert_eq(code, EXIT_OK, "an allowed fragment must not gate")
        assert_in("known fragments (not facts)    : 1", out, "fragment counted, not hidden")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --- it cannot run ---------------------------------------------------------------

def bad_baseline_is_exit_2():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        write(os.path.join(tmp, "new.md"), u"anything\n")
        code, out, err = run(script, "nosuchref", "old.md", "new.md")
        assert_eq(code, EXIT_CANNOT_RUN, "exit code")
        assert_in("NOTHING CHECKED", err, "diagnostic")
        assert "GATE PASSED" not in out, "a bad ref must never read as a pass"
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def missing_destination_is_exit_2_not_0():
    """The sharp one. A destination that does not exist contributes an empty haystack,
    so without this check a typo'd path would make every marker 'missing' - or, with one
    good destination, quietly shrink what was searched."""
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        code, out, err = run(script, "HEAD", "old.md", "no-such-file.md")
        assert_eq(code, EXIT_CANNOT_RUN, "exit code")
        assert_in("destination does not exist", err, "diagnostic names the path")
        assert "GATE PASSED" not in out, "a missing destination must never read as a pass"
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def absent_fenced_section_is_exit_2():
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, BASE)
        write(os.path.join(tmp, "new.md"), u"anything\n")
        code, out, err = run(script, "HEAD", "old.md", "new.md",
                             "--fenced-section", "## Nope")
        assert_eq(code, EXIT_CANNOT_RUN, "exit code")
        assert_in("wrong baseline?", err, "diagnostic")
        assert "GATE PASSED" not in out, "an absent section must never read as a pass"
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def fenced_section_narrows_the_scope():
    """Proves --fenced-section actually scopes: a marker OUTSIDE the fence is ignored."""
    tmp = tempfile.mkdtemp()
    try:
        script = build(tmp, u"# old\n\n`outside-the-fence`\n\n## Commands\n\n```\n`inside-fence`\n```\n")
        write(os.path.join(tmp, "new.md"), u"nothing at all\n")
        code, out, _ = run(script, "HEAD", "old.md", "new.md",
                           "--fenced-section", "## Commands")
        assert_eq(code, EXIT_DROPPED, "the in-fence marker must still gate")
        assert_in("inside-fence", out, "in-fence marker reported")
        assert "outside-the-fence" not in out, "out-of-fence marker must be ignored"
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


for name, body in [
    ("everything migrated passes", everything_migrated_passes),
    ("a line-wrapped destination still matches", wrapped_destination_still_matches),
    ("prose is deliberately not checked", prose_is_deliberately_not_checked),
    ("a dropped marker is refused and named", dropped_marker_fails),
    ("the failure names the remedy", failure_names_the_remedy),
    ("an allow-file fragment is counted, not hidden", allow_file_suppresses_a_fragment),
    ("a bad baseline exits 2", bad_baseline_is_exit_2),
    ("a missing destination exits 2, never 0", missing_destination_is_exit_2_not_0),
    ("an absent fenced section exits 2", absent_fenced_section_is_exit_2),
    ("--fenced-section actually narrows the scope", fenced_section_narrows_the_scope),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
