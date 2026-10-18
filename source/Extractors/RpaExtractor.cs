using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class RpaExtractionResult
    {
        public RpaExtractionResult()
        {
            Extracted = 0;
            Bytes = 0;
        }

        public int Extracted { get; set; }
        public long Bytes { get; set; }
    }

    internal static class RpaExtractor
    {
        private const uint AltExtraKey = 0xDABE8DF0;

        public static bool HasNativeSupport(string archivePath)
        {
            try
            {
                RpaIndex index = ReadIndex(archivePath);
                return index.Entries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static RpaExtractionResult ExtractArchive(string archivePath, string outputPath, Action<string> log)
        {
            RpaExtractionResult result = new RpaExtractionResult();
            RpaIndex index = ReadIndex(archivePath);
            using (FileStream stream = File.OpenRead(archivePath))
            {
                foreach (RpaEntry entry in index.Entries)
                {
                    bool renamed;
                    string destination = ExtractionPathUtils.GetUniqueFilePath(
                        ExtractionPathUtils.GetSafeOutputPath(outputPath, entry.RelativePath), out renamed);
                    string parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);
                    using (FileStream output = File.Create(destination))
                    {
                        // Parity with unrpa: only the first part of an entry is
                        // extracted (ArchiveView reads a single offset/length/prefix).
                        RpaPart part = entry.Parts[0];
                        if (part.Prefix != null && part.Prefix.Length > 0)
                            output.Write(part.Prefix, 0, part.Prefix.Length);
                        stream.Seek(part.Offset, SeekOrigin.Begin);
                        CopyExact(stream, output, part.Length);
                    }
                    result.Extracted++;
                    try { result.Bytes += new FileInfo(destination).Length; }
                    catch { }
                }
            }
            return result;
        }

        private sealed class RpaIndex
        {
            public readonly List<RpaEntry> Entries = new List<RpaEntry>();
        }

        private sealed class RpaEntry
        {
            public string RelativePath;
            public readonly List<RpaPart> Parts = new List<RpaPart>();
        }

        private sealed class RpaPart
        {
            public long Offset;
            public long Length;
            public byte[] Prefix;
        }

        private static RpaIndex ReadIndex(string archivePath)
        {
            RpaIndex index = new RpaIndex();
            using (FileStream stream = File.OpenRead(archivePath))
            {
                long offset;
                ulong key;
                bool hasKey;
                DetectArchive(stream, archivePath, out offset, out key, out hasKey);
                stream.Seek(offset, SeekOrigin.Begin);
                byte[] compressed = ReadToEnd(stream);
                byte[] pickled = CompressionHelper.ZlibDecompress(compressed);
                object root = PickleReader.Load(pickled);
                Dictionary<object, object> table = root as Dictionary<object, object>;
                if (table == null)
                    throw new InvalidDataException("RPA index is not a dict.");
                foreach (KeyValuePair<object, object> pair in table)
                {
                    RpaEntry entry = new RpaEntry();
                    entry.RelativePath = NormalizePath(pair.Key);
                    List<object> parts = pair.Value as List<object>;
                    if (parts == null)
                        throw new InvalidDataException("RPA index entry is not a list.");
                    foreach (object item in parts)
                    {
                        List<object> part = item as List<object>;
                        if (part == null || (part.Count != 2 && part.Count != 3))
                            throw new InvalidDataException("RPA index part is malformed.");
                        RpaPart piece = new RpaPart();
                        ulong rawOffset = ToUInt64(part[0]);
                        ulong rawLength = ToUInt64(part[1]);
                        if (hasKey)
                        {
                            rawOffset ^= key;
                            rawLength ^= key;
                        }
                        piece.Offset = (long)rawOffset;
                        piece.Length = (long)rawLength;
                        if (piece.Offset < 0 || piece.Length < 0)
                            throw new InvalidDataException("RPA index part is out of range.");
                        if (part.Count == 3)
                        {
                            byte[] prefix = part[2] as byte[];
                            if (prefix == null)
                            {
                                string text = part[2] as string;
                                prefix = text != null ? Encoding.UTF8.GetBytes(text) : new byte[0];
                            }
                            piece.Prefix = prefix;
                        }
                        entry.Parts.Add(piece);
                    }
                    index.Entries.Add(entry);
                }
            }
            return index;
        }

        private static void DetectArchive(FileStream stream, string archivePath, out long offset, out ulong key, out bool hasKey)
        {
            key = 0;
            hasKey = false;
            if (archivePath.EndsWith(".rpi", StringComparison.OrdinalIgnoreCase))
            {
                offset = 0;
                return;
            }
            stream.Seek(0, SeekOrigin.Begin);
            string header = ReadHeaderLine(stream);
            string[] parts = header.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                throw new InvalidDataException("RPA header was not found.");
            string version = parts[0];
            if (version == "RPA-2.0")
            {
                if (parts.Length < 2)
                    throw new InvalidDataException("RPA-2.0 header is truncated.");
                offset = (long)ParseHex64(parts[1]);
                return;
            }
            if (version == "RPA-3.0" || version == "RPA-3.2" || version == "RPA-4.0")
            {
                if (parts.Length < 3)
                    throw new InvalidDataException(version + " header is truncated.");
                offset = (long)ParseHex64(parts[1]);
                key = ParseHex64(parts[2]);
                hasKey = true;
                return;
            }
            if (version == "ALT-1.0")
            {
                if (parts.Length < 3)
                    throw new InvalidDataException("ALT-1.0 header is truncated.");
                key = ParseHex64(parts[1]) ^ AltExtraKey;
                offset = (long)ParseHex64(parts[2]);
                hasKey = true;
                return;
            }
            throw new InvalidDataException("Unsupported RPA header: " + version);
        }

        private static ulong ParseHex64(string text)
        {
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            if (text.Length == 0 || text.Length > 16)
                throw new InvalidDataException("RPA hex number is malformed.");
            ulong value = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                uint digit;
                if (c >= '0' && c <= '9') digit = (uint)(c - '0');
                else if (c >= 'a' && c <= 'f') digit = (uint)(c - 'a' + 10);
                else if (c >= 'A' && c <= 'F') digit = (uint)(c - 'A' + 10);
                else throw new InvalidDataException("RPA hex number is malformed.");
                value = (value << 4) | digit;
            }
            return value;
        }

        private static string ReadHeaderLine(FileStream stream)
        {
            List<byte> line = new List<byte>();
            while (line.Count < 4096)
            {
                int b = stream.ReadByte();
                if (b < 0 || b == '\n') break;
                if (b != '\r') line.Add((byte)b);
            }
            return Encoding.ASCII.GetString(line.ToArray());
        }

        private static string NormalizePath(object key)
        {
            string path;
            byte[] bytes = key as byte[];
            if (bytes != null)
                path = Encoding.UTF8.GetString(bytes);
            else if (key is string)
                path = (string)key;
            else
                throw new InvalidDataException("RPA index key is not a path.");
            path = path.Replace('/', Path.DirectorySeparatorChar);
            string[] parts = path.Split(Path.DirectorySeparatorChar);
            List<string> clean = new List<string>();
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part) || part == ".") continue;
                if (part == "..") continue;
                clean.Add(part);
            }
            if (clean.Count == 0) return "asset";
            return string.Join(Path.DirectorySeparatorChar.ToString(), clean.ToArray());
        }

        private static ulong ToUInt64(object value)
        {
            if (value is long) return (ulong)(long)value;
            if (value is int) return (ulong)(int)value;
            if (value is ulong) return (ulong)value;
            if (value is bool) return (bool)value ? 1u : 0u;
            throw new InvalidDataException("RPA index number has an unexpected type.");
        }

        private static byte[] ReadToEnd(FileStream stream)
        {
            long remaining = stream.Length - stream.Position;
            if (remaining < 0 || remaining > int.MaxValue)
                throw new InvalidDataException("RPA index blob is too large.");
            byte[] buffer = new byte[(int)remaining];
            int done = 0;
            while (done < buffer.Length)
            {
                int read = stream.Read(buffer, done, buffer.Length - done);
                if (read <= 0) throw new EndOfStreamException("RPA archive is truncated.");
                done += read;
            }
            return buffer;
        }

        private static void CopyExact(Stream input, Stream output, long count)
        {
            byte[] buffer = new byte[81920];
            while (count > 0)
            {
                int want = (int)Math.Min(buffer.Length, count);
                int read = input.Read(buffer, 0, want);
                if (read <= 0) throw new EndOfStreamException("RPA entry is truncated.");
                output.Write(buffer, 0, read);
                count -= read;
            }
        }
    }
}
