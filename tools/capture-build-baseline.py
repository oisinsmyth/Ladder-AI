#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Capture per-assembly test counts, in the one format verify-scrub.py compares.

WHY THIS IS A SCRIPT AND NOT A PROCEDURE. The two captures either side of a history rewrite are
only comparable if they were taken the same way. A baseline assembled by hand from a terminal
scroll and a "current" assembled by hand a week later differ in which solutions were remembered,
which orphan project was noticed, and whether a failing build was recorded or skipped past - and
every one of those differences reads downstream as a scrub that changed something. One script, run
twice, removes the whole class.

    python tools/capture-build-baseline.py --out sanitization/build-baseline.json      # before
    python tools/capture-build-baseline.py --out sanitization/build-current.json --repo <clone>
    python tools/verify-scrub.py --clone <clone> \
           --build-baseline sanitization/build-baseline.json \
           --build-current  sanitization/build-current.json

EMPTY IS NOT CLEAN applies here as everywhere: a run that discovered no solution, or that produced
no assembly row, exits 2 rather than writing a tidy empty capture that would later compare equal to
another tidy empty capture and pass.

ORPHAN PROJECTS ARE HUNTED DELIBERATELY. A test project belonging to no .sln is invisible to any
loop over solutions, which is what a CI script naturally writes; src/hmi-cli's 161 tests sat in
exactly that position until it was given a solution of its own. THE HUNT DID NOT RETIRE WITH IT -
nobody decides to leave a project out of a solution, they forget to, so a tree is never reliably
free of an orphan and the next one arrives just as quietly. Discovery here is by project file, and
solution membership is recorded rather than assumed.

