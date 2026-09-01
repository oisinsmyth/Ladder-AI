namespace OpennessCli.Model
{
    /// <summary>
    /// One device item and the identifier attributes it self-describes.
    /// </summary>
    /// <param name="Path">
    /// The item's path, in the same shape <c>list</c> prints — copy it verbatim if you need to
    /// name this item to another command. A device item's real name can contain spaces and an
    /// embedded article number as one literal string.
    /// </param>
    /// <param name="TypeName">The CLR type Openness returned, so an unexpected shape is visible.</param>
    /// <param name="Attributes">
    /// Identifier-bearing attributes, DISCOVERED through <c>GetAttributeInfos</c> rather than
    /// guessed by name. Empty is normal: most device items carry none.
    /// </param>
    public sealed record HardwareIdentifierItem(
        string Path,
        string TypeName,
        System.Collections.Generic.IReadOnlyList<HardwareIdentifierAttribute> Attributes);

    /// <param name="Name">The attribute name exactly as Openness spells it.</param>
    /// <param name="Value">
    /// Its value rendered as text, or a bracketed reason it could not be read. A value that could
    /// not be read is NOT rendered as empty: an attribute that threw and an attribute that is
    /// genuinely blank are different facts, and this is the field that has to keep them apart.
    /// </param>
    public sealed record HardwareIdentifierAttribute(string Name, string Value);

    /// <summary>
    /// 🔴 <b>WHAT THIS IS FOR: reading a hardware identifier OFF THE DEVICE'S OWN CONFIGURATION,
    /// never from another project's block.</b>
    ///
    /// <para><c>Harness.Map/CommsFbGenerator.cs</c> says exactly that of the <c>HW_ANY</c> interface
    /// identifier a Modbus server block carries. Until this command existed there was no way to
    /// satisfy it: nothing in this CLI exposed a system constant or a hardware identifier, so the
    /// value got carried across from a sibling project and flagged as unverified. A wrong identifier
    /// is not a compile error — it is a connection that never establishes.</para>
    ///
    /// <para><b>IT READS THE PROJECT, NOT THE CONTROLLER.</b> These are the identifiers the hardware
    /// configuration DECLARES. If the project is stale with respect to the device, this reports the
    /// project's answer with total confidence and it is the wrong one — the same caveat
    /// <c>served-area</c> prints about the corpus. Nothing here contacts a CPU.</para>
    ///
    /// <para><b>ATTRIBUTE NAMES ARE DISCOVERED, NOT ASSUMED.</b> The set is whatever
    /// <c>GetAttributeInfos</c> reports whose name carries an identifier sense. Openness spells
    /// these differently between device families, and a hardcoded name reads nothing on the family
    /// it was not written for — the failure mode already recorded for "ScreenNumber" on classic
    /// screens.</para>
    /// </summary>
    /// <param name="Items">Every device item walked, in tree order.</param>
    /// <param name="ItemsWalked">
    /// How many items were examined. <b>EMPTY IS NOT CLEAN:</b> zero items walked means the device
    /// filter matched nothing, and a report with no identifiers in it is then a statement about the
    /// filter rather than about the hardware.
    /// </param>
    /// <param name="ItemsWithIdentifiers">How many of them carried at least one.</param>
    /// <param name="DeviceFilter">The filter applied, or null for every device.</param>
    public sealed record HardwareIdentifierResult(
        System.Collections.Generic.IReadOnlyList<HardwareIdentifierItem> Items,
        int ItemsWalked,
        int ItemsWithIdentifiers,
        string? DeviceFilter);
}
