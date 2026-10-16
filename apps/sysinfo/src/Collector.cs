using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace SysInfo
{
    internal sealed class Collector
    {
        private string host = "";
        private string windows = "";
        private string versionName = "";
        private string build = "";
        private string computer = "";
        private string cpuName = "";
        private int cpuCores;
        private int cpuThreads;
        private int cpuMaxMhz;
        private int l2Kb;
        private int l3Kb;
        private ulong memoryInstalled;
        private ulong memoryUsable;
        private string memorySummary = "";
        private string memorySpeed = "";
        private GpuReading[] gpus = new GpuReading[0];
        private DiskReading[] disks = new DiskReading[0];
        private NicReading[] nics = new NicReading[0];
        private string nvidiaSmi = "";

        private PerformanceCounter cpuLoad;
        private PerformanceCounter[] coreMhz = new PerformanceCounter[0];
        private PerformanceCounter[] coreLoad = new PerformanceCounter[0];
        private int[] coreIds = new int[0];
        private PerformanceCounter[] thermals = new PerformanceCounter[0];
        private PerformanceCounter memoryFree;
        private DiskProbe[] diskProbes = new DiskProbe[0];
        private NicProbe[] nicProbes = new NicProbe[0];

        public void Open()
        {
            ReadOs();
            ReadComputer();
            ReadCpu();
            ReadMemory();
            ReadGpus();
            ReadDisks();
            ReadNics();
            OpenCounters();
            Prime();
            Thread.Sleep(350);
        }

        public Snapshot Sample()
        {
            Snapshot shot = new Snapshot();
            shot.Host = host;
            shot.Windows = windows;
            shot.Version = versionName;
            shot.Build = build;
            shot.Computer = computer;
            shot.CpuName = cpuName;
            shot.CpuCores = cpuCores;
            shot.CpuThreads = cpuThreads;
            shot.CpuMaxMhz = cpuMaxMhz;
            shot.L2Kb = l2Kb;
            shot.L3Kb = l3Kb;
            shot.MemoryInstalled = memoryInstalled;
            shot.MemoryUsable = memoryUsable;
            shot.MemorySummary = memorySummary;
            shot.MemorySpeed = memorySpeed;
            shot.TakenUtc = DateTime.UtcNow;
            shot.Note = "Per-core temperature and voltage are not exposed by Windows.";

            if (cpuLoad != null)
            {
                shot.HasCpuLoad = true;
                shot.CpuLoad = ClampPercent(cpuLoad.NextValue());
            }

            shot.Cores = new CoreReading[coreIds.Length];
            for (int i = 0; i < coreIds.Length; i++)
            {
                CoreReading core = new CoreReading();
                core.Id = coreIds[i];
                if (coreMhz[i] != null)
                {
                    core.Mhz = coreMhz[i].NextValue();
                }
                if (coreLoad[i] != null)
                {
                    core.Load = ClampPercent(coreLoad[i].NextValue());
                }
                shot.Cores[i] = core;
            }

            float hottest = float.NaN;
            for (int i = 0; i < thermals.Length; i++)
            {
                if (thermals[i] == null)
                {
                    continue;
                }
                float raw = thermals[i].NextValue();
                float celsius = raw - 273.15f;
                if (raw > 1000f)
                {
                    celsius = raw / 10f - 273.15f;
                }
                if (celsius < 10f || celsius > 120f)
                {
                    continue;
                }
                if (float.IsNaN(hottest) || celsius > hottest)
                {
                    hottest = celsius;
                }
            }
            if (!float.IsNaN(hottest))
            {
                shot.HasPackageTemp = true;
                shot.PackageC = hottest;
            }

            if (memoryFree != null)
            {
                shot.HasMemory = true;
                shot.MemoryAvailableMb = memoryFree.NextValue();
            }

            shot.Gpus = new GpuReading[gpus.Length];
            for (int i = 0; i < gpus.Length; i++)
            {
                shot.Gpus[i] = CopyGpu(gpus[i]);
            }
            RefreshNvidia(shot.Gpus);

            shot.Disks = new DiskReading[disks.Length];
            for (int i = 0; i < disks.Length; i++)
            {
                shot.Disks[i] = CopyDisk(disks[i]);
            }
            for (int i = 0; i < diskProbes.Length; i++)
            {
                DiskProbe probe = diskProbes[i];
                DiskReading target = FindDisk(shot.Disks, probe.Index);
                if (target == null)
                {
                    continue;
                }
                target.HasIo = true;
                if (probe.Read != null)
                {
                    target.ReadBps = probe.Read.NextValue();
                }
                if (probe.Write != null)
                {
                    target.WriteBps = probe.Write.NextValue();
                }
                if (probe.Idle != null)
                {
                    float idle = probe.Idle.NextValue();
                    float busy = 100f - idle;
                    if (busy < 0f)
                    {
                        busy = 0f;
                    }
                    if (busy > 100f)
                    {
                        busy = 100f;
                    }
                    target.Busy = busy;
                }
            }
            FillVolumes(shot.Disks);

            shot.Nics = new NicReading[nics.Length];
            for (int i = 0; i < nics.Length; i++)
            {
                shot.Nics[i] = CopyNic(nics[i]);
                if (i < nicProbes.Length && nicProbes[i] != null)
                {
                    shot.Nics[i].HasIo = true;
                    if (nicProbes[i].Rx != null)
                    {
                        shot.Nics[i].RxBps = nicProbes[i].Rx.NextValue();
                    }
                    if (nicProbes[i].Tx != null)
                    {
                        shot.Nics[i].TxBps = nicProbes[i].Tx.NextValue();
                    }
                }
            }
            return shot;
        }

        private void ReadOs()
        {
            host = Environment.MachineName;
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key == null)
                    {
                        return;
                    }
                    string product = Convert.ToString(key.GetValue("ProductName"), CultureInfo.InvariantCulture) ?? "";
                    string display = Convert.ToString(key.GetValue("DisplayVersion"), CultureInfo.InvariantCulture) ?? "";
                    string currentBuild = Convert.ToString(key.GetValue("CurrentBuild"), CultureInfo.InvariantCulture) ?? "";
                    object ubrValue = key.GetValue("UBR");
                    int buildNumber;
                    int.TryParse(currentBuild, NumberStyles.Integer, CultureInfo.InvariantCulture, out buildNumber);
                    if (buildNumber >= 22000 && product.StartsWith("Windows 10", StringComparison.Ordinal))
                    {
                        product = "Windows 11" + product.Substring("Windows 10".Length);
                    }
                    windows = product.Trim();
                    versionName = display.Trim();
                    build = currentBuild;
                    if (ubrValue != null)
                    {
                        build = build + "." + Convert.ToString(ubrValue, CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
            }
        }

        private void ReadComputer()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject item in rows)
                    {
                        string maker = Convert.ToString(item["Manufacturer"], CultureInfo.InvariantCulture) ?? "";
                        string model = Convert.ToString(item["Model"], CultureInfo.InvariantCulture) ?? "";
                        maker = maker.Trim();
                        model = model.Trim();
                        if (model.StartsWith(maker, StringComparison.OrdinalIgnoreCase))
                        {
                            computer = model;
                        }
                        else
                        {
                            computer = (maker + " " + model).Trim();
                        }
                        memoryUsable = ToULong(item["TotalPhysicalMemory"]);
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        private void ReadCpu()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize FROM Win32_Processor"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject item in rows)
                    {
                        cpuName = CleanCpuName(Convert.ToString(item["Name"], CultureInfo.InvariantCulture));
                        cpuCores = ToInt(item["NumberOfCores"]);
                        cpuThreads = ToInt(item["NumberOfLogicalProcessors"]);
                        cpuMaxMhz = ToInt(item["MaxClockSpeed"]);
                        l2Kb = ToInt(item["L2CacheSize"]);
                        l3Kb = ToInt(item["L3CacheSize"]);
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        private void ReadMemory()
        {
            List<string> groups = new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            int speed = 0;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject item in rows)
                    {
                        ulong capacity = ToULong(item["Capacity"]);
                        memoryInstalled += capacity;
                        int moduleSpeed = ToInt(item["ConfiguredClockSpeed"]);
                        if (moduleSpeed <= 0)
                        {
                            moduleSpeed = ToInt(item["Speed"]);
                        }
                        if (moduleSpeed > speed)
                        {
                            speed = moduleSpeed;
                        }
                        string maker = (Convert.ToString(item["Manufacturer"], CultureInfo.InvariantCulture) ?? "").Trim();
                        string kind = MemoryKind(ToInt(item["SMBIOSMemoryType"]));
                        string label = Fmt.WinGb(capacity);
                        if (kind.Length > 0)
                        {
                            label = label + " " + kind;
                        }
                        if (maker.Length > 0)
                        {
                            label = label + " " + maker;
                        }
                        if (!counts.ContainsKey(label))
                        {
                            counts[label] = 0;
                            order.Add(label);
                        }
                        counts[label] = counts[label] + 1;
                    }
                }
            }
            catch
            {
            }
            if (memoryInstalled == 0)
            {
                memoryInstalled = memoryUsable;
            }
            for (int i = 0; i < order.Count; i++)
            {
                string label = order[i];
                groups.Add(counts[label].ToString(CultureInfo.InvariantCulture) + " x " + label);
            }
            memorySummary = string.Join(", ", groups.ToArray());
            if (speed > 0)
            {
                memorySpeed = speed.ToString(CultureInfo.InvariantCulture) + " MT/s";
            }
        }

        private void ReadGpus()
        {
            List<GpuReading> list = new List<GpuReading>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate, PNPDeviceID, AdapterRAM FROM Win32_VideoController"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject item in rows)
                    {
                        string name = (Convert.ToString(item["Name"], CultureInfo.InvariantCulture) ?? "").Trim();
                        if (name.Length == 0)
                        {
                            continue;
                        }
                        GpuReading gpu = new GpuReading();
                        gpu.Name = name;
                        gpu.Driver = (Convert.ToString(item["DriverVersion"], CultureInfo.InvariantCulture) ?? "").Trim();
                        gpu.DriverDate = FormatDriverDate(item["DriverDate"]);
                        gpu.DeviceId = FormatPnp(Convert.ToString(item["PNPDeviceID"], CultureInfo.InvariantCulture));
                        ApplyChip(gpu);
                        list.Add(gpu);
                    }
                }
            }
            catch
            {
            }
            gpus = list.ToArray();
        }

        private void ReadDisks()
        {
            List<DiskReading> list = new List<DiskReading>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Index, Model, Size, InterfaceType, PNPDeviceID, FirmwareRevision FROM Win32_DiskDrive"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject item in rows)
                    {
                        DiskReading disk = new DiskReading();
                        disk.Model = (Convert.ToString(item["Model"], CultureInfo.InvariantCulture) ?? "").Trim();
                        disk.SizeBytes = ToULong(item["Size"]);
                        disk.Firmware = (Convert.ToString(item["FirmwareRevision"], CultureInfo.InvariantCulture) ?? "").Trim();
                        string pnp = Convert.ToString(item["PNPDeviceID"], CultureInfo.InvariantCulture) ?? "";
                        string iface = (Convert.ToString(item["InterfaceType"], CultureInfo.InvariantCulture) ?? "").Trim();
                        if (pnp.IndexOf("NVME", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            disk.Bus = "NVMe";
                        }
                        else if (iface.Length > 0)
                        {
                            disk.Bus = iface;
                        }
                        disk.Index = ToInt(item["Index"]);
                        list.Add(disk);
                    }
                }
            }
            catch
            {
            }
            disks = list.ToArray();
        }

        private void ReadNics()
        {
            Dictionary<string, NicReading> byName = new Dictionary<string, NicReading>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, MACAddress, Speed, NetEnabled FROM Win32_NetworkAdapter"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject item in rows)
                    {
                        bool enabled = false;
                        object flag = item["NetEnabled"];
                        if (flag != null)
                        {
                            enabled = Convert.ToBoolean(flag, CultureInfo.InvariantCulture);
                        }
                        if (!enabled)
                        {
                            continue;
                        }
                        string name = (Convert.ToString(item["Name"], CultureInfo.InvariantCulture) ?? "").Trim();
                        if (name.Length == 0 || SkipNic(name))
                        {
                            continue;
                        }
                        NicReading nic = new NicReading();
                        nic.Name = name;
                        nic.Mac = (Convert.ToString(item["MACAddress"], CultureInfo.InvariantCulture) ?? "").Trim();
                        nic.LinkBps = ToULong(item["Speed"]);
                        byName[NormalizeNic(name)] = nic;
                    }
                }
            }
            catch
            {
            }
            nics = new List<NicReading>(byName.Values).ToArray();
        }

        private void OpenCounters()
        {
            try
            {
                PerformanceCounterCategory logical = new PerformanceCounterCategory("Processor Information");
                List<int> ids = new List<int>();
                string[] instances = logical.GetInstanceNames();
                for (int i = 0; i < instances.Length; i++)
                {
                    int id = LogicalId(instances[i]);
                    if (id >= 0)
                    {
                        ids.Add(id);
                    }
                }
                ids.Sort();
                coreIds = ids.ToArray();
                coreMhz = new PerformanceCounter[coreIds.Length];
                coreLoad = new PerformanceCounter[coreIds.Length];
                for (int i = 0; i < coreIds.Length; i++)
                {
                    string instance = "0," + coreIds[i].ToString(CultureInfo.InvariantCulture);
                    coreMhz[i] = TryCounter("Processor Information", "Processor Frequency", instance);
                    coreLoad[i] = TryCounter("Processor Information", "% Processor Time", instance);
                }
                cpuLoad = TryCounter("Processor Information", "% Processor Time", "0,_Total");
                if (cpuLoad == null)
                {
                    cpuLoad = TryCounter("Processor", "% Processor Time", "_Total");
                }
            }
            catch
            {
            }

            try
            {
                PerformanceCounterCategory zones = new PerformanceCounterCategory("Thermal Zone Information");
                string[] names = zones.GetInstanceNames();
                thermals = new PerformanceCounter[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    thermals[i] = TryCounter("Thermal Zone Information", "Temperature", names[i]);
                }
            }
            catch
            {
            }

            memoryFree = TryCounter("Memory", "Available MBytes", null);

            try
            {
                PerformanceCounterCategory disksCategory = new PerformanceCounterCategory("PhysicalDisk");
                string[] names = disksCategory.GetInstanceNames();
                List<DiskProbe> probes = new List<DiskProbe>();
                for (int i = 0; i < names.Length; i++)
                {
                    int index;
                    string letters;
                    if (!TryDiskInstance(names[i], out index, out letters))
                    {
                        continue;
                    }
                    DiskProbe probe = new DiskProbe();
                    probe.Index = index;
                    probe.Letters = letters;
                    probe.Read = TryCounter("PhysicalDisk", "Disk Read Bytes/sec", names[i]);
                    probe.Write = TryCounter("PhysicalDisk", "Disk Write Bytes/sec", names[i]);
                    probe.Idle = TryCounter("PhysicalDisk", "% Idle Time", names[i]);
                    probes.Add(probe);
                }
                diskProbes = probes.ToArray();
            }
            catch
            {
            }

            try
            {
                PerformanceCounterCategory nicsCategory = new PerformanceCounterCategory("Network Interface");
                string[] names = nicsCategory.GetInstanceNames();
                List<NicReading> matched = new List<NicReading>();
                List<NicProbe> probes = new List<NicProbe>();
                Dictionary<string, NicReading> wmi = new Dictionary<string, NicReading>();
                for (int i = 0; i < nics.Length; i++)
                {
                    wmi[NormalizeNic(nics[i].Name)] = nics[i];
                }
                for (int i = 0; i < names.Length; i++)
                {
                    if (SkipNic(names[i]))
                    {
                        continue;
                    }
                    string key = NormalizeNic(names[i]);
                    NicReading nic;
                    if (!wmi.TryGetValue(key, out nic))
                    {
                        nic = new NicReading();
                        nic.Name = names[i].Replace('[', '(').Replace(']', ')');
                    }
                    NicProbe probe = new NicProbe();
                    probe.Rx = TryCounter("Network Interface", "Bytes Received/sec", names[i]);
                    probe.Tx = TryCounter("Network Interface", "Bytes Sent/sec", names[i]);
                    matched.Add(nic);
                    probes.Add(probe);
                }
                if (matched.Count > 0)
                {
                    nics = matched.ToArray();
                    nicProbes = probes.ToArray();
                }
            }
            catch
            {
            }
        }

        private void Prime()
        {
            Next(cpuLoad);
            for (int i = 0; i < coreLoad.Length; i++)
            {
                Next(coreLoad[i]);
                Next(coreMhz[i]);
            }
            for (int i = 0; i < thermals.Length; i++)
            {
                Next(thermals[i]);
            }
            Next(memoryFree);
            for (int i = 0; i < diskProbes.Length; i++)
            {
                Next(diskProbes[i].Read);
                Next(diskProbes[i].Write);
                Next(diskProbes[i].Idle);
            }
            for (int i = 0; i < nicProbes.Length; i++)
            {
                Next(nicProbes[i].Rx);
                Next(nicProbes[i].Tx);
            }
        }

        private static void Next(PerformanceCounter counter)
        {
            if (counter == null)
            {
                return;
            }
            try
            {
                counter.NextValue();
            }
            catch
            {
            }
        }

        private void RefreshNvidia(GpuReading[] current)
        {
            string path = FindNvidiaSmi();
            if (path == null)
            {
                return;
            }
            string query = Exec(path, "--query-gpu=name,temperature.gpu,clocks.gr,clocks.mem,utilization.gpu,power.draw,memory.total,memory.used,driver_version,vbios_version,pcie.link.gen.gpucurrent,pcie.link.gen.max,pcie.link.width.current,pcie.link.width.max,pstate,clocks.max.gr,clocks.max.mem,pci.device_id,pci.sub_device_id --format=csv,noheader,nounits", 4000);
            string detail = Exec(path, "-q", 5000);
            List<float> rx = new List<float>();
            List<float> tx = new List<float>();
            ParseThroughput(detail, rx, tx);
            string[] lines = query.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int nvidiaIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');
                if (fields.Length < 10)
                {
                    continue;
                }
                string name = fields[0].Trim();
                GpuReading gpu = MatchGpu(current, name);
                if (gpu == null)
                {
                    gpu = new GpuReading();
                    gpu.Name = name;
                }
                gpu.Live = true;
                gpu.Name = name.Length > 0 ? name : gpu.Name;
                float temp;
                if (TryFloat(Field(fields, 1), out temp))
                {
                    gpu.HasTemp = true;
                    gpu.TempC = temp;
                }
                float coreClock;
                if (TryFloat(Field(fields, 2), out coreClock))
                {
                    gpu.HasCoreMhz = true;
                    gpu.CoreMhz = coreClock;
                }
                float memClock;
                if (TryFloat(Field(fields, 3), out memClock))
                {
                    gpu.HasMemMhz = true;
                    gpu.MemMhz = memClock;
                }
                float load;
                if (TryFloat(Field(fields, 4), out load))
                {
                    gpu.HasLoad = true;
                    gpu.Load = load;
                }
                float watts;
                if (TryFloat(Field(fields, 5), out watts))
                {
                    gpu.HasWatts = true;
                    gpu.Watts = watts;
                }
                int total;
                if (TryInt(Field(fields, 6), out total))
                {
                    gpu.MemoryTotalMb = total;
                }
                int used;
                if (TryInt(Field(fields, 7), out used))
                {
                    gpu.MemoryUsedMb = used;
                }
                string nvidiaDriver = Field(fields, 8);
                if (nvidiaDriver.Length > 0 && nvidiaDriver != "[N/A]")
                {
                    if (gpu.Driver.Length > 0)
                    {
                        gpu.Driver = gpu.Driver + "  (NVIDIA " + nvidiaDriver + ")";
                    }
                    else
                    {
                        gpu.Driver = nvidiaDriver;
                    }
                }
                string bios = Field(fields, 9);
                if (bios.Length > 0 && bios != "[N/A]")
                {
                    gpu.Bios = bios.ToUpperInvariant();
                }
                gpu.Pcie = FormatPcie(Field(fields, 10), Field(fields, 11), Field(fields, 12), Field(fields, 13));
                string pstate = Field(fields, 14);
                if (pstate.Length > 0 && pstate != "[N/A]")
                {
                    gpu.PState = pstate;
                }
                int maxCore;
                if (TryInt(Field(fields, 15), out maxCore))
                {
                    gpu.MaxCoreMhz = maxCore;
                }
                int maxMem;
                if (TryInt(Field(fields, 16), out maxMem))
                {
                    gpu.MaxMemMhz = maxMem;
                }
                uint deviceId;
                uint subId;
                if (TryHex(Field(fields, 17), out deviceId) && TryHex(Field(fields, 18), out subId))
                {
                    gpu.DeviceId = Fmt.Pci(deviceId, subId);
                    ApplyChip(gpu);
                }
                if (nvidiaIndex < rx.Count)
                {
                    gpu.HasRx = true;
                    gpu.RxKBps = rx[nvidiaIndex];
                }
                if (nvidiaIndex < tx.Count)
                {
                    gpu.HasTx = true;
                    gpu.TxKBps = tx[nvidiaIndex];
                }
                nvidiaIndex++;
            }
        }

        private static void ParseThroughput(string text, List<float> rx, List<float> tx)
        {
            if (text == null || text.Length == 0)
            {
                return;
            }
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool isRx = line.IndexOf("Rx Throughput", StringComparison.Ordinal) >= 0;
                bool isTx = line.IndexOf("Tx Throughput", StringComparison.Ordinal) >= 0;
                if (!isRx && !isTx)
                {
                    continue;
                }
                float kb;
                if (!TryThroughput(line, out kb))
                {
                    kb = 0f;
                }
                if (isRx)
                {
                    rx.Add(kb);
                }
                else
                {
                    tx.Add(kb);
                }
            }
        }

        private static bool TryThroughput(string line, out float kbPerSec)
        {
            kbPerSec = 0f;
            int colon = line.LastIndexOf(':');
            if (colon < 0)
            {
                return false;
            }
            string tail = line.Substring(colon + 1).Trim();
            if (tail.StartsWith("N/A", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string number = tail;
            int space = tail.IndexOf(' ');
            if (space > 0)
            {
                number = tail.Substring(0, space);
            }
            return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out kbPerSec);
        }

        private static GpuReading MatchGpu(GpuReading[] current, string name)
        {
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf(current[i].Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current[i];
                }
            }
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].Name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 && !current[i].Live)
                {
                    return current[i];
                }
            }
            return null;
        }

        private void FillVolumes(DiskReading[] current)
        {
            List<string> lines = new List<string>();
            ulong totalSize = 0;
            ulong totalFree = 0;
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    DriveInfo drive = drives[i];
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                    {
                        continue;
                    }
                    totalSize += (ulong)drive.TotalSize;
                    totalFree += (ulong)drive.AvailableFreeSpace;
                    string fileSystem = "";
                    try
                    {
                        fileSystem = drive.DriveFormat;
                    }
                    catch
                    {
                    }
                    string line = drive.Name.TrimEnd('\\');
                    if (fileSystem.Length > 0)
                    {
                        line = line + " " + fileSystem;
                    }
                    line = line + "  " + Fmt.WinGb((ulong)drive.AvailableFreeSpace) + " free / " + Fmt.WinGb((ulong)drive.TotalSize);
                    lines.Add(line);
                }
            }
            catch
            {
            }
            string joined = string.Join("   ", lines.ToArray());
            for (int i = 0; i < current.Length; i++)
            {
                current[i].Volumes = joined;
                if (current.Length == 1)
                {
                    current[i].VolumeSize = totalSize;
                    current[i].VolumeFree = totalFree;
                }
            }
            if (current.Length > 1)
            {
                for (int d = 0; d < current.Length; d++)
                {
                    string letters = LettersFor(current[d].Index);
                    if (letters.Length == 0)
                    {
                        continue;
                    }
                    ulong size = 0;
                    ulong free = 0;
                    try
                    {
                        DriveInfo[] drives = DriveInfo.GetDrives();
                        for (int i = 0; i < drives.Length; i++)
                        {
                            if (!drives[i].IsReady)
                            {
                                continue;
                            }
                            string letter = drives[i].Name.Substring(0, 1);
                            if (letters.IndexOf(letter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                size += (ulong)drives[i].TotalSize;
                                free += (ulong)drives[i].AvailableFreeSpace;
                            }
                        }
                    }
                    catch
                    {
                    }
                    current[d].VolumeSize = size;
                    current[d].VolumeFree = free;
                }
            }
        }

        private string LettersFor(int index)
        {
            for (int i = 0; i < diskProbes.Length; i++)
            {
                if (diskProbes[i].Index == index)
                {
                    return diskProbes[i].Letters ?? "";
                }
            }
            return "";
        }

        private string FindNvidiaSmi()
        {
            if (nvidiaSmi.Length > 0)
            {
                return nvidiaSmi == "-" ? null : nvidiaSmi;
            }
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe"),
                Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    nvidiaSmi = candidates[i];
                    return nvidiaSmi;
                }
            }
            nvidiaSmi = "-";
            return null;
        }

        private static string Exec(string file, string args, int timeoutMs)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = file;
                start.Arguments = args;
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.CreateNoWindow = true;
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return "";
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                        return output;
                    }
                    return output;
                }
            }
            catch
            {
                return "";
            }
        }

        private static void ApplyChip(GpuReading gpu)
        {
            string id = gpu.DeviceId ?? "";
            if (id.IndexOf("28A1", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }
            gpu.Chip = "AD107";
            gpu.Process = "5 nm";
            gpu.Shaders = "2560 Unified";
            gpu.RopsTmus = "48 / 80";
            gpu.Bus = "96 bit";
            gpu.MemoryKind = "GDDR6";
        }

        private static string FormatPcie(string currentGen, string maxGen, string currentWidth, string maxWidth)
        {
            int curGen;
            int topGen;
            int curWidth;
            int topWidth;
            bool hasCurGen = TryInt(currentGen, out curGen);
            bool hasMaxGen = TryInt(maxGen, out topGen);
            bool hasCurWidth = TryInt(StripX(currentWidth), out curWidth);
            bool hasMaxWidth = TryInt(StripX(maxWidth), out topWidth);
            if (!hasMaxGen && !hasMaxWidth && !hasCurGen && !hasCurWidth)
            {
                return "";
            }
            StringBuilder text = new StringBuilder();
            text.Append("PCIe");
            if (hasMaxWidth)
            {
                text.Append(" x");
                text.Append(topWidth.ToString(CultureInfo.InvariantCulture));
            }
            if (hasMaxGen)
            {
                text.Append(" ");
                text.Append(topGen.ToString(CultureInfo.InvariantCulture));
                text.Append(".0");
            }
            if (hasCurWidth || hasCurGen)
            {
                text.Append(" @");
                if (hasCurWidth)
                {
                    text.Append(" x");
                    text.Append(curWidth.ToString(CultureInfo.InvariantCulture));
                }
                if (hasCurGen)
                {
                    text.Append(" ");
                    text.Append(curGen.ToString(CultureInfo.InvariantCulture));
                    text.Append(".0");
                }
            }
            return text.ToString();
        }

        private static string StripX(string text)
        {
            if (text == null)
            {
                return "";
            }
            return text.Trim().TrimEnd('x', 'X');
        }

        private static string FormatPnp(string pnp)
        {
            if (pnp == null)
            {
                return "";
            }
            uint vendor = HexAfter(pnp, "VEN_");
            uint device = HexAfter(pnp, "DEV_");
            uint sub = HexAfter(pnp, "SUBSYS_");
            if (vendor == 0 && device == 0)
            {
                return "";
            }
            uint packed = device << 16 | vendor;
            return Fmt.Pci(packed, sub);
        }

        private static uint HexAfter(string text, string token)
        {
            int at = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return 0;
            }
            int start = at + token.Length;
            int end = start;
            while (end < text.Length && Uri.IsHexDigit(text[end]))
            {
                end++;
            }
            uint value;
            if (uint.TryParse(text.Substring(start, end - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            return 0;
        }

        private static string FormatDriverDate(object raw)
        {
            if (raw == null)
            {
                return "";
            }
            try
            {
                string text = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (text == null || text.Length < 8)
                {
                    return "";
                }
                DateTime date = ManagementDateTimeConverter.ToDateTime(text);
                return date.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "";
            }
        }

        private static string CleanCpuName(string name)
        {
            if (name == null)
            {
                return "";
            }
            name = name.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "");
            while (name.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                name = name.Replace("  ", " ");
            }
            return name.Trim();
        }

        private static string MemoryKind(int code)
        {
            switch (code)
            {
                case 20: return "DDR";
                case 21: return "DDR2";
                case 24: return "DDR3";
                case 26: return "DDR4";
                case 30: return "LPDDR4";
                case 34: return "LPDDR5";
                case 35: return "LPDDR5X";
                default: return "";
            }
        }

        private static int LogicalId(string instance)
        {
            if (instance == null)
            {
                return -1;
            }
            int comma = instance.LastIndexOf(',');
            if (comma < 0 || comma + 1 >= instance.Length)
            {
                return -1;
            }
            int id;
            if (int.TryParse(instance.Substring(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                return id;
            }
            return -1;
        }

        private static bool TryDiskInstance(string name, out int index, out string letters)
        {
            index = -1;
            letters = "";
            if (name == null || name == "_Total")
            {
                return false;
            }
            string[] parts = name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return false;
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 1; i < parts.Length; i++)
            {
                string letter = parts[i].Trim().TrimEnd(':');
                if (letter.Length == 0)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(letter);
            }
            letters = builder.ToString();
            return true;
        }

        private static bool SkipNic(string name)
        {
            string text = name.ToLowerInvariant();
            return text.Contains("loopback") || text.Contains("isatap") || text.Contains("teredo") || text.Contains("bluetooth");
        }

        private static string NormalizeNic(string name)
        {
            return (name ?? "").Replace('(', '[').Replace(')', ']').Replace('#', '_').ToLowerInvariant();
        }

        private static PerformanceCounter TryCounter(string category, string name, string instance)
        {
            try
            {
                PerformanceCounter counter = new PerformanceCounter(category, name, instance, true);
                return counter;
            }
            catch
            {
                return null;
            }
        }

        private static DiskReading FindDisk(DiskReading[] current, int index)
        {
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].Index == index)
                {
                    return current[i];
                }
            }
            if (current.Length == 1)
            {
                return current[0];
            }
            return null;
        }

        private static GpuReading CopyGpu(GpuReading source)
        {
            GpuReading copy = new GpuReading();
            copy.Name = source.Name;
            copy.DeviceId = source.DeviceId;
            copy.Chip = source.Chip;
            copy.Process = source.Process;
            copy.Shaders = source.Shaders;
            copy.RopsTmus = source.RopsTmus;
            copy.Bus = source.Bus;
            copy.MemoryKind = source.MemoryKind;
            copy.Bios = source.Bios;
            copy.Driver = source.Driver;
            copy.DriverDate = source.DriverDate;
            copy.MemoryTotalMb = source.MemoryTotalMb;
            return copy;
        }

        private static DiskReading CopyDisk(DiskReading source)
        {
            DiskReading copy = new DiskReading();
            copy.Index = source.Index;
            copy.Model = source.Model;
            copy.Bus = source.Bus;
            copy.Firmware = source.Firmware;
            copy.SizeBytes = source.SizeBytes;
            return copy;
        }

        private static NicReading CopyNic(NicReading source)
        {
            NicReading copy = new NicReading();
            copy.Name = source.Name;
            copy.Mac = source.Mac;
            copy.LinkBps = source.LinkBps;
            return copy;
        }

        private static string Field(string[] fields, int index)
        {
            if (index < 0 || index >= fields.Length)
            {
                return "";
            }
            return fields[index].Trim();
        }

        private static bool TryFloat(string text, out float value)
        {
            value = 0f;
            if (text == null)
            {
                return false;
            }
            text = text.Trim();
            if (text.Length == 0 || text.StartsWith("[N/A]", StringComparison.OrdinalIgnoreCase) || text.StartsWith("N/A", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryInt(string text, out int value)
        {
            value = 0;
            float number;
            if (!TryFloat(text, out number))
            {
                return false;
            }
            value = (int)Math.Round(number);
            return true;
        }

        private static bool TryHex(string text, out uint value)
        {
            value = 0;
            if (text == null)
            {
                return false;
            }
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static ulong ToULong(object value)
        {
            if (value == null)
            {
                return 0;
            }
            try
            {
                return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static int ToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static float ClampPercent(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 100f)
            {
                return 100f;
            }
            return value;
        }

        private sealed class DiskProbe
        {
            public int Index;
            public string Letters;
            public PerformanceCounter Read;
            public PerformanceCounter Write;
            public PerformanceCounter Idle;
        }

        private sealed class NicProbe
        {
            public PerformanceCounter Rx;
            public PerformanceCounter Tx;
        }
    }
}
