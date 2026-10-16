using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HpFanCtrl
{
    internal sealed class AppSettings
    {
        public TempSource Source = TempSource.Combined;
        public FanPoint[] Points = FanCurve.DefaultPoints();

        public static string FilePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hp-fan-ctrl");
            return Path.Combine(folder, "settings.txt");
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                string path = FilePath();
                if (!File.Exists(path))
                {
                    return settings;
                }
                string[] lines = File.ReadAllLines(path);
                int count = 0;
                FanPoint[] loaded = new FanPoint[6];
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("source=", StringComparison.OrdinalIgnoreCase))
                    {
                        TempSource source;
                        if (Enum.TryParse(line.Substring(7), true, out source))
                        {
                            settings.Source = source;
                        }
                    }
                    else
                    {
                        int split = line.IndexOf('=');
                        int temp;
                        int percent;
                        if (split > 0
                            && int.TryParse(line.Substring(0, split), NumberStyles.Integer, CultureInfo.InvariantCulture, out temp)
                            && int.TryParse(line.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)
                            && count < loaded.Length)
                        {
                            loaded[count] = new FanPoint(temp, percent);
                            count++;
                        }
                    }
                }
                if (count == loaded.Length)
                {
                    settings.Points = loaded;
                }
            }
            catch
            {
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                string path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                StringBuilder text = new StringBuilder();
                text.AppendLine("source=" + Source.ToString());
                for (int i = 0; i < Points.Length; i++)
                {
                    text.Append(Points[i].Temp.ToString(CultureInfo.InvariantCulture));
                    text.Append('=');
                    text.AppendLine(Points[i].Percent.ToString(CultureInfo.InvariantCulture));
                }
                File.WriteAllText(path, text.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
