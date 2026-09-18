#!/usr/bin/env python3
"""Does a rewritten clone still contain restricted identifiers? The Gate 3 oracle.

WHY THIS EXISTS. `build-scrub-rules.py` ends every run by saying, deliberately, "IT HAS PROVED
NOTHING about the result." It emits rules; it does not rewrite and it does not judge. Publication is
a ONE-WAY DOOR - every other gate in this project guards a reversible act and this one does not - so
the judging is a separate job, and this is it.

*** IT SHARES NO DERIVATION CODE WITH THE BUILDER, ON PURPOSE. ***
If the emitter and the verifier worked out their vocabulary the same way, a bug in that derivation
would produce a rule that misses X and then hunt for X the same wrong way and find nothing: a
confident, earned-looking zero over the wrong population. So this reads the same RAW inputs and
derives its own searches, deliberately WIDER than the rules - case-insensitive, no length floor, no
Green-corpus exclusion, and substring-capable inside a token. Gate coverage is a strict superset of
rule coverage, so a rule the builder failed to emit shows up here as a residual rather than as
silence. It reads the builder's manifest ONLY for the input digest, the sentinel and the replacement
vocabulary - NEVER for search terms.

WHAT IT WALKS. The object database, not the working tree. The tip is not the artifact: a residual in
a commit from July is still published. `git cat-file --batch-all-objects --batch` with NO STDIN
yields every blob, commit and tree in one pass, including unreachable objects, and is a strict
superset of reachable + unreachable (measured here: 14,336 + 227 = 14,563 exactly). Paths come from
parsing every TREE, not from `git rev-list --objects --all`, which misses 8 real path names in this
repository because unreachable trees are never traversed and a tree shared by two directories is
printed once. Commit identity comes from RAW OBJECT BYTES, never `%an/%ae`, because a mailmap
rewrites exactly the address being hunted.

HOW IT IS FAST. Not by being parallel - by not rescanning. One pass lowercases and tokenises 243 MB
into 60,262 distinct `[a-z0-9_]+` runs, which is 1.0 MB of text. Whole-token hits are set membership;
embedded hits are a substring scan over that 1.0 MB, not over the corpus - 1.8 GB of scanning instead
of 413 GB. The whole gate runs in about eleven seconds single-threaded. Multiprocessing was designed
and then dropped on this measurement: under Windows `spawn` a worker exception can vanish or hang the
pool, and a gate that dies quietly is a false green. Eight seconds is not worth that.

WHAT IT CANNOT SEE, stated because a limit nobody prints is a limit nobody knows about:
  - an identifier split across a line break, or broken by markup or punctuation;
  - anything inside a blob that is not valid UTF-8 (15 of them here, all rendered images);
  - an identifier nobody supplied. THIS GATE PROVES CLOSURE OVER A VOCABULARY. It does not and
    cannot prove the absence of identifiers, and no number of green runs changes that.

EXIT CODES, per this project's standing contract:
    0  an EARNED zero - residuals zero AND the positive control intact AND every denominator non-zero
    1  a residual, or a damaged invariant
    2  NOTHING WAS EXAMINED - any zero denominator, an absent or stale canary, a missing corpus,
       an unacknowledged unsearchable blob. EMPTY IS NOT CLEAN: exit 2 is never a pass.
    3  REFUSED BEFORE READING ANYTHING - the clone is not a directory, or has no object database.
"""
import argparse
import collections
import glob
import hashlib
import io
import json
import os
import re
import subprocess
import sys

# *** THE CHARACTER CLASS MUST COVER EVERY SEPARATOR A NEEDLE CAN CONTAIN. ***
# The builder tokenises on [a-z0-9_]+ because it searches for word-bounded names. This tool cannot:
# most of its needles are DOTTED TAG PATHS, and splitting on the dot means `owner.member` becomes
# two tokens and the needle can never be found in the vocabulary built from them.
# Measured when this was [a-z0-9_]+: 214 of 1719 needles could match themselves in the instrument
# control - 1,505 were unsearchable, silently, while the tool reported a plausible-looking residual
# count over the ~200 that happened to be single words. A dot, hyphen, slash and at-sign are part of
# an identifier here, so they are part of a token here.
WORD = re.compile(rb"[a-z0-9_.@/-]+")
NAMEY_SECTIONS = ("names", "tags", "company", "modelline", "identifiers")
REMOVED_DEFAULT = "***REMOVED***"

# Text carriers that live outside the object database. No git plumbing reaches them, so a gate that
# only walks the ODB never looks. Measured on the source repo: packed-refs carries a branch name with
# a job code in it, and the reflog carries 16 identifier-bearing commit subjects and 194 branch names.
# A fresh clone should have none of this - which is exactly why it is asserted rather than assumed.
SIDECAR_FILES = ("config", "description", "info/exclude", "packed-refs", "COMMIT_EDITMSG")


def run(repo, *args, **kw):
    """git, captured as text. Never raises on non-zero - callers decide what a failure means."""
    return subprocess.run(["git"] + list(args), cwd=repo, capture_output=True,
                          text=True, errors="replace", **kw).stdout


