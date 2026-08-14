<#
.SYNOPSIS
    THE COMMITTED CONCURRENCY HAMMER for wave-cli. Drives N genuinely concurrent `wave-cli submit`
    processes at a shared wave store and RECONCILES THE RESULT AGAINST THE STORE ON DISK.

.DESCRIPTION
    This exists because the overnight concurrency campaign (1 -> 128 submitters, a crash found at 48,
    a sustained 256-submission run) was driven by ad-hoc scripts in scratch directories that are GONE,
    against a build that has changed substantially since. Those figures are PROVENANCE, NOT EVIDENCE.
    Everything here is parameterised so the same run can be taken again on a later build.

    WHAT AN "AGENT" IS HERE. A string passed to `wave-cli --agent`. Concurrency comes from CONCURRENT
    PROCESS INVOCATIONS of wave-cli.exe, never from AI agents. No sub-agent is spawned by this script
    and none should be spawned to drive it.

    THE FIVE MEASUREMENT RULES THIS SCRIPT IS BUILT AROUND
      1. RECONCILE AGAINST THE STORE ON DISK, never against what the submitters report. Every count in
         the ON-DISK column is parsed out of `wave-slots.state` itself.
      2. PRINT A DENOMINATOR ON EVERYTHING. Submitted / admitted / refused / unusable / no-result /
         on disk, on every row. Empty is not clean: a scenario that admitted nothing FAILS.
      3. NOTHING MAY VANISH. A submitter that produced no readable verdict is reported as NO-RESULT
         and fails the run. A refusal with no reason text is reported as UNNAMED and fails the run.
      4. READ `ACHIEVED CONCURRENCY`, never the slot count. SERIALISED against one block is a CORRECT
         RESULT and is reported as such, not as a failure.
      5. WAVE DURATION IS REFERENCE ONLY. It gates nothing here and must not be quoted as a capability
         figure.
    And rule 6, which has already cost this project a day: READ THE STORE PATH THE TOOL ECHOES, never
    the argument passed. The pre-flight `status` call echoes the resolved path and this script compares
    it against what it intended before a single submission is made.

    THE ARRIVAL BARRIER, AND WHY THERE ARE TWO MECHANISMS
      Simultaneous: the PARENT takes an exclusive handle on the store's own lease file, launches every
        submitter, lets them queue on it, then RELEASES. The barrier is the tool's real contended
        resource, so the submitters are released into contention within milliseconds of each other
        rather than smeared across the several seconds it takes Windows to launch N processes.
        A launch-ordered start is NOT simultaneous arrival, and at N=128 the smear is comparable to the
        whole wave: measuring it that way would hide a knee rather than find one.
      Staggered: no barrier. Submitters are launched -StaggerMs apart, which is the mode that reaches
        lease and ordering defects rather than raw contention.
      Which mechanism a row used is printed with the row.

    SHAPES. N slots against ONE FB instance is ONE SLOT under D9, so a run that submits N slots at one
    block and calls itself N-concurrent has measured nothing.
      Disjoint          - submitters are spread across the artificial Hx corpus (FB/DB 9010-9013, four
                          genuinely disjoint blocks). Reachable state is COMPUTED by
                          `converter reachable-state`, never declared. NOTE THE CEILING: the corpus has
                          four blocks, so achieved concurrency cannot exceed 4 no matter how many
                          submitters run. That is a property of the corpus and it is printed.
      Conflicting       - every submitter takes the SAME block. Expected result: SERIALISED.
      SyntheticDisjoint - DECLARED closures (--reaches), one unique synthetic name per submitter. This
                          is the shape D9 forbids for real work; it is kept for fixtures and it is the
                          only way to exercise the colourer at a width the corpus cannot reach. Every
                          row it produces is labelled DECLARED-NOT-COMPUTED and must not be read as a
                          measurement of anything the reference graph proved.

.PARAMETER Submitters
    Concurrent `wave-cli submit` processes per round. Default 8. Scales to 128 by parameter; the
    default is deliberately modest, because a default that hurts is a default that gets run once.

.PARAMETER Arrival
    Simultaneous, Staggered, or Both (default). See the barrier note above.

.PARAMETER StaggerMs
    Spread between launches in Staggered mode. Default 40.

.PARAMETER Shapes
    Any of Disjoint, Conflicting, SyntheticDisjoint. Default Disjoint,Conflicting.

.PARAMETER Rounds
    Rounds per scenario, accumulating into the SAME store. This is the sustained shape. Default 1.

.PARAMETER Cap
    D29's per-wave-set width cap passed to wave-cli. 0 (default) means AUTO: set to the submitter
    count so the cap does not bound the measurement. AN AUTO CAP IS NOT A BANDWIDTH FIGURE and the
    provenance string says so on every submission.

.EXAMPLE
    # powershell.exe -- the default run
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\hammer-waves.ps1

.EXAMPLE
    # powershell.exe -- one level of the sweep, both arrival modes, all three shapes
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\hammer-waves.ps1 -Submitters 48 -Shapes Disjoint,Conflicting,SyntheticDisjoint

.NOTES
    ASCII ONLY, CRLF. PowerShell 5.1 reads a BOM-less script as ANSI, and a UTF-8 em dash decodes to a
    quote delimiter with the parse error pointing a hundred lines away from the real one. No &&, no ||,
    no ternary, no null-coalescing.

    EXIT: 0 every scenario reconciled and every probe refused
          1 a discrepancy was found (this is a RESULT, and the detail says which)
          2 the run could not be made at all (nothing was measured)
#>

