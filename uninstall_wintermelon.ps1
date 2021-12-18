#Requires -RunAsAdministrator

# PowerShell script to unregister Wintermelon with Windows Task Scheduler
# in admin mode
$task1 = "Wintermelon screen lock save window position clone display"
$task2 = "Wintermelon screen unlock extend display restore window"

# Unregister task1 with Task Scheduler
Write-Host "Removing task 1 - Wintermelon screen lock save window position clone display..."
if ($(Get-ScheduledTask -TaskName $task1 -ErrorAction SilentlyContinue).TaskName -eq $task1) {
    Unregister-ScheduledTask -TaskName $task1 -Confirm:$false
} else {
    Write-Host "No such task exists."
}



# Unregister task2 with Task Scheduler
Write-Host "Removing task 2 - Wintermelon screen unlock extend display restore window..."
if ($(Get-ScheduledTask -TaskName $task2 -ErrorAction SilentlyContinue).TaskName -eq $task2) {
    Unregister-ScheduledTask -TaskName $task2 -Confirm:$false
} else {
    Write-Host "No such task exists."
}

