using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.ServedArea;

/// <summary>
/// 🔴 <b>THE WIDTH, READ OFF THE BLOCK THAT ACTUALLY SERVES IT — AND READ TWICE.</b>
///
/// <para><b>The defect.</b> A harness binding's <c>declaredRegisters</c> was authored by hand, per
/// lane. It flows into <c>MirrorGeometry</c>, into the map allocator, into <c>RegisterMap.MapHash</c>
/// and therefore into the build stamp — so the stamp does hash a declared width. What nothing checked
/// is whether that number is <b>true of the program</b>. A hand-typed derivable field is only an
/// opportunity to disagree with reality.</para>
///
/// <para><b>The truth lives in the program, in TWO PLACES THAT CAN SILENTLY DISAGREE.</b> The readable
/// statement <c>MB_SERVER(..., MB_HOLD_REG := P#M1000.0 WORD 37, ...)</c> and the sidecar constant
/// backing it, joined by the UId the fixed-shape port cites. <c>to-xml</c> rebuilds the operand from
/// the SIDECAR, so a readable line that drifted is invisible to every existing check and shows up only
/// when the controller serves a different area than the map was allocated against. Both are read here,
/// and a disagreement is a refusal naming both lines.</para>
///
/// <para>*** THE DIRECTION OF ERROR IS NOT SYMMETRIC AND IS CHOSEN DELIBERATELY. *** Deriving a
/// NARROWER area than is served costs a refusal on a map that would have fitted. Deriving a WIDER one
/// lets a map that overflows the real Modbus window allocate cleanly and fail on the wire as a device
/// fault — a symptom already recorded from the other side. So every uncertainty here REFUSES: a unit
/// that is not WORD, an area that is not marker memory, a bit offset inside the byte, a missing
/// sidecar, a file that would not parse, a second <c>MB_SERVER</c> call. None of them is guessed.</para>
///
/// <para>🔴 <b>WHAT THIS CANNOT POSSIBLY SEE: WHETHER THE BLOCK IT READ IS THE BLOCK ON THE
/// CONTROLLER.</b> It reads the corpus, not the CPU. The mirror's widening to 1024 registers was
/// proven by probing the device from both sides, and nothing in this file substitutes for that. What
/// it buys is exactly one thing and the claim must not be widened: <b>a binding can no longer disagree
/// with the program that was staged.</b> A corpus that is stale with respect to the controller derives
/// a confident, agreed, wrong number, and is indistinguishable from a fresh one.</para>
/// </summary>
public static class ServedAreaRunner
{
    /// <summary>The one instruction that serves a holding-register area, and its one area port.</summary>
    private const string Instruction = "MB_SERVER";
    private const string Port = "MB_HOLD_REG";

    /// <summary>
    /// The unit this producer will convert to a register count. <b>WORD only, deliberately.</b> A
    /// register is a word, so <c>WORD n</c> is <c>n</c> registers with nothing to round and nothing to
    /// assume. <c>BYTE</c> would need a rounding rule, and a rounding rule chosen without a real export
    /// to ground it is exactly the invention this project refuses — so an unrecognised unit is named and
    /// refused, and widening this is a change with a measurement behind it.
    /// </summary>
    private const string Unit = "WORD";

