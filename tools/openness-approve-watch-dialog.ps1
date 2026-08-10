<#
.SYNOPSIS
    Fallback: watches for TIA's "Openness access" approval dialog and accepts it with a real mouse
    click, so an unattended run is not blocked waiting for a person.

.DESCRIPTION
    PREFER openness-approve-build.ps1. Writing the whitelist entry means the dialog is never raised at
    all, and that route is proven (2026-08-10): a hand-written entry let a freshly launched Portal
    grant access with nobody present. Use this script for the cases the registry route does not cover
    -- a Portal already running before the entry was written, or a caller you cannot approve ahead of
    time.

    WHY THIS IS WIN32 AND NOT UI AUTOMATION. Measured on the real dialog: to UI Automation it publishes
    four unnamed Pane elements and NOT ONE BUTTON, so there is nothing to Invoke. Win32 shows the
    truth -- three `WindowsForms10.BUTTON.*` children, all with EMPTY captions. The labels exist only
    as pixels. So the buttons cannot be identified by name, by accessibility, or by a screenshot
    (PrintWindow does not render them, and SetForegroundWindow cannot raise the dialog from a
    background process). Position is the only signal available, which is what this script uses.

    HOW A BUTTON IS IDENTIFIED, since it cannot be read:
      * the dialog body lists the options in order -- "Yes", "Yes to all", "No"
      * the three buttons sorted left-to-right correspond to that order
      * "Yes to all" is the widest, because its caption is the longest (86px against 73px measured)
    The script REFUSES to click unless the layout matches that description exactly: right title, three
    visible buttons, and -- for the default target -- the middle one being the widest. A dialog that
    does not match is reported, never guessed at.

    THE SAFETY CHECK THAT MATTERS. A physical click lands on whatever is topmost at those coordinates.
    If the dialog is behind another window, clicking blind would click that other window instead. So
    before clicking, `WindowFromPoint` must confirm the target pixel actually belongs to the intended
    button; if it does not, the script tries to raise the dialog and, failing that, REFUSES. It never
    clicks a coordinate it has not confirmed.

    Still opt-in: without -Click it reports what it would do and clicks nothing.

.PARAMETER Click
    Actually click. Without it, nothing is clicked.

.PARAMETER Button
    Which option to press: YesToAll (default -- grants and saves the authorization, so the build is
    approved for next time too), Yes (grants once), or No (denies).

.PARAMETER TitlePattern
    Window title regex identifying the dialog.

.PARAMETER TimeoutMinutes
    Stop watching after this long. 0 = until Ctrl+C.

.PARAMETER PollSeconds
    Seconds between scans.

.PARAMETER Once
    Exit after the first dialog handled.

.PARAMETER Force
    Skip the "middle button must be widest" sanity check. Only for a dialog whose layout has changed
    and which you have inspected yourself.

.EXAMPLE
    .\tools\openness-approve-watch-dialog.ps1
    Report any approval dialog and what it would click. Clicks nothing.

.EXAMPLE
    .\tools\openness-approve-watch-dialog.ps1 -Click -Once -TimeoutMinutes 20
    Arm it, then start the openness-cli command that needs approval.

.OUTPUTS
    Exit 0 = handled (or, without -Click, observed) a dialog.
    Exit 4 = timed out with no dialog seen.
    Exit 5 = a dialog was found but refused: layout unrecognised, or the target pixel could not be
             confirmed. Deliberately distinct from "nothing found".
#>
[CmdletBinding()]
param(
    [switch]$Click,
    [ValidateSet("YesToAll", "Yes", "No")]
    [string]$Button = "YesToAll",
    [string]$TitlePattern = "Openness access",
    [int]$TimeoutMinutes = 10,
    [int]$PollSeconds = 2,
    [switch]$Once,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct WRECT { public int Left, Top, Right, Bottom; }
public struct WPOINT { public int X, Y; }
public class Win {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out WRECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(WPOINT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out WPOINT p);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint id);
}
"@

$MOUSEEVENTF_LEFTDOWN = 0x0002
$MOUSEEVENTF_LEFTUP = 0x0004

function Get-WinText([IntPtr]$h) {
    $sb = New-Object System.Text.StringBuilder 512
    [Win]::GetWindowText($h, $sb, 512) | Out-Null
    $sb.ToString()
}

function Get-WinClass([IntPtr]$h) {
    $sb = New-Object System.Text.StringBuilder 256
    [Win]::GetClassName($h, $sb, 256) | Out-Null
    $sb.ToString()
}

function Get-Rect([IntPtr]$h) {
    $r = New-Object WRECT
    [Win]::GetWindowRect($h, [ref]$r) | Out-Null
    [PSCustomObject]@{
        Left = $r.Left; Top = $r.Top
        Width = ($r.Right - $r.Left); Height = ($r.Bottom - $r.Top)
        CenterX = [int](($r.Left + $r.Right) / 2)
        CenterY = [int](($r.Top + $r.Bottom) / 2)
    }
}

