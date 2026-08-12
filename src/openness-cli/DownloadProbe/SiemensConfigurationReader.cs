using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Siemens.Engineering.Download.Configurations;

namespace DownloadProbe;

/// <summary>
/// Turns a live Siemens <c>DownloadConfiguration</c> into a <see cref="RaisedConfigurationView"/>.
/// The only file that touches the configuration types themselves.
///
/// IT IS REFLECTIVE ON PURPOSE, and that is not laziness. Reflecting the installed V20
/// <c>Siemens.Engineering.dll</c> shows there is no common API for "the selections":
/// <c>DownloadConfiguration</c> declares only <c>Message</c> and <c>Parent</c>, and each of the ~25
/// selection configurations declares its OWN <c>CurrentSelection</c> property over its OWN enum
/// type (<c>DataBlockReinitialization.CurrentSelection</c> is a
/// <c>DataBlockReinitializationSelections</c>, <c>StopModules.CurrentSelection</c> is a
/// <c>StopModulesSelections</c>, and so on). A typed handler would therefore be a switch over the
/// types someone thought to list — and the single most valuable outcome of this experiment is a
/// configuration NOBODY LISTED. Reflection handles the unknown one identically to the known one.
///
/// Nothing here writes. <see cref="EnumSelection.ApplyAndReadBack"/> is the only setter in this
/// file, it is only reachable from <see cref="ConfigurationRecorder"/>, and the value it sets comes
/// only from <see cref="NoActionFirstPolicy"/>.
/// </summary>
internal static class SiemensConfigurationReader
{
    internal static RaisedConfigurationView Read(DownloadConfiguration configuration)
    {
        var type = configuration.GetType();

        string? message = null;
        string? messageReadError = null;
        try
        {
            message = configuration.Message;
        }
        catch (Exception ex)
        {
            messageReadError = $"{ex.GetType().Name}: {ex.Message}";
        }

        var selectionProperty = FindSelectionProperty(type);
        IConfigurationSelection? selection = selectionProperty is null
            ? null
            : new EnumSelection(configuration, selectionProperty);

        return new RaisedConfigurationView(
            runtimeTypeName: type.FullName ?? type.Name,
            typeChain: TypeChain(type),
            message: message,
            messageReadError: messageReadError,
            familyDescription: DescribeFamily(configuration, selectionProperty is not null),
            selection: selection,
            properties: ReadOtherProperties(configuration, type, selectionProperty),
            attributeInfos: ReadAttributeInfos(configuration));
    }

    /// <summary>
    /// Openness' self-description of this live object's attributes. A READ, and the diagnostic that
    /// can explain a refused set: <c>CanWrite</c> on the CLR property is compile-time metadata and is
    /// true even when this reports <c>Read</c>.
    /// </summary>
    private static IReadOnlyList<ConfigurationAttributeInfoView> ReadAttributeInfos(DownloadConfiguration configuration)
    {
        try
        {
            return configuration.GetAttributeInfos()
                .Select(info => new ConfigurationAttributeInfoView(
                    Describe(() => info.Name),
                    Describe(() => info.AccessMode.ToString()),
                    Describe(() => info.CreateRelevance.ToString())))
                .ToList();
        }
        catch (Exception ex)
        {
            return new[]
            {
                new ConfigurationAttributeInfoView(
                    "<<GetAttributeInfos() FAILED>>", ExceptionReport.Summarise(ex), string.Empty),
            };
        }
    }

    private static string Describe(Func<string?> read)
    {
        try
        {
            return read() ?? "(null)";
        }
        catch (Exception ex)
        {
            return $"<<READ FAILED: {ExceptionReport.Summarise(ex)}>>";
        }
    }

