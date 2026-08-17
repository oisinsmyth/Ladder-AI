using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ladder.Wave;

namespace Ladder.Wave.Cli
{
    /// <summary>
    /// DB-1's SECOND INPUT: the corpus the change set is classified against.
    ///
    /// <para>DB-1 needs three things and a change set supplies only one of them. A drift report says
    /// <c>DB_PLC</c> changed; it does not say that <c>DB_PLC</c> is a global data block, and the whole
    /// change-class table is keyed on the KIND. This reads the kind — and the UDT/instance edges the
    /// blast radius is computed over — out of the <c>.ir</c> files themselves.</para>
    ///
    /// <para>🔴 <b>IDENTITY COMES FROM EACH FILE'S OWN DECLARATION, NEVER FROM ITS FILENAME AND NEVER
    /// FROM A NAME PREFIX.</b> Both mistakes are recorded in this repo. `drift-check` paired on the
    /// filename and reported ONE tag table twice, in two contradictory directions, because TIA's own
    /// name for it contains spaces (`Default tag table` vs `DefaultTagTable.ir`). `cross-check` derives
    /// reachability from an object's KIND rather than from a name prefix, "which anything can be renamed
    /// into". A classifier keyed on `FB_`/`iDB_`/`UDT_` would be a naming-convention checker wearing a
    /// router's clothes, and it would be WRONG about exactly the objects somebody renamed —
    /// <c>MotorFwdRevIOSet</c> in this corpus is a UDT whose name carries no `UDT_` prefix at all.</para>
    ///
    /// <para><b>AN OBJECT ABSENT FROM THE CORPUS IS NOT A DEFAULT KIND.</b> It is unlookupable, and the
    /// classifier refuses it BY NAME. That is not a corner case here: the live reconciliation of
    /// 2026-08-14 found two objects in the controller that no <c>.ir</c> describes, and a router that
    /// guessed their kind would be guessing whether loading them stops the CPU.</para>
    /// </summary>
    public sealed class IrCorpus
    {
        // Anchored at a member-declaration position on purpose. An unanchored `:\s*"` would also match
        // inside a COMMENT string — and this corpus has comments containing colons, quotes and block
        // names, so the loose form would invent type references out of prose.
        private static readonly Regex TypedMemberDeclaration =
            new Regex("^\\s*[A-Za-z_][A-Za-z0-9_]*\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static readonly Regex CallStatement =
            new Regex("^\\s*CALL\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled);

        private readonly Dictionary<string, CorpusObject> _byName;

        private IrCorpus(
            string directory,
            IReadOnlyList<CorpusObject> objects,
            Dictionary<string, CorpusObject> byName,
            int filesSeen,
            IReadOnlyList<string> unreadable,
            IReadOnlyList<string> ambiguous)
        {
            Directory = directory;
            Objects = objects;
            _byName = byName;
            FilesSeen = filesSeen;
            Unreadable = unreadable;
            AmbiguousNames = ambiguous;
        }

        /// <summary>The directory read.</summary>
        public string Directory { get; }

        /// <summary>Every object whose identity was read from its own content.</summary>
        public IReadOnlyList<CorpusObject> Objects { get; }

        /// <summary>
        /// How many <c>.ir</c> files were opened. THE DENOMINATOR. A corpus read that identified 43
        /// objects out of 43 files and one that identified 0 out of 0 both report "no problems"; only
        /// this number separates them.
        /// </summary>
        public int FilesSeen { get; }

        /// <summary>Files whose declaration could not be read, NAMED. Never silently dropped.</summary>
        public IReadOnlyList<string> Unreadable { get; }

        /// <summary>
        /// Object names claimed by more than one file. Neither file wins: a lookup on that name is a
        /// REFUSAL, because choosing one would be a guess about which was meant — `drift-check`'s
        /// PAIRING-FAILURE rule, applied to the same question one layer up.
        /// </summary>
        public IReadOnlyList<string> AmbiguousNames { get; }

        /// <summary>Read every <c>*.ir</c> in a directory. Never throws for bad content.</summary>
        public static IrCorpus Read(string directory)
        {
            if (directory == null)
            {
                throw new ArgumentNullException(nameof(directory));
            }

            var objects = new List<CorpusObject>();
            var unreadable = new List<string>();
            var seen = new Dictionary<string, CorpusObject>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = System.IO.Directory.Exists(directory)
                ? System.IO.Directory.GetFiles(directory, "*.ir", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : new string[0];

            foreach (var file in files)
            {
                CorpusObject? parsed;
                try
                {
                    parsed = Parse(file, File.ReadAllLines(file));
                }
                catch (IOException ex)
                {
                    unreadable.Add(Path.GetFileName(file) + " (" + ex.GetType().Name + ": " + ex.Message + ")");
                    continue;
                }

                if (parsed == null)
                {
                    unreadable.Add(Path.GetFileName(file) + " (no BLOCK / DB / TYPE / TAGTABLE declaration on its first non-blank line)");
                    continue;
                }

                objects.Add(parsed);

                if (seen.ContainsKey(parsed.Name))
                {
                    ambiguous.Add(parsed.Name);
                }
                else
                {
                    seen[parsed.Name] = parsed;
                }
            }

            foreach (var name in ambiguous)
            {
                seen.Remove(name);
            }

            return new IrCorpus(directory, objects, seen, files.Length, unreadable, ambiguous.ToArray());
        }

        /// <summary>Look an object up by its DECLARED name. False when absent OR ambiguous.</summary>
        public bool TryLookup(string name, out CorpusObject? found)
        {
            found = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return _byName.TryGetValue(name.Trim(), out found);
        }

        /// <summary>TRUE when the name is claimed by two files — a different absence from "not there".</summary>
        public bool IsAmbiguous(string name) =>
            name != null && AmbiguousNames.Any(a => string.Equals(a, name.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// DB-1's blast radius, DIRECT ROUTE: every data block whose OWN declaration carries a member of
        /// this type. "A UDT change is one object plus every DB built on it."
        /// </summary>
        public IReadOnlyList<string> DataBlocksDeclaring(string typeName) =>
            Objects
                .Where(o => o.Kind == ObjectKind.GlobalDataBlock || o.Kind == ObjectKind.InstanceDataBlock)
                .Where(o => o.DeclaredTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase))
                .Select(o => o.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        /// <summary>
        /// The SAME question by a second route: the instance DBs of every FB whose interface declares the
        /// type. It exists to be COMPARED with <see cref="DataBlocksDeclaring"/>, not to replace it.
        ///
        /// <para>In this corpus an instance DB inlines its FB's UDT members, so the two routes agree and
        /// the direct one suffices. That agreement is a PROPERTY OF THIS CORPUS, not of the format — and
        /// a blast radius that silently missed a DB would under-invalidate results, which is the
        /// direction that ships a stale green. So both are computed and a disagreement is REPORTED.</para>
        /// </summary>
        public IReadOnlyList<string> InstanceDbsOfBlocksDeclaring(string typeName)
        {
            var owners = Objects
                .Where(o => o.Kind == ObjectKind.FunctionBlock)
                .Where(o => o.DeclaredTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase))
                .Select(o => o.Name)
                .ToArray();

            return Objects
                .Where(o => o.Kind == ObjectKind.InstanceDataBlock && o.InstanceOf.Length > 0)
                .Where(o => owners.Contains(o.InstanceOf, StringComparer.OrdinalIgnoreCase))
                .Select(o => o.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// DB-4's closure edges for one object: "must already be present, or land in the same download".
        /// An instance DB depends on its FB; anything depends on the UDTs it declares and the blocks it
        /// calls. NOT class-changing — DB-4's batching question, kept apart from DB-1's blast radius,
        /// which is the only thing that moves an object's CLASS.
        /// </summary>
        public IReadOnlyList<string> DependenciesOf(string name)
        {
            CorpusObject? o;
            if (!TryLookup(name, out o) || o == null)
            {
                return new string[0];
            }

            var instanceEdge = o.InstanceOf.Length == 0 ? new string[0] : new[] { o.InstanceOf };

            return instanceEdge
                .Concat(o.DeclaredTypes)
                .Concat(o.Calls)
                .Where(d => !string.Equals(d, o.Name, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static CorpusObject? Parse(string path, IReadOnlyList<string> lines)
        {
            string? name = null;
            var kind = ObjectKind.Unknown;
            var header = -1;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                header = i;

                // The grammar is ir/SPEC.md's: a block declares its kind explicitly, a DB is an instance
                // DB only if it says INSTANCEOF further down, and a tag table's name may contain spaces
                // (TIA's own "Default tag table") so it is the REST OF THE LINE, not the next token.
                if (line.StartsWith("BLOCK ", StringComparison.Ordinal))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3)
                    {
                        return null;
                    }

                    switch (parts[1])
                    {
                        case "OB": kind = ObjectKind.OrganizationBlock; break;
                        case "FB": kind = ObjectKind.FunctionBlock; break;
                        case "FC": kind = ObjectKind.Function; break;
                        default: return null;
                    }

                    name = parts[2];
                }
                else if (line.StartsWith("DB ", StringComparison.Ordinal))
                {
                    kind = ObjectKind.GlobalDataBlock; // upgraded to InstanceDataBlock by INSTANCEOF below
                    name = line.Substring(3).Trim();
                }
                else if (line.StartsWith("TYPE ", StringComparison.Ordinal))
                {
                    kind = ObjectKind.DataType;
                    name = line.Substring(5).Trim();
                }
                else if (line.StartsWith("TAGTABLE ", StringComparison.Ordinal))
                {
                    kind = ObjectKind.TagTable;
                    name = line.Substring(9).Trim();
                }
                else
                {
                    return null;
                }

                break;
            }

            if (name == null || name.Length == 0 || kind == ObjectKind.Unknown)
            {
                return null;
            }

            var instanceOf = string.Empty;
            var declaredTypes = new List<string>();
            var calls = new List<string>();
            var inNetworks = false;

            for (var i = header + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (trimmed.StartsWith("NETWORK ", StringComparison.Ordinal))
                {
                    inNetworks = true;
                }

                if (!inNetworks)
                {
                    if (trimmed.StartsWith("INSTANCEOF ", StringComparison.Ordinal))
                    {
                        instanceOf = trimmed.Substring("INSTANCEOF ".Length).Trim();
                        kind = ObjectKind.InstanceDataBlock;
                    }

                    var typed = TypedMemberDeclaration.Match(line);
                    if (typed.Success)
                    {
                        declaredTypes.Add(typed.Groups[1].Value);
                    }
                }
                else
                {
                    var call = CallStatement.Match(line);
                    if (call.Success)
                    {
                        calls.Add(call.Groups[1].Value);
                    }
                }
            }

            return new CorpusObject(
                name,
                kind,
                instanceOf,
                declaredTypes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray(),
                calls.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray(),
                path);
        }
    }

    /// <summary>One object as the IR corpus declares itself to be.</summary>
    public sealed class CorpusObject
    {
        internal CorpusObject(
            string name,
            ObjectKind kind,
            string instanceOf,
            IReadOnlyList<string> declaredTypes,
            IReadOnlyList<string> calls,
            string path)
        {
            Name = name;
            Kind = kind;
            InstanceOf = instanceOf ?? string.Empty;
            DeclaredTypes = declaredTypes;
            Calls = calls;
            Path = path;
        }

        /// <summary>The name the file itself declares.</summary>
        public string Name { get; }

        /// <summary>What kind of object it declares itself to be.</summary>
        public ObjectKind Kind { get; }

        /// <summary>For an instance DB, the FB it instantiates. Empty otherwise.</summary>
        public string InstanceOf { get; }

        /// <summary>UDTs named by its own member declarations.</summary>
        public IReadOnlyList<string> DeclaredTypes { get; }

        /// <summary>Blocks it CALLs.</summary>
        public IReadOnlyList<string> Calls { get; }

        /// <summary>Where it was read from.</summary>
        public string Path { get; }
    }
}