[CmdletBinding()]
param(
    [int]$Submitters = 8,
    [ValidateSet('Simultaneous', 'Staggered', 'Both')]
    [string]$Arrival = 'Both',
    [string[]]$Shapes = @('Disjoint', 'Conflicting'),
    [int]$Rounds = 1,
    [int]$StaggerMs = 40,
    [int]$Cap = 0,
    [string]$CapProvenance = '',
    [string]$StoreRoot = 'C:\ProgramData\Ladder-AI\wave\hammer',
    [string]$WaveCli = '',
    [string]$Converter = '',
    [string]$Project = '',
    [string]$ReachableState = '',
    [string[]]$Blocks = @('FB_HxBoolEcho', 'FB_HxDwellTimer', 'FB_HxIntStep', 'FB_HxSealLatch'),
    [int]$LeaseTimeoutMs = 0,
    [int]$BarrierSettleMs = 0,
    [int]$ProcessTimeoutS = 0,
    [string]$RunId = '',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$ExitClean = 0
$ExitDiscrepancy = 1
$ExitUnusable = 2

# =====================================================================================================
# HELPERS
# =====================================================================================================

function Write-Rule {
    param([string]$Text)
    Write-Host ''
    Write-Host ('=' * 100)
    Write-Host $Text
    Write-Host ('=' * 100)
}

function Get-Median {
    param([int[]]$Values)
    if ($null -eq $Values) { return -1 }
    if ($Values.Count -eq 0) { return -1 }
    $sorted = @($Values | Sort-Object)
    $mid = [int][math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) { return [int]$sorted[$mid] }
    return [int](($sorted[$mid - 1] + $sorted[$mid]) / 2)
}

function Read-TextOrEmpty {
    # Get-Content -Raw returns $null for an empty file, and $null + $null is not the empty string.
    # A submitter whose output could not be read must reach the caller as '' so it is classified as
    # UNNAMED, never silently concatenated into something that happens to match.
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return '' }
    $raw = Get-Content -LiteralPath $Path -Raw
    if ($null -eq $raw) { return '' }
    return [string]$raw
}

function Invoke-WaveCli {
    # Runs wave-cli and returns exit code + combined output. NEVER reads $? after a pipeline: $? is
    # the LAST command's status, which on 2026-08-14 nearly produced a false finding twice in one
    # session. $LASTEXITCODE is the native process's own code and is what is read here.
    param([string]$Exe, [string[]]$Arguments)
    $raw = & $Exe @Arguments 2>&1
    $code = $LASTEXITCODE
    $text = ($raw | Out-String)
    return [PSCustomObject]@{ Exit = $code; Text = $text }
}

function Read-StoreFromDisk {
    # RULE 1. Parses wave-slots.state itself. The declared count and the parsed count are compared
    # here rather than trusted, because a store read short is a submission that silently never happens.
    param([string]$StoreDir)

    $slotsPath = Join-Path $StoreDir 'wave-slots.state'
    $markerPath = Join-Path $StoreDir 'wave-store.initialised'

    $result = [PSCustomObject]@{
        SlotsFileExists = (Test-Path -LiteralPath $slotsPath)
        MarkerExists    = (Test-Path -LiteralPath $markerPath)
        Declared        = -1
        Ids             = @()
        Count           = 0
        Torn            = $false
        Note            = ''
    }

    if (-not $result.SlotsFileExists) {
        if ($result.MarkerExists) {
            $result.Torn = $true
            $result.Note = 'MARKER PRESENT, SLOTS FILE ABSENT: a write died mid-replace. This is not an empty store.'
        }
        else {
            $result.Note = 'No slots file and no marker: nothing has ever been published here.'
        }
        return $result
    }

    $ids = New-Object System.Collections.Generic.List[string]
    foreach ($line in (Get-Content -LiteralPath $slotsPath)) {
        if ($line.StartsWith('slots=')) {
            $result.Declared = [int]$line.Substring(6)
            continue
        }
        if ($line.StartsWith('slot=')) {
            $fields = $line.Substring(5) -split "`t"
            $ids.Add($fields[0])
        }
    }

    $result.Ids = $ids.ToArray()
    $result.Count = $ids.Count

    if ($result.Declared -ne $result.Count) {
        $result.Torn = $true
        $result.Note = ('DECLARED ' + $result.Declared + ' SLOTS, CARRIES ' + $result.Count + '.')
    }

    return $result
}

function Get-EchoedStorePath {
    # RULE 6. Reads the path the TOOL echoes out of its own output rather than trusting the argument.
    param([string]$Text)
    if ($Text -match "NOTHING SUBMITTED at '([^']+)'") { return $Matches[1] }
    if ($Text -match "RESET '([^']+)'") { return $Matches[1] }
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line.StartsWith('STORE ')) {
            if ($line -match '^STORE (.+?) .{1,3} \d+ slot\(s\):$') { return $Matches[1] }
        }
    }
    return ''
}

function Get-SubmitArgs {
    param(
        [string]$StoreDir,
        [string]$Agent,
        [string]$Slot,
        [string]$Shape,
        [string]$Block,
        [string]$SyntheticName,
        [string]$ClosureFile,
        [int]$CapValue,
        [string]$CapText,
        [int]$LeaseMs
    )

    $list = New-Object System.Collections.Generic.List[string]
    $list.Add('submit')
    $list.Add('--store');  $list.Add($StoreDir)
    $list.Add('--agent');  $list.Add($Agent)
    $list.Add('--slot');   $list.Add($Slot)

    if ($Shape -eq 'SyntheticDisjoint') {
        # THE DECLARED PATH. D9 forbids this shape for real work; it is here to reach a width the
        # four-block corpus cannot, and every row it produces is labelled DECLARED-NOT-COMPUTED.
        $list.Add('--reaches'); $list.Add($SyntheticName)
        $list.Add('--reaches-from')
        $list.Add('SYNTHETIC fixture closure declared by tools/hammer-waves.ps1. NOT computed from any reference graph.')
    }
    else {
        $list.Add('--reachable-state'); $list.Add($ClosureFile)
        $list.Add('--reachable-block'); $list.Add($Block)
    }

    $list.Add('--cap');             $list.Add([string]$CapValue)
    $list.Add('--cap-provenance');  $list.Add($CapText)
    $list.Add('--width');           $list.Add('4')
    $list.Add('--lease-timeout-ms'); $list.Add([string]$LeaseMs)

    return $list.ToArray()
}

