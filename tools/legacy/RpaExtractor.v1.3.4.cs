using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace RpgmvpConverterWinForms
{
    public static class RpaExtractor
    {
        private static string lastError = "";

        public static string GetLastError() { return lastError; }

        public static string[] ExtractArchive(string rpaPath, string outputPath)
        {
            lastError = "";
            List<string> extractedFiles = new List<string>();

            try
            {
                FileStream fs = new FileStream(rpaPath, FileMode.Open, FileAccess.Read);
                byte[] fileBytes = new byte[fs.Length];
                fs.Read(fileBytes, 0, fileBytes.Length);
                fs.Close();

                int offset = 0;
                int key = 0;
                string magic = "";

                string headerStr = Encoding.ASCII.GetString(fileBytes, 0, 100);
                int newlinePos = headerStr.IndexOf('\n');
                if (newlinePos > 0)
                {
                    headerStr = headerStr.Substring(0, newlinePos);
                }

                string[] headerParts = headerStr.Split(' ');
                if (headerParts.Length >= 2)
                {
                    magic = headerParts[0];
                    try
                    {
                        offset = Convert.ToInt32(headerParts[1], 16);
                    }
                    catch { offset = 0; }

                    if (magic == "RPA-3.0" && headerParts.Length > 2)
                    {
                        try { key = Convert.ToInt32(headerParts[2], 16); } catch { }
                    }
                    else if (magic == "RPA-3.2" && headerParts.Length > 3)
                    {
                        try
                        {
                            int k1 = Convert.ToInt32(headerParts[2], 16);
                            int k2 = Convert.ToInt32(headerParts[3], 16);
                            key = k1 ^ k2;
                        }
                        catch { }
                    }
                }

                int compressedLen = (int)(fileBytes.Length - offset);
                byte[] compressed = new byte[compressedLen];
                Array.Copy(fileBytes, offset, compressed, 0, compressedLen);

                byte[] decompressed;
                try
                {
                    decompressed = DecompressRaw(compressed);
                }
                catch (Exception ex)
                {
                    lastError = "Decompress error: " + ex.Message;
                    return new string[0];
                }

                string indexData = Encoding.UTF8.GetString(decompressed);

                List<string> fileList = new List<string>();
                List<int> fileStarts = new List<int>();

                int idx = 0;
                while (idx < indexData.Length)
                {
                    int nullCount = 0;
                    int start = idx;
                    while (idx < indexData.Length && nullCount < 3)
                    {
                        if (indexData[idx] == '\0')
                        {
                            nullCount++;
                        }
                        else
                        {
                            nullCount = 0;
                        }
                        idx++;
                    }

                    if (nullCount >= 3)
                    {
                        string segment = indexData.Substring(start, idx - start - 3);
                        if (!string.IsNullOrWhiteSpace(segment))
                        {
                            string cleaned = segment.Trim('\0');
                            if (cleaned.Contains("/") || cleaned.Contains("\\"))
                            {
                                if (!fileList.Contains(cleaned))
                                {
                                    fileList.Add(cleaned);
                                    fileStarts.Add(fileStarts.Count > 0 ? fileStarts[fileStarts.Count - 1] + 1 : 0);
                                }
                            }
                        }
                    }
                }

                if (fileList.Count == 0)
                {
                    lastError = "No files in index";
                    return new string[0];
                }

                Directory.CreateDirectory(outputPath);

                int dataStart = 0;
                for (int i = 0; i < fileList.Count; i++)
                {
                    string filePath = fileList[i];
                    string dirPart = Path.GetDirectoryName(filePath);
                    string namePart = Path.GetFileName(filePath);

                    string outDir = outputPath;
                    if (!string.IsNullOrEmpty(dirPart))
                    {
                        outDir = Path.Combine(outputPath, dirPart.Replace("/", "\\"));
                    }

                    Directory.CreateDirectory(outDir);
                    string outFile = Path.Combine(outDir, namePart);

                    int nextStart = (i < fileList.Count - 1) ? (indexData.IndexOf(fileList[i + 1]) - 1) : indexData.Length;
                    int segmentLen = nextStart - dataStart;

                    if (segmentLen > 0)
                    {
                        string segment = indexData.Substring(dataStart, segmentLen);
                        byte[] partBytes = Encoding.GetEncoding(1252).GetBytes(segment);

                        for (int j = 0; j < partBytes.Length && j < 16; j++)
                        {
                            partBytes[j] = (byte)(partBytes[j] ^ (key & 0xFF));
                        }

                        File.WriteAllBytes(outFile, partBytes);
                        extractedFiles.Add(outFile);
                    }

                    dataStart = nextStart;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            return extractedFiles.ToArray();
        }

        private static byte[] DecompressRaw(byte[] data)
        {
            if (data.Length < 2) return data;

            using (MemoryStream msOut = new MemoryStream())
            {
                using (MemoryStream msIn = new MemoryStream(data))
                {
                    int startIdx = 0;
                    if (data[0] == 0x78 && data[1] == 0x9C)
                    {
                        startIdx = 2;
                    }
                    else if (data[0] == 0x78 && data[1] == 0x01)
                    {
                        startIdx = 2;
                    }

                    msIn.Position = startIdx;

                    using (DeflateStream ds = new DeflateStream(msIn, CompressionMode.Decompress, true))
                    {
                        byte[] buffer = new byte[4096];
                        int read;
                        while ((read = ds.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            msOut.Write(buffer, 0, read);
                        }
                    }
                }
                return msOut.ToArray();
            }
        }

        public static string[] FindRpaFiles(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return new string[0];
            return Directory.GetFiles(rootPath, "*.rpa", SearchOption.AllDirectories);
        }

        public static string[] FindRpaFilesInGameFolder(string gamePath)
        {
            string gameFolder = Path.Combine(gamePath, "game");
            if (Directory.Exists(gameFolder))
            {
                return FindRpaFiles(gameFolder);
            }
            return FindRpaFiles(gamePath);
        }
    }
}
