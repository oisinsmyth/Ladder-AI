"""Gate for the 2026-08-21 CLAUDE.md context cut.

Every falsifiable marker that the PRE-CUT CLAUDE.md carried in its Commands block must
still be findable - either resident in the current CLAUDE.md, or in one of the docs the
content was migrated into. A marker found nowhere means the migration dropped it.

Run before committing any further trim of CLAUDE.md:

    python tools/check-claude-md-migration.py [baseline-commit]

Exit 0 = nothing dropped. Exit 1 = at least one marker is unaccounted for.

Markers are backticked identifiers and --flags only. ALLCAPS prose fragments are
deliberately NOT checked: the narration was what the cut set out to remove, so holding
it as a requirement would fail the gate by design. Known residual false positives are
markdown/punctuation fragments (e.g. `filename does not (`), not facts.

Whitespace is normalised on both sides because the destination docs wrap at ~100 chars,
so a migrated phrase is routinely split across lines.
"""
import io, os, re, subprocess, sys

BASELINE = sys.argv[1] if len(sys.argv) > 1 else "a1eca27"

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)

# CLAUDE.md itself first: content kept resident counts as "not dropped".
DESTINATIONS = [
    "CLAUDE.md",
    os.path.join("src", "converter", "README.md"),
    os.path.join("src", "openness-cli", "README.md"),
    os.path.join("docs", "notes", "openness-quirks.md"),
    os.path.join("docs", "notes", "live-project-readiness.md"),
    os.path.join("docs", "notes", "claude-agent-skill-authoring.md"),
    os.path.join("docs", "notes", "deferred-items.md"),
    os.path.join("docs", "15-generation-pipeline.md"),
    os.path.join("docs", "06-lad-conventions.md"),
]


# Markers whose CONTENT was migrated but whose exact string cannot match, because the
# regex caught a markdown/punctuation fragment rather than an identifier. Each was checked
# by hand on 2026-08-21. Do NOT add to this list to silence a real drop - migrate the fact
# instead, then confirm it greps positive in its new home.
KNOWN_FRAGMENTS = {
    # markdown bold/backtick run, mid-sentence - the empty-remainder rule itself is in
    # src/converter/README.md under `diff`.
    ".** Where every network is inside the",
    # event-list fragment - the full touch-first event list is in
    # src/openness-cli/README.md under `hmi-edit-screen`.
    "on buttons), screens use",
    # trailing arrow/em-dash run - the zero-byte/truncated-write route is in
    # src/converter/README.md under `drift-check`.
    "unparseable — a zero-byte file, a truncated write — →",
}


def read(path):
    with io.open(path, encoding="utf-8") as handle:
        return handle.read()


def normalise(text):
    return re.sub(r"\s+", " ", text.lower()).strip()


try:
    old = subprocess.check_output(["git", "show", BASELINE + ":CLAUDE.md"]).decode("utf-8")
except subprocess.CalledProcessError:
    sys.stderr.write("cannot read CLAUDE.md at %s\n" % BASELINE)
    raise SystemExit(2)

lines = old.split("\n")
try:
    start = next(i for i, l in enumerate(lines) if l.startswith("## Commands"))
except StopIteration:
    sys.stderr.write("no '## Commands' section at %s - wrong baseline?\n" % BASELINE)
    raise SystemExit(2)
fences = [i for i, l in enumerate(lines[start:], start) if l.strip() == "```"]
commands_block = "\n".join(lines[fences[0] + 1: fences[1]])

markers = set(re.findall(r"`([^`\n]{4,60})`", commands_block))
markers |= set(re.findall(r"(--[a-z][a-z0-9-]{3,30})", commands_block))
markers = {m.strip() for m in markers if len(m.strip()) >= 4}

haystack = normalise("\n".join(read(p) for p in DESTINATIONS))
unfound = [m for m in markers if normalise(m) not in haystack]
missing = sorted(m for m in unfound if m not in KNOWN_FRAGMENTS)
allowed = sorted(m for m in unfound if m in KNOWN_FRAGMENTS)

out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
out.write("baseline                  : %s\n" % BASELINE)
out.write("markers in that Commands block : %d\n" % len(markers))
out.write("still findable                 : %d\n" % (len(markers) - len(unfound)))
out.write("known fragments (not facts)    : %d\n" % len(allowed))
out.write("UNACCOUNTED FOR                : %d\n" % len(missing))
if missing:
    out.write("\n--- migrate each of these, or justify and add to KNOWN_FRAGMENTS ---\n")
    for m in missing:
        out.write("  %s\n" % m)
else:
    out.write("\nGATE PASSED: nothing was dropped.\n")
out.flush()

raise SystemExit(1 if missing else 0)
