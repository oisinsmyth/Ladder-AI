#!/usr/bin/env python3
"""Self-tests for check-answer-keys.py (3-C).

    python tools/check-answer-keys.tests.py

EVERY CASE OWNS A THROWAWAY REPOSITORY. Two of the three checks are questions about the TRACKED
set - is this path tracked, is any other tracked path the same blob - and there is no honest way to
fake that without a repository.

NOTHING HERE IS A REAL ANSWER KEY. The fixtures are invented text files a few bytes long. The tool
never reads content, only hashes, so a real one would test nothing a fake one does not.

THE SIZE FILTER IS TESTED ON PURPOSE. The working-tree sweep hashes only files whose SIZE matches a
declared key, which is the optimisation that makes it cheap enough to run every time - and an
optimisation nobody tests is a silent blind spot. A same-size-different-content file is a case here.
"""
import hashlib
import io
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "check-answer-keys.py")

EXIT_OK = 0
EXIT_FOUND = 1
EXIT_CANNOT_RUN = 2

passed = 0
failed = 0
failures = []


def case(name, body):
    global passed, failed
    root = tempfile.mkdtemp(prefix="ak-test-")
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
    return full


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def git(root, *args):
    subprocess.run(("git",) + args, cwd=root, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, check=True)


def build(root, tracked=None, untracked=None, register=None):
    """A repo with `tracked` committed, `untracked` on disk only, and a register."""
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
    if register is not None:
        write(root, "tools/answer-keys.txt", register)


