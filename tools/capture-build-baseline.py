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
loop over solutions, which is what a CI script naturally writes; src/hmi-cli's 161 tests are in
exactly that position. Discovery here is by project file, and solution membership is recorded
rather than assumed.

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
    print(" %d assembly/assemblies" % len(rows))
    return rows, cannot, built_assemblies(build_out)


def main():
    ap = argparse.ArgumentParser(description="Capture per-assembly test counts for Gate 3.")
    ap.add_argument("--repo", default=".", help="repository to capture")
    ap.add_argument("--out", default="sanitization/build-baseline.json")
    ap.add_argument("--config", default="Release")
    ap.add_argument("--dotnet", default="dotnet")
    ap.add_argument("--note", default="")
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
    for target in slns:
        got, cannot, made = capture_target(root, args.dotnet, target, args.config)
        rows.extend(got)
        built |= made
        if cannot:
            cannot_build.append(cannot)
    for target in orphan_tests:
        got, cannot, made = capture_target(root, args.dotnet, target, args.config)
        for row in got:
            row["orphan"] = True
            row["note"] = "In NO solution. A CI that iterates *.sln skips this silently."
        rows.extend(got)
        built |= made
        if cannot:
            cannot_build.append(cannot)

    rows, unverified = partition_unverified(rows, built)

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
    return 0


if __name__ == "__main__":
    sys.exit(main())
