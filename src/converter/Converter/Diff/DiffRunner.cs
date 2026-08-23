using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Diff;

// Compares the before/after IR of one block at network granularity. See DiffModel for the semantic-
// equality definition (sidecar-free readable form). Either input may carry a real SIDECAR (a TIA export
// — the S7 shape) or be sidecar-less (freshly authored / a validation-corpus block): the comparison is
// sidecar-free either way, so a sidecar-less as-built diffs against a sidecar-carrying fixed block (or
// vice versa) fine. Found needed 2026-07-18 in the gen-block-modify-fix validation, where the committed
// as-built corpus was sidecar-less.
public static class DiffRunner
{
    public static DiffReport Run(
        string oldPath, string newPath, IReadOnlyList<int> onlyNetworks, bool allowHeaderChange = false,
        int? declaredInsertAt = null)
    {
        var (oldBlock, oldSidecars) = ParseEither(File.ReadAllText(oldPath));
        var (newBlock, newSidecars) = ParseEither(File.ReadAllText(newPath));

        var header = DiffHeader(oldBlock, oldSidecars, newBlock, newSidecars);
        var networks = DiffNetworks(oldBlock, newBlock);

        return new DiffReport(
            BlockName: newBlock.Name,
            BlockNameMismatch: !string.Equals(oldBlock.Name, newBlock.Name, StringComparison.Ordinal),
            OtherBlockName: oldBlock.Name,
            Header: header,
            Networks: networks,
            AllowedNetworks: onlyNetworks,
            HeaderChangeAllowed: allowHeaderChange,
            DeclaredInsertAt: declaredInsertAt);
    }

    // Parse whether or not the input carries a SIDECAR (same branch review/preflight use). A sidecar-less
    // block has no round-trip data, but diff never needs it — the per-network compare and the interface
    // canonical are both sidecar-free (SerializeBlock's INTERFACE portion doesn't touch the sidecars).
    private static (IrBlock Block, IReadOnlyList<NetworkSidecar> Sidecars) ParseEither(string text) =>
        IrParser.HasSidecarSection(text)
            ? IrParser.ParseBlock(text)
            : (IrParser.ParseBlockWithoutSidecar(text), Array.Empty<NetworkSidecar>());

