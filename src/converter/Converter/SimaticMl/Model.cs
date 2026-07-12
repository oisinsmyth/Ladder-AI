namespace Converter.SimaticMl;

// SliceAccessModifier carries a real, documented site construct: bit-within-word alarm
// addressing (06-lad-conventions.md C-501: "DB_Alarms.EStopAlarm0.%X3", a documented exception
// to C-301) — confirmed real, 2026-07-10, as `SliceAccessModifier="x15"` on the LAST <Component>
// of an Access's Symbol path. Always assumed to be on the last component only.
//
// ArrayIndex carries a second real construct found the same day: a literal-constant array
// subscript on the final Component (`CommsProcessData.Node_Error[n]`) — confirmed from an
// untouched sibling block after a round-trip on the affected block silently collapsed three
// distinct array elements (Node_Error[1], [2], [3]) into one indistinguishable tag path, a real
// data-loss bug caught by the project owner reviewing the result in TIA, not by a test. XML
// shape: `<Component Name="Node_Error" AccessModifier="Array"><Access Scope="LiteralConstant">
// <Constant><ConstantType>DInt</ConstantType><ConstantValue>n</ConstantValue></Constant>
// </Access></Component>`. Seen only on the last component, same as SliceAccessModifier, and
// never confirmed together with one on the same Component — but nothing here assumes they're
// mutually exclusive.
public sealed record AccessNode(
    int UId,
    string Scope,
    IReadOnlyList<string> ComponentPath,
    string? SliceAccessModifier = null,
    int? ArrayIndex = null)
{
    // "[n]" and ".%X15" notation composed together — array index before slice, matching the one
    // real case observed of a not-yet-seen combination; the site's own ".%X15" convention is
    // preserved exactly, "[n]" is the natural/obvious choice for array subscript, not otherwise
    // used by the IR.
    public string DottedPath
    {
        get
        {
            var path = string.Join('.', ComponentPath);
            if (ArrayIndex is not null)
            {
                path += $"[{ArrayIndex}]";
            }

            if (SliceAccessModifier is not null)
            {
                path += $".%{SliceAccessModifier.ToUpperInvariant()}";
            }

            return path;
        }
    }

    /// <summary>Inverse of <see cref="DottedPath"/> — used when rebuilding an AccessNode from an IR tag string.</summary>
    public static AccessNode FromDottedPath(int uid, string scope, string dottedPath)
    {
        var sliceMatch = System.Text.RegularExpressions.Regex.Match(dottedPath, @"^(?<rest>.+)\.%(?<slice>[A-Za-z]\d+)$");
        var slice = sliceMatch.Success ? sliceMatch.Groups["slice"].Value.ToLowerInvariant() : null;
        var rest = sliceMatch.Success ? sliceMatch.Groups["rest"].Value : dottedPath;

        var arrayMatch = System.Text.RegularExpressions.Regex.Match(rest, @"^(?<path>.+)\[(?<index>\d+)\]$");
        var arrayIndex = arrayMatch.Success ? int.Parse(arrayMatch.Groups["index"].Value) : (int?)null;
        var path = arrayMatch.Success ? arrayMatch.Groups["path"].Value : rest;

        return new AccessNode(uid, scope, path.Split('.'), slice, arrayIndex);
    }
}

