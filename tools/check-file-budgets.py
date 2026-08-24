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

The run ALSO reports every budgeted file with less than SLACK_FLOOR_BYTES of headroom.
That report NEVER changes the exit code - see the constant for why.

CEILINGS ARE SEEDED AT EACH FILE'S SIZE WHEN IT WAS ADDED, ROUNDED UP TO THE NEXT 512
BYTES. They are not a judgement about the right size - they are a ratchet. That is
deliberate: nobody can say what the right size for a skill is, but everybody can say
whether it grew, and growth is the failure mode that actually happened.

BYTES ARE CRLF-EQUIVALENT ON BOTH PATHS. core.autocrlf=true here, so git stores these
files LF and materialises them CRLF; both --staged and the default add the newline count
back rather than reporting the smaller LF figure. Every size in this project's record is
the worktree number, so without that the gate and the commit log disagree by one byte per
line.

  Fixed 2026-08-24: the default path returned the raw on-disk length while --staged
  returned the CRLF-equivalent, so ONE COMMIT MEASURED TWO DIFFERENT WAYS - and the
  permissive path was the one a human runs by hand while the strict one was the hook's.
  It only diverges where a budgeted file actually sits on disk with LF, which is not
  hypothetical: the main checkout carried `.claude/agents/hmi-designer.md` at 7,364 LF
  bytes against a 7,476 CRLF-equivalent, so the by-hand run read 112 bytes more headroom
  and listed ONE tight file where the hook listed two. Normalising the worktree path
  (rather than dropping the arithmetic from --staged) is the direction that cannot
  silently loosen a gate. No budgeted file changed side of its ceiling.

