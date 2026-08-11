namespace DeviceGuard;

/// <summary>
/// What a device says it is, read from the device itself — not what we hoped was at that address.
///
/// WHY THIS TYPE EXISTS. An IP address does not identify a controller. On this project's network
/// 10.10.10.10 is the standard PLC address at multiple deployment sites, and which physical device
/// answers depends on which VPN tunnel is up — observed live on 2026-08-11, where a remote tunnel
/// injected 10.10.10.0/24 at metric 0 and silently shadowed a local segment carrying the same range.
/// Nothing in the routing table announces that swap, and it can happen mid-session on a reconnect.
///
/// So the guard authorizes on identity, and treats the address as a routing hint only. The identity
/// is read over the protocol (order number and serial are both readable from an S7 CPU) and compared
/// against the allowlist entry BEFORE any write. Re-read on every connection: a reconnect can land
/// somewhere else.
///
/// This type carries observations only. It performs no I/O — <see cref="DeviceWriteGuard"/> stays a
/// pure decision function, exactly as <see cref="DeviceAccessGuard"/> does.
/// </summary>
public sealed record DeviceIdentity(
    string? OrderNumber = null,
    string? SerialNumber = null,
    string? MacAddress = null)
{
    /// <summary>True when this observation carries at least one identifier worth comparing.</summary>
    public bool HasAnyIdentifier =>
        !string.IsNullOrWhiteSpace(OrderNumber)
        || !string.IsNullOrWhiteSpace(SerialNumber)
        || !string.IsNullOrWhiteSpace(MacAddress);

    /// <summary>A short human description for messages.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(OrderNumber)) parts.Add($"order '{OrderNumber!.Trim()}'");
        if (!string.IsNullOrWhiteSpace(SerialNumber)) parts.Add($"serial '{SerialNumber!.Trim()}'");
        if (!string.IsNullOrWhiteSpace(MacAddress)) parts.Add($"MAC '{Normalize(MacAddress)}'");
        return parts.Count == 0 ? "<no identifiers>" : string.Join(", ", parts);
    }

    /// <summary>
    /// Compare an observation against what an entry declares.
    ///
    /// Fail-closed in both directions that matter:
    ///   - an entry that declares NO identifiers cannot be satisfied (you cannot verify against
    ///     nothing, so such an entry is not write-usable);
    ///   - an observation missing a field the entry declares is a MISMATCH, not a pass. "I could not
    ///     read the serial" must never be treated as "the serial matched".
    /// Every declared field must be present in the observation and equal.
    /// </summary>
    public static IdentityComparison Compare(AllowlistEntry entry, DeviceIdentity? observed)
    {
        var declared = new List<(string Field, string Expected)>();
        if (!string.IsNullOrWhiteSpace(entry.OrderNumber)) declared.Add(("order number", entry.OrderNumber!));
        if (!string.IsNullOrWhiteSpace(entry.SerialNumber)) declared.Add(("serial number", entry.SerialNumber!));
        if (!string.IsNullOrWhiteSpace(entry.MacAddress)) declared.Add(("MAC", entry.MacAddress!));

        if (declared.Count == 0)
            return IdentityComparison.NotDeclared();

        if (observed is null || !observed.HasAnyIdentifier)
            return IdentityComparison.NotObserved();

        foreach (var (field, expected) in declared)
        {
            var actual = field switch
            {
                "order number" => observed.OrderNumber,
                "serial number" => observed.SerialNumber,
                _ => observed.MacAddress,
            };

            if (string.IsNullOrWhiteSpace(actual))
                return IdentityComparison.Mismatch(
                    $"entry declares a {field} ('{expected.Trim()}') but the device did not report one — " +
                    "an unread identifier is a mismatch, never a pass");

            var normalizedExpected = field == "MAC" ? Normalize(expected) : expected.Trim();
            var normalizedActual = field == "MAC" ? Normalize(actual) : actual.Trim();

            if (!string.Equals(normalizedExpected, normalizedActual, StringComparison.OrdinalIgnoreCase))
                return IdentityComparison.Mismatch(
                    $"{field} mismatch — allowlist expects '{normalizedExpected}', device reports '{normalizedActual}'");
        }

        return IdentityComparison.Match();
    }

    /// <summary>Strip MAC separators and case so 00-1B-1B-2C and 001b:1b2c compare equal.</summary>
    private static string Normalize(string? mac) =>
        new string((mac ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

/// <summary>Outcome of comparing a declared identity against an observed one.</summary>
public sealed record IdentityComparison(bool Matched, string? Problem, bool WasDeclared, bool WasObserved)
{
    public static IdentityComparison Match() => new(true, null, true, true);

    public static IdentityComparison NotDeclared() => new(false,
        "the allowlist entry declares no order number, serial number or MAC, so there is nothing to " +
        "verify the device against; an entry with no identity is not write-usable", false, false);

    public static IdentityComparison NotObserved() => new(false,
        "no device identity was read before the write; identity must be re-read on every connection, " +
        "because a reconnect can land on a different device at the same address", true, false);

    public static IdentityComparison Mismatch(string problem) => new(false, problem, true, true);
}
