using System;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
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
