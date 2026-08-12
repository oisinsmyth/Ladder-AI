using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DownloadProbe;

/// <summary>
/// WHAT, IF ANYTHING, COULD PUT THE CPU BACK IN RUN — reported, never used.
///
/// The device item advertises <c>OnlineProvider</c> among its services (seen in the
/// provider-acquisition trail that <c>download-plan</c> prints), and the obvious question after a
/// download that stops the CPU is whether that provider can read or set the operating mode. This
/// answers it by REFLECTION over the installed assembly, at run time, and prints what it finds.
///
/// *** IT IS A SURVEY AND NOTHING MORE. *** No object is constructed, no service is acquired, no
/// member is invoked, and no mode is changed. Wiring a mode change is a separate decision that has
/// not been taken, and a test asserts over the compiled IL that this binary calls nothing declared in
/// <c>Siemens.Engineering.Online</c> at all — so "reports but does not act" is checked, not promised.
///
/// Run time rather than a transcribed constant on purpose: a fact written down here would be a claim
/// about an assembly that might not be the one installed. This reads the one that is.
///
/// *** WHAT IT ALREADY ESTABLISHED, BY REFLECTION OVER THE INSTALLED V20 ASSEMBLY (2026-08-12).
/// THE ANSWER IS NO. *** <c>OnlineProvider</c> exposes no CPU operating mode and nothing that could
/// change one:
///   - its three properties are <c>Configuration</c>, <c>Parent</c> and <c>State</c>, all read-only;
///   - <c>State</c> is an <c>OnlineState</c>, and <c>OnlineState</c> is a CONNECTION state, not a CPU
///     mode: Offline / Connecting / Online / Incompatible / NotReachable / Protected / Disconnecting.
///     The name reads like a run state and is not one, which is exactly why it is worth writing down;
///   - its methods are <c>GoOnline()</c>, <c>GoOffline()</c>, <c>SetPlcMasterSecret</c> and
///     <c>ResetPlcMasterSecret</c>. Going online is a CONNECTION, not a start;
///   - a sweep of every type in the assembly for a member naming an operating mode, run state, warm
///     or cold restart or memory reset found only OFFLINE HARDWARE CONFIGURATION —
///     <c>HW.OperatingMode</c> is a module CHANNEL mode, <c>HW.StartupActionAfterPowerOn</c> is a
///     configured parameter, <c>WebserverUserPermissions.ChangeOperatingMode</c> is a permission FLAG
///     that performs nothing.
/// So the download-configuration callback is not merely this tool's best route back to RUN — as far
/// as the public API goes it is the ONLY expression of a CPU stop or start there is. One caveat, held
/// open honestly: <c>OnlineProvider</c> inherits the name-based <c>SetAttribute(string, object)</c>,
/// whose accepted names are decided at run time by <c>GetAttributeInfos()</c> and are not
/// statically discoverable. That is unruled-out, not a lead — and it is not tried here.
/// </summary>
internal static class OnlineModeSurvey
{
    internal const string OnlineNamespace = "Siemens.Engineering.Online";

    internal const string ProviderTypeName = OnlineNamespace + ".OnlineProvider";

    /// <summary>
    /// Member names whose presence would answer "is there a route back to RUN": anything naming an
    /// operating mode, a run state, or a start/stop. Every match is flagged in the dump so a reader
    /// does not have to spot it in a member list, and a match is a LEAD, not a route — a settable
    /// property here still would not have been wired up.
    /// </summary>
    internal static readonly IReadOnlyList<string> ModeRelevantWords = new[]
    {
        "OperatingMode", "RunMode", "RunState", "OnlineState", "Mode", "Run", "Stop", "Start",
    };

    /// <summary>
    /// The conclusion, printed under the live dump so the dump is read as evidence for it rather than
    /// as a list to interpret. Kept next to the reflection that produced it.
    /// </summary>
    internal static readonly IReadOnlyList<string> EstablishedFinding = new[]
    {
        "ESTABLISHED BY THIS SURVEY: *** OnlineProvider OFFERS NO CPU OPERATING MODE. ***",
        "  Its only state property is OnlineState, which is a CONNECTION state — Offline / Connecting /",
        "  Online / Incompatible / NotReachable / Protected / Disconnecting. It READS LIKE a run state",
        "  and is not one. GoOnline() connects; it does not start a CPU.",
        "  A sweep of every type in the assembly for an operating-mode / run-state / restart member",
        "  found only OFFLINE hardware configuration (HW.OperatingMode is a module CHANNEL mode;",
        "  StartupActionAfterPowerOn is a configured parameter; ChangeOperatingMode is a permission",
        "  flag that performs nothing).",
        "  *** SO THE DOWNLOAD-CONFIGURATION CALLBACK IS THE ONLY PLACE IN THE PUBLIC API WHERE A CPU",
        "      STOP OR START IS EXPRESSIBLE AT ALL — i.e. StartModules is not the best route back to",
        "      RUN, it is the only one. ***",
        "  UNRULED-OUT, stated as such: OnlineProvider inherits the name-based SetAttribute(string,",
        "  object), whose accepted names are decided at run time and are not visible to reflection.",
        "  Not tried here. An unruled-out possibility is not a route.",
    };

