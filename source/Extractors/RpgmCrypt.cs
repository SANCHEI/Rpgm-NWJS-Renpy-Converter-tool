using System;
using System.IO;

namespace RpgmvpConverterWinForms
{
    internal static class RpgmCrypt
    {
        private static readonly byte[] HeaderMagic = new byte[] { (byte)'R', (byte)'P', (byte)'G', (byte)'M', (byte)'V' };
        private static readonly byte[] PngMagic = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] JpegMagic = new byte[] { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngHeader16 = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
        };
        private static readonly byte[] JpegJfifHeader16 = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01
        };
        private static readonly byte[] JpegExifHeader16 = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x00, 0x45, 0x78,
            0x69, 0x66, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        public static bool HasEncryptedHeader(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < HeaderMagic.Length) return false;
            for (int i = 0; i < HeaderMagic.Length; i++)
                if (fileBytes[i] != HeaderMagic[i]) return false;
            return true;
        }

        public static string DetectImageExtension(byte[] data)
        {
            if (StartsWith(data, PngMagic)) return ".png";
            if (StartsWith(data, JpegMagic)) return ".jpg";
            return null;
        }

        public static byte[] Decrypt(byte[] fileBytes, byte[] keyBytes)
        {
            if (fileBytes == null || fileBytes.Length <= 16)
                throw new InvalidDataException("File too short.");
            if (keyBytes == null || keyBytes.Length < 16)
                throw new InvalidDataException("Key too short.");
            byte[] data = new byte[fileBytes.Length - 16];
            Buffer.BlockCopy(fileBytes, 16, data, 0, data.Length);
            for (int i = 0; i < 16 && i < data.Length; i++) data[i] ^= keyBytes[i];
            return data;
        }

        public static bool TryReconstructKey(byte[] fileBytes, out byte[] keyBytes)
        {
            return TryReconstructWithTemplate(fileBytes, PngHeader16, out keyBytes)
                || TryReconstructWithTemplate(fileBytes, JpegJfifHeader16, out keyBytes)
                || TryReconstructWithTemplate(fileBytes, JpegExifHeader16, out keyBytes);
        }

        public static bool TryReconstructPngKey(byte[] fileBytes, out byte[] keyBytes)
        {
            return TryReconstructWithTemplate(fileBytes, PngHeader16, out keyBytes);
        }

        private static bool TryReconstructWithTemplate(byte[] fileBytes, byte[] expected, out byte[] keyBytes)
        {
            keyBytes = null;
            if (fileBytes == null || fileBytes.Length <= 32) return false;
            byte[] candidate = new byte[16];
            for (int i = 0; i < candidate.Length; i++) candidate[i] = (byte)(fileBytes[16 + i] ^ expected[i]);
            try
            {
                // Structural validation, not just magic: a template always
                // reproduces its own magic by construction, so magic alone
                // would accept a bogus key for any file.
                if (ValidateDecrypted(Decrypt(fileBytes, candidate)))
                {
                    keyBytes = candidate;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static bool ValidateKey(byte[] fileBytes, byte[] keyBytes)
        {
            try
            {
                return ValidateDecrypted(Decrypt(fileBytes, keyBytes));
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidateDecrypted(byte[] data)
        {
            if (StartsWith(data, PngMagic)) return ValidatePng(data);
            if (StartsWith(data, JpegMagic)) return ValidateJpeg(data);
            return false;
        }

        private static bool ValidatePng(byte[] data)
        {
            if (data.Length < 33) return false;
            if (ReadBigEndianInt32(data, 8) != 13) return false;
            if (data[12] != (byte)'I' || data[13] != (byte)'H' || data[14] != (byte)'D' || data[15] != (byte)'R') return false;
            return PngEncoder.ComputeCrc32(data, 12, 17) == (uint)ReadBigEndianInt32(data, 29);
        }

        private static bool ValidateJpeg(byte[] data)
        {
            if (data.Length < 4) return false;
            int position = 2;
            int segments = 0;
            while (position + 4 <= data.Length && segments < 100000)
            {
                if (data[position] != 0xFF) return false;
                byte marker = data[position + 1];
                if (marker == 0xD9) return position + 2 <= data.Length;
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    position += 2;
                    continue;
                }
                if (position + 4 > data.Length) return false;
                int length = (data[position + 2] << 8) | data[position + 3];
                if (length < 2 || position + 2 + length > data.Length) return false;
                if (marker == 0xDA)
                {
                    position += 2 + length;
                    int next = FindJpegEnd(data, position);
                    return next > position;
                }
                position += 2 + length;
                segments++;
            }
            return false;
        }

        private static int FindJpegEnd(byte[] data, int offset)
        {
            for (int index = offset; index + 1 < data.Length; index++)
                if (data[index] == 0xFF && data[index + 1] == 0xD9) return index + 2;
            return -1;
        }

        private static int ReadBigEndianInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data == null || data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[i] != prefix[i]) return false;
            return true;
        }
    }
}
