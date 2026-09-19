#!/usr/bin/env python3
"""Self-tests for check-green-claims.py (M-22).

    python tools/check-green-claims.tests.py

EVERY CASE OWNS A THROWAWAY REPOSITORY. The subject under test is tracked content at HEAD, and
there is no honest way to fake that without a repository. The fixture copies THREE scripts: the
checker, the oracle it derives its vocabulary from, and the builder it reads the --green default
out of. A repo with only the checker in it tests a different program.

THE FIXTURE USES THE REAL --green DIRECTORY NAMES on purpose. Those names are the de-identified
sandbox and the purpose-built reference corpus, so they carry nothing, and using them means the
tests exercise the REAL default list rather than a stub the tool would never meet.

NOTHING HERE PLANTS A REAL IDENTIFIER. Invented vocabulary throughout - which is also the only way
this suite could be committed at all.
"""
import io
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "check-green-claims.py")
ORACLE = os.path.join(HERE, "verify-scrub.py")
BUILDER = os.path.join(HERE, "build-scrub-rules.py")

EXIT_OK = 0
EXIT_FOUND = 1
EXIT_CANNOT_RUN = 2
EXIT_REFUSED = 3

# The real --green default, which the fixture reproduces as directories.
GREEN = ["ir/reference", "ir/test-project001", "gen/test-project001",
         "simatic-ml/reference", "simatic-ml/test-project001"]

passed, failed, failures = 0, 0, []


def case(name, body):
    global passed, failed
    tmp = tempfile.mkdtemp()
    try:
        body(tmp)
    except Exception as exc:                                # not just AssertionError - see below
        # A case that raises something unexpected must fail as a CASE, not kill the run. A harness
        # that cannot tell "one case failed" from "the run died" prints no result line at all,
        # which is the empty-is-not-clean failure wearing a test-runner costume.
        failed += 1
        failures.append("%s: %s: %s" % (name, type(exc).__name__, exc))
        print("FAIL  %s" % name)
        print("      %s: %s" % (type(exc).__name__, exc))
    else:
        passed += 1
        print("PASS  %s" % name)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def write(path, text, binary=False):
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    if binary:
        io.open(path, "wb").write(text)
    else:
        io.open(path, "w", encoding="utf-8", newline="\n").write(text)


def git(tmp, *args):
    quiet = {"stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL, "cwd": tmp}
    return subprocess.call(["git"] + list(args), **quiet)


def build(tmp, maps=None, terms=None, content=None, register=None,
          ignore_sanitization=True, seed_green_dirs=True):
    """A throwaway repo with the three scripts, a vocabulary, a register and one baseline commit.

    `content` maps repo-relative path -> text and is committed. `register` is the raw register text;
    the default declares exactly the five --green corpora, which is the state the real repo is in.
    """
    os.makedirs(os.path.join(tmp, "tools"))
    shutil.copy(SCRIPT, os.path.join(tmp, "tools", "check-green-claims.py"))
    shutil.copy(ORACLE, os.path.join(tmp, "tools", "verify-scrub.py"))
    shutil.copy(BUILDER, os.path.join(tmp, "tools", "build-scrub-rules.py"))
    for name, doc in (maps or {}).items():
        write(os.path.join(tmp, "sanitization", name + ".map.json"), doc)
    if terms is not None:
        write(os.path.join(tmp, "sanitization", "scrub-terms.md"), terms)
    if seed_green_dirs:
        for d in GREEN:
            write(os.path.join(tmp, d, "seed.md"), "ordinary sandbox content\n")
    for rel, text in (content or {}).items():
        write(os.path.join(tmp, rel), text)
    if register is None:
        register = "# test register\n" + "".join("%s # seeded\n" % d for d in GREEN)
    write(os.path.join(tmp, "tools", "green-claims.txt"), register)
    write(os.path.join(tmp, ".gitignore"),
          "sanitization/\n" if ignore_sanitization else "# nothing ignored\n")
    git(tmp, "init")
    git(tmp, "config", "user.email", "t@t")
    git(tmp, "config", "user.name", "t")
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "baseline")
    return os.path.join(tmp, "tools", "check-green-claims.py")


def run(tmp, script, *args):
    proc = subprocess.Popen([sys.executable, script, "--repo", tmp] + list(args),
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=tmp)
    out, err = proc.communicate()
    return (proc.returncode, out.decode("utf-8", "replace"), err.decode("utf-8", "replace"))


def assert_eq(got, want, what):
    assert got == want, "%s: got %r, want %r" % (what, got, want)


def assert_in(needle, haystack, what):
    assert needle in haystack, "%s: %r not in %r" % (what, needle, haystack)


