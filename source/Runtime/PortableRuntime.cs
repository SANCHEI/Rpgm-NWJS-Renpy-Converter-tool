using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class PortableRuntime
    {
        private const string ResourceName = "RpgmvpConverterWinForms.runtime.runtime-win-x64.zip";
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
                    ExtractResource(runtimeDirectory);
                    pythonPath = GetPythonPath();
                    if (!File.Exists(pythonPath))
                        throw new InvalidDataException("Embedded Python runtime is incomplete.");
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

        private static void ExtractResource(string destinationRoot)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream(ResourceName))
            {
                if (resource == null)
                    throw new InvalidOperationException("Embedded Python runtime was not found.");

                using (ZipArchive archive = new ZipArchive(resource, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string destination = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
                        EnsureInsideRuntime(destination);

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
            }
        }

        private static void EnsureInsideRuntime(string path)
        {
            string root = Path.GetFullPath(runtimeDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded runtime contains an unsafe path.");
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
