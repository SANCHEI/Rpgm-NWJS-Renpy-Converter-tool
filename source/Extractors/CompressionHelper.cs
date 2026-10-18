using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class CompressionHelper
    {
        public static byte[] ZlibDecompress(byte[] stored)
        {
            if (stored == null || stored.Length < 2)
                throw new InvalidDataException("Zlib block is truncated.");
            using (MemoryStream input = new MemoryStream(stored, 2, stored.Length - 2))
            using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
