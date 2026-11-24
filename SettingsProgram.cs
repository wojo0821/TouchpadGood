using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TouchpadGood
{
    internal static class SettingsProgram
    {
        public static readonly int WM_SHOW_SETTINGS = RegisterWindowMessage("WM_TOUCHPADGOOD_SHOW_SETTINGS");

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            try
            {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
                    SetProcessDPIAware();
            }
            catch
            {
                try { SetProcessDPIAware(); } catch { }
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "TouchpadGood_Settings_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    PostMessage(new IntPtr(0xffff), WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SettingsForm());
            }
        }
    }
}
