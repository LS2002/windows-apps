using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;

namespace HpFanCtrl
{
    internal static class Sensors
    {
        private static string nvidiaSmi;

        public static string MachineName()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string maker = (Convert.ToString(item["Manufacturer"], CultureInfo.InvariantCulture) ?? "").Trim();
                        string model = (Convert.ToString(item["Model"], CultureInfo.InvariantCulture) ?? "").Trim();
                        if (model.Length == 0)
                        {
                            return maker.Length == 0 ? "HP" : maker;
                        }
                        if (maker.Length == 0 || model.StartsWith(maker, StringComparison.OrdinalIgnoreCase))
                        {
                            return model;
                        }
                        return maker + " " + model;
                    }
                }
            }
            catch
            {
            }
            return "HP";
        }

        public static int? CpuCelsius()
        {
            try
            {
                int best = -1;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject zone in searcher.Get())
                    {
                        int raw = Convert.ToInt32(zone["CurrentTemperature"], CultureInfo.InvariantCulture);
                        int celsius = (int)Math.Round(raw / 10.0 - 273.15);
                        if (celsius >= 15 && celsius <= 115 && celsius > best)
                        {
                            best = celsius;
                        }
                    }
                }
                if (best > 0)
                {
                    return best;
                }
            }
            catch
            {
            }
            return null;
        }

        public static int? GpuCelsius()
        {
            string path = FindNvidiaSmi();
            if (path == null)
            {
                return null;
            }
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = path;
                start.Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits";
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return null;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(4000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                        return null;
                    }
                    int best = -1;
                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int value;
                        if (int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        {
                            if (value >= 0 && value <= 120 && value > best)
                            {
                                best = value;
                            }
                        }
                    }
                    if (best >= 0)
                    {
                        return best;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string FindNvidiaSmi()
        {
            if (nvidiaSmi != null)
            {
                return nvidiaSmi.Length == 0 ? null : nvidiaSmi;
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
            nvidiaSmi = "";
            return null;
        }
    }
}
