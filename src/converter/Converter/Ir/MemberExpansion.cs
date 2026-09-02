using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// How far a declared interface/DB member can be opened, and into what.
///
/// <para>Every value except <see cref="Inlined"/> and <see cref="NamedTypeOpened"/> terminates the
/// walk at this member; those two say "recurse into <c>Children</c>".</para>
/// </summary>
public enum MemberShape
{
    /// <summary>The member carries its own sub-members inlined in the IR — recurse into them.</summary>
    Inlined,

    /// <summary>
    /// The member's datatype names a PLC data type this corpus defines, and the walk descended into
    /// the type's own members. <b>FI-88's repair lives here</b> — see <see cref="MemberExpansion"/>.
    /// </summary>
    NamedTypeOpened,

    /// <summary>An FB instantiated as a static member of another FB. One leaf, never expanded here.</summary>
    MultiInstance,

    /// <summary>An IEC timer/counter instance. Instruction state, not interface. One leaf.</summary>
    IecInstance,

    /// <summary>An elementary type (or a member with no stated type at all). One leaf.</summary>
    Elementary,

    /// <summary>An <c>Array[…] of</c> something. ONE leaf standing for the whole array.</summary>
    Aggregate,

    /// <summary>
    /// A named type that could not be opened — absent from the corpus, or nested past the depth cap.
    /// One leaf, <b>and a gate</b>: the leaves under it are missing from a set that otherwise reads
    /// complete.
    /// </summary>
    Opaque,
}

/// <summary>The result of one classification. <c>Children</c> is non-empty only for the two recursing shapes.</summary>
public sealed record MemberClassification(
    MemberShape Shape,
    IReadOnlyList<DbMember> Children,
    string ElementType,
    string? OpaqueReason)
{
    /// <summary>Recurse into <see cref="Children"/>, or stop and emit one leaf?</summary>
    public bool Recurses => Shape is MemberShape.Inlined or MemberShape.NamedTypeOpened;

    public bool IsAggregate => Shape == MemberShape.Aggregate;

    public bool IsOpaque => Shape == MemberShape.Opaque;
}

