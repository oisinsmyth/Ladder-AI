using System;
using System.Collections.Generic;

namespace OpennessCli.Openness;

/// <summary>
/// The four Openness operations `create-instance-db` performs, behind a seam with no Portal in it.
///
/// It exists for the same reason <see cref="BlockNumbering"/> does, and for a sharper one. The
/// sequencing — what is done, in what order, and above all WHEN THE PROJECT IS SAVED — is where the
/// destructive defect lived, and a rule that can only be exercised against a live TIA session is a
/// rule nobody exercises. <see cref="OpennessGateway"/> binds these to real `PlcBlockComposition`
/// calls; the tests bind them to an in-memory list and can therefore make `set_Number` throw, make
/// `Delete` fail, and then ASSERT ON WHETHER THE PROJECT WAS SAVED — which is the whole question.
/// </summary>
/// <typeparam name="TBlock">
/// The block handle. Generic so the gateway can pass a `PlcBlock` without this file referencing
/// `Siemens.Engineering` at all — that reference is what makes the rest of the gateway untestable.
/// </typeparam>
public interface IInstanceDbSite<TBlock>
    where TBlock : class
{
    /// <summary>
    /// The block numbers already in the target group. Read-only, and the read itself is the point —
    /// see FI-63 part 1 in <see cref="InstanceDbCreation.Run"/>.
    /// </summary>
    IReadOnlyList<int> ExistingNumbers();

    /// <summary>
    /// `PlcBlockComposition.CreateInstanceDB(name, isAutoNumbered: true, 0, instanceOfName)`.
    /// Mutates the IN-MEMORY project model only; nothing reaches disk until <see cref="Save"/>.
    /// </summary>
    TBlock Create(string dbName, string instanceOfName);

    /// <summary>`PlcBlock.Number` — the value FI-63 measured as 0 on the first creation.</summary>
    int NumberOf(TBlock block);

    /// <summary>`PlcBlock.Delete()`. Used only to undo a create this same call made.</summary>
    void Delete(TBlock block);

    /// <summary>`Project.Save()` — the ONLY operation here that reaches disk.</summary>
    void Save();
}

/// <summary>
/// `create-instance-db`'s sequence, and the one property it now guarantees: <b>a run that does not
/// return a usable instance DB leaves the project unchanged.</b>
///
/// WHAT THIS REPLACES, AND WHY IT WAS WORSE THAN A PLAIN FAILURE (2026-08-13). The previous version
/// wrapped the whole operation in <c>finally { SaveProject(); }</c>. On this project the sequence ran:
/// create (auto-numbered) returns a DB numbered 0 → FI-63's repair sets <c>db.Number</c> → <b>the
/// setter throws</b> under automatic numbering → the exception unwinds → <b>the finally saves</b>.
/// So the command reported a failure it had already COMMITTED: a `DB0` that can neither compile nor
/// export, in a project the caller had just been told nothing happened to. Three delete-and-retry
/// cycles later it was still there, because each retry recreated it. A repair had turned a
/// recoverable state into a hard one.
///
/// The fix is not a better repair — it is that <b>the save moved onto the success path</b>. Openness
/// mutates an in-memory model and <c>Project.Save()</c> is the only thing that commits it (see
/// <c>OpennessGateway.SaveProject</c>), so declining to save is a complete undo of everything on
/// disk. The rollback below is the second half, for the in-memory session that may be a human's
/// Portal window rather than one this process launched.
///
/// WHAT IS DELIBERATELY NOT HERE. There is no renumber. FI-63's repair is retired rather than
/// hardened, because it is measured to throw on the only project it has ever run against, and
/// because the class of thing it is — mutate further to rescue a mutation that already went wrong —
/// is what made the defect destructive. The candidate replacement (delete the bad block and create
/// once more, i.e. FI-63's own throwaway workaround performed safely) is written up in
/// `src/openness-cli/README.md` as the next step and is NOT implemented, because it cannot be
/// verified without a live Portal and an unverified second write is the same bet again.
/// </summary>
public static class InstanceDbCreation
{
    public static TBlock Run<TBlock>(IInstanceDbSite<TBlock> site, string dbName, string instanceOfName)
        where TBlock : class
    {
        // FI-63, part 1 — kept, because it is the only part of the old fix that is READ-ONLY and so
        // the only part that cannot hurt. The defect was deterministic on the FIRST creation after a
        // project open and absent afterwards, and the workaround that unblocked the live job was to
        // create a throwaway DB and delete it. What a throwaway does, incidentally, is force the
        // composition to be enumerated — so enumerate it deliberately. Still a hypothesis about
        // someone else's allocator, still unverified against a live Portal; it just costs nothing.
        _ = site.ExistingNumbers();

        // From here to the Save is the only window in which the project model is dirty. Nothing in
        // it is on disk, and every exit from it that is not the Save undoes what it can.
        var db = site.Create(dbName, instanceOfName);

        int number;
        try
        {
            number = site.NumberOf(db);
        }
        catch (Exception ex)
        {
            // Reading the number back is how the whole defect is detected, so a read that FAILS is
            // not a reason to proceed — it is the same unanswered question as an invalid number
            // (FI-44, empty is not clean).
            throw Abandon(site, db, dbName, number: null, cause: ex);
        }

        if (!BlockNumbering.IsValid(number))
        {
            throw Abandon(site, db, dbName, number, cause: null);
        }

        // The one commit, on the one path where the operation actually completed. If Save itself
        // throws, it is deliberately NOT caught: nobody can tell from here whether it committed, and
        // deleting a block that may already be on disk is precisely the "repair" that caused this.
        // Let it escape as the unexpected error it is.
        site.Save();
        return db;
    }

