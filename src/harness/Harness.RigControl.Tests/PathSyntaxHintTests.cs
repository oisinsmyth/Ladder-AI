using Harness.RigControl;

namespace Harness.RigControl.Tests;

/// <summary>
/// The <c>%VAR%</c> diagnostic — and, just as deliberately, everything it must stay silent about.
///
/// <para>*** A DIAGNOSTIC THAT FIRES ON WORKING INPUT IS NOISE, AND NOISE GETS SWITCHED OFF *** —
/// after which the cases it was right about go through unhelped. So the quiet cases are tested as
/// carefully as the firing one, and the most important of them is a path that CONTAINS a '%' and
/// RESOLVES: '%' is a legal character in a Windows filename, and that is somebody's real directory.</para>
/// </summary>
public class PathSyntaxHintTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ladder-hint-" + Guid.NewGuid().ToString("N"));

    public PathSyntaxHintTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------- it fires

    [Fact]
    public void The_exact_path_the_owner_pasted_produces_the_hint()
    {
        var hint = PathSyntaxHint.For(@"%USERPROFILE%\.ladder\device-allowlist.json", exists: false);

        Assert.Contains("%USERPROFILE%", hint);
        Assert.Contains("cmd/batch variable syntax", hint);
        Assert.Contains("PowerShell does NOT expand it", hint);
    }

    /// <summary>
    /// The advice is a CLAIM, so the form it prints is the form that was run — see the README's live
    /// evidence. Pinning it here means a later reword cannot quietly change the incantation.
    /// </summary>
    [Fact]
    public void It_prints_the_powershell_form_quoted_with_the_rest_of_the_path_intact()
    {
        var hint = PathSyntaxHint.For(@"%USERPROFILE%\.ladder\device-allowlist.json", exists: false);

        Assert.Contains(@"""$env:USERPROFILE\.ladder\device-allowlist.json""", hint);
    }

    /// <summary>It repairs nothing, and it says so where the reader is standing.</summary>
    [Fact]
    public void It_states_that_nothing_was_expanded_for_you()
    {
        var hint = PathSyntaxHint.For(@"%APPDATA%\x.json", exists: false);

        Assert.Contains("Nothing here expanded it for you", hint);
        Assert.Contains("$env:APPDATA", hint);
    }

    // ---------------------------------------------------------------- it stays quiet

    /// <summary>
    /// *** THE UNAFFECTED CASE, TESTED AS DELIBERATELY AS THE REFUSED ONE. *** An ordinary missing file
    /// is a missing file; volunteering shell advice about it would be the gate firing outside its scope.
    /// </summary>
    [Fact]
    public void An_ordinary_missing_path_gets_no_shell_advice()
    {
        Assert.Equal(string.Empty, PathSyntaxHint.For(@"C:\nope\device-allowlist.json", exists: false));
    }

    /// <summary>
    /// *** THE ONE THAT PROVES THE GATE IS ON EXISTENCE AND NOT ON THE '%'. *** A real directory whose
    /// name contains a percent-delimited word: the file resolves, so there is nothing wrong and nothing
    /// to say. Driven through the REAL filesystem, because the claim is about a real path shape.
    /// </summary>
    [Fact]
    public void A_path_that_contains_a_percent_variable_and_ACTUALLY_RESOLVES_is_left_alone()
    {
        var odd = Path.Combine(_dir, "%TEMP%-not-a-variable");
        Directory.CreateDirectory(odd);
        var file = Path.Combine(odd, "allowlist.json");
        File.WriteAllText(file, "{}");

        Assert.Contains("%TEMP%", file);
        Assert.True(File.Exists(file), "the fixture must really exist or this test proves nothing.");
        Assert.Equal(string.Empty, PathSyntaxHint.For(file));
    }

    /// <summary>
    /// A file that EXISTS but is malformed is a different problem, and the hint must not attach itself
    /// to it — the substitution plainly worked.
    /// </summary>
    [Fact]
    public void An_existing_file_never_gets_the_hint_whatever_its_name()
    {
        Assert.Equal(string.Empty, PathSyntaxHint.For(@"%USERPROFILE%\x.json", exists: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\my%20encoded%20name\a.json")]   // percent-encoding, not a variable
    [InlineData(@"C:\100%\a.json")]                  // a bare percent
    [InlineData(@"C:\%\a.json")]
    public void Nothing_that_is_not_a_cmd_variable_produces_a_hint(string? path)
    {
        Assert.Equal(string.Empty, PathSyntaxHint.For(path, exists: false));
    }

    // ---------------------------------------------------------------- it is wired in

    /// <summary>
    /// Through the CLI, because a diagnostic nothing routes to is a diagnostic nobody sees — and this
    /// asserts the OBSERVABLE OUTPUT, not that a helper returns a string.
    /// </summary>
    [Fact]
    public void The_cli_prints_the_hint_and_still_refuses_exactly_as_before()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var built = 0;

        var exit = RigControlCli.Run(
            new[] { "--run", "--target", "10.10.10.10", "--yes",
                    "--allowlist", @"%USERPROFILE%\.ladder\device-allowlist.json" },
            _ => null,
            () => { built++; return new NeverTouchedTransport(); },
            stdout, stderr);

        // The behaviour is unchanged: same gate, same exit code, no transport.
        Assert.Equal(RigControlCli.ExitRefused, exit);
        Assert.Equal(0, built);
        Assert.Contains("AllowlistUnusable", stdout.ToString());

        // And now it says why.
        Assert.Contains("cmd/batch variable syntax", stdout.ToString());
        Assert.Contains("$env:USERPROFILE", stdout.ToString());
    }

    /// <summary>
    /// The counterpart at the CLI level: an ordinary bad path refuses with no shell advice attached.
    /// Without this, a hint that fired on everything would pass every other test in this file.
    /// </summary>
    [Fact]
    public void The_cli_adds_nothing_to_an_ordinary_missing_allowlist()
    {
        var stdout = new StringWriter();

        var exit = RigControlCli.Run(
            new[] { "--run", "--target", "10.10.10.10", "--yes", "--allowlist", Path.Combine(_dir, "gone.json") },
            _ => null,
            () => new NeverTouchedTransport(),
            stdout, new StringWriter());

        Assert.Equal(RigControlCli.ExitRefused, exit);
        Assert.Contains("AllowlistUnusable", stdout.ToString());
        Assert.DoesNotContain("cmd/batch", stdout.ToString());
        Assert.DoesNotContain("$env:", stdout.ToString());
    }
}
