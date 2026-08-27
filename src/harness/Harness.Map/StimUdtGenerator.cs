using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>Naming and prose the generator will not originate.</summary>
/// <param name="TypeName">The generated UDT's name.</param>
/// <param name="Comment">
/// The type's own comment. <b>Required, and never defaulted</b> — the rule
/// <see cref="InstanceDbNaming"/> holds, for the same reason: this type is the vocabulary a model can be
/// commanded in, and a comment describing that is a description of the plant.
/// </param>
public sealed record StimUdtNaming(string TypeName, string Comment);

/// <summary>
/// 🔴 <b>ONE MEMBER, DECLARED — the escape for everything the rungs do not prove, and the ONLY way a type
/// or a start value gets into this UDT other than by derivation.</b>
///
/// <para>Three quite different jobs, deliberately in one record because they are the same act: a person
/// saying something about the plant that no rung says.</para>
/// <list type="number">
/// <item>Supplying the <see cref="Datatype"/> the rungs prove only partly — <c>Stim.Phase</c> and
/// <c>Stim.ResetMode</c> are compared against integer literals, which fixes "an integer" and not its
/// width.</item>
/// <item>Declaring a member the shell references NOWHERE — the drives, the plant model, the scenario
/// timeline. <see cref="StimShellGenerator"/>'s own header draws that line and this record is where the
/// far side of it is written down.</item>
/// <item>Attaching a <see cref="StartValue"/> or a <see cref="Comment"/>, neither of which is ever
/// originated here.</item>
/// </list>
/// </summary>
/// <param name="Name">The member's name, without the <c>Stim.</c> root.</param>
/// <param name="Datatype">
/// The IR datatype. <b>Required for a member the shell does not reference</b>; optional for one it does,
/// where it is CHECKED against the derived type rather than trusted — a declaration that contradicts the
/// rungs is a refusal, not an override.
/// </param>
/// <param name="StartValue">
/// The member's start value, verbatim as IR writes it. <b>A start value is a preset and a preset is a
/// claim about the plant</b> — the rule <see cref="InstanceDbPreset"/> states in full. Nothing here
/// originates one, so absent simply means the member starts at its type default.
/// </param>
/// <param name="Comment">
/// The member's own comment. Optional, and its absence is REPORTED rather than filled:
/// <see cref="StimShellGenerator"/> emits no comments for a stated reason — <i>"a copied comment that is
/// subtly wrong for your head is worse than none"</i> — and that reason does not weaken for a member line.
/// </param>
public sealed record StimUdtMember(
    string Name,
    string? Datatype = null,
    string? StartValue = null,
    string? Comment = null);

/// <summary>What was declared for one stimulus UDT.</summary>
public sealed record StimUdtDeclaration(
    StimUdtNaming Naming,
    IReadOnlyList<StimUdtMember>? Members = null);

/// <summary>One emitted member and where its type came from.</summary>
/// <param name="Name">The member.</param>
/// <param name="Datatype">The type emitted.</param>
/// <param name="Derived">
/// True when the emitted rungs fixed the type; false when a person declared it. <b>Reported per member
/// rather than summarised</b>, so "the generator worked this out" and "somebody asserted this" never look
/// the same in a report.
/// </param>
/// <param name="Evidence">The rung that decided it, or the note that it was declared.</param>
public sealed record StimUdtMemberResult(string Name, string Datatype, bool Derived, string Evidence);

/// <summary>The generated UDT, and what the caller must be told about it.</summary>
public sealed record StimUdtResult(
    string Ir,
    string TypeName,
    IReadOnlyList<StimUdtMemberResult> Members,
    IReadOnlyList<string> Obligations)
{
    /// <summary>How many members the shell's own rungs typed without anybody saying so.</summary>
    public int DerivedCount => Members.Count(m => m.Derived);

    /// <summary>How many carry a type a person asserted.</summary>
    public int DeclaredCount => Members.Count(m => !m.Derived);
}

