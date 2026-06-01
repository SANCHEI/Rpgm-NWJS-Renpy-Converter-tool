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
                return RunCollectorExtraction("Loose resources", inputPath, outputDir, files);
            });
        }

        private async Task StartSignatureRecoveryAsync()
        {
            await StartLocalExtractionAsync("Signature recovery", "signature-recovery", delegate(string inputPath, string outputDir)
            {
                DateTime start = DateTime.UtcNow;
                CollectorResult extracted = SignatureAssetExtractor.Extract(inputPath, outputDir);
                string report = AssetCollectors.WriteDiagnostics(inputPath, outputDir);
                SafeLog("Unknown-engine diagnostics: " + report);
                return new OperationResult("Unknown format signature recovery", outputDir, extracted.Extracted, extracted.Bytes, 0, extracted.Renamed, extracted.Skipped, DateTime.UtcNow - start);
            });
        }

        private OperationResult RunCollectorExtraction(string engineName, string inputPath, string outputDir, List<string> files)
        {
            DateTime start = DateTime.UtcNow;
            string rootPath = InputDirectory(inputPath);
            CollectorResult copied = AssetCollectors.CopyFiles(rootPath, files, outputDir, "");
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
                    string relative = MakeRelativePath(relativeRoot, source);
                    string destination = GetSafeOutputPath(outputDir, string.IsNullOrWhiteSpace(prefix) ? relative : Path.Combine(prefix, relative));
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
                        continue;
                    }
                    string destination = GetSafeOutputPath(outputDir, Path.Combine(prefix, normalized));
                    bool collision = false;
                    if (!overwriteExisting)
                        destination = GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream input = entry.Open())
                    using (FileStream output = File.Create(destination))
                        ArchiveSafetyPolicy.CopyLimited(input, output, entry.Length);
                    extracted++;
                    bytes += SafeFileLength(destination);
                    if (collision) renamed++;
                    if (extractedPaths != null) extractedPaths.Add(destination);
                }
            }
            return new NwjsCopyStats(extracted, bytes, renamed, skipped);
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

    }
}
