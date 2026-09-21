#!/usr/bin/env python3
"""Self-tests for term_list.py, THE shared term-list parser (M-24).

    python tools/term_list.tests.py

THE LAST TWO CASES ARE THE REASON THIS FILE EXISTS. Everything above them tests the parser; those
two test the AGREEMENT, by running the real builder and the real verifier against one malformed
term list and asserting that neither accepts it. That is the property M-24 was filed about, and a
property nobody tests is a property that comes back.

The fixtures invent vocabulary. Nothing here is a real identifier.
"""
import importlib.util
import io
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import term_list                                            # noqa: E402

BUILDER = os.path.join(HERE, "build-scrub-rules.py")
ORACLE = os.path.join(HERE, "verify-scrub.py")
TERMLIB = os.path.join(HERE, "term_list.py")

HEADER = ("# terms\n\n| live | invented | class | scope | variants |\n|---|---|---|---|---|\n")

passed = 0
failed = 0
failures = []


def case(name, body):
    global passed, failed
    try:
        body()
        passed += 1
        print("  ok    %s" % name)
    except AssertionError as exc:
        failed += 1
        failures.append(name)
        print("  FAIL  %s\n            %s" % (name, exc))


def write_terms(tmp, body):
    path = os.path.join(tmp, "scrub-terms.md")
    io.open(path, "w", encoding="utf-8", newline="\n").write(HEADER + body)
    return path


def in_tmp(fn):
    tmp = tempfile.mkdtemp(prefix="termlist-")
    try:
        return fn(tmp)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --- the parser ----------------------------------------------------------------------------------

def a_well_formed_row_parses():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, "| Alpha | Beta | jobcode | global | auto |\n"))
        assert not bad, bad
        assert len(rows) == 1, rows
        assert rows[0]["live"] == "Alpha" and rows[0]["invented"] == "Beta"
        assert rows[0]["class"] == "jobcode"
        assert rows[0]["variants"] is None, "auto means derive them"
    in_tmp(body)


def a_BAD_CLASS_is_refused_not_skipped():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, "| Alpha | Beta | processs | global | auto |\n"))
        assert len(bad) == 1, bad
        assert "class must be one of" in bad[0][1]
        assert rows == [], "a refused row must not also be returned"
    in_tmp(body)


def a_SHORT_ROW_is_refused_not_skipped():
    """The silent shrink. A row that loses a column to a typo must never just vanish."""
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, "| Alpha | Beta |\n"))
        assert len(bad) == 1, bad
        assert "needs 5 columns" in bad[0][1]
    in_tmp(body)


def an_EMPTY_live_or_invented_is_refused():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, "|  | Beta | jobcode | global | auto |\n"))
        assert len(bad) == 1 and "required" in bad[0][1], bad
    in_tmp(body)


def EVERY_bad_row_is_reported_not_just_the_first():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(
            tmp, "| A | B | nope | global | auto |\n| C | D | alsonope | global | auto |\n"))
        assert len(bad) == 2, "a fixer wants the whole list, not one at a time: %r" % (bad,)
    in_tmp(body)


def the_HEADER_and_rule_rows_are_not_terms():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, "| Alpha | Beta | site | global | auto |\n"))
        assert not bad and len(rows) == 1, (rows, bad)
    in_tmp(body)


def PROSE_and_comments_are_not_rows():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(
            tmp, "<!-- a comment mentioning | pipes | -->\nordinary prose\n"
                 "| Alpha | Beta | site | global | auto |\n"))
        assert not bad, bad
        assert len(rows) == 1, rows
    in_tmp(body)


def EXPLICIT_variants_are_split():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, "| A | B | site | global | a; b ; c |\n"))
        assert not bad and rows[0]["variants"] == ["a", "b", "c"], rows
    in_tmp(body)


def a_MISSING_file_is_None_not_empty():
    """None means 'no list'; [] means 'a list declaring nothing'. Callers treat them differently."""
    rows, bad = term_list.load_terms(os.path.join(tempfile.gettempdir(), "definitely-not-here.md"))
    assert rows is None, rows
    assert bad == [], bad


def a_file_with_NO_rows_is_empty_not_None():
    def body(tmp):
        rows, bad = term_list.load_terms(write_terms(tmp, ""))
        assert rows == [] and bad == [], (rows, bad)
    in_tmp(body)


# --- THE AGREEMENT: the property M-24 was filed about ---------------------------------------------

