using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.CrossCheck;

// FI-22: the whole-project reader/writer graph — the substrate the cross-check facts (and, later,
// FI-25's forward tracer) are computed over. Where ProjectIndex indexes names only, this walks every
// block's networks across the whole export (reusing the same *.ir prefix dispatch and the
// TagReferences.AllDirectedUsages per-network walk) and accumulates, per full dotted tag path, who
// writes it and who reads it — plus a global-DB member inventory for the dead-wiring pass. Derived
// fresh, never stored.
public sealed class ProjectUsageGraph
{
    // One occurrence of a tag being read or written, located to its block + network (+ Set/Reset kind).
    // Guard (FI-25 v2) is the write's guarding condition Expr (null for reads / ENO-chained writes), so a
    // consumer can tell an armed write from a disarmed one (e.g. `NOT AlwaysTrue`).
    public sealed record UsageSite(string Block, int Network, CoilKind? Kind, Expr? Guard = null);

    public sealed record PathUsage(List<UsageSite> Writers, List<UsageSite> Readers);

    private readonly Dictionary<string, PathUsage> _usages = new(StringComparer.Ordinal);
    private readonly List<string> _globalDbMemberPaths = new();
    private readonly List<(string Block, int Network, string Path, TagDirection Direction)> _flat = new();
    private readonly List<(string Block, string CalledBlock)> _calls = new();
    private readonly List<string> _warnings = new();

    // FI-22 follow-up: interface-UDT aliasing. An instance DB mirrors its FB's interface 1:1 and the
    // same member is addressed two ways — FB-internal bare (`IO.Step`, inside the FB's own logic) and
    // orchestrator-external (`iDB_X.IO.Step`, from callers). The verbatim-keyed `_usages`/
    // `GlobalDbMemberPaths` above are LEFT UNTOUCHED (FI-25's forward tracer reads them as-is); these
    // two parallel indexes carry the correlation the dead-wiring pass needs to collapse both forms.
    private readonly Dictionary<string, string> _instanceToFb = new(StringComparer.Ordinal);
    private readonly List<(string InstanceDb, string Suffix)> _instanceMemberPaths = new();

    // FI-50. A MULTI-INSTANCE is an FB instantiated as a STATIC member of another FB rather than as
    // its own instance DB — `ValveWater : "FB_Valve"` inside FB_SiloVessel. It is a real instance
    // with real per-instance state, and the indexes above cannot see it: they are built from DB
    // sources carrying an InstanceOf, and a multi-instance has no DB of its own.
    //
    // That blindness is not an edge case where C-132 is in force, because C-132 makes the
    // single-STATIC-UDT interface the house style — so a whole corpus can consist of nothing BUT
    // multi-instances, and a per-instance check over it examines zero instances while exiting
    // cleanly. Found on a live job whose every FB reported "no instances" (2026-08-07).
    //
    // KEPT DELIBERATELY SEPARATE from _instanceToFb / _instanceMemberPaths rather than merged into
    // them. Those two are keyed on an iDB NAME and CanonicalizeInstancePath splits a path on its
    // root against them; a synthetic dotted key like "FB_SiloVessel.ValveWater" would canonicalize
    // paths that no logic ever writes, silently changing cross-check and trace output. Additive
    // index, no existing consumer perturbed.
    private readonly Dictionary<string, string> _multiInstanceToFb = new(StringComparer.Ordinal);
    private readonly List<(string Instance, string Suffix)> _multiInstanceMemberPaths = new();
    private readonly Dictionary<string, (string OwnerFb, string LocalRoot)> _multiInstanceOrigin =
        new(StringComparer.Ordinal);

    // Collected during the file walk, resolved once every block name is known — a static's datatype
    // can name a block that has not been read yet.
    private readonly List<(string OwnerFb, DbMember Member)> _multiInstanceCandidates = new();

    // Every block's own declared interface. Needed because a multi-instance static is written BARE —
    // `ValveIntake : "FB_Valve"` with no members beneath it, unlike a UDT-typed static, which the IR
    // expands in place. So the members of a multi-instance have to be read off the instantiated
    // block's own declaration rather than off the declaration site.
    private readonly Dictionary<string, IReadOnlyList<DbMember>> _blockInterfaces =
        new(StringComparer.Ordinal);

