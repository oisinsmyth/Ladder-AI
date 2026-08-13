namespace Harness.Device.Tests;

/// <summary>
/// Windows argument quoting. It is here because the one argument that MUST survive intact is the
/// group path — <c>PLC1 6ES7 214-1AG40-0XB0/Program blocks</c> — and a shortened or split one fails
/// with a message that does not point at the mismatch.
/// </summary>
public class CommandLineTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has space", "\"has space\"")]
    [InlineData("", "\"\"")]
    [InlineData(@"C:\path\to\file", @"C:\path\to\file")]
    [InlineData(@"C:\path with space\", "\"C:\\path with space\\\\\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    public void Quoting_follows_CreateProcess_rules(string argument, string expected)
    {
        Assert.Equal(expected, CommandLine.Quote(argument));
    }

    [Fact]
    public void A_group_path_with_spaces_and_an_article_number_survives_as_ONE_argument()
    {
        var rendered = CommandLine.Render(@"C:\bin\openness-cli.exe",
            new[] { "import-all", @"D:\x\Thing scratch.ap20", "--group", "PLC1 6ES7 214-1AG40-0XB0/Program blocks" });

        // The exe path carries no space, so it is rendered bare — only what NEEDS quoting gets it.
        Assert.Equal(
            "C:\\bin\\openness-cli.exe import-all \"D:\\x\\Thing scratch.ap20\" --group \"PLC1 6ES7 214-1AG40-0XB0/Program blocks\"",
            rendered);
    }

    [Fact]
    public void An_executable_path_containing_a_space_is_quoted_too()
    {
        Assert.StartsWith(
            "\"C:\\Program Files\\bin\\openness-cli.exe\" list",
            CommandLine.Render(@"C:\Program Files\bin\openness-cli.exe", new[] { "list" }),
            StringComparison.Ordinal);
    }
}
