# CLAUDE.md Commands-block migration inventory

Generated for the context-cut pass. Working file - delete when the cut is done.

`CLAUDE-ONLY` markers appear in CLAUDE.md's Commands block and in NEITHER
`src/converter/README.md` NOR `src/openness-cli/README.md`. Each must be written
into the owning README **before** the CLAUDE.md text is deleted.

| command | bytes | markers | CLAUDE-only |
|---|---|---|---|
| `converter to-ir` | 4941 | 60 | **22** |
| `converter diff` | 3650 | 47 | **22** |
| `converter drift-check` | 4085 | 51 | **17** |
| `converter compare` | 3886 | 41 | **10** |
| `openness-cli hmi-edit-screen` | 1947 | 34 | **8** |
| `converter reachable-state` | 3065 | 39 | **6** |
| `openness-cli download-plan` | 2508 | 35 | **5** |
| `openness-cli library` | 703 | 7 | **5** |
| `converter reuse-scan` | 1521 | 21 | **5** |
| `converter cross-check` | 1506 | 13 | **4** |
| `openness-cli import-all` | 1568 | 14 | **3** |
| `openness-cli block-layout` | 2026 | 25 | **3** |
| `openness-cli export-all` | 649 | 9 | **2** |
| `openness-cli library` | 418 | 8 | **2** |
| `converter to-ir` | 715 | 6 | **2** |
| `converter to-ir` | 794 | 7 | **2** |
| `converter interface-check` | 1743 | 30 | **2** |
| `converter candidate-scan` | 1044 | 13 | **2** |
| `openness-cli compile-all` | 1781 | 12 | **1** |
| `openness-cli hmi` | 924 | 3 | **1** |
| `openness-cli library` | 214 | 2 | **1** |
| `converter undriven-scan` | 1100 | 11 | **1** |
| `dotnet build` | 592 | 1 | **1** |
| `openness-cli list` | 185 | 2 | **0** |
| `openness-cli export` | 120 | 4 | **0** |
| `openness-cli import` | 93 | 3 | **0** |
| `openness-cli compile` | 1558 | 14 | **0** |
| `openness-cli delete` | 140 | 2 | **0** |
| `openness-cli block-layout` | 1119 | 16 | **0** |
| `openness-cli create-instance-db` | 158 | 3 | **0** |
| `openness-cli sanity-check` | 288 | 7 | **0** |
| `openness-cli portal-status` | 269 | 0 | **0** |
| `openness-cli hmi` | 1342 | 13 | **0** |
| `openness-cli hmi-create-screen` | 680 | 9 | **0** |
| `openness-cli hmi-compile` | 556 | 6 | **0** |
| `converter to-ir` | 183 | 2 | **0** |
| `converter sanitize` | 160 | 0 | **0** |
| `converter review` | 559 | 3 | **0** |
| `converter digest` | 357 | 3 | **0** |
| `converter preflight` | 202 | 3 | **0** |
| `converter tagstatus` | 789 | 13 | **0** |
| `converter target-scan` | 225 | 3 | **0** |
| `converter trace` | 240 | 3 | **0** |
| `converter ir-hash` | 296 | 2 | **0** |
| `converter relation-reconcile` | 950 | 13 | **0** |
| `converter signal-sweep` | 940 | 9 | **0** |
| `converter claim` | 849 | 14 | **0** |
| `converter claims` | 971 | 15 | **0** |
| `dotnet test` | 132 | 0 | **0** |

## CLAUDE-only markers, by command

### `converter candidate-scan`

- `--scope UnitA`
- `DQ3_UnitA_...`

### `converter compare`

- `-IsScratchProject`
- `ALLOWLIST NOT DENYLIST BECAUSE`
- `ARMED AND FENCED`
- `ASCII`
- `DELIMITER`
- `LONGER WORKS WITH`
- `PROJECT NAME`
- `PROJECTS BESIDE THE SCRATCH`
- `SILENT SIDE`
- `tools/confirm-roundtrip.allowlist`

