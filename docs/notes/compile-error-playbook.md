# Compile-error playbook (FI-14)

Known errors from the export → IR → import → compile → re-export cycle (Openness connect included),
each with its confirmed cause and proven fix. Lookup key is the verbatim message fragment (or the
symptom, where there is no message). **Every entry is a hypothesis to verify in context, not an
answer to trust blindly** — a new occurrence can have a different cause; the entry tells you what
to check first, and its source reference holds the full story. Add entries as encountered: message,
context, confirmed cause, proven fix, dated source. Never add an entry whose fix wasn't actually
proven.

Sources: `docs/notes/openness-quirks.md` (quirks), `docs/notes/stage-gates.md` (gates),
`CHANGELOG.md` (dated), `CLAUDE.md` environment notes.

## Compile stage

### "Inconsistent blocks and PLC data types (UDT) cannot be exported"
- **Where:** `export` after any `import` — the imported block *and its callers* get `IsConsistent = false`.
- **Cause:** `Import()` flags the block and callers inconsistent regardless of content; device-level `Compile()` reports Success but never clears the flag. Not a content defect.
- **Fix:** `openness-cli compile --block <name>` (block-level compile clears it in one call). Compile callees before callers.
- **Source:** quirks "Inconsistent blocks…" (2026-07-10, reconfirmed 2026-07-13).

### "Block 'X' that is accessed has not been compiled"
- **Where:** block-level compile of a caller.
- **Cause:** a callee (or referenced DB) was re-imported and not yet block-level compiled — compile order matters.
- **Fix:** compile the callee/DB first, then retry the caller. If diagnostics show only an error *count* with empty descriptions, the real text is in the nested `Messages` tree (fixed in `RunCompile`/`CollectMessages`).
- **Source:** quirks "…diagnostic messages were silently incomplete" (2026-07-10); caveat under the IsConsistent entry.

### "Missing instance DB"
- **Where:** compile of a block calling an FB (or using a timer) without its instance DB in the target project.
- **Cause:** the instance DB was never created/imported in the scratch project.
- **Fix:** `openness-cli create-instance-db` for instance DBs of natively-authored/imported FBs. For `--synthesize`-produced FBs, hand-author the instance DB's `.ir` instead (see next entry).
- **Source:** CHANGELOG 2026-07-14 (`create-instance-db` entry); gates "PlantAutoControl plan Tier 1".

### "The block <name> DB has an invalid number 0" (block-level compile; device compile clean)
- **Where:** block-level compile of an instance DB created via `create-instance-db` for a `--synthesize`-produced FB. Reproduced 4×.
- **Cause:** not root-caused; the synthesize-vs-native correlation is a lead, not proof. Distinct case: standalone system-FB instances (TON, Modbus_*) also get `DB0`, and those are invisible to `SW.Blocks` entirely — no Openness fix exists.
- **Fix:** hand-author the instance DB's own `.ir` (`DB <name> / NUMBER <n> / INSTANCEOF <FB> / MEMBERS` mirroring the FB's STATIC section, explicit non-colliding number) and import it like any other DB — live-verified clean. For standalone system-FB instances: convert to a multi-instance `Static` member at the source project (the only path that ever worked).
- **Source:** quirks "OPEN, 2026-07-15… RESOLVED same day"; quirks "Known constraints" (standalone system-FB instances).

