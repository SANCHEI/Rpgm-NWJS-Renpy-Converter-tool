using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UnityExtractorWinForms
{
    public sealed class UnityExtractorForm : Form
    {
        private readonly Color formBack = Color.FromArgb(17, 19, 24);
        private readonly Color panelBack = Color.FromArgb(25, 29, 36);
        private readonly Color textColor = Color.FromArgb(239, 243, 248);
        private readonly Color accentColor = Color.FromArgb(68, 197, 255);
        private readonly Color successColor = Color.FromArgb(70, 204, 120);
        private readonly Color logBack = Color.FromArgb(10, 12, 16);

        private TextBox pathBox;
        private ComboBox extractModeBox;
        private Button extractButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label statsLabel;
        private TextBox logBox;
        private Button clearLogButton;
        private Label langLabel;
        private string currentLang = "en";

        public UnityExtractorForm()
        {
            BuildUi();
            ApplyLocalization();
        }

        private void BuildUi()
        {
            Text = "Unity Asset Extractor";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(700, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = formBack;
            ForeColor = textColor;

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(exeDir, "app.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            Label titleLabel = new Label
            {
                Text = "Unity Asset Extractor",
                Location = new Point(20, 20),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = textColor,
                BackColor = Color.Transparent
            };
            Controls.Add(titleLabel);

            langLabel = new Label { Text = "En", Location = new Point(620, 22), Size = new Size(40, 20), ForeColor = accentColor, Cursor = Cursors.Hand };
            langLabel.Click += delegate
            {
                currentLang = currentLang == "en" ? "ru" : "en";
                ApplyLocalization();
            };
            Controls.Add(langLabel);

            Panel pathPanel = new Panel { Location = new Point(20, 70), Size = new Size(660, 40), BackColor = panelBack };
            Controls.Add(pathPanel);

            Label pathLabel = new Label { Text = "Game Folder:", Location = new Point(10, 10), Size = new Size(80, 20), ForeColor = textColor };
            pathPanel.Controls.Add(pathLabel);

            pathBox = new TextBox { Location = new Point(95, 8), Size = new Size(480, 24), BackColor = Color.FromArgb(13, 16, 21), ForeColor = textColor, BorderStyle = BorderStyle.None };
            pathPanel.Controls.Add(pathBox);

            Button browseBtn = new Button { Text = "...", Location = new Point(580, 6), Size = new Size(30, 26), FlatStyle = FlatStyle.Flat, BackColor = panelBack, ForeColor = textColor };
            browseBtn.FlatAppearance.BorderSize = 0;
            browseBtn.Click += delegate { using (var fd = new FolderBrowserDialog()) if (fd.ShowDialog() == DialogResult.OK) pathBox.Text = fd.SelectedPath; };
            pathPanel.Controls.Add(browseBtn);

            Button gameBtn = new Button { Text = "G", Location = new Point(615, 6), Size = new Size(30, 26), FlatStyle = FlatStyle.Flat, BackColor = panelBack, ForeColor = textColor };
            gameBtn.FlatAppearance.BorderSize = 0;
            gameBtn.Click += delegate { string root = TryFindUnityGame(AppDomain.CurrentDomain.BaseDirectory); if (!string.IsNullOrEmpty(root)) pathBox.Text = root; };
            pathPanel.Controls.Add(gameBtn);

            Panel modePanel = new Panel { Location = new Point(20, 120), Size = new Size(300, 40), BackColor = panelBack };
            Controls.Add(modePanel);

            Label modeLabel = new Label { Text = "Mode:", Location = new Point(10, 10), Size = new Size(50, 20), ForeColor = textColor };
            modePanel.Controls.Add(modeLabel);

            extractModeBox = new ComboBox { Location = new Point(65, 7), Size = new Size(150, 25), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(13, 16, 21), ForeColor = textColor };
            extractModeBox.Items.Add("Textures");
            extractModeBox.Items.Add("Videos");
            extractModeBox.Items.Add("All Assets");
            extractModeBox.SelectedIndex = 0;
            modePanel.Controls.Add(extractModeBox);

            extractButton = new Button { Text = "Extract Unity", Location = new Point(340, 120), Size = new Size(120, 38), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(138, 98, 255), ForeColor = Color.White };
            extractButton.FlatAppearance.BorderSize = 0;
            extractButton.Font = new Font("Segoe UI Semibold", 9f);
            extractButton.Click += delegate { StartExtraction(); };
            Controls.Add(extractButton);

            Panel progressPanel = new Panel { Location = new Point(20, 170), Size = new Size(660, 60), BackColor = panelBack };
            Controls.Add(progressPanel);

            progressBar = new ProgressBar { Location = new Point(10, 10), Size = new Size(640, 20), Style = ProgressBarStyle.Continuous, ForeColor = accentColor, BackColor = Color.FromArgb(40, 44, 52) };
            progressPanel.Controls.Add(progressBar);

            statusLabel = new Label { Text = "Ready", Location = new Point(10, 35), Size = new Size(300, 20), ForeColor = textColor };
            progressPanel.Controls.Add(statusLabel);

            statsLabel = new Label { Text = "", Location = new Point(320, 35), Size = new Size(330, 20), ForeColor = Color.FromArgb(155, 167, 181), TextAlign = ContentAlignment.TopRight };
            progressPanel.Controls.Add(statsLabel);

            Panel logPanel = new Panel { Location = new Point(20, 240), Size = new Size(660, 320), BackColor = panelBack, Padding = new Padding(5) };
            Controls.Add(logPanel);

            clearLogButton = new Button { Text = "Clear", Location = new Point(570, 5), Size = new Size(70, 25), FlatStyle = FlatStyle.Flat, BackColor = panelBack, ForeColor = textColor, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            clearLogButton.FlatAppearance.BorderSize = 0;
            clearLogButton.Click += delegate { logBox.Clear(); };
            logPanel.Controls.Add(clearLogButton);

            logBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new Point(10, 35),
                Size = new Size(640, 275),
                BackColor = logBack,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10f)
            };
            logBox.TextChanged += delegate { logBox.SelectionStart = logBox.Text.Length; logBox.ScrollToCaret(); };
            logBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A) { logBox.SelectAll(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.C) { if (logBox.SelectionLength > 0) Clipboard.SetText(logBox.SelectedText); e.SuppressKeyPress = true; }
            };
            logPanel.Controls.Add(logBox);
        }

        private void ApplyLocalization()
        {
            langLabel.Text = currentLang == "en" ? "En" : "Ru";
            extractButton.Text = currentLang == "en" ? "Extract Unity" : "Извлечь Unity";
            if (extractModeBox.Items.Count >= 3)
            {
                extractModeBox.Items[0] = currentLang == "en" ? "Textures" : "Текстуры";
                extractModeBox.Items[1] = currentLang == "en" ? "Videos" : "Видео";
                extractModeBox.Items[2] = currentLang == "en" ? "All Assets" : "Все ассеты";
            }
        }

        private void StartExtraction()
        {
            string rootPath = pathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                MessageBox.Show(currentLang == "en" ? "Please select a valid game folder" : "Выберите корректную папку игры", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string modeStr = extractModeBox.SelectedIndex == 0 ? "textures" : (extractModeBox.SelectedIndex == 1 ? "videos" : "all");
            WriteLog("Unity extraction started: " + modeStr);

            Task.Run(delegate { RunExtraction(rootPath, modeStr); });
        }

        private string TryFindUnityGame(string path)
        {
            DirectoryInfo current = new DirectoryInfo(path);
            while (current != null)
            {
                bool hasData = Directory.GetDirectories(current.FullName, "*_Data").Length > 0;
                if (hasData) return current.FullName;
                current = current.Parent;
            }
            return null;
        }

        private void RunExtraction(string rootPath, string extractMode)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempDir = Path.Combine(Path.GetTempPath(), "UnityExtractor");
                Directory.CreateDirectory(tempDir);
                string scriptPath = Path.Combine(tempDir, "extract_unity.py");

                string script = GetExtractionScript();
                File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(false));

                string outputDir = Path.Combine(rootPath, "extracted");
                Directory.CreateDirectory(outputDir);

                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Maximum = 100;
                    progressBar.Value = 0;
                    statusLabel.Text = "Scanning...";
                    statsLabel.Text = "";
                    extractButton.Enabled = false;
                });

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

                        process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                this.BeginInvoke((MethodInvoker)delegate
                                {
                                    WriteLog(e.Data);

                                    if (e.Data.StartsWith("TOTAL:"))
                                    {
                                        string[] parts = e.Data.Split(':');
                                        if (parts.Length >= 2) int.TryParse(parts[1], out totalFiles);
                                        progressBar.Maximum = Math.Max(totalFiles, 1);
                                        statusLabel.Text = "Found " + totalFiles + " files";
                                    }
                                    else if (e.Data.StartsWith("PROGRESS:"))
                                    {
                                        string[] parts = e.Data.Split(':');
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
                                            statsLabel.Text = processedFiles + " / " + totalFiles + " | " + speed.ToString("N2") + " f/s | ETA: " + eta;
                                            statusLabel.Text = "Processing " + processedFiles + "/" + totalFiles;
                                        }
                                    }
                                });
                            }
                        };
                        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                this.BeginInvoke((MethodInvoker)delegate { WriteLog("ERROR: " + e.Data); });
                        };
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                    }
                }

                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = progressBar.Maximum;
                    statusLabel.Text = "Complete";
                    statsLabel.Text = processedFiles + " / " + totalFiles;
                    extractButton.Enabled = true;
                    MessageBox.Show("Unity extraction complete!\n\nExtracted: " + processedFiles + " files\n\nOutput: " + outputDir, "Unity Extractor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (Exception ex)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = 0;
                    statusLabel.Text = "Error";
                    WriteLog("Error: " + ex.Message);
                    extractButton.Enabled = true;
                });
            }
        }

        private static string GetExtractionScript()
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

