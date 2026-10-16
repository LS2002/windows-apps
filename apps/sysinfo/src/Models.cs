using System;
using System.Globalization;
using System.Text;

namespace SysInfo
{
    internal sealed class CoreReading
    {
        public int Id;
        public float Mhz;
        public float Load;
    }

    internal sealed class GpuReading
    {
        public string Name = "";
        public string DeviceId = "";
        public string Chip = "";
        public string Process = "";
        public string Shaders = "";
        public string RopsTmus = "";
        public string Bus = "";
        public string MemoryKind = "";
        public string Bios = "";
        public string Driver = "";
        public string DriverDate = "";
        public string Pcie = "";
        public string PState = "";
        public int MemoryTotalMb;
        public int MemoryUsedMb;
        public int MaxCoreMhz;
        public int MaxMemMhz;
        public bool Live;
        public bool HasTemp;
        public float TempC;
        public bool HasCoreMhz;
        public float CoreMhz;
        public bool HasMemMhz;
        public float MemMhz;
        public bool HasLoad;
        public float Load;
        public bool HasWatts;
        public float Watts;
        public bool HasRx;
        public float RxKBps;
        public bool HasTx;
        public float TxKBps;
    }

    internal sealed class DiskReading
    {
        public int Index;
        public string Model = "";
        public string Bus = "";
        public string Firmware = "";
        public ulong SizeBytes;
        public string Volumes = "";
        public bool HasIo;
        public float ReadBps;
        public float WriteBps;
        public float Busy;
        public ulong VolumeSize;
        public ulong VolumeFree;
    }

    internal sealed class NicReading
    {
        public string Name = "";
        public string Mac = "";
        public ulong LinkBps;
        public bool HasIo;
        public float RxBps;
        public float TxBps;
    }

    internal sealed class Snapshot
    {
        public string Host = "";
        public string Windows = "";
        public string Version = "";
        public string Build = "";
        public string Computer = "";
        public string CpuName = "";
        public int CpuCores;
        public int CpuThreads;
        public int CpuMaxMhz;
        public int L2Kb;
        public int L3Kb;
        public bool HasPackageTemp;
        public float PackageC;
        public bool HasCpuLoad;
        public float CpuLoad;
        public CoreReading[] Cores = new CoreReading[0];
        public string MemorySummary = "";
        public string MemorySpeed = "";
        public ulong MemoryInstalled;
        public ulong MemoryUsable;
        public bool HasMemory;
        public float MemoryAvailableMb;
        public GpuReading[] Gpus = new GpuReading[0];
        public DiskReading[] Disks = new DiskReading[0];
        public NicReading[] Nics = new NicReading[0];
        public string Note = "";
        public DateTime TakenUtc;

