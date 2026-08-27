using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>
/// What the emitted rungs prove about one operand's type. <b>Three answers, and the third is not a
/// failure</b> — "the rungs prove an integer and not its width" is a real, useful, honest result, and it
/// is what makes the difference between a member this component may emit and one it must refuse.
/// </summary>
public enum StimTypeClass
{
    /// <summary>Nothing in the emitted rungs constrains it.</summary>
    Unknown = 0,

    /// <summary>Driven as a coil, or read as an atom of a boolean expression. <c>Bool</c>.</summary>
    Bool = 1,

    /// <summary>Unified with one of the shell's own Time-valued quantities. <c>Time</c>.</summary>
    Time = 2,

    /// <summary>
    /// 🔴 <b>Compared against, or moved from, a plain integer literal — WHICH PROVES AN INTEGER AND NOT
    /// ITS WIDTH.</b> <c>SInt</c>, <c>USInt</c>, <c>Byte</c>, <c>Int</c>, <c>UInt</c>, <c>DInt</c> and
    /// <c>Word</c> all satisfy every rung the shell emits. Choosing among them is a decision about the
    /// range the model can command, and this component does not take it.
    /// </summary>
    Integer = 3,
}

/// <summary>What one operand's type is, and the rung that says so.</summary>
/// <param name="Name">The operand, as the rungs write it.</param>
/// <param name="Class">See <see cref="StimTypeClass"/>.</param>
/// <param name="Datatype">The IR datatype, or null when <see cref="Class"/> does not fix one.</param>
/// <param name="Evidence">
/// The rung that decided it and the network it sits in — carried so a refusal can show its working
/// rather than assert a conclusion.
/// </param>
public sealed record StimOperandType(string Name, StimTypeClass Class, string? Datatype, string Evidence);