    public static ServedAreaReport Run(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var files = new List<string>();
        var missing = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateFiles(path, "*.ir", SearchOption.TopDirectoryOnly));
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                // FI-44. A path naming nothing used to resolve to an empty sequence and vanish, leaving
                // a scan that examined less than it was asked to and said so nowhere.
                missing.Add(path);
            }
        }

        files = files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            return ServedAreaReport.Refuse(files.Count, 0,
                $"{missing.Count} scope path(s) name nothing on disk: {string.Join(", ", missing)}. A scope that "
                + "matched nothing is not a corpus with no comms block in it — the two are the same output and "
                + "different facts, so the question is refused rather than answered from the part that resolved.");
        }

        var candidates = new List<Candidate>();
        var unreadable = new List<string>();
        var blocks = 0;

        foreach (var file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException e)
            {
                unreadable.Add($"{file}: could not be read ({e.Message})");
                continue;
            }

            // Non-code objects legitimately carry no networks. Matched positively by prefix, the same
            // dispatch ProjectUsageGraph uses, so "this is a DB" and "this file declares nothing at all"
            // stay different observations.
            if (text.StartsWith("DB ", StringComparison.Ordinal)
                || text.StartsWith("TYPE ", StringComparison.Ordinal)
                || text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                continue;
            }

            IrBlock block;
            IReadOnlyList<NetworkSidecar> sidecars;
            try
            {
                if (IrParser.HasSidecarSection(text))
                {
                    (block, sidecars) = IrParser.ParseBlock(text);
                }
                else
                {
                    block = IrParser.ParseBlockWithoutSidecar(text);
                    sidecars = Array.Empty<NetworkSidecar>();
                }
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                           or NonReducibleNetworkException or IrFormatException)
            {
                unreadable.Add($"{file}: not indexed ({ex.GetType().Name}: {ex.Message})");
                continue;
            }

            blocks++;
            candidates.AddRange(CandidatesIn(file, text, block, sidecars));
        }

        // 🔴 A FILE THAT DID NOT PARSE BREAKS BOTH CLAIMS THIS PRODUCER CAN MAKE. It could hold the
        // MB_SERVER call ("none at all" becomes false) or a SECOND one ("exactly one" becomes false).
        // Refused rather than answered from the rest: an answer computed over part of a corpus is a
        // positive claim about the whole of it.
        if (unreadable.Count > 0)
        {
            return ServedAreaReport.Refuse(files.Count, blocks,
                $"{unreadable.Count} of {files.Count} file(s) could not be read, so the corpus is PARTIAL: "
                + string.Join("; ", unreadable)
                + ". An unread file can hold the MB_SERVER call, or a second one — both 'exactly one' and "
                + "'none at all' depend on having read everything, so neither is claimed here.");
        }

        if (candidates.Count == 0)
        {
            return ServedAreaReport.NotDerived(files.Count, blocks, "no MB_SERVER call");
        }

        if (candidates.Count > 1)
        {
            return ServedAreaReport.Refuse(files.Count, blocks,
                $"the corpus contains {candidates.Count} MB_SERVER call(s): "
                + string.Join("; ", candidates.Select(c => $"{c.Block} at {c.File}:{c.ReadableLine} serving {c.ReadableText}"))
                + ". Which one serves the harness mirror is not derivable from the corpus, and picking one would "
                + "invent the answer — the map would then be checked against an area it does not live in.");
        }

        var only = candidates[0];

        if (only.Problem is { } problem)
        {
            return ServedAreaReport.Refuse(files.Count, blocks, problem);
        }

        if (!string.Equals(only.ReadableText, only.SidecarText, StringComparison.Ordinal))
        {
            return ServedAreaReport.Refuse(files.Count, blocks,
                $"the two homes of the served width DISAGREE. The readable statement at {only.File}:{only.ReadableLine} "
                + $"says `{only.ReadableText}`; the sidecar constant backing it at {only.File}:{only.SidecarLine} says "
                + $"`{only.SidecarText}`. `to-xml` rebuilds the operand from the SIDECAR, so the block that reaches the "
                + "controller serves the second of those and every reader of the IR sees the first. Reconcile them in "
                + "the block — a producer that picked one would make the wrong half authoritative half the time.");
        }

        if (!TryParsePointer(only.ReadableText, out var pointer, out var why))
        {
            return ServedAreaReport.Refuse(files.Count, blocks,
                $"the MB_HOLD_REG area pointer at {only.File}:{only.ReadableLine} is `{only.ReadableText}`, and {why} "
                + "Refused rather than interpreted: a served width guessed WIDE allocates a map that overflows the real "
                + "Modbus window and fails on the wire as a device fault.");
        }

        return new ServedAreaReport(
            Derived: true,
            pointer.Area,
            pointer.BaseByte,
            pointer.Count,
            only.Block,
            only.File,
            only.ReadableLine,
            only.SidecarLine,
            only.ReadableText,
            only.SidecarText,
            files.Count,
            blocks,
            Array.Empty<string>(),
            NotDerivedReason: string.Empty,
            Array.Empty<string>());
    }

    /// <param name="Problem">
    /// Non-null when the call was found but one of its two homes could not be read. Carried rather than
    /// thrown so the AMBIGUITY check still sees the call — a second MB_SERVER whose sidecar is missing
    /// is still a second MB_SERVER.
    /// </param>
    private sealed record Candidate(
        string File, string Block, int Network, string ReadableText, string SidecarText,
        int ReadableLine, int SidecarLine, string? Problem);

    private static IEnumerable<Candidate> CandidatesIn(
        string file, string text, IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var network in block.Networks)
        {
            foreach (var shape in network.FixedShapes.Where(f => string.Equals(f.Instruction, Instruction, StringComparison.Ordinal)))
            {
                var readable = shape.Arguments
                    .FirstOrDefault(a => string.Equals(a.Port, Port, StringComparison.Ordinal))
                    ?.Binding as PortBinding.Value;

                if (readable?.Expr is not Expr.Literal literal)
                {
                    yield return new Candidate(file, block.Name, network.Number, string.Empty, string.Empty, 0, 0,
                        $"{block.Name} network {network.Number} calls MB_SERVER but its {Port} port is not a literal area "
                        + "pointer, so the served width is not stated in the block at all. Nothing to derive and nothing "
                        + "to check the binding against.");
                    continue;
                }

                var readableText = literal.Value.Trim();
                var readableLine = LineOf(lines, readableText, sidecarConstant: false);

                var sidecarText = SidecarPointer(sidecars, network.Number, shape.InstancePath, out var sidecarProblem);
                if (sidecarText is null)
                {
                    yield return new Candidate(file, block.Name, network.Number, readableText, string.Empty, readableLine, 0,
                        $"{block.Name} network {network.Number} serves `{readableText}` at {file}:{readableLine}, but "
                        + $"{sidecarProblem} The width has two homes in the IR and both must be read back; one of them "
                        + "read is not a check, it is the single source of truth this producer exists to remove.");
                    continue;
                }

                yield return new Candidate(
                    file, block.Name, network.Number, readableText, sidecarText,
                    readableLine, LineOf(lines, sidecarText, sidecarConstant: true), null);
            }
        }
    }

    /// <summary>
    /// The sidecar's half of the number: the constant entry the fixed-shape port's UId points at. The
    /// JOIN IS THE UID, never position — a positional match would silently pair the port with whichever
    /// constant happened to be listed first.
    /// </summary>
    private static string? SidecarPointer(
        IReadOnlyList<NetworkSidecar> sidecars, int network, string instancePath, out string problem)
    {
        var networkSidecar = sidecars.FirstOrDefault(s => s.NetworkNumber == network);
        if (networkSidecar is null)
        {
            problem = "the block carries no sidecar for that network (a readable-only IR), so the second home of the "
                + "number does not exist to be compared.";
            return null;
        }

        var shapes = networkSidecar.FixedShapes
            .Where(f => string.Equals(f.Instruction, Instruction, StringComparison.Ordinal))
            .ToList();

        var shape = shapes.Count == 1
            ? shapes[0]
            : shapes.FirstOrDefault(f => string.Equals(string.Join('.', f.InstanceComponentPath), instancePath, StringComparison.Ordinal));

        if (shape is null)
        {
            problem = $"the sidecar for that network carries {shapes.Count} MB_SERVER entr(y/ies) and none of them is "
                + $"the instance `{instancePath}`, so the readable call cannot be joined to its constant.";
            return null;
        }

        if (shape.Arguments.FirstOrDefault(a => string.Equals(a.Port, Port, StringComparison.Ordinal))?.Binding
            is not PortBindingSidecar.Literal wired)
        {
            problem = $"the sidecar does not record the {Port} port as a literal constant, so there is no UId to look "
                + "the second copy of the width up by.";
            return null;
        }

        var constant = networkSidecar.ConstantUIds.FirstOrDefault(c => c.UId == wired.ConstantUId);
        if (constant is null)
        {
            problem = $"the sidecar's {Port} port cites constant UId {wired.ConstantUId}, and no constant of that UId is "
                + "declared in the network's sidecar.";
            return null;
        }

        problem = string.Empty;
        return constant.Value.Trim();
    }

    /// <summary>
    /// Where a pointer text sits in the file, 1-based, for a refusal that can be opened. Plain substring
    /// matching over the already-parsed text — the FACT came from the parser, this only locates it, and
    /// the two halves are told apart by whether the line is the sidecar's <c>constant</c> declaration.
    /// </summary>
    private static int LineOf(IReadOnlyList<string> lines, string pointer, bool sidecarConstant)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains(pointer, StringComparison.Ordinal))
            {
                continue;
            }

            if (lines[i].TrimStart().StartsWith("constant ", StringComparison.Ordinal) == sidecarConstant)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// <c>P#M1000.0 WORD 37</c> → marker memory, byte 1000, bit 0, 37 registers. Every shape this does
    /// not recognise is reported by name rather than approximated.
    /// </summary>
    internal static bool TryParsePointer(string text, out ServedAreaPointer pointer, out string why)
    {
        pointer = new ServedAreaPointer(text, string.Empty, 0, 0, string.Empty, 0);

        if (!text.StartsWith("P#", StringComparison.Ordinal))
        {
            why = "it is not an area pointer at all.";
            return false;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            why = $"it does not have the three parts (<address> <unit> <count>) an area pointer has; it has {parts.Length}.";
            return false;
        }

        var address = parts[0][2..];
        if (address.Length < 3 || address[0] != 'M')
        {
            why = $"`{address}` is not marker memory. The harness mirror is a %M area and this producer derives only "
                + "that; an area in a data block is a different address space and translating it would be an assumption.";
            return false;
        }

        var dot = address.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0
            || !int.TryParse(address[1..dot], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var baseByte)
            || !int.TryParse(address[(dot + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var bit))
        {
            why = $"`{address}` is not a <byte>.<bit> marker address.";
            return false;
        }

        if (bit != 0)
        {
            why = $"it starts at bit {bit} inside byte {baseByte}, and a register area is byte-aligned. What a "
                + "bit-offset holding-register area serves is not something to assume.";
            return false;
        }

        if (!string.Equals(parts[1], Unit, StringComparison.Ordinal))
        {
            why = $"its unit is `{parts[1]}`, not {Unit}. A register is a word, so only {Unit} converts to a register "
                + "count with nothing rounded and nothing assumed; widening this needs a real export to ground the rule.";
            return false;
        }

        if (!int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            why = $"`{parts[2]}` is not a positive count of {Unit}s.";
            return false;
        }

        pointer = new ServedAreaPointer(text, "M", baseByte, bit, Unit, count);
        why = string.Empty;
        return true;
    }
}
