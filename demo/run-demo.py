#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""A one-command run of this project's core claim, with no TIA Portal and no PLC.

    python demo/run-demo.py            # walk it, with output
    python demo/run-demo.py --check    # assert the expected result; CI runs this

WHAT IT PROVES. Siemens SimaticML goes to a readable intermediate representation and back, and the
result is semantically identical to what TIA exported. The corpus is `simatic-ml/reference/` — 15
blocks, eleven of them carrying TIA's own `<DocumentInfo>` envelope, so most of the answer keys are
genuine TIA artifacts rather than something this project wrote and then graded itself against.

*** WHAT IT DOES NOT PROVE, STATED HERE RATHER THAN DISCOVERED LATER. *** Nothing here goes through
TIA. A block that round-trips cleanly on this machine can still be refused on import or behave
differently after compile — that is the class the MemoryLayout defect belonged to, where a DB
round-tripped "equal and still wrong" and the first symptom was a runtime Modbus status code. The
loop that would catch it (`tools/confirm-roundtrip.ps1`) needs Portal exclusively and costs minutes
per block, and it deliberately refuses to print a verdict when run offline. This demo is strictly
weaker and says so.

THE RESULT IS 14 OF 15, NOT 15 OF 15. The exception is `NodeStatusAlarms`, and it is worth more than
a clean sweep would be: it is one of four seed artifacts committed with TIA's scaffolding trimmed, so
its `<Interface>` element is missing from the answer key. `converter compare` localises exactly one
difference — an ADDITION of TIA's own defaults. Our output is right and the answer key is incomplete,
which is recorded in three places in this repo and is why the number here is 14.

EVERY WRITE GOES TO A TEMPORARY DIRECTORY VIA --out. Without it `to-ir` and `to-xml` write beside
their input: that silently overwrote hand-authored `.ir` for two agents in one day, and run over an
`ir/` directory it replaces the very exports a later check compares against — which is how a whole
corpus of "MATCH" once became a tautology.
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CORPUS_XML = os.path.join(ROOT, "simatic-ml", "reference")
CORPUS_IR = os.path.join(ROOT, "ir", "reference")

# The walked example. ScaleValue is 16 readable lines - small enough to print whole, real enough to
# carry a wired interface. NEVER NodeStatusAlarms: it is the one known-incomplete answer key, and a
# demo whose headline example is the documented exception teaches the wrong lesson.
WALKED = "ScaleValue"

# Measured, not hoped for. `--check` fails if either moves.
EXPECT_EQUIVALENT = 14
EXPECT_EXCEPTIONS = ["NodeStatusAlarms"]


def find_converter():
    """CONVERTER_EXE, then Release, then Debug, then the portable `dotnet converter.dll` form."""
    env = os.environ.get("CONVERTER_EXE")
    if env and os.path.isfile(env):
        return [env]
    for config in ("Release", "Debug"):
        base = os.path.join(ROOT, "src", "converter", "Converter", "bin", config, "net8.0")
        for name in ("converter.exe", "converter"):
            exe = os.path.join(base, name)
            if os.path.isfile(exe):
                return [exe]
        dll = os.path.join(base, "converter.dll")
        if os.path.isfile(dll):
            return ["dotnet", dll]
    return None


