$ErrorActionPreference = 'Continue'
$log = Join-Path $PSScriptRoot 'repair-log.txt'
$id = 'ACPI\ELAN07CE\4&2F65F92&0'
$i2c = 'PCI\VEN_8086&DEV_7E78&SUBSYS_8C17103C&REV_20\3&2411E6FE&0&A8'
$wdf = 'HKLM:\SYSTEM\CurrentControlSet\Enum\PCI\VEN_8086&DEV_7E78&SUBSYS_8C17103C&REV_20\3&2411e6fe&0&A8\Device Parameters\WDF'

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

Write-Log 'set I2C 7E78 IdleInWorkingState=0 and directed power transition off'
Set-ItemProperty -Path $wdf -Name 'IdleInWorkingState' -Type DWord -Value 0
Set-ItemProperty -Path $wdf -Name 'WdfDirectedPowerTransitionEnable' -Type DWord -Value 0
$now = Get-ItemProperty $wdf
Write-Log ('registry Idle={0} Directed={1}' -f $now.IdleInWorkingState, $now.WdfDirectedPowerTransitionEnable)

Write-Log 'cycle I2C controller so the new power policy loads'
Disable-PnpDevice -InstanceId $i2c -Confirm:$false
Start-Sleep -Seconds 4
Enable-PnpDevice -InstanceId $i2c -Confirm:$false
Start-Sleep -Seconds 6
pnputil.exe /scan-devices | Out-Null
Start-Sleep -Seconds 4
Restart-Service ETDService -Force -ErrorAction Continue
Start-Sleep -Seconds 3
Write-Log ('after power policy ' + (Get-TouchpadState))

if ((Get-TouchpadState) -notlike 'OK*') {
    Write-Log 'restart touchpad once more'
    pnputil.exe /restart-device $id | Out-Null
    Start-Sleep -Seconds 4
    Write-Log ('after second restart ' + (Get-TouchpadState))
}

$final = Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue
if ($final) {
    Write-Log ('final status={0} problem={1}' -f $final.Status, $final.Problem)
}
Get-PnpDevice -Class HIDClass -ErrorAction SilentlyContinue |
    Where-Object { $_.InstanceId -match 'ELAN07CE' } |
    ForEach-Object { Write-Log ('node {0} {1}' -f $_.Status, $_.InstanceId) }
Write-Log 'power-policy done'
