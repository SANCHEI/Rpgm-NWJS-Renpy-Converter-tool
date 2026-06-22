using System;
using System.Collections.Generic;
using System.IO;

namespace RpgmvpConverterWinForms
{
    internal sealed class DryScanCache<TSummary>
    {
        private string cachedPath;
        private string cachedFingerprint;
        private TSummary cachedSummary;

        public bool TryGet(string inputPath, out TSummary summary)
        {
            if (!string.IsNullOrWhiteSpace(cachedPath)
                && string.Equals(cachedPath, Normalize(inputPath), StringComparison.OrdinalIgnoreCase)
                && string.Equals(cachedFingerprint, GetFingerprint(inputPath), StringComparison.Ordinal))
            {
                summary = cachedSummary;
                return true;
            }

            summary = default(TSummary);
            return false;
        }

        public void Store(string inputPath, TSummary summary)
        {
            cachedPath = Normalize(inputPath);
            cachedFingerprint = GetFingerprint(inputPath);
            cachedSummary = summary;
        }

        public void Clear()
        {
            cachedPath = null;
            cachedFingerprint = null;
            cachedSummary = default(TSummary);
        }

        private static string Normalize(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return "";
            try { return Path.GetFullPath(inputPath); }
            catch { return inputPath.Trim(); }
        }

        private static string GetFingerprint(string inputPath)
        {
            try
            {
                if (File.Exists(inputPath))
                {
                    FileInfo info = new FileInfo(inputPath);
                    return "f:" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
                }
                if (Directory.Exists(inputPath))
                {
                    long count = 0;
                    long bytes = 0;
                    long newestTicks = Directory.GetLastWriteTimeUtc(inputPath).Ticks;
                    Stack<string> pending = new Stack<string>();
                    pending.Push(inputPath);
                    while (pending.Count > 0)
                    {
                        string directory = pending.Pop();
                        try
                        {
                            DateTime directoryWrite = Directory.GetLastWriteTimeUtc(directory);
                            if (directoryWrite.Ticks > newestTicks) newestTicks = directoryWrite.Ticks;
                        }
                        catch { }

                        string[] files;
                        try { files = Directory.GetFiles(directory); }
                        catch { files = new string[0]; }
                        for (int i = 0; i < files.Length; i++)
                        {
                            try
                            {
                                FileInfo info = new FileInfo(files[i]);
                                count++;
                                bytes += info.Exists ? info.Length : 0;
                                if (info.LastWriteTimeUtc.Ticks > newestTicks)
                                    newestTicks = info.LastWriteTimeUtc.Ticks;
                            }
                            catch { }
                        }

                        string[] directories;
                        try { directories = Directory.GetDirectories(directory); }
                        catch { directories = new string[0]; }
                        for (int i = 0; i < directories.Length; i++)
                            pending.Push(directories[i]);
                    }
                    return "d:" + count + ":" + bytes + ":" + newestTicks;
                }
            }
            catch { }
            return "";
        }
    }
}
