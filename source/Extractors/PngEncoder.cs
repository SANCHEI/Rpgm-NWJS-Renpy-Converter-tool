using System;
using System.IO;
using System.IO.Compression;

namespace RpgmvpConverterWinForms
{
    internal static class PngEncoder
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        public static byte[] EncodeRgba(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException("rgba");
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
            if (rgba.Length != (long)width * (long)height * 4)
                throw new ArgumentException("RGBA buffer size does not match dimensions.");
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

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
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
    }
}
