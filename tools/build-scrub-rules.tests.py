"""Offline test suite for tools/build-scrub-rules.py.

    python tools/build-scrub-rules.tests.py

Same method as check-doc-migration.tests.py: each case builds a throwaway git repo, copies the
script under test into it UNMODIFIED, and runs it as a child process. No testability hooks were
added to the script - a gate that can be aimed elsewhere can be aimed away.

THE A8 CASE IS THE REASON THIS FILE EXISTS. A rule set that scrubs prose and misses every
lower-hyphenated directory built from the same word looks like a working scrub and is not, and the
only thing that tells the difference is an assertion on the STRING, not on the intent. Two further
regressions are pinned because both were live defects during construction, both measured: the
breadth filter withholding exactly the identifiers that matter, and path names being absent from
the corpus a content-only scan examines.

Exit-2 cases are asserted as carefully as exit-0 ones, because exit 2 means NOTHING WAS EXAMINED
and the one failure this tool must never have is reading as clean when it looked at nothing.
"""
import io, os, shutil, subprocess, sys, tempfile

EXIT_OK = 0
EXIT_FINDING = 1
EXIT_CANNOT_RUN = 2
EXIT_REFUSED = 3

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(ROOT, "tools", "build-scrub-rules.py")

passed, failed, failures = 0, 0, []


def case(name, body):
    global passed, failed
    tmp = tempfile.mkdtemp()
    try:
        body(tmp)
    except AssertionError as exc:
        failed += 1
        failures.append("%s: %s" % (name, exc))
        print("FAIL  %s" % name)
        print("      %s" % exc)
    else:
        passed += 1
        print("PASS  %s" % name)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def write(path, text, encoding="utf-8"):
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    with io.open(path, "w", encoding=encoding, newline="\n") as fh:
        fh.write(text)


def build(tmp, maps, terms, files=None):
    """A throwaway repo: the script under test, a sanitization/ directory, and some content."""
    # DEVNULL, not PIPE. `subprocess.call` with a PIPE nobody reads DEADLOCKS as soon as the child
    # fills the pipe buffer, and `git add` emits one CRLF warning per file: the 40-file case hung
    # here indefinitely while every smaller case passed, which is the worst possible shape for a
    # bug in a test harness.
    quiet = {"stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL, "cwd": tmp}
    os.makedirs(os.path.join(tmp, "tools"))
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "build-scrub-rules.py"))
    for name, doc in maps.items():
        write(os.path.join(tmp, "sanitization", name + ".map.json"), doc)
    if terms is not None:
        write(os.path.join(tmp, "sanitization", "scrub-terms.md"), terms)
    for rel, text in (files or {}).items():
        write(os.path.join(tmp, rel), text)
    write(os.path.join(tmp, ".gitignore"), "sanitization/\n")
    subprocess.call(["git", "init"], **quiet)
    subprocess.call(["git", "config", "user.email", "t@t"], **quiet)
    subprocess.call(["git", "config", "user.name", "t"], **quiet)
    subprocess.call(["git", "add", "-A"], **quiet)
    subprocess.call(["git", "commit", "-m", "baseline"], **quiet)
    return os.path.join(tmp, "tools", "build-scrub-rules.py")


def run(tmp, script, *args):
    proc = subprocess.Popen([sys.executable, script, "--repo", tmp, "--scan", "head"] + list(args),
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=tmp)
    out, err = proc.communicate()
    return (proc.returncode,
            out.decode("utf-8", "replace"),
            err.decode("utf-8", "replace"))


def rules_of(tmp):
    path = os.path.join(tmp, "sanitization", "scrub", "replace-text.txt")
    if not os.path.isfile(path):
        return []
    return [l.rstrip("\n") for l in io.open(path, encoding="utf-8") if l.strip()]


def assert_in(needle, haystack, what):
    assert needle in haystack, "%s: expected %r in:\n%s" % (what, needle, haystack[:1200])


def assert_eq(got, want, what):
    assert got == want, "%s: got %r, want %r" % (what, got, want)


# A term list with ZERO rows is a refusal by design, so the shared fixture carries one benign row.
# Cases that are specifically about the term list build their own.
TERMS_HEADER = ("# terms\n\n| live | invented | class | scope | variants |\n"
                "|---|---|---|---|---|\n"
                "| ZZ9999 | JOB9999 | jobcode | global | auto |\n")
