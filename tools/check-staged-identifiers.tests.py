#!/usr/bin/env python3
"""Self-tests for check-staged-identifiers.py (M-5).

    python tools/check-staged-identifiers.tests.py

EVERY CASE OWNS A THROWAWAY REPOSITORY, because the subject under test is a git index and there is
no honest way to fake one. The fixture copies BOTH scripts - the checker derives its vocabulary from
verify-scrub.py, so a repo with only the checker in it tests a different program.

NOTHING HERE PLANTS A REAL IDENTIFIER. The fixtures use invented vocabulary, which is also the only
way these tests could ever be committed: a suite for a leak checker that contained the leak would be
the funniest possible instance of the thing it checks for.
"""
import io
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "check-staged-identifiers.py")
ORACLE = os.path.join(HERE, "verify-scrub.py")

EXIT_OK = 0
EXIT_FOUND = 1
EXIT_CANNOT_RUN = 2
EXIT_REFUSED = 3

passed, failed, failures = 0, 0, []


def case(name, body):
    global passed, failed
    tmp = tempfile.mkdtemp()
    try:
        body(tmp)
    except Exception as exc:                                # not just AssertionError - see below
        # A case that raises something unexpected must fail as a CASE, not kill the run. A harness
        # that cannot tell "one case failed" from "the run died" prints no result line at all, which
        # is the empty-is-not-clean failure wearing a test-runner costume.
        failed += 1
        failures.append("%s: %s: %s" % (name, type(exc).__name__, exc))
        print("FAIL  %s" % name)
        print("      %s: %s" % (type(exc).__name__, exc))
    else:
        passed += 1
        print("PASS  %s" % name)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def write(path, text, binary=False):
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    if binary:
        io.open(path, "wb").write(text)
    else:
        io.open(path, "w", encoding="utf-8", newline="\n").write(text)


def git(tmp, *args):
    quiet = {"stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL, "cwd": tmp}
    return subprocess.call(["git"] + list(args), **quiet)


def build(tmp, maps=None, terms=None, committed=None, ignore_sanitization=True):
    """A throwaway repo with both scripts, a vocabulary, and one committed baseline commit."""
    os.makedirs(os.path.join(tmp, "tools"))
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "check-staged-identifiers.py"))
    shutil.copy(ORACLE, os.path.join(tmp, "tools", "verify-scrub.py"))
    for name, doc in (maps or {}).items():
        write(os.path.join(tmp, "sanitization", name + ".map.json"), doc)
    if terms is not None:
        write(os.path.join(tmp, "sanitization", "scrub-terms.md"), terms)
    for rel, text in (committed or {}).items():
        write(os.path.join(tmp, rel), text)
    write(os.path.join(tmp, ".gitignore"),
          "sanitization/\n" if ignore_sanitization else "# nothing ignored\n")
    git(tmp, "init")
    git(tmp, "config", "user.email", "t@t")
    git(tmp, "config", "user.name", "t")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "baseline")
    return os.path.join(tmp, "tools", "check-staged-identifiers.py")


def run(tmp, script, *args):
    proc = subprocess.Popen([sys.executable, script, "--repo", tmp] + list(args),
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=tmp)
    out, err = proc.communicate()
    return (proc.returncode, out.decode("utf-8", "replace"), err.decode("utf-8", "replace"))


def assert_eq(got, want, what):
    assert got == want, "%s: got %r, want %r" % (what, got, want)


def assert_in(needle, haystack, what):
    assert needle in haystack, "%s: %r not in %r" % (what, needle, haystack)


# Invented vocabulary throughout. `Zorbex` is the declared term (T1), `QuadrantWidgetUnit` is an
# inferred map key long enough to be T2, and `Plate` is an identity mapping, which is T3.
TERMS = ("# terms\n\n| live | invented | class | scope | variants |\n"
         "|---|---|---|---|---|\n"
         "| Zorbex | JOB4242 | site | global | auto |\n")
MAPS = {"m": '{"Names": {"QuadrantWidgetUnit": "GenericWidgetUnit", "Plate": "Plate"}}'}


