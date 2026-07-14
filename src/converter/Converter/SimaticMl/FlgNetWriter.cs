using System.Xml.Linq;

namespace Converter.SimaticMl;

public static class FlgNetWriter
{
    public static XElement Write(FlgNetwork network)
    {
        var ns = FlgNetParser.Ns;

        var partsElement = new XElement(ns + "Parts");
        foreach (var access in network.AccessNodes)
        {
            // LocalConstant — confirmed real, 2026-07-12 (S1 item 21): a bare reference by name,
            // `<Constant Name="X" />`, no `<Symbol>` wrapper at all (genuinely different shape
            // from every other Access scope this converter writes) — always single-component, so
            // `access.ComponentPath[0]` is the whole reference.
            if (access.Scope == "LocalConstant")
            {
                partsElement.Add(new XElement(
                    ns + "Access",
                    new XAttribute("Scope", "LocalConstant"),
                    new XAttribute("UId", access.UId),
                    new XElement(ns + "Constant", new XAttribute("Name", access.ComponentPath[0]))));
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

            partsElement.Add(new XElement(
                ns + "Access",
                new XAttribute("Scope", access.Scope),
                new XAttribute("UId", access.UId),
                new XElement(ns + "Symbol", componentElements)));
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

            partsElement.Add(new XElement(
                ns + "Access",
                new XAttribute("Scope", scope),
                new XAttribute("UId", constant.UId),
                new XElement(ns + "Constant", constantChildren)));
        }

        foreach (var part in network.Parts)
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
            // "ENO cannot be deactivated for the 'LIMIT' instruction"). T_SUB/T_CONV are confirmed
            // real WITHOUT DisabledENO at all (VibratorCycle grounding) — deliberately excluded.
            if (part.Name is "Move" or "And" or "Mul" or "Add" or "Sub" or "Div" or "Convert" or "Swap" or "Abs" or "Calc" or "LIMIT")
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
