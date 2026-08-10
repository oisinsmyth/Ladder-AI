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
}