/// <summary>
/// 🔴 <b>THE TYPES THE SHELL'S OWN RUNGS PROVE — INFERRED FROM THE EMITTED TEXT, NEVER FROM A LIST
/// SOMEBODY MAINTAINS.</b>
///
/// <para><b>Why it is not a table.</b> <see cref="StimShellGenerator"/> already computes
/// <c>RequiredUdtMembers</c> and <c>RequiredStatics</c>, and both are hand-written lists sitting beside
/// the rungs that reference them — the same shape as the README rule that generator's own
/// <c>BitsTheShellSets</c> replaced with a computation, for the same stated reason: <i>"a number in a
/// document is a thing that goes stale silently the first time a network changes."</i> A type table has
/// that failure and one worse: a member typed from a table the rungs have moved past does not fail, it
/// resolves to the wrong thing.</para>
///
/// <para>🔴 <b>WHAT KEEPS THE INFERENCE FROM DRIFTING: IT REFUSES WHAT IT DOES NOT UNDERSTAND.</b> Every
/// rung form is recognised completely or throws, the discipline <see cref="IrMemberLine"/> holds for the
/// same reason. A shell that grows a rung shape this file has not been taught surfaces as a REFUSAL
/// naming the rung, never as an operand typed by a fallback.</para>
///
/// <para><b>The mechanism.</b> Union-find over operand names, with the shell's own literals as the only
/// seeds: <c>TRUE</c>/<c>FALSE</c> and coil targets seed <c>Bool</c>, a <c>T#</c> literal and a timer's
/// <c>.ET</c> seed <c>Time</c>, a bare decimal seeds <c>Integer</c>. A comparison unifies its two sides,
/// a <c>MOVE</c> unifies destination with source, an <c>ADD</c>/<c>SUB</c> unifies all three. So
/// <c>Stim.ArmAt</c> comes out <c>Time</c> because <c>ScenT >= Stim.ArmAt</c> ties it to <c>ScenT</c>,
/// which <c>MOVE(… IN := T#0S) => ScenT</c> seeds — a chain of three rungs across two networks, which is
/// exactly the kind of thing a table gets wrong.</para>
///
/// <para><b>Two seeds meeting in one set is a THROW, not a winner.</b> An operand the rungs both coil and
/// compare against a time is a defect in the shell, and picking one would hide it.</para>
/// </summary>
public static class StimRungTypes
{
    /// <summary>
    /// The root the shell writes literally in front of every stimulus-UDT member (<c>Stim.Start</c>,
    /// <c>Stim.Phase</c>). Not a parameter: it is written into rungs this component emits itself.
    /// </summary>
    public const string StimRoot = "Stim";

    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex Path = new("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);
    private static readonly Regex TimeLiteral = new("^T#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IntegerLiteral = new("^[+-]?[0-9]+$", RegexOptions.Compiled);

    /// <summary>Comparison operators, longest first so <c>&gt;=</c> is not read as <c>&gt;</c>.</summary>
    private static readonly string[] Comparisons = { " <> ", " >= ", " <= ", " = ", " > ", " < " };

    /// <summary>
    /// Infers every operand's type from the emitted networks, <b>in first-reference order</b> — which is
    /// what gives the generated UDT a member order derived from the shell rather than chosen.
    /// </summary>
    /// <exception cref="ArgumentException">A rung this file does not fully recognise, or a contradiction.</exception>
    public static IReadOnlyList<StimOperandType> Infer(IReadOnlyList<StimShellNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);

        var sets = new UnionFind();

        foreach (var network in networks)
        {
            foreach (var rung in network.Rungs)
                Read(rung, $"{network.Id}/N{network.Number} `{rung}`", sets);
        }

        return sets.Resolve();
    }

    // --- reading one rung -----------------------------------------------------------------------------

    private static void Read(string rung, string where, UnionFind sets)
    {
        var text = rung.Trim();

        foreach (var coil in new[] { "COIL ", "SCOIL ", "RCOIL " })
        {
            if (!text.StartsWith(coil, StringComparison.Ordinal))
                continue;

            var split = text.IndexOf(" := ", StringComparison.Ordinal);
            if (split < 0)
                throw Unrecognised(where, "a coil rung with no ` := `");

            var target = text[coil.Length..split].Trim();
            sets.Seed(Operand(target, where), StimTypeClass.Bool, where);
            ReadBoolean(text[(split + 4)..], where, sets);
            return;
        }

        ReadCall(text, where, sets);
    }

    private static void ReadCall(string text, string where, UnionFind sets)
    {
        // `<NAME>(<args>)` with an optional ` => <destination>` after the closing parenthesis.
        var open = text.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
            throw Unrecognised(where, "not a coil and not a call");

        var instruction = text[..open].Trim();
        var close = MatchingParen(text, open, where);
        var arguments = SplitTopLevel(text[(open + 1)..close], ',');
        var destination = text[(close + 1)..].Trim();

        if (destination.Length > 0)
        {
            if (!destination.StartsWith("=> ", StringComparison.Ordinal))
                throw Unrecognised(where, $"trailing text `{destination}` that is not a ` => <destination>`");

            destination = destination[3..].Trim();
        }

        var ports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            var at = argument.IndexOf(" := ", StringComparison.Ordinal);
            if (at < 0)
            {
                // A bare first argument is an instance path (a TON's timer). It carries no type this
                // inference can use and none it needs.
                continue;
            }

            ports[argument[..at].Trim()] = argument[(at + 4)..].Trim();
        }

        switch (instruction)
        {
            case "MOVE":
                ReadBoolean(Require(ports, "EN", where), where, sets);
                sets.Union(Value(Require(ports, "IN", where), where, sets), Operand(destination, where), where);
                return;

            case "ADD":
            case "SUB":
                ReadBoolean(Require(ports, "EN", where), where, sets);
                var in1 = Value(Require(ports, "IN1", where), where, sets);
                var in2 = Value(Require(ports, "IN2", where), where, sets);
                sets.Union(in1, in2, where);
                sets.Union(in1, Operand(destination, where), where);
                return;

            case "TON":
            case "TOF":
                ReadBoolean(Require(ports, "IN", where), where, sets);
                sets.Seed(Value(Require(ports, "PT", where), where, sets), StimTypeClass.Time, where);
                return;

            default:
                throw Unrecognised(where, $"instruction `{instruction}` is not one this inference reads");
        }
    }

