"""Size ratchet for every file injected into an agent's context.

CLAUDE.md, the agent briefs and the skills are loaded before an agent does any work, so
their bytes are a tax on every dispatch. CLAUDE.md grew 45,130 -> 96,245 bytes in eleven
days and NOT ONE commit in that span reduced it - because no commit ever had to justify
growth. This makes growth explicit: a file may not exceed its ceiling, and raising a
ceiling is a commit of its own with a reason in the message.

    python tools/check-file-budgets.py [--staged]

Default measures the worktree. --staged measures the staged blobs of budgeted files that
are actually in the staged set, and is what hooks/pre-commit uses, so the gate judges what
is about to be committed rather than whatever is on disk.

Exit 0 = every budgeted file within its ceiling. Exit 1 = at least one is over, or the
table names a file that no longer exists. Exit 2 = the gate could not run.
EXIT 2 IS NOT A PASS.

CEILINGS ARE SEEDED AT EACH FILE'S SIZE WHEN IT WAS ADDED, ROUNDED UP TO THE NEXT 512
BYTES. They are not a judgement about the right size - they are a ratchet. That is
deliberate: nobody can say what the right size for a skill is, but everybody can say
whether it grew, and growth is the failure mode that actually happened.

BYTES ARE CRLF-EQUIVALENT. core.autocrlf=true here, so git stores these files LF and
materialises them CRLF; --staged adds the newline count back rather than reporting the
smaller blob. Every size in this project's record is the worktree number, so without that
the gate and the commit log disagree by one byte per line.

A file NOT in the table is ignored, so the hook can run unconditionally. A file IN the
table that has been deleted is a failure, not a skip - otherwise the table rots silently
and a deletion quietly removes a budget nobody notices is gone.
"""
import io, os, subprocess, sys

# (path, ceiling). Seeded 2026-08-21: each file's size that day rounded up to a 512-byte
# boundary with AT LEAST 256 bytes of slack - EXCEPT CLAUDE.md, which keeps the 20,480
# ceiling held through its own cut. The slack floor matters: a first pass rounded to the
# next 512 alone and left one file 20 bytes under its ceiling, where a typo fix would have
# failed the gate. A gate that cries wolf on trivial edits is one people learn to bypass.
# To raise one of these: do it in its own commit, and say in the message what earned it.
BUDGETS = [
    ("CLAUDE.md", 20480),

    (".claude/agents/assertion-enumerator.md", 5120),
    (".claude/agents/hmi-designer.md", 7680),
    # Raised 8704 -> 9216 on 2026-08-21. Earned by two additions the same day, both of
    # which change what the agent must DO rather than explaining something: the
    # evidence.json `deferred`/`transient` fields (a run could not otherwise hand back a
    # split gate honestly), and round-trip discipline (two real dispatches spent 41 and 67
    # tool calls, and round-trips dominate wall clock). The file is 8,853 -> ~9,180 after
    # both. It was 6,234 post-cut, so this is real growth and deliberately visible.
    (".claude/agents/lad-coder.md", 9216),
    (".claude/agents/lad-reader.md", 5120),

    (".claude/skills/design-for-testability/SKILL.md", 43008),
    (".claude/skills/enumerate-assertions/SKILL.md", 19968),
    (".claude/skills/explain-plc-block/SKILL.md", 6656),
    (".claude/skills/gen-architecture/SKILL.md", 24064),
    (".claude/skills/gen-block-modify-fix/SKILL.md", 11264),
    (".claude/skills/gen-block-modify-purpose/SKILL.md", 10752),
    (".claude/skills/gen-block-new/SKILL.md", 19968),
    (".claude/skills/gen-code-structure/SKILL.md", 16896),
    (".claude/skills/gen-equipment-spec/SKILL.md", 19456),
    (".claude/skills/gen-functional-analysis/SKILL.md", 5120),
    (".claude/skills/gen-pid-analysis/SKILL.md", 8704),
    (".claude/skills/review-conventions/SKILL.md", 30208),
    (".claude/skills/review-functional/SKILL.md", 18944),
    (".claude/skills/review-simplicity/SKILL.md", 12288),
]

STAGED = "--staged" in sys.argv[1:]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)


def crlf_equivalent(raw):
    """Byte length as it would appear in the worktree, whatever git handed us.

    A blob stored LF gains one byte per line. A blob that already carries CRLF - which
    would mean autocrlf is off in this clone - is already the worktree figure and must
    not be inflated a second time. (Arithmetic carried over from the CLAUDE.md-only
    budget script this replaces, where it was covered by its own test cases.)
    """
    if b"\r\n" in raw:
        return len(raw)
    return len(raw) + raw.count(b"\n")


def staged_paths():
    try:
        out = subprocess.check_output(["git", "diff", "--cached", "--name-only"])
    except (subprocess.CalledProcessError, OSError):
        sys.stderr.write("cannot read the staged set - NOTHING CHECKED\n")
        raise SystemExit(2)
    return set(out.decode("utf-8", "replace").split("\n"))


def measure_staged(path):
    try:
        raw = subprocess.check_output(["git", "show", ":" + path])
    except (subprocess.CalledProcessError, OSError):
        return None
    return crlf_equivalent(raw)


def measure_worktree(path):
    try:
        with open(path, "rb") as handle:
            return len(handle.read())
    except (IOError, OSError):
        return None


rows, over, missing = [], [], []

if STAGED:
    in_index = staged_paths()
    source = "staged blobs, CRLF-equivalent"
    checked = [(p, c) for p, c in BUDGETS if p.replace("\\", "/") in in_index]
else:
    source = "worktree files"
    checked = list(BUDGETS)

for path, ceiling in checked:
    size = measure_staged(path) if STAGED else measure_worktree(path)
    if size is None:
        # In --staged mode a budgeted file may be staged for DELETION, which is a
        # deliberate act; the table must then be edited in the same commit. Either way
        # an unreadable budgeted file is reported, never skipped.
        missing.append(path)
        continue
    rows.append((path, size, ceiling))
    if size > ceiling:
        over.append((path, size, ceiling))

out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
out.write("measured                       : %s\n" % source)
out.write("files in budget table          : %d\n" % len(BUDGETS))
out.write("files checked this run         : %d\n" % len(rows))
out.write("OVER BUDGET                    : %d\n" % len(over))

if not rows and not missing:
    out.write("\nnothing to check: no budgeted file was staged.\n")

if over or missing:
    out.write("\n--- GATE FAILED ---\n")
    for path, size, ceiling in over:
        out.write("  OVER  %s\n        %d bytes, ceiling %d, over by %d\n"
                  % (path, size, ceiling, size - ceiling))
    for path in missing:
        out.write("  GONE  %s is in the budget table but could not be read\n" % path)
    out.write("\nDo not fix this by deleting a hard rule, a routing rule or an\n")
    out.write("instruction. The rule is: new findings land in the README or\n")
    out.write("docs/notes, and a resident file earns a line only if it changes what\n")
    out.write("an agent must do BEFORE it looks anything up.\n")
    out.write("If the growth is genuinely earned, raise that file's ceiling in\n")
    out.write("tools/check-file-budgets.py in ITS OWN COMMIT, saying what earned it.\n")
else:
    tight = sorted(rows, key=lambda r: r[2] - r[1])[:3]
    out.write("\nGATE PASSED: every budgeted file is within its ceiling.\n")
    if tight:
        out.write("tightest headroom:\n")
        for path, size, ceiling in tight:
            out.write("  %5d bytes  %s\n" % (ceiling - size, path))
out.flush()

raise SystemExit(1 if (over or missing) else 0)
