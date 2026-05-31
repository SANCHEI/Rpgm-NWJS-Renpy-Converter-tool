using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace RpgmvpConverterWinForms
{
    internal static class ArchiveSafetyPolicy
    {
        public const int MaxEntryCount = 200000;
        public const long MaxEntryBytes = 4L * 1024L * 1024L * 1024L;
        public const long MaxTotalBytes = 16L * 1024L * 1024L * 1024L;
        public const long MaxCompressionRatio = 1000L;

        public static void ValidateZipArchive(ZipArchive archive)
        {
            if (archive.Entries.Count > MaxEntryCount)
                throw new InvalidDataException("Archive safety limit: more than " + MaxEntryCount + " entries.");

            long totalBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                ValidateEntrySize(entry.Length, entry.FullName);
                AddToTotal(ref totalBytes, entry.Length, entry.FullName);

                if (entry.Length > 1024L * 1024L
                    && entry.CompressedLength > 0
                    && entry.Length / entry.CompressedLength > MaxCompressionRatio)
                    throw new InvalidDataException("Archive safety limit: suspicious compression ratio for " + entry.FullName + ".");
            }
        }

        public static string NormalizeEntryPath(string archivedPath)
        {
            if (string.IsNullOrWhiteSpace(archivedPath))
                throw new InvalidDataException("Archive entry has an empty path.");

            string normalized = archivedPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
                throw new InvalidDataException("Archive entry uses an absolute path: " + archivedPath);

            string[] parts = normalized
                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Where(delegate(string part) { return part != "."; })
                .ToArray();
            if (parts.Length == 0 || parts.Any(delegate(string part) { return part == ".."; }))
                throw new InvalidDataException("Archive entry escapes the extraction folder: " + archivedPath);
            return Path.Combine(parts);
        }

        public static void ValidateEntry(string archivedPath, long size, ref int entryCount, ref long totalBytes)
        {
            NormalizeEntryPath(archivedPath);
            entryCount++;
            if (entryCount > MaxEntryCount)
                throw new InvalidDataException("Archive safety limit: more than " + MaxEntryCount + " entries.");
            ValidateEntrySize(size, archivedPath);
            AddToTotal(ref totalBytes, size, archivedPath);
        }

        public static void CopyLimited(Stream input, Stream output, long expectedBytes)
        {
            ValidateEntrySize(expectedBytes, "stream");
            byte[] buffer = new byte[64 * 1024];
            long copied = 0;
            while (true)
            {
                int read = input.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                copied += read;
                if (copied > expectedBytes || copied > MaxEntryBytes)
                    throw new InvalidDataException("Archive safety limit: extracted stream is larger than declared.");
                output.Write(buffer, 0, read);
            }
            if (copied != expectedBytes)
                throw new InvalidDataException("Archive entry is truncated.");
        }

        private static void ValidateEntrySize(long size, string name)
        {
            if (size < 0 || size > MaxEntryBytes)
                throw new InvalidDataException("Archive safety limit: entry is too large: " + name + ".");
        }

        private static void AddToTotal(ref long totalBytes, long size, string name)
        {
            if (totalBytes > MaxTotalBytes - size)
                throw new InvalidDataException("Archive safety limit: total extracted size exceeds "
                    + (MaxTotalBytes / (1024L * 1024L * 1024L)) + " GB near " + name + ".");
            totalBytes += size;
        }
    }
}
