#!/usr/bin/env python3
"""Has a block been re-traced since the clauses governing it changed?

WHY THIS EXISTS. On 2026-09-01 the first complete conformance wave returned four failures against
`FB_HopperBlockageMonitor`, all one symptom. The block was not broken when it was written: it
implements the reading of REQ-HBA-005 that was current on 2026-07-20, the day it was last edited.
`AR-HBA-05` overturned that reading on 2026-08-13 - "edge, not level", and "the accumulated
persistence time is untouched by reset" - and the block was never re-traced against the amended
clause. Twenty-four days, four failing vectors, one rig, 34,922 round trips, and a defect that was
statically visible in four lines of IR the whole time.

NOTHING ASKED THE QUESTION. That is the point. `review-functional` traces a block against a register
and would have caught it - but nothing said "this block's register moved under it, go and re-run
that." The result package already reports the same shape one level down for vectors
(`boundsCurrency: NOTHING ASKED WHETHER THIS VECTOR STILL TESTS THE SPECIFIED BOUND`); this is the
missing half at the block level.

WHAT IT IS AND IS NOT. It compares DATES, not meaning: the last commit touching a block's `.ir`
against the commit that introduced each ruling in its request's register. It cannot tell you whether
a ruling actually bears on a block - only that the register moved afterwards and nobody looked. That
is a deliberately coarse check with a low false-negative rate, which is the right trade for a
question nobody was asking at all.

IT EXITS NON-ZERO ON A FINDING, ON PURPOSE. The reader who investigated this put it plainly: a
warning here would have been skimmed exactly as the original was. A stale trace is not a note.

EXIT CODES, per this project's standing contract:
    0  every named block is current against its register
    1  at least one block predates a ruling - A FINDING, not a warning
    2  NOTHING WAS EXAMINED - no register, no rulings parsed, or no block resolved.
       EMPTY IS NOT CLEAN: exit 2 is never a pass.
"""
import argparse
import os
import re
import subprocess
import sys

# A ruling heading: `### AR-HBA-05 — reset is edge-triggered ...`. The id is what gets cited
# elsewhere, so it is what this reports.
RULING = re.compile(r"^#{2,4}\s+(?P<id>A[A-Z]-[A-Z0-9]+-\d+)\b(?P<rest>.*)$", re.MULTILINE)


def git_date(repo, path, pattern=None):
    """Author-date (YYYY-MM-DD) of the last commit touching `path`, or the FIRST commit that
    introduced `pattern` into it. Returns None when git knows nothing - an untracked file has no
    history and must not be silently treated as current."""
    cmd = ["git", "log", "--format=%ad", "--date=short"]
    if pattern:
        cmd += ["-S", pattern, "--reverse"]
    else:
        cmd += ["-1"]
    cmd += ["--", path]
    out = subprocess.run(cmd, cwd=repo, capture_output=True, text=True).stdout.strip().split("\n")
    out = [line for line in out if line]
    return out[0] if out else None


def main():
    ap = argparse.ArgumentParser(description="Block-vs-register currency check.")
    ap.add_argument("--repo", default=".", help="repository root")
    ap.add_argument("--register", required=True, help="the request's requirements.md")
    ap.add_argument("--block", action="append", required=True,
                    help="a block .ir governed by that register; repeatable")
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    register_rel = os.path.relpath(os.path.abspath(args.register), repo).replace("\\", "/")

    if not os.path.isfile(os.path.join(repo, register_rel)):
        print(f"NOTHING EXAMINED: no register at '{register_rel}'.")
        return 2

    with open(os.path.join(repo, register_rel), "r", encoding="utf-8", errors="replace") as f:
        text = f.read()

    rulings = [(m.group("id"), m.group("rest").strip(" —-")) for m in RULING.finditer(text)]
    if not rulings:
        print(f"NOTHING EXAMINED: '{register_rel}' parsed to ZERO rulings. Either the register uses "
              f"a heading shape this does not recognise, or it has none - and those are different "
              f"problems. This is a refusal, not a clean sheet.")
        return 2

    # Date each ruling by the commit that FIRST introduced its id into the register.
    dated = []
    for rid, title in rulings:
        d = git_date(repo, register_rel, pattern=rid)
        if d:
            dated.append((rid, title, d))

    if not dated:
        print(f"NOTHING EXAMINED: {len(rulings)} ruling(s) found in '{register_rel}' and NONE could "
              f"be dated from git history. The register may be untracked or newly added.")
        return 2

    print(f"register : {register_rel}")
    print(f"rulings  : {len(dated)} dated of {len(rulings)} found")

    findings, examined = [], 0
    for block in args.block:
        rel = os.path.relpath(os.path.abspath(block), repo).replace("\\", "/")
        bdate = git_date(repo, rel)
        if not bdate:
            print(f"  ?  {rel}: NO GIT HISTORY - cannot be shown current, so it is not claimed to be.")
            continue

        examined += 1
        later = sorted([r for r in dated if r[2] > bdate], key=lambda r: r[2])
        if later:
            findings.append((rel, bdate, later))
            print(f"  STALE {rel}: last changed {bdate}; {len(later)} ruling(s) landed AFTER it")
            for rid, title, d in later:
                print(f"       {d}  {rid}  {title[:78]}")
        else:
            print(f"  OK {rel}: last changed {bdate}; no ruling is newer")

    if examined == 0:
        print("\nNOTHING EXAMINED: no named block had git history. EMPTY IS NOT CLEAN.")
        return 2

    if findings:
        print(f"\nSTALE TRACE: {len(findings)} of {examined} block(s) predate a ruling in their own "
              f"register.\nThe register moved and nothing re-traced the block against it. Re-run "
              f"`review-functional` for each, and record the result - a clause that changed under a "
              f"block is how a correct block becomes a wrong one without anybody editing it.")
        return 1

    print(f"\nCURRENT: all {examined} block(s) postdate every ruling in this register.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
