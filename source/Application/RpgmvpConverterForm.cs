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
    public sealed partial class RpgmvpConverterForm : Form
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
        private ComboBox javaExtractModeBox;
        private Label subtitleLabel;
        private Label folderSectionLabel;
        private Label extractSectionLabel;
        private Label logSectionLabel;
        private Label keyLabel;
        private Label unityModeLabel;
        private Label javaModeLabel;
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
        private readonly JavaSvgPreviewRenderer svgPreviewRenderer;
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
        private static readonly HashSet<string> JavaImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico",
            ".tga", ".dds", ".tif", ".tiff", ".avif"
        };

        public RpgmvpConverterForm() : this(null)
        {
        }

        public RpgmvpConverterForm(string startupPath)
        {
            startupGamePath = startupPath;
            svgPreviewRenderer = new JavaSvgPreviewRenderer(
                delegate { return localCopyCancellationRequested; },
                ThrowIfLocalCopyCancelled,
                SafeLog,
                UpdateLocalProgress);
            BuildUi();

            ApplicationIcon.Apply(this);

            Shown += delegate { BeginInvoke((MethodInvoker)TryApplyStartupGamePath); };
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
            svgPreviewRenderer.Cancel();
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

        private string JavaModeValue()
        {
            switch (javaExtractModeBox.SelectedIndex)
            {
                case 0: return "images";
                case 2: return "all";
                default: return "images-svg";
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
                case GameEngine.Electron: return "Electron ASAR";
                case GameEngine.Html: return "HTML game";
                case GameEngine.Qsp: return "QSP";
                case GameEngine.Rags: return "RAGS experimental";
                case GameEngine.LegacyRpgMaker: return "RPG Maker XP/VX/VX Ace";
                case GameEngine.GameMaker: return "GameMaker experimental";
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
                case GameEngine.Electron: return Color.FromArgb(105, 190, 210);
                case GameEngine.Html: return Color.FromArgb(85, 180, 235);
                case GameEngine.Qsp: return Color.FromArgb(205, 165, 85);
                case GameEngine.Rags: return Color.FromArgb(190, 120, 210);
                case GameEngine.LegacyRpgMaker: return Color.FromArgb(85, 190, 240);
                case GameEngine.GameMaker: return Color.FromArgb(100, 200, 190);
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

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
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
                    "Game Asset Tool v1.9.0 report",
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
                ApplicationIcon.Apply(this);

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

                Button galleryButton = new Button
                {
                    Text = russian ? "Галерея файлов" : "Results Gallery",
                    Location = new Point(210, 292),
                    Size = new Size(160, 34),
                    BackColor = Color.FromArgb(45, 50, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                galleryButton.Click += delegate
                {
                    using (ResultsGalleryForm gallery = new ResultsGalleryForm(result.OutputDir, russian))
                        gallery.ShowDialog(this);
                };
                Controls.Add(galleryButton);

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
