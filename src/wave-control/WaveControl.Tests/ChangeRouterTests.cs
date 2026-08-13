using System;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>D23 — changes are routed into two queues, and anything that cannot be is refused.</summary>
    public sealed class ChangeRouterTests
    {
        [Theory]
        [InlineData(ChangeClass.Run)]
        [InlineData(ChangeClass.RunInit)]
        public void Run_class_changes_flow_through_a_wave_boundary(ChangeClass changeClass)
        {
            var verdict = ChangeRouter.Route(new ChangedObject("FB_Motor", ObjectKind.FunctionBlock, changeClass));

            Assert.True(verdict.Routed);
            Assert.Equal(DownloadQueue.RunQueue, verdict.Queue);
            Assert.False(verdict.Deferred);
        }

        [Theory]
        [InlineData(ObjectKind.OrganizationBlock)]
        [InlineData(ObjectKind.TextList)]
        [InlineData(ObjectKind.HardwareConfiguration)]
        [InlineData(ObjectKind.RetentivitySetting)]
        [InlineData(ObjectKind.FunctionBlock)]
        public void Stop_class_changes_accumulate_in_the_deferred_queue(ObjectKind kind)
        {
            var verdict = ChangeRouter.Route(new ChangedObject("thing", kind, ChangeClass.Stop));

            Assert.True(verdict.Routed);
            Assert.Equal(DownloadQueue.DeferredQueue, verdict.Queue);
            Assert.True(verdict.Deferred);
        }

        [Fact]
        public void An_unclassified_change_is_refused_and_not_quietly_deferred()
        {
            // The tempting alternative is to file it in the deferred queue on the grounds that a drain
            // pays for a stop anyway. That launders "nobody worked it out" into "this is STOP-class",
            // and the log then records a decision nobody made.
            var verdict = ChangeRouter.Route(new ChangedObject("FB_Mystery", ObjectKind.FunctionBlock, ChangeClass.Unknown));

            Assert.False(verdict.Routed);
            Assert.Equal(RoutingRefusal.UnknownChangeClass, verdict.Refusal);
            Assert.Equal(DownloadQueue.Unassigned, verdict.Queue);
        }

        [Fact]
        public void An_object_that_does_not_say_what_kind_it_is_is_refused()
        {
            // Without a kind the fixed-class cross-check cannot be applied, so an OB declared RUN would
            // pass simply by declining to say it is an OB.
            var verdict = ChangeRouter.Route(new ChangedObject("something", ObjectKind.Unknown, ChangeClass.Run));

            Assert.False(verdict.Routed);
            Assert.Equal(RoutingRefusal.UnknownObjectKind, verdict.Refusal);
        }

        [Fact]
        public void An_unnamed_object_is_refused()
        {
            var verdict = ChangeRouter.Route(new ChangedObject("   ", ObjectKind.FunctionBlock, ChangeClass.Run));

            Assert.False(verdict.Routed);
            Assert.Equal(RoutingRefusal.UnnamedObject, verdict.Refusal);
        }

        [Theory]
        [InlineData(ObjectKind.OrganizationBlock, ChangeClass.Run)]
        [InlineData(ObjectKind.OrganizationBlock, ChangeClass.RunInit)]
        [InlineData(ObjectKind.TextList, ChangeClass.Run)]
        [InlineData(ObjectKind.HardwareConfiguration, ChangeClass.Run)]
        [InlineData(ObjectKind.RetentivitySetting, ChangeClass.RunInit)]
        public void A_kind_whose_class_the_spec_fixes_is_refused_when_declared_otherwise(ObjectKind kind, ChangeClass declared)
        {
            // Refused, NOT silently corrected. A classifier that got this wrong is wrong about more than
            // the four kinds this table covers, and correcting the symptom hides that.
            var verdict = ChangeRouter.Route(new ChangedObject("thing", kind, declared));

            Assert.False(verdict.Routed);
            Assert.Equal(RoutingRefusal.ContradictsTheFixedClassForThatKind, verdict.Refusal);
            Assert.Contains(kind.ToString(), verdict.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void Every_fixed_kind_names_the_rule_that_fixes_it()
        {
            // The same pin ClassAEntry puts on its allowance list: an entry nobody had to justify is how
            // something wrong gets onto a table that everything else trusts.
            Assert.NotEmpty(ChangeRouter.KindsWithAFixedStopClass);

            foreach (var entry in ChangeRouter.KindsWithAFixedStopClass)
            {
                Assert.True(
                    entry.Value.Contains("R1") || entry.Value.Contains("DB-1"),
                    entry.Key + " cites no rule for its fixed class; it carries '" + entry.Value + "'.");
            }
        }

        [Fact]
        public void The_zero_values_are_the_ones_that_authorise_nothing()
        {
            Assert.Equal(ChangeClass.Unknown, default(ChangeClass));
            Assert.Equal(ObjectKind.Unknown, default(ObjectKind));
            Assert.Equal(DownloadQueue.Unassigned, default(DownloadQueue));
            Assert.Equal(RoutingRefusal.None, default(RoutingRefusal));
        }

        [Fact]
        public void A_self_reference_is_not_a_dependency()
        {
            var o = new ChangedObject("FB_A", ObjectKind.FunctionBlock, ChangeClass.Run, new[] { "FB_A", "fb_a", "UDT_A", " " });

            Assert.Equal(new[] { "UDT_A" }, o.DependsOn.ToArray());
        }
    }
}
