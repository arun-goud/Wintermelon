<#
.SYNOPSIS
    Builds Wintermelon and registers two scheduled tasks that mirror the
    Windows lock screen onto all monitors.

.DESCRIPTION
    Registers:
      "Wintermelon screen lock save window position clone display"
          -> runs "Wintermelon.exe save"    on workstation LOCK
      "Wintermelon screen unlock extend display restore window"
          -> runs "Wintermelon.exe restore" on workstation UNLOCK

    Uses native Task Scheduler SessionStateChangeTrigger (SessionLock /
    SessionUnlock) triggers, so no Security audit-policy change is needed.

.NOTES
    Must be run from an ELEVATED PowerShell (Run as Administrator).
        powershell -ExecutionPolicy Bypass -File .\install_wintermelon.ps1
#>

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

# --- Require elevation -------------------------------------------------------
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator. Right-click PowerShell -> Run as administrator, then re-run it."
    exit 1
}

$scriptDir = $PSScriptRoot
$exePath   = Join-Path $scriptDir 'bin\Release\net8.0-windows\win-x64\publish\Wintermelon.exe'
$userId    = [Security.Principal.WindowsIdentity]::GetCurrent().Name

$lockTaskName   = 'Wintermelon screen lock save window position clone display'
$unlockTaskName = 'Wintermelon screen unlock extend display restore window'

# --- Build (publish single-file exe) ----------------------------------------
if (-not $SkipBuild) {
    Write-Host "Publishing Wintermelon.exe ..." -ForegroundColor Cyan
    $proj = Join-Path $scriptDir 'Wintermelon.csproj'
    & dotnet publish $proj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }
}

if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at: $exePath`nRun without -SkipBuild, or build the project first."
    exit 1
}
Write-Host "Using executable: $exePath" -ForegroundColor Green

# --- Task XML templates ------------------------------------------------------
function New-WintermelonTaskXml {
    param(
        [string]$Description,
        [string]$StateChange,   # SessionLock or SessionUnlock
        [string]$Argument,      # save or restore
        [string]$User,
        [string]$Command
    )
    @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>$User</Author>
    <Description>$Description</Description>
  </RegistrationInfo>
  <Triggers>
    <SessionStateChangeTrigger>
      <Enabled>true</Enabled>
      <StateChange>$StateChange</StateChange>
      <UserId>$User</UserId>
    </SessionStateChangeTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$User</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT5M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$Command</Command>
      <Arguments>$Argument</Arguments>
    </Exec>
  </Actions>
</Task>
"@
}

$lockXml = New-WintermelonTaskXml -Description 'Wintermelon: on workstation lock, save window positions and clone displays so the lock screen shows on all monitors.' `
    -StateChange 'SessionLock' -Argument 'save' -User $userId -Command $exePath

$unlockXml = New-WintermelonTaskXml -Description 'Wintermelon: on workstation unlock, extend displays and restore window positions.' `
    -StateChange 'SessionUnlock' -Argument 'restore' -User $userId -Command $exePath

# --- Register tasks ----------------------------------------------------------
Write-Host "Registering scheduled tasks ..." -ForegroundColor Cyan
Register-ScheduledTask -TaskName $lockTaskName   -Xml $lockXml   -User $userId -Force | Out-Null
Register-ScheduledTask -TaskName $unlockTaskName -Xml $unlockXml -User $userId -Force | Out-Null

Write-Host ""
Write-Host "Done. Two tasks registered:" -ForegroundColor Green
Write-Host "  * $lockTaskName"
Write-Host "  * $unlockTaskName"
Write-Host ""
Write-Host "Test it: press Win+L. The lock screen should appear on all monitors." -ForegroundColor Yellow
Write-Host "Unlock and your windows return to their original positions (~3s)."
