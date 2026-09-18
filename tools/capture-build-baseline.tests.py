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
import importlib.util, io, os, shutil, subprocess, sys, tempfile

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
    ("an up-to-date project still counts", an_up_to_date_project_still_counts),
    ("an assembly MSBuild never mentioned is NOT counted",
     an_assembly_msbuild_never_mentioned_is_NOT_counted),
    ("a partial build failure keeps the projects that DID build",
     a_partial_build_failure_keeps_the_projects_that_DID_build),
    ("a failing assembly is parsed", a_failing_assembly_is_parsed),
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
