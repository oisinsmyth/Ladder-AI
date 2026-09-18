"""Offline test suite for tools/verify-scrub.py.

    python tools/verify-scrub.tests.py

Same method as build-scrub-rules.tests.py: each case builds a throwaway git repo, copies the script
under test into it UNMODIFIED, and runs it as a child process. No testability hooks - a gate that
can be aimed elsewhere can be aimed away.

WHY THIS FILE EXISTS, STATED PLAINLY. `verify-scrub.py` had been exercised against a real
git-filter-repo rewrite, which is stronger evidence than any fixture - but its THREE-TIER LOGIC, the
thing that decides every verdict, was proven by nothing. And the tool's own history is the argument:
it shipped once with a tokeniser that made 1,505 of 1,719 needles unmatchable while reporting a
plausible residual count, and its sibling shipped exiting 0 while emitting no rule for 18 of 19
declared identifiers. TWICE IN ONE SESSION A TOOL HERE EXITED 0 WHILE BROKEN. Both were caught
adversarially, neither by a test.

The tier cases are the point of the file:
  T1 DECLARED - an EMBEDDED-ONLY hit GATES. This is the width the builder deliberately lacks.
  T2 INFERRED - a WHOLE-TOKEN hit gates; an EMBEDDED-ONLY hit does NOT. `Foo.Bar` inside
                `Foo.BarBaz` is a different tag; 59 of 61 such hits were false positives.
  T3 ORDINARY - presence does NOT gate; a DECREASE against the canary DOES. The over-scrub detector.

Every case runs the real pre-scrub -> scrub -> verify cycle, because the canary only means anything
when it was recorded against the unscrubbed repository.
"""
import io, os, shutil, subprocess, sys, tempfile

EXIT_OK = 0
EXIT_FINDING = 1
EXIT_CANNOT_RUN = 2
EXIT_REFUSED = 3

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "verify-scrub.py")

# A string planted in every fixture so the positive control has something to hold on to. Without a
# surviving must-survive entry the gate returns 2 by design, which would mask every other verdict.
CONTROL = "PERMANENT-CONTROL-STRING"

passed, failed, failures = 0, 0, []


def case(name, body):
    global passed, failed
    tmp = tempfile.mkdtemp()
    try:
        body(tmp)
    except AssertionError as exc:
        failed += 1
        failures.append("%s: %s" % (name, exc))
        print("FAIL  %s" % name)
        print("      %s" % exc)
    else:
        passed += 1
        print("PASS  %s" % name)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def write(path, text):
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    with io.open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


# DEVNULL, never PIPE. `subprocess.call` with a PIPE nobody reads deadlocks as soon as the child
# fills the buffer, and `git add` emits one CRLF warning per file.
def git(tmp, *args):
    return subprocess.call(["git"] + list(args), cwd=tmp,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


MAPS = ('{"Names": {'
        '"AcmeWidgetUnit": "GenericWidgetUnit",'          # T2 - long, non-identity, not green
        '"KeepThisName": "KeepThisName",'                 # T3 - identity mapping
        '"Pump": "Mover"'                                 # T3 - under the length floor
        '}}')
TERMS = ("| live | invented | class | scope | variants |\n|---|---|---|---|---|\n"
         "| ZZ9999 | JOB9999 | jobcode | global | auto |\n")   # T1 - declared


def build(tmp, files, terms=TERMS, maps=MAPS):
    """A throwaway repo carrying the identifiers, with the script and sanitization inputs in it."""
    os.makedirs(os.path.join(tmp, "tools"))
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "verify-scrub.py"))
    if maps is not None:
        write(os.path.join(tmp, "sanitization", "m.map.json"), maps)
    if terms is not None:
        write(os.path.join(tmp, "sanitization", "scrub-terms.md"), terms)
    write(os.path.join(tmp, ".gitignore"), "sanitization/\ntools/\n")
    for rel, text in files.items():
        write(os.path.join(tmp, rel), text)
    git(tmp, "init")
    git(tmp, "config", "user.email", "t@t")
    git(tmp, "config", "user.name", "t")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "baseline")
    return os.path.join(tmp, "tools", "verify-scrub.py")


