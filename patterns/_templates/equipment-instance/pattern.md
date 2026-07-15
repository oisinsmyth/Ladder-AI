# Pattern: [pattern-name] (equipment-instance kind)

**Status:** proposed, pending review and sign-off. Not yet admitted to the library.

## Intent

[What equipment/function this FB covers, in one or two sentences. Cite `06-lad-conventions.md`
C-106 (repeated equipment gets one standard FB + UDT interface). Name the source block and where
it was grounded from: `[BlockName]` ([project], `[device/path]`), grounded fresh on [date].]

## When to use

- [Concrete equipment types/situations this pattern fits.]

## When *not* to use

- [Equipment types this doesn't cover — name the separate pattern/FB that does, if one exists,
  rather than leaving a silent gap. E.g. "VSD-driven motors — see `[other-pattern]` instead."]
- [Any other real, known exclusion. Don't invent hypothetical ones just to fill this section.]

## Interface

[Confirm every row against real call sites, not the interface declaration alone — a member can be
declared but never actually written/read by any real caller, which changes what a colleague
actually needs to wire.]

**Caller writes (real-world inputs):**

| Member | Type | Meaning |
|---|---|---|
| `[Member]` | `[Type]` | [What it represents, where it typically comes from] |

**Caller reads (this instance's own outputs):**

| Member | Type | Meaning |
|---|---|---|
| `[Member]` | `[Type]` | [What it represents, what typically consumes it] |

## Behaviour, by network

[Walk through what the block actually does, grouped by function, not just "network N does X"
mechanically. State known deviations from current site convention honestly rather than silently
cleaning them up or omitting them — e.g. if the real source uses a banned instruction, say so and
say it's not the thing to copy into new equipment.]

1. [Network/group 1 — what it computes and why.]

## Admission status

1. Instantiated at least once in reviewed, working logic — [yes/no + evidence].
2. Round-trips losslessly — [confirmed how, or pending].
3. Compiles when instantiated — [confirmed how (real `CALL` site, 0 errors in which project), or
   pending].
4. `pattern.md` complete — [pending review / reviewed by whom, when].
5. Human sign-off — [pending, or name + date].
