using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    public sealed class RpgmvpConverterForm : Form
    {
        private readonly Color formBack = Color.FromArgb(17, 19, 24);
        private readonly Color panelBack = Color.FromArgb(25, 29, 36);
        private readonly Color border = Color.FromArgb(50, 58, 70);
        private readonly Color textColor = Color.FromArgb(239, 243, 248);
        private readonly Color mutedColor = Color.FromArgb(155, 167, 181);
        private readonly Color accentColor = Color.FromArgb(68, 197, 255);
        private readonly Color successColor = Color.FromArgb(70, 204, 120);
        private readonly Color warningColor = Color.FromArgb(255, 183, 77);
        private readonly Color dangerColor = Color.FromArgb(255, 107, 107);
        private readonly Color inputBack = Color.FromArgb(13, 16, 21);
        private readonly Color logBack = Color.FromArgb(10, 12, 16);
        private readonly Color purpleColor = Color.FromArgb(156, 89, 208);
        private readonly Color pinkColor = Color.FromArgb(255, 105, 180);

        private TabControl tabControl;
        private ComboBox langCombo;

        private TextBox pathBox;
        private TextBox keyBox;
        private Button browseButton;
        private Button gameButton;
        private Button startButton;
        private Button pauseButton;
        private Button cancelButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label statsLabel;
        private TextBox logBox;

        private Button extractRpaButton;
        private ProgressBar rpaProgressBar;
        private Label rpaStatusLabel;
        private Label rpaStatsLabel;
        private TextBox rpaLogBox;

        private ComboBox unlockerModeBox;
        private Button unlockerButton;
        private TextBox unlockerLogBox;

        private System.Windows.Forms.Timer uiTimer;

        private ConversionRun currentRun;
        private RpaExtractionRun currentRpaRun;

        public RpgmvpConverterForm()
        {
            BuildUi();
            string initialRoot = TryFindGameRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(initialRoot))
            {
                pathBox.Text = initialRoot;
            }
            ApplyLocalization();
        }

        private void BuildUi()
        {
            Font uiFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            Font uiBold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            Font titleFont = new Font("Segoe UI Semibold", 13f, FontStyle.Regular);
            Font logFont = new Font("Consolas", 10f, FontStyle.Regular);

            Text = Loc.Get("app_title");
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(880, 680);
            Size = new Size(880, 680);
            BackColor = formBack;
            ForeColor = textColor;
            Font = uiFont;

            FormClosing += OnFormClosing;

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = panelBack
            };
            Controls.Add(header);

            Label titleLabel = new Label
            {
                Text = Loc.Get("app_title"),
                Font = titleFont,
                ForeColor = textColor,
                Location = new Point(18, 12),
                Size = new Size(320, 28)
            };
            titleLabel.Name = "titleLabel";
            header.Controls.Add(titleLabel);

            Label subtitleLabel = new Label
            {
                Text = Loc.Get("app_subtitle"),
                ForeColor = mutedColor,
                Location = new Point(19, 42),
                Size = new Size(600, 20)
            };
            subtitleLabel.Name = "subtitleLabel";
            header.Controls.Add(subtitleLabel);

            langCombo = new ComboBox
            {
                Location = new Point(720, 38),
                Size = new Size(130, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            langCombo.Items.Add("English");
            langCombo.Items.Add("Русский");
            langCombo.SelectedIndex = Loc.CurrentLanguage == "en" ? 0 : 1;
            langCombo.SelectedIndexChanged += OnLangChanged;
            header.Controls.Add(langCombo);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = formBack,
                ForeColor = textColor,
                Location = new Point(0, 70),
                Size = new Size(880, 610),
                Padding = new Point(12, 6)
            };

            TabPage tabRpgmvp = CreateRpgmvpTab(uiFont, uiBold, logFont);
            TabPage tabRpa = CreateRpaTab(uiFont, uiBold, logFont);
            TabPage tabUnlocker = CreateUnlockerTab(uiFont, uiBold, logFont);

            tabControl.TabPages.Add(tabRpgmvp);
            tabControl.TabPages.Add(tabRpa);
            tabControl.TabPages.Add(tabUnlocker);

            Controls.Add(tabControl);

            uiTimer = new System.Windows.Forms.Timer { Interval = 200 };
            uiTimer.Tick += delegate { UpdateUiFromRun(); };
        }

        private void OnLangChanged(object sender, EventArgs e)
        {
            Loc.SetLanguage(langCombo.SelectedIndex == 0 ? "en" : "ru");
            ApplyLocalization();
        }

        private TabPage CreateRpgmvpTab(Font uiFont, Font uiBold, Font logFont)
        {
            TabPage tab = new TabPage { BackColor = formBack, Name = "tabRpgmvp" };

            Label pathLabel = CreateLabel(Loc.Get("path_label"), new Point(18, 18), mutedColor);
            tab.Controls.Add(pathLabel);

            pathBox = CreateInputBox(new Point(18, 40), new Size(610, 26));
            tab.Controls.Add(pathBox);

            browseButton = CreateButton(Loc.Get("browse_btn"), new Point(640, 39), new Size(182, 28), accentColor, formBack, uiBold);
            browseButton.Click += delegate { BrowseFolder(); };
            tab.Controls.Add(browseButton);

            Label keyLabel = CreateLabel(Loc.Get("key_label"), new Point(18, 92), mutedColor);
            tab.Controls.Add(keyLabel);

            keyBox = CreateInputBox(new Point(18, 114), new Size(610, 26));
            tab.Controls.Add(keyBox);

            gameButton = CreateButton(Loc.Get("game_root_btn"), new Point(640, 113), new Size(182, 28), Color.FromArgb(22, 26, 32), textColor, uiBold);
            gameButton.Click += delegate { DetectGameRoot(); };
            tab.Controls.Add(gameButton);

            startButton = CreateButton(Loc.Get("start_btn"), new Point(18, 162), new Size(120, 34), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartConversionAsync(); };
            tab.Controls.Add(startButton);

            pauseButton = CreateButton(Loc.Get("pause_btn"), new Point(146, 162), new Size(120, 34), warningColor, Color.Black, uiBold);
            pauseButton.Enabled = false;
            pauseButton.Click += delegate { TogglePause(); };
            tab.Controls.Add(pauseButton);

            cancelButton = CreateButton(Loc.Get("cancel_btn"), new Point(274, 162), new Size(120, 34), dangerColor, textColor, uiBold);
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelConversion(); };
            tab.Controls.Add(cancelButton);

            progressBar = new ProgressBar
            {
                Location = new Point(18, 218),
                Size = new Size(804, 24),
                Style = ProgressBarStyle.Continuous
            };
            tab.Controls.Add(progressBar);

            statusLabel = CreateLabel(Loc.Get("status_waiting"), new Point(18, 250), textColor);
            statusLabel.Font = uiBold;
            statusLabel.Name = "statusLabel";
            tab.Controls.Add(statusLabel);

            statsLabel = CreateLabel(Loc.Get("stats_processed") + ": 0 / 0 | " + Loc.Get("stats_speed") + ": 0.00 " + Loc.Get("stats_fps") + " | " + Loc.Get("stats_eta") + ": --:--", new Point(18, 274), mutedColor);
            statsLabel.Name = "statsLabel";
            tab.Controls.Add(statsLabel);

            Panel logPanel = CreateLogPanel(logFont, "logPanel");
            tab.Controls.Add(logPanel);

            logBox = (TextBox)logPanel.Controls["logBox"];

            return tab;
        }

        private TabPage CreateRpaTab(Font uiFont, Font uiBold, Font logFont)
        {
            TabPage tab = new TabPage { BackColor = formBack, Name = "tabRpa" };

            Label rpaPathLabel = CreateLabel(Loc.Get("path_label"), new Point(18, 18), mutedColor);
            tab.Controls.Add(rpaPathLabel);

            TextBox rpaPathBox = CreateInputBox(new Point(18, 40), new Size(610, 26));
            tab.Controls.Add(rpaPathBox);

            Button rpaBrowseButton = CreateButton(Loc.Get("browse_btn"), new Point(640, 39), new Size(182, 28), accentColor, formBack, uiBold);
            rpaBrowseButton.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = Loc.Get("folder_dialog_title");
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        rpaPathBox.Text = dialog.SelectedPath;
                    }
                }
            };
            tab.Controls.Add(rpaBrowseButton);

            extractRpaButton = CreateButton(Loc.Get("rpa_extract_btn"), new Point(18, 88), new Size(150, 36), purpleColor, textColor, uiBold);
            extractRpaButton.Click += delegate { StartRpaExtraction(rpaPathBox.Text); };
            tab.Controls.Add(extractRpaButton);

            rpaProgressBar = new ProgressBar
            {
                Location = new Point(18, 146),
                Size = new Size(804, 24),
                Style = ProgressBarStyle.Continuous
            };
            tab.Controls.Add(rpaProgressBar);

            rpaStatusLabel = CreateLabel(Loc.Get("status_waiting"), new Point(18, 178), textColor);
            rpaStatusLabel.Font = uiBold;
            tab.Controls.Add(rpaStatusLabel);

            rpaStatsLabel = CreateLabel(Loc.Get("stats_processed") + ": 0 / 0", new Point(18, 202), mutedColor);
            tab.Controls.Add(rpaStatsLabel);

            Panel rpaLogPanel = CreateLogPanel(logFont, "rpaLogPanel");
            rpaLogPanel.Location = new Point(18, 238);
            tab.Controls.Add(rpaLogPanel);

            rpaLogBox = (TextBox)rpaLogPanel.Controls["logBox"];

            return tab;
        }

        private TabPage CreateUnlockerTab(Font uiFont, Font uiBold, Font logFont)
        {
            TabPage tab = new TabPage { BackColor = formBack, Name = "tabUnlocker" };

            Label unlockerPathLabel = CreateLabel(Loc.Get("path_label"), new Point(18, 18), mutedColor);
            tab.Controls.Add(unlockerPathLabel);

            TextBox unlockerPathBox = CreateInputBox(new Point(18, 40), new Size(610, 26));
            tab.Controls.Add(unlockerPathBox);

            Button unlockerBrowseButton = CreateButton(Loc.Get("browse_btn"), new Point(640, 39), new Size(182, 28), accentColor, formBack, uiBold);
            unlockerBrowseButton.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = Loc.Get("folder_dialog_title");
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        unlockerPathBox.Text = dialog.SelectedPath;
                    }
                }
            };
            tab.Controls.Add(unlockerBrowseButton);

            Label modeLabel = CreateLabel("Mode", new Point(18, 88), mutedColor);
            tab.Controls.Add(modeLabel);

            unlockerModeBox = new ComboBox
            {
                Location = new Point(18, 110),
                Size = new Size(120, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            unlockerModeBox.Items.Add(Loc.Get("unlocker_soft"));
            unlockerModeBox.Items.Add(Loc.Get("unlocker_hard"));
            unlockerModeBox.SelectedIndex = 0;
            tab.Controls.Add(unlockerModeBox);

            unlockerButton = CreateButton(Loc.Get("unlocker_btn"), new Point(18, 158), new Size(180, 40), pinkColor, formBack, uiBold);
            unlockerButton.Click += delegate { StartUnlocker(unlockerPathBox.Text); };
            tab.Controls.Add(unlockerButton);

            Panel unlockerLogPanel = CreateLogPanel(logFont, "unlockerLogPanel");
            unlockerLogPanel.Location = new Point(18, 218);
            tab.Controls.Add(unlockerLogPanel);

            unlockerLogBox = (TextBox)unlockerLogPanel.Controls["logBox"];

            return tab;
        }

        private Panel CreateLogPanel(Font logFont, string name)
        {
            Panel panel = new Panel
            {
                Location = new Point(18, 308),
                Size = new Size(804, 280),
                BackColor = logBack,
                BorderStyle = BorderStyle.FixedSingle,
                Name = name
            };

            Label logHeader = CreateLabel(Loc.Get("log_header"), new Point(10, 10), mutedColor);
            logHeader.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            logHeader.Name = "logHeader";
            panel.Controls.Add(logHeader);

            TextBox box = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new Point(10, 36),
                Size = new Size(782, 232),
                BackColor = logBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                Font = logFont,
                Name = "logBox"
            };
            panel.Controls.Add(box);

            return panel;
        }

        private void ApplyLocalization()
        {
            foreach (Control ctrl in Controls)
            {
                Panel header = ctrl as Panel;
                if (header != null && header.Dock == DockStyle.Top)
                {
                    foreach (Control hCtrl in header.Controls)
                    {
                        Label lbl = hCtrl as Label;
                        if (lbl != null && lbl.Name == "titleLabel")
                            lbl.Text = Loc.Get("app_title");
                        else
                        {
                            Label subLbl = hCtrl as Label;
                            if (subLbl != null && subLbl.Name == "subtitleLabel")
                                subLbl.Text = Loc.Get("app_subtitle");
                        }
                    }
                }
            }

            pauseButton.Text = (currentRun != null && currentRun.IsPaused) ? Loc.Get("resume_btn") : Loc.Get("pause_btn");
            unlockerModeBox.Items[0] = Loc.Get("unlocker_soft");
            unlockerModeBox.Items[1] = Loc.Get("unlocker_hard");

            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Name == "tabRpgmvp")
                {
                    foreach (Control ctrl in tab.Controls)
                    {
                        Label lbl = ctrl as Label;
                        if (lbl != null && lbl.Name == "statusLabel")
                            lbl.Text = currentRun == null ? Loc.Get("status_waiting") : lbl.Text;
                    }
                }
            }

            Refresh();
        }

        private Label CreateLabel(string text, Point location, Color color)
        {
            return new Label
            {
                Text = text,
                Location = location,
                Size = new Size(160, 20),
                ForeColor = color,
                BackColor = Color.Transparent
            };
        }

        private TextBox CreateInputBox(Point location, Size size)
        {
            return new TextBox
            {
                Location = location,
                Size = size,
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Button CreateButton(string text, Point location, Size size, Color backColor, Color foreColor, Font font)
        {
            Button button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = font,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = border;
            return button;
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = Loc.Get("folder_dialog_title");
                dialog.ShowNewFolderButton = true;

                if (Directory.Exists(pathBox.Text))
                {
                    dialog.SelectedPath = pathBox.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    pathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void DetectGameRoot()
        {
            string root = TryFindGameRoot(pathBox.Text);
            if (string.IsNullOrWhiteSpace(root))
            {
                WriteLog(logBox, Loc.Get("game_root_not_found"));
                return;
            }

            pathBox.Text = root;
            WriteLog(logBox, Loc.Get("game_root_detected") + ": " + root);
        }

        private async Task StartConversionAsync()
        {
            if (currentRun != null) return;

            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog(logBox, Loc.Get("msg_invalid_path"));
                return;
            }

            if (string.IsNullOrWhiteSpace(keyBox.Text))
            {
                string detectedKey = TryFindKey(rootPath);
                if (string.IsNullOrWhiteSpace(detectedKey))
                {
                    WriteLog(logBox, Loc.Get("msg_key_not_found"));
                    return;
                }
                keyBox.Text = detectedKey;
                WriteLog(logBox, Loc.Get("key_found") + ": " + detectedKey);
            }

            byte[] keyBytes;
            try
            {
                keyBytes = ParseKey(keyBox.Text);
            }
            catch
            {
                WriteLog(logBox, Loc.Get("invalid_key"));
                return;
            }

            if (keyBytes.Length < 16)
            {
                WriteLog(logBox, Loc.Get("msg_key_too_short"));
                return;
            }

            List<string> files = GetFilesToConvert(rootPath);
            if (files.Count == 0)
            {
                WriteLog(logBox, Loc.Get("msg_no_files"));
                return;
            }

            int workerCount = Math.Min(Math.Max(Environment.ProcessorCount, 2), 8);
            workerCount = Math.Min(workerCount, files.Count);

            currentRun = new ConversionRun(rootPath, files, keyBytes, workerCount);
            SetRunningState(true);

            progressBar.Maximum = files.Count;
            progressBar.Value = 0;
            statusLabel.Text = Loc.Get("status_preparing");
            statsLabel.Text = string.Format("{0}: 0 / {1} | {2}: 0.00 {3} | {4}: --:--", Loc.Get("stats_processed"), files.Count, Loc.Get("stats_speed"), Loc.Get("stats_fps"), Loc.Get("stats_eta"));
            pauseButton.Text = Loc.Get("pause_btn");

            WriteLog(logBox, string.Format("{0}: {1} | {2}: {3}", Loc.Get("msg_started"), files.Count, Loc.Get("msg_threads"), workerCount));
            WriteLog(logBox, Loc.Get("msg_skip_tilesets"));

            uiTimer.Start();
            currentRun.Start();

            try
            {
                await currentRun.Completion;
                FinishConversion(currentRun.CancelRequested);
            }
            catch (Exception ex)
            {
                WriteLog(logBox, Loc.Get("msg_errors") + ": " + ex.Message);
                FinishConversion(true);
            }
        }

        private void TogglePause()
        {
            if (currentRun == null) return;

            if (currentRun.IsPaused)
            {
                currentRun.Resume();
                pauseButton.Text = Loc.Get("pause_btn");
                pauseButton.BackColor = warningColor;
                pauseButton.ForeColor = Color.Black;
                WriteLog(logBox, Loc.Get("msg_resumed"));
            }
            else
            {
                currentRun.Pause();
                pauseButton.Text = Loc.Get("resume_btn");
                pauseButton.BackColor = accentColor;
                pauseButton.ForeColor = textColor;
                statusLabel.Text = Loc.Get("status_paused");
                WriteLog(logBox, Loc.Get("msg_pause_enabled"));
            }
        }

        private void CancelConversion()
        {
            if (currentRun != null)
            {
                currentRun.Cancel();
                cancelButton.Enabled = false;
                pauseButton.Enabled = false;
                statusLabel.Text = Loc.Get("status_stopping");
                WriteLog(logBox, Loc.Get("msg_cancel_requested"));
            }
            else if (currentRpaRun != null)
            {
                currentRpaRun.Cancel();
                cancelButton.Enabled = false;
                rpaStatusLabel.Text = Loc.Get("status_stopping");
                WriteLog(logBox, Loc.Get("msg_cancel_requested"));
            }
        }

        private void UpdateUiFromRun()
        {
            if (currentRun == null && currentRpaRun == null) return;

            if (currentRpaRun != null)
            {
                int processed = currentRpaRun.ProcessedCount;
                rpaProgressBar.Value = Math.Min(processed, rpaProgressBar.Maximum);
                rpaStatusLabel.Text = Loc.Get("rpa_extracting") + ": " + currentRpaRun.CurrentFile;
                rpaStatsLabel.Text = string.Format("{0}: {1} / {2}", Loc.Get("stats_processed"), processed, rpaProgressBar.Maximum);
                return;
            }

            if (currentRun != null)
            {
                int processed = Math.Min(currentRun.ProcessedCount, currentRun.TotalCount);
                progressBar.Value = Math.Min(processed, progressBar.Maximum);

                if (currentRun.CancelRequested)
                    statusLabel.Text = Loc.Get("status_stopping");
                else if (currentRun.IsPaused)
                    statusLabel.Text = Loc.Get("status_paused");
                else
                    statusLabel.Text = Loc.Get("status_processing") + ": " + (string.IsNullOrWhiteSpace(currentRun.CurrentFile) ? "..." : currentRun.CurrentFile);

                double elapsedSeconds = Math.Max((DateTime.UtcNow - currentRun.StartUtc).TotalSeconds, 0.001d);
                double speed = processed / elapsedSeconds;
                int remaining = currentRun.TotalCount - processed;
                string eta = speed > 0d && !currentRun.IsPaused ? FormatDuration(remaining / speed) : "--:--";
                statsLabel.Text = string.Format("{0}: {1} / {2} | {3}: {4:N2} {5} | {6}: {7}", Loc.Get("stats_processed"), processed, currentRun.TotalCount, Loc.Get("stats_speed"), speed, Loc.Get("stats_fps"), Loc.Get("stats_eta"), eta);
            }
        }

        private void FinishConversion(bool cancelled)
        {
            if (currentRun == null) return;

            ConversionRun finished = currentRun;
            currentRun = null;
            uiTimer.Stop();
            SetRunningState(false);

            int processed = Math.Min(finished.ProcessedCount, finished.TotalCount);
            progressBar.Value = Math.Min(processed, progressBar.Maximum);
            double elapsedSeconds = Math.Max((DateTime.UtcNow - finished.StartUtc).TotalSeconds, 0.001d);
            double speed = processed / elapsedSeconds;

            if (cancelled)
            {
                statusLabel.Text = Loc.Get("status_cancelled");
                statsLabel.Text = string.Format("{0}: {1} / {2} | {3}: {4:N2} {5}", Loc.Get("stats_processed"), processed, finished.TotalCount, Loc.Get("stats_speed"), speed, Loc.Get("stats_fps"));
                WriteLog(logBox, Loc.Get("msg_cancelled") + ": " + processed);
                return;
            }

            statusLabel.Text = Loc.Get("status_done");
            statsLabel.Text = string.Format("{0}: {1} / {2} | {3}: {4:N2} {5} | {6}: 00:00", Loc.Get("stats_processed"), processed, finished.TotalCount, Loc.Get("stats_speed"), speed, Loc.Get("stats_fps"), Loc.Get("stats_eta"));

            if (finished.ErrorCount > 0)
                WriteLog(logBox, string.Format("{0}: {1}. {2}: {3}", Loc.Get("msg_errors"), finished.ErrorCount, Loc.Get("msg_last_error"), finished.LastError));
            else
                WriteLog(logBox, string.Format("{0} {1} {2} {3}.", Loc.Get("msg_completed"), processed, Loc.Get("msg_files_processed"), FormatDuration(elapsedSeconds)));
        }

        private void SetRunningState(bool running)
        {
            pathBox.Enabled = !running;
            keyBox.Enabled = !running;
            browseButton.Enabled = !running;
            gameButton.Enabled = !running;
            startButton.Enabled = !running;
            pauseButton.Enabled = running;
            cancelButton.Enabled = running;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (currentRun == null && currentRpaRun == null) return;

            e.Cancel = true;
            MessageBox.Show(this, Loc.Get("close_warning_msg"), Loc.Get("close_warning_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WriteLog(TextBox target, string message)
        {
            if (target == null) return;
            if (target.TextLength > 0) target.AppendText(Environment.NewLine);
            target.AppendText(message);
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) return "--:--";
            TimeSpan span = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            if (span.TotalHours >= 1d)
                return string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
            return string.Format("{0:00}:{1:00}", span.Minutes, span.Seconds);
        }

        private static byte[] ParseKey(string input)
        {
            string key = input.Trim();
            if (key.Length % 2 != 0)
                throw new InvalidOperationException(Loc.Get("invalid_key"));

            byte[] bytes = new byte[key.Length / 2];
            for (int i = 0; i < key.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(key.Substring(i, 2), 16);
            return bytes;
        }

        private static string TryFindKey(string rootPath)
        {
            try
            {
                string systemJson = Directory.EnumerateFiles(rootPath, "System.json", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(systemJson)) return string.Empty;
                string json = File.ReadAllText(systemJson);
                Match match = Regex.Match(json, "\"encryptionKey\":\"([0-9a-fA-F]+)\"");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string TryFindGameRoot(string path)
        {
            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(path)) candidates.Add(path.Trim());
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir)) candidates.Add(baseDir);

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(candidate) && !File.Exists(candidate)) continue;

                DirectoryInfo current = Directory.Exists(candidate)
                    ? new DirectoryInfo(candidate)
                    : new FileInfo(candidate).Directory;

                while (current != null)
                {
                    bool hasWww = Directory.Exists(Path.Combine(current.FullName, "www"));
                    bool hasPackage = File.Exists(Path.Combine(current.FullName, "package.json"));
                    bool hasWwwData = Directory.Exists(Path.Combine(current.FullName, "www", "data"));
                    bool hasData = Directory.Exists(Path.Combine(current.FullName, "data"));
                    bool hasImg = Directory.Exists(Path.Combine(current.FullName, "img"));
                    bool hasGame = Directory.Exists(Path.Combine(current.FullName, "game"));
                    bool hasExe = Directory.EnumerateFiles(current.FullName, "*.exe", SearchOption.TopDirectoryOnly).Any();

                    if (string.Equals(current.Name, "Game", StringComparison.OrdinalIgnoreCase) ||
                        (hasWww && (hasPackage || hasWwwData || hasExe)) ||
                        (hasPackage && hasData && hasImg) ||
                        (hasGame && hasExe))
                        return current.FullName;

                    current = current.Parent;
                }
            }
            return null;
        }

        private static List<string> GetFilesToConvert(string rootPath)
        {
            string tilesetsPath1 = Path.Combine(rootPath, "www", "img", "tilesets");
            string weatherPath1 = Path.Combine(rootPath, "www", "img", "weather");
            string tilesetsPath2 = Path.Combine(rootPath, "img", "tilesets");
            string weatherPath2 = Path.Combine(rootPath, "img", "weather");

            return Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                    (path.EndsWith(".rpgmvp", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".png_", StringComparison.OrdinalIgnoreCase)) &&
                    !path.StartsWith(tilesetsPath1, StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith(weatherPath1, StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith(tilesetsPath2, StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith(weatherPath2, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void StartRpaExtraction(string rootPath)
        {
            if (currentRpaRun != null || currentRun != null)
            {
                WriteLog(rpaLogBox, Loc.Get("msg_invalid_path"));
                return;
            }

            if (!Directory.Exists(rootPath))
            {
                WriteLog(rpaLogBox, Loc.Get("msg_invalid_path"));
                return;
            }

            string[] rpaFiles = RpaExtractor.FindRpaFilesInGameFolder(rootPath);
            rpaFiles = rpaFiles.Where(f => !f.EndsWith("script.rpa", StringComparison.OrdinalIgnoreCase) &&
                                            !f.EndsWith("audio.rpa", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (rpaFiles.Length == 0)
            {
                WriteLog(rpaLogBox, Loc.Get("rpa_no_files"));
                return;
            }

            currentRpaRun = new RpaExtractionRun(rootPath, rpaFiles);

            rpaProgressBar.Maximum = rpaFiles.Length;
            rpaProgressBar.Value = 0;
            rpaStatusLabel.Text = Loc.Get("status_preparing");
            rpaStatsLabel.Text = string.Format("{0}: 0 / {1}", Loc.Get("stats_processed"), rpaFiles.Length);

            WriteLog(rpaLogBox, string.Format("{0}: {1}", Loc.Get("rpa_found"), rpaFiles.Length));
            foreach (string rpa in rpaFiles)
                WriteLog(rpaLogBox, "  - " + Path.GetFileName(rpa));

            uiTimer.Start();
            currentRpaRun.Start();

            Task.Run(async delegate
            {
                try
                {
                    await currentRpaRun.Completion;
                    FinishRpaExtraction();
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(delegate
                    {
                        WriteLog(rpaLogBox, Loc.Get("rpa_errors") + ": " + ex.Message);
                        FinishRpaExtraction();
                    }));
                }
            });
        }

        private void FinishRpaExtraction()
        {
            if (currentRpaRun == null) return;

            RpaExtractionRun finished = currentRpaRun;
            currentRpaRun = null;
            uiTimer.Stop();

            rpaProgressBar.Value = rpaProgressBar.Maximum;
            rpaStatusLabel.Text = Loc.Get("rpa_complete");

            if (finished.ErrorCount > 0)
            {
                WriteLog(rpaLogBox, string.Format("{0}: {1}", Loc.Get("rpa_errors"), finished.ErrorCount));
            }
            else
            {
                WriteLog(rpaLogBox, string.Format("{0} {1} {2}", Loc.Get("rpa_success"), finished.ExtractedCount, Loc.Get("stats_files")));
                if (finished.ExtractedCount == 0)
                    WriteLog(rpaLogBox, Loc.Get("rpa_try_external"));
            }
        }

        private void StartUnlocker(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                WriteLog(unlockerLogBox, Loc.Get("msg_invalid_path"));
                return;
            }

            string mode = unlockerModeBox.SelectedIndex == 0 ? "soft" : "hard";

            try
            {
                string modsPath = Path.Combine(rootPath, "game", "_mods");
                Directory.CreateDirectory(modsPath);

                string[] existingMods = Directory.GetDirectories(modsPath, "ZLZK_UGU_*");
                if (existingMods.Length > 0)
                {
                    WriteLog(unlockerLogBox, Loc.Get("unlocker_installed"));
                    MessageBox.Show(Loc.Get("unlocker_installed"), "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string modName = "ZLZK_UGU_" + mode;
                string destPath = Path.Combine(modsPath, modName);
                UnlockerResources.ExtractUnlocker(mode, destPath);

                if (Directory.Exists(destPath))
                {
                    WriteLog(unlockerLogBox, Loc.Get("unlocker_install_success"));
                    WriteLog(unlockerLogBox, Loc.Get("unlocker_install_path") + ": " + destPath);
                    MessageBox.Show(Loc.Get("unlocker_install_success") + "\n\n" + Loc.Get("unlocker_remove_note"), "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    WriteLog(unlockerLogBox, Loc.Get("unlocker_install_error"));
                }
            }
            catch (Exception ex)
            {
                WriteLog(unlockerLogBox, Loc.Get("unlocker_install_error") + ": " + ex.Message);
            }
        }

        private sealed class RpaExtractionRun
        {
            private readonly string[] rpaFiles;
            private readonly CancellationTokenSource cancellation;
            private int processedCount;
            private int errorCount;
            private int extractedCount;
            private string currentFile = string.Empty;

            public RpaExtractionRun(string rootPath, string[] rpaFiles)
            {
                this.rpaFiles = rpaFiles;
                cancellation = new CancellationTokenSource();
                Completion = Task.CompletedTask;
            }

            public int ProcessedCount { get { return processedCount; } }
            public int ErrorCount { get { return errorCount; } }
            public int ExtractedCount { get { return extractedCount; } }
            public string CurrentFile { get { return currentFile; } }
            public bool CancelRequested { get { return cancellation.IsCancellationRequested; } }
            public Task Completion { get; private set; }

            public void Start()
            {
                Completion = Task.Run(new Action(ProcessFiles), cancellation.Token);
            }

            public void Cancel()
            {
                cancellation.Cancel();
            }

            private void ProcessFiles()
            {
                string pythonExe = @"C:\Users\sanch\AppData\Local\Programs\Python\Python312\python.exe";
                if (!File.Exists(pythonExe)) pythonExe = "python";

                foreach (string rpaFile in rpaFiles)
                {
                    if (cancellation.IsCancellationRequested) break;

                    currentFile = Path.GetFileName(rpaFile);
                    try
                    {
                        string outputPath = Path.Combine(Path.GetDirectoryName(rpaFile), "extracted_" + Path.GetFileNameWithoutExtension(rpaFile));
                        Directory.CreateDirectory(outputPath);

                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = pythonExe;
                        psi.Arguments = "-m unrpa -mp \"" + outputPath + "\" \"" + rpaFile + "\"";
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        psi.RedirectStandardOutput = true;
                        psi.RedirectStandardError = true;

                        using (Process p = Process.Start(psi))
                        {
                            if (p != null)
                            {
                                p.StandardOutput.ReadToEnd();
                                p.StandardError.ReadToEnd();
                                p.WaitForExit(120000);
                                string[] existing = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
                                extractedCount += existing.Length;
                            }
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                    finally
                    {
                        Interlocked.Increment(ref processedCount);
                    }
                }
            }
        }

        private sealed class ConversionRun
        {
            private readonly ConcurrentQueue<string> queue;
            private readonly byte[] keyBytes;
            private readonly int workerCount;
            private readonly ManualResetEventSlim pauseGate;
            private readonly CancellationTokenSource cancellation;
            private int processedCount;
            private int errorCount;
            private string lastError = string.Empty;
            private string currentFile = string.Empty;

            public ConversionRun(string rootPath, List<string> files, byte[] keyBytes, int workerCount)
            {
                RootPath = rootPath;
                TotalCount = files.Count;
                this.keyBytes = keyBytes;
                this.workerCount = workerCount;
                queue = new ConcurrentQueue<string>(files);
                pauseGate = new ManualResetEventSlim(true);
                cancellation = new CancellationTokenSource();
                StartUtc = DateTime.UtcNow;
                Completion = Task.CompletedTask;
            }

            public string RootPath { get; private set; }
            public int TotalCount { get; private set; }
            public int ProcessedCount { get { return processedCount; } }
            public int ErrorCount { get { return errorCount; } }
            public string LastError { get { return lastError; } }
            public string CurrentFile { get { return currentFile; } }
            public bool IsPaused { get; private set; }
            public bool CancelRequested { get { return cancellation.IsCancellationRequested; } }
            public DateTime StartUtc { get; private set; }
            public Task Completion { get; private set; }

            public void Start()
            {
                Task[] workers = Enumerable.Range(0, workerCount).Select(delegate(int _)
                {
                    return Task.Run(new Action(ProcessQueue));
                }).ToArray();
                Completion = Task.WhenAll(workers);
            }

            public void Pause() { IsPaused = true; pauseGate.Reset(); }
            public void Resume() { IsPaused = false; pauseGate.Set(); }
            public void Cancel() { cancellation.Cancel(); pauseGate.Set(); }

            private void ProcessQueue()
            {
                while (!cancellation.IsCancellationRequested)
                {
                    pauseGate.Wait(cancellation.Token);

                    string filePath;
                    if (!queue.TryDequeue(out filePath)) break;

                    currentFile = filePath;
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(filePath);
                        if (bytes.Length <= 16) throw new InvalidDataException("File too short: " + filePath);

                        byte[] data = new byte[bytes.Length - 16];
                        Buffer.BlockCopy(bytes, 16, data, 0, data.Length);

                        for (int i = 0; i < 16; i++) data[i] = (byte)(data[i] ^ keyBytes[i]);

                        string outputPath = Path.ChangeExtension(filePath, ".png");
                        File.WriteAllBytes(outputPath, data);

                        if (File.Exists(outputPath)) File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref errorCount);
                        lastError = ex.Message;
                    }
                    finally
                    {
                        Interlocked.Increment(ref processedCount);
                    }
                }
            }
        }
    }
}
