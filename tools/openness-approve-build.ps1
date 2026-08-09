<#
.SYNOPSIS
    Pre-approves a freshly built Openness caller (openness-cli.exe) in TIA Portal's whitelist, so
    nobody has to sit at the machine and accept a dialog after every rebuild.

.DESCRIPTION
    FI-61: TIA approves an Openness caller by (Path, FileHash). Any rebuild produces a binary TIA has
    never seen, and a build it has never seen needs a PERSON to approve it at the machine. That costs
    seconds interactively and blocks completely unattended, where nobody accepts and the attach sits
    until --timeout-connect expires.

    Accepting that dialog does nothing more than write a registry entry:

        HKLM\SOFTWARE\Siemens\Automation\Openness\<version>\Whitelist\<exe name>\<entry>
            Path         = C:\...\bin\Debug\net48\openness-cli.exe
            DateModified = 2026/07/10 08:40:17.964    <- the file's LastWriteTime, that exact format
            FileHash     = PY8nw9ndT2J/qsmq1G289KwAEM10YkvRPjcY1j5MlTE=   <- base64 of SHA-256

    This script writes that entry directly. Same result, no dialog, no human.

    It is deliberately NOT a subcommand of openness-cli.exe. Approving a caller is a security
    decision, and it stays a separate, readable, auditable act the machine owner opts into rather
    than a capability the Portal-touching binary grants itself. It is also a chicken-and-egg problem:
    the binary that needs approval cannot be the thing that approves it before it can run.

    WHAT THIS COSTS, STATED PLAINLY. The whitelist is what stops arbitrary programs from driving TIA
    Portal. After openness-approve-setup.ps1, any process running as the granted account can approve
    that executable name without prompting. On a single-engineer workstation building its own tool
    that is a fair trade for unattended automation; on a shared machine it is not. Both halves are
    reversible: -Prune here, and -Revoke in the setup script.

.PARAMETER Exe
    Executable to approve. Defaults to this repo's Debug build of openness-cli.exe. Approval is per
    PATH as well as per hash, so a worktree build is a different application to Openness even from
    identical source -- pass its own path.

.PARAMETER Version
    Openness version key(s) to write under, e.g. '20.0'. Default: every version key present. Several
    majors coexist on an engineering PC and an entry under any of them is an approval, so writing all
    of them is the safe default.

.PARAMETER Prune
    After approving, delete this path's OTHER entries -- the stale hashes of previous builds. The
    whitelist accumulates one entry per approved build and had 84 for a single Debug path here.
    Nothing is ever deleted without this switch.

.PARAMETER Status
    Read-only. Dumps what TIA has actually stored for this exe name -- every entry, its path, its
    hash, its value names, and which one (if any) is this build. Nothing is written. Ask this before
    believing any verdict here, since every verdict rests on matching TIA's own records.

.PARAMETER Quiet
    One summary line instead of per-version detail. Used by the post-build hook.

.EXAMPLE
    .\tools\openness-approve-build.ps1

.EXAMPLE
    .\tools\openness-approve-build.ps1 -Exe .\src\openness-cli\OpennessCli\bin\Release\net48\openness-cli.exe -Prune

.EXAMPLE
    .\tools\openness-approve-build.ps1 -WhatIf
    Show what would be written, touching nothing.

.OUTPUTS
    Exit 0 = approved, or already approved (idempotent -- re-running is a no-op).
    Exit 2 = no TIA Openness whitelist on this machine (not installed, or unexpected layout).
    Exit 3 = access denied writing HKLM. Run tools\openness-approve-setup.ps1 once, elevated.
    Exit 1 = anything else.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Exe,
    [string[]]$Version,
    [switch]$Prune,
    [switch]$Status,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$WhitelistRoot = "SOFTWARE\Siemens\Automation\Openness"

# The three values TIA itself writes. Anything else found in a real entry is reported rather than
# ignored: an unknown value is the one way this script could write a dead entry that looks approved,
# and it should surface the moment it appears instead of during the next unattended run.
$KnownValueNames = @("Path", "DateModified", "FileHash")

