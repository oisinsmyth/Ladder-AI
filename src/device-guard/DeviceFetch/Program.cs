using DeviceFetch;

// Entry point: real HTTP getter, real environment. Both are injected into FetchCli so the whole
// flow is testable with fakes and never touches a real device or real env vars under test.
return await FetchCli.RunAsync(
    args,
    Environment.GetEnvironmentVariable,
    () => new HttpGetter());
