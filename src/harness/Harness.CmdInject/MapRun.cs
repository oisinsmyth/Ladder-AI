namespace Harness.CmdInject;

/// <summary>
/// The <c>map</c> verb — <b>resolve the binding against the map and print the whole thing, offline.</b>
///
/// <para>It takes no address and can contact nothing: its entire job is to let a person read, before
/// anyone arms anything, exactly which register the tool believes plays each role. A binding naming the
/// wrong registers is one of the two most dangerous inputs this tool takes, and this verb is where it is
/// caught by eye as well as by the resolver.</para>
/// </summary>
public static class MapRun
{
    public static CmdInjectExit Execute(string tagTablePath, string areaPointerPath, string bindingPath, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("harness-cmd-inject map — OFFLINE. Resolve the binding against the mirror map and print it.");
        output.WriteLine("  This verb takes no address and constructs no transport. It contacts nothing.");
        output.WriteLine();
        output.WriteLine($"tag table   : {tagTablePath}");
        output.WriteLine($"area pointer: {areaPointerPath}");
        output.WriteLine($"binding     : {bindingPath}");
        output.WriteLine();

        var resolution = Resolution.Load(tagTablePath, areaPointerPath, bindingPath);
        if (!resolution.Ok)
        {
            output.WriteLine("== NOT RESOLVED ==");
            foreach (var refusal in resolution.Refusals)
                output.WriteLine($"  - {refusal}");
            output.WriteLine();
            output.WriteLine("  Nothing was contacted. Fix the binding or the map and run `map` again.");
            return CmdInjectExit.MapRefused;
        }

        var binding = resolution.Binding!;
        var map = resolution.Map!;

        output.WriteLine($"map         : base %M{map.BaseByte}, {map.DeclaredRegisters} register(s) declared (0..{map.DeclaredRegisters - 1})");
        output.WriteLine($"command band: {binding.CommandBand}");
        output.WriteLine($"observation : {string.Join("; ", binding.ObservationBands)}");
        output.WriteLine();

        output.WriteLine("== band roles ==");
        foreach (var role in new[] { InjectionRole.Heartbeat, InjectionRole.Enable, InjectionRole.SafetyPermissive })
        {
            if (binding.BandRoles.TryGetValue(role, out var tag))
                output.WriteLine($"  {role,-16} reg {tag.Register,4}  {tag.TypeName,-5} {tag.Address}");
            else
                output.WriteLine($"  {role,-16} (not declared)");
        }
        output.WriteLine();

        foreach (var channel in binding.Channels)
        {
            output.WriteLine($"== channel '{channel.Name}' ==");
            output.WriteLine($"  sequence      reg {channel.SequenceRegister} (written ALONE, second)");
            output.WriteLine($"  operand span  reg {channel.OperandFirstRegister}..{channel.OperandFirstRegister + channel.OperandRegisterCount - 1} " +
                             $"({channel.OperandRegisterCount} register(s), written FIRST)");

            output.WriteLine("  command roles:");
            foreach (var role in channel.CommandRoles.Keys.OrderBy(r => channel.CommandRoles[r].Register))
            {
                var tag = channel.CommandRoles[role];
                output.WriteLine($"    {role,-6} reg {tag.Register,4}{Span(tag)}  {tag.TypeName,-5} {tag.Address}");
            }

            output.WriteLine("  ack roles:");
            foreach (var role in channel.ObservationRoles.Keys.OrderBy(r => channel.ObservationRoles[r].Register))
            {
                var tag = channel.ObservationRoles[role];
                output.WriteLine($"    {role,-9} reg {tag.Register,4}{Span(tag)}  {tag.TypeName,-5} {tag.Address}");
            }

            output.WriteLine();
        }

        output.WriteLine($"== RESOLVED: {binding.Channels.Count} channel(s), every role placed ==");
        return CmdInjectExit.Ok;
    }

    private static string Span(Harness.MirrorView.MirrorTag tag) =>
        tag.RegisterCount == 1 ? "     " : $"..{tag.LastRegister,-3}";
}
