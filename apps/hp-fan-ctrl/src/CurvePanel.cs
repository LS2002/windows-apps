using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HpFanCtrl
{
    internal sealed class CurvePanel : Control
    {
        private FanPoint[] points = FanCurve.DefaultPoints();
        private int drag = -1;
        private int liveTemp;
        private bool hasLive;
        private float dpiScale = 1f;

        public event EventHandler PointChanged;

        public CurvePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Cursor = Cursors.Hand;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = CreateGraphics())
            {
                dpiScale = Math.Max(1f, g.DpiY / 96f);
            }
        }

        private int Px(int value)
        {
            return (int)Math.Round(value * dpiScale);
        }

        public FanPoint[] Points
        {
            get { return points; }
            set
            {
                points = value ?? FanCurve.DefaultPoints();
                Invalidate();
            }
        }

        public void SetLive(int tempC, bool active)
        {
            liveTemp = tempC;
            hasLive = active;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            drag = HitTest(e.Location);
            if (drag >= 0)
            {
                ApplyDrag(e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag >= 0 && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                ApplyDrag(e.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            drag = -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle plot = PlotBounds();
            int dot = Math.Max(5, Px(6));
            using (Pen axis = new Pen(Color.FromArgb(226, 230, 236)))
            using (Pen grid = new Pen(Color.FromArgb(236, 239, 244)))
            using (Pen line = new Pen(Color.FromArgb(37, 99, 235), Math.Max(2f, Px(2))))
            using (SolidBrush pointBrush = new SolidBrush(Color.FromArgb(37, 99, 235)))
            using (SolidBrush text = new SolidBrush(Color.FromArgb(90, 98, 110)))
            using (Font font = new Font("Segoe UI", 8.5f))
            {
                g.Clear(Color.White);
                g.DrawRectangle(axis, plot);
                for (int percent = 0; percent <= 100; percent += 25)
                {
                    int y = Y(plot, percent);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    string percentLabel = percent.ToString() + "%";
                    SizeF percentSize = g.MeasureString(percentLabel, font);
                    float labelY = y - percentSize.Height / 2f;
                    if (labelY < 0)
                    {
                        labelY = 0;
                    }
                    g.DrawString(percentLabel, font, text, 0, labelY);
                }
                if (points.Length > 1)
                {
                    PointF[] linePoints = new PointF[points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        linePoints[i] = new PointF(X(plot, i), Y(plot, points[i].Percent));
                    }
                    g.DrawLines(line, linePoints);
                }
                if (hasLive && points.Length > 0)
                {
                    int percent = FanCurve.PercentAt(points, liveTemp);
                    float x = XForTemp(plot, liveTemp);
                    using (Pen marker = new Pen(Color.FromArgb(220, 38, 38), 1.4f))
                    using (SolidBrush markerBrush = new SolidBrush(Color.FromArgb(220, 38, 38)))
                    {
                        g.DrawLine(marker, x, plot.Top, x, plot.Bottom);
                        int liveDot = Math.Max(4, Px(4));
                        g.FillEllipse(markerBrush, x - liveDot, Y(plot, percent) - liveDot, liveDot * 2, liveDot * 2);
                    }
                }
                for (int i = 0; i < points.Length; i++)
                {
                    float x = X(plot, i);
                    float y = Y(plot, points[i].Percent);
                    g.FillEllipse(pointBrush, x - dot, y - dot, dot * 2, dot * 2);
                    g.DrawEllipse(Pens.White, x - dot, y - dot, dot * 2, dot * 2);
                    string label = points[i].Temp.ToString() + "°";
                    SizeF size = g.MeasureString(label, font);
                    g.DrawString(label, font, text, x - size.Width / 2f, plot.Bottom + Px(4));
                }
            }
        }

        private void ApplyDrag(int mouseY)
        {
            if (drag < 0 || drag >= points.Length)
            {
                return;
            }
            Rectangle plot = PlotBounds();
            int percent = PercentFromY(plot, mouseY);
            FanCurve.SetPercent(points, drag, percent);
            Invalidate();
            EventHandler handler = PointChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private int HitTest(Point location)
        {
            Rectangle plot = PlotBounds();
            int best = -1;
            float bestDistance = Px(16);
            for (int i = 0; i < points.Length; i++)
            {
                float dx = location.X - X(plot, i);
                float dy = location.Y - Y(plot, points[i].Percent);
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private Rectangle PlotBounds()
        {
            int left = Px(52);
            int top = Px(12);
            int right = Px(16);
            int bottom = Px(28);
            return Rectangle.FromLTRB(left, top, Math.Max(left + 10, Width - right), Math.Max(top + 10, Height - bottom));
        }

        private float X(Rectangle plot, int index)
        {
            if (points.Length <= 1)
            {
                return plot.Left;
            }
            return plot.Left + (plot.Width - 1) * (index / (float)(points.Length - 1));
        }

        private float XForTemp(Rectangle plot, int temp)
        {
            if (temp <= points[0].Temp)
            {
                return X(plot, 0);
            }
            int last = points.Length - 1;
            if (temp >= points[last].Temp)
            {
                return X(plot, last);
            }
            for (int i = 1; i < points.Length; i++)
            {
                if (temp <= points[i].Temp)
                {
                    float span = points[i].Temp - points[i - 1].Temp;
                    float t = span <= 0 ? 0 : (temp - points[i - 1].Temp) / span;
                    return X(plot, i - 1) + (X(plot, i) - X(plot, i - 1)) * t;
                }
            }
            return X(plot, last);
        }

        private static int Y(Rectangle plot, int percent)
        {
            if (percent < 0)
            {
                percent = 0;
            }
            if (percent > 100)
            {
                percent = 100;
            }
            return plot.Bottom - (int)Math.Round((plot.Height - 1) * (percent / 100.0));
        }

        private static int PercentFromY(Rectangle plot, int mouseY)
        {
            if (mouseY < plot.Top)
            {
                mouseY = plot.Top;
            }
            if (mouseY > plot.Bottom)
            {
                mouseY = plot.Bottom;
            }
            double t = (plot.Bottom - mouseY) / (double)Math.Max(1, plot.Height - 1);
            return (int)Math.Round(t * 100.0);
        }
    }
}
