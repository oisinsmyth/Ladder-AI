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
        string oldPath, string newPath, IReadOnlyList<int> onlyNetworks, bool allowHeaderChange = false)
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
            HeaderChangeAllowed: allowHeaderChange);
    }

    // Parse whether or not the input carries a SIDECAR (same branch review/preflight use). A sidecar-less
    // block has no round-trip data, but diff never needs it — the per-network compare and the interface
    // canonical are both sidecar-free (SerializeBlock's INTERFACE portion doesn't touch the sidecars).
    private static (IrBlock Block, IReadOnlyList<NetworkSidecar> Sidecars) ParseEither(string text) =>
        IrParser.HasSidecarSection(text)
            ? IrParser.ParseBlock(text)
            : (IrParser.ParseBlockWithoutSidecar(text), Array.Empty<NetworkSidecar>());

    private static IReadOnlyList<NetworkDiff> DiffNetworks(IrBlock oldBlock, IrBlock newBlock)
    {
        // Networks are matched by Number — S7 edits in place, so numbering is stable. A wholesale
        // renumbering would misreport (a known, documented limitation, not handled here).
        var oldByNumber = oldBlock.Networks.ToDictionary(n => n.Number);
        var newByNumber = newBlock.Networks.ToDictionary(n => n.Number);

        var results = new List<NetworkDiff>();
        foreach (var number in oldByNumber.Keys.Union(newByNumber.Keys).OrderBy(k => k))
        {
            var hasOld = oldByNumber.TryGetValue(number, out var oldNet);
            var hasNew = newByNumber.TryGetValue(number, out var newNet);

            if (hasOld && hasNew)
            {
                var oldText = IrSerializer.SerializeNetworkOnly(oldNet!);
                var newText = IrSerializer.SerializeNetworkOnly(newNet!);
                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                {
                    results.Add(new NetworkDiff(number, NetworkChangeKind.Identical, newNet!.Title, null, null));
                }
                else
                {
                    results.Add(new NetworkDiff(number, NetworkChangeKind.Changed, newNet!.Title, oldText, newText));
                }
            }
            else if (hasOld)
            {
                results.Add(new NetworkDiff(number, NetworkChangeKind.Removed, oldNet!.Title,
                    IrSerializer.SerializeNetworkOnly(oldNet!), null));
            }
            else
            {
                results.Add(new NetworkDiff(number, NetworkChangeKind.Added, newNet!.Title,
                    null, IrSerializer.SerializeNetworkOnly(newNet!)));
            }
        }

        return results;
    }

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
