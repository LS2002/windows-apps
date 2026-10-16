using System;
using System.Windows.Forms;

namespace RectangleWindows
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--self-test")
                {
                    string resultPath = "self-test-result.txt";
                    if (i + 1 < args.Length)
                    {
                        resultPath = args[i + 1];
                    }
                    return SelfTest.Run(resultPath);
                }
            }
            Application.Run(new TrayApp());
            return 0;
        }
    }
}
