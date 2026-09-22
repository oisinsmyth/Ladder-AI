<#
.SYNOPSIS
    Tests for the confirm loop's SCRATCH FENCE (ADR-0011). Runs entirely offline - it never contacts
    Portal, and proving that is half of what it is for.

.DESCRIPTION
    This project produced five guards in one week that were written, believed, and never executed.
    A fence is a guard. So this suite negative-tests it: every refusal case asserts the exit code AND
    that openness-cli was never launched, and there are POSITIVE cases too, because a fence that
    refuses everything is as broken as one that refuses nothing - it would just fail more quietly.

    *** HOW "PORTAL WAS NEVER CONTACTED" IS ASSERTED, NOT ASSUMED. ***
    confirm-roundtrip.ps1's ONLY route to Portal is Start-Process $OpennessCliPath, inside
    Invoke-Stage, at stages 1/4/5/6 and nowhere else (grep the script: openness-cli appears in no
    other executable position). So the tests hand it a STUB openness-cli - a .cmd that appends to a
    sentinel file and exits 0 - and assert the sentinel does not exist. That is a direct observation
    that the process was never launched, not an inference from a log line or an exit code.

    The instrument is itself controlled: Fence_PermittedProject_ReachesStage1 asserts the sentinel
    IS written on a permitted run. Without that, "sentinel absent" could equally mean the sentinel
    never worked, which is the shape of bug this whole suite exists to avoid.

    ASCII only, CRLF - same reason as the script under test. Windows PowerShell 5.1 reads a BOM-less
    .ps1 as ANSI, so a UTF-8 em dash decodes to a character the parser treats as a QUOTE DELIMITER,
    and the resulting error points a hundred lines away from the real one.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\confirm-roundtrip-fence.tests.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$scriptUnderTest = Join-Path $PSScriptRoot 'confirm-roundtrip.ps1'
$allowlistUnderTest = Join-Path $PSScriptRoot 'confirm-roundtrip.allowlist'
$repoRoot = Split-Path -Parent $PSScriptRoot

$EXIT_STAGEFAILED = 3
$EXIT_REFUSED     = 4
$EXIT_DRYRUN      = 10

$script:Pass = 0
$script:Fail = 0
$script:Failures = @()

function Test-Case {
    param([string]$Name, [scriptblock]$Body)

    try {
        & $Body
        Write-Host ("  PASS  {0}" -f $Name)
        $script:Pass++
    } catch {
        Write-Host ("  FAIL  {0}" -f $Name)
        Write-Host ("        {0}" -f $_.Exception.Message)
        $script:Fail++
        $script:Failures += $Name
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$What)
    if ($Expected -ne $Actual) {
        throw "$What : expected '$Expected', got '$Actual'"
    }
}

function Assert-Contains {
    param([string]$Haystack, [string]$Needle, [string]$What)
    if ($Haystack -notmatch [regex]::Escape($Needle)) {
        throw "$What : output did not contain '$Needle'. Output was:`n$Haystack"
    }
}

# A workspace per case: stub binaries, a sentinel, and somewhere for -ScratchDir to land.
function New-Workspace {
    $dir = Join-Path ([System.IO.Path]::GetTempPath()) ("fence-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null

    $sentinel = Join-Path $dir 'openness-cli-was-launched.txt'

    # The stub stands in for openness-cli. If confirm-roundtrip.ps1 ever reaches a Portal stage it
    # launches THIS, and the sentinel appears. Exits 0 so a permitted run gets past the stage-1 call
    # and fails later for a different, visible reason.
    $stubCli = Join-Path $dir 'openness-cli.cmd'
    Set-Content -LiteralPath $stubCli -Encoding ASCII -Value @(
        '@echo off',
        "echo LAUNCHED %* >> ""$sentinel""",
        'exit /b 0')

    # The converter stub exists only so the binary-presence guard passes; the fence must refuse
    # before this could matter either way.
    $stubConverter = Join-Path $dir 'converter.cmd'
    Set-Content -LiteralPath $stubConverter -Encoding ASCII -Value @(
        '@echo off',
        'exit /b 0')

    return [pscustomobject]@{
        Dir        = $dir
        Sentinel   = $sentinel
        Cli        = $stubCli
        Converter  = $stubConverter
        ScratchDir = (Join-Path $dir 'scratch')
    }
}

function Invoke-Loop {
    param(
        [Parameter(Mandatory = $true)]$Workspace,
        [Parameter(Mandatory = $true)][string[]]$ExtraArgs,
        [string]$Script
    )

    if (-not $Script) { $Script = $scriptUnderTest }

    $all = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Script,
        '-Block', 'FB_Anything',
        '-ScratchDir', $Workspace.ScratchDir,
        '-ConverterPath', $Workspace.Converter,
        '-OpennessCliPath', $Workspace.Cli) + $ExtraArgs

    $output = & powershell.exe @all 2>&1 | Out-String
    return [pscustomobject]@{ Exit = $LASTEXITCODE; Output = $output }
}

