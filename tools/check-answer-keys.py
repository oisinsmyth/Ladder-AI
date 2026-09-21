# -*- coding: utf-8 -*-
"""Is each validation case's answer key still the file it was, and does it exist once? (3-C.)

    python tools/check-answer-keys.py [--repo .] [--register tools/answer-keys.txt]

WHY THIS EXISTS. A validation case grades a generated block against an answer key. Measured
2026-09-21: the key for MotorVSDSystem-purpose existed as FOUR byte-identical copies, one of them
tracked as ordinary reuse corpus, and nothing recorded which was authoritative. Editing the bench
corpus would have moved a validation case's ground truth SILENTLY and the case would still have
graded. A pin makes that impossible to do quietly.

WHAT IT IS NOT. It is not M-5, not Gate 3 and not M-22. Those three all ask about VOCABULARY - is
there an identifier in here. This asks about IDENTITY: is this file still the file, and is it the
only one. A key can be perfectly clean and still be the wrong bytes.

THE THREE CHECKS, and only the first two gate:
  1. THE PIN.  The canonical path exists and hashes to the declared sha256. This is the whole
     point of the register. A tracked key that has vanished gates; an UNTRACKED one that is simply
     absent from this working tree does not, because it is gitignored and a fresh clone has
     never had it.
  2. UNIQUE IN THE REPOSITORY.  No other TRACKED path carries the same bytes. Compared by git blob
     sha, which IS the content hash - identical content is the same blob, always. A second tracked
     copy gates: git will let the two drift apart forever and nothing would say which one graded.
  3. COPIES IN THE WORKING TREE.  Counted and printed. REPORTED, NEVER GATED, and the reason is
     the finding this whole item came from: A GITIGNORE DOES NOT REMOVE A FILE FROM THE TREE.
     Deleting today's copy does not stop tomorrow's, so a count printed every run is worth more
     than a deletion. The question those copies really belong to - what could the agent READ - is
     M-23, and this tool does not pretend to answer it.

WHAT IT CANNOT SEE, stated because a limit nobody prints is a limit nobody knows about:
  - a key nobody declared. Silence is not a claim, so silence is not checked. The register has to
    be the only way to name ground truth, or it is one of two ways and they will disagree.
  - a copy that is not byte-identical. A re-export, a reformat, a CRLF flip or one edited line all
    read as unrelated files here. This finds DUPLICATES, not DESCENDANTS.
  - anything under Live Runs/. Deliberately not walked: its paths are live-run vocabulary and this
    output gets pasted into notes. A record of a leak must not be a copy of it.
  - whether the key was READ. That is the whole of M-23 and no hash answers it.

EXIT CODES, per this project's standing contract:
    0  every declared key is at its pin and is the only tracked copy of itself.
    1  FOUND SOMETHING. A worklist follows. Triage it; do not read the total as a severity.
    2  NOTHING WAS EXAMINED - no register, an empty one, or not a git repository. EMPTY IS NOT
       CLEAN, and a register that declares nothing proves nothing.

Paths and hashes only. No block content is ever read into the output - the files this tool hashes
are LAD, and hard rule 8 says this program does not get to look at them. It never does: it hashes
bytes and compares, and it prints no line of any file it opens.
"""
import argparse
import hashlib
import io
import os
import subprocess
import sys

EXIT_OK = 0
EXIT_FOUND = 1
EXIT_CANNOT_RUN = 2

# A Windows console is cp1252 here and a register reason is free text. A checker that DIES on a
# character in its own input is worse than useless - it fails in a way that looks like a finding.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except (AttributeError, ValueError):                            # a pipe that will not be retuned
    pass

# Never walked. Live-run paths are themselves vocabulary, and the cost of printing one into a note
# is the whole data boundary. .git holds compressed objects that would never byte-match anyway.
SKIP_DIRS = ("Live Runs", ".git")


def git_lines(repo, *args):
    """git stdout as text lines, or None if git itself could not answer."""
    try:
        out = subprocess.run(("git",) + args, cwd=repo, stdout=subprocess.PIPE,
                             stderr=subprocess.DEVNULL)
    except OSError:
        return None
    if out.returncode != 0:
        return None
    return out.stdout.decode("utf-8", "replace").splitlines()