/// <summary>
/// 🔴 <b>THE STIMULUS UDT — GENERATED WHERE THE RUNGS PROVE IT AND REFUSED WHERE THEY DO NOT, WHICH IS
/// THE WHOLE OF THE JUDGEMENT IN THIS FILE.</b>
///
/// <para><b>Where the line falls, stated once so it is not re-argued per member.</b> CLAUDE.md's rule is
/// MECHANISM vs A CLAIM ABOUT THE PLANT, and this artifact straddles it — which
/// <see cref="StimShellGenerator"/>'s own header already says in the other direction: the shell is
/// <i>"the clock, the phases, the cleardowns, the arming window"</i>, while <i>"the disarm lever, the
/// drives, the scenario timeline, the plant model"</i> stay authored.</para>
///
/// <list type="bullet">
/// <item><b>MECHANISM, and therefore derived and emitted:</b> a member's EXISTENCE, because the shell
/// references it in a rung the shell wrote itself; and its TYPE where a rung position fixes one — driven
/// as a coil or read as a boolean atom is <c>Bool</c>, unified with one of the shell's own Time-valued
/// quantities is <c>Time</c>. Also the member ORDER, which is first-reference order in the emitted
/// networks.</item>
/// <item><b>A CLAIM ABOUT THE PLANT, and therefore refused:</b> a WIDTH. <c>Stim.Phase</c> is written by
/// <c>MOVE(EN := …, IN := 3) => Stim.Phase</c> and <c>Stim.ResetMode</c> is read by
/// <c>Stim.ResetMode = 2</c>. Every one of <c>SInt</c>, <c>USInt</c>, <c>Byte</c>, <c>Int</c>,
/// <c>UInt</c>, <c>DInt</c> and <c>Word</c> satisfies those rungs. <b>Choosing among them is choosing the
/// range the model can be commanded over</b>, and <c>Int</c> is not a safer guess than the others — it is
/// the same guess with a familiar name. Refused, named, and handed back.</item>
/// <item><b>Also refused:</b> every member the shell references NOWHERE. That set is not small and is not
/// meant to be — the drives, the timeline, the model's own settings. The shell cannot know they exist,
/// and a UDT that invented a vocabulary for a plant model nobody wrote would be exactly the failure this
/// component is built to avoid.</item>
/// <item><b>Also refused:</b> every start value and every comment. Both are presets or prose, and both
/// follow rules this component already holds elsewhere (<see cref="InstanceDbPreset"/>, and
/// <see cref="StimShellGenerator"/>'s comment obligation).</item>
/// </list>
///
/// <para>🔴 <b>WHAT THIS IS WORTH, MEASURED RATHER THAN CLAIMED — AND IT IS NOT THE LINE COUNT.</b> The
/// declaration surface is barely smaller than the file, because a stimulus UDT is mostly comments and
/// comments are plant claims. What it buys is that the member SET AND THE TYPES CANNOT FALL BEHIND THE
/// RUNGS. A shell that grows a reference grows the UDT; a type that is a table entry today is an
/// inference over the emitted text here; and the one thing a hand-typed projection of a moving interface
/// reliably does — go stale, silently, as
/// <c>ir/test-project001/iDB_HopperBlockageStim.ir</c> already has by nine members — cannot happen.</para>
///
/// <para><b>Contract copied from <see cref="CopyLayerGenerator"/> unchanged:</b> pure text-out, no
/// filesystem, no Portal, and every uncertainty a refusal rather than a guess.</para>
/// </summary>
public static class StimUdtGenerator
{
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <param name="declaration">Naming, and the declared half. Required.</param>
    /// <param name="shell">
    /// 🔴 <b>THE SHELL, NOT THE SPEC — because it is the EMITTED RUNGS that prove a member is needed.</b>
    /// A spec states an intention; the rungs are what will resolve against this type at compile. Taking
    /// the spec would let the two disagree, which is the single-source-of-truth defect
    /// <c>converter served-area</c> exists to close one artifact over.
    /// </param>
    /// <exception cref="ArgumentException">Every refusal, naming what is missing and who resolves it.</exception>
    public static StimUdtResult Generate(StimUdtDeclaration declaration, StimShellResult shell)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(shell);

