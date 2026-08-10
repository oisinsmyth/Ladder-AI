<#
.SYNOPSIS
    Fallback: watches TIA Portal for the Openness approval dialog and accepts it, so an unattended
    run is not blocked waiting for a person.

.DESCRIPTION
    The primary fix is openness-approve-build.ps1, which writes the whitelist entry directly so the
    dialog is never raised at all. Prefer it: nothing to time, nothing to find, nothing to click.

    Use this when that is not available -- no elevated grant on the machine, an approval path that
    turns out not to be registry-backed, or a dialog raised by something other than a rebuild.

    SAFETY, BECAUSE THIS CLICKS BUTTONS INSIDE AN ENGINEERING TOOL. A robot that accepts dialogs in
    TIA Portal can accept one you badly wanted to read -- overwrite, delete, discard changes. So:

      * It observes and reports by default and clicks NOTHING unless you pass -Click.
      * It acts only inside a window whose visible text matches -TextPattern, and only on a button
        named by -ButtonPreference. Both are yours to tighten.
      * Every action prints the full dialog text that justified it, so the log shows what was
        accepted and why.

    Run it in observe mode first, trigger the dialog once, and read the exact wording it captures on
    YOUR TIA build and language. Then set -TextPattern from that text and add -Click. Guessing the
    wording and enabling clicks in one step is how a robot accepts the wrong dialog.

.PARAMETER Click
    Actually invoke the matching button. Without it nothing is clicked, only reported.

.PARAMETER TextPattern
    Regex the dialog's visible text must match before any button is considered.

.PARAMETER ButtonPreference
    Button names to accept, in order of preference -- the earliest one present in the dialog wins,
    wherever it sits in the tree. "Yes to all" first, so one click covers this build and the next.

.PARAMETER ProcessName
    Process name pattern for TIA Portal.

.PARAMETER TimeoutMinutes
    Stop watching after this long. 0 = watch until Ctrl+C.

.PARAMETER PollSeconds
    Seconds between scans.

.PARAMETER Once
    Exit after the first dialog handled.

.PARAMETER Deep
    Also scan the full window subtree, not just child dialog windows. Needed if the approval is a WPF
    in-window overlay with no window of its own -- slower on TIA's large visual tree, so it is opt-in.

.EXAMPLE
    .\tools\openness-approve-watch-dialog.ps1 -TimeoutMinutes 5
    Observe only: report any approval dialog and what it WOULD click.

.EXAMPLE
    Start-Process powershell -ArgumentList '-NoProfile','-File','.\tools\openness-approve-watch-dialog.ps1','-Click','-Once'
    Arm the watcher, then run the openness-cli command that needs approval.

.OUTPUTS
    Exit 0 = handled (or, without -Click, observed) at least one dialog.
    Exit 4 = timed out with no dialog seen.
    Exit 2 = no TIA Portal process running while watching.
#>
[CmdletBinding()]
param(
    [switch]$Click,
    [string]$TextPattern = "(?i)openness|access to the TIA Portal|external application",
    [string[]]$ButtonPreference = @("Yes to all", "Ja, alle", "Yes", "Ja", "Approve", "Allow", "OK"),
    [string]$ProcessName = "Siemens.Automation.Portal*",
    [int]$TimeoutMinutes = 10,
    [int]$PollSeconds = 2,
    [switch]$Once,
    [switch]$Deep,
    [switch]$Dump
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

function Get-Descendants($element, $controlType, $scope) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $controlType)
    try { return @($element.FindAll($scope, $condition)) } catch { return @() }
}

function Get-VisibleText($element) {
    $parts = @()
    if ($element.Current.Name) { $parts += $element.Current.Name }
    foreach ($t in (Get-Descendants $element $CT::Text $Scope::Descendants)) {
        if ($t.Current.Name) { $parts += $t.Current.Name }
    }
    return ($parts -join " | ")
}

# Preference order decides, not tree order: "Yes to all" wins over "Yes" no matter which the tree
# walk reaches first. Names are compared with the accelerator underscore stripped, since a WPF
# button reports "_Yes to all" as often as "Yes to all".
function Select-Button($element) {
    $buttons = @(Get-Descendants $element $CT::Button $Scope::Descendants)
    if ($buttons.Count -eq 0) { return $null }

    foreach ($wanted in $ButtonPreference) {
        foreach ($b in $buttons) {
            $name = $b.Current.Name
            if (-not $name) { continue }
            if (($name -replace '_', '').Trim() -ieq $wanted) { return $b }
        }
    }

    return $null
}

