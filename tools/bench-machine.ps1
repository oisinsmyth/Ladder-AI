<#
.SYNOPSIS
    Benchmarks this machine on the axes that matter for working this repo:
    raw specs, disk I/O, process-spawn overhead (proxy for tool-call overhead),
    CPU throughput, and dotnet build/test times for the portable (non-TIA) projects.

.DESCRIPTION
    Run this on each machine you want to compare, then diff the resulting JSON
    files with bench-compare.ps1. Results are saved under tools/bench-results/
    by default, named <hostname>-<timestamp>.json so runs from different
    machines never collide.

    openness-cli is intentionally NOT built by default: it requires
    Siemens.Engineering.dll (TIA Openness install) and a $(TiaOpennessDir)
    env var, so it isn't a fair cross-machine comparison unless both machines
    have TIA Portal installed. Pass -IncludeOpennessCli to attempt it anyway
    (best-effort; failure is recorded, not fatal).

.EXAMPLE
    .\tools\bench-machine.ps1
    .\tools\bench-compare.ps1 -Baseline .\tools\bench-results\PC1-....json -Candidate .\tools\bench-results\PC2-....json
#>

[CmdletBinding()]
param(
    [string]$OutDir,
    [switch]$IncludeOpennessCli,
    [switch]$SkipDotnet
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
if (-not $OutDir) { $OutDir = Join-Path $scriptRoot "bench-results" }

function Write-Section($title) {
    Write-Host ""
    Write-Host "== $title ==" -ForegroundColor Cyan
}

function Time-Block([scriptblock]$block) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $result = & $block
    $sw.Stop()
    return [PSCustomObject]@{
        Seconds = [math]::Round($sw.Elapsed.TotalSeconds, 3)
        Result  = $result
    }
}

$results = [ordered]@{
    Hostname   = $env:COMPUTERNAME
    Timestamp  = (Get-Date).ToString("o")
    RepoCommit = $null
}

# ---------------------------------------------------------------------------
Write-Section "System info"

$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$os  = Get-CimInstance Win32_OperatingSystem
$mem = Get-CimInstance Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum
$sysDrive = (Get-Item $env:SystemDrive).PSDrive.Name
$disk = Get-PhysicalDisk | Select-Object -First 1 FriendlyName, MediaType, Size

$sysInfo = [ordered]@{
    CPU              = $cpu.Name.Trim()
    PhysicalCores    = $cpu.NumberOfCores
    LogicalCores     = $cpu.NumberOfLogicalProcessors
    MaxClockMHz      = $cpu.MaxClockSpeed
    RamGB            = [math]::Round(($mem.Sum / 1GB), 1)
    OS               = $os.Caption.Trim()
    OSBuild          = $os.BuildNumber
    DiskModel        = ($disk.FriendlyName -join ", ")
    DiskMediaType    = ($disk.MediaType -join ", ")
    DotnetSdkVersion = (dotnet --version 2>$null)
    PSVersion        = $PSVersionTable.PSVersion.ToString()
}
$results.SystemInfo = $sysInfo
$sysInfo.GetEnumerator() | ForEach-Object { Write-Host ("{0,-18}: {1}" -f $_.Key, $_.Value) }

try {
    $results.RepoCommit = (git -C $repoRoot rev-parse --short HEAD 2>$null)
} catch { }

# ---------------------------------------------------------------------------
Write-Section "Disk I/O (sequential 256 MB)"

