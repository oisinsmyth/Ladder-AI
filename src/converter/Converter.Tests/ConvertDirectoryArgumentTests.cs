using System;
using System.IO;
using Xunit;

namespace Converter.Tests;

// `to-ir <directory>` used to hand the directory itself to File.ReadAllText and die with an unhandled
// UnauthorizedAccessException before converting anything. A directory now stands for the files in it
// that the mode reads (*.xml for to-ir).
public class ConvertDirectoryArgumentTests
{
    [Fact]
    public void ToIr_OnADirectory_ConvertsItsXmlFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "converter-dirarg-" + Guid.NewGuid().ToString("N"));
        var input = Directory.CreateDirectory(Path.Combine(root, "in")).FullName;
        var output = Directory.CreateDirectory(Path.Combine(root, "out")).FullName;
        try
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !Directory.Exists(Path.Combine(repo.FullName, "simatic-ml")))
            {
                repo = repo.Parent;
            }

            File.Copy(
                Path.Combine(repo!.FullName, "simatic-ml", "reference", "TimerSample.xml"),
                Path.Combine(input, "TimerSample.xml"));

            var exit = Program.RunConvert(new[] { "to-ir", input, "--out", output });

            Assert.Equal(0, exit);
            Assert.True(File.Exists(Path.Combine(output, "TimerSample.ir")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
