using Harness.Gate;

// The composition root, and nothing else. Every decision lives in GateCli / StampCli so it is testable
// without a process — a decision only reachable through a process is a decision nobody tests.
return args.Length > 0 && string.Equals(args[0], "stamp", StringComparison.Ordinal)
    ? StampCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText)
    : args.Length > 0 && string.Equals(args[0], "compress", StringComparison.Ordinal)
        // Emits the scaled preset table for a deploy to apply. It touches no device and edits no IR:
        // what it produces is a FILE, so the deploy and the wave can be shown to mean the same factor.
        ? CompressCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText)
    : args.Length > 0 && string.Equals(args[0], "derive", StringComparison.Ordinal)
        // File.ReadAllBytes is passed so the artifact hash is taken over BYTES rather than over text as
        // read - the stronger form, and the default wherever a real filesystem is available. The text
        // path remains for callers that only have a text reader, and the record says which was used.
        ? DeriveCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText, File.ReadAllBytes)
        // The same byte reader on the gate side: a record stamped over bytes must be RE-hashed over
        // bytes, or every derived field reads as stale for a reason nothing to do with the submission.
        : GateCli.Run(args, Console.Out, File.ReadAllText, File.ReadAllBytes);
