#Requires -RunAsAdministrator

# PowerShell script to register Wintermelon with Windows Task Scheduler
# by running the command below in PowerShell terminal launched in admin mode 
# powershell -File "Path to this script"

# Step 1: Enable audit of lock/unlock eventIDs 4800, 4801 using secpol.msc
# Windows search -> Local Security Policy (secpol.msc) 
#                  -> Advanced Audit Policy Configuration 
#                    -> System Audit Policies - Local Group Policy Object 
#                      -> Logon/Logoff 
#                        -> Audit Other Logon/Logoff Events = Success
# Step 2: Set up scheduled tasks using template XML files for lock/unlock event


# Step 1
auditpol /set /subcategory:"0CCE921C-69AE-11D9-BED3-505054503030" /success:enable /failure:enable

# Step 2
$scriptdir = Split-Path -Parent $PSCommandPath
$xml1 = $scriptdir + "\Screen_lock_Save_window_position_Clone_display.xml"
$xml2 = $scriptdir + "\Screen_unlock_Extend_display_Restore_window.xml"
$user = "$env:USERDOMAIN\$env:USERNAME"
$timestamp = Get-Date -format s

# Customize XML1 and then register with Task Scheduler
[xml]$schtask1 = Get-Content $xml1
$schtask1.Task.RegistrationInfo.Date = $timestamp.ToString()
$schtask1.Task.RegistrationInfo.Author = $user 
$schtask1.Task.Principals.Principal.UserId = $user
$schtask1.Task.Actions.Exec.Command = $scriptdir + "\bin\Release\netcoreapp3.1\publish\Wintermelon.exe"
$schtask1.Save($xml1)
Write-Host "Setting up task 1 - Wintermelon screen lock save window position clone display..."
Register-ScheduledTask -Xml (Get-Content $xml1 | Out-String) -TaskName "Wintermelon screen lock save window position clone display" -User $user 

# Customize XML2 and then register with Task Scheduler
[xml]$schtask2 = Get-Content $xml2
$schtask2.Task.RegistrationInfo.Date = $timestamp.ToString()
$schtask2.Task.RegistrationInfo.Author = $user 
$schtask2.Task.Principals.Principal.UserId = $user
$schtask2.Task.Actions.Exec.Command = $scriptdir + "\bin\Release\netcoreapp3.1\publish\Wintermelon.exe" 
$schtask2.Save($xml2)
Write-Host "Setting up task 2 - Wintermelon screen unlock extend display restore window..."
Register-ScheduledTask -Xml (Get-Content $xml2 | Out-String) -TaskName "Wintermelon screen unlock extend display restore window" -User $user 

