using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harness.CmdInject;

/// <summary>
/// A declared band — a first register and a count — <b>as the binding claims it, before the map is
/// consulted.</b> The resolver later checks this against where the tags actually land.
/// </summary>
/// <param name="FirstRegister">First holding register the band declares.</param>
/// <param name="RegisterCount">How many registers the band declares.</param>
public sealed record BandDeclaration(int FirstRegister, int RegisterCount)
{
    /// <summary>Last register the band declares.</summary>
    public int LastRegister => FirstRegister + RegisterCount - 1;

    /// <summary>True when <paramref name="register"/> falls inside the declared band.</summary>
    public bool Contains(int register) => register >= FirstRegister && register <= LastRegister;

    public override string ToString() => $"[{FirstRegister}..{LastRegister}] ({RegisterCount} register(s))";
}

/// <summary>One channel's role → tag-name assignments, as declared. <b>Tag names are job data, held only as strings.</b></summary>
/// <param name="Name">The channel's own name (also job data — never used by protocol logic, only for messages).</param>
/// <param name="Roles">Which map tag plays each role, keyed by role. Missing optional roles are simply absent.</param>
public sealed record ChannelDeclaration(string Name, IReadOnlyDictionary<InjectionRole, string> Roles);

/// <summary>
/// The binding — <b>the whole of what a job tells this tool about its map.</b>
///
/// <para>It is loaded from a file in the job folder and NEVER compiled in. It names, in job vocabulary,
/// which tag plays each role; the tool holds the roles and nothing else. A fixture stands in for it with
/// invented names (<c>UNIT_A</c>, <c>CH1_Seq</c>) and exercises exactly the same code.</para>
/// </summary>
public sealed record InjectionBinding(
    BandDeclaration CommandBand,
    IReadOnlyList<BandDeclaration> ObservationBands,
    IReadOnlyDictionary<InjectionRole, string> BandRoles,
    IReadOnlyList<ChannelDeclaration> Channels)
{
    /// <summary>Read the binding off disk. Any I/O or parse failure is a refusal, never a throw.</summary>
    public static BindingLoad Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return BindingLoad.Refused(
                $"the binding file '{path}' could not be read: {ex.Message}. There is no built-in map to fall " +
                "back to — the protocol lives in this binary and the map lives in the job folder, on purpose.");
        }

        return Parse(json, path);
    }

    /// <summary>The whole parse, over text, so every structural refusal is testable with no files.</summary>
    public static BindingLoad Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);

        BindingFile? file;
        try
        {
            file = JsonSerializer.Deserialize<BindingFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return BindingLoad.Refused($"the binding '{source}' is not valid JSON: {ex.Message}.");
        }

        if (file is null)
            return BindingLoad.Refused($"the binding '{source}' parsed to nothing.");

        var refusals = new List<string>();

        // ---- the two bands ----
        if (file.CommandBand is null)
            refusals.Add("the binding declares no commandBand. A command band is where every client write must land.");

        if (file.ObservationBands is null || file.ObservationBands.Count == 0)
            refusals.Add("the binding declares no observationBands. Ack registers must resolve inside one, and a command with " +
                         "no observable acknowledgement is a write into the dark.");

        var commandBand = file.CommandBand?.ToDeclaration();
        var observationBands = (file.ObservationBands ?? new List<BandFile>())
            .Select(b => b.ToDeclaration()).ToList();

        foreach (var band in observationBands.Append(commandBand).Where(b => b is not null).Select(b => b!))
        {
            if (band.RegisterCount < 1)
                refusals.Add($"a band declares {band.RegisterCount} register(s) — a band with no registers holds nothing.");
            if (band.FirstRegister < 0)
                refusals.Add($"a band declares first register {band.FirstRegister}, which is not a register address.");
        }

        // ---- the band-level roles ----
        var bandRoles = new Dictionary<InjectionRole, string>();
        AddIfPresent(bandRoles, InjectionRole.Heartbeat, file.Heartbeat);
        AddIfPresent(bandRoles, InjectionRole.Enable, file.Enable);
        AddIfPresent(bandRoles, InjectionRole.SafetyPermissive, file.SafetyPermissive);

        foreach (var role in Roles.RequiredBandRoles.Where(r => !bandRoles.ContainsKey(r)))
        {
            refusals.Add($"the binding names no tag for the required band role '{role}'. Without it the protocol has no " +
                         $"register to play {role}, and there is no default — a guessed register is exactly the leak this design removes.");
        }

        // ---- the channels ----
        var channels = new List<ChannelDeclaration>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var channel in file.Channels ?? new List<ChannelFile>())
        {
            var name = channel.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                refusals.Add("a channel declares no name. A nameless channel cannot be cited or reported.");
                continue;
            }

            if (!seenNames.Add(name))
            {
                refusals.Add($"channel '{name}' is declared more than once.");
                continue;
            }

            var roles = channel.RoleMap();

            foreach (var role in Roles.RequiredChannelCommandRoles.Where(r => !roles.ContainsKey(r)))
                refusals.Add($"channel '{name}' names no tag for the required command role '{role}'.");

            foreach (var role in Roles.RequiredObservationRoles.Where(r => !roles.ContainsKey(r)))
                refusals.Add($"channel '{name}' names no tag for the required observation role '{role}'.");

            channels.Add(new ChannelDeclaration(name, roles));
        }

        if (channels.Count == 0 && refusals.Count == 0)
        {
            // EMPTY IS NOT CLEAN. A binding with no channels resolves to a tool that can command nothing, which
            // is not a working tool pointed at an empty plant — it is a binding somebody wrote wrong.
            refusals.Add("the binding declares no channels. A binding that commands nothing is not a binding.");
        }

        if (refusals.Count > 0)
            return BindingLoad.Refused(refusals);

        return BindingLoad.Loaded(new InjectionBinding(commandBand!, observationBands, bandRoles, channels));
    }

    private static void AddIfPresent(IDictionary<InjectionRole, string> map, InjectionRole role, string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
            map[role] = tag.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // ---- the on-disk shape. Kept private: nothing outside this file speaks JSON. ----

    private sealed record BandFile(int FirstRegister, int RegisterCount)
    {
        public BandDeclaration ToDeclaration() => new(FirstRegister, RegisterCount);
    }

    private sealed record ChannelFile(
        string? Name,
        string? Seq,
        string? Code,
        string? Int1,
        string? Int2,
        string? Real1,
        string? Real2,
        string? AckSeq,
        string? AckCode,
        string? AckResult,
        string? AckCount)
    {
        public IReadOnlyDictionary<InjectionRole, string> RoleMap()
        {
            var map = new Dictionary<InjectionRole, string>();
            AddIfPresent(map, InjectionRole.Seq, Seq);
            AddIfPresent(map, InjectionRole.Code, Code);
            AddIfPresent(map, InjectionRole.Int1, Int1);
            AddIfPresent(map, InjectionRole.Int2, Int2);
            AddIfPresent(map, InjectionRole.Real1, Real1);
            AddIfPresent(map, InjectionRole.Real2, Real2);
            AddIfPresent(map, InjectionRole.AckSeq, AckSeq);
            AddIfPresent(map, InjectionRole.AckCode, AckCode);
            AddIfPresent(map, InjectionRole.AckResult, AckResult);
            AddIfPresent(map, InjectionRole.AckCount, AckCount);
            return map;
        }
    }

    private sealed record BindingFile(
        BandFile? CommandBand,
        List<BandFile>? ObservationBands,
        string? Heartbeat,
        string? Enable,
        string? SafetyPermissive,
        List<ChannelFile>? Channels);
}

/// <summary>The outcome of loading a binding — <b>Ok, or a list of reasons, never a half-built binding.</b></summary>
public sealed record BindingLoad(bool Ok, InjectionBinding? Binding, IReadOnlyList<string> Refusals)
{
    public static BindingLoad Loaded(InjectionBinding binding) => new(true, binding, Array.Empty<string>());

    public static BindingLoad Refused(params string[] refusals) => new(false, null, refusals);

    public static BindingLoad Refused(IReadOnlyList<string> refusals) => new(false, null, refusals);
}