def run(root):
    out = subprocess.run([sys.executable, SCRIPT, "--repo", root],
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    return out.returncode, out.stdout.decode("utf-8", "replace")


def assert_eq(actual, expected, what):
    assert actual == expected, "%s: expected %r, got %r" % (what, expected, actual)


# --- THE PIN ------------------------------------------------------------------------------------

def a_key_AT_its_pin_is_CLEAN(root):
    build(root,
          tracked={"keys/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "CLEAN" in out, "should say CLEAN"


def a_key_THAT_MOVED_off_its_pin_GATES(root):
    build(root,
          tracked={"keys/A.ir": "alpha EDITED"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "MOVED" in out, "should report MOVED"
    assert "GROUND TRUTH HAS MOVED" in out, "should name the consequence"


def a_TRACKED_key_that_is_GONE_gates(root):
    build(root,
          tracked={"keys/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    os.remove(os.path.join(root, "keys", "A.ir"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "GONE" in out, "should report GONE"


def an_UNTRACKED_key_that_is_ABSENT_does_NOT_gate(root):
    """A gitignored key is simply not in a fresh clone. That is expected, not a finding."""
    build(root,
          tracked={"keys/A.ir": "alpha"},
          register=("case-a | keys/A.ir | %s   # tracked\n"
                    "case-b | ignored/B.xml | %s   # untracked and absent here\n"
                    % (sha("alpha"), sha("beta"))))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "absent" in out, "should report it as absent"
    assert "not a finding" in out, "should say plainly that it is not a finding"


def an_UNTRACKED_key_that_is_PRESENT_is_still_pinned(root):
    build(root,
          tracked={"keys/A.ir": "alpha"},
          untracked={"ignored/B.xml": "beta EDITED"},
          register=("case-a | keys/A.ir | %s   # tracked\n"
                    "case-b | ignored/B.xml | %s   # untracked but present\n"
                    % (sha("alpha"), sha("beta"))))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "MOVED" in out, "an untracked key that IS here still gets checked"


# --- UNIQUE IN THE REPOSITORY ---------------------------------------------------------------------

def a_SECOND_TRACKED_copy_GATES(root):
    build(root,
          tracked={"keys/A.ir": "alpha", "corpus/A-copy.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_FOUND, "exit")
    assert "DUPLICATE" in out, "should report DUPLICATE"
    assert "corpus/A-copy.ir" in out, "should name the twin - a path is not a leak"


def an_UNTRACKED_copy_does_NOT_gate(root):
    """The finding AB-2 turned on: a gitignore does not remove a file from the tree. It is
    REPORTED so it is visible, and not gated because deleting it would not stop the next one."""
    build(root,
          tracked={"keys/A.ir": "alpha"},
          untracked={"scratch/A-copy.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "scratch/A-copy.ir" in out, "the readable copy must be PRINTED even though it passes"
    assert "GITIGNORE DOES NOT REMOVE A FILE FROM THE TREE" in out, "should state why it passes"


def a_WORKTREE_checkout_is_counted_SEPARATELY(root):
    """22 of these existed in the real repository. Listed one per line they would bury the
    finding, so they are bucketed - but never dropped."""
    build(root,
          tracked={"keys/A.ir": "alpha"},
          untracked={".claude/worktrees/wt1/keys/A.ir": "alpha",
                     ".claude/worktrees/wt2/keys/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "2 worktree checkout(s)" in out, "should bucket the checkouts"
    assert "0 working" in out, "a checkout is not a working copy"


def a_SAME_SIZE_DIFFERENT_CONTENT_file_is_NOT_a_copy(root):
    """The sweep filters by size first. This is that optimisation's blind-spot test."""
    build(root,
          tracked={"keys/A.ir": "alpha"},
          untracked={"scratch/decoy.ir": "ALPHA"},          # same length, different bytes
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "scratch/decoy.ir" not in out, "a same-size file is not a copy"
    assert "0 copy/copies" in out, "should find no copies at all"


def LIVE_RUNS_is_NOT_walked(root):
    """Its paths are vocabulary and this output gets pasted into notes."""
    build(root,
          tracked={"keys/A.ir": "alpha"},
          untracked={"Live Runs/J9999/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # the reason\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "J9999" not in out, "a live-run path must never reach this output"
    assert "0 copy/copies" in out, "and it must not be counted either"


# --- EMPTY IS NOT CLEAN ---------------------------------------------------------------------------

def an_ABSENT_register_is_NOTHING_EXAMINED(root):
    build(root, tracked={"keys/A.ir": "alpha"})
    code, out = run(root)
    assert_eq(code, EXIT_CANNOT_RUN, "exit")
    assert "EMPTY IS NOT CLEAN" in out, "should say so in those words"


def an_EMPTY_register_is_NOTHING_EXAMINED(root):
    build(root, tracked={"keys/A.ir": "alpha"},
          register="# only comments, no entries\n")
    code, out = run(root)
    assert_eq(code, EXIT_CANNOT_RUN, "exit")
    assert "declares no key" in out, "should say the register is empty"


def NOT_A_GIT_REPO_is_NOTHING_EXAMINED(root):
    write(root, "keys/A.ir", "alpha")
    write(root, "tools/answer-keys.txt", "case-a | keys/A.ir | %s   # r\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_CANNOT_RUN, "exit")
    assert "not a git repository" in out, "should say why it cannot run"


# --- the register itself ---------------------------------------------------------------------------

def a_MALFORMED_line_is_REFUSED_not_skipped(root):
    """Skipping an unparseable entry is how a key quietly stops being checked."""
    build(root, tracked={"keys/A.ir": "alpha"},
          register="case-a | keys/A.ir\n")
    code, out = run(root)
    assert code != EXIT_OK, "a malformed register must never pass"
    assert "expected 'case | path | sha256'" in out, "should name the format"


def the_REASONS_are_printed_every_run(root):
    build(root, tracked={"keys/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # BECAUSE THE BENCH CITES IT\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "BECAUSE THE BENCH CITES IT" in out, "a reason nobody reads is not a reason"


def a_MISSING_reason_is_called_out(root):
    build(root, tracked={"keys/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")                       # reported to the reader, not gated
    assert "NO REASON GIVEN" in out, "an entry with no reason should say so"


def the_verdict_REFUSES_to_claim_blindness(root):
    """The one sentence that keeps this tool honest about what it is not."""
    build(root, tracked={"keys/A.ir": "alpha"},
          register="case-a | keys/A.ir | %s   # r\n" % sha("alpha"))
    code, out = run(root)
    assert_eq(code, EXIT_OK, "exit")
    assert "NOT BLINDNESS" in out, "a clean run must not read as a blindness claim"


print("check-answer-keys.py self-tests")
print("=" * 70)
for name, body in [
    ("PIN: a key at its pin is CLEAN", a_key_AT_its_pin_is_CLEAN),
    ("PIN: a key that MOVED off its pin GATES", a_key_THAT_MOVED_off_its_pin_GATES),
    ("PIN: a TRACKED key that is GONE gates", a_TRACKED_key_that_is_GONE_gates),
    ("PIN: an UNTRACKED key that is ABSENT does NOT gate",
     an_UNTRACKED_key_that_is_ABSENT_does_NOT_gate),
    ("PIN: an UNTRACKED key that is PRESENT is still pinned",
     an_UNTRACKED_key_that_is_PRESENT_is_still_pinned),
    ("UNIQUE: a SECOND TRACKED copy GATES", a_SECOND_TRACKED_copy_GATES),
    ("UNIQUE: an UNTRACKED copy is printed and does NOT gate", an_UNTRACKED_copy_does_NOT_gate),
    ("SWEEP: a worktree checkout is counted separately", a_WORKTREE_checkout_is_counted_SEPARATELY),
    ("SWEEP: a same-size different-content file is NOT a copy",
     a_SAME_SIZE_DIFFERENT_CONTENT_file_is_NOT_a_copy),
    ("SWEEP: Live Runs/ is never walked and never printed", LIVE_RUNS_is_NOT_walked),
    ("CANNOT RUN: an ABSENT register is NOTHING EXAMINED", an_ABSENT_register_is_NOTHING_EXAMINED),
    ("CANNOT RUN: an EMPTY register is NOTHING EXAMINED", an_EMPTY_register_is_NOTHING_EXAMINED),
    ("CANNOT RUN: not a git repository is NOTHING EXAMINED", NOT_A_GIT_REPO_is_NOTHING_EXAMINED),
    ("REGISTER: a MALFORMED line is refused, not skipped", a_MALFORMED_line_is_REFUSED_not_skipped),
    ("REGISTER: the reasons are printed every run", the_REASONS_are_printed_every_run),
    ("REGISTER: a missing reason is called out", a_MISSING_reason_is_called_out),
    ("HONESTY: a clean verdict refuses to claim blindness", the_verdict_REFUSES_to_claim_blindness),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
