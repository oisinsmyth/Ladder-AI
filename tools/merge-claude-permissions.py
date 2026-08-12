"""Merge Claude Code permission rules from one settings.local.json into another.

WHY THIS EXISTS
---------------
`.claude/settings.local.json` is PER DIRECTORY. A git worktree gets its own, so
permission rules granted while working in a worktree do NOT exist in the main
checkout. Leaving the worktree silently loses every rule granted there: the
tooling still exists and is still TIA-approved, but nothing is permitted to run it.

Measured 2026-08-12. Exiting a worktree dropped 16 rules in one step, including
every openness-cli rule for the live project, download-probe, and the Release
converter. Nothing announced it.

WHAT THIS DOES
--------------
Adds rules present in SOURCE but missing from TARGET. It never removes, reorders
or rewrites an existing rule, and it is idempotent: running it twice adds nothing
the second time.

WHAT IT REFUSES TO DO
---------------------
A malformed settings.local.json SILENTLY DISABLES EVERY RULE IN IT. No error, no
warning, the permissions simply stop applying. That has already bitten this project
once, via a hand-edit that omitted a comma. So this script:

  * parses BOTH files before touching anything, and aborts if either is invalid
  * writes to a temp file, RE-PARSES IT, and only then replaces the target
  * takes a timestamped backup first

Usage:  python merge-claude-permissions.py [SOURCE_SETTINGS] [TARGET_SETTINGS]
Exit:   0 = merged or already up to date;  1 = refused, nothing written
"""

import json
import os
import shutil
import sys
import time

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_SOURCE = os.path.join(
    REPO, ".claude", "worktrees", "JOB9004-marker-db", ".claude", "settings.local.json"
)
DEFAULT_TARGET = os.path.join(REPO, ".claude", "settings.local.json")


def load(path, label):
    if not os.path.isfile(path):
        print("REFUSED: %s not found:\n  %s" % (label, path))
        return None
    try:
        with open(path, "r", encoding="utf-8-sig") as handle:
            return json.load(handle)
    except Exception as exc:
        # Do not try to repair it. A settings file that will not parse is one whose
        # rules are already not applying, and guessing at the author's intent is how
        # a permission gets widened by accident.
        print("REFUSED: %s is not valid JSON, so nothing was changed." % label)
        print("  %s" % path)
        print("  %s" % exc)
        return None


def rules_of(doc):
    perms = doc.get("permissions")
    if not isinstance(perms, dict):
        return None
    allow = perms.get("allow")
    if allow is None:
        return []
    if not isinstance(allow, list):
        return None
    return allow


def main():
    source_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SOURCE
    target_path = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_TARGET

    print("source : %s" % source_path)
    print("target : %s" % target_path)
    print("")

    source = load(source_path, "SOURCE")
    target = load(target_path, "TARGET")
    if source is None or target is None:
        return 1

    source_rules = rules_of(source)
    target_rules = rules_of(target)
    if source_rules is None:
        print("REFUSED: SOURCE has no usable permissions.allow list.")
        return 1
    if target_rules is None:
        print("REFUSED: TARGET's permissions.allow is not a list.")
        return 1

    # Exact string match, deliberately not fuzzy. Two rules differing only in a path
    # or a trailing wildcard are DIFFERENT permissions, and treating them as the same
    # is how a narrow rule silently becomes a broad one.
    existing = set(target_rules)
    missing = [r for r in source_rules if r not in existing]

    if not missing:
        print("Already up to date. %d rule(s) in target, nothing to add." % len(target_rules))
        return 0

    print("%d rule(s) will be ADDED. Nothing is removed or modified.\n" % len(missing))
    for rule in missing:
        print("  + %s" % (rule if len(rule) <= 150 else rule[:147] + "..."))
    print("")

    backup = "%s.backup-%s" % (target_path, time.strftime("%Y%m%d-%H%M%S"))
    shutil.copy2(target_path, backup)
    print("backup : %s" % backup)

    target.setdefault("permissions", {})["allow"] = target_rules + missing

    # Write, re-read, and only then swap in. If the written file will not parse, the
    # target is left exactly as it was.
    temp = target_path + ".merge-tmp"
    with open(temp, "w", encoding="utf-8", newline="\r\n") as handle:
        json.dump(target, handle, indent=2)
        handle.write("\n")

    try:
        with open(temp, "r", encoding="utf-8-sig") as handle:
            verified = json.load(handle)
        count = len(verified["permissions"]["allow"])
    except Exception as exc:
        os.remove(temp)
        print("REFUSED: the merged file did not parse. Target unchanged.\n  %s" % exc)
        return 1

    os.replace(temp, target_path)
    print("")
    print("MERGED. %d rule(s) now allowed, was %d." % (count, len(target_rules)))
    print("Start a new Claude Code session for the change to take effect.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
