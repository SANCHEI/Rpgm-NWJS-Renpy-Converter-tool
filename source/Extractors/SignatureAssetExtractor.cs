using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class SignatureAssetExtractor
    {
        private const long MaxScannedFileBytes = 512L * 1024L * 1024L;
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF8");
        private static readonly byte[] Ogg = Encoding.ASCII.GetBytes("OggS");
        private static readonly byte[] Riff = Encoding.ASCII.GetBytes("RIFF");
        private static readonly HashSet<string> ZipExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".apk", ".jar", ".nw"
        };

        public static CollectorResult Extract(string inputPath, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            Counter counter = new Counter();
            string root = AssetCollectors.InputDirectory(inputPath);
            foreach (string source in FindFiles(inputPath, outputDir))
            {
                try
                {
                    FileInfo info = new FileInfo(source);
                    if (info.Length <= MaxScannedFileBytes)
                    {
                        byte[] data = File.ReadAllBytes(source);
                        string relative = MakeSourceFolder(root, source);
                        ExtractBytes(data, outputDir, Path.Combine("embedded", relative), counter);
                    }
                    else
                    {
                        counter.Skipped++;
                    }

                    if (ZipExtensions.Contains(Path.GetExtension(source)))
                        ExtractZip(source, outputDir, counter);
                }
                catch
                {
                    counter.Skipped++;
                }
            }
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

        private static void ExtractZip(string source, string outputDir, Counter counter)
        {
            using (FileStream stream = File.OpenRead(source))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ArchiveSafetyPolicy.ValidateZipArchive(archive);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
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
                                int read = input.Read(data, offset, data.Length - offset);
                                if (read <= 0) throw new EndOfStreamException("ZIP entry is truncated.");
                                offset += read;
                            }
                        }
                        string folder = Path.Combine("archives", Path.GetFileNameWithoutExtension(source), ArchiveSafetyPolicy.NormalizeEntryPath(entry.FullName));
                        ExtractBytes(data, outputDir, folder, counter);
                    }
                    catch
                    {
                        counter.Skipped++;
                    }
                }
            }
        }

        private static void ExtractBytes(byte[] data, string outputDir, string folder, Counter counter)
        {
            ExtractDelimited(data, Png, FindPngEnd, ".png", outputDir, folder, counter);
            ExtractDelimited(data, Jpeg, FindJpegEnd, ".jpg", outputDir, folder, counter);
            ExtractDelimited(data, Gif, FindGifEnd, ".gif", outputDir, folder, counter);
            ExtractDelimited(data, Ogg, FindOggEnd, ".ogg", outputDir, folder, counter);
            ExtractDelimited(data, Riff, FindRiffEnd, null, outputDir, folder, counter);
        }

        private static void ExtractDelimited(byte[] data, byte[] signature, Func<byte[], int, int> findEnd, string extension, string outputDir, string folder, Counter counter)
        {
            int offset = 0;
            while ((offset = Find(data, signature, offset)) >= 0)
            {
                int end = findEnd(data, offset);
                string resolvedExtension = extension ?? GetRiffExtension(data, offset);
                if (end <= offset || string.IsNullOrEmpty(resolvedExtension))
                {
                    counter.Skipped++;
                    offset++;
                    continue;
                }

                Write(data, offset, end, resolvedExtension, outputDir, folder, counter);
                offset = end;
            }
        }

        private static void Write(byte[] data, int start, int end, string extension, string outputDir, string folder, Counter counter)
        {
            string relative = Path.Combine(folder, "asset-" + (counter.Extracted + 1).ToString("0000") + extension);
            string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, relative);
            bool renamed;
            destination = ExtractionPathUtils.GetUniqueFilePath(destination, out renamed);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (FileStream output = File.Create(destination))
                output.Write(data, start, end - start);
            counter.Extracted++;
            counter.Bytes += end - start;
            if (renamed) counter.Renamed++;
        }

        private static int FindPngEnd(byte[] data, int offset)
        {
            int position = offset + Png.Length;
            while (position + 12 <= data.Length)
            {
                int size = ReadBigEndianInt32(data, position);
                if (size < 0 || position > data.Length - size - 12) return -1;
                bool isEnd = data[position + 4] == (byte)'I'
                    && data[position + 5] == (byte)'E'
                    && data[position + 6] == (byte)'N'
                    && data[position + 7] == (byte)'D';
                position += size + 12;
                if (isEnd) return size == 0 ? position : -1;
            }
            return -1;
        }

        private static int FindJpegEnd(byte[] data, int offset)
        {
            for (int index = offset + Jpeg.Length; index + 1 < data.Length; index++)
                if (data[index] == 0xFF && data[index + 1] == 0xD9) return index + 2;
            return -1;
        }

        private static int FindGifEnd(byte[] data, int offset)
        {
            if (offset + 6 > data.Length
                || (data[offset + 4] != (byte)'7' && data[offset + 4] != (byte)'9')
                || data[offset + 5] != (byte)'a')
                return -1;
            for (int index = offset + 6; index < data.Length; index++)
                if (data[index] == 0x3B) return index + 1;
            return -1;
        }

        private static int FindOggEnd(byte[] data, int offset)
        {
            int position = offset;
            while (position + 27 <= data.Length && Find(data, Ogg, position) == position)
            {
                int segments = data[position + 26];
                if (position + 27 + segments > data.Length) return -1;
                int pageBytes = 27 + segments;
                for (int index = 0; index < segments; index++) pageBytes += data[position + 27 + index];
                if (position + pageBytes > data.Length) return -1;
                bool isEnd = (data[position + 5] & 0x04) != 0;
                position += pageBytes;
                if (isEnd) return position;
            }
            return -1;
        }

        private static int FindRiffEnd(byte[] data, int offset)
        {
            if (offset + 12 > data.Length) return -1;
            long end = (long)offset + 8 + ReadLittleEndianUInt32(data, offset + 4);
            return end <= data.Length && end > offset + 12 ? (int)end : -1;
        }

        private static string GetRiffExtension(byte[] data, int offset)
        {
            if (offset + 12 > data.Length) return null;
            string type = Encoding.ASCII.GetString(data, offset + 8, 4);
            if (type == "WAVE") return ".wav";
            if (type == "WEBP") return ".webp";
            return null;
        }

        private static int Find(byte[] data, byte[] signature, int offset)
        {
            for (int index = Math.Max(offset, 0); index <= data.Length - signature.Length; index++)
            {
                int match = 0;
                while (match < signature.Length && data[index + match] == signature[match]) match++;
                if (match == signature.Length) return index;
            }
            return -1;
        }

        private static int ReadBigEndianInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static uint ReadLittleEndianUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
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

            public CollectorResult ToResult()
            {
                return new CollectorResult(Extracted, Bytes, Renamed, Skipped);
            }
        }
    }
}