    // 🔴 2026-08-14. EVERY ROOT NAME A BLOCK DECLARES FOR ITSELF — inputs, outputs, in-outs, statics,
    // temps and constants. This is the index that tells a BLOCK-LOCAL path from a GLOBAL one, and its
    // absence was a live defect that MANUFACTURED FALSE FINDINGS.
    //
    // `_usages` is keyed on the path VERBATIM, and an FB addresses its own interface member with no
    // root at all — `IO.Step`, `Time`. So three different FBs, each with its own `IO` member of its
    // own UDT type and its own `Time : Real`, all landed on ONE key and `cross-check` reported them
    // as CROSS-BLOCK MULTI-WRITERS. *** THEY ARE NOT THE SAME STORAGE AND THERE WAS NO CONFLICT. ***
    // Measured on `ir/test-project001`: of four multi-writer paths spanning more than one block, TWO
    // were entirely fictitious and two were real.
    //
    // A false finding is treated here as the equal of a false green — *the first one is what gets a
    // check switched off* — and this one was caught only because a lane refused to feed the output
    // into a gate it did not trust. Had it been trusted, fictitious conflict edges would have entered
    // a submission's conflict graph and separated slots that never conflicted.
    //
    // WHY A DECLARATION SET RATHER THAN A NAME HEURISTIC: the question "is this bare name the block's
    // own member or a global PLC tag?" cannot be answered from the name — `PressureTripCount` (an
    // FB static) and `Start_PB` (a tag-table tag) are both bare single-component references. It is
    // answered exactly by whether the referencing block DECLARES that root, which the IR states
    // outright.
    private readonly Dictionary<string, HashSet<string>> _blockLocalRoots = new(StringComparer.Ordinal);

    // FI-44: every block name the corpus actually contains. Exists so a check can tell
    // "this block is not here" from "this block is here and has nothing wrong with it" — the
    // difference between those two is the whole of FI-44, and no other index carries it. `_flat`
    // and `_calls` come close but only hold blocks that reference a tag or make a call, so a
    // block that does neither would read as absent.
    private readonly HashSet<string> _blockNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _organizationBlocks = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, PathUsage> Usages => _usages;
    public IReadOnlyList<string> GlobalDbMemberPaths => _globalDbMemberPaths;
    public IReadOnlyList<(string Block, int Network, string Path, TagDirection Direction)> Flat => _flat;
    public IReadOnlyList<(string Block, string CalledBlock)> Calls => _calls;
    public IReadOnlyList<string> Warnings => _warnings;

    // iDB name -> the FB it instantiates (one-to-one; the FB->iDB direction is one-to-many).
    public IReadOnlyDictionary<string, string> InstanceToFb => _instanceToFb;

    // FI-44. Read this before concluding a scan found nothing wrong.
    public IReadOnlyCollection<string> BlockNames => _blockNames;

    // Blocks reachable from an OB through the CALL graph — i.e. blocks that actually execute. Empty
    // when the corpus contains no OB at all (a partial export), and callers must treat that as
    // "unknown", never as "nothing executes": an absent root set cannot distinguish the two.
    public IReadOnlyCollection<string> ReachableFromAnOb
    {
        get
        {
            if (_organizationBlocks.Count == 0)
            {
                return Array.Empty<string>();
            }

            var reachable = new HashSet<string>(_organizationBlocks, StringComparer.Ordinal);
            var queue = new Queue<string>(_organizationBlocks);
            var callsFrom = _calls
                .GroupBy(c => c.Block, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(c => c.CalledBlock).ToList(), StringComparer.Ordinal);

            while (queue.Count > 0)
            {
                if (!callsFrom.TryGetValue(queue.Dequeue(), out var callees))
                {
                    continue;
                }

                foreach (var callee in callees.Where(c => reachable.Add(c)))
                {
                    queue.Enqueue(callee);
                }
            }

            return reachable;
        }
    }

    public IReadOnlyCollection<string> OrganizationBlocks => _organizationBlocks;

    // Every leaf interface member of every instance DB, as (iDB name, member suffix relative to the
    // iDB root) — e.g. ("iDB_ShredderSequencer", "IO.Step"), ("iDB_ShredderSequencer", "StopCmd").
    public IReadOnlyList<(string InstanceDb, string Suffix)> InstanceMemberPaths => _instanceMemberPaths;

