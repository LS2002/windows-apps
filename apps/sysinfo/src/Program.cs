using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SysInfo
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
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        path = args[i + 1];
                    }
                    return RunSelfTest(path);
                }
            }
            string shotDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--shot" && i + 1 < args.Length)
                {
                    shotDir = args[i + 1];
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sysinfo-error.txt");
                    File.WriteAllText(path, e.Exception.ToString());
                }
                catch
                {
                }
            };
            MainForm form = new MainForm();
            if (shotDir != null)
            {
                form.Shown += delegate
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        DateTime until = DateTime.UtcNow.AddSeconds(25);
                        while (form.Samples < 2 && DateTime.UtcNow < until)
                        {
                            Thread.Sleep(40);
                        }
                        form.BeginInvoke(new Action(delegate
                        {
                            try
                            {
                                Directory.CreateDirectory(shotDir);
                                form.SaveShots(shotDir);
                            }
                            catch (Exception ex)
                            {
                                File.WriteAllText(Path.Combine(shotDir, "shot-error.txt"), ex.ToString());
                            }
                            form.Close();
                        }));
                    });
                };
            }
            Application.Run(form);
            return 0;
        }

        private static int RunSelfTest(string path)
        {
            try
            {
                Collector collector = new Collector();
                collector.Open();
                collector.Sample();
                Thread.Sleep(900);
                Snapshot shot = collector.Sample();
                File.WriteAllText(path, shot.Report());
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(path, ex.ToString());
                return 1;
            }
        }
    }
}
