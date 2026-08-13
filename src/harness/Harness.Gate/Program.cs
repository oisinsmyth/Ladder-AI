using Harness.Gate;

// The composition root, and nothing else. Every decision lives in GateCli so it is testable without a
// process — a decision only reachable through a process is a decision nobody tests.
return GateCli.Run(args, Console.Out, File.ReadAllText);
