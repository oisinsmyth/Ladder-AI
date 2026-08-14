namespace Converter.InterfaceCheck;

/// <summary>One required name's verdict.</summary>
/// <remarks>
/// *** THREE VERDICTS, NOT TWO. *** <see cref="NotAMemberName"/> exists because "I cannot judge this
/// input" and "the block does not carry it" are different facts that call for opposite actions, and
/// collapsing them would put a FAIL against a block over a caller's typo. Same reasoning as
/// <c>tagstatus</c>'s MEMBER-UNCHECKED.
/// </remarks>
public enum RequirementStatus
{
    /// <summary>Unusable zero value — a verdict nobody set is not a pass.</summary>
    Unstated = 0,

    /// <summary>The block's interface carries a member of this name.</summary>
    Present,

    /// <summary>
    /// The block's interface does NOT carry it. <b>This is a FAIL against the BLOCK</b>, never against
    /// whoever asked — see <see cref="InterfaceCheckReport.BlockFails"/>.
    /// </summary>
    Missing,

    /// <summary>
    /// The required token is not a member name at all (a dotted path, an empty string). Unjudgeable:
    /// the ruling is <i>member names only</i>, so a path is a misunderstanding of the input, not a
    /// finding about the block.
    /// </summary>
    NotAMemberName,
}

/// <summary>One required name, its verdict, and the evidence.</summary>
/// <param name="FoundAt">
/// The interface path the name resolved at (e.g. <c>STATIC/IO/HopperBlockedAlarm</c>). Empty when it
/// did not resolve. <b>Reported because a name can be present in a place that does not help</b> —
/// a reader who sees only PRESENT cannot tell an output member from a nested tunable.
/// </param>
public sealed record InterfaceRequirement(string Name, RequirementStatus Status, string FoundAt, string Detail);

/// <summary>
/// A member the walk could not see inside. <b>The reason a MISSING verdict may be unsound.</b>
/// </summary>
/// <param name="Member">The interface member.</param>
/// <param name="Datatype">Its declared type.</param>
public sealed record OpaqueMember(string Member, string Datatype, string Reason);

/// <summary>
/// <b>NB-30 — the static interface check.</b> A set difference between the response signals a
/// specification names and the block's actual interface, answerable before a scan elapses.
///
/// <para>*** THE OUTCOME IS THE POINT. *** Every gate outcome the harness has blames the SUBMISSION —
/// <c>REFUSED</c> means <i>fix the vector</i>. A missing response signal is a <b>FAIL AGAINST THE
/// BLOCK</b>, and the submission is correct. Reusing a submission-blaming outcome sends an author to
/// edit the artifact that is right.</para>
/// </summary>
/// <param name="IrHash">
/// The block's readable-IR hash — <b>the stamp this result was established against</b>. A consumer
/// carries the stamp, never a bool, so <i>"nobody ran it"</i> and <i>"ran it against a different
/// version of the block"</i> come out as distinct facts.
/// </param>
/// <param name="ExaminedMembers">
/// Every member name the walk saw, as <c>SECTION/path/name</c>. <b>The denominator.</b> An empty
/// examined set is <see cref="Checked"/> = false, never a clean sweep.
/// </param>
/// <param name="ExcludedTempMembers">
/// TEMP members, excluded BY NAME and counted on every run. A temp cannot be observed from outside the
/// block, so calling one PRESENT would answer a different question from the one asked — but a silent
/// exclusion has no cost to grow, so it is printed whether or not it is zero.
/// </param>
public sealed record InterfaceCheckReport(
    string Block,
    string BlockFile,
    string IrHash,
    bool Checked,
    string NotCheckedReason,
    IReadOnlyList<InterfaceRequirement> Requirements,
    IReadOnlyList<string> ExaminedMembers,
    IReadOnlyList<string> ExcludedTempMembers,
    IReadOnlyList<OpaqueMember> OpaqueMembers,
    IReadOnlyDictionary<string, int> SectionCounts,
    string RequirementsSource,
    string CorpusSummary)
{
    /// <summary>The required names the block does not carry.</summary>
    public IReadOnlyList<InterfaceRequirement> Missing =>
        Requirements.Where(r => r.Status == RequirementStatus.Missing).ToArray();

    /// <summary>The required names that were present.</summary>
    public IReadOnlyList<InterfaceRequirement> Present =>
        Requirements.Where(r => r.Status == RequirementStatus.Present).ToArray();

    /// <summary>Inputs that could not be judged.</summary>
    public IReadOnlyList<InterfaceRequirement> Unjudgeable =>
        Requirements.Where(r => r.Status is RequirementStatus.NotAMemberName or RequirementStatus.Unstated).ToArray();

    /// <summary>
    /// <b>The block is at fault.</b> True only when the check actually ran, every input was judgeable,
    /// and at least one required name is absent.
    /// </summary>
    public bool BlockFails => Checked && Missing.Count > 0;

    /// <summary>
    /// Report an outcome that examined nothing. <b>Every field a consumer might read is empty and
    /// <see cref="Checked"/> is false</b>, so no caller can mistake it for a pass.
    /// </summary>
    public static InterfaceCheckReport NotChecked(string block, string reason, string corpusSummary) =>
        new(block, string.Empty, string.Empty, Checked: false, reason,
            Array.Empty<InterfaceRequirement>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<OpaqueMember>(),
            new Dictionary<string, int>(StringComparer.Ordinal),
            RequirementsSource: string.Empty,
            corpusSummary);
}