    /// <summary>
    /// Undo the create and refuse to save. Returns the exception rather than throwing it, so the
    /// call site reads `throw Abandon(...)` and the compiler can see the path terminates.
    /// </summary>
    private static Exception Abandon<TBlock>(
        IInstanceDbSite<TBlock> site, TBlock db, string dbName, int? number, Exception? cause)
        where TBlock : class
    {
        // Best-effort, and bounded to exactly one thing: the block this call created, moments ago,
        // whose handle we are holding. It never searches for a block by name and never touches
        // anything it did not make — the failure being fixed is a tool that damaged a project while
        // trying to be helpful.
        Exception? rollbackFailure = null;
        try
        {
            site.Delete(db);
        }
        catch (Exception ex)
        {
            rollbackFailure = ex;
        }

        // NO Save() ON THIS PATH, IN ANY FORM. This is the fix. Everything above happened in the
        // in-memory model; without this call none of it exists on disk, whether the rollback worked
        // or not.
        return new InstanceDbCreationAbandonedException(dbName, number, rollbackFailure, cause);
    }
}

/// <summary>
/// `create-instance-db` did not produce a usable instance DB, and <b>the project was not saved</b>.
///
/// Replaces `InvalidBlockNumberException` (retired 2026-08-13), whose message — "setting it to N did
/// not take" — described a renumber that no longer happens, and which said nothing about the thing
/// the caller most needs to know: whether the failure is on disk. It was also unclassified in
/// <c>ExitCodes.ForException</c>, so this exact defect reported as exit 5, an internal fault.
/// </summary>
public sealed class InstanceDbCreationAbandonedException : Exception
{
    public InstanceDbCreationAbandonedException(string dbName, int? number, Exception? rollbackFailure, Exception? cause)
        : base(BuildMessage(dbName, number, rollbackFailure), cause)
    {
        DbName = dbName;
        Number = number;
        RollbackCompleted = rollbackFailure is null;
    }

    public string DbName { get; }

    /// <summary>The number that came back, or null if it could not be read at all.</summary>
    public int? Number { get; }

    /// <summary>
    /// Whether the partially-created block was removed from the in-memory project model. The
    /// on-disk guarantee holds either way — nothing was saved — but a false here means a Portal
    /// session that may belong to a human is holding a stray block, and it decides the exit code
    /// (<c>RollbackIncomplete</c> rather than <c>ChangeAbandoned</c>).
    /// </summary>
    public bool RollbackCompleted { get; }

    private static string BuildMessage(string dbName, int? number, Exception? rollbackFailure)
    {
        var what = number is { } n
            ? $"was created with invalid block number {n}"
            : "was created and its block number could not be read back";

        var head =
            $"create-instance-db abandoned: instance DB '{dbName}' {what}, so it was not kept. " +
            "A whole-device compile reports Success over an invalid-numbered block — only a per-block " +
            "compile reports 'has an invalid number' — so this is refused here rather than shipped.";

        return rollbackFailure is null
            ? head + "\n  PROJECT UNCHANGED: the partial block was removed and the project was NOT saved. " +
                     "Nothing to clean up; retry is safe."
            : head + $"\n  NOT SAVED, BUT NOT CLEAN: the project was NOT saved, so nothing reached disk, but " +
                     $"removing the partial block from the open session also failed ({rollbackFailure.GetType().Name}: " +
                     $"{rollbackFailure.Message}). Close this Portal session WITHOUT saving before retrying, and do " +
                     "not save it by hand — the stray block only exists in memory.";
    }
}
