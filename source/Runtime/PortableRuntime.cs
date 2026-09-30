using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class PortableRuntime
    {
        private const string ResourceName = "RpgmvpConverterWinForms.runtime.runtime-win-x64.zip";

        // Keep in sync with $runtimeVersion in source/scripts/build_portable_runtime.ps1.
        // Changing dependencies there must bump this string so the disk cache invalidates.
        private const string RuntimeVersion = "python-3.12.10-unrpa-2.3.0-unitypy-1.25.0-pyuepak-0.2.7-zstandard-0.25.0-pycryptodome-3.23.0-oozextract-0.5.4-win-x64-v11";

        // Pack layout (built by build_portable_runtime.ps1, published as release assets):
        //   base   - interpreter + unrpa (Renpy) + Pillow (XP3/GameMaker) +
        //            pyuepak with import-time deps lz4/zstandard (Godot AES + Unreal)
        //   unity  - UnityPy + texture/audio codec wheels (Unity only)
        //   spite  - brotli + pycryptodome (SPITE only)
        // Shared tiny wheels (brotli) are duplicated across packs on purpose:
        // overlaying is idempotent, correctness beats a few hundred KB.
        public const string PackBase = "base";
        public const string PackUnity = "unity";
        public const string PackSpite = "spite";

        private const string PackDownloadBaseUrl = "https://github.com/SANCHEI/Rpgm-NWJS-Renpy-Converter-tool/releases/download/runtime-v11/";

        private static readonly object Sync = new object();
        private static string runtimeDirectory;

        public static bool IsReady
        {
            get
            {
                lock (Sync)
                {
                    string pythonPath = GetPythonPath();
                    return !string.IsNullOrWhiteSpace(pythonPath) && File.Exists(pythonPath);
                }
            }
        }

        public static bool HasEmbeddedRuntime
        {
            get
            {
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using (Stream resource = assembly.GetManifestResourceStream(ResourceName))
                    {
                        return resource != null;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string EnsureExtracted()
        {
            lock (Sync)
            {
                string pythonPath = GetPythonPath();
                if (!string.IsNullOrWhiteSpace(pythonPath) && File.Exists(pythonPath))
                    return pythonPath;

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string runtimeRoot = Path.Combine(localAppData, "GameAssetTool", "runtime");
                TryDeleteAbandonedSessions(runtimeRoot);
                runtimeDirectory = Path.Combine(
                    runtimeRoot,
                    "session-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N"));

                try
                {
                    Directory.CreateDirectory(runtimeDirectory);
                    if (HasEmbeddedRuntime)
                    {
                        ExtractEmbeddedResource(runtimeDirectory);
                    }
                    else
                    {
                        PopulateFromCacheOrDownload(runtimeDirectory);
                    }
                    pythonPath = GetPythonPath();
                    if (!File.Exists(pythonPath))
                        throw new InvalidDataException("Python runtime is incomplete (python.exe not found).");
                    return pythonPath;
                }
                catch
                {
                    Cleanup();
                    throw;
                }
            }
        }

        public static string CreateSessionFilePath(string fileName)
        {
            EnsureExtracted();
            string path = Path.GetFullPath(Path.Combine(runtimeDirectory, fileName));
            EnsureInsideRuntime(path);
            return path;
        }

        public static ProcessStartInfo CreatePythonProcessInfo()
        {
            string pythonPath = EnsureExtracted();
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                WorkingDirectory = runtimeDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["PATH"] = runtimeDirectory + ";" + psi.EnvironmentVariables["PATH"];
            psi.EnvironmentVariables["PYTHONHOME"] = runtimeDirectory;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            return psi;
        }

        public static void Cleanup()
        {
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(runtimeDirectory))
                    return;

                string sessionDirectory = runtimeDirectory;
                runtimeDirectory = null;
                TryDeleteDirectory(sessionDirectory);

                string runtimeRoot = Path.GetDirectoryName(sessionDirectory);
                TryDeleteDirectoryIfEmpty(runtimeRoot);
                TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(runtimeRoot));
            }
        }

        private static string GetPythonPath()
        {
            return string.IsNullOrWhiteSpace(runtimeDirectory)
                ? null
                : Path.Combine(runtimeDirectory, "python.exe");
        }

        private static void ExtractEmbeddedResource(string destinationRoot)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream(ResourceName))
            {
                if (resource == null)
                    throw new InvalidOperationException("Embedded Python runtime was not found.");
                ExtractZipStream(resource, destinationRoot);
            }
        }

        public static void EnsurePacks(params string[] packs)
        {
            lock (Sync)
            {
                if (HasEmbeddedRuntime)
                {
                    // Full build embeds the combined runtime: every pack is present.
                    EnsureExtracted();
                    return;
                }
                string cacheRoot = GetVersionedCacheDirectory();
                EnsureBasePackInCache(cacheRoot);
                List<string> applied = ReadAppliedPacks(cacheRoot);
                bool changed = false;
                if (packs != null)
                {
                    for (int i = 0; i < packs.Length; i++)
                    {
                        string pack = packs[i] == null ? "" : packs[i].ToLowerInvariant();
                        if (pack == "" || pack == PackBase || applied.Contains(pack))
                            continue;
                        ValidatePackName(pack);
                        DownloadAndOverlayPack(cacheRoot, pack);
                        applied.Add(pack);
                        changed = true;
                    }
                }
                if (changed)
                {
                    WriteAppliedPacks(cacheRoot, applied);
                    if (!string.IsNullOrWhiteSpace(runtimeDirectory) && Directory.Exists(runtimeDirectory))
                        CopyDirectory(cacheRoot, runtimeDirectory);
                }
            }
        }

        private static void ValidatePackName(string pack)
        {
            if (pack != PackUnity && pack != PackSpite)
                throw new InvalidOperationException("Unknown runtime pack: " + pack);
        }

        private static string PackAssetName(string pack)
        {
            return "runtime-" + pack + "-win-x64.zip";
        }

        private static void PopulateFromCacheOrDownload(string destinationRoot)
        {
            string cacheRoot = GetVersionedCacheDirectory();
            EnsureBasePackInCache(cacheRoot);
            CopyDirectory(cacheRoot, destinationRoot);
            // Cache bookkeeping files must not pollute the session.
            TryDeleteFile(Path.Combine(destinationRoot, "GameAssetTool-runtime.txt"));
            TryDeleteFile(Path.Combine(destinationRoot, "packs-applied.txt"));
        }

        private static void EnsureBasePackInCache(string cacheRoot)
        {
            if (!IsValidCache(cacheRoot))
            {
                string zipPath = DownloadPackZip(PackBase, "Python runtime base pack (Renpy/XP3/GameMaker/Godot engines)");
                try
                {
                    TryDeleteDirectory(cacheRoot);
                    Directory.CreateDirectory(cacheRoot);
                    ExtractZipFile(zipPath, cacheRoot);
                    WriteCacheMarker(cacheRoot);
                    WriteAppliedPacks(cacheRoot, new List<string>(new string[] { PackBase }));
                }
                finally
                {
                    TryDeleteFile(zipPath);
                }
            }
        }

        private static void DownloadAndOverlayPack(string cacheRoot, string pack)
        {
            string what;
            if (pack == PackUnity) what = "Unity engine pack (UnityPy + codecs)";
            else what = "SPITE engine pack (brotli + pycryptodome)";
            string zipPath = DownloadPackZip(pack, what);
            try
            {
                ExtractZipFile(zipPath, cacheRoot);
            }
            finally
            {
                TryDeleteFile(zipPath);
            }
        }

        private static List<string> ReadAppliedPacks(string cacheRoot)
        {
            List<string> result = new List<string>();
            try
            {
                string marker = Path.Combine(cacheRoot, "packs-applied.txt");
                if (!File.Exists(marker)) return result;
                foreach (string line in File.ReadAllLines(marker, Encoding.ASCII))
                {
                    string pack = line.Trim().ToLowerInvariant();
                    if (pack != "" && !result.Contains(pack))
                        result.Add(pack);
                }
            }
            catch { }
            return result;
        }

        private static void WriteAppliedPacks(string cacheRoot, List<string> packs)
        {
            try { File.WriteAllLines(Path.Combine(cacheRoot, "packs-applied.txt"), packs.ToArray(), Encoding.ASCII); }
            catch { }
        }

        private static string GetVersionedCacheDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "GameAssetTool", "runtime-cache", RuntimeVersion);
        }

        private static bool IsValidCache(string cacheRoot)
        {
            try
            {
                if (!Directory.Exists(cacheRoot)) return false;
                if (!File.Exists(Path.Combine(cacheRoot, "python.exe"))) return false;
                if (!File.Exists(Path.Combine(cacheRoot, "GameAssetTool-runtime.txt"))) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteCacheMarker(string cacheRoot)
        {
            try { File.WriteAllText(Path.Combine(cacheRoot, "GameAssetTool-runtime.txt"), RuntimeVersion, Encoding.ASCII); }
            catch { }
        }

        private static string DownloadPackZip(string pack, string what)
        {
            string url = PackDownloadBaseUrl + PackAssetName(pack);
            string tempPath = Path.Combine(Path.GetTempPath(), "GameAssetTool-" + pack + "-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "GameAssetTool");
                    client.DownloadFile(url, tempPath);
                }
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempPath);
                throw new InvalidOperationException(
                    "This Lite build does not embed the Python runtime, and downloading " + what + " failed. " +
                    "Check your internet connection, or use the Full release (GameAssetTool-v*.exe) which works offline. " +
                    "Details: " + ex.Message);
            }
            VerifyDownloadHash(tempPath, url + ".sha256");
            return tempPath;
        }

        private static void VerifyDownloadHash(string zipPath, string sidecarUrl)
        {
            // Optional sidecar: maintainers should publish runtime-<pack>-win-x64.zip.sha256
            // next to the zip. HTTPS + maintainer-controlled release is the baseline; when the
            // sidecar exists its hash is enforced strictly.
            string expected = null;
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "GameAssetTool");
                    string text = client.DownloadString(sidecarUrl);
                    if (!string.IsNullOrWhiteSpace(text))
                        expected = text.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                }
            }
            catch
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(expected)) return;
            string actual;
            using (FileStream stream = File.OpenRead(zipPath))
            using (SHA256 sha = SHA256.Create())
            {
                actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(zipPath);
                throw new InvalidOperationException("Downloaded Python runtime failed SHA-256 verification and was discarded.");
            }
        }

        private static void ExtractZipStream(Stream resource, string destinationRoot)
        {
            using (ZipArchive archive = new ZipArchive(resource, ZipArchiveMode.Read))
            {
                ExtractArchiveEntries(archive, destinationRoot);
            }
        }

        private static void ExtractZipFile(string zipPath, string destinationRoot)
        {
            using (FileStream stream = File.OpenRead(zipPath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ExtractArchiveEntries(archive, destinationRoot);
            }
        }

        private static void ExtractArchiveEntries(ZipArchive archive, string destinationRoot)
        {
            string root = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Runtime archive contains an unsafe path.");
                EnsureInsideRuntimeForRoot(destination, root);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                using (Stream input = entry.Open())
                using (FileStream output = File.Create(destination))
                    input.CopyTo(output);
            }
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            string source = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (string directory in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
            }
            foreach (string file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string destination = Path.Combine(destinationRoot, relative);
                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);
                File.Copy(file, destination, true);
            }
        }

        private static void EnsureInsideRuntime(string path)
        {
            string root = Path.GetFullPath(runtimeDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            EnsureInsideRuntimeForRoot(path, root);
        }

        private static void EnsureInsideRuntimeForRoot(string path, string root)
        {
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded runtime contains an unsafe path.");
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

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }

        private static void TryDeleteDirectoryIfEmpty(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path)
                    && Directory.Exists(path)
                    && Directory.GetFileSystemEntries(path).Length == 0)
                {
                    Directory.Delete(path);
                }
            }
            catch { }
        }

        private static void TryDeleteAbandonedSessions(string runtimeRoot)
        {
            try
            {
                if (!Directory.Exists(runtimeRoot))
                    return;

                foreach (string directory in Directory.GetDirectories(runtimeRoot, "session-*", SearchOption.TopDirectoryOnly))
                {
                    string[] parts = Path.GetFileName(directory).Split('-');
                    int processId;
                    if (parts.Length < 3 || !int.TryParse(parts[1], out processId) || !IsProcessRunning(processId))
                        TryDeleteDirectory(directory);
                }

                TryDeleteDirectoryIfEmpty(runtimeRoot);
                TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(runtimeRoot));
            }
            catch { }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                    return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
