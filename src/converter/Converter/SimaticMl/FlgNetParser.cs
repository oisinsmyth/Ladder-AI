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

    private static readonly HashSet<string> SupportedPartNames = new(StringComparer.Ordinal)
        { "Contact", "Coil", "O", "TON", "TONR", "TOF", "Eq", "Ge", "Lt", "Ne", "Move", "And", "Not", "SCoil", "RCoil", "Mul", "Add", "Convert" };

    // Eq/Ge confirmed 2026-07-11 (FC ControlDelays); Lt confirmed 2026-07-12 (S1 item 19,
    // FB MotorDOL/FilterUnitSystem); Ne confirmed 2026-07-12 (S1 item 22, FB AirStar — identical shape
    // to Eq/Ge/Lt, same SrcType TemplateValue, same pre/in1/in2/out ports). Le/Gt's real Part
    // Names remain unconfirmed, refused rather than guessed at even though the IEC family
    // strongly suggests what they'd be named.
    private static readonly HashSet<string> SupportedComparisonPartNames = new(StringComparer.Ordinal) { "Eq", "Ge", "Lt", "Ne" };

    // LocalConstant confirmed real 2026-07-12 (S1 item 21, FB MotorVSDSystem/AirStar — 4 independent
    // instances) — a genuinely different shape from GlobalVariable/LocalVariable (see ParseAccess's
    // own LocalConstant branch), but the same allowlist gates all three.
    private static readonly HashSet<string> SupportedAccessScopes = new(StringComparer.Ordinal) { "GlobalVariable", "LocalVariable", "LocalConstant" };

    // TON's own <Instance> reference uses the same two scopes as an ordinary tag Access —
    // confirmed real, 2026-07-11: LocalVariable (multi-instance, FB MotorDOL) and GlobalVariable
    // (standalone instance DB, FC ControlDelays). Reused as-is by a Call's own <Instance>
    // (confirmed identical shape, 2026-07-12, FC PlantAutoControl — all 20 real instances
    // GlobalVariable; LocalVariable unconfirmed for a Call specifically, low-risk by analogy).
    private static readonly HashSet<string> SupportedInstanceScopes = new(StringComparer.Ordinal) { "GlobalVariable", "LocalVariable" };

    // A Call's own <Parameter Section="..."> — only Input/Output confirmed real, 2026-07-12,
    // FC PlantAutoControl (TomraControlSystem: 8 Input, 2 Output). InOut is real in principle (an FB's own
    // interface can declare InOut parameters) but unconfirmed on a Call specifically, refused
    // rather than guessed at.
    private static readonly HashSet<string> SupportedCallParameterSections = new(StringComparer.Ordinal) { "Input", "Output" };

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
                else if (scope == "LiteralConstant")
                {
                    constants.Add(ParseTopLevelLiteralConstant(child));
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
                        "This converter slice supports Contact/Coil/O/TON/TONR/TOF/Eq/Ge/Lt/Ne/Move/And/Not/SCoil/RCoil/Mul/Add/Convert only.");
                }

                var uid = RequireIntAttribute(child, "UId");
                var negated = name == "Contact" && ParseNegated(child, uid);
                var cardinality = name == "O" ? ParseCardinality(child, "O", uid) : (int?)null;
                if (name is "TON" or "TONR" or "TOF")
                {
                    var (version, timeType, instance) = ParseTon(child, name, uid);
                    parts.Add(new PartNode(uid, name, TonVersion: version, TimeType: timeType, Instance: instance));
                }
                else if (SupportedComparisonPartNames.Contains(name))
                {
                    var srcType = ParseSrcType(child, name, uid);
                    parts.Add(new PartNode(uid, name, SrcType: srcType));
                }
                else if (name == "Move")
                {
                    ParseMoveFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name));
                }
                else if (name == "And")
                {
                    var (andCardinality, andSrcType) = ParseAndFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, Cardinality: andCardinality, SrcType: andSrcType));
                }
                else if (name is "Mul" or "Add")
                {
                    var (mulCardinality, mulAutomaticSrcType, mulSrcType) = ParseMulFixedShape(child, name, uid);
                    parts.Add(new PartNode(uid, name, Cardinality: mulCardinality, AutomaticSrcType: mulAutomaticSrcType, SrcType: mulSrcType));
                }
                else if (name == "Convert")
                {
                    var (convertSrcType, convertDestType) = ParseConvertFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, SrcType: convertSrcType, DestType: convertDestType));
                }
                else
                {
                    parts.Add(new PartNode(uid, name, negated, cardinality));
                }
            }
            else if (child.Name == Ns + "Call")
            {
                parts.Add(ParseCall(child));
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

    // LiteralConstant used as a *top-level* Access (sibling to Contact/Eq under <Parts>) — a
    // comparison's (Eq/Ge) literal operand. Confirmed real, 2026-07-11, FC ControlDelays:
    // `<Access Scope="LiteralConstant" UId="22"><Constant><ConstantType>Int</ConstantType>
    // <ConstantValue>1</ConstantValue></Constant></Access>` — always has a <ConstantType>,
    // the exact opposite of TypedConstant's shape, so the two are validated as mirror images
    // rather than guessed at. Distinct from the *nested* LiteralConstant use inside an array-index
    // Component (ParseArrayIndex) — same scope string, different position, not reused here.
    private static ConstantAccessNode ParseTopLevelLiteralConstant(XElement access)
    {
        var uid = RequireIntAttribute(access, "UId");
        var constant = access.Element(Ns + "Constant")
            ?? throw new SimaticMlFormatException($"<Access Scope=\"LiteralConstant\" UId=\"{uid}\"> is missing its <Constant> element.");

        var constantType = constant.Element(Ns + "ConstantType")?.Value
            ?? throw new SimaticMlFormatException($"<Access Scope=\"LiteralConstant\" UId=\"{uid}\">'s <Constant> is missing <ConstantType> — only a typed literal has been observed at the top level.");

        var value = constant.Element(Ns + "ConstantValue")?.Value
            ?? throw new SimaticMlFormatException($"<Access Scope=\"LiteralConstant\" UId=\"{uid}\">'s <Constant> is missing <ConstantValue>.");

        return new ConstantAccessNode(uid, value, constantType);
    }

    // A `SrcType` `<TemplateValue Name="SrcType" Type="Type">` — confirmed real, 2026-07-11, on a
    // comparison (Eq/Ge, `FC ControlDelays`, `Int` in every instance seen) and, 2026-07-12, on a
    // bitwise-And (`FB VSDUpdateComs`, `Word`) — the latter co-occurring with a Cardinality
    // TemplateValue on the same Part, so this looks the value up by its own `Name` among all of
    // the Part's `TemplateValue` children rather than assuming it's the only one present.
    private static string ParseSrcType(XElement part, string partName, int uid)
    {
        var templateValue = part.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "SrcType")
            ?? throw new SimaticMlFormatException($"<Part Name=\"{partName}\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"SrcType\"> element.");

        var type = RequireAttribute(templateValue, "Type");
        if (type != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\">'s <TemplateValue Name=\"SrcType\" Type=\"{type}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        return templateValue.Value;
    }

    // A Move's own shape is entirely fixed in every real instance seen (`FB MotorDOL`,
    // 2026-07-11) — `DisabledENO="true"` and `<TemplateValue Name="Card" Type="Cardinality">1
    // </TemplateValue>` — no variable data to carry on PartNode (unlike TON's time_type or a
    // comparison's SrcType), so this only validates the fixed shape and hard-errors on anything
    // else, rather than store a field whose value never varies (same "don't carry a confirmed
    // constant" reasoning as TON's InstanceOfType). A Cardinality other than 1 would presumably
    // be a MOVE_BLK_VARIANT-style multi-element copy — real but unconfirmed, refused.
    private static void ParseMoveFixedShape(XElement movePart, int uid)
    {
        var disabledEno = movePart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Move\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        var templateValue = movePart.Element(Ns + "TemplateValue")
            ?? throw new SimaticMlFormatException($"<Part Name=\"Move\" UId=\"{uid}\"> is missing its <TemplateValue> cardinality element.");
        var name = RequireAttribute(templateValue, "Name");
        var type = RequireAttribute(templateValue, "Type");
        if (name != "Card" || type != "Cardinality" || templateValue.Value != "1")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Move\" UId=\"{uid}\">'s <TemplateValue Name=\"{name}\" Type=\"{type}\">{templateValue.Value}</TemplateValue> " +
                "— only Name=\"Card\" Type=\"Cardinality\">1 has been observed (a different value would presumably be a " +
                "MOVE_BLK_VARIANT-style multi-element copy — real but unconfirmed).");
        }
    }

    // A bitwise/word AND box instruction (`Part Name="And"`) — confirmed real, 2026-07-12,
    // `FB VSDUpdateComs`: `DisabledENO="true"` (same "don't store a confirmed constant" reasoning
    // as Move's own DisabledENO — never seen to vary) plus a `Card` and a `SrcType`
    // `TemplateValue` together on the same Part (`Card="2"`, `SrcType="Word"` in the one real
    // example — unlike Move's `Card`, this value IS carried as data since only one cardinality
    // has been observed, not enough to treat as a universal constant the way Move's `Card="1"`
    // is, after being confirmed fixed across multiple real instances).
    private static (int Cardinality, string SrcType) ParseAndFixedShape(XElement andPart, int uid)
    {
        var disabledEno = andPart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"And\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        var cardinality = ParseCardinality(andPart, "And", uid);
        var srcType = ParseSrcType(andPart, "And", uid);
        return (cardinality, srcType);
    }

    // A multiply/add box instruction (`Part Name="Mul"`/`"Add"`) — confirmed real, 2026-07-12,
    // `FB MotorDOL`/`FB EquipmentControlSystem`/`FilterUnitSystem` (independent instances, identical shape for both
    // Part Names): `DisabledENO="true"` (same reasoning as And/Move's own), a `Card`
    // `TemplateValue` (`Card="2"` in every real instance, carried as data — same "only one value
    // observed" reasoning as And's own Cardinality). Its own type is EITHER
    // `<AutomaticTyped Name="SrcType" />` — a self-closing element with no value at all (TIA
    // infers the type from the connected operands rather than declaring it statically) — OR an
    // ordinary `<TemplateValue Name="SrcType" Type="Type">X</TemplateValue>`, the same explicit
    // shape `Convert`/comparisons already use. Both confirmed real, 2026-07-12: `AutomaticTyped`
    // from `MotorDOL`/`EquipmentControlSystem` (S1 item 18's own original grounding); the explicit
    // `TemplateValue` shape from `FB AirStar` (`Mul UId=43`, `SrcType="Real"`), found live-
    // verifying S1 item 20 — a genuine counter-example to S1 item 18's own "always
    // AutomaticTyped" assumption, not guessed at or silently unified. Exactly one of the two must
    // be present; both or neither is refused.
    private static (int Cardinality, bool AutomaticSrcType, string? SrcType) ParseMulFixedShape(XElement mulPart, string partName, int uid)
    {
        var disabledEno = mulPart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        var cardinality = ParseCardinality(mulPart, partName, uid);

        var automaticTyped = mulPart.Element(Ns + "AutomaticTyped");
        var srcTypeTemplateValue = mulPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "SrcType");

        if (automaticTyped is not null && srcTypeTemplateValue is not null)
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\"> has both an <AutomaticTyped> element and a <TemplateValue " +
                "Name=\"SrcType\"> — only one or the other has been observed, never both.");
        }

        if (automaticTyped is not null)
        {
            var automaticTypedName = RequireAttribute(automaticTyped, "Name");
            if (automaticTypedName != "SrcType" || automaticTyped.HasElements || !string.IsNullOrEmpty(automaticTyped.Value))
            {
                throw new UnsupportedConstructException(
                    $"<Part Name=\"{partName}\" UId=\"{uid}\">'s <AutomaticTyped Name=\"{automaticTypedName}\"> — only a bare, " +
                    "empty <AutomaticTyped Name=\"SrcType\" /> has been observed.");
            }

            return (cardinality, true, null);
        }

        if (srcTypeTemplateValue is not null)
        {
            var type = RequireAttribute(srcTypeTemplateValue, "Type");
            if (type != "Type")
            {
                throw new UnsupportedConstructException(
                    $"<Part Name=\"{partName}\" UId=\"{uid}\">'s <TemplateValue Name=\"SrcType\" Type=\"{type}\"> — only " +
                    "Type=\"Type\" has been observed.");
            }

            return (cardinality, false, srcTypeTemplateValue.Value);
        }

        throw new SimaticMlFormatException(
            $"<Part Name=\"{partName}\" UId=\"{uid}\"> has neither an <AutomaticTyped> element nor a <TemplateValue Name=\"SrcType\"> — one of the two is required.");
    }

    // A type-conversion box instruction (`Part Name="Convert"`) — confirmed real, 2026-07-12,
    // `FB MotorDOL`/`EquipmentControlSystem` (ENO-chained after a `Mul`) and `FB ShredderControlSystem` (standalone,
    // independently rail-fed): `DisabledENO="true"` (same reasoning as above) plus two ordinary
    // `TemplateValue`s — `SrcType`/`DestType` (e.g. `Real`->`DInt`, `Int`->`Int`) — genuinely
    // typed *between* two types, unlike every other typed instruction's single `SrcType`.
    private static (string SrcType, string DestType) ParseConvertFixedShape(XElement convertPart, int uid)
    {
        var disabledEno = convertPart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Convert\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        var srcType = ParseSrcType(convertPart, "Convert", uid);

        var destTypeValue = convertPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "DestType")
            ?? throw new SimaticMlFormatException($"<Part Name=\"Convert\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"DestType\"> element.");
        var destTypeType = RequireAttribute(destTypeValue, "Type");
        if (destTypeType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Convert\" UId=\"{uid}\">'s <TemplateValue Name=\"DestType\" Type=\"{destTypeType}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        return (srcType, destTypeValue.Value);
    }

    // A TON/TONR's own Instance reference — same Scope values as an ordinary Access, but the
    // Component path is a direct child (no <Symbol> wrapper) — confirmed real, 2026-07-11 (TON)
    // and 2026-07-12 (TONR, S1 item 19: identical Version/Instance/time_type shape to TON — the
    // only difference is the extra `R` reset port, which is wired separately like PT, not part of
    // this Part element at all).
    private static (string Version, string TimeType, AccessNode Instance) ParseTon(XElement tonPart, string partName, int uid)
    {
        var version = RequireAttribute(tonPart, "Version");
        var instance = ParseInstanceReference(tonPart, partName, uid);

        var templateValue = tonPart.Element(Ns + "TemplateValue")
            ?? throw new SimaticMlFormatException($"<Part Name=\"{partName}\" UId=\"{uid}\"> is missing its <TemplateValue> time-type element.");
        var templateName = RequireAttribute(templateValue, "Name");
        var templateType = RequireAttribute(templateValue, "Type");
        if (templateName != "time_type" || templateType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\">'s <TemplateValue Name=\"{templateName}\" Type=\"{templateType}\"> — only " +
                "Name=\"time_type\" Type=\"Type\" has been observed.");
        }

        return (version, templateValue.Value, instance);
    }

    // An <Instance Scope="..." UId="..."><Component .../></Instance> reference — same shape used
    // by a TON's own Instance and, confirmed real 2026-07-12 (FC PlantAutoControl), a Call's own
    // Instance too (this is the reuse ir/SPEC.md's TON section deliberately anticipated: "a
    // future FC/FB call's own instance argument can reuse AccessNode rather than needing a
    // redesign"). Factored out once a second real caller (Call) confirmed the shape is genuinely
    // shared, not just TON-specific. contextLabel/uid are only used to phrase error messages.
    private static AccessNode ParseInstanceReference(XElement owner, string contextLabel, int uid)
    {
        var instanceElement = owner.Element(Ns + "Instance")
            ?? throw new SimaticMlFormatException($"<{contextLabel} UId=\"{uid}\"> is missing its <Instance> element.");
        var instanceScope = RequireAttribute(instanceElement, "Scope");
        if (!SupportedInstanceScopes.Contains(instanceScope))
        {
            throw new UnsupportedConstructException(
                $"<{contextLabel} UId=\"{uid}\">'s <Instance Scope=\"{instanceScope}\"> — only GlobalVariable/LocalVariable have been observed.");
        }

        var instanceUId = RequireIntAttribute(instanceElement, "UId");
        var instanceComponents = instanceElement.Elements(Ns + "Component").ToList();
        if (instanceComponents.Count == 0)
        {
            throw new SimaticMlFormatException($"<{contextLabel} UId=\"{uid}\">'s <Instance> has no <Component> path elements.");
        }

        if (instanceComponents.Any(c => c.Attribute("SliceAccessModifier") is not null || c.Attribute("AccessModifier") is not null))
        {
            throw new UnsupportedConstructException(
                $"<{contextLabel} UId=\"{uid}\">'s <Instance> has a slice/array-indexed Component — not observed on an Instance reference.");
        }

        var instancePath = instanceComponents.Select(c => RequireAttribute(c, "Name")).ToList();
        return new AccessNode(instanceUId, instanceScope, instancePath);
    }

    // An FB/FC call — confirmed real, 2026-07-12, FC PlantAutoControl (20 real instances). Genuinely
    // not a `<Part Name="Call">`: `<Call>` is its own sibling element under <Parts>, wrapping
    // `<CallInfo Name="<callee>" BlockType="FB"><Instance .../><Parameter .../>...</CallInfo>` —
    // adapted into an ordinary PartNode(Name="Call") here so the rest of the pipeline never needs
    // a parallel type. Only BlockType="FB" observed; stored verbatim (not hard-validated to a
    // constant) since an unconfirmed "FC" shouldn't be assumed impossible. <Parameter> children
    // are sparse — confirmed real: only wired parameters appear at all (19 of 20 real instances
    // have none), in source declaration order.
    private static PartNode ParseCall(XElement callElement)
    {
        var uid = RequireIntAttribute(callElement, "UId");
        var callInfo = callElement.Element(Ns + "CallInfo")
            ?? throw new SimaticMlFormatException($"<Call UId=\"{uid}\"> is missing its <CallInfo> element.");

        var blockName = RequireAttribute(callInfo, "Name");
        var blockType = RequireAttribute(callInfo, "BlockType");
        var instance = ParseInstanceReference(callInfo, "Call", uid);

        var parameters = callInfo.Elements(Ns + "Parameter").Select(p =>
        {
            var paramName = RequireAttribute(p, "Name");
            var section = RequireAttribute(p, "Section");
            if (!SupportedCallParameterSections.Contains(section))
            {
                throw new UnsupportedConstructException(
                    $"<Call UId=\"{uid}\">'s <Parameter Name=\"{paramName}\" Section=\"{section}\"> — only Input/Output have been observed.");
            }

            var type = RequireAttribute(p, "Type");
            return new CallParameterNode(paramName, section, type);
        }).ToList();

        return new PartNode(uid, "Call", Instance: instance, BlockName: blockName, BlockType: blockType, CallParameters: parameters);
    }

    private static AccessNode ParseAccess(XElement access)
    {
        var scope = RequireAttribute(access, "Scope");
        if (!SupportedAccessScopes.Contains(scope))
        {
            throw new UnsupportedConstructException(
                $"Unsupported Access scope '{scope}'. This converter slice supports GlobalVariable/LocalVariable/LocalConstant only.");
        }

        if (scope == "LocalConstant")
        {
            return ParseLocalConstantAccess(access);
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

    // A LocalConstant Access — confirmed real, 2026-07-12 (S1 item 21), 4 independent instances
    // (`FB MotorVSDSystem`: `MinSpd` ×2; `FB AirStar`: `PulseTimerMS` ×2): `<Constant Name="X" />`, a
    // bare, self-closing reference by name — no `<Symbol>` wrapper, no `<ConstantType>`/
    // `<ConstantValue>`, no value at all present at the reference site. `MinSpd`/`PulseTimerMS`
    // are exactly the real member names S1 item 20's own grounding confirmed as populated
    // `Constant`-section members on these same two blocks — this is how a network reads back a
    // reference to the block's own declared Interface `Constant` member, genuinely different from
    // both `TypedConstant`/`LiteralConstant` (which carry a literal value inline) and an ordinary
    // `Symbol`-based Access. Always single-component in every instance seen (never nested/dotted)
    // — modeled as an `AccessNode` with a one-element `ComponentPath` so the IR's own tag-ref text
    // (e.g. `MinSpd`) reads identically to the member's own declared name in that block's own
    // `INTERFACE`/`CONSTANT` section — `DottedPath`/`FromDottedPath` need no changes for a
    // single-component path. Always seen at a `ResolveTagOrLiteralOperand`-style operand position
    // (TON `PT`, comparison `in1`/`in2`, `Move`'s own `in`) — never a plain Contact/Coil operand,
    // though nothing here depends on that.
    private static AccessNode ParseLocalConstantAccess(XElement access)
    {
        var uid = RequireIntAttribute(access, "UId");
        var constant = access.Element(Ns + "Constant")
            ?? throw new SimaticMlFormatException($"<Access Scope=\"LocalConstant\" UId=\"{uid}\"> is missing its <Constant> element.");

        var name = RequireAttribute(constant, "Name");
        if (constant.HasElements || !string.IsNullOrEmpty(constant.Value))
        {
            throw new UnsupportedConstructException(
                $"<Access Scope=\"LocalConstant\" UId=\"{uid}\">'s <Constant Name=\"{name}\"> has extra content — only a " +
                "bare, childless <Constant Name=\"...\" /> has been observed.");
        }

        return new AccessNode(uid, "LocalConstant", new[] { name });
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

    // A `Card` `<TemplateValue Name="Card" Type="Cardinality">N</TemplateValue>` — confirmed real
    // on an OR-merge (`Part Name="O"`, 2026-07-10) and, 2026-07-12, on a bitwise-And (`Part
    // Name="And"`, `FB VSDUpdateComs`, `Card="2"`) — the latter co-occurring with a SrcType
    // TemplateValue on the same Part, so this looks the value up by its own `Name` among all of
    // the Part's `TemplateValue` children rather than assuming it's the only one present.
    private static int ParseCardinality(XElement part, string partName, int uid)
    {
        var templateValue = part.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "Card")
            ?? throw new SimaticMlFormatException($"<Part Name=\"{partName}\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"Card\"> cardinality element.");

        var type = RequireAttribute(templateValue, "Type");
        if (type != "Cardinality")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\">'s <TemplateValue Name=\"Card\" Type=\"{type}\"> — only " +
                "Type=\"Cardinality\" has been observed.");
        }

        if (!int.TryParse(templateValue.Value, out var cardinality))
        {
            throw new SimaticMlFormatException($"<Part Name=\"{partName}\" UId=\"{uid}\">'s cardinality value is not an integer: '{templateValue.Value}'.");
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
