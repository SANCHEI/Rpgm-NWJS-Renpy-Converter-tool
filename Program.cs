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
        private static void Main()
        {
            try { SetProcessDPIAware(); }
            catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new RpgmvpConverterForm());
            }
            finally
            {
                PortableRuntime.Cleanup();
            }
        }
    }
}
