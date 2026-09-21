# -*- coding: utf-8 -*-
"""Was a validation case's quarantine ever capable of hiding anything? (M-23, the buildable half.)

    python tools/check-readable-scope.py [--repo .] [--register tools/readable-scope.txt]
                                         [--manifest <file>] [--case <name>]

WHY THIS EXISTS. A blind validation is worth exactly its blindness, and blindness here was
UNRECORDABLE: `gen/telemetry.log` has no field for what an agent could read and never did, so the
2026-09-20 audit of the MotorVSDSystem-purpose run came back neither cleared nor impeached. Every
artifact asserting blindness was written by the party whose blindness is in question.

*** A GITIGNORE IS A COMMIT FENCE, NOT A READ FENCE. *** That sentence is AB-2's central finding.
The original quarantine's own commit message stated the goal as the generator "never being able to
discover it in the tree", and ignoring a file does not remove it from the tree.

WHAT IT CHECKS
  1. allow and quarantine do not overlap - a path declared both ways is a contradiction.
  2. every declared path exists. A rule about a file that is not there fences nothing.
  3. NO QUARANTINED FILE HAS A BYTE-IDENTICAL TWIN ANYWHERE IN THE TREE.  <-- the clause with teeth
  4. a TRACKED quarantined file is a VOID quarantine, reported every run.
  5. with --manifest: the paths a run received are a subset of allow.

🔴 CHECK 3 IS DELIBERATELY WIDER THAN THE DESIGN M-23 WAS FILED WITH, and the difference is the
only reason it works. The filed clause was "no declared-quarantine path is byte-identical to
anything inside the ALLOWED SET". Measured 2026-09-21 against the real history: the vector was a
copy in `scratch/`, which was never in the allowed set, so the filed clause would have passed it.
Only "no twin ANYWHERE readable" reaches it. A check that cannot catch the case that motivated it
is a check that will be trusted and should not be.

WHAT IT CANNOT DO - the hard half of M-23, unsolved and stated rather than hidden:
  - it cannot tell you what an agent ACTUALLY read. Nothing emits that. Until something does, the
    run half of this tool examines nothing, and it says so in those words rather than passing.
  - a --manifest an agent wrote about itself is the same self-report in a new location. The honest
    first version records what the DISPATCHER passed, which is at least a different party.
  - a copy that is not byte-identical - a re-export, a reformat, one edited line - is invisible
    here. This finds DUPLICATES, not DESCENDANTS.
  - anything under Live Runs/, deliberately not walked: its paths are themselves vocabulary.

*** A CLEAN VERDICT MEANS THE SCOPE IS COHERENT AND THE QUARANTINE IS NOT DUPLICATED. *** It has
never meant "the generator was blind" and the verdict text refuses that reading explicitly.

EXIT CODES, per this project's standing contract:
    0  every declared case is coherent and no quarantined file is duplicated.
    1  FOUND SOMETHING. A worklist follows.
    2  NOTHING WAS EXAMINED - no register, an empty one, or no case selected. EMPTY IS NOT CLEAN.
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

SKIP_DIRS = ("Live Runs", ".git")

# `void` IS A CONFESSION, NOT AN EXEMPTION, and the distinction is the whole integrity of this tool.
#   quarantine - the case ASSERTS this file was hidden. A twin anywhere, or tracked status, GATES,
#                because the assertion is then false.
#   void       - the case DECLARES the fence defeated and unrepairable. Twins are reported, loudly,
#                every run, and do not gate - there is no false assertion left to refuse. The price
#                is permanent and stated in the verdict: a case with a void fence CAN NEVER BE
#                CLEARED, by this tool or any audit, and the summary says so every single run.
# Downgrading `quarantine` to `void` to get a run through therefore buys nothing: it trades a
# failing check for a permanent, printed statement that the case's blindness is unprovable.
KINDS = ("allow", "quarantine", "void")

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except (AttributeError, ValueError):
    pass


def parse_register(path):
    """[(case, kind, rel_path, reason, line_no)], or None if the file is absent.

    A malformed row is REFUSED, never skipped - the same discipline as the term list, and for the
    same reason: a register that silently shrinks still produces output that looks correct.
    """
    if not os.path.isfile(path):
        return None
    entries, bad = [], []
    for line_no, raw in enumerate(io.open(path, encoding="utf-8"), 1):
        body, _, reason = raw.partition("#")
        if not body.strip():
            continue
        fields = [f.strip() for f in body.split("|")]
        if len(fields) != 3:
            bad.append((line_no, "expected 'case | allow|quarantine | path', got %d field(s)"
                        % len(fields)))
            continue
        case, kind, rel_path = fields
        if kind not in KINDS:
            bad.append((line_no, "kind must be one of %s, got %r" % (", ".join(KINDS), kind)))
            continue
        entries.append((case, kind, rel_path, reason.strip(), line_no))
    if bad:
        raise SystemExit("tools/readable-scope.txt: %d malformed row(s):\n%s"
                         % (len(bad), "\n".join("  line %d: %s" % b for b in bad)))
    return entries


def sha256_of(path):
    h = hashlib.sha256()
    with io.open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def files_under(repo, rel_path):
    """Every file at or beneath a declared path. A directory declaration covers its contents."""
    full = os.path.join(repo, rel_path.replace("/", os.sep))
    if os.path.isfile(full):
        return [rel_path]
    if not os.path.isdir(full):
        return []
    out = []
    for dirpath, dirnames, filenames in os.walk(full):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            out.append(os.path.relpath(os.path.join(dirpath, name), repo).replace("\\", "/"))
    return out


def twins_in_tree(repo, wanted):
    """{sha256: [rel_path, ...]} for files whose SIZE matches a quarantined file. Size filter first:
    hashing a repository this size to find a handful is a check nobody would run twice."""
    sizes = set(wanted.values())
    found = {}
    for dirpath, dirnames, filenames in os.walk(repo):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            full = os.path.join(dirpath, name)
            try:
                if os.path.getsize(full) not in sizes:
                    continue
                digest = sha256_of(full)
            except (OSError, ValueError):
                continue
            if digest in wanted:
                found.setdefault(digest, []).append(
                    os.path.relpath(full, repo).replace("\\", "/"))
    return found


def tracked_set(repo):
    try:
        out = subprocess.run(["git", "ls-files"], cwd=repo, stdout=subprocess.PIPE,
                             stderr=subprocess.DEVNULL)
    except OSError:
        return set()
    return set(out.stdout.decode("utf-8", "replace").splitlines()) if out.returncode == 0 else set()


def main():
    ap = argparse.ArgumentParser(description="Readable-scope check for validation cases (M-23).")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--register", default=None)
    ap.add_argument("--manifest", default=None,
                    help="a file of paths a run actually received, one per line")
    ap.add_argument("--case", default=None, help="check only this case")
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    register = args.register or os.path.join(repo, "tools", "readable-scope.txt")

    entries = parse_register(register)
    if entries is None:
        print("CANNOT RUN: no register at %s." % register)
        print("EMPTY IS NOT CLEAN - a check with nothing declared examined nothing.")
        return EXIT_CANNOT_RUN
    if args.case:
        entries = [e for e in entries if e[0] == args.case]
    if not entries:
        print("CANNOT RUN: the register declares no case%s."
              % (" named %r" % args.case if args.case else ""))
        print("EMPTY IS NOT CLEAN - a register that declares nothing proves nothing.")
        return EXIT_CANNOT_RUN

    tracked = tracked_set(repo)
    cases = {}
    for case, kind, rel_path, reason, line_no in entries:
        cases.setdefault(case, {"allow": [], "quarantine": [], "void": [], "reasons": []})
        cases[case][kind].append(rel_path)
        cases[case]["reasons"].append((kind, rel_path, reason))

    findings = []
    print("repository            : %s" % repo)
    print("register              : %s" % register)
    print("cases                 : %d" % len(cases))
    print("not walked            : %s - live-run paths are vocabulary" % ", ".join(SKIP_DIRS))

    unprovable = []
    for case in sorted(cases):
        allow = cases[case]["allow"]
        asserted = cases[case]["quarantine"]
        void = cases[case]["void"]
        quarantine = asserted + void
        gates = set(asserted)                               # only ASSERTED fences can be falsified
        print("\n=== %s ===" % case)
        print("  allow      : %d declared path(s)" % len(allow))
        print("  quarantine : %d asserted, %d declared VOID" % (len(asserted), len(void)))
        if void:
            unprovable.append(case)

        # ---- 1. overlap ----------------------------------------------------------------------
        allow_files = set()
        for a in allow:
            allow_files |= set(files_under(repo, a))
        quarantine_files = set()
        for q in quarantine:
            quarantine_files |= set(files_under(repo, q))
        both = allow_files & quarantine_files
        if both:
            findings.append("'%s': %d path(s) are declared BOTH allow and quarantine. That is a "
                            "contradiction, not a rule." % (case, len(both)))
            for p in sorted(both):
                print("  CONTRADICTION  %s" % p)

        # ---- 2. existence --------------------------------------------------------------------
        for kind, decl in (("allow", allow), ("quarantine", asserted), ("void", void)):
            for rel_path in decl:
                if not os.path.exists(os.path.join(repo, rel_path.replace("/", os.sep))):
                    findings.append("'%s': declared %s path '%s' does not exist. A rule about a "
                                    "file that is not there fences nothing." % (case, kind, rel_path))
                    print("  MISSING    %-10s %s" % (kind, rel_path))

        # ---- 3. the clause with teeth --------------------------------------------------------
        wanted = {}
        for q in sorted(quarantine_files):
            full = os.path.join(repo, q.replace("/", os.sep))
            if os.path.isfile(full):
                wanted[sha256_of(full)] = os.path.getsize(full)
        if wanted:
            found = twins_in_tree(repo, wanted)
            for q in sorted(quarantine_files):
                full = os.path.join(repo, q.replace("/", os.sep))
                if not os.path.isfile(full):
                    continue
                digest = sha256_of(full)
                twins = [t for t in found.get(digest, []) if t != q]
                in_scope_twins = [t for t in twins if t in allow_files]
                if twins:
                    label = "TWINNED   " if q in gates else "twinned   "
                    print("  %s %s  -> %d byte-identical cop(y/ies)%s"
                          % (label, q, len(twins),
                             "" if q in gates else "  [void fence - reported, does not gate]"))
                    for t in twins[:8]:
                        print("                 %s%s"
                              % (t, "   <-- INSIDE THE ALLOW SET" if t in allow_files else ""))
                    if len(twins) > 8:
                        print("                 ... and %d more" % (len(twins) - 8))
                    if q in gates:
                        findings.append(
                            "'%s': quarantined '%s' has %d byte-identical twin(s) elsewhere in the "
                            "tree%s. A quarantine with a copy outside it fences nothing - and a "
                            "copy under another name is exactly the vector AB-2 found."
                            % (case, q, len(twins),
                               " INCLUDING %d inside the allow set" % len(in_scope_twins)
                               if in_scope_twins else ""))
                else:
                    print("  unique     %s" % q)

        # ---- 4. a tracked quarantine is void -------------------------------------------------
        for q in sorted(quarantine_files):
            if q in tracked:
                print("  TRACKED    %s - readable by construction%s"
                      % (q, "" if q in gates else "  [already declared void]"))
                if q in gates:
                    findings.append(
                        "'%s': quarantined '%s' is TRACKED. The quarantine is VOID: tracked content "
                        "is readable and no directory name changes that. Either the file is not "
                        "really quarantined, or the declaration must say `void` and accept that "
                        "this case can never be cleared." % (case, q))

        # ---- 5. the run half -----------------------------------------------------------------
        if args.manifest:
            got = [l.strip().replace("\\", "/") for l in io.open(args.manifest, encoding="utf-8")
                   if l.strip()]
            outside = [p for p in got if p not in allow_files]
            print("  manifest   : %d path(s) received, %d outside the allow set"
                  % (len(got), len(outside)))
            for p in outside:
                print("       OUTSIDE  %s" % p)
            if outside:
                findings.append("'%s': %d path(s) in the run manifest are outside the declared "
                                "allow set." % (case, len(outside)))
        else:
            print("  manifest   : NONE SUPPLIED - the run half of this check EXAMINED NOTHING.")
            print("               What an agent actually read is not recorded anywhere in this")
            print("               repository. That is M-23's unsolved half, not an omission here.")

    print("\n--- WHY EACH PATH IS DECLARED (printed every run so it can be disagreed with) ---")
    for case in sorted(cases):
        for kind, rel_path, reason in cases[case]["reasons"]:
            print("  %-10s %s" % (kind, rel_path))
            print("      %s" % (reason or "NO REASON GIVEN - itself a finding for a reader."))

    if unprovable:
        print("\n🔴 %d CASE(S) DECLARE A VOID FENCE AND CAN NEVER BE CLEARED: %s"
              % (len(unprovable), ", ".join(sorted(set(unprovable)))))
        print("A void fence is a CONFESSION, not an exemption. It records that the quarantine was")
        print("defeated and cannot be repaired - so no audit of that case, however careful, can")
        print("establish that its generator was blind. The result stands or falls on other")
        print("evidence entirely. This line prints on every run, deliberately, and the only way to")
        print("remove it is to stop declaring the fence void - which means actually building one.")

    if not findings:
        print("\nCLEAN: every declared case is coherent and every ASSERTED quarantine is intact.")
        print("THIS IS NOT A BLINDNESS CLAIM AND MUST NEVER BE READ AS ONE. It says the scope is")
        print("self-consistent and no asserted fence is defeated by a copy. It says NOTHING about")
        print("what any agent could see, because nothing in this repository records that.")
        return EXIT_OK

    print("\n--- WORKLIST: %d finding(s) ---" % len(findings))
    for f in findings:
        print("  %s" % f)
    print("\nA VOID QUARANTINE IS NOT AUTOMATICALLY A BUG - it may be the honest state of a case "
          "whose\nkey is legitimately tracked corpus. What it is NOT is evidence of blindness, and "
          "the remedy is\nnever to delete the declaration.")
    return EXIT_FOUND


if __name__ == "__main__":
    sys.exit(main())
