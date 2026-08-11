using System.Text.Json;

namespace Harness.S7;

/// <summary>
/// The elementary types this transport can address. Deliberately only the ones a conformance vector
/// actually asserts on.
///
/// STRING is absent: an S7 STRING is a variable-length structure (max length, current length, then
/// characters) and giving it a fixed size in a tag map invites reading half of one. The one place a
/// string IS read — the rig marker DB — reads it explicitly, with its length declared at the call
/// site (<see cref="MarkerDbIdentitySource"/>).
/// </summary>
public enum S7DataType
{
    Bool,
    Byte,
    Word,
    Int,
    DWord,
    DInt,
    UDInt,
    Real,
}

public static class S7DataTypeExtensions
{
    /// <summary>Bytes occupied. A Bool occupies one BIT, but the smallest transfer is a byte.</summary>
    public static int SizeInBytes(this S7DataType t) => t switch
    {
        S7DataType.Bool => 1,
        S7DataType.Byte => 1,
        S7DataType.Word or S7DataType.Int => 2,
        S7DataType.DWord or S7DataType.DInt or S7DataType.UDInt or S7DataType.Real => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "unknown S7 data type."),
    };
}

/// <summary>
/// One symbolic name bound to a physical S7 address.
///
/// <para><b>Why this exists at all.</b> <see cref="Harness.ITransport"/> is symbolic — vectors name
/// tags. Classic S7comm is not: it can address a DB number, a byte offset and a length, and nothing
/// else. There is no symbol table on the wire, so something has to hold the binding, and it may as
/// well be a thing that can be reviewed against the TIA DB layout by eye.</para>
///
/// <para><b>Two conditions that must hold in TIA</b>, neither of which this code can check and both
/// of which produce confusing failures: the DB must have "Optimized block access" turned OFF (an
/// optimized DB has no stable byte offsets and is simply not reachable this way), and the CPU's
/// protection settings must permit PUT/GET access from a remote partner.</para>
/// </summary>
/// <param name="Area">
/// The named area this tag belongs to — in practice the DB's symbolic name. This is what the WRITE
/// FENCE is scoped on, so it is part of the tag rather than something the caller asserts: see
/// <see cref="S7Transport.Write"/>, which refuses when a caller's claimed area disagrees with the
/// tag's own. A fence authorizing area X while the bytes land in area Y is a fence in name only.
/// </param>
/// <param name="BitOffset">0–7, and meaningful only for <see cref="S7DataType.Bool"/>.</param>
public sealed record S7Tag(
    string Name,
    string Area,
    int DbNumber,
    int ByteOffset,
    S7DataType Type,
    int BitOffset = 0)
{
    /// <summary>First bit of this tag counted from the start of the DB — the common currency in which
    /// two tags of different widths can be compared for overlap.</summary>
    public int FirstBit => (ByteOffset * 8) + (Type == S7DataType.Bool ? BitOffset : 0);

    public int BitLength => Type == S7DataType.Bool ? 1 : Type.SizeInBytes() * 8;

    public bool OverlapsWith(S7Tag other) =>
        DbNumber == other.DbNumber
        && FirstBit < other.FirstBit + other.BitLength
        && other.FirstBit < FirstBit + BitLength;

    public string Describe() => Type == S7DataType.Bool
        ? $"{Name} = DB{DbNumber}.DBX{ByteOffset}.{BitOffset} ({Type}, area '{Area}')"
        : $"{Name} = DB{DbNumber}.DBB{ByteOffset} ({Type}, area '{Area}')";

    /// <summary>Structural checks that catch the map typos which otherwise surface as garbage data.</summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new S7ConfigurationException("a tag with no name is not addressable.");

        if (string.IsNullOrWhiteSpace(Area))
            throw new S7ConfigurationException(
                $"tag '{Name}' declares no area. The area is what the write fence is scoped on; a tag " +
                "that does not say which area it lives in cannot be written under a scoped run.");

        if (DbNumber <= 0)
            throw new S7ConfigurationException($"tag '{Name}': DB number must be positive, got {DbNumber}.");

        if (ByteOffset < 0)
            throw new S7ConfigurationException($"tag '{Name}': byte offset must not be negative.");

        if (Type == S7DataType.Bool)
        {
            if (BitOffset is < 0 or > 7)
                throw new S7ConfigurationException($"tag '{Name}': bit offset must be 0–7, got {BitOffset}.");
        }
        else
        {
            if (BitOffset != 0)
                throw new S7ConfigurationException(
                    $"tag '{Name}': a bit offset is meaningful only for a Bool, but this tag is {Type}.");

            // A non-optimized S7 DB aligns WORD-sized and larger elements on even byte boundaries. An
            // odd offset here is a transcription slip roughly every time, and it reads plausible
            // garbage rather than failing, so refuse it.
            if (Type.SizeInBytes() > 1 && ByteOffset % 2 != 0)
                throw new S7ConfigurationException(
                    $"tag '{Name}': a {Type} sits at an even byte offset in a standard (non-optimized) " +
                    $"DB, but this one is at {ByteOffset}. Check the offset against the DB layout — an " +
                    "odd offset here reads plausible-looking garbage instead of failing.");
        }
    }
}

