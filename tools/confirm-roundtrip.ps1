<#
.SYNOPSIS
    THE CONFIRM LOOP - export, round-trip one block through the converter and TIA, export again, and
    compare the first export against the last.

.DESCRIPTION
        export --> to-ir --> to-xml --> import --> compile --> export
           |______________ compare THESE TWO ___________________|

    The owner's invariance principle (docs/notes/test-environment-build-plan.md, 2026-08-12), and it
    is STRICTLY STRONGER than `converter drift-check`: drift-check never leaves the PC, so it can
    only ask whether the converter is self-consistent. This loop goes THROUGH TIA, so it also
    catches what TIA does on import and on compile - the class the MemoryLayout hole belonged to,
    where the DB round-tripped "equal and still wrong", drift-check said MATCH, import did not
    error, compile did not error, and the first symptom was a runtime Modbus status code.

    The judgement is NOT here. `converter compare` carries it, in-process and Portal-free, because
    FI-24 holds the converter to being a pure file transformer. This script owns Portal, the
    filesystem and the sequence, and nothing else.

    *** IT MUTATES. *** The import writes to the project, and it is emphatically not a no-op - the
    layout flip happens exactly there. So:

      - the DEFAULT IS A DRY RUN that stops before the import and prints what it would do next;
      - -Arm is required for the mutating half, and the armed run is FENCED TO AN ALLOWLISTED
        SCRATCH PROJECT (ADR-0011) - see "THE SCRATCH FENCE" below. There is no override flag;
      - the FIRST EXPORT is preserved byte-exact, never re-saved through any tool, and its hash is
        re-checked at the end. A comparison against a reformatted reference is worthless.

    *** THIS IS A GATE, NOT A CHECK AN AGENT RUNS PER EDIT. *** It needs Portal EXCLUSIVELY and costs
    minutes per block. Its place is a converter capability change (ADR-0010 names it as that gate)
    and pre-promotion verification. For the per-edit loop use `converter preflight`, `review` and
    `drift-check`, all of which are read-only, run in milliseconds, and can be pointed at anything.

    *** THE SCRATCH FENCE (ADR-0011). *** -Arm refuses any project that is not in
    tools/confirm-roundtrip.allowlist. The refusal is an EXIT CODE (4), evaluated before any binary
    check, before any directory is created, and before stage 1 - so on a refusal Portal is never
    contacted at all. It is an ALLOWLIST, never a denylist: this engineering PC carries about
    nineteen private engineering projects in folders beside the scratch ones, and a denylist fails open on
    the one nobody listed. Comparison is against the RESOLVED canonical path, so a relative path,
    different casing or a ".." cannot walk around it; anything that cannot be positively resolved -
    a bare project name, a junction, an 8.3 short name, a missing or empty allowlist - is a REFUSAL,
    not a pass. There is deliberately no -Force and no environment variable: adding a project is an
    owner decision recorded in the allowlist, not a flag.

    *** THIS SCRIPT DELIBERATELY DOES NOT RE-ASSERT block-layout --set Standard AFTER THE IMPORT. ***
    CLAUDE.md requires that in ordinary work, because a re-import reverts a block to Optimized. Here
    that revert is the measurement. Repairing it would make the loop report a pass on the exact
    defect it exists to detect.

    ASCII only, on purpose. Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI, and a UTF-8 em
    dash then decodes to a character the parser treats as a QUOTE DELIMITER - which breaks the
    script in a way whose error message points at an unrelated line. Keep it that way.

.PARAMETER Project
    Project name (already open in Portal) or path to a .apNN file - whatever openness-cli takes.
    WITH -Arm IT MUST BE THE PATH to the .apNN file: a bare name cannot be resolved to a canonical
    path, so the scratch fence cannot identify it, and an unidentifiable target is refused.

.PARAMETER Block
    The block to round-trip. With -Type, a PLC data type (UDT) instead.

.PARAMETER ScratchDir
    Where every artifact of this run lands. Created if absent. Nothing is written anywhere else.

.PARAMETER Group
    "<device>/<path>" for the import, copied VERBATIM from the Path column of `openness-cli list` -
    a device item's real name can contain spaces and an article number as one literal string, and a
    shortened guess fails with an error that does not point at the mismatch. Required with -Arm.

.PARAMETER FirstExport
    Use an export already taken instead of exporting now. The file is copied byte-exact into
    ScratchDir and Portal is not contacted for stage 1 - which also makes the dry run fully offline.

