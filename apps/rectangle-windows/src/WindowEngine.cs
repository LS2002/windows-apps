using System;
using System.Collections.Generic;
using System.Text;

namespace RectangleWindows
{
    internal struct Insets
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    internal static class WindowEngine
    {
        private static readonly Dictionary<IntPtr, FrameRect> Originals = new Dictionary<IntPtr, FrameRect>();
        private static readonly Dictionary<IntPtr, RotationState> Rotations = new Dictionary<IntPtr, RotationState>();

        private sealed class RotationState
        {
            public RotateAction Action;
            public int Index;
            public FrameRect Applied;
        }

        public static bool IsMovable(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }
            int style = Native.GetStyle(hwnd);
            if ((style & Native.WS_CHILD) != 0)
            {
                return false;
            }
            if ((style & Native.WS_CAPTION) == 0 && (style & Native.WS_THICKFRAME) == 0)
            {
                return false;
            }
            string className = ClassName(hwnd);
            if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd")
            {
                return false;
            }
            return true;
        }

        public static IntPtr ForegroundRoot()
        {
            IntPtr hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            IntPtr root = Native.GetAncestor(hwnd, Native.GA_ROOT);
            if (root != IntPtr.Zero)
            {
                return root;
            }
            return hwnd;
        }

        public static bool TryWorkArea(IntPtr hwnd, out FrameRect work, out IntPtr monitor)
        {
            work = new FrameRect();
            monitor = Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }
            MONITORINFO info = new MONITORINFO();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
            if (!Native.GetMonitorInfo(monitor, ref info))
            {
                return false;
            }
            work = FrameRect.FromRECT(info.rcWork);
            return work.Width > 0 && work.Height > 0;
        }

        public static bool TryFrame(IntPtr hwnd, out FrameRect frame)
        {
            frame = new FrameRect();
            RECT bounds;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out bounds, System.Runtime.InteropServices.Marshal.SizeOf(typeof(RECT))) == 0)
            {
                frame = FrameRect.FromRECT(bounds);
                return frame.Width > 0 && frame.Height > 0;
            }
            RECT window;
            if (!Native.GetWindowRect(hwnd, out window))
            {
                return false;
            }
            frame = FrameRect.FromRECT(window);
            return frame.Width > 0 && frame.Height > 0;
        }

        public static bool Maximize(IntPtr hwnd)
        {
            if (!IsMovable(hwnd))
            {
                return false;
            }
            int style = Native.GetStyle(hwnd);
            if ((style & Native.WS_MAXIMIZEBOX) != 0)
            {
                return Native.ShowWindow(hwnd, Native.SW_MAXIMIZE);
            }
            return Apply(hwnd, ActionKind.Maximize);
        }

        public static bool ApplyRotated(IntPtr hwnd, RotateAction action)
        {
            if (!IsMovable(hwnd))
            {
                return false;
            }
            FrameRect work;
            IntPtr monitor;
            if (!TryWorkArea(hwnd, out work, out monitor))
            {
                return false;
            }
            FrameRect current;
            if (!TryFrame(hwnd, out current))
            {
                return false;
            }

            int index = 0;
            RotationState previous;
            if (Rotations.TryGetValue(hwnd, out previous)
                && previous.Action == action
                && current.Near(previous.Applied, 8))
            {
                index = (previous.Index + 1) % 2;
            }

            FrameRect target = Layout.Slot(action, index, work);
            if (!Place(hwnd, target))
            {
                return false;
            }

            RotationState next = new RotationState();
            next.Action = action;
            next.Index = index;
            next.Applied = target;
            Rotations[hwnd] = next;
            return true;
        }

        public static bool Apply(IntPtr hwnd, ActionKind action)
        {
            return Apply(hwnd, action, false);
        }

        public static bool Apply(IntPtr hwnd, ActionKind action, bool snapToFirst)
        {
            if (!IsMovable(hwnd))
            {
                return false;
            }
            if (action == ActionKind.Restore)
            {
                return Restore(hwnd);
            }
            if (action == ActionKind.NextDisplay || action == ActionKind.PreviousDisplay)
            {
                return MoveDisplay(hwnd, action == ActionKind.NextDisplay);
            }

            FrameRect work;
            IntPtr monitor;
            if (!TryWorkArea(hwnd, out work, out monitor))
            {
                return false;
            }
            FrameRect current;
            if (!TryFrame(hwnd, out current))
            {
                return false;
            }

            double fraction = 0.5;
            if (!snapToFirst && Layout.UsesRepeatedThirds(action))
            {
                int index = Layout.MatchingCycleIndex(action, work, current);
                if (index >= 0)
                {
                    fraction = Layout.RepeatedThirds[(index + 1) % Layout.RepeatedThirds.Length];
                }
            }
            else if (action == ActionKind.Larger || action == ActionKind.Smaller)
            {
                int step = Math.Max(work.Width, work.Height) / 16;
                if (step < 20)
                {
                    step = 20;
                }
                if (action == ActionKind.Smaller)
                {
                    step = -step;
                }
                Remember(hwnd, current);
                return Place(hwnd, Layout.ResizeAboutCenter(work, current, step));
            }

            FrameRect target = Layout.ForAction(action, work, current, fraction);
            Remember(hwnd, current);
            return Place(hwnd, target);
        }

        public static List<FrameRect> WorkAreas()
        {
            List<FrameRect> areas = new List<FrameRect>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
            {
                MONITORINFO info = new MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
                if (Native.GetMonitorInfo(hMonitor, ref info))
                {
                    areas.Add(FrameRect.FromRECT(info.rcWork));
                }
                return true;
            }, IntPtr.Zero);
            areas.Sort(delegate(FrameRect a, FrameRect b)
            {
                int cmp = a.X.CompareTo(b.X);
                if (cmp != 0)
                {
                    return cmp;
                }
                return a.Y.CompareTo(b.Y);
            });
            return areas;
        }

        private static bool MoveDisplay(IntPtr hwnd, bool next)
        {
            FrameRect current;
            if (!TryFrame(hwnd, out current))
            {
                return false;
            }
            List<FrameRect> areas = WorkAreas();
            if (areas.Count == 0)
            {
                return false;
            }
            int index = 0;
            int bestDistance = int.MaxValue;
            int centerX = current.X + (current.Width / 2);
            int centerY = current.Y + (current.Height / 2);
            for (int i = 0; i < areas.Count; i++)
            {
                int dx = (areas[i].X + (areas[i].Width / 2)) - centerX;
                int dy = (areas[i].Y + (areas[i].Height / 2)) - centerY;
                int distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    index = i;
                }
            }
            FrameRect source = areas[index];
            int destinationIndex = next ? (index + 1) % areas.Count : (index - 1 + areas.Count) % areas.Count;
            FrameRect destination = areas[destinationIndex];
            double rx = source.Width == 0 ? 0 : (current.X - source.X) / (double)source.Width;
            double ry = source.Height == 0 ? 0 : (current.Y - source.Y) / (double)source.Height;
            double rw = source.Width == 0 ? 1 : current.Width / (double)source.Width;
            double rh = source.Height == 0 ? 1 : current.Height / (double)source.Height;
            int width = (int)Math.Round(rw * destination.Width);
            int height = (int)Math.Round(rh * destination.Height);
            if (width < 1)
            {
                width = 1;
            }
            if (height < 1)
            {
                height = 1;
            }
            if (width > destination.Width)
            {
                width = destination.Width;
            }
            if (height > destination.Height)
            {
                height = destination.Height;
            }
            int x = destination.X + (int)Math.Round(rx * destination.Width);
            int y = destination.Y + (int)Math.Round(ry * destination.Height);
            if (x < destination.X)
            {
                x = destination.X;
            }
            if (y < destination.Y)
            {
                y = destination.Y;
            }
            if (x + width > destination.Right)
            {
                x = destination.Right - width;
            }
            if (y + height > destination.Bottom)
            {
                y = destination.Bottom - height;
            }
            Remember(hwnd, current);
            return Place(hwnd, new FrameRect(x, y, width, height));
        }

        private static bool Restore(IntPtr hwnd)
        {
            FrameRect original;
            if (!Originals.TryGetValue(hwnd, out original))
            {
                return false;
            }
            Originals.Remove(hwnd);
            return Place(hwnd, original);
        }

        private static void Remember(IntPtr hwnd, FrameRect current)
        {
            if (!Originals.ContainsKey(hwnd))
            {
                Originals[hwnd] = current;
            }
        }

        public static bool Place(IntPtr hwnd, FrameRect target)
        {
            if (Native.IsZoomed(hwnd) || Native.IsIconic(hwnd))
            {
                Native.ShowWindow(hwnd, Native.SW_RESTORE);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                Insets insets = GetInsets(hwnd);
                int x = target.X - insets.Left;
                int y = target.Y - insets.Top;
                int width = target.Width + insets.Left + insets.Right;
                int height = target.Height + insets.Top + insets.Bottom;
                if (!Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE))
                {
                    return false;
                }
            }
            return true;
        }

        private static Insets GetInsets(IntPtr hwnd)
        {
            Insets insets = new Insets();
            RECT window;
            RECT frame;
            if (!Native.GetWindowRect(hwnd, out window))
            {
                return insets;
            }
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out frame, System.Runtime.InteropServices.Marshal.SizeOf(typeof(RECT))) != 0)
            {
                return insets;
            }
            insets.Left = frame.Left - window.Left;
            insets.Top = frame.Top - window.Top;
            insets.Right = window.Right - frame.Right;
            insets.Bottom = window.Bottom - frame.Bottom;
            if (insets.Left < 0) insets.Left = 0;
            if (insets.Top < 0) insets.Top = 0;
            if (insets.Right < 0) insets.Right = 0;
            if (insets.Bottom < 0) insets.Bottom = 0;
            return insets;
        }

        private static string ClassName(IntPtr hwnd)
        {
            StringBuilder builder = new StringBuilder(256);
            Native.GetClassName(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }
    }
}
