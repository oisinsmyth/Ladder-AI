#!/usr/bin/env python3
"""Does anything STAGED carry live-job vocabulary? (M-5, the committed-content boundary check.)

    python tools/check-staged-identifiers.py [--repo .] [--maps sanitization]
                                             [--terms sanitization/scrub-terms.md] [--name-terms]

WHY THIS EXISTS. Live-job identifiers reached committed source in 13 files across several lanes on
the first day of live-job work. Every lane had been told that nothing from the job folder is
committed, and every lane honoured it FOR ARTIFACTS - then quoted job identifiers into code comments
as evidence for a technical claim, because that does not feel like job content, it feels like
rigour. One lane audited its own files, caught them, and correctly declined to rewrite another
lane's. That is exactly why the leak survived: A PER-LANE CHECK CATCHES THE LANE'S OWN WORK; ONLY A
REPO-WIDE SWEEP CATCHES THE REPO.

WHAT IT IS NOT. It is not Gate 3. verify-scrub.py judges a rewritten clone against its entire object
database and decides whether something may be published. This asks a much smaller question at a much
earlier moment - is THIS COMMIT about to add live vocabulary to the history - and its answer is a
worklist for the person writing the commit, not a verdict. The difference is when the fix is cheap:
at commit time the author still remembers whether the concrete case was load-bearing, and afterwards
somebody has to re-derive that intent from prose.

*** ITS SUBJECT IS THE INDEX, NOT THE WORKING TREE. *** `git show :<path>` reads the staged blob,
which is what the commit will contain. Checking the working tree instead would pass a file whose
leak was staged and then edited away, and refuse one whose leak is only unstaged - both wrong, and
the second kind of wrong is what gets a gate switched off.

IT SHARES ITS VOCABULARY WITH THE GATE 3 ORACLE, DELIBERATELY. verify-scrub.py refuses to share
derivation code with build-scrub-rules.py, and that rule is about an EMITTER and THE JUDGE OF ITS
OUTPUT: derive both the same way and a bug emits a rule that misses X, then hunts for X the same
wrong way and reports a confident zero. Nothing of that shape applies here. This tool emits nothing,
judges a different subject at a different moment, and gates nothing that verify-scrub gates - so a
third independent derivation would buy no coverage and cost a third vocabulary to keep in step.

THE TIERS ARE THE ORACLE'S, MINUS ONE IT CANNOT HONESTLY APPLY:
  T1 DECLARED - the owner wrote it down. Reported wherever it appears, embedded or whole.
  T2 INFERRED - a distinctive map key. Reported on WHOLE-TOKEN presence only, because 59 of 61
                inferred-key hits in the first trial were the same identifier seen inside a longer
                one, and a check that cries wolf 59 times out of 61 gets learned-ignored.
  T3 ORDINARY - identity mappings, Green-present and short names. NEVER REPORTED HERE. In the oracle
                T3 gates on a FALL in its count between two corpora; a staging area has no before
                and after, so there is no such measurement to make and pretending otherwise would
                be a tier in name only.

WHAT IT CANNOT SEE, stated because a limit nobody prints is a limit nobody knows about:
  - an identifier split across a line break, or broken by markup or punctuation;
  - anything in a staged blob that is not valid UTF-8 - counted and reported, never called clean;
  - an identifier nobody supplied. THIS PROVES CLOSURE OVER A VOCABULARY and cannot prove the
    absence of identifiers. It is a worklist to triage, never a count to report.

EXIT CODES, per this project's standing contract:
    0  nothing staged carries the vocabulary - or there is genuinely nothing to examine and that
       was corroborated with git rather than inferred from an empty list.
    1  FOUND SOMETHING. A worklist follows. Triage it; do not read the total as a severity.
    2  NOTHING WAS EXAMINED while there was something to examine - no vocabulary during a live run,
       staged changes that produced no readable path, or an instrument that cannot find its own
       needles. EMPTY IS NOT CLEAN: exit 2 is never a pass.
    3  REFUSED - the term list is tracked by git, so the identifier list has itself been committed.

THE OUTPUT NAMES PATHS, TIERS AND COUNTS, NEVER THE MATCHED TERM. A record of a leak must not be a
copy of it, and this output gets pasted into notes. `--name-terms` prints them for a terminal you
are watching, and is the same trade `build-scrub-rules.py --name-collisions` makes.
"""
import argparse
import importlib.util
import io
import os
import subprocess
import sys


