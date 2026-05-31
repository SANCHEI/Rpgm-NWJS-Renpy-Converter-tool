using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal sealed class ResultsGalleryForm : Form
    {
        private const int BatchSize = 60;
        private static readonly HashSet<string> imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff"
        };
        private static readonly HashSet<string> audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".ogg", ".wav", ".flac", ".m4a", ".aac", ".mid", ".midi"
        };
        private static readonly HashSet<string> videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".avi", ".wmv", ".mov", ".mkv", ".flv"
        };

        private readonly string outputDir;
        private readonly bool russian;
        private readonly ComboBox filterBox;
        private readonly TextBox searchBox;
        private readonly FlowLayoutPanel grid;
        private readonly Label statusLabel;
        private readonly PictureBox largePreview;
        private readonly Label previewNameLabel;
        private readonly Label previewInfoLabel;
        private readonly System.Windows.Forms.Timer filterTimer;
        private List<string> indexedFiles = new List<string>();
        private List<string> matchingFiles = new List<string>();
        private string selectedPath;
        private int loadedCount;
        private int refreshVersion;
        private int previewVersion;
        private bool indexLoaded;

        public ResultsGalleryForm(string outputDir, bool russian)
        {
            this.outputDir = outputDir;
            this.russian = russian;

            Text = russian ? "Галерея результатов" : "Results Gallery";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1120, 720);
            MinimumSize = new Size(820, 540);
            BackColor = Color.FromArgb(17, 19, 24);
            ForeColor = Color.FromArgb(239, 243, 248);
            Font = new Font("Segoe UI", 9f);
            ApplicationIcon.Apply(this);

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.FromArgb(25, 29, 36) };
            Controls.Add(toolbar);
            toolbar.Controls.Add(new Label
            {
                Text = russian ? "Фильтр" : "Filter",
                Location = new Point(14, 12),
                Size = new Size(70, 20),
                ForeColor = Color.FromArgb(155, 167, 181)
            });
            filterBox = new ComboBox
            {
                Location = new Point(14, 32),
                Size = new Size(150, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(13, 16, 21),
                ForeColor = Color.FromArgb(239, 243, 248),
                FlatStyle = FlatStyle.Flat
            };
            filterBox.Items.AddRange(new object[]
            {
                russian ? "Все" : "All",
                russian ? "Изображения" : "Images",
                "SVG",
                russian ? "Аудио" : "Audio",
                russian ? "Видео" : "Video"
            });
            filterBox.SelectedIndex = 0;
            toolbar.Controls.Add(filterBox);

            toolbar.Controls.Add(new Label
            {
                Text = russian ? "Поиск" : "Search",
                Location = new Point(180, 12),
                Size = new Size(70, 20),
                ForeColor = Color.FromArgb(155, 167, 181)
            });
            searchBox = new TextBox
            {
                Location = new Point(180, 32),
                Size = new Size(330, 26),
                BackColor = Color.FromArgb(13, 16, 21),
                ForeColor = Color.FromArgb(239, 243, 248),
                BorderStyle = BorderStyle.FixedSingle
            };
            toolbar.Controls.Add(searchBox);

            Button revealButton = CreateButton(russian ? "Показать в папке" : "Show in Folder", new Point(530, 28), 145);
            revealButton.Click += delegate { RevealSelected(); };
            toolbar.Controls.Add(revealButton);

            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(12, 5, 0, 0),
                ForeColor = Color.FromArgb(155, 167, 181),
                BackColor = Color.FromArgb(25, 29, 36)
            };
            Controls.Add(statusLabel);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 730,
                FixedPanel = FixedPanel.Panel2,
                BackColor = Color.FromArgb(50, 58, 70)
            };
            Controls.Add(split);
            split.BringToFront();

            grid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(17, 19, 24),
                Padding = new Padding(10),
                WrapContents = true
            };
            split.Panel1.Controls.Add(grid);

            Panel previewPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Color.FromArgb(25, 29, 36) };
            split.Panel2.Controls.Add(previewPanel);
            previewNameLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(239, 243, 248),
                Font = new Font("Segoe UI Semibold", 10f),
                Text = russian ? "Выберите файл" : "Select a file"
            };
            previewPanel.Controls.Add(previewNameLabel);
            previewInfoLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                ForeColor = Color.FromArgb(155, 167, 181)
            };
            previewPanel.Controls.Add(previewInfoLabel);
            largePreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(10, 12, 16)
            };
            previewPanel.Controls.Add(largePreview);
            largePreview.BringToFront();

            filterTimer = new System.Windows.Forms.Timer { Interval = 250 };
            filterTimer.Tick += delegate
            {
                filterTimer.Stop();
                StartFilter();
            };
            filterBox.SelectedIndexChanged += delegate { QueueFilter(1); };
            searchBox.TextChanged += delegate { QueueFilter(250); };
            grid.Scroll += delegate
            {
                if (grid.VerticalScroll.Value + grid.ClientSize.Height >= grid.VerticalScroll.Maximum - 220)
                    AppendNextBatch();
            };
            Shown += delegate { BeginIndexing(); };
        }

        internal static List<string> GetMatchingFiles(string outputDir, int filterIndex, string search)
        {
            return FilterFiles(IndexFiles(outputDir), filterIndex, search);
        }

        private static List<string> IndexFiles(string outputDir)
        {
            if (!Directory.Exists(outputDir)) return new List<string>();
            try
            {
                return Directory.EnumerateFiles(outputDir, "*.*", SearchOption.AllDirectories)
                    .Where(IsGalleryFile)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> FilterFiles(IEnumerable<string> files, int filterIndex, string search)
        {
            string query = (search ?? "").Trim();
            return files.Where(delegate(string path)
            {
                string extension = Path.GetExtension(path);
                if (filterIndex == 1 && !imageExtensions.Contains(extension)) return false;
                if (filterIndex == 2 && !extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)) return false;
                if (filterIndex == 3 && !audioExtensions.Contains(extension)) return false;
                if (filterIndex == 4 && !videoExtensions.Contains(extension)) return false;
                return query.Length == 0 || path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();
        }

        private static bool IsGalleryFile(string path)
        {
            string extension = Path.GetExtension(path);
            return imageExtensions.Contains(extension)
                || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
                || audioExtensions.Contains(extension)
                || videoExtensions.Contains(extension);
        }

        private void BeginIndexing()
        {
            statusLabel.Text = russian ? "Индексирование файлов..." : "Indexing files...";
            Task.Run(delegate { return IndexFiles(outputDir); }).ContinueWith(delegate(Task<List<string>> task)
            {
                if (task.IsFaulted || IsDisposed) return;
                SafeBeginInvoke(delegate
                {
                    indexedFiles = task.Result;
                    indexLoaded = true;
                    StartFilter();
                });
            });
        }

        private void QueueFilter(int delay)
        {
            if (!indexLoaded) return;
            filterTimer.Stop();
            filterTimer.Interval = Math.Max(delay, 1);
            filterTimer.Start();
        }

        private void StartFilter()
        {
            if (!indexLoaded) return;
            filterTimer.Stop();
            int version = Interlocked.Increment(ref refreshVersion);
            int filter = filterBox.SelectedIndex;
            string search = searchBox.Text;
            List<string> files = indexedFiles;
            statusLabel.Text = russian ? "Фильтрация..." : "Filtering...";
            Task.Run(delegate { return FilterFiles(files, filter, search); }).ContinueWith(delegate(Task<List<string>> task)
            {
                if (task.IsFaulted || IsDisposed) return;
                SafeBeginInvoke(delegate
                {
                    if (version != refreshVersion) return;
                    ApplyFilter(task.Result);
                });
            });
        }

        private void ApplyFilter(List<string> files)
        {
            selectedPath = null;
            matchingFiles = files;
            loadedCount = 0;
            SetLargePreview(null);
            ClearTiles();
            AppendNextBatch();
        }

        private void AppendNextBatch()
        {
            if (loadedCount >= matchingFiles.Count)
            {
                UpdateStatus();
                return;
            }
            grid.SuspendLayout();
            int end = Math.Min(loadedCount + BatchSize, matchingFiles.Count);
            while (loadedCount < end)
                grid.Controls.Add(CreateTile(matchingFiles[loadedCount++]));
            grid.ResumeLayout();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            statusLabel.Text = loadedCount < matchingFiles.Count
                ? string.Format(russian ? "Загружено {0} из {1}. Прокрутите ниже для продолжения." : "Loaded {0} of {1}. Scroll down to load more.", loadedCount, matchingFiles.Count)
                : string.Format(russian ? "Файлов: {0}" : "Files: {0}", matchingFiles.Count);
        }

        private Control CreateTile(string path)
        {
            Panel tile = new Panel
            {
                Size = new Size(164, 154),
                Margin = new Padding(7),
                Padding = new Padding(6),
                BackColor = Color.FromArgb(25, 29, 36),
                Cursor = Cursors.Hand,
                Tag = path
            };
            PictureBox preview = new PictureBox
            {
                Location = new Point(6, 6),
                Size = new Size(152, 108),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(10, 12, 16),
                Image = CreateTypeThumbnail(Path.GetExtension(path), new Size(152, 108))
            };
            Label name = new Label
            {
                Text = Path.GetFileName(path),
                Location = new Point(6, 120),
                Size = new Size(152, 28),
                ForeColor = Color.FromArgb(239, 243, 248),
                AutoEllipsis = true
            };
            tile.Controls.Add(preview);
            tile.Controls.Add(name);
            EventHandler select = delegate { SelectTile(tile); };
            EventHandler open = delegate { SelectTile(tile); OpenSelected(); };
            tile.Click += select;
            preview.Click += select;
            name.Click += select;
            tile.DoubleClick += open;
            preview.DoubleClick += open;
            name.DoubleClick += open;
            LoadThumbnailAsync(preview, path);
            return tile;
        }

        private void LoadThumbnailAsync(PictureBox preview, string path)
        {
            Task.Run(delegate { return LoadScaledPreview(path, new Size(152, 108)); }).ContinueWith(delegate(Task<Image> task)
            {
                if (task.IsFaulted || task.Result == null || preview.IsDisposed)
                {
                    if (!task.IsFaulted && task.Result != null) task.Result.Dispose();
                    return;
                }
                SafeBeginInvoke(delegate
                {
                    if (preview.IsDisposed)
                    {
                        task.Result.Dispose();
                        return;
                    }
                    Image previous = preview.Image;
                    preview.Image = task.Result;
                    if (previous != null) previous.Dispose();
                });
            });
        }

        private void SelectTile(Panel tile)
        {
            foreach (Panel panel in grid.Controls.OfType<Panel>())
                panel.BackColor = Color.FromArgb(25, 29, 36);
            tile.BackColor = Color.FromArgb(48, 73, 90);
            selectedPath = tile.Tag as string;
            SetLargePreview(selectedPath);
        }

        private void SetLargePreview(string path)
        {
            int version = Interlocked.Increment(ref previewVersion);
            ReplaceLargePreview(null);
            previewNameLabel.Text = string.IsNullOrWhiteSpace(path)
                ? (russian ? "Выберите файл" : "Select a file")
                : Path.GetFileName(path);
            previewInfoLabel.Text = string.IsNullOrWhiteSpace(path) ? "" : GetFileInfo(path);
            if (string.IsNullOrWhiteSpace(path)) return;

            Task.Run(delegate { return LoadScaledPreview(path, new Size(900, 760)); }).ContinueWith(delegate(Task<Image> task)
            {
                if (task.IsFaulted || task.Result == null || IsDisposed)
                {
                    if (!task.IsFaulted && task.Result != null) task.Result.Dispose();
                    return;
                }
                SafeBeginInvoke(delegate
                {
                    if (version != previewVersion)
                    {
                        task.Result.Dispose();
                        return;
                    }
                    ReplaceLargePreview(task.Result);
                });
            });
        }

        private static Image LoadScaledPreview(string path, Size bounds)
        {
            string previewPath = ResolvePreviewPath(path);
            if (string.IsNullOrWhiteSpace(previewPath)) return null;
            try
            {
                using (Image image = Image.FromFile(previewPath))
                {
                    double scale = Math.Min((double)bounds.Width / Math.Max(image.Width, 1), (double)bounds.Height / Math.Max(image.Height, 1));
                    scale = Math.Min(scale, 1d);
                    int width = Math.Max(1, (int)Math.Round(image.Width * scale));
                    int height = Math.Max(1, (int)Math.Round(image.Height * scale));
                    Bitmap bitmap = new Bitmap(width, height);
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        graphics.DrawImage(image, 0, 0, width, height);
                    }
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ResolvePreviewPath(string path)
        {
            string extension = Path.GetExtension(path);
            string previewPath = path;
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                string png = Path.ChangeExtension(path, ".png");
                string fallback = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".preview.png");
                previewPath = File.Exists(png) ? png : fallback;
            }
            return imageExtensions.Contains(Path.GetExtension(previewPath)) && File.Exists(previewPath) ? previewPath : null;
        }

        private static Image CreateTypeThumbnail(string extension, Size size)
        {
            Bitmap bitmap = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(10, 12, 16)))
            using (SolidBrush foreground = new SolidBrush(Color.FromArgb(155, 167, 181)))
            using (Font font = new Font("Segoe UI Semibold", 12f))
            {
                graphics.FillRectangle(background, 0, 0, bitmap.Width, bitmap.Height);
                string text = string.IsNullOrWhiteSpace(extension) ? "FILE" : extension.TrimStart('.').ToUpperInvariant();
                SizeF textSize = graphics.MeasureString(text, font);
                graphics.DrawString(text, font, foreground, (bitmap.Width - textSize.Width) / 2, (bitmap.Height - textSize.Height) / 2);
            }
            return bitmap;
        }

        private static string GetFileInfo(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                return info.Extension.ToUpperInvariant() + Environment.NewLine + FormatBytes(info.Length);
            }
            catch
            {
                return "";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024d).ToString("N1") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024d * 1024d)).ToString("N1") + " MB";
            return (bytes / (1024d * 1024d * 1024d)).ToString("N2") + " GB";
        }

        private void RevealSelected()
        {
            if (string.IsNullOrWhiteSpace(selectedPath) || !File.Exists(selectedPath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select," + QuoteArgument(selectedPath),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenSelected()
        {
            if (string.IsNullOrWhiteSpace(selectedPath) || !File.Exists(selectedPath)) return;
            try { Process.Start(new ProcessStartInfo { FileName = selectedPath, UseShellExecute = true }); }
            catch { }
        }

        private void ClearTiles()
        {
            foreach (PictureBox picture in grid.Controls.OfType<Panel>().SelectMany(delegate(Panel panel) { return panel.Controls.OfType<PictureBox>(); }))
            {
                Image image = picture.Image;
                picture.Image = null;
                if (image != null) image.Dispose();
            }
            foreach (Control control in grid.Controls.Cast<Control>().ToList())
                control.Dispose();
            grid.Controls.Clear();
        }

        private void ReplaceLargePreview(Image image)
        {
            Image previous = largePreview.Image;
            largePreview.Image = image;
            if (previous != null) previous.Dispose();
        }

        private void SafeBeginInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((MethodInvoker)delegate { if (!IsDisposed) action(); }); }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                filterTimer.Dispose();
                ClearTiles();
                ReplaceLargePreview(null);
            }
            base.Dispose(disposing);
        }

        private static Button CreateButton(string text, Point location, int width)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 30),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
