using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Ladder.Converter.Leases;
using global::Converter.Tests;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// 🔴 <b>FI-24, pinned — and NARROWED, deliberately and in one place.</b>
///
/// <para>The invariant is asserted in prose in five files: <i>"the converter is a pure in-process file
/// transformer and never shells out or touches the environment"</i>. It is the reason the confirm loop
/// lives in a PowerShell script rather than a subcommand, and the reason <c>export-all</c> lives in
/// <c>openness-cli</c>. Until now <b>nothing enforced it</b>, and it had already been dented without
/// anyone noticing: <see cref="LeaseStore"/> reads a process's start time, which is the converter's
/// first and only contact with <see cref="Process"/>, and it arrived unremarked in a commit about
/// leases.</para>
///
/// <para><b>The narrowing, stated so it is a decision and not an erosion:</b> the converter starts NO
/// child process and mutates NO environment. Reading the start time of a pid it was handed is
/// permitted, because lease liveness cannot be decided without it and the alternative — having the
/// caller assert "the holder is alive" — is a claim nobody can check, made by the party with the
/// motive to get it wrong.</para>
///
/// <para><b>Why an IL walk rather than a grep.</b> A grep over source sees comments, strings and this
/// very file; it does not see a call reached through an alias, an extension method or a generic. The
/// walk resolves real operand tokens in compiled method bodies, and it is the same scanner
/// <c>JoinSiteWalkTests</c> uses — including its rule that an opcode the decoder does not recognise is
/// REPORTED rather than skipped, so precision is not bought with silent gaps.</para>
/// </summary>
public sealed class ConverterProcessInvariantTests
{
    private static Assembly ConverterAssembly => typeof(LeaseStore).Assembly;

    /// <summary>Anything that would START a process, or reach outside to run one.</summary>
    private static bool StartsAProcess(MemberInfo member)
    {
        var declaring = member.DeclaringType;
        if (declaring is null)
        {
            return false;
        }

        if (declaring == typeof(Process))
        {
            // Start in every overload, plus the two that run one indirectly.
            return member.Name is "Start" or "BeginOutputReadLine" or "BeginErrorReadLine";
        }

        // Constructing one of these is only ever a prelude to starting it.
        return declaring == typeof(ProcessStartInfo) && member is ConstructorInfo;
    }

    [Fact]
    public void The_converter_assembly_STARTS_NO_PROCESS()
    {
        var scan = IlWalkScanner.Scan(ConverterAssembly, StartsAProcess);

        Assert.True(scan.Undecodable.Count == 0,
            "the walk could not decode " + scan.Undecodable.Count + " method body/bodies, so it cannot claim to have "
            + "examined the assembly: " + string.Join("; ", scan.Undecodable.Take(5)));

        Assert.True(scan.Hits.Count == 0,
            "*** THE CONVERTER MUST NOT START A PROCESS (FI-24). *** Orchestration belongs in openness-cli or a script. "
            + "Found:\n  " + string.Join("\n  ", scan.Hits));
    }

    /// <summary>
    /// <b>The denominator.</b> A walk that enumerated nothing would satisfy the assertion above
    /// perfectly, and that is the failure this repository keeps meeting under other names.
    /// </summary>
    [Fact]
    public void The_walk_actually_examined_the_assembly()
    {
        var scan = IlWalkScanner.Scan(ConverterAssembly, StartsAProcess);

        Assert.True(scan.TypesEnumerated > 100, $"only {scan.TypesEnumerated} types were enumerated.");
        Assert.True(scan.BodiesExamined > 1000, $"only {scan.BodiesExamined} method bodies were examined.");
    }

    /// <summary>
    /// 🔴 <b>The positive control.</b> Without it, the green above is equally consistent with a predicate
    /// that never matches anything — and a guard that cannot fire is not a guard. This aims the same
    /// walk at something the converter demonstrably DOES call, and requires it to be found.
    /// </summary>
    [Fact]
    public void The_walk_finds_a_call_the_converter_really_does_make()
    {
        var scan = IlWalkScanner.Scan(
            ConverterAssembly,
            member => member.DeclaringType == typeof(Process) && member.Name == "GetProcessById");

        Assert.True(scan.Hits.Count > 0,
            "the walk found no call to Process.GetProcessById, which LeaseStore.LiveProcessStart makes — so the walk is "
            + "not seeing what it is pointed at, and the no-Process.Start result above proves nothing.");
    }

    /// <summary>
    /// The permitted contact, named. If lease liveness ever stops needing it, this test fails and the
    /// narrowing above should be withdrawn rather than left standing as licence for the next one.
    /// </summary>
    [Fact]
    public void The_ONLY_permitted_contact_with_Process_is_reading_a_pids_start_time()
    {
        var everyProcessMember = IlWalkScanner.Scan(
            ConverterAssembly,
            member => member.DeclaringType == typeof(Process));

        var names = everyProcessMember.Hits
            .Select(hit => hit.Contains("GetProcessById", StringComparison.Ordinal) ? "GetProcessById"
                : hit.Contains("StartTime", StringComparison.Ordinal) ? "StartTime"
                : hit)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            names.All(n => n is "GetProcessById" or "StartTime"),
            "the converter touches System.Diagnostics.Process in a way FI-24's narrowing does not cover. "
            + "Permitted: GetProcessById + StartTime, for lease liveness only. Found:\n  " + string.Join("\n  ", names));
    }
}