// Negated: a normally-closed contact (`<Negated Name="operand" />` child) — Contact-only.
// Cardinality: an OR-merge's branch count (`<TemplateValue Name="Card" Type="Cardinality">`) —
// "O"-only. Both confirmed real, 2026-07-10 (docs/notes/stage-gates.md, S1 item 7).
//
// TonVersion/TimeType/Instance: a TON's own `Version="1.0"` attribute, its
// `<TemplateValue Name="time_type" Type="Type">Time</TemplateValue>`, and its
// `<Instance Scope="..." UId="..."><Component .../></Instance>` — confirmed real, 2026-07-11,
// against `FB MotorDOL` (Scope="LocalVariable", multi-instance in the calling FB's own iDB) and
// `FC ControlDelays` (Scope="GlobalVariable", a standalone instance DB named directly by a
// single Component — not a two-component "DB_Timers.Member" path). Modeled as an AccessNode
// (not a new type) because the Instance element carries exactly the same data shape (scope +
// component path) as an ordinary Access — the `<Instance>` wrapper just skips the `<Symbol>`
// indirection an ordinary `<Access>` uses. This also means a future FC/FB call's own instance
// argument can reuse AccessNode rather than needing a redesign.
//
// SrcType: a comparison's (`Eq`/`Ge`) own `<TemplateValue Name="SrcType" Type="Type">` —
// confirmed real, 2026-07-11, `FC ControlDelays` (`Int` in every instance seen; stored verbatim,
// not assumed fixed).
//
// BlockName/BlockType/CallParameters: an FB/FC call (`<Call>`/`<CallInfo Name="..."
// BlockType="FB">`) — confirmed real, 2026-07-12, `FC PlantAutoControl` (20 real instances). Reuses
// Instance (same AccessNode shape as TON's own, confirmed identical). Genuinely different from
// every other Part kind: `<Call>` isn't a `<Part Name="Call">` in the source at all — it's its
// own sibling element under `<Parts>` (`<Call UId="N"><CallInfo ...>...</CallInfo></Call>`) —
// FlgNetParser adapts this into an ordinary PartNode(Name="Call") on the way in so the rest of
// the pipeline (OutPortFor, wire endpoint UId matching) never needs a parallel type. Only "FB"
// has been observed for BlockType — "FC" is real but unconfirmed on a Call specifically (same
// status as Gt/TOF elsewhere), stored verbatim rather than hard-validated to a constant, so a
// real FC call is at least represented faithfully rather than assumed impossible.
//
// AutomaticSrcType/DestType: `Mul`/`Convert` (S1 item 18, 2026-07-12, `FB MotorDOL`/`EquipmentControlSystem`/
// `ShredderControlSystem`). `Mul`'s own type is `<AutomaticTyped Name="SrcType" />` — a self-closing
// element with no value at all (TIA infers the type from the connected operands rather than
// declaring it statically), genuinely different from every other typed instruction's own
// `<TemplateValue Type="Type">X</TemplateValue>` shape — `AutomaticSrcType` is a bare bool
// (confirmed shape present, nothing to carry) rather than reusing `SrcType` (which would imply a
// value that doesn't exist in the source). `Convert` is typed *between* two types — `SrcType`
// (reused, already existing) and the new `DestType` — both ordinary `TemplateValue`s.
//
// All optional fields live on the one PartNode type rather than subtypes since every other Part
// kind (Coil, and Contact/O without these) is unaffected and the parser/writer already dispatch
// on `Name` for anything Part-shape-specific.
public sealed record PartNode(
    int UId,
    string Name,
    bool Negated = false,
    int? Cardinality = null,
    string? TonVersion = null,
    string? TimeType = null,
    AccessNode? Instance = null,
    string? SrcType = null,
    string? BlockName = null,
    string? BlockType = null,
    IReadOnlyList<CallParameterNode>? CallParameters = null,
    bool AutomaticSrcType = false,
    string? DestType = null);

// A wired parameter declared at a Call site — confirmed real, 2026-07-12, `FC PlantAutoControl`: only
// parameters that are actually wired appear here at all (19 of 20 real Call instances have zero
// — no <Parameter> element at all, not present-but-empty; the one real wired example,
// `TomraControlSystem`, has 10: 8 Section="Input", 2 Section="Output", all Type="Word"). Section is
// stored verbatim and validated to be exactly "Input" or "Output" at parse time — an "InOut"
// section is real in principle (an FB's own interface can have InOut parameters) but unconfirmed
// on a Call specifically, refused rather than guessed at. Name/Type mirror the source's own
// `<Parameter Name="..." Type="..." />` attributes exactly.
public sealed record CallParameterNode(string Name, string Section, string Type);

