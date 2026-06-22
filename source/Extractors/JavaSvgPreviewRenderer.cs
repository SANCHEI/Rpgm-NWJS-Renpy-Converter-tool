using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RpgmvpConverterWinForms
{
    internal sealed class JavaSvgPreviewRenderer
    {
        private const int RendererPreflightTimeoutMs = 5000;
        private const int RendererFileTimeoutMs = 20000;
        private readonly Func<bool> cancellationRequested;
        private readonly Action throwIfCancelled;
        private readonly Action<string> log;
        private readonly Action<string, int, int, long> updateProgress;
        private readonly object processSync = new object();
        private readonly List<Process> activeProcesses = new List<Process>();

        public JavaSvgPreviewRenderer(
            Func<bool> cancellationRequested,
            Action throwIfCancelled,
            Action<string> log,
            Action<string, int, int, long> updateProgress)
        {
            this.cancellationRequested = cancellationRequested;
            this.throwIfCancelled = throwIfCancelled;
            this.log = log;
            this.updateProgress = updateProgress;
        }

        public SvgPreviewResult Render(IEnumerable<string> extractedPaths, string outputDir)
        {
            List<string> svgFiles = extractedPaths
                .Where(delegate(string path) { return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (svgFiles.Count == 0) return new SvgPreviewResult(0, 0, 0, 0, 0);

            string renderer;
            try
            {
                renderer = ToolRuntime.EnsureResvgExtracted();
                if (!CanRunRenderer(renderer))
                {
                    log("WARN:SVG preview: embedded resvg.exe is unavailable or blocked by Windows security policy. SVG originals will be kept without PNG previews.");
                    return new SvgPreviewResult(0, 0, 1, 0, svgFiles.Count);
                }
            }
            catch (Exception ex)
            {
                log("WARN:SVG preview: embedded resvg.exe could not be prepared: " + ex.Message + ". SVG originals will be kept without PNG previews.");
                return new SvgPreviewResult(0, 0, 1, 0, svgFiles.Count);
            }
            int converted = 0;
            long bytes = 0;
            int errors = 0;
            int renamed = 0;
            int skipped = 0;
            int processed = 0;
            object cacheSync = new object();
            Dictionary<string, SvgPreviewCacheEntry> cache = LoadCache(outputDir);
            log("Java SVG preview conversion started with embedded resvg: " + svgFiles.Count + " file(s)");

            Parallel.ForEach(svgFiles, new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(8, Math.Max(2, Environment.ProcessorCount))
            }, delegate(string source)
            {
                if (cancellationRequested()) return;
                string relativeSource = ExtractionPathUtils.MakeRelativePath(outputDir, source);
                string hash = ComputeSha256(source);
                SvgPreviewCacheEntry cached;
                lock (cacheSync) cache.TryGetValue(relativeSource, out cached);
                string destination = cached == null
                    ? GetPreviewDestination(source)
                    : ExtractionPathUtils.GetSafeOutputPath(outputDir, cached.PreviewPath);
                if (cached != null
                    && string.Equals(cached.SourceSha256, hash, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(destination))
                {
                    Interlocked.Increment(ref skipped);
                    int cachedCurrent = Interlocked.Increment(ref processed);
                    updateProgress("Java SVG previews", cachedCurrent, svgFiles.Count, Interlocked.Read(ref bytes));
                    return;
                }
                try
                {
                    ExtractionPathUtils.TryDeleteFile(destination);
                    int exitCode = RunRenderer(renderer, source, destination);
                    if (exitCode != 0 || !File.Exists(destination))
                        throw new InvalidDataException("resvg exited with code " + exitCode + ".");
                    Interlocked.Increment(ref converted);
                    Interlocked.Add(ref bytes, ExtractionPathUtils.SafeFileLength(destination));
                    lock (cacheSync)
                    {
                        cache[relativeSource] = new SvgPreviewCacheEntry(hash, ExtractionPathUtils.MakeRelativePath(outputDir, destination));
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationRequested()) Interlocked.Increment(ref errors);
                    ExtractionPathUtils.TryDeleteFile(destination);
                    log("WARN:SVG preview:" + source + ":" + ex.Message);
                }
                int current = Interlocked.Increment(ref processed);
                updateProgress("Java SVG previews", current, svgFiles.Count, Interlocked.Read(ref bytes));
            });
            throwIfCancelled();
            SaveCache(outputDir, cache);
            return new SvgPreviewResult(converted, bytes, errors, renamed, skipped);
        }

        public void Cancel()
        {
            lock (processSync)
            {
                foreach (Process process in activeProcesses.ToArray())
                {
                    try
                    {
                        if (!process.HasExited) process.Kill();
                    }
                    catch { }
                }
            }
        }

        private int RunRenderer(string renderer, string source, string destination)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = renderer,
                Arguments = (NeedsSystemFonts(source) ? "" : "--skip-system-fonts ")
                    + ExtractionPathUtils.QuoteArg(source) + " " + ExtractionPathUtils.QuoteArg(destination),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) log("resvg: " + e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) log("resvg ERROR: " + e.Data);
                };
                throwIfCancelled();
                lock (processSync) activeProcesses.Add(process);
                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(RendererFileTimeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("resvg did not finish within " + (RendererFileTimeoutMs / 1000) + " seconds.");
                    }
                    return process.ExitCode;
                }
                finally
                {
                    lock (processSync) activeProcesses.Remove(process);
                }
            }
        }

        private bool CanRunRenderer(string renderer)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = renderer,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = new Process { StartInfo = psi })
            {
                throwIfCancelled();
                lock (processSync) activeProcesses.Add(process);
                try
                {
                    process.Start();
                    if (!process.WaitForExit(RendererPreflightTimeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    return process.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    log("WARN:SVG preview: resvg preflight failed: " + ex.Message);
                    return false;
                }
                finally
                {
                    lock (processSync) activeProcesses.Remove(process);
                }
            }
        }

        private static string GetPreviewDestination(string source)
        {
            string destination = Path.ChangeExtension(source, ".png");
            if (!File.Exists(destination)) return destination;
            return Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + ".preview.png");
        }

        private static Dictionary<string, SvgPreviewCacheEntry> LoadCache(string outputDir)
        {
            Dictionary<string, SvgPreviewCacheEntry> cache = new Dictionary<string, SvgPreviewCacheEntry>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(outputDir, "svg-preview-cache.tsv");
            if (!File.Exists(path)) return cache;
            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] values = line.Split('\t');
                    if (values.Length == 3)
                        cache[values[0]] = new SvgPreviewCacheEntry(values[1], values[2]);
                }
            }
            catch { }
            return cache;
        }

        private static void SaveCache(string outputDir, Dictionary<string, SvgPreviewCacheEntry> cache)
        {
            string path = Path.Combine(outputDir, "svg-preview-cache.tsv");
            try
            {
                File.WriteAllLines(path, cache
                    .OrderBy(delegate(KeyValuePair<string, SvgPreviewCacheEntry> pair) { return pair.Key; }, StringComparer.OrdinalIgnoreCase)
                    .Select(delegate(KeyValuePair<string, SvgPreviewCacheEntry> pair)
                    {
                        return pair.Key + "\t" + pair.Value.SourceSha256 + "\t" + pair.Value.PreviewPath;
                    }), new UTF8Encoding(false));
            }
            catch { }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream input = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        private static bool NeedsSystemFonts(string path)
        {
            try
            {
                string svg = File.ReadAllText(path);
                return svg.IndexOf("<text", StringComparison.OrdinalIgnoreCase) >= 0
                    || svg.IndexOf("<tspan", StringComparison.OrdinalIgnoreCase) >= 0
                    || svg.IndexOf("font-family", StringComparison.OrdinalIgnoreCase) >= 0
                    || svg.IndexOf("font-size", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return true;
            }
        }

        private sealed class SvgPreviewCacheEntry
        {
            public SvgPreviewCacheEntry(string sourceSha256, string previewPath)
            {
                SourceSha256 = sourceSha256;
                PreviewPath = previewPath;
            }

            public string SourceSha256 { get; private set; }
            public string PreviewPath { get; private set; }
        }
    }

    internal sealed class SvgPreviewResult
    {
        public SvgPreviewResult(int converted, long bytes, int errors, int renamed, int skipped)
        {
            Converted = converted;
            Bytes = bytes;
            Errors = errors;
            Renamed = renamed;
            Skipped = skipped;
        }

        public int Converted { get; private set; }
        public long Bytes { get; private set; }
        public int Errors { get; private set; }
        public int Renamed { get; private set; }
        public int Skipped { get; private set; }
    }
}