def run(tmp, script, *args):
    proc = subprocess.Popen(
        [sys.executable, script, "--clone", tmp, "--survive", CONTROL] + list(args),
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=tmp)
    out, err = proc.communicate()
    return (proc.returncode, out.decode("utf-8", "replace"), err.decode("utf-8", "replace"))


def canary(tmp, script):
    """Record the PRE-scrub state. Everything downstream is meaningless without this."""
    code, out, err = run(tmp, script, "--make-canary")
    assert code == EXIT_OK, "--make-canary failed: %s%s" % (out, err)


def scrub(tmp, rel, old, new):
    """Stand in for filter-repo: rewrite one tracked file so the OLD CONTENT IS GONE FROM THE ODB.

    *** EDITING AND COMMITTING IS NOT A SCRUB, AND THIS FIXTURE GOT IT WRONG FIRST TIME. ***
    A new commit leaves the previous blob in the object database, and this gate walks the object
    database rather than the tip - so the old text is still there, the T3 count does not fall and
    the positive control does not die. The two cases that depend on content actually disappearing
    both passed a broken fixture and failed a correct tool.

    Amending and then expiring the reflog and pruning is what makes the old blob unreachable AND
    unreferenced, which is the state filter-repo leaves behind - its own recipe ends with exactly
    `reflog expire --all --expire=now && gc --prune=now`.

    *** AND IT LEAVES A commit-map, WHICH THIS FIXTURE ALSO HAS TO LEAVE. *** The same lesson a
    second time: a stand-in that reproduces only the part of the state somebody happened to think of
    is a stand-in that passes a broken tool. .git/filter-repo/commit-map is how a rewritten clone
    records which original commit each new one came from, and the canary's subject check reads it -
    so a fixture without one is claiming to be a rewrite that cannot say what it rewrote."""
    p = os.path.join(tmp, rel)
    before = subprocess.run(["git", "rev-parse", "HEAD"], cwd=tmp, capture_output=True,
                            text=True).stdout.strip()
    text = io.open(p, encoding="utf-8").read().replace(old, new)
    write(p, text)
    git(tmp, "add", "-A")
    git(tmp, "commit", "--amend", "-m", "scrubbed")
    git(tmp, "reflog", "expire", "--all", "--expire=now")
    git(tmp, "gc", "--prune=now", "--quiet")
    after = subprocess.run(["git", "rev-parse", "HEAD"], cwd=tmp, capture_output=True,
                           text=True).stdout.strip()
    write(os.path.join(tmp, ".git", "filter-repo", "commit-map"),
          "old                                      new\n%s %s\n" % (before, after))


def assert_eq(got, want, what):
    assert got == want, "%s: got %r, want %r" % (got and what or what, got, want)


def assert_in(needle, hay, what):
    assert needle in hay, "%s: expected %r in:\n%s" % (what, needle, hay[:1500])


# ----------------------------------------------------------------- the tier cases

