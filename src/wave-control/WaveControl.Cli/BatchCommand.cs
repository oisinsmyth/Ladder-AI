using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Ladder.Wave.Cli
{
    /// <summary>
    /// THE ENTRY POINT DB-4 DID NOT HAVE — `wave-cli batch`.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WaveBoundaryBatchPlanner"/> and <see cref="BatchPlan"/> were built and unit-tested and
    /// *** NO EXECUTABLE REACHED THEM. *** Checked mechanically before this was written: outside its own
    /// tests, the planner's name occurred in this repository only in prose and in two doc comments
    /// (<see cref="WaveSetAdmission"/>, <see cref="ExcisionClosure"/>) — <c>Admit</c> does not call it,
    /// and <c>src/harness</c> does not reference this assembly at all. So the twenty-object limit was
    /// enforced on nothing that ever ran. A rule with no caller is a rule in the sense a comment is.
    /// </para>
    /// <para>
    /// *** EMPTY IS NOT CLEAN, AND THE DENOMINATOR IS PRINTED ON EVERY PATH. *** A change set of zero
    /// objects is exit 2 with the reason, never a green plan of zero batches — it is exactly what a
    /// broken change tracker produces. Every run prints how many objects it examined and how many the
    /// baseline named, because every other number here is a reason a batch did NOT happen.
    /// </para>
    /// <para>
    /// *** THE LIMIT MAY BE LOWERED AND NEVER RAISED. *** DB-4's twenty is a researched constant [R],
    /// not a tunable, so <c>--max-objects</c> above it is exit 2 before any file is touched. The library
    /// accepts a larger limit so a test can build an over-budget plan cheaply; this verb will not
    /// produce one, and <see cref="OverBudgetBatches"/> is the backstop that fires if the library ever
    /// hands one back anyway.
    /// </para>
    /// </remarks>
    public static class BatchCommand
    {
        private const int ExitPlanned = 0;
        private const int ExitRefused = 1;
        private const int ExitUnusable = 2;

        /// <summary>Run the verb. <paramref name="args"/> is the whole command line, verb included.</summary>
        public static int Run(string[] args) => Run(args, Console.Out, Console.Error);

        /// <summary>
        /// The testable form: the writers are parameters so a test can read what a real invocation would
        /// print, rather than asserting on an exit code that a disconnected check can also produce.
        /// </summary>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            var options = BatchOptions.Parse(args);

            // --- THE LIMIT. Read first, because a refusal here must happen before any file is touched.
            var limit = WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch;
            var limitProvenance = "DB-4's limit [R]: more than twenty changed objects cannot be " +
                                  "integrated consistently in one program cycle.";

            var requested = options.Value("--max-objects");
            if (requested != null)
            {
                int parsed;
                if (!int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < 1)
                {
                    error.WriteLine("BAD INVOCATION: --max-objects must be a whole number of at least 1; got '" +
                                    requested + "'.");
                    return ExitUnusable;
                }

                if (parsed > WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch)
                {
                    error.WriteLine(
                        "REFUSED: --max-objects " + parsed + " is above DB-4's limit of " +
                        WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch + ". The limit may be LOWERED " +
                        "and never RAISED: it is a researched property of the controller, not a budget this " +
                        "tool is free to spend. A single batch larger than twenty is an ERROR here, not a " +
                        "plan — NOTHING WAS EXAMINED.");
                    return ExitUnusable;
                }

                var stated = options.Value("--max-objects-provenance");
                if (string.IsNullOrWhiteSpace(stated))
                {
                    error.WriteLine(
                        "BAD INVOCATION: --max-objects requires --max-objects-provenance. Lowering the limit " +
                        "is a claim somebody made about this download, and a constant nobody can trace is one " +
                        "nobody can re-derive when it moves.");
                    return ExitUnusable;
                }

                limit = parsed;
                limitProvenance = stated.Trim();
            }

            // --- THE BASELINE. Declared empty, or read from a file. Never defaulted.
            var deployedFile = options.Value("--deployed");
            var declaredEmpty = options.Value("--deployed-empty");

            if (deployedFile != null && declaredEmpty != null)
            {
                error.WriteLine(
                    "BAD INVOCATION: --deployed and --deployed-empty are mutually exclusive. Taking both " +
                    "would mean choosing which baseline to believe.");
                return ExitUnusable;
            }

            DeployedProgram deployed;
            if (declaredEmpty != null)
            {
                if (declaredEmpty.Trim().Length == 0)
                {
                    error.WriteLine(
                        "BAD INVOCATION: --deployed-empty takes a REASON. It declares that every dependency " +
                        "is unsatisfied and every changed object must land in the batch, so it is a claim " +
                        "somebody made rather than a default.");
                    return ExitUnusable;
                }

                deployed = DeployedProgram.Empty(declaredEmpty.Trim());
            }
            else if (deployedFile != null)
            {
                var read = BatchInputs.ReadDeployed(deployedFile);
                if (!read.Ok)
                {
                    error.WriteLine("REFUSED: " + read.Refusal);
                    return ExitUnusable;
                }

                deployed = read.Value!;
            }
            else
            {
                error.WriteLine(
                    "BAD INVOCATION: a baseline is required — either --deployed <file> or --deployed-empty " +
                    "<reason>. Without one there is no way to tell a dependency already on the device from a " +
                    "missing one, and assuming the generous answer is how a plan passes that could not be " +
                    "downloaded.");
                return ExitUnusable;
            }

            // --- THE CHANGE SET.
            var changeSetFile = options.Value("--change-set");
            if (changeSetFile == null)
            {
                error.WriteLine("BAD INVOCATION: --change-set <file> is required.");
                return ExitUnusable;
            }

            var changeSet = BatchInputs.ReadChangeSet(changeSetFile);
            if (!changeSet.Ok)
            {
                error.WriteLine("REFUSED: " + changeSet.Refusal);
                return ExitUnusable;
            }

            var objects = changeSet.Value!;
            var plan = WaveBoundaryBatchPlanner.Plan(objects, deployed, limit);

            // --- THE CALLER'S OWN CLOSURE CHECK. The planner already verifies its packer with
            // DependencyClosure; this runs the same verifier again on the plan that came BACK, which is a
            // different question — it catches a plan mutated between planning and reporting, and it gives
            // ClosureViolation a printed surface. *** IT IS NOT AN INDEPENDENT AUTHORITY ON CLOSURE: ***
            // it is the same code, so it cannot disagree with the packer about the DEFINITION. The
            // independent reading of the definition lives in DependencyClosure itself, which is written
            // from the definition rather than from the packer's component reasoning.
            var violations = plan.Outcome == BatchPlanOutcome.Planned
                ? DependencyClosure.Verify(
                    plan.Batches.Select(b => (IEnumerable<string>)b.ObjectNames),
                    objects,
                    deployed)
                : (IReadOnlyList<ClosureViolation>)new ClosureViolation[0];

            var overBudget = OverBudgetBatches(plan);

            var context = new BatchContext(
                changeSetFile,
                changeSet.Provenance,
                deployed,
                limit,
                limitProvenance,
                plan,
                violations,
                overBudget);

            if (options.Flag("--json"))
            {
                // *** NOTHING BUT JSON GOES TO STDOUT ON THIS PATH. *** Measured elsewhere in this repo:
                // `compile --json` emitted a prose paragraph after the object and its output parsed as
                // nothing at all, on every run, for the whole life of the flag.
                output.WriteLine(Json(context));
            }
            else
            {
                output.Write(Text(context));
            }

            if (overBudget.Count > 0 || violations.Count > 0)
            {
                return ExitRefused;
            }

            switch (plan.Outcome)
            {
                case BatchPlanOutcome.Planned: return ExitPlanned;
                case BatchPlanOutcome.Refused: return ExitRefused;
                default: return ExitUnusable;
            }
        }

        /// <summary>
        /// *** THE BACKSTOP: ANY BATCH ABOVE DB-4's TWENTY, WHATEVER LIMIT WAS ASKED FOR. *** Exposed so
        /// it can be RUN rather than read — hand it a plan built at a larger limit (which the library
        /// permits and this verb does not) and it names the batches. A check whose exercise requires
        /// editing the check will not be exercised.
        /// </summary>
        public static IReadOnlyList<DownloadBatch> OverBudgetBatches(BatchPlan plan) =>
            plan == null
                ? new DownloadBatch[0]
                : plan.Batches.Where(b => b.Count > WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch).ToArray();

        /// <summary>Everything one run reports, so the text and the JSON are built from one value.</summary>
        private sealed class BatchContext
        {
            public BatchContext(
                string changeSetPath,
                string changeSetProvenance,
                DeployedProgram deployed,
                int limit,
                string limitProvenance,
                BatchPlan plan,
                IReadOnlyList<ClosureViolation> violations,
                IReadOnlyList<DownloadBatch> overBudget)
            {
                ChangeSetPath = changeSetPath;
                ChangeSetProvenance = changeSetProvenance;
                Deployed = deployed;
                Limit = limit;
                LimitProvenance = limitProvenance;
                Plan = plan;
                Violations = violations;
                OverBudget = overBudget;
            }

            public string ChangeSetPath { get; }
            public string ChangeSetProvenance { get; }
            public DeployedProgram Deployed { get; }
            public int Limit { get; }
            public string LimitProvenance { get; }
            public BatchPlan Plan { get; }
            public IReadOnlyList<ClosureViolation> Violations { get; }
            public IReadOnlyList<DownloadBatch> OverBudget { get; }
        }

        // =================================================================================================
        // THE REPORT
        // =================================================================================================

        private static string Text(BatchContext context)
        {
            var report = new StringBuilder();
            var plan = context.Plan;

            // --- THE DENOMINATOR, FIRST AND ON EVERY OUTCOME. -------------------------------------
            report.AppendLine("EXAMINED: " + plan.ObjectsExamined + " changed object(s) from '" +
                              context.ChangeSetPath + "' [" + context.ChangeSetProvenance + "]");
            report.AppendLine("BASELINE: " +
                              (context.Deployed.DeclaredEmpty
                                  ? "DECLARED EMPTY"
                                  : context.Deployed.Count + " object(s)") +
                              " [" + context.Deployed.Provenance + "]");
            report.AppendLine("LIMIT: " + context.Limit + " object(s) per batch [" + context.LimitProvenance + "]");

            report.AppendLine(plan.ToLogLine());

            foreach (var batch in plan.Batches)
            {
                report.AppendLine("  " + batch.ToLogLine());
            }

            foreach (var refusal in plan.Refusals)
            {
                report.AppendLine("  - " + refusal);
            }

            switch (plan.Outcome)
            {
                case BatchPlanOutcome.Planned:
                    report.AppendLine("CLOSURE: " + context.Violations.Count + " violation(s) over " +
                                      plan.Batches.Count + " batch(es) and " + plan.PlannedObjectCount +
                                      " object(s), re-checked on the returned plan. This is the SAME verifier " +
                                      "the packer used, so it is a check on the PLAN and not on the definition.");
                    foreach (var violation in context.Violations)
                    {
                        report.AppendLine("  ! " + violation);
                    }

                    report.AppendLine("CPU STOP: UNDETERMINED for every batch. Measured 2026-08-13 — a " +
                                      "two-object download left a RUNNING CPU running and a nineteen-object " +
                                      "one required the stop, so the stop is a function of WHAT changed, not " +
                                      "of object count. It is decided at download time (D32), never here.");

                    var resets = plan.Batches.Where(b => b.ResetsData).ToArray();
                    if (resets.Length > 0)
                    {
                        report.AppendLine("RESETS DATA: batch(es) " +
                                          string.Join(", ", resets.Select(b => b.Index.ToString(CultureInfo.InvariantCulture)).ToArray()) +
                                          " carry a RUN (Init) change — retentives included. Every result " +
                                          "obtained before them is invalid (DB-2's validity stamp).");
                    }

                    break;

                case BatchPlanOutcome.NothingToBatch:
                    report.AppendLine("NOTHING TO BATCH - this is not a pass. Zero changed objects is also " +
                                      "what a broken change tracker produces, so it does not share a verdict " +
                                      "with a clean plan (FI-44).");
                    break;
            }

            if (context.OverBudget.Count > 0)
            {
                report.AppendLine("*** OVER BUDGET: " + context.OverBudget.Count + " batch(es) exceed DB-4's " +
                                  WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch + "-object limit: " +
                                  string.Join(", ", context.OverBudget.Select(b =>
                                      "batch " + b.Index + " with " + b.Count).ToArray()) +
                                  ". This is an ERROR, not a report — a download of that set cannot be " +
                                  "integrated consistently in one program cycle. ***");
            }

            return report.ToString();
        }

        private static string Json(BatchContext context)
        {
            var plan = context.Plan;
            var json = new StringBuilder();

            json.Append('{');
            json.Append("\"verb\":\"batch\",");
            json.Append("\"outcome\":\"").Append(plan.Outcome).Append("\",");
            json.Append("\"objectsExamined\":").Append(plan.ObjectsExamined.ToString(CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"changeSet\":").Append(Quote(context.ChangeSetPath)).Append(',');
            json.Append("\"changeSetProvenance\":").Append(Quote(context.ChangeSetProvenance)).Append(',');
            json.Append("\"baselineObjectCount\":").Append(context.Deployed.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"baselineDeclaredEmpty\":").Append(context.Deployed.DeclaredEmpty ? "true" : "false").Append(',');
            json.Append("\"baselineProvenance\":").Append(Quote(context.Deployed.Provenance)).Append(',');
            json.Append("\"maxObjectsPerBatch\":").Append(context.Limit.ToString(CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"maxObjectsPerBatchProvenance\":").Append(Quote(context.LimitProvenance)).Append(',');
            json.Append("\"db4Limit\":").Append(WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch.ToString(CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"batchCount\":").Append(plan.Batches.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
            json.Append("\"plannedObjectCount\":").Append(plan.PlannedObjectCount.ToString(CultureInfo.InvariantCulture)).Append(',');

            json.Append("\"batches\":[");
            json.Append(string.Join(",", plan.Batches.Select(b =>
                "{\"index\":" + b.Index.ToString(CultureInfo.InvariantCulture) +
                ",\"count\":" + b.Count.ToString(CultureInfo.InvariantCulture) +
                ",\"cpuStop\":\"" + b.CpuStop + "\"" +
                ",\"resetsData\":" + (b.ResetsData ? "true" : "false") +
                ",\"objects\":[" + string.Join(",", b.ObjectNames.Select(Quote).ToArray()) + "]}").ToArray()));
            json.Append("],");

            json.Append("\"refusals\":[");
            json.Append(string.Join(",", plan.Refusals.Select(r =>
                "{\"reason\":\"" + r.Reason + "\"" +
                ",\"objects\":[" + string.Join(",", r.ObjectNames.Select(Quote).ToArray()) + "]" +
                ",\"detail\":" + Quote(r.Detail) + "}").ToArray()));
            json.Append("],");

            json.Append("\"closureViolations\":[");
            json.Append(string.Join(",", context.Violations.Select(v =>
                "{\"kind\":\"" + v.Kind + "\"" +
                ",\"object\":" + Quote(v.ObjectName) +
                ",\"dependency\":" + Quote(v.DependencyName) +
                ",\"detail\":" + Quote(v.Detail) + "}").ToArray()));
            json.Append("],");

            json.Append("\"overBudgetBatches\":[");
            json.Append(string.Join(",", context.OverBudget.Select(b =>
                b.Index.ToString(CultureInfo.InvariantCulture)).ToArray()));
            json.Append("],");

            json.Append("\"summary\":").Append(Quote(plan.Summary));
            json.Append('}');

            return json.ToString();
        }

        private static string Quote(string? value)
        {
            var text = value ?? string.Empty;
            var quoted = new StringBuilder(text.Length + 2);
            quoted.Append('"');

            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            quoted.Append(c);
                        }

                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }

        // =================================================================================================

        /// <summary>
        /// The same option grammar <c>Program.Options</c> uses — <c>--name value</c> pairs, a bare
        /// <c>--name</c> is a flag — restated here because that type is private to <c>Program</c>. The
        /// two must not diverge; <c>BatchCommandTests</c> pins the shared forms.
        /// </summary>
        private sealed class BatchOptions
        {
            private readonly List<KeyValuePair<string, string>> _pairs = new List<KeyValuePair<string, string>>();
            private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

            public static BatchOptions Parse(string[] args)
            {
                var options = new BatchOptions();

                for (var i = 1; i < args.Length; i++)
                {
                    if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
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

            /// <summary>
            /// The value, or NULL when the option was not given at all. A bare <c>--name</c> yields the
            /// empty string, which is a different fact from absence and is refused with its own reason
            /// wherever a value is required.
            /// </summary>
            public string? Value(string name) =>
                _pairs.Where(p => p.Key == name).Select(p => p.Value).FirstOrDefault()
                ?? (_flags.Contains(name) ? string.Empty : null);

            public bool Flag(string name) => _flags.Contains(name) || _pairs.Any(p => p.Key == name);
        }
    }

    /// <summary>
    /// Reads the two documents `wave-cli batch` needs — the CHANGE SET and the DEPLOYED BASELINE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE DEPENDENCY LISTS ARE THE WHOLE ANSWER, SO WHERE THEY CAME FROM IS PART OF THE INPUT. ***
    /// <see cref="ChangedObject"/> says outright that dependencies are DECLARED, not computed, and that
    /// a wrong list produces a plan that is wrong. That is the shape D9 forbids for reachable state, and
    /// the same remedy applies: the document must name its own provenance, and one that does not is
    /// REFUSED rather than read. This cannot make a false document true — it can only make an
    /// unattributed one unusable, which removes the case that actually happens, an agent typing the
    /// dependencies it believes it has.
    /// </para>
    /// <para>
    /// *** ABSENT IS NOT EMPTY, AT BOTH LEVELS, AND THE TWO ABSENCES ARE DIFFERENT FAULTS. *** A
    /// document with no <c>objects</c> key at all is not a document describing an empty change set; it
    /// is a document that never said, and that is a REFUSAL here. A document with <c>"objects": []</c>
    /// IS a claim — "nothing changed" — and it is passed to the planner, which reports
    /// <see cref="BatchPlanOutcome.NothingToBatch"/> as its own outcome. The same split governs a field
    /// inside an object: a <c>kind</c> or <c>changeClass</c> that is ABSENT reads as <c>Unknown</c> and
    /// is refused BY THE PLANNER with the planner's reason; a value that is PRESENT and unrecognised is
    /// refused HERE, naming the token, because "you named nothing" and "you named something I do not
    /// know" call for different fixes and must not share a verdict.
    /// </para>
    /// </remarks>
    public static class BatchInputs
    {
        /// <summary>What a read produced, or why it produced nothing.</summary>
        public sealed class Result<T> where T : class
        {
            internal Result(T? value, string provenance, string refusal)
            {
                Value = value;
                Provenance = provenance;
                Refusal = refusal;
            }

            /// <summary>TRUE when the document was read.</summary>
            public bool Ok => Value != null;

            /// <summary>What was read. Meaningful only when <see cref="Ok"/>.</summary>
            public T? Value { get; }

            /// <summary>The document's own statement of where it came from.</summary>
            public string Provenance { get; }

            /// <summary>Why not, when <see cref="Ok"/> is false. Always names the file.</summary>
            public string Refusal { get; }
        }

        /// <summary>
        /// Reads a change set: <c>{ "provenance": "...", "objects": [ { "name", "kind", "changeClass",
        /// "dependsOn": [], "artifactHash" } ] }</c>.
        /// </summary>
        public static Result<IReadOnlyList<ChangedObject>> ReadChangeSet(string path)
        {
            var text = ReadText(path);
            if (text.Refusal.Length > 0)
            {
                return Refuse<IReadOnlyList<ChangedObject>>(text.Refusal);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(text.Text);
            }
            catch (JsonException ex)
            {
                return Refuse<IReadOnlyList<ChangedObject>>("'" + path + "' is not readable JSON: " + ex.Message);
            }

            using (doc)
            {
                var provenance = Provenance(doc.RootElement);
                if (provenance.Length == 0)
                {
                    return Refuse<IReadOnlyList<ChangedObject>>(
                        "'" + path + "' states no `provenance`. Every batching decision hangs off the declared " +
                        "dependency lists in this file, and a list nobody can trace is one nobody can falsify.");
                }

                if (!doc.RootElement.TryGetProperty("objects", out var objects)
                    || objects.ValueKind != JsonValueKind.Array)
                {
                    return Refuse<IReadOnlyList<ChangedObject>>(
                        "'" + path + "' carries no `objects` array. That is not a change set describing no " +
                        "changes — it is a document that never said. An empty change set is written " +
                        "`\"objects\": []`, which IS a claim and is reported as NOTHING TO BATCH.");
                }

                var read = new List<ChangedObject>();
                var index = -1;

                foreach (var element in objects.EnumerateArray())
                {
                    index++;

                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        return Refuse<IReadOnlyList<ChangedObject>>(
                            "'" + path + "' object " + index + " is a " + element.ValueKind +
                            ", not an object. Skipping it would make the change set look smaller than it is.");
                    }

                    var name = Text(element, "name");

                    var kind = ParseEnum<ObjectKind>(element, "kind", out var kindRefusal);
                    if (kindRefusal.Length > 0)
                    {
                        return Refuse<IReadOnlyList<ChangedObject>>("'" + path + "' object " + index +
                            (name.Length == 0 ? string.Empty : " ('" + name + "')") + ": " + kindRefusal);
                    }

                    var changeClass = ParseEnum<ChangeClass>(element, "changeClass", out var classRefusal);
                    if (classRefusal.Length > 0)
                    {
                        return Refuse<IReadOnlyList<ChangedObject>>("'" + path + "' object " + index +
                            (name.Length == 0 ? string.Empty : " ('" + name + "')") + ": " + classRefusal);
                    }

                    var dependsOn = new List<string>();
                    if (element.TryGetProperty("dependsOn", out var declared))
                    {
                        if (declared.ValueKind != JsonValueKind.Array)
                        {
                            return Refuse<IReadOnlyList<ChangedObject>>(
                                "'" + path + "' object " + index + " ('" + name + "') declares `dependsOn` as a " +
                                declared.ValueKind + ". A dependency list read as absent when it was merely " +
                                "malformed produces a plan that looks closed and is not.");
                        }

                        dependsOn.AddRange(declared.EnumerateArray()
                            .Where(d => d.ValueKind == JsonValueKind.String)
                            .Select(d => d.GetString() ?? string.Empty));
                    }

                    read.Add(new ChangedObject(name, kind, changeClass, dependsOn, Text(element, "artifactHash")));
                }

                return new Result<IReadOnlyList<ChangedObject>>(read, provenance, string.Empty);
            }
        }

        /// <summary>
        /// Reads a deployed-program baseline: <c>{ "provenance": "...", "objects": [ "name", ... ] }</c>.
        /// </summary>
        /// <remarks>
        /// An empty <c>objects</c> array is REFUSED here rather than turned into
        /// <see cref="DeployedProgram.Empty(string)"/>. That is <see cref="DeployedProgram"/>'s own rule
        /// enforced at the file boundary: "the device holds nothing" is a declaration a caller makes on
        /// the command line with a reason, never a shape a file happens to have.
        /// </remarks>
        public static Result<DeployedProgram> ReadDeployed(string path)
        {
            var text = ReadText(path);
            if (text.Refusal.Length > 0)
            {
                return Refuse<DeployedProgram>(text.Refusal);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(text.Text);
            }
            catch (JsonException ex)
            {
                return Refuse<DeployedProgram>("'" + path + "' is not readable JSON: " + ex.Message);
            }

            using (doc)
            {
                var provenance = Provenance(doc.RootElement);
                if (provenance.Length == 0)
                {
                    return Refuse<DeployedProgram>(
                        "'" + path + "' states no `provenance`. A baseline decides whether a dependency is " +
                        "already satisfied or has to land in the batch (§9a(c)); one nobody can trace is one " +
                        "nobody can falsify.");
                }

                if (!doc.RootElement.TryGetProperty("objects", out var objects)
                    || objects.ValueKind != JsonValueKind.Array)
                {
                    return Refuse<DeployedProgram>(
                        "'" + path + "' carries no `objects` array, so it says nothing about what is on the " +
                        "device. An empty baseline is declared with --deployed-empty <reason>, never by a " +
                        "file that omits the key.");
                }

                var names = objects.EnumerateArray()
                    .Where(o => o.ValueKind == JsonValueKind.String)
                    .Select(o => o.GetString() ?? string.Empty)
                    .Where(o => o.Trim().Length > 0)
                    .ToArray();

                if (names.Length == 0)
                {
                    return Refuse<DeployedProgram>(
                        "'" + path + "' names no objects. 'the device holds nothing' and 'we could not read " +
                        "what the device holds' are the same empty list and opposite decisions — say which " +
                        "with --deployed-empty <reason> (FI-44).");
                }

                return new Result<DeployedProgram>(DeployedProgram.From(names, provenance), provenance, string.Empty);
            }
        }

        // =================================================================================================

        private static (string Text, string Refusal) ReadText(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return (string.Empty, "no path was given. A file argument that arrived empty is not a file " +
                                      "that could not be read — it is an option nobody filled in.");
            }

            try
            {
                return (File.ReadAllText(path), string.Empty);
            }
            catch (Exception ex) when (ex is IOException
                                       || ex is UnauthorizedAccessException
                                       || ex is ArgumentException
                                       || ex is NotSupportedException)
            {
                return (string.Empty, "could not read '" + path + "': " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static string Provenance(JsonElement root) =>
            root.TryGetProperty("provenance", out var value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty).Trim()
                : string.Empty;

        private static string Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty)
                : string.Empty;

        /// <summary>
        /// An ABSENT key reads as the enum's zero value — <c>Unknown</c> — which every consumer refuses
        /// with its own reason. A PRESENT but unrecognised token is refused HERE, naming it: the two are
        /// different mistakes with different fixes, and <c>Enum.TryParse</c> would also quietly accept
        /// <c>"3"</c> as an ordinal, which is a name nobody wrote.
        /// </summary>
        private static T ParseEnum<T>(JsonElement element, string property, out string refusal) where T : struct, Enum
        {
            refusal = string.Empty;

            var text = Text(element, property).Trim();
            if (text.Length == 0)
            {
                return default;
            }

            if (text.All(char.IsDigit)
                || !Enum.TryParse<T>(text, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(typeof(T), parsed))
            {
                refusal = "`" + property + "` is '" + text + "', which is not one of " +
                          string.Join(", ", Enum.GetNames(typeof(T))) + ". Refused rather than read as " +
                          "Unknown: a token nobody recognises and a field nobody filled in are different " +
                          "mistakes, and only one of them is fixed by editing the producer's table.";
                return default;
            }

            return parsed;
        }

        private static Result<T> Refuse<T>(string reason) where T : class =>
            new Result<T>(null, string.Empty, reason);
    }
}
