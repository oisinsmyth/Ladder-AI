"""Rewrite FB_HopperBlockageStim NETWORK 21's comment and convert NETWORK 22 to a plain coil.

N21 comment: C-204/C-602 rewrite - states what the structure achieves, drops the
development record and the argument-against-alternatives.

N22: `RCOIL Test[6] := Running OR InTrailingCleardown` becomes a plain coil carrying the
same pass-through contract as N21. Behaviourally identical (while authoritative both write
0; while idle both leave this scan's value standing) and it removes the block's only S/R
mechanism, which C-403 requires to appear in the startup-reset block and which OB100 does
not name.
"""
import io

P = "ir/test-project001/FB_HopperBlockageStim.ir"

N21 = (
    "Presents the commanded plant to the input map through the input buffer's own "
    "test-injection members: with the flag set the map takes each input from its test member "
    "instead of from the physical terminal. Every rung here passes through while this model is "
    "not driving, so a line this model does not own carries whatever head does own it, and the "
    "arbitration layer ahead of every head is what a pass-through rests on when no head owns a "
    "line at all. The flag itself is combined rather than owned - this model ORs itself in, and "
    "the flag is set whenever any head wants injection. The model stays authoritative through "
    "the trailing clear-down as well as the run, holding the two commanded inputs low there, so "
    "the clear-down's reset lands on a hopper this model is still driving low. The operator "
    "reset is written directly, being a control line rather than a field input that reaches the "
    "block through the map."
)

N22 = (
    "The plant-running condition the monitor sees is the forward feedback or the reverse one, "
    "and this model commands only the forward. The reverse feedback's test member is therefore "
    "held low for the whole run and through the trailing clear-down that follows it, so a "
    "commanded stop is a stop rather than the forward feedback alone going low. Outside those "
    "phases it passes through on the same contract as the network above."
)

src = io.open(P, encoding="utf-8", newline="").read()

# --- N21 comment -------------------------------------------------------------------------
lines = src.split("\n")
idx = [i for i, l in enumerate(lines)
       if l.startswith('  COMMENT "Presents the commanded plant to the input map')]
assert len(idx) == 1, "N21 comment: %r" % idx
lines[idx[0]] = '  COMMENT "' + N21 + '"'

idx = [i for i, l in enumerate(lines)
       if l.startswith('  COMMENT "The model commands one running feedback')]
assert len(idx) == 1, "N22 comment: %r" % idx
lines[idx[0]] = '  COMMENT "' + N22 + '"'

src = "\n".join(lines)

old_rung = "  RCOIL DB_Input.Test[6] := Running OR InTrailingCleardown\n"
new_rung = "  COIL DB_Input.Test[6] := NOT Running AND NOT InTrailingCleardown AND DB_Input.Test[6]\n"
assert src.count(old_rung) == 1, "N22 rung: %d" % src.count(old_rung)
src = src.replace(old_rung, new_rung)

io.open(P, "w", encoding="utf-8", newline="").write(src)
print("patched N21 comment + N22 (comment and rung)")