    // 🔴 *** MATCHED ON CONTENT FIRST, THEN ON NUMBER — AND MATCHING ONLY ON NUMBER WAS A CLOSED CHECK.
    // ***
    //
    // This used to pair networks purely by Number, with a comment calling wholesale renumbering "a known,
    // documented limitation". The limitation is not exotic: INSERT ONE NETWORK at 11 in a 20-network block
    // and 11..20 all shift, so the report reads "10 changed, 1 added" and `--only {11}` exits 1 naming
    // NINE NETWORKS NOBODY TOUCHED. This is the S7 modification gate, whose entire job is "prove the rest
    // is identical", and it was answering a question about numbers while claiming to answer one about
    // content. CLAUDE.md lists it as one of four measured instances of a check that is CLOSED — it examines
    // something real that is not the thing it claims, so it is never empty and never silent.
    //
    // *** THE ANCHOR IS UNIQUENESS, AND THE AMBIGUITY RULE IS THE WHOLE SAFETY ARGUMENT. *** Only content
    // appearing EXACTLY ONCE on each side is paired across numbers. Two networks with identical bodies —
    // which real ladder has, e.g. repeated per-device rungs — anchor to nothing and fall back to number
    // matching, because pairing them would be a guess. A content anchor that mis-paired duplicates would
    // be a NEW false green in a gate whose job is proving identity, which is a worse failure than the one
    // being fixed.
    //
    // Everything the old code did survives for the unchanged case: a network with the same content at the
    // same number anchors to itself and reports Identical, exactly as before.
    private static IReadOnlyList<NetworkDiff> DiffNetworks(IrBlock oldBlock, IrBlock newBlock)
    {
        var oldByNumber = oldBlock.Networks.ToDictionary(n => n.Number);
        var newByNumber = newBlock.Networks.ToDictionary(n => n.Number);

        // 🔴 TWO TEXTS PER NETWORK, AND THE DISTINCTION IS LOAD-BEARING.
        //
        // SerializeNetworkOnly's first line is `NETWORK <number> "<title>"`, so THE NUMBER IS PART OF THE
        // TEXT. Comparing that form across positions can never match — a moved network would always look
        // changed, which is precisely the old behaviour. So identity is compared on a number-free form,
        // and the real text is kept for display, where the reader wants the actual numbers.
        //
        // This changes nothing about the same-number case the old code handled: normalising a number that
        // is equal on both sides is a no-op, so an in-place edit still reports Changed and an untouched
        // network still reports Identical, byte for byte as before.
        var oldText = oldByNumber.ToDictionary(kv => kv.Key, kv => IrSerializer.SerializeNetworkOnly(kv.Value));
        var newText = newByNumber.ToDictionary(kv => kv.Key, kv => IrSerializer.SerializeNetworkOnly(kv.Value));

        var oldIdentity = oldByNumber.ToDictionary(kv => kv.Key, kv => Identity(kv.Value));
        var newIdentity = newByNumber.ToDictionary(kv => kv.Key, kv => Identity(kv.Value));

        // Content -> the single number holding it, for content held by exactly one network on that side.
        var oldUnique = Unique(oldIdentity);
        var newUnique = Unique(newIdentity);

        var results = new List<NetworkDiff>();
        var pairedOld = new HashSet<int>();
        var pairedNew = new HashSet<int>();

        foreach (var (content, oldNumber) in oldUnique)
        {
            if (!newUnique.TryGetValue(content, out var newNumber))
                continue;

            pairedOld.Add(oldNumber);
            pairedNew.Add(newNumber);

            results.Add(oldNumber == newNumber
                ? new NetworkDiff(newNumber, NetworkChangeKind.Identical, newByNumber[newNumber].Title, null, null)
                : new NetworkDiff(newNumber, NetworkChangeKind.Moved, newByNumber[newNumber].Title,
                    oldText[oldNumber], newText[newNumber], MovedFrom: oldNumber));
        }

        // Whatever did not anchor is matched by number, among the unpaired only — the old behaviour,
        // now applied to the residue instead of to everything.
        var residualOld = oldByNumber.Keys.Where(n => !pairedOld.Contains(n)).ToHashSet();
        var residualNew = newByNumber.Keys.Where(n => !pairedNew.Contains(n)).ToHashSet();

        foreach (var number in residualOld.Union(residualNew).OrderBy(k => k))
        {
            var hasOld = residualOld.Contains(number);
            var hasNew = residualNew.Contains(number);

            if (hasOld && hasNew)
            {
                // Still compared on CONTENT, not assumed different: two networks can share a body and
                // therefore fail to anchor while being perfectly identical in place.
                results.Add(string.Equals(oldIdentity[number], newIdentity[number], StringComparison.Ordinal)
                    ? new NetworkDiff(number, NetworkChangeKind.Identical, newByNumber[number].Title, null, null)
                    : new NetworkDiff(number, NetworkChangeKind.Changed, newByNumber[number].Title,
                        oldText[number], newText[number]));
            }
            else if (hasOld)
            {
                results.Add(new NetworkDiff(number, NetworkChangeKind.Removed, oldByNumber[number].Title,
                    oldText[number], null));
            }
            else
            {
                results.Add(new NetworkDiff(number, NetworkChangeKind.Added, newByNumber[number].Title,
                    null, newText[number]));
            }
        }

        return results.OrderBy(r => r.Number).ThenBy(r => r.Kind).ToList();
    }

    /// <summary>
    /// 🔴 <b>A network's content WITHOUT its position</b> — the thing two networks can share across a
    /// renumbering. Everything else about the network is kept, the TITLE included: a title is part of what
    /// a network is, and two rungs that differ only by title are different rungs.
    ///
    /// <para>Done by normalising the number on the model rather than by editing the serialized string,
    /// so it cannot be broken by a future change to the serializer's first line.</para>
    /// </summary>
    private static string Identity(IrNetwork network) =>
        IrSerializer.SerializeNetworkOnly(network with { Number = 0 });