def a_staged_DECLARED_term_is_FOUND(tmp):
    """The case the whole tool exists for: an identifier reaching the index inside a code comment,
    which is how all 13 of the real ones arrived - quoted as evidence for a technical claim."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "// measured against Zorbex line 3\nclass C {}\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "a staged declared term must be found (%s%s)" % (out, err))
    assert_in("src.cs", out, "the worklist must name the path")
    assert_in("1 declared (T1)", out, "the tier must be reported")


def a_PRE_EXISTING_hit_in_a_modified_file_is_NOT_reported(tmp):
    """*** WHAT THIS COMMIT INTRODUCES, NOT WHAT THE FILE CONTAINS. ***

    This is the difference between a gate that is kept and one that is switched off. Almost every
    file under src/ and docs/ in the real repository already carries inferred vocabulary - that is
    what the publication scrub exists for - so judging the whole staged blob refuses a one-line
    change for a reason its author did not cause and cannot fix in that commit.

    Found the hard way: the first version refused a three-line test fix, naming six needles that had
    been in those files for months."""
    s = build(tmp, MAPS, TERMS,
              {"README.md": "nothing here\n",
               "old.cs": "// QuadrantWidgetUnit was already here\n"})
    write(os.path.join(tmp, "old.cs"),
          "// QuadrantWidgetUnit was already here\nint x = 1;\n")
    git(tmp, "add", "old.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a pre-existing needle must not gate (%s%s)" % (out, err))
    assert_in("carried over          : 1", out, "the carried needle must still be COUNTED")


def a_NEWLY_INTRODUCED_hit_in_a_modified_file_IS_reported(tmp):
    """The other half. The same file, the same commit shape - and this time the identifier is being
    added, which is exactly the 13-files-across-several-lanes failure M-5 was filed for."""
    s = build(tmp, MAPS, TERMS,
              {"README.md": "nothing here\n", "old.cs": "// nothing of interest\n"})
    write(os.path.join(tmp, "old.cs"), "// nothing of interest\n// measured against Zorbex\n")
    git(tmp, "add", "old.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "a newly added needle must gate (%s%s)" % (out, err))
    assert_in("old.cs", out, "the worklist must name the path")


def a_RENAME_of_a_file_that_already_carried_vocabulary_is_NOT_reported(tmp):
    """A rename compares against its OLD path. Without -M git reports it as an add, the whole file
    reads as new, and moving a file becomes an accusation."""
    s = build(tmp, MAPS, TERMS,
              {"README.md": "nothing here\n",
               "old.cs": "// QuadrantWidgetUnit has been here all along\n"})
    git(tmp, "mv", "old.cs", "renamed.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a pure rename must not gate (%s%s)" % (out, err))


def the_TERM_ITSELF_is_NOT_printed_by_default(tmp):
    """*** A RECORD OF A LEAK MUST NOT BE A COPY OF IT. *** This output gets pasted into notes, and
    a checker that prints the site's name into every terminal it runs in has moved the leak
    rather than found it. --name-terms is the deliberate, watched exception."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "// Zorbex\n")
    git(tmp, "add", "src.cs")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "exit (%s)" % out)
    assert "zorbex" not in out.lower(), "the matched term was printed without --name-terms: %r" % out
    code, out, _ = run(tmp, s, "--name-terms")
    assert_in("zorbex", out.lower(), "--name-terms must actually print it")


def the_SUBJECT_is_the_index_NOT_the_working_tree(tmp):
    """An unstaged leak is not in this commit. Refusing here is the failure that gets a hook turned
    off: the author cannot make the refusal go away by fixing the commit, because the commit is
    already clean."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "// clean\n")
    git(tmp, "add", "src.cs")
    write(os.path.join(tmp, "unstaged.cs"), "// Zorbex\n")      # never added
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "an UNSTAGED leak must not gate (%s%s)" % (out, err))


def a_leak_STAGED_then_edited_away_is_STILL_FOUND(tmp):
    """The mirror, and the half that a working-tree check gets wrong in the dangerous direction.
    The index is what becomes the commit; tidying the file afterwards does not untangle that."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "// Zorbex\n")
    git(tmp, "add", "src.cs")
    write(os.path.join(tmp, "src.cs"), "// tidied up afterwards\n")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "the STAGED blob is the subject (%s%s)" % (out, err))