$benchTmp = Join-Path $env:TEMP "ladder-ai-bench-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $benchTmp | Out-Null
try {
    $bigFile = Join-Path $benchTmp "seq.bin"
    $buf = New-Object byte[] (256MB)
    (New-Object Random).NextBytes($buf)

    $write = Time-Block {
        $fs = [System.IO.File]::Create($bigFile)
        try { $fs.Write($buf, 0, $buf.Length) } finally { $fs.Flush($true); $fs.Close() }
    }
    $writeMBs = [math]::Round(256 / $write.Seconds, 1)
    Write-Host ("Sequential write: {0} s  ({1} MB/s)" -f $write.Seconds, $writeMBs)

    $read = Time-Block {
        $fs = [System.IO.File]::OpenRead($bigFile)
        try {
            $rb = New-Object byte[] (1MB)
            while ($fs.Read($rb, 0, $rb.Length) -gt 0) { }
        } finally { $fs.Close() }
    }
    $readMBs = [math]::Round(256 / $read.Seconds, 1)
    Write-Host ("Sequential read:  {0} s  ({1} MB/s)" -f $read.Seconds, $readMBs)

    $results.DiskSequential = [ordered]@{
        WriteSeconds = $write.Seconds
        WriteMBps    = $writeMBs
        ReadSeconds  = $read.Seconds
        ReadMBps     = $readMBs
    }

    Write-Section "Many small files (300 files, ~2 KB each) - proxy for Read/Edit/Grep tool overhead"

    $smallDir = Join-Path $benchTmp "small"
    New-Item -ItemType Directory -Path $smallDir | Out-Null
    $smallBuf = New-Object byte[] 2048
    (New-Object Random).NextBytes($smallBuf)

    $writeMany = Time-Block {
        for ($i = 0; $i -lt 300; $i++) {
            [System.IO.File]::WriteAllBytes((Join-Path $smallDir "f$i.bin"), $smallBuf)
        }
    }
    $readMany = Time-Block {
        for ($i = 0; $i -lt 300; $i++) {
            [void][System.IO.File]::ReadAllBytes((Join-Path $smallDir "f$i.bin"))
        }
    }
    $deleteMany = Time-Block {
        Remove-Item (Join-Path $smallDir "*") -Force
    }
    Write-Host ("Write 300 files:  {0} s  ({1} files/s)" -f $writeMany.Seconds, [math]::Round(300 / $writeMany.Seconds, 0))
    Write-Host ("Read 300 files:   {0} s  ({1} files/s)" -f $readMany.Seconds, [math]::Round(300 / $readMany.Seconds, 0))
    Write-Host ("Delete 300 files: {0} s" -f $deleteMany.Seconds)

    $results.DiskManySmallFiles = [ordered]@{
        Write300Seconds  = $writeMany.Seconds
        Read300Seconds   = $readMany.Seconds
        Delete300Seconds = $deleteMany.Seconds
    }
} finally {
    Remove-Item $benchTmp -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
Write-Section "Process spawn overhead (30x cmd.exe, proxy for shell tool-call latency)"

$spawn = Time-Block {
    for ($i = 0; $i -lt 30; $i++) {
        & cmd.exe /c "exit 0" | Out-Null
    }
}
$spawnAvgMs = [math]::Round(($spawn.Seconds / 30) * 1000, 1)
Write-Host ("30 spawns: {0} s  ({1} ms/spawn avg)" -f $spawn.Seconds, $spawnAvgMs)
$results.ProcessSpawn = [ordered]@{
    Total30Seconds = $spawn.Seconds
    AvgMs          = $spawnAvgMs
}

# ---------------------------------------------------------------------------
Write-Section "CPU throughput (SHA-256 over 512 MB)"

$cpuBench = Time-Block {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $chunk = New-Object byte[] (16MB)
    (New-Object Random).NextBytes($chunk)
    for ($i = 0; $i -lt 32; $i++) {
        [void]$sha.ComputeHash($chunk)
    }
}
$cpuMBs = [math]::Round(512 / $cpuBench.Seconds, 1)
Write-Host ("SHA-256 512 MB: {0} s  ({1} MB/s)" -f $cpuBench.Seconds, $cpuMBs)
$results.CpuHash = [ordered]@{
    Seconds = $cpuBench.Seconds
    MBps    = $cpuMBs
}

# ---------------------------------------------------------------------------
if (-not $SkipDotnet) {
    Write-Section "dotnet build/test (Converter - portable, no TIA dependency)"

    $converterProj = Join-Path $repoRoot "src\converter\Converter\Converter.csproj"
    $converterTestProj = Join-Path $repoRoot "src\converter\Converter.Tests\Converter.Tests.csproj"

    & dotnet clean $converterProj -c Release --nologo | Out-Null

    $build = Time-Block {
        & dotnet build $converterProj -c Release --nologo 2>&1
    }
    $buildOk = $LASTEXITCODE -eq 0
    Write-Host ("Build: {0} s  (exit {1})" -f $build.Seconds, $LASTEXITCODE)

    $test = Time-Block {
        & dotnet test $converterTestProj -c Release --nologo 2>&1
    }
    $testOk = $LASTEXITCODE -eq 0
    Write-Host ("Test:  {0} s  (exit {1})" -f $test.Seconds, $LASTEXITCODE)

    $results.DotnetConverter = [ordered]@{
        BuildSeconds = $build.Seconds
        BuildOk      = $buildOk
        TestSeconds  = $test.Seconds
        TestOk       = $testOk
    }

    if ($IncludeOpennessCli) {
        Write-Section "dotnet build openness-cli (best-effort - requires TIA Openness install)"
        $opennessProj = Join-Path $repoRoot "src\openness-cli\OpennessCli\OpennessCli.csproj"
        & dotnet clean $opennessProj -c Release --nologo 2>&1 | Out-Null
        $obuild = Time-Block {
            & dotnet build $opennessProj -c Release --nologo 2>&1
        }
        $obuildOk = $LASTEXITCODE -eq 0
        Write-Host ("Build: {0} s  (exit {1}, ok={2})" -f $obuild.Seconds, $LASTEXITCODE, $obuildOk)
        $results.DotnetOpennessCli = [ordered]@{
            BuildSeconds = $obuild.Seconds
            BuildOk      = $obuildOk
        }
    }
} else {
    Write-Section "dotnet build/test skipped (-SkipDotnet)"
}

# ---------------------------------------------------------------------------
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
$outFile = Join-Path $OutDir ("{0}-{1}.json" -f $env:COMPUTERNAME, $stamp)
$results | ConvertTo-Json -Depth 6 | Set-Content -Path $outFile -Encoding utf8

Write-Section "Done"
Write-Host "Results written to: $outFile"
Write-Host "Copy this file to/from the other machine, then run:"
Write-Host "  .\tools\bench-compare.ps1 -Baseline <fileA> -Candidate <fileB>"
