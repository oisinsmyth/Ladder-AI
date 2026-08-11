using System.Globalization;
using System.Text.Json;
using DeviceGuard;
using Harness.S7;

namespace Harness.RigWrite;

/// <summary>
/// The command line for a governed device write. In this build it plans one and stops.
///
/// <para><b>The client factory is a parameter that is never called.</b> <see cref="Run"/> takes a
/// <c>Func&lt;IS7Client&gt;</c> and no code path invokes it — which is a property a test can assert
/// (hand it a factory that throws and watch the dry run finish), rather than a claim in prose. The
/// composition root passes one that throws, so even a future caller that reached for it would get
/// <see cref="Arming.WhyNot"/> rather than a socket.</para>
/// </summary>
public static class RigWriteCli
{
    /// <summary>Nothing decidable from files refuses this write. Deferred steps remain open.</summary>
    public const int ExitOfflineClear = 0;

    /// <summary>At least one gate or precondition refuses, decided here without a device.</summary>
    public const int ExitWouldRefuse = 1;

    public const int ExitUsage = 2;

    /// <summary>Somebody asked this build to actually write. It cannot. See <see cref="Arming"/>.</summary>
    public const int ExitNotArmed = 3;

    /// <summary>Flags that mean "do it for real". Named so the refusal can be specific.</summary>
    private static readonly string[] ArmingFlags =
        { "--arm", "--armed", "--execute", "--write", "--go", "--yes", "--force", "--for-real" };

    public const string Usage =
        """
        rig-write — plan ONE governed write to an allowlisted test rig (ADR-0009), and stop.

        Usage:
          rig-write plan --target <address> [--allowlist <path>] [--restore-dir <dir>]
                         [--db <n>] [--offset <n>] [--length <n>] [--area <name>]
                         [--text <value>] [--purpose <text>] [--json]
          rig-write layout
          rig-write help

        THIS BUILD CANNOT WRITE TO A DEVICE. `plan` opens no socket, sends no packet and
        resolves no host: it decides every gate that can be decided from files, names the
        ones only the device can answer, and stops. There is no flag that arms it - the
        capability is absent from the assembly, not switched off.

        Defaults describe the rig marker DB (DB38 DB_RigMarker), so a bare
          rig-write plan --target <address> --allowlist <path>
        plans the proposed first governed write: the reserved SerialNumber member.

        The allowlist path comes from --allowlist, else LADDER_DEVICE_ALLOWLIST. With
        neither set there is NO allowlist and every target is refused.

        Exit codes:
          0  nothing decidable offline refuses this write (deferred steps remain open)
          1  a gate or precondition refuses
          2  usage error
          3  arming was requested, and this build has no such capability
        """;