    /// <summary>
    /// A boolean expression: terms joined by top-level <c>AND</c>/<c>OR</c>, each optionally negated, each
    /// either a parenthesised sub-expression, a comparison, or a plain operand — which is therefore Bool.
    /// </summary>
    private static void ReadBoolean(string expression, string where, UnionFind sets)
    {
        foreach (var raw in SplitBooleanTerms(expression))
        {
            var term = raw.Trim();

            while (term.StartsWith("NOT ", StringComparison.Ordinal))
                term = term[4..].Trim();

            if (term.Length == 0)
                throw Unrecognised(where, "an empty term in a boolean expression");

            if (term.StartsWith('(') && MatchingParen(term, 0, where) == term.Length - 1)
            {
                ReadBoolean(term[1..^1], where, sets);
                continue;
            }

            var comparison = Comparisons.FirstOrDefault(op => IndexAtTopLevel(term, op) >= 0);
            if (comparison is not null)
            {
                var at = IndexAtTopLevel(term, comparison);
                var left = Value(term[..at].Trim(), where, sets);
                var right = Value(term[(at + comparison.Length)..].Trim(), where, sets);
                sets.Union(left, right, where);
                continue;
            }

            sets.Seed(Operand(term, where), StimTypeClass.Bool, where);
        }
    }

    /// <summary>
    /// One value position: an operand, or a literal. A literal is given its own node and seeded, so a
    /// literal can carry a type into a set without being mistaken for a member of it.
    /// </summary>
    private static string Value(string text, string where, UnionFind sets)
    {
        if (string.Equals(text, "TRUE", StringComparison.Ordinal) || string.Equals(text, "FALSE", StringComparison.Ordinal))
        {
            sets.Seed(text, StimTypeClass.Bool, where);
            return text;
        }

        if (TimeLiteral.IsMatch(text))
        {
            sets.Seed(text, StimTypeClass.Time, where);
            return text;
        }

        if (IntegerLiteral.IsMatch(text))
        {
            // 🔴 A LITERAL NODE PER VALUE, NOT ONE SHARED "integer" NODE. Sharing one would unify every
            // operand ever compared against any integer into a single set — `Stim.Phase` and
            // `Stim.ResetMode` would become the same type by accident, and a contradiction anywhere would
            // be reported against all of them.
            var node = "#int:" + text;
            sets.Seed(node, StimTypeClass.Integer, where);
            return node;
        }

        var operand = Operand(text, where);

        // A timer's elapsed time. The only instruction member the shell reads, and it is a Time.
        if (operand.EndsWith(".ET", StringComparison.Ordinal))
            sets.Seed(operand, StimTypeClass.Time, where);

        return operand;
    }

    private static string Operand(string text, string where)
    {
        if (!Path.IsMatch(text))
            throw Unrecognised(where, $"`{text}` is not an operand this inference can read");

        return text;
    }

    // --- text ------------------------------------------------------------------------------------------

    private static string Require(IReadOnlyDictionary<string, string> ports, string port, string where) =>
        ports.TryGetValue(port, out var value)
            ? value
            : throw Unrecognised(where, $"no `{port} :=` argument");

