using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace RectangleWindows
{
    internal static class SelfTest
    {
        public static int Run(string resultPath)
        {
            StringBuilder log = new StringBuilder();
            int failures = 0;
            failures += LayoutChecks(log);
            failures += SnapChecks(log);
            failures += WindowChecks(log);
            failures += ScreenshotChecks(log);
            failures += SettingsChecks(log);
            log.AppendLine(failures == 0 ? "PASS" : "FAIL " + failures);
            File.WriteAllText(resultPath, log.ToString());
            return failures == 0 ? 0 : 1;
        }

        private static int LayoutChecks(StringBuilder log)
        {
            int failures = 0;
            FrameRect screen = new FrameRect(0, 0, 1920, 1080);
            failures += Expect(log, "left half", Layout.Horizontal(screen, false, 0.5), new FrameRect(0, 0, 960, 1080));
            failures += Expect(log, "right half", Layout.Horizontal(screen, true, 0.5), new FrameRect(960, 0, 960, 1080));
            failures += Expect(log, "top half", Layout.Vertical(screen, true, 0.5), new FrameRect(0, 0, 1920, 540));
            failures += Expect(log, "bottom half", Layout.Vertical(screen, false, 0.5), new FrameRect(0, 540, 1920, 540));
            failures += Expect(log, "top left", Layout.Corner(screen, false, true, 0.5, 0.5), new FrameRect(0, 0, 960, 540));
            failures += Expect(log, "bottom right", Layout.Corner(screen, true, false, 0.5, 0.5), new FrameRect(960, 540, 960, 540));
            failures += Expect(log, "first third", Layout.Third(screen, 0), new FrameRect(0, 0, 640, 1080));
            failures += Expect(log, "center third", Layout.Third(screen, 1), new FrameRect(640, 0, 640, 1080));
            failures += Expect(log, "last third", Layout.Third(screen, 2), new FrameRect(1280, 0, 640, 1080));
            failures += Expect(log, "two thirds", Layout.Horizontal(screen, false, 2.0 / 3.0), new FrameRect(0, 0, 1280, 1080));
            failures += Expect(log, "almost", Layout.AlmostMaximize(screen), new FrameRect(96, 54, 1728, 972));
            FrameRect window = new FrameRect(10, 10, 400, 300);
            failures += Expect(log, "center", Layout.Centered(screen, window), new FrameRect(760, 390, 400, 300));
            failures += Expect(log, "center half", Layout.Slot(RotateAction.CenterHalf, 0, screen), new FrameRect(480, 0, 960, 1080));
            failures += Expect(log, "center half rotated", Layout.Slot(RotateAction.CenterHalf, 1, screen), new FrameRect(0, 270, 1920, 540));
            failures += Expect(log, "third left", Layout.Slot(RotateAction.Third, 0, screen), new FrameRect(0, 0, 640, 1080));
            failures += Expect(log, "third right", Layout.Slot(RotateAction.Third, 1, screen), new FrameRect(1280, 0, 640, 1080));
            failures += Expect(log, "two thirds left", Layout.Slot(RotateAction.TwoThirds, 0, screen), new FrameRect(0, 0, 1280, 1080));
            failures += Expect(log, "two thirds right", Layout.Slot(RotateAction.TwoThirds, 1, screen), new FrameRect(640, 0, 1280, 1080));
            failures += Expect(log, "two thirds third press", Layout.Slot(RotateAction.TwoThirds, 2, screen), new FrameRect(0, 0, 1280, 1080));
            FrameRect odd = new FrameRect(0, 0, 1000, 500);
            FrameRect first = Layout.Third(odd, 0);
            FrameRect last = Layout.Third(odd, 2);
            if (first.Right > last.X)
            {
                log.AppendLine("FAIL thirds overlap on odd width");
                failures++;
            }
            else
            {
                log.AppendLine("ok odd thirds");
            }
            return failures;
        }

        private static int SnapChecks(StringBuilder log)
        {
            int failures = 0;
            FrameRect monitor = new FrameRect(0, 0, 1920, 1080);
            failures += ExpectZone(log, "edge left", SnapAreas.HitTest(0, 500, monitor), SnapZone.LeftHalf);
            failures += ExpectZone(log, "edge right", SnapAreas.HitTest(1919, 500, monitor), SnapZone.RightHalf);
            failures += ExpectZone(log, "edge top", SnapAreas.HitTest(960, 0, monitor), SnapZone.Maximize);
            failures += ExpectZone(log, "corner tl", SnapAreas.HitTest(0, 0, monitor), SnapZone.TopLeft);
            failures += ExpectZone(log, "corner br", SnapAreas.HitTest(1919, 1079, monitor), SnapZone.BottomRight);
            failures += ExpectZone(log, "bottom first", SnapAreas.HitTest(100, 1079, monitor), SnapZone.BottomFirstThird);
            failures += ExpectZone(log, "bottom center", SnapAreas.HitTest(960, 1079, monitor), SnapZone.BottomCenterThird);
            failures += ExpectZone(log, "bottom last", SnapAreas.HitTest(1800, 1079, monitor), SnapZone.BottomLastThird);
            failures += ExpectZone(log, "upper side band", SnapAreas.HitTest(0, 80, monitor), SnapZone.TopHalf);
            failures += ExpectZone(log, "lower side band", SnapAreas.HitTest(0, 1000, monitor), SnapZone.BottomHalf);
            failures += ExpectZone(log, "middle", SnapAreas.HitTest(400, 400, monitor), SnapZone.None);
            return failures;
        }

        private static int WindowChecks(StringBuilder log)
        {
            Form form = new Form();
            form.StartPosition = FormStartPosition.Manual;
            form.FormBorderStyle = FormBorderStyle.Sizable;
            form.Text = "RectangleWindows layout test";
            form.Bounds = new System.Drawing.Rectangle(120, 120, 480, 320);
            form.Show();
            Pump();
            IntPtr hwnd = form.Handle;
            FrameRect work;
            IntPtr monitor;
            if (!WindowEngine.TryWorkArea(hwnd, out work, out monitor))
            {
                log.AppendLine("FAIL work area");
                form.Close();
                return 1;
            }
            int failures = 0;
            failures += MoveRotated(log, hwnd, work, RotateAction.TwoThirds, 0);
            failures += MoveRotated(log, hwnd, work, RotateAction.TwoThirds, 1);
            failures += MoveRotated(log, hwnd, work, RotateAction.TwoThirds, 0);
            failures += MoveRotated(log, hwnd, work, RotateAction.Half, 0);
            failures += MoveRotated(log, hwnd, work, RotateAction.CenterHalf, 0);
            failures += MoveRotated(log, hwnd, work, RotateAction.CenterHalf, 1);
            failures += MoveAndCheck(log, hwnd, work, ActionKind.LeftHalf, Layout.Horizontal(work, false, 0.5));
            failures += MoveAndCheck(log, hwnd, work, ActionKind.RightHalf, Layout.Horizontal(work, true, 0.5));
            failures += MoveAndCheck(log, hwnd, work, ActionKind.TopLeft, Layout.Corner(work, false, true, 0.5, 0.5));
            failures += MoveAndCheck(log, hwnd, work, ActionKind.Maximize, work);
            FrameRect beforeRestore;
            WindowEngine.TryFrame(hwnd, out beforeRestore);
            if (!WindowEngine.Apply(hwnd, ActionKind.Restore))
            {
                log.AppendLine("FAIL restore command");
                failures++;
            }
            else
            {
                Pump();
                FrameRect restored;
                WindowEngine.TryFrame(hwnd, out restored);
                if (restored.Near(beforeRestore, 8))
                {
                    log.AppendLine("FAIL restore stayed maximized");
                    failures++;
                }
                else
                {
                    log.AppendLine("ok restore moved off maximize");
                }
            }
            form.Close();
            return failures;
        }

        private static int ScreenshotChecks(StringBuilder log)
        {
            int failures = 0;
            string expectedName = Screenshots.FileName(new DateTime(2026, 1, 1, 13, 12, 11));
            if (expectedName != "screenshot-20260101-131211.png")
            {
                log.AppendLine("FAIL screenshot name " + expectedName);
                failures++;
            }
            else
            {
                log.AppendLine("ok screenshot name");
            }

            string directory = Path.Combine(Path.GetTempPath(), "rectangle-windows-shot");
            Directory.CreateDirectory(directory);
            using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(40, 20))
            {
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.FromArgb(220, 20, 40));
                }
                string saved = Screenshots.SavePng(bitmap, directory, new DateTime(2026, 1, 1, 13, 12, 11));
                if (!File.Exists(saved) || Path.GetFileName(saved) != "screenshot-20260101-131211.png")
                {
                    log.AppendLine("FAIL save " + saved);
                    failures++;
                }
                else
                {
                    log.AppendLine("ok save " + Path.GetFileName(saved));
                }
                File.Delete(saved);
            }
            return failures;
        }

        private static int SettingsChecks(StringBuilder log)
        {
            int failures = 0;
            AppSettings defaults = AppSettings.CreateDefaults();
            if (defaults.GetShortcut(SettingAction.Maximize).Display != "Ctrl+Alt+Up")
            {
                log.AppendLine("FAIL default Maximize " + defaults.GetShortcut(SettingAction.Maximize).Display);
                failures++;
            }
            else
            {
                log.AppendLine("ok default Maximize");
            }
            if (defaults.GetShortcut(SettingAction.CenterHalf).Display != "Ctrl+Alt+0")
            {
                log.AppendLine("FAIL default CenterHalf " + defaults.GetShortcut(SettingAction.CenterHalf).Display);
                failures++;
            }
            else
            {
                log.AppendLine("ok default CenterHalf");
            }
            if (defaults.GetShortcut(SettingAction.ShotClipboard).Display != "Ctrl+Alt+Shift+4")
            {
                log.AppendLine("FAIL default ShotClipboard " + defaults.GetShortcut(SettingAction.ShotClipboard).Display);
                failures++;
            }
            else
            {
                log.AppendLine("ok default ShotClipboard");
            }

            AppSettings roundTrip = AppSettings.CreateDefaults();
            roundTrip.SnapWhileDragging = false;
            roundTrip.SetShortcut(SettingAction.Half, new HotkeySpec(Native.MOD_CONTROL | Native.MOD_SHIFT, 0x48));
            AppSettings loaded = AppSettings.FromJson(roundTrip.ToJson());
            if (loaded.SnapWhileDragging || loaded.GetShortcut(SettingAction.Half).Display != "Ctrl+Shift+H")
            {
                log.AppendLine("FAIL settings round-trip " + loaded.GetShortcut(SettingAction.Half).Display);
                failures++;
            }
            else
            {
                log.AppendLine("ok settings round-trip");
            }
            if (HotkeyText.Format(Native.MOD_CONTROL | Native.MOD_ALT, 0x31) != "Ctrl+Alt+1")
            {
                log.AppendLine("FAIL hotkey text");
                failures++;
            }
            else
            {
                log.AppendLine("ok hotkey text");
            }

            MessageWindow window = new MessageWindow();
            for (int i = 0; i < AppSettings.Catalog.Length; i++)
            {
                ActionDefinition definition = AppSettings.Catalog[i];
                HotkeySpec spec = defaults.GetShortcut(definition.Action);
                if (!Native.RegisterHotKey(window.Handle, i + 1, spec.RegisterModifiers, spec.Key))
                {
                    log.AppendLine("FAIL hotkey " + definition.Title + " " + spec.Display);
                    failures++;
                }
                else
                {
                    log.AppendLine("ok hotkey " + definition.Title + " " + spec.Display);
                    Native.UnregisterHotKey(window.Handle, i + 1);
                }
            }
            return failures;
        }

        private static int MoveRotated(StringBuilder log, IntPtr hwnd, FrameRect work, RotateAction action, int index)
        {
            FrameRect expected = Layout.Slot(action, index, work);
            if (!WindowEngine.ApplyRotated(hwnd, action))
            {
                log.AppendLine("FAIL rotate " + action + " " + index);
                return 1;
            }
            Pump();
            FrameRect actual;
            if (!WindowEngine.TryFrame(hwnd, out actual))
            {
                log.AppendLine("FAIL rotate frame " + action);
                return 1;
            }
            if (!actual.Near(expected, 8))
            {
                log.AppendLine("FAIL rotate " + action + " slot " + index + " actual " + Format(actual) + " expected " + Format(expected));
                return 1;
            }
            log.AppendLine("ok rotate " + action + " slot " + index + " " + Format(actual));
            return 0;
        }

        private static int MoveAndCheck(StringBuilder log, IntPtr hwnd, FrameRect work, ActionKind action, FrameRect expected)
        {
            if (!WindowEngine.Apply(hwnd, action, true))
            {
                log.AppendLine("FAIL apply " + action);
                return 1;
            }
            Pump();
            FrameRect actual;
            if (!WindowEngine.TryFrame(hwnd, out actual))
            {
                log.AppendLine("FAIL frame " + action);
                return 1;
            }
            if (!actual.Near(expected, 8))
            {
                log.AppendLine("FAIL " + action + " actual " + Format(actual) + " expected " + Format(expected));
                return 1;
            }
            log.AppendLine("ok window " + action + " " + Format(actual));
            return 0;
        }

        private static void Pump()
        {
            for (int i = 0; i < 15; i++)
            {
                Application.DoEvents();
            }
        }

        private static int Expect(StringBuilder log, string name, FrameRect actual, FrameRect expected)
        {
            if (!actual.Near(expected, 0))
            {
                log.AppendLine("FAIL " + name + " " + Format(actual) + " != " + Format(expected));
                return 1;
            }
            log.AppendLine("ok " + name);
            return 0;
        }

        private static int ExpectZone(StringBuilder log, string name, SnapZone actual, SnapZone expected)
        {
            if (actual != expected)
            {
                log.AppendLine("FAIL " + name + " " + actual + " != " + expected);
                return 1;
            }
            log.AppendLine("ok " + name);
            return 0;
        }

        private static string Format(FrameRect rect)
        {
            return rect.X + "," + rect.Y + " " + rect.Width + "x" + rect.Height;
        }
    }
}
