using System.Globalization;
using System.Text.RegularExpressions;

namespace Harness.Skeleton;

/// <summary>An IR construct this interpreter does not implement. Always a throw, never a skipped line.</summary>
public sealed class UnsupportedIrException : Exception
{
    public UnsupportedIrException(string message) : base(message) { }
}

/// <summary>Where a tag lives in bit memory, and how wide it is.</summary>
public sealed record TagAddress(int Byte, int Bit, int Width)
{
    /// <summary>A single bit.</summary>
    public bool IsBit => Width == 0;
}

/// <summary>
/// A tiny interpreter for exactly the LAD subset the harness generators emit — and NOTHING else.
///
/// <para><b>Why this exists at all.</b> Phase 2's exit criterion is that "a deliberately introduced bug
/// in the block under test must produce a RED result, and the corrected block must produce a GREEN one",
/// and it cannot be run end to end without a rig. The weak way to demonstrate it PC-side is to emulate
/// the block in C# and flip a boolean: that proves the harness reacts to a C# flag, not to the artifact.
/// This runs the GENERATED IR TEXT, so the defect lives where it will live on the device — in one
/// operator in one rung — and the red comes from the thing that will be downloaded.</para>
///
/// <para><b>What it deliberately is not.</b> Not a CPU model. It does not reproduce scan-cycle timing,
/// process-image update boundaries, instruction ENO chaining, overflow flags, optimized-versus-standard
/// access, retentive behaviour, or anything TIA does at compile time. <b>Green here is not evidence that
/// the block compiles or that it behaves this way on a 1214C</b> — it is evidence that the harness
/// sequence, the map, the copy layer and the model agree about a program whose text is fixed.</para>
///
/// <para><b>Every unsupported construct is a throw.</b> A silently ignored statement is the one failure
/// mode that would make this instrument dangerous: the interpreter would run, the demonstration would go
/// green, and it would be green because a rung was not executed.</para>
/// </summary>
public sealed class LadProgram
{
    private static readonly Regex TagLine = new(
        @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+[0-9A-Fa-f]+\s*:\s*(?<type>\S+)\s*@\s*(?<addr>%\S+)",
        RegexOptions.Compiled);

    private static readonly Regex MemoryAddress = new(
        @"^%M(?<size>[BWD]?)(?<byte>\d+)(?:\.(?<bit>\d+))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MoveLine = new(
        @"^MOVE\(\s*EN\s*:=\s*(?<en>.+?)\s*,\s*IN\s*:=\s*(?<in>.+?)\s*\)\s*=>\s*(?<dest>\S+)$",
        RegexOptions.Compiled);

    private static readonly Regex AddLine = new(
        @"^ADD\(\s*EN\s*:=\s*(?<en>.+?)\s*,\s*IN1\s*:=\s*(?<in1>.+?)\s*,\s*IN2\s*:=\s*(?<in2>.+?)\s*\)\s*=>\s*(?<dest>\S+)$",
        RegexOptions.Compiled);

    private static readonly Regex CoilLine = new(
        @"^(?<kind>S?)COIL\s+(?<dest>\S+)\s*:=\s*(?<expr>.+)$",
        RegexOptions.Compiled);

    private readonly Dictionary<string, TagAddress> _tags = new(StringComparer.Ordinal);
    private readonly List<Statement> _statements = new();

    /// <summary>Every tag this program can address, by name.</summary>
    public IReadOnlyDictionary<string, TagAddress> Tags => _tags;

    /// <summary>Statements, in the order one scan executes them.</summary>
    public int StatementCount => _statements.Count;

    /// <summary>Declare a tag table's symbols. Every block loaded afterwards resolves against them.</summary>
    public LadProgram WithTagTable(string ir)
    {
        ArgumentNullException.ThrowIfNull(ir);
        var inTags = false;

        foreach (var raw in Lines(ir))
        {
            var line = raw.Trim();

            if (!inTags)
            {
                inTags = string.Equals(line, "TAGS", StringComparison.Ordinal);
                continue;
            }

            if (line.Length == 0)
                continue;

            var match = TagLine.Match(raw);
            if (!match.Success)
                throw new UnsupportedIrException($"tag declaration not understood: '{line}'.");

            var address = ParseAddress(match.Groups["addr"].Value, match.Groups["type"].Value);
            var name = match.Groups["name"].Value;

            if (!_tags.TryAdd(name, address))
                throw new UnsupportedIrException($"tag '{name}' is declared twice, and the second declaration would silently win.");
        }

        return this;
    }