function Write-Detail($message) { if (-not $Quiet) { Write-Host $message } }

# Write-Error terminates under ErrorActionPreference=Stop and yields exit 1, which would throw away
# every exit code this script documents. Failures go through here instead.
function Exit-With($message, [int]$code) {
    [Console]::Error.WriteLine($message)
    exit $code
}

<#
    Entry subkey names are derived from the siblings TIA created, never guessed: this machine has
    them as 'Entry (N)', but that shape is an observation, not a contract. Copying an existing
    sibling's prefix and suffix and taking the next free number keeps a hand-written entry
    indistinguishable from one TIA wrote, whatever the local convention turns out to be.
#>
function New-EntryName([string[]]$ExistingNames) {
    $prefix = "Entry ("
    $suffix = ")"
    $next = 0

    foreach ($name in $ExistingNames) {
        if ($name -match '^(?<prefix>.*?)(?<n>\d+)(?<suffix>\D*)$') {
            $prefix = $Matches['prefix']
            $suffix = $Matches['suffix']
            $n = [int]$Matches['n']
            if ($n -ge $next) { $next = $n + 1 }
        }
    }

    # Siblings with no number at all (a lone 'Entry') leave $next at 0, which would collide on the
    # second run; step past anything already taken either way.
    $candidate = "$prefix$next$suffix"
    while ($ExistingNames -contains $candidate) {
        $next++
        $candidate = "$prefix$next$suffix"
    }

    return $candidate
}

# ---------------------------------------------------------------------------
# The executable, its hash, and its timestamp

if (-not $Exe) {
    $repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
    $Exe = Join-Path $repoRoot "src\openness-cli\OpennessCli\bin\Debug\net48\openness-cli.exe"
}

if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
    Exit-With "Executable not found: $Exe`nBuild it first, or pass -Exe with the path to approve." 1
}

$file = Get-Item -LiteralPath $Exe
$exePath = $file.FullName
$exeName = $file.Name

# Base64 of SHA-256 -- the form TIA stores. Get-FileHash returns hex, so hash the bytes directly.
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $stream = [System.IO.File]::OpenRead($exePath)
    try { $fileHash = [Convert]::ToBase64String($sha.ComputeHash($stream)) }
    finally { $stream.Dispose() }
}
finally { $sha.Dispose() }

# TIA's own format string, verbatim. Written from the file's real LastWriteTime so the entry stays
# self-consistent if TIA ever uses it as a pre-check rather than decoration.
$dateModified = $file.LastWriteTime.ToString("yyyy'/'MM'/'dd HH:mm:ss.fff")

Write-Detail ""
Write-Detail "Approving for TIA Openness:"
Write-Detail "  Path         $exePath"
Write-Detail "  FileHash     $fileHash"
Write-Detail "  DateModified $dateModified"

# ---------------------------------------------------------------------------
# The whitelist

# Registry64 explicitly. A 32-bit host is redirected to WOW6432Node, where TIA does not look -- the
# entry would be written, this script would report success, and the dialog would still appear.
$baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
    [Microsoft.Win32.RegistryHive]::LocalMachine,
    [Microsoft.Win32.RegistryView]::Registry64)