def an_INFERRED_key_WHOLE_is_found(tmp):
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "class QuadrantWidgetUnit {}\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "a whole-token inferred key must be found (%s%s)" % (out, err))
    assert_in("1 inferred (T2)", out, "the tier must be reported")


def an_INFERRED_key_EMBEDDED_is_NOT_found(tmp):
    """*** THE ASYMMETRY, AND IT IS NOT A SOFTENING. *** Measured on the first trial rewrite, 59 of
    61 inferred-key hits were the same identifier seen inside a longer one - a different tag that
    merely starts the same way. A check that cries wolf 59 times out of 61 gets learned-ignored,
    and a gate people have learned to ignore is worse than no gate, because it reports green."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "class QuadrantWidgetUnitExtended {}\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "an embedded inferred key must not gate (%s%s)" % (out, err))


def a_DECLARED_term_EMBEDDED_is_still_found(tmp):
    """The other side of the same asymmetry. The owner wrote this string down and meant it anywhere,
    so being inside a longer word is not a defence - that is what DECLARED means."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "class ZorbexPumpAdapter {}\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "an embedded DECLARED term must be found (%s%s)" % (out, err))


def a_T3_identity_mapping_is_NEVER_reported(tmp):
    """T3 gates on a FALL in its count between two corpora. A staging area has no before and after,
    so there is no such measurement to make here - and reporting T3 on presence instead would be a
    different check wearing the tier's name."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "class Plate {}\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a T3 identity mapping must never gate (%s%s)" % (out, err))


def a_DELETED_path_is_not_examined(tmp):
    """A deletion leaves no blob in the index and carries nothing into the commit. Reading `:path`
    for it would fail, and treating that failure as unsearchable would refuse every removal."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n", "old.cs": "// Zorbex\n"})
    git(tmp, "rm", "-q", "old.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "deleting a leaking file must not gate (%s%s)" % (out, err))


def a_non_UTF8_staged_blob_is_UNSEARCHABLE_not_clean(tmp):
    """EMPTY IS NOT CLEAN. A blob nobody could search must never be counted as clean, so this exits
    1 with nothing found - the finding IS that something went unexamined."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "blob.bin"), b"\xff\xfe\x00\x01 binary", binary=True)
    git(tmp, "add", "blob.bin")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "an unsearchable blob must not report clean (%s%s)" % (out, err))
    assert_in("NOT SEARCHED", out, "the unsearchable path must be named")
    assert_in("not the same as clean", out, "the principle must be stated, not implied")


def nothing_staged_is_exit_0_because_git_AGREES(tmp):
    """An empty path list has two causes that look identical - a commit staging nothing, and a
    parser that stopped working. Asking git separately is what makes this zero safe to trust."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "nothing staged is genuinely nothing to check (%s%s)" % (out, err))
    assert_in("git agrees", out, "the corroboration must be stated")


def no_vocabulary_and_no_live_run_is_exit_0(tmp):
    """The ordinary state of a fresh clone: sanitization/ is git-ignored and machine-local, so it
    will never exist there. Refusing every commit forever is a broken tool, not a gate."""
    s = build(tmp, None, None, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "// clean\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "no vocabulary and no live run is not a refusal (%s%s)" % (out, err))
    assert_in("never worked a live job", out, "the reason must be stated")


def no_vocabulary_DURING_A_LIVE_RUN_is_exit_2(tmp):
    """*** THE ONE JUDGEMENT IN THIS TOOL, AND THE HALF THAT MAKES THE OTHER HALF SAFE. ***

    Going quiet when the vocabulary is missing is not hypothetical here: after a machine move
    core.hooksPath pointed at the previous machine's path and BOTH existing gates sat inert for
    weeks while every commit reported green. So the trigger is the dangerous state rather than the
    tool's convenience - live-job material on disk with no vocabulary to check against is precisely
    the moment M-5 exists for, and it refuses."""
    s = build(tmp, None, None, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "Live Runs", "job", "notes.txt"), "a real job is in progress\n")
    write(os.path.join(tmp, "src.cs"), "// clean\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a live run with no vocabulary must refuse (%s%s)" % (out, err))
    assert_in("EMPTY IS NOT CLEAN", out, "the principle must be named")


def an_EMPTY_live_runs_folder_is_not_a_live_run(tmp):
    """The folder exists in every clone of this repository. Its mere presence must not refuse."""
    s = build(tmp, None, None, {"README.md": "nothing here\n"})
    os.makedirs(os.path.join(tmp, "Live Runs"))
    write(os.path.join(tmp, "src.cs"), "// clean\n")
    git(tmp, "add", "src.cs")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "an empty Live Runs folder is not a live run (%s%s)" % (out, err))


