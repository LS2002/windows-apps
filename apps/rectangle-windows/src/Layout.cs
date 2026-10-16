using System;

namespace RectangleWindows
{
    // Layout math ported from Rectangle's HalfSplitFrameCalculation and the
    // calculation classes under Rectangle/WindowCalculation. macOS places the
    // origin at the bottom-left, so a "leading" vertical split is the top of
    // the screen. These methods take a Windows work-area rect (origin at the
    // top-left, Y grows downward) and return the same on-screen result.
    internal struct FrameRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public FrameRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Right
        {
            get { return X + Width; }
        }

        public int Bottom
        {
            get { return Y + Height; }
        }

        public static FrameRect FromRECT(RECT rect)
        {
            return new FrameRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        public bool Near(FrameRect other, int tolerance)
        {
            return Math.Abs(X - other.X) <= tolerance
                && Math.Abs(Y - other.Y) <= tolerance
                && Math.Abs(Width - other.Width) <= tolerance
                && Math.Abs(Height - other.Height) <= tolerance;
        }
    }

    internal enum RotateAction
    {
        CenterHalf,
        Third,
        Half,
        TwoThirds
    }

    internal enum ActionKind
    {
        LeftHalf,
        RightHalf,
        TopHalf,
        BottomHalf,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Maximize,
        AlmostMaximize,
        MaximizeHeight,
        Center,
        Restore,
        FirstThird,
        CenterThird,
        LastThird,
        FirstTwoThirds,
        CenterTwoThirds,
        LastTwoThirds,
        Larger,
        Smaller,
        NextDisplay,
        PreviousDisplay
    }

    internal static class Layout
    {
        public const double AlmostMaximizeFraction = 0.9;
        private const double FloorTolerance = 0.0001;

        public static readonly double[] RepeatedThirds = new double[] { 0.5, 2.0 / 3.0, 1.0 / 3.0 };

        public static int FloorDimension(double value)
        {
            return (int)Math.Floor(value + FloorTolerance);
        }

        public static FrameRect Horizontal(FrameRect visible, bool trailing, double fraction)
        {
            int width = FloorDimension(visible.Width * fraction);
            if (width < 1)
            {
                width = 1;
            }
            if (width > visible.Width)
            {
                width = visible.Width;
            }
            int x = trailing ? visible.Right - width : visible.X;
            return new FrameRect(x, visible.Y, width, visible.Height);
        }

        public static FrameRect Vertical(FrameRect visible, bool top, double fraction)
        {
            int height = FloorDimension(visible.Height * fraction);
            if (height < 1)
            {
                height = 1;
            }
            if (height > visible.Height)
            {
                height = visible.Height;
            }
            int y = top ? visible.Y : visible.Bottom - height;
            return new FrameRect(visible.X, y, visible.Width, height);
        }

        public static FrameRect Corner(FrameRect visible, bool trailing, bool top, double horizontalFraction, double verticalFraction)
        {
            FrameRect horizontal = Horizontal(visible, trailing, horizontalFraction);
            FrameRect vertical = Vertical(visible, top, verticalFraction);
            return new FrameRect(horizontal.X, vertical.Y, horizontal.Width, vertical.Height);
        }

        public static FrameRect Third(FrameRect visible, int index)
        {
            int column = FloorDimension(visible.Width / 3.0);
            if (index <= 0)
            {
                return new FrameRect(visible.X, visible.Y, column, visible.Height);
            }
            if (index >= 2)
            {
                return new FrameRect(visible.Right - column, visible.Y, column, visible.Height);
            }
            int x = visible.X + column;
            int width = (visible.Right - column) - x;
            if (width < 1)
            {
                width = 1;
            }
            return new FrameRect(x, visible.Y, width, visible.Height);
        }

        public static FrameRect Centered(FrameRect visible, FrameRect window)
        {
            int width = window.Width;
            int height = window.Height;
            int x = window.X;
            int y = window.Y;

            if (height >= visible.Height)
            {
                height = visible.Height;
                y = visible.Y;
            }
            else
            {
                y = (int)Math.Round((visible.Height - window.Height) / 2.0) + visible.Y;
            }

            if (width >= visible.Width)
            {
                width = visible.Width;
                x = visible.X;
            }
            else
            {
                x = (int)Math.Round((visible.Width - window.Width) / 2.0) + visible.X;
            }

            return new FrameRect(x, y, width, height);
        }

        public static FrameRect AlmostMaximize(FrameRect visible)
        {
            int width = (int)Math.Round(visible.Width * AlmostMaximizeFraction);
            int height = (int)Math.Round(visible.Height * AlmostMaximizeFraction);
            int x = (int)Math.Round((visible.Width - width) / 2.0) + visible.X;
            int y = (int)Math.Round((visible.Height - height) / 2.0) + visible.Y;
            return new FrameRect(x, y, width, height);
        }

