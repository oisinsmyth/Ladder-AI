using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.SignalInventory;

// Where a signal leaf came from. Kept because the candidate-set question ("what could satisfy this
// requirement?") ranges over field IO and an FB's own interface members, and the two are different
// kinds of answer.
public enum SignalOrigin
{
    GlobalDb,     // a member of a global data block (the IO buffers, settings, plant state)
    TagTable,     // a PLC tag-table entry (physical IO, system bits)
    FbInterface,  // a member of a block's own interface (Static/Input/Output/InOut)
}

// One resolved signal leaf. `Type` and `IsRetain` are exactly what ProjectUsageGraph.CollectLeafPaths
// throws away, and both are needed: type to say "same-typed" (the transposition signature), RETAIN as a
// role hint.
public sealed record SignalLeaf(
    string Path,
    string Root,
    string Leaf,
    string Type,
    bool IsRetain,
    SignalOrigin Origin,
    string? StartValue = null,
    string? Container = null,
    // ONE leaf standing for a whole `Array[…] of` member. Defaulted so no existing call site changes.
    // The array is not expanded — the element index would be lost, and "is element N unused" is a
    // different question with a different answer shape (ProjectUsageGraph.UsagesCovering says so in
    // its own words). A consumer that must know it is looking at an aggregate rather than a scalar
    // reads this rather than re-parsing the type string.
    bool IsAggregate = false)
{
    // The DECLARING container's name: the DB/block for a member, the TAG TABLE for a tag.
    //
    // A tag-table tag is the one leaf whose `Path` cannot carry its container: a tag is referenced
    // BARE everywhere in IR (`"MotorRun"`, never `IO_Tags.MotorRun`), so ProjectUsageGraph,
    // candidate-scan and undriven-scan all key on the bare name and the inventory must match them.
    // The container is therefore carried alongside rather than folded into the path — anything that
    // needs to GROUP or QUALIFY (signal-sweep's disposition headings) reads this, and anything that
    // needs to LOOK UP a reference keeps reading `Path`. For every other origin the two agree.
    public string ContainerName => Container ?? Root;
}

/// <summary>
/// 🔴 <b>A MEMBER WHOSE TYPE COULD NOT BE OPENED, SO ITS LEAVES ARE MISSING FROM AN INVENTORY THAT
/// OTHERWISE READS COMPLETE (FI-88).</b>
///
/// <para>Kept a SEPARATE type from <c>InterfaceCheck.OpaqueMember</c> on purpose. That one is keyed on
/// <c>SECTION/path/name</c> because its consumer is a set-difference report over member NAMES; this one
/// is keyed on the <b>dotted signal path a binding names</b> (<c>FB_X.IO</c>), because its consumer is a
/// generator that has to say which entry of a signal set it may not trust. Folding them would force one
/// of the two to carry a key its own reader cannot use.</para>
///
/// <para><c>Reason</c> states the search root, the file count and that the scan does not recurse, so
/// "the type is one directory down" is distinguishable from "the type does not exist".</para>
/// </summary>
public sealed record OpaqueLeaf(string Path, string Datatype, string Reason);

// A typed inventory of every signal leaf in a project export.
//
// Deliberately a SIBLING of ProjectUsageGraph rather than an extension of it: TraceRunner reads that
// graph's shape as-is and its own comment declares the verbatim keying deliberate, so widening it to
// carry types would put a well-tested structure at risk for an unrelated question. Same `.ir` prefix
// dispatch as ProjectIndex/ProjectUsageGraph, so the three can never disagree about what a corpus
// contains.
public sealed class SignalInventory
{
    private readonly List<SignalLeaf> _leaves = new();
    private readonly List<string> _warnings = new();
    private readonly List<OpaqueLeaf> _opaque = new();

    // Built ONCE, before any leaf is collected, so file order decides nothing about how a member is
    // classified. Both are needed by MemberExpansion: the registry to open a named type, the block-name
    // set to tell an FB instantiation from a UDT (their datatype strings are identical — both quoted).
    private TagTypeRegistry _types = TagTypeRegistry.Empty;
    private IReadOnlySet<string> _blockNames = new HashSet<string>(StringComparer.Ordinal);
    private string _searchScope = string.Empty;

    public IReadOnlyList<SignalLeaf> Leaves => _leaves;

    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// 🔴 <b>MEMBERS WHOSE TYPE COULD NOT BE OPENED — leaves this inventory is MISSING.</b> Not a
    /// warning: a warning says a FILE could not be read, this says a MEMBER's leaves are absent from a
    /// set that otherwise reads complete. Consumers that gate must read both.
    /// </summary>
    public IReadOnlyList<OpaqueLeaf> OpaqueLeaves => _opaque;

    // How many files the inventory actually walked — the DENOMINATOR every absence claim must state.
    // A partial export makes "no signal matches" a scope fact, not a finding.
    public int FilesScanned { get; private set; }

