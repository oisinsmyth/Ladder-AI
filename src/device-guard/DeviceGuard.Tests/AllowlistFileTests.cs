namespace DeviceGuard.Tests;

/// <summary>Loading is where a fail-open bug would hide — a parse error must never read as "empty but fine".</summary>
public class AllowlistFileTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "device-guard-test-" + Guid.NewGuid() + ".json");
        File.WriteAllText(path, content);
        _temp.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var p in _temp)
            if (File.Exists(p)) File.Delete(p);
    }

    [Fact]
    public void Null_path_is_no_allowlist_configured()
    {
        var r = AllowlistFile.Load(null);
        Assert.False(r.Loaded);
        Assert.Equal(GuardReason.NoAllowlistConfigured, r.FailureReason);
    }

    [Fact]
    public void Missing_file_is_file_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), "device-guard-absent-" + Guid.NewGuid() + ".json");
        var r = AllowlistFile.Load(path);
        Assert.False(r.Loaded);
        Assert.Equal(GuardReason.AllowlistFileMissing, r.FailureReason);
    }

    [Fact]
    public void Malformed_json_is_unreadable_not_empty()
    {
        var path = WriteTemp("{ this is not json ");
        var r = AllowlistFile.Load(path);

        Assert.False(r.Loaded);
        Assert.Equal(GuardReason.AllowlistUnreadable, r.FailureReason);
    }

    [Fact]
    public void Valid_file_loads_entries()
    {
        var path = WriteTemp(
            """
            {
              "entries": [
                { "address": "192.0.2.10", "label": "Bench HMI", "kind": "test-rig" },
                { "address": "192.0.2.11", "label": "Line PLC", "kind": "production" }
              ]
            }
            """);

        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded);
        Assert.Equal(2, r.Entries.Count);
        Assert.Equal("192.0.2.10", r.Entries[0].Address);
        Assert.True(r.Entries[0].IsTestRig);
        Assert.False(r.Entries[1].IsTestRig);
    }

    [Fact]
    public void Empty_entries_array_loads_but_authorizes_nothing()
    {
        var path = WriteTemp("""{ "entries": [] }""");
        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded);
        Assert.Empty(r.Entries);

        var d = new DeviceAccessGuard(r).Check("192.0.2.10");
        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.AllowlistEmpty, d.Reason);
    }

    [Fact]
    public void Json_with_comments_and_trailing_commas_is_tolerated()
    {
        var path = WriteTemp(
            """
            {
              // approved bench devices
              "entries": [
                { "address": "192.0.2.10", "label": "Bench HMI", "kind": "test-rig" },
              ]
            }
            """);

        var r = AllowlistFile.Load(path);
        Assert.True(r.Loaded);
        Assert.Single(r.Entries);
    }

    /// <summary>
    /// The marker is a NESTED object, which is the one shape in this file that could bind to null
    /// without any parse error at all. A silently-unbound marker would leave the entry looking
    /// configured while the serial went unread — and the refusal would then blame the device.
    /// </summary>
    [Fact]
    public void A_nested_marker_block_binds()
    {
        var path = WriteTemp(
            """
            {
              "entries": [
                {
                  "address": "10.10.10.10",
                  "kind": "test-rig",
                  "serialNumber": "RIG-BENCH-01",
                  "marker": { "dbNumber": 100, "byteOffset": 2, "length": 32, "encoding": "S7String" }
                }
              ]
            }
            """);

        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded);
        var marker = Assert.Single(r.Entries).Marker;
        Assert.NotNull(marker);
        Assert.Equal(100, marker!.DbNumber);
        Assert.Equal(2, marker.ByteOffset);
        Assert.Equal(32, marker.Length);
        Assert.Equal(MarkerLocation.S7String, marker.EffectiveEncoding);
        Assert.True(marker.IsUsable);
    }

    [Fact]
    public void An_entry_with_no_marker_block_leaves_it_null_rather_than_empty()
    {
        // Null means "no marker available", which is a refusal for any entry declaring a serial. An
        // empty-but-present marker would instead be reported as a malformed marker — a different fix.
        var path = WriteTemp("""{ "entries": [ { "address": "10.10.10.10", "kind": "test-rig" } ] }""");

        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded);
        Assert.Null(Assert.Single(r.Entries).Marker);
    }

    [Fact]
    public void A_half_filled_marker_binds_and_is_reported_as_malformed()
    {
        // Binding must not "helpfully" default the missing fields — a reader pointed at the wrong
        // bytes could produce a plausible identifier out of unrelated data and match the allowlist.
        var path = WriteTemp(
            """
            {
              "entries": [
                { "address": "10.10.10.10", "kind": "test-rig", "marker": { "dbNumber": 100 } }
              ]
            }
            """);

        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded);
        var marker = Assert.Single(r.Entries).Marker;
        Assert.NotNull(marker);
        Assert.False(marker!.IsUsable);
        Assert.Contains("no byteOffset", marker.Problem());
    }
}
