# Pattern library

Proven LAD, extracted and documented from real working logic — never invented speculatively. Spec:
`docs/07-pattern-library-spec.md`. **Starting a new pattern? Copy from `_templates/` first**, not
from an existing pattern folder — the templates have placeholders and instructions, real patterns
don't.

**One pattern here contradicts that opening sentence, and it is a recorded exception rather than a
repeal: `valve-two-state/`.** It was written fresh from a specification instead of being extracted
from working logic, and it was admitted **unproven — it has never run on hardware, not once and not
on a bench** — by the owner's explicit instruction, ahead of the admission criteria and knowing it
cut across this rule. It is the only pattern in the library whose content did not come out of logic
that has run on a plant; every other folder here was extracted from working logic, whatever its
review state. Desk verification (parse, convert, import, compile, lossless round-trip, mechanical
review) is not evidence that it controls a valve, so read it as a starting point that still owes you
commissioning. Its own `valve-two-state/pattern.md` leads with the full declaration and the precise
record of what was and was not checked — read that before using it, and don't soften it. **The rule
above is still the rule**: a fresh-from-spec admission needs the owner's explicit instruction, per
pattern, every time, and does not become the norm because it has happened once.

Three kinds so far (not a closed taxonomy — see the spec for what to do if a new pattern fits
none of them). The examples named below are illustrative, not a directory listing:

- **Equipment-instance** — a whole, proven, callable FB/FC, reused via ordinary `CALL`
  (`docs/06-lad-conventions.md` C-106). Real example: `motor-dol/` (block, interface UDT and a real
  call site); `valve-two-state/` is the same kind, and is the unproven exception above.
  Template: `_templates/equipment-instance/`.
- **Repeated rung-shape** — the same rung shape repeated many times within one sequencing/mapping
  FC, kept inline (C-109/C-110), documented from a real annotated example rather than templated.
  Real example: `chained-permissive-enable/`. Template: `_templates/rung-shape/`.
- **Buffer-DB structure** — the member *layout* of a singleton per-station buffer DB (C-304's data
  side): not callable and not ladder logic, so it fits neither kind above, and reuse means drafting
  a new DB to the same layout (spec, Kind 3). Real examples: `db-inputs/`, `db-outputs/`. No
  template folder — the pattern is the layout in `pattern.md` plus the real DB beside it.

Admission (every kind): proven in reviewed working logic, lossless round-trip, compiles when
instantiated (or, for a rung-shape pattern, when a new drafted instance is produced from the
documentation), complete `pattern.md`, human sign-off. See each kind's own admission criteria in
`docs/07-pattern-library-spec.md` — they differ in what "compiles when instantiated" means for
each kind. `valve-two-state/` meets all of these **except the first**, by the owner's recorded
decision; its `pattern.md` names the unmet criterion and says why it was admitted anyway. Note that
several patterns here are still `proposed, pending sign-off` — that is a *review* state and says
nothing about provenance; all of them were extracted from working logic.
