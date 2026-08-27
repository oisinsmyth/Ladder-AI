using Harness.Run;

// The composition root, and nothing else. Every decision lives in LoopCli so it is testable without a
// process — a decision only reachable through a process is a decision nobody tests. The transport
// factory is supplied here and nowhere else, which is why no test in this repo can open a socket.
// readBytes is supplied so this path re-hashes a byte-stamped provenance record the same way
// harness-gate does. Both composition roots must pass it or the two gates disagree, and the loop is
// the one that spends rig time.
return LoopCli.Run(args, Console.Out, File.ReadAllText, WriteFile, readBytes: File.ReadAllBytes);

// 🔴 THE DIRECTORY IS CREATED HERE AND NOWHERE ELSE. `--emit <dir>` threw
// DirectoryNotFoundException for any directory that did not already exist — an unhandled exception, a
// stack trace, and a run whose whole report had already been printed above it. The fix belongs at the
// composition root rather than in LoopCli: the CLI writes through an injected `writeFile` precisely so
// no test touches a disk, and creating directories inside it would undo that. It became load-bearing
// when a stimulus shell fragment gained a SUBDIRECTORY of its own, which can never pre-exist.
static void WriteFile(string path, string content)
{
    if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
        Directory.CreateDirectory(directory);

    File.WriteAllText(path, content);
}
