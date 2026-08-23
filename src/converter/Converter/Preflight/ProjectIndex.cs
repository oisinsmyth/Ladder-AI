using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Preflight;

// Name index over a project's exported IR directory (ir/<project>/), plus the batch under
// pre-flight itself (so a new DB and the block referencing it can be checked together before
// either exists in the project). Same .ir prefix dispatch as ReviewRunner/DigestBuilder.
public sealed class ProjectIndex
{
    private readonly HashSet<string> _tagNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dbNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _typeNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockNames = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = new();

    // FI-65 component 1. Numbers and network slots are author-allocated in the IR (`NUMBER 50` is a
    // line an agent writes), so they are the two things two agents pick independently and collide on.
    // Recorded here rather than by a second scanner because this class already parses every block and
    // DB to get their names — a separate walk would be a second corpus dispatch, and SignalInventory's
    // own header states the invariant that the corpus is read one way "so the three can never disagree
    // about what a corpus contains".
    private readonly Dictionary<(string Kind, int Number), string> _numberOwners = new();
    private readonly Dictionary<string, IReadOnlyList<int>> _blockNetworks = new(StringComparer.Ordinal);

    // 2026-08-14. The two relations an EXCLUSIVE edit claim has to reason about, because editing one
    // object silently changes another: a UDT is edited and every object declaring a member of that
    // type moves with it; an FB is edited and its instance DBs' interfaces move with it. Both are
    // invisible to the filesystem — two agents take two different names and neither is refused.
    private readonly Dictionary<string, HashSet<string>> _typeUsers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _instanceOf = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Warnings => _warnings;

    // Keyed on (kind, number) because the number spaces are per-kind: FB50 and DB50 coexist, FB50 and
    // a second FB50 do not.
    public string? NumberOwner(string kind, int number) =>
        _numberOwners.TryGetValue((kind, number), out var name) ? name : null;

    public IReadOnlyList<int> NetworksOf(string blockName) =>
        _blockNetworks.TryGetValue(blockName, out var networks) ? networks : Array.Empty<int>();

    // FI-44 — "empty is not clean". A caller that validates against this index needs to distinguish
    // "checked the corpus, the resource is free" from "the corpus was empty, so everything looks free".
    // The second is not an answer, and treating it as one is how a check passes by examining nothing.
    public bool IndexedAnything =>
        _blockNames.Count + _dbNames.Count + _typeNames.Count + _tagNames.Count > 0;

    // THE DENOMINATOR, per name-set (2026-08-23). `IndexedAnything` above has said "the corpus was
    // not empty" since FI-44 and answers only the boolean; a caller that prints a finding worded
    // "does not resolve to any block in the project" needs the SIZE of the set that sentence is
    // falsifiable against, and the four name-sets are four different sizes deciding four different
    // finding classes. They were private with no accessors from the day this class was written.
    //
    // Each is the MERGED count — project ∪ batch — because that is what the resolution actually
    // consulted. The project/batch split is carried separately, by the two file counts below.

    /// <summary>Names <see cref="ResolvesAsBlock"/> can say yes to — the denominator of a `call` or
    /// `instanceof` finding, and of nothing else.</summary>
    public int BlockNameCount => _blockNames.Count;

    /// <summary>Names <see cref="ResolvesAsTagRoot"/> can say yes to (tags ∪ DBs) — the denominator
    /// of a `tag` ROOT finding. Not the member-path findings: those never touch this class.</summary>
    public int TagRootNameCount => _tagNames.Count + _dbNames.Count;

    /// <summary>Names <see cref="ResolvesAsType"/> can say yes to.</summary>
    public int TypeNameCount => _typeNames.Count;

    /// <summary>.ir files enumerated from <c>--project</c>, parseable or not: a file that failed to
    /// index still got walked, and is separately surfaced as an INDEX WARNING.</summary>
    public int ProjectFileCount { get; private set; }

    /// <summary>.ir files supplied as the batch under pre-flight.</summary>
    public int BatchFileCount { get; private set; }