    /// <summary>Load one block's networks. Blocks execute in the order they are loaded — the OB1 call order.</summary>
    public LadProgram WithBlock(string ir)
    {
        ArgumentNullException.ThrowIfNull(ir);
        var inBody = false;

        foreach (var raw in Lines(ir))
        {
            var line = raw.Trim();

            if (line.StartsWith("NETWORK ", StringComparison.Ordinal))
            {
                inBody = true;
                continue;
            }

            if (!inBody || line.Length == 0)
                continue;

            _statements.Add(Parse(line));
        }

        return this;
    }

    /// <summary>Execute one scan against a bit-memory image.</summary>
    public void Scan(byte[] memory)
    {
        ArgumentNullException.ThrowIfNull(memory);

        foreach (var statement in _statements)
            statement.Execute(memory);
    }

    // -------------------------------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------------------------------

    private Statement Parse(string line)
    {
        var move = MoveLine.Match(line);
        if (move.Success)
        {
            return new MoveStatement(
                ParseExpression(move.Groups["en"].Value),
                ParseOperand(move.Groups["in"].Value),
                Resolve(move.Groups["dest"].Value));
        }

        var add = AddLine.Match(line);
        if (add.Success)
        {
            return new AddStatement(
                ParseExpression(add.Groups["en"].Value),
                ParseOperand(add.Groups["in1"].Value),
                ParseOperand(add.Groups["in2"].Value),
                Resolve(add.Groups["dest"].Value));
        }

        var coil = CoilLine.Match(line);
        if (coil.Success)
        {
            var dest = Resolve(coil.Groups["dest"].Value);
            if (!dest.IsBit)
                throw new UnsupportedIrException($"'{line}' drives a coil onto a {dest.Width}-byte tag; a coil writes a bit.");

            // A SET coil never clears. That asymmetry is the whole reason the start echo can be trusted:
            // a level coil would drop back low the moment the block's start condition did, and a test that
            // began and ended between two polls would read as never having run.
            return new CoilStatement(ParseExpression(coil.Groups["expr"].Value), dest,
                SetOnly: coil.Groups["kind"].Value == "S");
        }

        throw new UnsupportedIrException(
            $"statement not implemented by this interpreter: '{line}'. It implements MOVE, ADD and COIL, which is exactly what the harness generators emit — anything else is a construct whose behaviour here would be a guess.");
    }

    private TagAddress Resolve(string name) =>
        _tags.TryGetValue(name, out var address)
            ? address
            : throw new UnsupportedIrException($"'{name}' is not declared in any tag table loaded into this program. An undeclared destination would be a write to nowhere.");

    private static TagAddress ParseAddress(string address, string type)
    {
        var match = MemoryAddress.Match(address);
        if (!match.Success)
            throw new UnsupportedIrException($"address '{address}' is not bit memory; this interpreter models %M and nothing else.");

        var byteAddress = int.Parse(match.Groups["byte"].Value, CultureInfo.InvariantCulture);
        var size = match.Groups["size"].Value.ToUpperInvariant();

        if (match.Groups["bit"].Success)
            return new TagAddress(byteAddress, int.Parse(match.Groups["bit"].Value, CultureInfo.InvariantCulture), 0);

        return size switch
        {
            "W" => new TagAddress(byteAddress, 0, 2),
            "D" => new TagAddress(byteAddress, 0, 4),
            "B" => new TagAddress(byteAddress, 0, 1),
            _ => throw new UnsupportedIrException($"address '{address}' (declared {type}) names neither a bit nor a sized word."),
        };
    }

