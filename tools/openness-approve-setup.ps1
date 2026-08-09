<#
.SYNOPSIS
    One-time, elevated: lets your normal (non-admin) account write TIA's Openness whitelist, so
    openness-approve-build.ps1 can approve each new build without a UAC prompt.

.DESCRIPTION
    TIA writes the whitelist under HKLM, elevated -- that UAC prompt is the second half of its own
    approval dialog. Without this setup, every rebuild trades one dialog for another.

    This grants ONE account read/write on ONE key:

        HKLM\SOFTWARE\Siemens\Automation\Openness\<version>\Whitelist\<exe name>

    Nothing wider. Other executables' whitelists, the Openness install and the rest of HKLM are
    untouched, so this does not become a general "approve anything" right -- it is scoped to the one
    program named by -ExeName.

    READ THIS BEFORE RUNNING IT. The whitelist is the control that stops arbitrary programs from
    driving TIA Portal. After this grant, anything running as the named account can add approvals for
    that one executable name without prompting -- including malware that drops its own
    openness-cli.exe somewhere and approves it. On a single-engineer workstation building its own
    tool, that is a fair trade for unattended automation. On a shared or exposed machine it is not.
    That is the machine owner's call, which is why this is a separate script, run deliberately,
    elevated, and undone with -Revoke.

.PARAMETER Identity
    Account to grant. Defaults to whoever runs this script. Pass it explicitly when elevating as a
    different principal than your desktop session -- the account that BUILDS is the one that needs
    the grant.

.PARAMETER ExeName
    Executable name whose whitelist key is granted. Default openness-cli.exe.

.PARAMETER Version
    Openness version key(s), e.g. '20.0'. Default: every version key present.

.PARAMETER Revoke
    Remove the grant instead of adding it.

.EXAMPLE
    # From an ELEVATED PowerShell, in the repo root:
    .\tools\openness-approve-setup.ps1

.EXAMPLE
    .\tools\openness-approve-setup.ps1 -Identity MYPC\oisin

.EXAMPLE
    .\tools\openness-approve-setup.ps1 -Revoke

.OUTPUTS
    Exit 0 = granted (or revoked).
    Exit 2 = no TIA Openness whitelist on this machine.
    Exit 3 = not elevated.
    Exit 1 = anything else.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Identity,
    [string]$ExeName = "openness-cli.exe",
    [string[]]$Version,
    [switch]$Revoke
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$WhitelistRoot = "SOFTWARE\Siemens\Automation\Openness"

function Exit-With($message, [int]$code) {
    [Console]::Error.WriteLine($message)
    exit $code
}

# ---------------------------------------------------------------------------
# Elevation, checked up front: a half-applied ACL change across several version keys is worse than a
# refusal before anything has been touched.

$principal = New-Object System.Security.Principal.WindowsPrincipal(
    [System.Security.Principal.WindowsIdentity]::GetCurrent())

if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $me = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    Write-Host ""
    Write-Host "This script must run ELEVATED -- it changes an HKLM key's permissions." -ForegroundColor Yellow
    Write-Host "Start PowerShell as administrator, then:"
    Write-Host "    cd '$((Get-Location).Path)'"
    Write-Host "    .\tools\openness-approve-setup.ps1 -Identity $me"
    Write-Host ""
    Write-Host "Pass -Identity explicitly: an elevated shell may run as a different account than your"
    Write-Host "desktop session, and the account that BUILDS is the one that needs the grant."
    exit 3
}

if (-not $Identity) { $Identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name }

# Resolve now, so a typo fails here instead of writing a rule for a non-existent account.
try {
    $account = New-Object System.Security.Principal.NTAccount($Identity)
    $null = $account.Translate([System.Security.Principal.SecurityIdentifier])
}
catch {
    Exit-With "Cannot resolve account '$Identity'. Use DOMAIN\user or MACHINE\user." 1
}

# ---------------------------------------------------------------------------

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
    }

    if ($versionNames.Count -eq 0) {
        Exit-With "No Openness version keys found under HKLM\$WhitelistRoot." 2
    }

    # ReadKey+WriteKey to add entries; Delete so -Prune can clear stale hashes. ContainerInherit so
    # the per-build entry subkeys inherit it -- without inheritance the first entry can be created
    # but never updated or removed.
    $rights = [System.Security.AccessControl.RegistryRights]::ReadKey `
        -bor [System.Security.AccessControl.RegistryRights]::WriteKey `
        -bor [System.Security.AccessControl.RegistryRights]::Delete
    $inheritance = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit
    $propagation = [System.Security.AccessControl.PropagationFlags]::None
    $allow = [System.Security.AccessControl.AccessControlType]::Allow

    foreach ($versionName in $versionNames) {
        $appKeyPath = "$WhitelistRoot\$versionName\Whitelist\$ExeName"
        $action = if ($Revoke) { "revoke registry write access for $Identity" }
                  else { "grant registry write access to $Identity" }

        if (-not $PSCmdlet.ShouldProcess("HKLM\$appKeyPath", $action)) { continue }

        # Created if absent: an exe's whitelist key only exists once something has been approved from
        # it, and the point here is to approve the first build without a human.
        $appKey = $baseKey.CreateSubKey($appKeyPath)
        try {
            $acl = $appKey.GetAccessControl()
            $rule = New-Object System.Security.AccessControl.RegistryAccessRule(
                $account, $rights, $inheritance, $propagation, $allow)

            if ($Revoke) {
                $acl.RemoveAccessRuleAll($rule)
                $appKey.SetAccessControl($acl)
                Write-Host "  [$versionName] revoked: $Identity no longer has explicit access to HKLM\$appKeyPath"
            }
            else {
                $acl.AddAccessRule($rule)
                $appKey.SetAccessControl($acl)
                Write-Host "  [$versionName] granted: $Identity can write HKLM\$appKeyPath"
            }
        }
        finally { $appKey.Dispose() }
    }

    Write-Host ""
    if ($Revoke) {
        Write-Host "Revoked. Approving a new build now needs elevation, or TIA's own dialog, again."
    }
    else {
        Write-Host "Done. Approve a build with no elevation and no dialog:"
        Write-Host "    .\tools\openness-approve-build.ps1"
        Write-Host ""
        Write-Host "Builds of src/openness-cli also self-approve from now on -- see the ApproveForOpenness"
        Write-Host "target in OpennessCli.csproj. Undo this grant at any time with -Revoke."
    }

    exit 0
}
finally { $baseKey.Dispose() }