# Invented vocabulary. `Zorbex` is the declared term (T1). `QuadrantWidgetUnit` is an inferred map
# key long enough to be T2. `Plate` is an identity mapping, which is T3 and never gates.
TERMS = ("# terms\n\n| live | invented | class | scope | variants |\n"
         "|---|---|---|---|---|\n"
         "| Zorbex | JOB4242 | site | global | auto |\n")
MAPS = {"m": '{"Names": {"QuadrantWidgetUnit": "GenericWidgetUnit", "Plate": "Plate"}}'}


def a_declared_directory_with_no_vocabulary_is_CLEAN(tmp):
    """The permit direction. A suite where the gate only ever refuses proves nothing."""
    s = build(tmp, MAPS, TERMS)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a clean register must pass (%s%s)" % (out, err))
    assert_in("CLEAN", out, "the verdict must say so")


def a_DECLARED_T1_term_in_a_Green_directory_GATES(tmp):
    """The case the tool exists for, and the one the real repo failed on first contact."""
    s = build(tmp, MAPS, TERMS, {"gen/test-project001/notes.md": "measured against Zorbex\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "a declared term in a Green dir must gate (%s%s)" % (out, err))
    assert_in("gen/test-project001/notes.md", out, "the worklist must name the path")
    assert_in("1 declared (T1)", out, "the tier must be reported")


def a_whole_token_T2_key_in_a_Green_directory_GATES(tmp):
    s = build(tmp, MAPS, TERMS,
              {"ir/reference/b.ir": "TAG QuadrantWidgetUnit AT %I0.0\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "a whole-token inferred key must gate (%s%s)" % (out, err))
    assert_in("1 inferred (T2)", out, "the tier must be reported")


def a_T2_key_EMBEDDED_only_does_NOT_gate(tmp):
    """The oracle's asymmetry, preserved: an inferred key inside a longer token is usually a
    different identifier that merely starts the same way. T1 counts embedded; T2 does not."""
    s = build(tmp, MAPS, TERMS,
              {"ir/reference/b.ir": "TAG XxQuadrantWidgetUnitYy AT %I0.0\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "an embedded inferred key must not gate (%s%s)" % (out, err))


def a_T1_term_EMBEDDED_in_a_longer_token_STILL_gates(tmp):
    """The other half of the asymmetry. The owner named that string and meant it anywhere."""
    s = build(tmp, MAPS, TERMS, {"ir/reference/b.ir": "TAG XxZorbexYy AT %I0.0\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "an embedded declared term must still gate (%s%s)" % (out, err))


def a_register_entry_NOT_on_the_green_list_GATES(tmp):
    """Half (1). A claim the tooling has never honoured is the AB-2 shape exactly."""
    reg = "# test\n" + "".join("%s # seeded\n" % d for d in GREEN) + "docs/evidence # invented\n"
    s = build(tmp, MAPS, TERMS, {"docs/evidence/x.md": "ordinary\n"}, register=reg)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "an undeclared-to-the-tooling dir must gate (%s%s)" % (out, err))
    assert_in("not covered by the scrub tools", out, "the finding must say which direction")


def a_green_list_entry_NOT_in_the_register_GATES(tmp):
    """Half (1), the other direction. An entry on the list with no written reason is a silent
    exemption - the list withholds a rewrite rule for every term present in it."""
    reg = "# test\n" + "".join("%s # seeded\n" % d for d in GREEN[:-1])
    s = build(tmp, MAPS, TERMS, register=reg)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "an unexplained --green entry must gate (%s%s)" % (out, err))
    assert_in("nothing in", out, "the finding must name the register")


def a_SUBDIRECTORY_of_a_green_entry_is_COVERED(tmp):
    """PREFIX, not equality. --green membership is `git ls-files <corpus>`, a prefix walk, so
    testing equality here would report a directory as undeclared while the scrub was in fact
    treating it as Green - a finding in the wrong direction."""
    reg = ("# test\n" + "".join("%s # seeded\n" % d for d in GREEN)
           + "gen/test-project001/hx-corpus # a subdirectory\n")
    s = build(tmp, MAPS, TERMS,
              {"gen/test-project001/hx-corpus/r.md": "ordinary\n"}, register=reg)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a subdirectory of a green entry is covered (%s%s)" % (out, err))


def a_term_CONVENTIONAL_in_ANOTHER_declared_corpus_does_NOT_gate(tmp):
    """🔴 LEAVE-ONE-OUT, the permit half. A map key that clean content legitimately uses is
    conventional. Without the demotion every Green corpus fails on keys it is entitled to hold."""
    s = build(tmp, MAPS, TERMS, {
        "ir/reference/vocab.ir": "TAG QuadrantWidgetUnit AT %I0.0\n",
        "gen/test-project001/notes.md": "the QuadrantWidgetUnit is conventional here\n",
    })
    code, out, err = run(tmp, s)
    # ir/reference carries it too, so for gen/test-project001 it is conventional - and vice versa.
    assert_eq(code, EXIT_OK,
              "a key present in two declared corpora is conventional (%s%s)" % (out, err))


def the_SAME_term_present_ONLY_here_DOES_gate(tmp):
    """🔴 LEAVE-ONE-OUT, the refuse half, and the pair above is what proves the design.

    A naive implementation that passes the Green corpora wholesale lets a directory's own content
    demote its own needles, and this case is the one it fails: the term appears NOWHERE else, so
    nothing makes it conventional, and it must gate."""
    s = build(tmp, MAPS, TERMS,
              {"gen/test-project001/notes.md": "the QuadrantWidgetUnit appears only here\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND,
              "a key present in only the dir under test must gate (%s%s)" % (out, err))


def an_ABSENT_register_is_NOTHING_EXAMINED(tmp):
    s = build(tmp, MAPS, TERMS)
    os.remove(os.path.join(tmp, "tools", "green-claims.txt"))
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "an absent register cannot be a pass (%s%s)" % (out, err))
    assert_in("EMPTY IS NOT CLEAN", out, "it must say so in the house words")
    # ABSENT and EMPTY are two guards that both land on exit 2, so the code alone cannot tell them
    # apart and a mutation swapping one for the other would redden nothing. The message can.
    assert_in("no Green register at", out, "it must distinguish ABSENT from EMPTY")


def an_EMPTY_register_is_NOTHING_EXAMINED(tmp):
    """A register with no entries checks nothing and would report success - the worst of both."""
    s = build(tmp, MAPS, TERMS, register="# only comments\n\n# and blanks\n")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN, "an empty register cannot be a pass (%s%s)" % (out, err))


def a_STALE_entry_is_REPORTED_without_gating(tmp):
    """An escape nobody sees is how a list accretes. Reported every run, but it is not a leak."""
    reg = ("# test\n" + "".join("%s # seeded\n" % d for d in GREEN)
           + "ir/reference/gone # nothing tracked under here\n")
    s = build(tmp, MAPS, TERMS, register=reg)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "a stale entry must not gate (%s%s)" % (out, err))
    assert_in("stale", out, "a stale entry must be reported")


def a_TRACKED_term_list_is_REFUSED(tmp):
    """The term list names every identifier the check hunts for. Committing it publishes the thing
    the check exists to keep out, so this refuses before reading anything."""
    s = build(tmp, MAPS, TERMS, ignore_sanitization=False)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_REFUSED, "a tracked term list must be refused (%s%s)" % (out, err))
    assert_in("REFUSED", err, "the refusal goes to stderr")


def NO_vocabulary_and_no_live_runs_does_NOT_gate(tmp):
    """A fresh clone has no sanitization/ and never will. Refusing there forever is a broken tool
    that teaches --no-verify. The structural half still ran and still stands."""
    s = build(tmp, None, None)
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_OK, "no vocabulary on a clean clone is not a refusal (%s%s)" % (out, err))
    assert_in("CONTENT half did not run", out, "it must say which half did not run")


def NO_vocabulary_WITH_live_run_material_is_NOTHING_EXAMINED(tmp):
    """The dangerous state, and the one judgement in the tool - the same one M-5 makes."""
    s = build(tmp, None, None)
    write(os.path.join(tmp, "Live Runs", "J0000", "note.txt"), "a live job is on this machine\n")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_CANNOT_RUN,
              "no vocabulary during a live run must refuse (%s%s)" % (out, err))
    assert_in("EMPTY IS NOT CLEAN", out, "it must say so in the house words")


def an_UNSEARCHABLE_blob_is_NOT_counted_clean(tmp):
    """A blob no text pass reaches is not a clean blob. It is an unexamined one."""
    s = build(tmp, MAPS, TERMS)
    write(os.path.join(tmp, "ir", "reference", "blob.bin"), b"\xff\xfe\x00\x01rubbish", binary=True)
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "binary")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "an unsearchable blob is not clean (%s%s)" % (out, err))
    assert_in("NOT SEARCHED", out, "it must name what it could not read")


