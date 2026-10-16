using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SysInfo
{
    internal sealed class MainForm : Form
    {
        private readonly List<Action<Snapshot>> binders = new List<Action<Snapshot>>();
        private readonly List<Panel> stacks = new List<Panel>();
        private readonly Label hostValue;
        private readonly Label windowsValue;
        private readonly Label versionValue;
        private readonly Label buildValue;
        private readonly Label computerValue;
        private readonly Label status;
        private readonly TabControl tabs;
        private bool built;
        private int layoutDepth;
        private volatile bool closing;
        public volatile int Samples;

        public MainForm()
        {
            Text = "sysinfo";
            Font = new Font("Segoe UI", 9f);
            BackColor = Theme.Surface;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int windowW = work.Width - 64;
            int windowH = work.Height - 64;
            if (windowW > 1500)
            {
                windowW = 1500;
            }
            if (windowH > 980)
            {
                windowH = 980;
            }
            ClientSize = new Size(Math.Max(640, windowW), Math.Max(480, windowH));
            MinimumSize = new Size(Math.Min(760, work.Width), Math.Min(520, work.Height));

            int textH = TextRenderer.MeasureText("Ag", Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
            int row = textH + 16;
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Top;
            header.Height = row * 3 + 16;
            header.Padding = new Padding(12, 8, 12, 4);
            header.ColumnCount = 4;
            header.RowCount = 3;
            header.BackColor = BackColor;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, row));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, row));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, row));
            hostValue = AddField(header, "Hostname", 0, 0, 1);
            windowsValue = AddField(header, "Windows", 2, 0, 1);
            versionValue = AddField(header, "Version", 0, 1, 1);
            buildValue = AddField(header, "Build", 2, 1, 1);
            computerValue = AddField(header, "Computer", 0, 2, 3);
            hostValue.Text = "Reading…";

            status = new Label();
            status.Dock = DockStyle.Bottom;
            status.Height = textH + 14;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Padding = new Padding(12, 0, 8, 0);
            status.ForeColor = Theme.Muted;
            status.BackColor = Theme.Surface;
            status.Text = "Reading sensors…";

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI", 9f);
            tabs.Padding = new Point(12, 4);

            Controls.Add(header);
            Controls.Add(status);
            Controls.Add(tabs);
            tabs.BringToFront();
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception)
            {
            }
            FormClosing += delegate { closing = true; };
            Load += delegate { StartWorker(); };
        }

        public void SaveShots(string directory)
        {
            StringBuilder log = new StringBuilder();
            string[] names = new string[] { "overview", "cpu", "gpu", "memory", "storage", "network" };
            TopMost = true;
            BringToFront();
            for (int i = 0; i < tabs.TabCount && i < names.Length; i++)
            {
                tabs.SelectedIndex = i;
                Application.DoEvents();
                LayoutAll();
                Update();
                Refresh();
                TabPage page = tabs.TabPages[i];
                log.AppendLine(page.Text + " controls=" + page.Controls.Count);
                if (page.Controls.Count > 0 && page.Controls[0] is Panel)
                {
                    Panel scroll = (Panel)page.Controls[0];
                    log.AppendLine("  scroll " + scroll.ClientSize.Width.ToString(CultureInfo.InvariantCulture) + "x" + scroll.ClientSize.Height.ToString(CultureInfo.InvariantCulture));
                    if (scroll.Controls.Count > 0 && scroll.Controls[0] is Panel)
                    {
                        Panel stack = (Panel)scroll.Controls[0];
                        log.AppendLine("  stack " + stack.Width.ToString(CultureInfo.InvariantCulture) + "x" + stack.Height.ToString(CultureInfo.InvariantCulture) + " children=" + stack.Controls.Count.ToString(CultureInfo.InvariantCulture));
                        for (int c = 0; c < stack.Controls.Count; c++)
                        {
                            Control child = stack.Controls[c];
                            log.AppendLine("    " + child.GetType().Name + " " + child.Bounds.ToString() + " " + child.Controls.Count.ToString(CultureInfo.InvariantCulture));
                        }
                        int bmpW = Math.Max(stack.Width, 1);
                        int bmpH = Math.Max(Math.Min(stack.Height, 2400), 1);
                        using (Bitmap bitmap = new Bitmap(bmpW, bmpH))
                        {
                            stack.DrawToBitmap(bitmap, new Rectangle(0, 0, bmpW, bmpH));
                            bitmap.Save(Path.Combine(directory, "shot-" + names[i] + ".png"), ImageFormat.Png);
                        }
                    }
                }
                Rectangle bounds = RectangleToScreen(ClientRectangle);
                using (Bitmap screen = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics graphics = Graphics.FromImage(screen))
                    {
                        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    }
                    screen.Save(Path.Combine(directory, "screen-" + names[i] + ".png"), ImageFormat.Png);
                }
            }
            log.AppendLine(status.Text);
            File.WriteAllText(Path.Combine(directory, "shot-log.txt"), log.ToString());
        }

        private void StartWorker()
        {
            Thread thread = new Thread(delegate()
            {
                Collector collector = new Collector();
                try
                {
                    collector.Open();
                    while (!closing)
                    {
                        Snapshot shot = collector.Sample();
                        if (closing || IsDisposed)
                        {
                            break;
                        }
                        try
                        {
                            BeginInvoke(new Action(delegate()
                            {
                                Apply(shot);
                            }));
                        }
                        catch (InvalidOperationException)
                        {
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        BeginInvoke(new Action(delegate()
                        {
                            status.Text = ex.Message;
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "sysinfo";
            thread.Start();
        }

        private void Apply(Snapshot shot)
        {
            if (IsDisposed)
            {
                return;
            }
            if (!built)
            {
                try
                {
                    Build(shot);
                    built = true;
                    LayoutAll();
                }
                catch (Exception ex)
                {
                    status.Text = ex.Message;
                    return;
                }
            }
            hostValue.Text = shot.Host;
            windowsValue.Text = shot.Windows;
            versionValue.Text = shot.Version;
            buildValue.Text = shot.Build;
            computerValue.Text = shot.Computer;
            for (int i = 0; i < binders.Count; i++)
            {
                binders[i](shot);
            }
            status.Text = "Updated " + shot.TakenUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            Samples++;
        }

        private void Build(Snapshot shot)
        {
            tabs.TabPages.Clear();
            stacks.Clear();
            AddPage("Overview", delegate(Panel stack)
            {
                AddCpu(stack, shot);
                AddGpus(stack, shot);
                AddMemory(stack, shot);
                AddDisks(stack, shot);
                AddNics(stack, shot);
            });
            AddPage("CPU", delegate(Panel stack) { AddCpu(stack, shot); });
            AddPage("GPU", delegate(Panel stack) { AddGpus(stack, shot); });
            AddPage("Memory", delegate(Panel stack) { AddMemory(stack, shot); });
            AddPage("Storage", delegate(Panel stack) { AddDisks(stack, shot); });
            AddPage("Network", delegate(Panel stack) { AddNics(stack, shot); });
        }

        private void AddPage(string name, Action<Panel> fill)
        {
            TabPage page = new TabPage(name);
            page.UseVisualStyleBackColor = false;
            page.BackColor = BackColor;
            Panel scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.BackColor = BackColor;
            Panel stack = new Panel();
            stack.BackColor = BackColor;
            scroll.Controls.Add(stack);
            stacks.Add(stack);
            Panel stackRef = stack;
            scroll.Resize += delegate { LayoutStack(scroll, stackRef); };
            page.Controls.Add(scroll);
            tabs.TabPages.Add(page);
            try
            {
                fill(stack);
            }
            catch (Exception ex)
            {
                Label error = new Label();
                error.Text = ex.Message;
                error.AutoSize = true;
                stack.Controls.Add(error);
            }
        }

        private void LayoutAll()
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                Panel stack = stacks[i];
                if (stack.Parent is Panel)
                {
                    LayoutStack((Panel)stack.Parent, stack);
                }
            }
        }

        private void LayoutStack(Panel scroll, Panel stack)
        {
            if (layoutDepth > 0)
            {
                return;
            }
            layoutDepth++;
            try
            {
                int width = scroll.ClientSize.Width;
                if (width < 80)
                {
                    return;
                }
                PlaceStack(scroll, stack, width);
                if (stack.Width > scroll.ClientSize.Width && scroll.ClientSize.Width > 80)
                {
                    PlaceStack(scroll, stack, scroll.ClientSize.Width);
                }
            }
            finally
            {
                layoutDepth--;
            }
        }

        private static void PlaceStack(Panel scroll, Panel stack, int width)
        {
            int gap = 12;
            int margin = 12;
            int content = width - margin * 2;
            bool two = content >= 880;
            int colW = two ? (content - gap) / 2 : content;
            int leftY = margin;
            int rightY = margin;
            for (int i = 0; i < stack.Controls.Count; i++)
            {
                Control child = stack.Controls[i];
                SectionPanel section = child as SectionPanel;
                bool pair = two && section != null && !section.Wide;
                int cell = pair ? colW : content;
                child.Width = cell;
                child.PerformLayout();
                int h = child.Height;
                if (pair)
                {
                    bool left = leftY <= rightY;
                    int x = left ? margin : margin + colW + gap;
                    int y = left ? leftY : rightY;
                    child.SetBounds(x, y, cell, h);
                    if (left)
                    {
                        leftY = y + h + gap;
                    }
                    else
                    {
                        rightY = y + h + gap;
                    }
                }
                else
                {
                    int y = Math.Max(leftY, rightY);
                    child.SetBounds(margin, y, cell, h);
                    leftY = y + h + gap;
                    rightY = leftY;
                }
            }
            int bottom = Math.Max(leftY, rightY) + 8;
            stack.Size = new Size(width, Math.Max(bottom, scroll.ClientSize.Height));
        }

        private void AddCpu(Panel stack, Snapshot shot)
        {
            SectionPanel section = new SectionPanel("CPU", Theme.Secondary);
            List<string> captions = new List<string>();
            List<Func<Snapshot, string>> reads = new List<Func<Snapshot, string>>();
            captions.Add("Name");
            reads.Add(delegate(Snapshot s) { return s.CpuName; });
            captions.Add("Cores");
            reads.Add(delegate(Snapshot s) { return s.CpuCores.ToString(CultureInfo.InvariantCulture); });
            captions.Add("Threads");
            reads.Add(delegate(Snapshot s) { return s.CpuThreads.ToString(CultureInfo.InvariantCulture); });
            captions.Add("Max clock");
            reads.Add(delegate(Snapshot s) { return s.CpuMaxMhz.ToString(CultureInfo.InvariantCulture) + " MHz"; });
            captions.Add("L2 cache");
            reads.Add(delegate(Snapshot s) { return s.L2Kb > 0 ? Fmt.CacheMb(s.L2Kb) : "n/a"; });
            captions.Add("L3 cache");
            reads.Add(delegate(Snapshot s) { return s.L3Kb > 0 ? Fmt.CacheMb(s.L3Kb) : "n/a"; });
            AddSpecs(section, captions, reads);

            TileGrid package = new TileGrid();
            AddTile(package, delegate(Snapshot s)
            {
                return Face("", s.HasPackageTemp ? Fmt.Temp(s.PackageC) : "n/a", "Temp", s.PackageC, s.HasPackageTemp);
            });
            AddTile(package, delegate(Snapshot s)
            {
                return Face("", s.HasCpuLoad ? Fmt.Percent(s.CpuLoad) : "n/a", "Load", s.CpuLoad, s.HasCpuLoad);
            });
            AddTile(package, delegate(Snapshot s)
            {
                float max = 0f;
                for (int i = 0; i < s.Cores.Length; i++)
                {
                    if (s.Cores[i].Mhz > max)
                    {
                        max = s.Cores[i].Mhz;
                    }
                }
                return Face("", max > 0f ? Fmt.Ghz(max) : "n/a", "GHz", max, max > 0f);
            });
            AddTile(package, delegate(Snapshot s)
            {
                return Face("", "n/a", "Voltage", 0f, false);
            });
            section.Controls.Add(package);
            section.Controls.Add(Hint("Logical processors"));
            TileGrid cores = new TileGrid();
            for (int i = 0; i < shot.Cores.Length; i++)
            {
                int id = shot.Cores[i].Id;
                AddTile(cores, delegate(Snapshot s)
                {
                    CoreReading core = null;
                    for (int n = 0; n < s.Cores.Length; n++)
                    {
                        if (s.Cores[n].Id == id)
                        {
                            core = s.Cores[n];
                            break;
                        }
                    }
                    if (core == null)
                    {
                        return Face(id.ToString(CultureInfo.InvariantCulture), "n/a", "GHz", "", 0f, false);
                    }
                    string value = core.Mhz > 0f ? Fmt.Ghz(core.Mhz) : "park";
                    return Face(id.ToString(CultureInfo.InvariantCulture), value, "GHz", Fmt.Percent(core.Load), core.Mhz, core.Mhz > 0f);
                });
            }
            section.Controls.Add(cores);
            stack.Controls.Add(section);
        }

        private void AddGpus(Panel stack, Snapshot shot)
        {
            for (int g = 0; g < shot.Gpus.Length; g++)
            {
                int index = g;
                GpuReading gpu = shot.Gpus[g];
                Color accent = gpu.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Theme.Secondary
                    : Theme.Success;
                SectionPanel section = new SectionPanel(gpu.Name.Length > 0 ? gpu.Name : "GPU", accent);
                List<string> captions = new List<string>();
                List<Func<Snapshot, string>> reads = new List<Func<Snapshot, string>>();
                AddRow(captions, reads, "Name", delegate(Snapshot s) { return GpuAt(s, index).Name; }, gpu.Name);
                AddRow(captions, reads, "Device ID", delegate(Snapshot s) { return GpuAt(s, index).DeviceId; }, gpu.DeviceId);
                AddRow(captions, reads, "Chip", delegate(Snapshot s) { return GpuAt(s, index).Chip; }, gpu.Chip);
                AddRow(captions, reads, "Process", delegate(Snapshot s) { return GpuAt(s, index).Process; }, gpu.Process);
                AddRow(captions, reads, "Shaders", delegate(Snapshot s) { return GpuAt(s, index).Shaders; }, gpu.Shaders);
                AddRow(captions, reads, "ROPs / TMUs", delegate(Snapshot s) { return GpuAt(s, index).RopsTmus; }, gpu.RopsTmus);
                AddRow(captions, reads, "BIOS", delegate(Snapshot s) { return GpuAt(s, index).Bios; }, gpu.Bios);
                AddRow(captions, reads, "Driver", delegate(Snapshot s) { return GpuAt(s, index).Driver; }, gpu.Driver);
                AddRow(captions, reads, "Driver date", delegate(Snapshot s) { return GpuAt(s, index).DriverDate; }, gpu.DriverDate);
                AddRow(captions, reads, "Memory", delegate(Snapshot s)
                {
                    GpuReading item = GpuAt(s, index);
                    if (item.MemoryTotalMb <= 0)
                    {
                        return "";
                    }
                    if (item.MemoryUsedMb > 0 || item.Live)
                    {
                        return item.MemoryUsedMb.ToString(CultureInfo.InvariantCulture) + " / " + item.MemoryTotalMb.ToString(CultureInfo.InvariantCulture) + " MB";
                    }
                    return item.MemoryTotalMb.ToString(CultureInfo.InvariantCulture) + " MB";
                }, gpu.MemoryTotalMb > 0 ? "x" : "");
                AddRow(captions, reads, "Memory type", delegate(Snapshot s) { return GpuAt(s, index).MemoryKind; }, gpu.MemoryKind);
                AddRow(captions, reads, "Bus width", delegate(Snapshot s) { return GpuAt(s, index).Bus; }, gpu.Bus);
                AddRow(captions, reads, "PCIe", delegate(Snapshot s) { return GpuAt(s, index).Pcie; }, gpu.Pcie.Length > 0 ? gpu.Pcie : (gpu.Live ? "…" : ""));
                AddRow(captions, reads, "Boost", delegate(Snapshot s)
                {
                    GpuReading item = GpuAt(s, index);
                    return item.MaxCoreMhz > 0 ? item.MaxCoreMhz.ToString(CultureInfo.InvariantCulture) + " MHz" : "";
                }, gpu.MaxCoreMhz > 0 ? "x" : (gpu.Live ? "…" : ""));
                AddRow(captions, reads, "Memory clock", delegate(Snapshot s)
                {
                    GpuReading item = GpuAt(s, index);
                    return item.HasMemMhz ? item.MemMhz.ToString("0", CultureInfo.InvariantCulture) + " MHz" : "";
                }, gpu.HasMemMhz ? "x" : (gpu.Live ? "…" : ""));
                AddRow(captions, reads, "P-State", delegate(Snapshot s) { return GpuAt(s, index).PState; }, gpu.PState.Length > 0 ? gpu.PState : (gpu.Live ? "…" : ""));
                AddSpecs(section, captions, reads);
                if (gpu.Live)
                {
                    TileGrid tiles = new TileGrid();
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        GpuReading item = GpuAt(s, index);
                        return Face("", item.HasTemp ? Fmt.Temp(item.TempC) : "n/a", "Temp", item.TempC, item.HasTemp);
                    });
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        GpuReading item = GpuAt(s, index);
                        return Face("", item.HasCoreMhz ? Fmt.Ghz(item.CoreMhz) : "n/a", "GHz", item.CoreMhz, item.HasCoreMhz);
                    });
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        GpuReading item = GpuAt(s, index);
                        return Face("", item.HasLoad ? Fmt.Percent(item.Load) : "n/a", "Load", item.Load, item.HasLoad);
                    });
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        return Face("", "n/a", "Voltage", 0f, false);
                    });
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        GpuReading item = GpuAt(s, index);
                        return Face("", item.HasWatts ? Fmt.Watts(item.Watts) : "n/a", "Watt", item.Watts, item.HasWatts);
                    });
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        GpuReading item = GpuAt(s, index);
                        float bytes = item.RxKBps * 1024f;
                        return Face("", item.HasRx ? Fmt.Rate(bytes) : "n/a", item.HasRx ? "Rx " + Fmt.RateUnit(bytes) : "Rx", bytes, item.HasRx);
                    });
                    AddTile(tiles, delegate(Snapshot s)
                    {
                        GpuReading item = GpuAt(s, index);
                        float bytes = item.TxKBps * 1024f;
                        return Face("", item.HasTx ? Fmt.Rate(bytes) : "n/a", item.HasTx ? "Tx " + Fmt.RateUnit(bytes) : "Tx", bytes, item.HasTx);
                    });
                    section.Controls.Add(tiles);
                }
                stack.Controls.Add(section);
            }
        }

        private void AddMemory(Panel stack, Snapshot shot)
        {
            SectionPanel section = new SectionPanel("Memory", Theme.Warning);
            section.Wide = false;
            List<string> captions = new List<string>();
            List<Func<Snapshot, string>> reads = new List<Func<Snapshot, string>>();
            captions.Add("Installed");
            reads.Add(delegate(Snapshot s) { return Fmt.WinGb(s.MemoryInstalled); });
            captions.Add("Usable");
            reads.Add(delegate(Snapshot s) { return Fmt.WinGb(s.MemoryUsable); });
            captions.Add("Modules");
            reads.Add(delegate(Snapshot s) { return s.MemorySummary; });
            captions.Add("Speed");
            reads.Add(delegate(Snapshot s) { return s.MemorySpeed.Length > 0 ? s.MemorySpeed : "n/a"; });
            AddSpecs(section, captions, reads);
            TileGrid tiles = new TileGrid();
            AddTile(tiles, delegate(Snapshot s)
            {
                ulong used = UsedBytes(s);
                return Face("", Fmt.GbNumber(used), "GB used", (float)used, s.HasMemory);
            });
            AddTile(tiles, delegate(Snapshot s)
            {
                ulong free = FreeBytes(s);
                return Face("", Fmt.GbNumber(free), "GB free", (float)free, s.HasMemory);
            });
            AddTile(tiles, delegate(Snapshot s)
            {
                float load = MemoryLoad(s);
                return Face("", s.HasMemory ? Fmt.Percent(load) : "n/a", "Load", load, s.HasMemory);
            });
            AddTile(tiles, delegate(Snapshot s)
            {
                return Face("", s.MemorySpeed.Length > 0 ? s.MemorySpeed.Replace(" MT/s", "") : "n/a", "MT/s", 0f, false);
            });
            section.Controls.Add(tiles);
            stack.Controls.Add(section);
        }

        private void AddDisks(Panel stack, Snapshot shot)
        {
            for (int d = 0; d < shot.Disks.Length; d++)
            {
                int index = d;
                DiskReading disk = shot.Disks[d];
                SectionPanel section = new SectionPanel(shot.Disks.Length == 1 ? "Storage" : disk.Model, Theme.Storage);
                section.Wide = false;
                List<string> captions = new List<string>();
                List<Func<Snapshot, string>> reads = new List<Func<Snapshot, string>>();
                captions.Add("Model");
                reads.Add(delegate(Snapshot s) { return DiskAt(s, index).Model; });
                captions.Add("Bus");
                reads.Add(delegate(Snapshot s) { return DiskAt(s, index).Bus; });
                captions.Add("Firmware");
                reads.Add(delegate(Snapshot s) { return DiskAt(s, index).Firmware; });
                captions.Add("Size");
                reads.Add(delegate(Snapshot s) { return Fmt.WinGb(DiskAt(s, index).SizeBytes); });
                captions.Add("Volumes");
                reads.Add(delegate(Snapshot s) { return DiskAt(s, index).Volumes; });
                AddSpecs(section, captions, reads);
                TileGrid tiles = new TileGrid();
                AddTile(tiles, delegate(Snapshot s)
                {
                    DiskReading item = DiskAt(s, index);
                    ulong used = item.VolumeSize > item.VolumeFree ? item.VolumeSize - item.VolumeFree : 0;
                    return Face("", item.VolumeSize > 0 ? Fmt.GbNumber(used) : "n/a", "GB used", (float)used, item.VolumeSize > 0);
                });
                AddTile(tiles, delegate(Snapshot s)
                {
                    DiskReading item = DiskAt(s, index);
                    return Face("", item.VolumeSize > 0 ? Fmt.GbNumber(item.VolumeFree) : "n/a", "GB free", (float)item.VolumeFree, item.VolumeSize > 0);
                });
                AddTile(tiles, delegate(Snapshot s)
                {
                    DiskReading item = DiskAt(s, index);
                    return Face("", item.HasIo ? Fmt.Rate(item.ReadBps) : "n/a", item.HasIo ? "Read " + Fmt.RateUnit(item.ReadBps) : "Read", item.ReadBps, item.HasIo);
                });
                AddTile(tiles, delegate(Snapshot s)
                {
                    DiskReading item = DiskAt(s, index);
                    return Face("", item.HasIo ? Fmt.Rate(item.WriteBps) : "n/a", item.HasIo ? "Write " + Fmt.RateUnit(item.WriteBps) : "Write", item.WriteBps, item.HasIo);
                });
                AddTile(tiles, delegate(Snapshot s)
                {
                    DiskReading item = DiskAt(s, index);
                    return Face("", item.HasIo ? Fmt.Percent(item.Busy) : "n/a", "Busy", item.Busy, item.HasIo);
                });
                section.Controls.Add(tiles);
                stack.Controls.Add(section);
            }
        }

        private void AddNics(Panel stack, Snapshot shot)
        {
            if (shot.Nics.Length == 0)
            {
                SectionPanel empty = new SectionPanel("Network", Theme.Secondary);
                empty.Wide = false;
                empty.Controls.Add(Hint("No active adapter reported a traffic counter."));
                stack.Controls.Add(empty);
                return;
            }
            for (int n = 0; n < shot.Nics.Length; n++)
            {
                int index = n;
                NicReading nic = shot.Nics[n];
                string title = shot.Nics.Length == 1 ? "Network" : nic.Name;
                SectionPanel section = new SectionPanel(title, Theme.Secondary);
                section.Wide = false;
                List<string> captions = new List<string>();
                List<Func<Snapshot, string>> reads = new List<Func<Snapshot, string>>();
                captions.Add("Adapter");
                reads.Add(delegate(Snapshot s) { return NicAt(s, index).Name; });
                captions.Add("MAC");
                reads.Add(delegate(Snapshot s) { return NicAt(s, index).Mac; });
                captions.Add("Link");
                reads.Add(delegate(Snapshot s) { return Fmt.Link(NicAt(s, index).LinkBps); });
                AddSpecs(section, captions, reads);
                TileGrid tiles = new TileGrid();
                AddTile(tiles, delegate(Snapshot s)
                {
                    NicReading item = NicAt(s, index);
                    return Face("", item.HasIo ? Fmt.Rate(item.RxBps) : "n/a", item.HasIo ? "Rx " + Fmt.RateUnit(item.RxBps) : "Rx", item.RxBps, item.HasIo);
                });
                AddTile(tiles, delegate(Snapshot s)
                {
                    NicReading item = NicAt(s, index);
                    return Face("", item.HasIo ? Fmt.Rate(item.TxBps) : "n/a", item.HasIo ? "Tx " + Fmt.RateUnit(item.TxBps) : "Tx", item.TxBps, item.HasIo);
                });
                AddTile(tiles, delegate(Snapshot s)
                {
                    NicReading item = NicAt(s, index);
                    if (item.LinkBps <= 0)
                    {
                        return Face("", "n/a", "Link", 0f, false);
                    }
                    return Face("", Fmt.LinkNumber(item.LinkBps), Fmt.LinkUnit(item.LinkBps), 0f, false);
                });
                section.Controls.Add(tiles);
                stack.Controls.Add(section);
            }
        }

        private void AddSpecs(SectionPanel section, List<string> captions, List<Func<Snapshot, string>> reads)
        {
            SpecGrid grid = new SpecGrid(captions.ToArray());
            section.Controls.Add(grid);
            for (int i = 0; i < reads.Count; i++)
            {
                int slot = i;
                Func<Snapshot, string> read = reads[i];
                binders.Add(delegate(Snapshot s)
                {
                    grid.SetValue(slot, read(s));
                });
            }
        }

        private static void AddRow(List<string> captions, List<Func<Snapshot, string>> reads, string caption, Func<Snapshot, string> read, string initial)
        {
            if (initial == null || initial.Length == 0)
            {
                return;
            }
            captions.Add(caption);
            reads.Add(read);
        }

        private void AddTile(TileGrid grid, Func<Snapshot, TileFace> read)
        {
            SensorTile tile = new SensorTile();
            grid.Controls.Add(tile);
            binders.Add(delegate(Snapshot s)
            {
                TileFace face = read(s);
                tile.SetReading(face.Badge, face.Value, face.Caption, face.Side, face.Sample, face.HasSample);
            });
        }

        private static Label Hint(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = Theme.Muted;
            label.BackColor = Theme.Card;
            label.Font = new Font("Segoe UI", 8f);
            return label;
        }

        private static Label AddField(TableLayoutPanel header, string caption, int column, int row, int span)
        {
            Label name = new Label();
            name.Text = caption;
            name.TextAlign = ContentAlignment.MiddleRight;
            name.AutoSize = true;
            name.Dock = DockStyle.Fill;
            name.ForeColor = Theme.Muted;
            name.Margin = new Padding(0, 6, 6, 4);
            header.Controls.Add(name, column, row);

            Panel box = new Panel();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.White;
            box.Margin = new Padding(0, 4, 12, 4);
            box.Dock = DockStyle.Fill;
            FitLabel value = new FitLabel();
            value.Dock = DockStyle.Fill;
            value.BackColor = Color.White;
            box.Controls.Add(value);
            header.Controls.Add(box, column + 1, row);
            if (span > 1)
            {
                header.SetColumnSpan(box, span);
            }
            return value;
        }

        private static TileFace Face(string badge, string value, string caption, float sample, bool hasSample)
        {
            return Face(badge, value, caption, "", sample, hasSample);
        }

        private static TileFace Face(string badge, string value, string caption, string side, float sample, bool hasSample)
        {
            TileFace face = new TileFace();
            face.Badge = badge;
            face.Value = value;
            face.Caption = caption;
            face.Side = side ?? "";
            face.Sample = sample;
            face.HasSample = hasSample;
            return face;
        }

        private static GpuReading GpuAt(Snapshot shot, int index)
        {
            if (index >= 0 && index < shot.Gpus.Length)
            {
                return shot.Gpus[index];
            }
            return new GpuReading();
        }

        private static DiskReading DiskAt(Snapshot shot, int index)
        {
            if (index >= 0 && index < shot.Disks.Length)
            {
                return shot.Disks[index];
            }
            return new DiskReading();
        }

        private static NicReading NicAt(Snapshot shot, int index)
        {
            if (index >= 0 && index < shot.Nics.Length)
            {
                return shot.Nics[index];
            }
            return new NicReading();
        }

        private static ulong UsedBytes(Snapshot shot)
        {
            if (!shot.HasMemory || shot.MemoryUsable == 0)
            {
                return 0;
            }
            ulong free = FreeBytes(shot);
            if (free >= shot.MemoryUsable)
            {
                return 0;
            }
            return shot.MemoryUsable - free;
        }

        private static ulong FreeBytes(Snapshot shot)
        {
            if (!shot.HasMemory)
            {
                return 0;
            }
            double bytes = shot.MemoryAvailableMb * 1024d * 1024d;
            if (bytes < 0d)
            {
                return 0;
            }
            return (ulong)bytes;
        }

        private static float MemoryLoad(Snapshot shot)
        {
            if (!shot.HasMemory || shot.MemoryUsable == 0)
            {
                return 0f;
            }
            return (float)(UsedBytes(shot) * 100d / shot.MemoryUsable);
        }

        private struct TileFace
        {
            public string Badge;
            public string Value;
            public string Caption;
            public string Side;
            public float Sample;
            public bool HasSample;
        }
    }
}
