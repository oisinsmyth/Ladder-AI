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

        foreach (var network in block.Networks)
        {
            var sliceWrites = SliceAccessWritesInNetwork(network).ToList();
            if (sliceWrites.Count == 0)
            {
                continue;
            }

            var satisfiesAlarmWordException = sliceWrites.Count == 1 && !string.IsNullOrEmpty(network.Title);
            if (satisfiesAlarmWordException)
            {
                continue;
            }

            yield return new Finding(
                "C-301",
                FindingSeverity.Error,
                block.Name,
                network.Number,
                $"Network {network.Number} writes {sliceWrites.Count} slice-access bit(s) ({string.Join(", ", sliceWrites)}) without satisfying the C-501 alarm-word exception (exactly one bit, network titled).",
                sliceWrites.Count > 1
                    ? "Split into one network per alarm bit, each titled with its own alarm text, or move this logic into a self-identified data-handling/comms block (C-105)."
                    : "Add a network title stating the alarm text, or move this logic into a self-identified data-handling/comms block (C-105).");

            yield return new Finding(
                "C-501",
                FindingSeverity.Warn,
                block.Name,
                network.Number,
                $"Network {network.Number}'s slice-access alarm bit(s) don't satisfy C-501's own conditions (exactly one bit per network, network title states the alarm text).",
                "Either restructure to satisfy C-501 as written, or — if this packed/summarized form is intentionally fine — propose it as a documented exception in docs/06-lad-conventions.md rather than leaving the rule and the practice disagreeing.");
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

    private static bool IsSliceAccessTag(string tag)
    {
        var lastDot = tag.LastIndexOf('.');
        return lastDot >= 0 && lastDot + 1 < tag.Length && tag[lastDot + 1] == '%';
    }

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

        foreach (var (tag, networkNumber) in resetTargets)
        {
            if (!setSeen.Contains(tag))
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
