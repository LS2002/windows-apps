# Rectangle for Windows

A small Windows tray app inspired by [Rectangle](https://github.com/rxhanson/Rectangle) for macOS. Snap windows with keyboard shortcuts, drag windows to screen edges, and capture a selected area to the clipboard or desktop.

## Features

- Keyboard window layouts:
  - **Ctrl+Alt+Up** — maximize
  - **Ctrl+Alt+0** — centered half
  - **Ctrl+Alt+1** — one third
  - **Ctrl+Alt+2** — half
  - **Ctrl+Alt+3** — two thirds
- Drag a window to an edge or corner to snap it
- Area screenshots:
  - **Ctrl+Alt+Shift+4** — copy selection to the clipboard
  - **Ctrl+Alt+Shift+3** — save `screenshot-yyyyMMdd-HHmmss.png` to the desktop
- Settings window for recording your own shortcuts (Rectangle-style tabs)

Shortcuts are editable. Double-click the tray icon or choose **Settings...**.

## Build

Requires the .NET Framework 4.x C# compiler that ships with Windows.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

This produces `RectangleWindows.exe` in the project folder.

## Run

Start `RectangleWindows.exe`. It lives in the system tray.

If you need to move elevated windows (for example IDA running as administrator), right-click the tray icon and choose **Restart as Administrator**, or enable **Run this program as an administrator** in the exe Compatibility properties.

## Config

Settings are stored at:

`%AppData%\RectangleWindows\config.json`

## License

MIT. Layout behavior is based on ideas from Rectangle (MIT), Copyright (c) 2019-2026 Ryan Hanson / Spectacle.
