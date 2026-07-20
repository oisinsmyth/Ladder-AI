using System.Linq;
using System.Xml.Linq;

namespace Converter.SimaticMl;

public static class FlgNetWriter
{
    public static XElement Write(FlgNetwork network)
    {
        var ns = FlgNetParser.Ns;

        // <Parts> children must be UId-ascending within the Access group (all <Access> elements) and
        // within the Part group — TIA rejects an out-of-order element on import ("must be sorted
        // according to the current flow"). All <Access> elements (tag/local-constant reads AND
        // literal/typed constants) form ONE group, so they are collected and merge-sorted by UId
        // together, not emitted as separate access-then-constant runs: the synthesizer can mint a
        // constant's UId interleaved among the tag-access UIds (a TON PT or comparison literal), whereas
        // real blocks number constants after their tag-accesses. So the merge is a no-op for real blocks
        // (byte-stable round-trip) and only reorders the synthesized interleaved case. The Normalizer
        // sorts parts before comparing, which masked this from the parity / own-sidecar oracles.
        // Surfaced 2026-07-20 on MotorStarter NW3: a TypedConstant UId 31 emitted after tag-accesses
        // 37..57, which TIA import rejects as out of flow order.
        var partsElement = new XElement(ns + "Parts");
        var accessElements = new List<(int UId, XElement Element)>();
        foreach (var access in network.AccessNodes)
        {
            // LocalConstant — confirmed real, 2026-07-12 (S1 item 21): a bare reference by name,
            // `<Constant Name="X" />`, no `<Symbol>` wrapper at all (genuinely different shape
            // from every other Access scope this converter writes) — always single-component, so
            // `access.ComponentPath[0]` is the whole reference.
            if (access.Scope == "LocalConstant")
            {
                accessElements.Add((access.UId, new XElement(
                    ns + "Access",
                    new XAttribute("Scope", "LocalConstant"),
                    new XAttribute("UId", access.UId),
                    new XElement(ns + "Constant", new XAttribute("Name", access.ComponentPath[0])))));
                continue;
            }

            var componentElements = access.ComponentPath
                .Select((component, index) =>
                {
                    var element = new XElement(ns + "Component", new XAttribute("Name", component));
                    if (index == access.ComponentPath.Count - 1)
                    {
                        if (access.ArrayIndex is not null)
                        {
                            element.Add(new XAttribute("AccessModifier", "Array"));
                            element.Add(new XElement(
                                ns + "Access",
                                new XAttribute("Scope", "LiteralConstant"),
                                new XElement(
                                    ns + "Constant",
                                    new XElement(ns + "ConstantType", "DInt"),
                                    new XElement(ns + "ConstantValue", access.ArrayIndex.Value))));
                        }

                        if (access.SliceAccessModifier is not null)
                        {
                            element.Add(new XAttribute("SliceAccessModifier", access.SliceAccessModifier));
                        }
                    }

                    return element;
                });

            accessElements.Add((access.UId, new XElement(
                ns + "Access",
                new XAttribute("Scope", access.Scope),
                new XAttribute("UId", access.UId),
                new XElement(ns + "Symbol", componentElements))));
        }

        foreach (var constant in network.Constants)
        {
            // ConstantType present -> LiteralConstant (a comparison operand); absent ->
            // TypedConstant (a TON PT literal) — the two scopes are mirror images, never mixed.
            var scope = constant.ConstantType is not null ? "LiteralConstant" : "TypedConstant";
            var constantChildren = new List<XElement>();
            if (constant.ConstantType is not null)
            {
                constantChildren.Add(new XElement(ns + "ConstantType", constant.ConstantType));
            }

            constantChildren.Add(new XElement(ns + "ConstantValue", constant.Value));

            accessElements.Add((constant.UId, new XElement(
                ns + "Access",
                new XAttribute("Scope", scope),
                new XAttribute("UId", constant.UId),
                new XElement(ns + "Constant", constantChildren))));
        }

        foreach (var (_, element) in accessElements.OrderBy(a => a.UId))
        {
            partsElement.Add(element);
        }

        foreach (var part in FlowOrderedParts(network))
        {
            if (part.Name == "Call")
            {
                partsElement.Add(WriteCall(ns, part));
                continue;
            }

            var partElement = new XElement(ns + "Part", new XAttribute("Name", part.Name));
            if (part.Version is not null)
            {
                partElement.Add(new XAttribute("Version", part.Version));
            }

            partElement.Add(new XAttribute("UId", part.UId));

            // Move's own shape is entirely fixed — DisabledENO="true" and Card=1 (below) — never
            // carried as PartNode fields since neither ever varies in any real instance seen
            // (same "don't store a confirmed constant" reasoning as TON's InstanceOfType).
            // Attribute order matches the real source: Name, UId, DisabledENO. And/Mul/Add/Convert
            // (S1 items 12/18/19) share the same fixed DisabledENO="true" — their own Cardinality/
            // SrcType/DestType, unlike Move's, ARE carried as PartNode fields (only one real
            // value has been observed for each, not enough to treat as a universal constant) and
            // already round-trip via the existing Cardinality/SrcType/DestType blocks below. TON/
            // TONR never carry DisabledENO at all (confirmed real, no EN/ENO on either). Sub/Div
            // (2026-07-14, FC Scale) share the same fixed DisabledENO="true" too, as does Abs and
            // LIMIT (2026-07-14, FB VSDSim — LIMIT's own DisabledENO was missed in an earlier
            // reading of the real export; TIA's own Import() validator caught the omission live,
            // "ENO cannot be deactivated for the 'LIMIT' instruction"). FillBlockI (2026-07-14, FC
            // ModbusComs) shares the same fixed DisabledENO="true" too. T_SUB/T_CONV/
            // MOVE_BLK_VARIANT/WAIT are confirmed real WITHOUT DisabledENO at all — deliberately
            // excluded from this list.
            if (part.Name is "Move" or "And" or "Mul" or "Add" or "Sub" or "Div" or "Convert" or "Swap" or "Abs" or "Calc" or "LIMIT" or "FillBlockI")
            {
                partElement.Add(new XAttribute("DisabledENO", "true"));
            }

            if (part.Negated)
            {
                partElement.Add(new XElement(ns + "Negated", new XAttribute("Name", "operand")));
            }

            if (part.Instance is not null)
            {
                var instance = part.Instance;
                var instanceComponents = instance.ComponentPath.Select(c => new XElement(ns + "Component", new XAttribute("Name", c)));
                partElement.Add(new XElement(
                    ns + "Instance",
                    new XAttribute("Scope", instance.Scope),
                    new XAttribute("UId", instance.UId),
                    instanceComponents));
            }

            // Calc's own free-text Equation — confirmed real, 2026-07-14, FB VSDSim, always
            // before Card/SrcType in the real source's own element order.
            if (part.Equation is not null)
            {
                partElement.Add(new XElement(ns + "Equation", part.Equation));
            }

            if (part.Cardinality is not null)
            {
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", "Card"),
                    new XAttribute("Type", "Cardinality"),
                    part.Cardinality.Value));
            }
            else if (part.Name == "Move")
            {
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", "Card"),
                    new XAttribute("Type", "Cardinality"),
                    1));
            }

            // Mul's own type — a self-closing <AutomaticTyped Name="SrcType" /> with no value at
            // all (confirmed real, 2026-07-12, S1 item 18), never a <TemplateValue> like every
            // other typed instruction. Emitted right after Cardinality, matching the real
            // source's own element order.
            if (part.AutomaticSrcType)
            {
                partElement.Add(new XElement(ns + "AutomaticTyped", new XAttribute("Name", "SrcType")));
            }

            if (part.SrcType is not null)
            {
                // LIMIT's own TemplateValue is named "value_type", not "SrcType" — confirmed
                // real, 2026-07-14, FB VSDSim (2 instances) — same semantic role (the type the
                // instruction operates on), reusing PartNode.SrcType rather than a parallel field,
                // but the source attribute name genuinely differs, so this is the one Part Name
                // that needs a different Name here. T_SUB's own first TemplateValue is named
                // "date_type" (Phase 2 Tier 2, FB VibratorCycle) — also reusing SrcType. T_CONV's
                // is "src_type" (lowercase, unlike ordinary Convert's "SrcType").
                var srcTypeAttributeName = part.Name switch
                {
                    "LIMIT" => "value_type",
                    "T_SUB" => "date_type",
                    "T_CONV" => "src_type",
                    _ => "SrcType",
                };
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", srcTypeAttributeName),
                    new XAttribute("Type", "Type"),
                    part.SrcType));
            }

            // TimeType comes after SrcType here — matches every real instance seen where both are
            // present (T_SUB: date_type then time_type, Phase 2 Tier 2). TON/TONR/TOF never carry
            // SrcType at all, so their own solitary TimeType is unaffected by this ordering.
            if (part.TimeType is not null)
            {
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", "time_type"),
                    new XAttribute("Type", "Type"),
                    part.TimeType));
            }

            // Convert's own DestType — confirmed real, 2026-07-12, S1 item 18, always paired
            // with SrcType above, matching the real source's own element order (SrcType then
            // DestType). T_CONV's own is "dest_type" (lowercase, Phase 2 Tier 2).
            if (part.DestType is not null)
            {
                var destTypeAttributeName = part.Name == "T_CONV" ? "dest_type" : "DestType";
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", destTypeAttributeName),
                    new XAttribute("Type", "Type"),
                    part.DestType));
            }

            partsElement.Add(partElement);
        }

        var wiresElement = new XElement(ns + "Wires");
        foreach (var wire in network.Wires)
        {
            var wireElement = new XElement(ns + "Wire", new XAttribute("UId", wire.UId));
            foreach (var endpoint in wire.Endpoints)
            {
                wireElement.Add(WriteEndpoint(ns, endpoint));
            }

            wiresElement.Add(wireElement);
        }

        return new XElement(ns + "FlgNet", partsElement, wiresElement);
    }

    // Instruction Parts must be emitted in TIA's wire-graph FLOW order, not UId order (Gap I): TIA
    // rejects import if a producer's downstream consumer is separated from it by an independent rung
    // ("must be sorted according to the current flow"). Real exports satisfy this only because TIA
    // numbers UIds along the flow, so UId order == flow order for them; the synthesizer's UId numbering
    // does not follow flow, so it must be re-ordered here. This is a DFS from the power rail following
    // producer->consumer edges: a part's consumers are emitted immediately after it, grouping each rung
    // with the downstream parts it feeds before moving to independent rungs. Byte-stable for real blocks
    // (their flow == UId order, so the DFS reproduces it); validated against TIA import for the
    // synthesized case (MotorStarter/MotorVSDSystem NW3, the same-network-timer-.Q-consumer shape).
    private static IEnumerable<PartNode> FlowOrderedParts(FlgNetwork network)
    {
        var partByUId = network.Parts.ToDictionary(p => p.UId);
        return FlowOrderedPartUIds(network).Select(uid => partByUId[uid]);
    }

    // The flow-order rule as a bare UId sequence — the single source of truth for the ordering, so
    // `preflight`'s FlowOrderCheck (FI-27) can validate a serialized block's <Part> order against
    // exactly what `Write` emits, catching offline a regression the Normalizer masks (it sorts <Parts>).
    internal static IReadOnlyList<int> FlowOrderedPartUIds(FlgNetwork network)
    {
        var partByUId = network.Parts.ToDictionary(p => p.UId);
        if (partByUId.Count <= 1)
        {
            return network.Parts.Select(p => p.UId).ToList();
        }

        // Producer -> consumer adjacency among Parts, plus the rail-connected roots. Each wire lists its
        // producer endpoint first (the power rail, or a Part's output port), then its consumer endpoints;
        // an <IdentCon> producer is an <Access> data source (a leaf, not a Part) and is skipped for the
        // instruction-Part flow. Consumer order within a wire is preserved (a producer's fan-out order).
        var adjacency = partByUId.Keys.ToDictionary(uid => uid, _ => new List<int>());
        var railRoots = new List<int>();
        foreach (var wire in network.Wires)
        {
            if (wire.Endpoints.Count == 0)
            {
                continue;
            }

            var producer = wire.Endpoints[0];
            var consumers = wire.Endpoints.Skip(1)
                .Where(e => e.Kind == EndpointKind.NameCon && e.UId is int cu && partByUId.ContainsKey(cu))
                .Select(e => e.UId!.Value);

            if (producer.Kind == EndpointKind.Powerrail)
            {
                railRoots.AddRange(consumers);
            }
            else if (producer.Kind == EndpointKind.NameCon && producer.UId is int pu && adjacency.ContainsKey(pu))
            {
                adjacency[pu].AddRange(consumers);
            }
        }

        var order = new List<int>(partByUId.Count);
        var visited = new HashSet<int>();

        void Visit(int uid)
        {
            if (!visited.Add(uid))
            {
                return;
            }

            order.Add(uid);
            foreach (var consumer in adjacency[uid])
            {
                Visit(consumer);
            }
        }

        foreach (var root in railRoots)
        {
            Visit(root);
        }

        // Any Part not reachable from the rail (defensive — every Part should be) is appended in UId order.
        foreach (var part in network.Parts.OrderBy(p => p.UId))
        {
            if (!visited.Contains(part.UId))
            {
                Visit(part.UId);
            }
        }

        return order;
    }

    // A Call is its own sibling element under <Parts>, not a <Part Name="Call"> — mirrors
    // FlgNetParser.ParseCall's own adaptation on the way in, in reverse. Attribute order matches
    // the real source: CallInfo's Name then BlockType, Instance's Scope then UId, Parameter's
    // Name then Section then Type — confirmed real, 2026-07-12, FC PlantAutoControl. Instance itself is
    // omitted entirely when absent — confirmed real, 2026-07-12 (S1 item 24): an FC call's own
    // <CallInfo> has no <Instance> at all (FB MotorVSDSystem's own "Scale" call).
    private static XElement WriteCall(XNamespace ns, PartNode part)
    {
        var blockName = part.BlockName
            ?? throw new SimaticMlFormatException($"Call UId=\"{part.UId}\" has no BlockName to write.");
        var blockType = part.BlockType
            ?? throw new SimaticMlFormatException($"Call UId=\"{part.UId}\" has no BlockType to write.");

        var callInfo = new XElement(ns + "CallInfo", new XAttribute("Name", blockName), new XAttribute("BlockType", blockType));

        if (part.Instance is { } instance)
        {
            var instanceComponents = instance.ComponentPath.Select(c => new XElement(ns + "Component", new XAttribute("Name", c)));
            callInfo.Add(new XElement(
                ns + "Instance",
                new XAttribute("Scope", instance.Scope),
                new XAttribute("UId", instance.UId),
                instanceComponents));
        }

        foreach (var parameter in part.CallParameters ?? Array.Empty<CallParameterNode>())
        {
            callInfo.Add(new XElement(
                ns + "Parameter",
                new XAttribute("Name", parameter.Name),
                new XAttribute("Section", parameter.Section),
                new XAttribute("Type", parameter.Type)));
        }

        return new XElement(ns + "Call", new XAttribute("UId", part.UId), callInfo);
    }

    private static XElement WriteEndpoint(XNamespace ns, WireEndpoint endpoint) => endpoint.Kind switch
    {
        EndpointKind.Powerrail => new XElement(ns + "Powerrail"),
        EndpointKind.IdentCon => new XElement(ns + "IdentCon", new XAttribute("UId", endpoint.UId!.Value)),
        EndpointKind.NameCon => new XElement(ns + "NameCon", new XAttribute("UId", endpoint.UId!.Value), new XAttribute("Name", endpoint.PortName!)),
        EndpointKind.OpenCon => new XElement(ns + "OpenCon", new XAttribute("UId", endpoint.UId!.Value)),
        _ => throw new SimaticMlFormatException($"Unknown wire endpoint kind: {endpoint.Kind}"),
    };
}
