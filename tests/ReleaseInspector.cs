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

            Type unlockerType = assembly.GetType("RpgmvpConverterWinForms.UnlockerResources", true);
            MethodInfo extractUnlocker = unlockerType.GetMethod("ExtractUnlocker", BindingFlags.Public | BindingFlags.Static);
            string unlockerOutput = Path.Combine(temp, "unlocker");
            extractUnlocker.Invoke(null, new object[] { "soft", unlockerOutput });

            string[] files = Directory.GetFiles(unlockerOutput, "*.rpy", SearchOption.AllDirectories);
            bool nestedGameFolder = Directory.Exists(Path.Combine(unlockerOutput, "game"));

            Type formType = assembly.GetType("RpgmvpConverterWinForms.RpgmvpConverterForm", true);
            MethodInfo detectEngine = formType.GetMethod("DetectEngine", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo detectEngineFast = formType.GetMethod("DetectEngineFast", BindingFlags.NonPublic | BindingFlags.Static);
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
            string genericGameFolderEngine = DetectEngine(detectEngine, Path.Combine(temp, "generic-game-folder"), delegate(string path)
            {
                Directory.CreateDirectory(Path.Combine(path, "game"));
            });
            bool renpyUnlockerScope = (bool)canInstallUnlocker.Invoke(null, new object[] { Path.Combine(temp, "renpy") })
                && !(bool)canInstallUnlocker.Invoke(null, new object[] { Path.Combine(temp, "nwjs") })
                && !(bool)canInstallUnlocker.Invoke(null, new object[] { Path.Combine(temp, "generic-game-folder") });
            bool fastDetection = DetectExistingEngine(detectEngineFast, Path.Combine(temp, "unity")) == "Unity"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "renpy")) == "Renpy"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "rpgm")) == "RpgMaker"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "godot")) == "Godot"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "kirikiri")) == "Kirikiri"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "unreal")) == "Unreal"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "nwjs")) == "Nwjs"
                && DetectExistingEngine(detectEngineFast, Path.Combine(temp, "generic-game-folder")) == "Unknown";

            string rootLookup = Path.Combine(temp, "root-lookup");
            string nestedLookup = Path.Combine(rootLookup, "one", "two");
            Directory.CreateDirectory(nestedLookup);
            File.WriteAllBytes(Path.Combine(rootLookup, "data.xp3"), new byte[] { 0 });
            string foundRoot = (string)tryFindGameRoot.Invoke(null, new object[] { nestedLookup });
            bool fastRootLookup = string.Equals(foundRoot, rootLookup, StringComparison.OrdinalIgnoreCase);
            bool contextualGui = VerifyContextualGui(formType);
            bool nwjsExtraction = VerifyNwjsExtraction(formType, temp);

            Type runtimeType = assembly.GetType("RpgmvpConverterWinForms.PortableRuntime", true);
            MethodInfo ensureRuntime = runtimeType.GetMethod("EnsureExtracted", BindingFlags.Public | BindingFlags.Static);
            MethodInfo createPythonProcessInfo = runtimeType.GetMethod("CreatePythonProcessInfo", BindingFlags.Public | BindingFlags.Static);
            MethodInfo cleanupRuntime = runtimeType.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static);
            string pythonPath = null;
            string runtimeDirectory = null;
            bool portableImports = false;
            bool runtimeRemoved = false;
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

            Console.WriteLine("AssemblyVersion=" + assembly.GetName().Version);
            Console.WriteLine("EmbeddedUnityScript=" + hasUnityScript);
            Console.WriteLine("UnityFilteringFix=" + unityFilteringFix);
            Console.WriteLine("EmbeddedGodotScript=" + hasGodotScript);
            Console.WriteLine("EmbeddedXp3Script=" + hasXp3Script);
            Console.WriteLine("EmbeddedUnrealScript=" + hasUnrealScript);
            Console.WriteLine("EmbeddedPortableRuntime=" + hasPortableRuntime);
            Console.WriteLine("PortableRuntimeImports=" + portableImports);
            Console.WriteLine("PortableRuntimeRemoved=" + runtimeRemoved);
            Console.WriteLine("UnlockerFiles=" + files.Length);
            Console.WriteLine("UnexpectedNestedGameFolder=" + nestedGameFolder);
            Console.WriteLine("DetectUnity=" + unityEngine);
            Console.WriteLine("DetectRenpy=" + renpyEngine);
            Console.WriteLine("DetectRpgMaker=" + rpgmEngine);
            Console.WriteLine("DetectGodot=" + godotEngine);
            Console.WriteLine("DetectKirikiri=" + kirikiriEngine);
            Console.WriteLine("DetectUnreal=" + unrealEngine);
            Console.WriteLine("DetectNwjs=" + nwjsEngine);
            Console.WriteLine("DetectGenericGameFolder=" + genericGameFolderEngine);
            Console.WriteLine("RenpyUnlockerScope=" + renpyUnlockerScope);
            Console.WriteLine("FastDetection=" + fastDetection);
            Console.WriteLine("FastRootLookup=" + fastRootLookup);
            Console.WriteLine("ContextualGui=" + contextualGui);
            Console.WriteLine("NwjsExtraction=" + nwjsExtraction);

            return hasUnityScript
                && unityFilteringFix
                && hasGodotScript
                && hasXp3Script
                && hasUnrealScript
                && hasPortableRuntime
                && portableImports
                && runtimeRemoved
                && files.Length > 0
                && !nestedGameFolder
                && unityEngine == "Unity"
                && renpyEngine == "Renpy"
                && rpgmEngine == "RpgMaker"
                && godotEngine == "Godot"
                && kirikiriEngine == "Kirikiri"
                && unrealEngine == "Unreal"
                && nwjsEngine == "Nwjs"
                && genericGameFolderEngine == "Unknown"
                && renpyUnlockerScope
                && fastDetection
                && fastRootLookup
                && contextualGui
                && nwjsExtraction
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
            bool readableDisabledButtons = HasReadableDisabledContrast(startButton)
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

            return initial && unity && renpy && rpgm && unreal && nwjs && readableDisabledButtons && tooltips && dpi && dragHighlight && expanded && collapsed;
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
