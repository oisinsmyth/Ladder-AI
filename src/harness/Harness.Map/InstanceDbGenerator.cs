using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>Naming and numbering the generator needs and must not invent.</summary>
/// <param name="DbName">The generated instance DB's name.</param>
/// <param name="DbNumber">
/// Required, with no default — hard rule 3 forbids inventing a DB number, and X-J reserves 9000–9999 for
/// harness objects, which the CALLER allocates from
/// (<c>converter claim --allocate --kind block-number --type DB --floor 9000</c>).
/// </param>
/// <param name="FbName">The FB this is an instance OF. Emitted as <c>INSTANCEOF</c> and checked against the supplied FB.</param>
/// <param name="Comment">
/// The DB's own comment. <b>Required, and never defaulted.</b> Every DB in the committed corpus carries
/// one, generated code is held to a stricter bar than site code, and an invented comment ("Instance of
/// FB_X") passes review while telling a reader nothing.
/// </param>
public sealed record InstanceDbNaming(string DbName, int DbNumber, string FbName, string Comment);

/// <summary>
/// 🔴 <b>WHERE THE INSTANCE'S MEMBERS COME FROM — and there is no default, because the two answers differ
/// in what happens to the FB's OWN START VALUES.</b>
/// </summary>
public enum InstanceDbMemberSource
{
    /// <summary>
    /// The members are PROJECTED from the FB's interface: structure copied, <b>every start value dropped</b>
    /// unless a preset declares it. See <see cref="InstanceDbGenerator"/>.
    /// </summary>
    ProjectedFromFb,

    /// <summary>
    /// 🔴 <b>The <c>MEMBERS</c> section is emitted EMPTY and TIA fills it from the FB at compile — which
    /// means the instance inherits the FB's own start values.</b>
    ///
    /// <para>This is a real shape, not a fallback: <c>ir/test-project001/iDB_Comms_ModbusServer.ir</c> is
    /// exactly it, and it is the shape to declare when the FB holds a member the projection refuses to
    /// reproduce (a system-type instance such as <c>MB_SERVER</c> or <c>TCON_IP_v4</c>).</para>
    ///
    /// <para><b>Its cost is stated rather than hidden</b>: what the FB declares as a start value becomes
    /// THIS instance's start value, so a SECOND instance of the same FB declared this way inherits the same
    /// one. <see cref="InstanceDbGenerator"/> reports the inherited paths, and
    /// <see cref="ProgramGenerator"/> REFUSES the second instance.</para>
    /// </summary>
    LeftToTia,
}

/// <summary>
/// 🔴 <b>ONE DECLARED START VALUE. A START VALUE IS A PRESET, AND A PRESET IS A CLAIM ABOUT THE PLANT.</b>
///
/// <para>The generator projects STRUCTURE and refuses to originate a single start value. Whatever the
/// instance is to start at — a persistence threshold, a debounce, a listening port, a connection ID — is
/// declared here by somebody who knows the plant, by member path.</para>
/// </summary>
/// <param name="Path">
/// Dotted member path from the top of the <c>MEMBERS</c> section: <c>Running</c>,
/// <c>Stim.ModelThreshold</c>, <c>Comms.RemoteAddress.ADDR[1]</c>. <b>A path that names nothing in the
/// projection is a REFUSAL</b> — a mistyped path that quietly did nothing is a preset nobody would notice
/// missing.
/// </param>
/// <param name="Value">The start value, verbatim as IR writes it (<c>T#60S</c>, <c>16#0010</c>, <c>503</c>). Null only when <paramref name="Cleared"/>.</param>
/// <param name="Cleared">
/// 🔴 <b>The explicit "this instance carries NO start value of its own" — which is a DECISION, not an
/// absence.</b> It exists so that dropping a start value the FB declares is something somebody typed. Without
/// it the only way to drop one would be silence, and silence is what this whole generator refuses to accept.
/// </param>
public sealed record InstanceDbPreset(string Path, string? Value, bool Cleared = false);

/// <summary>What was declared for one instance DB.</summary>
public sealed record InstanceDbDeclaration(
    InstanceDbNaming Naming,
    InstanceDbMemberSource Members,
    IReadOnlyList<InstanceDbPreset>? Presets = null);

