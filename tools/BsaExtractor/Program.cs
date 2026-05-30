using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BsaExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("BSA Extractor - Extract files from BSArc archives");
                Console.WriteLine("Usage: BsaExtractor.exe <bsa_file> [output_folder]");
                return;
            }

            string bsaPath = args[0];
            string outputFolder = args.Length > 1 ? args[1] : Path.Combine(Path.GetDirectoryName(bsaPath), "extracted");

            Console.WriteLine("BSA Extractor");
            Console.WriteLine("Input: " + bsaPath);
            Console.WriteLine("Output: " + outputFolder);
            Console.WriteLine();

            try
            {
                ExtractBsa(bsaPath, outputFolder);
                Console.WriteLine("Done!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        static void ExtractBsa(string bsaPath, string outputFolder)
        {
            if (!File.Exists(bsaPath))
            {
                throw new FileNotFoundException("BSA file not found: " + bsaPath);
            }

            Directory.CreateDirectory(outputFolder);

            using (FileStream fs = new FileStream(bsaPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                byte[] magic = br.ReadBytes(8);
                string magicStr = Encoding.ASCII.GetString(magic.Take(6).ToArray()).TrimEnd('\0');

                Console.WriteLine("Magic: '" + magicStr + "'");

                if (!magicStr.StartsWith("BSArc"))
                {
                    throw new InvalidDataException("Not a valid BSArc file");
                }

                ushort version = br.ReadUInt16();
                Console.WriteLine("Version: " + version);

                uint flags = br.ReadUInt32();
                Console.WriteLine("Flags: 0x" + flags.ToString("X8"));

                br.BaseStream.Seek(32, SeekOrigin.Begin);

                uint headerSize = br.ReadUInt32();
                uint dataOffset = br.ReadUInt32();
                uint totalSize = br.ReadUInt32();
                uint fileCount = br.ReadUInt32();

                Console.WriteLine("Header size: " + headerSize);
                Console.WriteLine("Data offset: " + dataOffset);
                Console.WriteLine("Total size: " + totalSize);
                Console.WriteLine("File count: " + fileCount);

                fs.Seek(headerSize, SeekOrigin.Begin);

                List<BsaFileEntry> entries = new List<BsaFileEntry>();

                long maxOffset = fs.Length;
                int maxEntries = 10000;

                for (int i = 0; i < maxEntries; i++)
                {
                    if (fs.Position + 16 > maxOffset)
                        break;

                    try
                    {
                        byte[] hashBytes = br.ReadBytes(8);
                        uint fileSize = br.ReadUInt32();
                        uint fileOffset = br.ReadUInt32();

                        if (fileSize > 0 && fileSize < 10000000 && fileOffset < maxOffset)
                        {
                            string hashStr = BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);

                            entries.Add(new BsaFileEntry
                            {
                                Hash = hashStr,
                                Size = fileSize,
                                Offset = fileOffset
                            });
                        }
                    }
                    catch
                    {
                        break;
                    }
                }

                Console.WriteLine("Found " + entries.Count + " file entries");

                int extracted = 0;
                int errors = 0;

                foreach (var entry in entries)
                {
                    try
                    {
                        fs.Seek(entry.Offset, SeekOrigin.Begin);
                        byte[] header = new byte[16];
                        fs.Read(header, 0, 16);

                        string ext = GetExtension(header);

                        string outPath = Path.Combine(outputFolder, "file_" + entry.Hash + "_" + extracted + ext);

                        fs.Seek(entry.Offset, SeekOrigin.Begin);
                        byte[] fileData = new byte[entry.Size];
                        fs.Read(fileData, 0, (int)entry.Size);

                        File.WriteAllBytes(outPath, fileData);

                        extracted++;

                        if (extracted % 10 == 0)
                        {
                            Console.WriteLine("Extracted " + extracted + " files...");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        if (errors <= 3)
                        {
                            Console.WriteLine("Error extracting: " + ex.Message);
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Extracted: " + extracted + " files");
                Console.WriteLine("Errors: " + errors);
            }
        }

        static string GetExtension(byte[] header)
        {
            if (header.Length >= 2)
            {
                if (header[0] == 0xFF && header[1] == 0xD8)
                    return ".jpg";
                if (header[0] == 0x89 && header[1] == 0x50)
                    return ".png";
                if (header[0] == 0x47 && header[1] == 0x49)
                    return ".gif";
                if (header[0] == 0x42 && header[1] == 0x4D)
                    return ".bmp";
                if (header.Length >= 4 && header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
                    return ".ogg";
                if (header.Length >= 4 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
                    return ".wav";
            }
            return ".bin";
        }

        class BsaFileEntry
        {
            public string Hash { get; set; }
            public uint Size { get; set; }
            public uint Offset { get; set; }
        }
    }
}