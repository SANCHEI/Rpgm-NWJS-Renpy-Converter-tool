using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal sealed class UnityDecensorDialog : Form
    {
        private readonly string gameRoot;
        private readonly UnityDecensorEnvironment environment;
        private readonly Label status;
        private readonly ListBox packages;
        private readonly Button installRecommended;
        private readonly Button installSw;
        private readonly Button remove;
        private List<BepInExPackage> latestPackages = new List<BepInExPackage>();

        public UnityDecensorDialog(string gameRoot)
        {
            this.gameRoot = gameRoot;
            environment = UnityDecensorInstaller.DetectEnvironment(gameRoot);
            Text = "Unity Decensor";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 410);
            BackColor = Color.FromArgb(17, 19, 24);
            ForeColor = Color.FromArgb(239, 243, 248);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ApplicationIcon.Apply(this);

            Controls.Add(new Label
            {
                Text = "Detected environment: " + environment.DisplayName,
                Location = new Point(18, 16),
                Size = new Size(680, 22),
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.FromArgb(68, 197, 255)
            });
            Controls.Add(new Label
            {
                Text = "SW_Decensor variant: " + environment.SwDecensorVariant,
                Location = new Point(18, 43),
                Size = new Size(680, 20),
                ForeColor = Color.FromArgb(155, 167, 181)
            });
            Controls.Add(new Label
            {
                Text = "Latest Unity packages from the official BepInEx Bleeding Edge builds:",
                Location = new Point(18, 78),
                Size = new Size(680, 20)
            });
            packages = new ListBox
            {
                Location = new Point(18, 102),
                Size = new Size(684, 132),
                BackColor = Color.FromArgb(13, 16, 21),
                ForeColor = Color.FromArgb(239, 243, 248),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(packages);
            installRecommended = CreateButton("Install Recommended BepInEx", 18, 254, 220);
            installRecommended.Enabled = false;
            installRecommended.Click += async delegate { await InstallRecommendedAsync(); };
            Controls.Add(installRecommended);
            installSw = CreateButton("Install Built-in SW_Decensor", 250, 254, 210);
            installSw.Click += delegate { InstallBuiltInSwDecensor(); };
            Controls.Add(installSw);
            remove = CreateButton("Remove Managed Files", 472, 254, 180);
            remove.Click += delegate { RemoveManagedFiles(); };
            Controls.Add(remove);
            status = new Label
            {
                Text = "Loading latest BepInEx package list...",
                Location = new Point(18, 302),
                Size = new Size(684, 72),
                ForeColor = Color.FromArgb(155, 167, 181)
            };
            Controls.Add(status);
            Shown += async delegate { await RefreshPackagesAsync(); };
            UpdateButtons();
        }

        private async Task RefreshPackagesAsync()
        {
            try
            {
                latestPackages = await Task.Run(delegate { return UnityDecensorInstaller.FetchLatestPackages(); });
                packages.Items.Clear();
                foreach (BepInExPackage package in latestPackages)
                    packages.Items.Add(package.DisplayName);
                BepInExPackage recommended = UnityDecensorInstaller.FindRecommendedPackage(latestPackages, environment);
                status.Text = recommended == null
                    ? "Compatible package was not found. You can still install the built-in SW_Decensor for an existing BepInEx setup."
                    : "Recommended: " + recommended.DisplayName;
            }
            catch (Exception ex)
            {
                status.Text = "Could not load online package list: " + ex.Message;
            }
            UpdateButtons();
        }

        private async Task InstallRecommendedAsync()
        {
            BepInExPackage package = UnityDecensorInstaller.FindRecommendedPackage(latestPackages, environment);
            if (package == null) return;
            if (!string.IsNullOrWhiteSpace(environment.ExistingBepInEx)
                && !UnityDecensorInstaller.IsManagedInstallPresent(gameRoot))
            {
                status.Text = "An existing unmanaged BepInEx setup was found. It was left unchanged; install SW_Decensor into that setup or update it manually.";
                return;
            }
            if (!string.IsNullOrWhiteSpace(environment.ExistingBepInEx)
                && MessageBox.Show("Update the BepInEx files previously installed by Game Asset Tool?", "Unity Decensor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                SetBusy(true, "Downloading and installing " + package.DisplayName + "...");
                await Task.Run(delegate { UnityDecensorInstaller.InstallBepInExPackage(gameRoot, package); });
                status.Text = "BepInEx installed. Install the built-in SW_Decensor " + environment.SwDecensorVariant + " plugin.";
            }
            catch (Exception ex)
            {
                status.Text = "BepInEx installation failed: " + ex.Message;
            }
            finally
            {
                SetBusy(false, status.Text);
            }
        }

        private void InstallBuiltInSwDecensor()
        {
            try
            {
                string selected = UnityDecensorInstaller.InstallEmbeddedSwDecensor(gameRoot);
                status.Text = "Installed built-in SW_Decensor " + environment.SwDecensorVariant + ": " + selected;
            }
            catch (Exception ex)
            {
                status.Text = "SW_Decensor installation failed: " + ex.Message;
            }
            UpdateButtons();
        }

        private void RemoveManagedFiles()
        {
            if (!UnityDecensorInstaller.IsManagedInstallPresent(gameRoot)) return;
            if (MessageBox.Show("Remove only files installed by Game Asset Tool?", "Unity Decensor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            UnityDecensorInstaller.RemoveManagedInstall(gameRoot);
            status.Text = "Managed Unity decensor files removed.";
            UpdateButtons();
        }

        private void SetBusy(bool busy, string message)
        {
            status.Text = message;
            installRecommended.Enabled = !busy && UnityDecensorInstaller.FindRecommendedPackage(latestPackages, environment) != null;
            installSw.Enabled = !busy;
            remove.Enabled = !busy && UnityDecensorInstaller.IsManagedInstallPresent(gameRoot);
        }

        private void UpdateButtons()
        {
            SetBusy(false, status == null ? "" : status.Text);
        }

        private static Button CreateButton(string text, int x, int y, int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 32),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.FromArgb(239, 243, 248),
                FlatStyle = FlatStyle.Flat
            };
        }
    }
}
