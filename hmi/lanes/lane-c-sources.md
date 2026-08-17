# Lane C — Sources

**Owns exclusively:** `docs/notes/hmi-svg-contract.md`, `docs/notes/hmi-template-suite-numbers.md`
**Portal:** never · **Wave:** 1 · **Size:** S · **Blocks:** nothing (Lane A consumes its output as a patch)
**Depends on:** nothing

## The retrieval method that works — use it, do not rediscover it

> **`support.industry.siemens.com` returns HTTP 403 to automated fetch.
> `cache.industry.siemens.com` serves the same PDFs and does not.**

That is how the Openness system manual and both Template Suite manuals were read during the
research session. For large PDFs that defeat a fetch tool, `pdftotext -layout <file> - | grep …`
piping to stdout works and writes nothing to disk.

## Jobs

### P3 — SVG restrictions [XS]

Retrieve **"Restrictions on SVG graphics (RT Unified)"** and **section 2.4.4 "Functions not
supported by SIMATIC WinCC"** of Siemens document 109782045. Both defeated retrieval during the
research session; the cache host is the untried route.

This is the contract for the *second* converter (dynamic SVG). Without it, that converter would be
written against guesses.

### P4 — Dynamic SVG binding syntax [XS]

Verify at **primary source** the syntax currently held at `[SEARCH]` tier — meaning: found in search
summaries, never confirmed:

```
<hmi:paramDef name="ShowElement" type="boolean" default="true"/>
types: boolean | number | string | HmiColor
hmi-bind:display="{{ParamProps.ShowElement ? 'inline' : 'none'}}"
```

**This is the contract.** A converter built on unverified syntax is a converter built on nothing.

### Q9 — Siemens Excel Importer/Exporter fidelity [XS]

Siemens 109792619. Still 403 at the time of writing; retry via the cache host. It is one of the two
existence proofs that a screen **serialiser** is feasible despite there being no screen export
function, so its actual fidelity matters to Lane P's T6 — specifically, *what it manages to
round-trip and what it drops.*

### Template Suite number harvest [S]

From the **SIMATIC HMI Template Suite** manuals (V5.0 and V21, both already retrieved and linked in
`research/hmi-research.txt` §11), harvest concrete numbers for Lane A:

- the palette, as hex
- grid pitch
- zone geometry — title bar, process area and alarm banner heights
- the type scale

Lane A is writing rules with experiment 2's provisional values in the meantime. Deliver these as a
patch it can swap in, not as a blocker.

## Reporting standard

**An unretrievable document is a result, not a failure — but only if you say what you tried.** If P3
still defeats retrieval after the cache host, record: the URLs attempted, the response each gave,
and what would be needed (a human with a SIOS login, most likely). A silent "couldn't find it"
leaves the next person to repeat all of it.

Label every finding by evidence tier the way the research document does: `[VENDOR-DOC]` for
primary source, `[SEARCH]` for search-summary tier, `[INFERENCE]` for reasoning. **Do not promote a
`[SEARCH]` claim to `[VENDOR-DOC]` because it was found twice.**

## Done when

- P3 and P4 are answered at primary source, or recorded as unretrievable **with the attempt log**
- Q9 is answered or recorded the same way
- Template Suite numbers are delivered to Lane A as a swap-in patch
- Every claim carries an evidence tier
