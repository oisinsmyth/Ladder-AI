# Lane D — Agent & integration

**Owns exclusively:** `.claude/agents/hmi-designer.md`, `.claude/skills/hmi-*/`
**Portal:** never (the agent it defines will) · **Wave:** 3 · **Size:** S
**Depends on:** Lane A (the conventions document), Lane B (the tools the method invokes)
**🔴 GATED ON ADR-0007. Do not start this lane before the decision.**

## What is being created

`hmi-designer`: a sub-agent standing in the same relation to HMI screen content that `lad-coder`
stands in to ladder logic — the **only** agent permitted to author or modify HMI screens, dispatched
for every such task no matter how small, working to a fixed contract and handing back evidence
rather than assurances.

Three grounds, each measured rather than assumed:

- **The work is iterative and noisy.** Experiment 2's winning arm ran 7 render cycles and inspected 7
  screenshots. That is a lot of context spent on images and intermediate defects, none of which the
  dispatching conversation needs. An agent contains it and returns the verdict.
- **The method must not be re-decided per task.** The rules layer is free and decisive; skipping it
  produced green-for-running and accent chrome in every arm that lacked it, unprompted. Encoding the
  method in an agent definition is how it stops being a choice.
- **It mirrors a pattern this project already trusts.** Hard rule 8 exists because LAD work done
  inline drifts. The same shape transfers without modification.

## The mandatory method to encode (the measured hybrid)

1. **Read the house style guide** (`docs/17-hmi-conventions.md`) and author HTML/CSS against it.
   Never invent a palette, a type scale or a spacing scale.
2. **Render headlessly and look at the result.** Not optional: the arm that skipped this shipped a
   visible text collision; the arm that did it caught clipping, collisions, dead space and a
   mis-seated label across 7 cycles.
3. **Run the mechanical checks AND the render, both, every time.**
   🔴 **This is the single most important line in the agent definition.** Measured directly: T2
   missed a visible text collision (glyph overflow without box intersection) and the render missed 2
   off-canvas elements across 8 cycles. **Neither subsumes the other.**
4. **Iterate** until the checks are clean and the render looks right.
5. **Hand back** the source, the final render, the check output *verbatim*, and a statement of what
   it could not satisfy. **Its summary is not evidence.**

### And explicitly, in the definition itself

> **DO NOT invoke `artifact-design` or any general-purpose design skill.**

Measured three times (research §3.6 reasoning, §3.9, §3.10): it is tuned for web artifacts, it did
not prevent a single house-rule violation in either experiment, it added gradients, accent colour
and shadows, and in experiment 2 it **cost more tokens than the hybrid while scoring worst on
overlap and off-canvas.** State the prohibition with its reason, or somebody will helpfully re-add it.

## Prohibitions to encode

- **No self-review for acceptance.** A fixer reviewing its own fix is the correlated check this
  project exists to avoid. Reviewer → fixer is fine; the reverse is not. A separate reviewing pass
  reads the **built** screen via T6, not the spec it came from.
- **No writes to the real project.** Same gate as everything else: what reaches the engineer is a
  diff plus evidence.
- **No invented tags.** T8 makes `converter tagstatus` mandatory.
- **No safety content**, under any framing.
- **A green compile is not a pass.** `hmi-compile` is blind to geometry.

## ⚠️ The authoring trap that has bitten this repo

**Quote the `description:` field.** Frontmatter is parsed as strict YAML and **both failure modes are
completely silent**:

- an unquoted `: ` (colon-space) makes the file unparseable and the agent is **not registered at
  all** — `lad-coder` vanished exactly this way, with the file sitting there git-tracked and
  readable;
- an unquoted ` #` (space-hash) opens a YAML comment and **silently truncates the description**, which
  is worse because nothing looks broken. Five skills sat in that state for an unknown period, losing
  160–927 characters each and degrading skill selection across the whole Design/Build path with no
  symptom.

**Run `/doctor` after creating the file.** It is the only surface that reports either mode; upstream
closed the request to surface it in-session as "not planned", so expect the silence to persist.

Mechanism and the parse-it-yourself check: `docs/notes/claude-agent-skill-authoring.md`.

## Done when

- `.claude/agents/hmi-designer.md` exists, `description:` quoted, `/doctor` clean
- The method and prohibitions above are encoded, each with its reason
- One dispatched screen comes back with source + render + verbatim check output + a stated list of
  what it could not satisfy
- The dispatching conversation can verify that evidence **without** re-reading the screen itself
