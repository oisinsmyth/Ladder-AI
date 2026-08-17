using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ladder.Wave;

namespace Ladder.Wave.Cli
{
    /// <summary>
    /// <c>wave-cli route</c> — DB-1's ENTRY POINT.
    ///
    /// <para>DB-1 is "CHANGE TRACKER *** AND ROUTER ***" and its three outputs are named in the spec:
    /// WHICH objects changed, WHAT CLASS each change is, and BLAST RADIUS. <see cref="ChangeRouter"/>
    /// was built and says of itself "WHAT THIS TYPE DOES NOT DO: it does not CLASSIFY ... DB-1 is not
    /// built" — and until this verb existed nothing executable reached it at all: <c>wave-cli</c> had
    /// <c>submit</c> and <c>status</c>, both driving admission and colouring, and the router was
    /// referenced only by its own tests.</para>
    ///
    /// <para>🔴 <b>EMPTY IS NOT CLEAN, AND THE DENOMINATOR IS PRINTED ON EVERY RUN.</b> A change set of
    /// zero, a corpus of zero files, a drift report that compared nothing, and a set whose every row was
    /// a MATCH are all <b>exit 2 with a reason</b>, never a green "0 objects routed". Every other number
    /// in the summary is a reason something did NOT get routed; the examined count is the one that says
    /// the question was asked of anything at all.</para>
    ///
    /// <para>🔴 <b>NOTHING IS DEFAULTED TO RUN.</b> An object whose kind or nature cannot be established
    /// is NAMED and the run fails closed (exit 1). Misclassifying a STOP as a RUN puts a CPU-stopping
    /// download through a wave boundary; the cost of the opposite error is an agent waiting.</para>
    /// </summary>
    public static class RouteCommand
    {
        /// <summary>Every change got a class and a queue.</summary>
        public const int ExitRouted = 0;

        /// <summary>At least one change could not be classified or routed. FAIL CLOSED.</summary>
        public const int ExitRefused = 1;

        /// <summary>Nothing was examined, or the invocation cannot be honoured. Retrying will not help.</summary>
        public const int ExitUnusable = 2;

        /// <summary>DB-4's limit: "more than 20 changed objects cannot be integrated consistently in one program cycle".</summary>
        public const int BatchBudget = 20;

        /// <summary>Run the verb. Writers are injected so the whole verb is testable end to end.</summary>
        public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (stdout == null) throw new ArgumentNullException(nameof(stdout));
            if (stderr == null) throw new ArgumentNullException(nameof(stderr));

            var options = RouteOptions.Parse(args);

            if (options.Has("--help") || options.Has("-h"))
            {
                stdout.WriteLine(Usage);
                return ExitUnusable;
            }

            var project = options.Value("--project");
            if (string.IsNullOrWhiteSpace(project))
            {
                stderr.WriteLine("BAD INVOCATION: --project <ir-dir> is required. The change class is keyed on " +
                                 "each object's KIND, and the kind is read out of the .ir corpus — there is " +
                                 "nothing to classify against without it.");
                return ExitUnusable;
            }

            var corpus = IrCorpus.Read(project);

            if (corpus.FilesSeen == 0)
            {
                stderr.WriteLine("NOTHING EXAMINED: no .ir files under '" + project + "'. A corpus of zero " +
                                 "objects would refuse every change for the same reason and look exactly like " +
                                 "a change set nobody could classify. Note the walk is TopDirectoryOnly — a " +
                                 "path one level above the files reads as an empty corpus.");
                return ExitUnusable;
            }

            var changeSet = ReadChangeSet(options, stderr);
            if (changeSet == null)
            {
                return ExitUnusable;
            }

            if (!changeSet.Ok)
            {
                stderr.WriteLine("REFUSED: " + changeSet.Refusal);
                return ExitUnusable;
            }