os.makedirs(output_path, exist_ok=True)

def extract_file(file_path, output, mode):
    total = 0
    try:
        env = UnityPy.load(file_path)
        
        has_textures = any(obj.type.name == 'Texture2D' for obj in env.objects)
        if not has_textures and mode in ['textures', 'all']:
            return 0
        
        print('Processing: ' + os.path.basename(file_path))
        sys.stdout.flush()
        
        file_output = os.path.join(output, os.path.splitext(os.path.basename(file_path))[0])
        os.makedirs(file_output, exist_ok=True)
        
        textures = 0
        audios = 0
        
        for obj in env.objects:
            try:
                if mode in ['textures', 'all'] and obj.type.name == 'Texture2D':
                    data = obj.read()
                    name = getattr(data, 'name', None) or getattr(data, 'm_Name', 'texture_' + str(textures))
                    safe_name = ''.join(c for c in str(name) if c.isalnum() or c in '._- ')
                    
                    if hasattr(data, 'image') and data.image:
                        img_path = os.path.join(file_output, safe_name + '.png')
                        data.image.save(img_path)
                        textures += 1
                        total += 1
                
                if mode in ['audios', 'all'] and obj.type.name == 'AudioClip':
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
            except:
                pass
        
        if textures > 0 or audios > 0:
            print('  +' + str(textures) + ' textures, +' + str(audios) + ' audios')
            sys.stdout.flush()
        
    except Exception as e:
        print('Error: ' + str(e))
        sys.stdout.flush()
    
    return total

