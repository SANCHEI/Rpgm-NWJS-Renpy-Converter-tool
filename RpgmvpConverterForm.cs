using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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
        private Label keyLabel;
        private Label unityModeLabel;
        private Label extractionHintLabel;
        private Label detectedEngineLabel;
        private Label scanSummaryLabel;
        private Label statusLabel;
        private Label statsLabel;
        private Label runtimeStatusLabel;
        private Label unlockerSectionLabel;
        private TextBox logBox;
        private Panel logPanel;
        private ProgressBar progressBar;
        private Button browseButton;
        private Button dryRunButton;
        private Button startButton;
        private Button unlockerButton;
        private Button removeUnlockerButton;
        private Button pauseButton;
        private Button cancelButton;
        private Button openOutputButton;
        private Button toggleLogButton;
        private ToolTip actionToolTip;

        private readonly object processSync = new object();
        private readonly object runtimeWarmupSync = new object();
        private readonly List<Panel> dragHighlightPanels = new List<Panel>();
        private System.Windows.Forms.Timer uiTimer;
        private ConversionRun currentRun;
        private Task runtimeWarmupTask;
        private Process activeProcess;
        private bool externalRunning;
        private bool closing;
        private bool logExpanded;
        private bool unlockerLayoutVisible = true;
        private bool localCopyCancellationRequested;
        private string lastOutputDir = "";
        private GameEngine selectedEngine;

        private const int CompactClientHeight = 570;
        private const int ExpandedClientHeight = 872;
        private const int UnlockerSectionHeight = 64;

        public RpgmvpConverterForm()
        {
            BuildUi();

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); }
                catch { }
            }

            Shown += delegate { BeginInvoke((MethodInvoker)TryApplyStartupGamePath); };
        }

        private void BuildUi()
        {
            Font uiFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            Font uiBold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            Font titleFont = new Font("Segoe UI Semibold", 14f, FontStyle.Regular);
            Font logFont = new Font("Consolas", 9.5f, FontStyle.Regular);

            Text = "Game Asset Tool v1.6.1";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(928, CompactClientHeight);
            BackColor = formBack;
            ForeColor = textColor;
            Font = uiFont;
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragLeave += OnDragLeave;
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
            runtimeStatusLabel = new Label
            {
                Text = "Runtime: waiting for a supported folder",
                ForeColor = mutedColor,
                Location = new Point(620, 24),
                Size = new Size(290, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            header.Controls.Add(runtimeStatusLabel);

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

            Controls.Add(CreateSectionLabel("Extract Assets", y));
            y += 22;

            Panel extractionPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(892, 72),
                BackColor = panelBack,
                Padding = new Padding(8)
            };
            Controls.Add(extractionPanel);
            extractionHintLabel = new Label
            {
                Text = "Select a supported game folder to see its extraction options.",
                Location = new Point(8, 8),
                Size = new Size(680, 20),
                ForeColor = mutedColor
            };
            extractionPanel.Controls.Add(extractionHintLabel);
            keyLabel = new Label
            {
                Text = "Optional key",
                Location = new Point(8, 40),
                Size = new Size(100, 20),
                ForeColor = mutedColor,
                Visible = false
            };
            extractionPanel.Controls.Add(keyLabel);
            keyBox = new TextBox
            {
                Location = new Point(112, 36),
                Size = new Size(574, 26),
                BackColor = inputBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = uiFont,
                Visible = false
            };
            extractionPanel.Controls.Add(keyBox);
            unityModeLabel = new Label
            {
                Text = "Asset type",
                Location = new Point(8, 40),
                Size = new Size(100, 20),
                ForeColor = mutedColor,
                Visible = false
            };
            extractionPanel.Controls.Add(unityModeLabel);
            unityExtractModeBox = new ComboBox
            {
                Location = new Point(112, 36),
                Size = new Size(180, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            unityExtractModeBox.Items.Add("Textures");
            unityExtractModeBox.Items.Add("Videos");
            unityExtractModeBox.Items.Add("Audio");
            unityExtractModeBox.Items.Add("Meshes");
            unityExtractModeBox.Items.Add("All");
            unityExtractModeBox.SelectedIndex = 4;
            extractionPanel.Controls.Add(unityExtractModeBox);
            startButton = CreateButton("Extract Assets", new Point(704, 20), new Size(180, 34), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartDetectedExtractionAsync(); };
            extractionPanel.Controls.Add(startButton);
            y += 82;

            unlockerSectionLabel = CreateSectionLabel("Gallery Unlocker for Ren'Py", y);
            Controls.Add(unlockerSectionLabel);
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
            toggleLogButton = CreateButton("Show Log", new Point(426, y), new Size(120, 30), Color.FromArgb(45, 50, 60), textColor, uiBold);
            toggleLogButton.Click += delegate { ToggleLog(); };
            Controls.Add(toggleLogButton);
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

            logPanel = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(892, 286),
                BackColor = logBack,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
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
                Size = new Size(870, 238),
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
            actionToolTip = new ToolTip
            {
                AutoPopDelay = 6000,
                InitialDelay = 350,
                ReshowDelay = 150,
                ShowAlways = true
            };
            ConfigureActionTooltips();
            CreateDragHighlightBorders();
            UpdateEngineContext(GameEngine.Unknown);
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
            ReadableButton button = new ReadableButton
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = foreColor,
                ActiveBackColor = backColor,
                ActiveForeColor = foreColor,
                DisabledBackColor = Color.FromArgb(42, 48, 58),
                DisabledForeColor = Color.FromArgb(190, 200, 212),
                BorderColor = border,
                Font = font,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
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
            {
                e.Effect = DragDropEffects.Copy;
                SetDragHighlight(true);
            }
            else
            {
                SetDragHighlight(false);
            }
        }

        private void OnDragLeave(object sender, EventArgs e)
        {
            SetDragHighlight(false);
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            SetDragHighlight(false);
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

        private void TryApplyStartupGamePath()
        {
            if (closing || IsDisposed) return;
            string initialRoot = TryFindGameRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(initialRoot))
                ApplyGamePath(initialRoot, false);
        }

        private void OnPathChanged()
        {
            string path = pathBox.Text.Trim();
            if (!Directory.Exists(path))
            {
                detectedEngineLabel.Text = "Engine: not detected";
                detectedEngineLabel.ForeColor = mutedColor;
                scanSummaryLabel.Text = "Select a folder or drop it into this window.";
                UpdateEngineContext(GameEngine.Unknown);
                return;
            }

            TryAutoDetectKey(path);
            GameEngine engine = DetectEngineFast(path);
            detectedEngineLabel.Text = "Engine: " + EngineName(engine);
            detectedEngineLabel.ForeColor = EngineColor(engine);
            scanSummaryLabel.Text = "Ready to scan. Click Dry Run / Scan to inspect files before extraction.";
            UpdateEngineContext(engine);
            WarmPortableRuntimeInBackground(engine);
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
                    "{0} archive(s), {1} candidate file(s), input size {2} (not estimated output)",
                    summary.ArchiveCount,
                    summary.FileCount,
                    FormatBytes(summary.TotalBytes));
                UpdateEngineContext(summary.Engine);
                if (showLog)
                {
                    WriteLog("Dry run: " + EngineName(summary.Engine));
                    WriteLog("Found: " + summary.ArchiveCount + " archive(s), " + summary.FileCount + " candidate file(s), input size " + FormatBytes(summary.TotalBytes));
                }
                WarmPortableRuntimeInBackground(summary.Engine);
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
            if (engine == GameEngine.Nwjs)
            {
                await StartNwjsExtractionAsync();
                return;
            }
            if (engine != GameEngine.RpgMaker)
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
            int skipped = 0;

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
                    skipped = ParseInt(line, 5);
                }
                else
                {
                    SafeLog(line);
                }
            });

            if (exitCode != 0 && errors == 0) errors = 1;
            return new OperationResult("Unity", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private async Task StartNwjsExtractionAsync()
        {
            if (currentRun != null || externalRunning) return;

            string rootPath = pathBox.Text.Trim();
            if (!Directory.Exists(rootPath) || !IsNwjsGame(rootPath))
            {
                MessageBox.Show("No NWJS game found. Select a folder containing www, package.json, package.nw or app.nw.", "NWJS Extractor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputDir = Path.Combine(rootPath, "extracted", "nwjs");
            lastOutputDir = outputDir;
            localCopyCancellationRequested = false;
            SetExternalRunningState(true, "NWJS");
            WriteLog("NWJS file extraction started");

            OperationResult result;
            try
            {
                result = await Task.Run(delegate { return RunNwjsExtraction(rootPath, outputDir); });
            }
            catch (OperationCanceledException)
            {
                result = OperationResult.Failed("NWJS", outputDir, "cancelled");
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed("NWJS", outputDir, ex.Message);
            }
            SetExternalRunningState(false, "NWJS");
            CompleteExternalOperation(result);
        }

        private OperationResult RunNwjsExtraction(string rootPath, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> looseFiles = GetNwjsLooseFiles(rootPath, outputDir);
            List<string> archives = FindNwjsPackageArchives(rootPath);
            int total = looseFiles.Count + archives.Count;
            int processed = 0;
            int extracted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;
            int skipped = 0;

            BeginUi(delegate
            {
                progressBar.Maximum = Math.Max(total, 1);
                progressBar.Value = 0;
                statusLabel.Text = "NWJS: found " + looseFiles.Count + " loose file(s), " + archives.Count + " archive(s)";
            });

            foreach (string source in looseFiles)
            {
                ThrowIfLocalCopyCancelled();
                try
                {
                    string relative = MakeRelativePath(rootPath, source);
                    string destination = GetSafeOutputPath(outputDir, Path.Combine("loose", relative));
                    bool collision;
                    destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination);
                    extracted++;
                    bytes += SafeFileLength(destination);
                    if (collision) renamed++;
                }
                catch (Exception ex)
                {
                    errors++;
                    SafeLog("WARN:" + source + ":" + ex.Message);
                }
                processed++;
                UpdateNwjsProgress(processed, total, bytes);
            }

            foreach (string archive in archives)
            {
                ThrowIfLocalCopyCancelled();
                try
                {
                    NwjsCopyStats stats = ExtractNwjsZipArchive(archive, outputDir);
                    extracted += stats.Extracted;
                    bytes += stats.Bytes;
                    renamed += stats.Renamed;
                    skipped += stats.Skipped;
                }
                catch (InvalidDataException)
                {
                    skipped++;
                    SafeLog("NWJS package is not ZIP-compatible and was skipped: " + archive);
                }
                catch (Exception ex)
                {
                    errors++;
                    SafeLog("WARN:" + archive + ":" + ex.Message);
                }
                processed++;
                UpdateNwjsProgress(processed, total, bytes);
            }

            return new OperationResult("NWJS", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private void UpdateNwjsProgress(int processed, int total, long bytes)
        {
            BeginUi(delegate
            {
                progressBar.Maximum = Math.Max(total, 1);
                progressBar.Value = Math.Min(processed, progressBar.Maximum);
                statusLabel.Text = "NWJS: " + processed + " / " + total;
                statsLabel.Text = "Sources: " + processed + " / " + total + " | Size: " + FormatBytes(bytes);
            });
        }

        private void ThrowIfLocalCopyCancelled()
        {
            if (localCopyCancellationRequested)
                throw new OperationCanceledException();
        }

        private NwjsCopyStats ExtractNwjsZipArchive(string archivePath, string outputDir)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            string archiveName = Path.GetFileNameWithoutExtension(archivePath);
            using (FileStream stream = File.OpenRead(archivePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    ThrowIfLocalCopyCancelled();
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        skipped++;
                        continue;
                    }

                    string destination = GetSafeOutputPath(outputDir, Path.Combine("archives", archiveName, entry.FullName));
                    bool collision;
                    destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream input = entry.Open())
                    using (FileStream output = File.Create(destination))
                        input.CopyTo(output);
                    extracted++;
                    bytes += SafeFileLength(destination);
                    if (collision) renamed++;
                }
            }
            return new NwjsCopyStats(extracted, bytes, renamed, skipped);
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
            if (!CanInstallUnlocker(rootPath))
            {
                MessageBox.Show("Unlocker works with Ren'Py game folders.", "Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private static bool CanInstallUnlocker(string rootPath)
        {
            return Directory.Exists(rootPath) && IsRenpyGame(rootPath);
        }

        private bool EnsurePortableRuntimeAvailable()
        {
            try
            {
                statusLabel.Text = "Preparing built-in runtime...";
                SetRuntimeStatus("Runtime: preparing silently...", warningColor);
                Task warmup;
                lock (runtimeWarmupSync) warmup = runtimeWarmupTask;
                if (warmup != null && !warmup.IsCompleted)
                    warmup.Wait();
                PortableRuntime.EnsureExtracted();
                SetRuntimeStatus("Runtime: ready", successColor);
                return true;
            }
            catch (Exception ex)
            {
                SetRuntimeStatus("Runtime: unavailable", dangerColor);
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
            localCopyCancellationRequested = true;
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
            statsLabel.Text = "Extracted: " + result.Extracted + " | Size: " + FormatBytes(result.Bytes) + " | Errors: " + result.Errors
                + (result.Skipped > 0 ? " | Skipped: " + result.Skipped : "");
            UpdateActionTooltips();
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
            UpdateUnlockerControls(running);
            pauseButton.Enabled = running;
            cancelButton.Enabled = running;
            openOutputButton.Enabled = !running && Directory.Exists(lastOutputDir);
            if (!running) UpdateEngineContext(selectedEngine);
            else UpdateActionTooltips();
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
                UpdateUnlockerControls(running);
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
                else
                {
                    UpdateEngineContext(selectedEngine);
                }
                UpdateActionTooltips();
            });
        }

        private void ToggleLog()
        {
            logExpanded = !logExpanded;
            logPanel.Visible = logExpanded;
            toggleLogButton.Text = logExpanded ? "Hide Log" : "Show Log";
            UpdateWindowHeight();
            UpdateActionTooltips();
        }

        private void UpdateEngineContext(GameEngine engine)
        {
            selectedEngine = engine;
            bool busy = currentRun != null || externalRunning;
            bool showKey = engine == GameEngine.RpgMaker || engine == GameEngine.Unreal;
            bool showUnityMode = engine == GameEngine.Unity;

            keyLabel.Visible = showKey;
            keyBox.Visible = showKey;
            unityModeLabel.Visible = showUnityMode;
            unityExtractModeBox.Visible = showUnityMode;
            startButton.Enabled = !busy && CanExtractAssets(engine);
            UpdateUnlockerControls(busy);

            switch (engine)
            {
                case GameEngine.RpgMaker:
                    keyLabel.Text = "RPGM HEX key";
                    extractionHintLabel.Text = "Encrypted image assets. The key is detected automatically when possible.";
                    break;
                case GameEngine.Unity:
                    extractionHintLabel.Text = "Choose the Unity asset types to export.";
                    break;
                case GameEngine.Renpy:
                    extractionHintLabel.Text = "RPA archives will be extracted into separate folders.";
                    break;
                case GameEngine.Godot:
                    extractionHintLabel.Text = "Standard unencrypted PCK archives will be extracted.";
                    break;
                case GameEngine.Kirikiri:
                    extractionHintLabel.Text = "Standard unencrypted XP3 archives will be extracted.";
                    break;
                case GameEngine.Unreal:
                    keyLabel.Text = "Unreal AES key";
                    extractionHintLabel.Text = "Experimental PAK extraction. AES key is optional; Oodle and IoStore are reported.";
                    break;
                case GameEngine.Nwjs:
                    extractionHintLabel.Text = "NWJS files will be copied. ZIP-compatible package.nw archives will be unpacked.";
                    break;
                default:
                    extractionHintLabel.Text = "Select a supported game folder to see its extraction options.";
                    break;
            }

            UpdateRuntimeStatusForEngine(engine);
            UpdateActionTooltips();
        }

        private void UpdateUnlockerControls(bool busy)
        {
            string rootPath = pathBox.Text.Trim();
            bool installed = GetUnlockerDirectories(rootPath).Any();
            UpdateUnlockerLayout(selectedEngine == GameEngine.Renpy || installed);
            bool canInstall = !busy && selectedEngine == GameEngine.Renpy && Directory.Exists(rootPath);
            unlockerModeBox.Enabled = canInstall;
            unlockerButton.Enabled = canInstall;
            removeUnlockerButton.Enabled = !busy && installed;
        }

        private static bool CanExtractAssets(GameEngine engine)
        {
            return engine == GameEngine.RpgMaker
                || engine == GameEngine.Renpy
                || engine == GameEngine.Unity
                || engine == GameEngine.Godot
                || engine == GameEngine.Kirikiri
                || engine == GameEngine.Unreal
                || engine == GameEngine.Nwjs;
        }

        private void UpdateUnlockerLayout(bool visible)
        {
            unlockerSectionLabel.Visible = visible;
            unlockerModeBox.Visible = visible;
            unlockerButton.Visible = visible;
            removeUnlockerButton.Visible = visible;
            if (unlockerLayoutVisible == visible)
            {
                UpdateWindowHeight();
                return;
            }

            int offset = visible ? ScaleLogicalHeight(UnlockerSectionHeight) : -ScaleLogicalHeight(UnlockerSectionHeight);
            foreach (Control control in new Control[] { pauseButton, cancelButton, openOutputButton, toggleLogButton, progressBar, statusLabel, statsLabel, logPanel })
                control.Top += offset;
            unlockerLayoutVisible = visible;
            UpdateWindowHeight();
        }

        private void UpdateWindowHeight()
        {
            int logicalHeight = logExpanded ? ExpandedClientHeight : CompactClientHeight;
            if (!unlockerLayoutVisible) logicalHeight -= UnlockerSectionHeight;
            ClientSize = new Size(ClientSize.Width, ScaleLogicalHeight(logicalHeight));
        }

        private int ScaleLogicalHeight(int logicalHeight)
        {
            float currentDpi = CurrentAutoScaleDimensions.Height;
            return ScaleLogicalHeightForDpi(logicalHeight, currentDpi);
        }

        private static int ScaleLogicalHeightForDpi(int logicalHeight, float dpi)
        {
            return (int)Math.Round(logicalHeight * (dpi > 0 ? dpi / 96f : 1f));
        }

        private void ConfigureActionTooltips()
        {
            SetActionTooltip(pathBox, "Drop a game folder here or choose it with Browse.");
            SetActionTooltip(browseButton, "Select the root folder of a game.");
            SetActionTooltip(dryRunButton, "Inspect supported archives and estimate the input size without extracting files.");
            SetActionTooltip(toggleLogButton, "Show or hide technical extraction messages.");
            UpdateActionTooltips();
        }

        private void UpdateActionTooltips()
        {
            if (actionToolTip == null) return;
            bool busy = currentRun != null || externalRunning;
            SetActionTooltip(startButton, busy
                ? "Wait for the current operation to finish."
                : CanExtractAssets(selectedEngine)
                    ? "Extract supported assets for the detected engine."
                    : "Select a supported game folder first.");
            SetActionTooltip(unlockerButton, selectedEngine == GameEngine.Renpy
                ? "Install the Ren'Py gallery unlocker. Try Soft mode first."
                : "The gallery unlocker is available only for detected Ren'Py folders.");
            SetActionTooltip(removeUnlockerButton, removeUnlockerButton.Enabled
                ? "Remove previously installed Ren'Py gallery unlocker files."
                : "No installed Ren'Py gallery unlocker was found.");
            SetActionTooltip(pauseButton, "Pause or resume RPG Maker asset conversion.");
            SetActionTooltip(cancelButton, busy ? "Stop the current operation." : "No operation is currently running.");
            SetActionTooltip(openOutputButton, Directory.Exists(lastOutputDir)
                ? "Open the most recent extraction output folder."
                : "Run an extraction first to create an output folder.");
        }

        private void SetActionTooltip(Control control, string text)
        {
            if (control == null || actionToolTip == null) return;
            actionToolTip.SetToolTip(control, text);
            control.AccessibleDescription = text;
        }

        private void CreateDragHighlightBorders()
        {
            const int thickness = 4;
            dragHighlightPanels.Add(new Panel { Location = new Point(0, 0), Size = new Size(ClientSize.Width, thickness), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            dragHighlightPanels.Add(new Panel { Location = new Point(0, ClientSize.Height - thickness), Size = new Size(ClientSize.Width, thickness), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right });
            dragHighlightPanels.Add(new Panel { Location = new Point(0, 0), Size = new Size(thickness, ClientSize.Height), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left });
            dragHighlightPanels.Add(new Panel { Location = new Point(ClientSize.Width - thickness, 0), Size = new Size(thickness, ClientSize.Height), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right });
            foreach (Panel panel in dragHighlightPanels)
            {
                panel.BackColor = accentColor;
                panel.Enabled = false;
                panel.Visible = false;
                Controls.Add(panel);
                panel.BringToFront();
            }
        }

        private void SetDragHighlight(bool visible)
        {
            foreach (Panel panel in dragHighlightPanels)
            {
                panel.Visible = visible;
                if (visible) panel.BringToFront();
            }
        }

        private void UpdateRuntimeStatusForEngine(GameEngine engine)
        {
            if (!UsesPortableRuntime(engine))
            {
                SetRuntimeStatus(engine == GameEngine.Unknown ? "Runtime: waiting for a supported folder" : "Runtime: not needed", mutedColor);
                return;
            }
            if (PortableRuntime.IsReady)
            {
                SetRuntimeStatus("Runtime: ready", successColor);
                return;
            }

            Task warmup;
            lock (runtimeWarmupSync) warmup = runtimeWarmupTask;
            SetRuntimeStatus(warmup != null && !warmup.IsCompleted ? "Runtime: preparing silently..." : "Runtime: preparing after selection", warningColor);
        }

        private void SetRuntimeStatus(string text, Color color)
        {
            if (runtimeStatusLabel == null) return;
            runtimeStatusLabel.Text = text;
            runtimeStatusLabel.ForeColor = color;
        }

        private void OpenOutputFolder()
        {
            if (!Directory.Exists(lastOutputDir)) return;
            try { Process.Start(new ProcessStartInfo { FileName = lastOutputDir, UseShellExecute = true }); }
            catch (Exception ex) { WriteLog("Could not open output folder: " + ex.Message); }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            closing = true;
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
            Task warmup;
            lock (runtimeWarmupSync) warmup = runtimeWarmupTask;
            if (warmup != null)
            {
                try { warmup.Wait(); }
                catch { }
            }
            PortableRuntime.Cleanup();
        }

        private void WarmPortableRuntimeInBackground(GameEngine engine)
        {
            if (!UsesPortableRuntime(engine) || closing) return;
            if (PortableRuntime.IsReady)
            {
                SetRuntimeStatus("Runtime: ready", successColor);
                return;
            }
            lock (runtimeWarmupSync)
            {
                if (runtimeWarmupTask != null) return;
                SetRuntimeStatus("Runtime: preparing silently...", warningColor);
                runtimeWarmupTask = Task.Run(delegate
                {
                    bool ready = false;
                    try
                    {
                        PortableRuntime.EnsureExtracted();
                        ready = true;
                    }
                    catch { }
                    BeginUi(delegate
                    {
                        SetRuntimeStatus(ready ? "Runtime: ready" : "Runtime: unavailable", ready ? successColor : dangerColor);
                    });
                });
            }
        }

        private static bool UsesPortableRuntime(GameEngine engine)
        {
            return engine == GameEngine.Renpy
                || engine == GameEngine.Unity
                || engine == GameEngine.Godot
                || engine == GameEngine.Kirikiri
                || engine == GameEngine.Unreal;
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
            string detected = TryFindKeyFast(path);
            if (!string.IsNullOrWhiteSpace(detected)) keyBox.Text = detected;
        }

        private static string TryFindKey(string rootPath)
        {
            try
            {
                string detected = TryFindKeyFast(rootPath);
                if (!string.IsNullOrWhiteSpace(detected)) return detected;
                string systemJson = Directory.EnumerateFiles(rootPath, "System.json", SearchOption.AllDirectories).FirstOrDefault();
                return ReadEncryptionKey(systemJson);
            }
            catch { return ""; }
        }

        private static string TryFindKeyFast(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return "";
            string[] candidates =
            {
                Path.Combine(rootPath, "System.json"),
                Path.Combine(rootPath, "data", "System.json"),
                Path.Combine(rootPath, "www", "data", "System.json")
            };
            foreach (string candidate in candidates)
            {
                string key = ReadEncryptionKey(candidate);
                if (!string.IsNullOrWhiteSpace(key)) return key;
            }
            return "";
        }

        private static string ReadEncryptionKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                Match match = Regex.Match(File.ReadAllText(path), "\"encryptionKey\":\"([0-9a-fA-F]+)\"");
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
            if (IsNwjsGame(rootPath)) return GameEngine.Nwjs;
            return GameEngine.Unknown;
        }

        private static GameEngine DetectEngineFast(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return GameEngine.Unknown;
            if (IsUnityGame(rootPath)) return GameEngine.Unity;
            if (IsRenpyGameFast(rootPath)) return GameEngine.Renpy;
            if (HasRpgmFilesFast(rootPath)) return GameEngine.RpgMaker;
            if (IsGodotGameFast(rootPath)) return GameEngine.Godot;
            if (IsKirikiriGameFast(rootPath)) return GameEngine.Kirikiri;
            if (IsUnrealGameFast(rootPath)) return GameEngine.Unreal;
            if (IsNwjsGame(rootPath)) return GameEngine.Nwjs;
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

        private static bool IsRenpyGameFast(string rootPath)
        {
            string gameFolder = Path.Combine(rootPath, "game");
            if (!Directory.Exists(gameFolder)) return false;
            return EnumerateFilesTopLevelSafe(gameFolder, "*.rpa").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpyc").Any()
                || File.Exists(Path.Combine(rootPath, "renpy.exe"));
        }

        private static bool IsNwjsGame(string rootPath)
        {
            bool hasWww = Directory.Exists(Path.Combine(rootPath, "www"));
            bool hasPackage = File.Exists(Path.Combine(rootPath, "package.json"));
            bool hasPackageNw = File.Exists(Path.Combine(rootPath, "package.nw")) || Directory.Exists(Path.Combine(rootPath, "package.nw"));
            bool hasAppNw = File.Exists(Path.Combine(rootPath, "app.nw")) || Directory.Exists(Path.Combine(rootPath, "app.nw"));
            return hasWww || hasPackage || hasPackageNw || hasAppNw;
        }

        private static bool HasRpgmFiles(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.rpgmvp").Any() || EnumerateFilesSafe(rootPath, "*.png_").Any();
        }

        private static bool HasRpgmFilesFast(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "data", "System.json"))
                || File.Exists(Path.Combine(rootPath, "www", "data", "System.json"))
                || EnumerateFilesTopLevelSafe(rootPath, "*.rpgmvp").Any()
                || EnumerateFilesTopLevelSafe(rootPath, "*.png_").Any();
        }

        private static bool IsGodotGame(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "project.godot"))
                || EnumerateFilesSafe(rootPath, "*.pck").Any()
                || HasGodotEmbeddedPck(rootPath);
        }

        private static bool IsGodotGameFast(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "project.godot"))
                || EnumerateFilesTopLevelSafe(rootPath, "*.pck").Any()
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

        private static bool IsKirikiriGameFast(string rootPath)
        {
            return EnumerateFilesTopLevelSafe(rootPath, "*.xp3").Any();
        }

        private static bool IsUnrealGame(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.pak").Any() || EnumerateFilesSafe(rootPath, "*.utoc").Any();
        }

        private static bool IsUnrealGameFast(string rootPath)
        {
            if (EnumerateFilesTopLevelSafe(rootPath, "*.pak").Any() || EnumerateFilesTopLevelSafe(rootPath, "*.utoc").Any())
                return true;
            foreach (string folder in GetUnrealPakFolders(rootPath))
            {
                if (EnumerateFilesTopLevelSafe(folder, "*.pak").Any() || EnumerateFilesTopLevelSafe(folder, "*.utoc").Any())
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> GetUnrealPakFolders(string rootPath)
        {
            List<string> folders = new List<string>
            {
                Path.Combine(rootPath, "Content", "Paks"),
                Path.Combine(rootPath, "Engine", "Content", "Paks")
            };
            try
            {
                foreach (string folder in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
                    folders.Add(Path.Combine(folder, "Content", "Paks"));
            }
            catch { }
            return folders;
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
            else if (engine == GameEngine.Nwjs)
            {
                List<string> nwjsArchives = FindNwjsPackageArchives(rootPath);
                files = GetNwjsLooseFiles(rootPath, Path.Combine(rootPath, "extracted", "nwjs"))
                    .Concat(nwjsArchives)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                archives = nwjsArchives.Count;
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

        private static IEnumerable<string> EnumerateFilesTopLevelSafe(string rootPath, string pattern)
        {
            if (!Directory.Exists(rootPath)) return Enumerable.Empty<string>();
            try { return Directory.EnumerateFiles(rootPath, pattern, SearchOption.TopDirectoryOnly).ToList(); }
            catch { return Enumerable.Empty<string>(); }
        }

        private static List<string> FindUnityBundleFiles(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.bundle").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> FindNwjsPackageArchives(string rootPath)
        {
            return new[] { "package.nw", "app.nw" }
                .Select(delegate(string name) { return Path.Combine(rootPath, name); })
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetNwjsLooseFiles(string rootPath, string outputDir)
        {
            List<string> roots = new List<string>();
            foreach (string relative in new[] { "www", "package.nw", "app.nw" })
            {
                string candidate = Path.Combine(rootPath, relative);
                if (Directory.Exists(candidate)) roots.Add(candidate);
            }
            if (roots.Count == 0 && File.Exists(Path.Combine(rootPath, "package.json")))
                roots.Add(rootPath);

            string outputPrefix = AppendDirectorySeparator(Path.GetFullPath(outputDir));
            string extractedPrefix = AppendDirectorySeparator(Path.GetFullPath(Path.Combine(rootPath, "extracted")));
            HashSet<string> archivePaths = new HashSet<string>(FindNwjsPackageArchives(rootPath), StringComparer.OrdinalIgnoreCase);
            return roots
                .SelectMany(delegate(string root) { return EnumerateFilesSafe(root, "*.*"); })
                .Where(delegate(string path)
                {
                    string fullPath = Path.GetFullPath(path);
                    if (fullPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)
                        || fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase)
                        || archivePaths.Contains(fullPath))
                        return false;
                    string extension = Path.GetExtension(fullPath);
                    return !new[] { ".exe", ".dll", ".pdb", ".log" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string TryFindGameRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            DirectoryInfo current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
            int remainingParents = 6;
            while (current != null && remainingParents-- > 0)
            {
                string root = current.FullName;
                bool known = IsUnityGame(root)
                    || IsGodotGameFast(root)
                    || IsKirikiriGameFast(root)
                    || IsUnrealGameFast(root)
                    || Directory.Exists(Path.Combine(root, "www"))
                    || Directory.Exists(Path.Combine(root, "game"))
                    || File.Exists(Path.Combine(root, "package.json"))
                    || File.Exists(Path.Combine(root, "package.nw"))
                    || Directory.Exists(Path.Combine(root, "package.nw"))
                    || File.Exists(Path.Combine(root, "app.nw"))
                    || Directory.Exists(Path.Combine(root, "app.nw"));
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

        private static string GetSafeOutputPath(string outputDir, string relativePath)
        {
            string root = AppendDirectorySeparator(Path.GetFullPath(outputDir));
            string destination = Path.GetFullPath(Path.Combine(outputDir, SanitizeRelativePath(relativePath)));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Output path escapes extraction folder: " + relativePath);
            return destination;
        }

        private static string GetUniqueFilePath(string path, out bool renamed)
        {
            string directory = Path.GetDirectoryName(path);
            string filename = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string candidate = path;
            int suffix = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, filename + " (" + suffix + ")" + extension);
                suffix++;
            }
            renamed = !string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase);
            return candidate;
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

        private sealed class NwjsCopyStats
        {
            public NwjsCopyStats(int extracted, long bytes, int renamed, int skipped)
            {
                Extracted = extracted;
                Bytes = bytes;
                Renamed = renamed;
                Skipped = skipped;
            }

            public int Extracted { get; private set; }
            public long Bytes { get; private set; }
            public int Renamed { get; private set; }
            public int Skipped { get; private set; }
        }

        private sealed class OperationResult
        {
            public OperationResult(string engine, string outputDir, int extracted, long bytes, int errors, int renamed, TimeSpan duration)
                : this(engine, outputDir, extracted, bytes, errors, renamed, 0, duration)
            {
            }

            public OperationResult(string engine, string outputDir, int extracted, long bytes, int errors, int renamed, int skipped, TimeSpan duration)
            {
                Engine = engine;
                OutputDir = outputDir;
                Extracted = extracted;
                Bytes = bytes;
                Errors = errors;
                Renamed = renamed;
                Skipped = skipped;
                Duration = duration;
            }

            public string Engine { get; private set; }
            public string OutputDir { get; private set; }
            public int Extracted { get; private set; }
            public long Bytes { get; private set; }
            public int Errors { get; private set; }
            public int Renamed { get; private set; }
            public int Skipped { get; private set; }
            public TimeSpan Duration { get; private set; }

            public static OperationResult Failed(string engine, string outputDir, string message)
            {
                return new OperationResult(engine + " - " + message, outputDir, 0, 0, 1, 0, TimeSpan.Zero);
            }

            public string ToReport()
            {
                return string.Join(Environment.NewLine, new[]
                {
                    "Game Asset Tool v1.6.1 report",
                    "Engine: " + Engine,
                    "Extracted files: " + Extracted,
                    "Extracted size: " + FormatBytes(Bytes),
                    "Renamed conflicts: " + Renamed,
                    "Skipped items: " + Skipped,
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

        private sealed class ReadableButton : Button
        {
            public Color ActiveBackColor { get; set; }
            public Color ActiveForeColor { get; set; }
            public Color DisabledBackColor { get; set; }
            public Color DisabledForeColor { get; set; }
            public Color BorderColor { get; set; }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                Cursor = Enabled ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Rectangle bounds = ClientRectangle;
                using (SolidBrush background = new SolidBrush(Enabled ? ActiveBackColor : DisabledBackColor))
                    e.Graphics.FillRectangle(background, bounds);

                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    using (Pen outline = new Pen(BorderColor))
                        e.Graphics.DrawRectangle(outline, 0, 0, bounds.Width - 1, bounds.Height - 1);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    Font,
                    bounds,
                    Enabled ? ActiveForeColor : DisabledForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                if (Focused && ShowFocusCues)
                {
                    Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
                    ControlPaint.DrawFocusRectangle(e.Graphics, focus);
                }
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