# =====================================================================================================
# PRE-FLIGHT
# =====================================================================================================

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($repoRoot)) { $repoRoot = (Get-Location).Path }

if ([string]::IsNullOrWhiteSpace($WaveCli)) {
    $WaveCli = Join-Path $repoRoot 'src\wave-control\WaveControl.Cli\bin\Release\net8.0\wave-cli.exe'
}
if ([string]::IsNullOrWhiteSpace($Converter)) {
    $Converter = Join-Path $repoRoot 'src\converter\Converter\bin\Release\net8.0\converter.exe'
}
if ([string]::IsNullOrWhiteSpace($Project)) {
    $Project = Join-Path $repoRoot 'ir\test-project001'
}
if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = (Get-Date).ToString('yyyyMMdd-HHmmss')
}

if ($Submitters -lt 1) {
    Write-Host 'REFUSED: -Submitters must be at least 1. Nothing would be measured.'
    exit $ExitUnusable
}
if ($Rounds -lt 1) {
    Write-Host 'REFUSED: -Rounds must be at least 1. Nothing would be measured.'
    exit $ExitUnusable
}
# powershell.exe -File passes an array parameter as ONE STRING, so `-Shapes A,B` arrives as the single
# value "A,B" and would be refused as an unknown shape. Splitting here makes the -File form (the one
# the .EXAMPLE block documents, and the one an unattended caller uses) behave like the -Command form.
# A documented command that only works in one invocation style is a defect in the document.
$Shapes = @($Shapes | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 })
$Blocks = @($Blocks | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 })

foreach ($shape in $Shapes) {
    if (@('Disjoint', 'Conflicting', 'SyntheticDisjoint') -notcontains $shape) {
        Write-Host ('REFUSED: unknown shape "' + $shape + '". Known: Disjoint, Conflicting, SyntheticDisjoint.')
        exit $ExitUnusable
    }
}

$arrivals = @()
if ($Arrival -eq 'Both') { $arrivals = @('Simultaneous', 'Staggered') }
else { $arrivals = @($Arrival) }

if ($Cap -le 0) {
    $Cap = [math]::Max($Submitters, 4)
    if ([string]::IsNullOrWhiteSpace($CapProvenance)) {
        $CapProvenance = 'AUTO by tools/hammer-waves.ps1: set to the submitter count so the cap does not bound the measurement. THIS IS NOT A D29 POLL-BANDWIDTH FIGURE and no capability claim rests on it.'
    }
}
if ([string]::IsNullOrWhiteSpace($CapProvenance)) {
    Write-Host 'REFUSED: -Cap was given without -CapProvenance. A cap nobody can trace is one nobody can re-check.'
    exit $ExitUnusable
}

if ($LeaseTimeoutMs -le 0) {
    # The barrier holds every submitter's clock running while the parent launches the rest, so the
    # budget has to cover the launch smear as well as the queue.
    $LeaseTimeoutMs = 30000 + ($Submitters * 500)
}
if ($BarrierSettleMs -le 0) {
    $BarrierSettleMs = 300 + ($Submitters * 15)
}
if ($ProcessTimeoutS -le 0) {
    $ProcessTimeoutS = [int]([math]::Ceiling($LeaseTimeoutMs / 1000.0)) + 120
}

Write-Rule 'HAMMER-WAVES - a committed, repeatable concurrency run against wave-cli'
Write-Host ('  run id            : ' + $RunId)
Write-Host ('  wave-cli          : ' + $WaveCli)
Write-Host ('  converter         : ' + $Converter)
Write-Host ('  ir project        : ' + $Project)
Write-Host ('  store root        : ' + $StoreRoot)
Write-Host ('  submitters        : ' + $Submitters)
Write-Host ('  rounds            : ' + $Rounds)
Write-Host ('  shapes            : ' + ($Shapes -join ', '))
Write-Host ('  arrival modes     : ' + ($arrivals -join ', '))
Write-Host ('  stagger ms        : ' + $StaggerMs)
Write-Host ('  cap               : ' + $Cap)
Write-Host ('  cap provenance    : ' + $CapProvenance)
Write-Host ('  lease timeout ms  : ' + $LeaseTimeoutMs)
Write-Host ('  barrier settle ms : ' + $BarrierSettleMs)
Write-Host ('  process timeout s : ' + $ProcessTimeoutS)
Write-Host ('  corpus blocks     : ' + ($Blocks -join ', '))
Write-Host ''
Write-Host '  CEILING NOTE: the Disjoint shape can never exceed ACHIEVED CONCURRENCY of'
Write-Host ('               ' + $Blocks.Count + ', because that is how many genuinely disjoint blocks the corpus holds.')
Write-Host '               That is a property of the corpus, not of wave-cli.'

if (-not (Test-Path -LiteralPath $WaveCli)) {
    Write-Host ''
    Write-Host ('REFUSED: wave-cli.exe not found at "' + $WaveCli + '". Nothing was measured.')
    exit $ExitUnusable
}

