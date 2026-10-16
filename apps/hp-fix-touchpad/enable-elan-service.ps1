Set-Service ETDService -StartupType Automatic
Start-Service ETDService
Get-Service ETDService | Select-Object Name, Status, StartType | Out-String |
  Set-Content "c:\Users\sfz_2nbee89\OneDrive\Desktop\Cursor\apps\hp-fix-touchpad\elan-service.txt"
Start-Sleep -Seconds 3
Get-PnpDevice -InstanceId "ACPI\ELAN07CE\4&2F65F92&0" |
  Select-Object Status, Problem | Out-String |
  Add-Content "c:\Users\sfz_2nbee89\OneDrive\Desktop\Cursor\apps\hp-fix-touchpad\elan-service.txt"