    public static SignalInventory Build(string projectDir)
    {
        var inventory = new SignalInventory();

        var files = Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // The type namespace and the block namespace are whole-corpus facts, so they are resolved
        // before the first member is looked at. Doing it lazily would make a member's classification
        // depend on whether its type's file happened to be read first.
        inventory._types = TagTypeRegistry.FromFiles(files);
        inventory._blockNames = MemberExpansion.BlockNamesFromHeaders(files);
        inventory._searchScope = MemberExpansion.DescribeSearchScope(projectDir, files.Count);

        foreach (var path in files)
        {
            inventory.AddFile(path);
        }

        return inventory;
    }

    private void AddFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            FilesScanned++;

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                var db = DbIrParser.ParseDb(text);

                // An instance DB's members belong to its FB's interface, not to a global data landscape;
                // classifying them as GlobalDb would put every instance's copy into an IO candidate set.
                var origin = db.InstanceOfName is null ? SignalOrigin.GlobalDb : SignalOrigin.FbInterface;
                foreach (var member in AllDbMembers(db))
                {
                    CollectLeaves(db.Name, member, origin);
                }

                return;
            }

            if (text.StartsWith("TYPE ", StringComparison.Ordinal))
            {
                // A UDT is a shape, not a signal — its leaves surface through the DBs and blocks using
                // it, so the parse result is DISCARDED. It is still PARSED, because until FI-88 this
                // returned early and "the type is absent from the corpus" and "the type is here and
                // broken" produced byte-identical output. They need different actions from whoever
                // reads the result, so the throw is allowed to reach the catch below and become a
                // warning.
                TypeIrParser.ParseType(text);
                return;
            }

            if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                var table = TagTableIrParser.ParseTagTable(text);
                foreach (var tag in table.Tags)
                {
                    // Path stays BARE — that is how a tag is written in IR and how the usage graph keys
                    // it. The table name rides in `Container` (see SignalLeaf) so a consumer that needs
                    // the qualifier has it without the lookup key changing meaning.
                    _leaves.Add(new SignalLeaf(tag.Name, tag.Name, tag.Name, tag.DataTypeName, false,
                        SignalOrigin.TagTable, StartValue: null, Container: table.Name));
                }

                return;
            }

            var block = IrParser.HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);

            foreach (var member in AllInterfaceMembers(block))
            {
                CollectLeaves(block.Name, member, SignalOrigin.FbInterface);
            }
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                       or NonReducibleNetworkException or IrFormatException)
        {
            // Same degrade-to-warning contract every scan honours: one unparseable file must not
            // collapse the inventory.
            _warnings.Add($"{path}: not inventoried ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static IEnumerable<DbMember> AllDbMembers(DbSource db) =>
        db.Members
            .Concat(db.InputMembers ?? Array.Empty<DbMember>())
            .Concat(db.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(db.InOutMembers);

    // Every declared interface member of a block. NOT filtered by section on purpose: the members a
    // candidate set cares about (an FB's `Outputs.FaultActive`) live under STATIC inside the site's
    // interface-UDT structs, while the SimaticML INPUT/OUTPUT sections carry unrelated data-link words.
    // Filtering by section gets the wrong answer on exactly the block the REQ-017 defect concerns —
    // direction is computed from the usage graph instead (see CandidateScanRunner).
    private static IEnumerable<DbMember> AllInterfaceMembers(IrBlock block) =>
        (block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers);

    // The leaf recursion, keeping the type/RETAIN/start value ProjectUsageGraph.CollectLeafPaths
    // throws away.
    //
    // 🔴 THE "HOW FAR DOES THIS OPEN" DECISION IS NOT MADE HERE ANY MORE — it is MemberExpansion's,
    // shared with every other walk that asks (FI-88). This method used to expand a member IFF its
    // sub-members were physically inlined in the block's own .ir, which is true of a file round-tripped
    // through TIA and false of one an authoring pipeline wrote. On a real program that made a UDT-typed
    // STATIC — the block's whole caller-visible interface, referenced 251 times — report as ONE leaf
    // with direction `unused`, no writers and no readers, at exit 0.
    private void CollectLeaves(string prefix, DbMember member, SignalOrigin origin, int depth = 0)
    {
        var path = prefix + "." + member.Name;
        var shape = MemberExpansion.Classify(member, _types, _blockNames, depth, _searchScope);

        if (shape.Recurses)
        {
            foreach (var child in shape.Children)
            {
                CollectLeaves(path, child, origin, depth + 1);
            }

            return;
        }

        if (shape.IsOpaque)
        {
            // Recorded AND the row still emitted. Dropping it would shorten the set silently, which is
            // the one outcome worse than an incomplete set: the missing entry is exactly the one
            // nothing else will mention.
            _opaque.Add(new OpaqueLeaf(path, member.Datatype, shape.OpaqueReason!));
        }

        _leaves.Add(new SignalLeaf(path, prefix.Split('.')[0], member.Name, member.Datatype,
            member.Retain, origin, member.StartValue, Container: null, IsAggregate: shape.IsAggregate));
    }
}
