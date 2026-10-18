using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class GameMakerResult
    {
        public GameMakerResult()
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

    internal static class GameMakerExtractor
    {
        private static readonly byte[] PngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] FioqMagic = Encoding.ASCII.GetBytes("fioq");
        private static readonly byte[] Bz2QoiMagic = Encoding.ASCII.GetBytes("2zoq");
        private const int ScanChunkBytes = 64 * 1024 * 1024;
        private const int ScanOverlapBytes = 4096;
        private const long MaxTextureBytes = 512L * 1024L * 1024L;
        private const long MaxPixels = 100000000L;
        private static readonly string[] ImageExtensions =
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tif", ".tiff"
        };

        public static bool HasNativeSupport(string gamePath, string outputPath)
        {
            try
            {
                List<string> files = FindFiles(gamePath, outputPath);
                if (files.Count == 0) return false;
                // BZ2QOI pages need a bzip2 decoder, which the BCL does not ship:
                // those containers stay on the Python fallback path.
                foreach (string file in files)
                    if (ContainsMarker(file, Bz2QoiMagic)) return false;
                return true;
            }
            catch { return false; }
        }

        public static List<string> FindFiles(string gamePath, string outputPath)
        {
            List<string> matches = new List<string>();
            if (string.IsNullOrWhiteSpace(gamePath)) return matches;
            try
            {
                if (File.Exists(gamePath))
                {
                    matches.Add(Path.GetFullPath(gamePath));
                    return matches;
                }
            }
            catch { return matches; }
            if (!Directory.Exists(gamePath)) return matches;
            string outputRoot = "";
            try { outputRoot = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
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
                        if (outputRoot != "" && (string.Equals(full, outputRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                            || full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        if (string.Equals(Path.GetFileName(full), "extracted", StringComparison.OrdinalIgnoreCase))
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
                        string name = Path.GetFileName(files[i]);
                        string lowered = name.ToLowerInvariant();
                        if (!string.Equals(lowered, "data.win", StringComparison.OrdinalIgnoreCase)
                            && !(lowered.StartsWith("audiogroup", StringComparison.OrdinalIgnoreCase) && lowered.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)))
                            continue;
                        string full = Path.GetFullPath(files[i]);
                        if (outputRoot != "" && full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                            continue;
                        matches.Add(full);
                    }
                    catch { }
                }
            }
            matches.Sort(StringComparer.OrdinalIgnoreCase);
            return matches;
        }

        public static GameMakerResult ExtractAll(string gamePath, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            GameMakerResult total = new GameMakerResult();
            Directory.CreateDirectory(outputPath);
            List<string> files = FindFiles(gamePath, outputPath);
            foreach (string source in files)
            {
                if (isCancelled != null && isCancelled())
                    throw new OperationCanceledException();
                if (log != null)
                    log("Processing GameMaker container: " + MakeRelativeSafe(gamePath, source));
                try
                {
                    string original = ExtractionPathUtils.GetSafeOutputPath(outputPath, Path.Combine("originals", Path.GetFileName(source)));
                    bool originalRenamed;
                    original = ExtractionPathUtils.GetUniqueFilePath(original, out originalRenamed);
                    string parent = Path.GetDirectoryName(original);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    File.Copy(source, original);
                    total.Extracted++;
                    try { total.Bytes += new FileInfo(original).Length; }
                    catch { }
                    if (originalRenamed) total.Renamed++;
                    GameMakerResult textures = ExtractTextures(source, outputPath, log, isCancelled);
                    total.Extracted += textures.Extracted;
                    total.Bytes += textures.Bytes;
                    total.Renamed += textures.Renamed;
                    total.Skipped += textures.Skipped;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    total.Errors++;
                    if (log != null)
                        log("WARN:" + Path.GetFileName(source) + ": " + ex.Message);
                }
            }
            GameMakerResult external = CopyExternalImages(gamePath, outputPath);
            total.Extracted += external.Extracted;
            total.Bytes += external.Bytes;
            total.Renamed += external.Renamed;
            total.Skipped += external.Skipped;
            return total;
        }

        private static GameMakerResult ExtractTextures(string source, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            GameMakerResult result = new GameMakerResult();
            string folder = Path.Combine("embedded", Path.GetFileNameWithoutExtension(source));
            using (FileStream stream = File.OpenRead(source))
            {
                byte[] buffer = new byte[ScanChunkBytes + ScanOverlapBytes];
                long fileLength = stream.Length;
                long chunkBase = 0;
                int carried = 0;
                while (chunkBase + carried < fileLength)
                {
                    if (isCancelled != null && isCancelled())
                        throw new OperationCanceledException();
                    stream.Seek(chunkBase + carried, SeekOrigin.Begin);
                    int read = 0;
                    while (read < ScanChunkBytes)
                    {
                        int got = stream.Read(buffer, carried + read, ScanChunkBytes - read);
                        if (got <= 0) break;
                        read += got;
                    }
                    if (read <= 0) break;
                    int length = carried + read;
                    int safeEnd = length - ScanOverlapBytes;
                    if (chunkBase + length >= fileLength) safeEnd = length;
                    int position = 0;
                    while (position < safeEnd)
                    {
                        int pngAt = IndexOf(buffer, length, PngSignature, position);
                        int fioqAt = IndexOf(buffer, length, FioqMagic, position);
                        int next = MinPositive(pngAt, fioqAt);
                        if (next < 0 || next >= safeEnd) break;
                        long absolute = chunkBase + next;
                        try
                        {
                            int consumed;
                            if (StartsWithAt(buffer, length, next, PngSignature))
                                consumed = ExtractPng(source, absolute, outputPath, folder, result);
                            else
                                consumed = ExtractFioq(source, absolute, outputPath, folder, result);
                            position = next + Math.Max(consumed, 1);
                        }
                        catch (Exception ex)
                        {
                            result.Skipped++;
                            if (log != null)
                                log("WARN:" + Path.GetFileName(source) + "@" + absolute + ": " + ex.Message);
                            position = next + 1;
                        }
                    }
                    carried = Math.Min(ScanOverlapBytes, length);
                    Buffer.BlockCopy(buffer, length - carried, buffer, 0, carried);
                    chunkBase += length - carried;
                }
            }
            return result;
        }

        private static int ExtractPng(string source, long absolute, string outputPath, string folder, GameMakerResult result)
        {
            int length = MeasurePng(source, absolute);
            if (length <= 0)
                throw new InvalidDataException("Invalid embedded PNG.");
            byte[] raw = ReadRange(source, absolute, length);
            bool renamed;
            string destination = ExtractionPathUtils.GetUniqueFilePath(
                ExtractionPathUtils.GetSafeOutputPath(outputPath, Path.Combine(folder, "texture-page-" + (result.Extracted + 1).ToString("0000") + ".png")),
                out renamed);
            string parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
            File.WriteAllBytes(destination, raw);
            result.Extracted++;
            result.Bytes += raw.Length;
            if (renamed) result.Renamed++;
            return length;
        }

        private static int ExtractFioq(string source, long absolute, string outputPath, string folder, GameMakerResult result)
        {
            byte[] header = ReadRange(source, absolute, 12);
            if (!StartsWith(header, FioqMagic))
                throw new InvalidDataException("GameMaker QOI header was not found.");
            int encodedSize = (int)ReadU32LE(header, 8);
            if (encodedSize < 0 || (long)encodedSize > MaxTextureBytes)
                throw new InvalidDataException("GameMaker QOI payload is truncated or too large.");
            byte[] raw = ReadRange(source, absolute, 12 + encodedSize);
            int width;
            int height;
            byte[] pixels = DecodeFioq(raw, out width, out height);
            byte[] png = PngEncoder.EncodeRgba(pixels, width, height);
            bool renamed;
            string destination = ExtractionPathUtils.GetUniqueFilePath(
                ExtractionPathUtils.GetSafeOutputPath(outputPath, Path.Combine(folder, "texture-page-" + (result.Extracted + 1).ToString("0000") + ".png")),
                out renamed);
            string parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
            File.WriteAllBytes(destination, png);
            long saved = png.Length;
            int collisions = renamed ? 1 : 0;
            int count = 1;
            bool rawRenamed;
            string rawPath = ExtractionPathUtils.GetUniqueFilePath(
                ExtractionPathUtils.GetSafeOutputPath(outputPath, Path.Combine(folder, "texture-page-" + (result.Extracted + 1).ToString("0000") + ".qoi")),
                out rawRenamed);
            string rawParent = Path.GetDirectoryName(rawPath);
            if (!string.IsNullOrWhiteSpace(rawParent))
                Directory.CreateDirectory(rawParent);
            File.WriteAllBytes(rawPath, raw);
            saved += raw.Length;
            if (rawRenamed) collisions++;
            count++;
            result.Extracted += count;
            result.Bytes += saved;
            result.Renamed += collisions;
            return raw.Length;
        }

        private static int MeasurePng(string source, long absolute)
        {
            using (FileStream stream = File.OpenRead(source))
            {
                stream.Seek(absolute, SeekOrigin.Begin);
                byte[] signature = new byte[8];
                if (ReadFull(stream, signature, 8) != 8) return 0;
                for (int i = 0; i < PngSignature.Length; i++)
                    if (signature[i] != PngSignature[i]) return 0;
                long position = absolute + 8;
                int chunks = 0;
                byte[] header = new byte[8];
                while (chunks < 100000)
                {
                    if (ReadFull(stream, header, 8) != 8) return 0;
                    long size = ((long)(header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3])) & 0xFFFFFFFFL;
                    bool isEnd = header[4] == (byte)'I' && header[5] == (byte)'E' && header[6] == (byte)'N' && header[7] == (byte)'D';
                    if (size > 256L * 1024L * 1024L) return 0;
                    try { stream.Seek(size + 4, SeekOrigin.Current); }
                    catch { return 0; }
                    position += 12 + size;
                    chunks++;
                    if (position > stream.Length) return 0;
                    if (isEnd) return size == 0 ? (int)(position - absolute) : 0;
                }
                return 0;
            }
        }

        private static byte[] DecodeFioq(byte[] data, out int width, out int height)
        {
            if (data == null || data.Length < 12 || !StartsWith(data, FioqMagic))
                throw new InvalidDataException("GameMaker QOI header was not found.");
            width = data[4] | (data[5] << 8);
            height = data[6] | (data[7] << 8);
            int encodedSize = (int)ReadU32LE(data, 8);
            if (width <= 0 || height <= 0 || (long)width * (long)height > MaxPixels)
                throw new InvalidDataException("GameMaker QOI dimensions are invalid.");
            if (encodedSize < 0 || encodedSize > MaxTextureBytes || 12 + encodedSize > data.Length)
                throw new InvalidDataException("GameMaker QOI payload is truncated or too large.");
            int[,] index = new int[64, 4];
            byte[] pixels = new byte[(long)width * (long)height * 4];
            int red = 0;
            int green = 0;
            int blue = 0;
            int alpha = 255;
            int position = 12;
            int end = 12 + encodedSize;
            int run = 0;
            for (long pixel = 0; pixel < (long)width * (long)height; pixel++)
            {
                if (run > 0)
                {
                    run--;
                }
                else
                {
                    if (position >= end)
                        throw new InvalidDataException("GameMaker QOI pixel stream is truncated.");
                    int first = data[position++];
                    if ((first & 0xC0) == 0x00)
                    {
                        int[] entry = new int[] { index[first & 0x3F, 0], index[first & 0x3F, 1], index[first & 0x3F, 2], index[first & 0x3F, 3] };
                        red = entry[0];
                        green = entry[1];
                        blue = entry[2];
                        alpha = entry[3];
                    }
                    else if ((first & 0xE0) == 0x40)
                    {
                        run = first & 0x1F;
                    }
                    else if ((first & 0xE0) == 0x60)
                    {
                        if (position >= end)
                            throw new InvalidDataException("GameMaker QOI run is truncated.");
                        run = ((first & 0x1F) << 8 | data[position]) + 32;
                        position++;
                    }
                    else if ((first & 0xC0) == 0x80)
                    {
                        red = (red + SignExtend((first >> 4) & 0x03, 2)) & 0xFF;
                        green = (green + SignExtend((first >> 2) & 0x03, 2)) & 0xFF;
                        blue = (blue + SignExtend(first & 0x03, 2)) & 0xFF;
                    }
                    else if ((first & 0xE0) == 0xC0)
                    {
                        if (position >= end)
                            throw new InvalidDataException("GameMaker QOI diff is truncated.");
                        int merged = first << 8 | data[position];
                        position++;
                        red = (red + SignExtend((merged >> 8) & 0x1F, 5)) & 0xFF;
                        green = (green + SignExtend((merged >> 4) & 0x0F, 4)) & 0xFF;
                        blue = (blue + SignExtend(merged & 0x0F, 4)) & 0xFF;
                    }
                    else if ((first & 0xF0) == 0xE0)
                    {
                        if (position + 2 > end)
                            throw new InvalidDataException("GameMaker QOI alpha diff is truncated.");
                        int merged = first << 16 | data[position] << 8 | data[position + 1];
                        position += 2;
                        red = (red + SignExtend((merged >> 15) & 0x1F, 5)) & 0xFF;
                        green = (green + SignExtend((merged >> 10) & 0x1F, 5)) & 0xFF;
                        blue = (blue + SignExtend((merged >> 5) & 0x1F, 5)) & 0xFF;
                        alpha = (alpha + SignExtend(merged & 0x1F, 5)) & 0xFF;
                    }
                    else
                    {
                        if ((first & 8) != 0)
                        {
                            if (position >= end) throw new InvalidDataException("GameMaker QOI color is truncated.");
                            red = data[position++];
                        }
                        if ((first & 4) != 0)
                        {
                            if (position >= end) throw new InvalidDataException("GameMaker QOI color is truncated.");
                            green = data[position++];
                        }
                        if ((first & 2) != 0)
                        {
                            if (position >= end) throw new InvalidDataException("GameMaker QOI color is truncated.");
                            blue = data[position++];
                        }
                        if ((first & 1) != 0)
                        {
                            if (position >= end) throw new InvalidDataException("GameMaker QOI color is truncated.");
                            alpha = data[position++];
                        }
                    }
                }
                int slot = (red ^ green ^ blue ^ alpha) & 63;
                index[slot, 0] = red;
                index[slot, 1] = green;
                index[slot, 2] = blue;
                index[slot, 3] = alpha;
                long target = pixel * 4;
                pixels[target] = (byte)red;
                pixels[target + 1] = (byte)green;
                pixels[target + 2] = (byte)blue;
                pixels[target + 3] = (byte)alpha;
            }
            return pixels;
        }

        private static int SignExtend(int value, int bits)
        {
            int sign = 1 << (bits - 1);
            return (value & sign) != 0 ? value - (1 << bits) : value;
        }

        private static GameMakerResult CopyExternalImages(string gamePath, string outputPath)
        {
            GameMakerResult result = new GameMakerResult();
            if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath)) return result;
            string outputRoot = "";
            try { outputRoot = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
            catch { }
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
                        if (outputRoot != "" && (string.Equals(full, outputRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                            || full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        if (string.Equals(Path.GetFileName(full), "extracted", StringComparison.OrdinalIgnoreCase))
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
                        string extension = Path.GetExtension(files[i]).ToLowerInvariant();
                        bool supported = false;
                        for (int e = 0; e < ImageExtensions.Length; e++)
                            if (extension == ImageExtensions[e]) { supported = true; break; }
                        if (!supported) continue;
                        string full = Path.GetFullPath(files[i]);
                        if (outputRoot != "" && full.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                            continue;
                        string relative = MakeRelativeSafe(gamePath, full);
                        bool renamed;
                        string destination = ExtractionPathUtils.GetUniqueFilePath(
                            ExtractionPathUtils.GetSafeOutputPath(outputPath, Path.Combine("external", relative)), out renamed);
                        string parent = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrWhiteSpace(parent))
                            Directory.CreateDirectory(parent);
                        File.Copy(full, destination);
                        result.Extracted++;
                        try { result.Bytes += new FileInfo(destination).Length; }
                        catch { }
                        if (renamed) result.Renamed++;
                    }
                    catch
                    {
                        result.Skipped++;
                    }
                }
            }
            return result;
        }

        private static bool ContainsMarker(string path, byte[] marker)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] buffer = new byte[64 * 1024];
                    byte[] tail = new byte[marker.Length - 1];
                    int tailLength = 0;
                    while (true)
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if (read <= 0) break;
                        int total = tailLength + read;
                        byte[] window = new byte[total];
                        Buffer.BlockCopy(tail, 0, window, 0, tailLength);
                        Buffer.BlockCopy(buffer, 0, window, tailLength, read);
                        if (IndexOf(window, total, marker, 0) >= 0) return true;
                        tailLength = Math.Min(marker.Length - 1, total);
                        Buffer.BlockCopy(window, total - tailLength, tail, 0, tailLength);
                    }
                }
            }
            catch { }
            return false;
        }

        private static int IndexOf(byte[] data, int length, byte[] pattern, int offset)
        {
            if (pattern.Length == 0 || length < pattern.Length) return -1;
            int limit = length - pattern.Length;
            for (int i = Math.Max(offset, 0); i <= limit; i++)
            {
                if (data[i] != pattern[0]) continue;
                bool match = true;
                for (int j = 1; j < pattern.Length; j++)
                    if (data[i + j] != pattern[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[i] != prefix[i]) return false;
            return true;
        }

        private static bool StartsWithAt(byte[] data, int length, int offset, byte[] prefix)
        {
            if (offset < 0 || offset + prefix.Length > length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[offset + i] != prefix[i]) return false;
            return true;
        }

        private static int MinPositive(int first, int second)
        {
            if (first < 0) return second;
            if (second < 0) return first;
            return Math.Min(first, second);
        }

        private static byte[] ReadRange(string path, long offset, int count)
        {
            if (count < 0) throw new InvalidDataException("Byte range is not supported.");
            byte[] buffer = new byte[count];
            using (FileStream stream = File.OpenRead(path))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                int done = 0;
                while (done < count)
                {
                    int read = stream.Read(buffer, done, count - done);
                    if (read <= 0) throw new EndOfStreamException("Unexpected end of file.");
                    done += read;
                }
            }
            return buffer;
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

        private static uint ReadU32LE(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
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
    }
}