/// <summary>
/// <b>ONE classifier for "how far does this member open", shared by every walk that asks.</b>
///
/// <para><b>WHY IT EXISTS (FI-88, 2026-09-03).</b> <c>SignalInventory.CollectLeaves</c> carried the
/// comment "same leaf recursion as <c>ProjectUsageGraph.CollectLeafPaths</c>", and
/// <c>InterfaceCheckRunner.Walk</c> was a third copy of the same decision with a fourth answer. Three
/// copies of one decision is how two tools come to disagree about one corpus: <c>interface-check</c>
/// resolved a UDT-typed member through the project's type definitions, <c>signal-set</c> did not, and
/// on a real program that difference reported a member referenced <b>251 times</b> as
/// <c>direction = unused, writers = [], readers = []</c> with <c>partial: false</c> and exit 0. Six of
/// eight function blocks were judged unharnessable on the strength of that output.</para>
///
/// <para><b>THE DECISION ORDER IS THE CONTRACT — first match wins.</b>
/// <list type="number">
/// <item>Inlined sub-members present → recurse. Unchanged and highest priority, so a corpus
/// round-tripped through TIA (which inlines everything) is classified exactly as it was before.</item>
/// <item>The unquoted datatype is a BLOCK NAME in this corpus → one leaf. A multi-instance is an
/// instance in its own right, not a struct field of its owner.</item>
/// <item>The unquoted datatype is an IEC instance type → one leaf. Its <c>.Q</c>/<c>.ET</c> are
/// written by the instruction, not by any caller, so "who drives this" is not a meaningful question
/// about them.</item>
/// <item>The ELEMENT type is elementary, or there is no type at all → one leaf.</item>
/// <item><c>Array[…] of</c> → ONE AGGREGATE LEAF. Deliberately not expanded: the element index would
/// be lost, and <c>ProjectUsageGraph.UsagesCovering</c> already records that "whether some particular
/// ELEMENT is unused is a different question with a different answer shape".</item>
/// <item>The element type resolves in the corpus and depth is under the cap → <b>recurse. THIS IS THE
/// FI-88 FIX.</b> It can only fire where branch 1 did not, i.e. where nothing was inlined, so on a
/// fully-inlined corpus the whole classifier is a provable no-op.</item>
/// <item>The declaration carries a <c>VERSION</c> and nothing above resolved it → one leaf. An
/// instruction or library instance, whose definition lives in TIA's libraries and can never be an
/// <c>.ir</c> file — so gating on it would be a gate NOBODY CAN EVER CLEAR.</item>
/// <item>Otherwise, or at the depth cap → one leaf AND an opaque report. <b>This gates.</b></item>
/// </list></para>
///
/// <para><b>Two branches — the system-identifier types on the elementary list, and the
/// <c>VERSION</c> branch — were added while proving the repair a no-op on <c>ir/test-project001</c>,
/// which declares <c>InterfaceId : HW_ANY</c> and <c>MbServer : MB_SERVER VERSION 5.3</c> with no
/// inlined body. Without them the gate fires on a healthy committed corpus.</b> Each carries its own
/// note at the point it is made.</para>
///
/// <para>🔴 <b>THE MULTI-INSTANCE DISCRIMINATOR AND THE TRAP IN IT.</b> <c>IO : "UDT_Valve"</c> and
/// <c>Valve : "FB_Valve"</c> are <b>both quoted</b>, so the datatype STRING cannot separate a UDT from
/// an FB instantiation. What separates them is which namespace the name resolves in, <b>block names
/// first</b> — verbatim the test <c>ProjectUsageGraph.ResolveMultiInstances</c> already uses, so the
/// inventory and the usage graph cannot disagree. Where a UDT and a block share a name the BLOCK wins,
/// because <c>ProjectUsageGraph</c> already decides it that way.</para>
///
/// <para>🔴 <b>AND THE BLOCK-NAME SET COMES FROM THE <c>BLOCK &lt;KIND&gt; &lt;Name&gt;</c> HEADER
/// LINE, NEVER FROM <c>TagTypeRegistry</c>.</b> That registry calls
/// <c>IrParser.ParseBlockWithoutSidecar</c> unconditionally and swallows the throw, so every
/// re-exported FB (any file carrying a <c>SIDECAR</c> section) is silently absent from its FB index.
/// Keying the discriminator there would classify a multi-instance of a round-tripped block as OPAQUE —
/// a false gate, for exactly the corpora this repair serves. See
/// <see cref="BlockNamesFromHeaders"/>.</para>
/// </summary>
public static class MemberExpansion
{
    /// <summary>
    /// Elementary types have no members to descend into, so a leaf of one of these is not "opaque".
    /// Anything NOT here and NOT resolvable and NOT carrying inlined members is reported, which is the
    /// fail-closed direction: a type this list forgets becomes a gate, never a silent pass.
    ///
    /// <para>The single home for this list. <c>InterfaceCheckRunner</c> forwards to it.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> Elementary = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Bool", "Byte", "Word", "DWord", "LWord", "SInt", "USInt", "Int", "UInt", "DInt", "UDInt",
        "LInt", "ULInt", "Real", "LReal", "Char", "WChar", "String", "WString", "Time", "LTime",
        "S5Time", "Date", "Time_Of_Day", "TOD", "LTOD", "LTime_Of_Day", "DT", "Date_And_Time", "DTL",
        "LDT", "Variant", "Any", "Pointer", "Void", "Timer", "Counter", "Block_FB", "Block_FC", "Block_DB",