    private Operand ParseOperand(string text)
    {
        text = text.Trim();

        if (text.StartsWith("16#", StringComparison.Ordinal))
            return new Literal(unchecked((int)uint.Parse(text[3..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return new Literal(value);

        return new TagOperand(Resolve(text));
    }

    /// <summary>
    /// The expression grammar, and it is the whole of what the generators emit:
    /// <c>expr := and ( 'OR' and )* ; and := atom ( 'AND' atom )* ; atom := 'NOT' atom | '(' expr ')' |
    /// operand [ cmp operand ]</c>. A bare operand is a contact; anything unparsed is a throw.
    /// </summary>
    private Expression ParseExpression(string text)
    {
        var tokens = Tokenize(text);
        var position = 0;
        var expression = ParseOr(tokens, ref position);

        if (position != tokens.Count)
            throw new UnsupportedIrException($"expression '{text}' has trailing text this interpreter does not understand from token {position}.");

        return expression;
    }

    private Expression ParseOr(List<string> tokens, ref int i)
    {
        var left = ParseAnd(tokens, ref i);

        while (i < tokens.Count && tokens[i] == "OR")
        {
            i++;
            left = new BinaryLogic(left, ParseAnd(tokens, ref i), Or: true);
        }

        return left;
    }

    private Expression ParseAnd(List<string> tokens, ref int i)
    {
        var left = ParseAtom(tokens, ref i);

        while (i < tokens.Count && tokens[i] == "AND")
        {
            i++;
            left = new BinaryLogic(left, ParseAtom(tokens, ref i), Or: false);
        }

        return left;
    }

    private Expression ParseAtom(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count)
            throw new UnsupportedIrException("expression ended where a term was expected.");

        var token = tokens[i];

        if (token == "NOT")
        {
            i++;
            return new Negation(ParseAtom(tokens, ref i));
        }

        if (token == "(")
        {
            i++;
            var inner = ParseOr(tokens, ref i);
            if (i >= tokens.Count || tokens[i] != ")")
                throw new UnsupportedIrException("unbalanced parentheses in an expression.");
            i++;
            return inner;
        }

        if (token is "AND" or "OR" or ")")
            throw new UnsupportedIrException($"'{token}' appears where a term was expected.");

        i++;

        if (token == "TRUE")
            return new Constant(true);

        if (token == "FALSE")
            return new Constant(false);

        if (i < tokens.Count && Comparisons.Contains(tokens[i]))
        {
            var op = tokens[i];
            i++;

            if (i >= tokens.Count)
                throw new UnsupportedIrException($"comparison '{op}' has no right-hand operand.");

            var right = ParseOperand(tokens[i]);
            i++;
            return new Comparison(ParseOperand(token), op, right);
        }

        var address = Resolve(token);
        if (!address.IsBit)
            throw new UnsupportedIrException($"'{token}' is used as a contact but it is a {address.Width}-byte tag; a contact reads a bit.");

        return new Contact(address);
    }

    private static readonly HashSet<string> Comparisons = new(StringComparer.Ordinal) { "=", "<>", ">=", "<=", ">", "<" };

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c is '(' or ')') { tokens.Add(c.ToString()); i++; continue; }

            if (c is '<' or '>' or '=')
            {
                var two = i + 1 < text.Length ? text.Substring(i, 2) : string.Empty;
                if (two is "<=" or ">=" or "<>") { tokens.Add(two); i += 2; continue; }
                tokens.Add(c.ToString()); i++; continue;
            }

            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('(' or ')' or '<' or '>' or '='))
                i++;

            if (i == start)
                throw new UnsupportedIrException($"could not tokenize '{text}' at character {i}.");

            tokens.Add(text[start..i]);
        }

