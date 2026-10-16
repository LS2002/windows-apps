using System;
using System.Drawing;
using System.Windows.Forms;

namespace RectangleWindows
{
    internal sealed class ShortcutRecorder : Control
    {
        private HotkeySpec value;
        private bool recording;
        private bool hovered;

        public event EventHandler RecordingStarted;
        public event EventHandler RecordingEnded;
        public event EventHandler ValueChanged;

        public ShortcutRecorder()
        {
            value = new HotkeySpec();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(160, 24);
        }

        public HotkeySpec Value
        {
            get { return value == null ? new HotkeySpec() : value.Clone(); }
            set
            {
                this.value = value == null ? new HotkeySpec() : value.Clone();
                Invalidate();
            }
        }

        public bool IsRecording
        {
            get { return recording; }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            BeginRecording();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            if (recording)
            {
                EndRecording(false);
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right || key == Keys.Tab)
            {
                return recording;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!recording)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    BeginRecording();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                EndRecording(false);
                return;
            }
            if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
            {
                value = new HotkeySpec();
                EndRecording(true);
                return;
            }
            if (IsModifier(e.KeyCode))
            {
                Invalidate();
                return;
            }

            uint modifiers = 0;
            if (e.Control)
            {
                modifiers |= Native.MOD_CONTROL;
            }
            if (e.Alt)
            {
                modifiers |= Native.MOD_ALT;
            }
            if (e.Shift)
            {
                modifiers |= Native.MOD_SHIFT;
            }
            if ((Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0)
            {
                modifiers |= Native.MOD_WIN;
            }
            if (modifiers == 0)
            {
                return;
            }

            value = new HotkeySpec(modifiers, (uint)e.KeyCode);
            EndRecording(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = recording ? Color.FromArgb(232, 242, 255) : (hovered || Focused ? Color.FromArgb(245, 245, 245) : Color.White);
            Color border = recording ? Color.FromArgb(47, 132, 229) : Color.FromArgb(180, 180, 180);
            using (SolidBrush brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }
            using (Pen pen = new Pen(border))
            {
                g.DrawRectangle(pen, bounds);
            }

            string text;
            Color textColor;
            if (recording)
            {
                text = "Type shortcut";
                textColor = Color.FromArgb(47, 132, 229);
            }
            else if (value == null || value.IsEmpty)
            {
                text = "Click to record";
                textColor = Color.FromArgb(140, 140, 140);
            }
            else
            {
                text = value.Display;
                textColor = Color.FromArgb(30, 30, 30);
            }

            using (Font font = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (SolidBrush brush = new SolidBrush(textColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, brush, new RectangleF(2, 0, Width - 4, Height), format);
            }
        }

        private void BeginRecording()
        {
            if (recording)
            {
                return;
            }
            recording = true;
            Focus();
            Invalidate();
            if (RecordingStarted != null)
            {
                RecordingStarted(this, EventArgs.Empty);
            }
        }

        private void EndRecording(bool changed)
        {
            if (!recording)
            {
                return;
            }
            recording = false;
            Invalidate();
            if (RecordingEnded != null)
            {
                RecordingEnded(this, EventArgs.Empty);
            }
            if (changed && ValueChanged != null)
            {
                ValueChanged(this, EventArgs.Empty);
            }
        }

        private static bool IsModifier(Keys key)
        {
            return key == Keys.ControlKey
                || key == Keys.LControlKey
                || key == Keys.RControlKey
                || key == Keys.ShiftKey
                || key == Keys.LShiftKey
                || key == Keys.RShiftKey
                || key == Keys.Menu
                || key == Keys.LMenu
                || key == Keys.RMenu
                || key == Keys.LWin
                || key == Keys.RWin;
        }
    }
}
