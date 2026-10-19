using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class AliceSactResult
    {
        public AliceSactResult()
        {
            Archives = 0;
            Files = 0;
            OpaqueEntries = 0;
            Bytes = 0;
            Errors = 0;
            Renamed = 0;
        }

        public int Archives { get; set; }
        public int Files { get; set; }
        public int OpaqueEntries { get; set; }
        public long Bytes { get; set; }
        public int Errors { get; set; }
        public int Renamed { get; set; }
    }

    internal static class AliceSactExtractor
    {
        private const int BlockSize = 1 << 8;
        private const int EntryHeaderSize = 32;
        private const int MaxTableBytes = 3 * 1024 * 1024;
        private const long MaxEntries = 1000000;
        private const int CopyBufferSize = 64 * 1024;
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private sealed class AldEntry
        {
            public long Offset;
            public long Size;
            public string Extension;
            public bool Opaque;

            public string Kind
            {
                get
                {
                    if (Opaque) return "opaque-cg";
                    if (!string.IsNullOrEmpty(Extension)) return Extension.TrimStart('.');
                    return "index";
                }
            }
        }

        public static bool HasNativeSupport(string archivePath)
        {
            try
            {
                return ParseEntries(archivePath).Count > 0;
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
                    if (inputPath.EndsWith(".ald", StringComparison.OrdinalIgnoreCase))
                        archives.Add(Path.GetFullPath(inputPath));
                    return archives;
                }
            }
            catch { return archives; }
            if (!Directory.Exists(inputPath)) return archives;
            string outputPrefix = "";
            try { outputPrefix = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
            string extractedPrefix = "";
            try { extractedPrefix = Path.GetFullPath(Path.Combine(inputPath, "extracted")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
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
                        if (outputPrefix.Length > 0 && full.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (extractedPrefix.Length > 0 && full.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    catch { }
                    pending.Push(subdirs[i]);
                }
                string[] files;
                try { files = Directory.GetFiles(directory, "*.ald"); }
                catch { continue; }
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string full = Path.GetFullPath(files[i]);
                        if (outputPrefix.Length > 0 && full.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (ExtractionPathUtils.IsOwnToolFile(full)) continue;
                        archives.Add(full);
                    }
                    catch { }
                }
            }
            archives.Sort(StringComparer.OrdinalIgnoreCase);
            return archives;
        }

        public static AliceSactResult ExtractAll(string gamePath, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            AliceSactResult total = new AliceSactResult();
            Directory.CreateDirectory(outputPath);
            List<string> archives = FindArchives(gamePath, outputPath);
            foreach (string archive in archives)
            {
                if (isCancelled != null && isCancelled())
                    throw new OperationCanceledException();
                if (log != null)
                    log("Processing ALD: " + Path.GetFileName(archive));
                try
                {
                    ProcessArchive(archive, outputPath, log, total);
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

        private static void ProcessArchive(string archive, string outputPath, Action<string> log, AliceSactResult total)
        {
            List<AldEntry> entries = ParseEntries(archive);
            string archiveName = Path.GetFileNameWithoutExtension(archive);
            string archiveDir = Path.Combine(outputPath, "archives", archiveName);

            int extracted = 0;
            long bytes = 0;
            using (FileStream source = File.OpenRead(archive))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    AldEntry entry = entries[i];
                    if (entry.Opaque || string.IsNullOrEmpty(entry.Extension)) continue;
                    string name = "entry_" + (i + 1).ToString("D5") + entry.Extension;
                    bool renamed;
                    string destination = ExtractionPathUtils.GetUniqueFilePath(
                        ExtractionPathUtils.GetSafeOutputPath(archiveDir, name), out renamed);
                    string parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    CopyPayload(source, entry.Offset + EntryHeaderSize, entry.Size - EntryHeaderSize, destination);
                    extracted++;
                    bytes += entry.Size - EntryHeaderSize;
                    total.Renamed += renamed ? 1 : 0;
                }
            }

            WriteManifest(archive, archiveName, entries, extracted, outputPath);
            total.Archives++;
            total.Files += entries.Count;
            total.OpaqueEntries += CountOpaque(entries);
            total.Bytes += bytes;
            if (log != null)
                log(archiveName + ": " + entries.Count + " entries, " + extracted + " media extracted, " + CountOpaque(entries) + " opaque (CG codec not decoded)");
        }

        private static int CountOpaque(List<AldEntry> entries)
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Opaque) count++;
            return count;
        }

        private static void CopyPayload(FileStream source, long offset, long length, string destination)
        {
            if (length <= 0) throw new InvalidDataException("ALD entry payload is empty.");
            source.Seek(offset, SeekOrigin.Begin);
            using (FileStream output = File.Create(destination))
            {
                byte[] buffer = new byte[CopyBufferSize];
                long remaining = length;
                while (remaining > 0)
                {
                    int want = (int)Math.Min(buffer.Length, remaining);
                    int read = source.Read(buffer, 0, want);
                    if (read <= 0) break;
                    output.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
        }

        private static void WriteManifest(string archive, string archiveName, List<AldEntry> entries, int extracted, string outputPath)
        {
            string manifestName = archiveName + "-contents.txt";
            bool renamed;
            string manifestPath = ExtractionPathUtils.GetUniqueFilePath(
                ExtractionPathUtils.GetSafeOutputPath(outputPath, Path.Combine("archives", manifestName)), out renamed);
            string parent = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
            using (StreamWriter writer = new StreamWriter(manifestPath, false, Utf8NoBom))
            {
                writer.WriteLine("Game Asset Tool AliceSoft SACT contents");
                writer.WriteLine("Archive: " + Path.GetFileName(archive));
                writer.WriteLine("Entries: " + entries.Count);
                writer.WriteLine("Media extracted: " + extracted);
                writer.WriteLine();
                writer.WriteLine("entry\tsize\ttype");
                for (int i = 0; i < entries.Count; i++)
                {
                    AldEntry entry = entries[i];
                    writer.WriteLine("entry_" + (i + 1).ToString("D5") + "\t" + entry.Size + "\t" + entry.Kind);
                }
                writer.WriteLine();
                writer.WriteLine("AliceSoft SACT CG image/video payloads use a closed codec shipped in the game's own DLLs; listed opaque entries stay inside the archive.");
            }
        }

        private static List<AldEntry> ParseEntries(string archivePath)
        {
            List<AldEntry> entries = new List<AldEntry>();
            using (FileStream stream = File.OpenRead(archivePath))
            {
                long fileLength = stream.Length;
                if (fileLength < BlockSize * 2)
                    throw new InvalidDataException("ALD archive is too small.");

                byte[] buffer = new byte[MaxTableBytes];
                int available = ReadFull(stream, buffer, buffer.Length);

                List<long> blocks = new List<long>();
                long previous = -1;
                int position = 0;
                while (position + 3 <= available && blocks.Count < MaxEntries)
                {
                    long value = buffer[position] | (buffer[position + 1] << 8) | (buffer[position + 2] << 16);
                    if (value <= previous || value > fileLength / BlockSize) break;
                    blocks.Add(value);
                    previous = value;
                    position += 3;
                }

                if (blocks.Count < 2)
                    throw new InvalidDataException("ALD offset table was not found.");
                if (blocks[0] <= 0)
                    throw new InvalidDataException("ALD offset table starts at an invalid position.");
                if (blocks[blocks.Count - 1] != fileLength / BlockSize)
                    throw new InvalidDataException("ALD offset table does not span the archive.");
                if ((long)blocks.Count * 3 >= blocks[0] * BlockSize)
                    throw new InvalidDataException("ALD offset table overlaps the first entry.");

                for (int i = 0; i + 1 < blocks.Count; i++)
                {
                    long start = blocks[i] * BlockSize;
                    long end = blocks[i + 1] * BlockSize;
                    if (start < 0 || end <= start || end > fileLength)
                        throw new InvalidDataException("ALD entry points outside the archive.");
                    AldEntry entry = new AldEntry();
                    entry.Offset = start;
                    entry.Size = end - start;
                    Classify(stream, entry);
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private static void Classify(FileStream stream, AldEntry entry)
        {
            if (entry.Size <= EntryHeaderSize)
            {
                entry.Opaque = false;
                return;
            }
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            byte[] header = new byte[EntryHeaderSize];
            if (ReadFull(stream, header, header.Length) != EntryHeaderSize)
            {
                entry.Opaque = true;
                return;
            }
            if (header[0] != 0x20 || header[1] != 0x00 || header[2] != 0x00 || header[3] != 0x00)
            {
                entry.Opaque = false;
                return;
            }
            byte[] probe = new byte[16];
            int read = ReadFull(stream, probe, probe.Length);
            string extension = read > 0 ? DetectMediaExtension(probe, read) : null;
            if (extension == null)
            {
                entry.Opaque = true;
                return;
            }
            entry.Extension = extension;
            entry.Opaque = false;
        }

        private static string DetectMediaExtension(byte[] buffer, int length)
        {
            if (length >= 4 && buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                return ".png";
            if (length >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                return ".jpg";
            if (length >= 4 && buffer[0] == (byte)'O' && buffer[1] == (byte)'g' && buffer[2] == (byte)'g' && buffer[3] == (byte)'S')
                return ".ogg";
            if (length >= 4 && buffer[0] == (byte)'f' && buffer[1] == (byte)'L' && buffer[2] == (byte)'a' && buffer[3] == (byte)'C')
                return ".flac";
            if (length >= 3 && buffer[0] == (byte)'I' && buffer[1] == (byte)'D' && buffer[2] == (byte)'3')
                return ".mp3";
            if (length >= 2 && buffer[0] == 0xFF && (buffer[1] & 0xE0) == 0xE0)
                return ".mp3";
            if (length >= 12 && buffer[0] == (byte)'R' && buffer[1] == (byte)'I' && buffer[2] == (byte)'F' && buffer[3] == (byte)'F')
            {
                if (buffer[8] == (byte)'W' && buffer[9] == (byte)'E' && buffer[10] == (byte)'B' && buffer[11] == (byte)'P')
                    return ".webp";
                if (buffer[8] == (byte)'W' && buffer[9] == (byte)'A' && buffer[10] == (byte)'V' && buffer[11] == (byte)'E')
                    return ".wav";
            }
            if (length >= 4 && buffer[0] == 0x1A && buffer[1] == 0x45 && buffer[2] == 0xDF && buffer[3] == 0xA3)
                return ".mkv";
            if (length >= 2 && buffer[0] == (byte)'B' && buffer[1] == (byte)'M')
                return ".bmp";
            return null;
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