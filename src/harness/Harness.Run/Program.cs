using Harness.Run;

// The composition root, and nothing else. Every decision lives in LoopCli so it is testable without a
// process — a decision only reachable through a process is a decision nobody tests. The transport
// factory is supplied here and nowhere else, which is why no test in this repo can open a socket.
// readBytes is supplied so this path re-hashes a byte-stamped provenance record the same way
// harness-gate does. Both composition roots must pass it or the two gates disagree, and the loop is
// the one that spends rig time.
return LoopCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText, readBytes: File.ReadAllBytes);
