# The Unified HMI store on disk — why there is no export, and the one thing it buys us

**2026-08-10 [LIVE, filesystem only — no Portal, no Openness, no approval needed].**

Prompted by the owner's question: *if a library type carries no content, is the content referenced
somewhere else?* It is. That answer explains the export wall, and it hands us one capability that
every previous document listed as impossible.

Project names and object names are **genericized** per `docs/13-data-boundary.md`.

---

## 1. The library type is a POINTER, not a container

`LibraryTypeVersion.Export` emits a `ContentObject` for `PlcTypeLibraryType` and **nothing** for a
plain `LibraryType` — faceplates *and* images alike (`hmi-faceplate-gap-probe.md` §P13). The reason
is not that the content is withheld. **The content is not in the library object at all.** It lives in
the project's own HMI store:

```
<project>_V20/IM/HMI/I/0/Saved/
    screens/        48 × screen_<id>.rdf         one file per screen
    faceplates/     15 × faceplate_<id>.rdf      one file per faceplate
    graphics/        1 × imageLibrary_<id>.rdf   ONE file for the whole image library
    general/        25 × .rdf
    system/          2 × .rdf
    fonts/          ttf, woff2
    config/         json, level
    device/         pbb
    deltas/         per-save delta tracking
```

**103 `.rdf` files in total.** The screen count matched the live project exactly (48 screens, 48
files) at a moment when a probe screen had just been deleted — so the correspondence is **1:1 per
object**, not a bundle.

Note `graphics/` holds **one** file for the entire image library, not one per image. That is the
"stored once, referenced many times" design that makes a pointer-shaped library type inevitable.

## 2. Why there is no Unified screen export

Siemens stores Unified HMI as a **runtime-oriented binary store**, written per object. There is no
document format to export **because the project never keeps one** — the `.ap20` has no
screen-as-document to hand out.

That reframes the wall confirmed five ways in `openness-hmi-api-survey.md` §8. It is not an API
oversight and not a missing feature; it is a consequence of the storage model. Treat it as
**structural and unlikely to move** in a future TIA version.

## 3. Format: binary, with a magic header

```
$ file screens/screen_<id>.rdf
data
$ head -c 200 screens/screen_<id>.rdf | tr -c '[:print:]\n' '.'
RDF.....  ........#... ....................<snip>....<ScreenName>
```

Magic `RDF`, binary body, object names embedded as plain strings. Sizes: screens 3.6–38 KB,
faceplates ~5.8 KB.

🔴 **Do not decode or edit it.** `adr-0007-hmi-engineering-scope.md` already names this format:
*"the `.rdf` runtime format is exactly what [hard rule 7] exists to forbid touching."* It is
undocumented, version-fragile, and unsupported by Siemens. Everything in §4 works **without**
decoding a single byte.

## 4. What it buys: per-screen invariance checking — [MEASURED]

**A change to one object rewrites only that object's file.**

Measured over a real session: roughly **fifteen open/save cycles** on 2026-08-09/10, including
creating and deleting two screens. Of 32 files modified in that window, **every one** was under
`deltas/` or `config/`. **Zero** files under `screens/` or `faceplates/` were rewritten — the newest
screen file remained 2026-07-07, five weeks old.

So a content hash per `screen_<id>.rdf` gives:

> **"These three screens changed; the other 45 are provably identical."**

That is the direct analogue of `converter diff`'s untouched-network invariance check — the S7
guarantee that `docs/notes/hmi-ai-design-options.md` §14e and ADR-0007 both listed as unavailable for
HMI because there is no export. It needs **no decoding, no Siemens support, and no new Openness
capability**, and it works while the Openness whitelist is unapproved.

**Operational detail:** hash `screens/*.rdf` and `faceplates/*.rdf` only. `deltas/` and
`config/config.level` churn on **every** save, so a whole-directory hash is worthless.

### Not yet established

- **Idempotence under edit-and-revert.** Untouched-object stability is measured; whether editing a
  screen and reverting it restores the same bytes is **not**. If the file embeds a timestamp or a
  monotonic id, a reverted screen would hash differently — that weakens the check from "provably
  identical" to "provably untouched", which is still useful but is a different claim.
- **Id → name mapping.** Files are `screen_1006.rdf`, not `screen_HomeScreen.rdf`. The name is
  embedded in the file and Openness lists screens, so the mapping is tractable — but a rename would
  keep the id, which is a feature (identity survives renaming), not a bug.
- Whether a **delete** removes the file promptly. The counts matched after a delete, so probably yes.

## 5. Faceplate authoring — the ways around decoding

The owner's question: *is decoding inevitable for full faceplate authoring?* **No — there are four
routes that do not touch the binary, and decoding should be the last of them, not the first.**

1. **`CreateFromDocuments` with a hand-authored `ContentObject`.** **Untested, and the best lead.**
   Export and import are separate code paths; nothing requires both to exist. We now hold two real
   worked examples of the wrapper schema from the same TIA version — one with a `ContentObject`, one
   without — so the document shape is known. If the importer accepts content the exporter never
   emits, authoring is solved without decoding anything.
2. **Do not author types at all — author SCREENS and open them as popups.** Fully supported, fully
   creatable, fully diffable. The reference project already does exactly this for its equipment
   holders, using a screen window with an `Expression` dynamization, and opens faceplates with
   `UI.OpenScreenInPopup` / `UI.OpenFaceplateInPopup` from script. A screen-based "faceplate" needs
   no library type, and everything about it is inside measured write capability.
3. **Custom Web Controls as the reuse unit** (`hmi-web-tooling-research.md` §Q1). Plain files —
   `manifest.json` plus HTML/JS/CSS — installed by copy, diffable, version-controllable, and
   authorable by exactly the toolchain an AI is best at. `HmiCustomWebControlContainer` is in the
   creatable list; it requires the two-argument `Create<T>(name, containedType)` overload, which
   `openness-cli` now supports but has **not yet run**.
4. **Human authors the type once; the AI stamps instances.** Instantiation is measured and works
   (§P11), the compile gates the wiring structurally (§P12), and authoring never arises.

**Decoding `.rdf` would be the only route to programmatically authoring a faceplate TYPE with novel
content, if all four above fail.** Even then it should be argued in an ADR before a line is written:
it is reverse-engineering an undocumented format that Siemens can change in any update, on the write
side, where a mistake corrupts a private project. Routes 1–3 each deliver the *capability* the type
was wanted for, without that exposure.

---

## Where this sits

| Document | Relationship |
|---|---|
| `hmi-faceplate-gap-probe.md` §P13 | the library-wide export comparison this explains |
| `openness-hmi-api-survey.md` §8 | "no screen export", confirmed five ways — §2 above gives the mechanism |
| `hmi-ai-design-options.md` §14e | listed the invariance check as unavailable; §4 above supplies it |
| `adr-0007-hmi-engineering-scope.md` | names `.rdf` as forbidden to touch; nothing here touches it |
