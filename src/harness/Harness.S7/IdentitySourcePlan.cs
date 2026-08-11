using DeviceGuard;

namespace Harness.S7;

/// <summary>
/// Turns one allowlist entry into the set of identity sources that will be read from the device.
///
/// <para><b>Why this exists as a type rather than a line of wiring.</b> Declaring an expected
/// identifier and being able to READ one are different things, and nothing previously checked that
/// they lined up. An entry could declare a serial number that no configured source was capable of
/// producing; the mismatch then surfaced at connect time as <i>"entry declares a serial number but the
/// device did not report one"</i> — which is true, fail-closed, and blames the DEVICE for what is
/// actually a CONFIGURATION gap. That is the difference between a rig that is wrong and a rig that was
/// never asked properly, and the two need different fixes.</para>
///
/// <para><b>This changes no verdict.</b> An unsatisfiable entry was refused before and is refused now.
/// The only thing that improves is the DIAGNOSIS — which matters here because the tempting "fix" for a
/// refusal like that is to delete the serial from the entry until it passes, and that would leave the
/// fence verifying a MODEL rather than a DEVICE. On this project's network the same address reaches
/// different physical controllers depending on which tunnel is up, so a model-only check is close to
/// no check at all. The plan names the real problem so nobody reaches for that shortcut.</para>
/// </summary>
public sealed record IdentitySourcePlan(
    IReadOnlyList<IDeviceIdentitySource> Sources,
    string? Problem)
{
    /// <summary>True when every identifier the entry declares has a source able to read it.</summary>
    public bool IsUsable => Problem is null;

    /// <summary>
    /// Build the plan for an entry.
    ///
    /// <para>The order code source is ALWAYS included: it is the one identifier proven readable on this
    /// CPU family over classic S7comm, it costs one request, and <see cref="DeviceIdentityReader"/>
    /// refuses to run with no sources at all.</para>
    /// </summary>
    public static IdentitySourcePlan ForEntry(AllowlistEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var sources = new List<IDeviceIdentitySource> { new OrderCodeIdentitySource() };

        // A marker that is present but malformed is a REFUSAL, never a silent drop. Dropping it would
        // leave the serial unverified on an entry that looks fully configured — the worst of both.
        if (entry.Marker is { } marker)
        {
            var markerProblem = marker.Problem();
            if (markerProblem is not null)
            {
                return Unusable(sources,
                    $"the marker location for '{entry.DisplayLabel}' cannot be used: {markerProblem}");
            }

            sources.Add(new MarkerDbIdentitySource(
                marker.DbNumber!.Value,
                marker.ByteOffset!.Value,
                marker.Length!.Value,
                string.Equals(marker.EffectiveEncoding, MarkerLocation.FixedChars, StringComparison.OrdinalIgnoreCase)
                    ? MarkerEncoding.FixedChars
                    : MarkerEncoding.S7String));
        }

        if (entry.UseCpuInfoSerial)
            sources.Add(new CpuInfoIdentitySource());

        var canReadSerial = entry.Marker is not null || entry.UseCpuInfoSerial;

        // ---- Every DECLARED identifier must have a source that can produce it. ----

        if (!string.IsNullOrWhiteSpace(entry.SerialNumber) && !canReadSerial)
        {
            return Unusable(sources,
                $"'{entry.DisplayLabel}' declares a serial number ('{entry.SerialNumber!.Trim()}') but no " +
                "configured source can read one, so the check could never pass. An S7-1200 over classic " +
                "S7comm reports its ORDER CODE and, on the CPU measured here, refuses SZL 0x001C. Give the " +
                "entry a 'marker' block naming the DB the program publishes its identifier in, or set " +
                "'useCpuInfoSerial' to try the CPU's own record. DO NOT remove the serial number to make " +
                "this pass — the order code is identical across every unit of the model, so an entry " +
                "without a serial verifies a model and not a device.");
        }

        if (!string.IsNullOrWhiteSpace(entry.MacAddress))
        {
            return Unusable(sources,
                $"'{entry.DisplayLabel}' declares a MAC ('{entry.MacAddress!.Trim()}') but no identity " +
                "source reads one. A device's MAC is not visible across a routed tunnel at all — only the " +
                "gateway's is — so this cannot be satisfied from here. Remove it, or verify identity by " +
                "serial number instead.");
        }

        // The mirror-image gap: something readable that nothing will check. Not a safety hole by itself,
        // but it means the marker was configured under the impression it was being verified.
        if (entry.Marker is not null && string.IsNullOrWhiteSpace(entry.SerialNumber))
        {
            return Unusable(sources,
                $"'{entry.DisplayLabel}' configures a marker at {entry.Marker.Describe()} but declares no " +
                "serial number to compare it against, so the marker would be read on every connection and " +
                "never checked. Declare the expected marker value as the entry's serial number.");
        }

        return new IdentitySourcePlan(sources, null);
    }

    private static IdentitySourcePlan Unusable(List<IDeviceIdentitySource> sources, string problem) =>
        new(sources, problem);

    /// <summary>The configured source names, for messages and for a dry-run of what will be read.</summary>
    public string Describe() =>
        Sources.Count == 0 ? "<no sources>" : string.Join(", ", Sources.Select(s => s.Name));
}