def sha256_of(path):
    h = hashlib.sha256()
    with io.open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def parse_register(path):
    """[(case, rel_path, sha256, reason, line_no)], or None if the file is not there at all."""
    if not os.path.isfile(path):
        return None
    entries = []
    for line_no, raw in enumerate(io.open(path, encoding="utf-8"), 1):
        body, _, reason = raw.partition("#")
        if not body.strip():
            continue
        fields = [f.strip() for f in body.split("|")]
        if len(fields) != 3:
            raise SystemExit("tools/answer-keys.txt line %d: expected 'case | path | sha256', "
                             "got %d field(s)" % (line_no, len(fields)))
        case, rel_path, digest = fields
        entries.append((case, rel_path, digest.lower(), reason.strip(), line_no))
    return entries


def tracked_blobs(repo):
    """{blob_sha: [rel_path, ...]} for every tracked file. Identical content IS the same blob."""
    lines = git_lines(repo, "ls-files", "-s")
    if lines is None:
        return None
    by_blob = {}
    for line in lines:
        meta, _, rel_path = line.partition("\t")
        parts = meta.split()
        if len(parts) < 3:
            continue
        by_blob.setdefault(parts[1], []).append(rel_path)
    return by_blob


def copies_in_working_tree(repo, wanted):
    """Walk once, hashing ONLY files whose size matches a declared key. {sha256: [rel_path, ...]}.

    The size filter is what makes this cheap enough to run every time. A repository this size holds
    a lot of large files and hashing all of them to find four would be a check nobody runs.
    """
    sizes = set(wanted.values())
    found = {}
    for dirpath, dirnames, filenames in os.walk(repo):
        rel_dir = os.path.relpath(dirpath, repo).replace("\\", "/")
        if rel_dir == ".":
            rel_dir = ""
        dirnames[:] = [d for d in dirnames
                       if (d if not rel_dir else rel_dir + "/" + d) not in SKIP_DIRS
                       and d not in SKIP_DIRS]
        for name in filenames:
            full = os.path.join(dirpath, name)
            try:
                if os.path.getsize(full) not in sizes:
                    continue
                digest = sha256_of(full)
            except (OSError, ValueError):
                continue
            if digest in wanted:
                rel = os.path.relpath(full, repo).replace("\\", "/")
                found.setdefault(digest, []).append(rel)
    return found