        var naming = declaration.Naming ?? throw new ArgumentException(
            "no naming was declared. A type needs a name and a comment, and this generator originates neither.",
            nameof(declaration));

        if (!Identifier.IsMatch(naming.TypeName ?? string.Empty))
            throw new ArgumentException($"'{naming.TypeName}' is not a usable type name.", nameof(declaration));

        if (string.IsNullOrWhiteSpace(naming.Comment))
        {
            throw new ArgumentException(
                $"'{naming.TypeName}' declares no comment. This type IS the vocabulary a model can be commanded in, so a "
                + "description of it is a description of the plant — which is the one thing this generator will not write.",
                nameof(declaration));
        }

        var declared = Index(declaration.Members, naming.TypeName!);
        var inferred = StimRungTypes.Infer(shell.Networks);

        var members = new List<StimUdtMemberResult>();
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var undeclaredWidths = new List<string>();

        // ---- the derived half, in the order the shell first references it ----------------------------
        foreach (var operand in inferred)
        {
            var name = StimRungTypes.StimMemberName(operand.Name);
            if (name is null)
            {
                RefuseNestedMember(operand.Name, naming.TypeName!);
                continue;
            }

            referenced.Add(name);
            declared.TryGetValue(name, out var supplied);

            if (operand.Datatype is { } derivedType)
            {
                // 🔴 A DECLARATION THAT DISAGREES WITH THE RUNGS IS A REFUSAL, NOT AN OVERRIDE. The rungs
                // are what resolves against this type at compile; a declaration that wins would produce a
                // UDT the block cannot use, and the report would say a person chose it.
                if (supplied?.Datatype is { Length: > 0 } asserted
                    && !string.Equals(asserted, derivedType, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"'{naming.TypeName}' declares '{name} : {asserted}', and the shell's own rungs make it "
                        + $"{derivedType} — {operand.Evidence}. The rungs are what resolves against this type at compile, "
                        + "so a declaration that won here would emit a UDT the generated block cannot use. Drop the "
                        + "declared type (it is derived) or change the head so the rungs say what you mean.",
                        nameof(declaration));
                }

                members.Add(new StimUdtMemberResult(name, derivedType, Derived: true, operand.Evidence));
                continue;
            }

            if (supplied?.Datatype is { Length: > 0 } chosen)
            {
                members.Add(new StimUdtMemberResult(
                    name, chosen, Derived: false, $"DECLARED. The rungs prove {Describe(operand.Class)} — {operand.Evidence}"));
                continue;
            }

            undeclaredWidths.Add(
                $"'{name}' — the rungs prove {Describe(operand.Class)}, at {operand.Evidence}");
        }

        // 🔴 EVERY UNDERIVABLE TYPE IN ONE REFUSAL, NOT THE FIRST ONE. A caller who fixes one and is then
        // told about the next has been made to run the generator once per missing type.
        if (undeclaredWidths.Count > 0)
        {
            throw new ArgumentException(
                $"'{naming.TypeName}' cannot be generated: {undeclaredWidths.Count} member(s) the shell references have a "
                + $"type the rungs DO NOT FIX. {string.Join("; ", undeclaredWidths)}. *** AN INTEGER LITERAL IN A RUNG "
                + "PROVES AN INTEGER AND NOT ITS WIDTH: *** SInt, USInt, Byte, Int, UInt, DInt and Word all satisfy every "
                + "rung the shell emits, and choosing among them chooses the range the model can be commanded over. `Int` "
                + "is not a safer guess than the others — it is the same guess with a familiar name. Declare a datatype "
                + "for each, as the person who knows what the model must be able to say.", nameof(declaration));
        }

