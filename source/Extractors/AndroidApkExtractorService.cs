using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class AndroidApkExtractorService
    {
        private readonly Func<bool> isCancelled;
        private readonly Action<string> log;
        private readonly Action<int, int, long> progress;

        public AndroidApkExtractorService(Func<bool> isCancelled, Action<string> log, Action<int, int, long> progress)
        {
            this.isCancelled = isCancelled ?? delegate { return false; };
            this.log = log ?? delegate { };
            this.progress = progress ?? delegate { };
        }

        public ApkExtractionResult Extract(string inputPath, string outputDir)
        {
            DateTime start = DateTime.UtcNow;
            Directory.CreateDirectory(outputDir);
            List<string> archives = AssetCollectors.FindInputFiles(inputPath, ".apk")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            int extracted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;
            int skipped = 0;
            int total = Math.Max(archives.Count, 1);

            for (int i = 0; i < archives.Count; i++)
            {
                ThrowIfCancelled();
                string archive = archives[i];
                try
                {
                    string apkPrefix = Path.Combine("apk", Path.GetFileNameWithoutExtension(archive));
                    WriteApkDiagnostics(archive, outputDir, apkPrefix);
                    CopyStats zip = ExtractZipArchive(archive, outputDir, apkPrefix);
                    extracted += zip.Extracted;
                    bytes += zip.Bytes;
                    renamed += zip.Renamed;
                    skipped += zip.Skipped;

                    string signatureOutput = Path.Combine(outputDir, "signature-recovery", Path.GetFileNameWithoutExtension(archive));
                    CollectorResult carved = SignatureAssetExtractor.Extract(archive, signatureOutput, isCancelled);
                    extracted += carved.Extracted;
                    bytes += carved.Bytes;
                    renamed += carved.Renamed;
                    skipped += carved.Skipped;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors++;
                    log("WARN:" + archive + ":" + ex.Message);
                }
                progress(i + 1, total, bytes);
            }

            if (archives.Count == 0) skipped++;
            return new ApkExtractionResult(extracted, bytes, errors, renamed, skipped, DateTime.UtcNow - start);
        }

        private void WriteApkDiagnostics(string archivePath, string outputDir, string extractedPrefix)
        {
            try
            {
                string reportPath = Path.Combine(outputDir, "GameAssetTool-apk-diagnostics.txt");
                string report = ApkDiagnosticBuilder.BuildReport(archivePath, extractedPrefix.Replace('\\', '/'));
                if (File.Exists(reportPath))
                    File.AppendAllText(reportPath, Environment.NewLine + report, new UTF8Encoding(false));
                else
                    File.WriteAllText(reportPath, report, new UTF8Encoding(false));
            }
            catch { }
        }

        private CopyStats ExtractZipArchive(string archivePath, string outputDir, string prefix)
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
                    ThrowIfCancelled();
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        skipped++;
                        diagnostics.Add(DiagnosticZipLine(archivePath, entry.FullName, "directory entry skipped"));
                        continue;
                    }
                    if (!IsArchiveAssetFile(entry.FullName))
                        continue;

                    string normalized;
                    try
                    {
                        normalized = ArchiveSafetyPolicy.NormalizeEntryPath(entry.FullName);
                    }
                    catch (InvalidDataException ex)
                    {
                        skipped++;
                        diagnostics.Add(DiagnosticZipLine(archivePath, entry.FullName, ex.Message));
                        continue;
                    }

                    string outputRelative = HiddenMediaExtensions.NormalizeRelativePath(normalized, entry);
                    string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine(prefix, outputRelative));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    try
                    {
                        using (Stream input = entry.Open())
                        using (FileStream output = File.Create(destination))
                            ArchiveSafetyPolicy.CopyLimited(input, output, entry.Length);
                        extracted++;
                        bytes += SafeFileLength(destination);
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        diagnostics.Add(DiagnosticZipLine(archivePath, entry.FullName, ex.Message));
                        log("Archive entry skipped: " + entry.FullName + ": " + ex.Message);
                        try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                    }
                }
            }
            WriteZipDiagnostics(outputDir, diagnostics);
            return new CopyStats(extracted, bytes, renamed, skipped);
        }

        private static bool IsArchiveAssetFile(string relativePath)
        {
            string extension = Path.GetExtension(relativePath);
            return MediaTypeRegistry.IsImageLike(extension)
                || MediaTypeRegistry.IsVideo(extension)
                || MediaTypeRegistry.IsAudio(extension)
                || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tmx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".po", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ini", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".woff", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".sspj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".assets", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bundle", StringComparison.OrdinalIgnoreCase)
                                || extension.Equals(".unity3d", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ress", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".resource", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".pck", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(relativePath).Equals("data.win", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".win", StringComparison.OrdinalIgnoreCase);
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

        private static string DiagnosticZipLine(string archivePath, string entry, string message)
        {
            return archivePath + "\t" + entry + "\t" + (message ?? "").Replace("\r", " ").Replace("\n", " ");
        }

        private void ThrowIfCancelled()
        {
            if (isCancelled()) throw new OperationCanceledException();
        }

        private static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private sealed class CopyStats
        {
            public CopyStats(int extracted, long bytes, int renamed, int skipped)
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
    }

    internal sealed class ApkExtractionResult
    {
        public ApkExtractionResult(int extracted, long bytes, int errors, int renamed, int skipped, TimeSpan duration)
        {
            Extracted = extracted;
            Bytes = bytes;
            Errors = errors;
            Renamed = renamed;
            Skipped = skipped;
            Duration = duration;
        }

        public int Extracted { get; private set; }
        public long Bytes { get; private set; }
        public int Errors { get; private set; }
        public int Renamed { get; private set; }
        public int Skipped { get; private set; }
        public TimeSpan Duration { get; private set; }
    }
}
