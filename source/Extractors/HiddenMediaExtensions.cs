using System;
using System.IO;
using System.IO.Compression;

namespace RpgmvpConverterWinForms
{
    internal static class HiddenMediaExtensions
    {
        private const int HeaderBytes = 256;

        public static string NormalizeRelativePath(string relativePath, string sourcePath)
        {
            if (!IsNlchPath(relativePath)) return relativePath;
            return IsWebmFile(sourcePath) ? Path.ChangeExtension(relativePath, ".webm") : relativePath;
        }

        public static string NormalizeRelativePath(string relativePath, ZipArchiveEntry entry)
        {
            if (!IsNlchPath(relativePath)) return relativePath;
            return IsWebmEntry(entry) ? Path.ChangeExtension(relativePath, ".webm") : relativePath;
        }

        private static bool IsNlchPath(string path)
        {
            return path.EndsWith(".nlch", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWebmFile(string path)
        {
            try
            {
                byte[] header = new byte[HeaderBytes];
                using (FileStream stream = File.OpenRead(path))
                {
                    int read = stream.Read(header, 0, header.Length);
                    return IsWebmHeader(header, read);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWebmEntry(ZipArchiveEntry entry)
        {
            try
            {
                byte[] header = new byte[HeaderBytes];
                using (Stream stream = entry.Open())
                {
                    int read = stream.Read(header, 0, header.Length);
                    return IsWebmHeader(header, read);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWebmHeader(byte[] header, int count)
        {
            if (count < 8) return false;
            if (header[0] != 0x1A || header[1] != 0x45 || header[2] != 0xDF || header[3] != 0xA3)
                return false;
            return true;
        }
    }
}