def derive_needles(maps_dir, terms_path, green_tokens=frozenset()):
    """The search vocabulary and its TIER, derived independently of the builder.

    No length floor and no Green exclusion are applied to the SEARCH - both are filters the builder
    applies when deciding what to REWRITE, and this tool's job is to find what those filters let
    through. They are applied to the TIER instead, which decides what gates.

    *** WHY TIERS AT ALL. *** Measured against the first trial rewrite: a flat "any hit fails" rule
    produced 394 residuals, of which 317 were identity mappings the maps explicitly declare as
    keep-this-name, 20 were the builder's documented Green/short decisions, and 59 of the remaining
    61 were the SAME identifier matched inside a longer one. A gate that cries wolf 59 times out of
    61 gets learned-ignored, which is its own kind of false green. Tiering is not a softening; T3
    gates on a DECREASE, which is a check a flat rule cannot express at all.

    T1 DECLARED - the owner wrote it down. Gates on presence anywhere, embedded or not.
    T2 INFERRED - a distinctive map key. Gates on WHOLE-TOKEN presence only.
    T3 ORDINARY - identity mapping, Green-present, or short. Never gates on presence; gates on a
                  FALL in its count, which is the signature of a bare-word rule eating the corpus.
    """
    needles, sources, tier = set(), {}, {}
    for path in sorted(glob.glob(os.path.join(maps_dir, "*.map.json"))):
        try:
            doc = json.loads(io.open(path, "rb").read().decode("utf-8-sig"))
        except (ValueError, OSError):
            continue                                        # counted by the caller via map_count
        for section, body in doc.items():
            if section.lower() not in NAMEY_SECTIONS or not isinstance(body, dict):
                continue
            for key, value in body.items():
                if not isinstance(key, str) or not key.strip():
                    continue
                # AN IDENTITY MAPPING IS STILL AN IDENTIFIER.
                # The builder drops the 326 keys where key == value, correctly: there is nothing
                # to rewrite. But "nothing to rewrite" is not "nothing to leak" - if one of them is
                # a site's own name that somebody mapped to itself, the builder cannot see it
                # and this tool can. This divergence falls straight out of the independence rule and
                # is one of the few places where the verifier's coverage genuinely exceeds the
                # rules' rather than merely restating them.
                low = key.lower()
                needles.add(low)
                sources.setdefault(low, "map")
                identity = isinstance(value, str) and value == key
                if identity or len(low) < 8 or low.encode() in green_tokens:
                    tier[low] = "T3"
                else:
                    tier.setdefault(low, "T2")
    if os.path.isfile(terms_path):
        for line in io.open(terms_path, encoding="utf-8"):
            line = line.strip()
            if not line.startswith("|") or line.startswith("|--"):
                continue
            cells = [c.strip() for c in line.strip("|").split("|")]
            if len(cells) < 2 or cells[0].lower() in ("live", "term", "source"):
                continue
            if cells[0]:
                low = cells[0].lower()
                needles.add(low)
                sources[low] = "term"
                tier[low] = "T1"                            # a declaration outranks any inference
    return needles, sources, tier


def walk_odb(repo):
    """(kind, name, payload_bytes) for every object in the database, reachable or not.

    ONE pass, NO STDIN. Feeding object names in on stdin is the shape that deadlocks: a git blocked
    reading a stdin nobody closed looks exactly like a slow repository. `--batch-all-objects` needs
    no stdin at all and is a strict superset of reachable + unreachable."""
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
                # "<oid> missing" for an unreadable object. Reported, never skipped silently: a
                # parser that breaks here abandons the rest of history while still reporting a
                # non-zero count, which is how a partial scan passes for a complete one.
                yield "missing", (parts[0].decode("ascii", "replace") if parts else "?"), b""
                continue
            name, kind, size = parts[0].decode("ascii"), parts[1].decode("ascii"), int(parts[2])
            body = stream.read(size)
            stream.read(1)
            yield kind, name, body
    finally:
        stream.close()
        proc.wait()


def tree_names(payload):
    """Entry names from a raw tree object: <mode> <name>\\0<20 raw bytes>, repeated."""
    out, i, n = [], 0, len(payload)
    while i < n:
        sp = payload.find(b" ", i)
        if sp < 0:
            break
        nul = payload.find(b"\x00", sp)
        if nul < 0:
            break
        out.append(payload[sp + 1:nul])
        i = nul + 21
    return out


def commit_identity_and_message(payload):
    """(identity_lines, message) from a raw commit object.

    Raw bytes on purpose: `git log --format=%an` applies .mailmap, which can rewrite precisely the
    address this gate exists to find."""
    head, _, message = payload.partition(b"\n\n")
    ident = [ln for ln in head.split(b"\n")
             if ln.startswith(b"author ") or ln.startswith(b"committer ")]
    return ident, message


def tokenise(chunks):
    """Lowercased [a-z0-9_]+ runs across an iterable of byte chunks, as one set."""
    vocab = set()
    for chunk in chunks:
        if chunk:
            vocab.update(WORD.findall(chunk.lower()))
    return vocab


def token_counts(texts):
    """Counter of lowercased whole tokens across the corpus. ONE pass.

    Raw `bytes.count` counts substrings, which for the over-scrub detector is the wrong measure:
    occurrences that were part of a LONGER identifier disappear legitimately when that longer
    identifier is rewritten, and counting them makes a working scrub look like damage.

    A per-needle regex with lookarounds gives the right answer and does not finish - 348 needles
    over 243 MB is 85 GB of scanning. Tokenising once and counting is the same answer for the price
    of the pass already being made."""
    counter = collections.Counter()
    for chunk in texts:
        if chunk:
            counter.update(WORD.findall(chunk.lower()))
    return counter


