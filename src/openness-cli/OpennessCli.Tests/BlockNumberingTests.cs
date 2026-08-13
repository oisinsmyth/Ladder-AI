using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

// FI-63. `create-instance-db` gave the FIRST instance DB created after a project open the number 0 —
// not a valid block number — while later creations in the same session numbered correctly. Measured
// deterministically three times on a live job.
//
// It matters because it is FI-52's family: a WHOLE-DEVICE COMPILE REPORTS SUCCESS over an
// invalid-numbered block, and only the per-block compile says "has an invalid number 0". The default
// gate passes and the defect ships.
//
// This file shrank on 2026-08-13, when the RENUMBER REPAIR was retired. `LowestFree` chose the
// replacement number for that repair; `set_Number` throws under automatic numbering so the repair
// never once succeeded, and the `finally { SaveProject(); }` it unwound through committed the broken
// block it had just failed to fix. What replaced it is a refusal that leaves the project unchanged —
// see `InstanceDbCreationTests`, which is where the FI-63 detection now lives and where it is tested
// against the project state rather than against a return value.
//
// What is left here is the detection rule itself, which is still exactly what decides whether a
// created block is kept.
public class BlockNumberingTests
{
    [Fact]
    public void Zero_IsTheInvalidValueThisExistsToCatch()
    {
        Assert.False(BlockNumbering.IsValid(0));
        Assert.False(BlockNumbering.IsValid(-1));
        Assert.True(BlockNumbering.IsValid(1));
        Assert.True(BlockNumbering.IsValid(37));
    }
}
