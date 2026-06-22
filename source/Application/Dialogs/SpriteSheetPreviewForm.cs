using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal sealed class SpriteSheetPreviewForm : Form
    {
        private readonly string imagePath;
        private readonly bool russian;
        private readonly Image sourceImage;
        private readonly MemoryStream sourceStream;
        private readonly SpriteCanvas canvas;
        private readonly NumericUpDown columnsBox;
        private readonly NumericUpDown rowsBox;
        private readonly Label metaLabel;
        private int selectedColumn;
        private int selectedRow;

        public SpriteSheetPreviewForm(string imagePath, bool russian)
        {
            this.imagePath = imagePath;
            this.russian = russian;
            Text = russian ? "Sprite sheet" : "Sprite Sheet";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 560);
            Size = new Size(920, 680);
            BackColor = Color.FromArgb(15, 18, 24);
            ForeColor = Color.WhiteSmoke;
            ApplicationIcon.Apply(this);

            byte[] bytes = File.ReadAllBytes(imagePath);
            sourceStream = new MemoryStream(bytes);
            sourceImage = Image.FromStream(sourceStream, false, false);

            Label title = new Label();
            title.AutoSize = false;
            title.Location = new Point(16, 14);
            title.Size = new Size(640, 24);
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.White;
            title.AutoEllipsis = true;
            title.Text = Path.GetFileName(imagePath);
            Controls.Add(title);

            Label columnsLabel = CreateLabel(russian ? "Колонки" : "Columns", 16, 52);
            Controls.Add(columnsLabel);
            columnsBox = CreateNumberBox(86, 49);
            Controls.Add(columnsBox);

            Label rowsLabel = CreateLabel(russian ? "Строки" : "Rows", 176, 52);
            Controls.Add(rowsLabel);
            rowsBox = CreateNumberBox(236, 49);
            Controls.Add(rowsBox);

            int columns;
            int rows;
            GuessGrid(sourceImage.Width, sourceImage.Height, out columns, out rows);
            columnsBox.Value = columns;
            rowsBox.Value = rows;
            columnsBox.ValueChanged += delegate { UpdateGrid(); };
            rowsBox.ValueChanged += delegate { UpdateGrid(); };

            Button exportButton = CreateButton(russian ? "Экспорт кадра..." : "Export Frame...", 326, 47, 145, 30);
            exportButton.Click += delegate { ExportSelectedFrame(); };
            Controls.Add(exportButton);

            Button closeButton = CreateButton(russian ? "Закрыть" : "Close", 482, 47, 100, 30);
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            metaLabel = new Label();
            metaLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            metaLabel.Location = new Point(16, 88);
            metaLabel.Size = new Size(872, 36);
            metaLabel.ForeColor = Color.FromArgb(170, 185, 205);
            Controls.Add(metaLabel);

            canvas = new SpriteCanvas();
            canvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            canvas.Location = new Point(16, 130);
            canvas.Size = new Size(872, 500);
            canvas.BackColor = Color.FromArgb(7, 10, 14);
            canvas.BorderStyle = BorderStyle.FixedSingle;
            canvas.Image = sourceImage;
            canvas.Columns = (int)columnsBox.Value;
            canvas.Rows = (int)rowsBox.Value;
            canvas.SelectedColumn = selectedColumn;
            canvas.SelectedRow = selectedRow;
            canvas.FrameSelected += delegate(int column, int row)
            {
                selectedColumn = column;
                selectedRow = row;
                canvas.SelectedColumn = selectedColumn;
                canvas.SelectedRow = selectedRow;
                UpdateMeta();
            };
            Controls.Add(canvas);

            FormClosed += delegate
            {
                sourceImage.Dispose();
                sourceStream.Dispose();
            };

            UpdateMeta();
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

        private static NumericUpDown CreateNumberBox(int x, int y)
        {
            NumericUpDown box = new NumericUpDown();
            box.Location = new Point(x, y);
            box.Size = new Size(72, 24);
            box.Minimum = 1;
            box.Maximum = 64;
            box.Value = 1;
            box.BackColor = Color.FromArgb(7, 10, 14);
            box.ForeColor = Color.WhiteSmoke;
            return box;
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

        private static void GuessGrid(int width, int height, out int columns, out int rows)
        {
            columns = 1;
            rows = 1;
            if (width <= 0 || height <= 0) return;

            double ratio = (double)width / Math.Max(1, height);
            if (ratio >= 2.0)
            {
                columns = Math.Max(1, Math.Min(16, (int)Math.Round(ratio)));
                rows = 1;
                return;
            }

            double reverse = (double)height / Math.Max(1, width);
            if (reverse >= 2.0)
            {
                columns = 1;
                rows = Math.Max(1, Math.Min(16, (int)Math.Round(reverse)));
                return;
            }

            if (width >= 1024 && height >= 1024)
            {
                columns = 4;
                rows = 4;
            }
        }

        private void UpdateGrid()
        {
            int columns = (int)columnsBox.Value;
            int rows = (int)rowsBox.Value;
            selectedColumn = Math.Min(selectedColumn, columns - 1);
            selectedRow = Math.Min(selectedRow, rows - 1);
            canvas.Columns = columns;
            canvas.Rows = rows;
            canvas.SelectedColumn = selectedColumn;
            canvas.SelectedRow = selectedRow;
            canvas.Invalidate();
            UpdateMeta();
        }

        private void UpdateMeta()
        {
            int columns = Math.Max(1, (int)columnsBox.Value);
            int rows = Math.Max(1, (int)rowsBox.Value);
            int frameWidth = sourceImage.Width / columns;
            int frameHeight = sourceImage.Height / rows;
            metaLabel.Text = (russian ? "Размер: " : "Size: ") + sourceImage.Width + " x " + sourceImage.Height
                + " | " + (russian ? "кадр: " : "frame: ") + frameWidth + " x " + frameHeight
                + " | " + (russian ? "выбран: " : "selected: ") + (selectedColumn + 1) + ", " + (selectedRow + 1);
        }

        private void ExportSelectedFrame()
        {
            int columns = Math.Max(1, (int)columnsBox.Value);
            int rows = Math.Max(1, (int)rowsBox.Value);
            int frameWidth = sourceImage.Width / columns;
            int frameHeight = sourceImage.Height / rows;
            if (frameWidth <= 0 || frameHeight <= 0) return;

            Rectangle sourceRect = new Rectangle(selectedColumn * frameWidth, selectedRow * frameHeight, frameWidth, frameHeight);
            using (Bitmap frame = new Bitmap(frameWidth, frameHeight, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(frame))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(sourceImage, new Rectangle(0, 0, frameWidth, frameHeight), sourceRect, GraphicsUnit.Pixel);

                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = russian ? "Сохранить кадр" : "Save frame";
                    dialog.Filter = "PNG (*.png)|*.png";
                    dialog.FileName = Path.GetFileNameWithoutExtension(imagePath) + "_r" + (selectedRow + 1) + "_c" + (selectedColumn + 1) + ".png";
                    string directory = Path.GetDirectoryName(imagePath);
                    if (!string.IsNullOrWhiteSpace(directory)) dialog.InitialDirectory = directory;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    frame.Save(dialog.FileName, ImageFormat.Png);
                }
            }
        }

        private sealed class SpriteCanvas : Panel
        {
            public event Action<int, int> FrameSelected;
            public Image Image;
            public int Columns = 1;
            public int Rows = 1;
            public int SelectedColumn;
            public int SelectedRow;

            public SpriteCanvas()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Image == null) return;

                Rectangle target = GetImageRectangle();
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(Image, target);

                int columns = Math.Max(1, Columns);
                int rows = Math.Max(1, Rows);
                using (Pen gridPen = new Pen(Color.FromArgb(160, 68, 197, 255), 1F))
                using (Pen selectedPen = new Pen(Color.FromArgb(255, 255, 112, 168), 3F))
                {
                    for (int column = 1; column < columns; column++)
                    {
                        int x = target.Left + (int)Math.Round(target.Width * column / (double)columns);
                        e.Graphics.DrawLine(gridPen, x, target.Top, x, target.Bottom);
                    }

                    for (int row = 1; row < rows; row++)
                    {
                        int y = target.Top + (int)Math.Round(target.Height * row / (double)rows);
                        e.Graphics.DrawLine(gridPen, target.Left, y, target.Right, y);
                    }

                    Rectangle selected = new Rectangle(
                        target.Left + (int)Math.Round(target.Width * SelectedColumn / (double)columns),
                        target.Top + (int)Math.Round(target.Height * SelectedRow / (double)rows),
                        Math.Max(1, (int)Math.Round(target.Width / (double)columns)),
                        Math.Max(1, (int)Math.Round(target.Height / (double)rows)));
                    e.Graphics.DrawRectangle(selectedPen, selected);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Rectangle target = GetImageRectangle();
                if (!target.Contains(e.Location)) return;
                int columns = Math.Max(1, Columns);
                int rows = Math.Max(1, Rows);
                int column = Math.Min(columns - 1, Math.Max(0, (int)((e.X - target.Left) * columns / (double)Math.Max(1, target.Width))));
                int row = Math.Min(rows - 1, Math.Max(0, (int)((e.Y - target.Top) * rows / (double)Math.Max(1, target.Height))));
                if (FrameSelected != null) FrameSelected(column, row);
                Invalidate();
            }

            private Rectangle GetImageRectangle()
            {
                if (Image == null || Width <= 0 || Height <= 0) return Rectangle.Empty;
                float scale = Math.Min((float)ClientSize.Width / Image.Width, (float)ClientSize.Height / Image.Height);
                int width = Math.Max(1, (int)(Image.Width * scale));
                int height = Math.Max(1, (int)(Image.Height * scale));
                int x = (ClientSize.Width - width) / 2;
                int y = (ClientSize.Height - height) / 2;
                return new Rectangle(x, y, width, height);
            }
        }
    }
}