// A literal operand — either a TON `PT` (`Access Scope="TypedConstant"`, `ConstantType` absent)
// or a comparison operand (`Access Scope="LiteralConstant"` used at the *top level* — sibling to
// Contact/Eq under `<Parts>`, distinct from the array-index-only nested use of the same scope
// string — `ConstantType` always present, e.g. `Int`). Both confirmed real, 2026-07-11 (`FB
// MotorDOL/FC ControlDelays` and `FC ControlDelays` respectively). Unified into one type (not two)
// since both are "an Access carrying a literal Constant, no Symbol" — ConstantType is the one
// real difference, modeled as nullable rather than as a second type. Value is stored verbatim,
// never interpreted (same discipline as DB StartValue).
public sealed record ConstantAccessNode(int UId, string Value, string? ConstantType = null);

public enum EndpointKind
{
    Powerrail,
    IdentCon,
    NameCon,
    OpenCon,
}

public sealed record WireEndpoint(EndpointKind Kind, int? UId, string? PortName);

public sealed record WireNode(int UId, IReadOnlyList<WireEndpoint> Endpoints);

/// <summary>
/// One SimaticML network (a CompileUnit's FlgNet content): the Parts/Wires wiring graph,
/// per ADR-0001. AccessNodes and Parts both live inside the source's &lt;Parts&gt; element —
/// modeled separately here because they play different roles in the graph (data source vs.
/// instruction node).
/// </summary>
public sealed record FlgNetwork(
    IReadOnlyList<AccessNode> AccessNodes,
    IReadOnlyList<PartNode> Parts,
    IReadOnlyList<WireNode> Wires,
    IReadOnlyList<ConstantAccessNode>? Constants = null)
{
    public IReadOnlyList<ConstantAccessNode> Constants { get; init; } = Constants ?? Array.Empty<ConstantAccessNode>();
}

// CompileUnit "ID" is opaque — confirmed against a real export (2026-07-10) not to follow the
// same simple sequential-int scheme as FlgNet's own UIds (a real one came back as "D"). Treated
// as a string throughout, unlike Part/Wire/Access UId which are FlgNet-internal and stayed int.
//
// Title: a real, distinct MultilingualText[CompositionName="Title"] — confirmed real,
// 2026-07-12 (S1 item 16, `FC PlantAutoControl`, all 20 networks), the human-visible per-network
// label shown in the TIA LAD editor above each network, genuinely different from Comment (which
// has been empty on every real network seen all session — Title is what engineers actually use).
// This is why the IR's own `NETWORK <n> "<title>"` line was repurposed to carry this field
// rather than Comment (S1 item 16) — matching the field's own name and ir/SPEC.md's original
// sketch, which the converter's earlier build had accidentally wired to Comment instead, unnoticed
// until real data finally had non-trivial content in both fields at once.
public sealed record CompileUnitSource(string UId, string? Comment, string? Title, FlgNetwork Network);