.PARAMETER IrProject
    Passed to to-xml as --project, so member types resolve. Without it, to-xml REFUSES rather than
    guessing (FI-71) and the loop stops at stage 3 with that as the reason.

.PARAMETER Arm
    Perform the mutating half (import, compile, second export, compare). Without it the run stops
    after stage 3 and exits 10 - a dry run proves nothing and must not look like a pass. Subject to
    the scratch fence: see "THE SCRATCH FENCE" above and tools/confirm-roundtrip.allowlist.

.PARAMETER IsScratchProject
    NO LONGER ACCEPTED (ADR-0011) and refused BY NAME, the same shape as download-plan refusing
    --yes and --force by name. It used to be the caller's own assertion that the target was safe to
    write to, with nothing behind it; the allowlist replaced it, because relying on the caller's
    word is the option ADR-0011 explicitly rejected. Declared only so that a recorded invocation
    fails loudly instead of silently meaning something else.

.EXAMPLE
    # Dry run against an export already in hand - no Portal contact at all.
    .\confirm-roundtrip.ps1 -Project Scratch1 -Block MyBlock -ScratchDir C:\tmp\loop `
        -FirstExport C:\tmp\already-exported.xml

.EXAMPLE
    # The armed loop. -Project is the PATH to the .apNN file, and it must be in the allowlist.
    .\confirm-roundtrip.ps1 -Project 'C:\...\GenProject1\GenProject1.ap20' -Block MyBlock `
        -ScratchDir C:\tmp\loop -Group 'PLC1 6ES7 214-1AG40-0XB0/Program blocks' `
        -IrProject ir\test-project001 -Arm

.NOTES
    EXIT CODES - a stage failure and a content change are DIFFERENT FINDINGS and never share a code.

      0  loop completed, first export and last export are EQUIVALENT
      1  loop completed, the content CHANGED - `compare` names what
      2  loop completed but the comparison COULD NOT BE MADE (not a pass)
      3  a stage failed; the loop did not complete and says nothing about invariance
      4  refused before anything ran (the SCRATCH FENCE, a guard, a missing binary, a missing
         argument). On a fence refusal specifically, Portal was never contacted and nothing was
         written anywhere.
     10  dry run finished - the mutating half was never attempted, so nothing is proven
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [Parameter(Mandatory = $true)][string]$Block,
    [Parameter(Mandatory = $true)][string]$ScratchDir,

    [string]$Group,
    [string]$Device,
    [string]$FirstExport,
    [string]$IrProject,

    [switch]$Type,
    [switch]$Arm,
    [switch]$IsScratchProject,
    [switch]$AllowSilentLayout,
    [int]$MaxDifferences = 50,

    [string]$ConverterPath,
    [string]$OpennessCliPath
)

$ErrorActionPreference = 'Stop'

# Default to the Release binaries of THE CHECKOUT THIS SCRIPT LIVES IN. Not a hard-coded absolute
# path: this repo is worked in git worktrees, a worktree build lands at the worktree path, and a
# default pointing elsewhere would silently run a DIFFERENT build of the tool being validated -
# which is the failure mode FI-73 records (a day of converter fixes reaching no agent at all because
# the skills invoked a stale binary). Resolved here rather than as a param default because
# $PSScriptRoot is not yet populated while parameter defaults are being evaluated under 5.1.
$repoOfThisScript = Split-Path -Parent $PSScriptRoot
if (-not $ConverterPath) {
    $ConverterPath = Join-Path $repoOfThisScript 'src\converter\Converter\bin\Release\net8.0\converter.exe'
}
if (-not $OpennessCliPath) {
    $OpennessCliPath = Join-Path $repoOfThisScript 'src\openness-cli\OpennessCli\bin\Release\net48\openness-cli.exe'
}

$EXIT_EQUIVALENT  = 0
$EXIT_CHANGED     = 1
$EXIT_NOTCOMPARED = 2
$EXIT_STAGEFAILED = 3
$EXIT_REFUSED     = 4
$EXIT_DRYRUN      = 10

function Write-Heading([string]$Text) {
    Write-Host ''
    Write-Host ('=' * 78)
    Write-Host $Text
    Write-Host ('=' * 78)
}

function Deny([string]$Reason) {
    Write-Host ''
    Write-Host "REFUSED: $Reason"
    exit $EXIT_REFUSED
}

# Start-Process, not the PowerShell pipeline: openness-cli launches TIA Portal as a CHILD THAT
# INHERITS STDOUT, so a pipe outlives the command and hangs until timeout (CLAUDE.md). Redirection
# here is an OS-level file handle handed to the child, which is the safe shape.
$script:Stages = @()
function Invoke-Stage {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPrefix
    )

    $quoted = $Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }
    $line = $quoted -join ' '

    $outLog = "$LogPrefix.out.log"
    $errLog = "$LogPrefix.err.log"

    Write-Host ''
    Write-Host "STAGE $Name"
    Write-Host "  > $Exe $line"

    $proc = Start-Process -FilePath $Exe -ArgumentList $line -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    $code = $proc.ExitCode

    foreach ($log in @($outLog, $errLog)) {
        if ((Test-Path $log) -and ((Get-Item $log).Length -gt 0)) {
            Get-Content $log | ForEach-Object { Write-Host "  | $_" }
        }
    }

    Write-Host "  exit $code   (logs: $outLog, $errLog)"
    $script:Stages += [pscustomobject]@{ Name = $Name; Exit = $code }
    return $code
}

function Show-StageTable {
    Write-Host ''
    Write-Host 'STAGE EXIT CODES'
    foreach ($s in $script:Stages) {
        Write-Host ("  {0,-28} {1}" -f $s.Name, $s.Exit)
    }
}

function Stop-OnStageFailure([string]$Name, [int]$Code, [string]$Hint) {
    if ($Code -eq 0) { return }
    Write-Heading 'LOOP DID NOT COMPLETE'
    Write-Host "Stage '$Name' exited $Code."
    if ($Hint) { Write-Host $Hint }
    Write-Host ''
    Write-Host '*** THIS IS NOT AN INVARIANCE FINDING. *** "the stage failed" and "the content changed"'
    Write-Host 'are different findings. Nothing here says anything about whether the block survived the'
    Write-Host 'round trip - the round trip did not happen.'
    Show-StageTable
    exit $EXIT_STAGEFAILED
}

function Get-Md5([string]$Path) {
    return (Get-FileHash -Path $Path -Algorithm MD5).Hash.ToLower()
}

# ============================================================================== THE SCRATCH FENCE
#
# ADR-0011. The armed loop MUTATES, so it may only be pointed at an allowlisted scratch project.
# This whole block runs BEFORE any binary check, BEFORE any directory is created, and above all
# BEFORE STAGE 1 - which is the first thing that would launch openness-cli and therefore Portal.
# The precedent is openness-cli's own --yes gates: without the gate satisfied, the plan prints and
# PORTAL IS NEVER ATTACHED. A fence evaluated after the project is open has already done the thing
# it was meant to prevent.
#
# THIS SCRIPT'S ONLY ROUTE TO PORTAL IS Start-Process $OpennessCliPath, in Invoke-Stage, at stages
# 1/4/5/6 and nowhere else. So "the fence refused before openness-cli was launched" and "the fence
# refused before Portal was contacted" are the same statement, and the fence tests assert the first
# one directly with a sentinel stub rather than inferring it.

function Get-RepoRootOfThisScript {
    $probe = Split-Path -Parent $PSCommandPath
    while ($probe) {
        # A git worktree's .git is a FILE, not a directory; Test-Path matches both.
        if (Test-Path (Join-Path $probe '.git')) { return $probe }
        $parent = Split-Path -Parent $probe
        if ($parent -eq $probe) { break }
        $probe = $parent
    }
    return $null
}

# Canonicalisation, ADR-0011 requirement 5: compare the RESOLVED path, never the string as typed,
# so a relative path, a different casing or a ".." cannot walk around the fence. Requirement 4 does
# the rest of the work here: anything this cannot positively resolve is a REFUSAL, not a pass.
#
# Windows PowerShell 5.1 has no ResolveLinkTarget and Resolve-Path does NOT follow reparse points,
# so a junction or symlink cannot be resolved reliably from here. It is therefore DETECTED AND
# REFUSED rather than half-resolved - "I could not canonicalise this" is an honest refusal, and a
# comparison that silently ignored a junction would be a fence with a documented way through it.
# 8.3 short names (PROGRA~1) get the same treatment for the same reason: GetFullPath does not
# expand them, so they are refused with the fix named.
function Resolve-CanonicalPath {
    param([string]$Path, [ref]$Problem)

    $Problem.Value = $null

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $Problem.Value = 'the path is empty'
        return $null
    }

    $full = $null
    try {
        $full = [System.IO.Path]::GetFullPath($Path)
    } catch {
        $Problem.Value = "the path could not be resolved: $($_.Exception.Message)"
        return $null
    }

    $full = $full.TrimEnd([char]'\', [char]'/')

    foreach ($component in $full.Split([char]'\')) {
        if ($component -match '~\d') {
            $Problem.Value = "'$component' looks like an 8.3 short name, which cannot be canonicalised from here - supply the long path"
            return $null
        }
    }

    return $full
}

# Walks the path and every ancestor directory looking for a reparse point. Only meaningful for
# things that exist, which the caller has already established for the project itself.
function Test-AnyReparsePoint {
    param([string]$FullPath, [ref]$Where)

    $Where.Value = $null
    $probe = $FullPath
    while ($probe) {
        if (Test-Path -LiteralPath $probe) {
            $item = Get-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
            if ($item -and (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
                $Where.Value = $probe
                return $true
            }
        }
        $parent = Split-Path -Parent $probe
        if (-not $parent -or $parent -eq $probe) { break }
        $probe = $parent
    }
    return $false
}

function Assert-ScratchProject {
    param([string]$ProjectValue)

    $allowlistPath = Join-Path $PSScriptRoot 'confirm-roundtrip.allowlist'

    # Empty is not clean (FI-44, ADR-0011 requirement 4). A missing or contentless allowlist means
    # the fence verified NOTHING, so it must refuse - never "no entries, therefore nothing to
    # object to".
    if (-not (Test-Path -LiteralPath $allowlistPath)) {
        Deny @"
-Arm is fenced to the scratch project (ADR-0011) and the allowlist is MISSING:
  $allowlistPath

The fence cannot verify anything without it, and an unverified target is a refusal, not a pass.
"@
    }

    $entries = @()
    foreach ($line in (Get-Content -LiteralPath $allowlistPath)) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }
        $entries += $trimmed
    }

    if ($entries.Count -eq 0) {
        Deny @"
-Arm is fenced to the scratch project (ADR-0011) and the allowlist contains NO ENTRIES:
  $allowlistPath

An empty allowlist permits nothing. This is a refusal rather than a pass by construction.
"@
    }

    $repoRootForEntries = Get-RepoRootOfThisScript

    $resolvedEntries = @()
    foreach ($entry in $entries) {
        $raw = $entry
        if ($entry.StartsWith('repo:', [System.StringComparison]::OrdinalIgnoreCase)) {
            if (-not $repoRootForEntries) {
                Deny "Allowlist entry '$raw' is repo-relative, but the repository root could not be located from $PSCommandPath."
            }
            $raw = Join-Path $repoRootForEntries $entry.Substring('repo:'.Length)
        }

        if (-not [System.IO.Path]::IsPathRooted($raw)) {
            Deny @"
Allowlist entry '$entry' is a bare relative path.

It would resolve differently depending on the working directory, which is exactly the ambiguity
ADR-0011 requirement 4 refuses. Use an absolute path, or the 'repo:' prefix.
"@
        }

        $problem = $null
        $resolved = Resolve-CanonicalPath -Path $raw -Problem ([ref]$problem)
        if (-not $resolved) {
            Deny "Allowlist entry '$entry' could not be canonicalised: $problem"
        }

        $resolvedEntries += [pscustomobject]@{ Raw = $entry; Resolved = $resolved }
    }

    $permittedList = ($resolvedEntries | ForEach-Object { "  " + $_.Raw + "  ->  " + $_.Resolved }) -join [System.Environment]::NewLine

    # A bare project NAME (openness-cli accepts one for an already-open project) has no path to
    # canonicalise, so the fence cannot positively identify it as the scratch project - and
    # "probably the scratch one, it is named like it" is precisely the guess ADR-0011 declines to
    # make. Arming therefore requires the path to the .apNN FILE.
    #
    # *** IT MUST BE A FILE, AND THE EXTENSION IS CHECKED. *** Found by the fence's own tests,
    # 2026-08-13: a bare name is not merely unresolvable, it can RESOLVE TO A DIRECTORY - run from
    # the repo root, `-Project GenProject1` hit the project FOLDER, which exists. The fence still
    # refused (the folder does not equal the .ap20 entry, so it failed closed), but it refused with
    # the wrong reason printed, and a guard that explains itself wrongly is how someone concludes it
    # is broken and goes looking for a way around it.
    $isProjectFile = (Test-Path -LiteralPath $ProjectValue -PathType Leaf) -and
                     ([System.IO.Path]::GetExtension($ProjectValue) -match '^\.ap\d+$')
    if (-not $isProjectFile) {
        $what = if (Test-Path -LiteralPath $ProjectValue -PathType Container) {
            'it is a DIRECTORY, not a project file'
        } elseif (Test-Path -LiteralPath $ProjectValue) {
            'it exists but is not a .apNN project file'
        } else {
            'no such file exists'
        }

        Deny @"
-Arm is fenced to the scratch project (ADR-0011), and -Project is not a project file: $what.

  -Project as given : $ProjectValue

The armed loop needs the PATH TO THE .apNN FILE, not a project name and not the project folder. A
name cannot be resolved to a canonical path, so the fence cannot positively identify it as the
scratch project - and an unidentifiable target is a refusal, not a pass (ADR-0011 requirement 4). A
dry run (no -Arm) still accepts whatever openness-cli accepts, including a bare name.

Permitted (from $allowlistPath):
$permittedList

Nothing was written and Portal was never contacted.
"@
    }

    $problem = $null
    $projectResolved = Resolve-CanonicalPath -Path $ProjectValue -Problem ([ref]$problem)
    if (-not $projectResolved) {
        Deny "-Project could not be canonicalised: $problem"
    }

    $where = $null
    if (Test-AnyReparsePoint -FullPath $projectResolved -Where ([ref]$where)) {
        Deny @"
-Project reaches the target through a junction or symlink:
  $where

Windows PowerShell 5.1 cannot resolve a reparse point reliably, so this path cannot be canonicalised
and the fence will not compare a path it could not canonicalise (ADR-0011 requirements 4 and 5).
Supply the real path to the project instead.
"@
    }

    foreach ($entry in $resolvedEntries) {
        if ([string]::Equals($projectResolved, $entry.Resolved, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host ''
            Write-Host 'SCRATCH FENCE: permitted (ADR-0011)'
            Write-Host "  -Project resolves to : $projectResolved"
            Write-Host "  allowlist entry      : $($entry.Raw)"
            return
        }
    }

    Deny @"
-Arm is fenced to the scratch project, and this is not one (ADR-0011).

  -Project as given    : $ProjectValue
  -Project resolves to : $projectResolved

Permitted (from $allowlistPath):
$permittedList

The confirm loop's import stage WRITES, and it is measurably not a no-op. CLAUDE.md hard rule 5 puts
the promotion into the real project behind the engineer, and a mutating loop that could be pointed
at a real project is a way around that gate that nobody decided to build - it would be reached by a
mistyped path, not by a decision.

*** THERE IS NO OVERRIDE FLAG, DELIBERATELY. *** If this project legitimately needs the loop, that is
an owner decision that adds it to the allowlist with the restore point named (ADR-0011 requirement 6
and its revisit trigger). Nothing you can pass on this command line will get you past this.

Nothing was written and Portal was never contacted.
"@
}

# ------------------------------------------------------------------------------ guards, up front

Write-Heading "CONFIRM LOOP - $Block in $Project"

# Refused BY NAME rather than quietly ignored, the same shape as download-plan refusing --yes and
# --force by name. -IsScratchProject used to be the caller's own assertion that the target was safe
# to write to, with nothing behind it. ADR-0011 replaced that with the allowlist, because "rely on
# the caller's care" is the option the ruling explicitly rejected. Leaving the switch accepted would
# let a recorded invocation keep working while silently meaning something else.
if ($IsScratchProject) {
    Deny @'
-IsScratchProject is no longer accepted (ADR-0011).

It was the caller's assertion that the target was a scratch copy, with no detection behind it. The
fence is now an allowlist in tools/confirm-roundtrip.allowlist, checked against the resolved path,
because a caller's word is what ADR-0011 declined to rely on. Drop the switch; if the project is in
the allowlist the run proceeds, and if it is not, no flag will help.
'@
}

if ($Arm) { Assert-ScratchProject -ProjectValue $Project }

foreach ($pair in @(, @('converter', $ConverterPath)) + @(, @('openness-cli', $OpennessCliPath))) {
    if (-not (Test-Path $pair[1])) {
        # openness-cli is only needed once Portal is involved; a fully offline dry run (-FirstExport,
        # no -Arm) must not be blocked by its absence.
        $needed = ($pair[0] -eq 'converter') -or $Arm -or (-not $FirstExport)
        if ($needed) { Deny "$($pair[0]) not found at: $($pair[1])" }
        Write-Host "NOTE: $($pair[0]) not found at $($pair[1]) - not needed for this run."
    }
}

if ($Arm -and -not $Group) {
    Deny @'
-Arm requires -Group "<device>/<path>".

Copy it VERBATIM from the Path column of `openness-cli list`. A device item's real name can contain
spaces and an embedded article number as one literal string, and a shortened guess fails with a
"No device item found under ..." that does not point at the mismatch.
'@
}

# Data boundary (docs/13). Live-run material may be worked freely and must never be committed, so
# the one mechanical thing this script can enforce is that its artifacts do not land in the repo.
$repoRoot = $null
$probe = Split-Path -Parent $PSCommandPath
while ($probe -and -not $repoRoot) {
    if (Test-Path (Join-Path $probe '.git')) { $repoRoot = $probe }
    $parent = Split-Path -Parent $probe
    if ($parent -eq $probe) { break }
    $probe = $parent
}

$looksLiveRun = ($Project -match 'Live Runs') -or ($FirstExport -and $FirstExport -match 'Live Runs')
if ($looksLiveRun -and $repoRoot) {
    $resolvedScratch = [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $ScratchDir))
    if ($resolvedScratch.StartsWith([System.IO.Path]::GetFullPath($repoRoot), [System.StringComparison]::OrdinalIgnoreCase)) {
        Deny @"
-Project looks like live-run content and -ScratchDir is inside the repository working tree:
  $resolvedScratch

Live-run access is full and unsanitized; RETENTION is what the boundary governs (docs/13). Put the
scratch directory outside the repo - the job's own scratch dir is the right place.
"@
    }
}

if (-not (Test-Path $ScratchDir)) { New-Item -ItemType Directory -Path $ScratchDir -Force | Out-Null }
$ScratchDir = (Resolve-Path $ScratchDir).Path

$irDir    = Join-Path $ScratchDir 'ir'
$regenDir = Join-Path $ScratchDir 'regen'
$logDir   = Join-Path $ScratchDir 'logs'
foreach ($d in @($irDir, $regenDir, $logDir)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

$firstXml  = Join-Path $ScratchDir '01-first-export.xml'
$irFile    = Join-Path $irDir '01-first-export.ir'
$regenXml  = Join-Path $regenDir '01-first-export.xml'
$secondXml = Join-Path $ScratchDir '06-second-export.xml'

$kindFlag = if ($Type) { '--type' } else { '--block' }
$mode = if ($Arm) { 'ARMED - the import WILL write to the project' } else { 'DRY RUN - stops before the import' }

Write-Host "Project     : $Project"
Write-Host "Target      : $kindFlag $Block"
Write-Host "Scratch dir : $ScratchDir"
Write-Host "Mode        : $mode"

# --------------------------------------------------------------- stage 1: the reference export

if ($FirstExport) {
    if (-not (Test-Path $FirstExport)) { Deny "-FirstExport not found: $FirstExport" }
    Write-Host ''
    Write-Host 'STAGE 1 export'
    # Copy-Item, never a load-and-save: the reference must stay byte-exact or the comparison at the
    # end is against a reformatted document and means nothing.
    Copy-Item -LiteralPath $FirstExport -Destination $firstXml -Force
    Write-Host '  supplied by -FirstExport, copied byte-exact'
    Write-Host '  exit 0 (Portal not contacted)'
    $script:Stages += [pscustomobject]@{ Name = '1 export (supplied)'; Exit = 0 }
} else {
    if (Test-Path $firstXml) { Remove-Item -LiteralPath $firstXml -Force }
    $args1 = @($Project, $kindFlag, $Block, '--out', $firstXml)
    if ($Device) { $args1 += @('--device', $Device) }
    $code = Invoke-Stage -Name '1 export' -Exe $OpennessCliPath -Arguments $args1 -LogPrefix (Join-Path $logDir '01-export')
    Stop-OnStageFailure '1 export' $code 'Nothing was written to the project. openness-cli exit codes: src/openness-cli/README.md.'
}

if (-not (Test-Path $firstXml)) {
    Stop-OnStageFailure '1 export' 1 "The export command succeeded but produced no file at $firstXml."
}

$firstHash = Get-Md5 $firstXml
Write-Host "  reference md5: $firstHash  ($firstXml)"
Write-Host '  *** this file is the restore material for the block. It is never re-saved by this script. ***'

# ------------------------------------------------------------------- stages 2-3: the PC-side half

$code = Invoke-Stage -Name '2 to-ir' -Exe $ConverterPath -Arguments @('to-ir', $firstXml, '--out', $irDir) `
    -LogPrefix (Join-Path $logDir '02-to-ir')
