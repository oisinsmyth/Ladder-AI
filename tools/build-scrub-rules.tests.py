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
import io, json, os, shutil, subprocess, sys, tempfile

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
    # Exception, not just AssertionError. A case that raises something unexpected - an IndexError
    # from parsing a rule line that no longer has the shape it assumed - used to kill the whole run
    # mid-way, so the suite printed NO RESULT LINE AT ALL. A harness that cannot tell "one case
    # failed" from "the run died" is the empty-is-not-clean failure wearing a test-runner costume,
    # and it was found by a mutation that made the suite vanish instead of turn red.
    except Exception as exc:
        failed += 1
        failures.append("%s: %s: %s" % (name, type(exc).__name__, exc))
        print("FAIL  %s" % name)
        print("      %s: %s" % (type(exc).__name__, exc))
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


def bury(tmp, rel, old, new):
    """Rewrite a file so OLD survives ONLY in an earlier commit — the corpus history mode exists for.

    *** THE EXACT OPPOSITE OF verify-scrub.tests.py's scrub(), AND THE DIFFERENCE IS THE POINT. ***
    That helper amends, expires the reflog and prunes, so the old blob is GONE from the object
    database. Here it must REMAIN in the object database while being absent from HEAD. So: edit,
    and make a SECOND commit. Never amend, never gc.

    Without this the suite structurally cannot test history mode. build() makes exactly one commit,
    so the ODB and HEAD hold identical blobs and the two scan modes are handed the same corpus -
    a `--scan history` case over that fixture would pass while proving nothing at all.
    """
    p = os.path.join(tmp, rel)
    write(p, io.open(p, encoding="utf-8").read().replace(old, new))
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "the identifier is gone from HEAD, not from history")


