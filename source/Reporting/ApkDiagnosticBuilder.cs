using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class ApkDiagnosticBuilder
    {
        public static ApkDiagnosticSummary Analyze(string archivePath, string extractedPrefix)
        {
            List<ApkEntryInfo> entries = ReadEntries(archivePath);
            List<string> names = entries.Select(delegate(ApkEntryInfo entry) { return entry.Name; }).ToList();
            List<string> hints = DetectHints(names);
            string route = BuildSuggestedRoute(hints, names, extractedPrefix);
            string topExtensions = BuildTopExtensions(entries);
            return new ApkDiagnosticSummary(archivePath, entries.Count, hints, route, topExtensions);
        }

        public static string BuildReport(string archivePath, string extractedPrefix)
        {
            ApkDiagnosticSummary summary = Analyze(archivePath, extractedPrefix);
            StringBuilder report = new StringBuilder();
            report.AppendLine("Game Asset Tool APK diagnostics");
            report.AppendLine("APK: " + archivePath);
            report.AppendLine("Entries: " + summary.EntryCount);
            report.AppendLine("Detected inside: " + summary.DetectedText);
            report.AppendLine("Suggested route: " + summary.SuggestedRoute);
            report.AppendLine("Suggested action: " + summary.ActionText);
            report.AppendLine();
            report.AppendLine("Top asset extensions:");
            report.AppendLine(string.IsNullOrWhiteSpace(summary.TopExtensions) ? "unknown" : summary.TopExtensions);
            report.AppendLine();
            report.AppendLine("Route notes:");
            report.AppendLine("- APK recovery keeps common inner engine containers such as Unity .assets/.bundle/.unity3d/.ress, Godot .pck and GameMaker data.win when they are present.");
            report.AppendLine("- After extraction, run the suggested extractor on the extracted APK folder/file when the direct media output is not enough.");
            return report.ToString();
        }

        public static string BuildDryRunSummary(IEnumerable<string> archives, int limit)
        {
            if (archives == null) return "";
            List<string> parts = new List<string>();
            foreach (string archive in archives.Take(Math.Max(limit, 1)))
            {
                try
                {
                    ApkDiagnosticSummary summary = Analyze(archive, "");
                    parts.Add(Path.GetFileName(archive) + ": " + summary.DetectedText + " -> " + summary.SuggestedRoute);
                }
                catch (Exception ex)
                {
                    parts.Add(Path.GetFileName(archive) + ": unreadable APK (" + ex.Message + ")");
                }
            }
            return string.Join("; ", parts.ToArray());
        }

        public static ApkFollowupAction TryReadSuggestedFollowup(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir)) return null;
            string path = Path.Combine(outputDir, "GameAssetTool-apk-diagnostics.txt");
            if (!File.Exists(path)) return null;
            try
            {
                string route = "";
                string action = "";
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("Suggested route:", StringComparison.OrdinalIgnoreCase))
                        route = line.Substring("Suggested route:".Length).Trim();
                    else if (line.StartsWith("Suggested action:", StringComparison.OrdinalIgnoreCase))
                        action = line.Substring("Suggested action:".Length).Trim();
                }

                ApkFollowupAction parsed = ParseAction(outputDir, action);
                if (parsed != null) return parsed;
                return ParseRoute(outputDir, route);
            }
            catch
            {
                return null;
            }
        }

        private static List<ApkEntryInfo> ReadEntries(string archivePath)
        {
            List<ApkEntryInfo> entries = new List<ApkEntryInfo>();
            using (FileStream stream = File.OpenRead(archivePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ArchiveSafetyPolicy.ValidateZipArchive(archive);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    entries.Add(new ApkEntryInfo(entry.FullName.Replace('\\', '/'), entry.Length));
                }
            }
            return entries;
        }

        private static List<string> DetectHints(List<string> names)
        {
            List<string> hints = new List<string>();
            if (names.Any(delegate(string name)
            {
                return name.IndexOf("libunity", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("globalgamemanagers", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase);
            }))
                hints.Add("Unity");
            if (names.Any(delegate(string name) { return name.EndsWith(".pck", StringComparison.OrdinalIgnoreCase) || name.IndexOf("libgodot", StringComparison.OrdinalIgnoreCase) >= 0; }))
                hints.Add("Godot");
            if (names.Any(delegate(string name) { return name.Equals("assets/www/index.html", StringComparison.OrdinalIgnoreCase) || name.EndsWith("/package.json", StringComparison.OrdinalIgnoreCase); }))
                hints.Add("HTML/NWJS-like");
            if (names.Any(delegate(string name) { return name.EndsWith("data.win", StringComparison.OrdinalIgnoreCase); }))
                hints.Add("GameMaker");
            if (names.Any(delegate(string name) { return name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase); }))
                hints.Add("Java/JAR");
            return hints;
        }

        private static string BuildSuggestedRoute(List<string> hints, List<string> names, string extractedPrefix)
        {
            if (hints.Contains("Unity"))
                return "Unity extractor on " + CombineRoute(extractedPrefix, FindUnityDataFolder(names));
            if (hints.Contains("Godot"))
                return "Godot extractor on " + CombineRoute(extractedPrefix, FindFirst(names, ".pck"));
            if (hints.Contains("GameMaker"))
                return "GameMaker extractor on " + CombineRoute(extractedPrefix, FindDataWin(names));
            if (hints.Contains("HTML/NWJS-like"))
                return "NWJS/HTML extractor on " + CombineRoute(extractedPrefix, "assets/www");
            if (hints.Contains("Java/JAR"))
                return "Java/JAR extractor on " + CombineRoute(extractedPrefix, FindFirst(names, ".jar"));
            return "Signature recovery / manual inspection";
        }

        internal static string BuildActionText(string suggestedRoute)
        {
            ApkFollowupAction action = ParseRoute("", suggestedRoute);
            if (action == null) return "none";
            return "engine=" + action.EngineKey + ";path=" + action.RelativePath.Replace('\\', '/');
        }

        private static ApkFollowupAction ParseAction(string outputDir, string action)
        {
            if (string.IsNullOrWhiteSpace(action) || action.Equals("none", StringComparison.OrdinalIgnoreCase))
                return null;
            string engine = "";
            string relative = "";
            foreach (string part in action.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pieces = part.Split(new[] { '=' }, 2);
                if (pieces.Length != 2) continue;
                if (pieces[0].Trim().Equals("engine", StringComparison.OrdinalIgnoreCase)) engine = pieces[1].Trim();
                else if (pieces[0].Trim().Equals("path", StringComparison.OrdinalIgnoreCase)) relative = pieces[1].Trim();
            }
            if (string.IsNullOrWhiteSpace(engine) || string.IsNullOrWhiteSpace(relative)) return null;
            return CreateFollowup(outputDir, engine, relative);
        }

        private static ApkFollowupAction ParseRoute(string outputDir, string route)
        {
            if (string.IsNullOrWhiteSpace(route)) return null;
            return TryParseRoute(outputDir, route, "Unity extractor on ", "Unity")
                ?? TryParseRoute(outputDir, route, "Godot extractor on ", "Godot")
                ?? TryParseRoute(outputDir, route, "GameMaker extractor on ", "GameMaker")
                ?? TryParseRoute(outputDir, route, "NWJS/HTML extractor on ", "Html")
                ?? TryParseRoute(outputDir, route, "Java/JAR extractor on ", "JavaJar");
        }

        private static ApkFollowupAction TryParseRoute(string outputDir, string route, string prefix, string engine)
        {
            if (!route.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string relative = route.Substring(prefix.Length).Trim();
            if (relative.StartsWith("*", StringComparison.Ordinal)) return null;
            return CreateFollowup(outputDir, engine, relative);
        }

        private static ApkFollowupAction CreateFollowup(string outputDir, string engine, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) return null;
            string normalized = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string target = string.IsNullOrWhiteSpace(outputDir)
                ? normalized
                : Path.GetFullPath(Path.Combine(outputDir, normalized));
            if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(target) && !File.Exists(target)) return null;
            return new ApkFollowupAction(engine, normalized, target);
        }

        private static string FindUnityDataFolder(List<string> names)
        {
            foreach (string name in names)
            {
                int index = name.IndexOf("assets/bin/Data/", StringComparison.OrdinalIgnoreCase);
                if (index >= 0) return name.Substring(0, index + "assets/bin/Data".Length);
            }
            return "assets/bin/Data";
        }

        private static string FindDataWin(List<string> names)
        {
            string found = names.FirstOrDefault(delegate(string name) { return name.EndsWith("data.win", StringComparison.OrdinalIgnoreCase); });
            return string.IsNullOrWhiteSpace(found) ? "data.win" : found;
        }

        private static string FindFirst(List<string> names, string extension)
        {
            string found = names.FirstOrDefault(delegate(string name) { return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase); });
            return string.IsNullOrWhiteSpace(found) ? "*" + extension : found;
        }

        private static string CombineRoute(string prefix, string relative)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return relative;
            if (string.IsNullOrWhiteSpace(relative)) return prefix;
            return (prefix.TrimEnd('/', '\\') + "/" + relative.TrimStart('/', '\\')).Replace('\\', '/');
        }

        private static string BuildTopExtensions(List<ApkEntryInfo> entries)
        {
            return string.Join(Environment.NewLine, entries
                .Select(delegate(ApkEntryInfo entry)
                {
                    string extension = Path.GetExtension(entry.Name);
                    return string.IsNullOrWhiteSpace(extension) ? "<no extension>" : extension.ToLowerInvariant();
                })
                .GroupBy(delegate(string extension) { return extension; }, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(delegate(IGrouping<string, string> group) { return group.Count(); })
                .ThenBy(delegate(IGrouping<string, string> group) { return group.Key; }, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .Select(delegate(IGrouping<string, string> group) { return group.Key + " x" + group.Count(); })
                .ToArray());
        }

        private sealed class ApkEntryInfo
        {
            public ApkEntryInfo(string name, long length)
            {
                Name = name;
                Length = length;
            }

            public string Name { get; private set; }
            public long Length { get; private set; }
        }
    }

    internal sealed class ApkDiagnosticSummary
    {
        public ApkDiagnosticSummary(string archivePath, int entryCount, List<string> detected, string suggestedRoute, string topExtensions)
        {
            ArchivePath = archivePath;
            EntryCount = entryCount;
            Detected = detected ?? new List<string>();
            SuggestedRoute = string.IsNullOrWhiteSpace(suggestedRoute) ? "Signature recovery / manual inspection" : suggestedRoute;
            ActionText = ApkDiagnosticBuilder.BuildActionText(SuggestedRoute);
            TopExtensions = topExtensions ?? "";
        }

        public string ArchivePath { get; private set; }
        public int EntryCount { get; private set; }
        public List<string> Detected { get; private set; }
        public string SuggestedRoute { get; private set; }
        public string ActionText { get; private set; }
        public string TopExtensions { get; private set; }
        public string DetectedText
        {
            get { return Detected.Count > 0 ? string.Join(", ", Detected.ToArray()) : "unknown"; }
        }
    }

    internal sealed class ApkFollowupAction
    {
        public ApkFollowupAction(string engineKey, string relativePath, string targetPath)
        {
            EngineKey = engineKey ?? "";
            RelativePath = relativePath ?? "";
            TargetPath = targetPath ?? "";
        }

        public string EngineKey { get; private set; }
        public string RelativePath { get; private set; }
        public string TargetPath { get; private set; }
    }
}
