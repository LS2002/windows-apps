$p = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power"
Set-ItemProperty -Path $p -Name HiberbootEnabled -Type DWord -Value 0
$v = (Get-ItemProperty $p).HiberbootEnabled
Set-Content -Path "c:\Users\sfz_2nbee89\OneDrive\Desktop\Cursor\apps\hp-fix-touchpad\faststartup.txt" -Value $v
