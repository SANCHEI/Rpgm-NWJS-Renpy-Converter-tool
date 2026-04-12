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
        private readonly Color pinkColor = Color.FromArgb(255, 105, 180);

        private Label titleLabel;
        private Label subtitleLabel;
        private Button langButton;

        private Label rootLabel;
        private TextBox pathBox;
        private Label keyLabelInner;
        private TextBox keyBox;
        private Button browseButton;

        private Label unlockerLabel;
        private ComboBox unlockerModeBox;
        private Button unlockerButton;

        private Button startButton;
        private Button pauseButton;
        private Button cancelButton;

        private ProgressBar progressBar;
        private Label statusLabel;
        private Label statsLabel;
        private TextBox logBox;

        private System.Windows.Forms.Timer uiTimer;
        private ConversionRun currentRun;

        public RpgmvpConverterForm()
        {
            BuildUi();
            string initialRoot = TryFindGameRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(initialRoot))
            {
                pathBox.Text = initialRoot;
                TryAutoDetectKey(initialRoot);
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
            MinimumSize = new Size(900, 700);
            Size = new Size(900, 700);
            BackColor = formBack;
            ForeColor = textColor;
            Font = uiFont;

            FormClosing += OnFormClosing;

            Panel header = new Panel
            {
                Height = 68,
                BackColor = panelBack,
                Dock = DockStyle.Top
            };
            Controls.Add(header);

            titleLabel = new Label
            {
                Text = Loc.Get("app_title"),
                Font = titleFont,
                ForeColor = textColor,
                Location = new Point(18, 10),
                Size = new Size(500, 28),
                BackColor = Color.Transparent
            };
            header.Controls.Add(titleLabel);

            subtitleLabel = new Label
            {
                Text = Loc.Get("app_subtitle"),
                ForeColor = mutedColor,
                Location = new Point(19, 38),
                Size = new Size(500, 20),
                BackColor = Color.Transparent
            };
            header.Controls.Add(subtitleLabel);

            langButton = new Button
            {
                Text = Loc.CurrentLanguage == "en" ? "RU" : "EN",
                Location = new Point(800, 18),
                Size = new Size(70, 32),
                BackColor = accentColor,
                ForeColor = formBack,
                Font = uiBold,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            langButton.FlatAppearance.BorderSize = 0;
            langButton.Click += OnLangButtonClick;
            header.Controls.Add(langButton);

            int y = 100;

            rootLabel = new Label
            {
                Text = Loc.Get("root_label"),
                Location = new Point(18, y),
                Size = new Size(100, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
            };
            Controls.Add(rootLabel);

            y += 24;

            Panel pathPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(852, 34),
                BackColor = inputBack,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(4)
            };
            Controls.Add(pathPanel);

            pathBox = new TextBox
            {
                Location = new Point(8, 4),
                Size = new Size(660, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            pathBox.TextChanged += delegate(object sender, EventArgs e) { OnPathChanged(); };
            pathPanel.Controls.Add(pathBox);

            browseButton = CreateButton(Loc.Get("browse_btn"), new Point(676, 3), new Size(168, 28), accentColor, formBack, uiBold);
            browseButton.Click += delegate { BrowseFolder(); };
            pathPanel.Controls.Add(browseButton);

            y += 42;

            Panel keyPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(852, 34),
                BackColor = inputBack,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(4)
            };
            Controls.Add(keyPanel);

            keyLabelInner = new Label
            {
                Text = Loc.Get("key_label"),
                Location = new Point(8, 8),
                Size = new Size(80, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
            };
            keyPanel.Controls.Add(keyLabelInner);

            keyBox = new TextBox
            {
                Location = new Point(90, 4),
                Size = new Size(762, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            keyPanel.Controls.Add(keyBox);

            y += 38;

            unlockerLabel = new Label
            {
                Text = Loc.Get("unlocker_label"),
                Location = new Point(18, y),
                Size = new Size(300, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
            };
            Controls.Add(unlockerLabel);

            y += 24;

            unlockerModeBox = new ComboBox
            {
                Location = new Point(18, y),
                Size = new Size(100, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            unlockerModeBox.Items.Add(Loc.Get("unlocker_soft"));
            unlockerModeBox.Items.Add(Loc.Get("unlocker_hard"));
            unlockerModeBox.SelectedIndex = 0;
            Controls.Add(unlockerModeBox);

            unlockerButton = CreateButton(Loc.Get("unlocker_btn"), new Point(130, y - 3), new Size(160, 30), pinkColor, formBack, uiBold);
            unlockerButton.Click += delegate { StartUnlocker(); };
            Controls.Add(unlockerButton);

            y += 42;

            startButton = CreateButton(Loc.Get("start_btn"), new Point(18, y), new Size(100, 34), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartConversionAsync(); };
            Controls.Add(startButton);

            pauseButton = CreateButton(Loc.Get("pause_btn"), new Point(126, y), new Size(100, 34), warningColor, Color.Black, uiBold);
            pauseButton.Enabled = false;
            pauseButton.Click += delegate { TogglePause(); };
            Controls.Add(pauseButton);

            cancelButton = CreateButton(Loc.Get("cancel_btn"), new Point(234, y), new Size(100, 34), dangerColor, textColor, uiBold);
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelConversion(); };
            Controls.Add(cancelButton);

            y += 46;

            progressBar = new ProgressBar
            {
                Location = new Point(18, y),
                Size = new Size(852, 24),
                Style = ProgressBarStyle.Continuous
            };
            Controls.Add(progressBar);

            y += 32;

            statusLabel = new Label
            {
                Text = Loc.Get("status_waiting"),
                Location = new Point(18, y),
                Size = new Size(852, 20),
                ForeColor = textColor,
                BackColor = Color.Transparent,
                Font = uiBold
            };
            Controls.Add(statusLabel);

            y += 24;

            statsLabel = new Label
            {
                Text = Loc.Get("stats_processed") + ": 0 / 0 | " + Loc.Get("stats_speed") + ": 0.00 " + Loc.Get("stats_fps") + " | " + Loc.Get("stats_eta") + ": --:--",
                Location = new Point(18, y),
                Size = new Size(852, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
            };
            Controls.Add(statsLabel);

            y += 28;

            Panel logPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(852, 260),
                BackColor = logBack,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(logPanel);

            Label logHeader = new Label
            {
                Text = Loc.Get("log_header"),
                Location = new Point(10, 10),
                Size = new Size(100, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            logPanel.Controls.Add(logHeader);

            logBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new Point(10, 36),
                Size = new Size(830, 212),
                BackColor = logBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                Font = logFont
            };
            logPanel.Controls.Add(logBox);

            uiTimer = new System.Windows.Forms.Timer { Interval = 200 };
            uiTimer.Tick += delegate { UpdateUiFromRun(); };
        }

        private void OnLangButtonClick(object sender, EventArgs e)
        {
            string newLang = Loc.CurrentLanguage == "en" ? "ru" : "en";
            Loc.SetLanguage(newLang);
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            titleLabel.Text = Loc.Get("app_title");
            subtitleLabel.Text = Loc.Get("app_subtitle");
            langButton.Text = Loc.CurrentLanguage == "en" ? "RU" : "EN";

            rootLabel.Text = Loc.Get("root_label");
            browseButton.Text = Loc.Get("browse_btn");
            keyLabelInner.Text = Loc.Get("key_label");

            unlockerLabel.Text = Loc.Get("unlocker_label");
            unlockerButton.Text = Loc.Get("unlocker_btn");
            if (unlockerModeBox.Items.Count >= 2)
            {
                unlockerModeBox.Items[0] = Loc.Get("unlocker_soft");
                unlockerModeBox.Items[1] = Loc.Get("unlocker_hard");
            }

            startButton.Text = currentRun == null ? Loc.Get("start_btn") : startButton.Text;
            pauseButton.Text = currentRun != null && currentRun.IsPaused ? Loc.Get("resume_btn") : Loc.Get("pause_btn");
            cancelButton.Text = Loc.Get("cancel_btn");

            statusLabel.Text = currentRun == null ? Loc.Get("status_waiting") : statusLabel.Text;
            UpdateStatsLabel();

            Refresh();
        }

        private void UpdateStatsLabel()
        {
            if (currentRun == null)
            {
                statsLabel.Text = Loc.Get("stats_processed") + ": 0 / 0 | " + Loc.Get("stats_speed") + ": 0.00 " + Loc.Get("stats_fps") + " | " + Loc.Get("stats_eta") + ": --:--";
            }
        }

        private void TryAutoDetectKey(string path)
        {
            if (string.IsNullOrWhiteSpace(keyBox.Text))
            {
                string detectedKey = TryFindKey(path);
                if (!string.IsNullOrWhiteSpace(detectedKey))
                {
                    keyBox.Text = detectedKey;
                }
            }
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
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = Loc.Get("folder_dialog_title");
                dialog.CheckFileExists = false;
                dialog.CheckPathExists = true;
                dialog.FileName = "Select Folder";
                dialog.Filter = "Folders|*.folder";
                dialog.InitialDirectory = Directory.Exists(pathBox.Text) ? pathBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string folderPath = Path.GetDirectoryName(dialog.FileName);
                    if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    {
                        folderPath = dialog.FileName;
                    }
                    pathBox.Text = folderPath;
                    TryAutoDetectKey(folderPath);
                }
            }
        }

        private void OnPathChanged()
        {
            string path = pathBox.Text.Trim();
            if (Directory.Exists(path))
            {
                TryAutoDetectKey(path);
            }
        }

        private async Task StartConversionAsync()
        {
            if (currentRun != null) return;

            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog(Loc.Get("msg_invalid_path"));
                return;
            }

            if (string.IsNullOrWhiteSpace(keyBox.Text))
            {
                string detectedKey = TryFindKey(rootPath);
                if (string.IsNullOrWhiteSpace(detectedKey))
                {
                    WriteLog(Loc.Get("msg_key_not_found"));
                    return;
                }
                keyBox.Text = detectedKey;
                WriteLog(Loc.Get("key_found") + ": " + detectedKey);
            }

            byte[] keyBytes;
            try
            {
                keyBytes = ParseKey(keyBox.Text);
            }
            catch
            {
                WriteLog(Loc.Get("invalid_key"));
                return;
            }

            if (keyBytes.Length < 16)
            {
                WriteLog(Loc.Get("msg_key_too_short"));
                return;
            }

            List<string> files = GetFilesToConvert(rootPath);
            if (files.Count == 0)
            {
                WriteLog(Loc.Get("msg_no_files"));
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

            WriteLog(string.Format("{0}: {1} | {2}: {3}", Loc.Get("msg_started"), files.Count, Loc.Get("msg_threads"), workerCount));
            WriteLog(Loc.Get("msg_skip_tilesets"));

            uiTimer.Start();
            currentRun.Start();

            try
            {
                await currentRun.Completion;
                FinishConversion(currentRun.CancelRequested);
            }
            catch (Exception ex)
            {
                WriteLog(Loc.Get("msg_errors") + ": " + ex.Message);
                FinishConversion(true);
            }
        }

        private void StartUnlocker()
        {
            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog(Loc.Get("msg_invalid_path"));
                return;
            }

            if (!IsRpgmOrNwjsGame(rootPath))
            {
                WriteLog("Unlocker only works with RPGM/NWJS games.");
                MessageBox.Show("Unlocker only works with RPGM/NWJS games.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    WriteLog(Loc.Get("unlocker_installed"));
                    MessageBox.Show(Loc.Get("unlocker_installed"), "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string modName = "ZLZK_UGU_" + mode;
                string destPath = Path.Combine(modsPath, modName);
                UnlockerResources.ExtractUnlocker(mode, destPath);

                if (Directory.Exists(destPath))
                {
                    WriteLog(Loc.Get("unlocker_install_success"));
                    WriteLog(Loc.Get("unlocker_install_path") + ": " + destPath);
                    MessageBox.Show(Loc.Get("unlocker_install_success") + "\n\n" + Loc.Get("unlocker_remove_note"), "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    WriteLog(Loc.Get("unlocker_install_error"));
                }
            }
            catch (Exception ex)
            {
                WriteLog(Loc.Get("unlocker_install_error") + ": " + ex.Message);
            }
        }

        private static bool IsRpgmOrNwjsGame(string rootPath)
        {
            bool hasGame = Directory.Exists(Path.Combine(rootPath, "game"));
            bool hasExe = Directory.EnumerateFiles(rootPath, "*.exe", SearchOption.TopDirectoryOnly).Any();
            bool hasPackage = File.Exists(Path.Combine(rootPath, "package.json"));
            bool hasWww = Directory.Exists(Path.Combine(rootPath, "www"));
            bool hasData = Directory.Exists(Path.Combine(rootPath, "data")) || Directory.Exists(Path.Combine(rootPath, "www", "data"));

            return (hasGame && hasExe) || (hasPackage && hasWww) || (hasWww && hasData);
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
                WriteLog(Loc.Get("msg_resumed"));
            }
            else
            {
                currentRun.Pause();
                pauseButton.Text = Loc.Get("resume_btn");
                pauseButton.BackColor = accentColor;
                pauseButton.ForeColor = textColor;
                statusLabel.Text = Loc.Get("status_paused");
                WriteLog(Loc.Get("msg_pause_enabled"));
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
                WriteLog(Loc.Get("msg_cancel_requested"));
            }
        }

        private void UpdateUiFromRun()
        {
            if (currentRun == null) return;

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
                WriteLog(Loc.Get("msg_cancelled") + ": " + processed);
                return;
            }

            statusLabel.Text = Loc.Get("status_done");
            statsLabel.Text = string.Format("{0}: {1} / {2} | {3}: {4:N2} {5} | {6}: 00:00", Loc.Get("stats_processed"), processed, finished.TotalCount, Loc.Get("stats_speed"), speed, Loc.Get("stats_fps"), Loc.Get("stats_eta"));

            if (finished.ErrorCount > 0)
                WriteLog(string.Format("{0}: {1}. {2}: {3}", Loc.Get("msg_errors"), finished.ErrorCount, Loc.Get("msg_last_error"), finished.LastError));
            else
                WriteLog(string.Format("{0} {1} {2} {3}.", Loc.Get("msg_completed"), processed, Loc.Get("msg_files_processed"), FormatDuration(elapsedSeconds)));
        }

        private void SetRunningState(bool running)
        {
            pathBox.Enabled = !running;
            keyBox.Enabled = !running;
            browseButton.Enabled = !running;
            startButton.Enabled = !running;
            pauseButton.Enabled = running;
            cancelButton.Enabled = running;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (currentRun == null) return;

            e.Cancel = true;
            MessageBox.Show(this, Loc.Get("close_warning_msg"), Loc.Get("close_warning_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WriteLog(string message)
        {
            if (logBox.TextLength > 0) logBox.AppendText(Environment.NewLine);
            logBox.AppendText(message);
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

        private sealed class UnlockerRun
        {
            public bool CancelRequested { get; private set; }
            public void Cancel() { CancelRequested = true; }
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
