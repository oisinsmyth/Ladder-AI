# Prototypes

**Copied verbatim from the research session's scratch directory. Not refactored, not cleaned up,
not production code.** They are here so Lane B starts from something that ran rather than from a
description of something that ran.

| file | is the prototype of | proven |
|---|---|---|
| `flatten-probe.html` | **T1 `hmi-flatten`** — the flattener is the `<script>` block at the bottom | ✅ 55 items, 4 faceplate refs, 14 bindings, at 1920×1080 |
| `scorecard.py` | **T2 `hmi-lint`** (geometry half) + **T3 `hmi-style-check`** (HSL half) | ✅ scored all four arms of experiment 2 identically |
| `geometry-harness.py` | experiment 1's measurement harness — T1's driver, the copy/inject/`--dump-dom`/parse loop | ✅ ran experiment 1 |

## How they work

`geometry-harness.py` and `scorecard.py` both use the same trick, and it is the whole method:

1. copy the arm's directory to a scratch working dir
2. inject a probe `<script>` before `</body>` — it walks the DOM on `load`, reads
   `getBoundingClientRect()` and `getComputedStyle()` for every element, and stashes the result as
   JSON in a hidden `<pre>`
3. run `chrome --headless --dump-dom` against the file URL
4. regex the `<pre>` out of the dumped DOM and parse it

No browser automation framework, no MCP server, no dependency beyond the Chrome already installed.
That is the point.

## Known defects — deliberately preserved

**Do not fix these silently. They are findings, and Lane B needs to inherit them knowingly.**

- **`scorecard.py`'s overlap metric is defective in both directions, and this is measured.** It
  scored one arm at 0 overlaps despite a *visible* text collision — glyphs overflow their box
  without the boxes intersecting — and it false-positived 7 times on another arm where the overlap
  was deliberate. This is exactly why the method requires the render loop **as well as** the
  checker: neither subsumes the other.
- **Hardcoded absolute Chrome path.** `C:\Program Files\Google\Chrome\Application\chrome.exe`.
- **Hardcoded 1280×800 canvas** in `scorecard.py`, 1920×1080 in `geometry-harness.py`.
- **No denominator printed, exits 0 on an empty run.** Both violate the project's standing rule that
  a check must print `COMPARED: n` and must never pass over zero comparisons. Fix this in promotion —
  it is not cosmetic.
- **`flatten-probe.html`'s layer resolution is written three ways in one expression** (a leftover
  from probing which `dataset` spelling worked). Pick one.
- **Font is a fallback, not Siemens Sans.** If the real metrics differ, every text rect measured so
  far is wrong. Lane B's first job after promotion.

## The regression baseline

`research/experiment2/{direct,tooled,rules-only,hybrid}/` — four complete screens, independently
authored, with four **known** scorecards recorded in `research/hmi-research.txt` §3.10. Wire them as
golden tests during promotion. The baseline exists already and was not created for the purpose.