def run(conv, *args):
    proc = subprocess.Popen(list(conv) + list(args), cwd=ROOT,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    out, _ = proc.communicate()
    return proc.returncode, out.decode("utf-8", "replace")


def show(title, code, out, keep=None):
    print("\n" + "-" * 78)
    print("$ converter " + title)
    print("-" * 78)
    lines = out.rstrip("\n").split("\n")
    if keep is not None and len(lines) > keep:
        lines = lines[:keep] + ["    ... (%d more lines)" % (len(lines) - keep)]
    for line in lines:
        print("  " + line)
    print("  [exit %d]" % code)


def walk_one_block(conv, work):
    """Six commands over one block: read it, describe it, check it, prove the diff, write it back,
    and compare the result against what TIA produced."""
    ir_dir = os.path.join(work, "ir")
    regen = os.path.join(work, "regen")
    src_xml = os.path.join(CORPUS_XML, WALKED + ".xml")
    ir_file = os.path.join(ir_dir, WALKED + ".ir")

    print("=" * 78)
    print("PART 1 - one block, six commands: %s" % WALKED)
    print("=" * 78)
    print("  answer key : simatic-ml/reference/%s.xml   (%d bytes, exported by TIA)"
          % (WALKED, os.path.getsize(src_xml)))

    code, out = run(conv, "to-ir", src_xml, "--out", ir_dir)
    show("to-ir simatic-ml/reference/%s.xml --out <tmp>/ir" % WALKED, code, out)
    if code != 0 or not os.path.isfile(ir_file):
        return None

    print("\n  The whole intermediate representation, as a person reads it:")
    print("  " + "." * 74)
    for line in open(ir_file, encoding="utf-8").read().rstrip("\n").split("\n"):
        print("  | " + line)
    print("  " + "." * 74)

    code, out = run(conv, "digest", ir_file, "--fingerprint")
    show("digest <tmp>/ir/%s.ir --fingerprint" % WALKED, code, out, keep=14)

    code, out = run(conv, "review", ir_file, "--project", CORPUS_IR)
    show("review <tmp>/ir/%s.ir --project ir/reference" % WALKED, code, out, keep=10)

    # --only is NOT decoration. Without it `diff` exits 0 unconditionally and proves nothing, so a
    # demo that omitted it would be showing a no-op and calling it an invariance check.
    code, out = run(conv, "diff", os.path.join(CORPUS_IR, WALKED + ".ir"), ir_file, "--only", "1")
    show("diff ir/reference/%s.ir <tmp>/ir/%s.ir --only 1" % (WALKED, WALKED), code, out, keep=10)

    code, out = run(conv, "to-xml", ir_file, "--out", regen, "--project", CORPUS_IR)
    show("to-xml <tmp>/ir/%s.ir --out <tmp>/regen --project ir/reference" % WALKED, code, out)
    if code != 0:
        return None

    code, out = run(conv, "compare", src_xml, os.path.join(regen, WALKED + ".xml"))
    show("compare simatic-ml/reference/%s.xml <tmp>/regen/%s.xml" % (WALKED, WALKED), code, out)
    return code == 0


def sweep(conv, work):
    """Every block in the corpus, round-tripped. This is the claim; part 1 is how it is made."""
    ir_dir = os.path.join(work, "sweep-ir")
    regen = os.path.join(work, "sweep-regen")
    names = sorted(os.path.splitext(f)[0] for f in os.listdir(CORPUS_XML) if f.endswith(".xml"))

    print("\n" + "=" * 78)
    print("PART 2 - the whole corpus: %d blocks, SimaticML -> IR -> SimaticML" % len(names))
    print("=" * 78)

    equivalent, exceptions = [], []
    for name in names:
        src = os.path.join(CORPUS_XML, name + ".xml")
        code, _ = run(conv, "to-ir", src, "--out", ir_dir)
        if code != 0:
            exceptions.append((name, "to-ir refused"))
            continue
        code, _ = run(conv, "to-xml", os.path.join(ir_dir, name + ".ir"),
                      "--out", regen, "--project", CORPUS_IR)
        if code != 0:
            exceptions.append((name, "to-xml refused"))
            continue
        code, _ = run(conv, "compare", src, os.path.join(regen, name + ".xml"))
        if code == 0:
            equivalent.append(name)
            print("  EQUIVALENT   %s" % name)
        else:
            exceptions.append((name, "compare found a difference"))
            print("  DIFFERS      %s" % name)

    print("\n  COMPARED: %d block(s). EQUIVALENT: %d. DIFFERS: %d."
          % (len(names), len(equivalent), len(exceptions)))
    return equivalent, exceptions


def main():
    ap = argparse.ArgumentParser(description="Round-trip demo: SimaticML <-> IR, no TIA required.")
    ap.add_argument("--check", action="store_true",
                    help="assert the expected result and exit non-zero if it moved. CI runs this, so "
                         "the demo cannot quietly go stale the way an un-run example does.")
    args = ap.parse_args()

    conv = find_converter()
    if conv is None:
        print("NOTHING RUN: converter was not found. Build it first:\n"
              "    dotnet build -c Release src/converter/converter.sln\n"
              "EMPTY IS NOT CLEAN - this is a refusal, not a demo with nothing to show.",
              file=sys.stderr)
        return 2
    if not os.path.isdir(CORPUS_XML) or not os.path.isdir(CORPUS_IR):
        print("NOTHING RUN: the reference corpus is missing. Expected simatic-ml/reference and "
              "ir/reference.", file=sys.stderr)
        return 2

    print("converter : %s" % " ".join(conv))
    work = tempfile.mkdtemp(prefix="ladder-demo-")
    try:
        walked = walk_one_block(conv, work)
        equivalent, exceptions = sweep(conv, work)
    finally:
        shutil.rmtree(work, ignore_errors=True)

    print("\n" + "=" * 78)
    print("WHAT THIS RUN SHOWED")
    print("=" * 78)
    print("  %d of %d committed blocks round-trip to a semantically identical document."
          % (len(equivalent), len(equivalent) + len(exceptions)))
    for name, why in exceptions:
        note = ""
        if name in EXPECT_EXCEPTIONS:
            note = ("  <- KNOWN AND DOCUMENTED: a seed artifact committed with TIA's scaffolding "
                    "trimmed, so its <Interface> is absent from the ANSWER KEY. The single "
                    "difference is an addition of TIA's own defaults. Our output is right.")
        print("  %s: %s%s" % (name, why, note))
    print("\n  WHAT IT DID NOT SHOW: nothing here went through TIA Portal. Import and compile "
          "behaviour is not covered,\n  and the loop that would cover it needs a licensed "
          "environment this demo deliberately does not require.")

    if not args.check:
        return 0

    problems = []
    if walked is not True:
        problems.append("the walked example (%s) did not round-trip" % WALKED)
    if len(equivalent) != EXPECT_EQUIVALENT:
        problems.append("expected %d equivalent, got %d" % (EXPECT_EQUIVALENT, len(equivalent)))
    unexpected = sorted(n for n, _ in exceptions if n not in EXPECT_EXCEPTIONS)
    if unexpected:
        problems.append("unexpected block(s) failed to round-trip: " + ", ".join(unexpected))
    fixed = sorted(n for n in EXPECT_EXCEPTIONS if n not in [x for x, _ in exceptions])
    if fixed:
        problems.append("%s now round-trips - the answer key was refreshed. Update "
                        "EXPECT_EQUIVALENT/EXPECT_EXCEPTIONS: a closed gap must not linger here "
                        "pretending to still be one." % ", ".join(fixed))

    print()
    if problems:
        print("--- DEMO CHECK FAILED ---")
        for p in problems:
            print("  " + p)
        return 1
    print("DEMO CHECK PASSED: %d equivalent, %d known and documented exception(s)."
          % (len(equivalent), len(exceptions)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
