using System.Drawing;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal sealed class DiagnosticsDialog : Form
    {
        public DiagnosticsDialog(string text, bool russian)
        {
            Text = russian ? "Диагностика" : "Diagnostics";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 520);
            MinimumSize = new Size(560, 360);
            BackColor = Color.FromArgb(17, 19, 24);
            ForeColor = Color.FromArgb(239, 243, 248);
            ApplicationIcon.Apply(this);

            TextBox box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(10, 12, 16),
                ForeColor = Color.FromArgb(239, 243, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f),
                Text = text ?? "",
                TabStop = false,
                HideSelection = true
            };
            Controls.Add(box);

            Shown += delegate
            {
                box.SelectionStart = 0;
                box.SelectionLength = 0;
            };
        }
    }
}
