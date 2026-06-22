using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RpgmvpConverterWinForms
{
    internal sealed class ExtractionReportSnapshot
    {
        private ExtractionReportSnapshot(string outputDir, List<FileInfo> files)
        {
            OutputDir = outputDir ?? "";
            Files = files ?? new List<FileInfo>();
        }

        public string OutputDir { get; private set; }
        public List<FileInfo> Files { get; private set; }

        public static ExtractionReportSnapshot Capture(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir))
                return new ExtractionReportSnapshot(outputDir, new List<FileInfo>());
            try
            {
                List<FileInfo> files = Directory.EnumerateFiles(outputDir, "*.*", SearchOption.AllDirectories)
                    .Where(delegate(string path)
                    {
                        string name = Path.GetFileName(path);
                        return !name.StartsWith("GameAssetTool-", StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(delegate(string path)
                    {
                        try { return new FileInfo(path); }
                        catch { return null; }
                    })
                    .Where(delegate(FileInfo info) { return info != null && info.Exists; })
                    .ToList();
                return new ExtractionReportSnapshot(outputDir, files);
            }
            catch
            {
                return new ExtractionReportSnapshot(outputDir, new List<FileInfo>());
            }
        }
    }
}
