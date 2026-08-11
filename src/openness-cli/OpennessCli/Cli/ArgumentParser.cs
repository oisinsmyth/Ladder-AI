using System;
using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Cli;

// TagTables switches list's own output from blocks to PLC tag tables — a distinct object type
// (confirmed real 2026-07-14, grounding PlantAutoControl's own dependency closure: some referenced
// tags are bare, single-component Access references that never show up in the ordinary block
// enumeration at all). A separate view, not merged into the same listing, since a tag table has
// none of BlockInfo's own fields (Number/Language/Safety/Consistent).
public sealed record ListOptions(
    string ProjectIdentifier,
    bool Json,
    bool TagTables,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// Exactly one of BlockName/TypeName/TagTableName is set — --block/--type/--tagtable are mutually
// exclusive alternatives (a PLC data type/UDT and a PLC tag table, both confirmed real 2026-07-14,
// have no Number/ProgrammingLanguage the way a block does, so each needs its own distinct
// resolution path, not a shared "name" field).
public sealed record ExportCommandOptions(
    string ProjectIdentifier,
    string? BlockName,
    string? TypeName,
    string? TagTableName,
    string? Device,
    string OutPath,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// FI-70. Bulk export of every block and PLC data type to one directory, so there is something to
// compare the on-disk IR AGAINST — `converter drift-check --complete` is the other half of the
// recipe, and it lives in the converter because the converter never touches the environment (FI-24).
// TagTables is opt-in: `drift-check` pairs by basename against the `.ir` corpus, and a tag table has
// no `.ir` counterpart in the shape the corpus uses, so including it by default would manufacture
// EXPORT-ONLY findings that mean nothing.
public sealed record ExportAllCommandOptions(
    string ProjectIdentifier,
    string OutDir,
    string? Device,
    bool IncludeTagTables,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// AsType/AsTagTable select which composition Import() targets (PlcTypeGroup.Types /
// PlcTagTableGroup.TagTables vs. PlcBlockGroup.Blocks) — both default to false (blocks), today's
// existing behavior, unchanged. Mutually exclusive, same as --block/--type/--tagtable on export.
public sealed record ImportCommandOptions(
    string ProjectIdentifier,
    string GroupPath,
    IReadOnlyList<string> Files,
    bool AsType,
    bool AsTagTable,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// The bulk half of import. Takes directories as well as files, works out what each file IS rather
// than being told (--type/--tagtable are per-invocation on `import`, so a mixed set needs three
// separate runs in an order the operator has to know), and retries to a fixpoint.
public sealed record ImportAllCommandOptions(
    string ProjectIdentifier,
    string GroupPath,
    IReadOnlyList<string> Paths,
    bool Json,
    bool DryRun,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// The bulk half of the compile gate: FI-52 means only a per-block/per-type compile clears an
// imported block's inconsistent flag, and after a restore that is ninety-odd of them.
public sealed record CompileAllCommandOptions(
    string ProjectIdentifier,
    string? Device,
    bool Json,
    bool Force,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record CompileCommandOptions(
    string ProjectIdentifier,
    string? Device,
    string? Block,
    string? Type,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record DeleteCommandOptions(
    string ProjectIdentifier,
    string BlockName,
    string? Device,
    bool Confirm,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// Grounding/scaffolding command (2026-07-14, `PlantAutoControl` round-trip plan Phase 0.2) — creates an
// instance DB backing an already-existing FB, for FBs imported standalone with no calling context.
// Not S6+ logic generation: invents no tag/address/DB number (DbName is engineer-supplied, the DB
// number itself is always auto-assigned by TIA).
public sealed record CreateInstanceDbCommandOptions(
    string ProjectIdentifier,
    string GroupPath,
    string DbName,
    string InstanceOfName,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// No <project> positional: portal-status inspects the running Portal *processes*, not a project,
// so it never connects or opens anything. Carries the common flags (--json + install/timeouts) only
// for uniformity with every other command; the timeout values are unused (it does no connect/open).
public sealed record PortalStatusOptions(
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// Screen defaults to null rather than "*": summarising every screen is cheap, reading every item on
// every screen is not, so the expensive mode is opt-in. MaxItems bounds a single screen's read.
public sealed record HmiOptions(
    string ProjectIdentifier,
    string? Screen,
    int MaxItems,
    bool Json,
    bool Schema,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds,
    bool Scripts = false);

// The only HMI command that writes. Confirm mirrors `delete`'s own gate: the mutating commands in
// this tool state what they will do and require --yes before doing it. ItemTypes are CLR type names
// from `hmi --schema`'s own creatable list.
public sealed record HmiCreateScreenOptions(
    string ProjectIdentifier,
    string ScreenName,
    long Width,
    long Height,
    IReadOnlyList<string> ItemTypes,
    bool Confirm,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// The second writing HMI command. Sets are "<Target>.<Attribute>=<Value>"; events are
// "<Target>:<EventType>[=<script>]". Target is an item name, or the literal "Screen" for the screen
// itself. Same --yes gate as the other mutating commands.
public sealed record HmiEditScreenOptions(
    string ProjectIdentifier,
    string ScreenName,
    IReadOnlyList<(string Target, string Attribute, string Value)> Sets,
    IReadOnlyList<(string Target, string EventType, string? Script)> Events,
    IReadOnlyList<(string Target, string Property, string Tag)> Binds,
    // (What, Target, Detail): What is "item" | "bind" | "event"; Detail is the property or event
    // type where one applies. Screen-scoped, so it cannot go through hmi-delete.
    IReadOnlyList<(string What, string Target, string? Detail)> Deletes,
    IReadOnlyList<string> AddItems,
    // (Target, Property, DynamizationKind) — the non-tag dynamization kinds.
    IReadOnlyList<(string Target, string Property, string Kind)> BindKinds,
    // (Target, Property) — wipe the mapping table on that tag dynamization. Applied before Maps, so
    // a command carrying both is re-runnable rather than duplicating entries each time.
    IReadOnlyList<(string Target, string Property)> MapClears,
    // (Target, Property, EntrySpec) — "<EntryType>[;<Attr>=<Value>]..." per entry.
    IReadOnlyList<(string Target, string Property, string EntrySpec)> Maps,
    bool Confirm,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// Metamodel-driven object commands. `Kind` names a composition on HmiSoftware (Screens, Tags,
// DiscreteAlarms, ...); one options record serves create, delete and inventory because the shape is
// the same and three near-identical records would only drift.
public sealed record LibraryOptions(
    string ProjectIdentifier,
    bool IncludeMasterCopies,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds,
    string? ExportTypeName = null,
    string? ExportVersion = null,
    string? OutDirectory = null,
    string? ProbeDocumentsTypeName = null);

public sealed record HmiObjectOptions(
    string ProjectIdentifier,
    string Kind,
    string Name,
    string? Parent,
    bool AllowAnyName,
    // `hmi-set` only: plain attributes, and MultilingualText attributes, which need a different
    // write path entirely.
    IReadOnlyList<(string Attribute, string Value)> Sets,
    IReadOnlyList<(string Attribute, string Value)> Texts,
    bool Confirm,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// The one HMI-tag write. Kept separate from screen editing because a tag is device-scoped, not
// screen-scoped, and creating one is not part of editing a screen.
public sealed record HmiCreateTagOptions(
    string ProjectIdentifier,
    string TagName,
    string TableName,
    string DataType,
    bool Confirm,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public abstract record ParseResult
{
    private ParseResult()
    {
    }

    public sealed record ListSuccess(ListOptions Options) : ParseResult;

    public sealed record ExportSuccess(ExportCommandOptions Options) : ParseResult;

    public sealed record ExportAllSuccess(ExportAllCommandOptions Options) : ParseResult;

    public sealed record ImportSuccess(ImportCommandOptions Options) : ParseResult;

    public sealed record ImportAllSuccess(ImportAllCommandOptions Options) : ParseResult;

    public sealed record CompileSuccess(CompileCommandOptions Options) : ParseResult;

    public sealed record CompileAllSuccess(CompileAllCommandOptions Options) : ParseResult;

    public sealed record DeleteSuccess(DeleteCommandOptions Options) : ParseResult;

    public sealed record CreateInstanceDbSuccess(CreateInstanceDbCommandOptions Options) : ParseResult;

    public sealed record SanityCheckSuccess(ListOptions Options) : ParseResult;

    public sealed record PortalStatusSuccess(PortalStatusOptions Options) : ParseResult;

    public sealed record HmiSuccess(HmiOptions Options) : ParseResult;

    public sealed record HmiCreateScreenSuccess(HmiCreateScreenOptions Options) : ParseResult;

    public sealed record HmiEditScreenSuccess(HmiEditScreenOptions Options) : ParseResult;

    // Reuses CompileCommandOptions: an HMI compile takes the same device/json/timeout shape, and a
    // parallel record would only invite the two to drift.
    public sealed record HmiCompileSuccess(CompileCommandOptions Options) : ParseResult;

    public sealed record HmiCreateTagSuccess(HmiCreateTagOptions Options) : ParseResult;

    public sealed record HmiNewSuccess(HmiObjectOptions Options) : ParseResult;

    public sealed record HmiDeleteSuccess(HmiObjectOptions Options) : ParseResult;

    public sealed record HmiInventorySuccess(HmiObjectOptions Options) : ParseResult;

    public sealed record HmiSetSuccess(HmiObjectOptions Options) : ParseResult;

    public sealed record LibrarySuccess(LibraryOptions Options) : ParseResult;

    public sealed record Failure(string Message) : ParseResult;
}

public static class ArgumentParser
{
    /// <summary>
    /// Splits an item spec of the form <c>TypeName[:ContainedTypeValue]</c>, as taken by
    /// <c>hmi-create-screen --item</c> and <c>hmi-edit-screen --add-item</c>.
    /// </summary>
    /// <remarks>
    /// The contained type is not decoration. Container types refuse the one-argument
    /// <c>Create&lt;T&gt;(name)</c> outright, and Openness says so in as many words —
    /// <c>'ContainedTypeValue' parameter is missing. Please use correct method for object
    /// creation.</c> — so for a faceplate, custom web control or custom widget container the
    /// two-argument overload is the ONLY way to construct one. Measured 2026-08-09; it is also
    /// one of the few Openness refusals that names its own reason.
    /// </remarks>
    public static (string ItemType, string? ContainedType) SplitItemSpec(string spec)
    {
        if (spec is null)
        {
            throw new ArgumentNullException(nameof(spec));
        }

        var separator = spec.IndexOf(':');
        if (separator < 0)
        {
            return (spec.Trim(), null);
        }

        var itemType = spec.Substring(0, separator).Trim();
        var contained = spec.Substring(separator + 1).Trim();
        return (itemType, contained.Length == 0 ? null : contained);
    }

    public const int DefaultTimeoutConnectSeconds = 180;
    public const int DefaultTimeoutOpenSeconds = 1800;

    // Generous enough that a real screen is never silently clipped in practice, low enough that a
    // pathological one cannot stall a run. Truncation is always visible in the output.
    public const int DefaultHmiMaxItems = 500;

    // A Unified Comfort Panel content area, i.e. a plausible screen rather than a 0x0 one. Overridable.
    public const int DefaultHmiScreenWidth = 1280;
    public const int DefaultHmiScreenHeight = 615;

    private const string Usage =
        "Usage:\n" +
        "  openness-cli list          <project> [--json] [--tagtables] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli export        <project> (--block <name> | --type <name> | --tagtable <name>) --out <path> [--device <name>] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli export-all    <project> --out <dir> [--device <name>] [--tagtables] [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "    Exports EVERY block and PLC data type to one directory, so `converter drift-check --project <ir-dir> --exports <dir> --complete` can compare the IR on disk against what is\n" +
        "    actually in the controller (FI-70). Safety blocks are REFUSED and NAMED, never silently omitted - a dump missing a file is read as 'not in the controller' by the completeness\n" +
        "    check, which would turn a correct refusal into a false finding. Exits 7 if any export failed or was refused; --tagtables is opt-in (a tag table has no .ir counterpart to pair with).\n" +
        "  openness-cli import        <project> --group <device>/<path> [--type | --tagtable] <files...> [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli import-all    <project> --group <device>/<path> <dirs-or-files...> [--json] [--dry-run] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "    Bulk restore: classifies every file (tag table / PLC data type / block) from its own root element, imports tag tables then types then blocks, and RETRIES failures until a pass\n" +
        "    makes no progress - so a dependency order nobody can supply from filenames does not have to be supplied. --dry-run prints the plan and never contacts Portal. Exits 13 if any\n" +
        "    file did not go in, INCLUDING one that was never attempted (unreadable, unclassifiable, duplicate name) - a project missing a block looks exactly like a whole one.\n" +
        "  openness-cli compile-all   <project> [--device <name>] [--json] [--force] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "    Compiles EVERY inconsistent type and block, types first, in one session - the bulk half of the gate. FI-52: a device compile does not clear the inconsistent flag an imported block\n" +
        "    carries, so only a per-item compile does, and after a restore that is ninety-odd of them. Exits 8 if any item compiled with errors, 11 if any remain inconsistent. Errors and\n" +
        "    inconsistency are counted separately: one means examined-and-wrong, the other means not examined at all.\n" +
        "  openness-cli compile       <project> [--device <name>] [--block <name> | --type <name>] [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli delete        <project> --block <name> [--device <name>] --yes [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName> [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli library       <project> [--master-copies] [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "    READ-ONLY walk of the project library: every type with its CLR class name, status, supported export formats and versions. Faceplates are library types, not device content.\n" +
        "  openness-cli library       <project> --export-version <TypeName> [--version <v>] --out <directory>\n" +
        "    Calls LibraryTypeVersion.Export(FileInfo, ExportOptions) - a SECOND, format-free export distinct from the type-level ExportAsDocuments.\n" +
        "    GetSupportedExportFormats() is empty for every HMI type, but CreateFromDocuments takes no format either, so that emptiness never constrained this call.\n" +
        "    Without --version the DEFAULT version is exported. Reports how the call was bound (overload, parameter type, options value) so a negative result is diagnosable.\n" +
        "    Exits 7 if the call returns without writing anything: an export that produced nothing is a failed export, not a quiet success.\n" +
        "  openness-cli library       <project> --probe-documents <TypeName> --out <directory>\n" +
        "    Calls EVERY Export* overload on the type - including ExportAsDocuments when GetSupportedExportFormats() is EMPTY - and reports each binding and outcome.\n" +
        "    An empty format list has been treated as a gate since P10 and has never been TESTED as one. An empty advertisement is not a refusal. A throw here is a RESULT.\n" +
        "  openness-cli sanity-check  <project> [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli portal-status [--json] [--tia-install <path>]\n" +
        "  openness-cli hmi           <project> [--screen <name>|*] [--schema] [--max-items <n>] [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  <project> is either the name of a project already open in TIA Portal, or a path to a .apNN file.\n" +
        "  --type selects a PLC data type (UDT) instead of a block; on import it's a switch (no value) applying to all files.\n" +
        "  --tagtable selects a PLC tag table instead of a block; on export it takes a name, on import it's a switch (no value) applying to all files.\n" +
        "  list --tagtables enumerates tag tables instead of blocks.\n" +
        "  hmi is read-only. Without --screen it summarises screens; --screen <name> (or * for all) also reads that screen's items and dynamizations.\n" +
        "  hmi --scripts dumps the FULL body of every event handler, not just the one-line preview. On a real Unified project the behaviour lives in these\n" +
        "    handlers (274 of them in the reference project, against zero script modules), so the structural listing alone describes the skeleton and omits the animal.\n" +
        "  hmi --schema reports the metamodel instead: creatable screen-item types, and every attribute's access mode and create-relevance (Mandatory/Relevant/None).\n" +
        "    WinCC Unified has no screen export, so this is what stands in for a screen XML. Unified only — classic exposes no screen items. Implies --screen * unless one is given.\n" +
        "  openness-cli hmi-create-tag <project> --name <name> --table <table> [--datatype <t>] --yes\n" +
        "  openness-cli hmi-compile    <project> [--device <name>] [--json]\n" +
        "  openness-cli hmi-inventory  <project> [--kind <Composition>] [--json]      # read-only census of HMI objects\n" +
        "  openness-cli hmi-new        <project> --kind <Composition> --name <name> [--in <parent>] --yes\n" +
        "  openness-cli hmi-delete     <project> --kind <Composition> --name <name> --yes [--allow-any-name]\n" +
        "    --kind names a composition on HmiSoftware (Screens, Tags, TagTables, ScreenGroups, DiscreteAlarms, AlarmClasses, Connections, DataLogs, ...).\n" +
        "    hmi-delete refuses any name not starting with 'ZZ_AI_' unless --allow-any-name is given: it only removes its own probe artifacts.\n" +
        "  openness-cli hmi-create-screen <project> --name <name> [--width <n>] [--height <n>] [--item <TypeName>[:<ContainedType>]]... --yes [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "    Creates a screen, runs Validate(), saves. Never overwrites an existing screen; --yes required. --item takes a type from `hmi --schema`'s creatable list.\n" +
        "    CONTAINER types (HmiFaceplateContainer, HmiCustomWebControlContainer, HmiCustomWidgetContainer) REFUSE the plain form: Openness answers\n" +
        "      \"'ContainedTypeValue' parameter is missing\". Give the contained type after a colon - e.g. --item HmiFaceplateContainer:MyFaceplateType\n" +
        "      (a library type name from `openness-cli library`). Same syntax on hmi-edit-screen's --add-item.\n" +
        "  openness-cli hmi-edit-screen <project> --name <name> [--set <Target>.<Attr>=<Value>]... [--event <Target>:<EventType>[=<script>|@<file>]]... --yes [--json] [...]\n" +
        "    --event is idempotent: an existing handler for that event is UPDATED, not duplicated. '@<file>' loads a multi-line script body; the script's SyntaxCheck() is run and reported.\n" +
        "    Modifies an EXISTING screen and/or attaches event handlers. Target is an item name, or 'Screen' for the screen itself. --yes required.\n" +
        "    Event names are touch-first: Tapped/ContextTapped/KeyDown/KeyUp (buttons add Down/Up), screens use Loaded/Unloaded. There is no 'Click'.\n" +
        "    --set targets NEST: '<Item>.<Property>.<DynAttr>' reaches a dynamization's own attributes, and the path continues through\n" +
        "      engineering objects and compositions — e.g. 'Rect_1.BackColor.FlashingRate=Fast' or\n" +
        "      'Rect_1.BackColor.ValueConverter.MappingTable.ConditionType=Range' or '...MappingTable.Entries[0].Flashing=True'.\n" +
        "    A value may carry an explicit CLR type when the API declares the member as 'object': color:#FF0000, int:3, uint:7, long:, ulong:,\n" +
        "      double:, bool:True, str:literal. Untagged values are coerced from the target's own GetAttributeInfos, as before.\n" +
        "  openness-cli hmi-edit-screen ... [--map-clear <Target>.<Property>]... [--map <Target>.<Property>=<EntrySpec>]...\n" +
        "    Drives TagDynamization -> ValueConverter -> MappingTable -> Entries: the value-to-colour-and-flash mechanism an alarm display is built from.\n" +
        "    <EntrySpec> is '<EntryType>[;<Attr>=<Value>]...' where EntryType is Simple | Range | Bitmask | Base, or 'bits:SingleBit' / 'bits:MultiBit'\n" +
        "      for the non-generic Create(BitDynamizationType) overload, which creates a whole SET of bitmask entries in one call.\n" +
        "      e.g. --map \"Rect_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;Flashing=True;FlashingRate=Fast\"\n" +
        "    Every entry created is READ BACK field by field and reported with the CLR type stored, because Value/AlternateValue are declared 'object'\n" +
        "      and nothing in the metamodel says what they want. --map-clear deletes all entries first, so a re-run does not stack duplicates.\n" +
        "    Requires a tag binding on that property already (--bind): only a TagDynamization carries a ValueConverter.";

    /// <summary>
    /// Pulls the flags every subcommand shares off whichever options record the parse produced.
    /// Lives here, next to the records it reads, rather than in <c>Program</c> — and is public so a
    /// unit test can assert it handles EVERY success variant. That test exists because it was
    /// needed: `hmi` shipped its own dispatch case, built clean, passed 152 tests, and still died
    /// at runtime on this switch, which nothing had covered. Same reasoning that made
    /// <c>ExitCodes</c> public on 2026-08-05 — a second dispatch on the same type is exactly where
    /// a new subcommand gets forgotten.
    /// </summary>
    // FI-68. --tia-install is resolved HERE rather than in each of the fifteen Parse* methods that
    // accept it: this accessor is the single place every command's copy is read, so one wrapper covers
    // all of them and a command added later inherits it. Openness rejects a relative path with an
    // exception that names something else entirely; see PathArguments.
    public static (string? TiaInstallOverride, int TimeoutConnectSeconds, int TimeoutOpenSeconds) CommonOptions(ParseResult result)
    {
        var raw = RawCommonOptions(result);
        return (PathArguments.ToAbsoluteOrNull(raw.TiaInstallOverride), raw.TimeoutConnectSeconds, raw.TimeoutOpenSeconds);
    }

    private static (string? TiaInstallOverride, int TimeoutConnectSeconds, int TimeoutOpenSeconds) RawCommonOptions(ParseResult result) => result switch
    {
        ParseResult.ListSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ExportSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ExportAllSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ImportSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ImportAllSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.CompileSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.CompileAllSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.DeleteSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.CreateInstanceDbSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.SanityCheckSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.PortalStatusSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiCreateScreenSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiEditScreenSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiCompileSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiCreateTagSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiNewSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiDeleteSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiInventorySuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.HmiSetSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.LibrarySuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        _ => throw new InvalidOperationException($"Unhandled parse result: {result.GetType().Name}"),
    };

    /// <summary>
    /// The project this parse targets, or null for `portal-status`, which targets none. Used to tell
    /// <c>Connect</c> which running Portal to prefer — see <c>ChooseProcessToAttach</c>. Same
    /// every-variant guard as <see cref="CommonOptions"/> covers this.
    /// </summary>
    public static string? ProjectIdentifier(ParseResult result) => result switch
    {
        ParseResult.ListSuccess s => s.Options.ProjectIdentifier,
        ParseResult.ExportSuccess s => s.Options.ProjectIdentifier,
        ParseResult.ExportAllSuccess s => s.Options.ProjectIdentifier,
        ParseResult.ImportSuccess s => s.Options.ProjectIdentifier,
        ParseResult.ImportAllSuccess s => s.Options.ProjectIdentifier,
        ParseResult.CompileSuccess s => s.Options.ProjectIdentifier,
        ParseResult.CompileAllSuccess s => s.Options.ProjectIdentifier,
        ParseResult.DeleteSuccess s => s.Options.ProjectIdentifier,
        ParseResult.CreateInstanceDbSuccess s => s.Options.ProjectIdentifier,
        ParseResult.SanityCheckSuccess s => s.Options.ProjectIdentifier,
        ParseResult.PortalStatusSuccess => null,
        ParseResult.HmiSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiCreateScreenSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiEditScreenSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiCompileSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiCreateTagSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiNewSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiDeleteSuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiInventorySuccess s => s.Options.ProjectIdentifier,
        ParseResult.HmiSetSuccess s => s.Options.ProjectIdentifier,
        ParseResult.LibrarySuccess s => s.Options.ProjectIdentifier,
        _ => throw new InvalidOperationException($"Unhandled parse result: {result.GetType().Name}"),
    };

    public static ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParseResult.Failure(Usage);
        }

        return args[0] switch
        {
            "list" => ParseList(args),
            "export" => ParseExport(args),
            "export-all" => ParseExportAll(args),
            "import" => ParseImport(args),
            "import-all" => ParseImportAll(args),
            "compile" => ParseCompile(args),
            "compile-all" => ParseCompileAll(args),
            "delete" => ParseDelete(args),
            "create-instance-db" => ParseCreateInstanceDb(args),
            "sanity-check" => ParseSanityCheck(args),
            "portal-status" => ParsePortalStatus(args),
            "hmi" => ParseHmi(args),
            "hmi-create-screen" => ParseHmiCreateScreen(args),
            "hmi-edit-screen" => ParseHmiEditScreen(args),
            "hmi-create-tag" => ParseHmiCreateTag(args),
            "hmi-new" => ParseHmiObject(args, "hmi-new", requireName: true, requireConfirm: true),
            "hmi-delete" => ParseHmiObject(args, "hmi-delete", requireName: true, requireConfirm: true),
            "hmi-inventory" => ParseHmiObject(args, "hmi-inventory", requireName: false, requireConfirm: false),
            "hmi-set" => ParseHmiObject(args, "hmi-set", requireName: true, requireConfirm: true),
            "library" => ParseLibrary(args),
            // Reuses ParseCompile so the flags stay identical to `compile`; only the ParseResult
            // differs, which is what routes it to the HMI-aware device lookup.
            "hmi-compile" => ParseCompile(args) switch
            {
                ParseResult.CompileSuccess ok => new ParseResult.HmiCompileSuccess(ok.Options),
                var other => other,
            },
            var other => new ParseResult.Failure(
                $"Unknown subcommand '{other}'. Supported subcommands: list, export, import, compile, delete, create-instance-db, sanity-check, portal-status, library, hmi, hmi-compile, hmi-create-screen, hmi-edit-screen, hmi-create-tag, hmi-inventory, hmi-new, hmi-delete, hmi-set.{Environment.NewLine}{Usage}"),
        };
    }

    private static ParseResult ParseList(string[] args)
    {
        string? projectIdentifier = null;
        var json = false;
        var tagTables = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--tagtables":
                    tagTables = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ListSuccess(new ListOptions(projectIdentifier, json, tagTables, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseSanityCheck(string[] args)
    {
        // Same flag shape as `list` (project + --json + common flags, no --device — checks
        // every device in the project), so reuse its parsing directly rather than duplicating it.
        var result = ParseList(args);
        return result is ParseResult.ListSuccess success ? new ParseResult.SanityCheckSuccess(success.Options) : result;
    }

    private static ParseResult ParsePortalStatus(string[] args)
    {
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    // No <project> positional here — portal-status inspects Portal processes, not a
                    // project. Anything not a recognised flag is a mistake, flagged rather than swallowed.
                    return new ParseResult.Failure(
                        $"Unexpected argument '{args[i]}'. `portal-status` takes no <project> and no positional arguments — it inspects running Portal processes.{Environment.NewLine}{Usage}");
            }
        }

        return new ParseResult.PortalStatusSuccess(new PortalStatusOptions(json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseExport(string[] args)
    {
        string? projectIdentifier = null;
        string? block = null;
        string? type = null;
        string? tagTable = null;
        string? device = null;
        string? outPath = null;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--block":
                    if (!TryTakeValue(args, ref i, "--block", out block, out var blockErr))
                    {
                        return new ParseResult.Failure(blockErr);
                    }

                    break;
                case "--type":
                    if (!TryTakeValue(args, ref i, "--type", out type, out var typeErr))
                    {
                        return new ParseResult.Failure(typeErr);
                    }

                    break;
                case "--tagtable":
                    if (!TryTakeValue(args, ref i, "--tagtable", out tagTable, out var tagTableErr))
                    {
                        return new ParseResult.Failure(tagTableErr);
                    }

                    break;
                case "--out":
                    if (!TryTakeValue(args, ref i, "--out", out outPath, out var outErr))
                    {
                        return new ParseResult.Failure(outErr);
                    }

                    break;
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        var selectedCount = (block is not null ? 1 : 0) + (type is not null ? 1 : 0) + (tagTable is not null ? 1 : 0);
        if (selectedCount == 0)
        {
            return new ParseResult.Failure($"Missing required flag: --block <name>, --type <name>, or --tagtable <name>.{Environment.NewLine}{Usage}");
        }

        if (selectedCount > 1)
        {
            return new ParseResult.Failure($"--block, --type, and --tagtable are mutually exclusive.{Environment.NewLine}{Usage}");
        }

        if (outPath is null)
        {
            return new ParseResult.Failure($"Missing required flag: --out <path>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ExportSuccess(new ExportCommandOptions(projectIdentifier, block, type, tagTable, device, PathArguments.ToAbsolute(outPath), tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseExportAll(string[] args)
    {
        string? projectIdentifier = null;
        string? outDir = null;
        string? device = null;
        var includeTagTables = false;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":
                    if (!TryTakeValue(args, ref i, "--out", out outDir, out var outErr))
                    {
                        return new ParseResult.Failure(outErr);
                    }

                    break;
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--tagtables":
                    includeTagTables = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (outDir is null)
        {
            return new ParseResult.Failure($"Missing required flag: --out <dir>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ExportAllSuccess(new ExportAllCommandOptions(
            projectIdentifier, PathArguments.ToAbsolute(outDir), device, includeTagTables, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseImport(string[] args)
    {
        string? projectIdentifier = null;
        string? group = null;
        var files = new List<string>();
        var asType = false;
        var asTagTable = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--group":
                    if (!TryTakeValue(args, ref i, "--group", out group, out var groupErr))
                    {
                        return new ParseResult.Failure(groupErr);
                    }

                    break;
                case "--type":
                    asType = true;
                    break;
                case "--tagtable":
                    asTagTable = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        return new ParseResult.Failure($"Unknown flag '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    if (projectIdentifier is null)
                    {
                        projectIdentifier = args[i];
                    }
                    else
                    {
                        files.Add(args[i]);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (group is null)
        {
            return new ParseResult.Failure($"Missing required flag: --group <device>/<path>.{Environment.NewLine}{Usage}");
        }

        if (files.Count == 0)
        {
            return new ParseResult.Failure($"Missing required argument: at least one <file>.{Environment.NewLine}{Usage}");
        }

        if (asType && asTagTable)
        {
            return new ParseResult.Failure($"--type and --tagtable are mutually exclusive.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ImportSuccess(new ImportCommandOptions(projectIdentifier, group, files.Select(PathArguments.ToAbsolute).ToList(), asType, asTagTable, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseCompileAll(string[] args)
    {
        string? projectIdentifier = null;
        string? device = null;
        var json = false;
        var force = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--json":
                    json = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        return new ParseResult.Failure($"Unknown flag '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    if (projectIdentifier is not null)
                    {
                        return new ParseResult.Failure($"Unexpected argument '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    projectIdentifier = args[i];
                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.CompileAllSuccess(new CompileAllCommandOptions(
            projectIdentifier, device, json, force, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseImportAll(string[] args)
    {
        string? projectIdentifier = null;
        string? group = null;
        var paths = new List<string>();
        var json = false;
        var dryRun = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--group":
                    if (!TryTakeValue(args, ref i, "--group", out group, out var groupErr))
                    {
                        return new ParseResult.Failure(groupErr);
                    }

                    break;
                case "--json":
                    json = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        return new ParseResult.Failure($"Unknown flag '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    if (projectIdentifier is null)
                    {
                        projectIdentifier = args[i];
                    }
                    else
                    {
                        paths.Add(args[i]);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (group is null)
        {
            return new ParseResult.Failure($"Missing required flag: --group <device>/<path>.{Environment.NewLine}{Usage}");
        }

        if (paths.Count == 0)
        {
            return new ParseResult.Failure($"Missing required argument: at least one <dir-or-file>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ImportAllSuccess(new ImportAllCommandOptions(
            projectIdentifier, group, paths.Select(PathArguments.ToAbsolute).ToList(), json, dryRun, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseCompile(string[] args)
    {
        string? projectIdentifier = null;
        string? device = null;
        string? block = null;
        string? type = null;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--block":
                    if (!TryTakeValue(args, ref i, "--block", out block, out var blockErr))
                    {
                        return new ParseResult.Failure(blockErr);
                    }

                    break;
                case "--type":
                    if (!TryTakeValue(args, ref i, "--type", out type, out var typeErr))
                    {
                        return new ParseResult.Failure(typeErr);
                    }

                    break;
                case "--json":
                    json = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (block is not null && type is not null)
        {
            return new ParseResult.Failure($"--block and --type are mutually exclusive.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.CompileSuccess(new CompileCommandOptions(projectIdentifier, device, block, type, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseDelete(string[] args)
    {
        string? projectIdentifier = null;
        string? block = null;
        string? device = null;
        var confirm = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--block":
                    if (!TryTakeValue(args, ref i, "--block", out block, out var blockErr))
                    {
                        return new ParseResult.Failure(blockErr);
                    }

                    break;
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--yes":
                    confirm = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (block is null)
        {
            return new ParseResult.Failure($"Missing required flag: --block <name>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.DeleteSuccess(new DeleteCommandOptions(projectIdentifier, block, device, confirm, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseCreateInstanceDb(string[] args)
    {
        string? projectIdentifier = null;
        string? group = null;
        string? name = null;
        string? instanceOf = null;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--group":
                    if (!TryTakeValue(args, ref i, "--group", out group, out var groupErr))
                    {
                        return new ParseResult.Failure(groupErr);
                    }

                    break;
                case "--name":
                    if (!TryTakeValue(args, ref i, "--name", out name, out var nameErr))
                    {
                        return new ParseResult.Failure(nameErr);
                    }

                    break;
                case "--instance-of":
                    if (!TryTakeValue(args, ref i, "--instance-of", out instanceOf, out var instanceOfErr))
                    {
                        return new ParseResult.Failure(instanceOfErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (group is null)
        {
            return new ParseResult.Failure($"Missing required flag: --group <device>/<path>.{Environment.NewLine}{Usage}");
        }

        if (name is null)
        {
            return new ParseResult.Failure($"Missing required flag: --name <name>.{Environment.NewLine}{Usage}");
        }

        if (instanceOf is null)
        {
            return new ParseResult.Failure($"Missing required flag: --instance-of <FBName>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.CreateInstanceDbSuccess(new CreateInstanceDbCommandOptions(projectIdentifier, group, name, instanceOf, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseHmi(string[] args)
    {
        string? projectIdentifier = null;
        string? screen = null;
        var maxItems = DefaultHmiMaxItems;
        var json = false;
        var schema = false;
        var scripts = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--schema":
                    schema = true;
                    break;
                case "--scripts":
                    scripts = true;
                    break;
                case "--screen":
                    if (!TryTakeValue(args, ref i, "--screen", out screen, out var screenErr))
                    {
                        return new ParseResult.Failure(screenErr);
                    }

                    break;
                case "--max-items":
                    if (!TryTakeIntValue(args, ref i, "--max-items", out maxItems, out var maxErr))
                    {
                        return new ParseResult.Failure(maxErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        // --schema derives from whatever screens are walked, so on its own it means "all of them".
        // Requiring --screen * alongside it would be a trap with no upside.
        if (schema && screen is null)
        {
            screen = "*";
        }

        return new ParseResult.HmiSuccess(new HmiOptions(projectIdentifier, screen, maxItems, json, schema, tiaInstall, timeoutConnect, timeoutOpen, scripts));
    }

    private static ParseResult ParseHmiCreateScreen(string[] args)
    {
        string? projectIdentifier = null;
        string? name = null;
        var width = DefaultHmiScreenWidth;
        var height = DefaultHmiScreenHeight;
        var itemTypes = new List<string>();
        var confirm = false;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--yes":
                    confirm = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--name":
                    if (!TryTakeValue(args, ref i, "--name", out name, out var nameErr))
                    {
                        return new ParseResult.Failure(nameErr);
                    }

                    break;
                case "--width":
                    if (!TryTakeIntValue(args, ref i, "--width", out var w, out var widthErr))
                    {
                        return new ParseResult.Failure(widthErr);
                    }

                    width = w;
                    break;
                case "--height":
                    if (!TryTakeIntValue(args, ref i, "--height", out var h, out var heightErr))
                    {
                        return new ParseResult.Failure(heightErr);
                    }

                    height = h;
                    break;
                case "--item":
                    if (!TryTakeValue(args, ref i, "--item", out var item, out var itemErr))
                    {
                        return new ParseResult.Failure(itemErr);
                    }

                    itemTypes.Add(item!);
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (name is null)
        {
            return new ParseResult.Failure($"Missing required flag: --name <screen name>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.HmiCreateScreenSuccess(new HmiCreateScreenOptions(
            projectIdentifier, name, width, height, itemTypes, confirm, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseHmiEditScreen(string[] args)
    {
        string? projectIdentifier = null;
        string? name = null;
        var sets = new List<(string, string, string)>();
        var events = new List<(string, string, string?)>();
        var binds = new List<(string, string, string)>();
        var deletes = new List<(string, string, string?)>();
        var addItems = new List<string>();
        var bindKinds = new List<(string, string, string)>();
        var mapClears = new List<(string, string)>();
        var maps = new List<(string, string, string)>();
        var confirm = false;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--yes":
                    confirm = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--name":
                    if (!TryTakeValue(args, ref i, "--name", out name, out var nameErr))
                    {
                        return new ParseResult.Failure(nameErr);
                    }

                    break;
                case "--set":
                    if (!TryTakeValue(args, ref i, "--set", out var rawSet, out var setErr))
                    {
                        return new ParseResult.Failure(setErr);
                    }

                    if (!TryParseSet(rawSet!, out var set, out var setParseErr))
                    {
                        return new ParseResult.Failure(setParseErr);
                    }

                    sets.Add(set);
                    break;
                case "--event":
                    if (!TryTakeValue(args, ref i, "--event", out var rawEvent, out var evErr))
                    {
                        return new ParseResult.Failure(evErr);
                    }

                    if (!TryParseEvent(rawEvent!, out var ev, out var evParseErr))
                    {
                        return new ParseResult.Failure(evParseErr);
                    }

                    events.Add(ev);
                    break;
                case "--bind-kind":
                    if (!TryTakeValue(args, ref i, "--bind-kind", out var rawBindKind, out var bindKindErr))
                    {
                        return new ParseResult.Failure(bindKindErr);
                    }

                    // "<Target>.<Property>=<DynamizationKind>" — same grammar as --bind, but the
                    // value names the dynamization type rather than a tag.
                    if (!TryParseSet(rawBindKind!, out var bindKind, out var bindKindParseErr))
                    {
                        return new ParseResult.Failure(bindKindParseErr.Replace("--set", "--bind-kind"));
                    }

                    bindKinds.Add(bindKind);
                    break;
                case "--map":
                    if (!TryTakeValue(args, ref i, "--map", out var rawMap, out var mapErr))
                    {
                        return new ParseResult.Failure(mapErr);
                    }

                    // "<Target>.<Property>=<EntrySpec>" — same LHS grammar as --bind; the value is
                    // the entry spec, which may itself contain '=' inside its Attr=Value pairs, and
                    // TryParseSet splits on the FIRST '=' so the whole spec survives intact.
                    if (!TryParseSet(rawMap!, out var map, out var mapParseErr))
                    {
                        return new ParseResult.Failure(mapParseErr.Replace("--set", "--map"));
                    }

                    maps.Add(map);
                    break;
                case "--map-clear":
                    if (!TryTakeValue(args, ref i, "--map-clear", out var rawMapClear, out var mapClearErr))
                    {
                        return new ParseResult.Failure(mapClearErr);
                    }

                    var mapDot = rawMapClear!.LastIndexOf('.');
                    if (mapDot <= 0 || mapDot == rawMapClear.Length - 1)
                    {
                        return new ParseResult.Failure($"--map-clear expects '<Target>.<Property>', got '{rawMapClear}'.");
                    }

                    mapClears.Add((rawMapClear.Substring(0, mapDot), rawMapClear.Substring(mapDot + 1)));
                    break;
                case "--add-item":
                    if (!TryTakeValue(args, ref i, "--add-item", out var addItem, out var addItemErr))
                    {
                        return new ParseResult.Failure(addItemErr);
                    }

                    addItems.Add(addItem!);
                    break;
                case "--delete-item":
                    if (!TryTakeValue(args, ref i, "--delete-item", out var delItem, out var delItemErr))
                    {
                        return new ParseResult.Failure(delItemErr);
                    }

                    deletes.Add(("item", delItem!, null));
                    break;
                case "--delete-bind":
                    if (!TryTakeValue(args, ref i, "--delete-bind", out var delBind, out var delBindErr))
                    {
                        return new ParseResult.Failure(delBindErr);
                    }

                    // "<Target>.<Property>" — same left-hand grammar as --set/--bind.
                    var bindDot = delBind!.LastIndexOf('.');
                    if (bindDot <= 0 || bindDot == delBind.Length - 1)
                    {
                        return new ParseResult.Failure($"--delete-bind expects '<Target>.<Property>', got '{delBind}'.");
                    }

                    deletes.Add(("bind", delBind.Substring(0, bindDot), delBind.Substring(bindDot + 1)));
                    break;
                case "--delete-event":
                    if (!TryTakeValue(args, ref i, "--delete-event", out var delEvent, out var delEventErr))
                    {
                        return new ParseResult.Failure(delEventErr);
                    }

                    var evColon = delEvent!.IndexOf(':');
                    if (evColon <= 0 || evColon == delEvent.Length - 1)
                    {
                        return new ParseResult.Failure($"--delete-event expects '<Target>:<EventType>', got '{delEvent}'.");
                    }

                    deletes.Add(("event", delEvent.Substring(0, evColon), delEvent.Substring(evColon + 1)));
                    break;
                case "--bind":
                    if (!TryTakeValue(args, ref i, "--bind", out var rawBind, out var bindErr))
                    {
                        return new ParseResult.Failure(bindErr);
                    }

                    // Same "<Target>.<Property>=<Value>" grammar as --set; the value is a tag name.
                    if (!TryParseSet(rawBind!, out var bind, out var bindParseErr))
                    {
                        return new ParseResult.Failure(bindParseErr.Replace("--set", "--bind"));
                    }

                    binds.Add(bind);
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (name is null)
        {
            return new ParseResult.Failure($"Missing required flag: --name <screen name>.{Environment.NewLine}{Usage}");
        }

        // An edit command with no edits is a mistake worth catching at parse time — it would
        // otherwise open the project, change nothing, save, and report success.
        if (sets.Count == 0 && events.Count == 0 && binds.Count == 0 && deletes.Count == 0 && addItems.Count == 0
            && bindKinds.Count == 0 && mapClears.Count == 0 && maps.Count == 0)
        {
            return new ParseResult.Failure($"Nothing to do: pass at least one --set, --event, --bind, --bind-kind, --map, --map-clear, --add-item or --delete-*.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.HmiEditScreenSuccess(new HmiEditScreenOptions(
            projectIdentifier, name, sets, events, binds, deletes, addItems, bindKinds, mapClears, maps, confirm, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    // "<Target>.<Attribute>=<Value>". Split on the FIRST '=' so a value may contain one, and on the
    // LAST '.' before it so a target name may contain dots.
    private static bool TryParseSet(string raw, out (string Target, string Attribute, string Value) set, out string error)
    {
        set = default;
        var eq = raw.IndexOf('=');
        if (eq <= 0)
        {
            error = $"--set expects '<Target>.<Attribute>=<Value>', got '{raw}'.";
            return false;
        }

        var lhs = raw.Substring(0, eq);
        var value = raw.Substring(eq + 1);
        var dot = lhs.LastIndexOf('.');
        if (dot <= 0 || dot == lhs.Length - 1)
        {
            error = $"--set expects '<Target>.<Attribute>=<Value>', got '{raw}'. Use 'Screen' as the target for the screen itself.";
            return false;
        }

        set = (lhs.Substring(0, dot), lhs.Substring(dot + 1), value);
        error = string.Empty;
        return true;
    }

    // "<Target>:<EventType>", "<Target>:<EventType>=<inline script>", or
    // "<Target>:<EventType>@<path to script file>". The file form exists because a real handler body
    // is multi-line JavaScript, and passing that as one shell argument is miserable and error-prone.
    // The script is optional either way — an event handler with no script is legitimate.
    private static bool TryParseEvent(string raw, out (string Target, string EventType, string? Script) ev, out string error)
    {
        ev = default;
        var colon = raw.IndexOf(':');
        if (colon <= 0 || colon == raw.Length - 1)
        {
            error = $"--event expects '<Target>:<EventType>', '<Target>:<EventType>=<script>' or '<Target>:<EventType>@<file>', got '{raw}'.";
            return false;
        }

        var target = raw.Substring(0, colon);
        var rest = raw.Substring(colon + 1);

        // Whichever separator comes FIRST wins, so a '@' inside an inline script and an '=' inside a
        // file path are both harmless.
        var eq = rest.IndexOf('=');
        var at = rest.IndexOf('@');
        var useFile = at >= 0 && (eq < 0 || at < eq);
        var sep = useFile ? at : eq;

        if (sep < 0)
        {
            ev = (target, rest, null);
            error = string.Empty;
            return true;
        }

        if (sep == 0)
        {
            error = $"--event is missing an event type before the separator, got '{raw}'.";
            return false;
        }

        var eventType = rest.Substring(0, sep);
        var payload = rest.Substring(sep + 1);

        if (!useFile)
        {
            ev = (target, eventType, payload);
            error = string.Empty;
            return true;
        }

        if (payload.Length == 0)
        {
            error = $"--event '@' expects a script file path after it, got '{raw}'.";
            return false;
        }

        if (!System.IO.File.Exists(payload))
        {
            error = $"--event script file not found: '{payload}'.";
            return false;
        }

        ev = (target, eventType, System.IO.File.ReadAllText(payload));
        error = string.Empty;
        return true;
    }

    // One parser for hmi-new / hmi-delete / hmi-inventory: the flag shape is identical and three
    // near-copies would drift. `verb` decides which ParseResult comes back and whether --name and
    // --yes are required (inventory is read-only, so neither is).
    private static ParseResult ParseLibrary(string[] args)
    {
        string? projectIdentifier = null;
        var includeMasterCopies = false;
        var json = false;
        string? tiaInstall = null;
        string? exportTypeName = null;
        string? exportVersion = null;
        string? outDirectory = null;
        string? probeDocumentsTypeName = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--master-copies":
                    includeMasterCopies = true;
                    break;
                case "--export-version":
                    if (!TryTakeValue(args, ref i, "--export-version", out exportTypeName, out var exportErr))
                    {
                        return new ParseResult.Failure(exportErr);
                    }

                    break;
                case "--probe-documents":
                    if (!TryTakeValue(args, ref i, "--probe-documents", out probeDocumentsTypeName, out var probeErr))
                    {
                        return new ParseResult.Failure(probeErr);
                    }

                    break;
                case "--version":
                    if (!TryTakeValue(args, ref i, "--version", out exportVersion, out var versionErr))
                    {
                        return new ParseResult.Failure(versionErr);
                    }

                    break;
                case "--out":
                    if (!TryTakeValue(args, ref i, "--out", out outDirectory, out var outErr))
                    {
                        return new ParseResult.Failure(outErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        // --out is meaningless without a type to export, and an export with nowhere to write is a
        // silent no-op waiting to happen. Both directions are hard errors rather than defaults.
        if ((exportTypeName is not null || probeDocumentsTypeName is not null) && outDirectory is null)
        {
            return new ParseResult.Failure($"--export-version and --probe-documents require --out <directory>.{Environment.NewLine}{Usage}");
        }

        if (exportTypeName is null && probeDocumentsTypeName is null && (outDirectory is not null || exportVersion is not null))
        {
            return new ParseResult.Failure($"--out and --version are only meaningful with --export-version or --probe-documents.{Environment.NewLine}{Usage}");
        }

        if (exportTypeName is not null && probeDocumentsTypeName is not null)
        {
            return new ParseResult.Failure($"--export-version and --probe-documents are alternatives; pass one.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.LibrarySuccess(new LibraryOptions(
            projectIdentifier,
            includeMasterCopies,
            json,
            tiaInstall,
            timeoutConnect,
            timeoutOpen,
            exportTypeName,
            exportVersion,
            PathArguments.ToAbsoluteOrNull(outDirectory),
            probeDocumentsTypeName));
    }

    private static ParseResult ParseHmiObject(string[] args, string verb, bool requireName, bool requireConfirm)
    {
        string? projectIdentifier = null;
        string? kind = null;
        string? name = null;
        string? parent = null;
        var allowAnyName = false;
        var objSets = new List<(string, string)>();
        var objTexts = new List<(string, string)>();
        var confirm = false;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--yes":
                    confirm = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--allow-any-name":
                    allowAnyName = true;
                    break;
                case "--kind":
                    if (!TryTakeValue(args, ref i, "--kind", out kind, out var kindErr))
                    {
                        return new ParseResult.Failure(kindErr);
                    }

                    break;
                case "--name":
                    if (!TryTakeValue(args, ref i, "--name", out name, out var nameErr))
                    {
                        return new ParseResult.Failure(nameErr);
                    }

                    break;
                case "--set":
                    if (!TryTakeValue(args, ref i, "--set", out var objSet, out var objSetErr))
                    {
                        return new ParseResult.Failure(objSetErr);
                    }

                    if (!TryParseNameValue(objSet!, "--set", out var parsedSet, out var parsedSetErr))
                    {
                        return new ParseResult.Failure(parsedSetErr);
                    }

                    objSets.Add(parsedSet);
                    break;
                case "--text":
                    if (!TryTakeValue(args, ref i, "--text", out var objText, out var objTextErr))
                    {
                        return new ParseResult.Failure(objTextErr);
                    }

                    if (!TryParseNameValue(objText!, "--text", out var parsedText, out var parsedTextErr))
                    {
                        return new ParseResult.Failure(parsedTextErr);
                    }

                    objTexts.Add(parsedText);
                    break;
                case "--in":
                    if (!TryTakeValue(args, ref i, "--in", out parent, out var parentErr))
                    {
                        return new ParseResult.Failure(parentErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (requireName && kind is null)
        {
            return new ParseResult.Failure($"Missing required flag: --kind <Composition> (e.g. Screens, Tags, DiscreteAlarms).{Environment.NewLine}{Usage}");
        }

        if (requireName && name is null)
        {
            return new ParseResult.Failure($"Missing required flag: --name <name>.{Environment.NewLine}{Usage}");
        }

        if (verb == "hmi-set" && objSets.Count == 0 && objTexts.Count == 0)
        {
            return new ParseResult.Failure($"Nothing to do: pass at least one --set <Attr>=<Value> or --text <Attr>=<Value>.{Environment.NewLine}{Usage}");
        }

        var options = new HmiObjectOptions(
            projectIdentifier, kind ?? string.Empty, name ?? string.Empty, parent, allowAnyName,
            objSets, objTexts, confirm || !requireConfirm, json, tiaInstall, timeoutConnect, timeoutOpen);

        return verb switch
        {
            "hmi-new" => new ParseResult.HmiNewSuccess(options),
            "hmi-delete" => new ParseResult.HmiDeleteSuccess(options),
            "hmi-set" => new ParseResult.HmiSetSuccess(options),
            _ => new ParseResult.HmiInventorySuccess(options),
        };
    }

    // "<Name>=<Value>", split on the first '=' so values may contain one.
    private static bool TryParseNameValue(string raw, string flag, out (string Name, string Value) parsed, out string error)
    {
        parsed = default;
        var eq = raw.IndexOf('=');
        if (eq <= 0)
        {
            error = $"{flag} expects '<Attribute>=<Value>', got '{raw}'.";
            return false;
        }

        parsed = (raw.Substring(0, eq), raw.Substring(eq + 1));
        error = string.Empty;
        return true;
    }

    private static ParseResult ParseHmiCreateTag(string[] args)
    {
        string? projectIdentifier = null;
        string? name = null;
        string? table = null;
        var dataType = "Bool";
        var confirm = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--yes":
                    confirm = true;
                    break;
                case "--name":
                    if (!TryTakeValue(args, ref i, "--name", out name, out var nameErr))
                    {
                        return new ParseResult.Failure(nameErr);
                    }

                    break;
                case "--table":
                    if (!TryTakeValue(args, ref i, "--table", out table, out var tableErr))
                    {
                        return new ParseResult.Failure(tableErr);
                    }

                    break;
                case "--datatype":
                    if (!TryTakeValue(args, ref i, "--datatype", out var dt, out var dtErr))
                    {
                        return new ParseResult.Failure(dtErr);
                    }

                    dataType = dt!;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (name is null)
        {
            return new ParseResult.Failure($"Missing required flag: --name <tag name>.{Environment.NewLine}{Usage}");
        }

        if (table is null)
        {
            return new ParseResult.Failure($"Missing required flag: --table <tag table name>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.HmiCreateTagSuccess(new HmiCreateTagOptions(
            projectIdentifier, name, table, dataType, confirm, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static bool TryTakePositional(string arg, ref string? projectIdentifier, out string error)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            error = $"Unknown flag '{arg}'.{Environment.NewLine}{Usage}";
            return false;
        }

        if (projectIdentifier is not null)
        {
            error = $"Unexpected extra argument '{arg}'.{Environment.NewLine}{Usage}";
            return false;
        }

        projectIdentifier = arg;
        error = string.Empty;
        return true;
    }

    private static bool TryTakeValue(string[] args, ref int i, string flag, out string? value, out string error)
    {
        if (i + 1 >= args.Length)
        {
            value = null;
            error = $"Flag '{flag}' requires a value.";
            return false;
        }

        i++;
        value = args[i];
        error = string.Empty;
        return true;
    }

    private static bool TryTakeIntValue(string[] args, ref int i, string flag, out int value, out string error)
    {
        if (!TryTakeValue(args, ref i, flag, out var raw, out error))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(raw, out value) || value <= 0)
        {
            error = $"Flag '{flag}' requires a positive integer number of seconds, got '{raw}'.";
            return false;
        }

        return true;
    }
}