/// <summary>The generated instance DB, and what the caller must be told about it.</summary>
/// <param name="Ir">The DB, as readable IR.</param>
/// <param name="DbName">Echoed so a caller building a manifest does not re-derive it.</param>
/// <param name="FbName">The FB it instantiates.</param>
/// <param name="Members">Which of the two shapes was declared. Reported, never inferred from the output's size.</param>
/// <param name="InheritedStartValues">
/// 🔴 <b>Under <see cref="InstanceDbMemberSource.LeftToTia"/>, every FB start-value path this instance will
/// inherit.</b> Empty means the FB declares none — an earned zero, computed from the FB's own text.
/// </param>
public sealed record InstanceDbResult(
    string Ir,
    string DbName,
    string FbName,
    InstanceDbMemberSource Members,
    IReadOnlyList<string> InheritedStartValues);

/// <summary>
/// 🔴 <b>AN INSTANCE DB IS A MECHANICAL PROJECTION OF ITS FB'S INTERFACE — EXCEPT FOR ITS START VALUES,
/// WHICH ARE NOT MECHANICAL AT ALL.</b>
///
/// <para>The structure is derivable and therefore should never be typed: the <c>MEMBERS</c> section is the
/// FB's <c>STATIC</c> section, and the <c>INPUT</c>/<c>OUTPUT</c>/<c>INOUT</c> sections are the FB's own,
/// line for line, at the same indentation. <c>ir/test-project001/iDB_HopperBlockageStim.ir</c> is 52 hand
/// typed lines of it — and it is ALREADY STALE against the FB it instantiates, missing nine statics the FB
/// has grown since. That staleness is the case for generating it, stated as a measurement rather than a
/// preference.</para>
///
/// <para>🔴 <b>WHAT IS NOT MECHANICAL: THE START VALUES.</b> A start value is a preset — a commissioning
/// threshold, a debounce, a listening port — and a preset says what the equipment does. CLAUDE.md's line is
/// MECHANISM vs A CLAIM ABOUT THE PLANT, and it does not stop at the file boundary of something a generator
/// emitted. So <b>every start value in the FB's interface is DROPPED, and every start value the instance is
/// to carry must be DECLARED by path.</b> An undeclared one is a refusal naming the path.</para>
///
/// <para>🔴 <b>THE MEASURED HAZARD THAT SETTLES IT.</b> <c>FB_Comms_ModbusServer</c> holds
/// <c>Comms.LocalPort = 503</c> and <c>Comms.ID = 16#0010</c> as FB start values. Both IDENTIFY the
/// instance: two servers on one CPU cannot share a listening port or a connection ID. A generator that
/// copied FB start values into every instance it emitted would hand the second instance the first one's
/// port, and the failure is not a compile error — it is a connection that never establishes, on a rig,
/// hours later. Copying is as wrong as inventing; only declaring is right.</para>
///
/// <para>Contract copied from <see cref="CopyLayerGenerator"/> and <see cref="SlotFcGenerator"/> unchanged:
/// pure text-in/text-out, no filesystem, no Portal, block number required, and every uncertainty a refusal
/// rather than a guess.</para>
/// </summary>
public static class InstanceDbGenerator
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// 🔴 <b>THE MEMBER SHAPES THIS GENERATOR WILL PROJECT, AND THE LIST IS GROUNDED IN THE COMMITTED
    /// CORPUS RATHER THAN IN A BELIEF ABOUT TIA.</b>
    ///
    /// <para>A member carrying a <c>VERSION</c> is a SYSTEM TYPE instance, and what TIA accepts for one
    /// inside an instance DB varies by type: <c>TON_TIME</c> appears fully expanded in two committed
    /// instance DBs, and <c>MB_SERVER</c>/<c>TCON_IP_v4</c> appear in NONE — the one committed instance DB
    /// of an FB holding them leaves its members to TIA entirely. This generator cannot reach Portal to find
    /// out which others work, so it projects the two it can point at and refuses the rest by name.</para>
    /// </summary>
    private static readonly HashSet<string> ProjectableVersionedTypes =
        new(StringComparer.Ordinal) { "TON_TIME", "TOF_TIME" };

    /// <param name="fbIr">
    /// 🔴 <b>THE FB'S OWN IR, REQUIRED — a name is not enough to project from.</b> The caller supplies it
    /// from the program set; <see cref="ProgramGenerator"/> refuses a declaration whose FB is not there,
    /// rather than emitting an <c>INSTANCEOF</c> pointing at a block nobody has seen.
    /// </param>
    public static InstanceDbResult Generate(InstanceDbDeclaration declaration, string fbIr)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(fbIr);

        var naming = declaration.Naming;
        Validate(naming);

        var fb = FbInterface.Read(fbIr, naming.FbName);
        var presets = declaration.Presets ?? Array.Empty<InstanceDbPreset>();
        ValidatePresets(presets, naming.DbName);

        var ir = new StringBuilder();
        ir.Append("DB ").Append(naming.DbName).Append('\n');
        ir.Append("  ROOTID 0\n");
        ir.Append("  NUMBER ").Append(naming.DbNumber).Append('\n');
        ir.Append("  INSTANCEOF ").Append(naming.FbName).Append('\n');
        ir.Append("  COMMENT \"").Append(Escape(naming.Comment)).Append("\"\n");

        // MEMORYLAYOUT is deliberately NOT projected even when the FB declares one. A harness-read block's
        // layout is asserted after every import (`openness-cli block-layout --set Standard --yes`, which an
        // import silently reverts) — declaring it here would be a second, weaker statement of the same fact,
        // free to disagree with the one that actually runs. No committed instance DB carries the line.

        // INPUT/OUTPUT/INOUT are projected in BOTH shapes: they are the FB's own formal-parameter storage,
        // they are present-but-empty in the committed corpus, and present-but-empty is a distinction the IR
        // grammar preserves on purpose.
        var projecting = declaration.Members == InstanceDbMemberSource.ProjectedFromFb;
        AppendSection(ir, "INPUT", fb.Input, naming.DbName, projecting);
        AppendSection(ir, "OUTPUT", fb.Output, naming.DbName, projecting);
        if (fb.InOut is { Count: > 0 })
            AppendSection(ir, "INOUT", fb.InOut, naming.DbName, projecting);

        var inherited = Array.Empty<string>();

        if (declaration.Members == InstanceDbMemberSource.LeftToTia)
        {
            if (presets.Count > 0)
            {
                throw new ArgumentException(
                    $"'{naming.DbName}' declares `leftToTia` members AND {presets.Count} preset(s). The two cannot both hold: "
                    + "an empty MEMBERS section carries no member for a preset to sit on, and TIA fills the instance from the "
                    + "FB's own start values. Declare `projectedFromFb` to set a start value per instance, or drop the presets.",
                    nameof(declaration));
            }

            ir.Append("  MEMBERS\n");
            inherited = fb.StartValuePaths().ToArray();
            return new InstanceDbResult(ir.ToString(), naming.DbName, naming.FbName, declaration.Members, inherited);
        }

        ir.Append("  MEMBERS\n");
        var projected = Project(fb, naming.DbName, presets);
        ir.Append(projected);

        return new InstanceDbResult(ir.ToString(), naming.DbName, naming.FbName, declaration.Members, inherited);
    }

    private static string Project(FbInterface fb, string dbName, IReadOnlyList<InstanceDbPreset> presets)
    {
        var byPath = new Dictionary<string, InstanceDbPreset>(StringComparer.Ordinal);
        foreach (var preset in presets)
        {
            if (!byPath.TryAdd(preset.Path, preset))
            {
                throw new ArgumentException(
                    $"'{dbName}' declares the preset path '{preset.Path}' twice. One member has one start value; two "
                    + "declarations of it is two answers to a question with one, and nothing here picks between them.",
                    nameof(presets));
            }
        }

        var body = new StringBuilder();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new List<string>();

        WriteMembers(body, fb.Static, string.Empty, dbName, byPath, used, dropped);

        // 🔴 A START VALUE THE FB DECLARES AND THE DECLARATION DOES NOT MENTION IS A REFUSAL, IN BOTH
        // DIRECTIONS. Copying it makes every instance share what may identify one of them; dropping it
        // silently changes what the instance starts at. Neither is the generator's to choose.
        if (dropped.Count > 0)
        {
            throw new ArgumentException(
                $"'{dbName}' would drop {dropped.Count} start value(s) the FB declares, and the declaration says nothing "
                + $"about them: {string.Join(", ", dropped)}. A start value is a PRESET and a preset is a claim about the "
                + "plant — a listening port and a connection ID are both FB start values, and both identify the instance. "
                + "Declare each path with a `value`, or with `cleared: true` to say this instance deliberately starts at the "
                + "type default. The generator will not carry one over on its own.",
                nameof(presets));
        }

        var unknown = byPath.Keys.Where(p => !used.Contains(p)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"'{dbName}' declares {unknown.Length} preset(s) whose path names nothing in the projected interface: "
                + $"{string.Join(", ", unknown)}. A preset that lands nowhere is a start value nobody would notice missing, "
                + "so it is refused rather than ignored. Check the path against the FB's STATIC section.",
                nameof(presets));
        }

        return body.ToString();
    }

    private static void WriteMembers(
        StringBuilder body,
        IReadOnlyList<string> lines,
        string pathPrefix,
        string dbName,
        Dictionary<string, InstanceDbPreset> presets,
        HashSet<string> used,
        List<string> dropped)
    {
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            var member = IrMemberLineReader.Parse(line);
            var path = pathPrefix.Length == 0 ? member.Name : pathPrefix + "." + member.Name;

            RefuseUnprojectableType(member, path, dbName);

            body.Append(member.Render(Resolve(path, member.StartValue, presets, used, dropped))).Append('\n');
            i++;

            // Children are every following line indented deeper than this one — the same indent-based
            // recursion the converter's own reader uses, and the only nesting signal the format has.
            var childIndent = member.Indent + "  ";
            var children = new List<string>();
            while (i < lines.Count && lines[i].StartsWith(childIndent, StringComparison.Ordinal))
            {
                children.Add(lines[i]);
                i++;
            }

            WriteChildren(body, children, path, childIndent, dbName, presets, used, dropped);
        }
    }

    private static void WriteChildren(
        StringBuilder body,
        IReadOnlyList<string> children,
        string path,
        string childIndent,
        string dbName,
        Dictionary<string, InstanceDbPreset> presets,
        HashSet<string> used,
        List<string> dropped)
    {
        // Subelement lines first, mirroring the serializer's own order; they are array element start
        // values, so each one is a preset in its own right and is resolved by an indexed path.
        var nested = new List<string>();
        foreach (var child in children)
        {
            if (!IrMemberLineReader.IsSubelement(child))
            {
                nested.Add(child);
                continue;
            }

            var (index, value) = IrMemberLineReader.ParseSubelement(child);
            var elementPath = path + "[" + index + "]";
            var resolved = Resolve(elementPath, value, presets, used, dropped);
            if (resolved is not null)
                body.Append(childIndent).Append('[').Append(index).Append("] = ").Append(resolved).Append('\n');
        }

        if (nested.Count > 0)
            WriteMembers(body, nested, path, dbName, presets, used, dropped);
    }

    /// <summary>
    /// The one place a start value is decided. <b>Declared wins; FB-declared-and-undeclared is recorded as a
    /// refusal; neither is inherited quietly.</b>
    /// </summary>
    private static string? Resolve(
        string path,
        string? fbStartValue,
        Dictionary<string, InstanceDbPreset> presets,
        HashSet<string> used,
        List<string> dropped)
    {
        if (presets.TryGetValue(path, out var preset))
        {
            used.Add(path);
            return preset.Cleared ? null : preset.Value;
        }

        if (fbStartValue is not null)
            dropped.Add($"{path} (= {fbStartValue})");

        return null;
    }

    private static void RefuseUnprojectableType(IrMemberLine member, string path, string dbName)
    {
        if (!member.HasVersion)
            return;

        var datatype = member.Datatype;
        if (datatype.StartsWith('"') || ProjectableVersionedTypes.Contains(datatype))
            return;

        throw new ArgumentException(
            $"'{dbName}' cannot project '{path} : {datatype}'. A member carrying a VERSION is a SYSTEM TYPE instance, and "
            + $"what an instance DB must contain for one is a TIA fact this generator cannot reach Portal to check. Only "
            + $"{string.Join(" and ", ProjectableVersionedTypes.OrderBy(t => t, StringComparer.Ordinal))} are grounded in the "
            + "committed corpus. Declare this instance DB with `leftToTia` members — TIA then fills the whole instance from "
            + "the FB, which is exactly what the one committed instance DB of an FB like this one does.",
            nameof(dbName));
    }

    private static void ValidatePresets(IReadOnlyList<InstanceDbPreset> presets, string dbName)
    {
        foreach (var preset in presets)
        {
            if (string.IsNullOrWhiteSpace(preset.Path))
                throw new ArgumentException($"'{dbName}' declares a preset with no path. There is nothing to set.", nameof(presets));

            // Value-and-cleared, or neither, is a declaration that has not decided. Refused rather than
            // resolved by precedence: a precedence rule here would make one of the two silently inert.
            if (preset.Cleared && preset.Value is not null)
            {
                throw new ArgumentException(
                    $"'{dbName}' declares preset '{preset.Path}' as BOTH `cleared` and `value` '{preset.Value}'. Those are "
                    + "opposite claims about what the instance starts at; declare one.",
                    nameof(presets));
            }

            if (!preset.Cleared && preset.Value is null)
            {
                throw new ArgumentException(
                    $"'{dbName}' declares preset '{preset.Path}' with neither a `value` nor `cleared: true`. An absent value "
                    + "is not a start value of zero — say which, because dropping a preset and setting one to the type default "
                    + "must not look the same in the declaration.",
                    nameof(presets));
            }
        }
    }

    private static void Validate(InstanceDbNaming naming)
    {
        ArgumentNullException.ThrowIfNull(naming);

        if (!SafeIdentifier.IsMatch(naming.DbName ?? string.Empty))
            throw new ArgumentException($"'{naming.DbName}' is not a usable DB name.", nameof(naming));

        if (!SafeIdentifier.IsMatch(naming.FbName ?? string.Empty))
            throw new ArgumentException($"'{naming.FbName}' is not a usable FB name.", nameof(naming));

        if (naming.DbNumber <= 0)
        {
            throw new ArgumentException(
                $"'{naming.DbName}' has no block number. It is required and never defaulted: hard rule 3 forbids inventing "
                + "one, and X-J reserves 9000-9999 for harness objects — allocate it with "
                + "`converter claim --allocate --kind block-number --type DB --floor 9000`.",
                nameof(naming));
        }

        if (string.IsNullOrWhiteSpace(naming.Comment))
        {
            throw new ArgumentException(
                $"'{naming.DbName}' has no comment. Every DB in the committed corpus carries one and the generator will not "
                + "invent it: a generated \"Instance of " + naming.FbName + "\" passes review and tells a reader nothing about "
                + "why this instance exists or what is written into it.",
                nameof(naming));
        }
    }

    private static void AppendSection(
        StringBuilder ir, string keyword, IReadOnlyList<string>? members, string dbName, bool projecting)
    {
        // Null is "the FB has no such section at all"; an empty list is "present but empty". The IR grammar
        // preserves the difference on both sides, so the projection does too.
        if (members is null)
            return;

        ir.Append("  ").Append(keyword).Append('\n');
        foreach (var line in members)
        {
            var member = IrMemberLineReader.Parse(line);

            // 🔴 A FORMAL PARAMETER'S DEFAULT IS A PRESET TOO, and the preset paths address the MEMBERS
            // section, so there is nowhere here to declare one. Refused rather than carried: no committed
            // instance DB has a populated INPUT/OUTPUT section at all, so this fires on new ground only, and
            // it fires loudly instead of quietly copying a default into every instance.
            if (projecting && member.StartValue is not null)
            {
                throw new ArgumentException(
                    $"'{dbName}' would carry the FB's {keyword} default '{member.Name} = {member.StartValue}' into the "
                    + "instance. A formal parameter's default is a preset like any other, and the preset paths address the "
                    + "MEMBERS section, so there is nowhere to declare this one. Declare `leftToTia` members — which says in "
                    + "one word that this instance takes the FB's own start values — or remove the default from the FB.",
                    nameof(members));
            }

            ir.Append(member.Render(member.StartValue)).Append('\n');
        }
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

/// <summary>
/// The four interface sections an instance DB is projected from, as RAW LINES. Nothing here interprets a
/// member beyond finding its section — see <see cref="IrMemberLine"/> for why the lines stay opaque.
/// </summary>
internal sealed record FbInterface(
    IReadOnlyList<string>? Input,
    IReadOnlyList<string>? Output,
    IReadOnlyList<string> InOut,
    IReadOnlyList<string> Static)
{
    /// <summary>
    /// Every <c>&lt;path&gt; = &lt;value&gt;</c> the FB's STATIC section declares. Used to report what a
    /// <see cref="InstanceDbMemberSource.LeftToTia"/> instance INHERITS — computed from the FB's own text so
    /// the report cannot disagree with the block it describes.
    /// </summary>
    public IReadOnlyList<string> StartValuePaths()
    {
        var found = new List<string>();
        Walk(Input ?? Array.Empty<string>(), string.Empty, found);
        Walk(Output ?? Array.Empty<string>(), string.Empty, found);
        Walk(InOut, string.Empty, found);
        Walk(Static, string.Empty, found);
        return found;
    }

    private static void Walk(IReadOnlyList<string> lines, string prefix, List<string> found)
    {
        var i = 0;
        while (i < lines.Count)
        {
            if (IrMemberLineReader.IsSubelement(lines[i]))
            {
                var (index, value) = IrMemberLineReader.ParseSubelement(lines[i]);
                found.Add($"{prefix}[{index}] (= {value})");
                i++;
                continue;
            }

            var member = IrMemberLineReader.Parse(lines[i]);
            var path = prefix.Length == 0 ? member.Name : prefix + "." + member.Name;
            if (member.StartValue is not null)
                found.Add($"{path} (= {member.StartValue})");

            i++;
            var childIndent = member.Indent + "  ";
            var children = new List<string>();
            while (i < lines.Count && lines[i].StartsWith(childIndent, StringComparison.Ordinal))
            {
                children.Add(lines[i]);
                i++;
            }

            if (children.Count > 0)
                Walk(children, path, found);
        }
    }

    /// <summary>
    /// Reads an FB's interface out of its IR. <b>Every disagreement with what was declared is a throw</b> —
    /// an <c>INSTANCEOF</c> naming a block that is not the one supplied would be an instance DB of something
    /// nobody looked at.
    /// </summary>
    public static FbInterface Read(string fbIr, string expectedName)
    {
        var lines = fbIr.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0)
            throw new ArgumentException($"the IR supplied for '{expectedName}' is empty.", nameof(fbIr));

        var header = lines[0];
        if (!header.StartsWith("BLOCK FB ", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"the IR supplied for '{expectedName}' starts '{header}'. An instance DB is an instance of an FB; only a "
                + "`BLOCK FB <name>` header can be projected from.",
                nameof(fbIr));
        }

        var actual = header["BLOCK FB ".Length..].Trim();
        if (!string.Equals(actual, expectedName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"the IR supplied for '{expectedName}' is block '{actual}'. The INSTANCEOF would name one block and the "
                + "members would come from another.",
                nameof(fbIr));
        }

        var interfaceAt = Array.IndexOf(lines, "INTERFACE");
        if (interfaceAt < 0)
        {
            throw new ArgumentException(
                $"'{expectedName}' declares no INTERFACE section, so there is nothing to project. An FB with no interface at "
                + "all has no instance data — which is a claim about the block, not something to assume from an absence.",
                nameof(fbIr));
        }

        List<string>? input = null;
        List<string>? output = null;
        var inout = new List<string>();
        List<string>? statics = null;
        List<string>? current = null;

        for (var i = interfaceAt + 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // The interface ends at the first line that is not indented — a NETWORK, a SIDECAR, or the
            // blank line before one.
            if (line.Length == 0)
                continue;
            if (!line.StartsWith("  ", StringComparison.Ordinal))
                break;

            switch (line)
            {
                case "  INPUT": current = input = new List<string>(); continue;
                case "  OUTPUT": current = output = new List<string>(); continue;
                case "  INOUT": current = inout; continue;
                case "  STATIC": current = statics = new List<string>(); continue;

                // TEMP is per-scan scratch and CONSTANT is not stored at all; neither has a home in a DB,
                // and no committed instance DB carries either section.
                case "  TEMP":
                case "  CONSTANT":
                case "  RETURN": current = null; continue;
            }

            if (current is null)
                continue;

            current.Add(line);
        }

        if (statics is null)
        {
            throw new ArgumentException(
                $"'{expectedName}' declares no STATIC section. An FB with no statics has no instance data to project, and an "
                + "instance DB generated from that would be an empty block asserted to mirror something nobody checked.",
                nameof(fbIr));
        }

        return new FbInterface(input, output, inout, statics);
    }
}
