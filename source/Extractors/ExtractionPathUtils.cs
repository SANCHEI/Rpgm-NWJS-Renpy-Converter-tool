using System;
using System.IO;
using System.Linq;

namespace RpgmvpConverterWinForms
{
    internal static class ExtractionPathUtils
    {
        public static string GetSafeOutputPath(string outputDir, string relativePath)
        {
            string root = AppendDirectorySeparator(Path.GetFullPath(outputDir));
            string destination = Path.GetFullPath(Path.Combine(outputDir, SanitizeRelativePath(relativePath)));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Output path escapes extraction folder: " + relativePath);
            return destination;
        }

        public static string MakeRelativePath(string rootPath, string path)
        {
            Uri root = new Uri(AppendDirectorySeparator(rootPath));
            Uri file = new Uri(path);
            return Uri.UnescapeDataString(root.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        public static string GetUniqueFilePath(string path, out bool renamed)
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

        public static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        public static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
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

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }
    }
}
