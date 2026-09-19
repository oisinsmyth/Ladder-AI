#!/usr/bin/env python3
"""Emit the git-filter-repo rule set that de-identifies a clone for public release.

WHY THIS EXISTS. The publication plan (docs/notes/portfolio-publication-plan.md) rewrites this
repository's history into a public artifact. Its audit found that the obvious way to do that is
wrong in a way that LOOKS RIGHT: a `\\bCamelCase\\b` rule scrubs the identifier out of prose and
matches NONE of the lower-hyphenated directory names built from the same word, so the scrub reports
success while 93 path names and 211 citations still carry the identifier (finding A8). A second
measurement found that rules derived from sanitization/*.map.json alone MISS 44 identifier-bearing
files, because job codes and identifying names barely appear in those maps. Neither population
dominates the other. This tool exists so that the rule set is DERIVED AND CHECKED rather than
assembled by hand, and so that the derivation is repeatable when somebody re-runs it in a year.

WHAT IT IS AND IS NOT. It emits rules. It does not rewrite anything, it never touches the object
database, and running it has no effect on any repository. The rewrite is git-filter-repo's job and
the verdict on the result is tools/verify-scrub.py's job - DELIBERATELY A DIFFERENT SCRIPT WITH NO
SHARED CODE. If the emitter and the verifier derived their vocabulary the same way, a bug in that
derivation would produce a rule that misses X and then hunt for X the same wrong way and find
nothing: a confident, earned-looking zero over the wrong population. The house rule that these
scripts share no helper module is usually ergonomics. Here it is the independence control.

THE OUTPUT IS GENERATED. DO NOT HAND-EDIT IT. Every artifact carries the digest of its inputs, and
the next run overwrites your edit without saying so. If a rule is wrong, fix the input and re-run.

*** EVERY EMITTED LINE CARRIES AN EXPLICIT `==>`. *** git-filter-repo's default replacement is the
literal string `***REMOVED***`. A rule file that relies on the default silently substitutes that
string into prose, which is both a defect and very hard to notice in a 1,300-commit rewrite.

WHY IT SCANS HISTORY AND NOT THE WORKTREE. A key that occurs only in a commit from July is still in
the artifact being published. Dropping it because it is absent from HEAD is precisely the class of
error this tool exists to prevent, so `--scan history` is the default and `--scan head` prints that
it did not examine history.

EXIT CODES, per this project's standing contract:
    0  rules emitted, every check passed
    1  A BLOCKING FINDING - a replacement collides with a job folder, the map is non-injective,
       the sentinel already occurs in the repository, or a rename target already exists.
       Nothing is emitted.
    2  NOTHING WAS EXAMINED - no maps, no term rows, or no key survived derivation.
       EMPTY IS NOT CLEAN: exit 2 is never a pass.
    3  REFUSED BEFORE ANYTHING WAS READ - the term list is tracked by git (the identifier list has
       been committed), or --out resolves inside tracked space. Nothing is read, nothing is written.

ON 1, 2 AND 3 NO OUTPUT FILE IS WRITTEN. A partial rule set is the worst possible artifact: it
looks like a rule set and it scrubs some of what it names.
"""
import argparse
import hashlib
import io
import json
import os
import re
import subprocess
import sys

# The seven sections the C# loader (src/converter/Converter/Sanitize/SanitizationMap.cs) knows.
# Only the two NAMEY ones carry keys a text pass can look for; the rest are whole-value replacements
# keyed on parsed XML structure - a network comment is keyed "<block>#<n>" where n is the
# compile-unit index, which is uncomputable from a flat file.
STRUCTURED_SECTIONS = ("names", "comments", "titles", "networkcomments",
                       "networktitles", "tags", "startvalues")
NAMEY_SECTIONS = ("names", "tags")

# Sections that are NOT in the C# schema and are therefore SILENTLY DROPPED by the structured
# sanitizer. Measured 2026-09-17: 15 entries across three of them, and they are exactly the
# site, site and product-line vocabulary - the terms a map-driven pass would otherwise miss.
# A text pass that ignores these ignores the only sections that name a site.
EXTRA_NAMEY_SECTIONS = ("company", "modelline", "identifiers")

# Prose keys that appear at a map's top level and are not vocabulary at all.
PROSE_KEYS = ("_purpose", "notes", "blockname", "blockcomment")

# AB-1's own figure. A shorter token collides with ordinary code and ordinary English, and the
# Green corpora are the only mechanical dictionary available (Windows has no wordlist and these
# scripts are stdlib-only). RAISING THIS TO SILENCE A REFUSAL IS THE WRONG FIX - see the failure
# prose at the bottom of main().
MIN_GLOBAL_LENGTH = 8

# A variant matching in more than this many distinct blobs is behaving like an ordinary word rather
# than an identifier, and gets demoted to composite-only. A RATCHET, NOT A TRUTH: it is reported on
# every run so a later reader can see what it cost.
ORDINARY_WORD_BLOB_THRESHOLD = 12

# What a compiler, an interpreter or a build engine reads. A declared term embedded inside a token
# in one of these is a term whose anchorless rule EDITS CODE, which is a different fact from being
# embedded anywhere - job vocabulary lives in `.ir` and `.xml`, and those are deliberately absent.
BUILD_SOURCE_EXT = (".cs", ".csproj", ".sln", ".props", ".targets", ".py", ".ps1",
                    ".json", ".yml", ".yaml")

SENTINEL_TEMPLATE = "QQSCRUBQQ%04dQQ"
TERM_CLASSES = ("jobcode", "site", "site", "modelline", "block", "member", "pathstem")
SPACED_CLASSES = ("site", "site", "modelline")

# The join between the two vocabularies above. EXTRA_NAMEY_SECTIONS names the map sections that
# carry site, site and product-line terms; SPACED_CLASSES names the classes that get a spaced
# variant. They describe the same thing from two directions and were never connected, so a company
# name out of a map got `AcmeHoldings`, `acmeholdings`, `acme-holdings` and never `Acme Holdings`.
#
# THE DISTINCTION BETWEEN THE THREE VALUES HERE IS PRESENTATIONAL. Membership of SPACED_CLASSES is
# the only thing any consumer tests, so all three could map to one class with no behavioural
# difference; they are named honestly so the run log reads sensibly, not because a finer taxonomy
# exists downstream.
SECTION_CLASS = {"company": "site", "modelline": "modelline", "identifiers": "site"}


def git(repo, *args):
    """Run git, return stdout as text. Never raises on a non-zero exit - callers decide."""
    return subprocess.run(["git"] + list(args), cwd=repo,
                          capture_output=True, text=True, errors="replace").stdout


def is_tracked(repo, relpath):
    proc = subprocess.run(["git", "ls-files", "--error-unmatch", relpath],
                          cwd=repo, capture_output=True, text=True)
    return proc.returncode == 0


