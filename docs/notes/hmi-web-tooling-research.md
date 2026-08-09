# Web-tooling carry-over into WinCC Unified HMI — external research

**Date:** 2026-08-09 · **Method:** external web research only (no TIA Portal, no `openness-cli`, nothing built).
**Scope:** how far modern AI-assisted web-development tooling transfers to WinCC Unified HMI engineering on TIA Portal V20.

Every substantive claim is tagged `[VERIFIED — source]` or `[INFERRED]`. Sources are URLs, Siemens SIOS
entry IDs and manual titles. A "could not establish" section is at the end and is meant to be read, not
skipped — this project has been burned by conclusions drawn from single negative results.

**Established facts taken as given (measured live by this project, not re-derived here):** no screen export
for Unified; Unified screens are flat and absolutely positioned; 35 of 56 item types creatable via Openness;
faceplate *types* not authorable; alarm text not writable; HMI device compile is a real reference-integrity
gate.

---

## Verdicts

### Q1 — Custom Web Controls: **yes, this is the hole in the wall. Highest-value finding in the report.**

Custom Web Controls (CWC) exist, are a first-class documented V20 feature with a Siemens system manual and a
V20-specific application example, and are **plain source files on disk**: HTML + JavaScript + CSS + a
`manifest.json`, zipped to `{GUID}.zip` and dropped into the project's `UserFiles/CustomControls/` folder or
the global `…\Portal Vxx\Data\Hmi\CustomControls\` folder. That means a CWC **diffs, hashes, code-reviews and
version-controls exactly like any web project**, because it *is* a web project — the zip is a build artifact,
not the source of truth. Two further facts make this decisive rather than merely interesting. First, Siemens'
own V20 documentation states a CWC "can be displayed as an independent Web page in any browser and on any
mobile device" — so the entire authoring and verification loop (browser, Playwright, pixel diff, unit tests)
runs **outside TIA Portal entirely**, with no multi-minute Portal open in the loop. Second, *inside* the
control's rectangle there is a full HTML/CSS layout engine: flexbox, grid, reflow, text metrics — none of
Unified's flat absolute-positioning constraint applies within a CWC. The costs are real and must be priced in:
CWCs **cannot be stored as library types**, they do not travel with a project copy unless the `UserFiles`
folder goes too, on Unified Comfort Panels they run locally with **no external HTTP, no `fetch`, no
`XMLHttpRequest` and no debugger**, and whether an *instance* can be placed on a screen via Openness is
**unestablished** (installing the *package* needs no Openness at all — it is a file copy).

### Q2 — Runtime: **yes, it is genuinely browser-delivered, and yes, it can be screenshotted.**

Siemens themselves describe the Unified runtime as built on "HTML5, SVG, and JavaScript … also via secure
access using maintenance-free web clients". Unified PC Runtime publishes over **HTTPS on 443** at
`https://<computername>` (not `localhost` — the certificate is issued to the machine name), reached with a
plain Chrome/Firefox and a TIA-configured user with HMI Administrator/Operator roles. Ctrl+Shift+X in TIA
Portal starts the simulator. The screen debugger is **Chrome DevTools Protocol on port 9222** (scheduler on
9224) — official Siemens documentation names the ports, and a third-party open-source CDP proxy for WinCC
Unified exists, which is independent confirmation the protocol is real CDP. So Playwright/Puppeteer
screenshotting of the *real* runtime is on the table, with the caveats that the debugger is local-only, must
be off in production, and login plus a self-signed certificate stand in the way of a naive script. This is the
only route to ground truth for a rendered Unified screen.

### Q3 — Prior art: **crowded, and one competitor has already solved our open problem.**

Three findings matter. (1) **Siemens' own Engineering Copilot / Industrial Copilot already generates WinCC
Unified visualization** — the April 2024 press release says it can "easily create an initial machine or plant
visualization in WinCC Unified", and the current product page lists HMI JavaScript generation and tag
dynamization. (2) **SiVArc** (SIMATIC Visualization Architect, ~£4.4k floating licence) generates HMI screens
from PLC blocks by rule, supports Unified, and is itself Openness-drivable — it is the incumbent
template-driven answer and it is *not* AI. (3) Most importantly, **the "no export" wall has already been
walked around twice**: Siemens application example **109792619** exports Unified screen objects *and their
attributes* to Excel and imports them back, via Openness; and the commercial "TIA Openness Manager" advertises
"WinCC Unified screens export and import as JSON preserving all properties, text content, and dynamizations"
across V15–V21. The wall is a *format* wall, not an *inspection* wall. A canonical screen serialization is
buildable — two independent parties have built one.

### Q4 — Design rules: **the rules exist and are citable, but the standards contain almost none of the numbers, and Siemens' own material argues both sides.**

ANSI/ISA-101.01-2015 is a lifecycle standard: it requires that you *have* an HMI Philosophy, Style Guide and
Toolkit; it does not say what goes in them. The concrete rules come from PAS/Hollifield's High-Performance-HMI
doctrine, the ASM Consortium, and vendor style guides — Rockwell PlantPAx being the only substantial public
palette. The collisions with web-UI defaults are sharp and enumerable: grey background not dark mode; colour
reserved for *abnormal* state; **green-for-running is explicitly "an improper use of color"** and should be
brightness-against-background instead; no gradients, no 3-D, no shadows, no photorealism; **no animation except
alarm-related flashing**; analog indicators with embedded normal/abnormal bands instead of big KPI numbers;
embedded pre-scaled trends; a reserved faceplate zone instead of floating dialogs; a four-level display
hierarchy where Level 1 must *not* look like the plant. The most dangerous finding is that **Siemens
contradicts itself**: the HMI Template Suite commits to flat design with no 3-D effects and no gradients,
while the official WinCC Unified Screen Engineering training deck teaches binding a tag to an animated flame's
size, colouring pipes by contents, and rotating a fan. And the open web is poisoned — a top-ranking "ISA-101"
site publishes a fabricated palette that is verbatim Google Material Design. On numbers: a **2 m viewing
distance implies ~9.3 mm minimum character height, ≈ 44 px on an MTP1900** — roughly **4× larger** than the
10–11 pt that vendor style guides specify for desk monitors. WCAG's 4.5:1 is derived for a self-selected
reading distance on an sRGB display with a *fixed* flare constant; it says nothing about glare, sunlight or
2 m panels, and W3C's own APCA work states WCAG 2.x "cannot be used for guidance designing dark mode".

### Q5 — AI-UI tooling: **reject nearly the whole design-to-code industry; it is built to solve the exact inverse of this problem.**

Builder.io states its product's purpose outright: to "transform flat design structures into code hierarchies",
solving the fact that "Figma designs are typically flat, absolutely-positioned layers that need conversion into
responsive, nested component structures". Every serious entrant — v0, Locofy, Visual Copilot, Stitch, Figma
Make, Framer — is a machine for **destroying absolute coordinates and manufacturing flow layout**. We want the
thing they throw away. Two exceptions have real value. The **Figma REST API** exposes `absoluteBoundingBox` on
every layout node — Figma's document model *is* absolute-positioned with auto-layout as an overlay — so it is
the one viable off-the-shelf design-time IR, at the cost of being cloud-only, paid-seat and rate-limited to
10–20 requests/minute. And **HTML/CSS as an intermediate representation, mechanically flattened through
headless Chrome, is not just sane here but *safer* here than anywhere else**, because the Unified runtime is
itself Chrome: `DOMSnapshot.captureSnapshot` returns the whole resolved box tree in one call, and the usual
font-metric mismatch largely evaporates when the flattener and the renderer share a text engine. The
verification story inverts the obvious plan: with no export and a multi-minute write cycle, **screenshot
diffing must not be the gate** — an approved baseline generated from the same source as the thing it checks is
precisely the correlated check `PlantAutoControl-bench-autopsy.md` was written about. The gate should be a
**render-free geometric linter** over the item tuples (overlap, off-canvas, zero-size, touch-target, text
overflow, near-miss alignment, reading-order acyclicity), which is arithmetic on 200 items, offline,
deterministic and explainable. Pixels are a third-line tripwire. And the research literature supplies the
missing piece: **let the LLM emit quantized integer boxes and repair them with a solver** — LayoutRectifier
does exactly this, model-agnostic and training-free, and LaTCoder measured **+60% TreeBLEU / −43% MAE** from
divide → per-block generate → absolute-position assemble.

---

## Q1 detail — Custom Web Controls

### Existence and version

- CWCs are documented in the TIA Portal V20 Information System under "Programming Custom Web Controls (RT
  Unified)", with sub-chapters: general/folder structure, contract-based interaction and the manifest,
  interaction via the API, extensions, restrictions, installing and using.
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified]`
- The same chapter exists for V21, so the feature is not being retired.
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v21/configuring-screens-rt-unified/overview-of-screen-objects-rt-unified/my-controls-rt-unified/using-custom-web-controls-rt-unified]`
- Siemens system manual: **SIOS 109794040**, "SIMATIC HMI WinCC Unified — Programming Custom Web Controls".
  `[VERIFIED — https://support.industry.siemens.com/cs/document/109794040]` (the PDF itself returned HTTP 403
  to automated fetch; content below is from the Information System HTML, which is the same source text)
- Siemens application example: **SIOS 109779176**, "Integration of Custom Web Controls in WinCC Unified".
  A **V20-specific edition dated 24 October 2025** exists
  (`109779176_CustomWebControls_WinCCUnifiedV20_en_V2.pdf`), alongside earlier V3.0/V4.0 editions.
  `[VERIFIED — https://cache.industry.siemens.com/dl/files/176/109779176/att_1346964/v1/109779176_CustomWebControls_WinCCUnifiedV20_en_V2.pdf]`

### Package format — this is the part that matters

Zip named `{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}.zip`, GUID in braces, taken from `manifest.json`. No
subfolders beyond the two below.
`[VERIFIED — https://github.com/tia-portal-applications/CWC-in-WinCC-Unified/blob/main/docs/engineering_CWC.md]`

```
{GUID}.zip
├── manifest.json          # the contract: properties, methods, events, types
├── assets/                # logo shown in the TIA Portal toolbox (JPG/PNG/ICO/TIFF/BMP)
└── control/
    ├── index.html         # SPA entry point ("start" in the manifest)
    ├── code.js, styles.css, graphics, icons
    └── js/webcc.min.js    # Siemens-supplied bridge — must not be modified
```

Install locations, both plain filesystem paths:

- **Per project:** `<project>\UserFiles\CustomControls\{GUID}.zip`, then Toolbox → "My Controls" → *Update*.
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/installing-and-using-custom-web-controls-rt-unified]`
- **Global, all projects:** `C:\Program Files\Siemens\Automation\Portal Vxx\Data\Hmi\CustomControls\{GUID}.zip`.
  `[VERIFIED — https://github.com/tia-portal-applications/CWC-in-WinCC-Unified/blob/main/docs/engineering_CWC.md]`

**Therefore the package is fully file-based, diffable and version-controllable** — keep the unzipped source
tree in git and treat `{GUID}.zip` as a build output. `[INFERRED, but from verified facts: the payload is
HTML/JS/CSS/JSON text and both install locations are ordinary directories]`

### manifest.json contract

Root keys `mver` and `control`; under `control`: `identity` (name, version, displayname, icon, `type` as
`guid://…`, `start`), `environment.prerequisites.renderingspace`
(min/max/default width and height in px) and `environment.extensions` (e.g. `HMI`, `mandatory`, semver range),
`metadata`, `contracts` (`properties`, `methods`, `events`) and `types` for custom object types.
`[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/contract-based-interaction-and-the-manifest-file-rt-unified/manifest-structure-rt-unified]`

Naming rules: alphanumeric ASCII plus underscore, no leading digit, case-sensitive, no special characters; the
manifest accepts decimal values only, not hexadecimal. Property types are `boolean`, `number`, `string`,
`array` or a custom type declared in `types`.
`[VERIFIED — same page + https://github.com/tia-portal-applications/CWC-in-WinCC-Unified/blob/main/docs/engineering_CWC.md]`

A JSON schema file (`CWC_manifest_Schema.json`) is shipped for editor autocompletion — i.e. the contract is
machine-checkable before anything touches Portal.
`[VERIFIED — https://github.com/tia-portal-applications/CWC-in-WinCC-Unified/blob/main/docs/engineering_CWC.md]`

### Runtime API (WebCC)

- `WebCC.start(callback)` — must be called directly at control start, not nested. `result === true` means the
  bridge is up.
- `WebCC.Properties.<Name>` — read/write the declared properties.
- `WebCC.onPropertyChanged.subscribe(fn)` — callback receives `{key, value}`.
- `WebCC.Events.fire(eventName, …args)` — raises a manifest-declared event into WinCC, usable in Unified
  scripting to move information from client to server.
- Manifest-declared **methods** are invoked *by* WinCC on the control.
- `WebCC.isDesignMode` — lets the control render a design-time preview differently from runtime.
- `webcc.min.js` is Siemens-supplied and must not be edited; it is obtained from a Siemens CWC example, not
  from npm.
`[VERIFIED — https://github.com/tia-portal-applications/CWC-in-WinCC-Unified/blob/main/docs/engineering_CWC.md
 + https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/interaction-between-control-and-container-via-the-api-rt-unified]`

An **HMI extension** exists alongside the base contract, with `Properties` and `Style` objects and
`DatePrecise` / `Big` / `Variant` prototypes; **Formatting** and **Dialog** extensions also exist. The detailed
per-method surface of these extensions could not be extracted.
`[VERIFIED that they exist — https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/extensions-rt-unified]`

### Tag binding and write access

Interface properties declared in the manifest appear in the TIA Portal inspector and are dynamized with
tags exactly like any other screen-item property — PLC or HMI tags.
**Tag access is "Read only" by default and must be explicitly changed to allow writes.**
`[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/installing-and-using-custom-web-controls-rt-unified]`

Note the shape of this: the control does **not** hold a tag handle. It reads and writes *its own declared
properties*, and WinCC does the tag binding. This is a genuinely good boundary — the control is testable
offline by driving its properties directly, with no PLC and no runtime present. `[INFERRED]`

### Restrictions — read these before designing anything around CWCs

Official V20 restrictions page
(`https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/restrictions-rt-unified`)
`[VERIFIED]`:

- **On Unified Comfort Panels the control runs locally, not hosted in a web server.** Links outside the
  control (e.g. `http` links) are not supported. **Retrieval of external data is not supported — no
  `XMLHttpRequest`, no `fetch`.** Debugging a CWC on a Unified Comfort Panel is not supported.
- JavaScript `Number` is IEEE-754 double, so integers beyond ~15 digits round; `DInt` and `Date` tags are
  specifically called out, with the `Big` and `DatePrecise` prototypes as the mitigation.
- With complex types (arrays, UDTs) **only bottom-level elements can be linked to a property**.
- External network access to CWCs may fail for network/security-configuration reasons.

Additional, from the V20 overview page `[VERIFIED —
https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/custom-web-controls-rt-unified]`:

- The control must be a **Single Page Application** — all HTML/JS/CSS must arrive when the page is called or
  be added dynamically during user actions.
- Siemens explicitly does **not** recommend CWCs with 3D representations on Unified Comfort Panels
  (performance).
- The control "can be displayed as an independent Web page in any browser and on any mobile device" — **the
  offline development/verification loop is officially sanctioned, not a hack.**

**CWCs reportedly cannot be saved as library types** (neither global nor project library) — they live only in
the project or on the machine. Attempts to confirm this against the V20 Information System ("My controls (RT
Unified)" and the CWC restrictions page) did **not** find the statement; the only sources are third-party
write-ups, one of them SEO/AI-flavoured. It is consistent with the install mechanism (a zip in `UserFiles`,
not a library object) but it is **not verified**.
`[INFERRED — low confidence; verify in-house before it constrains a design]`

**Project portability trap:** "If you copy the project to another PC, the custom web controls will not be
submitted, and a compilation error will occur" — the `UserFiles` folder must travel with the project, or the
control must be installed globally on the target machine.
`[VERIFIED — https://github.com/tia-portal-applications/CWC-in-WinCC-Unified/blob/main/docs/engineering_CWC.md]`

**Sandboxing:** third-party write-ups claim the CWC runs in a sandboxed `iframe` with Siemens-controlled
`sandbox` attributes that omit `allow-popups` and `allow-forms`, so `window.open()` and form submission fail
silently. This is **plausible and consistent with the official restrictions**, but the only sources found for
it are SEO/AI-flavoured blogs, so it is **not** treated as verified.
`[INFERRED — low confidence; see could-not-establish]`

### Licensing

No separate CWC licence was found. CWCs are described as "freely programmable" extensions; what is licensed is
WinCC Unified itself (separate **Unified PC ES** engineering and **Unified PC RT** runtime licences, PowerTag-
tiered). `[INFERRED — absence of any licensing statement across the Siemens CWC chapter, the system manual
listing and the application example; the Unified licence structure itself is
VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v21/installation/licensing/licensing-of-wincc-unified-options]`

### Openness

- **Installing the package needs no Openness at all** — it is a file copy into `UserFiles\CustomControls\`
  followed by a Toolbox refresh. That is scriptable today with `File.Copy`. `[VERIFIED — install page above]`
- **Whether a CWC *instance* can be created on a screen through Openness could not be established.** No
  Siemens documentation, forum answer or open-source example was found either way. This is directly
  measurable in-house: `openness-cli hmi --schema` already dumps `GetCreationInfos`, so the question is
  "does the creatable-type list include the CWC's type name / a generic custom-control type?" — one cheap
  probe, no new tooling. `[COULD NOT ESTABLISH]`
- Context for why the docs are silent: the V20 Openness "tasks" chapter documents an HMI surface that is
  strikingly thin — creating screen folders, deleting screens/templates, cycles, text lists, graphic lists,
  connections, tag folders/tags, VB scripts. **No screen-item creation, no dynamizations, no faceplates, no
  custom controls, no SiVArc appear there at all** — yet this project has measured screen-item creation and
  dynamization working. The documented surface is a subset of the real one, so **documentation silence is
  not evidence of absence for anything in this area**, in either direction.
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/openness-tasks/introduction]`

### Versioning a CWC across a change

Rolling out a new version is the Toolbox → "My Controls" → *Update* action, which refreshes every instance of
that control across all screens. Three outcomes are reported: updated successfully, updated with loss ("some
properties have been lost due to incompatible changes"), or already up to date. **Renaming a property or
event, deleting one, or changing a data type prevents the automatic update** — those are the breaking changes.
Updates are blocked if the project is open read-only.
`[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/programming-custom-web-controls-rt-unified/updating-custom-web-controls-rt-unified]`

This is a semver-shaped contract with a real migration hazard, so the manifest's `contracts` block should be
treated as a **public API under change control** — exactly the discipline this project already applies to IR
and interface UDTs. `[INFERRED]`

### Ecosystem (evidence the format is real and used)

- `tia-portal-applications/CWC-in-WinCC-Unified` — the official Siemens-org engineering guide, tested on
  WinCC Unified V19, PC RT and Unified Comfort Panels, "not recommended for Unified Basic Panels".
  `[VERIFIED — https://github.com/tia-portal-applications/CWC-in-WinCC-Unified]`
- `tia-portal-applications/TableControl` — officially released through SIOS 109779176.
  `[VERIFIED — https://github.com/tia-portal-applications/TableControl]`
- `tia-portal-applications/GaugeMeter` — the reference CWC: `manifest.json` + `control/` + `assets/`, targets
  "Unified PC station or Unified Comfort Panel", **SemVer** versioning, and notably **no build script at all**
  — packaging is a manual "select the folders, make a zip named `{551BF148-…}.zip`, copy it in".
  A ~20-line packaging script is a real, unfilled gap.
  `[VERIFIED — https://github.com/tia-portal-applications/GaugeMeter]`
- Community CWCs: `CWC-Table`, `CWC-Area-Chart` (ApexCharts), `CWC-Select-element`, `UnifiedScreenControl`,
  `vogler75/winccua-graphql-cwc` (MIT), `vogler75/winccua-mqtt-cwc`.
  `[VERIFIED — https://github.com/topics/wincc-unified]`
- `wincc-cli` on npm — a scaffolding CLI for creating CWCs. `[VERIFIED that it exists —
  https://www.npmjs.com/package/wincc-cli; package page returned 403 to automated fetch, so version,
  licence and download counts are UNVERIFIED]`
- Note the asymmetry the GraphQL CWC exposes: on **Unified PC RT** a CWC evidently *can* make outbound HTTP
  calls (that project routes GraphQL/REST through IIS rewrite rules to Hasura, and even to the ChatGPT API);
  on **Unified Comfort Panel** the official restrictions forbid `fetch`/`XHR` outright. Any CWC design must
  pick a side. `[VERIFIED — https://github.com/vogler75/winccua-graphql-cwc + V20 restrictions page]`

---

## Q2 detail — the runtime, and screenshotting it

### What the runtime actually is

- Siemens, on their own product page: "Ensure optimum usability and homogeneous user interfaces regardless of
  the device due to technologies such as **HTML5, SVG, and JavaScript** — also via secure access using
  maintenance-free web clients."
  `[VERIFIED — https://www.siemens.com/en-us/products/simatic-hmi/wincc-unified/]`
- Same page: "A runtime API provides full read/write access to the WinCC Unified Object Model and RT data via
  standard programming languages", plus OPC UA, MQTT and **GraphQL** interfaces. `[VERIFIED — same]`
- So: not native, not a proprietary vector canvas — a web application. Whether the *panel-side* browser is
  specifically Chromium is not stated by Siemens; the CDP debug port strongly implies a Blink/V8 lineage but
  that is inference. `[INFERRED]`

### Reaching it

- Unified PC Runtime is an **HTTPS site on port 443**, reached at `https://<computername>`. `https://localhost`
  breaks because the generated certificate is issued to the machine name, not the alias.
  `[VERIFIED — https://www.dmcinfo.com/latest-thinking/blog/id/10322/getting-started-with-wincc-unified,
  corroborated by https://www.dmcinfo.com/blog/36696/debugging-scripts-in-wincc-unified/]`
- Login is a TIA-configured user with HMI Administrator and Operator roles. `[VERIFIED — same]`
- Ctrl+Shift+X in TIA Portal transfers the project to the Unified Runtime simulator. `[VERIFIED — same]`
- **Chrome DevTools Protocol:** Siemens' own V20 documentation gives the debugger ports — **screen debugger
  9222**, scheduler debugger 9224, enabled in SIMATIC Runtime Manager's "Scripts Debugger" tab, requiring
  membership of the Windows "SIMATIC HMI" group. Siemens warns "The debugger is only available locally.
  Remote access from the debugger to other devices is not possible" and that it should be **disabled in
  production**.
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/runtime-scripting-rt-unified/debugging-scripts-rt-unified/enabling-the-debugger-rt-unified]`
- Independent corroboration that this is genuine CDP: `ploxc/wincc-unified-debug-proxy`, a Rust CDP proxy that
  polls WinCC's `/json` endpoint and forwards CDP messages on 9222 to local WebSocket servers on 9230/9231 for
  VS Code. A working third-party CDP client is much stronger evidence than a docs mention.
  `[VERIFIED — https://github.com/ploxc/wincc-unified-debug-proxy]`

### Screenshotting — the practical assessment

Playwright/Puppeteer can drive an HTTPS site with a self-signed certificate (`ignoreHTTPSErrors`), perform a
form login, and screenshot. Nothing about Unified blocks this in principle. `[INFERRED — no published example
of Playwright/Selenium driving WinCC Unified was found; see could-not-establish]`

Two distinct routes exist and they answer different questions:

1. **Screenshot the web client over 443.** Gives the *as-rendered truth* of a compiled, downloaded project.
   Requires a runtime licence, a running simulation, and a project download — i.e. the multi-minute cycle
   still applies, but only once per batch of screens, not per screen.
2. **Attach to CDP on 9222.** Gives the live DOM, not just pixels — meaning `document.querySelectorAll` +
   `getBoundingClientRect()` yields **resolved absolute geometry of every rendered element**. That is a
   machine-readable snapshot of a screen obtained *without any export format*, and it is comparable across
   runs. Local-only and must be off in production, so it is a bench/CI instrument, never a site tool.
   `[INFERRED from the verified CDP-on-9222 fact]`

### A third route worth more than either: runtime introspection

The Unified runtime object model exposes screen items to JavaScript — `HMIRuntime.Screens("X").ScreenItems("Y")`
and the active screen's item collection, with per-item `Name`, `Type`, `Left`, `Top`, `Width`, `Height`.
Siemens publishes a scripting tips document (**SIOS 109758536**, "WinCC Unified Tips and Tricks for Scripting
(JavaScript)").
`[VERIFIED that the doc exists — https://support.industry.siemens.com/cs/attachments/109758536/109758536_Unified_TipsScripting_V31_en.pdf;
the specific property list is from secondary write-ups and is INFERRED at the property-name level]`

Plus two non-browser channels into a running system: **OpenPipe** (a Windows named pipe, `\\.\pipe\HmiRuntime`,
JSON messages, callable from Python/C#/Node/PowerShell) and an **ODK** (C#/C++ runtime object model), and the
documented **GraphQL** interface (**SIOS 109826709**, "WinCC Unified GraphQL System Manual").
`[VERIFIED that these interfaces exist — https://www.plc-hmi-scadas.com/en/blog/wincc-unified-cwc-open-pipe/
and https://support.industry.siemens.com/cs/attachments/109826709/GQLWCCUenUS_en-US.pdf; the detailed
OpenPipe message schema was not verified]`

### A fourth route: Siemens ships a screenshot function

`tia-portal-applications/Web-Toolbox_CWC` is a **Siemens-org custom web control with no visible surface** that
exposes browser capabilities to Unified as callable functions, including **`takeScreenshot` — "captures and
downloads PNG screenshots"** — plus `Download` (write a text file to the client), `openNewTab`,
`openNewWindow`/`closeWindow`, `openScreenOnMonitorX`, `checkFullscreen` and `playSound`. It declares
properties (`Screens`, `TabTitle`) and events (`SizeChanged`, `Url`, `Touched`, `UnifiedInContainer`).
`[VERIFIED — https://github.com/tia-portal-applications/Web-Toolbox_CWC]`

Two things follow. First, **a PNG of the real screen can be produced from inside the runtime**, with no CDP,
no debugger and no Playwright — which matters because the debugger is local-only and forbidden in production.
Second, and more importantly for the review problem: an invisible CWC is a **general-purpose escape hatch for
getting data out of a running Unified screen**. A control that walks the runtime object model and calls
`Download` with a JSON dump is the same shape of trick, using only published, Siemens-sanctioned parts.
`[INFERRED from the verified capability list]`

### Deployment surface note

WinCC Unified also runs on **Industrial Edge** ("Unified Edge RT"), deployed to an Edge device and managed
through a "WinCC Unified Web Runtime Manager" with online or offline (project-file upload) download, plus an
**AutoScale** feature for adapting screens to the display. Unified Comfort Panels run a Linux-based runtime.
`[VERIFIED — https://github.com/industrial-edge/wincc-unified-on-IE/blob/main/docs/installation_and_tutorial.md;
the Linux claim for panels is from secondary sources and is INFERRED]`

**AutoScale matters to the "fixed resolution" premise.** Since V17 the Runtime Manager can scale screens
automatically to the display, and "Adapt screen to window" / "Fit screen to window" display modes exist for
screen windows. This is **uniform scaling, not reflow** — the flat absolute-position model is untouched, but a
pixel-diff harness must pin the client viewport or it will chase scale factors.
`[VERIFIED — https://support.industry.siemens.com/forum/ww/en/posts/tip-wincc-unified-v17-autoscale-scaling-the-unified-screens-automatically-to-display/264714/
+ https://docs.tia.siemens.cloud/r/en-us/v20/configuring-screens-rt-unified/basics-rt-unified/managing-screens-in-runtime-settings-rt-unified/defining-the-screen-resolution-rt-unified]`

---

## Q3 detail — prior art

### Siemens Engineering Copilot / Industrial Copilot — the direct competitor

- Siemens press release, **22 April 2024**: the Industrial Copilot is connected to the TIA Portal, does
  "automated code generation in structured control language (SCL): The TIA Portal can take the code suggestion
  directly from the AI", explains SCL blocks, and can "easily create an initial machine or plant visualization
  in WinCC Unified".
  `[VERIFIED — https://press.siemens.com/global/en/pressrelease/siemens-xcelerator-scaling-roll-out-generative-ai-siemens-industrial-copilot]`
- Current product page (Engineering Copilot TIA Standard, redirecting to Siemens' "Eigen Engineering Agent"
  page): SCL **and LAD** code generation, test-logic creation, syntax fixing, bulk property changes,
  documentation; for HMI — "Generating and integrating JavaScript for dynamic HMI behavior" in WinCC Unified,
  VB script migration, tag dynamization support. Delivered as a subscription via Siemens Digital Exchange,
  one-month trial, pricing on request.
  `[VERIFIED — https://www.siemens.com/en-us/products/tia-portal/engineering-copilot-tia-standard/]`
- Note what is *and is not* claimed: "an **initial** visualization", and HMI **JavaScript** rather than HMI
  **layout**. Siemens is not claiming a reviewed, standards-compliant screen. That is the gap this project
  would be filling. `[INFERRED from the wording]`
- Natural-language processing runs on GPT models via Azure OpenAI. `[VERIFIED — press release above]`

### SiVArc — the incumbent non-AI generator

- SIMATIC Visualization Architect is a TIA Portal option package that **automatically generates HMI
  visualisations from the PLC program**, using *generation rules* that say which HMI objects are produced for
  which PLC blocks and devices. `[VERIFIED —
  https://support.industry.siemens.com/cs/attachments/109740350/109740350_SiVArc_GettingStarted_V2_en.pdf
  (title/abstract; the PDF returned 403 to automated fetch) +
  https://www.siemens.com/en-us/products/simatic-hmi/wincc-unified-engineering/]`
- **Supports WinCC Unified**, including Unified Comfort Panels and Unified Edge RT; faceplates can be used as
  layout templates and shared between Comfort and Unified device rules.
  `[VERIFIED at the vendor-summary level; the primary manual (SIOS 109826234 / 109742281) returned 403 or
  reset to automated fetch — treat device coverage as high-confidence but not manual-quoted]`
- **Price:** SIMATIC Visualization Architect V20, article **6AV2107-0PX02-4AH5**, floating licence, listed at
  **£4,391.00** by a UK Siemens distributor.
  `[VERIFIED — https://www.parmley-graham.co.uk/automation/siemens/software/wincc-tia-v20-hmi-amp-scada/6av2107-0px02-4ah5]`
- **Openness-drivable:** the TIA Portal Openness documentation carries a SiVArc Openness section covering
  SiVArc instance properties, copying rules from libraries, finding screen rule groups, and triggering SiVArc
  generation. Siemens also publishes a talk on combining Openness and SiVArc for automated visualisation
  creation.
  `[VERIFIED that the section exists — https://cache.industry.siemens.com/dl/files/886/109826886/att_1163875/v1/TIAPortalOpenness_enUS_en-US.pdf
  (manual is >10 MB, could not be fetched in full — exact class/method names UNVERIFIED) +
  https://www.youtube.com/watch?v=2OElSUSSUo0]`
- **Assessment:** SiVArc is a rules engine, not a designer. It answers "generate the same screen for the 200th
  valve" extremely well and "design a good overview screen for this plant" not at all. It is complementary to,
  not competitive with, an AI designer — and it is the thing an AI designer should *emit rules for* if the
  goal is per-instance scale. `[INFERRED]`

### The export wall has already been walked around — twice

- **Siemens application example SIOS 109792619**, "Automatically creating and exporting HMI screen objects in
  WinCC": an Excel Importer/Exporter that **generates an Excel file containing all HMI objects of a screen
  including their attributes**, and creates/modifies objects from a filled-in Excel table, **through TIA
  Openness**. Its documented failure modes ("No SIMATIC WinCC Unified Comfort Panel or Unified PC
  Configured", "Failure to Join the Openness Group") confirm it is a **Unified** tool. Its contents list
  includes "List of screen object types", "List of attributes" and "Accessing Object Properties via TIA Portal
  Openness". Its section structure is the tell — §2.2 covers *Mapping between Excel table and HMI screen,
  Basic structure, Different objects different properties, List of screen object types, Accessing object
  properties via TIA Portal Openness, **Simple properties**, **Complex properties / compositions**,
  **Dynamizations**, **Events**, **Fonts**, List of attributes*; and §2.3 is **"Command-Based Export with
  ExcelExporter"** with sub-sections for *Export special properties* and **"Export all configured HMI
  screens"**. That is a screen serializer with a command-line entry point, covering exactly the property
  classes a review artifact needs.
  `[VERIFIED — https://cache.industry.siemens.com/dl/files/619/109792619/att_1355001/v1/109792619_Excel_Importer_Exporter_V2_1_1.pdf
  — structure and headings read from the PDF; the per-attribute coverage tables would not extract, so
  round-trip *fidelity* remains unproven]`
- **TIA Openness Manager** (commercial, tiaopenessmanager.ch): "WinCC Unified screens export and import as
  JSON preserving all properties, text content, and dynamizations." Covers screens and popups, screen
  templates, HMI tag tables, VB script functions, connections, text lists, graphic lists. TIA V15–V21. Tiers:
  free (1 file per operation), CHF 109.99/yr, CHF 330/yr. Ships an **MCP server with 22 tools** over stdio,
  explicitly listing Claude Code as a client.
  `[VERIFIED — https://tiaopenessmanager.ch/en]`
- **Copia** (git-based source control vendor) is the independent negative confirmation, and its wording is
  precise: "Since WinCC Unified is not supported for export by the Siemens TIA Openness API, WinCC Unified HMI
  details are not supported for display/differences." Comfort/Advanced/Professional screens *are* supported.
  `[VERIFIED — https://docs.copia.io/docs/git-based-source-control/supported-vendors/siemens/s7tiaportal]`
- **Reading these together is the important bit.** There is no *export function*. There is nothing preventing
  a *serializer* built on the same property-walk our `openness-cli hmi` already performs — and Siemens
  themselves ship one, and a vendor sells a JSON one. The wall is a missing file format, not a missing
  capability.

### TIA V21 — does the wall fall?

- V21 introduces **SIMATIC Source Documents (Simatic SD)**, a text-based export format (`.s7dcl` for code,
  `.s7res` for comments/i18n) explicitly aimed at Git, first appearing in V20 Update 4 and with full
  import/export in V21.
  `[VERIFIED at third-party level — https://controlbyte.tech/blog/tia-portal-v21-new-features/ +
  https://antomatix.com/siemens-tia-portal-v21-2025-whats-new/]`
- **It covers PLC code only.** Siemens' own V21 Information System page is titled "Exporting and importing
  blocks in SIMATIC SD format (S7-1200, S7-1500, S7-1200 G2)", sits under "Creating and managing blocks", and
  states: "You can convert blocks of the LAD, FBD and SCL programming languages, data blocks and PLC data
  types into text form by exporting them in SIMATIC SD format." No HMI or WinCC object is in scope.
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v21/creating-and-managing-blocks/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500-s7-1200-g2/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500-s7-1200-g2]`
  **So V21 does not knock the wall down.** Caveat kept deliberately: this is a positive statement of scope on
  a PLC page, not a Siemens sentence saying "Unified screens are excluded" — if V21 ever matters commercially,
  re-check against the V21 WinCC Unified and Openness chapters directly rather than trusting this inference.
- V21 also brings a rebuilt **NextGen WinCC Unified Screen Editor** (live previews in screen windows and
  static faceplate containers, snap-to-line/grid, direct text editing, multi-selection resize) and an Alarm
  Indicator widget. Relevant only in that it changes the human's authoring experience, not the API.
  `[VERIFIED at third-party level — same two sources]`

### Other prior art

- `tia-portal-applications/TIA-Openness-Create-Synoptic-Unified` — Siemens-org C# tool that walks PLC networks
  and emits JSON into `<project>/UserFiles/Synoptic` for integration into Unified runtime. Prior art for
  "derive visualisation content from PLC code, ship it as data".
  `[VERIFIED — https://github.com/tia-portal-applications/TIA-Openness-Create-Synoptic-Unified]`
- `tia-portal-applications/TIA-Add-In-ShowScripts` — Siemens-org C# add-in that **exports all JavaScripts of
  screens to readable files** with an Excel overview. Another proof that Unified screen *content* is readable
  and dumpable to files through the API even though no export format exists.
  `[VERIFIED — https://github.com/orgs/tia-portal-applications/repositories]`
- `tia-portal-applications/tia-portal-openness-unified-library` — a Siemens-org .NET wrapper
  (`UnifiedOpennessConnector`) for building Openness tools against Unified. Documented only at
  "copy the DLL and read the DLL docs" level.
  `[VERIFIED — https://github.com/tia-portal-applications/tia-portal-openness-unified-library]`
- **Dynamic SVG** is a *second* file-based extension point and is under-appreciated. A dynamic SVG is an
  ordinary SVG file carrying an `hmi:paramDef` interface in a Siemens XML namespace; parameters become
  properties that dynamize against tags in the inspector, with no scripting. Introduced in **WinCC Unified
  V18** as a library **type** (dynamic SVG types can live in libraries — unlike CWCs); property dynamization
  happens per instance; dynamic SVGs are language-neutral; **SMIL animation is not parsed** and client-side
  scripts are unsupported. Siemens application example **SIOS 109782045**, "Using dynamic SVGs with SIMATIC
  WinCC".
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/wincc-unified-rt-unified/working-with-libraries-rt-unified/using-types-and-their-versions-rt-unified/editing-dynamic-svg-type-rt-unified
  + https://server.svghmi.pro/api/users/download/109782045_WinCC_and_SVG_DOC_en.pdf
  + https://docs.tia.siemens.cloud/r/en-us/v20/configuring-screens-rt-unified/overview-of-screen-objects-rt-unified/graphics-rt-unified/importing-svg-graphics-rt-unified]`
  A commercial dynamic-SVG marketplace exists (svghmi.pro, hmix.tech), which is evidence the format is stable
  enough to sell against. `[VERIFIED — https://svghmi.pro/ , https://hmix.tech/]`
- Open-source web HMI/SCADA worth knowing as *render targets*, not as competitors: **FUXA** — MIT licence,
  ~4.9k stars, Node.js + Angular + HTML5/CSS/**SVG**, a fully web-based SCADA/HMI editor with tag binding
  (Modbus, OPC-UA, MQTT, Siemens S7), and **headless portable binaries** for Windows/macOS/Linux plus Docker.
  Also Rapid SCADA and Scada-LTS.
  `[VERIFIED — https://github.com/frangoteam/FUXA]`
  Relevance: FUXA is an existing, MIT-licensed, SVG-based, absolutely-positioned HMI renderer that runs
  headless. If a preview/review renderer is wanted for an AI-designed Unified screen, this is the closest
  off-the-shelf starting point found. `[INFERRED]`
- Academic: "Automated HMI design as a custom feature in industrial SCADA systems", Procedia Computer Science
  (Elsevier), 2024 — user-guided ad-hoc visualisation generation on top of a SCADA HMI, framed around
  situational awareness. `[VERIFIED that it exists —
  https://www.sciencedirect.com/science/article/pii/S1877050924001789; full text returned 403, so its
  method and conclusions are UNVERIFIED]`
- Patents exist on automatic HMI generation (e.g. US 11,422,833 for vision systems; US 11,734,803 "System and
  method for generating HMI graphics"). Not read; flagged only so nobody assumes the space is clear.
  `[VERIFIED that the patents exist — https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/11734803]`

---

## Q4 detail — the design-rules collision

### Verdict

The rules exist and are citable, but **almost none of the numbers come from the standards**.
ANSI/ISA-101.01-2015 is a *lifecycle and process* standard: it requires that you *have* an HMI Philosophy, a
Style Guide and a Toolkit; it does not tell you what to put in them. The concrete, quantitative rules that
contradict web-UI defaults come from the **PAS/Hollifield High-Performance-HMI doctrine**, the **ASM
Consortium**, and **vendor style guides that publish actual hex values** — Rockwell PlantPAx being the best
public one. Two findings matter more than the rest. First, **Siemens' own material is internally
contradictory**: the SIOS HMI Template Suite commits explicitly to flat design with no 3-D effects and no
gradients, while Siemens' own WinCC Unified training deck teaches binding a tag to the *size of an animated
flame* on an SVG boiler, rotating a fan, and colouring pipes by contents — three of the exact defects the HP-HMI
literature names. **The vendor's own examples are the training data.** Second, the open web is actively
poisoned here: at least one top-ranking "ISA-101 guide" site serves a fabricated "canonical ISA-101 palette"
that is verbatim **Google Material Design 2014 hex values** with "Inter" and "JetBrains Mono" as recommended
fonts. Any RAG over the open web for ISA-101 colours will find that.

### The rules that contradict web-UI defaults

The most valuable table in this report. Left column is what an LLM trained on web/app UI will produce.

| # | Web/LLM default | High-performance-HMI rule | Source |
|---|---|---|---|
| 1 | Dark mode / near-black canvas | Light **grey** background in a **brightly lit** control room. Black backgrounds are a CRT-glare accident; a dark control room "causes a physiological reaction that facilitates sleeping". Light background and high ambient light are recommended **as a pair**. | `[VERIFIED — ASM Consortium, "Why Gray Backgrounds for DCS Operating Displays?" https://process.honeywell.com/content/dam/process/en/documents/document-lists/doc_asm-consortium/white-papers/February%2028%202011%20-%20Why%20Gray%20Backgrounds%20for%20DCS%20Operating%20Displays.pdf]` |
| 2 | Pure white or pure black canvas | Neither — grey. Background luminance is "the main determinant of their eye adaptation level"; neutral grey leaves the whole foreground colour space free for salience coding and doesn't collide with colour-vision deficiency. | `[VERIFIED — same]` |
| 3 | Saturated accent colour for brand/emphasis | "Bright colors are primarily used to bring or draw attention to **abnormal** situations, not normal ones. Screens depicting the operation running normally should not be covered in brightly saturated colors." | `[VERIFIED — PAS/Hollifield & Perez, "The High Performance HMI" v2.0 p.9, hosted by ISA: https://www.isa.org/getmedia/06130a38-f7af-4b35-8c9c-2c34f25c1977/The-High-Performance-HMI-Overview-v2-01.pdf]` |
| 4 | **Green = running, red = stopped** — the single most common LLM HMI output | Explicitly "an improper use of color". Correct coding is **relative brightness against the background**: brighter than background = ON ("a light bulb inside them"), darker = OFF, no sensed status = filled the same as the background. | `[VERIFIED — PAS p.9–10, Fig. 9]` |
| 5 | Reuse red/amber for decoration, headers, hovers | Alarm colours are used "**solely** for the depiction of an alarm-related condition and for no other purpose. If color is used inconsistently, then it ceases to have meaning." Rockwell: "if using red for Priority 1 alarms, then do not use red to represent a running pump." | `[VERIFIED — PAS p.9; Rockwell PROCES-WP023 §8.1 https://literature.rockwellautomation.com/idc/groups/literature/documents/wp/proces-wp023_-en-p.pdf]` |
| 6 | Colour alone conveys state | "Color, by itself, is never used as the sole differentiator of an important condition or status." Alarms **redundantly coded** by colour *and* shape *and* text. Named deficiencies: red-green, white-cyan, green-yellow. | `[VERIFIED — PAS p.9–10]` |
| 7 | Code alarms by **type** (temp/pressure/flow) | "redundantly coded **by priority, not type**". | `[VERIFIED — PAS p.19–20]` |
| 8 | Gradients on buttons, cards, headers | "Gradient colors should not be used." Siemens hit this in its own product: "To configure buttons **without a color gradient**, text fields were used." | `[VERIFIED — Rockwell §8.1; Siemens SIOS 91174767 HMI Template Suite manual V3.1 §2.6.5 https://cache.industry.siemens.com/dl/files/767/91174767/att_1106605/v2/Final_91174767_HMITemplateSuiteUnified_V31_DOC_en.pdf]` |
| 9 | Drop shadows, bevels, rounded cards, 3-D depth | "Low-contrast depictions in **2-D, not 3D**." Rockwell: "Avoid using color, gradients, three dimensional images, cutaways, and animations." Siemens: "flat design… refrains from using three-dimensional effects (shadows or textures)." | `[VERIFIED — PAS p.6; Rockwell §8.8; Siemens SIOS 91174767 §2.4.1]` |
| 10 | Photorealistic/skeuomorphic equipment | The named failure mode: a flashy graphic "dedicates 90% of the screen space to the depiction of 3-D equipment… the information actually used by the operator only makes up 10%". HP graphics are "generally non-schematic except when functionally essential". | `[VERIFIED — PAS p.3–4, p.6]` |
| 11 | Animation, transitions, hover states, spinners | "**No animation except for specific alarm-related graphic behavior.**" Explicitly bad: "Spinning pumps/compressors, moving conveyors, animated flames." | `[VERIFIED — PAS p.6]` |
| 12 | Flashing used decoratively, or never | The one sanctioned animation: flash **while unacknowledged**, stop on acknowledge, persist while the condition holds — because "People do not detect color change well in peripheral vision, but movement, such as flashing, is readily detected." **Text itself must not blink** (blink a border or adjacent object) and the operator must be able to stop it. | `[VERIFIED — PAS p.10; Rockwell §8.2]` |
| 13 | Big bold numbers as the hero element (KPI cards) | "All Data, No Information." Replace with **analog indicators** carrying normal range, abnormal range, alarm range and interlock threshold on the scale — "the knowledge of what is normal is embedded into the HMI itself." | `[VERIFIED — PAS p.7–8, Figs. 6–7]` |
| 14 | Bar charts for level/position | Moving-pointer elements are better: "as the bar's value gets low, the bar disappears. The human eye is better at detecting the presence of something than its absence." | `[VERIFIED — PAS p.14, Fig. 14]` |
| 15 | Big saturated fill for tank level | "Vessel levels should not be shown as large blobs of saturated color. A simple strip depiction showing the proximity to alarm limits is better." | `[VERIFIED — PAS p.13]` |
| 16 | Trends "available on click" | Trends **embedded and pre-scaled**, showing normal *and* abnormal bands, correct history on open. "A properly scaled and ranged trend may take **10 to 20 clicks/selections** to create." Soft cap **3–4 traces** per trend. | `[VERIFIED — PAS p.12–13, p.22]` |
| 17 | Floating/modal dialogs over content | A **reserved faceplate zone** — the faceplate "appears in a reserved 'faceplate zone' rather than floating around the screen obscuring the graphic. All control manipulation is accomplished through the standardized faceplates." | `[VERIFIED — PAS p.17, p.24]` |
| 18 | Flat navigation, one screen per asset | **Four-level hierarchy** with progressive disclosure; flat sets are the named defect ("like a computer hard disk with one folder for all the files"). L1 Operation Overview (**no control interactions**), L2 Unit Control, L3 Unit Detail, L4 Support/Diagnostic. This one *is* in the standard — ISA-101.01-2015 clause 6.3, Figures 3–6 "Sample Level 1/2/3/4 Display". | `[VERIFIED — PAS p.15–18; ANSI/ISA-101.01-2015 TOC preview https://www.grahamnasby.com/files_publications/ANSI-ISA-101-01-2015_TOC-excerpt.pdf]` |
| 19 | "Make it look like the plant" / P&ID replica | L1 must **not**: "But it doesn't look like a power plant?! Correct! Does your automobile instrument panel look like a diagram of your engine?" | `[VERIFIED — PAS p.16]` |
| 20 | Colour-code pipes by contents; large unit callouts | Both listed as defects. Rockwell: engineering units in a **smaller, lighter grey** (#919191) than the value. | `[VERIFIED — PAS p.6; Rockwell §8.1, §8.9]` |
| 21 | Decorative chrome, "every pixel beautiful" | "**every pixel has a purpose**"; non-data pixels that must stay "should be visually muted so they do their job without attracting attention." | `[VERIFIED — Rockwell §6.2]` |
| 22 | Colour for emphasis | "Use **line thickness** for emphasis rather than color." | `[VERIFIED — Rockwell §8.1]` |
| 23 | Black body text | "Use **dark gray, not black** text" (#3F3F3F). | `[VERIFIED — Rockwell §8.1, §8.7]` |
| 24 | Hide disabled controls | **Grey out, do not hide** — hiding "could create confusion when users are unable to find the control." | `[VERIFIED — Rockwell §8.3]` |
| 25 | One toggle button that flips state | Two **separate** command buttons ("Run" / "Stop") — "prevents unintended operation by users that press the control more than once." | `[VERIFIED — Rockwell §8.15.2]` |
| 26 | Free-flowing layout | Flow depiction is **directional and fixed**: left to right; vapours up, liquid down; consistent entry/exit points; minimise crossing lines. | `[VERIFIED — Rockwell §8.8; PAS p.6]` |
| 27 | Centre-aligned numbers | Numeric data for comparison: **right-justified, decimal points aligned**; labels left-justified; no centre justification except in buttons/tables. | `[VERIFIED — Rockwell §6.8]` |
| 28 | "Colour is free" | Colour is "the most **overused and abused** attribute in display design", and the listed motives are exactly the LLM's: "to make the display 'prettier,' to keep the display from being 'dull,' to make the display more 'realistic' or **more like a phone 'app.'**" | `[VERIFIED — Rockwell §8.1]` |

**Honest counterweight worth carrying:** the grey-background consensus is practice-derived, not experimentally
settled. The Center for Operator Performance's Phase I study (2015, Pacific Science & Engineering) found "a
lack of empirical evidence which conclusively demonstrates that gray background optimally reduces eyestrain",
tested grey (227,227,227) against light blues, and **Phase II was never done**.
`[VERIFIED — https://centerforoperatorperformance.org/projects/background-color-hmi]`

### Quantitative thresholds

**The only substantial public palette found is Rockwell PlantPAx (PROCES-WP023)** `[VERIFIED]`:

- *Structure/greys:* display background **#E0E0E0**, behind-tabs #C0C0C0, grouping box #E8E8E8, separator
  #D8D8D8, process/connector lines and equipment borders **#A0A0A4**, titles/headings **#3F3F3F**,
  engineering units **#919191**, nav button fill #C6C6C6 / border #AAAAAA.
- *Alarm priority* (note magenta for lowest): Urgent **#E22028**, High **#EC8629**, Medium **#F5E11B**,
  Low **#916AAD**, program error/fault #000000, warning #3F3F3F. White foreground except on yellow (#3F3F3F).
- *States:* off/de-energised/idle/stopped/closed **#808080**; on/energised/running **#F0F0F0**;
  disabled/out-of-service #808080; manual jog & transition #93C2E4; dynamic data value #475CA7.
- *Line weights:* primary process lines **3 px**, secondary **1 px**.

**Text height vs viewing distance.** ISO 9241-303:2011 clause 5.5.4 (character height) and 5.5.2 (luminance
contrast) are normative; the preview confirms a design viewing distance ≥ 300 mm and notes "for presentation
tasks or projection, the preferred viewing distance is still larger (typically 2 m to 10 m)".
`[VERIFIED — https://cdn.standards.iteh.ai/samples/57992/bddfd91165b444f6b9815a6993feadc5/ISO-9241-303-2011.pdf]`
The requirement values sit past the preview cut, so the commonly reported **minimum 16 arcmin, capability
20–22 arcmin** is `[VERIFIED against secondary reporting only — primary paywalled]`.

Worked character heights (uppercase cap height, *not* font size), h = 2·d·tan(θ/2):

| Viewing distance | 16′ (min) | 20′ | 22′ | 1/200 rule (= 17.2′) |
|---|---|---|---|---|
| 0.5 m | 2.33 mm | 2.91 mm | 3.20 mm | 2.50 mm |
| 1.0 m | 4.65 mm | 5.82 mm | 6.40 mm | 5.00 mm |
| **2.0 m** | **9.31 mm** | **11.64 mm** | **12.80 mm** | **10.00 mm** |

The folk "1/200 of viewing distance" rule computes to 17.2 arcmin — inside the ISO band, so it is very likely a
rounded restatement of the same ergonomics rather than an independent rule. `[INFERRED, arithmetically exact]`

**Converted to pixels on real Siemens panels.** Active areas are published
`[VERIFIED — https://docs.tia.siemens.cloud/r/unified_comfort_panels_enus_20/technical-information/technical-specifications/mtp1500-mtp1900-mtp2200-unified-comfort]`:
MTP1500 344×193 mm @1366×768 → **0.252 mm/px (101 ppi)**; MTP1900 409×230 mm @1920×1080 →
**0.213 mm/px (119 ppi)**; MTP2200 476×268 mm @1920×1080 → **0.248 mm/px (102 ppi)**.

So on an **MTP1900 read at 2 m**, minimum legible cap height ≈ **44 px**, comfortable ≈ **55–60 px**; at a cap
height of ~0.7 em that is a **font size of ~62–86 px**. On an MTP1500 at 2 m: ~37 px cap height minimum
(~53 px font). At 1 m on an MTP1500 it drops to ~18–25 px cap height.
`[INFERRED from verified inputs; the 0.7 cap-height factor is font-dependent]`

**This number demolishes the vendor defaults, and it is the most useful single figure here.** Rockwell's own
table specifies primary live data at **11 pt Arial bold**, general text 10 pt, units and labels **8 pt**
`[VERIFIED — Rockwell §8.9]`. At 96 dpi, 10 pt ≈ 13.3 px em ≈ 9.5 px cap height ≈ **2.4 mm on a 100 ppi
panel** — correct for a 0.5 m desk monitor and roughly **4× too small for a 2 m panel**. Rockwell says so
without numbers ("a large monitor mounted high on a wall … may need a larger font"); Siemens says the same
without numbers ("The font size used depends on the distance between the operator and the configured operator
panel").

**Touch targets.**

| Source | Value | Status |
|---|---|---|
| ISO 9241-9 | button = breadth of distal finger joint of 95th-percentile male ≈ **22 mm** | `[VERIFIED as reported — https://www.uxmatters.com/mt/archives/2013/03/common-misconceptions-about-touch.php, which argues the standard is obsolete because capacitive screens sense the centroid, not the contact patch, and proposes 6–8 mm from targeting research]` |
| Nielsen Norman Group | **10 × 10 mm** minimum physical size; ~2 mm spacing; MIT Touch Lab fingertips 16–20 mm, thumb ~25 mm | `[VERIFIED — https://www.nngroup.com/articles/touch-target-size/]` |
| Rockwell PlantPAx (touch, incl. gloved hand) | command buttons **40 × 40 px** min; navigation 35 × 35; help/trend nav 32 × 32; screen objects **40 × 40 default, 30 × 30 absolute minimum**; checkbox/radio 30 px touch vs 20 px mouse; **10 px** spacing between command touch objects; 2 px between nav buttons; **4 px** global minimum spacing. Baseline is a 1920×1080 layout. | `[VERIFIED — Rockwell §6.8, §8.15.1]` |
| "12 mm minimum for gloved operation" | Widely repeated, attributed to ISO 9241-411 + NN/g. **No primary source found; NN/g's article does not mention gloves at all.** | `[COULD NOT ESTABLISH — treat as folklore]` |

**The conflict a generator must resolve:** Rockwell's 40 px on an MTP1900 (0.213 mm/px) is **8.5 mm** — below
NN/g's 10 mm floor and far below ISO 9241-9's 22 mm. Rockwell's px figures implicitly assume a ~0.28 mm/px
desktop monitor. **On a high-ppi Siemens panel, re-derive touch targets in millimetres and convert; never copy
the px number.** A ≥ 15 mm floor for gloved use is ≈ **70 px on MTP1900**, **60 px on MTP1500** —
`[INFERRED, engineering judgement, not a cited threshold]`.

Also note the **two-distance problem**: the operator *reads* at ~2 m but *touches* at arm's length
(~0.4–0.7 m). Legibility sizing and hit-target sizing are governed by different distances and must be computed
separately.

**Contrast — where WCAG transfers and where it does not.**

- WCAG 2.x AA 4.5:1, AAA 7:1, large text and non-text 3:1. The 4.5:1 figure is derived as **3:1 × 1.5**, where
  3:1 is the ISO 9241-3 / ANSI-HFES-100-1988 baseline and 1.5 is contrast-sensitivity loss at **20/40 acuity —
  "typical visual acuity of elders at roughly age 80"**. The `+0.05` term is "Typical Viewing Flare", a
  *fixed* ambient-reflection constant.
  `[VERIFIED — https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html]`
- **Where it does not transfer:** the flare term is one constant, not your plant's illuminance; the formula is
  sRGB-bound; it is defined only for **solid text on a solid background** and "does not cover gradient text,
  drop shadows, or outline text"; it exempts non-interactive/disabled components; and it says nothing about
  viewing distance, glare geometry, sunlight, or matte-vs-gloss surfaces. A screen that passes 4.5:1 in a
  checker can be unreadable at 2 m through a dirty panel in daylight.
  `[VERIFIED — same, plus https://www.w3.org/TR/UNDERSTANDING-WCAG20/visual-audio-contrast-contrast.html]`
- **APCA / WCAG 3 is the better fit** for a fixed-palette industrial surface: WCAG 2.x "far overstates contrast
  for dark colors to the point that 4.5:1 can be functionally unreadable when a color is near black", and
  "**WCAG 2.x contrast cannot be used for guidance designing 'dark mode.'**" APCA gives perceptually uniform Lc
  values that scale with font size/weight — Lc 90 body text, Lc 75 columns, Lc 60 content minimum, Lc 45
  headlines. `[VERIFIED — https://github.com/Myndex/SAPC-APCA/blob/master/documentation/WhyAPCA.md]`
- **ISO 9241-303:2011 is the industrially relevant one** and its clause list is directly on point: 5.2.2
  Illuminance, 5.2.3 Display luminance, 5.2.4 Luminance balance and glare, 5.2.5 Luminance adjustment, 5.4.11
  Unwanted reflections, 5.5.2 Luminance contrast, 5.6.2 Luminance coding, 5.7.3 Contrast for object legibility,
  Annex D (normative). Verified requirement text from the preview: task areas viewed in sequence should sit
  between **0.1 L and 10 L** of average screen luminance; the office example gives **100–150 cd/m² at 500 lx**.
  `[VERIFIED — iTeh preview, link above]`

**Alarm-system numbers.** ISA-18.2 Table 1, per operating position, based on ≥30 days of data
`[VERIFIED — https://www.exida.com/articles/ALARM-MANAGEMENT-AND-ISA-18-A-JOURNEY-NOT-A-DESTINATION.pdf]`:

| Metric | Very likely acceptable | Max manageable |
|---|---|---|
| Alarms/day | ~150 | ~300 |
| Alarms/hour (avg) | ~6 | ~12 |
| Alarms/10 min (avg) | ~1 | ~2 |

Plus: ≤10 alarms in any 10-minute period; <1% of hours with >30 alarms; <1% of 10-min periods with >10 alarms;
<1% of time in flood; top-10 alarms ≤1–5% of load; chattering/fleeting zero; stale <5/day. **Priority
distribution ~80% Low / 15% Medium / 5% High** for three priorities, and ISA-18.2 recommends **no more than
three or four priorities**. EEMUA 191 aligns (<1 alarm/10 min steady state; ≤10 in the first 10 min after an
upset; flood = a 10-min period with >10 new alarms); **Edition 4 published November 2024**
`[VERIFIED — https://www.eemua.org/products/publications/print/eemua-publication-191]`. ISA-18.2 and
IEC 62682 are technically aligned.

**Machinery-standard colour semantics — and a collision to guard against.** IEC 60073 assigns **red** =
emergency/danger/fault, **yellow/amber** = abnormal, **green** = normal/safe, **blue** = mandatory action,
**white** = neutral information; IEC 61310-1 covers visual/acoustic/tactile safety signalling on that basis.
`[VERIFIED as to scope and mapping via secondary sources — https://www.sis.se/en/produkter/standardization/colour-coding/iec600732002/ ,
https://standards.globalspec.com/std/263967/IEC%2061310-1 ; primary text paywalled]`
**IEC 60073 says green = normal/running; HP-HMI doctrine says never colour a normal state green.** These are
different domains (pilot lamps and actuators vs. process graphics) and a generator that blends them produces
exactly the wrong thing. This needs to be an explicit rule, not an emergent behaviour.

### Siemens-specific guidance

1. **A real Siemens HMI style guide exists — SIOS 81318674**, "HMI-Projecting style guide based on SIMATIC
   WinCC Unified, TIA Portal" (EN/DE). **Content could not be retrieved** (SIOS returns 403 to automated
   fetch on both `/cs/document/` and `/cs/attachments/`). From search snippets it covers graphic types, HMI
   data types and naming prefixes for screen and faceplate objects, framed around "reusable and
   high-performance project engineering" — i.e. it reads as a *naming/reuse* style guide, not a visual one.
   Direct attachment URL for a manual retrieval:
   `https://support.industry.siemens.com/cs/attachments/81318674/81318674_HMI_Styleguide_DOC_v10_en.pdf`
   `[COULD NOT ESTABLISH content — retrieve by hand]`
2. **The HMI Template Suite (SIOS 91174767) is Siemens' de-facto visual design system for Unified**, and it
   *was* extracted `[VERIFIED — manual V3.1, 06/2022]`:
   - "**The entire project is created in flat design. Flat design is a minimalist style that refrains from
     using three-dimensional effects (shadows or textures). This facilitates configuration and provides
     clarity for operators, as the focus remains on the content.**" — Siemens on the record against
     3-D/shadow/texture.
   - Published palette. Main: accent blue **0,161,209**; anthracite **38,39,41** (main navigation); dark grey
     **72,73,78** (title bar); grey 181,190,197 (inactive button); light grey 205,211,215 (MainWindow); light
     grey 240,242,243; white 255,255,255 (status bar, content board). Status: Warning 1 **234,206,33**;
     Warning 2 **231,121,16**; Alarm **222,56,88**; **Status OK 94,209,173**; complementary dark blue 0,80,104.
     Text 133,147,153.
   - **Two direct HP-HMI violations inside Siemens' own palette:** the accent colour is used for the title bar
     and *active buttons* (saturated colour on normal-state chrome), and there is a dedicated **"Status OK"
     mint green** — a saturated colour assigned to a *normal* condition, which is rules 3–5 above.
   - Layout: MainWindow ≈ **70%** of display area; edges reserved for navigation and global functions; entry
     fields marked by a **white background** (no white background ⇒ not editable).
   - Font: **Siemens Sans** for all text and process values. **No font sizes given anywhere.**
   - Template resolutions: 800×480 (MTP700), 1280×800 (MTP1000/1200), 1366×768 (MTP1500),
     1920×1080 (MTP1900/2200).
   - The Suite's Wizard drives TIA Portal **via Openness** and requires "Siemens TIA Openness" group
     membership — the same path `openness-cli` uses.
3. **Siemens' own training material teaches the anti-patterns.** The "WinCC Unified Screen Engineering"
   hands-on deck (usa.siemens.com, Unrestricted © Siemens 2024) walks the student through adding an SVG boiler
   with a flame and binding a tag to **FlameSize**, binding a tag to **PipeColor** by range, and **rotating a
   fan graphic** from a slider — animated skeuomorphism, contents-coloured piping and decorative motion.
   `[VERIFIED — https://www.gothightech.com/resources/labs/WinCC%20Unified%20Lab03%20Screen%20Engineering.pdf]`
   **This is the strongest single argument for an explicit rules layer in front of any generator.**
4. **What a project can restyle.** Unified has a **style** system: predefined styles plus **custom styles
   authored in "Corporate Designer"**, imported per device version, applied in Runtime settings and
   switchable at runtime from a user-defined function.
   `[VERIFIED as to mechanism — https://docs.tia.siemens.cloud/r/en-us/v21/configuring-screens-rt-unified/basics-rt-unified/managing-screens-in-runtime-settings-rt-unified/using-a-style-rt-unified
   and .../custom-styles-rt-unified]` The predefined style names and the exact property surface a style
   controls could not be extracted (JS-rendered docs). **This matters:** if a style can carry the palette, a
   generator should emit a *style*, not per-item colours.
5. **Siemens iX** (`ix.siemens.io`, `github.com/siemens/ix`) is real, **MIT-licensed**, Siemens AG, Stencil.js
   web components with React/Angular/Vue/Blazor wrappers; status tokens named Alarm/Critical/Warning/Success/
   Info/Neutral, chart tokens, border and effect tokens **including gradients**, light/dark themes, and a
   warning that "status colors" fail contrast in text. **But it is a design system for industrial *web
   applications*, not HMI panels** — no mention of WinCC, HMI, control rooms, panels or process visualisation,
   and no ISA-101 claim. **Assessment: not usable as authority, and actively risky** — it is precisely the
   accent-colour/gradient/dark-mode vocabulary, wearing a Siemens badge that an LLM would over-trust.
   `[VERIFIED — https://github.com/siemens/ix , https://ix.siemens.io/docs/home/overview]`
6. Siemens publishes a free **"HMI Design Masterclass"** (7 × ~10-min units), cited twice in the Template Suite
   manual at `new.siemens.com/global/en/products/automation/simatic-hmi/design-masterclass.html`.
   `[VERIFIED that Siemens cites it; content not reviewed — likely the highest-value remaining Siemens source]`
7. **No Siemens statement aligning WinCC Unified with ISA-101 or high-performance HMI was found** — not in the
   Template Suite manual, the Screen Engineering deck, or the iX docs.

---

## Q5 detail — the AI-UI-generation tooling landscape

### 1. Design-to-code / AI UI generation — the direction problem

Builder.io's Visual Copilot was "trained on over 2 million data points" to "transform flat design structures
into code hierarchies", explicitly framing flat absolute positioning as the *input defect* to be removed.
Hosted, Figma-plugin-delivered, no offline mode; the code generator underneath is **Mitosis (MIT)**.
`[VERIFIED — https://www.builder.io/blog/figma-to-code-visual-copilot , https://github.com/BuilderIO/mitosis]`
**Reject: it is a flat→nested converter, and we need nested→flat.** The same verdict applies to the category.

- **v0 (Vercel)** — React/TypeScript + Tailwind + shadcn/Radix. A real Platform API exists (REST + TS SDK from
  an OpenAPI schema, prompt → project → files → deploy, in Vercel Sandbox).
  `[VERIFIED — https://vercel.com/blog/introducing-the-new-v0-api , https://github.com/vercel/v0-sdk]`
  Cloud-only, and **Tailwind utility classes are the worst possible IR here** — the geometry lives inside a
  class-name string that a browser has to resolve anyway. Reject as an IR.
- **Figma REST API — the one genuinely viable design-time IR.** `HasLayoutTrait` carries
  `absoluteBoundingBox` and `absoluteRenderBounds`, where "the x and y inside this property represent the
  absolute position of the node on the page". In the Plugin API `absoluteBoundingBox` is supported on **34
  node types**, `absoluteRenderBounds` on 17; null for invisible nodes; width/height are post-scale/rotation.
  `[VERIFIED — https://github.com/figma/rest-api-spec/blob/main/dist/api_types.ts ,
  https://developers.figma.com/docs/plugins/api/node-properties/]`
  Costs: **rate limits of 10/min (Starter), 15/min (Professional), 20/min (Organization)** on a Dev or Full
  seat, with View/Collab seats getting ~6 per *month*; cloud-only; paid seat.
  `[VERIFIED — https://developers.figma.com/docs/rest-api/rate-limits]`
  And it presupposes a human designing HMI screens in Figma — a workflow change, not a tool.
- **Figma Dev Mode MCP server** — hosted remote endpoint plus a **desktop server running locally through the
  Figma desktop app**, requiring a Dev or Full seat on a paid plan. Its output is React + Tailwind design
  context, not a coordinate tree, so **the REST API is strictly better for this purpose**.
  `[VERIFIED — https://help.figma.com/hc/en-us/articles/32132100833559-Guide-to-the-Dev-Mode-MCP-server]`
- **Anima** — the one mainstream tool that emits absolute coordinates by default ("every element has
  `top: 42px`, `left: 130px`"), reportedly even when the Flexbox option is ticked. What the industry treats as
  Anima's *defect* is our *requirement* — but it is SaaS, Figma-coupled, and the same data is free and
  structured from the REST API.
  `[VERIFIED — https://www.pixelperfecthtml.com/figma-to-code-plugins-anima-vs-locofy-vs-hand-coding/ ,
  https://forum.animaapp.com/t/auto-layout-from-figma-still-shows-absolute-positioning/229]`
- **Locofy** — HTML/CSS, absolute positioning available per element for overlays, but the product is oriented
  at responsiveness; no documented API for the absolute output. Reject.
  `[VERIFIED — https://www.locofy.ai/docs/classic/design-structure/responsiveness/auto-layout/]`
- **TeleportHQ / UIDL** — a genuinely open IR: "a universal format called User Interface Definition Language
  (UIDL), represented by a human-readable JSON document", with a published JSON Schema, "intended to be an
  intermediary format between creation tools and the teleport code generators". **But UIDL is a
  component/element tree with style maps and has no absolute-positioning concept.**
  `[VERIFIED — https://docs.teleporthq.io/uidl/ , https://github.com/teleporthq/teleport-code-generators]`
  Architecturally interesting as precedent for "open JSON IR + pluggable generators" — which is what
  `ir/SPEC.md` already is — but useless as a coordinate source.
- **Galileo AI → Google Stitch** (acquired mid-2025, Gemini-powered). An official **Apache-2.0** SDK exists
  (`google-labs-code/stitch-sdk`) but needs `STITCH_API_KEY` and calls `stitch.googleapis.com`; `getHtml()`
  returns a download URL. Cloud; layout approach undocumented. Marginal.
  `[VERIFIED — https://github.com/google-labs-code/stitch-sdk]`
- **Uizard** — acquired by Miro Labs mid-2024, no public product update since. **Figma Make** — React zip,
  paid tier, no API. **Framer AI** — plugin-bundled React package, positioning undocumented. All reject.
  `[VERIFIED — https://www.banani.co/blog/uizard-ai-review , https://framertoai.com/guides/export-framer-to-react]`

**What transfers from this category: nothing you buy.** The transferable asset is the **Figma node shape** —
`{type, absoluteBoundingBox:{x,y,width,height}, name, fills, characters}` — as a proven schema for a flat
absolute item list. Copy the shape, not the tool. `[INFERRED]`

### 2. HTML/CSS as an IR, mechanically flattened

**Verdict: sane, and materially safer here than elsewhere, because the target renderer is Chrome.**

- **CDP gives the whole box tree in one call.** `DOMSnapshot.captureSnapshot` returns "the full DOM tree of the
  root node (including iframes, template contents, and imported documents) in a flattened array, as well as
  layout and white-listed computed style information for the nodes"; with `includeDOMRects: true` it adds
  `offsetRects, clientRects, scrollRects`, plus `LayoutTreeSnapshot.bounds` (absolute boxes), text boxes with
  character ranges and inline text-node positions. Marked **experimental**. `DOM.getBoxModel` is per-node and
  is the wrong tool for a full tree.
  `[VERIFIED — https://chromedevtools.github.io/devtools-protocol/tot/DOMSnapshot/ ,
  https://chromedevtools.github.io/devtools-protocol/tot/DOM/]`
- **Drivable from .NET.** `PuppeteerSharp` is **MIT**, ships **netstandard2.0 → .NET Framework 4.6.1+** (i.e.
  the same `net48` process as Openness code) and exposes raw CDP via `CDPSession.SendAsync`. Playwright is
  **Apache-2.0**, netstandard2.0, `BoundingBoxAsync()` → `{x,y,width,height}` in float px relative to the main
  frame viewport.
  `[VERIFIED — https://github.com/hardkoded/puppeteer-sharp , https://www.nuget.org/packages/microsoft.playwright/ ,
  https://playwright.dev/dotnet/docs/api/class-locator]`
- **There is no off-the-shelf "HTML → absolute positions" tool.** The nearest is an MCP helper,
  `viewpo_get_layout_map`, which "extracts the DOM layout tree with element hierarchy, bounding rects, and
  computed CSS styles from a URL at a specified viewport width".
  `[VERIFIED — https://glama.ai/mcp/servers/littlebearapps/viewpo-mcp/tools/viewpo_get_layout_map]`
  **Be blunt: this is a ~50-line script, not a dependency.** Walk the DOMSnapshot layout tree, filter to
  leaf/labelled nodes, round to integers, emit tuples.
- **The known failure mode is real and documented:** font rendering differs between machines with identical
  specs, between headless and headed mode, and headless line-breaking differs from desktop. Mitigations:
  `--font-render-hinting=none`, system-level font install, wait for font load before capture.
  `[VERIFIED — https://github.com/microsoft/playwright/issues/20097 ,
  https://github.com/puppeteer/puppeteer/issues/2410 , https://issues.chromium.org/issues/40530343]`
  **Our mitigation is stronger than anyone else's:** the target *is* Chrome, so flattening in Chrome with
  Siemens Sans installed uses the same text engine; Siemens' HMI style "is optimized for the 'Siemens Sans'
  font" and Unified supports downloading custom TrueType fonts to the runtime.
  `[VERIFIED — https://www.siemens.com/en-us/products/simatic-hmi/hmi-template-suite/ ,
  https://docs.tia.siemens.cloud/r/en-us/v20/wincc-unified-rt-unified/configuring-in-multiple-languages-rt-unified/languages-and-fonts-in-runtime-rt-unified/own-fonts-rt-unified]`
  Residual risk, stated by Siemens: "browsers interpret HTML5 differently, it is possible that objects are
  displayed differently depending on the browser and the browser version used".
  `[VERIFIED — https://docs.tia.siemens.cloud/r/en-us/v20/operating-unified-pc-rt-unified/starting-and-displaying-runtime-rt-unified/internet-browsers-for-wincc-unified-pc-rt-unified]`
  That same Siemens page confirms Chrome/Edge/Firefox support and that "Google Chrome has proven to be the
  preferred browser" — **independent Siemens confirmation of the Q2 premise.**

### 3. Constraint / auto-layout solvers that flatten

| Engine | Licence | Runtime | Algorithms | Notes |
|---|---|---|---|---|
| **Yoga.Net** (chenrensong) | **MIT** | Pure C#, net8/9/10, zero native deps, NativeAOT | Flexbox **+ CSS Grid** | Port of Meta Yoga 3.2.1, **833 tests mirroring the C++ gtest suite**, "deterministic layout (no undefined behavior from rounding)", `YGPointScaleFactor` + `YGRoundValueToPixelGrid()`; read out via `YGNodeLayoutGetLeft/Top/Width/Height` `[VERIFIED — https://github.com/chenrensong/Yoga.Net]` |
| **Yoga** (Meta, official) | MIT | C++ with C API, Java/JS bindings | **Flexbox only** — "a familiar subset of CSS, mostly focused on Flexbox" | `[VERIFIED — https://github.com/facebook/yoga]` |
| **Taffy** | **MIT** | Rust | Block + Flexbox + **Grid** | "fast, deterministic layout for custom renderers"; powers Servo, Bevy, Zed, Slint. Python binding via *stretchable*; **no .NET binding** `[VERIFIED — https://github.com/DioxusLabs/taffy]` |
| **stretchable** | **MIT** | Python 3.8+, **Windows wheels** | Block/Flexbox/Grid via Taffy | `get_box(Edge.CONTENT)` → `Box(x, y, w, h)`; **~900 tests, mostly run via Selenium against Chrome** — validated against real browser layout `[VERIFIED — https://github.com/mortencombat/stretchable]` |
| **Cassowary.net** | 🔴 **LGPL** | .NET/Mono | Cassowary incremental linear | Licence is a decision, not a detail `[VERIFIED — https://github.com/jozilla/Cassowary.net]` |
| **CassowaryNET** | licence unstated | pure .NET | Cassowary | 73 commits, maintenance unknown `[VERIFIED — https://github.com/simoncowen88/CassowaryNET]` |

Ranked on *offline + deterministic + integer rects + .NET-reachable*:

1. **Yoga.Net (MIT)** — the standout, and the shortest path in .NET from "LLM writes flexbox-ish structure" to
   integer rects. Caveat: targets net8+, **not net48**, so it lives in a separate tool from the Openness
   binary — which is the correct boundary anyway and matches the existing `converter`/`openness-cli` split.
2. **stretchable / Taffy (MIT, Python)** — best-validated, grid support, Windows wheels.
3. **OR-Tools CP-SAT (Apache-2.0)** — `Google.OrTools` NuGet with a win-x64 runtime package; `no_overlap_2d`
   for rectangle packing. `[VERIFIED — https://www.nuget.org/packages/Google.OrTools/]`
   ⚠️ **Determinism caveat that matters:** the documented position is that single-threaded solving is
   deterministic, **but multiple non-determinism bugs are filed against `num_workers=1` across v9.4/9.5/9.7**.
   `[VERIFIED — https://github.com/google/or-tools/issues/3943 , /3948 , /3590]`
   Pin the version, set `num_workers=1` and a fixed seed, build the model deterministically, **and still
   regression-test that the same input gives the same pixels.**
4. **Z3 (MIT)** — `Microsoft.Z3` NuGet, no dependencies, integer constraints and optimisation. Precedent:
   **InferUI** encodes Android layout synthesis in Z3 with explicit robustness properties "views do not
   overlap" and "views fit on screen". `[VERIFIED — https://pavol-bielik.github.io/data/papers/bielik18-inferui.pdf]`
   Overkill for repair; the right tool if synthesis-under-constraints is ever wanted.
5. **Cassowary/Kiwi — reject.** No maintained .NET port; LGPL or licence-unknown; buys nothing Yoga.Net or
   CP-SAT don't.
6. **gridstack.js (MIT)** — JSON model is `{x, y, w, h}` in *grid units* with `collide()`, `compact()`,
   `save()/load()` and a pluggable `registerEngine()`. Whether `GridStackEngine` runs headlessly without the
   DOM is undocumented and determinism is not claimed. **Useful as a model, not a dependency.**
   `[VERIFIED — https://github.com/gridstack/gridstack.js]`
7. **Squarified treemap** (Bruls/Huizing/van Wijk) — fully specified and deterministic given sorted input.
   Relevant only for auto-partitioning a screen into equipment zones. `[VERIFIED — https://www.win.tue.nl/~vanwijk/stm.pdf]`

**The research finding that should shape the architecture: let the LLM emit boxes, then repair them.**

- Modern layout-generation models represent a layout as exactly our tuple: "element class c and bounding box
  x,y,w,h with **quantized integer coordinates**"; LayoutPrompter and LayoutNUWA use an HTML-based code
  format, LayoutDM a sequence of quantized attributes. LayoutPrompter is **training-free**, in-context
  learning with dynamic exemplar selection plus layout ranking.
  `[VERIFIED — https://arxiv.org/html/2502.14005 , https://arxiv.org/abs/2311.06495]`
- **The four standard metrics — FID, Alignment, Overlap, Max IoU — are "all computable from bounding boxes
  alone, requiring no additional semantic information beyond spatial coordinates and element categories".**
  That is a published, peer-reviewed, render-free scorecard. `[VERIFIED — https://arxiv.org/html/2502.14005]`
  Formulas from Li et al. 2020 `[VERIFIED — https://ar5iv.labs.arxiv.org/html/2009.05284]`:
  - **Overlap:** `Σᵢ Σⱼ≠ᵢ (sᵢ ∩ sⱼ) / sᵢ`
  - **Alignment:** `Σᵢ min(g(Δxᵢᴸ), g(Δxᵢᶜ), g(Δxᵢᴿ), g(Δyᵢᵀ), g(Δyᵢᶜ), g(Δyᵢᴮ))`, `g(x) = −log(1−x)`
  - **Order loss:** `Σᵢ Σⱼ 𝟙[oᵢ<oⱼ] · k(dᵢ − dⱼ)`
  ⚠️ **`ktrk115/const_layout`, the reference implementation, is AGPLv3 — do not vendor it.** Reimplement from
  the published formulas. `[VERIFIED — https://github.com/ktrk115/const_layout]`
- **LayoutRectifier** (arXiv 2508.11177) is the post-processor: model-agnostic, training-free, two stages —
  "grid systems with discrete search to mitigate misalignments", then "a novel box containment function
  designed to adjust the positions and sizes of the layout elements" — fixing misalignment, unwanted overlaps
  and unsatisfied containment while minimising deviation from the generated layout.
  `[VERIFIED — https://arxiv.org/abs/2508.11177]` **This is the "LLM proposes, solver disposes" pattern.**
- **LaTCoder (KDD '25)** validates it with numbers: divide the design into blocks, generate code per block,
  then assemble — **APS (Absolute Positioning): "each block's code wraps in a parent `<div>` with position and
  size set according to BBox coordinates, ensuring strict positional fidelity"** — with a composite MAE+CLIP
  verifier choosing between assembly strategies. Against direct prompting with GPT-4o on CC-HARD:
  **TreeBLEU +60%, MAE −43.23%**; with DeepSeek-VL2: +66.67% / −38.53%; human annotators preferred LaTCoder in
  >60% of cases. `[VERIFIED — https://arxiv.org/html/2508.03560v1]`
- **Counterweight:** LLM spatial reasoning is the weak link — "while LLMs excel at textual reasoning, their
  ability to understand and manipulate spatial relationships remains limited", and integer binning of
  coordinates (≤3 tokens each) plus anchor+deviation encodings outperform raw float emission.
  `[VERIFIED — https://arxiv.org/html/2509.16891 , https://arxiv.org/html/2404.07449v1]`
  **Design the IR so coordinates are grid-unit integers on a coarse grid, not raw pixels.**

### 4. Visual regression and screenshot diffing

**All three SaaS platforms are rejected on data-boundary grounds as much as technical ones** — uploading
deployed plant screens to a third party collides directly with `docs/13-data-boundary.md`'s retention rule.
Percy has no self-hosted mode; Chromatic executes "visual tests in the cloud" and is Storybook-coupled;
Applitools requires an account and API key, and its "Visual AI" is a proprietary, unauditable judge — the
wrong shape for a project whose thesis is decorrelated, explainable checks.
`[VERIFIED — https://www.browserstack.com/docs/percy , https://www.chromatic.com/docs/quickstart/ ,
https://help.applitools.com/hc/en-us/articles/360007189231-The-different-deployment-modes]`

🔴 **The trap for us specifically: `toHaveScreenshot` does not exist in Playwright's .NET or Python bindings.**
Visual comparison is a feature of the **Node test runner**, not the library; the .NET assertions page lists 27
assertions and none is a screenshot assertion, with long-standing open requests. `ScreenshotAsync()` exists
everywhere; the assertion does not.
`[VERIFIED — https://playwright.dev/dotnet/docs/test-assertions ,
https://github.com/microsoft/playwright-dotnet/issues/2433 , https://github.com/microsoft/playwright-python/issues/1833]`
Also: Playwright's docs describe `threshold` (default 0.2) as YIQ colour space while current pixelmatch uses
**OKLab with the HyAB metric** — **do not carry a tuned threshold between the two.**
`[VERIFIED — https://playwright.dev/docs/api/class-pageassertions , https://github.com/mapbox/pixelmatch]`

**However, `ToMatchAriaSnapshotAsync` *does* exist in .NET**, and ARIA snapshots are a YAML representation of
the accessibility tree compared as structured text. That is the right shape — a structural, diffable,
reviewable snapshot instead of pixels. Whether Unified's rendered output carries useful ARIA roles is
unverified. `[VERIFIED — https://playwright.dev/dotnet/docs/test-assertions , https://playwright.dev/docs/aria-snapshots]`

| Tool | Licence | Runtime | AA handling | Verdict |
|---|---|---|---|---|
| **ImageMagick / Magick.NET** | **Apache-2.0** | **netstandard2.0 → runs in the `net48` process** | metrics `AE, RMSE, PSNR, MAE, MSE, NCC, PHASH, SSIM, DSSIM` | **Best .NET fit** — SSIM/DSSIM/PHASH/RMSE from one NuGet, no Node. ⚠️ open bugs on inverted SSIM/DSSIM values — validate direction on a known pair `[VERIFIED — https://usage.imagemagick.org/compare/ , https://github.com/ImageMagick/ImageMagick/issues/8114]` |
| **odiff** | **MIT** | Zig + SIMD, prebuilt Windows x64, CLI + Node API | YIQ NTSC + explicit AA exclusion | Easiest to shell out to; ~6× faster than ImageMagick/pixelmatch full-page `[VERIFIED — https://github.com/dmtrKovalenko/odiff]` |
| **honeydiff** (Vizzly) | **MIT** | Rust + NAPI, prebuilt Windows x64 | **CIEDE2000, default ΔE 2.0**, "conservative anti-aliasing detection for font and sub-pixel rendering noise" | **Best-motivated defaults for UI screenshots**; Node binding only `[VERIFIED — https://github.com/vizzly-testing/honeydiff]` |
| **pixelmatch** | **ISC** | JS, zero deps | Vyšniauskas 2009 detector; **`includeAA` defaults `false`, i.e. AA is already ignored** | Has a **windowed diff** giving localised difference *density* — exactly the discriminator between "a widget moved" (dense cluster) and "fonts differ" (thin scatter along glyph edges) `[VERIFIED — https://github.com/mapbox/pixelmatch]` |
| **PixelMatch.net** | **ISC** | netstandard2.0 → net48 | YIQ-era | ⚠️ last published 2020, pre-v6 `[VERIFIED — https://www.nuget.org/packages/StronglyTyped.PixelMatch.net]` |
| **blazediff** | **MIT** | JS + Rust/SIMD + WASM; **Python bindings** | pixelmatch-derived | Claimed 4.4–4.9× faster than odiff at 4K `[VERIFIED — https://github.com/teimurjan/blazediff]` |
| **reg-suit** | **MIT** | Node CLI; **captures nothing**, consumes an image dir | `x-img-diff-js` "calculates more structural information than naive pixel based comparison" | Reports **inserted or moved regions**; good report layer `[VERIFIED — https://github.com/reg-viz/reg-suit]` |
| **BackstopJS** | MIT | Node + Puppeteer/Playwright | `ignoreAntialiasing` | **Copy its architecture (baselines committed to the repo), not the tool** `[VERIFIED — https://github.com/garris/BackstopJS]` |
| **ssim.js** | MIT | JS | — | 🔴 **archived 7 Dec 2023, read-only. Reject** `[VERIFIED — https://github.com/obartra/ssim]` |
| **Resemble.js** | MIT | JS + node-canvas | `ignoreAntialiasing()` | 🔴 self-declared "ultra low-maintenance mode" `[VERIFIED — https://github.com/rsmbl/Resemble.js]` |
| **Puppeteer** | Apache-2.0 | Node only | none | 🔴 no built-in comparison; dominated by Playwright |

**Perceptual metrics.** SSIM via `skimage.metrics.structural_similarity` with `full=True` returns the full SSIM
*map*, not just a scalar — **always pass `data_range` explicitly**, the auto-guess is documented wrong for
float data. `[VERIFIED — https://scikit-image.org/docs/0.25.x/api/skimage.metrics.html]`
**LPIPS: reject** — it needs PyTorch plus downloaded weights, and it was *designed* to be insensitive to small
perceptually-irrelevant shifts, which is the exact opposite of "did this widget move 3 px?".
`[VERIFIED — https://github.com/richzhang/PerceptualSimilarity]`
ΔE/CIEDE2000: `Wacton.Unicolour` on .NET; `colour-science`/`pyciede2000` on Python.

Matching metric to question:

| Question | Right tool | Wrong tool |
|---|---|---|
| "Did this widget move 3 px?" | **Compare the integers.** Not an image problem at all. | SSIM/LPIPS/pixel count — they *correlate with* the answer instead of *being* it |
| "Something changed — where?" | pixelmatch windowed diff, or reg-suit's moved-region report | a global SSIM scalar (a small widget barely moves it at 1920×1080) |
| "Is this just anti-aliasing?" | pixelmatch AA detection (on by default), or honeydiff ΔE ≥ 2.0 | raw pixel count at threshold 0 |
| "Is the colour wrong?" | **CIEDE2000 ΔE**, in units defensible to an engineer | Euclidean RGB |

**The tuning trap:** every threshold (`maxDiffPixels`, `threshold`, ΔE) is a judgement call with no ground
truth on a target with no export. **A geometry comparator has no knob: 340 ≠ 343 is a fact.**

**Comparing a render against a *specification* — the crux, and the numbers are sobering.**
**WebDevJudge**: 654 paired implementations, expert preference labels, structured rubric tree. Best model
(Claude-4-Sonnet) **66.06% agreement with human experts; human baseline 84.82%; human inter-annotator
agreement with rubrics 89.7%. "No current model achieves a sufficient level of agreement with expert
judgments."** Named failure modes: functional-equivalence rejection, feasibility-verification gaps, and
persistent positional bias despite explicit instructions. `[VERIFIED — https://arxiv.org/html/2510.18560v1]`
Broader MLLM-as-judge calibration: GPT-4V ~70% agreement overall, 79.3% pairwise — **pairwise is consistently
the most reliable format, absolute scoring the least.** `[VERIFIED — https://mllm-judge.github.io/]`

**The decisive detail: code beat the screenshot.** WebDevJudge found "removing code caused larger performance
drops than removing screenshots, suggesting models anchor judgments in structured source code despite
multimodal capabilities". **Give the judge the Openness item list, not just a picture.** And steal the
protocol: the model returns only category-level scores plus short rationales, and **the final score is computed
deterministically outside the model** — never let it do the arithmetic or set the gate.
`[VERIFIED — https://arxiv.org/html/2510.18560v1 , https://www.emergentmind.com/topics/vlm-as-a-judge]`

**Design2Code's metric definitions are the ones worth stealing**, read at source level
`[VERIFIED — https://raw.githubusercontent.com/NoviScl/Design2Code/main/Design2Code/metrics/visual_score.py]`:

- **Block matching** — cost matrix `−similarity(A[i], B[j])`, solved with the **Hungarian algorithm**
  (`scipy.optimize.linear_sum_assignment`), refined by `find_possible_merge()` accepting merges that improve
  cost by >0.05; matches with text similarity <0.5 discarded.
- **Text similarity** — `difflib.SequenceMatcher.ratio()`.
- **Position similarity** — **`1 − max(|x₂−x₁|, |y₂−y₁|)` on normalised block-centre coordinates** (Chebyshev
  distance between matched centres). Four lines of code, and almost exactly the right primitive here.
- **Colour similarity** — RGB→Lab, `delta_e_cie2000()`, then `max(0, 1 − ΔE/100)`.
- **CLIP similarity** — ViT-B/32 with **text regions masked out**, so it scores layout rather than re-reading
  text.
- **Aggregate** — `0.2 × (size + text + position + colour + clip)`.

Reported figures: GPT-4o direct prompting — Block-Match 93.0, Text 98.2, Position 85.5, Colour 84.1,
CLIP 90.4; LLaVA-1.6-7B — Block-Match 50.4. Annotators judged GPT-4V's output could replace the reference page
in 49% of cases and rated it *better* than the original in 64%.
`[VERIFIED — https://www.emergentmind.com/topics/design2code-benchmark , https://salt-nlp.github.io/Design2Code/]`
⚠️ The repo is MIT but "data, code and model checkpoint are intended and licensed for **research use only**".
`[VERIFIED — https://github.com/NoviScl/Design2Code]`

**Skip the Hungarian matching step.** Design2Code matches by text similarity because HTML renders have no
element identity. **We have names.** Match on name; use Hungarian on (type, position) only for unnamed items.
`[INFERRED]`

Other benchmarks briefly: **WebSight** — 823k synthetic screenshot↔HTML/CSS pairs, commercially usable, no
metric defined; notable as evidence that synthetic pairs are cheap, and screens *can* be created
programmatically here, so an in-house corpus is feasible
`[VERIFIED — https://huggingface.co/datasets/HuggingFaceM4/WebSight]`. **pix2code** — 77% token accuracy on a
synthetic DSL, 2017, obsolete. **Web2Code**, **WebCode2M** (2.56M pairs, scale over quality),
**Interaction2Code** (127 pages, 374 interactions — relevant because *no screenshot will ever verify a
`Tapped`/`Loaded`/`CommandFired` handler*), **DesignBench** (900 samples; generation/edit/repair — which maps
onto the `gen-block-new` / `gen-block-modify-*` trio), **UI-Bench** (pairwise + TrueSkill; yields a ranking,
not a gate). `[VERIFIED — arXiv 2406.20098, 2411.03292, 2506.06251, 2508.20410]`

**OCR / element detection — reject as the primary route, and be blunt why:** it is a lossy re-derivation of
information already held exactly. **OmniParser v2 reports 39.6 average accuracy on ScreenSpot-Pro** and its
`icon_detect` model is **AGPL** (inherited from Ultralytics YOLOv8). **Apple Screen Recognition: 71.3% mAP**,
weights never released. **Ferret-UI: research/non-commercial only.** **UIED (Apache-2.0) calls Google Cloud
Vision OCR by default** — an immediate data-boundary violation. Generic YOLO on GUIs "struggled with small or
densely packed GUI elements" — precisely what an HMI screen is.
`[VERIFIED — https://huggingface.co/microsoft/OmniParser-v2.0 , https://arxiv.org/abs/2101.04893 ,
https://github.com/apple/ml-ferret , https://github.com/MulongXie/UIED , https://arxiv.org/abs/2408.03507]`
⚠️ **`Windows.Media.Ocr` is a trap for this project specifically:** it is "only supported for desktop apps with
package identity", i.e. MSIX-packaged — and packaging `openness-cli` as MSIX **changes its `(Path, FileHash)`,
colliding head-on with FI-61's Openness whitelist.**
`[VERIFIED — https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr]`

### 5. What survives contact with this target

**(a) Anything needing a fast iteration loop → reject or move the loop.** XFix's search-based repair needs a
render per candidate fix — a non-starter at multi-minute write cycles.
`[VERIFIED — https://dl.acm.org/doi/10.1145/3092703.3092726]`
**Reject any generate→render→compare→fix loop against TIA.** The adaptation is to run the loop against a
headless-Chrome model of the screen (milliseconds, free), converge there, then pay the Openness write cost
once. LaTCoder's verifier-selects-between-assemblies pattern fits exactly.

**(b) Tools assuming reflow → mostly vacuous, but their *predicates* survive.** ReDeCheck's engine (render at
viewport widths 320–1400 px in 60 px steps, binary-search for breakpoints) is worthless on a fixed-resolution
panel. Its **failure taxonomy** is the valuable part, and three of five transfer
`[VERIFIED — https://github.com/redecheck/redecheck , https://arxiv.org/html/2605.25476]`:

- **Element Collision** — "elements collide into one another due to insufficient accommodation space" →
  static pairwise overlap ✅
- **Viewport Protrusion** — elements "protrude out of the viewable area" → out-of-panel ✅
- **Element Protrusion** — child protrudes out of its container → the screen is the universal parent ✅ (partial)
- **Wrapping**, **Small-Range Layout** — no reflow, no media queries ❌

Critically, ReDeCheck evaluates all of these on **DOM bounding-box geometry, not pixels** — and its authors
publish the honest limitation: it "cannot distinguish between issues that are observable in practice from
those that are not". `[VERIFIED — https://eprints.whiterose.ac.uk/id/eprint/116989/10/c50-3.pdf]`
**That is the false-positive ceiling, stated by the people who built it.** Their fix was to add a pixel stage
(VISER/VERVE); we cannot, so ours must be **conservative predicates plus first-class per-item/per-pair
suppression from version 1** — designed in, not retrofitted.

**(c) Offline, deterministic, scriptable from Windows/.NET → ranks highest:** an in-house geometry comparator,
Yoga.Net, CP-SAT/Z3, Magick.NET, odiff, and Playwright/PuppeteerSharp for the DOM read.

**(d) Validating a layout WITHOUT rendering it — where the value is.**

The pixel-based mainstream does **not** transfer: **OwlEyes** (85% precision / 84% recall detection, 90%
localisation, from screenshots); **Nighthawk** (Faster-RCNN, 0.84/0.84 detection, 0.59/0.60 localisation);
**WebSee** (needs a mock-up image); **GVT** (98%/96%, but takes two images).
`[VERIFIED — https://conf.researchr.org/details/ase-2020/ase-2020-papers/15/Owl-Eyes-Spotting-UI-Display-Issues-via-Visual-Understanding ,
https://arxiv.org/abs/2205.13945 , https://viterbi-web.usc.edu/~halfond/papers/mahajan16icst.pdf ,
https://arxiv.org/abs/1802.04732]`

⚠️ **The number to confront:** Nighthawk's authors implemented a static/view-hierarchy baseline and measured
**precision 0.32, recall 0.36**, naming the cause: *"because the font size in the JSON file cannot be obtained,
the issue that the font is displayed incompletely in EditText cannot be detected."*
`[VERIFIED — https://ar5iv.labs.arxiv.org/html/2205.13945]`
**But that gap is closable here** — font and font size are readable back from each item's attributes, and
advance widths are computable from the TTF with no rendering (`fontTools` hmtx/unitsPerEm, MIT; or
`SKFont.MeasureText` in .NET, with the documented HarfBuzz-shaping caveat).
`[VERIFIED — https://pypi.org/project/fonttools/ , https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skfont.measuretext]`
Note also their corpus composition — component occlusion 47%, missing image 25%, text overlap 21%, NULL value
6%, blurred screen 1%: **~32% of it is asset/render/data failure a geometry oracle structurally cannot see**,
and which here is either impossible or detectable from the *dynamization binding* instead. Their 0.32/0.36 is
a lower bound on a differently-shaped problem.

**dVermin (ASE 2022)** is the useful hybrid: **97% precision / 97% recall page-level, 84%/91% view-level**,
using bounding-box **IoU** for sibling overlap and parent cropping, refined with alpha-channel visible matrices
and SSIM at threshold 0.9. **The IoU predicates transfer; the alpha refinement — which is what buys the 97% —
does not.** A pure-IoU version will fire on every intentional decorative overlap.
`[VERIFIED — https://arxiv.org/abs/2212.04388]`

**X-PERT's Relative Alignment Graph is the best idea in this section.** It detects issues "by comparing the
text content of matching components and the relative layouts of elements by extracting a relative alignment
graph of DOM elements", modelling the page as "a set of potentially overlapping rectangles in a two
dimensional plane". `[VERIFIED — http://shauvik.com/public/pubs/roychoudhary13icse_cr.pdf]`
Encode each screen as pairwise relations (`left-of`, `above`, `x-aligned`, `contains`, `overlaps` — Mystique
publishes a ready-made vocabulary: stack, grid, packing, overlapping, null
`[VERIFIED — https://arxiv.org/pdf/2307.13567]`) and **diff the relation set between two reads. That is
`converter diff` for screens.**

**Reading order is fully solved geometrically.** Breuel's pairwise partial order: *a* precedes *b* if their
horizontal ranges overlap and *a* is above *b*; also *a* precedes *b* if *a* is entirely left of *b* and no *c*
exists whose vertical range lies between them and whose horizontal range overlaps both. Topologically sort.
O(n²) on 200 items is instant, it handles columns natively, and **a cycle in the derived order is itself a
defect finding** — geometrically ambiguous reading order on an operator display, which no published tool
reports. Recursive XY-cut (Ha/Haralick/Phillips 1995) is the alternative, with the known limitation that it
"struggles with complex structures".
`[VERIFIED — https://www.researchgate.net/publication/2564797_High_Performance_Document_Layout_Analysis ,
https://arxiv.org/pdf/2504.10258]`

**Alignment-group detection gives a scalar quality metric.** GRIDS defines an aligned group as elements sharing
a grid line, and "the total number of grid-lines actually utilized in any feasible solution quantifies the
overall alignment". One-dimensional clustering of all left/right/centre-x edges with tolerance τ, then count
clusters. **The derived rule — flag any edge within τ of a cluster of ≥2 but not exactly equal to it — catches
the "off by 2 px" defect a human eye sees instantly and no absolute rule will ever find.** Lineage runs back to
Pavlidis & Van Wyk 1985, which infers "collinearity of sides, and vertical and horizontal alignment of points"
from a rough drawing and repairs to satisfy them.
`[VERIFIED — https://arxiv.org/pdf/2001.02921 , https://dl.acm.org/doi/10.1145/325165.325240]`

**Spatial indexing: don't.** 200 items brute-force is 20,000 comparisons — microseconds. Shapely `STRtree`
(BSD-3) and NetTopologySuite `STRtree`/`Quadtree` (**EPL-1.0 / EDL-1.0 dual**) exist if ever needed.

**There is no off-the-shelf "list of rects → layout problems" library.** The nearest is `qt_layout_check`,
a Qt widget-tree diagnostic scanning for `zero_size`, `no_layout`, `smaller_than_hint`, `text_truncated`,
`overlapping_siblings` — three of five map one-for-one.
`[VERIFIED — https://glama.ai/mcp/servers/0xCarbon/qt-mcp/tools/qt_layout_check]`
Figma's linters check *token conformance*, not geometry validity; the one geometric rule family there is
**4-point grid snapping**. axe-core has exactly one geometric rule, `target-size` — with a documented history
of false positives from overlapping/translucent elements *even with a full render available*.
`[VERIFIED — https://github.com/dequelabs/axe-core/blob/develop/doc/rule-descriptions.md ,
https://github.com/dequelabs/axe-core/issues/4805]`
**This gets built in-house; it is a small job.**

### Touch-target and legibility thresholds, converted to real Siemens panels

- **WCAG 2.2 SC 2.5.8 (AA): 24×24 CSS px**, with the **spacing exception** — undersized targets pass if "a 24
  CSS pixel diameter circle is centered on the bounding box of each, [and] the circles do not intersect another
  target", which converts a size rule into a centre-distance rule. **SC 2.5.5 (AAA): 44×44 CSS px.**
  `[VERIFIED — https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html ,
  https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html]`
- **Apple HIG 44×44 pt; Material/Android 48×48 dp; Google Design for Driving 76×76 dp** — the last being the
  closest consumer analogue to an industrial panel (hostile environment, divided attention).
  `[VERIFIED — https://tetralogical.com/blog/2022/12/20/foundations-target-size/]`
- **Lighthouse `tap-targets` is the best-specified algorithm and the one to copy:** flag iff **both**
  (1) target < 48×48 px **and** (2) ≥25% of the target area within 48 px of its centre overlaps another
  target; "tap targets that are 48 px by 48 px never fail"; 8 px margin as the starting remedy.
  **The conjunction is what keeps precision up.**
  `[VERIFIED — https://developer.chrome.com/docs/lighthouse/seo/tap-targets]`
- ISO 9241-411 figures reported by secondary sources — 9.0 mm square edge / 11.0 mm circular for error rates
  below 4%, 2–3 mm minimum edge spacing (5–8 mm recommended), public-kiosk band 12–15 mm — are
  `[COULD NOT ESTABLISH against the standard; see could-not-establish #24]`.

**Siemens publishes the panel geometry needed to convert mm→px**
`[VERIFIED — https://docs.tia.siemens.cloud/r/unified_comfort_panels_enus_20/technical-information/technical-specifications/mtp700-mtp1000-mtp1200-unified-comfort
and .../mtp1500-mtp1900-mtp2200-unified-comfort]`:

| Panel | Active area (mm) | Resolution | px/mm | 9 mm min target | 3 mm gap |
|---|---|---|---|---|---|
| MTP700 | 152 × 91 | 800 × 480 | 5.27 | **48 px** | 16 px |
| MTP1000 | 217 × 136 | 1280 × 800 | 5.89 | **54 px** | 18 px |
| MTP1200 | 261 × 163 | 1280 × 800 | 4.91 | **45 px** | 15 px |
| MTP1500 | 344 × 193 | 1366 × 768 | 3.97 | **36 px** | 12 px |
| MTP1900 | 409 × 230 | 1920 × 1080 | 4.69 | **43 px** | 15 px |
| MTP2200 | 476 × 268 | 1920 × 1080 | 4.03 | **37 px** | 13 px |

(px/mm and derived pixel values are `[INFERRED]` arithmetic on VERIFIED inputs. This table also closes the
Q4 gap on the three smaller panels.) The 9 mm minimum lands at 48 px on the 7" panel — coincidentally the
Material/Lighthouse figure. **Express every physical threshold in millimetres, store panel px/mm as config,
derive pixels. Never hard-code 48.**

⚠️ **A cross-agent conflict worth recording.** The Q5 research surfaced ISA-101 colour claims ("low-saturation,
predominantly grayscale base… colour reserved exclusively for deviation states", specific hex palettes) from
`hmilibrary.com` and `industrialmonitordirect.com`. The Q4 research established that **`hmilibrary.com`'s
"canonical ISA-101 palette" is verbatim Google Material Design 2014 hex values**. The *doctrine* is
independently supported by PAS and Rockwell (see Q4); **the hex values from those two sites are not, and are
excluded from this report.** This is a live demonstration of the retrieval hazard in §Q4.

### Candidate render-free linter rule set

All from the item tuple list, no rendering:

**Geometry** — G1 viewport protrusion · G2 fully off-screen · G3 zero/degenerate size (**`hmi-compile` accepts
a zero-width screen, so nothing upstream catches this today**) · G4 negative/non-integer geometry ·
G5 interactive-pair overlap (error) · G6 any-pair overlap (warning, needs suppression) · G7 text-bearing item
overlapped by anything (occlusion was 47% of Nighthawk's corpus) · G8 unintended containment · G9 duplicate item.

**Touch** — T1 min target ≥ 9 mm × px/mm · T4 Lighthouse two-condition crowding rule · T5 WCAG 24 px circle
rule · T7 min gap ≥ 3 mm × px/mm · T8 fat-finger-zone collision.

**Text** — X1/X2 cap height ≥ 16′/20′ arc at the stated viewing distance · X3 estimated text overflow (+10%
margin, from TTF advance widths) · X5 placeholder/NULL text · X6 font not Siemens Sans.

**Alignment** — A1 grid conformance · **A2 near-miss alignment (τ ≈ 4 px, cluster ≥ 2) — the highest-value rule
here** · A3 grid-line count as a regression metric · A4/A5/A6 spacing scale, rhythm, size consistency.

**Reading & structure** — R1 reading-order acyclicity · R5 ~70% main-area zone conformance (Siemens Template
Suite) · C5 navigation depth ≤ 4 (ISA-101 hierarchy).

**Project-specific, and the ones no external tool could ever do** — C1/C2 a saturated/alarm colour must bind to
an alarm-class tag (the IEC 60073 / HP-HMI cross-check, possible only because both the colour literal *and* the
tag binding are readable) · **C7 unbound interactive item (no handler, no tag) — highest-value project-specific
rule** · C8 dynamization type/target mismatch (the P7/P8 findings already recorded in `CLAUDE.md`).

---

## What could not be established

Read this section. Several of the entries below would change a design decision if they came out the other way.

**On Custom Web Controls (Q1)**

1. **Whether a CWC instance can be created on a Unified screen through Openness.** No documentation, forum
   answer or open-source example was found either way. Installing the *package* provably needs no Openness
   (file copy + Toolbox refresh), but placing an instance is unknown. **Directly measurable in-house with the
   existing `openness-cli hmi --schema`.** Note the V20 Openness "tasks" chapter documents no screen-item
   creation at all, yet this project has measured it working — so silence there means nothing.
2. **The sandboxing claim.** That a CWC runs in an iframe with Siemens-controlled `sandbox` attributes omitting
   `allow-popups`/`allow-forms` is plausible and consistent with the official restrictions, but every source
   found is a third-party blog, one of them SEO/AI-flavoured. The related *Browser/Web Control* constraints
   (X-Frame-Options, CSP `frame-ancestors`, same-origin policy, `SameSite` cookies, HTTPS-only, no manual
   certificate bypass inside an iframe) come from the same family of sources.
   `[https://www.plc-hmi-scadas.com/en/blog/embebido-web-wincc-unified-iframe-politicas-seguridad/]`
3. **Whether CWCs can be stored as library types.** Reported as impossible by third parties; not found in the
   V20 Information System pages checked ("My controls", restrictions, install/use).
4. **CWC licensing.** No separate licence statement was found anywhere. The conclusion "no extra licence" rests
   entirely on absence of evidence.
5. **The Siemens CWC system manual (SIOS 109794040) and the V20/V30/V40 application example PDFs
   (SIOS 109779176)** could not be read — `support.industry.siemens.com` returns HTTP 403 to automated fetch,
   and the copies that did download are image-heavy PDFs that would not extract. Everything quoted here comes
   from the equivalent TIA Portal Information System HTML pages and the Siemens-org GitHub guide. **The
   manuals may contain restrictions not reflected in the online help.**
6. **Whether a CWC can host an entire screen** (one full-bleed control doing all the UI in HTML) is untested
   and unmentioned in any source. Siemens' only quantitative steer is a warning against 3-D CWCs on Unified
   Comfort Panels; no "maximum controls per screen" figure was found.
7. **`webcc.min.js` versioning** — it is obtained from Siemens examples, not from a package registry, and no
   statement was found about whether a control built against one version runs on a different runtime version.

**On the runtime (Q2)**

8. **No published example of Playwright or Selenium driving WinCC Unified** was found. The conclusion that it
   works is inference from "it is an HTTPS web app with a form login" — sound, but untested.
9. **Whether the panel-side client is specifically Chromium.** Siemens says HTML5/SVG/JavaScript and documents
   a CDP debug port; they do not name the engine. The Unified Comfort Panel running Linux is third-party.
10. **The exact runtime-scripting property surface.** That `ScreenItems` exposes `Name`, `Type`, `Left`, `Top`,
    `Width`, `Height` comes from secondary write-ups, not from the Siemens scripting reference (SIOS 109758536
    was located but not read). Verify before building a runtime introspector on it.
11. **The OpenPipe message schema** and what the ODK actually exposes — existence verified, contents not.

**On prior art (Q3)**

12. **Whether the Siemens Excel Importer/Exporter (SIOS 109792619) round-trips with *fidelity*.** Its section
    structure is now established (command-based export, "export all configured HMI screens", simple
    properties, complex properties/compositions, dynamizations, events, fonts, list of attributes), so the
    *shape* is right. What could not be read is the per-attribute coverage table — whether every attribute
    and every dynamization kind survives a round trip, and what it does with the kinds this project has
    already mapped (Flashing, ResourceList, mapping-table entries, bitmask entries). This is the single most
    important open question in Q3: it decides whether the review artifact is mostly a download or mostly a
    build. Also unknown: supported TIA Portal versions.
13. **TIA Openness Manager's actual fidelity.** The "preserving all properties, text content, and
    dynamizations" claim is vendor marketing on the vendor's own site. Unverified. There is a free tier
    (1 file per operation) that would settle it cheaply.
14. **SiVArc's exact Openness API surface** (class and method names). The Openness system manual is >10 MB and
    could not be fetched; the SiVArc manuals returned 403/ECONNRESET. Existence of a "SiVArc Openness" section
    is established; its contents are not.
15. **SiVArc's Unified device coverage in V20 specifically** — established at vendor-summary level only, not
    quoted from the SiVArc manual.
16. **Whether TIA V21 changes anything for Unified screens in Openness.** SIMATIC SD is verified PLC-only, but
    the V21 Openness "what's new" list could not be extracted.
17. **The method and conclusions of Šverko & Galinac Grbac (Procedia CS 232, 2024)** on automated HMI design —
    ScienceDirect returned 403.
18. **Whether the Siemens Engineering Copilot's HMI output is layout generation or only script/dynamization
    generation.** The wording ("an *initial* visualization", "generating and integrating JavaScript for
    dynamic HMI behavior") suggests the latter, but no demo, documentation or evaluation was found.

**On design rules (Q4)**

19. **The normative text of ANSI/ISA-101.01-2015.** Paywalled. Structure only was verified from ANSI's preview:
    nine clauses; Clause 4 lifecycle; **Clause 5 Human Factors Engineering & Ergonomics** (5.2 User Sensory
    Limits, 5.3 User Cognitive Limits); **Clause 6 Display Styles and Overall HMI Structure** (6.3 Display
    Hierarchy, Figures 3–6 = sample Level 1–4 displays); Clause 7 User Interaction; Clause 8 Performance
    (8.3 HMI Duty Factors); Clause 9 Training; Table 7 "Example numeric decimal formatting", Table 8 "Example
    access and navigation performance". Approved 9 July 2015, ISBN 978-1-941546-46-8, and "Clauses 4–9 present
    mandatory requirements and non-mandatory recommendations **as noted**". So the standard does contain some
    quantitative examples, and 5.2 is where legibility numbers would live.
20. **Any 2024/2025 revision of ISA-101.01.** ISA's own page lists exactly three published documents
    (101.01-2015, TR101.01-2022 HMI Philosophy, TR101.02-2019 Usability and Performance) and nothing under
    revision. `[VERIFIED as absence — https://www.isa.org/standards-and-publications/isa-standards/isa-101-standards]`
21. **Whether ISA-101.01 *mandates* a Style Guide or merely describes one.** The philosophy/style-guide/toolkit
    triad is in clause 4.2, but whether it carries "shall" is behind the paywall. **Do not repeat "ISA-101
    requires a style guide" as verified fact.**
22. **Any published RGB values in ISA-101, ISA-18.2, EEMUA 191 or IEC 62682.** None found, and no evidence any
    exist. Every hex value in this report comes from a *vendor* style guide or a research study. **A claim of
    "ISA-101 colours" would be unsupportable.**
23. **The ASM Consortium "Effective Console Operator HMI Design" (2nd ed.) guideline text.** Book only.
    Metadata verified: 64 guidelines (down from 81), >50% revised, 28 new figures; the 2008 predecessor had
    16 guideline areas, 8 of them on colour.
24. **The "12 mm gloved touch target"** and the "9.0 mm square / 11.0 mm circular for <4% error, 3.0 mm
    spacing" ISO 9241-411 figures — widely repeated, no primary source found, NN/g does not mention gloves.
25. **Siemens' own viewing-distance → character-height guidance.** Both Siemens and Rockwell state the
    dependency and give no numbers. No Siemens panel document with a mm-or-px legibility table was found.
26. **The Siemens HMI style guide SIOS 81318674** (403) and the **HMI Design Masterclass** (the URL cited in
    the Template Suite manual does not resolve to any masterclass content). Both are worth a manual retrieval.
27. **WinCC Unified predefined style names and the property surface a style controls.** The V20 "Custom
    styles" page rendered only navigation, and "Predefined styles" 404s at the guessed slug. **This matters:
    if styles carry the palette, a generator should emit a style rather than per-item colours.**
28. **Active display areas for MTP700/MTP1000/MTP1200**, so px↔mm conversion for the smaller panels is
    uncomputed.
29. **Any peer-reviewed evaluation of AI/LLM-generated industrial HMI against HP-HMI or ISA-101 criteria.**
    None found. Vendor claims exist; generic LLM risk literature exists; **a critique of AI-generated HMI
    against these criteria appears not to have been published. This project is ahead of the literature.**

**On tooling (Q5)**

30. **Whether the Unified runtime DOM carries stable, engineering-meaningful element identity** (the item name
    as an `id` or `data-*` attribute). **The single most important open question in Q5** — the CDP-geometry
    oracle and the relation-graph diff both hinge on it. Answerable in one session by attaching DevTools to a
    running simulation.
31. **Whether `UI.ActiveScreen.Items` enumerates screen items at runtime.** Siemens documents `Screen.FindItem()`
    as the supported way to obtain a handle, which hints enumeration may not be. Sourced only from a
    low-quality aggregator; the Siemens scripting Tips & Tricks PDF (SIOS 109758536) returned 403.
32. **Whether Unified Comfort Panels render through the same HTML5 stack on-device.** Verified for Unified
    **PC** Runtime only. If panels are the target, the browser route may not apply.
33. **How much the Unified runtime varies across Chrome versions.** Siemens warns that it does; the magnitude
    is unknown and is a direct threat to any pixel baseline.
34. **ReDeCheck's published false-positive rate** — the best empirical estimate of how noisy a geometry-only
    linter is. Behind a paywall (HTTP 402). **Worth chasing before committing to rule thresholds.**
35. **Whether `GridStackEngine` runs headlessly without the DOM**, and whether its collision/compaction are
    deterministic. Neither documented.
36. **Whether `stretchable`'s `get_box()` coordinates are absolute-to-root or parent-relative.**
37. **Which pixelmatch version Playwright bundles** — the docs say YIQ, upstream is OKLab. Matters for any
    tuned threshold.
38. **Exact metric formulas for Web2Code, WebCode2M, Interaction2Code and DesignBench.** Only Design2Code's
    were read at source level; those are the only ones vouched for line by line.
39. **Licences not confirmed:** `redecheck`, `viser`, `owleyes`, `Nighthawk`, `design-lint`, `CassowaryNET`,
    `jwosty/Yoga.NET`, `stitch-sdk` output terms, `x-img-diff-js`. **Confirmed:** MIT — Yoga.Net, Taffy,
    stretchable, fontTools, gridstack, Mitosis, Z3, PuppeteerSharp, odiff, honeydiff, blazediff, reg-suit,
    BackstopJS; Apache-2.0 — OR-Tools, Magick.NET, Playwright, Stitch SDK; ISC — pixelmatch, PixelMatch.net;
    BSD-3 — Shapely; EPL-1.0/EDL-1.0 — NetTopologySuite. 🔴 **AGPLv3 — `const_layout`**; 🔴 **LGPL —
    Cassowary.net**; 🔴 **AGPL — OmniParser `icon_detect`**; 🔴 **non-commercial — Ferret-UI**; Design2Code is
    MIT code with research-use-only data/checkpoints.
40. **A general-purpose "list of rects → layout problems" library — none exists.** That part gets built.

**Sources deliberately not trusted.** `hmilibrary.com/standards/isa-101` serves a fabricated "canonical
ISA-101 palette" that is verbatim Google Material Design 2014 hex values (#D32F2F, #F57C00, #FBC02D, #1976D2…)
plus invented figures ("14 px minimum readout", "256×128 PNG symbol export") and "Inter / JetBrains Mono" as
recommended fonts. `industrialmonitordirect.com`, `plcprogramming.io` and `processcontrolguide.com` are the
same genre and appeared repeatedly in search results throughout this research — several claims in this report
were *rejected* because that was their only source. **If any generator here does retrieval over the open web
for HMI design rules, this is what it will find.**

---

## Build / buy / reject

Ranked by value to *this* project, not by general merit. "Build" means small and in-house; "adopt" means take
a permissive dependency; "buy" means money; "reject" means do not spend time on it.

### The two conclusions that reframe the problem

**1. The "no export" wall does not block review — it blocks *convenience*.** There is no export *function*,
but a **serializer** built on the property walk `openness-cli hmi` already performs recovers diff, content
hash, golden round-trip and version control. Siemens themselves ship one (Excel, SIOS 109792619, with a
command-based "export all configured HMI screens"), and a commercial vendor sells a JSON one (V15–V21). Two
independent existence proofs. **This is the single highest-value thing to build, and everything else in the
review chain depends on it.**

**2. Custom Web Controls are where the whole web toolchain actually lands.** A CWC is a plain source tree,
installed by file copy, that Siemens explicitly says "can be displayed as an independent Web page in any
browser". Inside its rectangle there is a real layout engine and no absolute-positioning constraint. **The
entire generate → render → screenshot → diff → fix loop runs there, offline, in milliseconds, with no TIA
Portal in the loop at all** — which is the only way any iterative approach survives a multi-minute write cycle.
The Unified screen then becomes a thin placement surface for a small number of CWC rectangles plus native
controls, rather than a canvas of 200 hand-placed primitives.

### Ranked

| # | Item | Verdict | Why |
|---|---|---|---|
| 1 | **JSON screen serializer over the Openness read-back** (stable ordering, canonical form, content hash) | **BUILD FIRST** | Recovers diff/hash/round-trip/version-control. Siemens (109792619) and TIA Openness Manager both prove it works. Everything downstream needs it. |
| 2 | **Spec-vs-readback comparator** — intended tuples vs actual tuples | **BUILD** | GVT's oracle made exact and symbolic. No computer vision, no threshold, no baseline approval. This is `converter diff` for screens. |
| 3 | **Render-free geometric linter** (the G/T/X/A/R/C rule set above) | **BUILD** | Arithmetic on ~200 items: offline, deterministic, explainable, no knobs. Design in per-item/per-pair suppression from v1 — ReDeCheck's authors publish the false-positive limitation. |
| 4 | **Relation-graph regression check** (X-PERT RAG + Mystique's vocabulary) | **BUILD** | Turns "did my write break the layout?" into a set difference over `left-of`/`above`/`x-aligned`/`contains`/`overlaps`. |
| 5 | **Physical-units threshold table** (mm-based rules + per-panel px/mm config) | **BUILD — cheap, high leverage** | The px numbers in every vendor style guide are wrong by 2–4× on a high-ppi panel at 2 m. Store mm, derive px. Table already computed above for all six MTP panels. |
| 6 | **CWC-first screen architecture + a ~20-line CWC packaging script** | **BUILD** | Siemens' own reference CWC has *no build script*; packaging is manual. The offline web loop is the prize; the packager is the entry fee. |
| 7 | **Coarse-grid integer coordinate IR** for the LLM to emit | **BUILD** | Published layout models all quantize; raw float emission is the documented weak point of LLM spatial reasoning. |
| 8 | **Solver repair pass** — grid snap + overlap/containment fix | **ADOPT (Yoga.Net, MIT)** | LayoutRectifier proves model-agnostic, training-free post-processing fixes exactly LLM layout's three failure modes. Yoga.Net is pure C#, deterministic, pixel-grid rounding, 833 conformance tests. Runs net8+, so a separate tool from the net48 Openness binary — the correct boundary anyway. |
| 9 | **Divide → per-block generate → absolute-position assemble** (LaTCoder APS) | **ADOPT THE PATTERN** | Measured +60% TreeBLEU / −43% MAE over direct prompting, and it is the natural shape for a flat screen. |
| 10 | **Design2Code's position and colour metrics** (Chebyshev centre distance; `1 − ΔE2000/100`) | **REIMPLEMENT** | Four lines each, published, near-perfect fit. Skip the Hungarian matching — we have item *names*. |
| 11 | **Layout metrics: Alignment / Overlap / Max IoU** (Li et al. 2020 formulas) | **REIMPLEMENT** | Computable from boxes alone. 🔴 Do **not** vendor `const_layout` — AGPLv3. |
| 12 | **Breuel partial order + topological sort** for reading order | **BUILD** | Free defect finding: a cycle = geometrically ambiguous reading order on an operator display. No published tool reports this. |
| 13 | **CDP geometry read of the live runtime** (PuppeteerSharp/Playwright + `DOMSnapshot.captureSnapshot`) | **BUILD — but verify the premise first** | A *second, independent* geometric oracle alongside the Openness walk. Gated on could-not-establish #30 (does the DOM carry item identity?). One session with DevTools settles it. |
| 14 | **Headless-Chrome flattener** (HTML/CSS → integer rects) | **BUILD if HTML is chosen as the IR** | ~50 lines of CDP; no product exists. Font risk is largely neutralised because the target *is* Chrome — install Siemens Sans and the text engine matches. |
| 15 | **Magick.NET** | **ADOPT — Apache-2.0** | SSIM/DSSIM/PHASH/RMSE in-process on net48, no Node. ⚠️ validate metric direction against a known pair (open inverted-value bugs). |
| 16 | **odiff or honeydiff** | **ADOPT — MIT** | Standalone Windows binaries, browser-free. honeydiff's CIEDE2000 ΔE 2.0 default is the best-motivated threshold found for UI screenshots. |
| 17 | **Playwright `ToMatchAriaSnapshotAsync`** | **INVESTIGATE** | Exists in .NET (unlike `toHaveScreenshot`) and gives a structured, diffable YAML snapshot instead of pixels. Unknown whether Unified emits useful ARIA roles. |
| 18 | **stretchable / Taffy** | **ADOPT if the flattener lives in Python — MIT** | Grid + flexbox, Windows wheels, ~900 tests validated against real Chrome. |
| 19 | **OR-Tools CP-SAT / Z3** | **ADOPT for constrained placement — Apache-2.0 / MIT** | ⚠️ pin the version, `num_workers=1`, fixed seed, and regression-test determinism anyway — non-determinism bugs are filed against single-worker mode. |
| 20 | **Dynamic SVG as a second file-based extension point** | **INVESTIGATE — likely worth it** | Plain SVG + an `hmi:paramDef` interface; a real **library type** (unlike CWCs); diffable, version-controllable, marketplace-proven. Cheaper than a CWC for symbol-level work, and it *can* live in a library. |
| 21 | **TIA Openness Manager (CHF 0 / 110 / 330 per year)** | **BUY THE FREE TIER TO EVALUATE** | Free tier is 1 file per operation — enough to settle whether its JSON round-trip really preserves all dynamizations. If it does, item 1 becomes an integration rather than a build. Ships an MCP server. ⚠️ Fidelity claim is vendor marketing, unverified. |
| 22 | **Siemens Excel Importer/Exporter (SIOS 109792619)** | **DOWNLOAD AND TEST** | Free, Siemens-authored, Openness-based, covers simple + complex properties, dynamizations, events and fonts, with a command-based exporter. Test its round-trip fidelity before writing item 1 from scratch. |
| 23 | **Siemens docs behind the 403 wall** — HMI Style Guide (81318674), HMI Template Suite (91174767), Engineering Guideline (109827603), CWC system manual (109794040), scripting Tips & Tricks (109758536) | **GET MANUALLY** | All free, all blocked to automated fetch (anti-bot, not paywall). Vendor-authoritative layout zones, palettes, naming and CWC restrictions. Highest-yield hour of manual work available. |
| 24 | **VLM-as-judge with a rubric** | **ADVISORY ONLY — NEVER A GATE** | 66.06% expert agreement vs an 84.82% human baseline. Feed it the **item list *and* the screenshot** — WebDevJudge found removing the code hurt more than removing the screenshot. Score computed deterministically outside the model. |
| 25 | **Figma REST API as a design-time IR** | **CONDITIONAL BUY** | `absoluteBoundingBox` on every layout node is exactly the right shape — but cloud-only, paid seat, 10–20 req/min, and it presupposes humans designing HMI in Figma. Copy the *node schema* regardless; it costs nothing. |
| 26 | **FUXA (MIT) as a preview/review renderer** | **INVESTIGATE** | SVG-based, absolutely positioned, tag-bound, headless binaries. The closest off-the-shelf way to show a human an AI-designed screen before it reaches a panel. |
| 27 | **reg-suit** | **OPTIONAL — MIT** | Captures nothing; consumes an image dir; reports *moved regions* rather than pixel counts. |
| 28 | **SiVArc (~£4,391, floating)** | **REJECT for design; RECONSIDER for scale** | A rules engine, not a designer — excellent at "the 200th valve", useless at "design a good overview". It is Openness-drivable, so if per-instance scale ever matters, the AI should emit *SiVArc rules* rather than screens. |
| 29 | **Siemens Engineering Copilot** | **WATCH, DO NOT COMPETE HEAD-ON** | Already generates "an initial visualization" and HMI JavaScript. It does not claim reviewed, standards-compliant layout — which is exactly the gap this project fills. |
| 30 | **Playwright `toHaveScreenshot`** | 🔴 **REJECT for .NET/Python** | Does not exist outside the Node test runner. `ScreenshotAsync()` does; the assertion does not. |
| 31 | **Builder.io Visual Copilot / Locofy / v0 / Figma Make / Framer / Stitch / Uizard / TeleportHQ** | 🔴 **REJECT** | All exist to convert flat-absolute *into* nested-responsive — the exact inverse of the need. TeleportHQ's UIDL has no coordinate concept at all. |
| 32 | **Anima** | 🔴 **REJECT (but note the irony)** | It *does* emit absolute coordinates — the industry treats that as its defect. SaaS, Figma-coupled, and Figma REST gives the same data free and structured. |
| 33 | **Percy / Chromatic / Applitools** | 🔴 **REJECT** | Cloud SaaS requiring upload of deployed plant screens — a direct `docs/13-data-boundary.md` retention collision, before any technical argument. |
| 34 | **LPIPS** | 🔴 **REJECT** | Designed to be *insensitive* to small shifts — the opposite of the question being asked — and drags PyTorch onto the engineering PC. |
| 35 | **OmniParser / Ferret-UI / UIED / ScreenAI / any OCR route** | 🔴 **REJECT** | AGPL, or non-commercial, or cloud-OCR-by-default; and 39.6–71% accuracy re-deriving data already held exactly. |
| 36 | **`Windows.Media.Ocr`** | 🔴 **REJECT — project-specific hazard** | Requires MSIX package identity, which changes `openness-cli`'s `(Path, FileHash)` and collides head-on with FI-61's Openness whitelist. |
| 37 | **ssim.js / Resemble.js / Puppeteer / BackstopJS / Cassowary .NET ports / Kiwi** | 🔴 **REJECT** | Archived, low-maintenance, dominated, browser-coupled, or LGPL/licence-unknown. Copy BackstopJS's *architecture* (baselines committed to the repo), not the tool. |
| 38 | **Siemens iX design system** | 🔴 **REJECT as authority — actively risky** | MIT and genuinely Siemens, but it is a design system for industrial *web apps*: accent colours, gradients, dark mode, no ISA-101 claim, no mention of panels or control rooms. A Siemens badge on exactly the vocabulary an LLM must be steered away from. |
| 39 | **Screenshot diffing as the *primary* gate** | 🔴 **REJECT — the deepest one** | With no export, an approved baseline is a self-generated ground truth from the same source as the thing it checks. That is the **correlated check** `docs/evidence/PlantAutoControl-bench-autopsy.md` exists to prevent. Pixels are a tripwire; geometry is the gate. |

### Two sequencing notes

- **The cheapest next actions are not builds.** Retrieve the five 403'd Siemens PDFs in a browser (item 23);
  download and test the free Siemens Excel Importer/Exporter (item 22); run one `openness-cli hmi --schema`
  probe for a custom-control creatable type (Q1's open question); and attach DevTools once to a running
  simulation to answer whether the runtime DOM carries item identity (could-not-establish #30). Four short
  tasks, each of which removes a "could not establish" that currently constrains the design.
- **The design-rules layer is not optional and is not emergent.** Siemens' own training material teaches
  animated flames, contents-coloured pipes and rotating fans; the open web's top ISA-101 result is a
  fabricated Material Design palette; and no LLM will arrive at "green for running is an improper use of
  colour" on its own. The rules must be written down, cited, and mechanically checked — which is the same
  conclusion, and the same architecture, that `docs/06-lad-conventions.md` plus `converter review` already
  represent on the LAD side.