try {
    $root = $baseKey.OpenSubKey($WhitelistRoot, $false)
    if (-not $root) {
        Exit-With "No TIA Openness whitelist at HKLM\$WhitelistRoot -- is TIA Portal Openness installed?" 2
    }

    try {
        $versionNames = @($root.GetSubKeyNames() | Where-Object { $_ -match '^\d+(\.\d+)*$' })
    }
    finally { $root.Dispose() }

    if ($Version) {
        $requested = $Version
        $versionNames = @($versionNames | Where-Object { $requested -contains $_ })
        if ($versionNames.Count -eq 0) {
            Exit-With "None of the requested version(s) [$($requested -join ', ')] exist under HKLM\$WhitelistRoot." 2
        }
    }

    if ($versionNames.Count -eq 0) {
        Exit-With "HKLM\$WhitelistRoot exists but contains no version keys." 2
    }

    # Read before writing. Re-running must be a no-op, and the read path needs no privilege, so an
    # already-approved build never touches write access at all.
    function Read-Entries($BaseKey, [string]$KeyPath) {
        $entries = @()
        $appKeyRead = $BaseKey.OpenSubKey($KeyPath, $false)
        if (-not $appKeyRead) { return $entries }
        try {
            foreach ($entryName in $appKeyRead.GetSubKeyNames()) {
                $entryKey = $appKeyRead.OpenSubKey($entryName, $false)
                if (-not $entryKey) { continue }
                try {
                    $entries += [PSCustomObject]@{
                        Name       = $entryName
                        Path       = [string]$entryKey.GetValue("Path")
                        FileHash   = [string]$entryKey.GetValue("FileHash")
                        ValueNames = @($entryKey.GetValueNames())
                    }
                }
                finally { $entryKey.Dispose() }
            }
        }
        finally { $appKeyRead.Dispose() }
        return $entries
    }

    # Read-only. Answers "what did TIA actually store?" -- the question worth asking before believing
    # anything this script concludes, since every verdict here rests on matching TIA's own records.
    if ($Status) {
        foreach ($versionName in $versionNames) {
            $appKeyPath = "$WhitelistRoot\$versionName\Whitelist\$exeName"
            $entries = @(Read-Entries $baseKey $appKeyPath)
            Write-Host ""
            Write-Host "[$versionName] $($entries.Count) entry/entries under HKLM\$appKeyPath"

            $forThisPath = @($entries | Where-Object {
                $_.Path -and $_.Path.Equals($exePath, [System.StringComparison]::OrdinalIgnoreCase) })
            Write-Host "        $($forThisPath.Count) name this exact path; $(@($forThisPath | Where-Object { $_.FileHash -ceq $fileHash }).Count) match this file's hash"

            foreach ($e in $entries) {
                $mark = if ($e.FileHash -ceq $fileHash -and $e.Path -and
                            $e.Path.Equals($exePath, [System.StringComparison]::OrdinalIgnoreCase)) { "THIS BUILD" }
                        elseif ($e.Path -and $e.Path.Equals($exePath, [System.StringComparison]::OrdinalIgnoreCase)) { "same path, older hash" }
                        else { "other path" }
                Write-Host "  $($e.Name.PadRight(12)) $mark"
                Write-Host "        path   $($e.Path)"
                Write-Host "        hash   $($e.FileHash)"
                Write-Host "        values $($e.ValueNames -join ', ')"
            }
        }
        Write-Host ""
        exit 0
    }

    $approvedCount = 0
    $alreadyCount = 0
    $prunedCount = 0

    foreach ($versionName in $versionNames) {
        $appKeyPath = "$WhitelistRoot\$versionName\Whitelist\$exeName"
        $existingEntries = @(Read-Entries $baseKey $appKeyPath)

        # Paths compare case-insensitively (Windows); base64 hashes compare exactly. Same rule as
        # OpennessWhitelist.Evaluate in the CLI, which reads this same data.
        $samePath = @($existingEntries | Where-Object {
            $_.Path -and $_.Path.Equals($exePath, [System.StringComparison]::OrdinalIgnoreCase) })
        $match = @($samePath | Where-Object { $_.FileHash -ceq $fileHash })

        if ($match.Count -gt 0 -and -not $Prune) {
            Write-Detail "  [$versionName] already approved (entry '$($match[0].Name)')."
            $alreadyCount++
            continue
        }

        # Surface any value TIA writes that this script does not, rather than writing a dead entry.
        $unknown = @($samePath | ForEach-Object { $_.ValueNames } |
            Where-Object { $KnownValueNames -notcontains $_ } | Select-Object -Unique)
        if ($unknown.Count -gt 0) {
            Write-Warning ("Existing whitelist entries carry value(s) this script does not write: " +
                ($unknown -join ", ") + ". If approval still prompts, that is the first place to look.")
        }

        try {
            if ($match.Count -eq 0) {
                # Named BEFORE ShouldProcess so -WhatIf shows the whole change. Computing it inside
                # left the dry run unable to say what it would create, which is the one detail a
                # reviewer of this script most wants to see.
                $entryName = New-EntryName -ExistingNames @($existingEntries | ForEach-Object { $_.Name })

                if ($PSCmdlet.ShouldProcess("HKLM\$appKeyPath", "add whitelist entry '$entryName' for $exeName")) {
                    $appKey = $baseKey.CreateSubKey($appKeyPath)
                    try {
                        $entryKey = $appKey.CreateSubKey($entryName)
                        try {
                            $entryKey.SetValue("Path", $exePath, [Microsoft.Win32.RegistryValueKind]::String)
                            $entryKey.SetValue("DateModified", $dateModified, [Microsoft.Win32.RegistryValueKind]::String)
                            $entryKey.SetValue("FileHash", $fileHash, [Microsoft.Win32.RegistryValueKind]::String)
                        }
                        finally { $entryKey.Dispose() }
                        Write-Detail "  [$versionName] approved (entry '$entryName')."
                        $approvedCount++
                    }
                    finally { $appKey.Dispose() }
                }
            }
            else {
                Write-Detail "  [$versionName] already approved (entry '$($match[0].Name)')."
                $alreadyCount++
            }

            if ($Prune) {
                $stale = @($samePath | Where-Object { $_.FileHash -cne $fileHash })
                if ($stale.Count -gt 0 -and
                    $PSCmdlet.ShouldProcess("HKLM\$appKeyPath", "delete $($stale.Count) stale entry/entries for this path")) {
                    $appKey = $baseKey.OpenSubKey($appKeyPath, $true)
                    if ($appKey) {
                        try {
                            foreach ($s in $stale) {
                                $appKey.DeleteSubKeyTree($s.Name, $false)
                                $prunedCount++
                            }
                        }
                        finally { $appKey.Dispose() }
                    }
                    Write-Detail "  [$versionName] pruned $($stale.Count) stale entry/entries for this path."
                }
            }
        }
        catch [System.UnauthorizedAccessException] {
            Write-Host ""
            Write-Host "ACCESS DENIED writing HKLM\$appKeyPath" -ForegroundColor Yellow
            Write-Host "TIA writes this key elevated -- that is the UAC prompt behind its own approval dialog."
            Write-Host "Run this ONCE, elevated, and this script never needs elevation again:"
            Write-Host "    .\tools\openness-approve-setup.ps1"
            Write-Host "Or approve just this build now by re-running the present script as Administrator."
            exit 3
        }
        catch [System.Security.SecurityException] {
            Write-Host ""
            Write-Host "ACCESS DENIED writing HKLM\$appKeyPath -- run .\tools\openness-approve-setup.ps1 once, elevated." -ForegroundColor Yellow
            exit 3
        }
    }

    if ($Quiet) {
        $summary = "openness whitelist: $approvedCount approved, $alreadyCount already approved"
        if ($Prune) { $summary += ", $prunedCount pruned" }
        Write-Host "$summary -- $exeName"
    }
    else {
        Write-Detail ""
        # "Nothing was written" has three very different causes and they must not share a message:
        # an earlier version reported a -WhatIf run as "already approved", which is the exact false
        # reassurance this tool exists to remove.
        if ($approvedCount -gt 0) {
            Write-Detail "Done. This build can now connect without anyone accepting a dialog."
        }
        elseif ($alreadyCount -gt 0) {
            Write-Detail "Nothing to do -- this build was already approved."
        }
        elseif ($WhatIfPreference) {
            Write-Detail "-WhatIf: nothing was written. The lines above are what a real run would add."
        }
        else {
            Write-Detail "Nothing was written, and nothing matched. Run with -Status to see what is stored."
        }
        Write-Detail "Verify empirically, which is the only proof that counts: run any openness-cli"
        Write-Detail "command that attaches to Portal and confirm it connects without prompting."
        Write-Detail ""
    }

    exit 0
}
finally { $baseKey.Dispose() }
