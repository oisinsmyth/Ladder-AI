namespace Converter.SignalSet;

// What the SUBJECT BLOCK does with the signal — never what the project does with it. A harness binding
// needs exactly this distinction: a signal the block READS is one the harness must stimulate, a signal
// it WRITES is one the harness observes, and getting the two the wrong way round produces a binding
// that drives an output and watches an input.
//
// COMPUTED from the usage graph, like MemberRole in candidate-scan and for the same measured reason:
// under C-132 the members that matter live under STATIC inside interface-UDT structs while the
// SimaticML INPUT/OUTPUT sections carry unrelated data-link words, so section-filtering answers the
// wrong question on the real corpus.
public enum SignalDirection
{
    Read,     // the block reads it
    Written,  // the block writes it
    Both,     // read and written (an internal latch exposed on the interface)
    Unused,   // declared on the interface and neither read nor written by the block
}

// WHERE THE SIGNAL IS DECLARED. Kept separate from the direction because a binding generator makes
// different decisions on each axis: the direction says which side of the wire the harness sits on, the
// origin says whether there is a declaration to bind against at all.
public enum SignalDeclaration
{
    Interface,   // the subject block's own interface member
    GlobalDb,    // a member of a global data block
    TagTable,    // a PLC tag-table entry
    InstanceDb,  // another placement's interface, addressed absolutely (iDB_X.IO.Step)

    // 🔴 REFERENCED BY THE BLOCK AND DECLARED NOWHERE THE INVENTORY WALKED. A raw address, or a
    // declaration that lives in a file this export does not contain. It is emitted rather than
    // dropped: a binding generator handed a silently-shortened signal set produces a binding that
    // looks complete, and the missing entries are exactly the ones nothing else will mention.
    Undeclared,
}

// One signal in the set. `Writers`/`Readers` are PROJECT-WIDE sites (`Block N<n>`) while `Direction` is
// relative to the subject — a harness needs both: the direction to place itself, the site lists to know
// who else is already driving the signal it is about to contend with.
public sealed record SignalEntry(
    string Member,
    string Path,
    // Null ONLY for an Undeclared reference. Nothing in the corpus states the type, and naming one
    // anyway would be an invented fact on the document a harness binds from.
    string? Type,
    bool Retain,
    string? StartValue,
    SignalDirection Direction,
    SignalDeclaration Origin,
    IReadOnlyList<string> Writers,
    IReadOnlyList<string> Readers);

// FI-44 - why the set is empty. Two ways, and they need different words because they need different
// actions: fix the name you asked for, versus widen the filters you asked with.
public enum SignalSetScope
{
    Scanned,        // the block exists and the set below is a real result
    UnknownBlock,   // no block of this name in the corpus - a typo, a not-yet-written block, or a wrong --project
    NoSignalsInScope, // the block exists; the origin/type/direction filters left nothing
}

public sealed record SignalSetReport(
    string ProjectDir,
    int FilesScanned,
    string BlockName,
    string OriginFilter,
    string? TypeFilter,
    string DirectionFilter,
    IReadOnlyList<SignalEntry> Entries,
    IReadOnlyList<string> Warnings,
    SignalSetScope Scope = SignalSetScope.Scanned)
{
    // The count of the block's OWN interface members in the set, stated separately because the two
    // halves answer different questions and a single total hides which one is empty.
    public int InterfaceCount => Entries.Count(e => e.Origin == SignalDeclaration.Interface);

    public int ReferencedCount => Entries.Count - InterfaceCount;

    // 🔴 "EMPTY IS NOT CLEAN", and KEYED ON THE ROW COUNT as well as on the scope enum — the lesson
    // UndrivenScanReport.ExaminedNothing records after its third shape arrived three weeks after the
    // first two were called complete. The enum is a catalogue of causes someone has already thought
    // of; the row count is the fact itself, so a third shape gates on arrival rather than on being
    // noticed.
    public bool ExaminedNothing => Scope is not SignalSetScope.Scanned || Entries.Count == 0;

    // 🔴 THE SET IS NOT A COMPLETE STATEMENT OF THE BLOCK'S SIGNALS — a file in the corpus could not be
    // parsed, so members may be missing from it and referenced paths may be misclassified Undeclared.
    //
    // EXIT-BEARING rather than a WARNING line, deliberately. Every sibling prints its inventory
    // warnings and exits 0, which is right for a check a human reads; this document is the INPUT to a
    // harness binding, so the incompleteness is consumed by a generator that will never see the
    // warning. A detection that only warns on the path that reaches production gets skimmed.
    public bool Partial => Warnings.Count > 0;
}
