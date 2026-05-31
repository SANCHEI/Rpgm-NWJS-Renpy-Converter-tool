using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class LegacyRpgMakerExtractor
    {
        private const uint Version1Key = 0xDEADCAFE;
        private const int MaxNameLength = 1024 * 1024;

        public static CollectorResult ExtractArchive(string archivePath, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;

            using (FileStream stream = File.OpenRead(archivePath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte[] header = reader.ReadBytes(8);
                if (header.Length != 8
                    || Encoding.ASCII.GetString(header, 0, 6) != "RGSSAD"
                    || header[6] != 0)
                {
                    throw new InvalidDataException("Invalid RGSS archive header.");
                }

                List<ArchiveEntry> entries;
                if (header[7] == 1)
                    entries = ReadVersion1Entries(reader);
                else if (header[7] == 3)
                    entries = ReadVersion3Entries(reader);
                else
                    throw new NotSupportedException("Unsupported RGSS archive version: " + header[7] + ".");

                int entryCount = 0;
                long totalBytes = 0;
                foreach (ArchiveEntry entry in entries)
                    ArchiveSafetyPolicy.ValidateEntry(entry.Name, entry.Size, ref entryCount, ref totalBytes);

                foreach (ArchiveEntry entry in entries)
                {
                    try
                    {
                        string destination = GetSafeOutputPath(outputDir, entry.Name);
                        destination = GetUniqueFilePath(destination, ref renamed);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        stream.Position = entry.Offset;
                        using (FileStream output = File.Create(destination))
                            DecryptTo(stream, output, entry.Size, entry.Key);
                        extracted++;
                        bytes += entry.Size;
                    }
                    catch
                    {
                        skipped++;
                    }
                }
            }

            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static List<ArchiveEntry> ReadVersion1Entries(BinaryReader reader)
        {
            List<ArchiveEntry> entries = new List<ArchiveEntry>();
            uint key = Version1Key;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                int nameLength = ReadVersion1Int32(reader, ref key);
                ValidateNameLength(reader, nameLength);
                byte[] encryptedName = reader.ReadBytes(nameLength);
                string name = DecryptVersion1Name(encryptedName, ref key);
                int size = ReadVersion1Int32(reader, ref key);
                ValidateEntry(reader.BaseStream, reader.BaseStream.Position, size);
                entries.Add(new ArchiveEntry(name, reader.BaseStream.Position, size, key));
                reader.BaseStream.Position += size;
            }
            return entries;
        }

        private static List<ArchiveEntry> ReadVersion3Entries(BinaryReader reader)
        {
            List<ArchiveEntry> entries = new List<ArchiveEntry>();
            EnsureRemaining(reader.BaseStream, 4);
            uint key = unchecked(reader.ReadUInt32() * 9 + 3);
            while (true)
            {
                EnsureRemaining(reader.BaseStream, 16);
                uint offset = reader.ReadUInt32() ^ key;
                uint size = reader.ReadUInt32() ^ key;
                uint fileKey = reader.ReadUInt32() ^ key;
                uint nameLength = reader.ReadUInt32() ^ key;
                if (offset == 0) break;
                if (nameLength > MaxNameLength || nameLength > int.MaxValue)
                    throw new InvalidDataException("Invalid RGSS filename length.");
                EnsureRemaining(reader.BaseStream, (int)nameLength);
                string name = DecryptVersion3Name(reader.ReadBytes((int)nameLength), key);
                if (size > int.MaxValue)
                    throw new InvalidDataException("RGSS file is too large.");
                ValidateEntry(reader.BaseStream, offset, (int)size);
                entries.Add(new ArchiveEntry(name, offset, (int)size, fileKey));
            }
            return entries;
        }

        private static int ReadVersion1Int32(BinaryReader reader, ref uint key)
        {
            EnsureRemaining(reader.BaseStream, 4);
            int value = reader.ReadInt32() ^ unchecked((int)key);
            key = unchecked(key * 7 + 3);
            return value;
        }

        private static string DecryptVersion1Name(byte[] bytes, ref uint key)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= (byte)(key & 0xff);
                key = unchecked(key * 7 + 3);
            }
            return Encoding.UTF8.GetString(bytes);
        }

        private static string DecryptVersion3Name(byte[] bytes, uint key)
        {
            byte[] keyBytes = BitConverter.GetBytes(key);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= keyBytes[i % 4];
            return Encoding.UTF8.GetString(bytes);
        }

        private static void DecryptTo(Stream input, Stream output, int size, uint key)
        {
            byte[] buffer = new byte[64 * 1024];
            byte[] keyBytes = BitConverter.GetBytes(key);
            int keyIndex = 0;
            int remaining = size;
            while (remaining > 0)
            {
                int read = input.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                if (read <= 0) throw new EndOfStreamException();
                for (int i = 0; i < read; i++)
                {
                    if (keyIndex == 4)
                    {
                        keyIndex = 0;
                        key = unchecked(key * 7 + 3);
                        keyBytes = BitConverter.GetBytes(key);
                    }
                    buffer[i] ^= keyBytes[keyIndex++];
                }
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static void ValidateNameLength(BinaryReader reader, int length)
        {
            if (length < 0 || length > MaxNameLength)
                throw new InvalidDataException("Invalid RGSS filename length.");
            EnsureRemaining(reader.BaseStream, length);
        }

        private static void ValidateEntry(Stream stream, long offset, int size)
        {
            if (offset < 0 || size < 0 || offset > stream.Length || stream.Length - offset < size)
                throw new InvalidDataException("RGSS archive entry escapes the archive.");
        }

        private static void EnsureRemaining(Stream stream, int bytes)
        {
            if (bytes < 0 || stream.Length - stream.Position < bytes)
                throw new EndOfStreamException();
        }

        private static string GetSafeOutputPath(string outputDir, string archivedPath)
        {
            string[] parts = archivedPath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Select(delegate(string part)
                {
                    string clean = string.Concat(part.Where(delegate(char value) { return !Path.GetInvalidFileNameChars().Contains(value); }));
                    return string.IsNullOrWhiteSpace(clean) || clean == "." || clean == ".." ? "asset" : clean;
                })
                .ToArray();
            if (parts.Length == 0) parts = new[] { "asset" };
            string root = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destination = Path.GetFullPath(Path.Combine(outputDir, Path.Combine(parts)));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("RGSS output path escapes extraction folder.");
            return destination;
        }

        private static string GetUniqueFilePath(string path, ref int renamed)
        {
            string candidate = path;
            string directory = Path.GetDirectoryName(path);
            string stem = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int suffix = 2;
            while (File.Exists(candidate))
                candidate = Path.Combine(directory, stem + " (" + suffix++ + ")" + extension);
            if (!candidate.Equals(path, StringComparison.OrdinalIgnoreCase)) renamed++;
            return candidate;
        }

        private sealed class ArchiveEntry
        {
            public ArchiveEntry(string name, long offset, int size, uint key)
            {
                Name = name;
                Offset = offset;
                Size = size;
                Key = key;
            }

            public string Name { get; private set; }
            public long Offset { get; private set; }
            public int Size { get; private set; }
            public uint Key { get; private set; }
        }
    }
}
