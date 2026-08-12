using System;
using System.IO;

namespace Ladder.Download.Tests
{
    internal static class Fixture
    {
        public const string DifferentialOneObject = "differential-one-object.txt";
        public const string FullNinetyNineObjects = "full-ninety-nine-objects.txt";
        public const string HardwareThreeWordings = "hardware-three-wordings.txt";
        public const string UpToDateNothingTransferred = "up-to-date-nothing-transferred.txt";
        public const string AbortedNoDownloadResult = "aborted-no-download-result.txt";
        public const string UnrecognisedVocabulary = "unrecognised-vocabulary-SYNTHETIC.txt";

        public static string Read(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Fixture '" + name + "' was not copied to the output directory. It is the evidence " +
                    "these tests exist to pin, so a missing one is a failure, not a skip.",
                    path);
            }

            return File.ReadAllText(path);
        }

        public static DownloadFeedback Parse(string name) =>
            DownloadFeedbackParser.Parse(ProbeLogReader.Read(Read(name)));
    }
}
