using System;
using System.IO;
using System.Windows.Forms;

namespace HpFanCtrl
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--self-test")
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-result.txt");
                    if (i + 1 < args.Length)
                    {
                        path = args[i + 1];
                    }
                    return FanCurve.RunSelfTest(path);
                }
                if (args[i] == "--probe")
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-result.txt");
                    if (i + 1 < args.Length)
                    {
                        path = args[i + 1];
                    }
                    using (HpBios bios = new HpBios())
                    {
                        File.WriteAllText(path, bios.ProbeReport());
                    }
                    return 0;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
