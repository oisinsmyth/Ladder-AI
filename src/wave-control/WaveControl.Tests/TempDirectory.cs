using System;
using System.IO;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// A scratch directory per test. The marker is a file and its behaviour IS filesystem behaviour,
    /// so these tests use a real one rather than an abstraction that would let the interesting cases
    /// (a torn write, a locked handle) be modelled instead of exercised.
    /// </summary>
    internal sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ladder-wave-tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leaked scratch directory in %TEMP% is not worth failing a test over.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
