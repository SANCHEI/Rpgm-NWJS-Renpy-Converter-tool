using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RpgmvpConverterWinForms
{
    internal sealed class GodotExtractionResult
    {
        public GodotExtractionResult()
        {
            Extracted = 0;
            Bytes = 0;
            Errors = 0;
            Renamed = 0;
            Skipped = 0;
            PreviewCount = 0;
        }

        public int Extracted { get; set; }
        public long Bytes { get; set; }
        public int Errors { get; set; }
        public int Renamed { get; set; }
        public int Skipped { get; set; }
        public int PreviewCount { get; set; }
        public string DiagnosticLine { get; set; }
    }

    internal static class GodotExtractor
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GDPC");
        private const uint PackDirEncrypted = 1;
        private const uint PackRelFilebase = 2;
        private const uint PackFileEncrypted = 1;
        private const uint PackFileRemoval = 2;
        private const long MaxEntryBytes = 4L * 1024L * 1024L * 1024L;
        private const int MaxEntryCount = 200000;
        private const long MaxKeyFileBytes = 4L * 1024L * 1024L;
        private const long MaxScannedExeBytes = 256L * 1024L * 1024L;
        private static readonly Regex HexKey = new Regex("(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])", RegexOptions.Compiled);
        private static readonly Regex Base64Key = new Regex("(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{43}=(?![A-Za-z0-9+/=])", RegexOptions.Compiled);
        private static readonly Regex ImportedHashSuffix = new Regex("^(.*)-[0-9a-fA-F]{32}(\\.[^.]+)?$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex ImportedImageName = new Regex(@"\.(png|jpg|jpeg|webp)-[0-9a-f]{32}\.", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static bool HasNativeSupport(string archivePath)
        {
            try
            {
                using (FileStream stream = File.OpenRead(archivePath))
                    return LocatePck(stream) >= 0;
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
            string outputRoot = "";
            try { outputRoot = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
            try
            {
                if (File.Exists(inputPath))
                {
                    string lowered = inputPath.ToLowerInvariant();
                    if (lowered.EndsWith(".pck") || (lowered.EndsWith(".exe") && HasNativeSupport(inputPath)))
                        archives.Add(Path.GetFullPath(inputPath));
                    return archives;
                }
            }
            catch { return archives; }
            if (!Directory.Exists(inputPath)) return archives;
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
                        if (outputRoot != "" && full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    catch { }
                    pending.Push(subdirs[i]);
                }
                string[] files;
                try { files = Directory.GetFiles(directory); }
                catch { continue; }
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        string lowered = files[i].ToLowerInvariant();
                        if (!(lowered.EndsWith(".pck") || (lowered.EndsWith(".exe") && HasNativeSupport(files[i]))))
                            continue;
                        string full = Path.GetFullPath(files[i]);
                        if (outputRoot != "" && full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                            continue;
                        archives.Add(full);
                    }
                    catch { }
                }
            }
            archives.Sort(StringComparer.OrdinalIgnoreCase);
            return archives;
        }

        public static GodotExtractionResult ExtractAll(string gamePath, string outputPath, string manualKey, Action<string> log, Func<bool> isCancelled)
        {
            GodotExtractionResult total = new GodotExtractionResult();
            Directory.CreateDirectory(outputPath);
            List<byte[]> keys = DiscoverKeys(gamePath, outputPath, manualKey, log);
            List<string> archives = FindArchives(gamePath, outputPath);
            List<string> diagnostics = new List<string>();
            foreach (string archive in archives)
            {
                if (isCancelled != null && isCancelled())
                    throw new OperationCanceledException();
                if (log != null)
                    log("Processing PCK: " + MakeRelativeSafe(gamePath, archive));
                try
                {
                    GodotExtractionResult one = ExtractArchive(archive, outputPath, keys, log, isCancelled);
                    total.Extracted += one.Extracted;
                    total.Bytes += one.Bytes;
                    total.Renamed += one.Renamed;
                    total.Skipped += one.Skipped;
                    total.PreviewCount += one.PreviewCount;
                    diagnostics.Add(one.DiagnosticLine);
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
            WriteDiagnostics(outputPath, diagnostics);
            return total;
        }

        private static List<byte[]> DiscoverKeys(string gamePath, string outputPath, string manualKey, Action<string> log)
        {
            List<byte[]> keys = new List<byte[]>();
            List<string> sources = new List<string>();
            if (!string.IsNullOrWhiteSpace(manualKey))
            {
                foreach (string value in manualKey.Split(new char[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    AddKey(keys, sources, value, "manual field");
            }
            string outputRoot = "";
            try { outputRoot = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
            if (!string.IsNullOrWhiteSpace(gamePath) && Directory.Exists(gamePath))
            {
                Stack<string> pending = new Stack<string>();
                pending.Push(gamePath);
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
                            if (Path.GetFileName(full).Equals("extracted", StringComparison.OrdinalIgnoreCase)) continue;
                            if (outputRoot != "" && full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase)) continue;
                        }
                        catch { }
                        pending.Push(subdirs[i]);
                    }
                    string[] files;
                    try { files = Directory.GetFiles(directory); }
                    catch { continue; }
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            string name = Path.GetFileName(files[i]);
                            string lowered = name.ToLowerInvariant();
                            long size = new FileInfo(files[i]).Length;
                            if ((lowered == "keys.txt" || lowered == "godot.keys" || lowered == "godot-key.txt") && size <= MaxKeyFileBytes)
                            {
                                foreach (string line in File.ReadAllLines(files[i]))
                                    AddKey(keys, sources, line.Split('#')[0], lowered);
                            }
                            else if (lowered.EndsWith(".exe") && size <= MaxScannedExeBytes)
                            {
                                ScanExeForKeys(files[i], name, keys, sources);
                            }
                        }
                        catch { }
                    }
                }
            }
            if (log != null)
            {
                if (keys.Count > 0)
                {
                    log("Discovered Godot key candidate(s): " + keys.Count);
                    sources.Sort(StringComparer.OrdinalIgnoreCase);
                    log("Godot key source(s): " + string.Join(", ", DistinctSources(sources).ToArray()));
                }
                else
                {
                    log("Godot key candidate(s): 0");
                }
            }
            return keys;
        }

        private static void ScanExeForKeys(string path, string source, List<byte[]> keys, List<string> sources)
        {
            // A 256MB game binary must not be loaded twice (bytes + decoded string)
            // just to hunt for a 64-char key: scan 1MB windows with an overlap wider
            // than any key pattern (64 hex / 44 base64 chars).
            const int ChunkSize = 1024 * 1024;
            const int Overlap = 256;
            byte[] tail = new byte[0];
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] buffer = new byte[ChunkSize];
                    while (true)
                    {
                        int read = 0;
                        while (read < buffer.Length)
                        {
                            int got = stream.Read(buffer, read, buffer.Length - read);
                            if (got <= 0) break;
                            read += got;
                        }
                        if (read == 0) break;
                        string text;
                        if (tail.Length > 0)
                        {
                            byte[] joined = new byte[tail.Length + read];
                            Buffer.BlockCopy(tail, 0, joined, 0, tail.Length);
                            Buffer.BlockCopy(buffer, 0, joined, tail.Length, read);
                            text = Encoding.ASCII.GetString(joined);
                        }
                        else
                        {
                            text = Encoding.ASCII.GetString(buffer, 0, read);
                        }
                        foreach (Match match in HexKey.Matches(text))
                            AddKey(keys, sources, match.Value, source);
                        foreach (Match match in Base64Key.Matches(text))
                            AddKey(keys, sources, match.Value, source);
                        if (read < buffer.Length) break;
                        int keep = read > Overlap ? Overlap : read;
                        tail = new byte[keep];
                        Buffer.BlockCopy(buffer, read - keep, tail, 0, keep);
                    }
                }
            }
            catch { }
        }

        private static List<string> DistinctSources(List<string> sources)
        {
            List<string> result = new List<string>();
            foreach (string source in sources)
                if (!result.Contains(source)) result.Add(source);
            return result;
        }

        private static void AddKey(List<byte[]> keys, List<string> sources, string value, string source)
        {
            byte[] key = ParseKey(value);
            if (key == null) return;
            foreach (byte[] existing in keys)
                if (BytesEqual(existing, key)) return;
            keys.Add(key);
            sources.Add(source);
        }

        private static byte[] ParseKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string text = value.Trim().Trim('"', '\'');
            if (text.Length == 0) return null;
            try
            {
                byte[] decoded;
                if (text.Length == 64)
                    decoded = HexToBytes(text);
                else
                    decoded = Convert.FromBase64String(text);
                return decoded != null && decoded.Length == 32 ? decoded : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] HexToBytes(string text)
        {
            if (text.Length % 2 != 0) return null;
            byte[] result = new byte[text.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int high = HexValue(text[i * 2]);
                int low = HexValue(text[i * 2 + 1]);
                if (high < 0 || low < 0) return null;
                result[i] = (byte)((high << 4) | low);
            }
            return result;
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private static long LocatePck(FileStream stream)
        {
            if (stream.Length < 4) return -1;
            stream.Seek(0, SeekOrigin.Begin);
            if (Matches(ReadExact(stream, 4), Magic)) return 0;
            if (stream.Length < 12) return -1;
            stream.Seek(-4, SeekOrigin.End);
            if (!Matches(ReadExact(stream, 4), Magic)) return -1;
            stream.Seek(-12, SeekOrigin.End);
            long embeddedSize = ReadI64(stream);
            long[] candidates = new long[] { stream.Length - embeddedSize - 8, stream.Length - embeddedSize - 12 };
            foreach (long candidate in candidates)
            {
                if (candidate < 0) continue;
                stream.Seek(candidate, SeekOrigin.Begin);
                if (Matches(ReadExact(stream, 4), Magic)) return candidate;
            }
            return -1;
        }

        private static bool Matches(byte[] data, byte[] pattern)
        {
            if (data.Length < pattern.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
                if (data[i] != pattern[i]) return false;
            return true;
        }

        private const long MaxPreviewBufferBytes = 64L * 1024L * 1024L;

        private static long CopyStreamToFile(FileStream source, string destination, long size, byte[] digest, Action<string> log, string entryPath)
        {
            if (size < 0 || size > MaxEntryBytes)
                throw new InvalidDataException("Godot PCK entry is too large: " + entryPath);
            bool check = !IsZeroDigest(digest);
            MD5 md5 = check ? MD5.Create() : null;
            try
            {
                byte[] buffer = new byte[65536];
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    long remaining = size;
                    while (remaining > 0)
                    {
                        int want = remaining > buffer.Length ? buffer.Length : (int)remaining;
                        int read = source.Read(buffer, 0, want);
                        if (read <= 0) throw new EndOfStreamException("Unexpected end of PCK archive.");
                        output.Write(buffer, 0, read);
                        if (md5 != null) md5.TransformBlock(buffer, 0, read, buffer, 0);
                        remaining -= read;
                    }
                }
                if (md5 != null)
                {
                    md5.TransformFinalBlock(buffer, 0, 0);
                    if (!DigestMatchesHash(md5.Hash, digest) && log != null)
                        log("WARN:MD5 mismatch for " + entryPath);
                }
            }
            finally
            {
                if (md5 != null) md5.Dispose();
            }
            return size;
        }

        private static byte[] LoadPreviewBytes(string destination, long size)
        {
            if (!IsImportedTexturePath(destination)) return null;
            if (size < 0 || size > MaxPreviewBufferBytes) return null;
            try { return File.ReadAllBytes(destination); }
            catch { return null; }
        }

        private static GodotExtractionResult ExtractArchive(string archive, string outputPath, List<byte[]> keys, Action<string> log, Func<bool> isCancelled)
        {
            GodotExtractionResult result = new GodotExtractionResult();
            StringBuilder diagnostics = new StringBuilder();
            string archiveOutput = Path.Combine(outputPath, "archives", NormalizeName(Path.GetFileNameWithoutExtension(archive)));
            using (FileStream stream = File.OpenRead(archive))
            {
                long pckStart = LocatePck(stream);
                if (pckStart < 0)
                    throw new InvalidDataException("PCK header was not found.");
                List<GodotEntry> entries = ReadEntries(stream, pckStart, keys);
                foreach (GodotEntry entry in entries)
                {
                    if (isCancelled != null && isCancelled())
                        throw new OperationCanceledException();
                    stream.Seek(entry.Offset, SeekOrigin.Begin);
                    bool renamed;
                    string destination = ExtractionPathUtils.GetUniqueFilePath(
                        ExtractionPathUtils.GetSafeOutputPath(archiveOutput, NormalizeName(entry.Path)), out renamed);
                    string parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    byte[] data;
                    long written;
                    if (entry.Encrypted)
                    {
                        data = DecryptWithCandidates(stream, keys);
                        if (data.Length != entry.Size)
                            throw new InvalidDataException("Encrypted Godot entry size mismatch for " + entry.Path);
                        File.WriteAllBytes(destination, data);
                        written = data.Length;
                        if (!IsZeroDigest(entry.Digest) && !DigestMatches(data, entry.Digest))
                        {
                            if (log != null) log("WARN:MD5 mismatch for " + entry.Path);
                        }
                    }
                    else
                    {
                        // Plain payloads stream straight to disk: a multi-GB pack must not
                        // sit in memory as one byte[] (plus a second copy for previews).
                        written = CopyStreamToFile(stream, destination, entry.Size, entry.Digest, log, entry.Path);
                        data = LoadPreviewBytes(destination, entry.Size);
                    }
                    result.Extracted++;
                    result.Bytes += written;
                    if (renamed) result.Renamed++;
                    int previews = 0;
                    if (IsImportedTexturePath(destination))
                    {
                        if (data != null)
                            previews = WriteImportedPreview(destination, data, result);
                        else
                            result.Skipped++;
                    }
                    result.PreviewCount += previews;
                }
            }
            diagnostics.Append(archive + " | entries=" + result.Extracted + " previews=" + result.PreviewCount);
            result.DiagnosticLine = diagnostics.ToString();
            return result;
        }

        private sealed class GodotEntry
        {
            public string Path;
            public long Offset;
            public long Size;
            public byte[] Digest;
            public bool Encrypted;
        }

        private static List<GodotEntry> ReadEntries(FileStream stream, long pckStart, List<byte[]> keys)
        {
            long archiveSize = stream.Length;
            stream.Seek(pckStart + 4, SeekOrigin.Begin);
            long packVersion = ReadU32(stream);
            ReadU32(stream);
            ReadU32(stream);
            ReadU32(stream);
            if (packVersion == 1)
            {
                ReadExact(stream, 16 * 4);
                long fileCount = ReadU32(stream);
                if (fileCount > MaxEntryCount)
                    throw new InvalidDataException("Godot PCK contains too many entries.");
                List<GodotEntry> simple = new List<GodotEntry>((int)Math.Min(fileCount, 4096));
                for (long i = 0; i < fileCount; i++)
                {
                    long pathLength = ReadU32(stream);
                    if (pathLength > 1024 * 1024)
                        throw new InvalidDataException("Godot PCK entry path is too long.");
                    string path = ReadUtf8(stream, pathLength);
                    long rawOffset = ReadI64(stream);
                    long size = ReadI64(stream);
                    if (size < 0 || size > MaxEntryBytes)
                        throw new InvalidDataException("Godot PCK entry is too large: " + path);
                    byte[] digest = ReadExact(stream, 16);
                    simple.Add(MakeEntry(path, ChooseOffset(rawOffset, size, archiveSize, pckStart), size, digest, false, archiveSize));
                }
                return simple;
            }
            if (packVersion != 2 && packVersion != 3 && packVersion != 4)
                throw new InvalidDataException("Unsupported Godot PCK format version: " + packVersion + ". Supported versions: 1, 2, 3 and compatible 4 archives.");
            long packFlags = ReadU32(stream);
            long fileBase = ReadI64(stream);
            if (packVersion == 3 || packVersion == 4 || (packFlags & PackRelFilebase) != 0)
                fileBase += pckStart;
            if (packVersion == 3 || packVersion == 4)
            {
                long directoryOffset = ReadI64(stream) + pckStart;
                stream.Seek(directoryOffset, SeekOrigin.Begin);
            }
            else
            {
                ReadExact(stream, 16 * 4);
            }
            long count = ReadU32(stream);
            if (count > MaxEntryCount)
                throw new InvalidDataException("Godot PCK contains too many entries.");
            Stream directory = stream;
            MemoryStream decryptedDirectory = null;
            try
            {
                if ((packFlags & PackDirEncrypted) != 0)
                {
                    byte[] directoryData = DecryptWithCandidates(stream, keys);
                    decryptedDirectory = new MemoryStream(directoryData, false);
                    directory = decryptedDirectory;
                }
                List<GodotEntry> entries = new List<GodotEntry>((int)Math.Min(count, 4096));
                for (long i = 0; i < count; i++)
                {
                    long pathLength = ReadU32(directory);
                    if (pathLength > 1024 * 1024)
                        throw new InvalidDataException("Godot PCK entry path is too long.");
                    string path = ReadUtf8(directory, pathLength);
                    long rawOffset = ReadI64(directory);
                    long size = ReadI64(directory);
                    if (size < 0 || size > MaxEntryBytes)
                        throw new InvalidDataException("Godot PCK entry is too large: " + path);
                    byte[] digest = ReadExact(directory, 16);
                    long entryFlags = ReadU32(directory);
                    if ((entryFlags & PackFileRemoval) != 0) continue;
                    long offset = fileBase + rawOffset;
                    if (offset < 0 || offset > archiveSize || ((entryFlags & PackFileEncrypted) == 0 && offset + size > archiveSize))
                        throw new InvalidDataException("PCK entry points outside the archive: " + path);
                    GodotEntry entry = new GodotEntry();
                    entry.Path = path;
                    entry.Offset = offset;
                    entry.Size = size;
                    entry.Digest = digest;
                    entry.Encrypted = (entryFlags & PackFileEncrypted) != 0;
                    entries.Add(entry);
                }
                return entries;
            }
            finally
            {
                if (decryptedDirectory != null) decryptedDirectory.Dispose();
            }
        }

        private static GodotEntry MakeEntry(string path, long offset, long size, byte[] digest, bool encrypted, long archiveSize)
        {
            if (offset < 0 || offset > archiveSize || offset + size > archiveSize)
                throw new InvalidDataException("PCK entry points outside the archive: " + path);
            GodotEntry entry = new GodotEntry();
            entry.Path = path;
            entry.Offset = offset;
            entry.Size = size;
            entry.Digest = digest;
            entry.Encrypted = encrypted;
            return entry;
        }

        private static long ChooseOffset(long rawOffset, long size, long archiveSize, long pckStart)
        {
            long relativeOffset = pckStart + rawOffset;
            if (relativeOffset + size <= archiveSize) return relativeOffset;
            return rawOffset;
        }

        private static byte[] DecryptWithCandidates(FileStream stream, List<byte[]> keys)
        {
            if (keys == null || keys.Count == 0)
                throw new InvalidDataException("Encrypted Godot PCK: no key candidates were found. Paste a 64-character HEX key or place it in keys.txt next to the game.");
            long start = stream.Position;
            InvalidDataException last = null;
            foreach (byte[] key in keys)
            {
                try
                {
                    stream.Seek(start, SeekOrigin.Begin);
                    return DecryptBlock(stream, key);
                }
                catch (InvalidDataException ex)
                {
                    last = ex;
                }
                catch (EndOfStreamException ex)
                {
                    last = new InvalidDataException(ex.Message);
                }
                catch (IOException ex)
                {
                    last = new InvalidDataException(ex.Message);
                }
            }
            throw new InvalidDataException("Encrypted Godot PCK: tried " + keys.Count + " key candidate(s), but none passed MD5 validation. The key is likely wrong or the archive uses an unsupported encryption variant." + (last != null ? " Last error: " + last.Message : ""));
        }

        private static byte[] DecryptBlock(FileStream stream, byte[] key)
        {
            byte[] expectedDigest = ReadExact(stream, 16);
            long size = ReadI64(stream);
            if (size < 0 || size > MaxEntryBytes)
                throw new InvalidDataException("Encrypted Godot block is too large.");
            byte[] iv = ReadExact(stream, 16);
            long padded = (size + 15) & ~15L;
            if (padded > int.MaxValue)
                throw new InvalidDataException("Encrypted Godot block is too large.");
            byte[] encrypted = ReadExact(stream, padded);
            byte[] data = AesCfbDecrypt(key, iv, encrypted);
            if (data.Length > size)
            {
                byte[] truncated = new byte[size];
                Buffer.BlockCopy(data, 0, truncated, 0, (int)size);
                data = truncated;
            }
            if (!DigestMatches(data, expectedDigest))
                throw new InvalidDataException("Godot encrypted block MD5 mismatch.");
            return data;
        }

        private static byte[] AesCfbDecrypt(byte[] key, byte[] iv, byte[] data)
        {
            if (key == null || key.Length != 32)
                throw new InvalidDataException("Godot AES key must be 32 bytes.");
            if (iv == null || iv.Length != 16)
                throw new InvalidDataException("Godot AES IV must be 16 bytes.");
            using (AesManaged aes = new AesManaged())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] output = new byte[data.Length];
                    byte[] feedback = new byte[16];
                    Buffer.BlockCopy(iv, 0, feedback, 0, 16);
                    byte[] keystream = new byte[16];
                    int position = 0;
                    while (position < data.Length)
                    {
                        encryptor.TransformBlock(feedback, 0, 16, keystream, 0);
                        int block = Math.Min(16, data.Length - position);
                        for (int i = 0; i < block; i++)
                            output[position + i] = (byte)(data[position + i] ^ keystream[i]);
                        Buffer.BlockCopy(data, position, feedback, 0, block);
                        if (block < 16) break;
                        position += block;
                    }
                    return output;
                }
            }
        }

        private static bool DigestMatches(byte[] data, byte[] expected)
        {
            if (expected == null) return true;
            if (IsZeroDigest(expected)) return true;
            using (MD5 md5 = MD5.Create())
                return DigestMatchesHash(md5.ComputeHash(data), expected);
        }

        private static bool DigestMatchesHash(byte[] actual, byte[] expected)
        {
            if (expected == null) return true;
            if (actual == null || actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; i++)
                if (actual[i] != expected[i]) return false;
            return true;
        }

        private static bool IsZeroDigest(byte[] digest)
        {
            if (digest == null) return true;
            for (int i = 0; i < digest.Length; i++)
                if (digest[i] != 0) return false;
            return true;
        }

        private static int WriteImportedPreview(string destination, byte[] data, GodotExtractionResult result)
        {
            if (!IsImportedTexturePath(destination)) return 0;
            byte[] image;
            string extension;
            string method;
            if (!TryExtractEmbeddedImage(data, out image, out extension))
            {
                string rawMethod;
                if (!TryDecodeUncompressedTexture(data, out image, out rawMethod))
                {
                    result.Skipped++;
                    return 0;
                }
                extension = ".png";
                method = rawMethod;
            }
            else
            {
                method = "embedded";
            }
            string previewName = ImportedPreviewStem(destination) + extension;
            string previewPath = Path.Combine(Path.GetDirectoryName(destination) ?? "", previewName);
            bool renamed;
            previewPath = ExtractionPathUtils.GetUniqueFilePath(previewPath, out renamed);
            string parent = Path.GetDirectoryName(previewPath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
            File.WriteAllBytes(previewPath, image);
            result.Bytes += image.Length;
            if (renamed) result.Renamed++;
            result.Extracted++;
            return 1;
        }

        private static bool TryExtractEmbeddedImage(byte[] data, out byte[] image, out string extension)
        {
            image = null;
            extension = "";
            int bestStart = -1;
            int bestEnd = -1;
            string bestExtension = "";
            int webp = IndexOf(data, Encoding.ASCII.GetBytes("RIFF"), 0);
            while (webp >= 0)
            {
                if (webp + 12 <= data.Length && data[webp + 8] == (byte)'W' && data[webp + 9] == (byte)'E'
                    && data[webp + 10] == (byte)'B' && data[webp + 11] == (byte)'P')
                {
                    long size = (long)ReadU32LE(data, webp + 4) + 8;
                    if (size > 12 && webp + size <= data.Length)
                    {
                        bestStart = webp;
                        bestEnd = webp + (int)size;
                        bestExtension = ".webp";
                        break;
                    }
                }
                webp = IndexOf(data, Encoding.ASCII.GetBytes("RIFF"), webp + 1);
            }
            int png = IndexOf(data, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0);
            if (png >= 0)
            {
                int end = IndexOf(data, Encoding.ASCII.GetBytes("IEND"), png);
                if (end >= 0 && end + 8 <= data.Length && (bestStart < 0 || png < bestStart))
                {
                    bestStart = png;
                    bestEnd = end + 8;
                    bestExtension = ".png";
                }
            }
            int jpg = IndexOf(data, new byte[] { 0xFF, 0xD8, 0xFF }, 0);
            if (jpg >= 0)
            {
                int end = IndexOf(data, new byte[] { 0xFF, 0xD9 }, jpg + 2);
                if (end >= 0 && (bestStart < 0 || jpg < bestStart))
                {
                    bestStart = jpg;
                    bestEnd = end + 2;
                    bestExtension = ".jpg";
                }
            }
            if (bestStart < 0) return false;
            image = new byte[bestEnd - bestStart];
            Buffer.BlockCopy(data, bestStart, image, 0, image.Length);
            extension = bestExtension;
            return true;
        }

        private static bool IsImportedTexturePath(string path)
        {
            string lowered = path.ToLowerInvariant().Replace('\\', '/');
            if (lowered.EndsWith(".ctex") || lowered.EndsWith(".stex")) return true;
            string name = lowered.Substring(lowered.LastIndexOf('/') + 1);
            return lowered.Contains("/.godot/imported/") && ImportedImageName.IsMatch(name);
        }

        private static string ImportedPreviewStem(string path)
        {
            string name = Path.GetFileName(path) ?? "";
            string stem = Path.GetFileNameWithoutExtension(name);
            Match match = ImportedHashSuffix.Match(stem);
            if (match.Success) stem = match.Groups[1].Value;
            int dot = stem.LastIndexOf('.');
            string original = dot >= 0 ? stem.Substring(0, dot) : stem;
            if (string.IsNullOrEmpty(original)) original = stem;
            return string.IsNullOrEmpty(original) ? "asset" : original;
        }

        private static bool PlausibleDimension(long value)
        {
            return value > 0 && value <= 16384;
        }

        private static bool TryDecodeUncompressedTexture(byte[] data, out byte[] image, out string method)
        {
            image = null;
            method = "";
            if (data == null || data.Length < 32) return false;
            List<int[]> candidates = CollectDimensions(data);
            int[] bpps = new int[] { 4, 3, 2, 1 };
            string[] modes = new string[] { "RGBA", "RGB", "LA", "L" };
            foreach (int[] dims in candidates)
            {
                long pixels = (long)dims[0] * (long)dims[1];
                if (pixels <= 0 || pixels > 268435456) continue;
                for (int f = 0; f < bpps.Length; f++)
                {
                    long required = pixels * bpps[f];
                    if (required <= 0 || required > data.Length) continue;
                    for (int offset = 16; offset < Math.Min(data.Length, 512); offset += 4)
                    {
                        long remaining = data.Length - offset;
                        if (remaining == required || (remaining >= required && remaining - required <= 64))
                        {
                            byte[] rgba = ConvertToRgba(data, offset, dims[0], dims[1], bpps[f]);
                            if (rgba == null) continue;
                            image = PngEncoder.EncodeRgba(rgba, dims[0], dims[1]);
                            method = "raw-" + modes[f] + "-" + dims[0] + "x" + dims[1];
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static List<int[]> CollectDimensions(byte[] data)
        {
            List<int[]> result = new List<int[]>();
            HashSet<string> seen = new HashSet<string>();
            int[] preferred = new int[] { 8, 12, 16, 20, 24, 28, 32, 36, 40, 48, 64 };
            foreach (int offset in preferred)
            {
                if (offset + 8 <= data.Length)
                    TryAddDimensions(result, seen, ReadU32LE(data, offset), ReadU32LE(data, offset + 4));
            }
            int limit = Math.Min(data.Length - 8, 128);
            for (int offset = 0; offset <= limit; offset += 4)
                TryAddDimensions(result, seen, ReadU32LE(data, offset), ReadU32LE(data, offset + 4));
            return result;
        }

        private static void TryAddDimensions(List<int[]> result, HashSet<string> seen, long width, long height)
        {
            if (!PlausibleDimension(width) || !PlausibleDimension(height)) return;
            string key = width + "x" + height;
            if (seen.Contains(key)) return;
            seen.Add(key);
            result.Add(new int[] { (int)width, (int)height });
        }

        private static byte[] ConvertToRgba(byte[] data, int offset, int width, int height, int bpp)
        {
            try
            {
                long count = (long)width * (long)height;
                if (bpp <= 0 || count <= 0) return null;
                if (offset < 0 || offset + count * (long)bpp > data.Length) return null;
                byte[] rgba = new byte[count * 4];
                for (long pixel = 0; pixel < count; pixel++)
                {
                    long source = offset + pixel * bpp;
                    if (bpp == 4)
                    {
                        rgba[pixel * 4] = data[source];
                        rgba[pixel * 4 + 1] = data[source + 1];
                        rgba[pixel * 4 + 2] = data[source + 2];
                        rgba[pixel * 4 + 3] = data[source + 3];
                    }
                    else if (bpp == 3)
                    {
                        rgba[pixel * 4] = data[source];
                        rgba[pixel * 4 + 1] = data[source + 1];
                        rgba[pixel * 4 + 2] = data[source + 2];
                        rgba[pixel * 4 + 3] = 255;
                    }
                    else if (bpp == 2)
                    {
                        rgba[pixel * 4] = data[source];
                        rgba[pixel * 4 + 1] = data[source];
                        rgba[pixel * 4 + 2] = data[source];
                        rgba[pixel * 4 + 3] = data[source + 1];
                    }
                    else
                    {
                        rgba[pixel * 4] = data[source];
                        rgba[pixel * 4 + 1] = data[source];
                        rgba[pixel * 4 + 2] = data[source];
                        rgba[pixel * 4 + 3] = 255;
                    }
                }
                return rgba;
            }
            catch
            {
                return null;
            }
        }

        private static int IndexOf(byte[] data, byte[] pattern, int offset)
        {
            if (pattern.Length == 0 || data.Length < pattern.Length) return -1;
            for (int i = Math.Max(offset, 0); i <= data.Length - pattern.Length; i++)
            {
                if (data[i] != pattern[0]) continue;
                bool match = true;
                for (int j = 1; j < pattern.Length; j++)
                    if (data[i + j] != pattern[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
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

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "asset";
            string fixedSeparators = value.Replace('\\', '/');
            if (fixedSeparators.StartsWith("res://", StringComparison.Ordinal))
                fixedSeparators = fixedSeparators.Substring(6);
            List<string> parts = new List<string>();
            foreach (string part in fixedSeparators.Split('/'))
            {
                if (string.IsNullOrEmpty(part) || part == ".") continue;
                if (part == "..")
                    throw new InvalidDataException("Archive entry escapes the output folder.");
                StringBuilder clean = new StringBuilder(part.Length);
                foreach (char c in part)
                {
                    if (c == '<' || c == '>' || c == ':' || c == '"' || c == '|' || c == '?' || c == '*' || c == '\0')
                        continue;
                    clean.Append(c);
                }
                parts.Add(clean.Length == 0 ? "asset" : clean.ToString());
            }
            if (parts.Count == 0) return "asset";
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.ToArray());
        }

        private static long ReadU32(Stream stream)
        {
            byte[] buffer = ReadExact(stream, 4);
            return (long)((uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24)));
        }

        private static long ReadU32(byte[] buffer, int offset)
        {
            return (long)((uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24)));
        }

        private static uint ReadU32LE(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
        }

        private static long ReadI64(Stream stream)
        {
            byte[] buffer = ReadExact(stream, 8);
            uint low = (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
            uint high = (uint)(buffer[4] | (buffer[5] << 8) | (buffer[6] << 16) | (buffer[7] << 24));
            return (long)(((ulong)high << 32) | low);
        }

        private static byte[] ReadExact(Stream stream, long size)
        {
            if (size < 0 || size > int.MaxValue)
                throw new InvalidDataException("Godot block size is not supported: " + size);
            byte[] buffer = new byte[(int)size];
            int done = 0;
            while (done < buffer.Length)
            {
                int read = stream.Read(buffer, done, buffer.Length - done);
                if (read <= 0) throw new EndOfStreamException("Unexpected end of PCK archive.");
                done += read;
            }
            return buffer;
        }

        private static string ReadUtf8(Stream stream, long length)
        {
            if (length < 0 || length > 1024 * 1024)
                throw new InvalidDataException("Godot PCK entry path is too long.");
            byte[] buffer = ReadExact(stream, length);
            int end = buffer.Length;
            while (end > 0 && buffer[end - 1] == 0) end--;
            return Encoding.UTF8.GetString(buffer, 0, end);
        }

        private static void WriteDiagnostics(string outputPath, List<string> lines)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(outputPath, "GameAssetTool-godot-diagnostics.txt"), false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("Godot diagnostics");
                    foreach (string line in lines)
                        writer.WriteLine(line);
                    writer.WriteLine("Note: .ctex/.stex are Godot imported texture containers. Recovered PNG/JPG/WebP previews and simple raw payloads are adjacent .png files; compressed GPU formats still need engine-specific decoding.");
                }
            }
            catch { }
        }
    }
}
