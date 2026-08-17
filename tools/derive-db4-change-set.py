#!/usr/bin/env python3
"""Derive the two documents `wave-cli batch` consumes, from COMMITTED ARTIFACTS.

WHY THIS EXISTS
---------------
`wave-cli batch` plans DB-4's dependency-closed download batches, and its whole answer
rests on the DECLARED dependency list of every changed object.  `ChangedObject` says so
outright: dependencies are declared, not computed, and a wrong list produces a plan that
is wrong.  That is the shape D9 forbids for reachable state, so it gets D9's remedy --
a PRODUCER, reading artifacts, rather than an agent typing what it believes.

Everything here is read from something already in the repository:

  kind          the object's own IR declaration line (BLOCK FB / BLOCK FC / BLOCK OB /
                DB / TYPE / TAGTABLE)
  name          THE DECLARED NAME, NOT THE FILENAME.  `DefaultTagTable.ir` declares
                `Default tag table`, with spaces -- pairing on the filename is a
                measured drift-check defect (2026-08-14) that reported one object
                twice, once in each direction.
  dependencies  iDB -> FB          from the iDB's own INSTANCEOF line
                anything -> UDT    from a quoted "UDT_x" that a TYPE in the corpus declares
                caller -> callee   from `converter cross-check --json` siblingRefs.calls
                caller -> iDB      from siblingRefs.instanceDbRoots -- a block that calls
                                   FB_X(iDB_X, ...) does not compile without iDB_X

WHAT IT DOES *NOT* DECIDE
-------------------------
THE CHANGE CLASS.  DB-1 owns it, DB-1 is not built, and inventing one here would be the
laundering this project exists to prevent.  So only the classes THE SPEC FIXES are
applied -- an organisation block is STOP by R1, whatever the caller asks for -- and every
other object takes the class the caller states on the command line, with per-object
overrides.  An object with no IR at all gets NO kind and NO class: the fields are omitted
so they read as Unknown downstream and are REFUSED with a reason, rather than defaulted
into something plausible.

USAGE (PowerShell or bash; nothing here expands shell variables)

  python tools/derive-db4-change-set.py baseline ^
      --project ir/test-project001 --extra MotorIOSet --extra MotorVSDIOSet ^
      --provenance "..." --out <file>

  python tools/derive-db4-change-set.py change-set ^
      --project ir/test-project001 --cross-check <cc.json> ^
      --change-class Run --select DB_PLC --select FB_Comms_ModbusServer ^
      --provenance "..." --out <file>

  `--select` names the changed objects; `--all` takes the whole corpus.  `--unknown <name>`
  adds an object with no IR, which is a refusal downstream and is meant to be.
"""

import argparse
import json
import os
import re
import subprocess
import sys

# The classes the spec FIXES for a kind, whatever the caller says.  DB-1: "New / deleted
# OB, or OB property change ... STOP".  Nothing else in this corpus has a fixed class.
FIXED_CLASS = {"OrganizationBlock": "Stop"}

BLOCK_KINDS = {"FB": "FunctionBlock", "FC": "Function", "OB": "OrganizationBlock"}


def declared(path):
    """(name, kind) from the file's own declaration, or (None, None)."""
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        text = handle.read()

    instance_of = None
    name = None
    kind = None

    for raw in text.splitlines():
        line = raw.strip()
        if not line:
            continue

        if name is None:
            block = re.match(r"^BLOCK\s+(FB|FC|OB)\s+(.+)$", line)
            if block:
                kind = BLOCK_KINDS[block.group(1)]
                name = block.group(2).strip()
                continue

            db = re.match(r"^DB\s+(.+)$", line)
            if db:
                kind = "GlobalDataBlock"
                name = db.group(1).strip()
                continue

            udt = re.match(r"^TYPE\s+(.+)$", line)
            if udt:
                kind = "DataType"
                name = udt.group(1).strip()
                continue

            tags = re.match(r"^TAGTABLE\s+(.+)$", line)
            if tags:
                kind = "TagTable"
                name = tags.group(1).strip()
                continue

            # A first meaningful line that declares nothing: not an object we can read.
            return None, None, None, text

        of = re.match(r"^INSTANCEOF\s+(.+)$", line)
        if of and instance_of is None:
            instance_of = of.group(1).strip()

    if name is not None and kind == "GlobalDataBlock" and instance_of:
        kind = "InstanceDataBlock"

    return name, kind, instance_of, text


def read_corpus(project):
    """Every .ir in the project directory, keyed by its DECLARED name."""
    corpus = {}
    unreadable = []

    for entry in sorted(os.listdir(project)):
        if not entry.lower().endswith(".ir"):
            continue

        path = os.path.join(project, entry)
        name, kind, instance_of, text = declared(path)

        if name is None:
            unreadable.append(entry)
            continue

        if name in corpus:
            # Two files claiming one identity: nothing can be derived, and picking one
            # would be a guess about which was meant.
            raise SystemExit(
                "PAIRING FAILURE: '%s' and '%s' both declare the object '%s'."
                % (corpus[name]["file"], entry, name))

        corpus[name] = {
            "file": entry,
            "kind": kind,
            "instanceOf": instance_of,
            "text": text,
        }

    if unreadable:
        raise SystemExit(
            "REFUSED: %d file(s) in %s declare no object and could not be classified: %s. "
            "Skipping them would make the corpus look smaller than it is."
            % (len(unreadable), project, ", ".join(unreadable)))

    if not corpus:
        raise SystemExit("REFUSED: %s contains no readable .ir files. Empty is not clean." % project)

    return corpus