print('Unity Asset Extractor')
print('Game: ' + game_path)
print('Output: ' + output_path)
print('Mode: ' + extract_mode)
print('-' * 40)

total_extracted = 0
all_files = []

for item in os.listdir(game_path):
    if item.endswith('_Data'):
        data_folder = os.path.join(game_path, item)
        
        try:
            for f in os.listdir(data_folder):
                if f.endswith('.assets') and not f.endswith('.resS'):
                    all_files.append(os.path.join(data_folder, f))
        except: pass
        
        ggm_path = os.path.join(data_folder, 'globalgamemanagers.assets')
        if os.path.exists(ggm_path):
            all_files.append(ggm_path)
        
        streaming = os.path.join(data_folder, 'StreamingAssets', 'aa', 'StandaloneWindows64')
        if os.path.exists(streaming):
            try:
                for bundle_file in os.listdir(streaming):
                    if bundle_file.endswith('.bundle'):
                        all_files.append(os.path.join(streaming, bundle_file))
            except: pass

print('Found ' + str(len(all_files)) + ' files to process')
sys.stdout.flush()
print('TOTAL:' + str(len(all_files)))
sys.stdout.flush()

for i, file_path in enumerate(all_files):
    print('PROGRESS:' + str(i) + ':' + str(len(all_files)))
    sys.stdout.flush()
    total_extracted += extract_file(file_path, output_path, extract_mode)

print('-' * 40)
print('Done! Extracted ' + str(total_extracted) + ' assets')
sys.stdout.flush()
";
        }

        private void WriteLog(string message)
        {
            if (logBox.TextLength > 0) logBox.AppendText(Environment.NewLine);
            logBox.AppendText(message);
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "--:--";
            TimeSpan span = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            if (span.TotalHours >= 1)
                return string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
            return string.Format("{0:00}:{1:00}", span.Minutes, span.Seconds);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UnityExtractorForm());
        }
    }
}