def t1_declared_embedded_only_GATES(tmp):
    """THE DECLARED-TERM CASE. The owner wrote the term down, so it must not appear anywhere -
    including inside a larger token, which is exactly the width the builder deliberately lacks."""
    s = build(tmp, {"doc.md": "%s\nprefixZZ9999suffix\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "an embedded declared term must gate (%s)" % out)
    assert_in("T1 DECLARED", out, "the tier must be reported")


def t2_inferred_embedded_only_does_NOT_gate(tmp):
    """`Foo.Bar` inside `Foo.BarBaz` is a DIFFERENT tag, not a leak. Measured on the real corpus:
    59 of 61 inferred-key hits were embedded-only, and gating on them made the verdict unusable."""
    s = build(tmp, {"doc.md": "%s\nprefixAcmeWidgetUnitsuffix\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "an embedded INFERRED key must not gate (%s)" % out)
    assert_in("embedded only", out, "the embedded count must still be reported")


def t2_inferred_whole_token_GATES(tmp):
    s = build(tmp, {"doc.md": "%s\nAcmeWidgetUnit stands alone\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a whole-token inferred key must gate (%s)" % out)


def t3_ordinary_presence_does_NOT_gate(tmp):
    """Identity mappings and short keys are the conventional vocabulary. Their PRESENCE is expected;
    gating on it makes exit 1 permanent and the gate gets learned-ignored."""
    s = build(tmp, {"doc.md": "%s\nKeepThisName and Pump are ordinary\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "T3 presence must not gate (%s)" % out)
    assert_in("T3 ORDINARY", out, "T3 must still be reported")


def t3_decrease_GATES(tmp):
    """THE OVER-SCRUB DETECTOR. A bare-word rule that eats the already-clean corpus shows up here
    as a conventional name falling, and nothing else in the tool would notice."""
    body = "\n".join(["KeepThisName"] * 10)
    s = build(tmp, {"doc.md": "%s\n%s\n" % (CONTROL, body)})
    canary(tmp, s)
    scrub(tmp, "doc.md", body, "KeepThisName")          # 10 occurrences -> 1
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a T3 decrease must gate (%s)" % out)
    assert_in("more than half", out, "the over-scrub finding must name itself")


# ----------------------------------------------------------------- the surfaces

def residual_in_a_commit_message_only(tmp):
    """A blob-only scan passes this repo.

    THE COMMIT COMES BEFORE THE CANARY, and that ordering is the real procedure rather than a
    convenience: the source repository reaches the exact state being published, THEN the canary is
    recorded against it, then the rewrite runs. A canary recorded before a later commit describes a
    tree that no longer exists, which the subject check now refuses - correctly, and it caught this
    fixture doing it."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    write(os.path.join(tmp, "doc.md"), "%s\nx\n" % CONTROL)
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "mentions ZZ9999 in the message")
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a residual in a commit message must gate (%s)" % out)


def residual_in_a_filename_only(tmp):
    """Content clean; the identifier is only a path component. Proves tree parsing runs."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL, "ZZ9999-notes/x.md": "nothing here\n"})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a residual in a path must gate (%s)" % out)


def residual_in_a_branch_name_only(tmp):
    """filter-repo renames paths, not refs - this is the class it structurally cannot fix."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    git(tmp, "branch", "feature-ZZ9999")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a residual in a ref name must gate (%s)" % out)


def residual_in_an_unreachable_object(tmp):
    """The strongest single case: proves the OBJECT DATABASE is walked, not the refs.

    The control assertion matters as much as the finding - without proving the object is invisible
    to `rev-list --objects --all`, this case would pass on a tool that only walked reachable
    history."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    p = subprocess.Popen(["git", "hash-object", "-w", "--stdin"], cwd=tmp,
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    oid = p.communicate(b"ZZ9999 lives only here\n")[0].decode().strip()
    reach = subprocess.run(["git", "rev-list", "--objects", "--all"], cwd=tmp,
                           capture_output=True, text=True).stdout
    assert oid not in reach, "control failed: the object is reachable, so this proves nothing"
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a residual in an unreachable object must gate (%s)" % out)


def residual_in_a_non_odb_carrier(tmp):
    """`.git/config` and friends are reached by NO git plumbing - not by `cat-file`, not by
    `rev-list`, not by `for-each-ref`. A remote URL is the realistic case: they routinely carry a
    identifying name.

    THIS CASE EXISTS BECAUSE MUTATION TESTING FOUND IT MISSING. Disabling the whole non-ODB carrier
    scan turned 0 of 17 cases red - the branch-name case passes through `for-each-ref` and never
    touches these files, so the entire surface was untested while looking covered."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    git(tmp, "remote", "add", "origin", "https://example.invalid/ZZ9999-project.git")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a residual in .git/config must gate (%s)" % out)


def case_insensitivity(tmp):
    s = build(tmp, {"doc.md": "%s\nzz9999 lower and ZZ9999 upper\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a lower-case residual must gate (%s)" % out)


# ----------------------------------------------------------------- refusals

def must_survive_absent_is_exit_2_despite_zero_residuals(tmp):
    """THE HEADLINE CASE. A gate that examined nothing also reports zero residuals, and the
    scrub/verify loop actively rewards a shrinking search. A zero with a dead positive control is
    not a pass, and this is the case that makes that rule real rather than prose."""
    s = build(tmp, {"doc.md": "%s\nnothing identifying\n" % CONTROL})
    canary(tmp, s)
    scrub(tmp, "doc.md", CONTROL, "the control is gone now")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a dead positive control must be exit 2, not 0 (%s)" % out)
    assert_in("EMPTY IS NOT CLEAN", out, "the principle must be named")


def no_canary_is_exit_2(tmp):
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    code, out, _ = run(tmp, s)              # deliberately no --make-canary
    assert_eq(code, EXIT_CANNOT_RUN, "no canary must be exit 2, never 0")
    assert_in("NOTHING EXAMINED", out, "exit 2 must say so")


def no_needles_is_exit_2(tmp):
    s = build(tmp, {"doc.md": "%s\n" % CONTROL}, terms=None, maps='{"Names": {}}')
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "an empty vocabulary must be exit 2")


def not_a_git_repo_is_exit_3(tmp):
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    outside = tempfile.mkdtemp()
    try:
        proc = subprocess.Popen([sys.executable, s, "--clone", outside],
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=tmp)
        out, err = proc.communicate()
        assert_eq(proc.returncode, EXIT_REFUSED, "a non-repo must refuse before reading anything")
        assert_in("REFUSED", err.decode("utf-8", "replace"), "the refusal must say why")
    finally:
        shutil.rmtree(outside, ignore_errors=True)


def fast_mode_says_it_did_not_examine_history(tmp):
    """--fast exists, and must never be quotable as a Gate 3 answer."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s, "--fast")
    assert_in("THIS DID NOT EXAMINE HISTORY", out, "--fast must disclaim itself unconditionally")


def build_baseline_absence_is_stated(tmp):
    """Without a baseline the run says nothing about whether the scrub broke a test, and it must
    say so rather than let a clean residual count read as a full Gate 3 pass."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_in("NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST", out,
              "the missing build baseline must be stated")


# ------------------------------------------------------- the build comparison
#
# These pin the half of Gate 3 that used to be decorative. `--build-baseline` tested
# os.path.isfile and compared NOTHING: handing it any file at all removed the "says nothing about
# whether the scrub broke a test" banner while verifying exactly as much as passing nothing. A gate
# that greened on a file existing. Every case below would have passed against that version except
# the ones that assert a finding - which is the point.

def capture(tmp, name, rows, cannot=None):
    """Write a build capture in the schema tools/capture-build-baseline.py emits."""
    doc = {"schema": "ladder-ai/build-baseline/1",
           "assemblies": [{"assembly": a, "passed": p, "failed": f, "total": p + f}
                          for a, p, f in rows],
           "cannotBuild": [{"target": t} for t in (cannot or [])]}
    path = os.path.join(tmp, name)
    write(path, __import__("json").dumps(doc, indent=2))
    return path


def clean_repo(tmp):
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    return s


def build_baseline_ALONE_still_says_it_proved_nothing(tmp):
    """THE CASE THAT CAUGHT THE STUB. A baseline on its own is one half of a comparison, and the
    old code let it silence the banner. Supplying a perfectly good baseline and nothing to compare
    it against must still say, loudly, that no test was verified."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", base)
    assert_eq(code, EXIT_OK, "a lone baseline is not itself a finding (%s)" % out)
    assert_in("NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST", out,
              "a baseline with no --build-current must NOT silence the banner")


def build_identical_captures_pass_and_are_reported(tmp):
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 1), ("B.Tests", 5, 0)])
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 1), ("B.Tests", 5, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_OK, "identical captures must pass (%s)" % out)
    assert_in("build comparison", out, "the comparison must be reported, not assumed")
    assert "NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST" not in out, \
        "with both halves present the banner must come down"


def build_a_DECREASE_in_passes_GATES(tmp):
    """The whole reason the comparison exists. AB-1 recorded a rename silently weakening a test."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 0)])
    cur = capture(tmp, "cur.json", [("A.Tests", 9, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_FINDING, "a lost pass must gate (%s)" % out)
    assert_in("10 passing to 9", out, "the finding must name the numbers")


def build_a_RISE_in_failures_GATES(tmp):
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 1)])
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 3)])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_FINDING, "more failures must gate (%s)" % out)


def build_a_VANISHED_assembly_GATES(tmp):
    """A test that no longer runs has not passed - it has stopped being asked. The totals cannot
    see this: the row simply is not there to be compared."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 0), ("B.Tests", 5, 0)])
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_FINDING, "a missing assembly must gate (%s)" % out)
    assert_in("B.Tests", out, "the finding must name the assembly that went missing")