### `converter cross-check`

- `BLOCKS THAT MAY NEVER`
- `NEVER SUBTRACTED`
- `WRITER LINE COUNTS WRITES`
- `unreachableKnown: false`

### `converter diff`

- `--allow-header`
- `.** Where every network is inside the`
- `ALREADY FALSE`
- `BLOCK COMMENT CANNOT`
- `COMPARED: <n>`
- `FIRST CONTACT WITH REAL`
- `HEADER CHANGE COUNTS`
- `HEADER CHANGE DOES NOT`
- `HEADER COMMENT CHANGED`
- `HEADER COMMENT CHANGED (does not gate)`
- `HEADER changed: interface`
- `NARROWED THE SAME DAY`
- `NOTHING WAS PROVEN`
- `OUTSIDE THE SET`
- `TAXONOMY`
- `UNCHANGED REMAINDER`
- `VERDICT STATES ITS OWN`
- `commentChanged`
- `gen-block-modify-fix`
- `gen-block-modify-purpose`
- `interfaceChanged`
- `…, header unchanged`

### `converter drift-check`

- `0 drifted, 0 match`
- `COMPARED: <n> object(s) put through the Normalizer`
- `DriftStatus.Error`
- `EXPORT-ONLY: Default tag table`
- `GREEN OVER ZERO COMPARISONS`
- `LEVEL ABOVE`
- `NEVER GATED`
- `NOTHING COMPARED`
- `NOTHING COMPARED - this is not a pass`
- `PAIRING-FAILURE`
- `SILENT ABOUT`
- `SKIPPED: DefaultTagTable`
- `THREE MEASURED ROUTES`
- `TopDirectoryOnly`
- `and the outermost`
- `filename does not (`
- `unparseable — a zero-byte file, a truncated write — →`

### `converter interface-check`

- `--enumeration`
- `INPUT 0, OUTPUT 0, STATIC 25`

### `converter reachable-state`

- `--reachable-block`
- `--reachable-state`
- `--reaches-from`
- `<FB>|<suffix>`
- `CANONICALISED ONTO`
- `POOLS WHERE`

### `converter reuse-scan`

- `--tag DB_X.Member`
- `CORPUS PRODUCED`
- `LICENSES`
- `SUMMARY: 0 block(s) matched`
- `SUMMARY: … of <n> file(s) scanned`

### `converter to-ir`

- `--allow-blind-types`
- `BESIDE THE INPUT`

### `converter undriven-scan`

- `FB_X/Member`

### `dotnet build`

- `AFTER ANY CONVERTER CHANGE`

### `openness-cli block-layout`

- `DESTROYS ITS RETAINED DATA`
- `DURABLE`
- `READS THE LAYOUT BACK`

### `openness-cli compile-all`

- `Block "X" that is accessed has not been compiled`

### `openness-cli download-plan`

- `ApplyConfiguration()`
- `Download()`
- `IEngineeringServiceProvider.GetService<DownloadProvider>()`
- `LEVEL AND THAT`
- `PROVIDER ACQUISITION PATH`

### `openness-cli export-all`

- `REFUSED AND NAMED`
- `converter drift-check --exports <dir> --complete`

### `openness-cli hmi`

- `--scripts`

### `openness-cli hmi-edit-screen`

- `CRASHES TIA PORTAL`
- `Item.Prop.ValueConverter.MappingTable.Entries[0].Flashing`
- `MappingTableEntryRange`
- `MappingTableEntrySimple`
- `ResourceList`
- `SECOND WRITING HMI COMMAND`
- `TagParameter`
- `on buttons), screens use`

### `openness-cli import-all`

- `DEPENDENCY ORDER NOT DERIVABLE`
- `Data type "X" is unknown`
- `REJECTIONS WITH REASONS`

### `openness-cli library`

- `--probe-documents`
- `ADVERTISEMENT`

