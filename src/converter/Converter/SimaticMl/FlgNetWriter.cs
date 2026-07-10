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
                    if (index == access.ComponentPath.Count - 1 && access.SliceAccessModifier is not null)
                    {
                        element.Add(new XAttribute("SliceAccessModifier", access.SliceAccessModifier));
                    }

                    return element;
                });

            partsElement.Add(new XElement(
                ns + "Access",
                new XAttribute("Scope", access.Scope),
                new XAttribute("UId", access.UId),
                new XElement(ns + "Symbol", componentElements)));
        }

        foreach (var part in network.Parts)
        {
            partsElement.Add(new XElement(ns + "Part", new XAttribute("Name", part.Name), new XAttribute("UId", part.UId)));
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