function Assert-PortalNeverContacted {
    param($Workspace, [string]$What)
    if (Test-Path -LiteralPath $Workspace.Sentinel) {
        $contents = Get-Content -LiteralPath $Workspace.Sentinel | Out-String
        throw "$What : openness-cli WAS launched - Portal would have been contacted. Sentinel:`n$contents"
    }
}

# A project file that exists and is definitely not allowlisted. Synthetic on purpose: this machine
# carries about nineteen Private engineering projects that would serve, and none of their names may enter
# the repository (docs/13 - the boundary is retention, not access).
function New-NonScratchProject {
    param($Workspace, [string]$Name = 'NotTheScratchProject')
    $dir = Join-Path $Workspace.Dir $Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $file = Join-Path $dir "$Name.ap20"
    Set-Content -LiteralPath $file -Encoding ASCII -Value 'not a real project, a fence fixture'
    return $file
}

# Copies the script somewhere WITHOUT its allowlist, so the missing/empty/malformed allowlist cases
# can be exercised without touching the real one (which another lane may be relying on).
function New-IsolatedScriptCopy {
    param($Workspace, [string[]]$AllowlistLines)

    $dir = Join-Path $Workspace.Dir 'isolated'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $copy = Join-Path $dir 'confirm-roundtrip.ps1'
    Copy-Item -LiteralPath $scriptUnderTest -Destination $copy -Force

    if ($null -ne $AllowlistLines) {
        Set-Content -LiteralPath (Join-Path $dir 'confirm-roundtrip.allowlist') -Encoding ASCII -Value $AllowlistLines
    }

    return $copy
}

