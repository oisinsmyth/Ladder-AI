using Harness.Map;
using PusherSlotGen;

var mode = args.Length > 0 ? args[0] : "shell";
var repo = args.Length > 1 ? args[1] : ".";
var ir = Path.Combine(repo, "ir", "test-project001");
var stage = Path.Combine(repo, "agent-tasks", "pusher-slot", "generated");
Directory.CreateDirectory(stage);

if (mode is "shell")
{
    var shell = StimShellGenerator.Generate(Spec.Head);
    File.WriteAllText(Path.Combine(stage, "FB_PusherStim.networks.ir"), shell.Ir);

    Console.WriteLine($"NETWORKS: {shell.Networks.Count}");
    Console.WriteLine("REQUIRED UDT MEMBERS: " + string.Join(", ", shell.RequiredUdtMembers));
    Console.WriteLine("REQUIRED STATICS: " + string.Join(", ", shell.RequiredStatics));
    Console.WriteLine("OBLIGATIONS:");
    foreach (var o in shell.Obligations) Console.WriteLine("  - " + o);
    Console.WriteLine();
    Console.WriteLine(shell.Ir);
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
            "Single instance of the pusher stimulus head. Every command member is written by the harness "
            + "before an index and left at its type default here, because a defaulted scenario would run "
            + "rather than refuse: a zero EndAt ends the scenario at the instant it starts, and a zero "
            + "edge time is a signal that has already changed."),
        InstanceDbMemberSource.LeftToTia);

    var result = InstanceDbGenerator.Generate(decl, fbIr);
    File.WriteAllText(Path.Combine(ir, Spec.InstanceDb + ".ir"), result.Ir.ReplaceLineEndings("\r\n"));
    Console.WriteLine($"iDB {result.DbName} of {result.FbName}, members={result.Members}");
    Console.WriteLine("INHERITED START VALUES: " + (result.InheritedStartValues.Count == 0
        ? "(none - earned zero, computed from the FB own text)"
        : string.Join(", ", result.InheritedStartValues)));
    return 0;
}

