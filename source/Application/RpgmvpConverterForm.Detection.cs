using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    public sealed partial class RpgmvpConverterForm
    {
        private static GameEngine DetectEngine(string inputPath)
        {
            GameEngine direct = DetectDirectFileEngine(inputPath);
            if (direct != GameEngine.Unknown) return direct;
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath)) return GameEngine.Unknown;
            string pygameRoot = GetPygamePyInstallerRoot(inputPath);
            if (!string.Equals(pygameRoot, rootPath, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(pygameRoot)
                && IsPygamePyInstallerGame(pygameRoot))
                return GameEngine.PygamePyInstaller;
            if (AssetCollectors.IsGameMakerInput(rootPath)) return GameEngine.GameMaker;
            if (IsUnityGame(rootPath)) return GameEngine.Unity;
            if (IsRenpyGame(rootPath)) return GameEngine.Renpy;
            if (IsLegacyRpgMakerGame(rootPath)) return GameEngine.LegacyRpgMaker;
            if (HasRpgmFiles(rootPath)) return GameEngine.RpgMaker;
            if (IsGodotGame(rootPath)) return GameEngine.Godot;
            if (IsKirikiriGame(rootPath)) return GameEngine.Kirikiri;
            if (IsWolfRpgGame(rootPath)) return GameEngine.WolfRpg;
            if (IsTyranoScriptGame(rootPath)) return GameEngine.TyranoScript;
            if (IsUnrealGame(rootPath)) return GameEngine.Unreal;
            if (IsElectronGame(rootPath)) return GameEngine.Electron;
            if (IsNwjsGame(rootPath)) return GameEngine.Nwjs;
            if (IsSrpgStudioGame(rootPath)) return GameEngine.SrpgStudio;
            if (IsPixelGameMakerGame(rootPath)) return GameEngine.PixelGameMaker;
            if (IsJavaJarGame(rootPath)) return GameEngine.JavaJar;
            if (IsFlashGame(rootPath)) return GameEngine.Flash;
            if (IsSpakDatGame(rootPath)) return GameEngine.SpakDat;
            if (IsPygamePyInstallerGame(rootPath)) return GameEngine.PygamePyInstaller;
            if (AssetCollectors.IsHtmlGame(rootPath)) return GameEngine.Html;
            if (AssetCollectors.IsQspGame(rootPath)) return GameEngine.Qsp;
            if (AssetCollectors.IsRagsInput(rootPath)) return GameEngine.Rags;
            return GameEngine.Unknown;
        }

        private static GameEngine DetectEngineFast(string inputPath)
        {
            GameEngine direct = DetectDirectFileEngine(inputPath);
            if (direct != GameEngine.Unknown) return direct;
            string rootPath = InputDirectory(inputPath);
            if (!Directory.Exists(rootPath)) return GameEngine.Unknown;
            string pygameRoot = GetPygamePyInstallerRoot(inputPath);
            if (!string.Equals(pygameRoot, rootPath, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(pygameRoot)
                && IsPygamePyInstallerGame(pygameRoot))
                return GameEngine.PygamePyInstaller;
            if (AssetCollectors.IsGameMakerInput(rootPath)) return GameEngine.GameMaker;
            if (IsUnityGame(rootPath)) return GameEngine.Unity;
            if (IsRenpyGameFast(rootPath)) return GameEngine.Renpy;
            if (IsLegacyRpgMakerGame(rootPath)) return GameEngine.LegacyRpgMaker;
            if (HasRpgmFilesFast(rootPath)) return GameEngine.RpgMaker;
            if (IsGodotGameFast(rootPath)) return GameEngine.Godot;
            if (IsKirikiriGameFast(rootPath)) return GameEngine.Kirikiri;
            if (IsWolfRpgGame(rootPath)) return GameEngine.WolfRpg;
            if (IsTyranoScriptGame(rootPath)) return GameEngine.TyranoScript;
            if (IsUnrealGameFast(rootPath)) return GameEngine.Unreal;
            if (IsElectronGame(rootPath)) return GameEngine.Electron;
            if (IsNwjsGame(rootPath)) return GameEngine.Nwjs;
            if (IsSrpgStudioGame(rootPath)) return GameEngine.SrpgStudio;
            if (IsPixelGameMakerGame(rootPath)) return GameEngine.PixelGameMaker;
            if (IsJavaJarGame(rootPath)) return GameEngine.JavaJar;
            if (IsFlashGame(rootPath)) return GameEngine.Flash;
            if (IsSpakDatGame(rootPath)) return GameEngine.SpakDat;
            if (IsPygamePyInstallerGame(rootPath)) return GameEngine.PygamePyInstaller;
            if (AssetCollectors.IsHtmlGame(rootPath)) return GameEngine.Html;
            if (AssetCollectors.IsQspGame(rootPath)) return GameEngine.Qsp;
            if (AssetCollectors.IsRagsInput(rootPath)) return GameEngine.Rags;
            return GameEngine.Unknown;
        }

        private static GameEngine DetectDirectFileEngine(string path)
        {
            if (!File.Exists(path)) return GameEngine.Unknown;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".rpa": return GameEngine.Renpy;
                case ".apk": return GameEngine.AndroidApk;
                case ".pck": return GameEngine.Godot;
                case ".xp3": return GameEngine.Kirikiri;
                case ".pak":
                case ".utoc": return GameEngine.Unreal;
                case ".jar": return GameEngine.JavaJar;
                case ".swf": return GameEngine.Flash;
                case ".asar": return GameEngine.Electron;
                case ".qsp": return GameEngine.Qsp;
                case ".rag": return GameEngine.Rags;
                case ".rts":
                case ".dts":
                case ".srk":
                case ".srpgs": return GameEngine.SrpgStudio;
                case ".pgmproject":
                case ".pgmexport": return GameEngine.PixelGameMaker;
                case ".rgssad":
                case ".rgss2a":
                case ".rgss3a": return GameEngine.LegacyRpgMaker;
                case ".dat": return SpakDatExtractor.IsSpakArchive(path) ? GameEngine.SpakDat : GameEngine.Unknown;
                case ".exe": return IsGodotEmbeddedPckFile(path) ? GameEngine.Godot : GameEngine.Unknown;
                default: return GameEngine.Unknown;
            }
        }


        private static bool IsPygamePyInstallerGame(string rootPath)
        {
            string internalDir = Path.Combine(rootPath, "_internal");
            return Directory.Exists(Path.Combine(internalDir, "pygame"))
                && (Directory.Exists(Path.Combine(internalDir, "assets")) || File.Exists(Path.Combine(internalDir, "base_library.zip")));
        }
        private static bool IsUnityGame(string rootPath)
        {
            try
            {
                if (File.Exists(Path.Combine(rootPath, "UnityPlayer.dll"))) return true;
                foreach (string exe in EnumerateFilesTopLevelSafe(rootPath, "*.exe"))
                {
                    string name = Path.GetFileNameWithoutExtension(exe);
                    if (name.StartsWith("GameAssetTool", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string dataFolder = Path.Combine(rootPath, name + "_Data");
                    if (Directory.Exists(dataFolder)) return true;
                }
                foreach (string dataFolder in Directory.EnumerateDirectories(rootPath, "*_Data", SearchOption.TopDirectoryOnly))
                {
                    if (File.Exists(Path.Combine(dataFolder, "globalgamemanagers"))
                        || Directory.Exists(Path.Combine(dataFolder, "Managed"))
                        || Directory.Exists(Path.Combine(dataFolder, "Resources")))
                        return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static bool IsRenpyGame(string rootPath)
        {
            string gameFolder = Path.Combine(rootPath, "game");
            if (!Directory.Exists(gameFolder)) return false;
            return EnumerateFilesSafe(gameFolder, "*.rpa").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpy").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpyc").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpym").Any()
                || EnumerateFilesSafe(gameFolder, "*.rpymc").Any()
                || File.Exists(Path.Combine(rootPath, "renpy.exe"));
        }

        private static bool IsRenpyGameFast(string rootPath)
        {
            string gameFolder = Path.Combine(rootPath, "game");
            if (!Directory.Exists(gameFolder)) return false;
            return EnumerateFilesTopLevelSafe(gameFolder, "*.rpa").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpy").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpyc").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpym").Any()
                || EnumerateFilesTopLevelSafe(gameFolder, "*.rpymc").Any()
                || File.Exists(Path.Combine(rootPath, "renpy.exe"));
        }

        private static bool IsNwjsGame(string rootPath)
        {
            bool hasWww = Directory.Exists(Path.Combine(rootPath, "www"));
            bool hasPackage = File.Exists(Path.Combine(rootPath, "package.json"));
            bool hasPackageNw = File.Exists(Path.Combine(rootPath, "package.nw")) || Directory.Exists(Path.Combine(rootPath, "package.nw"));
            bool hasAppNw = File.Exists(Path.Combine(rootPath, "app.nw")) || Directory.Exists(Path.Combine(rootPath, "app.nw"));
            return hasWww || hasPackage || hasPackageNw || hasAppNw;
        }

        private static bool IsWolfRpgGame(string rootPath)
        {
            List<string> archives = FindWolfArchiveFiles(rootPath);
            bool hasWolfArchive = archives.Any(delegate(string path)
            {
                return path.EndsWith(".wolf", StringComparison.OrdinalIgnoreCase);
            });
            bool hasGameExe = File.Exists(Path.Combine(rootPath, "Game.exe"))
                || File.Exists(Path.Combine(rootPath, "GamePro.exe"));
            string dataFolder = Path.Combine(rootPath, "Data");
            bool hasLooseData = Directory.Exists(Path.Combine(dataFolder, "BasicData"))
                || File.Exists(Path.Combine(dataFolder, "BasicData", "Game.dat"));
            return hasWolfArchive || (hasGameExe && (archives.Count > 0 || hasLooseData));
        }

        private static bool IsTyranoScriptGame(string rootPath)
        {
            string dataFolder = Path.Combine(rootPath, "data");
            return Directory.Exists(Path.Combine(dataFolder, "scenario"))
                && (Directory.Exists(Path.Combine(dataFolder, "system"))
                    || Directory.Exists(Path.Combine(rootPath, "tyrano"))
                    || File.Exists(Path.Combine(rootPath, "index.html")));
        }

        private static bool IsJavaJarGame(string rootPath)
        {
            return EnumerateFilesTopLevelSafe(rootPath, "*.jar").Any() || IsJavaLooseResourceGame(rootPath);
        }

        private static bool IsSrpgStudioGame(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return false;
            return File.Exists(Path.Combine(rootPath, "runtime.rts"))
                || EnumerateFilesTopLevelSafe(rootPath, "*.rts").Any()
                || EnumerateFilesTopLevelSafe(rootPath, "*.dts").Any()
                || EnumerateFilesTopLevelSafe(rootPath, "*.srk").Any()
                || File.Exists(Path.Combine(rootPath, "Script", "base", "base-listcommand.js"));
        }

        private static bool IsPixelGameMakerGame(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return false;
            if (EnumerateFilesTopLevelSafe(rootPath, "*.pgmproject").Any()
                || EnumerateFilesTopLevelSafe(rootPath, "*.pgmexport").Any())
                return true;
            bool hasPlayer = File.Exists(Path.Combine(rootPath, "player.exe"))
                || File.Exists(Path.Combine(rootPath, "Player.exe"));
            bool hasPgmmvMarkers = Directory.Exists(Path.Combine(rootPath, "Resources"))
                || Directory.Exists(Path.Combine(rootPath, "resources"))
                || Directory.Exists(Path.Combine(rootPath, "fonts"))
                || EnumerateFilesSafe(rootPath, "*.sspj").Any();
            return hasPlayer && hasPgmmvMarkers;
        }

        private static bool IsFlashGame(string rootPath)
        {
            return AssetExtractorRegistry.Find("flash-swf").CanExtract(rootPath);
        }

        private static bool IsElectronGame(string rootPath)
        {
            return AssetExtractorRegistry.Find("electron-asar").CanExtract(rootPath);
        }

        private static bool IsSpakDatGame(string rootPath)
        {
            return AssetExtractorRegistry.Find("spak-dat").CanExtract(rootPath);
        }

        private static bool HasRpgmFiles(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.rpgmvp").Any() || EnumerateFilesSafe(rootPath, "*.png_").Any();
        }

        private static bool HasRpgmFilesFast(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "data", "System.json"))
                || File.Exists(Path.Combine(rootPath, "www", "data", "System.json"))
                || EnumerateFilesTopLevelSafe(rootPath, "*.rpgmvp").Any()
                || EnumerateFilesTopLevelSafe(rootPath, "*.png_").Any();
        }

        private static bool IsGodotGame(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "project.godot"))
                || EnumerateFilesSafe(rootPath, "*.pck").Any()
                || HasGodotEmbeddedPck(rootPath);
        }

        private static bool IsGodotGameFast(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "project.godot"))
                || EnumerateFilesTopLevelSafe(rootPath, "*.pck").Any()
                || HasGodotEmbeddedPck(rootPath);
        }

        private static bool HasGodotEmbeddedPck(string rootPath)
        {
            try
            {
                foreach (string executable in Directory.EnumerateFiles(rootPath, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    if (IsGodotEmbeddedPckFile(executable)) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsGodotEmbeddedPckFile(string executable)
        {
            try
            {
                if (!File.Exists(executable)) return false;
                using (FileStream stream = File.OpenRead(executable))
                {
                    if (stream.Length < 4) return false;
                    stream.Seek(-4, SeekOrigin.End);
                    byte[] footer = new byte[4];
                    return stream.Read(footer, 0, footer.Length) == footer.Length
                        && Encoding.ASCII.GetString(footer) == "GDPC";
                }
            }
            catch { return false; }
        }

        private static bool IsKirikiriGame(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.xp3").Any();
        }

        private static bool IsKirikiriGameFast(string rootPath)
        {
            return EnumerateFilesTopLevelSafe(rootPath, "*.xp3").Any();
        }

        private static bool IsUnrealGame(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.pak").Any() || EnumerateFilesSafe(rootPath, "*.utoc").Any();
        }

        private static bool IsUnrealGameFast(string rootPath)
        {
            if (EnumerateFilesTopLevelSafe(rootPath, "*.pak").Any() || EnumerateFilesTopLevelSafe(rootPath, "*.utoc").Any())
                return true;
            foreach (string folder in GetUnrealPakFolders(rootPath))
            {
                if (EnumerateFilesTopLevelSafe(folder, "*.pak").Any() || EnumerateFilesTopLevelSafe(folder, "*.utoc").Any())
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> GetUnrealPakFolders(string rootPath)
        {
            List<string> folders = new List<string>
            {
                Path.Combine(rootPath, "Content", "Paks"),
                Path.Combine(rootPath, "Engine", "Content", "Paks")
            };
            try
            {
                foreach (string folder in Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly))
                    folders.Add(Path.Combine(folder, "Content", "Paks"));
            }
            catch { }
            return folders;
        }

        private static ScanSummary BuildScanSummary(string inputPath)
        {
            return BuildScanSummaryCore(inputPath, CancellationToken.None, GameEngine.Unknown);
        }

        private static ScanSummary BuildScanSummaryCore(string inputPath, CancellationToken cancellationToken)
        {
            return BuildScanSummaryCore(inputPath, cancellationToken, GameEngine.Unknown);
        }

        private static ScanSummary BuildScanSummaryCore(string inputPath, CancellationToken cancellationToken, GameEngine forcedEngine)
        {
            string rootPath = InputDirectory(inputPath);
            cancellationToken.ThrowIfCancellationRequested();
            GameEngine detectedEngine = DetectEngine(inputPath);
            GameEngine engine = forcedEngine == GameEngine.Unknown ? detectedEngine : forcedEngine;
            IEnumerable<string> files;
            int archives;
            string routeHints = "";
            if (engine == GameEngine.Unity)
            {
                files = EnumerateFilesSafe(rootPath, "*.*").Where(delegate(string path)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    return ext == ".assets" || ext == ".bundle" || ext == ".ress"
                        || MediaTypeRegistry.IsMedia(ext)
                        || MediaTypeRegistry.IsText(ext);
                }).ToList();
                archives = files.Count(delegate(string path)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    return ext == ".assets" || ext == ".bundle";
                });
            }
            else if (engine == GameEngine.Renpy)
            {
                List<string> renpyArchives = FindRenpyArchives(inputPath);
                files = renpyArchives.Count > 0
                    ? renpyArchives
                    : AssetCollectors.GetLooseResourceFiles(GetRenpyGameFolder(inputPath), Path.Combine(rootPath, "extracted", "renpy", "loose"));
                archives = renpyArchives.Count;
            }
            else if (engine == GameEngine.Godot)
            {
                files = FindGodotArchives(inputPath);
                archives = files.Count();
            }
            else if (engine == GameEngine.Kirikiri)
            {
                files = EnumerateFilesSafe(rootPath, "*.xp3").ToList();
                archives = files.Count();
            }
            else if (engine == GameEngine.Unreal)
            {
                files = EnumerateFilesSafe(rootPath, "*.pak").Concat(EnumerateFilesSafe(rootPath, "*.utoc")).ToList();
                archives = files.Count();
            }
            else if (engine == GameEngine.Nwjs)
            {
                List<string> nwjsArchives = FindNwjsPackageArchives(rootPath);
                files = GetNwjsLooseFiles(rootPath, Path.Combine(rootPath, "extracted", "nwjs"))
                    .Concat(nwjsArchives)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                archives = nwjsArchives.Count;
            }
            else if (engine == GameEngine.WolfRpg)
            {
                List<string> wolfArchives = FindWolfArchiveFiles(rootPath);
                files = GetWolfLooseFiles(rootPath, Path.Combine(rootPath, "extracted", "wolf"))
                    .Concat(wolfArchives)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                archives = wolfArchives.Count;
            }
            else if (engine == GameEngine.TyranoScript)
            {
                files = GetTyranoFiles(rootPath, Path.Combine(rootPath, "extracted", "tyrano")).ToList();
                archives = 0;
            }
            else if (engine == GameEngine.JavaJar)
            {
                List<string> javaArchives = FindJavaArchives(inputPath);
                files = GetJavaLooseFiles(rootPath, Path.Combine(rootPath, "extracted", "java"), "images-svg")
                    .Concat(javaArchives)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                archives = javaArchives.Count;
            }
            else if (engine == GameEngine.AndroidApk)
            {
                List<string> apkArchives = FindApkFiles(inputPath);
                files = apkArchives;
                archives = apkArchives.Count;
                cancellationToken.ThrowIfCancellationRequested();
                routeHints = ApkDiagnosticBuilder.BuildDryRunSummary(apkArchives, 3);
            }
            else if (engine == GameEngine.SrpgStudio)
            {
                files = GetSrpgStudioFiles(inputPath, Path.Combine(rootPath, "extracted", "srpg-studio"));
                archives = files.Count(delegate(string path)
                {
                    string ext = Path.GetExtension(path);
                    return ext.Equals(".rts", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".dts", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".srk", StringComparison.OrdinalIgnoreCase);
                });
            }
            else if (engine == GameEngine.PixelGameMaker)
            {
                files = GetPixelGameMakerFiles(inputPath, Path.Combine(rootPath, "extracted", "pixel-game-maker"));
                archives = files.Count(delegate(string path)
                {
                    string ext = Path.GetExtension(path);
                    return ext.Equals(".pgmexport", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".pgmproject", StringComparison.OrdinalIgnoreCase);
                });
            }
            else if (engine == GameEngine.Flash)
            {
                files = FindFlashFiles(inputPath);
                archives = files.Count();
            }
            else if (engine == GameEngine.Electron)
            {
                files = FindElectronArchives(inputPath);
                archives = files.Count();
            }
            else if (engine == GameEngine.Html)
            {
                files = AssetCollectors.GetHtmlFiles(inputPath, Path.Combine(rootPath, "extracted", "html"));
                archives = 0;
            }
            else if (engine == GameEngine.Qsp)
            {
                files = AssetCollectors.GetQspFiles(inputPath, Path.Combine(rootPath, "extracted", "qsp"));
                archives = files.Count(delegate(string path) { return path.EndsWith(".qsp", StringComparison.OrdinalIgnoreCase); });
            }
            else if (engine == GameEngine.Rags)
            {
                files = AssetCollectors.FindInputFiles(inputPath, ".rag");
                archives = files.Count();
            }
            else if (engine == GameEngine.LegacyRpgMaker)
            {
                files = FindLegacyRpgMakerArchives(inputPath);
                archives = files.Count();
            }
            else if (engine == GameEngine.GameMaker)
            {
                files = AssetCollectors.FindGameMakerFiles(inputPath);
                archives = files.Count();
            }
            else if (engine == GameEngine.SpakDat)
            {
                archives = SpakDatExtractor.FindArchives(inputPath).Count;
                files = SpakDatExtractor.FindSourceFiles(inputPath);
            }
            else if (engine == GameEngine.PygamePyInstaller)
            {
                string pygameRoot = GetPygamePyInstallerRoot(inputPath);
                files = GetPygamePyInstallerFiles(pygameRoot, Path.Combine(pygameRoot, "extracted", "pygame"));
                archives = 0;
            }
            else
            {
                files = GetFilesToConvert(rootPath);
                archives = 0;
            }
            cancellationToken.ThrowIfCancellationRequested();
            List<string> fileList = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            long bytes = 0;
            foreach (string path in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bytes += SafeFileLength(path);
            }
            string unknownExtensions = BuildUnknownExtensionSummary(fileList);
            cancellationToken.ThrowIfCancellationRequested();
            string topExtensions = ExtractionReportBuilder.BuildFileExtensionSummary(fileList, 6);
            string largestFiles = ExtractionReportBuilder.BuildLargestFileSummary(rootPath, fileList, 3);
            return new ScanSummary(engine, fileList.Count, archives, bytes, unknownExtensions, topExtensions, largestFiles, routeHints);
        }

        private static string BuildUnknownExtensionSummary(IEnumerable<string> files)
        {
            List<string> unknown = files
                .Select(delegate(string path)
                {
                    string extension = Path.GetExtension(path);
                    return string.IsNullOrWhiteSpace(extension) ? "<no extension>" : extension.ToLowerInvariant();
                })
                .Where(delegate(string extension)
                {
                    return extension.Equals("<no extension>", StringComparison.OrdinalIgnoreCase)
                        || !MediaTypeRegistry.IsKnownDryRunExtension(extension);
                })
                .ToList();

            if (unknown.Count == 0) return "";
            return string.Join(", ", unknown
                .GroupBy(delegate(string extension) { return extension; }, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(delegate(IGrouping<string, string> group) { return group.Count(); })
                .ThenBy(delegate(IGrouping<string, string> group) { return group.Key; }, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(delegate(IGrouping<string, string> group) { return group.Key + " x" + group.Count(); })
                .ToArray());
        }

        private static List<string> GetFilesToConvert(string rootPath)
        {
            string[] skipped =
            {
                Path.Combine(rootPath, "www", "img", "tilesets") + Path.DirectorySeparatorChar,
                Path.Combine(rootPath, "www", "img", "weather") + Path.DirectorySeparatorChar,
                Path.Combine(rootPath, "img", "tilesets") + Path.DirectorySeparatorChar,
                Path.Combine(rootPath, "img", "weather") + Path.DirectorySeparatorChar
            };
            return EnumerateFilesSafe(rootPath, "*.*")
                .Where(delegate(string path)
                {
                    bool supported = path.EndsWith(".rpgmvp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".png_", StringComparison.OrdinalIgnoreCase);
                    return supported && !skipped.Any(delegate(string prefix) { return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase); });
                })
                .ToList();
        }

        private static IEnumerable<string> EnumerateFilesSafe(string rootPath, string pattern)
        {
            if (!Directory.Exists(rootPath)) return Enumerable.Empty<string>();
            try { return Directory.EnumerateFiles(rootPath, pattern, SearchOption.AllDirectories).ToList(); }
            catch { return Enumerable.Empty<string>(); }
        }

        private static IEnumerable<string> EnumerateFilesTopLevelSafe(string rootPath, string pattern)
        {
            if (!Directory.Exists(rootPath)) return Enumerable.Empty<string>();
            try { return Directory.EnumerateFiles(rootPath, pattern, SearchOption.TopDirectoryOnly).ToList(); }
            catch { return Enumerable.Empty<string>(); }
        }

        private static List<string> FindUnityBundleFiles(string rootPath)
        {
            return EnumerateFilesSafe(rootPath, "*.bundle").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> FindNwjsPackageArchives(string rootPath)
        {
            return new[] { "package.nw", "app.nw" }
                .Select(delegate(string name) { return Path.Combine(rootPath, name); })
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetRenpyGameFolder(string inputPath)
        {
            if (File.Exists(inputPath)) return Path.GetDirectoryName(Path.GetFullPath(inputPath));
            string rootPath = InputDirectory(inputPath);
            string gameFolder = Path.Combine(rootPath, "game");
            return Directory.Exists(gameFolder) ? gameFolder : rootPath;
        }

        private static List<string> FindGodotArchives(string inputPath)
        {
            if (File.Exists(inputPath))
            {
                string fullPath = Path.GetFullPath(inputPath);
                string extension = Path.GetExtension(fullPath);
                if (extension.Equals(".pck", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) && IsGodotEmbeddedPckFile(fullPath))
                    return new List<string> { fullPath };
                return new List<string>();
            }

            string rootPath = InputDirectory(inputPath);
            return EnumerateFilesSafe(rootPath, "*.pck")
                .Concat(EnumerateFilesTopLevelSafe(rootPath, "*.exe").Where(IsGodotEmbeddedPckFile))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> FindRenpyArchives(string inputPath)
        {
            if (File.Exists(inputPath) && inputPath.EndsWith(".rpa", StringComparison.OrdinalIgnoreCase))
                return new List<string> { Path.GetFullPath(inputPath) };
            return EnumerateFilesSafe(GetRenpyGameFolder(inputPath), "*.rpa").ToList();
        }

        private static List<string> FindWolfArchiveFiles(string rootPath)
        {
            string[] extensions = { ".wolf", ".data", ".pak", ".bin", ".assets", ".content", ".res", ".resource" };
            IEnumerable<string> roots = new[] { rootPath, Path.Combine(rootPath, "Data") }.Where(Directory.Exists);
            return roots
                .SelectMany(delegate(string folder) { return EnumerateFilesTopLevelSafe(folder, "*.*"); })
                .Where(delegate(string path) { return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetWolfLooseFiles(string rootPath, string outputDir)
        {
            string dataFolder = Path.Combine(rootPath, "Data");
            if (!Directory.Exists(dataFolder)) return new List<string>();
            HashSet<string> archives = new HashSet<string>(FindWolfArchiveFiles(rootPath), StringComparer.OrdinalIgnoreCase);
            return GetLooseFiles(dataFolder, outputDir)
                .Where(delegate(string path) { return !archives.Contains(path); })
                .ToList();
        }

        private static List<string> GetTyranoFiles(string rootPath, string outputDir)
        {
            string dataFolder = Path.Combine(rootPath, "data");
            return Directory.Exists(dataFolder) ? GetLooseFiles(dataFolder, outputDir) : new List<string>();
        }

        private static List<string> FindJavaArchives(string rootPath)
        {
            return AssetCollectors.FindInputFiles(rootPath, ".jar")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> FindApkFiles(string inputPath)
        {
            return AssetCollectors.FindInputFiles(inputPath, ".apk")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetSrpgStudioFiles(string inputPath, string outputDir)
        {
            return AssetCollectors.GetLooseResourceFiles(inputPath, outputDir)
                .Concat(AssetCollectors.FindInputFiles(inputPath, ".rts"))
                .Concat(AssetCollectors.FindInputFiles(inputPath, ".dts"))
                .Concat(AssetCollectors.FindInputFiles(inputPath, ".srk"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetPixelGameMakerFiles(string inputPath, string outputDir)
        {
            return AssetCollectors.GetLooseResourceFiles(inputPath, outputDir)
                .Concat(AssetCollectors.FindInputFiles(inputPath, ".pgmproject"))
                .Concat(AssetCollectors.FindInputFiles(inputPath, ".pgmexport"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsLegacyRpgMakerGame(string rootPath)
        {
            return FindLegacyRpgMakerArchives(rootPath).Count > 0;
        }

        private static List<string> FindLegacyRpgMakerArchives(string inputPath)
        {
            return new[] { ".rgssad", ".rgss2a", ".rgss3a" }
                .SelectMany(delegate(string extension) { return AssetCollectors.FindInputFiles(inputPath, extension); })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsJavaLooseResourceGame(string rootPath)
        {
            string resources = Path.Combine(rootPath, "res");
            if (!Directory.Exists(resources) || !EnumerateFilesTopLevelSafe(rootPath, "*.exe").Any())
                return false;
            try
            {
                return Directory.EnumerateDirectories(rootPath, "jre*", SearchOption.TopDirectoryOnly).Any();
            }
            catch { return false; }
        }

        private static List<string> GetJavaLooseFiles(string rootPath, string outputDir, string mode)
        {
            string resources = Path.Combine(rootPath, "res");
            if (!IsJavaLooseResourceGame(rootPath)) return new List<string>();
            List<string> files = GetLooseFiles(resources, outputDir);
            if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase)) return files;
            if (string.Equals(mode, "text", StringComparison.OrdinalIgnoreCase)) return files.Where(IsJavaTextFile).ToList();
            if (string.Equals(mode, "images-text", StringComparison.OrdinalIgnoreCase)) return files.Where(IsJavaImageOrTextFile).ToList();
            return files.Where(IsJavaImageFile).ToList();
        }
        private static bool IsJavaImageFile(string path)
        {
            return MediaTypeRegistry.IsImageLike(Path.GetExtension(path));
        }

        private static bool IsJavaTextFile(string path)
        {
            return MediaTypeRegistry.IsText(Path.GetExtension(path));
        }

        private static bool IsJavaImageOrTextFile(string path)
        {
            string extension = Path.GetExtension(path);
            return MediaTypeRegistry.IsImageLike(extension) || MediaTypeRegistry.IsText(extension);
        }
        private static List<string> FindFlashFiles(string rootPath)
        {
            return FlashSwfExtractor.FindFiles(rootPath);
        }

        private static List<string> FindElectronArchives(string rootPath)
        {
            return ElectronAsarExtractor.FindArchives(rootPath);
        }

        private static bool IsExistingInput(string path)
        {
            return Directory.Exists(path) || File.Exists(path);
        }

        private static string InputDirectory(string path)
        {
            return AssetCollectors.InputDirectory(path);
        }

        private static List<string> GetLooseFiles(string sourceRoot, string outputDir)
        {
            string outputPrefix = AppendDirectorySeparator(Path.GetFullPath(outputDir));
            string extractedPrefix = AppendDirectorySeparator(Path.GetFullPath(Path.Combine(sourceRoot, "extracted")));
            return EnumerateFilesSafe(sourceRoot, "*.*")
                .Where(delegate(string path)
                {
                    string fullPath = Path.GetFullPath(path);
                    return !fullPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)
                        && !fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetNwjsLooseFiles(string rootPath, string outputDir)
        {
            List<string> roots = new List<string>();
            foreach (string relative in new[] { "www", "package.nw", "app.nw" })
            {
                string candidate = Path.Combine(rootPath, relative);
                if (Directory.Exists(candidate)) roots.Add(candidate);
            }
            if (roots.Count == 0 && File.Exists(Path.Combine(rootPath, "package.json")))
                roots.Add(rootPath);

            string outputPrefix = AppendDirectorySeparator(Path.GetFullPath(outputDir));
            string extractedPrefix = AppendDirectorySeparator(Path.GetFullPath(Path.Combine(rootPath, "extracted")));
            HashSet<string> archivePaths = new HashSet<string>(FindNwjsPackageArchives(rootPath), StringComparer.OrdinalIgnoreCase);
            return roots
                .SelectMany(delegate(string root) { return EnumerateFilesSafe(root, "*.*"); })
                .Where(delegate(string path)
                {
                    string fullPath = Path.GetFullPath(path);
                    if (fullPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase)
                        || fullPath.StartsWith(extractedPrefix, StringComparison.OrdinalIgnoreCase)
                        || archivePaths.Contains(fullPath))
                        return false;
                    string extension = Path.GetExtension(fullPath);
                    return !new[] { ".exe", ".dll", ".pdb", ".log" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetPygamePyInstallerRoot(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return inputPath;
            string root = InputDirectory(inputPath);
            if (Path.GetFileName(root).Equals("_internal", StringComparison.OrdinalIgnoreCase))
                root = Directory.GetParent(root) != null ? Directory.GetParent(root).FullName : root;
            return root;
        }

        private static List<string> GetPygamePyInstallerFiles(string rootPath, string outputDir)
        {
            return IsPygamePyInstallerGame(rootPath)
                ? AssetCollectors.GetLooseResourceFiles(rootPath, outputDir)
                : new List<string>();
        }

        private static string TryFindGameRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            DirectoryInfo current = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;
            if (current != null && current.Name.Equals("_internal", StringComparison.OrdinalIgnoreCase)) current = current.Parent;
            int remainingParents = 6;
            while (current != null && remainingParents-- > 0)
            {
                string root = current.FullName;
                bool known = IsUnityGame(root)
                    || IsGodotGameFast(root)
                    || IsKirikiriGameFast(root)
                    || IsWolfRpgGame(root)
                    || IsTyranoScriptGame(root)
                    || IsUnrealGameFast(root)
                    || IsJavaJarGame(root)
                    || IsFlashGame(root)
                    || IsElectronGame(root)
                    || IsSpakDatGame(root)
                    || IsPygamePyInstallerGame(root)
                    || IsLegacyRpgMakerGame(root)
                    || AssetCollectors.IsHtmlGame(root)
                    || AssetCollectors.IsQspGame(root)
                    || AssetCollectors.IsRagsInput(root)
                    || AssetCollectors.IsGameMakerInput(root)
                    || Directory.Exists(Path.Combine(root, "www"))
                    || Directory.Exists(Path.Combine(root, "game"))
                    || File.Exists(Path.Combine(root, "package.json"))
                    || File.Exists(Path.Combine(root, "package.nw"))
                    || Directory.Exists(Path.Combine(root, "package.nw"))
                    || File.Exists(Path.Combine(root, "app.nw"))
                    || Directory.Exists(Path.Combine(root, "app.nw"));
                if (known) return root;
                current = current.Parent;
            }
            return null;
        }

        private enum GameEngine
        {
            Unknown,
            RpgMaker,
            Renpy,
            Unity,
            Godot,
            Kirikiri,
            Unreal,
            Nwjs,
            WolfRpg,
            TyranoScript,
            JavaJar,
            Flash,
            Electron,
            Html,
            Qsp,
            Rags,
            LegacyRpgMaker,
            GameMaker,
            AndroidApk,
            SrpgStudio,
            PixelGameMaker,
            SpakDat,
            PygamePyInstaller
        }

        private sealed class ScanSummary
        {
            public ScanSummary(GameEngine engine, int fileCount, int archiveCount, long totalBytes, string unknownExtensions, string topExtensions, string largestFiles, string routeHints)
            {
                Engine = engine;
                FileCount = fileCount;
                ArchiveCount = archiveCount;
                TotalBytes = totalBytes;
                UnknownExtensions = unknownExtensions ?? "";
                TopExtensions = topExtensions ?? "";
                LargestFiles = largestFiles ?? "";
                RouteHints = routeHints ?? "";
            }

            public GameEngine Engine { get; private set; }
            public int FileCount { get; private set; }
            public int ArchiveCount { get; private set; }
            public long TotalBytes { get; private set; }
            public string UnknownExtensions { get; private set; }
            public string TopExtensions { get; private set; }
            public string LargestFiles { get; private set; }
            public string RouteHints { get; private set; }
        }

    }
}
