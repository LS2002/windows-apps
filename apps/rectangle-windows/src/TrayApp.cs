using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RectangleWindows
{
    internal sealed class RegisteredHotkey
    {
        public int Id;
        public HotkeySpec Spec;
        public SettingAction Action;
        public string Title;
        public bool Failed;
    }

    internal sealed class MessageWindow : NativeWindow
    {
        public event EventHandler<int> HotkeyPressed;
        public event EventHandler<int> PointerMessage;

        public MessageWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && HotkeyPressed != null)
            {
                HotkeyPressed(this, m.WParam.ToInt32());
            }
            else if (m.Msg == Native.WM_APP_POINTER && PointerMessage != null)
            {
                PointerMessage(this, m.WParam.ToInt32());
            }
            else
            {
                base.WndProc(ref m);
            }
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private const int VkLButton = 0x01;
        private readonly MessageWindow window;
        private readonly NotifyIcon tray;
        private readonly List<RegisteredHotkey> hotkeys;
        private readonly FootprintForm footprint;
        private readonly Icon trayIcon;
        private AppSettings settings;
        private SettingsForm settingsForm;
        private ScreenshotOverlay shotOverlay;
        private IntPtr hook;
        private HookProc hookProc;
        private IntPtr dragWindow;
        private FrameRect dragStart;
        private int dragStartX;
        private int dragStartY;
        private bool dragging;
        private bool hotkeysSuspended;
        private int nextHotkeyId;

        public TrayApp()
        {
            settings = AppSettings.Load();
            settings.RunAtStartup = StartupEnabled();
            hotkeys = new List<RegisteredHotkey>();
            window = new MessageWindow();
            window.HotkeyPressed += OnHotkey;
            window.PointerMessage += OnPointer;
            RegisterHotkeys();
            footprint = new FootprintForm();
            trayIcon = MakeIcon();
            tray = new NotifyIcon();
            tray.Icon = trayIcon;
            tray.Visible = true;
            tray.Text = "Rectangle for Windows";
            tray.ContextMenuStrip = BuildMenu();
            tray.DoubleClick += delegate { ShowSettings(); };
            InstallHook();
        }

        public void ApplySettings(AppSettings next)
        {
            settings = next;
            RegisterHotkeys();
            RebuildMenu();
        }

        public void SuspendHotkeys()
        {
            hotkeysSuspended = true;
            UnregisterHotkeys();
        }

        public void ResumeHotkeys()
        {
            hotkeysSuspended = false;
            RegisterHotkeys();
        }

        public List<string> ConflictLabels(AppSettings probe)
        {
            List<string> conflicts = new List<string>();
            UnregisterHotkeys();
            try
            {
                int id = 5000;
                Dictionary<string, string> seen = new Dictionary<string, string>();
                for (int i = 0; i < AppSettings.Catalog.Length; i++)
                {
                    ActionDefinition definition = AppSettings.Catalog[i];
                    HotkeySpec spec = probe.GetShortcut(definition.Action);
                    if (spec.IsEmpty)
                    {
                        continue;
                    }
                    string signature = spec.Modifiers + ":" + spec.Key;
                    if (seen.ContainsKey(signature))
                    {
                        conflicts.Add(definition.Title + " (" + spec.Display + ")");
                        continue;
                    }
                    seen[signature] = definition.Title;
                    if (!Native.RegisterHotKey(window.Handle, id, spec.RegisterModifiers, spec.Key))
                    {
                        conflicts.Add(definition.Title + " (" + spec.Display + ")");
                    }
                    else
                    {
                        Native.UnregisterHotKey(window.Handle, id);
                    }
                    id++;
                }
            }
            finally
            {
                if (!hotkeysSuspended)
                {
                    RegisterHotkeys();
                }
            }
            return conflicts;
        }

        public void SetStartupEnabled(bool enabled)
        {
            SetStartup(enabled);
        }

        public void RevealConfig()
        {
            settings.Save();
            string path = AppSettings.ConfigPath;
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        public void RestartElevated()
        {
            ProcessStartInfo info = new ProcessStartInfo(Application.ExecutablePath);
            info.UseShellExecute = true;
            info.Verb = "runas";
            try
            {
                Process.Start(info);
                ExitThread();
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        public void ShowSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.BringToFront();
                settingsForm.Activate();
                return;
            }
            settingsForm = new SettingsForm(this, settings);
            settingsForm.FormClosed += delegate { settingsForm = null; };
            settingsForm.Show();
        }

        private void RegisterHotkeys()
        {
            UnregisterHotkeys();
            if (hotkeysSuspended)
            {
                return;
            }
            nextHotkeyId = 1;
            Dictionary<string, SettingAction> seen = new Dictionary<string, SettingAction>();
            for (int i = 0; i < AppSettings.Catalog.Length; i++)
            {
                ActionDefinition definition = AppSettings.Catalog[i];
                HotkeySpec spec = settings.GetShortcut(definition.Action);
                if (spec.IsEmpty)
                {
                    continue;
                }
                string signature = spec.Modifiers + ":" + spec.Key;
                if (seen.ContainsKey(signature))
                {
                    RegisteredHotkey conflict = new RegisteredHotkey();
                    conflict.Id = -1;
                    conflict.Spec = spec;
                    conflict.Action = definition.Action;
                    conflict.Title = definition.Title;
                    conflict.Failed = true;
                    hotkeys.Add(conflict);
                    continue;
                }
                seen[signature] = definition.Action;
                RegisteredHotkey item = new RegisteredHotkey();
                item.Id = nextHotkeyId++;
                item.Spec = spec.Clone();
                item.Action = definition.Action;
                item.Title = definition.Title;
                if (!Native.RegisterHotKey(window.Handle, item.Id, item.Spec.RegisterModifiers, item.Spec.Key))
                {
                    item.Failed = true;
                }
                hotkeys.Add(item);
            }
        }

        private void UnregisterHotkeys()
        {
            for (int i = 0; i < hotkeys.Count; i++)
            {
                if (hotkeys[i].Id > 0 && !hotkeys[i].Failed)
                {
                    Native.UnregisterHotKey(window.Handle, hotkeys[i].Id);
                }
            }
            hotkeys.Clear();
        }

        private void OnHotkey(object sender, int id)
        {
            for (int i = 0; i < hotkeys.Count; i++)
            {
                if (hotkeys[i].Id == id)
                {
                    InvokeAction(hotkeys[i].Action);
                    return;
                }
            }
        }

        private void InvokeAction(SettingAction action)
        {
            switch (action)
            {
                case SettingAction.Maximize:
                    WindowEngine.Maximize(WindowEngine.ForegroundRoot());
                    break;
                case SettingAction.CenterHalf:
                    WindowEngine.ApplyRotated(WindowEngine.ForegroundRoot(), RotateAction.CenterHalf);
                    break;
                case SettingAction.Third:
                    WindowEngine.ApplyRotated(WindowEngine.ForegroundRoot(), RotateAction.Third);
                    break;
                case SettingAction.Half:
                    WindowEngine.ApplyRotated(WindowEngine.ForegroundRoot(), RotateAction.Half);
                    break;
                case SettingAction.TwoThirds:
                    WindowEngine.ApplyRotated(WindowEngine.ForegroundRoot(), RotateAction.TwoThirds);
                    break;
                case SettingAction.ShotClipboard:
                    BeginShot(ShotDestination.Clipboard);
                    break;
                case SettingAction.ShotDesktop:
                    BeginShot(ShotDestination.Desktop);
                    break;
            }
        }

        private void BeginShot(ShotDestination destination)
        {
            if (shotOverlay != null && !shotOverlay.IsDisposed)
            {
                return;
            }
            HideFootprint();
            shotOverlay = new ScreenshotOverlay(destination, delegate(string message)
            {
                tray.ShowBalloonTip(2500, "Rectangle for Windows", message, ToolTipIcon.Info);
            });
            shotOverlay.FormClosed += delegate { shotOverlay = null; };
            shotOverlay.Show();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            for (int i = 0; i < AppSettings.Catalog.Length; i++)
            {
                ActionDefinition definition = AppSettings.Catalog[i];
                if (definition.IsScreenshot)
                {
                    continue;
                }
                HotkeySpec spec = settings.GetShortcut(definition.Action);
                string text = definition.Title;
                if (!spec.IsEmpty)
                {
                    text += "  (" + spec.Display + ")";
                }
                SettingAction action = definition.Action;
                menu.Items.Add(text, null, delegate { InvokeAction(action); });
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Screenshot to Clipboard", null, delegate { BeginShot(ShotDestination.Clipboard); });
            menu.Items.Add("Screenshot to Desktop", null, delegate { BeginShot(ShotDestination.Desktop); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings...", null, delegate { ShowSettings(); });
            menu.Items.Add("Restart as Administrator", null, delegate { RestartElevated(); });
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            return menu;
        }

        private void RebuildMenu()
        {
            ContextMenuStrip old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = BuildMenu();
            if (old != null)
            {
                old.Dispose();
            }
        }

        private void InstallHook()
        {
            hookProc = HookCallback;
            string module = Process.GetCurrentProcess().MainModule.ModuleName;
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(module), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == Native.HC_ACTION)
            {
                int message = wParam.ToInt32();
                bool buttonDown = (Native.GetAsyncKeyState(VkLButton) & 0x8000) != 0;
                if (message == Native.WM_LBUTTONDOWN || message == Native.WM_LBUTTONUP || (message == Native.WM_MOUSEMOVE && buttonDown))
                {
                    Native.PostMessage(window.Handle, Native.WM_APP_POINTER, wParam, IntPtr.Zero);
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        private void OnPointer(object sender, int message)
        {
            if (!settings.SnapWhileDragging)
            {
                return;
            }
            Native.POINT cursor;
            if (!Native.GetCursorPos(out cursor))
            {
                return;
            }
            if (message == Native.WM_LBUTTONDOWN)
            {
                BeginDrag(cursor.X, cursor.Y);
            }
            else if (message == Native.WM_MOUSEMOVE)
            {
                if ((Native.GetAsyncKeyState(VkLButton) & 0x8000) != 0)
                {
                    UpdateDrag(cursor.X, cursor.Y);
                }
            }
            else if (message == Native.WM_LBUTTONUP)
            {
                EndDrag(cursor.X, cursor.Y);
            }
        }

        private void BeginDrag(int x, int y)
        {
            dragging = false;
            dragWindow = WindowEngine.ForegroundRoot();
            dragStartX = x;
            dragStartY = y;
            if (!WindowEngine.IsMovable(dragWindow) || dragWindow == footprint.Handle)
            {
                dragWindow = IntPtr.Zero;
                return;
            }
            FrameRect frame;
            if (!WindowEngine.TryFrame(dragWindow, out frame))
            {
                dragWindow = IntPtr.Zero;
                return;
            }
            dragStart = frame;
        }

        private void UpdateDrag(int x, int y)
        {
            if (dragWindow == IntPtr.Zero)
            {
                return;
            }
            FrameRect now;
            if (!WindowEngine.TryFrame(dragWindow, out now) || now.Near(dragStart, 2))
            {
                return;
            }
            dragging = true;
            FrameRect monitor = SnapAreas.MonitorAt(x, y);
            SnapZone zone = SnapAreas.HitTest(x, y, monitor);
            if (zone == SnapZone.None || monitor.Width == 0)
            {
                HideFootprint();
                return;
            }
            FrameRect work = SnapAreas.WorkAreaAt(x, y);
            FrameRect preview = Layout.ForAction(SnapAreas.ToAction(zone), work, dragStart, 0.5);
            footprint.Bounds = new Rectangle(preview.X, preview.Y, preview.Width, preview.Height);
            if (!footprint.Visible)
            {
                footprint.Show();
            }
        }

        private void EndDrag(int x, int y)
        {
            HideFootprint();
            IntPtr hwnd = dragWindow;
            bool wasDragging = dragging;
            dragWindow = IntPtr.Zero;
            dragging = false;
            if (!wasDragging || hwnd == IntPtr.Zero)
            {
                return;
            }
            FrameRect monitor = SnapAreas.MonitorAt(x, y);
            SnapZone zone = SnapAreas.HitTest(x, y, monitor);
            if (zone == SnapZone.None)
            {
                return;
            }
            WindowEngine.Apply(hwnd, SnapAreas.ToAction(zone), true);
        }

        private void HideFootprint()
        {
            if (footprint.Visible)
            {
                footprint.Hide();
            }
        }

        private static Icon MakeIcon()
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(32, 36, 44));
                using (Brush fill = new SolidBrush(Color.FromArgb(47, 132, 229)))
                {
                    graphics.FillRectangle(fill, 3, 7, 12, 18);
                }
                using (Pen pen = new Pen(Color.White, 2))
                {
                    graphics.DrawRectangle(pen, 3, 7, 25, 18);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private static bool StartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key == null)
                {
                    return false;
                }
                object value = key.GetValue("RectangleWindows");
                return value != null && string.Equals(value.ToString(), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void SetStartup(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null)
                {
                    return;
                }
                if (enabled)
                {
                    key.SetValue("RectangleWindows", Application.ExecutablePath);
                }
                else
                {
                    key.DeleteValue("RectangleWindows", false);
                }
            }
        }

        protected override void ExitThreadCore()
        {
            if (hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
            UnregisterHotkeys();
            if (shotOverlay != null && !shotOverlay.IsDisposed)
            {
                shotOverlay.Close();
            }
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.Close();
            }
            tray.Visible = false;
            tray.Dispose();
            footprint.Dispose();
            base.ExitThreadCore();
        }
    }
}
