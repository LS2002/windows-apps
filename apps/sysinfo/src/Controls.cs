using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SysInfo
{
    internal static class Theme
    {
        public static readonly Color Surface = Color.FromArgb(255, 245, 230);
        public static readonly Color Card = Color.White;
        public static readonly Color Primary = Color.FromArgb(250, 212, 192);
        public static readonly Color Secondary = Color.FromArgb(128, 161, 193);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Warning = Color.FromArgb(217, 119, 6);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Ink = Color.FromArgb(17, 24, 39);
        public static readonly Color Muted = Color.FromArgb(55, 65, 81);
        public static readonly Color Line = Color.FromArgb(232, 213, 196);
        public static readonly Color Storage = Color.FromArgb(176, 112, 86);

        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int d = Math.Max(2, radius * 2);
            if (d > bounds.Width)
            {
                d = bounds.Width;
            }
            if (d > bounds.Height)
            {
                d = bounds.Height;
            }
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SensorTile : Control
    {
        private readonly List<float> history = new List<float>();
        private readonly Font valueFont = new Font("Segoe UI", 13f, FontStyle.Bold);
        private readonly Font captionFont = new Font("Segoe UI", 8.25f);
        private readonly Font badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        private string badge = "";
        private string valueText = "…";
        private string caption = "";
        private string side = "";
        private const int HistoryLimit = 36;
        private static readonly TextFormatFlags Tight = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        public SensorTile()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Margin = new Padding(6);
            BackColor = Theme.Card;
            ApplySize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                valueFont.Dispose();
                captionFont.Dispose();
                badgeFont.Dispose();
            }
            base.Dispose(disposing);
        }

        public void SetReading(string badgeText, string value, string captionText, string sideText, float sample, bool keepSample)
        {
            badge = badgeText ?? "";
            valueText = value ?? "";
            caption = captionText ?? "";
            side = sideText ?? "";
            if (keepSample && !float.IsNaN(sample) && !float.IsInfinity(sample))
            {
                history.Add(sample);
                if (history.Count > HistoryLimit)
                {
                    history.RemoveAt(0);
                }
            }
            ApplySize();
            Invalidate();
        }

        private void ApplySize()
        {
            Size next = NaturalSize();
            if (Width < next.Width || Height < next.Height)
            {
                Size = next;
            }
        }

        private Size NaturalSize()
        {
            Size valueSize = TextRenderer.MeasureText("00.00", valueFont, Size.Empty, Tight);
            Size captionSize = TextRenderer.MeasureText("GHz", captionFont, Size.Empty, Tight);
            int pad = 10;
            int width = Math.Max(valueSize.Width, captionSize.Width);
            int height = pad + valueSize.Height + 4 + captionSize.Height + 8 + 8 + pad;
            if (badge.Length > 0 || side.Length > 0)
            {
                Size metaSize = TextRenderer.MeasureText("00", badgeFont, Size.Empty, Tight);
                Size sideSize = TextRenderer.MeasureText("100%", badgeFont, Size.Empty, Tight);
                width = Math.Max(width, metaSize.Width + sideSize.Width + 12);
                height += metaSize.Height + 4;
            }
            return new Size(width + pad * 2, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Card);
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(bounds, 8))
            using (SolidBrush fill = new SolidBrush(Theme.Primary))
            {
                g.FillPath(fill, path);
            }

            int pad = 10;
            int y = pad;
            Color muted = Theme.Muted;
            if (badge.Length > 0 || side.Length > 0)
            {
                int metaH = TextRenderer.MeasureText("0", badgeFont, Size.Empty, Tight).Height;
                Rectangle meta = new Rectangle(pad, y, Width - pad * 2, metaH);
                if (badge.Length > 0)
                {
                    TextRenderer.DrawText(g, badge, badgeFont, meta, muted, TextFormatFlags.Left | TextFormatFlags.Top | Tight);
                }
                if (side.Length > 0)
                {
                    TextRenderer.DrawText(g, side, badgeFont, meta, muted, TextFormatFlags.Right | TextFormatFlags.Top | Tight);
                }
                y += metaH + 4;
            }

            int valueH = TextRenderer.MeasureText("0", valueFont, Size.Empty, Tight).Height;
            int captionH = TextRenderer.MeasureText("0", captionFont, Size.Empty, Tight).Height;
            Rectangle valueRect = new Rectangle(pad, y, Width - pad * 2, valueH);
            TextRenderer.DrawText(g, valueText, valueFont, valueRect, Theme.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | Tight);
            y += valueH + 2;
            Rectangle captionRect = new Rectangle(pad, y, Width - pad * 2, captionH);
            TextRenderer.DrawText(g, caption, captionFont, captionRect, muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | Tight);
            int sparkTop = Height - pad - 8;
            if (sparkTop < y + captionH + 4)
            {
                sparkTop = y + captionH + 4;
            }
            DrawSpark(g, new Rectangle(pad, sparkTop, Math.Max(4, Width - pad * 2), 8));
        }

        private void DrawSpark(Graphics g, Rectangle area)
        {
            if (history.Count < 2)
            {
                return;
            }
            float min = history[0];
            float max = history[0];
            for (int i = 1; i < history.Count; i++)
            {
                if (history[i] < min)
                {
                    min = history[i];
                }
                if (history[i] > max)
                {
                    max = history[i];
                }
            }
            float span = max - min;
            if (span < 0.001f)
            {
                span = 1f;
                min = max - 0.5f;
            }
            using (Pen pen = new Pen(Theme.Secondary, 1.6f))
            {
                pen.LineJoin = LineJoin.Round;
                PointF[] points = new PointF[history.Count];
                float step = (area.Width - 1f) / (history.Count - 1);
                for (int i = 0; i < history.Count; i++)
                {
                    float t = (history[i] - min) / span;
                    float y = area.Bottom - 1f - t * (area.Height - 2f);
                    points[i] = new PointF(area.Left + i * step, y);
                }
                g.DrawLines(pen, points);
            }
        }
    }

    internal sealed class SpecGrid : Control
    {
        private readonly string[] captions;
        private readonly string[] values;
        private readonly Font rowFont = new Font("Segoe UI", 9f);
        private static readonly TextFormatFlags Tight = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        public SpecGrid(string[] captions)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.captions = captions ?? new string[0];
            values = new string[this.captions.Length];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = "";
            }
            BackColor = Theme.Card;
        }

        public void SetValue(int index, string text)
        {
            if (index < 0 || index >= values.Length)
            {
                return;
            }
            string next = text ?? "";
            if (values[index] == next)
            {
                return;
            }
            values[index] = next;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                rowFont.Dispose();
            }
            base.Dispose(disposing);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            return new Size(Math.Max(width, 200), ContentHeight(width));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int labelWidth = MeasureLabelWidth();
            int rowHeight = MeasureRowHeight();
            int columns = ColumnsFor(Width, labelWidth);
            int gap = 20;
            int colWidth = columns <= 1 ? Width : (Width - gap) / columns;
            int boxWidth = colWidth - labelWidth - 8;
            if (boxWidth < 40)
            {
                boxWidth = 40;
            }
            for (int i = 0; i < captions.Length; i++)
            {
                int column = i % columns;
                int row = i / columns;
                int x = column * (colWidth + gap);
                int y = row * rowHeight;
                Rectangle labelRect = new Rectangle(x, y, labelWidth - 10, rowHeight);
                TextRenderer.DrawText(e.Graphics, captions[i], rowFont, labelRect, Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | Tight);
                Rectangle box = new Rectangle(x + labelWidth, y + 4, boxWidth, rowHeight - 8);
                using (SolidBrush fill = new SolidBrush(Theme.Surface))
                {
                    e.Graphics.FillRectangle(fill, box);
                }
                using (Pen pen = new Pen(Theme.Line))
                {
                    e.Graphics.DrawRectangle(pen, box);
                }
                Rectangle textRect = new Rectangle(box.X + 10, box.Y, Math.Max(4, box.Width - 20), box.Height);
                TextRenderer.DrawText(e.Graphics, values[i], rowFont, textRect, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | Tight);
            }
        }

        private int ContentHeight(int width)
        {
            int rows = (captions.Length + ColumnsFor(width, MeasureLabelWidth()) - 1) / Math.Max(1, ColumnsFor(width, MeasureLabelWidth()));
            if (captions.Length == 0)
            {
                rows = 0;
            }
            if (rows < 1)
            {
                rows = 1;
            }
            return rows * MeasureRowHeight();
        }

        private int ColumnsFor(int width, int labelWidth)
        {
            int minColumn = labelWidth + 220;
            if (width >= minColumn * 2 + 20)
            {
                return 2;
            }
            return 1;
        }

        private int MeasureLabelWidth()
        {
            int width = 48;
            for (int i = 0; i < captions.Length; i++)
            {
                Size size = TextRenderer.MeasureText(captions[i], rowFont, Size.Empty, Tight);
                int wanted = size.Width + 18;
                if (wanted > width)
                {
                    width = wanted;
                }
            }
            return width;
        }

        private int MeasureRowHeight()
        {
            return TextRenderer.MeasureText("Ag", rowFont, Size.Empty, Tight).Height + 16;
        }
    }

    internal sealed class SectionPanel : Panel
    {
        private readonly Color accent;
        public bool Wide;

        public SectionPanel(string title, Color accent)
        {
            this.accent = accent;
            Wide = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;

            Label heading = new Label();
            heading.Text = title;
            heading.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            heading.ForeColor = Theme.Ink;
            heading.BackColor = Theme.Card;
            heading.AutoSize = true;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(heading);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            int inner = Math.Max(120, width - 32);
            int y = 16;
            for (int i = 0; i < Controls.Count; i++)
            {
                Size pref = Controls[i].GetPreferredSize(new Size(inner, 0));
                int h = pref.Height;
                if (h < 18)
                {
                    h = Controls[i].Height > 0 ? Controls[i].Height : 18;
                }
                y += h + 8;
            }
            return new Size(width, y + 12);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            int inner = Math.Max(120, ClientSize.Width - 32);
            int y = 16;
            for (int i = 0; i < Controls.Count; i++)
            {
                Control child = Controls[i];
                if (child is Label && ((Label)child).AutoSize)
                {
                    ((Label)child).MaximumSize = new Size(inner, 0);
                }
                Size pref = child.GetPreferredSize(new Size(inner, 0));
                int h = pref.Height;
                if (child is Label && h < 20)
                {
                    h = 20;
                }
                if (h < 16)
                {
                    h = 16;
                }
                child.SetBounds(16, y, inner, h);
                y += h + 8;
            }
            Height = y + 16;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Surface);
            Rectangle card = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(card, 8))
            using (SolidBrush fill = new SolidBrush(Theme.Card))
            using (Pen pen = new Pen(Theme.Line))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
            using (SolidBrush bar = new SolidBrush(accent))
            {
                e.Graphics.FillRectangle(bar, 12, 8, 36, 4);
            }
        }
    }

    internal sealed class TileGrid : Panel
    {
        private bool placing;

        public TileGrid()
        {
            BackColor = Theme.Card;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            if (width < 40)
            {
                width = 40;
            }
            int rowsHeight = Measure(width, false);
            return new Size(width, rowsHeight);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (placing)
            {
                return;
            }
            placing = true;
            int height = Measure(Width, true);
            if (Height != height)
            {
                Height = height;
            }
            placing = false;
        }

        private int Measure(int width, bool place)
        {
            int cellWidth = 0;
            int cellHeight = 0;
            for (int i = 0; i < Controls.Count; i++)
            {
                Control child = Controls[i];
                if (child.Width > cellWidth)
                {
                    cellWidth = child.Width;
                }
                if (child.Height > cellHeight)
                {
                    cellHeight = child.Height;
                }
            }
            int x = 0;
            int y = 0;
            int lineHeight = 0;
            for (int i = 0; i < Controls.Count; i++)
            {
                Control child = Controls[i];
                int slotW = cellWidth + child.Margin.Horizontal;
                int slotH = cellHeight + child.Margin.Vertical;
                if (x > 0 && x + slotW > width)
                {
                    x = 0;
                    y += lineHeight;
                    lineHeight = 0;
                }
                if (place && (child.Width != cellWidth || child.Height != cellHeight))
                {
                    child.Size = new Size(cellWidth, cellHeight);
                }
                if (place)
                {
                    child.SetBounds(x + child.Margin.Left, y + child.Margin.Top, cellWidth, cellHeight);
                }
                x += slotW;
                if (slotH > lineHeight)
                {
                    lineHeight = slotH;
                }
            }
            return y + lineHeight;
        }
    }

    internal sealed class FitLabel : Label
    {
        public FitLabel()
        {
            AutoSize = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            Rectangle rect = new Rectangle(8, 0, Math.Max(4, Width - 16), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                rect,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
}