def load_maps(mapdir):
    """Every *.map.json, read with utf-8-sig.

    25 of the 43 maps carry a UTF-8 BOM. `json.load` with plain utf-8 dies on them, which is not a
    hypothetical: it killed the first measurement pass of this corpus. The C# loader is unaffected
    because File.ReadAllText honours a BOM, so the trap is Python-only and invisible from the C#
    side."""
    stems, boms, used, ignored, dead = [], 0, {}, {}, []
    malformed = []
    pairs = {}          # key -> {replacement -> [map stems that say so]}
    section_of = {}     # key -> {section names it was found under}
    for path in sorted(__import__("glob").glob(os.path.join(mapdir, "*.map.json"))):
        stem = os.path.basename(path)[: -len(".map.json")]
        stems.append(stem)
        raw = io.open(path, "rb").read()
        if raw.startswith(b"\xef\xbb\xbf"):
            boms += 1
        doc = json.loads(raw.decode("utf-8-sig"))
        recognised = 0
        for section, body in doc.items():
            low = section.lower()
            if low in PROSE_KEYS or not isinstance(body, dict):
                continue
            if low in NAMEY_SECTIONS or low in EXTRA_NAMEY_SECTIONS:
                recognised += 1
                used[low] = used.get(low, 0) + len(body)
                for key, value in body.items():
                    # A JSON null (one exists in the real corpus) would otherwise be carried all the
                    # way to emission and written as the literal replacement `None`, or crash the
                    # sort in the injectivity check with a TypeError and exit 1 with no gate prose -
                    # indistinguishable from a real blocking finding. A map value that is not a
                    # non-empty string is not vocabulary.
                    if not isinstance(value, str) or not value.strip():
                        malformed.append("%s/%s: value is %s, not a string"
                                         % (stem, section, type(value).__name__))
                        continue
                    pairs.setdefault(key, {}).setdefault(value, []).append(stem)
                    # THE SECTION WAS IN SCOPE HERE AND WAS THROWN AWAY. A key's section is the
                    # only statement anyone makes about WHAT KIND OF NAME IT IS, and without it
                    # every map key reached variants_for as "block" - so a identifying name never got
                    # its spaced form, which is the only form prose actually writes it in.
                    section_of.setdefault(key, set()).add(low)
            elif low in STRUCTURED_SECTIONS:
                recognised += 1
                ignored[low] = ignored.get(low, 0) + len(body)
            else:
                ignored["UNKNOWN:" + section] = ignored.get("UNKNOWN:" + section, 0) + len(body)
        if recognised == 0:
            dead.append(stem)

    # *** A DOTTED KEY DECLARES ITS HEAD, AND NOTHING WAS READING THAT. ***
    # A `Tags` entry is a STRUCTURED rename - `X.Y -> A.B` says X becomes A and Y becomes B - and
    # flattening it into one text substitution keeps only the whole-string claim. So a head declared
    # ONLY in Tags got no rule of its own: written alone it was neither rewritten by the builder nor
    # hunted by the verifier, because both treat the dotted key atomically. Measured on this
    # repository, on an artifact that had already passed the identifier half of Gate 3: SIX live
    # head names occur standalone in the corpus and survived the rewrite untouched, invisible to
    # every gate in the pipeline.
    #
    # It is also why composed and decomposed forms disagreed - `X.Y` moved and `X` did not, so any
    # file that builds the dotted form from its parts ended up with halves that no longer matched.
    # One defect, two symptoms, and only the symptom that broke a test was visible.
    #
    # DERIVED ONLY WHERE THE DOTTED KEYS AGREE, AND NEVER OVER THE MAPS THEMSELVES. A head the maps
    # state directly keeps what they say - an inference must not outrank a declaration. Where two
    # dotted keys rename one head two different ways, that is returned rather than resolved: it is
    # the same shape as the non-injective finding and equally the owner's to settle.
    head_votes, head_sections = {}, {}
    for key, reps in list(pairs.items()):
        if "." not in key or not key.split(".", 1)[0]:
            continue
        khead = key.split(".", 1)[0]
        for rep, saying in reps.items():
            if "." in rep and rep.split(".", 1)[0]:
                head_votes.setdefault(khead, {}).setdefault(rep.split(".", 1)[0], []).extend(saying)
        head_sections.setdefault(khead, set()).update(section_of.get(key, ()))

    derived, head_conflicts = 0, []
    for khead, votes in sorted(head_votes.items()):
        if khead in pairs:
            continue                                        # the maps state this head themselves
        if len(votes) > 1:
            head_conflicts.append(khead)
            continue
        rep = list(votes)[0]
        if rep == khead:
            continue                                        # identity: nothing to rewrite
        pairs[khead] = {rep: sorted(set(votes[rep]))}
        section_of.setdefault(khead, set()).update(head_sections.get(khead, ()))
        derived += 1

    return (stems, boms, used, ignored, dead, pairs, malformed, section_of, derived,
            head_conflicts)


