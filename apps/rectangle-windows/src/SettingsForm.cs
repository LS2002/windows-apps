using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RectangleWindows
{
    internal sealed class SettingsForm : Form
    {
        private readonly TrayApp host;
        private readonly AppSettings settings;
        private readonly Dictionary<SettingAction, ShortcutRecorder> recorders;
        private readonly TabControl tabs;
        private readonly Panel shortcutsHost;
        private readonly CheckBox snapCheck;
        private readonly CheckBox startupCheck;
        private readonly Label conflictLabel;
        private bool suspendEvents;

        public SettingsForm(TrayApp host, AppSettings settings)
        {
            this.host = host;
            this.settings = settings;
            recorders = new Dictionary<SettingAction, ShortcutRecorder>();

            Text = "Shortcuts";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(500, 540);
            BackColor = Color.FromArgb(246, 246, 246);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            KeyPreview = true;

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(18, 6);
            tabs.SelectedIndexChanged += OnTabChanged;

            TabPage shortcutsTab = new TabPage("Shortcuts");
            TabPage snapTab = new TabPage("Snap Areas");
            TabPage behaviorTab = new TabPage("Behavior");
            TabPage appTab = new TabPage("App Settings");
            shortcutsTab.BackColor = BackColor;
            snapTab.BackColor = BackColor;
            behaviorTab.BackColor = BackColor;
            appTab.BackColor = BackColor;

            shortcutsHost = new Panel();
            shortcutsHost.Dock = DockStyle.Fill;
            shortcutsHost.AutoScroll = true;
            shortcutsHost.Padding = new Padding(0, 8, 0, 8);
            BuildShortcutRows(shortcutsHost);
            shortcutsTab.Controls.Add(shortcutsHost);

            Panel shortcutFooter = new Panel();
            shortcutFooter.Dock = DockStyle.Bottom;
            shortcutFooter.Height = 48;
            shortcutFooter.BackColor = BackColor;
            Button restore = MakeButton("Restore Defaults");
            restore.Location = new Point(16, 10);
            restore.Click += delegate { RestoreDefaults(); };
            conflictLabel = new Label();
            conflictLabel.AutoSize = false;
            conflictLabel.Location = new Point(150, 14);
            conflictLabel.Size = new Size(330, 28);
            conflictLabel.ForeColor = Color.FromArgb(170, 60, 40);
            conflictLabel.TextAlign = ContentAlignment.MiddleLeft;
            shortcutFooter.Controls.Add(restore);
            shortcutFooter.Controls.Add(conflictLabel);
            shortcutsTab.Controls.Add(shortcutFooter);

            snapCheck = new CheckBox();
            snapCheck.Text = "Snap windows by dragging";
            snapCheck.AutoSize = true;
            snapCheck.Location = new Point(28, 28);
            snapCheck.Checked = settings.SnapWhileDragging;
            snapCheck.CheckedChanged += delegate
            {
                if (suspendEvents)
                {
                    return;
                }
                settings.SnapWhileDragging = snapCheck.Checked;
                Persist();
            };
            Label snapHint = MakeHint("Drag a window to a screen edge. A footprint shows where it will land, like Rectangle on macOS.", 28, 60, 440);
            snapTab.Controls.Add(snapCheck);
            snapTab.Controls.Add(snapHint);

            Label behaviorHint = MakeHint("Repeating a window shortcut rotates that size on the screen: left, then right, then left again. Center Half flips between a centered column and a centered row.", 28, 28, 440);
            behaviorTab.Controls.Add(behaviorHint);

            startupCheck = new CheckBox();
            startupCheck.Text = "Launch on login";
            startupCheck.AutoSize = true;
            startupCheck.Location = new Point(28, 28);
            startupCheck.Checked = settings.RunAtStartup;
            startupCheck.CheckedChanged += delegate
            {
                if (suspendEvents)
                {
                    return;
                }
                settings.RunAtStartup = startupCheck.Checked;
                host.SetStartupEnabled(startupCheck.Checked);
                Persist();
            };
            Label appHint = MakeHint("Settings are saved to %AppData%\\RectangleWindows\\config.json.", 28, 64, 440);
            Button openConfig = MakeButton("Reveal Config File");
            openConfig.Location = new Point(28, 120);
            openConfig.Click += delegate { host.RevealConfig(); };
            Button restartAdmin = MakeButton("Restart as Administrator");
            restartAdmin.Location = new Point(170, 120);
            restartAdmin.Width = 180;
            restartAdmin.Click += delegate { host.RestartElevated(); };
            appTab.Controls.Add(startupCheck);
            appTab.Controls.Add(appHint);
            appTab.Controls.Add(openConfig);
            appTab.Controls.Add(restartAdmin);

            tabs.TabPages.Add(shortcutsTab);
            tabs.TabPages.Add(snapTab);
            tabs.TabPages.Add(behaviorTab);
            tabs.TabPages.Add(appTab);
            Controls.Add(tabs);
            RefreshConflicts();
        }

        private void BuildShortcutRows(Panel hostPanel)
        {
            int y = 12;
            string lastSection = null;
            for (int i = 0; i < AppSettings.Catalog.Length; i++)
            {
                ActionDefinition definition = AppSettings.Catalog[i];
                if (definition.Section != lastSection)
                {
                    if (lastSection != null)
                    {
                        y += 10;
                    }
                    Label section = new Label();
                    section.Text = definition.Section.ToUpperInvariant();
                    section.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
                    section.ForeColor = Color.FromArgb(120, 120, 120);
                    section.Location = new Point(28, y);
                    section.AutoSize = true;
                    hostPanel.Controls.Add(section);
                    y += 26;
                    lastSection = definition.Section;
                }

                Panel row = new Panel();
                row.Location = new Point(0, y);
                row.Size = new Size(480, 34);
                row.BackColor = Color.Transparent;

                Label title = new Label();
                title.Text = definition.Title;
                title.TextAlign = ContentAlignment.MiddleRight;
                title.Location = new Point(20, 4);
                title.Size = new Size(220, 24);
                title.ForeColor = Color.FromArgb(30, 30, 30);

                PictureBox icon = new PictureBox();
                icon.Location = new Point(248, 8);
                icon.Size = new Size(22, 16);
                icon.SizeMode = PictureBoxSizeMode.CenterImage;
                icon.Image = MakeActionIcon(definition.Action);

                ShortcutRecorder recorder = new ShortcutRecorder();
                recorder.Location = new Point(280, 5);
                recorder.Size = new Size(160, 24);
                recorder.Value = settings.GetShortcut(definition.Action);
                SettingAction action = definition.Action;
                recorder.RecordingStarted += delegate { host.SuspendHotkeys(); };
                recorder.RecordingEnded += delegate { host.ResumeHotkeys(); };
                recorder.ValueChanged += delegate
                {
                    settings.SetShortcut(action, recorder.Value);
                    Persist();
                    RefreshConflicts();
                };
                recorders[action] = recorder;

                row.Controls.Add(title);
                row.Controls.Add(icon);
                row.Controls.Add(recorder);
                hostPanel.Controls.Add(row);
                y += 34;
            }

            Label tip = MakeHint("Click a shortcut field and press the new key combination. Backspace clears it. Esc cancels.", 28, y + 12, 430);
            hostPanel.Controls.Add(tip);
        }

        private void RestoreDefaults()
        {
            settings.RestoreDefaults();
            suspendEvents = true;
            snapCheck.Checked = settings.SnapWhileDragging;
            suspendEvents = false;
            for (int i = 0; i < AppSettings.Catalog.Length; i++)
            {
                ActionDefinition definition = AppSettings.Catalog[i];
                ShortcutRecorder recorder;
                if (recorders.TryGetValue(definition.Action, out recorder))
                {
                    recorder.Value = settings.GetShortcut(definition.Action);
                }
            }
            Persist();
            RefreshConflicts();
        }

        private void Persist()
        {
            settings.Save();
            host.ApplySettings(settings);
        }

        private void RefreshConflicts()
        {
            List<string> conflicts = host.ConflictLabels(settings);
            if (conflicts.Count == 0)
            {
                conflictLabel.Text = "";
            }
            else
            {
                conflictLabel.Text = "In use: " + string.Join("; ", conflicts.ToArray());
            }
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (tabs.SelectedTab != null)
            {
                Text = tabs.SelectedTab.Text;
            }
        }

        private static Label MakeHint(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 72);
            label.ForeColor = Color.FromArgb(110, 110, 110);
            return label;
        }

        private static Button MakeButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Size = new Size(130, 28);
            button.FlatStyle = FlatStyle.System;
            return button;
        }

        private static Image MakeActionIcon(SettingAction action)
        {
            Bitmap bitmap = new Bitmap(22, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Pen pen = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                using (Brush fill = new SolidBrush(Color.FromArgb(47, 132, 229)))
                {
                    g.DrawRectangle(pen, 1, 1, 19, 13);
                    switch (action)
                    {
                        case SettingAction.Maximize:
                            g.FillRectangle(fill, 2, 2, 18, 12);
                            break;
                        case SettingAction.CenterHalf:
                            g.FillRectangle(fill, 6, 2, 9, 12);
                            break;
                        case SettingAction.Half:
                            g.FillRectangle(fill, 2, 2, 9, 12);
                            break;
                        case SettingAction.Third:
                            g.FillRectangle(fill, 2, 2, 6, 12);
                            break;
                        case SettingAction.TwoThirds:
                            g.FillRectangle(fill, 2, 2, 12, 12);
                            break;
                        case SettingAction.ShotClipboard:
                            g.FillRectangle(fill, 5, 4, 11, 8);
                            g.DrawRectangle(pen, 4, 3, 13, 10);
                            break;
                        case SettingAction.ShotDesktop:
                            g.FillRectangle(fill, 4, 3, 13, 9);
                            g.DrawLine(pen, 8, 13, 13, 13);
                            break;
                    }
                }
            }
            return bitmap;
        }
    }
}
