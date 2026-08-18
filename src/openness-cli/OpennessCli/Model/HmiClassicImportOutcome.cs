using System.Collections.Generic;

namespace OpennessCli.Model;

/// <summary>
/// What a CLASSIC HMI tag-table or text-list import actually did, MEASURED BY RE-READING THE
/// PROJECT — not by what <c>Import()</c> returned and not by an exit code.
///
/// Both counts are here because they answer different questions and this project has been burned by
/// conflating them:
/// <list type="bullet">
///   <item><c>ContainersBefore/After</c> — how many tag tables (or text lists) the device holds. This
///   is what moves when an import CREATES a table.</item>
///   <item><c>MembersBefore/After</c> — how many tags the device holds across every table. This is
///   what moves when an import ADDS TO an existing table, and it is the ONLY number that can tell
///   REPLACE from MERGE: a table whose member count went from 40 to 41 was merged into; one that
///   went from 40 to 1 was replaced.</item>
/// </list>
///
/// <c>MembersBefore/After</c> are NULLABLE and null means "the API exposes no member composition",
/// which is the literal state for a classic <c>TextList</c>: <c>Siemens.Engineering.Hmi
/// .TextGraphicList.TextList</c> has <c>Name</c>, <c>Parent</c>, <c>Export</c>, <c>Delete</c> and the
/// attribute bag, and NO entries collection at all. A zero there would be a claim about the project;
/// null is a claim about the API, and they are not the same finding. (Empty is not clean — the same
/// rule that makes an unexamined count a reportable outcome rather than a pass.)
/// </summary>
public sealed record HmiClassicImportOutcome(
    string Kind,
    string DevicePath,
    int ContainersBefore,
    int ContainersAfter,
    int? MembersBefore,
    int? MembersAfter,
    IReadOnlyList<string> ReturnedByImport,
    IReadOnlyList<string> PresentAfter,
    IReadOnlyList<string> Files);
