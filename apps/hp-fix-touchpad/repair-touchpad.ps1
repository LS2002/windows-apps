$ErrorActionPreference = 'Continue'
$log = Join-Path $PSScriptRoot 'repair-log.txt'
$id = 'ACPI\ELAN07CE\4&2F65F92&0'
$i2c = 'PCI\VEN_8086&DEV_7E78&SUBSYS_8C17103C&REV_20\3&2411E6FE&0&A8'

function Write-Log($msg) {
    $line = '{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $msg
    Add-Content -Path $log -Value $line
    Write-Output $line
}

function Get-TouchpadState {
    $d = Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue
    if (-not $d) { return 'MISSING' }
    return '{0} problem={1}' -f $d.Status, $d.Problem
}

Set-Content -Path $log -Value ('start ' + (Get-Date -Format o))
Write-Log ('admin=' + ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
Write-Log ('before ' + (Get-TouchpadState))

Write-Log 'restart ELAN service'
Restart-Service ETDService -Force -ErrorAction Continue
Start-Sleep -Seconds 2

Write-Log 'restart touchpad device'
& pnputil.exe /restart-device $id
Start-Sleep -Seconds 4
Write-Log ('after device restart ' + (Get-TouchpadState))

if ((Get-TouchpadState) -notlike 'OK*') {
    Write-Log 'disable and enable touchpad'
    Disable-PnpDevice -InstanceId $id -Confirm:$false -ErrorAction Continue
    Start-Sleep -Seconds 3
    Enable-PnpDevice -InstanceId $id -Confirm:$false -ErrorAction Continue
    Start-Sleep -Seconds 4
    Write-Log ('after disable/enable ' + (Get-TouchpadState))
}

if ((Get-TouchpadState) -notlike 'OK*') {
    Write-Log 'remove device node and rescan'
    & pnputil.exe /remove-device $id
    Start-Sleep -Seconds 2
    & pnputil.exe /scan-devices
    Start-Sleep -Seconds 5
    Restart-Service ETDService -Force -ErrorAction Continue
    Start-Sleep -Seconds 3
    Write-Log ('after rescan ' + (Get-TouchpadState))
}

if ((Get-TouchpadState) -notlike 'OK*') {
    Write-Log 'restart parent I2C controller 7E78'
    Disable-PnpDevice -InstanceId $i2c -Confirm:$false -ErrorAction Continue
    Start-Sleep -Seconds 3
    Enable-PnpDevice -InstanceId $i2c -Confirm:$false -ErrorAction Continue
    Start-Sleep -Seconds 5
    & pnputil.exe /scan-devices
    Start-Sleep -Seconds 4
    Restart-Service ETDService -Force -ErrorAction Continue
    Start-Sleep -Seconds 3
    Write-Log ('after i2c restart ' + (Get-TouchpadState))
}

$final = Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue
if ($final) {
    Write-Log ('final status={0} problem={1} desc={2}' -f $final.Status, $final.Problem, $final.ProblemDescription)
} else {
    Write-Log 'final device node missing'
}
Get-PnpDevice -Class Mouse, HIDClass -ErrorAction SilentlyContinue |
    Where-Object { $_.InstanceId -match 'ELAN07CE' } |
    ForEach-Object { Write-Log ('node {0} {1} {2}' -f $_.Status, $_.FriendlyName, $_.InstanceId) }
Write-Log 'done'
