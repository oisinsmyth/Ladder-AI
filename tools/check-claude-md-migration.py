"""Gate for the 2026-08-21 CLAUDE.md context cut.

Every falsifiable marker that the PRE-CUT CLAUDE.md carried in its Commands block must
still be findable - either resident in the current CLAUDE.md, or in one of the docs the
content was migrated into. A marker found nowhere means the migration dropped it.

Run before committing any further trim of CLAUDE.md:

    python tools/check-claude-md-migration.py [baseline-commit]

Exit 0 = nothing dropped. Exit 1 = at least one marker is unaccounted for. Exit 2 = the
gate could not run.

Markers are backticked identifiers and --flags only. ALLCAPS prose fragments are
deliberately NOT checked: the narration was what the cut set out to remove, so holding
it as a requirement would fail the gate by design. Known residual false positives are
markdown/punctuation fragments (e.g. `filename does not (`), not facts; they live in
tools/claude-md-migration.fragments with the standing warning against adding to it.

THIS IS NOW A THIN WRAPPER around tools/check-doc-migration.py, which is the same gate
with the CLAUDE.md specifics lifted out into arguments (2026-08-21). The path, the
default baseline and the output are unchanged, because AITODO.md, tools/README.md and
docs/notes/context-cut-handoff.md all cite this filename and this gate is the only thing
standing between a tidy-up and a silently lost fact.
"""
import os, subprocess, sys

BASELINE = sys.argv[1] if len(sys.argv) > 1 else "a1eca27"

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

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

cmd = [
    sys.executable,
    os.path.join(ROOT, "tools", "check-doc-migration.py"),
    BASELINE,
    "CLAUDE.md",
] + DESTINATIONS + [
    "--fenced-section", "## Commands",
    "--allow", os.path.join(ROOT, "tools", "claude-md-migration.fragments"),
]

raise SystemExit(subprocess.call(cmd, cwd=ROOT))