### "Tag <name> not defined"
- **Where:** compile of an imported block.
- **Cause:** a referenced global root (DB, tag-table entry) isn't present in the target project — or, in verification fixtures, a deliberately verification-only tag is missing its scope declaration.
- **Fix:** find the root in `ir/<project>/`; import the missing DB / tag-table entries first. The minimal-tag-table path (filter a real export's IR down to the needed entries) is proven.
- **Source:** CHANGELOG 2026-07-14 (PlantAutoControl Phase 2; tag table support); gates.

### Type mismatch on a system-datatype port (e.g. `Modbus_Comm_Load.PORT`)
- **Where:** compile after importing a block calling a system FB.
- **Cause:** the port needs the actual Siemens system datatype (`PORT`), not a generic integer.
- **Fix:** declare the feeding tag with the system datatype named by the instruction's spec.
- **Source:** CHANGELOG 2026-07-14 (Phase 2 Tier 4).

### Bare time literal rejected on a `TONR`/`TOF` `PT` port
- **Where:** import/compile of a block feeding `PT` with a `T#…` literal on those instruction kinds.
- **Cause:** TIA rejects the bare literal shape there (grounded live; exact TIA rule unestablished).
- **Fix:** feed `PT` from a `Time` tag (the already-grounded shape).
- **Source:** CHANGELOG 2026-07-14 (FBTimers entry).

### "The entered address is not within the valid address range"
- **Where:** compile of a `DB0`-numbered block.
- **Cause:** `DB0` is itself an invalid number (see the invalid-number-0 entries above for how it arises).
- **Fix:** as per those entries — hand-authored explicit number, or source-side multi-instance conversion.
- **Source:** quirks "Known constraints".

## Import stage

### "Element '<member>' cannot be found. Please check the consistency of the type used."
- **Where:** import of an instance DB (or any block referencing a UDT) right after importing a new
  version of that UDT that added/renamed members — the iDB names members TIA's type system doesn't
  know yet.
- **Cause:** `Import()` of a PLC data type does not update dependents' view of the type; until the
  UDT is type-compiled, TIA resolves the OLD type version, so new members "cannot be found."
  Import order compounds it: an iDB imported before its (interface-changed) FB hits the same class.
- **Fix:** on interface-changing waves, the proven order is UDTs (`--type`) → `compile --type` each
  → FBs → iDBs → callers; then block compiles callees→callers. Live-verified 2026-07-17 (fix wave
  1: first attempt failed exactly here; corrected order ran 9 imports to a 0/0 device compile).
- **Source:** stage-gates "fix wave" entry (2026-07-17); gen/GenProject1/fix-wave-1.md §7.
- **Where:** import of converter-generated XML.
- **Cause:** Part/wire order in the FlgNet doesn't match true document order — a converter bug class (two instances found and fixed generally by UId sort, 2026-07-14).
- **Fix:** this class is fixed; a recurrence is a new converter bug — report it, never hand-patch the XML (hard rule 7).
- **Source:** CHANGELOG 2026-07-13/14 (FlgNetBuilder fixes; full-cycle verification pass).

### "Interface: A structure without components is not allowed"
- **Where:** compile after importing a block/DB whose anonymous `Struct` members arrived empty.
- **Cause:** was a real converter parser gap (nested `<Member>` without `<Sections>` wrapper silently dropped) — fixed recursively for arbitrary depth.
- **Fix:** fixed class; a recurrence is a new converter bug — report it.
- **Source:** CHANGELOG 2026-07-14 (EquipmentControlSystem; ShredderControlSystem).

### "No device item found under '<path>'"
- **Where:** `import` / `create-instance-db` with `--group <device>/<path>`.
- **Cause:** the group path was guessed/shortened — a device item's real name can contain spaces and an embedded article number as one literal string.
- **Fix:** copy `list`'s own `Path` column verbatim.
- **Source:** CLAUDE.md environment notes.

### "An instruction with the name 'WAIT' cannot be found"
- **Where:** import into `SampleProject` of a block using `WAIT` (shape faithful to the real JOB9002 export).
- **Cause:** likely a missing library/technology-object dependency in the target project — never identified; confirmed not a converter bug.
- **Fix:** none proven. Treat as a target-project dependency gap; `WAIT` is closed as not-needed (FI-04, `16-future-ideas.md`).
- **Source:** CHANGELOG 2026-07-14 (tiers 5/6); AITODO.

### Re-import silently creates a duplicate block (no error)
- **Where:** `import` after the engineer renamed the block directly in TIA Portal.
- **Cause:** Openness import matches by *name* — importing under the old name creates a new block instead of updating the renamed one; undetectable from the tooling side.
- **Fix:** prevention only: renames/deletes done by hand in TIA must be communicated before the next import.
- **Source:** CLAUDE.md environment notes.

### Converter rejects embedded `\n`/`\r` in Title/Comment text
- **Where:** `to-xml`/`to-ir` (a deliberate converter guard, clear error message).
- **Cause:** multi-line text would corrupt the line-oriented `.ir` format.
- **Fix:** keep Title/Comment single-line in IR (write longer prose as multiple networks' comments or restructure).
- **Source:** CHANGELOG 2026-07-14 (S3 first proof).

## Connect / open / export stage

### Connect hangs or `ConnectTimeoutException` (no new Portal process appears)
- **Where:** any `openness-cli` command's connect stage.
- **Cause (check in order):** (1) first-connect approval dialog waiting inside TIA Portal — one per Portal binary; (2) stale Portal-process pileup — the confirmed correlate of "second instance won't connect" (2026-07-14 audit); (3) transient slow launch — one occurrence simply outlasted a 9-minute budget, then never reproduced.
- **Fix:** (1) have the engineer check Portal for the dialog; (2) `tasklist` for `Siemens.Automation.Portal.exe`, close idle instances (orphans self-heal via `LaunchedInstanceRegistry`, strays don't); (3) retry once with patience. Keep `--timeout-connect` + `--timeout-open` *summed* under any outer timeout, or the outer kill fires first and can leave Portal stuck.
- **Source:** quirks (first-connect; "sometimes won't connect", CLOSED audit; practical lesson 2026-07-14).

### "It has already been opened by user … can only be opened again after a 2 minute delay"
- **Where:** `Projects.Open()` on a project whose file lock another process holds (or after an unclean close).
- **Cause:** exclusive `.apXX` file lock; historically also a tooling bug (empty process chosen while a sibling held the lock — fixed with the two-pass search).
- **Fix:** the two-pass search now finds the lock-holder and reuses it. If hit anyway: identify the process holding the lock; after an unclean close, wait the stated ~2 minutes.
- **Source:** quirks "Follow-up, 2026-07-14".

### "Another project is already open"
- **Where:** `Projects.Open()` within one Openness session.
- **Cause:** one-project-per-session constraint; two Openness sessions on the *same* project is a genuine TIA single-writer constraint, unsupported.
- **Fix:** handled automatically for *different* projects (dedicated instance launch); for the same project, don't — sequence the work.
- **Source:** quirks (auto-switch history; concurrent sessions).

### "The argument 'path' cannot be a relative path."
- **Where:** cold-open (`<project>` given as a `.apNN` file).
- **Fix:** absolute path.
- **Source:** quirks "Project paths".

### `Export()` returns success but no file exists
- **Where:** `PlcBlock.Export()` — observed once, cause unconfirmed (Portal-side timing?).
- **Fix:** verify the output file exists after `Export()` returns; retry once before failing (built into `export`).
- **Source:** quirks "Known constraints".

### `FileLoadException: Siemens.Engineering.Contract` / `MissingMethodException: Assembly.Load(…)`
- **Where:** first API call from any PC-side tool referencing `Siemens.Engineering.dll`.
- **Cause:** modern .NET target — the DLL internally uses a .NET-Framework-only `Assembly.Load` overload.
- **Fix:** target `net48` (builds fine on net8.0-windows; fails only at runtime).
- **Source:** quirks ".NET target framework".
