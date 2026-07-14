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
        { "Contact", "Coil", "O", "TON", "TONR", "TOF", "Eq", "Ge", "Lt", "Ne", "Gt", "Le", "Move", "And", "Not", "SCoil", "RCoil", "Mul", "Add", "Sub", "Div", "Convert", "Swap", "Abs", "LIMIT", "T_SUB", "T_CONV", "Calc", "MOVE_BLK_VARIANT" };

    // Eq/Ge confirmed 2026-07-11 (FC ControlDelays); Lt confirmed 2026-07-12 (S1 item 19,
    // FB MotorDOL/FilterUnitSystem); Ne confirmed 2026-07-12 (S1 item 22, FB AirStar — identical shape
    // to Eq/Ge/Lt, same SrcType TemplateValue, same pre/in1/in2/out ports); Gt/Le both confirmed
    // 2026-07-14 (`FC Scale`, grounding `FB MotorVSDSystem`'s own dependency closure — identical shape
    // again, completing the full IEC comparison family).
    private static readonly HashSet<string> SupportedComparisonPartNames = new(StringComparer.Ordinal) { "Eq", "Ge", "Lt", "Ne", "Gt", "Le" };

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
                        "This converter slice supports Contact/Coil/O/TON/TONR/TOF/Eq/Ge/Lt/Ne/Gt/Le/Move/And/Not/SCoil/RCoil/Mul/Add/Sub/Div/Convert/Swap/Abs/LIMIT/T_SUB/T_CONV/Calc/MOVE_BLK_VARIANT only.");
                }

                var uid = RequireIntAttribute(child, "UId");
                var negated = name == "Contact" && ParseNegated(child, uid);
                var cardinality = name == "O" ? ParseCardinality(child, "O", uid) : (int?)null;
                if (name is "TON" or "TONR" or "TOF")
                {
                    var (version, timeType, instance) = ParseTon(child, name, uid);
                    parts.Add(new PartNode(uid, name, Version: version, TimeType: timeType, Instance: instance));
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
                    var (mulCardinality, mulAutomaticSrcType, mulSrcType) = ParseMulFixedShape(child, name, uid, requireCardinality: true);
                    parts.Add(new PartNode(uid, name, Cardinality: mulCardinality, AutomaticSrcType: mulAutomaticSrcType, SrcType: mulSrcType));
                }
                else if (name is "Sub" or "Div")
                {
                    var (_, subAutomaticSrcType, subSrcType) = ParseMulFixedShape(child, name, uid, requireCardinality: false);
                    parts.Add(new PartNode(uid, name, AutomaticSrcType: subAutomaticSrcType, SrcType: subSrcType));
                }
                else if (name == "Convert")
                {
                    var (convertSrcType, convertDestType) = ParseConvertFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, SrcType: convertSrcType, DestType: convertDestType));
                }
                else if (name == "Swap")
                {
                    var swapSrcType = ParseDisabledEnoSingleSrcTypeShape(child, "Swap", uid);
                    parts.Add(new PartNode(uid, name, SrcType: swapSrcType));
                }
                else if (name == "Abs")
                {
                    var absSrcType = ParseDisabledEnoSingleSrcTypeShape(child, "Abs", uid);
                    parts.Add(new PartNode(uid, name, SrcType: absSrcType));
                }
                else if (name == "LIMIT")
                {
                    var (limitVersion, limitValueType) = ParseLimitFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, Version: limitVersion, SrcType: limitValueType));
                }
                else if (name == "T_SUB")
                {
                    var (tSubVersion, dateType, timeType) = ParseTSubFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, Version: tSubVersion, SrcType: dateType, TimeType: timeType));
                }
                else if (name == "T_CONV")
                {
                    var (tConvVersion, tConvSrcType, tConvDestType) = ParseTConvFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, Version: tConvVersion, SrcType: tConvSrcType, DestType: tConvDestType));
                }
                else if (name == "Calc")
                {
                    var (calcCardinality, calcSrcType, calcEquation) = ParseCalcFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, Cardinality: calcCardinality, SrcType: calcSrcType, Equation: calcEquation));
                }
                else if (name == "MOVE_BLK_VARIANT")
                {
                    var moveBlkVariantVersion = ParseMoveBlkVariantFixedShape(child, uid);
                    parts.Add(new PartNode(uid, name, Version: moveBlkVariantVersion));
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
    //
    // Reused for Sub/Div too (confirmed real 2026-07-14, `FC Scale`) — identical DisabledENO/
    // AutomaticTyped-or-TemplateValue shape, but genuinely different on `Card`: Sub/Div carry
    // **no `Card` element at all** (always binary, `in1`/`in2` wire ports, never a chain),
    // whereas Mul/Add always do. `requireCardinality` selects which: `true` requires a `Card`
    // element (Mul/Add, unchanged); `false` requires its *absence* (hard error if present — an
    // unconfirmed shape, refused rather than silently accepted) and returns a `null` Cardinality.
    private static (int? Cardinality, bool AutomaticSrcType, string? SrcType) ParseMulFixedShape(XElement mulPart, string partName, int uid, bool requireCardinality)
    {
        var disabledEno = mulPart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        int? cardinality;
        if (requireCardinality)
        {
            cardinality = ParseCardinality(mulPart, partName, uid);
        }
        else
        {
            var cardTemplateValue = mulPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "Card");
            if (cardTemplateValue is not null)
            {
                throw new UnsupportedConstructException(
                    $"<Part Name=\"{partName}\" UId=\"{uid}\"> has a <TemplateValue Name=\"Card\"> element — not confirmed " +
                    "real for Sub/Div (always binary, no real example has shown one), refused rather than guessed at.");
            }

            cardinality = null;
        }

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

    // A DisabledENO="true" + single SrcType TemplateValue shape — confirmed real for Swap
    // (2026-07-12, S1 item 25, `FB TomraControlSystem`, 2 instances, `Type="Type">Word</TemplateValue>`)
    // and Abs (2026-07-14, `FB VSDSim`'s own numeric-simulation ladder, `SrcType="Real"`) —
    // identical shape, generalized once a second real instruction confirmed it isn't Swap-
    // specific (same "don't stack special cases" reasoning as the Version field rename).
    private static string ParseDisabledEnoSingleSrcTypeShape(XElement part, string partName, int uid)
    {
        var disabledEno = part.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"{partName}\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        return ParseSrcType(part, partName, uid);
    }

    // A clamp/limiter box instruction (`Part Name="LIMIT"`) — confirmed real, 2026-07-14, `FB
    // VSDSim` (2 instances): `Version="1.0"` (same attribute TON carries, stored on the same
    // PartNode.Version field — see its own doc comment), `DisabledENO="true"` (same reasoning as
    // Convert/Swap/Abs's own — confirmed live, 2026-07-14: TIA's own Import() validator rejects a
    // regenerated LIMIT that omits it, "ENO cannot be deactivated for the 'LIMIT' instruction" —
    // an earlier reading of the real export had missed this attribute entirely), plus a single
    // `value_type` `TemplateValue` (`Type="Type">Real</TemplateValue>` in both instances) —
    // genuinely a different TemplateValue Name than every other typed instruction's own
    // "SrcType", but the same semantic role (the type LIMIT operates on), so stored in the same
    // PartNode.SrcType field rather than a new one.
    private static (string Version, string ValueType) ParseLimitFixedShape(XElement limitPart, int uid)
    {
        var disabledEno = limitPart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"LIMIT\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        var version = RequireAttribute(limitPart, "Version");

        var templateValue = limitPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "value_type")
            ?? throw new SimaticMlFormatException($"<Part Name=\"LIMIT\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"value_type\"> element.");
        var type = RequireAttribute(templateValue, "Type");
        if (type != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"LIMIT\" UId=\"{uid}\">'s <TemplateValue Name=\"value_type\" Type=\"{type}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        return (version, templateValue.Value);
    }

    // A time-arithmetic subtraction box instruction (`Part Name="T_SUB"`) — confirmed real,
    // 2026-07-14, `FB VibratorCycle` (Phase 2 Tier 2): `Version="1.2"`, two TemplateValues
    // (`date_type` then `time_type`, in that order — matching the wire order, IN1 then IN2 — both
    // `"Time"` in the one real instance, stored verbatim rather than assumed always Time-typed).
    // No DisabledENO at all (same "checked for absence" discipline as LIMIT). `date_type`/
    // `time_type` are stored on the existing PartNode.SrcType/TimeType fields respectively (same
    // semantic role — the type of each operand — reused rather than adding parallel fields; a
    // coincidental name collision with TON's own `time_type` TemplateValue, genuinely unrelated
    // instructions sharing an attribute name).
    private static (string Version, string DateType, string TimeType) ParseTSubFixedShape(XElement tSubPart, int uid)
    {
        var disabledEno = tSubPart.Attribute("DisabledENO")?.Value;
        if (disabledEno is not null)
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"T_SUB\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno}\" — no real instance has carried this attribute.");
        }

        var version = RequireAttribute(tSubPart, "Version");

        var dateTypeValue = tSubPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "date_type")
            ?? throw new SimaticMlFormatException($"<Part Name=\"T_SUB\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"date_type\"> element.");
        var dateType = RequireAttribute(dateTypeValue, "Type");
        if (dateType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"T_SUB\" UId=\"{uid}\">'s <TemplateValue Name=\"date_type\" Type=\"{dateType}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        var timeTypeValue = tSubPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "time_type")
            ?? throw new SimaticMlFormatException($"<Part Name=\"T_SUB\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"time_type\"> element.");
        var timeType = RequireAttribute(timeTypeValue, "Type");
        if (timeType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"T_SUB\" UId=\"{uid}\">'s <TemplateValue Name=\"time_type\" Type=\"{timeType}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        return (version, dateTypeValue.Value, timeTypeValue.Value);
    }

    // A time-arithmetic type-conversion box instruction (`Part Name="T_CONV"`) — confirmed real,
    // 2026-07-14, `FB VibratorCycle` (Phase 2 Tier 2), immediately following T_SUB in an
    // ENO-chain: `Version="1.2"`, `src_type`/`dest_type` TemplateValues (lowercase, unlike
    // ordinary Convert's `SrcType`/`DestType`) — same semantic role as Convert's own pair, stored
    // on the existing PartNode.SrcType/DestType fields. No DisabledENO (same as T_SUB).
    private static (string Version, string SrcType, string DestType) ParseTConvFixedShape(XElement tConvPart, int uid)
    {
        var disabledEno = tConvPart.Attribute("DisabledENO")?.Value;
        if (disabledEno is not null)
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"T_CONV\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno}\" — no real instance has carried this attribute.");
        }

        var version = RequireAttribute(tConvPart, "Version");

        var srcTypeValue = tConvPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "src_type")
            ?? throw new SimaticMlFormatException($"<Part Name=\"T_CONV\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"src_type\"> element.");
        var srcTypeType = RequireAttribute(srcTypeValue, "Type");
        if (srcTypeType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"T_CONV\" UId=\"{uid}\">'s <TemplateValue Name=\"src_type\" Type=\"{srcTypeType}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        var destTypeValue = tConvPart.Elements(Ns + "TemplateValue").FirstOrDefault(t => t.Attribute("Name")?.Value == "dest_type")
            ?? throw new SimaticMlFormatException($"<Part Name=\"T_CONV\" UId=\"{uid}\"> is missing its <TemplateValue Name=\"dest_type\"> element.");
        var destTypeType = RequireAttribute(destTypeValue, "Type");
        if (destTypeType != "Type")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"T_CONV\" UId=\"{uid}\">'s <TemplateValue Name=\"dest_type\" Type=\"{destTypeType}\"> — only " +
                "Type=\"Type\" has been observed.");
        }

        return (version, srcTypeValue.Value, destTypeValue.Value);
    }

    // A free-expression box instruction (`Part Name="Calc"`) — confirmed real, 2026-07-14, `FB
    // VSDSim` (Phase 2 Tier 3, 2 instances, identical shape): `DisabledENO="true"` (same
    // reasoning as Mul/Add's own), a free-text `<Equation>` element (e.g. `"IN1*(IN2/IN3)"` —
    // carried verbatim, see PartNode.Equation's own doc comment), a `Card` TemplateValue
    // (Cardinality-driven inputs, same as Mul/Add/WAND), and an explicit `SrcType` TemplateValue
    // (both real instances `"Real"` — no `AutomaticTyped` alternative has been observed for Calc,
    // unlike Mul/Add, so only the explicit-TemplateValue shape is accepted here).
    private static (int Cardinality, string SrcType, string Equation) ParseCalcFixedShape(XElement calcPart, int uid)
    {
        var disabledEno = calcPart.Attribute("DisabledENO")?.Value;
        if (disabledEno != "true")
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"Calc\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno ?? "(absent)"}\" — only \"true\" has been observed.");
        }

        var equationElement = calcPart.Element(Ns + "Equation")
            ?? throw new SimaticMlFormatException($"<Part Name=\"Calc\" UId=\"{uid}\"> is missing its <Equation> element.");

        var cardinality = ParseCardinality(calcPart, "Calc", uid);
        var srcType = ParseSrcType(calcPart, "Calc", uid);

        return (cardinality, srcType, equationElement.Value);
    }

    // A block-move-with-array-indexing instruction (`Part Name="MOVE_BLK_VARIANT"`) — confirmed
    // real, 2026-07-14 (Phase 2 Tier 5, `FC MoveData`/`FC VSDDataSequence`, 4 identical
    // instances): `Version="1.2"`, no other attributes or children at all — genuinely the
    // simplest possible Part-level shape (all its complexity lives in the wiring: `en`/`SRC`/
    // `COUNT`/`SRC_INDEX`/`DEST_INDEX` inputs, `Ret_Val`/`DEST` outputs — see
    // MoveBlkVariantStatement's own doc comment). No `DisabledENO` in any real instance —
    // checked for absence, not assumed.
    private static string ParseMoveBlkVariantFixedShape(XElement part, int uid)
    {
        var disabledEno = part.Attribute("DisabledENO")?.Value;
        if (disabledEno is not null)
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"MOVE_BLK_VARIANT\" UId=\"{uid}\"> has DisabledENO=\"{disabledEno}\" — no real instance has carried this attribute.");
        }

        if (part.HasElements)
        {
            throw new UnsupportedConstructException(
                $"<Part Name=\"MOVE_BLK_VARIANT\" UId=\"{uid}\"> has child elements — no real instance has carried any.");
        }

        return RequireAttribute(part, "Version");
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

    // An FB/FC call — confirmed real, 2026-07-12, FC PlantAutoControl (20 real FB-call instances) and
    // FB MotorVSDSystem (1 real FC-call instance, S1 item 24). Genuinely not a `<Part Name="Call">`:
    // `<Call>` is its own sibling element under <Parts>, wrapping `<CallInfo Name="<callee>"
    // BlockType="FB"/"FC">[<Instance .../>]<Parameter .../>...</CallInfo>` — adapted into an
    // ordinary PartNode(Name="Call") here so the rest of the pipeline never needs a parallel type.
    // Both BlockType="FB" and "FC" now confirmed real; stored verbatim (not hard-validated to a
    // constant). <Parameter> children are sparse — confirmed real: only wired parameters appear
    // at all (19 of 20 real FB-call instances have none; the one real FC-call instance has 6:
    // 5 Input + 1 Output, same Section shape as every FB-call parameter seen), in source
    // declaration order.
    private static PartNode ParseCall(XElement callElement)
    {
        var uid = RequireIntAttribute(callElement, "UId");
        var callInfo = callElement.Element(Ns + "CallInfo")
            ?? throw new SimaticMlFormatException($"<Call UId=\"{uid}\"> is missing its <CallInfo> element.");

        var blockName = RequireAttribute(callInfo, "Name");
        var blockType = RequireAttribute(callInfo, "BlockType");

        // An FC call has no <Instance> element at all — confirmed real, 2026-07-12 (S1 item 24,
        // FB MotorVSDSystem's own "Scale" call): FCs are stateless, no instance DB needed, unlike every
        // FB call grounded so far (S1 item 14). Checked by direct presence rather than gated on
        // BlockType — the two are expected to correlate, but nothing rules out a real
        // counter-example either way, so this doesn't assume one implies the other.
        var instance = callInfo.Element(Ns + "Instance") is not null
            ? ParseInstanceReference(callInfo, "Call", uid)
            : null;

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
