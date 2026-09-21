"""Offline test suite for tools/capture-build-baseline.py.

    python tools/capture-build-baseline.tests.py

No SDK is needed: every case feeds recorded MSBuild and VSTest output to the parsers, which is
where this script's judgement lives. The subprocess cases cover the refusals.

WHY THE FRESHNESS CASES CARRY THE WEIGHT. This script's first version reported OpennessCli.Tests
at 809 passed / 24 failed out of a solution that does not build on this machine, because
`dotnet test --no-build` happily ran a DLL built three weeks earlier on a different computer. The
second version tried to catch that with timestamps and was worse: an incremental build does not
rewrite an already-current DLL, so a re-run minutes later would have condemned every assembly in
the repository. The signal that works is MSBuild's own `Project -> ...dll` line, which it prints
for a project it rebuilt AND for one it confirmed up to date, and omits for one it never reached.
"""
import importlib.util, io, json, os, shutil, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "capture-build-baseline.py")

spec = importlib.util.spec_from_file_location("capture_build_baseline", SCRIPT)
cap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cap)

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


BUILD_OK = (
    "  WaveControl -> C:\\r\\src\\wave-control\\bin\\Release\\netstandard2.0\\Ladder.Wave.dll\n"
    "  Converter -> C:\\r\\src\\converter\\bin\\Release\\net8.0\\converter.dll\n"
    "  Converter.Tests -> C:\\r\\src\\converter\\Converter.Tests\\bin\\Release\\net8.0\\Converter.Tests.dll\n"
    "\nBuild succeeded.\n")

TEST_OK = ("Passed!  - Failed:     0, Passed:  1811, Skipped:     0, Total:  1811, "
           "Duration: 9 s - Converter.Tests.dll (net8.0)\n")


# ------------------------------------------------------------------ freshness

def an_up_to_date_project_still_counts(tmp):
    """MSBuild prints the arrow for a project it did NOT recompile. Treating that as stale was the
    second wrong answer, and it would have condemned every assembly on any repeat run."""
    built = cap.built_assemblies(BUILD_OK)
    rows = cap.parse_assemblies(TEST_OK, "s.sln")
    fresh, unverified = cap.partition_unverified(rows, built)
    assert len(fresh) == 1 and not unverified, "up-to-date must count: %r / %r" % (fresh, unverified)


def an_assembly_msbuild_never_mentioned_is_NOT_counted(tmp):
    """THE REAL CASE, replayed: openness-cli's build reaches one project, and the test step then
    runs a leftover OpennessCli.Tests.dll from another machine."""
    build = "  DownloadFeedback -> C:\\r\\bin\\Release\\netstandard2.0\\Ladder.Download.dll\n"
    test = ("Failed!  - Failed:    24, Passed:   809, Skipped:     0, Total:   833, "
            "Duration: 3 s - OpennessCli.Tests.dll (net48)\n")
    fresh, unverified = cap.partition_unverified(
        cap.parse_assemblies(test, "openness-cli.sln"), cap.built_assemblies(build))
    assert not fresh, "an unbuilt assembly must not be counted: %r" % fresh
    assert len(unverified) == 1 and unverified[0]["passed"] == 809, \
        "it must be RECORDED, not dropped: %r" % unverified
    assert "unknown provenance" in unverified[0]["reason"], "the reason must be stated"


def a_partial_build_failure_keeps_the_projects_that_DID_build(tmp):
    """harness.sln fails on one project and still produces sixteen good assemblies. Discarding a
    whole solution because one project failed would understate the baseline - and understating it
    makes the post-scrub run look like a gain while hiding a real loss underneath."""
    build = BUILD_OK + "  ...\\Bad.csproj(9,9): error CS0246: nope [C:\\r\\Bad.csproj]\n"
    fresh, unverified = cap.partition_unverified(
        cap.parse_assemblies(TEST_OK, "s.sln"), cap.built_assemblies(build))
    assert len(fresh) == 1 and not unverified, "the good assembly must survive: %r" % unverified


