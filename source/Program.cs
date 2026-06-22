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
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                ApplicationErrorLog.Write(e.Exception);
                MessageBox.Show(
                    "Game Asset Tool hit an unexpected error.\n\nA diagnostic log was written to:\n"
                    + ApplicationErrorLog.LogPath,
                    "Game Asset Tool",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ApplicationErrorLog.Write(e.ExceptionObject as Exception);
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string startupPath = args != null && args.Length > 0 ? args[0] : null;
                Application.Run(new RpgmvpConverterForm(startupPath));
            }
            catch (Exception ex)
            {
                ApplicationErrorLog.Write(ex);
                MessageBox.Show(
                    "Game Asset Tool could not continue.\n\nA diagnostic log was written to:\n"
                    + ApplicationErrorLog.LogPath,
                    "Game Asset Tool",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                PortableRuntime.Cleanup();
                ToolRuntime.Cleanup();
            }
        }
    }
}
