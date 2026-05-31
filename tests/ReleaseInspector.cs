using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

internal static class ReleaseInspector
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: ReleaseInspector.exe <GameAssetTool.exe>");
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
            bool hasPortableRuntime = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.runtime.runtime-win-x64.zip");
            bool hasWolfCli = assembly.GetManifestResourceNames().Contains("RpgmvpConverterWinForms.tools.UberWolfCli.exe");

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
            string directFlashEngine = DetectExistingEngine(detectEngine, Path.Combine(temp, "flash", "game.swf"));
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
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "html")) == "Html"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "qsp")) == "Qsp"
                && DetectExistingEngine(detectEngineFast, ragsPath) == "Rags"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "flash", "game.swf")) == "Flash"
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
            bool startupFolderArgument = VerifyStartupFolderArgument(formType, temp);
            bool startupFileArgument = VerifyStartupFileArgument(formType, temp);

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
                psi.Arguments = "-c \"import unrpa, UnityPy, pyuepak; print('portable-runtime-ok')\"";
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
            MethodInfo cleanupTools = toolRuntimeType.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static);
            string wolfCliPath = null;
            try
            {
                wolfCliPath = (string)ensureWolfCli.Invoke(null, null);
                wolfCliExtracted = File.Exists(wolfCliPath);
            }
            finally
            {
                cleanupTools.Invoke(null, null);
                wolfCliRemoved = string.IsNullOrWhiteSpace(wolfCliPath) || !File.Exists(wolfCliPath);
            }

            Console.WriteLine("AssemblyVersion=" + assembly.GetName().Version);
            Console.WriteLine("EmbeddedUnityScript=" + hasUnityScript);
            Console.WriteLine("UnityFilteringFix=" + unityFilteringFix);
            Console.WriteLine("EmbeddedGodotScript=" + hasGodotScript);
            Console.WriteLine("EmbeddedXp3Script=" + hasXp3Script);
            Console.WriteLine("EmbeddedUnrealScript=" + hasUnrealScript);
            Console.WriteLine("EmbeddedPortableRuntime=" + hasPortableRuntime);
            Console.WriteLine("EmbeddedWolfCli=" + hasWolfCli);
            Console.WriteLine("PortableRuntimeImports=" + portableImports);
            Console.WriteLine("PortableRuntimeRemoved=" + runtimeRemoved);
            Console.WriteLine("WolfCliExtracted=" + wolfCliExtracted);
            Console.WriteLine("WolfCliRemoved=" + wolfCliRemoved);
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
            Console.WriteLine("DetectHtml=" + htmlEngine);
            Console.WriteLine("DetectQsp=" + qspEngine);
            Console.WriteLine("DetectRags=" + ragsEngine);
            Console.WriteLine("DetectDirectFlash=" + directFlashEngine);
            Console.WriteLine("DetectGenericGameFolder=" + genericGameFolderEngine);
            Console.WriteLine("RenpyUnlockerScope=" + renpyUnlockerScope);
            Console.WriteLine("FastDetection=" + fastDetection);
            Console.WriteLine("FastRootLookup=" + fastRootLookup);
            Console.WriteLine("ContextualGui=" + contextualGui);
            Console.WriteLine("NwjsExtraction=" + nwjsExtraction);
            Console.WriteLine("LocalEngineExtraction=" + localEngineExtraction);
            Console.WriteLine("CollectorExtraction=" + collectorExtraction);
            Console.WriteLine("StartupFolderArgument=" + startupFolderArgument);
            Console.WriteLine("StartupFileArgument=" + startupFileArgument);

            return hasUnityScript
                && unityFilteringFix
                && hasGodotScript
                && hasXp3Script
                && hasUnrealScript
                && hasPortableRuntime
                && hasWolfCli
                && portableImports
                && runtimeRemoved
                && wolfCliExtracted
                && wolfCliRemoved
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
                && htmlEngine == "Html"
                && qspEngine == "Qsp"
                && ragsEngine == "Rags"
                && directFlashEngine == "Flash"
                && genericGameFolderEngine == "Unknown"
                && renpyUnlockerScope
                && fastDetection
                && fastRootLookup
                && contextualGui
                && nwjsExtraction
                && localEngineExtraction
                && collectorExtraction
                && startupFolderArgument
                && startupFileArgument
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
            bool java = GetBool(startButton, "Enabled");

            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Flash") });
            bool flash = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("JPEG", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Html") });
            bool html = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("HTML", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Qsp") });
            bool qsp = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("QSP", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Rags") });
            bool rags = GetBool(startButton, "Enabled")
                && GetString(extractionHint, "Text").IndexOf("RAGS", StringComparison.OrdinalIgnoreCase) >= 0;
            updateContext.Invoke(form, new[] { Enum.Parse(engineType, "Unknown") });
            bool unknown = !GetBool(startButton, "Enabled")
                && GetString(startButton, "Text") == "Export Diagnostics";
            bool readableDisabledButtons = HasReadableDisabledContrast(startButton)
                && HasReadableDisabledContrast(collectLooseButton)
                && HasReadableDisabledContrast(unlockerButton)
                && HasReadableDisabledContrast(removeUnlockerButton)
                && HasReadableDisabledContrast(pauseButton)
                && HasReadableDisabledContrast(cancelButton)
                && HasReadableDisabledContrast(openOutputButton);
            bool tooltips = !string.IsNullOrWhiteSpace((string)toolTip.GetType().GetMethod("GetToolTip").Invoke(toolTip, new[] { startButton }))
                && !string.IsNullOrWhiteSpace((string)toolTip.GetType().GetMethod("GetToolTip").Invoke(toolTip, new[] { openOutputButton }));
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

            return initial && unity && renpy && rpgm && unreal && nwjs && wolf && tyrano && java && flash
                && html && qsp && rags && unknown && russian
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
            return extracted == 3
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
        using (FileStream stream = File.Create(Path.Combine(java, "game.jar")))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("assets/picture.png").Open()))
                writer.Write("png");
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open()))
                writer.Write("inside-output");
        }

        string flash = Path.Combine(root, "flash");
        Directory.CreateDirectory(flash);
        WriteMinimalSwf(Path.Combine(flash, "movie.swf"));

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
            object javaResult = InvokeExtraction(formType, form, "RunJavaExtraction", java, Path.Combine(java, "extracted", "java"));
            object flashResult = InvokeExtraction(formType, form, "RunFlashExtraction", flash, Path.Combine(flash, "extracted", "flash"));
            object wolfResult = InvokeExtraction(formType, form, "RunWolfExtraction", wolf, Path.Combine(wolf, "extracted", "wolf"));
            return GetInt(tyranoResult, "Extracted") == 1
                && File.Exists(Path.Combine(tyrano, "extracted", "tyrano", "data", "scenario", "first.ks"))
                && GetInt(javaResult, "Extracted") == 3
                && GetInt(javaResult, "Errors") == 0
                && File.Exists(Path.Combine(java, "extracted", "java", "loose", "res", "images", "hero.png"))
                && File.Exists(Path.Combine(java, "extracted", "java", "archives", "game", "assets", "picture.png"))
                && !File.Exists(Path.Combine(java, "extracted", "outside.txt"))
                && GetInt(flashResult, "Extracted") == 2
                && GetInt(flashResult, "Errors") == 0
                && File.Exists(Path.Combine(flash, "extracted", "flash", "originals", "movie.swf"))
                && File.Exists(Path.Combine(flash, "extracted", "flash", "embedded", "movie", "image-7.jpg"))
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
        string root = Path.Combine(temp, "startup-folder");
        Directory.CreateDirectory(Path.Combine(root, "data", "scenario"));
        Directory.CreateDirectory(Path.Combine(root, "data", "system"));
        File.WriteAllText(Path.Combine(root, "data", "scenario", "first.ks"), "*start");
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

    private static bool VerifyCollectorExtraction(Assembly assembly, string temp)
    {
        Type collectors = assembly.GetType("RpgmvpConverterWinForms.AssetCollectors", true);
        MethodInfo getHtmlFiles = collectors.GetMethod("GetHtmlFiles", BindingFlags.Public | BindingFlags.Static);
        MethodInfo getQspFiles = collectors.GetMethod("GetQspFiles", BindingFlags.Public | BindingFlags.Static);
        MethodInfo copyFiles = collectors.GetMethod("CopyFiles", BindingFlags.Public | BindingFlags.Static);
        MethodInfo extractRags = collectors.GetMethod("ExtractRags", BindingFlags.Public | BindingFlags.Static);
        MethodInfo writeDiagnostics = collectors.GetMethod("WriteDiagnostics", BindingFlags.Public | BindingFlags.Static);

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

        string unknown = Path.Combine(root, "unknown");
        Directory.CreateDirectory(unknown);
        File.WriteAllBytes(Path.Combine(unknown, "archive.bin"), new byte[] { 1, 2, 3 });
        string diagnosticsOutput = Path.Combine(root, "diagnostics");
        string report = (string)writeDiagnostics.Invoke(null, new object[] { unknown, diagnosticsOutput });

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
            && File.Exists(report)
            && File.ReadAllText(report).Contains("archive.bin | 01-02-03");
    }

    private static object InvokeExtraction(Type formType, object form, string method, string root, string output)
    {
        return formType.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { root, output });
    }

    private static int GetInt(object instance, string property)
    {
        return (int)instance.GetType().GetProperty(property).GetValue(instance, null);
    }

    private static void WriteMinimalSwf(string path)
    {
        byte[] jpeg = { 0xff, 0xd8, 0xff, 0xd9 };
        byte[] body =
        {
            0x08, 0x00,
            0x00, 0x00,
            0x01, 0x00,
            0x46, 0x05,
            0x07, 0x00,
            jpeg[0], jpeg[1], jpeg[2], jpeg[3],
            0x00, 0x00
        };
        byte[] file = new byte[8 + body.Length];
        file[0] = (byte)'F';
        file[1] = (byte)'W';
        file[2] = (byte)'S';
        file[3] = 9;
        byte[] length = BitConverter.GetBytes(file.Length);
        Array.Copy(length, 0, file, 4, length.Length);
        Array.Copy(body, 0, file, 8, body.Length);
        File.WriteAllBytes(path, file);
    }

    private static void WriteMinimalRags(string path)
    {
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 0xff, 0xd8, 0xff, 0x00, 0xff, 0xd9, 4, 5, 6 });
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
