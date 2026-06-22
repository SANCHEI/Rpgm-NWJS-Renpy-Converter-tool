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
        private async Task StartDetectedExtractionAsync()
        {
            if (IsDiagnosticsOnlyProfile())
            {
                await RunDryScanAsync(true);
                return;
            }

            string inputPath = pathBox.Text.Trim();
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath))
            {
                WriteLog("Invalid path");
                return;
            }

            GameEngine engine = EffectiveEngine(DetectEngine(inputPath));
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
            if (engine == GameEngine.AndroidApk)
            {
                await StartAndroidApkExtractionAsync();
                return;
            }
            if (engine == GameEngine.SrpgStudio)
            {
                await StartSrpgStudioExtractionAsync();
                return;
            }
            if (engine == GameEngine.PixelGameMaker)
            {
                await StartPixelGameMakerExtractionAsync();
                return;
            }
            if (engine == GameEngine.Flash)
            {
                await StartFlashExtractionAsync();
                return;
            }
            if (engine == GameEngine.Electron)
            {
                await StartElectronExtractionAsync();
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
            if (engine == GameEngine.LegacyRpgMaker)
            {
                await StartLegacyRpgMakerExtractionAsync();
                return;
            }
            if (engine == GameEngine.GameMaker)
            {
                await StartGameMakerExtractionAsync();
                return;
            }
            if (engine == GameEngine.SpakDat)
            {
                await StartSpakDatExtractionAsync();
                return;
            }
            if (engine == GameEngine.PygamePyInstaller)
            {
                await StartPygamePyInstallerExtractionAsync();
                return;
            }
            if (engine != GameEngine.RpgMaker)
            {
                await StartSignatureRecoveryAsync();
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

            string outputDir = Path.Combine(rootPath, "extracted", "rpgm");
            if (!TryResetExtractionRootForOutput(outputDir)) return;

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
            if (!TryResetExtractionRootForOutput(outputDir)) return;
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
            if (!TryResetExtractionRootForOutput(outputDir)) return;
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
            UnityExtractionConfig config = UnityExtractionConfig.Create(mode, includeBundles);
            string outputDir = Path.Combine(rootPath, "extracted", "unity");
            lastOutputDir = outputDir;
            if (!TryResetExtractionRootForOutput(outputDir)) return;
            SetExternalRunningState(true, "Unity");
            WriteLog("Unity extraction started: " + mode + (includeBundles ? " with bundles" : " without bundles"));

            OperationResult result;
            try
            {
                result = await Task.Run(delegate { return RunUnityExtraction(rootPath, outputDir, config); });
            }
            catch (Exception ex)
            {
                result = OperationResult.Failed("Unity", outputDir, ex.Message);
            }
            SetExternalRunningState(false, "Unity");
            CompleteExternalOperation(result);
        }

        private OperationResult RunUnityExtraction(string rootPath, string outputDir, UnityExtractionConfig config)
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
            string unityPhase = "scan";

            ProcessStartInfo psi = CreatePythonProcessInfo();
            psi.Arguments = QuoteArg(scriptPath);
            config.ApplyTo(psi, rootPath, outputDir);

            int exitCode = RunExternalProcess(psi, delegate(string line)
            {
                if (line.StartsWith("TOTAL:", StringComparison.Ordinal))
                {
                    int total = ParseInt(line, 1);
                    BeginUi(delegate
                    {
                        progressBar.Maximum = Math.Max(total, 1);
                        progressBar.Value = 0;
                        statusLabel.Text = T("Unity: scan complete", "Unity: проверка завершена");
                        statsLabel.Text = T("Archives found: ", "Архивов найдено: ") + total;
                    });
                }
                else if (line.StartsWith("PHASE:", StringComparison.Ordinal))
                {
                    unityPhase = line.Substring("PHASE:".Length).Trim();
                    BeginUi(delegate
                    {
                        if (unityPhase == "scan")
                        {
                            progressBar.Value = 0;
                            statusLabel.Text = T("Unity: scanning files", "Unity: поиск файлов");
                            statsLabel.Text = T("Looking for .assets, .bundle and direct media files...", "Поиск .assets, .bundle и открытых медиа...");
                        }
                        else if (unityPhase == "direct")
                        {
                            progressBar.Value = 0;
                            statusLabel.Text = T("Unity: copying direct media", "Unity: копирование открытых медиа");
                            statsLabel.Text = T("Copying files already present outside Unity archives...", "Копирование файлов, которые уже лежат вне Unity-архивов...");
                        }
                        else if (unityPhase == "archives")
                        {
                            progressBar.Value = 0;
                            statusLabel.Text = T("Unity: extracting archives", "Unity: извлечение архивов");
                            statsLabel.Text = T("Exporting textures/videos from Unity archives...", "Экспорт текстур/видео из Unity-архивов...");
                        }
                    });
                }
                else if (line.StartsWith("DIRECT_PROGRESS:", StringComparison.Ordinal))
                {
                    int processed = ParseInt(line, 1);
                    int total = ParseInt(line, 2);
                    long currentBytes = ParseLong(line, 3);
                    BeginUi(delegate
                    {
                        progressBar.Maximum = Math.Max(total, 1);
                        progressBar.Value = Math.Min(processed, progressBar.Maximum);
                        statusLabel.Text = T("Unity direct media: ", "Unity открытые медиа: ") + processed + " / " + total;
                        statsLabel.Text = T("Copied direct files: ", "Скопировано открытых файлов: ") + processed + " / " + total + T(" | Size: ", " | Размер: ") + FormatBytes(currentBytes);
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
                        statusLabel.Text = T("Unity archives: ", "Unity архивы: ") + processed + " / " + total;
                        statsLabel.Text = T("Archives: ", "Архивы: ") + processed + " / " + total + T(" | Extracted size: ", " | Извлечено: ") + FormatBytes(currentBytes);
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
            if (!TryResetExtractionRootForOutput(outputDir)) return;
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
                    string originalRelative = MakeRelativePath(rootPath, source);
                    string relative = HiddenMediaExtensions.NormalizeRelativePath(originalRelative, source);
                    DeleteStaleRenamedOutput(outputDir, Path.Combine("loose", originalRelative), Path.Combine("loose", relative));
                    string destination = GetSafeOutputPath(outputDir, Path.Combine("loose", relative));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination, true);
                    extracted++;
                    bytes += SafeFileLength(destination);
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
                catch (InvalidDataException ex)
                {
                    skipped++;
                    SafeLog("NWJS package skipped: " + archive + ": " + ex.Message);
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
            ExtractionProgressEvent progress = new ExtractionProgressEvent(operation, processed, total, bytes);
            OnExtractionProgress(progress);
        }

        private void OnExtractionProgress(ExtractionProgressEvent progress)
        {
            BeginUi(delegate
            {
                progressBar.Maximum = Math.Max(progress.Total, 1);
                progressBar.Value = Math.Min(progress.Processed, progressBar.Maximum);
                statusLabel.Text = progress.Operation + ": " + progress.Processed + " / " + progress.Total;
                statsLabel.Text = T("Sources: ", "Источники: ") + progress.Processed + " / " + progress.Total + T(" | Size: ", " | Размер: ") + FormatBytes(progress.Bytes);
            });
        }

        private void ThrowIfLocalCopyCancelled()
        {
            if (localCopyCancellationRequested)
                throw new OperationCanceledException();
            if (localExtractionContext != null)
                localExtractionContext.ThrowIfCancellationRequested();
        }

        private bool IsLocalCopyCancelled()
        {
            return localCopyCancellationRequested
                || (localExtractionContext != null && localExtractionContext.IsCancellationRequested);
        }

        private NwjsCopyStats ExtractNwjsZipArchive(string archivePath, string outputDir)
        {
            return ExtractZipArchive(archivePath, outputDir, Path.Combine("archives", Path.GetFileNameWithoutExtension(archivePath)), null, null, true);
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
            string mode = JavaModeValue();
            await StartLocalExtractionAsync("Java game / JAR", "java", delegate(string rootPath, string outputDir)
            {
                return RunJavaExtractionWithMode(rootPath, outputDir, mode);
            });
        }

        private async Task StartFlashExtractionAsync()
        {
            await StartLocalExtractionAsync("Flash SWF experimental", "flash", delegate(string rootPath, string outputDir)
            {
                return RunFlashExtraction(rootPath, outputDir);
            });
        }

        private async Task StartElectronExtractionAsync()
        {
            await StartLocalExtractionAsync("Electron ASAR", "electron", delegate(string rootPath, string outputDir)
            {
                return RunElectronExtraction(rootPath, outputDir);
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

        private async Task StartLegacyRpgMakerExtractionAsync()
        {
            await StartLocalExtractionAsync("RPG Maker XP/VX/VX Ace", "rgss", delegate(string inputPath, string outputDir)
            {
                return RunLegacyRpgMakerExtraction(inputPath, outputDir);
            });
        }

        private async Task StartGameMakerExtractionAsync()
        {
            await StartPortableScriptExtractionAsync(
                "GameMaker experimental",
                "gamemaker",
                "extract_gamemaker.py",
                "RpgmvpConverterWinForms.scripts.extract_gamemaker.py");
        }

        private async Task StartAndroidApkExtractionAsync()
        {
            await StartLocalExtractionAsync("Android APK recovery", "android-apk", RunAndroidApkExtraction);
        }

        private async Task StartSrpgStudioExtractionAsync()
        {
            await StartLocalExtractionAsync("SRPG Studio recovery", "srpg-studio", delegate(string inputPath, string outputDir)
            {
                return RunLooseAndSignatureRecovery("SRPG Studio recovery", inputPath, outputDir, GetSrpgStudioFiles(inputPath, outputDir));
            });
        }

        private async Task StartPixelGameMakerExtractionAsync()
        {
            await StartLocalExtractionAsync("Pixel Game Maker MV recovery", "pixel-game-maker", delegate(string inputPath, string outputDir)
            {
                return RunLooseAndSignatureRecovery("Pixel Game Maker MV recovery", inputPath, outputDir, GetPixelGameMakerFiles(inputPath, outputDir));
            });
        }


        private async Task StartPygamePyInstallerExtractionAsync()
        {
            await StartLocalExtractionAsync("Pygame / PyInstaller", "pygame", RunPygamePyInstallerExtraction);
        }

        private OperationResult RunPygamePyInstallerExtraction(string inputPath, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            string root = InputDirectory(inputPath);
            List<string> loose = AssetCollectors.GetLooseResourceFiles(root, outputDir);
            CollectorResult copied = AssetCollectors.CopyFiles(root, loose, outputDir, "loose");
            CollectorResult decoded = DecodePygameDatImages(root, Path.Combine(outputDir, "decoded-dat"));
            return new OperationResult("Pygame / PyInstaller", outputDir, copied.Extracted + decoded.Extracted, copied.Bytes + decoded.Bytes, 0, copied.Renamed + decoded.Renamed, copied.Skipped + decoded.Skipped, DateTime.UtcNow - start);
        }

        private CollectorResult DecodePygameDatImages(string rootPath, string outputDir)
        {
            int extracted = 0, renamed = 0, skipped = 0;
            long bytes = 0;
            foreach (string file in Directory.EnumerateFiles(rootPath, "*.dat", SearchOption.AllDirectories))
            {
                if (file.IndexOf(Path.DirectorySeparatorChar + "extracted" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                byte[] data;
                try { data = File.ReadAllBytes(file); } catch { skipped++; continue; }
                if (data.Length < 8) { skipped++; continue; }
                for (int i = 0; i < data.Length; i++) data[i] ^= 0x6A;
                string ext = DetectDecodedImageExtension(data);
                if (ext == null) { skipped++; continue; }
                string relative = MakeRelativePathLocal(rootPath, file);
                relative = Path.ChangeExtension(relative, ext);
                string destination = Path.Combine(outputDir, relative);
                destination = UniqueOutputPathLocal(destination, ref renamed);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllBytes(destination, data);
                extracted++;
                bytes += data.Length;
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }


        private static string MakeRelativePathLocal(string rootPath, string filePath)
        {
            Uri root = new Uri(AppendDirectorySeparator(Path.GetFullPath(rootPath)));
            Uri file = new Uri(Path.GetFullPath(filePath));
            return Uri.UnescapeDataString(root.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string UniqueOutputPathLocal(string path, ref int renamed)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string stem = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int suffix = 2;
            while (true)
            {
                string candidate = Path.Combine(dir, stem + " (" + suffix + ")" + ext);
                if (!File.Exists(candidate))
                {
                    renamed++;
                    return candidate;
                }
                suffix++;
            }
        }
        private static string DetectDecodedImageExtension(byte[] data)
        {
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
            if (data.Length >= 4 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46) return ".webp";
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
            if (data.Length >= 4 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38) return ".gif";
            return null;
        }
        private async Task StartSpakDatExtractionAsync()
        {
            await StartPortableScriptExtractionAsync(
                "SPAK DAT / SPITE experimental",
                "spak-dat",
                "extract_spite.py",
                "RpgmvpConverterWinForms.scripts.extract_spite.py");
        }

        private async Task StartLooseResourceCollectionAsync()
        {
            await StartLocalExtractionAsync("Loose resources", "loose-assets", delegate(string inputPath, string outputDir)
            {
                List<string> files = AssetCollectors.GetLooseResourceFiles(inputPath, outputDir);
                files = FilterLooseCollectionFiles(files, LooseModeValue());
                return RunCollectorExtraction("Loose resources (" + LooseModeValue() + ")", inputPath, outputDir, files);
            });
        }

        private string LooseModeValue()
        {
            return CurrentExtractionProfile().LooseMode(selectedLooseModeIndex);
        }

        private static List<string> FilterLooseCollectionFiles(IEnumerable<string> files, string mode)
        {
            if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
                return files.ToList();

            bool imagesOnly = string.Equals(mode, "images", StringComparison.OrdinalIgnoreCase);
            return files.Where(delegate(string path)
            {
                string extension = Path.GetExtension(path);
                if (MediaTypeRegistry.IsImageLike(extension)) return true;
                return !imagesOnly && MediaTypeRegistry.IsVideo(extension);
            }).ToList();
        }

        private async Task StartSignatureRecoveryAsync()
        {
            await StartLocalExtractionAsync("Signature recovery", "signature-recovery", delegate(string inputPath, string outputDir)
            {
                DateTime start = DateTime.UtcNow;
                CollectorResult extracted = SignatureAssetExtractor.Extract(inputPath, outputDir, IsLocalCopyCancelled);
                string report = AssetCollectors.WriteDiagnostics(inputPath, outputDir);
                SafeLog("Unknown-engine diagnostics: " + report);
                return new OperationResult("Unknown format signature recovery", outputDir, extracted.Extracted, extracted.Bytes, 0, extracted.Renamed, extracted.Skipped, DateTime.UtcNow - start);
            });
        }

        private OperationResult RunCollectorExtraction(string engineName, string inputPath, string outputDir, List<string> files)
        {
            DateTime start = DateTime.UtcNow;
            string rootPath = InputDirectory(inputPath);
            NwjsCopyStats copied = CopyLooseFiles(rootPath, files, outputDir, "");
            return new OperationResult(engineName, outputDir, copied.Extracted, copied.Bytes, 0, copied.Renamed, copied.Skipped, DateTime.UtcNow - start);
        }

        private OperationResult RunLegacyRpgMakerExtraction(string inputPath, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> archives = FindLegacyRpgMakerArchives(inputPath);
            int extracted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;
            int skipped = 0;
            for (int i = 0; i < archives.Count; i++)
            {
                ThrowIfLocalCopyCancelled();
                string archive = archives[i];
                try
                {
                    bool renamedDirectory;
                    string destination = GetSafeOutputPath(outputDir, Path.Combine("archives", Path.GetFileNameWithoutExtension(archive)));
                    destination = GetUniqueDirectoryPath(destination, out renamedDirectory);
                    Directory.CreateDirectory(destination);
                    CollectorResult stats = LegacyRpgMakerExtractor.ExtractArchive(archive, destination);
                    extracted += stats.Extracted;
                    bytes += stats.Bytes;
                    renamed += stats.Renamed;
                    skipped += stats.Skipped;
                    if (renamedDirectory) renamed++;
                }
                catch (Exception ex)
                {
                    errors++;
                    SafeLog("WARN:" + archive + ":" + ex.Message);
                }
                UpdateLocalProgress("RPG Maker RGSS", i + 1, archives.Count, bytes);
            }

            if (archives.Count == 0) skipped++;
            return new OperationResult("RPG Maker XP/VX/VX Ace", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
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
            if (!TryResetExtractionRootForOutput(outputDir)) return;
            localCopyCancellationRequested = false;
            if (localExtractionContext != null)
            {
                localExtractionContext.Dispose();
                localExtractionContext = null;
            }
            localExtractionContext = new ExtractionRunContext(engineName);
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
            if (localExtractionContext != null)
            {
                localExtractionContext.Dispose();
                localExtractionContext = null;
            }
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
            return RunJavaExtractionWithMode(rootPath, outputDir, "images-svg");
        }

        private OperationResult RunJavaExtractionWithMode(string rootPath, string outputDir, string mode)
        {
            string inputPath = rootPath;
            rootPath = InputDirectory(rootPath);
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> archives = FindJavaArchives(inputPath);
            bool allResources = string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase);
            bool renderPreviews = string.Equals(mode, "images-svg", StringComparison.OrdinalIgnoreCase);
            List<string> looseFiles = GetJavaLooseFiles(rootPath, outputDir, mode);
            List<string> extractedPaths = new List<string>();
            NwjsCopyStats loose = CopyLooseFiles(rootPath, looseFiles, outputDir, "loose", extractedPaths, true);
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
                    NwjsCopyStats stats = ExtractZipArchive(
                        archive,
                        outputDir,
                        Path.Combine("archives", Path.GetFileNameWithoutExtension(archive)),
                        allResources ? null : (Func<string, bool>)IsJavaImageFile,
                        extractedPaths,
                        true);
                    extracted += stats.Extracted;
                    bytes += stats.Bytes;
                    renamed += stats.Renamed;
                    skipped += stats.Skipped;
                }
                catch (InvalidDataException ex)
                {
                    skipped++;
                    SafeLog("Java JAR skipped: " + archive + ": " + ex.Message);
                }
                catch (Exception ex)
                {
                    errors++;
                    SafeLog("WARN:" + archive + ":" + ex.Message);
                }
                UpdateLocalProgress("Java", looseFiles.Count + i + 1, total, bytes);
            }

            if (renderPreviews)
            {
                SvgPreviewResult previews = svgPreviewRenderer.Render(extractedPaths, outputDir);
                extracted += previews.Converted;
                bytes += previews.Bytes;
                errors += previews.Errors;
                renamed += previews.Renamed;
                skipped += previews.Skipped;
            }

            return new OperationResult("Java game / JAR (" + mode + ")", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private OperationResult RunAndroidApkExtraction(string inputPath, string outputDir)
        {
            AndroidApkExtractorService service = new AndroidApkExtractorService(
                IsLocalCopyCancelled,
                SafeLog,
                delegate(int processed, int total, long bytes)
                {
                    UpdateLocalProgress("Android APK", processed, total, bytes);
                });
            ApkExtractionResult result = service.Extract(inputPath, outputDir);
            return new OperationResult(
                "Android APK recovery",
                outputDir,
                result.Extracted,
                result.Bytes,
                result.Errors,
                result.Renamed,
                result.Skipped,
                result.Duration);
        }

        private OperationResult RunLooseAndSignatureRecovery(string engineName, string inputPath, string outputDir, List<string> files)
        {
            DateTime start = DateTime.UtcNow;
            string rootPath = InputDirectory(inputPath);
            Directory.CreateDirectory(outputDir);
            NwjsCopyStats loose = CopyLooseFiles(rootPath, files, outputDir, "loose", null, true);
            CollectorResult carved = SignatureAssetExtractor.Extract(inputPath, Path.Combine(outputDir, "signature-recovery"), IsLocalCopyCancelled);
            return new OperationResult(
                engineName,
                outputDir,
                loose.Extracted + carved.Extracted,
                loose.Bytes + carved.Bytes,
                0,
                loose.Renamed + carved.Renamed,
                loose.Skipped + carved.Skipped,
                DateTime.UtcNow - start);
        }

        private OperationResult RunFlashExtraction(string rootPath, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            CollectorResult result = AssetExtractorRegistry.Find("flash-swf").Extract(rootPath, outputDir);
            return new OperationResult("Flash SWF experimental", outputDir, result.Extracted, result.Bytes, 0, result.Renamed, result.Skipped, DateTime.UtcNow - start);
        }

        private OperationResult RunElectronExtraction(string rootPath, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            CollectorResult result = AssetExtractorRegistry.Find("electron-asar").Extract(rootPath, outputDir);
            return new OperationResult("Electron ASAR", outputDir, result.Extracted, result.Bytes, 0, result.Renamed, result.Skipped, DateTime.UtcNow - start);
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

            WriteWolfDiagnostics(rootPath, outputDir, archives, looseFiles, errors, skipped);
            return new OperationResult("WOLF RPG", outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private static void WriteWolfDiagnostics(string rootPath, string outputDir, List<string> archives, List<string> looseFiles, int errors, int skipped)
        {
            try
            {
                StringBuilder report = new StringBuilder();
                report.AppendLine("Game Asset Tool WOLF RPG diagnostics");
                report.AppendLine("Root: " + rootPath);
                report.AppendLine("Archives: " + archives.Count);
                report.AppendLine("Loose Data files: " + looseFiles.Count);
                report.AppendLine("Errors: " + errors);
                report.AppendLine("Skipped: " + skipped);
                report.AppendLine();
                report.AppendLine("Archive candidates:");
                foreach (string archive in archives.Take(80))
                    report.AppendLine("- " + MakeRelativePath(rootPath, archive) + " | " + FormatBytes(SafeFileLength(archive)));
                if (archives.Count > 80) report.AppendLine("- ...");
                report.AppendLine();
                report.AppendLine("Notes:");
                report.AppendLine("- Embedded UberWolf CLI is used for supported DxLib/WOLF archives.");
                report.AppendLine("- If an archive stays protected, try signature recovery or provide a sample for format-specific support.");
                File.WriteAllText(Path.Combine(outputDir, "GameAssetTool-wolf-diagnostics.txt"), report.ToString(), new UTF8Encoding(false));
            }
            catch { }
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
            return CopyLooseFiles(relativeRoot, files, outputDir, prefix, null, false);
        }

        private NwjsCopyStats CopyLooseFiles(string relativeRoot, IEnumerable<string> files, string outputDir, string prefix, List<string> extractedPaths)
        {
            return CopyLooseFiles(relativeRoot, files, outputDir, prefix, extractedPaths, false);
        }

        private NwjsCopyStats CopyLooseFiles(string relativeRoot, IEnumerable<string> files, string outputDir, string prefix, List<string> extractedPaths, bool overwriteExisting)
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
                    string originalRelative = MakeRelativePath(relativeRoot, source);
                    string relative = HiddenMediaExtensions.NormalizeRelativePath(originalRelative, source);
                    string originalOutputRelative = string.IsNullOrWhiteSpace(prefix) ? originalRelative : Path.Combine(prefix, originalRelative);
                    string outputRelative = string.IsNullOrWhiteSpace(prefix) ? relative : Path.Combine(prefix, relative);
                    DeleteStaleRenamedOutput(outputDir, originalOutputRelative, outputRelative);
                    string destination = GetSafeOutputPath(outputDir, outputRelative);
                    bool collision = false;
                    if (!overwriteExisting)
                        destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination, overwriteExisting);
                    extracted++;
                    bytes += SafeFileLength(destination);
                    if (collision) renamed++;
                    if (extractedPaths != null) extractedPaths.Add(destination);
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
            return ExtractZipArchive(archivePath, outputDir, prefix, null, null, false);
        }

        private NwjsCopyStats ExtractZipArchive(string archivePath, string outputDir, string prefix, Func<string, bool> includeFile, List<string> extractedPaths)
        {
            return ExtractZipArchive(archivePath, outputDir, prefix, includeFile, extractedPaths, false);
        }

        private NwjsCopyStats ExtractZipArchive(string archivePath, string outputDir, string prefix, Func<string, bool> includeFile, List<string> extractedPaths, bool overwriteExisting)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            List<string> diagnostics = new List<string>();
            using (FileStream stream = File.OpenRead(archivePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ArchiveSafetyPolicy.ValidateZipArchive(archive);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    ThrowIfLocalCopyCancelled();
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        skipped++;
                        diagnostics.Add(DiagnosticZipLine(archivePath, entry.FullName, "directory entry skipped"));
                        continue;
                    }
                    if (includeFile != null && !includeFile(entry.FullName))
                        continue;

                    string normalized;
                    try
                    {
                        normalized = ArchiveSafetyPolicy.NormalizeEntryPath(entry.FullName);
                    }
                    catch (InvalidDataException ex)
                    {
                        skipped++;
                        SafeLog("Archive entry skipped: " + entry.FullName + ": " + ex.Message);
                        diagnostics.Add(DiagnosticZipLine(archivePath, entry.FullName, ex.Message));
                        continue;
                    }
                    string outputRelative = HiddenMediaExtensions.NormalizeRelativePath(normalized, entry);
                    DeleteStaleRenamedOutput(outputDir, Path.Combine(prefix, normalized), Path.Combine(prefix, outputRelative));
                    string destination = GetSafeOutputPath(outputDir, Path.Combine(prefix, outputRelative));
                    bool collision = false;
                    if (!overwriteExisting)
                        destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    try
                    {
                        using (Stream input = entry.Open())
                        using (FileStream output = File.Create(destination))
                            ArchiveSafetyPolicy.CopyLimited(input, output, entry.Length);
                        extracted++;
                        bytes += SafeFileLength(destination);
                        if (collision) renamed++;
                        if (extractedPaths != null) extractedPaths.Add(destination);
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        diagnostics.Add(DiagnosticZipLine(archivePath, entry.FullName, ex.Message));
                        SafeLog("Archive entry skipped: " + entry.FullName + ": " + ex.Message);
                        try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                    }
                }
            }
            WriteZipDiagnostics(outputDir, diagnostics);
            return new NwjsCopyStats(extracted, bytes, renamed, skipped);
        }

        private static string DiagnosticZipLine(string archivePath, string entry, string message)
        {
            return archivePath + "\t" + entry + "\t" + (message ?? "").Replace("\r", " ").Replace("\n", " ");
        }

        private static void WriteZipDiagnostics(string outputDir, List<string> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0) return;
            try
            {
                string path = Path.Combine(outputDir, "GameAssetTool-zip-diagnostics.tsv");
                List<string> lines = new List<string>();
                if (File.Exists(path)) lines.AddRange(File.ReadAllLines(path, Encoding.UTF8));
                else lines.Add("archive\tentry\tmessage");
                lines.AddRange(diagnostics);
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        private static void DeleteStaleRenamedOutput(string outputDir, string originalRelative, string normalizedRelative)
        {
            if (string.Equals(originalRelative, normalizedRelative, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                string stale = GetSafeOutputPath(outputDir, originalRelative);
                if (File.Exists(stale)) File.Delete(stale);
            }
            catch { }
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
            if (!TryResetExtractionRootForOutput(outputDir)) return;
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
            int skipped = 0;

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
                    skipped = ParseInt(line, 5);
                }
                else
                {
                    SafeLog(line);
                }
            });

            if (exitCode != 0 && errors == 0) errors = 1;
            return new OperationResult(engineName, outputDir, extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
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
            if (localExtractionContext != null) localExtractionContext.Cancel();
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
            svgPreviewRenderer.Cancel();
            cancelButton.Enabled = false;
            statusLabel.Text = T("Stopping...", "Остановка...");
            WriteLog("Stop requested.");
        }

        private void UpdateUiFromRun()
        {
            if (currentRun == null)
            {
                if (externalRunning) UpdateExternalOperationUi();
                return;
            }
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

        private void UpdateExternalOperationUi()
        {
            if (!externalRunning) return;

            DateTime now = DateTime.UtcNow;
            int outputFiles;
            long outputBytes;
            string lastFile;
            lock (externalOutputSync)
            {
                outputFiles = externalOutputFiles;
                outputBytes = externalOutputBytes;
                lastFile = externalLastFile;
            }
            double elapsedSeconds = Math.Max((now - externalStartUtc).TotalSeconds, 0);
            string progressText = progressBar.Maximum > 1
                ? progressBar.Value + " / " + progressBar.Maximum
                : T("working", "работает");
            statusLabel.Text = externalOperationName + ": " + progressText
                + T(" | Elapsed: ", " | Время: ") + FormatDuration(elapsedSeconds);

            string outputText = T("Output: ", "Результат: ")
                + outputFiles + T(" file(s), ", " файл(ов), ")
                + FormatBytes(outputBytes);
            if (!string.IsNullOrWhiteSpace(lastFile))
                outputText += T(" | Last: ", " | Последний: ") + lastFile;
            statsLabel.Text = outputText;
        }

        private void StartExternalOutputWatcher()
        {
            StopExternalOutputWatcher();
            lock (externalOutputSync)
            {
                externalOutputFileSizes.Clear();
                externalOutputFiles = 0;
                externalOutputBytes = 0;
                externalLastFile = "";
            }

            if (string.IsNullOrWhiteSpace(lastOutputDir)) return;
            try
            {
                Directory.CreateDirectory(lastOutputDir);
                FileSystemWatcher watcher = new FileSystemWatcher(lastOutputDir)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite
                };
                watcher.Created += delegate(object sender, FileSystemEventArgs e) { TrackExternalOutputFile(e.FullPath); };
                watcher.Changed += delegate(object sender, FileSystemEventArgs e) { TrackExternalOutputFile(e.FullPath); };
                watcher.Renamed += delegate(object sender, RenamedEventArgs e) { TrackExternalOutputFile(e.FullPath); };
                watcher.EnableRaisingEvents = true;
                externalOutputWatcher = watcher;
            }
            catch { }
        }

        private void StopExternalOutputWatcher()
        {
            FileSystemWatcher watcher = externalOutputWatcher;
            externalOutputWatcher = null;
            if (watcher == null) return;
            try { watcher.EnableRaisingEvents = false; }
            catch { }
            watcher.Dispose();
        }

        private void TrackExternalOutputFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path)) return;
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists) return;
                lock (externalOutputSync)
                {
                    long previous;
                    if (externalOutputFileSizes.TryGetValue(path, out previous))
                    {
                        externalOutputBytes += info.Length - previous;
                        externalOutputFileSizes[path] = info.Length;
                    }
                    else
                    {
                        externalOutputFileSizes[path] = info.Length;
                        externalOutputFiles++;
                        externalOutputBytes += info.Length;
                    }
                    externalLastFile = info.Name;
                }
            }
            catch { }
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

        private bool TryResetExtractionRootForOutput(string outputDir)
        {
            try
            {
                string fullOutput = Path.GetFullPath(outputDir);
                DirectoryInfo output = new DirectoryInfo(fullOutput);
                DirectoryInfo extractedRoot = output;
                while (extractedRoot != null && !extractedRoot.Name.Equals("extracted", StringComparison.OrdinalIgnoreCase))
                    extractedRoot = extractedRoot.Parent;

                if (extractedRoot == null || extractedRoot.Parent == null)
                {
                    if (Directory.Exists(fullOutput))
                        Directory.Delete(fullOutput, true);
                    Directory.CreateDirectory(fullOutput);
                    return true;
                }

                if (extractedRoot.Exists)
                {
                    DialogResult answer = MessageBox.Show(
                        "Existing extracted folder will be deleted before extraction:\n\n"
                        + extractedRoot.FullName
                        + "\n\nContinue?",
                        "Clean extracted",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (answer != DialogResult.Yes)
                    {
                        WriteLog("Extraction cancelled before clearing extracted folder.");
                        return false;
                    }

                    WriteLog("Clearing previous extracted folder: " + extractedRoot.FullName);
                    try
                    {
                        DeleteDirectoryRobust(extractedRoot.FullName);
                    }
                    catch (Exception deleteError)
                    {
                        List<string> lockedFiles = FindLockedFiles(extractedRoot.FullName, 8);
                        string details = lockedFiles.Count > 0
                            ? "\n\nPossible locked files:\n" + string.Join("\n", lockedFiles.ToArray()) + (lockedFiles.Count >= 8 ? "\n..." : "")
                            : "";
                        MessageBox.Show(
                            "Could not clear existing extracted folder:\n"
                            + deleteError.Message
                            + details,
                            "Extraction",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        WriteLog("Could not clear extracted folder: " + deleteError.Message);
                        return false;
                    }
                }

                Directory.CreateDirectory(fullOutput);
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("Could not clear extracted folder: " + ex.Message);
                MessageBox.Show(
                    "Could not clear existing extracted folder:\n" + ex.Message,
                    "Extraction",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        private static void DeleteDirectoryRobust(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            foreach (string entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(entry, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(ToLongPath(path), true);
        }

        private static string ToLongPath(string path)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) return full;
            if (full.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + full.Substring(2);
            return @"\\?\" + full;
        }
        private static List<string> FindLockedFiles(string folder, int limit)
        {
            List<string> locked = new List<string>();
            if (!Directory.Exists(folder)) return locked;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories); }
            catch { return locked; }

            foreach (string file in files)
            {
                if (locked.Count >= limit) break;
                try
                {
                    using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    locked.Add(file);
                }
                catch (IOException)
                {
                    locked.Add(file);
                }
            }
            return locked;
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
            string reportText = result.ToReport();
            string htmlReportPath = SaveHtmlReport(result, reportText);
            lastOperationResult = result;
            lastReportText = reportText;
            lastReportPath = htmlReportPath;
            lastResultButton.Enabled = File.Exists(lastReportPath);
            UpdateActionTooltips();
            if (!string.IsNullOrWhiteSpace(htmlReportPath))
                WriteLog("HTML report: " + htmlReportPath);
            int galleryFiles = ResultsGalleryForm.PrepareIndexCache(result.OutputDir);
            if (galleryFiles > 0) WriteLog("Gallery index: " + galleryFiles + " file(s)");
            using (ResultsDialog dialog = new ResultsDialog(result, htmlReportPath, reportText, russianUi, RunApkFollowup))
                dialog.ShowDialog(this);
        }

        private void RunApkFollowup(ApkFollowupAction action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.TargetPath))
                return;
            if (!Directory.Exists(action.TargetPath) && !File.Exists(action.TargetPath))
            {
                MessageBox.Show(
                    "Suggested APK follow-up path was not found:\n" + action.TargetPath,
                    "APK follow-up",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            selectedExtractionProfileIndex = 0;
            BuildModeMenus();
            UpdateModeButtonTexts();

            pathBox.Text = action.TargetPath;
            WriteLog("APK follow-up: " + action.EngineKey + " -> " + action.TargetPath);
            BeginInvoke((MethodInvoker)async delegate { await StartDetectedExtractionAsync(); });
        }

        private static void DeleteTextReport(string outputDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputDir)) return;
                string path = Path.Combine(outputDir, "GameAssetTool-report.txt");
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static void DeleteUnityDiagnosticsText(string outputDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputDir)) return;
                string path = Path.Combine(outputDir, "GameAssetTool-unity-diagnostics.txt");
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }
        private static string SaveHtmlReport(OperationResult result, string reportText)
        {
            try
            {
                Directory.CreateDirectory(result.OutputDir);
                string htmlPath = Path.Combine(result.OutputDir, "GameAssetTool-report.html");
                string text = string.IsNullOrWhiteSpace(reportText) ? result.ToReport() : reportText;
                File.WriteAllText(htmlPath, ExtractionReportBuilder.BuildHtml(text, result.OutputDir), new UTF8Encoding(false));
                DeleteTextReport(result.OutputDir);
                DeleteUnityDiagnosticsText(result.OutputDir);
                return htmlPath;
            }
            catch
            {
                return "";
            }
        }

    }
}
