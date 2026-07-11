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

    private static readonly HashSet<string> SupportedPartNames = new(StringComparer.Ordinal) { "Contact", "Coil", "O", "TON" };
    private static readonly HashSet<string> SupportedAccessScopes = new(StringComparer.Ordinal) { "GlobalVariable", "LocalVariable" };

    // TON's own <Instance> reference uses the same two scopes as an ordinary tag Access —
    // confirmed real, 2026-07-11: LocalVariable (multi-instance, FB MotorDOL) and GlobalVariable
    // (standalone instance DB, FC ControlDelays).
    private static readonly HashSet<string> SupportedInstanceScopes = new(StringComparer.Ordinal) { "GlobalVariable", "LocalVariable" };

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
        var constants = new List<ConstantAccessNode>();
        var parts = new List<PartNode>();

        foreach (var child in partsElement.Elements())
        {
            if (child.Name == Ns + "Access")
            {
                var scope = RequireAttribute(child, "Scope");
                if (scope == "TypedConstant")
                {
                    constants.Add(ParseTypedConstant(child));
                }
                else
                {
                    accessNodes.Add(ParseAccess(child));
                }
            }
            else if (child.Name == Ns + "Part")
            {
                var name = RequireAttribute(child, "Name");
                if (!SupportedPartNames.Contains(name))
                {
                    throw new UnsupportedConstructException(
                        $"Unsupported instruction '{name}' (UId={RequireAttribute(child, "UId")}). " +
                        "This converter slice supports Contact/Coil/O/TON only.");
                }

                var uid = RequireIntAttribute(child, "UId");
                var negated = name == "Contact" && ParseNegated(child, uid);
                var cardinality = name == "O" ? ParseOrCardinality(child, uid) : (int?)null;
                if (name == "TON")
                {
                    var (version, timeType, instance) = ParseTon(child, uid);
                    parts.Add(new PartNode(uid, name, TonVersion: version, TimeType: timeType, Instance: instance));
                }
                else
                {
                    parts.Add(new PartNode(uid, name, negated, cardinality));
                }
            }
            else
            {
                throw new SimaticMlFormatException($"Unrecognized element <{child.Name.LocalName}> inside <Parts>.");
            }
        }

        var wires = wiresElement.Elements(Ns + "Wire").Select(ParseWire).ToList();

        return new FlgNetwork(accessNodes, parts, wires, constants);
    }

    // TypedConstant Access — only a top-level PT source, never a Contact/Coil operand (those
    // still go through ParseAccess/SupportedAccessScopes unchanged). Only a bare <ConstantValue>
    // has been observed (no <ConstantType>, unlike the nested LiteralConstant array-index shape)
    // — confirmed real, 2026-07-11, FC ControlDelays: `T#100MS`.
    private static ConstantAccessNode ParseTypedConstant(XElement access)
    {
        var uid = RequireIntAttribute(access, "UId");
        var constant = access.Element(Ns + "Constant")
            ?? throw new SimaticMlFormatException($"<Access Scope=\"TypedConstant\" UId=\"{uid}\"> is missing its <Constant> element.");

        if (constant.Element(Ns + "ConstantType") is not null)
        {
            throw new UnsupportedConstructException(
                $"<Access Scope=\"TypedConstant\" UId=\"{uid}\">'s <Constant> has a <ConstantType> child — only a bare <ConstantValue> has been observed.");
        }

        var value = constant.Element(Ns + "ConstantValue")?.Value
            ?? throw new SimaticMlFormatException($"<Access Scope=\"TypedConstant\" UId=\"{uid}\">'s <Constant> is missing <ConstantValue>.");

        return new ConstantAccessNode(uid, value);
    }

    // A TON's own Instance reference — same Scope values as an ordinary Access, but the
    // Component path is a direct child (no <Symbol> wrapper) — confirmed real, 2026-07-11.
    private static (string Version, string TimeType, AccessNode Instance) ParseTon(XElement tonPart, int uid)
    {
        var version = RequireAttribute(tonPart, "Version");

        var instanceElement = tonPart.Element(Ns + "Instance")
            ?? throw new SimaticMlFormatException($"<Part Name=\"TON\" UId=\"{uid}\"> is missing its <Instance> element.");
        var instanceScope = RequireAttribute(instanceElement, "Scope");
        if (!SupportedInstanceScopes.Contains(instanceScope))
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"TON\" UId=\"{uid}\">'s <Instance Scope=\"{instanceScope}\"> — only GlobalVariable/LocalVariable have been observed.");
        }

        var instanceUId = RequireIntAttribute(instanceElement, "UId");
        var instanceComponents = instanceElement.Elements(Ns + "Component").ToList();
        if (instanceComponents.Count == 0)
        {
            throw new SimaticMlFormatException($"<Part Name=\"TON\" UId=\"{uid}\">'s <Instance> has no <Component> path elements.");
        }

        if (instanceComponents.Any(c => c.Attribute("SliceAccessModifier") is not null || c.Attribute("AccessModifier") is not null))
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"TON\" UId=\"{uid}\">'s <Instance> has a slice/array-indexed Component — not observed on an Instance reference.");
        }

        var instancePath = instanceComponents.Select(c => RequireAttribute(c, "Name")).ToList();
        var instance = new AccessNode(instanceUId, instanceScope, instancePath);

        var templateValue = tonPart.Element(Ns + "TemplateValue")
            ?? throw new SimaticMlFormatException($"<Part Name=\"TON\" UId=\"{uid}\"> is missing its <TemplateValue> time-type element.");
        var templateName = RequireAttribute(templateValue, "Name");
        var templateType = RequireAttribute(templateValue, "Type");
        if (templateName != "time_type" || templateType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"TON\" UId=\"{uid}\">'s <TemplateValue Name=\"{templateName}\" Type=\"{templateType}\"> — only " +
                "Name=\"time_type\" Type=\"Type\" has been observed.");
        }

        return (version, templateValue.Value, instance);
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
        // last Component, confirmed real 2026-07-10. Any other Component carrying one is outside
        // what's been observed so far; refuse rather than silently take the wrong one.
        var sliceModifier = components[^1].Attribute("SliceAccessModifier")?.Value;
        if (components.Take(components.Count - 1).Any(c => c.Attribute("SliceAccessModifier") is not null))
        {
            throw new UnsupportedConstructException(
                "SliceAccessModifier found on a non-final <Component> — only the last component was observed to carry one.");
        }

        var arrayIndex = ParseArrayIndex(components[^1]);
        if (components.Take(components.Count - 1).Any(c => c.Attribute("AccessModifier")?.Value == "Array"))
        {
            throw new UnsupportedConstructException(
                "Array-indexed Component found on a non-final <Component> — only the last component was observed to carry one.");
        }

        return new AccessNode(RequireIntAttribute(access, "UId"), scope, path, sliceModifier, arrayIndex);
    }

    // Array subscript access (e.g. `CommsProcessData.Node_Error[1]`) — confirmed real 2026-07-10:
    // `<Component Name="Node_Error" AccessModifier="Array"><Access Scope="LiteralConstant">
    // <Constant><ConstantType>DInt</ConstantType><ConstantValue>1</ConstantValue></Constant>
    // </Access></Component>`. Only this exact shape (literal constant, DInt) has been observed —
    // a computed/variable index would need a different nested Access scope, refused rather than
    // guessed at.
    private static int? ParseArrayIndex(XElement component)
    {
        var accessModifier = component.Attribute("AccessModifier")?.Value;
        var nestedAccess = component.Element(Ns + "Access");

        if (accessModifier is null && nestedAccess is null)
        {
            return null;
        }

        if (accessModifier != "Array" || nestedAccess is null)
        {
            throw new UnsupportedConstructException(
                $"<Component Name=\"{component.Attribute("Name")?.Value}\"> has an unrecognized array-index shape " +
                "(expected AccessModifier=\"Array\" together with a nested <Access>).");
        }

        var indexScope = RequireAttribute(nestedAccess, "Scope");
        if (indexScope != "LiteralConstant")
        {
            throw new UnsupportedConstructException(
                $"Array index Access scope '{indexScope}' is not supported — only a literal constant index has been observed.");
        }

        var constant = nestedAccess.Element(Ns + "Constant")
            ?? throw new SimaticMlFormatException("Array index <Access> is missing its <Constant> element.");
        var constantType = constant.Element(Ns + "ConstantType")?.Value
            ?? throw new SimaticMlFormatException("Array index <Constant> is missing <ConstantType>.");
        if (constantType != "DInt")
        {
            throw new UnsupportedConstructException(
                $"Array index constant type '{constantType}' is not supported — only DInt has been observed.");
        }

        var constantValue = constant.Element(Ns + "ConstantValue")?.Value
            ?? throw new SimaticMlFormatException("Array index <Constant> is missing <ConstantValue>.");
        if (!int.TryParse(constantValue, out var index))
        {
            throw new SimaticMlFormatException($"Array index constant value is not an integer: '{constantValue}'.");
        }

        return index;
    }

    // Only `<Negated Name="operand" />` has been observed on a Contact — anything else (a
    // second Negated child, or a different Name) is a real but unconfirmed shape, refused
    // rather than guessed at (design philosophy #10).
    private static bool ParseNegated(XElement contactPart, int uid)
    {
        var negatedElements = contactPart.Elements(Ns + "Negated").ToList();
        if (negatedElements.Count == 0)
        {
            return false;
        }

        if (negatedElements.Count > 1)
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Contact\" UId=\"{uid}\"> has {negatedElements.Count} <Negated> children — only one has been observed.");
        }

        var name = RequireAttribute(negatedElements[0], "Name");
        if (name != "operand")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Contact\" UId=\"{uid}\"><Negated Name=\"{name}\"> — only Name=\"operand\" has been observed.");
        }

        return true;
    }

    // Only `<TemplateValue Name="Card" Type="Cardinality">N</TemplateValue>` has been observed
    // on an OR-merge (`Part Name="O"`) — confirmed against two real exports, 2026-07-10.
    private static int ParseOrCardinality(XElement orPart, int uid)
    {
        var templateValue = orPart.Element(Ns + "TemplateValue")
            ?? throw new SimaticMlFormatException($"<Part Name=\"O\" UId=\"{uid}\"> is missing its <TemplateValue> cardinality element.");

        var name = RequireAttribute(templateValue, "Name");
        var type = RequireAttribute(templateValue, "Type");
        if (name != "Card" || type != "Cardinality")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"O\" UId=\"{uid}\">'s <TemplateValue Name=\"{name}\" Type=\"{type}\"> — only " +
                "Name=\"Card\" Type=\"Cardinality\" has been observed.");
        }

        if (!int.TryParse(templateValue.Value, out var cardinality))
        {
            throw new SimaticMlFormatException($"<Part Name=\"O\" UId=\"{uid}\">'s cardinality value is not an integer: '{templateValue.Value}'.");
        }

        return cardinality;
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