def DIVERGENT_green_defaults_between_the_two_tools_GATE(tmp):
    """They are separate literals by design - the independence rule forbids merging them. It does
    not forbid asking whether they agree, and neither tool can report this about itself."""
    s = build(tmp, MAPS, TERMS)
    path = os.path.join(tmp, "tools", "build-scrub-rules.py")
    src = io.open(path, encoding="utf-8").read()
    assert '"simatic-ml/test-project001"' in src, "the fixture assumption about the literal broke"
    write(path, src.replace('"simatic-ml/test-project001"', '"simatic-ml/somewhere-else"', 1))
    git(tmp, "add", "-A")
    git(tmp, "commit", "-m", "diverge")
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "divergent --green defaults must gate (%s%s)" % (out, err))
    assert_in("DIVERGED", out, "the finding must name the divergence")


def the_TERMS_are_NOT_printed_without_the_flag(tmp):
    """A record of a leak must not be a copy of it, and this output gets pasted into notes."""
    s = build(tmp, MAPS, TERMS, {"gen/test-project001/notes.md": "measured against Zorbex\n"})
    code, out, err = run(tmp, s)
    assert_eq(code, EXIT_FOUND, "the finding must still fire (%s%s)" % (out, err))
    assert "zorbex" not in out.lower(), "the matched term must NOT be printed by default"
    assert_in("--name-terms", out, "it must say how to see them")