def a_TRACKED_term_list_REFUSES(tmp):
    """The term list names every live identifier the check hunts for. Committing it publishes the
    thing the check exists to keep out, so this refuses rather than running against it."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"}, ignore_sanitization=False)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_REFUSED, "a tracked term list must refuse (%s%s)" % (out, err))
    assert_in("TRACKED BY GIT", err, "the refusal must say why, on stderr")


def the_instrument_control_is_REPORTED_every_run(tmp):
    """The oracle once reported a plausible residual count while 1,505 of its needles could not
    match anything at all, and every run looked exactly like a clean one. A control nobody prints
    is a control nobody checks."""
    s = build(tmp, MAPS, TERMS, {"README.md": "nothing here\n"})
    write(os.path.join(tmp, "src.cs"), "// clean\n")
    git(tmp, "add", "src.cs")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s)" % out)
    assert_in("instrument control", out, "the control must be reported on every run")
    # TWO, not three: the fixture declares Zorbex (T1) and infers QuadrantWidgetUnit (T2), while
    # `Plate` is an identity mapping and therefore T3, which is not hunted here at all. The count
    # is asserted exactly so that a tier silently changing population shows up as a failure.
    assert_in("2 of 2 needles matched themselves", out,
              "the control must name both numbers, not just say it passed")


for name, body in [
    ("a staged DECLARED term is FOUND", a_staged_DECLARED_term_is_FOUND),
    ("INTRODUCED: a pre-existing hit in a modified file is NOT reported",
     a_PRE_EXISTING_hit_in_a_modified_file_is_NOT_reported),
    ("INTRODUCED: a newly added hit in a modified file IS reported",
     a_NEWLY_INTRODUCED_hit_in_a_modified_file_IS_reported),
    ("INTRODUCED: a rename of a file that already carried vocabulary is NOT reported",
     a_RENAME_of_a_file_that_already_carried_vocabulary_is_NOT_reported),
    ("the term itself is NOT printed by default", the_TERM_ITSELF_is_NOT_printed_by_default),
    ("the SUBJECT is the index, not the working tree", the_SUBJECT_is_the_index_NOT_the_working_tree),
    ("a leak staged then edited away is STILL found", a_leak_STAGED_then_edited_away_is_STILL_FOUND),
    ("TIER: an INFERRED key WHOLE is found", an_INFERRED_key_WHOLE_is_found),
    ("TIER: an INFERRED key EMBEDDED is NOT found", an_INFERRED_key_EMBEDDED_is_NOT_found),
    ("TIER: a DECLARED term EMBEDDED is still found", a_DECLARED_term_EMBEDDED_is_still_found),
    ("TIER: a T3 identity mapping is NEVER reported", a_T3_identity_mapping_is_NEVER_reported),
    ("a DELETED path is not examined", a_DELETED_path_is_not_examined),
    ("a non-UTF-8 staged blob is UNSEARCHABLE, not clean",
     a_non_UTF8_staged_blob_is_UNSEARCHABLE_not_clean),
    ("nothing staged is exit 0 because git AGREES", nothing_staged_is_exit_0_because_git_AGREES),
    ("no vocabulary and no live run is exit 0", no_vocabulary_and_no_live_run_is_exit_0),
    ("no vocabulary DURING A LIVE RUN is exit 2", no_vocabulary_DURING_A_LIVE_RUN_is_exit_2),
    ("an EMPTY Live Runs folder is not a live run", an_EMPTY_live_runs_folder_is_not_a_live_run),
    ("a TRACKED term list REFUSES", a_TRACKED_term_list_REFUSES),
    ("the instrument control is REPORTED every run", the_instrument_control_is_REPORTED_every_run),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
