using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace RpgmvpConverterWinForms
{
    internal static class ToolRuntime
    {
        private const string WolfCliResourceName = "RpgmvpConverterWinForms.tools.UberWolfCli.exe";
        private const string WolfCliSha256 = "FFFBE66CAF10699865010217AEABE3A3684EC9320FFE461268F1C9509FDA8917";
        private const string ResvgResourceName = "RpgmvpConverterWinForms.tools.resvg.exe";
        private const string ResvgSha256 = "433A7C744CFF561ED64FCF73C7C04E239D7A07AE5F0AADBF1BA8471D63707402";
        private static readonly object Sync = new object();
        private static string runtimeDirectory;

        public static string EnsureWolfCliExtracted()
        {
            lock (Sync)
            {
                EnsureRuntimeDirectory();
                string path = Path.Combine(runtimeDirectory, "UberWolfCli.exe");
                if (!File.Exists(path))
                    ExtractResource(WolfCliResourceName, path);
                ValidateSha256(path, WolfCliSha256);
                return path;
            }
        }

        public static string EnsureResvgExtracted()
        {
            lock (Sync)
            {
                EnsureRuntimeDirectory();
                string path = Path.Combine(runtimeDirectory, "resvg.exe");
                if (!File.Exists(path))
                    ExtractResource(ResvgResourceName, path);
                ValidateSha256(path, ResvgSha256);
                return path;
            }
        }

        public static string CreateSessionDirectory(string name)
        {
            lock (Sync)
            {
                EnsureRuntimeDirectory();
                string path = Path.GetFullPath(Path.Combine(runtimeDirectory, name));
                EnsureInsideRuntime(path);
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static void DeleteSessionDirectory(string path)
        {
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                string fullPath = Path.GetFullPath(path);
                EnsureInsideRuntime(fullPath);
                TryDeleteDirectory(fullPath);
            }
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

        private static void EnsureRuntimeDirectory()
        {
            if (!string.IsNullOrWhiteSpace(runtimeDirectory) && Directory.Exists(runtimeDirectory))
                return;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string runtimeRoot = Path.Combine(localAppData, "GameAssetTool", "tools");
            TryDeleteAbandonedSessions(runtimeRoot);
            runtimeDirectory = Path.Combine(
                runtimeRoot,
                "session-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runtimeDirectory);
        }

        private static void ExtractResource(string resourceName, string destination)
        {
            EnsureInsideRuntime(destination);
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream(resourceName))
            {
                if (resource == null)
                    throw new InvalidOperationException("Embedded tool was not found: " + resourceName);

                using (FileStream output = File.Create(destination))
                    resource.CopyTo(output);
            }
        }

        private static void ValidateSha256(string path, string expected)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Embedded tool checksum mismatch: " + Path.GetFileName(path));
            }
        }

        private static void EnsureInsideRuntime(string path)
        {
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
                throw new InvalidOperationException("Embedded tool runtime is not initialized.");

            string root = Path.GetFullPath(runtimeDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded tool path escapes its temporary folder.");
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