        // ---- the declared-only half, in declaration order ---------------------------------------------
        foreach (var member in declaration.Members ?? Array.Empty<StimUdtMember>())
        {
            if (referenced.Contains(member.Name))
                continue;

            if (string.IsNullOrWhiteSpace(member.Datatype))
            {
                throw new ArgumentException(
                    $"'{naming.TypeName}' declares member '{member.Name}' with no datatype, and no rung the shell emits "
                    + "references it — so there is nothing to derive one from. A member the shell never touches is part of "
                    + "the head's own vocabulary (a drive, a model setting, a timeline value), which is authored, and "
                    + "authored members carry authored types.", nameof(declaration));
            }

            members.Add(new StimUdtMemberResult(
                member.Name, member.Datatype!, Derived: false,
                "DECLARED. No emitted rung references this member — it is part of the head's authored vocabulary."));
        }

        if (members.Count == 0)
        {
            throw new ArgumentException(
                $"'{naming.TypeName}' would be emitted with no members at all. An empty stimulus UDT is a model that can be "
                + "commanded nothing and publishes nothing, and every vector against it would read the same thing whatever "
                + "the block did — the refusal `outcomeBits is empty` already makes for the same reason.", nameof(declaration));
        }

        return new StimUdtResult(
            Render(naming, members, declared),
            naming.TypeName!,
            members,
            Obligations(naming.TypeName!, members, declared, shell, referenced));
    }

    private static string Describe(StimTypeClass type) => type switch
    {
        StimTypeClass.Integer => "an INTEGER of unstated width",
        StimTypeClass.Unknown => "NOTHING AT ALL about its type",
        _ => type.ToString(),
    };

    private static void RefuseNestedMember(string operand, string typeName)
    {
        if (!operand.StartsWith(StimRungTypes.StimRoot + ".", StringComparison.Ordinal))
            return;

        throw new ArgumentException(
            $"the shell references '{operand}', which is a member INSIDE a member of '{typeName}'. This generator emits a "
            + "flat type: how the nesting is shaped — one struct or three, which members sit under which — is a design "
            + "decision about the model's vocabulary, and inventing a shape that happens to make the path resolve is the "
            + "invention this generator exists to refuse.", nameof(typeName));
    }

    private static Dictionary<string, StimUdtMember> Index(IReadOnlyList<StimUdtMember>? members, string typeName)
    {
        var byName = new Dictionary<string, StimUdtMember>(StringComparer.Ordinal);

        foreach (var member in members ?? Array.Empty<StimUdtMember>())
        {
            if (member is null)
                throw new ArgumentException($"'{typeName}' declares a null member.", nameof(members));

            if (!Identifier.IsMatch(member.Name ?? string.Empty))
                throw new ArgumentException($"'{member?.Name}' is not a usable member name in '{typeName}'.", nameof(members));

            if (!byName.TryAdd(member.Name!, member))
            {
                throw new ArgumentException(
                    $"'{typeName}' declares member '{member.Name}' twice. One member has one type and one start value; two "
                    + "declarations of it is two answers to a question with one, and nothing here picks between them.",
                    nameof(members));
            }
        }

        return byName;
    }

    /// <summary>
    /// The type's IR. <b>Same grammar the converter's own <c>TypeIrSerializer</c> writes</b> — a `TYPE`
    /// header at column 0, `ROOTID`/`COMMENT`/`MEMBERS` at two, members at four, and the fixed trailing
    /// token order in which `COMMENT` is always last so it can be peeled before the ` = ` start-value
    /// search. See <see cref="IrMemberLine"/> for why this component reads and writes that grammar
    /// directly rather than referencing the converter.
    /// </summary>
    private static string Render(
        StimUdtNaming naming, IReadOnlyList<StimUdtMemberResult> members, IReadOnlyDictionary<string, StimUdtMember> declared)
    {
        var ir = new StringBuilder();
        ir.Append("TYPE ").Append(naming.TypeName).Append('\n');
        ir.Append("  ROOTID 0\n");
        ir.Append("  COMMENT \"").Append(Escape(naming.Comment)).Append("\"\n");
        ir.Append("  MEMBERS\n");

        foreach (var member in members)
        {
            ir.Append("    ").Append(member.Name).Append(" : ").Append(member.Datatype);

            declared.TryGetValue(member.Name, out var supplied);

            if (supplied?.StartValue is { Length: > 0 } start)
                ir.Append(" = ").Append(start);

            if (supplied?.Comment is { Length: > 0 } comment)
                ir.Append(" COMMENT \"").Append(Escape(comment)).Append('"');

            ir.Append('\n');
        }

        return ir.ToString();
    }

    private static IReadOnlyList<string> Obligations(
        string typeName,
        IReadOnlyList<StimUdtMemberResult> members,
        IReadOnlyDictionary<string, StimUdtMember> declared,
        StimShellResult shell,
        IReadOnlySet<string> referenced)
    {
        var owed = new List<string>();

        var uncommented = members
            .Where(m => !declared.TryGetValue(m.Name, out var d) || string.IsNullOrWhiteSpace(d.Comment))
            .Select(m => m.Name)
            .ToArray();

        if (uncommented.Length > 0)
        {
            owed.Add(
                $"WRITE {uncommented.Length} MEMBER COMMENT(S): {string.Join(", ", uncommented)}. This generator writes "
                + "none, for the reason the shell generator states: a copied comment that is subtly wrong for your head is "
                + "worse than none. Every member of the committed stimulus UDT carries one, and generated code is held to a "
                + "stricter bar than site code — so this type is NOT equivalent to a hand-authored one until they exist.");
        }

        var asserted = members.Where(m => !m.Derived).Select(m => m.Name).ToArray();
        if (asserted.Length > 0)
        {
            owed.Add(
                $"{asserted.Length} MEMBER TYPE(S) WERE DECLARED, NOT DERIVED: {string.Join(", ", asserted)}. Nothing "
                + "checked them against anything — they are your claim about what the model must be able to say, and a "
                + "range too narrow for a scenario you have not written yet fails as a value that silently saturates.");
        }

        var presets = members.Where(m => declared.TryGetValue(m.Name, out var d) && d.StartValue is { Length: > 0 })
            .Select(m => m.Name).ToArray();

        owed.Add(presets.Length == 0
            ? $"NO START VALUE WAS DECLARED on any member of '{typeName}', so every one starts at its type default. That is "
              + "a legitimate type and it is also indistinguishable from one whose presets nobody got to — only you can say "
              + "which this is."
            : $"{presets.Length} MEMBER(S) CARRY A DECLARED START VALUE: {string.Join(", ", presets)}. A start value is a "
              + "preset and a preset is a claim about the plant; nothing here originated one.");

        // 🔴 THE SHELL'S OWN HAND-WRITTEN REQUIREMENT LIST, COMPARED AGAINST WHAT ITS RUNGS ACTUALLY
        // REFERENCE. The two are maintained by different mechanisms — one is a literal array beside the
        // networks, the other is read out of the emitted text — so they can disagree, and until this line
        // existed nothing said so.
        var listedNotReferenced = shell.RequiredUdtMembers.Where(m => !referenced.Contains(m)).ToArray();
        if (listedNotReferenced.Length > 0)
        {
            owed.Add(
                $"THE SHELL'S OWN `RequiredUdtMembers` LIST NAMES {listedNotReferenced.Length} MEMBER(S) THAT NO EMITTED "
                + $"RUNG REFERENCES: {string.Join(", ", listedNotReferenced)}. They are therefore NOT in this type. That "
                + "list is a hand-written array beside the networks and this derivation is read out of the emitted text, so "
                + "the disagreement is real and one of the two is wrong. If your authored networks need them, declare them "
                + "here with a datatype; if nothing needs them, the list is stale.");
        }

        var referencedNotListed = referenced.Where(m => !shell.RequiredUdtMembers.Contains(m)).ToArray();
        if (referencedNotListed.Length > 0)
        {
            owed.Add(
                $"AND {referencedNotListed.Length} MEMBER(S) THE RUNGS REFERENCE ARE ABSENT FROM THAT LIST: "
                + $"{string.Join(", ", referencedNotListed)}. They ARE in this type, because the rungs reference them — "
                + "which is what the list was meant to enumerate. A head built from the list alone would be missing them.");
        }

        return owed;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
