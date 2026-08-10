using System;
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
// Only the number CHOICE is testable here; priming the composition and reading the number back need a
// live Portal and are owed a verification run.
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

    [Fact]
    public void LowestFree_TakesTheGapRatherThanPushingTheNumberingUp()
    {
        // Lowest-free, not highest-plus-one: this runs only to repair a block that came back invalid,
        // so it should slot into a gap the project already has instead of growing the space each time.
        Assert.Equal(2, BlockNumbering.LowestFree(new[] { 1, 3, 4 }));
        Assert.Equal(5, BlockNumbering.LowestFree(new[] { 1, 2, 3, 4 }));
        Assert.Equal(1, BlockNumbering.LowestFree(Array.Empty<int>()));
    }

    // The broken value is 0, and 0 must never be treated as "taken" — otherwise the repair could
    // consider the invalid number it is repairing to be an occupied slot.
    [Fact]
    public void LowestFree_IgnoresInvalidNumbersInTheTakenSet()
    {
        Assert.Equal(1, BlockNumbering.LowestFree(new[] { 0, -5 }));
        Assert.Equal(2, BlockNumbering.LowestFree(new[] { 0, 1 }));
    }

    [Fact]
    public void LowestFree_RespectsAFloorAndNeverReturnsZero()
    {
        Assert.Equal(100, BlockNumbering.LowestFree(new[] { 1, 2, 3 }, floor: 100));
        Assert.Equal(101, BlockNumbering.LowestFree(new[] { 100 }, floor: 100));
        Assert.Equal(1, BlockNumbering.LowestFree(Array.Empty<int>(), floor: 0));
        Assert.Equal(1, BlockNumbering.LowestFree(Array.Empty<int>(), floor: -3));
    }

    [Fact]
    public void TheFailureMessageNamesWhyAGreenDeviceCompileIsNotEvidence()
    {
        var message = new InvalidBlockNumberException("iDB_Sim", 0, 12).Message;

        Assert.Contains("iDB_Sim", message);
        Assert.Contains("whole-device compile reports Success", message);
        Assert.Contains("per-block", message);
    }
}
