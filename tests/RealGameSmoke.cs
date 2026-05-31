using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class RealGameSmoke
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3 || !File.Exists(args[0]) || !Directory.Exists(args[1]))
        {
            Console.Error.WriteLine("Usage: RealGameSmoke.exe <GameAssetTool.exe> <games-root> <output-root> [--extract] [--allow-wolf-cli]");
            return 2;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        string gamesRoot = Path.GetFullPath(args[1]);
        string outputRoot = Path.GetFullPath(args[2]);
        bool extract = args.Any(delegate(string value) { return value.Equals("--extract", StringComparison.OrdinalIgnoreCase); });
        bool allowWolfCli = args.Any(delegate(string value) { return value.Equals("--allow-wolf-cli", StringComparison.OrdinalIgnoreCase); });
        string gameFilter = GetOptionValue(args, "--game");
        Directory.CreateDirectory(outputRoot);

        Assembly assembly = Assembly.LoadFile(assemblyPath);
        Type formType = assembly.GetType("RpgmvpConverterWinForms.RpgmvpConverterForm", true);
        MethodInfo detectEngine = RequireMethod(formType, "DetectEngine", PrivateStatic);
        MethodInfo buildScanSummary = RequireMethod(formType, "BuildScanSummary", PrivateStatic);
        object form = Activator.CreateInstance(formType);

        int failures = 0;
        try
        {
            foreach (string game in EnumerateInputs(gamesRoot).OrderBy(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(gameFilter)
                    && !Path.GetFileName(game).Equals(gameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                string engine = Convert.ToString(detectEngine.Invoke(null, new object[] { game }));
                object summary = buildScanSummary.Invoke(null, new object[] { game });
                int archives = GetInt(summary, "ArchiveCount");
                int candidates = GetInt(summary, "FileCount");
                long inputBytes = GetLong(summary, "TotalBytes");
                Console.WriteLine("AUDIT|{0}|{1}|archives={2}|candidates={3}|bytes={4}", Path.GetFileName(game), engine, archives, candidates, inputBytes);

                if (!extract) continue;
                try
                {
                    SmokeResult result = ExtractGame(formType, form, game, engine, outputRoot, allowWolfCli);
                    Console.WriteLine("RESULT|{0}|{1}|status={2}|files={3}|bytes={4}|errors={5}|renamed={6}|skipped={7}|output={8}",
                        Path.GetFileName(game),
                        engine,
                        result.Status,
                        result.Extracted,
                        result.Bytes,
                        result.Errors,
                        result.Renamed,
                        result.Skipped,
                        result.OutputDir);
                    if (result.Status == "failed" || result.Errors > 0) failures++;
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.WriteLine("RESULT|{0}|{1}|status=failed|error={2}", Path.GetFileName(game), engine, Unwrap(ex).Message);
                }
            }
        }
        finally
        {
            IDisposable disposable = form as IDisposable;
            if (disposable != null) disposable.Dispose();
            Cleanup(assembly, "RpgmvpConverterWinForms.PortableRuntime");
            Cleanup(assembly, "RpgmvpConverterWinForms.ToolRuntime");
        }

        Console.WriteLine("SUMMARY|failures={0}|output={1}", failures, outputRoot);
        return failures == 0 ? 0 : 1;
    }

    private static SmokeResult ExtractGame(Type formType, object form, string game, string engine, string outputRoot, bool allowWolfCli)
    {
        string gameOutput = Path.Combine(outputRoot, Sanitize(Path.GetFileName(game)));
        Directory.CreateDirectory(gameOutput);

        if (engine == "RpgMaker")
            return RunRpgmSample(formType, form, game, gameOutput);
        if (engine == "Renpy")
        {
            string gameFolder = Path.Combine(game, "game");
            List<string> archives = Directory.Exists(gameFolder)
                ? Directory.GetFiles(gameFolder, "*.rpa", SearchOption.AllDirectories).ToList()
                : new List<string>();
            if (archives.Count == 0)
                return SmokeResult.CreateSkipped(gameOutput, "loose Ren'Py files; no RPA archive");
            return FromOperation(Invoke(formType, form, "RunRenpyExtraction", game, archives, Path.Combine(gameOutput, "renpy")));
        }
        if (engine == "Unity")
            return FromOperation(Invoke(formType, form, "RunUnityExtraction", game, Path.Combine(gameOutput, "unity"), "all", true));
        if (engine == "Unreal")
            return FromOperation(Invoke(formType, form, "RunPortableScriptExtraction", game, Path.Combine(gameOutput, "unreal"), "Unreal experimental", "extract_unreal.py", "RpgmvpConverterWinForms.scripts.extract_unreal.py", ""));
        if (engine == "Nwjs")
            return FromOperation(Invoke(formType, form, "RunNwjsExtraction", game, Path.Combine(gameOutput, "nwjs")));
        if (engine == "WolfRpg")
        {
            IList archives = (IList)InvokeStatic(formType, "FindWolfArchiveFiles", game);
            if (archives.Count > 0 && !allowWolfCli)
                return SmokeResult.CreateSkipped(gameOutput, "encrypted WOLF archives require --allow-wolf-cli");
            return FromOperation(Invoke(formType, form, "RunWolfExtraction", game, Path.Combine(gameOutput, "wolf")));
        }
        if (engine == "TyranoScript")
            return FromOperation(Invoke(formType, form, "RunTyranoExtraction", game, Path.Combine(gameOutput, "tyrano")));
        if (engine == "JavaJar")
            return FromOperation(Invoke(formType, form, "RunJavaExtraction", game, Path.Combine(gameOutput, "java")));
        if (engine == "Flash")
            return FromOperation(Invoke(formType, form, "RunFlashExtraction", game, Path.Combine(gameOutput, "flash")));
        if (engine == "Html")
            return RunCollector(formType.Assembly, "GetHtmlFiles", game, Path.Combine(gameOutput, "html"));
        if (engine == "Qsp")
            return RunCollector(formType.Assembly, "GetQspFiles", game, Path.Combine(gameOutput, "qsp"));
        if (engine == "Rags")
            return FromCollector(InvokeCollectorStatic(formType.Assembly, "ExtractRags", game, Path.Combine(gameOutput, "rags")), Path.Combine(gameOutput, "rags"));
        return SmokeResult.CreateSkipped(gameOutput, "unsupported or undetected");
    }

    private static IEnumerable<string> EnumerateInputs(string gamesRoot)
    {
        return Directory.GetDirectories(gamesRoot)
            .Concat(Directory.GetFiles(gamesRoot, "*.rag", SearchOption.TopDirectoryOnly));
    }

    private static SmokeResult RunCollector(Assembly assembly, string listMethod, string game, string output)
    {
        object files = InvokeCollectorStatic(assembly, listMethod, game, output);
        return FromCollector(InvokeCollectorStatic(assembly, "CopyFiles", game, files, output, ""), output);
    }

    private static SmokeResult RunRpgmSample(Type formType, object form, string game, string gameOutput)
    {
        List<string> sourceFiles = ((IEnumerable<string>)InvokeStatic(formType, "GetFilesToConvert", game)).Take(5).ToList();
        if (sourceFiles.Count == 0)
            return SmokeResult.CreateSkipped(gameOutput, "no encrypted RPGM images");

        string key = Convert.ToString(InvokeStatic(formType, "TryFindKey", game));
        if (string.IsNullOrWhiteSpace(key))
            key = Convert.ToString(Invoke(formType, form, "TryReconstructKey", game));
        if (string.IsNullOrWhiteSpace(key))
            return SmokeResult.Failed(gameOutput, "RPGM key not found");

        byte[] keyBytes = (byte[])InvokeStatic(formType, "ParseKey", key);
        string staging = Path.Combine(gameOutput, "rpgm-sample-source");
        Directory.CreateDirectory(staging);
        List<string> stagedFiles = new List<string>();
        foreach (string source in sourceFiles)
        {
            string relative = source.Substring(game.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destination = Path.Combine(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            stagedFiles.Add(destination);
        }

        Type runType = formType.GetNestedType("ConversionRun", BindingFlags.NonPublic);
        object run = Activator.CreateInstance(runType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { staging, stagedFiles, keyBytes }, null);
        RequireMethod(runType, "Start", BindingFlags.Public | BindingFlags.Instance).Invoke(run, null);
        ((Task)GetProperty(run, "Completion")).Wait();
        return new SmokeResult(
            "ok",
            Convert.ToString(GetProperty(run, "OutputDir")),
            GetInt(run, "ProcessedCount") - GetInt(run, "ErrorCount"),
            GetLong(run, "TotalBytes"),
            GetInt(run, "ErrorCount"),
            GetInt(run, "RenamedCount"),
            0);
    }

    private static object Invoke(Type type, object target, string name, params object[] args)
    {
        try
        {
            return RequireMethod(type, name, PrivateInstance).Invoke(target, args);
        }
        catch (TargetInvocationException ex)
        {
            throw Unwrap(ex);
        }
    }

    private static object InvokeStatic(Type type, string name, params object[] args)
    {
        try
        {
            return RequireMethod(type, name, PrivateStatic).Invoke(null, args);
        }
        catch (TargetInvocationException ex)
        {
            throw Unwrap(ex);
        }
    }

    private static object InvokeCollectorStatic(Assembly assembly, string name, params object[] args)
    {
        try
        {
            Type collectors = assembly.GetType("RpgmvpConverterWinForms.AssetCollectors", true);
            return RequireMethod(collectors, name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }
        catch (TargetInvocationException ex)
        {
            throw Unwrap(ex);
        }
    }

    private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags)
    {
        MethodInfo method = type.GetMethod(name, flags);
        if (method == null) throw new MissingMethodException(type.FullName, name);
        return method;
    }

    private static SmokeResult FromOperation(object result)
    {
        return new SmokeResult(
            GetInt(result, "Errors") == 0 ? "ok" : "warnings",
            Convert.ToString(GetProperty(result, "OutputDir")),
            GetInt(result, "Extracted"),
            GetLong(result, "Bytes"),
            GetInt(result, "Errors"),
            GetInt(result, "Renamed"),
            GetInt(result, "Skipped"));
    }

    private static SmokeResult FromCollector(object result, string output)
    {
        return new SmokeResult(
            "ok",
            output,
            GetInt(result, "Extracted"),
            GetLong(result, "Bytes"),
            0,
            GetInt(result, "Renamed"),
            GetInt(result, "Skipped"));
    }

    private static object GetProperty(object value, string name)
    {
        return value.GetType().GetProperty(name).GetValue(value, null);
    }

    private static int GetInt(object value, string name)
    {
        return Convert.ToInt32(GetProperty(value, name));
    }

    private static long GetLong(object value, string name)
    {
        return Convert.ToInt64(GetProperty(value, name));
    }

    private static Exception Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
        return ex;
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value;
    }

    private static string GetOptionValue(string[] args, string option)
    {
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return "";
    }

    private static void Cleanup(Assembly assembly, string typeName)
    {
        try
        {
            Type type = assembly.GetType(typeName, false);
            if (type == null) return;
            MethodInfo cleanup = type.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static);
            if (cleanup != null) cleanup.Invoke(null, null);
        }
        catch { }
    }

    private sealed class SmokeResult
    {
        public SmokeResult(string status, string outputDir, int extracted, long bytes, int errors, int renamed, int skipped)
        {
            Status = status;
            OutputDir = outputDir;
            Extracted = extracted;
            Bytes = bytes;
            Errors = errors;
            Renamed = renamed;
            Skipped = skipped;
        }

        public string Status { get; private set; }
        public string OutputDir { get; private set; }
        public int Extracted { get; private set; }
        public long Bytes { get; private set; }
        public int Errors { get; private set; }
        public int Renamed { get; private set; }
        public int Skipped { get; private set; }

        public static SmokeResult CreateSkipped(string outputDir, string reason)
        {
            return new SmokeResult("skipped: " + reason, outputDir, 0, 0, 0, 0, 0);
        }

        public static SmokeResult Failed(string outputDir, string reason)
        {
            return new SmokeResult("failed: " + reason, outputDir, 0, 0, 1, 0, 0);
        }
    }
}
