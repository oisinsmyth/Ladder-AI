using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Review;

// One method per rule, following this codebase's own established "hand-rolled per-kind" style
// (Ir/TagReferences.cs's own doc comment; confirmed consistent across IrParser/IrSerializer/
// GraphReducer, and an explicit 2026-07-14 audit already deferred generalizing this shape) —
// no shared rule-check abstraction, each function is independent and reads its own rule text
// directly from docs/06-lad-conventions.md.
public static class Rules
{
    // C-003 (warn) — Prefixes: FB_/FC_/DB_/UDT_; instance DBs named iDB_<FBName>_<Instance>.
    public static IEnumerable<Finding> CheckC003BlockPrefix(IrBlock block)
    {
        var requiredPrefix = block.Kind switch
        {
            "FB" => "FB_",
            "FC" => "FC_",
            _ => null, // OB and anything else: C-003 names no required prefix for these.
        };

        if (requiredPrefix is not null && !block.Name.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            yield return new Finding(
                "C-003",
                FindingSeverity.Warn,
                block.Name,
                null,
                $"Block name '{block.Name}' does not start with the required '{requiredPrefix}' prefix for a {block.Kind}.",
                $"Rename to '{requiredPrefix}{block.Name}' (coordinate with any callers/references first).");
        }
    }

    public static IEnumerable<Finding> CheckC003DbPrefix(DbSource db)
    {
        if (db.InstanceOfName is not null)
        {
            if (!db.Name.StartsWith("iDB_", StringComparison.Ordinal))
            {
                yield return new Finding(
                    "C-003",
                    FindingSeverity.Warn,
                    db.Name,
                    null,
                    $"Instance DB name '{db.Name}' (of '{db.InstanceOfName}') does not follow the required 'iDB_<FBName>_<Instance>' pattern.",
                    $"Rename to a form like 'iDB_{db.InstanceOfName}_<instance>' (coordinate with any callers first).");
            }
        }
        else if (!db.Name.StartsWith("DB_", StringComparison.Ordinal))
        {
            yield return new Finding(
                "C-003",
                FindingSeverity.Warn,
                db.Name,
                null,
                $"DB name '{db.Name}' does not start with the required 'DB_' prefix.",
                $"Rename to 'DB_{db.Name}' (coordinate with any callers/references first).");
        }
    }

    // C-003 (warn) — the `UDT_` half of the same prefix rule. Named in C-003's own sentence
    // alongside FB_/FC_/DB_, and checkable against a TYPE file's name with no more machinery than
    // the DB form above needs.
    public static IEnumerable<Finding> CheckC003TypePrefix(PlcTypeSource type)
    {
        if (!type.Name.StartsWith("UDT_", StringComparison.Ordinal))
        {
            yield return new Finding(
                "C-003",
                FindingSeverity.Warn,
                type.Name,
                null,
                $"PLC data type name '{type.Name}' does not start with the required 'UDT_' prefix.",
                $"Rename to 'UDT_{type.Name}' (coordinate with every block whose interface members are typed as it first).");
        }
    }