Stop-OnStageFailure '2 to-ir' $code 'The converter could not read this export. An UnsupportedConstructException here is a converter SCOPE item (owner, 2026-08-12), not a defect in the block.'

if (-not (Test-Path $irFile)) { Stop-OnStageFailure '2 to-ir' 1 "to-ir reported success but no .ir at $irFile." }

$args3 = @('to-xml', $irFile, '--out', $regenDir)
if ($IrProject) { $args3 += @('--project', $IrProject) }
$code = Invoke-Stage -Name '3 to-xml' -Exe $ConverterPath -Arguments $args3 -LogPrefix (Join-Path $logDir '03-to-xml')
Stop-OnStageFailure '3 to-xml' $code 'If this is the FI-71 unresolved-member-type refusal, pass -IrProject <ir-dir>. It is refusing rather than guessing a type TIA would reject at compile.'

if (-not (Test-Path $regenXml)) { Stop-OnStageFailure '3 to-xml' 1 "to-xml reported success but no XML at $regenXml." }

if ((Get-Md5 $firstXml) -ne $firstHash) {
    Stop-OnStageFailure 'reference integrity' 1 'The first export changed on disk during the PC-side stages. The comparison would be against a rewritten reference; refusing to continue.'
}

# ------------------------------------------------------------------------------ the dry-run stop