The output names assemblies and counts. It carries no identifier and is safe to read, but it is
written under sanitization/ by default because that is where this pipeline's working files live.
"""

import argparse
import io
import json
import os
import re
import subprocess
import sys
import time

# Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 34 ms - X.dll (net8.0)
SUMMARY = re.compile(
    r"^\s*(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+"
    r"Skipped:\s+(\d+),\s+Total:\s+(\d+),.*?-\s+(\S+?)\.dll", re.IGNORECASE)

# ...\Foo.csproj(12,34): error CS0246: The type or namespace name 'Bar' could not be found [...\Foo.csproj]
ERROR_LINE = re.compile(r"\berror\s+([A-Z]+\d+)\s*:\s*(.*?)\s*(?:\[([^\]]*\.csproj)\])?\s*$")

#   Converter.Tests -> C:\...\bin\Release\net8.0\Converter.Tests.dll
ARROW = re.compile(r"->\s+(\S.*?\.dll)\s*$", re.IGNORECASE)

# *** THE NAME OF THE TEST THAT FAILED, WHICH THIS TOOL USED TO THROW AWAY. ***
# Added 2026-09-21 after a CI run reported "Ladder.Wave.Tests went from 0 failing to 1" and the
# name was UNRECOVERABLE: dotnet test's stdout was captured, the summary line parsed out of it, and
# the rest discarded - so the one fact needed to act on the gate never reached the log, and raw
# Actions logs are 403 to anonymous callers anyway. A gate that says a test broke without saying
# WHICH is a gate you cannot act on from the machine that is not the one that ran it.
# `dotnet test` prints failures two ways depending on logger and version.
FAILED_TEST = re.compile(r"^\s*(?:Failed|X)\s+(\S+(?:\([^)]*\))?)\s*(?:\[.*\])?\s*$")

SKIP_DIRS = {".git", "bin", "obj", "node_modules", "packages", "TestResults"}


def walk_projects(root):
    """Every .sln and .csproj in the tree, ignoring build output and nested worktrees."""
    slns, projs = [], []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames
                       if d not in SKIP_DIRS and not d.startswith(".claude")]
        for name in filenames:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace("\\", "/")
            if name.endswith(".sln"):
                slns.append(rel)
            elif name.endswith(".csproj"):
                projs.append(rel)
    return sorted(slns), sorted(projs)


def orphan_projects(root, slns, projs):
    """Projects named in no solution. A solution-shaped loop never reaches these."""
    named = ""
    for sln in slns:
        try:
            named += io.open(os.path.join(root, sln), encoding="utf-8-sig",
                             errors="replace").read()
        except IOError:
            continue
    return [p for p in projs if os.path.basename(p) not in named]


def run(cmd, cwd):
    # An unlaunchable command reports a failure exit code rather than raising. A traceback out of
    # here would abandon the capture mid-run, and a half-written capture is one that gets compared.
    try:
        p = subprocess.Popen(cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    except OSError as exc:
        return 127, "could not launch %r: %s" % (cmd[0], exc)
    out, _ = p.communicate()
    return p.returncode, out.decode("utf-8", "replace")


# *** A TEST NAME IS ARBITRARY TEXT AND STDOUT IS NOT ALWAYS UTF-8. ***
# dotnet test's output is decoded with errors="replace", so a name can carry U+FFFD - and cp1252,
# which is what Python picks for a PIPED stdout on Windows, CANNOT ENCODE U+FFFD. Printing one
# raises UnicodeEncodeError and takes the whole capture down. Trading a failing gate for a crashing
# one is strictly worse: the gate at least says what it found. Names are printed ASCII-safe; the
# JSON keeps the original, because json.dump escapes rather than encodes.
def printable(name):
    return name.encode("ascii", "backslashreplace").decode("ascii")


def parse_failed_tests(text):
    """Every failing test NAME dotnet test printed, in order, de-duplicated.

    Names are NOT joined to individual assemblies: the summary line and the failure lines are not
    reliably interleaved per assembly across loggers, and a name attached to the WRONG assembly
    would be worse than a name attached to none. They ARE filtered by TARGET by the caller, which
    is sound because a target is one `dotnet test` invocation - see `counted_targets`. That filter
    is not optional: the first run of this feature recorded 26 names against a capture reporting 2
    failures, 24 of them from an excluded stale artifact.
    """
    names, seen = [], set()
    for line in text.splitlines():
        m = FAILED_TEST.match(line)
        if not m:
            continue
        name = m.group(1)
        if name.lower().endswith(".dll") or name in seen:
            continue
        seen.add(name)
        names.append(name)
    return names


def parse_assemblies(text, target):
    rows, seen = [], set()
    for line in text.splitlines():
        m = SUMMARY.match(line)
        if not m:
            continue
        failed, passed, skipped, total, assembly = m.groups()
        # A solution can report the same assembly once per target framework. Fold them: the
        # comparison is keyed by assembly name, and two rows under one key would refuse to load.
        if assembly in seen:
            for row in rows:
                if row["assembly"] == assembly:
                    row["passed"] += int(passed)
                    row["failed"] += int(failed)
                    row["skipped"] += int(skipped)
                    row["total"] += int(total)
                    row["multiTargeted"] = True
            continue
        seen.add(assembly)
        rows.append({"target": target, "assembly": assembly, "passed": int(passed),
                     "failed": int(failed), "skipped": int(skipped), "total": int(total)})
    return rows


def built_assemblies(text):
    """Assembly names MSBuild reported emitting, from its `Project -> ...\\Name.dll` lines.

    THIS IS THE FRESHNESS SIGNAL, and it took a wrong answer to find the right one.

    `dotnet test --no-build` runs whatever DLLs are sitting in bin/, and after a failed build
    those can be artifacts from another machine. The first run of this script reported
    OpennessCli.Tests at 809 passed / 24 failed out of a solution that DOES NOT BUILD here - from
    a binary dated three weeks earlier, produced before this machine had an SDK at all. Its own
    project was never attempted, because the project it depends on failed first.

    That error runs in the dangerous direction: a fresh clone has no bin/ at all, so the stale
    assembly is absent on the other side of the comparison and reads as a whole test project
    destroyed by the scrub.

    Timestamps CANNOT answer this. An incremental build does not rewrite a DLL that is already
    current, so a second capture minutes later would find every assembly older than the run and
    condemn all of them. MSBuild prints the `->` line either way - for a project it recompiled and
    for one it confirmed up to date - and prints nothing for a project it never reached. That
    distinction is exactly the question being asked.
    """
    names = set()
    for line in text.splitlines():
        m = ARROW.search(line)
        if m:
            names.add(os.path.basename(m.group(1))[:-4])
    return names


def partition_unverified(rows, built):
    """Split rows into ones MSBuild confirmed it produced this run, and ones it never mentioned.

    An unmentioned assembly is not counted. Its numbers may even be right, but this script cannot
    establish that they describe THIS tree, and a number of unknown provenance in a baseline is
    worse than an absent one: it will be compared.
    """
    fresh, unverified = [], []
    for row in rows:
        if row["assembly"] in built:
            fresh.append(row)
        else:
            row["reason"] = ("MSBuild never reported building this assembly in this run, so "
                             "--no-build ran a pre-existing artifact of unknown provenance")
            unverified.append(row)
    return fresh, unverified


def load_expected(path):
    """Read a committed baseline to gate against. Returns (by_assembly, doc); raises ValueError.

    *** THIS IS A SECOND IMPLEMENTATION OF A COMPARISON THAT ALSO LIVES IN verify-scrub.py, AND THE
    DUPLICATION IS DELIBERATE RATHER THAN OVERLOOKED. *** That one answers "did the scrub break a
    test" during a history rewrite and needs a canary, a needle vocabulary and a clone. This one
    answers "did this commit break a test" and must run on a CI runner with none of those. Wiring CI
    through the scrub verifier to reuse thirty lines would drag the whole Gate 3 apparatus - and its
    gitignored sanitization/ inputs, which a fresh clone does not have - into every pull request.

    The rules are kept identical on purpose and the tests below mirror the eight already proven
    against its twin. If you change one, change both: they are the same claim about the same file
    format, and a divergence would mean a run could pass one gate and fail the other.
    """
    try:
        doc = json.loads(io.open(path, encoding="utf-8-sig").read())
    except Exception as exc:
        raise ValueError("--expect '%s' is not readable JSON: %s" % (path, exc))
    rows = doc.get("assemblies")
    if not isinstance(rows, list):
        raise ValueError("--expect '%s' carries no 'assemblies' list." % path)
    by = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("--expect '%s' has a row that is not an object." % path)
        name = row.get("assembly")
        if not name:
            raise ValueError("--expect '%s' has a row with no 'assembly' name." % path)
        if name in by:
            raise ValueError("--expect '%s' lists assembly '%s' twice - one of those rows is wrong "
                             "and this tool cannot tell which." % (path, name))
        try:
            by[name] = (int(row["passed"]), int(row["failed"]))
        except (KeyError, TypeError, ValueError):
            raise ValueError("--expect '%s' row '%s' has no integer passed/failed." % (path, name))
    return by, doc


def compare_to_expected(expected, expected_doc, rows, cannot_build):
    """Returns (printable lines, findings). Asymmetric, for the same reason its twin is.

    A test that stopped passing is a regression. A test that started passing is not evidence of
    anything and MUST NOT be able to cancel a loss elsewhere - so gains are reported and left alone,
    and only losses gate. Keyed per assembly, never on the total: 10+5 before and 5+10 after nets to
    zero while a whole project's tests have moved.
    """
    actual = {r["assembly"]: (r["passed"], r["failed"]) for r in rows}
    lines, findings = [], []
    shared = sorted(set(expected) & set(actual))
    vanished = sorted(set(expected) - set(actual))
    appeared = sorted(set(actual) - set(expected))

    lines.append("baseline comparison        : %d assembly/assemblies compared against %s"
                 % (len(shared), "the expected baseline"))
    if appeared:
        lines.append("  %d new assembly/assemblies (reported, does NOT gate): %s"
                     % (len(appeared), ", ".join(appeared)))

    if vanished:
        findings.append("%d assembly/assemblies in the baseline did not run (%s). A test that no "
                        "longer runs has not passed - it has stopped being asked."
                        % (len(vanished), ", ".join(vanished)))
    for name in shared:
        was_p, was_f = expected[name]
        now_p, now_f = actual[name]
        if now_p < was_p:
            findings.append("%s dropped from %d passing to %d." % (name, was_p, now_p))
        if now_f > was_f:
            findings.append("%s went from %d failing to %d." % (name, was_f, now_f))
        if now_p > was_p:
            lines.append("  %s gained %d pass(es) - reported, does NOT gate. If a known failure was "
                         "fixed, re-capture the baseline so it cannot linger pretending to still be "
                         "one." % (name, now_p - was_p))

    was_gated = set(r.get("target") for r in expected_doc.get("cannotBuild", []) if isinstance(r, dict))
    now_gated = set(r.get("target") for r in cannot_build if isinstance(r, dict))
    newly = sorted(t for t in (now_gated - was_gated) if t)
    if was_gated or now_gated:
        lines.append("  targets that cannot build  : %d expected, %d now"
                     % (len(was_gated), len(now_gated)))
    if newly:
        findings.append("%d target(s) built in the baseline and do not build now: %s"
                        % (len(newly), ", ".join(newly)))
    return lines, findings


def parse_errors(text):
    """Distinct (code, project) build errors, so a refusal records a reason and not a wall."""
    found, order = {}, []
    for line in text.splitlines():
        m = ERROR_LINE.search(line)
        if not m:
            continue
        code, message, proj = m.group(1), m.group(2), m.group(3) or ""
        key = (code, os.path.basename(proj))
        if key not in found:
            found[key] = message
            order.append(key)
    return [{"code": c, "project": p, "example": found[(c, p)]} for c, p in order]


def capture_target(root, dotnet, target, config):
    """Build then test one solution or project. Returns (rows, cannot_entry or None, built names)."""
    print("  %-52s building..." % target, end="")
    sys.stdout.flush()
    code, build_out = run([dotnet, "build", "-c", config, target, "--nologo"], root)
    cannot = None
    if code != 0:
        errors = parse_errors(build_out)
        cannot = {"target": target, "exitCode": code, "errors": errors[:12]}
        print(" FAILED (%d distinct error(s))" % len(errors), end="")
    else:
        print(" ok", end="")
    sys.stdout.flush()

    # Tests are attempted even after a partial build failure: a solution where one project cannot
    # build still has assemblies that can, and dropping them would understate the baseline - which
    # makes the post-scrub run look like a gain and hides a real loss underneath it.
    print(" testing...", end="")
    sys.stdout.flush()
    _, test_out = run([dotnet, "test", "-c", config, "--no-build", target, "--nologo"], root)
    rows = parse_assemblies(test_out, target)
    failures = parse_failed_tests(test_out)
    print(" %d assembly/assemblies" % len(rows), end="")
    # PRINTED AT THE POINT OF CAPTURE, not only at the gate. The gate compares counts and may be
    # run with a baseline that already tolerates these; the names still belong in the log, because
    # the run that produced them is the only one that had them.
    if failures:
        print("  [%d failing: %s]" % (len(failures),
                                      ", ".join(printable(f) for f in failures[:4])
                                      + (", ..." if len(failures) > 4 else "")), end="")
    print()
    return rows, cannot, built_assemblies(build_out), failures


def main():
    ap = argparse.ArgumentParser(description="Capture per-assembly test counts for Gate 3.")
    ap.add_argument("--repo", default=".", help="repository to capture")
    ap.add_argument("--out", default="sanitization/build-baseline.json")
    ap.add_argument("--config", default="Release")
    ap.add_argument("--dotnet", default="dotnet")
    ap.add_argument("--note", default="")
    ap.add_argument("--expect", default=None,
                    help="a committed baseline to GATE against. Exit 1 if this run lost an assembly, "
                         "lost a pass, gained a failure, or gained an unbuildable target.")
    args = ap.parse_args()

    root = os.path.abspath(args.repo)
    if not os.path.isdir(root):
        print("REFUSED: '%s' is not a directory." % args.repo, file=sys.stderr)
        return 3

    code, _ = run([args.dotnet, "--version"], root)
    if code != 0:
        print("REFUSED: '%s' is not runnable. Without an SDK this script can only produce an empty "
              "capture, and an empty capture compares equal to another empty one." % args.dotnet,
              file=sys.stderr)
        return 3
    _, sdk = run([args.dotnet, "--version"], root)

    # *** VALIDATED BEFORE THE BUILD, NOT AFTER IT. ***
    # Two reasons, and the second one is why this moved. A bad --expect path should refuse in a
    # second rather than after a five-minute build and test cycle. And while it was validated at the
    # END, the case meant to guard it was VACUOUS: the fixture never produced an assembly, so the run
    # exited 2 at "NOTHING EXAMINED" and never reached this check at all. The test asserted an exit
    # code that a completely different refusal was producing. Found by mutating the guard and
    # watching nothing turn red.
    expected, expected_doc = None, None
    if args.expect:
        if not os.path.isfile(args.expect):
            print("NOTHING COMPARED: --expect '%s' does not exist. A MISSING baseline is a refusal, "
                  "not a silent downgrade to 'no gate'. EMPTY IS NOT CLEAN." % args.expect,
                  file=sys.stderr)
            return 2
        try:
            expected, expected_doc = load_expected(args.expect)
        except ValueError as exc:
            print("NOTHING COMPARED: %s EMPTY IS NOT CLEAN." % exc, file=sys.stderr)
            return 2
        if not expected:
            print("NOTHING COMPARED: the expected baseline lists ZERO assemblies, so no test would "
                  "be compared. An empty baseline is not a clean one.", file=sys.stderr)
            return 2

    slns, projs = walk_projects(root)
    orphans = orphan_projects(root, slns, projs)
    orphan_tests = [p for p in orphans if "test" in os.path.basename(p).lower()]

    print("discovered : %d solution(s), %d project(s), %d orphan project(s) in no solution"
          % (len(slns), len(projs), len(orphans)))
    if not slns and not orphan_tests:
        print("\nNOTHING EXAMINED: no solution and no orphan test project was discovered under "
              "'%s'. EMPTY IS NOT CLEAN - this is a refusal, not a capture of a repository that "
              "happens to have no tests." % args.repo, file=sys.stderr)
        return 2

    rows, cannot_build, built = [], [], set()
    failed_by_target = {}
    for target in slns:
        got, cannot, made, failing = capture_target(root, args.dotnet, target, args.config)
        rows.extend(got)
        built |= made
        if failing:
            failed_by_target[target] = failing
        if cannot:
            cannot_build.append(cannot)
    for target in orphan_tests:
        got, cannot, made, failing = capture_target(root, args.dotnet, target, args.config)
        if failing:
            failed_by_target[target] = failing
        for row in got:
            row["orphan"] = True
            row["note"] = "In NO solution. A CI that iterates *.sln skips this silently."
        rows.extend(got)
        built |= made
        if cannot:
            cannot_build.append(cannot)

    rows, unverified = partition_unverified(rows, built)

    # *** THE NAMES MUST AGREE WITH THE COUNT. *** A target whose assemblies were all EXCLUDED -
    # MSBuild never built them, so --no-build ran a leftover artifact - contributes no failures to
    # the totals, and its names must not appear either. Measured on the first run of this feature:
    # 26 names were recorded against a capture reporting 2 failures, 24 of them from a stale
    # openness-cli artifact. A list that disagrees with the number beside it is worse than no list.
    counted_targets = {r["target"] for r in rows}
    failed_tests = [n for t, names in sorted(failed_by_target.items()) if t in counted_targets
                    for n in names]

    if not rows:
        print("\nNOTHING EXAMINED: %d target(s) were built and tested and NOT ONE produced a "
              "verified assembly. EMPTY IS NOT CLEAN." % (len(slns) + len(orphan_tests)),
              file=sys.stderr)
        return 2

    _, head = run(["git", "rev-parse", "HEAD"], root)
    _, branch = run(["git", "rev-parse", "--abbrev-ref", "HEAD"], root)

    doc = {
        "schema": "ladder-ai/build-baseline/1",
        "capturedAt": time.strftime("%Y-%m-%d"),
        "capturedBy": "tools/capture-build-baseline.py",
        # Recorded so a gate failure is ACTIONABLE from a machine that did not run it.
        "failedTests": failed_tests,
        "commit": head.strip(),
        "branch": branch.strip(),
        "sdk": sdk.strip(),
        "configuration": args.config,
        "note": args.note or ("Per-assembly counts for verify-scrub.py. Compare a post-rewrite "
                              "capture against this one PER ASSEMBLY: a total-only comparison "
                              "nets a loss in one assembly against a gain in another."),
        "totals": {
            "assemblies": len(rows),
            "passed": sum(r["passed"] for r in rows),
            "failed": sum(r["failed"] for r in rows),
            "skipped": sum(r["skipped"] for r in rows),
            "total": sum(r["total"] for r in rows),
        },
        "assemblies": sorted(rows, key=lambda r: r["assembly"]),
        "cannotBuild": cannot_build,
        # Recorded, never counted. These ran out of bin/ without MSBuild producing them in this
        # run, so their numbers describe some earlier tree. Kept visible because an omission the
        # reader cannot see is indistinguishable from a tree that never had those tests.
        "unverifiedAssemblies": sorted(unverified, key=lambda r: r["assembly"]),
    }

    out_dir = os.path.dirname(os.path.abspath(args.out))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    io.open(args.out, "w", encoding="utf-8", newline="\n").write(
        json.dumps(doc, indent=2, sort_keys=False))

    t = doc["totals"]
    print("\nwritten : %s" % args.out)
    print("  %d assembly/assemblies, %d passed, %d failed, %d skipped"
          % (t["assemblies"], t["passed"], t["failed"], t["skipped"]))
    if unverified:
        print("  %d assembly/assemblies NOT COUNTED - MSBuild never built them in this run, so "
              "--no-build ran a leftover artifact:" % len(unverified))
        for row in unverified:
            print("      %-40s %d passed, %d failed  (EXCLUDED)"
                  % (row["assembly"], row["passed"], row["failed"]))
    if cannot_build:
        print("  %d target(s) COULD NOT BUILD - recorded, not hidden:" % len(cannot_build))
        for entry in cannot_build:
            codes = sorted(set(e["code"] for e in entry["errors"]))
            print("      %-48s %s" % (entry["target"], ", ".join(codes) or "no error parsed"))
    if t["failed"]:
        print("  NOTE: %d test(s) already fail. That is fine for a baseline and is the POINT of "
              "one - what gates later is a RISE in this number, not the number itself."
              % t["failed"])

    if not args.expect:
        return 0

    # ---- the gate ---------------------------------------------------------------------------------
    # `expected` was read and validated before the build; by here it cannot be missing or malformed.
    print()
    gate_lines, findings = compare_to_expected(expected, expected_doc, rows, cannot_build)
    for line in gate_lines:
        print(line)
    if findings:
        print("\n--- BUILD GATE FAILED ---")
        for f in findings:
            print("  " + f)
        # *** THE NAMES, BECAUSE A COUNT IS NOT ACTIONABLE. *** This block exists because a CI run
        # on 2026-09-21 reported "went from 0 failing to 1" and the name was nowhere: it had been
        # parsed out of dotnet test's stdout and dropped. On a hosted runner whose raw logs are 403
        # to anonymous callers, that made a one-line fix into an unanswerable question.
        if failed_tests:
            print("\n  THE %d FAILING TEST(S) THIS RUN SAW, by name:" % len(failed_tests))
            for name in failed_tests:
                print("      %s" % printable(name))
        else:
            print("\n  NO TEST NAME WAS CAPTURED. The counts moved but dotnet test printed no line "
                  "this\n  tool could match, so the name is genuinely unavailable rather than "
                  "withheld - say so\n  rather than guessing, and re-run with the console logger "
                  "at normal verbosity.")
        print("\nA test that used to pass does not pass now. Fix it, or - if the change is "
              "deliberate - re-capture %s in its own commit, saying what moved and why."
              % args.expect)
        return 1
    print("\nBUILD GATE PASSED: no assembly lost, no pass lost, no new failure, no new "
          "unbuildable target.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