def untokenisable(needles):
    """Needles that no token can ever contain - they hold a character outside the token class.

    A identifying name with a SPACE in it is the live case. Widening the token class to include spaces
    is not the fix: tokens would become whole lines and the vocabulary would stop being an index.
    These few are searched against the raw corpus instead, which is affordable precisely because
    there are a handful of them."""
    return sorted(n for n in needles if not WORD.fullmatch(n.encode()))


def residuals(vocab, needles):
    """(whole, embedded) - needles present as a complete token, and needles present only INSIDE one.

    The vocabulary IS the index. An identifier embedded in a longer word-run is a substring of some
    token, so the substring pass runs over the 1.0 MB token set rather than the 243 MB corpus - the
    difference between 1.8 GB of scanning and 413 GB. Joining on a newline means a needle can never
    match across two tokens, which would be a false positive.

    *** THE TWO ARE RETURNED SEPARATELY BECAUSE THEY MEAN DIFFERENT THINGS. ***
    A whole-token hit is the identifier. An embedded hit may be a DIFFERENT identifier that merely
    starts the same way - `Foo.Bar` inside `Foo.BarBaz` is a different tag, not a leak. Measured on
    the first trial rewrite, 59 of 61 inferred-key hits were embedded-only. For a DECLARED term the
    embedded hit still counts, because the owner named that string and meant it anywhere; for an
    inferred key it is a candidate, not a finding. Collapsing them into one set is what made the
    first verdict unusable."""
    whole = {n for n in needles if n.encode() in vocab}
    embedded = set()
    remaining = [n for n in needles if n not in whole]
    if remaining:
        joined = b"\n".join(sorted(vocab))
        for needle in remaining:
            if needle.encode() in joined:
                embedded.add(needle)
    return whole, embedded


def load_canary(path):
    if not os.path.isfile(path):
        return None
    try:
        return json.loads(io.open(path, "rb").read().decode("utf-8-sig"))
    except ValueError:
        return None


def input_digest(maps_dir, terms_path):
    digest = hashlib.sha256()
    for path in sorted(glob.glob(os.path.join(maps_dir, "*.map.json"))):
        digest.update(io.open(path, "rb").read())
    if os.path.isfile(terms_path):
        digest.update(io.open(terms_path, "rb").read())
    return digest.hexdigest()


def green_corpus_blob(repo, corpora):
    """The already-sanitized corpora, lowercased, as one blob. Used only for TIER assignment - a
    name clean content legitimately uses is conventional, so it reports rather than gates."""
    out = []
    for corpus in corpora:
        for rel in run(repo, "ls-files", corpus).split("\n"):
            if rel.strip():
                try:
                    out.append(io.open(os.path.join(repo, rel.strip()), "rb").read().lower())
                except OSError:
                    pass
    # A TOKEN SET, NOT A BLOB - and the difference produced a false finding on the first run.
    # "Is this a conventional name?" is a WHOLE-TOKEN question: a name that merely appears as a
    # substring of some longer name in clean content is not thereby conventional. Tested as a
    # substring, an 11-character map key was tiered T3 (conventional, report-only) while the builder
    # correctly rewrote it - and the over-scrub detector then reported its disappearance, 1,438
    # whole-token occurrences to 0, as damage. It was the scrub working. The verifier may be wider
    # than the builder about WHAT IT HUNTS; it must agree with it about WHAT COUNTS AS CONVENTIONAL,
    # or the two disagree about which population is which.
    return set(WORD.findall(b"\n".join(out)))


def make_canary(repo, maps_dir, terms_path, out_path, survive_texts, green):
    """Record the PRE-scrub state, so that afterwards a zero can be told apart from a nothing.

    M-21: a canary at zero means either the scrub over-matched or the string was never there, and
    those look identical without a before-count. M-20: the counts describe a subject that versions
    independently of this file, so the subject version is stamped in and a stale stamp reports
    UNVERIFIED rather than passing."""
    counts = {}
    texts = []
    for kind, _, payload in walk_odb(repo):
        if kind in ("blob", "commit"):
            texts.append(payload)
    blob = b"\n".join(texts).lower()
    for text in survive_texts:
        counts[text] = blob.count(text.lower().encode())
    needles, _, tier = derive_needles(maps_dir, terms_path, green_corpus_blob(repo, green))
    vocab = tokenise(texts)
    whole, embedded = residuals(vocab, needles)

    # T3 BASELINE COUNTS - the over-scrub detector's denominator.
    # T3 is the conventional vocabulary: identity mappings, Green-present names, short names. Its
    # PRESENCE is expected and never gates. Its DISAPPEARANCE is the signature of a bare-word rule
    # eating the already-clean corpus (risk A5), and without a before-count that is invisible.
    tc = token_counts(texts)
    t3 = {n: tc.get(n.encode(), 0) for n in needles if tier.get(n) == "T3"}

    doc = {
        "subjectVersion": run(repo, "rev-parse", "HEAD").strip(),
        "inputDigest": input_digest(maps_dir, terms_path),
        "mustSurvive": [{"text": t, "expected": counts[t]} for t in survive_texts],
        "mustVanishWhole": len(whole),
        "mustVanishEmbedded": len(embedded),
        "tier3Baseline": t3,
        "mustNotAppear": ["QQSCRUBQQ", REMOVED_DEFAULT],
    }
    io.open(out_path, "w", encoding="utf-8", newline="\n").write(
        json.dumps(doc, indent=2, sort_keys=True))
    return doc