if (-not $Arm) {
    Write-Heading 'DRY RUN - STOPPING BEFORE THE IMPORT'
    Write-Host 'Done so far (nothing written to the project):'
    Write-Host "  reference export : $firstXml  (md5 $firstHash)"
    Write-Host "  IR               : $irFile"
    Write-Host "  regenerated XML  : $regenXml   <-- this is the file that WOULD be imported"
    Write-Host ''
    Write-Host 'What -Arm WOULD do next, in order:'
    Write-Host ''

    $wouldGroup = if ($Group) { $Group } else { '<REQUIRED: -Group "<device>/<path>", verbatim from the Path column of openness-cli list>' }
    $importKind = if ($Type) { ' --type' } else { '' }
    $compileDevice = if ($Device) { " --device ""$Device""" } else { '' }

    Write-Host '  4. IMPORT - WRITES TO THE PROJECT'
    Write-Host "       openness-cli import ""$Project"" --group ""$wouldGroup""$importKind ""$regenXml"""
    Write-Host "     Openness matches by NAME, so this REPLACES the block '$Block' in the project."
    Write-Host '     It is not a no-op: a re-import is measured to revert a block memory layout to'
    Write-Host '     Optimized, and that revert is the thing this loop is here to catch.'
    Write-Host ''
    Write-Host '  5. COMPILE'
    Write-Host "       openness-cli compile ""$Project""$compileDevice $kindFlag ""$Block"""
    Write-Host '     Its exit code is RECORDED, not gated on. The same code means different things on'
    Write-Host '     either side of the 2026-08-12 openness-cli fix (before it, every per-block compile'
    Write-Host '     in a project with a permanent hardware warning exited 8 on errors: 0), and this'
    Write-Host '     script does not guess which binary it has. Stage 6 is the gate that holds in both'
    Write-Host '     cases - TIA refuses to export an inconsistent block.'
    Write-Host ''
    Write-Host "  6. EXPORT AGAIN -> $secondXml"
    Write-Host ''
    Write-Host '  7. COMPARE'
    Write-Host "       converter compare ""$firstXml"" ""$secondXml"""
    Write-Host ''
    Write-Host 'To arm it:  -Arm -IsScratchProject -Group "<device>/<path>"'
    Write-Host ''
    Write-Host '*** The block would be left in whatever state the import produced. The reference export'
    Write-Host "    $firstXml is the restore material: re-import it to put the block back. ***"
    Show-StageTable
    Write-Host ''
    Write-Host 'VERDICT: DRY RUN - the mutating half was never attempted, so NOTHING is proven about'
    Write-Host '         whether this block survives a trip through TIA.'
    exit $EXIT_DRYRUN
}

