using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class ArcExtractionResult
    {
        public ArcExtractionResult()
        {
            Extracted = 0;
            Bytes = 0;
            Errors = 0;
            Renamed = 0;
            Skipped = 0;
        }

        public int Extracted { get; set; }
        public long Bytes { get; set; }
        public int Errors { get; set; }
        public int Renamed { get; set; }
        public int Skipped { get; set; }
    }

    internal static class ArcExtractor
    {
        private static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);

        public static bool HasNativeSupport(string archivePath)
        {
            try
            {
                List<ArcRecord> records = ParseTable(archivePath);
                foreach (ArcRecord record in records)
                    if (record.IsFile) return true;
                return false;
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
                    if (inputPath.EndsWith(".arc", StringComparison.OrdinalIgnoreCase))
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
                try { files = Directory.GetFiles(directory, "*.arc"); }
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

        public static ArcExtractionResult ExtractAll(string gamePath, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            ArcExtractionResult total = new ArcExtractionResult();
            Directory.CreateDirectory(outputPath);
            List<string> archives = FindArchives(gamePath, outputPath);
            foreach (string archive in archives)
            {
                if (isCancelled != null && isCancelled())
                    throw new OperationCanceledException();
                if (log != null)
                    log("Processing ARC: " + MakeRelativeSafe(gamePath, archive));
                try
                {
                    ArcExtractionResult one = ExtractArchive(archive, outputPath, log, isCancelled);
                    total.Extracted += one.Extracted;
                    total.Bytes += one.Bytes;
                    total.Renamed += one.Renamed;
                    total.Skipped += one.Skipped;
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

        public static ArcExtractionResult ExtractArchive(string archivePath, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            ArcExtractionResult result = new ArcExtractionResult();
            List<ArcRecord> records = ParseTable(archivePath);
            string archiveOutput = Path.Combine(outputPath, "archives", NormalizeName(Path.GetFileNameWithoutExtension(archivePath)));
            using (FileStream stream = File.OpenRead(archivePath))
            {
                string currentDir = "";
                int processed = 0;
                foreach (ArcRecord record in records)
                {
                    if (isCancelled != null && isCancelled())
                        throw new OperationCanceledException();
                    if (!record.IsFile)
                    {
                        currentDir = record.Name;
                        continue;
                    }
                    processed++;
                    try
                    {
                        string relative = string.IsNullOrEmpty(currentDir)
                            ? record.Name
                            : currentDir + Path.DirectorySeparatorChar + record.Name;
                        bool renamed;
                        string destination = ExtractionPathUtils.GetUniqueFilePath(
                            ExtractionPathUtils.GetSafeOutputPath(archiveOutput, relative), out renamed);
                        string parent = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrWhiteSpace(parent))
                            Directory.CreateDirectory(parent);
                        stream.Seek(record.Offset, SeekOrigin.Begin);
                        using (FileStream output = File.Create(destination))
                            CopyExact(stream, output, record.Size);
                        result.Extracted++;
                        result.Bytes += record.Size;
                        if (renamed) result.Renamed++;
                    }
                    catch (Exception ex)
                    {
                        result.Skipped++;
                        if (log != null)
                            log("WARN:" + Path.GetFileName(archivePath) + "@" + record.Name + ": " + ex.Message);
                    }
                    if ((processed & 127) == 0 && isCancelled != null && isCancelled())
                        throw new OperationCanceledException();
                }
            }
            return result;
        }

        private sealed class ArcRecord
        {
            public bool IsFile;
            public string Name;
            public long Offset;
            public long Size;
            public long NextPosition;
        }

        private static List<ArcRecord> ParseTable(string archivePath)
        {
            List<ArcRecord> records = new List<ArcRecord>();
            using (FileStream stream = File.OpenRead(archivePath))
            {
                long fileLength = stream.Length;
                if (fileLength < 16)
                    throw new InvalidDataException("ARC archive is too small.");
                uint count = ReadU32(stream);
                long dataStart = ReadU32(stream);
                if (count == 0)
                    throw new InvalidDataException("ARC archive has no entries.");
                if (dataStart < 16 || dataStart > fileLength)
                    throw new InvalidDataException("ARC data offset is out of range.");
                long position = 16;
                while (position < dataStart)
                {
                    stream.Seek(position, SeekOrigin.Begin);
                    ArcRecord file = TryReadFileRecord(stream, dataStart, fileLength);
                    if (file != null)
                    {
                        records.Add(file);
                        position = file.NextPosition;
                        continue;
                    }
                    stream.Seek(position, SeekOrigin.Begin);
                    ArcRecord directory = TryReadDirRecord(stream, dataStart);
                    if (directory != null)
                    {
                        records.Add(directory);
                        position = directory.NextPosition;
                        continue;
                    }
                    throw new InvalidDataException("ARC table entry at 0x" + position.ToString("X") + " is not recognized.");
                }
                if (position != dataStart)
                    throw new InvalidDataException("ARC table does not end at the data section.");
                bool anyFile = false;
                foreach (ArcRecord record in records)
                    if (record.IsFile) { anyFile = true; break; }
                if (!anyFile)
                    throw new InvalidDataException("ARC archive contains no files.");
            }
            return records;
        }

        private static ArcRecord TryReadFileRecord(FileStream stream, long dataStart, long fileLength)
        {
            long position = stream.Position;
            if (position + 9 > dataStart) return null;
            uint offset = ReadU32(stream);
            uint size = ReadU32(stream);
            int length = stream.ReadByte();
            if (length <= 0 || length > 128) return null;
            if (position + 9 + length > dataStart) return null;
            byte[] nameBytes = new byte[length];
            if (ReadFull(stream, nameBytes, length) != length) return null;
            if ((long)offset < dataStart || (long)offset + (long)size > fileLength)
                return null;
            ArcRecord record = new ArcRecord();
            record.IsFile = true;
            record.Name = DecodeName(nameBytes);
            record.Offset = (long)offset;
            record.Size = (long)size;
            record.NextPosition = position + Pad16(9 + length);
            return record;
        }

        private static ArcRecord TryReadDirRecord(FileStream stream, long dataStart)
        {
            long position = stream.Position;
            if (position + 5 > dataStart) return null;
            uint marker = ReadU32(stream);
            int length = stream.ReadByte();
            if (length <= 0 || length > 128) return null;
            if (position + 5 + length > dataStart) return null;
            byte[] nameBytes = new byte[length];
            if (ReadFull(stream, nameBytes, length) != length) return null;
            ArcRecord record = new ArcRecord();
            record.IsFile = false;
            record.Name = DecodeName(nameBytes).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            record.NextPosition = position + Pad16(5 + length);
            return record;
        }

        private static long Pad16(long value)
        {
            return value + (-value % 16 + 16) % 16;
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

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "asset";
            string fixedSeparators = value.Replace('\\', '/');
            List<string> parts = new List<string>();
            foreach (string part in fixedSeparators.Split('/'))
            {
                if (string.IsNullOrEmpty(part) || part == ".") continue;
                if (part == "..")
                    throw new InvalidDataException("Archive entry escapes the output folder.");
                parts.Add(part);
            }
            if (parts.Count == 0) return "asset";
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.ToArray());
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

        private static uint ReadU32(FileStream stream)
        {
            byte[] buffer = new byte[4];
            if (ReadFull(stream, buffer, 4) != 4)
                throw new EndOfStreamException("ARC archive is truncated.");
            return (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
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

        private static void CopyExact(Stream input, Stream output, long count)
        {
            byte[] buffer = new byte[81920];
            while (count > 0)
            {
                int want = (int)Math.Min(buffer.Length, count);
                int read = input.Read(buffer, 0, want);
                if (read <= 0) throw new EndOfStreamException("ARC entry is truncated.");
                output.Write(buffer, 0, read);
                count -= read;
            }
        }
    }
}