    // C-005 (error) — names use letters, digits, and underscore only, starting with a letter.
    // Runs per dot-separated path component (array-index suffixes stripped, %Xn/%Bn/%Wn
    // slice-address components skipped entirely — that's addressing syntax, not a user-chosen
    // name) — never against a whole dotted path or an Expr.Literal value (TagReferences.AllTagPaths
    // already excludes literals).
    public static IEnumerable<Finding> CheckC005Charset(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            foreach (var path in TagReferences.AllTagPaths(network))
            {
                foreach (var finding in CheckPathCharset(path, block.Name, network.Number))
                {
                    yield return finding;
                }
            }
        }
    }

    // C-001 (error) — member/variable names are short PascalCase, underscore-free. Physical-IO tags
    // keep their underscores by design, but those live in tag tables (exempt here), not in DB/UDT/
    // block members. Prefixes (FB_/DB_/…) are C-003's job; this checks member/variable names only.
    // Recurses nested struct members, same shape as CheckC005CharsetDbMembers.
    public static IEnumerable<Finding> CheckC001MemberNames(string ownerName, IReadOnlyList<DbMember> members)
    {
        foreach (var member in members)
        {
            // TIA-informative system parameters — e.g. an OB's Initial_Call/Remanence startup-info
            // inputs (BAREPARAM INFORMATIVE) — are TIA-provided, not author-named and not renameable,
            // so C-001 doesn't apply to them.
            if (member.Informative)
            {
                continue;
            }

            if (!IsPascalCaseMember(member.Name))
            {
                yield return new Finding(
                    "C-001",
                    FindingSeverity.Error,
                    ownerName,
                    null,
                    $"Member/variable '{member.Name}' is not PascalCase (C-001: short PascalCase, underscore-free; physical-IO tags keep underscores, DB/UDT/block members do not).",
                    $"Rename '{member.Name}' to PascalCase without underscores (e.g. Cycle_Start -> CycleStart).");
            }

            if (member.NestedMembers is { Count: > 0 })
            {
                foreach (var finding in CheckC001MemberNames(ownerName, member.NestedMembers))
                {
                    yield return finding;
                }
            }
        }
    }

    // PascalCase member name: non-empty, starts with an ASCII uppercase letter, otherwise letters
    // and digits only (no underscore or punctuation). All-caps system leaves like a timer's PT/ET/Q
    // pass. Char-based to match this file's IsValidIdentifier style (no Regex).
    private static bool IsPascalCaseMember(string name) =>
        name.Length > 0 && char.IsAsciiLetterUpper(name[0]) && name.All(char.IsAsciiLetterOrDigit);

    // C-001 (error) — TAG-TABLE layer, the one place tag NAMES actually live. Two sub-layers, and
    // every tag lands in exactly one of them (no tag is silently unexamined):
    //
    //   (a) PHYSICAL-IO tags — any tag whose LogicalAddress is in the process image (`%I…`/`%Q…`).
    //       C-001's format is `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>` (e.g. `DI3_FCC_RunFb`). The
    //       equipment token sits in the MIDDLE, so the check is a field split, never a prefix match.
    //       Two further cross-checks are possible here that a name alone could not give, because the
    //       ADDRESS is right there next to the name: the direction letter must agree with I-vs-Q,
    //       and the D/A letter must agree with bit-vs-word width. A `DQ…` tag at an `%I` address is
    //       not a style question — one of the two is wrong.
    //   (b) EVERY OTHER tag (`%M` flags, and anything not in the process image) — C-001's
    //       variables layer: short PascalCase, underscore-free.
    //
    // C-007's vendor-default exception is applied HERE rather than left implicit, and is REPORTED as
    // an Info finding rather than silently skipped: `Clock_0.5Hz` is tolerated, but a reader is told
    // it was tolerated and why. Physical-IO tags keep their underscores by design — that is layer
    // (a)'s own format, not an exemption from checking (which is what the pre-2026-08-13 runner
    // asserted: "no member-naming check applies", for the file kind where the rule most applies).
    public static IEnumerable<Finding> CheckC001TagNames(PlcTagTableSource table)
    {
        foreach (var tag in table.Tags)
        {
            foreach (var finding in CheckC001TagName(table.Name, tag))
            {
                yield return finding;
            }
        }
    }

    // Per-tag half of CheckC001TagNames, split out 2026-08-13 so a caller can attribute a finding to
    // the TAG it is about. The table-level method above is the same loop and is unchanged in
    // behaviour — this is a granularity split, not a rule change. It exists because the harness-scope
    // classifier (HarnessScope) is per-TAG, not per-table: a tag table has no number, so the table's
    // NAME is the only table-level property available and a name is exactly what must not be
    // load-bearing here. Deciding per tag is what makes "rename the table" worth nothing.
    public static IEnumerable<Finding> CheckC001TagName(string tableName, PlcTagSource tag)
    {
        var address = ProcessImageAddress.Classify(tag.LogicalAddress);

        if (address is null)
        {
            // Layer (b): a flag/memory tag is a variable — short PascalCase, underscore-free.
            if (IsPascalCaseMember(tag.Name))
            {
                yield break;
            }

            if (IsVendorDefaultTagName(tag.Name))
            {
                yield return new Finding(
                    "C-001",
                    FindingSeverity.Info,
                    tableName,
                    null,
                    $"Tag '{tag.Name}' ({tag.LogicalAddress}) is not PascalCase, but is a TIA vendor-default name — tolerated as-is under C-007, not a defect.",
                    "No action. C-007 makes vendor-supplied names (the clock/system memory bits) a documented standing exception; renaming one buys nothing and risks confusing it with project content.");
                yield break;
            }

            yield return new Finding(
                "C-001",
                FindingSeverity.Error,
                tableName,
                null,
                $"Tag '{tag.Name}' ({tag.LogicalAddress}) is not at a physical-IO address, so C-001's variables layer applies: short PascalCase, underscore-free. It is neither.",
                $"Rename '{tag.Name}' to short PascalCase without underscores, or — if this really is a physical-IO point — give it a physical-IO address and the `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>` name.");
            yield break;
        }

        // Layer (a): physical IO. Shape first — a name that isn't in the format at all can't be
        // cross-checked against the address, so that is the only finding for this tag.
        var shape = PhysicalIoTagName.Parse(tag.Name);
        if (shape is null)
        {
            yield return new Finding(
                "C-001",
                FindingSeverity.Error,
                tableName,
                null,
                $"Tag '{tag.Name}' is at physical-IO address {tag.LogicalAddress} but does not follow C-001's physical-IO format `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>` (e.g. DI3_FCC_RunFb).",
                $"Rename to `{(address.IsInput ? (address.IsBit is false ? "AI" : "DI") : (address.IsBit is false ? "AQ" : "DQ"))}<n>_<Equipment>_<Signal>`, using the frozen C-004 equipment identifier verbatim.");
            yield break;
        }

        if (shape.IsInput != address.IsInput)
        {
            yield return new Finding(
                "C-001",
                FindingSeverity.Error,
                tableName,
                null,
                $"Tag '{tag.Name}' names itself an {(shape.IsInput ? "input" : "output")} ('{shape.Prefix}') but is addressed at {tag.LogicalAddress}, which is a physical {(address.IsInput ? "input" : "output")} — the name and the address disagree.",
                $"Fix whichever is wrong: rename the tag to the '{(address.IsInput ? (shape.IsDigital ? "DI" : "AI") : (shape.IsDigital ? "DQ" : "AQ"))}' form, or re-address it.");
        }

        if (address.IsBit is bool isBit && shape.IsDigital != isBit)
        {
            yield return new Finding(
                "C-001",
                FindingSeverity.Error,
                tableName,
                null,
                $"Tag '{tag.Name}' names itself {(shape.IsDigital ? "digital" : "analog")} ('{shape.Prefix}') but is addressed at {tag.LogicalAddress}, a {(isBit ? "single bit" : "byte/word/dword")} — a digital point is a bit, an analog point is not.",
                $"Fix whichever is wrong: rename to the '{(shape.IsInput ? (isBit ? "DI" : "AI") : (isBit ? "DQ" : "AQ"))}' form, or re-address it.");
        }
    }

    // C-005 (error) — TAG-TABLE layer: the table's own name and every tag name. Same charset rule as
    // everywhere else (letters/digits/underscore, starting with a letter); the LogicalAddress is
    // deliberately NOT checked (`%I0.0`'s dot is addressing syntax, not a chosen name — the same
    // exclusion CheckPathCharset already makes for `%Xn` slice components). C-007's vendor-default
    // exception is REPORTED as Info, never silently applied: `Default tag table` (a space) and
    // `Clock_0.5Hz` (a dot) are the two named in doc 06 and both are real in the corpus.
    public static IEnumerable<Finding> CheckC005TagTableCharset(PlcTagTableSource table)
    {
        if (!IsValidIdentifier(table.Name))
        {
            yield return IsVendorDefaultTableName(table.Name)
                ? new Finding(
                    "C-005",
                    FindingSeverity.Info,
                    table.Name,
                    null,
                    $"Tag table name '{table.Name}' contains characters outside letters/digits/underscore, but is a TIA vendor-supplied name — tolerated as-is under C-007, not a defect.",
                    "No action. C-007 names this exact case as a documented standing exception to C-005.")
                : new Finding(
                    "C-005",
                    FindingSeverity.Error,
                    table.Name,
                    null,
                    $"Tag table name '{table.Name}' contains characters other than letters/digits/underscore, or doesn't start with a letter.",
                    $"Rename the tag table '{table.Name}' to use only letters, digits, and underscore, starting with a letter.");
        }

        foreach (var tag in table.Tags)
        {
            if (IsValidIdentifier(tag.Name))
            {
                continue;
            }

            yield return IsVendorDefaultTagName(tag.Name)
                ? new Finding(
                    "C-005",
                    FindingSeverity.Info,
                    table.Name,
                    null,
                    $"Tag '{tag.Name}' contains characters outside letters/digits/underscore, but is a TIA vendor-default name — tolerated as-is under C-007, not a defect.",
                    "No action. C-007 makes the vendor-supplied clock/system memory bits a documented standing exception to C-005.")
                : new Finding(
                    "C-005",
                    FindingSeverity.Error,
                    table.Name,
                    null,
                    $"Tag name '{tag.Name}' contains characters other than letters/digits/underscore, or doesn't start with a letter.",
                    $"Rename '{tag.Name}' to use only letters, digits, and underscore, starting with a letter.");
        }
    }

    // C-406 (error) — TAG-TABLE layer: the declaration form, against a tag's own DataTypeName. A PLC
    // tag is expected to be an elementary scalar, so this may well never fire — but DataTypeName is a
    // free string in the IR model, so a TOF_TIME/TONR_TIME here is REPRESENTABLE and therefore worth
    // actually looking for. Reported as Checked rather than CheckedVacuous for exactly that reason:
    // "cannot appear" is a claim this codebase can't make about a free-text field.
    public static IEnumerable<Finding> CheckC406TagDataTypes(PlcTagTableSource table)
    {
        foreach (var tag in table.Tags.Where(t => t.DataTypeName is "TONR_TIME" or "TOF_TIME"))
        {
            yield return new Finding(
                "C-406",
                FindingSeverity.Error,
                table.Name,
                null,
                $"Tag '{tag.Name}' is declared as {tag.DataTypeName} - only TON_TIME is permitted.",
                "Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.");
        }
    }

    // The TIA vendor-supplied tag names C-007 tolerates: the clock memory byte's bits (`Clock_0.5Hz`
    // … `Clock_10Hz` — the doc's own example) and the system memory byte's bits. Deliberately a
    // narrow, enumerated set rather than a loose pattern: a project tag that happens to look like one
    // must NOT slip through the exception, and TIA's auto-generated `Tag_1` placeholder is
    // specifically NOT here — an unnamed tag is exactly what a naming review should catch.
    private static readonly string[] VendorDefaultSystemTagNames =
        { "FirstScan", "DiagStatusUpdate", "AlwaysTRUE", "AlwaysFALSE" };

    private static bool IsVendorDefaultTagName(string name)
    {
        if (VendorDefaultSystemTagNames.Contains(name, StringComparer.Ordinal))
        {
            return true;
        }

        if (!name.StartsWith("Clock_", StringComparison.Ordinal) || !name.EndsWith("Hz", StringComparison.Ordinal))
        {
            return false;
        }

        var frequency = name["Clock_".Length..^"Hz".Length];
        return frequency.Length > 0 && frequency.All(c => char.IsAsciiDigit(c) || c == '.');
    }

    // `Default tag table` — named verbatim in C-007 as a tolerated vendor name.
    private static bool IsVendorDefaultTableName(string name) =>
        string.Equals(name, "Default tag table", StringComparison.Ordinal);

    // A physical-IO address in the process image, decomposed far enough to cross-check a C-001 name
    // against it. IsBit is NULLABLE on purpose: `%I0.0` is provably a bit and `%IW64` provably isn't,
    // but a bare `%I5` is neither, and guessing there would manufacture a false finding — a width
    // that cannot be established simply isn't cross-checked (the direction still is).
    private sealed record ProcessImageAddress(bool IsInput, bool? IsBit)
    {
        public static ProcessImageAddress? Classify(string logicalAddress)
        {
            if (logicalAddress.Length < 2 || logicalAddress[0] != '%')
            {
                return null;
            }

            var area = char.ToUpperInvariant(logicalAddress[1]);
            if (area != 'I' && area != 'Q')
            {
                return null;
            }

            var rest = logicalAddress[2..];
            if (rest.Length > 0 && char.IsAsciiLetter(rest[0]))
            {
                // %IB / %IW / %ID (and any other size letter): a byte/word/dword, never a bit.
                return new ProcessImageAddress(area == 'I', false);
            }

            return new ProcessImageAddress(area == 'I', rest.Contains('.') ? true : null);
        }
    }

    // A C-001 physical-IO tag name, split into its `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>` fields.
    // Hand-rolled rather than a Regex, matching this file's IsValidIdentifier/IsPascalCaseMember style.
    private sealed record PhysicalIoTagName(string Prefix, bool IsInput, bool IsDigital)
    {
        public static PhysicalIoTagName? Parse(string name)
        {
            if (name.Length < 2)
            {
                return null;
            }

            var prefix = name[..2];
            var isDigital = prefix is "DI" or "DQ";
            var isAnalog = prefix is "AI" or "AQ";
            if (!isDigital && !isAnalog)
            {
                return null;
            }

            var i = 2;
            while (i < name.Length && char.IsAsciiDigit(name[i]))
            {
                i++;
            }

            if (i == 2 || i >= name.Length || name[i] != '_')
            {
                return null; // no point number, or no `_` separating it from the equipment token
            }

            // Equipment and Signal: at least two non-empty underscore-separated fields after the
            // number. The equipment token is the FIRST of them — it sits in the middle of the name,
            // which is why this is a field split and not a prefix test.
            var fields = name[(i + 1)..].Split('_');
            if (fields.Length < 2 || fields.Any(f => f.Length == 0))
            {
                return null;
            }

            return new PhysicalIoTagName(prefix, prefix[1] == 'I', isDigital);
        }
    }

    public static IEnumerable<Finding> CheckC005CharsetDbMembers(string ownerName, IReadOnlyList<DbMember> members)
    {
        foreach (var member in members)
        {
            if (!IsValidIdentifier(member.Name))
            {
                yield return new Finding(
                    "C-005",
                    FindingSeverity.Error,
                    ownerName,
                    null,
                    $"Member name '{member.Name}' contains characters other than letters/digits/underscore, or doesn't start with a letter.",
                    $"Rename '{member.Name}' to use only letters, digits, and underscore, starting with a letter.");
            }

            if (member.NestedMembers is { Count: > 0 })
            {
                foreach (var finding in CheckC005CharsetDbMembers(ownerName, member.NestedMembers))
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<Finding> CheckPathCharset(string path, string blockName, int networkNumber)
    {
        foreach (var rawComponent in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawComponent.StartsWith('%'))
            {
                // %Xn/%Bn/%Wn slice-address syntax, not a user-chosen name — not C-005's concern.
                continue;
            }

            var bracketIndex = rawComponent.IndexOf('[');
            var name = bracketIndex >= 0 ? rawComponent[..bracketIndex] : rawComponent;

            if (!IsValidIdentifier(name))
            {
                yield return new Finding(
                    "C-005",
                    FindingSeverity.Error,
                    blockName,
                    networkNumber,
                    $"Name component '{name}' (from '{path}') contains characters other than letters/digits/underscore, or doesn't start with a letter.",
                    $"Rename '{name}' to use only letters, digits, and underscore, starting with a letter.");
            }
        }
    }

    private static bool IsValidIdentifier(string component)
    {
        if (component.Length == 0 || !char.IsAsciiLetter(component[0]))
        {
            return false;
        }

        foreach (var c in component)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    // C-201 (error) — every network has a title; every block has a header comment. Two separate
    // sub-checks, deliberately: the header-comment concept applies to any content kind that has
    // its own Comment field (BLOCK and DB both do); the network-title concept only exists for
    // BLOCK-kind files, since DB/TYPE/TAGTABLE files have no networks at all.
    public static IEnumerable<Finding> CheckC201HeaderComment(string ownerName, string? comment)
    {
        if (string.IsNullOrEmpty(comment))
        {
            yield return new Finding(
                "C-201",
                FindingSeverity.Error,
                ownerName,
                null,
                $"'{ownerName}' has no header comment.",
                "Add a COMMENT stating the block's purpose (and, per C-201, author/revision — not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).");
        }
    }

    public static IEnumerable<Finding> CheckC201NetworkTitles(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            if (network.IsEmpty)
            {
                // A network with zero Parts has nothing to title - not this rule's concern
                // (matches the corpus's own trailing-[empty]-network pattern, e.g. TimerSample
                // Network 4).
                continue;
            }

            if (string.IsNullOrEmpty(network.Title))
            {
                yield return new Finding(
                    "C-201",
                    FindingSeverity.Error,
                    block.Name,
                    network.Number,
                    $"Network {network.Number} has real logic but no title.",
                    "Add a NETWORK title describing what this network does.");
            }
        }
    }

    // C-301 (error) — no absolute addressing; symbolic access only, except: alarm words (per
    // C-501's own two-part condition), comms mapping, and documented data-handling blocks (C-105).
    // Phase-1 scope, stated honestly: this checks slice-access usage (.%Xn/.%Bn/.%Wn) against the
    // three documented exceptions. It does NOT independently detect raw %M/%DBx.DBWy absolute
    // addressing — whether that shape is even representable in the current IR model is unconfirmed
    // (no grounding comment/test/fixture anywhere shows this codebase has seen one), so claiming to
    // catch it would overstate what's actually verified.
    //
    // The comms-mapping and data-handling exceptions share one mechanism here (deliberately loose,
    // Phase-1-appropriate): a block "self-identifies" via its own name or header comment containing
    // a small set of keywords. This is NOT a full C-105 compliance check (C-105 itself — e.g. its
    // own "header comment states it contains indexed access" requirement — is Bucket B/C, out of
    // Phase-1 scope); it only decides whether C-301's exception applies here.
    private static readonly string[] SelfIdentifyingKeywords = { "data-handling", "data handling", "comms", "communication", "recipe" };

    public static IEnumerable<Finding> CheckC301AbsoluteAddressing(IrBlock block)
    {
        if (IsSelfIdentifiedExemptBlock(block))
        {
            yield break;
        }

        // C-501 as amended (owner ruling, 2026-08-06): THE UNIT IS THE ALARM WORD, NOT THE BIT.
        // This check previously required `sliceWrites.Count == 1` plus a Title, which flagged the
        // proven site shape as a violation — `patterns/motor-dol` NETWORK 14 writes three bits of
        // one word and always did. The doc was amended and this was not, so a block written to the
        // current rule failed review while a block written to the superseded one passed. The three
        // conditions below are the amended rule's own, in its order.
        var networksByWord = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var network in block.Networks)
        {
            foreach (var word in SliceAccessWritesInNetwork(network).Select(SliceWordPath).Distinct(StringComparer.Ordinal))
            {
                if (!networksByWord.TryGetValue(word, out var owners))
                {
                    owners = new List<int>();
                    networksByWord[word] = owners;
                }

                owners.Add(network.Number);
            }
        }

        foreach (var network in block.Networks)
        {
            var sliceAssignments = network.Assignments.Where(a => IsSliceAccessTag(a.CoilTag)).ToList();
            if (sliceAssignments.Count == 0)
            {
                continue;
            }

            var sliceWrites = sliceAssignments.Select(a => a.CoilTag).ToList();
            var words = sliceWrites.Select(SliceWordPath).Distinct(StringComparer.Ordinal).ToList();
            var reasons = new List<string>();

            // Condition 1, both halves: one network per alarm word, and all of that word's bits in it.
            if (words.Count > 1)
            {
                reasons.Add($"it writes bits of {words.Count} different words ({string.Join(", ", words)}) — a network's subject is the one word it changes");
            }

            foreach (var word in words)
            {
                var others = networksByWord[word].Where(n => n != network.Number).ToList();
                if (others.Count > 0)
                {
                    reasons.Add($"'{word}' is also written by network(s) {string.Join(", ", others)} — all of a word's bits belong in one network");
                }
            }

            // Condition 2: every bit driven by a single named cause, never an inline expression.
            var expressionDriven = sliceAssignments.Where(a => !IsSingleNamedCause(a.Condition)).Select(a => a.CoilTag).ToList();
            if (expressionDriven.Count > 0)
            {
                reasons.Add($"{string.Join(", ", expressionDriven)} driven by an inline expression rather than a single named cause (C-130 guarantees a named one exists)");
            }

            // Condition 3: the bit map lives in the network COMMENT. The alarm text used to go in the
            // Title, which worked while a network held exactly one bit; a word-sized network has
            // nowhere else to put it. Checking that every written bit is mentioned is deliberately
            // weaker than parsing the `%X0 = FTR = "..."` form — the point is that no bit is
            // undocumented, and a format assertion here would be brittle without being stronger.
            if (string.IsNullOrWhiteSpace(network.Comment))
            {
                reasons.Add("it has no network comment carrying the bit map (one line per bit, with that bit's C-505 alarm text)");
            }
            else
            {
                var undocumented = sliceWrites
                    .Select(SliceBitToken)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(bit => network.Comment!.IndexOf(bit, StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();
                if (undocumented.Count > 0)
                {
                    reasons.Add($"the network comment's bit map does not mention {string.Join(", ", undocumented)}");
                }
            }

            if (reasons.Count == 0)
            {
                continue;
            }

            var detail = string.Join("; ", reasons);

            yield return new Finding(
                "C-301",
                FindingSeverity.Error,
                block.Name,
                network.Number,
                $"Network {network.Number} writes slice-access bit(s) ({string.Join(", ", sliceWrites)}) without satisfying the C-501 alarm-word exception: {detail}.",
                "Write all of one alarm word's bits in a single commented network, each bit driven by a single named cause, or move this logic into a self-identified data-handling/comms block (C-105).");

            yield return new Finding(
                "C-501",
                FindingSeverity.Warn,
                block.Name,
                network.Number,
                $"Network {network.Number}'s slice-access alarm bit(s) don't satisfy C-501's own conditions: {detail}.",
                "Either restructure to satisfy C-501 as written (one network per alarm word, a single named cause per bit, the bit map in the network comment), or — if this form is intentionally fine — propose it as a documented exception in docs/06-lad-conventions.md rather than leaving the rule and the practice disagreeing.");
        }
    }

    private static bool IsSelfIdentifiedExemptBlock(IrBlock block)
    {
        var haystack = $"{block.Name} {block.Comment}".ToLowerInvariant();
        return SelfIdentifyingKeywords.Any(keyword => haystack.Contains(keyword, StringComparison.Ordinal));
    }

    private static IEnumerable<string> SliceAccessWritesInNetwork(IrNetwork network)
    {
        foreach (var assignment in network.Assignments)
        {
            if (IsSliceAccessTag(assignment.CoilTag))
            {
                yield return assignment.CoilTag;
            }
        }
    }

    // internal, not private: FI-65's claim validator reserves alarm BITS, so it needs the same
    // word/bit decomposition C-501 uses. Copying three lines would let the two drift, and a claim
    // registry that splits a slice path differently from the rule that audits it is worse than none.
    internal static bool IsSliceAccessTag(string tag)
    {
        var lastDot = tag.LastIndexOf('.');
        return lastDot >= 0 && lastDot + 1 < tag.Length && tag[lastDot + 1] == '%';
    }

    // "DB_Alarms.EStopAlarm0.%X3" -> "DB_Alarms.EStopAlarm0". C-501's unit of grouping since the
    // 2026-08-06 amendment: the word is what a network's subject is, so the word is what the rule
    // counts. Only ever called on a tag IsSliceAccessTag already accepted.
    internal static string SliceWordPath(string tag)
    {
        var lastDot = tag.LastIndexOf('.');
        return lastDot >= 0 ? tag[..lastDot] : tag;
    }

    // "DB_Alarms.EStopAlarm0.%X3" -> "%X3", for checking the comment's bit map mentions it.
    internal static string SliceBitToken(string tag)
    {
        var lastDot = tag.LastIndexOf('.');
        return lastDot >= 0 ? tag[(lastDot + 1)..] : tag;
    }

    // C-501 condition 2 — `COIL IO.Alarm.%X0 := IO.FTR`, or `:= IO.FTR AND NOT IO.SuppFTR`.
    //
    // The suppressor term is PERMITTED (owner ruling, 2026-08-06). This check first rejected any
    // AND, which contradicted C-504's filter-placement rule: suppression is applied in the
    // alarm-write network and nowhere else, so a suppressed alarm bit is *necessarily*
    // `cause AND NOT suppressor`. Between them the two rules forbade the only correct
    // implementation, and this check flagged compliant code while passing the superseded form.
    //
    // What stays forbidden is anonymous logic that hides what a bit means — an OR of causes, a
    // comparison, an unnamed intermediate. The test below is therefore shape-specific rather than
    // "does it contain an AND": exactly one bare (or negated-bare) named cause, and every remaining
    // operand a NEGATED bare tag. `A AND B` still fails, because two un-negated tags give the bit
    // two plausible subjects and the reader cannot tell which one the bit is named for.
    private static bool IsSingleNamedCause(Expr condition) => condition switch
    {
        Expr.TagRef => true,
        Expr.Not not => not.Operand is Expr.TagRef,
        Expr.And and => and.Operands.Count(IsBareNamedCause) == 1
                        && and.Operands.All(op => IsBareNamedCause(op) || IsNegatedNamedTag(op)),
        _ => false,
    };

    private static bool IsBareNamedCause(Expr operand) => operand is Expr.TagRef;

    private static bool IsNegatedNamedTag(Expr operand) => operand is Expr.Not { Operand: Expr.TagRef };

    // C-406 (error) — TON is the only timer instruction used; TOF/TONR are violations. Checked in
    // two genuinely different places: the *declaration* form (a DbMember whose own Datatype is
    // "TONR_TIME"/"TOF_TIME" — what's actually sitting in FBTimers.ir, which has zero networks)
    // and the *usage* form (TimerBinding.Kind inside a network's own Timers list — what
    // TimingAndCalls.ir calls). A check against only one form silently misses real violations that
    // only show up in the other.
    public static IEnumerable<Finding> CheckC406TimerUsage(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            foreach (var timer in network.Timers)
            {
                if (timer.Kind is TimerKind.Tonr or TimerKind.Tof)
                {
                    yield return new Finding(
                        "C-406",
                        FindingSeverity.Error,
                        block.Name,
                        network.Number,
                        $"Network {network.Number} calls '{timer.InstancePath}' as a {timer.Kind} timer - only TON is permitted.",
                        "Replace with TON plus explicit inversion/edge logic per C-406's own construction guidance (off-delay/retentive behavior built from TON, not TOF/TONR).");
                }
            }
        }
    }

    public static IEnumerable<Finding> CheckC406TimerDeclarations(string ownerName, IReadOnlyList<DbMember> members)
    {
        foreach (var member in members)
        {
            if (member.Datatype is "TONR_TIME" or "TOF_TIME")
            {
                yield return new Finding(
                    "C-406",
                    FindingSeverity.Error,
                    ownerName,
                    null,
                    $"Member '{member.Name}' is declared as {member.Datatype} - only TON_TIME is permitted.",
                    "Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.");
            }

            if (member.NestedMembers is { Count: > 0 })
            {
                foreach (var finding in CheckC406TimerDeclarations(ownerName, member.NestedMembers))
                {
                    yield return finding;
                }
            }
        }
    }

    // C-102 (error) — no jumps (JMP/LBL). C-401 (error) — no counter instructions
    // (CTU/CTD/CTUD). C-404 (error) — no built-in edge instructions (-|P|-/-|N|-). All three:
    // genuinely vacuous against this converter's IR model today, not "no violations found in this
    // corpus." No Jump/Label, Counter, or P_TRIG/N_TRIG construct exists anywhere in
    // Converter.Ir.Model - the parser's own FlgNetParser.SupportedPartNames whitelist rejects any
    // of these with UnsupportedConstructException before a .ir file can even exist (confirmed
    // directly against FlgNetParser.cs during S4 planning). A file that reaches this checker is
    // structurally incapable of violating any of the three, the same way a fish can't be checked
    // for "doesn't have legs" - these always return no findings, unconditionally, and the caller
    // reports their status as CheckedVacuous rather than Checked for every file, not just when a
    // scan happens to find nothing. Built anyway: cheap, and real defense-in-depth if
    // WAIT/Jump/counters (already-known open questions, see AITODO.md) ever get built into the IR
    // model later - at that point these checks start actually verifying something and their status
    // should be revisited.
    public static IEnumerable<Finding> CheckC102NoJumps(IrBlock block) => Enumerable.Empty<Finding>();

    public static IEnumerable<Finding> CheckC401NoCounters(IrBlock block) => Enumerable.Empty<Finding>();

    public static IEnumerable<Finding> CheckC404NoBuiltInEdgeInstructions(IrBlock block) => Enumerable.Empty<Finding>();

    // C-408 (error) — a timer's ET is never compared to produce a boolean trigger; each time
    // threshold is its own named timer, activated via Q (staged sequences chain timers). Reading ET
    // as a *value* (HMI/diagnostics/proportional) is permitted — that form MOVEs ET to a named
    // variable and carries no comparison, so it never matches here. Scope: ANY comparison operand
    // that is a `.ET` reference, not only comparison against a literal constant (the skill recipe's
    // broader, intent-matching form — owner-confirmed 2026-07-18).
    public static IEnumerable<Finding> CheckC408EtComparison(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            var etPaths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var expr in TagReferences.AllExpressions(network))
            {
                foreach (var path in EtOperandsInComparisons(expr, insideCompare: false))
                {
                    if (seen.Add(path))
                    {
                        etPaths.Add(path);
                    }
                }
            }

            if (etPaths.Count == 0)
            {
                continue;
            }

            yield return new Finding(
                "C-408",
                FindingSeverity.Error,
                block.Name,
                network.Number,
                $"Network {network.Number} compares timer ET ({string.Join(", ", etPaths)}) to produce a boolean trigger - C-408 forbids this. Use a named timer's Q (staged sequences chain timers: Q of stage n enables stage n+1's timer); read ET only as a value copied to a named variable.",
                "Replace the ET comparison with a named timer whose Q gives the trigger; if the ET value itself is needed (display/diagnostics/proportional), MOVE it to a named variable instead of wiring ET mid-rung.");
        }
    }

    // Yields every `.ET` tag reference that appears inside a comparison subtree. `insideCompare`
    // flips true when descending into a Compare's operands, so a `.ET` read outside any comparison
    // (the permitted value read) is never yielded.
    private static IEnumerable<string> EtOperandsInComparisons(Expr expr, bool insideCompare)
    {
        switch (expr)
        {
            case Expr.TagRef tagRef:
                if (insideCompare && tagRef.Path.EndsWith(".ET", StringComparison.Ordinal))
                {
                    yield return tagRef.Path;
                }

                break;
            case Expr.And and:
                foreach (var operand in and.Operands)
                {
                    foreach (var path in EtOperandsInComparisons(operand, insideCompare))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Or or:
                foreach (var operand in or.Operands)
                {
                    foreach (var path in EtOperandsInComparisons(operand, insideCompare))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Not not:
                foreach (var path in EtOperandsInComparisons(not.Operand, insideCompare))
                {
                    yield return path;
                }

                break;
            case Expr.Compare compare:
                foreach (var path in EtOperandsInComparisons(compare.Left, insideCompare: true))
                {
                    yield return path;
                }

                foreach (var path in EtOperandsInComparisons(compare.Right, insideCompare: true))
                {
                    yield return path;
                }

                break;
            case Expr.Literal:
                break;
        }
    }

    // C-410 (error) — a timer's own IN never reads that same timer instance's own output.
    //
    // Ground truth (a live job, 2026-08-18): a timer written `TON(X, IN := NOT X.Q, PT := ...)`
    // fires once after startup and then does not re-arm — or re-arms only after an enormous,
    // irregular delay. Established by controlled experiment on real hardware: in one program a
    // timer with an ordinary Bool `IN` kept perfect time while a self-referential one did not, and
    // breaking the self-reference through a plain Bool repaired it, measurably, on the device. Six
    // instances were found in one corpus, by a person happening to grep for them; one was a
    // simulation layer's master clock, whose failure mode was every simulated rate multiplied by
    // zero while every health bit stayed good. That is the whole argument for mechanizing it: the
    // defect is silent, it is fatal to the block it sits in, and it is detectable structurally.
    //
    // *** TWO SEVERITIES OF THE DEFECT, REPORTED DISTINGUISHABLY, BOTH GATING. ***
    //   TOTAL   — the IN reads the timer's own output and NOTHING ELSE (`IN := NOT X.Q`). The timer
    //             has no external arming term at all: it never re-arms.
    //   PARTIAL — the self-reference is conjoined with at least one external term
    //             (`IN := ArmBit AND NOT X.Q`). Every disarm→arm transition gives a fresh first
    //             cycle, which works; the second and later cycles inside one armed period do not.
    // Both are emitted as Error rather than TOTAL=Error/PARTIAL=Warn. The distinction is carried in
    // the finding text (the literal words SELF-RESTART (TOTAL) / SELF-RESTART (PARTIAL)) so a reader
    // and a grep can both separate them, but a PARTIAL still ships a block that silently stops
    // timing after its first cycle — and a finding that only warns is the class this project has
    // already recorded as getting skimmed. Nothing is hidden by the choice: the severity says
    // "this gates", the text says which of the two it is.
    //
    // *** SCOPE IS DIRECT, DELIBERATELY, AND THE INDIRECT FORM IS THE FIX. ***
    // The rule fires only when the self-reference is a tag reference INSIDE THE TIMER'S OWN IN
    // expression. It does NOT chase a path through an intermediate bit, because the one-hop
    // indirect form — assign the timer's Q to a named Bool, gate the IN on that Bool — IS the
    // repair this defect has: it is what was applied to the live corpus and measured working on the
    // device. In that corpus the repaired coil sits in the SAME NETWORK as the timer it feeds, so
    // even a network-order-sensitive "the intermediate is written no later than the timer" variant
    // would flag the fix. A rule that flags the fix is worse than no rule, and the mechanism behind
    // the hardware behaviour is known only empirically (route it through a Bool and it works), which
    // is not enough to justify guessing which indirect paths are still broken. So this rule is
    // narrow ON PURPOSE and its name says exactly what it covers: the IN reads its own output.
    //
    // *** SELF-REFERENCE MEANS THE INSTANCE'S OWN *OUTPUT* — `Q` OR `ET`, NEVER `IN` OR `PT`. ***
    // `.Q` is the measured case and `.ET` is the same feedback loop through the other output port
    // (C-408 has its own, separate quarrel with ET comparisons). A read of the timer's own **`.IN`**
    // member is a different construction entirely: it is the self-holding term of a latch, using the
    // instruction's input image as the latch memory instead of a separate Bool. Three timers in the
    // committed reference corpora do exactly that, and an earlier draft of this rule reported them
    // as deriving the IN "from its own output" — which is simply not true of an input member, and a
    // finding that misdescribes what it found is how a real rule gets switched off. An `IN` that
    // reads BOTH its own `.IN` and its own `.Q` (the self-holding one-shot,
    // `Trigger OR Self.IN AND NOT Self.Q`) still fires, on the `.Q` alone — the Q feedback is the
    // thing measured to misbehave, whatever else the rung reads.
    //
    // Kind-agnostic: TON is the only permitted timer
    // (C-406) but a TONR/TOF written this way has the same shape, and a block carrying both defects
    // should be told about both. The TONR reset port is NOT examined — a retentive timer cleared by
    // its own Q is a different construction with a different argument, and C-406 already refuses
    // TONR outright.
    public static IEnumerable<Finding> CheckC410SelfRestartingTimer(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            foreach (var timer in network.Timers)
            {
                var leaves = CollectTagRefPaths(timer.In).ToList();
                var selfPaths = leaves
                    .Where(path => ReferencesOwnTimerOutput(path, timer.InstancePath))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (selfPaths.Count == 0)
                {
                    continue;
                }

                // TOTAL when the IN has no term that is not the timer's own output. Literals do not
                // count as external terms — `IN := X.ET < T#1S` still arms on nothing but itself. A
                // read of the timer's own `.IN`/`.PT` DOES count as external here: it is a term the
                // rung genuinely carries, and calling such a rung TOTAL would claim the timer has no
                // arming path when what it has is a latch.
                var externalTerms = leaves.Count(path => !ReferencesOwnTimerOutput(path, timer.InstancePath));
                var total = externalTerms == 0;

                yield return new Finding(
                    "C-410",
                    FindingSeverity.Error,
                    block.Name,
                    network.Number,
                    total
                        ? $"Timer '{timer.InstancePath}' (network {network.Number}) derives its IN from its own output ({string.Join(", ", selfPaths)}) and from nothing else — SELF-RESTART (TOTAL). Measured on real hardware: a timer wired this way fires once and then does not re-arm, or re-arms only after an enormous, irregular delay."
                        : $"Timer '{timer.InstancePath}' (network {network.Number}) derives its IN from its own output ({string.Join(", ", selfPaths)}), conjoined with {externalTerms} external term(s) — SELF-RESTART (PARTIAL). Each disarm-to-arm transition gives one fresh cycle that works; the second and later cycles inside one armed period do not.",
                    $"Break the self-reference through a plain Bool: write the timer's Q to a named Bool of its own (`COIL <Flag> := {timer.InstancePath}.Q`) and gate the timer's IN on that Bool instead of on its own output. That is the repair measured working on the device — the IN must not name the timer itself.");
            }
        }
    }

    // True when `path` reads one of THIS timer instance's own OUTPUT members — `<instance>.Q` or
    // `<instance>.ET`. Deliberately not "any member of the instance": `.IN` and `.PT` are inputs,
    // and a read of the timer's own `.IN` is a latch's self-holding term, not output feedback.
    //
    // Case-insensitive because TIA identifiers are, and a case difference here would be a silent
    // miss on the one thing the rule exists to catch. The instance half is prefix-tested rather than
    // split, so a nested or array instance path (`Group.Dwell.Q`, `Timers[1].Q`) matches its own
    // instance while a same-prefixed sibling (`XTimer.Q` against instance `X`) does not.
    private static bool ReferencesOwnTimerOutput(string path, string instancePath)
    {
        if (path.Length <= instancePath.Length
            || !path.StartsWith(instancePath, StringComparison.OrdinalIgnoreCase)
            || path[instancePath.Length] != '.')
        {
            return false;
        }

        // The first component after the instance is the port; anything below it (a hypothetical
        // `X.Q.<something>`) is still a read of that port.
        var member = path[(instancePath.Length + 1)..];
        var dot = member.IndexOf('.');
        var port = dot < 0 ? member : member[..dot];
        return string.Equals(port, "Q", StringComparison.OrdinalIgnoreCase)
            || string.Equals(port, "ET", StringComparison.OrdinalIgnoreCase);
    }

    // C-103 (warn) — Set/Reset pairs live in the same block, ideally adjacent networks. Mechanized
    // per-file: collect every CoilTag written as a Set (SCOIL, CoilKind.Set) and every one written
    // as a Reset (RCOIL, CoilKind.Reset) across the block's networks, then flag each Set-target with
    // no matching Reset-target in this same block, and each Reset-target with no matching Set-target.
    //
    // Deliberately worded as a *candidate*, never a hard-asserted defect: the owner-ruled exception
    // (a fault-style bit intentionally Set from OUTSIDE a reusable FB by the orchestrating FC while
    // the FB Resets it internally, or vice versa) is genuinely cross-block, so a per-file tool cannot
    // distinguish it from a real unpaired Set/Reset — it can only surface the candidate for a human
    // to confirm against the intent comment. Findings carry the network where the unpaired coil first
    // appears (first-seen wins for a target written in several networks), so the reader lands on it
    // directly rather than getting a bare block-level pointer.
    public static IEnumerable<Finding> CheckC103SetResetPairing(IrBlock block)
    {
        // First-seen network per target, insertion-ordered for deterministic output.
        var setTargets = new List<KeyValuePair<string, int>>();
        var resetTargets = new List<KeyValuePair<string, int>>();
        var setSeen = new HashSet<string>(StringComparer.Ordinal);
        var resetSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var network in block.Networks)
        {
            foreach (var assignment in network.Assignments)
            {
                switch (assignment.Kind)
                {
                    case CoilKind.Set when setSeen.Add(assignment.CoilTag):
                        setTargets.Add(new KeyValuePair<string, int>(assignment.CoilTag, network.Number));
                        break;
                    case CoilKind.Reset when resetSeen.Add(assignment.CoilTag):
                        resetTargets.Add(new KeyValuePair<string, int>(assignment.CoilTag, network.Number));
                        break;
                }
            }
        }

        foreach (var (tag, networkNumber) in setTargets)
        {
            if (!resetSeen.Contains(tag))
            {
                yield return new Finding(
                    "C-103",
                    FindingSeverity.Warn,
                    block.Name,
                    networkNumber,
                    $"Set coil (SCOIL) on '{tag}' has no matching Reset (RCOIL) in this block; if this is the deliberate external-set/internal-reset reusable-FB pattern, that is the documented C-103 exception — confirm the intent comment.",
                    $"Add the paired Reset of '{tag}' in this block (ideally an adjacent network), or — if the pairing is intentionally cross-block (reusable-FB pattern) — record that intent in a comment so the split is deliberate, not an oversight.");
            }
        }

        // C-403's dedicated startup-reset block is exempt from the reset-without-set half, and ONLY
        // that half (2026-08-24). C-403 requires every S/R-written bit in the program to be reset in
        // one dedicated startup OB; by construction that block can contain nothing BUT unpaired
        // resets, so C-103's "pair them in the same block" cannot be satisfied there without
        // breaching C-403. The two rules are in direct conflict on this one block, and C-403 wins:
        // it is the one that keeps equipment from restarting into a latched state.
        //
        // Measured on a real generation before this fix: 131 of that run's 146 findings — 90% — were
        // this single structural false positive on one OB, all byte-identical bar the operand. That
        // is not noise, it is a gate that hides its own signal: a genuine finding would have had to
        // be spotted among 131 false ones, and the honest reading of a 146-finding report is that
        // nobody reads it.
        //
        // The set-without-reset half above is deliberately NOT exempted. A startup block exists to
        // clear bits; a SET coil in one is exactly the thing worth asking about, and silencing the
        // whole rule to remove the false half would trade one blind spot for another.
        var isStartupResetBlock =
            string.Equals(block.Kind, "OB", StringComparison.Ordinal) &&
            string.Equals(block.SecondaryType, "Startup", StringComparison.Ordinal);

        foreach (var (tag, networkNumber) in resetTargets)
        {
            if (!setSeen.Contains(tag) && !isStartupResetBlock)
            {
                yield return new Finding(
                    "C-103",
                    FindingSeverity.Warn,
                    block.Name,
                    networkNumber,
                    $"Reset coil (RCOIL) on '{tag}' has no matching Set (SCOIL) in this block; if this is the deliberate external-set/internal-reset reusable-FB pattern, that is the documented C-103 exception — confirm the intent comment.",
                    $"Add the paired Set of '{tag}' in this block (ideally an adjacent network), or — if the pairing is intentionally cross-block (reusable-FB pattern) — record that intent in a comment so the split is deliberate, not an oversight.");
            }
        }
    }

    // C-121 (error) — a step transition is a plain MOVE to the Step register whose EN carries an
    // inline `Step = <from>` guard: `MOVE(EN := Step = <from> AND <condition>, IN := <to>) => Step`.
    // Never a coil, JMP/LBL, or other statement. Two mechanically-checkable failure modes (INLINE
    // form only — the owner-clarified named-bit-equivalent of `Step = <from>` is not mechanically
    // verifiable and is deferred to AI):
    //   (a) a Step register (DestTag leaf == "Step", per C-118) written by anything OTHER than a
    //       plain MoveStatement (a coil or a box instruction) — a hard defect, the transition isn't a
    //       MOVE at all;
    //   (b) a MOVE to Step whose EN tree contains no `Step = <from>` comparison guard — worded as a
    //       candidate, since a named bit that is a genuine equivalent of `Step = <from>` is a
    //       judgment call this tool cannot confirm.
    public static IEnumerable<Finding> CheckC121StepTransition(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            // Case (a): any non-MOVE statement whose write target is a Step register.
            foreach (var (kind, destTag) in NonMoveDestWrites(network))
            {
                if (HasStepLeaf(destTag))
                {
                    yield return new Finding(
                        "C-121",
                        FindingSeverity.Error,
                        block.Name,
                        network.Number,
                        $"Network {network.Number} writes the Step register '{destTag}' with a {kind}, not a MOVE — a Step transition must be a plain MOVE (C-121: MOVE(EN := Step = <from> AND <condition>, IN := <to>) => Step; never a coil/JMP/LBL/box instruction).",
                        "Express the transition as a plain MOVE to Step gated by `Step = <from> AND <condition>` in its EN.");
                }
            }

            // Case (b): a MOVE to Step with no inline `Step = <from>` guard in its EN.
            foreach (var move in network.Moves)
            {
                if (!HasStepLeaf(move.DestTag))
                {
                    continue;
                }

                if (!ExprHasStepGuard(move.En))
                {
                    yield return new Finding(
                        "C-121",
                        FindingSeverity.Error,
                        block.Name,
                        network.Number,
                        $"Network {network.Number}'s MOVE to Step ('{move.DestTag}') has no inline `Step = <from>` guard in its EN; C-121 requires it unless a named bit is a genuine equivalent of `Step = <from>` (that form is a judgment call — confirm).",
                        "Gate the transition MOVE with `Step = <from> AND <condition>` in its EN, or confirm that a named bit standing in for `Step = <from>` is intended (deferred to human/AI review, not mechanically verifiable).");
                }
            }
        }
    }

    // The C-118 phase tag is named "Step" — a Step register write is any DestTag whose leaf
    // (last dot-separated component) is exactly "Step" (a bare "Step", or a member like "IO.Step").
    private static bool HasStepLeaf(string tag) =>
        tag == "Step" || tag.EndsWith(".Step", StringComparison.Ordinal);

    // Every non-MOVE statement kind that writes a destination tag, paired with an IR-facing kind
    // label — used by C-121 case (a) to catch a Step register written by anything other than a plain
    // MOVE. MoveStatement is deliberately excluded (case (b) handles genuine MOVEs); MOVE_BLK_VARIANT
    // is included because it is not a plain scalar MOVE. Modbus multi-write instructions are omitted:
    // their destinations are Done/Busy/Error/Status status bits, never a Step register.
    private static IEnumerable<(string Kind, string DestTag)> NonMoveDestWrites(IrNetwork network)
    {
        foreach (var assignment in network.Assignments)
        {
            var kind = assignment.Kind switch
            {
                CoilKind.Set => "Set coil (SCOIL)",
                CoilKind.Reset => "Reset coil (RCOIL)",
                _ => "coil (COIL)",
            };
            yield return (kind, assignment.CoilTag);
        }

        foreach (var wordAnd in network.WordAnds)
        {
            yield return ("WAND", wordAnd.DestTag);
        }

        foreach (var mul in network.Muls)
        {
            yield return (mul.Kind.ToString().ToUpperInvariant(), mul.DestTag);
        }

        foreach (var convert in network.Converts)
        {
            yield return ("CONVERT", convert.DestTag);
        }

        foreach (var swap in network.Swaps)
        {
            yield return ("SWAP", swap.DestTag);
        }

        foreach (var abs in network.AbsStatements)
        {
            yield return ("ABS", abs.DestTag);
        }

        foreach (var limit in network.Limits)
        {
            yield return ("LIMIT", limit.DestTag);
        }

        foreach (var tSub in network.TSubs)
        {
            yield return ("T_SUB", tSub.DestTag);
        }

        foreach (var tConv in network.TConvs)
        {
            yield return ("T_CONV", tConv.DestTag);
        }

        foreach (var calc in network.Calcs)
        {
            yield return ("CALC", calc.DestTag);
        }

        foreach (var fill in network.FillBlockIs)
        {
            yield return ("FillBlockI", fill.DestTag);
        }

        foreach (var moveBlk in network.MoveBlkVariants)
        {
            yield return ("MOVE_BLK_VARIANT", moveBlk.DestTag);
            yield return ("MOVE_BLK_VARIANT", moveBlk.RetValTag);
        }

        foreach (var fixedShape in network.FixedShapes)
        {
            foreach (var argument in fixedShape.Arguments)
            {
                if (argument.Binding is PortBinding.Dest dest)
                {
                    yield return (fixedShape.Instruction, dest.Tag);
                }
            }
        }
    }

    // True when the EN Expr tree contains a comparison (Expr.Compare) with a `Step` register on one
    // side — the inline `Step = <from>` guard C-121 requires. Walks And/Or/Not/Compare recursively,
    // mirroring EtOperandsInComparisons's traversal style. Any comparison operator counts: a genuine
    // `Step = <from>` uses `=`, but flagging only `=` would false-negative a hand-written `Step <> n`
    // style guard; presence of a Step-vs-value comparison in EN is the mechanizable signal.
    private static bool ExprHasStepGuard(Expr expr)
    {
        switch (expr)
        {
            case Expr.Compare compare:
                return IsStepTagRef(compare.Left) || IsStepTagRef(compare.Right)
                    || ExprHasStepGuard(compare.Left) || ExprHasStepGuard(compare.Right);
            case Expr.And and:
                return and.Operands.Any(ExprHasStepGuard);
            case Expr.Or or:
                return or.Operands.Any(ExprHasStepGuard);
            case Expr.Not not:
                return ExprHasStepGuard(not.Operand);
            default:
                return false;
        }
    }

    private static bool IsStepTagRef(Expr expr) =>
        expr is Expr.TagRef tagRef && HasStepLeaf(tagRef.Path);

    // C-118 (error) — CROSS-FILE (FI-09). A stepped sequence's phase is exactly one `Step : Int`
    // member living inside the block's caller-visible interface UDT — never a bare private Static,
    // never a `DB_Controls`/`DB_Settings` member (docs/06 C-118). The Step member lives in a
    // SEPARATE file (the referenced UDT), so this is the first review rule needing a second file:
    // it resolves the UDT a top-level interface member is typed as via the --project index
    // (TagTypeRegistry). Without an index the enclosing UDT can't be resolved, so ReviewRunner
    // records the rule NotApplicable rather than calling this at all.
    //
    // Trigger: the block actually uses a Step register — some tag it reads or writes has leaf
    // "Step" (HasStepLeaf, the same phase-tag convention C-121 keys off). A block that never
    // touches a Step register has no stepped sequence for C-118 to place, so it yields nothing.
    // Findings key off WHERE the referenced Step register lives: a bare `Step` is block-local; a
    // `Root.Step` whose Root is a UDT-typed interface member is the correct home (checked for
    // exactly-one and Int); a `Root.Step` whose Root is a known DB is the forbidden Controls/
    // Settings placement; anything else is present-but-unresolvable.
    public static IEnumerable<Finding> CheckC118StepInterfaceUdt(IrBlock block, TagTypeRegistry udtIndex)
    {
        var stepPaths = block.Networks
            .SelectMany(TagReferences.AllTagPaths)
            .Where(HasStepLeaf)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (stepPaths.Count == 0)
        {
            yield break;
        }

        var interfaceMembers = TopLevelInterfaceMembers(block).ToList();

        foreach (var path in stepPaths)
        {
            var components = path.Split('.');

            // Bare `Step` — a block-local (private Static/Temp) register, not the interface UDT.
            if (components.Length == 1)
            {
                yield return new Finding(
                    "C-118",
                    FindingSeverity.Error,
                    block.Name,
                    null,
                    $"The step register is referenced as a bare '{path}', a block-local variable — C-118 requires the phase to live as a `Step : Int` member inside the block's caller-visible interface UDT, never a bare private Static.",
                    "Declare `Step : Int` inside the block's interface UDT and reference it through that UDT-typed interface member (e.g. `IO.Step`).");
                continue;
            }

            var root = StripSubscriptComponent(components[0]);
            var rootMember = interfaceMembers.FirstOrDefault(m => string.Equals(m.Name, root, StringComparison.Ordinal));

            // Root is a top-level interface member typed as a UDT the index knows — the correct
            // home. Descend into that UDT and check its Step member(s): exactly one, typed Int.
            if (rootMember is not null && udtIndex.TryGetUdt(rootMember.Datatype.Trim('"'), out var udt))
            {
                var udtName = rootMember.Datatype.Trim('"');
                var steps = udt.Members.Where(m => string.Equals(m.Name, "Step", StringComparison.Ordinal)).ToList();

                if (steps.Count == 0)
                {
                    yield return new Finding(
                        "C-118",
                        FindingSeverity.Error,
                        block.Name,
                        null,
                        $"The step register '{path}' is referenced through interface member '{root}' (type '{udtName}'), but that UDT declares no `Step` member — C-118's `Step : Int` cannot be resolved.",
                        "Add `Step : Int` to the interface UDT, or reference the correct interface member.");
                    continue;
                }

                if (steps.Count > 1)
                {
                    yield return new Finding(
                        "C-118",
                        FindingSeverity.Error,
                        block.Name,
                        null,
                        $"Interface UDT '{udtName}' declares {steps.Count} members named `Step` — C-118 requires exactly one phase register.",
                        "Keep a single `Step : Int` phase member in the interface UDT and remove the duplicates.");
                }

                foreach (var step in steps.Where(s => !string.Equals(s.Datatype.Trim('"'), "Int", StringComparison.Ordinal)))
                {
                    yield return new Finding(
                        "C-118",
                        FindingSeverity.Error,
                        block.Name,
                        null,
                        $"The `Step` member of interface UDT '{udtName}' is '{step.Datatype}', not `Int` — C-118 requires `Step : Int`.",
                        "Declare the phase register as `Step : Int` inside the interface UDT.");
                }

                continue;
            }

            // Root resolves to a DB the index knows (e.g. DB_Controls/DB_Settings) — the wrong home.
            if (udtIndex.IsKnownDb(root))
            {
                yield return new Finding(
                    "C-118",
                    FindingSeverity.Error,
                    block.Name,
                    null,
                    $"The step register '{path}' lives in DB '{root}', not the block's interface UDT — C-118 forbids placing the phase in a `DB_Controls`/`DB_Settings` (or any) DB.",
                    "Declare `Step : Int` inside the block's caller-visible interface UDT and reference it there instead of via a DB.");
                continue;
            }

            // Present but unresolvable: step logic exists, but its register's root is neither a
            // UDT-typed interface member nor a known DB — no interface-UDT `Step : Int` to point to.
            yield return new Finding(
                "C-118",
                FindingSeverity.Error,
                block.Name,
                null,
                $"The step register '{path}' does not resolve to a `Step : Int` member of the block's interface UDT — root '{root}' is neither a UDT-typed interface member nor a DB known to the --project index.",
                "Ensure the phase is a `Step : Int` member of the block's caller-visible interface UDT, referenced through the UDT-typed interface member.");
        }
    }

    // Does this block give C-118 anything to judge? A block that never touches a Step register has
    // no stepped sequence to place, so C-118 is genuinely NotApplicable to it — with or without a
    // --project index. A block that DOES reference one, reviewed with no index, is a different
    // animal: the rule has a subject and was NOT judged. ReviewRunner uses this to tell those two
    // apart, because reporting both as "not applicable" is how "we did not check" reads as "clean".
    internal static bool UsesStepRegister(IrBlock block) =>
        block.Networks.SelectMany(TagReferences.AllTagPaths).Any(HasStepLeaf);

    // Same question for C-122/C-125, whose subject is a step-gated (dwell) timer.
    internal static bool HasStepGatedTimer(IrBlock block) =>
        block.Networks.SelectMany(n => n.Timers).Any(t => ExprHasStepGuard(t.In));

    // C-119 (error) — SINGLE-FILE. Idle/home is always step 0. Mechanized narrowly: collect the
    // block's step-number set (CollectBlockStepNumbers) and assert 0 is present; a stepped sequence
    // with no step 0 is flagged. ONLY step-0 PRESENCE is mechanized — C-119's other half ("returned
    // to explicitly on stop, fault recovery, and restart", tied to C-124) is a judgment call this
    // tool cannot verify, so it is deliberately NOT checked (and the finding says so). A block with
    // no step logic at all yields nothing (rule has nothing to place).
    public static IEnumerable<Finding> CheckC119IdleIsStepZero(IrBlock block)
    {
        var steps = CollectBlockStepNumbers(block);
        if (steps.Count == 0)
        {
            yield break;
        }

        if (!steps.Contains(0))
        {
            yield return new Finding(
                "C-119",
                FindingSeverity.Error,
                block.Name,
                null,
                $"Stepped sequence has no step 0 — idle/home must be step 0 (C-119). Steps present: {string.Join(", ", steps.OrderBy(n => n))}. (Only step-0 presence is checked mechanically; C-119's 'returned to explicitly on stop/fault/restart' half — C-124 — is a judgment call not verified here.)",
                "Number the idle/home state step 0, and route stop/fault-recovery/restart back to it (C-124).");
        }
    }

    // C-120 (warn) — SINGLE-FILE. Steps ascend in multiples of 10, so a later revision can insert
    // one without renumbering. Mechanized narrowly: every distinct step number in the block's set
    // must be a multiple of 10; any that isn't is flagged. The step-legend-comment (C-201) and
    // one-sentence-phase (C-101) clauses of C-120 are judgment calls — deliberately NOT mechanized.
    public static IEnumerable<Finding> CheckC120StepsMultipleOfTen(IrBlock block)
    {
        foreach (var n in CollectBlockStepNumbers(block).Where(n => n % 10 != 0).OrderBy(n => n))
        {
            yield return new Finding(
                "C-120",
                FindingSeverity.Warn,
                block.Name,
                null,
                $"Step {n} is not a multiple of 10 (C-120: steps ascend by 10 so a later revision can insert one without renumbering).",
                $"Renumber step {n} to a multiple of 10. (C-120's step-legend and one-sentence-phase clauses are judgment calls, not checked here.)");
        }
    }

    // C-603 (warn) — SINGLE-FILE. "Step membership is enumerated, not ranged." A condition over a
    // stepped sequence's phase enumerates the steps it means (`Step = 30 OR Step = 40 OR Step = 50`);
    // an ordered-range predicate (`>=`, `<=`, `>`, `<`, and the two-sided spans built from them) is
    // allowed "only where 'every future step inserted in this span belongs here too' is the stated
    // intent (comment)". `Step = n` and `Step <> n` are always fine — neither is ordered, so neither
    // silently absorbs an inserted step.
    //
    // *** WHERE THE MECHANICAL LINE IS DRAWN, AND WHY IT IS DRAWN THERE. ***
    // The rule's exemption is "the intent is STATED in the comment". Deciding whether a paragraph of
    // English states that intent is taste, and this runner does not do taste. Deciding whether the
    // network merely HAS a comment is worthless — the measured case (FB_ShredderSequencer network 14
    // before 2026-08-21) had one comment covering FOUR step-conditioned coils, stated the range
    // intent for exactly ONE of them, and carried two unstated ranges alongside it. A
    // has-any-comment exemption passes all four; the rule must fail three.
    //
    // So the mechanized test is per-SUBJECT, not per-network: *** THE NETWORK COMMENT MUST NAME THE
    // WRITE TARGET WHOSE CONDITION CARRIES THE RANGE ***, matched on a word boundary against the
    // target's leaf name (`IO.PusherParkCmd` -> `PusherParkCmd`) or its full dotted path. A comment
    // that never mentions the coil cannot have stated an intent for it; a comment that does mention
    // it by name is where a reader would go to find the intent, and whether the sentence there
    // actually says it stays with the human/AI simplicity reviewer. This check is deliberately
    // ONE-SIDED: it can prove the intent was NOT stated (nobody wrote the name down), never that it
    // WAS. That is the same shape as C-121's "named bit may be a genuine equivalent" deferral, and
    // it is why the finding text says what the reviewer still has to confirm.
    //
    // *** ONE EXCEPTION TO THE NAME ANCHOR: A WRITE TO THE STEP REGISTER ITSELF. *** The anchor works
    // because a coil name is distinctive prose. "Step" is not — every sequencer's network comments
    // are full of the word, so anchoring a Step-write's range on it would exempt essentially every
    // transition automatically. A ranged predicate guarding a MOVE to Step is therefore always a
    // finding; the repair is C-601's (name the condition to a bit, then the bit's name is an anchor),
    // or enumeration, which is what C-121's `Step = <from>` transitions want anyway.
    //
    // ATTRIBUTION AND ITS RESIDUAL (EMPTY IS NOT CLEAN). Subjects come from
    // TagReferences.AllDirectedUsages — every WRITE carries its guarding condition (coil condition /
    // instruction EN), which is where a step-membership condition lives. A ranged step predicate can
    // in principle sit somewhere that is not a write guard (a CALL input argument, a MOVE's IN
    // value). Those are NOT quietly dropped and NOT quietly passed: they are returned separately in
    // UnattributedRanges, and ReviewRunner records C-603 Skipped for the file — exit 2, REVIEW
    // INCOMPLETE — because the rule had a subject there and could not judge it.
    public static C603Result CheckC603StepMembershipEnumerated(IrBlock block)
    {
        var findings = new List<Finding>();
        var unattributed = new List<string>();

        foreach (var network in block.Networks)
        {
            // Reference identity, not value equality: two structurally identical `Step >= 20` nodes
            // in one network are two occurrences, and a guard reached twice (the same Expr instance
            // reported for two writes) must not be counted twice.
            var attributed = new HashSet<Expr>(ReferenceEqualityComparer.Instance);

            foreach (var usage in TagReferences.AllDirectedUsages(network))
            {
                if (usage.Direction != TagDirection.Write || usage.Guard is null)
                {
                    continue;
                }

                var ranges = RangedStepComparisons(usage.Guard).ToList();
                if (ranges.Count == 0)
                {
                    continue;
                }

                foreach (var range in ranges)
                {
                    attributed.Add(range);
                }

                var rendered = string.Join(", ", ranges.Select(RenderComparison).Distinct(StringComparer.Ordinal));

                if (HasStepLeaf(usage.Path))
                {
                    findings.Add(new Finding(
                        "C-603",
                        FindingSeverity.Warn,
                        block.Name,
                        network.Number,
                        $"Network {network.Number}'s write to the Step register '{usage.Path}' is guarded by an ordered-range step predicate ({rendered}) — C-603 wants step membership enumerated (`Step = 30 OR Step = 40 …`). A range guarding a Step write cannot be exempted by a stated intent in the network comment the way a named coil can: the word \"Step\" appears throughout a sequencer's comments, so it identifies no particular condition.",
                        "Enumerate the steps this transition applies to, or name the condition to its own bit (C-601) and state the range intent against that name in the network comment."));
                    continue;
                }

                if (CommentNamesSubject(network.Comment, usage.Path))
                {
                    // Named in the comment — the intent MAY be stated there. Not mechanically
                    // confirmable, so this is where the tool stops and the reviewer starts.
                    continue;
                }

                findings.Add(new Finding(
                    "C-603",
                    FindingSeverity.Warn,
                    block.Name,
                    network.Number,
                    $"Network {network.Number} drives '{usage.Path}' from an ordered-range step predicate ({rendered}), and the network comment never names '{usage.Path}' — so the \"every future step inserted in this span belongs here too\" intent C-603 requires is not stated for this condition. (C-120 lets a later revision insert step 45; a range absorbs it with no visible decision.)",
                    $"Enumerate the steps this condition means (`Step = 30 OR Step = 40 OR Step = 50`), or — if the span really is the intent — say so in the network comment, naming '{LeafName(usage.Path)}' so a reader can tell which condition the sentence is about."));
            }

            foreach (var expr in TagReferences.AllExpressions(network))
            {
                foreach (var range in RangedStepComparisons(expr))
                {
                    if (!attributed.Contains(range))
                    {
                        unattributed.Add($"network {network.Number}: {RenderComparison(range)}");
                    }
                }
            }
        }

        return new C603Result(findings, unattributed);
    }

    // Every ordered-range comparison over a Step register in an Expr tree. `=` and `<>` are excluded
    // by C-603's own text ("`Step <> 0` and `Step = n` are always fine"): neither is ordered, so
    // neither can silently absorb a step inserted between two existing numbers. Traversal mirrors
    // ExprHasStepGuard/StepComparisonLiterals so a rule cannot see a different tree than its
    // neighbours do. The compared-against side need not be a literal — `Step >= FirstRunStep` is
    // just as ordered, and just as absorbing, as `Step >= 30`.
    private static IEnumerable<Expr.Compare> RangedStepComparisons(Expr expr)
    {
        switch (expr)
        {
            case Expr.Compare compare:
                if (OrderedRangeOperators.Contains(compare.Operator)
                    && (IsStepTagRef(compare.Left) || IsStepTagRef(compare.Right)))
                {
                    yield return compare;
                }

                foreach (var c in RangedStepComparisons(compare.Left))
                {
                    yield return c;
                }

                foreach (var c in RangedStepComparisons(compare.Right))
                {
                    yield return c;
                }

                break;
            case Expr.And and:
                foreach (var operand in and.Operands)
                {
                    foreach (var c in RangedStepComparisons(operand))
                    {
                        yield return c;
                    }
                }

                break;
            case Expr.Or or:
                foreach (var operand in or.Operands)
                {
                    foreach (var c in RangedStepComparisons(operand))
                    {
                        yield return c;
                    }
                }

                break;
            case Expr.Not not:
                foreach (var c in RangedStepComparisons(not.Operand))
                {
                    yield return c;
                }

                break;
        }
    }

    private static readonly HashSet<string> OrderedRangeOperators = new(StringComparer.Ordinal) { ">=", "<=", ">", "<" };

    // Word-boundary containment, so a subject named `Run` is not "named" by the word `Running` and a
    // subject named `IO.PusherParkCmd` is found from the comment's own `PusherParkCmd`. Case-
    // insensitive: S7 identifiers are, and a comment that writes `pusherParkCmd` has still named the
    // coil. Both the leaf and the full dotted path count as the name.
    private static bool CommentNamesSubject(string? comment, string tagPath)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        return ContainsWord(comment!, tagPath) || ContainsWord(comment!, LeafName(tagPath));
    }

    private static bool ContainsWord(string haystack, string needle)
    {
        if (needle.Length == 0)
        {
            return false;
        }

        var index = haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var beforeOk = index == 0 || !IsIdentChar(haystack[index - 1]);
            var end = index + needle.Length;
            var afterOk = end >= haystack.Length || !IsIdentChar(haystack[end]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            index = haystack.IndexOf(needle, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    // A dotted path's own separator is NOT an identifier character here on purpose: matching the leaf
    // `PusherParkCmd` inside the path `IO.PusherParkCmd` is exactly the match this wants.
    //
    // A HYPHEN IS. C-005's charset gives no S7 identifier a hyphen, so a hyphen next to the name is
    // always English compounding, not the tag: a comment reading "from the reverse-run step onward"
    // has not named a coil called `Run`. Counting it as a boundary would exempt short generic names
    // on ordinary prose, and the wrong direction to be wrong in is the one that grants an exemption.
    // The cost is a comment written as `PusherParkCmd-driven`, which fails to exempt and reports a
    // finding — fail-closed, which is the direction this project's checks are supposed to fail in.
    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    private static string LeafName(string tagPath)
    {
        var idx = tagPath.LastIndexOf('.');
        return StripSubscriptComponent(idx < 0 ? tagPath : tagPath[(idx + 1)..]);
    }

    // Enough of an Expr renderer to quote the offending predicate back at the reader. IrSerializer's
    // own is private and serializes a whole statement; a finding only ever needs `<operand> <op>
    // <operand>`, and anything more structured than a tag or a literal is named rather than
    // reproduced (it is never the interesting half of a step comparison).
    private static string RenderComparison(Expr.Compare compare) =>
        $"{RenderOperand(compare.Left)} {compare.Operator} {RenderOperand(compare.Right)}";

    private static string RenderOperand(Expr expr) => expr switch
    {
        Expr.TagRef tagRef => tagRef.Path,
        Expr.Literal literal => literal.Value,
        _ => "<expression>",
    };

    // C-122 (error) — CROSS-FILE (FI-09). A step's own maximum-dwell timer has a specific shape
    // (docs/06 C-122): (a) IN gated by `Step = <n>` so it self-resets the instant the step changes;
    // (b) it lives multi-instance in the owning block's own Static (per C-407), never in DB_Timers;
    // (c) its PT comes from a settings member of the block's own interface UDT (C-307 per-instance
    // scope) — DB_Settings only when the timing is genuinely plant-wide.
    //
    // SCOPE (conservative, to avoid false positives on real steppers): only timers that look like
    // step-dwell timers are subjects — a timer whose IN is gated by ANY Step comparison
    // (ExprHasStepGuard, the same signal C-121 keys off). A timer with no Step relationship in its
    // IN (e.g. a chained/derived timer fed by another timer's Q — FB_ShredderSequencer's own
    // UpstreamEnableTimer) is NOT a C-122 subject and is skipped entirely. This is what keeps a
    // legitimate non-dwell timer inside a stepper block from being flagged.
    //
    // Part (c) resolves the referenced interface UDT via the --project index, so — exactly like
    // C-118 — the whole rule is recorded NotApplicable when no index is supplied (ReviewRunner
    // gates it). The "Q always drives a fault (C-123)" clause is a judgment call (fault-bit
    // identification) and is deliberately NOT mechanized.
    public static IEnumerable<Finding> CheckC122DwellTimerShape(IrBlock block, TagTypeRegistry udtIndex)
    {
        var interfaceMembers = TopLevelInterfaceMembers(block).ToList();

        foreach (var network in block.Networks)
        {
            foreach (var timer in network.Timers)
            {
                // Subject test: only a Step-gated timer is a step-dwell timer.
                if (!ExprHasStepGuard(timer.In))
                {
                    continue;
                }

                // (a) IN must carry a specific-step `Step = <n>` equality gate. A subject gated only
                // by a non-equality Step comparison (e.g. `Step >= 30`, or `Step = <a variable>`)
                // can't self-reset per step the way C-122 requires.
                if (!HasStepEqualityGuard(timer.In))
                {
                    yield return new Finding(
                        "C-122",
                        FindingSeverity.Error,
                        block.Name,
                        network.Number,
                        $"Step-dwell timer '{timer.InstancePath}' (network {network.Number}) has no `Step = <n>` equality gate on its IN — C-122 requires IN gated by `Step = <that step>` so the timer self-resets the instant the step changes.",
                        "Gate the timer's IN with `Step = <that step>` (an equality against the specific step number), not a range or other Step comparison.");
                }

                // (b) Multi-instance in the block's own Static — never DB_Timers.
                var instanceRoot = StripSubscriptComponent(timer.InstancePath.Split('.')[0]);
                if (string.Equals(instanceRoot, "DB_Timers", StringComparison.Ordinal))
                {
                    yield return new Finding(
                        "C-122",
                        FindingSeverity.Error,
                        block.Name,
                        network.Number,
                        $"Step-dwell timer '{timer.InstancePath}' (network {network.Number}) is instanced in DB_Timers — C-122/C-407 require a step-dwell timer to be multi-instance in the owning block's own Static section (it belongs to this instance, not a shared DB_Timers).",
                        "Declare the timer as a multi-instance member in the block's Static section instead of in DB_Timers.");
                }

                // (c) PT from a settings member of the block's own interface UDT. Only checkable when
                // PT is a tag reference. A PT whose root is a top-level interface member typed as a
                // UDT the index knows is the correct per-instance home — clean. A bare block-local
                // converted setpoint (a Static member whose type is neither a UDT nor a known DB —
                // the accepted MUL+CONVERT ms-idiom in FB_ShredderSequencer) is NOT flagged
                // (conservative). A PT sourced directly from a DB is surfaced as a candidate: C-122
                // wants the per-instance PT to be a UDT settings member, and a genuinely plant-wide
                // DB_Settings timing is the documented judgment exception.
                if (timer.Pt is Expr.TagRef ptRef)
                {
                    var ptRoot = StripSubscriptComponent(ptRef.Path.Split('.')[0]);
                    var ptRootMember = interfaceMembers.FirstOrDefault(m => string.Equals(m.Name, ptRoot, StringComparison.Ordinal));
                    var isUdtSettingsMember = ptRootMember is not null && udtIndex.TryGetUdt(ptRootMember.Datatype.Trim('"'), out _);

                    if (!isUdtSettingsMember && udtIndex.IsKnownDb(ptRoot))
                    {
                        yield return new Finding(
                            "C-122",
                            FindingSeverity.Error,
                            block.Name,
                            network.Number,
                            $"Step-dwell timer '{timer.InstancePath}' (network {network.Number}) takes its PT '{ptRef.Path}' from DB '{ptRoot}', not a settings member of the block's own interface UDT — C-122 wants the PT per-instance (C-307). If this timing is genuinely plant-wide with no single owning block, DB_Settings is the documented exception — confirm.",
                            "Source the PT from a settings member of the block's interface UDT (per-instance), or confirm the timing is genuinely plant-wide (the DB_Settings exception).");
                    }
                }
            }
        }
    }

    // C-125 (warn) — CROSS-FILE (FI-09). A dwell-timeout timer's own fault bit lives in the block's
    // caller-visible interface UDT, HMI-exposed (docs/06 C-125: "HMI exposure is satisfied by
    // C-118's placement plus … any C-122 timeout's own fault bit living in the same interface UDT,
    // not a private Static"). Resolving the fault bit's home needs the referenced interface UDT, so
    // — exactly like C-118/C-122 — the whole rule is recorded NotApplicable when no --project index
    // is supplied (ReviewRunner gates it).
    //
    // CONTRAPOSITIVE framing, to avoid false positives: rather than asserting "every dwell timer
    // must drive a fault in the UDT" (which would wrongly flag a dwell timer whose Q drives a
    // legitimate HOLD, not a fault — e.g. FB_PusherControl's PressureConfirmTimer.Q → PressureHold),
    // this identifies bits ALREADY provably timeout faults, then checks ONLY their home. A coil is a
    // timeout-fault bit when ALL of:
    //   (1) its Condition reads `<T>.Q` for a C-122-subject timer <T> — a timer whose IN carries a
    //       Step comparison (the same ExprHasStepGuard subject test C-122 uses to pick dwell timers);
    //   (2) its CoilTag leaf name ends with `Fault`;
    //   (3) it is a self-latch cleared by `NOT <...>FaultReset` — its Condition references its own
    //       CoilTag AND references a `FaultReset` tag beneath a NOT (the C-123 clear discipline).
    // A HOLD bit (no `Fault` in its name, cleared by another timer's Q rather than FaultReset) is
    // never a subject, so PressureHold is correctly not flagged.
    public static IEnumerable<Finding> CheckC125TimeoutFaultInInterfaceUdt(IrBlock block, TagTypeRegistry udtIndex)
    {
        // C-122 subject timers' `.Q` read paths — what a timeout-fault coil latches off.
        var subjectTimerQPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var network in block.Networks)
        {
            foreach (var timer in network.Timers)
            {
                if (ExprHasStepGuard(timer.In))
                {
                    subjectTimerQPaths.Add($"{timer.InstancePath}.Q");
                }
            }
        }

        if (subjectTimerQPaths.Count == 0)
        {
            yield break;
        }

        var interfaceMembers = TopLevelInterfaceMembers(block).ToList();

        foreach (var network in block.Networks)
        {
            foreach (var assignment in network.Assignments)
            {
                if (!IsTimeoutFaultCoil(assignment, subjectTimerQPaths))
                {
                    continue;
                }

                // Home resolution mirrors C-118 exactly: a `Root.Fault` whose Root is a UDT-typed
                // interface member is the correct HMI-exposed home (clean); anything else — a bare
                // block-local `Fault`, a DB member, or an unresolvable root — is the C-125 candidate.
                if (FaultBitHomeIsInterfaceUdt(assignment.CoilTag, interfaceMembers, udtIndex))
                {
                    continue;
                }

                yield return new Finding(
                    "C-125",
                    FindingSeverity.Warn,
                    block.Name,
                    network.Number,
                    $"Timeout-fault bit '{assignment.CoilTag}' (network {network.Number}) appears to live in a private Static/DB rather than the block's interface UDT — C-125 wants a C-122 dwell-timeout timer's own fault bit HMI-exposed as a member of the interface UDT (per C-118's placement), not a bare private Static or DB member. Confirm.",
                    "Declare this timeout-fault bit as a member of the block's caller-visible interface UDT and reference it through the UDT-typed interface member (e.g. `IO.<...>Fault`), so its HMI exposure is satisfied per C-125.");
            }
        }
    }

    // A coil is a timeout-fault bit per C-125's three conditions (see CheckC125 doc comment).
    private static bool IsTimeoutFaultCoil(CoilAssignment assignment, IReadOnlySet<string> subjectTimerQPaths)
    {
        // (2) CoilTag leaf name ends with "Fault".
        if (!LeafEndsWith(assignment.CoilTag, "Fault"))
        {
            return false;
        }

        var conditionTags = CollectTagRefPaths(assignment.Condition).ToList();

        // (1) Condition reads a C-122-subject timer's `.Q`.
        if (!conditionTags.Any(subjectTimerQPaths.Contains))
        {
            return false;
        }

        // (3a) Self-latch: Condition references its own CoilTag.
        if (!conditionTags.Contains(assignment.CoilTag, StringComparer.Ordinal))
        {
            return false;
        }

        // (3b) Cleared by `NOT <...>FaultReset`.
        return ExprHasNegatedFaultReset(assignment.Condition);
    }

    // True when the fault bit's home is a top-level interface member typed as a UDT the index knows
    // — the C-125 clean case. A bare block-local leaf, a DB member, or an unresolvable root is not.
    private static bool FaultBitHomeIsInterfaceUdt(string coilTag, IReadOnlyList<DbMember> interfaceMembers, TagTypeRegistry udtIndex)
    {
        var components = coilTag.Split('.');
        if (components.Length == 1)
        {
            return false; // bare block-local (private Static), not the interface UDT
        }

        var root = StripSubscriptComponent(components[0]);
        var rootMember = interfaceMembers.FirstOrDefault(m => string.Equals(m.Name, root, StringComparison.Ordinal));
        return rootMember is not null && udtIndex.TryGetUdt(rootMember.Datatype.Trim('"'), out _);
    }

    // The last dot-separated component's own tail — used to test a CoilTag leaf against "Fault" and
    // a cleared-by tag against "FaultReset" without matching a same-named intermediate component.
    private static bool LeafEndsWith(string tag, string suffix)
    {
        var lastDot = tag.LastIndexOf('.');
        var leaf = lastDot < 0 ? tag : tag[(lastDot + 1)..];
        return leaf.EndsWith(suffix, StringComparison.Ordinal);
    }

    // Every TagRef path anywhere in an Expr tree (And/Or/Not/Compare recursion, same shape as
    // ExprHasStepGuard). Used by C-125 to test what a fault coil's Condition reads.
    private static IEnumerable<string> CollectTagRefPaths(Expr expr)
    {
        switch (expr)
        {
            case Expr.TagRef tagRef:
                yield return tagRef.Path;
                break;
            case Expr.And and:
                foreach (var operand in and.Operands)
                {
                    foreach (var path in CollectTagRefPaths(operand))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Or or:
                foreach (var operand in or.Operands)
                {
                    foreach (var path in CollectTagRefPaths(operand))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Not not:
                foreach (var path in CollectTagRefPaths(not.Operand))
                {
                    yield return path;
                }

                break;
            case Expr.Compare compare:
                foreach (var path in CollectTagRefPaths(compare.Left))
                {
                    yield return path;
                }

                foreach (var path in CollectTagRefPaths(compare.Right))
                {
                    yield return path;
                }

                break;
        }
    }

    // True when the Expr tree contains a NOT whose operand subtree references a `<...>FaultReset`
    // tag — C-123's clear discipline, the third signal that a coil is a genuine timeout-fault latch.
    private static bool ExprHasNegatedFaultReset(Expr expr)
    {
        switch (expr)
        {
            case Expr.Not not:
                return CollectTagRefPaths(not.Operand).Any(p => LeafEndsWith(p, "FaultReset"))
                    || ExprHasNegatedFaultReset(not.Operand);
            case Expr.And and:
                return and.Operands.Any(ExprHasNegatedFaultReset);
            case Expr.Or or:
                return or.Operands.Any(ExprHasNegatedFaultReset);
            case Expr.Compare compare:
                return ExprHasNegatedFaultReset(compare.Left) || ExprHasNegatedFaultReset(compare.Right);
            default:
                return false;
        }
    }

    // The block's step-number set — the union of (1) every literal MOVEd into a Step register (a
    // transition's target step) and (2) every literal compared against a Step register inside a
    // transition MOVE's EN or a timer's IN (a transition's from-step / a dwell gate). Step literals
    // are NOT in TagReferences.AllTagPaths (it excludes Expr.Literal), so they are collected here
    // directly from those two shapes. Used by C-119 (is 0 present) and C-120 (all multiples of 10).
    private static IReadOnlySet<int> CollectBlockStepNumbers(IrBlock block)
    {
        var steps = new HashSet<int>();

        foreach (var network in block.Networks)
        {
            // Shape 1: MOVE(..., IN := <literal>) => <Step register>.
            foreach (var move in network.Moves)
            {
                if (HasStepLeaf(move.DestTag) && move.In is Expr.Literal lit && TryParseStep(lit.Value, out var target))
                {
                    steps.Add(target);
                }
            }

            // Shape 2: `Step <op> <literal>` comparisons in transition ENs and timer INs.
            foreach (var move in network.Moves)
            {
                foreach (var n in StepComparisonLiterals(move.En, equalityOnly: false))
                {
                    steps.Add(n);
                }
            }

            foreach (var timer in network.Timers)
            {
                foreach (var n in StepComparisonLiterals(timer.In, equalityOnly: false))
                {
                    steps.Add(n);
                }
            }
        }

        return steps;
    }

    // True when the Expr tree contains a `Step = <literal>` equality guard (C-122's per-step
    // self-reset signal) — the equality-and-literal-only counterpart of ExprHasStepGuard, which
    // accepts any Step comparison.
    private static bool HasStepEqualityGuard(Expr expr) => StepComparisonLiterals(expr, equalityOnly: true).Any();

    // Every literal step number that appears in a `Step <op> <literal>` comparison anywhere in an
    // Expr tree. Mirrors ExprHasStepGuard's And/Or/Not/Compare traversal but returns the literal
    // ints instead of a bool. equalityOnly restricts to the `=` operator (C-122's specific-step
    // gate); false accepts any comparison operator (the C-119/C-120 step-number census).
    private static IEnumerable<int> StepComparisonLiterals(Expr expr, bool equalityOnly)
    {
        switch (expr)
        {
            case Expr.Compare compare:
                if (!equalityOnly || compare.Operator == "=")
                {
                    if (IsStepTagRef(compare.Left) && compare.Right is Expr.Literal rl && TryParseStep(rl.Value, out var r))
                    {
                        yield return r;
                    }

                    if (IsStepTagRef(compare.Right) && compare.Left is Expr.Literal ll && TryParseStep(ll.Value, out var l))
                    {
                        yield return l;
                    }
                }

                foreach (var n in StepComparisonLiterals(compare.Left, equalityOnly))
                {
                    yield return n;
                }

                foreach (var n in StepComparisonLiterals(compare.Right, equalityOnly))
                {
                    yield return n;
                }

                break;
            case Expr.And and:
                foreach (var operand in and.Operands)
                {
                    foreach (var n in StepComparisonLiterals(operand, equalityOnly))
                    {
                        yield return n;
                    }
                }

                break;
            case Expr.Or or:
                foreach (var operand in or.Operands)
                {
                    foreach (var n in StepComparisonLiterals(operand, equalityOnly))
                    {
                        yield return n;
                    }
                }

                break;
            case Expr.Not not:
                foreach (var n in StepComparisonLiterals(not.Operand, equalityOnly))
                {
                    yield return n;
                }

                break;
        }
    }

    private static bool TryParseStep(string value, out int result) =>
        int.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result);

    // Every top-level (non-nested) interface member across all sections — the candidates for the
    // UDT-typed member C-118 resolves the interface UDT through.
    private static IEnumerable<DbMember> TopLevelInterfaceMembers(IrBlock block) =>
        (block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers)
            .Concat(block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(block.TempMembers)
            .Concat(block.ConstantMembers ?? Array.Empty<DbMember>());

    private static string StripSubscriptComponent(string component)
    {
        var idx = component.IndexOf('[');
        return idx < 0 ? component : component[..idx];
    }
}
