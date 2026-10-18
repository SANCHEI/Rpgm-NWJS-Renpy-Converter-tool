using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RpgmvpConverterWinForms
{
    internal static class UnityArchiveDiscovery
    {
        private static readonly string[] ArchiveExtensions = { ".assets", ".bundle", ".unity3d" };
        private static readonly string[] BundleLikeExtensions = { ".bundle", ".unity3d" };
        private static readonly string[] AddressableHints = { "streamingassets\\aa", "streamingassets/aa", "assetbundles", "bundles" };
        private static readonly string[] SkippedFolders = { "BepInEx", "dotnet", "mono", "logs", "extracted" };

        public static bool IsArchiveLikeExtension(string extension)
        {
            return ArchiveExtensions.Any(item => item.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsBundleLikeExtension(string extension)
        {
            return BundleLikeExtensions.Any(item => item.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static List<string> FindBundleLikeArchives(string rootPath)
        {
            return EnumerateUnityFiles(rootPath)
                .Where(IsBundleLikeArchive)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        public static bool IsBundleLikeArchive(string path)
        {
            string extension = Path.GetExtension(path);
            if (IsBundleLikeExtension(extension)) return true;
            if (IsArchiveLikeExtension(extension)) return false;
            if (!ShouldProbeBundleSignature(path)) return false;
            return IsUnityBundleSignature(path);
        }

        public static bool IsUnityBundleSignature(string path)
        {
            return ProbeUnityArchiveKind(path).StartsWith("Unity", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldProbeBundleSignature(string path)
        {
            string extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension) && !extension.Equals(".resource", StringComparison.OrdinalIgnoreCase)) return false;
            string lower = path.ToLowerInvariant().Replace('/', '\\');
            if (AddressableHints.Any(hint => lower.Contains(hint.Replace('/', '\\')))) return true;
            string parent = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
            return parent.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(extension);
        }

        public static string BuildArchiveSummary(string rootPath, int maxItems)
        {
            List<FileInfo> allFiles = EnumerateUnityFiles(rootPath)
                .Where(path => IsArchiveLikeExtension(Path.GetExtension(path)) || IsBundleLikeArchive(path))
                .Select(path => new FileInfo(path))
                .Where(info => info.Exists)
                .ToList();
            List<FileInfo> files = allFiles
                .OrderByDescending(info => info.Length)
                .Take(Math.Max(1, maxItems))
                .ToList();
            if (files.Count == 0) return "";
            string preview = string.Join("; ", files.Select(info => MakeRelative(rootPath, info.FullName) + " (" + FormatBytes(info.Length) + ")").ToArray());
            string signatures = BuildSignatureSummary(allFiles);
            return string.IsNullOrWhiteSpace(signatures) ? preview : preview + " | " + signatures;
        }

        private static string BuildSignatureSummary(List<FileInfo> files)
        {
            if (files == null || files.Count == 0) return "";
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo file in files)
            {
                string kind = ProbeUnityArchiveKind(file.FullName);
                if (!counts.ContainsKey(kind)) counts[kind] = 0;
                counts[kind]++;
            }
            return "signatures: " + string.Join(", ", counts.OrderByDescending(item => item.Value).Select(item => item.Key + " x" + item.Value).ToArray());
        }

        private static string ProbeUnityArchiveKind(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] buffer = new byte[Math.Min(16, (int)Math.Min(stream.Length, 16))];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    string header = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                    if (header.StartsWith("UnityFS", StringComparison.Ordinal)) return "UnityFS";
                    if (header.StartsWith("UnityWeb", StringComparison.Ordinal)) return "UnityWeb";
                    if (header.StartsWith("UnityRaw", StringComparison.Ordinal)) return "UnityRaw";
                }
            }
            catch
            {
            }
            return Path.GetExtension(path).Equals(".assets", StringComparison.OrdinalIgnoreCase) ? "assets" : "unknown";
        }

        public static string BuildSkippedFolderSummary(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return "";
            List<string> found = new List<string>();
            foreach (string name in SkippedFolders)
            {
                try
                {
                    string path = Path.Combine(rootPath, name);
                    if (Directory.Exists(path)) found.Add(name);
                }
                catch { }
            }
            return found.Count == 0 ? "" : string.Join(", ", found.ToArray());
        }

        private static IEnumerable<string> EnumerateUnityFiles(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return Enumerable.Empty<string>();
            try
            {
                return Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                    .Where(path => !IsUnderSkippedFolder(rootPath, path))
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static bool IsUnderSkippedFolder(string rootPath, string path)
        {
            string relative = MakeRelative(rootPath, path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Any(part => SkippedFolders.Any(skip => skip.Equals(part, StringComparison.OrdinalIgnoreCase)));
        }

        private static string MakeRelative(string rootPath, string path)
        {
            try
            {
                string fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(path);
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(fullRoot.Length) : fullPath;
            }
            catch { return path; }
        }

        private static string FormatBytes(long value)
        {
            double size = Math.Max(0, value);
            string[] units = { "B", "KB", "MB", "GB" };
            int unit = 0;
            while (size >= 1024.0 && unit < units.Length - 1)
            {
                size /= 1024.0;
                unit++;
            }
            return unit == 0 ? ((long)size).ToString() + " " + units[unit] : size.ToString("0.0") + " " + units[unit];
        }
    }
}