        public static FrameRect MaximizeHeight(FrameRect visible, FrameRect window)
        {
            int width = window.Width;
            int x = window.X;
            if (width > visible.Width)
            {
                width = visible.Width;
                x = visible.X;
            }
            else if (x < visible.X)
            {
                x = visible.X;
            }
            else if (x + width > visible.Right)
            {
                x = visible.Right - width;
            }
            return new FrameRect(x, visible.Y, width, visible.Height);
        }

        public static FrameRect ResizeAboutCenter(FrameRect visible, FrameRect window, int grow)
        {
            int width = window.Width + (grow * 2);
            int height = window.Height + (grow * 2);
            int minWidth = Math.Max(160, visible.Width / 5);
            int minHeight = Math.Max(120, visible.Height / 5);
            if (width < minWidth)
            {
                width = minWidth;
            }
            if (height < minHeight)
            {
                height = minHeight;
            }
            if (width > visible.Width)
            {
                width = visible.Width;
            }
            if (height > visible.Height)
            {
                height = visible.Height;
            }
            int centerX = window.X + (window.Width / 2);
            int centerY = window.Y + (window.Height / 2);
            int x = centerX - (width / 2);
            int y = centerY - (height / 2);
            if (x < visible.X)
            {
                x = visible.X;
            }
            if (y < visible.Y)
            {
                y = visible.Y;
            }
            if (x + width > visible.Right)
            {
                x = visible.Right - width;
            }
            if (y + height > visible.Bottom)
            {
                y = visible.Bottom - height;
            }
            return new FrameRect(x, y, width, height);
        }

        public static FrameRect ForAction(ActionKind action, FrameRect visible, FrameRect window, double fraction)
        {
            switch (action)
            {
                case ActionKind.LeftHalf:
                    return Horizontal(visible, false, fraction);
                case ActionKind.RightHalf:
                    return Horizontal(visible, true, fraction);
                case ActionKind.TopHalf:
                    return Vertical(visible, true, fraction);
                case ActionKind.BottomHalf:
                    return Vertical(visible, false, fraction);
                case ActionKind.TopLeft:
                    return Corner(visible, false, true, 0.5, 0.5);
                case ActionKind.TopRight:
                    return Corner(visible, true, true, 0.5, 0.5);
                case ActionKind.BottomLeft:
                    return Corner(visible, false, false, 0.5, 0.5);
                case ActionKind.BottomRight:
                    return Corner(visible, true, false, 0.5, 0.5);
                case ActionKind.Maximize:
                    return visible;
                case ActionKind.AlmostMaximize:
                    return AlmostMaximize(visible);
                case ActionKind.MaximizeHeight:
                    return MaximizeHeight(visible, window);
                case ActionKind.Center:
                    return Centered(visible, window);
                case ActionKind.FirstThird:
                    return Third(visible, 0);
                case ActionKind.CenterThird:
                    return Third(visible, 1);
                case ActionKind.LastThird:
                    return Third(visible, 2);
                case ActionKind.FirstTwoThirds:
                    return Horizontal(visible, false, 2.0 / 3.0);
                case ActionKind.LastTwoThirds:
                    return Horizontal(visible, true, 2.0 / 3.0);
                case ActionKind.CenterTwoThirds:
                    return CenteredFraction(visible, 2.0 / 3.0);
                default:
                    return window;
            }
        }

        public static FrameRect CenteredFraction(FrameRect visible, double fraction)
        {
            int width = FloorDimension(visible.Width * fraction);
            int x = visible.X + (int)Math.Round((visible.Width - width) / 2.0);
            return new FrameRect(x, visible.Y, width, visible.Height);
        }

        public static FrameRect VerticalCenteredFraction(FrameRect visible, double fraction)
        {
            int height = FloorDimension(visible.Height * fraction);
            int y = visible.Y + (int)Math.Round((visible.Height - height) / 2.0);
            return new FrameRect(visible.X, y, visible.Width, height);
        }

        public static FrameRect Slot(RotateAction action, int index, FrameRect visible)
        {
            bool second = (index % 2) == 1;
            switch (action)
            {
                case RotateAction.CenterHalf:
                    if (!second)
                    {
                        return CenteredFraction(visible, 0.5);
                    }
                    return VerticalCenteredFraction(visible, 0.5);
                case RotateAction.Third:
                    return Third(visible, second ? 2 : 0);
                case RotateAction.Half:
                    return Horizontal(visible, second, 0.5);
                case RotateAction.TwoThirds:
                    return Horizontal(visible, second, 2.0 / 3.0);
                default:
                    return visible;
            }
        }

        public static int MatchingCycleIndex(ActionKind action, FrameRect visible, FrameRect window)
        {
            if (!UsesRepeatedThirds(action))
            {
                return -1;
            }
            for (int i = 0; i < RepeatedThirds.Length; i++)
            {
                FrameRect candidate = ForAction(action, visible, window, RepeatedThirds[i]);
                if (candidate.Near(window, 4))
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool UsesRepeatedThirds(ActionKind action)
        {
            return action == ActionKind.LeftHalf
                || action == ActionKind.RightHalf
                || action == ActionKind.TopHalf
                || action == ActionKind.BottomHalf;
        }
    }
}
