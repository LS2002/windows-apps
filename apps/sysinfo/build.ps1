$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sources = @(
    "Models.cs",
    "Collector.cs",
    "Controls.cs",
    "MainForm.cs",
    "Program.cs"
) | ForEach-Object { Join-Path $root "src\$_" }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/win32manifest:$root\app.manifest" `
    "/win32icon:$root\sysinfo.ico" `
    "/out:$root\sysinfo.exe" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    /reference:System.Windows.Forms.dll `
    $sources

exit $LASTEXITCODE
