using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using RpgmvpConverterWinForms;

namespace GameAssetTool.ApplicationUi
{
    internal class NativeVirtualGalleryDemoForm : Form
    {
        private const int DefaultThumbnailSize = 112;
        private const int PlaceholderImageIndex = 0;
        private const int ImageFallbackIndex = 1;
        private const int SvgFallbackIndex = 2;
        private const int VideoFallbackIndex = 3;
        private const int AudioFallbackIndex = 4;
        private const int FileFallbackIndex = 5;

        private readonly TextBox pathBox;
        private readonly Button browseButton;
        private readonly ComboBox filterCombo;
        private readonly TextBox searchBox;
        private readonly ComboBox sortCombo;
        private readonly ComboBox groupCombo;
        private readonly ComboBox thumbnailSizeCombo;
        private readonly CheckBox upscaleCheckBox;
        private readonly Button spriteSheetButton;
        private readonly Button openModelsButton;
        private readonly Button showInFolderButton;
        private readonly Button openButton;
        private readonly Label statusLabel;
        private readonly Label hintLabel;
        private readonly SplitContainer contentSplit;
        private readonly SmoothListView listView;
        private readonly PictureBox previewBox;
        private readonly Label previewTitleLabel;
        private readonly Label previewMetaLabel;
        private readonly ImageList thumbnails;
        private readonly System.Windows.Forms.Timer filterTimer;
        private readonly System.Windows.Forms.Timer thumbnailInvalidateTimer;
        private readonly object thumbnailLock = new object();
        private readonly Dictionary<string, int> thumbnailIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> queuedThumbnails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim thumbnailSlots = new SemaphoreSlim(4, 4);
        private readonly ToolTip controlToolTip;
        private readonly string sessionManifestDir;
        private readonly bool russian;

        private CancellationTokenSource indexCts;
        private CancellationTokenSource filterCts;
        private CancellationTokenSource previewCts;
        private List<GalleryFile> allFiles = new List<GalleryFile>();
        private List<GalleryFile> visibleFiles = new List<GalleryFile>();
        private Dictionary<string, int> skippedPreviewExtensions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private List<string> modelFiles = new List<string>();
        private int skippedPreviewCount;
        private int thumbnailVersion;
        private int previewVersion;
        private int filterVersion;
        private Image previewImage;
        private MemoryStream previewImageStream;
        private int thumbnailSize = DefaultThumbnailSize;
        private bool allowThumbnailUpscale;
        private bool indexing;

        public NativeVirtualGalleryDemoForm(string initialPath)
            : this(initialPath, false)
        {
        }

