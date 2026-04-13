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

        private Label rootLabel;
        private TextBox pathBox;
        private Label keyLabelInner;
        private TextBox keyBox;
        private Label engineRpgmLabel;
        private Label engineNwjsLabel;
        private Button browseButton;

        private Label unlockerLabel;
        private ComboBox unlockerModeBox;
        private Button unlockerButton;

        private Label unityLabel;
        private ComboBox unityExtractModeBox;
        private Button unityExtractButton;

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
            
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(exeDir, "app.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }
            
            string initialRoot = TryFindGameRoot(exeDir);
            if (!string.IsNullOrWhiteSpace(initialRoot))
            {
                pathBox.Text = initialRoot;
                TryAutoDetectKey(initialRoot);
            }
        }

        private void BuildUi()
        {
            Font uiFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            Font uiBold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            Font titleFont = new Font("Segoe UI Semibold", 13f, FontStyle.Regular);
            Font logFont = new Font("Consolas", 10f, FontStyle.Regular);

            Text = "Game Asset Tool";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 850);
            Size = new Size(900, 850);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
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
                Text = "Game Asset Tool",
                Font = titleFont,
                ForeColor = textColor,
                Location = new Point(18, 10),
                Size = new Size(500, 28),
                BackColor = Color.Transparent
            };
            header.Controls.Add(titleLabel);

            subtitleLabel = new Label
            {
                Text = "Convert, Extract, Unlock",
                ForeColor = mutedColor,
                Location = new Point(19, 38),
                Size = new Size(500, 20),
                BackColor = Color.Transparent
            };
            header.Controls.Add(subtitleLabel);

            int y = 100;

            rootLabel = new Label
            {
                Text = "Game Root",
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

            browseButton = CreateButton("...", new Point(676, 3), new Size(168, 28), accentColor, formBack, uiBold);
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
                Text = "HEX key",
                Location = new Point(8, 8),
                Size = new Size(70, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
            };
            keyPanel.Controls.Add(keyLabelInner);

            keyBox = new TextBox
            {
                Location = new Point(80, 4),
                Size = new Size(520, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            keyPanel.Controls.Add(keyBox);

            engineRpgmLabel = new Label
            {
                Text = "RPGM",
                Location = new Point(610, 8),
                Size = new Size(45, 20),
                ForeColor = Color.FromArgb(68, 197, 255),
                BackColor = Color.Transparent,
                Font = uiBold
            };
            keyPanel.Controls.Add(engineRpgmLabel);

            engineNwjsLabel = new Label
            {
                Text = "NWJS",
                Location = new Point(660, 8),
                Size = new Size(45, 20),
                ForeColor = Color.FromArgb(255, 105, 180),
                BackColor = Color.Transparent,
                Font = uiBold
            };
            keyPanel.Controls.Add(engineNwjsLabel);

            startButton = CreateButton("Start", new Point(715, 3), new Size(125, 28), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartConversionAsync(); };
            keyPanel.Controls.Add(startButton);

            y += 38;

            unlockerLabel = new Label
            {
                Text = "Gallery Unlocker",
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
            unlockerModeBox.Items.Add("Soft");
            unlockerModeBox.Items.Add("Hard");
            unlockerModeBox.SelectedIndex = 0;
            Controls.Add(unlockerModeBox);

            unlockerButton = CreateButton("Unlock", new Point(130, y - 3), new Size(160, 30), pinkColor, formBack, uiBold);
            unlockerButton.Click += delegate { StartUnlocker(); };
            Controls.Add(unlockerButton);

            y += 42;

            unityLabel = new Label
            {
                Text = "Unity Extractor",
                Location = new Point(18, y),
                Size = new Size(300, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
            };
            Controls.Add(unityLabel);

            y += 24;

            unityExtractModeBox = new ComboBox
            {
                Location = new Point(18, y),
                Size = new Size(150, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            unityExtractModeBox.Items.Add("Textures");
            unityExtractModeBox.Items.Add("Videos");
            unityExtractModeBox.Items.Add("All");
            unityExtractModeBox.SelectedIndex = 0;
            Controls.Add(unityExtractModeBox);

            unityExtractButton = CreateButton("Extract", new Point(180, y - 3), new Size(160, 30), Color.FromArgb(138, 98, 255), formBack, uiBold);
            unityExtractButton.Click += delegate { StartUnityExtraction(); };
            Controls.Add(unityExtractButton);

            y += 42;

            pauseButton = CreateButton("Pause", new Point(18, y), new Size(100, 30), warningColor, Color.Black, uiBold);
            pauseButton.Enabled = false;
            pauseButton.Click += delegate { TogglePause(); };
            Controls.Add(pauseButton);

            cancelButton = CreateButton("Cancel", new Point(126, y), new Size(100, 30), dangerColor, textColor, uiBold);
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelConversion(); };
            Controls.Add(cancelButton);

            y += 38;

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
                Text = "Waiting to start",
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
                Text = "Processed: 0 / 0 | Speed: 0.00 f/s | ETA: --:--",
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
                Size = new Size(852, 330),
                BackColor = logBack,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(logPanel);

            Label logHeader = new Label
            {
                Text = "Log",
                Location = new Point(10, 10),
                Size = new Size(60, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            logPanel.Controls.Add(logHeader);

            Button clearLogButton = new Button
            {
                Text = "Clear",
                Location = new Point(75, 8),
                Size = new Size(80, 24),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                Font = uiFont
            };
            clearLogButton.FlatAppearance.BorderSize = 0;
            clearLogButton.Click += delegate { logBox.Clear(); };
            logPanel.Controls.Add(clearLogButton);

            logBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new Point(10, 36),
                Size = new Size(830, 282),
                BackColor = logBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                Font = logFont
            };
            logBox.TextChanged += delegate(object sender, EventArgs e) 
            { 
                logBox.SelectionStart = logBox.Text.Length; 
                logBox.ScrollToCaret(); 
            };
            logBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    logBox.SelectAll();
                    e.SuppressKeyPress = true;
                }
                else if (e.Control && e.KeyCode == Keys.C)
                {
                    if (logBox.SelectionLength > 0)
                    {
                        Clipboard.SetText(logBox.SelectedText);
                    }
                    e.SuppressKeyPress = true;
                }
            };
            logPanel.Controls.Add(logBox);

            uiTimer = new System.Windows.Forms.Timer { Interval = 200 };
            uiTimer.Tick += delegate { UpdateUiFromRun(); };
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
                dialog.Title = "Select Game Folder";
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
                WriteLog("Invalid path");
                return;
            }

            if (IsRenpyGame(rootPath))
            {
                if (!IsPythonInstalled())
                {
                    var result = MessageBox.Show("Python is required for Renpy extraction.\n\nWould you like to download it now?", "Python Required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        InstallPython();
                    }
                    return;
                }
                WriteLog("Renpy game detected - starting file extraction");
                Task.Run(delegate { RunRenpyExtraction(rootPath); });
                return;
            }

            if (IsUnityGame(rootPath))
            {
                WriteLog("Unity game detected - use Unity Extractor");
                MessageBox.Show("Unity game detected.\n\nPlease use the 'Extract' button for Unity extraction.", "Game Asset Tool", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(keyBox.Text))
            {
                string detectedKey = TryFindKey(rootPath);
                if (string.IsNullOrWhiteSpace(detectedKey))
                {
                    WriteLog("Key not found");
                    return;
                }
                keyBox.Text = detectedKey;
                WriteLog("Key found" + ": " + detectedKey);
            }

            byte[] keyBytes;
            try
            {
                keyBytes = ParseKey(keyBox.Text);
            }
            catch
            {
                WriteLog("Invalid key format");
                return;
            }

            if (keyBytes.Length < 16)
            {
                WriteLog("Key too short (min 16 bytes)");
                return;
            }

            List<string> files = GetFilesToConvert(rootPath);
            if (files.Count == 0)
            {
                WriteLog("No .rpgmvp/.png_ files found");
                return;
            }

            int workerCount = Math.Min(Math.Max(Environment.ProcessorCount, 2), 8);
            workerCount = Math.Min(workerCount, files.Count);

            currentRun = new ConversionRun(rootPath, files, keyBytes, workerCount);
            SetRunningState(true);

            progressBar.Maximum = files.Count;
            progressBar.Value = 0;
            statusLabel.Text = "Preparing...";
            statsLabel.Text = string.Format("Processed: 0 / {0} | Speed: 0.00 f/s | ETA: --:--", files.Count);
            pauseButton.Text = "Pause";

            WriteLog(string.Format("Started: {0} | threads: {1}", files.Count, workerCount));
            WriteLog("Skipping: img/tilesets, img/weather");

            uiTimer.Start();
            currentRun.Start();

            try
            {
                await currentRun.Completion;
                FinishConversion(currentRun.CancelRequested);
            }
            catch (Exception ex)
            {
                WriteLog("Errors" + ": " + ex.Message);
                FinishConversion(true);
            }
        }

        private void StartUnlocker()
        {
            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
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
                    WriteLog("Unlocker already installed");
                    MessageBox.Show("Unlocker already installed", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string modName = "ZLZK_UGU_" + mode;
                string destPath = Path.Combine(modsPath, modName);
                UnlockerResources.ExtractUnlocker(mode, destPath);

                if (Directory.Exists(destPath))
                {
                    WriteLog("Unlocker installed successfully");
                    WriteLog("Installed to" + ": " + destPath);
                    MessageBox.Show("Unlocker installed successfully" + "\n\n" + "Run game to activate. Remove for uninstall.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    WriteLog("Installation error");
                }
            }
            catch (Exception ex)
            {
                    WriteLog("Installation error" + ": " + ex.Message);
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

        private void StartUnityExtraction()
        {
            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }

            if (!IsUnityGame(rootPath))
            {
                WriteLog("Unity extraction: No Unity game found.");
                MessageBox.Show("Unity extraction: No Unity game found.\n\nLook for folders with *_Data, StreamingAssets, or UnityPlayer.dll", "Unity Extractor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsPythonInstalled())
            {
                var result = MessageBox.Show("Python is required for Unity extraction.\n\nWould you like to download it now?", "Python Required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    InstallPython();
                    return;
                }
                else
                {
                    return;
                }
            }

            if (!CheckUnityPyInstalled())
            {
                var result = MessageBox.Show("UnityPy is required for Unity extraction.\n\nWould you like to install it now?", "UnityPy Required", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    InstallUnityPy();
                    if (!CheckUnityPyInstalled())
                    {
                        WriteLog("UnityPy installation failed");
                        MessageBox.Show("Failed to install UnityPy.\n\nPlease run: pip install UnityPy", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            string extractMode = unityExtractModeBox.SelectedIndex == 0 ? "textures" : (unityExtractModeBox.SelectedIndex == 1 ? "videos" : "all");
            WriteLog("Unity extraction started: " + extractMode);

            Task.Run(delegate { RunUnityExtraction(rootPath, extractMode); });
        }

        private static bool IsPythonInstalled()
        {
            string[] pythonPaths = { "python", "python3", @"C:\Python312\python.exe", @"C:\Program Files\Python312\python.exe", @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python312\python.exe" };

            foreach (string pyPath in pythonPaths)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = pyPath;
                    psi.Arguments = "--version";
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;

                    using (Process process = Process.Start(psi))
                    {
                        process.WaitForExit();
                        if (process.ExitCode == 0) return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private void InstallPython()
        {
            try
            {
                WriteLog("Downloading Python...");
                statusLabel.Text = "Downloading Python...";

                string tempDir = Path.Combine(Path.GetTempPath(), "GameAssetTool");
                Directory.CreateDirectory(tempDir);
                string installerPath = Path.Combine(tempDir, "python-installer.exe");

                using (var client = new System.Net.WebClient())
                {
                    client.DownloadFile("https://www.python.org/ftp/python/3.12.0/python-3.12.0-amd64.exe", installerPath);
                }

                WriteLog("Installing Python silently...");
                statusLabel.Text = "Installing Python...";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = installerPath;
                psi.Arguments = "/quiet InstallAllUsers=1 PrependPath=1";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();
                }

                WriteLog("Python installed. Please restart the application.");
                MessageBox.Show("Python has been installed.\n\nPlease CLOSE and REOPEN this application.", "Python Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to install Python automatically.\n\nPlease install Python manually:\nhttps://www.python.org/downloads/", "Python Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool CheckUnityPyInstalled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = "-c \"import UnityPy; print('ok')\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Trim().Contains("ok");
                }
            }
            catch
            {
                return false;
            }
        }

        private void InstallUnityPy()
        {
            try
            {
                WriteLog("Installing UnityPy...");
                statusLabel.Text = "Installing UnityPy...";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = "-m pip install UnityPy";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                psi.StandardErrorEncoding = System.Text.Encoding.UTF8;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        WriteLog("UnityPy installed successfully");
                    }
                    else
                    {
                        WriteLog("UnityPy install error: " + error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Install error: " + ex.Message);
            }
        }

        private static bool IsUnityGame(string rootPath)
        {
            bool hasDataFolder = Directory.Exists(Path.Combine(rootPath, "*_Data"));
            bool hasStreamingAssets = Directory.Exists(Path.Combine(rootPath, "StreamingAssets"));
            bool hasUnityPlayer = File.Exists(Path.Combine(rootPath, "UnityPlayer.dll"));
            bool hasManaged = Directory.Exists(Path.Combine(rootPath, "Managed"));

            return hasStreamingAssets || hasUnityPlayer || (hasManaged && hasDataFolder);
        }

        private static bool IsRenpyGame(string rootPath)
        {
            string gameFolder = Path.Combine(rootPath, "game");
            
            bool hasGameFolder = Directory.Exists(gameFolder);
            if (!hasGameFolder) return false;

            bool hasRpa = Directory.GetFiles(gameFolder, "*.rpa", SearchOption.TopDirectoryOnly).Length > 0;
            bool hasRpyc = Directory.GetFiles(gameFolder, "*.rpyc", SearchOption.TopDirectoryOnly).Length > 0;
            bool hasRpy = Directory.GetFiles(gameFolder, "*.rpy", SearchOption.AllDirectories).Length > 0;
            bool hasRenpyExe = File.Exists(Path.Combine(rootPath, "renpy.exe"));
            bool hasGameExe = File.Exists(Path.Combine(rootPath, "game.exe"));
            bool hasLauncherScript = File.Exists(Path.Combine(rootPath, "launcher.sh")) || File.Exists(Path.Combine(rootPath, "game", "launcher.py"));

            return hasRpa || hasRpyc || hasRpy || hasRenpyExe || hasGameExe || hasLauncherScript;
        }

        private void RunRenpyExtraction(string rootPath)
        {
            try
            {
                string gameFolder = Path.Combine(rootPath, "game");
                string outputDir = Path.Combine(rootPath, "extracted");
                Directory.CreateDirectory(outputDir);

                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Maximum = 100;
                    progressBar.Value = 0;
                    statusLabel.Text = "Renpy: Finding archives...";
                    statsLabel.Text = "";
                });

                var rpaFiles = new List<string>();
                foreach (string rpa in Directory.GetFiles(gameFolder, "*.rpa", SearchOption.AllDirectories))
                    rpaFiles.Add(rpa);

                if (rpaFiles.Count == 0)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        statusLabel.Text = "Renpy: No RPA files found";
                        WriteLog("No RPA files found");
                    });
                    return;
                }

                WriteLog("Found " + rpaFiles.Count + " RPA archives");

                string tempDir = Path.Combine(Path.GetTempPath(), "RpgmvpConverter");
                Directory.CreateDirectory(tempDir);
                string scriptPath = Path.Combine(tempDir, "extract_rpa.py");

                string script = @"
import struct
import zlib
import pickle
import os
import sys

def extract_rpa(rpa_path, output_dir):
    with open(rpa_path, 'rb') as f:
        header = f.readline().decode('ascii').strip()
        
        if header.startswith('RPA-3.0'):
            parts = header.split()
            index_offset = int(parts[1], 16)
            key = int(parts[2], 16)
        elif header.startswith('RPA-2.0'):
            parts = header.split()
            index_offset = int(parts[1], 16)
            key = 0
        else:
            print('ERROR: Unknown RPA format')
            return 0, 0
        
        f.seek(index_offset)
        compressed = f.read()
        index_data = zlib.decompress(compressed)
        index = pickle.loads(index_data)
        
        total = sum(len(entries) for entries in index.values())
        print('TOTAL:' + str(total))
        sys.stdout.flush()
        
        count = 0
        for filename, entries in index.items():
            for entry in entries:
                if len(entry) == 2:
                    offset, length = entry
                    start = 0
                else:
                    offset, length, start = entry
                
                if key != 0:
                    offset ^= key
                    length ^= key
                
                f.seek(offset)
                data = f.read(length)
                
                out_path = os.path.join(output_dir, filename.replace('/', os.sep))
                out_dir = os.path.dirname(out_path)
                if out_dir:
                    os.makedirs(out_dir, exist_ok=True)
                
                with open(out_path, 'wb') as out:
                    out.write(data)
                
                count += 1
                if count % 500 == 0:
                    print('PROGRESS:' + str(count) + ':' + str(total))
                    sys.stdout.flush()
        
        print('PROGRESS:' + str(count) + ':' + str(total))
        return count, total

rpa_file = os.environ.get('RPA_PATH', '')
output_dir = os.environ.get('OUTPUT_PATH', '')

if not rpa_file or not os.path.exists(rpa_file):
    print('ERROR: RPA file not found')
    sys.exit(1)

count = extract_rpa(rpa_file, output_dir)
print('DONE:' + str(count[0]))
";

                File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(false));

                int totalExtracted = 0;
                int totalFiles = 1;
                DateTime startTime = DateTime.UtcNow;

                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Maximum = 100;
                    progressBar.Value = 0;
                    statusLabel.Text = "Renpy: Starting...";
                    statsLabel.Text = "ETA: --:--";
                });

                for (int i = 0; i < rpaFiles.Count; i++)
                {
                    string rpaFile = rpaFiles[i];
                    string rpaName = Path.GetFileName(rpaFile);
                    
                    WriteLog("Processing: " + rpaName);

                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "python";
                    psi.Arguments = "\"" + scriptPath + "\"";
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                    psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                    psi.EnvironmentVariables["RPA_PATH"] = rpaFile;
                    psi.EnvironmentVariables["OUTPUT_PATH"] = outputDir;
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                    using (Process process = Process.Start(psi))
                    {
                        if (process != null)
                        {
                            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                {
                                    this.BeginInvoke((MethodInvoker)delegate
                                    {
                                        string data = e.Data;

                                        if (data.StartsWith("TOTAL:"))
                                        {
                                            string[] parts = data.Split(':');
                                            if (parts.Length >= 2)
                                            {
                                                int.TryParse(parts[1], out totalFiles);
                                                progressBar.Maximum = Math.Max(totalFiles, 1);
                                            }
                                        }
                                        else if (data.StartsWith("PROGRESS:"))
                                        {
                                            string[] parts = data.Split(':');
                                            if (parts.Length >= 3)
                                            {
                                                int.TryParse(parts[1], out totalExtracted);
                                                int.TryParse(parts[2], out totalFiles);
                                                progressBar.Maximum = Math.Max(totalFiles, 1);
                                                progressBar.Value = Math.Min(totalExtracted, progressBar.Maximum);

                                                double elapsed = Math.Max((DateTime.UtcNow - startTime).TotalSeconds, 0.1);
                                                double speed = elapsed > 0 ? totalExtracted / elapsed : 0;
                                                int remaining = totalFiles - totalExtracted;
                                                string eta = speed > 0 ? FormatDuration(remaining / speed) : "--:--";
                                                statsLabel.Text = "Extracted: " + totalExtracted + " / " + totalFiles + " | " + speed.ToString("N0") + " f/s | ETA: " + eta;
                                                statusLabel.Text = "Renpy: " + totalExtracted + " / " + totalFiles;
                                            }
                                        }
                                        else if (data.StartsWith("ERROR:"))
                                        {
                                            WriteLog(data);
                                            statusLabel.Text = "Renpy: Error";
                                        }
                                    });
                                }
                            };
                            process.BeginOutputReadLine();
                            process.WaitForExit();
                        }
                    }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = progressBar.Maximum;
                    statusLabel.Text = "Renpy: Complete";
                    double elapsed = Math.Max((DateTime.UtcNow - startTime).TotalSeconds, 0.1);
                    double speed = elapsed > 0 ? totalExtracted / elapsed : 0;
                    statsLabel.Text = "Extracted: " + totalExtracted + " | " + speed.ToString("N0") + " f/s";
                    MessageBox.Show("Renpy extraction complete!\n\nExtracted: " + totalExtracted + " files\n\nOutput: " + outputDir, "Renpy Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = 0;
                    statusLabel.Text = "Renpy: Error";
                    WriteLog("Error: " + ex.Message);
                });
            }
        }

        private void RunUnityExtraction(string rootPath, string extractMode)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempDir = Path.Combine(Path.GetTempPath(), "RpgmvpConverter");
                Directory.CreateDirectory(tempDir);
                string scriptPath = Path.Combine(tempDir, "extract_unity.py");

                string script = GetUnityExtractionScript();
                File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(false));

                string outputDir = Path.Combine(rootPath, "extracted");
                Directory.CreateDirectory(outputDir);

                progressBar.Maximum = 100;
                progressBar.Value = 0;
                statusLabel.Text = "Unity: Scanning...";
                statsLabel.Text = "ETA: --:--";

                int totalFiles = 0;
                int processedFiles = 0;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = "\"" + scriptPath + "\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = System.Text.Encoding.UTF8;
                psi.StandardErrorEncoding = System.Text.Encoding.UTF8;
                psi.EnvironmentVariables["GAME_PATH"] = rootPath;
                psi.EnvironmentVariables["OUTPUT_PATH"] = outputDir;
                psi.EnvironmentVariables["EXTRACT_MODE"] = extractMode;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "x";

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        DateTime startTime = DateTime.UtcNow;
                        string lastStatus = "";

                        process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                string data = e.Data;
                                this.BeginInvoke((MethodInvoker)delegate
                                {
                                    WriteLog(data);

                                    if (data.StartsWith("TOTAL:"))
                                    {
                                        string[] parts = data.Split(':');
                                        if (parts.Length >= 2)
                                        {
                                            int.TryParse(parts[1], out totalFiles);
                                            progressBar.Maximum = Math.Max(totalFiles, 1);
                                            statusLabel.Text = "Unity: Found " + totalFiles + " files";
                                        }
                                    }
                                    else if (data.StartsWith("PROGRESS:"))
                                    {
                                        string[] parts = data.Split(':');
                                        if (parts.Length >= 3)
                                        {
                                            int.TryParse(parts[1], out processedFiles);
                                            int.TryParse(parts[2], out totalFiles);
                                            progressBar.Maximum = Math.Max(totalFiles, 1);
                                            progressBar.Value = Math.Min(processedFiles, progressBar.Maximum);

                                            double elapsed = Math.Max((DateTime.UtcNow - startTime).TotalSeconds, 0.1);
                                            double speed = processedFiles / elapsed;
                                            int remaining = totalFiles - processedFiles;
                                            string eta = speed > 0 ? FormatDuration(remaining / speed) : "--:--";
                                            statsLabel.Text = "Processed: " + processedFiles + " / " + totalFiles + " | Speed: " + speed.ToString("N2") + " f/s | ETA: " + eta;
                                            statusLabel.Text = "Unity: Processing " + processedFiles + "/" + totalFiles + " (" + (totalFiles > 0 ? (processedFiles * 100 / totalFiles).ToString() : "0") + "%)";
                                        }
                                    }
                                    else if (data.StartsWith("Processing:") && lastStatus != data)
                                    {
                                        lastStatus = data;
                                        statusLabel.Text = "Unity: " + data;
                                    }
                                });
                            }
                        };
                        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                this.BeginInvoke((MethodInvoker)delegate { WriteLog("ERROR: " + e.Data); });
                            }
                        };
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                    }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = progressBar.Maximum;
                    statusLabel.Text = "Unity: Complete";
                    statsLabel.Text = "Processed: " + processedFiles + " / " + totalFiles;
                    WriteLog("Unity extraction complete! Output: " + outputDir);
                    MessageBox.Show("Unity extraction complete!\n\nExtracted: " + processedFiles + " files\n\nOutput: " + outputDir, "Unity Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = 0;
                    statusLabel.Text = "Unity: Error";
                    WriteLog("Unity extraction error: " + ex.Message);
                });
            }
        }

        private static string GetUnityExtractionScript()
        {
            return @"# -*- coding: utf-8 -*-
from __future__ import print_function
import sys
import io
import os

if sys.version_info[0] >= 3:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import UnityPy

game_path = os.environ.get('GAME_PATH', '')
output_path = os.environ.get('OUTPUT_PATH', '')
extract_mode = os.environ.get('EXTRACT_MODE', 'all')

if not game_path or not output_path:
    print('ERROR: GAME_PATH or OUTPUT_PATH not set in environment', file=sys.stderr)
    sys.exit(1)

os.makedirs(output_path, exist_ok=True)

def has_relevant_content(file_path, mode):
    try:
        env = UnityPy.load(file_path)
        for obj in env.objects:
            if mode in ['textures', 'all'] and obj.type.name == 'Texture2D':
                return True
            if mode in ['videos', 'all'] and obj.type.name == 'VideoClip':
                return True
            if mode in ['audios', 'all'] and obj.type.name == 'AudioClip':
                return True
        return False
    except:
        return False

def extract_file(file_path, output, mode):
    total = 0
    try:
        print('Loading: ' + os.path.basename(file_path))
        sys.stdout.flush()
        
        env = UnityPy.load(file_path)
        
        print('Processing: ' + os.path.basename(file_path))
        sys.stdout.flush()
        
        file_output = os.path.join(output, os.path.splitext(os.path.basename(file_path))[0])
        os.makedirs(file_output, exist_ok=True)
        
        textures = 0
        videos = 0
        audios = 0
        
        for obj in env.objects:
            try:
                if mode in ['textures', 'all']:
                    if obj.type.name == 'Texture2D':
                        data = obj.read()
                        name = getattr(data, 'name', None) or getattr(data, 'm_Name', 'texture_' + str(textures))
                        safe_name = ''.join(c for c in str(name) if c.isalnum() or c in '._- ')
                        
                        if hasattr(data, 'image') and data.image:
                            img_path = os.path.join(file_output, safe_name + '.png')
                            data.image.save(img_path)
                            textures += 1
                            total += 1
                
                if mode in ['videos', 'all']:
                    if obj.type.name == 'VideoClip':
                        data = obj.read()
                        name = getattr(data, 'm_Name', 'video_' + str(videos))
                        safe_name = ''.join(c for c in str(name) if c.isalnum() or c in '._- ')
                        videos += 1
                
                if mode in ['audios', 'all']:
                    if obj.type.name == 'AudioClip':
                        data = obj.read()
                        name = getattr(data, 'name', None) or getattr(data, 'm_Name', 'audio_' + str(audios))
                        safe_name = ''.join(c for c in str(name) if c.isalnum() or c in '._- ')
                        
                        audio_data = getattr(data, 'audio_data', None) or getattr(data, 'm_AudioData', None)
                        if audio_data:
                            audio_path = os.path.join(file_output, safe_name + '.wav')
                            with open(audio_path, 'wb') as f:
                                f.write(audio_data)
                            audios += 1
                            total += 1
            
            except Exception as e:
                pass
        
        return total
        
    except Exception as e:
        print('Error: ' + str(e))
        sys.stdout.flush()
        return 0

print('Unity Asset Extractor')
print('Game: ' + game_path)
print('Output: ' + output_path)
print('Mode: ' + extract_mode)
print('-' * 50)

total_extracted = 0
all_files = []

# Find all *_Data folders and collect files
data_folders = []
for item in os.listdir(game_path):
    if item.endswith('_Data'):
        data_folders.append(os.path.join(game_path, item))

if not data_folders:
    print('No Unity data folders found')
else:
    for data_folder in data_folders:
        try:
            for f in os.listdir(data_folder):
                if f.endswith('.assets') and not f.endswith('.resS'):
                    all_files.append(os.path.join(data_folder, f))
        except Exception as e:
            pass
        
        ggm_path = os.path.join(data_folder, 'globalgamemanagers.assets')
        if os.path.exists(ggm_path):
            all_files.append(ggm_path)
        
        streaming = os.path.join(data_folder, 'StreamingAssets', 'aa', 'StandaloneWindows64')
        if os.path.exists(streaming):
            try:
                for bundle_file in os.listdir(streaming):
                    if bundle_file.endswith('.bundle'):
                        all_files.append(os.path.join(streaming, bundle_file))
            except:
                pass

# Filter files that have relevant content
print('Checking for content...')
sys.stdout.flush()
files_with_content = []
for f in all_files:
    if has_relevant_content(f, extract_mode):
        files_with_content.append(f)

all_files = files_with_content

print('Found ' + str(len(all_files)) + ' files with content')
sys.stdout.flush()
print('TOTAL:' + str(len(all_files)))
sys.stdout.flush()

for i, file_path in enumerate(all_files):
    total_extracted += extract_file(file_path, output_path, extract_mode)
    print('PROGRESS:' + str(i + 1) + ':' + str(len(all_files)))
    sys.stdout.flush()

print('-' * 50)
print('Done! Extracted ' + str(total_extracted) + ' assets')
sys.stdout.flush()
";
        }

        private void TogglePause()
        {
            if (currentRun == null) return;

            if (currentRun.IsPaused)
            {
                currentRun.Resume();
                pauseButton.Text = "Pause";
                pauseButton.BackColor = warningColor;
                pauseButton.ForeColor = Color.Black;
                WriteLog("Processing resumed");
            }
            else
            {
                currentRun.Pause();
                pauseButton.Text = "Resume";
                pauseButton.BackColor = accentColor;
                pauseButton.ForeColor = textColor;
                statusLabel.Text = "Paused";
                WriteLog("Pause enabled");
            }
        }

        private void CancelConversion()
        {
            if (currentRun != null)
            {
                currentRun.Cancel();
                cancelButton.Enabled = false;
                pauseButton.Enabled = false;
                statusLabel.Text = "Stopping...";
                WriteLog("Stop requested");
            }
        }

        private void UpdateUiFromRun()
        {
            if (currentRun == null) return;

            int processed = Math.Min(currentRun.ProcessedCount, currentRun.TotalCount);
            progressBar.Value = Math.Min(processed, progressBar.Maximum);

            if (currentRun.CancelRequested)
                statusLabel.Text = "Stopping...";
            else if (currentRun.IsPaused)
                statusLabel.Text = "Paused";
            else
                statusLabel.Text = "Processing" + ": " + (string.IsNullOrWhiteSpace(currentRun.CurrentFile) ? "..." : currentRun.CurrentFile);

            double elapsedSeconds = Math.Max((DateTime.UtcNow - currentRun.StartUtc).TotalSeconds, 0.001d);
            double speed = processed / elapsedSeconds;
            int remaining = currentRun.TotalCount - processed;
            string eta = speed > 0d && !currentRun.IsPaused ? FormatDuration(remaining / speed) : "--:--";
            statsLabel.Text = string.Format("Processed: {0} / {1} | Speed: {2:N2} f/s | ETA: {3}", processed, currentRun.TotalCount, speed, eta);
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
                statusLabel.Text = "Cancelled";
                statsLabel.Text = string.Format("Processed: {0} / {1} | Speed: {2:N2} f/s", processed, finished.TotalCount, speed);
                WriteLog("Cancelled" + ": " + processed);
                return;
            }

            statusLabel.Text = "Done";
            statsLabel.Text = string.Format("Processed: {0} / {1} | Speed: {2:N2} f/s | ETA: 00:00", processed, finished.TotalCount, speed);

            if (finished.ErrorCount > 0)
                WriteLog(string.Format("Errors: {0}. Last error: {1}", finished.ErrorCount, finished.LastError));
            else
            {
                WriteLog(string.Format("Completed {0} {1} {2}.", processed, "files processed in", FormatDuration(elapsedSeconds)));
                MessageBox.Show("Conversion complete!\n\nExtracted: " + processed + " files\n\nOutput: " + finished.OutputDir, "RPGMVP Converter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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
            MessageBox.Show(this, "Conversion in progress. Stop first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WriteLog(string message)
        {
            if (logBox.TextLength > 0) logBox.AppendText(Environment.NewLine);
            logBox.AppendText(message);
        }

        private static bool CheckUnrpaInstalled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "python";
                psi.Arguments = "-c \"from unrpa import UNRPA; print('ok')\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Trim().Contains("ok");
                }
            }
            catch
            {
                return false;
            }
        }

        private void InstallUnrpa()
        {
            try
            {
                WriteLog("Installing unrpa...");
                statusLabel.Text = "Installing unrpa...";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd";
                psi.Arguments = "/c pip install unrpa";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        WriteLog("unrpa installed successfully");
                        MessageBox.Show("unrpa installed successfully!\n\nClick Start again to extract RPA files.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        WriteLog("Failed to install unrpa");
                        MessageBox.Show("Failed to install unrpa.\n\nError: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Install error: " + ex.Message);
            }
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
                throw new InvalidOperationException("Invalid HEX key");

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
                    bool hasUnityData = Directory.GetDirectories(current.FullName, "*_Data", SearchOption.TopDirectoryOnly).Any();
                    bool hasUnityPlayer = File.Exists(Path.Combine(current.FullName, "UnityPlayer.dll"));
                    bool hasManaged = Directory.Exists(Path.Combine(current.FullName, "Managed"));

                    if (string.Equals(current.Name, "Game", StringComparison.OrdinalIgnoreCase) ||
                        (hasWww && (hasPackage || hasWwwData || hasExe)) ||
                        (hasPackage && hasData && hasImg) ||
                        (hasGame && hasExe) ||
                        (hasUnityData && hasExe) ||
                        (hasUnityPlayer && hasManaged))
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
            private readonly string outputDir;
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
                outputDir = Path.Combine(rootPath, "extracted");
                Directory.CreateDirectory(outputDir);
                queue = new ConcurrentQueue<string>(files);
                pauseGate = new ManualResetEventSlim(true);
                cancellation = new CancellationTokenSource();
                StartUtc = DateTime.UtcNow;
                Completion = Task.CompletedTask;
            }

            public string RootPath { get; private set; }
            public string OutputDir { get { return outputDir; } }
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

                        string relativePath = filePath.Substring(RootPath.Length);
                        if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                            relativePath = relativePath.Substring(1);
                        
                        string outputPath = Path.Combine(outputDir, relativePath);
                        outputPath = Path.ChangeExtension(outputPath, ".png");

                        string outDir = Path.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                        File.WriteAllBytes(outputPath, data);
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