def the_TERMS_ARE_printed_with_name_terms(tmp):
    """The watched-terminal exception. An escape that does not work is not an escape."""
    s = build(tmp, MAPS, TERMS, {"gen/test-project001/notes.md": "measured against Zorbex\n"})
    code, out, err = run(tmp, s, "--name-terms")
    assert_eq(code, EXIT_FOUND, "the finding must still fire (%s%s)" % (out, err))
    assert_in("zorbex", out.lower(), "--name-terms must print the matched term")


for name, body in [
    ("a declared directory with no vocabulary is CLEAN",
     a_declared_directory_with_no_vocabulary_is_CLEAN),
    ("TIER: a DECLARED (T1) term in a Green directory GATES",
     a_DECLARED_T1_term_in_a_Green_directory_GATES),
    ("TIER: a whole-token INFERRED (T2) key GATES",
     a_whole_token_T2_key_in_a_Green_directory_GATES),
    ("TIER: an EMBEDDED-only T2 key does NOT gate", a_T2_key_EMBEDDED_only_does_NOT_gate),
    ("TIER: an EMBEDDED T1 term STILL gates", a_T1_term_EMBEDDED_in_a_longer_token_STILL_gates),
    ("REGISTER: an entry not on the --green list GATES",
     a_register_entry_NOT_on_the_green_list_GATES),
    ("REGISTER: a --green entry not in the register GATES",
     a_green_list_entry_NOT_in_the_register_GATES),
    ("REGISTER: a SUBDIRECTORY of a --green entry is covered",
     a_SUBDIRECTORY_of_a_green_entry_is_COVERED),
    ("LEAVE-ONE-OUT: conventional in ANOTHER declared corpus does NOT gate",
     a_term_CONVENTIONAL_in_ANOTHER_declared_corpus_does_NOT_gate),
    ("LEAVE-ONE-OUT: the SAME term present ONLY here DOES gate",
     the_SAME_term_present_ONLY_here_DOES_gate),
    ("CANNOT RUN: an ABSENT register is NOTHING EXAMINED", an_ABSENT_register_is_NOTHING_EXAMINED),
    ("CANNOT RUN: an EMPTY register is NOTHING EXAMINED", an_EMPTY_register_is_NOTHING_EXAMINED),
    ("CANNOT RUN: no vocabulary WITH live-run material is NOTHING EXAMINED",
     NO_vocabulary_WITH_live_run_material_is_NOTHING_EXAMINED),
    ("CANNOT RUN: a TRACKED term list is REFUSED", a_TRACKED_term_list_is_REFUSED),
    ("STALE: an entry with no tracked files is reported without gating",
     a_STALE_entry_is_REPORTED_without_gating),
    ("no vocabulary and no live runs does NOT gate", NO_vocabulary_and_no_live_runs_does_NOT_gate),
    ("an UNSEARCHABLE blob is NOT counted clean", an_UNSEARCHABLE_blob_is_NOT_counted_clean),
    ("DIVERGENCE: the two --green defaults disagreeing GATES",
     DIVERGENT_green_defaults_between_the_two_tools_GATE),
    ("HYGIENE: the terms are NOT printed without the flag", the_TERMS_are_NOT_printed_without_the_flag),
    ("HYGIENE: the terms ARE printed with --name-terms", the_TERMS_ARE_printed_with_name_terms),
]:
    case(name, body)

print("=" * 70)
print("RESULT: %d passed, %d failed" % (passed, failed))
for f in failures:
    print("  failed: %s" % f)
raise SystemExit(1 if failed else 0)