TERMS_EMPTY = ("# terms\n\n| live | invented | class | scope | variants |\n"
               "|---|---|---|---|---|\n")
ONE_MAP = '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit"}}'


# --------------------------------------------------------------------------- permit

def emits_three_files(tmp):
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER + "| ZZ1234 | JOB9001 | jobcode | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit is referenced here.\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    for name in ("replace-text.txt", "replace-message.txt", "manifest.json"):
        assert os.path.isfile(os.path.join(tmp, "sanitization", "scrub", name)), "missing " + name


def a8_lower_concatenated_variant_matches_a_hyphenated_directory(tmp):
    """THE A8 REGRESSION. Asserted on the string a rule must match, never on the intent."""
    import re
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER,
              {"gen/acmewidgetunit-bench/notes.md": "see gen/acmewidgetunit-bench/x\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("gen/acmewidgetunit-bench/notes.md") for p in pats), \
        "no emitted rule matches the lower-hyphenated directory form - this is A8"


def path_only_occurrence_still_gets_a_rule(tmp):
    """A variant present ONLY in a path name. `git cat-file` never shows a path, so a content-only
    scan calls this dead and the directory survives the rewrite."""
    import re
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER,
              {"gen/AcmeWidgetUnit/keep.md": "nothing identifying in here\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("gen/AcmeWidgetUnit/keep.md") for p in pats), \
        "a path-only occurrence produced no rule"


def a_wide_variant_is_reported_but_still_emitted(tmp):
    """Breadth is a REPORT. Withholding on it left 77 of 93 real paths unmatched when measured."""
    import re
    files = dict(("doc%02d.md" % i, "AcmeWidgetUnit\n") for i in range(40))
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, files)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert_in("REPORT ONLY", out, "breadth must be reported")
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("AcmeWidgetUnit") for p in pats), "a wide variant was withheld, not reported"


