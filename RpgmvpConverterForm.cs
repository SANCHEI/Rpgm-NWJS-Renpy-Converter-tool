using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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

        private TextBox pathBox;
        private TextBox keyBox;
        private ComboBox unlockerModeBox;
        private ComboBox unityExtractModeBox;
        private Label detectedEngineLabel;
        private Label scanSummaryLabel;
        private Label statusLabel;
        private Label statsLabel;
        private TextBox logBox;
        private ProgressBar progressBar;
        private Button browseButton;
        private Button dryRunButton;
        private Button startButton;
        private Button unityExtractButton;
        private Button unlockerButton;
        private Button removeUnlockerButton;
        private Button pauseButton;
        private Button cancelButton;
        private Button openOutputButton;

        private readonly object processSync = new object();
        private System.Windows.Forms.Timer uiTimer;
        private ConversionRun currentRun;
        private Process activeProcess;
        private bool externalRunning;
        private string lastOutputDir = "";

        public RpgmvpConverterForm()
        {
            BuildUi();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); }
                catch { }
            }

            string initialRoot = TryFindGameRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(initialRoot))
                ApplyGamePath(initialRoot, false);
        }

        private void BuildUi()
        {
            Font uiFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            Font uiBold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            Font titleFont = new Font("Segoe UI Semibold", 14f, FontStyle.Regular);
            Font logFont = new Font("Consolas", 9.5f, FontStyle.Regular);

            Text = "Game Asset Tool v1.5";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 900);
            Size = new Size(940, 900);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = formBack;
            ForeColor = textColor;
            Font = uiFont;
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;

            Panel header = new Panel { Height = 68, BackColor = panelBack, Dock = DockStyle.Top };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Text = "Game Asset Tool",
                Font = titleFont,
                ForeColor = textColor,
                Location = new Point(18, 10),
                Size = new Size(500, 28),
                BackColor = Color.Transparent
            });
            header.Controls.Add(new Label
            {
                Text = "Drop a game folder here, scan it, then extract or unlock",
                ForeColor = mutedColor,
                Location = new Point(19, 39),
                Size = new Size(700, 20),
                BackColor = Color.Transparent
            });

            int y = 88;
            Controls.Add(CreateSectionLabel("Game Folder", y));
            y += 22;

            Panel pathPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(892, 34),
                BackColor = inputBack,
                Padding = new Padding(4)
            };
            Controls.Add(pathPanel);
            pathBox = new TextBox
            {
                Location = new Point(8, 4),
                Size = new Size(710, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            pathBox.TextChanged += delegate { OnPathChanged(); };
            pathPanel.Controls.Add(pathBox);
            browseButton = CreateButton("Browse...", new Point(730, 3), new Size(154, 28), accentColor, formBack, uiBold);
            browseButton.Click += delegate { BrowseFolder(); };
            pathPanel.Controls.Add(browseButton);
            y += 42;

            Panel scanPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(892, 56),
                BackColor = panelBack,
                Padding = new Padding(8)
            };
            Controls.Add(scanPanel);
            detectedEngineLabel = new Label
            {
                Text = "Engine: not detected",
                Location = new Point(10, 8),
                Size = new Size(300, 20),
                ForeColor = accentColor,
                Font = uiBold
            };
            scanPanel.Controls.Add(detectedEngineLabel);
            scanSummaryLabel = new Label
            {
                Text = "Select a folder or drop it into this window.",
                Location = new Point(10, 30),
                Size = new Size(690, 20),
                ForeColor = mutedColor
            };
            scanPanel.Controls.Add(scanSummaryLabel);
            dryRunButton = CreateButton("Dry Run / Scan", new Point(718, 13), new Size(158, 30), Color.FromArgb(45, 50, 60), textColor, uiBold);
            dryRunButton.Click += delegate { RunDryScan(true); };
            scanPanel.Controls.Add(dryRunButton);
            y += 66;

            Panel keyPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(892, 34),
                BackColor = inputBack,
                Padding = new Padding(4)
            };
            Controls.Add(keyPanel);
            keyPanel.Controls.Add(new Label
            {
                Text = "Optional key",
                Location = new Point(8, 8),
                Size = new Size(100, 20),
                ForeColor = mutedColor
            });
            keyBox = new TextBox
            {
                Location = new Point(112, 4),
                Size = new Size(590, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont
            };
            keyPanel.Controls.Add(keyBox);
            startButton = CreateButton("Extract Detected", new Point(714, 3), new Size(170, 28), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartDetectedExtractionAsync(); };
            keyPanel.Controls.Add(startButton);
            y += 44;

            Controls.Add(CreateSectionLabel("Gallery Unlocker for Ren'Py / NWJS", y));
            y += 22;
            unlockerModeBox = new ComboBox
            {
                Location = new Point(18, y),
                Size = new Size(100, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            unlockerModeBox.Items.Add("Soft");
            unlockerModeBox.Items.Add("Hard");
            unlockerModeBox.SelectedIndex = 0;
            Controls.Add(unlockerModeBox);
            unlockerButton = CreateButton("Install Unlocker", new Point(130, y - 3), new Size(170, 30), pinkColor, formBack, uiBold);
            unlockerButton.Click += delegate { InstallUnlocker(); };
            Controls.Add(unlockerButton);
            removeUnlockerButton = CreateButton("Remove Unlocker", new Point(310, y - 3), new Size(170, 30), Color.FromArgb(95, 65, 80), textColor, uiBold);
            removeUnlockerButton.Click += delegate { RemoveUnlocker(); };
            Controls.Add(removeUnlockerButton);
            y += 42;

            Controls.Add(CreateSectionLabel("Unity Extractor", y));
            y += 22;
            unityExtractModeBox = new ComboBox
            {
                Location = new Point(18, y),
                Size = new Size(150, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            unityExtractModeBox.Items.Add("Textures");
            unityExtractModeBox.Items.Add("Videos");
            unityExtractModeBox.Items.Add("Audio");
            unityExtractModeBox.Items.Add("Meshes");
            unityExtractModeBox.Items.Add("All");
            unityExtractModeBox.SelectedIndex = 0;
            Controls.Add(unityExtractModeBox);
            unityExtractButton = CreateButton("Extract Unity", new Point(180, y - 3), new Size(170, 30), Color.FromArgb(138, 98, 255), textColor, uiBold);
            unityExtractButton.Click += async delegate { await StartUnityExtractionAsync(); };
            Controls.Add(unityExtractButton);
            y += 46;

            pauseButton = CreateButton("Pause", new Point(18, y), new Size(100, 30), warningColor, Color.Black, uiBold);
            pauseButton.Enabled = false;
            pauseButton.Click += delegate { TogglePause(); };
            Controls.Add(pauseButton);
            cancelButton = CreateButton("Cancel", new Point(126, y), new Size(100, 30), dangerColor, textColor, uiBold);
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelOperation(); };
            Controls.Add(cancelButton);
            openOutputButton = CreateButton("Open Output Folder", new Point(236, y), new Size(180, 30), Color.FromArgb(45, 50, 60), textColor, uiBold);
            openOutputButton.Enabled = false;
            openOutputButton.Click += delegate { OpenOutputFolder(); };
            Controls.Add(openOutputButton);
            y += 40;

            progressBar = new ProgressBar
            {
                Location = new Point(18, y),
                Size = new Size(892, 24),
                Style = ProgressBarStyle.Continuous
            };
            Controls.Add(progressBar);
            y += 30;
            statusLabel = new Label
            {
                Text = "Waiting to start",
                Location = new Point(18, y),
                Size = new Size(892, 20),
                ForeColor = textColor,
                Font = uiBold
            };
            Controls.Add(statusLabel);
            y += 22;
            statsLabel = new Label
            {
                Text = "Processed: 0 / 0 | Size: -- | ETA: --:--",
                Location = new Point(18, y),
                Size = new Size(892, 20),
                ForeColor = mutedColor
            };
            Controls.Add(statsLabel);
            y += 26;

            Panel logPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(892, 320),
                BackColor = logBack,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(logPanel);
            logPanel.Controls.Add(new Label
            {
                Text = "Log",
                Location = new Point(10, 10),
                Size = new Size(60, 20),
                ForeColor = mutedColor,
                Font = uiBold
            });
            Button clearLogButton = CreateButton("Clear", new Point(75, 8), new Size(80, 24), Color.FromArgb(45, 50, 60), textColor, uiFont);
            clearLogButton.Click += delegate { logBox.Clear(); };
            logPanel.Controls.Add(clearLogButton);
            logBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new Point(10, 36),
                Size = new Size(870, 272),
                BackColor = logBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                Font = logFont
            };
            logBox.TextChanged += delegate
            {
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
            };
            logPanel.Controls.Add(logBox);

            uiTimer = new System.Windows.Forms.Timer { Interval = 200 };
            uiTimer.Tick += delegate { UpdateUiFromRun(); };
        }

        private Label CreateSectionLabel(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(18, y),
                Size = new Size(500, 20),
                ForeColor = mutedColor,
                BackColor = Color.Transparent
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
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select game folder";
                dialog.SelectedPath = Directory.Exists(pathBox.Text) ? pathBox.Text : "";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ApplyGamePath(dialog.SelectedPath, true);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;
            string path = Directory.Exists(paths[0]) ? paths[0] : Path.GetDirectoryName(paths[0]);
            if (!string.IsNullOrWhiteSpace(path))
                ApplyGamePath(path, true);
        }

        private void ApplyGamePath(string path, bool scan)
        {
            string detectedRoot = TryFindGameRoot(path);
            pathBox.Text = string.IsNullOrWhiteSpace(detectedRoot) ? path : detectedRoot;
            TryAutoDetectKey(pathBox.Text);
            if (scan) RunDryScan(false);
        }

        private void OnPathChanged()
        {
            string path = pathBox.Text.Trim();
            if (!Directory.Exists(path))
            {
                detectedEngineLabel.Text = "Engine: not detected";
                detectedEngineLabel.ForeColor = mutedColor;
                scanSummaryLabel.Text = "Select a folder or drop it into this window.";
                return;
            }

            TryAutoDetectKey(path);
            GameEngine engine = DetectEngine(path);
            detectedEngineLabel.Text = "Engine: " + EngineName(engine);
            detectedEngineLabel.ForeColor = EngineColor(engine);
            scanSummaryLabel.Text = "Ready to scan. Click Dry Run / Scan to inspect files before extraction.";
        }

        private void RunDryScan(bool showLog)
        {
            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                ScanSummary summary = BuildScanSummary(rootPath);
                detectedEngineLabel.Text = "Engine: " + EngineName(summary.Engine);
                detectedEngineLabel.ForeColor = EngineColor(summary.Engine);
                scanSummaryLabel.Text = string.Format(
                    "{0} archive(s), {1} candidate file(s), estimated input {2}",
                    summary.ArchiveCount,
                    summary.FileCount,
                    FormatBytes(summary.TotalBytes));
                if (showLog)
                {
                    WriteLog("Dry run: " + EngineName(summary.Engine));
                    WriteLog("Found: " + summary.ArchiveCount + " archive(s), " + summary.FileCount + " candidate file(s), " + FormatBytes(summary.TotalBytes));
                }
            }
            finally
            {
                Cursor = previous;
            }
        }

        private async Task StartDetectedExtractionAsync()
        {
            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }

            GameEngine engine = DetectEngine(rootPath);
            if (engine == GameEngine.Unity)
            {
                await StartUnityExtractionAsync();
                return;
            }
            if (engine == GameEngine.Renpy)
            {
                await StartRenpyExtractionAsync();
                return;
            }
            if (engine == GameEngine.Godot)
            {
                await StartPortableScriptExtractionAsync("Godot", "godot", "extract_godot.py", "RpgmvpConverterWinForms.scripts.extract_godot.py");
                return;
            }
            if (engine == GameEngine.Kirikiri)
            {
                await StartPortableScriptExtractionAsync("KiriKiri XP3", "kirikiri", "extract_xp3.py", "RpgmvpConverterWinForms.scripts.extract_xp3.py");
                return;
            }
            if (engine == GameEngine.Unreal)
            {
                await StartPortableScriptExtractionAsync("Unreal experimental", "unreal", "extract_unreal.py", "RpgmvpConverterWinForms.scripts.extract_unreal.py");
                return;
            }
            if (engine != GameEngine.RpgMaker && engine != GameEngine.Nwjs)
            {
                MessageBox.Show("No supported game assets found. Run Dry Run / Scan and check the selected folder.", "Game Asset Tool", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await StartRpgmExtractionAsync(rootPath);
        }

        private async Task StartRpgmExtractionAsync(string rootPath)
        {
            if (currentRun != null || externalRunning) return;

            byte[] keyBytes;
            if (string.IsNullOrWhiteSpace(keyBox.Text))
            {
                string detectedKey = TryFindKey(rootPath);
                if (string.IsNullOrWhiteSpace(detectedKey))
                    detectedKey = TryReconstructKey(rootPath);
                if (string.IsNullOrWhiteSpace(detectedKey))
                {
                    WriteLog("Could not determine RPGM encryption key.");
                    return;
                }
                keyBox.Text = detectedKey;
            }

            try { keyBytes = ParseKey(keyBox.Text); }
            catch
            {
                WriteLog("Invalid HEX key format.");
                return;
            }

            List<string> files = GetFilesToConvert(rootPath);
            if (files.Count == 0)
            {
                WriteLog("No .rpgmvp/.png_ files found.");
                return;
            }

            currentRun = new ConversionRun(rootPath, files, keyBytes);
            lastOutputDir = currentRun.OutputDir;
            SetRpgmRunningState(true);
            progressBar.Maximum = Math.Max(files.Count, 1);
            progressBar.Value = 0;
            statusLabel.Text = "RPGM: preparing...";
            statsLabel.Text = "Processed: 0 / " + files.Count + " | Size: -- | ETA: --:--";
            WriteLog("RPGM extraction started: " + files.Count + " file(s)");
            uiTimer.Start();
            currentRun.Start();

            ConversionRun finished = currentRun;
            try
            {
                await finished.Completion;
            }
            catch (OperationCanceledException) { }
            finally
            {
                FinishRpgmExtraction(finished);
            }
        }

        private async Task StartRenpyExtractionAsync()
        {
            if (currentRun != null || externalRunning) return;

            string rootPath = pathBox.Text.Trim();
            string gameFolder = Path.Combine(rootPath, "game");
            List<string> archives = EnumerateFilesSafe(gameFolder, "*.rpa").ToList();
            if (archives.Count == 0)
            {
                MessageBox.Show("No RPA archives found in the game folder.", "Ren'Py Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!EnsurePortableRuntimeAvailable()) return;

            string outputDir = Path.Combine(rootPath, "extracted", "renpy");
            lastOutputDir = outputDir;
            SetExternalRunningState(true, "Ren'Py");
            progressBar.Maximum = Math.Max(archives.Count, 1);
            progressBar.Value = 0;
            WriteLog("Ren'Py extraction started with unrpa 2.3.0: " + archives.Count + " archive(s)");

            OperationResult result;
            try
            {
                result = await Task.Run(delegate { return RunRenpyExtraction(rootPath, archives, outputDir); });
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed("Ren'Py", outputDir, ex.Message);
            }
            SetExternalRunningState(false, "Ren'Py");
            CompleteExternalOperation(result);
        }

        private OperationResult RunRenpyExtraction(string rootPath, List<string> archives, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            int errors = 0;
            int renamed = 0;
            Directory.CreateDirectory(outputDir);
            string gameFolder = Path.Combine(rootPath, "game");

            for (int i = 0; i < archives.Count; i++)
            {
                string archive = archives[i];
                string relative = MakeRelativePath(gameFolder, archive);
                string subfolder = Path.ChangeExtension(relative, null);
                string archiveOutput = Path.Combine(outputDir, SanitizeRelativePath(subfolder));
                bool pathRenamed;
                archiveOutput = GetUniqueDirectoryPath(archiveOutput, out pathRenamed);
                if (pathRenamed) renamed++;
                Directory.CreateDirectory(archiveOutput);

                BeginUi(delegate
                {
                    statusLabel.Text = "Ren'Py: " + Path.GetFileName(archive);
                    progressBar.Value = Math.Min(i, progressBar.Maximum);
                    statsLabel.Text = "Archives: " + i + " / " + archives.Count;
                });
                SafeLog("Processing RPA: " + relative);

                ProcessStartInfo psi = CreatePythonProcessInfo();
                psi.Arguments = "-m unrpa -m -p " + QuoteArg(archiveOutput) + " " + QuoteArg(archive);
                int exitCode = RunExternalProcess(psi, delegate(string line) { SafeLog(line); });
                if (exitCode != 0) errors++;
            }

            FileStats stats = GetFileStats(outputDir);
            return new OperationResult("Ren'Py", outputDir, stats.Count, stats.Bytes, errors, renamed, DateTime.UtcNow - start);
        }

        private async Task StartUnityExtractionAsync()
        {
            if (currentRun != null || externalRunning) return;

            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath) || !IsUnityGame(rootPath))
            {
                MessageBox.Show("No Unity game found. Select a folder containing *_Data or UnityPlayer.dll.", "Unity Extractor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsurePortableRuntimeAvailable()) return;

            List<string> bundles = FindUnityBundleFiles(rootPath);
            bool includeBundles = true;
            if (bundles.Count > 0)
            {
                long bytes = bundles.Sum(delegate(string file) { return SafeFileLength(file); });
                includeBundles = MessageBox.Show(
                    "Found " + bundles.Count + " bundle file(s), " + FormatBytes(bytes) + ".\n\nExtract bundles too?",
                    "Unity Bundles",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;
            }

            string mode = UnityModeValue();
            string outputDir = Path.Combine(rootPath, "extracted", "unity");
            lastOutputDir = outputDir;
            SetExternalRunningState(true, "Unity");
            WriteLog("Unity extraction started: " + mode + (includeBundles ? " with bundles" : " without bundles"));

            OperationResult result;
            try
            {
                result = await Task.Run(delegate { return RunUnityExtraction(rootPath, outputDir, mode, includeBundles); });
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed("Unity", outputDir, ex.Message);
            }
            SetExternalRunningState(false, "Unity");
            CompleteExternalOperation(result);
        }

        private OperationResult RunUnityExtraction(string rootPath, string outputDir, string mode, bool includeBundles)
        {
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            string scriptPath = PortableRuntime.CreateSessionFilePath("extract_unity.py");
            File.WriteAllText(scriptPath, EmbeddedScripts.ReadText("RpgmvpConverterWinForms.scripts.extract_unity.py"), new UTF8Encoding(false));

            int extracted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;

            ProcessStartInfo psi = CreatePythonProcessInfo();
            psi.Arguments = QuoteArg(scriptPath);
            psi.EnvironmentVariables["GAME_PATH"] = rootPath;
            psi.EnvironmentVariables["OUTPUT_PATH"] = outputDir;
            psi.EnvironmentVariables["EXTRACT_MODE"] = mode;
            psi.EnvironmentVariables["INCLUDE_BUNDLES"] = includeBundles ? "1" : "0";
            psi.EnvironmentVariables["MAX_WORKERS"] = "4";

            int exitCode = RunExternalProcess(psi, delegate(string line)
            {
                if (line.StartsWith("TOTAL:", StringComparison.Ordinal))
                {
                    int total = ParseInt(line, 1);
                    BeginUi(delegate
                    {
                        progressBar.Maximum = Math.Max(total, 1);
                        progressBar.Value = 0;
                        statusLabel.Text = "Unity: found " + total + " archive(s)";
                    });
                }
                else if (line.StartsWith("PROGRESS:", StringComparison.Ordinal))
                {
                    int processed = ParseInt(line, 1);
                    int total = ParseInt(line, 2);
                    long currentBytes = ParseLong(line, 3);
                    BeginUi(delegate
                    {
                        progressBar.Maximum = Math.Max(total, 1);
                        progressBar.Value = Math.Min(processed, progressBar.Maximum);
                        statusLabel.Text = "Unity: " + processed + " / " + total;
                        statsLabel.Text = "Archives: " + processed + " / " + total + " | Size: " + FormatBytes(currentBytes);
                    });
                }
                else if (line.StartsWith("RESULT:", StringComparison.Ordinal))
                {
                    extracted = ParseInt(line, 1);
                    bytes = ParseLong(line, 2);
                    errors = ParseInt(line, 3);
                    renamed = ParseInt(line, 4);
                }
                else
                {
                    SafeLog(line);
                }
            });

            if (exitCode != 0 && errors == 0) errors = 1;
            return new OperationResult("Unity", outputDir, extracted, bytes, errors, renamed, DateTime.UtcNow - start);
        }

        private async Task StartPortableScriptExtractionAsync(string engineName, string outputFolder, string scriptFile, string resourceName)
        {
            if (currentRun != null || externalRunning) return;

            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }
            if (!EnsurePortableRuntimeAvailable()) return;

            string outputDir = Path.Combine(rootPath, "extracted", outputFolder);
            lastOutputDir = outputDir;
            SetExternalRunningState(true, engineName);
            WriteLog(engineName + " extraction started");

            OperationResult result;
            try
            {
                string optionalKey = keyBox.Text.Trim();
                result = await Task.Run(delegate
                {
                    return RunPortableScriptExtraction(rootPath, outputDir, engineName, scriptFile, resourceName, optionalKey);
                });
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed(engineName, outputDir, ex.Message);
            }
            SetExternalRunningState(false, engineName);
            CompleteExternalOperation(result);
        }

        private OperationResult RunPortableScriptExtraction(string rootPath, string outputDir, string engineName, string scriptFile, string resourceName, string optionalKey)
        {
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            string scriptPath = PortableRuntime.CreateSessionFilePath(scriptFile);
            File.WriteAllText(scriptPath, EmbeddedScripts.ReadText(resourceName), new UTF8Encoding(false));

            int extracted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;

            ProcessStartInfo psi = CreatePythonProcessInfo();
            psi.Arguments = QuoteArg(scriptPath);
            psi.EnvironmentVariables["GAME_PATH"] = rootPath;
            psi.EnvironmentVariables["OUTPUT_PATH"] = outputDir;
            psi.EnvironmentVariables["OPTIONAL_KEY"] = optionalKey;

            int exitCode = RunExternalProcess(psi, delegate(string line)
            {
                if (line.StartsWith("TOTAL:", StringComparison.Ordinal))
                {
                    int total = ParseInt(line, 1);
                    BeginUi(delegate
                    {
                        progressBar.Maximum = Math.Max(total, 1);
                        progressBar.Value = 0;
                        statusLabel.Text = engineName + ": found " + total + " archive(s)";
                    });
                }
                else if (line.StartsWith("PROGRESS:", StringComparison.Ordinal))
                {
                    int processed = ParseInt(line, 1);
                    int total = ParseInt(line, 2);
                    long currentBytes = ParseLong(line, 3);
                    BeginUi(delegate
                    {
                        progressBar.Maximum = Math.Max(total, 1);
                        progressBar.Value = Math.Min(processed, progressBar.Maximum);
                        statusLabel.Text = engineName + ": " + processed + " / " + total;
                        statsLabel.Text = "Archives: " + processed + " / " + total + " | Size: " + FormatBytes(currentBytes);
                    });
                }
                else if (line.StartsWith("RESULT:", StringComparison.Ordinal))
                {
                    extracted = ParseInt(line, 1);
                    bytes = ParseLong(line, 2);
                    errors = ParseInt(line, 3);
                    renamed = ParseInt(line, 4);
                }
                else
                {
                    SafeLog(line);
                }
            });

            if (exitCode != 0 && errors == 0) errors = 1;
            return new OperationResult(engineName, outputDir, extracted, bytes, errors, renamed, DateTime.UtcNow - start);
        }

        private void InstallUnlocker()
        {
            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath) || !IsRpgmOrNwjsGame(rootPath))
            {
                MessageBox.Show("Unlocker works with Ren'Py / NWJS-style game folders.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string modsPath = Path.Combine(rootPath, "game", "_mods");
            Directory.CreateDirectory(modsPath);
            if (GetUnlockerDirectories(rootPath).Any())
            {
                MessageBox.Show("Unlocker is already installed. Remove it before switching mode.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string mode = unlockerModeBox.SelectedIndex == 0 ? "soft" : "hard";
            string destination = Path.Combine(modsPath, "ZLZK_UGU_" + mode);
            try
            {
                UnlockerResources.ExtractUnlocker(mode, destination);
                if (!Directory.EnumerateFiles(destination, "*.rpy", SearchOption.AllDirectories).Any())
                    throw new InvalidDataException("Unlocker files were not created.");
                WriteLog("Unlocker installed: " + destination);
                MessageBox.Show("Unlocker installed.\n\nRun the game to activate it.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                WriteLog("Unlocker installation failed: " + ex.Message);
                MessageBox.Show("Unlocker installation failed:\n" + ex.Message, "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveUnlocker()
        {
            string rootPath = pathBox.Text.Trim();
            List<string> directories = GetUnlockerDirectories(rootPath).ToList();
            if (directories.Count == 0)
            {
                MessageBox.Show("Unlocker is not installed.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Remove installed unlocker files?", "Remove Unlocker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string modsPath = Path.GetFullPath(Path.Combine(rootPath, "game", "_mods")) + Path.DirectorySeparatorChar;
            foreach (string directory in directories)
            {
                string fullPath = Path.GetFullPath(directory);
                if (!fullPath.StartsWith(modsPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Refusing to remove a folder outside game\\_mods.");
                Directory.Delete(fullPath, true);
                WriteLog("Unlocker removed: " + fullPath);
            }
        }

        private IEnumerable<string> GetUnlockerDirectories(string rootPath)
        {
            string modsPath = Path.Combine(rootPath, "game", "_mods");
            if (!Directory.Exists(modsPath)) return Enumerable.Empty<string>();
            return Directory.EnumerateDirectories(modsPath, "ZLZK_UGU_*", SearchOption.TopDirectoryOnly).ToList();
        }

        private bool EnsurePortableRuntimeAvailable()
        {
            try
            {
                statusLabel.Text = "Preparing built-in runtime...";
                PortableRuntime.EnsureExtracted();
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("Built-in runtime failed: " + ex.Message);
                MessageBox.Show(
                    "Could not prepare the built-in extraction runtime.\n\n" + ex.Message,
                    "Built-in Runtime",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private static ProcessStartInfo CreatePythonProcessInfo()
        {
            return PortableRuntime.CreatePythonProcessInfo();
        }

        private int RunExternalProcess(ProcessStartInfo psi, Action<string> onLine)
        {
            using (Process process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) onLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) onLine("ERROR: " + e.Data);
                };

                lock (processSync) activeProcess = process;
                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    return process.ExitCode;
                }
                catch
                {
                    if (process.HasExited) return process.ExitCode;
                    throw;
                }
                finally
                {
                    lock (processSync)
                    {
                        if (ReferenceEquals(activeProcess, process)) activeProcess = null;
                    }
                }
            }
        }

        private void TogglePause()
        {
            if (currentRun == null) return;
            if (currentRun.IsPaused)
            {
                currentRun.Resume();
                pauseButton.Text = "Pause";
                WriteLog("RPGM extraction resumed.");
            }
            else
            {
                currentRun.Pause();
                pauseButton.Text = "Resume";
                statusLabel.Text = "Paused";
                WriteLog("RPGM extraction paused.");
            }
        }

        private void CancelOperation()
        {
            if (currentRun != null) currentRun.Cancel();
            lock (processSync)
            {
                if (activeProcess != null)
                {
                    try
                    {
                        if (!activeProcess.HasExited) activeProcess.Kill();
                    }
                    catch { }
                }
            }
            cancelButton.Enabled = false;
            statusLabel.Text = "Stopping...";
            WriteLog("Stop requested.");
        }

        private void UpdateUiFromRun()
        {
            if (currentRun == null) return;
            int processed = Math.Min(currentRun.ProcessedCount, currentRun.TotalCount);
            progressBar.Maximum = Math.Max(currentRun.TotalCount, 1);
            progressBar.Value = Math.Min(processed, progressBar.Maximum);
            double elapsed = Math.Max((DateTime.UtcNow - currentRun.StartUtc).TotalSeconds, 0.1);
            double speed = processed / elapsed;
            string eta = speed > 0 && !currentRun.IsPaused ? FormatDuration((currentRun.TotalCount - processed) / speed) : "--:--";
            statusLabel.Text = currentRun.IsPaused ? "Paused" : "RPGM: " + processed + " / " + currentRun.TotalCount;
            statsLabel.Text = "Processed: " + processed + " / " + currentRun.TotalCount + " | Size: " + FormatBytes(currentRun.TotalBytes) + " | ETA: " + eta;
        }

        private void FinishRpgmExtraction(ConversionRun finished)
        {
            if (!ReferenceEquals(currentRun, finished)) return;
            currentRun = null;
            uiTimer.Stop();
            SetRpgmRunningState(false);
            OperationResult result = new OperationResult(
                "RPG Maker",
                finished.OutputDir,
                finished.ProcessedCount,
                finished.TotalBytes,
                finished.ErrorCount,
                finished.RenamedCount,
                DateTime.UtcNow - finished.StartUtc);
            CompleteExternalOperation(result);
        }

        private void CompleteExternalOperation(OperationResult result)
        {
            lastOutputDir = result.OutputDir;
            openOutputButton.Enabled = Directory.Exists(lastOutputDir);
            progressBar.Value = progressBar.Maximum;
            statusLabel.Text = result.Errors == 0 ? "Complete" : "Complete with warnings";
            statsLabel.Text = "Extracted: " + result.Extracted + " | Size: " + FormatBytes(result.Bytes) + " | Errors: " + result.Errors;
            string reportPath = SaveReport(result);
            WriteLog("Report: " + reportPath);
            using (ResultsDialog dialog = new ResultsDialog(result, reportPath))
                dialog.ShowDialog(this);
        }

        private static string SaveReport(OperationResult result)
        {
            Directory.CreateDirectory(result.OutputDir);
            string reportPath = Path.Combine(result.OutputDir, "GameAssetTool-report.txt");
            File.WriteAllText(reportPath, result.ToReport(), new UTF8Encoding(false));
            return reportPath;
        }

        private void SetRpgmRunningState(bool running)
        {
            pathBox.Enabled = !running;
            browseButton.Enabled = !running;
            dryRunButton.Enabled = !running;
            startButton.Enabled = !running;
            unityExtractButton.Enabled = !running;
            unlockerButton.Enabled = !running;
            removeUnlockerButton.Enabled = !running;
            pauseButton.Enabled = running;
            cancelButton.Enabled = running;
            openOutputButton.Enabled = !running && Directory.Exists(lastOutputDir);
        }

        private void SetExternalRunningState(bool running, string operation)
        {
            externalRunning = running;
            BeginUi(delegate
            {
                pathBox.Enabled = !running;
                browseButton.Enabled = !running;
                dryRunButton.Enabled = !running;
                startButton.Enabled = !running;
                unityExtractButton.Enabled = !running;
                unlockerButton.Enabled = !running;
                removeUnlockerButton.Enabled = !running;
                pauseButton.Enabled = false;
                cancelButton.Enabled = running;
                openOutputButton.Enabled = !running && Directory.Exists(lastOutputDir);
                if (running)
                {
                    progressBar.Maximum = 1;
                    progressBar.Value = 0;
                    statusLabel.Text = operation + ": starting...";
                    statsLabel.Text = "";
                }
            });
        }

        private void OpenOutputFolder()
        {
            if (!Directory.Exists(lastOutputDir)) return;
            try { Process.Start(new ProcessStartInfo { FileName = lastOutputDir, UseShellExecute = true }); }
            catch (Exception ex) { WriteLog("Could not open output folder: " + ex.Message); }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (currentRun != null) currentRun.Cancel();
            lock (processSync)
            {
                try
                {
                    if (activeProcess != null && !activeProcess.HasExited)
                    {
                        activeProcess.Kill();
                        activeProcess.WaitForExit(2000);
                    }
                }
                catch { }
            }
            PortableRuntime.Cleanup();
        }

        private void BeginUi(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke((MethodInvoker)delegate { action(); });
            else action();
        }

        private void SafeLog(string message)
        {
            BeginUi(delegate { WriteLog(message); });
        }

        private void WriteLog(string message)
        {
            if (logBox.TextLength > 0) logBox.AppendText(Environment.NewLine);
            logBox.AppendText(message);
        }

        private void TryAutoDetectKey(string path)
        {
            if (!string.IsNullOrWhiteSpace(keyBox.Text)) return;
            string detected = TryFindKey(path);
            if (!string.IsNullOrWhiteSpace(detected)) keyBox.Text = detected;
        }

        private static string TryFindKey(string rootPath)
        {
            try
            {
                string systemJson = Directory.EnumerateFiles(rootPath, "System.json", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(systemJson)) return "";
                Match match = Regex.Match(File.ReadAllText(systemJson), "\"encryptionKey\":\"([0-9a-fA-F]+)\"");
                return match.Success ? match.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        private string TryReconstructKey(string rootPath)
        {
            string file = GetFilesToConvert(rootPath).FirstOrDefault(delegate(string path) { return SafeFileLength(path) > 32; });
            if (file == null) return "";
            byte[] bytes = File.ReadAllBytes(file);
            byte[] expected = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
            byte[] key = new byte[16];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(bytes[16 + i] ^ expected[i]);
            string value = BitConverter.ToString(key).Replace("-", "").ToLowerInvariant();
            WriteLog("RPGM key reconstructed: " + value);
            return value;
        }

        private static byte[] ParseKey(string input)
        {
            string key = input.Trim();
            if (key.Length < 32 || key.Length % 2 != 0)
                throw new InvalidOperationException("Invalid HEX key.");
            byte[] bytes = new byte[key.Length / 2];
            for (int i = 0; i < key.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(key.Substring(i, 2), 16);
            return bytes;
        }

        private static GameEngine DetectEngine(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return GameEngine.Unknown;
            if (IsUnityGame(rootPath)) return GameEngine.Unity;
            if (IsRenpyGame(rootPath)) return GameEngine.Renpy;
            if (HasRpgmFiles(rootPath)) return GameEngine.RpgMaker;
            if (IsGodotGame(rootPath)) return GameEngine.Godot;
            if (IsKirikiriGame(rootPath)) return GameEngine.Kirikiri;
            if (IsUnrealGame(rootPath)) return GameEngine.Unreal;
            if (IsRpgmOrNwjsGame(rootPath)) return GameEngine.Nwjs;
            return GameEngine.Unknown;
        }

        private static bool IsUnityGame(string rootPath)
        {
            try
            {
                bool hasDataFolder = Directory.EnumerateDirectories(rootPath, "*_Data", SearchOption.TopDirectoryOnly).Any();
                return hasDataFolder || File.Exists(Path.Combine(rootPath, "UnityPlayer.dll"));
            }
            catch { return false; }
        }

        private static bool IsRenpyGame(string rootPath)
        {
            string gameFolder = Path.Combine(rootPath, "game");
            if (!Directory.Exists(gameFolder)) return false;
            return EnumerateFilesSafe(gameFolder, "*.rpa").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpyc").Any()
                || File.Exists(Path.Combine(rootPath, "renpy.exe"));
        }

        private static bool IsRpgmOrNwjsGame(string rootPath)
        {
            bool hasGame = Directory.Exists(Path.Combine(rootPath, "game"));
            bool hasWww = Directory.Exists(Path.Combine(rootPath, "www"));
            bool hasPackage = File.Exists(Path.Combine(rootPath, "package.json"));
            return hasGame || hasWww || hasPackage;
        }

        private static bool HasRpgmFiles(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.rpgmvp").Any() || EnumerateFilesSafe(rootPath, "*.png_").Any();
        }

        private static bool IsGodotGame(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "project.godot"))
                || EnumerateFilesSafe(rootPath, "*.pck").Any()
                || HasGodotEmbeddedPck(rootPath);
        }

        private static bool HasGodotEmbeddedPck(string rootPath)
        {
            try
            {
                foreach (string executable in Directory.EnumerateFiles(rootPath, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    using (FileStream stream = File.OpenRead(executable))
                    {
                        if (stream.Length < 4) continue;
                        stream.Seek(-4, SeekOrigin.End);
                        byte[] footer = new byte[4];
                        if (stream.Read(footer, 0, footer.Length) == footer.Length && Encoding.ASCII.GetString(footer) == "GDPC")
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsKirikiriGame(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.xp3").Any();
        }

        private static bool IsUnrealGame(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.pak").Any() || EnumerateFilesSafe(rootPath, "*.utoc").Any();
        }

        private static ScanSummary BuildScanSummary(string rootPath)
        {
            GameEngine engine = DetectEngine(rootPath);
            IEnumerable<string> files;
            int archives;
            if (engine == GameEngine.Unity)
            {
                files = EnumerateFilesSafe(rootPath, "*.*").Where(delegate(string path)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    return ext == ".assets" || ext == ".bundle" || ext == ".ress"
                        || ext == ".png" || ext == ".jpg" || ext == ".jpeg"
                        || ext == ".mp4" || ext == ".webm" || ext == ".ogg" || ext == ".wav";
                }).ToList();
                archives = files.Count(delegate(string path)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    return ext == ".assets" || ext == ".bundle";
                });
            }
            else if (engine == GameEngine.Renpy)
            {
                files = EnumerateFilesSafe(Path.Combine(rootPath, "game"), "*.rpa").ToList();
                archives = files.Count();
            }
            else if (engine == GameEngine.Godot)
            {
                files = EnumerateFilesSafe(rootPath, "*.pck").ToList();
                archives = files.Count();
            }
            else if (engine == GameEngine.Kirikiri)
            {
                files = EnumerateFilesSafe(rootPath, "*.xp3").ToList();
                archives = files.Count();
            }
            else if (engine == GameEngine.Unreal)
            {
                files = EnumerateFilesSafe(rootPath, "*.pak").Concat(EnumerateFilesSafe(rootPath, "*.utoc")).ToList();
                archives = files.Count();
            }
            else
            {
                files = GetFilesToConvert(rootPath);
                archives = 0;
            }
            long bytes = files.Sum(delegate(string path) { return SafeFileLength(path); });
            return new ScanSummary(engine, files.Count(), archives, bytes);
        }

        private static List<string> GetFilesToConvert(string rootPath)
        {
            string[] skipped =
            {
                Path.Combine(rootPath, "www", "img", "tilesets") + Path.DirectorySeparatorChar,
                Path.Combine(rootPath, "www", "img", "weather") + Path.DirectorySeparatorChar,
                Path.Combine(rootPath, "img", "tilesets") + Path.DirectorySeparatorChar,
                Path.Combine(rootPath, "img", "weather") + Path.DirectorySeparatorChar
            };
            return EnumerateFilesSafe(rootPath, "*.*")
                .Where(delegate(string path)
                {
                    bool supported = path.EndsWith(".rpgmvp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".png_", StringComparison.OrdinalIgnoreCase);
                    return supported && !skipped.Any(delegate(string prefix) { return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase); });
                })
                .ToList();
        }

        private static IEnumerable<string> EnumerateFilesSafe(string rootPath, string pattern)
        {
            if (!Directory.Exists(rootPath)) return Enumerable.Empty<string>();
            try { return Directory.EnumerateFiles(rootPath, pattern, SearchOption.AllDirectories).ToList(); }
            catch { return Enumerable.Empty<string>(); }
        }

        private static List<string> FindUnityBundleFiles(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.bundle").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string TryFindGameRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            DirectoryInfo current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
            while (current != null)
            {
                string root = current.FullName;
                bool known = IsUnityGame(root)
                    || IsGodotGame(root)
                    || IsKirikiriGame(root)
                    || IsUnrealGame(root)
                    || Directory.Exists(Path.Combine(root, "www"))
                    || Directory.Exists(Path.Combine(root, "game"))
                    || File.Exists(Path.Combine(root, "package.json"));
                if (known) return root;
                current = current.Parent;
            }
            return null;
        }

        private string UnityModeValue()
        {
            switch (unityExtractModeBox.SelectedIndex)
            {
                case 0: return "textures";
                case 1: return "videos";
                case 2: return "audios";
                case 3: return "meshes";
                default: return "all";
            }
        }

        private static string EngineName(GameEngine engine)
        {
            switch (engine)
            {
                case GameEngine.RpgMaker: return "RPG Maker MV/MZ";
                case GameEngine.Renpy: return "Ren'Py";
                case GameEngine.Unity: return "Unity";
                case GameEngine.Godot: return "Godot";
                case GameEngine.Kirikiri: return "KiriKiri XP3";
                case GameEngine.Unreal: return "Unreal experimental";
                case GameEngine.Nwjs: return "NWJS";
                default: return "not detected";
            }
        }

        private Color EngineColor(GameEngine engine)
        {
            switch (engine)
            {
                case GameEngine.Unity: return Color.FromArgb(160, 125, 255);
                case GameEngine.Renpy: return pinkColor;
                case GameEngine.RpgMaker: return accentColor;
                case GameEngine.Godot: return Color.FromArgb(71, 140, 191);
                case GameEngine.Kirikiri: return Color.FromArgb(255, 155, 95);
                case GameEngine.Unreal: return Color.FromArgb(178, 178, 190);
                case GameEngine.Nwjs: return Color.FromArgb(255, 183, 77);
                default: return mutedColor;
            }
        }

        private static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static int ParseInt(string value, int index)
        {
            int result;
            string[] parts = value.Split(':');
            return parts.Length > index && int.TryParse(parts[index], out result) ? result : 0;
        }

        private static long ParseLong(string value, int index)
        {
            long result;
            string[] parts = value.Split(':');
            return parts.Length > index && long.TryParse(parts[index], out result) ? result : 0;
        }

        private static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static FileStats GetFileStats(string rootPath)
        {
            List<string> files = EnumerateFilesSafe(rootPath, "*.*").Where(delegate(string file)
            {
                return !file.EndsWith("GameAssetTool-report.txt", StringComparison.OrdinalIgnoreCase);
            }).ToList();
            return new FileStats(files.Count, files.Sum(delegate(string file) { return SafeFileLength(file); }));
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024d).ToString("N1") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024d * 1024d)).ToString("N1") + " MB";
            return (bytes / (1024d * 1024d * 1024d)).ToString("N2") + " GB";
        }

        private static string FormatDuration(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(seconds, 0));
            if (span.TotalHours >= 1) return string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
            return string.Format("{0:00}:{1:00}", span.Minutes, span.Seconds);
        }

        private static string MakeRelativePath(string rootPath, string path)
        {
            Uri root = new Uri(AppendDirectorySeparator(rootPath));
            Uri file = new Uri(path);
            return Uri.UnescapeDataString(root.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private static string SanitizeRelativePath(string path)
        {
            string[] parts = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string[] cleaned = parts.Select(delegate(string part)
            {
                string value = string.Concat(part.Where(delegate(char c) { return !Path.GetInvalidFileNameChars().Contains(c); }));
                return string.IsNullOrWhiteSpace(value) || value == "." || value == ".." ? "archive" : value;
            }).ToArray();
            return cleaned.Length == 0 ? "archive" : Path.Combine(cleaned);
        }

        private static string GetUniqueDirectoryPath(string path, out bool renamed)
        {
            string candidate = path;
            int suffix = 2;
            while (Directory.Exists(candidate))
            {
                candidate = path + " (" + suffix + ")";
                suffix++;
            }
            renamed = !string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase);
            return candidate;
        }

        private enum GameEngine
        {
            Unknown,
            RpgMaker,
            Renpy,
            Unity,
            Godot,
            Kirikiri,
            Unreal,
            Nwjs
        }

        private sealed class ScanSummary
        {
            public ScanSummary(GameEngine engine, int fileCount, int archiveCount, long totalBytes)
            {
                Engine = engine;
                FileCount = fileCount;
                ArchiveCount = archiveCount;
                TotalBytes = totalBytes;
            }

            public GameEngine Engine { get; private set; }
            public int FileCount { get; private set; }
            public int ArchiveCount { get; private set; }
            public long TotalBytes { get; private set; }
        }

        private sealed class FileStats
        {
            public FileStats(int count, long bytes)
            {
                Count = count;
                Bytes = bytes;
            }

            public int Count { get; private set; }
            public long Bytes { get; private set; }
        }

        private sealed class OperationResult
        {
            public OperationResult(string engine, string outputDir, int extracted, long bytes, int errors, int renamed, TimeSpan duration)
            {
                Engine = engine;
                OutputDir = outputDir;
                Extracted = extracted;
                Bytes = bytes;
                Errors = errors;
                Renamed = renamed;
                Duration = duration;
            }

            public string Engine { get; private set; }
            public string OutputDir { get; private set; }
            public int Extracted { get; private set; }
            public long Bytes { get; private set; }
            public int Errors { get; private set; }
            public int Renamed { get; private set; }
            public TimeSpan Duration { get; private set; }

            public static OperationResult Failed(string engine, string outputDir, string message)
            {
                return new OperationResult(engine + " - " + message, outputDir, 0, 0, 1, 0, TimeSpan.Zero);
            }

            public string ToReport()
            {
                return string.Join(Environment.NewLine, new[]
                {
                    "Game Asset Tool v1.5 report",
                    "Engine: " + Engine,
                    "Extracted files: " + Extracted,
                    "Extracted size: " + FormatBytes(Bytes),
                    "Renamed conflicts: " + Renamed,
                    "Errors: " + Errors,
                    "Elapsed: " + FormatDuration(Duration.TotalSeconds),
                    "Output: " + OutputDir,
                    "Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }

        private sealed class ResultsDialog : Form
        {
            public ResultsDialog(OperationResult result, string reportPath)
            {
                Text = "Extraction Results";
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(620, 390);
                MinimumSize = new Size(620, 390);
                BackColor = Color.FromArgb(17, 19, 24);
                ForeColor = Color.FromArgb(239, 243, 248);

                Controls.Add(new Label
                {
                    Text = result.Errors == 0 ? "Extraction complete" : "Extraction complete with warnings",
                    Location = new Point(20, 18),
                    Size = new Size(560, 30),
                    Font = new Font("Segoe UI Semibold", 14f),
                    ForeColor = result.Errors == 0 ? Color.FromArgb(70, 204, 120) : Color.FromArgb(255, 183, 77)
                });

                TextBox summary = new TextBox
                {
                    Location = new Point(20, 62),
                    Size = new Size(560, 210),
                    Multiline = true,
                    ReadOnly = true,
                    BackColor = Color.FromArgb(10, 12, 16),
                    ForeColor = Color.FromArgb(239, 243, 248),
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 10f),
                    Text = result.ToReport() + Environment.NewLine + "Report: " + reportPath
                };
                Controls.Add(summary);

                Button openButton = new Button
                {
                    Text = "Open Output Folder",
                    Location = new Point(20, 292),
                    Size = new Size(180, 34),
                    BackColor = Color.FromArgb(68, 197, 255),
                    FlatStyle = FlatStyle.Flat
                };
                openButton.Click += delegate
                {
                    try { Process.Start(new ProcessStartInfo { FileName = result.OutputDir, UseShellExecute = true }); }
                    catch { }
                };
                Controls.Add(openButton);

                Button closeButton = new Button
                {
                    Text = "Close",
                    Location = new Point(470, 292),
                    Size = new Size(110, 34),
                    BackColor = Color.FromArgb(45, 50, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                closeButton.Click += delegate { Close(); };
                Controls.Add(closeButton);
            }
        }

        private sealed class ConversionRun
        {
            private static readonly object outputPathLock = new object();
            private static readonly HashSet<string> outputPathReservations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentQueue<string> queue;
            private readonly byte[] keyBytes;
            private readonly ManualResetEventSlim pauseGate = new ManualResetEventSlim(true);
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private int processedCount;
            private int errorCount;
            private int renamedCount;
            private long totalBytes;

            public ConversionRun(string rootPath, List<string> files, byte[] keyBytes)
            {
                RootPath = rootPath;
                TotalCount = files.Count;
                this.keyBytes = keyBytes;
                queue = new ConcurrentQueue<string>(files);
                OutputDir = Path.Combine(rootPath, "extracted", "rpgm");
                Directory.CreateDirectory(OutputDir);
                StartUtc = DateTime.UtcNow;
                Completion = Task.FromResult(0);
            }

            public string RootPath { get; private set; }
            public string OutputDir { get; private set; }
            public int TotalCount { get; private set; }
            public int ProcessedCount { get { return processedCount; } }
            public int ErrorCount { get { return errorCount; } }
            public int RenamedCount { get { return renamedCount; } }
            public long TotalBytes { get { return totalBytes; } }
            public bool IsPaused { get; private set; }
            public DateTime StartUtc { get; private set; }
            public Task Completion { get; private set; }

            public void Start()
            {
                int workers = Math.Min(Math.Max(Environment.ProcessorCount, 2), Math.Min(8, TotalCount));
                Completion = Task.WhenAll(Enumerable.Range(0, workers).Select(delegate(int _) { return Task.Run((Action)ProcessQueue); }));
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
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(filePath);
                        if (bytes.Length <= 16) throw new InvalidDataException("File too short: " + filePath);
                        byte[] data = new byte[bytes.Length - 16];
                        Buffer.BlockCopy(bytes, 16, data, 0, data.Length);
                        for (int i = 0; i < 16 && i < data.Length; i++) data[i] ^= keyBytes[i];

                        string relative = filePath.Substring(RootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string outputPath = Path.ChangeExtension(Path.Combine(OutputDir, relative), ".png");
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                        bool renamed;
                        outputPath = GetUniqueFilePath(outputPath, out renamed);
                        if (renamed) Interlocked.Increment(ref renamedCount);
                        File.WriteAllBytes(outputPath, data);
                        Interlocked.Add(ref totalBytes, data.Length);
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

            private static string GetUniqueFilePath(string path, out bool renamed)
            {
                lock (outputPathLock)
                {
                    string candidate = path;
                    string directory = Path.GetDirectoryName(path);
                    string filename = Path.GetFileNameWithoutExtension(path);
                    string extension = Path.GetExtension(path);
                    int suffix = 2;
                    while (File.Exists(candidate) || outputPathReservations.Contains(candidate))
                    {
                        candidate = Path.Combine(directory, filename + " (" + suffix + ")" + extension);
                        suffix++;
                    }
                    outputPathReservations.Add(candidate);
                    renamed = !string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase);
                    return candidate;
                }
            }
        }
    }
}
