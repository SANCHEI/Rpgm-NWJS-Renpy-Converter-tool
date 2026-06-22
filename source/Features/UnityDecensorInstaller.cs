using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace RpgmvpConverterWinForms
{
    internal enum UnityRuntimeKind
    {
        MonoBe5,
        MonoBe6,
        Il2Cpp
    }

    internal sealed class UnityDecensorEnvironment
    {
        public string GameRoot { get; set; }
        public string GameExecutable { get; set; }
        public string Architecture { get; set; }
        public UnityRuntimeKind Runtime { get; set; }
        public string ExistingBepInEx { get; set; }

        public string SwDecensorVariant
        {
            get
            {
                if (Runtime == UnityRuntimeKind.Il2Cpp) return "IL2CPP";
                return Runtime == UnityRuntimeKind.MonoBe6 ? "BE6" : "BE5";
            }
        }

        public string DisplayName
        {
            get
            {
                string runtime = Runtime == UnityRuntimeKind.Il2Cpp
                    ? "IL2CPP / BE6"
                    : Runtime == UnityRuntimeKind.MonoBe6 ? "Mono / BE6" : "Mono / BE5";
                return runtime + " / " + Architecture
                    + (string.IsNullOrWhiteSpace(ExistingBepInEx) ? "" : " / installed: " + ExistingBepInEx);
            }
        }
    }

    internal sealed class BepInExPackage
    {
        public string Channel { get; set; }
        public string ArtifactId { get; set; }
        public string Runtime { get; set; }
        public string Architecture { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }

        public string DisplayName
        {
            get { return Channel + " " + Runtime + " " + Architecture + " - " + Version; }
        }
    }

    internal sealed class UnityDecensorManifest
    {
        public string BepInExPackage { get; set; }
        public string SwDecensorVariant { get; set; }
        public List<string> InstalledFiles { get; set; }
        public List<string> BepInExFiles { get; set; }

        public UnityDecensorManifest()
        {
            InstalledFiles = new List<string>();
            BepInExFiles = new List<string>();
        }
    }

    internal sealed class UnityDoorstopDiagnostic
    {
        public string InstalledPackage { get; set; }
        public string ProxyName { get; set; }
        public bool BepInExLogExists { get; set; }
        public bool LauncherLoadsWinHttp { get; set; }
        public bool LikelyDoorstopConflict { get; set; }
        public string Details { get; set; }
    }

    internal static class UnityDecensorInstaller
    {
        private const string ManifestName = ".gameassettool-unity-decensor.json";
        private const string EmbeddedSwDecensorResource = "RpgmvpConverterWinForms.tools.SW_Decensor_v0.7.4.2.zip";
        private const string Be6BuildsPage = "https://builds.bepinex.dev/projects/bepinex_be";
        private const long MaxDownloadBytes = 128L * 1024 * 1024;
        private const long MaxArchiveEntryBytes = 128L * 1024 * 1024;
        private const long MaxArchiveExpandedBytes = 512L * 1024 * 1024;
        private const int MaxArchiveEntries = 10000;
        private const int VisibleBuildCount = 3;
        private const int MaxLauncherScanBytes = 8 * 1024 * 1024;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static UnityDecensorEnvironment DetectEnvironment(string rootPath)
        {
            rootPath = Path.GetFullPath(rootPath);
            if (!Directory.Exists(rootPath))
                throw new DirectoryNotFoundException("Unity game folder was not found.");

            string dataPath = Directory.EnumerateDirectories(rootPath, "*_Data", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dataPath))
                throw new InvalidDataException("Unity *_Data folder was not found.");

            string executable = FindGameExecutable(rootPath, dataPath);
            bool il2cpp = File.Exists(Path.Combine(rootPath, "GameAssembly.dll"))
                || Directory.Exists(Path.Combine(dataPath, "il2cpp_data"));
            string existingBepInEx = DetectExistingBepInEx(rootPath);
            return new UnityDecensorEnvironment
            {
                GameRoot = rootPath,
                GameExecutable = executable,
                Architecture = DetectPeArchitecture(executable),
                Runtime = il2cpp
                    ? UnityRuntimeKind.Il2Cpp
                    : existingBepInEx == "BE5" ? UnityRuntimeKind.MonoBe5 : UnityRuntimeKind.MonoBe6,
                ExistingBepInEx = existingBepInEx
            };
        }

        public static List<BepInExPackage> FetchLatestPackages()
        {
            List<BepInExPackage> packages = new List<BepInExPackage>();
            using (WebClient client = CreateWebClient())
                AddBe6Packages(packages, client.DownloadString(Be6BuildsPage));
            return packages;
        }

        public static BepInExPackage FindRecommendedPackage(IEnumerable<BepInExPackage> packages, UnityDecensorEnvironment environment)
        {
            if (environment.Runtime == UnityRuntimeKind.MonoBe5)
                return null;
            string runtime = environment.Runtime == UnityRuntimeKind.Il2Cpp ? "IL2CPP" : "Mono";
            return packages.FirstOrDefault(delegate(BepInExPackage package)
            {
                return package.Runtime == runtime
                    && package.Architecture == environment.Architecture;
            });
        }

        public static List<BepInExPackage> GetCompatiblePackages(IEnumerable<BepInExPackage> packages, UnityDecensorEnvironment environment)
        {
            if (environment.Runtime == UnityRuntimeKind.MonoBe5)
                return new List<BepInExPackage>();
            string runtime = environment.Runtime == UnityRuntimeKind.Il2Cpp ? "IL2CPP" : "Mono";
            return packages.Where(delegate(BepInExPackage package)
            {
                return package.Runtime == runtime && package.Architecture == environment.Architecture;
            }).ToList();
        }

        public static string GetInstalledPackageDisplayName(string rootPath)
        {
            UnityDecensorManifest manifest = ReadManifest(rootPath);
            if (!string.IsNullOrWhiteSpace(manifest.BepInExPackage))
                return manifest.BepInExPackage;
            string existing = DetectExistingBepInEx(Path.GetFullPath(rootPath));
            return string.IsNullOrWhiteSpace(existing) ? "not installed" : existing + " unmanaged";
        }

        public static bool IsBepInExLaunchConfirmed(string rootPath)
        {
            return File.Exists(Path.Combine(Path.GetFullPath(rootPath), "BepInEx", "LogOutput.log"));
        }

        public static string GetLauncherRiskWarning(string rootPath)
        {
            rootPath = Path.GetFullPath(rootPath);
            List<string> markers = new List<string>();
            if (File.Exists(Path.Combine(rootPath, "startup.exe"))) markers.Add("startup.exe");
            if (File.Exists(Path.Combine(rootPath, "launcher.exe"))) markers.Add("launcher.exe");
            if (LauncherReferencesWinHttp(rootPath)) markers.Add("launcher references winhttp.dll");
            if (markers.Count == 0) return "";
            return "This game appears to use a custom launcher (" + string.Join(", ", markers.Distinct().ToArray()) + "). BepInEx Doorstop uses a proxy DLL and may conflict with custom launchers. Install anyway?";
        }

        public static void InstallBepInExPackage(string rootPath, BepInExPackage package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.Url))
                throw new InvalidOperationException("BepInEx package URL is missing.");

            string tempFile = Path.Combine(Path.GetTempPath(), "GameAssetTool-BepInEx-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (WebClient client = CreateWebClient())
                    client.DownloadFile(package.Url, tempFile);
                if (new FileInfo(tempFile).Length > MaxDownloadBytes)
                    throw new InvalidDataException("Downloaded BepInEx ZIP is unexpectedly large.");
                InstallZip(rootPath, tempFile, package.DisplayName);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        public static string InstallSwDecensorZip(string rootPath, string zipPath)
        {
            using (FileStream input = File.OpenRead(zipPath))
            using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
                return InstallSwDecensorArchive(rootPath, archive);
        }

        public static string InstallEmbeddedSwDecensor(string rootPath)
        {
            using (Stream input = typeof(UnityDecensorInstaller).Assembly.GetManifestResourceStream(EmbeddedSwDecensorResource))
            {
                if (input == null)
                    throw new InvalidOperationException("Embedded SW_Decensor package is missing.");
                using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
                    return InstallSwDecensorArchive(rootPath, archive);
            }
        }

        private static string InstallSwDecensorArchive(string rootPath, ZipArchive archive)
        {
            UnityDecensorEnvironment environment = DetectEnvironment(rootPath);
            string selectedEntry = SelectSwDecensorEntry(archive, environment.SwDecensorVariant);
            ZipArchiveEntry entry = archive.GetEntry(selectedEntry);
            EnsureEntrySize(entry);
            string pluginRelative = NormalizeRelativePath(Path.Combine(
                "BepInEx",
                "plugins",
                "SW_Decensor",
                Path.GetFileName(entry.Name)));
            UnityDecensorManifest existingManifest = ReadManifest(rootPath);
            string destination = SafeDestination(rootPath, pluginRelative);
            if (File.Exists(destination) && !existingManifest.InstalledFiles.Contains(pluginRelative, StringComparer.OrdinalIgnoreCase))
                throw new IOException("Refusing to overwrite an existing plugin file: " + pluginRelative);
            WriteManagedFile(rootPath, pluginRelative, entry);
            UnityDecensorManifest manifest = ReadManifest(rootPath);
            manifest.SwDecensorVariant = environment.SwDecensorVariant;
            WriteManifest(rootPath, manifest);
            return selectedEntry;
        }

        public static bool IsManagedInstallPresent(string rootPath)
        {
            return File.Exists(GetManifestPath(rootPath));
        }

        public static void RemoveManagedInstall(string rootPath)
        {
            rootPath = Path.GetFullPath(rootPath);
            UnityDecensorManifest manifest = ReadManifest(rootPath);
            foreach (string relative in manifest.InstalledFiles.OrderByDescending(delegate(string item) { return item.Length; }))
            {
                string path = SafeDestination(rootPath, relative);
                TryDeleteFile(path);
                TryDeleteEmptyParents(Path.GetDirectoryName(path), rootPath);
            }
            TryDeleteFile(GetManifestPath(rootPath));
        }

        public static UnityDoorstopDiagnostic DiagnoseDoorstop(string rootPath)
        {
            rootPath = Path.GetFullPath(rootPath);
            string proxy = new[] { "winhttp.dll", "version.dll", "winmm.dll" }
                .FirstOrDefault(delegate(string name) { return File.Exists(Path.Combine(rootPath, name)); }) ?? "not found";
            string logPath = Path.Combine(rootPath, "BepInEx", "LogOutput.log");
            bool logExists = File.Exists(logPath);
            bool launcherLoadsWinHttp = LauncherReferencesWinHttp(rootPath);
            string installed = GetInstalledPackageDisplayName(rootPath);
            bool managed = IsManagedInstallPresent(rootPath);
            bool likelyConflict = managed && proxy != "not found" && !logExists && launcherLoadsWinHttp;
            StringBuilder details = new StringBuilder();
            details.AppendLine("Installed package: " + installed);
            details.AppendLine("Doorstop proxy: " + proxy);
            details.AppendLine("BepInEx log: " + (logExists ? logPath : "not created"));
            details.AppendLine("Launcher references winhttp.dll: " + (launcherLoadsWinHttp ? "yes" : "no"));
            details.AppendLine();
            if (likelyConflict)
                details.AppendLine("Likely Doorstop conflict: the game launcher loads winhttp.dll before BepInEx can create its log. Use Remove Managed Files to restore the original launch path.");
            else if (managed && !logExists)
                details.AppendLine("BepInEx has not created a log yet. Launch the game once, then run diagnostics again. If the game crashes immediately, remove the managed files.");
            else if (logExists)
                details.AppendLine("BepInEx created its log. Review LogOutput.log if a plugin still fails.");
            else
                details.AppendLine("No managed BepInEx installation was found.");
            return new UnityDoorstopDiagnostic
            {
                InstalledPackage = installed,
                ProxyName = proxy,
                BepInExLogExists = logExists,
                LauncherLoadsWinHttp = launcherLoadsWinHttp,
                LikelyDoorstopConflict = likelyConflict,
                Details = details.ToString()
            };
        }

        private static bool LauncherReferencesWinHttp(string rootPath)
        {
            foreach (string path in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(delegate(string path)
                {
                    string name = Path.GetFileName(path).ToLowerInvariant();
                    return name == "launcher.c" || name == "startup.c" || name == "launcher.exe" || name == "startup.exe";
                }))
            {
                try
                {
                    byte[] data;
                    using (FileStream stream = File.OpenRead(path))
                    {
                        int length = (int)Math.Min(stream.Length, MaxLauncherScanBytes);
                        data = new byte[length];
                        int offset = 0;
                        while (offset < length)
                        {
                            int read = stream.Read(data, offset, length - offset);
                            if (read == 0) break;
                            offset += read;
                        }
                    }
                    string text = Encoding.ASCII.GetString(data);
                    if (text.IndexOf("winhttp.dll", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static void InstallZip(string rootPath, string zipPath, string packageName)
        {
            rootPath = Path.GetFullPath(rootPath);
            UnityDecensorManifest originalManifest = ReadManifest(rootPath);
            string originalManifestJson = File.Exists(GetManifestPath(rootPath))
                ? File.ReadAllText(GetManifestPath(rootPath), Encoding.UTF8)
                : null;
            string transactionRoot = Path.Combine(Path.GetTempPath(), "GameAssetTool-BepInEx-transaction-" + Guid.NewGuid().ToString("N"));
            string stagingRoot = Path.Combine(transactionRoot, "staging");
            string backupRoot = Path.Combine(transactionRoot, "backup");
            List<string> packageFiles = new List<string>();
            List<string> appliedFiles = new List<string>();
            List<string> backedUpFiles = new List<string>();
            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(backupRoot);
            try
            {
                using (FileStream input = File.OpenRead(zipPath))
                using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    EnsureArchiveLimits(archive);
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                        string relative = NormalizeRelativePath(entry.FullName);
                        string destination = SafeDestination(rootPath, relative);
                        if (File.Exists(destination) && !originalManifest.InstalledFiles.Contains(relative, StringComparer.OrdinalIgnoreCase))
                            throw new IOException("Refusing to overwrite an existing game file: " + relative);
                        packageFiles.Add(relative);
                        string staged = SafeDestination(stagingRoot, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(staged));
                        using (Stream source = entry.Open())
                        using (FileStream output = File.Create(staged))
                            source.CopyTo(output);
                    }
                }

                foreach (string relative in packageFiles)
                {
                    string destination = SafeDestination(rootPath, relative);
                    if (File.Exists(destination))
                    {
                        string backup = SafeDestination(backupRoot, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Copy(destination, backup, true);
                        backedUpFiles.Add(relative);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(SafeDestination(stagingRoot, relative), destination, true);
                    appliedFiles.Add(relative);
                }

                UnityDecensorManifest manifest = ReadManifest(rootPath);
                foreach (string relative in packageFiles)
                    if (!manifest.InstalledFiles.Contains(relative, StringComparer.OrdinalIgnoreCase))
                        manifest.InstalledFiles.Add(relative);
                manifest.BepInExFiles = packageFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                manifest.BepInExPackage = packageName;
                WriteManifest(rootPath, manifest);
            }
            catch
            {
                RollBackBepInExTransaction(rootPath, backupRoot, appliedFiles, backedUpFiles, originalManifestJson);
                throw;
            }
            finally
            {
                TryDeleteDirectory(transactionRoot);
            }
        }

        private static void RollBackBepInExTransaction(
            string rootPath,
            string backupRoot,
            List<string> appliedFiles,
            List<string> backedUpFiles,
            string originalManifestJson)
        {
            foreach (string relative in appliedFiles.OrderByDescending(delegate(string item) { return item.Length; }))
            {
                string destination = SafeDestination(rootPath, relative);
                if (backedUpFiles.Contains(relative, StringComparer.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(SafeDestination(backupRoot, relative), destination, true);
                }
                else
                {
                    TryDeleteFile(destination);
                    TryDeleteEmptyParents(Path.GetDirectoryName(destination), rootPath);
                }
            }
            if (originalManifestJson == null)
                TryDeleteFile(GetManifestPath(rootPath));
            else
                File.WriteAllText(GetManifestPath(rootPath), originalManifestJson, new UTF8Encoding(false));
        }

        private static void WriteManagedFile(string rootPath, string relative, ZipArchiveEntry entry)
        {
            EnsureEntrySize(entry);
            rootPath = Path.GetFullPath(rootPath);
            UnityDecensorManifest manifest = ReadManifest(rootPath);
            string destination = SafeDestination(rootPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (Stream input = entry.Open())
            using (FileStream output = File.Create(destination))
                input.CopyTo(output);
            if (!manifest.InstalledFiles.Contains(relative, StringComparer.OrdinalIgnoreCase))
                manifest.InstalledFiles.Add(relative);
            WriteManifest(rootPath, manifest);
        }

        private static void EnsureArchiveLimits(ZipArchive archive)
        {
            if (archive.Entries.Count > MaxArchiveEntries)
                throw new InvalidDataException("Archive contains too many files.");
            long expandedBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                EnsureEntrySize(entry);
                expandedBytes += entry.Length;
                if (expandedBytes > MaxArchiveExpandedBytes)
                    throw new InvalidDataException("Archive expands beyond the supported size limit.");
            }
        }

        private static void EnsureEntrySize(ZipArchiveEntry entry)
        {
            if (entry == null)
                throw new InvalidDataException("Archive entry is missing.");
            if (entry.Length > MaxArchiveEntryBytes)
                throw new InvalidDataException("Archive entry is unexpectedly large: " + entry.FullName);
        }

        private static string SelectSwDecensorEntry(ZipArchive archive, string variant)
        {
            List<string> dlls = archive.Entries
                .Where(delegate(ZipArchiveEntry entry)
                {
                    return entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        && entry.Name.IndexOf("SW_Decensor", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .Select(delegate(ZipArchiveEntry entry) { return entry.FullName; })
                .ToList();
            string match = dlls.FirstOrDefault(delegate(string path)
            {
                return GetSwDecensorMarkers(variant).Any(delegate(string marker)
                {
                    return path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            });
            if (string.IsNullOrWhiteSpace(match) && dlls.Count == 1)
                match = dlls[0];
            if (string.IsNullOrWhiteSpace(match))
                throw new InvalidDataException("SW_Decensor " + variant + " DLL was not found in the selected ZIP.");
            return match;
        }

        private static string[] GetSwDecensorMarkers(string variant)
        {
            if (variant == "IL2CPP") return new[] { "il2cpp" };
            if (variant == "BE6") return new[] { "be6", "bepinex6", "bepinex 6", "bepinex_6", "bepinex-6" };
            return new[] { "be5", "bepinex5", "bepinex 5", "bepinex_5", "bepinex-5" };
        }

        private static void AddBe6Packages(List<BepInExPackage> packages, string html)
        {
            MatchCollection artifacts = Regex.Matches(
                html,
                @"href=""(?<url>/projects/bepinex_be/(?<id>\d+)/BepInEx-Unity\.(?<runtime>Mono|IL2CPP)-win-(?<arch>x86|x64)-(?<version>6\.0\.0-be\.[^""]+)\.zip)""",
                RegexOptions.IgnoreCase);
            if (artifacts.Count == 0)
                throw new InvalidDataException("Latest BepInEx 6 artifact was not found.");
            List<string> buildIds = artifacts.Cast<Match>()
                .Select(delegate(Match match) { return match.Groups["id"].Value; })
                .Distinct()
                .Take(VisibleBuildCount)
                .ToList();
            foreach (Match match in artifacts)
            {
                int buildIndex = buildIds.IndexOf(match.Groups["id"].Value);
                if (buildIndex < 0) continue;
                packages.Add(new BepInExPackage
                {
                    Channel = buildIndex == 0 ? "Latest" : buildIndex == 1 ? "Previous" : "Fallback",
                    ArtifactId = match.Groups["id"].Value,
                    Runtime = match.Groups["runtime"].Value.Equals("IL2CPP", StringComparison.OrdinalIgnoreCase) ? "IL2CPP" : "Mono",
                    Architecture = match.Groups["arch"].Value.ToLowerInvariant(),
                    Version = WebUtility.UrlDecode(match.Groups["version"].Value),
                    Url = "https://builds.bepinex.dev" + match.Groups["url"].Value
                });
            }
        }

        private static string FindGameExecutable(string rootPath, string dataPath)
        {
            string expected = Path.Combine(rootPath, Path.GetFileName(dataPath).Substring(0, Path.GetFileName(dataPath).Length - 5) + ".exe");
            if (File.Exists(expected)) return expected;
            string executable = Directory.EnumerateFiles(rootPath, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(delegate(string path) { return !Path.GetFileName(path).Equals("UnityCrashHandler64.exe", StringComparison.OrdinalIgnoreCase); });
            if (string.IsNullOrWhiteSpace(executable))
                throw new InvalidDataException("Unity game executable was not found.");
            return executable;
        }

        private static string DetectPeArchitecture(string executable)
        {
            using (FileStream stream = File.OpenRead(executable))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Game executable is not a PE file.");
                stream.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550) throw new InvalidDataException("Game executable PE header is invalid.");
                ushort machine = reader.ReadUInt16();
                if (machine == 0x8664) return "x64";
                if (machine == 0x014C) return "x86";
                throw new InvalidDataException("Unsupported Unity executable architecture: 0x" + machine.ToString("X4"));
            }
        }

        private static bool IsBepInEx6Installed(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "BepInEx", "core", "BepInEx.Core.dll"));
        }

        private static string DetectExistingBepInEx(string rootPath)
        {
            if (IsBepInEx6Installed(rootPath)) return "BE6";
            if (File.Exists(Path.Combine(rootPath, "BepInEx", "core", "BepInEx.dll"))) return "BE5";
            return "";
        }

        private static UnityDecensorManifest ReadManifest(string rootPath)
        {
            string path = GetManifestPath(rootPath);
            if (!File.Exists(path)) return new UnityDecensorManifest();
            UnityDecensorManifest manifest = Json.Deserialize<UnityDecensorManifest>(File.ReadAllText(path, Encoding.UTF8));
            if (manifest == null) return new UnityDecensorManifest();
            if (manifest.InstalledFiles == null) manifest.InstalledFiles = new List<string>();
            if (manifest.BepInExFiles == null) manifest.BepInExFiles = new List<string>();
            return manifest;
        }

        private static void WriteManifest(string rootPath, UnityDecensorManifest manifest)
        {
            File.WriteAllText(GetManifestPath(rootPath), Json.Serialize(manifest), new UTF8Encoding(false));
        }

        private static string GetManifestPath(string rootPath)
        {
            return SafeDestination(Path.GetFullPath(rootPath), ManifestName);
        }

        private static string NormalizeRelativePath(string relative)
        {
            string normalized = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidDataException("Archive entry path is empty.");
            return normalized;
        }

        private static string SafeDestination(string rootPath, string relative)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(rootPath, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive entry escapes the game folder: " + relative);
            return path;
        }

        private static WebClient CreateWebClient()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "GameAssetTool/2.4.1";
            return client;
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }

        private static void TryDeleteEmptyParents(string directory, string rootPath)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
            while (!string.IsNullOrWhiteSpace(directory)
                && directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(directory).Length != 0) break;
                    Directory.Delete(directory);
                    directory = Path.GetDirectoryName(directory);
                }
                catch { break; }
            }
        }
    }
}
