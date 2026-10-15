using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class DpmManifestResult
    {
        public DpmManifestResult()
        {
            Archives = 0;
            Files = 0;
            Bytes = 0;
            Errors = 0;
        }

        public int Archives { get; set; }
        public int Files { get; set; }
        public long Bytes { get; set; }
        public int Errors { get; set; }
    }

    internal static class HspDpmExtractor
    {
        private const int RecordSize = 40;
        private static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);

        public static bool HasNativeSupport(string archivePath)
        {
            try
            {
                return ParseTable(archivePath).Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static List<string> FindArchives(string inputPath, string outputPath)
        {
            List<string> archives = new List<string>();
            if (string.IsNullOrWhiteSpace(inputPath)) return archives;
            try
            {
                if (File.Exists(inputPath))
                {
                    if (inputPath.EndsWith(".dpm", StringComparison.OrdinalIgnoreCase))
                        archives.Add(Path.GetFullPath(inputPath));
                    return archives;
                }
            }
            catch { return archives; }
            if (!Directory.Exists(inputPath)) return archives;
            string outputRoot = "";
            try { outputRoot = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
            string gameRoot = "";
            try { gameRoot = Path.GetFullPath(inputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
            Stack<string> pending = new Stack<string>();
            pending.Push(inputPath);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                string[] subdirs;
                try { subdirs = Directory.GetDirectories(directory); }
                catch { continue; }
                for (int i = 0; i < subdirs.Length; i++)
                {
                    try
                    {
                        string full = Path.GetFullPath(subdirs[i]);
                        if (outputRoot != "" && (string.Equals(full, outputRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                            || full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        if (gameRoot != "" && string.Equals(full, gameRoot + "extracted", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    catch { }
                    pending.Push(subdirs[i]);
                }
                string[] files;
                try { files = Directory.GetFiles(directory, "*.dpm"); }
                catch { continue; }
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string full = Path.GetFullPath(files[i]);
                        if (outputRoot != "" && full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (ExtractionPathUtils.IsOwnToolFile(full)) continue;
                        archives.Add(full);
                    }
                    catch { }
                }
            }
            archives.Sort(StringComparer.OrdinalIgnoreCase);
            return archives;
        }

        public static DpmManifestResult WriteManifests(string gamePath, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            DpmManifestResult total = new DpmManifestResult();
            Directory.CreateDirectory(outputPath);
            List<string> archives = FindArchives(gamePath, outputPath);
            foreach (string archive in archives)
            {
                if (isCancelled != null && isCancelled())
                    throw new OperationCanceledException();
                if (log != null)
                    log("Processing DPM: " + MakeRelativeSafe(gamePath, archive));
                try
                {
                    List<DpmEntry> entries = ParseTable(archive);
                    string manifestName = Path.GetFileNameWithoutExtension(archive) + "-contents.txt";
                    bool renamed;
                    string manifestPath = ExtractionPathUtils.GetUniqueFilePath(
                        ExtractionPathUtils.GetSafeOutputPath(Path.Combine(outputPath, "archives"), manifestName), out renamed);
                    string parent = Path.GetDirectoryName(manifestPath);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    using (StreamWriter writer = new StreamWriter(manifestPath, false, new UTF8Encoding(false)))
                    {
                        writer.WriteLine("Game Asset Tool DPM contents");
                        writer.WriteLine("Archive: " + Path.GetFileName(archive));
                        writer.WriteLine("Files: " + entries.Count);
                        writer.WriteLine();
                        writer.WriteLine("name\tsize");
                        foreach (DpmEntry entry in entries)
                            writer.WriteLine(entry.Name + "\t" + entry.Size);
                        writer.WriteLine();
                        writer.WriteLine("DPM2 file data uses a closed cipher that this tool does not decode: listed files stay inside the archive.");
                    }
                    total.Archives++;
                    total.Files += entries.Count;
                    foreach (DpmEntry entry in entries)
                        total.Bytes += entry.Size;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    total.Errors++;
                    if (log != null)
                        log("WARN:" + Path.GetFileName(archive) + ": " + ex.Message);
                }
            }
            return total;
        }

        private sealed class DpmEntry
        {
            public string Name;
            public long Size;
            public long Offset;
        }

        private static List<DpmEntry> ParseTable(string archivePath)
        {
            List<DpmEntry> entries = new List<DpmEntry>();
            using (FileStream stream = File.OpenRead(archivePath))
            {
                long fileLength = stream.Length;
                if (fileLength < 32)
                    throw new InvalidDataException("DPM archive is too small.");
                byte[] header = new byte[32];
                if (ReadFull(stream, header, 32) != 32)
                    throw new EndOfStreamException("DPM header is truncated.");
                if (header[0] != (byte)'D' || header[1] != (byte)'P' || header[2] != (byte)'M' || header[3] != (byte)'2')
                    throw new InvalidDataException("DPM2 header was not found.");
                long maxFiles = ReadU32(header, 4);
                long stringsBase = ReadU32(header, 8);
                long dataStart = ReadU32(header, 12);
                if (maxFiles <= 0 || maxFiles > 1000000)
                    throw new InvalidDataException("DPM file count is out of range.");
                long recordsEnd = 32 + maxFiles * RecordSize;
                if (stringsBase < recordsEnd || dataStart <= stringsBase || dataStart > fileLength)
                    throw new InvalidDataException("DPM table pointers are out of range.");
                byte[] record = new byte[RecordSize];
                for (long i = 0; i < maxFiles; i++)
                {
                    stream.Seek(32 + i * RecordSize, SeekOrigin.Begin);
                    if (ReadFull(stream, record, RecordSize) != RecordSize)
                        throw new EndOfStreamException("DPM table is truncated.");
                    uint nameOffset = ReadU32(record, 4);
                    ulong size = ReadU64(record, 8);
                    ulong offset = ReadU64(record, 16);
                    string name = ReadCString(stream, fileLength, stringsBase, nameOffset, dataStart);
                    if ((long)offset < 0 || (long)size < 0 || dataStart + (long)offset + (long)size > fileLength)
                        throw new InvalidDataException("DPM entry points outside the archive.");
                    DpmEntry entry = new DpmEntry();
                    entry.Name = name;
                    entry.Size = (long)size;
                    entry.Offset = (long)offset;
                    entries.Add(entry);
                }
            }
            if (entries.Count == 0)
                throw new InvalidDataException("DPM archive contains no files.");
            return entries;
        }

        private static string ReadCString(FileStream stream, long fileLength, long stringsBase, uint offset, long dataStart)
        {
            long absolute = stringsBase + offset;
            if (absolute < stringsBase || absolute >= dataStart)
                throw new InvalidDataException("DPM name points outside the string table.");
            stream.Seek(absolute, SeekOrigin.Begin);
            List<byte> bytes = new List<byte>();
            while (bytes.Count <= 259)
            {
                int b = stream.ReadByte();
                if (b < 0)
                    throw new EndOfStreamException("DPM string table is truncated.");
                if (b == 0) break;
                bytes.Add((byte)b);
            }
            if (bytes.Count == 0 || bytes.Count > 259)
                throw new InvalidDataException("DPM name is malformed.");
            return DecodeName(bytes.ToArray());
        }

        private static string DecodeName(byte[] bytes)
        {
            bool ascii = true;
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] >= 0x80) { ascii = false; break; }
            if (ascii) return Encoding.ASCII.GetString(bytes);
            try { return Utf8Strict.GetString(bytes); }
            catch { }
            try { return Encoding.GetEncoding(932).GetString(bytes); }
            catch { return Encoding.UTF8.GetString(bytes); }
        }

        private static string MakeRelativeSafe(string rootPath, string path)
        {
            try
            {
                string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(root.Length);
            }
            catch { }
            return path;
        }

        private static uint ReadU32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
        }

        private static ulong ReadU64(byte[] buffer, int offset)
        {
            uint low = ReadU32(buffer, offset);
            uint high = ReadU32(buffer, offset + 4);
            return ((ulong)high << 32) | low;
        }

        private static int ReadFull(Stream stream, byte[] buffer, int count)
        {
            int done = 0;
            while (done < count)
            {
                int read = stream.Read(buffer, done, count - done);
                if (read <= 0) break;
                done += read;
            }
            return done;
        }
    }
}
