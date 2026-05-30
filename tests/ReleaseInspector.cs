using System;
using System.Diagnostics;
using System.IO;
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

            return hasUnityScript
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
}
