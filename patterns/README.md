# Pattern library

Proven LAD, extracted and documented from real working logic — never invented speculatively. Spec:
`docs/07-pattern-library-spec.md`. **Starting a new pattern? Copy from `_templates/` first**, not
from an existing pattern folder — the templates have placeholders and instructions, real patterns
don't.

Two kinds so far (not a closed taxonomy — see the spec for what to do if a new pattern fits
neither):

- **Equipment-instance** — a whole, proven, callable FB/FC, reused via ordinary `CALL`
  (`docs/06-lad-conventions.md` C-106). Real example: `motor-dol/`. Template: `_templates/equipment-instance/`.
- **Repeated rung-shape** — the same rung shape repeated many times within one sequencing/mapping
  FC, kept inline (C-109/C-110), documented from a real annotated example rather than templated.
  Real example: `chained-permissive-enable/`. Template: `_templates/rung-shape/`.

Admission (both kinds): proven in reviewed working logic, lossless round-trip, compiles when
instantiated (or, for a rung-shape pattern, when a new drafted instance is produced from the
documentation), complete `pattern.md`, human sign-off. See each kind's own admission criteria in
`docs/07-pattern-library-spec.md` — they differ in what "compiles when instantiated" means for
each kind.