def build_a_MOVE_between_assemblies_GATES_although_the_total_is_equal(tmp):
    """THE CASE A TOTAL-ONLY COMPARISON CANNOT MAKE. 10+5 before, 5+10 after: the totals agree
    exactly and a whole project's tests have moved. Keyed per assembly, the loss is visible."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 0), ("B.Tests", 5, 0)])
    cur = capture(tmp, "cur.json", [("A.Tests", 5, 0), ("B.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_FINDING, "an equal-total move must still gate (%s)" % out)


def build_a_GAIN_does_NOT_gate(tmp):
    """A test that started passing is not evidence of anything, and must never be able to cancel
    a loss elsewhere - but it is not itself a finding either."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 0)])
    cur = capture(tmp, "cur.json", [("A.Tests", 12, 0), ("NewSuite.Tests", 3, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_OK, "gains and new assemblies must not gate (%s)" % out)
    assert_in("does NOT gate", out, "the gain must still be reported")


def build_a_NEWLY_UNBUILDABLE_target_GATES(tmp):
    """A target that built before and does not now produces no assembly at all, so it leaves no
    row to compare and no count to fall. It has to be caught on its own terms."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [("A.Tests", 10, 0)], cannot=["gated.sln"])
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 0)], cannot=["gated.sln", "broke.sln"])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_FINDING, "a newly-unbuildable target must gate (%s)" % out)
    assert_in("broke.sln", out, "the finding must name it")
    assert "gated.sln" not in out.split("GATE FAILED")[-1], \
        "a target that ALREADY could not build is not a new finding"


def build_current_with_no_baseline_is_exit_2(tmp):
    s = clean_repo(tmp)
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-current", cur)
    assert_eq(code, EXIT_CANNOT_RUN, "a capture with nothing to compare against is exit 2 (%s)" % out)


def build_a_MISSING_baseline_path_is_exit_2_not_a_shrug(tmp):
    """A typo in this path used to read exactly like a deliberate omission - the failure that
    looks like the safe default, which is the family of bug this repo keeps finding."""
    s = clean_repo(tmp)
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-baseline",
                       os.path.join(tmp, "typo.json"), "--build-current", cur)
    assert_eq(code, EXIT_CANNOT_RUN, "a missing baseline path must refuse (%s)" % out)
    # The REASON is asserted, not just the exit code. Mutation testing found that deleting the
    # explicit guard still produced exit 2 - the loader fails to open the file a moment later - so
    # the verdict is defended twice while the diagnostic was defended not at all. A refusal that
    # says "not readable JSON" about a path that does not exist sends the reader somewhere else.
    assert_in("MISSING baseline is a refusal", out,
              "the refusal must say the path is missing, not merely refuse")


def build_malformed_json_is_exit_2(tmp):
    s = clean_repo(tmp)
    bad = os.path.join(tmp, "bad.json")
    write(bad, "{ this is not json")
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", bad, "--build-current", cur)
    assert_eq(code, EXIT_CANNOT_RUN, "unparseable input must refuse, never skip (%s)" % out)
    # As above: swallowing the parse error and returning {} ALSO exits 2, via the zero-assemblies
    # refusal. Same verdict, and a reason that would send the reader hunting for missing tests
    # instead of a broken file. The message is the thing under test here.
    assert_in("not readable JSON", out, "the refusal must name the parse failure as the cause")


def build_zero_assemblies_is_exit_2(tmp):
    """Two empty captures compare equal. EMPTY IS NOT CLEAN."""
    s = clean_repo(tmp)
    base = capture(tmp, "base.json", [])
    cur = capture(tmp, "cur.json", [])
    code, out, _ = run(tmp, s, "--build-baseline", base, "--build-current", cur)
    assert_eq(code, EXIT_CANNOT_RUN, "an empty capture is not a clean one (%s)" % out)


def build_a_DUPLICATE_assembly_row_is_exit_2(tmp):
    """Two rows under one key: the tool cannot tell which is the truth, so it must not pick."""
    s = clean_repo(tmp)
    dupe = os.path.join(tmp, "dupe.json")
    write(dupe, __import__("json").dumps({"assemblies": [
        {"assembly": "A.Tests", "passed": 10, "failed": 0},
        {"assembly": "A.Tests", "passed": 4, "failed": 0}]}))
    cur = capture(tmp, "cur.json", [("A.Tests", 10, 0)])
    code, out, _ = run(tmp, s, "--build-baseline", dupe, "--build-current", cur)
    assert_eq(code, EXIT_CANNOT_RUN, "an ambiguous capture must refuse (%s)" % out)


# ------------------------------------------------- the canary's subject stamp (M-20)
#
# `make_canary` stamped subjectVersion, printed it, and NOTHING EVER COMPARED IT - while its own
# docstring promised "a stale stamp reports UNVERIFIED rather than passing". Found while auditing the
# pipeline immediately before the irreversible rewrite.
#
# It is not a neutral omission. The must-survive check passes when a control is seen AT LEAST as
# often as recorded, so an ageing canary carries smaller expected counts and the gate gets WEAKER the
# staler it is. The canary is the instrument, and an uncalibrated instrument reads clean.

def a_canary_from_a_different_tree_is_exit_2(tmp):
    """The plain case: record the canary, then move the repository on. Its counts now describe a
    tree that no longer exists, and no commit-map relates the two."""
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    write(os.path.join(tmp, "later.md"), "an ordinary commit after the canary\n")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "moves HEAD away from the stamp")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a canary from another tree must refuse (%s)" % out)
    assert_in("UNVERIFIED", out, "the refusal must name what it could not establish")


def a_canary_matching_the_rewritten_subject_PASSES(tmp):
    """The other direction, and the one that keeps the check from being a blanket refusal. After a
    real rewrite the clone's HEAD is a NEW hash the canary cannot possibly carry - so the check reads
    filter-repo's commit-map to ask which ORIGINAL commit this one came from, and that is what must
    match. Without this case, deleting the commit-map lookup entirely would still look correct."""
    s = build(tmp, {"doc.md": "%s\nprefixZZ9999suffix\n" % CONTROL})
    canary(tmp, s)
    scrub(tmp, "doc.md", "prefixZZ9999suffix", "prefixJOB9999suffix")
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "a canary matching the rewritten subject must pass (%s)" % out)


def a_clone_rewritten_from_a_DIFFERENT_commit_is_exit_2(tmp):
    """THE CASE THE COMMIT-MAP COMPARISON ACTUALLY GUARDS, and it was missing.

    The plain different-tree case above has no commit-map, so it exercises the ABSENT-map branch and
    leaves the comparison itself untested - mutation testing said so by disabling the comparison and
    turning nothing red. Here the map exists and points somewhere else: the canary was recorded at A,
    the repository moved to B, and the rewrite mapped B onto C. A rewrite of the wrong tree is
    exactly the accident that produces a confident, meaningless earned zero."""
    s = build(tmp, {"doc.md": "%s\nprefixZZ9999suffix\n" % CONTROL})
    canary(tmp, s)                                    # stamped at A
    write(os.path.join(tmp, "later.md"), "a commit the canary never saw\n")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "moves the subject to B")
    scrub(tmp, "doc.md", "prefixZZ9999suffix", "prefixJOB9999suffix")   # map records B -> C
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN,
              "a clone rewritten from a commit the canary never saw must refuse (%s)" % out)
    assert_in("rewritten from", out, "the refusal must name the mismatch it found")


def a_canary_with_no_subject_stamp_is_exit_2(tmp):
    """A canary that cannot say which tree it describes is not a usable instrument."""
    import json as _json
    s = build(tmp, {"doc.md": "%s\n" % CONTROL})
    canary(tmp, s)
    path = os.path.join(tmp, "sanitization", "canary.json")
    doc = _json.loads(io.open(path, encoding="utf-8").read())
    doc.pop("subjectVersion", None)
    write(path, _json.dumps(doc, indent=2))
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a canary with no subject must refuse (%s)" % out)
    assert_in("no subjectVersion", out, "the refusal must say what is missing")


def instrument_control_is_reported(tmp):
    """Every needle must match itself in a synthetic object pushed through the same matcher. This
    is the check that caught 1,505 of 1,719 needles being unmatchable."""
    s = build(tmp, {"doc.md": "%s\nZZ9999\n" % CONTROL})
    canary(tmp, s)
    code, out, _ = run(tmp, s)
    assert_in("instrument control", out, "the self-test must be reported on every run")
    assert "CANNOT MATCH" not in out, "the instrument reported itself broken on a healthy fixture"


for name, body in [
    ("T1 declared, embedded-only, GATES", t1_declared_embedded_only_GATES),
    ("T2 inferred, embedded-only, does NOT gate", t2_inferred_embedded_only_does_NOT_gate),
    ("T2 inferred, whole-token, GATES", t2_inferred_whole_token_GATES),
    ("T3 ordinary, presence, does NOT gate", t3_ordinary_presence_does_NOT_gate),
    ("T3 decrease GATES (the over-scrub detector)", t3_decrease_GATES),
    ("a residual in a commit message only", residual_in_a_commit_message_only),
    ("a residual in a filename only", residual_in_a_filename_only),
    ("a residual in a branch name only", residual_in_a_branch_name_only),
    ("a residual in an unreachable object", residual_in_an_unreachable_object),
    ("a residual in a non-ODB carrier (.git/config)", residual_in_a_non_odb_carrier),
    ("case-insensitivity", case_insensitivity),
    ("MUST-SURVIVE absent is exit 2 despite zero residuals",
     must_survive_absent_is_exit_2_despite_zero_residuals),
    ("no canary is exit 2", no_canary_is_exit_2),
    ("no needles is exit 2", no_needles_is_exit_2),
    ("not a git repo is exit 3", not_a_git_repo_is_exit_3),
    ("--fast says it did not examine history", fast_mode_says_it_did_not_examine_history),
    ("a missing build baseline is stated", build_baseline_absence_is_stated),
    ("a build baseline ALONE still says it proved nothing",
     build_baseline_ALONE_still_says_it_proved_nothing),
    ("identical build captures pass and are reported",
     build_identical_captures_pass_and_are_reported),
    ("a DECREASE in passes GATES", build_a_DECREASE_in_passes_GATES),
    ("a RISE in failures GATES", build_a_RISE_in_failures_GATES),
    ("a VANISHED assembly GATES", build_a_VANISHED_assembly_GATES),
    ("a MOVE between assemblies GATES although the total is equal",
     build_a_MOVE_between_assemblies_GATES_although_the_total_is_equal),
    ("a GAIN does NOT gate", build_a_GAIN_does_NOT_gate),
    ("a NEWLY UNBUILDABLE target GATES", build_a_NEWLY_UNBUILDABLE_target_GATES),
    ("--build-current with no baseline is exit 2", build_current_with_no_baseline_is_exit_2),
    ("a MISSING baseline path is exit 2, not a shrug",
     build_a_MISSING_baseline_path_is_exit_2_not_a_shrug),
    ("malformed capture JSON is exit 2", build_malformed_json_is_exit_2),
    ("zero assemblies is exit 2", build_zero_assemblies_is_exit_2),
    ("a DUPLICATE assembly row is exit 2", build_a_DUPLICATE_assembly_row_is_exit_2),
    ("SUBJECT: a canary from a different tree is exit 2", a_canary_from_a_different_tree_is_exit_2),
    ("SUBJECT: a canary matching the rewritten subject PASSES",
     a_canary_matching_the_rewritten_subject_PASSES),
    ("SUBJECT: a clone rewritten from a DIFFERENT commit is exit 2",
     a_clone_rewritten_from_a_DIFFERENT_commit_is_exit_2),
    ("SUBJECT: a canary with no subject stamp is exit 2", a_canary_with_no_subject_stamp_is_exit_2),
    ("the instrument control is reported", instrument_control_is_reported),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
