using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal sealed class FlashSwfExtractor : IAssetExtractor
    {
        public string Id { get { return "flash-swf"; } }
        public string DisplayName { get { return "Flash SWF experimental"; } }

        public bool CanExtract(string inputPath)
        {
            return FindFiles(inputPath).Count > 0;
        }

        public CollectorResult Extract(string inputPath, string outputDir)
        {
            List<string> files = FindFiles(inputPath);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            Directory.CreateDirectory(outputDir);

            foreach (string source in files)
            {
                try
                {
                    bool collision;
                    string original = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine("originals", Path.GetFileName(source)));
                    original = ExtractionPathUtils.GetUniqueFilePath(original, out collision);
                    Directory.CreateDirectory(Path.GetDirectoryName(original));
                    File.Copy(source, original);
                    extracted++;
                    bytes += ExtractionPathUtils.SafeFileLength(original);
                    if (collision) renamed++;

                    CollectorResult assets = ExtractAssets(source, outputDir);
                    extracted += assets.Extracted;
                    bytes += assets.Bytes;
                    renamed += assets.Renamed;
                    skipped += assets.Skipped;
                }
                catch
                {
                    skipped++;
                }
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        public static List<string> FindFiles(string inputPath)
        {
            return AssetCollectors.FindInputFiles(inputPath, ".swf")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static CollectorResult ExtractAssets(string source, string outputDir)
        {
            byte[] body = ReadSwfBody(source);
            int position = GetSwfTagStart(body);
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            string swfName = Path.GetFileNameWithoutExtension(source);
            Dictionary<int, VideoStream> videos = new Dictionary<int, VideoStream>();

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
                else if (tagCode == 60 && length >= 10)
                {
                    int streamId = ReadUInt16(body, position);
                    videos[streamId] = new VideoStream(streamId, body[position + 9] & 0x0f);
                }
                else if (tagCode == 61 && length > 4)
                {
                    int streamId = ReadUInt16(body, position);
                    VideoStream video;
                    if (videos.TryGetValue(streamId, out video))
                        video.Frames.Add(new VideoFrame(ReadUInt16(body, position + 2), body.Skip(position + 4).Take(length - 4).ToArray()));
                    else
                        skipped++;
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

            foreach (VideoStream video in videos.Values.Where(delegate(VideoStream item) { return item.Frames.Count > 0; }))
            {
                string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine("embedded", swfName, "video-" + video.Id + ".flv"));
                bool collision;
                destination = ExtractionPathUtils.GetUniqueFilePath(destination, out collision);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                WriteFlv(destination, video);
                extracted++;
                bytes += ExtractionPathUtils.SafeFileLength(destination);
                if (collision) renamed++;
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static void WriteFlv(string destination, VideoStream video)
        {
            using (FileStream output = File.Create(destination))
            using (BinaryWriter writer = new BinaryWriter(output))
            {
                writer.Write(Encoding.ASCII.GetBytes("FLV"));
                writer.Write((byte)1);
                writer.Write((byte)1);
                WriteBigEndian(writer, 9);
                WriteBigEndian(writer, 0);

                foreach (VideoFrame frame in video.Frames.OrderBy(delegate(VideoFrame item) { return item.Number; }))
                {
                    int timestamp = Math.Max(frame.Number, 0) * 1000 / 24;
                    int dataSize = frame.Data.Length + 1;
                    writer.Write((byte)9);
                    WriteUInt24(writer, dataSize);
                    WriteUInt24(writer, timestamp & 0x00ffffff);
                    writer.Write((byte)((timestamp >> 24) & 0xff));
                    WriteUInt24(writer, 0);
                    writer.Write((byte)(((frame.Number == 0 ? 1 : 2) << 4) | video.Codec));
                    writer.Write(frame.Data);
                    WriteBigEndian(writer, 11 + dataSize);
                }
            }
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

        private static void WriteUInt24(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 16) & 0xff));
            writer.Write((byte)((value >> 8) & 0xff));
            writer.Write((byte)(value & 0xff));
        }

        private static void WriteBigEndian(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 24) & 0xff));
            writer.Write((byte)((value >> 16) & 0xff));
            writer.Write((byte)((value >> 8) & 0xff));
            writer.Write((byte)(value & 0xff));
        }

        private sealed class VideoStream
        {
            public VideoStream(int id, int codec)
            {
                Id = id;
                Codec = codec;
                Frames = new List<VideoFrame>();
            }

            public int Id { get; private set; }
            public int Codec { get; private set; }
            public List<VideoFrame> Frames { get; private set; }
        }

        private sealed class VideoFrame
        {
            public VideoFrame(int number, byte[] data)
            {
                Number = number;
                Data = data;
            }

            public int Number { get; private set; }
            public byte[] Data { get; private set; }
        }
    }
}
