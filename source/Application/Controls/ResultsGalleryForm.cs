using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GameAssetTool.ApplicationUi;

namespace RpgmvpConverterWinForms
{
    internal sealed class ResultsGalleryForm : NativeVirtualGalleryDemoForm
    {
        private const string IndexCacheName = "GameAssetTool-index.tsv";

        public ResultsGalleryForm(string outputDir, bool russian)
            : base(outputDir, russian)
        {
            Text = russian ? "Галерея результатов" : "Results Gallery";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ApplicationIcon.Apply(this);
        }

        internal static List<string> GetMatchingFiles(string outputDir, int filterIndex, string search)
        {
            return FilterFiles(IndexFiles(outputDir), filterIndex, search, 0);
        }

        internal static int PrepareIndexCache(string outputDir)
        {
            return PrepareIndexCache(outputDir, CancellationToken.None);
        }

        internal static int PrepareIndexCache(string outputDir, CancellationToken token)
        {
            List<string> files = IndexFiles(outputDir, token);
            token.ThrowIfCancellationRequested();
            WriteIndexCache(outputDir, files);
            return files.Count;
        }

        private static List<string> IndexFiles(string outputDir)
        {
            return IndexFiles(outputDir, CancellationToken.None);
        }

        private static List<string> IndexFiles(string outputDir, CancellationToken token)
        {
            if (!Directory.Exists(outputDir))
            {
                return new List<string>();
            }

            token.ThrowIfCancellationRequested();
            List<string> cached = ReadIndexCache(outputDir);
            if (cached != null)
            {
                return cached;
            }

            try
            {
                List<string> files = new List<string>();
                foreach (string path in Directory.EnumerateFiles(outputDir, "*.*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    if (IsGalleryFile(path)) files.Add(path);
                }
                files.Sort(delegate(string left, string right)
                {
                    return string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
                });
                return files;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> FilterFiles(IEnumerable<string> files, int filterIndex, string search, int sortIndex)
        {
            string query = (search ?? "").Trim();
            List<string> result = files.Where(delegate(string path)
            {
                string extension = Path.GetExtension(path);
                if (filterIndex == 1 && !MediaTypeRegistry.IsImageLike(extension)) return false;
                if (filterIndex == 2 && !MediaTypeRegistry.IsVector(extension)) return false;
                if (filterIndex == 3 && !MediaTypeRegistry.IsAudio(extension)) return false;
                if (filterIndex == 4 && !MediaTypeRegistry.IsVideo(extension)) return false;
                return query.Length == 0 || path.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();
            SortFiles(result, sortIndex);
            return result;
        }

        private static bool IsGalleryFile(string path)
        {
            string extension = Path.GetExtension(path);
            return MediaTypeRegistry.IsMedia(extension) || MediaTypeRegistry.IsText(extension);
        }

        private static void SortFiles(List<string> files, int sortIndex)
        {
            Comparison<string> comparison;
            switch (sortIndex)
            {
                case 1:
                    comparison = delegate(string left, string right)
                    {
                        int type = string.Compare(Path.GetExtension(left), Path.GetExtension(right), StringComparison.OrdinalIgnoreCase);
                        return type != 0 ? type : string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
                    };
                    break;
                case 2:
                    comparison = delegate(string left, string right)
                    {
                        int size = SafeFileLength(left).CompareTo(SafeFileLength(right));
                        return size != 0 ? size : string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
                    };
                    break;
                case 3:
                    comparison = delegate(string left, string right)
                    {
                        int date = SafeLastWriteTicks(right).CompareTo(SafeLastWriteTicks(left));
                        return date != 0 ? date : string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
                    };
                    break;
                default:
                    comparison = delegate(string left, string right)
                    {
                        return string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
                    };
                    break;
            }

            files.Sort(comparison);
        }

        private static List<string> ReadIndexCache(string outputDir)
        {
            string cachePath = Path.Combine(outputDir, IndexCacheName);
            if (!File.Exists(cachePath))
            {
                return null;
            }

            try
            {
                List<string> files = new List<string>();
                foreach (string line in File.ReadLines(cachePath))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    string relative = parts[0].Replace('/', Path.DirectorySeparatorChar);
                    string path = Path.Combine(outputDir, relative);
                    long length;
                    long ticks;
                    if (!long.TryParse(parts[1], out length) || !long.TryParse(parts[2], out ticks))
                    {
                        continue;
                    }

                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length == length && info.LastWriteTimeUtc.Ticks == ticks && IsGalleryFile(path))
                    {
                        files.Add(path);
                    }
                }

                return files;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteIndexCache(string outputDir, IEnumerable<string> files)
        {
            if (!Directory.Exists(outputDir))
            {
                return;
            }

            string cachePath = Path.Combine(outputDir, IndexCacheName);
            try
            {
                using (StreamWriter writer = new StreamWriter(cachePath, false))
                {
                    foreach (string path in files)
                    {
                        try
                        {
                            FileInfo info = new FileInfo(path);
                            if (!info.Exists)
                            {
                                continue;
                            }

                            string relative = MakeRelativePath(outputDir, path).Replace(Path.DirectorySeparatorChar, '/');
                            writer.Write(relative);
                            writer.Write('\t');
                            writer.Write(info.Length);
                            writer.Write('\t');
                            writer.Write(info.LastWriteTimeUtc.Ticks);
                            writer.WriteLine();
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string MakeRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
            return relative.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static long SafeLastWriteTicks(string path)
        {
            try { return File.GetLastWriteTimeUtc(path).Ticks; }
            catch { return 0; }
        }
    }
}
