$out = "c:\Users\sfz_2nbee89\OneDrive\Desktop\Cursor\apps\hp-fix-touchpad\elan-service.txt"
Stop-Service ETDService -Force -ErrorAction SilentlyContinue
Set-Service ETDService -StartupType Disabled
Get-Service ETDService | Select-Object Name, Status, StartType | Out-String | Set-Content $out
