using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ReleaseInspector
{
    private static int Main(string[] args)
    {
        if ((args.Length != 1 && args.Length != 2) || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: ReleaseInspector.exe <GameAssetTool.exe> [upstream-rgss-fixtures]");
            return 2;
        }

        string temp = Path.Combine(Path.GetTempPath(), "GameAssetTool-UnlockerTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            Assembly assembly = Assembly.LoadFile(Path.GetFullPath(args[0]));
            string resourceName = "RpgmvpConverterWinForms.scripts.extract_unity.py";
            bool hasUnityScript = assembly.GetManifestResourceNames().Contains(resourceName);
            string unityScript = ReadResourceText(assembly, resourceName);
            bool unityFilteringFix = unityScript.Contains("if not supported:")
                && unityScript.Contains("mesh.export(\"obj\")")
                && unityScript.Contains("RESULT:{0}:{1}:{2}:{3}:{4}");
            bool hasGodotScript = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.scripts.extract_godot.py");
            bool hasXp3Script = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.scripts.extract_xp3.py");
            bool hasUnrealScript = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.scripts.extract_unreal.py");
            bool hasGameMakerScript = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.scripts.extract_gamemaker.py");
            bool hasPortableRuntime = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.runtime.runtime-win-x64.zip");
            bool hasWolfCli = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.tools.UberWolfCli.exe");
            bool hasResvg = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.tools.resvg.exe");

            Type unlockerType = assembly.GetType("RpgmvpConverterWinForms.UnlockerResources", true);
            MethodInfo extractUnlocker = unlockerType.GetMethod("ExtractUnlocker", BindingFlags.Public | BindingFlags.Static);
            string unlockerOutput = Path.Combine(temp, "unlocker");
            extractUnlocker.Invoke(null, new object[] { "soft", unlockerOutput });

            string[] files = Directory.GetFiles(unlockerOutput, "*.rpy", SearchOption.AllDirectories);
            bool nestedGameFolder = Directory.Exists(Path.Combine(unlockerOutput, "game"));

            Type formType = assembly.GetType("RpgmvpConverterWinForms.RpgmvpConverterForm", true);
            MethodInfo detectEngine = formType.GetMethod("DetectEngine", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo detectEngineFast = formType.GetMethod("DetectEngineFast", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo buildScanSummary = formType.GetMethod("BuildScanSummary", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo tryFindGameRoot = formType.GetMethod("TryFindGameRoot", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo canInstallUnlocker = formType.GetMethod("CanInstallUnlocker", BindingFlags.NonPublic | BindingFlags.Static);
            string unityEngine = DetectEngine(detectEngine, Path.Combine(temp, "unity"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "Sample_Data"));
            });
            string renpyEngine = DetectEngine(detectEngine, Path.Combine(temp, "renpy"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "game"));
                File.WriteAllBytes(Path.Combine(path, "game", "archive.rpa"), new byte[] { 0 });
            });
            string renpyLoosePath = Path.Combine(temp, "renpy-loose");
            string renpyLooseEngine = DetectEngine(detectEngine, renpyLoosePath, delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "game"));
                File.WriteAllText(Path.Combine(path, "game", "script.rpy"), "label start:");
                File.WriteAllText(Path.Combine(path, "game", "hero.png"), "png");
            });
            object renpyLooseSummary = buildScanSummary.Invoke(null, new object[] { renpyLoosePath });
            bool renpyLooseScan = GetInt(renpyLooseSummary, "ArchiveCount") == 0
                && GetInt(renpyLooseSummary, "FileCount") == 2;
            string rpgmEngine = DetectEngine(detectEngine, Path.Combine(temp, "rpgm"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "image.rpgmvp"), new byte[] { 0 });
            });
            string godotEngine = DetectEngine(detectEngine, Path.Combine(temp, "godot"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "game.pck"), new byte[] { 0 });
            });
            string kirikiriEngine = DetectEngine(detectEngine, Path.Combine(temp, "kirikiri"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "data.xp3"), new byte[] { 0 });
            });
            string unrealEngine = DetectEngine(detectEngine, Path.Combine(temp, "unreal"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "pakchunk0-Windows.pak"), new byte[] { 0 });
            });
            string nwjsEngine = DetectEngine(detectEngine, Path.Combine(temp, "nwjs"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "package.json"), "{}");
            });
            string wolfEngine = DetectEngine(detectEngine, Path.Combine(temp, "wolf"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "Data", "BasicData"));
                File.WriteAllBytes(Path.Combine(path, "Data", "BasicData", "Game.dat"), new byte[] { 0 });
                File.WriteAllBytes(Path.Combine(path, "Game.exe"), new byte[] { 0 });
            });
            string tyranoEngine = DetectEngine(detectEngine, Path.Combine(temp, "tyrano"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "data", "scenario"));
                Directory.CreateDirectory(Path.Combine(path, "data", "system"));
                File.WriteAllText(Path.Combine(path, "data", "scenario", "first.ks"), "*start");
            });
            string javaEngine = DetectEngine(detectEngine, Path.Combine(temp, "java"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "game.jar"), new byte[] { 0 });
            });
            string javaLooseEngine = DetectEngine(detectEngine, Path.Combine(temp, "java-loose"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "jre1.8.0"));
                Directory.CreateDirectory(Path.Combine(path, "res", "images"));
                File.WriteAllBytes(Path.Combine(path, "Game.exe"), new byte[] { 0 });
                File.WriteAllText(Path.Combine(path, "res", "images", "hero.png"), "png");
            });
            string flashEngine = DetectEngine(detectEngine, Path.Combine(temp, "flash"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                WriteMinimalSwf(Path.Combine(path, "game.swf"));
            });
            string electronEngine = DetectEngine(detectEngine, Path.Combine(temp, "electron"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "resources"));
                WriteMinimalAsar(Path.Combine(path, "resources", "app.asar"));
            });
            string htmlEngine = DetectEngine(detectEngine, Path.Combine(temp, "html"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "assets"));
                File.WriteAllText(Path.Combine(path, "index.html"), "<html></html>");
                File.WriteAllText(Path.Combine(path, "assets", "hero.jpg"), "jpg");
            });
            string qspEngine = DetectEngine(detectEngine, Path.Combine(temp, "qsp"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "game.qsp"), "qsp");
            });
            string ragsPath = Path.Combine(temp, "sample.rag");
            WriteMinimalRags(ragsPath);
            string ragsEngine = DetectExistingEngine(detectEngine, ragsPath);
            string legacyRpgMakerEngine = DetectEngine(detectEngine, Path.Combine(temp, "legacy-rpg-maker"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "Game.rgss3a"), new byte[] { 0 });
            });
            string gameMakerEngine = DetectEngine(detectEngine, Path.Combine(temp, "gamemaker"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                WriteMinimalDataWin(Path.Combine(path, "data.win"));
            });
            string spakDatEngine = DetectEngine(detectEngine, Path.Combine(temp, "spak-dat"), delegate(string path)
            {
                Directory.CreateDirectory(path);
                WriteMinimalSpak(Path.Combine(path, "media.dat"));
            });
            string directFlashEngine = DetectExistingEngine(detectEngine, Path.Combine(temp, "flash", "game.swf"));
            string directElectronEngine = DetectExistingEngine(detectEngine, Path.Combine(temp, "electron", "resources", "app.asar"));
            string genericGameFolderEngine = DetectEngine(detectEngine, Path.Combine(temp, "generic-game-folder"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "game"));
            });
            bool renpyUnlockerScope = (bool)canInstallUnlocker.Invoke(null, new object[] { Path.Combine(temp, "renpy") })
                && !(bool)canInstallUnlocker.Invoke(null, new object[] { Path.Combine(temp, "nwjs") })
                && !(bool)canInstallUnlocker.Invoke(null, new object[] { Path.Combine(temp, "generic-game-folder") });
            bool fastDetection = DetectExistingEngine(detectEngineFast, Path.Combine(temp, "unity")) == "Unity"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "renpy")) == "Renpy"
                && DetectExistingEngine(detectEngineFast, renpyLoosePath) == "Renpy"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "rpgm")) == "RpgMaker"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "godot")) == "Godot"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "kirikiri")) == "Kirikiri"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "unreal")) == "Unreal"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "nwjs")) == "Nwjs"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "wolf")) == "WolfRpg"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "tyrano")) == "TyranoScript"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "java")) == "JavaJar"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "java-loose")) == "JavaJar"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "flash")) == "Flash"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "electron")) == "Electron"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "html")) == "Html"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "qsp")) == "Qsp"
                && DetectExistingEngine(detectEngineFast, ragsPath) == "Rags"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "legacy-rpg-maker")) == "LegacyRpgMaker"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "gamemaker")) == "GameMaker"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "spak-dat")) == "SpakDat"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "flash", "game.swf")) == "Flash"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "electron", "resources", "app.asar")) == "Electron"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "generic-game-folder")) == "Unknown";

            string rootLookup = Path.Combine(temp, "root-lookup");
            string nestedLookup = Path.Combine(rootLookup, "one", "two");
            Directory.CreateDirectory(nestedLookup);
            File.WriteAllBytes(Path.Combine(rootLookup, "data.xp3"), new byte[] { 0 });
            string foundRoot = (string)tryFindGameRoot.Invoke(null, new object[] { nestedLookup });
            bool fastRootLookup = string.Equals(foundRoot, rootLookup, StringComparison.OrdinalIgnoreCase);
            bool contextualGui = VerifyContextualGui(formType);
            bool nwjsExtraction = VerifyNwjsExtraction(formType, temp);
            bool localEngineExtraction = VerifyLocalEngineExtraction(formType, temp);
            bool collectorExtraction = VerifyCollectorExtraction(assembly, temp);
            bool legacyRpgMakerExtraction = VerifyLegacyRpgMakerExtraction(assembly, formType, temp);
            bool upstreamRgssFixtures = args.Length < 2 || VerifyUpstreamRgssFixtures(assembly, args[1], temp);
            bool startupFolderArgument = VerifyStartupFolderArgument(formType, temp);
            bool startupFileArgument = VerifyStartupFileArgument(formType, temp);
            bool droppedFolderArgument = VerifyDroppedFolderArgument(formType, temp);
            bool extractorRegistry = assembly.GetType("RpgmvpConverterWinForms.IAssetExtractor", true).IsInterface
                && assembly.GetType("RpgmvpConverterWinForms.AssetExtractorRegistry", true) != null;

            Type runtimeType = assembly.GetType("RpgmvpConverterWinForms.PortableRuntime", true);
            MethodInfo ensureRuntime = runtimeType.GetMethod("EnsureExtracted", BindingFlags.Public | BindingFlags.Static);
            MethodInfo createPythonProcessInfo = runtimeType.GetMethod("CreatePythonProcessInfo", BindingFlags.Public | BindingFlags.Static);
            MethodInfo cleanupRuntime = runtimeType.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static);
            string pythonPath = null;
            string runtimeDirectory = null;
            bool portableImports = false;
            bool runtimeRemoved = false;
            bool wolfCliExtracted = false;
            bool wolfCliRemoved = false;
            bool resvgExtracted = false;
            bool resvgRemoved = false;
            try
            {
                string staleSession = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GameAssetTool",
                    "runtime",
                    "session-99999999-stale");
                Directory.CreateDirectory(staleSession);
                File.WriteAllText(Path.Combine(staleSession, "stale.txt"), "stale");

                pythonPath = (string)ensureRuntime.Invoke(null, null);
                runtimeDirectory = Path.GetDirectoryName(pythonPath);
                ProcessStartInfo psi = (ProcessStartInfo)createPythonProcessInfo.Invoke(null, null);
                psi.Arguments = "-c \"import unrpa, UnityPy, pyuepak, zstandard; from pyuepak.aes_windows import aes_cfb_decrypt; print('portable-runtime-ok')\"";
                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    portableImports = process.ExitCode == 0 && output.Contains("portable-runtime-ok");
                }
            }
            finally
            {
                cleanupRuntime.Invoke(null, null);
                runtimeRemoved = string.IsNullOrWhiteSpace(runtimeDirectory) || !Directory.Exists(runtimeDirectory);
            }

            Type toolRuntimeType = assembly.GetType("RpgmvpConverterWinForms.ToolRuntime", true);
            MethodInfo ensureWolfCli = toolRuntimeType.GetMethod("EnsureWolfCliExtracted", BindingFlags.Public | BindingFlags.Static);
            MethodInfo ensureResvg = toolRuntimeType.GetMethod("EnsureResvgExtracted", BindingFlags.Public | BindingFlags.Static);
            MethodInfo cleanupTools = toolRuntimeType.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static);
            string wolfCliPath = null;
            string resvgPath = null;
            try
            {
                wolfCliPath = (string)ensureWolfCli.Invoke(null, null);
                wolfCliExtracted = File.Exists(wolfCliPath);
                resvgPath = (string)ensureResvg.Invoke(null, null);
                resvgExtracted = File.Exists(resvgPath);
            }
            finally
            {
                cleanupTools.Invoke(null, null);
                wolfCliRemoved = string.IsNullOrWhiteSpace(wolfCliPath) || !File.Exists(wolfCliPath);
                resvgRemoved = string.IsNullOrWhiteSpace(resvgPath) || !File.Exists(resvgPath);
            }

            Console.WriteLine("AssemblyVersion=" + assembly.GetName().Version);
            Console.WriteLine("EmbeddedUnityScript=" + hasUnityScript);
            Console.WriteLine("UnityFilteringFix=" + unityFilteringFix);
            Console.WriteLine("EmbeddedGodotScript=" + hasGodotScript);
            Console.WriteLine("EmbeddedXp3Script=" + hasXp3Script);
            Console.WriteLine("EmbeddedUnrealScript=" + hasUnrealScript);
            Console.WriteLine("EmbeddedGameMakerScript=" + hasGameMakerScript);
            Console.WriteLine("EmbeddedPortableRuntime=" + hasPortableRuntime);
            Console.WriteLine("EmbeddedWolfCli=" + hasWolfCli);
            Console.WriteLine("EmbeddedResvg=" + hasResvg);
            Console.WriteLine("PortableRuntimeImports=" + portableImports);
            Console.WriteLine("PortableRuntimeRemoved=" + runtimeRemoved);
            Console.WriteLine("WolfCliExtracted=" + wolfCliExtracted);
            Console.WriteLine("WolfCliRemoved=" + wolfCliRemoved);
            Console.WriteLine("ResvgExtracted=" + resvgExtracted);
            Console.WriteLine("ResvgRemoved=" + resvgRemoved);
            Console.WriteLine("UnlockerFiles=" + files.Length);
            Console.WriteLine("UnexpectedNestedGameFolder=" + nestedGameFolder);
            Console.WriteLine("DetectUnity=" + unityEngine);
            Console.WriteLine("DetectRenpy=" + renpyEngine);
            Console.WriteLine("DetectRenpyLoose=" + renpyLooseEngine);
            Console.WriteLine("RenpyLooseScan=" + renpyLooseScan);
            Console.WriteLine("DetectRpgMaker=" + rpgmEngine);
            Console.WriteLine("DetectGodot=" + godotEngine);
            Console.WriteLine("DetectKirikiri=" + kirikiriEngine);
            Console.WriteLine("DetectUnreal=" + unrealEngine);
            Console.WriteLine("DetectNwjs=" + nwjsEngine);
            Console.WriteLine("DetectWolfRpg=" + wolfEngine);
            Console.WriteLine("DetectTyranoScript=" + tyranoEngine);
            Console.WriteLine("DetectJavaJar=" + javaEngine);
            Console.WriteLine("DetectJavaLoose=" + javaLooseEngine);
            Console.WriteLine("DetectFlash=" + flashEngine);
            Console.WriteLine("DetectElectron=" + electronEngine);
            Console.WriteLine("DetectHtml=" + htmlEngine);
            Console.WriteLine("DetectQsp=" + qspEngine);
            Console.WriteLine("DetectRags=" + ragsEngine);
            Console.WriteLine("DetectLegacyRpgMaker=" + legacyRpgMakerEngine);
            Console.WriteLine("DetectGameMaker=" + gameMakerEngine);
            Console.WriteLine("DetectDirectFlash=" + directFlashEngine);
            Console.WriteLine("DetectDirectElectron=" + directElectronEngine);
            Console.WriteLine("DetectGenericGameFolder=" + genericGameFolderEngine);
            Console.WriteLine("RenpyUnlockerScope=" + renpyUnlockerScope);
            Console.WriteLine("FastDetection=" + fastDetection);
            Console.WriteLine("FastRootLookup=" + fastRootLookup);
            Console.WriteLine("ContextualGui=" + contextualGui);
            Console.WriteLine("NwjsExtraction=" + nwjsExtraction);
            Console.WriteLine("LocalEngineExtraction=" + localEngineExtraction);
            Console.WriteLine("CollectorExtraction=" + collectorExtraction);
            Console.WriteLine("LegacyRpgMakerExtraction=" + legacyRpgMakerExtraction);
            Console.WriteLine("UpstreamRgssFixtures=" + upstreamRgssFixtures);
            Console.WriteLine("StartupFolderArgument=" + startupFolderArgument);
            Console.WriteLine("StartupFileArgument=" + startupFileArgument);
            Console.WriteLine("DroppedFolderArgument=" + droppedFolderArgument);
            Console.WriteLine("ExtractorRegistry=" + extractorRegistry);

            return hasUnityScript
                && unityFilteringFix
                && hasGodotScript
                && hasXp3Script
                && hasUnrealScript
                && hasGameMakerScript
                && hasPortableRuntime
                && hasWolfCli
                && hasResvg
                && portableImports
                && runtimeRemoved
                && wolfCliExtracted
                && wolfCliRemoved
                && resvgExtracted
                && resvgRemoved
                && files.Length > 0
                && !nestedGameFolder
                && unityEngine == "Unity"
                && renpyEngine == "Renpy"
                && renpyLooseEngine == "Renpy"
                && renpyLooseScan
                && rpgmEngine == "RpgMaker"
                && godotEngine == "Godot"
                && kirikiriEngine == "Kirikiri"
                && unrealEngine == "Unreal"
                && nwjsEngine == "Nwjs"
                && wolfEngine == "WolfRpg"
                && tyranoEngine == "TyranoScript"
                && javaEngine == "JavaJar"
                && javaLooseEngine == "JavaJar"
                && flashEngine == "Flash"
                && electronEngine == "Electron"
                && htmlEngine == "Html"
                && qspEngine == "Qsp"
                && ragsEngine == "Rags"
                && legacyRpgMakerEngine == "LegacyRpgMaker"
                && gameMakerEngine == "GameMaker"
                && directFlashEngine == "Flash"
                && directElectronEngine == "Electron"
                && genericGameFolderEngine == "Unknown"
                && renpyUnlockerScope
                && fastDetection
                && fastRootLookup
                && contextualGui
                && nwjsExtraction
                && localEngineExtraction
                && collectorExtraction
                && legacyRpgMakerExtraction
                && upstreamRgssFixtures
                && startupFolderArgument
                && startupFileArgument
                && droppedFolderArgument
                && extractorRegistry
                ? 0
                : 1;
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static string DetectEngine(MethodInfo detectEngine, string path, Action<string> arrange)
    {
        arrange(path);
        object value = detectEngine.Invoke(null, new object[] { path });
        return value.ToString();
    }

    private static string ReadResourceText(Assembly assembly, string name)
    {
        using (Stream stream = assembly.GetManifestResourceStream(name))
        using (StreamReader reader = new StreamReader(stream))
            return reader.ReadToEnd();
    }

    private static string DetectExistingEngine(MethodInfo detectEngine, string path)
    {
        object value = detectEngine.Invoke(null, new object[] { path });
        return value.ToString();
    }

    private static bool VerifyContextualGui(Type formType)
    {
        object form = Activator.CreateInstance(formType);
        try
        {
            Type engineType = formType.GetNestedType("GameEngine", BindingFlags.NonPublic);
            MethodInfo updateContext = formType.GetMethod("UpdateEngineContext", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo toggleLog = formType.GetMethod("ToggleLog", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo setDragHighlight = formType.GetMethod("SetDragHighlight", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo scaleLogicalHeightForDpi = formType.GetMethod("ScaleLogicalHeightForDpi", BindingFlags.NonPublic | BindingFlags.Static);
            object startButton = GetField(formType, form, "startButton");
            object collectLooseButton = GetField(formType, form, "collectLooseButton");
            object keyBox = GetField(formType, form, "keyBox");
            object keyLabel = GetField(formType, form, "keyLabel");
            object unityMode = GetField(formType, form, "unityExtractModeBox");
            object javaMode = GetField(formType, form, "javaExtractModeBox");
            object extractionHint = GetField(formType, form, "extractionHintLabel");
            object unlockerButton = GetField(formType, form, "unlockerButton");
            object unlockerSection = GetField(formType, form, "unlockerSectionLabel");
            object unlockerMode = GetField(formType, form, "unlockerModeBox");
            object removeUnlockerButton = GetField(formType, form, "removeUnlockerButton");
            object pauseButton = GetField(formType, form, "pauseButton");
            object cancelButton = GetField(formType, form, "cancelButton");
            object openOutputButton = GetField(formType, form, "openOutputButton");
            object logPanel = GetField(formType, form, "logPanel");
            object toggleLogButton = GetField(formType, form, "toggleLogButton");
            object runtimeStatus = GetField(formType, form, "runtimeStatusLabel");
            object toolTip = GetField(formType, form, "actionToolTip");
            object language = GetField(formType, form, "languageBox");
            IEnumerable dragPanels = (IEnumerable)GetField(formType, form, "dragHighlightPanels");

            bool initial = !GetBool(startButton, "Enabled")
                && !GetLocalVisible(keyBox)
                && !GetLocalVisible(unityMode)
                && !GetLocalVisible(javaMode)
                && !GetLocalVisible(logPanel)
                && !GetLocalVisible(unlockerSection)
                && GetString(runtimeStatus, "Text").StartsWith("Runtime:", StringComparison.Ordinal);

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Unity") });
            bool unity = GetBool(startButton, "Enabled")
                && GetLocalVisible(unityMode)
                && !GetLocalVisible(keyBox)
                && !GetLocalVisible(unlockerSection);

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Renpy") });
            bool renpy = GetBool(startButton, "Enabled")
                && GetLocalVisible(unlockerSection)
                && GetLocalVisible(unlockerMode)
                && GetLocalVisible(unlockerButton);

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "RpgMaker") });
            bool rpgm = GetBool(startButton, "Enabled")
                && GetLocalVisible(keyBox)
                && !GetLocalVisible(unityMode)
                && GetString(keyLabel, "Text") == "RPGM HEX key";

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Godot") });
            bool godot = GetBool(startButton, "Enabled")
                && GetLocalVisible(keyBox)
                && GetString(keyLabel, "Text") == "Godot PCK key";

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Unreal") });
            bool unreal = GetBool(startButton, "Enabled")
                && GetLocalVisible(keyBox)
                && !GetLocalVisible(unityMode)
                && GetString(keyLabel, "Text") == "Unreal AES key";

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Nwjs") });
            bool nwjs = GetBool(startButton, "Enabled")
                && !GetBool(unlockerButton, "Enabled")
                && !GetLocalVisible(unlockerSection)
                && GetString(extractionHint, "Text").IndexOf("Unlocker", StringComparison.OrdinalIgnoreCase) < 0;

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "WolfRpg") });
            bool wolf = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("UberWolf", StringComparison.OrdinalIgnoreCase) >= 0;

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "TyranoScript") });
            bool tyrano = GetBool(startButton, "Enabled");

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "JavaJar") });
            bool java = GetBool(startButton, "Enabled")
                && GetLocalVisible(javaMode)
                && !GetLocalVisible(unityMode);

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Flash") });
            bool flash = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("FLV", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Electron") });
            bool electron = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("app.asar", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Html") });
            bool html = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("HTML", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Qsp") });
            bool qsp = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("QSP", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Rags") });
            bool rags = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("RAGS", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "LegacyRpgMaker") });
            bool legacyRpgMaker = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("RGSS", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "GameMaker") });
            bool gameMaker = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("data.win", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Unknown") });
            bool unknown = !GetBool(startButton, "Enabled")
                && GetString(startButton, "Text") == "Recover Embedded Assets";
            bool readableDisabledButtons = HasReadableDisabledContrast(startButton)
                && HasReadableDisabledContrast(collectLooseButton)
                && HasReadableDisabledContrast(unlockerButton)
                && HasReadableDisabledContrast(removeUnlockerButton)
                && HasReadableDisabledContrast(pauseButton)
                && HasReadableDisabledContrast(cancelButton)
                && HasReadableDisabledContrast(openOutputButton);
            bool tooltips = !string.IsNullOrWhiteSpace((string)toolTip.GetType().GetMethod("GetToolTip").Invoke(toolTip, new[] { startButton }))
                && !string.IsNullOrWhiteSpace((string)toolTip.GetType().GetMethod("GetToolTip").Invoke(toolTip, new[] { openOutputButton }))
                && !string.IsNullOrWhiteSpace((string)toolTip.GetType().GetMethod("GetToolTip").Invoke(toolTip, new[] { javaMode }));
            bool dpi = form.GetType().GetProperty("AutoScaleMode").GetValue(form, null).ToString() == "Dpi"
                && (int)scaleLogicalHeightForDpi.Invoke(null, new object[] { 570, 120f }) == 712
                && (int)scaleLogicalHeightForDpi.Invoke(null, new object[] { 570, 144f }) == 855;
            language.GetType().GetProperty("SelectedIndex").SetValue(language, 1, null);
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "RpgMaker") });
            bool russian = GetString(startButton, "Text") == "Извлечь ресурсы"
                && GetString(keyLabel, "Text") == "HEX-ключ RPGM";
            language.GetType().GetProperty("SelectedIndex").SetValue(language, 0, null);
            setDragHighlight.Invoke(form, new object[] { true });
            bool dragHighlight = dragPanels.Cast<object>().Count() == 4 && dragPanels.Cast<object>().All(GetLocalVisible);
            setDragHighlight.Invoke(form, new object[] { false });
            dragHighlight = dragHighlight && dragPanels.Cast<object>().All(delegate(object panel) { return !GetLocalVisible(panel); });

            int compactHeight = GetSizeHeight(form, "ClientSize");
            toggleLog.Invoke(form, null);
            int expandedHeight = GetSizeHeight(form, "ClientSize");
            bool expanded = GetString(toggleLogButton, "Text") == "Hide Log" && expandedHeight > compactHeight;
            toggleLog.Invoke(form, null);
            bool collapsed = GetString(toggleLogButton, "Text") == "Show Log"
                && GetSizeHeight(form, "ClientSize") == compactHeight;

            return initial && unity && renpy && rpgm && godot && unreal && nwjs && wolf && tyrano && java && flash && electron
                && html && qsp && rags && legacyRpgMaker && gameMaker && unknown && russian
                && readableDisabledButtons && tooltips && dpi && dragHighlight && expanded && collapsed;
        }
        finally
        {
            MethodInfo dispose = formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            dispose.Invoke(form, null);
        }
    }

    private static bool VerifyNwjsExtraction(Type formType, string temp)
    {
        string game = Path.Combine(temp, "nwjs-extraction");
        string www = Path.Combine(game, "www");
        string output = Path.Combine(game, "extracted", "nwjs");
        Directory.CreateDirectory(www);
        File.WriteAllText(Path.Combine(www, "index.html"), "<html></html>");
        using (FileStream stream = File.Create(Path.Combine(game, "package.nw")))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("scripts/app.js").Open()))
                writer.Write("console.log('ok');");
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open()))
                writer.Write("kept-inside-output");
        }

        object form = Activator.CreateInstance(formType);
        try
        {
            MethodInfo extract = formType.GetMethod("RunNwjsExtraction", BindingFlags.NonPublic | BindingFlags.Instance);
            object result = extract.Invoke(form, new object[] { game, output });
            int extracted = (int)result.GetType().GetProperty("Extracted").GetValue(result, null);
            int errors = (int)result.GetType().GetProperty("Errors").GetValue(result, null);
            return extracted == 2
                && errors == 0
                && File.Exists(Path.Combine(output, "loose", "www", "index.html"))
                && File.Exists(Path.Combine(output, "archives", "package", "scripts", "app.js"))
                && !File.Exists(Path.Combine(game, "extracted", "outside.txt"));
        }
        finally
        {
            MethodInfo dispose = formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            dispose.Invoke(form, null);
        }
    }

    private static bool VerifyLocalEngineExtraction(Type formType, string temp)
    {
        string root = Path.Combine(temp, "local-engine-extraction");
        string tyrano = Path.Combine(root, "tyrano");
        Directory.CreateDirectory(Path.Combine(tyrano, "data", "scenario"));
        Directory.CreateDirectory(Path.Combine(tyrano, "data", "system"));
        File.WriteAllText(Path.Combine(tyrano, "data", "scenario", "first.ks"), "*start");

        string java = Path.Combine(root, "java");
        Directory.CreateDirectory(java);
        Directory.CreateDirectory(Path.Combine(java, "jre1.8.0"));
        Directory.CreateDirectory(Path.Combine(java, "res", "images"));
        File.WriteAllBytes(Path.Combine(java, "Game.exe"), new byte[] { 0 });
        File.WriteAllText(Path.Combine(java, "res", "images", "hero.png"), "loose-png");
        File.WriteAllText(Path.Combine(java, "res", "images", "vector.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><rect width=\"16\" height=\"16\" fill=\"#f00\"/></svg>");
        File.WriteAllText(Path.Combine(java, "res", "images", "metadata.xml"), "<metadata/>");
        using (FileStream stream = File.Create(Path.Combine(java, "game.jar")))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("assets/picture.png").Open()))
                writer.Write("png");
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("assets/vector.svg").Open()))
                writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><circle cx=\"8\" cy=\"8\" r=\"8\" fill=\"#0f0\"/></svg>");
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("assets/metadata.xml").Open()))
                writer.Write("<metadata/>");
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open()))
                writer.Write("inside-output");
        }

        string flash = Path.Combine(root, "flash");
        Directory.CreateDirectory(flash);
        WriteMinimalSwf(Path.Combine(flash, "movie.swf"));

        string electron = Path.Combine(root, "electron");
        Directory.CreateDirectory(Path.Combine(electron, "resources"));
        WriteMinimalAsar(Path.Combine(electron, "resources", "app.asar"));

        string wolf = Path.Combine(root, "wolf");
        Directory.CreateDirectory(Path.Combine(wolf, "Data", "BasicData"));
        Directory.CreateDirectory(Path.Combine(wolf, "Data", "Picture"));
        File.WriteAllText(Path.Combine(wolf, "Data", "BasicData", "Game.dat"), "game");
        File.WriteAllText(Path.Combine(wolf, "Data", "Picture", "hero.png"), "png");
        File.WriteAllBytes(Path.Combine(wolf, "Game.exe"), new byte[] { 0 });

        object form = Activator.CreateInstance(formType);
        try
        {
            object tyranoResult = InvokeExtraction(formType, form, "RunTyranoExtraction", tyrano, Path.Combine(tyrano, "extracted", "tyrano"));
            string javaOutput = Path.Combine(java, "extracted", "java");
            object javaResult = InvokeExtraction(formType, form, "RunJavaExtraction", java, javaOutput);
            object javaCachedResult = InvokeExtractionWithMode(formType, form, java, javaOutput, "images-svg");
            string javaImagesOutput = Path.Combine(java, "extracted", "java-images");
            object javaImagesResult = InvokeExtractionWithMode(formType, form, java, javaImagesOutput, "images");
            string javaAllOutput = Path.Combine(java, "extracted", "java-all");
            object javaAllResult = InvokeExtractionWithMode(formType, form, java, javaAllOutput, "all");
            object flashResult = InvokeExtraction(formType, form, "RunFlashExtraction", flash, Path.Combine(flash, "extracted", "flash"));
            object electronResult = InvokeExtraction(formType, form, "RunElectronExtraction", electron, Path.Combine(electron, "extracted", "electron"));
            object wolfResult = InvokeExtraction(formType, form, "RunWolfExtraction", wolf, Path.Combine(wolf, "extracted", "wolf"));
            return GetInt(tyranoResult, "Extracted") == 1
                && File.Exists(Path.Combine(tyrano, "extracted", "tyrano", "data", "scenario", "first.ks"))
                && GetInt(javaResult, "Extracted") == 6
                && GetInt(javaResult, "Errors") == 0
                && File.Exists(Path.Combine(java, "extracted", "java", "loose", "res", "images", "hero.png"))
                && File.Exists(Path.Combine(java, "extracted", "java", "loose", "res", "images", "vector.svg"))
                && IsPng(Path.Combine(java, "extracted", "java", "loose", "res", "images", "vector.png"))
                && !File.Exists(Path.Combine(java, "extracted", "java", "loose", "res", "images", "metadata.xml"))
                && File.Exists(Path.Combine(java, "extracted", "java", "archives", "game", "assets", "picture.png"))
                && File.Exists(Path.Combine(java, "extracted", "java", "archives", "game", "assets", "vector.svg"))
                && IsPng(Path.Combine(java, "extracted", "java", "archives", "game", "assets", "vector.png"))
                && File.Exists(Path.Combine(javaOutput, "svg-preview-cache.tsv"))
                && GetInt(javaCachedResult, "Skipped") >= 2
                && Directory.GetFiles(javaOutput, "vector*.svg", SearchOption.AllDirectories).Length == 2
                && Directory.GetFiles(javaOutput, "vector*.png", SearchOption.AllDirectories).Length == 2
                && GetInt(javaImagesResult, "Errors") == 0
                && !File.Exists(Path.Combine(javaImagesOutput, "loose", "res", "images", "vector.png"))
                && GetInt(javaAllResult, "Errors") == 0
                && File.Exists(Path.Combine(javaAllOutput, "loose", "res", "images", "metadata.xml"))
                && File.Exists(Path.Combine(javaAllOutput, "archives", "game", "assets", "metadata.xml"))
                && !File.Exists(Path.Combine(java, "extracted", "java", "archives", "game", "assets", "metadata.xml"))
                && !File.Exists(Path.Combine(java, "extracted", "outside.txt"))
                && GetInt(flashResult, "Extracted") == 3
                && GetInt(flashResult, "Errors") == 0
                && File.Exists(Path.Combine(flash, "extracted", "flash", "originals", "movie.swf"))
                && File.Exists(Path.Combine(flash, "extracted", "flash", "embedded", "movie", "image-7.jpg"))
                && IsFlv(Path.Combine(flash, "extracted", "flash", "embedded", "movie", "video-9.flv"))
                && GetInt(electronResult, "Extracted") == 1
                && GetInt(electronResult, "Errors") == 0
                && File.ReadAllText(Path.Combine(electron, "extracted", "electron", "archives", "app", "scripts", "app.js")) == "console.log('asar');"
                && GetInt(wolfResult, "Extracted") == 2
                && GetInt(wolfResult, "Errors") == 0
                && File.Exists(Path.Combine(wolf, "extracted", "wolf", "loose", "Data", "Picture", "hero.png"));
        }
        finally
        {
            MethodInfo dispose = formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            dispose.Invoke(form, null);
        }
    }

    private static bool VerifyStartupFolderArgument(Type formType, string temp)
    {
        string parent = Path.Combine(temp, "startup-folder-parent");
        string root = Path.Combine(parent, "(unknown) SPITE");
        Directory.CreateDirectory(root);
        WriteMinimalRags(Path.Combine(parent, "parent-marker.rag"));
        object form = Activator.CreateInstance(formType, new object[] { root });
        try
        {
            MethodInfo apply = formType.GetMethod("TryApplyStartupGamePath", BindingFlags.NonPublic | BindingFlags.Instance);
            apply.Invoke(form, null);
            object pathBox = GetField(formType, form, "pathBox");
            return string.Equals(GetString(pathBox, "Text"), root, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            MethodInfo dispose = formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            dispose.Invoke(form, null);
        }
    }

    private static bool VerifyStartupFileArgument(Type formType, string temp)
    {
        string file = Path.Combine(temp, "startup-file.rag");
        WriteMinimalRags(file);
        object form = Activator.CreateInstance(formType, new object[] { file });
        try
        {
            MethodInfo apply = formType.GetMethod("TryApplyStartupGamePath", BindingFlags.NonPublic | BindingFlags.Instance);
            apply.Invoke(form, null);
            object pathBox = GetField(formType, form, "pathBox");
            return string.Equals(GetString(pathBox, "Text"), file, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            MethodInfo dispose = formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            dispose.Invoke(form, null);
        }
    }

    private static bool VerifyDroppedFolderArgument(Type formType, string temp)
    {
        string parent = Path.Combine(temp, "drop-folder-parent");
        string root = Path.Combine(parent, "(unknown) SPITE");
        Directory.CreateDirectory(Path.Combine(parent, "www"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        WriteMinimalSpak(Path.Combine(root, "data", "media.dat"));
        object form = Activator.CreateInstance(formType);
        try
        {
            DataObject data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { root });
            DragEventArgs args = new DragEventArgs(data, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.Copy);
            MethodInfo drop = formType.GetMethod("OnDragDrop", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            drop.Invoke(form, new object[] { form, args });
            object pathBox = GetField(formType, form, "pathBox");
            object detectedEngineLabel = GetField(formType, form, "detectedEngineLabel");
            return string.Equals(GetString(pathBox, "Text"), root, StringComparison.OrdinalIgnoreCase)
                && GetString(detectedEngineLabel, "Text").EndsWith("SPAK DAT experimental", StringComparison.Ordinal);
        }
        finally
        {
            MethodInfo dispose = formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            dispose.Invoke(form, null);
        }
    }

    private static bool VerifyCollectorExtraction(Assembly assembly, string temp)
    {
        Type collectors = assembly.GetType("RpgmvpConverterWinForms.AssetCollectors", true);
        MethodInfo getHtmlFiles = collectors.GetMethod("GetHtmlFiles", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getQspFiles = collectors.GetMethod("GetQspFiles", BindingFlags.Public | BindingFlags.Static);
        MethodInfo copyFiles = collectors.GetMethod("CopyFiles", BindingFlags.Public | BindingFlags.Static);
        MethodInfo extractRags = collectors.GetMethod("ExtractRags", BindingFlags.Public | BindingFlags.Static);
        MethodInfo writeDiagnostics = collectors.GetMethod("WriteDiagnostics", BindingFlags.Public | BindingFlags.Static);
        Type signatureExtractor = assembly.GetType("RpgmvpConverterWinForms.SignatureAssetExtractor", true);
        MethodInfo extractSignatures = signatureExtractor.GetMethod("Extract", BindingFlags.Public | BindingFlags.Static);
        Type spakExtractor = assembly.GetType("RpgmvpConverterWinForms.SpakDatExtractor", true);
        object spakExtractorInstance = Activator.CreateInstance(spakExtractor);
        MethodInfo extractSpak = spakExtractor.GetMethod("Extract", BindingFlags.Public | BindingFlags.Instance);

        string root = Path.Combine(temp, "collector-extraction");
        string html = Path.Combine(root, "html");
        Directory.CreateDirectory(Path.Combine(html, "assets"));
        File.WriteAllText(Path.Combine(html, "index.html"), "<html></html>");
        File.WriteAllText(Path.Combine(html, "assets", "hero.jpg"), "jpg");
        string htmlOutput = Path.Combine(html, "extracted", "html");
        object htmlFiles = getHtmlFiles.Invoke(null, new object[] { html, htmlOutput });
        object htmlResult = copyFiles.Invoke(null, new object[] { html, htmlFiles, htmlOutput, "" });

        string qsp = Path.Combine(root, "qsp");
        Directory.CreateDirectory(Path.Combine(qsp, "qsp"));
        Directory.CreateDirectory(Path.Combine(qsp, "images"));
        File.WriteAllText(Path.Combine(qsp, "game.qsp"), "qsp");
        File.WriteAllText(Path.Combine(qsp, "images", "scene.jpg"), "jpg");
        File.WriteAllText(Path.Combine(qsp, "qsp", "runtime.jpg"), "skip");
        string qspOutput = Path.Combine(qsp, "extracted", "qsp");
        object qspFiles = getQspFiles.Invoke(null, new object[] { qsp, qspOutput });
        object qspResult = copyFiles.Invoke(null, new object[] { qsp, qspFiles, qspOutput, "" });

        string rags = Path.Combine(root, "sample.rag");
        WriteMinimalRags(rags);
        string ragsOutput = Path.Combine(root, "rags-output");
        object ragsResult = extractRags.Invoke(null, new object[] { rags, ragsOutput });

        string gameMakerOutput = Path.Combine(root, "gamemaker-gallery");
        Directory.CreateDirectory(gameMakerOutput);
        File.WriteAllText(Path.Combine(gameMakerOutput, "texture-page-0001.png"), "png");
        Type gallery = assembly.GetType("RpgmvpConverterWinForms.ResultsGalleryForm", true);
        MethodInfo getMatchingFiles = gallery.GetMethod("GetMatchingFiles", BindingFlags.NonPublic | BindingFlags.Static);
        IEnumerable galleryImages = (IEnumerable)getMatchingFiles.Invoke(null, new object[] { gameMakerOutput, 1, "texture-page" });
        bool galleryFilter = galleryImages.Cast<object>().Count() == 1;
        for (int i = 0; i < 305; i++)
            File.WriteAllText(Path.Combine(gameMakerOutput, "gallery-" + i.ToString("D3") + ".png"), "png");
        IEnumerable galleryAll = (IEnumerable)getMatchingFiles.Invoke(null, new object[] { gameMakerOutput, 0, "" });
        bool galleryNoHardCap = gallery.GetField("MaxVisibleItems", BindingFlags.NonPublic | BindingFlags.Static) == null
            && galleryAll.Cast<object>().Count() == 306;

        string unknown = Path.Combine(root, "unknown");
        Directory.CreateDirectory(unknown);
        WriteMinimalDataWin(Path.Combine(unknown, "archive.bin"));
        string diagnosticsOutput = Path.Combine(root, "diagnostics");
        string report = (string)writeDiagnostics.Invoke(null, new object[] { unknown, diagnosticsOutput });
        string signaturesOutput = Path.Combine(root, "signature-output");
        object signatureResult = extractSignatures.Invoke(null, new object[] { unknown, signaturesOutput });

        string spak = Path.Combine(root, "spak");
        Directory.CreateDirectory(Path.Combine(spak, "data"));
        WriteMinimalSpak(Path.Combine(spak, "data", "media.dat"));
        File.WriteAllBytes(Path.Combine(spak, "data", "protected.dat"), new byte[] { 1, 2, 3, 4 });
        string spakOutput = Path.Combine(root, "spak-output");
        object spakResult = extractSpak.Invoke(spakExtractorInstance, new object[] { spak, spakOutput });

        return GetInt(htmlResult, "Extracted") == 2
            && File.Exists(Path.Combine(htmlOutput, "index.html"))
            && File.Exists(Path.Combine(htmlOutput, "assets", "hero.jpg"))
            && GetInt(qspResult, "Extracted") == 2
            && File.Exists(Path.Combine(qspOutput, "game.qsp"))
            && File.Exists(Path.Combine(qspOutput, "images", "scene.jpg"))
            && !File.Exists(Path.Combine(qspOutput, "qsp", "runtime.jpg"))
            && GetInt(ragsResult, "Extracted") == 2
            && File.Exists(Path.Combine(ragsOutput, "originals", "sample.rag"))
            && File.Exists(Path.Combine(ragsOutput, "embedded", "sample", "asset-0001.jpg"))
            && galleryFilter
            && galleryNoHardCap
            && File.Exists(report)
            && File.ReadAllText(report).Contains("archive.bin | 46-4F-52-4D")
            && GetInt(signatureResult, "Extracted") == 1
            && IsPng(Path.Combine(signaturesOutput, "embedded", "archive", "asset-0001.png"))
            && GetInt(spakResult, "Extracted") == 2
            && IsPng(Path.Combine(spakOutput, "archives", "media", "feedfacecafebeef.png"))
            && File.Exists(Path.Combine(spakOutput, "external", "data", "protected.dat"))
            && File.ReadAllText(Path.Combine(spakOutput, "SPAK-DAT-manifest.txt")).Contains("protected-or-unknown");
    }

    private static bool VerifyLegacyRpgMakerExtraction(Assembly assembly, Type formType, string temp)
    {
        string source = Path.Combine(temp, "rgss-source");
        Directory.CreateDirectory(source);
        WriteMinimalRgssVersion1(Path.Combine(source, "Game.rgssad"), "Graphics\\xp.txt", "xp");
        WriteMinimalRgssVersion1(Path.Combine(source, "Game.rgss2a"), "Graphics\\vx.txt", "vx");
        WriteMinimalRgssVersion3(Path.Combine(source, "Game.rgss3a"), "Graphics\\vxace.txt", "vxace");
        string output = Path.Combine(temp, "rgss-output");
        object form = Activator.CreateInstance(formType);
        try
        {
            object result = InvokeExtraction(formType, form, "RunLegacyRpgMakerExtraction", source, output);
            return GetInt(result, "Errors") == 0
                && Directory.GetFiles(output, "xp.txt", SearchOption.AllDirectories).Any()
                && Directory.GetFiles(output, "vx.txt", SearchOption.AllDirectories).Any()
                && Directory.GetFiles(output, "vxace.txt", SearchOption.AllDirectories).Any();
        }
        finally
        {
            formType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance).Invoke(form, null);
        }
    }

    private static bool VerifyUpstreamRgssFixtures(Assembly assembly, string fixtures, string temp)
    {
        if (!Directory.Exists(fixtures)) return false;
        Type extractor = assembly.GetType("RpgmvpConverterWinForms.LegacyRpgMakerExtractor", true);
        MethodInfo extractArchive = extractor.GetMethod("ExtractArchive", BindingFlags.Public | BindingFlags.Static);
        foreach (string name in new[] { "Game.rgssad", "Game.rgss2a", "Game.rgss3a" })
        {
            string archive = Path.Combine(fixtures, name);
            if (!File.Exists(archive)) return false;
            string output = Path.Combine(temp, "upstream-" + Path.GetExtension(name).TrimStart('.'));
            object result = extractArchive.Invoke(null, new object[] { archive, output });
            if (GetInt(result, "Extracted") == 0 || GetInt(result, "Skipped") != 0)
                return false;
        }
        return true;
    }

    private static object InvokeExtraction(Type formType, object form, string method, string root, string output)
    {
        return formType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { root, output });
    }

    private static object InvokeExtractionWithMode(Type formType, object form, string root, string output, string mode)
    {
        return formType.GetMethod("RunJavaExtractionWithMode", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(form, new object[] { root, output, mode });
    }

    private static int GetInt(object instance, string property)
    {
        return (int)instance.GetType().GetProperty(property).GetValue(instance, null);
    }

    private static void WriteMinimalSwf(string path)
    {
        byte[] jpeg = { 0xff, 0xd8, 0xff, 0xd9 };
        List<byte> body = new List<byte>(new byte[]
        {
            0x08, 0x00,
            0x00, 0x00,
            0x01, 0x00
        });
        WriteSwfTag(body, 21, new byte[] { 0x07, 0x00, jpeg[0], jpeg[1], jpeg[2], jpeg[3] });
        WriteSwfTag(body, 60, new byte[] { 0x09, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x02 });
        WriteSwfTag(body, 61, new byte[] { 0x09, 0x00, 0x00, 0x00, 0x11, 0x22, 0x33 });
        WriteSwfTag(body, 0, new byte[0]);

        byte[] file = new byte[8 + body.Count];
        file[0] = (byte)'F';
        file[1] = (byte)'W';
        file[2] = (byte)'S';
        file[3] = 9;
        byte[] length = BitConverter.GetBytes(file.Length);
        Array.Copy(length, 0, file, 4, length.Length);
        Array.Copy(body.ToArray(), 0, file, 8, body.Count);
        File.WriteAllBytes(path, file);
    }

    private static void WriteSwfTag(List<byte> body, int code, byte[] data)
    {
        int header = (code << 6) | data.Length;
        body.Add((byte)(header & 0xff));
        body.Add((byte)((header >> 8) & 0xff));
        body.AddRange(data);
    }

    private static void WriteMinimalAsar(string path)
    {
        byte[] content = System.Text.Encoding.UTF8.GetBytes("console.log('asar');");
        string json = "{\"files\":{\"scripts\":{\"files\":{\"app.js\":{\"size\":" + content.Length + ",\"offset\":\"0\"}}}}}";
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        int headerSize = ((8 + jsonBytes.Length + 3) / 4) * 4;
        byte[] header = new byte[headerSize];
        WriteUInt32(header, 0, (uint)(4 + jsonBytes.Length));
        WriteUInt32(header, 4, (uint)jsonBytes.Length);
        Array.Copy(jsonBytes, 0, header, 8, jsonBytes.Length);

        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write((uint)4);
            writer.Write((uint)header.Length);
            writer.Write(header);
            writer.Write(content);
        }
    }

    private static void WriteMinimalSpak(string path)
    {
        byte[] png =
        {
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44,
            0xae, 0x42, 0x60, 0x82
        };
        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("SPAK"));
            writer.Write((uint)1);
            writer.Write((uint)1);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("feedfacecafebeef"));
            writer.Write((uint)0);
            writer.Write((uint)png.Length);
            writer.Write((uint)0);
            writer.Write(png);
        }
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value & 0xff);
        bytes[offset + 1] = (byte)((value >> 8) & 0xff);
        bytes[offset + 2] = (byte)((value >> 16) & 0xff);
        bytes[offset + 3] = (byte)((value >> 24) & 0xff);
    }

    private static void WriteMinimalRags(string path)
    {
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 0xff, 0xd8, 0xff, 0x00, 0xff, 0xd9, 4, 5, 6 });
    }

    private static void WriteMinimalDataWin(string path)
    {
        byte[] png =
        {
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
            0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e,
            0x44, 0x00, 0x00, 0x00, 0x00
        };
        byte[] prefix = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 0 };
        File.WriteAllBytes(path, prefix.Concat(png).Concat(new byte[] { 0, 1, 2 }).ToArray());
    }

    private static void WriteMinimalRgssVersion1(string path, string name, string content)
    {
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(content);
        uint key = 0xDEADCAFE;
        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RGSSAD"));
            writer.Write((byte)0);
            writer.Write((byte)1);
            writer.Write(nameBytes.Length ^ unchecked((int)key));
            key = unchecked(key * 7 + 3);
            for (int i = 0; i < nameBytes.Length; i++)
            {
                writer.Write((byte)(nameBytes[i] ^ (byte)(key & 0xff)));
                key = unchecked(key * 7 + 3);
            }
            writer.Write(data.Length ^ unchecked((int)key));
            key = unchecked(key * 7 + 3);
            writer.Write(EncryptRgssData(data, key));
        }
    }

    private static void WriteMinimalRgssVersion3(string path, string name, string content)
    {
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(content);
        uint seed = 0x12345678;
        uint key = unchecked(seed * 9 + 3);
        uint fileKey = 0xCAFEBABE;
        uint offset = (uint)(8 + 4 + 16 + nameBytes.Length + 16);
        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RGSSAD"));
            writer.Write((byte)0);
            writer.Write((byte)3);
            writer.Write(seed);
            writer.Write(offset ^ key);
            writer.Write((uint)data.Length ^ key);
            writer.Write(fileKey ^ key);
            writer.Write((uint)nameBytes.Length ^ key);
            byte[] keyBytes = BitConverter.GetBytes(key);
            for (int i = 0; i < nameBytes.Length; i++)
                writer.Write((byte)(nameBytes[i] ^ keyBytes[i % 4]));
            writer.Write(key);
            writer.Write(key);
            writer.Write(key);
            writer.Write(key);
            writer.Write(EncryptRgssData(data, fileKey));
        }
    }

    private static byte[] EncryptRgssData(byte[] data, uint key)
    {
        byte[] result = data.ToArray();
        byte[] keyBytes = BitConverter.GetBytes(key);
        int keyIndex = 0;
        for (int i = 0; i < result.Length; i++)
        {
            if (keyIndex == 4)
            {
                keyIndex = 0;
                key = unchecked(key * 7 + 3);
                keyBytes = BitConverter.GetBytes(key);
            }
            result[i] ^= keyBytes[keyIndex++];
        }
        return result;
    }

    private static bool IsPng(string path)
    {
        if (!File.Exists(path)) return false;
        byte[] signature = File.ReadAllBytes(path).Take(8).ToArray();
        return signature.SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
    }

    private static bool IsFlv(string path)
    {
        return File.Exists(path)
            && File.ReadAllBytes(path).Take(3).SequenceEqual(System.Text.Encoding.ASCII.GetBytes("FLV"));
    }

    private static object GetField(Type type, object instance, string name)
    {
        return type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
    }

    private static bool GetBool(object instance, string property)
    {
        return (bool)instance.GetType().GetProperty(property).GetValue(instance, null);
    }

    private static string GetString(object instance, string property)
    {
        return (string)instance.GetType().GetProperty(property).GetValue(instance, null);
    }

    private static int GetSizeHeight(object instance, string property)
    {
        object size = instance.GetType().GetProperty(property).GetValue(instance, null);
        return (int)size.GetType().GetProperty("Height").GetValue(size, null);
    }

    private static bool GetLocalVisible(object control)
    {
        Type current = control.GetType();
        while (current != null)
        {
            MethodInfo getState = current.GetMethod("GetState", BindingFlags.NonPublic | BindingFlags.Instance);
            if (getState != null)
                return (bool)getState.Invoke(control, new object[] { 2 });
            current = current.BaseType;
        }
        throw new MissingMethodException("Control.GetState");
    }

    private static bool HasReadableDisabledContrast(object button)
    {
        Type type = button.GetType();
        object foreground = type.GetProperty("DisabledForeColor").GetValue(button, null);
        object background = type.GetProperty("DisabledBackColor").GetValue(button, null);
        double foregroundLuminance = GetLuminance(foreground);
        double backgroundLuminance = GetLuminance(background);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05) >= 4.5;
    }

    private static double GetLuminance(object color)
    {
        Type type = color.GetType();
        return 0.2126 * GetLinearColor(type, color, "R")
            + 0.7152 * GetLinearColor(type, color, "G")
            + 0.0722 * GetLinearColor(type, color, "B");
    }

    private static double GetLinearColor(Type type, object color, string property)
    {
        double value = (byte)type.GetProperty(property).GetValue(color, null) / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
