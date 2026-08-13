namespace OpennessCli.Openness;

// FI-63. `create-instance-db` gave the FIRST instance DB created after a project open the number 0,
// which is not a valid block number; subsequent creations in the same session numbered correctly.
// Measured deterministically three times on a live job.
//
// WHY IT IS SERIOUS RATHER THAN UNTIDY — it is the FI-52 family again: a whole-device compile reports
// Success over an invalid-numbered block, and only the PER-BLOCK compile surfaces it
// ("has an invalid number 0"). So the default gate passes and the defect ships.
//
// The number choice is separated from the Openness call so the rule can be tested without a Portal —
// the gateway half (priming the composition, reading the number back, correcting it) cannot be.
public static class BlockNumbering
{
    /// <summary>
    /// A block number Openness will accept. Zero is the observed failure and negatives are not
    /// numbers; everything else is left alone, because this exists to catch a specific broken value
    /// and not to impose a numbering policy the project does not have.
    /// </summary>
    public static bool IsValid(int number) => number > 0;

    // `LowestFree(taken, floor)` lived here until 2026-08-13. It chose the replacement number for the
    // renumber repair, and the repair is retired: `set_Number` throws under automatic numbering, so
    // the repair never once succeeded, and the `finally { SaveProject(); }` it unwound through
    // COMMITTED the broken block it had just failed to fix (see InstanceDbCreation). Deleted rather
    // than left in place, because a number-allocator sitting next to a check that detects bad numbers
    // is an invitation to wire them back together, and the seam that must not be recreated is exactly
    // "mutate further to rescue a mutation that already went wrong".
}
