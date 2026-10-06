using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace RpgmvpConverterWinForms
{
    internal sealed class DirectorySnapshotFile
    {
        public DirectorySnapshotFile(string fullPath, string extensionLower, long length, long lastWriteTicks, bool isTopLevel)
        {
            FullPath = fullPath;
            ExtensionLower = extensionLower;
            Length = length;
            LastWriteTicks = lastWriteTicks;
            IsTopLevel = isTopLevel;
        }

        public string FullPath { get; private set; }
        public string ExtensionLower { get; private set; }
        public long Length { get; private set; }
        public long LastWriteTicks { get; private set; }
        public bool IsTopLevel { get; private set; }
    }

    internal sealed class DirectorySnapshot
    {
        private readonly List<DirectorySnapshotFile> files = new List<DirectorySnapshotFile>();
        private readonly Dictionary<string, List<DirectorySnapshotFile>> byExtension =
            new Dictionary<string, List<DirectorySnapshotFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DirectorySnapshotFile> byPath =
            new Dictionary<string, DirectorySnapshotFile>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> topLevelFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string RootPath { get; private set; }
        public long TotalBytes { get; private set; }
        public long NewestTicks { get; private set; }

        public IList<DirectorySnapshotFile> Files { get { return files; } }
        public int Count { get { return files.Count; } }

        public string Fingerprint { get { return "d:" + Count + ":" + TotalBytes + ":" + NewestTicks; } }

        private DirectorySnapshot(string rootPath)
        {
            RootPath = rootPath;
        }

        public bool HasExtension(string extensionLower)
        {
            if (string.IsNullOrEmpty(extensionLower)) return false;
            return byExtension.ContainsKey(extensionLower);
        }

        public bool HasTopLevelExtension(string extensionLower)
        {
            List<DirectorySnapshotFile> list;
            if (!byExtension.TryGetValue(extensionLower, out list)) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].IsTopLevel) return true;
            return false;
        }

        public bool HasExtensionInDirectory(string directoryPrefix, string extensionLower)
        {
            List<DirectorySnapshotFile> list;
            if (!byExtension.TryGetValue(extensionLower, out list)) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].FullPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool HasAnyExtensionInDirectory(string directoryPrefix, params string[] extensions)
        {
            for (int i = 0; i < extensions.Length; i++)
                if (HasExtensionInDirectory(directoryPrefix, extensions[i])) return true;
            return false;
        }

        public List<string> GetFilesWithExtension(string extensionLower)
        {
            List<DirectorySnapshotFile> list;
            if (!byExtension.TryGetValue(extensionLower, out list)) return new List<string>();
            List<string> result = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++) result.Add(list[i].FullPath);
            return result;
        }

        public List<string> GetTopLevelFilesWithExtension(string extensionLower)
        {
            List<DirectorySnapshotFile> list;
            if (!byExtension.TryGetValue(extensionLower, out list)) return new List<string>();
            List<string> result = new List<string>();
            for (int i = 0; i < list.Count; i++)
                if (list[i].IsTopLevel) result.Add(list[i].FullPath);
            return result;
        }

        public List<string> GetFilesWithExtensionInDirectory(string directoryPrefix, string extensionLower)
        {
            List<DirectorySnapshotFile> list;
            if (!byExtension.TryGetValue(extensionLower, out list)) return new List<string>();
            List<string> result = new List<string>();
            for (int i = 0; i < list.Count; i++)
                if (list[i].FullPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(list[i].FullPath);
            return result;
        }

        public bool HasTopLevelFile(string fileName)
        {
            return topLevelFileNames.Contains(fileName);
        }

        public long GetLength(string fullPath)
        {
            DirectorySnapshotFile entry;
            if (byPath.TryGetValue(fullPath, out entry)) return entry.Length;
            try { return new FileInfo(fullPath).Length; }
            catch { return 0; }
        }

        public Dictionary<string, long> BuildLengthMap(IEnumerable<string> paths)
        {
            Dictionary<string, long> map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                DirectorySnapshotFile entry;
                if (byPath.TryGetValue(path, out entry))
                    map[path] = entry.Length;
                else
                {
                    try { map[path] = new FileInfo(path).Length; }
                    catch { map[path] = 0; }
                }
            }
            return map;
        }

        public static DirectorySnapshot Create(string rootPath, CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(rootPath);
            DirectorySnapshot snapshot = new DirectorySnapshot(root);
            string rootWithSep = root.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? root : root + Path.DirectorySeparatorChar;
            try
            {
                snapshot.NewestTicks = Directory.GetLastWriteTimeUtc(root).Ticks;
            }
            catch { }

            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                bool isTopDir = string.Equals(
                    directory.TrimEnd(Path.DirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
                try
                {
                    DateTime dirWrite = Directory.GetLastWriteTimeUtc(directory);
                    if (dirWrite.Ticks > snapshot.NewestTicks) snapshot.NewestTicks = dirWrite.Ticks;
                }
                catch { }

                string[] filePaths;
                try { filePaths = Directory.GetFiles(directory); }
                catch { continue; }

                for (int i = 0; i < filePaths.Length; i++)
                {
                    if ((i & 511) == 0) cancellationToken.ThrowIfCancellationRequested();
                    string fullPath;
                    try { fullPath = Path.GetFullPath(filePaths[i]); }
                    catch { continue; }
                    // Skip previous extraction output to keep scan focused on inputs.
                    if (fullPath.StartsWith(rootWithSep + "extracted" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string ext;
                    try { ext = Path.GetExtension(fullPath).ToLowerInvariant(); }
                    catch { ext = ""; }
                    long length = 0;
                    long ticks = 0;
                    try
                    {
                        FileInfo info = new FileInfo(fullPath);
                        if (info.Exists)
                        {
                            length = info.Length;
                            ticks = info.LastWriteTimeUtc.Ticks;
                        }
                    }
                    catch { }
                    DirectorySnapshotFile entry = new DirectorySnapshotFile(fullPath, ext, length, ticks, isTopDir);
                    snapshot.files.Add(entry);
                    snapshot.byPath[fullPath] = entry;
                    List<DirectorySnapshotFile> list;
                    if (!snapshot.byExtension.TryGetValue(ext, out list))
                    {
                        list = new List<DirectorySnapshotFile>();
                        snapshot.byExtension[ext] = list;
                    }
                    list.Add(entry);
                    if (isTopDir)
                    {
                        try { snapshot.topLevelFileNames.Add(Path.GetFileName(fullPath)); }
                        catch { }
                    }
                    snapshot.TotalBytes += length;
                    if (ticks > snapshot.NewestTicks) snapshot.NewestTicks = ticks;
                }

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(directory); }
                catch { continue; }
                for (int i = 0; i < subdirs.Length; i++)
                {
                    // Skip extracted output tree entirely.
                    try
                    {
                        string full = Path.GetFullPath(subdirs[i]);
                        if (string.Equals(full, rootWithSep + "extracted", StringComparison.OrdinalIgnoreCase) ||
                            full.StartsWith(rootWithSep + "extracted" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    catch { }
                    pending.Push(subdirs[i]);
                }
            }
            return snapshot;
        }
    }
}