    private static int MatchingParen(string text, int open, string where)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')' && --depth == 0)
                return i;
        }

        throw Unrecognised(where, "unbalanced parentheses");
    }

    private static IReadOnlyList<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
                depth--;
            else if (text[i] == separator && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts.Where(p => p.Trim().Length > 0).Select(p => p.Trim()).ToList();
    }

    private static IReadOnlyList<string> SplitBooleanTerms(string text)
    {
        var terms = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
                depth--;
            else if (depth == 0)
            {
                foreach (var joiner in new[] { " AND ", " OR " })
                {
                    if (i + joiner.Length > text.Length || string.CompareOrdinal(text, i, joiner, 0, joiner.Length) != 0)
                        continue;

                    terms.Add(text[start..i]);
                    start = i + joiner.Length;
                    i += joiner.Length - 1;
                    break;
                }
            }
        }

        terms.Add(text[start..]);
        return terms.Where(t => t.Trim().Length > 0).ToList();
    }

    private static int IndexAtTopLevel(string text, string needle)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(')
                depth++;
            else if (text[i] == ')')
                depth--;
            else if (depth == 0 && i + needle.Length <= text.Length
                     && string.CompareOrdinal(text, i, needle, 0, needle.Length) == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static ArgumentException Unrecognised(string where, string why) =>
        new($"the stimulus shell emitted a rung this type inference does not fully understand — {why}, at {where}. "
            + "*** IT IS REFUSED RATHER THAN PARTIALLY READ. *** An inference that skipped what it did not recognise "
            + "would type the operands it did understand and stay silent about the rest, which is a UDT member given a "
            + "confident wrong type instead of a refusal. Teach this file the rung shape, or the shell has grown a "
            + "construct the generated UDT cannot be derived from.", nameof(where));

    // --- the sets ---------------------------------------------------------------------------------------

    private sealed class UnionFind
    {
        private readonly Dictionary<string, string> _parent = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (StimTypeClass Class, string Where)> _seed = new(StringComparer.Ordinal);

        /// <summary>
        /// Every node in FIRST-REFERENCE order. It is what gives the generated UDT a member order, and it
        /// is recorded HERE — by the one reader of the rungs — rather than by a second scan. A second scan
        /// is a second reader of the same text, free to disagree with this one about what a rung mentions.
        /// </summary>
        private readonly List<string> _order = new();

        public void Seed(string node, StimTypeClass type, string where)
        {
            var root = Find(node);
            if (!_seed.TryGetValue(root, out var existing))
            {
                _seed[root] = (type, where);
                return;
            }

            if (existing.Class != type)
                throw Contradiction(node, existing, (type, where));
        }

        public void Union(string left, string right, string where)
        {
            var a = Find(left);
            var b = Find(right);
            if (string.Equals(a, b, StringComparison.Ordinal))
                return;

            var seedA = _seed.TryGetValue(a, out var sa) ? sa : ((StimTypeClass, string)?)null;
            var seedB = _seed.TryGetValue(b, out var sb) ? sb : ((StimTypeClass, string)?)null;

            if (seedA is { } x && seedB is { } y && x.Item1 != y.Item1)
                throw Contradiction($"{left} and {right} (joined at {where})", x, y);

            _parent[a] = b;
            _seed.Remove(a);

            if (seedA is { } carried && seedB is null)
                _seed[b] = carried;
        }

        public IReadOnlyList<StimOperandType> Resolve()
        {
            var resolved = new List<StimOperandType>();

            foreach (var node in _order.ToArray())
            {
                if (node.StartsWith('#') || node is "TRUE" or "FALSE" || TimeLiteral.IsMatch(node))
                    continue;

                var root = Find(node);
                var (type, where) = _seed.TryGetValue(root, out var seed)
                    ? seed
                    : (StimTypeClass.Unknown, "no emitted rung constrains it");

                resolved.Add(new StimOperandType(node, type, DatatypeOf(type), where));
            }

            return resolved;
        }

        private string Find(string node)
        {
            if (!_parent.TryGetValue(node, out var parent))
            {
                _parent[node] = node;
                _order.Add(node);
                return node;
            }

            if (string.Equals(parent, node, StringComparison.Ordinal))
                return node;

            var root = Find(parent);
            _parent[node] = root;
            return root;
        }

        private static string? DatatypeOf(StimTypeClass type) => type switch
        {
            StimTypeClass.Bool => "Bool",
            StimTypeClass.Time => "Time",
            _ => null,
        };

        private static ArgumentException Contradiction(
            string what, (StimTypeClass Class, string Where) first, (StimTypeClass Class, string Where) second) =>
            new($"the emitted rungs CONTRADICT each other about '{what}': {first.Where} makes it {first.Class}, and "
                + $"{second.Where} makes it {second.Class}. Neither is preferred here — an operand the shell both coils "
                + "and compares against a duration is a defect in the shell, and picking a winner would emit a UDT that "
                + "compiles while one of the two networks reads the wrong thing.", nameof(what));
    }

    /// <summary>Whether an operand is a member of the stimulus UDT, and which member.</summary>
    /// <returns>The bare member name, or null when the operand is not a top-level stimulus member.</returns>
    public static string? StimMemberName(string operand)
    {
        if (!operand.StartsWith(StimRoot + ".", StringComparison.Ordinal))
            return null;

        var member = operand[(StimRoot.Length + 1)..];
        return Identifier.IsMatch(member) ? member : null;
    }
}