    /// <summary>
    /// The <c>CurrentSelection</c> property, wherever in the hierarchy it is declared, provided it is
    /// an enum and settable. Matched by name because that is the only thing every selection
    /// configuration in the assembly has in common.
    /// </summary>
    private static PropertyInfo? FindSelectionProperty(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(
                "CurrentSelection",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (property is not null && property.PropertyType.IsEnum && property.CanRead && property.CanWrite)
            {
                return property;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> TypeChain(Type type)
    {
        var chain = new List<string>();
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            chain.Add(current.Name);
        }

        return chain;
    }

    /// <summary>
    /// Which of the three shapes this configuration has, and — for the two that are not selections —
    /// an explicit statement that nothing is written to it.
    /// </summary>
    private static string DescribeFamily(DownloadConfiguration configuration, bool hasSelection) => configuration switch
    {
        DownloadSelectionConfiguration when hasSelection =>
            "DownloadSelectionConfiguration — answered per the NoAction-first policy",
        DownloadSelectionConfiguration =>
            "DownloadSelectionConfiguration WITH NO CurrentSelection (e.g. DownloadCertificate) — nothing written",
        DownloadCheckConfiguration =>
            "DownloadCheckConfiguration (a Checked flag, not a selection) — READ ONLY, this tool never ticks it",
        DownloadPasswordConfiguration =>
            "DownloadPasswordConfiguration — READ ONLY, this tool never calls SetPassword",
        _ when hasSelection =>
            "UNRECOGNISED FAMILY, but it exposes a CurrentSelection — answered per the NoAction-first policy",
        _ =>
            "*** UNRECOGNISED CONFIGURATION FAMILY *** — logged in full, nothing written to it",
    };

    /// <summary>
    /// Every other readable public property, rendered. This is what "logged in full" means for a
    /// configuration this tool has never seen: the shape is unknown, so everything readable is
    /// reported rather than a chosen subset.
    /// </summary>
    private static IReadOnlyList<ConfigurationPropertyView> ReadOtherProperties(
        DownloadConfiguration configuration, Type type, PropertyInfo? selectionProperty)
    {
        var views = new List<ConfigurationPropertyView>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .Where(p => selectionProperty is null || p.Name != selectionProperty.Name)
                     // Message is already printed verbatim above; printing it twice would invite the
                     // reader to trust whichever copy came second.
                     .Where(p => p.Name != nameof(DownloadConfiguration.Message))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            string rendered;
            try
            {
                var value = property.GetValue(configuration);
                rendered = value switch
                {
                    null => "(null)",
                    string s => s,
                    _ => value.ToString() ?? $"({value.GetType().Name})",
                };
            }
            catch (Exception ex)
            {
                rendered = $"<<READ FAILED: {ex.GetType().Name}: {ex.Message}>>";
            }

            views.Add(new ConfigurationPropertyView(property.Name, property.PropertyType.Name, rendered));
        }

        return views;
    }

    /// <summary>
    /// A <c>CurrentSelection</c> enum property, read and written by name.
    /// <c>Enum.GetNames</c> is the list of names the enum DECLARES — Openness offers no way to ask
    /// which of them this instance would actually accept, which is a limitation to report, not one to
    /// paper over.
    ///
    /// THE SHAPE, MEASURED against the installed V20 assembly rather than assumed. On
    /// <c>StopModules</c>, <c>CurrentSelection</c> is declared on <c>StopModules</c> ITSELF (not on a
    /// generic base — <c>DownloadSelectionConfiguration</c> is non-generic and declares no such
    /// property), its type is the concrete enum <c>StopModulesSelections</c>, and the compiled setter
    /// body is exactly <c>m_InternalAccess.SetAttribute("CurrentSelection", (object)value)</c>. So
    /// parsing over <c>_property.PropertyType</c> and setting through that same property is already
    /// the correct binding, and a "wrong closed generic / boxed int / wrong enum type" mismatch is
    /// ruled out by the metadata. <see cref="SetByAttributeName"/> is the same operation the setter
    /// performs internally, reached without the reflection layer — a second route, not a second
    /// value.
    /// </summary>
    private sealed class EnumSelection : IConfigurationSelection
    {
        private readonly DownloadConfiguration _configuration;
        private readonly PropertyInfo _property;

        internal EnumSelection(DownloadConfiguration configuration, PropertyInfo property)
        {
            _configuration = configuration;
            _property = property;
            AvailableSelections = Enum.GetNames(property.PropertyType);
            DefaultValuedSelection = ZeroValuedName(property.PropertyType);
        }

        public string EnumTypeName => _property.PropertyType.FullName ?? _property.PropertyType.Name;

        public IReadOnlyList<string> AvailableSelections { get; }

        public string? DefaultValuedSelection { get; }

        public string ReadCurrentSelection() => _property.GetValue(_configuration)?.ToString() ?? "(null)";

        public SelectionApplyResult Apply(string selectionName)
        {
            var attempts = new List<SelectionApplyAttempt>();

            object value;
            try
            {
                // Enum.Parse over the NAME, never over a number: the same reason --options rejects
                // numeric strings. Parsed against the property's OWN closed enum type, so the value
                // handed to the setter is of exactly the declared type.
                value = Enum.Parse(_property.PropertyType, selectionName, ignoreCase: false);
            }
            catch (Exception ex)
            {
                attempts.Add(new SelectionApplyAttempt(
                    $"Enum.Parse({_property.PropertyType.Name}, \"{selectionName}\")", false, ex));
                return new SelectionApplyResult(selectionName, attempts, ReadCurrentOrNull(out var parseReadError), parseReadError);
            }

            var propertyRoute =
                $"property set — {_property.DeclaringType?.Name ?? "?"}.{_property.Name} = " +
                $"{_property.PropertyType.Name}.{selectionName} (PropertyInfo.SetValue)";
            try
            {
                _property.SetValue(_configuration, value);
                attempts.Add(new SelectionApplyAttempt(propertyRoute, true, null));
            }
            catch (Exception ex)
            {
                attempts.Add(new SelectionApplyAttempt(propertyRoute, false, ex));
            }

            // The second route is tried ONLY after the first failed, and it writes THE SAME value —
            // the one the policy chose, already parsed above. It exists because the two differ in
            // exactly one respect that matters here: PropertyInfo.SetValue wraps whatever the setter
            // throws in a TargetInvocationException, whereas this call surfaces the engineering
            // exception as thrown. If the refusal is real, this reports it in its own words; if the
            // refusal was an artefact of the reflection binding, this succeeds and says so.
            if (!attempts.Any(a => a.Succeeded))
            {
                var attributeRoute = $"SetAttribute(\"{_property.Name}\", {_property.PropertyType.Name}.{selectionName}) — the same call the property setter makes internally";
                try
                {
                    SetByAttributeName(value);
                    attempts.Add(new SelectionApplyAttempt(attributeRoute, true, null));
                }
                catch (Exception ex)
                {
                    attempts.Add(new SelectionApplyAttempt(attributeRoute, false, ex));
                }
            }

            var readBack = ReadCurrentOrNull(out var readError);
            return new SelectionApplyResult(selectionName, attempts, readBack, readError);
        }

        private void SetByAttributeName(object value) =>
            _configuration.SetAttribute(_property.Name, value);

        private string? ReadCurrentOrNull(out Exception? error)
        {
            try
            {
                error = null;
                return ReadCurrentSelection();
            }
            catch (Exception ex)
            {
                error = ex;
                return null;
            }
        }

        /// <summary>
        /// The member whose numeric value is zero, or null if none is. That is the value an unset
        /// field reads as, so a "current selection" equal to it proves nothing about whether anything
        /// ever chose it.
        /// </summary>
        private static string? ZeroValuedName(Type enumType)
        {
            try
            {
                var zero = Enum.ToObject(enumType, 0);
                return Enum.IsDefined(enumType, zero) ? Enum.GetName(enumType, zero) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
