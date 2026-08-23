namespace Converter.ServedArea;

/// <summary>
/// One parsed <c>MB_HOLD_REG</c> area pointer — <c>P#M1000.0 WORD 37</c>.
/// </summary>
/// <param name="Text">Verbatim, exactly as the IR carries it. Everything else here is derived from it.</param>
public sealed record ServedAreaPointer(string Text, string Area, int BaseByte, int Bit, string Unit, int Count);

/// <summary>
/// What the program corpus says the Modbus server actually serves.
/// </summary>
/// <param name="Derived">
/// True only when BOTH homes of the number were read and AGREED. Anything else is
/// <see cref="Refusals"/> (a defect in the corpus) or <see cref="NotDerivedReason"/> (an absence) —
/// and neither is a pass.
/// </param>
/// <param name="BlocksScanned">
/// <b>The denominator, printed on every run.</b> "No MB_SERVER call" over 18 blocks and "no MB_SERVER
/// call" over 0 blocks are different facts, and only this number separates them.
/// </param>
public sealed record ServedAreaReport(
    bool Derived,
    string Area,
    int BaseByte,
    int Registers,
    string BlockName,
    string File,
    int ReadableLine,
    int SidecarLine,
    string ReadableText,
    string SidecarText,
    int FilesScanned,
    int BlocksScanned,
    IReadOnlyList<string> Refusals,
    string NotDerivedReason,
    IReadOnlyList<string> Unreadable)
{
    public bool Refused => Refusals.Count > 0;

    /// <summary>
    /// <b>The line printed on every run, whatever the outcome.</b> Shape borrowed from
    /// <c>drift-check</c>'s <c>COMPARED:</c> and <c>preflight</c>'s <c>RESOLVED AGAINST:</c>: a green
    /// that does not say what it examined is the shape this project keeps being caught by.
    /// </summary>
    public string Denominator => Refused
        ? $"REFUSED — {BlocksScanned} block(s) in {FilesScanned} file(s) scanned; {Refusals.Count} refusal(s) below. "
          + "The served area was NOT derived, and the declared width therefore stands unchecked."
        : Derived
            ? $"served area: base {BaseByte}, {Registers} register(s), derived from {File}:{ReadableLine} + sidecar {File}:{SidecarLine}"
            : $"NOT DERIVED — {BlocksScanned} block(s) in {FilesScanned} file(s) scanned, {NotDerivedReason}";

    public static ServedAreaReport NotDerived(int files, int blocks, string reason) =>
        new(false, string.Empty, 0, 0, string.Empty, string.Empty, 0, 0, string.Empty, string.Empty,
            files, blocks, Array.Empty<string>(), reason, Array.Empty<string>());

    public static ServedAreaReport Refuse(int files, int blocks, params string[] refusals) =>
        new(false, string.Empty, 0, 0, string.Empty, string.Empty, 0, 0, string.Empty, string.Empty,
            files, blocks, refusals, string.Empty, Array.Empty<string>());
}
