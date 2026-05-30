using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            try { SetProcessDPIAware(); }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string startupPath = args != null && args.Length > 0 ? args[0] : null;
                Application.Run(new RpgmvpConverterForm(startupPath));
            }
            finally
            {
                PortableRuntime.Cleanup();
                ToolRuntime.Cleanup();
            }
        }
    }
}
