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
    public sealed partial class RpgmvpConverterForm
    {
        private void BuildUi()
        {
            Font uiFont = new Font("Segoe UI", 9f, FontStyle.Regular);
            Font uiBold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            Font titleFont = new Font("Segoe UI Semibold", 14f, FontStyle.Regular);
            Font logFont = new Font("Consolas", 9.5f, FontStyle.Regular);

            Text = "Game Asset Tool v2.4.1";
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
                Size = new Size(360, 28),
                BackColor = Color.Transparent
            });
            subtitleLabel = new Label
            {
                Text = "Drop a game folder or file here, scan it, then extract or unlock",
                ForeColor = mutedColor,
                Location = new Point(19, 39),
                Size = new Size(380, 20),
                BackColor = Color.Transparent
            };
            header.Controls.Add(subtitleLabel);
            runtimeStatusLabel = new Label
            {
                Text = "Runtime: waiting for a supported folder",
                ForeColor = mutedColor,
                Location = new Point(500, 24),
                Size = new Size(245, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            header.Controls.Add(runtimeStatusLabel);
            healthCheckButton = CreateButton("Health", new Point(755, 20), new Size(82, 28), Color.FromArgb(45, 50, 60), textColor, uiBold);
            healthCheckButton.Click += delegate { RunHealthCheck(); };
            header.Controls.Add(healthCheckButton);
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
                Size = new Size(500, 20),
                ForeColor = mutedColor
            };
            scanPanel.Controls.Add(scanSummaryLabel);
            engineOverrideBox = new ComboBox
            {
                Location = new Point(530, 13),
                Size = new Size(178, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            engineOverrideBox.SelectedIndexChanged += delegate
            {
                if (!updatingEngineOverrideItems) OnEngineOverrideChanged();
            };
            scanPanel.Controls.Add(engineOverrideBox);
            dryRunButton = CreateButton("Dry Run / Scan", new Point(718, 13), new Size(158, 30), Color.FromArgb(45, 50, 60), textColor, uiBold);
            dryRunButton.Click += async delegate { await RunDryScanAsync(true); };
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
            javaModeLabel = new Label
            {
                Text = "Java mode",
                Location = new Point(8, 40),
                Size = new Size(100, 20),
                ForeColor = mutedColor,
                Visible = false
            };
            extractionPanel.Controls.Add(javaModeLabel);
            javaExtractModeBox = new ComboBox
            {
                Location = new Point(112, 36),
                Size = new Size(250, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBack,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            javaExtractModeBox.Items.Add("Images only");
            javaExtractModeBox.Items.Add("Images + SVG previews");
            javaExtractModeBox.Items.Add("All resources");
            javaExtractModeBox.SelectedIndex = 1;
            extractionPanel.Controls.Add(javaExtractModeBox);
            extractModeMenu = new ContextMenuStrip();
            extractModeMenu.BackColor = panelBack;
            extractModeMenu.ForeColor = textColor;
            extractModeMenu.ShowCheckMargin = false;
            extractModeMenu.ShowImageMargin = false;
            looseModeMenu = new ContextMenuStrip();
            looseModeMenu.BackColor = panelBack;
            looseModeMenu.ForeColor = textColor;
            looseModeMenu.ShowCheckMargin = false;
            looseModeMenu.ShowImageMargin = false;

            startButton = CreateButton("Extract: Auto", new Point(704, 8), new Size(150, 26), successColor, formBack, uiBold);
            startButton.Click += async delegate { await StartDetectedExtractionAsync(); };
            extractionPanel.Controls.Add(startButton);
            extractModeButton = CreateButton("▼", new Point(858, 8), new Size(26, 26), Color.FromArgb(45, 50, 60), textColor, uiBold);
            extractModeButton.Click += delegate { ShowModeMenu(extractModeMenu, extractModeButton); };
            extractionPanel.Controls.Add(extractModeButton);

            collectLooseButton = CreateButton("Loose: Images + Video", new Point(704, 39), new Size(150, 26), Color.FromArgb(45, 50, 60), textColor, uiBold);
            collectLooseButton.Click += async delegate { await StartLooseResourceCollectionAsync(); };
            extractionPanel.Controls.Add(collectLooseButton);
            looseModeButton = CreateButton("▼", new Point(858, 39), new Size(26, 26), Color.FromArgb(45, 50, 60), textColor, uiBold);
            looseModeButton.Click += delegate { ShowModeMenu(looseModeMenu, looseModeButton); };
            extractionPanel.Controls.Add(looseModeButton);
            unityDecensorButton = CreateButton("Unity Decensor...", new Point(514, 39), new Size(180, 26), Color.FromArgb(95, 65, 80), textColor, uiBold);
            unityDecensorButton.Click += delegate { OpenUnityDecensor(); };
            unityDecensorButton.Visible = false;
            extractionPanel.Controls.Add(unityDecensorButton);
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
            lastResultButton = CreateButton("Last Result", new Point(426, y), new Size(150, 30), Color.FromArgb(45, 50, 60), textColor, uiBold);
            lastResultButton.Enabled = false;
            lastResultButton.Click += delegate { ShowLastResult(); };
            Controls.Add(lastResultButton);
            toggleLogButton = CreateButton("Show Log", new Point(586, y), new Size(120, 30), Color.FromArgb(45, 50, 60), textColor, uiBold);
            toggleLogButton.Click += delegate { ToggleLog(); };
            Controls.Add(toggleLogButton);
            skipGalleryIndexCheckBox = new CheckBox
            {
                Text = "Skip gallery index",
                Location = new Point(724, y + 5),
                Size = new Size(186, 24),
                ForeColor = mutedColor,
                BackColor = Color.Transparent,
                Checked = false
            };
            Controls.Add(skipGalleryIndexCheckBox);
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

        private void BuildModeMenus()
        {
            if (extractModeMenu != null)
            {
                extractModeMenu.Items.Clear();
                AddModeItem(extractModeMenu, ExtractionProfileDisplayName(0), selectedExtractionProfileIndex == 0, delegate { SelectExtractionProfile(0); });
                AddModeItem(extractModeMenu, ExtractionProfileDisplayName(1), selectedExtractionProfileIndex == 1, delegate { SelectExtractionProfile(1); });
                AddModeItem(extractModeMenu, ExtractionProfileDisplayName(2), selectedExtractionProfileIndex == 2, delegate { SelectExtractionProfile(2); });
                AddModeItem(extractModeMenu, ExtractionProfileDisplayName(3), selectedExtractionProfileIndex == 3, delegate { SelectExtractionProfile(3); });
                AddModeItem(extractModeMenu, ExtractionProfileDisplayName(4), selectedExtractionProfileIndex == 4, delegate { SelectExtractionProfile(4); });
                AddModeItem(extractModeMenu, ExtractionProfileDisplayName(5), selectedExtractionProfileIndex == 5, delegate { SelectExtractionProfile(5); });
            }

            if (looseModeMenu != null)
            {
                looseModeMenu.Items.Clear();
                AddModeItem(looseModeMenu, LooseModeDisplayName(0), selectedLooseModeIndex == 0, delegate { SelectLooseMode(0); });
                AddModeItem(looseModeMenu, LooseModeDisplayName(1), selectedLooseModeIndex == 1, delegate { SelectLooseMode(1); });
                AddModeItem(looseModeMenu, LooseModeDisplayName(2), selectedLooseModeIndex == 2, delegate { SelectLooseMode(2); });
            }
        }

        private void AddModeItem(ContextMenuStrip menu, string text, bool selected, EventHandler clickHandler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem((selected ? "✓ " : "   ") + text);
            item.Checked = false;
            item.ForeColor = selected ? accentColor : textColor;
            item.BackColor = panelBack;
            item.Font = new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular);
            item.Click += clickHandler;
            menu.Items.Add(item);
        }

        private void ShowModeMenu(ContextMenuStrip menu, Control anchor)
        {
            if (menu == null || anchor == null || !anchor.Enabled) return;
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void SetOptionControlsEnabled(bool enabled)
        {
            if (engineOverrideBox != null) engineOverrideBox.Enabled = enabled;
            if (skipGalleryIndexCheckBox != null) skipGalleryIndexCheckBox.Enabled = enabled;
        }
        private void SelectExtractionProfile(int index)
        {
            selectedExtractionProfileIndex = Math.Max(0, Math.Min(5, index));
            BuildModeMenus();
            UpdateModeButtonTexts();
            OnProfileChanged();
        }

        private void SelectLooseMode(int index)
        {
            selectedLooseModeIndex = Math.Max(0, Math.Min(2, index));
            BuildModeMenus();
            UpdateModeButtonTexts();
        }

        private void UpdateModeButtonTexts()
        {
            if (startButton != null)
            {
                string verb = selectedEngine == GameEngine.Unknown || IsRecoveryProfile()
                    ? T("Recover", "Поиск")
                    : T("Extract", "Извлечь");
                if (IsDiagnosticsOnlyProfile()) verb = T("Diagnostics", "Диагностика");
                startButton.Text = verb + ": " + ExtractionProfileShortName(selectedExtractionProfileIndex);
            }

            if (collectLooseButton != null)
            {
                collectLooseButton.Text = T("Loose: ", "Открытые: ") + LooseModeShortName(selectedLooseModeIndex);
            }
        }

        private string ExtractionProfileDisplayName(int index)
        {
            switch (index)
            {
                case 1: return T("Images only", "Только изображения");
                case 2: return T("Images + Video", "Изображения + видео");
                case 3: return T("Everything", "Все файлы");
                case 4: return T("Diagnostics only", "Только диагностика");
                case 5: return T("Recovery mode", "Восстановление");
                default: return T("Auto: Images + Video", "Авто: изображения + видео");
            }
        }

        private string ExtractionProfileShortName(int index)
        {
            switch (index)
            {
                case 1: return T("Images", "Изобр.");
                case 2: return T("Media", "Медиа");
                case 3: return T("Everything", "Все");
                case 4: return T("Only", "Только");
                case 5: return T("Recovery", "Восст.");
                default: return T("Auto media", "Авто медиа");
            }
        }

        private string LooseModeDisplayName(int index)
        {
            switch (index)
            {
                case 1: return T("Images only", "Только изображения");
                case 2: return T("All loose files", "Все открытые файлы");
                default: return T("Images + Video", "Изображения + видео");
            }
        }

        private string LooseModeShortName(int index)
        {
            switch (index)
            {
                case 1: return T("Images", "Изобр.");
                case 2: return T("All", "Все");
                default: return T("Media", "Медиа");
            }
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
            extractModeButton.Text = "▼";
            looseModeButton.Text = "▼";
            unityDecensorButton.Text = T("Unity Decensor...", "Unity Decensor...");
            unlockerButton.Text = T("Install Unlocker", "Установить анлокер");
            removeUnlockerButton.Text = T("Remove Unlocker", "Удалить анлокер");
            pauseButton.Text = T("Pause", "Пауза");
            cancelButton.Text = T("Cancel", "Отмена");
            openOutputButton.Text = T("Open Output Folder", "Открыть папку");
            lastResultButton.Text = T("Last Result", "Последний отчёт");
            healthCheckButton.Text = T("Health", "Проверка");
            toggleLogButton.Text = logExpanded ? T("Hide Log", "Скрыть лог") : T("Show Log", "Показать лог");
            clearLogButton.Text = T("Clear", "Очистить");
            if (skipGalleryIndexCheckBox != null) skipGalleryIndexCheckBox.Text = T("Skip gallery index", "Без индекса галереи");
            BuildEngineOverrideItems();

            BuildModeMenus();
            UpdateModeButtonTexts();
            int javaMode = javaExtractModeBox.SelectedIndex;
            javaExtractModeBox.Items.Clear();
            javaExtractModeBox.Items.Add(T("Images only", "Только изображения"));
            javaExtractModeBox.Items.Add(T("Images + SVG previews", "Изображения + PNG-превью SVG"));
            javaExtractModeBox.Items.Add(T("All resources", "Все ресурсы"));
            javaExtractModeBox.SelectedIndex = javaMode >= 0 ? javaMode : 1;

            int unlockerMode = unlockerModeBox.SelectedIndex;
            unlockerModeBox.Items.Clear();
            unlockerModeBox.Items.Add(T("Soft", "Мягкий"));
            unlockerModeBox.Items.Add(T("Hard", "Жёсткий"));
            unlockerModeBox.SelectedIndex = unlockerMode >= 0 ? unlockerMode : 0;

            string path = pathBox.Text.Trim();
            if (IsExistingInput(path))
            {
                detectedEngineLabel.Text = BuildEngineLabel(EffectiveEngine(DetectEngineFast(path)));
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

            UpdateEngineContext(EffectiveEngine(selectedEngine));
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
            if (paths.Length > 1)
            {
                WriteLog("Multiple dropped items detected; using the first path only.");
                scanSummaryLabel.Text = T(
                    "Multiple items dropped. Using the first path only.",
                    "Перетащено несколько элементов. Используется только первый путь.");
            }
        }

        private void ApplyGamePath(string path, bool scan)
        {
            string detectedRoot = IsExistingInput(path) ? null : TryFindGameRoot(path);
            pathBox.Text = string.IsNullOrWhiteSpace(detectedRoot) ? path : detectedRoot;
            TryAutoDetectKey(InputDirectory(pathBox.Text));
            if (scan && IsHandleCreated && Visible)
            {
                Task ignored = RunDryScanAsync(false);
            }
        }

        private void OnProfileChanged()
        {
            string path = pathBox.Text.Trim();
            GameEngine engine = IsExistingInput(path) ? DetectEngineFast(path) : GameEngine.Unknown;
            UpdateEngineContext(EffectiveEngine(engine));
        }

        private GameEngine EffectiveEngine(GameEngine detected)
        {
            if (CurrentExtractionProfile().IsRecovery) return GameEngine.Unknown;
            GameEngine forced = SelectedForcedEngine();
            return forced == GameEngine.Unknown ? detected : forced;
        }

        private void BuildEngineOverrideItems()
        {
            if (engineOverrideBox == null) return;
            int selected = engineOverrideBox.SelectedIndex;
            updatingEngineOverrideItems = true;
            try
            {
                engineOverrideBox.Items.Clear();
                engineOverrideBox.Items.Add(T("Auto engine", "Авто движок"));
                engineOverrideBox.Items.Add("RPG Maker MV/MZ");
                engineOverrideBox.Items.Add("Ren'Py");
                engineOverrideBox.Items.Add("Unity");
                engineOverrideBox.Items.Add("Godot");
                engineOverrideBox.Items.Add("KiriKiri XP3");
                engineOverrideBox.Items.Add("Unreal PAK");
                engineOverrideBox.Items.Add("NWJS");
                engineOverrideBox.Items.Add("WOLF RPG");
                engineOverrideBox.Items.Add("TyranoScript");
                engineOverrideBox.Items.Add("Java / JAR");
                engineOverrideBox.Items.Add("Flash SWF");
                engineOverrideBox.Items.Add("Electron ASAR");
                engineOverrideBox.Items.Add("HTML");
                engineOverrideBox.Items.Add("QSP");
                engineOverrideBox.Items.Add("RAGS");
                engineOverrideBox.Items.Add("RPG Maker XP/VX");
                engineOverrideBox.Items.Add("GameMaker");
                engineOverrideBox.Items.Add("Android APK");
                engineOverrideBox.Items.Add("SRPG Studio");
                engineOverrideBox.Items.Add("Pixel Game Maker");
                engineOverrideBox.Items.Add("SPAK DAT");
                engineOverrideBox.Items.Add("Pygame / PyInstaller");
                engineOverrideBox.SelectedIndex = selected >= 0 && selected < engineOverrideBox.Items.Count ? selected : 0;
            }
            finally
            {
                updatingEngineOverrideItems = false;
            }
        }

        private void OnEngineOverrideChanged()
        {
            string path = pathBox == null ? "" : pathBox.Text.Trim();
            if (IsExistingInput(path))
                UpdateEngineContext(EffectiveEngine(DetectEngineFast(path)));
            else
                UpdateEngineContext(GameEngine.Unknown);
            UpdateDetectedEngineLabel();
            UpdateActionTooltips();
        }

        private GameEngine SelectedForcedEngine()
        {
            if (engineOverrideBox == null || engineOverrideBox.SelectedIndex <= 0) return GameEngine.Unknown;
            switch (engineOverrideBox.SelectedIndex)
            {
                case 1: return GameEngine.RpgMaker;
                case 2: return GameEngine.Renpy;
                case 3: return GameEngine.Unity;
                case 4: return GameEngine.Godot;
                case 5: return GameEngine.Kirikiri;
                case 6: return GameEngine.Unreal;
                case 7: return GameEngine.Nwjs;
                case 8: return GameEngine.WolfRpg;
                case 9: return GameEngine.TyranoScript;
                case 10: return GameEngine.JavaJar;
                case 11: return GameEngine.Flash;
                case 12: return GameEngine.Electron;
                case 13: return GameEngine.Html;
                case 14: return GameEngine.Qsp;
                case 15: return GameEngine.Rags;
                case 16: return GameEngine.LegacyRpgMaker;
                case 17: return GameEngine.GameMaker;
                case 18: return GameEngine.AndroidApk;
                case 19: return GameEngine.SrpgStudio;
                case 20: return GameEngine.PixelGameMaker;
                case 21: return GameEngine.SpakDat;
                case 22: return GameEngine.PygamePyInstaller;
                default: return GameEngine.Unknown;
            }
        }

        private string BuildEngineLabel(GameEngine engine)
        {
            string suffix = SelectedForcedEngine() == GameEngine.Unknown ? "" : T(" (forced)", " (вручную)");
            return T("Engine: ", "Движок: ") + EngineName(engine) + suffix;
        }

        private void UpdateDetectedEngineLabel()
        {
            if (detectedEngineLabel == null) return;
            string path = pathBox == null ? "" : pathBox.Text.Trim();
            GameEngine engine = IsExistingInput(path) ? EffectiveEngine(DetectEngineFast(path)) : GameEngine.Unknown;
            detectedEngineLabel.Text = engine == GameEngine.Unknown && SelectedForcedEngine() == GameEngine.Unknown
                ? T("Engine: not detected", "Движок: не определён")
                : BuildEngineLabel(engine);
            detectedEngineLabel.ForeColor = engine == GameEngine.Unknown ? mutedColor : EngineColor(engine);
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
            GameEngine engine = EffectiveEngine(DetectEngineFast(path));
            detectedEngineLabel.Text = BuildEngineLabel(engine);
            detectedEngineLabel.ForeColor = EngineColor(engine);
            scanSummaryLabel.Text = T(
                "Ready to scan. Click Dry Run / Scan to inspect files before extraction.",
                "Готово к проверке. Нажмите «Проверить», чтобы просмотреть файлы перед извлечением.");
            UpdateEngineContext(engine);
        }

        private async Task RunDryScanAsync(bool showLog)
        {
            if (dryScanRunning)
            {
                CancelDryScan();
                return;
            }

            string inputPath = pathBox.Text.Trim();
            if (!IsExistingInput(inputPath))
            {
                WriteLog("Invalid path");
                return;
            }

            Cursor previous = Cursor;
            dryScanCancellation = new CancellationTokenSource();
            CancellationToken token = dryScanCancellation.Token;
            SetDryScanRunningState(true);
            Cursor = Cursors.WaitCursor;
            try
            {
                scanSummaryLabel.Text = T("Scanning in background. Click Cancel Scan to stop.", "Проверка в фоне. Нажмите «Отмена», чтобы остановить.");
                statusLabel.Text = T("Dry Run: scanning", "Проверка: сканирование");
                statsLabel.Text = T("Reading folders and archive lists...", "Чтение папок и списка архивов...");
                ScanSummary summary;
                string cacheKey = inputPath + "|engine=" + SelectedForcedEngine().ToString();
                if (dryScanCache.TryGet(cacheKey, out summary))
                {
                    if (showLog) WriteLog("Dry run: using cached scan summary.");
                }
                else
                {
                    GameEngine forcedEngine = SelectedForcedEngine();
                    summary = await Task.Run(delegate { return BuildScanSummaryCore(inputPath, token, forcedEngine); }, token);
                    dryScanCache.Store(cacheKey, summary);
                }
                token.ThrowIfCancellationRequested();
                if (!string.Equals(pathBox.Text.Trim(), inputPath, StringComparison.OrdinalIgnoreCase))
                    return;
                GameEngine engine = EffectiveEngine(summary.Engine);
                detectedEngineLabel.Text = BuildEngineLabel(engine);
                detectedEngineLabel.ForeColor = EngineColor(engine);
                string scanSummary = string.Format(
                    T(
                        "{0} archive(s), {1} candidate file(s), input size {2} (not estimated output)",
                        "{0} архив(а), {1} подходящих файлов, входной размер {2} (не оценка результата)"),
                    summary.ArchiveCount,
                    summary.FileCount,
                    FormatBytes(summary.TotalBytes));
                if (!string.IsNullOrWhiteSpace(summary.UnknownExtensions))
                    scanSummary += T(" | Unknown: ", " | Неизвестные: ") + summary.UnknownExtensions;
                if (!string.IsNullOrWhiteSpace(summary.TopExtensions))
                    scanSummary += T(" | Top: ", " | Топ: ") + summary.TopExtensions;
                if (!string.IsNullOrWhiteSpace(summary.RouteHints))
                    scanSummary += T(" | Route: ", " | Маршрут: ") + ShortUiText(summary.RouteHints, 140);
                scanSummaryLabel.Text = scanSummary;
                string preflight = BuildPreflightSummary(inputPath, engine, summary);
                statsLabel.Text = ShortUiText(preflight.Replace(Environment.NewLine, " | "), 150);
                UpdateEngineContext(engine);
                if (showLog)
                {
                    WriteLog("Dry run: " + EngineName(summary.Engine) + (SelectedForcedEngine() == GameEngine.Unknown ? "" : " (forced)"));
                    WriteLog("Found: " + summary.ArchiveCount + " archive(s), " + summary.FileCount + " candidate file(s), input size " + FormatBytes(summary.TotalBytes));
                    foreach (string line in preflight.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                        WriteLog(line);
                    if (!string.IsNullOrWhiteSpace(summary.UnknownExtensions))
                        WriteLog("Unknown extensions: " + summary.UnknownExtensions);
                    if (!string.IsNullOrWhiteSpace(summary.TopExtensions))
                        WriteLog("Top extensions: " + summary.TopExtensions);
                    if (!string.IsNullOrWhiteSpace(summary.LargestFiles))
                        WriteLog("Largest inputs: " + summary.LargestFiles);
                    if (!string.IsNullOrWhiteSpace(summary.RouteHints))
                        WriteLog("Route hints: " + summary.RouteHints);
                }
            }
            catch (OperationCanceledException)
            {
                scanSummaryLabel.Text = T("Dry Run cancelled.", "Проверка отменена.");
                statusLabel.Text = T("Scan cancelled", "Проверка отменена");
                if (showLog) WriteLog("Dry run cancelled.");
            }
            catch (Exception ex)
            {
                scanSummaryLabel.Text = T("Dry Run failed. See log.", "Проверка не удалась. Смотрите лог.");
                WriteLog("Dry run failed: " + ex.Message);
            }
            finally
            {
                SetDryScanRunningState(false);
                if (dryScanCancellation != null)
                {
                    dryScanCancellation.Dispose();
                    dryScanCancellation = null;
                }
                Cursor = previous;
            }
        }

        private string BuildPreflightSummary(string inputPath, GameEngine engine, ScanSummary summary)
        {
            string rootPath = InputDirectory(inputPath);
            return PreflightBuilder.Build(new PreflightInfo
            {
                EngineName = EngineName(engine),
                ProfileName = ExtractionProfileName(),
                OutputDir = GetDefaultOutputFolder(rootPath, engine),
                FileCount = summary.FileCount,
                ArchiveCount = summary.ArchiveCount,
                TotalSize = FormatBytes(summary.TotalBytes),
                ExistingOutputPolicy = "ask",
                DiagnosticsOnly = IsDiagnosticsOnlyProfile(),
                UsesPortableRuntime = engine == GameEngine.Unity
                    || engine == GameEngine.Renpy
                    || engine == GameEngine.Godot
                    || engine == GameEngine.Kirikiri
                    || engine == GameEngine.Unreal
                    || engine == GameEngine.GameMaker
                    || engine == GameEngine.SpakDat,
                IsRpgMaker = engine == GameEngine.RpgMaker,
                HasManualKey = keyBox != null && !string.IsNullOrWhiteSpace(keyBox.Text),
                IsUnreal = engine == GameEngine.Unreal,
                IsUnity = engine == GameEngine.Unity,
                UnityMode = UnityModeValue(),
                UnknownExtensions = summary.UnknownExtensions
            });
        }

        private static string GetDefaultOutputFolder(string rootPath, GameEngine engine)
        {
            switch (engine)
            {
                case GameEngine.RpgMaker: return Path.Combine(rootPath, "extracted", "rpgm");
                case GameEngine.Renpy: return Path.Combine(rootPath, "extracted", "renpy");
                case GameEngine.Unity: return Path.Combine(rootPath, "extracted", "unity");
                case GameEngine.Godot: return Path.Combine(rootPath, "extracted", "godot");
                case GameEngine.Kirikiri: return Path.Combine(rootPath, "extracted", "kirikiri");
                case GameEngine.Unreal: return Path.Combine(rootPath, "extracted", "unreal");
                case GameEngine.Nwjs: return Path.Combine(rootPath, "extracted", "nwjs");
                case GameEngine.WolfRpg: return Path.Combine(rootPath, "extracted", "wolf");
                case GameEngine.TyranoScript: return Path.Combine(rootPath, "extracted", "tyrano");
                case GameEngine.JavaJar: return Path.Combine(rootPath, "extracted", "java");
                case GameEngine.AndroidApk: return Path.Combine(rootPath, "extracted", "android-apk");
                case GameEngine.SrpgStudio: return Path.Combine(rootPath, "extracted", "srpg-studio");
                case GameEngine.PixelGameMaker: return Path.Combine(rootPath, "extracted", "pixel-game-maker");
                case GameEngine.Flash: return Path.Combine(rootPath, "extracted", "flash");
                case GameEngine.Electron: return Path.Combine(rootPath, "extracted", "electron");
                case GameEngine.Html: return Path.Combine(rootPath, "extracted", "html");
                case GameEngine.Qsp: return Path.Combine(rootPath, "extracted", "qsp");
                case GameEngine.Rags: return Path.Combine(rootPath, "extracted", "rags");
                case GameEngine.LegacyRpgMaker: return Path.Combine(rootPath, "extracted", "rgss");
                case GameEngine.GameMaker: return Path.Combine(rootPath, "extracted", "gamemaker");
                case GameEngine.SpakDat: return Path.Combine(rootPath, "extracted", "spak-dat");
                case GameEngine.PygamePyInstaller: return Path.Combine(rootPath, "extracted", "pygame");
                default: return Path.Combine(rootPath, "extracted", "signature-recovery");
            }
        }

        private void CancelDryScan()
        {
            if (dryScanCancellation != null)
                dryScanCancellation.Cancel();
        }

        private void SetDryScanRunningState(bool running)
        {
            dryScanRunning = running;
            pathBox.Enabled = !running;
            browseButton.Enabled = !running;
            extractModeButton.Enabled = !running;
            startButton.Enabled = !running;
            collectLooseButton.Enabled = !running;
            unityDecensorButton.Enabled = !running;
            looseModeButton.Enabled = !running;
            healthCheckButton.Enabled = !running;
            dryRunButton.Enabled = true;
            dryRunButton.Text = running ? T("Cancel Scan", "Отмена") : T("Dry Run / Scan", "Проверить");
            SetOptionControlsEnabled(!running);
            progressBar.Style = running ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (running)
            {
                progressBar.MarqueeAnimationSpeed = 25;
                cancelButton.Enabled = false;
            }
            else
            {
                progressBar.MarqueeAnimationSpeed = 0;
                progressBar.Value = Math.Min(progressBar.Value, progressBar.Maximum);
                cancelButton.Enabled = currentRun != null || externalRunning;
                UpdateEngineContext(selectedEngine);
            }
            UpdateActionTooltips();
        }

        private static string ShortUiText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength) return value;
            return value.Substring(0, Math.Max(1, maxLength - 3)) + "...";
        }

        private void SetRpgmRunningState(bool running)
        {
            pathBox.Enabled = !running;
            browseButton.Enabled = !running;
            dryRunButton.Enabled = !running;
            extractModeButton.Enabled = !running;
            startButton.Enabled = !running;
            collectLooseButton.Enabled = !running;
            unityDecensorButton.Enabled = !running;
            looseModeButton.Enabled = !running;
            healthCheckButton.Enabled = !running;
            UpdateUnlockerControls(running);
            pauseButton.Enabled = running;
            cancelButton.Enabled = running;
            openOutputButton.Enabled = !running && Directory.Exists(lastOutputDir);
            lastResultButton.Enabled = !running && lastOperationResult != null && File.Exists(lastReportPath);
            if (!running) UpdateEngineContext(selectedEngine);
            else UpdateActionTooltips();
        }

        private void SetExternalRunningState(bool running, string operation)
        {
            externalRunning = running;
            if (running)
            {
                externalOperationName = operation;
                externalStartUtc = DateTime.UtcNow;
                externalOutputFiles = 0;
                externalOutputBytes = 0;
                externalLastFile = "";
                StartExternalOutputWatcher();
                uiTimer.Start();
            }
            else
            {
                uiTimer.Stop();
                StopExternalOutputWatcher();
            }
            BeginUi(delegate
            {
                pathBox.Enabled = !running;
                browseButton.Enabled = !running;
                dryRunButton.Enabled = !running;
                extractModeButton.Enabled = !running;
                startButton.Enabled = !running;
                collectLooseButton.Enabled = !running;
                unityDecensorButton.Enabled = !running;
                looseModeButton.Enabled = !running;
                healthCheckButton.Enabled = !running;
                UpdateUnlockerControls(running);
                pauseButton.Enabled = false;
                cancelButton.Enabled = running;
                openOutputButton.Enabled = !running && Directory.Exists(lastOutputDir);
                lastResultButton.Enabled = !running && lastOperationResult != null && File.Exists(lastReportPath);
                if (running)
                {
                    progressBar.Style = ProgressBarStyle.Continuous;
                    progressBar.Maximum = 1;
                    progressBar.Value = 0;
                    statusLabel.Text = operation + T(": starting...", ": запуск...");
                    statsLabel.Text = T("Watching output folder for new files...", "Отслеживание новых файлов в папке результата...");
                }
                else
                {
                    progressBar.Style = ProgressBarStyle.Continuous;
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
            bool showKey = engine == GameEngine.RpgMaker || engine == GameEngine.Godot || engine == GameEngine.Unreal;
            bool isUnity = engine == GameEngine.Unity;
            bool showJavaMode = engine == GameEngine.JavaJar;

            keyLabel.Visible = showKey;
            keyBox.Visible = showKey;
            unityDecensorButton.Visible = isUnity;
            unityDecensorButton.Enabled = !busy && isUnity && Directory.Exists(InputDirectory(pathBox.Text.Trim()));
            javaModeLabel.Visible = showJavaMode;
            javaExtractModeBox.Visible = showJavaMode;
            extractModeButton.Enabled = !busy;
            looseModeButton.Enabled = !busy && IsExistingInput(pathBox.Text.Trim());
            startButton.Enabled = !busy && (CanExtractAssets(engine) || IsExistingInput(pathBox.Text.Trim()));
            collectLooseButton.Enabled = !busy && IsExistingInput(pathBox.Text.Trim());
            UpdateModeButtonTexts();
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
                    extractionHintLabel.Text = T(
                        "Unity extraction follows the selected Extract profile.",
                        "Unity извлекается по выбранному профилю Extract.");
                    break;
                case GameEngine.Renpy:
                    extractionHintLabel.Text = FindRenpyArchives(pathBox.Text.Trim()).Count > 0
                        ? T("RPA archives will be extracted into separate folders.", "Архивы RPA будут извлечены в отдельные папки.")
                        : T("No RPA archives found. Open Ren'Py resources will be collected.", "Архивы RPA не найдены. Будут собраны открытые ресурсы Ren'Py.");
                    break;
                case GameEngine.Godot:
                    keyLabel.Text = T("Godot PCK key", "Ключ Godot PCK");
                    extractionHintLabel.Text = T("PCK extraction. For encrypted archives, enter the key or keep the field empty for automatic discovery.", "Извлечение PCK. Для зашифрованных архивов укажите ключ или оставьте поле пустым для автопоиска.");
                    break;
                case GameEngine.Kirikiri:
                    extractionHintLabel.Text = T("Standard XP3 extraction. Supported TLG5 images also receive PNG previews.", "Извлечение стандартных XP3. Для поддерживаемых изображений TLG5 также создаются PNG-превью.");
                    break;
                case GameEngine.Unreal:
                    keyLabel.Text = T("Unreal AES key", "AES-ключ Unreal");
                    extractionHintLabel.Text = T(
                        "Experimental PAK extraction with AES key discovery and Zlib, Gzip, LZ4, Zstd or Oodle decoding. Oodle has a built-in open-source fallback; a local official DLL is preferred when available. IoStore is reported.",
                        "Экспериментальное извлечение PAK с поиском AES-ключа и распаковкой Zlib, Gzip, LZ4, Zstd или Oodle. Для Oodle встроен open-source fallback; локальная официальная DLL используется при наличии. IoStore отмечается в отчёте.");
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
                    javaModeLabel.Text = T("Java mode", "Режим Java");
                    extractionHintLabel.Text = T(
                        "Choose fast images-only, cached SVG previews or all resources.",
                        "Выберите быстрые изображения, кэшируемые PNG-превью SVG или все ресурсы.");
                    break;
                case GameEngine.Flash:
                    extractionHintLabel.Text = T(
                        "Experimental Flash inspection copies SWF files and extracts embedded JPEG, PNG, GIF and FLV video.",
                        "Экспериментальный анализ Flash копирует SWF и извлекает встроенные JPEG, PNG, GIF и FLV-видео.");
                    break;
                case GameEngine.Electron:
                    extractionHintLabel.Text = T(
                        "Electron app.asar files will be unpacked with archive safety limits.",
                        "Файлы Electron app.asar будут распакованы с защитными лимитами архива.");
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
                case GameEngine.LegacyRpgMaker:
                    extractionHintLabel.Text = T(
                        "RGSSAD, RGSS2A and RGSS3A archives will be extracted by the built-in parser.",
                        "Архивы RGSSAD, RGSS2A и RGSS3A будут извлечены встроенным parser-ом.");
                    break;
                case GameEngine.GameMaker:
                    extractionHintLabel.Text = T(
                        "Experimental data.win recovery preserves the original and extracts PNG, QOI and BZ2QOI texture pages.",
                        "Экспериментальное восстановление data.win сохраняет оригинал и извлекает страницы текстур PNG, QOI и BZ2QOI.");
                    break;
                case GameEngine.AndroidApk:
                    extractionHintLabel.Text = T(
                        "APK files are unpacked as ZIP containers and scanned for embedded assets, including GPU texture containers.",
                        "APK распаковывается как ZIP-контейнер и сканируется на встроенные ресурсы, включая контейнеры GPU-текстур.");
                    break;
                case GameEngine.SrpgStudio:
                    extractionHintLabel.Text = T(
                        "SRPG Studio resources are collected, and .rts/.dts/.srk containers are scanned by signatures.",
                        "Ресурсы SRPG Studio собираются, а контейнеры .rts/.dts/.srk сканируются по сигнатурам.");
                    break;
                case GameEngine.PixelGameMaker:
                    extractionHintLabel.Text = T(
                        "Pixel Game Maker MV open resources are collected, encrypted or packed data is scanned by signatures.",
                        "Открытые ресурсы Pixel Game Maker MV собираются, зашифрованные или упакованные данные сканируются по сигнатурам.");
                    break;
                case GameEngine.SpakDat:
                    extractionHintLabel.Text = T(
                        "Experimental SPAK DAT / SPITE extraction decodes resources whose runtime paths can be recovered. Remaining protected blocks stay as .dat files in protected/.",
                        "Экспериментальное извлечение SPAK DAT / SPITE декодирует ресурсы с восстановленными runtime-путями. Остальные защищённые блоки остаются .dat-файлами в protected/.");
                    break;
                case GameEngine.PygamePyInstaller:
                    extractionHintLabel.Text = T(
                        "Pygame/PyInstaller assets will be collected. XOR-obfuscated image .dat files are decoded when recognized.",
                        "Ресурсы Pygame/PyInstaller будут собраны. XOR-зашифрованные изображения .dat декодируются при распознавании.");
                    break;
                default:
                    extractionHintLabel.Text = T(
                        "Unknown format. Recover embedded media by signatures or collect loose resources.",
                        "Неизвестный формат. Извлеките встроенные медиа по сигнатурам или соберите открытые ресурсы.");
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
                || engine == GameEngine.Electron
                || engine == GameEngine.Html
                || engine == GameEngine.Qsp
                || engine == GameEngine.Rags
                || engine == GameEngine.LegacyRpgMaker
                || engine == GameEngine.GameMaker
                || engine == GameEngine.AndroidApk
                || engine == GameEngine.SrpgStudio
                || engine == GameEngine.PixelGameMaker
                || engine == GameEngine.SpakDat
                || engine == GameEngine.PygamePyInstaller;
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
            foreach (Control control in new Control[] { pauseButton, cancelButton, openOutputButton, lastResultButton, toggleLogButton, progressBar, statusLabel, statsLabel, logPanel })
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
            SetActionTooltip(dryRunButton, T("Inspect supported archives, show preflight details and estimate the input size without extracting files.", "Проверить архивы, показать preflight-сводку и входной размер без извлечения файлов."));
            SetActionTooltip(toggleLogButton, T("Show or hide technical extraction messages.", "Показать или скрыть технические сообщения."));
            SetActionTooltip(healthCheckButton, T("Check embedded runtime, tools, temp folders and common Windows blocking symptoms.", "Проверить встроенный runtime, инструменты, temp-папки и возможные блокировки Windows."));
            SetActionTooltip(engineOverrideBox, T("Override auto-detection when the game is detected as the wrong engine. Auto is recommended for normal use.", "Выбрать движок вручную, если автоопределение ошиблось. Обычно лучше оставить Auto."));
            SetActionTooltip(skipGalleryIndexCheckBox, T("Skip preparing the gallery index after extraction. Useful for very large outputs or low-memory runs; the gallery can still scan later when opened.", "Не подготавливать индекс галереи после извлечения. Полезно для больших результатов; галерея всё равно сможет просканировать папку при открытии."));
            SetActionTooltip(languageBox, T("Switch interface language.", "Переключить язык интерфейса."));
            SetActionTooltip(extractModeButton, T("Choose the extraction profile shown on the Extract button.", "Выбрать профиль извлечения, который отображается на кнопке запуска."));
            SetActionTooltip(looseModeButton, T("Choose what Collect Loose Media should copy: images/videos, images only, or all already unpacked files.", "Выбрать, что копирует «Открытые медиа»: изображения/видео, только изображения или все уже распакованные файлы."));
            SetActionTooltip(javaExtractModeBox, T(
                "Images only is fastest. SVG previews are cached after the first conversion. All resources keeps non-image JAR and res files.",
                "«Только изображения» работает быстрее всего. PNG-превью SVG кэшируются после первой конвертации. «Все ресурсы» сохраняет и файлы других типов из JAR и res."));
            UpdateActionTooltips();
        }

        private void UpdateActionTooltips()
        {
            if (actionToolTip == null) return;
            bool busy = currentRun != null || externalRunning;
            SetActionTooltip(startButton, busy
                ? T("Wait for the current operation to finish.", "Дождитесь завершения текущей операции.")
                : CanExtractAssets(selectedEngine)
                    ? T("Extract supported assets for the detected engine using the selected profile.", "Извлечь поддерживаемые ресурсы определённого движка с выбранным профилем.")
                    : T("Recover embedded media by signatures and write diagnostics for this unknown format.", "Извлечь встроенные медиа по сигнатурам и записать диагностику неизвестного формата."));
            SetActionTooltip(collectLooseButton, T("Copy already unpacked files using the selected loose mode. This does not unpack archives like data.win, .rpa, .assets or .pak.", "Скопировать уже распакованные файлы с выбранным loose-режимом. Это не распаковывает архивы вроде data.win, .rpa, .assets или .pak."));
            SetActionTooltip(unityDecensorButton, T("Detect Mono BE5, Mono BE6 or IL2CPP, install the latest compatible BepInEx online and install the built-in SW_Decensor.", "Определить Mono BE5, Mono BE6 или IL2CPP, установить актуальный совместимый BepInEx из сети и встроенный SW_Decensor."));
            SetActionTooltip(unlockerButton, selectedEngine == GameEngine.Renpy
                ? T("Install the Ren'Py gallery unlocker. Try Soft mode first.", "Установить анлокер галереи Ren'Py. Сначала попробуйте мягкий режим.")
                : T("The gallery unlocker is available only for detected Ren'Py folders.", "Анлокер галереи доступен только для определённых папок Ren'Py."));
            SetActionTooltip(removeUnlockerButton, removeUnlockerButton.Enabled
                ? T("Remove previously installed Ren'Py gallery unlocker files.", "Удалить ранее установленные файлы анлокера Ren'Py.")
                : T("No installed Ren'Py gallery unlocker was found.", "Установленный анлокер Ren'Py не найден."));
            SetActionTooltip(pauseButton, T("Pause or resume RPG Maker asset conversion.", "Приостановить или продолжить конвертацию RPG Maker."));
            SetActionTooltip(cancelButton, busy ? T("Stop the current operation.", "Остановить текущую операцию.") : T("No operation is currently running.", "Сейчас нет выполняемой операции."));
            SetActionTooltip(lastResultButton, lastOperationResult != null && File.Exists(lastReportPath)
                ? T("Open the last extraction summary, diagnostics and gallery again.", "Повторно открыть последний отчёт, диагностику и галерею.")
                : T("Run an extraction first to create a saved result.", "Сначала выполните извлечение, чтобы появился сохранённый результат."));
            SetActionTooltip(openOutputButton, Directory.Exists(lastOutputDir)
                ? T("Open the most recent extraction output folder.", "Открыть папку последнего результата.")
                : T("Run an extraction first to create an output folder.", "Сначала выполните извлечение, чтобы создать папку результата."));
            SetActionTooltip(keyBox, selectedEngine == GameEngine.RpgMaker
                ? T("The RPG Maker HEX key is filled automatically when possible. You can paste it manually if detection fails.", "HEX-ключ RPG Maker заполняется автоматически, когда это возможно. Если определение не сработало, вставьте ключ вручную.")
                : selectedEngine == GameEngine.Godot
                    ? T("Optional Godot PCK key. Leave it empty to search keys.txt and textual key candidates inside game executables.", "Необязательный ключ Godot PCK. Оставьте поле пустым для поиска в keys.txt и текстовых кандидатах внутри EXE игры.")
                    : T("Optional Unreal AES key. Leave it empty to search keys.txt and textual key candidates inside game executables.", "Необязательный AES-ключ Unreal. Оставьте поле пустым для поиска в keys.txt и текстовых кандидатах внутри EXE игры."));
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

            SetRuntimeStatus(
                T("Runtime: preparing on extraction", "Runtime: подготовится при извлечении"),
                warningColor);
        }

        private void SetRuntimeStatus(string text, Color color)
        {
            if (runtimeStatusLabel == null) return;
            runtimeStatusLabel.Text = text;
            runtimeStatusLabel.ForeColor = color;
        }

    }
}
