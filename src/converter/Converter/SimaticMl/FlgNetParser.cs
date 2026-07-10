using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Parses a single &lt;FlgNet&gt; network (one CompileUnit's NetworkSource content).
/// Scoped to this converter slice: Contact/Coil parts, GlobalVariable/LocalVariable access,
/// Powerrail/IdentCon/NameCon/OpenCon wire endpoints. Anything else is a hard error —
/// <see cref="UnsupportedConstructException"/> for a recognized-but-out-of-scope construct,
/// <see cref="SimaticMlFormatException"/> for XML that doesn't match the expected shape at all.
/// </summary>
public static class FlgNetParser
{
    public static readonly XNamespace Ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";

    private static readonly HashSet<string> SupportedPartNames = new(StringComparer.Ordinal) { "Contact", "Coil" };
    private static readonly HashSet<string> SupportedAccessScopes = new(StringComparer.Ordinal) { "GlobalVariable", "LocalVariable" };

    public static FlgNetwork Parse(XElement flgNet)
    {
        if (flgNet.Name != Ns + "FlgNet")
        {
            throw new SimaticMlFormatException($"Expected root element <FlgNet>, got <{flgNet.Name.LocalName}>.");
        }

        var partsElement = flgNet.Element(Ns + "Parts")
            ?? throw new SimaticMlFormatException("<FlgNet> is missing its <Parts> element.");
        var wiresElement = flgNet.Element(Ns + "Wires")
            ?? throw new SimaticMlFormatException("<FlgNet> is missing its <Wires> element.");

        var accessNodes = new List<AccessNode>();
        var parts = new List<PartNode>();

        foreach (var child in partsElement.Elements())
        {
            if (child.Name == Ns + "Access")
            {
                accessNodes.Add(ParseAccess(child));
            }
            else if (child.Name == Ns + "Part")
            {
                var name = RequireAttribute(child, "Name");
                if (!SupportedPartNames.Contains(name))
                {
                    throw new UnsupportedConstructException(
                        $"Unsupported instruction '{name}' (UId={RequireAttribute(child, "UId")}). " +
                        "This converter slice supports Contact/Coil only.");
                }

                parts.Add(new PartNode(RequireIntAttribute(child, "UId"), name));
            }
            else
            {
                throw new SimaticMlFormatException($"Unrecognized element <{child.Name.LocalName}> inside <Parts>.");
            }
        }

        var wires = wiresElement.Elements(Ns + "Wire").Select(ParseWire).ToList();

        return new FlgNetwork(accessNodes, parts, wires);
    }

    private static AccessNode ParseAccess(XElement access)
    {
        var scope = RequireAttribute(access, "Scope");
        if (!SupportedAccessScopes.Contains(scope))
        {
            throw new UnsupportedConstructException(
                $"Unsupported Access scope '{scope}'. This converter slice supports GlobalVariable/LocalVariable only.");
        }

        var symbol = access.Element(Ns + "Symbol")
            ?? throw new SimaticMlFormatException("<Access> is missing its <Symbol> element.");
        var components = symbol.Elements(Ns + "Component").ToList();
        var path = components.Select(c => RequireAttribute(c, "Name")).ToList();

        if (path.Count == 0)
        {
            throw new SimaticMlFormatException("<Access><Symbol> has no <Component> path elements.");
        }

        // Bit-within-word slice access (site convention C-501) — assumed to appear only on the
        // last Component, confirmed real 2026-07-11. Any other Component carrying one is outside
        // what's been observed so far; refuse rather than silently take the wrong one.
        var sliceModifier = components[^1].Attribute("SliceAccessModifier")?.Value;
        if (components.Take(components.Count - 1).Any(c => c.Attribute("SliceAccessModifier") is not null))
        {
            throw new UnsupportedConstructException(
                "SliceAccessModifier found on a non-final <Component> — only the last component was observed to carry one.");
        }

        return new AccessNode(RequireIntAttribute(access, "UId"), scope, path, sliceModifier);
    }

    private static WireNode ParseWire(XElement wire)
    {
        var endpoints = new List<WireEndpoint>();
        foreach (var child in wire.Elements())
        {
            var endpoint = child.Name.LocalName switch
            {
                "Powerrail" => new WireEndpoint(EndpointKind.Powerrail, null, null),
                "IdentCon" => new WireEndpoint(EndpointKind.IdentCon, RequireIntAttribute(child, "UId"), null),
                "NameCon" => new WireEndpoint(EndpointKind.NameCon, RequireIntAttribute(child, "UId"), RequireAttribute(child, "Name")),
                "OpenCon" => new WireEndpoint(EndpointKind.OpenCon, RequireIntAttribute(child, "UId"), null),
                _ => throw new SimaticMlFormatException($"Unrecognized wire endpoint <{child.Name.LocalName}>."),
            };
            endpoints.Add(endpoint);
        }

        return new WireNode(RequireIntAttribute(wire, "UId"), endpoints);
    }

    internal static string RequireAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value
            ?? throw new SimaticMlFormatException($"<{element.Name.LocalName}> is missing required attribute '{name}'.");

    internal static int RequireIntAttribute(XElement element, string name)
    {
        var raw = RequireAttribute(element, name);
        if (!int.TryParse(raw, out var value))
        {
            throw new SimaticMlFormatException($"<{element.Name.LocalName}>'s '{name}' attribute is not an integer: '{raw}'.");
        }

        return value;
    }
}