        public NativeVirtualGalleryDemoForm(string initialPath, bool russian)
        {
            this.russian = russian;
            Text = russian ? "Галерея результатов" : "Results Gallery";
            MinimumSize = new Size(920, 620);
            Size = new Size(1120, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 18, 24);
            ForeColor = Color.WhiteSmoke;
            AllowDrop = true;
            controlToolTip = new ToolTip();
            controlToolTip.AutoPopDelay = 12000;
            controlToolTip.InitialDelay = 400;
            controlToolTip.ReshowDelay = 120;
            sessionManifestDir = Path.Combine(Path.GetTempPath(), "GameAssetTool", "gallery-index-" + Guid.NewGuid().ToString("N"));

            thumbnails = new ImageList();
            thumbnails.ColorDepth = ColorDepth.Depth32Bit;
            thumbnails.ImageSize = new Size(thumbnailSize, thumbnailSize);
            SeedImageList();

            Label titleLabel = new Label();
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.Text = russian ? "Галерея результатов" : "Results Gallery";
            titleLabel.Location = new Point(16, 14);
            titleLabel.ForeColor = Color.White;

            hintLabel = new Label();
            hintLabel.AutoSize = true;
            hintLabel.Text = russian
                ? "Перетащите папку сюда. Двойной клик открывает файл. Миниатюры загружаются через Windows Shell, где возможно."
                : "Drop extracted folder here. Double-click opens a file. Thumbnails use Windows Shell when possible.";
            hintLabel.Location = new Point(18, 46);
            hintLabel.ForeColor = Color.FromArgb(170, 185, 205);

            Label pathLabel = CreateLabel(russian ? "Папка" : "Folder", 18, 82);
            pathBox = new TextBox();
            pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pathBox.Location = new Point(18, 104);
            pathBox.Size = new Size(760, 24);
            pathBox.BackColor = Color.FromArgb(7, 10, 14);
            pathBox.ForeColor = Color.WhiteSmoke;
            pathBox.BorderStyle = BorderStyle.FixedSingle;

            browseButton = CreateButton(russian ? "Обзор..." : "Browse...", 790, 102, 140, 28);
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Click += delegate { BrowseForFolder(); };

            Button scanButton = CreateButton(russian ? "Сканировать" : "Scan", 942, 102, 140, 28);
            scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            scanButton.Click += delegate { LoadFolder(pathBox.Text); };

            Label filterLabel = CreateLabel(russian ? "Фильтр" : "Filter", 18, 145);
            filterCombo = new ComboBox();
            filterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            filterCombo.Items.AddRange(new object[]
            {
                russian ? "Все" : "All media",
                russian ? "Изображения" : "Images",
                russian ? "Спрайты" : "Sprites",
                "UI",
                russian ? "Фоны/CG" : "Backgrounds/CG",
                russian ? "Крупные" : "Large images",
                russian ? "Очень крупные" : "Huge images",
                russian ? "Широкие CG" : "Wide CG",
                russian ? "Портрет" : "Portrait",
                russian ? "Пейзаж" : "Landscape",
                russian ? "Кадры анимации" : "Animation frames",
                "SVG",
                russian ? "Видео" : "Video"
            });
            filterCombo.SelectedIndex = 0;
            filterCombo.Location = new Point(18, 168);
            filterCombo.Size = new Size(150, 24);
            filterCombo.BackColor = Color.FromArgb(7, 10, 14);
            filterCombo.ForeColor = Color.WhiteSmoke;
            filterCombo.SelectedIndexChanged += delegate { ScheduleFilter(); };
            controlToolTip.SetToolTip(filterCombo, russian
                ? "Фильтр списка по типу ассета: спрайты, UI, фоны/CG, портреты, кадры анимации и медиа-типы. Часть фильтров использует имя файла и размер изображения."
                : "Filter by asset class: sprites, UI, backgrounds/CG, portraits, animation frames and media types. Some filters use file names and image dimensions.");

            Label searchLabel = CreateLabel(russian ? "Поиск" : "Search", 184, 145);
            searchBox = new TextBox();
            searchBox.Location = new Point(184, 168);
            searchBox.Size = new Size(330, 24);
            searchBox.BackColor = Color.FromArgb(7, 10, 14);
            searchBox.ForeColor = Color.WhiteSmoke;
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.TextChanged += delegate { ScheduleFilter(); };
            controlToolTip.SetToolTip(searchBox, russian ? "Поиск по имени и полному пути. Фильтрация запускается с небольшой задержкой, чтобы окно не зависало при вводе." : "Search by file name and full path. Filtering is delayed slightly to keep typing responsive.");

            Label sortLabel = CreateLabel(russian ? "Сортировка" : "Sort", 530, 145);
            sortCombo = new ComboBox();
            sortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            sortCombo.Items.AddRange(new object[]
            {
                russian ? "Имя" : "Name",
                russian ? "Тип" : "Type",
                russian ? "Размер" : "Size",
                russian ? "Дата" : "Modified"
            });
            sortCombo.SelectedIndex = 0;
            sortCombo.Location = new Point(530, 168);
            sortCombo.Size = new Size(120, 24);
            sortCombo.BackColor = Color.FromArgb(7, 10, 14);
            sortCombo.ForeColor = Color.WhiteSmoke;
            sortCombo.SelectedIndexChanged += delegate { ScheduleFilter(); };
            controlToolTip.SetToolTip(sortCombo, russian ? "Сортировка видимых файлов." : "Sort visible files.");

            Label groupLabel = CreateLabel(russian ? "Группа" : "Group", 666, 145);
            groupCombo = new ComboBox();
            groupCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            groupCombo.Items.AddRange(new object[]
            {
                russian ? "Нет" : "None",
                russian ? "Папка" : "Folder",
                russian ? "Тип" : "Type",
                russian ? "Вид" : "Kind",
                russian ? "Размер" : "Size",
                russian ? "Первая папка" : "Top folder"
            });
            groupCombo.SelectedIndex = 0;
            groupCombo.Location = new Point(666, 168);
            groupCombo.Size = new Size(105, 24);
            groupCombo.BackColor = Color.FromArgb(7, 10, 14);
            groupCombo.ForeColor = Color.WhiteSmoke;
            groupCombo.SelectedIndexChanged += delegate { ScheduleFilter(); };
            controlToolTip.SetToolTip(groupCombo, russian ? "Группировка видимых файлов: папка, расширение, тип медиа или размер." : "Group visible files by folder, extension, media kind or size.");

            Label sizeLabel = CreateLabel(russian ? "Миниат." : "Thumb", 787, 145);
            thumbnailSizeCombo = new ComboBox();
            thumbnailSizeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            thumbnailSizeCombo.Items.AddRange(new object[] { "64", "96", "112", "128", "160", "224" });
            thumbnailSizeCombo.SelectedItem = thumbnailSize.ToString();
            thumbnailSizeCombo.Location = new Point(787, 168);
            thumbnailSizeCombo.Size = new Size(78, 24);
            thumbnailSizeCombo.BackColor = Color.FromArgb(7, 10, 14);
            thumbnailSizeCombo.ForeColor = Color.WhiteSmoke;
            thumbnailSizeCombo.SelectedIndexChanged += delegate { ChangeThumbnailSize(); };
            controlToolTip.SetToolTip(thumbnailSizeCombo, russian ? "Размер миниатюр. Больше = удобнее смотреть, но тяжелее для больших папок." : "Thumbnail size. Larger is easier to inspect but heavier for big folders.");

            upscaleCheckBox = new CheckBox();
            upscaleCheckBox.AutoSize = true;
            upscaleCheckBox.Text = russian ? "Увелич." : "Upscale";
            upscaleCheckBox.Location = new Point(878, 170);
            upscaleCheckBox.ForeColor = Color.FromArgb(190, 205, 225);
            upscaleCheckBox.CheckedChanged += delegate
            {
                allowThumbnailUpscale = upscaleCheckBox.Checked;
                ResetThumbnails(false);
            };

            spriteSheetButton = CreateButton(russian ? "Спрайты..." : "Sheet...", 450, 198, 110, 28);
            spriteSheetButton.Enabled = false;
            spriteSheetButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            spriteSheetButton.Click += delegate { OpenSpriteSheetTool(); };
            controlToolTip.SetToolTip(spriteSheetButton, russian ? "Открыть выбранное изображение как sprite sheet и экспортировать отдельный кадр." : "Open the selected image as a sprite sheet and export a single frame.");

            openModelsButton = CreateButton(russian ? "Модели..." : "Models...", 572, 198, 110, 28);
            openModelsButton.Enabled = false;
            openModelsButton.Visible = false;
            openModelsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            openModelsButton.Click += delegate { OpenModelFolder(); };
            controlToolTip.SetToolTip(openModelsButton, russian ? "Открыть папку с экспортированными .obj моделями. Полный 3D-просмотр не загружается, чтобы галерея оставалась быстрой." : "Open the folder with exported .obj models. Full 3D preview is intentionally not loaded to keep the gallery fast.");

            openButton = CreateButton(russian ? "Открыть" : "Open", 694, 198, 110, 28);
            openButton.Enabled = false;
            openButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            openButton.Click += delegate { OpenSelectedFile(); };

            showInFolderButton = CreateButton(russian ? "Показать в папке" : "Show in Folder", 816, 198, 145, 28);
            showInFolderButton.Enabled = false;
            showInFolderButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            showInFolderButton.Click += delegate { ShowSelectedInFolder(); };

            Button refreshButton = CreateButton(russian ? "Обновить" : "Refresh", 974, 198, 108, 28);
            refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshButton.Click += delegate { LoadFolder(pathBox.Text); };

            contentSplit = new SplitContainer();
            contentSplit.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            contentSplit.Location = new Point(18, 234);
            contentSplit.Size = new Size(1064, 447);
            contentSplit.BackColor = Color.FromArgb(15, 18, 24);
            contentSplit.FixedPanel = FixedPanel.None;
            contentSplit.Panel1MinSize = 420;
            contentSplit.Panel2MinSize = 260;
            contentSplit.SplitterWidth = 6;
            contentSplit.SplitterDistance = 730;
            contentSplit.Panel1.BackColor = Color.FromArgb(10, 14, 20);
            contentSplit.Panel2.BackColor = Color.FromArgb(10, 14, 20);

            listView = new SmoothListView();
            listView.Dock = DockStyle.Fill;
            listView.BackColor = Color.FromArgb(10, 14, 20);
            listView.ForeColor = Color.WhiteSmoke;
            listView.BorderStyle = BorderStyle.FixedSingle;
            listView.HideSelection = false;
            listView.LargeImageList = thumbnails;
            listView.View = View.LargeIcon;
            listView.VirtualMode = true;
            listView.VirtualListSize = 0;
            listView.MultiSelect = false;
            listView.ShowItemToolTips = true;
            listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
            listView.CacheVirtualItems += OnCacheVirtualItems;
            listView.SelectedIndexChanged += delegate
            {
                UpdateSelectionButtons();
                UpdatePreviewFromSelection();
            };
            listView.DoubleClick += delegate { OpenSelectedFile(); };
            contentSplit.Panel1.Controls.Add(listView);

            Panel previewPanel = new Panel();
            previewPanel.Dock = DockStyle.Fill;
            previewPanel.Padding = new Padding(12);
            previewPanel.BackColor = Color.FromArgb(10, 14, 20);
            contentSplit.Panel2.Controls.Add(previewPanel);

            previewTitleLabel = new Label();
            previewTitleLabel.Dock = DockStyle.Top;
            previewTitleLabel.Height = 48;
            previewTitleLabel.AutoEllipsis = true;
            previewTitleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            previewTitleLabel.ForeColor = Color.WhiteSmoke;
            previewTitleLabel.Text = russian ? "Просмотр" : "Preview";

            previewMetaLabel = new Label();
            previewMetaLabel.Dock = DockStyle.Bottom;
            previewMetaLabel.Height = 74;
            previewMetaLabel.AutoEllipsis = true;
            previewMetaLabel.ForeColor = Color.FromArgb(170, 185, 205);
            previewMetaLabel.Text = russian
                ? "Выберите изображение, gif, webp или видео для предпросмотра."
                : "Select an image, gif, webp or video to preview it here.";

            previewBox = new PictureBox();
            previewBox.Dock = DockStyle.Fill;
            previewBox.BackColor = Color.FromArgb(7, 10, 14);
            previewBox.BorderStyle = BorderStyle.FixedSingle;
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewBox.DoubleClick += delegate { OpenSelectedFile(); };

            previewPanel.Controls.Add(previewBox);
            previewPanel.Controls.Add(previewMetaLabel);
            previewPanel.Controls.Add(previewTitleLabel);

            statusLabel = new Label();
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            statusLabel.AutoEllipsis = true;
            statusLabel.Location = new Point(18, 692);
            statusLabel.Size = new Size(1064, 24);
            statusLabel.ForeColor = Color.FromArgb(170, 185, 205);
            statusLabel.Text = russian
                ? "Готово. Выберите папку extracted или перетащите её в окно."
                : "Ready. Choose an extracted folder or drag it into this window.";

            filterTimer = new System.Windows.Forms.Timer();
            filterTimer.Interval = 180;
            filterTimer.Tick += delegate
            {
                filterTimer.Stop();
                ApplyFilter();
            };

            thumbnailInvalidateTimer = new System.Windows.Forms.Timer();
            thumbnailInvalidateTimer.Interval = 90;
            thumbnailInvalidateTimer.Tick += delegate
            {
                thumbnailInvalidateTimer.Stop();
                if (!IsDisposed) listView.Invalidate();
            };

            Controls.Add(titleLabel);
            Controls.Add(hintLabel);
            Controls.Add(pathLabel);
            Controls.Add(pathBox);
            Controls.Add(browseButton);
            Controls.Add(scanButton);
            Controls.Add(filterLabel);
            Controls.Add(filterCombo);
            Controls.Add(searchLabel);
            Controls.Add(searchBox);
            Controls.Add(sortLabel);
            Controls.Add(sortCombo);
            Controls.Add(groupLabel);
            Controls.Add(groupCombo);
            Controls.Add(sizeLabel);
            Controls.Add(thumbnailSizeCombo);
            controlToolTip.SetToolTip(upscaleCheckBox, russian ? "Увеличивать маленькие картинки в миниатюрах." : "Upscale small images in thumbnails.");
            Controls.Add(upscaleCheckBox);
            Controls.Add(spriteSheetButton);
            Controls.Add(openModelsButton);
            Controls.Add(openButton);
            Controls.Add(showInFolderButton);
            Controls.Add(refreshButton);
            Controls.Add(contentSplit);
            Controls.Add(statusLabel);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;

            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                Shown += delegate { if (!IsDisposed) LoadFolder(initialPath); };
            }
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Text = text;
            label.Location = new Point(x, y);
            label.ForeColor = Color.FromArgb(190, 205, 225);
            return label;
        }

