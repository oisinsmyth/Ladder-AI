"""Prove a doc cut dropped nothing: every marker the OLD file carried is still findable.

Generalised from check-claude-md-migration.py, which was written for the 2026-08-21
CLAUDE.md context cut and hard-coded that file, its destinations and its baseline. The
same question recurs every time content is moved out of a file that was too big: did
anything fall on the floor? This answers it for any (baseline, file, destinations).

    python tools/check-doc-migration.py <baseline-ref> <file> <dest> [<dest> ...]
                                        [--fenced-section "## Heading"]
                                        [--allow <fragments-file>]

Exit 0 = nothing dropped. Exit 1 = at least one marker is unaccounted for. Exit 2 = the
gate could not run (bad ref, unreadable file, named section absent). EXIT 2 IS NOT A PASS
- the same 1-vs-2 split the mechanical floor uses, for the same reason.

WHAT COUNTS AS A MARKER. Backticked identifiers 4-60 chars, plus long --flags. Prose is
deliberately NOT checked: on a cut whose whole purpose is removing narration, holding the
narration as a requirement would fail the gate by design. This does mean a fact stated
only in prose is outside the gate's reach - say so when you use it, rather than implying
coverage it does not have.

--fenced-section limits extraction to the first fenced block under a named heading, for
the case where only one section of the old file was migrated. Without it, the whole
baseline file is scanned.

--allow names a file of audited false positives, one per line, `#` for comments. These
are regex artefacts - markdown and punctuation runs that were never facts. DO NOT ADD TO
THAT FILE TO SILENCE A REAL DROP. Migrate the fact instead, then confirm it greps positive
in its new home. An allowlist that grows every time the gate complains is a gate that has
been turned off slowly.

Whitespace is normalised on both sides because destination docs wrap, so a migrated phrase
is routinely split across lines.
"""
import io, os, re, subprocess, sys

argv = sys.argv[1:]


def take_option(name):
    if name in argv:
        i = argv.index(name)
        if i + 1 >= len(argv):
            sys.stderr.write("%s needs a value\n" % name)
            raise SystemExit(2)
        value = argv[i + 1]
        del argv[i:i + 2]
        return value
    return None


SECTION = take_option("--fenced-section")
ALLOW_FILE = take_option("--allow")

if len(argv) < 3:
    sys.stderr.write(__doc__.split("\n\n")[2] + "\n")
    raise SystemExit(2)

BASELINE, TARGET, DESTINATIONS = argv[0], argv[1], argv[2:]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)


def read(path):
    with io.open(path, encoding="utf-8") as handle:
        return handle.read()


def normalise(text):
    return re.sub(r"\s+", " ", text.lower()).strip()


try:
    old = subprocess.check_output(["git", "show", "%s:%s" % (BASELINE, TARGET)]).decode("utf-8")
except (subprocess.CalledProcessError, OSError):
    sys.stderr.write("cannot read %s at %s - NOTHING CHECKED\n" % (TARGET, BASELINE))
    raise SystemExit(2)

if SECTION:
    lines = old.split("\n")
    try:
        start = next(i for i, l in enumerate(lines) if l.startswith(SECTION))
    except StopIteration:
        sys.stderr.write("no '%s' section at %s - wrong baseline? NOTHING CHECKED\n"
                         % (SECTION, BASELINE))
        raise SystemExit(2)
    fences = [i for i, l in enumerate(lines[start:], start) if l.strip() == "```"]
    if len(fences) < 2:
        sys.stderr.write("no fenced block under '%s' at %s - NOTHING CHECKED\n"
                         % (SECTION, BASELINE))
        raise SystemExit(2)
    source = "\n".join(lines[fences[0] + 1: fences[1]])
    scope = "fenced block under %s" % SECTION
else:
    source = old
    scope = "whole file"

allowed_fragments = set()
if ALLOW_FILE:
    try:
        for line in read(ALLOW_FILE).split("\n"):
            line = line.strip()
            if line and not line.startswith("#"):
                allowed_fragments.add(line)
    except (IOError, OSError):
        sys.stderr.write("cannot read allow file %s - NOTHING CHECKED\n" % ALLOW_FILE)
        raise SystemExit(2)

markers = set(re.findall(r"`([^`\n]{4,60})`", source))
markers |= set(re.findall(r"(--[a-z][a-z0-9-]{3,30})", source))
markers = {m.strip() for m in markers if len(m.strip()) >= 4}

missing_dest = [p for p in DESTINATIONS if not os.path.exists(p)]
if missing_dest:
    for p in missing_dest:
        sys.stderr.write("destination does not exist: %s\n" % p)
    sys.stderr.write("NOTHING CHECKED\n")
    raise SystemExit(2)

haystack = normalise("\n".join(read(p) for p in DESTINATIONS))
unfound = [m for m in markers if normalise(m) not in haystack]
missing = sorted(m for m in unfound if m not in allowed_fragments)
allowed = sorted(m for m in unfound if m in allowed_fragments)

out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
out.write("baseline                       : %s\n" % BASELINE)
out.write("file                           : %s\n" % TARGET)
out.write("scanned                        : %s\n" % scope)
out.write("destinations                   : %d\n" % len(DESTINATIONS))
out.write("markers in that scope          : %d\n" % len(markers))
out.write("still findable                 : %d\n" % (len(markers) - len(unfound)))
out.write("known fragments (not facts)    : %d\n" % len(allowed))
out.write("UNACCOUNTED FOR                : %d\n" % len(missing))
if missing:
    out.write("\n--- migrate each of these, or justify and add to the allow file ---\n")
    for m in missing:
        out.write("  %s\n" % m)
else:
    out.write("\nGATE PASSED: nothing was dropped.\n")
out.flush()

raise SystemExit(1 if missing else 0)
