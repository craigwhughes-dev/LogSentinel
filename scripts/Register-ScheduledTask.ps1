<#
Registers a nightly Windows Scheduled Task that runs the LogSentinel exe.
NOT run automatically by any tooling — review and execute manually when ready:

    powershell -File scripts\Register-ScheduledTask.ps1 -Time 03:00

Assumes you've already published the app, e.g.:
    dotnet publish src\LogSentinel -c Release -o publish
#>
[CmdletBinding()]
param(
    [string]$TaskName = 'LogSentinel',

    # HH:mm, 24-hour clock
    [string]$Time = '03:00',

    [string]$ExePath = (Join-Path $PSScriptRoot '..\publish\LogSentinel.exe'),

    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\config\log_sentinel.config.json')
)

$ErrorActionPreference = 'Stop'

$ExePath = (Resolve-Path $ExePath).Path
$ConfigPath = (Resolve-Path $ConfigPath).Path

$action = New-ScheduledTaskAction -Execute $ExePath -Argument "--config `"$ConfigPath`""
$trigger = New-ScheduledTaskTrigger -Daily -At $Time
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 1)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType S4U -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Nightly LogSentinel log scan + Claude investigation'

Write-Output "Registered scheduled task '$TaskName' to run daily at $Time -> $ExePath"