    // FI-50. Multi-instance path -> the FB it instantiates. The path is the chain of static member
    // names from a root, e.g. "iDB_SiloW.ValveWater" where the owning FB has an instance DB, or
    // "FB_SiloVessel/ValveWater" where it does not yet (declaration-site form — see
    // MultiInstanceOrigin). Disjoint from InstanceToFb by construction.
    public IReadOnlyDictionary<string, string> MultiInstanceToFb => _multiInstanceToFb;

    public IReadOnlyList<(string Instance, string Suffix)> MultiInstanceMemberPaths => _multiInstanceMemberPaths;

    // For a multi-instance path: the block whose networks address it, and the LOCAL root those
    // networks use. Inside FB_SiloVessel the water valve's open command is written as
    // `ValveWater.IO.OpenCmd` — bare, with no instance root — so a usage lookup has to be made on
    // the local form and then restricted to the owning block, or two FBs that happen to share a
    // static name would pool each other's writers.
    public IReadOnlyDictionary<string, (string OwnerFb, string LocalRoot)> MultiInstanceOrigin =>
        _multiInstanceOrigin;

    public static ProjectUsageGraph Build(string projectDir) => Build(projectDir, Array.Empty<string>());

    // Additive overload mirroring ProjectIndex.Build(projectDir, batchPaths): extra .ir files outside the
    // export contribute their usages too, so a freshly-generated block can be analysed against the corpus
    // before it has been imported. Shapes and keying are untouched — existing callers are unaffected.
    public static ProjectUsageGraph Build(string projectDir, IReadOnlyList<string> extraFiles)
    {
        var graph = new ProjectUsageGraph();
        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            graph.AddFile(path);
        }

        foreach (var path in extraFiles)
        {
            graph.AddFile(path);
        }

