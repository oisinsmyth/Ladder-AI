using Converter.Ir;
using Converter.SimaticMl;

namespace Converter;

/// <summary>
/// Reduces a Contact/Coil FlgNetwork to the readable IR form (ADR-0001): one or more
/// independent series chains (Powerrail -> Contact -> Contact -> ... -> Coil), bundled in one
/// network. Two real-world shapes confirmed against actual exports (2026-07-10), both handled
/// explicitly rather than guessed at:
///   - Multiple independent chains sharing one network, with no wiring between them (a
///     16-independent-rung alarm-bit network).
///   - The rail connection itself is commonly one wire with many endpoints — Powerrail plus
///     every chain's first element — not one wire per chain. This is not fan-out that breaks
///     series-purity; it's just a shared source, so it's tracked separately from each chain's
///     own private wires (see <see cref="CoilAssignmentSidecar"/>).
///   - An empty network (source `&lt;NetworkSource /&gt;`, no content at all) reduces to zero assignments.
///
/// Deliberately narrow otherwise: real OR/parallel-branch detection *within* a single chain is
/// real complexity (ADR-0001's "Consequences" section) that this slice's fixtures don't
/// exercise. Anything that isn't a clean series chain throws
/// <see cref="NonReducibleNetworkException"/> rather than guessing — the seam for overall-plan
/// item 7's explicit-form fallback, not built yet since nothing here exercises it.
/// </summary>
public static class GraphReducer
{
    public static ReducedNetwork Reduce(FlgNetwork network, int networkNumber, string title, string compileUnitUId)
    {
        if (network.AccessNodes.Count == 0 && network.Parts.Count == 0 && network.Wires.Count == 0)
        {
            var emptyNetwork = new IrNetwork(networkNumber, title, Array.Empty<CoilAssignment>());
            var emptySidecar = new NetworkSidecar(networkNumber, compileUnitUId, Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
            return new ReducedNetwork(emptyNetwork, emptySidecar);
        }

        var accessByUId = network.AccessNodes.ToDictionary(a => a.UId);
        var constantsByUId = network.Constants.ToDictionary(c => c.UId);
        var wiresByPort = BuildPortIndex(network.Wires);

        var tonParts = network.Parts.Where(p => p.Name == "TON").ToList();
        var coils = network.Parts.Where(p => p.Name == "Coil").ToList();
        if (coils.Count == 0 && tonParts.Count == 0)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: no Coil or TON found.");
        }

        var assignments = new List<CoilAssignment>();
        var assignmentSidecars = new List<CoilAssignmentSidecar>();
        var timerBindings = new List<TimerBinding>();
        var timerSidecars = new List<TimerBindingSidecar>();
        var allAccessEntries = new List<SidecarAccessEntry>();
        var allConstantEntries = new List<SidecarConstantEntry>();
        var visitedWireUIds = new HashSet<int>();

        // Timers are reduced first (order doesn't affect correctness — a Coil's own chain can
        // only reference a TON's Q via an ordinary Access elsewhere, see the note on
        // ChainStepSidecar, never via direct wire-graph traversal into this pass — but doing
        // timers first keeps their statements first in emitted IR, reads naturally).
        foreach (var ton in tonParts)
        {
            var (binding, sidecar, accessEntries, constantEntries) =
                ReduceTimer(network, ton, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            timerBindings.Add(binding);
            timerSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var coil in coils)
        {
            var (assignment, sidecar, accessEntries, constantEntries) =
                ReduceOneChain(network, coil, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            assignments.Add(assignment);
            assignmentSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        if (visitedWireUIds.Count != network.Wires.Count)
        {
            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: {network.Wires.Count - visitedWireUIds.Count} wire(s) unaccounted for after " +
                "reduction — unexpected topology, refusing to silently drop structure.");
        }

        var irNetwork = new IrNetwork(networkNumber, title, assignments, timerBindings);
        var networkSidecar = new NetworkSidecar(networkNumber, compileUnitUId, allAccessEntries, assignmentSidecars, allConstantEntries, timerSidecars);
        return new ReducedNetwork(irNetwork, networkSidecar);
    }

    private static (CoilAssignment Assignment, CoilAssignmentSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceOneChain(
        FlgNetwork network,
        PartNode coil,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();
        var (condition, steps, railWireUId) = TraceChain(
            network, (coil.UId, "in"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (coilTag, coilOperandWireUId) = ResolveOperand(wiresByPort, accessByUId, coil.UId, networkNumber);
        visitedWireUIds.Add(coilOperandWireUId);
        AddAccessEntry(accessEntries, coilTag);

        var assignment = new CoilAssignment(coilTag.TagPath, condition);
        var sidecar = new CoilAssignmentSidecar(railWireUId, steps, coil.UId, coilTag.UId, coilOperandWireUId);

        return (assignment, sidecar, accessEntries, constantEntries);
    }

    // A TON's IN is reduced exactly like a Coil's condition — same backward trace, terminating
    // at the TON's own "IN" port instead of a Coil's "in" (and, like a Coil's chain, may itself
    // terminate at another TON's Q instead of the rail — see TraceChain). PT is fed by an
    // IdentCon directly (no chain — it's a single operand, tag or literal), unlike Coil's own
    // "operand" pattern only in that there's no intervening Contact-chain concept for it. `Q`
    // itself is deliberately *not* validated here — whether/how it's consumed (an ordinary
    // Access elsewhere, confirmed real FC ControlDelays; or a direct wire into another chain,
    // confirmed real FC TimerSample) is entirely the consuming chain's concern via TraceChain;
    // this reduction only owns IN/PT/ET.
    private static (TimerBinding Binding, TimerBindingSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceTimer(
        FlgNetwork network,
        PartNode ton,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (inExpr, inSteps, inRailWireUId) = TraceChain(
            network, (ton.UId, "IN"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (ptExpr, presetSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, ton.UId, "PT", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var et = ResolveOptionalOutputPort(wiresByPort, ton.UId, "ET", networkNumber, visitedWireUIds);

        var instance = ton.Instance
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no Instance reference.");
        var instancePath = string.Join('.', instance.ComponentPath);

        var binding = new TimerBinding(instancePath, inExpr, ptExpr);
        var sidecar = new TimerBindingSidecar(
            ton.UId,
            ton.TonVersion ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no Version."),
            ton.TimeType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no time_type."),
            instance.UId,
            instance.Scope,
            instance.ComponentPath,
            inRailWireUId,
            inSteps,
            presetSidecar,
            et);

        return (binding, sidecar, accessEntries, constantEntries);
    }

    // Resolves a single IdentCon-fed operand (no chain, unlike a boolean chain position) that's
    // either an ordinary tag (AccessNode) or a literal constant (ConstantAccessNode) — used for
    // a TON's `PT` (confirmed real 2026-07-11, FB MotorDOL / FC ControlDelays) and, since the
    // same date, a comparison's `in1`/`in2` (FC ControlDelays) — one shared resolver rather than
    // duplicating the tag-vs-constant branch per caller, parameterized by which port to resolve.
    private static (Expr Expr, OperandSidecar Sidecar) ResolveTagOrLiteralOperand(
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int partUId,
        string port,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries)
    {
        var wire = RequireWireAt(wiresByPort, (partUId, port), networkNumber);
        var identCon = wire.Endpoints.FirstOrDefault(e => e.Kind == EndpointKind.IdentCon)
            ?? throw new NonReducibleNetworkException(
                $"Network {networkNumber}: {port} wire {wire.UId} for UId={partUId} has no IdentCon source.");

        visitedWireUIds.Add(wire.UId);

        if (accessByUId.TryGetValue(identCon.UId!.Value, out var access))
        {
            AddAccessEntry(accessEntries, new SidecarAccessEntry(access.DottedPath, access.UId, access.Scope));
            return (new Expr.TagRef(access.DottedPath), new OperandSidecar.TagOperand(access.UId, wire.UId));
        }

        if (constantsByUId.TryGetValue(identCon.UId!.Value, out var constant))
        {
            AddConstantEntry(constantEntries, new SidecarConstantEntry(constant.Value, constant.UId, constant.ConstantType));
            return (new Expr.Literal(constant.Value), new OperandSidecar.LiteralOperand(constant.UId, wire.UId));
        }

        throw new NonReducibleNetworkException(
            $"Network {networkNumber}: {port} wire {wire.UId} for UId={partUId} references unknown Access/Constant UId={identCon.UId}.");
    }

    private static void AddConstantEntry(List<SidecarConstantEntry> entries, SidecarConstantEntry entry)
    {
        if (!entries.Any(e => e.UId == entry.UId))
        {
            entries.Add(entry);
        }
    }

    // IR-text infix operator per Part Name — only Eq/Ge confirmed real (ir/SPEC.md's readable-form
    // table already sketches the full IEC family, but Ne/Le/Gt/Lt Part Names are unconfirmed, so
    // only these two are reachable — SupportedComparisonPartNames gates this at parse time).
    private static string ComparisonOperator(string partName) => partName switch
    {
        "Eq" => "=",
        "Ge" => ">=",
        _ => throw new UnsupportedConstructException($"Unsupported comparison Part Name '{partName}'."),
    };

    // ET is an optional output port — confirmed real, 2026-07-11: entirely absent from <Wires>,
    // or wired to OpenCon (FB MotorDOL). A wire to any other endpoint is refused — no live
    // example of a *used* ET exists yet (unlike Q, which TraceChain now handles as a genuine
    // chain leaf when wired directly to a consumer — see ChainStepSidecar.TimerOutputStep).
    private static OpenConnectionSidecar? ResolveOptionalOutputPort(
        Dictionary<(int, string), WireNode> wiresByPort,
        int tonUId,
        string port,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        if (!wiresByPort.TryGetValue((tonUId, port), out var wire))
        {
            return null;
        }

        var others = wire.Endpoints
            .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == tonUId && e.PortName == port))
            .ToList();

        if (others.Count != 1 || others[0].Kind != EndpointKind.OpenCon)
        {
            throw new UnsupportedConstructException(
                $"Network {networkNumber}: TON UId={tonUId}'s '{port}' port is wired to a consumer — only an unconnected " +
                $"(absent or OpenCon) '{port}' is supported this phase; reading a TON's output back only works via an " +
                "ordinary Access elsewhere.");
        }

        visitedWireUIds.Add(wire.UId);
        return new OpenConnectionSidecar(wire.UId, others[0].UId!.Value);
    }

    private static void AddAccessEntry(List<SidecarAccessEntry> entries, SidecarAccessEntry entry)
    {
        if (!entries.Any(e => e.TagPath == entry.TagPath && e.UId == entry.UId))
        {
            entries.Add(entry);
        }
    }

    // Shared backward trace from a starting port (a Coil's "in", or a TON's "IN") to the rail —
    // one position at a time, rail-to-coil order once reversed. Each position is a single
    // Contact or comparison (Eq/Ge — confirmed real, 2026-07-11, FC ControlDelays; both are
    // pass-through positions, chain continues further upstream), an OR-merge (Part Name="O"), or
    // an already-defined TON's Q output — the last confirmed real, 2026-07-11, `FC TimerSample`
    // (purpose-built by the project owner to close this gap: Q wired directly into a plain
    // Coil). OR-merge and TON-via-Q are both always rail-facing/terminal — nothing further
    // upstream to trace within *this* chain. When a TON's Q terminates the chain, the chain
    // never touches Powerrail at all, so RailWireUId comes back null (see TimerOutputStep's doc
    // comment). Used identically by ReduceOneChain and ReduceTimer: a TON's IN is reduced
    // exactly the same way a Coil's condition is, just terminating at a different port name.
    private static (Expr Condition, List<ChainStepSidecar> Steps, int? RailWireUId) TraceChain(
        FlgNetwork network,
        (int UId, string Port) startPort,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries)
    {
        var steps = new List<ChainStepSidecar>();
        var stepExprs = new List<Expr>();
        var currentInPort = startPort;
        int? railWireUId = null;

        while (true)
        {
            var wire = RequireWireAt(wiresByPort, (currentInPort.UId, currentInPort.Port), networkNumber);

            if (wire.Endpoints.Any(e => e.Kind == EndpointKind.Powerrail))
            {
                // Reached the rail. Other endpoints on this same wire (if any) belong to
                // sibling chains' first elements — not this chain's concern, not fan-out.
                visitedWireUIds.Add(wire.UId);
                railWireUId = wire.UId;
                break;
            }

            var others = wire.Endpoints
                .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == currentInPort.UId && e.PortName == currentInPort.Port))
                .ToList();

            if (others.Count != 1)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} has fan-out ({wire.Endpoints.Count} endpoints) — not a pure series chain.");
            }

            var other = others[0];
            if (other.Kind != EndpointKind.NameCon)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} feeds ({currentInPort.UId}, {currentInPort.Port}) from an unsupported endpoint ({other.Kind}).");
            }

            var upstreamPart = network.Parts.FirstOrDefault(p => p.UId == other.UId)
                ?? throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} references unknown part UId={other.UId}.");

            // Each upstream part kind has its own "out"-equivalent port name: Contact/O/Eq/Ge use
            // "out"; a TON's only confirmed real upstream leaf is "Q" ("ET" has no live example
            // as a consumed leaf, refused like any other unrecognized shape).
            var expectedPort = upstreamPart.Name switch
            {
                "Contact" or "O" or "Eq" or "Ge" => "out",
                "TON" => "Q",
                _ => null,
            };

            if (expectedPort is null || other.PortName != expectedPort)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} feeds ({currentInPort.UId}, {currentInPort.Port}) from " +
                    $"'{upstreamPart.Name}' via port '{other.PortName}' — outside this slice.");
            }

            visitedWireUIds.Add(wire.UId);
            var outgoingWireUId = wire.UId;

            if (upstreamPart.Name == "TON")
            {
                var instancePath = string.Join('.', upstreamPart.Instance!.ComponentPath);
                steps.Insert(0, new ChainStepSidecar.TimerOutputStep(upstreamPart.UId, "Q", outgoingWireUId));
                stepExprs.Insert(0, new Expr.TagRef($"{instancePath}.Q"));
                break;
            }

            if (upstreamPart.Name == "Contact")
            {
                var (tag, operandWireUId) = ResolveOperand(wiresByPort, accessByUId, upstreamPart.UId, networkNumber);
                visitedWireUIds.Add(operandWireUId);
                AddAccessEntry(accessEntries, tag);

                Expr operandExpr = upstreamPart.Negated ? new Expr.Not(new Expr.TagRef(tag.TagPath)) : new Expr.TagRef(tag.TagPath);
                steps.Insert(0, new ChainStepSidecar.ContactStep(upstreamPart.UId, tag.UId, operandWireUId, upstreamPart.Negated, outgoingWireUId));
                stepExprs.Insert(0, operandExpr);
                currentInPort = (upstreamPart.UId, "in");
                continue;
            }

            if (upstreamPart.Name == "Eq" || upstreamPart.Name == "Ge")
            {
                var (leftExpr, leftOperand) = ResolveTagOrLiteralOperand(
                    wiresByPort, accessByUId, constantsByUId, upstreamPart.UId, "in1", networkNumber, visitedWireUIds, accessEntries, constantEntries);
                var (rightExpr, rightOperand) = ResolveTagOrLiteralOperand(
                    wiresByPort, accessByUId, constantsByUId, upstreamPart.UId, "in2", networkNumber, visitedWireUIds, accessEntries, constantEntries);

                var compareStep = new ChainStepSidecar.CompareStep(
                    upstreamPart.UId,
                    upstreamPart.Name,
                    upstreamPart.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: comparison UId={upstreamPart.UId} has no SrcType."),
                    leftOperand,
                    rightOperand,
                    outgoingWireUId);
                steps.Insert(0, compareStep);
                stepExprs.Insert(0, new Expr.Compare(ComparisonOperator(upstreamPart.Name), leftExpr, rightExpr));
                // Eq/Ge's own rail-facing/continuation port is "pre" — genuinely different from
                // a Contact's "in", not a typo (confirmed real, 2026-07-11, FC ControlDelays).
                currentInPort = (upstreamPart.UId, "pre");
                continue;
            }

            if (upstreamPart.Name == "O")
            {
                var (orStep, orExpr, orRailWireUId) = ResolveOrMerge(
                    network, wiresByPort, accessByUId, upstreamPart, outgoingWireUId, networkNumber, visitedWireUIds, accessEntries);
                steps.Insert(0, orStep);
                stepExprs.Insert(0, orExpr);
                railWireUId = orRailWireUId;
                break;
            }

            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: upstream of ({currentInPort.UId}, {currentInPort.Port}) is a '{upstreamPart.Name}', not a Contact — outside this slice.");
        }

        Expr condition = stepExprs.Count switch
        {
            0 => new Expr.And(Array.Empty<Expr>()), // wired directly to the rail — always on
            1 => stepExprs[0],
            _ => new Expr.And(stepExprs),
        };

        return (condition, steps, railWireUId);
    }

    // An OR-merge is always rail-facing (confirmed real, 2026-07-10): every branch resolves to
    // exactly one Contact fed directly by Powerrail. A branch that is itself a multi-element
    // chain, or shares its OR-merge with a differently-wired rail, is real-but-unconfirmed —
    // refused rather than guessed at.
    private static (ChainStepSidecar.OrStep OrStep, Expr Expr, int RailWireUId) ResolveOrMerge(
        FlgNetwork network,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        PartNode orPart,
        int outgoingWireUId,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries)
    {
        if (orPart.Cardinality is not int cardinality || cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: OR-merge UId={orPart.UId} has no usable cardinality.");
        }

        var branches = new List<ChainStepSidecar.ContactStep>();
        var branchExprs = new List<Expr>();
        var railWireUId = -1;

        for (var k = 1; k <= cardinality; k++)
        {
            var inPort = (orPart.UId, $"in{k}");
            var branchWire = RequireWireAt(wiresByPort, inPort, networkNumber);
            var others = branchWire.Endpoints
                .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == orPart.UId && e.PortName == $"in{k}"))
                .ToList();

            if (others.Count != 1)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} wire {branchWire.UId} has fan-out — not a single-contact branch.");
            }

            var other = others[0];
            if (other.Kind != EndpointKind.NameCon || other.PortName != "out")
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} is fed from an unsupported endpoint ({other.Kind}).");
            }

            var branchPart = network.Parts.FirstOrDefault(p => p.UId == other.UId)
                ?? throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} references unknown part UId={other.UId}.");

            if (branchPart.Name != "Contact")
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} is a '{branchPart.Name}', not a Contact — " +
                    "multi-element/non-contact OR branches are outside this slice.");
            }

            visitedWireUIds.Add(branchWire.UId);

            var branchInWire = RequireWireAt(wiresByPort, (branchPart.UId, "in"), networkNumber);
            if (!branchInWire.Endpoints.Any(e => e.Kind == EndpointKind.Powerrail))
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} contact UId={branchPart.UId} is not fed " +
                    "directly from Powerrail — multi-contact OR branches are outside this slice.");
            }

            if (railWireUId == -1)
            {
                railWireUId = branchInWire.UId;
            }
            else if (railWireUId != branchInWire.UId)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branches are fed from different rail wires " +
                    $"({railWireUId} vs {branchInWire.UId}) — only a single shared rail wire has been observed.");
            }

            visitedWireUIds.Add(branchInWire.UId);

            var (tag, operandWireUId) = ResolveOperand(wiresByPort, accessByUId, branchPart.UId, networkNumber);
            visitedWireUIds.Add(operandWireUId);
            if (!accessEntries.Any(e => e.TagPath == tag.TagPath && e.UId == tag.UId))
            {
                accessEntries.Add(tag);
            }

            Expr branchExpr = branchPart.Negated ? new Expr.Not(new Expr.TagRef(tag.TagPath)) : new Expr.TagRef(tag.TagPath);
            branches.Add(new ChainStepSidecar.ContactStep(branchPart.UId, tag.UId, operandWireUId, branchPart.Negated, branchWire.UId));
            branchExprs.Add(branchExpr);
        }

        return (new ChainStepSidecar.OrStep(orPart.UId, branches, outgoingWireUId), new Expr.Or(branchExprs), railWireUId);
    }

    private static (SidecarAccessEntry Tag, int WireUId) ResolveOperand(
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        int partUId,
        int networkNumber)
    {
        var wire = RequireWireAt(wiresByPort, (partUId, "operand"), networkNumber);
        var identCon = wire.Endpoints.FirstOrDefault(e => e.Kind == EndpointKind.IdentCon)
            ?? throw new NonReducibleNetworkException(
                $"Network {networkNumber}: operand wire {wire.UId} for UId={partUId} has no IdentCon source.");

        var access = accessByUId.TryGetValue(identCon.UId!.Value, out var found)
            ? found
            : throw new NonReducibleNetworkException(
                $"Network {networkNumber}: operand wire {wire.UId} references unknown Access UId={identCon.UId}.");

        return (new SidecarAccessEntry(access.DottedPath, access.UId, access.Scope), wire.UId);
    }

    private static Dictionary<(int, string), WireNode> BuildPortIndex(IReadOnlyList<WireNode> wires)
    {
        var index = new Dictionary<(int, string), WireNode>();
        foreach (var wire in wires)
        {
            foreach (var endpoint in wire.Endpoints)
            {
                if (endpoint.Kind == EndpointKind.NameCon)
                {
                    index[(endpoint.UId!.Value, endpoint.PortName!)] = wire;
                }
            }
        }

        return index;
    }

    private static WireNode RequireWireAt(Dictionary<(int, string), WireNode> index, (int, string) port, int networkNumber) =>
        index.TryGetValue(port, out var wire)
            ? wire
            : throw new NonReducibleNetworkException($"Network {networkNumber}: no wire found feeding port {port}.");
}

public sealed class NonReducibleNetworkException : Exception
{
    public NonReducibleNetworkException(string message)
        : base(message)
    {
    }
}