# ------------------------------------------------------------------- parsing

def a_failing_assembly_is_parsed(tmp):
    rows = cap.parse_assemblies(
        "Failed!  - Failed:     9, Passed:  1811, Skipped:     0, Total:  1820, "
        "Duration: 9 s - Converter.Tests.dll (net8.0)\n", "s.sln")
    assert rows and rows[0]["failed"] == 9 and rows[0]["passed"] == 1811, rows


def the_FAILING_TEST_NAMES_are_captured(tmp):
    """*** THE NAME, WHICH THIS TOOL USED TO THROW AWAY. ***

    A CI run on 2026-09-21 reported "Ladder.Wave.Tests went from 0 failing to 1" and the name was
    unrecoverable: it had been parsed out of dotnet test's stdout and dropped. On a runner whose raw
    logs are 403 to anonymous callers, that turned a one-line fix into an unanswerable question.
    Both logger spellings are covered because which one appears depends on the version."""
    text = ("  Failed Some.Namespace.ClassName.A_test_that_broke [12 ms]\n"
            "  X Some.Namespace.ClassName.Another_one(x: 1) [3 ms]\n"
            "  Passed Some.Namespace.ClassName.Fine [1 ms]\n")
    names = cap.parse_failed_tests(text)
    assert names == ["Some.Namespace.ClassName.A_test_that_broke",
                     "Some.Namespace.ClassName.Another_one(x: 1)"], names


def a_PASSING_run_captures_no_names(tmp):
    text = ("Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, "
            "Duration: 1 s - A.Tests.dll (net8.0)\n")
    assert cap.parse_failed_tests(text) == [], "a green run must not invent a failure"


def the_ASSEMBLY_SUMMARY_line_is_not_read_as_a_test_name(tmp):
    """`Failed!  - Failed: 2, ...` starts with the same word. Matching it would report the DLL as a
    failing test and bury the real one."""
    text = ("Failed!  - Failed:     2, Passed:   472, Skipped:     0, Total:   474, "
            "Duration: 8 s - A.Tests.dll (net8.0)\n")
    assert cap.parse_failed_tests(text) == [], cap.parse_failed_tests(text)


def a_multi_targeted_assembly_is_folded_not_duplicated(tmp):
    """One assembly reported once per target framework. Two rows under one key would make the
    capture refuse to load, so they are summed and flagged."""
    text = ("Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, "
            "Duration: 1 s - A.Tests.dll (net8.0)\n"
            "Passed!  - Failed:     1, Passed:     5, Skipped:     0, Total:     6, "
            "Duration: 1 s - A.Tests.dll (net48)\n")
    rows = cap.parse_assemblies(text, "s.sln")
    assert len(rows) == 1, "must fold to one row: %r" % rows
    assert rows[0]["passed"] == 15 and rows[0]["failed"] == 1, rows
    assert rows[0].get("multiTargeted"), "the fold must be visible"


def build_errors_are_deduped_per_code_and_project(tmp):
    text = ("a.cs(1,1): error CS0246: missing X [C:\\r\\A.csproj]\n"
            "a.cs(2,2): error CS0246: missing Y [C:\\r\\A.csproj]\n"
            "b.cs(3,3): error CS0246: missing Z [C:\\r\\B.csproj]\n")
    errs = cap.parse_errors(text)
    assert len(errs) == 2, "one entry per (code, project): %r" % errs


# ----------------------------------------------------------------- discovery

def a_project_in_no_solution_is_found(tmp):
    """src/hmi-cli's 161 tests live here. A loop over *.sln - which is what a CI script naturally
    writes - never reaches them, and reports green."""
    write(os.path.join(tmp, "src", "a", "a.sln"), "Project(\"{X}\") = \"A\", \"A\\A.csproj\"\n")
    write(os.path.join(tmp, "src", "a", "A", "A.csproj"), "<Project/>")
    write(os.path.join(tmp, "src", "lone", "Lone.Tests", "Lone.Tests.csproj"), "<Project/>")
    slns, projs = cap.walk_projects(tmp)
    orphans = cap.orphan_projects(tmp, slns, projs)
    assert orphans == ["src/lone/Lone.Tests/Lone.Tests.csproj"], orphans


