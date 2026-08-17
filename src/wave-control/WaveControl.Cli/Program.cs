using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Ladder.Wave;

namespace Ladder.Wave.Cli
{
    /// <summary>
    /// THE DRIVER — the entry point an agent invokes to submit a slot into a wave set and get a
    /// decision back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** EXIT CODES ARE THE CONTRACT, AND THEY FOLLOW THIS REPO'S CONVENTION. *** 0 admitted,
    /// 1 refused, 2 unusable (nothing was checked). A caller that reads only the exit code learns the
    /// verdict; a caller that reads stdout learns why, and what the colouring cost.
    /// </para>
    /// </remarks>
    public static class Program
    {
        private const int ExitAdmitted = 0;
        private const int ExitRefused = 1;
        private const int ExitUnusable = 2;

        /// <summary>Entry point.</summary>
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
                {
                    Usage();
                    return ExitUnusable;
                }

                var options = Options.Parse(args);

                switch (args[0])
                {
                    case "route": return RouteCommand.Run(args, Console.Out, Console.Error);
                    case "submit": return Submit(options);
                    case "status": return Status(options);
                    case "reset": return Reset(options);
                    case "batch": return BatchCommand.Run(args);
                    default:
                        Console.Error.WriteLine("Unknown command '" + args[0] + "'.");
                        Usage();
                        return ExitUnusable;
                }
            }
            catch (WaveStoreContendedException ex)
            {
                // *** RULED 2026-08-14: A LEASE TIMEOUT IS A REFUSAL, NOT AN UNUSABLE STORE. *** Exit 2
                // means "nothing was decided and RETRYING WILL NOT HELP"; here retrying WOULD help,
                // because the contention is transient by construction — so a harness keying on exit 2
                // would give up where it should back off, which is a wrong answer rather than a rough
                // edge. Caught BEFORE WaveStoreException, which keeps exit 2 for a store that is
                // genuinely unusable.
                Console.Error.WriteLine("REFUSED (RETRYABLE): " + ex.Message);
                return ExitRefused;
            }
            catch (WaveStoreException ex)
            {
                Console.Error.WriteLine("STORE UNUSABLE: " + ex.Message);
                return ExitUnusable;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("BAD INVOCATION: " + ex.Message);
                return ExitUnusable;
            }
            catch (Exception ex)
            {
                // *** NOTHING MAY LEAVE THIS PROCESS AS A STACK TRACE. *** Measured 2026-08-14: an
                // UnauthorizedAccessException escaped the lease loop at 48 concurrent agents and four
                // agents died with an unhandled exception and whatever exit code the runtime chose.
                // The reconciliation still closed, so nothing was lost — but a crash is LOUD WITHOUT
                // BEING NAMED, and a campaign harness cannot tell it from a refusal. An unexpected
                // exception is exit 2: nothing was decided, and the caller should not retry blindly.
                Console.Error.WriteLine(
                    "UNEXPECTED (" + ex.GetType().Name + "): " + ex.Message +
                    " — nothing was decided. This is a defect in wave-cli, not a verdict about the " +
                    "submission.");
                return ExitUnusable;
            }
        }

        // =================================================================================================
        // SUBMIT
        // =================================================================================================

        private static int Submit(Options options)
        {
            var startedUtc = DateTimeOffset.UtcNow;
            var clock = Stopwatch.StartNew();

            var slotId = options.Required("--slot");
            var agent = options.Required("--agent");
            var store = options.Store();

            // *** THE COMPUTED PATH AND THE DECLARED PATH ARE MUTUALLY EXCLUSIVE, BY REFUSAL. ***
            // Accepting both would mean silently choosing which to believe, and the one a submitting
            // agent typed is the one D9 forbids.
            var closureFile = options.Value("--reachable-state");
            var declared = options.Many("--reaches");
            var declaredFrom = options.Value("--reaches-from");

            IReadOnlyList<string> reaches;
            string reachesFrom;

            if (closureFile != null)
            {
                if (declared.Count > 0 || declaredFrom != null)
                {
                    Console.Error.WriteLine(
                        "REFUSED: --reachable-state cannot be combined with --reaches / --reaches-from. " +
                        "One is COMPUTED from the IR by `converter reachable-state`, the other is DECLARED by " +
                        "the submitting agent; taking both would mean choosing which to believe, and D9's " +
                        "whole claim is that the choice is not the submitter's to make.");
                    return ExitUnusable;
                }

                var read = ReachableStateDocument.Read(closureFile, options.Required("--reachable-block"));
                if (!read.Ok)
                {
                    Console.Error.WriteLine("REFUSED: " + read.Refusal);
                    return ExitRefused;
                }

                reaches = read.ReachableState;
                reachesFrom = read.Provenance;
            }
            else
            {
                reaches = declared;
                reachesFrom = declaredFrom ?? string.Empty;
            }

            var slot = new TestSlot(
                slotId,
                agent,
                reaches,
                reachesFrom,
                options.Many("--model"),
                options.Int("--width") ?? 0,
                options.Value("--tests-model"));

            using (var lease = store.AcquireLease(agent, TimeSpan.FromMilliseconds(options.Int("--lease-timeout-ms") ?? 30000)))
            {
                var existing = store.Read().ToList();

                if (existing.Any(s => string.Equals(s.Slot.Id, slot.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("REFUSED: a slot named '" + slot.Id + "' is already in the store. " +
                                      "Slot ids are how every edge, wave set and result refers to a slot, so " +
                                      "two of them make the colouring ambiguous.");
                    Emit(lease, clock, startedUtc, existing.Count, null);
                    return ExitRefused;
                }

                var proposed = existing
                    .Concat(new[] { WaveStore.Stored(slot, agent, DateTimeOffset.UtcNow) })
                    .ToList();

                var slots = proposed.Select(s => s.Slot).ToArray();

                // *** EVERY COMPUTED EDGE IS DERIVED HERE, NOT SUPPLIED. *** D9's rule is that
                // independence is computed rather than declared; a driver that accepted an edge list
                // from the submitting agent would let a submitter declare itself independent.
                var edges = SlotConflictDerivation.AllComputedEdges(slots).ToList();
                edges.AddRange(options.Blacklist());

                var cap = options.Int("--cap");
                var capProvenance = options.Value("--cap-provenance");

                var plan = WaveSetAdmission.Admit(slots, edges, cap, capProvenance);

                if (!plan.Usable)
                {
                    Console.WriteLine("REFUSED: " + plan.Summary);
                    Console.WriteLine(plan.Describe());
                    Emit(lease, clock, startedUtc, existing.Count, null);
                    return ExitRefused;
                }

                store.Write(proposed);

                Console.WriteLine("ADMITTED: '" + slot.Id + "' by " + agent + ".");
                ReportColouring(plan, slot.Id, edges);
                Emit(lease, clock, startedUtc, proposed.Count, plan);
                return ExitAdmitted;
            }
        }

        // =================================================================================================
        // STATUS / RESET
        // =================================================================================================

        private static int Status(Options options)
        {
            var store = options.Store();
            var stored = store.Read();

            if (stored.Count == 0)
            {
                Console.WriteLine("NOTHING SUBMITTED at '" + store.Directory + "'. That is not an admitted " +
                                  "wave set — it is a store nobody has put anything in.");
                return ExitUnusable;
            }

            var slots = stored.Select(s => s.Slot).ToArray();
            var edges = SlotConflictDerivation.AllComputedEdges(slots).ToList();
            edges.AddRange(options.Blacklist());

            var plan = WaveSetAdmission.Admit(slots, edges, options.Int("--cap"), options.Value("--cap-provenance"));

            Console.WriteLine("STORE " + store.Directory + " — " + stored.Count + " slot(s):");
            foreach (var s in stored.OrderBy(s => s.Slot.Id, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("  " + s.Slot.Id + " by " + s.SubmittedBy + " at " +
                                  s.SubmittedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            }

            if (!plan.Usable)
            {
                Console.WriteLine(plan.Describe());
                return ExitRefused;
            }

            ReportColouring(plan, null, edges);
            return ExitAdmitted;
        }

        private static int Reset(Options options)
        {
            var store = options.Store();
            var agent = options.Required("--agent");

            using (store.AcquireLease(agent, TimeSpan.FromMilliseconds(options.Int("--lease-timeout-ms") ?? 30000)))
            {
                // *** RESET MUST WORK ON A STORE NOTHING ELSE CAN READ. THAT IS THE ENTIRE POINT OF IT. ***
                // Measured 2026-08-14, and it is the guard I had just added biting the hand that clears
                // it: after 90 kill-mid-write cycles the store was left with its marker present and its
                // slots file gone, Read() correctly REFUSED - and `reset` refused too, because it read
                // the outgoing count first. The deliberate escape was closed by the guard it exists to
                // escape, so the store was unrecoverable through the tool and the only route left was
                // hand-editing files in ProgramData. An over-firing guard does not stay respected; it
                // gets worked around, and the workaround is worse than the state it prevented.
                //
                // So the count is DECORATION and is treated as such. What was destroyed is reported when
                // it is knowable and NAMED AS UNKNOWN when it is not - never quietly printed as 0, which
                // would say "there was nothing to lose" about precisely the case where there may have
                // been a great deal.
                string outgoing;
                try
                {
                    outgoing = store.Read().Count + " slot(s)";
                }
                catch (WaveStoreException ex)
                {
                    outgoing = "an UNKNOWN number of slots (the store could not be read: " + ex.Message + ")";
                }

                store.Clear();
                Console.WriteLine("RESET '" + store.Directory + "': " + outgoing + " removed by " + agent + ".");
                return ExitAdmitted;
            }
        }

        // =================================================================================================
        // THE COLOURING REPORT — the anti-vacuity half
        // =================================================================================================

        /// <summary>
        /// *** REPORTS THE COLOURING, BECAUSE "N AGENTS SUBMITTED" MEASURES NOTHING ON ITS OWN. ***
        /// </summary>
        /// <remarks>
        /// N slots that share an FB instance, a stimulus surface or a reset are ONE SLOT under D9. A
        /// campaign generating N slots against one block and reporting "N concurrent" would be
        /// measuring its own generator. So the driver prints the WAVE COUNT and the ACHIEVED
        /// CONCURRENCY — the largest wave set — and names every separation with the reason that caused
        /// it. *** A RUN THAT REPORTS ONE WAVE IS A RESULT, NOT A FAILURE ***, and it must be
        /// impossible to mistake for a parallel one.
        /// </remarks>
        private static void ReportColouring(WaveSetPlan plan, string? justAdmitted, IReadOnlyList<ConflictEdge> edges)
        {
            var achieved = plan.WaveSets.Count == 0 ? 0 : plan.WaveSets.Max(w => w.SlotCount);

            Console.WriteLine("COLOURING: " + plan.WaveSets.Count + " wave set(s) over " +
                              plan.SlotsExamined + " slot(s); ACHIEVED CONCURRENCY " + achieved +
                              " (the largest wave set).");

            if (plan.WaveSets.Count == 1)
            {
                Console.WriteLine("  ALL SLOTS CO-RUN: one wave set, so nothing was separated.");
            }
            else if (achieved == 1)
            {
                Console.WriteLine("  *** SERIALISED: every wave set holds ONE slot. THIS RUN IS NOT " +
                                  "PARALLEL. *** That is a result, not a failure — but a campaign row " +
                                  "reporting it as N-concurrent would be measuring nothing.");
            }

            foreach (var set in plan.WaveSets)
            {
                Console.WriteLine("  " + set.ToLogLine());
            }

            var separations = edges
                .Where(e => ConflictEdgeKinds.IsStated(e.Kind))
                .OrderBy(e => e.Kind.ToString(), StringComparer.Ordinal)
                .ThenBy(e => e.FirstSlot, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (separations.Length == 0)
            {
                Console.WriteLine("  NO CONFLICT EDGES: the slots are disjoint on every computed relation.");
            }
            else
            {
                Console.WriteLine("  SEPARATED BY " + separations.Length + " edge(s):");
                foreach (var edge in separations)
                {
                    Console.WriteLine("    " + edge.Kind + " " + edge.FirstSlot + " <-> " + edge.SecondSlot +
                                      (edge.Reason.Length == 0 ? string.Empty : ": " + edge.Reason));
                }
            }

            if (justAdmitted != null)
            {
                var itsWave = plan.WaveSets.FirstOrDefault(w =>
                    w.Slots.Any(s => string.Equals(s.Id, justAdmitted, StringComparison.OrdinalIgnoreCase)));

                if (itsWave != null)
                {
                    Console.WriteLine("  '" + justAdmitted + "' is in wave set " + itsWave.Colour +
                                      " with " + (itsWave.SlotCount - 1) + " other slot(s).");
                }
            }

            Console.WriteLine("  " + ModelOrderingPlan.FidelityResidual);
        }

        /// <summary>
        /// Emits the reference figures the owner wants tracked from here on: start, end, elapsed, slot
        /// count, and the concurrency actually achieved — plus what the lease cost, so a serialised
        /// submission is visible rather than merely correct.
        /// </summary>
        private static void Emit(LeaseAcquisition lease, Stopwatch clock, DateTimeOffset startedUtc, int slotCount, WaveSetPlan? plan)
        {
            var achieved = plan == null || plan.WaveSets.Count == 0 ? 0 : plan.WaveSets.Max(w => w.SlotCount);

            Console.WriteLine("MEASURE" +
                              " started=" + startedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) +
                              " ended=" + DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) +
                              " elapsed-ms=" + ((int)clock.Elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) +
                              " slots=" + slotCount.ToString(CultureInfo.InvariantCulture) +
                              " waves=" + (plan == null ? "n/a" : plan.WaveSets.Count.ToString(CultureInfo.InvariantCulture)) +
                              " achieved-concurrency=" + achieved.ToString(CultureInfo.InvariantCulture) +
                              " lease-waited-ms=" + ((int)lease.Waited.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) +
                              " lease-attempts=" + lease.Attempts.ToString(CultureInfo.InvariantCulture) +
                              " lease-contended=" + (lease.Contended ? "yes" : "no"));
        }

        private static void Usage()
        {
            Console.WriteLine(@"wave-cli — drive wave-set admission and slot colouring.

  wave-cli submit --store <dir> --agent <id> --slot <id>
                  --reachable-state <file> --reachable-block <name>
                                             THE COMPUTED PATH. Reads the closure and its provenance
                                             out of `converter reachable-state --project <ir-dir>
                                             --json`. A block whose closure the producer WITHHELD is
                                             a REFUSAL here, never a submission with no reachable
                                             state. Cannot be combined with the two flags below.
                  [--reaches <name>]...      THE DECLARED PATH — the submitting agent states its own
                  [--reaches-from <text>]    closure, which is the shape D9 forbids. Kept for fixtures
                                             and for a corpus the converter cannot read; prefer
                                             --reachable-state. WHERE the closure came from is
                                             required by admission: 'computed and found empty' and
                                             'nobody computed it' are the same empty set and opposite
                                             actions
                  [--model <name>]...        model instances this slot parametrises (D28)
                  [--tests-model <name>]     this slot IS the test of that model (X-I)
                  [--width <registers>]      slot width on the wire
                  [--cap <n> --cap-provenance <text>]
                                             D29's poll-bandwidth cap. REQUIRED to admit, with its
                                             provenance: it is derived from a measured constant that
                                             has already moved once (RTT_p99 173 -> 201)
                  [--blacklist <a>:<b>:<reason>]...  add-only exclusions (D22)
                  [--lease-timeout-ms <n>]

  wave-cli route  --project <ir-dir> ...      DB-1: which objects changed, what class each change
                                              is, and the blast radius. `route --help` for its own
                                              flags.

  wave-cli status --store <dir> [--cap <n> --cap-provenance <text>]
  wave-cli reset  --store <dir> --agent <id>

  wave-cli batch  --change-set <file.json>
                  (--deployed <file.json> | --deployed-empty <reason>)
                  [--max-objects <n> --max-objects-provenance <text>]  LOWER only; above DB-4's
                                             twenty is exit 2 before anything is examined
                  [--json]
                                             DB-4: at most TWENTY DEPENDENCY-CLOSED objects per
                                             wave-boundary download. An empty change set is exit 2
                                             with a reason, never a plan of zero batches.

EXIT: 0 admitted · 1 refused · 2 unusable (nothing was checked).
      A LEASE TIMEOUT IS EXIT 1 AND ITS REASON SAYS 'RETRYABLE' — back off and retry. Exit 2 is
      reserved for a store that cannot be used at all: malformed, inside a worktree, or an argument
      that cannot be honoured. Retrying an exit 2 will not help.

THE STORE IS SHARED AND HAS NO DEFAULT. A per-worktree store is always empty, admits everything and
looks exactly like success — put it beside the claims registry, outside every worktree.");
        }

        // =================================================================================================

        private sealed class Options
        {
            private readonly List<KeyValuePair<string, string>> _pairs = new List<KeyValuePair<string, string>>();
            private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

            public static Options Parse(string[] args)
            {
                var options = new Options();

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

            public string? Value(string name) =>
                _pairs.Where(p => p.Key == name).Select(p => p.Value).FirstOrDefault();

            public IReadOnlyList<string> Many(string name) =>
                _pairs.Where(p => p.Key == name).Select(p => p.Value).ToArray();

            public int? Int(string name)
            {
                var text = Value(name);
                return text == null ? (int?)null : int.Parse(text, CultureInfo.InvariantCulture);
            }

            public string Required(string name) =>
                Value(name) ?? throw new ArgumentException(name + " is required.");

            public WaveStore Store() =>
                new WaveStore(
                    Value("--store") ?? Environment.GetEnvironmentVariable("LADDER_WAVE_STORE"),
                    _flags.Contains("--allow-worktree-store"));

            public IReadOnlyList<ConflictEdge> Blacklist()
            {
                var edges = new List<ConflictEdge>();

                foreach (var entry in Many("--blacklist"))
                {
                    var parts = entry.Split(new[] { ':' }, 3);
                    if (parts.Length < 3)
                    {
                        throw new ArgumentException(
                            "--blacklist takes <slotA>:<slotB>:<reason>. The reason is not optional: " +
                            "§2.5's named failure mode is defensive over-blacklisting, where concurrency " +
                            "collapses toward serial and nobody notices because it still works.");
                    }

                    edges.Add(new ConflictEdge(parts[0], parts[1], ConflictEdgeKind.Blacklist, parts[2]));
                }

                return edges;
            }
        }
    }
}