def main():
    ap = argparse.ArgumentParser(description="Answer-key pin and duplication check (3-C).")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--register", default=None)
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    register = args.register or os.path.join(repo, "tools", "answer-keys.txt")

    entries = parse_register(register)
    if entries is None:
        print("CANNOT RUN: no register at %s." % register)
        print("EMPTY IS NOT CLEAN - a check with nothing declared examined nothing.")
        return EXIT_CANNOT_RUN
    if not entries:
        print("CANNOT RUN: the register at %s declares no key." % register)
        print("EMPTY IS NOT CLEAN - a register that declares nothing proves nothing.")
        return EXIT_CANNOT_RUN

    by_blob = tracked_blobs(repo)
    if by_blob is None:
        print("CANNOT RUN: %s is not a git repository, or git could not list it." % repo)
        print("EMPTY IS NOT CLEAN - check 2 compares against the tracked set and there is none.")
        return EXIT_CANNOT_RUN

    tracked = {p for paths in by_blob.values() for p in paths}
    findings = []

    print("repository            : %s" % repo)
    print("register              : %s" % register)
    print("declared              : %d answer key(s)" % len(entries))
    print("tracked files         : %d (the population check 2 compares against)" % len(tracked))
    print("not walked            : %s - live-run paths are vocabulary and this output gets pasted"
          % ", ".join(SKIP_DIRS))

    # ---- 1. THE PIN ------------------------------------------------------------------------------
    print("\n--- 1. THE PIN: is each key still the bytes it was declared as? ---")
    present = {}
    for case, rel_path, digest, reason, line_no in entries:
        full = os.path.join(repo, rel_path.replace("/", os.sep))
        is_tracked = rel_path in tracked
        label = "tracked" if is_tracked else "UNTRACKED"
        if not os.path.isfile(full):
            if is_tracked:
                print("  GONE      %-22s %s" % (case, rel_path))
                findings.append("'%s': the tracked canonical path '%s' does not exist. The ground "
                                "truth this case grades against is missing." % (case, rel_path))
            else:
                print("  absent    %-22s %s  [%s - gitignored, not in a fresh clone; not a finding]"
                      % (case, rel_path, label))
            continue
        actual = sha256_of(full)
        present[actual] = os.path.getsize(full)
        if actual == digest:
            print("  ok        %-22s %s  [%s]" % (case, rel_path, label))
        else:
            print("  MOVED     %-22s %s  [%s]" % (case, rel_path, label))
            findings.append("'%s': '%s' no longer matches its pin. GROUND TRUTH HAS MOVED. Declared "
                            "%s..., found %s.... Either the edit was intended - in which case the "
                            "register is what needs updating, deliberately, in its own commit - or "
                            "a validation case has been silently regraded."
                            % (case, rel_path, digest[:12], actual[:12]))

    # ---- 2. UNIQUE IN THE REPOSITORY -------------------------------------------------------------
    print("\n--- 2. IS IT THE ONLY TRACKED COPY OF ITSELF? ---")
    for case, rel_path, digest, reason, line_no in entries:
        if rel_path not in tracked:
            print("  n/a       %-22s untracked, so there is no tracked copy to be second" % case)
            continue
        blob = None
        for sha, paths in by_blob.items():
            if rel_path in paths:
                blob = sha
                break
        twins = [p for p in by_blob.get(blob, []) if p != rel_path]
        if not twins:
            print("  unique    %-22s %s" % (case, rel_path))
        else:
            print("  DUPLICATE %-22s %s" % (case, rel_path))
            for t in twins:
                print("                + also tracked at  %s" % t)
            findings.append("'%s': %d other TRACKED path(s) carry the same bytes. Git will let them "
                            "drift apart and nothing would then say which one graded." % (case, len(twins)))

    # ---- 3. COPIES IN THE WORKING TREE -----------------------------------------------------------
    print("\n--- 3. COPIES IN THE WORKING TREE (reported, does NOT gate) ---")
    if not present:
        print("  nothing to search for - no declared key is present in this working tree.")
    else:
        found = copies_in_working_tree(repo, present)
        for case, rel_path, digest, reason, line_no in entries:
            copies = [c for c in found.get(digest, []) if c != rel_path]
            worktrees = [c for c in copies if c.startswith(".claude/worktrees/")]
            others = [c for c in copies if c not in worktrees]
            print("  %-22s %d copy/copies outside the canonical path"
                  "  (%d working, %d worktree checkout(s))"
                  % (case, len(copies), len(others), len(worktrees)))
            for c in others:
                print("       readable copy  %s" % c)
            if worktrees:
                print("       + %d checkout(s) of the same commit under .claude/worktrees/, e.g. %s"
                      % (len(worktrees), worktrees[0]))
        print("  A GITIGNORE DOES NOT REMOVE A FILE FROM THE TREE. These are readable today and a\n"
              "  deletion would not stop the next one. What could actually be read is M-23's\n"
              "  question, and this tool does not answer it.")

    # ---- the reasons, every run ------------------------------------------------------------------
    print("\n--- WHY EACH KEY IS WHERE IT IS (printed every run so it can be disagreed with) ---")
    for case, rel_path, digest, reason, line_no in entries:
        print("  %s" % case)
        print("      %s" % (reason or "NO REASON GIVEN - that is itself a finding for a reader."))

    # ---- Verdict ---------------------------------------------------------------------------------
    if not findings:
        print("\nCLEAN: every declared key is at its pin, and every tracked one is the only tracked "
              "copy of itself.")
        print("THIS PROVES IDENTITY OVER A REGISTER, NOT BLINDNESS. It says ground truth has not "
              "moved and\nis not forked in the repository. It says NOTHING about what any agent "
              "could read - a key can be\nperfectly pinned and sitting in plain sight, which is "
              "exactly the case for two of these.")
        return EXIT_OK

    print("\n--- WORKLIST: %d finding(s) ---" % len(findings))
    for f in findings:
        print("  %s" % f)
    print("\nA MOVED PIN IS NOT AUTOMATICALLY A BUG - it is an UNDECLARED change to ground truth. "
          "The remedy\nis to decide which file is right and say so in the register, in a commit "
          "that does only that.\nIt is never to relax the check.")
    return EXIT_FOUND


if __name__ == "__main__":
    sys.exit(main())