# -------------------------------------------------------------------------- stages 4-6: through TIA

Write-Heading 'ARMED - from here the project is being modified'

$args4 = @($Project, '--group', $Group)
if ($Type) { $args4 += '--type' }
$args4 += $regenXml
$code = Invoke-Stage -Name '4 import' -Exe $OpennessCliPath -Arguments $args4 -LogPrefix (Join-Path $logDir '04-import')
Stop-OnStageFailure '4 import' $code "The project may or may not have been modified - check it before re-running. Restore material: $firstXml"

$args5 = @($Project, $kindFlag, $Block)
if ($Device) { $args5 += @('--device', $Device) }
$compileExit = Invoke-Stage -Name '5 compile' -Exe $OpennessCliPath -Arguments $args5 -LogPrefix (Join-Path $logDir '05-compile')
if ($compileExit -ne 0) {
    # Deliberately not a stop, and deliberately not version-dependent. Until 2026-08-12 "compile
    # --block" keyed its verdict on State rather than ErrorCount, so in a project carrying a
    # permanent hardware warning EVERY per-block compile exited 8 regardless of the block; that is
    # now fixed, but the fix shipped Debug-only (the Release binary was in use against Portal and
    # deliberately not rebuilt), so the binary this script invokes may be from either era and the
    # same exit code means different things in each.
    #
    # Rather than branch on a version it cannot see, the loop leans on something that is TIA's
    # behaviour and not openness-cli's verdict logic: TIA REFUSES TO EXPORT AN INCONSISTENT BLOCK.
    # So stage 6 gates, with a reason, whichever binary is in use - and the compile's exit code and
    # full log are reported here for the operator either way.
    Write-Host ''
    Write-Host "  NOTE: compile exited $compileExit and the loop is CONTINUING deliberately."
    Write-Host '        Exit 8 on a clean block was the pre-2026-08-12 "keys on State" defect; on a'
    Write-Host '        binary carrying that fix, 8 means real errors and 11 means the block is still'
    Write-Host '        inconsistent. This script does not guess which binary it has. Stage 6 is the'
    Write-Host '        gate that holds in both cases: TIA refuses to export an inconsistent block.'
    Write-Host '        Read the compile log above before trusting a pass.'
}