def build_output_and_nested_worktrees_are_skipped(tmp):
    write(os.path.join(tmp, "src", "a", "a.sln"), "x")
    write(os.path.join(tmp, "src", "a", "bin", "Release", "stale.csproj"), "<Project/>")
    write(os.path.join(tmp, "src", "a", "obj", "stale2.csproj"), "<Project/>")
    write(os.path.join(tmp, ".claude", "worktrees", "w", "src", "b", "b.sln"), "x")
    slns, projs = cap.walk_projects(tmp)
    assert slns == ["src/a/a.sln"], "a worktree copy is not a second solution: %r" % slns
    assert projs == [], "build output is not source: %r" % projs


# ------------------------------------------------------------------ refusals

def run_script(cwd, *args):
    p = subprocess.Popen([sys.executable, SCRIPT] + list(args), cwd=cwd,
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    out, _ = p.communicate()
    return p.returncode, out.decode("utf-8", "replace")


# ------------------------------------------------- the CI gate (--expect)
#
# These mirror, case for case, the eight already proven against verify-scrub.py's build comparison.
# The duplication is deliberate and documented at load_expected(): the same claim about the same file
# format, reachable from a CI runner that has none of Gate 3's inputs. If one set changes, both do.
#
# They drive compare_to_expected directly rather than through a build, because a case that needed the
# SDK could not run on a machine without one - and the comparison is the part under test, not dotnet.

def expected_doc(rows, cannot=None):
    return {"assemblies": [{"assembly": a, "passed": p, "failed": f} for a, p, f in rows],
            "cannotBuild": [{"target": t} for t in (cannot or [])]}


def actual_rows(rows):
    return [{"assembly": a, "passed": p, "failed": f} for a, p, f in rows]


def gate(expect_rows, actual, expect_cannot=None, actual_cannot=None):
    doc = expected_doc(expect_rows, expect_cannot)
    by = {r["assembly"]: (r["passed"], r["failed"]) for r in doc["assemblies"]}
    return cap.compare_to_expected(by, doc, actual_rows(actual),
                                   [{"target": t} for t in (actual_cannot or [])])


def expect_identical_passes(tmp):
    _, findings = gate([("A", 10, 1), ("B", 5, 0)], [("A", 10, 1), ("B", 5, 0)])
    assert not findings, "an identical run must pass: %r" % findings


def expect_a_DECREASE_in_passes_GATES(tmp):
    _, findings = gate([("A", 10, 0)], [("A", 9, 0)])
    assert findings and "10 passing to 9" in findings[0], \
        "a lost pass must gate and name the numbers: %r" % findings


def expect_a_RISE_in_failures_GATES(tmp):
    """THE CASE THE WHOLE CI GATE EXISTS FOR. Two known failures are tolerated by being recorded in
    the baseline; a THIRD is a regression and must turn the build red."""
    _, findings = gate([("GoldenHarness.Tests", 204, 2)], [("GoldenHarness.Tests", 203, 3)])
    assert any("204 passing to 203" in f for f in findings), "the lost pass must gate: %r" % findings
    assert any("2 failing to 3" in f for f in findings), "the third failure must gate: %r" % findings


def expect_the_known_2_do_NOT_gate(tmp):
    """The other half of the same case, and the one that keeps CI honest rather than merely green:
    the 2 recorded failures are not excluded, not filtered and not hidden - they RUN, and the gate
    tolerates exactly the recorded number."""
    _, findings = gate([("GoldenHarness.Tests", 204, 2)], [("GoldenHarness.Tests", 204, 2)])
    assert not findings, "the recorded known failures must not gate: %r" % findings


def expect_a_VANISHED_assembly_GATES(tmp):
    """This is the orphan guard, and the assembly it names is the one that taught it. src/hmi-cli
    belonged to no solution, so a CI looping over *.sln dropped its 161 tests and reported green.
    It has a solution now - but the guard is not about hmi-cli: the baseline names every assembly,
    so ANY of them going missing is a finding, whoever drops it and however."""
    _, findings = gate([("A", 10, 0), ("HmiCli.Tests", 161, 0)], [("A", 10, 0)])
    assert findings and "HmiCli.Tests" in findings[0], \
        "a missing assembly must gate and name itself: %r" % findings


def expect_a_MOVE_between_assemblies_GATES_although_the_total_is_equal(tmp):
    """10+5 before, 5+10 after: the totals agree exactly and a whole project's tests have moved."""
    _, findings = gate([("A", 10, 0), ("B", 5, 0)], [("A", 5, 0), ("B", 10, 0)])
    assert findings, "an equal-total move must still gate"


def expect_a_GAIN_does_NOT_gate(tmp):
    """A test that started passing is not evidence of anything and must never cancel a loss."""
    lines, findings = gate([("A", 10, 0)], [("A", 12, 0), ("New.Tests", 3, 0)])
    assert not findings, "gains and new assemblies must not gate: %r" % findings
    assert any("does NOT gate" in l for l in lines), "the gain must still be reported: %r" % lines


def expect_a_NEWLY_UNBUILDABLE_target_GATES(tmp):
    """openness-cli and Harness.RigRead cannot build on a runner and are recorded as such. A target
    that built in the baseline and does not now produces no assembly at all, so it leaves no row to
    compare and no count to fall - it has to be caught on its own terms."""
    _, findings = gate([("A", 10, 0)], [("A", 10, 0)],
                       expect_cannot=["src/openness-cli/openness-cli.sln"],
                       actual_cannot=["src/openness-cli/openness-cli.sln", "src/converter/converter.sln"])
    assert findings and "converter.sln" in findings[-1], \
        "a newly-unbuildable target must gate and name itself: %r" % findings
    assert not any("openness-cli" in f for f in findings), \
        "an ALREADY-gated target is not a new finding: %r" % findings


def expect_a_MISSING_baseline_is_exit_2(tmp):
    """*** THIS CASE WAS VACUOUS AND MUTATION TESTING IS WHAT SAID SO. ***

    It asserted only `exit == 2`, and the fixture never produced an assembly - so the run exited 2 at
    "NOTHING EXAMINED" and never reached the --expect check at all. Disabling that check turned
    0 of 22 red: the case was watching a completely different refusal produce the same number.

    Two things fixed it. The tool now validates --expect BEFORE the build, so this is reachable in a
    fixture with no SDK; and the REASON is asserted, not just the code."""
    write(os.path.join(tmp, "src", "a", "a.sln"), "x")
    code, out = run_script(tmp, "--repo", tmp, "--out", os.path.join(tmp, "o.json"),
                           "--expect", os.path.join(tmp, "typo.json"))
    assert code == 2, "a missing --expect must refuse, got %d: %s" % (code, out)
    assert "MISSING baseline is a refusal" in out, \
        "the refusal must say the baseline path is missing, not merely refuse:\n%s" % out


def expect_malformed_JSON_is_exit_2(tmp):
    bad = os.path.join(tmp, "bad.json")
    write(bad, "{ this is not json")
    try:
        cap.load_expected(bad)
    except ValueError as exc:
        assert "not readable JSON" in str(exc), "the refusal must name the parse failure: %s" % exc
    else:
        raise AssertionError("unparseable input must raise, never return empty")


def expect_a_DUPLICATE_row_is_refused(tmp):
    dupe = os.path.join(tmp, "dupe.json")
    write(dupe, json.dumps({"assemblies": [{"assembly": "A", "passed": 1, "failed": 0},
                                           {"assembly": "A", "passed": 9, "failed": 0}]}))
    try:
        cap.load_expected(dupe)
    except ValueError as exc:
        assert "twice" in str(exc), "the refusal must say what is ambiguous: %s" % exc
    else:
        raise AssertionError("an ambiguous baseline must refuse")


def no_solution_and_no_orphan_test_is_exit_2(tmp):
    """EMPTY IS NOT CLEAN. A tidy empty capture compares equal to another tidy empty capture."""
    code, out = run_script(tmp, "--repo", tmp, "--out", os.path.join(tmp, "o.json"))
    assert code == 2, "expected 2, got %d: %s" % (code, out)
    assert "NOTHING EXAMINED" in out, out


def a_missing_dotnet_is_exit_3_not_an_empty_capture(tmp):
    code, out = run_script(tmp, "--repo", tmp, "--dotnet", "definitely-not-a-real-sdk-binary",
                           "--out", os.path.join(tmp, "o.json"))
    assert code == 3, "expected 3, got %d: %s" % (code, out)
    assert "empty capture compares equal" in out, out


def a_missing_repo_is_exit_3(tmp):
    code, out = run_script(tmp, "--repo", os.path.join(tmp, "nope"),
                           "--out", os.path.join(tmp, "o.json"))
    assert code == 3, "expected 3, got %d: %s" % (code, out)


for name, body in [
    ("EXPECT: an identical run passes", expect_identical_passes),
    ("EXPECT: a DECREASE in passes GATES", expect_a_DECREASE_in_passes_GATES),
    ("EXPECT: a RISE in failures GATES", expect_a_RISE_in_failures_GATES),
    ("EXPECT: the known 2 do NOT gate", expect_the_known_2_do_NOT_gate),
    ("EXPECT: a VANISHED assembly GATES", expect_a_VANISHED_assembly_GATES),
    ("EXPECT: a MOVE between assemblies GATES although the total is equal",
     expect_a_MOVE_between_assemblies_GATES_although_the_total_is_equal),
    ("EXPECT: a GAIN does NOT gate", expect_a_GAIN_does_NOT_gate),
    ("EXPECT: a NEWLY UNBUILDABLE target GATES", expect_a_NEWLY_UNBUILDABLE_target_GATES),
    ("EXPECT: a MISSING baseline is exit 2", expect_a_MISSING_baseline_is_exit_2),
    ("EXPECT: malformed JSON is refused", expect_malformed_JSON_is_exit_2),
    ("EXPECT: a DUPLICATE row is refused", expect_a_DUPLICATE_row_is_refused),
    ("an up-to-date project still counts", an_up_to_date_project_still_counts),
    ("an assembly MSBuild never mentioned is NOT counted",
     an_assembly_msbuild_never_mentioned_is_NOT_counted),
    ("a partial build failure keeps the projects that DID build",
     a_partial_build_failure_keeps_the_projects_that_DID_build),
    ("a failing assembly is parsed", a_failing_assembly_is_parsed),
    ("NAMES: the failing test names are captured", the_FAILING_TEST_NAMES_are_captured),
    ("NAMES: a passing run captures no names", a_PASSING_run_captures_no_names),
    ("NAMES: the assembly summary line is not a test name",
     the_ASSEMBLY_SUMMARY_line_is_not_read_as_a_test_name),
    ("a multi-targeted assembly is folded, not duplicated",
     a_multi_targeted_assembly_is_folded_not_duplicated),
    ("build errors are deduped per code and project",
     build_errors_are_deduped_per_code_and_project),
    ("a project in no solution is found", a_project_in_no_solution_is_found),
    ("build output and nested worktrees are skipped",
     build_output_and_nested_worktrees_are_skipped),
    ("no solution and no orphan test is exit 2", no_solution_and_no_orphan_test_is_exit_2),
    ("a missing dotnet is exit 3, not an empty capture",
     a_missing_dotnet_is_exit_3_not_an_empty_capture),
    ("a missing repo is exit 3", a_missing_repo_is_exit_3),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
