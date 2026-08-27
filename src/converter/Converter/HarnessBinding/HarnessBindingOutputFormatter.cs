using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Converter.HarnessBinding;

// The "one record, two renderers" pattern every other command here uses — except that this one's
// second renderer is not a report ABOUT the work, it IS the work: `--emit` writes the binding document
// itself, in `Harness.Gate.BindingDocument`'s wire format.
public static class HarnessBindingOutputFormatter
{
    public static string FormatText(HarnessBindingReport report)
    {
        var sb = new StringBuilder();

        // The denominator first, as every mechanical-floor command states it.
        sb.Append("harness-binding  project=").Append(report.ProjectDir)
            .Append(" (").Append(report.FilesScanned).Append(" file(s) scanned)\n");
        sb.Append("stimulus=").Append(report.StimulusBlock);

        if (report.StimulusInstance.Length > 0)
        {
            sb.Append(" on ").Append(report.StimulusInstance);
        }

        sb.Append("  observed=")
            .Append(report.ObservedBlocks.Count == 0 ? "(none)" : string.Join(",", report.ObservedBlocks))
            .Append("  scope=")
            .Append(report.Scopes.Count == 0 ? "(none)" : string.Join(",", report.Scopes))
            .Append('\n');

        sb.Append("THE HARNESS DRIVES WHAT THE STIMULUS HEAD READS AND OBSERVES EVERYTHING ELSE. "
                 + "A member the owning block also writes is never driven.\n");

        Section(sb, "vectorTargets — the harness DRIVES these", report.VectorTargets);
        Section(sb, "resultSources — the harness OBSERVES these", report.ResultSources);

        if (report.Excluded.Count > 0)
        {
            sb.Append("\nnot carried (").Append(report.Excluded.Count).Append(")\n");

            foreach (var e in report.Excluded.OrderBy(e => e.Path, StringComparer.Ordinal))
            {
                sb.Append("  ").Append(e.Path.PadRight(52)).Append(e.IrType.PadRight(8))
                    .Append(e.Direction.PadRight(9)).Append(Explain(e.Reason)).Append('\n');
            }
        }

        sb.Append("\nserved area: ").Append(report.ServedDenominator).Append('\n');
        sb.Append("registers required by the rows above: ").Append(report.RequiredRegisters);

        if (report.ServedRegisters is int servedWidth)
        {
            // REPORTED, NEVER SUBSTITUTED — BatchPlanner's rule. The two numbers answer different
            // questions: one is what these signals need, the other is what the program serves.
            sb.Append("  (the program serves ").Append(servedWidth)
                .Append(servedWidth >= report.RequiredRegisters ? ", which fits" : ", WHICH IS NARROWER")
                .Append(". The control band and any neighbour's reservation come out of the same window "
                        + "and are NOT counted here — MapAllocator allocates, this only reports.)");
        }

        sb.Append('\n');

        if (report.Holes.Count > 0)
        {
            sb.Append("\nUNRESOLVED HOLES (").Append(report.Holes.Count)
                .Append(") — each is a claim about the plant or a specification, and NONE is defaulted.\n");
            sb.Append("They are emitted into the document under `unresolvedHoles`, which gate 0b REFUSES: "
                      + "the scaffold cannot reach a run until a human has filled them and deleted the key.\n");

            foreach (var hole in report.Holes)
            {
                sb.Append("  ").Append(hole.Field).Append("  (").Append(hole.Paths.Count).Append(")\n");
                sb.Append("      MISSING: ").Append(hole.Missing).Append('\n');
                sb.Append("      RESOLVED BY: ").Append(hole.ResolvedBy).Append('\n');

                foreach (var path in hole.Paths)
                {
                    sb.Append("      @ ").Append(path).Append('\n');
                }
            }
        }

        foreach (var refusal in report.Refusals)
        {
            sb.Append("\nREFUSED: ").Append(refusal.Subject).Append(" — ").Append(refusal.Detail).Append('\n');
        }

        foreach (var warning in report.Warnings)
        {
            sb.Append("WARNING: ").Append(warning).Append('\n');
        }

        sb.Append("\nDERIVED: ").Append(report.VectorTargets.Count).Append(" vector target(s), ")
            .Append(report.ResultSources.Count).Append(" result source(s), ")
            .Append(report.Excluded.Count).Append(" not carried, ")
            .Append(report.Holes.Count).Append(" hole(s), ")
            .Append(report.Refusals.Count).Append(" refusal(s)\n");

        if (report.Refused)
        {
            sb.Append("*** NOTHING MAY BE USED FROM THIS SCAFFOLD. *** A refusal above means the document "
                      + "was not written; a partially-emitted binding is the shape that looks finished.\n");
        }

        if (report.ExaminedNothing)
        {
            sb.Append("NOTHING EXAMINED — this is not a pass: ").Append(report.Scope switch
            {
                BindingScope.UnknownBlock => "a named block is not in this corpus.",
                BindingScope.NothingInScope => "the --scope filters admitted no member.",
                _ => "no member of the named blocks is both typed and used.",
            }).Append('\n');
        }

        if (report.Partial)
        {
            sb.Append("PARTIAL — ").Append(report.Warnings.Count)
                .Append(" file(s) could not be read, so these rows are not a complete statement of the "
                        + "blocks' signals.\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static string Explain(ExclusionReason reason) => reason switch
    {
        ExclusionReason.ObservedInput =>
            "an input of an OBSERVED block — the stimulus model drives it, not the harness",
        ExclusionReason.ReadWriteContention =>
            "read AND written by its own block — offered as an observation, never driven",
        ExclusionReason.Unused => "declared and neither read nor written in this corpus",
        _ => "outside the --scope filters",
    };

    private static void Section(StringBuilder sb, string heading, IReadOnlyList<DerivedSignal> rows)
    {
        sb.Append('\n').Append(heading).Append(" (").Append(rows.Count).Append(")\n");

        foreach (var s in rows)
        {
            sb.Append("  ").Append(s.Tag.PadRight(52)).Append(s.MirrorType.PadRight(6))
                .Append(s.RegisterWidth).Append(" reg");

            if (s.LatchedBy is not null)
            {
                sb.Append("  latchedBy=").Append(s.LatchedBy);
            }

            sb.Append('\n').Append("      latch: ").Append(s.LatchEvidence).Append('\n');
        }
    }

    public static string FormatJson(HarnessBindingReport report) =>
        JsonSerializer.Serialize(new
        {
            project = report.ProjectDir,
            filesScanned = report.FilesScanned,
            stimulusBlock = report.StimulusBlock,
            stimulusInstance = report.StimulusInstance,
            observedBlocks = report.ObservedBlocks,
            scopes = report.Scopes,
            slotId = report.SlotId,
            vectorTargets = report.VectorTargets.Select(Row),
            resultSources = report.ResultSources.Select(Row),
            excluded = report.Excluded.Select(e => new
            {
                path = e.Path,
                type = e.IrType,
                direction = e.Direction,
                reason = e.Reason.ToString(),
                detail = Explain(e.Reason),
            }),
            holes = report.Holes.Select(h => new
            {
                field = h.Field,
                paths = h.Paths,
                missing = h.Missing,
                resolvedBy = h.ResolvedBy,
            }),
            refusals = report.Refusals.Select(r => new { subject = r.Subject, detail = r.Detail }),
            servedBaseByte = report.ServedBaseByte,
            servedRegisters = report.ServedRegisters,
            servedDenominator = report.ServedDenominator,
            requiredRegisters = report.RequiredRegisters,
            counts = new
            {
                vectorTargets = report.VectorTargets.Count,
                resultSources = report.ResultSources.Count,
                excluded = report.Excluded.Count,
                holes = report.Holes.Count,
                refusals = report.Refusals.Count,
            },
            scope = report.Scope.ToString(),
            examinedNothing = report.ExaminedNothing,
            refused = report.Refused,
            partial = report.Partial,
            warnings = report.Warnings,
        }, Indented);

    private static object Row(DerivedSignal s) => new
    {
        tag = s.Tag,
        type = s.MirrorType,
        irType = s.IrType,
        registerWidth = s.RegisterWidth,
        latchedBy = s.LatchedBy,
        latchEvidence = s.LatchEvidence,
        writers = s.Writers,
        readers = s.Readers,
    };

    /// <summary>
    /// THE BINDING DOCUMENT ITSELF, in <c>Harness.Gate.BindingDocument</c>'s wire format.
    ///
    /// <para><b>Only what the corpus states is written.</b> Every other field is ABSENT rather than
    /// null or zero — and absent is exactly what the downstream refusals are written against
    /// (<c>MirrorValueType.Unstated</c>, a blank <c>blockNumber</c>, a missing <c>inertRest</c>), so an
    /// unfinished scaffold fails on the same checks a hand-typed unfinished binding would.</para>
    ///
    /// <para>🔴 <b><c>unresolvedHoles</c> IS NOT AN ANNOTATION AND THAT IS THE POINT.</b> Gate 0b splits
    /// a binding's unmapped keys on the leading underscore: <c>_note</c> is counted, anything else is
    /// REFUSED. So this key makes the scaffold fail closed — the document cannot reach a run while it
    /// is present, and a comment saying the same thing would have been read past. <i>A warning is not a
    /// gate.</i></para>
    /// </summary>
    public static string FormatBinding(HarnessBindingReport report)
    {
        var document = new JsonObject
        {
            ["_generatedBy"] =
                "converter harness-binding - the MECHANICALLY DERIVABLE half only. Nothing here is a claim "
                + "about the plant or about a specification; those are listed in `unresolvedHoles` and are "
                + "the coordinator's to write.",
            ["_derivedFrom"] =
                $"{report.ProjectDir} ({report.FilesScanned} file(s)). Types and RETAIN from the typed signal "
                + "inventory; direction and the writer/reader sites from ProjectUsageGraph - the same "
                + "producers `signal-set`, `candidate-scan` and `undriven-scan` answer from, so this document "
                + "cannot disagree with them. Modbus window: " + report.ServedDenominator,
        };

        // A NON-ANNOTATION KEY, DELIBERATELY: gate 0b refuses it, so the document cannot be run until a
        // human has resolved and removed it.
        if (report.Holes.Count > 0)
        {
            var holes = new JsonArray();

            foreach (var hole in report.Holes)
            {
                holes.Add(new JsonObject
                {
                    ["field"] = hole.Field,
                    ["paths"] = new JsonArray(hole.Paths.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()),
                    ["missing"] = hole.Missing,
                    ["resolvedBy"] = hole.ResolvedBy,
                });
            }

            document["unresolvedHoles"] = holes;
        }

        // The two document-level facts the corpus DOES state — and only when served-area derived them.
        // NOT DERIVED is carried as an absence, never as a number: exit 2 there is never a pass, and a
        // width written from a refused derivation is the confident wrong answer the whole command
        // exists to avoid.
        if (report.ServedBaseByte is int baseByte)
        {
            document["baseByte"] = baseByte;
        }

        if (report.ServedRegisters is int registers)
        {
            document["declaredRegisters"] = registers;
        }

        var slot = new JsonObject
        {
            ["slotId"] = report.SlotId,
            ["_registerOrder"] =
                "*** ARRAY POSITION IS REGISTER POSITION, and this order was chosen by the scaffold "
                + "(ordinal by tag), not read off a deployed layer. Re-ordering these lists at the next "
                + "deploy moves signals to different registers with nothing to flag it - the failure the "
                + "committed binding's own `_resultSourceOrder` note records. Confirm or replace this "
                + "order before generating a copy layer; see the `registerOrder` hole.",
            ["vectorTargets"] = Signals(report.VectorTargets),
            ["resultSources"] = Signals(report.ResultSources),
        };

        document["slots"] = new JsonArray { slot };

        return document.ToJsonString(BindingJson);
    }

    private static JsonArray Signals(IReadOnlyList<DerivedSignal> rows)
    {
        var array = new JsonArray();

        foreach (var s in rows)
        {
            var row = new JsonObject
            {
                ["tag"] = s.Tag,
                ["type"] = s.MirrorType,
            };

            // Written ONLY when the corpus proves a latch. Absent is the positive claim "this binding
            // claims no latch", so a speculative value here would be an invention wearing a fact's
            // clothes — and the evidence rides along so a reader can check it without re-deriving.
            if (s.LatchedBy is not null)
            {
                row["latchedBy"] = s.LatchedBy;
                row["_latchEvidence"] = s.LatchEvidence;
            }

            array.Add(row);
        }

        return array;
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // The emitted BINDING is edited by a person - they fill the holes in by hand - so it is written
    // with relaxed escaping. The default encoder turns every backtick into ` and every dash into
    // —, which is correct JSON and unreadable prose, and the citations in these holes are the
    // half a reader has to be able to read. Not used for the --json REPORT, which is machine input.
    private static readonly JsonSerializerOptions BindingJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
