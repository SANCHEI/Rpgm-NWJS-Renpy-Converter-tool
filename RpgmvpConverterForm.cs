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
        private ComboBox languageBox;
        private ComboBox unlockerModeBox;
        private ComboBox unityExtractModeBox;
        private Label subtitleLabel;
        private Label folderSectionLabel;
        private Label extractSectionLabel;
        private Label logSectionLabel;
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
        private Button collectLooseButton;
        private Button unlockerButton;
        private Button removeUnlockerButton;
        private Button pauseButton;
        private Button cancelButton;
        private Button openOutputButton;
        private Button toggleLogButton;
        private Button clearLogButton;
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
        private bool russianUi;
        private string lastOutputDir = "";
        private readonly string startupGamePath;
        private GameEngine selectedEngine;

        private const int CompactClientHeight = 570;
        private const int ExpandedClientHeight = 872;
        private const int UnlockerSectionHeight = 64;

        public RpgmvpConverterForm() : this(null)
        {
        }

        public RpgmvpConverterForm(string startupPath)
        {
            startupGamePath = startupPath;
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

            Text = "Game Asset Tool v1.8.0";
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
            subtitleLabel = new Label
            {
                Text = "Drop a game folder or file here, scan it, then extract or unlock",
                ForeColor = mutedColor,
                Location = new Point(19, 39),
                Size = new Size(700, 20),
                BackColor = Color.Transparent
            };
            header.Controls.Add(subtitleLabel);
            runtimeStatusLabel = new Label
            {
                Text = "Runtime: waiting for a supported folder",
                ForeColor = mutedColor,
                Location = new Point(575, 24),
                Size = new Size(265, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            header.Controls.Add(runtimeStatusLabel);
            languageBox = new ComboBox
            {
                Location = new Point(852, 21),
                Size = new Size(58, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            languageBox.Items.Add("EN");
            languageBox.Items.Add("RU");
            languageBox.SelectedIndex = 0;
            languageBox.SelectedIndexChanged += delegate
            {
                russianUi = languageBox.SelectedIndex == 1;
                LocalizeUi();
            };
            header.Controls.Add(languageBox);

            int y = 88;
            folderSectionLabel = CreateSectionLabel("Game Folder", y);
            Controls.Add(folderSectionLabel);
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

            extractSectionLabel = CreateSectionLabel("Extract Assets", y);
            Controls.Add(extractSectionLabel);
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
            startButton = CreateButton("Extract Assets", new Point(704, 8), new Size(180, 26), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartDetectedExtractionAsync(); };
            extractionPanel.Controls.Add(startButton);
            collectLooseButton = CreateButton("Collect Loose Files", new Point(704, 39), new Size(180, 26), Color.FromArgb(45, 50, 60), textColor, uiBold);
            collectLooseButton.Click += async delegate { await StartLooseResourceCollectionAsync(); };
            extractionPanel.Controls.Add(collectLooseButton);
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
            logSectionLabel = new Label
            {
                Text = "Log",
                Location = new Point(10, 10),
                Size = new Size(60, 20),
                ForeColor = mutedColor,
                Font = uiBold
            };
            logPanel.Controls.Add(logSectionLabel);
            clearLogButton = CreateButton("Clear", new Point(75, 8), new Size(80, 24), Color.FromArgb(45, 50, 60), textColor, uiFont);
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
            LocalizeUi();
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

        private string T(string english, string russian)
        {
            return russianUi ? russian : english;
        }

        private void LocalizeUi()
        {
            if (subtitleLabel == null) return;

            subtitleLabel.Text = T("Drop a game folder or file here, scan it, then extract or unlock", "Перетащите папку или файл игры, проверьте и извлеките ресурсы");
            folderSectionLabel.Text = T("Game Folder or File", "Папка или файл игры");
            extractSectionLabel.Text = T("Extract Assets", "Извлечение ресурсов");
            unlockerSectionLabel.Text = T("Gallery Unlocker for Ren'Py", "Анлокер галереи для Ren'Py");
            logSectionLabel.Text = T("Log", "Лог");
            browseButton.Text = T("Browse...", "Обзор...");
            dryRunButton.Text = T("Dry Run / Scan", "Проверить");
            startButton.Text = T("Extract Assets", "Извлечь ресурсы");
            collectLooseButton.Text = T("Collect Loose Files", "Собрать открытые");
            unlockerButton.Text = T("Install Unlocker", "Установить анлокер");
            removeUnlockerButton.Text = T("Remove Unlocker", "Удалить анлокер");
            pauseButton.Text = T("Pause", "Пауза");
            cancelButton.Text = T("Cancel", "Отмена");
            openOutputButton.Text = T("Open Output Folder", "Открыть результат");
            toggleLogButton.Text = logExpanded ? T("Hide Log", "Скрыть лог") : T("Show Log", "Показать лог");
            clearLogButton.Text = T("Clear", "Очистить");

            int unityMode = unityExtractModeBox.SelectedIndex;
            unityExtractModeBox.Items.Clear();
            unityExtractModeBox.Items.Add(T("Textures", "Текстуры"));
            unityExtractModeBox.Items.Add(T("Videos", "Видео"));
            unityExtractModeBox.Items.Add(T("Audio", "Аудио"));
            unityExtractModeBox.Items.Add(T("Meshes", "Меши"));
            unityExtractModeBox.Items.Add(T("All", "Все"));
            unityExtractModeBox.SelectedIndex = unityMode >= 0 ? unityMode : 4;

            int unlockerMode = unlockerModeBox.SelectedIndex;
            unlockerModeBox.Items.Clear();
            unlockerModeBox.Items.Add(T("Soft", "Мягкий"));
            unlockerModeBox.Items.Add(T("Hard", "Жёсткий"));
            unlockerModeBox.SelectedIndex = unlockerMode >= 0 ? unlockerMode : 0;

            string path = pathBox.Text.Trim();
            if (IsExistingInput(path))
            {
                detectedEngineLabel.Text = T("Engine: ", "Движок: ") + EngineName(DetectEngineFast(path));
                scanSummaryLabel.Text = T(
                    "Ready to scan. Click Dry Run / Scan to inspect files before extraction.",
                    "Готово к проверке. Нажмите «Проверить», чтобы просмотреть файлы перед извлечением.");
            }
            else
            {
                detectedEngineLabel.Text = T("Engine: not detected", "Движок: не определён");
                scanSummaryLabel.Text = T("Select a folder or file, or drop it into this window.", "Выберите папку или файл либо перетащите в это окно.");
            }

            if (currentRun == null && !externalRunning)
            {
                statusLabel.Text = T("Waiting to start", "Ожидание запуска");
                statsLabel.Text = T("Processed: 0 / 0 | Size: -- | ETA: --:--", "Обработано: 0 / 0 | Размер: -- | Осталось: --:--");
            }

            UpdateEngineContext(selectedEngine);
            ConfigureActionTooltips();
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = T("Select game folder", "Выберите папку игры");
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
            string path = paths[0];
            if (!string.IsNullOrWhiteSpace(path))
                ApplyGamePath(path, true);
        }

        private void ApplyGamePath(string path, bool scan)
        {
            string detectedRoot = TryFindGameRoot(path);
            pathBox.Text = File.Exists(path) || string.IsNullOrWhiteSpace(detectedRoot) ? path : detectedRoot;
            TryAutoDetectKey(InputDirectory(pathBox.Text));
            if (scan) RunDryScan(false);
        }

        private void TryApplyStartupGamePath()
        {
            if (closing || IsDisposed) return;
            bool hasStartupArgument = !string.IsNullOrWhiteSpace(startupGamePath);
            string requestedPath = hasStartupArgument
                ? startupGamePath
                : AppDomain.CurrentDomain.BaseDirectory;
            string initialRoot = TryFindGameRoot(requestedPath);
            if (string.IsNullOrWhiteSpace(initialRoot) && hasStartupArgument && IsExistingInput(requestedPath))
                initialRoot = requestedPath;
            if (!string.IsNullOrWhiteSpace(initialRoot))
                ApplyGamePath(hasStartupArgument ? requestedPath : initialRoot, false);
        }

        private void OnPathChanged()
        {
            string path = pathBox.Text.Trim();
            if (!IsExistingInput(path))
            {
                detectedEngineLabel.Text = T("Engine: not detected", "Движок: не определён");
                detectedEngineLabel.ForeColor = mutedColor;
                scanSummaryLabel.Text = T("Select a folder or file, or drop it into this window.", "Выберите папку или файл либо перетащите в это окно.");
                UpdateEngineContext(GameEngine.Unknown);
                return;
            }

            TryAutoDetectKey(InputDirectory(path));
            GameEngine engine = DetectEngineFast(path);
            detectedEngineLabel.Text = T("Engine: ", "Движок: ") + EngineName(engine);
            detectedEngineLabel.ForeColor = EngineColor(engine);
            scanSummaryLabel.Text = T(
                "Ready to scan. Click Dry Run / Scan to inspect files before extraction.",
                "Готово к проверке. Нажмите «Проверить», чтобы просмотреть файлы перед извлечением.");
            UpdateEngineContext(engine);
            WarmPortableRuntimeInBackground(engine);
        }

        private void RunDryScan(bool showLog)
        {
            string inputPath = pathBox.Text.Trim();
            if (!IsExistingInput(inputPath))
            {
                WriteLog("Invalid path");
                return;
            }

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                ScanSummary summary = BuildScanSummary(inputPath);
                detectedEngineLabel.Text = T("Engine: ", "Движок: ") + EngineName(summary.Engine);
                detectedEngineLabel.ForeColor = EngineColor(summary.Engine);
                scanSummaryLabel.Text = string.Format(
                    T(
                        "{0} archive(s), {1} candidate file(s), input size {2} (not estimated output)",
                        "{0} архив(а), {1} подходящих файлов, входной размер {2} (не оценка результата)"),
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
            string inputPath = pathBox.Text.Trim();
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }

            GameEngine engine = DetectEngine(inputPath);
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
            if (engine == GameEngine.WolfRpg)
            {
                await StartWolfExtractionAsync();
                return;
            }
            if (engine == GameEngine.TyranoScript)
            {
                await StartTyranoExtractionAsync();
                return;
            }
            if (engine == GameEngine.JavaJar)
            {
                await StartJavaExtractionAsync();
                return;
            }
            if (engine == GameEngine.Flash)
            {
                await StartFlashExtractionAsync();
                return;
            }
            if (engine == GameEngine.Html)
            {
                await StartHtmlExtractionAsync();
                return;
            }
            if (engine == GameEngine.Qsp)
            {
                await StartQspExtractionAsync();
                return;
            }
            if (engine == GameEngine.Rags)
            {
                await StartRagsExtractionAsync();
                return;
            }
            if (engine != GameEngine.RpgMaker)
            {
                await StartDiagnosticExportAsync();
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

            string inputPath = pathBox.Text.Trim();
            string rootPath = InputDirectory(inputPath);
            string gameFolder = GetRenpyGameFolder(inputPath);
            List<string> archives = FindRenpyArchives(inputPath);
            if (archives.Count == 0)
            {
                await StartRenpyLooseExtractionAsync(rootPath, gameFolder);
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
                result = await Task.Run(delegate { return RunRenpyExtraction(gameFolder, archives, outputDir); });
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed("Ren'Py", outputDir, ex.Message);
            }
            SetExternalRunningState(false, "Ren'Py");
            CompleteExternalOperation(result);
        }

        private OperationResult RunRenpyExtraction(string gameFolder, List<string> archives, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            int errors = 0;
            int renamed = 0;
            Directory.CreateDirectory(outputDir);
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

        private async Task StartRenpyLooseExtractionAsync(string rootPath, string gameFolder)
        {
            string outputDir = Path.Combine(rootPath, "extracted", "renpy", "loose");
            lastOutputDir = outputDir;
            SetExternalRunningState(true, "Ren'Py loose files");
            WriteLog("Ren'Py resources are already open. Collecting loose files.");
            OperationResult result;
            try
            {
                result = await Task.Run(delegate
                {
                    DateTime start = DateTime.UtcNow;
                    List<string> files = AssetCollectors.GetLooseResourceFiles(gameFolder, outputDir);
                    CollectorResult copied = AssetCollectors.CopyFiles(gameFolder, files, outputDir, "");
                    return new OperationResult("Ren'Py loose resources", outputDir, copied.Extracted, copied.Bytes, 0, copied.Renamed, copied.Skipped, DateTime.UtcNow - start);
                });
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed("Ren'Py loose resources", outputDir, ex.Message);
            }
            SetExternalRunningState(false, "Ren'Py loose files");
            CompleteExternalOperation(result);
        }

        private async Task StartUnityExtractionAsync()
        {
            if (currentRun != null || externalRunning) return;

            string rootPath = InputDirectory(pathBox.Text.Trim());
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

            string rootPath = InputDirectory(pathBox.Text.Trim());
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
            UpdateLocalProgress("NWJS", processed, total, bytes);
        }

        private void UpdateLocalProgress(string operation, int processed, int total, long bytes)
        {
            BeginUi(delegate
            {
                progressBar.Maximum = Math.Max(total, 1);
                progressBar.Value = Math.Min(processed, progressBar.Maximum);
                statusLabel.Text = operation + ": " + processed + " / " + total;
                statsLabel.Text = T("Sources: ", "Источники: ") + processed + " / " + total + T(" | Size: ", " | Размер: ") + FormatBytes(bytes);
            });
        }

        private void ThrowIfLocalCopyCancelled()
        {
            if (localCopyCancellationRequested)
                throw new OperationCanceledException();
        }

        private NwjsCopyStats ExtractNwjsZipArchive(string archivePath, string outputDir)
        {
            return ExtractZipArchive(archivePath, outputDir, Path.Combine("archives", Path.GetFileNameWithoutExtension(archivePath)));
        }

        private async Task StartTyranoExtractionAsync()
        {
            await StartLocalExtractionAsync("TyranoScript", "tyrano", delegate(string rootPath, string outputDir)
            {
                return RunTyranoExtraction(rootPath, outputDir);
            });
        }

        private async Task StartJavaExtractionAsync()
        {
            await StartLocalExtractionAsync("Java game / JAR", "java", delegate(string rootPath, string outputDir)
            {
                return RunJavaExtraction(rootPath, outputDir);
            });
        }

        private async Task StartFlashExtractionAsync()
        {
            await StartLocalExtractionAsync("Flash SWF experimental", "flash", delegate(string rootPath, string outputDir)
            {
                return RunFlashExtraction(rootPath, outputDir);
            });
        }

        private async Task StartWolfExtractionAsync()
        {
            await StartLocalExtractionAsync("WOLF RPG", "wolf", delegate(string rootPath, string outputDir)
            {
                return RunWolfExtraction(rootPath, outputDir);
            });
        }

        private async Task StartHtmlExtractionAsync()
        {
            await StartLocalExtractionAsync("HTML game", "html", delegate(string inputPath, string outputDir)
            {
                return RunCollectorExtraction("HTML game", inputPath, outputDir, AssetCollectors.GetHtmlFiles(inputPath, outputDir));
            });
        }

        private async Task StartQspExtractionAsync()
        {
            await StartLocalExtractionAsync("QSP", "qsp", delegate(string inputPath, string outputDir)
            {
                return RunCollectorExtraction("QSP", inputPath, outputDir, AssetCollectors.GetQspFiles(inputPath, outputDir));
            });
        }

        private async Task StartRagsExtractionAsync()
        {
            await StartLocalExtractionAsync("RAGS experimental", "rags", delegate(string inputPath, string outputDir)
            {
                DateTime start = DateTime.UtcNow;
                CollectorResult extracted = AssetCollectors.ExtractRags(inputPath, outputDir);
                AssetCollectors.WriteDiagnostics(inputPath, outputDir);
                return new OperationResult("RAGS experimental media recovery", outputDir, extracted.Extracted, extracted.Bytes, 0, extracted.Renamed, extracted.Skipped, DateTime.UtcNow - start);
            });
        }

        private async Task StartLooseResourceCollectionAsync()
        {
            await StartLocalExtractionAsync("Loose resources", "loose-assets", delegate(string inputPath, string outputDir)
            {
                List<string> files = AssetCollectors.GetLooseResourceFiles(inputPath, outputDir);
                return RunCollectorExtraction("Loose resources", inputPath, outputDir, files);
            });
        }

        private async Task StartDiagnosticExportAsync()
        {
            await StartLocalExtractionAsync("Diagnostics", "diagnostics", delegate(string inputPath, string outputDir)
            {
                DateTime start = DateTime.UtcNow;
                string report = AssetCollectors.WriteDiagnostics(inputPath, outputDir);
                SafeLog("Unknown-engine diagnostics: " + report);
                return new OperationResult("Unknown engine diagnostics", outputDir, 1, SafeFileLength(report), 0, 0, DateTime.UtcNow - start);
            });
        }

        private OperationResult RunCollectorExtraction(string engineName, string inputPath, string outputDir, List<string> files)
        {
            DateTime start = DateTime.UtcNow;
            string rootPath = InputDirectory(inputPath);
            CollectorResult copied = AssetCollectors.CopyFiles(rootPath, files, outputDir, "");
            return new OperationResult(engineName, outputDir, copied.Extracted, copied.Bytes, 0, copied.Renamed, copied.Skipped, DateTime.UtcNow - start);
        }

        private async Task StartLocalExtractionAsync(string engineName, string outputFolder, Func<string, string, OperationResult> extract)
        {
            if (currentRun != null || externalRunning) return;

            string inputPath = pathBox.Text.Trim();
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }

            string outputDir = Path.Combine(rootPath, "extracted", outputFolder);
            lastOutputDir = outputDir;
            localCopyCancellationRequested = false;
            SetExternalRunningState(true, engineName);
            WriteLog(engineName + " extraction started");

            OperationResult result;
            try
            {
                result = await Task.Run(delegate { return extract(inputPath, outputDir); });
            }
            catch (OperationCanceledException)
            {
                result = OperationResult.Failed(engineName, outputDir, "cancelled");
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed(engineName, outputDir, ex.Message);
            }
            SetExternalRunningState(false, engineName);
            CompleteExternalOperation(result);
        }

        private OperationResult RunTyranoExtraction(string rootPath, string outputDir)
        {
            rootPath = InputDirectory(rootPath);
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> files = GetTyranoFiles(rootPath, outputDir);
            NwjsCopyStats stats = CopyLooseFiles(rootPath, files, outputDir, "");
            return new OperationResult("TyranoScript", outputDir, stats.Extracted, stats.Bytes, 0, stats.Renamed, stats.Skipped, DateTime.UtcNow - start);
        }

        private OperationResult RunJavaExtraction(string rootPath, string outputDir)
        {
            string inputPath = rootPath;
            rootPath = InputDirectory(rootPath);
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> archives = FindJavaArchives(inputPath);
            List<string> looseFiles = GetJavaLooseFiles(rootPath, outputDir);
            NwjsCopyStats loose = CopyLooseFiles(rootPath, looseFiles, outputDir, "loose");
            int extracted = loose.Extracted;
            long bytes = loose.Bytes;
            int errors = 0;
            int renamed = loose.Renamed;
            int skipped = loose.Skipped;
            int total = looseFiles.Count + archives.Count;

            if (looseFiles.Count > 0)
                UpdateLocalProgress("Java", looseFiles.Count, total, bytes);

            for (int i = 0; i < archives.Count; i++)
            {
                ThrowIfLocalCopyCancelled();
                string archive = archives[i];
                try
                {
                    NwjsCopyStats stats = ExtractZipArchive(archive, outputDir, Path.Combine("archives", Path.GetFileNameWithoutExtension(archive)));
                    extracted += stats.Extracted;
                    bytes += stats.Bytes;
                    renamed += stats.Renamed;
                    skipped += stats.Skipped;
                }
                catch (InvalidDataException)
                {
                    skipped++;
                    SafeLog("Java JAR is not ZIP-compatible and was skipped: " + archive);
                }
                catch (Exception ex)
                {
                    errors++;
                    SafeLog("WARN:" + archive + ":" + ex.Message);
                }
                UpdateLocalProgress("Java", looseFiles.Count + i + 1, total, bytes);
            }

            return new OperationResult("Java game / JAR", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private OperationResult RunFlashExtraction(string rootPath, string outputDir)
        {
            string inputPath = rootPath;
            rootPath = InputDirectory(rootPath);
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> files = FindFlashFiles(inputPath);
            int extracted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;
            int skipped = 0;

            for (int i = 0; i < files.Count; i++)
            {
                ThrowIfLocalCopyCancelled();
                string source = files[i];
                try
                {
                    bool collision;
                    string destination = GetSafeOutputPath(outputDir, Path.Combine("originals", Path.GetFileName(source)));
                    destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination);
                    extracted++;
                    bytes += SafeFileLength(destination);
                    if (collision) renamed++;

                    NwjsCopyStats images = ExtractSwfImages(source, outputDir);
                    extracted += images.Extracted;
                    bytes += images.Bytes;
                    renamed += images.Renamed;
                    skipped += images.Skipped;
                }
                catch (NotSupportedException ex)
                {
                    skipped++;
                    SafeLog("Flash inspection skipped: " + ex.Message);
                }
                catch (Exception ex)
                {
                    errors++;
                    SafeLog("WARN:" + source + ":" + ex.Message);
                }
                UpdateLocalProgress("Flash", i + 1, files.Count, bytes);
            }

            return new OperationResult("Flash SWF experimental", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private OperationResult RunWolfExtraction(string rootPath, string outputDir)
        {
            rootPath = InputDirectory(rootPath);
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> looseFiles = GetWolfLooseFiles(rootPath, outputDir);
            List<string> archives = FindWolfArchiveFiles(rootPath);
            NwjsCopyStats loose = CopyLooseFiles(rootPath, looseFiles, outputDir, "loose");
            int extracted = loose.Extracted;
            long bytes = loose.Bytes;
            int errors = 0;
            int renamed = loose.Renamed;
            int skipped = loose.Skipped;

            if (archives.Count > 0)
            {
                string staging = ToolRuntime.CreateSessionDirectory("wolf-run-" + Guid.NewGuid().ToString("N"));
                try
                {
                    List<string> stagedArchives = StageWolfFiles(rootPath, archives, staging);
                    string cliPath = ToolRuntime.EnsureWolfCliExtracted();
                    string gameExe = FindWolfGameExecutable(staging);
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = cliPath,
                        WorkingDirectory = staging,
                        Arguments = !string.IsNullOrWhiteSpace(gameExe)
                            ? QuoteArg(gameExe)
                            : string.Join(" ", stagedArchives.Select(QuoteArg)),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    int exitCode = RunExternalProcess(psi, delegate(string line) { SafeLog(line); });
                    if (exitCode != 0) errors++;

                    foreach (string stagedArchive in stagedArchives)
                    {
                        string unpacked = Path.Combine(Path.GetDirectoryName(stagedArchive), Path.GetFileNameWithoutExtension(stagedArchive));
                        if (!Directory.Exists(unpacked))
                        {
                            skipped++;
                            continue;
                        }

                        string relative = Path.ChangeExtension(MakeRelativePath(staging, stagedArchive), null);
                        NwjsCopyStats copied = CopyLooseFiles(unpacked, EnumerateFilesSafe(unpacked, "*.*").ToList(), outputDir, Path.Combine("archives", relative));
                        extracted += copied.Extracted;
                        bytes += copied.Bytes;
                        renamed += copied.Renamed;
                        skipped += copied.Skipped;
                    }
                }
                finally
                {
                    ToolRuntime.DeleteSessionDirectory(staging);
                }
            }

            return new OperationResult("WOLF RPG", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private static List<string> StageWolfFiles(string rootPath, IEnumerable<string> archives, string staging)
        {
            List<string> result = new List<string>();
            foreach (string archive in archives)
            {
                string relative = MakeRelativePath(rootPath, archive);
                string destination = GetSafeChildPath(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(archive, destination, true);
                result.Add(destination);
            }

            foreach (string name in new[] { "GamePro.exe", "Game.exe" })
            {
                string source = Path.Combine(rootPath, name);
                if (File.Exists(source))
                    File.Copy(source, GetSafeChildPath(staging, name), true);
            }
            return result;
        }

        private static string FindWolfGameExecutable(string rootPath)
        {
            foreach (string name in new[] { "GamePro.exe", "Game.exe" })
            {
                string path = Path.Combine(rootPath, name);
                if (File.Exists(path)) return path;
            }
            return "";
        }

        private NwjsCopyStats CopyLooseFiles(string relativeRoot, IEnumerable<string> files, string outputDir, string prefix)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            foreach (string source in files)
            {
                ThrowIfLocalCopyCancelled();
                try
                {
                    string relative = MakeRelativePath(relativeRoot, source);
                    string destination = GetSafeOutputPath(outputDir, string.IsNullOrWhiteSpace(prefix) ? relative : Path.Combine(prefix, relative));
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
                    skipped++;
                    SafeLog("WARN:" + source + ":" + ex.Message);
                }
            }
            return new NwjsCopyStats(extracted, bytes, renamed, skipped);
        }

        private NwjsCopyStats ExtractZipArchive(string archivePath, string outputDir, string prefix)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
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

                    string destination = GetSafeOutputPath(outputDir, Path.Combine(prefix, entry.FullName));
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

        private NwjsCopyStats ExtractSwfImages(string source, string outputDir)
        {
            byte[] body = ReadSwfBody(source);
            int position = GetSwfTagStart(body);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            string swfName = Path.GetFileNameWithoutExtension(source);

            while (position + 2 <= body.Length)
            {
                int tagHeader = ReadUInt16(body, position);
                position += 2;
                int tagCode = tagHeader >> 6;
                int length = tagHeader & 0x3f;
                if (length == 0x3f)
                {
                    if (position + 4 > body.Length) break;
                    length = ReadInt32(body, position);
                    position += 4;
                }
                if (length < 0 || position + length > body.Length) break;
                if (tagCode == 0) break;

                int imageOffset = 0;
                int imageLength = 0;
                int characterId = 0;
                if (tagCode == 21 && length > 2)
                {
                    characterId = ReadUInt16(body, position);
                    imageOffset = position + 2;
                    imageLength = length - 2;
                }
                else if (tagCode == 35 && length > 6)
                {
                    characterId = ReadUInt16(body, position);
                    imageOffset = position + 6;
                    imageLength = Math.Min(ReadInt32(body, position + 2), length - 6);
                }
                else if (tagCode == 90 && length > 8)
                {
                    characterId = ReadUInt16(body, position);
                    imageOffset = position + 8;
                    imageLength = Math.Min(ReadInt32(body, position + 2), length - 8);
                }

                string extension = DetectImageExtension(body, imageOffset, imageLength);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    string destination = GetSafeOutputPath(outputDir, Path.Combine("embedded", swfName, "image-" + characterId + extension));
                    bool collision;
                    destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (FileStream output = File.Create(destination))
                        output.Write(body, imageOffset, imageLength);
                    extracted++;
                    bytes += SafeFileLength(destination);
                    if (collision) renamed++;
                }
                else if (imageLength > 0)
                {
                    skipped++;
                }
                position += length;
            }
            return new NwjsCopyStats(extracted, bytes, renamed, skipped);
        }

        private static byte[] ReadSwfBody(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < 8 || file[1] != (byte)'W' || file[2] != (byte)'S')
                throw new InvalidDataException("Invalid SWF header: " + path);
            if (file[0] == (byte)'F')
                return file.Skip(8).ToArray();
            if (file[0] == (byte)'C')
            {
                if (file.Length < 14) throw new InvalidDataException("Compressed SWF is incomplete: " + path);
                using (MemoryStream input = new MemoryStream(file, 10, file.Length - 14))
                using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
            }
            if (file[0] == (byte)'Z')
                throw new NotSupportedException("LZMA-compressed ZWS is not supported yet: " + path);
            throw new InvalidDataException("Unknown SWF compression: " + path);
        }

        private static int GetSwfTagStart(byte[] body)
        {
            if (body.Length < 5) throw new InvalidDataException("SWF body is incomplete.");
            int rectBits = 5 + 4 * (body[0] >> 3);
            int position = (rectBits + 7) / 8 + 4;
            if (position > body.Length) throw new InvalidDataException("SWF frame header is incomplete.");
            return position;
        }

        private static string DetectImageExtension(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 3 || offset + length > data.Length) return "";
            if (data[offset] == 0xff && data[offset + 1] == 0xd8 && data[offset + 2] == 0xff) return ".jpg";
            if (length >= 8 && data[offset] == 0x89 && data[offset + 1] == 0x50 && data[offset + 2] == 0x4e && data[offset + 3] == 0x47) return ".png";
            if (length >= 6 && data[offset] == (byte)'G' && data[offset + 1] == (byte)'I' && data[offset + 2] == (byte)'F') return ".gif";
            return "";
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

        private async Task StartPortableScriptExtractionAsync(string engineName, string outputFolder, string scriptFile, string resourceName)
        {
            if (currentRun != null || externalRunning) return;

            string rootPath = InputDirectory(pathBox.Text.Trim());
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
            string rootPath = InputDirectory(pathBox.Text.Trim());
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
            string rootPath = InputDirectory(pathBox.Text.Trim());
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
                statusLabel.Text = T("Preparing built-in runtime...", "Подготовка встроенного runtime...");
                SetRuntimeStatus(T("Runtime: preparing silently...", "Runtime: подготовка в фоне..."), warningColor);
                Task warmup;
                lock (runtimeWarmupSync) warmup = runtimeWarmupTask;
                if (warmup != null && !warmup.IsCompleted)
                    warmup.Wait();
                PortableRuntime.EnsureExtracted();
                SetRuntimeStatus(T("Runtime: ready", "Runtime: готов"), successColor);
                return true;
            }
            catch (Exception ex)
            {
                SetRuntimeStatus(T("Runtime: unavailable", "Runtime: недоступен"), dangerColor);
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
                pauseButton.Text = T("Pause", "Пауза");
                WriteLog("RPGM extraction resumed.");
            }
            else
            {
                currentRun.Pause();
                pauseButton.Text = T("Resume", "Продолжить");
                statusLabel.Text = T("Paused", "Приостановлено");
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
            statusLabel.Text = T("Stopping...", "Остановка...");
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
            statusLabel.Text = currentRun.IsPaused ? T("Paused", "Приостановлено") : "RPGM: " + processed + " / " + currentRun.TotalCount;
            statsLabel.Text = T("Processed: ", "Обработано: ") + processed + " / " + currentRun.TotalCount
                + T(" | Size: ", " | Размер: ") + FormatBytes(currentRun.TotalBytes)
                + T(" | ETA: ", " | Осталось: ") + eta;
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
            statusLabel.Text = result.Errors == 0 ? T("Complete", "Завершено") : T("Complete with warnings", "Завершено с предупреждениями");
            statsLabel.Text = T("Extracted: ", "Извлечено: ") + result.Extracted
                + T(" | Size: ", " | Размер: ") + FormatBytes(result.Bytes)
                + T(" | Errors: ", " | Ошибки: ") + result.Errors
                + (result.Skipped > 0 ? T(" | Skipped: ", " | Пропущено: ") + result.Skipped : "");
            UpdateActionTooltips();
            string reportPath = SaveReport(result);
            WriteLog("Report: " + reportPath);
            using (ResultsDialog dialog = new ResultsDialog(result, reportPath, russianUi))
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
            collectLooseButton.Enabled = !running;
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
                collectLooseButton.Enabled = !running;
                UpdateUnlockerControls(running);
                pauseButton.Enabled = false;
                cancelButton.Enabled = running;
                openOutputButton.Enabled = !running && Directory.Exists(lastOutputDir);
                if (running)
                {
                    progressBar.Maximum = 1;
                    progressBar.Value = 0;
                    statusLabel.Text = operation + T(": starting...", ": запуск...");
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
            toggleLogButton.Text = logExpanded ? T("Hide Log", "Скрыть лог") : T("Show Log", "Показать лог");
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
            startButton.Enabled = !busy && (CanExtractAssets(engine) || IsExistingInput(pathBox.Text.Trim()));
            startButton.Text = engine == GameEngine.Unknown
                ? T("Export Diagnostics", "Экспорт диагностики")
                : T("Extract Assets", "Извлечь ресурсы");
            collectLooseButton.Enabled = !busy && IsExistingInput(pathBox.Text.Trim());
            UpdateUnlockerControls(busy);

            switch (engine)
            {
                case GameEngine.RpgMaker:
                    keyLabel.Text = T("RPGM HEX key", "HEX-ключ RPGM");
                    extractionHintLabel.Text = T(
                        "Encrypted image assets. The key is detected automatically when possible.",
                        "Зашифрованные изображения. Ключ определяется автоматически, когда это возможно.");
                    break;
                case GameEngine.Unity:
                    extractionHintLabel.Text = T("Choose the Unity asset types to export.", "Выберите типы ресурсов Unity для извлечения.");
                    break;
                case GameEngine.Renpy:
                    extractionHintLabel.Text = FindRenpyArchives(pathBox.Text.Trim()).Count > 0
                        ? T("RPA archives will be extracted into separate folders.", "Архивы RPA будут извлечены в отдельные папки.")
                        : T("No RPA archives found. Open Ren'Py resources will be collected.", "Архивы RPA не найдены. Будут собраны открытые ресурсы Ren'Py.");
                    break;
                case GameEngine.Godot:
                    extractionHintLabel.Text = T("Standard unencrypted PCK archives will be extracted.", "Будут извлечены стандартные незашифрованные архивы PCK.");
                    break;
                case GameEngine.Kirikiri:
                    extractionHintLabel.Text = T("Standard unencrypted XP3 archives will be extracted.", "Будут извлечены стандартные незашифрованные архивы XP3.");
                    break;
                case GameEngine.Unreal:
                    keyLabel.Text = T("Unreal AES key", "AES-ключ Unreal");
                    extractionHintLabel.Text = T(
                        "Experimental PAK extraction. AES key is optional; Oodle and IoStore are reported.",
                        "Экспериментальное извлечение PAK. AES-ключ необязателен; Oodle и IoStore отмечаются в отчёте.");
                    break;
                case GameEngine.Nwjs:
                    extractionHintLabel.Text = T(
                        "NWJS files will be copied. ZIP-compatible package.nw archives will be unpacked.",
                        "Файлы NWJS будут скопированы. ZIP-совместимые архивы package.nw будут распакованы.");
                    break;
                case GameEngine.WolfRpg:
                    extractionHintLabel.Text = T(
                        "WOLF archives use the embedded UberWolf CLI. Loose Data files are copied too.",
                        "Архивы WOLF извлекаются встроенным UberWolf CLI. Открытые файлы Data также копируются.");
                    break;
                case GameEngine.TyranoScript:
                    extractionHintLabel.Text = T(
                        "TyranoScript project data will be copied with its original folder structure.",
                        "Данные проекта TyranoScript будут скопированы с исходной структурой папок.");
                    break;
                case GameEngine.JavaJar:
                    extractionHintLabel.Text = T(
                        "Java JAR archives will be safely unpacked. Loose res folders from bundled Java games are copied without the JRE.",
                        "Архивы Java JAR будут безопасно распакованы. Открытая папка res из Java-игры копируется без JRE.");
                    break;
                case GameEngine.Flash:
                    extractionHintLabel.Text = T(
                        "Experimental Flash inspection copies SWF files and extracts embedded JPEG, PNG and GIF images.",
                        "Экспериментальный анализ Flash копирует SWF и извлекает встроенные JPEG, PNG и GIF.");
                    break;
                case GameEngine.Html:
                    extractionHintLabel.Text = T(
                        "Static HTML game files and open media resources will be collected with their folder structure.",
                        "Файлы статической HTML-игры и открытые медиа будут собраны с сохранением структуры.");
                    break;
                case GameEngine.Qsp:
                    extractionHintLabel.Text = T(
                        "QSP databases and open media resources will be collected without copying the bundled player.",
                        "Базы QSP и открытые медиа будут собраны без копирования встроенного проигрывателя.");
                    break;
                case GameEngine.Rags:
                    extractionHintLabel.Text = T(
                        "Experimental RAGS recovery copies the encrypted database and carves confidently detected embedded media.",
                        "Экспериментальное восстановление RAGS копирует зашифрованную базу и извлекает уверенно найденные медиа.");
                    break;
                default:
                    extractionHintLabel.Text = T(
                        "Unknown format. Export diagnostics or collect loose resources for further analysis.",
                        "Неизвестный формат. Экспортируйте диагностику или соберите открытые ресурсы для анализа.");
                    break;
            }

            UpdateRuntimeStatusForEngine(engine);
            UpdateActionTooltips();
        }

        private void UpdateUnlockerControls(bool busy)
        {
            string rootPath = InputDirectory(pathBox.Text.Trim());
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
                || engine == GameEngine.Nwjs
                || engine == GameEngine.WolfRpg
                || engine == GameEngine.TyranoScript
                || engine == GameEngine.JavaJar
                || engine == GameEngine.Flash
                || engine == GameEngine.Html
                || engine == GameEngine.Qsp
                || engine == GameEngine.Rags;
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
            SetActionTooltip(pathBox, T("Drop a game folder or a supported file here, or choose a folder with Browse.", "Перетащите папку или поддерживаемый файл игры либо выберите папку через «Обзор»."));
            SetActionTooltip(browseButton, T("Select the root folder of a game.", "Выберите корневую папку игры."));
            SetActionTooltip(dryRunButton, T("Inspect supported archives and estimate the input size without extracting files.", "Проверьте архивы и входной размер без извлечения файлов."));
            SetActionTooltip(toggleLogButton, T("Show or hide technical extraction messages.", "Показать или скрыть технические сообщения."));
            SetActionTooltip(languageBox, T("Switch interface language.", "Переключить язык интерфейса."));
            UpdateActionTooltips();
        }

        private void UpdateActionTooltips()
        {
            if (actionToolTip == null) return;
            bool busy = currentRun != null || externalRunning;
            SetActionTooltip(startButton, busy
                ? T("Wait for the current operation to finish.", "Дождитесь завершения текущей операции.")
                : CanExtractAssets(selectedEngine)
                    ? T("Extract supported assets for the detected engine.", "Извлечь поддерживаемые ресурсы определённого движка.")
                    : T("Export a diagnostic report for this unknown format.", "Экспортировать диагностический отчёт для неизвестного формата."));
            SetActionTooltip(collectLooseButton, T("Collect open media, scripts and project files without unpacking archives.", "Собрать открытые медиа, скрипты и файлы проекта без распаковки архивов."));
            SetActionTooltip(unlockerButton, selectedEngine == GameEngine.Renpy
                ? T("Install the Ren'Py gallery unlocker. Try Soft mode first.", "Установить анлокер галереи Ren'Py. Сначала попробуйте мягкий режим.")
                : T("The gallery unlocker is available only for detected Ren'Py folders.", "Анлокер галереи доступен только для определённых папок Ren'Py."));
            SetActionTooltip(removeUnlockerButton, removeUnlockerButton.Enabled
                ? T("Remove previously installed Ren'Py gallery unlocker files.", "Удалить ранее установленные файлы анлокера Ren'Py.")
                : T("No installed Ren'Py gallery unlocker was found.", "Установленный анлокер Ren'Py не найден."));
            SetActionTooltip(pauseButton, T("Pause or resume RPG Maker asset conversion.", "Приостановить или продолжить конвертацию RPG Maker."));
            SetActionTooltip(cancelButton, busy ? T("Stop the current operation.", "Остановить текущую операцию.") : T("No operation is currently running.", "Сейчас нет выполняемой операции."));
            SetActionTooltip(openOutputButton, Directory.Exists(lastOutputDir)
                ? T("Open the most recent extraction output folder.", "Открыть папку последнего результата.")
                : T("Run an extraction first to create an output folder.", "Сначала выполните извлечение, чтобы создать папку результата."));
            SetActionTooltip(keyBox, selectedEngine == GameEngine.RpgMaker
                ? T("The RPG Maker HEX key is filled automatically when possible. You can paste it manually if detection fails.", "HEX-ключ RPG Maker заполняется автоматически, когда это возможно. Если определение не сработало, вставьте ключ вручную.")
                : T("Optional Unreal AES key. Leave it empty for unencrypted PAK archives.", "Необязательный AES-ключ Unreal. Для незашифрованных PAK оставьте поле пустым."));
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
                SetRuntimeStatus(
                    engine == GameEngine.Unknown
                        ? T("Runtime: waiting for a supported folder", "Runtime: выберите поддерживаемую папку")
                        : T("Runtime: not needed", "Runtime: не требуется"),
                    mutedColor);
                return;
            }
            if (PortableRuntime.IsReady)
            {
                SetRuntimeStatus(T("Runtime: ready", "Runtime: готов"), successColor);
                return;
            }

            Task warmup;
            lock (runtimeWarmupSync) warmup = runtimeWarmupTask;
            SetRuntimeStatus(
                warmup != null && !warmup.IsCompleted
                    ? T("Runtime: preparing silently...", "Runtime: подготовка в фоне...")
                    : T("Runtime: preparing after selection", "Runtime: подготовится после выбора"),
                warningColor);
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
            ToolRuntime.Cleanup();
        }

        private void WarmPortableRuntimeInBackground(GameEngine engine)
        {
            if (!UsesPortableRuntime(engine) || closing) return;
            if (PortableRuntime.IsReady)
            {
                SetRuntimeStatus(T("Runtime: ready", "Runtime: готов"), successColor);
                return;
            }
            lock (runtimeWarmupSync)
            {
                if (runtimeWarmupTask != null) return;
                SetRuntimeStatus(T("Runtime: preparing silently...", "Runtime: подготовка в фоне..."), warningColor);
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
                        SetRuntimeStatus(
                            ready ? T("Runtime: ready", "Runtime: готов") : T("Runtime: unavailable", "Runtime: недоступен"),
                            ready ? successColor : dangerColor);
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

        private static GameEngine DetectEngine(string inputPath)
        {
            GameEngine direct = DetectDirectFileEngine(inputPath);
            if (direct != GameEngine.Unknown) return direct;
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath)) return GameEngine.Unknown;
            if (IsUnityGame(rootPath)) return GameEngine.Unity;
            if (IsRenpyGame(rootPath)) return GameEngine.Renpy;
            if (HasRpgmFiles(rootPath)) return GameEngine.RpgMaker;
            if (IsGodotGame(rootPath)) return GameEngine.Godot;
            if (IsKirikiriGame(rootPath)) return GameEngine.Kirikiri;
            if (IsWolfRpgGame(rootPath)) return GameEngine.WolfRpg;
            if (IsTyranoScriptGame(rootPath)) return GameEngine.TyranoScript;
            if (IsUnrealGame(rootPath)) return GameEngine.Unreal;
            if (IsNwjsGame(rootPath)) return GameEngine.Nwjs;
            if (IsJavaJarGame(rootPath)) return GameEngine.JavaJar;
            if (IsFlashGame(rootPath)) return GameEngine.Flash;
            if (AssetCollectors.IsHtmlGame(rootPath)) return GameEngine.Html;
            if (AssetCollectors.IsQspGame(rootPath)) return GameEngine.Qsp;
            if (AssetCollectors.IsRagsInput(rootPath)) return GameEngine.Rags;
            return GameEngine.Unknown;
        }

        private static GameEngine DetectEngineFast(string inputPath)
        {
            GameEngine direct = DetectDirectFileEngine(inputPath);
            if (direct != GameEngine.Unknown) return direct;
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath)) return GameEngine.Unknown;
            if (IsUnityGame(rootPath)) return GameEngine.Unity;
            if (IsRenpyGameFast(rootPath)) return GameEngine.Renpy;
            if (HasRpgmFilesFast(rootPath)) return GameEngine.RpgMaker;
            if (IsGodotGameFast(rootPath)) return GameEngine.Godot;
            if (IsKirikiriGameFast(rootPath)) return GameEngine.Kirikiri;
            if (IsWolfRpgGame(rootPath)) return GameEngine.WolfRpg;
            if (IsTyranoScriptGame(rootPath)) return GameEngine.TyranoScript;
            if (IsUnrealGameFast(rootPath)) return GameEngine.Unreal;
            if (IsNwjsGame(rootPath)) return GameEngine.Nwjs;
            if (IsJavaJarGame(rootPath)) return GameEngine.JavaJar;
            if (IsFlashGame(rootPath)) return GameEngine.Flash;
            if (AssetCollectors.IsHtmlGame(rootPath)) return GameEngine.Html;
            if (AssetCollectors.IsQspGame(rootPath)) return GameEngine.Qsp;
            if (AssetCollectors.IsRagsInput(rootPath)) return GameEngine.Rags;
            return GameEngine.Unknown;
        }

        private static GameEngine DetectDirectFileEngine(string path)
        {
            if (!File.Exists(path)) return GameEngine.Unknown;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".rpa": return GameEngine.Renpy;
                case ".pck": return GameEngine.Godot;
                case ".xp3": return GameEngine.Kirikiri;
                case ".pak":
                case ".utoc": return GameEngine.Unreal;
                case ".jar": return GameEngine.JavaJar;
                case ".swf": return GameEngine.Flash;
                case ".qsp": return GameEngine.Qsp;
                case ".rag": return GameEngine.Rags;
                default: return GameEngine.Unknown;
            }
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
                || EnumerateFilesSafe(gameFolder, "*.rpy").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpyc").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpym").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpymc").Any()
                || File.Exists(Path.Combine(rootPath, "renpy.exe"));
        }

        private static bool IsRenpyGameFast(string rootPath)
        {
            string gameFolder = Path.Combine(rootPath, "game");
            if (!Directory.Exists(gameFolder)) return false;
            return EnumerateFilesTopLevelSafe(gameFolder, "*.rpa").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpy").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpyc").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpym").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpymc").Any()
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

        private static bool IsWolfRpgGame(string rootPath)
        {
            List<string> archives = FindWolfArchiveFiles(rootPath);
            bool hasWolfArchive = archives.Any(delegate(string path)
            {
                return path.EndsWith(".wolf", StringComparison.OrdinalIgnoreCase);
            });
            bool hasGameExe = File.Exists(Path.Combine(rootPath, "Game.exe"))
                || File.Exists(Path.Combine(rootPath, "GamePro.exe"));
            string dataFolder = Path.Combine(rootPath, "Data");
            bool hasLooseData = Directory.Exists(Path.Combine(dataFolder, "BasicData"))
                || File.Exists(Path.Combine(dataFolder, "BasicData", "Game.dat"));
            return hasWolfArchive || (hasGameExe && (archives.Count > 0 || hasLooseData));
        }

        private static bool IsTyranoScriptGame(string rootPath)
        {
            string dataFolder = Path.Combine(rootPath, "data");
            return Directory.Exists(Path.Combine(dataFolder, "scenario"))
                && (Directory.Exists(Path.Combine(dataFolder, "system"))
                    || Directory.Exists(Path.Combine(rootPath, "tyrano"))
                    || File.Exists(Path.Combine(rootPath, "index.html")));
        }

        private static bool IsJavaJarGame(string rootPath)
        {
            return EnumerateFilesTopLevelSafe(rootPath, "*.jar").Any() || IsJavaLooseResourceGame(rootPath);
        }

        private static bool IsFlashGame(string rootPath)
        {
            return EnumerateFilesTopLevelSafe(rootPath, "*.swf").Any();
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

        private static ScanSummary BuildScanSummary(string inputPath)
        {
            string rootPath = InputDirectory(inputPath);
            GameEngine engine = DetectEngine(inputPath);
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
                List<string> renpyArchives = FindRenpyArchives(inputPath);
                files = renpyArchives.Count > 0
                    ? renpyArchives
                    : AssetCollectors.GetLooseResourceFiles(GetRenpyGameFolder(inputPath), Path.Combine(rootPath, "extracted", "renpy", "loose"));
                archives = renpyArchives.Count;
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
            else if (engine == GameEngine.WolfRpg)
            {
                List<string> wolfArchives = FindWolfArchiveFiles(rootPath);
                files = GetWolfLooseFiles(rootPath, Path.Combine(rootPath, "extracted", "wolf"))
                    .Concat(wolfArchives)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                archives = wolfArchives.Count;
            }
            else if (engine == GameEngine.TyranoScript)
            {
                files = GetTyranoFiles(rootPath, Path.Combine(rootPath, "extracted", "tyrano")).ToList();
                archives = 0;
            }
            else if (engine == GameEngine.JavaJar)
            {
                List<string> javaArchives = FindJavaArchives(inputPath);
                files = GetJavaLooseFiles(rootPath, Path.Combine(rootPath, "extracted", "java"))
                    .Concat(javaArchives)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                archives = javaArchives.Count;
            }
            else if (engine == GameEngine.Flash)
            {
                files = FindFlashFiles(inputPath);
                archives = files.Count();
            }
            else if (engine == GameEngine.Html)
            {
                files = AssetCollectors.GetHtmlFiles(inputPath, Path.Combine(rootPath, "extracted", "html"));
                archives = 0;
            }
            else if (engine == GameEngine.Qsp)
            {
                files = AssetCollectors.GetQspFiles(inputPath, Path.Combine(rootPath, "extracted", "qsp"));
                archives = files.Count(delegate(string path) { return path.EndsWith(".qsp", StringComparison.OrdinalIgnoreCase); });
            }
            else if (engine == GameEngine.Rags)
            {
                files = AssetCollectors.FindInputFiles(inputPath, ".rag");
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

        private static string GetRenpyGameFolder(string inputPath)
        {
            if (File.Exists(inputPath)) return Path.GetDirectoryName(Path.GetFullPath(inputPath));
            string rootPath = InputDirectory(inputPath);
            string gameFolder = Path.Combine(rootPath, "game");
            return Directory.Exists(gameFolder) ? gameFolder : rootPath;
        }

        private static List<string> FindRenpyArchives(string inputPath)
        {
            if (File.Exists(inputPath) && inputPath.EndsWith(".rpa", StringComparison.OrdinalIgnoreCase))
                return new List<string> { Path.GetFullPath(inputPath) };
            return EnumerateFilesSafe(GetRenpyGameFolder(inputPath), "*.rpa").ToList();
        }

        private static List<string> FindWolfArchiveFiles(string rootPath)
        {
            string[] extensions = { ".wolf", ".data", ".pak", ".bin", ".assets", ".content", ".res", ".resource" };
            IEnumerable<string> roots = new[] { rootPath, Path.Combine(rootPath, "Data") }.Where(Directory.Exists);
            return roots
                .SelectMany(delegate(string folder) { return EnumerateFilesTopLevelSafe(folder, "*.*"); })
                .Where(delegate(string path) { return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetWolfLooseFiles(string rootPath, string outputDir)
        {
            string dataFolder = Path.Combine(rootPath, "Data");
            if (!Directory.Exists(dataFolder)) return new List<string>();
            HashSet<string> archives = new HashSet<string>(FindWolfArchiveFiles(rootPath), StringComparer.OrdinalIgnoreCase);
            return GetLooseFiles(dataFolder, outputDir)
                .Where(delegate(string path) { return !archives.Contains(path); })
                .ToList();
        }

        private static List<string> GetTyranoFiles(string rootPath, string outputDir)
        {
            string dataFolder = Path.Combine(rootPath, "data");
            return Directory.Exists(dataFolder) ? GetLooseFiles(dataFolder, outputDir) : new List<string>();
        }

        private static List<string> FindJavaArchives(string rootPath)
        {
            return AssetCollectors.FindInputFiles(rootPath, ".jar")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsJavaLooseResourceGame(string rootPath)
        {
            string resources = Path.Combine(rootPath, "res");
            if (!Directory.Exists(resources) || !EnumerateFilesTopLevelSafe(rootPath, "*.exe").Any())
                return false;
            try
            {
                return Directory.EnumerateDirectories(rootPath, "jre*", SearchOption.TopDirectoryOnly).Any();
            }
            catch { return false; }
        }

        private static List<string> GetJavaLooseFiles(string rootPath, string outputDir)
        {
            string resources = Path.Combine(rootPath, "res");
            return IsJavaLooseResourceGame(rootPath) ? GetLooseFiles(resources, outputDir) : new List<string>();
        }

        private static List<string> FindFlashFiles(string rootPath)
        {
            return AssetCollectors.FindInputFiles(rootPath, ".swf")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsExistingInput(string path)
        {
            return Directory.Exists(path) || File.Exists(path);
        }

        private static string InputDirectory(string path)
        {
            return AssetCollectors.InputDirectory(path);
        }

        private static List<string> GetLooseFiles(string sourceRoot, string outputDir)
        {
            string outputPrefix = AppendDirectorySeparator(Path.GetFullPath(outputDir));
            string extractedPrefix = AppendDirectorySeparator(Path.GetFullPath(Path.Combine(sourceRoot, "extracted")));
            return EnumerateFilesSafe(sourceRoot, "*.*")
                .Where(delegate(string path)
                {
                    string fullPath = Path.GetFullPath(path);
                    return !fullPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)
                        && !fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase);
                })
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
                    || IsWolfRpgGame(root)
                    || IsTyranoScriptGame(root)
                    || IsUnrealGameFast(root)
                    || IsJavaJarGame(root)
                    || IsFlashGame(root)
                    || AssetCollectors.IsHtmlGame(root)
                    || AssetCollectors.IsQspGame(root)
                    || AssetCollectors.IsRagsInput(root)
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
                case GameEngine.WolfRpg: return "WOLF RPG";
                case GameEngine.TyranoScript: return "TyranoScript";
                case GameEngine.JavaJar: return "Java game / JAR";
                case GameEngine.Flash: return "Flash SWF experimental";
                case GameEngine.Html: return "HTML game";
                case GameEngine.Qsp: return "QSP";
                case GameEngine.Rags: return "RAGS experimental";
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
                case GameEngine.WolfRpg: return Color.FromArgb(110, 205, 150);
                case GameEngine.TyranoScript: return Color.FromArgb(255, 140, 190);
                case GameEngine.JavaJar: return Color.FromArgb(235, 155, 75);
                case GameEngine.Flash: return Color.FromArgb(225, 80, 75);
                case GameEngine.Html: return Color.FromArgb(85, 180, 235);
                case GameEngine.Qsp: return Color.FromArgb(205, 165, 85);
                case GameEngine.Rags: return Color.FromArgb(190, 120, 210);
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

        private static string GetSafeChildPath(string rootPath, string relativePath)
        {
            string root = AppendDirectorySeparator(Path.GetFullPath(rootPath));
            string destination = Path.GetFullPath(Path.Combine(rootPath, SanitizeRelativePath(relativePath)));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Path escapes temporary folder: " + relativePath);
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
            Nwjs,
            WolfRpg,
            TyranoScript,
            JavaJar,
            Flash,
            Html,
            Qsp,
            Rags
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
                    "Game Asset Tool v1.8.0 report",
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
            public ResultsDialog(OperationResult result, string reportPath, bool russian)
            {
                Text = russian ? "Результаты извлечения" : "Extraction Results";
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(620, 390);
                MinimumSize = new Size(620, 390);
                BackColor = Color.FromArgb(17, 19, 24);
                ForeColor = Color.FromArgb(239, 243, 248);

                Controls.Add(new Label
                {
                    Text = result.Errors == 0
                        ? (russian ? "Извлечение завершено" : "Extraction complete")
                        : (russian ? "Извлечение завершено с предупреждениями" : "Extraction complete with warnings"),
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
                    Text = russian ? "Открыть результат" : "Open Output Folder",
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
                    Text = russian ? "Закрыть" : "Close",
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
