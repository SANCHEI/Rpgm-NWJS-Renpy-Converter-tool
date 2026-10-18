using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class SignatureAssetExtractor
    {
        private const long MaxScannedFileBytes = 512L * 1024L * 1024L;
        private const int ScanChunkBytes = 128 * 1024 * 1024;
        private const int ScanOverlapBytes = 2 * 1024 * 1024;
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF8");
        private static readonly byte[] Ogg = Encoding.ASCII.GetBytes("OggS");
        private static readonly byte[] Riff = Encoding.ASCII.GetBytes("RIFF");
        private static readonly byte[] Webm = { 0x1A, 0x45, 0xDF, 0xA3 };
        private static readonly byte[] Mp3Id3 = Encoding.ASCII.GetBytes("ID3");
        private static readonly byte[] Dds = Encoding.ASCII.GetBytes("DDS ");
        private static readonly byte[] Ktx1 = { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Ktx2 = { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Pvr3 = Encoding.ASCII.GetBytes("PVR\u0003");
        private static readonly byte[] Pkm = Encoding.ASCII.GetBytes("PKM ");
        private static readonly byte[] Astc = { 0x13, 0xAB, 0xA1, 0x5C };
        private static readonly byte[] Crn = Encoding.ASCII.GetBytes("CRN");
        private static readonly byte[] Qoi = Encoding.ASCII.GetBytes("qoif");
        private static readonly byte[] Tlg0 = Encoding.ASCII.GetBytes("TLG0.0\0sds\x1A");
        private static readonly byte[] Tlg5 = Encoding.ASCII.GetBytes("TLG5.0\0raw\x1A");
        private static readonly byte[] Tlg6 = Encoding.ASCII.GetBytes("TLG6.0\0raw\x1A");
        private static readonly byte[] QoiEndMarker = { 0, 0, 0, 0, 0, 0, 0, 1 };
        private static readonly HashSet<string> ZipExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".apk", ".jar", ".nw"
        };

        public static CollectorResult Extract(string inputPath, string outputDir)
        {
            return Extract(inputPath, outputDir, null);
        }

        internal static CollectorResult Extract(string inputPath, string outputDir, Func<bool> isCancelled)
        {
            Directory.CreateDirectory(outputDir);
            Counter counter = new Counter();
            string root = AssetCollectors.InputDirectory(inputPath);
            foreach (string source in FindFiles(inputPath, outputDir))
            {
                ThrowIfCancelled(isCancelled);
                try
                {
                    FileInfo info = new FileInfo(source);
                    if (info.Length <= MaxScannedFileBytes)
                    {
                        byte[] data = File.ReadAllBytes(source);
                        string relative = MakeSourceFolder(root, source);
                        ExtractBytes(data, data.Length, outputDir, Path.Combine("embedded", relative), counter, 0, isCancelled);
                    }
                    else
                    {
                        string relative = MakeSourceFolder(root, source);
                        ExtractStreamChunks(source, outputDir, Path.Combine("embedded", relative), counter, isCancelled);
                    }

                    if (ZipExtensions.Contains(Path.GetExtension(source)))
                        ExtractZip(source, outputDir, counter, isCancelled);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    counter.Skipped++;
                }
            }
            counter.WriteReports(outputDir);
            return counter.ToResult();
        }

        private static IEnumerable<string> FindFiles(string inputPath, string outputDir)
        {
            if (File.Exists(inputPath))
                return new[] { Path.GetFullPath(inputPath) };

            string root = AssetCollectors.InputDirectory(inputPath);
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            string outputPrefix = AppendSeparator(Path.GetFullPath(outputDir));
            string extractedPrefix = AppendSeparator(Path.GetFullPath(Path.Combine(root, "extracted")));
            try
            {
                return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(delegate(string path)
                    {
                        if (ExtractionPathUtils.IsOwnToolFile(path)) return false;
                        string fullPath = Path.GetFullPath(path);
                        return !fullPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)
                            && !fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static void ExtractZip(string source, string outputDir, Counter counter, Func<bool> isCancelled)
        {
            using (FileStream stream = File.OpenRead(source))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ArchiveSafetyPolicy.ValidateZipArchive(archive);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    ThrowIfCancelled(isCancelled);
                    if (string.IsNullOrEmpty(entry.Name) || entry.Length > MaxScannedFileBytes)
                    {
                        if (!string.IsNullOrEmpty(entry.Name)) counter.Skipped++;
                        continue;
                    }

                    try
                    {
                        byte[] data = new byte[checked((int)entry.Length)];
                        using (Stream input = entry.Open())
                        {
                            int offset = 0;
                            while (offset < data.Length)
                            {
                                ThrowIfCancelled(isCancelled);
                                int read = input.Read(data, offset, data.Length - offset);
                                if (read <= 0) throw new EndOfStreamException("ZIP entry is truncated.");
                                offset += read;
                            }
                        }
                        string folder = Path.Combine("archives", Path.GetFileNameWithoutExtension(source), ArchiveSafetyPolicy.NormalizeEntryPath(entry.FullName));
                        ExtractBytes(data, data.Length, outputDir, folder, counter, 0, isCancelled);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        counter.AddZipDiagnostic(source, entry.FullName, ex.Message);
                        counter.Skipped++;
                    }
                }
            }
        }

        private static void ExtractStreamChunks(string source, string outputDir, string folder, Counter counter, Func<bool> isCancelled)
        {
            using (FileStream stream = File.OpenRead(source))
            {
                byte[] buffer = new byte[ScanChunkBytes + ScanOverlapBytes];
                long position = 0;
                int carried = 0;
                while (position < stream.Length)
                {
                    ThrowIfCancelled(isCancelled);
                    int offset = carried;
                    int read = stream.Read(buffer, offset, ScanChunkBytes);
                    if (read <= 0) break;
                    int length = offset + read;
                    // The buffer head always holds exactly [0, length) valid bytes,
                    // so scanners run directly on it: no per-chunk LOH copy.
                    long chunkBase = position - carried;
                    ExtractBytes(buffer, length, outputDir, folder, counter, chunkBase, isCancelled);

                    carried = Math.Min(ScanOverlapBytes, length);
                    Buffer.BlockCopy(buffer, length - carried, buffer, 0, carried);
                    position += read;
                }
            }
        }

        private static void ExtractBytes(byte[] data, int length, string outputDir, string folder, Counter counter, long baseOffset, Func<bool> isCancelled)
        {
            ExtractDelimited(data, length, Png, FindPngEnd, ".png", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Jpeg, FindJpegEnd, ".jpg", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Gif, FindGifEnd, ".gif", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Ogg, FindOggEnd, ".ogg", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Webm, FindEbmlEnd, ".webm", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Mp3Id3, FindMp3End, ".mp3", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Riff, FindRiffEnd, null, outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Dds, FindDdsEnd, ".dds", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Ktx1, FindKtx1End, ".ktx", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Ktx2, FindKtx2End, ".ktx2", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Pvr3, FindPvr3End, ".pvr", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Pkm, FindPkmEnd, ".pkm", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Astc, FindAstcEnd, ".astc", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Crn, FindCrnEnd, ".crn", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Qoi, FindQoiEnd, ".qoi", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Tlg0, FindTlgEnd, ".tlg", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Tlg5, FindTlgEnd, ".tlg", outputDir, folder, counter, baseOffset, isCancelled);
            ExtractDelimited(data, length, Tlg6, FindTlgEnd, ".tlg", outputDir, folder, counter, baseOffset, isCancelled);
        }

        private delegate int FindEndHandler(byte[] data, int length, int offset);

        private static void ExtractDelimited(byte[] data, int length, byte[] signature, FindEndHandler findEnd, string extension, string outputDir, string folder, Counter counter, long baseOffset, Func<bool> isCancelled)
        {
            int offset = 0;
            while ((offset = FindBounded(data, length, signature, offset)) >= 0)
            {
                ThrowIfCancelled(isCancelled);
                int end = findEnd(data, length, offset);
                string resolvedExtension = extension ?? GetRiffExtension(data, length, offset);
                if (end <= offset || string.IsNullOrEmpty(resolvedExtension))
                {
                    counter.Skipped++;
                    offset++;
                    continue;
                }

                ThrowIfCancelled(isCancelled);
                Write(data, offset, end, resolvedExtension, outputDir, folder, counter, baseOffset + offset);
                offset = end;
            }
        }

        private static void ThrowIfCancelled(Func<bool> isCancelled)
        {
            if (isCancelled != null && isCancelled())
                throw new OperationCanceledException();
        }

        private static void Write(byte[] data, int start, int end, string extension, string outputDir, string folder, Counter counter, long absoluteOffset)
        {
            int length = end - start;
            string hash = ComputeHash(data, start, length);
            if (!counter.TryReserveDiscovery(folder, absoluteOffset, hash))
                return;

            string name = "asset-" + (counter.Extracted + 1).ToString("0000") + "-0x" + absoluteOffset.ToString("X8") + extension;
            string relative = Path.Combine(folder, name);
            string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, relative);
            bool renamed;
            destination = ExtractionPathUtils.GetUniqueFilePath(destination, out renamed);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (FileStream output = File.Create(destination))
                output.Write(data, start, length);
            counter.Extracted++;
            counter.Bytes += length;
            if (renamed) counter.Renamed++;
            counter.AddDiscovery(extension, absoluteOffset, length, destination, hash);
        }

        private static string ComputeHash(byte[] data, int offset, int count)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data, offset, count)).Replace("-", "");
        }

        private static int FindPngEnd(byte[] data, int length, int offset)
        {
            int position = offset + Png.Length;
            while (position + 12 <= length)
            {
                int size = ReadBigEndianInt32(data, position);
                if (size < 0 || position > length - size - 12) return -1;
                bool isEnd = data[position + 4] == (byte)'I'
                    && data[position + 5] == (byte)'E'
                    && data[position + 6] == (byte)'N'
                    && data[position + 7] == (byte)'D';
                position += size + 12;
                if (isEnd) return size == 0 ? position : -1;
            }
            return -1;
        }

        private static int FindJpegEnd(byte[] data, int length, int offset)
        {
            for (int index = offset + Jpeg.Length; index + 1 < length; index++)
                if (data[index] == 0xFF && data[index + 1] == 0xD9) return index + 2;
            return -1;
        }

        private static int FindGifEnd(byte[] data, int length, int offset)
        {
            if (offset + 6 > length
                || (data[offset + 4] != (byte)'7' && data[offset + 4] != (byte)'9')
                || data[offset + 5] != (byte)'a')
                return -1;
            for (int index = offset + 6; index < length; index++)
                if (data[index] == 0x3B) return index + 1;
            return -1;
        }

        private static int FindOggEnd(byte[] data, int length, int offset)
        {
            int position = offset;
            while (position + 27 <= length && FindBounded(data, length, Ogg, position) == position)
            {
                int segments = data[position + 26];
                if (position + 27 + segments > length) return -1;
                int pageBytes = 27 + segments;
                for (int index = 0; index < segments; index++) pageBytes += data[position + 27 + index];
                if (position + pageBytes > length) return -1;
                bool isEnd = (data[position + 5] & 0x04) != 0;
                position += pageBytes;
                if (isEnd) return position;
            }
            return -1;
        }

        private static int FindRiffEnd(byte[] data, int length, int offset)
        {
            if (offset + 12 > length) return -1;
            long end = (long)offset + 8 + ReadLittleEndianUInt32(data, offset + 4);
            return end <= length && end > offset + 12 ? (int)end : -1;
        }

        private static int FindEbmlEnd(byte[] data, int length, int offset)
        {
            if (offset + 8 > length) return -1;
            int next = FindBounded(data, length, Webm, offset + Webm.Length);
            if (next > offset) return next;
            return Math.Min(length, offset + 256 * 1024 * 1024);
        }

        private static int FindMp3End(byte[] data, int length, int offset)
        {
            int nextPng = FindBounded(data, length, Png, offset + Mp3Id3.Length);
            int nextJpeg = FindBounded(data, length, Jpeg, offset + Mp3Id3.Length);
            int nextOgg = FindBounded(data, length, Ogg, offset + Mp3Id3.Length);
            int next = MinPositive(nextPng, nextJpeg, nextOgg);
            return next > offset ? next : Math.Min(length, offset + 64 * 1024 * 1024);
        }

        private static int FindDdsEnd(byte[] data, int length, int offset)
        {
            if (offset + 128 > length) return -1;
            int height = ReadLittleEndianInt32(data, offset + 12);
            int width = ReadLittleEndianInt32(data, offset + 16);
            int fourCc = ReadLittleEndianInt32(data, offset + 84);
            if (width <= 0 || height <= 0 || width > 32768 || height > 32768) return -1;
            int blockSize = fourCc == 0x31545844 ? 8 : 16; // DXT1 uses 8-byte blocks, DXT3/DXT5/BCn usually 16.
            long dataBytes = ((long)Math.Max(1, (width + 3) / 4)) * Math.Max(1, (height + 3) / 4) * blockSize;
            long end = offset + 128L + dataBytes;
            return end > offset + 128 && end <= length ? (int)end : -1;
        }

        private static int FindKtx1End(byte[] data, int length, int offset)
        {
            if (offset + 64 > length) return -1;
            bool littleEndian = ReadLittleEndianUInt32(data, offset + 12) == 0x04030201;
            if (!littleEndian) return -1;
            uint bytesOfKeyValueData = ReadLittleEndianUInt32(data, offset + 60);
            long position = offset + 64L + bytesOfKeyValueData;
            if (position < offset + 64 || position > length - 4) return -1;
            uint imageSize = ReadLittleEndianUInt32(data, (int)position);
            long end = position + 4L + imageSize;
            return end > position && end <= length ? (int)Align4(end) : -1;
        }

        private static int FindKtx2End(byte[] data, int length, int offset)
        {
            if (offset + 80 > length) return -1;
            uint levelCount = ReadLittleEndianUInt32(data, offset + 36);
            if (levelCount == 0 || levelCount > 32) return -1;
            long levelIndex = offset + 80L;
            long end = levelIndex;
            for (int level = 0; level < levelCount; level++)
            {
                if (levelIndex + 24 > length) return -1;
                ulong byteOffset = ReadLittleEndianUInt64(data, (int)levelIndex);
                ulong byteLength = ReadLittleEndianUInt64(data, (int)levelIndex + 8);
                if (byteOffset > int.MaxValue || byteLength > int.MaxValue) return -1;
                end = Math.Max(end, offset + (long)byteOffset + (long)byteLength);
                levelIndex += 24;
            }
            return end > offset + 80 && end <= length ? (int)end : -1;
        }

        private static int FindPvr3End(byte[] data, int length, int offset)
        {
            if (offset + 52 > length) return -1;
            uint metaSize = ReadLittleEndianUInt32(data, offset + 44);
            int height = ReadLittleEndianInt32(data, offset + 24);
            int width = ReadLittleEndianInt32(data, offset + 28);
            if (width <= 0 || height <= 0 || width > 32768 || height > 32768) return -1;
            long next = MinPositiveLong(
                FindBounded(data, length, Png, offset + 52),
                FindBounded(data, length, Jpeg, offset + 52),
                FindBounded(data, length, Dds, offset + 52),
                FindBounded(data, length, Ktx1, offset + 52),
                FindBounded(data, length, Pvr3, offset + 52));
            long minimum = offset + 52L + metaSize;
            return next > minimum && next <= length ? (int)next : -1;
        }

        private static int FindPkmEnd(byte[] data, int length, int offset)
        {
            if (offset + 16 > length) return -1;
            int width = ReadBigEndianUInt16(data, offset + 12);
            int height = ReadBigEndianUInt16(data, offset + 14);
            if (width <= 0 || height <= 0 || width > 32768 || height > 32768) return -1;
            long dataBytes = ((long)Math.Max(1, (width + 3) / 4)) * Math.Max(1, (height + 3) / 4) * 8;
            long end = offset + 16L + dataBytes;
            return end <= length ? (int)end : -1;
        }

        private static int FindAstcEnd(byte[] data, int length, int offset)
        {
            if (offset + 16 > length) return -1;
            int blockX = data[offset + 4];
            int blockY = data[offset + 5];
            int width = data[offset + 7] | (data[offset + 8] << 8) | (data[offset + 9] << 16);
            int height = data[offset + 10] | (data[offset + 11] << 8) | (data[offset + 12] << 16);
            if (blockX <= 0 || blockY <= 0 || width <= 0 || height <= 0 || width > 32768 || height > 32768) return -1;
            long blocks = ((long)(width + blockX - 1) / blockX) * ((height + blockY - 1) / blockY);
            long end = offset + 16L + blocks * 16L;
            return end <= length ? (int)end : -1;
        }

        private static int FindCrnEnd(byte[] data, int length, int offset)
        {
            if (offset + 16 > length) return -1;
            uint size = ReadBigEndianUInt32(data, offset + 2);
            long end = offset + (long)size;
            return size >= 16 && end <= length ? (int)end : -1;
        }

        private static int FindQoiEnd(byte[] data, int length, int offset)
        {
            if (offset + 22 > length) return -1;
            int width = ReadBigEndianInt32(data, offset + 4);
            int height = ReadBigEndianInt32(data, offset + 8);
            if (width <= 0 || height <= 0 || width > 32768 || height > 32768) return -1;
            int end = FindBounded(data, length, QoiEndMarker, offset + 14);
            return end > offset ? end + QoiEndMarker.Length : -1;
        }

        private static int FindTlgEnd(byte[] data, int length, int offset)
        {
            int next = MinPositive(
                FindBounded(data, length, Tlg0, offset + 1),
                FindBounded(data, length, Tlg5, offset + 1),
                FindBounded(data, length, Tlg6, offset + 1),
                FindBounded(data, length, Png, offset + 1),
                FindBounded(data, length, Jpeg, offset + 1));
            return next > offset ? next : -1;
        }

        private static string GetRiffExtension(byte[] data, int length, int offset)
        {
            if (offset + 12 > length) return null;
            string type = Encoding.ASCII.GetString(data, offset + 8, 4);
            if (type == "WAVE") return ".wav";
            if (type == "WEBP") return ".webp";
            return null;
        }

        private static int Find(byte[] data, byte[] signature, int offset)
        {
            if (data == null) return -1;
            return FindBounded(data, data.Length, signature, offset);
        }

        private static int FindBounded(byte[] data, int length, byte[] signature, int offset)
        {
            if (data == null || signature == null || signature.Length == 0)
                return -1;
            // Boyer-Moore-Horspool: sub-linear skips on misses (the common case),
            // identical leftmost-match semantics to the previous naive scan.
            if (length > data.Length) length = data.Length;
            int[] skip = GetSkipTable(signature);
            int limit = length - signature.Length;
            int index = Math.Max(offset, 0);
            while (index <= limit)
            {
                int match = signature.Length - 1;
                while (match >= 0 && data[index + match] == signature[match]) match--;
                if (match < 0) return index;
                index += skip[data[index + signature.Length - 1]];
            }
            return -1;
        }

        private static readonly object SkipTableSync = new object();
        private static readonly Dictionary<byte[], int[]> SkipTables = BuildSkipTables();

        private static Dictionary<byte[], int[]> BuildSkipTables()
        {
            Dictionary<byte[], int[]> tables = new Dictionary<byte[], int[]>();
            byte[][] known = new byte[][]
            {
                Png, Jpeg, Gif, Ogg, Riff, Webm, Mp3Id3, Dds, Ktx1, Ktx2,
                Pvr3, Pkm, Astc, Crn, Qoi, Tlg0, Tlg5, Tlg6, QoiEndMarker
            };
            for (int i = 0; i < known.Length; i++)
                tables[known[i]] = BuildSkipTable(known[i]);
            return tables;
        }

        private static int[] BuildSkipTable(byte[] signature)
        {
            int[] skip = new int[256];
            for (int i = 0; i < skip.Length; i++) skip[i] = signature.Length;
            for (int i = 0; i + 1 < signature.Length; i++) skip[signature[i]] = signature.Length - 1 - i;
            return skip;
        }

        private static int[] GetSkipTable(byte[] signature)
        {
            int[] skip;
            lock (SkipTableSync)
            {
                if (!SkipTables.TryGetValue(signature, out skip))
                {
                    skip = BuildSkipTable(signature);
                    SkipTables[signature] = skip;
                }
            }
            return skip;
        }

        private static int ReadBigEndianInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static int ReadBigEndianUInt16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static int ReadLittleEndianInt32(byte[] data, int offset)
        {
            return data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24;
        }

        private static uint ReadLittleEndianUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
        }

        private static ulong ReadLittleEndianUInt64(byte[] data, int offset)
        {
            uint lo = ReadLittleEndianUInt32(data, offset);
            uint hi = ReadLittleEndianUInt32(data, offset + 4);
            return lo | ((ulong)hi << 32);
        }

        private static long Align4(long value)
        {
            return (value + 3) & ~3L;
        }

        private static int MinPositive(params int[] values)
        {
            int result = -1;
            foreach (int value in values)
                if (value > 0 && (result < 0 || value < result)) result = value;
            return result;
        }

        private static long MinPositiveLong(params long[] values)
        {
            long result = -1;
            foreach (long value in values)
                if (value > 0 && (result < 0 || value < result)) result = value;
            return result;
        }

        private static string MakeSourceFolder(string root, string source)
        {
            string relative;
            try { relative = ExtractionPathUtils.MakeRelativePath(root, source); }
            catch { relative = Path.GetFileName(source); }
            return Path.Combine(Path.GetDirectoryName(relative) ?? "", Path.GetFileNameWithoutExtension(relative));
        }

        private static string AppendSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private sealed class Counter
        {
            public int Extracted;
            public long Bytes;
            public int Renamed;
            public int Skipped;
            private readonly HashSet<string> hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> offsets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> manifest = new List<string>();
            private readonly List<string> zipDiagnostics = new List<string>();

            public bool TryReserveDiscovery(string folder, long offset, string hash)
            {
                string offsetKey = folder + "\t" + offset.ToString();
                if (!offsets.Add(offsetKey)) return false;
                if (!hashes.Add(hash))
                {
                    Skipped++;
                    return false;
                }
                return true;
            }

            public void AddDiscovery(string extension, long offset, int length, string destination, string hash)
            {
                if (string.IsNullOrWhiteSpace(extension)) extension = "<unknown>";
                manifest.Add(extension.TrimStart('.')
                    + "\t0x" + offset.ToString("X")
                    + "\t" + length
                    + "\t" + hash
                    + "\t" + destination);
            }

            public void AddZipDiagnostic(string archive, string entry, string message)
            {
                zipDiagnostics.Add(archive + "\t" + entry + "\t" + (message ?? "").Replace("\r", " ").Replace("\n", " "));
            }

            public void WriteReports(string outputDir)
            {
                try
                {
                    if (manifest.Count > 0)
                    {
                        List<string> lines = new List<string> { "type\toffset\tbytes\tsha256\toutput" };
                        lines.AddRange(manifest);
                        File.WriteAllLines(Path.Combine(outputDir, "GameAssetTool-signatures.tsv"), lines.ToArray(), new UTF8Encoding(false));
                    }
                    if (zipDiagnostics.Count > 0)
                    {
                        List<string> lines = new List<string> { "archive\tentry\tmessage" };
                        lines.AddRange(zipDiagnostics);
                        File.WriteAllLines(Path.Combine(outputDir, "GameAssetTool-zip-diagnostics.tsv"), lines.ToArray(), new UTF8Encoding(false));
                    }
                }
                catch { }
            }

            public CollectorResult ToResult()
            {
                return new CollectorResult(Extracted, Bytes, Renamed, Skipped);
            }
        }
    }
}
