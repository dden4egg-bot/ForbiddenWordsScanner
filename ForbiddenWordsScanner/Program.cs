using System;
using System.Threading;
using System.Windows.Forms;

namespace ForbiddenWordsScanner
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            _mutex = new Mutex(true, "ForbiddenWordsScannerMutex", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("The application is already running!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (args.Length >= 2)
            {
                Console.WriteLine($"Scanning: {args[0]}");
                ScannerEngine engine = new ScannerEngine();
                engine.StartConsoleScan(args[0], args[1].Split(','));
                Console.WriteLine("Done.");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}