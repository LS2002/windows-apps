using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace RectangleWindows
{
    internal enum ShotDestination
    {
        Clipboard,
        Desktop
    }

    internal static class Screenshots
    {
        public static string FileName(DateTime time)
        {
            return "screenshot-" + time.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png";
        }

        public static Bitmap Capture(Rectangle screenRect)
        {
            Bitmap bitmap = new Bitmap(screenRect.Width, screenRect.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(screenRect.X, screenRect.Y, 0, 0, screenRect.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

        public static void CopyToClipboard(Image image)
        {
            Clipboard.SetImage(image);
        }

        public static string SaveToDesktop(Image image)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return SavePng(image, desktop, DateTime.Now);
        }

        public static string SavePng(Image image, string directory, DateTime time)
        {
            string name = FileName(time);
            string path = Path.Combine(directory, name);
            int suffix = 2;
            while (File.Exists(path))
            {
                name = "screenshot-" + time.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + suffix + ".png";
                path = Path.Combine(directory, name);
                suffix++;
            }
            image.Save(path, ImageFormat.Png);
            return path;
        }
    }

    internal sealed class ScreenshotOverlay : Form
    {
        private readonly ShotDestination destination;
        private readonly Action<string> notify;
        private Point anchor;
        private Point current;
        private bool dragging;
        private bool finishing;
        private Rectangle selected;

        public ScreenshotOverlay(ShotDestination destination, Action<string> notify)
        {
            this.destination = destination;
            this.notify = notify;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.28;
            TopMost = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;
            Bounds = SystemInformation.VirtualScreen;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00000080;
                return parameters;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Bounds = SystemInformation.VirtualScreen;
            Activate();
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Close();
                return;
            }
            if (e.Button != MouseButtons.Left || finishing)
            {
                return;
            }
            anchor = Cursor.Position;
            current = anchor;
            dragging = true;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging)
            {
                return;
            }
            current = Cursor.Position;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left)
            {
                return;
            }
            dragging = false;
            Capture = false;
            current = Cursor.Position;
            Rectangle rect = Normalize(anchor, current);
            if (rect.Width < 3 || rect.Height < 3)
            {
                Close();
                return;
            }
            selected = rect;
            finishing = true;
            Hide();
            Timer timer = new Timer();
            timer.Interval = 80;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                Finish();
            };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Font font = new Font(Font.FontFamily, 11f, FontStyle.Bold))
            using (Brush text = new SolidBrush(Color.White))
            {
                string hint = "Drag to select an area.  Esc cancels.";
                SizeF hintSize = e.Graphics.MeasureString(hint, font);
                float hintX = (ClientSize.Width - hintSize.Width) / 2f;
                e.Graphics.DrawString(hint, font, text, hintX, 24f);
            }

            if (!dragging)
            {
                return;
            }
            Rectangle rect = Normalize(anchor, current);
            Rectangle local = new Rectangle(rect.X - Left, rect.Y - Top, rect.Width, rect.Height);
            using (Pen pen = new Pen(Color.White, 2))
            {
                e.Graphics.DrawRectangle(pen, local);
            }
            string size = rect.Width + " x " + rect.Height;
            using (Font font = new Font(Font.FontFamily, 10f, FontStyle.Bold))
            using (Brush back = new SolidBrush(Color.Black))
            using (Brush text = new SolidBrush(Color.White))
            {
                SizeF measured = e.Graphics.MeasureString(size, font);
                float x = local.X;
                float y = local.Bottom + 6;
                if (y + measured.Height > ClientSize.Height)
                {
                    y = local.Y - measured.Height - 6;
                }
                e.Graphics.FillRectangle(back, x, y, measured.Width + 8, measured.Height);
                e.Graphics.DrawString(size, font, text, x + 4, y);
            }
        }

        private void Finish()
        {
            try
            {
                using (Bitmap bitmap = Screenshots.Capture(selected))
                {
                    if (destination == ShotDestination.Clipboard)
                    {
                        Screenshots.CopyToClipboard(bitmap);
                        Notify("Screenshot copied to the clipboard.");
                    }
                    else
                    {
                        string path = Screenshots.SaveToDesktop(bitmap);
                        Notify("Saved " + Path.GetFileName(path) + " to the desktop.");
                    }
                }
            }
            catch (Exception ex)
            {
                Notify("Screenshot failed: " + ex.Message);
            }
            Close();
        }

        private void Notify(string message)
        {
            if (notify != null)
            {
                notify(message);
            }
        }

        private static Rectangle Normalize(Point start, Point end)
        {
            int x = Math.Min(start.X, end.X);
            int y = Math.Min(start.Y, end.Y);
            int width = Math.Abs(end.X - start.X);
            int height = Math.Abs(end.Y - start.Y);
            return new Rectangle(x, y, width, height);
        }
    }
}