function Get-AllowlistedProjectPath {
    foreach ($line in (Get-Content -LiteralPath $allowlistUnderTest)) {
        $t = $line.Trim()
        if ($t.Length -eq 0 -or $t.StartsWith('#')) { continue }
        if ($t.StartsWith('repo:', [System.StringComparison]::OrdinalIgnoreCase)) {
            $candidate = Join-Path $repoRoot $t.Substring(5)
        } else {
            $candidate = $t
        }
        if (Test-Path -LiteralPath $candidate) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    return $null
}

Write-Host ''
Write-Host '=============================================================================='
Write-Host 'CONFIRM LOOP - SCRATCH FENCE TESTS (ADR-0011). Offline; Portal is never contacted.'
Write-Host '=============================================================================='
Write-Host ''

# Resolved once, up front, because both the refusal cases and the permit cases need it.
$allowlisted = Get-AllowlistedProjectPath
$allowlistedFolder = if ($allowlisted) { Split-Path -Parent $allowlisted } else { $null }

# ------------------------------------------------------------------- refusals (the fence firing)

Test-Case 'Fence_NonScratchProject_RefusesAndNeverContactsPortal' {
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-PortalNeverContacted $ws 'non-scratch project'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'REFUSED' 'refusal'
    Assert-Contains $r.Output 'ADR-0011' 'cites the ruling'
    Assert-Contains $r.Output 'NO OVERRIDE FLAG' 'says there is no way past it'
}

Test-Case 'Fence_BareProjectName_IsARefusalNotAGuess' {
    # openness-cli accepts a bare name for an already-open project, but a name cannot be resolved to
    # a canonical path - so the fence cannot identify it, and unidentifiable is a refusal (req 4).
    $ws = New-Workspace
    $r = Invoke-Loop $ws @('-Project', 'NoSuchProjectAnywhere', '-Group', 'Dev/Program blocks', '-Arm')

    Assert-PortalNeverContacted $ws 'bare project name'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'not a project file' 'reason'
    Assert-Contains $r.Output 'no such file exists' 'specific reason'
}

Test-Case 'Fence_ProjectFolderInsteadOfProjectFile_IsRefusedWithTheRightReason' {
    # This case is why the previous one changed. Run from the repo root, `-Project GenProject1`
    # resolves to the project FOLDER, which EXISTS - so an existence check alone passes and the
    # fence then refuses for a misleading reason. A guard that explains itself wrongly is how
    # someone concludes it is broken and looks for a way around it.
    if (-not $allowlistedFolder) { throw 'SETUP: no allowlist entry resolves to a file on this machine.' }

    $ws = New-Workspace
    $r = Invoke-Loop $ws @('-Project', $allowlistedFolder, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-PortalNeverContacted $ws 'project folder'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'it is a DIRECTORY, not a project file' 'reason names the actual problem'
}

Test-Case 'Fence_IsScratchProjectSwitch_RefusedByName' {
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm', '-IsScratchProject')

    Assert-PortalNeverContacted $ws '-IsScratchProject'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'no longer accepted' 'refused by name'
}

Test-Case 'Fence_MissingAllowlist_RefusesRatherThanPermitting' {
    # Empty is not clean (FI-44): a fence with no allowlist has verified nothing.
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $copy = New-IsolatedScriptCopy $ws $null
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm') -Script $copy

    Assert-PortalNeverContacted $ws 'missing allowlist'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'allowlist is MISSING' 'reason'
}

Test-Case 'Fence_EmptyAllowlist_RefusesRatherThanPermitting' {
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $copy = New-IsolatedScriptCopy $ws @('# every line a comment', '', '# and nothing else')
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm') -Script $copy

    Assert-PortalNeverContacted $ws 'empty allowlist'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'NO ENTRIES' 'reason'
}

Test-Case 'Fence_RelativeAllowlistEntry_IsRefusedAsAmbiguous' {
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $copy = New-IsolatedScriptCopy $ws @('GenProject1/GenProject1.ap20')
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm') -Script $copy

    Assert-PortalNeverContacted $ws 'relative allowlist entry'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'bare relative path' 'reason'
}

Test-Case 'Fence_AllowlistedNameInTheWrongDirectory_IsStillRefused' {
    # The string compare must be against the RESOLVED path, so a file merely NAMED like the scratch
    # project somewhere else on disk must not satisfy the fence (req 5).
    $ws = New-Workspace
    $project = New-NonScratchProject $ws 'GenProject1'
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-PortalNeverContacted $ws 'same-named project elsewhere'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output 'this is not one' 'reason'
}

# ------------------------------------------------------ permits (a fence that refuses everything
#                                                          is as broken as one that refuses nothing)

Test-Case 'Fence_PermittedProject_ReachesStage1' {
    if (-not $allowlisted) { throw 'SETUP: no allowlist entry resolves to a file on this machine, so the permit path cannot be exercised.' }

    $ws = New-Workspace
    $r = Invoke-Loop $ws @('-Project', $allowlisted, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-Contains $r.Output 'SCRATCH FENCE: permitted' 'fence verdict'

    # *** THE CONTROL FOR THE INSTRUMENT. *** The stub IS launched here, which is what makes its
    # absence meaningful in every refusal case above. The run then stops at stage 1 (exit 3) because
    # the stub exports no file - a stage failure, deliberately not an invariance finding.
    if (-not (Test-Path -LiteralPath $ws.Sentinel)) {
        throw 'the permitted run did not launch openness-cli, so the sentinel proves nothing in the refusal cases'
    }
    Assert-Equal $EXIT_STAGEFAILED $r.Exit 'exit code'
}

Test-Case 'Fence_PermittedProject_SurvivesCasingAndDotDot' {
    if (-not $allowlisted) { throw 'SETUP: no allowlist entry resolves to a file on this machine.' }

    # Same file, spelled so that a naive string compare would miss it: upper-cased, and routed
    # through a ".." that GetFullPath collapses (req 5).
    $dir = Split-Path -Parent $allowlisted
    $leaf = Split-Path -Leaf $allowlisted
    $viaDotDot = Join-Path (Join-Path $dir '..') (Join-Path (Split-Path -Leaf $dir) $leaf)
    $awkward = $viaDotDot.ToUpperInvariant()

    $ws = New-Workspace
    $r = Invoke-Loop $ws @('-Project', $awkward, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-Contains $r.Output 'SCRATCH FENCE: permitted' 'fence verdict on an awkwardly spelled path'
}

# ------------------------------------------------------------------------------- scope of the fence

Test-Case 'Fence_DryRunAgainstAnyProject_IsNotFencedAndStaysOffline' {
    # The fence guards the MUTATING half. A dry run writes nothing to the project, so it stays
    # available against anything - and with -FirstExport it does not contact Portal at all.
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $export = Join-Path $ws.Dir 'already-exported.xml'
    Set-Content -LiteralPath $export -Encoding ASCII -Value '<Document />'

    $r = Invoke-Loop $ws @('-Project', $project, '-FirstExport', $export)

    if ($r.Exit -eq $EXIT_REFUSED) {
        throw "a dry run was refused by the fence; the fence is meant to guard -Arm only. Output:`n$($r.Output)"
    }
    Assert-PortalNeverContacted $ws 'offline dry run'
}

# -------------------------------------------------- path forms the fence CLAIMS to handle
#
# Added by the fence-hammering campaign, 2026-08-14. The script's own header says a junction and an
# 8.3 short name are "a REFUSAL, not a pass", and NOTHING EXERCISED EITHER. An untested fence claim
# is the same family as a guard that was written and never executed - and the junction case is the
# sharp one, because the junction's TARGET really is the allowlisted project, so a half-resolution
# would look exactly like correct behaviour.

Test-Case 'Fence_JunctionToTheAllowlistedProject_IsRefusedNotHalfResolved' {
    if (-not $allowlistedFolder) { throw 'SETUP: no allowlist entry resolves to a file on this machine.' }

    $ws = New-Workspace
    $link = Join-Path $ws.Dir 'viaJunction'
    New-Item -ItemType Junction -Path $link -Target $allowlistedFolder -ErrorAction Stop | Out-Null
    try {
        $viaLink = Join-Path $link (Split-Path -Leaf $allowlisted)
        $r = Invoke-Loop $ws @('-Project', $viaLink, '-Group', 'Dev/Program blocks', '-Arm')

        Assert-PortalNeverContacted $ws 'junction to the allowlisted project'
        Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
        # The REASON matters as much as the verdict: refusing because the file looked absent would
        # pass this assertion for the wrong cause and would not be evidence about junctions at all.
        Assert-Contains $r.Output 'junction or symlink' 'reason names the reparse point'
    } finally {
        # Non-recursive: this removes the LINK, never anything behind it.
        if (Test-Path -LiteralPath $link) { [System.IO.Directory]::Delete($link) }
    }
}

Test-Case 'Fence_EightDotThreeLookingComponent_IsRefusedWithTheFixNamed' {
    # A real long directory name that merely LOOKS like an 8.3 alias. Refusing it is the fail-closed
    # direction and the right one, but it IS a false refusal, so it is pinned as a known cost rather
    # than discovered by somebody whose run stopped for no visible reason.
    $ws = New-Workspace
    $dir = Join-Path $ws.Dir 'Sand~1'
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $project = Join-Path $dir 'GenProject1.ap20'
    Set-Content -LiteralPath $project -Encoding ASCII -Value 'a project in a folder whose REAL name contains ~1'

    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-PortalNeverContacted $ws '8.3-looking component'
    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    Assert-Contains $r.Output '8.3 short name' 'reason'
    Assert-Contains $r.Output 'supply the long path' 'the fix is named'
}

Test-Case 'Fence_Refusal_CreatesNoScratchDirectory' {
    # The script's header claims the fence is evaluated "before any directory is created". That is a
    # claim about ORDER, and an exit code cannot distinguish a fence that ran first from one that ran
    # after a side effect - so it is observed directly, the same way the sentinel observes Portal.
    $ws = New-Workspace
    $project = New-NonScratchProject $ws
    $r = Invoke-Loop $ws @('-Project', $project, '-Group', 'Dev/Program blocks', '-Arm')

    Assert-Equal $EXIT_REFUSED $r.Exit 'exit code'
    if (Test-Path -LiteralPath $ws.ScratchDir) {
        throw 'the refused run created its ScratchDir, so the fence did not run before every side effect'
    }
}

Write-Host ''
Write-Host '=============================================================================='
Write-Host ("RESULT: {0} passed, {1} failed" -f $script:Pass, $script:Fail)
if ($script:Fail -gt 0) {
    foreach ($f in $script:Failures) { Write-Host ("  failed: {0}" -f $f) }
    Write-Host '=============================================================================='
    exit 1
}
Write-Host '=============================================================================='
exit 0
