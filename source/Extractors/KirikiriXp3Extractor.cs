using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class Xp3ExtractionResult
    {
        public Xp3ExtractionResult()
        {
            Extracted = 0;
            Bytes = 0;
            Errors = 0;
            Renamed = 0;
        }

        public int Extracted { get; set; }
        public long Bytes { get; set; }
        public int Errors { get; set; }
        public int Renamed { get; set; }
    }

    internal static class KirikiriXp3Extractor
    {
        private static readonly byte[] Magic = new byte[]
        {
            (byte)'X', (byte)'P', (byte)'3', 0x0D, 0x0A, 0x20, 0x0A, 0x1A, 0x8B, 0x67, 0x01
        };
        private const byte IndexContinues = 0x80;
        private const byte IndexCompressed = 0x01;
        private const uint SegmentCompressed = 0x01;
        private const uint EntryEncrypted = 0x80000000;
        private static readonly byte[] Tlg0 = Encoding.ASCII.GetBytes("TLG0.0\0sds\x1A");
        private static readonly byte[] Tlg5 = Encoding.ASCII.GetBytes("TLG5.0\0raw\x1A");
        private static readonly byte[] Tlg6 = Encoding.ASCII.GetBytes("TLG6.0\0raw\x1A");
        private const long MaxTlgPixels = 4096L * 4096L;
        private static readonly uint[] CrcTable = BuildCrcTable();

        public static bool HasStandardArchives(string gamePath, string outputPath)
        {
            try
            {
                foreach (string archive in FindArchives(gamePath, outputPath))
                {
                    try
                    {
                        using (FileStream stream = File.OpenRead(archive))
                        {
                            if (HasMagic(stream)) return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        public static List<string> FindArchives(string gamePath, string outputPath)
        {
            List<string> archives = new List<string>();
            if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath)) return archives;
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
                    }
                    catch { }
                    pending.Push(subdirs[i]);
                }
                string[] files;
                try { files = Directory.GetFiles(directory, "*.xp3"); }
                catch { continue; }
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
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

        public static Xp3ExtractionResult ExtractAll(string gamePath, string outputPath, Action<string> log, Func<bool> isCancelled)
        {
            Xp3ExtractionResult total = new Xp3ExtractionResult();
            Directory.CreateDirectory(outputPath);
            List<string> archives = FindArchives(gamePath, outputPath);
            for (int i = 0; i < archives.Count; i++)
            {
                if (isCancelled != null && isCancelled())
                    throw new OperationCanceledException();
                string archive = archives[i];
                if (log != null)
                    log("Processing XP3: " + MakeRelativeSafe(gamePath, archive));
                try
                {
                    Xp3ExtractionResult one = ExtractArchive(archive, outputPath, log);
                    total.Extracted += one.Extracted;
                    total.Bytes += one.Bytes;
                    total.Renamed += one.Renamed;
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

        public static Xp3ExtractionResult ExtractArchive(string archivePath, string outputPath, Action<string> log)
        {
            Xp3ExtractionResult result = new Xp3ExtractionResult();
            string archiveOutput = Path.Combine(outputPath, "archives", NormalizeName(Path.GetFileNameWithoutExtension(archivePath)));
            using (FileStream stream = File.OpenRead(archivePath))
            {
                if (!HasMagic(stream))
                    throw new InvalidDataException("XP3 header was not found.");
                stream.Seek(Magic.Length, SeekOrigin.Begin);
                long indexOffset = (long)ReadU64(stream);
                byte[] index = ReadIndex(stream, indexOffset);
                List<Xp3Entry> entries = ParseEntries(index);
                foreach (Xp3Entry entry in entries)
                {
                    byte[] content;
                    using (MemoryStream assembled = new MemoryStream((int)Math.Min((long)entry.ExpectedSize, 8L * 1024L * 1024L)))
                    {
                        foreach (Xp3Segment segment in entry.Segments)
                        {
                            stream.Seek(segment.Offset, SeekOrigin.Begin);
                            byte[] stored = ReadExact(stream, segment.StoredSize);
                            if ((segment.Flags & SegmentCompressed) != 0)
                                stored = Inflate(stored);
                            if (stored.Length != (long)segment.OriginalSize)
                                throw new InvalidDataException("XP3 segment size mismatch for " + entry.FileName);
                            assembled.Write(stored, 0, stored.Length);
                        }
                        content = assembled.ToArray();
                    }
                    if (content.Length != (long)entry.ExpectedSize)
                        throw new InvalidDataException("XP3 file size mismatch for " + entry.FileName);
                    bool renamed;
                    string destination = ExtractionPathUtils.GetUniqueFilePath(
                        ExtractionPathUtils.GetSafeOutputPath(archiveOutput, entry.FileName), out renamed);
                    string parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    File.WriteAllBytes(destination, content);
                    result.Extracted++;
                    result.Bytes += content.Length;
                    if (renamed) result.Renamed++;
                    Xp3ExtractionResult preview = TrySaveTlgPreview(content, destination, log);
                    result.Extracted += preview.Extracted;
                    result.Bytes += preview.Bytes;
                    result.Renamed += preview.Renamed;
                }
            }
            return result;
        }

        private static bool HasMagic(Stream stream)
        {
            long saved = 0;
            try { saved = stream.Position; stream.Seek(0, SeekOrigin.Begin); }
            catch { return false; }
            byte[] header = new byte[Magic.Length];
            int read = 0;
            try { read = stream.Read(header, 0, header.Length); }
            catch { return false; }
            finally
            {
                try { stream.Seek(saved, SeekOrigin.Begin); }
                catch { }
            }
            if (read != Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++)
                if (header[i] != Magic[i]) return false;
            return true;
        }

        private static byte[] ReadIndex(FileStream stream, long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            int mode = 0;
            while (true)
            {
                mode = stream.ReadByte();
                if (mode < 0) throw new EndOfStreamException("Unexpected end of XP3 archive.");
                if (mode != IndexContinues) break;
                ReadExact(stream, 8);
                stream.Seek((long)ReadU64(stream), SeekOrigin.Begin);
            }
            if (mode == IndexCompressed)
            {
                long compressedSize = (long)ReadU64(stream);
                long originalSize = (long)ReadU64(stream);
                byte[] block = Inflate(ReadExact(stream, compressedSize));
                if (block.Length != originalSize)
                    throw new InvalidDataException("XP3 index size mismatch.");
                return block;
            }
            if (mode == 0)
                return ReadExact(stream, (long)ReadU64(stream));
            throw new InvalidDataException("Unsupported XP3 index flags: " + mode);
        }

        private sealed class Xp3Segment
        {
            public uint Flags;
            public long Offset;
            public ulong OriginalSize;
            public long StoredSize;
        }

        private sealed class Xp3Entry
        {
            public string FileName;
            public ulong ExpectedSize;
            public List<Xp3Segment> Segments = new List<Xp3Segment>();
        }

        private static List<Xp3Entry> ParseEntries(byte[] index)
        {
            List<Xp3Entry> entries = new List<Xp3Entry>();
            int position = 0;
            while (position + 12 <= index.Length)
            {
                string chunkName = Encoding.ASCII.GetString(index, position, 4);
                ulong chunkSize = ReadU64(index, position + 4);
                position += 12;
                if (chunkSize > (ulong)(index.Length - position))
                    throw new InvalidDataException("Broken XP3 chunk size.");
                int end = position + (int)chunkSize;
                if (chunkName == "File")
                {
                    string fileName = null;
                    ulong expectedSize = 0;
                    List<Xp3Segment> segments = new List<Xp3Segment>();
                    int inner = position;
                    while (inner + 12 <= end)
                    {
                        string name = Encoding.ASCII.GetString(index, inner, 4);
                        ulong size = ReadU64(index, inner + 4);
                        inner += 12;
                        if (size > (ulong)(end - inner))
                            throw new InvalidDataException("Broken XP3 chunk size.");
                        int valueEnd = inner + (int)size;
                        if (name == "info")
                        {
                            if (size < 22) throw new InvalidDataException("Broken XP3 info chunk.");
                            uint flags = ReadU32(index, inner);
                            ulong originalSize = ReadU64(index, inner + 4);
                            ushort nameLength = ReadU16(index, inner + 20);
                            if ((flags & EntryEncrypted) != 0)
                                throw new InvalidDataException("Encrypted XP3 entries are not supported yet.");
                            if (22 + nameLength * 2 > (int)size)
                                throw new InvalidDataException("Broken XP3 info chunk.");
                            fileName = NormalizeName(Encoding.Unicode.GetString(index, inner + 22, nameLength * 2));
                            expectedSize = originalSize;
                        }
                        else if (name == "segm")
                        {
                            if (size % 28 != 0) throw new InvalidDataException("Broken XP3 segment chunk.");
                            for (int offset = inner; offset + 28 <= valueEnd; offset += 28)
                            {
                                Xp3Segment segment = new Xp3Segment();
                                segment.Flags = ReadU32(index, offset);
                                segment.Offset = (long)ReadU64(index, offset + 4);
                                segment.OriginalSize = ReadU64(index, offset + 12);
                                segment.StoredSize = (long)ReadU64(index, offset + 20);
                                segments.Add(segment);
                            }
                        }
                        inner = valueEnd;
                    }
                    if (fileName != null && segments.Count > 0)
                    {
                        Xp3Entry entry = new Xp3Entry();
                        entry.FileName = fileName;
                        entry.ExpectedSize = expectedSize;
                        entry.Segments = segments;
                        entries.Add(entry);
                    }
                }
                position = end;
            }
            return entries;
        }

        private static Xp3ExtractionResult TrySaveTlgPreview(byte[] content, string destination, Action<string> log)
        {
            Xp3ExtractionResult result = new Xp3ExtractionResult();
            if (!StartsWith(content, Tlg0) && !StartsWith(content, Tlg5) && !StartsWith(content, Tlg6))
                return result;
            byte[] wrapped = content;
            if (StartsWith(content, Tlg0) && content.Length >= 15)
            {
                wrapped = new byte[content.Length - 15];
                Buffer.BlockCopy(content, 15, wrapped, 0, wrapped.Length);
            }
            if (StartsWith(wrapped, Tlg6))
            {
                try
                {
                    File.WriteAllText(Path.ChangeExtension(destination, null) + ".preview-note.txt",
                        "TLG6 image was extracted as the original .tlg file." + Environment.NewLine
                        + "Built-in PNG preview conversion currently supports TLG5 only.",
                        new UTF8Encoding(false));
                }
                catch { }
                if (log != null)
                    log("WARN:" + Path.GetFileName(destination) + ": TLG6 preview conversion is not supported yet; original TLG was kept.");
                return result;
            }
            if (!StartsWith(wrapped, Tlg5)) return result;
            byte[] rgba = DecodeTlg5(content);
            int width = tlgWidth;
            int height = tlgHeight;
            bool renamed;
            string preview = ExtractionPathUtils.GetUniqueFilePath(
                Path.ChangeExtension(destination, null) + ".png", out renamed);
            File.WriteAllBytes(preview, EncodePng(rgba, width, height));
            result.Extracted = 1;
            try { result.Bytes = new FileInfo(preview).Length; }
            catch { result.Bytes = 0; }
            if (renamed) result.Renamed = 1;
            return result;
        }

        private static int tlgWidth;
        private static int tlgHeight;

        private static byte[] DecodeTlg5(byte[] content)
        {
            byte[] input = content;
            if (StartsWith(input, Tlg0))
            {
                if (input.Length < 15) throw new InvalidDataException("TLG0 wrapper is truncated.");
                byte[] stripped = new byte[input.Length - 15];
                Buffer.BlockCopy(input, 15, stripped, 0, stripped.Length);
                input = stripped;
            }
            if (!StartsWith(input, Tlg5) || input.Length < 24)
                throw new InvalidDataException("TLG5 header was not found.");
            int channels = input[11];
            int width = (int)ReadU32(input, 12);
            int height = (int)ReadU32(input, 16);
            int blockHeight = (int)ReadU32(input, 20);
            if ((channels != 3 && channels != 4) || width <= 0 || height <= 0 || blockHeight <= 0
                || (long)width * (long)height > MaxTlgPixels)
                throw new InvalidDataException("Unsupported TLG5 dimensions or channel count.");
            tlgWidth = width;
            tlgHeight = height;
            int position = 24 + ((height - 1) / blockHeight + 1) * 4;
            byte[] dictionary = new byte[4096];
            int dictPos = 0;
            byte[] pixels = new byte[(long)width * (long)height * 4];
            byte[] previous = new byte[width * 4];
            for (int blockY = 0; blockY < height; blockY += blockHeight)
            {
                int rows = Math.Min(blockHeight, height - blockY);
                int planeSize = width * rows;
                byte[][] planes = new byte[channels][];
                for (int c = 0; c < channels; c++)
                {
                    if (position + 5 > input.Length)
                        throw new InvalidDataException("TLG5 block header is truncated.");
                    byte marker = input[position];
                    int size = (int)ReadU32(input, position + 1);
                    position += 5;
                    if (size < 0 || position + size > input.Length)
                        throw new InvalidDataException("TLG5 block is truncated.");
                    if (marker == 0)
                    {
                        planes[c] = LzssDecompress(input, position, size, planeSize, dictionary, ref dictPos);
                    }
                    else
                    {
                        planes[c] = new byte[planeSize];
                        Buffer.BlockCopy(input, position, planes[c], 0, planeSize);
                    }
                    position += size;
                    if (planes[c].Length != planeSize)
                        throw new InvalidDataException("TLG5 raw plane size mismatch.");
                }
                for (int row = 0; row < rows; row++)
                {
                    byte[] current = new byte[width * 4];
                    int previousR = 0;
                    int previousG = 0;
                    int previousB = 0;
                    int previousA = 0;
                    int rowOffset = row * width;
                    for (int x = 0; x < width; x++)
                    {
                        int index = rowOffset + x;
                        int blue = (planes[0][index] + planes[1][index]) & 0xFF;
                        int green = planes[1][index];
                        int red = (planes[2][index] + green) & 0xFF;
                        int alpha = channels == 4 ? planes[3][index] : 0xFF;
                        previousR = (previousR + red + previous[x * 4]) & 0xFF;
                        previousG = (previousG + green + previous[x * 4 + 1]) & 0xFF;
                        previousB = (previousB + blue + previous[x * 4 + 2]) & 0xFF;
                        previousA = channels == 4 ? (previousA + alpha + previous[x * 4 + 3]) & 0xFF : 0xFF;
                        current[x * 4] = (byte)previousR;
                        current[x * 4 + 1] = (byte)previousG;
                        current[x * 4 + 2] = (byte)previousB;
                        current[x * 4 + 3] = (byte)previousA;
                    }
                    Buffer.BlockCopy(current, 0, pixels, (blockY + row) * width * 4, width * 4);
                    previous = current;
                }
            }
            return pixels;
        }

        private static byte[] LzssDecompress(byte[] input, int offset, int length, int outputSize, byte[] dictionary, ref int dictPos)
        {
            byte[] output = new byte[outputSize];
            int outPos = 0;
            int end = offset + length;
            int position = offset;
            int flags = 0;
            while (position < end && outPos < outputSize)
            {
                flags >>= 1;
                if ((flags & 0x100) != 0x100)
                {
                    if (position >= end) throw new InvalidDataException("TLG5 LZSS block is truncated.");
                    flags = input[position] | 0xFF00;
                    position++;
                }
                if ((flags & 1) != 0)
                {
                    if (position + 2 > end) throw new InvalidDataException("TLG5 LZSS block is truncated.");
                    int first = input[position];
                    int second = input[position + 1];
                    position += 2;
                    int dictionaryPosition = first | (second & 0x0F) << 8;
                    int run = 3 + (second >> 4);
                    if (run == 18)
                    {
                        if (position >= end) throw new InvalidDataException("TLG5 LZSS run is truncated.");
                        run += input[position];
                        position++;
                    }
                    for (int i = 0; i < run; i++)
                    {
                        byte value = dictionary[dictionaryPosition];
                        dictionaryPosition = (dictionaryPosition + 1) & 0xFFF;
                        dictionary[dictPos] = value;
                        dictPos = (dictPos + 1) & 0xFFF;
                        if (outPos >= outputSize) throw new InvalidDataException("TLG5 LZSS output size mismatch.");
                        output[outPos++] = value;
                        if (outPos == outputSize) break;
                    }
                }
                else
                {
                    if (position >= end) throw new InvalidDataException("TLG5 LZSS literal is truncated.");
                    byte value = input[position++];
                    dictionary[dictPos] = value;
                    dictPos = (dictPos + 1) & 0xFFF;
                    if (outPos >= outputSize) throw new InvalidDataException("TLG5 LZSS output size mismatch.");
                    output[outPos++] = value;
                }
            }
            if (outPos != outputSize)
                throw new InvalidDataException("TLG5 LZSS output size mismatch.");
            return output;
        }

        private static byte[] EncodePng(byte[] rgba, int width, int height)
        {
            using (MemoryStream output = new MemoryStream())
            {
                byte[] signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
                output.Write(signature, 0, signature.Length);
                byte[] ihdr = new byte[13];
                WriteU32BigEndian(ihdr, 0, (uint)width);
                WriteU32BigEndian(ihdr, 4, (uint)height);
                ihdr[8] = 8;
                ihdr[9] = 6;
                WriteChunk(output, "IHDR", ihdr);
                using (MemoryStream raw = new MemoryStream((width * 4 + 1) * height))
                {
                    byte[] row = new byte[width * 4 + 1];
                    for (int y = 0; y < height; y++)
                    {
                        row[0] = 0;
                        Buffer.BlockCopy(rgba, y * width * 4, row, 1, width * 4);
                        raw.Write(row, 0, row.Length);
                    }
                    raw.Seek(0, SeekOrigin.Begin);
                    using (MemoryStream compressed = new MemoryStream())
                    {
                        byte[] deflated;
                        using (MemoryStream deflateBytes = new MemoryStream())
                        {
                            using (DeflateStream deflate = new DeflateStream(deflateBytes, CompressionMode.Compress))
                                raw.CopyTo(deflate);
                            deflated = deflateBytes.ToArray();
                        }
                        compressed.WriteByte(0x78);
                        compressed.WriteByte(0x9C);
                        compressed.Write(deflated, 0, deflated.Length);
                        WriteU32BigEndian(compressed, Adler32(raw.ToArray()));
                        WriteChunk(output, "IDAT", compressed.ToArray());
                    }
                }
                WriteChunk(output, "IEND", new byte[0]);
                return output.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint mod = 65521;
            uint a = 1;
            uint b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % mod;
                b = (b + a) % mod;
            }
            return (b << 16) | a;
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            WriteU32BigEndian(output, (uint)data.Length);
            output.Write(typeBytes, 0, typeBytes.Length);
            if (data.Length > 0)
                output.Write(data, 0, data.Length);
            uint crc = Crc32(typeBytes, data);
            WriteU32BigEndian(output, crc);
        }

        private static uint Crc32(byte[] typeBytes, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < typeBytes.Length; i++)
                crc = CrcTable[(crc ^ typeBytes[i]) & 0xFF] ^ (crc >> 8);
            for (int i = 0; i < data.Length; i++)
                crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }

        private static void WriteU32BigEndian(Stream output, uint value)
        {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void WriteU32BigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static byte[] Inflate(byte[] stored)
        {
            if (stored.Length < 2)
                throw new InvalidDataException("XP3 zlib block is truncated.");
            using (MemoryStream input = new MemoryStream(stored, 2, stored.Length - 2))
            using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
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

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[i] != prefix[i]) return false;
            return true;
        }

        private static byte[] ReadExact(Stream stream, long size)
        {
            if (size < 0 || size > int.MaxValue)
                throw new InvalidDataException("XP3 block size is not supported: " + size);
            byte[] buffer = new byte[(int)size];
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0) throw new EndOfStreamException("Unexpected end of XP3 archive.");
                offset += read;
            }
            return buffer;
        }

        private static ulong ReadU64(Stream stream)
        {
            byte[] buffer = ReadExact(stream, 8);
            return ReadU64(buffer, 0);
        }

        private static ulong ReadU64(byte[] buffer, int offset)
        {
            uint low = ReadU32(buffer, offset);
            uint high = ReadU32(buffer, offset + 4);
            return ((ulong)high << 32) | low;
        }

        private static uint ReadU32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
        }

        private static ushort ReadU16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }
    }
}
