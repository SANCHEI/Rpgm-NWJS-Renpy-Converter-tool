using System;
using System.Drawing;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal static class ApplicationIcon
    {
        public static void Apply(Form form)
        {
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (icon != null) form.Icon = (Icon)icon.Clone();
                }
            }
            catch { }
        }
    }
}
