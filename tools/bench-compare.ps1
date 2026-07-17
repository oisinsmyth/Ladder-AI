<#
.SYNOPSIS
    Compares two bench-machine.ps1 result JSON files side by side.

.EXAMPLE
    .\tools\bench-compare.ps1 -Baseline .\tools\bench-results\PC1-....json -Candidate .\tools\bench-results\PC2-....json
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Baseline,
    [Parameter(Mandatory)][string]$Candidate
)

$ErrorActionPreference = "Stop"

$a = Get-Content $Baseline -Raw | ConvertFrom-Json
$b = Get-Content $Candidate -Raw | ConvertFrom-Json

function Row($label, $va, $vb, [switch]$HigherIsBetter) {
    if ($null -eq $va -or $null -eq $vb) { return }
    $ratio = if ($va -ne 0) { $vb / $va } else { $null }
    $note = ""
    if ($ratio) {
        if ($HigherIsBetter) {
            $note = if ($ratio -ge 1) { "{0}x faster" -f [math]::Round($ratio, 2) } else { "{0}x slower" -f [math]::Round(1 / $ratio, 2) }
        } else {
            $note = if ($ratio -le 1) { "{0}x faster" -f [math]::Round(1 / $ratio, 2) } else { "{0}x slower" -f [math]::Round($ratio, 2) }
        }
    }
    "{0,-28} {1,14} {2,14}   {3}" -f $label, $va, $vb, $note
}

Write-Host ""
Write-Host ("{0,-28} {1,14} {2,14}" -f "", $a.Hostname, $b.Hostname) -ForegroundColor Cyan
Write-Host ("-" * 72)

Write-Host "-- System --" -ForegroundColor Yellow
"{0,-28} {1}" -f "CPU (A)", $a.SystemInfo.CPU
"{0,-28} {1}" -f "CPU (B)", $b.SystemInfo.CPU
Row "Physical cores" $a.SystemInfo.PhysicalCores $b.SystemInfo.PhysicalCores -HigherIsBetter
Row "Logical cores"  $a.SystemInfo.LogicalCores  $b.SystemInfo.LogicalCores  -HigherIsBetter
Row "RAM (GB)"        $a.SystemInfo.RamGB         $b.SystemInfo.RamGB         -HigherIsBetter
"{0,-28} {1,14} {2,14}" -f "Disk media", $a.SystemInfo.DiskMediaType, $b.SystemInfo.DiskMediaType
"{0,-28} {1,14} {2,14}" -f ".NET SDK", $a.SystemInfo.DotnetSdkVersion, $b.SystemInfo.DotnetSdkVersion

Write-Host ""
Write-Host "-- Disk I/O --" -ForegroundColor Yellow
Row "Seq write MB/s"       $a.DiskSequential.WriteMBps $b.DiskSequential.WriteMBps -HigherIsBetter
Row "Seq read MB/s"        $a.DiskSequential.ReadMBps  $b.DiskSequential.ReadMBps  -HigherIsBetter
Row "Write 300 files (s)"  $a.DiskManySmallFiles.Write300Seconds  $b.DiskManySmallFiles.Write300Seconds
Row "Read 300 files (s)"   $a.DiskManySmallFiles.Read300Seconds   $b.DiskManySmallFiles.Read300Seconds
Row "Delete 300 files (s)" $a.DiskManySmallFiles.Delete300Seconds $b.DiskManySmallFiles.Delete300Seconds

Write-Host ""
Write-Host "-- Process spawn / CPU --" -ForegroundColor Yellow
Row "Avg spawn (ms)"    $a.ProcessSpawn.AvgMs $b.ProcessSpawn.AvgMs
Row "SHA-256 MB/s"      $a.CpuHash.MBps       $b.CpuHash.MBps -HigherIsBetter

if ($a.DotnetConverter -and $b.DotnetConverter) {
    Write-Host ""
    Write-Host "-- dotnet (Converter) --" -ForegroundColor Yellow
    Row "Build (s)" $a.DotnetConverter.BuildSeconds $b.DotnetConverter.BuildSeconds
    Row "Test (s)"  $a.DotnetConverter.TestSeconds  $b.DotnetConverter.TestSeconds
}

if ($a.DotnetOpennessCli -and $b.DotnetOpennessCli) {
    Write-Host ""
    Write-Host "-- dotnet (openness-cli) --" -ForegroundColor Yellow
    Row "Build (s)" $a.DotnetOpennessCli.BuildSeconds $b.DotnetOpennessCli.BuildSeconds
}

Write-Host ""
Write-Host "Note: lower seconds = faster; ratios above are candidate relative to baseline." -ForegroundColor DarkGray