        public string Report()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(Host + " | " + Windows + " | " + Version + " | " + Build);
            text.AppendLine(Computer);
            text.AppendLine(CpuName + " cores=" + CpuCores.ToString(CultureInfo.InvariantCulture)
                + " threads=" + CpuThreads.ToString(CultureInfo.InvariantCulture)
                + " maxMhz=" + CpuMaxMhz.ToString(CultureInfo.InvariantCulture)
                + " packageC=" + (HasPackageTemp ? PackageC.ToString("0.0", CultureInfo.InvariantCulture) : "n/a")
                + " load=" + (HasCpuLoad ? CpuLoad.ToString("0.0", CultureInfo.InvariantCulture) : "n/a"));
            for (int i = 0; i < Cores.Length; i++)
            {
                CoreReading core = Cores[i];
                text.AppendLine("  core " + core.Id.ToString(CultureInfo.InvariantCulture)
                    + " mhz=" + core.Mhz.ToString("0", CultureInfo.InvariantCulture)
                    + " load=" + core.Load.ToString("0.0", CultureInfo.InvariantCulture));
            }
            text.AppendLine("memory " + MemorySummary + " usable=" + MemoryUsable.ToString(CultureInfo.InvariantCulture)
                + " availMb=" + MemoryAvailableMb.ToString("0", CultureInfo.InvariantCulture));
            for (int i = 0; i < Gpus.Length; i++)
            {
                GpuReading gpu = Gpus[i];
                text.AppendLine("gpu " + gpu.Name + " id=" + gpu.DeviceId + " chip=" + gpu.Chip
                    + " live=" + gpu.Live.ToString()
                    + " temp=" + gpu.TempC.ToString("0.0", CultureInfo.InvariantCulture)
                    + " mhz=" + gpu.CoreMhz.ToString("0", CultureInfo.InvariantCulture)
                    + " load=" + gpu.Load.ToString("0", CultureInfo.InvariantCulture)
                    + " W=" + gpu.Watts.ToString("0.00", CultureInfo.InvariantCulture)
                    + " rx=" + gpu.RxKBps.ToString("0", CultureInfo.InvariantCulture)
                    + " tx=" + gpu.TxKBps.ToString("0", CultureInfo.InvariantCulture)
                    + " pcie=" + gpu.Pcie);
            }
            for (int i = 0; i < Disks.Length; i++)
            {
                DiskReading disk = Disks[i];
                text.AppendLine("disk " + disk.Model + " " + disk.Bus
                    + " read=" + disk.ReadBps.ToString("0", CultureInfo.InvariantCulture)
                    + " write=" + disk.WriteBps.ToString("0", CultureInfo.InvariantCulture)
                    + " busy=" + disk.Busy.ToString("0", CultureInfo.InvariantCulture));
            }
            for (int i = 0; i < Nics.Length; i++)
            {
                NicReading nic = Nics[i];
                text.AppendLine("nic " + nic.Name + " link=" + nic.LinkBps.ToString(CultureInfo.InvariantCulture)
                    + " rx=" + nic.RxBps.ToString("0", CultureInfo.InvariantCulture)
                    + " tx=" + nic.TxBps.ToString("0", CultureInfo.InvariantCulture));
            }
            if (Note.Length > 0)
            {
                text.AppendLine(Note);
            }
            return text.ToString();
        }
    }

    internal static class Fmt
    {
        public static string Num(float value, int decimals)
        {
            return value.ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture).TrimEnd();
        }

        public static string Temp(float celsius)
        {
            return celsius.ToString("0", CultureInfo.InvariantCulture) + "°";
        }

        public static string Ghz(float mhz)
        {
            return (mhz / 1000f).ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string Percent(float value)
        {
            if (value < 0f)
            {
                value = 0f;
            }
            if (value > 100f)
            {
                value = 100f;
            }
            return value.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        public static string Watts(float watts)
        {
            if (watts >= 10f)
            {
                return watts.ToString("0.0", CultureInfo.InvariantCulture);
            }
            return watts.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string Rate(float bytesPerSec)
        {
            if (bytesPerSec < 0f)
            {
                bytesPerSec = 0f;
            }
            const float kb = 1024f;
            const float mb = 1024f * 1024f;
            const float gb = 1024f * 1024f * 1024f;
            if (bytesPerSec >= gb)
            {
                return (bytesPerSec / gb).ToString("0.00", CultureInfo.InvariantCulture);
            }
            if (bytesPerSec >= mb)
            {
                return (bytesPerSec / mb).ToString("0.0", CultureInfo.InvariantCulture);
            }
            if (bytesPerSec >= kb)
            {
                return (bytesPerSec / kb).ToString("0", CultureInfo.InvariantCulture);
            }
            return bytesPerSec.ToString("0", CultureInfo.InvariantCulture);
        }

        public static string RateUnit(float bytesPerSec)
        {
            if (bytesPerSec >= 1024f * 1024f * 1024f)
            {
                return "GB/s";
            }
            if (bytesPerSec >= 1024f * 1024f)
            {
                return "MB/s";
            }
            if (bytesPerSec >= 1024f)
            {
                return "KB/s";
            }
            return "B/s";
        }

        public static string WinGb(ulong bytes)
        {
            return GbNumber(bytes) + " GB";
        }

        public static string GbNumber(ulong bytes)
        {
            double gb = bytes / 1024d / 1024d / 1024d;
            if (gb >= 100d)
            {
                return gb.ToString("0", CultureInfo.InvariantCulture);
            }
            return gb.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string CacheMb(int kilobytes)
        {
            if (kilobytes <= 0)
            {
                return "";
            }
            double mb = kilobytes / 1024d;
            if (mb >= 10d)
            {
                return mb.ToString("0", CultureInfo.InvariantCulture) + " MB";
            }
            return mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        public static string Link(ulong bitsPerSec)
        {
            if (bitsPerSec <= 0)
            {
                return "n/a";
            }
            return LinkNumber(bitsPerSec) + " " + LinkUnit(bitsPerSec);
        }

        public static string LinkNumber(ulong bitsPerSec)
        {
            double mbps = bitsPerSec / 1000d / 1000d;
            if (mbps >= 1000d)
            {
                return (mbps / 1000d).ToString("0.00", CultureInfo.InvariantCulture);
            }
            return mbps.ToString("0", CultureInfo.InvariantCulture);
        }

        public static string LinkUnit(ulong bitsPerSec)
        {
            if (bitsPerSec / 1000d / 1000d >= 1000d)
            {
                return "Gbps";
            }
            return "Mbps";
        }

        public static string Pci(uint deviceId, uint subSystem)
        {
            uint vendor = deviceId & 0xFFFFu;
            uint device = (deviceId >> 16) & 0xFFFFu;
            uint subVendor = subSystem & 0xFFFFu;
            uint subDevice = (subSystem >> 16) & 0xFFFFu;
            return string.Format(CultureInfo.InvariantCulture, "{0:X4} {1:X4} - {2:X4} {3:X4}", vendor, device, subVendor, subDevice);
        }
    }
}
