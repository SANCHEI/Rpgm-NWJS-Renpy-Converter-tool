using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace RpgmvpConverterWinForms
{
    internal sealed class ElectronAsarExtractor : IAssetExtractor
    {
        public string Id { get { return "electron-asar"; } }
        public string DisplayName { get { return "Electron ASAR"; } }

        public bool CanExtract(string inputPath)
        {
            return FindArchives(inputPath).Count > 0;
        }

        public CollectorResult Extract(string inputPath, string outputDir)
        {
            List<string> archives = FindArchives(inputPath);
            if (archives.Count == 0)
                throw new FileNotFoundException("No Electron app.asar archive was found.");

            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            Directory.CreateDirectory(outputDir);

            foreach (string archive in archives)
            {
                CollectorResult result = ExtractArchive(archive, outputDir);
                extracted += result.Extracted;
                bytes += result.Bytes;
                renamed += result.Renamed;
                skipped += result.Skipped;
            }
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        public static List<string> FindArchives(string inputPath)
        {
            if (File.Exists(inputPath) && inputPath.EndsWith(".asar", StringComparison.OrdinalIgnoreCase))
                return new List<string> { Path.GetFullPath(inputPath) };

            string root = AssetCollectors.InputDirectory(inputPath);
            if (!Directory.Exists(root)) return new List<string>();
            return new[]
            {
                Path.Combine(root, "app.asar"),
                Path.Combine(root, "resources", "app.asar")
            }
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        private static CollectorResult ExtractArchive(string archivePath, string outputDir)
        {
            int extracted = 0;
            long bytes = 0;
            int renamed = 0;
            int skipped = 0;
            int entryCount = 0;
            long totalBytes = 0;

            using (FileStream stream = File.OpenRead(archivePath))
            {
                AsarHeader header = ReadHeader(stream);
                List<AsarEntry> entries = new List<AsarEntry>();
                CollectEntries(header.Files, "", entries, ref skipped);

                foreach (AsarEntry entry in entries)
                {
                    string normalized;
                    try
                    {
                        ArchiveSafetyPolicy.ValidateEntry(entry.Path, entry.Size, ref entryCount, ref totalBytes);
                        normalized = ArchiveSafetyPolicy.NormalizeEntryPath(entry.Path);
                    }
                    catch (InvalidDataException)
                    {
                        skipped++;
                        continue;
                    }

                    string prefix = Path.Combine("archives", Path.GetFileNameWithoutExtension(archivePath));
                    string destination = ExtractionPathUtils.GetSafeOutputPath(outputDir, Path.Combine(prefix, normalized));
                    bool collision;
                    destination = ExtractionPathUtils.GetUniqueFilePath(destination, out collision);

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));

                    try
                    {
                        if (entry.Unpacked)
                        {
                            string unpacked = Path.Combine(archivePath + ".unpacked", normalized);
                            using (FileStream input = File.OpenRead(unpacked))
                            using (FileStream output = File.Create(destination))
                                ArchiveSafetyPolicy.CopyLimited(input, output, entry.Size);
                        }
                        else
                        {
                            stream.Position = header.DataOffset + entry.Offset;
                            using (FileStream output = File.Create(destination))
                                CopyExactly(stream, output, entry.Size);
                        }
                        extracted++;
                        bytes += ExtractionPathUtils.SafeFileLength(destination);
                        if (collision) renamed++;
                    }
                    catch
                    {
                        ExtractionPathUtils.TryDeleteFile(destination);
                        skipped++;
                    }
                }
            }
            DecryptRpgmEncryptedFiles(outputDir);
            return new CollectorResult(extracted, bytes, renamed, skipped);
        }

        private static void DecryptRpgmEncryptedFiles(string outputDir)
        {
            string keyHex = FindEncryptionKey(outputDir);
            if (string.IsNullOrWhiteSpace(keyHex)) return;
            byte[] keyBytes;
            try { keyBytes = HexToBytes(keyHex); }
            catch { return; }
            if (keyBytes == null || keyBytes.Length < 16) return;
            int decryptedCount = 0;
            foreach (string path in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (path.EndsWith(".png_", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".jpg_", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".ogg_", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] data = File.ReadAllBytes(path);
                        if (!RpgmCrypt.HasEncryptedHeader(data)) continue;
                        byte[] decrypted = RpgmCrypt.Decrypt(data, keyBytes);
                        string ext = DetectDecryptedExtension(decrypted);
                        if (ext == null) continue;
                        string renamed = ExtractionPathUtils.GetUniqueFilePath(
                            Path.ChangeExtension(path, ext), out bool _);
                        File.WriteAllBytes(renamed, decrypted);
                        decryptedCount++;
                    }
                }
                catch { }
            }
            Console.WriteLine("RPGM_DECRYPTED: " + decryptedCount + " files");
        }

        private static string FindEncryptionKey(string outputDir)
        {
            foreach (string sysJson in Directory.GetFiles(outputDir, "System.json", SearchOption.AllDirectories))
            {
                try
                {
                    string text = File.ReadAllText(sysJson);
                    var root = new JavaScriptSerializer().DeserializeObject(text) as Dictionary<string, object>;
                    if (root != null && root.TryGetValue("encryptionKey", out object keyObj))
                        return keyObj as string;
                }
                catch { }
            }
            return null;
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0) return null;
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            return bytes;
        }

        private static string DetectDecryptedExtension(byte[] data)
        {
            string img = RpgmCrypt.DetectImageExtension(data);
            if (img != null) return img;
            if (data.Length >= 4 && data[0] == (byte)'O' && data[1] == (byte)'g' && data[2] == (byte)'g' && data[3] == (byte)'S')
                return ".ogg";
            if (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
                return ".mp3";
            if (data.Length >= 3 && data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
                return ".mp3";
            return null;
        }

        private static AsarHeader ReadHeader(FileStream stream)
        {
            byte[] sizePickle = ReadExactly(stream, 8);
            if (ReadUInt32(sizePickle, 0) != 4)
                throw new InvalidDataException("Invalid ASAR size pickle.");

            long headerSize = ReadUInt32(sizePickle, 4);
            if (headerSize < 8 || headerSize > 64L * 1024L * 1024L || stream.Length < 8 + headerSize)
                throw new InvalidDataException("Invalid ASAR header size.");

            byte[] headerPickle = ReadExactly(stream, (int)headerSize);
            long payloadSize = ReadUInt32(headerPickle, 0);
            long jsonSize = ReadUInt32(headerPickle, 4);
            if (jsonSize <= 0 || jsonSize > payloadSize - 4 || 8 + jsonSize > headerPickle.Length)
                throw new InvalidDataException("Invalid ASAR JSON header.");

            string json = Encoding.UTF8.GetString(headerPickle, 8, (int)jsonSize);
            Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            Dictionary<string, object> files;
            if (root == null || !TryGetDictionary(root, "files", out files))
                throw new InvalidDataException("ASAR header does not contain a files map.");
            return new AsarHeader(files, 8 + headerSize);
        }

        private static void CollectEntries(Dictionary<string, object> files, string prefix, List<AsarEntry> entries, ref int skipped)
        {
            foreach (KeyValuePair<string, object> pair in files)
            {
                Dictionary<string, object> node = pair.Value as Dictionary<string, object>;
                if (node == null)
                {
                    skipped++;
                    continue;
                }

                string path = string.IsNullOrWhiteSpace(prefix) ? pair.Key : Path.Combine(prefix, pair.Key);
                Dictionary<string, object> children;
                if (TryGetDictionary(node, "files", out children))
                {
                    CollectEntries(children, path, entries, ref skipped);
                    continue;
                }
                if (node.ContainsKey("link"))
                {
                    skipped++;
                    continue;
                }

                long size;
                if (!TryGetLong(node, "size", out size))
                {
                    skipped++;
                    continue;
                }
                long offset = 0;
                object rawOffset;
                if (node.TryGetValue("offset", out rawOffset)
                    && !long.TryParse(Convert.ToString(rawOffset, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
                {
                    skipped++;
                    continue;
                }
                entries.Add(new AsarEntry(path, offset, size, GetBoolean(node, "unpacked")));
            }
        }

        private static void CopyExactly(Stream input, Stream output, long count)
        {
            byte[] buffer = new byte[64 * 1024];
            long copied = 0;
            while (copied < count)
            {
                int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, count - copied));
                if (read <= 0) throw new InvalidDataException("ASAR entry is truncated.");
                output.Write(buffer, 0, read);
                copied += read;
            }
        }

        private static byte[] ReadExactly(Stream input, int count)
        {
            byte[] bytes = new byte[count];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = input.Read(bytes, offset, bytes.Length - offset);
                if (read <= 0) throw new InvalidDataException("Unexpected end of ASAR archive.");
                offset += read;
            }
            return bytes;
        }

        private static long ReadUInt32(byte[] bytes, int offset)
        {
            return (long)bytes[offset]
                | ((long)bytes[offset + 1] << 8)
                | ((long)bytes[offset + 2] << 16)
                | ((long)bytes[offset + 3] << 24);
        }

        private static bool TryGetDictionary(Dictionary<string, object> node, string name, out Dictionary<string, object> value)
        {
            object raw;
            value = node.TryGetValue(name, out raw) ? raw as Dictionary<string, object> : null;
            return value != null;
        }

        private static bool TryGetLong(Dictionary<string, object> node, string name, out long value)
        {
            object raw;
            value = 0;
            return node.TryGetValue(name, out raw)
                && long.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool GetBoolean(Dictionary<string, object> node, string name)
        {
            object raw;
            return node.TryGetValue(name, out raw) && Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        }

        private sealed class AsarHeader
        {
            public AsarHeader(Dictionary<string, object> files, long dataOffset)
            {
                Files = files;
                DataOffset = dataOffset;
            }

            public Dictionary<string, object> Files { get; private set; }
            public long DataOffset { get; private set; }
        }

        private sealed class AsarEntry
        {
            public AsarEntry(string path, long offset, long size, bool unpacked)
            {
                Path = path;
                Offset = offset;
                Size = size;
                Unpacked = unpacked;
            }

            public string Path { get; private set; }
            public long Offset { get; private set; }
            public long Size { get; private set; }
            public bool Unpacked { get; private set; }
        }
    }
}
