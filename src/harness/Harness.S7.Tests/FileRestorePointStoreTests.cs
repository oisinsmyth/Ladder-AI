using DeviceGuard;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// A restore-point store's failure mode is believing it covers more than it does, so most of these
/// tests are about the store saying NO — to a file that was never verified, to one that covers a
/// different DB, to one that has been edited, and to a caller that needs a kind of restore point this
/// store cannot produce at all.
/// </summary>
public class FileRestorePointStoreTests : IDisposable
{
    private const string Target = "192.0.2.11";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ladder-rp-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>A device made of byte arrays, so capture and restore can be observed exactly.</summary>
    private sealed class MemoryRegions : IRegionAccess
    {
        private readonly Dictionary<int, byte[]> _blocks = new();

        /// <summary>Model a region the program rewrites every scan, so a restore cannot stick.</summary>
        public int? OverwrittenDb { get; set; }

        public MemoryRegions With(int db, params byte[] bytes)
        {
            _blocks[db] = bytes;
            return this;
        }

        public byte[] Block(int db) => _blocks[db];

        public byte[] Read(RestoreRegion region)
        {
            var slice = new byte[region.Size];
            Array.Copy(_blocks[region.DbNumber], region.StartByte, slice, 0, region.Size);
            return slice;
        }

        public void Write(RestoreRegion region, byte[] data)
        {
            if (OverwrittenDb == region.DbNumber) return;   // the program won; nothing landed
            Array.Copy(data, 0, _blocks[region.DbNumber], region.StartByte, data.Length);
        }
    }

    private static readonly RestoreRegion[] InterfaceRegion =
    {
        new("DB_Interface", 10, 0, 8),
    };

    private FileRestorePointStore Store(
        string[]? requiredAreas = null,
        RestorePointScope scope = RestorePointScope.ProcessDataOnly,
        TimeSpan? maxAge = null,
        Func<DateTimeOffset>? clock = null) =>
        new(_dir, requiredAreas ?? new[] { "DB_Interface" }, scope, maxAge, clock);

    private static MemoryRegions Device() =>
        new MemoryRegions().With(10, 1, 2, 3, 4, 5, 6, 7, 8);

    // ---------------------------------------------------------------- the happy path

    [Fact]
    public void A_captured_restore_point_is_verified_and_satisfies_the_fence()
    {
        var store = Store();
        var manifest = store.Capture(Target, InterfaceRegion, "a test", Device());

        Assert.True(manifest.Verified);
        Assert.True(store.HasVerifiedRestorePoint(Target));
        Assert.Null(store.LastRefusal);
    }

    [Fact]
    public void The_manifest_is_readable_by_a_person()
    {
        var store = Store();
        store.Capture(Target, InterfaceRegion, "an engineer", Device());

        var text = File.ReadAllText(store.PathFor(Target));

        Assert.Contains("192.0.2.11", text);
        Assert.Contains("an engineer", text);
        Assert.Contains("ProcessDataOnly", text);
    }

    [Fact]
    public void It_closes_the_write_fences_seventh_gate_end_to_end()
    {
        // The point of the whole class: DeviceWriteGuard refuses every write until something can
        // answer this question with a yes, and until now nothing could.
        var store = Store();
        store.Capture(Target, InterfaceRegion, "a test", Device());

        var entry = new AllowlistEntry(
            Address: Target, Kind: "test-rig", WriteEligible: true, OutputsIsolated: true,
            IsolationAssertedBy: "an engineer", OrderNumber: "6ES7 214-1AG40-0XB0");

        var guard = new DeviceWriteGuard(AllowlistFile.Result.Ok(new[] { entry }, "(test)"), store);

        var decision = guard.Check(Target, "DB_Interface",
            new DeviceIdentity(OrderNumber: "6ES7 214-1AG40-0XB0"),
            WriteScope.For("a run", "DB_Interface"));

        Assert.True(decision.Allowed);
    }

    // ---------------------------------------------------------------- saying no

    [Fact]
    public void No_file_means_no()
    {
        var store = Store();

        Assert.False(store.HasVerifiedRestorePoint(Target));
        Assert.Contains("no restore point file", store.LastRefusal!);
    }