def every_line_carries_an_explicit_arrow(tmp):
    """filter-repo's default replacement is ***REMOVED***. A line without `==>` substitutes that
    string into prose, silently, across the whole rewrite."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    run(tmp, s)
    lines = rules_of(tmp)
    assert lines, "no rules emitted"
    for l in lines:
        assert "==>" in l, "line without an explicit replacement: %r" % l
        assert "REMOVED" not in l.split("==>", 1)[1], "default replacement leaked: %r" % l


def longest_first_ordering_holds(tmp):
    import re
    maps = '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit", ' \
           '"AcmeWidgetUnitExtended": "GenericWidgetUnitExtended"}}'
    s = build(tmp, {"m": maps}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit and AcmeWidgetUnitExtended\n"})
    run(tmp, s)
    lens = []
    for l in rules_of(tmp):
        esc = l.split("==>")[0][len("regex:") + 2:-2]
        lens.append(len(re.sub(r"\\(.)", r"\1", esc)))
    assert lens == sorted(lens, reverse=True), "not longest-first: %r" % lens


def a_bom_map_parses(tmp):
    """25 of the 43 real maps carry a BOM; plain utf-8 dies on them."""
    s = build(tmp, {}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    write(os.path.join(tmp, "sanitization", "m.map.json"), ONE_MAP, encoding="utf-8-sig")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a BOM'd map must parse (%s%s)" % (out, err))
    assert rules_of(tmp), "BOM'd map contributed no rules"


def identity_mappings_are_dropped_and_counted(tmp):
    s = build(tmp, {"m": '{"Names": {"SameName": "SameName", "AcmeWidgetUnit": "GenericWidgetUnit"}}'},
              TERMS_HEADER, {"doc.md": "SameName AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("no-ops", out, "identity drops must be counted")
    assert not any("SameName" in l.split("==>")[0] for l in rules_of(tmp)), \
        "an identity mapping produced a rule"


def a_non_standard_section_contributes(tmp):
    """company/modelLine/identifiers are dropped by the C# loader and carry the site terms."""
    s = build(tmp, {"m": '{"company": {"AcmeHoldings": "GenericHoldings"}, "_purpose": "prose"}'},
              TERMS_HEADER, {"doc.md": "AcmeHoldings\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("company=1", out, "non-standard section must be counted as USED")
    assert rules_of(tmp), "non-standard section contributed no rule"


def a_structured_section_is_counted_but_ignored(tmp):
    s = build(tmp, {"m": '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit"}, '
                         '"NetworkComments": {"AcmeWidgetUnit#1": "text"}}'},
              TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("networkcomments=1", out, "ignored sections must be counted, not silent")


def a_term_row_overrides_a_map_key(tmp):
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| AcmeWidgetUnit | TermChosenName | block | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert any("TermChosenName" in l for l in rules_of(tmp)), "term row did not override the map"


def green_present_variant_is_withheld(tmp):
    """A second, Green-absent key is present deliberately: with only the withheld one, zero rules
    survive and the run is correctly exit 2, which would mask what this case is actually about."""
    maps = '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit", "AcmeSurvivor": "GenericSurvivor"}}'
    s = build(tmp, {"m": maps}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit AcmeSurvivor\n", "ir/reference/x.ir": "AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s, "--green", "ir/reference")
    assert_eq(code, EXIT_OK, "exit (%s)" % out)
    assert any("AcmeSurvivor" in l.split("==>")[0] for l in rules_of(tmp)), \
        "the Green-absent key should still have been emitted"
    assert_in("present in Green", out, "Green withholding must be reported")
    assert not any("AcmeWidgetUnit" in l.split("==>")[0] for l in rules_of(tmp)), \
        "a Green-present variant was emitted globally"


def a_declared_short_term_is_emitted_anyway(tmp):
    """A DECLARATION IS NOT A CANDIDATE — the regression that motivated the bypass.

    Job codes are five characters. With the length floor applied to declared terms, 15 of 19 real
    term rows were discarded INCLUDING ALL FOUR JOB CODES, and 11 of the 12 identifier occurrences
    in the tracked .gitignore survived a full application of all 98 rules. The floor exists to stop
    an INFERRED map key colliding with ordinary code; it must never touch a term the owner wrote
    down by hand."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| ZZ123 | JOB1234 | jobcode | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and ZZ123 both appear\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s)" % out)
    assert any("ZZ123" in l.split("==>")[0] for l in rules_of(tmp)), \
        "a 5-character DECLARED term got no rule - the length floor is eating the term list"
    assert_in("from DECLARED term rows", out, "the two populations must be reported separately")


def a_declared_green_present_term_is_emitted_anyway(tmp):
    """The Green test is also a filter for INFERRED names only. A declared term that happens to
    appear in a clean corpus is still a term the owner declared."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| AcmeDeclared | GenericDeclared | site | global | auto |\n",
              {"doc.md": "AcmeDeclared\n", "ir/reference/x.ir": "AcmeDeclared\n"})
    code, out, _ = run(tmp, s, "--green", "ir/reference")
    assert_eq(code, EXIT_OK, "exit (%s)" % out)
    assert any("AcmeDeclared" in l.split("==>")[0] for l in rules_of(tmp)), \
        "a declared term was withheld by the Green test"


def a_short_key_is_withheld_and_counted(tmp):
    s = build(tmp, {"m": '{"Names": {"Pump": "Mover", "AcmeWidgetUnit": "GenericWidgetUnit"}}'},
              TERMS_HEADER, {"doc.md": "Pump AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("under 8 chars", out, "short withholding must be counted")
    assert not any("Pump" in l.split("==>")[0] for l in rules_of(tmp)), "a 4-char key got a rule"


# --------------------------------------------------------------------------- refuse

def non_injective_map_refuses(tmp):
    s = build(tmp, {"m": '{"Names": {"AcmeWidgetUnit": "Shared", "AcmeOtherUnit": "Shared"}}'},
              TERMS_HEADER, {"doc.md": "AcmeWidgetUnit AcmeOtherUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_FINDING, "a collapse must be a finding")
    assert_in("NON-INJECTIVE", out, "the collapse must be named")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


def tracked_term_list_refuses_before_reading(tmp):
    s = build(tmp, {"m": ONE_MAP}, None, {"doc.md": "AcmeWidgetUnit\n"})
    write(os.path.join(tmp, "terms.md"), TERMS_HEADER)
    subprocess.call(["git", "add", "terms.md"], cwd=tmp,
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.call(["git", "commit", "-m", "oops"], cwd=tmp,
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    code, out, err = run(tmp, s, "--terms", "terms.md")
    assert_eq(code, EXIT_REFUSED, "a tracked term list must refuse")
    assert_in("TRACKED BY GIT", err, "the refusal must say why")


def out_dir_in_tracked_space_refuses(tmp):
    """The predicate is 'can git see it', not 'does git track it'.

    `git ls-files` only knows the index, so a NEW directory inside a tracked one passed the old
    check and the rule file - which names every identifier in the repository - landed one
    `git add -A` from being committed. All three forms below defeated the old check."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"docs/keep.md": "x\n"})
    for target in ("docs", "docs/scrub-out", "tools/scrub-out"):
        code, out, err = run(tmp, s, "--out", target)
        assert_eq(code, EXIT_REFUSED, "--out %s must refuse" % target)
        assert_in("NOT ignored by git", err, "the refusal for %s must say why" % target)
        assert not os.path.isfile(os.path.join(tmp, target, "replace-text.txt")), \
            "a refusal wrote an output file for %s" % target


# --------------------------------------------------------------------------- cannot run

def no_maps_is_exit_2(tmp):
    s = build(tmp, {}, TERMS_HEADER, {"doc.md": "x\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "no maps must be exit 2")
    assert_in("NOTHING EXAMINED", out, "exit 2 must say so")


def absent_term_list_is_exit_2(tmp):
    s = build(tmp, {"m": ONE_MAP}, None, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "an absent term list must be exit 2, never a partial run")
    assert_in("MISS 44", out, "the message must say why the maps alone are not enough")


def empty_term_list_is_exit_2(tmp):
    """A header with no rows. Distinct message from an ABSENT list: a list that shrank to nothing
    and a list nobody supplied are different problems and must not read the same."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_EMPTY, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a zero-row term list must be exit 2")
    assert_in("ZERO rows", out, "the empty case must name itself")


def malformed_row_is_exit_2_and_named(tmp):
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| onlytwo | cells |\n| ZZ1 | J1 | notaclass | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a malformed row must refuse, not skip")
    assert_in("malformed row", out, "the bad row must be named")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


def nothing_matching_is_exit_2_not_0(tmp):
    """EMPTY IS NOT CLEAN: every variant filtered out is a refusal, not a clean repository."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "nothing relevant here\n"})
    code, out, _ = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "no surviving rule must be exit 2")
    assert_in("EMPTY IS NOT CLEAN", out, "the principle must be named")


for name, body in [
    ("emits the three artifacts", emits_three_files),
    ("A8: lower-concatenated variant matches a hyphenated directory",
     a8_lower_concatenated_variant_matches_a_hyphenated_directory),
    ("a path-only occurrence still gets a rule", path_only_occurrence_still_gets_a_rule),
    ("a wide variant is reported but still emitted", a_wide_variant_is_reported_but_still_emitted),
    ("every line carries an explicit ==>", every_line_carries_an_explicit_arrow),
    ("longest-first ordering holds", longest_first_ordering_holds),
    ("a BOM'd map parses", a_bom_map_parses),
    ("identity mappings are dropped and counted", identity_mappings_are_dropped_and_counted),
    ("a non-standard section contributes", a_non_standard_section_contributes),
    ("a structured section is counted but ignored", a_structured_section_is_counted_but_ignored),
    ("a term row overrides a map key", a_term_row_overrides_a_map_key),
    ("a Green-present variant is withheld", green_present_variant_is_withheld),
    ("a DECLARED short term is emitted anyway", a_declared_short_term_is_emitted_anyway),
    ("a DECLARED Green-present term is emitted anyway",
     a_declared_green_present_term_is_emitted_anyway),
    ("a short key is withheld and counted", a_short_key_is_withheld_and_counted),
    ("a non-injective map refuses", non_injective_map_refuses),
    ("a tracked term list refuses before reading", tracked_term_list_refuses_before_reading),
    ("--out in tracked space refuses", out_dir_in_tracked_space_refuses),
    ("no maps is exit 2", no_maps_is_exit_2),
    ("an absent term list is exit 2", absent_term_list_is_exit_2),
    ("an empty term list does not crash", empty_term_list_is_exit_2),
    ("a malformed row is exit 2 and named", malformed_row_is_exit_2_and_named),
    ("nothing matching is exit 2, not 0", nothing_matching_is_exit_2_not_0),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
