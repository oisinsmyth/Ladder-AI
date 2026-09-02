using Harness.Map;
using ShredderSlotGen;

// Adapted from agent-tasks/pusher-slot/gen/Program.cs. DELIBERATELY WITHOUT ITS `ob` MODE: the pusher
// lane regenerates Main.ir through CyclicObGenerator and is doing so concurrently, so a second lane
// writing that file would be two agents editing one object. This lane's call site is reported instead.
// No copy layer, no mirror, no socket, no Portal - Harness.Map is a pure text-out library.

var mode = args.Length > 0 ? args[0] : "shell";
var repo = args.Length > 1 ? args[1] : ".";
var ir = Path.Combine(repo, "ir", "test-project001");
var stage = Path.Combine(repo, "agent-tasks", "shredder-slot", "generated");
Directory.CreateDirectory(stage);

if (mode is "shell")
{
    var shell = StimShellGenerator.Generate(Spec.Head);
    File.WriteAllText(Path.Combine(stage, Spec.HeadName + ".networks.ir"), shell.Ir);

    Console.WriteLine($"NETWORKS: {shell.Networks.Count}");
    Console.WriteLine("REQUIRED UDT MEMBERS: " + string.Join(", ", shell.RequiredUdtMembers));
    Console.WriteLine("REQUIRED STATICS: " + string.Join(", ", shell.RequiredStatics));
    Console.WriteLine("OBLIGATIONS:");
    foreach (var o in shell.Obligations) Console.WriteLine("  - " + o);
    return 0;
}

if (mode is "udt")
{
    var shell = StimShellGenerator.Generate(Spec.Head);
    var result = StimUdtGenerator.Generate(Spec.Udt, shell);
    File.WriteAllText(Path.Combine(ir, Spec.UdtName + ".ir"), result.Ir.ReplaceLineEndings("\r\n"));
    Console.WriteLine($"UDT {result.TypeName}: {result.DerivedCount} derived, {result.DeclaredCount} declared");
    foreach (var m in result.Members)
        Console.WriteLine($"  {(m.Derived ? "DERIVED " : "DECLARED")} {m.Name} : {m.Datatype}  [{m.Evidence}]");
    foreach (var o in result.Obligations) Console.WriteLine("  OBLIGATION: " + o);
    return 0;
}

if (mode is "idb")
{
    var fbIr = File.ReadAllText(Path.Combine(ir, Spec.HeadName + ".ir"));
    var decl = new InstanceDbDeclaration(
        new InstanceDbNaming(
            Spec.InstanceDb, Spec.InstanceNumber, Spec.HeadName,
            "Single instance of the shredder-sequencer stimulus head. Every command member is written by "
            + "the harness before an index and left at its type default here, because a defaulted scenario "
            + "must refuse rather than run: a zero EndAt ends the scenario at the instant it starts, and a "
            + "zero feedback delay is a contactor that answers before it was asked."),
        InstanceDbMemberSource.ProjectedFromFb);

    var result = InstanceDbGenerator.Generate(decl, fbIr);
    File.WriteAllText(Path.Combine(ir, Spec.InstanceDb + ".ir"), result.Ir.ReplaceLineEndings("\r\n"));
    Console.WriteLine($"iDB {result.DbName} of {result.FbName}, members={result.Members}");
    Console.WriteLine("INHERITED START VALUES: " + (result.InheritedStartValues.Count == 0
        ? "(none - earned zero, computed from the FB own text)"
        : string.Join(", ", result.InheritedStartValues)));
    return 0;
}

Console.Error.WriteLine("modes: shell | udt | idb");
return 2;