// RootUId is the block element's own "ID" attribute (e.g. `<SW.Blocks.FC ID="0">`), separate
// from its CompileUnits' own IDs — confirmed real and required, 2026-07-10: Import() rejects a
// block element with no ID ("Cannot find the required 'ID' attribute element"). Opaque, like
// CompileUnitSource.UId — not assumed to always be "0" just because that's what one real
// export showed.
//
// StaticMembers: an FB's own Static Interface section — confirmed real, 2026-07-11 (S1 item 7
// Phase B, MotorDOL). Null when the source has no Static section at all (every FC seen — FCs
// have no instance data, so no Static section exists in the source to begin with, distinct from
// an FB with an empty one). Reuses DbMember's full shape (Retain/Version/SetPoint/nested
// members) — an instance DB's own Static section is a realization of exactly this same shape
// (DbSourceParser and BlockSourceParser share the same member-parsing helpers).
//
// TempMembers: every block seen (FC or FB) has a Temp section, even if empty (`<Section
// Name="Temp" />`), so this is never null — an empty list is the common case. Every real Temp
// member seen (MotorDOL) is the bare Name/Datatype shape a DB's own nested member has (no
// Remanence/Accessibility/AttributeList), so it reuses DbMember too, just never populating
// Retain/Version/SetPoint/NestedMembers (DbSourceParser hard-errors if a real Temp member ever
// shows more than that bare shape).
//
// InputMembers/OutputMembers: confirmed real, 2026-07-12 (S1 item 20, `FB TomraControlSystem` — 8
// Input, 2 Output, all Word-typed). Same shape as StaticMembers (Name/Datatype/Remanence/
// Accessibility + AttributeList), EXCEPT the AttributeList never carries a SetPoint
// BooleanAttribute the way Static's own does (confirmed: Static members in the same file carry
// 4 BooleanAttributes, Input/Output members carry only 3) — DbInterfaceMembers.ParseMember's
// `requireSetPoint` parameter handles this, not a separate DbMember shape. Nullable like
// StaticMembers (absent section vs. present-but-empty is a real distinction — every FC/most FB
// seen has these present-but-empty).
//
// InOutMembers: the section is always present (confirmed real in all 3 grounded FBs — an empty,
// self-closing `<Section Name="InOut" />`) but never populated in any real example seen — no
// content shape confirmed. Modeled the same as TempMembers (never null, empty is the only case
// observed) rather than StaticMembers' nullable convention, since section-absence was never
// actually observed for InOut.
//
// ConstantMembers: confirmed real, 2026-07-12 (S1 item 20, two independent instances — `FB
// MotorVSDSystem`/`AirStar`). A genuinely distinct third member shape — Name/Datatype/
// Accessibility="Public" plus a required StartValue, no AttributeList/Remanence at all — neither
// StaticMembers' full shape nor TempMembers' bare shape fits, hence
// DbInterfaceMembers.ParseConstantMember/WriteConstantMember rather than reusing ParseMember/
// ParseBareMember. Nullable like StaticMembers (TomraControlSystem has an empty Constant section;
// MotorVSDSystem/AirStar's are populated).
//
// Return remains out of scope — confirmed FC-specific (absent entirely, not even an empty
// element, on all 3 FBs grounded for this item) — the existing fixed-Void-Ret_Val handling
// already only applies when a Return section is actually present.
// Title: a real, distinct block-level MultilingualText[CompositionName="Title"] — confirmed real,
// 2026-07-12 (S1 item 17), on two of PlantAutoControl's own dependency FBs (`MotorVSDSystem`, `AirStar`,
// both titled "VSD Motor" — a shared, templated title across that FB family). Previously assumed
// always-empty (S1 item 16's own grounding only found network-level Title populated) and
// hard-errored on; this is the first real counter-example. Same treatment as network-level
// Title: read via MultilingualTextHelper.ReadMultilingualText, its own optional `TITLE "..."`
// line in the IR (mirroring the existing `COMMENT "..."` line — genuinely a new line, not a
// repurposed one, since the BLOCK line's own quoted text is the block's real Name, not available
// to repurpose the way a NETWORK line's label was).
public sealed record BlockSource(
    string RootUId,
    string Kind,
    string Name,
    int Number,
    string Language,
    string? Comment,
    IReadOnlyList<CompileUnitSource> CompileUnits,
    IReadOnlyList<DbMember>? StaticMembers = null,
    IReadOnlyList<DbMember>? TempMembers = null,
    string? Title = null,
    IReadOnlyList<DbMember>? InputMembers = null,
    IReadOnlyList<DbMember>? OutputMembers = null,
    IReadOnlyList<DbMember>? InOutMembers = null,
    IReadOnlyList<DbMember>? ConstantMembers = null)
{
    public IReadOnlyList<DbMember> TempMembers { get; init; } = TempMembers ?? Array.Empty<DbMember>();

    public IReadOnlyList<DbMember> InOutMembers { get; init; } = InOutMembers ?? Array.Empty<DbMember>();
}

public sealed class SimaticMlFormatException : Exception
{
    public SimaticMlFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A recognized-but-out-of-scope construct for this converter slice (e.g. a TON, an OR-merge,
/// a negated contact). Distinct from <see cref="SimaticMlFormatException"/>: the XML is
/// well-formed and understood, it's just not something this slice's converter can represent
/// yet (design philosophy #10 — hard error, not a silent partial result).
/// </summary>
public sealed class UnsupportedConstructException : Exception
{
    public UnsupportedConstructException(string message)
        : base(message)
    {
    }
}
