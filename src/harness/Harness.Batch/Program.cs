using Harness.Batch;

// The composition root, and it decides NOTHING. Every decision lives in BatchCli so it is testable
// without a process — a decision only reachable through a process is a decision nobody tests.
return BatchCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText);
