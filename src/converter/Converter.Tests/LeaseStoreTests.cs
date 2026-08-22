using System;
using System.IO;
using System.Linq;
using Ladder.Converter.Leases;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// 🔴 <b>The lock that a 3,365-line hand-edited text file has been standing in for.</b>
///
/// <para>That file's README says what it is: <i>"a cooperative convention… not a real lock — it only
/// works if every agent actually follows it."</i> It has been raced, and the collision was <i>"caught by
/// chance when re-reading git log."</i></para>
///
/// <para><b>These tests are about the two decisions that make it a lock rather than a note:</b> who wins
/// when two acquire at once, and when — if ever — a lease may be taken away from its holder.</para>
/// </summary>
public sealed class LeaseStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lease-tests-" + Guid.NewGuid().ToString("N"));

    private static readonly DateTime T0 = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    private const int LivePid = 4242;
    private const int DeadPid = 9999;
    private static readonly DateTime LiveStart = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime DeadStart = new(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>Only <see cref="LivePid"/> is running, and it started at <see cref="LiveStart"/>.</summary>
    private LeaseStore Store(DateTime now, Func<int, DateTime?>? processes = null) =>
        new(_root, () => now, processes ?? (pid => pid == LivePid ? LiveStart : null));

    /// <summary>
    /// The world BEFORE the holder died — <see cref="DeadPid"/> is running here.
    ///
    /// <para><b>A holder must be alive to take a lease</b>, so a dead-holder scenario has to be staged
    /// in two steps rather than declared: acquire while the process exists, then move to a world where
    /// it does not. Refusing to record a holder that is already gone is the store's behaviour, not an
    /// obstacle to work around — it is what stops a lease being taken out in a dead process's name.</para>
    /// </summary>
    private LeaseStore StoreBeforeItDied(DateTime now) =>
        new(_root, () => now, pid => pid == LivePid ? LiveStart : pid == DeadPid ? DeadStart : null);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_first_acquirer_gets_it_and_the_second_is_REFUSED_and_told_who_has_it()
    {
        var store = Store(T0);

        var first = store.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-a", LivePid, TimeSpan.FromMinutes(10), "deploy");
        Assert.Equal(LeaseResult.Acquired, first.Result);

        var second = store.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-b", LivePid, TimeSpan.FromMinutes(10), "deploy");

        Assert.Equal(LeaseResult.HeldByAnother, second.Result);

        // *** NAMING THE HOLDER IS THE POINT, NOT A NICETY. *** The recorded race on the text file went
        // unnoticed because two entries were indistinguishable; a refusal that cannot say who won leaves
        // the loser in exactly that position.
        Assert.Equal("agent-a", second.Holder!.Holder);
        Assert.Contains("agent-a", second.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE CASE THAT MAKES IT A LEASE RATHER THAN A CLAIM.</b> A claim is held until released and
    /// stale ones are never auto-released — correct for a block number, fatal for a gate, because one
    /// crashed agent would wedge the rig forever.
    /// </summary>
    [Fact]
    public void A_DEAD_holder_past_its_TTL_is_reclaimed_and_the_reclaim_is_reported_as_such()
    {
        StoreBeforeItDied(T0).TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-a", DeadPid, TimeSpan.FromMinutes(5), "wave");

        // agent-a's process is gone (only LivePid is running) and T0+10min is past its 5-minute TTL.
        var later = Store(T0.AddMinutes(10));
        var taken = later.TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-b", LivePid, TimeSpan.FromMinutes(5), "wave");

        Assert.Equal(LeaseResult.Reclaimed, taken.Result);
        Assert.Equal("agent-b", later.Read(LeaseResource.Rig, "10.10.10.10")!.Holder);

        // Distinct from a plain Acquired: somebody's run died holding the gate, and smoothing that into
        // an ordinary success hides the only evidence it happened.
        Assert.Contains("SOMEBODY'S RUN DIED HOLDING THE GATE", taken.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>The converse, and it is the dangerous one.</b> Evicting a live holder puts two writers on
    /// one resource — the exact failure this exists to prevent. A long download is not a dead one.
    /// </summary>
    [Fact]
    public void A_LIVE_holder_past_its_TTL_is_reported_and_NEVER_evicted()
    {
        Store(T0).TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-a", LivePid, TimeSpan.FromMinutes(1), "long compile");

        var later = Store(T0.AddHours(3));
        var refused = later.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-b", LivePid, TimeSpan.FromMinutes(5), "deploy");

        Assert.Equal(LeaseResult.HeldByAnother, refused.Result);
        Assert.Equal("agent-a", later.Read(LeaseResource.Portal, "proj.ap20")!.Holder);
        Assert.Contains("STILL ALIVE", refused.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>PIDs ARE REUSED, AND A BARE PID CHECK WOULD EVENTUALLY HAND TWO AGENTS ONE GATE.</b>
    /// A pid that is alive but whose process started at a different instant is a DIFFERENT process that
    /// merely inherited the number — the original holder is gone.
    /// </summary>
    [Fact]
    public void A_REUSED_pid_is_not_mistaken_for_the_original_holder()
    {
        Store(T0).TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-a", LivePid, TimeSpan.FromMinutes(5), "wave");

        // Same pid is alive, but it started AFTER the lease was taken: an unrelated process.
        var reused = new DateTime(2026, 8, 22, 11, 30, 0, DateTimeKind.Utc);
        var later = new LeaseStore(_root, () => T0.AddMinutes(10), pid => pid == LivePid ? reused : null);

        var taken = later.TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-b", LivePid, TimeSpan.FromMinutes(5), "wave");

        Assert.Equal(LeaseResult.Reclaimed, taken.Result);

        // The control: with the ORIGINAL start time the same pid is the same holder, and is not reclaimed.
        Dispose();
        Store(T0).TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-a", LivePid, TimeSpan.FromMinutes(5), "wave");
        var stillHeld = Store(T0.AddMinutes(10))
            .TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-b", LivePid, TimeSpan.FromMinutes(5), "wave");

        Assert.Equal(LeaseResult.HeldByAnother, stillHeld.Result);
    }

    /// <summary>
    /// A holder that has died but whose lease has NOT expired is left alone. The TTL is the declared
    /// window for noticing a dead run, and cutting it short would make the declaration meaningless.
    /// </summary>
    [Fact]
    public void A_dead_holder_INSIDE_its_TTL_is_not_reclaimed_yet_and_the_report_says_when_it_can_be()
    {
        StoreBeforeItDied(T0).TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-a", DeadPid, TimeSpan.FromMinutes(30), "deploy");

        var soon = Store(T0.AddMinutes(5));
        var refused = soon.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-b", LivePid, TimeSpan.FromMinutes(5), "deploy");

        Assert.Equal(LeaseResult.HeldByAnother, refused.Result);
        Assert.Contains("Reclaimable from", refused.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_holder_may_release_it()
    {
        var store = Store(T0);
        store.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-a", LivePid, TimeSpan.FromMinutes(10), "deploy");

        var wrong = store.Release(LeaseResource.Portal, "proj.ap20", "agent-b");
        Assert.Equal(LeaseResult.HeldByAnother, wrong.Result);
        Assert.NotNull(store.Read(LeaseResource.Portal, "proj.ap20"));

        var right = store.Release(LeaseResource.Portal, "proj.ap20", "agent-a");
        Assert.True(right.Held);
        Assert.Null(store.Read(LeaseResource.Portal, "proj.ap20"));
    }

    [Fact]
    public void Portal_and_rig_are_different_locks_and_do_not_shadow_each_other()
    {
        var store = Store(T0);

        Assert.Equal(LeaseResult.Acquired, store.TryAcquire(LeaseResource.Portal, "x", "agent-a", LivePid, TimeSpan.FromMinutes(5), "").Result);
        Assert.Equal(LeaseResult.Acquired, store.TryAcquire(LeaseResource.Rig, "x", "agent-a", LivePid, TimeSpan.FromMinutes(5), "").Result);

        Assert.Equal(2, store.All().Length);
    }

    [Fact]
    public void A_generic_or_absent_holder_is_refused_because_that_is_how_the_recorded_race_happened()
    {
        var store = Store(T0);

        var outcome = store.TryAcquire(LeaseResource.Portal, "proj.ap20", "   ", LivePid, TimeSpan.FromMinutes(5), "");

        Assert.Equal(LeaseResult.Invalid, outcome.Result);
        Assert.Contains("indistinguishable", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unnamed_resource_a_blank_target_and_a_non_positive_TTL_are_all_refused()
    {
        var store = Store(T0);

        Assert.Equal(LeaseResult.Invalid, store.TryAcquire(LeaseResource.Unstated, "x", "a", LivePid, TimeSpan.FromMinutes(5), "").Result);
        Assert.Equal(LeaseResult.Invalid, store.TryAcquire(LeaseResource.Portal, " ", "a", LivePid, TimeSpan.FromMinutes(5), "").Result);
        Assert.Equal(LeaseResult.Invalid, store.TryAcquire(LeaseResource.Portal, "x", "a", LivePid, TimeSpan.Zero, "").Result);
    }

    [Fact]
    public void A_lease_round_trips_through_the_file_exactly()
    {
        var store = Store(T0);
        store.TryAcquire(LeaseResource.Rig, "10.10.10.10", "agent-a", LivePid, TimeSpan.FromMinutes(7), "the wave");

        var read = store.Read(LeaseResource.Rig, "10.10.10.10")!;

        Assert.Equal("agent-a", read.Holder);
        Assert.Equal(LivePid, read.ProcessId);
        Assert.Equal(LiveStart, read.ProcessStartUtc);
        Assert.Equal(T0.AddMinutes(7), read.ExpiresUtc);
        Assert.Equal("the wave", read.Purpose);
    }

    /// <summary>
    /// A slot that exists and will not parse is refused, never overwritten — the same rule the claims
    /// store applies. Overwriting a lock this tool cannot identify is how it stops being a lock.
    /// </summary>
    [Fact]
    public void An_unreadable_slot_is_refused_rather_than_clobbered()
    {
        var store = Store(T0);
        store.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-a", LivePid, TimeSpan.FromMinutes(5), "");

        var file = Directory.EnumerateFiles(_root, "*.lease").Single();
        File.WriteAllText(file, "this is not a lease");

        var outcome = store.TryAcquire(LeaseResource.Portal, "proj.ap20", "agent-b", LivePid, TimeSpan.FromMinutes(5), "");

        Assert.Equal(LeaseResult.Invalid, outcome.Result);
        Assert.Contains("could not be read", outcome.Detail, StringComparison.Ordinal);
        Assert.Equal("this is not a lease", File.ReadAllText(file));
    }
}