            IReadOnlyDictionary<string, ChangeClass> declaredClasses;
            string declarationError;
            if (!options.TryDeclaredClasses(out declaredClasses, out declarationError))
            {
                stderr.WriteLine("BAD INVOCATION: " + declarationError);
                return ExitUnusable;
            }

            var report = Build(corpus, changeSet, declaredClasses);

            if (options.Has("--json"))
            {
                stdout.WriteLine(ToJson(report));
            }
            else
            {
                WriteText(report, stdout);
            }

            return report.ExitCode;
        }

        private static ChangeSetResult? ReadChangeSet(RouteOptions options, TextWriter stderr)
        {
            var driftPath = options.Value("--drift-check");
            var declared = options.Many("--changed");
            var declaredFrom = options.Value("--changed-from");

            if (driftPath != null && declared.Count > 0)
            {
                // The same refusal `submit` makes about --reachable-state vs --reaches, for the same
                // reason: one is produced by a tool from the artifacts, the other is typed by whoever is
                // asking, and accepting both would mean silently choosing which to believe.
                stderr.WriteLine("REFUSED: --drift-check cannot be combined with --changed. One change set " +
                                 "is COMPUTED by `converter drift-check`, the other is DECLARED by the " +
                                 "caller; taking both would mean choosing which to believe.");
                return null;
            }

            if (driftPath == null && declared.Count == 0)
            {
                stderr.WriteLine("BAD INVOCATION: a change set is required — either --drift-check <report.json> " +
                                 "--exports-are <committed-corpus|controller-dump>, or --changed <name>=<nature> " +
                                 "--changed-from <text>.");
                return null;
            }

            if (driftPath != null)
            {
                var meaning = ExportsMeaning.Unstated;
                switch ((options.Value("--exports-are") ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "committed-corpus": meaning = ExportsMeaning.CommittedCorpus; break;
                    case "controller-dump": meaning = ExportsMeaning.ControllerDump; break;
                }

                return ChangeSetDocument.FromDriftCheck(driftPath, meaning);
            }

            return ChangeSetDocument.FromDeclaration(declared, declaredFrom ?? string.Empty);
        }

        // =================================================================================================
        // THE PIPELINE
        // =================================================================================================

        internal static RouteReport Build(
            IrCorpus corpus,
            ChangeSetResult changeSet,
            IReadOnlyDictionary<string, ChangeClass> declaredClasses)
        {
            var report = new RouteReport(corpus, changeSet.Provenance);

            report.NotChanges = changeSet.Entries.Where(e => !e.IsChange).ToArray();
            var changes = changeSet.Entries.Where(e => e.IsChange).ToArray();
            report.DeclaredCount = changes.Length;

            if (changes.Length == 0)
            {
                report.Verdict =
                    "NOTHING ROUTED - this is not a pass. " + changeSet.Entries.Count +
                    " row(s) were read and every one of them is a NON-CHANGE. A change set of zero is a " +
                    "statement about the producer, not a clean bill of health for the program.";
                report.ExitCode = ExitUnusable;
                return report;
            }

            // ---- (2) WHAT CLASS each change is ----------------------------------------------------
            var verdicts = new List<ClassificationVerdict>();
            foreach (var change in changes)
            {
                ChangeClass declaredClass;
                if (!declaredClasses.TryGetValue(change.Name, out declaredClass))
                {
                    declaredClass = ChangeClass.Unknown;
                }

                verdicts.Add(ChangeClassifier.Classify(change.Name, change.Nature, corpus, declaredClass));
            }

            // ---- (3) BLAST RADIUS ------------------------------------------------------------------
            // "a UDT change is one object plus every DB built on it". The derived objects are REAL
            // objects that must be downloaded, so they are added to the routed set and to the count DB-4's
            // budget is expressed in - not merely mentioned in a footnote.
            var declaredNames = new HashSet<string>(changes.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            var derived = new List<ClassificationVerdict>();

            foreach (var verdict in verdicts.Where(v => v.Classified && v.Kind == ObjectKind.DataType && v.Nature == ChangeNature.Modified))
            {
                var direct = corpus.DataBlocksDeclaring(verdict.Name);
                var viaFb = corpus.InstanceDbsOfBlocksDeclaring(verdict.Name);
                var union = direct.Concat(viaFb).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

                report.BlastRadii.Add(new BlastRadiusEntry(verdict.Name, direct, viaFb, union,
                    union.Where(declaredNames.Contains).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray()));

                foreach (var db in union.Where(db => !declaredNames.Contains(db)))
                {
                    CorpusObject? found;
                    corpus.TryLookup(db, out found);

                    derived.Add(new ClassificationVerdict(
                        db,
                        found == null ? ObjectKind.Unknown : found.Kind,
                        ChangeNature.Modified,
                        ChangeClass.RunInit,
                        ClassificationRefusal.None,
                        "DB-1 BLAST RADIUS of '" + verdict.Name + "' — modified UDT ... RUN (Init) on EVERY DB " +
                        "built on it. This object is not in the change set; it is entailed by one that is.",
                        string.Empty,
                        false));

                    declaredNames.Add(db);
                }
            }

            report.DerivedCount = derived.Count;

            // ---- ROUTE: the actual consumer, ChangeRouter --------------------------------------------
            foreach (var verdict in verdicts.Concat(derived))
            {
                if (!verdict.Classified)
                {
                    report.Refusals.Add(new RefusalRow(verdict.Name, verdict.Kind, verdict.Nature,
                        "classify", verdict.Refusal.ToString(), verdict.Reason));
                    continue;
                }

                var dependencies = corpus.DependenciesOf(verdict.Name);
                var routed = ChangeRouter.Route(new ChangedObject(verdict.Name, verdict.Kind, verdict.ChangeClass, dependencies));

                if (!routed.Routed)
                {
                    report.Refusals.Add(new RefusalRow(verdict.Name, verdict.Kind, verdict.Nature,
                        "route", routed.Refusal.ToString(), routed.Reason));
                    continue;
                }

                report.Routed.Add(new RoutedRow(verdict, routed.Queue, dependencies,
                    dependencies.Where(d => !declaredNames.Contains(d))
                        .OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToArray()));
            }

            var examined = report.DeclaredCount + report.DerivedCount;

            report.ExitCode = report.Refusals.Count == 0 ? ExitRouted : ExitRefused;
            report.Verdict = report.Refusals.Count == 0
                ? "ROUTED - all " + examined + " object(s) examined carry a DB-1 class and a queue."
                : "REFUSED - " + report.Refusals.Count + " of " + examined + " object(s) examined could not " +
                  "be classified or routed. NOTHING was defaulted to RUN; each is named above.";

            return report;
        }

        // =================================================================================================
        // OUTPUT
        // =================================================================================================

        internal static void WriteText(RouteReport report, TextWriter w)
        {
            var examined = report.DeclaredCount + report.DerivedCount;

            w.WriteLine("ROUTE - DB-1 change tracker and router.");
            w.WriteLine("CORPUS: " + report.Corpus.Directory + " - " + report.Corpus.FilesSeen +
                        " .ir file(s) read, " + report.Corpus.Objects.Count + " object(s) identified from their " +
                        "OWN declaration, " + report.Corpus.Unreadable.Count + " unreadable, " +
                        report.Corpus.AmbiguousNames.Count + " ambiguous.");

            foreach (var unreadable in report.Corpus.Unreadable)
            {
                w.WriteLine("  UNREADABLE: " + unreadable);
            }

            foreach (var ambiguous in report.Corpus.AmbiguousNames)
            {
                w.WriteLine("  AMBIGUOUS IDENTITY: " + ambiguous);
            }

            w.WriteLine("CHANGE SET: " + report.Provenance);
            w.WriteLine("EXAMINED: " + report.DeclaredCount + " object(s) from the change set + " +
                        report.DerivedCount + " entailed by blast radius = " + examined + " total.");

            if (report.NotChanges.Count > 0)
            {
                w.WriteLine("NOT A CHANGE (" + report.NotChanges.Count + ", excluded from the count above and " +
                            "named so the exclusion is visible):");
                foreach (var row in report.NotChanges.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    w.WriteLine("  " + row.Name + " [" + row.Source + "] " + row.Note);
                }
            }

            if (report.Routed.Count > 0)
            {
                w.WriteLine("ROUTED (" + report.Routed.Count + "):");
                foreach (var row in report.Routed.OrderBy(r => r.Verdict.Name, StringComparer.OrdinalIgnoreCase))
                {
                    w.WriteLine("  " + row.Verdict.Name + "  [" + row.Verdict.Kind + ", " + row.Verdict.Nature + "]" +
                                "  -> " + Describe(row.Verdict.ChangeClass) + " -> " + row.Queue +
                                (row.Verdict.FromDeclaration ? "  (class DECLARED, not computed)" : string.Empty));
                    w.WriteLine("      rule: " + row.Verdict.Rule);
                    if (row.NotInChangeSet.Count > 0)
                    {
                        w.WriteLine("      DB-4 closure: depends on " + string.Join(", ", row.NotInChangeSet.ToArray()) +
                                    " - not in this change set, so it must ALREADY BE PRESENT on the device.");
                    }
                }
            }

            if (report.Refusals.Count > 0)
            {
                w.WriteLine("REFUSED (" + report.Refusals.Count + "):");
                foreach (var row in report.Refusals.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    w.WriteLine("  " + row.Name + "  [" + row.Kind + ", " + row.Nature + "]  " +
                                row.Stage + ": " + row.Refusal);
                    w.WriteLine("      " + row.Reason);
                }
            }

            if (report.BlastRadii.Count > 0)
            {
                w.WriteLine("BLAST RADIUS (" + report.BlastRadii.Count + " modified UDT(s)):");
                foreach (var radius in report.BlastRadii)
                {
                    w.WriteLine("  " + radius.TypeName + " -> " + radius.All.Count + " DB(s) built on it" +
                                (radius.All.Count == 0
                                    ? ": NONE in this corpus. That is a computed 0 against " +
                                      report.Corpus.Objects.Count + " objects, not an unasked question."
                                    : ": " + string.Join(", ", radius.All.ToArray())));

                    if (radius.AlreadyInChangeSet.Count > 0)
                    {
                        w.WriteLine("      already in the change set: " + string.Join(", ", radius.AlreadyInChangeSet.ToArray()));
                    }

                    w.WriteLine("      two routes: DIRECT (a DB declaring the type) found " + radius.Direct.Count +
                                "; VIA FB INSTANCE found " + radius.ViaFbInstance.Count + " - " +
                                (radius.RoutesAgree
                                    ? "they AGREE."
                                    : "*** THEY DISAGREE. *** The union is used. A DB reachable by only one " +
                                      "route means an instance DB does not inline its FB's members, which " +
                                      "this corpus has never contained; check before trusting the radius."));
                }
            }

            var runQueue = report.Routed.Count(r => r.Queue == DownloadQueue.RunQueue);
            var deferred = report.Routed.Count(r => r.Queue == DownloadQueue.DeferredQueue);
            w.WriteLine("QUEUES: RunQueue " + runQueue + ", DeferredQueue " + deferred + ".");

            if (deferred > 0)
            {
                w.WriteLine("  *** " + deferred + " object(s) are STOP-class: they do NOT flow through a wave " +
                            "boundary and accumulate until the deferred queue is drained (D23 / D24). ***");
            }

            w.WriteLine("BUDGET: " + examined + " object(s) against DB-4's <=" + BatchBudget +
                        (examined > BatchBudget
                            ? " - OVER. This cannot be integrated consistently in one program cycle; it needs " +
                              "dependency-closed batching."
                            : " - within it."));

            w.WriteLine("SCOPE: the fixed-kind cross-check in ChangeRouter reads the SAME DB-1 rows this " +
                        "classifier does, so for a COMPUTED class it demonstrates self-consistency and " +
                        "nothing more. It has independent force only over a class supplied by --class.");

            w.WriteLine("VERDICT: " + report.Verdict);
        }

        private static string Describe(ChangeClass changeClass) =>
            changeClass == ChangeClass.RunInit ? "RUN (Init)" : changeClass.ToString().ToUpperInvariant();

        internal static string ToJson(RouteReport report)
        {
            var examined = report.DeclaredCount + report.DerivedCount;

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();

                    writer.WriteStartObject("corpus");
                    writer.WriteString("directory", report.Corpus.Directory);
                    writer.WriteNumber("filesSeen", report.Corpus.FilesSeen);
                    writer.WriteNumber("objectsIdentified", report.Corpus.Objects.Count);
                    WriteArray(writer, "unreadable", report.Corpus.Unreadable);
                    WriteArray(writer, "ambiguousNames", report.Corpus.AmbiguousNames);
                    writer.WriteEndObject();

                    writer.WriteStartObject("changeSet");
                    writer.WriteString("provenance", report.Provenance);
                    writer.WriteNumber("declared", report.DeclaredCount);
                    writer.WriteNumber("derivedByBlastRadius", report.DerivedCount);
                    writer.WriteNumber("examined", examined);
                    writer.WriteStartArray("notChanges");
                    foreach (var row in report.NotChanges)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", row.Name);
                        writer.WriteString("source", row.Source);
                        writer.WriteString("note", row.Note);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();

                    writer.WriteStartArray("routed");
                    foreach (var row in report.Routed.OrderBy(r => r.Verdict.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", row.Verdict.Name);
                        writer.WriteString("kind", row.Verdict.Kind.ToString());
                        writer.WriteString("nature", row.Verdict.Nature.ToString());
                        writer.WriteString("changeClass", row.Verdict.ChangeClass.ToString());
                        writer.WriteString("queue", row.Queue.ToString());
                        writer.WriteString("rule", row.Verdict.Rule);
                        writer.WriteBoolean("classFromDeclaration", row.Verdict.FromDeclaration);
                        WriteArray(writer, "dependsOn", row.DependsOn);
                        WriteArray(writer, "dependenciesNotInChangeSet", row.NotInChangeSet);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteStartArray("refused");
                    foreach (var row in report.Refusals.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", row.Name);
                        writer.WriteString("kind", row.Kind.ToString());
                        writer.WriteString("nature", row.Nature.ToString());
                        writer.WriteString("stage", row.Stage);
                        writer.WriteString("refusal", row.Refusal);
                        writer.WriteString("reason", row.Reason);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteStartArray("blastRadius");
                    foreach (var radius in report.BlastRadii)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", radius.TypeName);
                        WriteArray(writer, "dataBlocks", radius.All);
                        WriteArray(writer, "directRoute", radius.Direct);
                        WriteArray(writer, "viaFbInstanceRoute", radius.ViaFbInstance);
                        WriteArray(writer, "alreadyInChangeSet", radius.AlreadyInChangeSet);
                        writer.WriteBoolean("routesAgree", radius.RoutesAgree);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteStartObject("queues");
                    writer.WriteNumber("runQueue", report.Routed.Count(r => r.Queue == DownloadQueue.RunQueue));
                    writer.WriteNumber("deferredQueue", report.Routed.Count(r => r.Queue == DownloadQueue.DeferredQueue));
                    writer.WriteEndObject();

                    writer.WriteStartObject("budget");
                    writer.WriteNumber("limit", BatchBudget);
                    writer.WriteNumber("objects", examined);
                    writer.WriteBoolean("withinLimit", examined <= BatchBudget);
                    writer.WriteEndObject();

                    writer.WriteString("verdict", report.Verdict);
                    writer.WriteNumber("exitCode", report.ExitCode);
                    writer.WriteEndObject();
                }

                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void WriteArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
        {
            writer.WriteStartArray(name);
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }
            writer.WriteEndArray();
        }

        /// <summary>The verb's own usage, so Program.cs's does not have to grow to carry it.</summary>
        public const string Usage = @"wave-cli route — DB-1: which objects changed, what class each change is, and the blast radius.

  wave-cli route --project <ir-dir> (--drift-check <report.json> --exports-are <meaning> | --changed <name>=<nature>... --changed-from <text>) [--class <name>=<Run|RunInit|Stop>]... [--json]

  --project <ir-dir>       REQUIRED. Supplies each object's KIND — read from the .ir's own
                           declaration, never from its filename or a name prefix — and the
                           UDT/instance edges the blast radius is computed over.

  --drift-check <file>     THE COMPUTED PATH. A `converter drift-check --json` report.
  --exports-are <meaning>  REQUIRED with it: committed-corpus | controller-dump. The report does
                           NOT record whether --complete was passed, and a SKIPPED row means
                           'added since the last export, nothing pending' against a committed
                           corpus and 'this object is NOT in the controller' against a fresh dump.
                           There is no default because the two readings are opposite.

  --changed <name>=<nature>  THE DECLARED PATH. nature = added|deleted|modified|unknown, and
                             'unknown' is a REFUSAL downstream rather than a wildcard.
  --changed-from <text>      REQUIRED with it. A declared change set is a transferred
                             responsibility; say where it came from and whether you took it.

  --class <name>=<class>   The escape for a (kind, nature) DB-1's table has NO ROW for. REFUSED
                           where the table does have one — it is not an override.

EXIT: 0 every object routed · 1 at least one refusal (fail closed) · 2 nothing examined / unusable.
      A change set of zero, an empty corpus and a drift report that compared nothing are all exit 2.
      NOTHING is ever defaulted to RUN.";

        // =================================================================================================
        // *** A SECOND OPTION PARSER, DELIBERATELY. *** Program.Options is a private nested type and
        // this verb lives in its own file so that two lanes can add verbs to this CLI without
        // overwriting each other. Making Options internal would be the tidier change and is the wrong
        // one to make while another agent holds that file open. Same grammar: `--name value` pairs and
        // bare `--flag`s.
        // =================================================================================================

        private sealed class RouteOptions
        {
            private readonly List<KeyValuePair<string, string>> _pairs = new List<KeyValuePair<string, string>>();
            private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

            public static RouteOptions Parse(IReadOnlyList<string> args)
            {
                var options = new RouteOptions();

                for (var i = 1; i < args.Count; i++)
                {
                    if (!args[i].StartsWith("--", StringComparison.Ordinal) && args[i] != "-h")
                    {
                        continue;
                    }

                    if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        options._pairs.Add(new KeyValuePair<string, string>(args[i], args[i + 1]));
                        i++;
                    }
                    else
                    {
                        options._flags.Add(args[i]);
                    }
                }

                return options;
            }

            public string? Value(string name) =>
                _pairs.Where(p => p.Key == name).Select(p => p.Value).FirstOrDefault();

            public IReadOnlyList<string> Many(string name) =>
                _pairs.Where(p => p.Key == name).Select(p => p.Value).ToArray();

            public bool Has(string name) => _flags.Contains(name);

            public bool TryDeclaredClasses(out IReadOnlyDictionary<string, ChangeClass> classes, out string error)
            {
                var map = new Dictionary<string, ChangeClass>(StringComparer.OrdinalIgnoreCase);
                classes = map;
                error = string.Empty;

                foreach (var entry in Many("--class"))
                {
                    var split = entry.Split(new[] { '=' }, 2);
                    if (split.Length != 2 || split[0].Trim().Length == 0)
                    {
                        error = "--class takes <name>=<Run|RunInit|Stop>; got '" + entry + "'.";
                        return false;
                    }

                    ChangeClass parsed;
                    switch (split[1].Trim().ToLowerInvariant())
                    {
                        case "run": parsed = ChangeClass.Run; break;
                        case "runinit": parsed = ChangeClass.RunInit; break;
                        case "stop": parsed = ChangeClass.Stop; break;
                        default:
                            error = "'" + split[1].Trim() + "' is not a change class. Use Run, RunInit or Stop. " +
                                    "'Unknown' is not declarable — it is the state --class exists to leave.";
                            return false;
                    }

                    if (map.ContainsKey(split[0].Trim()))
                    {
                        error = "'" + split[0].Trim() + "' is declared twice in --class.";
                        return false;
                    }

                    map[split[0].Trim()] = parsed;
                }

                return true;
            }
        }
    }

    // =====================================================================================================
    // REPORT MODEL
    // =====================================================================================================

    /// <summary>Everything one <c>route</c> run decided.</summary>
    internal sealed class RouteReport
    {
        public RouteReport(IrCorpus corpus, string provenance)
        {
            Corpus = corpus;
            Provenance = provenance;
            Routed = new List<RoutedRow>();
            Refusals = new List<RefusalRow>();
            BlastRadii = new List<BlastRadiusEntry>();
            NotChanges = new ChangeSetEntry[0];
            Verdict = string.Empty;
        }

        public IrCorpus Corpus { get; }
        public string Provenance { get; }
        public List<RoutedRow> Routed { get; }
        public List<RefusalRow> Refusals { get; }
        public List<BlastRadiusEntry> BlastRadii { get; }
        public IReadOnlyList<ChangeSetEntry> NotChanges { get; set; }
        public int DeclaredCount { get; set; }
        public int DerivedCount { get; set; }
        public string Verdict { get; set; }
        public int ExitCode { get; set; }
    }

    internal sealed class RoutedRow
    {
        public RoutedRow(ClassificationVerdict verdict, DownloadQueue queue, IReadOnlyList<string> dependsOn, IReadOnlyList<string> notInChangeSet)
        {
            Verdict = verdict;
            Queue = queue;
            DependsOn = dependsOn;
            NotInChangeSet = notInChangeSet;
        }

        public ClassificationVerdict Verdict { get; }
        public DownloadQueue Queue { get; }
        public IReadOnlyList<string> DependsOn { get; }
        public IReadOnlyList<string> NotInChangeSet { get; }
    }

    internal sealed class RefusalRow
    {
        public RefusalRow(string name, ObjectKind kind, ChangeNature nature, string stage, string refusal, string reason)
        {
            Name = name;
            Kind = kind;
            Nature = nature;
            Stage = stage;
            Refusal = refusal;
            Reason = reason;
        }

        public string Name { get; }
        public ObjectKind Kind { get; }
        public ChangeNature Nature { get; }
        public string Stage { get; }
        public string Refusal { get; }
        public string Reason { get; }
    }

    internal sealed class BlastRadiusEntry
    {
        public BlastRadiusEntry(
            string typeName,
            IReadOnlyList<string> direct,
            IReadOnlyList<string> viaFbInstance,
            IReadOnlyList<string> all,
            IReadOnlyList<string> alreadyInChangeSet)
        {
            TypeName = typeName;
            Direct = direct;
            ViaFbInstance = viaFbInstance;
            All = all;
            AlreadyInChangeSet = alreadyInChangeSet;
        }

        public string TypeName { get; }
        public IReadOnlyList<string> Direct { get; }
        public IReadOnlyList<string> ViaFbInstance { get; }
        public IReadOnlyList<string> All { get; }
        public IReadOnlyList<string> AlreadyInChangeSet { get; }

        public bool RoutesAgree =>
            Direct.OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(ViaFbInstance.OrderBy(d => d, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }
}