    public static ProjectIndex Build(string projectDir, IReadOnlyList<string> batchPaths)
    {
        var index = new ProjectIndex();

        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly))
        {
            index.ProjectFileCount++;
            index.AddFile(path);
        }

        // The batch itself counts: a new block plus its new DB/tag table are pre-flighted as a
        // set, the same way they'd be imported as a set.
        foreach (var path in batchPaths)
        {
            index.BatchFileCount++;
            index.AddFile(path);
        }

        return index;
    }

    public bool ResolvesAsTagRoot(string root) =>
        _tagNames.Contains(root) || _dbNames.Contains(root);

    public bool ResolvesAsBlock(string name) => _blockNames.Contains(name);

    public bool ResolvesAsType(string name) => _typeNames.Contains(name);

    /// <summary>
    /// A DB by its own name. Distinct from <see cref="ResolvesAsTagRoot"/>, which is tags ∪ DBs: a
    /// tag is addressable but is not an editable OBJECT, and conflating them would let an exclusive
    /// edit claim be taken on a tag name.
    /// </summary>
    public bool ResolvesAsDb(string name) => _dbNames.Contains(name);

    /// <summary>
    /// Objects that DECLARE a member of the named type — the blast radius of editing a UDT.
    ///
    /// <para>Recorded here because this class already parses every block and DB to get their names; a
    /// separate walk would be a second corpus dispatch, and this file's own header states the
    /// invariant that the corpus is read one way "so the three can never disagree about what a corpus
    /// contains".</para>
    /// </summary>
    public IReadOnlyCollection<string> DeclarersOfType(string typeName) =>
        _typeUsers.TryGetValue(typeName, out var users) ? users : Array.Empty<string>();

    /// <summary>Instance DB name → the FB it instantiates. Editing the FB changes the iDB's interface.</summary>
    public IReadOnlyDictionary<string, string> InstanceOf => _instanceOf;

    private void AddFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                var db = DbIrParser.ParseDb(text);
                _dbNames.Add(db.Name);
                _numberOwners[("DB", db.Number)] = db.Name;
                if (db.InstanceOfName is { } instantiated)
                {
                    _instanceOf[db.Name] = Unquote(instantiated);
                }

                RecordTypeUsers(db.Name, db.Members);
                RecordTypeUsers(db.Name, db.InputMembers);
                RecordTypeUsers(db.Name, db.OutputMembers);
                RecordTypeUsers(db.Name, db.InOutMembers);
                return;
            }

            if (text.StartsWith("TYPE ", StringComparison.Ordinal))
            {
                _typeNames.Add(TypeIrParser.ParseType(text).Name);
                return;
            }

            if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                foreach (var tag in TagTableIrParser.ParseTagTable(text).Tags)
                {
                    _tagNames.Add(tag.Name);
                }

                return;
            }

            var block = HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
            _blockNames.Add(block.Name);
            _numberOwners[(block.Kind, block.Number)] = block.Name;
            _blockNetworks[block.Name] = block.Networks.Select(n => n.Number).ToList();
            RecordTypeUsers(block.Name, block.InputMembers);
            RecordTypeUsers(block.Name, block.OutputMembers);
            RecordTypeUsers(block.Name, block.InOutMembers);
            RecordTypeUsers(block.Name, block.StaticMembers);
            RecordTypeUsers(block.Name, block.TempMembers);
            RecordTypeUsers(block.Name, block.ConstantMembers);
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
        {
            _warnings.Add($"{path}: not indexed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    // Recurses nested members: a UDT reached three structs down is still edited by whoever edits it.
    private void RecordTypeUsers(string owner, IReadOnlyList<DbMember>? members)
    {
        foreach (var member in members ?? Array.Empty<DbMember>())
        {
            var type = Unquote(member.Datatype);
            if (type.Length > 0)
            {
                if (!_typeUsers.TryGetValue(type, out var users))
                {
                    users = new HashSet<string>(StringComparer.Ordinal);
                    _typeUsers[type] = users;
                }

                users.Add(owner);
            }

            RecordTypeUsers(owner, member.NestedMembers);
        }
    }

    // A UDT-typed member is written `IO : "UDT_PusherIO"`; an elementary one is bare. The quotes are
    // syntax, not part of the name, so they are stripped before the name is used as a key.
    private static string Unquote(string? value) => (value ?? string.Empty).Trim().Trim('"');

    internal static bool HasSidecarSection(string text) => IrParser.HasSidecarSection(text);
}