        graph.ResolveMultiInstances();
        return graph;
    }

    // FI-50. Expand every multi-instance static into instance paths, rooted on a real instance DB
    // where the owning FB has one.
    //
    // ITERATED TO A FIXPOINT because multi-instances nest: FB_SiloSequence owns a FB_SiloCycle,
    // and if FB_SiloSequence is itself reached through an instance DB then the cycle's real path is
    // iDB_SeqW.Cycle. A single pass would root the cycle on the declaration site and lose the
    // per-instance resolution that is this whole index's reason to exist.
    private void ResolveMultiInstances()
    {
        for (var round = 0; round < MaxNestingDepth; round++)
        {
            var added = 0;
            foreach (var (ownerFb, member) in _multiInstanceCandidates)
            {
                var fbType = Unquote(member.Datatype);
                if (!_blockNames.Contains(fbType))
                {
                    continue; // a UDT or elementary type, not an FB instantiation
                }

                foreach (var root in RootsFor(ownerFb))
                {
                    // The declaration-site root already ends in its marker; a placement root joins
                    // with a dot like any other member path.
                    var instance = root.EndsWith(DeclarationSiteMarker, StringComparison.Ordinal)
                        ? root + member.Name
                        : root + "." + member.Name;
                    if (!_multiInstanceToFb.TryAdd(instance, fbType))
                    {
                        continue;
                    }

                    _multiInstanceOrigin[instance] = (ownerFb, member.Name);

                    // Members come from the INSTANTIATED block's own interface, not from the
                    // declaration site — see _blockInterfaces.
                    var iface = _blockInterfaces.TryGetValue(fbType, out var declared)
                        ? declared
                        : Array.Empty<DbMember>();
                    foreach (var child in iface)
                    {
                        CollectMultiInstanceLeafPaths(instance, string.Empty, child);
                    }

                    added++;
                }
            }

            if (added == 0)
            {
                break;
            }
        }
    }

    // Where the owning FB is instantiated. Real instance DBs first; multi-instance paths already
    // resolved in an earlier round next. Falling back to the DECLARATION SITE when neither exists
    // is what keeps a not-yet-wired corpus judgeable — a block written before its caller still has
    // its interface examined, once, under a path that says plainly it is a class and not a
    // placement. Reporting nothing at all there is the FI-44 failure in a new costume.
    private IEnumerable<string> RootsFor(string ownerFb)
    {
        var found = false;
        foreach (var kv in _instanceToFb)
        {
            if (string.Equals(kv.Value, ownerFb, StringComparison.Ordinal))
            {
                found = true;
                yield return kv.Key;
            }
        }

        foreach (var kv in _multiInstanceToFb)
        {
            if (string.Equals(kv.Value, ownerFb, StringComparison.Ordinal))
            {
                found = true;
                yield return kv.Key;
            }
        }

        if (!found)
        {
            yield return ownerFb + DeclarationSiteMarker;
        }
    }

    private void CollectMultiInstanceLeafPaths(string instance, string suffixPrefix, DbMember member)
    {
        // A nested FB-typed static is an instance in its own right and gets its own rows; it is not
        // a leaf of this one.
        if (_blockNames.Contains(Unquote(member.Datatype)))
        {
            return;
        }

        // An IEC timer/counter static is INSTRUCTION STATE, not interface. Its `.Q` and `.ET` are
        // written by the timer instruction rather than by any caller, so reporting them as members
        // nobody drives is a false positive — and a loud one, since every dwell in a sequencer has
        // one. Excluded at the source rather than filtered downstream, because the question "who
        // drives this" is not meaningful for them at all.
        if (IecInstanceTypes.Contains(Unquote(member.Datatype)))
        {
            return;
        }

        var suffix = suffixPrefix.Length == 0 ? member.Name : suffixPrefix + "." + member.Name;
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectMultiInstanceLeafPaths(instance, suffix, child);
            }
        }
        else
        {
            _multiInstanceMemberPaths.Add((instance, suffix));
        }
    }

    private static string Unquote(string? s) => (s ?? string.Empty).Trim().Trim('"');

    // FI-53. Pool every usage that lands ON a declared member or ANYWHERE INSIDE it, ignoring array
    // subscripts.
    //
    // Why this is needed at all: an `Array[0..3] of "UDT_X"` member is inventoried as ONE leaf
    // (`DB_ParamRet.Silo`) because the walk does not expand a UDT behind an array. Every real
    // reference, though, is written through an element and a member — `DB_ParamRet.Silo[0].ZeroOffset`
    // — so an exact-string lookup finds nothing and the member reads as dead. Measured on a live
    // project: `DB_ParamRet.Silo` and `DB_WeighInterface.Silo` both reported "unused (no writer, no
    // reader)" against 24 and 16 real readers respectively, while their plain-scalar siblings in the
    // same DB listed theirs correctly.
    //
    // The FI-51 half of this (expressing the subscript) was fixed first; this is the reference-graph
    // half, and it is the more dangerous of the two, because its failure mode is a check quietly
    // saying a live member is dead. Deleting on that advice would have removed the plant's entire
    // weighing path.
    //
    // "Any element counts" is the right granularity here: the declared thing is one member of one
    // type, and the question this graph answers is whether anything uses it. Whether some particular
    // ELEMENT is unused is a different question with a different answer shape, and is not pretended
    // to be answered.
    public (IReadOnlyList<UsageSite> Writers, IReadOnlyList<UsageSite> Readers) UsagesCovering(string declaredPath)
    {
        var writers = new List<UsageSite>();
        var readers = new List<UsageSite>();

        foreach (var kv in _usages)
        {
            var stripped = StripSubscripts(kv.Key);
            if (!string.Equals(stripped, declaredPath, StringComparison.Ordinal)
                && !stripped.StartsWith(declaredPath + ".", StringComparison.Ordinal))
            {
                continue;
            }

            writers.AddRange(kv.Value.Writers);
            readers.AddRange(kv.Value.Readers);
        }

        return (writers, readers);
    }

    /// <summary>
    /// Every usage that lands ON <paramref name="leafPath"/> <b>or on an ANCESTOR of it</b>, ignoring
    /// array subscripts on both sides.
    ///
    /// <para>*** THE OPPOSITE DIRECTION FROM <see cref="UsagesCovering"/>, AND A DIFFERENT QUESTION. ***
    /// That one asks <i>"does anything use this declared thing, at any depth inside it?"</i> and walks
    /// DOWNWARD. This one asks <i>"does this one leaf receive a value?"</i> and walks UPWARD, because a
    /// write to a whole struct writes every member of it: <c>MOVE(IN := DB_Param.Recipe[3]) =&gt;
    /// Selected</c> drives <c>Selected.SRID</c>, <c>Selected.TargetMC</c> and every other member,
    /// under a usage key that mentions none of them.</para>
    ///
    /// <para>MEASURED, 2026-08-18: fifty such whole-struct MOVEs left every <c>Selected.*</c> member
    /// reported UNDRIVEN on all four instances of one FB — 40 false reports out of that block's rows —
    /// because <c>undriven-scan</c> looked the key up VERBATIM. It lives here, on the graph, rather
    /// than in the scan, because the join is a property of how storage nests and not of any one
    /// check's question.</para>
    ///
    /// <para>Only the ANCESTOR direction is admitted. A write to a DESCENDANT (<c>Selected.SRID</c>)
    /// drives part of <c>Selected</c> and not the rest, so counting it would be the false-green
    /// direction; <see cref="UsagesCovering"/> answers that separately and says so.</para>
    ///
    /// <para>🔴 <b><paramref name="notAbove"/> IS NOT OPTIONAL POLISH — WITHOUT IT THIS METHOD MARKS
    /// EVERY MEMBER OF EVERY INSTANCE DRIVEN.</b> <c>CALL FB_Drum(iDB_Drum_DrumA, EN := TRUE)</c>
    /// records a WRITE at the bare path <c>iDB_Drum_DrumA</c> — the instance root — which is an
    /// ancestor of every member in it. That reference is the CALL naming its own state store; it is
    /// not a data write of the interface, and treating it as one turned a block with 20 genuine
    /// undriven members into 168 driven ones and exit 0. Measured while building this, and it is the
    /// exact false-green shape the whole repair exists to remove. So a caller passes the instance root
    /// as the floor: an ancestor is admitted only STRICTLY BELOW it. Pass null only when the path is
    /// already block-local and has no instance root to be confused with.</para>
    /// </summary>
    public (IReadOnlyList<UsageSite> Writers, IReadOnlyList<UsageSite> Readers) UsagesReaching(
        string leafPath, string? notAbove = null)
    {
        var target = StripSubscripts(leafPath);
        var floor = notAbove is null ? null : StripSubscripts(notAbove);
        var writers = new List<UsageSite>();
        var readers = new List<UsageSite>();

        foreach (var kv in _usages)
        {
            var stripped = StripSubscripts(kv.Key);
            var exact = string.Equals(stripped, target, StringComparison.Ordinal);
            if (!exact && !target.StartsWith(stripped + ".", StringComparison.Ordinal))
            {
                continue;
            }

            // An ancestor at or above the floor is out of bounds — see the notAbove note above.
            if (!exact && floor is not null && !stripped.StartsWith(floor + ".", StringComparison.Ordinal))
            {
                continue;
            }

            writers.AddRange(kv.Value.Writers);
            readers.AddRange(kv.Value.Readers);
        }

        return (writers, readers);
    }

    // Removes "[n]" from every component. An index never contains a dot, so this cannot disturb the
    // component boundaries the path is built from.
    public static string StripSubscripts(string path) =>
        path.IndexOf('[') < 0
            ? path
            : System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", string.Empty);

    private static readonly HashSet<string> IecInstanceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TON_TIME", "TOF_TIME", "TONR_TIME", "TP_TIME",
        "IEC_TIMER", "IEC_LTIMER",
        "CTU_INT", "CTD_INT", "CTUD_INT", "IEC_COUNTER", "IEC_UCOUNTER",
        "IEC_SCOUNTER", "IEC_DCOUNTER", "IEC_UDCOUNTER", "IEC_LCOUNTER",
    };

    // A multi-instance chain is bounded by how deeply FBs nest in practice; this only caps the
    // fixpoint loop so a malformed corpus with a type cycle cannot spin.
    private const int MaxNestingDepth = 8;

    // Marks a path rooted on a block name rather than on a placement. Not a dot, so the
    // declaration-site form can never be mistaken for a member path.
    public const string DeclarationSiteMarker = "/";

    private void AddFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                AddDb(DbIrParser.ParseDb(text));
                return;
            }

            if (text.StartsWith("TYPE ", StringComparison.Ordinal) || text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                return; // UDT members / tag-table entries are not walked for usage here
            }

            var block = IrParser.HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
            AddBlock(block);
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                       or NonReducibleNetworkException or IrFormatException)
        {
            _warnings.Add($"{path}: not indexed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private void AddBlock(IrBlock block)
    {
        _blockNames.Add(block.Name); // FI-44 — recorded before any usage walk, so a block with no
                                     // tag references and no calls is still known to exist.

        // The cyclic-execution ROOTS. Kept as a derived property of the block (its KIND), never as a
        // name convention: "starts with OB" would be a rule anything can be renamed into, and
        // "nothing calls it" would make every uncalled block its own root — which is precisely the
        // state being detected. An OB is called by the operating system, and nothing else is.
        if (string.Equals(block.Kind, "OB", StringComparison.OrdinalIgnoreCase))
        {
            _organizationBlocks.Add(block.Name);
        }

        // FI-50. A static whose datatype names another FB is a multi-instance. Recorded now,
        // resolved after the walk — the named block may not have been read yet.
        foreach (var member in block.StaticMembers ?? Array.Empty<DbMember>())
        {
            _multiInstanceCandidates.Add((block.Name, member));
        }

        _blockInterfaces[block.Name] = (block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers)
            .Concat(block.StaticMembers ?? Array.Empty<DbMember>())
            .ToList();

        // TEMPS AND CONSTANTS ARE INCLUDED HERE AND DELIBERATELY NOT IN `_blockInterfaces` ABOVE.
        // That index answers "what does an instance of this FB contain", where a temp does not
        // belong; this one answers "does this name resolve inside this block", where it does — a
        // temp named `Time` is exactly the collision that produced the false finding.
        _blockLocalRoots[block.Name] = (block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers)
            .Concat(block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(block.TempMembers)
            .Concat(block.ConstantMembers ?? Array.Empty<DbMember>())
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var network in block.Networks)
        {
            foreach (var call in network.Calls)
            {
                _calls.Add((block.Name, call.BlockName));
            }

            foreach (var usage in TagReferences.AllDirectedUsages(network))
            {
                var site = new UsageSite(block.Name, network.Number, usage.SetResetKind, usage.Guard);
                var entry = GetOrAdd(usage.Path);
                if (usage.Direction == TagDirection.Write)
                {
                    entry.Writers.Add(site);
                }
                else
                {
                    entry.Readers.Add(site);
                }

                _flat.Add((block.Name, network.Number, usage.Path, usage.Direction));
            }
        }
    }

    // Global-DB members: their full path (DBName.member…) is how logic always addresses them, so the
    // reader/writer sets are unambiguous — inventoried into _globalDbMemberPaths as before.
    //
    // Instance-DB / interface-UDT members alias between an FB-internal bare name and an external
    // iDB-qualified path (FI-22 follow-up). We no longer skip them: capture the iDB->FB link and
    // inventory the iDB's interface-member leaves (as suffixes relative to the iDB root) into the
    // parallel indexes above, so CrossCheckRunner can correlate the two alias forms. The global path
    // (_usages / _globalDbMemberPaths) is untouched.
    private void AddDb(DbSource db)
    {
        if (db.InstanceOfName is not null)
        {
            _instanceToFb[db.Name] = db.InstanceOfName;
            foreach (var member in AllInterfaceMembers(db))
            {
                CollectInstanceLeafPaths(db.Name, suffixPrefix: string.Empty, member);
            }

            return;
        }

        foreach (var member in db.Members ?? Array.Empty<DbMember>())
        {
            CollectLeafPaths(db.Name, member);
        }
    }

    private static IEnumerable<DbMember> AllInterfaceMembers(DbSource db)
    {
        foreach (var m in db.Members ?? Array.Empty<DbMember>())
        {
            yield return m;
        }

        foreach (var m in db.InputMembers ?? Array.Empty<DbMember>())
        {
            yield return m;
        }

        foreach (var m in db.OutputMembers ?? Array.Empty<DbMember>())
        {
            yield return m;
        }

        foreach (var m in db.InOutMembers)
        {
            yield return m;
        }
    }

    private void CollectLeafPaths(string prefix, DbMember member)
    {
        var path = prefix + "." + member.Name;
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectLeafPaths(path, child);
            }
        }
        else
        {
            _globalDbMemberPaths.Add(path);
        }
    }

    // Same leaf-recursion as CollectLeafPaths, but records the member SUFFIX (path relative to the iDB
    // root) rather than a root-qualified path — that suffix is exactly the FB-internal alias form.
    private void CollectInstanceLeafPaths(string instanceDb, string suffixPrefix, DbMember member)
    {
        // FI-50. Same exclusion as the multi-instance path: an IEC timer/counter inside an instance
        // DB is instruction state, written by the instruction and not by any caller. Applied on both
        // paths deliberately — the two forms describe the same placement, and a member that is noise
        // through one route is noise through the other.
        if (IecInstanceTypes.Contains(Unquote(member.Datatype)))
        {
            return;
        }

        var suffix = suffixPrefix.Length == 0 ? member.Name : suffixPrefix + "." + member.Name;
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectInstanceLeafPaths(instanceDb, suffix, child);
            }
        }
        else
        {
            _instanceMemberPaths.Add((instanceDb, suffix));
        }
    }

    // Collapses either alias form of an interface-UDT member to one canonical key `"<FB>|<suffix>"`.
    // A path rooted at a known instance-DB name (`iDB_X.IO.Step`) canonicalizes to `"<FB>|IO.Step"`;
    // the FB-internal bare form (`IO.Step`, referenced inside FB `<FB>`'s own logic) is the same key,
    // formed by the caller as `"<FB>|IO.Step"` since a bare path carries no root to key off. Returns
    // null when the root is not a known iDB (a global-DB / local / physical path).
    public string? CanonicalizeInstancePath(string path)
    {
        var dot = path.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var root = path.Substring(0, dot);
        return _instanceToFb.TryGetValue(root, out var fb)
            ? fb + "|" + path.Substring(dot + 1)
            : null;
    }

    /// <summary>
    /// The block that OWNS the storage <paramref name="path"/> names when referenced from inside
    /// <paramref name="block"/>, or null when the path is global (a DB path, a PLC tag, a physical
    /// address, an `iDB_…` reference — anything whose root the block does not declare).
    ///
    /// <para>*** THIS IS THE TEST THAT SEPARATES A REAL CROSS-BLOCK CONFLICT FROM AN ALIAS. *** A
    /// non-null answer means the path is block-local and CANNOT be the same storage as an
    /// identically-spelled path inside a different block.</para>
    /// </summary>
    public string? OwnerOf(string block, string path)
    {
        var root = StripSubscripts(path);
        var dot = root.IndexOf('.');
        if (dot > 0)
        {
            root = root.Substring(0, dot);
        }

        return _blockLocalRoots.TryGetValue(block, out var roots) && roots.Contains(root) ? block : null;
    }

    /// <summary>
    /// The STORAGE-IDENTITY key for a usage: a block-local path keyed under its owner, a global path
    /// left exactly as written. Two usages share a key if and only if they can name the same storage.
    ///
    /// <para>⚠️ <b>WHAT THIS KEY DELIBERATELY DOES NOT DO: it does not pool an FB-internal member with
    /// the `iDB.&lt;suffix&gt;` form of the same member.</b> Those genuinely are one storage when the FB
    /// has one instance — but an FB with TWO instance DBs has an internal write landing in BOTH, and
    /// pooling it with either one would invent a conflict exactly as the bug this replaces did.
    /// Rather than choose, the alias set is REPORTED on the fact (<c>MultiWriterFact.InstanceAliases</c>)
    /// so a consumer can join them knowingly. Facts, not verdicts — and no answer invented for a case
    /// the corpus has not yet produced (every FB in `ir/test-project001` has exactly one instance DB,
    /// which is precisely why designing only for that would be designing for the case that happens to
    /// exist).</para>
    /// </summary>
    public string QualifiedPath(string block, string path) =>
        OwnerOf(block, path) is { } owner ? owner + "|" + path : path;

    /// <summary>
    /// Every `iDB.&lt;path&gt;` form that names the same storage as <paramref name="path"/> inside FB
    /// <paramref name="owner"/> — empty for a temp or constant (which no instance DB contains), and
    /// empty for an FB with no instance DB yet. Read off the instance DBs' OWN declared members, so a
    /// name that is not really in the instance interface never produces a phantom alias.
    /// </summary>
    public IReadOnlyList<string> InstanceAliasesOf(string owner, string path)
    {
        var stripped = StripSubscripts(path);
        var aliases = new List<string>();
        foreach (var (instanceDb, suffix) in _instanceMemberPaths)
        {
            if (!string.Equals(suffix, stripped, StringComparison.Ordinal)
                || !_instanceToFb.TryGetValue(instanceDb, out var fb)
                || !string.Equals(fb, owner, StringComparison.Ordinal))
            {
                continue;
            }

            aliases.Add(instanceDb + "." + path);
        }

        return aliases.Distinct(StringComparer.Ordinal).OrderBy(a => a, StringComparer.Ordinal).ToList();
    }

    private PathUsage GetOrAdd(string path)
    {
        if (!_usages.TryGetValue(path, out var entry))
        {
            entry = new PathUsage(new List<UsageSite>(), new List<UsageSite>());
            _usages[path] = entry;
        }

        return entry;
    }
}
