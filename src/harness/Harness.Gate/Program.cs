using Harness.Gate;

// The composition root, and nothing else. Every decision lives in GateCli / StampCli so it is testable
// without a process — a decision only reachable through a process is a decision nobody tests.
return args.Length > 0 && string.Equals(args[0], "stamp", StringComparison.Ordinal)
    ? StampCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText)
    : args.Length > 0 && string.Equals(args[0], "derive", StringComparison.Ordinal)
        ? DeriveCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText)
        : GateCli.Run(args, Console.Out, File.ReadAllText);