        private static Button CreateButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(75, 90, 112);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 49, 64);
            button.BackColor = Color.FromArgb(36, 44, 58);
            button.ForeColor = Color.WhiteSmoke;
            return button;
        }

        private void SeedImageList()
        {
            thumbnails.Images.Add(CreateTypeTile("...", Color.FromArgb(80, 90, 104), thumbnailSize));
            thumbnails.Images.Add(CreateTypeTile("IMG", Color.FromArgb(34, 199, 255), thumbnailSize));
            thumbnails.Images.Add(CreateTypeTile("SVG", Color.FromArgb(79, 210, 143), thumbnailSize));
            thumbnails.Images.Add(CreateTypeTile("VID", Color.FromArgb(155, 103, 255), thumbnailSize));
            thumbnails.Images.Add(CreateTypeTile("AUD", Color.FromArgb(255, 112, 168), thumbnailSize));
            thumbnails.Images.Add(CreateTypeTile("FILE", Color.FromArgb(160, 170, 184), thumbnailSize));
        }

        private void BrowseForFolder()
        {
            string selectedFolder;
            if (TryShowModernFolderPicker(pathBox.Text, out selectedFolder))
            {
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    LoadFolder(selectedFolder);
                }

                return;
            }

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = russian ? "Выберите папку extracted для просмотра" : "Choose extracted folder to preview";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(pathBox.Text))
                {
                    dialog.SelectedPath = pathBox.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadFolder(dialog.SelectedPath);
                }
            }
        }

        private bool TryShowModernFolderPicker(string currentPath, out string selectedFolder)
        {
            selectedFolder = null;
            IFileDialog dialog = null;
            IShellItem result = null;
            IShellItem startFolder = null;
            IntPtr displayName = IntPtr.Zero;

            try
            {
                dialog = (IFileDialog)new FileOpenDialog();
                dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST);
                dialog.SetTitle(russian ? "Выберите папку extracted для просмотра" : "Choose extracted folder to preview");
                dialog.SetOkButtonLabel(russian ? "Выбрать папку" : "Select Folder");

                string folder = ResolveFolder(currentPath);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    Guid shellItemGuid = typeof(IShellItem).GUID;
                    SHCreateShellItemFromParsingName(folder, IntPtr.Zero, ref shellItemGuid, out startFolder);
                    dialog.SetFolder(startFolder);
                }

                int hr = dialog.Show(Handle);
                if (hr == unchecked((int)0x800704C7))
                {
                    return true;
                }

                if (hr != 0)
                {
                    return false;
                }

                dialog.GetResult(out result);
                result.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out displayName);
                selectedFolder = Marshal.PtrToStringUni(displayName);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (displayName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(displayName);
                }

                if (startFolder != null)
                {
                    Marshal.ReleaseComObject(startFolder);
                }

                if (result != null)
                {
                    Marshal.ReleaseComObject(result);
                }

                if (dialog != null)
                {
                    Marshal.ReleaseComObject(dialog);
                }
            }
        }

        private void LoadFolder(string inputPath)
        {
            string folder = ResolveFolder(inputPath);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(
                    this,
                    russian ? "Папка не найдена." : "Folder not found.",
                    russian ? "Галерея результатов" : "Results Gallery",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            CancelIndexing();
            pathBox.Text = folder;
            allFiles = new List<GalleryFile>();
            visibleFiles = new List<GalleryFile>();
            skippedPreviewExtensions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            modelFiles = new List<string>();
            skippedPreviewCount = 0;
            openModelsButton.Enabled = false;
            openModelsButton.Visible = false;
            thumbnailVersion++;
            lock (thumbnailLock)
            {
                queuedThumbnails.Clear();
                thumbnailIndices.Clear();
            }

            listView.VirtualListSize = 0;
            ClearPreview(
                russian ? "Просмотр" : "Preview",
                russian ? "Выберите изображение, gif, webp или видео для предпросмотра." : "Select an image, gif, webp or video to preview it here.");
            GalleryScanResult cachedResult;
            if (TryLoadManifest(folder, out cachedResult))
            {
                allFiles = cachedResult.Files;
                skippedPreviewExtensions = cachedResult.SkippedExtensions;
                skippedPreviewCount = cachedResult.SkippedCount;
                modelFiles = cachedResult.ModelFiles;
                openModelsButton.Visible = modelFiles.Count > 0;
                openModelsButton.Enabled = modelFiles.Count > 0;
                indexing = false;
                ApplyFilter();
                return;
            }

            statusLabel.Text = russian ? "Индексация медиафайлов..." : "Indexing media files...";
            indexing = true;

            indexCts = new CancellationTokenSource();
            CancellationToken token = indexCts.Token;
            Task.Factory.StartNew(delegate { return ScanFiles(folder, token); }, token)
                .ContinueWith(delegate(Task<GalleryScanResult> task)
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    BeginInvokeSafe(new Action(delegate
                    {
                        indexing = false;
                        if (task.IsCanceled || token.IsCancellationRequested)
                        {
                            statusLabel.Text = "Indexing canceled.";
                            return;
                        }

                        if (task.IsFaulted)
                        {
                            statusLabel.Text = "Indexing failed: " + task.Exception.GetBaseException().Message;
                            return;
                        }

                        GalleryScanResult result = task.Result ?? new GalleryScanResult();
                        SaveManifest(folder, result);
                        allFiles = result.Files;
                        skippedPreviewExtensions = result.SkippedExtensions;
                        skippedPreviewCount = result.SkippedCount;
                        modelFiles = result.ModelFiles;
                        openModelsButton.Visible = modelFiles.Count > 0;
                        openModelsButton.Enabled = modelFiles.Count > 0;
                        ApplyFilter();
                    }));
                });
        }

        private static string ResolveFolder(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return null;
            }

            string trimmed = inputPath.Trim().Trim('"');
            if (Directory.Exists(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            if (File.Exists(trimmed))
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(trimmed));
                return directory;
            }

            return trimmed;
        }


        private void BeginInvokeSafe(Action action)
        {
            if (action == null || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private bool TryLoadManifest(string folder, out GalleryScanResult result)
        {
            result = null;
            try
            {
                string path = ManifestPath(folder);
                if (!File.Exists(path)) return false;
                GalleryManifest manifest = new JavaScriptSerializer().Deserialize<GalleryManifest>(File.ReadAllText(path, Encoding.UTF8));
                if (manifest == null || !string.Equals(manifest.Root, folder, StringComparison.OrdinalIgnoreCase) || manifest.Files == null)
                    return false;

                GalleryScanResult loaded = new GalleryScanResult();
                foreach (GalleryManifestFile item in manifest.Files)
                {
                    if (item == null || string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path)) continue;
                    GalleryFile file = new GalleryFile();
                    file.Path = item.Path;
                    file.Name = item.Name;
                    file.Extension = item.Extension;
                    file.Kind = item.Kind;
                    file.Size = item.Size;
                    file.Modified = item.Modified;
                    file.Width = item.Width;
                    file.Height = item.Height;
                    loaded.Files.Add(file);
                }
                loaded.SkippedCount = manifest.SkippedCount;
                if (manifest.SkippedExtensions != null)
                    loaded.SkippedExtensions = new Dictionary<string, int>(manifest.SkippedExtensions, StringComparer.OrdinalIgnoreCase);
                if (manifest.ModelFiles != null)
                    loaded.ModelFiles = manifest.ModelFiles.Where(File.Exists).ToList();
                result = loaded;
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        private void SaveManifest(string folder, GalleryScanResult result)
        {
            if (result == null) return;
            try
            {
                Directory.CreateDirectory(sessionManifestDir);
                GalleryManifest manifest = new GalleryManifest();
                manifest.Root = folder;
                manifest.SkippedCount = result.SkippedCount;
                manifest.SkippedExtensions = new Dictionary<string, int>(result.SkippedExtensions, StringComparer.OrdinalIgnoreCase);
                manifest.ModelFiles = new List<string>(result.ModelFiles);
                manifest.Files = result.Files.Select(delegate(GalleryFile file)
                {
                    return new GalleryManifestFile
                    {
                        Path = file.Path,
                        Name = file.Name,
                        Extension = file.Extension,
                        Kind = file.Kind,
                        Size = file.Size,
                        Modified = file.Modified,
                        Width = file.Width,
                        Height = file.Height
                    };
                }).ToList();
                File.WriteAllText(ManifestPath(folder), new JavaScriptSerializer().Serialize(manifest), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private string ManifestPath(string folder)
        {
            string key = folder == null ? "root" : folder.ToLowerInvariant();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                return Path.Combine(sessionManifestDir, BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant() + ".json");
            }
        }

        private static GalleryScanResult ScanFiles(string root, CancellationToken token)
        {
            GalleryScanResult result = new GalleryScanResult();
            Stack<string> pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string directory = pending.Pop();

                string[] files;
                try
                {
                    files = Directory.GetFiles(directory);
                }
                catch
                {
                    files = new string[0];
                }

                for (int i = 0; i < files.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string extension = Path.GetExtension(files[i]);
                    GalleryFile file = CreateGalleryFile(files[i]);
                    if (file != null && IsPreviewable(file))
                    {
                        result.Files.Add(file);
                    }
                    else
                    {
                        result.SkippedCount++;
                        string key = string.IsNullOrWhiteSpace(extension) ? "<no extension>" : extension.ToLowerInvariant();
                        if (!result.SkippedExtensions.ContainsKey(key)) result.SkippedExtensions[key] = 0;
                        result.SkippedExtensions[key]++;
                        if (key.Equals(".obj", StringComparison.OrdinalIgnoreCase))
                            result.ModelFiles.Add(files[i]);
                    }
                }

                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(directory);
                }
                catch
                {
                    directories = new string[0];
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    pending.Push(directories[i]);
                }
            }

            return result;
        }

        private static GalleryFile CreateGalleryFile(string path)
        {
            string extension = Path.GetExtension(path);
            FileKind kind = GetKind(extension);
            if (kind == FileKind.Other)
            {
                return null;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch
            {
                return null;
            }

            GalleryFile item = new GalleryFile();
            item.Path = path;
            item.Name = Path.GetFileName(path);
            item.Extension = string.IsNullOrEmpty(extension) ? "" : extension.ToLowerInvariant();
            item.Kind = kind;
            item.Size = info.Exists ? info.Length : 0L;
            item.Modified = info.Exists ? info.LastWriteTime : DateTime.MinValue;
            if (kind == FileKind.Image && IsClassicBitmap(item.Extension))
            {
                Size dimensions = TryReadImageDimensions(path);
                item.Width = dimensions.Width;
                item.Height = dimensions.Height;
            }
            return item;
        }

        private static bool HasDimensions(GalleryFile file)
        {
            return file != null && file.Width > 0 && file.Height > 0;
        }

        private static bool IsSpriteLike(GalleryFile file)
        {
            if (file == null || file.Kind != FileKind.Image) return false;
            if (IsBackgroundLike(file)) return false;
            string name = (file.Name ?? "").ToLowerInvariant();
            if (ContainsAny(name, "sprite", "spr_", "chara", "character", "actor", "body", "face", "stand", "pose", "walk", "idle", "attack", "motion")) return true;
            if (HasDimensions(file) && file.Width <= 1024 && file.Height <= 1024 && !IsUiLike(file)) return true;
            return IsAnimationFrameLike(file);
        }

        private static bool IsUiLike(GalleryFile file)
        {
            if (file == null || file.Kind != FileKind.Image) return false;
            string name = (file.Name ?? "").ToLowerInvariant();
            if (ContainsAny(name, "ui", "button", "btn", "icon", "cursor", "window", "frame", "panel", "menu", "arrow", "check", "slider", "gauge")) return true;
            return HasDimensions(file) && Math.Max(file.Width, file.Height) <= 512;
        }

        private static bool IsBackgroundLike(GalleryFile file)
        {
            if (file == null || file.Kind != FileKind.Image) return false;
            string name = (file.Name ?? "").ToLowerInvariant();
            if (ContainsAny(name, "background", "backdrop", "bg_", "_bg", "cg", "scene", "room", "map", "location", "stage")) return true;
            return HasDimensions(file) && file.Width >= 900 && file.Height >= 500 && file.Width >= file.Height;
        }

        private static bool IsAnimationFrameLike(GalleryFile file)
        {
            if (file == null || file.Kind != FileKind.Image) return false;
            string name = Path.GetFileNameWithoutExtension(file.Name ?? "").ToLowerInvariant();
            if (ContainsAny(name, "anim", "animation", "frame", "sequence", "seq", "walk", "idle", "run", "attack", "motion")) return true;
            int trailingDigits = CountTrailingDigits(name);
            return trailingDigits >= 2 && HasDimensions(file) && Math.Max(file.Width, file.Height) <= 2048;
        }

        private static string BuildAssetClassText(GalleryFile file)
        {
            string assetClass = GuessAssetClass(file);
            return string.IsNullOrWhiteSpace(assetClass) ? "" : "Class: " + assetClass + " | ";
        }

        private static string GuessAssetClass(GalleryFile file)
        {
            if (file == null) return "";
            if (file.Kind == FileKind.Video) return "Video";
            if (file.Kind == FileKind.Audio) return "Audio";
            if (file.Kind == FileKind.Vector) return "Vector";
            if (IsBackgroundLike(file)) return "Background/CG";
            if (IsUiLike(file)) return "UI";
            if (IsAnimationFrameLike(file)) return "Animation frame";
            if (IsSpriteLike(file)) return "Sprite";
            if (file.Kind == FileKind.Image) return "Image";
            return "";
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < needles.Length; i++)
                if (value.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static int CountTrailingDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int count = 0;
            for (int i = value.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(value[i])) break;
                count++;
            }
            return count;
        }

        private static Size TryReadImageDimensions(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Image image = Image.FromStream(stream, false, false))
                {
                    return new Size(image.Width, image.Height);
                }
            }
            catch
            {
                return Size.Empty;
            }
        }

        private void ScheduleFilter()
        {
            filterTimer.Stop();
            filterTimer.Start();
        }

        private void ChangeThumbnailSize()
        {
            int newSize;
            if (thumbnailSizeCombo.SelectedItem == null ||
                !int.TryParse(thumbnailSizeCombo.SelectedItem.ToString(), out newSize) ||
                newSize <= 0 ||
                newSize == thumbnailSize)
            {
                return;
            }

            thumbnailSize = newSize;
            ResetThumbnails(true);
        }

        private void ResetThumbnails(bool resetImageList)
        {
            thumbnailVersion++;
            lock (thumbnailLock)
            {
                queuedThumbnails.Clear();
                thumbnailIndices.Clear();
            }

            if (resetImageList)
            {
                thumbnails.Images.Clear();
                thumbnails.ImageSize = new Size(thumbnailSize, thumbnailSize);
                SeedImageList();
                listView.LargeImageList = thumbnails;
            }

            ScheduleThumbnailInvalidate();
            QueueInitialThumbnails();
        }

        private void ApplyFilter()
        {
            string query = searchBox.Text.Trim();
            int filter = filterCombo.SelectedIndex;
            int sort = sortCombo.SelectedIndex;
            int group = groupCombo.SelectedIndex;
            string root = ResolveFolder(pathBox.Text);
            List<GalleryFile> source = allFiles.ToList();

            if (filterCts != null)
            {
                filterCts.Cancel();
                filterCts.Dispose();
            }
            filterCts = new CancellationTokenSource();
            CancellationToken token = filterCts.Token;
            int version = Interlocked.Increment(ref filterVersion);

            if (source.Count > 1500)
                statusLabel.Text = russian ? "Фильтрация медиафайлов..." : "Filtering media files...";

            Task.Factory.StartNew(delegate
            {
                return BuildFilteredFiles(source, query, filter, sort, group, root, token);
            }, token).ContinueWith(delegate(Task<List<GalleryFile>> task)
            {
                if (IsDisposed)
                    return;

                BeginInvokeSafe(new Action(delegate
                {
                    if (version != filterVersion || token.IsCancellationRequested)
                        return;

                    if (task.IsCanceled)
                    {
                        statusLabel.Text = BuildStatusText();
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        statusLabel.Text = "Filter failed: " + task.Exception.GetBaseException().Message;
                        return;
                    }

                    visibleFiles = task.Result ?? new List<GalleryFile>();
                    thumbnailVersion++;

                    listView.BeginUpdate();
                    try
                    {
                        listView.VirtualListSize = 0;
                        listView.VirtualListSize = visibleFiles.Count;
                    }
                    finally
                    {
                        listView.EndUpdate();
                    }

                    QueueInitialThumbnails();
                    UpdateSelectionButtons();
                    UpdatePreviewFromSelection();
                    statusLabel.Text = BuildStatusText();
                }));
            });
        }

        private static List<GalleryFile> BuildFilteredFiles(List<GalleryFile> source, string query, int filter, int sort, int group, string root, CancellationToken token)
        {
            IEnumerable<GalleryFile> files = source ?? new List<GalleryFile>();
            token.ThrowIfCancellationRequested();

            if (filter == 1) files = files.Where(f => f.Kind == FileKind.Image);
            else if (filter == 2) files = files.Where(f => IsSpriteLike(f));
            else if (filter == 3) files = files.Where(f => IsUiLike(f));
            else if (filter == 4) files = files.Where(f => IsBackgroundLike(f));
            else if (filter == 5) files = files.Where(f => f.Kind == FileKind.Image && HasDimensions(f) && Math.Max(f.Width, f.Height) >= 720);
            else if (filter == 6) files = files.Where(f => f.Kind == FileKind.Image && HasDimensions(f) && Math.Max(f.Width, f.Height) >= 1920);
            else if (filter == 7) files = files.Where(f => f.Kind == FileKind.Image && HasDimensions(f) && f.Width >= f.Height * 2);
            else if (filter == 8) files = files.Where(f => f.Kind == FileKind.Image && HasDimensions(f) && f.Height > f.Width);
            else if (filter == 9) files = files.Where(f => f.Kind == FileKind.Image && HasDimensions(f) && f.Width > f.Height);
            else if (filter == 10) files = files.Where(f => IsAnimationFrameLike(f));
            else if (filter == 11) files = files.Where(f => f.Kind == FileKind.Vector);
            else if (filter == 12) files = files.Where(f => f.Kind == FileKind.Video);

            if (!string.IsNullOrEmpty(query))
            {
                files = files.Where(f => f.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         f.Path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            token.ThrowIfCancellationRequested();

            if (group != 0)
                files = files.OrderBy(f => GetGroupKey(f, group, root), StringComparer.OrdinalIgnoreCase);

            if (sort == 1)
                files = ThenByGroupAware(files, group, f => f.Extension).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
            else if (sort == 2)
                files = ThenByGroupAwareDescending(files, group, f => f.Size).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
            else if (sort == 3)
                files = ThenByGroupAwareDescending(files, group, f => f.Modified).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
            else
                files = ThenByGroupAware(files, group, f => f.Name);

            token.ThrowIfCancellationRequested();
            return files.ToList();
        }

        private string BuildStatusText()
        {
            string suffix = indexing ? " | indexing..." : "";
            string skipped = skippedPreviewCount > 0 ? " | skipped non-preview: " + skippedPreviewCount.ToString("n0") + BuildSkippedExtensionText() : "";
            string models = modelFiles.Count > 0 ? " | models: " + modelFiles.Count.ToString("n0") : "";
            return string.Format(
                "Native ListView VirtualMode | Visible: {0:n0} / Previewable indexed: {1:n0} | Thumb: {2}px | cached thumbnails load lazily{3}{4}{5}",
                visibleFiles.Count,
                allFiles.Count,
                thumbnailSize,
                skipped,
                models,
                suffix);
        }
        private string BuildSkippedExtensionText()
        {
            if (skippedPreviewExtensions == null || skippedPreviewExtensions.Count == 0) return "";
            string[] top = skippedPreviewExtensions
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .Select(kv => kv.Key + " " + kv.Value.ToString("n0"))
                .ToArray();
            return top.Length == 0 ? "" : " (" + string.Join(", ", top) + ")";
        }

        private static string GetGroupKey(GalleryFile file, int group, string root)
        {
            if (group == 1)
            {
                string directory = Path.GetDirectoryName(file.Path) ?? "";
                if (!string.IsNullOrEmpty(root) && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    directory = directory.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(directory) ? "(root)" : directory;
            }

            if (group == 2)
                return string.IsNullOrEmpty(file.Extension) ? "(no extension)" : file.Extension;

            if (group == 3)
                return file.Kind.ToString();

            if (group == 4)
            {
                if (file.Size >= 100L * 1024L * 1024L) return "100 MB+";
                if (file.Size >= 10L * 1024L * 1024L) return "10-100 MB";
                if (file.Size >= 1024L * 1024L) return "1-10 MB";
                if (file.Size >= 100L * 1024L) return "100 KB-1 MB";
                return "0-100 KB";
            }

            if (group == 5)
            {
                string directory = Path.GetDirectoryName(file.Path) ?? "";
                if (!string.IsNullOrEmpty(root) && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    directory = directory.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(directory)) return "(root)";
                char[] separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                string[] parts = directory.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length == 0 ? "(root)" : parts[0];
            }

            return "";
        }
        private string GetGroupKey(GalleryFile file, int group)
        {
            if (group == 1)
            {
                string root = ResolveFolder(pathBox.Text);
                string directory = Path.GetDirectoryName(file.Path) ?? "";
                if (!string.IsNullOrEmpty(root) && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    directory = directory.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                return string.IsNullOrEmpty(directory) ? "(root)" : directory;
            }

            if (group == 2)
            {
                return string.IsNullOrEmpty(file.Extension) ? "(no extension)" : file.Extension;
            }

            if (group == 3)
            {
                return file.Kind.ToString();
            }

            if (group == 4)
            {
                if (file.Size >= 100L * 1024L * 1024L)
                {
                    return "100 MB+";
                }

                if (file.Size >= 10L * 1024L * 1024L)
                {
                    return "10-100 MB";
                }

                if (file.Size >= 1024L * 1024L)
                {
                    return "1-10 MB";
                }

                if (file.Size >= 100L * 1024L)
                {
                    return "100 KB-1 MB";
                }

                return "0-100 KB";
            }

            if (group == 5)
            {
                string root = ResolveFolder(pathBox.Text);
                string directory = Path.GetDirectoryName(file.Path) ?? "";
                if (!string.IsNullOrEmpty(root) && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    directory = directory.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(directory)) return "(root)";
                char[] separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                string[] parts = directory.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length == 0 ? "(root)" : parts[0];
            }

            return "";
        }

        private static IOrderedEnumerable<GalleryFile> ThenByGroupAware(
            IEnumerable<GalleryFile> files,
            int group,
            Func<GalleryFile, string> selector)
        {
            IOrderedEnumerable<GalleryFile> ordered = files as IOrderedEnumerable<GalleryFile>;
            if (group != 0 && ordered != null)
            {
                return ordered.ThenBy(selector, StringComparer.OrdinalIgnoreCase);
            }

            return files.OrderBy(selector, StringComparer.OrdinalIgnoreCase);
        }

        private static IOrderedEnumerable<GalleryFile> ThenByGroupAwareDescending<TKey>(
            IEnumerable<GalleryFile> files,
            int group,
            Func<GalleryFile, TKey> selector)
        {
            IOrderedEnumerable<GalleryFile> ordered = files as IOrderedEnumerable<GalleryFile>;
            if (group != 0 && ordered != null)
            {
                return ordered.ThenByDescending(selector);
            }

            return files.OrderByDescending(selector);
        }


        private void ScheduleThumbnailInvalidate()
        {
            if (IsDisposed || thumbnailInvalidateTimer == null)
            {
                return;
            }

            thumbnailInvalidateTimer.Stop();
            thumbnailInvalidateTimer.Start();
        }
        private void QueueInitialThumbnails()
        {
            int count = Math.Min(visibleFiles.Count, 120);
            int version = thumbnailVersion;
            for (int i = 0; i < count; i++)
            {
                QueueThumbnail(visibleFiles[i], version);
            }
        }

        private void OnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= visibleFiles.Count)
            {
                e.Item = new ListViewItem("");
                return;
            }

            GalleryFile file = visibleFiles[e.ItemIndex];
            int imageIndex;
            lock (thumbnailLock)
            {
                if (!thumbnailIndices.TryGetValue(file.Path, out imageIndex))
                {
                    imageIndex = GetFallbackImageIndex(file.Kind);
                }
            }

            QueueThumbnail(file, thumbnailVersion);

            ListViewItem item = new ListViewItem(file.Name, imageIndex);
            item.ToolTipText = file.Path + Environment.NewLine +
                               BuildDimensionText(file) +
                               FormatBytes(file.Size) + " | " +
                               file.Modified.ToString("yyyy-MM-dd HH:mm:ss");
            item.Tag = file.Path;
            e.Item = item;
        }

        private void OnCacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            int version = thumbnailVersion;
            int start = Math.Max(0, e.StartIndex);
            int end = Math.Min(visibleFiles.Count - 1, e.EndIndex + 80);
            for (int i = start; i <= end; i++)
            {
                QueueThumbnail(visibleFiles[i], version);
            }
        }

        private void QueueThumbnail(GalleryFile file, int version)
        {
            if (file == null)
            {
                return;
            }

            lock (thumbnailLock)
            {
                if (thumbnailIndices.ContainsKey(file.Path) || queuedThumbnails.Contains(file.Path))
                {
                    return;
                }

                queuedThumbnails.Add(file.Path);
            }

            Task.Factory.StartNew(delegate
            {
                thumbnailSlots.Wait();
                Bitmap bitmap = null;
                try
                {
                    bitmap = CreateCachedThumbnail(file, thumbnailSize, allowThumbnailUpscale);
                }
                catch
                {
                    bitmap = null;
                }
                finally
                {
                    thumbnailSlots.Release();
                }

                if (bitmap == null)
                {
                    return;
                }

                if (IsDisposed)
                {
                    bitmap.Dispose();
                    return;
                }

                BeginInvokeSafe(new Action(delegate
                {
                    if (IsDisposed)
                    {
                        bitmap.Dispose();
                        return;
                    }

                    int imageIndex;
                    lock (thumbnailLock)
                    {
                        if (thumbnailIndices.TryGetValue(file.Path, out imageIndex))
                        {
                            bitmap.Dispose();
                            return;
                        }

                        imageIndex = thumbnails.Images.Count;
                        thumbnails.Images.Add(bitmap);
                        thumbnailIndices[file.Path] = imageIndex;
                    }

                    if (version == thumbnailVersion)
                    {
                        ScheduleThumbnailInvalidate();
                    }
                }));
            });
        }

        private static Bitmap CreateCachedThumbnail(GalleryFile file, int size, bool allowUpscale)
        {
            string cachePath = GetThumbnailCachePath(file, size, allowUpscale);
            if (!string.IsNullOrWhiteSpace(cachePath))
            {
                Bitmap cached = TryLoadCachedThumbnail(cachePath);
                if (cached != null) return cached;
            }

            Bitmap bitmap = CreateThumbnail(file, size, allowUpscale);
            if (bitmap != null && ShouldCacheThumbnail(file) && !string.IsNullOrWhiteSpace(cachePath))
            {
                TrySaveCachedThumbnail(cachePath, bitmap);
            }

            return bitmap;
        }

        private static bool ShouldCacheThumbnail(GalleryFile file)
        {
            return file != null && IsPreviewable(file);
        }

        private static bool IsPreviewable(GalleryFile file)
        {
            return file != null && (file.Kind == FileKind.Image || file.Kind == FileKind.Vector || file.Kind == FileKind.Video);
        }

        private static string GetThumbnailCachePath(GalleryFile file, int size, bool allowUpscale)
        {
            if (!ShouldCacheThumbnail(file)) return null;
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
                string key = file.Path + "|" + file.Size + "|" + file.Modified.ToUniversalTime().Ticks + "|" + size + "|" + allowUpscale;
                string hash;
                using (SHA256 sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "");
                string directory = Path.Combine(root, "GameAssetTool", "thumb-cache", hash.Substring(0, 2));
                return Path.Combine(directory, hash + ".png");
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap TryLoadCachedThumbnail(string cachePath)
        {
            try
            {
                if (!File.Exists(cachePath)) return null;
                byte[] bytes = File.ReadAllBytes(cachePath);
                using (MemoryStream stream = new MemoryStream(bytes))
                using (Image image = Image.FromStream(stream, false, false))
                    return new Bitmap(image);
            }
            catch
            {
                return null;
            }
        }

        private static void TrySaveCachedThumbnail(string cachePath, Bitmap bitmap)
        {
            try
            {
                string directory = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                bitmap.Save(cachePath, ImageFormat.Png);
            }
            catch
            {
            }
        }
        private static Bitmap CreateThumbnail(GalleryFile file, int size, bool allowUpscale)
        {
            if (file.Kind == FileKind.Image && IsClassicBitmap(file.Extension))
            {
                Bitmap decoded = TryDecodeImage(file.Path, size, allowUpscale);
                if (decoded != null)
                {
                    return decoded;
                }
            }

            Bitmap shell = TryCreateShellThumbnail(file.Path, size, allowUpscale);
            if (shell != null)
            {
                return shell;
            }

            if (file.Kind == FileKind.Vector)
            {
                return CreateTypeTile("SVG", Color.FromArgb(79, 210, 143));
            }

            if (file.Kind == FileKind.Video)
            {
                return CreateTypeTile("VID", Color.FromArgb(155, 103, 255));
            }

            if (file.Kind == FileKind.Audio)
            {
                return CreateTypeTile("AUD", Color.FromArgb(255, 112, 168));
            }

            return CreateTypeTile("IMG", Color.FromArgb(34, 199, 255));
        }

        private static bool IsClassicBitmap(string extension)
        {
            return MediaTypeRegistry.IsClassicBitmap(extension);
        }

        private static Bitmap TryDecodeImage(string path, int size, bool allowUpscale)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Image image = Image.FromStream(stream, false, false))
                {
                    return FitImage(image, size, allowUpscale);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap FitImage(Image image, int size, bool allowUpscale)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(10, 14, 20));
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float scale = Math.Min((float)size / image.Width, (float)size / image.Height);
                if (!allowUpscale)
                {
                    scale = Math.Min(1F, scale);
                }
                int width = Math.Max(1, (int)(image.Width * scale));
                int height = Math.Max(1, (int)(image.Height * scale));
                int x = (size - width) / 2;
                int y = (size - height) / 2;
                graphics.DrawImage(image, new Rectangle(x, y, width, height));
            }

            return bitmap;
        }

        private static Bitmap TryCreateShellThumbnail(string path, int size)
        {
            return TryCreateShellThumbnail(path, size, false);
        }

        private static Bitmap TryCreateShellThumbnail(string path, int size, bool allowUpscale)
        {
            IShellItemImageFactory factory = null;
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                int hr = factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_BIGGERSIZEOK, out hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero)
                {
                    return null;
                }

                using (Bitmap shellBitmap = Image.FromHbitmap(hBitmap))
                {
                    return FitImage(shellBitmap, size, allowUpscale);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }

                if (factory != null)
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }

        private static Bitmap CreateTypeTile(string label, Color accent)
        {
            return CreateTypeTile(label, accent, DefaultThumbnailSize);
        }

        private static Bitmap CreateTypeTile(string label, Color accent, int tileSize)
        {
            Bitmap bitmap = new Bitmap(tileSize, tileSize);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.FromArgb(10, 14, 20));

                using (SolidBrush cardBrush = new SolidBrush(Color.FromArgb(22, 28, 38)))
                using (Pen borderPen = new Pen(Color.FromArgb(70, accent), 2F))
                {
                    int padding = Math.Max(6, tileSize / 18);
                    Rectangle rect = new Rectangle(padding, padding, tileSize - padding * 2, tileSize - padding * 2);
                    graphics.FillRectangle(cardBrush, rect);
                    graphics.DrawRectangle(borderPen, rect);
                }

                using (SolidBrush accentBrush = new SolidBrush(accent))
                using (Font font = new Font("Segoe UI", GetTileFontSize(label, tileSize), FontStyle.Bold, GraphicsUnit.Point))
                {
                    SizeF size = graphics.MeasureString(label, font);
                    float x = (tileSize - size.Width) / 2F;
                    float y = (tileSize - size.Height) / 2F;
                    graphics.DrawString(label, font, accentBrush, x, y);
                }
            }

            return bitmap;
        }

        private static float GetTileFontSize(string label, int tileSize)
        {
            float baseSize = tileSize / 6F;
            if (label.Length > 4)
            {
                baseSize = tileSize / 8F;
            }

            return Math.Max(12F, Math.Min(42F, baseSize));
        }

        private int GetFallbackImageIndex(FileKind kind)
        {
            if (kind == FileKind.Image)
            {
                return ImageFallbackIndex;
            }

            if (kind == FileKind.Vector)
            {
                return SvgFallbackIndex;
            }

            if (kind == FileKind.Video)
            {
                return VideoFallbackIndex;
            }

            if (kind == FileKind.Audio)
            {
                return AudioFallbackIndex;
            }

            return FileFallbackIndex;
        }

        private static FileKind GetKind(string extension)
        {
            switch (MediaTypeRegistry.GetMediaKind(extension))
            {
                case MediaAssetKind.Image:
                    return FileKind.Image;
                case MediaAssetKind.Vector:
                    return FileKind.Vector;
                case MediaAssetKind.Video:
                    return FileKind.Video;
                case MediaAssetKind.Audio:
                    return FileKind.Audio;
                default:
                    return FileKind.Other;
            }
        }

        private void UpdatePreviewFromSelection()
        {
            int selectedIndex = listView.SelectedIndices.Count > 0 ? listView.SelectedIndices[0] : -1;
            if (selectedIndex < 0 || selectedIndex >= visibleFiles.Count)
            {
                ClearPreview("Preview", "Select an image, gif, webp or video to preview it here.");
                return;
            }

            GalleryFile file = visibleFiles[selectedIndex];
            ClearPreview(file.Name, "Loading preview...");
            int version = previewVersion;
            previewMetaLabel.Text = BuildPreviewMeta(file, null);
            CancelPreviewTask();
            previewCts = new CancellationTokenSource();
            CancellationToken token = previewCts.Token;
            int previewSize = Math.Max(320, Math.Min(900, Math.Max(previewBox.Width, previewBox.Height) * 2));

            Task.Factory.StartNew(delegate
            {
                token.ThrowIfCancellationRequested();
                return CreateLargePreview(file, previewSize, token);
            }, token).ContinueWith(delegate(Task<PreviewImage> task)
            {
                if (IsDisposed)
                {
                    DisposePreviewResult(task);
                    return;
                }

                BeginInvokeSafe(new Action(delegate
                {
                    if (version != previewVersion || token.IsCancellationRequested || task.IsFaulted || task.IsCanceled)
                    {
                        DisposePreviewResult(task);
                        return;
                    }

                    PreviewImage result = task.Result;
                    if (result == null || result.Image == null)
                    {
                        ClearPreview(file.Name, "No preview available." + Environment.NewLine + file.Path);
                        return;
                    }

                    ClearPreviewImageOnly();
                    previewImage = result.Image;
                    previewImageStream = result.Stream;
                    previewBox.SizeMode = ShouldCenterPreview(previewImage)
                        ? PictureBoxSizeMode.CenterImage
                        : PictureBoxSizeMode.Zoom;
                    previewBox.Image = previewImage;
                    previewTitleLabel.Text = file.Name;
                    previewMetaLabel.Text = BuildPreviewMeta(file, result);
                }));
            });
        }

        private static string BuildPreviewMeta(GalleryFile file, PreviewImage preview)
        {
            string resolution = "";
            if (HasDimensions(file))
            {
                resolution = " | " + file.Width + " x " + file.Height;
            }
            else if (preview != null && preview.Width > 0 && preview.Height > 0)
            {
                resolution = " | " + preview.Width + " x " + preview.Height;
            }

            return FormatBytes(file.Size) +
                   resolution +
                   " | " +
                   file.Modified.ToString("yyyy-MM-dd HH:mm:ss") +
                   Environment.NewLine +
                   file.Path;
        }

        private static string BuildDimensionText(GalleryFile file)
        {
            if (!HasDimensions(file))
            {
                return "";
            }

            return file.Width + " x " + file.Height + " | ";
        }

        private void ClearPreview(string title, string meta)
        {
            ++previewVersion;
            CancelPreviewTask();
            ClearPreviewImageOnly();
            previewTitleLabel.Text = title;
            previewMetaLabel.Text = meta;
        }

        private void CancelPreviewTask()
        {
            if (previewCts != null)
            {
                previewCts.Cancel();
                previewCts.Dispose();
                previewCts = null;
            }
        }

        private void ClearPreviewImageOnly()
        {
            previewBox.Image = null;
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;

            if (previewImage != null)
            {
                previewImage.Dispose();
                previewImage = null;
            }

            if (previewImageStream != null)
            {
                previewImageStream.Dispose();
                previewImageStream = null;
            }
        }

        private static void DisposePreviewResult(Task<PreviewImage> task)
        {
            if (task == null || task.Status != TaskStatus.RanToCompletion || task.Result == null)
            {
                return;
            }

            if (task.Result.Image != null)
            {
                task.Result.Image.Dispose();
            }

            if (task.Result.Stream != null)
            {
                task.Result.Stream.Dispose();
            }
        }

        private static PreviewImage CreateLargePreview(GalleryFile file, int size, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (file.Kind == FileKind.Image && IsClassicBitmap(file.Extension))
            {
                PreviewImage loaded = TryLoadImagePreview(file.Path, token);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            token.ThrowIfCancellationRequested();
            Bitmap shell = TryCreateShellThumbnail(file.Path, size);
            if (shell != null)
            {
                return new PreviewImage(shell, null);
            }

            if (file.Kind == FileKind.Vector)
            {
                return new PreviewImage(CreateTypeTile("SVG", Color.FromArgb(79, 210, 143), size), null);
            }

            if (file.Kind == FileKind.Video)
            {
                return new PreviewImage(CreateTypeTile("VIDEO", Color.FromArgb(155, 103, 255), size), null);
            }

            if (file.Kind == FileKind.Audio)
            {
                return new PreviewImage(CreateTypeTile("AUDIO", Color.FromArgb(255, 112, 168), size), null);
            }

            return new PreviewImage(CreateTypeTile("FILE", Color.FromArgb(160, 170, 184), size), null);
        }

        private static PreviewImage TryLoadImagePreview(string path, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                byte[] bytes = File.ReadAllBytes(path);
                token.ThrowIfCancellationRequested();
                MemoryStream stream = new MemoryStream(bytes);
                Image image = Image.FromStream(stream, false, false);
                return new PreviewImage(image, stream);
            }
            catch
            {
                return null;
            }
        }

        private bool ShouldCenterPreview(Image image)
        {
            if (image == null || previewBox.Width <= 0 || previewBox.Height <= 0)
            {
                return false;
            }

            return image.Width <= previewBox.Width && image.Height <= previewBox.Height;
        }

        private void UpdateSelectionButtons()
        {
            bool hasSelection = listView.SelectedIndices.Count > 0;
            openButton.Enabled = hasSelection;
            showInFolderButton.Enabled = hasSelection;
            GalleryFile selected = GetSelectedFile();
            spriteSheetButton.Enabled = selected != null && selected.Kind == FileKind.Image && IsClassicBitmap(selected.Extension);
        }

        private GalleryFile GetSelectedFile()
        {
            int index = -1;
            if (listView.SelectedIndices.Count > 0)
            {
                index = listView.SelectedIndices[0];
            }
            else if (listView.FocusedItem != null)
            {
                index = listView.FocusedItem.Index;
            }

            if (index < 0 || index >= visibleFiles.Count)
            {
                return null;
            }

            return visibleFiles[index];
        }

        private string GetSelectedPath()
        {
            GalleryFile file = GetSelectedFile();
            return file == null ? null : file.Path;
        }

        private void OpenSpriteSheetTool()
        {
            GalleryFile file = GetSelectedFile();
            if (file == null || file.Kind != FileKind.Image || !IsClassicBitmap(file.Extension) || !File.Exists(file.Path))
            {
                return;
            }

            using (SpriteSheetPreviewForm dialog = new SpriteSheetPreviewForm(file.Path, russian))
            {
                dialog.ShowDialog(this);
            }
        }

        private void OpenSelectedFile()
        {
            string path = GetSelectedPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open file failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenModelFolder()
        {
            string path = modelFiles != null && modelFiles.Count > 0 ? modelFiles[0] : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + path.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Explorer failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowSelectedInFolder()
        {
            string path = GetSelectedPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + path.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Explorer failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                LoadFolder(files[0]);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            CancelPreviewTask();
            CancelFiltering();
            CancelIndexing();
            ClearPreviewImageOnly();
            TryDeleteDirectory(sessionManifestDir);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private void CancelFiltering()
        {
            if (filterCts != null)
            {
                filterCts.Cancel();
                filterCts.Dispose();
                filterCts = null;
            }
        }

        private void CancelIndexing()
        {
            if (indexCts != null)
            {
                indexCts.Cancel();
                indexCts.Dispose();
                indexCts = null;
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024D && unit < units.Length - 1)
            {
                value /= 1024D;
                unit++;
            }

            return string.Format("{0:0.##} {1}", value, units[unit]);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false, EntryPoint = "SHCreateItemFromParsingName")]
        private static extern void SHCreateShellItemFromParsingName(
            string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        private interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);

            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(FOS fos);
            void GetOptions(out FOS pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName(out IntPtr pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;

            public SIZE(int width, int height)
            {
                cx = width;
                cy = height;
            }
        }

        [Flags]
        private enum SIIGBF
        {
            SIIGBF_RESIZETOFIT = 0x00000000,
            SIIGBF_BIGGERSIZEOK = 0x00000001
        }

        [Flags]
        private enum FOS
        {
            FOS_PICKFOLDERS = 0x00000020,
            FOS_FORCEFILESYSTEM = 0x00000040,
            FOS_PATHMUSTEXIST = 0x00000800
        }

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000
        }

        private enum FileKind
        {
            Other,
            Image,
            Vector,
            Video,
            Audio
        }

        private sealed class GalleryScanResult
        {
            public List<GalleryFile> Files = new List<GalleryFile>(1024);
            public Dictionary<string, int> SkippedExtensions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public List<string> ModelFiles = new List<string>();
            public int SkippedCount;
        }

        private sealed class GalleryManifest
        {
            public string Root { get; set; }
            public List<GalleryManifestFile> Files { get; set; }
            public Dictionary<string, int> SkippedExtensions { get; set; }
            public List<string> ModelFiles { get; set; }
            public int SkippedCount { get; set; }
        }

        private sealed class GalleryManifestFile
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public string Extension { get; set; }
            public FileKind Kind { get; set; }
            public long Size { get; set; }
            public DateTime Modified { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class GalleryFile
        {
            public string Path;
            public string Name;
            public string Extension;
            public FileKind Kind;
            public long Size;
            public DateTime Modified;
            public int Width;
            public int Height;
        }

        private sealed class PreviewImage
        {
            public readonly Image Image;
            public readonly MemoryStream Stream;
            public readonly int Width;
            public readonly int Height;

            public PreviewImage(Image image, MemoryStream stream)
                : this(image, stream, image == null ? 0 : image.Width, image == null ? 0 : image.Height)
            {
            }

            public PreviewImage(Image image, MemoryStream stream, int width, int height)
            {
                Image = image;
                Stream = stream;
                Width = width;
                Height = height;
            }
        }

        private sealed class SmoothListView : ListView
        {
            public SmoothListView()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
                DoubleBuffered = true;
            }
        }
    }
}