    /// <summary>Content held by exactly ONE network on this side. Anything shared is not an anchor.</summary>
    private static Dictionary<string, int> Unique(Dictionary<int, string> byNumber) =>
        byNumber
            .GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Key, StringComparer.Ordinal);

    private static HeaderDiff DiffHeader(
        IrBlock oldBlock, IReadOnlyList<NetworkSidecar> oldSidecars,
        IrBlock newBlock, IReadOnlyList<NetworkSidecar> newSidecars)
    {
        var oldTitle = Normalize(oldBlock.Title);
        var newTitle = Normalize(newBlock.Title);
        var oldComment = Normalize(oldBlock.Comment);
        var newComment = Normalize(newBlock.Comment);

        // Two canonicals per side, and the pair is the whole point (2026-08-21). STRUCTURE is the
        // interface with every member COMMENT blanked — names, datatypes, RETAIN/SETPOINT, start
        // values, external-access flags, nesting. WHOLE additionally carries the comment text. A
        // difference in STRUCTURE gates; a difference visible only in WHOLE is documentation.
        var oldStructure = InterfaceCanonical(StripMemberComments(oldBlock), oldSidecars);
        var newStructure = InterfaceCanonical(StripMemberComments(newBlock), newSidecars);
        var oldWhole = InterfaceCanonical(oldBlock, oldSidecars);
        var newWhole = InterfaceCanonical(newBlock, newSidecars);

        var structureChanged = !string.Equals(oldStructure, newStructure, StringComparison.Ordinal);

        return new HeaderDiff(
            TitleChanged: !string.Equals(oldTitle, newTitle, StringComparison.Ordinal),
            TitleBefore: oldTitle,
            TitleAfter: newTitle,
            CommentChanged: !string.Equals(oldComment, newComment, StringComparison.Ordinal),
            CommentBefore: oldComment,
            CommentAfter: newComment,
            InterfaceChanged: structureChanged,
            // Deliberately NOT "whole differs" — that is true whenever the structure changed too, and
            // would report a comment edit on every retype. This is the carve-out only: the structure
            // is identical AND the text is not, i.e. nothing changed but the documentation.
            InterfaceCommentChanged: !structureChanged
                && !string.Equals(oldWhole, newWhole, StringComparison.Ordinal));
    }

    // A copy of the block whose interface members carry no COMMENT, recursively through nested
    // members. Model-level rather than text-level on purpose: stripping comment lines out of the
    // serialized form would be a parser guessing at its own output, and a member comment can carry
    // anything including text that looks like the grammar around it.
    //
    // ONLY `Comment` is blanked. Every other DbMember field stays in the comparison — Retain,
    // SetPoint, Datatype, StartValue, Subelements, the three External* flags, Informative and its
    // InformativeComment. That restraint is the point: DiffModel's own note says the dangerous
    // direction here is one careless generalisation, so "documentation" means the field that exists
    // to hold prose, not everything that looks descriptive. InformativeComment in particular is left
    // gating - it is tied to the Informative flag, and nobody has established it cannot matter.
    private static IrBlock StripMemberComments(IrBlock block) => block with
    {
        InputMembers = StripMemberComments(block.InputMembers),
        OutputMembers = StripMemberComments(block.OutputMembers),
        InOutMembers = StripMemberComments(block.InOutMembers) ?? Array.Empty<DbMember>(),
        StaticMembers = StripMemberComments(block.StaticMembers),
        TempMembers = StripMemberComments(block.TempMembers) ?? Array.Empty<DbMember>(),
        ConstantMembers = StripMemberComments(block.ConstantMembers),
    };

    // null maps to null, never to empty. A section that is absent from the source and a section that
    // is present-but-empty are a real, must-preserve distinction (IrSerializer.SerializeInterface),
    // and collapsing them here would make an interface change invisible to the gate.
    private static IReadOnlyList<DbMember>? StripMemberComments(IReadOnlyList<DbMember>? members) =>
        members?.Select(StripMemberComments).ToList();

    private static DbMember StripMemberComments(DbMember member) => member with
    {
        Comment = null,
        NestedMembers = StripMemberComments(member.NestedMembers),
    };

    // The block's INTERFACE section as sidecar-free, UId-free text, extracted from the canonical
    // whole-block serialization. Everything from "SIDECAR" onward (volatile UIds) and every network
    // is dropped; RootUId/Number/etc. header scalars are dropped too by keeping only the INTERFACE
    // slice. Empty string when the block has no interface (SerializeBlock omits the section).
    private static string InterfaceCanonical(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var full = IrSerializer.SerializeBlock(block, sidecars);
        var preSidecar = full.Split("\nSIDECAR\n", 2)[0];
        var headerAndInterface = preSidecar.Split("\nNETWORK ", 2)[0];
        var idx = headerAndInterface.IndexOf("\nINTERFACE\n", StringComparison.Ordinal);
        return idx < 0 ? string.Empty : headerAndInterface[idx..];
    }

    private static string? Normalize(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
