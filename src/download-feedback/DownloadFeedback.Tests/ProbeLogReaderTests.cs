using System;
using System.Linq;

namespace Ladder.Download.Tests
{
    public sealed class ProbeLogReaderTests
    {
        [Fact]
        public void A_log_with_no_result_section_reads_as_absent_not_as_empty()
        {
            var summary = ProbeLogReader.Read(Fixture.Read(Fixture.AbortedNoDownloadResult));

            Assert.False(summary.IsPresent);
            Assert.Empty(summary.Messages);
        }

        [Fact]
        public void Empty_and_null_input_read_as_absent()
        {
            Assert.False(ProbeLogReader.Read(null).IsPresent);
            Assert.False(ProbeLogReader.Read(string.Empty).IsPresent);
            Assert.False(ProbeLogReader.Read("nothing to see here").IsPresent);
        }

        [Fact]
        public void The_result_level_state_and_counts_are_read()
        {
            var summary = ProbeLogReader.Read(Fixture.Read(Fixture.HardwareThreeWordings));

            Assert.True(summary.IsPresent);
            Assert.Equal("Success", summary.State);
            Assert.Equal(0, summary.ErrorCount);
            Assert.Equal(0, summary.WarningCount);
        }

        [Fact]
        public void Nesting_indentation_becomes_tree_structure()
        {
            var summary = ProbeLogReader.Read(Fixture.Read(Fixture.HardwareThreeWordings));

            var root = Assert.Single(summary.Messages);
            Assert.Equal("PLC_1", root.Text);

            var group = Assert.Single(root.Children);
            Assert.Equal("Hardware configuration", group.Text);
            Assert.Equal(5, group.Children.Count);
        }

        [Fact]
        public void Per_node_state_and_timestamp_survive_the_read()
        {
            var summary = ProbeLogReader.Read(Fixture.Read(Fixture.UpToDateNothingTransferred));

            var root = Assert.Single(summary.Messages);
            Assert.Equal("Information", root.State);

            var child = Assert.Single(root.Children);
            Assert.Equal("Information", child.State);
            Assert.Equal(
                DateTimeOffset.Parse("2026-08-12T01:05:01.7714559Z", System.Globalization.CultureInfo.InvariantCulture),
                child.Timestamp);
        }

        [Fact]
        public void A_none_placeholder_for_absent_children_is_not_read_as_a_message()
        {
            // The older log format printed a literal "(none)" where a node had no children. It is
            // not a message and must not become one.
            var summary = ProbeLogReader.Read(Fixture.Read(Fixture.UpToDateNothingTransferred));

            var child = summary.Messages.Single().Children.Single();
            Assert.Empty(child.Children);
            Assert.DoesNotContain("(none)", child.Text);
        }

        [Fact]
        public void Reading_stops_at_the_next_section_header()
        {
            var summary = ProbeLogReader.Read(Fixture.Read(Fixture.FullNinetyNineObjects));

            // 1 root + 99 loads + a stop + a start, and nothing from whatever followed the section.
            var total = Count(summary.Messages);
            Assert.Equal(102, total);
        }

        [Fact]
        public void Both_line_ending_conventions_are_accepted()
        {
            var text = Fixture.Read(Fixture.DifferentialOneObject).Replace("\r\n", "\n");

            var lf = DownloadFeedbackParser.Parse(ProbeLogReader.Read(text));
            var crlf = DownloadFeedbackParser.Parse(ProbeLogReader.Read(text.Replace("\n", "\r\n")));

            Assert.Equal(1, lf.LoadedObjectCount);
            Assert.Equal(lf.LoadedObjectCount, crlf.LoadedObjectCount);
        }

        private static int Count(System.Collections.Generic.IReadOnlyList<DownloadMessageNode> nodes) =>
            nodes.Count + nodes.Sum(n => Count(n.Children));
    }
}