def load_terms(path):
    """The explicit term list: `| live | invented | class | scope | variants |`.

    This carries what the maps do not - job codes, site and site names - and it is the half of
    the vocabulary that found the 44 files the maps miss. A malformed row is exit 2, NEVER a skip:
    a term list that silently shrinks is the failure mode this whole design is built around."""
    rows, bad = [], []
    if not os.path.isfile(path):
        return None, []
    for n, line in enumerate(io.open(path, encoding="utf-8"), 1):
        line = line.strip()
        if not line.startswith("|") or line.startswith("|--") or line.startswith("| ---"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if cells[0].lower() in ("live", "term", "source"):
            continue                                        # header row
        if len(cells) < 5:
            # Was `if len(cells) < 3: continue` above this - a row that lost columns to a typo was
            # dropped without ever reaching `bad`, which is the exact silent shrink the docstring
            # says must never happen. Every non-header table row now either parses or refuses.
            bad.append((n, "needs 5 columns: live | invented | class | scope | variants, got %d"
                        % len(cells)))
            continue
        live, invented, klass, scope, variants = cells[:5]
        if not live or not invented:
            bad.append((n, "live and invented are both required"))
        elif klass.lower() not in TERM_CLASSES:
            bad.append((n, "class must be one of %s" % ", ".join(TERM_CLASSES)))
        else:
            rows.append({"live": live, "invented": invented, "class": klass.lower(),
                         "scope": scope or "global",
                         "variants": None if variants.lower() in ("auto", "") else
                                     [v.strip() for v in variants.split(";") if v.strip()]})
    return rows, bad


def split_camel(name):
    """Split before an uppercase preceded by lowercase/digit, and before an uppercase followed by a
    lowercase and preceded by an uppercase. Digits stay attached to the token before them."""
    s = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", name)
    s = re.sub(r"(?<=[A-Z])(?=[A-Z][a-z])", " ", s)
    return [t for t in re.split(r"[\s_\-.]+", s) if t]


def variants_for(key, klass):
    """Every written form the same identifier takes in this repository.

    *** THE LOWER-CONCATENATED FORM IS THE WHOLE A8 FIX AND IS NOT OPTIONAL. ***
    A directory named `<lowerconcat>-bench` is NOT matched by the kebab variant, which would insert
    hyphens inside the stem. It IS matched by `\\b<lowerconcat>\\b`, because `-` is a non-word
    character and therefore a word boundary on the right. Emitting kebab and calling the case
    handled is exactly the error the audit caught."""
    if "." in key:
        # A dotted tag path is one whole-path rule. Splitting it into components manufactures two
        # bare-word keys per tag and detonates the collision budget for no coverage gain.
        return [key]
    parts = split_camel(key)
    if not parts:
        return [key]
    lower = [p.lower() for p in parts]
    out = [key, "".join(lower), "-".join(lower), "_".join(lower), "_".join(p.upper() for p in lower)]
    if re.search(r"\d$", key):
        stem, digits = re.match(r"^(.*?)(\d+)$", key).groups()
        low = "".join(p.lower() for p in split_camel(stem))
        out += [low + "-" + digits, low + digits]
    if klass in SPACED_CLASSES:
        out += [" ".join(parts), " ".join(lower)]
    seen, uniq = set(), []
    for v in out:
        if v and v not in seen:
            seen.add(v)
            uniq.append(v)
    return uniq


def resolve_conflict(key, candidates, reference_blob):
    """A total ladder. Returns (chosen, rung).

    Every candidate is already an owner-approved sanitized name, so the CHOICE IS A CONSISTENCY
    QUESTION, NOT A LEAK QUESTION - any of them is safe. Rung 4 is arbitrary and exists only to
    make the ladder total; the run log names what fell to it so the arbitrariness stays visible."""
    if len(candidates) == 1:
        return list(candidates)[0], 0
    in_ref = [c for c in sorted(candidates)
              if re.search(r"\b" + re.escape(c) + r"\b", reference_blob, re.IGNORECASE)]
    if len(in_ref) == 1:
        return in_ref[0], 1
    # Rung 2 reads "the block's own map is authoritative for its own name". It LOOKS like a type
    # confusion - `stems` holds map filenames and `key` is a vocabulary key - and it is not: the maps
    # are named after the blocks they describe, and 41 of 43 stems are themselves keys, so this fires
    # routinely on the real corpus. Measured before touching it, because it reads like a bug.
    own = [c for c, stems in sorted(candidates.items()) if key in stems]
    if len(own) == 1:
        return own[0], 2
    # *** DISTINCT SOURCES, NOT OCCURRENCES. *** `stems` is appended to once per (section, key), not
    # once per file, so ONE map declaring the same key in both `names` and `tags` voted twice and
    # could outvote two maps that each said so once. The list is deliberately left un-deduplicated
    # at the point it is built - it records where each vote came from, which is worth keeping - and
    # the collapse happens here, where the question being asked is "how many maps agree".
    ranked = sorted(candidates.items(), key=lambda kv: (-len(set(kv[1])), kv[0]))
    if len(ranked) > 1 and len(set(ranked[0][1])) > len(set(ranked[1][1])):
        return ranked[0][0], 3
    return sorted(candidates)[0], 4


class ScanFailed(Exception):
    """The corpus could not be read. Raised rather than returned so it cannot be mistaken for an
    empty corpus - the two are the same value and opposite conclusions."""


def all_blobs(repo, scan):
    """(identifier, text) for every blob. `history` walks the whole object database; `head` walks
    the worktree and says so. A blob that is not valid UTF-8 comes back as None so the caller can
    count it - A BLOB NOBODY COULD SEARCH MUST NEVER BE COUNTED AS CLEAN."""
    if scan == "head":
        for rel in git(repo, "ls-files").split("\n"):
            if not rel.strip():
                continue
            full = os.path.join(repo, rel)
            try:
                yield rel, io.open(full, "rb").read().decode("utf-8")
            except (OSError, UnicodeDecodeError):
                yield rel, None
        return
    # ONE pass, NO STDIN, streamed. Three separate reasons, each measured:
    #
    # 1. No stdin at all removes the deadlock class entirely. The earlier shape fed object names in
    #    on stdin, and passing a str with text=False makes stdin.write raise while git sits blocked
    #    on a stdin nobody closed - 99.5s to a timeout with zero bytes read, which reads exactly
    #    like a slow repository rather than a defect.
    # 2. `capture_output=True` materialises all 242 MB before a single byte is examined, which is
    #    the very thing scan_stream exists to avoid. Popen + incremental reads hold one blob.
    # 3. It is faster: the whole object database streams into Python in about 0.8s. The bottleneck
    #    on Windows is the SHELL PIPE, not git - the same bytes through an MSYS pipe cost ~15s - so
    #    git's stdout is read directly here and never piped through sh.
    proc = subprocess.Popen(["git", "cat-file", "--batch-all-objects", "--batch"],
                            cwd=repo, stdout=subprocess.PIPE)
    stream = proc.stdout
    try:
        while True:
            header = stream.readline()
            if not header:
                break
            parts = header.split()
            if len(parts) < 3:
                # `git cat-file --batch` emits "<oid> missing" for an unreadable object. Breaking
                # here silently abandoned the REST OF HISTORY while leaving a non-zero scanned
                # count, so the EMPTY-IS-NOT-CLEAN guard never fired and the run exited 0 having
                # examined a fraction of the corpus. An unreadable object is unsearchable, and a
                # blob nobody could search must never be counted as clean.
                yield (parts[0].decode("ascii", "replace") if parts else "?"), None
                continue
            name, otype, size = parts[0], parts[1], int(parts[2])
            body = stream.read(size)
            stream.read(1)                                  # the trailing newline
            if otype != b"blob":
                continue                                    # trees and commits are scanned elsewhere
            try:
                yield name.decode("ascii"), body.decode("utf-8")
            except UnicodeDecodeError:
                yield name.decode("ascii"), None
    finally:
        stream.close()
        rc = proc.wait()
    # THE EXIT CODE WAS DISCARDED HERE. A `git cat-file` that fails outright - an unreadable object
    # database, a path that is not a repository - closes its stdout immediately, so the loop above
    # ends after zero blobs and the caller receives a perfectly clean, perfectly empty corpus. In a
    # tool whose whole doctrine is that empty is not clean, on the mode that produces the published
    # artifact, that is the worst available failure: silent, confident, and indistinguishable from
    # a repository with nothing to find.
    if rc != 0:
        raise ScanFailed("git cat-file exited %d and examined nothing" % rc)


WORD_RUN = re.compile(r"[A-Za-z0-9_]+")

# A SECOND, WIDER RUN, used only to answer "does this declared term occur anywhere".
# WORD_RUN strips dots, hyphens and slashes, so a declared term containing one can never be a
# substring of a blob built from it - the check returns False for every dotted term no matter how
# many times it occurs. Measured: a 3-character site term with a dot in it occurred 33 times in
# the corpus, got no rule, and survived the rewrite. It is the same shape of error as tokenising
# needles too narrowly on the verifier side: THE TOKEN CLASS MUST COVER THE NEEDLE, not just the
# language the corpus happens to be written in.
WIDE_RUN = re.compile(r"[A-Za-z0-9_.@/-]+")


def scan_stream(make_iter, needles, prelude=()):
    """Blob-count per needle, STREAMING. Returns (counts, searched, unsearchable, seen).

    `prelude` texts are scanned for counts and vocabulary but are NOT counted as blobs. The path
    corpus is fed in that way, and the distinction is load-bearing rather than tidy: it used to be
    chained in with the blobs, it is a str and never None, so `searched` was >= 1 no matter what
    happened afterwards and the EMPTY-IS-NOT-CLEAN guard in main() COULD NOT FIRE. A run that read
    not one blob still reported "blobs scanned: 1" and carried on.

    *** THREE OBVIOUS IMPLEMENTATIONS FAIL ON THIS CORPUS, ALL MEASURED ON THIS MACHINE. ***

    1. One `re.search` per needle over the corpus is O(needles x corpus): roughly sixteen hundred
       passes over 232 MB of history. It does not finish.
    2. A combined alternation looks like the fix and is not. Python's `re` has no DFA, so a
       400-branch alternation is tried BRANCH BY BRANCH at every position - the same multiplication
       wearing a different hat.
    3. Tokenising, but holding every blob in a list first, dies on MEMORY: history is 232 MB across
       6,359 blobs, and as decoded Python strings that is most of a gigabyte before any work starts.

    What works: stream the blobs, never retain them, and tokenise each one once. A needle made only
    of word characters matches `\\b...\\b` exactly when it IS one of that blob's `[A-Za-z0-9_]+`
    runs - a set intersection. A needle spanning several runs (a dotted tag path, a kebab form)
    is prefiltered on whether ALL its components are among this blob's tokens, which short-circuits
    on the first miss and costs a few dict lookups; only survivors pay for a real regex, and in
    practice almost none do."""
    simple, complex_ = set(), {}
    for n in set(needles):
        if WORD_RUN.fullmatch(n):
            simple.add(n)
        else:
            parts = WORD_RUN.findall(n)
            if parts:
                complex_[n] = (parts, re.compile(r"\b" + re.escape(n) + r"\b"))

    counts, searched, unsearchable, seen = {}, 0, 0, set()

    def absorb(text):
        tokens = set(WORD_RUN.findall(text))
        # `seen` is the corpus vocabulary the declared-presence check and the collision check read,
        # so it uses the WIDE class: a needle containing a dot must be findable in it.
        seen.update(tokens)
        seen.update(WIDE_RUN.findall(text))
        for token in simple.intersection(tokens):
            counts[token] = counts.get(token, 0) + 1
        for n, (parts, pat) in complex_.items():
            if all(p in tokens for p in parts) and pat.search(text):
                counts[n] = counts.get(n, 0) + 1

    for text in prelude:
        if text is not None:
            absorb(text)
    for text in make_iter():
        if text is None:
            unsearchable += 1
            continue
        searched += 1
        absorb(text)
    return counts, searched, unsearchable, seen


def main():
    ap = argparse.ArgumentParser(description="Emit git-filter-repo de-identification rules.")
    ap.add_argument("--repo", default=".", help="repository root")
    ap.add_argument("--maps", default="sanitization", help="directory of *.map.json")
    ap.add_argument("--terms", default="sanitization/scrub-terms.md",
                    help="explicit term list (job codes, site and site names)")
    ap.add_argument("--out", default="sanitization/scrub", help="output directory")
    ap.add_argument("--green", action="append", default=None,
                    help="an already-sanitized corpus; repeatable. Rules are path-scoped to leave "
                         "these alone, which is how an ordinary word that is also a map key is "
                         "handled without a human.")
    ap.add_argument("--job-folder", action="append", default=None,
                    help="a live-job folder; every replacement is grepped back against it")
    ap.add_argument("--scan", choices=("history", "head"), default="history")
    ap.add_argument("--min-global-length", type=int, default=MIN_GLOBAL_LENGTH)
    ap.add_argument("--explain-cuts", action="store_true",
                    help="for each row the CUTS check refuses, print the actual tokens: what its "
                         "rule would corrupt in build source, and every token in the corpus that "
                         "contains the term, which is where the `variants` cell comes from. OFF BY "
                         "DEFAULT and for the same reason as --name-collisions: these are live "
                         "identifiers. Use it at a terminal you are watching, not in a pipeline, "
                         "and do not paste the output into a document.")
    ap.add_argument("--name-collisions", action="store_true",
                    help="print the colliding strings themselves. OFF BY DEFAULT because this "
                         "output gets pasted into notes, and a record of a leak must not be a copy "
                         "of it. Use it at a terminal you are watching, not in a pipeline.")
    args = ap.parse_args()

    repo = os.path.abspath(args.repo)
    green = args.green or ["ir/reference", "ir/test-project001", "gen/test-project001",
                           "simatic-ml/reference", "simatic-ml/test-project001"]
    job_folders = args.job_folder or ["Live Runs"]
    out_dir = os.path.join(repo, args.out)

    # ---- Refusals, before anything is read ----------------------------------------------------
    terms_rel = os.path.relpath(os.path.abspath(os.path.join(repo, args.terms)), repo)
    terms_rel = terms_rel.replace("\\", "/")
    if is_tracked(repo, terms_rel):
        print("REFUSED: '%s' is TRACKED BY GIT." % terms_rel, file=sys.stderr)
        print("That file is the identifier list. It being in the index means the leak list has "
              "been committed, which is the exact condition this tooling exists to prevent. The "
              "tool that reads it is the last thing that should be quiet about that.\n"
              "Remove it from the index and confirm sanitization/ is ignored before re-running.",
              file=sys.stderr)
        return 3
    # *** THE PREDICATE IS "CAN GIT SEE IT", NOT "DOES GIT TRACK IT". ***
    # `git ls-files` only knows what is already in the index, so `--out docs/scrub-out` - a new
    # directory inside tracked `docs/` - passed the old check and wrote the file naming every
    # identifier in the repository into tracked space, one `git add -A` from being committed.
    # `git check-ignore` asks the question that actually matters. It is also case-correct: ls-files
    # pathspecs are case-sensitive even where core.ignorecase is true, so on NTFS `--out DOCS`
    # resolved to the same directory as `docs/` and passed.
    out_rel = os.path.relpath(out_dir, repo).replace("\\", "/")
    ignored = subprocess.run(["git", "check-ignore", "-q", out_rel],
                             cwd=repo, capture_output=True).returncode == 0
    if not ignored:
        print("REFUSED: --out '%s' is NOT ignored by git." % out_rel, file=sys.stderr)
        print("The rule files name every identifier in the repository. Writing them where git can "
              "see them turns the scrub's input into the thing it is scrubbing. Add the directory "
              "to .gitignore, or point --out somewhere already ignored.", file=sys.stderr)
        return 3

    # ---- Inputs ---------------------------------------------------------------------------------
    (stems, boms, used, ignored, dead, pairs, bad_values, section_of,
     derived_heads, head_conflicts) = load_maps(os.path.join(repo, args.maps))
    rows, bad_rows = load_terms(os.path.join(repo, args.terms))

    print("maps read                     : %d" % len(stems))
    print("  with a UTF-8 BOM            : %d" % boms)
    print("  sections USED               : %s" % (", ".join("%s=%d" % kv for kv in sorted(used.items())) or "none"))
    print("  sections IGNORED            : %s" % (", ".join("%s=%d" % kv for kv in sorted(ignored.items())) or "none"))
    if dead:
        print("  maps with NO recognised section: %d (%s)" % (len(dead), ", ".join(dead)))
    print("  bare heads DERIVED from dotted keys: %d  [a dotted key declares its head]"
          % derived_heads)
    if head_conflicts:
        print("  heads whose dotted keys DISAGREE  : %d" % len(head_conflicts))
    if bad_values:
        print("  entries DROPPED, value not a string: %d" % len(bad_values))
        for why in bad_values[:10]:
            print("      %s" % why)

    if not stems:
        print("\nNOTHING EXAMINED: no *.map.json under '%s'. EMPTY IS NOT CLEAN." % args.maps)
        return 2
    if rows is None:
        print("\nNOTHING EXAMINED: no term list at '%s'." % terms_rel)
        print("The maps alone MISS 44 identifier-bearing files - job codes and identifying names are "
              "barely present in them. A run without the term list is not a partial run, it is a "
              "run against the wrong vocabulary.")
        return 2
    if bad_rows:
        print("\nNOTHING EXAMINED: %d malformed row(s) in '%s'." % (len(bad_rows), terms_rel))
        for n, why in bad_rows[:20]:
            print("  line %d: %s" % (n, why))
        print("A malformed row is refused rather than skipped: a term list that silently shrinks is "
              "the failure this design exists to prevent.")
        return 2
    print("term rows read                : %d of %d" % (len(rows), len(rows) + len(bad_rows)))
    if not rows:
        print("\nNOTHING EXAMINED: the term list parsed to ZERO rows. EMPTY IS NOT CLEAN.")
        return 2

    # ---- Vocabulary ------------------------------------------------------------------------------
    identity = {k for k, v in pairs.items() if set(v) == {k}}
    live_keys = {k: v for k, v in pairs.items() if k not in identity}
    print("keys after identity-drop      : %d (dropped %d no-ops)" % (len(live_keys), len(identity)))

    reference_blob = ""
    for corpus in green:
        for rel in git(repo, "ls-files", corpus).split("\n"):
            if rel.strip():
                try:
                    reference_blob += io.open(os.path.join(repo, rel), encoding="utf-8",
                                              errors="replace").read()
                except OSError:
                    pass

    rungs = {0: 0, 1: 0, 2: 0, 3: 0, 4: 0}
    rung4 = []
    chosen = {}
    for key, candidates in sorted(live_keys.items()):
        pick, rung = resolve_conflict(key, candidates, reference_blob)
        rungs[rung] += 1
        if rung == 4:
            rung4.append(key)
        chosen[key] = pick
    print("conflicts resolved            : %d (rung1=%d rung2=%d rung3=%d rung4=%d)"
          % (sum(rungs[r] for r in (1, 2, 3, 4)), rungs[1], rungs[2], rungs[3], rungs[4]))

    # *** TWO ROWS CAN DECLARE THE SAME TERM, AND THE THREE LINES BELOW ARE PLAIN ASSIGNMENTS. ***
    # Row order decides which invented name a live identifier becomes, silently: no counter, no
    # finding, nothing in the manifest. It is the SAME DEFECT AS F9 in a different dict - F9 was
    # `candidates.setdefault` discarding a claim without saying so, and this is `chosen[...] = ...`
    # overwriting one.
    #
    # It is live in the real term list, which is how it was found: one 5-character term is declared
    # by two rows, as `site` and as `jobcode`, with different replacements. The surviving one is
    # sensible and the discarded one is the bare token `None` - so today the last row happens to be
    # the right one, and REORDERING THE TABLE WOULD REWRITE A SITE NAME TO `None` THROUGHOUT THE
    # CORPUS with nothing said. A silent choice between two of the owner's own declarations is the
    # owner's to make.
    #
    # Case-insensitive, because the emitted rule is: two rows differing only in capitalisation
    # produce one `(?i)` rule, so they are the same declaration whatever the table looks like.
    # Agreeing rows are reported rather than refused - a duplicate that changes nothing is a tidiness
    # problem, and refusing it would be this tool deciding how the owner keeps their own list.
    by_term, duplicate_terms = {}, []
    for r in rows:
        by_term.setdefault(r["live"].lower(), []).append(r)
    redundant = 0
    for low, these in sorted(by_term.items()):
        if len(these) < 2:
            continue
        reps = sorted({r["invented"] for r in these})
        if len(reps) == 1:
            redundant += 1
            continue
        duplicate_terms.append(
            "one %d-character term is declared by %d rows (%s) that DISAGREE on the replacement: "
            "%s. The last row in the table silently wins. Delete a row or make them agree."
            % (len(low), len(these), ", ".join(sorted(r["class"] for r in these)),
               " vs ".join("'%s'" % r for r in reps)))
    print("duplicate term rows           : %d agreeing (reported), %d DISAGREEING"
          % (redundant, len(duplicate_terms)))

    # Explicit terms beat map-derived keys unconditionally, and are logged where they override.
    overrides = [r["live"] for r in rows if r["live"] in chosen]
    for r in rows:
        chosen[r["live"]] = r["invented"]
    klass_of = {r["live"]: r["class"] for r in rows}
    forced_variants = {r["live"]: r["variants"] for r in rows if r["variants"]}
    print("term rows overriding a map key: %d" % len(overrides))

    # *** A SEPARATE DICT, AND THE SEPARATION IS THE WHOLE CARE OF THIS FIX. ***
    # `klass_of` does not mean "this key's class". It means THE OWNER WROTE THIS KEY DOWN BY HAND,
    # and three other decisions read it that way: `declared` bypasses the length floor and the Green
    # test, and the emitted rule is anchorless and case-insensitive rather than \b-anchored. Folding
    # map keys into it to give them a class would silently promote every one of ~1,390 inferred
    # keys to declared status - a far larger change than the one being made, wearing the costume of
    # a one-line fix.
    section_class_of = {}
    for key, sections in section_of.items():
        for low in sorted(sections):
            if low in SECTION_CLASS:
                section_class_of[key] = SECTION_CLASS[low]
                break
    spaced_eligible = sum(1 for k in section_class_of if k in chosen)
    print("map keys with a section class : %d  [spaced forms; NOT declared]" % spaced_eligible)

    if not chosen:
        print("\nNOTHING EXAMINED: no key survived derivation. EMPTY IS NOT CLEAN.")
        return 2

    # ---- Variants ---------------------------------------------------------------------------------
    # *** TWO KEYS CAN PRODUCE THE SAME WRITTEN FORM, AND THIS USED TO BE `candidates.setdefault`. ***
    # setdefault keeps whichever key sorted first and discarded the rest IN SILENCE - no counter, no
    # finding, nothing in the manifest. Three things followed, and the third is the serious one:
    #
    #   1. Partial collision: the variant was emitted with the WINNER's replacement, so two live
    #      identifiers collapsed onto one invented name. The NON-INJECTIVE check cannot see this -
    #      it reads `rules`, and the loser never reaches `rules`.
    #   2. Total collision: the losing key got NO RULE AT ALL, and was counted in none of
    #      withheld_short, withheld_green or dead_variants, because those are per-variant. The run's
    #      own arithmetic balanced perfectly with a key missing.
    #   3. A map key could squat a DECLARED term's variant. `declared` is evaluated on the winning
    #      key, so that form was emitted with the narrow case-sensitive \b rule instead of the
    #      anchorless (?i) one - silently reintroducing F1 through a path neither guard covers.
    claims = {}
    for key in sorted(chosen):
        # A term row's class still wins: the owner's own statement about a key outranks the section
        # a map happened to file it under.
        klass = klass_of.get(key) or section_class_of.get(key, "block")
        for v in (forced_variants.get(key) or variants_for(key, klass)):
            claims.setdefault(v, []).append(key)

    candidates = {}
    collision_same, collision_declared, collision_problems = 0, 0, []
    for v, keys in sorted(claims.items()):
        if len(keys) == 1:
            candidates[v] = keys[0]
            continue
        reps = {chosen[k] for k in keys}
        declared = [k for k in keys if k in klass_of]
        if len(reps) == 1:
            # Harmless: whichever key claimed it, the text becomes the same thing. A declared
            # claimant is still preferred so the rule keeps its anchorless, case-insensitive form.
            candidates[v] = sorted(declared or keys)[0]
            collision_same += 1
        elif len(declared) == 1:
            candidates[v] = declared[0]
            collision_declared += 1
        else:
            # Both remaining shapes are the owner's to resolve, not this tool's. Emitting either
            # choice maps two DISTINCT live identifiers onto one invented name, which is precisely
            # what the NON-INJECTIVE gate exists to prevent and structurally cannot see from here.
            candidates[v] = sorted(declared or keys)[0]
            collision_problems.append(
                "%d keys claim the same written form and disagree on the replacement%s"
                % (len(keys), " - BOTH ARE DECLARED TERMS" if len(declared) > 1 else ""))
    print("variant collisions            : %d  (%d harmless, %d resolved to a declared term, "
          "%d unresolved)"
          % (collision_same + collision_declared + len(collision_problems),
             collision_same, collision_declared, len(collision_problems)))

    # ---- Occurrence scan --------------------------------------------------------------------------
    # ONE streaming pass. Nothing is retained: see scan_stream for the three implementations that
    # do not survive this corpus.
    # PATH NAMES ARE PART OF THE CORPUS, and are easy to forget because `git cat-file` never shows
    # them: a variant can occur ONLY in a directory name, in which case a content-only scan calls it
    # dead and emits no rule, and the directory sails through the rewrite. Every path ever recorded,
    # not just HEAD's, for the same reason the blob scan walks history.
    all_paths = "\n".join(
        ln.split(" ", 1)[1] for ln in git(repo, "rev-list", "--objects", "--all").split("\n")
        if " " in ln)

    try:
        blob_hits, searched, unsearchable, corpus_tokens = scan_stream(
            lambda: (text for _, text in all_blobs(repo, args.scan)), candidates,
            prelude=[all_paths])
    except ScanFailed as exc:
        print("\nNOTHING EXAMINED: %s. EMPTY IS NOT CLEAN." % exc)
        return 2
    green_hits, _, _, _ = scan_stream(
        lambda: iter([reference_blob.lower()]), [v.lower() for v in candidates])

    # *** BREADTH IS MEASURED AT HEAD, NEVER OVER HISTORY, AND THE DIFFERENCE IS NOT COSMETIC. ***
    # The demotion below asks "does this behave like an ordinary word rather than an identifier",
    # and answers it by counting how many distinct FILES carry it. Counted over history instead, a
    # file edited thirty times contributes thirty blobs, so an ordinary-looking count is
    # manufactured by editing activity. Measured on this repository: the same vocabulary demoted 8
    # variants at HEAD and 22 over history, and those 14 extra demotions are rules NOT EMITTED -
    # identifiers left in the artifact because a file had been revised a lot. Presence still comes
    # from history, which is the question history is the right corpus for.
    head_hits = blob_hits if args.scan == "head" else scan_stream(
        lambda: (text for _, text in all_blobs(repo, "head")), candidates)[0]

    print("blobs scanned (%-7s)      : %d  (unsearchable: %d)" % (args.scan, searched, unsearchable))
    if args.scan == "head":
        print("  *** THIS DID NOT EXAMINE HISTORY. *** A key occurring only in an old commit is "
              "still in the artifact being published, and this run cannot see it.")
    if not searched:
        print("\nNOTHING EXAMINED: zero readable blobs. EMPTY IS NOT CLEAN.")
        return 2

    # For declared terms, presence is a SUBSTRING question. `blob_hits` is boundary-aware, so a
    # short declared term occurring only inside a larger token reads as absent there.
    token_blob = "\n".join(sorted(corpus_tokens)).lower()

    # *** THE VOCABULARY A COMPILER RESOLVES, WHICH IS A DIFFERENT CORPUS FROM THE ONE ABOVE. ***
    # HEAD only, and deliberately: the question this answers is "does the published artifact still
    # build", and the build Gate 3 compares is HEAD's. A historical source blob cannot be scoped by
    # extension anyway - `git cat-file --batch-all-objects` yields object ids, not paths.
    source_tokens, source_files = set(), 0
    for rel in git(repo, "ls-files").split("\n"):
        if not rel.strip() or not rel.endswith(BUILD_SOURCE_EXT):
            continue
        try:
            src = io.open(os.path.join(repo, rel), encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        source_files += 1
        source_tokens.update(t.lower() for t in WORD_RUN.findall(src))
        source_tokens.update(t.lower() for t in WIDE_RUN.findall(src))

    def enclosing_tokens(v):
        """Distinct corpus tokens that CONTAIN v without being v.

        *** THIS IS NOT A PROXY FOR THE DAMAGE AN ANCHORLESS RULE DOES. IT IS THE DAMAGE. ***
        A declared term is emitted `(?i)<term>` with no word boundary, so filter-repo rewrites the
        term wherever it appears INCLUDING INSIDE A LARGER WORD. Every token in this set is one
        the rewrite silently edits, and until now nothing counted them: the filter loop below gives
        declared terms an unconditional `continue`, so they are the one population that reaches the
        widest matcher in the tool having been measured by none of its breadth tests.

        `token_blob` is one lowercased token per line, so the enclosing token is the line the hit
        landed on. `start = pos + 1` rather than `pos + len(low)` because a term can occur twice in
        one token; advancing by one keeps the walk linear over the blob either way."""
        low = v.lower()
        found, start = set(), 0
        while True:
            pos = token_blob.find(low, start)
            if pos < 0:
                return found
            start = pos + 1
            left = token_blob.rfind("\n", 0, pos) + 1
            right = token_blob.find("\n", pos)
            token = token_blob[left:right] if right >= 0 else token_blob[left:]
            if token != low:
                found.add(token)

    rules, withheld_short, withheld_green, wide, dead_variants = [], [], [], [], 0
    declared_emitted = 0
    declared_variants, declared_breadth = [], []
    for v, key in sorted(candidates.items()):
        declared = key in klass_of
        if v not in blob_hits and not (declared and v.lower() in token_blob):
            dead_variants += 1                             # absent from this repository: no rule needed
            continue

        # *** A DECLARATION IS NOT A CANDIDATE. ***
        # The length floor and the Green test exist to stop a name INFERRED from a map colliding
        # with ordinary code. Neither reasoning applies to a term the owner wrote down by hand, and
        # applying them anyway silently discards exactly the vocabulary the term list exists for.
        # Measured before this bypass: 15 of 19 term rows were under the floor - INCLUDING ALL FOUR
        # JOB CODES, which are five characters - so 11 of the 12 identifier occurrences in the
        # tracked `.gitignore` survived a full application of all 98 rules. The term list is the
        # half of the vocabulary that found the 44 files the maps miss; filtering it is self-
        # defeating.
        if declared:
            rules.append((v, chosen[key], key))
            declared_emitted += 1
            declared_variants.append((v, key))
            continue

        if len(v) < args.min_global_length:
            withheld_short.append(v)
            continue
        if v.lower() in green_hits:
            withheld_green.append(v)
            continue
        if head_hits.get(v, 0) > ORDINARY_WORD_BLOB_THRESHOLD and "." not in v:
            # REPORTED, NOT WITHHELD - and the distinction cost a measured A8 regression to learn.
            # Withholding here looks principled ("it behaves like an ordinary word") and is exactly
            # backwards: a path stem cited across thirty-eight files is WIDE BECAUSE IT IS
            # LOAD-BEARING, not because it is ordinary. Measured on this repository, withholding on
            # breadth left 77 of 93 identifier-bearing PATHS unmatched - the precise failure A8
            # names. The Green-corpus test is the principled filter for "conventional name"; this
            # one is a report, on the same footing as check-file-budgets.py's slack floor.
            wide.append("%s files=%d" % (len(v) * "*", head_hits[v]))
        rules.append((v, chosen[key], key))

    print("variants considered           : %d" % len(candidates))
    print("  absent from this repository  : %d (no rule needed)" % dead_variants)
    print("variants emitted              : %d" % len(rules))
    print("  from DECLARED term rows      : %d  [bypass the floor and the Green test]"
          % declared_emitted)
    print("  from inferred map keys       : %d" % (len(rules) - declared_emitted))
    print("  withheld, under %d chars     : %d  [map keys only]"
          % (args.min_global_length, len(withheld_short)))
    print("  withheld, present in Green   : %d  [map keys only]" % len(withheld_green))
    print("  wide (>%d files) - REPORT ONLY: %d  [emitted anyway; see the comment at the test]"
          % (ORDINARY_WORD_BLOB_THRESHOLD, len(wide)))

    # *** THE RULE SET IS APPLIED LONGEST-FIRST, AND MEASURING EACH NEEDLE ALONE IGNORES THAT. ***
    # Computed here rather than inside the loop above because it needs the FINISHED needle set: a
    # token is only collateral if the shorter needle can still reach it after every LONGER rule has
    # already rewritten that token, and until the loop ends there is no way to know which needles
    # survive filtering.
    #
    # Measured on the real vocabulary: a 3-character modelline term is a PREFIX of a 4-character one
    # that has its own row, so the longer rule fires first and the only build-source token the
    # shorter one appeared to threaten was never reachable. The check named collateral that ordering
    # already protects - the right verdict for that row, reached by a wrong reason, which is the
    # kind of finding that gets argued with and then disbelieved when it is right.
    #
    # Longer rules are applied with their REAL replacements rather than a neutral placeholder, so a
    # replacement that happens to re-create the shorter needle is not hidden here. That case is also
    # what the F4 overlap check gates on, which is why this one can afford to simply report what the
    # substitution actually produces.
    rep_of = {v: rep for v, rep, _ in rules}
    longest_first = sorted(rep_of, key=lambda s: (-len(s), s))

    def reachable_after_longer_rules(token, v):
        text = token.lower()
        for w in longest_first:
            if len(w) <= len(v):
                break                                       # sorted longest-first: nothing shorter
            text = text.replace(w.lower(), rep_of[w].lower())
        return v.lower() in text

    for v, key in declared_variants:
        enc = enclosing_tokens(v)
        # The FULL enclosing set is carried alongside the source-only one because the two answer
        # different questions and --explain-cuts needs both: `cut` is the collateral that makes this
        # gate, while `enc` is where the owner picks the forms for a `variants` cell.
        cut = sorted(t for t in (enc & source_tokens) if reachable_after_longer_rules(t, v))
        if cut:
            declared_breadth.append((v, key, cut, v.lower() in source_tokens,
                                     not WORD_RUN.fullmatch(v), sorted(enc)))

    cuts_code = [r for r in declared_breadth if r[2] and not r[3]]
    also_whole = [r for r in declared_breadth if r[2] and r[3]]
    print("  declared terms editing source from INSIDE a token")
    print("    and never matching one whole : %d  [GATES - see below]" % len(cuts_code))
    print("    but also present whole       : %d  [report only; the rewrite is load-bearing there]"
          % len(also_whole))

    if not rules:
        print("\nNOTHING EXAMINED: every variant was filtered out - no rule would be emitted.")
        print("EMPTY IS NOT CLEAN: this is a refusal, not a clean repository.")
        return 2

    # ---- Blocking checks --------------------------------------------------------------------------
    findings = []

    # Collected while building `candidates`, long before this list existed. An unresolved variant
    # collision is the owner's call: two identifiers they named separately would be rewritten to
    # one name, and no automatic choice here is better than telling them.
    findings.extend(collision_problems)
    findings.extend(duplicate_terms)

    # Two dotted keys that rename one head two different ways. Resolving it here would pick a winner
    # for a live identifier on no authority at all, which is the same reason the variant-collision
    # check hands its unresolved cases back rather than choosing.
    if head_conflicts:
        findings.append(
            "%d head(s) shared by several dotted map keys are renamed INCONSISTENTLY by them. A "
            "dotted key declares its head, so two keys disagreeing about one head means one live "
            "name would leave the rewrite as two. Make the maps agree, or state the head directly "
            "in a Names section, which outranks anything inferred." % len(head_conflicts))

    def substitute_replacements(ruleset, remap):
        """Apply a replacement->replacement remap, INCLUDING inside DOTTED replacements.

        *** REWRITING ONLY WHOLE REPLACEMENTS IS HOW ONE LIVE NAME LEAVES AS TWO. ***
        A map's Tags section yields dotted replacements like `Head.Member`, whose components are the
        same invented names the Names section yields on their own. Substitute only the whole string
        and the bare rule renames `Head` to `Head1` while the dotted rule still says `Head` - so one
        live identifier leaves the rewrite as two different invented names depending on whether it
        was written alone or as part of a path. The NON-INJECTIVE check cannot see this: it looks
        for several keys collapsing onto one name, and this is one key FANNING OUT.

        Measured, and it reached a published artifact: 5 such disagreements broke 8 converter tests.
        A DB fixture's member was rewritten to `FaultTripTimer1` by the bare rule while the map key
        that looks it up became `FaultTripTimer` by the dotted one, and a lookup that had never
        failed stopped finding anything. Nothing in the identifier half of Gate 3 could have caught
        that - only the build half did, which is the argument for the build half in one sentence."""
        def rewrite(rep):
            if rep in remap:
                return remap[rep]
            if "." not in rep:
                return rep
            parts = rep.split(".")
            return ".".join(remap.get(p, p) for p in parts) if any(p in remap for p in parts) \
                else rep
        return [(v, rewrite(r), k) for v, r, k in ruleset]

    # *** REWRITING PART OF A SOURCE IDENTIFIER DE-IDENTIFIES NOTHING. ***
    # A declared term is emitted `(?i)<term>` with no word boundary, chosen by declaredness alone
    # (see rule_line). That is right for a term that occurs only inside larger JOB tokens - it is
    # finding A8 in mirror image, and four terms need it. It is catastrophic for a term that occurs
    # only inside larger SOURCE tokens: the rewrite cuts a symbol in half. Measured on this
    # repository before this check existed, one three-character dotted term spanned a member access
    # and collapsed `<var>.<Member>` into a single identifier in four files, taking three solutions
    # from 5,894 passing tests to 2,196 - and the builder emitted it without a word, because
    # declared terms take an unconditional `continue` past every breadth test in the filter loop.
    #
    # THE LINE IS "WHOLE SOMEWHERE IN SOURCE", NOT LENGTH, AND NOT DOTTEDNESS.
    # Length does not separate the populations: measured here, a 3-character term and a legitimate
    # 5-character job code both had ~12 enclosing tokens. Dottedness catches only the sharpest case.
    # What separates them cleanly is whether the term is ever a source token IN ITS OWN RIGHT. If it
    # is, the rewrite is doing real work in that file and the embedded hits ride along with it. If
    # it is NEVER whole in source, every edit it makes there is to the inside of somebody else's
    # identifier, and no amount of that hides a job code - the symbol being cut was not the secret.
    #
    # It gates rather than repairs, and that is not laziness. Both anchorings are wrong for one of
    # the two populations, so there is no substitute to reach for; the fix is the `variants` column,
    # which already parses and which REPLACES the auto-derivation, so naming the forms that should
    # be rewritten is exactly how a dangerous bare form stops being emitted.
    # The REPLACEMENT names the row, and the live term never appears. `invented` is the term list's
    # own second column, so the owner reads this straight off the table they wrote - while the
    # value itself is vocabulary this tool made up, which is safe to print anywhere. A record of a
    # leak must not be a copy of it, and an unidentifiable finding is not a worklist.
    #
    # *** THE REPLACEMENT ALONE IS NOT UNIQUE, AND THE FIRST VERSION OF THIS ASSUMED IT WAS. ***
    # Two rows in the real list share one invented name - two spellings of one site, which is
    # legitimate and is exactly what a replacement is FOR - so `term row 'X'` picked out two rows
    # and the worklist it produced could not be acted on without guessing. Found by a guard written
    # to apply this check's own advice, which refused rather than editing the wrong row. The class
    # and the term's LENGTH disambiguate without disclosing anything: the owner has the table open,
    # and neither value is a character of the term.
    # Grouped by ROW, not by variant: a row usually claims several written forms, and three
    # findings for two decisions reads as a longer list than it is. The owner edits rows.
    by_row = {}
    for v, key, cut, whole, dotted, enc in cuts_code:
        n, forms, sep, allenc = by_row.get(key, (set(), 0, False, set()))
        by_row[key] = (n | set(cut), forms + 1, sep or dotted, allenc | set(enc))
    for key, (cut, forms, dotted, _enc) in sorted(by_row.items(), key=lambda kv: -len(kv[1][0])):
        findings.append(
            "term row '%s' (%s, %d characters): %d variant(s)%s edit %d source token(s) from "
            "INSIDE and none of them is a source token in its own right - an anchorless rule there "
            "cuts identifiers in half and de-identifies nothing. Give that row an explicit "
            "`variants` cell naming the forms that should be rewritten."
            % (chosen[key], klass_of.get(key, "?"), len(key), forms,
               " (one contains a SEPARATOR)" if dotted else "", len(cut)))

    # AB-1's trap 2: "a replacement can BE a leak" - one invented name in the 2026-08-27 run already
    # existed verbatim in the job's own IR. Choosing the vocabulary is part of the check.
    #
    # Scoped to .ir contents and ALL filenames, which is AB-1's own scope (its steps 1 and 5 read
    # .ir filenames and .ir member names). Reading every document in a live job instead is both
    # unnecessary and, measured on this machine, DOES NOT FINISH: 17,797 files, and concatenating
    # them exceeded 110 seconds before the check itself began. Streamed into a token SET rather than
    # one giant string, so it costs one pass and every lookup is O(1).
    job_tokens, job_files, job_bytes = set(), 0, 0
    for folder in job_folders:
        full = os.path.join(repo, folder)
        if not os.path.isdir(full):
            continue
        for base, _, files in os.walk(full):
            for name in files:
                job_files += 1
                job_tokens.update(WORD_RUN.findall(name))
                if name.lower().endswith(".ir"):
                    try:
                        text = io.open(os.path.join(base, name), encoding="utf-8",
                                       errors="replace").read()
                    except OSError:
                        continue
                    job_bytes += len(text)
                    job_tokens.update(WORD_RUN.findall(text))

    if job_files:
        print("job-folder collision check    : %d filenames, %.1f MB of .ir, %d distinct tokens"
              % (job_files, job_bytes / 1e6, len(job_tokens)))
        collided = sorted({r for _, r, _ in rules} & job_tokens)
        if collided:
            # A collision is resolved by CHANGING THE REPLACEMENT, never by dropping the key - the
            # key still has to be scrubbed. Under the no-human ruling the new replacement is derived
            # deterministically and re-checked against both the job folder and this repository, so
            # the substitute cannot itself be a leak or shadow an existing name. If no clean
            # substitute exists within the bound, that IS a refusal: we do not ship a guess.
            remap, unresolved = {}, []
            for original in collided:
                for suffix in range(1, 100):
                    candidate = "%s%d" % (original, suffix)
                    if candidate not in job_tokens and candidate not in corpus_tokens:
                        remap[original] = candidate
                        break
                else:
                    unresolved.append(original)
            rules = substitute_replacements(rules, remap)
            print("  replacements auto-substituted: %d (collided with the job's own vocabulary)"
                  % len(remap))
            if args.name_collisions:
                for original in sorted(remap):
                    print("      %s -> %s" % (original, remap[original]))
            if unresolved:
                findings.append(
                    "%d replacement(s) collide with a job folder and NO clean substitute was found "
                    "within 99 attempts. Set an explicit replacement for these in the term list."
                    % len(unresolved))
    else:
        print("job-folder collision check    : NO JOB FOLDER PRESENT - NOT PERFORMED")
        print("  *** This run did not check whether a replacement is itself a live-job name. ***")

    by_replacement = {}
    for _, replacement, key in rules:
        by_replacement.setdefault(replacement, set()).add(key)
    for replacement, keys in sorted(by_replacement.items()):
        if len(keys) > 1:
            findings.append("NON-INJECTIVE: %d distinct keys collapse onto '%s'. A collapse is how "
                            "a rename silently weakens a test that distinguished them."
                            % (len(keys), replacement))

    # *** THIS WAS A WHOLE-STRING SET INTERSECTION, AND THE ARTIFACT WAS CLEAN BY LUCK. ***
    # `replacements & searchable` fires only when a replacement is character-for-character identical
    # to a needle. filter-repo does not apply rules that way: it applies them in file order, so what
    # actually matters is whether one rule's needle MATCHES INSIDE another rule's replacement. Rule A
    # rewrites X to GenericWidgetUnit; rule B then hunts WidgetUnit and finds it sitting in A's own
    # output.
    #
    # And the case the old check could not see is the case the emitter deliberately creates: a
    # DECLARED term is emitted ANCHORLESS and CASE-INSENSITIVE, by design, so it matches inside a
    # word. The old check was also case-sensitive while those rules are (?i).
    #
    # So the predicate is evaluated with the semantics each rule will really have - the same
    # construction the path-rename pass already uses - rather than with string equality.
    def overlap_patterns(ruleset):
        return [(v, rep, key,
                 re.compile(re.escape(v), re.IGNORECASE) if key in klass_of
                 else re.compile(r"\b" + re.escape(v) + r"\b"))
                for v, rep, key in ruleset]

    def find_overlaps(ruleset):
        """Replacements that some OTHER rule's needle can match inside."""
        hits = set()
        for _, rep, key, _ in overlap_patterns(ruleset):
            for v2, _, key2, pat in overlap_patterns(ruleset):
                if v2 == rep and key2 == key:
                    continue                    # a rule does not overlap itself
                if pat.search(rep):
                    hits.add(rep)
                    break
        return sorted(hits)

    # Owner ruling 2026-09-18: auto-substitute, gate only if substitution fails - the same discipline
    # the job-folder check already uses, and for the same reason. A substitute is re-checked against
    # the job vocabulary and this repository, so it cannot itself be a leak, and the loop re-runs
    # because a fresh replacement can overlap in its turn.
    overlap_subs = {}
    for _ in range(100):
        overlapping = find_overlaps(rules)
        if not overlapping:
            break
        progressed = False
        for original in overlapping:
            for suffix in range(1, 100):
                candidate = "%s%d" % (original, suffix)
                if (candidate not in job_tokens and candidate not in corpus_tokens
                        and candidate not in {r for _, r, _ in rules}):
                    rules = substitute_replacements(rules, {original: candidate})
                    overlap_subs[original] = candidate
                    progressed = True
                    break
            if progressed:
                break
        if not progressed:
            break
    remaining = find_overlaps(rules)
    # Read by the manifest, and computed AFTER the substitution loop so it records what was actually
    # emitted rather than what was proposed.
    replacements = {r for _, r, _ in rules}
    print("replacement overlap check     : %d substituted, %d unresolved"
          % (len(overlap_subs), len(remaining)))
    if remaining:
        # WHAT SUFFIXING CAN AND CANNOT FIX, because the difference decides whether this gates.
        # An EQUALITY overlap against an inferred needle is fixable: `\bGenericFoo\b` no longer
        # matches `GenericFoo1`, since a digit is a word character and kills the right boundary.
        # A SUBSTRING overlap, or any overlap against a DECLARED needle, is not: a declared rule is
        # anchorless and case-insensitive, so it still matches inside `GenericFoo1`, and no suffix
        # removes a substring from a string. Those need a replacement the owner chooses.
        findings.append("%d replacement(s) can be matched INSIDE by another rule's needle and no "
                        "clean substitute exists - appending to a name cannot remove a substring "
                        "from it. filter-repo applies rules in file order, so the second rule "
                        "would rewrite the first one's output. Set an explicit replacement for "
                        "these in the term list." % len(remaining))

    # The sentinel is a word-character run, so the token union settles it without re-reading a byte.
    # A pre-existing hit is a REFUSAL rather than a silent retry: if this shape already occurs, the
    # assumption that a sentinel is inert does not hold and that is worth stopping for.
    sentinel = None
    for n in range(10000):
        candidate = SENTINEL_TEMPLATE % n
        if candidate not in corpus_tokens:
            sentinel = candidate
            break
    if sentinel is None:
        findings.append("no unused sentinel could be found in 10,000 attempts.")
    else:
        print("sentinel                      : %s (0 pre-existing in %d blobs)" % (sentinel, searched))

    # ---- PATH RENAMES ---------------------------------------------------------------------------
    # *** `--replace-text` REWRITES BLOB CONTENTS ONLY. IT NEVER TOUCHES A PATH. ***
    # Computed here, BEFORE the findings gate, because a rename whose target already exists is
    # SILENT FILE LOSS - git merges the two paths and nothing errors. Without this whole block the
    # tool scanned path names, emitted rules for them and renamed nothing: every identifier-bearing
    # directory and filename survived the rewrite while the run exited 0. A test asserting "an
    # emitted regex matches the path string" does NOT prove otherwise - that is a Python regex
    # applied to a string by the test, and filter-repo never does it to a path.
    # Same anchoring asymmetry as the emitted rules, or the computed rename targets would disagree
    # with what filter-repo actually produces.
    compiled = [(re.compile(re.escape(v), re.IGNORECASE) if k in klass_of
                 else re.compile(r"\b" + re.escape(v) + r"\b"), rep)
                for v, rep, k in rules]

    def rename_of(path):
        out = path
        for pat, rep in compiled:
            out = pat.sub(rep, out)
        return out

    renames, seen_paths = {}, set()
    for line in git(repo, "rev-list", "--objects", "--all").split("\n"):
        if " " not in line:
            continue
        p = line.split(" ", 1)[1].strip()
        if not p or p in seen_paths:
            continue
        seen_paths.add(p)
        new = rename_of(p)
        if new != p:
            renames[p] = new

    # Collapse to the shortest directory prefix that accounts for each change, so one --path-rename
    # covers a whole subtree. filter-repo treats a trailing-slash OLD as a directory prefix.
    dir_renames = {}
    for old, new in renames.items():
        po, pn = old.split("/"), new.split("/")
        for i in range(min(len(po), len(pn))):
            if po[i] != pn[i]:
                o, n = "/".join(po[:i + 1]), "/".join(pn[:i + 1])
                if i + 1 < len(po):
                    o, n = o + "/", n + "/"
                dir_renames[o] = n
                break

    print("paths ever recorded           : %d" % len(seen_paths))
    print("  paths the rules change      : %d" % len(renames))
    print("  --path-rename pairs emitted : %d" % len(dir_renames))

    collisions = sorted(o for o, n in dir_renames.items() if n.rstrip("/") in seen_paths)
    if collisions:
        findings.append(
            "%d --path-rename target(s) ALREADY EXIST as a path in this repository. git merges the "
            "two rather than erroring, so this is SILENT FILE LOSS. Change the replacement that "
            "produces the target." % len(collisions))
    if renames and not dir_renames:
        findings.append("paths change but no --path-rename pair was derived - the prefix collapse "
                        "produced nothing, which cannot be right.")

    if findings:
        print("\n--- GATE FAILED ---")
        for f in findings:
            print("  " + f)
        if by_row and args.explain_cuts:
            # *** THIS PRINTS LIVE IDENTIFIERS. *** It is the one place in this tool that does so
            # on purpose, because the decision it supports cannot be made without them: choosing
            # which written forms of a term should be rewritten IS reading those forms. The same
            # trade as --name-collisions, with the same instruction - a watched terminal, not a
            # pipeline, and nothing pasted into a document afterwards.
            print("\n--- THE TOKENS BEHIND THOSE FINDINGS (--explain-cuts) ---")
            print("LIVE IDENTIFIERS FOLLOW. Do not paste this into a note, an issue or a commit "
                  "message.")
            for key, (cut, forms, dotted, enc) in sorted(by_row.items(),
                                                         key=lambda kv: -len(kv[1][0])):
                print("\n  term row '%s' (%s, %d characters)"
                      % (chosen[key], klass_of.get(key, "?"), len(key)))
                print("    WOULD CORRUPT in build source (%d) - this is why the row gates:" % len(cut))
                for token in sorted(cut):
                    print("        %s" % token)
                rest = [t for t in enc if t not in cut]
                print("    every OTHER token in the corpus containing the term (%d) - the `variants`"
                      % len(rest))
                print("    cell is chosen from HERE, by keeping the ones that are job vocabulary:")
                for token in rest:
                    print("        %s" % token)
            print("\n  A `variants` cell is `;`-separated and REPLACES the derived list rather than "
                  "adding\n  to it, so a form you do not list gets no rule - which is exactly how "
                  "the bare short\n  form above stops being emitted. List the forms that should be "
                  "rewritten, and nothing else.")
        elif by_row:
            print("\n  Run again with --explain-cuts, at a terminal you are watching, to see the "
                  "actual\n  tokens behind those findings and choose the `variants` cells from them.")

        print("\nNOTHING WAS WRITTEN. Do not raise --min-global-length to silence a refusal: the "
              "floor is what keeps a short ordinary word out of eighteen hundred files. Do not "
              "delete a term row to silence a collision: change the REPLACEMENT, and grep the new "
              "one back against every job folder before you keep it. Choosing the vocabulary is "
              "part of the check, not something that happens after it.")
        return 1

    # ---- Emit ----------------------------------------------------------------------------------
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    rules.sort(key=lambda r: (-len(r[0]), r[0]))          # longest-first

    digest = hashlib.sha256()
    for path in sorted(__import__("glob").glob(os.path.join(repo, args.maps, "*.map.json"))):
        digest.update(io.open(path, "rb").read())
    digest.update(io.open(os.path.join(repo, args.terms), "rb").read())

    def rule_line(variant, replacement, key):
        """*** A DECLARED TERM IS ANCHORLESS. AN INFERRED KEY IS WORD-BOUNDED. ***

        Measured on the first trial rewrite: four declared terms HAD an emitted `\\b…\\b` rule and
        survived it, because they occur only inside a larger token and `\\b` refuses to match there.
        That is A8 in mirror image - A8 was the rules being too narrow for the lower-hyphen path
        forms, this is them being too narrow for a declared term inside a word.

        Inferred map keys keep their boundaries deliberately: an unanchored rule for an ordinary
        inferred name rewrites it inside unrelated words, which is exactly the collision the length
        floor and the Green test exist to prevent. The asymmetry is the point - a declaration is not
        a candidate."""
        if key in klass_of:
            # *** AND CASE-INSENSITIVE. ***
            # filter-repo compiles these with Python `re`, which is case-SENSITIVE, while the
            # verifier searches case-insensitively. So a declared term written in the corpus in a
            # different case than the term list records is hunted by the gate and missed by the
            # rule - the two tools disagree, and the gate is right.
            # Measured: the last surviving declared term had a correct anchorless rule, occurred 33
            # times, and every occurrence was in a case the rule could not match. `variants_for`
            # returns dotted keys verbatim with no case variants at all, so nothing else covered it.
            # A declared term is a declaration about a NAME, not about a spelling of it.
            return "regex:(?i)%s==>%s\n" % (re.escape(variant), replacement)
        return "regex:\\b%s\\b==>%s\n" % (re.escape(variant), replacement)

    text_path = os.path.join(out_dir, "replace-text.txt")
    msg_path = os.path.join(out_dir, "replace-message.txt")
    body = "".join(rule_line(v, r, k) for v, r, k in rules)
    io.open(text_path, "w", encoding="utf-8", newline="\n").write(body)
    io.open(msg_path, "w", encoding="utf-8", newline="\n").write(body)

    rename_path = os.path.join(out_dir, "path-renames.args")
    with io.open(rename_path, "w", encoding="utf-8", newline="\n") as fh:
        for old in sorted(dir_renames, key=lambda s: (-len(s), s)):
            fh.write("--path-rename\n%s:%s\n" % (old, dir_renames[old]))

    manifest = {
        "inputDigest": digest.hexdigest(),
        "pathRenames": len(dir_renames),
        "pathsChanged": len(renames),
        "renameTargetCollisions": len(collisions),
        "sentinel": sentinel,
        "scan": args.scan,
        "blobsScanned": searched,
        "unsearchableBlobs": unsearchable,
        "ruleCount": len(rules),
        "replacements": sorted(replacements),
        "greenCorpora": green,
        "minGlobalLength": args.min_global_length,
        "rung4Keys": len(rung4),
    }
    with io.open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(manifest, indent=2, sort_keys=True))

    print("\nEMITTED: %d rules -> %s" % (len(rules), out_rel))
    print("  replace-text.txt, replace-message.txt, path-renames.args (%d pairs), manifest.json"
          % len(dir_renames))
    print("\nThese files are GENERATED. Hand-editing them is silently undone by the next run.")
    print("This tool has emitted rules. IT HAS PROVED NOTHING about the result - run "
          "tools/verify-scrub.py against the rewritten clone, and read its positive control.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