function Invoke-Button($button) {
    $pattern = $null
    if ($button.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        return $true
    }
    return $false
}

$deadline = if ($TimeoutMinutes -gt 0) { (Get-Date).AddMinutes($TimeoutMinutes) } else { [datetime]::MaxValue }
$handled = 0
$sawPortal = $false
$mode = if ($Click) { "CLICK -- will accept matching dialogs" }
        else { "OBSERVE ONLY -- nothing will be clicked (pass -Click to act)" }

Write-Host ""
Write-Host "Watching for the TIA Openness approval dialog." -ForegroundColor Cyan
Write-Host "  mode              $mode"
Write-Host "  text must match   $TextPattern"
Write-Host "  buttons, in order $($ButtonPreference -join ' > ')"
Write-Host ""

while ((Get-Date) -lt $deadline) {
    $portals = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
    if ($portals.Count -gt 0) { $sawPortal = $true }

    foreach ($portal in $portals) {
        $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $portal.Id)
        $windows = @()
        try { $windows = @($AE::RootElement.FindAll($Scope::Children, $condition)) } catch { continue }

        foreach ($window in $windows) {
            # Child windows first: a real modal dialog is one, and this scan is cheap. -Deep adds the
            # window itself, which is what finds a WPF overlay that owns no window of its own.
            $candidates = @(Get-Descendants $window $CT::Window $Scope::Descendants)
            if ($Deep) { $candidates += $window }
            if ($candidates.Count -eq 0) { continue }

            foreach ($candidate in $candidates) {
                $text = ""
                try { $text = Get-VisibleText $candidate } catch { continue }
                if (-not $text -or $text -notmatch $TextPattern) { continue }

                $stamp = (Get-Date).ToString("HH:mm:ss")

                # -Dump exists because the first live encounter with the real dialog matched its text
                # and then matched no button at all: the preference list was guesswork. Printing the
                # whole subtree is how you replace a guess with the actual control names.
                if ($Dump) {
                    Write-Host "[$stamp] DUMP of: $($candidate.Current.Name)" -ForegroundColor Cyan
                    $all = @()
                    try { $all = @($candidate.FindAll($Scope::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) } catch { }
                    Write-Host "         $($all.Count) descendant element(s)"
                    foreach ($el in $all) {
                        $type = "?"
                        try { $type = $el.Current.ControlType.ProgrammaticName -replace '^ControlType\.', '' } catch { }
                        Write-Host ("         {0,-14} name='{1}' id='{2}' enabled={3}" -f
                            $type, $el.Current.Name, $el.Current.AutomationId, $el.Current.IsEnabled)
                    }
                    continue
                }

                $button = Select-Button $candidate

                if (-not $button) {
                    Write-Host "[$stamp] dialog text matched but no button matched the preference list:" -ForegroundColor Yellow
                    Write-Host "         $text"
                    continue
                }

                if (-not $Click) {
                    Write-Host "[$stamp] WOULD CLICK '$($button.Current.Name)'" -ForegroundColor Yellow
                    Write-Host "         dialog text: $text"
                    Write-Host "         re-run with -Click to accept it for real."
                    $handled++
                    if ($Once) { exit 0 }
                    continue
                }

                if (Invoke-Button $button) {
                    Write-Host "[$stamp] CLICKED '$($button.Current.Name)'" -ForegroundColor Green
                    Write-Host "         dialog text: $text"
                    $handled++
                    if ($Once) { exit 0 }
                }
                else {
                    Write-Host "[$stamp] '$($button.Current.Name)' matched but exposes no Invoke pattern -- not clicked." -ForegroundColor Yellow
                }
            }
        }
    }

    Start-Sleep -Seconds $PollSeconds
}

Write-Host ""
if (-not $sawPortal) {
    Write-Host "No TIA Portal process matched '$ProcessName' while watching." -ForegroundColor Yellow
    exit 2
}
if ($handled -eq 0) {
    Write-Host "Timed out after $TimeoutMinutes min with no approval dialog seen." -ForegroundColor Yellow
    exit 4
}

Write-Host "Handled $handled dialog(s)."
exit 0