# DEVNULL, never PIPE. `subprocess.call` with a PIPE nobody reads deadlocks as soon as the child
# fills the buffer, and `git add` emits one CRLF warning per file.
def git(tmp, *args):
    return subprocess.call(["git"] + list(args), cwd=tmp,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def git_out(tmp, *args):
    return subprocess.run(["git"] + list(args), cwd=tmp, capture_output=True,
                          text=True, errors="replace").stdout


def run(tmp, script, *args):
    """NO --scan IS INJECTED HERE.

    It used to hard-code `--scan head`, and that single invisible word is the whole of finding F18:
    every case in this file ran the mode that is NOT the default, and the mode that produces the
    published artifact was entered by nothing. An override nobody can see at the call site is the
    kind that survives a review. Each case now names its mode through run_head or run_history.
    """
    proc = subprocess.Popen([sys.executable, script, "--repo", tmp] + list(args),
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=tmp)
    out, err = proc.communicate()
    return (proc.returncode,
            out.decode("utf-8", "replace"),
            err.decode("utf-8", "replace"))


def run_head(tmp, script, *args):
    return run(tmp, script, "--scan", "head", *args)


def run_history(tmp, script, *args):
    """The DEFAULT mode, named explicitly rather than left implicit — a case that relied on the
    default would go quiet the moment somebody changed it."""
    return run(tmp, script, "--scan", "history", *args)


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
    """FOUR artifacts, and each one NON-EMPTY.

    Measured by mutation, 2026-09-18: this case asserted existence only, so a tool emitting four
    empty files passed it - fourteen other cases went red and the one case NAMED for the artifacts
    did not. And `path-renames.args` was not checked here or anywhere else: suppressing it entirely
    turned 0 of 30 red, which is the same artifact whose total absence was defect F3.

    The fixture carries an identifier in a PATH as well as in content, so the rename file has
    something to contain; asserting non-empty on a file that is legitimately empty would be a
    different kind of useless."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER + "| ZZ1234 | JOB9001 | jobcode | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit is referenced here.\n",
               "gen/AcmeWidgetUnit/keep.md": "content\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    for name in ("replace-text.txt", "replace-message.txt", "manifest.json", "path-renames.args"):
        full = os.path.join(tmp, "sanitization", "scrub", name)
        assert os.path.isfile(full), "missing " + name
        assert os.path.getsize(full) > 0, "%s was written EMPTY - an empty rule set is not a run" % name


def a8_lower_concatenated_variant_matches_a_hyphenated_directory(tmp):
    """THE A8 REGRESSION. Asserted on the string a rule must match, never on the intent."""
    import re
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER,
              {"gen/acmewidgetunit-bench/notes.md": "see gen/acmewidgetunit-bench/x\n"})
    code, out, err = run_head(tmp, s)
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
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("gen/AcmeWidgetUnit/keep.md") for p in pats), \
        "a path-only occurrence produced no rule"


def a_wide_variant_is_reported_but_still_emitted(tmp):
    """Breadth is a REPORT. Withholding on it left 77 of 93 real paths unmatched when measured."""
    import re
    files = dict(("doc%02d.md" % i, "AcmeWidgetUnit\n") for i in range(40))
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, files)
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert_in("REPORT ONLY", out, "breadth must be reported")
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("AcmeWidgetUnit") for p in pats), "a wide variant was withheld, not reported"


def every_line_carries_an_explicit_arrow(tmp):
    """filter-repo's default replacement is ***REMOVED***. A line without `==>` substitutes that
    string into prose, silently, across the whole rewrite.

    Measured by mutation, 2026-09-18: this case SLEPT THROUGH THE REMOVAL OF `==>`. The emitter has
    two branches - a case-insensitive one for DECLARED terms and a boundary-anchored one for
    inferred map keys - and the old fixture produced no declared-term rule at all, so stripping the
    arrow from the declared branch went unnoticed here and was caught, by luck, somewhere else.
    A case named for "every line" must make every line exist. The declared term below therefore
    OCCURS in the corpus, and both branches are asserted present."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| AcmeDeclaredTerm | GenericDeclared | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and AcmeDeclaredTerm both appear\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    lines = rules_of(tmp)
    assert lines, "no rules emitted"
    assert any(l.startswith("regex:(?i)") for l in lines), \
        "no DECLARED-term rule emitted, so the case-insensitive branch is not under test here"
    assert any(l.startswith("regex:\\b") for l in lines), \
        "no inferred-key rule emitted, so the boundary-anchored branch is not under test here"
    for l in lines:
        assert "==>" in l, "line without an explicit replacement: %r" % l
        assert "REMOVED" not in l.split("==>", 1)[1], "default replacement leaked: %r" % l


def longest_first_ordering_holds(tmp):
    """Ordering matters because filter-repo applies rules in file order: a short rule that is a
    prefix of a long one fires first and the long one never matches what is left.

    Measured by mutation, 2026-09-18: this case DISCARDED the exit code and then asserted
    `lens == sorted(lens, reverse=True)` over whatever came back. With the emitter mutated to write
    an empty rule file it passed happily, because `[] == sorted([])` is true - a tool that exited 2
    and wrote nothing satisfied a case named for its ordering. Both holes are closed below: the
    exit code is checked, and there must be at least two rules for the ordering to be a claim at
    all."""
    import re
    maps = '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit", ' \
           '"AcmeWidgetUnitExtended": "GenericWidgetUnitExtended"}}'
    s = build(tmp, {"m": maps}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit and AcmeWidgetUnitExtended\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    lens = []
    for l in rules_of(tmp):
        esc = l.split("==>")[0][len("regex:") + 2:-2]
        lens.append(len(re.sub(r"\\(.)", r"\1", esc)))
    assert len(lens) >= 2, \
        "fewer than two rules emitted, so ORDERING IS NOT UNDER TEST HERE: %r" % lens
    assert lens == sorted(lens, reverse=True), "not longest-first: %r" % lens


def a_bom_map_parses(tmp):
    """25 of the 43 real maps carry a BOM; plain utf-8 dies on them."""
    s = build(tmp, {}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    write(os.path.join(tmp, "sanitization", "m.map.json"), ONE_MAP, encoding="utf-8-sig")
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "a BOM'd map must parse (%s%s)" % (out, err))
    assert rules_of(tmp), "BOM'd map contributed no rules"


def identity_mappings_are_dropped_and_counted(tmp):
    s = build(tmp, {"m": '{"Names": {"SameName": "SameName", "AcmeWidgetUnit": "GenericWidgetUnit"}}'},
              TERMS_HEADER, {"doc.md": "SameName AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("no-ops", out, "identity drops must be counted")
    assert not any("SameName" in l.split("==>")[0] for l in rules_of(tmp)), \
        "an identity mapping produced a rule"


def a_non_standard_section_contributes(tmp):
    """company/modelLine/identifiers are dropped by the C# loader and carry the site terms."""
    s = build(tmp, {"m": '{"company": {"AcmeHoldings": "GenericHoldings"}, "_purpose": "prose"}'},
              TERMS_HEADER, {"doc.md": "AcmeHoldings\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("company=1", out, "non-standard section must be counted as USED")
    assert rules_of(tmp), "non-standard section contributed no rule"


def a_structured_section_is_counted_but_ignored(tmp):
    """Counted AND ignored — the case asserted only the first half.

    Measured by mutation, 2026-09-18: feeding structured sections into the vocabulary turned
    0 of 30 red. The name of this case makes two claims and it tested one of them, so the half
    that actually protects the output was unguarded.

    A network comment is keyed `<block>#<n>` where n is the compile-unit index. That key is
    uncomputable from a flat file, so a text rule built from it would match nothing at best and
    something unintended at worst.

    The key below is deliberately a PLAIN TOKEN and deliberately PRESENT in the corpus. The first
    attempt at this repair used the realistic `<block>#1` form and stayed green under mutation,
    because `#` is outside the token class, so the key could never be found in the corpus and no
    rule could be emitted whether the section was ignored or not - a repair that was itself
    vacuous, caught by re-running the detector rather than by reading it."""
    s = build(tmp, {"m": '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit"}, '
                         '"Comments": {"AcmeStructuredKey": "text"}}'},
              TERMS_HEADER, {"doc.md": "AcmeWidgetUnit AcmeStructuredKey\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("comments=1", out, "ignored sections must be counted, not silent")
    assert not any("AcmeStructuredKey" in l for l in rules_of(tmp)), \
        "a STRUCTURED section contributed vocabulary to the text pass - it is keyed on parsed XML " \
        "structure and cannot be matched as text"


def a_term_row_overrides_a_map_key(tmp):
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| AcmeWidgetUnit | TermChosenName | block | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert any("TermChosenName" in l for l in rules_of(tmp)), "term row did not override the map"


def green_present_variant_is_withheld(tmp):
    """A second, Green-absent key is present deliberately: with only the withheld one, zero rules
    survive and the run is correctly exit 2, which would mask what this case is actually about."""
    maps = '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit", "AcmeSurvivor": "GenericSurvivor"}}'
    s = build(tmp, {"m": maps}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit AcmeSurvivor\n", "ir/reference/x.ir": "AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s, "--green", "ir/reference")
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
    code, out, _ = run_head(tmp, s)
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
    code, out, _ = run_head(tmp, s, "--green", "ir/reference")
    assert_eq(code, EXIT_OK, "exit (%s)" % out)
    assert any("AcmeDeclared" in l.split("==>")[0] for l in rules_of(tmp)), \
        "a declared term was withheld by the Green test"


def a_short_key_is_withheld_and_counted(tmp):
    s = build(tmp, {"m": '{"Names": {"Pump": "Mover", "AcmeWidgetUnit": "GenericWidgetUnit"}}'},
              TERMS_HEADER, {"doc.md": "Pump AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit")
    assert_in("under 8 chars", out, "short withholding must be counted")
    assert not any("Pump" in l.split("==>")[0] for l in rules_of(tmp)), "a 4-char key got a rule"


# ------------------------------------------------------------------- scan history
#
# FINDING F18. Until these existed, every case in this file ran `--scan head` and the DEFAULT mode
# - the one that produced the published artifact, 6,345 blobs and 119 rules - was entered by
# nothing at all. The whole of `all_blobs`'s streaming reader, its missing-object guard, and the
# second breadth pass were shipped unexecuted by any test.

def an_identifier_only_in_an_old_commit(tmp):
    """THE HEADLINE CASE, and the only one that proves the fixture works.

    One repository, two modes, OPPOSITE VERDICTS: head sees a clean tree and refuses with exit 2,
    history finds the identifier in a superseded blob and emits a rule. If these ever agree, the
    fixture has stopped exercising history and every case below it is worthless - so the
    disagreement is asserted, not assumed.

    This is the tool's entire reason for defaulting to history, quoted from its own docstring: "a
    key that occurs only in a commit from July is still in the artifact being published"."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit is here.\n"})
    bury(tmp, "doc.md", "AcmeWidgetUnit", "nothing identifying")

    head_code, head_out, _ = run_head(tmp, s)
    assert_eq(head_code, EXIT_CANNOT_RUN,
              "head must see a clean tree and refuse (%s)" % head_out)

    hist_code, hist_out, hist_err = run_history(tmp, s)
    assert_eq(hist_code, EXIT_OK, "history must find the buried identifier (%s%s)"
              % (hist_out, hist_err))
    assert any("AcmeWidgetUnit" in l.split("==>")[0] for l in rules_of(tmp)), \
        "history mode emitted no rule for an identifier that survives only in an old commit"
    assert head_code != hist_code, \
        "THE TWO MODES AGREED. The fixture is not exercising history and this case proves nothing."


def an_identifier_in_an_unreachable_object(tmp):
    """`--batch-all-objects` reads the object database, not the commit graph.

    A branch deleted without gc leaves its blobs unreachable but present, and they ship in a clone
    only if packed - but the point stands for the ARTIFACT being judged. The control assertion is
    what makes this case mean anything: without proving `rev-list --objects --all` cannot see the
    object, it would pass just as well against a reachable one."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"keep.md": "AcmeSurvivor\n"})
    git(tmp, "checkout", "-q", "-b", "doomed")
    write(os.path.join(tmp, "secret.md"), "AcmeWidgetUnit\n")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "on a branch about to be deleted")
    git(tmp, "checkout", "-q", "-")
    git(tmp, "branch", "-q", "-D", "doomed")

    reachable = git_out(tmp, "rev-list", "--objects", "--all")
    assert "secret.md" not in reachable, \
        "CONTROL FAILED: the object is still reachable, so this case does not test unreachability"

    code, out, err = run_history(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert any("AcmeWidgetUnit" in l.split("==>")[0] for l in rules_of(tmp)), \
        "an unreachable object's identifier got no rule - the ODB walk is not reaching it"


def a_non_utf8_blob_is_counted_unsearchable_not_clean(tmp):
    """A BLOB NOBODY COULD SEARCH MUST NEVER BE COUNTED AS CLEAN - the script says so itself.
    Exercises the UnicodeDecodeError arm, which only the history reader has."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    with io.open(os.path.join(tmp, "blob.bin"), "wb") as fh:
        fh.write(b"\xff\xfe\x00\x01 AcmeWidgetUnit \xc3\x28\xa0\xa1")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "a blob no text pass can read")

    code, out, err = run_history(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert_in("unsearchable:", out, "the unsearchable count must be reported")
    counted = int(out.split("unsearchable:")[1].split(")")[0].strip())
    assert counted >= 1, "a non-UTF-8 blob was not counted as unsearchable (got %d)" % counted


def a_corrupted_object_is_unsearchable_and_does_not_end_the_walk(tmp):
    """DEFECT F6'S SITE. `git cat-file --batch` prints `<oid> missing` for an object it cannot
    unpack, on stdout, WHILE EXITING 0. Breaking out of the loop there silently abandoned the rest
    of history while leaving a non-zero scanned count, so the empty-is-not-clean guard never fired.

    Measured for this fixture: a corrupted loose object produces exactly that line, and git still
    streams every other object afterwards. The identifier lives in a DIFFERENT file so that the
    run has something to find after the bad object."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit\n", "spoil.md": "this blob gets corrupted\n"})
    oid = git_out(tmp, "hash-object", "spoil.md").strip()
    loose = os.path.join(tmp, ".git", "objects", oid[:2], oid[2:])
    # NOT a silent skip. A case that quietly does nothing when its precondition fails is the exact
    # shape of failure this suite exists to catch: it would keep printing PASS while testing
    # nothing. Freshly committed objects are loose here; if a future git packs them on commit,
    # this must say so out loud and be rewritten, not pass by default.
    assert os.path.isfile(loose), \
        "precondition failed: the object is packed, not loose, so nothing was corrupted and this " \
        "case tested nothing"
    os.chmod(loose, 0o600)          # git writes loose objects read-only
    with io.open(loose, "wb") as fh:
        fh.write(b"garbage-not-zlib")

    code, out, err = run_history(tmp, s)
    assert_eq(code, EXIT_OK, "the walk must continue past an unreadable object (%s%s)" % (out, err))
    assert any("AcmeWidgetUnit" in l.split("==>")[0] for l in rules_of(tmp)), \
        "the walk stopped at the corrupted object and never reached the identifier"


def a_repository_with_no_blobs_is_exit_2(tmp):
    """EMPTY IS NOT CLEAN, and until this run the guard that says so COULD NOT FIRE.

    The path corpus was chained in with the blobs and is a str, never None, so `searched` was >= 1
    no matter what the object database did and `if not searched` at main() was unreachable dead
    code. A repository whose history holds not one blob reported "blobs scanned: 1" and carried on.

    The fixture keeps the worktree free of tracked files by excluding through .git/info/exclude
    rather than a tracked .gitignore - a tracked .gitignore would itself be a blob and defeat the
    whole point."""
    os.makedirs(os.path.join(tmp, "tools"))
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "build-scrub-rules.py"))
    write(os.path.join(tmp, "sanitization", "m.map.json"), ONE_MAP)
    write(os.path.join(tmp, "sanitization", "scrub-terms.md"), TERMS_HEADER)
    git(tmp, "init")
    git(tmp, "config", "user.email", "t@t")
    git(tmp, "config", "user.name", "t")
    write(os.path.join(tmp, ".git", "info", "exclude"), "sanitization/\ntools/\n")
    git(tmp, "commit", "--allow-empty", "-m", "a history with no blobs in it")
    s = os.path.join(tmp, "tools", "build-scrub-rules.py")

    code, out, _ = run_history(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a blobless history must refuse, not read as clean (%s)" % out)
    assert_in("NOTHING EXAMINED", out, "exit 2 must say what it did not examine")
    assert_in("blobs scanned (history)      : 0", out,
              "the count must be honest: the path corpus is not a blob")


def the_manifest_records_the_scan_mode(tmp):
    """Provenance. An artifact that does not say which corpus produced it cannot be audited later,
    and `head` and `history` produce legitimately different rule sets."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, err = run_history(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    manifest = json.loads(io.open(
        os.path.join(tmp, "sanitization", "scrub", "manifest.json"), encoding="utf-8").read())
    assert_eq(manifest.get("scan"), "history", "the manifest must record the mode it ran in")


def breadth_is_measured_at_head_even_when_scanning_history(tmp):
    """The second scan at main()'s breadth line, which ONLY history mode runs.

    Breadth asks "does this behave like an ordinary word rather than an identifier" by counting
    distinct files. Counted over history a file edited thirty times contributes thirty blobs, so an
    ordinary-looking count is manufactured by editing activity: measured on the real repository,
    the same vocabulary demoted 8 variants at HEAD and 22 over history, and those 14 extra
    demotions are rules NOT EMITTED.

    Here one file carrying the identifier is revised well past the threshold. Every revision is a
    separate blob in history; at HEAD it is still one file. The rule must survive."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit revision 0\n"})
    for i in range(1, 20):
        write(os.path.join(tmp, "doc.md"), "AcmeWidgetUnit revision %d\n" % i)
        git(tmp, "add", "-A")
        git(tmp, "commit", "-m", "revision %d" % i)

    code, out, err = run_history(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert any("AcmeWidgetUnit" in l.split("==>")[0] for l in rules_of(tmp)), \
        "breadth was counted over HISTORY: 20 revisions of one file demoted the identifier to " \
        "an ordinary word and the rule was never emitted"


# --------------------------------------------------- replacement overlap (F4)
#
# The check was `replacements & searchable` - an exact, case-SENSITIVE, whole-string set
# intersection. filter-repo does not apply rules that way: it applies them in file order, so what
# matters is whether one rule's needle MATCHES INSIDE another rule's replacement.
#
# And the case the old check could not see is the case the emitter deliberately creates. A DECLARED
# term is emitted anchorless and case-insensitive, on purpose, so it matches inside a word.

def an_equality_overlap_is_auto_substituted(tmp):
    """The shape the old check DID catch, now resolved rather than merely refused.

    A suffix works here and only here: `\\bGenericThing\\b` stops matching `GenericThing1`, because
    a digit is a word character and there is no longer a boundary after the name."""
    maps = '{"Names": {"AcmeWidgetUnit": "GenericThing", "GenericThing": "SomethingElse"}}'
    s = build(tmp, {"m": maps}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit and GenericThing both appear\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "an equality overlap should be substituted, not refused (%s%s)"
              % (out, err))
    assert_in("replacement overlap check", out, "the check must report what it did")
    line = [l for l in out.splitlines() if l.startswith("replacement overlap check")][0]
    assert "0 substituted" not in line, "the overlap was not detected at all: %r" % line
    assert_in("0 unresolved", line, "the substitution should have resolved it: %r" % line)


def a_declared_needle_INSIDE_a_replacement_GATES(tmp):
    """*** THE CASE THE OLD CHECK COULD NOT SEE, AND CANNOT BE SUBSTITUTED AWAY. ***

    The declared term `Generic` is emitted anchorless and case-insensitive, so it matches inside
    `GenericHolding` - the replacement chosen for another key. Whole-string intersection sees
    nothing, because the two strings are not equal.

    No suffix fixes this: appending to a name cannot remove a substring from it, and the anchorless
    rule matches `GenericHolding1` just as happily. So this gates, and the message says to choose a
    replacement by hand - which is the honest answer rather than a substitution that does not
    work."""
    s = build(tmp, {"m": '{"Names": {"AcmeWidgetUnit": "GenericHolding"}}'},
              TERMS_HEADER + "| Generic | Scrubbed | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and Generic both appear\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "a needle matching inside a replacement must gate (%s)" % out)
    assert_in("matched INSIDE", out, "the finding must name the actual relation")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


# ------------------------------------------------------ variant collisions (F9)
#
# `candidates.setdefault(v, key)` kept whichever key sorted first and DISCARDED THE REST IN SILENCE.
# Measured on the real corpus: 5 collisions, which is exactly the figure the review recorded and
# which nothing in the tool had ever printed. All five happen to be harmless there; the defect is
# that nobody could have known that.
#
# Every fixture below makes the two keys' own literal forms ABSENT from the corpus, so only the
# shared variant survives. Otherwise each key emits its own literal rule too and the NON-INJECTIVE
# gate fires first, which would test a different check entirely.

def a_harmless_collision_is_counted_not_discarded(tmp):
    """Two keys, one written form, the SAME replacement. Nothing goes wrong - and that is precisely
    why it has to be counted: silence here is indistinguishable from silence over a collision that
    collapses two identifiers."""
    s = build(tmp, {"m": '{"Names": {"AcmeWidget": "GenericThing", "Acme_Widget": "GenericThing"}}'},
              TERMS_HEADER, {"doc.md": "see acme-widget here\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "a same-replacement collision is harmless (%s%s)" % (out, err))
    # The two keys share FOUR written forms - acmewidget, acme-widget, acme_widget, ACME_WIDGET -
    # because split_camel normalises `_` and camel humps to the same parts. Assert the shape rather
    # than a hard-coded total, but assert that SOMETHING was counted: zero here is the old silence.
    line = [l for l in out.splitlines() if l.startswith("variant collisions")]
    assert line, "no collision line printed at all:\n%s" % out
    n = int(line[0].split(":")[1].split("(")[0].strip())
    assert n >= 1, "a real collision was counted as zero: %r" % line[0]
    assert_in("0 unresolved", line[0], "a same-replacement collision must not be a problem")


def a_collision_with_DIFFERENT_replacements_GATES(tmp):
    """THE CASE THE OLD CODE COULD NOT REACH. Two live identifiers, one written form, two different
    invented names - so whichever is emitted, the other identifier is rewritten to a name that is
    not its own. The NON-INJECTIVE gate exists to stop exactly this and cannot see it, because the
    losing key never reaches `rules`."""
    s = build(tmp, {"m": '{"Names": {"AcmeWidget": "GenericOne", "Acme_Widget": "GenericTwo"}}'},
              TERMS_HEADER, {"doc.md": "see acme-widget here\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "an unresolved collision must gate (%s)" % out)
    assert_in("disagree on the replacement", out, "the finding must say what is wrong")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


def a_DECLARED_term_wins_a_collision_and_keeps_its_anchorless_rule(tmp):
    """*** THE SERIOUS CONSEQUENCE, ASSERTED. ***

    `declared` is evaluated on whichever key WON the variant. A map key sorting ahead of a declared
    term therefore took the form over, and it was emitted with the narrow case-sensitive `\\b` rule
    instead of the anchorless `(?i)` one - silently reintroducing F1 through a path that neither the
    length-floor guard nor the Green-test guard covers.

    `AcmeWidget` (map) sorts before `Acme_Widget` (declared) and would have won."""
    s = build(tmp, {"m": '{"Names": {"AcmeWidget": "GenericMap"}}'},
              TERMS_HEADER + "| Acme_Widget | GenericTerm | site | global | auto |\n",
              {"doc.md": "see acme-widget here\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    # re.escape escapes the hyphen, so the needle reads `acme\-widget` in the file. Compare with the
    # escaping removed rather than against a literal that only looks right.
    hit = [l for l in rules_of(tmp) if "acme-widget" in l.split("==>")[0].replace("\\", "")]
    assert hit, "no rule for the contested form at all: %r" % rules_of(tmp)
    assert all("GenericTerm" in l.split("==>", 1)[1] for l in hit), \
        "a map key took a DECLARED term's written form: %r" % hit
    assert all(l.startswith("regex:(?i)") for l in hit), \
        "the declared term's form lost its anchorless case-insensitive rule: %r" % hit


# ----------------------------------------------------------- section class (F11)
#
# A key's map SECTION is the only statement anyone makes about what kind of name it is, and
# load_maps threw it away at the point it was read. Every map key therefore reached variants_for as
# "block", which is not in SPACED_CLASSES, so NO MAP KEY COULD EVER GET A SPACED FORM - and a
# spaced form is the only way a site or site name is written in prose.
#
# Measured on the real corpus after the fix: 14 keys gain a section class, 4 of them would gain a
# spaced variant, and ALL FOUR are already in the owner's term list with a spaced class. So the
# artifact does not move here. The defect is real and the corpus happens not to expose it - which
# is why these cases carry the proof instead.

def a_company_section_key_gets_its_spaced_form(tmp):
    """THE F11 CASE. `AcmeHoldings` in a `company` section, written as `Acme Holdings` in prose."""
    import re
    s = build(tmp, {"m": '{"company": {"AcmeHoldings": "GenericHoldings"}}'},
              TERMS_HEADER, {"doc.md": "the site is operated by Acme Holdings plc\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("the site is operated by Acme Holdings plc") for p in pats), \
        "no emitted rule matches the SPACED form - the section class is being discarded again"


def the_same_key_under_names_gets_NO_spaced_form(tmp):
    """The control for the case above, and without it that case proves only that some rule matched.

    A block name is not written with spaces in prose, so `names` must NOT produce a spaced form.
    If this ever passes AND the case above passes, spaced forms are being handed out unconditionally
    and the section class is once again doing nothing."""
    import re
    s = build(tmp, {"m": '{"Names": {"AcmeHoldings": "GenericHoldings"}}'},
              TERMS_HEADER, {"doc.md": "AcmeHoldings and also Acme Holdings appear\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    pats = [re.compile(l.split("==>")[0][len("regex:"):]) for l in rules_of(tmp)]
    assert any(p.search("AcmeHoldings") for p in pats), "the concatenated form must still match"
    assert not any(p.search("operated by Acme Holdings plc") for p in pats), \
        "a `names` key got a spaced form - the section class is not being consulted"


def a_section_class_does_NOT_make_a_map_key_DECLARED(tmp):
    """*** THE TRAP IN FIXING F11, ASSERTED. ***

    `klass_of` does not mean "this key's class". It means THE OWNER WROTE THIS KEY DOWN, and three
    other decisions read it that way: a declared key bypasses the length floor, bypasses the Green
    test, and is emitted anchorless and case-insensitive. Giving map keys a class by putting them
    into `klass_of` would have promoted ~1,390 inferred keys to declared status - a far larger
    change than the fix, wearing the costume of a one-line edit.

    `Pump` is four characters and lives in a `company` section. It must still be withheld by the
    length floor, and the declared-row count must not move."""
    s = build(tmp, {"m": '{"company": {"Pump": "Mover", "AcmeHoldings": "GenericHoldings"}}'},
              TERMS_HEADER, {"doc.md": "Pump and Acme Holdings\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s)" % out)
    assert_in("under 8 chars", out, "the length floor must still apply to a section-classed map key")
    assert not any("Pump" in l.split("==>")[0] for l in rules_of(tmp)), \
        "a section-classed map key bypassed the length floor - it was treated as DECLARED"
    # The fixture's only term row does not occur in the corpus, so the declared count is ZERO and
    # every emitted rule comes from the map. That is the assertion: a section-classed map key is
    # counted as INFERRED, and a non-zero number here would mean the class had promoted it.
    assert_in("from DECLARED term rows      : 0", out,
              "a section-classed map key was counted as DECLARED (%s)" % out)


# ------------------------------------------------------- conflict resolution (F13)
#
# Until these existed, `resolve_conflict` had NEVER RUN past its first line in any test. Every
# fixture in this file built a single map, so `len(candidates) == 1` always and the function
# returned rung 0 immediately. Rungs 1 through 4 - the entire ladder that decides which invented
# name a contested key gets - were unexecuted, and F13 lived in rung 3 undisturbed.

def one_map_cannot_outvote_two(tmp):
    """F13. The vote list is appended to once per (SECTION, key), not once per FILE.

    So a single map declaring the same key in `names`, `tags` and `company` cast three votes for
    its own replacement and beat two maps that each said so once. Rung 3 asks "how many maps
    agree", and it was counting how many times one map repeated itself.

    alpha says GenericAaa three times over; beta and gamma each say GenericBbb once. Counting
    occurrences: 3 to 2, alpha wins. Counting SOURCES: 1 to 2, and the two maps win."""
    maps = {
        "alpha": '{"Names": {"AcmeWidgetUnit": "GenericAaa"}, '
                 ' "Tags": {"AcmeWidgetUnit": "GenericAaa"}, '
                 ' "company": {"AcmeWidgetUnit": "GenericAaa"}}',
        "beta": '{"Names": {"AcmeWidgetUnit": "GenericBbb"}}',
        "gamma": '{"Names": {"AcmeWidgetUnit": "GenericBbb"}}',
    }
    s = build(tmp, maps, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert_in("rung3=1", out, "the two agreeing maps must win at rung 3 (%s)" % out)
    assert any("GenericBbb" in l.split("==>", 1)[1] for l in rules_of(tmp)), \
        "one map outvoted two by repeating itself across sections"


def a_real_majority_still_wins_at_rung_3(tmp):
    """The other direction, and the reason this is not just `len(set(...))` applied blindly: a
    genuine majority of DISTINCT maps must still carry rung 3. Without this case, deleting rung 3
    altogether would pass the case above."""
    maps = {
        "alpha": '{"Names": {"AcmeWidgetUnit": "GenericAaa"}}',
        "beta": '{"Names": {"AcmeWidgetUnit": "GenericBbb"}}',
        "gamma": '{"Names": {"AcmeWidgetUnit": "GenericBbb"}}',
    }
    s = build(tmp, maps, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert_in("rung3=1", out, "two distinct maps against one must resolve at rung 3 (%s)" % out)
    assert any("GenericBbb" in l.split("==>", 1)[1] for l in rules_of(tmp)), \
        "the genuine majority lost"


def a_tie_falls_to_rung_4_and_is_reported(tmp):
    """Rung 4 is arbitrary by design and the run log names what fell to it, so the arbitrariness
    stays visible rather than looking like a decision."""
    maps = {
        "alpha": '{"Names": {"AcmeWidgetUnit": "GenericAaa"}}',
        "beta": '{"Names": {"AcmeWidgetUnit": "GenericBbb"}}',
    }
    s = build(tmp, maps, TERMS_HEADER, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    assert_in("rung4=1", out, "an even split must fall to rung 4 and say so (%s)" % out)
    manifest = json.loads(io.open(
        os.path.join(tmp, "sanitization", "scrub", "manifest.json"), encoding="utf-8").read())
    assert_eq(manifest.get("rung4Keys"), 1, "the manifest must record what fell to the arbitrary rung")


# --------------------------------------------------------------------------- refuse

def non_injective_map_refuses(tmp):
    s = build(tmp, {"m": '{"Names": {"AcmeWidgetUnit": "Shared", "AcmeOtherUnit": "Shared"}}'},
              TERMS_HEADER, {"doc.md": "AcmeWidgetUnit AcmeOtherUnit\n"})
    code, out, _ = run_head(tmp, s)
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
    code, out, err = run_head(tmp, s, "--terms", "terms.md")
    assert_eq(code, EXIT_REFUSED, "a tracked term list must refuse")
    assert_in("TRACKED BY GIT", err, "the refusal must say why")


def out_dir_in_tracked_space_refuses(tmp):
    """The predicate is 'can git see it', not 'does git track it'.

    `git ls-files` only knows the index, so a NEW directory inside a tracked one passed the old
    check and the rule file - which names every identifier in the repository - landed one
    `git add -A` from being committed. All three forms below defeated the old check."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"docs/keep.md": "x\n"})
    for target in ("docs", "docs/scrub-out", "tools/scrub-out"):
        code, out, err = run_head(tmp, s, "--out", target)
        assert_eq(code, EXIT_REFUSED, "--out %s must refuse" % target)
        assert_in("NOT ignored by git", err, "the refusal for %s must say why" % target)
        assert not os.path.isfile(os.path.join(tmp, target, "replace-text.txt")), \
            "a refusal wrote an output file for %s" % target


# --------------------------------------------------------------------------- cannot run

def no_maps_is_exit_2(tmp):
    s = build(tmp, {}, TERMS_HEADER, {"doc.md": "x\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "no maps must be exit 2")
    assert_in("NOTHING EXAMINED", out, "exit 2 must say so")


def absent_term_list_is_exit_2(tmp):
    s = build(tmp, {"m": ONE_MAP}, None, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "an absent term list must be exit 2, never a partial run")
    assert_in("MISS 44", out, "the message must say why the maps alone are not enough")


def empty_term_list_is_exit_2(tmp):
    """A header with no rows. Distinct message from an ABSENT list: a list that shrank to nothing
    and a list nobody supplied are different problems and must not read the same."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_EMPTY, {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a zero-row term list must be exit 2")
    assert_in("ZERO rows", out, "the empty case must name itself")


def malformed_row_is_exit_2_and_named(tmp):
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| onlytwo | cells |\n| ZZ1 | J1 | notaclass | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "a malformed row must refuse, not skip")
    assert_in("malformed row", out, "the bad row must be named")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


def nothing_matching_is_exit_2_not_0(tmp):
    """EMPTY IS NOT CLEAN: every variant filtered out is a refusal, not a clean repository."""
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER, {"doc.md": "nothing relevant here\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "no surviving rule must be exit 2")
    assert_in("EMPTY IS NOT CLEAN", out, "the principle must be named")


def two_rows_declaring_the_same_term_DIFFERENTLY_gates(tmp):
    """*** F9 AGAIN, IN A DIFFERENT DICT, AND LIVE IN THE REAL TERM LIST. ***

    `chosen[r["live"]] = r["invented"]` is a plain assignment in row order, so a term declared twice
    keeps whichever row is last - no counter, no finding, nothing in the manifest. F9 was the same
    failure in `candidates.setdefault`. Found by measurement rather than by reading: the real list
    declares one 5-character term as both `site` and `jobcode`, and the row that loses carries the
    bare token `None` as its replacement. Reordering the table would have rewritten a site name to
    `None` throughout the corpus in silence."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Zorbex | SiteAlpha | site | global | auto |\n"
                             "| Zorbex | JOB7777 | jobcode | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and Zorbex appear\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "disagreeing duplicate rows must gate (%s)" % out)
    assert_in("DISAGREE on the replacement", out, "the finding must name the relation")
    assert_in("SiteAlpha", out, "both replacements must be named so the owner can pick")
    assert_in("JOB7777", out, "both replacements must be named so the owner can pick")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


def two_rows_declaring_the_same_term_IDENTICALLY_is_reported_not_refused(tmp):
    """A duplicate that changes nothing is a tidiness problem, and refusing it would be this tool
    deciding how the owner keeps their own table. It is counted so it cannot hide.

    The fixture is a literally repeated row - the copy-paste shape - rather than two spellings of
    one term. Two SPELLINGS that agree are a different situation and the existing NON-INJECTIVE
    check already refuses them, because they are two distinct keys collapsing onto one replacement.
    That refusal is loud and pre-dates this change, so it is left alone rather than folded in
    here: this check exists to stop a SILENT choice, and there is nothing silent about it."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Zorbex | SiteAlpha | site | global | auto |\n"
                             "| Zorbex | SiteAlpha | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and Zorbex appear\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "agreeing duplicates must not gate (%s%s)" % (out, err))
    assert_in("1 agreeing (reported)", out, "the redundant row must be counted")
    assert "DISAGREE on the replacement" not in out, "agreement must not be reported as conflict"


def duplicate_detection_is_CASE_INSENSITIVE(tmp):
    """The emitted rule is `(?i)`, so two rows differing only in capitalisation ARE one declaration
    however the table looks. Comparing them case-sensitively would let the disagreement through in
    the one spelling somebody was most likely to use by accident."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Zorbex | SiteAlpha | site | global | auto |\n"
                             "| zorbex | JOB7777 | jobcode | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and Zorbex appear\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "a case-only duplicate must still gate (%s)" % out)
    assert_in("DISAGREE on the replacement", out, "the finding must name the relation")


# ---- CUTS: a declared term's anchorless rule editing source code from inside a token -------------
# The four cases below are one discrimination, and each half has to be proved or the check is
# unfalsifiable. A declared term is emitted `(?i)<term>` with no word boundary BECAUSE four real
# terms occur only inside larger job tokens and a \b rule misses them (A8 in mirror image). The same
# anchoring applied to a term that lives only inside larger SOURCE tokens cuts symbols in half: one
# three-character dotted term spanned a member access in four files and took three solutions from
# 5,894 passing tests to 2,196. Length does not separate those two populations - measured on the
# real vocabulary, a 3-character term and a legitimate 5-character job code both had ~12 enclosing
# tokens. Being whole in source somewhere does.

def a_declared_term_that_only_cuts_source_tokens_GATES(tmp):
    """The build-breaker's shape. `Pq` appears nowhere as a source token of its own - only inside
    `PqValue` - so every edit its rule makes in Prog.cs is to the inside of somebody else's
    identifier, and none of them hides anything. That is the case that must refuse."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit appears here\n",
               "Prog.cs": "class C { int PqValue; }\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "a declared term cutting only source tokens must gate (%s)" % out)
    assert_in("cuts identifiers in half", out, "the finding must name the mechanism")
    assert_in("Scrubbed", out, "the finding must name the ROW, by its invented replacement")
    # An invented replacement is NOT unique - the real list has two rows sharing one, being two
    # spellings of a single site. Class and length disambiguate them while disclosing nothing.
    assert_in("(site, 2 characters)", out,
              "the finding must carry enough to pick the row out when a replacement is shared")
    assert_eq(rules_of(tmp), [], "a refusal must write nothing")


def the_CUT_TOKENS_are_NOT_printed_without_the_flag(tmp):
    """*** A RECORD OF A LEAK MUST NOT BE A COPY OF IT. *** The finding names the row by its
    INVENTED replacement, which is safe vocabulary this tool made up, and stops there. A refusal
    that printed the live forms would put them in every terminal, pipeline and pasted note that a
    failing build touches - and this gate fires precisely on the terms nobody has sanitised yet."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit appears here\n",
               "Prog.cs": "class C { int PqValue; }\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "exit (%s)" % out)
    assert "pqvalue" not in out.lower(), "the corrupted token was printed by default: %r" % out
    assert_in("--explain-cuts", out, "the refusal must say how to see them")


def the_CUT_TOKENS_ARE_printed_with_the_flag(tmp):
    """The other half. A disclosure flag that discloses nothing is worse than no flag: the owner
    reads the instruction, runs it, sees no more than before, and concludes the tool is broken -
    or worse, that there is nothing there."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and PqUnitTag appear\n",
               "Prog.cs": "class C { int PqValue; }\n"})
    code, out, _ = run_head(tmp, s, "--explain-cuts")
    assert_eq(code, EXIT_FINDING, "the flag must not change the verdict (%s)" % out)
    assert_in("pqvalue", out.lower(), "--explain-cuts must print what the rule would corrupt")
    assert_in("pqunittag", out.lower(),
              "--explain-cuts must also print the NON-source forms - that is where variants come "
              "from, and a list of only the collateral cannot be chosen from")


def a_substitution_propagates_INTO_dotted_replacements(tmp):
    """*** ONE LIVE NAME MUST NOT LEAVE THE REWRITE AS TWO. ***

    `AcmeWidgetUnit` maps to `GenericThing`, and `AcmeWidgetUnit.Flag` maps to `GenericThing.Flag` -
    the same invented head, written twice, which is exactly the shape a map's Names and Tags
    sections produce. The overlap check then has to substitute `GenericThing`, because another
    rule's needle matches inside it. If that substitution rewrites only the WHOLE replacement, the
    bare rule starts saying `GenericThing1` while the dotted one still says `GenericThing`, and any
    file that composes the dotted form from its parts ends up with halves that no longer agree.

    This is not hypothetical and it is not caught by the identifier half of Gate 3. It reached a
    published artifact: 5 disagreements, 8 broken converter tests, found only by the build
    comparison."""
    maps = ('{"Names": {"AcmeWidgetUnit": "GenericThing", "GenericThing": "SomethingElse"},'
            ' "Tags": {"AcmeWidgetUnit.Flag": "GenericThing.Flag"}}')
    s = build(tmp, {"m": maps}, TERMS_HEADER,
              {"doc.md": "AcmeWidgetUnit and AcmeWidgetUnit.Flag and GenericThing all appear\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "exit (%s%s)" % (out, err))
    reps = [l.split("==>", 1)[1] for l in rules_of(tmp)]
    bare = [r for r in reps if r.startswith("GenericThing") and "." not in r]
    dotted = [r for r in reps if r.startswith("GenericThing") and "." in r]
    assert bare and dotted, "fixture did not produce both a bare and a dotted replacement: %r" % reps
    heads = {r.split(".")[0] for r in dotted} | set(bare)
    assert len(heads) == 1, \
        "the substitution left the head spelled two ways - one live name, two invented: %r" % heads


def a_token_a_LONGER_rule_rewrites_FIRST_is_not_collateral(tmp):
    """*** RULES ARE APPLIED LONGEST-FIRST, SO MEASURING A NEEDLE ALONE OVERSTATES ITS REACH. ***

    `PqV` is declared and IS a whole token in source, so its rule is load-bearing and it does not
    gate. It is also longer than `Pq`, so it is emitted first and rewrites `PqValue` before the
    shorter rule ever sees it - after which `Pq` matches nothing there at all.

    Measured on the real vocabulary before this was fixed: a 3-character modelline term is a prefix
    of a 4-character one with its own row, and the check named the one build-source token that
    ordering had already made unreachable. It reached the right verdict for that row by the wrong
    reason, which is the kind of finding that gets argued with - and then disbelieved on the day it
    is right."""
    maps = '{"Names": {"AcmeWidgetUnit": "GenericWidgetUnit"}}'
    s = build(tmp, {"m": maps},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | auto |\n"
                             "| PqV | Scrub2 | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit appears here\n",
               "Prog.cs": "class C { int PqValue; string s = \"PqV\"; }\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK,
              "a token a longer rule rewrites first is not collateral (%s%s)" % (out, err))
    assert_in("and never matching one whole : 0", out,
              "the shorter needle must contribute NO collateral once ordering is accounted for")


def a_variants_cell_of_only_ABSENT_forms_emits_no_rule_and_does_not_gate(tmp):
    """*** THE REMEDY FOR A ROW WITH NOTHING TO SCRUB, AND THE README NOW RECOMMENDS IT. ***

    Some rows gate with an EMPTY candidate list: every token containing the term is build-source
    collateral, so the term has no legitimate written form in the repository and its rule is pure
    damage. Deleting the row fixes it and throws away the owner's declaration. The alternative is a
    `variants` cell naming a form that does not occur here - the forced list REPLACES the derived
    one, the named form is dropped as dead, no rule is emitted, and the declaration stays on record
    for the next corpus.

    That is a real behaviour with three moving parts (forced list replaces; absent variant is
    dropped; a row emitting nothing cannot reach the CUTS check), and a recommendation resting on
    three untested interactions is a recommendation resting on nothing."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | QQ-NO-SCRUBBABLE-FORM-QQ |\n",
              {"doc.md": "AcmeWidgetUnit appears here\n",
               "Prog.cs": "class C { int PqValue; }\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "an absent-form variants cell must discharge the row (%s%s)"
              % (out, err))
    needles = [l[len("regex:"):].split("==>")[0].replace("\\", "") for l in rules_of(tmp)]
    assert not any("Pq" in n for n in needles), \
        "the row still emitted a rule - a forced list must REPLACE the derived one: %r" % needles
    assert not any("QQ-NO-SCRUBBABLE-FORM-QQ" in n for n in needles), \
        "an absent variant must be dropped as dead, not emitted: %r" % needles
    assert_in("absent from this repository", out, "the drop must be counted, not silent")


def a_declared_term_embedded_only_in_JOB_tokens_does_NOT_gate(tmp):
    """*** THE A8 HALF, AND THE REASON THIS CANNOT GATE ON LENGTH OR ON EMBEDDING ALONE. ***

    The same two-character term, embedded exactly as often - but in a .md file rather than build
    source. The anchorless rule is doing precisely the job it was given an anchorless form for, and
    a check that refuses here has re-broken A8 to fix its mirror image."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit and PqUnitTag appear here\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "embedding in NON-source must not gate (%s%s)" % (out, err))
    assert any(l.split("==>")[0].endswith("Pq") for l in rules_of(tmp)), \
        "the declared term got no rule - the cuts check is eating the A8 population"


def a_declared_term_ALSO_whole_in_source_does_NOT_gate(tmp):
    """The other escape, and it is a report rather than a refusal. Here `Pq` IS a source token in
    its own right, so its rule is load-bearing in Prog.cs and the incidental hit inside `PqValue`
    rides along with work that genuinely de-identifies. Gating this would refuse the ordinary case
    of a job code that appears in a test fixture."""
    s = build(tmp, {"m": ONE_MAP},
              TERMS_HEADER + "| Pq | Scrubbed | site | global | auto |\n",
              {"doc.md": "AcmeWidgetUnit appears here\n",
               "Prog.cs": "class C { int PqValue; string s = \"Pq\"; }\n"})
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "whole-in-source must not gate (%s%s)" % (out, err))
    assert_in("but also present whole", out, "the report-only population must be counted")


def an_explicit_variants_cell_discharges_the_cuts_finding(tmp):
    """*** THE ESCAPE HATCH, AND NOTHING IN THIS SUITE EXERCISED IT BEFORE. ***

    The `variants` column has parsed since load_terms was written and every fixture said `auto`, so
    the one column the new finding tells the owner to reach for had no test at all. A forced list
    REPLACES the auto-derivation rather than adding to it - variants_for puts the literal key first
    and a forced list does not put it back - which is exactly why naming the safe forms stops the
    dangerous bare form being emitted. That behaviour is now pinned.

    IT RUNS THE SAME FIXTURE TWICE, and that is the point rather than thoroughness. Asserting only
    that the second run is clean passes just as happily when the check has been deleted - the first
    version of this case survived a mutation that disabled the gate outright, proving an escape
    hatch on a door that was already open. Refusing first and passing second is the only shape that
    shows THE CELL is what changed the outcome."""
    row = "| Pq | Scrubbed | site | global | %s |\n"
    s = build(tmp, {"m": ONE_MAP}, TERMS_HEADER + row % "auto",
              {"doc.md": "AcmeWidgetUnit and PqExplicitForm appear here\n",
               "Prog.cs": "class C { int PqValue; }\n"})
    code, out, _ = run_head(tmp, s)
    assert_eq(code, EXIT_FINDING, "the fixture must gate BEFORE the variants cell (%s)" % out)

    write(os.path.join(tmp, "sanitization", "scrub-terms.md"),
          TERMS_HEADER + row % "PqExplicitForm")
    code, out, err = run_head(tmp, s)
    assert_eq(code, EXIT_OK, "an explicit variants cell must discharge the finding (%s%s)"
              % (out, err))
    needles = [l[len("regex:"):].split("==>")[0].replace("\\", "") for l in rules_of(tmp)]
    assert "(?i)Pq" not in needles, \
        "the bare short form was emitted anyway - a forced variants list must REPLACE the auto list"
    assert any(n.endswith("PqExplicitForm") for n in needles), \
        "the named variant got no rule - the forced list was ignored"


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
    ("HISTORY: an identifier only in an old commit", an_identifier_only_in_an_old_commit),
    ("HISTORY: an identifier in an unreachable object", an_identifier_in_an_unreachable_object),
    ("HISTORY: a non-UTF-8 blob is unsearchable, not clean",
     a_non_utf8_blob_is_counted_unsearchable_not_clean),
    ("HISTORY: a corrupted object does not end the walk",
     a_corrupted_object_is_unsearchable_and_does_not_end_the_walk),
    ("HISTORY: a repository with no blobs is exit 2", a_repository_with_no_blobs_is_exit_2),
    ("HISTORY: the manifest records the scan mode", the_manifest_records_the_scan_mode),
    ("HISTORY: breadth is measured at head", breadth_is_measured_at_head_even_when_scanning_history),
    ("OVERLAP: an equality overlap is auto-substituted", an_equality_overlap_is_auto_substituted),
    ("OVERLAP: a declared needle INSIDE a replacement GATES",
     a_declared_needle_INSIDE_a_replacement_GATES),
    ("COLLISION: a harmless collision is counted, not discarded",
     a_harmless_collision_is_counted_not_discarded),
    ("COLLISION: different replacements GATES", a_collision_with_DIFFERENT_replacements_GATES),
    ("COLLISION: a DECLARED term wins and keeps its anchorless rule",
     a_DECLARED_term_wins_a_collision_and_keeps_its_anchorless_rule),
    ("SECTION: a company key gets its spaced form", a_company_section_key_gets_its_spaced_form),
    ("SECTION: a names key gets NO spaced form", the_same_key_under_names_gets_NO_spaced_form),
    ("SECTION: a section class does NOT make a map key declared",
     a_section_class_does_NOT_make_a_map_key_DECLARED),
    ("CONFLICT: one map cannot outvote two", one_map_cannot_outvote_two),
    ("CONFLICT: a real majority still wins at rung 3", a_real_majority_still_wins_at_rung_3),
    ("CONFLICT: a tie falls to rung 4 and is reported", a_tie_falls_to_rung_4_and_is_reported),
    ("a non-injective map refuses", non_injective_map_refuses),
    ("a tracked term list refuses before reading", tracked_term_list_refuses_before_reading),
    ("--out in tracked space refuses", out_dir_in_tracked_space_refuses),
    ("no maps is exit 2", no_maps_is_exit_2),
    ("an absent term list is exit 2", absent_term_list_is_exit_2),
    ("an empty term list does not crash", empty_term_list_is_exit_2),
    ("a malformed row is exit 2 and named", malformed_row_is_exit_2_and_named),
    ("nothing matching is exit 2, not 0", nothing_matching_is_exit_2_not_0),
    ("DUPLICATE: two rows declaring the same term DIFFERENTLY gates",
     two_rows_declaring_the_same_term_DIFFERENTLY_gates),
    ("DUPLICATE: two rows declaring it IDENTICALLY is reported",
     two_rows_declaring_the_same_term_IDENTICALLY_is_reported_not_refused),
    ("DUPLICATE: detection is case-insensitive", duplicate_detection_is_CASE_INSENSITIVE),
    ("CUTS: a declared term that only cuts source tokens GATES",
     a_declared_term_that_only_cuts_source_tokens_GATES),
    ("SUBST: a substitution propagates INTO dotted replacements",
     a_substitution_propagates_INTO_dotted_replacements),
    ("CUTS: a token a LONGER rule rewrites first is not collateral",
     a_token_a_LONGER_rule_rewrites_FIRST_is_not_collateral),
    ("CUTS: a variants cell of only ABSENT forms emits no rule and does not gate",
     a_variants_cell_of_only_ABSENT_forms_emits_no_rule_and_does_not_gate),
    ("CUTS: the cut tokens are NOT printed without the flag",
     the_CUT_TOKENS_are_NOT_printed_without_the_flag),
    ("CUTS: the cut tokens ARE printed with --explain-cuts",
     the_CUT_TOKENS_ARE_printed_with_the_flag),
    ("CUTS: embedded only in JOB tokens does NOT gate",
     a_declared_term_embedded_only_in_JOB_tokens_does_NOT_gate),
    ("CUTS: also whole in source does NOT gate",
     a_declared_term_ALSO_whole_in_source_does_NOT_gate),
    ("CUTS: an explicit variants cell discharges the finding",
     an_explicit_variants_cell_discharges_the_cuts_finding),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