def oracle():
    """verify-scrub.py, loaded as a module. The filename has hyphens, so `import` cannot."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "verify-scrub.py")
    if not os.path.isfile(path):
        raise SystemExit("cannot find %s - this tool derives its vocabulary from it" % path)
    spec = importlib.util.spec_from_file_location("verify_scrub", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)                            # __main__-guarded: nothing runs
    return mod


def git_bytes(repo, *args):
    """stdout as raw bytes, plus the exit code. Never decoded here - a staged blob is arbitrary."""
    proc = subprocess.Popen(["git"] + list(args), cwd=repo,
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    out, _ = proc.communicate()
    return proc.returncode, out


def has_staged_changes(repo):
    """git's own answer, asked separately - AND ASKED IN THE SAME SCOPE.

    THIS IS THE CORROBORATION, AND IT IS THE WHOLE REASON THE ZERO CASE IS SAFE. An empty path list
    has two causes that look identical - a commit that stages no content, and a parser that stopped
    working - and only one of them is clean. Asking git a second way distinguishes them, so `no
    paths` exits 0 when git agrees there is nothing and 2 when it does not.

    *** --diff-filter=ACMR, MATCHING staged_paths EXACTLY, AND THE FIRST VERSION DID NOT. ***
    Without it this asks "are there staged changes" while the path list asks "is content being
    added", and those differ for a commit that only DELETES files: zero paths, staged changes, and
    a refusal of a commit that cannot possibly leak anything because it adds nothing. Found by the
    case named for it. A corroborating question in a different scope does not corroborate - it
    invents disagreement, and here it invented it in the direction that refuses correct work, which
    is the direction that gets a hook switched off."""
    rc, _ = git_bytes(repo, "diff", "--cached", "--quiet", "--diff-filter=ACMR")
    return rc != 0


def staged_paths(repo):
    """Paths whose CONTENT this commit adds. -z because a path may contain anything but NUL.

    Deletions are excluded: D leaves no blob in the index to read, and a path being removed carries
    nothing into the commit. Renames report their new name, which is the one that gets committed."""
    rc, out = git_bytes(repo, "diff", "--cached", "--name-only", "-z", "--diff-filter=ACMR")
    if rc != 0:
        return None
    return [p.decode("utf-8", "replace") for p in out.split(b"\0") if p]


def staged_blob(repo, path):
    """The INDEX version of a path. None when it cannot be read as UTF-8 - never silently skipped."""
    rc, out = git_bytes(repo, "show", ":" + path)
    if rc != 0:
        return None
    try:
        out.decode("utf-8")
    except UnicodeDecodeError:
        return None
    return out


def live_run_material(repo):
    """Whether this machine is carrying live-job material at all.

    The trigger for the no-vocabulary case, and the one judgement in this tool. `sanitization/` is
    git-ignored and machine-local, so a fresh clone has no vocabulary and never will; refusing every
    commit there forever is not a gate, it is a broken tool that teaches people --no-verify. Going
    quiet instead is the other failure, and this repository has already paid for it once - after a
    machine move core.hooksPath pointed at the previous machine's path and BOTH existing gates sat
    inert for weeks while every commit reported green.

    So the trigger is the dangerous state rather than the tool's own convenience: no vocabulary AND
    no live-job material is genuinely nothing to check, while no vocabulary DURING A LIVE RUN is
    precisely the moment M-5 exists for, and that one refuses."""
    folder = os.path.join(repo, "Live Runs")
    if not os.path.isdir(folder):
        return False
    for _, dirs, files in os.walk(folder):
        if files:
            return True
        if not dirs:
            break
    return False


def main():
    ap = argparse.ArgumentParser(description="Refuse a commit that stages live-job vocabulary.")
    ap.add_argument("--repo", default=".", help="repository root")
    ap.add_argument("--maps", default="sanitization", help="directory of *.map.json")
    ap.add_argument("--terms", default="sanitization/scrub-terms.md", help="the explicit term list")
    ap.add_argument("--name-terms", action="store_true",
                    help="print the matched terms themselves. OFF BY DEFAULT because this output "
                         "gets pasted into notes, and a record of a leak must not be a copy of it. "
                         "Use it at a terminal you are watching, not in a pipeline.")
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    maps_dir = os.path.join(repo, args.maps)
    terms_path = os.path.join(repo, args.terms)
    vs = oracle()

    # ---- Refusal, before anything is read --------------------------------------------------------
    rel = os.path.relpath(terms_path, repo).replace("\\", "/")
    rc, out = git_bytes(repo, "ls-files", "--error-unmatch", rel)
    if rc == 0 and out.strip():
        sys.stderr.write(
            "REFUSED: the term list '%s' is TRACKED BY GIT.\n"
            "It names every live identifier this check hunts for, so committing it publishes the\n"
            "thing the check exists to keep out. Add it to .gitignore and remove it from the\n"
            "index before running this again.\n" % rel)
        return 3

    # ---- Vocabulary ------------------------------------------------------------------------------
    needles, sources, tier = vs.derive_needles(maps_dir, terms_path)
    hunted = sorted(n for n in needles if tier.get(n) in ("T1", "T2"))
    if not needles:
        if live_run_material(repo):
            print("NOTHING EXAMINED: no de-identification vocabulary was found under '%s', and this\n"
                  "machine is carrying live-job material. That is the exact situation M-5 exists\n"
                  "for - a live run with the sweep unable to run. EMPTY IS NOT CLEAN." % args.maps)
            return 2
        print("nothing to check: no vocabulary under '%s' and no live-job material present.\n"
              "This is the ordinary state of a clone that has never worked a live job." % args.maps)
        return 0

    # ---- Instrument control ----------------------------------------------------------------------
    # *** WITHOUT THIS THE RESULT IS UNFALSIFIABLE. *** The oracle once reported a plausible
    # residual count while 1,505 of its needles could not match anything at all, and every run of it
    # looked exactly like this one. A needle that cannot match itself proves nothing by not matching
    # a staged file.
    control = ("\n".join(hunted) + "\n").encode("utf-8")
    loose = vs.untokenisable(hunted)
    cw, ce = vs.residuals(vs.tokenise([control]), hunted)
    found = cw | ce | {n for n in loose if n.encode() in control.lower()}
    if len(found) != len(hunted):
        print("NOTHING EXAMINED: %d of %d needles could not match a payload built from themselves.\n"
              "The matcher is not searching for what it says it is searching for, and any count it\n"
              "produces is meaningless. EMPTY IS NOT CLEAN."
              % (len(hunted) - len(found), len(hunted)))
        return 2

    # ---- The staged corpus -----------------------------------------------------------------------
    paths = staged_paths(repo)
    if paths is None:
        print("NOTHING EXAMINED: git could not list the staged paths. EMPTY IS NOT CLEAN.")
        return 2
    if not paths:
        if has_staged_changes(repo):
            print("NOTHING EXAMINED: git reports staged changes and this found no readable path to\n"
                  "examine. EMPTY IS NOT CLEAN.")
            return 2
        print("nothing to check: this commit stages no content (git agrees - `diff --cached` is "
              "empty).")
        return 0

    print("subject               : the git INDEX - %d staged path(s), not the working tree"
          % len(paths))
    print("vocabulary            : %d needle(s) hunted  [T1 %d declared, T2 %d inferred; T3 is not "
          "reportable here]"
          % (len(hunted),
             len([n for n in hunted if tier[n] == "T1"]),
             len([n for n in hunted if tier[n] == "T2"])))
    print("instrument control    : %d of %d needles matched themselves" % (len(found), len(hunted)))

    worklist, unsearchable = [], []
    for path in paths:
        blob = staged_blob(repo, path)
        if blob is None:
            unsearchable.append(path)
            continue
        vocab = vs.tokenise([blob])
        whole, embedded = vs.residuals(vocab, hunted)
        raw = blob.lower()
        for n in loose:
            if n.encode() in raw:
                whole.add(n)
        # T1 counts embedded: the owner named that string and meant it anywhere. T2 does not,
        # because an inferred key inside a longer one is usually a different identifier that merely
        # starts the same way - the asymmetry is the oracle's, and it is what makes this readable.
        hits = sorted({n for n in whole if tier[n] in ("T1", "T2")}
                      | {n for n in embedded if tier[n] == "T1"})
        if hits:
            worklist.append((path, hits))

    print("unsearchable          : %d staged blob(s) were not valid UTF-8 %s"
          % (len(unsearchable), "(listed below)" if unsearchable else "- none"))
    for path in unsearchable:
        print("    NOT SEARCHED  %s" % path)

    if not worklist:
        if unsearchable:
            print("\nNO HIT IN WHAT COULD BE SEARCHED - but %d blob(s) could not be searched at "
                  "all.\nThat is not the same as clean. Look at them by hand." % len(unsearchable))
            return 1
        print("\nCLEAN: no staged blob carries a T1 or T2 needle.")
        print("THIS PROVES CLOSURE OVER A VOCABULARY, not the absence of identifiers. An identifier "
              "nobody\nwrote down is invisible to it, and always will be.")
        return 0

    print("\n--- WORKLIST: %d staged path(s) carry live-job vocabulary ---" % len(worklist))
    for path, hits in worklist:
        t1 = len([n for n in hits if tier[n] == "T1"])
        print("  %s" % path)
        print("      %d declared (T1), %d inferred (T2)" % (t1, len(hits) - t1))
        if args.name_terms:
            for n in hits:
                print("        %s  [%s, %s]" % (n, tier[n], sources.get(n, "?")))
    print("\nTRIAGE THIS, DO NOT COUNT IT. A hit is a question - is this string in this file for a "
          "reason\nthat survives the job it came from? - and the answer is usually rewrite the "
          "sentence, not\ndelete the file. A 'lesson learned' written in the job's own vocabulary "
          "is still a leak.")
    if not args.name_terms:
        print("Re-run with --name-terms at a terminal you are watching to see which strings matched.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
