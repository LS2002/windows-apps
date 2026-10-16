using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HpFanCtrl
{
    internal enum TempSource
    {
        Cpu = 0,
        Gpu = 1,
        Combined = 2
    }

    internal sealed class FanPoint
    {
        public int Temp;
        public int Percent;

        public FanPoint(int temp, int percent)
        {
            Temp = temp;
            Percent = percent;
        }
    }

    internal static class FanCurve
    {
        public const int MinLevel = 20;
        public const int MaxLevel = 55;

        public static FanPoint[] DefaultPoints()
        {
            return new FanPoint[]
            {
                new FanPoint(45, 30),
                new FanPoint(55, 40),
                new FanPoint(65, 55),
                new FanPoint(75, 70),
                new FanPoint(85, 85),
                new FanPoint(95, 100)
            };
        }

        public static int PercentAt(FanPoint[] points, int tempC)
        {
            if (points == null || points.Length == 0)
            {
                return 0;
            }
            if (tempC <= points[0].Temp)
            {
                return points[0].Percent;
            }
            int last = points.Length - 1;
            if (tempC >= points[last].Temp)
            {
                return points[last].Percent;
            }
            for (int i = 1; i < points.Length; i++)
            {
                if (tempC <= points[i].Temp)
                {
                    FanPoint left = points[i - 1];
                    FanPoint right = points[i];
                    int span = right.Temp - left.Temp;
                    if (span <= 0)
                    {
                        return right.Percent;
                    }
                    double t = (tempC - left.Temp) / (double)span;
                    return (int)Math.Round(left.Percent + t * (right.Percent - left.Percent));
                }
            }
            return points[last].Percent;
        }

        public static void SetPercent(FanPoint[] points, int index, int percent)
        {
            if (percent < 0)
            {
                percent = 0;
            }
            if (percent > 100)
            {
                percent = 100;
            }
            points[index].Percent = percent;
            for (int i = index - 1; i >= 0; i--)
            {
                if (points[i].Percent > points[i + 1].Percent)
                {
                    points[i].Percent = points[i + 1].Percent;
                }
            }
            for (int i = index + 1; i < points.Length; i++)
            {
                if (points[i].Percent < points[i - 1].Percent)
                {
                    points[i].Percent = points[i - 1].Percent;
                }
            }
        }

        // 0 hands the fans back to the BIOS. 1-100% maps onto the speed
        // range the HP BIOS fan command actually accepts.
        public static byte PercentToLevel(int percent)
        {
            if (percent <= 0)
            {
                return 0;
            }
            if (percent > 100)
            {
                percent = 100;
            }
            int level = MinLevel + (percent * (MaxLevel - MinLevel)) / 100;
            if (level < MinLevel)
            {
                level = MinLevel;
            }
            if (level > MaxLevel)
            {
                level = MaxLevel;
            }
            return (byte)level;
        }

        public static int? PickTemp(TempSource source, int? cpu, int? gpu)
        {
            if (source == TempSource.Cpu)
            {
                return cpu;
            }
            if (source == TempSource.Gpu)
            {
                return gpu;
            }
            if (cpu.HasValue && gpu.HasValue)
            {
                return cpu.Value > gpu.Value ? cpu.Value : gpu.Value;
            }
            if (cpu.HasValue)
            {
                return cpu;
            }
            return gpu;
        }

        public static int RunSelfTest(string path)
        {
            List<string> lines = new List<string>();
            bool ok = true;
            FanPoint[] points = DefaultPoints();
            Check(lines, ref ok, "at-45", PercentAt(points, 45) == 30);
            Check(lines, ref ok, "at-50", PercentAt(points, 50) == 35);
            Check(lines, ref ok, "below", PercentAt(points, 30) == 30);
            Check(lines, ref ok, "above", PercentAt(points, 100) == 100);
            Check(lines, ref ok, "level-0", PercentToLevel(0) == 0);
            Check(lines, ref ok, "level-100", PercentToLevel(100) == MaxLevel);
            Check(lines, ref ok, "level-50", PercentToLevel(50) == MinLevel + (MaxLevel - MinLevel) / 2);
            SetPercent(points, 2, 20);
            Check(lines, ref ok, "monotonic-down", points[0].Percent <= points[1].Percent && points[1].Percent <= points[2].Percent);
            points = DefaultPoints();
            SetPercent(points, 1, 100);
            Check(lines, ref ok, "monotonic-up", points[1].Percent <= points[2].Percent && points[2].Percent <= points[5].Percent);
            int? hotter = PickTemp(TempSource.Combined, 60, 72);
            Check(lines, ref ok, "combined", hotter == 72);
            Check(lines, ref ok, "cpu-only", PickTemp(TempSource.Combined, 61, null) == 61);
            lines.Add(ok ? "PASS" : "FAIL");
            File.WriteAllLines(path, lines.ToArray(), Encoding.UTF8);
            return ok ? 0 : 1;
        }

        private static void Check(List<string> lines, ref bool ok, string name, bool pass)
        {
            if (!pass)
            {
                ok = false;
            }
            lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}", pass ? "ok" : "FAIL", name));
        }
    }
}
