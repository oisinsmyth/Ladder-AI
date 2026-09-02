"""Assemble FB_ShredderSequencerStim.ir = authored header/interface + GENERATED shell + authored tail.

Adapted from agent-tasks/pusher-slot/gen/assemble.py. Nothing here writes IR of its own. The only
transformation it performs is projecting the GENERATED UDT's member list into the FB's struct member,
which is a derivation and not an authoring. It reports the exact hand-written line count so that
number is measured rather than claimed.
"""
import io
import os
import re
import sys

root = sys.argv[1]
here = os.path.dirname(os.path.abspath(__file__))
ir = os.path.join(root, "ir", "test-project001")

head = io.open(os.path.join(here, "authored-head.ir"), encoding="utf-8").read()
tail = io.open(os.path.join(here, "authored-tail.ir"), encoding="utf-8").read()
shell = io.open(os.path.join(here, "..", "generated",
                             "FB_ShredderSequencerStim.networks.ir"), encoding="utf-8").read()
udt = io.open(os.path.join(ir, "UDT_ShredderSequencerStim.ir"), encoding="utf-8").read()

# The struct member list, projected from the generated UDT. Name and type only: the comments live on
# the type, and repeating them on the instance is where two copies start to disagree.
members = []
for line in udt.splitlines():
    m = re.match(r"^    (\w+) : (\S+)", line)
    if m:
        members.append("      %s : %s" % (m.group(1), m.group(2)))
assert members, "no members projected from the generated UDT"

body = head.replace("@@STIM_MEMBERS@@", "\n".join(members)) + shell + tail
io.open(os.path.join(ir, "FB_ShredderSequencerStim.ir"), "w",
        encoding="utf-8", newline="\r\n").write(body)

authored = len(head.splitlines()) - 1 + len(tail.splitlines())   # -1 for the marker line
generated_shell = len(shell.splitlines())
projected = len(members)
print("FB_ShredderSequencerStim.ir written to %s" % ir)
print("  authored lines (header + interface + networks 18-26) : %d" % authored)
print("  generated lines (shell networks 1-17)                : %d" % generated_shell)
print("  projected lines (UDT member list)                    : %d" % projected)
print("  total                                                : %d" % len(body.splitlines()))