        return tokens;
    }

    private static IEnumerable<string> Lines(string ir) => ir.Replace("\r\n", "\n").Split('\n');

    // -------------------------------------------------------------------------------------------------
    // Memory
    // -------------------------------------------------------------------------------------------------

    /// <summary>Read a tag, sign-extended for word and double-word tags as S7 Int/DInt are.</summary>
    public static int Read(byte[] memory, TagAddress address) => address.Width switch
    {
        0 => (memory[address.Byte] & (1 << address.Bit)) != 0 ? 1 : 0,
        1 => memory[address.Byte],
        2 => (short)((memory[address.Byte] << 8) | memory[address.Byte + 1]),
        4 => (memory[address.Byte] << 24) | (memory[address.Byte + 1] << 16) | (memory[address.Byte + 2] << 8) | memory[address.Byte + 3],
        _ => throw new UnsupportedIrException($"a {address.Width}-byte tag is not one this interpreter models."),
    };

    /// <summary>Write a tag, big-endian, as S7 lays out bit memory.</summary>
    public static void Write(byte[] memory, TagAddress address, int value)
    {
        switch (address.Width)
        {
            case 0:
                if (value != 0) memory[address.Byte] |= (byte)(1 << address.Bit);
                else memory[address.Byte] &= (byte)~(1 << address.Bit);
                break;
            case 1:
                memory[address.Byte] = (byte)value;
                break;
            case 2:
                memory[address.Byte] = (byte)(value >> 8);
                memory[address.Byte + 1] = (byte)value;
                break;
            case 4:
                memory[address.Byte] = (byte)(value >> 24);
                memory[address.Byte + 1] = (byte)(value >> 16);
                memory[address.Byte + 2] = (byte)(value >> 8);
                memory[address.Byte + 3] = (byte)value;
                break;
            default:
                throw new UnsupportedIrException($"a {address.Width}-byte tag is not one this interpreter models.");
        }
    }

    // -------------------------------------------------------------------------------------------------
    // The model
    // -------------------------------------------------------------------------------------------------

    private abstract record Operand
    {
        public abstract int Read(byte[] memory);
    }

    private sealed record Literal(int Value) : Operand
    {
        public override int Read(byte[] memory) => Value;
    }

    private sealed record TagOperand(TagAddress Address) : Operand
    {
        public override int Read(byte[] memory) => LadProgram.Read(memory, Address);
    }

    private abstract record Expression
    {
        public abstract bool Evaluate(byte[] memory);
    }

    private sealed record Constant(bool Value) : Expression
    {
        public override bool Evaluate(byte[] memory) => Value;
    }

    private sealed record Contact(TagAddress Address) : Expression
    {
        public override bool Evaluate(byte[] memory) => LadProgram.Read(memory, Address) != 0;
    }

    private sealed record Negation(Expression Inner) : Expression
    {
        public override bool Evaluate(byte[] memory) => !Inner.Evaluate(memory);
    }

    private sealed record BinaryLogic(Expression Left, Expression Right, bool Or) : Expression
    {
        public override bool Evaluate(byte[] memory) =>
            Or ? Left.Evaluate(memory) || Right.Evaluate(memory)
               : Left.Evaluate(memory) && Right.Evaluate(memory);
    }

    private sealed record Comparison(Operand Left, string Op, Operand Right) : Expression
    {
        public override bool Evaluate(byte[] memory)
        {
            var l = Left.Read(memory);
            var r = Right.Read(memory);

            return Op switch
            {
                "=" => l == r,
                "<>" => l != r,
                ">=" => l >= r,
                "<=" => l <= r,
                ">" => l > r,
                "<" => l < r,
                _ => throw new UnsupportedIrException($"comparison '{Op}' is not implemented."),
            };
        }
    }

    private abstract record Statement
    {
        public abstract void Execute(byte[] memory);
    }

    private sealed record MoveStatement(Expression En, Operand In, TagAddress Dest) : Statement
    {
        public override void Execute(byte[] memory)
        {
            if (En.Evaluate(memory))
                LadProgram.Write(memory, Dest, In.Read(memory));
        }
    }

    private sealed record AddStatement(Expression En, Operand In1, Operand In2, TagAddress Dest) : Statement
    {
        public override void Execute(byte[] memory)
        {
            if (En.Evaluate(memory))
                LadProgram.Write(memory, Dest, In1.Read(memory) + In2.Read(memory));
        }
    }

    private sealed record CoilStatement(Expression Expr, TagAddress Dest, bool SetOnly) : Statement
    {
        public override void Execute(byte[] memory)
        {
            var value = Expr.Evaluate(memory);

            if (SetOnly)
            {
                if (value)
                    LadProgram.Write(memory, Dest, 1);

                return;
            }

            LadProgram.Write(memory, Dest, value ? 1 : 0);
        }
    }
}