if (Test-Path $secondXml) { Remove-Item -LiteralPath $secondXml -Force }
$args6 = @($Project, $kindFlag, $Block, '--out', $secondXml)
if ($Device) { $args6 += @('--device', $Device) }
$code = Invoke-Stage -Name '6 export' -Exe $OpennessCliPath -Arguments $args6 -LogPrefix (Join-Path $logDir '06-export')
Stop-OnStageFailure '6 export' $code "Exit 5 here usually means TIA refused because the block is INCONSISTENT after the import - see the compile log. The block is still in its post-import state; restore material: $firstXml"

if ((Get-Md5 $firstXml) -ne $firstHash) {
    Stop-OnStageFailure 'reference integrity' 1 'The first export changed on disk during the Portal stages - the comparison would be against a rewritten reference.'
}

# ---------------------------------------------------------------------------- stage 7: the verdict

$args7 = @('compare', $firstXml, $secondXml, '--max-differences', "$MaxDifferences")
if ($AllowSilentLayout) { $args7 += '--allow-silent-layout' }
$compareExit = Invoke-Stage -Name '7 compare' -Exe $ConverterPath -Arguments $args7 -LogPrefix (Join-Path $logDir '07-compare')

Write-Heading 'RESULT'
Show-StageTable
Write-Host ''
Write-Host "reference : $firstXml (md5 $firstHash, unchanged)"
Write-Host "re-export : $secondXml"
Write-Host ''
Write-Host "*** The block in '$Project' is now whatever the import made it. This script does NOT"
Write-Host '    re-assert "block-layout --set Standard" - that would repair the defect it measures.'
Write-Host "    Restore by re-importing $firstXml. ***"
Write-Host ''

switch ($compareExit) {
    0 {
        Write-Host 'VERDICT: INVARIANT - the block survived export -> to-ir -> to-xml -> import -> compile'
        Write-Host '         -> export with no semantic change. This is the confirm loop passing.'
        exit $EXIT_EQUIVALENT
    }
    1 {
        Write-Host 'VERDICT: CHANGED - the round trip did not preserve the block. The differences above name'
        Write-Host '         the element and both values. This is the finding the loop exists to produce.'
        exit $EXIT_CHANGED
    }
    default {
        Write-Host 'VERDICT: NOT COMPARED - the loop ran but the comparison could not be made. NOT a pass.'
        exit $EXIT_NOTCOMPARED
    }
}