def dependencies(corpus, cross_check):
    """name -> sorted list of declared dependencies, from the sources named in the header."""
    types = set(n for n, o in corpus.items() if o["kind"] == "DataType")
    edges = dict((name, set()) for name in corpus)

    for name, obj in corpus.items():
        if obj["instanceOf"]:
            edges[name].add(obj["instanceOf"])

        for quoted in re.findall(r'"([^"\n]+)"', obj["text"]):
            if quoted in types and quoted != name:
                edges[name].add(quoted)

    for entry in cross_check.get("siblingRefs", []):
        block = entry.get("block")
        if block not in edges:
            continue

        for called in entry.get("calls", []) or []:
            edges[block].add(called)

        for idb in entry.get("instanceDbRoots", []) or []:
            edges[block].add(idb)

    return dict((name, sorted(e - {name})) for name, e in edges.items())


def head_sha():
    try:
        return subprocess.check_output(
            ["git", "rev-parse", "--short", "HEAD"], stderr=subprocess.DEVNULL).decode().strip()
    except Exception:
        return "unknown"


def write(path, document):
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(document, handle, indent=2)
        handle.write("\n")


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["baseline", "change-set"])
    parser.add_argument("--project", required=True)
    parser.add_argument("--cross-check")
    parser.add_argument("--provenance", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--select", action="append", default=[])
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--extra", action="append", default=[],
                        help="baseline only: an object known to be on the device with no .ir")
    parser.add_argument("--unknown", action="append", default=[],
                        help="change-set only: a changed object with no .ir, so no kind and no class")
    parser.add_argument("--change-class", default=None,
                        help="the class for every object the spec does not fix")
    parser.add_argument("--class-of", action="append", default=[],
                        help="Name=Class, overriding --change-class for one object")
    args = parser.parse_args(argv)

    corpus = read_corpus(args.project)
    stamp = "%s @ %s, %d .ir file(s)" % (args.project, head_sha(), len(corpus))

    if args.mode == "baseline":
        names = sorted(list(corpus.keys()) + list(args.extra))
        write(args.out, {
            "provenance": args.provenance + " [" + stamp + "]",
            "objects": names,
        })
        print("BASELINE: %d object(s) -> %s" % (len(names), args.out))
        print("  %d read from %s, %d supplied by --extra: %s"
              % (len(corpus), args.project, len(args.extra), ", ".join(args.extra) or "none"))
        return 0

    if not args.change_class:
        raise SystemExit(
            "REFUSED: change-set needs --change-class. DB-1 owns the class and DB-1 is not built, "
            "so this tool applies only the classes the spec FIXES and will not pick the rest for you.")

    if not args.cross_check:
        raise SystemExit(
            "REFUSED: change-set needs --cross-check <converter cross-check --json output>. Without "
            "it, caller->callee dependencies would be silently absent and every batch would look "
            "closed because nothing said otherwise.")

    with open(args.cross_check, "r", encoding="utf-8") as handle:
        cross = json.load(handle)

    overrides = {}
    for pair in args.class_of:
        if "=" not in pair:
            raise SystemExit("REFUSED: --class-of takes Name=Class; got '%s'." % pair)
        key, value = pair.split("=", 1)
        overrides[key.strip()] = value.strip()

    if args.all:
        selected = sorted(corpus.keys())
    else:
        selected = list(args.select)

    missing = [n for n in selected if n not in corpus]
    if missing:
        raise SystemExit(
            "REFUSED: %s names %d object(s) that are not in %s: %s. A --select naming nothing would "
            "produce a smaller change set that looks like a thorough one."
            % ("--select", len(missing), args.project, ", ".join(missing)))

    if not selected and not args.unknown:
        raise SystemExit(
            "REFUSED: nothing was selected. Emitting an empty change set here would move the "
            "'empty is not clean' problem one layer up, where it is harder to see.")

    edges = dependencies(corpus, cross)

    objects = []
    for name in selected:
        kind = corpus[name]["kind"]
        change_class = FIXED_CLASS.get(kind, overrides.get(name, args.change_class))
        objects.append({
            "name": name,
            "kind": kind,
            "changeClass": change_class,
            "dependsOn": edges[name],
            "artifactHash": "file:" + corpus[name]["file"],
        })

    for name in args.unknown:
        # Deliberately no kind and no class. The consumer reads them as Unknown and refuses,
        # which is the correct outcome for an object nobody can classify.
        objects.append({"name": name})

    write(args.out, {
        "provenance": args.provenance + " [" + stamp + "; classes: fixed=" +
                      json.dumps(FIXED_CLASS) + ", default=" + args.change_class +
                      ", overrides=" + json.dumps(overrides) + "]",
        "objects": objects,
    })

    fixed = [o["name"] for o in objects if o.get("kind") in FIXED_CLASS]
    print("CHANGE SET: %d object(s) -> %s" % (len(objects), args.out))
    print("  %d selected from a corpus of %d; %d with no .ir (kind and class withheld): %s"
          % (len(selected), len(corpus), len(args.unknown), ", ".join(args.unknown) or "none"))
    print("  class '%s' applied, except %d object(s) the spec FIXES: %s"
          % (args.change_class, len(fixed), ", ".join(fixed) or "none"))
    print("  %d dependency edge(s) declared" % sum(len(o.get("dependsOn", [])) for o in objects))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
