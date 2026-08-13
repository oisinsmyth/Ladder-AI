using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `create-instance-db`'s sequencing, and the one property it exists to hold: <b>a run that does not
/// return a usable instance DB leaves the project unchanged.</b>
///
/// THE DEFECT THESE GUARD (measured on a live job, 2026-08-13). Auto-numbering handed back a DB
/// numbered 0; FI-63's repair set `db.Number`; the setter threw under automatic numbering; the
/// exception unwound through <c>finally { SaveProject(); }</c> — <b>and the finally committed the
/// broken block</b>. The command then reported a failure it had already written to disk. Three
/// delete-and-retry cycles did not clear it, because each retry recreated it. The exit code was not
/// the bug: the committed `DB0` was, so <b>every test here asserts on the project, not on the
/// return value</b> — the block list, and the number of times Save was called.
///
/// <see cref="Legacy"/> at the bottom is a transcription of the pre-fix sequence, kept and executed.
/// The same assertions run against it and it FAILS them, so these guards are demonstrated to detect
/// the defect rather than merely to pass alongside its absence.
/// </summary>
public partial class InstanceDbCreationTests
{
    // ---- the fixed sequence --------------------------------------------------------------------

    [Fact]
    public void ValidNumber_KeepsTheBlockAndSavesExactlyOnce()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 12 };

        var db = InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim");

        Assert.Equal("iDB_Sim", db.Name);
        Assert.Equal(12, db.Number);
        Assert.Equal(new[] { "iDB_Sim" }, site.Blocks.Select(b => b.Name));
        Assert.Equal(1, site.SaveCalls);
    }

    // The defect, from the project's side. 0 is the value FI-63 measured on the first creation after
    // a project open.
    [Fact]
    public void InvalidNumber_LeavesTheProjectExactlyAsItWas()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 0 };
        site.Blocks.Add(new FakeDb("iDB_Existing", 7));

        Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        // The two assertions that are the entire point. Nothing was saved, so nothing reached disk;
        // and the partial block is gone from the session, so the pre-existing project is intact.
        Assert.Equal(0, site.SaveCalls);
        Assert.Equal(new[] { "iDB_Existing" }, site.Blocks.Select(b => b.Name));
    }

    // The old code's finally saved on EVERY exit. A save that happens even once on a failure path is
    // the destructive half, so it gets its own named assertion rather than living inside the test
    // above.
    [Fact]
    public void InvalidNumber_NeverSaves()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 0 };

        Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        Assert.Equal(0, site.SaveCalls);
    }

    // The repair is retired, not hardened: nothing on the failure path may write to the project
    // beyond undoing what this call itself created. `set_Number` is the call that threw on the live
    // job, and the fixed sequence must not reach for it at all.
    [Fact]
    public void InvalidNumber_DoesNotAttemptARenumber()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 0 };

        Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        Assert.Equal(0, site.SetNumberCalls);
        Assert.Equal(1, site.DeleteCalls);
    }

    // FI-44, empty is not clean. Reading the number back is how the defect is detected, so a read
    // that fails is not permission to proceed — it is the same unanswered question as a bad number.
    [Fact]
    public void NumberThatCannotBeReadBack_IsTreatedAsAFailure()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 12, NumberReadThrows = true };

        var ex = Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        Assert.Null(ex.Number);
        Assert.Equal(0, site.SaveCalls);
        Assert.Empty(site.Blocks);
    }

    // The cleanup can itself fail, and the guarantee has to survive that. Nothing was saved either
    // way — the on-disk promise is unconditional — but the session is not clean, which the caller
    // must be told because the recovery differs.
    [Fact]
    public void RollbackThatFails_StillNeverSaves_AndSaysSo()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 0, DeleteThrows = true };

        var ex = Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        Assert.False(ex.RollbackCompleted);
        Assert.Equal(0, site.SaveCalls);
        Assert.Contains("NOT SAVED, BUT NOT CLEAN", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WITHOUT saving", ex.Message, StringComparison.Ordinal);
    }

    // A create that throws leaves nothing to roll back — and, critically, still no save. The old
    // finally would have saved here too.
    [Fact]
    public void CreateThatThrows_SavesNothingAndPropagates()
    {
        var site = new RecordingSite { CreateThrows = true };

        Assert.Throws<InvalidOperationException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        Assert.Equal(0, site.SaveCalls);
        Assert.Empty(site.Blocks);
    }

    // FI-63 part 1, the only half of the old fix that is kept: it is READ-ONLY, so it cannot hurt,
    // and it is still the best available hypothesis for why the first creation after a project open
    // misnumbers. Asserted as "read, and read before the create" — after the create it would be a
    // different operation entirely.
    [Fact]
    public void TheCompositionIsEnumeratedBeforeTheCreate()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 12 };

        InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim");

        Assert.Equal(1, site.ExistingNumbersReads);
        Assert.True(site.ExistingNumbersReadBeforeCreate);
    }

    [Fact]
    public void TheMessageNamesTheBlock_TheNumber_AndThatNothingWasSaved()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 0 };

        var ex = Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(site, "iDB_Sim", "FB_Sim"));

        Assert.Contains("iDB_Sim", ex.Message, StringComparison.Ordinal);
        Assert.Contains("invalid block number 0", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PROJECT UNCHANGED", ex.Message, StringComparison.Ordinal);
        // The reason this is refused rather than returned, kept from FI-63's own message: the default
        // gate cannot see it.
        Assert.Contains("whole-device compile reports Success", ex.Message, StringComparison.Ordinal);
    }

    // ---- the negative test ---------------------------------------------------------------------
    //
    // A guard that has only ever been run against the fixed code has not been tested. These run the
    // SAME assertions against `Legacy.Run`, a transcription of the pre-fix sequence, and assert that
    // it violates them — so the guards above are demonstrated to be sensitive to this specific
    // defect, permanently, rather than by an experiment someone once did by hand.

    [Fact]
    public void Negative_TheOldSequenceCommitsTheBrokenBlock()
    {
        var site = new RecordingSite { NumberFromAutoNumbering = 0, SetNumberThrows = true };
        site.Blocks.Add(new FakeDb("iDB_Existing", 7));

        Assert.Throws<InvalidOperationException>(() => Legacy.Run(site, "iDB_Sim", "FB_Sim"));

        // Exactly the reported defect, reproduced: the command failed, and it SAVED anyway...
        Assert.Equal(1, site.SaveCalls);
        // ...with the invalid-numbered block still in the project. This is the `DB0` that could
        // neither compile nor export and had to be deleted by hand.
        Assert.Contains(site.Blocks, b => b.Name == "iDB_Sim" && b.Number == 0);
    }

    // The guards, restated as a single statement about the two sequences: on the same input, one
    // saves and one does not. If a future edit reintroduces a save on the failure path, this fails.
    [Fact]
    public void Negative_TheFixedSequenceAndTheOldOneDisagreeAboutSaving()
    {
        var legacySite = new RecordingSite { NumberFromAutoNumbering = 0, SetNumberThrows = true };
        var fixedSite = new RecordingSite { NumberFromAutoNumbering = 0 };

        Assert.Throws<InvalidOperationException>(() => Legacy.Run(legacySite, "iDB_Sim", "FB_Sim"));
        Assert.Throws<InstanceDbCreationAbandonedException>(() => InstanceDbCreation.Run(fixedSite, "iDB_Sim", "FB_Sim"));

        Assert.Equal(1, legacySite.SaveCalls);
        Assert.Equal(0, fixedSite.SaveCalls);
        Assert.NotEmpty(legacySite.Blocks);
        Assert.Empty(fixedSite.Blocks);
    }

    // ---- exit codes ----------------------------------------------------------------------------

    [Fact]
    public void Abandoned_WithACleanSession_IsItsOwnExitCode()
    {
        var ex = new InstanceDbCreationAbandonedException("iDB_Sim", 0, rollbackFailure: null, cause: null);

        Assert.Equal(ExitCodes.ChangeAbandoned, ExitCodes.ForException(ex));
        Assert.NotEqual(ExitCodes.UnexpectedError, ExitCodes.ForException(ex));
    }

    [Fact]
    public void Abandoned_WithAFailedRollback_EarnsTheOtherCode()
    {
        var ex = new InstanceDbCreationAbandonedException(
            "iDB_Sim", 0, rollbackFailure: new InvalidOperationException("Delete() refused."), cause: null);

        Assert.Equal(ExitCodes.RollbackIncomplete, ExitCodes.ForException(ex));
    }

    // ---- through the CLI, against FakeGateway ---------------------------------------------------

    [Fact]
    public void Cli_Success_ReportsTheBlockAndSavesOnce()
    {
        var gateway = new FakeGateway { NumberFromAutoNumbering = 12 };

        var (exitCode, stdout, _) = Capture(() => Program.RunCreateInstanceDb(gateway, Options(), 60));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("DB12", stdout, StringComparison.Ordinal);
        Assert.Equal(1, gateway.Project.SaveCalls);
        Assert.Single(gateway.ProjectBlocks);
    }

    [Fact]
    public void Cli_InvalidNumber_LeavesTheProjectUnchangedAndExits17()
    {
        var gateway = new FakeGateway { NumberFromAutoNumbering = 0 };

        var ex = Assert.Throws<InstanceDbCreationAbandonedException>(
            () => Program.RunCreateInstanceDb(gateway, Options(), 60));

        // The project, first — this is the assertion the old code could not have passed.
        Assert.Empty(gateway.ProjectBlocks);
        Assert.Equal(0, gateway.Project.SaveCalls);
        // Then the code a shell script branches on.
        Assert.Equal(ExitCodes.ChangeAbandoned, ExitCodes.ForException(ex));
    }

    [Fact]
    public void Cli_FailedRollback_StillSavesNothingAndExits18()
    {
        var gateway = new FakeGateway { NumberFromAutoNumbering = 0, DeleteThrows = true };

        var ex = Assert.Throws<InstanceDbCreationAbandonedException>(
            () => Program.RunCreateInstanceDb(gateway, Options(), 60));

        Assert.Equal(0, gateway.Project.SaveCalls);
        Assert.Equal(ExitCodes.RollbackIncomplete, ExitCodes.ForException(ex));
    }

    private static CreateInstanceDbCommandOptions Options() =>
        new("SampleProject", "PLC_1/Program blocks", "iDB_Sim", "FB_Sim", null, 180, 1800);

    private static (int ExitCode, string Stdout, string Stderr) Capture(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new System.IO.StringWriter();
        var stderr = new System.IO.StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            return (action(), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}

/// <summary>A block handle with no Portal behind it.</summary>
internal sealed class FakeDb
{
    public FakeDb(string name, int number)
    {
        Name = name;
        Number = number;
    }

    public string Name { get; }

    public int Number { get; set; }
}

/// <summary>
/// An in-memory project that records what was done to it. <see cref="Blocks"/> and
/// <see cref="SaveCalls"/> together ARE the project: "the failed run changed nothing" is asserted
/// against them, which is the only form of that assertion that would have caught the real defect.
///
/// <see cref="SetNumber"/> is deliberately NOT on <see cref="IInstanceDbSite{TBlock}"/> — the fixed
/// sequence never renumbers, so putting it on the production interface would be dead surface. It
/// lives here because <see cref="InstanceDbCreationTests.Legacy"/> needs it: the setter is what
/// threw on the live job, and the negative test cannot reproduce the defect without it.
/// </summary>
internal sealed class RecordingSite : IInstanceDbSite<FakeDb>
{
    public List<FakeDb> Blocks { get; } = new();

    public int SaveCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public int SetNumberCalls { get; private set; }

    public int ExistingNumbersReads { get; private set; }

    public bool? ExistingNumbersReadBeforeCreate { get; private set; }

    public int NumberFromAutoNumbering { get; set; } = 12;

    /// <summary>Measured on the live job: `set_Number` throws under automatic numbering.</summary>
    public bool SetNumberThrows { get; set; }

    public bool NumberReadThrows { get; set; }

    public bool DeleteThrows { get; set; }

    public bool CreateThrows { get; set; }

    private int _creates;

    public IReadOnlyList<int> ExistingNumbers()
    {
        ExistingNumbersReads++;
        ExistingNumbersReadBeforeCreate ??= _creates == 0;
        return Blocks.Select(b => b.Number).ToList();
    }

    public FakeDb Create(string dbName, string instanceOfName)
    {
        _creates++;
        if (CreateThrows)
        {
            throw new InvalidOperationException("CreateInstanceDB refused.");
        }

        var block = new FakeDb(dbName, NumberFromAutoNumbering);
        Blocks.Add(block);
        return block;
    }

    public int NumberOf(FakeDb block) =>
        NumberReadThrows ? throw new InvalidOperationException("Number getter unavailable.") : block.Number;

    /// <summary>Not part of the site interface — see the class comment.</summary>
    public void SetNumber(FakeDb block, int number)
    {
        SetNumberCalls++;
        if (SetNumberThrows)
        {
            throw new InvalidOperationException("set_Number is not permitted under automatic numbering.");
        }

        block.Number = number;
    }

    public void Delete(FakeDb block)
    {
        DeleteCalls++;
        if (DeleteThrows)
        {
            throw new InvalidOperationException("Delete() refused.");
        }

        Blocks.Remove(block);
    }

    public void Save() => SaveCalls++;
}

public partial class InstanceDbCreationTests
{
    /// <summary>
    /// The pre-fix sequence, transcribed from `OpennessGateway.CreateInstanceDb` as it stood before
    /// 2026-08-13 — prime, create, renumber if invalid, and <c>finally { Save(); }</c>.
    ///
    /// Kept and executed rather than deleted, because it is the only thing that can demonstrate the
    /// guards above are sensitive to the defect they were written for. It is the negative control:
    /// if a future edit puts a save back on the failure path, the fixed sequence starts behaving
    /// like this one and <c>Negative_TheFixedSequenceAndTheOldOneDisagreeAboutSaving</c> fails.
    /// </summary>
    internal static class Legacy
    {
        public static FakeDb Run(RecordingSite site, string dbName, string instanceOfName)
        {
            try
            {
                var takenBefore = site.ExistingNumbers();
                var db = site.Create(dbName, instanceOfName);

                if (!BlockNumbering.IsValid(site.NumberOf(db)))
                {
                    // The lowest-free choice the retired `BlockNumbering.LowestFree` used to make.
                    // Inlined here so the production class can be rid of it while this control keeps
                    // reproducing the exact old behaviour.
                    var used = new HashSet<int>(takenBefore.Concat(site.Blocks.Select(b => b.Number)).Where(BlockNumbering.IsValid));
                    var repaired = 1;
                    while (used.Contains(repaired))
                    {
                        repaired++;
                    }

                    // This is the throw. Everything after it is skipped — except the finally.
                    site.SetNumber(db, repaired);
                }

                return db;
            }
            finally
            {
                // THE DESTRUCTIVE LINE. It runs on the throwing path too, committing a block the
                // caller is about to be told was not created.
                site.Save();
            }
        }
    }
}