def the_VERIFIER_RAISES_on_a_malformed_row():
    """It used to skip it, tier the row T1 anyway, and hand out a clean bill of health."""
    def body(tmp):
        path = write_terms(tmp, "| Alpha | Beta | processs | global | auto |\n")
        spec = importlib.util.spec_from_file_location("vs_under_test", ORACLE)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        try:
            mod.derive_needles(tmp, path, frozenset())
        except term_list.MalformedTermList as exc:
            assert "class must be one of" in str(exc), str(exc)
            return
        except Exception as exc:                            # a different exception is still a refusal
            assert "MalformedTermList" in type(exc).__name__, \
                "expected a term-list refusal, got %s: %s" % (type(exc).__name__, exc)
            return
        raise AssertionError("derive_needles ACCEPTED a row the builder refuses - this is M-24")
    in_tmp(body)


def the_TWO_TOOLS_AGREE_on_the_same_malformed_file():
    """The whole point. One file, two tools, one verdict."""
    def body(tmp):
        repo = os.path.join(tmp, "repo")
        os.makedirs(os.path.join(repo, "tools"))
        os.makedirs(os.path.join(repo, "sanitization"))
        for src, name in ((BUILDER, "build-scrub-rules.py"), (ORACLE, "verify-scrub.py"),
                          (TERMLIB, "term_list.py")):
            shutil.copy(src, os.path.join(repo, "tools", name))
        subprocess.run(["git", "init", "-q"], cwd=repo, check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        # The builder refuses (exit 3) to write rules anywhere git can see, BEFORE it parses
        # anything - the rule files name every identifier in the repository. That refusal is
        # correct and is a different tool's business; this fixture has to satisfy it to reach the
        # parse, or the test measures the wrong gate. It caught exactly that on its first run.
        io.open(os.path.join(repo, ".gitignore"), "w", encoding="utf-8",
                newline="\n").write("sanitization/\n")
        terms = os.path.join(repo, "sanitization", "scrub-terms.md")
        io.open(terms, "w", encoding="utf-8", newline="\n").write(
            HEADER + "| Alpha | Beta | processs | global | auto |\n")

        builder = subprocess.run(
            [sys.executable, os.path.join(repo, "tools", "build-scrub-rules.py"),
             "--repo", repo, "--terms", terms],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, cwd=repo)
        out = builder.stdout.decode("utf-8", "replace")
        assert builder.returncode == 2, \
            "builder should exit 2 NOTHING EXAMINED, got %d:\n%s" % (builder.returncode, out[-600:])
        assert "NOTHING EXAMINED" in out, out[-600:]

        probe = (
            "import importlib.util, sys\n"
            "sys.path.insert(0, %r)\n"
            "spec = importlib.util.spec_from_file_location('vs', %r)\n"
            "m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)\n"
            "try:\n"
            "    m.derive_needles(%r, %r, frozenset())\n"
            "    print('ACCEPTED')\n"
            "except Exception as e:\n"
            "    print('REFUSED', type(e).__name__)\n"
            % (os.path.join(repo, "tools"), os.path.join(repo, "tools", "verify-scrub.py"),
               os.path.join(repo, "sanitization"), terms))
        ver = subprocess.run([sys.executable, "-c", probe], stdout=subprocess.PIPE,
                             stderr=subprocess.STDOUT, cwd=repo)
        vout = ver.stdout.decode("utf-8", "replace")
        assert "REFUSED" in vout, \
            "the builder refused this file and the verifier did not - that IS M-24:\n%s" % vout
    in_tmp(body)


print("term_list.py self-tests (M-24)")
print("=" * 70)
for name, body in [
    ("a well-formed row parses", a_well_formed_row_parses),
    ("a BAD CLASS is refused, not skipped", a_BAD_CLASS_is_refused_not_skipped),
    ("a SHORT ROW is refused, not skipped", a_SHORT_ROW_is_refused_not_skipped),
    ("an EMPTY live or invented is refused", an_EMPTY_live_or_invented_is_refused),
    ("EVERY bad row is reported, not just the first", EVERY_bad_row_is_reported_not_just_the_first),
    ("the header and rule rows are not terms", the_HEADER_and_rule_rows_are_not_terms),
    ("prose and comments are not rows", PROSE_and_comments_are_not_rows),
    ("explicit variants are split", EXPLICIT_variants_are_split),
    ("a MISSING file is None, not empty", a_MISSING_file_is_None_not_empty),
    ("a file with NO rows is empty, not None", a_file_with_NO_rows_is_empty_not_None),
    ("AGREEMENT: the verifier RAISES on a malformed row", the_VERIFIER_RAISES_on_a_malformed_row),
    ("AGREEMENT: builder and verifier agree on one malformed file",
     the_TWO_TOOLS_AGREE_on_the_same_malformed_file),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
