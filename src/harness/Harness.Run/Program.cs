using Harness.Run;

// The composition root, and nothing else. Every decision lives in LoopCli so it is testable without a
// process — a decision only reachable through a process is a decision nobody tests. The transport
// factory is supplied here and nowhere else, which is why no test in this repo can open a socket.
return LoopCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText);
