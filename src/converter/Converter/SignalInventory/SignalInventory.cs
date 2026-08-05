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
    string? Container = null)
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

    public IReadOnlyList<SignalLeaf> Leaves => _leaves;

    public IReadOnlyList<string> Warnings => _warnings;

    // How many files the inventory actually walked — the DENOMINATOR every absence claim must state.
    // A partial export makes "no signal matches" a scope fact, not a finding.
    public int FilesScanned { get; private set; }

    public static SignalInventory Build(string projectDir)
    {
        var inventory = new SignalInventory();

        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly))
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
                return; // a UDT is a shape, not a signal; its leaves surface through the DBs using it
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

    // Same leaf recursion as ProjectUsageGraph.CollectLeafPaths, keeping the type/RETAIN/start value.
    private void CollectLeaves(string prefix, DbMember member, SignalOrigin origin)
    {
        var path = prefix + "." + member.Name;

        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectLeaves(path, child, origin);
            }

            return;
        }

        _leaves.Add(new SignalLeaf(path, prefix.Split('.')[0], member.Name, member.Datatype,
            member.Retain, origin, member.StartValue));
    }
}