def load_build_capture(path):
    """Read one build capture. Returns (by_assembly, doc); raises ValueError with a sayable reason.

    A capture is keyed BY ASSEMBLY and never by its total. A total-only comparison cannot tell
    "1811 here and 202 there" from "202 here and 1811 there", so a rename that moves a whole
    project's tests from one assembly to another nets to zero and passes - which is precisely the
    accident this comparison exists to catch.

    Every malformed input raises rather than returning something empty. A capture that failed to
    parse and a capture of a repository with no tests are indistinguishable once both are {}, and
    only one of them is a finding.
    """
    try:
        doc = json.loads(io.open(path, encoding="utf-8-sig").read())
    except Exception as exc:
        raise ValueError("--build capture '%s' is not readable JSON: %s" % (path, exc))
    rows = doc.get("assemblies")
    if not isinstance(rows, list):
        raise ValueError("--build capture '%s' carries no 'assemblies' list." % path)
    by = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("--build capture '%s' has a row that is not an object." % path)
        name = row.get("assembly")
        if not name:
            raise ValueError("--build capture '%s' has a row with no 'assembly' name." % path)
        if name in by:
            raise ValueError("--build capture '%s' lists assembly '%s' twice - one of those two "
                             "rows is wrong and this tool cannot tell which." % (path, name))
        try:
            by[name] = (int(row["passed"]), int(row["failed"]))
        except (KeyError, TypeError, ValueError):
            raise ValueError("--build capture '%s' row '%s' has no integer passed/failed count."
                             % (path, name))
    return by, doc


def compare_builds(base, cur, base_doc, cur_doc):
    """Compare two captures. Returns (printable lines, findings).

    WHAT GATES is asymmetric on purpose. A test that stopped passing is a scrub that broke
    something; a test that started passing is not evidence of anything and must not be able to
    cancel out a loss. So a DECREASE in passes, a RISE in failures, a whole assembly going
    missing, and a target that used to build and now does not all gate - while new assemblies and
    higher pass counts are reported and left alone.
    """
    lines, findings = [], []
    shared = sorted(set(base) & set(cur))
    vanished = sorted(set(base) - set(cur))
    appeared = sorted(set(cur) - set(base))

    fell = [(n, base[n][0], cur[n][0]) for n in shared if cur[n][0] < base[n][0]]
    broke = [(n, base[n][1], cur[n][1]) for n in shared if cur[n][1] > base[n][1]]
    rose = [n for n in shared if cur[n][0] > base[n][0]]

    base_pass = sum(p for p, _ in base.values())
    cur_pass = sum(p for p, _ in cur.values())
    lines.append("build comparison           : %d assembly/assemblies compared, %d -> %d passed"
                 % (len(shared), base_pass, cur_pass))
    if appeared:
        lines.append("  %d new assembly/assemblies not in the baseline (reported, does NOT gate): %s"
                     % (len(appeared), ", ".join(appeared)))
    if rose:
        lines.append("  %d assembly/assemblies gained passes (reported, does NOT gate)" % len(rose))

    if vanished:
        findings.append("%d assembly/assemblies in the baseline are ABSENT after the scrub (%s). A "
                        "test that no longer runs has not passed - it has stopped being asked."
                        % (len(vanished), ", ".join(vanished)))
    for name, before, after in fell:
        findings.append("%s dropped from %d passing to %d. The scrub was not supposed to change "
                        "behaviour." % (name, before, after))
    for name, before, after in broke:
        findings.append("%s went from %d failing to %d." % (name, before, after))

    # A target that built before and does not now is a break the per-assembly numbers cannot show:
    # it produces no assembly at all, so it leaves no row to compare and no count to fall.
    base_gated = set(r.get("target") for r in base_doc.get("cannotBuild", []) if isinstance(r, dict))
    cur_gated = set(r.get("target") for r in cur_doc.get("cannotBuild", []) if isinstance(r, dict))
    newly_gated = sorted(t for t in (cur_gated - base_gated) if t)
    if base_gated or cur_gated:
        lines.append("  targets that cannot build  : %d before, %d after"
                     % (len(base_gated), len(cur_gated)))
    if newly_gated:
        findings.append("%d target(s) built before the scrub and do not build after it: %s"
                        % (len(newly_gated), ", ".join(newly_gated)))
    return lines, findings