WHAT BELONGS IN THE TABLE: files injected into an agent's context - CLAUDE.md, the agent
briefs in .claude/agents/, the skills in .claude/skills/*/SKILL.md. As of 2026-08-24 the
table is EXHAUSTIVE over that scope: 1 + 4 + 14 = 19 files, matching `len(BUDGETS)`.
NOTHING ENFORCES THAT. A new skill or agent added tomorrow is simply unbudgeted and the
gate passes, because the table is the script's only input. Source files are NOT candidates
however large they get - src/, tools/, tests/ are read on demand, not preloaded, so their
bytes are not a per-dispatch tax and the ratchet has no claim on them.

A file NOT in the table is ignored, so the hook can run unconditionally. A file IN the
table that has been deleted is a failure, not a skip - otherwise the table rots silently
and a deletion quietly removes a budget nobody notices is gone.
"""
import io, os, subprocess, sys

# The seeding rule's slack floor, as a number the script can actually read.
#
# It was prose in four comments here and in ZERO lines of code from the day the table was
# seeded, so nothing ever checked it: the script compared files against their ceilings and
# never against its own seeding rule. Three violations were found by eye in a single day
# (two skills at 5 and 42 bytes of headroom, CLAUDE.md at 152). Noticing by eye is exactly
# what a budget table exists to stop - a rule that depends on somebody squinting at the
# "tightest headroom" list is a convention, not a gate.
#
# 🔴 IT REPORTS. IT MUST NEVER FAIL. That distinction is the whole design, and "improving"
# it into a refusal would invert it. A file under the floor is INSIDE ITS BUDGET; refusing
# the commit would be the gate refusing work that complies with it - precisely the cry-wolf
# behaviour the floor was invented to prevent. Curing a warning with a louder warning is
# not an improvement. The exit code contract is unchanged (0 within ceilings, 1 over or a
# table entry missing, 2 could not run); only the reader is better informed.
SLACK_FLOOR_BYTES = 256

# (path, ceiling). Seeded 2026-08-21: each file's size that day rounded up to a 512-byte
# boundary with AT LEAST SLACK_FLOOR_BYTES of slack - EXCEPT CLAUDE.md, which keeps the
# 20,480 ceiling held through its own cut. The slack floor matters: a first pass rounded to
# the next 512 alone and left one file 20 bytes under its ceiling, where a typo fix would
# have failed the gate. A gate that cries wolf on trivial edits is one people learn to
# bypass. The floor is enforced-as-a-report above, not left to the reader as it was here.
# To raise one of these: do it in its own commit, and say in the message what earned it.
BUDGETS = [
    # Raised 20480 -> 20992 on 2026-08-23. Earned by ONE table row: `portal-close`, which
    # TERMINATES Portal processes and was documented in no markdown file in the repository -
    # `grep -rn "portal-close" --include=*.md .` returned zero hits on the day it shipped.
    # This is the discovery case the gate's own text carves out. The index is where an agent
    # learns a command EXISTS; one that does not appear there is one an agent cannot look up,
    # and the failure mode is not ignorance but substitution - reaching for `taskkill` instead
    # of a `--yes`-gated tool that refuses a process younger than five minutes. The row says
    # it terminates, that it sweeps empty Portals BY DEFAULT, and that it has never been run.
    # Three previous additions this week were trimmed away rather than raising this; that was
    # right for those, and repeating it here would mean keeping a destructive command hidden
    # to protect a number. 423 bytes of slack, above the documented 256-byte floor.
    #
    # Raised 20992 -> 21504 on 2026-08-23. NOT earned by new content - this is the second
    # kind of raise, the one that pays for growth already landed rather than buying room for
    # growth to come, and it is the FIRST raise the floor report found instead of a person's
    # eye. Those 423 bytes were spent the same week by two converter rows: `served-area`
    # (182b3f9) cost 116 bytes and left 307, `neighbours` (466185a) cost a further 155 and
    # left 152. Both rows are load-bearing in the way the index is meant to be - each states
    # that EXIT 2 IS NOT A PASS for its command, which is the project's standing "empty is
    # not clean" rule at the one place an agent reads before running the tool - so neither is
    # a candidate for trimming back out. What is wrong is the headroom, not the rows: at 152
    # bytes a typo fix in CLAUDE.md could fail the build, and the reflex that teaches is
    # --no-verify. This restores the seeding rule's slack rather than buying anything: 664
    # bytes, none of it spent, and no other budgeted file's ceiling moves with it.
    ("CLAUDE.md", 21504),

    (".claude/agents/assertion-enumerator.md", 5120),
    (".claude/agents/hmi-designer.md", 7680),
    # Raised 8704 -> 9216 on 2026-08-21. Earned by two additions the same day, both of
    # which change what the agent must DO rather than explaining something: the
    # evidence.json `deferred`/`transient` fields (a run could not otherwise hand back a
    # split gate honestly), and round-trip discipline (two real dispatches spent 41 and 67
    # tool calls, and round-trips dominate wall clock). It was 6,234 post-cut, so this is
    # real growth and deliberately visible. Landed at 9,058, not the ~9,180 first predicted
    # here - the round-trip text overshot 9216 by 177 and was compressed rather than the
    # ceiling raised a second time, which is the pattern this budget exists to prevent.
    (".claude/agents/lad-coder.md", 9216),
    (".claude/agents/lad-reader.md", 5120),

    (".claude/skills/design-for-testability/SKILL.md", 43008),
    (".claude/skills/enumerate-assertions/SKILL.md", 19968),
    (".claude/skills/explain-plc-block/SKILL.md", 6656),
    (".claude/skills/gen-architecture/SKILL.md", 24064),
    # Raised 11264 -> 11776 and 10752 -> 11264 on 2026-08-23. NOT earned by new content — earned by
    # the gate having drifted into the state this table's own header warns about. The two files sat at
    # 42 and *** FIVE *** bytes of headroom after the `converter diff --insert` routing rules landed in
    # each of them (W2, `2c5eab9`). The header says a first pass left one file 20 bytes under its
    # ceiling "where a typo fix would have failed the gate", and that "a gate that cries wolf on trivial
    # edits is one people learn to bypass". Five bytes is well past that line: correcting a typo in
    # either skill would have failed the build, and the reflex that teaches is `--no-verify`.
    # This is the ONE case where raising a ceiling protects the gate instead of eroding it. Both now
    # sit at the documented 256-byte slack floor or better (~517 and ~554 bytes).
    (".claude/skills/gen-block-modify-fix/SKILL.md", 11776),
    (".claude/skills/gen-block-modify-purpose/SKILL.md", 11264),
    (".claude/skills/gen-block-new/SKILL.md", 19968),
    (".claude/skills/gen-code-structure/SKILL.md", 16896),
    (".claude/skills/gen-equipment-spec/SKILL.md", 19456),
    (".claude/skills/gen-functional-analysis/SKILL.md", 5120),
    (".claude/skills/gen-pid-analysis/SKILL.md", 8704),
    (".claude/skills/review-conventions/SKILL.md", 30208),
    (".claude/skills/review-functional/SKILL.md", 18944),
    # Raised 12288 -> 12800 on 2026-08-21, and NOT because the file needed room:
    # the C-603 mechanization paragraph was compressed twice to fit under 12,288 and
    # landed at 12,267, leaving 21 bytes. That is the exact state this table's own
    # seeding comment calls out as a gate that cries wolf - a typo fix would fail it.
    # This restores the >= 256 slack floor the seeding rule requires. It buys the file
    # 533 bytes, none of which is spent.
    (".claude/skills/review-simplicity/SKILL.md", 12800),
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
    """The SAME arithmetic as measure_staged, deliberately - see the module docstring.

    This returned the raw on-disk length until 2026-08-24. A file already materialised
    CRLF is unaffected (crlf_equivalent leaves it alone), so the change is only visible on
    a budgeted file sitting on disk with LF - where the old code read one extra byte of
    headroom per line and let the by-hand run disagree with the hook about the very same
    commit. Sharing crlf_equivalent, rather than each path doing its own arithmetic, is
    what makes the two incapable of drifting apart again.
    """
    try:
        with open(path, "rb") as handle:
            return crlf_equivalent(handle.read())
    except (IOError, OSError):
        return None


rows, over, missing, under_floor = [], [], [], []

if STAGED:
    in_index = staged_paths()
    source = "staged blobs, CRLF-equivalent"
    checked = [(p, c) for p, c in BUDGETS if p.replace("\\", "/") in in_index]
else:
    source = "worktree files, CRLF-equivalent"
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
    elif ceiling - size < SLACK_FLOOR_BYTES:
        # elif, not if: a file over its ceiling has negative headroom, so the arithmetic
        # alone would put it on BOTH lists. They are OPPOSITE conditions - "you have
        # complied and the ceiling has drifted too tight" versus "you have not complied" -
        # and a reader skimming two lists must never be able to conflate them.
        under_floor.append((path, size, ceiling))

out = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
out.write("measured                       : %s\n" % source)
out.write("files in budget table          : %d\n" % len(BUDGETS))
out.write("files checked this run         : %d\n" % len(rows))
out.write("OVER BUDGET                    : %d\n" % len(over))
# Stated on EVERY run including the zero case, so a reader can tell "nothing is tight"
# apart from "this run did not look".
out.write("UNDER SLACK FLOOR (not a fail) : %d\n" % len(under_floor))

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

# Printed LAST on both paths, and only when it has something to say. Last because on the
# failure path the refusal is what the reader must act on and the advisory must not
# compete with it; on both paths because a tight file that becomes invisible whenever some
# OTHER file is over is a tight file nobody ever pays for.
if under_floor:
    out.write("\n--- TIGHT: WITHIN BUDGET, UNDER THE %d-BYTE SLACK FLOOR (NOT A FAILURE) ---\n"
              % SLACK_FLOOR_BYTES)
    for path, size, ceiling in sorted(under_floor, key=lambda r: r[2] - r[1]):
        out.write("  TIGHT   %s\n        %d bytes headroom, floor %d, size %d, ceiling %d\n"
                  % (path, ceiling - size, SLACK_FLOOR_BYTES, size, ceiling))
    out.write("\nThese files COMPLY and the gate is NOT refusing them - this is a report,\n")
    out.write("not a refusal, and the exit code above is unaffected by it. Failing here\n")
    out.write("would refuse work that is inside its budget, which is the cry-wolf\n")
    out.write("behaviour the slack floor was invented to prevent.\n")
    out.write("They are listed because a ceiling this close to its file is one that a\n")
    out.write("typo fix can trip, and the reflex that teaches is --no-verify.\n")
    out.write("The fix is NOT to shrink the file. When one of these is next edited,\n")
    out.write("raise its ceiling to the next 512 boundary in its own commit, saying what\n")
    out.write("earned it - the same procedure as an over-budget raise, done before the\n")
    out.write("gate has to refuse anything.\n")

out.flush()

raise SystemExit(1 if (over or missing) else 0)
