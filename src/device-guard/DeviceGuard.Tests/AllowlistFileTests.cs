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

    // ---- null elements: the crash class, found by fuzzing the fence on 2026-08-14 ---------------

    /// <summary>
    /// *** THE ONE THAT CRASHED. *** `{"entries": [null]}` parses cleanly into a list holding a
    /// null, so Load used to return Loaded == true and DeviceAccessGuard.Check then dereferenced it:
    /// NullReferenceException, process exit -1073741819, and the word REFUSED nowhere in the output.
    ///
    /// <para>It DID fail closed - the stack ended at the guard and no socket was opened - and that
    /// property is asserted separately in DeviceAccessGuardTests so this fix cannot quietly trade it
    /// away. What was wrong is that a harness cannot tell an unhandled exception from a refusal: a
    /// crash is loud without being NAMED.</para>
    /// </summary>
    [Fact]
    public void Null_entry_is_unreadable_not_a_crash()
    {
        var path = WriteTemp("{\"entries\": [null]}");
        var r = AllowlistFile.Load(path);

        Assert.False(r.Loaded);
        Assert.Equal(GuardReason.AllowlistUnreadable, r.FailureReason);
        Assert.Contains("entries[0]", r.Message);
    }

    /// <summary>
    /// Refused, never silently filtered. Dropping the null would repair a file somebody wrote wrong
    /// and hand the next reader a shorter list than they authored - and the real rig entry sitting
    /// beside it would make the result look entirely healthy.
    /// </summary>
    [Fact]
    public void Null_entry_beside_a_real_one_still_refuses_the_whole_document()
    {
        var path = WriteTemp("{\"entries\": [{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\"}, null]}");
        var r = AllowlistFile.Load(path);

        Assert.False(r.Loaded);
        Assert.Empty(r.Entries);
        Assert.Contains("entries[1]", r.Message);
    }

    /// <summary>Every null is named, not just the first - a reader fixing one at a time is a reader
    /// running this three times.</summary>
    [Fact]
    public void Every_null_entry_is_named()
    {
        var path = WriteTemp("{\"entries\": [null, {\"address\":\"192.0.2.99\",\"kind\":\"test-rig\"}, null]}");
        var r = AllowlistFile.Load(path);

        Assert.False(r.Loaded);
        Assert.Contains("entries[0]", r.Message);
        Assert.Contains("entries[2]", r.Message);
    }

    /// <summary>
    /// *** THE CONTROL. *** Without it, "Loaded == false" above is also what a Load that rejects
    /// everything looks like - and a fence that refuses every document is as broken as one that
    /// refuses none, it just fails more quietly.
    /// </summary>
    [Fact]
    public void A_document_with_no_nulls_still_loads()
    {
        var path = WriteTemp("{\"entries\": [{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\"}]}");
        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded, r.Message);
        Assert.Single(r.Entries);
    }

    /// <summary>
    /// The rest of the null family, swept rather than met one at a time. A null INSIDE an entry is
    /// not the same defect - every AllowlistEntry member is nullable by declaration, so these are
    /// well-formed documents and must LOAD. They are pinned so that a future "reject nulls" tidy
    /// cannot widen the rule above into refusing ordinary entries.
    /// </summary>
    [Theory]
    [InlineData("{\"entries\": [{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\",\"label\":null}]}")]
    [InlineData("{\"entries\": [{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\",\"marker\":null}]}")]
    [InlineData("{\"entries\": [{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\",\"orderNumber\":null}]}")]
    [InlineData("{\"entries\": [{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\",\"writableAreas\":null}]}")]
    public void A_null_MEMBER_is_not_a_null_ENTRY_and_still_loads(string json)
    {
        var r = AllowlistFile.Load(WriteTemp(json));

        Assert.True(r.Loaded, r.Message);
        Assert.Single(r.Entries);
    }

    // ---- duplicate keys: CURRENT behaviour, pinned, deliberately not changed --------------------

    /// <summary>
    /// A DUPLICATED JSON KEY SILENTLY TAKES THE LAST VALUE, so an entry that READS as `plant` is
    /// honoured as a test rig. Measured 2026-08-14 through the shipped binary: the guard returned
    /// ALLOWED for an entry whose first `kind` was "plant".
    ///
    /// <para>Not an attack path - this file is owner-authored, and anyone who can add the second key
    /// can simply write `test-rig` once. It is a REVIEWABILITY defect: a human reading the entry top
    /// to bottom sees the wrong answer, and the eye stops at the first `kind`.</para>
    ///
    /// <para>Pinned rather than fixed, on instruction: System.Text.Json's duplicate handling is the
    /// behaviour of the parser, not of this fence, and quietly starting to REJECT duplicates would
    /// change what an existing allowlist file means. If this test ever fails because the document is
    /// now refused, that is a decision somebody made - not a regression - and the comment in the
    /// allowlist format docs must move with it.</para>
    /// </summary>
    [Fact]
    public void A_duplicate_key_takes_the_LAST_value_which_can_upgrade_a_plant_entry()
    {
        var path = WriteTemp("{\"entries\":[{\"address\":\"192.0.2.99\",\"kind\":\"plant\",\"kind\":\"test-rig\"}]}");
        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded, r.Message);
        var entry = Assert.Single(r.Entries);

        Assert.True(
            entry.IsTestRig,
            "CURRENT behaviour: the LAST duplicate key wins, so this entry is a test rig despite " +
            "reading as 'plant'. If this now fails, duplicates are being rejected or the first key " +
            "now wins - either is a change to what an existing allowlist file MEANS.");
    }

    /// <summary>The converse, so the pin above cannot be satisfied by a parser that ignores `kind`
    /// altogether: last-wins in the SAFE direction demotes a rig to plant.</summary>
    [Fact]
    public void A_duplicate_key_in_the_other_order_demotes_the_entry()
    {
        var path = WriteTemp("{\"entries\":[{\"address\":\"192.0.2.99\",\"kind\":\"test-rig\",\"kind\":\"plant\"}]}");
        var r = AllowlistFile.Load(path);

        Assert.True(r.Loaded, r.Message);
        Assert.False(Assert.Single(r.Entries).IsTestRig);
    }
}