def main():
    ap = argparse.ArgumentParser(description="Gate 3 oracle: is this rewritten clone clean?")
    ap.add_argument("--clone", default=".", help="the rewritten clone to judge")
    ap.add_argument("--maps", default="sanitization", help="directory of *.map.json")
    ap.add_argument("--terms", default="sanitization/scrub-terms.md")
    ap.add_argument("--manifest", default="sanitization/scrub/manifest.json")
    ap.add_argument("--canary", default="sanitization/canary.json",
                    help="pre-scrub counts. NOT under sanitization/scrub/, which the builder "
                         "overwrites without saying so.")
    ap.add_argument("--green", action="append", default=None)
    ap.add_argument("--green-baseline", default=None,
                    help="sha256 manifest of the Green corpora taken before the scrub")
    ap.add_argument("--build-baseline", default=None,
                    help="per-assembly test pass counts from BEFORE the scrub, from "
                         "tools/capture-build-baseline.py. OPTIONAL, and its absence is stated "
                         "loudly rather than assumed away. On its own it proves nothing: pass "
                         "--build-current too.")
    ap.add_argument("--build-current", default=None,
                    help="the same capture taken from the REWRITTEN clone. Compared per assembly "
                         "against --build-baseline.")
    ap.add_argument("--make-canary", action="store_true",
                    help="record the PRE-scrub state instead of judging. Run this on the source "
                         "repository before the rewrite.")
    ap.add_argument("--survive", action="append", default=None,
                    help="a MUST-SURVIVE control string; repeatable")
    ap.add_argument("--fast", action="store_true",
                    help="working tree only. NEVER a Gate 3 answer; says so on every run.")
    ap.add_argument("--name-matches", action="store_true",
                    help="print the matched strings. OFF BY DEFAULT: this output gets pasted into "
                         "notes, and a record of a leak must not be a copy of it.")
    args = ap.parse_args()

    repo = os.path.abspath(args.clone)
    if not os.path.isdir(repo) or not run(repo, "rev-parse", "--git-dir").strip():
        print("REFUSED: '%s' is not a git repository." % args.clone, file=sys.stderr)
        return 3

    maps_dir = args.maps if os.path.isabs(args.maps) else os.path.join(os.getcwd(), args.maps)
    terms_path = args.terms if os.path.isabs(args.terms) else os.path.join(os.getcwd(), args.terms)
    survive = args.survive or ["C-001", "NOTHING EXAMINED", "EMPTY IS NOT CLEAN",
                               "converter preflight", "docs/06-lad-conventions.md"]

    green = args.green or ["ir/reference", "ir/test-project001", "gen/test-project001",
                           "simatic-ml/reference", "simatic-ml/test-project001"]

    if args.make_canary:
        doc = make_canary(repo, maps_dir, terms_path, args.canary, survive, green)
        print("canary written : %s" % args.canary)
        print("  subject      : %s" % doc["subjectVersion"][:12])
        print("  must-survive : %d strings" % len(doc["mustSurvive"]))
        for entry in doc["mustSurvive"]:
            print("      %-32s expected %d" % (entry["text"], entry["expected"]))
        print("  must-vanish  : %d whole-token, %d embedded-only"
              % (doc["mustVanishWhole"], doc["mustVanishEmbedded"]))
        print("  T3 baseline  : %d conventional needles counted (the over-scrub denominator)"
              % len(doc["tier3Baseline"]))
        return 0

    needles, sources, tier = derive_needles(maps_dir, terms_path,
                                            green_corpus_blob(repo, green))
    findings, cannot = [], []

    # ---- preconditions ---------------------------------------------------------------------------
    if os.path.isfile(os.path.join(repo, ".git", "objects", "info", "alternates")):
        findings.append("this repository borrows objects from an ALTERNATE object store, so the "
                        "corpus scanned here is not the corpus that ships.")
    remotes = run(repo, "remote", "-v").strip()
    if run(repo, "rev-parse", "--is-shallow-repository").strip() == "true":
        cannot.append("the repository is SHALLOW - history is truncated, so a clean scan means "
                      "nothing about the commits that are missing.")
    # A PARTIAL CLONE IS THE WORST CASE HERE, because it does not look truncated. Blobs are fetched
    # on demand, so `--batch-all-objects` enumerates only what happens to be local and reports a
    # confident count over an unknown fraction of the artifact.
    cfg = run(repo, "config", "--list")
    if "extensions.partialclone" in cfg.lower() or "promisor=true" in cfg.lower().replace(" ", ""):
        cannot.append("this is a PARTIAL clone - objects are fetched on demand, so the object count "
                      "below is a fraction of the artifact and nothing here generalises to it.")

    # ---- the one pass ----------------------------------------------------------------------------
    blobs = commits = trees = unsearchable = missing = 0
    texts, path_names, ident_lines = [], set(), []
    if args.fast:
        for rel in run(repo, "ls-files").split("\n"):
            if rel.strip():
                path_names.add(rel.strip())
                try:
                    texts.append(io.open(os.path.join(repo, rel.strip()), "rb").read())
                    blobs += 1
                except OSError:
                    unsearchable += 1
    else:
        for kind, name, payload in walk_odb(repo):
            if kind == "blob":
                blobs += 1
                try:
                    payload.decode("utf-8")
                except UnicodeDecodeError:
                    unsearchable += 1                        # counted, never silently excluded
                    continue
                texts.append(payload)
            elif kind == "commit":
                commits += 1
                ident, message = commit_identity_and_message(payload)
                ident_lines.extend(ident)
                texts.append(message)
            elif kind == "tree":
                trees += 1
                for entry in tree_names(payload):
                    path_names.add(entry.decode("utf-8", "replace"))
            elif kind == "missing":
                missing += 1

    # ---- refs, and the carriers no plumbing reaches -----------------------------------------------
    ref_text = run(repo, "for-each-ref", "--format=%(refname) %(contents:subject)")
    ref_text += "\n" + run(repo, "rev-parse", "--symbolic-full-name", "HEAD")
    reflog_text = run(repo, "reflog", "--all", "--format=%gd %gs")
    sidecar_text = ""
    for rel in SIDECAR_FILES:
        full = os.path.join(repo, ".git", rel.replace("/", os.sep))
        if os.path.isfile(full):
            try:
                sidecar_text += "\n" + io.open(full, encoding="utf-8", errors="replace").read()
            except OSError:
                pass

    # ---- THE INSTRUMENT CONTROL - the check without which this tool is unfalsifiable ---------------
    # Every other check in here reports a COUNT OF ZERO as good news. So the one failure this design
    # cannot tolerate is a matcher that can never match anything: a regex-escaping slip, a bytes/str
    # confusion, a lowercasing asymmetry, and every surface returns zero and the gate exits 0 with a
    # perfect, earned-looking green.
    #
    # THE CANARY DOES NOT CATCH THIS. The must-survive counts are taken with `bytes.count`, a
    # different code path from `residuals()`, so a broken `residuals()` leaves them intact and the
    # positive control still reads healthy. This control is therefore not redundant with it.
    #
    # A synthetic object carrying every needle is pushed through THE SAME `tokenise` and `residuals`
    # the real corpus goes through. Every needle must find itself. Anything less is exit 2.
    loose = untokenisable(needles)
    control_payload = ("\n".join(sorted(needles)) + "\n").encode("utf-8")
    cw, ce = residuals(tokenise([control_payload]), needles)
    control_found = cw | ce | {n for n in loose if n.encode() in control_payload.lower()}
    control_missed = len(needles) - len(control_found)

    # ---- the verdict -------------------------------------------------------------------------------
    vocab = tokenise(texts)
    aux = tokenise([("\n".join(path_names)).encode("utf-8", "replace"),
                    ref_text.encode("utf-8", "replace"),
                    reflog_text.encode("utf-8", "replace"),
                    sidecar_text.encode("utf-8", "replace"),
                    b"\n".join(ident_lines)])

    content_whole, content_emb = residuals(vocab, needles)
    aux_whole, aux_emb = residuals(aux, needles)
    content_hits, aux_hits = content_whole | content_emb, aux_whole | aux_emb
    if loose:
        # The handful no token can hold, searched against the raw bytes.
        raw_all = b"\n".join(texts).lower()
        raw_aux = ("\n".join(path_names) + ref_text + reflog_text + sidecar_text).lower().encode(
            "utf-8", "replace") + b"\n" + b"\n".join(ident_lines).lower()
        for n in loose:
            if n.encode() in raw_all:
                content_hits.add(n)
            if n.encode() in raw_aux:
                aux_hits.add(n)

    print("clone                      : %s" % repo)
    print("scan mode                  : %s" % ("WORKING TREE ONLY" if args.fast else "object database"))
    if args.fast:
        print("  *** THIS DID NOT EXAMINE HISTORY. It is not a Gate 3 answer and must never be "
              "quoted as one. ***")
    print("objects                    : %d blobs, %d commits, %d trees" % (blobs, commits, trees))
    print("  unsearchable blobs       : %d  (not valid UTF-8 - NEVER counted clean)" % unsearchable)
    print("  unreadable objects       : %d" % missing)
    print("distinct paths             : %d" % len(path_names))
    print("search vocabulary          : %d needles (%d from maps, %d declared)"
          % (len(needles),
             sum(1 for n in needles if sources.get(n) == "map"),
             sum(1 for n in needles if sources.get(n) == "term")))
    print("corpus tokens              : %d" % len(vocab))
    print("instrument control         : %d of %d needles matched themselves%s"
          % (len(control_found), len(needles),
             "" if not control_missed else "   *** %d CANNOT MATCH ***" % control_missed))
    # ---- tier the hits. THIS is what decides the verdict. ------------------------------------------
    def of(tiername, *sets):
        out = set()
        for s in sets:
            out |= {n for n in s if tier.get(n, "T2") == tiername}
        return out

    t1_hit = of("T1", content_whole, content_emb, aux_whole, aux_emb)
    t2_whole = of("T2", content_whole, aux_whole)
    t2_emb = of("T2", content_emb, aux_emb)
    t3_hit = of("T3", content_whole, content_emb, aux_whole, aux_emb)

    print()
    print("T1 DECLARED   present anywhere : %-5d  GATES  (the owner named these)" % len(t1_hit))
    print("T2 INFERRED   whole-token      : %-5d  GATES" % len(t2_whole))
    print("T2 INFERRED   embedded only    : %-5d  reported, does NOT gate - an identifier inside a "
          "longer one is usually a DIFFERENT identifier" % len(t2_emb))
    print("T3 ORDINARY   present          : %-5d  reported, does NOT gate - conventional names are "
          "EXPECTED here; a FALL in their count is the finding" % len(t3_hit))

    if args.name_matches:
        for n in sorted(content_hits | aux_hits):
            print("      %s" % n)
    elif content_hits or aux_hits:
        print("      (the matched strings are NOT printed - this output gets pasted into notes, "
              "and a record of a leak must not be a copy of it. Use --name-matches at a terminal.)")

    # ---- denominators: empty is not clean ----------------------------------------------------------
    if control_missed:
        cannot.append("%d of %d needles COULD NOT MATCH THEMSELVES in a synthetic control object. "
                      "The matcher is broken, so every zero this run reported is meaningless - "
                      "including the ones that look like good news." % (control_missed, len(needles)))
    if not needles:
        cannot.append("the search vocabulary is EMPTY - no maps and no term rows were read.")
    if blobs == 0:
        cannot.append("zero blobs were examined.")
    if not args.fast and commits == 0:
        cannot.append("zero commits were examined.")
    if not vocab:
        cannot.append("the corpus tokenised to nothing.")
    if missing:
        cannot.append("%d object(s) could not be read, so part of the corpus was never searched."
                      % missing)

    # ---- the canary --------------------------------------------------------------------------------
    canary = load_canary(args.canary)
    if canary is None:
        cannot.append("no canary at '%s'. Without pre-scrub counts a zero cannot be told apart "
                      "from a search that examined nothing - run --make-canary on the SOURCE "
                      "repository before the rewrite." % args.canary)
    else:
        blob_all = b"\n".join(texts).lower()
        survived = 0
        for entry in canary.get("mustSurvive", []):
            seen = blob_all.count(entry["text"].lower().encode())
            expected = entry["expected"]
            if seen:
                survived += 1
            # A COUNT, NOT A PRESENCE TEST (M-21). The first version printed "ok" beside
            # `2668 seen / 2703 expected`, which is the wrong word for an unchecked delta: a
            # control that only notices total disappearance cannot see a rule that ate 90% of it.
            # A fall is expected here - blobs were rewritten - so the bar is a documented
            # tolerance, not equality, and anything past it is a finding rather than a shrug.
            drop = (expected - seen) / float(expected) if expected else 0.0
            state = "ok" if seen and drop <= 0.10 else ("GONE" if not seen else "FELL %.0f%%" % (drop * 100))
            if seen and drop > 0.10:
                findings.append("must-survive control '%s' fell %.0f%% (%d -> %d). Something "
                                "removed content that was not supposed to be touched."
                                % (entry["text"], drop * 100, expected, seen))
            print("  must-survive %-28s %5d seen / %5d expected  %s"
                  % (entry["text"], seen, expected, state))
        if canary.get("mustSurvive") and survived == 0:
            cannot.append("EVERY must-survive control is absent. Either this run examined nothing, "
                          "or it was pointed at the wrong tree, or the scrub over-applied. A zero "
                          "residual count alongside a dead positive control is not a pass.")
        digest_now = input_digest(maps_dir, terms_path)
        if canary.get("inputDigest") and canary["inputDigest"] != digest_now:
            cannot.append("the canary was recorded against different maps/terms than are on disk "
                          "now, so its counts describe a different vocabulary (M-20: a stale "
                          "declaration is UNVERIFIED, not a pass).")
        for text in canary.get("mustNotAppear", []):
            n = blob_all.count(text.lower().encode())
            if n:
                findings.append("'%s' appears %d time(s) - it must never appear in a finished "
                                "artifact." % (text, n))

        # ---- THE OVER-SCRUB DETECTOR -------------------------------------------------------------
        # T3 is the conventional vocabulary. Its presence never gates - that is the whole point of
        # the tier. What gates is a FALL: a bare-word rule that ate the already-sanitized corpus
        # shows up here as a conventional name dropping from 812 occurrences to 606, and nothing
        # else in this tool would notice. Presence is expected; disappearance is the finding.
        baseline = canary.get("tier3Baseline", {})
        if baseline:
            fell = []
            counts_now = token_counts(texts)
            for needle, before in baseline.items():
                if before <= 0:
                    continue
                after = counts_now.get(needle.encode(), 0)
                if after < before * 0.5:
                    fell.append((needle, before, after))
            print("  T3 over-scrub check        : %d conventional needles compared, %d fell by "
                  "more than half" % (len(baseline), len(fell)))
            if fell:
                findings.append("%d conventional name(s) LOST more than half their occurrences. "
                                "That is the signature of a rule matching far more than it was "
                                "meant to - check the demo corpora before anything else."
                                % len(fell))
        else:
            cannot.append("the canary carries no T3 baseline, so OVER-SCRUB WAS NOT TESTED: a rule "
                          "could have eaten the already-clean corpus and this run would not know.")

    # ---- Green corpora byte-identity ----------------------------------------------------------------
    if args.green_baseline:
        if not os.path.isfile(args.green_baseline):
            cannot.append("--green-baseline '%s' does not exist." % args.green_baseline)
        else:
            expected = {}
            for line in io.open(args.green_baseline, encoding="utf-8"):
                if "  " in line:
                    h, p = line.rstrip("\n").split("  ", 1)
                    expected[p] = h
            changed = 0
            for p, h in expected.items():
                full = os.path.join(repo, p)
                if not os.path.isfile(full):
                    changed += 1
                    continue
                if hashlib.sha256(io.open(full, "rb").read()).hexdigest() != h:
                    changed += 1
            print("green corpus files         : %d compared, %d changed" % (len(expected), changed))
            if not expected:
                cannot.append("the green baseline listed zero files.")
            if changed:
                findings.append("%d file(s) in the already-sanitized Green corpora changed. The "
                                "scrub was not supposed to touch them." % changed)

    # ---- the build comparison -----------------------------------------------------------------------
    # A BASELINE ALONE PROVES NOTHING, and the version of this tool that accepted one on its own
    # said so anyway: it tested os.path.isfile, printed a tidy path line, dropped the warning
    # banner, and verified exactly as much as a run with no baseline at all. A gate that greens on
    # a file existing. Both halves, captured by the same script, or the banner stays up.
    if args.build_current and not args.build_baseline:
        cannot.append("--build-current was supplied with no --build-baseline. A capture with "
                      "nothing to compare it against is not a measurement.")
    elif not args.build_baseline:
        print("build baseline             : NOT SUPPLIED")
        print("  *** THIS RUN SAYS NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST. *** It is not a "
              "full Gate 3 pass, whatever the residual count says.")
    elif not os.path.isfile(args.build_baseline):
        # A typo in this path used to read exactly like a deliberate omission, which is the whole
        # family of bug this repo keeps finding: the failure that looks like the safe default.
        cannot.append("--build-baseline '%s' does not exist. A MISSING baseline is a refusal, not "
                      "a silent downgrade to 'not supplied'." % args.build_baseline)
    elif not args.build_current:
        print("build baseline             : %s" % args.build_baseline)
        print("build current              : NOT SUPPLIED")
        print("  *** THIS RUN SAYS NOTHING ABOUT WHETHER THE SCRUB BROKE A TEST. *** A baseline on "
              "its own is one half of a comparison. Capture the rewritten clone with "
              "tools/capture-build-baseline.py and pass it as --build-current.")
    elif not os.path.isfile(args.build_current):
        cannot.append("--build-current '%s' does not exist." % args.build_current)
    else:
        try:
            base_by, base_doc = load_build_capture(args.build_baseline)
            cur_by, cur_doc = load_build_capture(args.build_current)
        except ValueError as exc:
            cannot.append(str(exc))
        else:
            if not base_by or not cur_by:
                cannot.append("a build capture listed ZERO assemblies, so NO TEST WAS COMPARED. "
                              "An empty capture is not a clean one.")
            else:
                build_lines, build_findings = compare_builds(base_by, cur_by, base_doc, cur_doc)
                for line in build_lines:
                    print(line)
                findings.extend(build_findings)

    if t1_hit:
        findings.append("%d DECLARED term(s) survive the rewrite. A declared term is not a "
                        "candidate: the owner wrote it down, so it must not appear anywhere - "
                        "embedded inside a longer token included." % len(t1_hit))
    if t2_whole:
        findings.append("%d inferred identifier(s) survive as WHOLE TOKENS." % len(t2_whole))
    if unsearchable:
        print("  note: %d unsearchable blob(s) were NOT searched. They are part of the artifact "
              "and no text pass reaches them." % unsearchable)

    if cannot:
        print("\n--- NOTHING EXAMINED ---")
        for c in cannot:
            print("  " + c)
        print("\nEMPTY IS NOT CLEAN. Exit 2 is never a pass: this run did not establish that the "
              "clone is clean, and it did not establish that it is dirty either.")
        return 2

    if findings:
        print("\n--- GATE FAILED ---")
        for f in findings:
            print("  " + f)
        print("\nAdd the residual terms to the term list and re-run the builder. Do NOT narrow this "
              "gate to make it pass - it is deliberately wider than the rules, and that width is "
              "the only thing that catches a rule the builder failed to emit.")
        return 1

    print("\nEARNED ZERO: zero residuals over a vocabulary of %d needles, with the positive control "
          "intact, across %d blobs, %d commits and %d paths, with %d blobs unsearchable."
          % (len(needles), blobs, commits, len(path_names), unsearchable))
    print("This says the supplied vocabulary is closed over this artifact. IT DOES NOT SAY THE "
          "ARTIFACT CONTAINS NO IDENTIFIERS - no vocabulary-based check can.")
    print("\nWHAT THIS RUN COULD NOT SEE:")
    print("  - an identifier present in NEITHER the maps NOR the term list. The builder cannot "
          "emit a rule for it and this tool cannot hunt it; independence of CODE does not remove "
          "a shared dependence on INPUTS, and this is the deepest limit of the design.")
    print("  - %d binary blob(s): byte content not searched. NOT OCR'd - an identifier rendered as "
          "pixels is invisible here." % unsearchable)
    print("  - an identifier split across a line break, or broken by markup or punctuation.")
    print("  - an INFERENTIAL identification: a plant described precisely enough to be recognised "
          "without being named. That is not a string and no string gate sees it.")
    if remotes:
        print("\nNOTE: this clone HAS a remote configured. Check it before anything is pushed.")
    # The stamp. A verdict covers the artifact AS IT WAS, and the plan's own Phase 3 re-applies
    # hygiene and repairs 80 files of SHA citations AFTER the rewrite - so "verify, then commit,
    # then push" is the likeliest real-world false green available, and the step order invites it.
    print("\nVERIFIED subject=%s objects=%d needles=%d"
          % (run(repo, "rev-parse", "HEAD").strip()[:12], blobs + commits + trees, len(needles)))
    print("  This verdict covers THAT HEAD and THAT object count. Any commit made after this run "
          "is unverified. Re-run after the last commit you intend to publish, and match this stamp "
          "against `git rev-parse HEAD` in the repository you actually push.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