/// <summary>
/// The symbol table: every tag a vector may name, bound to a real address, checked for the mistakes
/// that a hand-written map makes.
///
/// <para><b>An unknown tag is a hard error, never an empty read.</b> Note the contrast with
/// <see cref="Harness.FakeTransport"/>, which returns "" for a tag it does not know — appropriate for
/// authoring, where a typo shows up as a failed assertion you can see. Against a device the same
/// leniency would let a typo'd tag name silently compare "" against "" and pass. So this refuses.</para>
/// </summary>
public sealed class S7TagMap
{
    private readonly Dictionary<string, S7Tag> _byName;

    public S7TagMap(IEnumerable<S7Tag> tags)
    {
        var list = (tags ?? throw new ArgumentNullException(nameof(tags))).ToList();

        foreach (var t in list) t.Validate();

        _byName = new Dictionary<string, S7Tag>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in list)
        {
            if (_byName.TryGetValue(t.Name, out var existing))
                throw new S7ConfigurationException(
                    $"tag '{t.Name}' is defined twice ({existing.Describe()} and {t.Describe()}). " +
                    "Names are matched case-insensitively because the vectors are written by hand.");

            _byName[t.Name] = t;
        }

        // Overlap detection. Two tags occupying the same bits is a map error in every case that
        // matters, and it is invisible at run time: a write to one silently changes the other. Bools
        // sharing a byte at DIFFERENT bit positions do not overlap and are entirely normal, which is
        // why the comparison is in bits rather than bytes.
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (list[i].OverlapsWith(list[j]))
                    throw new S7ConfigurationException(
                        $"tags '{list[i].Name}' and '{list[j].Name}' occupy overlapping bits " +
                        $"({list[i].Describe()} / {list[j].Describe()}). Writing one would silently " +
                        "change the other.");
            }
        }

        Tags = list;
    }

    public IReadOnlyList<S7Tag> Tags { get; }

    /// <summary>Every distinct area named by the map — the vocabulary the write fence's scope must use.</summary>
    public IReadOnlyList<string> Areas =>
        Tags.Select(t => t.Area).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool TryResolve(string? name, out S7Tag tag)
    {
        if (!string.IsNullOrWhiteSpace(name)) return _byName.TryGetValue(name.Trim(), out tag!);
        tag = null!;
        return false;
    }

    public S7Tag Resolve(string? name)
    {
        if (TryResolve(name, out var tag)) return tag;

        throw new S7ConfigurationException(
            $"no tag named '{name}' is in the tag map, so there is no address to read or write. " +
            "An unknown tag is refused rather than read as empty: an empty read would let a typo in a " +
            "vector compare equal to an empty expectation and pass.");
    }

    // ---------------------------------------------------------------- JSON form

    private sealed record TagShape(
        string? Name, string? Area, int Db, int Byte, string? Type, int Bit = 0);

    private sealed record MapShape(List<TagShape>? Tags);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parse a tag map. Shape:
    /// <code>{ "tags": [ { "name": "Sensor_Level", "area": "DB_Interface", "db": 10, "byte": 4, "type": "Real" } ] }</code>
    /// Every failure is an exception, never a partial map — a tag map that quietly dropped the row it
    /// could not parse would produce an "unknown tag" error a long way from the actual mistake.
    /// </summary>
    public static S7TagMap FromJson(string json)
    {
        MapShape? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<MapShape>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new S7ConfigurationException($"malformed tag map: {ex.Message}", ex);
        }

        var rows = parsed?.Tags;
        if (rows is null || rows.Count == 0)
            throw new S7ConfigurationException(
                "the tag map contains no tags. An empty map is refused rather than accepted, because " +
                "every subsequent read would fail with a confusing 'unknown tag' instead of this.");

        return new S7TagMap(rows.Select((r, i) =>
        {
            if (!Enum.TryParse<S7DataType>(r.Type, ignoreCase: true, out var type))
                throw new S7ConfigurationException(
                    $"tag map row {i} ('{r.Name}') has type '{r.Type}', which is not one of: " +
                    string.Join(", ", Enum.GetNames<S7DataType>()) + ".");

            return new S7Tag(r.Name ?? string.Empty, r.Area ?? string.Empty, r.Db, r.Byte, type, r.Bit);
        }));
    }

    public static S7TagMap Load(string path) => FromJson(File.ReadAllText(path));
}
