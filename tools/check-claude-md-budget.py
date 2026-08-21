"""Anti-regrowth size budget for CLAUDE.md.

CLAUDE.md is injected into every dispatch, so its size is a tax on every request in
the project. It grew 45,130 -> 96,245 bytes in the eleven days before the 2026-08-21
context cut, and NOT ONE commit in that span reduced it. Growth is monotonic because
new findings land in CLAUDE.md - it is the file everyone reads. This gate is the thing
that makes the file shrink-or-hold instead.

    python tools/check-claude-md-budget.py [--staged]

Default measures the file in the worktree. --staged measures the staged blob and is
what hooks/pre-commit uses, so the gate judges what is about to be committed rather
than whatever happens to be on disk.

Exit 0 = within budget. Exit 1 = over budget, the gate failed. Exit 2 = the gate could
not run (CLAUDE.md unreadable, or not staged when --staged was asked for). Exit 2 is
NOT a pass - this repo's mechanical floor uses the same 1-vs-2 split, and "examined
nothing" has never been a green result here.

BYTES ARE ALWAYS REPORTED CRLF-EQUIVALENT. core.autocrlf=true, so git stores this file
LF and materialises it CRLF - a 149-byte difference at present, one per line. Every
size in the project's record is the worktree (CRLF) number, so --staged adds the
newline count back rather than reporting the smaller blob figure. Without that, the
gate and the commit log would disagree by 149 bytes and someone would eventually
"fix" the wrong one.

What this deliberately does NOT check: whether the content is any good, whether it
duplicates a README, or whether a fact was dropped. Size is a proxy, and a crude one.
The migration gate (check-claude-md-migration.py) is what protects the facts; run it
before any trim. This one only stops the file getting bigger.
"""
import io, os, subprocess, sys

CEILING = 20480          # 20 KiB - the ceiling held throughout the 2026-08-21 cut.
TARGET = "CLAUDE.md"

STAGED = "--staged" in sys.argv[1:]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)


def crlf_equivalent(raw):
    """Byte length as it would appear in the worktree, whatever git handed us.

    A blob stored LF gains one byte per line. A blob that already carries CRLF - which
    would mean autocrlf is off in this clone - is already the worktree figure and must
    not be inflated a second time.
    """
    if b"\r\n" in raw:
        return len(raw)
    return len(raw) + raw.count(b"\n")


if STAGED:
    source = "staged blob, CRLF-equivalent"
    try:
        raw = subprocess.check_output(["git", "show", ":" + TARGET])
    except (subprocess.CalledProcessError, OSError):
        sys.stderr.write("cannot read %s from the index - is it staged?\n" % TARGET)
        raise SystemExit(2)
    size = crlf_equivalent(raw)
else:
    source = "worktree file"
    try:
        with open(TARGET, "rb") as handle:
            raw = handle.read()
    except (IOError, OSError):
        sys.stderr.write("cannot read %s in %s\n" % (TARGET, ROOT))
        raise SystemExit(2)
    size = len(raw)

over = size > CEILING
headroom = CEILING - size

out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
out.write("target                         : %s\n" % TARGET)
out.write("measured                       : %s\n" % source)
out.write("bytes                          : %d\n" % size)
out.write("ceiling                        : %d\n" % CEILING)
if over:
    out.write("OVER BUDGET BY                 : %d\n" % -headroom)
    out.write("\n--- GATE FAILED: CLAUDE.md is over budget ---\n")
    out.write("Do not fix this by deleting a hard rule or a routing rule.\n")
    out.write("The rule is: new findings land in the README or docs/notes.\n")
    out.write("CLAUDE.md earns a line only if it changes what an agent must do\n")
    out.write("BEFORE it looks anything up. Move the fact to its owning doc, then\n")
    out.write("confirm it greps positive there and rerun check-claude-md-migration.py.\n")
else:
    out.write("headroom                       : %d\n" % headroom)
    out.write("\nGATE PASSED: within budget.\n")
out.flush()

raise SystemExit(1 if over else 0)