    public static int Run(
        string[] args,
        Func<string, string?> envLookup,
        Func<IS7Client> clientFactory,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        var @out = stdout ?? Console.Out;
        var err = stderr ?? Console.Error;

        if (args.Length == 0)
        {
            err.WriteLine(Usage);
            return ExitUsage;
        }

        // Checked before anything else, and before the verb, so that `rig-write plan --arm` and
        // `rig-write --arm` answer identically. A refusal that depends on argument order is a refusal
        // somebody will get round by reordering.
        var armingFlag = args.FirstOrDefault(a => ArmingFlags.Contains(a, StringComparer.OrdinalIgnoreCase));
        if (armingFlag is not null)
        {
            err.WriteLine($"'{armingFlag}' is not an option here, and its absence is deliberate.");
            err.WriteLine(Arming.WhyNot);
            return ExitNotArmed;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        switch (command)
        {
            case "-h" or "--help" or "help":
                @out.WriteLine(Usage);
                return ExitOfflineClear;

            case "layout":
                @out.WriteLine(MarkerDbLayout.Describe());
                @out.WriteLine(MarkerDbLayout.IsSelfConsistent
                    ? $"SELF-CONSISTENT: the four members occupy exactly {MarkerDbLayout.TotalBytes} bytes, " +
                      "the size TIA reports. That corroborates the member SIZES; it says nothing about " +
                      "their ORDER. Reading the whole block and checking for a String header at " +
                      $"{MarkerDbLayout.RigMarkerOffset}, {MarkerDbLayout.OrderNumberOffset} and " +
                      $"{MarkerDbLayout.SerialNumberOffset} settles that, and is step 1."
                    : "INCONSISTENT: the computed offsets do not sum to the reported block size. One of " +
                      "the two statements about this block is wrong; do not address it until that is " +
                      "resolved.");
                return MarkerDbLayout.IsSelfConsistent ? ExitOfflineClear : ExitWouldRefuse;

            case "plan":
                return RunPlan(rest, envLookup, @out, err);

            default:
                err.WriteLine($"Unknown command '{command}'.");
                err.WriteLine(Usage);
                return ExitUsage;
        }
    }

    private static int RunPlan(string[] rest, Func<string, string?> envLookup, TextWriter @out, TextWriter err)
    {
        var json = rest.Contains("--json");
        var target = GetOpt(rest, "--target");

        if (string.IsNullOrWhiteSpace(target))
        {
            err.WriteLine("plan requires --target <address>.");
            err.WriteLine(Usage);
            return ExitUsage;
        }

        if (!TryGetInt(rest, "--db", MarkerDbLayout.DbNumber, err, out var db) ||
            !TryGetInt(rest, "--offset", MarkerDbLayout.SerialNumberOffset, err, out var offset) ||
            !TryGetInt(rest, "--length", MarkerDbLayout.StringDeclaredMax, err, out var length))
        {
            return ExitUsage;
        }

        var area = GetOpt(rest, "--area") ?? MarkerDbLayout.AreaName;
        var text = GetOpt(rest, "--text") ?? "RIGWRITE-PROBE";

        var restoreDir = GetOpt(rest, "--restore-dir") ?? DefaultRestoreDir(envLookup);
        if (string.IsNullOrWhiteSpace(restoreDir))
        {
            err.WriteLine("no restore-point directory: pass --restore-dir <dir> (USERPROFILE is not set).");
            return ExitUsage;
        }

        RigWriteRequest request;
        try
        {
            request = RigWriteRequest.MarkerSerialProbe(target!, text, db, offset, length, area);
        }
        catch (S7ConfigurationException ex)
        {
            // A payload that cannot be encoded is a fault in the request, not a refusal by a gate.
            err.WriteLine($"the write cannot be encoded: {ex.Message}");
            return ExitUsage;
        }

        var purpose = GetOpt(rest, "--purpose") ?? request.Purpose;
        request = request with { Purpose = purpose };

        var allowlistPath = AllowlistPath.Resolve(GetOpt(rest, "--allowlist"), envLookup);
        var allowlist = AllowlistFile.Load(allowlistPath);

        // The store is built with the areas THIS run declares, so its coverage question is asked
        // against what will actually be touched rather than against a hand-maintained list. Process
        // data only: a DB byte-range capture cannot undo a download, and asking it to would be refused
        // rather than approximated.
        var store = new FileRestorePointStore(
            restoreDir!, new[] { area }, RestorePointScope.ProcessDataOnly);

        var plan = RigWritePlanner.Plan(request, allowlist, store, observedIdentity: null, inspectableStore: store);

        if (json) @out.WriteLine(ToJson(plan, allowlistPath, restoreDir!));
        else @out.WriteLine(plan.Render());

        return plan.OfflineClear ? ExitOfflineClear : ExitWouldRefuse;
    }

    private static string ToJson(RigWritePlan plan, string? allowlistPath, string restoreDir) =>
        JsonSerializer.Serialize(new
        {
            dryRun = true,
            deviceContacted = false,
            armingCompiledIn = Arming.CompiledIn,
            target = plan.Request.Target,
            area = plan.Request.Area,
            write = new
            {
                db = plan.Request.DbNumber,
                offset = plan.Request.ByteOffset,
                bytes = plan.Request.Size,
                payloadHex = Convert.ToHexString(plan.Request.Payload),
                what = plan.Request.What,
            },
            allowlistPath,
            restoreDir,
            identitySources = plan.IdentityPlan?.Describe(),
            identitySourceProblem = plan.IdentityPlan?.Problem,
            identityAssumed = plan.IdentityAssumed,
            fence = new
            {
                allowed = plan.Decision?.Allowed,
                gate = plan.BlockingGate?.ToString(),
                message = plan.Decision?.Message,
            },
            offlineClear = plan.OfflineClear,
            blockers = plan.Blockers,
            steps = plan.Steps.Select(s => new { s.Number, s.Name, status = s.Status.ToString(), s.Detail }),
        }, PrettyJson);

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static string? DefaultRestoreDir(Func<string, string?> envLookup)
    {
        var home = envLookup("USERPROFILE");
        return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, ".ladder", "restore-points");
    }

    private static string? GetOpt(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }

    /// <summary>
    /// A numeric option, refused rather than defaulted when it is present and unparseable. Silently
    /// falling back to the default would address a different DB than the one asked for.
    /// </summary>
    private static bool TryGetInt(string[] args, string name, int fallback, TextWriter err, out int value)
    {
        var raw = GetOpt(args, name);
        if (raw is null)
        {
            value = fallback;
            return true;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;

        err.WriteLine($"{name} '{raw}' is not a whole number.");
        return false;
    }
}
