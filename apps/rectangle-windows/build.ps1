$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sources = @(
    "Native.cs",
    "Layout.cs",
    "WindowEngine.cs",
    "SnapAreas.cs",
    "SettingsStore.cs",
    "ShortcutRecorder.cs",
    "SettingsForm.cs",
    "TrayApp.cs",
    "Screenshot.cs",
    "SelfTest.cs",
    "Program.cs"
) | ForEach-Object { Join-Path $root "src\$_" }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/win32manifest:$root\app.manifest" `
    "/out:$root\RectangleWindows.exe" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sources

exit $LASTEXITCODE