    [Fact]
    public void An_unverified_manifest_means_no()
    {
        var store = Store();
        store.Capture(Target, InterfaceRegion, "a test", Device());

        var path = store.PathFor(Target);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Verified\": true", "\"Verified\": false"));

        Assert.False(store.HasVerifiedRestorePoint(Target));
        Assert.Contains("never verified", store.LastRefusal!);
    }

    [Fact]
    public void An_edited_manifest_means_no()
    {
        var store = Store();
        store.Capture(Target, InterfaceRegion, "a test", Device());

        var path = store.PathFor(Target);
        var text = File.ReadAllText(path);

        var marker = "\"Base64\": \"";
        var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var payload = text[start..text.IndexOf('"', start)];

        File.WriteAllText(path,
            text.Replace(payload, Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 })));

        Assert.False(store.HasVerifiedRestorePoint(Target));
        Assert.Contains("does not match its recorded hash", store.LastRefusal!);
    }

    [Fact]
    public void A_restore_point_that_does_not_cover_what_the_run_will_write_means_no()
    {
        // "A restore point exists" is exactly the question that gets answered yes too easily.
        var capturing = Store(new[] { "DB_Interface" });
        capturing.Capture(Target, InterfaceRegion, "a test", Device());

        var asking = Store(new[] { "DB_Interface", "DB_Recipe" });

        Assert.False(asking.HasVerifiedRestorePoint(Target));
        Assert.Contains("DB_Recipe", asking.LastRefusal!);
    }

    [Fact]
    public void A_manifest_captured_from_another_device_means_no()
    {
        var store = Store();
        store.Capture(Target, InterfaceRegion, "a test", Device());

        // Copy the file to look like another device's, the way a reused backup would.
        File.Copy(store.PathFor(Target), store.PathFor("192.0.2.99"));

        Assert.False(store.HasVerifiedRestorePoint("192.0.2.99"));
        Assert.Contains("was captured from", store.LastRefusal!);
    }

    [Fact]
    public void A_stale_restore_point_means_no()
    {
        var now = new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);
        var store = Store(maxAge: TimeSpan.FromHours(8), clock: () => now);
        store.Capture(Target, InterfaceRegion, "a test", Device());

        Assert.True(store.HasVerifiedRestorePoint(Target));

        var later = Store(maxAge: TimeSpan.FromHours(8), clock: () => now.AddHours(9));
        Assert.False(later.HasVerifiedRestorePoint(Target));
        Assert.Contains("past the", later.LastRefusal!);
    }

    [Fact]
    public void A_run_that_needs_a_program_and_data_restore_point_is_refused_permanently()
    {
        // 10-non-goals.md #4(b): a download can silently reinitialise DB actual values and retentive
        // data. This store captures DB bytes, so it can never answer that question, and the refusal
        // is structural rather than a note somebody has to remember to read.
        var dataOnly = Store();
        dataOnly.Capture(Target, InterfaceRegion, "a test", Device());

        var needsMore = Store(scope: RestorePointScope.ProgramAndData);

        Assert.False(needsMore.HasVerifiedRestorePoint(Target));
        Assert.Contains("captures DB byte ranges only", needsMore.LastRefusal!);
    }

    // ---------------------------------------------------------------- capture-time refusals

    [Fact]
    public void Capturing_regions_that_miss_a_declared_area_is_refused()
    {
        var store = Store(new[] { "DB_Interface", "DB_Recipe" });

        var ex = Assert.Throws<S7ConfigurationException>(() =>
            store.Capture(Target, InterfaceRegion, "a test", Device()));

        Assert.Contains("worse than having none", ex.Message);
    }

    [Fact]
    public void An_unattributed_capture_is_refused()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() =>
            Store().Capture(Target, InterfaceRegion, "  ", Device()));

        Assert.Contains("unattributed record is not a record", ex.Message);
    }

    [Fact]
    public void Capturing_nothing_is_refused()
    {
        Assert.Throws<S7ConfigurationException>(() =>
            Store().Capture(Target, Array.Empty<RestoreRegion>(), "a test", Device()));
    }

    // ---------------------------------------------------------------- restore

    [Fact]
    public void Restore_puts_the_bytes_back_and_confirms_by_re_reading()
    {
        var device = Device();
        var store = Store();
        store.Capture(Target, InterfaceRegion, "a test", device);

        device.Block(10)[0] = 0xFF;
        device.Block(10)[7] = 0xFF;

        var result = store.Restore(Target, device);

        Assert.True(result.Restored);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, device.Block(10));
        Assert.Contains("confirmed every one by re-reading", result.Message);
    }

    [Fact]
    public void A_restore_that_does_not_stick_is_reported_as_unconfirmed()
    {
        // #4(b): success is confirmed by re-reading, never assumed. A region the program rewrites
        // every scan was never restorable, and reporting it as restored would be the lie that matters.
        var device = Device();
        var store = Store();
        store.Capture(Target, InterfaceRegion, "a test", device);

        device.OverwrittenDb = 10;
        device.Block(10)[0] = 0xFF;

        var result = store.Restore(Target, device);

        Assert.False(result.Restored);
        Assert.Contains("re-read does not match", Assert.Single(result.Problems));
    }

    [Fact]
    public void Restoring_from_nothing_fails_rather_than_reporting_success()
    {
        Assert.False(Store().Restore(Target, Device()).Restored);
    }

    // ---------------------------------------------------------------- keying

    [Fact]
    public void Two_targets_never_share_a_file()
    {
        var store = Store();

        Assert.NotEqual(store.PathFor("10.0.0.1"), store.PathFor("10.0.0.2"));
        Assert.NotEqual(store.PathFor("10.0.0.1"), store.PathFor("10:0:0:1"));
    }

    [Fact]
    public void The_key_is_the_one_the_guard_will_ask_with()
    {
        // The guard normalizes the target before asking. If the store were keyed on the raw address,
        // a capture under " 192.0.2.11 " would be invisible to a fence asking about "192.0.2.11".
        var store = Store();
        store.Capture(DeviceAddress.Normalize(" 192.0.2.11 "), InterfaceRegion, "a test", Device());

        Assert.True(store.HasVerifiedRestorePoint(DeviceAddress.Normalize("192.0.2.11")));
    }
}
