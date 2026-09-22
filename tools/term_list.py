# -*- coding: utf-8 -*-
"""THE term-list parser. One reader, so the builder and the verifier cannot disagree. (M-24.)

WHY THIS MODULE EXISTS. Until 2026-09-21 `sanitization/scrub-terms.md` had TWO readers with
different strictness, and they were measured disagreeing:

  * `build-scrub-rules.py` demanded five columns and a `class` from a fixed set, and REFUSED the
    whole file - exit 2, NOTHING EXAMINED - on any row that failed.
  * `verify-scrub.py`'s `derive_needles` took `cells[0]` from any row with two or more columns,
    ignored `class` entirely, and silently skipped the rest.

So three rows carrying an invalid class were **hunted as T1 by the verifier while the builder had
refused the file they came from**, and three files were certified `T1=0` against a vocabulary that
no rule set had ever been built from. Nothing shipped wrong; the near-miss is the point, and the
permissive tool was the one handing out clean bills of health.

*** INDEPENDENCE OF JUDGEMENT IS NOT LICENCE TO DISAGREE ABOUT THE INPUT. *** The builder and the
verifier are deliberately independent - that independence is the entire reason running the verifier
against the builder's output proves anything. But a row either IS a term or it is NOT. That is a
fact about a file, not a judgement about a repository, and it must not depend on who is asking.
This module is therefore the narrowest possible shared surface: it reads the table and says what is
in it. It computes no variants, emits no rules, tiers no needles, and knows nothing about a
repository. Everything downstream of "what does this file say" stays in the tool that owns it.

*** A MALFORMED ROW IS REFUSED, NEVER SKIPPED. *** The strict reading wins because the permissive
one has a failure mode with no symptom: a term list that silently shrinks still produces output
that looks exactly like correct output. There is no option to relax this, deliberately.

*** THE PROSE IN THIS FILE IS CORPUS. *** Any test fixture that copies a tool into a throwaway
repository also copies its comments, and the builder scans `.py` as build source - so a word written
here can change the token statistics a test depends on. The first version of this docstring used the
word "f-l-a-g" in a sentence, which made a standalone token of it, which made a
never-stands-alone member suddenly stand alone, which failed a build-scrub-rules test 1,200 lines
away. That test was RIGHT and this file was wrong. Prefer words that are not also fixture
vocabulary.

Importable as `term_list` from any script in tools/, because a script run as
`python tools/<name>.py` has tools/ as sys.path[0]. Test fixtures that copy a tool into a temporary
repository MUST copy this file alongside it - a tool whose parser is missing does not fall back, it
fails to import, which is the correct and loud behaviour.
"""
import io
import os

# Presentational, with one exception that is not: membership of SPACED_CLASSES is the only thing any
# consumer tests, and it decides whether a multi-word term also gets its spaced form hunted.
TERM_CLASSES = ("jobcode", "company", "site", "modelline", "block", "member", "pathstem")
SPACED_CLASSES = ("company", "site", "modelline")

HEADER_CELLS = ("live", "term", "source")


def is_table_row(line):
    """A markdown table row that is not the header rule. Comments and prose are not rows."""
    line = line.strip()
    return line.startswith("|") and not line.startswith("|--") and not line.startswith("| ---")


def load_terms(path):
    """-> (rows, bad). `rows` is [{live, invented, class, scope, variants}]; `bad` is [(line_no, why)].

    A caller that sees a non-empty `bad` MUST refuse. Returning the two separately rather than
    raising is deliberate: the builder reports every malformed row at once, which is worth more to
    whoever has to fix the file than the first one would be.

    `(None, [])` means the file does not exist - which is NOT the same as an empty list of terms,
    and callers distinguish them. A missing term list on a clone that has never worked a live job is
    ordinary; an empty one where live material is present is exit 2.
    """
    if not os.path.isfile(path):
        return None, []
    rows, bad = [], []
    for n, line in enumerate(io.open(path, encoding="utf-8"), 1):
        if not is_table_row(line):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if cells and cells[0].lower() in HEADER_CELLS:
            continue
        if len(cells) < 5:
            # This branch was once `if len(cells) < 3: continue`, and a row that lost a column to a
            # typo vanished without ever reaching `bad` - the exact silent shrink this design exists
            # to prevent. Every non-header row now either parses or refuses.
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


class MalformedTermList(Exception):
    """Raised by readers that cannot report a worklist and must simply stop.

    The builder prints every bad row and exits 2. The verifier is a library to three other tools, so
    it raises instead - but it MUST NOT continue, because continuing is precisely the M-24 defect.
    """

    def __init__(self, path, bad):
        self.path = path
        self.bad = bad
        Exception.__init__(self, "%s: %d malformed row(s): %s"
                           % (path, len(bad),
                              "; ".join("line %d: %s" % (n, why) for n, why in bad[:5])))
