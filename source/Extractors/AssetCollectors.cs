using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class AssetCollectors
    {
        private static readonly HashSet<string> looseExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".html", ".htm", ".css", ".js", ".json", ".xml", ".txt", ".csv", ".ini",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico",
            ".mp3", ".ogg", ".wav", ".flac", ".m4a", ".aac", ".mid", ".midi",
            ".mp4", ".webm", ".avi", ".wmv", ".mov", ".mkv",
            ".ttf", ".otf", ".woff", ".woff2",
            ".qsp", ".qproj", ".rpy", ".rpyc", ".rpym", ".rpymc", ".ks"
        };

        public static bool IsHtmlGame(string inputPath)
        {
            string root = InputDirectory(inputPath);
            return Directory.Exists(root)
                && File.Exists(Path.Combine(root, "index.html"))
                && !File.Exists(Path.Combine(root, "package.json"))
                && !Directory.Exists(Path.Combine(root, "tyrano"))
                && !Directory.Exists(Path.Combine(root, "data", "scenario"));
        }

        public static bool IsQspGame(string inputPath)
        {
            if (IsFileWithExtension(inputPath, ".qsp")) return true;
            string root = InputDirectory(inputPath);
            return Directory.Exists(root) && Directory.EnumerateFiles(root, "*.qsp", SearchOption.TopDirectoryOnly).Any();
        }

        public static bool IsRagsInput(string inputPath)
        {
            if (IsFileWithExtension(inputPath, ".rag")) return true;
            string root = InputDirectory(inputPath);
            return Directory.Exists(root) && Directory.EnumerateFiles(root, "*.rag", SearchOption.TopDirectoryOnly).Any();
        }

        public static bool IsGameMakerInput(string inputPath)
        {
            if (File.Exists(inputPath) && Path.GetFileName(inputPath).Equals("data.win", StringComparison.OrdinalIgnoreCase))
                return true;
            string root = InputDirectory(inputPath);
            return Directory.Exists(root) && File.Exists(Path.Combine(root, "data.win"));
        }

        public static List<string> GetHtmlFiles(string inputPath, string outputDir)
        {
            return GetLooseResourceFiles(InputDirectory(inputPath), outputDir);
        }

        public static List<string> GetQspFiles(string inputPath, string outputDir)
        {
            string root = InputDirectory(inputPath);
            return GetLooseResourceFiles(root, outputDir)
                .Where(delegate(string path)
                {
                    string runtime = AppendSeparator(Path.Combine(root, "qsp"));
                    return !Path.GetFullPath(path).StartsWith(runtime, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }

        public static List<string> GetLooseResourceFiles(string inputPath, string outputDir)
        {
            string root = InputDirectory(inputPath);
            if (!Directory.Exists(root)) return new List<string>();
            string outputPrefix = AppendSeparator(Path.GetFullPath(outputDir));
            string extractedPrefix = AppendSeparator(Path.GetFullPath(Path.Combine(root, "extracted")));
            try
            {
                return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(delegate(string path)
                    {
                        string fullPath = Path.GetFullPath(path);
                        return !fullPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)
                            && !fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase)
                            && looseExtensions.Contains(Path.GetExtension(path));
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static CollectorResult CopyFiles(string relativeRoot, IEnumerable<string> files, string outputDir, string prefix)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            Directory.CreateDirectory(outputDir);
            foreach (string source in files)
            {
                try
                {
                    string relative = MakeRelativePath(relativeRoot, source);
                    string destination = SafeOutputPath(outputDir, Path.Combine(prefix ?? "", relative));
                    destination = UniqueFilePath(destination, ref renamed);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination);
                    extracted++;
                    bytes += new FileInfo(destination).Length;
                }
                catch
                {
                    skipped++;
                }
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        public static CollectorResult ExtractRags(string inputPath, string outputDir)
        {
            List<string> files = FindInputFiles(inputPath, ".rag");
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            Directory.CreateDirectory(outputDir);

            foreach (string source in files)
            {
                string original = SafeOutputPath(outputDir, Path.Combine("originals", Path.GetFileName(source)));
                original = UniqueFilePath(original, ref renamed);
                Directory.CreateDirectory(Path.GetDirectoryName(original));
                File.Copy(source, original);
                extracted++;
                bytes += new FileInfo(original).Length;

                byte[] data = File.ReadAllBytes(source);
                string folder = Path.Combine("embedded", Path.GetFileNameWithoutExtension(source));
                CollectorResult images = ExtractImages(data, outputDir, folder);
                extracted += images.Extracted;
                bytes += images.Bytes;
                renamed += images.Renamed;
                skipped += images.Skipped;
                CollectorResult ogg = ExtractOgg(data, outputDir, folder);
                extracted += ogg.Extracted;
                bytes += ogg.Bytes;
                renamed += ogg.Renamed;
                skipped += ogg.Skipped;
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        public static string WriteDiagnostics(string inputPath, string outputDir)
        {
            string root = InputDirectory(inputPath);
            Directory.CreateDirectory(outputDir);
            List<string> files;
            try { files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).ToList(); }
            catch { files = new List<string>(); }

            StringBuilder report = new StringBuilder();
            report.AppendLine("Game Asset Tool diagnostics");
            report.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Input: " + inputPath);
            report.AppendLine("Root: " + root);
            report.AppendLine("Files: " + files.Count);
            report.AppendLine("Bytes: " + files.Sum(delegate(string path) { return SafeLength(path); }));
            report.AppendLine();
            report.AppendLine("Extensions:");
            foreach (IGrouping<string, string> group in files.GroupBy(delegate(string path)
            {
                string extension = Path.GetExtension(path);
                return string.IsNullOrWhiteSpace(extension) ? "(none)" : extension.ToLowerInvariant();
            }).OrderByDescending(delegate(IGrouping<string, string> group) { return group.Count(); }))
            {
                report.AppendLine(group.Key + " | files=" + group.Count() + " | bytes=" + group.Sum(delegate(string path) { return SafeLength(path); }));
            }
            report.AppendLine();
            report.AppendLine("Top-level entries:");
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly).Take(200))
                    report.AppendLine((Directory.Exists(entry) ? "[DIR] " : "[FILE] ") + Path.GetFileName(entry));
            }
            catch { }
            report.AppendLine();
            report.AppendLine("Top-level file signatures:");
            try
            {
                foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.TopDirectoryOnly).Take(100))
                    report.AppendLine(Path.GetFileName(file) + " | " + ReadHeader(file, 16));
            }
            catch { }

            string reportPath = Path.Combine(outputDir, "GameAssetTool-diagnostics.txt");
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            return reportPath;
        }

        public static string InputDirectory(string inputPath)
        {
            if (Directory.Exists(inputPath)) return Path.GetFullPath(inputPath);
            if (File.Exists(inputPath)) return Path.GetDirectoryName(Path.GetFullPath(inputPath));
            return inputPath;
        }

        public static List<string> FindInputFiles(string inputPath, string extension)
        {
            if (IsFileWithExtension(inputPath, extension)) return new List<string> { Path.GetFullPath(inputPath) };
            string root = InputDirectory(inputPath);
            if (!Directory.Exists(root)) return new List<string>();
            try { return Directory.EnumerateFiles(root, "*" + extension, SearchOption.TopDirectoryOnly).ToList(); }
            catch { return new List<string>(); }
        }

        public static List<string> FindGameMakerFiles(string inputPath)
        {
            if (File.Exists(inputPath) && Path.GetFileName(inputPath).Equals("data.win", StringComparison.OrdinalIgnoreCase))
                return new List<string> { Path.GetFullPath(inputPath) };
            string root = InputDirectory(inputPath);
            string dataWin = Path.Combine(root, "data.win");
            return File.Exists(dataWin) ? new List<string> { dataWin } : new List<string>();
        }

        private static CollectorResult ExtractImages(byte[] data, string outputDir, string folder)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            extracted += ExtractDelimited(data, new byte[] { 0xFF, 0xD8, 0xFF }, new byte[] { 0xFF, 0xD9 }, ".jpg", outputDir, folder, ref bytes, ref renamed, ref skipped);
            extracted += ExtractDelimited(data, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }, ".png", outputDir, folder, ref bytes, ref renamed, ref skipped);
            extracted += ExtractDelimited(data, Encoding.ASCII.GetBytes("GIF8"), new byte[] { 0x3B }, ".gif", outputDir, folder, ref bytes, ref renamed, ref skipped);
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static CollectorResult ExtractOgg(byte[] data, string outputDir, string folder)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            byte[] signature = Encoding.ASCII.GetBytes("OggS");
            int offset = 0;
            while ((offset = Find(data, signature, offset)) >= 0)
            {
                int end = ReadOggEnd(data, offset);
                if (end <= offset)
                {
                    skipped++;
                    offset += signature.Length;
                    continue;
                }
                WriteCarved(data, offset, end, ".ogg", outputDir, folder, extracted + 1, ref bytes, ref renamed);
                extracted++;
                offset = end;
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static int ReadOggEnd(byte[] data, int offset)
        {
            int current = offset;
            bool found = false;
            while (current + 27 <= data.Length
                && data[current] == (byte)'O'
                && data[current + 1] == (byte)'g'
                && data[current + 2] == (byte)'g'
                && data[current + 3] == (byte)'S')
            {
                int segments = data[current + 26];
                if (current + 27 + segments > data.Length) return -1;
                int body = 0;
                for (int i = 0; i < segments; i++) body += data[current + 27 + i];
                current += 27 + segments + body;
                if (current > data.Length) return -1;
                found = true;
            }
            return found ? current : -1;
        }

        private static int ExtractDelimited(byte[] data, byte[] start, byte[] end, string extension, string outputDir, string folder, ref long bytes, ref int renamed, ref int skipped)
        {
            int extracted = 0;
            int offset = 0;
            while ((offset = Find(data, start, offset)) >= 0)
            {
                int endOffset = Find(data, end, offset + start.Length);
                if (endOffset < 0)
                {
                    skipped++;
                    break;
                }
                int afterEnd = endOffset + end.Length;
                WriteCarved(data, offset, afterEnd, extension, outputDir, folder, extracted + 1, ref bytes, ref renamed);
                extracted++;
                offset = afterEnd;
            }
            return extracted;
        }

        private static void WriteCarved(byte[] data, int start, int end, string extension, string outputDir, string folder, int index, ref long bytes, ref int renamed)
        {
            string destination = SafeOutputPath(outputDir, Path.Combine(folder, "asset-" + index.ToString("0000") + extension));
            destination = UniqueFilePath(destination, ref renamed);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (FileStream stream = File.Create(destination))
                stream.Write(data, start, end - start);
            bytes += end - start;
        }

        private static int Find(byte[] data, byte[] pattern, int offset)
        {
            for (int i = Math.Max(offset, 0); i <= data.Length - pattern.Length; i++)
            {
                if (data[i] != pattern[0]) continue;
                bool match = true;
                for (int j = 1; j < pattern.Length; j++)
                {
                    if (data[i + j] == pattern[j]) continue;
                    match = false;
                    break;
                }
                if (match) return i;
            }
            return -1;
        }

        private static bool IsFileWithExtension(string path, string extension)
        {
            return File.Exists(path) && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        private static string UniqueFilePath(string path, ref int renamed)
        {
            string candidate = path;
            string directory = Path.GetDirectoryName(path);
            string stem = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int suffix = 2;
            while (File.Exists(candidate))
                candidate = Path.Combine(directory, stem + " (" + suffix++ + ")" + extension);
            if (!candidate.Equals(path, StringComparison.OrdinalIgnoreCase)) renamed++;
            return candidate;
        }

        private static string SafeOutputPath(string outputDir, string relativePath)
        {
            string root = AppendSeparator(Path.GetFullPath(outputDir));
            string destination = Path.GetFullPath(Path.Combine(outputDir, Sanitize(relativePath)));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Output path escapes extraction folder.");
            return destination;
        }

        private static string Sanitize(string path)
        {
            string[] parts = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string[] sanitized = parts.Select(delegate(string part)
            {
                string value = string.Concat(part.Where(delegate(char c) { return !Path.GetInvalidFileNameChars().Contains(c); }));
                return string.IsNullOrWhiteSpace(value) || value == "." || value == ".." ? "asset" : value;
            }).ToArray();
            return sanitized.Length == 0 ? "asset" : Path.Combine(sanitized);
        }

        private static string MakeRelativePath(string rootPath, string path)
        {
            Uri root = new Uri(AppendSeparator(rootPath));
            return Uri.UnescapeDataString(root.MakeRelativeUri(new Uri(path)).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private static long SafeLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static string ReadHeader(string path, int count)
        {
            try
            {
                byte[] bytes = new byte[count];
                using (FileStream stream = File.OpenRead(path))
                {
                    int read = stream.Read(bytes, 0, bytes.Length);
                    return BitConverter.ToString(bytes, 0, read);
                }
            }
            catch { return "unavailable"; }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }

    internal sealed class CollectorResult
    {
        public CollectorResult(int extracted, long bytes, int renamed, int skipped)
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
