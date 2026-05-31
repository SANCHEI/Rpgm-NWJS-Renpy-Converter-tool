using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal sealed class ResultsGalleryForm : Form
    {
        private const int MaxVisibleItems = 300;
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
            ".mp4", ".webm", ".avi", ".wmv", ".mov", ".mkv"
        };

        private readonly string outputDir;
        private readonly bool russian;
        private readonly ComboBox filterBox;
        private readonly TextBox searchBox;
        private readonly FlowLayoutPanel grid;
        private readonly Label statusLabel;
        private string selectedPath;

        public ResultsGalleryForm(string outputDir, bool russian)
        {
            this.outputDir = outputDir;
            this.russian = russian;

            Text = russian ? "Галерея результатов" : "Results Gallery";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(940, 680);
            MinimumSize = new Size(720, 500);
            BackColor = Color.FromArgb(17, 19, 24);
            ForeColor = Color.FromArgb(239, 243, 248);
            Font = new Font("Segoe UI", 9f);

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
            filterBox.SelectedIndexChanged += delegate { RefreshGrid(); };
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
            searchBox.TextChanged += delegate { RefreshGrid(); };
            toolbar.Controls.Add(searchBox);

            Button openButton = CreateButton(russian ? "Открыть файл" : "Open File", new Point(530, 28), 120);
            openButton.Click += delegate { OpenSelected(false); };
            toolbar.Controls.Add(openButton);
            Button revealButton = CreateButton(russian ? "Показать в папке" : "Show in Folder", new Point(660, 28), 145);
            revealButton.Click += delegate { OpenSelected(true); };
            toolbar.Controls.Add(revealButton);

            grid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(17, 19, 24),
                Padding = new Padding(10),
                WrapContents = true
            };
            Controls.Add(grid);
            grid.BringToFront();

            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(12, 5, 0, 0),
                ForeColor = Color.FromArgb(155, 167, 181),
                BackColor = Color.FromArgb(25, 29, 36)
            };
            Controls.Add(statusLabel);

            Shown += delegate { RefreshGrid(); };
        }

        internal static List<string> GetMatchingFiles(string outputDir, int filterIndex, string search)
        {
            if (!Directory.Exists(outputDir)) return new List<string>();
            string query = (search ?? "").Trim();
            try
            {
                return Directory.EnumerateFiles(outputDir, "*.*", SearchOption.AllDirectories)
                    .Where(delegate(string path)
                    {
                        string extension = Path.GetExtension(path);
                        bool supported = imageExtensions.Contains(extension)
                            || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
                            || audioExtensions.Contains(extension)
                            || videoExtensions.Contains(extension);
                        if (!supported) return false;
                        if (filterIndex == 1 && !imageExtensions.Contains(extension)) return false;
                        if (filterIndex == 2 && !extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)) return false;
                        if (filterIndex == 3 && !audioExtensions.Contains(extension)) return false;
                        if (filterIndex == 4 && !videoExtensions.Contains(extension)) return false;
                        return query.Length == 0 || path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    })
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void RefreshGrid()
        {
            selectedPath = null;
            foreach (Control control in grid.Controls.Cast<Control>().ToList())
                control.Dispose();
            grid.Controls.Clear();

            List<string> files = GetMatchingFiles(outputDir, filterBox.SelectedIndex, searchBox.Text);
            foreach (string path in files.Take(MaxVisibleItems))
                grid.Controls.Add(CreateTile(path));

            statusLabel.Text = files.Count > MaxVisibleItems
                ? string.Format(russian ? "Показано {0} из {1}. Уточните поиск." : "Showing {0} of {1}. Narrow the search to see more.", MaxVisibleItems, files.Count)
                : string.Format(russian ? "Файлов: {0}" : "Files: {0}", files.Count);
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
                Image = LoadThumbnail(path)
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
            EventHandler open = delegate { SelectTile(tile); OpenSelected(false); };
            tile.Click += select;
            preview.Click += select;
            name.Click += select;
            tile.DoubleClick += open;
            preview.DoubleClick += open;
            name.DoubleClick += open;
            return tile;
        }

        private Image LoadThumbnail(string path)
        {
            string extension = Path.GetExtension(path);
            string previewPath = path;
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                string png = Path.ChangeExtension(path, ".png");
                string fallback = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".preview.png");
                previewPath = File.Exists(png) ? png : fallback;
            }
            if (!imageExtensions.Contains(Path.GetExtension(previewPath)) || !File.Exists(previewPath))
                return CreateTypeThumbnail(extension);
            try
            {
                using (Image image = Image.FromFile(previewPath))
                    return new Bitmap(image);
            }
            catch
            {
                return CreateTypeThumbnail(extension);
            }
        }

        private static Image CreateTypeThumbnail(string extension)
        {
            Bitmap bitmap = new Bitmap(152, 108);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(10, 12, 16)))
            using (SolidBrush foreground = new SolidBrush(Color.FromArgb(155, 167, 181)))
            using (Font font = new Font("Segoe UI Semibold", 12f))
            {
                graphics.FillRectangle(background, 0, 0, bitmap.Width, bitmap.Height);
                string text = string.IsNullOrWhiteSpace(extension) ? "FILE" : extension.TrimStart('.').ToUpperInvariant();
                SizeF size = graphics.MeasureString(text, font);
                graphics.DrawString(text, font, foreground, (bitmap.Width - size.Width) / 2, (bitmap.Height - size.Height) / 2);
            }
            return bitmap;
        }

        private void SelectTile(Panel tile)
        {
            foreach (Panel panel in grid.Controls.OfType<Panel>())
                panel.BackColor = Color.FromArgb(25, 29, 36);
            tile.BackColor = Color.FromArgb(48, 73, 90);
            selectedPath = tile.Tag as string;
        }

        private void OpenSelected(bool reveal)
        {
            if (string.IsNullOrWhiteSpace(selectedPath) || !File.Exists(selectedPath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = reveal ? "explorer.exe" : selectedPath,
                    Arguments = reveal ? "/select," + QuoteArgument(selectedPath) : "",
                    UseShellExecute = true
                });
            }
            catch { }
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
