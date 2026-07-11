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
            partsElement.Add(new XElement(
                ns + "Access",
                new XAttribute("Scope", "TypedConstant"),
                new XAttribute("UId", constant.UId),
                new XElement(ns + "Constant", new XElement(ns + "ConstantValue", constant.Value))));
        }

        foreach (var part in network.Parts)
        {
            var partElement = new XElement(ns + "Part", new XAttribute("Name", part.Name));
            if (part.TonVersion is not null)
            {
                partElement.Add(new XAttribute("Version", part.TonVersion));
            }

            partElement.Add(new XAttribute("UId", part.UId));

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

            if (part.Cardinality is not null)
            {
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", "Card"),
                    new XAttribute("Type", "Cardinality"),
                    part.Cardinality.Value));
            }

            if (part.TimeType is not null)
            {
                partElement.Add(new XElement(
                    ns + "TemplateValue",
                    new XAttribute("Name", "time_type"),
                    new XAttribute("Type", "Type"),
                    part.TimeType));
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

    private static XElement WriteEndpoint(XNamespace ns, WireEndpoint endpoint) => endpoint.Kind switch
    {
        EndpointKind.Powerrail => new XElement(ns + "Powerrail"),
        EndpointKind.IdentCon => new XElement(ns + "IdentCon", new XAttribute("UId", endpoint.UId!.Value)),
        EndpointKind.NameCon => new XElement(ns + "NameCon", new XAttribute("UId", endpoint.UId!.Value), new XAttribute("Name", endpoint.PortName!)),
        EndpointKind.OpenCon => new XElement(ns + "OpenCon", new XAttribute("UId", endpoint.UId!.Value)),
        _ => throw new SimaticMlFormatException($"Unknown wire endpoint kind: {endpoint.Kind}"),
    };
}