    internal static IReadOnlyList<string> Describe(Assembly assembly)
    {
        var lines = new List<string>
        {
            "This is a REFLECTION SURVEY of the installed Siemens.Engineering assembly. Nothing here is",
            "constructed, acquired or invoked, and no operating mode is read or written by this tool.",
            string.Empty,
        };

        var types = SafeTypes(assembly)
            .Where(t => string.Equals(SafeNamespace(t), OnlineNamespace, StringComparison.Ordinal))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        if (types.Count == 0)
        {
            lines.Add($"*** NO TYPES FOUND IN '{OnlineNamespace}'. ***");
            lines.Add("Either the namespace has moved or the assembly could not be walked. Empty is not clean:");
            lines.Add("read this as 'the survey failed', not as 'there is no online API'.");
            return lines;
        }

        lines.Add($"types in {OnlineNamespace} ({types.Count}):");
        foreach (var type in types)
        {
            lines.Add($"  - {type.Name}{(type.IsEnum ? "  [enum]" : string.Empty)}");
        }

        lines.Add(string.Empty);
        var provider = types.FirstOrDefault(t => string.Equals(t.FullName, ProviderTypeName, StringComparison.Ordinal));
        if (provider is null)
        {
            lines.Add($"*** {ProviderTypeName} NOT FOUND in this assembly. ***");
            return lines;
        }

        lines.Add($"{provider.FullName} — every public member, with the mode-relevant ones flagged:");
        lines.Add($"  base type       : {provider.BaseType?.FullName ?? "(none)"}");
        lines.Add($"  interfaces      : {Render(SafeInterfaces(provider).Select(i => i.Name))}");
        lines.Add($"  public ctors    : {provider.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length}" +
                  "   (0 means it can only be obtained from the object model, never built)");

        foreach (var property in provider.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            lines.Add(
                $"  property        : {property.Name} : {SafeTypeName(property.PropertyType)}" +
                $" [{(property.CanRead ? "get" : string.Empty)}{(property.CanWrite ? "/SET" : string.Empty)}]" +
                Flag(property.Name, SafeTypeName(property.PropertyType)));
        }

        foreach (var method in provider.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(m => !m.IsSpecialName)
                     .OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{SafeTypeName(p.ParameterType)} {p.Name}"));
            lines.Add(
                $"  method          : {SafeTypeName(method.ReturnType)} {method.Name}({parameters})" +
                Flag(method.Name, SafeTypeName(method.ReturnType)));
        }

        foreach (var type in types.Where(t => t.IsEnum))
        {
            lines.Add($"  enum {type.Name} : {Render(SafeEnumNames(type))}");
        }

        lines.Add(string.Empty);
        lines.Add("Read the flagged lines as LEADS, not as a route. Even a settable operating-mode member here");
        lines.Add("is not wired up: this tool changes no mode, and that is asserted over its compiled IL.");
        lines.Add(string.Empty);
        lines.AddRange(EstablishedFinding);
        return lines;
    }

    private static string Flag(params string[] candidates) =>
        candidates.Any(c => ModeRelevantWords.Any(w => c.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
            ? "   <== MODE-RELEVANT NAME"
            : string.Empty;

    private static string Render(IEnumerable<string> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }

    /// <summary>
    /// Every walk here is individually guarded. Some types in this assembly reference
    /// <c>Siemens.Engineering.Contract</c>, which is not always beside the binary, and merely reading
    /// their <c>Namespace</c> throws — the same hazard the test suite's reflection walk documents.
    /// A survey that threw would take down a run whose real product is the download log.
    /// </summary>
    private static IReadOnlyList<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }

    private static string? SafeNamespace(Type type)
    {
        try
        {
            return type.Namespace;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    private static IReadOnlyList<Type> SafeInterfaces(Type type)
    {
        try
        {
            return type.GetInterfaces();
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }

    private static IReadOnlyList<string> SafeEnumNames(Type type)
    {
        try
        {
            return Enum.GetNames(type);
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    private static string SafeTypeName(Type type)
    {
        try
        {
            return type.Name;
        }
        catch (Exception)
        {
            return "(unreadable)";
        }
    }
}
