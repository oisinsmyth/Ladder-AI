using DeviceGuard;

// Entry point: delegate to the testable Cli.Run. Environment lookup is injected so the CLI can be
// exercised in tests without touching real process environment variables.
return Cli.Run(args, Environment.GetEnvironmentVariable);
