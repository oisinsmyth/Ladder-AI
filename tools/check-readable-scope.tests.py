#!/usr/bin/env python3
"""Self-tests for check-readable-scope.py (M-23, the buildable half).

    python tools/check-readable-scope.tests.py

THE CASE THAT MATTERS IS `the_HISTORICAL_VECTOR_is_caught`. It reconstructs the AB-2 shape - a
byte-identical copy of a quarantined key sitting in an ignored scratch directory, OUTSIDE the
allowed set - and asserts this tool gates on it. The design M-23 was filed with would NOT have: its
clause was "no quarantine twin inside the ALLOWED SET", and the historical copy was never in the
allowed set. That case is the reason the implemented clause is wider, and it is pinned here so the
narrow version cannot come back.

EVERY CASE OWNS A THROWAWAY REPOSITORY, because tracked-ness is one of the things checked.

Nothing here is a real key. The fixtures are invented text a few bytes long.
"""
import io
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "check-readable-scope.py")

EXIT_OK = 0
EXIT_FOUND = 1
EXIT_CANNOT_RUN = 2

passed = 0
failed = 0
failures = []


def case(name, body):
    global passed, failed
    root = tempfile.mkdtemp(prefix="scope-test-")
    try:
        body(root)
        passed += 1
        print("  ok    %s" % name)
    except AssertionError as exc:
        failed += 1
        failures.append(name)
        print("  FAIL  %s\n            %s" % (name, exc))
    finally:
        shutil.rmtree(root, ignore_errors=True)


def write(root, rel_path, text):
    full = os.path.join(root, rel_path.replace("/", os.sep))
    os.makedirs(os.path.dirname(full), exist_ok=True)
    io.open(full, "w", encoding="utf-8", newline="\n").write(text)


def git(root, *args):
    subprocess.run(("git",) + args, cwd=root, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, check=True)


def build(root, tracked=None, untracked=None, register=""):
    git(root, "init", "-q")
    git(root, "config", "user.email", "t@example.invalid")
    git(root, "config", "user.name", "t")
    for rel_path, text in (tracked or {}).items():
        write(root, rel_path, text)
    if tracked:
        git(root, "add", "-A")
        git(root, "commit", "-qm", "fixture")
    for rel_path, text in (untracked or {}).items():
        write(root, rel_path, text)
    write(root, "tools/readable-scope.txt", register)


