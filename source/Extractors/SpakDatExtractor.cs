using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class SpakDatExtractor : IAssetExtractor
    {
        private const int MaxEntries = 100000;
        private const long MaxEntryBytes = 512L * 1024L * 1024L;
        private const long MaxTotalBytes = 8L * 1024L * 1024L * 1024L;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SPAK");

        public string Id { get { return "spak-dat"; } }
        public string DisplayName { get { return "SPAK DAT experimental"; } }

        public bool CanExtract(string inputPath)
        {
            return FindArchives(inputPath).Count > 0;
        }

        public CollectorResult Extract(string inputPath, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            List<string> archives = FindArchives(inputPath);
            List<string> externalDatFiles = FindExternalDatFiles(inputPath, archives);
            List<string> manifest = new List<string>
            {
                "Game Asset Tool SPAK DAT manifest",
                "Protected or unknown entries are preserved as .dat files because SPITE encryption depends on the original runtime resource path.",
                "",
                "Source | Entry | Output | Bytes | Flags | Status"
            };
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;

            foreach (string archive in archives)
            {
                try
                {
                    string folderName = Path.GetFileNameWithoutExtension(archive);
                    string archiveOutput = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine("archives", folderName));
                    Directory.CreateDirectory(archiveOutput);
                    CollectorResult result = ExtractArchive(archive, archiveOutput, manifest);
                    extracted += result.Extracted;
                    bytes += result.Bytes;
                    renamed += result.Renamed;
                    skipped += result.Skipped;
                }
                catch (Exception ex)
                {
                    skipped++;
                    manifest.Add(Path.GetFileName(archive) + " | ERROR | " + ex.Message);
                }
            }

            CollectorResult externalResult = CopyExternalDatFiles(inputPath, externalDatFiles, outputDir, manifest);
            extracted += externalResult.Extracted;
            bytes += externalResult.Bytes;
            renamed += externalResult.Renamed;
            skipped += externalResult.Skipped;

            File.WriteAllLines(Path.Combine(outputDir, "SPAK-DAT-manifest.txt"), manifest, new UTF8Encoding(false));
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        public static List<string> FindSourceFiles(string inputPath)
        {
            List<string> archives = FindArchives(inputPath);
            return archives.Concat(FindExternalDatFiles(inputPath, archives))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<string> FindArchives(string inputPath)
        {
            if (File.Exists(inputPath))
                return IsSpakArchive(inputPath) ? new List<string> { Path.GetFullPath(inputPath) } : new List<string>();

            string root = AssetCollectors.InputDirectory(inputPath);
            if (!Directory.Exists(root)) return new List<string>();
            string extractedPrefix = AppendDirectorySeparator(Path.GetFullPath(Path.Combine(root, "extracted")));
            try
            {
                return Directory.EnumerateFiles(root, "*.dat", SearchOption.AllDirectories)
                    .Where(delegate(string path)
                    {
                        return !Path.GetFullPath(path).StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase)
                            && IsSpakArchive(path);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static List<string> FindExternalDatFiles(string inputPath, IEnumerable<string> archives)
        {
            if (File.Exists(inputPath)) return new List<string>();

            string root = AssetCollectors.InputDirectory(inputPath);
            if (!Directory.Exists(root)) return new List<string>();
            string extractedPrefix = AppendDirectorySeparator(Path.GetFullPath(Path.Combine(root, "extracted")));
            HashSet<string> archivePaths = new HashSet<string>(
                archives.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
            try
            {
                return Directory.EnumerateFiles(root, "*.dat", SearchOption.AllDirectories)
                    .Where(delegate(string path)
                    {
                        string fullPath = Path.GetFullPath(path);
                        return !fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase)
                            && !archivePaths.Contains(fullPath)
                            && !IsSpakArchive(fullPath);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static bool IsSpakArchive(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length < 12) return false;
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] header = new byte[Magic.Length];
                    return stream.Read(header, 0, header.Length) == header.Length
                        && header.SequenceEqual(Magic);
                }
            }
            catch
            {
                return false;
            }
        }

        public static CollectorResult ExtractArchive(string archivePath, string outputDir, List<string> manifest)
        {
            Directory.CreateDirectory(outputDir);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;

            using (FileStream stream = File.OpenRead(archivePath))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
                    throw new InvalidDataException("SPAK signature not found.");

                uint version = reader.ReadUInt32();
                uint entryCount = reader.ReadUInt32();
                if (version != 1)
                    throw new InvalidDataException("Unsupported SPAK version: " + version);
                if (entryCount > MaxEntries)
                    throw new InvalidDataException("SPAK entry limit exceeded.");

                long payloadOffset = 12L + entryCount * 28L;
                if (payloadOffset > stream.Length)
                    throw new InvalidDataException("SPAK table is truncated.");

                List<Entry> entries = new List<Entry>(checked((int)entryCount));
                long totalBytes = 0;
                for (int index = 0; index < entryCount; index++)
                {
                    string name = Encoding.ASCII.GetString(reader.ReadBytes(16));
                    uint offset = reader.ReadUInt32();
                    uint size = reader.ReadUInt32();
                    uint flags = reader.ReadUInt32();
                    long end = payloadOffset + offset + (long)size;
                    if (size > MaxEntryBytes || end > stream.Length)
                    {
                        skipped++;
                        continue;
                    }
                    totalBytes += size;
                    if (totalBytes > MaxTotalBytes)
                        throw new InvalidDataException("SPAK total output limit exceeded.");
                    entries.Add(new Entry(SanitizeEntryName(name, index), offset, size, flags));
                }

                byte[] buffer = new byte[81920];
                foreach (Entry entry in entries)
                {
                    stream.Position = payloadOffset + entry.Offset;
                    string extension = DetectExtension(stream, entry.Size);
                    stream.Position = payloadOffset + entry.Offset;
                    bool collision;
                    string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, entry.Name + extension);
                    destination = ExtractionPathUtils.GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));

                    using (FileStream output = File.Create(destination))
                    {
                        long remaining = entry.Size;
                        while (remaining > 0)
                        {
                            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                            if (read <= 0) throw new EndOfStreamException("SPAK entry is truncated.");
                            output.Write(buffer, 0, read);
                            remaining -= read;
                        }
                    }

                    extracted++;
                    bytes += entry.Size;
                    if (collision) renamed++;
                    manifest.Add(
                        Path.GetFileName(archivePath) + " | "
                        + entry.Name + " | "
                        + Path.GetFileName(destination) + " | "
                        + entry.Size + " | "
                        + entry.Flags + " | "
                        + (extension == ".dat" ? "protected-or-unknown" : "open-media"));
                }
            }

            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static CollectorResult CopyExternalDatFiles(string inputPath, IEnumerable<string> files, string outputDir, List<string> manifest)
        {
            string root = AssetCollectors.InputDirectory(inputPath);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            foreach (string source in files)
            {
                try
                {
                    string relativePath = ExtractionPathUtils.MakeRelativePath(root, source);
                    bool collision;
                    string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine("external", relativePath));
                    destination = ExtractionPathUtils.GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination);
                    long length = ExtractionPathUtils.SafeFileLength(source);
                    extracted++;
                    bytes += length;
                    if (collision) renamed++;
                    manifest.Add(
                        "external | "
                        + relativePath + " | "
                        + ExtractionPathUtils.MakeRelativePath(outputDir, destination) + " | "
                        + length + " | - | protected-or-unknown");
                }
                catch (Exception ex)
                {
                    skipped++;
                    manifest.Add("external | " + Path.GetFileName(source) + " | ERROR | " + ex.Message);
                }
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static string DetectExtension(Stream stream, uint size)
        {
            byte[] header = new byte[(int)Math.Min(size, 16u)];
            int read = stream.Read(header, 0, header.Length);
            if (read >= 8 && StartsWith(header, new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return ".png";
            if (read >= 3 && StartsWith(header, new byte[] { 0xff, 0xd8, 0xff })) return ".jpg";
            if (read >= 6 && (StartsWith(header, Encoding.ASCII.GetBytes("GIF87a")) || StartsWith(header, Encoding.ASCII.GetBytes("GIF89a")))) return ".gif";
            if (read >= 4 && StartsWith(header, Encoding.ASCII.GetBytes("OggS"))) return ".ogg";
            if (read >= 12 && StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && Encoding.ASCII.GetString(header, 8, 4) == "WAVE") return ".wav";
            if (read >= 12 && StartsWith(header, Encoding.ASCII.GetBytes("RIFF")) && Encoding.ASCII.GetString(header, 8, 4) == "WEBP") return ".webp";
            if (read >= 8 && Encoding.ASCII.GetString(header, 4, 4) == "ftyp") return ".mp4";
            return ".dat";
        }

        private static bool StartsWith(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length) return false;
            for (int index = 0; index < signature.Length; index++)
                if (data[index] != signature[index]) return false;
            return true;
        }

        private static string SanitizeEntryName(string name, int index)
        {
            string value = new string(name.Where(delegate(char character)
            {
                return (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');
            }).ToArray());
            return value.Length == 16 ? value.ToLowerInvariant() : "entry-" + (index + 1).ToString("0000");
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private sealed class Entry
        {
            public Entry(string name, uint offset, uint size, uint flags)
            {
                Name = name;
                Offset = offset;
                Size = size;
                Flags = flags;
            }

            public string Name { get; private set; }
            public uint Offset { get; private set; }
            public uint Size { get; private set; }
            public uint Flags { get; private set; }
        }
    }
}
