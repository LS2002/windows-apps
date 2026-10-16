$log = Join-Path $PSScriptRoot 'boot-watch.log'
$id = 'ACPI\ELAN07CE\4&2F65F92&0'
Add-Content $log ("==== boot watch start {0} ====" -f (Get-Date -Format o))
$last = ''
for ($i = 0; $i -lt 90; $i++) {
    $d = Get-PnpDevice -InstanceId $id -ErrorAction SilentlyContinue
    $kids = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -like 'HID\ELAN07CE*' }).Count
    $state = if ($d) { '{0} {1} children={2}' -f $d.Status, $d.Problem, $kids } else { 'missing' }
    if ($state -ne $last) {
        Add-Content $log ('{0} {1}' -f (Get-Date -Format 'HH:mm:ss'), $state)
        $last = $state
    }
    Start-Sleep -Seconds 2
}
Add-Content $log ("==== boot watch end {0} ====" -f (Get-Date -Format o))
