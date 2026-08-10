using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// The lowest free number at or above <paramref name="floor"/>, given what is already taken.
    ///
    /// Lowest-free rather than highest-plus-one on purpose: this runs only to repair a block that
    /// came back invalid, so it should slot into the gap the project already has rather than push the
    /// numbering space up every time the defect fires. Hard rule 3 is not in play — a block number
    /// chosen by the tool for a block the tool just created is not an invented address for a plant
    /// signal, and auto-numbering was already choosing one.
    /// </summary>
    public static int LowestFree(IEnumerable<int> taken, int floor = 1)
    {
        var used = new HashSet<int>(taken.Where(IsValid));
        var candidate = floor < 1 ? 1 : floor;
        while (used.Contains(candidate))
        {
            candidate++;
        }

        return candidate;
    }
}