if ($DryRun) {
    Write-Host ''
    Write-Host 'DRY RUN: the plan above is what would be executed. No store was created and wave-cli was never invoked.'
    $planned = $Shapes.Count * $arrivals.Count
    Write-Host ('  scenarios: ' + $planned + '  submissions: ' + ($planned * $Submitters * $Rounds) + '  probes: ' + ($planned * 2))
    exit $ExitClean
}

# --- THE CLOSURE DOCUMENT. Computed, once, and its provenance is carried into every submission. ------
$needComputed = $false
foreach ($shape in $Shapes) {
    if ($shape -ne 'SyntheticDisjoint') { $needComputed = $true }
}

$runDir = Join-Path $StoreRoot ('run-' + $RunId)
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

if ($needComputed) {
    if ([string]::IsNullOrWhiteSpace($ReachableState)) {
        if (-not (Test-Path -LiteralPath $Converter)) {
            Write-Host ('REFUSED: converter.exe not found at "' + $Converter + '" and no -ReachableState file was given.')
            Write-Host '         The Disjoint and Conflicting shapes use the COMPUTED closure and will not fall back to a declared one.'
            exit $ExitUnusable
        }

        $ReachableState = Join-Path $runDir 'reachable-state.json'
        Write-Host ''
        Write-Host ('  computing the closure: converter reachable-state --project ' + $Project)
        $raw = & $Converter 'reachable-state' '--project' $Project '--json' 2>&1
        $convExit = $LASTEXITCODE
        if ($convExit -ne 0) {
            Write-Host ('REFUSED: converter reachable-state exited ' + $convExit + '. Nothing was measured.')
            Write-Host ($raw | Out-String)
            exit $ExitUnusable
        }
        ($raw | Out-String) | Out-File -FilePath $ReachableState -Encoding ascii
    }

    if (-not (Test-Path -LiteralPath $ReachableState)) {
        Write-Host ('REFUSED: closure document "' + $ReachableState + '" is not there.')
        exit $ExitUnusable
    }

    $closureText = Get-Content -LiteralPath $ReachableState -Raw
    foreach ($block in $Blocks) {
        if ($closureText -notmatch ('"block"\s*:\s*"' + [regex]::Escape($block) + '"')) {
            Write-Host ('REFUSED: the closure document does not carry block "' + $block + '".')
            Write-Host '         A submitter would be refused for a reason that has nothing to do with concurrency.'
            exit $ExitUnusable
        }
    }
    Write-Host ('  closure document: ' + $ReachableState + '  (all ' + $Blocks.Count + ' corpus blocks present)')
}

# =====================================================================================================
# THE RUN
# =====================================================================================================

$rows = New-Object System.Collections.Generic.List[object]
$problems = New-Object System.Collections.Generic.List[string]

