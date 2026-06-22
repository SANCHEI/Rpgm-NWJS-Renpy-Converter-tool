using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class SkippedDiagnosticsBuilder
    {
        private static readonly string[] DiagnosticNames =
        {
            "GameAssetTool-zip-diagnostics.tsv",
            "GameAssetTool-apk-diagnostics.txt",
            "Unreal-skipped-files.txt",
            "SPAK-DAT-manifest.txt",
            "GameAssetTool-wolf-diagnostics.txt"
        };

        public static bool HasDetails(string outputDir, string reportPath, int errors, int skipped)
        {
            if (errors > 0 || skipped > 0) return true;
            return FindDiagnosticFiles(outputDir).Count > 0;
        }

        public static string Build(string outputDir, string reportPath)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Game Asset Tool skipped/error details");
            builder.AppendLine("Output: " + outputDir);
            builder.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();

            int contentStart = builder.Length;
            AppendReportSummary(builder, reportPath);
            foreach (string file in FindDiagnosticFiles(outputDir))
                AppendFile(builder, outputDir, file, 220);

            if (builder.Length == contentStart)
                builder.AppendLine("No extra diagnostics were found.");
            return builder.ToString();
        }

        private static void AppendReportSummary(StringBuilder builder, string reportPath)
        {
            if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath)) return;
            try
            {
                List<string> lines = File.ReadAllLines(reportPath, Encoding.UTF8)
                    .Where(delegate(string line)
                    {
                        return line.StartsWith("Engine:", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("Skipped items:", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("Errors:", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("Unknown extensions:", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("DAT diagnostics:", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("APK diagnostics:", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
                if (lines.Count == 0) return;
                builder.AppendLine("Report summary:");
                foreach (string line in lines) builder.AppendLine(line);
                builder.AppendLine();
            }
            catch { }
        }

        private static List<string> FindDiagnosticFiles(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir))
                return new List<string>();
            try
            {
                return DiagnosticNames
                    .SelectMany(delegate(string name)
                    {
                        return Directory.EnumerateFiles(outputDir, name, SearchOption.AllDirectories);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void AppendFile(StringBuilder builder, string outputDir, string path, int maxLines)
        {
            try
            {
                builder.AppendLine("== " + MakeRelativePathSafe(outputDir, path) + " ==");
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int written = 0;
                foreach (string line in lines)
                {
                    if (written >= maxLines)
                    {
                        builder.AppendLine("... (" + Math.Max(0, lines.Length - written) + " more line(s))");
                        break;
                    }
                    if (ShouldInclude(path, line))
                    {
                        builder.AppendLine(line);
                        written++;
                    }
                }
                if (written == 0) builder.AppendLine("(no skipped/error lines found)");
                builder.AppendLine();
            }
            catch (Exception ex)
            {
                builder.AppendLine("Could not read diagnostics: " + path + ": " + ex.Message);
                builder.AppendLine();
            }
        }

        private static bool ShouldInclude(string path, string line)
        {
            string name = Path.GetFileName(path);
            if (name.Equals("SPAK-DAT-manifest.txt", StringComparison.OrdinalIgnoreCase))
                return line.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
            if (name.Equals("GameAssetTool-wolf-diagnostics.txt", StringComparison.OrdinalIgnoreCase))
                return line.IndexOf("Errors:", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("Skipped:", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.StartsWith("- ", StringComparison.Ordinal);
            return true;
        }

        private static string MakeRelativePathSafe(string rootPath, string path)
        {
            try
            {
                Uri root = new Uri(AppendDirectorySeparator(Path.GetFullPath(rootPath)));
                Uri file = new Uri(Path.GetFullPath(path));
                return Uri.UnescapeDataString(root.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return path;
            }
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }
    }
}
