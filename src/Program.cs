using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace UmcVolume
{
    static class Program
    {
        public const string ShowEventName = @"Local\UmcVolume.Show";
        public const string ExitEventName = @"Local\UmcVolume.Exit";

        [STAThread]
        static void Main(string[] args)
        {
            // Started from the Windows "Run" key: stay in the tray. Started by hand: open the window.
            bool trayOnly = Array.Exists(args, a => string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase));

            // "/exit": ask a running instance to shut down cleanly (used by installers / uninstallers).
            if (Array.Exists(args, a => string.Equals(a, "/exit", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using (var ev = EventWaitHandle.OpenExisting(ExitEventName)) ev.Set();
                }
                catch { }
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, @"Local\UmcVolume.SingleInstance", out created))
            {
                if (!created)
                {
                    // Already running: ask that instance to show its window.
                    if (!trayOnly)
                    {
                        try
                        {
                            using (var ev = EventWaitHandle.OpenExisting(ShowEventName)) ev.Set();
                        }
                        catch { }
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log(e.Exception.ToString()); };

                try
                {
                    Application.Run(new TrayApp(!trayOnly));
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                    MessageBox.Show(ex.Message, "UmcVolume", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static void Log(string message)
        {
            try
            {
                string path = Path.Combine(Settings.DataDir, "umcvolume.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
