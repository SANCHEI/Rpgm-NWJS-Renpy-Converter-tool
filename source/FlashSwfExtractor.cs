using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace RpgmvpConverterWinForms
{
    internal static class FlashSwfExtractor
    {
        public static CollectorResult ExtractImages(string source, string outputDir)
        {
            byte[] body = ReadSwfBody(source);
            int position = GetSwfTagStart(body);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            string swfName = Path.GetFileNameWithoutExtension(source);

            while (position + 2 <= body.Length)
            {
                int tagHeader = ReadUInt16(body, position);
                position += 2;
                int tagCode = tagHeader >> 6;
                int length = tagHeader & 0x3f;
                if (length == 0x3f)
                {
                    if (position + 4 > body.Length) break;
                    length = ReadInt32(body, position);
                    position += 4;
                }
                if (length < 0 || position + length > body.Length) break;
                if (tagCode == 0) break;

                int imageOffset = 0;
                int imageLength = 0;
                int characterId = 0;
                if (tagCode == 21 && length > 2)
                {
                    characterId = ReadUInt16(body, position);
                    imageOffset = position + 2;
                    imageLength = length - 2;
                }
                else if (tagCode == 35 && length > 6)
                {
                    characterId = ReadUInt16(body, position);
                    imageOffset = position + 6;
                    imageLength = Math.Min(ReadInt32(body, position + 2), length - 6);
                }
                else if (tagCode == 90 && length > 8)
                {
                    characterId = ReadUInt16(body, position);
                    imageOffset = position + 8;
                    imageLength = Math.Min(ReadInt32(body, position + 2), length - 8);
                }

                string extension = DetectImageExtension(body, imageOffset, imageLength);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine("embedded", swfName, "image-" + characterId + extension));
                    bool collision;
                    destination = ExtractionPathUtils.GetUniqueFilePath(destination, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (FileStream output = File.Create(destination))
                        output.Write(body, imageOffset, imageLength);
                    extracted++;
                    bytes += ExtractionPathUtils.SafeFileLength(destination);
                    if (collision) renamed++;
                }
                else if (imageLength > 0)
                {
                    skipped++;
                }
                position += length;
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static byte[] ReadSwfBody(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < 8 || file[1] != (byte)'W' || file[2] != (byte)'S')
                throw new InvalidDataException("Invalid SWF header: " + path);
            if (file[0] == (byte)'F')
                return file.Skip(8).ToArray();
            if (file[0] == (byte)'C')
            {
                if (file.Length < 14) throw new InvalidDataException("Compressed SWF is incomplete: " + path);
                using (MemoryStream input = new MemoryStream(file, 10, file.Length - 14))
                using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
            }
            if (file[0] == (byte)'Z')
                throw new NotSupportedException("LZMA-compressed ZWS is not supported yet: " + path);
            throw new InvalidDataException("Unknown SWF compression: " + path);
        }

        private static int GetSwfTagStart(byte[] body)
        {
            if (body.Length < 5) throw new InvalidDataException("SWF body is incomplete.");
            int rectBits = 5 + 4 * (body[0] >> 3);
            int position = (rectBits + 7) / 8 + 4;
            if (position > body.Length) throw new InvalidDataException("SWF frame header is incomplete.");
            return position;
        }

        private static string DetectImageExtension(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 3 || offset + length > data.Length) return "";
            if (data[offset] == 0xff && data[offset + 1] == 0xd8 && data[offset + 2] == 0xff) return ".jpg";
            if (length >= 8 && data[offset] == 0x89 && data[offset + 1] == 0x50 && data[offset + 2] == 0x4e && data[offset + 3] == 0x47) return ".png";
            if (length >= 6 && data[offset] == (byte)'G' && data[offset + 1] == (byte)'I' && data[offset + 2] == (byte)'F') return ".gif";
            return "";
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

    }
}