if (mode is "ob")
{
    var calls = new List<ObCall>
    {
        new("FC_HarnessStimArbiter", null, "Harness Stimulus Arbitration Layer",
            "Releases the two things every stimulus head shares - the input map's test-injection array and the plant's single operator fault-reset line - to their inert values, once, before any head runs. IT MUST BE FIRST AND THAT IS NOT COSMETIC. Every head below owns the lines its own scenario needs and passes the rest through unchanged, which is what stops an idle head from wiping a running neighbour; but a pass-through with no rest value underneath it is a self-hold, so without this release a line would carry whatever the last head to drive it left, on every scan after it. The injection enable is the sharpest case: no head clears it - each ORs itself in over this release - so a sweep with this block missing, or placed after any head, latches injection on permanently and the input map never returns to the field for the life of the CPU. Placed after a head it is worse still: it would wipe that head's own assertion in the same scan it was made."),
        new("FB_HopperBlockageStim", "iDB_HopperBlockageStim", "Hopper-Blockage Stimulus Model",
            "Advances the commanded timeline the hopper-blockage monitor is tested against and presents it to the input buffer's test members. It runs ahead of the input map because for a commanded input the model is the field: the map that follows chooses between the terminal and the test member, and it must find this scan's commanded state rather than the previous scan's. Outside a run it drives nothing and the map reads the field."),
        new("FB_PusherStim", "iDB_PusherStim", "Pusher Stimulus Head",
            "Advances the commanded timeline the pusher control block is tested against and presents it to the input buffer's test members and to the operator command bits the pusher is wired from. Same placement reasoning as the model above: for a commanded input the head is the field, so it must run ahead of the input map. It shares the injection array and the reset line with the heads either side of it and writes both by the pass-through idiom: while its own index is running the commanded value owns each line, and while it is not, whatever a neighbouring head wrote in this same scan is handed on unchanged. This head asserts nothing at all while its own index is idle, so it never wipes a neighbour - but it must still run AFTER the hopper head, which does assert while idle, throughout the trailing cleardown that follows every hopper index. Ahead of it, this head's commanded hopper level and its reset request would both be driven low for the length of that window. Its position ahead of the input map is load-bearing for the ordinary reason: for a commanded input a head is the field."),
        new("FB_ShredderSequencerStim", "iDB_ShredderSequencerStim", "Shredder Sequencer Stimulus Head",
            "Advances the commanded timeline FB_ShredderSequencer is tested against and presents it to the input buffer's test members and to the operator pusher mode. Same placement reasoning as the two heads above - a commanded input reaches the control through the map, so a head must run ahead of the map - and the same pass-through idiom on every shared line. Until this call existed the block was in the program and reachable from nothing: it would import, compile and never run, while its slot's start echo still reported the slot commanded, which is the shape that costs a wave rather than announcing itself."),
        new("FC_Inputs", null, "Input Mapping"),
        new("FC_ControlMain", null, "Control"),
        new("FC_AlarmsMain", null, "Alarms"),
        new("FC_Outputs", null, "Output Mapping"),
        new("FB_HarnessViolationLatch", "iDB_HarnessViolationLatch", "Harness Violation Latches",
            "Latches the states the requirement register forbids outright. It runs after the control so it sees the outputs this scan's control produced, and before the copy layer so a state that lasted one scan is already latched when the mirror is written - a forbidden state that had to survive until the next scan to be published would be a forbidden state the harness could miss."),
        new("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer",
            "Publishes the build stamp, advances the scan counter, copies the commanded scenario out of the mirror into the stimulus model and the monitored block's outputs back into it. It runs after the control so the observations it publishes are the ones this scan's control produced, and before the server below so the client is served a mirror this scan wrote rather than one being written while it is read. The commanded scenario it copies in is acted on by the model at the top of the next scan, which is the one place a scan of latency is spent and the only place it can be."),
        new("FB_Comms_ModbusServer", "iDB_Comms_ModbusServer", "Modbus Server",
            "Serves the harness mirror in marker memory to a Modbus TCP client. It runs last so the whole transfer between the wire and marker memory happens with no other block touching those registers in between: what the client reads is the mirror exactly as this scan left it, and what the client writes is in place before the next scan reads it."),
        new("FB_HxBoolEcho", "iDB_HxBoolEcho", "Corpus Block - Bool Echo",
            "Test material for the tooling campaign, not plant logic. It reads and writes members of its own instance DB and nothing else, so it touches none of the marker registers the server above transfers and the network order of everything before it is unaffected. Placement after the copy layer rather than before it is latency-neutral and was checked rather than assumed: before the layer the block computes from the previous scan's commanded inputs and the layer publishes the result immediately, after it the block computes from this scan's inputs and the layer publishes on the next scan - two scans from command to observation either way."),
        new("FB_HxDwellTimer", "iDB_HxDwellTimer", "Corpus Block - Dwell Timer",
            "Test material for the tooling campaign, not plant logic. Same placement reasoning as the network above, and the same isolation: it touches only its own instance DB, including its own timer instance."),
        new("FB_HxIntStep", "iDB_HxIntStep", "Corpus Block - Int Sum",
            "Test material for the tooling campaign, not plant logic. Same placement reasoning as the networks above, and the same isolation: it touches only its own instance DB."),
        new("FB_HxSealLatch", "iDB_HxSealLatch", "Corpus Block - Sealed Latches",
            "Test material for the tooling campaign, not plant logic. Same placement reasoning as the networks above, and the same isolation: it touches only its own instance DB. Its two outputs are seals the block holds itself; they are values under test and not harness instrumentation, so an expectation on either is sampled against a copy-layer coil rather than declared latched."),
    };

    var presence = new List<ObCallPresence>
    {
        new("FC_HarnessStimArbiter", "nothing else releases the injection enable or the shared reset line. Omitted, every head's pass-through rung becomes a self-hold and the input map never returns to the field for the life of the CPU - which reads as a program that works until the first index that does not drive an index some other slot needs."),
        new("FB_PusherStim", "the pusher head drives the block under test and publishes the index outcome bits; a head called by nothing leaves every vector timing out while the start echo still reports the slot commanded and running."),
        new("FB_ShredderSequencerStim", "same reason, and it was measured in this program rather than imagined: the block was committed and called from nowhere, so it imported and compiled clean and did nothing at all."),
        new("FC_HarnessCopyLayer", "the copy layer is the only thing that writes the mirror the client reads."),
    };

    var order = new List<ObCallOrder>
    {
        new("FC_HarnessStimArbiter", "FB_HopperBlockageStim", "the arbiter releases the injection array and the shared reset line to their inert values, and every head then ORs or passes itself in over that release. Placed after a head it wipes that head's own assertion in the same scan it was made."),
        new("FC_HarnessStimArbiter", "FB_PusherStim", "same release, same reason."),
        new("FC_HarnessStimArbiter", "FB_ShredderSequencerStim", "same release, same reason."),
        new("FC_HarnessStimArbiter", "FC_Inputs", "the release must land before the map reads the array it releases, or the map reads the previous scan's injection."),
        new("FB_HopperBlockageStim", "FB_PusherStim", "the hopper head DRIVES test members 5, 6 and 8 and the reset line low throughout its trailing cleardown, a window whose own condition is NOT Running - so it asserts while reading as idle, and the pass-through contract does not hold for it. It must run where a genuinely running index overwrites it. RESTORED after being dropped: the arbiter's release makes pass-through order-independent for a head that is idle, and this head is the one that is not."),
        new("FB_HopperBlockageStim", "FB_ShredderSequencerStim", "same window, same three lines, same reason. The pusher and shredder heads are deliberately NOT ordered against each other - neither asserts while idle, so between those two the order is free."),
        new("FB_HopperBlockageStim", "FC_Inputs", "for a commanded input the head IS the field: the input map chooses between the physical terminal and the test member, and it must find this scan's commanded state, not the previous scan's."),
        new("FB_PusherStim", "FC_Inputs", "same reason."),
        new("FB_ShredderSequencerStim", "FC_Inputs", "same reason."),
        new("FB_PusherStim", "FC_ControlMain", "FC_ControlMain copies DB_Controls and DB_Input into the block under test's interface, so the head's commands must already be in place when it runs."),
        new("FC_ControlMain", "FC_HarnessCopyLayer", "the copy layer publishes the observations this scan's control produced."),
        new("FC_HarnessCopyLayer", "FB_Comms_ModbusServer", "the client must be served a mirror this scan wrote, not one being written while it is read."),
    };

    var naming = new CyclicObNaming(
        "Main", 1, "ProgramCycle", "Main Program Sweep (Cycle)",
        "Cyclic program sweep. The order of these calls is the program: commanded stimulus heads first, "
        + "because for a commanded input a head is the field and the input map below must find this scan's "
        + "commanded state; then the input map, the control, the alarms and the output map; then the harness "
        + "instrumentation, which observes what this scan's control produced; then the Modbus server, last, "
        + "so the whole wire-to-marker-memory transfer happens with no other block touching those "
        + "registers in between. THE ORDER OF THE THREE HEADS IS LOAD-BEARING AND MUST NOT BE CHANGED "
        + "WITHOUT READING THIS. The heads share the injection array and the plant reset line, and the "
        + "release above gives both a rest value each scan so that a head which is not running can hand a "
        + "neighbour's value on instead of driving a default of its own. TWO OF THE THREE HEADS DO EXACTLY "
        + "THAT AND THE FIRST DOES NOT: FB_HopperBlockageStim runs a trailing cleardown AFTER its own index "
        + "has finished - a window defined as CleardownPending AND NOT Start AND NOT Running, so it is "
        + "asserted precisely when that head reads as idle - and throughout it that head DRIVES test members "
        + "5, 6 and 8 and the reset line low rather than passing them through. The pusher and shredder heads "
        + "write three of those same four lines. So the hopper head is placed FIRST, where a head whose "
        + "index is genuinely running overwrites that drive; placed after either of the others it would wipe "
        + "a running index's commanded plant for about five seconds, and no gate would say so. THAT "
        + "PLACEMENT IS ALSO WHAT MAKES THE MULTI-WRITER ON THE RESET LINE SOUND: four blocks write it - the "
        + "release, then the three heads - and they are safe only because they are TOTALLY ORDERED here, "
        + "each later writer carrying the earlier value forward as its own pass-through term, with the only "
        + "consumer running after all four. Reorder them and that argument does not survive. What is NOT "
        + "ordered, deliberately, is the pusher head against the shredder head: neither asserts anything "
        + "while its own index is idle, so between those two either order gives the same program. One "
        + "scheduling consequence follows and is worth knowing before a multi-slot wave is timed: a hopper "
        + "trailing cleardown that overlaps another slot's index is overwritten by it, so that cleardown "
        + "does not happen - the heads must be given the trailing window between slots rather than being "
        + "started back to back. "
        + "Each network's own comment carries the reason it sits where it sits.");

    // Staged emission, for the invariance CHAIN only. `converter diff --insert` declares ONE
    // insertion point and takes the LAST value when repeated, so three insertions cannot be
    // declared in one call. Each stage omits some blocks so every hop is a single declared
    // insertion; the stage that omits nothing is the real Main.
    var omit = args.Length > 2
        ? args[2].Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    var outPath = args.Length > 3 ? args[3] : Path.Combine(ir, "Main.ir");

    calls = calls.Where(c => !omit.Contains(c.BlockName)).ToList();
    presence = presence.Where(x => !omit.Contains(x.BlockName)).ToList();
    order = order.Where(o => !omit.Contains(o.Earlier) && !omit.Contains(o.Later)).ToList();

    var result = CyclicObGenerator.Generate(naming, calls, presence, order);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, result.Ir.ReplaceLineEndings(Environment.NewLine));
    Console.WriteLine($"OB {result.BlockName}: calls = {string.Join(", ", result.CalledBlocks)}");
    return 0;
}

Console.Error.WriteLine("modes: shell | udt | idb | ob");
return 2;