foreach ($shape in $Shapes) {
    foreach ($mode in $arrivals) {

        $tag = $shape + '-' + $mode + '-n' + $Submitters
        $storeDir = Join-Path $runDir ('store-' + $tag)
        Write-Rule ('SCENARIO ' + $tag)

        New-Item -ItemType Directory -Path $storeDir -Force | Out-Null
        $outDir = Join-Path $storeDir '_out'
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null

        # --- RULE 6: the pre-flight echo check, before a single submission. -------------------------
        $pre = Invoke-WaveCli -Exe $WaveCli -Arguments @('status', '--store', $storeDir)
        $echoed = Get-EchoedStorePath -Text $pre.Text
        $intended = [System.IO.Path]::GetFullPath($storeDir)

        Write-Host ('  intended store : ' + $intended)
        Write-Host ('  ECHOED store   : ' + $echoed)

        if ([string]::IsNullOrWhiteSpace($echoed)) {
            $problems.Add($tag + ': the tool echoed no store path at pre-flight, so rule 6 could not be applied.')
            Write-Host '  *** THE TOOL ECHOED NO PATH. The argument was NOT taken as proof. ***'
        }
        elseif ($echoed -ne $intended) {
            $problems.Add($tag + ': ECHOED STORE "' + $echoed + '" IS NOT THE INTENDED "' + $intended + '".')
            Write-Host '  *** ECHOED PATH DIFFERS FROM THE ARGUMENT. This is the shadow-store trap. ***'
        }
        else {
            Write-Host '  store echo     : MATCHES (rule 6 satisfied against the tool output, not the argument)'
        }

        if ($pre.Exit -ne 2) {
            $problems.Add($tag + ': the store was not empty at pre-flight (status exit ' + $pre.Exit + '). Refusing to reconcile against a dirty store.')
            Write-Host ('  *** STORE NOT EMPTY AT PRE-FLIGHT (exit ' + $pre.Exit + '). ***')
            continue
        }

        # --- ROUNDS ----------------------------------------------------------------------------------
        $submitted = 0
        $admitted = 0
        $refused = 0
        $unusable = 0
        $noResult = 0
        $unnamed = 0
        $elapsedAll = New-Object System.Collections.Generic.List[int]
        $leaseWaitAll = New-Object System.Collections.Generic.List[int]
        $serviceAll = New-Object System.Collections.Generic.List[int]
        $contendedCount = 0
        $admittedIds = New-Object System.Collections.Generic.List[string]
        $roundWallMs = New-Object System.Collections.Generic.List[int]

        for ($round = 1; $round -le $Rounds; $round++) {

            $procs = @()
            $specs = @()

            for ($i = 1; $i -le $Submitters; $i++) {
                $slotId = ('S-' + $shape.Substring(0, 3).ToUpper() + '-' + $mode.Substring(0, 1) + '-n' + $Submitters + '-r' + $round + '-i' + $i)
                $agentId = ('hammer-a' + $i)

                $block = $Blocks[0]
                if ($shape -eq 'Disjoint') { $block = $Blocks[($i - 1) % $Blocks.Count] }

                $spec = [PSCustomObject]@{
                    Index   = $i
                    Slot    = $slotId
                    Agent   = $agentId
                    Block   = $block
                    Out     = (Join-Path $outDir ('r' + $round + '-i' + $i + '.out'))
                    Err     = (Join-Path $outDir ('r' + $round + '-i' + $i + '.err'))
                    Args    = @()
                    Proc    = $null
                    Exit    = $null
                    Text    = ''
                }

                $spec.Args = Get-SubmitArgs -StoreDir $storeDir -Agent $agentId -Slot $slotId `
                    -Shape $shape -Block $block -SyntheticName ('SYN-' + $tag + '-r' + $round + '-i' + $i) `
                    -ClosureFile $ReachableState -CapValue $Cap -CapText $CapProvenance -LeaseMs $LeaseTimeoutMs

                $specs += $spec
            }

            # --- THE BARRIER --------------------------------------------------------------------------
            $barrier = $null
            $leasePath = Join-Path $storeDir 'wave-store.lease'

            if ($mode -eq 'Simultaneous') {
                # The parent holds the store's own lease with FileShare.None. Every submitter queues on
                # it exactly as it would queue behind another agent, so the release is the arrival.
                $barrier = [System.IO.File]::Open($leasePath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                Write-Host ('  BARRIER: parent holds ' + $leasePath + ' exclusively; submitters will queue on it.')
            }
            else {
                Write-Host ('  BARRIER: none. Submitters are launched ' + $StaggerMs + ' ms apart (staggered arrival).')
            }

            $launchStart = [DateTime]::UtcNow
            foreach ($spec in $specs) {
                $spec.Proc = Start-Process -FilePath $WaveCli -ArgumentList $spec.Args -PassThru `
                    -WindowStyle Hidden -RedirectStandardOutput $spec.Out -RedirectStandardError $spec.Err

                # *** TOUCHING .Handle IS WHAT MAKES .ExitCode READABLE AT ALL, AND WITHOUT IT THIS
                # HARNESS SILENTLY MEASURES NOTHING. *** Measured 2026-08-14 while building this: a
                # Start-Process -PassThru object whose handle is never cached comes back with
                # HasExited = True and ExitCode = $null, so every submission would have been classified
                # from a null and the run would have reported a clean sweep of unreadable verdicts.
                # Caching the handle here populates ExitCode and ExitTime. The text-derived verdict
                # below is the SECOND, INDEPENDENT reading, kept because a single source that can fail
                # silently is exactly the shape this campaign exists to find.
                $null = $spec.Proc.Handle
                if ($mode -eq 'Staggered') {
                    if ($StaggerMs -gt 0) { Start-Sleep -Milliseconds $StaggerMs }
                }
            }
            $launchEnd = [DateTime]::UtcNow
            $launchSmearMs = [int]($launchEnd - $launchStart).TotalMilliseconds

            $releaseUtc = $launchEnd
            if ($mode -eq 'Simultaneous') {
                Start-Sleep -Milliseconds $BarrierSettleMs
                $earlyExits = 0
                foreach ($spec in $specs) {
                    if ($spec.Proc.HasExited) { $earlyExits++ }
                }
                if ($earlyExits -gt 0) {
                    Write-Host ('  NOTE: ' + $earlyExits + ' submitter(s) exited BEFORE the barrier dropped. They never contended.')
                }
                $releaseUtc = [DateTime]::UtcNow
                $barrier.Dispose()
                Write-Host ('  BARRIER RELEASED after a ' + $launchSmearMs + ' ms launch smear and a ' + $BarrierSettleMs + ' ms settle.')
            }

            # --- WAIT ---------------------------------------------------------------------------------
            # Waited on the Process OBJECT rather than by -Id: a submitter that has already exited is
            # not findable by process id, and Wait-Process would report the fastest agents as hung.
            $hung = 0
            foreach ($spec in $specs) {
                if (-not $spec.Proc.HasExited) {
                    $spec.Proc.WaitForExit($ProcessTimeoutS * 1000) | Out-Null
                }
                if (-not $spec.Proc.HasExited) {
                    $hung++
                    $problems.Add($tag + ' round ' + $round + ': submitter ' + $spec.Slot + ' did not exit within ' + $ProcessTimeoutS + ' s.')
                }
            }

            $lastExit = $releaseUtc
            foreach ($spec in $specs) {
                if ($spec.Proc.HasExited) {
                    $rawExitTime = $spec.Proc.ExitTime
                    if ($null -ne $rawExitTime) {
                        $exitUtc = $rawExitTime.ToUniversalTime()
                        if ($exitUtc -gt $lastExit) { $lastExit = $exitUtc }
                    }
                }
            }
            $wallMs = [int]($lastExit - $releaseUtc).TotalMilliseconds
            if ($wallMs -lt 0) { $wallMs = 0 }
            $roundWallMs.Add($wallMs)

            # --- COLLECT. Rule 3: a submitter with no readable verdict is NO-RESULT, never a pass. ----
            foreach ($spec in $specs) {
                $submitted++

                $text = (Read-TextOrEmpty -Path $spec.Out) + (Read-TextOrEmpty -Path $spec.Err)
                $spec.Text = $text

                if (-not $spec.Proc.HasExited) {
                    $noResult++
                    continue
                }

                $code = $spec.Proc.ExitCode
                $spec.Exit = $code

                # THE SECOND READING. Derived from the tool's own words, independently of the exit
                # code, so that "the exit code could not be read" and "the exit code says something the
                # output contradicts" come out as DIFFERENT facts instead of the same silence.
                $said = 'nothing'
                if ($text -cmatch 'ADMITTED: ') { $said = 'admitted' }
                elseif ($text -cmatch 'REFUSED') { $said = 'refused' }
                elseif ($text -cmatch '(STORE UNUSABLE|BAD INVOCATION|UNEXPECTED \()') { $said = 'unusable' }

                if ($null -eq $code) {
                    $noResult++
                    $problems.Add($tag + ': ' + $spec.Slot + ' exited with an UNREADABLE exit code (the process object carried none). Its output said "' + $said + '". A verdict that cannot be read is not a pass.')
                    continue
                }

                $expected = 'nothing'
                if ($code -eq 0) { $expected = 'admitted' }
                if ($code -eq 1) { $expected = 'refused' }
                if ($code -eq 2) { $expected = 'unusable' }
                # Exit 2 legitimately wears the word REFUSED: several unusable paths in Program.cs
                # print "REFUSED: ..." and return ExitUnusable. Accepting both there is a fact about
                # the tool's vocabulary, not a relaxation of the check - every other pairing still
                # has to agree.
                if (($code -eq 2) -and ($said -eq 'refused')) { $said = 'unusable' }

                if ($said -ne $expected) {
                    $problems.Add($tag + ': ' + $spec.Slot + ' exited ' + $code + ' (=' + $expected + ') but its output said "' + $said + '". The exit code and the text disagree.')
                }

                if ($code -eq 0) {
                    $admitted++
                    $admittedIds.Add($spec.Slot)
                    if ($text -notmatch 'ADMITTED') {
                        $unnamed++
                        $problems.Add($tag + ': ' + $spec.Slot + ' exited 0 with no ADMITTED line. A verdict with no text is not a verdict.')
                    }
                }
                elseif ($code -eq 1) {
                    $refused++
                    if ($text -notmatch 'REFUSED') {
                        $unnamed++
                        $problems.Add($tag + ': ' + $spec.Slot + ' exited 1 with no REFUSED line. A refusal must name itself.')
                    }
                }
                elseif ($code -eq 2) {
                    $unusable++
                    if ($text -notmatch '(STORE UNUSABLE|BAD INVOCATION|UNEXPECTED|REFUSED)') {
                        $unnamed++
                        $problems.Add($tag + ': ' + $spec.Slot + ' exited 2 with no named reason.')
                    }
                }
                else {
                    $noResult++
                    $problems.Add($tag + ': ' + $spec.Slot + ' exited ' + $code + ', which is outside wave-cli''s vocabulary (0/1/2). A crash is loud without being named.')
                }

                $thisElapsed = -1
                $thisLease = -1
                if ($text -match 'elapsed-ms=(\d+)') { $thisElapsed = [int]$Matches[1]; $elapsedAll.Add($thisElapsed) }
                if ($text -match 'lease-waited-ms=(\d+)') { $thisLease = [int]$Matches[1]; $leaseWaitAll.Add($thisLease) }
                if ($text -match 'lease-contended=yes') { $contendedCount++ }

                # *** SERVICE TIME = ELAPSED MINUS THE QUEUE, AND IT IS THE ONLY ONE OF THE THREE THAT
                # IS COMPARABLE ACROSS ARRIVAL MODES. *** Under the Simultaneous barrier a submitter's
                # elapsed time includes however long the parent held the lease, so quoting elapsed
                # alone would report the harness's own settle as though it were the tool's cost.
                if (($thisElapsed -ge 0) -and ($thisLease -ge 0)) {
                    $svc = $thisElapsed - $thisLease
                    if ($svc -lt 0) { $svc = 0 }
                    $serviceAll.Add($svc)
                }
            }

            Write-Host ('  round ' + $round + ': wall ' + $wallMs + ' ms, launch smear ' + $launchSmearMs + ' ms, hung ' + $hung)
        }

        # --- RECONCILE AGAINST THE STORE ON DISK ------------------------------------------------------
        $disk = Read-StoreFromDisk -StoreDir $storeDir

        $accounted = $admitted + $refused + $unusable + $noResult
        $vanished = $submitted - $accounted

        $onDiskFromRun = 0
        $missing = New-Object System.Collections.Generic.List[string]
        foreach ($id in $admittedIds) {
            if ($disk.Ids -contains $id) { $onDiskFromRun++ }
            else { $missing.Add($id) }
        }
        $strays = New-Object System.Collections.Generic.List[string]
        foreach ($id in $disk.Ids) {
            if (-not ($admittedIds -contains $id)) { $strays.Add($id) }
        }

        # --- THE COLOURING, READ BACK FROM THE STORE ON DISK ------------------------------------------
        $statusRes = Invoke-WaveCli -Exe $WaveCli -Arguments @('status', '--store', $storeDir, '--cap', [string]$Cap, '--cap-provenance', $CapProvenance)
        $achieved = -1
        $waves = -1
        $examined = -1
        $serialised = $false
        if ($statusRes.Text -match 'ACHIEVED CONCURRENCY (\d+)') { $achieved = [int]$Matches[1] }
        if ($statusRes.Text -match 'COLOURING: (\d+) wave set\(s\) over (\d+) slot\(s\)') {
            $waves = [int]$Matches[1]
            $examined = [int]$Matches[2]
        }
        # -cmatch on the exact sentence: a case-insensitive match on the bare word would also fire on
        # the lease refusal's "Serialised submission is the design", which is about something else.
        if ($statusRes.Text -cmatch 'SERIALISED: every wave set holds ONE slot') { $serialised = $true }

        # --- THE CONNECTIVITY PROBES ------------------------------------------------------------------
        # A campaign that never observes a REFUSAL has not shown the store is connected. Both probes
        # are made by an agent that submitted nothing, against state another agent wrote.
        $probeDup = 'NOT-RUN'
        $probeCap = 'NOT-RUN'

        if ($disk.Count -gt 0) {
            $victim = $disk.Ids[0]
            $dupArgs = Get-SubmitArgs -StoreDir $storeDir -Agent 'hammer-probe' -Slot $victim -Shape $shape `
                -Block $Blocks[0] -SyntheticName 'SYN-PROBE-DUP' -ClosureFile $ReachableState `
                -CapValue $Cap -CapText $CapProvenance -LeaseMs $LeaseTimeoutMs
            $dup = Invoke-WaveCli -Exe $WaveCli -Arguments $dupArgs
            if (($dup.Exit -eq 1) -and ($dup.Text -match 'already in the store')) { $probeDup = 'REFUSED-BY-NAME' }
            else {
                $probeDup = ('NOT-REFUSED (exit ' + $dup.Exit + ')')
                $problems.Add($tag + ': the duplicate-slot-id probe was NOT refused by name. A second agent could not see the first agent''s slot, which is the shared-store trap.')
            }
        }
        else {
            $problems.Add($tag + ': nothing reached the store, so the duplicate-id probe could not be run. Empty is not clean.')
        }

        $capArgs = New-Object System.Collections.Generic.List[string]
        $capArgs.Add('submit')
        $capArgs.Add('--store'); $capArgs.Add($storeDir)
        $capArgs.Add('--agent'); $capArgs.Add('hammer-probe')
        $capArgs.Add('--slot');  $capArgs.Add('S-PROBE-NOCAP-' + $tag)
        if ($shape -eq 'SyntheticDisjoint') {
            $capArgs.Add('--reaches'); $capArgs.Add('SYN-PROBE-NOCAP')
            $capArgs.Add('--reaches-from'); $capArgs.Add('SYNTHETIC probe closure.')
        }
        else {
            $capArgs.Add('--reachable-state'); $capArgs.Add($ReachableState)
            $capArgs.Add('--reachable-block'); $capArgs.Add($Blocks[0])
        }
        $noCap = Invoke-WaveCli -Exe $WaveCli -Arguments $capArgs.ToArray()
        if (($noCap.Exit -eq 1) -and ($noCap.Text -match 'WidthCapNotSupplied')) { $probeCap = 'REFUSED-BY-NAME' }
        else {
            $probeCap = ('NOT-REFUSED (exit ' + $noCap.Exit + ')')
            $problems.Add($tag + ': the missing-cap probe was NOT refused by name. The colouring would be UNCHECKED against bandwidth rather than passing it.')
        }

        $diskAfter = Read-StoreFromDisk -StoreDir $storeDir
        if ($diskAfter.Count -ne $disk.Count) {
            $problems.Add($tag + ': the refusal probes CHANGED the store from ' + $disk.Count + ' to ' + $diskAfter.Count + ' slots. A refusal must write nothing.')
        }

        # --- VERDICT ----------------------------------------------------------------------------------
        $verdict = 'RECONCILED'
        if ($disk.Torn) {
            $verdict = 'STORE-TORN'
            $problems.Add($tag + ': ' + $disk.Note)
        }
        if ($vanished -ne 0) {
            $verdict = 'VANISHED'
            $problems.Add($tag + ': ' + $vanished + ' submission(s) produced no verdict at all.')
        }
        if ($missing.Count -gt 0) {
            $verdict = 'ADMITTED-NOT-ON-DISK'
            $problems.Add($tag + ': ' + $missing.Count + ' slot(s) reported ADMITTED are NOT in the store on disk: ' + ($missing -join ', '))
        }
        if ($strays.Count -gt 0) {
            $verdict = 'ON-DISK-NOT-SUBMITTED'
            $problems.Add($tag + ': ' + $strays.Count + ' slot(s) are in the store that this run did not admit: ' + ($strays -join ', '))
        }
        if ($noResult -gt 0) { $verdict = 'NO-RESULT' }
        if ($unnamed -gt 0) { $verdict = 'UNNAMED-VERDICT' }
        if ($admitted -eq 0) {
            $verdict = 'NOTHING-ADMITTED'
            $problems.Add($tag + ': nothing was admitted. An empty wave set is not an admitted one.')
        }
        if ($probeDup -ne 'REFUSED-BY-NAME') { $verdict = 'PROBE-FAILED' }
        if ($probeCap -ne 'REFUSED-BY-NAME') { $verdict = 'PROBE-FAILED' }

        $wallTotal = 0
        foreach ($w in $roundWallMs) { $wallTotal += $w }

        $elapsedArr = $elapsedAll.ToArray()
        $leaseArr = $leaseWaitAll.ToArray()

        $minMs = -1
        $maxMs = -1
        if ($elapsedArr.Count -gt 0) {
            $minMs = ($elapsedArr | Measure-Object -Minimum).Minimum
            $maxMs = ($elapsedArr | Measure-Object -Maximum).Maximum
        }
        $medMs = Get-Median -Values $elapsedArr
        $maxLease = -1
        if ($leaseArr.Count -gt 0) { $maxLease = ($leaseArr | Measure-Object -Maximum).Maximum }

        $svcArr = $serviceAll.ToArray()
        $svcMin = -1
        $svcMax = -1
        if ($svcArr.Count -gt 0) {
            $svcMin = ($svcArr | Measure-Object -Minimum).Minimum
            $svcMax = ($svcArr | Measure-Object -Maximum).Maximum
        }
        $svcMed = Get-Median -Values $svcArr

        $closureKind = 'COMPUTED'
        if ($shape -eq 'SyntheticDisjoint') { $closureKind = 'DECLARED-NOT-COMPUTED' }

        $blocksUsed = 1
        if ($shape -eq 'Disjoint') { $blocksUsed = [math]::Min($Blocks.Count, $Submitters) }
        if ($shape -eq 'SyntheticDisjoint') { $blocksUsed = 0 }

        Write-Host ''
        Write-Host ('  SUBMITTED ' + $submitted + '  ADMITTED ' + $admitted + '  REFUSED ' + $refused + '  UNUSABLE ' + $unusable + '  NO-RESULT ' + $noResult + '  UNNAMED ' + $unnamed)
        Write-Host ('  ACCOUNTED ' + $accounted + ' of ' + $submitted + '  VANISHED ' + $vanished)
        Write-Host ('  ON DISK (parsed from wave-slots.state) ' + $disk.Count + '  declared ' + $disk.Declared + '  from this run ' + $onDiskFromRun + '  strays ' + $strays.Count)
        Write-Host ('  COLOURING (read back from the store) waves ' + $waves + '  examined ' + $examined + '  ACHIEVED CONCURRENCY ' + $achieved + '  serialised ' + $serialised)
        Write-Host ('  CLOSURE ' + $closureKind + '  distinct blocks ' + $blocksUsed + '  cap ' + $Cap)
        Write-Host ('  PROBE duplicate-id ' + $probeDup + '  PROBE missing-cap ' + $probeCap)
        Write-Host ('  DURATION wall ' + $wallTotal + ' ms over ' + $Rounds + ' round(s); per-agent elapsed min ' + $minMs + ' / med ' + $medMs + ' / max ' + $maxMs + ' ms; max lease wait ' + $maxLease + ' ms; contended ' + $contendedCount)
        Write-Host ('  SERVICE (elapsed minus lease queue, the cross-mode comparable) min ' + $svcMin + ' / med ' + $svcMed + ' / max ' + $svcMax + ' ms')
        Write-Host ('  VERDICT ' + $verdict)

        if ($serialised) {
            Write-Host '  NOTE: SERIALISED is a CORRECT RESULT for a shape whose slots all conflict. It is not a failure.'
        }

        $rows.Add([PSCustomObject]@{
                Shape      = $shape
                Arrival    = $mode
                Submitters = $Submitters
                Rounds     = $Rounds
                Closure    = $closureKind
                Blocks     = $blocksUsed
                Submitted  = $submitted
                Admitted   = $admitted
                Refused    = $refused
                Unusable   = $unusable
                NoResult   = $noResult
                OnDisk     = $disk.Count
                Waves      = $waves
                Achieved   = $achieved
                Serialised = $serialised
                Cap        = $Cap
                WallMs     = $wallTotal
                MinMs      = $minMs
                MedMs      = $medMs
                MaxMs      = $maxMs
                SvcMinMs   = $svcMin
                SvcMedMs   = $svcMed
                SvcMaxMs   = $svcMax
                LeaseMaxMs = $maxLease
                Contended  = $contendedCount
                ProbeDup   = $probeDup
                ProbeCap   = $probeCap
                Verdict    = $verdict
            })
    }
}

# =====================================================================================================
# THE TABLE
# =====================================================================================================

Write-Rule 'RUN TABLE'
$rows | Format-Table Shape, Arrival, Submitters, Rounds, Submitted, Admitted, Refused, NoResult, OnDisk, Waves, Achieved, WallMs, MedMs, Verdict -AutoSize | Out-String -Width 240 | Write-Host

Write-Host 'MACHINE-READABLE ROWS (pipe-delimited):'
Write-Host 'HAMMER-ROW|shape|arrival|submitters|rounds|closure|blocks|submitted|admitted|refused|unusable|noresult|ondisk|waves|achieved|serialised|cap|wall_ms|min_ms|med_ms|max_ms|svc_min_ms|svc_med_ms|svc_max_ms|lease_max_ms|contended|probe_dup|probe_cap|verdict'
foreach ($r in $rows) {
    Write-Host ('HAMMER-ROW|' + $r.Shape + '|' + $r.Arrival + '|' + $r.Submitters + '|' + $r.Rounds + '|' + $r.Closure + '|' + $r.Blocks + '|' + $r.Submitted + '|' + $r.Admitted + '|' + $r.Refused + '|' + $r.Unusable + '|' + $r.NoResult + '|' + $r.OnDisk + '|' + $r.Waves + '|' + $r.Achieved + '|' + $r.Serialised + '|' + $r.Cap + '|' + $r.WallMs + '|' + $r.MinMs + '|' + $r.MedMs + '|' + $r.MaxMs + '|' + $r.SvcMinMs + '|' + $r.SvcMedMs + '|' + $r.SvcMaxMs + '|' + $r.LeaseMaxMs + '|' + $r.Contended + '|' + $r.ProbeDup + '|' + $r.ProbeCap + '|' + $r.Verdict)
}

Write-Rule 'FINDINGS'
if ($problems.Count -eq 0) {
    Write-Host ('NONE. ' + $rows.Count + ' scenario(s) reconciled against the store on disk, every submission accounted for, every probe refused by name.')
    Write-Host 'THIS IS EVIDENCE ABOUT THE LEVELS ACTUALLY RUN AND NOTHING ELSE. A clean sweep is evidence about the range, never about the mechanism.'
}
else {
    Write-Host ($problems.Count.ToString() + ' finding(s):')
    foreach ($p in $problems) { Write-Host ('  - ' + $p) }
}

Write-Host ''
Write-Host ('STORES: ' + $runDir)
Write-Host '        This script NEVER deletes a store. The store on disk is the evidence every number above'
Write-Host '        was reconciled against, and a harness that tidies its own evidence away cannot be re-read.'

if ($rows.Count -eq 0) {
    Write-Host 'NOTHING WAS MEASURED.'
    exit $ExitUnusable
}
if ($problems.Count -gt 0) { exit $ExitDiscrepancy }
exit $ExitClean
