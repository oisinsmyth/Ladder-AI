using Converter.SimaticMl;

namespace Converter.Ir;

// Maps a callee block name → its parameter interface (param name → Type + Section), sourced from the
// callee's OWN .ir INPUT/OUTPUT sections. This is what lets `to-xml --synthesize` mint a wired-argument
// CALL: the readable CALL deliberately omits argument types (ADR-0001 — `Ir/Model.cs`: "the callee's
// own .ir file is the source of truth for its interface"), so the synthesizer looks the type up here,
// mirroring what the read side gets from a source `<Parameter Type=…>` element
// (`GraphReducer.ReduceCall`). Input + Output only in this cut (matching the read side's Input/else-
// Output shape); InOut is a future extension and is deliberately not registered, so a wired InOut arg
// resolves as "unknown parameter" and hard-errors rather than being silently mistyped.
public sealed class CalleeInterfaceRegistry
{
    public sealed record Param(string Type, string Section);

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, Param>> _byBlock;

    private CalleeInterfaceRegistry(IReadOnlyDictionary<string, IReadOnlyDictionary<string, Param>> byBlock) =>
        _byBlock = byBlock;

    public static readonly CalleeInterfaceRegistry Empty =
        new(new Dictionary<string, IReadOnlyDictionary<string, Param>>(StringComparer.Ordinal));

    // Builds the registry from already-parsed callee blocks. A block with no In/Out params still
    // registers (an empty param map), so a zero-argument CALL to it resolves as "known callee, no
    // params" rather than "unknown callee". Duplicate block names in the batch: last wins.
    public static CalleeInterfaceRegistry FromBlocks(IEnumerable<IrBlock> blocks)
    {
        var byBlock = new Dictionary<string, IReadOnlyDictionary<string, Param>>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            var parms = new Dictionary<string, Param>(StringComparer.Ordinal);
            foreach (var member in block.InputMembers ?? Array.Empty<DbMember>())
            {
                parms[member.Name] = new Param(NormalizeType(member.Datatype), "Input");
            }

            foreach (var member in block.OutputMembers ?? Array.Empty<DbMember>())
            {
                parms[member.Name] = new Param(NormalizeType(member.Datatype), "Output");
            }

            byBlock[block.Name] = parms;
        }

        return new CalleeInterfaceRegistry(byBlock);
    }

    // True + the param map when the block is known (even with zero params). Distinguishing "unknown
    // block" from "known block, unknown param" lets BuildCallSidecar give a precise error.
    public bool TryGetBlock(string blockName, out IReadOnlyDictionary<string, Param> parms) =>
        _byBlock.TryGetValue(blockName, out parms!);

    // A SimaticML `<Parameter Type="…">` value is the bare type name (e.g. `Word`, `UDT_ShredderInImage`);
    // a UDT-typed member in the IR grammar may carry its type quoted (`Name : "UDT_X"`). Trim any
    // surrounding quotes so the emitted Parameter type matches what the read side records — a no-op for
    // primitive types, which never carry quotes.
    private static string NormalizeType(string type) => type.Trim('"');
}
