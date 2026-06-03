<#
.SYNOPSIS
    Removes the two Wintermelon scheduled tasks.

.NOTES
    Must be run from an ELEVATED PowerShell (Run as Administrator).
        powershell -ExecutionPolicy Bypass -File .\uninstall_wintermelon.ps1
#>

$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

$tasks = @(
    'Wintermelon screen lock save window position clone display',
    'Wintermelon screen unlock extend display restore window'
)

foreach ($t in $tasks) {
    $existing = Get-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue
    if ($existing) {
        Unregister-ScheduledTask -TaskName $t -Confirm:$false
        Write-Host "Removed: $t" -ForegroundColor Green
    } else {
        Write-Host "Not found (already removed): $t" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Wintermelon tasks removed. Displays will no longer clone on lock." -ForegroundColor Yellow