function Find-Dialogs {
    $script:found = @()
    $cb = [Win+EnumProc]{
        param($h, $l)
        if ([Win]::IsWindowVisible($h) -and (Get-WinText $h) -match $TitlePattern) { $script:found += $h }
        return $true
    }
    [Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:found
}

function Get-DialogButtons([IntPtr]$dialog) {
    $script:btns = @()
    $cb = [Win+EnumProc]{
        param($h, $l)
        if ((Get-WinClass $h) -match 'BUTTON' -and [Win]::IsWindowVisible($h)) {
            $r = Get-Rect $h
            $script:btns += [PSCustomObject]@{
                Hwnd = $h; Left = $r.Left; Top = $r.Top; Width = $r.Width; Height = $r.Height
                CenterX = $r.CenterX; CenterY = $r.CenterY; Enabled = [Win]::IsWindowEnabled($h)
            }
        }
        return $true
    }
    [Win]::EnumChildWindows($dialog, $cb, [IntPtr]::Zero) | Out-Null
    return @($script:btns | Sort-Object Left)
}

# Confirms the pixel belongs to the button before any click. This is the check that stops a click
# landing in another application when the dialog is not on top.
function Confirm-Target([IntPtr]$dialog, $target) {
    $p = New-Object WPOINT
    $p.X = $target.CenterX
    $p.Y = $target.CenterY
    $at = [Win]::WindowFromPoint($p)
    if ($at -eq $target.Hwnd) { return $true }

    # Not on top: try to raise it, then look again. Raising can fail outright from a background
    # process, which is precisely why this returns false instead of clicking anyway.
    [Win]::BringWindowToTop($dialog) | Out-Null
    [Win]::SetForegroundWindow($dialog) | Out-Null
    Start-Sleep -Milliseconds 600

    $at = [Win]::WindowFromPoint($p)
    return ($at -eq $target.Hwnd)
}

function Invoke-PhysicalClick($target) {
    $orig = New-Object WPOINT
    [Win]::GetCursorPos([ref]$orig) | Out-Null
    try {
        [Win]::SetCursorPos($target.CenterX, $target.CenterY) | Out-Null
        Start-Sleep -Milliseconds 120
        [Win]::mouse_event($MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [Win]::mouse_event($MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    }
    finally {
        # Put the pointer back: the human may be using this machine.
        [Win]::SetCursorPos($orig.X, $orig.Y) | Out-Null
    }
}

$deadline = if ($TimeoutMinutes -gt 0) { (Get-Date).AddMinutes($TimeoutMinutes) } else { [datetime]::MaxValue }
$handled = 0
$refused = 0

Write-Host ""
Write-Host "Watching for the TIA Openness approval dialog (Win32; UI Automation cannot see its buttons)." -ForegroundColor Cyan
Write-Host "  mode    $(if ($Click) { 'CLICK -- will press the button for real' } else { 'OBSERVE ONLY -- pass -Click to act' })"
Write-Host "  target  $Button"
Write-Host "  title   $TitlePattern"
Write-Host ""

while ((Get-Date) -lt $deadline) {
    foreach ($dialog in (Find-Dialogs)) {
        $stamp = (Get-Date).ToString("HH:mm:ss")
        $title = Get-WinText $dialog
        $procId = 0
        [Win]::GetWindowThreadProcessId($dialog, [ref]$procId) | Out-Null
        $buttons = Get-DialogButtons $dialog

        Write-Host "[$stamp] '$title' (pid $procId), $($buttons.Count) visible button(s)"
        foreach ($b in $buttons) {
            Write-Host "         hwnd=$($b.Hwnd) x=$($b.Left) y=$($b.Top) size=$($b.Width)x$($b.Height) enabled=$($b.Enabled)"
        }

        if ($buttons.Count -ne 3) {
            Write-Host "         REFUSED: expected exactly 3 buttons (Yes / Yes to all / No). Layout unrecognised." -ForegroundColor Yellow
            $refused++
            continue
        }

        $index = switch ($Button) { "Yes" { 0 } "YesToAll" { 1 } "No" { 2 } }
        $target = $buttons[$index]

        # The captions are pixels only, so the widest-is-'Yes to all' relationship is the sole
        # cross-check available that the left-to-right mapping is the assumed one.
        $widest = ($buttons | Sort-Object Width -Descending)[0]
        if ($Button -eq "YesToAll" -and $widest.Hwnd -ne $target.Hwnd -and -not $Force) {
            Write-Host "         REFUSED: the middle button is not the widest, so the button order is not what this" -ForegroundColor Yellow
            Write-Host "         script assumes. Inspect it and re-run with -Force if you are sure." -ForegroundColor Yellow
            $refused++
            continue
        }

        Write-Host "         target: $Button at ($($target.CenterX),$($target.CenterY)) hwnd=$($target.Hwnd)"

        if (-not $Click) {
            Write-Host "         WOULD CLICK it. Re-run with -Click to do it for real." -ForegroundColor Yellow
            $handled++
            if ($Once) { exit 0 }
            continue
        }

        if (-not (Confirm-Target $dialog $target)) {
            Write-Host "         REFUSED: that pixel does not belong to the target button -- the dialog is covered by" -ForegroundColor Yellow
            Write-Host "         another window and could not be raised. NOT clicking; a blind click would hit that" -ForegroundColor Yellow
            Write-Host "         other window instead." -ForegroundColor Yellow
            $refused++
            continue
        }

        Invoke-PhysicalClick $target
        Write-Host "         CLICKED $Button." -ForegroundColor Green
        $handled++
        if ($Once) { exit 0 }
    }

    Start-Sleep -Seconds $PollSeconds
}

Write-Host ""
if ($handled -eq 0 -and $refused -gt 0) {
    Write-Host "Found $refused dialog(s) but refused every one -- see the reasons above." -ForegroundColor Yellow
    exit 5
}
if ($handled -eq 0) {
    Write-Host "Timed out after $TimeoutMinutes min with no approval dialog seen." -ForegroundColor Yellow
    exit 4
}

Write-Host "Handled $handled dialog(s)."
exit 0
