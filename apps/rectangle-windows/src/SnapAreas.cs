using System;
using System.Windows.Forms;

namespace RectangleWindows
{
    internal enum SnapZone
    {
        None,
        LeftHalf,
        RightHalf,
        TopHalf,
        BottomHalf,
        Maximize,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        BottomFirstThird,
        BottomCenterThird,
        BottomLastThird
    }

    // Drag targets follow Rectangle's snap-area table: edges, corners,
    // the side bands that become a top or bottom half, and bottom thirds.
    internal static class SnapAreas
    {
        public const int Edge = 8;
        public const int Corner = 50;
        public const int SideBand = 140;

        public static SnapZone HitTest(int x, int y, FrameRect monitor)
        {
            bool nearLeft = x <= monitor.X + Edge;
            bool nearRight = x >= monitor.Right - Edge;
            bool nearTop = y <= monitor.Y + Edge;
            bool nearBottom = y >= monitor.Bottom - Edge;
            bool cornerX = x <= monitor.X + Corner || x >= monitor.Right - Corner;
            bool cornerY = y <= monitor.Y + Corner || y >= monitor.Bottom - Corner;
            if ((nearLeft || nearRight) && (nearTop || nearBottom) && cornerX && cornerY)
            {
                if (nearTop && nearLeft) return SnapZone.TopLeft;
                if (nearTop && nearRight) return SnapZone.TopRight;
                if (nearBottom && nearLeft) return SnapZone.BottomLeft;
                return SnapZone.BottomRight;
            }

            bool upperBand = y <= monitor.Y + SideBand;
            bool lowerBand = y >= monitor.Bottom - SideBand;
            if ((nearLeft || nearRight) && upperBand && !nearTop)
            {
                return SnapZone.TopHalf;
            }
            if ((nearLeft || nearRight) && lowerBand && !nearBottom)
            {
                return SnapZone.BottomHalf;
            }
            if (nearBottom)
            {
                int span = monitor.Width / 3;
                if (x < monitor.X + span) return SnapZone.BottomFirstThird;
                if (x >= monitor.Right - span) return SnapZone.BottomLastThird;
                return SnapZone.BottomCenterThird;
            }
            if (nearLeft) return SnapZone.LeftHalf;
            if (nearRight) return SnapZone.RightHalf;
            if (nearTop) return SnapZone.Maximize;
            return SnapZone.None;
        }

        public static ActionKind ToAction(SnapZone zone)
        {
            switch (zone)
            {
                case SnapZone.LeftHalf: return ActionKind.LeftHalf;
                case SnapZone.RightHalf: return ActionKind.RightHalf;
                case SnapZone.TopHalf: return ActionKind.TopHalf;
                case SnapZone.BottomHalf: return ActionKind.BottomHalf;
                case SnapZone.Maximize: return ActionKind.Maximize;
                case SnapZone.TopLeft: return ActionKind.TopLeft;
                case SnapZone.TopRight: return ActionKind.TopRight;
                case SnapZone.BottomLeft: return ActionKind.BottomLeft;
                case SnapZone.BottomRight: return ActionKind.BottomRight;
                case SnapZone.BottomFirstThird: return ActionKind.FirstThird;
                case SnapZone.BottomCenterThird: return ActionKind.CenterThird;
                case SnapZone.BottomLastThird: return ActionKind.LastThird;
                default: return ActionKind.Maximize;
            }
        }

        public static FrameRect MonitorAt(int x, int y)
        {
            FrameRect found = new FrameRect();
            bool any = false;
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
            {
                MONITORINFO info = new MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
                if (!Native.GetMonitorInfo(hMonitor, ref info))
                {
                    return true;
                }
                if (x >= info.rcMonitor.Left && x < info.rcMonitor.Right && y >= info.rcMonitor.Top && y < info.rcMonitor.Bottom)
                {
                    found = FrameRect.FromRECT(info.rcMonitor);
                    any = true;
                }
                return true;
            }, IntPtr.Zero);
            if (!any)
            {
                return new FrameRect(0, 0, 0, 0);
            }
            return found;
        }

        public static FrameRect WorkAreaAt(int x, int y)
        {
            FrameRect found = new FrameRect();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data)
            {
                MONITORINFO info = new MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
                if (!Native.GetMonitorInfo(hMonitor, ref info))
                {
                    return true;
                }
                if (x >= info.rcMonitor.Left && x < info.rcMonitor.Right && y >= info.rcMonitor.Top && y < info.rcMonitor.Bottom)
                {
                    found = FrameRect.FromRECT(info.rcWork);
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }

    internal sealed class FootprintForm : Form
    {
        public FootprintForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = System.Drawing.Color.FromArgb(47, 132, 229);
            Opacity = 0.35;
            TopMost = true;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;
                cp.ExStyle |= 0x08000000;
                cp.ExStyle |= 0x00000020;
                return cp;
            }
        }
    }
}
