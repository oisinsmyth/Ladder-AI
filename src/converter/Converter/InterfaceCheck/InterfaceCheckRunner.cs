using Converter.Ir;
using Converter.IrHash;
using Converter.SimaticMl;

namespace Converter.InterfaceCheck;

/// <summary>
/// <b>NB-30.</b> Does this block's interface carry the response signals the specification names?
///
/// <para>*** YOU DO NOT NEED TO DRIVE A BLOCK TO DISCOVER IT LACKS AN OUTPUT THE SPEC NAMES. *** The
/// whole check is a set difference between two small name sets, and its absence is what let a
/// mis-bound response signal ride inside a relational conformance assertion — <i>a relational
/// assertion is structurally blind to any error its operands share, and a misbound operand is exactly
/// such an error.</i></para>
///
/// <para>*** THE DESIGN TRAP, RECORDED BEFORE THE BUILD AND SURVIVING INTO IT. *** On this corpus's
/// house style (C-132) an FB's <c>INPUT</c> and <c>OUTPUT</c> sections are <b>both empty</b> and the
/// entire caller interface is one STATIC member of a UDT. Two wrong implementations were predicted:
/// <list type="number">
/// <item>One reading INPUT/OUTPUT only would report <b>BOTH</b> signals missing — including the one
/// that exists — and a check that accuses correct work of the worst thing on the list is one that gets
/// disbelieved.</item>
/// <item>One matching COMMENTS would have found the word "inhibit" in a block comment and
/// <b>PASSED</b> the very defect this exists to catch.</item>
/// </list>
/// So: resolve through the interface UDT, and match <b>MEMBER NAMES ONLY</b> — never a comment, never
/// a network title, never a block comment. Both wrong forms are pinned by tests.</para>
///
/// <para><b>WHERE IT REFUSES TO JUDGE.</b> A MISSING verdict is a positive claim that the block does
/// not carry a name, and it is only sound over a COMPLETE member set. If a member's type could not be
/// opened — a named type not in the project, with no inlined members — the set is partial, and any
/// MISSING is withdrawn into NOT CHECKED naming the opaque member. An over-cautious NOT CHECKED costs
/// a re-run; a false FAIL against a block costs the check its credibility.</para>
/// </summary>
public static class InterfaceCheckRunner
{
    // Elementary types have no members to descend into, so a leaf of one of these is not "opaque".
    // Anything NOT here and NOT resolvable and NOT carrying inlined members is reported, which is the
    // fail-closed direction: a type this list forgets becomes a NOT CHECKED, never a silent pass.
    private static readonly HashSet<string> Elementary = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bool", "Byte", "Word", "DWord", "LWord", "SInt", "USInt", "Int", "UInt", "DInt", "UDInt",
        "LInt", "ULInt", "Real", "LReal", "Char", "WChar", "String", "WString", "Time", "LTime",
        "S5Time", "Date", "Time_Of_Day", "TOD", "LTOD", "LTime_Of_Day", "DT", "Date_And_Time", "DTL",
        "LDT", "Variant", "Any", "Pointer", "Void", "Timer", "Counter", "Block_FB", "Block_FC", "Block_DB",
    };

    /// <summary>Run the check.</summary>
    /// <param name="requirementsSource">Where the required names came from, for the report header.</param>
    public static InterfaceCheckReport Run(
        string projectDir,
        string blockName,
        IReadOnlyList<string> requiredNames,
        string requirementsSource)
    {
        var files = Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var corpus = $"{files.Count} .ir file(s) in {projectDir}";

        // FI-44. No required names is NOT a clean run: a set difference against an empty required set
        // is empty for every block, including one with no interface at all.
        if (requiredNames.Count == 0)
        {
            return InterfaceCheckReport.NotChecked(blockName,
                "no required names were supplied, so the set difference was taken against nothing. Empty is not "
                + "clean: an empty required set produces PRESENT-for-everything on every block in the corpus, "
                + "including one with no interface at all.", corpus, requirementsSource);
        }

        var matches = new List<(string File, IrBlock Block)>();
        var unparseable = new List<string>();
        foreach (var file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                unparseable.Add(Path.GetFileName(file));
                continue;
            }

            if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
            {
                continue;
            }

            IrBlock block;
            try
            {
                block = IrParser.HasSidecarSection(text)
                    ? IrParser.ParseBlock(text).Block
                    : IrParser.ParseBlockWithoutSidecar(text);
            }
            catch (IrFormatException)
            {
                unparseable.Add(Path.GetFileName(file));
                continue;
            }

            if (string.Equals(block.Name, blockName, StringComparison.Ordinal))
            {
                matches.Add((file, block));
            }
        }

        if (matches.Count == 0)
        {
            return InterfaceCheckReport.NotChecked(blockName,
                $"no block named '{blockName}' in the corpus ({corpus}"
                + (unparseable.Count == 0 ? "" : $"; {unparseable.Count} file(s) did not parse: {string.Join(", ", unparseable)}")
                + "). A block that is not here has no interface to compare, and reporting every required name MISSING "
                + "would be a FAIL against a block this run never saw.", corpus, requirementsSource);
        }

        if (matches.Count > 1)
        {
            return InterfaceCheckReport.NotChecked(blockName,
                $"{matches.Count} files declare a block named '{blockName}' ({string.Join(", ", matches.Select(m => Path.GetFileName(m.File)))}). "
                + "Which one the deployed program carries is not decidable from here, and checking the wrong one would "
                + "stamp the result with an ir-hash that names a block nobody deployed.", corpus, requirementsSource);
        }

        var (blockFile, target) = matches[0];
        var registry = TagTypeRegistry.FromFiles(files);

        var examined = new List<string>();
        var excludedTemps = new List<string>();
        var opaque = new List<OpaqueMember>();
        var sectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        // *** SECTIONS WALKED, AND THE ONE DELIBERATELY LEFT OUT. *** INPUT/OUTPUT/INOUT/STATIC/CONSTANT
        // are all externally reachable, so a response signal may legitimately live in any of them —
        // which is the whole point on a corpus where INPUT and OUTPUT are empty by house style. TEMP is
        // NOT walked: a temp does not survive the scan and cannot be observed from outside the block, so
        // calling one PRESENT would answer a different question from the one asked. It is excluded BY
        // NAME and COUNTED ON EVERY RUN — a silent narrowing has no cost to grow.
        foreach (var (section, members) in new (string, IReadOnlyList<DbMember>)[]
                 {
                     ("INPUT", target.InputMembers ?? Array.Empty<DbMember>()),
                     ("OUTPUT", target.OutputMembers ?? Array.Empty<DbMember>()),
                     ("INOUT", target.InOutMembers),
                     ("STATIC", target.StaticMembers ?? Array.Empty<DbMember>()),
                     ("CONSTANT", target.ConstantMembers ?? Array.Empty<DbMember>()),
                 })
        {
            var before = examined.Count;
            foreach (var member in members)
            {
                Walk(member, section, registry, examined, opaque, depth: 0);
            }

            sectionCounts[section] = examined.Count - before;
        }

        foreach (var temp in target.TempMembers)
        {
            excludedTemps.Add($"TEMP/{temp.Name}");
        }

        // FI-44 again, and the form that would actually happen: a block whose interface is genuinely
        // empty. Every required name would read MISSING and the report would look like a devastating
        // finding about the block, when what happened is that nothing was examined.
        if (examined.Count == 0)
        {
            return InterfaceCheckReport.NotChecked(blockName,
                $"'{blockName}' has no members in INPUT/OUTPUT/INOUT/STATIC/CONSTANT"
                + (excludedTemps.Count == 0 ? "" : $" ({excludedTemps.Count} TEMP member(s) excluded — a temp is not observable from outside the block)")
                + ". Every required name would read MISSING, which is what a check that examined nothing looks like "
                + "when it is mistaken for a finding.", corpus, requirementsSource);
        }

        var byName = examined
            .GroupBy(NameOf, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var requirements = new List<InterfaceRequirement>();
        foreach (var required in requiredNames)
        {
            requirements.Add(Judge(required, byName, excludedTemps));
        }

        // The completeness precondition on a MISSING verdict. PRESENT survives a partial walk — finding
        // a name does not depend on having seen everything — so only the negative half is withdrawn.
        if (opaque.Count > 0 && requirements.Any(r => r.Status == RequirementStatus.Missing))
        {
            return InterfaceCheckReport.NotChecked(blockName,
                $"{requirements.Count(r => r.Status == RequirementStatus.Missing)} required name(s) did not resolve, but "
                + $"{opaque.Count} interface member(s) could not be opened: "
                + string.Join("; ", opaque.Select(o => $"{o.Member} : {o.Datatype} ({o.Reason})"))
                + ". A MISSING verdict is the positive claim that the block does not carry a name, and that is only "
                + "sound over a COMPLETE member set. Supply the missing type in --project and re-run.", corpus, requirementsSource);
        }

        return new InterfaceCheckReport(
            blockName,
            blockFile,
            IrHashRunner.HashBlock(target),
            Checked: true,
            NotCheckedReason: string.Empty,
            requirements,
            examined,
            excludedTemps,
            opaque,
            sectionCounts,
            requirementsSource,
            corpus);
    }

    private static InterfaceRequirement Judge(
        string required,
        IReadOnlyDictionary<string, string[]> byName,
        IReadOnlyList<string> excludedTemps)
    {
        var name = (required ?? string.Empty).Trim();

        if (name.Length == 0 || name.Contains('.', StringComparison.Ordinal))
        {
            return new InterfaceRequirement(required ?? string.Empty, RequirementStatus.NotAMemberName, string.Empty,
                name.Length == 0
                    ? "empty required name."
                    : $"'{name}' looks like a PATH. The ruling is member names only: the same member is reachable by "
                      + "different paths from different callers, so a path would make the answer depend on who is asking. "
                      + $"Give the leaf name ('{name[(name.LastIndexOf('.') + 1)..]}').");
        }

        if (byName.TryGetValue(name, out var paths))
        {
            var exact = paths.FirstOrDefault(p => string.Equals(NameOf(p), name, StringComparison.Ordinal));
            var found = exact ?? paths[0];

            // A case-only difference is the SAME member to TIA, so this is PRESENT — but the spelling is
            // reported, because two artifacts spelling one member differently is how a name drifts.
            var detail = exact is null
                ? $"present, but the block spells it '{NameOf(found)}'. TIA identifiers are case-insensitive so this IS "
                  + "the same member; the spellings differ between the specification and the block."
                : "present in the block's interface.";

            return new InterfaceRequirement(name, RequirementStatus.Present, found, detail);
        }

        var temp = excludedTemps.FirstOrDefault(t =>
            string.Equals(NameOf(t), name, StringComparison.OrdinalIgnoreCase));

        return new InterfaceRequirement(name, RequirementStatus.Missing, string.Empty,
            temp is null
                ? $"the block's interface carries no member named '{name}'. *** THIS IS A FAIL AGAINST THE BLOCK, not "
                  + "against whoever asked for it — the specification names this response signal and the block does not "
                  + "provide it. Do NOT rename it in the block to close this: that closes the finding and destroys it as "
                  + "evidence."
                : $"'{name}' exists only as {temp}, which is EXCLUDED — a temp does not survive the scan and cannot be "
                  + "observed from outside the block, so it cannot carry a response signal.");
    }

    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    // Depth cap: a UDT that (illegally) references itself would otherwise recurse forever. Reached only
    // by malformed input, and it is reported as an opaque member rather than silently truncating the
    // member set — a truncated set is exactly what makes a MISSING verdict unsound.
    private const int MaxDepth = 12;

    private static void Walk(
        DbMember member,
        string path,
        TagTypeRegistry registry,
        List<string> examined,
        List<OpaqueMember> opaque,
        int depth)
    {
        var here = $"{path}/{member.Name}";
        examined.Add(here);

        if (depth >= MaxDepth)
        {
            opaque.Add(new OpaqueMember(here, member.Datatype ?? string.Empty,
                $"nesting deeper than {MaxDepth} — refusing to descend further rather than truncate the member set silently"));
            return;
        }

        if (member.NestedMembers is { Count: > 0 })
        {
            foreach (var nested in member.NestedMembers)
            {
                Walk(nested, here, registry, examined, opaque, depth + 1);
            }

            return;
        }

        var type = ElementTypeOf(member.Datatype);
        if (type.Length == 0 || Elementary.Contains(type))
        {
            return;
        }

        if (registry.TryGetUdt(type, out var udt))
        {
            foreach (var nested in udt.Members)
            {
                Walk(nested, here, registry, examined, opaque, depth + 1);
            }

            return;
        }

        // A named type with neither inlined members nor a definition in the corpus. Reported, so that
        // a MISSING verdict computed over this partial set is withdrawn rather than believed.
        opaque.Add(new OpaqueMember(here, member.Datatype ?? string.Empty,
            $"declared type '{type}' is not an elementary type, carries no inlined members, and is not a PLC data type "
            + "in --project — so its members could not be enumerated"));
    }

    // `Array[0..3] of "UDT_X"` -> `UDT_X`; `"UDT_X"` -> `UDT_X`. Quotes are stripped by TryGetUdt too,
    // but the elementary-type comparison happens here so it must see the bare name.
    private static string ElementTypeOf(string? datatype)
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
}
