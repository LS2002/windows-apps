using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace HpFanCtrl
{
    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly HpBios bios = new HpBios();
        private readonly Label cpuLabel;
        private readonly Label gpuLabel;
        private readonly Label targetLabel;
        private readonly Label statusLabel;
        private readonly Label machineLabel;
        private readonly RadioButton cpuRadio;
        private readonly RadioButton gpuRadio;
        private readonly RadioButton combinedRadio;
        private readonly Button startButton;
        private readonly Button stopButton;
        private readonly Button resetButton;
        private readonly CurvePanel curve;
        private readonly NumericUpDown[] editors;
        private readonly System.Windows.Forms.Timer clock;
        private readonly NotifyIcon tray;
        private readonly int unit;
        private int busy;
        private bool running;
        private bool closing;
        private bool quit;
        private bool biosReady;
        private bool updatingEditors;
        private bool notedTray;

        public MainForm()
        {
            settings = AppSettings.Load();
            Text = "hp-fan-ctrl";
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(244, 246, 248);
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(640, 560);
            unit = MeasureUnit();
            ClientSize = FitClient();
            Padding = new Padding(unit, unit, unit, unit / 2);
            DoubleBuffered = true;

            int titleH = unit * 2;
            int hintH = unit * 2;
            int cardH = unit * 4;
            int actionH = unit * 3;
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Top;
            header.Height = titleH + hintH + cardH + actionH + unit;
            header.ColumnCount = 1;
            header.RowCount = 4;
            header.Margin = new Padding(0);
            header.Padding = new Padding(0, 0, 0, unit / 2);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, titleH));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, hintH));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, cardH));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, actionH));

            machineLabel = new Label();
            machineLabel.AutoSize = false;
            machineLabel.AutoEllipsis = true;
            machineLabel.Dock = DockStyle.Fill;
            machineLabel.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            machineLabel.TextAlign = ContentAlignment.MiddleLeft;
            machineLabel.Text = "HP";
            header.Controls.Add(machineLabel, 0, 0);

            Label hint = new Label();
            hint.AutoSize = false;
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = Color.FromArgb(90, 98, 110);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = "Drag a point on the graph. A hotter temperature uses a faster fan.";
            header.Controls.Add(hint, 0, 1);

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 3;
            cards.RowCount = 1;
            cards.Margin = new Padding(0, unit / 5, 0, unit / 5);
            cards.Padding = new Padding(0);
            for (int col = 0; col < 3; col++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            }
            cards.Controls.Add(MakeCard("CPU", out cpuLabel, true), 0, 0);
            cards.Controls.Add(MakeCard("GPU", out gpuLabel, true), 1, 0);
            cards.Controls.Add(MakeCard("Fan", out targetLabel, false), 2, 0);
            header.Controls.Add(cards, 0, 2);

            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 2;
            actions.RowCount = 1;
            actions.Margin = new Padding(0, 0, 0, unit / 3);
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

            FlowLayoutPanel sources = new FlowLayoutPanel();
            sources.Dock = DockStyle.Fill;
            sources.FlowDirection = FlowDirection.LeftToRight;
            sources.WrapContents = false;
            sources.Padding = new Padding(0, unit / 3, 0, 0);
            sources.Margin = new Padding(0);
            cpuRadio = MakeRadio("CPU", settings.Source == TempSource.Cpu);
            gpuRadio = MakeRadio("GPU", settings.Source == TempSource.Gpu);
            combinedRadio = MakeRadio("Combined", settings.Source != TempSource.Cpu && settings.Source != TempSource.Gpu);
            cpuRadio.CheckedChanged += OnSourceChanged;
            gpuRadio.CheckedChanged += OnSourceChanged;
            combinedRadio.CheckedChanged += OnSourceChanged;
            sources.Controls.Add(cpuRadio);
            sources.Controls.Add(gpuRadio);
            sources.Controls.Add(combinedRadio);
            actions.Controls.Add(sources, 0, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, unit / 5, 0, 0);
            buttons.Margin = new Padding(0);
            resetButton = MakeButton("Reset");
            resetButton.Click += OnReset;
            stopButton = MakeButton("BIOS auto");
            stopButton.Enabled = false;
            stopButton.Click += OnStop;
            startButton = MakeButton("Start");
            startButton.Click += OnStart;
            buttons.Controls.Add(resetButton);
            buttons.Controls.Add(stopButton);
            buttons.Controls.Add(startButton);
            actions.Controls.Add(buttons, 1, 0);
            header.Controls.Add(actions, 0, 3);

            Panel editorHost = new Panel();
            editorHost.Dock = DockStyle.Bottom;
            editorHost.Height = unit * 6;
            editorHost.Padding = new Padding(0, unit / 2, 0, 0);
            editorHost.Margin = new Padding(0);

            Label editorCaption = new Label();
            editorCaption.Dock = DockStyle.Top;
            editorCaption.Height = unit + unit / 2;
            editorCaption.ForeColor = Color.FromArgb(90, 98, 110);
            editorCaption.TextAlign = ContentAlignment.MiddleLeft;
            editorCaption.Text = "Fan percent at each temperature";

            TableLayoutPanel pointRow = new TableLayoutPanel();
            pointRow.Dock = DockStyle.Fill;
            pointRow.ColumnCount = settings.Points.Length;
            pointRow.RowCount = 2;
            pointRow.Padding = new Padding(unit * 3, 0, 0, 0);
            pointRow.Margin = new Padding(0);
            pointRow.RowStyles.Add(new RowStyle(SizeType.Absolute, unit + unit / 2));
            pointRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            editors = new NumericUpDown[settings.Points.Length];
            for (int i = 0; i < settings.Points.Length; i++)
            {
                pointRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / settings.Points.Length));

                Label tempLabel = new Label();
                tempLabel.Dock = DockStyle.Fill;
                tempLabel.TextAlign = ContentAlignment.MiddleCenter;
                tempLabel.ForeColor = Color.FromArgb(70, 78, 90);
                tempLabel.Text = settings.Points[i].Temp.ToString(CultureInfo.InvariantCulture) + "°C";
                pointRow.Controls.Add(tempLabel, i, 0);

                NumericUpDown editor = new NumericUpDown();
                editor.Minimum = 0;
                editor.Maximum = 100;
                editor.DecimalPlaces = 0;
                editor.Increment = 5;
                editor.Value = settings.Points[i].Percent;
                editor.TextAlign = HorizontalAlignment.Center;
                editor.Width = Math.Max(64, unit * 4);
                editor.Anchor = AnchorStyles.None;
                editor.Tag = i;
                editor.ValueChanged += OnEditorChanged;
                editors[i] = editor;
                pointRow.Controls.Add(editor, i, 1);
            }
            editorHost.Controls.Add(pointRow);
            editorHost.Controls.Add(editorCaption);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Height = unit * 3;
            statusLabel.ForeColor = Color.FromArgb(70, 78, 90);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Text = "Reading temperatures…";

            curve = new CurvePanel();
            curve.Dock = DockStyle.Fill;
            curve.Margin = new Padding(0, unit / 2, 0, 0);
            curve.Points = settings.Points;
            curve.PointChanged += OnCurveChanged;

            Controls.Add(curve);
            Controls.Add(editorHost);
            Controls.Add(statusLabel);
            Controls.Add(header);

            tray = new NotifyIcon();
            tray.Icon = MakeTrayIcon();
            tray.Text = "hp-fan-ctrl";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowFromTray(); };
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("Open");
            openItem.Click += delegate { ShowFromTray(); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { Application.Exit(); };
            menu.Items.Add(openItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            tray.ContextMenuStrip = menu;
            Icon = (Icon)tray.Icon.Clone();

            clock = new System.Windows.Forms.Timer();
            clock.Interval = 4000;
            clock.Tick += OnTick;
            Load += OnLoad;
            FormClosing += OnClosing;
        }

        private int MeasureUnit()
        {
            int dpi = 96;
            IntPtr dc = GetDC(IntPtr.Zero);
            if (dc != IntPtr.Zero)
            {
                int value = GetDeviceCaps(dc, 90);
                ReleaseDC(IntPtr.Zero, dc);
                if (value >= 96)
                {
                    dpi = value;
                }
            }
            int line = (int)Math.Ceiling(Font.SizeInPoints / 72f * dpi * 1.45f);
            return Math.Max(16, line);
        }

        private Size FitClient()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int width = Math.Min(unit * 46, work.Width - unit * 4);
            int height = Math.Min(unit * 40, work.Height - unit * 4);
            width = Math.Max(width, unit * 34);
            height = Math.Max(height, unit * 32);
            return new Size(width, height);
        }

        private Control MakeCard(string caption, out Label value, bool gap)
        {
            Panel card = new Panel();
            card.Dock = DockStyle.Fill;
            card.BackColor = Color.White;
            card.Margin = new Padding(0, 0, gap ? unit / 2 : 0, 0);
            card.Padding = new Padding(unit, unit / 3, unit / 2, unit / 3);

            Label title = new Label();
            title.Text = caption;
            title.Dock = DockStyle.Top;
            title.Height = unit + 2;
            title.ForeColor = Color.FromArgb(110, 118, 130);
            title.TextAlign = ContentAlignment.BottomLeft;

            value = new Label();
            value.Text = "—";
            value.Dock = DockStyle.Fill;
            value.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            value.TextAlign = ContentAlignment.MiddleLeft;

            card.Controls.Add(value);
            card.Controls.Add(title);
            return card;
        }

        private RadioButton MakeRadio(string text, bool check)
        {
            RadioButton radio = new RadioButton();
            radio.Text = text;
            radio.AutoSize = true;
            radio.Margin = new Padding(0, unit / 5, unit, 0);
            radio.Checked = check;
            return radio;
        }

        private Button MakeButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(unit * 6, unit * 2);
            button.Padding = new Padding(unit / 2, unit / 5, unit / 2, unit / 5);
            button.Margin = new Padding(unit / 3, 0, 0, 0);
            button.UseVisualStyleBackColor = true;
            return button;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr dc, int index);

        private void OnLoad(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string machine = Sensors.MachineName();
                bool opened = bios.Open();
                BeginInvoke(new Action(delegate
                {
                    machineLabel.Text = ShortName(machine);
                    biosReady = opened;
                    if (!opened)
                    {
                        statusLabel.Text = "HP BIOS fan interface was not found. " + (bios.LastError ?? "");
                    }
                    else if (!IsAdmin())
                    {
                        statusLabel.Text = "Ready. Start will ask for administrator so the BIOS accepts fan changes.";
                    }
                    else
                    {
                        statusLabel.Text = "Ready. Start applies the curve. BIOS auto gives the fans back.";
                    }
                }));
            });
            clock.Start();
            OnTick(this, EventArgs.Empty);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref busy, 1) == 1)
            {
                return;
            }
            TempSource source = CurrentSource();
            FanPoint[] snapshot = CopyPoints();
            bool apply = running;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!closing)
                    {
                        Sample(source, snapshot, apply);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref busy, 0);
                }
            });
        }

        private void Sample(TempSource source, FanPoint[] snapshot, bool apply)
        {
            int? cpu = Sensors.CpuCelsius();
            if (!cpu.HasValue && biosReady)
            {
                int biosTemp;
                int code;
                if (bios.TryGetBiosCelsius(out biosTemp, out code))
                {
                    cpu = biosTemp;
                }
            }
            int? gpu = Sensors.GpuCelsius();
            int? used = FanCurve.PickTemp(source, cpu, gpu);
            int percent = used.HasValue ? FanCurve.PercentAt(snapshot, used.Value) : 0;
            byte level = FanCurve.PercentToLevel(percent);
            string status = null;
            bool failedHard = false;
            byte cpuLevel = 0;
            byte gpuLevel = 0;
            int rpm = 0;
            bool haveLevels = false;
            if (apply && used.HasValue && biosReady)
            {
                int code;
                if (!bios.TrySetFanLevels(level, level, out code))
                {
                    status = HpBios.Describe(code, bios.LastError);
                    failedHard = code == 3 || code == 5 || code == 46;
                }
                else
                {
                    haveLevels = bios.TryGetFanLevels(out cpuLevel, out gpuLevel, out code);
                    int ignored;
                    bios.TryGetFanRpm(0, out rpm, out ignored);
                    status = "Controlling both fans at " + percent.ToString(CultureInfo.InvariantCulture) + "%.";
                }
            }
            else if (!used.HasValue)
            {
                status = "No temperature reading yet.";
            }
            if (biosReady && !haveLevels)
            {
                int readCode;
                haveLevels = bios.TryGetFanLevels(out cpuLevel, out gpuLevel, out readCode);
            }

            if (closing || IsDisposed)
            {
                return;
            }
            BeginInvoke(new Action(delegate
            {
                if (closing || IsDisposed)
                {
                    return;
                }
                cpuLabel.Text = FormatTemp(cpu);
                gpuLabel.Text = FormatTemp(gpu);
                if (used.HasValue)
                {
                    targetLabel.Text = percent.ToString(CultureInfo.InvariantCulture) + "%";
                    curve.SetLive(used.Value, true);
                }
                else
                {
                    targetLabel.Text = "—";
                    curve.SetLive(0, false);
                }
                if (apply && failedHard)
                {
                    running = false;
                    stopButton.Enabled = false;
                    startButton.Enabled = true;
                }
                if (haveLevels)
                {
                    int cpuRpm = cpuLevel * 100;
                    int gpuRpm = gpuLevel * 100;
                    string fans = "Fans about " + cpuRpm.ToString(CultureInfo.InvariantCulture) + " and " + gpuRpm.ToString(CultureInfo.InvariantCulture) + " rpm.";
                    status = string.IsNullOrEmpty(status) ? fans : status + " " + fans;
                }
                else if (rpm > 0)
                {
                    status += " About " + rpm.ToString(CultureInfo.InvariantCulture) + " rpm.";
                }
                if (!string.IsNullOrEmpty(status) && (apply || failedHard || !used.HasValue))
                {
                    statusLabel.Text = status;
                }
                else if (!apply)
                {
                    statusLabel.Text = used.HasValue
                        ? "Watching. The curve would set " + percent.ToString(CultureInfo.InvariantCulture) + "%. Press Start to apply it."
                        : "Waiting for a temperature reading.";
                    if (!string.IsNullOrEmpty(status))
                    {
                        statusLabel.Text = statusLabel.Text + " " + status;
                    }
                }
                string tip = used.HasValue
                    ? "hp-fan-ctrl  " + percent.ToString(CultureInfo.InvariantCulture) + "%"
                    : "hp-fan-ctrl";
                if (tip.Length > 63)
                {
                    tip = tip.Substring(0, 63);
                }
                tray.Text = tip;
            }));
        }

        private void OnStart(object sender, EventArgs e)
        {
            if (!IsAdmin())
            {
                try
                {
                    ProcessStartInfo start = new ProcessStartInfo();
                    start.FileName = Application.ExecutablePath;
                    start.UseShellExecute = true;
                    start.Verb = "runas";
                    Process.Start(start);
                    quit = true;
                    Close();
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "Administrator approval was cancelled. " + ex.Message;
                }
                return;
            }
            if (!biosReady && !bios.Open())
            {
                statusLabel.Text = "HP BIOS fan interface was not found. " + (bios.LastError ?? "");
                return;
            }
            biosReady = true;
            running = true;
            startButton.Enabled = false;
            stopButton.Enabled = true;
            statusLabel.Text = "Applying the curve…";
            OnTick(this, EventArgs.Empty);
        }

        private void OnStop(object sender, EventArgs e)
        {
            running = false;
            startButton.Enabled = true;
            stopButton.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int code;
                bool ok = bios.TrySetFanLevels(0, 0, out code);
                BeginInvoke(new Action(delegate
                {
                    statusLabel.Text = ok
                        ? "Fans returned to BIOS control."
                        : "Could not return the fans to BIOS. " + HpBios.Describe(code, bios.LastError);
                }));
            });
        }

        private void OnReset(object sender, EventArgs e)
        {
            settings.Points = FanCurve.DefaultPoints();
            curve.Points = settings.Points;
            SyncEditors();
            settings.Save();
        }

        private void OnSourceChanged(object sender, EventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            if (radio == null || !radio.Checked)
            {
                return;
            }
            settings.Source = CurrentSource();
            settings.Save();
        }

        private void OnEditorChanged(object sender, EventArgs e)
        {
            if (updatingEditors)
            {
                return;
            }
            NumericUpDown editor = sender as NumericUpDown;
            if (editor == null)
            {
                return;
            }
            int index = (int)editor.Tag;
            FanCurve.SetPercent(settings.Points, index, (int)editor.Value);
            SyncEditors();
            curve.Invalidate();
            settings.Save();
        }

        private void OnCurveChanged(object sender, EventArgs e)
        {
            SyncEditors();
            settings.Save();
        }

        private void SyncEditors()
        {
            updatingEditors = true;
            for (int i = 0; i < editors.Length; i++)
            {
                int percent = settings.Points[i].Percent;
                if (percent < editors[i].Minimum)
                {
                    percent = (int)editors[i].Minimum;
                }
                if (percent > editors[i].Maximum)
                {
                    percent = (int)editors[i].Maximum;
                }
                editors[i].Value = percent;
            }
            updatingEditors = false;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!quit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                try
                {
                    Hide();
                    ShowInTaskbar = false;
                    if (!notedTray)
                    {
                        notedTray = true;
                        tray.BalloonTipTitle = "hp-fan-ctrl";
                        tray.BalloonTipText = "Still running in the tray. Right-click the icon and choose Exit to quit.";
                        tray.ShowBalloonTip(3000);
                    }
                }
                catch
                {
                }
                return;
            }
            closing = true;
            clock.Stop();
            for (int i = 0; i < 40 && busy == 1; i++)
            {
                Thread.Sleep(50);
            }
            if (running)
            {
                int code;
                bios.TrySetFanLevels(0, 0, out code);
            }
            settings.Save();
            bios.Dispose();
            tray.Visible = false;
            tray.Dispose();
        }

        private void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private static Icon MakeTrayIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    g.FillEllipse(brush, 1, 1, 30, 30);
                }
                using (Pen pen = new Pen(Color.White, 2.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 16, 7, 16, 25);
                    g.DrawLine(pen, 7, 16, 25, 16);
                    g.DrawLine(pen, 9, 9, 23, 23);
                    g.DrawLine(pen, 23, 9, 9, 23);
                }
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon raw = Icon.FromHandle(handle))
                    {
                        return (Icon)raw.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private TempSource CurrentSource()
        {
            if (cpuRadio.Checked)
            {
                return TempSource.Cpu;
            }
            if (gpuRadio.Checked)
            {
                return TempSource.Gpu;
            }
            return TempSource.Combined;
        }

        private FanPoint[] CopyPoints()
        {
            FanPoint[] copy = new FanPoint[settings.Points.Length];
            for (int i = 0; i < settings.Points.Length; i++)
            {
                copy[i] = new FanPoint(settings.Points[i].Temp, settings.Points[i].Percent);
            }
            return copy;
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "HP";
            }
            string cleaned = name.Replace("2-in-1 Laptop", "").Replace("2-in-1", "");
            cleaned = cleaned.Trim();
            return cleaned.Length == 0 ? name : cleaned;
        }

        private static string FormatTemp(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) + "°C" : "—";
        }

        private static bool IsAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