        // S7 SYSTEM IDENTIFIER TYPES, added 2026-09-03 while proving FI-88's repair a no-op on
        // `ir/test-project001`. They are scalar aliases (a HW_ANY is a Word, a CONN_OUC is a Word) with
        // NO members to descend into, so they belong on this list on the merits — but they were missing
        // from it, and nothing noticed, because `interface-check`'s only corpus sweep silently SKIPS
        // any block it withdrew into NOT CHECKED, which is exactly what an opaque member causes. The
        // committed comms block declares `InterfaceId : HW_ANY` and `ID : CONN_OUC` today, so without
        // these two lines the signal-set gate fires on a healthy corpus — a false gate, and the one
        // failure mode that gets a gate switched off.
        "HW_ANY", "HW_DEVICE", "HW_DPMASTER", "HW_DPSLAVE", "HW_IO", "HW_IEPORT", "HW_HSC",
        "HW_PWM", "HW_PTO", "HW_INTERFACE", "HW_SUBMODULE", "AOM_IDENT", "EVENT_ANY", "EVENT_ATT",
        "EVENT_HWINT", "OB_ANY", "OB_ATT", "OB_CYCLIC", "OB_DELAY", "OB_TOD", "OB_HWINT", "OB_DIAG",
        "OB_TIMEERROR", "OB_STARTUP", "OB_PCYCLE", "CONN_ANY", "CONN_OUC", "CONN_PRG", "CONN_R_ID",
        "DB_ANY", "DB_DYN", "DB_WWW", "PORT", "RTM", "REMOTE", "ERROR_STRUCT", "NREF", "CREF",
    };

    /// <summary>
    /// IEC timer/counter instance types. The single home for this list; <c>ProjectUsageGraph</c>
    /// forwards to it.
    /// </summary>
    public static readonly IReadOnlySet<string> IecInstanceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "TON_TIME", "TOF_TIME", "TONR_TIME", "TP_TIME",
        "IEC_TIMER", "IEC_LTIMER",
        "CTU_INT", "CTD_INT", "CTUD_INT", "IEC_COUNTER", "IEC_UCOUNTER",
        "IEC_SCOUNTER", "IEC_DCOUNTER", "IEC_UDCOUNTER", "IEC_LCOUNTER",
    };

    /// <summary>
    /// Depth cap. A UDT that (illegally) references itself would otherwise recurse forever; reaching
    /// the cap is reported as OPAQUE rather than silently truncating the member set, because a
    /// truncated set that reads complete is the whole failure this classifier exists to stop.
    /// Mirrors <c>InterfaceCheckRunner.MaxDepth</c>.
    /// </summary>
    public const int MaxDepth = 12;

    /// <summary>
    /// Classify one member. <paramref name="depth"/> is the number of levels already descended;
    /// <paramref name="blockNames"/> is the corpus's block-name namespace (see
    /// <see cref="BlockNamesFromHeaders"/>); <paramref name="searchScope"/> is the human-readable
    /// description of WHERE types were looked for, which an opaque reason must name so that "one
    /// directory down" is distinguishable from "does not exist".
    /// </summary>
    public static MemberClassification Classify(
        DbMember member,
        TagTypeRegistry registry,
        IReadOnlySet<string> blockNames,
        int depth,
        string searchScope)
    {
        // 1. Inlined members. Highest priority, unchanged: this is the only branch a fully
        //    round-tripped corpus ever reaches on a structured member.
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            return new MemberClassification(MemberShape.Inlined, nested, ElementTypeOf(member.Datatype), null);
        }

        var raw = Unquote(member.Datatype);

        // 2. A block name — a multi-instance. Block names are consulted BEFORE types, matching
        //    ProjectUsageGraph.ResolveMultiInstances, so a name declared as both resolves the same
        //    way in both walks.
        if (raw.Length > 0 && blockNames.Contains(raw))
        {
            return new MemberClassification(MemberShape.MultiInstance, Array.Empty<DbMember>(), raw, null);
        }

        // 3. An IEC timer/counter instance.
        if (raw.Length > 0 && IecInstanceTypes.Contains(raw))
        {
            return new MemberClassification(MemberShape.IecInstance, Array.Empty<DbMember>(), raw, null);
        }

        var element = ElementTypeOf(member.Datatype);

        // 4. Elementary, or no stated type.
        if (element.Length == 0 || Elementary.Contains(element))
        {
            return new MemberClassification(MemberShape.Elementary, Array.Empty<DbMember>(), element, null);
        }

        // 5. An array. One leaf for the whole array — expanding it would lose the element index, and
        //    "is element N unused" is a different question this walk does not pretend to answer.
        if (IsArray(member.Datatype))
        {
            return new MemberClassification(MemberShape.Aggregate, Array.Empty<DbMember>(), element, null);
        }

        // 6. THE FI-88 FIX. A named type this corpus defines, opened through the registry.
        if (depth < MaxDepth && registry.TryGetUdt(element, out var udt))
        {
            return new MemberClassification(MemberShape.NamedTypeOpened, udt.Members, element, null);
        }

        // 6b. AN INSTRUCTION OR LIBRARY INSTANCE, RECOGNISED BY ITS `VERSION`. Added 2026-09-03 for
        //     the same reason as the system types above: the committed corpus declares
        //     `MbServer : MB_SERVER VERSION 5.3` with no inlined body, and MB_SERVER's definition
        //     lives in TIA's libraries — it will NEVER be an .ir file, so no amount of widening
        //     `--project` could resolve it and the gate could never be cleared. A gate nobody can
        //     clear is a gate that gets bypassed.
        //
        //     Deliberately AFTER branch 6, not before it: a versioned type that the corpus DOES
        //     define is still opened. And deliberately keyed on the VERSION token rather than on a
        //     name list, because the list of instruction types is TIA's and unbounded, while a
        //     version is what the IR itself states about the declaration. It reports as an
        //     instruction instance for the same reason branch 3 does: its members are instruction
        //     state written by the instruction, so "who drives this" is not a question about them.
        if (member.Version is { Length: > 0 })
        {
            return new MemberClassification(MemberShape.IecInstance, Array.Empty<DbMember>(), element, null);
        }

        // 7. Gate. Either the cap, or a type nothing in the search scope defines.
        var reason = depth >= MaxDepth
            ? $"nesting deeper than {MaxDepth} levels — refusing to descend further rather than truncate the "
              + "member set silently"
            : $"declared type '{element}' is not elementary, carries no inlined members, and is not defined by "
              + $"any PLC data type in {searchScope} — so its leaves could not be enumerated and are MISSING "
              + "from this set";

        return new MemberClassification(MemberShape.Opaque, Array.Empty<DbMember>(), element, reason);
    }

    /// <summary>
    /// How an opaque reason describes where types were looked for. <b>It names the root, the file
    /// count, and that the scan does not recurse</b> — without the last part, a type sitting one
    /// directory below the search root is indistinguishable from one that does not exist anywhere,
    /// and those two need different actions from whoever reads the gate.
    /// </summary>
    public static string DescribeSearchScope(string root, int fileCount) =>
        $"'{root}' ({fileCount} .ir file(s), scanned TOP-DIRECTORY-ONLY with no recursion into subdirectories)";

    /// <summary>
    /// Every block name in a corpus, read off each file's <c>BLOCK &lt;KIND&gt; &lt;Name&gt;</c> first
    /// line.
    ///
    /// <para>🔴 <b>DELIBERATELY A HEADER READ AND NOT A PARSE.</b> The alternative — asking
    /// <c>TagTypeRegistry</c> for its FB index — drops every block whose file carries a
    /// <c>SIDECAR</c> section, because that registry parses with
    /// <c>ParseBlockWithoutSidecar</c> and swallows the resulting throw. A round-tripped FB is exactly
    /// the file that has a sidecar, so the FB index is missing precisely the blocks a re-exported
    /// corpus contains, and a multi-instance of one would be classified OPAQUE — a false gate. The
    /// header line is also the cheap read <c>InterfaceCheckTests</c> already uses for the same
    /// purpose.</para>
    /// </summary>
    public static HashSet<string> BlockNamesFromHeaders(IEnumerable<string> paths)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            string? first;
            try
            {
                first = File.ReadLines(path).FirstOrDefault();
            }
            catch (IOException)
            {
                continue;
            }

            if (first is null || !first.StartsWith("BLOCK ", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                names.Add(parts[2]);
            }
        }

        return names;
    }

    /// <summary>
    /// <c>Array[0..3] of "UDT_X"</c> → <c>UDT_X</c>; <c>"UDT_X"</c> → <c>UDT_X</c>;
    /// <c>TON_TIME VERSION 1.0</c> → <c>TON_TIME</c>. The single home for this;
    /// <c>InterfaceCheckRunner</c> forwards to it.
    /// </summary>
    public static string ElementTypeOf(string? datatype)
    {
        var text = (datatype ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var of = text.LastIndexOf(" of ", StringComparison.OrdinalIgnoreCase);
        if (of >= 0)
        {
            text = text[(of + 4)..].Trim();
        }

        // A `TON_TIME VERSION 1.0` style suffix is not part of the type name.
        var version = text.IndexOf(" VERSION ", StringComparison.OrdinalIgnoreCase);
        if (version >= 0)
        {
            text = text[..version].Trim();
        }

        return text.Trim('"');
    }

    private static bool IsArray(string? datatype) =>
        (datatype ?? string.Empty).TrimStart().StartsWith("Array", StringComparison.OrdinalIgnoreCase);

    private static string Unquote(string? s) => (s ?? string.Empty).Trim().Trim('"');
}