def run(root, *extra):
    out = subprocess.run([sys.executable, SCRIPT, "--repo", root] + list(extra),
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    return out.returncode, out.stdout.decode("utf-8", "replace")


def assert_eq(actual, expected, what):
    assert actual == expected, "%s: expected %r, got %r" % (what, expected, actual)


# --- the clause with teeth ------------------------------------------------------------------------

def the_HISTORICAL_VECTOR_is_caught(root):
    """AB-2's actual shape: a twin of the key in an IGNORED directory, OUTSIDE the allow set.

    The filed M-23 clause - "no twin inside the allowed set" - passes this. That is why the
    implemented clause is wider, and why this case exists."""
    build(root,
          tracked={"case/spec.md": "the spec", "case/source/base.ir": "BASE"},
          untracked={"keys/key.ir": "THE ANSWER KEY CONTENT",
                     "scratch/copy-under-another-name.ir": "THE ANSWER KEY CONTENT"},
          register=("c | allow | case/source/base.ir   # the baseline\n"
                    "c | quarantine | keys/key.ir      # the key\n"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "scratch/copy-under-another-name.ir" in out, "the twin must be NAMED"
    assert "INSIDE THE ALLOW SET" not in out, \
        "this twin is OUTSIDE the allow set - the filed clause would have missed it entirely"


def a_twin_INSIDE_the_allow_set_is_marked(root):
    """The filed clause's case. Still checked - it is simply not the one that works."""
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          untracked={"keys/key.ir": "KEYBYTES", "case/source/sneaky.ir": "KEYBYTES"},
          register=("c | allow | case/source   # the readable directory\n"
                    "c | quarantine | keys/key.ir  # the key\n"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "INSIDE THE ALLOW SET" in out, "a twin inside the allow set must be called out"


def an_UNTWINNED_quarantine_is_clean(root):
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          untracked={"keys/key.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | quarantine | keys/key.ir     # the key, and it is alone\n"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "unique" in out, "an intact fence should say so"


# --- a tracked quarantine is a false assertion ----------------------------------------------------

def a_TRACKED_quarantine_GATES(root):
    build(root,
          tracked={"case/source/base.ir": "BASE", "keys/key.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | quarantine | keys/key.ir     # asserted hidden, but it is committed\n"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "TRACKED" in out and "VOID" in out, "a tracked fence is void and must gate"


def a_VOID_declaration_reports_without_gating(root):
    """`void` is a confession. It must not gate - and it must cost something permanent."""
    build(root,
          tracked={"case/source/base.ir": "BASE", "keys/key.ir": "KEYBYTES"},
          untracked={"scratch/twin.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | void | keys/key.ir           # tracked corpus; the fence never existed\n"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "CAN NEVER BE CLEARED" in out, "a void fence must cost a permanent printed statement"
    assert "scratch/twin.ir" in out, "the twin is still REPORTED even though it does not gate"


def VOID_is_not_a_quiet_escape(root):
    """Downgrading to void must be louder than the failure it replaces, not quieter."""
    build(root,
          tracked={"case/source/base.ir": "BASE", "keys/key.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | void | keys/key.ir           # confessed\n"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "CONFESSION, not an exemption" in out, "the word must appear in the output, not just docs"


# --- coherence ------------------------------------------------------------------------------------

def a_path_declared_BOTH_ways_GATES(root):
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          register=("c | allow | case/source/base.ir      # readable\n"
                    "c | quarantine | case/source/base.ir # and also hidden?\n"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "CONTRADICTION" in out, "a path cannot be both"


def a_MISSING_declared_path_GATES(root):
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          register="c | allow | case/source/gone.ir   # a rule about a file that is not there\n")
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "MISSING" in out, "a rule about an absent file fences nothing"


# --- the run half ---------------------------------------------------------------------------------

def NO_manifest_says_it_examined_NOTHING(root):
    """The honest half. It must never read as a pass for the run."""
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          untracked={"keys/key.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | quarantine | keys/key.ir     # the key\n"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "EXAMINED NOTHING" in out, "the run half must declare that it checked nothing"
    assert "NOT A BLINDNESS CLAIM" in out, "the verdict must refuse the stronger reading"


def a_manifest_OUTSIDE_the_allow_set_GATES(root):
    build(root,
          tracked={"case/source/base.ir": "BASE", "elsewhere/other.ir": "OTHER"},
          untracked={"keys/key.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | quarantine | keys/key.ir     # the key\n"))
    manifest = os.path.join(root, "manifest.txt")
    io.open(manifest, "w", encoding="utf-8", newline="\n").write(
        "case/source/base.ir\nelsewhere/other.ir\n")
    code, out = run(root, "--manifest", manifest)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "elsewhere/other.ir" in out and "OUTSIDE" in out, out[-400:]


def a_manifest_INSIDE_the_allow_set_passes(root):
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          untracked={"keys/key.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | quarantine | keys/key.ir     # the key\n"))
    manifest = os.path.join(root, "manifest.txt")
    io.open(manifest, "w", encoding="utf-8", newline="\n").write("case/source/base.ir\n")
    code, out = run(root, "--manifest", manifest)
    assert_eq(code, EXIT_OK, "exit")
    assert "0 outside" in out, out[-300:]


# --- EMPTY IS NOT CLEAN ---------------------------------------------------------------------------

def an_ABSENT_register_is_NOTHING_EXAMINED(root):
    git(root, "init", "-q")
    code, out = run(root)
    assert_eq(code, EXIT_CANNOT_RUN, "exit")
    assert "EMPTY IS NOT CLEAN" in out, out


def an_EMPTY_register_is_NOTHING_EXAMINED(root):
    build(root, tracked={"a.txt": "a"}, register="# only comments\n")
    code, out = run(root)
    assert_eq(code, EXIT_CANNOT_RUN, "exit")
    assert "declares no case" in out, out


def a_MALFORMED_row_is_REFUSED(root):
    build(root, tracked={"a.txt": "a"}, register="c | allow\n")
    code, out = run(root)
    assert code != EXIT_OK, "a malformed register must never pass"
    assert "expected 'case | allow|quarantine | path'" in out, out


def an_UNKNOWN_kind_is_REFUSED(root):
    build(root, tracked={"a.txt": "a"}, register="c | maybe | a.txt  # ?\n")
    code, out = run(root)
    assert code != EXIT_OK, "an unknown kind must never pass"
    assert "kind must be one of" in out, out


def LIVE_RUNS_is_never_walked(root):
    build(root,
          tracked={"case/source/base.ir": "BASE"},
          untracked={"keys/key.ir": "KEYBYTES", "Live Runs/J9999/copy.ir": "KEYBYTES"},
          register=("c | allow | case/source/base.ir  # the baseline\n"
                    "c | quarantine | keys/key.ir     # the key\n"))
    code, out = run(root)
    assert "J9999" not in out, "a live-run path must never reach this output"
    assert_eq(code, EXIT_OK, "exit")


print("check-readable-scope.py self-tests (M-23)")
print("=" * 70)
for name, body in [
    ("TEETH: the HISTORICAL vector (twin outside the allow set) is caught",
     the_HISTORICAL_VECTOR_is_caught),
    ("TEETH: a twin INSIDE the allow set is marked as such", a_twin_INSIDE_the_allow_set_is_marked),
    ("TEETH: an untwinned quarantine is clean", an_UNTWINNED_quarantine_is_clean),
    ("VOID: a TRACKED asserted quarantine GATES", a_TRACKED_quarantine_GATES),
    ("VOID: a void declaration reports without gating", a_VOID_declaration_reports_without_gating),
    ("VOID: void is not a quiet escape", VOID_is_not_a_quiet_escape),
    ("COHERENCE: a path declared BOTH ways GATES", a_path_declared_BOTH_ways_GATES),
    ("COHERENCE: a MISSING declared path GATES", a_MISSING_declared_path_GATES),
    ("RUN: no manifest says it EXAMINED NOTHING", NO_manifest_says_it_examined_NOTHING),
    ("RUN: a manifest outside the allow set GATES", a_manifest_OUTSIDE_the_allow_set_GATES),
    ("RUN: a manifest inside the allow set passes", a_manifest_INSIDE_the_allow_set_passes),
    ("CANNOT RUN: an ABSENT register is NOTHING EXAMINED", an_ABSENT_register_is_NOTHING_EXAMINED),
    ("CANNOT RUN: an EMPTY register is NOTHING EXAMINED", an_EMPTY_register_is_NOTHING_EXAMINED),
    ("REGISTER: a MALFORMED row is refused", a_MALFORMED_row_is_REFUSED),
    ("REGISTER: an UNKNOWN kind is refused", an_UNKNOWN_kind_is_REFUSED),
    ("HYGIENE: Live Runs/ is never walked", LIVE_RUNS_is_never_walked),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
