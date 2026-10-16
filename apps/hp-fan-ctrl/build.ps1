$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sources = @(
    "FanCurve.cs",
    "HpBios.cs",
    "Sensors.cs",
    "SettingsStore.cs",
    "CurvePanel.cs",
    "MainForm.cs",
    "Program.cs"
) | ForEach-Object { Join-Path $root "src\$_" }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/win32manifest:$root\app.manifest" `
    "/out:$root\hp-fan-ctrl.exe" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    /reference:System.Windows.Forms.dll `
    $sources

exit $LASTEXITCODE
