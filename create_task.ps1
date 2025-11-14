# Create-AwarenessTask.ps1
# Creates a scheduled task for C:\awareness\AwarenessSpalsh.exe
# Task execustion: 13.11.2025 at 08:20 o'clock

$taskName = "AwarenessSplash0r"
$exePath  = "C:\awareness\AwarenessSplash.exe"

if (!(Test-Path $exePath)) {
    Write-Error "File '$exePath' not found. Please check path/filename."
    exit 1
}

# Date/Time for trigger (german Format dd.MM.yyyy HH:mm)
$triggerTime = [datetime]::ParseExact(
    "13.11.2025 08:20",
    "dd.MM.yyyy HH:mm",
    $null
)

$action   = New-ScheduledTaskAction -Execute $exePath
$trigger  = New-ScheduledTaskTrigger -Once -At $triggerTime

# Create Task in Context of logged in user
# (Please run script in context of target user with admin rights)
Register-ScheduledTask `
    -TaskName    $taskName `
    -Action      $action `
    -Trigger     $trigger `
    -Description "AwarenessSplash0r at $($triggerTime.ToString('dd.MM.yyyy HH:mm'))" `
    -Force

Write-Host "Scheduled Task '$taskName' created."
Write-Host "Task execution at: $($triggerTime.ToString('dd.MM.yyyy HH:mm')) (dd.MM.yyy HH:mm)"
Write-Host "Program: $exePath"
