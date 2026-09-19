# -*- coding: utf-8 -*-
"""Is every directory DECLARED Green actually Green? (M-22, the tier-claim check.)

    python tools/check-green-claims.py [--repo .] [--register tools/green-claims.txt]
                                       [--maps sanitization] [--terms sanitization/scrub-terms.md]
                                       [--name-terms]

WHY THIS EXISTS. The scrub builder takes a --green list, and that list is a machine-readable tier
register. Documents assert directories are Green in prose. Measured 2026-09-19: four directories
were asserted Green -- three of them by docs/13-data-boundary.md's own per-project approvals -- and
NOT ONE had ever been on the list. The register and the prose disagreed for two months and NOTHING
EVER COMPARED THEM. That is AB-2, and this is the comparison.

WHAT IT IS NOT. It is not M-5 and it is not Gate 3. M-5 asks what a commit INTRODUCES, over the
index. Gate 3 asks whether a rewritten clone is clean. This asks a third question neither of them
can: DOES THE LABEL MATCH THE CONTENT. A file can be full of vocabulary and perfectly honest about
it; a file can be clean and lying. Only the disagreement is a finding here.

*** IT ENUMERATES A DECLARATION, NOT A WORD. *** tools/green-claims.txt is the whole population.
A grep for "Green" was measured against this repository and rejected: ~33 directory assertions
against ~55 incidental uses ("the suite stays green", "greenfield"), the largest false-positive
block sitting inside a Green-declared corpus. A gate that is 40% signal gets learned-ignored.

*** THE LEAVE-ONE-OUT RULE, AND IT IS THE WHOLE DESIGN. *** derive_needles tiers a needle T3 --
conventional, report-only -- when it appears in the Green corpora. Pass the Green corpora while
checking a Green directory and ITS OWN CONTENT DEMOTES ITS OWN NEEDLES: the check passes
vacuously, always, for free. Pass nothing and every Green corpus fails on the conventional map
keys it is entitled to contain. Both are wrong. So when checking directory D the Green token set is
built from every OTHER registered directory: "is this name conventional?" is answered by content
that is not the content under test.

It shares derive_needles with the Gate 3 oracle rather than deriving a third vocabulary. The
builder/verifier independence rule governs builder-versus-verifier; this is neither, and a third
hand-rolled vocabulary would be a third thing to keep in step.

TIERS, the oracle's:
    T1 DECLARED - the owner wrote it down. Gates on presence ANYWHERE, embedded or not.
    T2 INFERRED - a distinctive map key. Gates on WHOLE-TOKEN presence only.
    T3 ORDINARY - identity mapping, short, or conventional in OTHER Green content. Never gates.

WHAT IT CANNOT SEE, stated because a limit nobody prints is a limit nobody knows about:
  - an identifier in neither the maps nor the term list. No vocabulary-based check reaches it.
  - a directory nobody declared. Silence is not a claim, so silence is not checked - which is why
    the register has to be the ONLY way to assert Green rather than one of two ways.
  - an untracked directory. Its content is not in HEAD, so a claim about one can never be checked
    here and should not be made.
  - an inferential identification: a plant described precisely enough to be recognised without
    being named. That is not a string.

EXIT CODES, per this project's standing contract:
    0  every declared directory is covered by the --green list and carries no T1 or T2 needle.
    1  FOUND SOMETHING. A worklist follows. Triage it; do not read the total as a severity.
    2  NOTHING WAS EXAMINED - no register, an empty one, no vocabulary during a live run, a corpus
       git could not list, or an instrument that cannot find its own needles. EMPTY IS NOT CLEAN.
    3  REFUSED - the term list is tracked by git, so the identifier list has itself been committed.

Paths and counts only. A record of a leak must not be a copy of it, and this output gets pasted
into notes. --name-terms is the watched-terminal exception, the same trade M-5 makes.
"""
import argparse
import ast
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
    """stdout as raw bytes, plus the exit code. Never decoded here - a tracked blob is arbitrary."""
    proc = subprocess.Popen(["git"] + list(args), cwd=repo,
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    out, _ = proc.communicate()
    return proc.returncode, out


def read_register(path):
    """[(directory, reason)] in file order, or None when the file is absent.

    An entry with no reason is KEPT and recorded as "NO REASON GIVEN" rather than rejected - the
    same choice accepted-merges.txt makes. Refusing it would push the next person towards deleting
    the line instead of writing the sentence, and a missing reason that is visible on every run
    gets fixed while a missing line never does.
    """
    if not os.path.isfile(path):
        return None
    entries = []
    for line in io.open(path, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        name, _, why = line.partition("#")
        name = name.strip().replace("\\", "/").rstrip("/")
        if name:
            entries.append((name, why.strip() or "NO REASON GIVEN"))
    return entries


def green_default(path):
    """The --green default list read out of a tool's SOURCE, or None if it cannot be found.

    Parsed with ast rather than matched with a regex. The list is a literal inside main() -
    `green = args.green or [...]` - so it cannot be imported without running the parser, and a
    regex over source that contains the word "green" in prose is the same mistake this whole tool
    exists to avoid. Returning None on a miss matters: it becomes NOTHING EXAMINED, never a pass.
    """
    try:
        tree = ast.parse(io.open(path, encoding="utf-8").read())
    except (OSError, SyntaxError):
        return None
    for node in ast.walk(tree):
        if not isinstance(node, ast.Assign) or len(node.targets) != 1:
            continue
        target = node.targets[0]
        if not isinstance(target, ast.Name) or target.id != "green":
            continue
        value = node.value
        if isinstance(value, ast.BoolOp) and isinstance(value.op, ast.Or):
            value = value.values[-1]
        if isinstance(value, ast.List) and value.elts:
            items = [e.value for e in value.elts
                     if isinstance(e, ast.Constant) and isinstance(e.value, str)]
            if len(items) == len(value.elts):
                return [i.replace("\\", "/").rstrip("/") for i in items]
    return None


def covered_by(path, corpora):
    """Is `path` inside any of `corpora`? PREFIX, not equality.

    --green membership is `git ls-files <corpus>`, which is a prefix walk, so listing
    gen/test-project001 already covers gen/test-project001/hx-corpus. Testing equality here would
    report a directory as undeclared while the scrub was in fact treating it as Green - a finding
    in the wrong direction, which is the kind that gets a gate switched off.
    """
    return any(path == c or path.startswith(c + "/") for c in corpora)


def tracked_files(repo, directory):
    """Tracked paths under a directory, or None if git could not answer."""
    rc, out = git_bytes(repo, "ls-files", "-z", "--", directory)
    if rc != 0:
        return None
    return [p.decode("utf-8", "replace") for p in out.split(b"\0") if p]


def blob_at_head(repo, path):
    """One tracked path at HEAD. None when it is not valid UTF-8 - never counted clean."""
    rc, out = git_bytes(repo, "show", "HEAD:" + path)
    if rc != 0:
        return None
    try:
        out.decode("utf-8")
    except UnicodeDecodeError:
        return None
    return out


def live_run_material(repo):
    """Whether this machine is carrying live-job material at all.

    The trigger for the no-vocabulary case, and the same judgement M-5 makes, for the same reason:
    `sanitization/` is git-ignored and machine-local, so a fresh clone has no vocabulary and never
    will. Refusing there forever is a broken tool; going quiet is the failure this repository has
    already paid for once. So the trigger is the dangerous state - no vocabulary DURING A LIVE RUN
    refuses, no vocabulary on a clone that has never worked one does not.
    """
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
    ap = argparse.ArgumentParser(
        description="Refuse a directory that is declared Green but carries live vocabulary.")
    ap.add_argument("--repo", default=".", help="repository root")
    ap.add_argument("--register", default="tools/green-claims.txt",
                    help="the Green declaration register")
    ap.add_argument("--maps", default="sanitization", help="directory of *.map.json")
    ap.add_argument("--terms", default="sanitization/scrub-terms.md", help="the explicit term list")
    ap.add_argument("--name-terms", action="store_true",
                    help="print the matched terms themselves. OFF BY DEFAULT because this output "
                         "gets pasted into notes, and a record of a leak must not be a copy of it. "
                         "Use it at a terminal you are watching, not in a pipeline.")
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    here = os.path.dirname(os.path.abspath(__file__))
    maps_dir = os.path.join(repo, args.maps)
    terms_path = os.path.join(repo, args.terms)
    vs = oracle()
    findings = []

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

    # ---- The register ----------------------------------------------------------------------------
    register = read_register(os.path.join(repo, args.register))
    if register is None:
        print("NOTHING EXAMINED: no Green register at '%s'. Every Green claim in this repository is\n"
              "unchecked, which is the state M-22 was filed to end. EMPTY IS NOT CLEAN."
              % args.register)
        return 2
    if not register:
        print("NOTHING EXAMINED: the Green register '%s' declares no directory. A register with no\n"
              "entries checks nothing and reports success, which is the worst of both. EMPTY IS NOT\n"
              "CLEAN." % args.register)
        return 2

    declared = [d for d, _ in register]
    print("register              : %s - %d declared director(y/ies)" % (args.register, len(declared)))
    for d, why in register:
        print("    DECLARED GREEN  %-28s %s" % (d, why))

    # ---- The two --green literals ----------------------------------------------------------------
    # They are duplicated source with no shared constant, kept in step by hand. The independence
    # rule (tools/README.md) forbids MERGING them; it does not forbid asking whether they agree,
    # and a silent divergence would mean the builder and the oracle disagree about which population
    # is conventional - which is a difference neither tool can report about itself.
    builder_green = green_default(os.path.join(here, "build-scrub-rules.py"))
    oracle_green = green_default(os.path.join(here, "verify-scrub.py"))
    if builder_green is None or oracle_green is None:
        which = "build-scrub-rules.py" if builder_green is None else "verify-scrub.py"
        print("\nNOTHING EXAMINED: could not read the --green default list out of %s. This check\n"
              "compares the register against that list, and it has no list. EMPTY IS NOT CLEAN."
              % which)
        return 2

    print("--green list          : %d entr(y/ies), builder and oracle %s"
          % (len(builder_green), "AGREE" if builder_green == oracle_green else "DISAGREE"))
    if builder_green != oracle_green:
        only_b = sorted(set(builder_green) - set(oracle_green))
        only_o = sorted(set(oracle_green) - set(builder_green))
        findings.append(
            "the --green defaults in build-scrub-rules.py and verify-scrub.py have DIVERGED. "
            "Only in the builder: %s. Only in the oracle: %s. They are separate literals by "
            "design and must still say the same thing: the builder decides what is not rewritten, "
            "the oracle decides what is not hunted, and a disagreement means one of them is "
            "treating live content as conventional."
            % (only_b or "-", only_o or "-"))

    # ---- Register vs --green list, both directions -----------------------------------------------
    for d in declared:
        if not covered_by(d, builder_green):
            findings.append(
                "'%s' is DECLARED GREEN in %s but is not covered by the scrub tools' --green list, "
                "so the tooling has never treated it as Green. Either it belongs on that list or "
                "the declaration is wrong." % (d, args.register))
    for g in builder_green:
        if not covered_by(g, declared):
            findings.append(
                "'%s' is on the scrub tools' --green list but nothing in %s declares it. The list "
                "withholds a rewrite rule for every term present there, so an entry nobody has "
                "written a reason for is a silent exemption." % (g, args.register))

    # ---- Vocabulary ------------------------------------------------------------------------------
    probe, _, _ = vs.derive_needles(maps_dir, terms_path)
    if not probe:
        if live_run_material(repo):
            print("\nNOTHING EXAMINED: no de-identification vocabulary under '%s', and this machine\n"
                  "is carrying live-job material. The structural half above ran; the half that\n"
                  "reads content did not. EMPTY IS NOT CLEAN." % args.maps)
            return 2
        print("\nThe CONTENT half did not run: no vocabulary under '%s' and no live-job material\n"
              "present. This is the ordinary state of a clone that has never worked a live job,\n"
              "and the register/--green comparison above DID run and stands on its own."
              % args.maps)
        return 1 if findings else 0

    # ---- Per-directory, leave-one-out ------------------------------------------------------------
    # Each corpus is tokenised ONCE and the leave-one-out set is assembled from the cache. Calling
    # green_corpus_blob per directory would re-read every corpus per directory instead.
    tokens_of, files_of, stale = {}, {}, []
    for d in declared:
        paths = tracked_files(repo, d)
        if paths is None:
            print("\nNOTHING EXAMINED: git could not list tracked files under '%s'. EMPTY IS NOT "
                  "CLEAN." % d)
            return 2
        files_of[d] = paths
        if not paths:
            stale.append(d)
        blobs = []
        for rel_path in paths:
            blob = blob_at_head(repo, rel_path)
            if blob is not None:
                blobs.append(blob.lower())
        tokens_of[d] = set(vs.WORD.findall(b"\n".join(blobs)))

    worklist, unsearchable, reported_t3 = [], [], 0
    for d in declared:
        if not files_of[d]:
            continue
        leave_one_out = set()
        for other in declared:
            if other != d:
                leave_one_out |= tokens_of[other]

        needles, sources, tier = vs.derive_needles(maps_dir, terms_path, leave_one_out)
        hunted = sorted(n for n in needles if tier.get(n) in ("T1", "T2"))
        if not hunted:
            print("\nNOTHING EXAMINED: the vocabulary for '%s' has no T1 or T2 needle after the\n"
                  "leave-one-out demotion. Every term was ruled conventional by the other declared\n"
                  "corpora, which would make this directory's result free. EMPTY IS NOT CLEAN." % d)
            return 2

        # Instrument control, per directory: the tiering changes with the leave-one-out set, so the
        # hunted population changes with it, and a control over a different population proves
        # nothing about this one. The oracle once reported a plausible count while 1,505 of its
        # needles could not match anything at all.
        control = ("\n".join(hunted) + "\n").encode("utf-8")
        loose = vs.untokenisable(hunted)
        cw, ce = vs.residuals(vs.tokenise([control]), hunted)
        found = cw | ce | {n for n in loose if n.encode() in control.lower()}
        if len(found) != len(hunted):
            print("\nNOTHING EXAMINED: %d of %d needles could not match a payload built from\n"
                  "themselves while checking '%s'. The matcher is not searching for what it says it\n"
                  "is searching for. EMPTY IS NOT CLEAN." % (len(hunted) - len(found), len(hunted), d))
            return 2
        reported_t3 += len(needles) - len(hunted)

        def hits_in(blob, hunted=hunted, loose=loose, tier=tier):
            vocab = vs.tokenise([blob])
            whole, embedded = vs.residuals(vocab, hunted)
            raw = blob.lower()
            for n in loose:
                if n.encode() in raw:
                    whole.add(n)
            # T1 counts embedded: the owner named that string and meant it anywhere. T2 does not,
            # because an inferred key inside a longer one is usually a different identifier that
            # merely starts the same way. The asymmetry is the oracle's.
            return ({n for n in whole if tier[n] in ("T1", "T2")}
                    | {n for n in embedded if tier[n] == "T1"})

        for rel_path in files_of[d]:
            blob = blob_at_head(repo, rel_path)
            if blob is None:
                unsearchable.append(rel_path)
                continue
            hits = hits_in(blob)
            if hits:
                worklist.append((d, rel_path, sorted(hits), tier, sources))

    print("vocabulary            : %d needle(s) in the maps and term list; per-directory tiering is\n"
          "                        LEAVE-ONE-OUT, so a corpus never excuses itself" % len(probe))
    print("unsearchable          : %d tracked blob(s) were not valid UTF-8 %s"
          % (len(unsearchable), "(listed below)" if unsearchable else "- none"))
    for rel_path in unsearchable:
        print("    NOT SEARCHED  %s" % rel_path)
    if stale:
        print("stale                 : %d declared director(y/ies) have no tracked files - trim the\n"
              "                        register rather than letting it accrete: %s"
              % (len(stale), ", ".join(stale)))

    for d, rel_path, hits, tier, sources in worklist:
        t1 = len([n for n in hits if tier[n] == "T1"])
        findings.append(
            "'%s' is declared Green and '%s' carries %d declared (T1) and %d inferred (T2) "
            "needle(s)." % (d, rel_path, t1, len(hits) - t1))

    # ---- Verdict ---------------------------------------------------------------------------------
    if not findings:
        if unsearchable:
            print("\nNO HIT IN WHAT COULD BE SEARCHED - but %d blob(s) could not be searched at "
                  "all.\nThat is not the same as clean. Look at them by hand." % len(unsearchable))
            return 1
        print("\nCLEAN: every declared directory is covered by the --green list and carries no T1 "
              "or T2 needle.")
        print("THIS PROVES CLOSURE OVER A VOCABULARY AND OVER A REGISTER. A directory nobody "
              "declared is\nnot checked here, and an identifier nobody wrote down is invisible to "
              "it.")
        return 0

    print("\n--- WORKLIST: %d finding(s) ---" % len(findings))
    for f in findings:
        print("  %s" % f)
    if args.name_terms:
        for d, rel_path, hits, tier, sources in worklist:
            print("    %s" % rel_path)
            for n in hits:
                print("        %s  [%s, %s]" % (n, tier[n], sources.get(n, "?")))
    print("\nA FALSE 'ALREADY SANITISED' LABEL IS THE FINDING, NOT THE DATA. Content carrying "
          "vocabulary is\nordinary here - that is what the publication scrub is for. A document "
          "saying it does not is\nwhat licenses a copy into somewhere the scrub does not run, which "
          "is the shape of AB-1.\nThe remedy is to sanitize the content or to stop calling it "
          "Green. It is never to delete the\nline from the register.")
    if not args.name_terms:
        print("Re-run with --name-terms at a terminal you are watching to see which strings matched.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
